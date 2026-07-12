using Microsoft.Data.Sqlite;

// ==========================================================================
// CatalogDb: كل الوصول لقاعدة بيانات الموقع (catalog.db - منفصلة تمامًا عن
// قاعدة بيانات برنامج المحل). الاسم/السعر/الكمية مصدرها المزامنة من البرنامج
// (upsert + حذف أي منتج مابقاش موجود)، والصورة وسعر ما قبل الخصم مصدرهم صفحة
// إدارة الموقع نفسها ومحفوظين هنا بس - مش بيتمسحوا وقت المزامنة.
// ==========================================================================
public static class CatalogDb
{
    private static string connectionString = "";

    public static void Init(string dbPath)
    {
        connectionString = $"Data Source={dbPath}";
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS Products (
            Barcode TEXT PRIMARY KEY,
            ProductName TEXT NOT NULL,
            SalePrice REAL NOT NULL DEFAULT 0,
            Quantity INTEGER NOT NULL DEFAULT 0,
            OriginalPrice REAL,
            ImageData BLOB,
            ImageContentType TEXT
        );";
        cmd.ExecuteNonQuery();
    }

    // ---------- الكتالوج العام ----------

    public static List<CatalogProductDto> GetProducts()
    {
        var result = new List<CatalogProductDto>();
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Barcode, ProductName, SalePrice, Quantity, OriginalPrice, (ImageData IS NOT NULL) AS HasImage FROM Products ORDER BY ProductName";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new CatalogProductDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.GetInt32(3),
                reader.GetInt64(5) == 1,
                reader.IsDBNull(4) ? null : reader.GetDecimal(4)));
        }
        return result;
    }

    public static (byte[] Data, string ContentType)? GetImage(string barcode)
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ImageData, ImageContentType FROM Products WHERE Barcode = @B";
        cmd.Parameters.AddWithValue("@B", barcode);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(0)) return null;
        return ((byte[])reader[0], reader.GetString(1));
    }

    // ---------- المزامنة من برنامج المحل (اسم/سعر/كمية بس - upsert + حذف الناقص) ----------

    public static void SyncProducts(List<SyncProductDto> products)
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var transaction = conn.BeginTransaction();
        try
        {
            var incomingBarcodes = new List<string>();

            foreach (var p in products)
            {
                incomingBarcodes.Add(p.Barcode);
                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO Products (Barcode, ProductName, SalePrice, Quantity) VALUES (@B, @N, @P, @Q)
                    ON CONFLICT(Barcode) DO UPDATE SET ProductName = @N, SalePrice = @P, Quantity = @Q";
                cmd.Parameters.AddWithValue("@B", p.Barcode);
                cmd.Parameters.AddWithValue("@N", p.ProductName);
                cmd.Parameters.AddWithValue("@P", p.SalePrice);
                cmd.Parameters.AddWithValue("@Q", p.Quantity);
                cmd.ExecuteNonQuery();
            }

            // أي منتج موجود في الموقع بس مابقاش موجود في آخر مزامنة (اتحذف من برنامج المحل) - يتحذف من هنا كمان
            using (var cmdDelete = conn.CreateCommand())
            {
                cmdDelete.Transaction = transaction;
                if (incomingBarcodes.Count == 0)
                {
                    cmdDelete.CommandText = "DELETE FROM Products";
                }
                else
                {
                    var paramNames = incomingBarcodes.Select((_, i) => $"@p{i}").ToArray();
                    cmdDelete.CommandText = $"DELETE FROM Products WHERE Barcode NOT IN ({string.Join(",", paramNames)})";
                    for (int i = 0; i < incomingBarcodes.Count; i++)
                        cmdDelete.Parameters.AddWithValue(paramNames[i], incomingBarcodes[i]);
                }
                cmdDelete.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // ---------- إدارة الموقع (صورة + سعر قبل الخصم - محفوظين هنا بس) ----------

    public static bool SetImage(string barcode, byte[] data, string contentType)
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Products SET ImageData = @D, ImageContentType = @T WHERE Barcode = @B";
        cmd.Parameters.AddWithValue("@D", data);
        cmd.Parameters.AddWithValue("@T", contentType);
        cmd.Parameters.AddWithValue("@B", barcode);
        return cmd.ExecuteNonQuery() > 0;
    }

    public static bool SetOriginalPrice(string barcode, decimal? originalPrice)
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Products SET OriginalPrice = @P WHERE Barcode = @B";
        cmd.Parameters.AddWithValue("@P", (object?)originalPrice ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@B", barcode);
        return cmd.ExecuteNonQuery() > 0;
    }
}
