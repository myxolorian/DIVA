# DIVA — Laundry Management

Aplikasi manajemen laundry: dashboard, customer tersimpan, order (jasa + qty), dan receipt yang bisa diunduh sebagai PDF atau dibagikan lewat link.

| Lapisan | Teknologi |
|---|---|
| Backend (`BE/`) | ASP.NET Core Web API, .NET 10, EF Core + Npgsql |
| Database | Supabase (PostgreSQL) |
| Frontend (`FE/`) | Bootstrap 5 + JavaScript (tanpa build step) |
| PDF | QuestPDF |

## Status

- [x] Tahap 0: fondasi (solution, paket, Dockerfile, CI)
- [x] Tahap 1: data layer (entity, migration, sequence nomor order, RLS, seed)
- [x] Tahap 2: auth (JWT Supabase)
- [ ] Tahap 3: API customer dan jasa
- [ ] Tahap 4: API order
- [ ] Tahap 5: receipt (JSON, PDF, link publik)
- [ ] Tahap 6: API dashboard
- [ ] Tahap 7: frontend
- [ ] Tahap 8-9: hardening dan deploy

## Struktur

```
BE/
  Diva.slnx
  src/Diva.Api/        # API: Domain/, Data/ (DbContext, Configurations, Migrations), Auth/, Features/
  tests/Diva.Tests/    # xUnit
FE/                    # halaman Bootstrap (menyusul di tahap 7)
Dockerfile             # build BE, salin FE ke wwwroot
```

## Prasyarat

- .NET 10 SDK
- Tool EF Core: `dotnet tool install --global dotnet-ef`
- Sebuah database PostgreSQL (project Supabase, atau Postgres lokal untuk development)

## Konfigurasi

Rahasia tidak pernah di-commit. Untuk development pakai user-secrets:

```bash
cd BE/src/Diva.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=<host pooler>;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true"
```

`Trust Server Certificate=true` diperlukan karena sertifikat pooler Supabase tidak ada di daftar sertifikat tepercaya bawaan sistem. Koneksi tetap terenkripsi.

Di production, isi lewat environment variable `ConnectionStrings__Default`.

| Pengaturan | Isi | Rahasia? |
|---|---|---|
| `ConnectionStrings:Default` | Connection string Session Pooler | **Ya** (user-secrets / env var) |
| `Supabase:Url` | Alamat project, sudah diisi di `appsettings.json` | Tidak |
| `Auth:DevBypass` | `true` = login palsu untuk development (lihat bagian Auth) | Tidak, tapi jangan aktif di production |

**Supabase:** ambil connection string dari *Project Settings > Database > Connection string* dan pilih **Session pooler** (kompatibel IPv4). Jangan pakai Transaction pooler (port 6543), karena tidak cocok dengan prepared statements Npgsql.

## Migrasi database

```bash
cd BE/src/Diva.Api

# menambah migration baru setelah mengubah model (tidak perlu koneksi database)
dotnet ef migrations add <NamaMigration> -o Data/Migrations

# menerapkan ke database
DIVA_DB_CONNECTION="<connection string>" dotnet ef database update
```

Kalau tabel sudah dibuat lewat SQL Editor Supabase (SQL yang sama dengan migration, termasuk catatan di `__EFMigrationsHistory`), `database update` tidak akan melakukan apa-apa. Itu normal.

Migration `EnableRowLevelSecurity` mengaktifkan RLS tanpa policy di semua tabel. Supabase membuka tabel di schema `public` lewat REST API-nya, jadi tanpa RLS siapa pun yang punya anon key bisa membaca data. Dengan RLS, hanya API di repo ini (yang konek sebagai `postgres`) yang bisa mengakses data. **Tabel baru harus ikut ditambahkan ke daftar `Tables` di migration tersebut lewat migration baru**; ada tes yang gagal kalau ada tabel yang terlewat.

Data awal (seed): 6 jasa dengan harga contoh dan satu baris `outlet_profile` ("DIVA Laundry"). Ubah lewat aplikasi setelah fitur pengaturan dan jasa jadi.

## Auth (login)

Login dilakukan di **Supabase Auth**, bukan di API ini. Alurnya:

1. FE mengirim email + password ke Supabase dan menerima *access token* (JWT).
2. FE memanggil API dengan header `Authorization: Bearer <token>`.
3. API memeriksa token: tanda tangan (public key diunduh dari `https://<project-ref>.supabase.co/auth/v1/.well-known/jwks.json`), issuer, audience `authenticated`, dan masa berlaku. Kalau gagal, jawabannya `401`.

API tidak menyimpan rahasia apa pun untuk auth. Semua endpoint wajib login, kecuali yang ditandai `AllowAnonymous()` (sekarang hanya `/health`). Algoritma yang diterima: ES256 dan RS256.

Endpoint pertama yang dilindungi: `GET /api/me` (mengembalikan id dan email user yang login).

### Mencoba tanpa Supabase (dev bypass)

Hanya untuk komputer development. Setiap request dianggap login sebagai `dev@diva.local`:

```bash
cd BE/src/Diva.Api
dotnet user-secrets set "Auth:DevBypass" "true"
dotnet run
# di terminal lain:
curl http://localhost:5189/api/me
```

User-secrets hanya dibaca di environment Development. Kalau `Auth:DevBypass=true` terbaca di environment lain (misalnya Production lewat env var), aplikasi **menolak start**. Untuk mematikan bypass: `dotnet user-secrets remove "Auth:DevBypass"`.

### Mencoba dengan login Supabase sungguhan

Prasyarat: user owner sudah dibuat di **Authentication > Users**, dan di **Project Settings > JWT Keys** yang berstatus *Current key* adalah **ECC (P-256)**. Kalau yang aktif masih *Legacy HS256*, token akan ditolak.

Bash:

```bash
TOKEN=$(curl -s -X POST "https://<project-ref>.supabase.co/auth/v1/token?grant_type=password" \
  -H "apikey: <publishable key>" -H "Content-Type: application/json" \
  -d '{"email":"<email owner>","password":"<password owner>"}' | jq -r .access_token)
curl -i http://localhost:5189/api/me -H "Authorization: Bearer $TOKEN"
```

PowerShell:

```powershell
$body = @{ email = "<email owner>"; password = "<password owner>" } | ConvertTo-Json
$login = Invoke-RestMethod -Method Post -ContentType "application/json" -Body $body `
  -Uri "https://<project-ref>.supabase.co/auth/v1/token?grant_type=password" `
  -Headers @{ apikey = "<publishable key>" }
Invoke-RestMethod http://localhost:5189/api/me -Headers @{ Authorization = "Bearer $($login.access_token)" }
```

Hasil yang benar: `200` dengan `{"id":"...","email":"..."}`. Kalau `401`, lihat log API (di Development, alasan penolakan token tercatat): audience atau issuer tidak cocok berarti `Supabase:Url` salah; kunci tidak ditemukan atau algoritma tidak diizinkan biasanya berarti signing key yang aktif di Supabase bukan ECC.

Jangan menempelkan token atau password di chat, issue, atau commit.

## Menjalankan

```bash
cd BE/src/Diva.Api
dotnet run          # http://localhost:5189, cek GET /health
```

## Tes

```bash
cd BE
dotnet test
```

## Docker

```bash
docker build -t diva .
docker run -p 8080:8080 -e ConnectionStrings__Default="..." diva
```
