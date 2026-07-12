using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // WhatsAppHelper: بتفتح واتساب (ديسكتوب أو الويب) على رقم معين برسالة جاهزة.
    // المستخدم بس بيدوس "إرسال" - مفيش إرسال تلقائي بالكامل (محتاج WhatsApp
    // Business API رسمي من ميتا، ده أبسط وأسرع وآمن وبيشتغل فورًا من غير اشتراك).
    // ==========================================================================
    public static class WhatsAppHelper
    {
        // بيحوّل رقم تليفون مصري لصيغة دولية مناسبة لواتساب (01xxxxxxxxx -> 20xxxxxxxxx)
        public static string NormalizeEgyptianPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;

            string digitsOnly = new string(Array.FindAll(phone.ToCharArray(), char.IsDigit));
            if (digitsOnly.Length == 0) return null;

            if (digitsOnly.StartsWith("0") && digitsOnly.Length == 11)
                return "20" + digitsOnly.Substring(1);

            if (digitsOnly.StartsWith("20"))
                return digitsOnly;

            if (digitsOnly.Length == 10)
                return "20" + digitsOnly;

            return digitsOnly;
        }

        // بيفتح واتساب على الرقم بالرسالة الجاهزة. لو الرقم فاضي، بيطلب من المستخدم يكتبه
        public static void SendMessage(string phone, string message, IWin32Window owner)
        {
            string normalized = NormalizeEgyptianPhone(phone);

            if (string.IsNullOrEmpty(normalized))
            {
                string typed = PromptForPhone(owner);
                if (typed == null) return;
                normalized = NormalizeEgyptianPhone(typed);
                if (string.IsNullOrEmpty(normalized))
                {
                    MessageBox.Show("رقم التليفون غير صحيح.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            try
            {
                string url = $"https://wa.me/{normalized}?text={Uri.EscapeDataString(message)}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("مش قادر يفتح واتساب: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string PromptForPhone(IWin32Window owner)
        {
            using (Form inputForm = new Form())
            {
                inputForm.Text = "رقم تليفون العميل";
                inputForm.Size = new System.Drawing.Size(340, 170);
                inputForm.StartPosition = FormStartPosition.CenterParent;
                inputForm.RightToLeft = RightToLeft.Yes;
                inputForm.RightToLeftLayout = true;
                inputForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                inputForm.MaximizeBox = false;
                inputForm.MinimizeBox = false;

                Label lbl = new Label { Text = "مفيش رقم تليفون مسجّل. اكتب رقم العميل:", Location = new System.Drawing.Point(15, 15), Size = new System.Drawing.Size(300, 40) };
                TextBox txt = new TextBox { Location = new System.Drawing.Point(15, 60), Width = 300 };

                Guna.UI2.WinForms.Guna2Button btnOk = new Guna.UI2.WinForms.Guna2Button { Text = "إرسال", Location = new System.Drawing.Point(15, 95), Width = 140, Height = 32, FillColor = System.Drawing.Color.FromArgb(39, 174, 96) };
                Guna.UI2.WinForms.Guna2Button btnCancel = new Guna.UI2.WinForms.Guna2Button { Text = "إلغاء", Location = new System.Drawing.Point(175, 95), Width = 140, Height = 32, FillColor = System.Drawing.Color.FromArgb(236, 240, 241), ForeColor = System.Drawing.Color.FromArgb(26, 43, 76) };

                string resultValue = null;
                btnOk.Click += (s, e) => { resultValue = txt.Text; inputForm.DialogResult = DialogResult.OK; inputForm.Close(); };
                btnCancel.Click += (s, e) => { inputForm.DialogResult = DialogResult.Cancel; inputForm.Close(); };

                inputForm.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
                inputForm.AcceptButton = btnOk;

                return inputForm.ShowDialog(owner) == DialogResult.OK ? resultValue : null;
            }
        }
    }
}
