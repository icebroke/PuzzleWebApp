using PuzzleWebApp.Models;

namespace PuzzleWebApp.Services;

public sealed record CrosswordLayout(int Rows, int Columns, List<CrosswordClue> Clues);

public static class CrosswordLayoutBuilder
{
    private const int SearchLimit = 20000;

    private sealed record Placement(int EntryIndex, int Row, int Column, CrosswordDirection Direction);
    private sealed record CellLetter(int EntryIndex, CrosswordDirection Direction, char Letter);
    private sealed record Candidate(Placement Placement, int Intersections, long Area);

    public static bool TryBuild(
        IReadOnlyList<(string Text, string Answer)> entries,
        out CrosswordLayout? layout)
    {
        layout = null;
        if (entries.Count < 2 || entries.Any(entry => entry.Answer.Length < 2))
        {
            return false;
        }

        var occupied = new Dictionary<(int Row, int Column), List<CellLetter>>();
        var placements = new Placement?[entries.Count];
        var anchorIndex = Enumerable.Range(0, entries.Count)
            .OrderByDescending(index => entries[index].Answer.Length)
            .ThenBy(index => index)
            .First();
        var anchor = new Placement(anchorIndex, 0, 0, CrosswordDirection.Across);
        placements[anchorIndex] = anchor;
        AddPlacement(anchor, entries, occupied);

        var searchedNodes = 0;
        if (!PlaceRemaining(entries, placements, occupied, ref searchedNodes))
        {
            return false;
        }

        var minRow = occupied.Keys.Min(cell => cell.Row);
        var maxRow = occupied.Keys.Max(cell => cell.Row);
        var minColumn = occupied.Keys.Min(cell => cell.Column);
        var maxColumn = occupied.Keys.Max(cell => cell.Column);
        var numberedStarts = placements
            .Select(placement => (placement!.Row, placement.Column))
            .Distinct()
            .OrderBy(cell => cell.Row)
            .ThenBy(cell => cell.Column)
            .Select((cell, index) => (cell, number: index + 1))
            .ToDictionary(item => item.cell, item => item.number);

        var clues = placements
            .Select(placement =>
            {
                var entry = entries[placement!.EntryIndex];
                return new CrosswordClue
                {
                    Number = numberedStarts[(placement.Row, placement.Column)],
                    Text = entry.Text,
                    Answer = entry.Answer,
                    StartRow = placement.Row - minRow,
                    StartColumn = placement.Column - minColumn,
                    Direction = placement.Direction
                };
            })
            .OrderBy(clue => clue.Number)
            .ThenBy(clue => clue.Direction)
            .ToList();

        layout = new CrosswordLayout(maxRow - minRow + 1, maxColumn - minColumn + 1, clues);
        return true;
    }

    private static bool PlaceRemaining(
        IReadOnlyList<(string Text, string Answer)> entries,
        Placement?[] placements,
        Dictionary<(int Row, int Column), List<CellLetter>> occupied,
        ref int searchedNodes)
    {
        if (placements.All(placement => placement is not null))
        {
            return true;
        }

        if (++searchedNodes > SearchLimit)
        {
            return false;
        }

        var selectedEntry = -1;
        List<Candidate>? selectedCandidates = null;
        for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            if (placements[entryIndex] is not null)
            {
                continue;
            }

            var candidates = GetCandidates(entryIndex, entries, placements, occupied);
            if (candidates.Count == 0)
            {
                continue;
            }

            if (selectedCandidates is null || candidates.Count < selectedCandidates.Count)
            {
                selectedEntry = entryIndex;
                selectedCandidates = candidates;
            }
        }

        if (selectedCandidates is null)
        {
            return false;
        }

        foreach (var candidate in selectedCandidates)
        {
            placements[selectedEntry] = candidate.Placement;
            AddPlacement(candidate.Placement, entries, occupied);
            if (PlaceRemaining(entries, placements, occupied, ref searchedNodes))
            {
                return true;
            }

            RemovePlacement(candidate.Placement, entries, occupied);
            placements[selectedEntry] = null;
            if (searchedNodes > SearchLimit)
            {
                break;
            }
        }

