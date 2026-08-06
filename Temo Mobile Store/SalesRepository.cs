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

    // نتيجة بحث عن منتج (بالاسم أو الأكثر مبيعًا) - زي ProductForSale بس معاها الباركود
    // نفسه، لازم نعرفه عشان نقدر نحمّل المنتج المختار في حقول البيع
    public class ProductSearchResult
    {
        public string Barcode;
        public string ProductName;
        public decimal SalePrice;
        public int Quantity;
        public bool IsSerialized;
        public byte[] Image;
    }

    // نتيجة بحث عن جهاز بالسيريال/IMEI - المنتج المرتبط بيه + IMEI نفسه محدد سلفًا
    public class SerialSearchResult
    {
        public string IMEI;
        public string Barcode;
        public string ProductName;
        public decimal SalePrice;
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
        public decimal Discount;
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
        public decimal Discount;
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
                using (SqliteCommand cmd = new SqliteCommand("SELECT Barcode, ProductName, Price, QuantitySold, Total, Discount, PaymentType, PaymentMethod, SaleDate, IMEI FROM Sales WHERE SaleID = @Id", conn))
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
                            Discount = reader["Discount"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Discount"]),
                            PaymentType = reader["PaymentType"] == DBNull.Value ? null : reader["PaymentType"].ToString(),
                            PaymentMethod = reader["PaymentMethod"] == DBNull.Value ? null : reader["PaymentMethod"].ToString(),
                            SaleDate = reader["SaleDate"].ToString(),
                            IMEI = reader["IMEI"] == DBNull.Value ? null : reader["IMEI"].ToString()
                        };
                    }
                }
            }
        }

        public static int GetDailyInvoiceNumber(int invoiceId, string saleDate)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(DISTINCT SalesInvoiceId) FROM Sales WHERE date(SaleDate) = date(@SaleDate) AND SalesInvoiceId <= @InvoiceId", conn))
                {
                    cmd.Parameters.AddWithValue("@SaleDate", saleDate);
                    cmd.Parameters.AddWithValue("@InvoiceId", invoiceId);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        // آخر فاتورة بيع (ممكن تحتوي على أكتر من صنف) + بيانات العميل المرتبط بيها (لو موجود) -
        // مستخدمة لإرسال آخر فاتورة واتساب. الإجمالي والكمية هنا مجمّعين على كل أصناف الفاتورة.
        public static (int InvoiceId, string ProductName, int QuantitySold, decimal Total, string SaleDate, string CustomerPhone, string CustomerName, int DailyInvoiceNumber) GetLastSaleWithCustomer()
        {
            var items = GetLastInvoiceForReceipt();
            if (items == null || items.Count == 0) return (0, null, 0, 0, null, null, null, 0);

            var first = items[0];
            string productDisplay = items.Count == 1 ? first.ProductName : $"{first.ProductName} + {items.Count - 1} صنف تاني";
            int totalQty = 0;
            decimal totalAmount = 0;
            foreach (var item in items) { totalQty += item.Quantity; totalAmount += item.Total; }

            return (first.SaleId, productDisplay, totalQty, totalAmount, first.SaleDate, first.CustomerPhone, first.CustomerName, first.DailyInvoiceNumber);
        }

        // كل أصناف آخر فاتورة بيع اتسجلت (ممكن تكون أكتر من صنف تحت نفس رقم الفاتورة) بكل
        // تفاصيلها (منتج/IMEI/دفع/عميل/كاشير) - مستخدمة في تصميم إيصال الطباعة. اسم الكاشير مش
        // متخزن على صف Sales نفسه، بيتجاب من قيد اليومية اللي اترحل وقت البيع
        // (JournalEntries.SourceType='Sale' AND SourceId=SalesInvoiceId) - نفس القيد اللي بيسجله الـAccountingEngine.
        public static System.Collections.Generic.List<ReceiptData> GetLastInvoiceForReceipt()
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                int invoiceId;
                using (SqliteCommand cmdLast = new SqliteCommand("SELECT SalesInvoiceId FROM Sales ORDER BY SaleID DESC LIMIT 1;", conn))
                {
                    object result = cmdLast.ExecuteScalar();
                    if (result == null || result == DBNull.Value) return null;
                    invoiceId = Convert.ToInt32(result);
                }

                var items = new System.Collections.Generic.List<ReceiptData>();
                using (SqliteCommand cmd = new SqliteCommand(@"
                    SELECT S.SaleID, S.Barcode, S.ProductName, S.Price, S.QuantitySold, S.Total, S.Discount,
                           S.PaymentType, S.PaymentMethod, S.SaleDate, S.IMEI, C.CustomerName, C.Phone,
                           (SELECT CreatedBy FROM JournalEntries WHERE SourceType = 'Sale' AND SourceId = @InvoiceId ORDER BY JournalEntryId LIMIT 1) AS CashierName
                    FROM Sales S
                    LEFT JOIN Customers C ON S.CustomerId = C.CustomerId
                    WHERE S.SalesInvoiceId = @InvoiceId
                    ORDER BY S.SaleID ASC;", conn))
                {
                    cmd.Parameters.AddWithValue("@InvoiceId", invoiceId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new ReceiptData
                            {
                                SaleId = Convert.ToInt32(reader["SaleID"]),
                                Barcode = reader["Barcode"] == DBNull.Value ? null : reader["Barcode"].ToString(),
                                ProductName = reader["ProductName"].ToString(),
                                UnitPrice = Convert.ToDecimal(reader["Price"]),
                                Quantity = Convert.ToInt32(reader["QuantitySold"]),
                                Total = Convert.ToDecimal(reader["Total"]),
                                Discount = reader["Discount"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["Discount"]),
                                PaymentType = reader["PaymentType"] == DBNull.Value ? null : reader["PaymentType"].ToString(),
                                PaymentMethod = reader["PaymentMethod"] == DBNull.Value ? null : reader["PaymentMethod"].ToString(),
                                SaleDate = reader["SaleDate"].ToString(),
                                IMEI = reader["IMEI"] == DBNull.Value ? null : reader["IMEI"].ToString(),
                                CustomerName = reader["CustomerName"] == DBNull.Value ? null : reader["CustomerName"].ToString(),
                                CustomerPhone = reader["Phone"] == DBNull.Value ? null : reader["Phone"].ToString(),
                                CashierName = reader["CashierName"] == DBNull.Value ? null : reader["CashierName"].ToString(),
                            });
                        }
                    }
                }

                if (items.Count == 0) return null;

                int dailyInvoiceNumber = GetDailyInvoiceNumber(invoiceId, items[0].SaleDate);
                foreach (var item in items) item.DailyInvoiceNumber = dailyInvoiceNumber;

                return items;
            }
        }

        // بحث عن منتج بالاسم أو الباركود (تاب "منتج" في شاشة البيع) - أول 20 نتيجة مطابقة بس
        public static System.Collections.Generic.List<ProductSearchResult> SearchProducts(string term)
        {
            var results = new System.Collections.Generic.List<ProductSearchResult>();
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(
                    "SELECT Barcode, ProductName, SalePrice, Quantity, IsSerialized, ProductImage FROM Products " +
                    "WHERE Barcode LIKE @Term OR ProductName LIKE @Term ORDER BY ProductName LIMIT 20", conn))
                {
                    cmd.Parameters.AddWithValue("@Term", $"%{term}%");
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new ProductSearchResult
                            {
                                Barcode = reader["Barcode"].ToString(),
                                ProductName = reader["ProductName"].ToString(),
                                SalePrice = Convert.ToDecimal(reader["SalePrice"]),
                                Quantity = Convert.ToInt32(reader["Quantity"]),
                                IsSerialized = reader["IsSerialized"] != DBNull.Value && Convert.ToInt32(reader["IsSerialized"]) == 1,
                                Image = reader["ProductImage"] == DBNull.Value ? null : (byte[])reader["ProductImage"]
                            });
                        }
                    }
                }
            }
            return results;
        }

        // بحث عن جهاز متاح بالسيريال/IMEI (تاب "سيريال" في شاشة البيع) - بس الأجهزة اللي
        // لسه في المخزون (Status='InStock')، أول 20 نتيجة مطابقة
        public static System.Collections.Generic.List<SerialSearchResult> SearchProductsBySerial(string term)
        {
            var results = new System.Collections.Generic.List<SerialSearchResult>();
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(@"
                    SELECT pu.IMEI, p.Barcode, p.ProductName, p.SalePrice
                    FROM ProductUnits pu
                    JOIN Products p ON pu.Barcode = p.Barcode
                    WHERE pu.Status = 'InStock' AND pu.IMEI LIKE @Term
                    ORDER BY p.ProductName LIMIT 20", conn))
                {
                    cmd.Parameters.AddWithValue("@Term", $"%{term}%");
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new SerialSearchResult
                            {
                                IMEI = reader["IMEI"].ToString(),
                                Barcode = reader["Barcode"].ToString(),
                                ProductName = reader["ProductName"].ToString(),
                                SalePrice = Convert.ToDecimal(reader["SalePrice"])
                            });
                        }
                    }
                }
            }
            return results;
        }

        // المنتجات الأكثر مبيعًا (بالكمية) - لكروت "الأكثر مبيعًا" السريعة في شاشة البيع.
        // بيرجع بيانات المنتج الحيّة (السعر/الكمية الحالية) مش القديمة وقت البيع
        public static System.Collections.Generic.List<ProductSearchResult> GetPopularProducts(int topN = 6)
        {
            var results = new System.Collections.Generic.List<ProductSearchResult>();
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(@"
                    SELECT p.Barcode, p.ProductName, p.SalePrice, p.Quantity, p.IsSerialized, p.ProductImage
                    FROM Products p
                    JOIN (SELECT Barcode, SUM(QuantitySold) AS Sold FROM Sales GROUP BY Barcode ORDER BY Sold DESC LIMIT @TopN) top
                        ON p.Barcode = top.Barcode
                    WHERE p.Quantity > 0
                    ORDER BY top.Sold DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@TopN", topN);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new ProductSearchResult
                            {
                                Barcode = reader["Barcode"].ToString(),
                                ProductName = reader["ProductName"].ToString(),
                                SalePrice = Convert.ToDecimal(reader["SalePrice"]),
                                Quantity = Convert.ToInt32(reader["Quantity"]),
                                IsSerialized = reader["IsSerialized"] != DBNull.Value && Convert.ToInt32(reader["IsSerialized"]) == 1,
                                Image = reader["ProductImage"] == DBNull.Value ? null : (byte[])reader["ProductImage"]
                            });
                        }
                    }
                }
            }
            return results;
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
