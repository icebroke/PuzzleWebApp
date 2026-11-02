using PuzzleWebApp.Models;

namespace PuzzleWebApp.Services;

public static class CrosswordScoring
{
    public static string GetClueAnswer(CrosswordClue clue, IReadOnlyDictionary<string, string> letters)
    {
        var answer = new char[clue.Answer.Length];
        for (var index = 0; index < clue.Answer.Length; index++)
        {
            var row = clue.StartRow + (clue.Direction == CrosswordDirection.Down ? index : 0);
            var column = clue.StartColumn + (clue.Direction == CrosswordDirection.Across ? index : 0);
            answer[index] = letters.TryGetValue(GetCellKey(row, column), out var letter)
                ? letter[0]
                : '·';
        }

        return new string(answer);
    }

    public static bool IsClueAnswered(CrosswordClue clue, IReadOnlyDictionary<string, string> letters) =>
        !GetClueAnswer(clue, letters).Contains('·');

    public static bool IsClueCorrect(CrosswordClue clue, IReadOnlyDictionary<string, string> letters) =>
        IsClueAnswered(clue, letters) &&
        GetClueAnswer(clue, letters).Equals(clue.Answer, StringComparison.OrdinalIgnoreCase);

    public static string GetCellKey(int row, int column) => $"{row},{column}";
}
