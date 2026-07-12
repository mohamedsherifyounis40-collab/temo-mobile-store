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
  header { background:linear-gradient(135deg, #1A2B4C, #26406F); color:#fff; padding:36px 20px 44px; text-align:center; }
  header h1 { margin:0; font-size:26px; letter-spacing:0.5px; }
  header p { margin:8px 0 0; font-size:13px; color:#B9C2D6; }
  .wrap { padding:0 16px 40px; max-width:1100px; margin:-26px auto 0; }
  .search-bar { background:#fff; border-radius:14px; box-shadow:0 4px 16px rgba(26,43,76,0.12); padding:6px; display:flex; align-items:center; margin-bottom:22px; }
  .search-bar span { padding:0 12px; font-size:16px; color:#9AA3B2; }
  .search { flex:1; border:none; outline:none; padding:12px 4px; font-size:14px; background:transparent; font-family:inherit; }
  .section-title { font-size:16px; font-weight:bold; color:#1A2B4C; margin:0 0 14px; }
  .grid { display:grid; grid-template-columns: repeat(auto-fill, minmax(210px, 1fr)); gap:16px; }
  .card { position:relative; background:#fff; border-radius:16px; padding:0 0 14px; box-shadow:0 2px 10px rgba(26,43,76,0.07); overflow:hidden; transition:transform .15s ease, box-shadow .15s ease; }
  .card:hover { transform:translateY(-3px); box-shadow:0 8px 20px rgba(26,43,76,0.14); }
  .thumb { height:130px; display:flex; align-items:center; justify-content:center; font-size:46px; background:linear-gradient(160deg, #F5F6FA, #E9ECF3); }
  .card.out .thumb { filter:grayscale(1); opacity:0.6; }
  .stock-badge { position:absolute; top:10px; inset-inline-start:10px; padding:4px 10px; border-radius:20px; font-size:11px; font-weight:bold; z-index:2; }
  .stock-badge.low { background:#FFF4E0; color:#C9821A; }
  .stock-badge.out { background:#1A2B4C; color:#fff; }
  .body { padding:12px 14px 0; }
  .name { font-size:14px; font-weight:600; min-height:38px; line-height:1.35; margin-bottom:8px; color:#1A2B4C; }
  .price { font-size:17px; font-weight:bold; color:#1A2B4C; }
  .price.out { color:#9AA3B2; }
  .empty { text-align:center; color:#9AA3B2; padding:70px 0; font-size:14px; }
  .updated { text-align:center; color:#9AA3B2; font-size:11px; margin-top:28px; }
</style>
</head>
<body>
<header>
  <h1>📱 Temo Mobile Store</h1>
  <p>كتالوج المنتجات المتاحة</p>
</header>
<div class=""wrap"">
  <div class=""search-bar""><span>🔍</span><input class=""search"" id=""search"" placeholder=""ابحث عن منتج..."" oninput=""render()""></div>
  <div class=""section-title"">📦 كل المنتجات</div>
  <div class=""grid"" id=""grid""></div>
  <div class=""empty"" id=""empty"" style=""display:none"">مفيش منتجات مطابقة.</div>
  <div class=""updated"" id=""updated""></div>
</div>
<script>
let allProducts = [];
const LOW_STOCK_THRESHOLD = 5;

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
    const outOfStock = p.quantity <= 0;
    const lowStock = !outOfStock && p.quantity <= LOW_STOCK_THRESHOLD;

    const card = document.createElement('div');
    card.className = 'card' + (outOfStock ? ' out' : '');

    let badgeHtml = '';
    if (outOfStock) badgeHtml = '<span class=""stock-badge out"">غير متوفر</span>';
    else if (lowStock) badgeHtml = '<span class=""stock-badge low"">تبقى ' + p.quantity + ' فقط</span>';

    card.innerHTML =
      badgeHtml +
      '<div class=""thumb"">📱</div>' +
      '<div class=""body"">' +
        '<div class=""name"">' + escapeHtml(p.productName) + '</div>' +
        '<div class=""price' + (outOfStock ? ' out' : '') + '"">' + p.salePrice.toFixed(2) + ' ج.م</div>' +
      '</div>';
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
