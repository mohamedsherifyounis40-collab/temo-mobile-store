using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // TreasuryPageControl: نسخة مستقلة من شاشتي "المصروفات" و"حركة القبض والصرف"
    // الموجودتين في Form1.cs (CreateExpensesDesign و CreateCashMovementsDesign)،
    // مع سويتش فوق للتبديل بينهم - زي ما اتفقنا (المبيعات لوحدها، والباقي هنا).
    //
    // ملحوظة مهمة: في التصميم الأصلي، جدول حركة القبض والصرف (dgvCashMovements)
    // معندوش CellClick خالص - التعديل والإلغاء كانوا بيتعملوا بس من جدول موحّد
    // في تبويب "العمليات اليومية". بما إننا هنا شاشة مستقلة، ضفنا CellClick
    // لجدولنا عشان "تعديل الحركة" و"إلغاء الحركة" يشتغلوا فعليًا.
    // ==========================================================================
    public partial class TreasuryPageControl : UserControl
    {
        private static readonly Color ColorPrimary = UIHelpers.ColorPrimary;
        private static readonly Color ColorSuccess = UIHelpers.ColorSuccess;
        private static readonly Color ColorDanger = UIHelpers.ColorDanger;
        private static readonly Color ColorWarning = UIHelpers.ColorWarning;
        private static readonly Color ColorBackground = UIHelpers.ColorBackground;

        // ---------- عناصر عامة ----------
        private Guna2ComboBox cmbTreasuryOperationType;
        private Panel pnlExpenseOps, pnlMovementOps;

        // ---------- المصروفات ----------
        private ComboBox cmbExpenseAccounts;
        private Guna2TextBox txtExpenseAmount;
        private Guna2Button btnSaveExpenseUpdate, btnEditExpenseMode, btnDeleteExpense;
        private DataGridView dgvExpenses;
        private int selectedExpenseID = -1;
        private DateTime selectedExpenseDate = DateTime.MinValue;

        // ---------- حركة القبض والصرف ----------
        private Guna2ComboBox cmbMovementType, cmbPaymentMethod, cmbMovementAccount;
        private Guna2TextBox txtMovementAmount, txtMovementReference, txtMovementDescription;
        private Guna2Button btnSaveMovementEdit, btnEditMovement, btnCancelMovement;
        private Label lblMethodBalance;
        private DataGridView dgvCashMovements;
        private int selectedMovementId = -1;

        public TreasuryPageControl()
        {
            this.Dock = DockStyle.Fill;
            this.AutoScroll = true;
            this.BackColor = ColorBackground;

            BuildUI();
            LoadAccountsIntoCombos();
            LoadExpensesData();
            LoadCashMovements();
            ApplyEmployeeRestrictionsIfNeeded();
        }

        // ==========================================================================
        // دالة عامة: تفتح تبويب "حركة قبض/صرف" مباشرة وتحدد النوع - مستخدمة من اختصارات F4/F5
        // ==========================================================================
        public void ShowMovementEntry(string movementType)
        {
            cmbTreasuryOperationType.SelectedIndex = 1;
            if (cmbMovementType.Items.Contains(movementType))
                cmbMovementType.SelectedItem = movementType;
        }

        // ==========================================================================
        // لو المستخدم موظف عادي: يقدر يسجّل مصروف/حركة بس، مايقدرش يعدّل أو يحذف أو يلغي
        // ==========================================================================
        private void ApplyEmployeeRestrictionsIfNeeded()
        {
            if (AuthManager.IsAdmin) return;

            btnEditExpenseMode.Enabled = false;
            btnSaveExpenseUpdate.Enabled = false;
            btnDeleteExpense.Enabled = false;

            btnEditMovement.Enabled = false;
            btnSaveMovementEdit.Enabled = false;
            btnCancelMovement.Enabled = false;
        }

        // ==========================================================================
        // الهيكل العام: سويتش فوق + بانلين يتبادلوا الظهور
        // ==========================================================================
        private void BuildUI()
        {
            Label lblOpType = new Label() { Text = "نوع العملية:", Location = new Point(20, 20), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            cmbTreasuryOperationType = new Guna2ComboBox() { Location = new Point(130, 17), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbTreasuryOperationType.Items.AddRange(new string[] { "مصروف عمومي 💸", "حركة قبض/صرف 💰" });
            cmbTreasuryOperationType.SelectedIndexChanged += CmbTreasuryOperationType_SelectedIndexChanged;

            pnlExpenseOps = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 660) };
            pnlMovementOps = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 660) };

            BuildExpensesPanel();
            BuildMovementsPanel();
            pnlMovementOps.Visible = false;

            this.Controls.AddRange(new Control[] { lblOpType, cmbTreasuryOperationType, pnlExpenseOps, pnlMovementOps });

            cmbTreasuryOperationType.SelectedIndex = 0;
        }

        private void CmbTreasuryOperationType_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = cmbTreasuryOperationType.SelectedIndex;
            pnlExpenseOps.Visible = idx == 0;
            pnlMovementOps.Visible = idx == 1;
        }

        // ==========================================================================
        // بانل المصروفات
        // ==========================================================================
        private void BuildExpensesPanel()
        {
            Guna2Panel gbAddExpense = new Guna2Panel() { Location = new Point(0, 0), Size = new Size(280, 360), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblExpTitleCard = new Label() { Text = "💸 تسجيل مصروف", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblExpAcc = new Label() { Text = "اختر بند الحساب المصروف:", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbExpenseAccounts = new ComboBox() { Location = new Point(20, 70), Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };

            Label lblExpAmount = new Label() { Text = "المبلغ المدفوع (ج.م):", Location = new Point(20, 108), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtExpenseAmount = new Guna2TextBox() { Location = new Point(20, 128), Width = 240, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Guna2Button btnAddExpense = new Guna2Button() { Text = "تسجيل مصروف جديد 💸", Location = new Point(20, 168), Width = 240, Height = 38, FillColor = ColorSuccess, Font = new Font("Segoe UI", 9, FontStyle.Bold), BorderRadius = 10 };
            btnAddExpense.Click += BtnAddExpense_Click;

            btnEditExpenseMode = new Guna2Button() { Text = "تعديل البند المحدّد", Location = new Point(20, 214), Width = 240, Height = 34, FillColor = ColorPrimary, BorderRadius = 9 };
            btnEditExpenseMode.Click += BtnEditExpenseMode_Click;

            btnSaveExpenseUpdate = new Guna2Button() { Text = "حفظ تعديل المصروف 💾", Location = new Point(20, 254), Width = 240, Height = 34, FillColor = ColorWarning, Font = new Font("Segoe UI", 9, FontStyle.Bold), Enabled = false, BorderRadius = 9 };
            btnSaveExpenseUpdate.Click += BtnSaveExpenseUpdate_Click;

            btnDeleteExpense = new Guna2Button() { Text = "حذف بند المصروف", Location = new Point(20, 296), Width = 240, Height = 32, FillColor = ColorDanger, BorderRadius = 9 };
            btnDeleteExpense.Click += BtnDeleteExpense_Click;

            gbAddExpense.Controls.AddRange(new Control[] { lblExpTitleCard, lblExpAcc, cmbExpenseAccounts, lblExpAmount, txtExpenseAmount, btnAddExpense, btnEditExpenseMode, btnSaveExpenseUpdate, btnDeleteExpense });

            Guna2Panel pnlExpGridCard = new Guna2Panel() { Location = new Point(300, 0), Size = new Size(800, 645), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblExpTitle = new Label() { Text = "📖 دفتر حركات المصروفات العمومية", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };
            dgvExpenses = new DataGridView() { Location = new Point(20, 50), Size = new Size(760, 580), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvExpenses.CellClick += DgvExpenses_CellClick;
            StyleDataGridView(dgvExpenses);
            pnlExpGridCard.Controls.AddRange(new Control[] { lblExpTitle, dgvExpenses });

            pnlExpenseOps.Controls.AddRange(new Control[] { gbAddExpense, pnlExpGridCard });
        }

        // ==========================================================================
        // بانل حركة القبض والصرف
        // ==========================================================================
        private void BuildMovementsPanel()
        {
            Guna2Panel gbMovement = new Guna2Panel() { Location = new Point(0, 0), Size = new Size(280, 645), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblMovementTitle = new Label() { Text = "💰 حركة قبض / صرف", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblType = new Label() { Text = "نوع الحركة:", Location = new Point(20, 55), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbMovementType = new Guna2ComboBox() { Location = new Point(20, 75), Width = 240, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbMovementType.Items.AddRange(new string[] { "قبض", "صرف" });

            Label lblMethod = new Label() { Text = "وسيلة الدفع:", Location = new Point(20, 113), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbPaymentMethod = new Guna2ComboBox() { Location = new Point(20, 133), Width = 240, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbPaymentMethod.Items.AddRange(UIHelpers.PaymentMethods);
            cmbPaymentMethod.SelectedIndexChanged += CmbPaymentMethod_SelectedIndexChanged;

            lblMethodBalance = new Label() { Text = "الرصيد الحالي: --", Location = new Point(20, 171), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblAmount = new Label() { Text = "المبلغ:", Location = new Point(20, 200), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtMovementAmount = new Guna2TextBox() { Location = new Point(20, 220), Width = 240, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblRef = new Label() { Text = "رقم مرجعي (اختياري):", Location = new Point(20, 258), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtMovementReference = new Guna2TextBox() { Location = new Point(20, 278), Width = 240, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblDesc = new Label() { Text = "الوصف:", Location = new Point(20, 316), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtMovementDescription = new Guna2TextBox() { Location = new Point(20, 336), Width = 240, Height = 55, Multiline = true, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblMovementAccount = new Label() { Text = "الحساب المرتبط (اختياري):", Location = new Point(20, 400), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbMovementAccount = new Guna2ComboBox() { Location = new Point(20, 420), Width = 240, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };

            Guna2Button btnAddMovement = new Guna2Button() { Text = "تسجيل الحركة ✅", Location = new Point(20, 462), Width = 240, Height = 38, FillColor = ColorSuccess, BorderRadius = 10 };
            btnAddMovement.Click += BtnAddMovement_Click;

            btnEditMovement = new Guna2Button() { Text = "تعديل الحركة المحددة ✏️", Location = new Point(20, 505), Width = 240, Height = 34, FillColor = ColorPrimary, BorderRadius = 9 };
            btnEditMovement.Click += BtnEditMovement_Click;

            btnSaveMovementEdit = new Guna2Button() { Text = "حفظ تعديل الحركة 💾", Location = new Point(20, 544), Width = 240, Height = 34, FillColor = ColorWarning, Enabled = false, BorderRadius = 9 };
            btnSaveMovementEdit.Click += BtnSaveMovementEdit_Click;

            btnCancelMovement = new Guna2Button() { Text = "إلغاء الحركة المحددة ❌", Location = new Point(20, 583), Width = 240, Height = 34, FillColor = ColorDanger, BorderRadius = 9 };
            btnCancelMovement.Click += BtnCancelMovement_Click;

            gbMovement.Controls.AddRange(new Control[] { lblMovementTitle, lblType, cmbMovementType, lblMethod, cmbPaymentMethod, lblMethodBalance, lblAmount, txtMovementAmount, lblRef, txtMovementReference, lblDesc, txtMovementDescription, lblMovementAccount, cmbMovementAccount, btnAddMovement, btnEditMovement, btnSaveMovementEdit, btnCancelMovement });

            Guna2Panel pnlMovGridCard = new Guna2Panel() { Location = new Point(300, 0), Size = new Size(800, 645), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblMovGridTitle = new Label() { Text = "📖 سجل حركات القبض والصرف", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };
            dgvCashMovements = new DataGridView() { Location = new Point(20, 50), Size = new Size(760, 580), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvCashMovements.CellClick += DgvCashMovements_CellClick;
            StyleDataGridView(dgvCashMovements);
            pnlMovGridCard.Controls.AddRange(new Control[] { lblMovGridTitle, dgvCashMovements });

            pnlMovementOps.Controls.AddRange(new Control[] { gbMovement, pnlMovGridCard });
        }

        // ==========================================================================
        // نفس تنسيق الجداول المستخدم في كل شاشات Form1.cs
        // ==========================================================================
        private void StyleDataGridView(DataGridView dgv) => UIHelpers.StyleDataGridView(dgv);

        // ==========================================================================
        // تحميل شجرة الحسابات في الكومبو بوكسين (المصروفات + حركة القبض/الصرف)
        // ==========================================================================
        private void LoadAccountsIntoCombos()
        {
            string query = "SELECT AccountCode, AccountName FROM AccountsTree ORDER BY AccountCode";
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    try
                    {
                        conn.Open();
                        DataTable dtExpense = new DataTable();
                        dtExpense.Load(cmd.ExecuteReader());
                        cmbExpenseAccounts.DataSource = dtExpense;
                        cmbExpenseAccounts.DisplayMember = "AccountName";
                        cmbExpenseAccounts.ValueMember = "AccountCode";

                        DataTable dtMovement = dtExpense.Copy();
                        cmbMovementAccount.DataSource = dtMovement;
                        cmbMovementAccount.DisplayMember = "AccountName";
                        cmbMovementAccount.ValueMember = "AccountCode";
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        // ==========================================================================
        // فحص هل تاريخ معين/النهاردة تم إقفاله بالفعل
        // ==========================================================================
        private bool IsDateClosed(DateTime date) => UIHelpers.IsDateClosed(date);

        private bool IsTodayClosed() => UIHelpers.IsTodayClosed();

        // ==========================================================================
        // المصروفات: إضافة / تعديل / حذف
        // ==========================================================================
        private void BtnAddExpense_Click(object sender, EventArgs e)
        {
            if (IsTodayClosed())
            {
                MessageBox.Show("تم إقفال اليوم بالفعل، لا يمكن تسجيل مصروفات جديدة.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cmbExpenseAccounts.SelectedValue == null || !decimal.TryParse(txtExpenseAmount.Text, out decimal amount))
            {
                MessageBox.Show("من فضلك اختر الحساب واكتب المبلغ بشكل صحيح!", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int selectedCode = Convert.ToInt32(cmbExpenseAccounts.SelectedValue);
            string query = "INSERT INTO Expenses (AccountCode, Amount) VALUES (@AccountCode, @Amount)";
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@AccountCode", selectedCode);
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    try
                    {
                        conn.Open(); cmd.ExecuteNonQuery();
                        ClearExpenseInputs();
                        LoadExpensesData();
                        MessageBox.Show("تم تسجيل المصروف بنجاح بالتاريخ والوقت اللحظي!", "تم التسجيل", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void DgvExpenses_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                DataGridViewRow row = dgvExpenses.Rows[e.RowIndex];
                selectedExpenseID = Convert.ToInt32(row.Cells["رقم الحركة"].Value);
                selectedExpenseDate = DateTime.Parse(row.Cells["التاريخ والوقت ⏰"].Value.ToString());
                cmbExpenseAccounts.SelectedValue = Convert.ToInt32(row.Cells["كود الحساب"].Value);
                txtExpenseAmount.Text = row.Cells["المبلغ ج.م"].Value.ToString();
                btnSaveExpenseUpdate.Enabled = false;
            }
        }

        private void BtnEditExpenseMode_Click(object sender, EventArgs e)
        {
            if (selectedExpenseID == -1)
            {
                MessageBox.Show("من فضلك اختر حركة مصروف من الجدول أولاً لتعديلها!", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            btnSaveExpenseUpdate.Enabled = true;
        }

        private void BtnSaveExpenseUpdate_Click(object sender, EventArgs e)
        {
            if (selectedExpenseID == -1 || !decimal.TryParse(txtExpenseAmount.Text, out decimal amount)) return;

            if (IsDateClosed(selectedExpenseDate))
            {
                MessageBox.Show("لا يمكن تعديل مصروف تابع ليوم تم إقفاله بالفعل.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int selectedCode = Convert.ToInt32(cmbExpenseAccounts.SelectedValue);
            string query = "UPDATE Expenses SET AccountCode = @AccountCode, Amount = @Amount WHERE ExpenseID = @ExpenseID";
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@AccountCode", selectedCode);
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    cmd.Parameters.AddWithValue("@ExpenseID", selectedExpenseID);
                    try
                    {
                        conn.Open(); cmd.ExecuteNonQuery();
                        btnSaveExpenseUpdate.Enabled = false;
                        ClearExpenseInputs();
                        LoadExpensesData();
                        MessageBox.Show("تم تعديل قيمة المصروف وتحديث الخلاصة المالية بنجاح!", "تم التعديل", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void BtnDeleteExpense_Click(object sender, EventArgs e)
        {
            if (selectedExpenseID == -1) return;

            if (IsDateClosed(selectedExpenseDate))
            {
                MessageBox.Show("لا يمكن حذف مصروف تابع ليوم تم إقفاله بالفعل.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("هل أنت متأكد من حذف حركة المصروف هذه نهائياً؟", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                string query = "DELETE FROM Expenses WHERE ExpenseID = @ExpenseID";
                using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
                {
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@ExpenseID", selectedExpenseID);
                        try
                        {
                            conn.Open(); cmd.ExecuteNonQuery();
                            ClearExpenseInputs();
                            LoadExpensesData();
                            MessageBox.Show("تم حذف حركة المصروف بنجاح!", "تم الحذف", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                    }
                }
            }
        }

        private void ClearExpenseInputs()
        {
            selectedExpenseID = -1;
            txtExpenseAmount.Clear();
            btnSaveExpenseUpdate.Enabled = false;
        }

        private void LoadExpensesData()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] {
                new DataColumn("رقم الحركة"),
                new DataColumn("كود الحساب"),
                new DataColumn("اسم بند المصروف"),
                new DataColumn("المبلغ ج.م"),
                new DataColumn("التاريخ والوقت ⏰")
            });

            string query = @"SELECT E.ExpenseID, E.AccountCode, A.AccountName, E.Amount, E.ExpenseDate 
                             FROM Expenses E 
                             INNER JOIN AccountsTree A ON E.AccountCode = A.AccountCode 
                             ORDER BY E.ExpenseID ASC";

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                dt.Rows.Add(reader["ExpenseID"], reader["AccountCode"], reader["AccountName"], reader["Amount"], reader["ExpenseDate"]);
                        }
                        dgvExpenses.DataSource = dt;
                        if (dgvExpenses.Columns["كود الحساب"] != null) dgvExpenses.Columns["كود الحساب"].Visible = false;
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        // ==========================================================================
        // حركة القبض والصرف: إضافة / تعديل / إلغاء
        // ==========================================================================
        private void BtnAddMovement_Click(object sender, EventArgs e)
        {
            if (IsTodayClosed())
            {
                MessageBox.Show("تم إقفال اليوم بالفعل، لا يمكن تسجيل حركات جديدة.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cmbMovementType.SelectedItem == null || cmbPaymentMethod.SelectedItem == null || !decimal.TryParse(txtMovementAmount.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("من فضلك اختر نوع الحركة ووسيلة الدفع وأدخل مبلغ صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string type = cmbMovementType.SelectedItem.ToString();
            string method = cmbPaymentMethod.SelectedItem.ToString();

            try
            {
                using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
                {
                    conn.Open();
                    using (SqliteTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            decimal currentBalance = 0;
                            using (SqliteCommand cmdBalance = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn, transaction))
                            {
                                cmdBalance.Parameters.AddWithValue("@Method", method);
                                var result = cmdBalance.ExecuteScalar();
                                if (result != null) currentBalance = Convert.ToDecimal(result);
                            }

                            if (type == "صرف" && amount > currentBalance)
                            {
                                transaction.Rollback();
                                MessageBox.Show($"الرصيد الحالي في \"{method}\" هو {currentBalance} فقط، لا يمكن صرف مبلغ أكبر منه.", "رصيد غير كافٍ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }

                            object accountCodeParam = (cmbMovementAccount.SelectedValue != null) ? (object)Convert.ToInt32(cmbMovementAccount.SelectedValue) : DBNull.Value;

                            using (SqliteCommand cmdInsert = new SqliteCommand(
                                "INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode) VALUES (@Date, @Type, @Method, @Amount, @Ref, @Desc, @CreatedAt, @AccountCode)", conn, transaction))
                            {
                                cmdInsert.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                                cmdInsert.Parameters.AddWithValue("@Type", type);
                                cmdInsert.Parameters.AddWithValue("@Method", method);
                                cmdInsert.Parameters.AddWithValue("@Amount", amount);
                                cmdInsert.Parameters.AddWithValue("@Ref", txtMovementReference.Text);
                                cmdInsert.Parameters.AddWithValue("@Desc", txtMovementDescription.Text);
                                cmdInsert.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmdInsert.Parameters.AddWithValue("@AccountCode", accountCodeParam);
                                cmdInsert.ExecuteNonQuery();
                            }

                            decimal newBalance = type == "قبض" ? currentBalance + amount : currentBalance - amount;
                            using (SqliteCommand cmdUpdate = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = @NewBalance WHERE PaymentMethod = @Method", conn, transaction))
                            {
                                cmdUpdate.Parameters.AddWithValue("@NewBalance", newBalance);
                                cmdUpdate.Parameters.AddWithValue("@Method", method);
                                cmdUpdate.ExecuteNonQuery();
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("حصل خطأ أثناء تسجيل الحركة: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show("تم تسجيل الحركة بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtMovementAmount.Clear();
            txtMovementReference.Clear();
            txtMovementDescription.Clear();
            cmbMovementAccount.SelectedIndex = -1;
            LoadCashMovements();
            CmbPaymentMethod_SelectedIndexChanged(null, EventArgs.Empty);
        }

        private void LoadCashMovements()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("Id"), new DataColumn("النوع"), new DataColumn("الوسيلة"), new DataColumn("المبلغ"), new DataColumn("المرجع"), new DataColumn("الوصف"), new DataColumn("التاريخ والوقت") });

            string query = "SELECT Id, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt FROM CashMovements ORDER BY Id DESC";
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                dt.Rows.Add(reader["Id"], reader["MovementType"], reader["PaymentMethod"], reader["Amount"], reader["ReferenceNumber"], reader["Description"], reader["CreatedAt"]);
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
            dgvCashMovements.DataSource = dt;
            if (dgvCashMovements.Columns["Id"] != null) dgvCashMovements.Columns["Id"].Visible = false;
        }

        private void CmbPaymentMethod_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbPaymentMethod.SelectedItem == null) return;

            string method = cmbPaymentMethod.SelectedItem.ToString();
            decimal balance = 0;

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                string query = "SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method";
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Method", method);
                    conn.Open();
                    var result = cmd.ExecuteScalar();
                    if (result != null) balance = Convert.ToDecimal(result);
                }
            }

            lblMethodBalance.Text = $"الرصيد الحالي في \"{method}\": {balance} جنيه";
        }

        // ==========================================================================
        // اختيار حركة من الجدول (لازمة للتعديل/الإلغاء) - مش موجودة في التصميم الأصلي
        // كوظيفة مباشرة على نفس الجدول، فضفناها هنا عشان الشاشة المستقلة تشتغل صح
        // ==========================================================================
        private void DgvCashMovements_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            int movementId = Convert.ToInt32(dgvCashMovements.Rows[e.RowIndex].Cells["Id"].Value);
            LoadMovementIntoFields(movementId);
        }

        private void LoadMovementIntoFields(int movementId)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("SELECT MovementType, PaymentMethod, Amount, ReferenceNumber, Description, AccountCode FROM CashMovements WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", movementId);
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                selectedMovementId = movementId;
                                cmbMovementType.Text = reader["MovementType"].ToString();
                                cmbPaymentMethod.Text = reader["PaymentMethod"].ToString();
                                txtMovementAmount.Text = reader["Amount"].ToString();
                                txtMovementReference.Text = reader["ReferenceNumber"]?.ToString();
                                txtMovementDescription.Text = reader["Description"]?.ToString();
                                if (reader["AccountCode"] != DBNull.Value)
                                    cmbMovementAccount.SelectedValue = Convert.ToInt32(reader["AccountCode"]);
                                else
                                    cmbMovementAccount.SelectedIndex = -1;
                                btnSaveMovementEdit.Enabled = false;
                            }
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void BtnEditMovement_Click(object sender, EventArgs e)
        {
            if (selectedMovementId == -1)
            {
                MessageBox.Show("من فضلك اختر حركة من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string movementDateStr = null;
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT MovementDate FROM CashMovements WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", selectedMovementId);
                    var res = cmd.ExecuteScalar();
                    if (res != null) movementDateStr = res.ToString();
                }
            }

            if (movementDateStr == null)
            {
                MessageBox.Show("لم يتم العثور على الحركة.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (IsDateClosed(DateTime.Parse(movementDateStr).Date))
            {
                MessageBox.Show("لا يمكن تعديل حركة تابعة ليوم تم إقفاله بالفعل.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnSaveMovementEdit.Enabled = true;
        }

        private void BtnSaveMovementEdit_Click(object sender, EventArgs e)
        {
            if (selectedMovementId == -1)
            {
                MessageBox.Show("من فضلك اختر حركة من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cmbMovementType.SelectedItem == null || cmbPaymentMethod.SelectedItem == null || !decimal.TryParse(txtMovementAmount.Text, out decimal newAmount) || newAmount <= 0)
            {
                MessageBox.Show("من فضلك اختر نوع الحركة ووسيلة الدفع وأدخل مبلغ صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string newType = cmbMovementType.SelectedItem.ToString();
            string newMethod = cmbPaymentMethod.SelectedItem.ToString();

            try
            {
                using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
                {
                    conn.Open();
                    using (SqliteTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            string oldType = null, oldMethod = null, movementDateStr = null;
                            decimal oldAmount = 0;
                            using (SqliteCommand cmd = new SqliteCommand("SELECT MovementType, PaymentMethod, Amount, MovementDate FROM CashMovements WHERE Id = @Id", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Id", selectedMovementId);
                                using (SqliteDataReader reader = cmd.ExecuteReader())
                                {
                                    if (!reader.Read())
                                    {
                                        transaction.Rollback();
                                        MessageBox.Show("لم يتم العثور على الحركة.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        return;
                                    }
                                    oldType = reader["MovementType"].ToString();
                                    oldMethod = reader["PaymentMethod"].ToString();
                                    oldAmount = Convert.ToDecimal(reader["Amount"]);
                                    movementDateStr = reader["MovementDate"].ToString();
                                }
                            }

                            if (IsDateClosed(DateTime.Parse(movementDateStr).Date))
                            {
                                transaction.Rollback();
                                MessageBox.Show("لا يمكن تعديل حركة تابعة ليوم تم إقفاله بالفعل.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }

                            decimal oldMethodBalance = 0;
                            using (SqliteCommand cmd = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Method", oldMethod);
                                var res = cmd.ExecuteScalar();
                                if (res != null) oldMethodBalance = Convert.ToDecimal(res);
                            }
                            decimal revertedOldBalance = oldType == "قبض" ? oldMethodBalance - oldAmount : oldMethodBalance + oldAmount;

                            decimal newMethodBalance = revertedOldBalance;
                            if (newMethod != oldMethod)
                            {
                                using (SqliteCommand cmd = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@Method", newMethod);
                                    var res = cmd.ExecuteScalar();
                                    if (res != null) newMethodBalance = Convert.ToDecimal(res);
                                }
                            }

                            if (newType == "صرف" && newAmount > newMethodBalance)
                            {
                                transaction.Rollback();
                                MessageBox.Show($"الرصيد المتاح في \"{newMethod}\" هو {newMethodBalance} فقط، لا يمكن صرف مبلغ أكبر منه.", "رصيد غير كافٍ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }

                            using (SqliteCommand cmd = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = @Balance WHERE PaymentMethod = @Method", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Balance", revertedOldBalance);
                                cmd.Parameters.AddWithValue("@Method", oldMethod);
                                cmd.ExecuteNonQuery();
                            }

                            decimal finalNewBalance = newType == "قبض" ? newMethodBalance + newAmount : newMethodBalance - newAmount;
                            using (SqliteCommand cmd = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = @Balance WHERE PaymentMethod = @Method", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Balance", finalNewBalance);
                                cmd.Parameters.AddWithValue("@Method", newMethod);
                                cmd.ExecuteNonQuery();
                            }

                            object accountCodeParam = (cmbMovementAccount.SelectedValue != null) ? (object)Convert.ToInt32(cmbMovementAccount.SelectedValue) : DBNull.Value;

                            using (SqliteCommand cmd = new SqliteCommand(
                                "UPDATE CashMovements SET MovementType = @Type, PaymentMethod = @Method, Amount = @Amount, ReferenceNumber = @Ref, Description = @Desc, AccountCode = @AccountCode WHERE Id = @Id", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Type", newType);
                                cmd.Parameters.AddWithValue("@Method", newMethod);
                                cmd.Parameters.AddWithValue("@Amount", newAmount);
                                cmd.Parameters.AddWithValue("@Ref", txtMovementReference.Text);
                                cmd.Parameters.AddWithValue("@Desc", txtMovementDescription.Text);
                                cmd.Parameters.AddWithValue("@AccountCode", accountCodeParam);
                                cmd.Parameters.AddWithValue("@Id", selectedMovementId);
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("حصل خطأ أثناء حفظ تعديل الحركة: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show("تم حفظ تعديل الحركة بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            selectedMovementId = -1;
            btnSaveMovementEdit.Enabled = false;
            txtMovementAmount.Clear();
            txtMovementReference.Clear();
            txtMovementDescription.Clear();
            LoadCashMovements();
            CmbPaymentMethod_SelectedIndexChanged(null, EventArgs.Empty);
        }

        private void BtnCancelMovement_Click(object sender, EventArgs e)
        {
            if (selectedMovementId == -1)
            {
                MessageBox.Show("من فضلك اختر حركة من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string type = null, method = null, movementDateStr = null;
            decimal amount = 0;
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT MovementType, PaymentMethod, Amount, MovementDate FROM CashMovements WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", selectedMovementId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            MessageBox.Show("لم يتم العثور على الحركة.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        type = reader["MovementType"].ToString();
                        method = reader["PaymentMethod"].ToString();
                        amount = Convert.ToDecimal(reader["Amount"]);
                        movementDateStr = reader["MovementDate"].ToString();
                    }
                }
            }

            if (IsDateClosed(DateTime.Parse(movementDateStr).Date))
            {
                MessageBox.Show("لا يمكن إلغاء حركة تابعة ليوم تم إقفاله بالفعل.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("هل أنت متأكد من إلغاء هذه الحركة؟ سيتم عكس أثرها على الرصيد.", "تأكيد الإلغاء", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
                {
                    conn.Open();
                    using (SqliteTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            decimal currentBalance = 0;
                            using (SqliteCommand cmdBalance = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn, transaction))
                            {
                                cmdBalance.Parameters.AddWithValue("@Method", method);
                                var result = cmdBalance.ExecuteScalar();
                                if (result != null) currentBalance = Convert.ToDecimal(result);
                            }

                            decimal newBalance = type == "قبض" ? currentBalance - amount : currentBalance + amount;
                            using (SqliteCommand cmdUpdate = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = @NewBalance WHERE PaymentMethod = @Method", conn, transaction))
                            {
                                cmdUpdate.Parameters.AddWithValue("@NewBalance", newBalance);
                                cmdUpdate.Parameters.AddWithValue("@Method", method);
                                cmdUpdate.ExecuteNonQuery();
                            }

                            using (SqliteCommand cmdDelete = new SqliteCommand("DELETE FROM CashMovements WHERE Id = @Id", conn, transaction))
                            {
                                cmdDelete.Parameters.AddWithValue("@Id", selectedMovementId);
                                cmdDelete.ExecuteNonQuery();
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("حصل خطأ أثناء إلغاء الحركة: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show("تم إلغاء الحركة بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            selectedMovementId = -1;
            btnSaveMovementEdit.Enabled = false;
            LoadCashMovements();
            CmbPaymentMethod_SelectedIndexChanged(null, EventArgs.Empty);
        }
    }
}
