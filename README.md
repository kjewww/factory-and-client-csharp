# Tutorial Integration Testing ASP.NET Core dengan `WebApplicationFactory` dan `HttpClient`

## Daftar Isi

1. [Pengenalan Singkat](#1-pengenalan-singkat)
2. [Persiapan Lingkungan](#2-persiapan-lingkungan)
3. [Instalasi](#3-instalasi)
4. [Langkah Implementasi Pengujian](#4-langkah-implementasi-pengujian)
5. [Cara Menjalankan](#5-cara-menjalankan)
6. [Hasil yang Diharapkan](#6-hasil-yang-diharapkan)
7. [Source Code Lengkap](#7-source-code-lengkap)
8. [Tips dan Troubleshooting](#8-tips-dan-troubleshooting)
9. [Rangkuman](#9-rangkuman)

---

## 1. Pengenalan Singkat

Repository ini berisi contoh **pengujian integrasi (integration testing)** untuk sebuah REST API sederhana bernama **TodoApi** menggunakan C#/.NET. Pengujian integrasi berbeda dengan *unit test*: jika unit test menguji satu fungsi secara terpisah, pengujian integrasi menguji **alur lengkap dari HTTP request hingga database**, termasuk routing, model binding, validasi, controller, service, dan EF Core.

Komponen utama yang dipakai:

| Komponen                               | Fungsi                                                                                 |
| -------------------------------------- | -------------------------------------------------------------------------------------- |
| **ASP.NET Core 10 (`net10.0`)**        | Framework untuk membuat REST API                                                       |
| **Entity Framework Core + SQLite**     | Lapisan penyimpanan data                                                               |
| **`WebApplicationFactory<T>`**         | Menjalankan aplikasi di dalam proses test (Tanpa perlu membuka port/network sungguhan) |
| **`HttpClient`**                       | Mengirim permintaan HTTP ke aplikasi yang dijalankan `WebApplicationFactory`           |
| **NUnit 4**                            | Framework test dan assertion (`Assert.That`)                                           |
| **`Microsoft.AspNetCore.Mvc.Testing`** | Menyediakan `WebApplicationFactory` dan `TestServer`                                   |

Alur pengujian yang terjadi:

```
Test (NUnit)
   │  CreateClient()
   ▼
HttpClient  ──► GET / POST / PUT / DELETE /api/todos
   │                      │
   │                      ▼
   │              TodosController ──► ITodoService ──► TodoDbContext
   │                                                       │
   │                                                       ▼
   │                                          SQLite in-memory (:memory:)
   ▼
Assert.That(statusCode / isi response)
```

**Keunggulan pendekatan ini:**

- Tidak perlu menjalankan server API secara manual sebelum `dotnet test`.
- Setiap test mendapat database **bersih dan terisolasi** (SQLite `:memory:`), sehingga test tidak saling mengganggu dan tidak merusak data asli (`todos.db`).
- Seluruh lapisan aplikasi ikut teruji, bukan hanya logika bisnis.

---

## 2. Persiapan Lingkungan

Sebelum mulai, siapkan hal-hal berikut:

### Perangkat lunak wajib

1. **.NET SDK 10.0** (atau lebih baru, selama masih mendukung target `net10.0`)
   - Unduh di: <https://dotnet.microsoft.com/download>
   - Cek instalasi:

   ```bash
   dotnet --version
   # Contoh hasil: 10.0.401
   ```

2. **Editor/IDE** (pilih salah satu):
   - Visual Studio 2022/2026 (dengan workload *ASP.NET and web development*)
   - Visual Studio Code + ekstensi C# / C# Dev Kit
   - JetBrains Rider

3. **Git** (opsional, hanya jika ingin clone dari repository)

### Pengetahuan dasar yang dibutuhkan

- Dasar C# (async/await, interface, dependency injection)
- Konsep REST API (GET, POST, PUT, DELETE dan status code HTTP)
- Dasar Entity Framework Core (`DbContext`, `DbSet`)

> **Catatan:** Karena target framework-nya `net10.0`, SDK versi 8 atau 9 **tidak akan bisa** membangun proyek ini. Gunakan `dotnet --list-sdks` untuk melihat SDK yang terpasang.

---

## 3. Instalasi

### 3.1 Mendapatkan source code

Jika repository tersedia:

```bash
git clone <url-repository> TodoIntegration
cd TodoIntegration
```

Atau, jika mendapat file `.zip`, ekstrak lalu masuk ke folder hasil ekstraksi.

### 3.2 Restore dependency

```bash
dotnet restore TodoIntegration.slnx
```

Perintah ini mengunduh paket NuGet yang dideklarasikan di kedua proyek (`Microsoft.AspNetCore.OpenApi`, `Microsoft.EntityFrameworkCore.Sqlite`, `NUnit`, `Microsoft.AspNetCore.Mvc.Testing`, dll).

### 3.3 Verifikasi build

```bash
dotnet build TodoIntegration.slnx
```

### 3.4 Struktur direktori

```
TodoIntegration/
├── TodoIntegration.slnx              # Solusi yang berisi 2 proyek
│
├── TodoApi/                          # Proyek REST API
│   ├── TodoApi.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   │
│   ├── Controllers/
│   │   └── TodosController.cs
│   │
│   ├── Data/
│   │   └── TodoDbContext.cs
│   │
│   ├── Models/
│   │   └── Todo.cs
│   │
│   └── Services/
│       ├── ITodoService.cs
│       └── TodoService.cs
│   
└── TodoApi.Tests/                    # Proyek pengujian integrasi
    ├── TodoApi.Tests.csproj
    ├── CustomWebApplicationFactory.cs
    └── TodosApiTests.cs
```

> Folder `bin/` dan `obj/` adalah hasil build dan dapat diabaikan (biasanya tidak dimasukkan ke Git).

---

## 4. Langkah Implementasi Pengujian

Berikut urutan membangun aplikasi **dan** pengujiannya dari awal. Kode lengkap ada di [Bab 7](#7-source-code-lengkap).

### Langkah 1 — Buat solusi dan proyek API

```bash
dotnet new sln -n TodoIntegration
dotnet new webapi -n TodoApi -f net10.0
dotnet sln TodoIntegration.slnx add TodoApi/TodoApi.csproj
cd TodoApi
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.0
cd ..
```

Proyek API memakai SDK `Microsoft.NET.Sdk.Web` dan target `net10.0`.

### Langkah 2 — Buat model dengan validasi

File `TodoApi/Models/Todo.cs`:

```csharp
public class Todo
{
    public int Id { get; set; }

    [Required]
    [MinLength(3)]
    public string Title { get; set; } = "";

    public bool IsCompleted { get; set; } = false;
}
```

`[Required]` dan `[MinLength(3)]` penting karena `[ApiController]` akan **otomatis mengembalikan 400 Bad Request** jika request tidak valid. Ini akan diuji oleh test `Create_WithEmptyTitle_ShouldReturn400` dan `Create_WithShortTitle_ShouldReturn400`.

### Langkah 3 — Siapkan DbContext

File `TodoApi/Data/TodoDbContext.cs` berisi `DbSet<Todo> Todos`. Di `Program.cs`, context didaftarkan dengan SQLite file biasa:

```csharp
builder.Services.AddDbContext<TodoDbContext>(options =>
{
    options.UseSqlite("Data Source=todos.db");
});
```

Pada aplikasi nyata, data tersimpan di file `todos.db`. Pada test, konfigurasi ini akan **ditimpa** dengan SQLite in-memory (lihat Langkah 8).

### Langkah 4 — Buat layer service

`ITodoService` mendeklarasikan operasi `GetAllAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`. `TodoService` mengimplementasikannya memakai `TodoDbContext` dan di-register sebagai `Scoped` di `Program.cs`:

```csharp
builder.Services.AddScoped<ITodoService, TodoService>();
```

### Langkah 5 — Buat controller REST

`TodosController` dengan route `[Route("api/todos")]` menyediakan:

| Method   | Endpoint          | Status sukses                     |
| -------- | ----------------- | --------------------------------- |
| `GET`    | `/api/todos`      | `200 OK`                          |
| `GET`    | `/api/todos/{id}` | `200 OK`                          |
| `POST`   | `/api/todos`      | `201 Created` + header `Location` |
| `PUT`    | `/api/todos/{id}` | `204 No Content`                  |
| `DELETE` | `/api/todos/{id}` | `204 No Content`                  |

Untuk data yang tidak ditemukan, controller mengembalikan `404 Not Found`.

### Langkah 6 — Buka akses ke `Program` (WAJIB)

Tambahkan di bagian paling bawah `Program.cs`:

```csharp
// Agar Program dapat diakses oleh project testing
public partial class Program { }
```

Tanpa baris ini, `WebApplicationFactory<Program>` akan gagal dengan error aksesibilitas karena *top-level statements* menghasilkan kelas `Program` bersifat internal.

### Langkah 7 — Buat proyek test dan pasang paket

```bash
dotnet new nunit -n TodoApi.Tests -f net10.0
dotnet sln TodoIntegration.slnx add TodoApi.Tests/TodoApi.Tests.csproj
cd TodoApi.Tests
dotnet add package Microsoft.AspNetCore.Mvc.Testing --version 10.0.12
dotnet add reference ..\TodoApi\TodoApi.csproj
cd ..
```

Paket penting di `TodoApi.Tests.csproj`:

```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.0" />
<PackageReference Include="NUnit" Version="4.3.2" />
<PackageReference Include="NUnit3TestAdapter" Version="5.0.0" />
```

`Microsoft.AspNetCore.Mvc.Testing` membawa `WebApplicationFactory` dan `TestServer`, sedangkan `ProjectReference` ke `TodoApi` membuat test bisa memakai kelas `Program`, `Todo`, dan `TodoDbContext`.

### Langkah 8 — Buat `CustomWebApplicationFactory`

File `TodoApi.Tests/CustomWebApplicationFactory.cs` adalah **kunci isolasi test**. Urutan logikanya:

1. Buat dan buka koneksi SQLite `:memory:` di constructor. Koneksi harus tetap terbuka selama test berjalan, kalau tidak database akan hilang.
2. Override `ConfigureWebHost`, lalu hapus registrasi `TodoDbContext` dan `DbContextOptions<TodoDbContext>` bawaan aplikasi dengan `RemoveAll<>()`.
3. Daftarkan ulang `TodoDbContext` yang memakai koneksi in-memory tersebut.
4. Panggil `db.Database.EnsureCreated()` agar tabel `Todos` langsung tersedia.
5. Di `Dispose`, tutup koneksi SQLite setelah test selesai.

Potongan intinya:

```csharp
protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.ConfigureServices(services =>
    {
        services.RemoveAll<TodoDbContext>();
        services.RemoveAll<DbContextOptions<TodoDbContext>>();

        services.AddDbContext<TodoDbContext>(options =>
        {
            options.UseSqlite(_connection);
        });

        var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
        db.Database.EnsureCreated();
    });
}
```

Karena setiap test membuat `new CustomWebApplicationFactory()`, setiap test juga mendapat koneksi `:memory:` baru — inilah sebabnya `GetAll_ShouldReturnOkAndEmptyList` selalu melihat daftar kosong meskipun test lain pernah membuat data.

### Langkah 9 — Tulis test dengan `HttpClient`

Pola umum setiap test:

```csharp
using var factory = new CustomWebApplicationFactory();
using var client = factory.CreateClient();      // HttpClient ke TestServer

var response = await client.PostAsJsonAsync("/api/todos", new { title = "Belajar C#", isCompleted = false });

Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
var todo = await response.Content.ReadFromJsonAsync<Todo>();
Assert.That(todo!.Id, Is.GreaterThan(0));
```

Method `HttpClient` yang dipakai:

- `GetAsync(url)` → `GET`
- `PostAsJsonAsync(url, obj)` → `POST` + body JSON
- `PutAsJsonAsync(url, obj)` → `PUT` + body JSON
- `DeleteAsync(url)` → `DELETE`
- `ReadFromJsonAsync<T>()` → mengubah response JSON menjadi objek C#

Assertion memakai gaya NUnit: `Assert.That(aktual, Is.EqualTo(...))`, `Is.Not.Null`, `Is.Empty`, `Is.True`, `Is.GreaterThan(0)`.

### Langkah 10 — Cakupan test yang dibuat

| Skenario                       | Nama test                                       | Yang diverifikasi                          |
| ------------------------------ | ----------------------------------------------- | ------------------------------------------ |
| GET semua data                 | `GetAll_ShouldReturnOkAndEmptyList`             | `200` dan list kosong pada database baru   |
| GET by id (ada)                | `GetById_ShouldReturnTodo`                      | Data yang baru dibuat bisa diambil kembali |
| GET by id (tidak ada)          | `GetById_WhenNotFound_ShouldReturn404`          | `404` untuk id `999`                       |
| POST valid                     | `Create_ShouldReturnCreated`                    | `201`, header `Location`, `Id > 0`         |
| PUT valid                      | `Update_ShouldReturnNoContentAndPersistChanges` | `204` dan perubahan tersimpan              |
| PUT tidak ada                  | `Update_WhenNotFound_ShouldReturn404`           | `404`                                      |
| DELETE valid                   | `Delete_ShouldReturnNoContentAndRemoveTodo`     | `204` lalu data hilang (`404`)             |
| DELETE tidak ada               | `Delete_WhenNotFound_ShouldReturn404`           | `404`                                      |
| POST title kosong              | `Create_WithEmptyTitle_ShouldReturn400`         | Validasi `[Required]` → `400`              |
| POST title terlalu pendek      | `Create_WithShortTitle_ShouldReturn400`         | Validasi `[MinLength(3)]` → `400`          |
| Validasi gagal tidak menyimpan | `Create_InvalidData_ShouldNotPersistTodo`       | Setelah `400`, list tetap kosong           |

Total **11 test**.

---

## 5. Cara Menjalankan

### 5.1 Menjalankan seluruh pengujian

Dari folder root (tempat file `TodoIntegration.slnx` berada):

```bash
dotnet test TodoIntegration.slnx
```

Atau dari folder proyek test:

```bash
cd TodoApi.Tests
dotnet test
```

### 5.2 Menjalankan sebagian test (filter)

```bash
dotnet test TodoIntegration.slnx --filter "FullyQualifiedName~Create_"
dotnet test TodoIntegration.slnx --filter "FullyQualifiedName~GetById_WhenNotFound"
```

### 5.3 Menampilkan nama test saat berjalan

```bash
dotnet test TodoIntegration.slnx --logger "console;verbosity=detailed"
```

### 5.4 Menjalankan API secara manual (opsional)

Ini **tidak perlu** untuk pengujian, tapi berguna untuk mencoba endpoint langsung:

```bash
dotnet run --project TodoApi
```

API tersedia di `http://localhost:5142` (sesuai `TodoApi/Properties/launchSettings.json`). Contoh permintaan:

```bash
# GET semua todo
curl http://localhost:5142/api/todos

# POST todo baru
curl -X POST http://localhost:5142/api/todos \
  -H "Content-Type: application/json" \
  -d "{\"title\":\"Belajar WebFactory\",\"isCompleted\":false}"

# GET by id
curl http://localhost:5142/api/todos/1

# PUT
curl -X PUT http://localhost:5142/api/todos/1 \
  -H "Content-Type: application/json" \
  -d "{\"title\":\"Belajar integration test\",\"isCompleted\":true}"

# DELETE
curl -X DELETE http://localhost:5142/api/todos/1
```

> **Catatan:** File `TodoApi/TodoApi.http` bawaan template masih berisi contoh endpoint `weatherforecast/` yang **tidak ada** di aplikasi ini. Ganti isinya dengan endpoint `/api/todos` di atas jika ingin memakai fitur *HTTP file* di Visual Studio/Rider.

Jika dijalankan manual, database nyata (`todos.db`) akan dibuat di folder kerja aplikasi. Test **tidak** memakai file ini, jadi data manual tidak akan mengganggu hasil test.

---

## 6. Hasil yang Diharapkan

Jika semua langkah benar, perintah `dotnet test TodoIntegration.slnx` menghasilkan output seperti berikut:

```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  TodoApi -> .../TodoApi/bin/Debug/net10.0/TodoApi.dll
  TodoApi.Tests -> .../TodoApi.Tests/bin/Debug/net10.0/TodoApi.Tests.dll
Test run for .../TodoApi.Tests/bin/Debug/net10.0/TodoApi.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    11, Skipped:     0, Total:    11, Duration: 4 s - TodoApi.Tests.dll (net10.0)
```

**Indikator keberhasilan:**

- `Failed: 0`
- `Passed: 11`
- `Total: 11`
- Tidak ada test yang `Skipped`

Artinya, seluruh alur HTTP (routing → validasi → controller → service → database SQLite in-memory) bekerja sesuai ekspektasi:

- Status code sesuai tabel di [Langkah 10](#langkah-10--cakupan-test-yang-dibuat).
- Data yang dibuat melalui `POST` bisa dibaca kembali lewat `GET`.
- Perubahan `PUT` tersimpan (`persist`) dan `DELETE` benar-benar menghapus data.
- Input yang melanggar validasi ditolak dengan `400` dan **tidak masuk ke database**.
- Setiap test melihat database kosong karena memakai koneksi `:memory:` masing-masing.

> **Catatan tentang warning:** pada environment yang sudah terpasang paket lama, mungkin muncul peringatan `NU1903` tentang `SQLitePCLRaw.lib.e_sqlite3 2.1.11`. Ini adalah **warning dari dependency transitif**, bukan kegagalan test. Test tetap `Passed`. Jika ingin menghilangkannya, jalankan `dotnet list package --vulnerable` lalu perbarui paket EF Core / SQLite ke versi yang sudah diperbaiki.

---

## 7. Source Code Lengkap

Berikut seluruh source code aplikasi dan pengujian agar tutorial bisa diikuti dari nol.

### 7.1 `TodoIntegration.slnx`

```xml
<Solution>
  <Project Path="TodoApi.Tests/TodoApi.Tests.csproj" />
  <Project Path="TodoApi/TodoApi.csproj" />
</Solution>
```

### 7.2 `TodoApi/TodoApi.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.12" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />
  </ItemGroup>

</Project>
```

### 7.3 `TodoApi/Program.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Register Controller
builder.Services.AddControllers();

// Register Database
builder.Services.AddDbContext<TodoDbContext>(options =>
{
    options.UseSqlite("Data Source=todos.db");
});

// Register Service
builder.Services.AddScoped<ITodoService, TodoService>();

var app = builder.Build();

// Routing
app.MapControllers();

app.Run();

// Agar Program dapat diakses oleh project testing
public partial class Program { }
```

### 7.4 `TodoApi/Models/Todo.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace TodoApi.Models;

public class Todo
{
    public int Id { get; set; }

    [Required]
    [MinLength(3)]
    public string Title { get; set; } = "";

    public bool IsCompleted { get; set; } = false;
}
```

### 7.5 `TodoApi/Data/TodoDbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using TodoApi.Models;

namespace TodoApi.Data;

public class TodoDbContext : DbContext
{
    public TodoDbContext(
        DbContextOptions<TodoDbContext> options
    ) : base(options)
    {
    }

    public DbSet<Todo> Todos => Set<Todo>();
}
```

### 7.6 `TodoApi/Services/ITodoService.cs`

```csharp
using TodoApi.Models;

namespace TodoApi.Services;

public interface ITodoService
{
    Task<List<Todo>> GetAllAsync();

    Task<Todo?> GetByIdAsync(int id);

    Task<Todo> CreateAsync(Todo todo);

    Task<bool> UpdateAsync(int id, Todo todo);

    Task<bool> DeleteAsync(int id);
}
```

### 7.7 `TodoApi/Services/TodoService.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Models;

namespace TodoApi.Services;

public class TodoService : ITodoService
{
    private readonly TodoDbContext _db;

    public TodoService(TodoDbContext db)
    {
        _db = db;
    }

    // GET ALL
    public async Task<List<Todo>> GetAllAsync()
    {
        return await _db.Todos.ToListAsync();
    }

    // GET BY ID
    public async Task<Todo?> GetByIdAsync(int id)
    {
        return await _db.Todos.FindAsync(id);
    }

    // CREATE
    public async Task<Todo> CreateAsync(Todo todo)
    {
        _db.Todos.Add(todo);

        await _db.SaveChangesAsync();

        return todo;
    }

    // UPDATE
    public async Task<bool> UpdateAsync(int id, Todo todo)
    {
        var existingTodo = await _db.Todos.FindAsync(id);

        if (existingTodo == null)
        {
            return false;
        }

        existingTodo.Title = todo.Title;
        existingTodo.IsCompleted = todo.IsCompleted;

        await _db.SaveChangesAsync();

        return true;
    }

    // DELETE
    public async Task<bool> DeleteAsync(int id)
    {
        var todo = await _db.Todos.FindAsync(id);

        if (todo == null)
        {
            return false;
        }

        _db.Todos.Remove(todo);

        await _db.SaveChangesAsync();

        return true;
    }
}
```

### 7.8 `TodoApi/Controllers/TodosController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using TodoApi.Models;
using TodoApi.Services;

namespace TodoApi.Controllers;

[ApiController]
[Route("api/todos")]
public class TodosController : ControllerBase
{
    private readonly ITodoService _service;

    public TodosController(ITodoService service)
    {
        _service = service;
    }

    // GET: /api/todos
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var todos = await _service.GetAllAsync();

        return Ok(todos);
    }

    // GET: /api/todos/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var todo = await _service.GetByIdAsync(id);

        if (todo == null)
        {
            return NotFound();
        }

        return Ok(todo);
    }

    // POST: /api/todos
    [HttpPost]
    public async Task<IActionResult> Create(Todo todo)
    {
        var createdTodo = await _service.CreateAsync(todo);

        return CreatedAtAction(
            nameof(GetById),
            new { id = createdTodo.Id },
            createdTodo
        );
    }

    // PUT: /api/todos/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Todo todo)
    {
        var success = await _service.UpdateAsync(id, todo);

        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }

    // DELETE: /api/todos/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _service.DeleteAsync(id);

        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }
}
```

### 7.9 `TodoApi.Tests/TodoApi.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />
    <PackageReference Include="NUnit" Version="4.3.2" />
    <PackageReference Include="NUnit.Analyzers" Version="4.7.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="5.0.0" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="NUnit.Framework" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\TodoApi\TodoApi.csproj" />
  </ItemGroup>

</Project>
```

### 7.10 `TodoApi.Tests/CustomWebApplicationFactory.cs`

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TodoApi.Data;

namespace TodoApi.Tests;

public class CustomWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public CustomWebApplicationFactory()
    {
        _connection = new SqliteConnection(
            "Data Source=:memory:"
        );

        _connection.Open();
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Hapus konfigurasi database asli
            services.RemoveAll<TodoDbContext>();

            services.RemoveAll<
                DbContextOptions<TodoDbContext>
            >();

            // Gunakan SQLite in-memory
            services.AddDbContext<TodoDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Buat tabel database
            var serviceProvider = services.BuildServiceProvider();

            using var scope = serviceProvider.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<TodoDbContext>();

            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
```

### 7.11 `TodoApi.Tests/TodosApiTests.cs`

```csharp
using System.Net;
using System.Net.Http.Json;
using TodoApi.Models;

namespace TodoApi.Tests;

public class TodosApiTests
{
    // ==================================
    // GET ALL
    // ==================================

    [Test]
    public async Task GetAll_ShouldReturnOkAndEmptyList()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/todos");

        Assert.That(response.StatusCode,
            Is.EqualTo(HttpStatusCode.OK));

        var todos = await response.Content
            .ReadFromJsonAsync<List<Todo>>();

        Assert.That(todos, Is.Not.Null);
        Assert.That(todos, Is.Empty);
    }

    // ==================================
    // GET BY ID
    // ==================================

    [Test]
    public async Task GetById_ShouldReturnTodo()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        // Create data first
        var createResponse = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = "Belajar C#",
                isCompleted = false
            }
        );

        var created = await createResponse.Content
            .ReadFromJsonAsync<Todo>();

        // Get by ID
        var response = await client.GetAsync(
            $"/api/todos/{created!.Id}"
        );

        Assert.That(response.StatusCode,
            Is.EqualTo(HttpStatusCode.OK));

        var todo = await response.Content
            .ReadFromJsonAsync<Todo>();

        Assert.That(todo, Is.Not.Null);
        Assert.That(todo!.Title, Is.EqualTo("Belajar C#"));
    }

    [Test]
    public async Task GetById_WhenNotFound_ShouldReturn404()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/todos/999");

        Assert.That(response.StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ==================================
    // POST / CREATE
    // ==================================

    [Test]
    public async Task Create_ShouldReturnCreated()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = "Belajar ASP.NET Core",
                isCompleted = false
            }
        );

        Assert.That(response.StatusCode,
            Is.EqualTo(HttpStatusCode.Created));

        Assert.That(response.Headers.Location, Is.Not.Null);

        var todo = await response.Content
            .ReadFromJsonAsync<Todo>();

        Assert.That(todo, Is.Not.Null);
        Assert.That(todo!.Id, Is.GreaterThan(0));
        Assert.That(todo.Title, Is.EqualTo("Belajar ASP.NET Core"));
    }

    // ==================================
    // PUT / UPDATE
    // ==================================

    [Test]
    public async Task Update_ShouldReturnNoContentAndPersistChanges()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        // Create
        var createResponse = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = "Belajar C#",
                isCompleted = false
            }
        );

        var created = await createResponse.Content
            .ReadFromJsonAsync<Todo>();

        // Update
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/todos/{created!.Id}",
            new
            {
                title = "Belajar ASP.NET Core",
                isCompleted = true
            }
        );

        Assert.That(updateResponse.StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));

        // Verify persistence
        var getResponse = await client.GetAsync(
            $"/api/todos/{created.Id}"
        );

        var updated = await getResponse.Content
            .ReadFromJsonAsync<Todo>();

        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Title,
            Is.EqualTo("Belajar ASP.NET Core"));
        Assert.That(updated.IsCompleted, Is.True);
    }

    [Test]
    public async Task Update_WhenNotFound_ShouldReturn404()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/todos/999",
            new
            {
                title = "Tugas baru",
                isCompleted = true
            }
        );

        Assert.That(response.StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ==================================
    // DELETE
    // ==================================

    [Test]
    public async Task Delete_ShouldReturnNoContentAndRemoveTodo()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        // Create
        var createResponse = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = "Tugas yang akan dihapus",
                isCompleted = false
            }
        );

        var created = await createResponse.Content
            .ReadFromJsonAsync<Todo>();

        // Delete
        var deleteResponse = await client.DeleteAsync(
            $"/api/todos/{created!.Id}"
        );

        Assert.That(deleteResponse.StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));

        // Verify deletion
        var getResponse = await client.GetAsync(
            $"/api/todos/{created.Id}"
        );

        Assert.That(getResponse.StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_WhenNotFound_ShouldReturn404()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/todos/999");

        Assert.That(response.StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ==================================
    // VALIDATION
    // ==================================

    [Test]
    public async Task Create_WithEmptyTitle_ShouldReturn400()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = "",
                isCompleted = false
            }
        );

        Assert.That(response.StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Create_WithShortTitle_ShouldReturn400()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = "AB",
                isCompleted = false
            }
        );

        Assert.That(response.StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Create_InvalidData_ShouldNotPersistTodo()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/todos",
            new
            {
                title = "",
                isCompleted = false
            }
        );

        Assert.That(response.StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest));

        var getResponse = await client.GetAsync("/api/todos");

        var todos = await getResponse.Content
            .ReadFromJsonAsync<List<Todo>>();

        Assert.That(todos, Is.Not.Null);
        Assert.That(todos, Is.Empty);
    }
}
```

---

## 8. Tips dan Troubleshooting

| Masalah                                             | Penyebab umum                                                | Solusi                                                                                                      |
| --------------------------------------------------- | ------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------- |
| Error `Program` tidak bisa diakses dari proyek test | Lupa menambahkan `public partial class Program { }`          | Tambahkan di bawah `app.Run();`                                                                             |
| Test gagal dengan `400` padahal data valid          | Target framework/SDK tidak cocok, atau body JSON salah ketik | Pastikan SDK 10.x dan gunakan `PostAsJsonAsync`                                                             |
| `GetAll_ShouldReturnOkAndEmptyList` tidak kosong    | Test memakai database file asli, bukan in-memory             | Pastikan `CustomWebApplicationFactory` dipakai dan `RemoveAll<DbContextOptions<TodoDbContext>>()` tetap ada |
| Database in-memory kosong saat diakses              | Koneksi `:memory:` ditutup lebih awal                        | Biarkan koneksi terbuka sampai `Dispose()` factory dipanggil (gunakan `using var factory`)                  |
| Warning `NU1903 SQLitePCLRaw.lib.e_sqlite3`         | Dependency transitif lama                                    | Bukan error; update paket jika ingin menghilangkan warning                                                  |
| `dotnet run` gagal: port `5142` dipakai             | Aplikasi lain memakai port tersebut                          | Ubah `applicationUrl` di `launchSettings.json` atau hentikan proses yang bentrok                            |
| Test tidak terdeteksi                               | Adapter NUnit belum terpasang                                | Pastikan `NUnit3TestAdapter` dan `Microsoft.NET.Test.Sdk` ada di `.csproj`                                  |

---

## 9. Rangkuman

Dengan mengikuti tutorial ini, mahasiswa akan:

1. Membangun REST API Todo sederhana dengan ASP.NET Core, EF Core, dan SQLite.
2. Memahami peran `WebApplicationFactory<Program>` untuk menjalankan aplikasi di dalam proses test.
3. Menggunakan `HttpClient` untuk mengirim request HTTP tanpa menyalakan server manual.
4. Mengganti database produksi dengan SQLite `:memory:` agar setiap test terisolasi.
5. Menguji seluruh endpoint beserta skenario sukses, *not found*, dan validasi dengan NUnit.
6. Menjalankan 11 test dengan hasil `Passed: 11, Failed: 0`.

Selamat mencoba dan selamat belajar pengujian perangkat lunak!
