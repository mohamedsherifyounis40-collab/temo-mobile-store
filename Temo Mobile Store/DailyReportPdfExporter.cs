using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // DailyReportPdfExporter: بيحوّل صفحة HTML (من DailyReportHtmlBuilder) لملف PDF
    // حقيقي أو نافذة طباعة، عن طريق WebView2 مباشرة (نفس المحرك اللي البرنامج كله
    // مبني عليه أصلًا كـ Blazor Hybrid) - مش GDI+ يدوي زي الإيصالات، عشان التصميم هنا
    // (كروت/جداول/ألوان) HTML/CSS حقيقي ومطابق للشاشات بالظبط.
    //
    // بيشتغل على WebView2 مستقل مستضاف في Form مخفي خصيصًا للتصدير (مش أي BlazorWebView
    // من شاشات البرنامج) - بيتقفل تلقائيًا بعد ما يخلص، عشان محتاج instance جديد يقدر
    // يعمل NavigateToString بمحتوى ثابت من غير ما يتصادم مع أي شاشة تانية شغالة.
    // ==========================================================================
    public static class DailyReportPdfExporter
    {
        public static async Task<(bool Success, string? FilePath, string? Error)> SaveAsPdfAsync(string html)
        {
            using var host = new HiddenWebViewHost();
            try
            {
                bool navigated = await host.NavigateAsync(html);
                if (!navigated)
                    return (false, null, "فشل تجهيز التقرير للتصدير.");

                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TemoStore_Invoices");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                string filePath = Path.Combine(folder, $"تقرير_يومي_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.pdf");

                var printSettings = host.WebView.CoreWebView2!.Environment.CreatePrintSettings();
                printSettings.ShouldPrintBackgrounds = true;
                printSettings.ShouldPrintHeaderAndFooter = false;

                bool saved = await host.WebView.CoreWebView2.PrintToPdfAsync(filePath, printSettings);
                if (!saved)
                    return (false, null, "فشل حفظ ملف الـ PDF.");

                return (true, filePath, null);
            }
            catch (Exception ex)
            {
                return (false, null, "حصل خطأ أثناء تصدير التقرير: " + ex.Message);
            }
        }

        public static async Task<string?> PrintAsync(string html)
        {
            using var host = new HiddenWebViewHost();
            try
            {
                bool navigated = await host.NavigateAsync(html);
                if (!navigated)
                    return "فشل تجهيز التقرير للطباعة.";

                host.WebView.CoreWebView2!.ShowPrintUI(CoreWebView2PrintDialogKind.System);

                // نافذة الطباعة نفسها بتفضل شغالة عن طريق ويندوز حتى بعد ما الدالة دي ترجع،
                // فمحتاجين نستنى شوية قبل ما نقفل الـ Form المخفي (لو قفلناه على طول ممكن
                // الطباعة تتقطع لسه بتحمّل)
                await Task.Delay(3000);
                return null;
            }
            catch (Exception ex)
            {
                return "حصل خطأ أثناء فتح نافذة الطباعة: " + ex.Message;
            }
        }

        private sealed class HiddenWebViewHost : IDisposable
        {
            private readonly Form _form;
            public WebView2 WebView { get; }

            public HiddenWebViewHost()
            {
                WebView = new WebView2 { Dock = DockStyle.Fill };
                _form = new Form
                {
                    FormBorderStyle = FormBorderStyle.None,
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new System.Drawing.Point(-3000, -3000),
                    Size = new System.Drawing.Size(950, 1400)
                };
                _form.Controls.Add(WebView);
                _form.Show();
            }

            public async Task<bool> NavigateAsync(string html)
            {
                await WebView.EnsureCoreWebView2Async(null);

                var tcs = new TaskCompletionSource<bool>();
                void Handler(object? s, CoreWebView2NavigationCompletedEventArgs e)
                {
                    WebView.CoreWebView2!.NavigationCompleted -= Handler;
                    tcs.TrySetResult(e.IsSuccess);
                }
                WebView.CoreWebView2!.NavigationCompleted += Handler;
                WebView.CoreWebView2.NavigateToString(html);
                return await tcs.Task;
            }

            public void Dispose()
            {
                _form.Close();
                _form.Dispose();
            }
        }
    }
}
