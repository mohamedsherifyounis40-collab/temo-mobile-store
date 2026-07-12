using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    public class InventoryAdjustmentRow
    {
        public string Barcode;
        public string ProductName;
        public int SystemQty;
        public int CountedQty;
        public int Difference;
    }

    // ==========================================================================
    // InventoryRepository: كل الوصول لقاعدة البيانات الخاص بشاشتي "المخزون"
    // (إكسسوارات + أجهزة/IMEI) و"جرد المخزن".
    // ==========================================================================
    public static class InventoryRepository
    {
        // ---------- المخزون (منتجات عادية غير مسلسلة) ----------

        public static DataTable GetAccessoryProducts()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("الباركود"), new DataColumn("اسم المنتج"), new DataColumn("سعر الشراء"), new DataColumn("سعر البيع"), new DataColumn("الكمية") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT Barcode, ProductName, Price, SalePrice, Quantity FROM Products WHERE IsSerialized = 0 OR IsSerialized IS NULL ORDER BY ROWID ASC", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        dt.Rows.Add(reader["Barcode"], reader["ProductName"], reader["Price"], reader["SalePrice"], reader["Quantity"]);
                }
            }
            return dt;
        }

        public static void AddAccessoryProduct(string barcode, string productName, decimal costPrice, decimal salePrice, int quantity)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("INSERT INTO Products (Barcode, ProductName, Price, SalePrice, Quantity, IsSerialized) VALUES (@Barcode, @ProductName, @Price, @SalePrice, @Quantity, 0)", conn))
                {
                    cmd.Parameters.AddWithValue("@Barcode", barcode);
                    cmd.Parameters.AddWithValue("@ProductName", productName);
                    cmd.Parameters.AddWithValue("@Price", costPrice);
                    cmd.Parameters.AddWithValue("@SalePrice", salePrice);
                    cmd.Parameters.AddWithValue("@Quantity", quantity);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void UpdateAccessoryProduct(string barcode, string productName, decimal costPrice, decimal salePrice, int quantity)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("UPDATE Products SET ProductName = @ProductName, Price = @Price, SalePrice = @SalePrice, Quantity = @Quantity WHERE Barcode = @Barcode", conn))
                {
                    cmd.Parameters.AddWithValue("@Barcode", barcode);
                    cmd.Parameters.AddWithValue("@ProductName", productName);
                    cmd.Parameters.AddWithValue("@Price", costPrice);
                    cmd.Parameters.AddWithValue("@SalePrice", salePrice);
                    cmd.Parameters.AddWithValue("@Quantity", quantity);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void DeleteProduct(string barcode)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("DELETE FROM Products WHERE Barcode = @Barcode", conn))
                {
                    cmd.Parameters.AddWithValue("@Barcode", barcode);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ---------- الأجهزة والسريالات (موبايلات بـ IMEI) ----------

        public static DataTable GetImeiUnits(string statusFilter, string search)
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("IMEI"), new DataColumn("الباركود"), new DataColumn("المنتج"), new DataColumn("الحالة"), new DataColumn("تاريخ الإضافة") });

            string query = @"SELECT PU.IMEI, PU.Barcode, PU.Status, PU.CreatedAt, P.ProductName
                FROM ProductUnits PU LEFT JOIN Products P ON PU.Barcode = P.Barcode
                WHERE (PU.IMEI LIKE @Search OR P.ProductName LIKE @Search)";

            if (statusFilter == "متاح في المخزون") query += " AND PU.Status = 'InStock'";
            else if (statusFilter == "مباع") query += " AND PU.Status = 'Sold'";

            query += " ORDER BY PU.CreatedAt DESC";

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Search", "%" + search + "%");
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string statusArabic = reader["Status"].ToString() == "InStock" ? "متاح في المخزون" : (reader["Status"].ToString() == "Sold" ? "مباع" : reader["Status"].ToString());
                            dt.Rows.Add(reader["IMEI"], reader["Barcode"], reader["ProductName"], statusArabic, reader["CreatedAt"]);
                        }
                    }
                }
            }
            return dt;
        }

        public static ProductForPurchase GetProductByBarcode(string barcode)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT ProductName, Price, SalePrice FROM Products WHERE Barcode = @B", conn))
                {
                    cmd.Parameters.AddWithValue("@B", barcode);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return null;
                        return new ProductForPurchase
                        {
                            ProductName = reader["ProductName"].ToString(),
                            Price = Convert.ToDecimal(reader["Price"]),
                            SalePrice = Convert.ToDecimal(reader["SalePrice"])
                        };
                    }
                }
            }
        }

        public static void UpdateModelPrice(string barcode, string productName, decimal costPrice, decimal salePrice)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("UPDATE Products SET ProductName = @Name, Price = @Cost, SalePrice = @Sale WHERE Barcode = @Barcode", conn))
                {
                    cmd.Parameters.AddWithValue("@Name", productName);
                    cmd.Parameters.AddWithValue("@Cost", costPrice);
                    cmd.Parameters.AddWithValue("@Sale", salePrice);
                    cmd.Parameters.AddWithValue("@Barcode", barcode);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // بيضيف جهاز جديد بالـ IMEI - لو الباركود موجود بيزوّد كميته، لو مش موجود بينشئ منتج جديد.
        // بيرمي DuplicateImeiException لو الرقم مسجل قبل كده
        public static void AddDevice(string barcode, string productName, decimal costPrice, decimal salePrice, string imei)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                using (SqliteCommand cmdCheckImei = new SqliteCommand("SELECT COUNT(*) FROM ProductUnits WHERE IMEI = @IMEI", conn))
                {
                    cmdCheckImei.Parameters.AddWithValue("@IMEI", imei);
                    if (Convert.ToInt32(cmdCheckImei.ExecuteScalar()) > 0)
                        throw new DuplicateImeiException(imei);
                }

                bool productExists = false;
                if (!string.IsNullOrEmpty(barcode))
                {
                    using (SqliteCommand cmdCheck = new SqliteCommand("SELECT COUNT(*) FROM Products WHERE Barcode = @B", conn))
                    {
                        cmdCheck.Parameters.AddWithValue("@B", barcode);
                        productExists = Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0;
                    }
                }

                string finalBarcode = barcode;

                if (productExists)
                {
                    using (SqliteCommand cmd = new SqliteCommand("UPDATE Products SET Quantity = Quantity + 1, Price = @U, IsSerialized = 1 WHERE Barcode = @B", conn))
                    {
                        cmd.Parameters.AddWithValue("@U", costPrice);
                        cmd.Parameters.AddWithValue("@B", barcode);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    finalBarcode = string.IsNullOrEmpty(barcode) ? ("NEW-" + DateTime.Now.Ticks) : barcode;
                    using (SqliteCommand cmd = new SqliteCommand("INSERT INTO Products (Barcode, ProductName, Price, SalePrice, Quantity, IsSerialized) VALUES (@B, @N, @U, @S, 1, 1)", conn))
                    {
                        cmd.Parameters.AddWithValue("@B", finalBarcode);
                        cmd.Parameters.AddWithValue("@N", productName);
                        cmd.Parameters.AddWithValue("@U", costPrice);
                        cmd.Parameters.AddWithValue("@S", salePrice);
                        cmd.ExecuteNonQuery();
                    }
                }

                using (SqliteCommand cmd = new SqliteCommand("INSERT INTO ProductUnits (Barcode, IMEI, Status, PurchaseId, CreatedAt) VALUES (@B, @IMEI, 'InStock', NULL, @C)", conn))
                {
                    cmd.Parameters.AddWithValue("@B", finalBarcode);
                    cmd.Parameters.AddWithValue("@IMEI", imei);
                    cmd.Parameters.AddWithValue("@C", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ---------- جرد المخزن ----------

        public static DataTable GetProductsForCount()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("الباركود");
            dt.Columns.Add("اسم المنتج");
            dt.Columns.Add("الكمية بالنظام", typeof(int));
            dt.Columns.Add("الكمية الفعلية");
            dt.Columns.Add("الفرق");

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT Barcode, ProductName, Quantity FROM Products ORDER BY ProductName ASC", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        dt.Rows.Add(reader["Barcode"].ToString(), reader["ProductName"].ToString(), Convert.ToInt32(reader["Quantity"]), "", "");
                }
            }
            return dt;
        }

        // بيحفظ نتيجة الجرد: يعدّل كمية كل صنف اتغيّر ويسجّل الفرق في سجل التسويات، جوه Transaction واحدة
        public static void SaveInventoryAdjustments(List<InventoryAdjustmentRow> changedRows)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        foreach (var r in changedRows)
                        {
                            using (SqliteCommand cmdUpdate = new SqliteCommand("UPDATE Products SET Quantity = @NewQty WHERE Barcode = @Barcode", conn, transaction))
                            {
                                cmdUpdate.Parameters.AddWithValue("@NewQty", r.CountedQty);
                                cmdUpdate.Parameters.AddWithValue("@Barcode", r.Barcode);
                                cmdUpdate.ExecuteNonQuery();
                            }

                            using (SqliteCommand cmdLog = new SqliteCommand("INSERT INTO InventoryAdjustments (Barcode, ProductName, SystemQuantityBefore, CountedQuantity, Difference, AdjustmentDate) VALUES (@Barcode, @Name, @Before, @Counted, @Diff, @Date)", conn, transaction))
                            {
                                cmdLog.Parameters.AddWithValue("@Barcode", r.Barcode);
                                cmdLog.Parameters.AddWithValue("@Name", r.ProductName);
                                cmdLog.Parameters.AddWithValue("@Before", r.SystemQty);
                                cmdLog.Parameters.AddWithValue("@Counted", r.CountedQty);
                                cmdLog.Parameters.AddWithValue("@Diff", r.Difference);
                                cmdLog.Parameters.AddWithValue("@Date", nowStr);
                                cmdLog.ExecuteNonQuery();
                            }
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

        public static DataTable GetAdjustmentsLog()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("التاريخ والوقت");
            dt.Columns.Add("الباركود");
            dt.Columns.Add("اسم المنتج");
            dt.Columns.Add("الكمية قبل الجرد");
            dt.Columns.Add("الكمية بعد الجرد");
            dt.Columns.Add("الفرق");

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT AdjustmentDate, Barcode, ProductName, SystemQuantityBefore, CountedQuantity, Difference FROM InventoryAdjustments ORDER BY AdjustmentId DESC", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int diff = Convert.ToInt32(reader["Difference"]);
                        dt.Rows.Add(reader["AdjustmentDate"], reader["Barcode"], reader["ProductName"], reader["SystemQuantityBefore"], reader["CountedQuantity"], diff > 0 ? $"+{diff}" : diff.ToString());
                    }
                }
            }
            return dt;
        }
    }
}
