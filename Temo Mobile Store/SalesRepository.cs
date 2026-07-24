using System;
using System.Data;
using Microsoft.Data.Sqlite;
using TemoStore.Core.Exceptions;

namespace Temo_Mobile_Store
{
    public class ProductForSale
    {
        public string ProductName;
        public decimal SalePrice;
        public int Quantity;
        public bool IsSerialized;
    }

    // بيانات كاملة لآخر عملية بيع - مستخدمة في طباعة إيصال العميل (كل حقل مربوط ببيانات حقيقية)
    public class ReceiptData
    {
        public int SaleId;
        public int DailyInvoiceNumber;
        public string Barcode;
        public string ProductName;
        public decimal UnitPrice;
        public int Quantity;
        public decimal Total;
        public string PaymentType;   // Cash / Credit
        public string PaymentMethod; // نقدي/فوري/... (لو كاش)
        public string SaleDate;
        public string IMEI;
        public string CustomerName;
        public string CustomerPhone;
        public string CashierName;
    }

    public class SaleRecord
    {
        public int SaleId;
        public string Barcode;
        public string ProductName;
        public decimal Price;
        public int QuantitySold;
        public decimal Total;
        public string PaymentType;
        public string PaymentMethod;
        public string SaleDate;
        public string IMEI;
    }

