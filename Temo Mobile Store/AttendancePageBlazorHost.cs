using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Temo_Mobile_Store.BlazorComponents;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // AttendancePageBlazorHost: تجربة شاشة الحضور والمرتبات الجديدة بـ HTML/CSS حقيقي
    // (Blazor Hybrid عن طريق WebView2) - نفس نمط AccountsPageBlazorHost بالظبط.
    // الشاشة القديمة (AttendancePageControl) منفصلة تمامًا ومتأثرتش.
    // ==========================================================================
    public partial class AttendancePageBlazorHost : UserControl
    {
        public AttendancePageBlazorHost()
        {
            this.Dock = DockStyle.Fill;

            var services = new ServiceCollection();
            services.AddWindowsFormsBlazorWebView();

            var blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index-attendance.html",
                Services = services.BuildServiceProvider()
            };
            blazorWebView.RootComponents.Add<AttendanceApp>("#app");

            this.Controls.Add(blazorWebView);
        }
    }
}
