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
- [x] Tahap 5: receipt (JSON, PDF, link publik)
- [x] Tahap 6: API dashboard
- [x] Tahap 7: frontend
- [x] Tahap 8-9: hardening dan deploy (Render)

## Struktur

```
BE/
  Diva.slnx
  src/Diva.Api/        # API: Domain/, Data/ (DbContext, Configurations, Migrations), Auth/, Features/
  tests/Diva.Tests/    # xUnit
FE/                    # halaman Bootstrap + JavaScript (assets/js/pages/ = satu script per halaman)
docs/postman/          # collection Postman siap-import
Dockerfile             # build BE, salin FE ke wwwroot
render.yaml            # Blueprint deploy ke Render (1 web service gratis)
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
| `Auth:DevBypass` | `true` = login palsu untuk development (lihat bagian Auth). Di luar Development aplikasi menolak start | Tidak, tapi jangan aktif di production |
| `Proxy:TrustForwardedHeaders` | `true` hanya kalau aplikasi berjalan di belakang proxy hosting (Render): IP pengunjung dan https dibaca dari header `X-Forwarded-*` | Tidak |

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
| GET | `/api/public/config` | Tanpa login. Alamat Supabase + publishable key untuk halaman login (tidak ada yang rahasia) |
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
| PATCH | `/api/orders/{id}/payment` | `{ "paymentStatus": "BelumLunas"\|"Lunas" }`. Menjadi Lunas → waktu lunas (`paidAt`) dicatat |
| DELETE | `/api/orders/{id}` | **Hapus permanen** order beserta isinya; link receipt-nya jadi 404. Nomor order tidak dipakai ulang |
| GET | `/api/reports/income?from=&to=` | Laporan pemasukan (lihat di bawah); `from`/`to` = tanggal WIB, default tanggal 1 bulan ini s.d. hari ini, maksimal 366 hari |
| GET | `/api/dashboard/summary?date=` | Ringkasan untuk halaman depan (lihat di bawah); `date` = tanggal WIB `yyyy-MM-dd`, default hari ini |
| GET | `/api/outlet` | Profil laundry (nama, alamat, telp, footer receipt) |
| PUT | `/api/outlet` | Ubah profil laundry |

**Tanpa login** (akses hanya lewat token acak order, dibatasi 60 request/menit per IP, tidak di-cache, `noindex`):

| Method | URL | Keterangan |
|---|---|---|
| GET | `/r/{token}` | **Link yang dibagikan ke customer**: halaman receipt (`FE/receipt.html`) dengan tombol Unduh PDF, Salin link, Bagikan |
| GET | `/api/public/receipts/{token}` | Data receipt (JSON). Telp customer disamarkan (`0812*****890`), tanpa alamat dan tanpa id |
| GET | `/api/public/receipts/{token}/pdf` | Receipt PDF format struk 80 mm, nama file = nomor order |

Aturan jasa: `minQty` = qty minimal yang ditagih (kiloan 3 kg, karpet 4 m²; qty lebih kecil ditagih sebesar minimum). `maxPrice` = batas atas untuk jasa berharga range (Dress Pesta/Kebaya Payet 60.000-150.000); harga sebenarnya diisi saat membuat order. Kedua aturan ini dipakai saat perhitungan order (tahap 4).

Aturan order (dihitung di server, FE hanya mengirim jasa dan qty):
- Qty di bawah `minQty` ditagih sebesar minimum: 2 kg Cuci Kering Setrika ditagih 3 kg (Rp21.000). Receipt menyimpan qty asli (`qty`) dan qty yang ditagih (`billedQty`).
- Jasa ber-`maxPrice` (Kebaya Payet) wajib mengirim `unitPrice` dalam rentangnya; jasa berharga tetap menolak `unitPrice`.
- Satuan Pcs harus bilangan bulat; Kg dan M2 boleh 2 desimal.
- Nama, satuan, harga, dan minimum jasa disalin ke `order_items`, jadi perubahan harga jasa tidak mengubah order lama.
- Nomor order `DIV-yyMMdd-NNNN`: tanggal WIB + penghitung dari sequence database (tidak reset harian, selalu unik). Setiap order punya `publicToken` acak untuk link receipt `/r/{token}`.

Isi ringkasan dashboard:

| Field | Arti |
|---|---|
| `today` | Order yang **masuk** pada tanggal itu (WIB): jumlah, nilai total, dan pecahannya menurut status bayar saat ini (`paidTotal` / `unpaidTotal`). Ini nilai order, bukan uang yang diterima |
| `income` | **Pemasukan** (uang yang diterima): order yang ditandai Lunas hari itu (`today`) dan sejak tanggal 1 bulan itu (`thisMonth`), menurut waktu lunasnya |
| `unpaid` | Semua order yang masih `BelumLunas` (piutang), dari semua tanggal |
| `statusCounts` | Jumlah order per status, dari semua tanggal |
| `dueToday` / `overdue` | Order berstatus Baru/Diproses yang tanggal selesainya hari ini / sudah lewat |
| `last7Days` | 7 hari sampai tanggal itu, termasuk hari tanpa order (nilai 0) |
| `recentOrders` | 5 order terbaru, bentuknya sama dengan list order |

**Pemasukan** dihitung pada saat order ditandai Lunas (`paid_at`), bukan saat order dibuat: order Senin yang dibayar Rabu adalah pemasukan Rabu. Menandai Lunas dua kali tidak menggeser tanggalnya; mengembalikan ke Belum lunas menghapusnya dari pemasukan. Order yang sudah Lunas sebelum kolom ini ada diberi `paid_at` = tanggal order (migration `OrderPaidAt`). Laporan `/api/reports/income` berisi `total`, `orders`, `days` (setiap hari dalam rentang, yang kosong bernilai 0) dan `items` (order yang lunas, waktu lunas terbaru dulu).

Aturan customer: nama dan no. telp wajib. No. telp disimpan tanpa pemisah (`0812-3456 7890` jadi `081234567890`), harus 8-15 digit dan boleh diawali `+`. Satu no. telp hanya boleh dipakai satu customer aktif; kalau sudah dipakai, jawabannya `409` dengan `customerId` milik customer tersebut.

### Postman

Import `docs/postman/DIVA.postman_collection.json` (Postman > **Import**). Collection ini memakai environment `DIVA Local` (`baseUrl`, `supabaseUrl`, `publishableKey`, `ownerEmail`, `ownerPassword`, `token`). Jalankan **Auth > Login Supabase** dulu; request lain otomatis memakai token itu. Request "Tambah jasa" dan "Tambah customer" menyimpan id hasilnya supaya request Detail/Ubah/Hapus langsung bisa dipakai.

## Frontend

File di `FE/` disajikan oleh API yang sama (satu alamat untuk API dan halaman web). Saat development, API membaca langsung dari folder `FE/` repo (`Frontend:Path` di `appsettings.Development.json`); di Docker, isinya disalin ke `wwwroot`. File statis ini publik, sedangkan datanya tetap dilindungi login di API.

Cara membuka: jalankan API (lihat **Menjalankan**), lalu buka `http://localhost:5189` di browser dan login dengan akun owner Supabase.

