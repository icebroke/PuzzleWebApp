using PuzzleWebApp.Models;

namespace PuzzleWebApp.Data;

public static class CrosswordSeed
{
    public static void EnsureSeeded(AppDbContext db)
    {
        var existing = db.Crosswords.FirstOrDefault();
        if (existing is not null)
        {
            const string description = "Lengkapkan teka silang kata tentang integriti, akauntabiliti dan tadbir urus yang baik.";
            if (existing.Description != description)
            {
                existing.Description = description;
                db.SaveChanges();
            }

            return;
        }

        var crossword = new Crossword
        {
            Id = 1,
            Title = "Integriti dan Tadbir Urus",
            Description = "Lengkapkan teka silang kata tentang integriti, akauntabiliti dan tadbir urus yang baik.",
            Rows = 18,
            Columns = 18,
            Clues =
            [
                CreateClue(1, "Amalan menerima atau memberi sesuatu yang boleh mempengaruhi keputusan rasmi.", "HADIAH", CrosswordDirection.Across, 5, 12),
                CreateClue(2, "Sikap jujur, berpegang teguh pada prinsip moral dan tidak menyalahgunakan kuasa", "INTEGRITI", CrosswordDirection.Down, 0, 5),
                CreateClue(3, "Perbuatan menggunakan kedudukan atau jawatan untuk memperoleh manfaat peribadi secara tidak wajar.", "SALAHGUNAKUASA", CrosswordDirection.Down, 1, 17),
                CreateClue(4, "Peraturan atau panduan yang menetapkan tingkah laku dan etika penjawat awam.", "KODETIKA", CrosswordDirection.Down, 10, 11),
                CreateClue(5, "Perbuatan memberi atau menerima suapan sebagai dorongan untuk melakukan sesuatu yang berkaitan dengan tugas.", "SUAPAN", CrosswordDirection.Across, 1, 0),
                CreateClue(6, "Sistem dan proses untuk memastikan organisasi diurus secara cekap, berkesan dan beretika.", "GOVERNANS", CrosswordDirection.Across, 1, 8),
                CreateClue(7, "Amalan keterbukaan dalam penyampaian maklumat dan proses membuat keputusan.", "KETELUSAN", CrosswordDirection.Down, 0, 11),
                CreateClue(8, "Agensi yang bertanggungjawab menjalankan fungsi pencegahan dan penyiasatan rasuah di Malaysia.", "SPRM", CrosswordDirection.Down, 7, 9),
                CreateClue(9, "Keadaan apabila kepentingan peribadi seseorang boleh mempengaruhi pelaksanaan tugas rasmi.", "KONFLIKKEPENTINGAN", CrosswordDirection.Across, 8, 0),
                CreateClue(10, "Individu yang melaporkan salah laku atau pelanggaran integriti melalui saluran yang ditetapkan.", "PEMBERIMAKLUMAT", CrosswordDirection.Across, 10, 2),
                CreateClue(11, "Apakah tindakan yang perlu dilakukan oleh penjawat awam apabila mengetahui berlaku salah laku atau pelanggaran prosedur dalam organisasi?", "LAPORAN", CrosswordDirection.Down, 10, 12),
                CreateClue(12, "Prinsip yang mewajibkan penjawat awam bertanggungjawab terhadap tindakan dan keputusan mereka.", "AKAUNTABILITI", CrosswordDirection.Across, 2, 0),
                CreateClue(13, "Nilai yang bermaksud melaksanakan tanggungjawab dengan penuh kepercayaan dan kejujuran.", "AMANAH", CrosswordDirection.Down, 2, 6),
                CreateClue(14, "Tindakan memastikan sesuatu keputusan dibuat berdasarkan peraturan, bukti dan kepentingan awam.", "KEWAJARAN", CrosswordDirection.Across, 9, 3),
                CreateClue(15, "Proses pemeriksaan dan penilaian rekod kewangan serta pematuhan organisasi.", "AUDIT", CrosswordDirection.Across, 4, 6)
            ]
        };

        db.Crosswords.Add(crossword);
        db.SaveChanges();
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
