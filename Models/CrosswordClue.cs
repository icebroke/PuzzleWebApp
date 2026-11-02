namespace PuzzleWebApp.Models;

public class CrosswordClue
{
    public int Id { get; set; }
    public int CrosswordId { get; set; }
    public int Number { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int StartRow { get; set; }
    public int StartColumn { get; set; }
    public CrosswordDirection Direction { get; set; }

    public Crossword Crossword { get; set; } = null!;
}
