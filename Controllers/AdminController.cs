using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PuzzleWebApp.Data;
using PuzzleWebApp.Models;
using PuzzleWebApp.Services;
using PuzzleWebApp.ViewModels;

namespace PuzzleWebApp.Controllers;

public class AdminController : Controller
{
    private const string AdminSessionKey = "CrosswordAdminAuthenticated";
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public AdminController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Login()
    {
        if (IsAuthenticated())
        {
            return RedirectToAction(nameof(Index));
        }

        return View(new AdminLoginViewModel
        {
            IsConfigured = !string.IsNullOrWhiteSpace(_configuration["Admin:AccessCode"])
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Login(AdminLoginViewModel model)
    {
        var configuredCode = _configuration["Admin:AccessCode"];
        model.IsConfigured = !string.IsNullOrWhiteSpace(configuredCode);
        if (!model.IsConfigured)
        {
            model.ErrorMessage = "Akses pentadbir belum dikonfigurasikan. Tetapkan pemboleh ubah persekitaran Admin__AccessCode sebelum log masuk.";
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var expected = Encoding.UTF8.GetBytes(configuredCode!);
        var provided = Encoding.UTF8.GetBytes(model.AccessCode ?? string.Empty);
        if (!CryptographicOperations.FixedTimeEquals(expected, provided))
        {
            model.AccessCode = string.Empty;
            model.ErrorMessage = "Kod akses tidak sah.";
            return View(model);
        }

        HttpContext.Session.SetString(AdminSessionKey, "true");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Index(string? q, string? status, int page = 1)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction(nameof(Login));
        }

        var crossword = await GetLiveCrosswordQuery()
            .AsNoTracking()
            .Include(item => item.Clues)
            .FirstAsync();
        var attempts = await _db.CrosswordAttempts
            .AsNoTracking()
            .Where(attempt => attempt.CrosswordId == crossword.Id)
            .OrderByDescending(attempt => attempt.SubmittedAtUtc)
            .ThenBy(attempt => attempt.PlayerName)
            .ToListAsync();

        var summaries = attempts.Select(attempt =>
        {
            var letters = DeserializeAnswers(attempt.AnswersJson);
            return new AdminAttemptSummaryViewModel
            {
                Id = attempt.Id,
                PlayerName = attempt.PlayerName,
                PlayerId = attempt.PlayerId,
                AnsweredCount = crossword.Clues.Count(clue => CrosswordScoring.IsClueAnswered(clue, letters)),
                CorrectCount = crossword.Clues.Count(clue => CrosswordScoring.IsClueCorrect(clue, letters)),
                TotalClues = crossword.Clues.Count,
                SubmittedAtUtc = attempt.SubmittedAtUtc
            };
        }).ToList();
        var submitted = summaries.Where(attempt => attempt.SubmittedAtUtc.HasValue).ToList();
        var searchTerm = q?.Trim() ?? string.Empty;
        var statusFilter = status is "submitted" or "in-progress" ? status : string.Empty;
        var filteredAttempts = summaries.AsEnumerable();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            filteredAttempts = filteredAttempts.Where(attempt =>
                attempt.PlayerName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                || attempt.PlayerId.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (statusFilter == "submitted")
        {
            filteredAttempts = filteredAttempts.Where(attempt => attempt.SubmittedAtUtc.HasValue);
        }
        else if (statusFilter == "in-progress")
        {
            filteredAttempts = filteredAttempts.Where(attempt => !attempt.SubmittedAtUtc.HasValue);
        }

        var totalFilteredAttempts = filteredAttempts.Count();
        const int pageSize = 20;
        var totalPages = Math.Max(1, (totalFilteredAttempts + pageSize - 1) / pageSize);
        var currentPage = Math.Clamp(page, 1, totalPages);

        return View(new AdminOverviewViewModel
        {
            CrosswordTitle = crossword.Title,
            TotalAttempts = summaries.Count,
            SubmittedAttempts = submitted.Count,
            TotalCorrectAnswers = submitted.Sum(attempt => attempt.CorrectCount),
            TotalSubmittedAnswers = submitted.Sum(attempt => attempt.TotalClues),
            SearchTerm = searchTerm,
            StatusFilter = statusFilter,
            CurrentPage = currentPage,
            PageSize = pageSize,
            TotalFilteredAttempts = totalFilteredAttempts,
            Attempts = filteredAttempts
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList()
        });
    }

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Puzzles()
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction(nameof(Login));
        }

        var liveId = await GetLiveCrosswordQuery()
            .Select(crossword => (int?)crossword.Id)
            .FirstOrDefaultAsync();
        var puzzles = await _db.Crosswords
            .AsNoTracking()
            .OrderByDescending(crossword => crossword.CreatedAtUtc)
            .ThenByDescending(crossword => crossword.Id)
            .Select(crossword => new AdminPuzzleSummaryViewModel
            {
                Id = crossword.Id,
                Title = crossword.Title,
                Description = crossword.Description,
                Rows = crossword.Rows,
                Columns = crossword.Columns,
                ClueCount = crossword.Clues.Count,
                AttemptCount = crossword.Attempts.Count,
                IsLive = crossword.Id == liveId
            })
            .ToListAsync();

        return View(new AdminPuzzleListViewModel { Puzzles = puzzles });
    }

    [HttpGet]
    public IActionResult CreatePuzzle()
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction(nameof(Login));
        }

        return View("PuzzleForm", new AdminPuzzleEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePuzzle(AdminPuzzleEditViewModel model)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction(nameof(Login));
        }

        var layout = BuildPuzzleLayout(model);
        if (!ModelState.IsValid || layout is null)
        {
            return View("PuzzleForm", model);
        }

        _db.Crosswords.Add(new Crossword
        {
            Title = model.Title,
            Description = model.Description,
            Rows = layout.Rows,
            Columns = layout.Columns,
            Clues = layout.Clues
        });
        await _db.SaveChangesAsync();
        TempData["AdminNotice"] = "Teka silang kata baharu telah ditambah dan kini aktif.";
        return RedirectToAction(nameof(Puzzles));
    }

