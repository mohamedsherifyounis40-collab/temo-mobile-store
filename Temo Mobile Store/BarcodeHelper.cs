using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // BarcodeHelper: رسم وطباعة باركود من نوع Code39 (بيدعم أرقام وحروف)
    // بدون أي مكتبات خارجية - رسم مباشر بالـ GDI+.
    // ==========================================================================
    public static class BarcodeHelper
    {
        private static readonly Dictionary<char, string> Code39Table = new Dictionary<char, string>
        {
            {'0',"000110100"}, {'1',"100100001"}, {'2',"001100001"}, {'3',"101100000"},
            {'4',"000110001"}, {'5',"100110000"}, {'6',"001110000"}, {'7',"000100101"},
            {'8',"100100100"}, {'9',"001100100"}, {'A',"100001001"}, {'B',"001001001"},
            {'C',"101001000"}, {'D',"000011001"}, {'E',"100011000"}, {'F',"001011000"},
            {'G',"000001101"}, {'H',"100001100"}, {'I',"001001100"}, {'J',"000011100"},
            {'K',"100000011"}, {'L',"001000011"}, {'M',"101000010"}, {'N',"000010011"},
            {'O',"100010010"}, {'P',"001010010"}, {'Q',"000000111"}, {'R',"100000110"},
            {'S',"001000110"}, {'T',"000010110"}, {'U',"110000001"}, {'V',"011000001"},
            {'W',"111000000"}, {'X',"010010001"}, {'Y',"110010000"}, {'Z',"011010000"},
            {'-',"010000101"}, {'.',"110000100"}, {' ',"011000100"}, {'$',"010101000"},
            {'/',"010100010"}, {'+',"010001010"}, {'%',"000101010"}, {'*',"010010100"}
        };

        // بيرسم الباركود جوه المستطيل المحدد
        public static void DrawCode39(Graphics g, string rawCode, Rectangle bounds)
        {
            string code = "*" + (rawCode ?? "").ToUpper() + "*";

            // نحسب العرض الكلي الأول عشان نظبط حجم الخط تلقائيًا حسب المساحة المتاحة
            int narrow = 2;
            int wide = narrow * 3;
            int totalWidth = 0;
            foreach (char c in code)
            {
                if (!Code39Table.ContainsKey(c)) continue;
                string pattern = Code39Table[c];
                foreach (char bit in pattern)
                    totalWidth += bit == '1' ? wide : narrow;
                totalWidth += narrow;
            }

            if (totalWidth > bounds.Width && totalWidth > 0)
            {
                double scale = (double)bounds.Width / totalWidth;
                narrow = Math.Max(1, (int)(narrow * scale));
                wide = narrow * 3;
            }

            int x = bounds.Left;
            foreach (char c in code)
            {
                if (!Code39Table.ContainsKey(c)) continue;
                string pattern = Code39Table[c];
                for (int i = 0; i < pattern.Length; i++)
                {
                    int w = pattern[i] == '1' ? wide : narrow;
                    bool isBar = (i % 2 == 0);
                    if (isBar)
                        g.FillRectangle(Brushes.Black, x, bounds.Top, w, bounds.Height);
                    x += w;
                }
                x += narrow;
            }
        }

        // بيفتح معاينة طباعة لملصق باركود واحد (باركود + اسم المنتج)
        public static void PrintBarcodeLabel(string barcode, string productName, IWin32Window owner)
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                MessageBox.Show("مفيش باركود لهذا المنتج.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            PrintDocument pd = new PrintDocument();
            pd.DefaultPageSettings.PaperSize = new PaperSize("BarcodeLabel", 280, 160);

            pd.PrintPage += (s, e) =>
            {
                Graphics g = e.Graphics;
                float pageWidth = e.PageBounds.Width;
                StringFormat center = new StringFormat { Alignment = StringAlignment.Center };

                g.DrawString(productName ?? "", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, new RectangleF(0, 5, pageWidth, 20), center);

                Rectangle barcodeBounds = new Rectangle(10, 30, (int)pageWidth - 20, 70);
                DrawCode39(g, barcode, barcodeBounds);

                g.DrawString(barcode, new Font("Consolas", 10), Brushes.Black, new RectangleF(0, 105, pageWidth, 20), center);
            };

            PrintPreviewDialog preview = new PrintPreviewDialog { Document = pd, Width = 500, Height = 500 };
            preview.ShowDialog(owner);
        }
    }
}
