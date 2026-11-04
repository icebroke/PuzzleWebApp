using System.Text.Json;
using PuzzleWebApp.Models;
using PuzzleWebApp.Services;

namespace PuzzleWebApp.Data;

public static class CrosswordSeed
{
    private const int Rows = 16;
    private const int Columns = 18;

    public static void EnsureSeeded(AppDbContext db)
    {
        var clues = CreateClues();
        ValidateLayout(clues);

        var existing = db.Crosswords.FirstOrDefault();
        if (existing is null)
        {
            db.Crosswords.Add(new Crossword
            {
                Id = 1,
                Title = "Kuiz Silang Kata Bulan Integriti MDHT 2026",
                Description = "Lengkapkan teka silang kata tentang integriti, akauntabiliti dan tadbir urus yang baik.",
                Rows = Rows,
                Columns = Columns,
                Clues = clues
            });
            db.SaveChanges();
            return;
        }

        const string description = "Lengkapkan teka silang kata tentang integriti, akauntabiliti dan tadbir urus yang baik.";
        if (existing.Description != description)
        {
            existing.Description = description;
        }

        var existingClues = db.CrosswordClues
            .Where(clue => clue.CrosswordId == existing.Id)
            .ToList();
        if (!MatchesTemplate(existingClues, clues))
        {
            throw new InvalidOperationException(
                "The saved crossword clues do not match the layout template, so the grid cannot be updated safely.");
        }

        var desiredByKey = clues.ToDictionary(clue => (clue.Number, clue.Direction));
        var layoutChanged = existing.Rows != Rows ||
                            existing.Columns != Columns ||
                            existingClues.Any(clue =>
                            {
                                var desired = desiredByKey[(clue.Number, clue.Direction)];
                                return clue.StartRow != desired.StartRow ||
                                       clue.StartColumn != desired.StartColumn;
                            });

        if (layoutChanged)
        {
            var attempts = db.CrosswordAttempts
                .Where(attempt => attempt.CrosswordId == existing.Id)
                .ToList();
            foreach (var attempt in attempts)
            {
                attempt.AnswersJson = RebaseAnswers(attempt.AnswersJson, existingClues, desiredByKey);
            }

            foreach (var clue in existingClues)
            {
                var desired = desiredByKey[(clue.Number, clue.Direction)];
                clue.StartRow = desired.StartRow;
                clue.StartColumn = desired.StartColumn;
            }

            existing.Rows = Rows;
            existing.Columns = Columns;
        }

        db.SaveChanges();
    }

    private static List<CrosswordClue> CreateClues() =>
    [
        CreateClue(1, "Amalan menerima atau memberi sesuatu yang boleh mempengaruhi keputusan rasmi.", "HADIAH", CrosswordDirection.Across, 9, 9),
        CreateClue(2, "Sikap jujur, berpegang teguh pada prinsip moral dan tidak menyalahgunakan kuasa", "INTEGRITI", CrosswordDirection.Down, 5, 5),
        CreateClue(3, "Perbuatan menggunakan kedudukan atau jawatan untuk memperoleh manfaat peribadi secara tidak wajar.", "SALAHGUNAKUASA", CrosswordDirection.Down, 0, 16),
        CreateClue(4, "Peraturan atau panduan yang menetapkan tingkah laku dan etika penjawat awam.", "KODETIKA", CrosswordDirection.Down, 7, 0),
        CreateClue(5, "Perbuatan memberi atau menerima suapan sebagai dorongan untuk melakukan sesuatu yang berkaitan dengan tugas.", "SUAPAN", CrosswordDirection.Across, 11, 12),
        CreateClue(6, "Sistem dan proses untuk memastikan organisasi diurus secara cekap, berkesan dan beretika.", "GOVERNANS", CrosswordDirection.Across, 0, 8),
        CreateClue(7, "Amalan keterbukaan dalam penyampaian maklumat dan proses membuat keputusan.", "KETELUSAN", CrosswordDirection.Down, 2, 10),
        CreateClue(8, "Agensi yang bertanggungjawab menjalankan fungsi pencegahan dan penyiasatan rasuah di Malaysia.", "SPRM", CrosswordDirection.Down, 12, 9),
        CreateClue(9, "Keadaan apabila kepentingan peribadi seseorang boleh mempengaruhi pelaksanaan tugas rasmi.", "KONFLIKKEPENTINGAN", CrosswordDirection.Across, 13, 0),
        CreateClue(10, "Individu yang melaporkan salah laku atau pelanggaran integriti melalui saluran yang ditetapkan.", "PEMBERIMAKLUMAT", CrosswordDirection.Across, 15, 2),
        CreateClue(11, "Apakah tindakan yang perlu dilakukan oleh penjawat awam apabila mengetahui berlaku salah laku atau pelanggaran prosedur dalam organisasi?", "LAPORAN", CrosswordDirection.Down, 1, 7),
        CreateClue(12, "Prinsip yang mewajibkan penjawat awam bertanggungjawab terhadap tindakan dan keputusan mereka.", "AKAUNTABILITI", CrosswordDirection.Across, 6, 1),
        CreateClue(13, "Nilai yang bermaksud melaksanakan tanggungjawab dengan penuh kepercayaan dan kejujuran.", "AMANAH", CrosswordDirection.Down, 0, 14),
        CreateClue(14, "Tindakan memastikan sesuatu keputusan dibuat berdasarkan peraturan, bukti dan kepentingan awam.", "KEWAJARAN", CrosswordDirection.Across, 2, 0),
        CreateClue(15, "Proses pemeriksaan dan penilaian rekod kewangan serta pematuhan organisasi.", "AUDIT", CrosswordDirection.Across, 11, 2)
    ];

