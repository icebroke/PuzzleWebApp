using PuzzleWebApp.Models;
using PuzzleWebApp.Services;

namespace PuzzleWebApp.Data;

public static class CrosswordSeed
{
    private sealed record RoundSeed(string Title, string Description, List<(string Text, string Answer)> Entries);

    public static void EnsureSeeded(AppDbContext db)
    {
        foreach (var round in CreateRounds())
        {
            if (db.Crosswords.Any(crossword => crossword.Title == round.Title))
            {
                continue;
            }

            var entries = round.Entries
                .Select(entry => (entry.Text, Answer: NormalizeAnswer(entry.Answer)))
                .ToList();
            if (!CrosswordLayoutBuilder.TryBuild(entries, out var layout) || layout is null)
            {
                throw new InvalidOperationException($"The crossword answers for {round.Title} could not be arranged.");
            }

            db.Crosswords.Add(new Crossword
            {
                Title = round.Title,
                Description = round.Description,
                Rows = layout.Rows,
                Columns = layout.Columns,
                Clues = layout.Clues
            });
        }

        db.SaveChanges();
    }

    private static List<RoundSeed> CreateRounds() =>
    [
        new RoundSeed("Kuiz Silang Kata Bulan Integriti MDHT 2026", "Lengkapkan teka silang kata tentang integriti, akauntabiliti dan tadbir urus yang baik.",
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
            ("Ketua jabatan menyemak kemajuan pelaksanaan sesuatu projek dari semasa ke semasa bagi memastikan kerja berjalan mengikut perancangan. Aktiviti ini dikenali sebagai __________.", "Pemantauan"),
            ("Apabila menerima aduan salah laku, pihak yang diberi kuasa perlu mendapatkan keterangan, mengumpulkan bukti dan meneliti fakta sebelum membuat keputusan. Proses ini dikenali sebagai __________.", "Siasatan"),
            ("Kakitangan yang didapati melanggar peraturan perkhidmatan boleh dikenakan __________ mengikut peraturan yang berkuat kuasa.", "Tindakan Tatatertib"),
            ("Pembelian peralatan yang tidak diperlukan sehingga menyebabkan penggunaan wang awam secara tidak berfaedah merupakan satu bentuk __________.", "Pembaziran"),
            ("Organisasi menetapkan sasaran kerja tahunan dan menilai pencapaian kakitangan berdasarkan hasil kerja serta tanggungjawab yang diberikan. Proses ini dikenali sebagai __________.", "Penilaian Prestasi")
        ])
    ];

    private static string NormalizeAnswer(string answer) =>
        new(answer.Where(char.IsLetter).Select(char.ToUpperInvariant).ToArray());
}
