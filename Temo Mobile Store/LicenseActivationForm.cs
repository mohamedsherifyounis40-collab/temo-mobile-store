using System;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // LicenseActivationForm: الشاشة اللي بتظهر للعميل لو الترخيص مش موجود أو
    // خلصت صلاحيته. البرنامج مايفتحش خالص لحد ما يتفعّل بمفتاح صحيح.
    // ==========================================================================
    public class LicenseActivationForm : Form
    {
        private Guna2TextBox txtHardwareId;
        private Guna2TextBox txtLicenseKey;
        private Label lblStatus;

        public bool Activated { get; private set; } = false;

        public LicenseActivationForm(string initialMessage)
        {
            this.Text = "تفعيل البرنامج - Temo Mobile Store";
            this.Size = new Size(520, 480);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(245, 246, 250);

            BuildUI(initialMessage);
        }

        private void BuildUI(string initialMessage)
        {
            Label lblTitle = new Label()
            {
                Text = "🔒 البرنامج محتاج تفعيل",
                Location = new Point(20, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 43, 76)
            };

            lblStatus = new Label()
            {
                Text = initialMessage,
                Location = new Point(20, 60),
                Size = new Size(470, 40),
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(192, 57, 43)
            };

            Guna2Panel pnlHwCard = new Guna2Panel() { Location = new Point(20, 110), Size = new Size(470, 110), FillColor = Color.White, BorderRadius = 12, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblHwTitle = new Label() { Text = "الخطوة 1: ابعت الكود ده للمسؤول", Location = new Point(15, 12), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(26, 43, 76) };
            txtHardwareId = new Guna2TextBox() { Location = new Point(15, 42), Width = 340, ReadOnly = true, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251), Font = new Font("Consolas", 11, FontStyle.Bold), Text = LicenseManager.GetHardwareId() };
            Guna2Button btnCopy = new Guna2Button() { Text = "نسخ 📋", Location = new Point(365, 42), Width = 90, Height = 32, FillColor = Color.FromArgb(41, 98, 189), BorderRadius = 8 };
            btnCopy.Click += (s, e) => { Clipboard.SetText(txtHardwareId.Text); MessageBox.Show("تم نسخ الكود.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information); };
            Label lblHwNote = new Label() { Text = "ابعته للمسؤول عبر واتساب، وهيردّلك بمفتاح التفعيل.", Location = new Point(15, 80), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            pnlHwCard.Controls.AddRange(new Control[] { lblHwTitle, txtHardwareId, btnCopy, lblHwNote });

            Guna2Panel pnlKeyCard = new Guna2Panel() { Location = new Point(20, 235), Size = new Size(470, 130), FillColor = Color.White, BorderRadius = 12, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblKeyTitle = new Label() { Text = "الخطوة 2: الصق مفتاح التفعيل اللي وصلك", Location = new Point(15, 12), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.FromArgb(26, 43, 76) };
            txtLicenseKey = new Guna2TextBox() { Location = new Point(15, 42), Width = 440, Height = 55, Multiline = true, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251), Font = new Font("Consolas", 8.5F) };

            pnlKeyCard.Controls.AddRange(new Control[] { lblKeyTitle, txtLicenseKey });

            Guna2Button btnActivateReal = new Guna2Button() { Text = "تفعيل البرنامج ✅", Location = new Point(20, 380), Width = 470, Height = 42, FillColor = Color.FromArgb(39, 174, 96), BorderRadius = 10, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            btnActivateReal.Click += BtnActivate_Click;

            this.Controls.AddRange(new Control[] { lblTitle, lblStatus, pnlHwCard, pnlKeyCard, btnActivateReal });
            this.AcceptButton = btnActivateReal;
        }

        private void BtnActivate_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtLicenseKey.Text))
            {
                MessageBox.Show("من فضلك الصق مفتاح التفعيل أولاً.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var result = LicenseManager.ValidateLicenseKey(txtLicenseKey.Text);
            if (!result.IsValid)
            {
                lblStatus.Text = result.Message;
                MessageBox.Show(result.Message, "فشل التفعيل", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            LicenseManager.SaveLicenseKey(txtLicenseKey.Text);
            Activated = true;
            MessageBox.Show($"تم تفعيل البرنامج بنجاح حتى تاريخ {result.Expiration:yyyy-MM-dd} ✅", "تم التفعيل", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
