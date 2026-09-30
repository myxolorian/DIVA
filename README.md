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
- [x] Tahap 3: API customer dan jasa
- [x] Tahap 4: API order
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
docs/postman/          # collection Postman siap-import
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

Data awal (seed): **21 jasa dari price list DIVA Laundry** (7 kategori, lihat `Data/Configurations/LaundryServiceConfiguration.cs`) dan satu baris `outlet_profile` ("DIVA Laundry"). Setelah database dibuat, ubah harga lewat API (`PUT /api/services/{id}`), bukan di file seed.

### Menerapkan migration baru ke Supabase

Setiap kali ada migration baru (misalnya `OrderItemBilling`), database Supabase perlu diperbarui. Pilih salah satu:

- **SQL Editor (paling mudah):** buat script SQL dari migration terakhir yang sudah diterapkan, lalu paste di SQL Editor Supabase:
  ```bash
  cd BE/src/Diva.Api
  dotnet ef migrations script RealPriceList OrderItemBilling
  ```
  (Argumen pertama = migration terakhir yang sudah ada di database, kedua = tujuan.)
- **Langsung dari laptop:** `DIVA_DB_CONNECTION="<connection string>" dotnet ef database update` (PowerShell: `$env:DIVA_DB_CONNECTION = '<connection string>'`, lalu `dotnet ef database update`).

Cek hasilnya di tabel `__EFMigrationsHistory`: nama migration baru harus tercatat.

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

## Endpoint

Semua endpoint di bawah wajib login (`Authorization: Bearer <token>`). Error memakai format standar ProblemDetails; error validasi (400) berisi pesan per kolom di `errors`.

| Method | URL | Keterangan |
|---|---|---|
| GET | `/health` | Tanpa login. Cek server hidup |
| GET | `/api/me` | User yang sedang login |
| GET | `/api/services?includeInactive=false` | Daftar jasa, urut seperti price list (`sortOrder`) |
| GET | `/api/services/{id}` | Detail jasa |
| POST | `/api/services` | Tambah jasa: `{ "name", "category", "unit": "Kg"\|"Pcs"\|"M2", "price", "minQty"?, "maxPrice"?, "sortOrder"? }` |
| PUT | `/api/services/{id}` | Ubah jasa (boleh sertakan `isActive`) |
| DELETE | `/api/services/{id}` | Nonaktifkan jasa (tidak dihapus, supaya order lama tetap utuh) |
| GET | `/api/customers?search=&page=1&pageSize=20` | Daftar customer aktif; `search` mencocokkan sebagian nama atau no. telp |
| GET | `/api/customers/{id}` | Detail customer |
| POST | `/api/customers` | Tambah customer: `{ "name", "phone", "address", "notes" }` |
| PUT | `/api/customers/{id}` | Ubah customer |
| DELETE | `/api/customers/{id}` | Hapus customer (soft delete: `is_deleted = true`) |
| POST | `/api/orders` | Buat order: `{ "customerId", "items": [{ "serviceId", "qty", "unitPrice"? }], "notes"?, "dueDate"?, "paymentStatus"? }` |
| GET | `/api/orders?status=&paymentStatus=&customerId=&search=&from=&to=&page=&pageSize=` | Daftar order, terbaru dulu; `search` = nomor order / nama / telp; `from`/`to` = tanggal WIB (`yyyy-MM-dd`) |
| GET | `/api/orders/{id}` | Detail order + item |
| PATCH | `/api/orders/{id}/status` | `{ "status": "Baru"\|"Diproses"\|"Selesai"\|"Diambil" }` |
| PATCH | `/api/orders/{id}/payment` | `{ "paymentStatus": "BelumLunas"\|"Lunas" }` |

Aturan jasa: `minQty` = qty minimal yang ditagih (kiloan 3 kg, karpet 4 m²; qty lebih kecil ditagih sebesar minimum). `maxPrice` = batas atas untuk jasa berharga range (Dress Pesta/Kebaya Payet 60.000-150.000); harga sebenarnya diisi saat membuat order. Kedua aturan ini dipakai saat perhitungan order (tahap 4).

Aturan order (dihitung di server, FE hanya mengirim jasa dan qty):
- Qty di bawah `minQty` ditagih sebesar minimum: 2 kg Cuci Kering Setrika ditagih 3 kg (Rp21.000). Receipt menyimpan qty asli (`qty`) dan qty yang ditagih (`billedQty`).
- Jasa ber-`maxPrice` (Kebaya Payet) wajib mengirim `unitPrice` dalam rentangnya; jasa berharga tetap menolak `unitPrice`.
- Satuan Pcs harus bilangan bulat; Kg dan M2 boleh 2 desimal.
- Nama, satuan, harga, dan minimum jasa disalin ke `order_items`, jadi perubahan harga jasa tidak mengubah order lama.
- Nomor order `DIV-yyMMdd-NNNN`: tanggal WIB + penghitung dari sequence database (tidak reset harian, selalu unik). Setiap order punya `publicToken` acak untuk link receipt `/r/{token}` (halamannya di tahap 5).

Aturan customer: nama dan no. telp wajib. No. telp disimpan tanpa pemisah (`0812-3456 7890` jadi `081234567890`), harus 8-15 digit dan boleh diawali `+`. Satu no. telp hanya boleh dipakai satu customer aktif; kalau sudah dipakai, jawabannya `409` dengan `customerId` milik customer tersebut.

### Postman

Import `docs/postman/DIVA.postman_collection.json` (Postman > **Import**). Collection ini memakai environment `DIVA Local` (`baseUrl`, `supabaseUrl`, `publishableKey`, `ownerEmail`, `ownerPassword`, `token`). Jalankan **Auth > Login Supabase** dulu; request lain otomatis memakai token itu. Request "Tambah jasa" dan "Tambah customer" menyimpan id hasilnya supaya request Detail/Ubah/Hapus langsung bisa dipakai.

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

Tes yang butuh database (API customer dan jasa) memakai PostgreSQL sungguhan. Tanpa env var `DIVA_TEST_DB`, tes itu dilewati (*skipped*) dan sisanya tetap jalan. Di CI, GitHub Actions menyediakan PostgreSQL sehingga semua tes jalan.

Untuk menjalankannya di laptop, arahkan `DIVA_TEST_DB` ke server PostgreSQL **lokal** (misalnya dari installer PostgreSQL atau Docker):

```powershell
$env:DIVA_TEST_DB = "Host=localhost;Database=postgres;Username=postgres;Password=<password lokal>"
dotnet test
```

**Jangan arahkan `DIVA_TEST_DB` ke Supabase.** Tes membuat database sementara `diva_test_...`, menjalankan semua migration di sana, lalu menghapusnya.

## Docker

```bash
docker build -t diva .
docker run -p 8080:8080 -e ConnectionStrings__Default="..." diva
```
