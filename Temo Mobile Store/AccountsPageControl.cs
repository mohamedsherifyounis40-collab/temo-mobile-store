using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // AccountsPageControl: نسخة مستقلة من ثلاث شاشات مرتبطة في Form1.cs:
    // شجرة الحسابات (CreateAccountsTreeDesign) + قائمة الدخل (CreateIncomeStatementDesign)
    // + ميزان المراجعة (CreateTrialBalanceDesign)، مع سويتش فوق للتبديل بينهم.
    // ==========================================================================
    public partial class AccountsPageControl : UserControl
    {
        private static readonly Color ColorPrimary = UIHelpers.ColorPrimary;
        private static readonly Color ColorSuccess = UIHelpers.ColorSuccess;
        private static readonly Color ColorDanger = UIHelpers.ColorDanger;
        private static readonly Color ColorWarning = UIHelpers.ColorWarning;
        private static readonly Color ColorNeutral = UIHelpers.ColorNeutral;
        private static readonly Color ColorBackground = UIHelpers.ColorBackground;

        private Guna2ComboBox cmbAccountsViewType;
        private Panel pnlAccountsTree, pnlIncomeStatement, pnlTrialBalance;

        // ---------- شجرة الحسابات ----------
        private Guna2TextBox txtAccountCode, txtAccountName;
        private Guna2Button btnSaveAccountEdit;
        private DataGridView dgvAccountsTree;
        private int selectedAccountCode = -1;

        // ---------- قائمة الدخل ----------
        private DateTimePicker dtpIncomeFrom, dtpIncomeTo;
        private DataGridView dgvIncomeStatement;

        // ---------- ميزان المراجعة ----------
        private DataGridView dgvTrialBalance;

        public AccountsPageControl()
        {
            this.Dock = DockStyle.Fill;
            this.Size = new Size(1150, 1150); // مقاس مبدئي واقعي قبل بناء الشاشة، عشان حسابات Anchor متبقاش غلط (راجع نفس التعليق في SalesPageControl)
            this.AutoScroll = true;
            this.BackColor = ColorBackground;

            BuildUI();
        }

        // ==========================================================================
        // الهيكل العام: سويتش فوق + 3 بانلات يتبادلوا الظهور
        // ==========================================================================
        private void BuildUI()
        {
            Label lblViewType = new Label() { Text = "العرض:", Location = new Point(20, 20), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            cmbAccountsViewType = new Guna2ComboBox() { Location = new Point(90, 17), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbAccountsViewType.Items.AddRange(new string[] { "شجرة الحسابات 🌳", "قائمة الدخل 📈", "ميزان المراجعة ⚖️" });
            cmbAccountsViewType.SelectedIndexChanged += CmbAccountsViewType_SelectedIndexChanged;

            AnchorStyles fillAnchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlAccountsTree = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 680), Anchor = fillAnchor };
            pnlIncomeStatement = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 680), Anchor = fillAnchor };
            pnlTrialBalance = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 680), Anchor = fillAnchor };

            BuildAccountsTreePanel();
            BuildIncomeStatementPanel();
            BuildTrialBalancePanel();

            pnlIncomeStatement.Visible = false;
            pnlTrialBalance.Visible = false;

            this.Controls.AddRange(new Control[] { lblViewType, cmbAccountsViewType, pnlAccountsTree, pnlIncomeStatement, pnlTrialBalance });

            cmbAccountsViewType.SelectedIndex = 0;
        }

        private void CmbAccountsViewType_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = cmbAccountsViewType.SelectedIndex;
            pnlAccountsTree.Visible = idx == 0;
            pnlIncomeStatement.Visible = idx == 1;
            pnlTrialBalance.Visible = idx == 2;

            if (idx == 1) ShowIncomeStatement();
            else if (idx == 2) ShowTrialBalance();
        }

        // ==========================================================================
        // بانل شجرة الحسابات
        // ==========================================================================
        private void BuildAccountsTreePanel()
        {
            Guna2Panel gbAccount = new Guna2Panel() { Location = new Point(0, 0), Size = new Size(280, 320), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblAccountTitle = new Label() { Text = "🌳 إضافة / تعديل حساب", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblCode = new Label() { Text = "كود الحساب:", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtAccountCode = new Guna2TextBox() { Location = new Point(20, 70), Width = 240, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblName = new Label() { Text = "اسم الحساب:", Location = new Point(20, 108), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtAccountName = new Guna2TextBox() { Location = new Point(20, 128), Width = 240, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Guna2Button btnAddAccount = new Guna2Button() { Text = "إضافة حساب جديد ✅", Location = new Point(20, 166), Width = 240, Height = 36, FillColor = ColorSuccess, BorderRadius = 10 };
            btnAddAccount.Click += BtnAddAccount_Click;

            Guna2Button btnEditAccountMode = new Guna2Button() { Text = "تعديل الحساب المحدد ✏️", Location = new Point(20, 210), Width = 240, Height = 34, FillColor = ColorPrimary, BorderRadius = 9 };
            btnEditAccountMode.Click += BtnEditAccountMode_Click;

            btnSaveAccountEdit = new Guna2Button() { Text = "حفظ تعديل الحساب 💾", Location = new Point(20, 250), Width = 240, Height = 34, FillColor = ColorWarning, Enabled = false, BorderRadius = 9 };
            btnSaveAccountEdit.Click += BtnSaveAccountEdit_Click;

            Guna2Button btnDeleteAccount = new Guna2Button() { Text = "حذف الحساب المحدد ❌", Location = new Point(20, 290), Width = 240, Height = 30, FillColor = ColorDanger, BorderRadius = 9 };
            btnDeleteAccount.Click += BtnDeleteAccount_Click;

            gbAccount.Controls.AddRange(new Control[] { lblAccountTitle, lblCode, txtAccountCode, lblName, txtAccountName, btnAddAccount, btnEditAccountMode, btnSaveAccountEdit, btnDeleteAccount });

            Guna2Panel pnlNote = new Guna2Panel() { Location = new Point(0, 335), Size = new Size(280, 150), FillColor = Color.FromArgb(248, 249, 251), BorderRadius = 12 };
            Label lblNote = new Label()
            {
                Text = "ℹ️ ملحوظة: كود الحساب في المحاسبة المصرية بيتقسّم حسب النوع:\n1xxx أصول، 2xxx التزامات، 3xxx حقوق ملكية، 4xxx إيرادات، 5xxx مصروفات.",
                Location = new Point(15, 12),
                Size = new Size(250, 125),
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(85, 92, 102)
            };
            pnlNote.Controls.Add(lblNote);

            AnchorStyles gridFillAnchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Guna2Panel pnlGridCard = new Guna2Panel() { Location = new Point(300, 0), Size = new Size(780, 680), Anchor = gridFillAnchor, FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblGridTitle = new Label() { Text = "📒 كل الحسابات المسجّلة", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };

            dgvAccountsTree = new DataGridView() { Location = new Point(20, 50), Size = new Size(740, 615), Anchor = gridFillAnchor, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvAccountsTree.CellClick += DgvAccountsTree_CellClick;
            StyleDataGridView(dgvAccountsTree);
            pnlGridCard.Controls.AddRange(new Control[] { lblGridTitle, dgvAccountsTree });

            pnlAccountsTree.Controls.AddRange(new Control[] { gbAccount, pnlNote, pnlGridCard });

            LoadAccountsTreeGrid();
        }

        // ==========================================================================
        // بانل قائمة الدخل
        // ==========================================================================
        private void BuildIncomeStatementPanel()
        {
            Guna2Panel gbFilter = new Guna2Panel() { Location = new Point(0, 0), Size = new Size(280, 190), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblFilterTitle = new Label() { Text = "📅 الفترة الزمنية", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblFrom = new Label() { Text = "من تاريخ:", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            dtpIncomeFrom = new DateTimePicker() { Location = new Point(20, 70), Width = 240, Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddMonths(-1) };

            Label lblTo = new Label() { Text = "إلى تاريخ:", Location = new Point(20, 108), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            dtpIncomeTo = new DateTimePicker() { Location = new Point(20, 128), Width = 240, Format = DateTimePickerFormat.Short, Value = DateTime.Now };

            Guna2Button btnShowIncome = new Guna2Button() { Text = "عرض قائمة الدخل 📈", Location = new Point(20, 158), Width = 240, Height = 34, FillColor = ColorPrimary, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), BorderRadius = 8 };
            btnShowIncome.Click += (s, e) => ShowIncomeStatement();

            gbFilter.Controls.AddRange(new Control[] { lblFilterTitle, lblFrom, dtpIncomeFrom, lblTo, dtpIncomeTo, btnShowIncome });
            gbFilter.Size = new Size(280, 205);

            Guna2Button btnPrintIncome = new Guna2Button() { Text = "طباعة / PDF 🖨️", Location = new Point(0, 425), Width = 280, Height = 32, FillColor = ColorNeutral, ForeColor = ColorPrimary, BorderRadius = 9 };
            btnPrintIncome.Click += (s, e) => GridPrintHelper.Print(dgvIncomeStatement, "قائمة الدخل", this.FindForm());

            Guna2Panel pnlNote = new Guna2Panel() { Location = new Point(0, 220), Size = new Size(280, 130), FillColor = Color.FromArgb(248, 249, 251), BorderRadius = 12 };
            Label lblNote = new Label()
            {
                Text = "ℹ️ ملحوظة: الإيرادات والمصروفات هنا بتتجمع من جدول المبيعات والمصروفات، وكمان أي حركة قبض/صرف مربوطة بحساب إيرادات (4xxx) أو مصروفات (5xxx).",
                Location = new Point(15, 12),
                Size = new Size(250, 105),
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(85, 92, 102)
            };
            pnlNote.Controls.Add(lblNote);

            AnchorStyles gridFillAnchor2 = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Guna2Panel pnlGridCard = new Guna2Panel() { Location = new Point(300, 0), Size = new Size(780, 680), Anchor = gridFillAnchor2, FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblGridTitle = new Label() { Text = "📈 قائمة الدخل التفصيلية", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };

            dgvIncomeStatement = new DataGridView() { Location = new Point(20, 50), Size = new Size(740, 615), Anchor = gridFillAnchor2, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvIncomeStatement.DefaultCellStyle.Font = new Font("Segoe UI", 10F);
            StyleDataGridView(dgvIncomeStatement);
            pnlGridCard.Controls.AddRange(new Control[] { lblGridTitle, dgvIncomeStatement });

            pnlIncomeStatement.Controls.AddRange(new Control[] { gbFilter, pnlNote, btnPrintIncome, pnlGridCard });
        }

        // ==========================================================================
        // بانل ميزان المراجعة
        // ==========================================================================
        private void BuildTrialBalancePanel()
        {
            Guna2Panel gbAction = new Guna2Panel() { Location = new Point(0, 0), Size = new Size(280, 140), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Guna2Button btnShowTrial = new Guna2Button() { Text = "عرض ميزان المراجعة ⚖️", Location = new Point(20, 20), Width = 240, Height = 40, FillColor = ColorPrimary, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), BorderRadius = 10 };
            btnShowTrial.Click += (s, e) => ShowTrialBalance();

            Guna2Button btnPrintTrial = new Guna2Button() { Text = "طباعة / PDF 🖨️", Location = new Point(20, 68), Width = 240, Height = 32, FillColor = ColorNeutral, ForeColor = ColorPrimary, BorderRadius = 9 };
            btnPrintTrial.Click += (s, e) => GridPrintHelper.Print(dgvTrialBalance, "ميزان المراجعة", this.FindForm());

            gbAction.Controls.AddRange(new Control[] { btnShowTrial, btnPrintTrial });

            Guna2Panel pnlNote = new Guna2Panel() { Location = new Point(0, 150), Size = new Size(280, 200), FillColor = Color.FromArgb(248, 249, 251), BorderRadius = 12 };
            Label lblNote = new Label()
            {
                Text = "ℹ️ ملحوظة محاسبية: الميزان ده بيعرض لحظة حالية (دلوقتي)، مش تاريخ معين. الأرقام كلها من الدفتر المحاسبي الحقيقي (القيود)، فالمدين بيساوي الدائن دايمًا بالضمان.",
                Location = new Point(15, 12),
                Size = new Size(250, 175),
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(85, 92, 102)
            };
            pnlNote.Controls.Add(lblNote);

            AnchorStyles gridFillAnchor3 = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Guna2Panel pnlGridCard = new Guna2Panel() { Location = new Point(300, 0), Size = new Size(780, 680), Anchor = gridFillAnchor3, FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblGridTitle = new Label() { Text = "⚖️ ميزان المراجعة - كل الحسابات", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };

            dgvTrialBalance = new DataGridView() { Location = new Point(20, 50), Size = new Size(740, 615), Anchor = gridFillAnchor3, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvTrialBalance.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            StyleDataGridView(dgvTrialBalance);
            pnlGridCard.Controls.AddRange(new Control[] { lblGridTitle, dgvTrialBalance });

            pnlTrialBalance.Controls.AddRange(new Control[] { gbAction, pnlNote, pnlGridCard });
        }

        // ==========================================================================
        // نفس تنسيق الجداول المستخدم في كل شاشات Form1.cs
        // ==========================================================================
        private void StyleDataGridView(DataGridView dgv) => UIHelpers.StyleDataGridView(dgv);

        // ==========================================================================
        // شجرة الحسابات: تحميل / إضافة / تعديل / حذف
        // ==========================================================================
        private void LoadAccountsTreeGrid()
        {
            try
            {
                dgvAccountsTree.DataSource = AccountsRepository.GetAccountsTreeGrid();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void DgvAccountsTree_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvAccountsTree.Rows[e.RowIndex];
            selectedAccountCode = Convert.ToInt32(row.Cells["كود الحساب"].Value);
            txtAccountCode.Text = selectedAccountCode.ToString();
            txtAccountCode.ReadOnly = true;
            txtAccountName.Text = row.Cells["اسم الحساب"].Value.ToString();
            btnSaveAccountEdit.Enabled = false;
        }

        private void BtnAddAccount_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(txtAccountCode.Text, out int code) || string.IsNullOrWhiteSpace(txtAccountName.Text))
            {
                MessageBox.Show("من فضلك أدخل كود حساب رقمي واسم حساب صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                if (AccountsRepository.AccountCodeExists(code))
                {
                    MessageBox.Show("الكود ده مستخدم بالفعل لحساب تاني.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                AccountsRepository.AddAccount(code, txtAccountName.Text.Trim());
                MessageBox.Show("تم إضافة الحساب بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearAccountInputs();
                LoadAccountsTreeGrid();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnEditAccountMode_Click(object sender, EventArgs e)
        {
            if (selectedAccountCode == -1)
            {
                MessageBox.Show("من فضلك اختر حساب من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            btnSaveAccountEdit.Enabled = true;
        }

        private void BtnSaveAccountEdit_Click(object sender, EventArgs e)
        {
            if (selectedAccountCode == -1 || string.IsNullOrWhiteSpace(txtAccountName.Text)) return;

            try
            {
                AccountsRepository.UpdateAccountName(selectedAccountCode, txtAccountName.Text.Trim());
                MessageBox.Show("تم تعديل اسم الحساب بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearAccountInputs();
                LoadAccountsTreeGrid();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnDeleteAccount_Click(object sender, EventArgs e)
        {
            if (selectedAccountCode == -1)
            {
                MessageBox.Show("من فضلك اختر حساب من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                int usageCount = AccountsRepository.GetAccountUsageCount(selectedAccountCode);
                if (usageCount > 0)
                {
                    MessageBox.Show($"لا يمكن حذف هذا الحساب لأنه مستخدم في {usageCount} حركة/حركات مسجّلة بالفعل.", "غير مسموح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show("هل أنت متأكد من حذف هذا الحساب نهائيًا؟", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                AccountsRepository.DeleteAccount(selectedAccountCode);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

            MessageBox.Show("تم حذف الحساب بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearAccountInputs();
            LoadAccountsTreeGrid();
        }

        private void ClearAccountInputs()
        {
            selectedAccountCode = -1;
            txtAccountCode.Clear();
            txtAccountCode.ReadOnly = false;
            txtAccountName.Clear();
            btnSaveAccountEdit.Enabled = false;
        }

        // ==========================================================================
        // قائمة الدخل
        // ==========================================================================
        private void ShowIncomeStatement()
        {
            DataTable dt;
            try
            {
                dt = AccountsRepository.GetIncomeStatement(dtpIncomeFrom.Value, dtpIncomeTo.Value);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

            dgvIncomeStatement.DataSource = dt;

            foreach (DataGridViewRow row in dgvIncomeStatement.Rows)
            {
                string label = row.Cells[0].Value?.ToString() ?? "";
                if (label.Contains("إجمالي") || label.Contains("مجمل") || label.Contains("صافي"))
                {
                    row.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
                    row.DefaultCellStyle.BackColor = ColorBackground;
                }
            }
        }

        // ==========================================================================
        // ميزان المراجعة
        // ==========================================================================
        private void ShowTrialBalance()
        {
            DataTable dt;
            try
            {
                dt = AccountsRepository.GetTrialBalance();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

            dgvTrialBalance.DataSource = dt;

            foreach (DataGridViewRow row in dgvTrialBalance.Rows)
            {
                string label = row.Cells[1].Value?.ToString() ?? "";
                if (label == "الإجمالي")
                {
                    row.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
                    row.DefaultCellStyle.BackColor = ColorBackground;
                }
            }
        }
    }
}
