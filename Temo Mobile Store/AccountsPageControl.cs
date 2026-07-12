using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using Microsoft.Data.Sqlite;

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

        private static readonly Dictionary<string, int> PaymentMethodAccountCodes = new Dictionary<string, int>
        {
            { "نقدي", 1100 }, { "فوري", 1110 }, { "أمان", 1120 }, { "سهولة", 1130 }, { "فودافون كاش", 1140 }, { "إنستاباي", 1150 }
        };

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

            pnlAccountsTree = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 680) };
            pnlIncomeStatement = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 680) };
            pnlTrialBalance = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 680) };

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

            Guna2Panel pnlGridCard = new Guna2Panel() { Location = new Point(300, 0), Size = new Size(780, 680), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblGridTitle = new Label() { Text = "📒 كل الحسابات المسجّلة", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };

            dgvAccountsTree = new DataGridView() { Location = new Point(20, 50), Size = new Size(740, 615), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
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

            Guna2Panel pnlGridCard = new Guna2Panel() { Location = new Point(300, 0), Size = new Size(780, 680), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblGridTitle = new Label() { Text = "📈 قائمة الدخل التفصيلية", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };

            dgvIncomeStatement = new DataGridView() { Location = new Point(20, 50), Size = new Size(740, 615), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
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
                Text = "ℹ️ ملحوظة محاسبية مهمة: الميزان ده بيعرض لحظة حالية (دلوقتي)، مش تاريخ معين. لو المدين ما يساويش الدائن بالظبط، الفرق بيعكس حركات لسه مش مربوطة بحساب في شجرة الحسابات.",
                Location = new Point(15, 12),
                Size = new Size(250, 175),
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(85, 92, 102)
            };
            pnlNote.Controls.Add(lblNote);

            Guna2Panel pnlGridCard = new Guna2Panel() { Location = new Point(300, 0), Size = new Size(780, 680), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblGridTitle = new Label() { Text = "⚖️ ميزان المراجعة - كل الحسابات", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };

            dgvTrialBalance = new DataGridView() { Location = new Point(20, 50), Size = new Size(740, 615), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
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
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("كود الحساب"), new DataColumn("اسم الحساب") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("SELECT AccountCode, AccountName FROM AccountsTree ORDER BY AccountCode ASC", conn))
                {
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                dt.Rows.Add(reader["AccountCode"], reader["AccountName"]);
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
            dgvAccountsTree.DataSource = dt;
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

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmdCheck = new SqliteCommand("SELECT COUNT(*) FROM AccountsTree WHERE AccountCode = @Code", conn))
                {
                    cmdCheck.Parameters.AddWithValue("@Code", code);
                    if (Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0)
                    {
                        MessageBox.Show("الكود ده مستخدم بالفعل لحساب تاني.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                using (SqliteCommand cmd = new SqliteCommand("INSERT INTO AccountsTree (AccountCode, AccountName) VALUES (@Code, @Name)", conn))
                {
                    cmd.Parameters.AddWithValue("@Code", code);
                    cmd.Parameters.AddWithValue("@Name", txtAccountName.Text.Trim());
                    try
                    {
                        cmd.ExecuteNonQuery();
                        MessageBox.Show("تم إضافة الحساب بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        ClearAccountInputs();
                        LoadAccountsTreeGrid();
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
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

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("UPDATE AccountsTree SET AccountName = @Name WHERE AccountCode = @Code", conn))
                {
                    cmd.Parameters.AddWithValue("@Name", txtAccountName.Text.Trim());
                    cmd.Parameters.AddWithValue("@Code", selectedAccountCode);
                    try
                    {
                        conn.Open();
                        cmd.ExecuteNonQuery();
                        MessageBox.Show("تم تعديل اسم الحساب بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        ClearAccountInputs();
                        LoadAccountsTreeGrid();
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void BtnDeleteAccount_Click(object sender, EventArgs e)
        {
            if (selectedAccountCode == -1)
            {
                MessageBox.Show("من فضلك اختر حساب من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                int usageCount = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM Expenses WHERE AccountCode = @Code", conn))
                {
                    cmd.Parameters.AddWithValue("@Code", selectedAccountCode);
                    usageCount += Convert.ToInt32(cmd.ExecuteScalar());
                }
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM CashMovements WHERE AccountCode = @Code", conn))
                {
                    cmd.Parameters.AddWithValue("@Code", selectedAccountCode);
                    usageCount += Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (usageCount > 0)
                {
                    MessageBox.Show($"لا يمكن حذف هذا الحساب لأنه مستخدم في {usageCount} حركة/حركات مسجّلة بالفعل.", "غير مسموح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show("هل أنت متأكد من حذف هذا الحساب نهائيًا؟", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                using (SqliteCommand cmd = new SqliteCommand("DELETE FROM AccountsTree WHERE AccountCode = @Code", conn))
                {
                    cmd.Parameters.AddWithValue("@Code", selectedAccountCode);
                    cmd.ExecuteNonQuery();
                }
            }

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
            string fromDateTime = dtpIncomeFrom.Value.ToString("yyyy-MM-dd") + " 00:00:00";
            string toDateTime = dtpIncomeTo.Value.ToString("yyyy-MM-dd") + " 23:59:59";
            string fromDateOnly = dtpIncomeFrom.Value.ToString("yyyy-MM-dd");
            string toDateOnly = dtpIncomeTo.Value.ToString("yyyy-MM-dd");

            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("البند"), new DataColumn("المبلغ") });

            decimal totalRevenue = 0, totalExpenses = 0;

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                decimal salesRevenue = 0, cogs = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Total) AS T, SUM(CostPrice * QuantitySold) AS C FROM Sales WHERE SaleDate BETWEEN @From AND @To", conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDateTime);
                    cmd.Parameters.AddWithValue("@To", toDateTime);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            salesRevenue = reader["T"] != DBNull.Value ? Convert.ToDecimal(reader["T"]) : 0;
                            cogs = reader["C"] != DBNull.Value ? Convert.ToDecimal(reader["C"]) : 0;
                        }
                    }
                }
                dt.Rows.Add("إيرادات مبيعات الموبايلات والإكسسوارات", salesRevenue.ToString("N2"));
                totalRevenue += salesRevenue;

                string revQuery = @"SELECT AC.AccountName, SUM(CM.Amount) AS Total
                    FROM CashMovements CM INNER JOIN AccountsTree AC ON CM.AccountCode = AC.AccountCode
                    WHERE CM.MovementType = 'قبض' AND AC.AccountCode >= 4000 AND AC.AccountCode < 5000
                      AND CM.MovementDate BETWEEN @From AND @To
                    GROUP BY AC.AccountCode, AC.AccountName ORDER BY AC.AccountCode";
                using (SqliteCommand cmd = new SqliteCommand(revQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDateOnly);
                    cmd.Parameters.AddWithValue("@To", toDateOnly);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            decimal amt = Convert.ToDecimal(reader["Total"]);
                            dt.Rows.Add(reader["AccountName"].ToString(), amt.ToString("N2"));
                            totalRevenue += amt;
                        }
                    }
                }

                dt.Rows.Add("إجمالي الإيرادات", totalRevenue.ToString("N2"));
                dt.Rows.Add("", "");
                dt.Rows.Add("تكلفة البضاعة المباعة", cogs.ToString("N2"));
                decimal grossProfit = totalRevenue - cogs;
                dt.Rows.Add("مجمل الربح", grossProfit.ToString("N2"));
                dt.Rows.Add("", "");

                var expenseTotals = new Dictionary<string, decimal>();
                var expenseOrder = new List<(int code, string name)>();

                string expQuery = @"SELECT A.AccountCode, A.AccountName, SUM(E.Amount) AS Total
                    FROM Expenses E INNER JOIN AccountsTree A ON E.AccountCode = A.AccountCode
                    WHERE E.ExpenseDate BETWEEN @From AND @To
                    GROUP BY A.AccountCode, A.AccountName";
                using (SqliteCommand cmd = new SqliteCommand(expQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDateTime);
                    cmd.Parameters.AddWithValue("@To", toDateTime);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int code = Convert.ToInt32(reader["AccountCode"]);
                            string name = reader["AccountName"].ToString();
                            decimal amt = Convert.ToDecimal(reader["Total"]);
                            string key = code + "|" + name;
                            if (!expenseTotals.ContainsKey(key)) { expenseTotals[key] = 0; expenseOrder.Add((code, name)); }
                            expenseTotals[key] += amt;
                        }
                    }
                }

                string movExpQuery = @"SELECT AC.AccountCode, AC.AccountName, SUM(CM.Amount) AS Total
                    FROM CashMovements CM INNER JOIN AccountsTree AC ON CM.AccountCode = AC.AccountCode
                    WHERE CM.MovementType = 'صرف' AND AC.AccountCode >= 5000 AND AC.AccountCode < 6000
                      AND CM.MovementDate BETWEEN @From AND @To
                    GROUP BY AC.AccountCode, AC.AccountName";
                using (SqliteCommand cmd = new SqliteCommand(movExpQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDateOnly);
                    cmd.Parameters.AddWithValue("@To", toDateOnly);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int code = Convert.ToInt32(reader["AccountCode"]);
                            string name = reader["AccountName"].ToString();
                            decimal amt = Convert.ToDecimal(reader["Total"]);
                            string key = code + "|" + name;
                            if (!expenseTotals.ContainsKey(key)) { expenseTotals[key] = 0; expenseOrder.Add((code, name)); }
                            expenseTotals[key] += amt;
                        }
                    }
                }

                foreach (var item in expenseOrder.OrderBy(x => x.code))
                {
                    string key = item.code + "|" + item.name;
                    decimal amt = expenseTotals[key];
                    dt.Rows.Add(item.name, amt.ToString("N2"));
                    totalExpenses += amt;
                }

                dt.Rows.Add("إجمالي المصروفات", totalExpenses.ToString("N2"));
                dt.Rows.Add("", "");
                decimal netProfit = grossProfit - totalExpenses;
                dt.Rows.Add("صافي الربح النهائي", netProfit.ToString("N2"));
            }

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
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("كود الحساب"), new DataColumn("اسم الحساب"), new DataColumn("مدين"), new DataColumn("دائن") });

            decimal totalDebit = 0, totalCredit = 0;

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                var accounts = new List<(int code, string name)>();
                using (SqliteCommand cmd = new SqliteCommand("SELECT AccountCode, AccountName FROM AccountsTree ORDER BY AccountCode", conn))
                {
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            accounts.Add((Convert.ToInt32(reader["AccountCode"]), reader["AccountName"].ToString()));
                    }
                }

                foreach (var acc in accounts)
                {
                    decimal debit = 0, credit = 0;

                    string matchingMethod = PaymentMethodAccountCodes.FirstOrDefault(x => x.Value == acc.code).Key;
                    if (matchingMethod != null)
                    {
                        using (SqliteCommand cmd = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn))
                        {
                            cmd.Parameters.AddWithValue("@Method", matchingMethod);
                            var res = cmd.ExecuteScalar();
                            debit = res != null ? Convert.ToDecimal(res) : 0;
                        }
                    }
                    else if (acc.code == 1200)
                    {
                        using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Quantity * Price) FROM Products", conn))
                        {
                            var res = cmd.ExecuteScalar();
                            debit = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                        }
                    }
                    else if (acc.code >= 4000 && acc.code < 5000)
                    {
                        using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE MovementType = 'قبض' AND AccountCode = @Code", conn))
                        {
                            cmd.Parameters.AddWithValue("@Code", acc.code);
                            var res = cmd.ExecuteScalar();
                            credit = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                        }
                    }
                    else if (acc.code >= 5000 && acc.code < 6000)
                    {
                        using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM Expenses WHERE AccountCode = @Code", conn))
                        {
                            cmd.Parameters.AddWithValue("@Code", acc.code);
                            var res = cmd.ExecuteScalar();
                            debit += (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                        }
                        using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE MovementType = 'صرف' AND AccountCode = @Code", conn))
                        {
                            cmd.Parameters.AddWithValue("@Code", acc.code);
                            var res = cmd.ExecuteScalar();
                            debit += (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                        }
                    }
                    else
                    {
                        decimal inAmount = 0, outAmount = 0;
                        using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE MovementType = 'قبض' AND AccountCode = @Code", conn))
                        {
                            cmd.Parameters.AddWithValue("@Code", acc.code);
                            var res = cmd.ExecuteScalar();
                            inAmount = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                        }
                        using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE MovementType = 'صرف' AND AccountCode = @Code", conn))
                        {
                            cmd.Parameters.AddWithValue("@Code", acc.code);
                            var res = cmd.ExecuteScalar();
                            outAmount = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                        }
                        decimal net = inAmount - outAmount;
                        if (net >= 0) credit = net; else debit = Math.Abs(net);
                    }

                    if (debit == 0 && credit == 0) continue;

                    dt.Rows.Add(acc.code, acc.name, debit == 0 ? "" : debit.ToString("N2"), credit == 0 ? "" : credit.ToString("N2"));
                    totalDebit += debit;
                    totalCredit += credit;
                }

                decimal allSales = 0, allCogs = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Total) AS T, SUM(CostPrice * QuantitySold) AS C FROM Sales", conn))
                {
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            allSales = reader["T"] != DBNull.Value ? Convert.ToDecimal(reader["T"]) : 0;
                            allCogs = reader["C"] != DBNull.Value ? Convert.ToDecimal(reader["C"]) : 0;
                        }
                    }
                }
                if (allSales > 0)
                {
                    dt.Rows.Add("", "إيرادات المبيعات (من جدول المبيعات مباشرة)", "", allSales.ToString("N2"));
                    totalCredit += allSales;
                }
                if (allCogs > 0)
                {
                    dt.Rows.Add("", "تكلفة البضاعة المباعة (تقديري)", allCogs.ToString("N2"), "");
                    totalDebit += allCogs;
                }
            }

            dt.Rows.Add("", "", "", "");
            dt.Rows.Add("", "الإجمالي", totalDebit.ToString("N2"), totalCredit.ToString("N2"));

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
