using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;
using TemoStore.Core.Entities;

namespace TemoStore.Data.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly SqliteConnection _conn;
        private readonly SqliteTransaction _tx;

        public ProductRepository(SqliteConnection conn, SqliteTransaction tx)
        {
            _conn = conn;
            _tx = tx;
        }

        public ProductRecord? GetByBarcode(string barcode)
        {
            using var cmd = new SqliteCommand("SELECT Barcode, ProductName, Price, SalePrice, Quantity, IsSerialized FROM Products WHERE Barcode = @B", _conn, _tx);
            cmd.Parameters.AddWithValue("@B", barcode);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return new ProductRecord
            {
                Barcode = reader["Barcode"].ToString()!,
                ProductName = reader["ProductName"].ToString()!,
                Price = Convert.ToDecimal(reader["Price"]),
                SalePrice = Convert.ToDecimal(reader["SalePrice"]),
                Quantity = Convert.ToInt32(reader["Quantity"]),
                IsSerialized = reader["IsSerialized"] != DBNull.Value && Convert.ToInt32(reader["IsSerialized"]) == 1
            };
        }

        public void DeductQuantity(string barcode, int qty)
        {
            using var cmd = new SqliteCommand("UPDATE Products SET Quantity = Quantity - @Q WHERE Barcode = @B", _conn, _tx);
            cmd.Parameters.AddWithValue("@Q", qty);
            cmd.Parameters.AddWithValue("@B", barcode);
            cmd.ExecuteNonQuery();
        }

        public void AddQuantity(string barcode, int qty)
        {
            using var cmd = new SqliteCommand("UPDATE Products SET Quantity = Quantity + @Q WHERE Barcode = @B", _conn, _tx);
            cmd.Parameters.AddWithValue("@Q", qty);
            cmd.Parameters.AddWithValue("@B", barcode);
            cmd.ExecuteNonQuery();
        }

        public void UpsertOnPurchase(string barcode, string productName, decimal unitCost, decimal salePrice, int qty, bool isSerialized)
        {
            bool productExists = false;
            if (!string.IsNullOrEmpty(barcode))
            {
                using var cmdCheck = new SqliteCommand("SELECT COUNT(*) FROM Products WHERE Barcode = @B", _conn, _tx);
                cmdCheck.Parameters.AddWithValue("@B", barcode);
                productExists = Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0;
            }

            if (productExists)
            {
                using var cmd = new SqliteCommand(
                    "UPDATE Products SET Quantity = Quantity + @Q, Price = @U, IsSerialized = CASE WHEN @IsSerialized = 1 THEN 1 ELSE IsSerialized END WHERE Barcode = @B", _conn, _tx);
                cmd.Parameters.AddWithValue("@Q", qty);
                cmd.Parameters.AddWithValue("@U", unitCost);
                cmd.Parameters.AddWithValue("@IsSerialized", isSerialized ? 1 : 0);
                cmd.Parameters.AddWithValue("@B", barcode);
                cmd.ExecuteNonQuery();
            }
            else
            {
                string finalBarcode = string.IsNullOrEmpty(barcode) ? ("NEW-" + DateTime.Now.Ticks) : barcode;
                using var cmd = new SqliteCommand(
                    "INSERT INTO Products (Barcode, ProductName, Price, SalePrice, Quantity, IsSerialized) VALUES (@B, @N, @U, @S, @Q, @IsSerialized)", _conn, _tx);
                cmd.Parameters.AddWithValue("@B", finalBarcode);
                cmd.Parameters.AddWithValue("@N", productName);
                cmd.Parameters.AddWithValue("@U", unitCost);
                cmd.Parameters.AddWithValue("@S", salePrice);
                cmd.Parameters.AddWithValue("@Q", qty);
                cmd.Parameters.AddWithValue("@IsSerialized", isSerialized ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
        }

        public void InsertProductUnit(string barcode, string imei, int purchaseId)
        {
            using var cmd = new SqliteCommand(
                "INSERT INTO ProductUnits (Barcode, IMEI, Status, PurchaseId, CreatedAt) VALUES (@B, @IMEI, 'InStock', @P, @C)", _conn, _tx);
            cmd.Parameters.AddWithValue("@B", barcode);
            cmd.Parameters.AddWithValue("@IMEI", imei);
            cmd.Parameters.AddWithValue("@P", purchaseId);
            cmd.Parameters.AddWithValue("@C", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();
        }

        public void MarkImeiSold(string imei, int saleId)
        {
            using var cmd = new SqliteCommand("UPDATE ProductUnits SET Status = 'Sold', SaleId = @SaleId WHERE IMEI = @IMEI", _conn, _tx);
            cmd.Parameters.AddWithValue("@SaleId", saleId);
            cmd.Parameters.AddWithValue("@IMEI", imei);
            cmd.ExecuteNonQuery();
        }

        public void MarkImeiInStock(string imei)
        {
            using var cmd = new SqliteCommand("UPDATE ProductUnits SET Status = 'InStock', SaleId = NULL WHERE IMEI = @IMEI", _conn, _tx);
            cmd.Parameters.AddWithValue("@IMEI", imei);
            cmd.ExecuteNonQuery();
        }

        public bool ImeiExists(string imei)
        {
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM ProductUnits WHERE IMEI = @IMEI", _conn, _tx);
            cmd.Parameters.AddWithValue("@IMEI", imei);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public string GetImeiStatus(string imei)
        {
            using var cmd = new SqliteCommand("SELECT Status FROM ProductUnits WHERE IMEI = @IMEI", _conn, _tx);
            cmd.Parameters.AddWithValue("@IMEI", imei);
            var res = cmd.ExecuteScalar();
            return res?.ToString() ?? "";
        }

        public void DeleteProductUnitsForPurchase(int purchaseId)
        {
            using var cmd = new SqliteCommand("DELETE FROM ProductUnits WHERE PurchaseId = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", purchaseId);
            cmd.ExecuteNonQuery();
        }

        public IReadOnlyList<ProductRecord> GetBelowThreshold(int threshold)
        {
            var list = new List<ProductRecord>();
            using var cmd = new SqliteCommand("SELECT Barcode, ProductName, Price, SalePrice, Quantity, IsSerialized FROM Products WHERE Quantity <= @T", _conn, _tx);
            cmd.Parameters.AddWithValue("@T", threshold);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ProductRecord
                {
                    Barcode = reader["Barcode"].ToString()!,
                    ProductName = reader["ProductName"].ToString()!,
                    Price = Convert.ToDecimal(reader["Price"]),
                    SalePrice = Convert.ToDecimal(reader["SalePrice"]),
                    Quantity = Convert.ToInt32(reader["Quantity"]),
                    IsSerialized = reader["IsSerialized"] != DBNull.Value && Convert.ToInt32(reader["IsSerialized"]) == 1
                });
            }
            return list;
        }
    }
}
