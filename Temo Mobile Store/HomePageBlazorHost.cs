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
    // MainShell.BuildHomeDashboardPage بيحط نسخة منها جوه pnlContent نفسها كمحتوى
    // دائم للنافذة الرئيسية (مفيش سايدبار/توب بار أصلي في MainShell خالص دلوقتي)،
    // فنفس السايدبار/التوب بار بتاع الشاشة دي هو اللي بيظهر في الاتنين.
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

            // كل ما الكونترول ده يظهر تاني (المستخدم رجع لشاشة الرئيسية من شاشة تانية،
            // راجع MainShell.ShowPageInline) نطلب من HomePage.razor تحديث فوري للداشبورد.
            this.VisibleChanged += (s, e) => { if (this.Visible) HomeRefreshSignal.Request(); };
        }
    }
}
