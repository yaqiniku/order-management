# Order Management API

Project ini adalah REST API sederhana untuk mengelola customer, product, order, dan order detail. API dibuat dengan ASP.NET Core 8 dan PostgreSQL. Bagian yang paling saya perhatikan di project ini adalah konsistensi data ketika beberapa request masuk secara bersamaan.

## Persiapan

Yang perlu disiapkan:

- .NET 8 SDK
- PostgreSQL
- EF Core CLI (`dotnet-ef`)

Buat file `API/.env` dengan konfigurasi database lokal. File ini tidak ikut disimpan ke Git.

```env
DB_HOST=localhost
DB_PORT=5432
DB_NAME=order_management
DB_USERNAME=postgres
DB_PASSWORD=password_anda
```

Setelah database dibuat, jalankan migration:

```bash
dotnet ef database update --project DAL --startup-project API
```

Kemudian jalankan API:

```bash
dotnet run --project API
```

Alamat Swagger akan muncul di terminal saat aplikasi berjalan. Sebelum membuat order, buat data customer dan product terlebih dahulu. Untuk membuat order, gunakan endpoint `POST /api/Order/Insert` dan sertakan header `Idempotency-Key`, atau bisa dianggap kayak requestID yang nantninya digenerate dan diinsert ke database.

## Idempotency

Saya memilih menggunakan header `Idempotency-Key`. Alasannya, client dapat mengirim key yang sama ketika melakukan retry, misalnya karena koneksi terputus atau tombol submit tidak sengaja ditekan dua kali.

Key tersebut disimpan pada kolom `orders.idempotency_key` yang memiliki unique index. Proses insert juga menggunakan `ON CONFLICT DO NOTHING`. Jadi, pengecekan idempotency tidak hanya dilakukan oleh aplikasi, tetapi tetap dijaga oleh database.

Jika request dengan key yang sama dikirim kembali:

- order baru tidak dibuat;
- stock tidak dikurangi lagi;
- client mendapatkan ID order yang sebelumnya sudah dibuat;
- nilai `affectedRows` untuk request ulang adalah `0`.

Pendekatan ini juga melindungi kasus ketika dua request dengan key yang sama masuk hampir bersamaan sebelum request pertama sempat commit.

## Penanganan concurrency

### Dua order berebut stock yang sama

Contohnya, Product X memiliki stock 15, sedangkan dua user masing-masing memesan 10 unit.

Saat order diproses, row product dikunci menggunakan `SELECT ... FOR UPDATE`. Request yang mendapat lock lebih dahulu akan memeriksa dan mengurangi stock. Request kedua menunggu, kemudian membaca nilai stock yang sudah terbaru. Dengan begitu, hanya satu order yang berhasil dan stock tidak mungkin terpotong melebihi jumlah yang tersedia.

Sebagai perlindungan tambahan, tabel product juga memiliki check constraint `quantity >= 0`.

### Dua admin mengubah status order yang sama

Perubahan status juga mengambil row lock pada header order. Misalnya dua admin mencoba melakukan `Ship` dan `Cancel` pada order `CONFIRMED` yang sama. Salah satu request akan menang, sedangkan request berikutnya akan membaca status terbaru dan gagal dengan `409 Conflict` karena transisinya sudah tidak valid.

Jika `Cancel` yang menang, pengembalian stock dilakukan dalam transaction yang sama. Ini mencegah status berubah tetapi stock belum kembali, atau sebaliknya.

### Dua request membuat order dengan idempotency key yang sama

Untuk kasus ini, unique index di database menjadi penentu akhirnya. Walaupun kedua request sempat sama-sama tidak menemukan order saat pengecekan awal, PostgreSQL tetap hanya mengizinkan satu row dengan idempotency key tersebut.

## Race condition lain yang diperhatikan

Selain tiga skenario utama, ada beberapa kondisi lain yang perlu dijaga:

1. **Edit detail bersamaan dengan Ship atau Cancel.** Kedua proses mengunci header order terlebih dahulu. Detail hanya boleh diubah ketika status order masih `PENDING`.
2. **Dua request mengubah atau menghapus detail yang sama.** Setelah header dikunci, detail dibaca ulang agar proses tidak memakai data lama. Stock product juga dikunci sebelum quantity diubah.
3. **Cancel dikirim dua kali.** Request kedua akan melihat status `CANCELLED` dan berhenti sebelum mengembalikan stock untuk kedua kalinya.
4. **Order berisi beberapa product.** Product dikunci berdasarkan urutan ID yang konsisten untuk mengurangi kemungkinan deadlock antar-request.

