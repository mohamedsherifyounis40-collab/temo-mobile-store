using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;
using TemoStore.Core.Exceptions;

namespace Temo_Mobile_Store
{
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
        public bool SkipInventory;
    }

    // ==========================================================================
    // SuppliersRepository: القراءة فقط لشاشة الموردين - بيانات الموردين، كشف
    // الحساب، أرصدتهم. كل عمليات الكتابة المالية (حفظ/تعديل/إلغاء فاتورة شراء،
    // سداد مورد) بقت بتعدي من خلال ICoreEngine (راجع TemoStore.Engines.Handlers).
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

        // قايمة كل فواتير الشراء (كل الموردين) - للشاشة الجديدة (سجل المشتريات)، قراءة بس
        public static DataTable GetPurchases()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("PurchaseId", typeof(int)), new DataColumn("SupplierId", typeof(int)), new DataColumn("المورد"), new DataColumn("التاريخ"), new DataColumn("الإجمالي", typeof(decimal)) });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(
                    "SELECT p.PurchaseId, p.SupplierId, s.SupplierName, p.PurchaseDate, p.TotalAmount FROM Purchases p JOIN Suppliers s ON s.SupplierId = p.SupplierId ORDER BY p.PurchaseId DESC", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        dt.Rows.Add(Convert.ToInt32(reader["PurchaseId"]), Convert.ToInt32(reader["SupplierId"]), reader["SupplierName"], reader["PurchaseDate"], Convert.ToDecimal(reader["TotalAmount"]));
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

        // بيحمّل بنود فاتورة شراء موجودة (مع أرقام IMEI) عشان تتحمل في عربة التعديل
        public static List<PurchaseCartItem> GetPurchaseItemsForInvoice(int purchaseId)
        {
            var items = new List<PurchaseCartItem>();
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT Barcode, ProductName, Quantity, UnitCost, LineTotal, SkipInventory FROM PurchaseItems WHERE PurchaseId = @Id", conn))
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
                                Imeis = null,
                                SkipInventory = Convert.ToInt32(reader["SkipInventory"]) == 1
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
    }
}
