using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using TemoStore.Core.Commands;
using TemoStore.Core.Entities;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // InventoryCountPageControl: نسخة مستقلة من شاشة "جرد المخزن" الموجودة في
    // Form1.cs (CreateInventoryCountDesign). نفس مبدأ باقي الشاشات: منفصل
    // بالكامل عن Form1.cs، بيقرا ويكتب على نفس قاعدة البيانات بالظبط.
    // ==========================================================================
    public partial class InventoryCountPageControl : UserControl
    {
        private static readonly Color ColorPrimary = UIHelpers.ColorPrimary;
        private static readonly Color ColorSuccess = UIHelpers.ColorSuccess;
        private static readonly Color ColorDanger = UIHelpers.ColorDanger;
        private static readonly Color ColorNeutral = UIHelpers.ColorNeutral;
        private static readonly Color ColorBackground = UIHelpers.ColorBackground;

        private Guna2TextBox txtInventorySearch;
        private DataGridView dgvInventoryCount;

        public InventoryCountPageControl()
        {
            this.Dock = DockStyle.Fill;
            this.Size = new Size(1150, 1150); // مقاس مبدئي واقعي قبل بناء الشاشة، عشان حسابات Anchor متبقاش غلط (راجع نفس التعليق في SalesPageControl)
            this.AutoScroll = true;
            this.BackColor = ColorBackground;

            BuildUI();
            LoadInventoryCountGrid();
        }

        private void BuildUI()
        {
            AnchorStyles widenAnchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Guna2Panel pnlToolbar = new Guna2Panel() { Location = new Point(20, 20), Size = new Size(1100, 90), Anchor = widenAnchor, FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };

            Label lblSearch = new Label() { Text = "بحث بالاسم أو الباركود:", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtInventorySearch = new Guna2TextBox() { Location = new Point(20, 38), Width = 280, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };
            txtInventorySearch.TextChanged += (s, e) => FilterInventoryCountGrid();

            Guna2Button btnRefreshInventoryCount = new Guna2Button() { Text = "بدء جرد جديد 🔄", Location = new Point(320, 38), Width = 190, Height = 34, FillColor = ColorNeutral, ForeColor = ColorPrimary, BorderRadius = 9 };
            btnRefreshInventoryCount.Click += BtnRefreshInventoryCount_Click;

            Guna2Button btnViewAdjustmentsLog = new Guna2Button() { Text = "سجل التسويات السابقة 📜", Location = new Point(520, 38), Width = 220, Height = 34, FillColor = ColorNeutral, ForeColor = ColorPrimary, BorderRadius = 9 };
            btnViewAdjustmentsLog.Click += BtnViewAdjustmentsLog_Click;

            Guna2Button btnSaveInventoryCount = new Guna2Button() { Text = "حفظ نتيجة الجرد وتسوية الفروقات ✅", Location = new Point(750, 38), Width = 330, Height = 34, FillColor = ColorSuccess, BorderRadius = 9 };
            btnSaveInventoryCount.Click += BtnSaveInventoryCount_Click;

            pnlToolbar.Controls.AddRange(new Control[] { lblSearch, txtInventorySearch, btnRefreshInventoryCount, btnViewAdjustmentsLog, btnSaveInventoryCount });

            Guna2Panel pnlNote = new Guna2Panel() { Location = new Point(20, 120), Size = new Size(1100, 45), Anchor = widenAnchor, FillColor = Color.FromArgb(255, 249, 230), BorderRadius = 10 };
            Label lblNote = new Label()
            {
                Text = "اكتب الكمية الفعلية اللي عددتها بإيدك في عمود \"الكمية الفعلية\" لأي صنف جردته، والباقي سيبه فاضي. عمود \"الفرق\" هيتحسب لوحده. لما تخلص، دوس \"حفظ نتيجة الجرد\".",
                Location = new Point(15, 12),
                Size = new Size(1070, 25),
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(138, 90, 0)
            };
            pnlNote.Controls.Add(lblNote);

            AnchorStyles gridFillAnchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Guna2Panel pnlGridCard = new Guna2Panel() { Location = new Point(20, 180), Size = new Size(1100, 545), Anchor = gridFillAnchor, FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblGridTitle = new Label() { Text = "📋 جدول الجرد", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = ColorPrimary };

            dgvInventoryCount = new DataGridView() { Location = new Point(20, 50), Size = new Size(1060, 480), Anchor = gridFillAnchor, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, AllowUserToAddRows = false, RowHeadersVisible = false };
            StyleDataGridView(dgvInventoryCount);
            dgvInventoryCount.CellValueChanged += DgvInventoryCount_CellValueChanged;
            dgvInventoryCount.CellFormatting += DgvInventoryCount_CellFormatting;
            dgvInventoryCount.CurrentCellDirtyStateChanged += (s, e) => { if (dgvInventoryCount.IsCurrentCellDirty) dgvInventoryCount.CommitEdit(DataGridViewDataErrorContexts.Commit); };

            pnlGridCard.Controls.AddRange(new Control[] { lblGridTitle, dgvInventoryCount });

            this.Controls.AddRange(new Control[] { pnlToolbar, pnlNote, pnlGridCard });
        }

        // ==========================================================================
        // نفس تنسيق الجداول المستخدم في كل شاشات Form1.cs
        // ==========================================================================
        private void StyleDataGridView(DataGridView dgv) => UIHelpers.StyleDataGridView(dgv);

        // ==========================================================================
        // تحميل جدول الجرد بكل المنتجات، وكمياتهم المسجّلة بالنظام
        // ==========================================================================
        private void LoadInventoryCountGrid()
        {
            DataTable dt = InventoryRepository.GetProductsForCount();

            dgvInventoryCount.DataSource = dt;
            dgvInventoryCount.Columns["الباركود"].ReadOnly = true;
            dgvInventoryCount.Columns["اسم المنتج"].ReadOnly = true;
            dgvInventoryCount.Columns["الكمية بالنظام"].ReadOnly = true;
            dgvInventoryCount.Columns["الفرق"].ReadOnly = true;
            // عمود "الكمية الفعلية" هو الوحيد القابل للتعديل، عشان المستخدم يكتب فيه بس نتيجة العد اليدوي
        }

        private void FilterInventoryCountGrid()
        {
            if (!(dgvInventoryCount.DataSource is DataTable dt)) return;
            string search = txtInventorySearch.Text.Trim().Replace("'", "''");
            dt.DefaultView.RowFilter = string.IsNullOrEmpty(search) ? "" : $"[اسم المنتج] LIKE '%{search}%' OR [الباركود] LIKE '%{search}%'";
        }

        private void DgvInventoryCount_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgvInventoryCount.Columns[e.ColumnIndex].Name != "الكمية الفعلية") return;

            var row = dgvInventoryCount.Rows[e.RowIndex];
            string countedText = row.Cells["الكمية الفعلية"].Value?.ToString().Trim();
            int systemQty = Convert.ToInt32(row.Cells["الكمية بالنظام"].Value);

            if (string.IsNullOrEmpty(countedText))
            {
                row.Cells["الفرق"].Value = "";
            }
            else if (int.TryParse(countedText, out int countedQty) && countedQty >= 0)
            {
                int diff = countedQty - systemQty;
                row.Cells["الفرق"].Value = diff == 0 ? "مطابق" : (diff > 0 ? $"+{diff}" : diff.ToString());
            }
            else
            {
                MessageBox.Show("من فضلك ادخل رقم صحيح موجب للكمية.", "قيمة غير صحيحة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                row.Cells["الكمية الفعلية"].Value = "";
                row.Cells["الفرق"].Value = "";
            }
        }

        private void DgvInventoryCount_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgvInventoryCount.Columns[e.ColumnIndex].Name != "الفرق") return;

            string val = e.Value?.ToString();
            if (string.IsNullOrEmpty(val)) return;

            if (val == "مطابق") e.CellStyle.ForeColor = ColorSuccess;
            else if (val.StartsWith("+")) e.CellStyle.ForeColor = Color.FromArgb(41, 128, 185); // أزرق: زيادة عن المسجل بالنظام
            else e.CellStyle.ForeColor = ColorDanger; // نقص عن المسجل بالنظام
        }

        private void BtnRefreshInventoryCount_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show("هل تريد بدء جرد جديد؟ أي كميات مكتوبة دلوقتي ومتحفظتش هتتمسح.", "تأكيد", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;
            txtInventorySearch.Clear();
            LoadInventoryCountGrid();
        }

        private void BtnSaveInventoryCount_Click(object sender, EventArgs e)
        {
            if (!(dgvInventoryCount.DataSource is DataTable dt)) return;

            var changedRows = new List<InventoryAdjustmentLine>();

            foreach (DataRow dr in dt.Rows)
            {
                string countedText = dr["الكمية الفعلية"]?.ToString().Trim();
                if (string.IsNullOrEmpty(countedText)) continue;
                if (!int.TryParse(countedText, out int countedQty)) continue;

                int systemQty = Convert.ToInt32(dr["الكمية بالنظام"]);
                int diff = countedQty - systemQty;
                if (diff == 0) continue; // مطابق، مفيش داعي نسجله في السجل

                changedRows.Add(new InventoryAdjustmentLine
                {
                    Barcode = dr["الباركود"].ToString(),
                    ProductName = dr["اسم المنتج"].ToString(),
                    SystemQty = systemQty,
                    CountedQty = countedQty,
                    Difference = diff
                });
            }

            if (changedRows.Count == 0)
            {
                MessageBox.Show("مفيش أي فروقات لتسويتها. إما لسه محددتش كميات، أو كل الكميات المكتوبة مطابقة للنظام.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string summary = string.Join("\n", changedRows.Select(r => $"{r.ProductName}: {r.SystemQty} -> {r.CountedQty} ({(r.Difference > 0 ? "+" : "")}{r.Difference})"));
            var confirm = MessageBox.Show($"هيتم تعديل كمية {changedRows.Count} صنف في المخزون حسب نتيجة الجرد ده:\n\n{summary}\n\nمتأكد إنك عايز تكمل؟", "تأكيد تسوية الجرد", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            try
            {
                AppServices.CoreEngine.Execute(new SaveInventoryAdjustmentsCommand { Rows = changedRows, PerformedBy = AuthManager.CurrentUsername });
            }
            catch (Exception ex)
            {
                MessageBox.Show("حصل خطأ أثناء حفظ الجرد: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show($"تم تسوية {changedRows.Count} صنف بنجاح", "نجاح", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadInventoryCountGrid();
        }

        private void BtnViewAdjustmentsLog_Click(object sender, EventArgs e)
        {
            DataTable dt;
            try
            {
                dt = InventoryRepository.GetAdjustmentsLog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }

            using (Form logForm = new Form() { Text = "سجل تسويات الجرد", Size = new Size(900, 600), StartPosition = FormStartPosition.CenterParent, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true })
            {
                DataGridView dgvLog = new DataGridView() { Dock = DockStyle.Fill, DataSource = dt, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
                StyleDataGridView(dgvLog);
                logForm.Controls.Add(dgvLog);
                logForm.ShowDialog(this.FindForm());
            }
        }
    }
}
