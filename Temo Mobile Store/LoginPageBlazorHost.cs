using System.Drawing;
using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Temo_Mobile_Store.BlazorComponents;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // LoginPageBlazorHost: شاشة تسجيل الدخول بـ HTML/CSS حقيقي (Blazor Hybrid عن
    // طريق WebView2) بدل الـ LoginForm القديمة. نفس نمط باقي *PageBlazorHost لكنها
    // Form مش UserControl لأنها بتفتح كـ ShowDialog قبل ما MainShell يتفتح خالص.
    // ==========================================================================
    public class LoginPageBlazorHost : Form
    {
        public static LoginPageBlazorHost Instance { get; private set; }

        public LoginPageBlazorHost()
        {
            Instance = this;
            this.FormClosed += (s, e) => { if (Instance == this) Instance = null; };

            this.Text = "تسجيل الدخول - Temo Mobile Store";
            this.Size = new Size(460, 760);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var services = new ServiceCollection();
            services.AddWindowsFormsBlazorWebView();

            var blazorWebView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index-login.html",
                Services = services.BuildServiceProvider()
            };
            blazorWebView.RootComponents.Add<LoginApp>("#app");

            this.Controls.Add(blazorWebView);
        }

        public void CompleteLogin()
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
