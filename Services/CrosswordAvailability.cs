using PuzzleWebApp.Models;

namespace PuzzleWebApp.Services;

public static class CrosswordAvailability
{
    public static IOrderedQueryable<Crossword> GetAvailablePuzzles(
        IQueryable<Crossword> puzzles,
        DateTime nowUtc) => puzzles
        .Where(puzzle => puzzle.ManualActivation
            || (puzzle.ScheduledStartAtUtc.HasValue
                && puzzle.ScheduledEndAtUtc.HasValue
                && puzzle.ScheduledStartAtUtc.Value <= nowUtc
                && puzzle.ScheduledEndAtUtc.Value > nowUtc))
        .OrderByDescending(puzzle => puzzle.ManualActivation)
        .ThenByDescending(puzzle => puzzle.CreatedAtUtc)
        .ThenByDescending(puzzle => puzzle.Id);

    public static bool IsAvailable(Crossword puzzle, DateTime nowUtc) =>
        puzzle.ManualActivation
        || (puzzle.ScheduledStartAtUtc.HasValue
            && puzzle.ScheduledEndAtUtc.HasValue
            && puzzle.ScheduledStartAtUtc.Value <= nowUtc
            && puzzle.ScheduledEndAtUtc.Value > nowUtc);

    public static TimeZoneInfo MalaysiaTimeZone => TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Singapore Standard Time" : "Asia/Kuala_Lumpur");

    public static DateTime? ToUtc(DateTime? malaysiaTime) => malaysiaTime.HasValue
        ? TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(malaysiaTime.Value, DateTimeKind.Unspecified),
            MalaysiaTimeZone)
        : null;

    public static DateTime? ToMalaysiaTime(DateTime? utcTime) => utcTime.HasValue
        ? TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcTime.Value, DateTimeKind.Utc),
            MalaysiaTimeZone)
        : null;
}