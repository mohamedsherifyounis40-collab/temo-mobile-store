using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // TsplPrinter: بيبعت أوامر TSPL خام (Raw) مباشرة لطابعة الملصقات عن طريق
    // WinSpool، من غير ما يعدي على GDI/PrintDocument خالص. اتعمل بعد ما ثبتنا
    // إن درايفر EML-400L بيرجّع أرقام صفحة سليمة لـ.NET (PageBounds/MarginBounds
    // مظبوطين) لكن لما فعليًا بيحوّل صفحة GDI لأوامر طباعة بيغلط ويطبع المحتوى
    // مزحلق ومقطوع - ده تأكد بالمقارنة مع الـ Selftest اللي الطابعة بتطبعه من
    // فيرموير نفسها (بلغة TSPL) واللي طلع مظبوط تمامًا 100%. فبنستخدم نفس اللغة
    // (TSPL) اللي أثبتت إنها بتشتغل صح على الهاردوير ده بدل ما نعتمد على تحويل
    // GDI بتاع الدرايفر المعطوب.
    //
    // بنستخدم صراحة نقاط الدخول "A" (ANSI - OpenPrinterA وStartDocPrinterA) مع
    // ExactSpelling=true بدل CharSet.Auto - لو سبنا CharSet.Auto، .NET الحديث
    // بيحل الاستدعاء لنسخة "W" (Unicode) تلقائيًا، لكن structure الـ DOCINFOA
    // بتاعتنا بتمرر نصوصها كـ ANSI (LPStr) صراحة. الفرق ده كان بيبوّظ البيانات
    // اللي بتوصل للـ Spooler (اسم المستند/نوع البيانات) ويخلي StartDocPrinter/
    // WritePrinter يفشلوا - فبنثبّت النوعين (نقطة الدخول + الـ marshaling) على
    // ANSI مع بعض عشان يتطابقوا، بدل ما نسيب .NET يخمّن.
    // ==========================================================================
    public static class TsplPrinter
    {
        [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern int StartDocPrinter(IntPtr hPrinter, int level, [In] ref DOCINFOA pDocInfo);

        [DllImport("winspool.drv", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool WritePrinter(IntPtr hPrinter, byte[] pBytes, int dwCount, out int dwWritten);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
            [MarshalAs(UnmanagedType.LPStr)] public string pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)] public string pDataType;
        }

        // بيبعت بايتات خام لقايمة انتظار الطباعة (Print Queue) بتاعة الطابعة مباشرة -
        // ده بيعدي على الـ Spooler لكن بيتخطى أي رسم/تحويل GDI، فالدرايفر بيمرر
        // البيانات زي ما هي (Datatype=RAW) من غير ما يحاول "يفهمها" أو يعيد رسمها.
        public static bool SendRaw(string printerName, byte[] data, string jobName, out string error)
        {
            error = null;
            var docInfo = new DOCINFOA { pDocName = jobName, pOutputFile = null, pDataType = "RAW" };

            if (!OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero))
            {
                error = $"OpenPrinter فشلت: {new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message}";
                return false;
            }

            try
            {
                int jobId = StartDocPrinter(hPrinter, 1, ref docInfo);
                if (jobId == 0)
                {
                    error = $"StartDocPrinter فشلت: {new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message}";
                    return false;
                }

                try
                {
                    if (!StartPagePrinter(hPrinter))
                    {
                        error = $"StartPagePrinter فشلت: {new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message}";
                        return false;
                    }

                    try
                    {
                        if (!WritePrinter(hPrinter, data, data.Length, out int written) || written != data.Length)
                        {
                            error = $"WritePrinter فشلت: {new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message}";
                            return false;
                        }
                        return true;
                    }
                    finally
                    {
                        EndPagePrinter(hPrinter);
                    }
                }
                finally
                {
                    EndDocPrinter(hPrinter);
                }
            }
            finally
            {
                ClosePrinter(hPrinter);
            }
        }

        public static bool SendCommand(string printerName, string tsplCommand, string jobName, out string error)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(tsplCommand);
            return SendRaw(printerName, bytes, jobName, out error);
        }
    }
}
