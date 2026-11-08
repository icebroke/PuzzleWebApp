using Microsoft.EntityFrameworkCore;
using PuzzleWebApp.Models;
using PuzzleWebApp.Services;

namespace PuzzleWebApp.Data;

public static class CrosswordSeed
{
    private sealed record RoundSeed(
        int RoundNumber,
        string LegacyTitle,
        string Title,
        string Description,
        DateTime ScheduledStartAtUtc,
        DateTime ScheduledEndAtUtc,
        List<(string Text, string Answer)> Entries);

    public static void EnsureSeeded(AppDbContext db)
    {
        var rounds = CreateRounds();
        var existingPuzzles = db.Crosswords.Include(crossword => crossword.Clues).ToList();
        var assignedPuzzles = new HashSet<Crossword>();

        foreach (var round in rounds)
        {
            var crossword = existingPuzzles.FirstOrDefault(existing =>
                    existing.Title == round.Title
                    && existing.ScheduledStartAtUtc == round.ScheduledStartAtUtc
                    && !assignedPuzzles.Contains(existing))
                ?? existingPuzzles.FirstOrDefault(existing =>
                    existing.Title == round.LegacyTitle
                    && !assignedPuzzles.Contains(existing));
            if (crossword is null && round.LegacyTitle.EndsWith("Pusingan 2", StringComparison.Ordinal))
            {
                crossword = existingPuzzles.FirstOrDefault(existing =>
                    existing.Title == round.Title
                    && !existing.ScheduledStartAtUtc.HasValue
                    && !existing.ScheduledEndAtUtc.HasValue
                    && !assignedPuzzles.Contains(existing));
            }

            var entries = round.Entries
                .Select(entry => (entry.Text, Answer: NormalizeAnswer(entry.Answer)))
                .ToList();
            if (!CrosswordLayoutBuilder.TryBuild(entries, out var layout) || layout is null)
            {
                throw new InvalidOperationException($"The crossword answers for {round.Title} could not be arranged.");
            }

            if (crossword is null)
            {
                crossword = new Crossword();
                db.Crosswords.Add(crossword);
                existingPuzzles.Add(crossword);
            }
            assignedPuzzles.Add(crossword);

            crossword.RoundNumber = round.RoundNumber;
            crossword.Title = round.Title;
            crossword.Description = round.Description;
            crossword.Rows = layout.Rows;
            crossword.Columns = layout.Columns;
            crossword.ScheduledStartAtUtc = round.ScheduledStartAtUtc;
            crossword.ScheduledEndAtUtc = round.ScheduledEndAtUtc;
            var hasAttempts = crossword.Id != 0
                && db.CrosswordAttempts.Any(attempt => attempt.CrosswordId == crossword.Id);
            if (!hasAttempts)
            {
                db.CrosswordClues.RemoveRange(crossword.Clues);
                crossword.Clues = layout.Clues;
            }

            foreach (var duplicate in existingPuzzles.Where(existing =>
                existing.Id != crossword.Id
                && existing.Title == round.Title
                && existing.ScheduledStartAtUtc == round.ScheduledStartAtUtc))
            {
                duplicate.Title = $"{round.Title} (Arkib)";
                duplicate.ScheduledStartAtUtc = null;
                duplicate.ScheduledEndAtUtc = null;
                duplicate.ManualActivation = false;
            }
        }

        db.SaveChanges();
    }

