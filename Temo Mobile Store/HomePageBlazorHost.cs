using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Temo_Mobile_Store.BlazorComponents;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // HomePageBlazorHost: شاشة الرئيسية بـ HTML/CSS حقيقي (Blazor Hybrid عن طريق
    // WebView2) بدل إحداثيات WinForms - نفس نمط SalesPageBlazorHost بالظبط، بس
    // بيحمّل صفحة استضافة منفصلة (index-home.html) عشان يجيب CSS الخاص بيه بس.
    //
    // embedded=true: الاستخدام الحقيقي - MainShell.BuildHomeDashboardPage بيحطه
    // جوه pnlContent نفسها كمحتوى دائم، فبنشيل السايدبار/التوب بار بتاعته (Embedded
    // parameter) عشان مانكررش سايدبار MainShell الحقيقي المفروض يكون ظاهر أصلًا.
    // embedded=false (افتراضي): نافذة منفصلة كاملة (BuildHomeBlazorPage التجريبية).
    // ==========================================================================
    public partial class HomePageBlazorHost : UserControl
    {
        public HomePageBlazorHost(bool embedded = false)
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
            blazorWebView.RootComponents.Add<HomeApp>("#app", new Dictionary<string, object> { ["Embedded"] = embedded });

            this.Controls.Add(blazorWebView);
        }
    }
}
