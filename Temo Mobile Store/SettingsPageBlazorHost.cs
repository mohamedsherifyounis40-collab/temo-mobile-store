using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Temo_Mobile_Store.BlazorComponents;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // SettingsPageBlazorHost: تجربة شاشة الإعدادات الجديدة بـ HTML/CSS حقيقي
    // (Blazor Hybrid عن طريق WebView2) - نفس نمط AttendancePageBlazorHost بالظبط.
    // الشاشة القديمة (SettingsPageControl) منفصلة تمامًا ومتأثرتش.
    // ==========================================================================
    public partial class SettingsPageBlazorHost : UserControl
    {
        public SettingsPageBlazorHost()
        {
            this.Dock = DockStyle.Fill;

            var services = new ServiceCollection();
            services.AddWindowsFormsBlazorWebView();

            var blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index-settings.html",
                Services = services.BuildServiceProvider()
            };
            blazorWebView.RootComponents.Add<SettingsApp>("#app");

            this.Controls.Add(blazorWebView);
        }
    }
}