## Response error

Semua endpoint menggunakan bentuk response error yang sama. Contohnya:

```json
{
  "error": {
    "code": "conflict",
    "message": "Status order tidak dapat diubah dari 'SHIPPED' menjadi 'CANCELLED'.",
    "correlationId": "b71c9dfe89a34eeca9bc77a38f214e0d"
  }
}
```

Status code yang digunakan:

- `400 Bad Request` untuk input yang tidak valid;
- `404 Not Found` jika data tidak ditemukan;
- `409 Conflict` untuk stock tidak cukup, status tidak valid, atau conflict lain;
- `500 Internal Server Error` untuk error yang tidak diperkirakan.

Detail internal dari error 500 tidak dikirim ke client.

## Logging dan correlation ID

Setiap request dicatat menggunakan `ILogger`, bukan `Console.WriteLine`. Informasi yang dicatat meliputi HTTP method, path, status code, durasi request, exception, dan correlation ID.

Client dapat mengirim header `X-Correlation-ID`. Jika header tersebut tidak ada, server akan membuat ID baru. Correlation ID dikembalikan melalui response header dan body error sehingga request yang bermasalah lebih mudah dicari di log.

## Database dan migration

Saya menggunakan PostgreSQL karena mendukung transaction, row-level lock, unique constraint, dan check constraint yang diperlukan oleh project ini. Perubahan schema disimpan sebagai EF Core migration di folder `DAL/Migrations`.

## Automated test

Test berada di `Tests/OrderConcurrencyTests.cs`. Test menggunakan PostgreSQL sungguhan karena database in-memory tidak dapat menggambarkan perilaku `FOR UPDATE`, transaction, dan unique constraint secara akurat.

Saat ini terdapat tiga integration test:

1. **Concurrent stock deduction**  
   Dua order masing-masing meminta 10 unit ketika stock hanya 15. Test memastikan satu order berhasil, satu gagal, hanya satu order tersimpan, dan stock akhir menjadi 5.

2. **Idempotency under race**  
   Dua order dikirim bersamaan menggunakan idempotency key yang sama. Test memastikan hanya satu order tersimpan, kedua request mendapatkan ID yang sama, dan stock hanya berkurang satu kali.

3. **Concurrent status update**  
   Order dibuat sampai berstatus `CONFIRMED`, lalu `Ship` dan `Cancel` dijalankan bersamaan. Test memastikan hanya satu operasi berhasil. Jika hasil akhirnya `SHIPPED`, stock tetap 5. Jika hasil akhirnya `CANCELLED`, stock kembali menjadi 15.

Untuk menjalankan semua test:

```bash
dotnet test Tests/Tests.csproj -m:1
```

Untuk menampilkan hasil yang lebih lengkap:

```bash
dotnet test Tests/Tests.csproj -m:1 --logger "console;verbosity=normal"
```

Test secara default membaca koneksi dari `API/.env`. Walaupun begitu, sebaiknya gunakan database khusus test agar tidak bercampur dengan data development:

```bash
TEST_DB_CONNECTION='Host=localhost;Port=5432;Database=order_management_test;Username=postgres;Password=password_anda' \
dotnet test Tests/Tests.csproj -m:1
```

Sebelum test berjalan, migration akan diterapkan dan data customer serta product khusus test akan dibuat. Setiap data memakai ID acak. Setelah test selesai, data yang dibuat oleh test tersebut akan dihapus kembali.

Hasil yang diharapkan:

```text
Passed: 3
Failed: 0
Skipped: 0
Total: 3
```

## Mencoba secara manual melalui Swagger

Concurrency lebih mudah dibuktikan melalui automated test, tetapi alurnya juga dapat dicoba secara manual:

1. Buat customer dan Product X dengan stock 15.
2. Siapkan dua request order yang masing-masing membeli 10 unit dengan idempotency key berbeda.
3. Jalankan keduanya hampir bersamaan. Salah satu request seharusnya berhasil dan request lainnya mendapat `409 Conflict`. Stock akhir harus 5.
4. Ulangi menggunakan idempotency key yang sama. Kedua response harus mengarah ke ID order yang sama dan hanya satu order yang tersimpan.
5. Untuk status order, siapkan order `CONFIRMED`. Jalankan endpoint `Ship` dan `Cancel` hampir bersamaan dari dua tab Swagger. Hanya satu perubahan status yang boleh berhasil.
