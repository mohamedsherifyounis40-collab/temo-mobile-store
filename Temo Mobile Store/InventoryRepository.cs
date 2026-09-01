using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    // نفس شكل ProductDto في CatalogWebsite - مستخدمة وقت المزامنة مع موقع الكتالوج البارة
    public class CatalogProductDto
    {
        public string Barcode { get; set; }
        public string ProductName { get; set; }
        public decimal SalePrice { get; set; }
        public int Quantity { get; set; }
    }

    // ==========================================================================
    // InventoryRepository: كل الوصول لقاعدة البيانات الخاص بشاشتي "المخزون"
    // (إكسسوارات + أجهزة/IMEI) و"جرد المخزن".
    // ==========================================================================
    public static class InventoryRepository
    {
        // ---------- المخزون (منتجات عادية غير مسلسلة) ----------

        // صورة منتج (اختيارية، تجميلية بس) - بتتحفظ/تتقرأ مباشرة من غير ما تعدّي على
        // CoreEngine (زي إضافة/تعديل المنتج نفسه) لأنها مش عملية تجارية محتاجة تدقيق/محاسبة،
        // نفس مبدأ StoreSettings.LogoImage بالظبط
        public static void UpdateProductImage(string barcode, byte[] image)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("UPDATE Products SET ProductImage = @Image WHERE Barcode = @Barcode", conn))
                {
                    cmd.Parameters.AddWithValue("@Image", (object)image ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Barcode", barcode);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static byte[] GetProductImage(string barcode)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT ProductImage FROM Products WHERE Barcode = @Barcode", conn))
                {
                    cmd.Parameters.AddWithValue("@Barcode", barcode);
                    object result = cmd.ExecuteScalar();
                    return result == null || result == DBNull.Value ? null : (byte[])result;
                }
            }
        }

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

        // كل المنتجات (إكسسوارات + أجهزة) بشكل بسيط لمزامنتهم مع موقع الكتالوج البارة
        public static List<CatalogProductDto> GetProductsForCatalogSync()
        {
            var result = new List<CatalogProductDto>();
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT Barcode, ProductName, SalePrice, Quantity FROM Products ORDER BY ProductName", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new CatalogProductDto
                        {
                            Barcode = reader["Barcode"].ToString(),
                            ProductName = reader["ProductName"].ToString(),
                            SalePrice = Convert.ToDecimal(reader["SalePrice"]),
                            Quantity = Convert.ToInt32(reader["Quantity"])
                        });
                    }
                }
            }
            return result;
        }

        // ---------- الأجهزة والسريالات (موبايلات بـ IMEI) ----------

        public static DataTable GetImeiUnits(string statusFilter, string search)
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("IMEI"), new DataColumn("الباركود"), new DataColumn("المنتج"), new DataColumn("سعر الشراء"), new DataColumn("سعر البيع"), new DataColumn("الحالة"), new DataColumn("تاريخ الإضافة") });

            string query = @"SELECT PU.IMEI, PU.Barcode, PU.Status, PU.CreatedAt, P.ProductName, P.Price, P.SalePrice
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
                            dt.Rows.Add(reader["IMEI"], reader["Barcode"], reader["ProductName"], reader["Price"], reader["SalePrice"], statusArabic, reader["CreatedAt"]);
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

        // تاريخ حركة منتج معين بالكامل (بيع/شراء/تسوية جرد) في مكان واحد، مرتب زمنيًا -
        // مفيدة لما تحب تعرف "المنتج ده اتباع/اتشرى/اتجرد إمتى وبكام".
        public static DataTable GetProductMovementHistory(string barcode)
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] {
                new DataColumn("التاريخ"), new DataColumn("النوع"), new DataColumn("التفاصيل"),
                new DataColumn("الكمية"), new DataColumn("المبلغ")
            });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                string query = @"
                    SELECT SaleDate AS OpDate, 'بيع' AS OpType,
                           (CASE WHEN PaymentType = 'Credit' THEN 'بيع آجل' ELSE 'بيع نقدي - ' || COALESCE(PaymentMethod, 'نقدي') END) AS Details,
                           QuantitySold AS Qty, Total AS Amount
                        FROM Sales WHERE Barcode = @Barcode
                    UNION ALL
                    SELECT P.PurchaseDate AS OpDate, 'شراء' AS OpType,
                           'من: ' || COALESCE(S.SupplierName, 'بدون مورد') AS Details,
                           PI.Quantity AS Qty, PI.LineTotal AS Amount
                        FROM PurchaseItems PI
                        INNER JOIN Purchases P ON PI.PurchaseId = P.PurchaseId
                        LEFT JOIN Suppliers S ON P.SupplierId = S.SupplierId
                        WHERE PI.Barcode = @Barcode
                    UNION ALL
                    SELECT AdjustmentDate AS OpDate, 'تسوية جرد' AS OpType,
                           'قبل: ' || SystemQuantityBefore || ' - بعد: ' || CountedQuantity AS Details,
                           Difference AS Qty, 0 AS Amount
                        FROM InventoryAdjustments WHERE Barcode = @Barcode
                    ORDER BY OpDate ASC";

                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Barcode", barcode);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string opType = reader["OpType"].ToString() ?? "";
                            int qty = Convert.ToInt32(reader["Qty"]);
                            decimal amount = Convert.ToDecimal(reader["Amount"]);
                            // للبيع/الشراء الكمية دايمًا رقم موجب (كمية العملية نفسها)، أما تسوية
                            // الجرد فالإشارة نفسها هي المعنى (زيادة أو نقص فعلي في الرصيد)
                            string qtyText = opType == "تسوية جرد" ? (qty > 0 ? $"+{qty}" : qty.ToString()) : qty.ToString();
                            dt.Rows.Add(
                                reader["OpDate"], opType, reader["Details"],
                                qtyText,
                                amount == 0 ? "" : amount.ToString("N2")
                            );
                        }
                    }
                }
            }

            return dt;
        }
    }
}
