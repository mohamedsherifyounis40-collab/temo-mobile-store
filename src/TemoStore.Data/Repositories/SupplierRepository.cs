using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;
using TemoStore.Core.Entities;

namespace TemoStore.Data.Repositories
{
    public class SupplierRepository : ISupplierRepository
    {
        private readonly SqliteConnection _conn;
        private readonly SqliteTransaction _tx;

        public SupplierRepository(SqliteConnection conn, SqliteTransaction tx)
        {
            _conn = conn;
            _tx = tx;
        }

        public bool Exists(int supplierId)
        {
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM Suppliers WHERE SupplierId = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", supplierId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public decimal GetBalance(int supplierId)
        {
            decimal totalPurchases = 0, totalPaid = 0;
            using (var cmd = new SqliteCommand("SELECT SUM(TotalAmount) FROM Purchases WHERE SupplierId = @Id", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@Id", supplierId);
                var res = cmd.ExecuteScalar();
                totalPurchases = res != null && res != DBNull.Value ? Convert.ToDecimal(res) : 0;
            }
            using (var cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE SupplierId = @Id AND MovementType = 'صرف'", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@Id", supplierId);
                var res = cmd.ExecuteScalar();
                totalPaid = res != null && res != DBNull.Value ? Convert.ToDecimal(res) : 0;
            }
            return totalPurchases - totalPaid;
        }

        public IReadOnlyList<SupplierStatementLine> GetStatement(int supplierId)
        {
            var list = new List<SupplierStatementLine>();
            using (var cmd = new SqliteCommand("SELECT PurchaseId, PurchaseDate, TotalAmount FROM Purchases WHERE SupplierId = @Id ORDER BY PurchaseDate", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@Id", supplierId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    list.Add(new SupplierStatementLine { Date = reader["PurchaseDate"].ToString()!, Type = "فاتورة شراء", Details = "فاتورة رقم " + reader["PurchaseId"], Amount = Convert.ToDecimal(reader["TotalAmount"]) });
            }
            using (var cmd = new SqliteCommand("SELECT CreatedAt, Amount, PaymentMethod FROM CashMovements WHERE SupplierId = @Id AND MovementType = 'صرف' ORDER BY CreatedAt", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@Id", supplierId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    list.Add(new SupplierStatementLine { Date = reader["CreatedAt"].ToString()!, Type = "سداد", Details = "سداد عبر " + reader["PaymentMethod"], Amount = -Convert.ToDecimal(reader["Amount"]) });
            }
            return list;
        }
    }
}