        return false;
    }

    private static List<Candidate> GetCandidates(
        int entryIndex,
        IReadOnlyList<(string Text, string Answer)> entries,
        Placement?[] placements,
        Dictionary<(int Row, int Column), List<CellLetter>> occupied)
    {
        var candidates = new Dictionary<(int Row, int Column, CrosswordDirection Direction), Candidate>();
        var answer = entries[entryIndex].Answer;
        foreach (var existing in placements.Where(placement => placement is not null).Select(placement => placement!))
        {
            var existingAnswer = entries[existing.EntryIndex].Answer;
            for (var existingIndex = 0; existingIndex < existingAnswer.Length; existingIndex++)
            {
                var crossingRow = existing.Row + (existing.Direction == CrosswordDirection.Down ? existingIndex : 0);
                var crossingColumn = existing.Column + (existing.Direction == CrosswordDirection.Across ? existingIndex : 0);
                for (var answerIndex = 0; answerIndex < answer.Length; answerIndex++)
                {
                    if (answer[answerIndex] != existingAnswer[existingIndex])
                    {
                        continue;
                    }

                    var direction = existing.Direction == CrosswordDirection.Across
                        ? CrosswordDirection.Down
                        : CrosswordDirection.Across;
                    var row = crossingRow - (direction == CrosswordDirection.Down ? answerIndex : 0);
                    var column = crossingColumn - (direction == CrosswordDirection.Across ? answerIndex : 0);
                    var placement = new Placement(entryIndex, row, column, direction);
                    if (!CanPlace(placement, entries, occupied, out var intersections))
                    {
                        continue;
                    }

                    var area = GetArea(placement, entries, occupied);
                    var key = (row, column, direction);
                    if (!candidates.TryGetValue(key, out var previous) || intersections > previous.Intersections)
                    {
                        candidates[key] = new Candidate(placement, intersections, area);
                    }
                }
            }
        }

        return candidates.Values
            .OrderBy(candidate => candidate.Area)
            .ThenByDescending(candidate => candidate.Intersections)
            .ThenBy(candidate => candidate.Placement.Row)
            .ThenBy(candidate => candidate.Placement.Column)
            .ToList();
    }

    private static bool CanPlace(
        Placement placement,
        IReadOnlyList<(string Text, string Answer)> entries,
        Dictionary<(int Row, int Column), List<CellLetter>> occupied,
        out int intersections)
    {
        intersections = 0;
        var answer = entries[placement.EntryIndex].Answer;
        for (var index = 0; index < answer.Length; index++)
        {
            var row = placement.Row + (placement.Direction == CrosswordDirection.Down ? index : 0);
            var column = placement.Column + (placement.Direction == CrosswordDirection.Across ? index : 0);
            if (occupied.TryGetValue((row, column), out var occupants))
            {
                if (occupants.Count != 1 || occupants[0].Direction == placement.Direction || occupants[0].Letter != answer[index])
                {
                    return false;
                }

                intersections++;
                continue;
            }

            var sideCells = placement.Direction == CrosswordDirection.Across
                ? new[] { (row - 1, column), (row + 1, column) }
                : new[] { (row, column - 1), (row, column + 1) };
            if (sideCells.Any(occupied.ContainsKey))
            {
                return false;
            }
        }

        var before = placement.Direction == CrosswordDirection.Across
            ? (placement.Row, placement.Column - 1)
            : (placement.Row - 1, placement.Column);
        var after = placement.Direction == CrosswordDirection.Across
            ? (placement.Row, placement.Column + answer.Length)
            : (placement.Row + answer.Length, placement.Column);
        return intersections > 0 && !occupied.ContainsKey(before) && !occupied.ContainsKey(after);
    }

    private static long GetArea(
        Placement placement,
        IReadOnlyList<(string Text, string Answer)> entries,
        Dictionary<(int Row, int Column), List<CellLetter>> occupied)
    {
        var lastRow = placement.Row + (placement.Direction == CrosswordDirection.Down ? entries[placement.EntryIndex].Answer.Length - 1 : 0);
        var lastColumn = placement.Column + (placement.Direction == CrosswordDirection.Across ? entries[placement.EntryIndex].Answer.Length - 1 : 0);
        var minRow = Math.Min(placement.Row, occupied.Keys.Min(cell => cell.Row));
        var maxRow = Math.Max(lastRow, occupied.Keys.Max(cell => cell.Row));
        var minColumn = Math.Min(placement.Column, occupied.Keys.Min(cell => cell.Column));
        var maxColumn = Math.Max(lastColumn, occupied.Keys.Max(cell => cell.Column));
        return (long)(maxRow - minRow + 1) * (maxColumn - minColumn + 1);
    }

    private static void AddPlacement(
        Placement placement,
        IReadOnlyList<(string Text, string Answer)> entries,
        Dictionary<(int Row, int Column), List<CellLetter>> occupied)
    {
        var answer = entries[placement.EntryIndex].Answer;
        for (var index = 0; index < answer.Length; index++)
        {
            var cell = (
                placement.Row + (placement.Direction == CrosswordDirection.Down ? index : 0),
                placement.Column + (placement.Direction == CrosswordDirection.Across ? index : 0));
            if (!occupied.TryGetValue(cell, out var letters))
            {
                letters = [];
                occupied.Add(cell, letters);
            }

            letters.Add(new CellLetter(placement.EntryIndex, placement.Direction, answer[index]));
        }
    }

    private static void RemovePlacement(
        Placement placement,
        IReadOnlyList<(string Text, string Answer)> entries,
        Dictionary<(int Row, int Column), List<CellLetter>> occupied)
    {
        var answer = entries[placement.EntryIndex].Answer;
        for (var index = 0; index < answer.Length; index++)
        {
            var cell = (
                placement.Row + (placement.Direction == CrosswordDirection.Down ? index : 0),
                placement.Column + (placement.Direction == CrosswordDirection.Across ? index : 0));
            var letters = occupied[cell];
            letters.RemoveAll(letter => letter.EntryIndex == placement.EntryIndex);
            if (letters.Count == 0)
            {
                occupied.Remove(cell);
            }
        }
    }
}
