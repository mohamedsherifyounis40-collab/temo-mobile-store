using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Temo_Mobile_Store.BlazorComponents;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // InventoryPageBlazorHost: تجربة شاشة مخزون جديدة بـ HTML/CSS حقيقي (Blazor Hybrid
    // عن طريق WebView2) بدل إحداثيات WinForms - نفس نمط SalesPageBlazorHost/HomePageBlazorHost
    // بالظبط. الشاشة القديمة (InventoryPageControl) منفصلة تمامًا ومتأثرتش.
    // ==========================================================================
    public partial class InventoryPageBlazorHost : UserControl
    {
        public InventoryPageBlazorHost()
        {
            this.Dock = DockStyle.Fill;

            var services = new ServiceCollection();
            services.AddWindowsFormsBlazorWebView();

            var blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index-inventory.html",
                Services = services.BuildServiceProvider()
            };
            blazorWebView.RootComponents.Add<InventoryApp>("#app");

            this.Controls.Add(blazorWebView);
        }
    }
}
