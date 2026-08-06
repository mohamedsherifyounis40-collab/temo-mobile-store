using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Temo_Mobile_Store.BlazorComponents;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // AccountsPageBlazorHost: تجربة شاشة حسابات جديدة بـ HTML/CSS حقيقي (Blazor Hybrid
    // عن طريق WebView2) - نفس نمط InventoryPageBlazorHost بالظبط. الشاشة القديمة
    // (AccountsPageControl) منفصلة تمامًا ومتأثرتش.
    // ==========================================================================
    public partial class AccountsPageBlazorHost : UserControl
    {
        public AccountsPageBlazorHost()
        {
            this.Dock = DockStyle.Fill;

            var services = new ServiceCollection();
            services.AddWindowsFormsBlazorWebView();

            var blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index-accounts.html",
                Services = services.BuildServiceProvider()
            };
            blazorWebView.RootComponents.Add<AccountsApp>("#app");

            this.Controls.Add(blazorWebView);
        }
    }
}
