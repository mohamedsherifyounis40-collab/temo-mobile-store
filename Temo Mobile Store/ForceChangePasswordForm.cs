using System;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // ForceChangePasswordForm: بتظهر إجباريًا لما حد يدخل بحساب الأدمن الافتراضي
    // (admin/admin123) - مينفعش يدخل البرنامج غير بعد ما يغيّر الباسورد ده.
    // ==========================================================================
    public class ForceChangePasswordForm : Form
    {
        private readonly string username;
        private Guna2TextBox txtNewPassword;
        private Guna2TextBox txtConfirmPassword;
        private Label lblError;

        public ForceChangePasswordForm(string username)
        {
            this.username = username;

            this.Text = "لازم تغيّر كلمة المرور الافتراضية";
            this.Size = new Size(420, 340);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ControlBox = false;
            this.BackColor = Color.FromArgb(245, 246, 250);

            BuildUI();
        }

        private void BuildUI()
        {
            Label lblTitle = new Label()
            {
                Text = "🔐 لسه بتستخدم كلمة المرور الافتراضية",
                Location = new Point(20, 20),
                Size = new Size(370, 30),
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 43, 76)
            };

            Label lblSubtitle = new Label()
            {
                Text = "من فضلك اختر كلمة مرور جديدة لحساب admin قبل ما تكمل.",
                Location = new Point(20, 55),
                Size = new Size(370, 40),
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(85, 92, 102)
            };

            Label lblNew = new Label() { Text = "كلمة المرور الجديدة", Location = new Point(20, 105), AutoSize = true, Font = new Font("Segoe UI", 9.5F) };
            txtNewPassword = new Guna2TextBox() { Location = new Point(20, 130), Width = 370, PasswordChar = '●', BorderRadius = 8 };

            Label lblConfirm = new Label() { Text = "تأكيد كلمة المرور", Location = new Point(20, 170), AutoSize = true, Font = new Font("Segoe UI", 9.5F) };
            txtConfirmPassword = new Guna2TextBox() { Location = new Point(20, 195), Width = 370, PasswordChar = '●', BorderRadius = 8 };

            lblError = new Label() { Location = new Point(20, 232), Size = new Size(370, 20), ForeColor = Color.FromArgb(192, 57, 43), Font = new Font("Segoe UI", 8.5F) };

            Guna2Button btnSave = new Guna2Button() { Text = "حفظ ومتابعة ✅", Location = new Point(20, 258), Width = 370, Height = 42, FillColor = Color.FromArgb(39, 174, 96), BorderRadius = 10, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            btnSave.Click += BtnSave_Click;

            this.Controls.AddRange(new Control[] { lblTitle, lblSubtitle, lblNew, txtNewPassword, lblConfirm, txtConfirmPassword, lblError, btnSave });
            this.AcceptButton = btnSave;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNewPassword.Text) || txtNewPassword.Text.Length < 6)
            {
                lblError.Text = "كلمة المرور لازم تكون 6 أحرف على الأقل.";
                return;
            }
            if (txtNewPassword.Text == "admin123")
            {
                lblError.Text = "من فضلك اختر كلمة مرور مختلفة عن الافتراضية.";
                return;
            }
            if (txtNewPassword.Text != txtConfirmPassword.Text)
            {
                lblError.Text = "كلمة المرور وتأكيدها مش متطابقين.";
                return;
            }

            AuthManager.ChangePassword(username, txtNewPassword.Text);
            MessageBox.Show("تم تغيير كلمة المرور بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
