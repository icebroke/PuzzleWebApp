namespace PuzzleWebApp.Models;

public class Crossword
{
    public int Id { get; set; }
    public int RoundNumber { get; set; } = 1;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Rows { get; set; }
    public int Columns { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ScheduledStartAtUtc { get; set; }
    public DateTime? ScheduledEndAtUtc { get; set; }
    public bool ManualActivation { get; set; }

    public ICollection<CrosswordClue> Clues { get; set; } = new List<CrosswordClue>();
    public ICollection<CrosswordAttempt> Attempts { get; set; } = new List<CrosswordAttempt>();
}