    [HttpGet]
    public async Task<IActionResult> EditPuzzle(int id)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction(nameof(Login));
        }

        var crossword = await _db.Crosswords.AsNoTracking()
            .Include(item => item.Clues)
            .SingleOrDefaultAsync(item => item.Id == id);
        if (crossword is null)
        {
            return NotFound();
        }

        return View("PuzzleForm", ToEditModel(crossword));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPuzzle(int id, AdminPuzzleEditViewModel model)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction(nameof(Login));
        }

        var crossword = await _db.Crosswords
            .Include(item => item.Clues)
            .SingleOrDefaultAsync(item => item.Id == id);
        if (crossword is null)
        {
            return NotFound();
        }

        model.Id = id;
        var layout = BuildPuzzleLayout(model);
        if (!ModelState.IsValid || layout is null)
        {
            return View("PuzzleForm", model);
        }

        crossword.Title = model.Title;
        crossword.Description = model.Description;
        crossword.Rows = layout.Rows;
        crossword.Columns = layout.Columns;
        await using var transaction = await _db.Database.BeginTransactionAsync();
        _db.CrosswordClues.RemoveRange(crossword.Clues);
        await _db.SaveChangesAsync();
        crossword.Clues = layout.Clues;
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["AdminNotice"] = "Perubahan teka silang kata telah disimpan.";
        return RedirectToAction(nameof(Puzzles));
    }

    [HttpGet]
    public async Task<IActionResult> DeletePuzzle(int id)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction(nameof(Login));
        }

        if (await _db.Crosswords.CountAsync() <= 1)
        {
            TempData["AdminNotice"] = "Sekurang-kurangnya satu teka silang kata mesti dikekalkan.";
            return RedirectToAction(nameof(Puzzles));
        }

        var crossword = await _db.Crosswords.AsNoTracking()
            .Select(item => new AdminPuzzleSummaryViewModel
            {
                Id = item.Id,
                Title = item.Title,
                Description = item.Description,
                Rows = item.Rows,
                Columns = item.Columns,
                ClueCount = item.Clues.Count,
                AttemptCount = item.Attempts.Count
            })
            .SingleOrDefaultAsync(item => item.Id == id);
        return crossword is null ? NotFound() : View(crossword);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePuzzleConfirmed(int id)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction(nameof(Login));
        }

        if (await _db.Crosswords.CountAsync() <= 1)
        {
            TempData["AdminNotice"] = "Sekurang-kurangnya satu teka silang kata mesti dikekalkan.";
            return RedirectToAction(nameof(Puzzles));
        }

        var crossword = await _db.Crosswords.SingleOrDefaultAsync(item => item.Id == id);
        if (crossword is null)
        {
            return NotFound();
        }

        _db.Crosswords.Remove(crossword);
        await _db.SaveChangesAsync();
        TempData["AdminNotice"] = "Teka silang kata dan semua rekod percubaannya telah dipadam.";
        return RedirectToAction(nameof(Puzzles));
    }

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Attempt(int id)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction(nameof(Login));
        }

        var attempt = await _db.CrosswordAttempts
            .AsNoTracking()
            .Include(existing => existing.Crossword)
            .ThenInclude(crossword => crossword.Clues)
            .SingleOrDefaultAsync(existing => existing.Id == id);
        if (attempt is null)
        {
            return NotFound();
        }

        var letters = DeserializeAnswers(attempt.AnswersJson);
        var results = attempt.Crossword.Clues
            .OrderBy(clue => clue.Number)
            .ThenBy(clue => clue.Direction)
            .Select(clue => new AdminClueResultViewModel
            {
                Number = clue.Number,
                Text = clue.Text,
                Direction = clue.Direction,
                PlayerAnswer = CrosswordScoring.GetClueAnswer(clue, letters),
                CorrectAnswer = clue.Answer,
                IsAnswered = CrosswordScoring.IsClueAnswered(clue, letters),
                IsCorrect = CrosswordScoring.IsClueCorrect(clue, letters)
            })
            .ToList();

        return View(new AdminAttemptDetailViewModel
        {
            CrosswordTitle = attempt.Crossword.Title,
            PlayerName = attempt.PlayerName,
            PlayerId = attempt.PlayerId,
            SubmittedAtUtc = attempt.SubmittedAtUtc,
            CorrectCount = results.Count(result => result.IsCorrect),
            AnsweredCount = results.Count(result => result.IsAnswered),
            TotalClues = results.Count,
            Clues = results
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Remove(AdminSessionKey);
        return RedirectToAction(nameof(Login));
    }

    private bool IsAuthenticated() =>
        HttpContext.Session.GetString(AdminSessionKey) == "true";

    private IQueryable<Crossword> GetLiveCrosswordQuery() => _db.Crosswords
        .OrderByDescending(crossword => crossword.CreatedAtUtc)
        .ThenByDescending(crossword => crossword.Id);

    private CrosswordLayout? BuildPuzzleLayout(AdminPuzzleEditViewModel model)
    {
        model.Title = model.Title?.Trim() ?? string.Empty;
        model.Description = model.Description?.Trim() ?? string.Empty;
        model.Clues ??= [];
        foreach (var clue in model.Clues)
        {
            clue.Text = clue.Text?.Trim() ?? string.Empty;
            clue.Answer = new string((clue.Answer ?? string.Empty)
                .Where(character => !char.IsWhiteSpace(character))
                .Select(char.ToUpperInvariant)
                .ToArray());
        }

        if (model.Clues.Count < 2)
        {
            ModelState.AddModelError(nameof(model.Clues), "Masukkan sekurang-kurangnya dua petunjuk supaya jawapan boleh bersilang.");
            return null;
        }

        for (var index = 0; index < model.Clues.Count; index++)
        {
            var answer = model.Clues[index].Answer;
            if (answer.Length < 2 || answer.Any(letter => letter is < 'A' or > 'Z'))
            {
                ModelState.AddModelError($"Clues[{index}].Answer", "Jawapan mesti mengandungi sekurang-kurangnya dua huruf A hingga Z.");
            }
        }

        if (!ModelState.IsValid)
        {
            return null;
        }

        var entries = model.Clues.Select(clue => (clue.Text, clue.Answer)).ToList();
        if (!CrosswordLayoutBuilder.TryBuild(entries, out var layout))
        {
            ModelState.AddModelError(nameof(model.Clues), "Sistem tidak dapat menyusun jawapan ini sebagai teka silang kata. Pastikan jawapan berkongsi huruf yang boleh disilang.");
            return null;
        }

        return layout;
    }

    private static AdminPuzzleEditViewModel ToEditModel(Crossword crossword) => new()
    {
        Id = crossword.Id,
        Title = crossword.Title,
        Description = crossword.Description,
        Clues = crossword.Clues.OrderBy(clue => clue.Number).ThenBy(clue => clue.Direction)
            .Select(clue => new AdminPuzzleClueEditViewModel
            {
                Text = clue.Text,
                Answer = clue.Answer
            }).ToList()
    };

    private static Dictionary<string, string> DeserializeAnswers(string answersJson) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(answersJson)
        ?? throw new InvalidOperationException("The saved crossword progress is not valid JSON.");
}
