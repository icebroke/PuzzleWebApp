namespace PuzzleWebApp.Models;

public class CrosswordAttempt
{
    public int Id { get; set; }
    public int CrosswordId { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string AnswersJson { get; set; } = "{}";
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSavedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAtUtc { get; set; }
    public string CreativeAnswer { get; set; } = string.Empty;
    public DateTime? CreativeStartedAtUtc { get; set; }
    public DateTime? CreativeSubmittedAtUtc { get; set; }
    public int? CreativeJudgmentOrder { get; set; }

    public Crossword Crossword { get; set; } = null!;
}
