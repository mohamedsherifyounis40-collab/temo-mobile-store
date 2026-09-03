using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // ReceiptPrintHelper: طباعة/معاينة/تصدير PDF لإيصال البيع. نفس منطق الرسم اللي
    // كان جوه SalesPageControl قبل ما الشاشة تتنقل لـ Blazor Hybrid - منقول هنا حرفيًا
    // كـ helper مستقل عشان SalesPage.razor يقدر يستدعيه من غير ما يحمل تفاصيل الرسم.
    // بيانات المحل (اسم/عنوان/تليفون/شعار) بتتقرا مرة لكل طباعة بدل ما تتخزن مؤقتًا،
    // عشان أي تعديل في إعدادات المحل يظهر فورًا في أول إيصال بعده.
    // ==========================================================================
    public static class ReceiptPrintHelper
    {
        public static PaperSize GetPaperSize(int paperSizeIndex)
        {
            switch (paperSizeIndex)
            {
                case 1: // 58 مم
                    return new PaperSize("Thermal58", 228, 1100);
                case 2: // A4
                    return new PaperSize("A4", 827, 1169);
                default: // 80 مم (الافتراضي)
                    return new PaperSize("Thermal80", 315, 1100);
            }
        }

        public static void PrintInvoice(List<ReceiptData> items, int paperSizeIndex)
        {
            PrintDocument pd = new PrintDocument();
            pd.PrintPage += (s, ev) => RenderReceiptPage(ev, items);
            pd.DefaultPageSettings.PaperSize = BuildReceiptPaperSize(paperSizeIndex, items);

            PrintPreviewDialog pdd = new PrintPreviewDialog() { Document = pd };
            pdd.ShowDialog();
        }

        // بيحفظ آخر فاتورة كملف PDF مباشرة (باستخدام Microsoft Print to PDF المدمجة في ويندوز)
        public static bool SaveInvoicePdf(List<ReceiptData> items, int paperSizeIndex, out string filePath, out string error)
        {
            filePath = null;
            error = null;

            bool hasPdfPrinter = false;
            foreach (string printerName in PrinterSettings.InstalledPrinters)
            {
                if (printerName.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasPdfPrinter = true;
                    break;
                }
            }

            if (!hasPdfPrinter)
            {
                error = "مش لاقي طابعة \"Microsoft Print to PDF\" على الجهاز ده. تأكد إنها مفعّلة من إعدادات الطابعات في ويندوز.";
                return false;
            }

            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TemoStore_Invoices");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string fileName = $"فاتورة_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.pdf";
                filePath = Path.Combine(folder, fileName);

                PrintDocument pd = new PrintDocument();
                pd.PrintPage += (s, ev) => RenderReceiptPage(ev, items);
                pd.DefaultPageSettings.PaperSize = BuildReceiptPaperSize(paperSizeIndex, items);
                pd.PrinterSettings.PrinterName = "Microsoft Print to PDF";
                pd.PrinterSettings.PrintToFile = true;
                pd.PrinterSettings.PrintFileName = filePath;

                pd.Print();
                return true;
            }
            catch (Exception ex)
            {
                error = "حصل خطأ أثناء حفظ الـ PDF: " + ex.Message;
                filePath = null;
                return false;
            }
        }

        // بيبني رسالة تهنئة/إشعار الفاتورة وبيبعتها لواتساب العميل (بيفتح واتساب ويب/ديسكتوب
        // بالرسالة جاهزة - مفيش إرسال تلقائي من غير تفاعل المستخدم، زي WhatsAppHelper العادي)
        public static void SendInvoiceWhatsApp(string customerPhone, string customerName, int dailyInvoiceNumber, string saleDate, decimal total, IWin32Window owner)
        {
            string greetingName = string.IsNullOrWhiteSpace(customerName) ? "عميلنا العزيز" : customerName;
            string displayDate = DateTime.TryParse(saleDate, out DateTime parsedDate) ? parsedDate.ToString("yyyy-MM-dd") : saleDate;
            string separatorLine = "------------------------";

            UIHelpers.LoadStoreSettings(out string storeName, out _, out _, out _, out _);
            string brandName = string.IsNullOrWhiteSpace(storeName) ? "Temo Mobile Store" : storeName;

            string message = $"مرحبًا أستاذ/ة {greetingName}\n" +
                              $"نشكرك على ثقتك في {brandName}\n" +
                              $"تم إصدار فاتورتك بنجاح.\n" +
                              $"{separatorLine}\n" +
                              $"رقم الفاتورة: {dailyInvoiceNumber}\n" +
                              $"التاريخ: {displayDate}\n" +
                              $"الإجمالي: {total:N2} جنيه\n" +
                              $"{separatorLine}\n" +
                              $"إذا كان لديك أي استفسار أو احتجت إلى دعم أو خدمة ما بعد البيع، يسعدنا التواصل معك في أي وقت.\n\n" +
                              $"شكرًا لاختيارك {brandName}، ونتطلع لخدمتك مرة أخرى.";

            WhatsAppHelper.SendMessage(customerPhone, message, owner);
        }

        // بيبني مقاس الورقة، وللطابعات الحرارية (80/58مم) بيحسب الارتفاع المطلوب فعليًا حسب
        // محتوى الفاتورة (مفيش IMEI؟ عميل نقدي؟ فمفيش أسطر فاضية) بدل ارتفاع ثابت ضخم بيسيب
        // مسافة بيضا في آخر كل إيصال
        private static PaperSize BuildReceiptPaperSize(int paperSizeIndex, List<ReceiptData> items)
        {
            PaperSize paperSize = GetPaperSize(paperSizeIndex);
            if (paperSize.Width < 500)
            {
                using (var tempBmp = new Bitmap(1, 1))
                using (var measureG = Graphics.FromImage(tempBmp))
                {
                    float contentHeight = RenderReceipt(measureG, paperSize.Width, true, items);
                    paperSize.Height = (int)Math.Ceiling(contentHeight) + 20;
                }
            }
            return paperSize;
        }

        private static void RenderReceiptPage(PrintPageEventArgs e, List<ReceiptData> items)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            RenderReceipt(g, e.PageBounds.Width, false, items);
        }

        // ==========================================================================
        // رسم/قياس الإيصال - نفس الدالة تستخدم مرتين: مرة "قياس بس" (measureOnly=true،
        // بترجع الارتفاع المطلوب من غير ما ترسم حاجة فعليًا) قبل الطباعة عشان نظبط مقاس
        // الورقة، ومرة تانية "رسم فعلي" وقت الطباعة الحقيقية - بنفس المنطق بالظبط، فمفيش
        // احتمال إن القياس يختلف عن الرسم الفعلي. items ممكن تحتوي أكتر من صنف لو الفاتورة
        // فيها أكتر من منتج - بيانات الفاتورة المشتركة (رقمها/تاريخها/العميل) بتتاخد من أول عنصر.
        // ==========================================================================
        private static float RenderReceipt(Graphics g, float pageWidth, bool measureOnly, List<ReceiptData> items)
        {
            UIHelpers.LoadStoreSettings(out string currentStoreName, out string currentStorePhone, out string currentStoreAddress, out byte[] currentStoreLogo, out string currentStoreWhatsApp);

            ReceiptData receipt = items[0];
            bool isThermal = pageWidth < 500;
            float margin = isThermal ? 14 : 24;
            float contentWidth = pageWidth - margin * 2;

            Color colorPrimary = UIHelpers.ColorPrimary;
            Color colorAccent = UIHelpers.ColorSuccess;
            Color colorWarning = UIHelpers.ColorWarning;
            Color colorMuted = Color.FromArgb(120, 126, 138);
            Color colorLightBg = Color.FromArgb(238, 242, 248);

            using var fontBrand = new Font("Arial", isThermal ? 15 : 20, FontStyle.Bold);
            using var fontTagline = new Font("Arial", isThermal ? 8 : 10, FontStyle.Regular);
            using var fontLabelBold = new Font("Arial", isThermal ? 10 : 12, FontStyle.Bold);
            using var fontBody = new Font("Arial", isThermal ? 9 : 11, FontStyle.Regular);
            using var fontBodyBold = new Font("Arial", isThermal ? 9 : 11, FontStyle.Bold);
            using var fontSmall = new Font("Arial", isThermal ? 7.5f : 9, FontStyle.Regular);
            using var fontItemName = new Font("Arial", isThermal ? 10.5f : 13, FontStyle.Bold);
            using var fontTotalValue = new Font("Arial", isThermal ? 15 : 19, FontStyle.Bold);
            using var fontSectionHeader = new Font("Arial", isThermal ? 9.5f : 11.5f, FontStyle.Bold);

            var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            var farAlign = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
            var nearAlign = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

            float yPos = 0;

            void FillRect(RectangleF rect, Color color) { if (!measureOnly) using (Brush b = new SolidBrush(color)) g.FillRectangle(b, rect); }
            void FillRounded(RectangleF rect, Color color, float radius) { if (!measureOnly) using (Brush b = new SolidBrush(color)) using (var path = RoundedRect(rect, radius)) g.FillPath(b, path); }
            void BorderRounded(RectangleF rect, Color color, float radius, float width) { if (!measureOnly) using (Pen p = new Pen(color, width)) using (var path = RoundedRect(rect, radius)) g.DrawPath(p, path); }
            void Text(string s, Font f, Color color, RectangleF rect, StringFormat fmt) { if (!measureOnly) using (Brush b = new SolidBrush(color)) g.DrawString(s ?? "", f, b, rect, fmt); }
            void Dashed(float y) { if (!measureOnly) DrawDashedLine(g, margin, pageWidth - margin, y); }

            // 1) شريط علوي (لمسة براند)
            FillRect(new RectangleF(0, yPos, pageWidth, isThermal ? 4 : 6), colorPrimary);
            yPos += (isThermal ? 4 : 6) + (isThermal ? 10 : 14);

            // 2) الشعار (من إعدادات المحل - نفس الشعار المستخدم في باقي البرنامج)
            if (currentStoreLogo != null && currentStoreLogo.Length > 0)
            {
                float logoSize = isThermal ? 48 : 64;
                if (!measureOnly)
                {
                    using (var ms = new MemoryStream(currentStoreLogo))
                    using (var logoImg = Image.FromStream(ms))
                        g.DrawImage(logoImg, (pageWidth - logoSize) / 2, yPos, logoSize, logoSize);
                }
                yPos += logoSize + (isThermal ? 8 : 12);
            }

            // 3) اسم المحل
            string brandName = string.IsNullOrWhiteSpace(currentStoreName) ? "Temo Mobile Store" : currentStoreName;
            Text(brandName, fontBrand, colorPrimary, new RectangleF(0, yPos, pageWidth, isThermal ? 24 : 30), center);
            yPos += isThermal ? 24 : 30;

            Text("هواتف وإكسسوارات موبايل", fontTagline, colorMuted, new RectangleF(0, yPos, pageWidth, isThermal ? 16 : 20), center);
            yPos += isThermal ? 18 : 22;

            // 4) العنوان
            if (!string.IsNullOrWhiteSpace(currentStoreAddress))
            {
                Text(currentStoreAddress, fontSmall, colorMuted, new RectangleF(margin, yPos, contentWidth, isThermal ? 14 : 18), center);
                yPos += isThermal ? 15 : 19;
            }

            // 5) تليفون / واتساب (سطر واحد لو الاتنين موجودين)
            string phoneLine = "";
            if (!string.IsNullOrWhiteSpace(currentStorePhone)) phoneLine += $"تليفون: {currentStorePhone}";
            if (!string.IsNullOrWhiteSpace(currentStoreWhatsApp))
                phoneLine += (phoneLine.Length > 0 ? "   |   " : "") + $"واتساب: {currentStoreWhatsApp}";
            if (phoneLine.Length > 0)
            {
                Text(phoneLine, fontSmall, colorMuted, new RectangleF(margin, yPos, contentWidth, isThermal ? 14 : 18), center);
                yPos += isThermal ? 16 : 20;
            }

            yPos += isThermal ? 6 : 8;
            Dashed(yPos);
            yPos += isThermal ? 12 : 16;

            // 6) بيانات الفاتورة
            string invoiceCode = $"INV-{receipt.SaleId:D6}";
            Text($"رقم الفاتورة:  #{receipt.DailyInvoiceNumber}", fontBodyBold, colorPrimary, new RectangleF(margin, yPos, contentWidth, isThermal ? 16 : 20), nearAlign);
            yPos += isThermal ? 18 : 22;

            string dateDisplay = DateTime.TryParse(receipt.SaleDate, out DateTime saleDt) ? saleDt.ToString("yyyy-MM-dd  hh:mm tt") : receipt.SaleDate;

            void InfoLine(string label, string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                Text($"{label}:  {value}", fontBody, Color.Black, new RectangleF(margin, yPos, contentWidth, isThermal ? 16 : 20), nearAlign);
                yPos += isThermal ? 18 : 22;
            }

            InfoLine("التاريخ والوقت", dateDisplay);
            InfoLine("الكاشير", receipt.CashierName);
            InfoLine("العميل", string.IsNullOrWhiteSpace(receipt.CustomerName) ? "عميل نقدي" : receipt.CustomerName);
            InfoLine("هاتف العميل", receipt.CustomerPhone);
            InfoLine("ملاحظات", receipt.Notes);

            yPos += isThermal ? 4 : 6;
            Dashed(yPos);
            yPos += isThermal ? 12 : 16;

            // 7) عنوان قسم الأصناف
            float sectionBarH = isThermal ? 22 : 28;
            FillRect(new RectangleF(margin, yPos, contentWidth, sectionBarH), colorPrimary);
            Text("تفاصيل الصنف", fontSectionHeader, Color.White, new RectangleF(margin, yPos, contentWidth, sectionBarH), center);
            yPos += sectionBarH + (isThermal ? 10 : 14);

            // 8) كارت الصنف - بيتكرر لكل صنف في الفاتورة
            foreach (var item in items)
            {
                float iconSize = isThermal ? 34 : 44;
                if (!measureOnly) DrawDeviceIcon(g, new RectangleF(margin, yPos, iconSize, iconSize), colorPrimary);

                float textX = margin + iconSize + (isThermal ? 8 : 12);
                float textW = contentWidth - iconSize - (isThermal ? 8 : 12);
                float itemTop = yPos;

                Text(item.ProductName, fontItemName, Color.Black, new RectangleF(textX, yPos, textW, isThermal ? 16 : 20), nearAlign);
                yPos += isThermal ? 17 : 21;

                if (!string.IsNullOrWhiteSpace(item.Barcode))
                {
                    Text($"باركود: {item.Barcode}", fontSmall, colorMuted, new RectangleF(textX, yPos, textW, isThermal ? 13 : 16), nearAlign);
                    yPos += isThermal ? 14 : 17;
                }
                if (!string.IsNullOrWhiteSpace(item.IMEI))
                {
                    Text($"IMEI/سيريال: {item.IMEI}", fontSmall, colorMuted, new RectangleF(textX, yPos, textW, isThermal ? 13 : 16), nearAlign);
                    yPos += isThermal ? 14 : 17;
                }

                Text($"{item.Quantity} × {item.UnitPrice:N2}  =  {item.Total:N2} ج.م", fontBodyBold, colorPrimary, new RectangleF(textX, yPos, textW, isThermal ? 16 : 20), nearAlign);
                yPos += isThermal ? 18 : 22;

                yPos = Math.Max(yPos, itemTop + iconSize + (isThermal ? 6 : 8));
                yPos += isThermal ? 8 : 10;
            }

            yPos -= 4; // تعويض هامش آخر عنصر زيادة
            Dashed(yPos);
            yPos += isThermal ? 12 : 16;

            // 9) الإجماليات
            decimal subtotal = 0m;
            decimal discount = 0m;
            foreach (var item in items) { subtotal += item.Total; discount += item.Discount; }
            decimal grandTotal = subtotal - discount;

            void TotalLine(string label, decimal amount)
            {
                Text(label, fontBody, Color.Black, new RectangleF(margin, yPos, contentWidth / 2, isThermal ? 16 : 20), nearAlign);
                Text($"{amount:N2} ج.م", fontBody, Color.Black, new RectangleF(margin + contentWidth / 2, yPos, contentWidth / 2, isThermal ? 16 : 20), farAlign);
                yPos += isThermal ? 17 : 21;
            }
            TotalLine("الإجمالي الفرعي", subtotal);
            TotalLine("الخصم", discount);

            yPos += isThermal ? 2 : 4;
            if (!measureOnly) using (Pen p = new Pen(colorPrimary, 1.2f)) g.DrawLine(p, margin, yPos, pageWidth - margin, yPos);
            yPos += isThermal ? 8 : 10;

            float totalBoxH = isThermal ? 34 : 42;
            FillRounded(new RectangleF(margin, yPos, contentWidth, totalBoxH), Color.FromArgb(236, 247, 240), isThermal ? 8 : 10);
            Text("الإجمالي النهائي", fontBodyBold, colorAccent, new RectangleF(margin + 12, yPos, contentWidth / 2, totalBoxH), nearAlign);
            Text($"{grandTotal:N2} ج.م", fontTotalValue, colorAccent, new RectangleF(margin, yPos, contentWidth - 12, totalBoxH), farAlign);
            yPos += totalBoxH + (isThermal ? 12 : 16);

            // 10) صندوق وسيلة الدفع / المدفوع
            bool isCredit = receipt.PaymentType == "Credit";
            decimal paid = isCredit ? 0m : grandTotal;
            decimal remaining = isCredit ? grandTotal : 0m;
            string methodDisplay = isCredit ? "آجل" : (string.IsNullOrWhiteSpace(receipt.PaymentMethod) ? "نقدي" : receipt.PaymentMethod);

            float payBoxH = isThermal ? 46 : 58;
            RectangleF payBox = new RectangleF(margin, yPos, contentWidth, payBoxH);
            FillRounded(payBox, colorLightBg, isThermal ? 8 : 10);
            BorderRounded(payBox, Color.FromArgb(220, 226, 236), isThermal ? 8 : 10, 1f);
            if (!measureOnly) using (Pen p = new Pen(Color.FromArgb(220, 226, 236), 1f)) g.DrawLine(p, margin + contentWidth / 2, yPos + 8, margin + contentWidth / 2, yPos + payBoxH - 8);

            RectangleF methodCol = new RectangleF(margin, yPos, contentWidth / 2, payBoxH);
            RectangleF paidCol = new RectangleF(margin + contentWidth / 2, yPos, contentWidth / 2, payBoxH);
            Text("طريقة الدفع", fontSmall, colorMuted, new RectangleF(methodCol.X, methodCol.Y + 6, methodCol.Width, isThermal ? 13 : 16), center);
            Text(methodDisplay, fontBodyBold, Color.Black, new RectangleF(methodCol.X, methodCol.Y + payBoxH - (isThermal ? 22 : 26), methodCol.Width, isThermal ? 18 : 22), center);
            Text("المدفوع", fontSmall, colorMuted, new RectangleF(paidCol.X, paidCol.Y + 6, paidCol.Width, isThermal ? 13 : 16), center);
            Text($"{paid:N2} ج.م", fontBodyBold, Color.Black, new RectangleF(paidCol.X, paidCol.Y + payBoxH - (isThermal ? 22 : 26), paidCol.Width, isThermal ? 18 : 22), center);
            yPos += payBoxH + (isThermal ? 8 : 10);

            if (remaining > 0)
            {
                Text($"المتبقي: {remaining:N2} ج.م", fontBodyBold, colorWarning, new RectangleF(0, yPos, pageWidth, isThermal ? 18 : 22), center);
                yPos += isThermal ? 20 : 24;
            }

            yPos += isThermal ? 4 : 6;
            Dashed(yPos);
            yPos += isThermal ? 14 : 18;

            // 11) QR + باركود جنبًا لجنب
            float halfW = contentWidth / 2;
            float codeSize = isThermal ? 64 : 80;

            Text("امسح لعرض الفاتورة", fontSmall, colorMuted, new RectangleF(margin, yPos, halfW, isThermal ? 13 : 16), center);
            if (!measureOnly)
            {
                string qrPayload = $"{brandName}\n{invoiceCode}\n{dateDisplay}\n{receipt.ProductName}\nEGP {grandTotal:N2}";
                RectangleF qrRect = new RectangleF(margin + halfW / 2 - codeSize / 2, yPos + 16, codeSize, codeSize);
                QrCodeHelper.DrawQrCode(g, qrPayload, Rectangle.Round(qrRect));
            }

            Text("باركود الفاتورة", fontSmall, colorMuted, new RectangleF(margin + halfW, yPos, halfW, isThermal ? 13 : 16), center);
            float barcodeH = isThermal ? 38 : 48;
            if (!measureOnly)
            {
                RectangleF barcodeRect = new RectangleF(margin + halfW + 12, yPos + 20, halfW - 24, barcodeH);
                BarcodeHelper.DrawCode39(g, invoiceCode, Rectangle.Round(barcodeRect));
            }
            Text(invoiceCode, fontSmall, Color.Black, new RectangleF(margin + halfW, yPos + 20 + barcodeH + 2, halfW, isThermal ? 13 : 16), center);

            yPos += 16 + codeSize + (isThermal ? 6 : 8);
            Dashed(yPos);
            yPos += isThermal ? 14 : 18;

            // 12) خاتمة الشكر
            Text("♥ شكرًا لتعاملكم معنا ♥", fontLabelBold, colorPrimary, new RectangleF(0, yPos, pageWidth, isThermal ? 18 : 22), center);
            yPos += isThermal ? 20 : 24;
            Text($"نتمنى لكم يومًا سعيدًا مع {brandName}", fontSmall, colorMuted, new RectangleF(0, yPos, pageWidth, isThermal ? 16 : 20), center);
            yPos += isThermal ? 18 : 22;

            yPos += isThermal ? 6 : 8;
            FillRect(new RectangleF(0, yPos, pageWidth, isThermal ? 4 : 6), colorPrimary);
            yPos += isThermal ? 4 : 6;

            return yPos;
        }

        // بيرسم أيقونة جهاز عامة (زخرفية بس - مفيش عمود صور منتجات في قاعدة البيانات
        // فمينفعش نعرض صورة المنتج الحقيقية، وأولى من عرض صورة وهمية)
        private static void DrawDeviceIcon(Graphics g, RectangleF bounds, Color color)
        {
            using (Pen pen = new Pen(color, 1.8f))
            using (var path = RoundedRect(bounds, bounds.Width * 0.22f))
                g.DrawPath(pen, path);

            float dotR = bounds.Width * 0.1f;
            using (Brush b = new SolidBrush(color))
                g.FillEllipse(b, bounds.X + bounds.Width / 2 - dotR / 2, bounds.Bottom - bounds.Height * 0.2f - dotR / 2, dotR, dotR);
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // بيرسم خط منقط أفقي - مستخدم كفواصل شيك بدل الخط العادي
        private static void DrawDashedLine(Graphics g, float x1, float x2, float y)
        {
            using (Pen dashedPen = new Pen(Color.FromArgb(190, 195, 205), 1f))
            {
                dashedPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawLine(dashedPen, x1, y, x2, y);
            }
        }
    }
}
