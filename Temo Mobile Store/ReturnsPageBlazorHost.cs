using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Temo_Mobile_Store.BlazorComponents;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // ReturnsPageBlazorHost: شاشة المرتجعات المستقلة (Blazor Hybrid عن طريق WebView2) -
    // نفس نمط SalesPageBlazorHost/MaintenancePageBlazorHost بالظبط.
    // ==========================================================================
    public partial class ReturnsPageBlazorHost : UserControl
    {
        public ReturnsPageBlazorHost()
        {
            this.Dock = DockStyle.Fill;

            var services = new ServiceCollection();
            services.AddWindowsFormsBlazorWebView();

            var blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index-returns.html",
                Services = services.BuildServiceProvider()
            };
            blazorWebView.RootComponents.Add<ReturnsApp>("#app");

            this.Controls.Add(blazorWebView);
        }
    }
}
