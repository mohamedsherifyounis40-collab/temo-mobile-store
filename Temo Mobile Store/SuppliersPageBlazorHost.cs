using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Temo_Mobile_Store.BlazorComponents;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // SuppliersPageBlazorHost: تجربة شاشة موردين جديدة بـ HTML/CSS حقيقي (Blazor Hybrid
    // عن طريق WebView2) بدل إحداثيات WinForms - نفس نمط CustomersPageBlazorHost بالظبط.
    // الشاشة القديمة (SuppliersPageControl) منفصلة تمامًا ومتأثرتش.
    // ==========================================================================
    public partial class SuppliersPageBlazorHost : UserControl
    {
        public SuppliersPageBlazorHost()
        {
            this.Dock = DockStyle.Fill;

            var services = new ServiceCollection();
            services.AddWindowsFormsBlazorWebView();

            var blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index-suppliers.html",
                Services = services.BuildServiceProvider()
            };
            blazorWebView.RootComponents.Add<SuppliersApp>("#app");

            this.Controls.Add(blazorWebView);
        }
    }
}