    private static List<RoundSeed> CreateRounds() =>
    [
        new RoundSeed(1, "Kuiz Silang Kata Bulan Integriti MDHT 2026", "Kuiz Silang Kata Bulan Integriti MDHT 2026", "Lengkapkan teka silang kata tentang integriti, akauntabiliti dan tadbir urus yang baik.",
            MalaysiaTimeToUtc(2026, 11, 8, 9), MalaysiaTimeToUtc(2026, 11, 12, 17),
        [
            ("Seorang pegawai terlibat dalam penilaian sebutharga yang disertai oleh syarikat milik ahli keluarganya. Apakah keadaan yang perlu dikenal pasti dan diuruskan oleh pegawai tersebut?", "Konflik Kepentingan"),
            ("Seorang ketua jabatan meluluskan sesuatu pembayaran tanpa menyemak dokumen sokongan, kemudian berlaku kerugian kepada organisasi. Apakah prinsip yang perlu dipertanggungjawabkan?", "Akauntabiliti"),
            ("Seorang pegawai menyediakan laporan prestasi jabatan dengan memasukkan angka yang telah diubah supaya pencapaian kelihatan lebih baik daripada keadaan sebenar. Apakah nilai yang telah dicabuli?", "Integriti"),
            ("Seorang pegawai mencadangkan anak saudaranya dilantik ke jawatan kosong tanpa melalui proses pemilihan yang adil. Apakah amalan ini dikenali sebagai?", "Nepotisme"),
            ("Seorang kakitangan mengetahui terdapat tuntutan perjalanan palsu yang dibuat oleh rakan sekerjanya. Dia membuat laporan melalui saluran rasmi organisasi. Apakah tindakan yang telah dilakukan?", "Pelaporan"),
            ("Seorang pegawai berkongsi dokumen dalaman yang belum diluluskan untuk edaran kepada pihak luar tanpa kebenaran. Apakah prinsip yang telah dilanggar?", "Kerahsiaan"),
            ("Seorang pegawai menerima arahan untuk meluluskan pembayaran walaupun kerja belum disahkan siap. Apakah nilai yang perlu menjadi pegangan pegawai tersebut ketika menghadapi tekanan ini?", "Ketegasan"),
            ("Seorang pegawai memberikan layanan istimewa kepada pemohon lesen kerana pemohon tersebut merupakan kenalan rapatnya. Apakah prinsip yang telah diketepikan?", "Keadilan"),
            ("Seorang pegawai menggunakan kenderaan rasmi jabatan untuk urusan peribadi pada hujung minggu tanpa kebenaran. Apakah perbuatan ini dikategorikan sebagai?", "Salahguna Aset"),
            ("Seorang pegawai memastikan hanya syarikat milik kenalan rapatnya dimaklumkan tentang peluang perolehan supaya syarikat tersebut mendapat kelebihan. Apakah amalan ini dikenali sebagai?", "Kronisme"),
            ("Seorang pegawai dilantik sebagai ahli jawatankuasa penilaian, tetapi mempunyai kepentingan kewangan dalam salah satu syarikat yang dinilai. Apakah tindakan awal yang perlu dibuat oleh pegawai tersebut?", "Pengisytiharan"),
            ("Sebelum melaksanakan sesuatu urusan kewangan, pegawai memastikan semua tindakan mengikut pekeliling dan prosedur yang sedang berkuat kuasa. Apakah amalan ini dikenali sebagai?", "Pematuhan"),
            ("Seorang penjawat awam memulangkan wang lebihan bayaran yang diterimanya walaupun tiada pihak menyedari kesilapan tersebut. Apakah nilai yang ditunjukkan?", "Amanah"),
            ("Pihak pengurusan memastikan setiap keputusan perolehan mempunyai justifikasi, rekod pertimbangan dan proses kelulusan yang jelas. Apakah konsep pengurusan yang diamalkan?", "Tadbir Urus"),
            ("Sebuah jabatan menyediakan mekanisme rasmi untuk menerima aduan berkaitan salah laku kakitangan, merekodkan aduan dan menjalankan tindakan susulan. Apakah mekanisme ini?", "Saluran Aduan")
        ]),
        new RoundSeed(2, "Kuiz Silang Kata Bulan Integriti MDHT 2026", "Kuiz Silang Kata Bulan Integriti MDHT 2026", "Lengkapkan teka silang kata tentang integriti, akauntabiliti dan tadbir urus yang baik.",
            MalaysiaTimeToUtc(2026, 11, 15, 9), MalaysiaTimeToUtc(2026, 11, 19, 17),
        [
            ("Bagi mengelakkan seorang pegawai mengawal keseluruhan proses pembayaran, organisasi perlu mengamalkan __________ supaya tugas penyediaan, semakan dan kelulusan dilaksanakan oleh individu yang berbeza.", "Pengasingan Tugas"),
            ("Kegagalan merekod kutipan hasil dengan betul sehingga menyebabkan wang yang sepatutnya diterima oleh jabatan tidak dapat dikesan dikenali sebagai __________ hasil.", "Ketirisan"),
            ("Seorang pegawai mengubah maklumat pada resit rasmi untuk menunjukkan jumlah bayaran yang berbeza daripada jumlah sebenar. Tindakan ini merupakan __________ dokumen.", "Pemalsuan"),
            ("Dua syarikat berpakat untuk menetapkan harga tawaran masing-masing bagi memastikan salah satu daripada mereka berjaya memperoleh kontrak. Perbuatan ini dikenali sebagai __________.", "Pakatan Sulit"),
            ("Seorang pegawai menolak permohonan pelanggan yang layak hanya kerana latar belakang peribadinya. Tindakan tersebut merupakan satu bentuk __________.", "Diskriminasi"),
            ("Dalam proses pengambilan pekerja, calon dipilih berdasarkan kelayakan, kebolehan dan pencapaian tanpa dipengaruhi hubungan peribadi. Prinsip ini dikenali sebagai __________.", "Meritokrasi"),
            ("Seorang kakitangan sentiasa menjaga tingkah laku, memberikan perkhidmatan berkualiti dan melaksanakan tugas mengikut standard jawatan yang disandang. Sikap ini mencerminkan __________.", "Profesionalisme"),
            ("Sebelum melantik kontraktor, jabatan menyemak latar belakang, kemampuan kewangan dan rekod pengalaman kontraktor bagi memastikan kesesuaiannya. Proses ini dikenali sebagai __________.", "Usaha Wajar"),
            ("Organisasi menetapkan had kuasa melulus, pemeriksaan dokumen dan pengesahan transaksi bagi mengurangkan risiko kesilapan serta penyelewengan. Kesemua langkah ini merupakan __________ organisasi.", "Kawalan Dalaman"),
            ("Sebelum melaksanakan sesuatu program, jabatan mengenal pasti kemungkinan masalah, menilai kesannya dan menyediakan langkah pencegahan. Proses ini dikenali sebagai __________.", "Pengurusan risiko"),
            ("Ketua jabatan menyemak data pelaksanaan sesuatu projek dari semasa ke semasa bagi memastikan kerja berjalan mengikut perancangan. Aktiviti ini dikenali sebagai __________.", "Pemantauan"),
            ("Apabila menerima aduan salah laku, pihak yang diberi kuasa perlu mendapatkan keterangan, mengumpulkan bukti dan meneliti fakta sebelum membuat keputusan. Proses ini dikenali sebagai __________.", "Siasatan"),
            ("Kakitangan yang didapati melanggar peraturan perkhidmatan boleh dikenakan __________ mengikut peraturan yang berkuat kuasa.", "Tindakan Tatatertib"),
            ("Pembelian peralatan yang tidak diperlukan sehingga menyebabkan penggunaan wang awam secara tidak berfaedah merupakan satu bentuk __________.", "Pembaziran"),
            ("Organisasi menetapkan sasaran kerja tahunan dan menilai pencapaian kakitangan berdasarkan hasil kerja serta tanggungjawab yang diberikan. Proses ini dikenali sebagai __________.", "Penilaian Prestasi")
        ])
    ];

    private static DateTime MalaysiaTimeToUtc(int year, int month, int day, int hour) =>
        TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified),
            TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Singapore Standard Time" : "Asia/Kuala_Lumpur"));

    private static string NormalizeAnswer(string answer) =>
        new(answer.Where(char.IsLetter).Select(char.ToUpperInvariant).ToArray());
}
