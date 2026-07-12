using Microsoft.Data.Sqlite;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string dbPath = Path.Combine(AppContext.BaseDirectory, "catalog.db");
string connectionString = $"Data Source={dbPath}";

// السيكرت اللي البرنامج بيبعته وقت المزامنة عشان نتأكد إن الطلب فعلاً من برنامج المحل
// مش من حد تاني. اتحط كـ Environment Variable وقت النشر، مش مكتوب هنا في الكود.
string syncSecret = Environment.GetEnvironmentVariable("SYNC_SECRET") ?? "change-me-please";

EnsureSchema();

app.MapGet("/", () => Results.Content(CatalogHtml, "text/html; charset=utf-8"));

app.MapGet("/api/products", () =>
{
    var products = GetProducts();
    return Results.Json(products);
});

// المزامنة: برنامج المحل بيبعت هنا كل شوية بقائمة المنتجات كاملة، وهي بتستبدل الكتالوج
// القديم بالكامل (مش بتعمل دمج/مقارنة - أبسط وأقل عرضة للأخطاء لكتالوج بالحجم ده)
app.MapPost("/api/sync", async (HttpRequest request) =>
{
    if (request.Headers["X-Sync-Secret"] != syncSecret)
        return Results.Unauthorized();

    List<ProductDto>? products;
    try
    {
        products = await request.ReadFromJsonAsync<List<ProductDto>>();
    }
    catch
    {
        return Results.BadRequest("صيغة البيانات غير صحيحة.");
    }

    if (products == null) return Results.BadRequest("مفيش بيانات.");

    ReplaceProducts(products);
    return Results.Ok(new { synced = products.Count, at = DateTime.UtcNow });
});

app.Run();

void EnsureSchema()
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"CREATE TABLE IF NOT EXISTS Products (
        Barcode TEXT PRIMARY KEY,
        ProductName TEXT NOT NULL,
        SalePrice REAL NOT NULL DEFAULT 0,
        Quantity INTEGER NOT NULL DEFAULT 0
    );";
    cmd.ExecuteNonQuery();
}

List<ProductDto> GetProducts()
{
    var result = new List<ProductDto>();
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT Barcode, ProductName, SalePrice, Quantity FROM Products ORDER BY ProductName";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        result.Add(new ProductDto(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetDecimal(2),
            reader.GetInt32(3)));
    }
    return result;
}

void ReplaceProducts(List<ProductDto> products)
{
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    using var transaction = conn.BeginTransaction();
    try
    {
        using (var cmdDelete = conn.CreateCommand())
        {
            cmdDelete.Transaction = transaction;
            cmdDelete.CommandText = "DELETE FROM Products";
            cmdDelete.ExecuteNonQuery();
        }

        foreach (var p in products)
        {
            using var cmdInsert = conn.CreateCommand();
            cmdInsert.Transaction = transaction;
            cmdInsert.CommandText = "INSERT INTO Products (Barcode, ProductName, SalePrice, Quantity) VALUES (@B, @N, @P, @Q)";
            cmdInsert.Parameters.AddWithValue("@B", p.Barcode);
            cmdInsert.Parameters.AddWithValue("@N", p.ProductName);
            cmdInsert.Parameters.AddWithValue("@P", p.SalePrice);
            cmdInsert.Parameters.AddWithValue("@Q", p.Quantity);
            cmdInsert.ExecuteNonQuery();
        }

        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}

record ProductDto(string Barcode, string ProductName, decimal SalePrice, int Quantity);

partial class Program
{
    const string CatalogHtml = @"<!DOCTYPE html>
<html lang=""ar"" dir=""rtl"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>Temo Mobile Store - الكتالوج</title>
<style>
  * { box-sizing: border-box; }
  body { margin:0; font-family: 'Segoe UI', Tahoma, Arial, sans-serif; background:#F5F6FA; color:#1A2B4C; }
  header { background:#1A2B4C; color:#fff; padding:22px 20px; text-align:center; }
  header h1 { margin:0; font-size:22px; }
  header p { margin:6px 0 0; font-size:13px; color:#9AA3B2; }
  .wrap { padding:16px; max-width:960px; margin:0 auto; }
  .search { width:100%; padding:12px 16px; border-radius:12px; border:1px solid #E1E4EA; font-size:14px; margin-bottom:16px; }
  .grid { display:grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap:14px; }
  .card { background:#fff; border-radius:14px; padding:16px; box-shadow:0 2px 8px rgba(0,0,0,0.06); }
  .card .name { font-size:15px; font-weight:bold; margin-bottom:8px; min-height:38px; }
  .card .price { font-size:18px; font-weight:bold; color:#1A2B4C; margin-bottom:8px; }
  .badge { display:inline-block; padding:4px 10px; border-radius:20px; font-size:12px; font-weight:bold; }
  .badge.in { background:#E6F7EE; color:#27AE60; }
  .badge.out { background:#FDEAEA; color:#E74C3C; }
  .empty { text-align:center; color:#9AA3B2; padding:60px 0; }
  .updated { text-align:center; color:#9AA3B2; font-size:11px; margin-top:24px; }
</style>
</head>
<body>
<header>
  <h1>📱 Temo Mobile Store</h1>
  <p>كتالوج المنتجات المتاحة</p>
</header>
<div class=""wrap"">
  <input class=""search"" id=""search"" placeholder=""ابحث عن منتج..."" oninput=""render()"">
  <div class=""grid"" id=""grid""></div>
  <div class=""empty"" id=""empty"" style=""display:none"">مفيش منتجات مطابقة.</div>
  <div class=""updated"" id=""updated""></div>
</div>
<script>
let allProducts = [];

async function load() {
  try {
    const res = await fetch('/api/products');
    allProducts = await res.json();
    document.getElementById('updated').textContent = 'آخر تحديث: ' + new Date().toLocaleString('ar-EG');
    render();
  } catch (e) {
    document.getElementById('updated').textContent = 'حصل خطأ في تحميل الكتالوج';
  }
}

function render() {
  const q = document.getElementById('search').value.trim().toLowerCase();
  const filtered = allProducts.filter(p => p.productName.toLowerCase().includes(q));
  const grid = document.getElementById('grid');
  const empty = document.getElementById('empty');
  grid.innerHTML = '';
  empty.style.display = filtered.length === 0 ? 'block' : 'none';
  filtered.forEach(p => {
    const card = document.createElement('div');
    card.className = 'card';
    const inStock = p.quantity > 0;
    card.innerHTML =
      '<div class=""name"">' + escapeHtml(p.productName) + '</div>' +
      '<div class=""price"">' + p.salePrice.toFixed(2) + ' ج.م</div>' +
      '<span class=""badge ' + (inStock ? 'in' : 'out') + '"">' + (inStock ? 'متوفر ✅' : 'غير متوفر ❌') + '</span>';
    grid.appendChild(card);
  });
}

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

load();
setInterval(load, 60000);
</script>
</body>
</html>";
}
