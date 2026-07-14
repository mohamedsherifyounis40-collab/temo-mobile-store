using System;
using System.Data;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    // بتترمي لما الكمية المطلوبة في عملية بيع (تسجيل/تعديل) تتعدى المتاح فعليًا بالمخزون
    public class InsufficientStockException : Exception
    {
        public int AvailableQuantity { get; }

        public InsufficientStockException(int availableQuantity)
            : base($"الكمية المطلوبة أكبر من المتاح! المتاح حالياً هو: {availableQuantity}")
        {
            AvailableQuantity = availableQuantity;
        }
    }

    // بتترمي لما محاولة تعديل/إلغاء بيع تابع ليوم محاسبي تم إقفاله بالفعل
    public class DateClosedException : Exception
    {
        public DateClosedException(string message) : base(message) { }
    }

    public class ProductForSale
    {
        public string ProductName;
        public decimal SalePrice;
        public int Quantity;
        public bool IsSerialized;
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

        // بيسجل عملية بيع كاملة (فحص مخزون + إدراج + تحديث وحدة IMEI لو موجودة + تحديث الكمية
        // + قبض في الخزينة لو البيع كاش) في Transaction واحدة، وبيرجع رقم فاتورة اليوم
        public static int AddSale(string barcode, string productName, decimal salePrice, int quantitySold, decimal total, object customerId, string paymentType, string imei, string paymentMethod)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        int currentStock = 0; decimal currentCostPrice = 0;
                        using (SqliteCommand cmdCheck = new SqliteCommand("SELECT Quantity, Price FROM Products WHERE Barcode = @Barcode", conn, transaction))
                        {
                            cmdCheck.Parameters.AddWithValue("@Barcode", barcode);
                            using (SqliteDataReader reader = cmdCheck.ExecuteReader())
                            {
                                if (reader.Read()) { currentStock = Convert.ToInt32(reader["Quantity"]); currentCostPrice = Convert.ToDecimal(reader["Price"]); }
                            }
                        }

                        if (quantitySold > currentStock)
                            throw new InsufficientStockException(currentStock);

                        using (SqliteCommand cmdInsert = new SqliteCommand(
                            "INSERT INTO Sales (Barcode, ProductName, CostPrice, Price, QuantitySold, Total, CustomerId, PaymentType, IMEI, PaymentMethod) VALUES (@Barcode, @ProductName, @CostPrice, @Price, @QuantitySold, @Total, @CustomerId, @PaymentType, @IMEI, @PaymentMethod)", conn, transaction))
                        {
                            cmdInsert.Parameters.AddWithValue("@Barcode", barcode);
                            cmdInsert.Parameters.AddWithValue("@ProductName", productName);
                            cmdInsert.Parameters.AddWithValue("@CostPrice", currentCostPrice);
                            cmdInsert.Parameters.AddWithValue("@Price", salePrice);
                            cmdInsert.Parameters.AddWithValue("@QuantitySold", quantitySold);
                            cmdInsert.Parameters.AddWithValue("@Total", total);
                            cmdInsert.Parameters.AddWithValue("@CustomerId", customerId ?? DBNull.Value);
                            cmdInsert.Parameters.AddWithValue("@PaymentType", paymentType);
                            cmdInsert.Parameters.AddWithValue("@IMEI", (object)imei ?? DBNull.Value);
                            cmdInsert.Parameters.AddWithValue("@PaymentMethod", paymentType == "Cash" ? (object)paymentMethod : DBNull.Value);
                            cmdInsert.ExecuteNonQuery();
                        }

                        int newSaleId;
                        using (SqliteCommand cmdId = new SqliteCommand("SELECT last_insert_rowid();", conn, transaction))
                            newSaleId = Convert.ToInt32(cmdId.ExecuteScalar());

                        if (imei != null)
                        {
                            using (SqliteCommand cmdUnit = new SqliteCommand("UPDATE ProductUnits SET Status = 'Sold', SaleId = @SaleId WHERE IMEI = @IMEI", conn, transaction))
                            {
                                cmdUnit.Parameters.AddWithValue("@SaleId", newSaleId);
                                cmdUnit.Parameters.AddWithValue("@IMEI", imei);
                                cmdUnit.ExecuteNonQuery();
                            }
                        }

                        using (SqliteCommand cmdUpdate = new SqliteCommand("UPDATE Products SET Quantity = Quantity - @Qty WHERE Barcode = @Barcode", conn, transaction))
                        {
                            cmdUpdate.Parameters.AddWithValue("@Qty", quantitySold);
                            cmdUpdate.Parameters.AddWithValue("@Barcode", barcode);
                            cmdUpdate.ExecuteNonQuery();
                        }

                        if (paymentType == "Cash")
                        {
                            using (SqliteCommand cmd = new SqliteCommand(
                                "INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode, CustomerId, SaleId) VALUES (@Date, 'قبض', @Method, @Amount, @Ref, @Desc, @CreatedAt, 4100, @CustomerId, @SaleId)", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@Method", paymentMethod);
                                cmd.Parameters.AddWithValue("@Amount", total);
                                cmd.Parameters.AddWithValue("@Ref", "فاتورة بيع رقم " + newSaleId);
                                cmd.Parameters.AddWithValue("@Desc", "قبض كاش من عملية بيع رقم " + newSaleId);
                                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@CustomerId", customerId ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@SaleId", newSaleId);
                                cmd.ExecuteNonQuery();
                            }

                            decimal balanceBeforeSale = TreasuryRepository.GetBalanceInTransaction(conn, transaction, paymentMethod);
                            TreasuryRepository.SetBalanceInTransaction(conn, transaction, paymentMethod, balanceBeforeSale + total);
                        }

                        int dailyInvoiceNumber;
                        using (SqliteCommand cmdDaily = new SqliteCommand("SELECT COUNT(*) FROM Sales WHERE date(SaleDate) = date('now','localtime') AND SaleID <= @SaleId", conn, transaction))
                        {
                            cmdDaily.Parameters.AddWithValue("@SaleId", newSaleId);
                            dailyInvoiceNumber = Convert.ToInt32(cmdDaily.ExecuteScalar());
                        }

                        transaction.Commit();
                        return dailyInvoiceNumber;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
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

        // بيعدّل كمية بيع موجود، وبيظبط كمية المخزون تبعًا لذلك، جوه Transaction واحدة
        public static void UpdateSaleQuantity(int saleId, int newQty)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string barcode; int oldQty; decimal price; string saleDate; decimal oldTotal; string paymentType; string paymentMethod;
                        using (SqliteCommand cmd = new SqliteCommand("SELECT Barcode, QuantitySold, Price, Total, SaleDate, PaymentType, PaymentMethod FROM Sales WHERE SaleID = @Id", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", saleId);
                            using (SqliteDataReader reader = cmd.ExecuteReader())
                            {
                                if (!reader.Read())
                                    throw new InvalidOperationException("لم يتم العثور على عملية البيع.");
                                barcode = reader["Barcode"].ToString();
                                oldQty = Convert.ToInt32(reader["QuantitySold"]);
                                price = Convert.ToDecimal(reader["Price"]);
                                oldTotal = Convert.ToDecimal(reader["Total"]);
                                saleDate = reader["SaleDate"].ToString();
                                paymentType = reader["PaymentType"] == DBNull.Value ? null : reader["PaymentType"].ToString();
                                paymentMethod = reader["PaymentMethod"] == DBNull.Value ? null : reader["PaymentMethod"].ToString();
                            }
                        }

                        if (UIHelpers.IsDateClosed(DateTime.Parse(saleDate).Date))
                            throw new DateClosedException("لا يمكن تعديل عملية بيع تابعة ليوم تم إقفاله بالفعل.");

                        int currentStock = 0;
                        using (SqliteCommand cmd = new SqliteCommand("SELECT Quantity FROM Products WHERE Barcode = @Barcode", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Barcode", barcode);
                            var res = cmd.ExecuteScalar();
                            if (res != null) currentStock = Convert.ToInt32(res);
                        }

                        int availableIfReverted = currentStock + oldQty;
                        if (newQty > availableIfReverted)
                            throw new InsufficientStockException(availableIfReverted);

                        decimal newTotal = price * newQty;
                        decimal totalDelta = newTotal - oldTotal;

                        if (paymentType == "Cash" && paymentMethod != null && totalDelta != 0)
                        {
                            decimal currentBalance = TreasuryRepository.GetBalanceInTransaction(conn, transaction, paymentMethod);
                            if (totalDelta < 0 && -totalDelta > currentBalance)
                                throw new InsufficientBalanceException(paymentMethod, currentBalance);

                            using (SqliteCommand cmd = new SqliteCommand(
                                "INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode, SaleId) VALUES (@Date, @Type, @Method, @Amount, @Ref, @Desc, @CreatedAt, 4100, @SaleId)", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@Type", totalDelta > 0 ? "قبض" : "صرف");
                                cmd.Parameters.AddWithValue("@Method", paymentMethod);
                                cmd.Parameters.AddWithValue("@Amount", Math.Abs(totalDelta));
                                cmd.Parameters.AddWithValue("@Ref", "تعديل فاتورة بيع رقم " + saleId);
                                cmd.Parameters.AddWithValue("@Desc", "تسوية كاش بسبب تعديل الكمية في عملية بيع رقم " + saleId);
                                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@SaleId", saleId);
                                cmd.ExecuteNonQuery();
                            }

                            TreasuryRepository.SetBalanceInTransaction(conn, transaction, paymentMethod, currentBalance + totalDelta);
                        }

                        using (SqliteCommand cmdUpdateSale = new SqliteCommand("UPDATE Sales SET QuantitySold = @Qty, Total = @Total WHERE SaleID = @Id", conn, transaction))
                        {
                            cmdUpdateSale.Parameters.AddWithValue("@Qty", newQty);
                            cmdUpdateSale.Parameters.AddWithValue("@Total", newTotal);
                            cmdUpdateSale.Parameters.AddWithValue("@Id", saleId);
                            cmdUpdateSale.ExecuteNonQuery();
                        }

                        int stockAdjustment = oldQty - newQty;
                        using (SqliteCommand cmdUpdateStock = new SqliteCommand("UPDATE Products SET Quantity = Quantity + @Adjustment WHERE Barcode = @Barcode", conn, transaction))
                        {
                            cmdUpdateStock.Parameters.AddWithValue("@Adjustment", stockAdjustment);
                            cmdUpdateStock.Parameters.AddWithValue("@Barcode", barcode);
                            cmdUpdateStock.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // بيلغي عملية بيع: يرجّع الكمية للمخزون، يرجّع وحدة الـ IMEI (لو موجودة) لحالة "متاح"، ويحذف البيع
        public static void CancelSale(int saleId, string barcode, int quantitySold, string imei)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string saleDate; decimal saleTotal; string paymentType; string paymentMethod;
                        using (SqliteCommand cmdSaleDate = new SqliteCommand("SELECT SaleDate, Total, PaymentType, PaymentMethod FROM Sales WHERE SaleID = @Id", conn, transaction))
                        {
                            cmdSaleDate.Parameters.AddWithValue("@Id", saleId);
                            using (SqliteDataReader reader = cmdSaleDate.ExecuteReader())
                            {
                                if (!reader.Read())
                                    throw new InvalidOperationException("لم يتم العثور على عملية البيع.");
                                saleDate = reader["SaleDate"].ToString();
                                saleTotal = Convert.ToDecimal(reader["Total"]);
                                paymentType = reader["PaymentType"] == DBNull.Value ? null : reader["PaymentType"].ToString();
                                paymentMethod = reader["PaymentMethod"] == DBNull.Value ? null : reader["PaymentMethod"].ToString();
                            }
                        }

                        if (UIHelpers.IsDateClosed(DateTime.Parse(saleDate).Date))
                            throw new DateClosedException("لا يمكن إلغاء عملية بيع تابعة ليوم تم إقفاله بالفعل.");

                        if (paymentType == "Cash" && paymentMethod != null)
                        {
                            decimal currentBalance = TreasuryRepository.GetBalanceInTransaction(conn, transaction, paymentMethod);
                            if (saleTotal > currentBalance)
                                throw new InsufficientBalanceException(paymentMethod, currentBalance);

                            using (SqliteCommand cmd = new SqliteCommand(
                                "INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode, SaleId) VALUES (@Date, 'صرف', @Method, @Amount, @Ref, @Desc, @CreatedAt, 4100, @SaleId)", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@Method", paymentMethod);
                                cmd.Parameters.AddWithValue("@Amount", saleTotal);
                                cmd.Parameters.AddWithValue("@Ref", "إلغاء فاتورة بيع رقم " + saleId);
                                cmd.Parameters.AddWithValue("@Desc", "قيد عكسي - إلغاء عملية بيع رقم " + saleId);
                                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@SaleId", saleId);
                                cmd.ExecuteNonQuery();
                            }

                            TreasuryRepository.SetBalanceInTransaction(conn, transaction, paymentMethod, currentBalance - saleTotal);
                        }

                        using (SqliteCommand cmdUpdateStock = new SqliteCommand("UPDATE Products SET Quantity = Quantity + @Qty WHERE Barcode = @Barcode", conn, transaction))
                        {
                            cmdUpdateStock.Parameters.AddWithValue("@Qty", quantitySold);
                            cmdUpdateStock.Parameters.AddWithValue("@Barcode", barcode);
                            cmdUpdateStock.ExecuteNonQuery();
                        }

                        if (imei != null)
                        {
                            using (SqliteCommand cmdUnit = new SqliteCommand("UPDATE ProductUnits SET Status = 'InStock', SaleId = NULL WHERE IMEI = @IMEI", conn, transaction))
                            {
                                cmdUnit.Parameters.AddWithValue("@IMEI", imei);
                                cmdUnit.ExecuteNonQuery();
                            }
                        }

                        using (SqliteCommand cmdDelete = new SqliteCommand("DELETE FROM Sales WHERE SaleID = @Id", conn, transaction))
                        {
                            cmdDelete.Parameters.AddWithValue("@Id", saleId);
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