    private static void ValidateLayout(IReadOnlyCollection<CrosswordClue> clues)
    {
        if (clues.Select(clue => (clue.StartRow, clue.StartColumn)).Distinct().Count() != clues.Count)
        {
            throw new InvalidOperationException("Each numbered crossword answer must have its own start cell.");
        }

        var lettersByCell = new Dictionary<(int Row, int Column), List<(CrosswordClue Clue, char Letter)>>();
        foreach (var clue in clues)
        {
            if (string.IsNullOrEmpty(clue.Answer) || clue.Answer.Any(letter => letter is < 'A' or > 'Z'))
            {
                throw new InvalidOperationException($"Clue {clue.Number} must have an answer containing letters only.");
            }

            for (var index = 0; index < clue.Answer.Length; index++)
            {
                var row = clue.StartRow + (clue.Direction == CrosswordDirection.Down ? index : 0);
                var column = clue.StartColumn + (clue.Direction == CrosswordDirection.Across ? index : 0);
                if (row < 0 || row >= Rows || column < 0 || column >= Columns)
                {
                    throw new InvalidOperationException($"Clue {clue.Number} extends outside the crossword grid.");
                }

                var cell = (row, column);
                if (!lettersByCell.TryGetValue(cell, out var occupants))
                {
                    occupants = [];
                    lettersByCell.Add(cell, occupants);
                }

                occupants.Add((clue, clue.Answer[index]));
            }
        }

        var intersections = new Dictionary<CrosswordClue, HashSet<CrosswordClue>>();
        foreach (var clue in clues)
        {
            intersections.Add(clue, []);
        }

        foreach (var occupants in lettersByCell.Values)
        {
            var across = occupants.Where(item => item.Clue.Direction == CrosswordDirection.Across).ToList();
            var down = occupants.Where(item => item.Clue.Direction == CrosswordDirection.Down).ToList();
            if (across.Count > 1 || down.Count > 1)
            {
                throw new InvalidOperationException("Two answers in the same direction occupy the same crossword cell.");
            }

            if (across.Count == 1 && down.Count == 1)
            {
                if (across[0].Letter != down[0].Letter)
                {
                    throw new InvalidOperationException(
                        $"Clues {across[0].Clue.Number} and {down[0].Clue.Number} cross on different letters.");
                }

                intersections[across[0].Clue].Add(down[0].Clue);
                intersections[down[0].Clue].Add(across[0].Clue);
            }
        }

        if (intersections.Any(pair => pair.Value.Count == 0))
        {
            throw new InvalidOperationException("Every crossword answer must intersect at least one other answer.");
        }

        var connected = new HashSet<CrosswordClue>();
        var pending = new Stack<CrosswordClue>();
        pending.Push(clues.First());
        while (pending.TryPop(out var clue))
        {
            if (connected.Add(clue))
            {
                foreach (var crossing in intersections[clue])
                {
                    pending.Push(crossing);
                }
            }
        }

        if (connected.Count != clues.Count)
        {
            throw new InvalidOperationException("All crossword answers must be connected through matching intersections.");
        }

        var occupiedCells = lettersByCell.Keys.ToHashSet();
        foreach (var clue in clues)
        {
            var before = clue.Direction == CrosswordDirection.Across
                ? (clue.StartRow, clue.StartColumn - 1)
                : (clue.StartRow - 1, clue.StartColumn);
            var after = clue.Direction == CrosswordDirection.Across
                ? (clue.StartRow, clue.StartColumn + clue.Answer.Length)
                : (clue.StartRow + clue.Answer.Length, clue.StartColumn);
            if (occupiedCells.Contains(before) || occupiedCells.Contains(after))
            {
                throw new InvalidOperationException($"Clue {clue.Number} would create an unintended extra letter run.");
            }
        }
    }

