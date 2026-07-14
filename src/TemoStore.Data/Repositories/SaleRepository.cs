using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;
using TemoStore.Core.Entities;

namespace TemoStore.Data.Repositories
{
    public class SaleRepository : ISaleRepository
    {
        private readonly SqliteConnection _conn;
        private readonly SqliteTransaction _tx;

        public SaleRepository(SqliteConnection conn, SqliteTransaction tx)
        {
            _conn = conn;
            _tx = tx;
        }

        public int Insert(SaleRecord sale)
        {
            using (var cmd = new SqliteCommand(
                "INSERT INTO Sales (Barcode, ProductName, CostPrice, Price, QuantitySold, Total, CustomerId, PaymentType, IMEI, PaymentMethod) VALUES (@Barcode, @ProductName, @CostPrice, @Price, @QuantitySold, @Total, @CustomerId, @PaymentType, @IMEI, @PaymentMethod)", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@Barcode", sale.Barcode);
                cmd.Parameters.AddWithValue("@ProductName", sale.ProductName);
                cmd.Parameters.AddWithValue("@CostPrice", sale.CostPrice);
                cmd.Parameters.AddWithValue("@Price", sale.Price);
                cmd.Parameters.AddWithValue("@QuantitySold", sale.QuantitySold);
                cmd.Parameters.AddWithValue("@Total", sale.Total);
                cmd.Parameters.AddWithValue("@CustomerId", (object?)sale.CustomerId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PaymentType", sale.PaymentType);
                cmd.Parameters.AddWithValue("@IMEI", (object?)sale.Imei ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PaymentMethod", (object?)sale.PaymentMethod ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
            using var cmdId = new SqliteCommand("SELECT last_insert_rowid();", _conn, _tx);
            return Convert.ToInt32(cmdId.ExecuteScalar());
        }

        public SaleRecord? GetById(int saleId)
        {
            using var cmd = new SqliteCommand("SELECT Barcode, ProductName, CostPrice, Price, QuantitySold, Total, CustomerId, PaymentType, PaymentMethod, SaleDate, IMEI FROM Sales WHERE SaleID = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", saleId);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return new SaleRecord
            {
                SaleId = saleId,
                Barcode = reader["Barcode"].ToString()!,
                ProductName = reader["ProductName"].ToString()!,
                CostPrice = Convert.ToDecimal(reader["CostPrice"]),
                Price = Convert.ToDecimal(reader["Price"]),
                QuantitySold = Convert.ToInt32(reader["QuantitySold"]),
                Total = Convert.ToDecimal(reader["Total"]),
                CustomerId = reader["CustomerId"] == DBNull.Value ? null : Convert.ToInt32(reader["CustomerId"]),
                PaymentType = reader["PaymentType"] == DBNull.Value ? "" : reader["PaymentType"].ToString()!,
                PaymentMethod = reader["PaymentMethod"] == DBNull.Value ? null : reader["PaymentMethod"].ToString(),
                SaleDate = reader["SaleDate"].ToString()!,
                Imei = reader["IMEI"] == DBNull.Value ? null : reader["IMEI"].ToString()
            };
        }

        public void UpdateQuantityAndTotal(int saleId, int qty, decimal total)
        {
            using var cmd = new SqliteCommand("UPDATE Sales SET QuantitySold = @Qty, Total = @Total WHERE SaleID = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@Qty", qty);
            cmd.Parameters.AddWithValue("@Total", total);
            cmd.Parameters.AddWithValue("@Id", saleId);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int saleId)
        {
            using var cmd = new SqliteCommand("DELETE FROM Sales WHERE SaleID = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", saleId);
            cmd.ExecuteNonQuery();
        }

        public int GetDailyInvoiceNumber(int saleId, string saleDate)
        {
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM Sales WHERE date(SaleDate) = date(@SaleDate) AND SaleID <= @SaleId", _conn, _tx);
            cmd.Parameters.AddWithValue("@SaleDate", saleDate);
            cmd.Parameters.AddWithValue("@SaleId", saleId);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }
}