| Halaman | Isi |
|---|---|
| `login.html` | Login email + password (langsung ke Supabase Auth, token disimpan di browser) |
| `index.html` | Beranda: pemasukan hari ini dan bulan ini, nilai order hari ini, belum lunas, status order, grafik 7 hari, order terbaru, peringatan order terlambat |
| `order-new.html` | Buat order 3 langkah: pilih/tambah customer → pilih jasa + qty (dikelompokkan per kategori) → tanggal selesai, pembayaran, catatan |
| `order.html?id=` | Detail order: ubah status, tandai lunas, kirim receipt lewat WhatsApp, salin link, unduh PDF, hapus order |
| `laporan.html` | Laporan pemasukan: Hari ini, 7 hari, Bulan ini, Bulan lalu, atau tanggal pilihan sendiri; total, rata-rata per hari, hari terbaik, grafik per hari, daftar order lunas |
| `orders.html` | Daftar order dengan filter status / pembayaran dan pencarian |
| `customers.html` | Customer tersimpan: cari, tambah, ubah, hapus, buat order, WhatsApp/telepon |
| `services.html` | Jasa & harga: ubah harga, tambah jasa, nonaktifkan |
| `settings.html` | "Lainnya" di HP: menu Laporan, Jasa & Harga, Customer; profil laundry (tampil di receipt) dan logout |
| `receipt.html` | Receipt publik untuk customer (`/r/{token}`) |

