using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PuzzleWebApp.Data;
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

        var crossword = await _db.Crosswords
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

    private static Dictionary<string, string> DeserializeAnswers(string answersJson) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(answersJson)
        ?? throw new InvalidOperationException("The saved crossword progress is not valid JSON.");
}
