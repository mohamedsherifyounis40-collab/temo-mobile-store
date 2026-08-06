using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Temo_Mobile_Store.BlazorComponents;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // InventoryCountPageBlazorHost: تجربة شاشة جرد مخزن جديدة بـ HTML/CSS حقيقي (Blazor
    // Hybrid عن طريق WebView2) - نفس نمط InventoryPageBlazorHost بالظبط. الشاشة القديمة
    // (InventoryCountPageControl) منفصلة تمامًا ومتأثرتش.
    // ==========================================================================
    public partial class InventoryCountPageBlazorHost : UserControl
    {
        public InventoryCountPageBlazorHost()
        {
            this.Dock = DockStyle.Fill;

            var services = new ServiceCollection();
            services.AddWindowsFormsBlazorWebView();

            var blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index-inventorycount.html",
                Services = services.BuildServiceProvider()
            };
            blazorWebView.RootComponents.Add<InventoryCountApp>("#app");

            this.Controls.Add(blazorWebView);
        }
    }
}
