using PuzzleWebApp.Models;

namespace PuzzleWebApp.ViewModels;

public class CrosswordPlayViewModel
{
    public string CrosswordTitle { get; set; } = string.Empty;
    public string CrosswordDescription { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public int Rows { get; set; }
    public int Columns { get; set; }
    public int AnsweredCount { get; set; }
    public int TimeLimitMinutes { get; set; }
    public int RemainingSeconds { get; set; }
    public bool IsSubmitted { get; set; }
    public bool TimeExpired { get; set; }
    public DateTime? LastSavedAtUtc { get; set; }
    public List<CrosswordCellViewModel> Cells { get; set; } = [];
    public List<CrosswordClueViewModel> Clues { get; set; } = [];
}

public class CrosswordCellViewModel
{
    public int Row { get; set; }
    public int Column { get; set; }
    public bool IsPlayable { get; set; }
    public int? ClueNumber { get; set; }
    public string? Letter { get; set; }
    public string? AcrossKey { get; set; }
    public string? DownKey { get; set; }
}

public class CrosswordClueViewModel
{
    public int Number { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public CrosswordDirection Direction { get; set; }
    public int Length { get; set; }
    public int StartRow { get; set; }
    public int StartColumn { get; set; }
}

public class SaveCrosswordProgressRequest
{
    public Dictionary<string, string> Letters { get; set; } = [];
}

public class SubmitCrosswordRequest
{
    public Dictionary<string, string> Letters { get; set; } = [];
}
