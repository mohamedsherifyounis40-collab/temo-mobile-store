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
                "INSERT INTO Sales (Barcode, ProductName, CostPrice, Price, QuantitySold, Total, Discount, Tax, AmountPaid, CustomerId, PaymentType, IMEI, PaymentMethod, SalesInvoiceId, Notes) VALUES (@Barcode, @ProductName, @CostPrice, @Price, @QuantitySold, @Total, @Discount, @Tax, @AmountPaid, @CustomerId, @PaymentType, @IMEI, @PaymentMethod, @SalesInvoiceId, @Notes)", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@Barcode", sale.Barcode);
                cmd.Parameters.AddWithValue("@ProductName", sale.ProductName);
                cmd.Parameters.AddWithValue("@CostPrice", sale.CostPrice);
                cmd.Parameters.AddWithValue("@Price", sale.Price);
                cmd.Parameters.AddWithValue("@QuantitySold", sale.QuantitySold);
                cmd.Parameters.AddWithValue("@Total", sale.Total);
                cmd.Parameters.AddWithValue("@Discount", sale.Discount);
                cmd.Parameters.AddWithValue("@Tax", sale.Tax);
                cmd.Parameters.AddWithValue("@AmountPaid", sale.AmountPaid);
                cmd.Parameters.AddWithValue("@CustomerId", (object?)sale.CustomerId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PaymentType", sale.PaymentType);
                cmd.Parameters.AddWithValue("@IMEI", (object?)sale.Imei ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PaymentMethod", (object?)sale.PaymentMethod ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SalesInvoiceId", sale.SalesInvoiceId == 0 ? (object)DBNull.Value : sale.SalesInvoiceId);
                cmd.Parameters.AddWithValue("@Notes", sale.Notes);
                cmd.ExecuteNonQuery();
            }
            using var cmdId = new SqliteCommand("SELECT last_insert_rowid();", _conn, _tx);
            return Convert.ToInt32(cmdId.ExecuteScalar());
        }

        public void SetInvoiceId(int saleId, int invoiceId)
        {
            using var cmd = new SqliteCommand("UPDATE Sales SET SalesInvoiceId = @InvoiceId WHERE SaleID = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@InvoiceId", invoiceId);
            cmd.Parameters.AddWithValue("@Id", saleId);
            cmd.ExecuteNonQuery();
        }

        // بيتسجل على أول صنف بس في الفاتورة (زي SalesInvoiceId) - المبلغ ده على مستوى
        // الفاتورة كلها، معرفتش قيمته الفعلية إلا بعد ما كل أسطر الفاتورة تتضاف وتتجمع
        public void SetAmountPaid(int invoiceId, decimal amountPaid)
        {
            using var cmd = new SqliteCommand("UPDATE Sales SET AmountPaid = @AmountPaid WHERE SaleID = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@AmountPaid", amountPaid);
            cmd.Parameters.AddWithValue("@Id", invoiceId);
            cmd.ExecuteNonQuery();
        }

        public IReadOnlyList<SaleRecord> GetByInvoiceId(int invoiceId)
        {
            var results = new List<SaleRecord>();
            using var cmd = new SqliteCommand("SELECT SaleID, Barcode, ProductName, CostPrice, Price, QuantitySold, Total, Discount, Tax, AmountPaid, CustomerId, PaymentType, PaymentMethod, SaleDate, IMEI, SalesInvoiceId, Notes FROM Sales WHERE SalesInvoiceId = @InvoiceId ORDER BY SaleID ASC", _conn, _tx);
            cmd.Parameters.AddWithValue("@InvoiceId", invoiceId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new SaleRecord
                {
                    SaleId = Convert.ToInt32(reader["SaleID"]),
                    Barcode = reader["Barcode"].ToString()!,
                    ProductName = reader["ProductName"].ToString()!,
                    CostPrice = Convert.ToDecimal(reader["CostPrice"]),
                    Price = Convert.ToDecimal(reader["Price"]),
                    QuantitySold = Convert.ToInt32(reader["QuantitySold"]),
                    Total = Convert.ToDecimal(reader["Total"]),
                    Discount = reader["Discount"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Discount"]),
                    Tax = reader["Tax"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Tax"]),
                    AmountPaid = reader["AmountPaid"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["AmountPaid"]),
                    CustomerId = reader["CustomerId"] == DBNull.Value ? null : Convert.ToInt32(reader["CustomerId"]),
                    PaymentType = reader["PaymentType"] == DBNull.Value ? "" : reader["PaymentType"].ToString()!,
                    PaymentMethod = reader["PaymentMethod"] == DBNull.Value ? null : reader["PaymentMethod"].ToString(),
                    SaleDate = reader["SaleDate"].ToString()!,
                    Imei = reader["IMEI"] == DBNull.Value ? null : reader["IMEI"].ToString(),
                    SalesInvoiceId = reader["SalesInvoiceId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SalesInvoiceId"]),
                    Notes = reader["Notes"] == DBNull.Value ? "" : reader["Notes"].ToString()!
                });
            }
            return results;
        }

        public SaleRecord? GetById(int saleId)
        {
            using var cmd = new SqliteCommand("SELECT Barcode, ProductName, CostPrice, Price, QuantitySold, Total, Discount, Tax, AmountPaid, CustomerId, PaymentType, PaymentMethod, SaleDate, IMEI, SalesInvoiceId, Notes FROM Sales WHERE SaleID = @Id", _conn, _tx);
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
                Discount = reader["Discount"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Discount"]),
                Tax = reader["Tax"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Tax"]),
                AmountPaid = reader["AmountPaid"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["AmountPaid"]),
                CustomerId = reader["CustomerId"] == DBNull.Value ? null : Convert.ToInt32(reader["CustomerId"]),
                PaymentType = reader["PaymentType"] == DBNull.Value ? "" : reader["PaymentType"].ToString()!,
                PaymentMethod = reader["PaymentMethod"] == DBNull.Value ? null : reader["PaymentMethod"].ToString(),
                SaleDate = reader["SaleDate"].ToString()!,
                Imei = reader["IMEI"] == DBNull.Value ? null : reader["IMEI"].ToString(),
                SalesInvoiceId = reader["SalesInvoiceId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SalesInvoiceId"]),
                Notes = reader["Notes"] == DBNull.Value ? "" : reader["Notes"].ToString()!
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

        public int GetDailyInvoiceNumber(int invoiceId, string saleDate)
        {
            using var cmd = new SqliteCommand("SELECT COUNT(DISTINCT SalesInvoiceId) FROM Sales WHERE date(SaleDate) = date(@SaleDate) AND SalesInvoiceId <= @InvoiceId", _conn, _tx);
            cmd.Parameters.AddWithValue("@SaleDate", saleDate);
            cmd.Parameters.AddWithValue("@InvoiceId", invoiceId);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }
}
