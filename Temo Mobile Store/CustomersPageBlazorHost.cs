using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Temo_Mobile_Store.BlazorComponents;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // CustomersPageBlazorHost: تجربة شاشة عملاء جديدة بـ HTML/CSS حقيقي (Blazor Hybrid
    // عن طريق WebView2) بدل إحداثيات WinForms - نفس نمط SalesPageBlazorHost/HomePageBlazorHost/
    // InventoryPageBlazorHost بالظبط. الشاشة القديمة (CustomersPageControl) منفصلة تمامًا ومتأثرتش.
    // ==========================================================================
    public partial class CustomersPageBlazorHost : UserControl
    {
        public CustomersPageBlazorHost()
        {
            this.Dock = DockStyle.Fill;

            var services = new ServiceCollection();
            services.AddWindowsFormsBlazorWebView();

            var blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index-customers.html",
                Services = services.BuildServiceProvider()
            };
            blazorWebView.RootComponents.Add<CustomersApp>("#app");

            this.Controls.Add(blazorWebView);
        }
    }
}
