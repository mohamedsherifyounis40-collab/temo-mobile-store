using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Temo_Mobile_Store.BlazorComponents;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // PurchasesPageBlazorHost: تجربة شاشة مشتريات جديدة بـ HTML/CSS حقيقي (Blazor Hybrid
    // عن طريق WebView2) - نفس نمط SalesPageBlazorHost بالظبط. منطق الشراء الحقيقي (فاتورة
    // شراء جديدة) كان جوه SuppliersPageControl بس - دي واجهة جديدة بالكامل ليه.
    // ==========================================================================
    public partial class PurchasesPageBlazorHost : UserControl
    {
        public PurchasesPageBlazorHost()
        {
            this.Dock = DockStyle.Fill;

            var services = new ServiceCollection();
            services.AddWindowsFormsBlazorWebView();

            var blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index-purchases.html",
                Services = services.BuildServiceProvider()
            };
            blazorWebView.RootComponents.Add<PurchasesApp>("#app");

            this.Controls.Add(blazorWebView);
        }
    }
}
