# PuzzleWebApp

Aplikasi web berasaskan ASP.NET Core MVC yang membolehkan pengguna menyertai permainan teka silang kata bertema integriti, akauntabiliti dan tadbir urus. Projek ini menggabungkan pengalaman permainan interaktif dengan papan pentadbiran untuk memantau kemajuan dan keputusan peserta.

## Gambaran keseluruhan

PuzzleWebApp dibangunkan untuk mengendalikan satu set permainan teka silang kata yang bertujuan menyampaikan mesej berkaitan:

- integriti
- akauntabiliti
- tadbir urus
- tadbir urus yang baik
- pencegahan rasuah dan salah guna kuasa

Pengguna boleh memasukkan nama dan ID pemain, menyimpan kemajuan jawapan, menghantar penyelesaian, serta melihat status permainan mereka. Pentadbir pula boleh log masuk untuk melihat ringkasan jawapan semua peserta dan analisis setiap cubaannya.

## Ciri-ciri utama

- Permainan teka silang kata berasaskan grid
- Log masuk pemain dengan nama dan ID unik
- Simpan kemajuan permainan secara automatik dalam sesi
- Penghantaran jawapan dengan pengesahan anti-forgery
- Papan pentadbiran untuk melihat statistik peserta
- Pemeriksaan jawapan dan analisis jumlah jawapan yang betul
- Penyederhanaan trafik dengan rate limiting
- Keselamatan tambahan melalui CSP (Content Security Policy)
- Pangkalan data SQLite yang dicipta dan disemai secara automatik

## Teknologi yang digunakan

- ASP.NET Core MVC
- .NET 10
- Entity Framework Core
- SQLite
- Session Management
- Rate Limiting
- Razor Views

## Struktur projek

```text
PuzzleWebApp/
├── Controllers/           # Pengawal MVC untuk halaman utama dan admin
├── Data/                  # Konteks DB dan data semai awal
├── Models/                # Entiti domain seperti Crossword, Clue dan Attempt
├── Middlewares/           # Middleware custom seperti CSP
├── Services/              # Logik pemarkahan dan sokongan permainan
├── ViewModels/            # Model paparan untuk UI
├── Views/                 # Paparan Razor
├── wwwroot/               # Aset statik seperti CSS, JS, imej
├── Program.cs             # Konfigurasi aplikasi
├── appsettings.json       # Tetapan konfigurasi aplikasi
├── PuzzleWebApp.csproj    # Fail projek .NET
├── puzzle.db              # Pangkalan data SQLite tempatan
└── README.md              # Dokumentasi projek
```

## Prasyarat

Sebelum menjalankan projek, pastikan anda telah memasang:

- .NET SDK 10.0 atau lebih baru
- Penyemak imbas moden seperti Chrome, Edge atau Firefox
- Git (pilihan, untuk menyalin projek)

## Persediaan dan pelaksanaan

1. Salin repositori ini ke mesin anda:

```bash
git clone https://github.com/icebroke/PuzzleWebApp.git
cd PuzzleWebApp
```

2. Pulihkan pakej projek:

```bash
dotnet restore
```

3. Bina projek:

```bash
dotnet build
```

4. Jalankan aplikasi:

```bash
dotnet run
```

5. Buka URL yang dipaparkan di terminal. Secara lalai, aplikasi ini menggunakan profil berikut:

- HTTP: http://localhost:5021
- HTTPS: https://localhost:7102

## Konfigurasi

Aplikasi ini menggunakan SQLite dengan rentetan sambungan berikut dalam `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=puzzle.db"
}
```

Untuk log masuk pentadbir, konfigurasi kod akses disimpan dalam `Admin:AccessCode`.

Anda boleh menukar nilai ini melalui pembolehubah persekitaran seperti berikut:

```bash
setx Admin__AccessCode "NILAI_KOD_AKSES"
```

Pada Linux/macOS:

```bash
export Admin__AccessCode="NILAI_KOD_AKSES"
```

Untuk persekitaran pembangunan, anda juga boleh menggunakan `dotnet user-secrets` jika perlu.

## Pentadbiran

Akses halaman admin biasanya melalui URL berikut:

```text
/Admin/Login
```

Selepas log masuk, pentadbir boleh:

- melihat senarai semua cubaan pemain
- menilai jumlah jawapan yang dijawab dan betul
- melihat butiran setiap cubaan pemain
- memantau prestasi keseluruhan teka silang kata

## Pangkalan data dan benih data

Apabila aplikasi dimulakan, `AppDbContext` akan memastikan pangkalan data dibuat dan migrasi digunakan. Jika data masih belum wujud, `CrosswordSeed` akan menyemai teka silang kata awal serta soalan berkaitan integriti dan tadbir urus.

## Peringatan

- Kod akses pentadbir harus ditetapkan dengan selamat untuk persekitaran pengeluaran.
- Jangan gunakan konfigurasi default yang terdedah dalam persekitaran pengeluaran.
- Sebarang data pemain dan jawapan yang disimpan dalam `puzzle.db` harus dilindungi mengikut keperluan keselamatan organisasi anda.

## Sumbangan

Bila-bila masa, anda boleh menambah baik fungsi, memperbaiki isu UI, atau memperluaskan kandungan teka silang kata. Pastikan perubahan diuji menggunakan perintah `dotnet build` sebelum menghantar kemas kini.

## Lesen

In progres.

## TODO

- [x]Fix the answer input to follow the direction.
- [x] Change the title to 'Kuiz Silang Kata Bulan Integriti MDHT 2026' in DB.
- [ ] Fix UI for mobile view.
- [ ] Fix UX behaviour and also interaction.
- [ ] 1st round 8 - 12 November 2026, 17:00
- [ ] 2nd round 15 - 19 November 2026, 17:00
- [ ] 2nd round 15 - 19 November 2026, 17:00
