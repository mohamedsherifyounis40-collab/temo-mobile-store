using System;
using System.Data;
using System.IO;
using ClosedXML.Excel;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // ExcelExportHelper: بيصدّر أي DataTable (نتيجة أي تقرير في البرنامج) لملف Excel
    // حقيقي في مجلد "TemoStore_Reports" جوه مستندات المستخدم - نفس مكان وأسلوب حفظ
    // فواتير الـ PDF (ReceiptPrintHelper) والنسخ الاحتياطية (BackupManager)، عشان
    // كل ملفات البرنامج المُصدَّرة تبقى في أماكن متوقعة وموحدة.
    // ==========================================================================
    public static class ExcelExportHelper
    {
        public static string ReportsFolderPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TemoStore_Reports");

        public static bool ExportToExcel(DataTable table, string reportName, out string filePath, out string error)
        {
            filePath = null;
            error = null;

            try
            {
                if (!Directory.Exists(ReportsFolderPath)) Directory.CreateDirectory(ReportsFolderPath);

                string safeName = reportName.Length > 20 ? reportName.Substring(0, 20) : reportName;
                filePath = Path.Combine(ReportsFolderPath, $"{safeName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.xlsx");

                using (var workbook = new XLWorkbook())
                {
                    string sheetName = reportName.Length > 31 ? reportName.Substring(0, 31) : reportName;
                    var sheet = workbook.Worksheets.Add(sheetName);
                    sheet.RightToLeft = true;
                    sheet.Cell(1, 1).InsertTable(table, "ReportData", true);
                    sheet.Columns().AdjustToContents();
                    workbook.SaveAs(filePath);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = "حصل خطأ أثناء التصدير: " + ex.Message;
                filePath = null;
                return false;
            }
        }

        public static void OpenReportsFolder()
        {
            if (!Directory.Exists(ReportsFolderPath)) Directory.CreateDirectory(ReportsFolderPath);
            System.Diagnostics.Process.Start("explorer.exe", ReportsFolderPath);
        }
    }
}
