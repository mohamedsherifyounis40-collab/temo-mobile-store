using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Temo_Mobile_Store.BlazorComponents;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // SalesPageBlazorHost: تجربة شاشة مبيعات جديدة بـ HTML/CSS حقيقي (Blazor Hybrid
    // عن طريق WebView2) بدل إحداثيات WinForms - UserControl بسيط بيستضيف BlazorWebView
    // واحد بيحمّل SalesApp، بنفس نمط استضافة أي UserControl تاني في OpenPageWindow
    // (MainShell.cs) بالظبط. الشاشة القديمة (SalesPageControl) منفصلة تمامًا ومتأثرتش.
    // ==========================================================================
    public partial class SalesPageBlazorHost : UserControl
    {
        public SalesPageBlazorHost()
        {
            this.Dock = DockStyle.Fill;

            var services = new ServiceCollection();
            services.AddWindowsFormsBlazorWebView();

            var blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index.html",
                Services = services.BuildServiceProvider()
            };
            blazorWebView.RootComponents.Add<SalesApp>("#app");

            this.Controls.Add(blazorWebView);
        }
    }
}
