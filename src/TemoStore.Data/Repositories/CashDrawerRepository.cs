using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;
using TemoStore.Core.Entities;

namespace TemoStore.Data.Repositories
{
    public class CashDrawerRepository : ICashDrawerRepository
    {
        private readonly SqliteConnection _conn;
        private readonly SqliteTransaction _tx;

        public CashDrawerRepository(SqliteConnection conn, SqliteTransaction tx)
        {
            _conn = conn;
            _tx = tx;
        }

        public decimal GetBalance(string method)
        {
            using var cmd = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", _conn, _tx);
            cmd.Parameters.AddWithValue("@Method", method);
            var res = cmd.ExecuteScalar();
            return res != null && res != DBNull.Value ? Convert.ToDecimal(res) : 0;
        }

        public IReadOnlyDictionary<string, decimal> GetAllBalances()
        {
            var dict = new Dictionary<string, decimal>();
            using var cmd = new SqliteCommand("SELECT PaymentMethod, CurrentBalance FROM PaymentMethodBalances", _conn, _tx);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                dict[reader["PaymentMethod"].ToString()!] = Convert.ToDecimal(reader["CurrentBalance"]);
            return dict;
        }

        public void SetBalance(string method, decimal newBalance)
        {
            using var cmd = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = @Balance WHERE PaymentMethod = @Method", _conn, _tx);
            cmd.Parameters.AddWithValue("@Balance", newBalance);
            cmd.Parameters.AddWithValue("@Method", method);
            int rows = cmd.ExecuteNonQuery();
            if (rows == 0)
            {
                using var cmdInsert = new SqliteCommand("INSERT INTO PaymentMethodBalances (PaymentMethod, CurrentBalance) VALUES (@Method, @Balance)", _conn, _tx);
                cmdInsert.Parameters.AddWithValue("@Method", method);
                cmdInsert.Parameters.AddWithValue("@Balance", newBalance);
                cmdInsert.ExecuteNonQuery();
            }
        }

        public int InsertMovement(CashMovementRecord movement)
        {
            using (var cmd = new SqliteCommand(
                "INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode, CustomerId, SupplierId, PurchaseId, SaleId, LinkedMovementId) " +
                "VALUES (@Date, @Type, @Method, @Amount, @Ref, @Desc, @CreatedAt, @AccountCode, @CustomerId, @SupplierId, @PurchaseId, @SaleId, @LinkedId)", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@Type", movement.MovementType);
                cmd.Parameters.AddWithValue("@Method", movement.PaymentMethod);
                cmd.Parameters.AddWithValue("@Amount", movement.Amount);
                cmd.Parameters.AddWithValue("@Ref", (object?)movement.ReferenceNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Desc", (object?)movement.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@AccountCode", (object?)movement.AccountCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CustomerId", (object?)movement.CustomerId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SupplierId", (object?)movement.SupplierId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PurchaseId", (object?)movement.PurchaseId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SaleId", (object?)movement.SaleId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@LinkedId", (object?)movement.LinkedMovementId ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
            using var cmdId = new SqliteCommand("SELECT last_insert_rowid();", _conn, _tx);
            return Convert.ToInt32(cmdId.ExecuteScalar());
        }

        public CashMovementRecord? GetMovementById(int id)
        {
            using var cmd = new SqliteCommand(
                "SELECT MovementType, PaymentMethod, Amount, ReferenceNumber, Description, AccountCode, MovementDate, LinkedMovementId, PurchaseId, SaleId, CustomerId, SupplierId FROM CashMovements WHERE Id = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", id);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return new CashMovementRecord
            {
                Id = id,
                MovementType = reader["MovementType"].ToString()!,
                PaymentMethod = reader["PaymentMethod"].ToString()!,
                Amount = Convert.ToDecimal(reader["Amount"]),
                ReferenceNumber = reader["ReferenceNumber"] == DBNull.Value ? null : reader["ReferenceNumber"].ToString(),
                Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                AccountCode = reader["AccountCode"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AccountCode"]),
                MovementDate = reader["MovementDate"].ToString()!,
                LinkedMovementId = reader["LinkedMovementId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["LinkedMovementId"]),
                PurchaseId = reader["PurchaseId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["PurchaseId"]),
                SaleId = reader["SaleId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["SaleId"]),
                CustomerId = reader["CustomerId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["CustomerId"]),
                SupplierId = reader["SupplierId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["SupplierId"])
            };
        }

        public CashMovementRecord? FindByPurchaseId(int purchaseId)
        {
            using var cmd = new SqliteCommand("SELECT Id FROM CashMovements WHERE PurchaseId = @Id AND MovementType = 'صرف'", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", purchaseId);
            var res = cmd.ExecuteScalar();
            return res == null ? null : GetMovementById(Convert.ToInt32(res));
        }

        public void DeleteMovement(int id)
        {
            using var cmd = new SqliteCommand("DELETE FROM CashMovements WHERE Id = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

        public void LinkMovements(int firstId, int secondId)
        {
            using var cmd = new SqliteCommand("UPDATE CashMovements SET LinkedMovementId = @Second WHERE Id = @First", _conn, _tx);
            cmd.Parameters.AddWithValue("@Second", secondId);
            cmd.Parameters.AddWithValue("@First", firstId);
            cmd.ExecuteNonQuery();
        }
    }
}
