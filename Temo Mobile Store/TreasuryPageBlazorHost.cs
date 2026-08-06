using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Temo_Mobile_Store.BlazorComponents;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // TreasuryPageBlazorHost: تجربة شاشة خزينة جديدة بـ HTML/CSS حقيقي (Blazor Hybrid
    // عن طريق WebView2) - نفس نمط InventoryPageBlazorHost بالظبط. الشاشة القديمة
    // (TreasuryPageControl) منفصلة تمامًا ومتأثرتش.
    // ==========================================================================
    public partial class TreasuryPageBlazorHost : UserControl
    {
        public TreasuryPageBlazorHost()
        {
            this.Dock = DockStyle.Fill;

            var services = new ServiceCollection();
            services.AddWindowsFormsBlazorWebView();

            var blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index-treasury.html",
                Services = services.BuildServiceProvider()
            };
            blazorWebView.RootComponents.Add<TreasuryApp>("#app");

            this.Controls.Add(blazorWebView);
        }
    }
}
