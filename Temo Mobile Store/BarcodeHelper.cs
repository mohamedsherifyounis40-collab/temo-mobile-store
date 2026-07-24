using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // BarcodeHelper: رسم وطباعة باركود Code39 (لكود فاتورة قصير - DrawCode39
    // اليدوي القديم لسة مستخدم هنا لأن الكود قصير وبيشتغل صح) وCode128 (لملصقات
    // المخزون - DrawCode128 بيستخدم مكتبة ZXing.Net لأن قيم الباركود هناك بتكون
    // نص وصفي طويل، وCode128 أكثف بكتير من Code39 لنفس المحتوى فبيفضل قابل
    // للقراءة على ملصق 45×23مم الصغير).
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

            int ComputeWidth(int n, int w)
            {
                int total = 0;
                foreach (char c in code)
                {
                    if (!Code39Table.ContainsKey(c)) continue;
                    string pattern = Code39Table[c];
                    foreach (char bit in pattern)
                        total += bit == '1' ? w : n;
                    total += n;
                }
                return total;
            }

            // نحسب العرض الكلي الأول عشان نظبط حجم الخط تلقائيًا حسب المساحة المتاحة
            int narrow = 2;
            int wide = narrow * 3;

            // مسافة الأمان (Quiet Zone) على الجانبين قبل أول شرطة وبعد آخر شرطة - لازم
            // تكون موجودة فعليًا (~10 أضعاف عرض أضيق شرطة) وإلا القارئ مش هيقدر يحدد بداية/نهاية
            // الباركود ومش هيستجيب خالص. الكود القديم كان بيدي كل عرض bounds للباركود نفسه
            // ومكنش بيحجز حيّز ثابت لمسافة الأمان - فلما محتوى الباركود طويل نسبيًا لمساحة
            // الملصق (45×23مم)، مسافة الأمان كانت بتقرب من الصفر وده كان سبب إن الباركود
            // بيتطبع شكله لكن مفيش قارئ بيقدر يمسحه.
            int quietZone = narrow * 10;
            int availableWidth = Math.Max(1, bounds.Width - quietZone * 2);
            int totalWidth = ComputeWidth(narrow, wide);

            if (totalWidth > availableWidth && totalWidth > 0)
            {
                double scale = (double)availableWidth / totalWidth;
                // مينفعش عرض أضيق شرطة ينزل عن 2 - أقل من كده بيبقى رفيع أوي على طابعة
                // الملصقات الحرارية إنها تطبعه بوضوح، وده كمان بيبوّظ قراية القارئ
                narrow = Math.Max(2, (int)(narrow * scale));
                wide = narrow * 3;
                quietZone = narrow * 10;
                availableWidth = Math.Max(1, bounds.Width - quietZone * 2);
                totalWidth = ComputeWidth(narrow, wide);
            }

            // بنوسّط الباركود أفقيًا جوه الحدود (بعد حجز مسافة الأمان على الجانبين) بدل ما نبدأ
            // من أقصى الشمال - عشان كان بيبان مايل ناحية الشمال لو الباركود أضيق من المساحة المتاحة
            int x = bounds.Left + quietZone + Math.Max(0, (availableWidth - totalWidth) / 2);
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

        // بيرسم باركود Code128 (البيانات/الأنماط من ZXing.Net - مكتبة موثوقة، بدل
        // ما نطبّق المعيار يدويًا زي ما حصل مع QR القديم وطلع فيه خطأ) - أكثف بكتير
        // من Code39 لنفس المحتوى، فمناسب أكتر لقيم باركود المخزون اللي بتكون نص طويل.
        //
        // بنرسم كل "مودیول" (خط/فراغ) كمستطيل مباشر بدل ما نولّد Bitmap بحجم bounds
        // بالبكسل ونمططه - لو الباركود محتاج مثلاً 250+ مودیول وbounds.Width بس ~160،
        // كل مودیول كان بياخد أقل من بكسل واحد في الـBitmap الوسيط فيتقطع/يندمج مع
        // اللي جنبه قبل ما يوصل للطابعة أصلاً - أي طباعة عليه كانت هتفشل مهما كانت
        // دقة الطابعة الحقيقية عالية. الرسم المباشر بيسيب دقة الطباعة الفعلية هي
        // اللي تحدد وضوح الخطوط، مش دقة صورة وسيطة مصغّرة.
        public static void DrawCode128(Graphics g, string text, Rectangle bounds)
        {
            if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0) return;

            var writer = new ZXing.OneD.Code128Writer();
            ZXing.Common.BitMatrix matrix = writer.encode(text, BarcodeFormat.CODE_128, 1, 1);

            float cellWidth = (float)bounds.Width / matrix.Width;
            for (int col = 0; col < matrix.Width; col++)
            {
                if (!matrix[col, 0]) continue;
                float xLeft = bounds.Left + col * cellWidth;
                float xRight = bounds.Left + (col + 1) * cellWidth;
                g.FillRectangle(Brushes.Black, xLeft, bounds.Top, xRight - xLeft, bounds.Height);
            }
        }

        private static string FindLabelPrinterName()
        {
            foreach (string installedName in PrinterSettings.InstalledPrinters)
            {
                if (installedName.IndexOf("eml-400l", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    installedName.IndexOf("eml 400l", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return installedName;
                }
            }
            return null;
        }

        // بيطبع ملصق باركود واحد (باركود + اسم المنتج). بيستخدم أوامر TSPL خام بتتبعت
        // مباشرة لفيرموير الطابعة (بدل ما يعدّي عبر GDI/PrintDocument) - اتأكدنا بالتجربة
        // إن درايفر EML-400L بيحوّل صفحات GDI لأوامر طباعة غلط (المحتوى بيتزحلق ويتقطع)
        // رغم إن أرقام الصفحة اللي بيرجّعها لـ.NET سليمة، بينما نفس الطابعة لما بتطبع
        // اختبارها الداخلي (Selftest) بلغة TSPL بيطلع مظبوط تمامًا. فبنبعت TSPL بنفسنا
        // عشان نضمن نفس الدقة دي، بدل ما نعتمد على تحويل GDI بتاع الدرايفر المعطوب.
        //
        // TSPL بيتعامل مع نص خام بس (مفيش دعم Unicode/عربي مضمون من غير معرفة خط/كودبيدج
        // الفيرموير بالظبط) فلو اسم المنتج أو الباركود فيهم حروف مش ASCII، بنرجع تلقائيًا
        // لطريقة GDI القديمة (PrintBarcodeLabelGdi) بدل ما نطبع نص متقطّع أو فاضي.
        public static void PrintBarcodeLabel(string barcode, string productName, IWin32Window owner, int copies = 1)
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                MessageBox.Show("مفيش باركود لهذا المنتج.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            copies = Math.Max(1, copies);

            string labelPrinterName = FindLabelPrinterName();
            if (labelPrinterName == null)
            {
                MessageBox.Show("مش لاقي طابعة الباركود \"EML-400L\" في قائمة الطابعات المثبتة على الجهاز ده. تأكد إنها متوصلة ومركب الدرايفر بتاعها.", "الطابعة غير موجودة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!IsAsciiOnly(barcode) || !IsAsciiOnly(productName))
            {
                PrintBarcodeLabelGdi(barcode, productName, owner, labelPrinterName, copies);
                return;
            }

            if (MessageBox.Show($"طباعة {copies} نسخة ملصق للمنتج \"{productName}\"؟", "تأكيد الطباعة", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            string tspl = BuildTsplLabel(barcode, productName, copies);
            bool ok = TsplPrinter.SendCommand(labelPrinterName, tspl, "Temo Barcode Label", out string sendError);
            if (!ok)
                MessageBox.Show($"حصل خطأ أثناء إرسال الملصق للطابعة:\n{sendError}", "خطأ طباعة", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static bool IsAsciiOnly(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            foreach (char c in s)
                if (c > 127) return false;
            return true;
        }

        // بيبني أوامر TSPL لملصق 45×23مم على دقة 203dpi (~8 نقطة/مم - اتأكدنا منها
        // فعليًا عن طريق PrinterResolutions بتاعة الطابعة). بنحاذي المحتوى شمال بهامش
        // بسيط بدل ما نتوسّط - نفس أسلوب الـSelftest اللي طبعته الطابعة نفسها ومظبوط
        // 100%، بدل ما نخاطر بحسابات توسيط تقريبية لعرض الباركود مش مضمونة مع TSPL.
        //
        // سُمك خط الباركود (narrow module) وحجم خط اسم المنتج بيتحسبوا ديناميكيًا حسب
        // طول المحتوى - لو ثابتين على رقم كبير، أي قيمة باركود متوسطة الطول بتطلع أعرض
        // من الملصق (45مم) فعليًا وبتتقطع من الحافة، وده معناه إن جزء من نهاية الباركود
        // (وفيه علامة النهاية الأساسية للترميز) بيضيع فالسكانر مش بيلاقي باركود صالح
        // يقراه خالص - مش مجرد مشكلة شكل. فبنقلل السُمك تلقائيًا لحد ما الباركود كله
        // يفضل جوه حدود الملصق، وبنكبّره لحد أقصى معقول لو المحتوى قصير عشان يتقرا أوضح.
        private static string BuildTsplLabel(string barcode, string productName, int copies)
        {
            const double dotsPerMm = 203.0 / 25.4;
            int labelWidthDots = (int)Math.Round(45 * dotsPerMm);
            int marginDots = (int)Math.Round(1.0 * dotsPerMm);
            int availableWidthDots = labelWidthDots - 2 * marginDots;

            int nameTop = (int)Math.Round(1.5 * dotsPerMm);
            int barcodeTop = (int)Math.Round(6.5 * dotsPerMm);
            int barcodeHeight = (int)Math.Round(10 * dotsPerMm);
            int textTop = (int)Math.Round(17.5 * dotsPerMm);

            string safeName = SanitizeForTspl(productName);
            string safeBarcode = SanitizeForTspl(barcode);

            // تقدير عدد "مودیولات" Code128 اللي القيمة هتحتاجها: كل حرف بيانات ~11 مودیول،
            // زائد كود البداية + خانة التحقق (11 لكل واحد) + كود النهاية (13)، زائد مسافة
            // أمان (Quiet Zone) ~10 مودیول على كل جانب.
            //
            // مهم: مفيش حد أدنى مفروض هنا غير 1 - لو فرضنا حد أدنى (زي 2) بغض النظر عن طول
            // القيمة، وطول القيمة يحتاج مساحة أكبر من المتاحة، الباركود هيتقطع من حافة
            // الملصق (بما فيها علامة النهاية) ويبقى غير قابل للقراءة خالص مهما كان وضوح
            // الخطوط - ده كان بالظبط سبب إن السكانر مش بيستجيب. فبنسيب narrow ينزل لحد 1
            // لو القيمة طويلة، عشان الأولوية إن الباركود كله يفضل جوه حدود الملصق.
            int estimatedModules = 11 * (Math.Max(1, safeBarcode.Length) + 2) + 13 + 20;
            int narrow = Math.Max(1, Math.Min(4, availableWidthDots / estimatedModules));

            // بنختار أكبر خط لاسم المنتج بيفضل جوه عرض الملصق - عرض الحرف التقريبي لكل
            // خط مدمج في الطابعة (بالنقطة): "2"=12، "1"=8. شلنا خط "3" (16 نقطة) من
            // الاختيار خالص - كان بيفضّل يتقارب من حافة الملصق بالظبط من غير هامش أمان،
            // فأي فرق بسيط في عرض الحرف الفعلي عن التقدير كان كافي إن آخر حرف يتقطع
            // (زي ما حصل مع "x" في "max"). وبناخد 90% بس من العرض المتاح كهامش أمان.
            string nameFont = "1";
            int safeAvailableWidth = (int)(availableWidthDots * 0.9);
            foreach (var (font, charWidth) in new (string, int)[] { ("2", 12), ("1", 8) })
            {
                if (charWidth * Math.Max(1, safeName.Length) <= safeAvailableWidth)
                {
                    nameFont = font;
                    break;
                }
            }

            var sb = new StringBuilder();
            sb.Append("SIZE 45 mm,23 mm\r\n");
            sb.Append("GAP 2 mm,0 mm\r\n");
            sb.Append("DIRECTION 0,0\r\n");
            sb.Append("REFERENCE 0,0\r\n");
            sb.Append("CLS\r\n");
            sb.Append($"TEXT {marginDots},{nameTop},\"{nameFont}\",0,1,1,\"{safeName}\"\r\n");
            sb.Append($"BARCODE {marginDots},{barcodeTop},\"128\",{barcodeHeight},0,0,{narrow},{narrow},\"{safeBarcode}\"\r\n");
            sb.Append($"TEXT {marginDots},{textTop},\"1\",0,1,1,\"{safeBarcode}\"\r\n");
            sb.Append($"PRINT 1,{copies}\r\n");
            return sb.ToString();
        }

        // بنشيل علامات التنصيص وأي أسطر جديدة عشان محتوى زي اسم منتج فيه " ما يبوّظش
        // بنية أمر TSPL نفسه أو يسمح بحقن أوامر إضافية جوه النص المطبوع.
        private static string SanitizeForTspl(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\"", "").Replace("\r", "").Replace("\n", "");
        }

        // الطريقة القديمة (GDI/PrintDocument) - لسه محتفظين بيها كحل احتياطي لأي محتوى
        // فيه حروف مش ASCII (زي أسماء منتجات بالعربي) لأن TSPL مش مضمون يعرضها صح.
        private static void PrintBarcodeLabelGdi(string barcode, string productName, IWin32Window owner, string labelPrinterName, int copies)
        {
            PrintDocument pd = new PrintDocument();
            pd.PrinterSettings.PrinterName = labelPrinterName;
            pd.PrinterSettings.Copies = (short)Math.Min(copies, short.MaxValue);

            PaperSize matchedDriverSize = null;
            foreach (PaperSize ps in pd.PrinterSettings.PaperSizes)
            {
                if (Math.Abs(ps.Width - 177) <= 5 && Math.Abs(ps.Height - 91) <= 5)
                {
                    matchedDriverSize = ps;
                    break;
                }
            }
            pd.DefaultPageSettings.PaperSize = matchedDriverSize ?? new PaperSize("BarcodeLabel45x23", 177, 91);
            pd.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
            pd.OriginAtMargins = true;

            pd.PrintPage += (s, e) =>
            {
                Graphics g = e.Graphics;
                float pageWidth = e.MarginBounds.Width;
                float pageHeight = e.MarginBounds.Height;
                StringFormat center = new StringFormat { Alignment = StringAlignment.Center };

                float fontScale = Math.Max(0.5f, Math.Min(1.3f, pageHeight / 91f));
                float marginX = Math.Max(2f, pageWidth * 0.035f);
                float nameHeight = pageHeight * 0.16f;
                float textHeight = pageHeight * 0.14f;
                float gap = pageHeight * 0.02f;
                float barcodeTop = nameHeight + gap;
                float barcodeHeight = Math.Max(1f, pageHeight - nameHeight - textHeight - gap * 2);

                using Font nameFont = new Font("Arial", Math.Max(5f, 10 * fontScale), FontStyle.Bold);
                using Font textFont = new Font("Consolas", Math.Max(5f, 9 * fontScale));

                g.DrawString(productName ?? "", nameFont, Brushes.Black, new RectangleF(0, 0, pageWidth, nameHeight), center);

                Rectangle barcodeBounds = new Rectangle((int)marginX, (int)barcodeTop, (int)(pageWidth - marginX * 2), (int)barcodeHeight);
                DrawCode128(g, barcode, barcodeBounds);

                g.DrawString(barcode, textFont, Brushes.Black, new RectangleF(0, barcodeTop + barcodeHeight + gap, pageWidth, textHeight), center);
            };

            PrintPreviewDialog preview = new PrintPreviewDialog { Document = pd, Width = 500, Height = 500 };
            preview.ShowDialog(owner);
        }
    }
}
