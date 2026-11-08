using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PuzzleWebApp.Data;
using PuzzleWebApp.Models;
using PuzzleWebApp.Services;
using PuzzleWebApp.ViewModels;

namespace PuzzleWebApp.Controllers;

[Authorize]
public class AdminController : Controller
{
    private const string MustChangePasswordClaim = "admin_must_change_password";
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<AdminUser> _passwordHasher;

    public AdminController(AppDbContext db, IPasswordHasher<AdminUser> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var actionName = context.ActionDescriptor.RouteValues["action"];
        if (User.HasClaim(MustChangePasswordClaim, "true")
            && actionName is not nameof(ChangePassword) and not nameof(Logout))
        {
            context.Result = RedirectToAction(nameof(ChangePassword));
            return;
        }

        await next();
    }

    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Index));
        }

        return View(new AdminLoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("admin-login")]
    [ValidateAntiForgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Login(AdminLoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedUsername = AdminUserSeed.NormalizeUsername(model.Username);
        var admin = await _db.AdminUsers.SingleOrDefaultAsync(user => user.NormalizedUsername == normalizedUsername);
        var passwordMatches = admin is not null
            && _passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, model.Password)
                is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        if (!passwordMatches || admin is null)
        {
            model.Password = string.Empty;
            model.ErrorMessage = "Nama pengguna atau kata laluan tidak sah.";
            return View(model);
        }

        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, admin.Id.ToString(CultureInfo.InvariantCulture)),
            new(System.Security.Claims.ClaimTypes.Name, admin.Username)
        };
        if (admin.MustChangePassword)
        {
            claims.Add(new System.Security.Claims.Claim(MustChangePasswordClaim, "true"));
        }

        var identity = new System.Security.Claims.ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new System.Security.Claims.ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });
        return RedirectToAction(admin.MustChangePassword ? nameof(ChangePassword) : nameof(Index));
    }

    [HttpGet]
    public IActionResult ChangePassword() => View(new AdminChangePasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(AdminChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userId, NumberStyles.None, CultureInfo.InvariantCulture, out var adminId))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var admin = await _db.AdminUsers.SingleOrDefaultAsync(user => user.Id == adminId);
        if (admin is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var currentPasswordValid = _passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, model.CurrentPassword)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        if (!currentPasswordValid)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Kata laluan semasa tidak sah.");
            model.CurrentPassword = string.Empty;
            model.NewPassword = string.Empty;
            model.ConfirmPassword = string.Empty;
            return View(model);
        }

        var newPasswordIsCurrent = _passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, model.NewPassword)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        if (newPasswordIsCurrent)
        {
            ModelState.AddModelError(nameof(model.NewPassword), "Kata laluan baharu mesti berbeza daripada kata laluan semasa.");
            model.CurrentPassword = string.Empty;
            model.NewPassword = string.Empty;
            model.ConfirmPassword = string.Empty;
            return View(model);
        }

        admin.PasswordHash = _passwordHasher.HashPassword(admin, model.NewPassword);
        admin.MustChangePassword = false;
        await _db.SaveChangesAsync();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["AdminPasswordChanged"] = true;
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Index(string? q, string? status, int page = 1)
    {
        var crosswords = await _db.Crosswords
            .AsNoTracking()
            .Include(crossword => crossword.Clues)
            .Where(crossword => crossword.RoundNumber == 1 || crossword.RoundNumber == 2)
            .ToListAsync();
        var crosswordIds = crosswords.Select(crossword => crossword.Id).ToList();
        var crosswordById = crosswords.ToDictionary(crossword => crossword.Id);
        var attempts = await _db.CrosswordAttempts
            .AsNoTracking()
            .Where(attempt => crosswordIds.Contains(attempt.CrosswordId))
            .ToListAsync();

        var summaries = attempts
            .GroupBy(attempt => attempt.PlayerId.Trim().ToUpperInvariant())
            .Select(playerAttempts =>
        {
            var roundOne = playerAttempts
                .Where(attempt => crosswordById[attempt.CrosswordId].RoundNumber == 1)
                .OrderByDescending(attempt => attempt.StartedAtUtc)
                .FirstOrDefault();
            var roundTwo = playerAttempts
                .Where(attempt => crosswordById[attempt.CrosswordId].RoundNumber == 2)
                .OrderByDescending(attempt => attempt.StartedAtUtc)
                .FirstOrDefault();
            var roundOneSubmitted = roundOne?.SubmittedAtUtc.HasValue == true;
            var roundTwoSubmitted = roundTwo?.SubmittedAtUtc.HasValue == true;
            var roundOneClues = roundOne is null ? 0 : crosswordById[roundOne.CrosswordId].Clues.Count;
            var roundTwoClues = roundTwo is null ? 0 : crosswordById[roundTwo.CrosswordId].Clues.Count;
            var roundOneLetters = roundOne is null ? new Dictionary<string, string>() : DeserializeAnswers(roundOne.AnswersJson);
            var roundTwoLetters = roundTwo is null ? new Dictionary<string, string>() : DeserializeAnswers(roundTwo.AnswersJson);
            return new AdminPlayerSummaryViewModel
            {
                PlayerName = roundTwo?.PlayerName ?? roundOne?.PlayerName ?? string.Empty,
                PlayerId = roundTwo?.PlayerId ?? roundOne?.PlayerId ?? string.Empty,
                RoundOneAttemptId = roundOne?.Id,
                RoundOneCorrectCount = roundOneSubmitted
                    ? crosswordById[roundOne!.CrosswordId].Clues.Count(clue => CrosswordScoring.IsClueCorrect(clue, roundOneLetters))
                    : 0,
                RoundOneTotalClues = roundOneClues,
                RoundOneSubmitted = roundOneSubmitted,
                RoundOneTimeTaken = roundOne is not null && roundOne.SubmittedAtUtc.HasValue
                    ? roundOne.SubmittedAtUtc.Value - roundOne.StartedAtUtc
                    : null,
                RoundTwoAttemptId = roundTwo?.Id,
                RoundTwoCorrectCount = roundTwoSubmitted
                    ? crosswordById[roundTwo!.CrosswordId].Clues.Count(clue => CrosswordScoring.IsClueCorrect(clue, roundTwoLetters))
                    : 0,
                RoundTwoTotalClues = roundTwoClues,
                RoundTwoSubmitted = roundTwoSubmitted,
                RoundTwoTimeTaken = roundTwo is not null && roundTwo.SubmittedAtUtc.HasValue
                    ? roundTwo.SubmittedAtUtc.Value - roundTwo.StartedAtUtc
                    : null,
                RoundTwoCreativeSubmitted = roundTwo?.CreativeSubmittedAtUtc.HasValue == true,
                CreativeJudgmentOrder = roundTwo?.CreativeJudgmentOrder
            };
        }).ToList();
        var standings = summaries.Where(player => player.IsComplete)
            .OrderByDescending(player => player.TotalCorrectCount)
            .ToList();
        foreach (var tiedPlayers in standings.GroupBy(player => player.TotalCorrectCount).Where(group => group.Count() > 1))
        {
            foreach (var player in tiedPlayers)
            {
                player.IsTiedOnTotal = true;
            }
        }

        var nextPlace = 1;
        foreach (var tiedPlayers in standings.GroupBy(player => player.TotalCorrectCount))
        {
            var tieGroup = tiedPlayers.ToList();
            var judgmentIsComplete = tieGroup.Count == 1
                || tieGroup.All(player => player.CreativeJudgmentOrder.HasValue)
                    && tieGroup.Select(player => player.CreativeJudgmentOrder!.Value).Order().SequenceEqual(Enumerable.Range(1, tieGroup.Count));
            foreach (var player in tieGroup)
            {
                player.CreativeJudgmentOptions = Enumerable.Range(1, tieGroup.Count).ToList();
            }
            if (judgmentIsComplete)
            {
                var placedPlayers = tieGroup.Count == 1
                    ? tieGroup
                    : tieGroup.OrderBy(player => player.CreativeJudgmentOrder).ToList();
                foreach (var player in placedPlayers)
                {
                    player.OverallPlace = nextPlace++;
                }
            }
            else
            {
                nextPlace += tieGroup.Count;
            }
        }

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
            filteredAttempts = filteredAttempts.Where(player => player.IsComplete);
        }
        else if (statusFilter == "in-progress")
        {
            filteredAttempts = filteredAttempts.Where(player => !player.IsComplete);
        }

        var filteredList = filteredAttempts
            .OrderBy(player => player.OverallPlace ?? int.MaxValue)
            .ThenByDescending(player => player.TotalCorrectCount)
            .ThenBy(player => player.PlayerName)
            .ToList();
        var totalFilteredAttempts = filteredList.Count;
        const int pageSize = 20;
        var totalPages = Math.Max(1, (totalFilteredAttempts + pageSize - 1) / pageSize);
        var currentPage = Math.Clamp(page, 1, totalPages);

        return View(new AdminOverviewViewModel
        {
            CrosswordTitle = "Keputusan Pusingan 1 & 2",
            TotalAttempts = summaries.Count,
            SubmittedAttempts = summaries.Count(player => player.IsComplete),
            TotalCorrectAnswers = summaries.Where(player => player.IsComplete).Sum(player => player.TotalCorrectCount),
            TotalSubmittedAnswers = summaries.Where(player => player.IsComplete)
                .Sum(player => player.RoundOneTotalClues + player.RoundTwoTotalClues),
            SearchTerm = searchTerm,
            StatusFilter = statusFilter,
            CurrentPage = currentPage,
            PageSize = pageSize,
            TotalFilteredAttempts = totalFilteredAttempts,
            Attempts = filteredList
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCreativeJudgmentPlace(int attemptId, int? creativeJudgmentOrder, string? q, string? status, int page = 1)
    {
        if (creativeJudgmentOrder < 1)
        {
            creativeJudgmentOrder = null;
        }

        var attempt = await _db.CrosswordAttempts
            .Include(existing => existing.Crossword)
            .SingleOrDefaultAsync(existing => existing.Id == attemptId);
        if (attempt is null
            || attempt.Crossword.RoundNumber != 2
            || !attempt.SubmittedAtUtc.HasValue
            || !attempt.CreativeSubmittedAtUtc.HasValue)
        {
            return BadRequest();
        }

        attempt.CreativeJudgmentOrder = creativeJudgmentOrder;
        await _db.SaveChangesAsync();
        TempData["AdminNotice"] = "Kedudukan penilaian kreativiti telah dikemas kini.";
        return RedirectToAction(nameof(Index), new { q, status, page });
    }

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Puzzles()
    {
        var nowUtc = DateTime.UtcNow;
        var liveId = await CrosswordAvailability.GetAvailablePuzzles(_db.Crosswords, nowUtc)
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
                IsLive = crossword.Id == liveId,
                ManualActivation = crossword.ManualActivation,
                ScheduledStartAtUtc = crossword.ScheduledStartAtUtc,
                ScheduledEndAtUtc = crossword.ScheduledEndAtUtc
            })
            .ToListAsync();

        foreach (var puzzle in puzzles)
        {
            var start = CrosswordAvailability.ToMalaysiaTime(puzzle.ScheduledStartAtUtc);
            var end = CrosswordAvailability.ToMalaysiaTime(puzzle.ScheduledEndAtUtc);
            puzzle.ScheduleLabel = start.HasValue && end.HasValue
                ? $"{start.Value:dd MMM yyyy, HH:mm} hingga {end.Value:dd MMM yyyy, HH:mm} (MYT)"
                : "Jadual belum ditetapkan";
        }

        return View(new AdminPuzzleListViewModel { Puzzles = puzzles });
    }

    [HttpGet]
    public IActionResult CreatePuzzle()
    {
        return View("PuzzleForm", new AdminPuzzleEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePuzzle(AdminPuzzleEditViewModel model)
    {
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
            ScheduledStartAtUtc = CrosswordAvailability.ToUtc(model.ScheduledStartAt),
            ScheduledEndAtUtc = CrosswordAvailability.ToUtc(model.ScheduledEndAt),
            Clues = layout.Clues
        });
        await _db.SaveChangesAsync();
        TempData["AdminNotice"] = "Teka silang kata baharu telah ditambah. Ia hanya dibuka mengikut jadual atau pengaktifan manual.";
        return RedirectToAction(nameof(Puzzles));
    }

    [HttpGet]
    public async Task<IActionResult> EditPuzzle(int id)
    {
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
        crossword.ScheduledStartAtUtc = CrosswordAvailability.ToUtc(model.ScheduledStartAt);
        crossword.ScheduledEndAtUtc = CrosswordAvailability.ToUtc(model.ScheduledEndAt);
        await using var transaction = await _db.Database.BeginTransactionAsync();
        _db.CrosswordClues.RemoveRange(crossword.Clues);
        await _db.SaveChangesAsync();
        crossword.Clues = layout.Clues;
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["AdminNotice"] = "Perubahan teka silang kata telah disimpan.";
        return RedirectToAction(nameof(Puzzles));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleManualActivation(int id)
    {
        var crossword = await _db.Crosswords.SingleOrDefaultAsync(item => item.Id == id);
        if (crossword is null)
        {
            return NotFound();
        }

        if (crossword.ManualActivation)
        {
            crossword.ManualActivation = false;
            TempData["AdminNotice"] = "Pengaktifan manual telah dimatikan.";
        }
        else
        {
            await _db.Crosswords
                .Where(item => item.ManualActivation)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.ManualActivation, false));
            crossword.ManualActivation = true;
            TempData["AdminNotice"] = "Teka silang kata telah diaktifkan secara manual.";
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Puzzles));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPuzzleGame(int id)
    {
        var crossword = await _db.Crosswords
            .AsNoTracking()
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
    public async Task<IActionResult> ResetPuzzleGameConfirmed(int id)
    {
        if (!await _db.Crosswords.AnyAsync(item => item.Id == id))
        {
            return NotFound();
        }

        var deletedAttempts = await _db.CrosswordAttempts
            .Where(attempt => attempt.CrosswordId == id)
            .ExecuteDeleteAsync();
        TempData["AdminNotice"] = $"Permainan telah ditetapkan semula. {deletedAttempts} rekod percubaan pemain dipadam.";
        return RedirectToAction(nameof(Puzzles));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPlayerGame(int id)
    {
        var attempt = await _db.CrosswordAttempts
            .AsNoTracking()
            .Include(existing => existing.Crossword)
            .SingleOrDefaultAsync(existing => existing.Id == id);
        if (attempt is null)
        {
            return NotFound();
        }

        return View(new AdminAttemptDetailViewModel
        {
            AttemptId = attempt.Id,
            RoundNumber = attempt.Crossword.RoundNumber,
            CrosswordTitle = attempt.Crossword.Title,
            PlayerName = attempt.PlayerName,
            PlayerId = attempt.PlayerId,
            SubmittedAtUtc = attempt.SubmittedAtUtc
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPlayerGameConfirmed(int id)
    {
        var attempt = await _db.CrosswordAttempts
            .Include(existing => existing.Crossword)
            .SingleOrDefaultAsync(existing => existing.Id == id);
        if (attempt is null)
        {
            return NotFound();
        }

        var playerName = attempt.PlayerName;
        var playerId = attempt.PlayerId;
        var roundNumber = attempt.Crossword.RoundNumber;
        _db.CrosswordAttempts.Remove(attempt);
        await _db.SaveChangesAsync();

        TempData["AdminNotice"] = $"Percubaan Pusingan {roundNumber} untuk {playerName} telah ditetapkan semula. Percubaan pemain lain tidak berubah.";
        return RedirectToAction(nameof(Index), new { q = playerId });
    }

    [HttpGet]
    public async Task<IActionResult> DeletePuzzle(int id)
    {
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
            AttemptId = attempt.Id,
            RoundNumber = attempt.Crossword.RoundNumber,
            CrosswordTitle = attempt.Crossword.Title,
            PlayerName = attempt.PlayerName,
            PlayerId = attempt.PlayerId,
            CreativeAnswer = attempt.CreativeAnswer,
            StartedAtUtc = attempt.StartedAtUtc,
            SubmittedAtUtc = attempt.SubmittedAtUtc,
            CorrectCount = results.Count(result => result.IsCorrect),
            AnsweredCount = results.Count(result => result.IsAnswered),
            TotalClues = results.Count,
            Clues = results
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private IQueryable<Crossword> GetNewestCrosswordQuery() => _db.Crosswords
        .OrderByDescending(crossword => crossword.CreatedAtUtc)
        .ThenByDescending(crossword => crossword.Id);

    private CrosswordLayout? BuildPuzzleLayout(AdminPuzzleEditViewModel model)
    {
        model.Title = model.Title?.Trim() ?? string.Empty;
        model.Description = model.Description?.Trim() ?? string.Empty;
        model.Clues ??= [];

        if (model.ScheduledStartAt.HasValue != model.ScheduledEndAt.HasValue)
        {
            ModelState.AddModelError(nameof(model.ScheduledEndAt), "Reset kedua-dua tarikh mula dan tamat, atau kosongkan kedua-duanya.");
        }
        else if (model.ScheduledStartAt.HasValue && model.ScheduledEndAt <= model.ScheduledStartAt)
        {
            ModelState.AddModelError(nameof(model.ScheduledEndAt), "Tarikh tamat mesti selepas tarikh mula.");
        }

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
        ScheduledStartAt = CrosswordAvailability.ToMalaysiaTime(crossword.ScheduledStartAtUtc),
        ScheduledEndAt = CrosswordAvailability.ToMalaysiaTime(crossword.ScheduledEndAtUtc),
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
