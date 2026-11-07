namespace PuzzleWebApp.Models;

public class RoundQuestion
{
    public int Id { get; set; }
    public int RoundNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string AnswerGuidance { get; set; } = string.Empty;
}