    // ==========================================================================
    // SalesRepository: كل الوصول لقاعدة البيانات الخاص بشاشة المبيعات (نقطة البيع)
    // في مكان واحد - فحص مخزون، تسجيل/تعديل/إلغاء بيع، وحدات IMEI، وترقيم الفواتير.
    // ==========================================================================
    public static class SalesRepository
    {
        public static DataTable GetCustomers()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("CustomerId", typeof(int)), new DataColumn("CustomerName") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT CustomerId, CustomerName FROM Customers ORDER BY CustomerName", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        dt.Rows.Add(Convert.ToInt32(reader["CustomerId"]), reader["CustomerName"].ToString());
                }
            }
            return dt;
        }

        public static DataTable GetSales()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("رقم البيع"), new DataColumn("المنتج"), new DataColumn("الكمية المباعة"), new DataColumn("الإجمالي"), new DataColumn("التاريخ والوقت ⏰") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT SaleID, ProductName, QuantitySold, Total, SaleDate FROM Sales ORDER BY SaleID ASC", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        dt.Rows.Add(reader["SaleID"], reader["ProductName"], reader["QuantitySold"], reader["Total"], reader["SaleDate"]);
                }
            }
            return dt;
        }

        public static ProductForSale GetProductForSale(string barcode)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT ProductName, SalePrice, Quantity, IsSerialized FROM Products WHERE Barcode = @Barcode", conn))
                {
                    cmd.Parameters.AddWithValue("@Barcode", barcode);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return null;
                        return new ProductForSale
                        {
                            ProductName = reader["ProductName"].ToString(),
                            SalePrice = Convert.ToDecimal(reader["SalePrice"]),
                            Quantity = Convert.ToInt32(reader["Quantity"]),
                            IsSerialized = reader["IsSerialized"] != DBNull.Value && Convert.ToInt32(reader["IsSerialized"]) == 1
                        };
                    }
                }
            }
        }

        public static DataTable GetAvailableImeisForSale(string barcode)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("IMEI");

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT IMEI FROM ProductUnits WHERE Barcode = @Barcode AND Status = 'InStock' ORDER BY UnitId", conn))
                {
                    cmd.Parameters.AddWithValue("@Barcode", barcode);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read()) dt.Rows.Add(reader["IMEI"].ToString());
                    }
                }
            }
            return dt;
        }

        public static SaleRecord GetSaleById(int saleId)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT Barcode, ProductName, Price, QuantitySold, Total, PaymentType, PaymentMethod, SaleDate, IMEI FROM Sales WHERE SaleID = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", saleId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return null;
                        return new SaleRecord
                        {
                            SaleId = saleId,
                            Barcode = reader["Barcode"].ToString(),
                            ProductName = reader["ProductName"].ToString(),
                            Price = Convert.ToDecimal(reader["Price"]),
                            QuantitySold = Convert.ToInt32(reader["QuantitySold"]),
                            Total = Convert.ToDecimal(reader["Total"]),
                            PaymentType = reader["PaymentType"] == DBNull.Value ? null : reader["PaymentType"].ToString(),
                            PaymentMethod = reader["PaymentMethod"] == DBNull.Value ? null : reader["PaymentMethod"].ToString(),
                            SaleDate = reader["SaleDate"].ToString(),
                            IMEI = reader["IMEI"] == DBNull.Value ? null : reader["IMEI"].ToString()
                        };
                    }
                }
            }
        }

        public static int GetDailyInvoiceNumber(int saleId, string saleDate)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM Sales WHERE date(SaleDate) = date(@SaleDate) AND SaleID <= @SaleId", conn))
                {
                    cmd.Parameters.AddWithValue("@SaleDate", saleDate);
                    cmd.Parameters.AddWithValue("@SaleId", saleId);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        // آخر عملية بيع + بيانات العميل المرتبط بيها (لو موجود) - مستخدمة لإرسال آخر فاتورة واتساب
        public static (int SaleId, string ProductName, int QuantitySold, decimal Total, string SaleDate, string CustomerPhone, string CustomerName) GetLastSaleWithCustomer()
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(
                    "SELECT S.SaleID, S.ProductName, S.QuantitySold, S.Total, S.SaleDate, C.Phone, C.CustomerName FROM Sales S LEFT JOIN Customers C ON S.CustomerId = C.CustomerId ORDER BY S.SaleID DESC LIMIT 1", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return (0, null, 0, 0, null, null, null);
                    return (
                        Convert.ToInt32(reader["SaleID"]),
                        reader["ProductName"].ToString(),
                        Convert.ToInt32(reader["QuantitySold"]),
                        Convert.ToDecimal(reader["Total"]),
                        reader["SaleDate"].ToString(),
                        reader["Phone"] != DBNull.Value ? reader["Phone"].ToString() : null,
                        reader["CustomerName"] != DBNull.Value ? reader["CustomerName"].ToString() : null
                    );
                }
            }
        }

        // آخر عملية بيع بكل تفاصيلها (منتج/IMEI/دفع/عميل/كاشير) - مستخدمة في تصميم إيصال الطباعة الجديد.
        // اسم الكاشير مش متخزن على صف Sales نفسه، بيتجاب من قيد اليومية اللي اترحل وقت البيع
        // (JournalEntries.SourceType='Sale' AND SourceId=SaleId) - نفس القيد اللي بيسجله الـAccountingEngine.
        public static ReceiptData GetLastSaleForReceipt()
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(@"
                    SELECT S.SaleID, S.Barcode, S.ProductName, S.Price, S.QuantitySold, S.Total,
                           S.PaymentType, S.PaymentMethod, S.SaleDate, S.IMEI, C.CustomerName, C.Phone,
                           (SELECT CreatedBy FROM JournalEntries WHERE SourceType = 'Sale' AND SourceId = S.SaleID ORDER BY JournalEntryId LIMIT 1) AS CashierName
                    FROM Sales S
                    LEFT JOIN Customers C ON S.CustomerId = C.CustomerId
                    ORDER BY S.SaleID DESC LIMIT 1;", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;

                    var receipt = new ReceiptData
                    {
                        SaleId = Convert.ToInt32(reader["SaleID"]),
                        Barcode = reader["Barcode"] == DBNull.Value ? null : reader["Barcode"].ToString(),
                        ProductName = reader["ProductName"].ToString(),
                        UnitPrice = Convert.ToDecimal(reader["Price"]),
                        Quantity = Convert.ToInt32(reader["QuantitySold"]),
                        Total = Convert.ToDecimal(reader["Total"]),
                        PaymentType = reader["PaymentType"] == DBNull.Value ? null : reader["PaymentType"].ToString(),
                        PaymentMethod = reader["PaymentMethod"] == DBNull.Value ? null : reader["PaymentMethod"].ToString(),
                        SaleDate = reader["SaleDate"].ToString(),
                        IMEI = reader["IMEI"] == DBNull.Value ? null : reader["IMEI"].ToString(),
                        CustomerName = reader["CustomerName"] == DBNull.Value ? null : reader["CustomerName"].ToString(),
                        CustomerPhone = reader["Phone"] == DBNull.Value ? null : reader["Phone"].ToString(),
                        CashierName = reader["CashierName"] == DBNull.Value ? null : reader["CashierName"].ToString(),
                    };

                    receipt.DailyInvoiceNumber = GetDailyInvoiceNumber(receipt.SaleId, receipt.SaleDate);
                    return receipt;
                }
            }
        }

        // آخر عملية بيع (من غير عميل) - مستخدمة وقت طباعة آخر فاتورة
        public static (string ProductName, int QuantitySold, decimal Total, string SaleDate, int SaleId) GetLastSale()
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT SaleID, ProductName, QuantitySold, Total, SaleDate FROM Sales ORDER BY SaleID DESC LIMIT 1", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return (null, 0, 0, null, 0);
                    return (
                        reader["ProductName"].ToString(),
                        Convert.ToInt32(reader["QuantitySold"]),
                        Convert.ToDecimal(reader["Total"]),
                        reader["SaleDate"].ToString(),
                        Convert.ToInt32(reader["SaleID"])
                    );
                }
            }
        }
    }
}
