using System.Text;

// ==========================================================================
// Pages: قوالب الـ HTML - صفحة الكتالوج العامة، وصفحة إدارة الموقع (محمية).
// ==========================================================================
public static class Pages
{
    public const string CatalogHtml = @"<!DOCTYPE html>
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
  .thumb { height:130px; display:flex; align-items:center; justify-content:center; font-size:46px; background:linear-gradient(160deg, #F5F6FA, #E9ECF3); overflow:hidden; }
  .thumb img { width:100%; height:100%; object-fit:cover; }
  .card.out .thumb { filter:grayscale(1); opacity:0.6; }
  .stock-badge, .discount-badge { position:absolute; top:10px; padding:4px 10px; border-radius:20px; font-size:11px; font-weight:bold; z-index:2; }
  .stock-badge { inset-inline-start:10px; }
  .discount-badge { inset-inline-end:10px; background:#E74C3C; color:#fff; }
  .stock-badge.low { background:#FFF4E0; color:#C9821A; }
  .stock-badge.out { background:#1A2B4C; color:#fff; }
  .body { padding:12px 14px 0; }
  .name { font-size:14px; font-weight:600; min-height:38px; line-height:1.35; margin-bottom:8px; color:#1A2B4C; }
  .price-row { display:flex; align-items:baseline; gap:8px; flex-wrap:wrap; }
  .price { font-size:17px; font-weight:bold; color:#1A2B4C; }
  .price.out { color:#9AA3B2; }
  .original-price { font-size:12px; color:#9AA3B2; text-decoration:line-through; }
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
    const hasDiscount = p.originalPrice && p.originalPrice > p.salePrice;

    const card = document.createElement('div');
    card.className = 'card' + (outOfStock ? ' out' : '');

    let stockBadge = '';
    if (outOfStock) stockBadge = '<span class=""stock-badge out"">غير متوفر</span>';
    else if (lowStock) stockBadge = '<span class=""stock-badge low"">تبقى ' + p.quantity + ' فقط</span>';

    let discountBadge = '';
    let originalPriceHtml = '';
    if (hasDiscount) {
      const pct = Math.round((1 - p.salePrice / p.originalPrice) * 100);
      discountBadge = '<span class=""discount-badge"">-' + pct + '%</span>';
      originalPriceHtml = '<span class=""original-price"">' + p.originalPrice.toFixed(2) + ' ج.م</span>';
    }

    const thumbContent = p.hasImage
      ? '<img src=""/image/' + encodeURIComponent(p.barcode) + '"" alt="""">'
      : '📱';

    card.innerHTML =
      stockBadge + discountBadge +
      '<div class=""thumb"">' + thumbContent + '</div>' +
      '<div class=""body"">' +
        '<div class=""name"">' + escapeHtml(p.productName) + '</div>' +
        '<div class=""price-row"">' +
          '<span class=""price' + (outOfStock ? ' out' : '') + '"">' + p.salePrice.toFixed(2) + ' ج.م</span>' +
          originalPriceHtml +
        '</div>' +
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

    public static string BuildAdminHtml(List<CatalogProductDto> products)
    {
        var sb = new StringBuilder();
        sb.Append(@"<!DOCTYPE html>
<html lang=""ar"" dir=""rtl"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>إدارة كتالوج Temo</title>
<style>
  * { box-sizing:border-box; }
  body { margin:0; font-family:'Segoe UI', Tahoma, Arial, sans-serif; background:#F5F6FA; color:#1A2B4C; }
  header { background:#1A2B4C; color:#fff; padding:20px; text-align:center; }
  header h1 { margin:0; font-size:20px; }
  .wrap { max-width:900px; margin:0 auto; padding:20px 16px 60px; }
  .row { display:flex; gap:16px; background:#fff; border-radius:14px; padding:16px; margin-bottom:14px; box-shadow:0 2px 8px rgba(26,43,76,0.06); align-items:center; flex-wrap:wrap; }
  .thumb { width:70px; height:70px; border-radius:10px; background:#F0F1F5; display:flex; align-items:center; justify-content:center; font-size:28px; overflow:hidden; flex-shrink:0; }
  .thumb img { width:100%; height:100%; object-fit:cover; }
  .info { flex:1; min-width:180px; }
  .info .name { font-weight:bold; font-size:14px; margin-bottom:4px; }
  .info .meta { font-size:12px; color:#5B6675; }
  form { display:flex; gap:8px; align-items:center; }
  input[type=file] { font-size:12px; max-width:150px; }
  input[type=number] { width:100px; padding:6px 8px; border:1px solid #E1E4EA; border-radius:6px; }
  button { padding:7px 14px; border:none; border-radius:8px; background:#1A2B4C; color:#fff; font-size:12px; cursor:pointer; }
  button:hover { background:#26406F; }
  .empty { text-align:center; color:#9AA3B2; padding:60px 0; }
</style>
</head>
<body>
<header><h1>🛠️ إدارة كتالوج Temo Mobile Store</h1></header>
<div class=""wrap"">");

        if (products.Count == 0)
        {
            sb.Append("<div class=\"empty\">مفيش منتجات لسه - محتاج تعمل مزامنة من برنامج المحل الأول.</div>");
        }

        foreach (var p in products)
        {
            string barcodeEscaped = Uri.EscapeDataString(p.Barcode);
            string thumbContent = p.HasImage ? $"<img src=\"/image/{barcodeEscaped}\" alt=\"\">" : "📱";
            string nameEscaped = System.Net.WebUtility.HtmlEncode(p.ProductName);
            string currentOriginal = p.OriginalPrice.HasValue ? p.OriginalPrice.Value.ToString("0.##") : "";

            sb.Append($@"
<div class=""row"">
  <div class=""thumb"">{thumbContent}</div>
  <div class=""info"">
    <div class=""name"">{nameEscaped}</div>
    <div class=""meta"">السعر: {p.SalePrice:N2} ج.م — الكمية: {p.Quantity}</div>
  </div>
  <form method=""post"" enctype=""multipart/form-data"" action=""/admin/products/{barcodeEscaped}/image"">
    <input type=""file"" name=""image"" accept=""image/*"" required>
    <button type=""submit"">رفع صورة</button>
  </form>
  <form method=""post"" action=""/admin/products/{barcodeEscaped}/price"">
    <input type=""number"" step=""0.01"" name=""originalPrice"" placeholder=""سعر قبل الخصم"" value=""{currentOriginal}"">
    <button type=""submit"">حفظ</button>
  </form>
</div>");
        }

        sb.Append("</div></body></html>");
        return sb.ToString();
    }
}
