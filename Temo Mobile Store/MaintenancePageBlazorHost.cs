using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Temo_Mobile_Store.BlazorComponents;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // MaintenancePageBlazorHost: تجربة شاشة صيانة جديدة بـ HTML/CSS حقيقي (Blazor Hybrid
    // عن طريق WebView2) - نفس نمط InventoryPageBlazorHost بالظبط. الشاشة القديمة
    // (MaintenancePageControl) منفصلة تمامًا ومتأثرتش.
    // ==========================================================================
    public partial class MaintenancePageBlazorHost : UserControl
    {
        public MaintenancePageBlazorHost()
        {
            this.Dock = DockStyle.Fill;

            var services = new ServiceCollection();
            services.AddWindowsFormsBlazorWebView();

            var blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index-maintenance.html",
                Services = services.BuildServiceProvider()
            };
            blazorWebView.RootComponents.Add<MaintenanceApp>("#app");

            this.Controls.Add(blazorWebView);
        }
    }
}
