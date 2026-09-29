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
- [ ] Tahap 2: auth (JWT Supabase)
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
  src/Diva.Api/        # API: Domain/, Data/ (DbContext, Configurations, Migrations)
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
dotnet user-secrets set "ConnectionStrings:Default" "Host=...;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<password>;SSL Mode=Require"
```

Di production, isi lewat environment variable `ConnectionStrings__Default`.

**Supabase:** ambil connection string dari *Project Settings > Database > Connection string* dan pilih **Session pooler** (kompatibel IPv4). Jangan pakai Transaction pooler (port 6543), karena tidak cocok dengan prepared statements Npgsql.

## Migrasi database

```bash
cd BE/src/Diva.Api

# menambah migration baru setelah mengubah model (tidak perlu koneksi database)
dotnet ef migrations add <NamaMigration> -o Data/Migrations

# menerapkan ke database
DIVA_DB_CONNECTION="<connection string>" dotnet ef database update
```

Migration `EnableRowLevelSecurity` mengaktifkan RLS tanpa policy di semua tabel. Supabase membuka tabel di schema `public` lewat REST API-nya, jadi tanpa RLS siapa pun yang punya anon key bisa membaca data. Dengan RLS, hanya API di repo ini (yang konek sebagai `postgres`) yang bisa mengakses data. **Tabel baru harus ikut ditambahkan ke daftar `Tables` di migration tersebut lewat migration baru**; ada tes yang gagal kalau ada tabel yang terlewat.

Data awal (seed): 6 jasa dengan harga contoh dan satu baris `outlet_profile` ("DIVA Laundry"). Ubah lewat aplikasi setelah fitur pengaturan dan jasa jadi.

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
