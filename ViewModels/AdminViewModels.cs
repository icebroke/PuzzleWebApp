using System.ComponentModel.DataAnnotations;
using PuzzleWebApp.Models;

namespace PuzzleWebApp.ViewModels;

public class AdminLoginViewModel
{
    [Required]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 12)]
    public string Password { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class AdminChangePasswordViewModel
{
    [Required]
    [DataType(DataType.Password)]
    [StringLength(200)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [StringLength(200, MinimumLength = 12)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class AdminPuzzleListViewModel
{
    public List<AdminPuzzleSummaryViewModel> Puzzles { get; set; } = [];
}

public class AdminPuzzleSummaryViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Rows { get; set; }
    public int Columns { get; set; }
    public int ClueCount { get; set; }
    public int AttemptCount { get; set; }
    public bool IsLive { get; set; }
    public bool ManualActivation { get; set; }
    public DateTime? ScheduledStartAtUtc { get; set; }
    public DateTime? ScheduledEndAtUtc { get; set; }
    public string ScheduleLabel { get; set; } = string.Empty;
}

public class AdminPuzzleEditViewModel
{
    public int? Id { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(160)]
    public string Title { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    public DateTime? ScheduledStartAt { get; set; }
    public DateTime? ScheduledEndAt { get; set; }

    public List<AdminPuzzleClueEditViewModel> Clues { get; set; } = [];
}

public class AdminPuzzleClueEditViewModel
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(240)]
    public string Text { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(30)]
    public string Answer { get; set; } = string.Empty;
}

public class AdminOverviewViewModel
{
    public string CrosswordTitle { get; set; } = string.Empty;
    public int TotalAttempts { get; set; }
    public int SubmittedAttempts { get; set; }
    public int TotalCorrectAnswers { get; set; }
    public int TotalSubmittedAnswers { get; set; }
    public string SearchTerm { get; set; } = string.Empty;
    public string StatusFilter { get; set; } = string.Empty;
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalFilteredAttempts { get; set; }
    public int TotalPages => Math.Max(1, (TotalFilteredAttempts + PageSize - 1) / PageSize);
    public int FirstItemNumber => TotalFilteredAttempts == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
    public int LastItemNumber => Math.Min(CurrentPage * PageSize, TotalFilteredAttempts);
    public List<AdminPlayerSummaryViewModel> Attempts { get; set; } = [];
}

public class AdminPlayerSummaryViewModel
{
    public string PlayerName { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public int? RoundOneAttemptId { get; set; }
    public int RoundOneCorrectCount { get; set; }
    public int RoundOneTotalClues { get; set; }
    public bool RoundOneSubmitted { get; set; }
    public int? RoundTwoAttemptId { get; set; }
    public int RoundTwoCorrectCount { get; set; }
    public int RoundTwoTotalClues { get; set; }
    public bool RoundTwoSubmitted { get; set; }
    public bool RoundTwoCreativeSubmitted { get; set; }
    public int? CreativeJudgmentOrder { get; set; }
    public List<int> CreativeJudgmentOptions { get; set; } = [];
    public int TotalCorrectCount => RoundOneCorrectCount + RoundTwoCorrectCount;
    public bool IsComplete => RoundOneSubmitted && RoundTwoSubmitted;
    public bool IsTiedOnTotal { get; set; }
    public int? OverallPlace { get; set; }
}

public class AdminAttemptDetailViewModel
{
    public string CrosswordTitle { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string CreativeAnswer { get; set; } = string.Empty;
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
