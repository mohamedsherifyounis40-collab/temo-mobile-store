namespace Temo_Mobile_Store
{
    // ==========================================================================
    // CashDrawerHelper: بيبعت نبضة فتح الدرج لطابعة الفواتير المتوصل بيها الدرج
    // (الطريقة المعتادة: كابل RJ11 من الطابعة للدرج). بيستخدم نفس آلية الإرسال
    // الخام لقايمة الطباعة (WinSpool RAW) اللي TsplPrinter.SendRaw بيوفرها لطابعة
    // الملصقات - الفرق هنا إننا بنبعت أمر ESC/POS القياسي بدل TSPL.
    // ==========================================================================
    public static class CashDrawerHelper
    {
        // ESC p 0 25 250 (1B 70 00 19 FA) - أمر ESC/POS القياسي لفتح الدرج، شغال
        // على أغلب طابعات الفواتير الحرارية اللي بتدعم منفذ الدرج (Cash Drawer Kick-Out).
        private static readonly byte[] OpenDrawerCommand = { 0x1B, 0x70, 0x00, 0x19, 0xFA };

        public static bool OpenDrawer(string printerName, out string error)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                error = "لسه ماتحددتش طابعة الفواتير من الإعدادات - افتح ⚙️ الإعدادات وحدد الطابعة عشان اختصار F8 يشتغل.";
                return false;
            }

            return TsplPrinter.SendRaw(printerName, OpenDrawerCommand, "Temo Open Drawer", out error);
        }
    }
}