    private static bool MatchesTemplate(
        IReadOnlyCollection<CrosswordClue> existing,
        IReadOnlyCollection<CrosswordClue> template) =>
        existing.Count == template.Count &&
        template.All(expected => existing.Any(clue =>
            clue.Number == expected.Number &&
            clue.Direction == expected.Direction &&
            clue.Answer.Equals(expected.Answer, StringComparison.OrdinalIgnoreCase)));

    private static string RebaseAnswers(
        string answersJson,
        IEnumerable<CrosswordClue> existingClues,
        IReadOnlyDictionary<(int Number, CrosswordDirection Direction), CrosswordClue> desiredByKey)
    {
        var savedAnswers = JsonSerializer.Deserialize<Dictionary<string, string>>(answersJson)
            ?? throw new InvalidOperationException("The saved crossword progress is not valid JSON.");
        var rebasedAnswers = new Dictionary<string, string>(StringComparer.Ordinal);
        var conflictingCells = new HashSet<string>(StringComparer.Ordinal);
        var existingCellKeys = existingClues
            .SelectMany(clue => Enumerable.Range(0, clue.Answer.Length).Select(index =>
                CrosswordScoring.GetCellKey(
                    clue.StartRow + (clue.Direction == CrosswordDirection.Down ? index : 0),
                    clue.StartColumn + (clue.Direction == CrosswordDirection.Across ? index : 0))))
            .ToHashSet(StringComparer.Ordinal);
        if (savedAnswers.Keys.Any(key => !existingCellKeys.Contains(key)))
        {
            throw new InvalidOperationException("Saved crossword progress contains cells outside the existing grid.");
        }

        foreach (var clue in existingClues)
        {
            var desired = desiredByKey[(clue.Number, clue.Direction)];
            for (var index = 0; index < clue.Answer.Length; index++)
            {
                var oldRow = clue.StartRow + (clue.Direction == CrosswordDirection.Down ? index : 0);
                var oldColumn = clue.StartColumn + (clue.Direction == CrosswordDirection.Across ? index : 0);
                if (!savedAnswers.TryGetValue(CrosswordScoring.GetCellKey(oldRow, oldColumn), out var letter))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(letter) || letter.Length != 1 || !char.IsLetter(letter[0]))
                {
                    throw new InvalidOperationException("Saved crossword cells must contain exactly one letter.");
                }

                var newRow = desired.StartRow + (desired.Direction == CrosswordDirection.Down ? index : 0);
                var newColumn = desired.StartColumn + (desired.Direction == CrosswordDirection.Across ? index : 0);
                var newCellKey = CrosswordScoring.GetCellKey(newRow, newColumn);
                if (conflictingCells.Contains(newCellKey))
                {
                    continue;
                }

                if (rebasedAnswers.TryGetValue(newCellKey, out var existingLetter) &&
                    !existingLetter.Equals(letter, StringComparison.OrdinalIgnoreCase))
                {
                    // A previously shared cell cannot retain conflicting letters at a new crossing.
                    rebasedAnswers.Remove(newCellKey);
                    conflictingCells.Add(newCellKey);
                    continue;
                }

                rebasedAnswers[newCellKey] = letter.ToUpperInvariant();
            }
        }

        return JsonSerializer.Serialize(rebasedAnswers);
    }

    private static CrosswordClue CreateClue(
        int number,
        string text,
        string answer,
        CrosswordDirection direction,
        int row,
        int column)
    {
        return new CrosswordClue
        {
            Number = number,
            Text = text,
            Answer = answer,
            Direction = direction,
            StartRow = row,
            StartColumn = column
        };
    }
}
