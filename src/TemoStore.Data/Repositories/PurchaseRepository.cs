using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;
using TemoStore.Core.Entities;

namespace TemoStore.Data.Repositories
{
    public class PurchaseRepository : IPurchaseRepository
    {
        private readonly SqliteConnection _conn;
        private readonly SqliteTransaction _tx;

        public PurchaseRepository(SqliteConnection conn, SqliteTransaction tx)
        {
            _conn = conn;
            _tx = tx;
        }

        public int InsertPurchase(int supplierId, decimal totalAmount)
        {
            using (var cmd = new SqliteCommand("INSERT INTO Purchases (SupplierId, PurchaseDate, TotalAmount, Notes) VALUES (@S, @D, @T, @N)", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@S", supplierId);
                cmd.Parameters.AddWithValue("@D", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@T", totalAmount);
                cmd.Parameters.AddWithValue("@N", DBNull.Value);
                cmd.ExecuteNonQuery();
            }
            using var cmdId = new SqliteCommand("SELECT last_insert_rowid();", _conn, _tx);
            return Convert.ToInt32(cmdId.ExecuteScalar());
        }

        public void UpdatePurchase(int purchaseId, int supplierId, decimal totalAmount)
        {
            using var cmd = new SqliteCommand("UPDATE Purchases SET SupplierId = @S, TotalAmount = @T WHERE PurchaseId = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@S", supplierId);
            cmd.Parameters.AddWithValue("@T", totalAmount);
            cmd.Parameters.AddWithValue("@Id", purchaseId);
            cmd.ExecuteNonQuery();
        }

        public void InsertItem(int purchaseId, PurchaseLine line)
        {
            using var cmd = new SqliteCommand(
                "INSERT INTO PurchaseItems (PurchaseId, Barcode, ProductName, Quantity, UnitCost, LineTotal) VALUES (@P, @B, @N, @Q, @U, @L)", _conn, _tx);
            cmd.Parameters.AddWithValue("@P", purchaseId);
            cmd.Parameters.AddWithValue("@B", string.IsNullOrEmpty(line.Barcode) ? (object)DBNull.Value : line.Barcode);
            cmd.Parameters.AddWithValue("@N", line.ProductName);
            cmd.Parameters.AddWithValue("@Q", line.Qty);
            cmd.Parameters.AddWithValue("@U", line.UnitCost);
            cmd.Parameters.AddWithValue("@L", line.LineTotal);
            cmd.ExecuteNonQuery();
        }

        public void DeleteItems(int purchaseId)
        {
            using var cmd = new SqliteCommand("DELETE FROM PurchaseItems WHERE PurchaseId = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", purchaseId);
            cmd.ExecuteNonQuery();
        }

        public void DeletePurchase(int purchaseId)
        {
            using var cmd = new SqliteCommand("DELETE FROM Purchases WHERE PurchaseId = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", purchaseId);
            cmd.ExecuteNonQuery();
        }

        public PurchaseRecord? GetById(int purchaseId)
        {
            PurchaseRecord? record = null;
            using (var cmd = new SqliteCommand("SELECT SupplierId, PurchaseDate, TotalAmount FROM Purchases WHERE PurchaseId = @Id", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@Id", purchaseId);
                using var reader = cmd.ExecuteReader();
                if (!reader.Read()) return null;
                record = new PurchaseRecord
                {
                    PurchaseId = purchaseId,
                    SupplierId = reader["SupplierId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SupplierId"]),
                    PurchaseDate = reader["PurchaseDate"].ToString()!,
                    TotalAmount = Convert.ToDecimal(reader["TotalAmount"])
                };
            }

            using (var cmd = new SqliteCommand("SELECT Barcode, ProductName, Quantity, UnitCost, LineTotal FROM PurchaseItems WHERE PurchaseId = @Id", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@Id", purchaseId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    record.Lines.Add(new PurchaseLine
                    {
                        Barcode = reader["Barcode"] == DBNull.Value ? null : reader["Barcode"].ToString(),
                        ProductName = reader["ProductName"].ToString()!,
                        Qty = Convert.ToInt32(reader["Quantity"]),
                        UnitCost = Convert.ToDecimal(reader["UnitCost"]),
                        LineTotal = Convert.ToDecimal(reader["LineTotal"])
                    });
                }
            }

            foreach (var line in record.Lines)
            {
                if (string.IsNullOrEmpty(line.Barcode)) continue;
                using var cmd = new SqliteCommand("SELECT IMEI FROM ProductUnits WHERE PurchaseId = @Id AND Barcode = @B", _conn, _tx);
                cmd.Parameters.AddWithValue("@Id", purchaseId);
                cmd.Parameters.AddWithValue("@B", line.Barcode);
                using var reader = cmd.ExecuteReader();
                var imeis = new List<string>();
                while (reader.Read()) imeis.Add(reader["IMEI"].ToString()!);
                if (imeis.Count > 0)
                {
                    line.IsSerialized = true;
                    line.Imeis = imeis;
                }
            }

            return record;
        }
    }
}
