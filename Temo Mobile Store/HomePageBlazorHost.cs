using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Temo_Mobile_Store.BlazorComponents;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // HomePageBlazorHost: تجربة شاشة رئيسية جديدة بـ HTML/CSS حقيقي (Blazor Hybrid
    // عن طريق WebView2) بدل إحداثيات WinForms - نفس نمط SalesPageBlazorHost بالظبط،
    // بس بيحمّل صفحة استضافة منفصلة (index-home.html) عشان يجيب CSS الخاص بيه بس.
    // الداشبورد القديم (BuildHomeDashboardPage جوه MainShell.cs) منفصل تمامًا ومتأثرش.
    // ==========================================================================
    public partial class HomePageBlazorHost : UserControl
    {
        public HomePageBlazorHost()
        {
            this.Dock = DockStyle.Fill;

            var services = new ServiceCollection();
            services.AddWindowsFormsBlazorWebView();

            var blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index-home.html",
                Services = services.BuildServiceProvider()
            };
            blazorWebView.RootComponents.Add<HomeApp>("#app");

            this.Controls.Add(blazorWebView);
        }
    }
}
