using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using TemoStore.Core.Commands;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // CustomersPageControl: نسخة مستقلة تمامًا من شاشة "العملاء" الموجودة في
    // Form1.cs (CreateCustomersDesign). نفس مبدأ الشاشات السابقة: منفصل بالكامل
    // عن Form1.cs، بيقرا ويكتب على نفس قاعدة البيانات بالظبط.
    // ==========================================================================
    public partial class CustomersPageControl : UserControl
    {
        private static readonly Color ColorSuccess = UIHelpers.ColorSuccess;
        private static readonly Color ColorDanger = UIHelpers.ColorDanger;
        private static readonly Color ColorWarning = UIHelpers.ColorWarning;
        private static readonly Color ColorPrimary = UIHelpers.ColorPrimary;
        private static readonly Color ColorBackground = UIHelpers.ColorBackground;

        private Guna2TextBox txtCustomerName, txtCustomerPhone, txtCollectAmount;
        private Guna2Button btnSaveCustomerEdit;
        private Guna2ComboBox cmbCollectCustomer, cmbCollectPaymentMethod;
        private DataGridView dgvCustomers, dgvCustomerStatement;

        private int selectedCustomerId = -1;

        public CustomersPageControl()
        {
            this.Dock = DockStyle.Fill;
            this.Size = new Size(1150, 1150); // مقاس مبدئي واقعي قبل بناء الشاشة، عشان حسابات Anchor متبقاش غلط (راجع نفس التعليق في SalesPageControl)
            this.AutoScroll = true;
            this.BackColor = ColorBackground;

            BuildUI();
            LoadCustomersGrid();
            LoadCollectCustomerCombo();
        }

        private void BuildUI()
        {
            Guna2Panel gbCustomer = new Guna2Panel() { Location = new Point(20, 20), Size = new Size(300, 230), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblCustomerTitle = new Label() { Text = "👤 إضافة / تعديل عميل", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = ColorPrimary };
            Label lblCustomerName = new Label() { Text = "اسم العميل:", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtCustomerName = new Guna2TextBox() { Location = new Point(20, 70), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };
            Label lblCustomerPhone = new Label() { Text = "رقم التليفون:", Location = new Point(20, 108), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtCustomerPhone = new Guna2TextBox() { Location = new Point(20, 128), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Guna2Button btnAddCustomer = new Guna2Button() { Text = "إضافة عميل جديد ✅", Location = new Point(20, 166), Width = 260, Height = 34, FillColor = ColorSuccess, BorderRadius = 9 };
            btnAddCustomer.Click += BtnAddCustomer_Click;

            gbCustomer.Controls.AddRange(new Control[] { lblCustomerTitle, lblCustomerName, txtCustomerName, lblCustomerPhone, txtCustomerPhone, btnAddCustomer });

            Guna2Panel gbCustomerActions = new Guna2Panel() { Location = new Point(20, 260), Size = new Size(300, 110), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            btnSaveCustomerEdit = new Guna2Button() { Text = "حفظ التعديل 💾", Location = new Point(20, 18), Width = 260, Height = 32, FillColor = ColorWarning, Enabled = false, BorderRadius = 9 };
            btnSaveCustomerEdit.Click += BtnSaveCustomerEdit_Click;

            Guna2Button btnDeleteCustomer = new Guna2Button() { Text = "حذف العميل ❌", Location = new Point(20, 60), Width = 260, Height = 32, FillColor = ColorDanger, BorderRadius = 9 };
            btnDeleteCustomer.Click += BtnDeleteCustomer_Click;

            gbCustomerActions.Controls.AddRange(new Control[] { btnSaveCustomerEdit, btnDeleteCustomer });

            Guna2Panel gbCollect = new Guna2Panel() { Location = new Point(20, 385), Size = new Size(300, 300), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblCollectTitle = new Label() { Text = "💰 تحصيل من عميل (سداد دين)", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = ColorPrimary };
            Label lblCollectCustomer = new Label() { Text = "العميل:", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbCollectCustomer = new Guna2ComboBox() { Location = new Point(20, 70), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };

            Label lblCollectMethod = new Label() { Text = "وسيلة التحصيل:", Location = new Point(20, 108), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbCollectPaymentMethod = new Guna2ComboBox() { Location = new Point(20, 128), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbCollectPaymentMethod.Items.AddRange(UIHelpers.PaymentMethods);

            Label lblCollectAmount = new Label() { Text = "المبلغ المحصّل:", Location = new Point(20, 166), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtCollectAmount = new Guna2TextBox() { Location = new Point(20, 186), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Guna2Button btnCollect = new Guna2Button() { Text = "تسجيل التحصيل ✅", Location = new Point(20, 226), Width = 260, Height = 38, FillColor = ColorSuccess, BorderRadius = 10 };
            btnCollect.Click += BtnCollectFromCustomer_Click;

            gbCollect.Controls.AddRange(new Control[] { lblCollectTitle, lblCollectCustomer, cmbCollectCustomer, lblCollectMethod, cmbCollectPaymentMethod, lblCollectAmount, txtCollectAmount, btnCollect });

            // الكارتين دول واقفين فوق بعض عموديًا، فبنمدّهم عرضًا بس (Top|Left|Right) من غير Bottom عشان محدش يغطي التاني
            AnchorStyles widenAnchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Guna2Panel pnlGridCard = new Guna2Panel() { Location = new Point(340, 20), Size = new Size(780, 320), Anchor = widenAnchor, FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblCustomersGridTitle = new Label() { Text = "👥 العملاء وأرصدتهم", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ColorPrimary };
            dgvCustomers = new DataGridView() { Location = new Point(20, 50), Size = new Size(740, 255), Anchor = widenAnchor, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvCustomers.CellClick += DgvCustomers_CellClick;
            StyleDataGridView(dgvCustomers);
            pnlGridCard.Controls.AddRange(new Control[] { lblCustomersGridTitle, dgvCustomers });

            Guna2Panel pnlStatementCard = new Guna2Panel() { Location = new Point(340, 355), Size = new Size(780, 310), Anchor = widenAnchor, FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblCustomerStatementTitle = new Label() { Text = "📋 كشف حساب العميل المحدد (دوس على أي صف فوق)", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ColorPrimary };
            dgvCustomerStatement = new DataGridView() { Location = new Point(20, 50), Size = new Size(740, 245), Anchor = widenAnchor, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            StyleDataGridView(dgvCustomerStatement);
            pnlStatementCard.Controls.AddRange(new Control[] { lblCustomerStatementTitle, dgvCustomerStatement });

            this.Controls.AddRange(new Control[] { gbCustomer, gbCustomerActions, gbCollect, pnlGridCard, pnlStatementCard });
        }

        private void StyleDataGridView(DataGridView dgv) => UIHelpers.StyleDataGridView(dgv);

        private void LoadCollectCustomerCombo()
        {
            if (cmbCollectCustomer == null) return;
            try
            {
                DataTable dt = CustomersRepository.GetCollectCustomerCombo();
                cmbCollectCustomer.DataSource = dt;
                cmbCollectCustomer.DisplayMember = "CustomerName";
                cmbCollectCustomer.ValueMember = "CustomerId";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void LoadCustomersGrid()
        {
            if (dgvCustomers == null) return;
            try
            {
                DataTable dt = CustomersRepository.GetCustomersWithBalances();
                dgvCustomers.DataSource = dt;
                if (dgvCustomers.Columns["CustomerId"] != null) dgvCustomers.Columns["CustomerId"].Visible = false;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        // بيدوّر على عميل برقمه، يحدده في الجدول، ويجيب بياناته وكشف حسابه (مستخدمة من نتيجة البحث الشامل Ctrl+F)
        public void HighlightCustomer(string customerIdText)
        {
            if (!int.TryParse(customerIdText, out int customerId)) return;
            foreach (DataGridViewRow row in dgvCustomers.Rows)
            {
                if (row.Cells["CustomerId"].Value != null && Convert.ToInt32(row.Cells["CustomerId"].Value) == customerId)
                {
                    dgvCustomers.ClearSelection();
                    row.Selected = true;
                    dgvCustomers.CurrentCell = row.Cells[0];
                    dgvCustomers.FirstDisplayedScrollingRowIndex = row.Index;
                    DgvCustomers_CellClick(dgvCustomers, new DataGridViewCellEventArgs(0, row.Index));
                    return;
                }
            }
        }

        private void DgvCustomers_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvCustomers.Rows[e.RowIndex];
            selectedCustomerId = Convert.ToInt32(row.Cells["CustomerId"].Value);
            txtCustomerName.Text = row.Cells["اسم العميل"].Value.ToString();
            txtCustomerPhone.Text = row.Cells["التليفون"].Value?.ToString();
            btnSaveCustomerEdit.Enabled = true;

            LoadCustomerStatement(selectedCustomerId);
        }

        private void LoadCustomerStatement(int customerId)
        {
            try
            {
                dgvCustomerStatement.DataSource = CustomersRepository.GetCustomerStatement(customerId);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnAddCustomer_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCustomerName.Text))
            {
                MessageBox.Show("من فضلك أدخل اسم العميل.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                CustomersRepository.AddCustomer(txtCustomerName.Text.Trim(), string.IsNullOrWhiteSpace(txtCustomerPhone.Text) ? null : txtCustomerPhone.Text.Trim());
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

            MessageBox.Show("تم إضافة العميل بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearCustomerInputs();
            LoadCustomersGrid();
            LoadCollectCustomerCombo();
        }

        private void BtnSaveCustomerEdit_Click(object sender, EventArgs e)
        {
            if (selectedCustomerId == -1 || string.IsNullOrWhiteSpace(txtCustomerName.Text)) return;

            try
            {
                CustomersRepository.UpdateCustomer(selectedCustomerId, txtCustomerName.Text.Trim(), string.IsNullOrWhiteSpace(txtCustomerPhone.Text) ? null : txtCustomerPhone.Text.Trim());
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

            MessageBox.Show("تم تعديل بيانات العميل بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearCustomerInputs();
            LoadCustomersGrid();
            LoadCollectCustomerCombo();
        }

        private void BtnDeleteCustomer_Click(object sender, EventArgs e)
        {
            if (selectedCustomerId == -1)
            {
                MessageBox.Show("من فضلك اختر عميل من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                if (CustomersRepository.HasSales(selectedCustomerId))
                {
                    MessageBox.Show("لا يمكن حذف هذا العميل لأن له مبيعات مسجّلة بالفعل.", "غير مسموح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show("هل أنت متأكد من حذف هذا العميل؟", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                CustomersRepository.DeleteCustomer(selectedCustomerId);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

            MessageBox.Show("تم حذف العميل بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearCustomerInputs();
            LoadCustomersGrid();
            LoadCollectCustomerCombo();
        }

        private void ClearCustomerInputs()
        {
            selectedCustomerId = -1;
            txtCustomerName.Clear();
            txtCustomerPhone.Clear();
            btnSaveCustomerEdit.Enabled = false;
        }

        private void BtnCollectFromCustomer_Click(object sender, EventArgs e)
        {
            if (cmbCollectCustomer.SelectedValue == null || cmbCollectPaymentMethod.SelectedItem == null
                || !decimal.TryParse(txtCollectAmount.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("من فضلك اختر العميل ووسيلة التحصيل وأدخل مبلغ صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (IsTodayClosed())
            {
                MessageBox.Show("تم إقفال اليوم بالفعل، لا يمكن تسجيل تحصيل جديد.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int customerId = Convert.ToInt32(cmbCollectCustomer.SelectedValue);
            string method = cmbCollectPaymentMethod.SelectedItem.ToString();
            string customerName = cmbCollectCustomer.Text;

            try
            {
                AppServices.CoreEngine.Execute(new CollectFromCustomerCommand { CustomerId = customerId, CustomerName = customerName, Method = method, Amount = amount, PerformedBy = AuthManager.CurrentUsername });
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

            MessageBox.Show("تم تسجيل التحصيل بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtCollectAmount.Clear();
            LoadCustomersGrid();
        }

        // ==========================================================================
        // فحص هل تاريخ النهاردة تم إقفاله بالفعل (منع تحصيل في يوم مقفول)
        // ==========================================================================
        private bool IsTodayClosed() => UIHelpers.IsTodayClosed();
    }
}
