using System;
using System.Drawing;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // QrCodeHelper: توليد ورسم QR Code لملصقات الأصناف (بديل Code39/Code128 لو
    // احتجنا لاحقًا سكانر 2D). بيستخدم مكتبة ZXing.Net بدل تطبيق يدوي للمعيار -
    // أول نسخة يدوية (Reed-Solomon + إخفاء أقنعة + توزيع كودوردز مبني يدويًا)
    // طلعت بترسم شكل يشبه QR لكن مش بيتقرا فعليًا (اتأكد بفك تشفيره ببرنامج اختبار
    // منفصل وفشل في كل الأحجام) - خطأ دقيق في مكان ما في التطبيق اليدوي للمعيار
    // (ISO/IEC 18004) صعب اكتشافه بالعين لأن الشكل العام (أنماط الزوايا الثلاثة)
    // بيطلع صح حتى لو المحتوى غلط.
    // ==========================================================================
    public static class QrCodeHelper
    {
        // بنرسم كل "مودیول" كمستطيل مباشر (مش صورة Bitmap وسيطة بحجم bounds بالبكسل
        // بيتم تمططها) عشان دقة الطباعة الفعلية هي اللي تحدد وضوح المربعات، مش دقة
        // صورة وسيطة ممكن تكون أصغر بكتير من مساحة الطباعة الحقيقية فتقطع تفاصيل QR
        // قبل ما توصل للطابعة أصلاً (نفس الغلطة اللي كانت في DrawCode128 الأول).
        public static void DrawQrCode(Graphics g, string text, Rectangle bounds)
        {
            if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0) return;

            BitMatrix matrix;
            try
            {
                var writer = new QRCodeWriter();
                matrix = writer.encode(text, BarcodeFormat.QR_CODE, 1, 1,
                    new System.Collections.Generic.Dictionary<EncodeHintType, object>
                    {
                        { EncodeHintType.ERROR_CORRECTION, ZXing.QrCode.Internal.ErrorCorrectionLevel.M }
                    });
            }
            catch { return; } // النص طويل جدًا أو فاضي - نتجاهل الرسم بهدوء بدل ما نكسر باقي الفاتورة

            int size = Math.Min(bounds.Width, bounds.Height);
            float moduleSize = (float)size / matrix.Width;
            float offsetX = bounds.Left + (bounds.Width - moduleSize * matrix.Width) / 2f;
            float offsetY = bounds.Top + (bounds.Height - moduleSize * matrix.Height) / 2f;

            using (Brush black = new SolidBrush(Color.Black))
            {
                for (int row = 0; row < matrix.Height; row++)
                    for (int col = 0; col < matrix.Width; col++)
                        if (matrix[col, row])
                            g.FillRectangle(black, offsetX + col * moduleSize, offsetY + row * moduleSize, moduleSize + 0.6f, moduleSize + 0.6f);
            }
        }
    }
}
