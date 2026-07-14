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
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("التاريخ"), new DataColumn("النوع"), new DataColumn("التفاصيل"), new DataColumn("المبلغ"), new DataColumn("PurchaseId", typeof(int)) });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT PurchaseId, PurchaseDate, TotalAmount, Notes FROM Purchases WHERE SupplierId = @Id ORDER BY PurchaseDate", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", supplierId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            dt.Rows.Add(reader["PurchaseDate"], "فاتورة شراء", "فاتورة رقم " + reader["PurchaseId"], reader["TotalAmount"], Convert.ToInt32(reader["PurchaseId"]));
                    }
                }
                using (SqliteCommand cmd = new SqliteCommand("SELECT CreatedAt, Amount, PaymentMethod FROM CashMovements WHERE SupplierId = @Id AND MovementType = 'صرف' ORDER BY CreatedAt", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", supplierId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            dt.Rows.Add(reader["CreatedAt"], "سداد", "سداد عبر " + reader["PaymentMethod"], "-" + Convert.ToDecimal(reader["Amount"]).ToString("N2"), DBNull.Value);
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

        // بيتأكد إن أرقام الـIMEI الجديدة مش مسجّلة قبل كده - excludePurchaseId بيستثني
        // وحدات فاتورة معينة من الفحص (مستخدم في التعديل، بعد ما نكون شلنا وحداتها القديمة أصلاً)
        private static void CheckImeisNotDuplicated(SqliteConnection conn, SqliteTransaction transaction, List<PurchaseCartItem> items)
        {
            foreach (var checkItem in items)
            {
                if (!checkItem.IsSerialized || checkItem.Imeis == null) continue;
                foreach (string imei in checkItem.Imeis)
                {
                    using (SqliteCommand cmdCheckImei = new SqliteCommand("SELECT COUNT(*) FROM ProductUnits WHERE IMEI = @IMEI", conn, transaction))
                    {
                        cmdCheckImei.Parameters.AddWithValue("@IMEI", imei);
                        if (Convert.ToInt32(cmdCheckImei.ExecuteScalar()) > 0)
                            throw new DuplicateImeiException(imei);
                    }
                }
            }
        }

        // بيضيف بنود فاتورة شراء (بنود + منتجات جديدة/محدّثة + وحدات IMEI) لفاتورة موجودة بالفعل -
        // مستخدم لإنشاء فاتورة جديدة ولإعادة تطبيق البنود بعد تعديل فاتورة قديمة
        private static void ApplyPurchaseItems(SqliteConnection conn, SqliteTransaction transaction, int purchaseId, List<PurchaseCartItem> items)
        {
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
        }

        // بيسجّل سداد كاش فوري لفاتورة شراء (حركة خزينة + خصم من الرصيد)، مربوط بالفاتورة عبر PurchaseId
        private static void ApplyCashPayment(SqliteConnection conn, SqliteTransaction transaction, int purchaseId, int supplierId, decimal totalAmount, string cashMethod)
        {
            decimal currentBalance = TreasuryRepository.GetBalanceInTransaction(conn, transaction, cashMethod);
            if (totalAmount > currentBalance)
                throw new InsufficientBalanceException(cashMethod, currentBalance);

            using (SqliteCommand cmd = new SqliteCommand(
                "INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode, SupplierId, PurchaseId) VALUES (@Date, 'صرف', @Method, @Amount, @Ref, @Desc, @CreatedAt, 2100, @SupplierId, @PurchaseId)", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@Method", cashMethod);
                cmd.Parameters.AddWithValue("@Amount", totalAmount);
                cmd.Parameters.AddWithValue("@Ref", "فاتورة شراء رقم " + purchaseId);
                cmd.Parameters.AddWithValue("@Desc", "سداد كاش فوري لفاتورة شراء رقم " + purchaseId);
                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@SupplierId", supplierId);
                cmd.Parameters.AddWithValue("@PurchaseId", purchaseId);
                cmd.ExecuteNonQuery();
            }

            TreasuryRepository.SetBalanceInTransaction(conn, transaction, cashMethod, currentBalance - totalAmount);
        }

        // بيرجّع فاتورة شراء (مخزون + وحدات IMEI + سداد كاش لو كان موجود) لحالتها قبل الفاتورة -
        // مستخدم في الإلغاء وفي التعديل (بيتشال القديم الأول قبل ما يتطبق الجديد).
        // بيرمي InvalidOperationException لو أي صنف من الفاتورة اتباع بالفعل (منتج بسيريال بيع، أو
        // منتج عادي كميته في المخزون بقت أقل من كمية الفاتورة) - في الحالة دي مينفعش يتلغي/يتعدل.
        // بيرمي DateClosedException لو تاريخ الفاتورة تابع ليوم مقفول.
        // بيرجع إجمالي الفاتورة القديمة (مفيد للمنادي لمعرفة قد إيه كان اتسدد).
        private static decimal ReversePurchaseEffects(SqliteConnection conn, SqliteTransaction transaction, int purchaseId)
        {
            string purchaseDate; int supplierId; decimal oldTotalAmount;
            using (SqliteCommand cmd = new SqliteCommand("SELECT PurchaseDate, SupplierId, TotalAmount FROM Purchases WHERE PurchaseId = @Id", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", purchaseId);
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                        throw new InvalidOperationException("لم يتم العثور على فاتورة الشراء.");
                    purchaseDate = reader["PurchaseDate"].ToString();
                    supplierId = reader["SupplierId"] == DBNull.Value ? -1 : Convert.ToInt32(reader["SupplierId"]);
                    oldTotalAmount = Convert.ToDecimal(reader["TotalAmount"]);
                }
            }

            if (UIHelpers.IsDateClosed(DateTime.Parse(purchaseDate).Date))
                throw new DateClosedException("لا يمكن إلغاء أو تعديل فاتورة شراء تابعة ليوم تم إقفاله بالفعل.");

            var items = new List<(string Barcode, int Quantity)>();
            using (SqliteCommand cmd = new SqliteCommand("SELECT Barcode, Quantity FROM PurchaseItems WHERE PurchaseId = @Id", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", purchaseId);
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        items.Add((reader["Barcode"] == DBNull.Value ? null : reader["Barcode"].ToString(), Convert.ToInt32(reader["Quantity"])));
                }
            }

            using (SqliteCommand cmd = new SqliteCommand("SELECT IMEI, Status FROM ProductUnits WHERE PurchaseId = @Id", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", purchaseId);
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!string.Equals(reader["Status"].ToString(), "InStock", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException($"الجهاز صاحب رقم IMEI \"{reader["IMEI"]}\" من الفاتورة دي اتباع بالفعل، لا يمكن إلغاء أو تعديل الفاتورة.");
                    }
                }
            }

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Barcode)) continue;

                int currentStock = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT Quantity FROM Products WHERE Barcode = @B", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@B", item.Barcode);
                    var res = cmd.ExecuteScalar();
                    if (res != null) currentStock = Convert.ToInt32(res);
                }

                if (currentStock < item.Quantity)
                    throw new InvalidOperationException("الكمية المتاحة بالمخزون لبعض أصناف الفاتورة دي أقل من كميتها الأصلية، يبدو إن جزء من البضاعة اتباع بالفعل. لا يمكن إلغاء أو تعديل الفاتورة.");
            }

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Barcode)) continue;

                using (SqliteCommand cmd = new SqliteCommand("UPDATE Products SET Quantity = Quantity - @Q WHERE Barcode = @B", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Q", item.Quantity);
                    cmd.Parameters.AddWithValue("@B", item.Barcode);
                    cmd.ExecuteNonQuery();
                }
            }

            using (SqliteCommand cmd = new SqliteCommand("DELETE FROM ProductUnits WHERE PurchaseId = @Id", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", purchaseId);
                cmd.ExecuteNonQuery();
            }

            using (SqliteCommand cmd = new SqliteCommand("DELETE FROM PurchaseItems WHERE PurchaseId = @Id", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", purchaseId);
                cmd.ExecuteNonQuery();
            }

            // لو الفاتورة كانت اتسددت كاش فوري، بنعمل قيد عكسي (حركة "قبض" بنفس القيمة والوسيلة)
            // بدل ما نحذف حركة الصرف الأصلية - عشان يفضل أثرها موجود في سجل حركات الخزينة للمراجعة.
            string cashMethod = null; decimal cashAmount = 0;
            using (SqliteCommand cmd = new SqliteCommand("SELECT PaymentMethod, Amount FROM CashMovements WHERE PurchaseId = @Id AND MovementType = 'صرف'", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", purchaseId);
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        cashMethod = reader["PaymentMethod"].ToString();
                        cashAmount = Convert.ToDecimal(reader["Amount"]);
                    }
                }
            }

            if (cashMethod != null)
            {
                using (SqliteCommand cmd = new SqliteCommand(
                    "INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode, SupplierId, PurchaseId) VALUES (@Date, 'قبض', @Method, @Amount, @Ref, @Desc, @CreatedAt, 2100, @SupplierId, @PurchaseId)", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@Method", cashMethod);
                    cmd.Parameters.AddWithValue("@Amount", cashAmount);
                    cmd.Parameters.AddWithValue("@Ref", "إلغاء/تعديل فاتورة شراء رقم " + purchaseId);
                    cmd.Parameters.AddWithValue("@Desc", "قيد عكسي - استرجاع سداد كاش فوري لفاتورة شراء رقم " + purchaseId);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@SupplierId", supplierId == -1 ? (object)DBNull.Value : supplierId);
                    cmd.Parameters.AddWithValue("@PurchaseId", purchaseId);
                    cmd.ExecuteNonQuery();
                }

                decimal balanceBeforeReversal = TreasuryRepository.GetBalanceInTransaction(conn, transaction, cashMethod);
                TreasuryRepository.SetBalanceInTransaction(conn, transaction, cashMethod, balanceBeforeReversal + cashAmount);
            }

            return oldTotalAmount;
        }

        // بيلغي فاتورة شراء بالكامل: يرجّع المخزون والكاش (لو كان مدفوع) لحالته قبل الفاتورة، وبيحذفها
        public static void CancelPurchase(int purchaseId)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        ReversePurchaseEffects(conn, transaction, purchaseId);

                        using (SqliteCommand cmd = new SqliteCommand("DELETE FROM Purchases WHERE PurchaseId = @Id", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", purchaseId);
                            cmd.ExecuteNonQuery();
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

        // بيحمّل بنود فاتورة شراء موجودة (مع أرقام IMEI) عشان تتحمل في عربة التعديل
        public static List<PurchaseCartItem> GetPurchaseItemsForInvoice(int purchaseId)
        {
            var items = new List<PurchaseCartItem>();
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT Barcode, ProductName, Quantity, UnitCost, LineTotal FROM PurchaseItems WHERE PurchaseId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", purchaseId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new PurchaseCartItem
                            {
                                Barcode = reader["Barcode"] == DBNull.Value ? null : reader["Barcode"].ToString(),
                                ProductName = reader["ProductName"].ToString(),
                                Qty = Convert.ToInt32(reader["Quantity"]),
                                UnitCost = Convert.ToDecimal(reader["UnitCost"]),
                                LineTotal = Convert.ToDecimal(reader["LineTotal"]),
                                IsSerialized = false,
                                Imeis = null
                            });
                        }
                    }
                }

                foreach (var item in items)
                {
                    if (string.IsNullOrEmpty(item.Barcode)) continue;
                    using (SqliteCommand cmd = new SqliteCommand("SELECT IMEI FROM ProductUnits WHERE PurchaseId = @Id AND Barcode = @B", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", purchaseId);
                        cmd.Parameters.AddWithValue("@B", item.Barcode);
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            var imeis = new List<string>();
                            while (reader.Read()) imeis.Add(reader["IMEI"].ToString());
                            if (imeis.Count > 0)
                            {
                                item.IsSerialized = true;
                                item.Imeis = imeis;
                            }
                        }
                    }
                }
            }
            return items;
        }

        // بيعدّل فاتورة شراء موجودة: بيرجّع أثرها القديم بالكامل (مخزون/IMEI/كاش) ثم بيطبّق
        // البنود الجديدة كأنها فاتورة جديدة، جوه Transaction واحدة، وبيحافظ على نفس رقم
        // ونفس تاريخ الفاتورة الأصليين. بيرمي نفس الاستثناءات اللي بيرميها الإلغاء والحفظ العادي.
        public static void EditPurchase(int purchaseId, int supplierId, List<PurchaseCartItem> newItems, bool payCashNow, string cashMethod)
        {
            decimal totalAmount = 0;
            foreach (var item in newItems) totalAmount += item.LineTotal;

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        ReversePurchaseEffects(conn, transaction, purchaseId);

                        CheckImeisNotDuplicated(conn, transaction, newItems);

                        if (payCashNow)
                        {
                            decimal currentBalance = TreasuryRepository.GetBalanceInTransaction(conn, transaction, cashMethod);
                            if (totalAmount > currentBalance)
                                throw new InsufficientBalanceException(cashMethod, currentBalance);
                        }

                        using (SqliteCommand cmd = new SqliteCommand("UPDATE Purchases SET SupplierId = @S, TotalAmount = @T WHERE PurchaseId = @Id", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@S", supplierId);
                            cmd.Parameters.AddWithValue("@T", totalAmount);
                            cmd.Parameters.AddWithValue("@Id", purchaseId);
                            cmd.ExecuteNonQuery();
                        }

                        ApplyPurchaseItems(conn, transaction, purchaseId, newItems);

                        if (payCashNow)
                            ApplyCashPayment(conn, transaction, purchaseId, supplierId, totalAmount, cashMethod);

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

        // بيحفظ فاتورة شراء كاملة (بنود + منتجات جديدة/محدّثة + وحدات IMEI + سداد كاش فوري لو مطلوب)
        // جوه Transaction واحدة - بيرمي DuplicateImeiException أو InsufficientBalanceException لو في مشكلة
        public static int SavePurchase(int supplierId, List<PurchaseCartItem> items, bool payCashNow, string cashMethod)
        {
            decimal totalAmount = 0;
            foreach (var item in items) totalAmount += item.LineTotal;

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                CheckImeisNotDuplicated(conn, null, items);

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

                        ApplyPurchaseItems(conn, transaction, purchaseId, items);

                        if (payCashNow)
                            ApplyCashPayment(conn, transaction, purchaseId, supplierId, totalAmount, cashMethod);

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
