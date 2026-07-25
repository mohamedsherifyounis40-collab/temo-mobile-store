using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ClosedXML.Excel;
using TemoStore.Core.Commands;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // ExcelImportHelper: توليد قالب إكسل فاضي بنفس أعمدة كارت "إضافة منتج" في
    // المخزون، واستيراد دفعة منتجات من ملف إكسل متملّي بنفس القالب. الاستيراد
    // بيتخطى (Skip) أي صف باركوده موجود بالفعل في المخزون بدل ما يوقف العملية
    // كلها أو يستبدل بيانات موجودة - القرار ده مقصود عشان الاستيراد يكون آمن
    // يتكرر لو حصل خطأ نص الطريق من غير خوف إنه يبوّظ منتجات موجودة.
    // ==========================================================================
    public static class ExcelImportHelper
    {
        private static readonly string[] Headers = { "باركود", "اسم المنتج", "سعر الشراء", "سعر البيع", "الكمية" };

        public static void GenerateProductTemplate(string filePath)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("منتجات");

            for (int i = 0; i < Headers.Length; i++)
            {
                var cell = sheet.Cell(1, i + 1);
                cell.Value = Headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(230, 232, 238);
            }

            // صف مثال توضيحي - المستخدم هيمسحه أو يكتب فوقه
            sheet.Cell(2, 1).Value = "1234567890";
            sheet.Cell(2, 2).Value = "اسم المنتج هنا";
            sheet.Cell(2, 3).Value = 100;
            sheet.Cell(2, 4).Value = 150;
            sheet.Cell(2, 5).Value = 10;

            sheet.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        }

        public class ImportResult
        {
            public int SuccessCount;
            public List<string> SkippedDuplicateBarcodes = new();
            public List<string> RowErrors = new();
        }

        public static ImportResult ImportProducts(string filePath, string performedBy)
        {
            var result = new ImportResult();

            var existingBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DataTable existing = InventoryRepository.GetAccessoryProducts();
            foreach (DataRow row in existing.Rows)
                if (row["الباركود"] != null)
                    existingBarcodes.Add(row["الباركود"].ToString());

            using var workbook = new XLWorkbook(filePath);
            var sheet = workbook.Worksheets.First();
            var usedRows = sheet.RowsUsed().Skip(1); // بنتخطى صف العناوين

            foreach (var row in usedRows)
            {
                int rowNumber = row.RowNumber();

                string barcode = row.Cell(1).GetString().Trim();
                string productName = row.Cell(2).GetString().Trim();
                string costText = row.Cell(3).GetString().Trim();
                string saleText = row.Cell(4).GetString().Trim();
                string qtyText = row.Cell(5).GetString().Trim();

                if (string.IsNullOrEmpty(barcode) && string.IsNullOrEmpty(productName)) continue; // صف فاضي

                if (string.IsNullOrEmpty(barcode) || string.IsNullOrEmpty(productName))
                {
                    result.RowErrors.Add($"صف {rowNumber}: الباركود أو اسم المنتج فاضي.");
                    continue;
                }

                if (existingBarcodes.Contains(barcode))
                {
                    result.SkippedDuplicateBarcodes.Add(barcode);
                    continue;
                }

                if (!decimal.TryParse(costText, out decimal costPrice))
                {
                    result.RowErrors.Add($"صف {rowNumber}: سعر شراء غير صحيح (\"{costText}\").");
                    continue;
                }
                if (!decimal.TryParse(saleText, out decimal salePrice))
                {
                    result.RowErrors.Add($"صف {rowNumber}: سعر بيع غير صحيح (\"{saleText}\").");
                    continue;
                }
                if (!int.TryParse(qtyText, out int quantity))
                {
                    result.RowErrors.Add($"صف {rowNumber}: كمية غير صحيحة (\"{qtyText}\").");
                    continue;
                }

                try
                {
                    AppServices.CoreEngine.Execute(new AddAccessoryProductCommand
                    {
                        Barcode = barcode,
                        ProductName = productName,
                        CostPrice = costPrice,
                        SalePrice = salePrice,
                        Quantity = quantity,
                        PerformedBy = performedBy
                    });
                    existingBarcodes.Add(barcode); // لو نفس الباركود اتكرر في صفوف تانية جوه نفس الملف
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.RowErrors.Add($"صف {rowNumber}: {ex.Message}");
                }
            }

            return result;
        }
    }
}
