using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // طباعة/تصدير ملخص اليوم - نفس أسلوب GridPrintHelper/ReceiptPrintHelper: نافذة طباعة
    // ويندوز العادية، المستخدم يختار طابعة حقيقية أو "Microsoft Print to PDF" لحفظه كـ PDF
    // من غير أي مكتبة خارجية. منقولة زي ما هي من MainShell.BuildHomeDashboardPageOld
    // (اللي بقت كود ميت بعد ما "الرئيسية" اتحولت لـ Blazor) - كانت شغالة بالظبط، بس
    // مربوطة بزرار مش موجود في الشاشة الحالية، فمكانها الصح إنها Helper تقدر أي شاشة توصلها.
    // ==========================================================================
    public static class DailySummaryPrintHelper
    {
        public static void Print(
            IWin32Window? owner,
            decimal todaySales, decimal todayPurchases, decimal todayExpenses, decimal todayProfit,
            int invoiceCount, int maintenanceCount,
            List<(string Method, decimal Balance)> methodBalances, decimal totalBalance)
        {
            PrintDocument pd = new PrintDocument();
            pd.DefaultPageSettings.Landscape = false;

            pd.PrintPage += (s, e) =>
            {
                Graphics g = e.Graphics;
                float left = e.MarginBounds.Left;
                float width = e.MarginBounds.Width;
                float y = e.MarginBounds.Top;

                Font fontTitle = new Font("Arial", 16, FontStyle.Bold);
                Font fontDate = new Font("Arial", 9);
                Font fontSectionTitle = new Font("Arial", 10.5F, FontStyle.Bold);
                Font fontLabel = new Font("Arial", 10);
                Font fontValue = new Font("Arial", 10, FontStyle.Bold);
                Font fontFooter = new Font("Arial", 8);

                StringFormat sfLeft = new StringFormat { Alignment = StringAlignment.Near };
                StringFormat sfRight = new StringFormat { Alignment = StringAlignment.Far };

                g.DrawString("ملخص يومي — Temo Mobile Store", fontTitle, Brushes.Black, new RectangleF(left, y, width, 30), new StringFormat { Alignment = StringAlignment.Center });
                y += 32;
                g.DrawString(DateTime.Now.ToString("yyyy-MM-dd"), fontDate, Brushes.Gray, new RectangleF(left, y, width, 18), new StringFormat { Alignment = StringAlignment.Center });
                y += 30;
                g.DrawLine(Pens.Black, left, y, left + width, y);
                y += 15;

                void DrawRow(string label, string value)
                {
                    g.DrawString(label, fontLabel, Brushes.Black, new RectangleF(left, y, width, 20), sfRight);
                    g.DrawString(value, fontValue, Brushes.Black, new RectangleF(left, y, width, 20), sfLeft);
                    y += 24;
                }

                DrawRow("إجمالي المبيعات", todaySales.ToString("N2") + " ج.م");
                DrawRow("إجمالي المشتريات", todayPurchases.ToString("N2") + " ج.م");
                DrawRow("إجمالي المصروفات", todayExpenses.ToString("N2") + " ج.م");
                DrawRow("صافي الربح", todayProfit.ToString("N2") + " ج.م");
                DrawRow("عدد الفواتير", invoiceCount.ToString());
                DrawRow("طلبات الصيانة", maintenanceCount.ToString());

                y += 10;
                g.DrawLine(Pens.Black, left, y, left + width, y);
                y += 15;
                g.DrawString("أرصدة وسائل الدفع", fontSectionTitle, Brushes.Black, new RectangleF(left, y, width, 20), sfRight);
                y += 26;

                DrawRow("الإجمالي (كل الوسائل)", totalBalance.ToString("N2") + " ج.م");
                foreach (var mb in methodBalances)
                    DrawRow(mb.Method, mb.Balance.ToString("N2") + " ج.م");

                y += 20;
                g.DrawLine(Pens.Black, left, y, left + width, y);
                y += 12;
                g.DrawString("تاريخ الطباعة: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"), fontFooter, Brushes.Gray, new RectangleF(left, y, width, 16), new StringFormat { Alignment = StringAlignment.Center });

                e.HasMorePages = false;
            };

            using (PrintDialog printDialog = new PrintDialog())
            {
                printDialog.Document = pd;
                printDialog.AllowSomePages = false;
                printDialog.UseEXDialog = true;
                if (printDialog.ShowDialog(owner) == DialogResult.OK)
                    pd.Print();
            }
        }
    }
}
