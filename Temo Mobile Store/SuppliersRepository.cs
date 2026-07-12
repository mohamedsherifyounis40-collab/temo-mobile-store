using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    // بترمي لما رقم IMEI في فاتورة الشراء يكون مسجّل بالفعل في النظام من قبل
    public class DuplicateImeiException : Exception
    {
        public string Imei { get; }
        public DuplicateImeiException(string imei) : base($"رقم الـIMEI \"{imei}\" مسجل بالفعل في النظام من قبل. راجع القايمة وشيله أو صحّحه.")
        {
            Imei = imei;
        }
    }

    public class ProductForPurchase
    {
        public string ProductName;
        public decimal Price;
        public decimal SalePrice;
        public bool IsSerialized;
    }

    public class PurchaseCartItem
    {
        public string Barcode;
        public string ProductName;
        public int Qty;
        public decimal UnitCost;
        public decimal SalePrice;
        public decimal LineTotal;
        public bool IsSerialized;
        public List<string> Imeis;
    }

    // ==========================================================================
    // SuppliersRepository: كل الوصول لقاعدة البيانات الخاص بشاشة الموردين -
    // بيانات الموردين، كشف الحساب، فواتير الشراء (مع تحديث المخزون ووحدات IMEI)،
    // وسداد الموردين.
    // ==========================================================================
    public static class SuppliersRepository
    {
        public static DataTable GetSupplierCombos()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("SupplierId", typeof(int)), new DataColumn("SupplierName") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT SupplierId, SupplierName FROM Suppliers ORDER BY SupplierName", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        dt.Rows.Add(Convert.ToInt32(reader["SupplierId"]), reader["SupplierName"].ToString());
                }
            }
            return dt;
        }

        public static DataTable GetSuppliersWithBalances()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("SupplierId"), new DataColumn("اسم المورد"), new DataColumn("التليفون"), new DataColumn("إجمالي المشتريات"), new DataColumn("إجمالي المسدد"), new DataColumn("المتبقي") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                var suppliers = new List<(int id, string name, string phone)>();
                using (SqliteCommand cmd = new SqliteCommand("SELECT SupplierId, SupplierName, Phone FROM Suppliers ORDER BY SupplierName", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        suppliers.Add((Convert.ToInt32(reader["SupplierId"]), reader["SupplierName"].ToString(), reader["Phone"]?.ToString()));
                }

                foreach (var sup in suppliers)
                {
                    decimal totalPurchases = 0, totalPaid = 0;
                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(TotalAmount) FROM Purchases WHERE SupplierId = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", sup.id);
                        var res = cmd.ExecuteScalar();
                        totalPurchases = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                    }
                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE SupplierId = @Id AND MovementType = 'صرف'", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", sup.id);
                        var res = cmd.ExecuteScalar();
                        totalPaid = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                    }

                    dt.Rows.Add(sup.id, sup.name, sup.phone, totalPurchases.ToString("N2"), totalPaid.ToString("N2"), (totalPurchases - totalPaid).ToString("N2"));
                }
            }
            return dt;
        }

        public static DataTable GetSupplierStatement(int supplierId)
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("التاريخ"), new DataColumn("النوع"), new DataColumn("التفاصيل"), new DataColumn("المبلغ") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT PurchaseId, PurchaseDate, TotalAmount, Notes FROM Purchases WHERE SupplierId = @Id ORDER BY PurchaseDate", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", supplierId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            dt.Rows.Add(reader["PurchaseDate"], "فاتورة شراء", "فاتورة رقم " + reader["PurchaseId"], reader["TotalAmount"]);
                    }
                }
                using (SqliteCommand cmd = new SqliteCommand("SELECT CreatedAt, Amount, PaymentMethod FROM CashMovements WHERE SupplierId = @Id AND MovementType = 'صرف' ORDER BY CreatedAt", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", supplierId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            dt.Rows.Add(reader["CreatedAt"], "سداد", "سداد عبر " + reader["PaymentMethod"], "-" + Convert.ToDecimal(reader["Amount"]).ToString("N2"));
                    }
                }
            }
            return dt;
        }

        public static void AddSupplier(string name, string phone)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("INSERT INTO Suppliers (SupplierName, Phone, CreatedAt) VALUES (@N, @P, @C)", conn))
                {
                    cmd.Parameters.AddWithValue("@N", name);
                    cmd.Parameters.AddWithValue("@P", (object)phone ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@C", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void UpdateSupplier(int supplierId, string name, string phone)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("UPDATE Suppliers SET SupplierName = @N, Phone = @P WHERE SupplierId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@N", name);
                    cmd.Parameters.AddWithValue("@P", (object)phone ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Id", supplierId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static bool HasPurchases(int supplierId)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM Purchases WHERE SupplierId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", supplierId);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        public static void DeleteSupplier(int supplierId)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("DELETE FROM Suppliers WHERE SupplierId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", supplierId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static ProductForPurchase GetProductForPurchase(string barcode)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT ProductName, Price, SalePrice, IsSerialized FROM Products WHERE Barcode = @Barcode", conn))
                {
                    cmd.Parameters.AddWithValue("@Barcode", barcode);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return null;
                        return new ProductForPurchase
                        {
                            ProductName = reader["ProductName"].ToString(),
                            Price = Convert.ToDecimal(reader["Price"]),
                            SalePrice = Convert.ToDecimal(reader["SalePrice"]),
                            IsSerialized = reader["IsSerialized"] != DBNull.Value && Convert.ToInt32(reader["IsSerialized"]) == 1
                        };
                    }
                }
            }
        }

        // بيحفظ فاتورة شراء كاملة (بنود + منتجات جديدة/محدّثة + وحدات IMEI + سداد كاش فوري لو مطلوب)
        // جوه Transaction واحدة - بيرمي DuplicateImeiException أو InsufficientBalanceException لو في مشكلة
        public static int SavePurchase(int supplierId, List<PurchaseCartItem> items, bool payCashNow, string cashMethod)
        {
            decimal totalAmount = 0;
            foreach (var item in items) totalAmount += item.LineTotal;

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                foreach (var checkItem in items)
                {
                    if (!checkItem.IsSerialized || checkItem.Imeis == null) continue;
                    foreach (string imei in checkItem.Imeis)
                    {
                        using (SqliteCommand cmdCheckImei = new SqliteCommand("SELECT COUNT(*) FROM ProductUnits WHERE IMEI = @IMEI", conn))
                        {
                            cmdCheckImei.Parameters.AddWithValue("@IMEI", imei);
                            if (Convert.ToInt32(cmdCheckImei.ExecuteScalar()) > 0)
                                throw new DuplicateImeiException(imei);
                        }
                    }
                }

                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        if (payCashNow)
                        {
                            decimal currentBalance = TreasuryRepository.GetBalanceInTransaction(conn, transaction, cashMethod);
                            if (totalAmount > currentBalance)
                                throw new InsufficientBalanceException(cashMethod, currentBalance);
                        }

                        int purchaseId;
                        using (SqliteCommand cmd = new SqliteCommand("INSERT INTO Purchases (SupplierId, PurchaseDate, TotalAmount, Notes) VALUES (@S, @D, @T, @N)", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@S", supplierId);
                            cmd.Parameters.AddWithValue("@D", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmd.Parameters.AddWithValue("@T", totalAmount);
                            cmd.Parameters.AddWithValue("@N", DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                        using (SqliteCommand cmdId = new SqliteCommand("SELECT last_insert_rowid();", conn, transaction))
                            purchaseId = Convert.ToInt32(cmdId.ExecuteScalar());

                        foreach (var item in items)
                        {
                            using (SqliteCommand cmd = new SqliteCommand(
                                "INSERT INTO PurchaseItems (PurchaseId, Barcode, ProductName, Quantity, UnitCost, LineTotal) VALUES (@P, @B, @N, @Q, @U, @L)", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@P", purchaseId);
                                cmd.Parameters.AddWithValue("@B", string.IsNullOrEmpty(item.Barcode) ? (object)DBNull.Value : item.Barcode);
                                cmd.Parameters.AddWithValue("@N", item.ProductName);
                                cmd.Parameters.AddWithValue("@Q", item.Qty);
                                cmd.Parameters.AddWithValue("@U", item.UnitCost);
                                cmd.Parameters.AddWithValue("@L", item.LineTotal);
                                cmd.ExecuteNonQuery();
                            }

                            bool productExists = false;
                            if (!string.IsNullOrEmpty(item.Barcode))
                            {
                                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM Products WHERE Barcode = @B", conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@B", item.Barcode);
                                    productExists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                                }
                            }

                            string finalBarcode = item.Barcode;

                            if (productExists)
                            {
                                using (SqliteCommand cmd = new SqliteCommand(
                                    "UPDATE Products SET Quantity = Quantity + @Q, Price = @U, IsSerialized = CASE WHEN @IsSerialized = 1 THEN 1 ELSE IsSerialized END WHERE Barcode = @B", conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@Q", item.Qty);
                                    cmd.Parameters.AddWithValue("@U", item.UnitCost);
                                    cmd.Parameters.AddWithValue("@IsSerialized", item.IsSerialized ? 1 : 0);
                                    cmd.Parameters.AddWithValue("@B", item.Barcode);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            else
                            {
                                finalBarcode = string.IsNullOrEmpty(item.Barcode) ? ("NEW-" + DateTime.Now.Ticks) : item.Barcode;
                                using (SqliteCommand cmd = new SqliteCommand(
                                    "INSERT INTO Products (Barcode, ProductName, Price, SalePrice, Quantity, IsSerialized) VALUES (@B, @N, @U, @S, @Q, @IsSerialized)", conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@B", finalBarcode);
                                    cmd.Parameters.AddWithValue("@N", item.ProductName);
                                    cmd.Parameters.AddWithValue("@U", item.UnitCost);
                                    cmd.Parameters.AddWithValue("@S", item.SalePrice);
                                    cmd.Parameters.AddWithValue("@Q", item.Qty);
                                    cmd.Parameters.AddWithValue("@IsSerialized", item.IsSerialized ? 1 : 0);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            if (item.IsSerialized && item.Imeis != null)
                            {
                                foreach (string imei in item.Imeis)
                                {
                                    using (SqliteCommand cmd = new SqliteCommand(
                                        "INSERT INTO ProductUnits (Barcode, IMEI, Status, PurchaseId, CreatedAt) VALUES (@B, @IMEI, 'InStock', @P, @C)", conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@B", finalBarcode);
                                        cmd.Parameters.AddWithValue("@IMEI", imei);
                                        cmd.Parameters.AddWithValue("@P", purchaseId);
                                        cmd.Parameters.AddWithValue("@C", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }
                        }

                        if (payCashNow)
                        {
                            using (SqliteCommand cmd = new SqliteCommand(
                                "INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode, SupplierId) VALUES (@Date, 'صرف', @Method, @Amount, @Ref, @Desc, @CreatedAt, 2100, @SupplierId)", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@Method", cashMethod);
                                cmd.Parameters.AddWithValue("@Amount", totalAmount);
                                cmd.Parameters.AddWithValue("@Ref", "فاتورة شراء رقم " + purchaseId);
                                cmd.Parameters.AddWithValue("@Desc", "سداد كاش فوري لفاتورة شراء رقم " + purchaseId);
                                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@SupplierId", supplierId);
                                cmd.ExecuteNonQuery();
                            }

                            decimal balanceBeforePayment = TreasuryRepository.GetBalanceInTransaction(conn, transaction, cashMethod);
                            TreasuryRepository.SetBalanceInTransaction(conn, transaction, cashMethod, balanceBeforePayment - totalAmount);
                        }

                        transaction.Commit();
                        return purchaseId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // بيسجّل سداد لمورد ويخصم من رصيد وسيلة الدفع، جوه Transaction واحدة
        public static void PaySupplier(int supplierId, string supplierName, string method, decimal amount)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        decimal currentBalance = TreasuryRepository.GetBalanceInTransaction(conn, transaction, method);
                        if (amount > currentBalance)
                            throw new InsufficientBalanceException(method, currentBalance);

                        using (SqliteCommand cmd = new SqliteCommand(
                            "INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode, SupplierId) VALUES (@Date, 'صرف', @Method, @Amount, @Ref, @Desc, @CreatedAt, 2100, @SupplierId)", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@Method", method);
                            cmd.Parameters.AddWithValue("@Amount", amount);
                            cmd.Parameters.AddWithValue("@Ref", "");
                            cmd.Parameters.AddWithValue("@Desc", "سداد لمورد: " + supplierName);
                            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmd.Parameters.AddWithValue("@SupplierId", supplierId);
                            cmd.ExecuteNonQuery();
                        }

                        TreasuryRepository.SetBalanceInTransaction(conn, transaction, method, currentBalance - amount);

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
    }
}
