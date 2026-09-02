using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;
using TemoStore.Core.Entities;

namespace TemoStore.Data.Repositories
{
    public class ReturnRepository : IReturnRepository
    {
        private readonly SqliteConnection _conn;
        private readonly SqliteTransaction _tx;

        public ReturnRepository(SqliteConnection conn, SqliteTransaction tx)
        {
            _conn = conn;
            _tx = tx;
        }

        public int Insert(ReturnRecord ret)
        {
            using (var cmd = new SqliteCommand(
                "INSERT INTO SalesReturns (SaleId, Barcode, ProductName, Quantity, RefundAmount, Reason, PaymentType, PaymentMethod, CustomerId, PerformedBy) " +
                "VALUES (@SaleId, @Barcode, @ProductName, @Quantity, @RefundAmount, @Reason, @PaymentType, @PaymentMethod, @CustomerId, @PerformedBy)", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@SaleId", ret.SaleId);
                cmd.Parameters.AddWithValue("@Barcode", ret.Barcode);
                cmd.Parameters.AddWithValue("@ProductName", ret.ProductName);
                cmd.Parameters.AddWithValue("@Quantity", ret.Quantity);
                cmd.Parameters.AddWithValue("@RefundAmount", ret.RefundAmount);
                cmd.Parameters.AddWithValue("@Reason", ret.Reason);
                cmd.Parameters.AddWithValue("@PaymentType", ret.PaymentType);
                cmd.Parameters.AddWithValue("@PaymentMethod", (object?)ret.PaymentMethod ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CustomerId", (object?)ret.CustomerId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PerformedBy", ret.PerformedBy);
                cmd.ExecuteNonQuery();
            }
            using var cmdId = new SqliteCommand("SELECT last_insert_rowid();", _conn, _tx);
            return Convert.ToInt32(cmdId.ExecuteScalar());
        }

        public int GetReturnedQuantity(int saleId)
        {
            using var cmd = new SqliteCommand("SELECT COALESCE(SUM(Quantity), 0) FROM SalesReturns WHERE SaleId = @SaleId", _conn, _tx);
            cmd.Parameters.AddWithValue("@SaleId", saleId);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public IReadOnlyList<ReturnRecord> GetBySaleId(int saleId)
        {
            var results = new List<ReturnRecord>();
            using var cmd = new SqliteCommand(
                "SELECT ReturnId, SaleId, Barcode, ProductName, Quantity, RefundAmount, Reason, PaymentType, PaymentMethod, CustomerId, ReturnDate, PerformedBy " +
                "FROM SalesReturns WHERE SaleId = @SaleId ORDER BY ReturnId ASC", _conn, _tx);
            cmd.Parameters.AddWithValue("@SaleId", saleId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                results.Add(ReadReturn(reader));
            return results;
        }

        public IReadOnlyList<ReturnRecord> GetAll()
        {
            var results = new List<ReturnRecord>();
            using var cmd = new SqliteCommand(
                "SELECT ReturnId, SaleId, Barcode, ProductName, Quantity, RefundAmount, Reason, PaymentType, PaymentMethod, CustomerId, ReturnDate, PerformedBy " +
                "FROM SalesReturns ORDER BY ReturnId DESC", _conn, _tx);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                results.Add(ReadReturn(reader));
            return results;
        }

        private static ReturnRecord ReadReturn(SqliteDataReader reader) => new ReturnRecord
        {
            ReturnId = Convert.ToInt32(reader["ReturnId"]),
            SaleId = Convert.ToInt32(reader["SaleId"]),
            Barcode = reader["Barcode"] == DBNull.Value ? "" : reader["Barcode"].ToString()!,
            ProductName = reader["ProductName"] == DBNull.Value ? "" : reader["ProductName"].ToString()!,
            Quantity = Convert.ToInt32(reader["Quantity"]),
            RefundAmount = Convert.ToDecimal(reader["RefundAmount"]),
            Reason = reader["Reason"] == DBNull.Value ? "" : reader["Reason"].ToString()!,
            PaymentType = reader["PaymentType"] == DBNull.Value ? "" : reader["PaymentType"].ToString()!,
            PaymentMethod = reader["PaymentMethod"] == DBNull.Value ? null : reader["PaymentMethod"].ToString(),
            CustomerId = reader["CustomerId"] == DBNull.Value ? null : Convert.ToInt32(reader["CustomerId"]),
            ReturnDate = reader["ReturnDate"].ToString()!,
            PerformedBy = reader["PerformedBy"] == DBNull.Value ? "" : reader["PerformedBy"].ToString()!
        };
    }
}
