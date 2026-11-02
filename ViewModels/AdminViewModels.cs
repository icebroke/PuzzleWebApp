using System.ComponentModel.DataAnnotations;
using PuzzleWebApp.Models;

namespace PuzzleWebApp.ViewModels;

public class AdminLoginViewModel
{
    [StringLength(200)]
    public string AccessCode { get; set; } = string.Empty;
    public bool IsConfigured { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AdminOverviewViewModel
{
    public string CrosswordTitle { get; set; } = string.Empty;
    public int TotalAttempts { get; set; }
    public int SubmittedAttempts { get; set; }
    public int TotalCorrectAnswers { get; set; }
    public int TotalSubmittedAnswers { get; set; }
    public List<AdminAttemptSummaryViewModel> Attempts { get; set; } = [];
}

public class AdminAttemptSummaryViewModel
{
    public int Id { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public int AnsweredCount { get; set; }
    public int CorrectCount { get; set; }
    public int TotalClues { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
}

public class AdminAttemptDetailViewModel
{
    public string CrosswordTitle { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public DateTime? SubmittedAtUtc { get; set; }
    public int CorrectCount { get; set; }
    public int AnsweredCount { get; set; }
    public int TotalClues { get; set; }
    public List<AdminClueResultViewModel> Clues { get; set; } = [];
}

public class AdminClueResultViewModel
{
    public int Number { get; set; }
    public string Text { get; set; } = string.Empty;
    public string PlayerAnswer { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public CrosswordDirection Direction { get; set; }
    public bool IsAnswered { get; set; }
    public bool IsCorrect { get; set; }
}