Tampilan dibuat untuk HP dulu (navigasi bawah + tombol "+" di tengah), lalu melebar menjadi sidebar di layar ≥ 992px.

Bootstrap 5.3.8, Bootstrap Icons 1.13.1, dan huruf Plus Jakarta Sans disimpan di `FE/assets/vendor/` (bukan CDN), jadi aplikasi tidak bergantung pada server pihak ketiga. Semua teks dari database ditulis lewat `textContent` (bukan `innerHTML`), sehingga isian seperti nama customer tidak bisa menyisipkan script. Halaman dikirim dengan `Cache-Control: no-cache`: browser selalu mengecek versi terbaru setelah ada update.

Setelah login, `assets/js/api.js` otomatis menambahkan token ke setiap request, memperbarui token yang hampir kedaluwarsa, dan kembali ke halaman login kalau API membalas 401.

PDF dibuat dengan QuestPDF (lisensi Community: gratis untuk usaha dengan omzet di bawah USD 1 juta/tahun).

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

## Keamanan di production

- Semua respons membawa header keamanan: `Content-Security-Policy` (hanya script dari DIVA sendiri; koneksi keluar hanya ke Supabase Auth), `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy`.
- Di luar Development: HSTS (browser selalu memakai https), error 500 tanpa detail teknis, dan `Auth:DevBypass` ditolak.
- Di belakang proxy (`Proxy:TrustForwardedHeaders=true`), batas 60 request/menit untuk receipt publik dihitung per IP pengunjung asli, bukan per IP proxy.
- Ukuran body request maksimal 1 MB.

## Docker

```bash
docker build -t diva .
docker run -p 8080:8080 -e ConnectionStrings__Default="..." diva
```

Image berisi API + halaman FE (`wwwroot`), mendengarkan http di port 8080. HTTPS disediakan oleh hosting di depannya.

## Deploy ke Render (gratis)

`render.yaml` di root repo adalah *Blueprint*: Render membaca file ini dan membuat 1 web service gratis dari `Dockerfile`, region Singapore, health check `/health`, dan deploy otomatis setiap ada perubahan di branch `main`.

1. Daftar/login di [render.com](https://render.com) dengan akun GitHub.
2. **New > Blueprint**, pilih repo **DIVA** (beri Render akses ke repo itu kalau diminta), lalu **Connect**.
3. Render meminta nilai `ConnectionStrings__Default`: paste connection string **Session pooler** Supabase yang sama dengan di user-secrets laptop (dengan `Trust Server Certificate=true`). Nilai ini hanya tersimpan di Render, tidak di GitHub.
4. Klik **Apply / Deploy Blueprint**. Build pertama ±5-10 menit; tunggu status **Live**.
5. Buka alamatnya (`https://diva-xxxx.onrender.com`), login dengan akun owner Supabase.

Catatan paket gratis:
- Aplikasi **tidur setelah 15 menit tanpa pengunjung**; pembukaan berikutnya menunggu ±1 menit. Supaya tetap bangun, buat monitor gratis di [UptimeRobot](https://uptimerobot.com) atau [cron-job.org](https://cron-job.org) yang membuka `https://diva-xxxx.onrender.com/health` setiap 10 menit (1 service menyala 24 jam ≈ 744 jam, masih di bawah kuota 750 jam/bulan).
- Supabase paket gratis mem-*pause* project yang 7 hari tidak dipakai; dibuka lagi dari dashboard Supabase.
- Migration baru tidak dijalankan otomatis: SQL-nya tetap dijalankan di Supabase SQL Editor sebelum/bersamaan dengan deploy.
