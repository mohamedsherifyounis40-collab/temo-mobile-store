using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Temo_Mobile_Store.BlazorComponents;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // ReportsPageBlazorHost: تجربة شاشة التقارير الجديدة بـ HTML/CSS حقيقي
    // (Blazor Hybrid عن طريق WebView2) - نفس نمط AttendancePageBlazorHost بالظبط.
    // الشاشة القديمة (ReportsPageControl) منفصلة تمامًا ومتأثرتش.
    // ==========================================================================
    public partial class ReportsPageBlazorHost : UserControl
    {
        public ReportsPageBlazorHost()
        {
            this.Dock = DockStyle.Fill;

            var services = new ServiceCollection();
            services.AddWindowsFormsBlazorWebView();

            var blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index-reports.html",
                Services = services.BuildServiceProvider()
            };
            blazorWebView.RootComponents.Add<ReportsApp>("#app");

            this.Controls.Add(blazorWebView);
        }
    }
}
