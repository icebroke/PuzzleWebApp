namespace PuzzleWebApp.ViewModels;

public class RateLimitErrorViewModel
{
    public string ReturnUrl { get; set; } = "/";
    public int RetryAfterSeconds { get; set; }
}
