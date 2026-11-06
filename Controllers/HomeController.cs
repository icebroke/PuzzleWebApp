using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using PuzzleWebApp.Data;
using PuzzleWebApp.Models;
using PuzzleWebApp.Services;
using PuzzleWebApp.ViewModels;

namespace PuzzleWebApp.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var crossword = await GetLiveCrosswordQuery().AsNoTracking().FirstAsync();
        return View(new CrosswordJoinViewModel
        {
            CrosswordTitle = crossword.Title,
            CrosswordDescription = crossword.Description
        });
    }

    [HttpGet]
    [DisableRateLimiting]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult RateLimitError(string? returnUrl, int retryAfterSeconds = 1)
    {
        return View(new RateLimitErrorViewModel
        {
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action(nameof(Index))!,
            RetryAfterSeconds = Math.Clamp(retryAfterSeconds, 1, 60)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(CrosswordJoinViewModel model)
    {
        model.PlayerName = model.PlayerName?.Trim() ?? string.Empty;
        model.PlayerId = model.PlayerId?.Trim() ?? string.Empty;

        if (!ModelState.IsValid)
        {
            var crossword = await GetLiveCrosswordQuery().AsNoTracking().FirstAsync();
            model.CrosswordTitle = crossword.Title;
            model.CrosswordDescription = crossword.Description;
            return View("Index", model);
        }

        var currentCrossword = await GetLiveCrosswordQuery().FirstAsync();
        var normalizedPlayerId = model.PlayerId.ToUpperInvariant();
        var attempt = await _db.CrosswordAttempts.SingleOrDefaultAsync(existing =>
            existing.CrosswordId == currentCrossword.Id &&
            existing.PlayerId == normalizedPlayerId);

        if (attempt is null)
        {
            attempt = new CrosswordAttempt
            {
                CrosswordId = currentCrossword.Id,
                PlayerId = normalizedPlayerId,
                PlayerName = model.PlayerName,
                StartedAtUtc = DateTime.UtcNow
            };
            _db.CrosswordAttempts.Add(attempt);
        }
        else
        {
            attempt.PlayerName = model.PlayerName;
        }

        await _db.SaveChangesAsync();
        HttpContext.Session.SetInt32("CrosswordAttemptId", attempt.Id);
        return RedirectToAction(nameof(Play));
    }

    public async Task<IActionResult> Play()
    {
        var attemptId = HttpContext.Session.GetInt32("CrosswordAttemptId");
        if (attemptId is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var attempt = await _db.CrosswordAttempts
            .Include(existing => existing.Crossword)
            .ThenInclude(crossword => crossword.Clues)
            .SingleOrDefaultAsync(existing => existing.Id == attemptId.Value);

        if (attempt is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var deadline = GetDeadline(attempt);
        if (!attempt.SubmittedAtUtc.HasValue && DateTime.UtcNow >= deadline)
        {
            attempt.SubmittedAtUtc = deadline;
            await _db.SaveChangesAsync();
        }

        var answers = JsonSerializer.Deserialize<Dictionary<string, string>>(attempt.AnswersJson)
            ?? throw new InvalidOperationException("The saved crossword progress is not valid JSON.");
        var clues = attempt.Crossword.Clues.OrderBy(clue => clue.Number).ThenBy(clue => clue.Direction).ToList();
        var cells = Enumerable.Range(0, attempt.Crossword.Rows * attempt.Crossword.Columns)
            .Select(index => new CrosswordCellViewModel
            {
                Row = index / attempt.Crossword.Columns,
                Column = index % attempt.Crossword.Columns
            })
            .ToArray();

        foreach (var clue in clues)
        {
            var key = GetClueKey(clue);
            for (var index = 0; index < clue.Answer.Length; index++)
            {
                var row = clue.StartRow + (clue.Direction == CrosswordDirection.Down ? index : 0);
                var column = clue.StartColumn + (clue.Direction == CrosswordDirection.Across ? index : 0);
                var cell = cells[row * attempt.Crossword.Columns + column];
                cell.IsPlayable = true;
                cell.ClueNumber ??= index == 0 ? clue.Number : null;
                if (clue.Direction == CrosswordDirection.Across)
                {
                    cell.AcrossKey = key;
                }
                else
                {
                    cell.DownKey = key;
                }
            }
        }

        foreach (var cell in cells.Where(cell => cell.IsPlayable))
        {
            answers.TryGetValue(CrosswordScoring.GetCellKey(cell.Row, cell.Column), out var letter);
            cell.Letter = letter;
        }

        var timeExpired = attempt.SubmittedAtUtc.HasValue && attempt.SubmittedAtUtc.Value >= deadline;
        var remainingSeconds = attempt.SubmittedAtUtc.HasValue
            ? 0
            : Math.Max(0, (int)Math.Ceiling((deadline - DateTime.UtcNow).TotalSeconds));

        return View(new CrosswordPlayViewModel
        {
            CrosswordTitle = attempt.Crossword.Title,
            CrosswordDescription = attempt.Crossword.Description,
            PlayerName = attempt.PlayerName,
            Rows = attempt.Crossword.Rows,
            Columns = attempt.Crossword.Columns,
            AnsweredCount = attempt.Crossword.Clues.Count(clue => CrosswordScoring.IsClueAnswered(clue, answers)),
            TimeLimitMinutes = attempt.Crossword.Clues.Count,
            RemainingSeconds = remainingSeconds,
            IsSubmitted = attempt.SubmittedAtUtc.HasValue,
            TimeExpired = timeExpired,
            LastSavedAtUtc = attempt.LastSavedAtUtc,
            Cells = cells.ToList(),
            Clues = clues.Select(clue => new CrosswordClueViewModel
            {
                Number = clue.Number,
                Key = GetClueKey(clue),
                Text = clue.Text,
                Direction = clue.Direction,
                Length = clue.Answer.Length,
                StartRow = clue.StartRow,
                StartColumn = clue.StartColumn,
            }).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProgress([FromBody] SaveCrosswordProgressRequest? request)
    {
        var attemptId = HttpContext.Session.GetInt32("CrosswordAttemptId");
        if (attemptId is null)
        {
            return Unauthorized(new { message = "Sesi permainan anda telah tamat. Masukkan semula ID pemain untuk menyambung." });
        }

        if (request?.Letters is null)
        {
            return BadRequest(new { message = "Tiada kemajuan teka silang kata diterima." });
        }

        var attempt = await _db.CrosswordAttempts
            .Include(existing => existing.Crossword)
            .ThenInclude(crossword => crossword.Clues)
            .SingleOrDefaultAsync(existing => existing.Id == attemptId.Value);

        if (attempt is null)
        {
            return NotFound(new { message = "Permainan yang disimpan tidak ditemui. Kembali ke halaman utama dan masukkan ID pemain sekali lagi." });
        }

        if (attempt.SubmittedAtUtc.HasValue)
        {
            return Conflict(new { message = "Teka silang kata ini telah dihantar dan tidak boleh diubah lagi." });
        }

        if (DateTime.UtcNow >= GetDeadline(attempt))
        {
            return Conflict(new { message = "Masa keseluruhan teka silang kata telah tamat.", timedOut = true });
        }

        if (!TryNormalizeAnswers(attempt.Crossword, request.Letters, out var letters, out var error))
        {
            return BadRequest(new { message = error });
        }

        attempt.AnswersJson = JsonSerializer.Serialize(letters);
        attempt.LastSavedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Json(new
        {
            savedAtUtc = attempt.LastSavedAtUtc,
            answeredCount = attempt.Crossword.Clues.Count(clue => CrosswordScoring.IsClueAnswered(clue, letters)),
            totalClues = attempt.Crossword.Clues.Count
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit([FromBody] SubmitCrosswordRequest? request)
    {
        var attemptId = HttpContext.Session.GetInt32("CrosswordAttemptId");
        if (attemptId is null)
        {
            return Unauthorized(new { message = "Sesi permainan anda telah tamat. Masukkan semula ID pemain untuk menyambung." });
        }

        if (request?.Letters is null)
        {
            return BadRequest(new { message = "Tiada jawapan teka silang kata diterima." });
        }

        var attempt = await _db.CrosswordAttempts
            .Include(existing => existing.Crossword)
            .ThenInclude(crossword => crossword.Clues)
            .SingleOrDefaultAsync(existing => existing.Id == attemptId.Value);

        if (attempt is null)
        {
            return NotFound(new { message = "Permainan yang disimpan tidak ditemui. Kembali ke halaman utama dan masukkan ID pemain sekali lagi." });
        }

        if (attempt.SubmittedAtUtc.HasValue)
        {
            var savedAnswers = JsonSerializer.Deserialize<Dictionary<string, string>>(attempt.AnswersJson)
                ?? throw new InvalidOperationException("The saved crossword progress is not valid JSON.");
            var submittedDeadline = GetDeadline(attempt);
            return Json(new
            {
                submittedAtUtc = attempt.SubmittedAtUtc,
                answeredCount = attempt.Crossword.Clues.Count(clue => CrosswordScoring.IsClueAnswered(clue, savedAnswers)),
                timedOut = attempt.SubmittedAtUtc.Value >= submittedDeadline
            });
        }

        if (!TryNormalizeAnswers(attempt.Crossword, request.Letters, out var letters, out var error))
        {
            return BadRequest(new { message = error });
        }

        var answeredCount = attempt.Crossword.Clues.Count(clue => CrosswordScoring.IsClueAnswered(clue, letters));
        var submittedAtUtc = DateTime.UtcNow;
        var deadline = GetDeadline(attempt);
        if (submittedAtUtc >= deadline)
        {
            attempt.AnswersJson = JsonSerializer.Serialize(letters);
            attempt.LastSavedAtUtc = submittedAtUtc;
            attempt.SubmittedAtUtc = deadline;
            await _db.SaveChangesAsync();
            return Json(new { submittedAtUtc = attempt.SubmittedAtUtc, answeredCount, timedOut = true });
        }

        if (answeredCount == 0)
        {
            return BadRequest(new { message = "Jawab sekurang-kurangnya satu petunjuk dengan lengkap sebelum menghantar." });
        }

        attempt.AnswersJson = JsonSerializer.Serialize(letters);
        attempt.LastSavedAtUtc = submittedAtUtc;
        attempt.SubmittedAtUtc = submittedAtUtc;
        await _db.SaveChangesAsync();

        return Json(new { submittedAtUtc = attempt.SubmittedAtUtc, answeredCount, timedOut = false });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private static string GetClueKey(CrosswordClue clue) => $"{clue.Direction}-{clue.Number}";

    private IOrderedQueryable<Crossword> GetLiveCrosswordQuery() => _db.Crosswords
        .OrderByDescending(crossword => crossword.CreatedAtUtc)
        .ThenByDescending(crossword => crossword.Id);

    private static DateTime GetDeadline(CrosswordAttempt attempt) =>
        attempt.StartedAtUtc.AddMinutes(attempt.Crossword.Clues.Count);

    private static bool TryNormalizeAnswers(
        Crossword crossword,
        IReadOnlyDictionary<string, string> submittedLetters,
        out Dictionary<string, string> letters,
        out string error)
    {
        var allowedCells = crossword.Clues
            .SelectMany(clue => Enumerable.Range(0, clue.Answer.Length).Select(index =>
                CrosswordScoring.GetCellKey(
                    clue.StartRow + (clue.Direction == CrosswordDirection.Down ? index : 0),
                    clue.StartColumn + (clue.Direction == CrosswordDirection.Across ? index : 0))))
            .ToHashSet(StringComparer.Ordinal);
        letters = new Dictionary<string, string>(StringComparer.Ordinal);
        error = string.Empty;

        foreach (var (key, value) in submittedLetters)
        {
            if (!allowedCells.Contains(key))
            {
                error = "Kemajuan yang dihantar mengandungi petak di luar teka silang kata ini.";
                return false;
            }

            var normalizedLetter = value?.Trim().ToUpperInvariant() ?? string.Empty;
            if (normalizedLetter.Length != 1 || !char.IsLetter(normalizedLetter[0]))
            {
                error = "Setiap petak mesti mengandungi satu huruf sahaja.";
                return false;
            }

            letters.Add(key, normalizedLetter);
        }

        return true;
    }
}
