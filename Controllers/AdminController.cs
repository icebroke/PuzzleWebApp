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
            Clues = layout.Clues
        });
        await _db.SaveChangesAsync();
        TempData["AdminNotice"] = "Teka silang kata baharu telah ditambah dan kini aktif.";
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
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

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
