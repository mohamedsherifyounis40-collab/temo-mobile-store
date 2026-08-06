using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace Temo_Mobile_Store
{
    public class DenominationEntryForm : Form
    {
        // الفئات الورقية المصرية الشائعة، ممكن تضيف/تشيل فئات من هنا بسهولة
        private static readonly decimal[] Denominations = { 200m, 100m, 50m, 20m, 10m, 5m, 1m };

        private readonly Dictionary<decimal, TextBox> countBoxes = new Dictionary<decimal, TextBox>();
        private readonly Dictionary<decimal, Label> lineTotalLabels = new Dictionary<decimal, Label>();
        private readonly Dictionary<string, TextBox> otherMethodBoxes = new Dictionary<string, TextBox>();
        private Label lblGrandTotal;
        private Label lblExpected;
        private decimal recordedCashBalance;
        private Dictionary<string, decimal> otherMethodsRecorded;

        public decimal TotalCounted { get; private set; } = 0;
        public Dictionary<decimal, int> DenominationCounts { get; private set; } = new Dictionary<decimal, int>();
        public Dictionary<string, decimal> OtherMethodsActual { get; private set; } = new Dictionary<string, decimal>();

        public DenominationEntryForm(decimal recordedCash, Dictionary<string, decimal> otherMethodsRecorded)
        {
            recordedCashBalance = recordedCash;
            this.otherMethodsRecorded = otherMethodsRecorded;
            this.Text = "تأكيد أرصدة الوسائل - إقفال اليوم";
            this.Size = new Size(460, 780);
            this.StartPosition = FormStartPosition.CenterParent;
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.AutoScroll = true;

            BuildLayout();
        }

        private void BuildLayout()
        {
            Label lblSectionCash = new Label
            {
                Text = "أولاً: عد الكاش الفعلي (نقدي) 💵",
                Location = new Point(20, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 43, 76)
            };
            this.Controls.Add(lblSectionCash);

            lblExpected = new Label
            {
                Text = $"الرصيد النقدي المسجّل حاليًا: {recordedCashBalance:N2} ج.م",
                Location = new Point(20, 45),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 43, 76)
            };
            this.Controls.Add(lblExpected);

            Label lblHeaderQty = new Label { Text = "العدد", Location = new Point(20, 80), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            Label lblHeaderVal = new Label { Text = "الفئة", Location = new Point(120, 80), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            Label lblHeaderTotal = new Label { Text = "الإجمالي", Location = new Point(230, 80), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            this.Controls.AddRange(new Control[] { lblHeaderQty, lblHeaderVal, lblHeaderTotal });

            int y = 108;
            foreach (decimal denom in Denominations)
            {
                Label lblDenom = new Label { Text = $"{denom:0.##} ج.م", Location = new Point(120, y + 3), AutoSize = true, Font = new Font("Segoe UI", 10) };

                TextBox txtCount = new TextBox { Location = new Point(20, y), Width = 80, Text = "0" };
                txtCount.TextChanged += (s, e) => RecalculateTotals();

                Label lblLineTotal = new Label { Text = "0.00", Location = new Point(230, y + 3), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

                countBoxes[denom] = txtCount;
                lineTotalLabels[denom] = lblLineTotal;

                this.Controls.AddRange(new Control[] { lblDenom, txtCount, lblLineTotal });
                y += 36;
            }

            lblGrandTotal = new Label
            {
                Text = "إجمالي النقدي: 0.00 ج.م",
                Location = new Point(20, y + 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(39, 174, 96)
            };
            this.Controls.Add(lblGrandTotal);
            y += 45;

            Label lblSeparator1 = new Label { Text = "────────────────────────", Location = new Point(20, y), AutoSize = true, ForeColor = Color.Gray };
            this.Controls.Add(lblSeparator1);
            y += 25;

            Label lblSectionOther = new Label
            {
                Text = "ثانيًا: أرصدة باقي الوسائل الإلكترونية 📱",
                Location = new Point(20, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(26, 43, 76)
            };
            this.Controls.Add(lblSectionOther);
            y += 30;

            Label lblOtherNote = new Label
            {
                Text = "الأرقام دي متعبّية تلقائي بالرصيد المسجّل حاليًا في النظام، عدّلها لو الرصيد الفعلي في المحفظة/التطبيق مختلف:",
                Location = new Point(20, y),
                Size = new Size(400, 30),
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.Gray
            };
            this.Controls.Add(lblOtherNote);
            y += 35;

            foreach (var kvp in otherMethodsRecorded)
            {
                Label lblMethodName = new Label { Text = kvp.Key + ":", Location = new Point(230, y + 3), AutoSize = true, Font = new Font("Segoe UI", 9.5F) };
                TextBox txtBalance = new TextBox { Location = new Point(20, y), Width = 190, Text = kvp.Value.ToString("0.##") };
                otherMethodBoxes[kvp.Key] = txtBalance;

                this.Controls.AddRange(new Control[] { lblMethodName, txtBalance });
                y += 38;
            }

            y += 10;
            Guna2Button btnConfirm = new Guna2Button
            {
                Text = "تأكيد إقفال اليوم لكل الوسائل 🔒",
                Location = new Point(20, y),
                Width = 400,
                Height = 42,
                FillColor = Color.FromArgb(231, 76, 60),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnConfirm.Click += BtnConfirm_Click;
            this.Controls.Add(btnConfirm);
            y += 55;

            Guna2Button btnCancel = new Guna2Button
            {
                Text = "إلغاء",
                Location = new Point(20, y),
                Width = 400,
                Height = 34,
                FillColor = Color.FromArgb(236, 240, 241),
                ForeColor = Color.FromArgb(26, 43, 76)
            };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.Controls.Add(btnCancel);
            y += 55;

            this.ClientSize = new Size(430, y);
        }

        private void RecalculateTotals()
        {
            decimal grandTotal = 0;
            foreach (decimal denom in Denominations)
            {
                int count = 0;
                int.TryParse(countBoxes[denom].Text, out count);
                if (count < 0) count = 0;
                decimal lineTotal = denom * count;
                lineTotalLabels[denom].Text = lineTotal.ToString("N2");
                grandTotal += lineTotal;
            }
            lblGrandTotal.Text = $"إجمالي النقدي: {grandTotal:N2} ج.م";
        }

        private void BtnConfirm_Click(object sender, EventArgs e)
        {
            DenominationCounts.Clear();
            decimal grandTotal = 0;

            foreach (decimal denom in Denominations)
            {
                if (!int.TryParse(countBoxes[denom].Text, out int count) || count < 0)
                {
                    MessageBox.Show($"من فضلك أدخل عدد صحيح وموجب لفئة {denom:0.##} ج.م", "قيمة غير صحيحة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                DenominationCounts[denom] = count;
                grandTotal += denom * count;
            }

            var tempOtherActual = new Dictionary<string, decimal>();
            foreach (var kvp in otherMethodBoxes)
            {
                if (!decimal.TryParse(kvp.Value.Text, out decimal balance) || balance < 0)
                {
                    MessageBox.Show($"من فضلك أدخل رصيد صحيح وموجب لـ \"{kvp.Key}\"", "قيمة غير صحيحة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                tempOtherActual[kvp.Key] = balance;
            }

            TotalCounted = grandTotal;
            OtherMethodsActual = tempOtherActual;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
