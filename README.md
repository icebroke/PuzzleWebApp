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
- Susun atur teka silang kata 16 × 18 dengan persilangan jawapan yang disahkan
- Kemajuan pemain sedia ada dikekalkan apabila kedudukan jawapan dikemas kini

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

Pentadbir lalai disemai ketika aplikasi bermula jika belum ada akaun pentadbir:

- Nama pengguna: `admin`
- Kata laluan sementara: `ChangeMe123!`

Akaun ini diwajibkan menukar kata laluan pada log masuk pertama sebelum halaman pentadbiran boleh digunakan. Tukar nilai sementara dalam `AdminUserSeed` sebelum deployment. Kata laluan disimpan sebagai hash menggunakan ASP.NET Core Identity. SQLite tidak mempunyai akaun pengguna/kata laluan terbina dalam; akses pangkalan data dikawal oleh aplikasi dan kebenaran fail sistem operasi. Simpan `puzzle.db` di luar direktori web awam, hadkan akses fail kepada akaun aplikasi sahaja, dan lindungi sandaran pada tahap yang sama.

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
- menetapkan masa mula dan tamat permainan dalam waktu Malaysia
- mengaktifkan atau menyahaktifkan permainan secara manual

## Pangkalan data dan benih data

Apabila aplikasi dimulakan, `AppDbContext` akan memastikan pangkalan data dibuat dan migrasi digunakan. `AdminUserSeed` akan memastikan jadual serta akaun pentadbir awal tersedia; `CrosswordSeed` akan menyemai teka silang kata awal jika data belum wujud. Jika susun atur teka silang kata sedia ada berubah, kedudukan petunjuk dan dimensi grid akan dikemas kini. Kemajuan pemain dipetakan semula mengikut kedudukan huruf dalam jawapan masing-masing.

Pentadbir boleh menetapkan waktu mula dan tamat setiap teka silang kata dalam waktu Malaysia; masa disimpan sebagai UTC. Pemain hanya boleh menyertai, membuka, menyimpan atau menghantar permainan ketika waktu itu aktif. Kedua-dua masa perlu ditetapkan untuk jadual automatik berfungsi. Pengaktifan manual oleh pentadbir membuka satu teka silang kata serta-merta dan mengatasi jadual; teka silang kata lain yang diaktifkan secara manual akan menggantikannya. Pangkalan data SQLite sedia ada ditambah secara automatik dengan lajur jadual apabila aplikasi bermula.

### Kemas kini utama: susun atur teka silang kata

- Grid semasa mempunyai 16 baris dan 18 lajur.
- Kedudukan petunjuk disusun supaya jawapan bersilang pada huruf yang sama dan setiap petunjuk mempunyai petak mula tersendiri.
- Susun atur disahkan semasa aplikasi dimulakan: semua jawapan berada dalam grid dan bersambung melalui persilangan, tanpa konflik huruf atau jujukan huruf tambahan yang bukan jawapan.
- Saiz paparan grid mengikuti dimensi baris dan lajur. Apabila susun atur pangkalan data dikemas kini, huruf pemain dipindahkan berdasarkan petunjuk dan kedudukannya dalam jawapan; huruf pada persilangan yang bercanggah tidak dibawa ke petak baharu.

## Peringatan

- Nama pengguna dan kata laluan pentadbir mesti ditetapkan melalui pengurusan rahsia untuk persekitaran pengeluaran.
- Gunakan HTTPS dan kata laluan unik yang kuat; sesi admin menggunakan kuki HttpOnly, Secure, SameSite dan tamat selepas dua jam.
- Sebarang data pemain dan jawapan yang disimpan dalam `puzzle.db` harus dilindungi mengikut keperluan keselamatan organisasi anda.

## Sumbangan

Bila-bila masa, anda boleh menambah baik fungsi, memperbaiki isu UI, atau memperluaskan kandungan teka silang kata. Pastikan perubahan diuji menggunakan perintah `dotnet build` sebelum menghantar kemas kini.

## Lesen

In progres.

## TODO

- [x] Fix the answer input to follow the direction.
- [x] Change the title to 'Kuiz Silang Kata Bulan Integriti MDHT 2026' in DB.
- [x] Fix UI for mobile view.
- [x] Fix UX behaviour and also interaction.
- [] 1st round 8 November 2026, 09:00 - 12 November 2026, 17:00
- [] 2nd round 15 November 2026, 09:00 - 19 November 2026, 17:00
