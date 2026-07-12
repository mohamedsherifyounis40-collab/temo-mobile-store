var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string dbPath = Path.Combine(AppContext.BaseDirectory, "catalog.db");
CatalogDb.Init(dbPath);

// السيكرت اللي البرنامج بيبعته وقت المزامنة (اسم/سعر/كمية بس)، ومنفصل عن كلمة سر الإدارة
string syncSecret = Environment.GetEnvironmentVariable("SYNC_SECRET") ?? "change-me-please";
// كلمة سر صفحة إدارة الموقع (صورة + سعر قبل الخصم) - منفصلة تمامًا عن نظام مستخدمين برنامج المحل
string adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "change-me-please";

app.MapGet("/", () => Results.Content(Pages.CatalogHtml, "text/html; charset=utf-8"));

app.MapGet("/api/products", () => Results.Json(CatalogDb.GetProducts()));

app.MapGet("/image/{barcode}", (string barcode) =>
{
    var image = CatalogDb.GetImage(barcode);
    if (image == null) return Results.NotFound();
    return Results.File(image.Value.Data, image.Value.ContentType);
});

// المزامنة: برنامج المحل بيبعت هنا كل شوية بقائمة المنتجات (اسم/سعر/كمية بس)، وبتتحدّث
// جوه قاعدة بيانات الموقع من غير ما تمسح الصور أو أسعار الخصم اللي اتحطت من صفحة الإدارة
app.MapPost("/api/sync", async (HttpRequest request) =>
{
    if (request.Headers["X-Sync-Secret"] != syncSecret)
        return Results.Unauthorized();

    List<SyncProductDto>? products;
    try
    {
        products = await request.ReadFromJsonAsync<List<SyncProductDto>>();
    }
    catch
    {
        return Results.BadRequest("صيغة البيانات غير صحيحة.");
    }

    if (products == null) return Results.BadRequest("مفيش بيانات.");

    CatalogDb.SyncProducts(products);
    return Results.Ok(new { synced = products.Count, at = DateTime.UtcNow });
});

// ---------- صفحة إدارة الموقع: صورة كل منتج + سعر قبل الخصم (محميّة بكلمة سر منفصلة) ----------

app.MapGet("/admin", (HttpRequest request) =>
{
    if (!AdminAuth.Check(request, adminPassword)) return AdminAuth.UnauthorizedPrompt();
    return Results.Content(Pages.BuildAdminHtml(CatalogDb.GetProducts()), "text/html; charset=utf-8");
});

app.MapPost("/admin/products/{barcode}/image", async (string barcode, HttpRequest request) =>
{
    if (!AdminAuth.Check(request, adminPassword)) return AdminAuth.UnauthorizedPrompt();

    if (!request.HasFormContentType) return Results.BadRequest("مفيش صورة مرفوعة.");
    var form = await request.ReadFormAsync();
    var file = form.Files["image"];
    if (file == null || file.Length == 0) return Results.BadRequest("مفيش صورة مرفوعة.");
    if (file.Length > 5 * 1024 * 1024) return Results.BadRequest("الصورة أكبر من 5 ميجا.");

    using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    bool found = CatalogDb.SetImage(barcode, ms.ToArray(), file.ContentType);
    if (!found) return Results.NotFound("المنتج ده مش موجود (ممكن يكون اتشال من آخر مزامنة).");

    return Results.Redirect("/admin");
});

app.MapPost("/admin/products/{barcode}/price", async (string barcode, HttpRequest request) =>
{
    if (!AdminAuth.Check(request, adminPassword)) return AdminAuth.UnauthorizedPrompt();

    var form = await request.ReadFormAsync();
    string? originalPriceText = form["originalPrice"];

    decimal? originalPrice = null;
    if (!string.IsNullOrWhiteSpace(originalPriceText))
    {
        if (!decimal.TryParse(originalPriceText, out decimal parsed))
            return Results.BadRequest("السعر غير صحيح.");
        originalPrice = parsed;
    }

    bool found = CatalogDb.SetOriginalPrice(barcode, originalPrice);
    if (!found) return Results.NotFound("المنتج ده مش موجود (ممكن يكون اتشال من آخر مزامنة).");

    return Results.Redirect("/admin");
});

app.Run();
