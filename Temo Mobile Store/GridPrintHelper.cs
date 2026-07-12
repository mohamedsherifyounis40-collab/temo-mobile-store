using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // GridPrintHelper: دالة عامة لطباعة أي جدول (تقرير) من أي شاشة.
    // المستخدم يقدر يختار طابعة عادية، أو "Microsoft Print to PDF" (مدمجة في
    // ويندوز) عشان يحفظ التقرير كملف PDF بدون أي مكتبات إضافية.
    // ==========================================================================
    public static class GridPrintHelper
    {
        public static void Print(DataGridView dgv, string title, Form owner)
        {
            if (dgv.Rows.Count == 0)
            {
                MessageBox.Show("مفيش بيانات في الجدول عشان تتطبع. اعرض التقرير الأول.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int currentRow = 0;
            PrintDocument pd = new PrintDocument();
            pd.DefaultPageSettings.Landscape = true;

            pd.PrintPage += (s, e) =>
            {
                Graphics g = e.Graphics;
                float y = e.MarginBounds.Top;
                float pageWidth = e.MarginBounds.Width;

                Font fontTitle = new Font("Arial", 14, FontStyle.Bold);
                Font fontHeader = new Font("Arial", 9, FontStyle.Bold);
                Font fontCell = new Font("Arial", 8.5F);

                if (currentRow == 0)
                {
                    g.DrawString(title, fontTitle, Brushes.Black, e.MarginBounds.Left, y);
                    y += 30;
                    g.DrawString("تاريخ الطباعة: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"), new Font("Arial", 8), Brushes.Gray, e.MarginBounds.Left, y);
                    y += 25;
                }

                int visibleColCount = 0;
                foreach (DataGridViewColumn col in dgv.Columns)
                    if (col.Visible) visibleColCount++;
                if (visibleColCount == 0) visibleColCount = 1;
                float colWidth = pageWidth / visibleColCount;

                // رؤوس الأعمدة
                float x = e.MarginBounds.Left;
                foreach (DataGridViewColumn col in dgv.Columns)
                {
                    if (!col.Visible) continue;
                    g.DrawString(col.HeaderText, fontHeader, Brushes.Black, new RectangleF(x, y, colWidth, 20));
                    x += colWidth;
                }
                y += 22;
                g.DrawLine(Pens.Black, e.MarginBounds.Left, y, e.MarginBounds.Left + pageWidth, y);
                y += 4;

                while (currentRow < dgv.Rows.Count)
                {
                    if (y + 20 > e.MarginBounds.Bottom)
                    {
                        e.HasMorePages = true;
                        return;
                    }

                    x = e.MarginBounds.Left;
                    var row = dgv.Rows[currentRow];
                    foreach (DataGridViewColumn col in dgv.Columns)
                    {
                        if (!col.Visible) continue;
                        string cellText = row.Cells[col.Index].Value?.ToString() ?? "";
                        g.DrawString(cellText, fontCell, Brushes.Black, new RectangleF(x, y, colWidth, 18));
                        x += colWidth;
                    }
                    y += 20;
                    currentRow++;
                }

                e.HasMorePages = false;
            };

            using (PrintDialog printDialog = new PrintDialog())
            {
                printDialog.Document = pd;
                printDialog.AllowSomePages = false;
                printDialog.UseEXDialog = true;
                if (printDialog.ShowDialog(owner) == DialogResult.OK)
                {
                    currentRow = 0;
                    pd.Print();
                }
            }
        }
    }
}
