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
    // SuppliersPageControl: نسخة مستقلة تمامًا من شاشة "الموردون والمشتريات"
    // الموجودة في Form1.cs (CreateSuppliersDesign + BuildNewPurchasePanel +
    // BuildSupplierPaymentPanel). نفس مبدأ الشاشات السابقة.
    // ==========================================================================
    public partial class SuppliersPageControl : UserControl
    {
        private static readonly Color ColorPrimary = UIHelpers.ColorPrimary;
        private static readonly Color ColorSuccess = UIHelpers.ColorSuccess;
        private static readonly Color ColorDanger = UIHelpers.ColorDanger;
        private static readonly Color ColorWarning = UIHelpers.ColorWarning;
        private static readonly Color ColorBackground = UIHelpers.ColorBackground;

        // ---------- بيانات المورد ----------
        private Guna2TextBox txtSupplierName, txtSupplierPhone;
        private Guna2Button btnSaveSupplierEdit;
        private DataGridView dgvSuppliers, dgvSupplierStatement;
        private int selectedSupplierId = -1;

        // ---------- سويتش نوع العملية ----------
        private Guna2ComboBox cmbSupplierOperationType;
        private Panel pnlNewPurchase, pnlSupplierPayment;

        // ---------- فاتورة شراء جديدة ----------
        private Guna2ComboBox cmbPurchaseSupplier, cmbPurchasePaymentType, cmbPurchasePaymentMethod;
        private Guna2TextBox txtPurchaseBarcode, txtPurchaseProductName, txtPurchaseQty, txtPurchaseUnitCost, txtPurchaseImeiList, txtPurchaseSalePrice;
        private CheckBox chkPurchaseSerialized;
        private DataGridView dgvPurchaseCart;
        private Label lblPurchaseCartTotal;
        private List<(string Barcode, string ProductName, int Qty, decimal UnitCost, decimal SalePrice, decimal LineTotal, bool IsSerialized, List<string> Imeis)> currentPurchaseItems
            = new List<(string, string, int, decimal, decimal, decimal, bool, List<string>)>();

        // ---------- سداد مورد ----------
        private Guna2ComboBox cmbPaymentSupplier, cmbSupplierPaymentMethod;
        private Guna2TextBox txtSupplierPaymentAmount;

        public SuppliersPageControl()
        {
            this.Dock = DockStyle.Fill;
            this.AutoScroll = true;
            this.BackColor = ColorBackground;

            BuildUI();
            LoadSuppliersGrid();
            LoadSupplierCombos();
        }

        // ==========================================================================
        // الهيكل العام للشاشة
        // ==========================================================================
        private void BuildUI()
        {
            Guna2Panel gbSupplier = new Guna2Panel() { Location = new Point(20, 20), Size = new Size(300, 250), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblSupplierTitle = new Label() { Text = "🚚 إضافة / تعديل مورد", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = ColorPrimary };
            Label lblSupplierName = new Label() { Text = "اسم المورد:", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtSupplierName = new Guna2TextBox() { Location = new Point(20, 70), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };
            Label lblSupplierPhone = new Label() { Text = "رقم التليفون:", Location = new Point(20, 108), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtSupplierPhone = new Guna2TextBox() { Location = new Point(20, 128), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Guna2Button btnAddSupplier = new Guna2Button() { Text = "إضافة مورد جديد ✅", Location = new Point(20, 166), Width = 260, Height = 32, FillColor = ColorSuccess, BorderRadius = 9 };
            btnAddSupplier.Click += BtnAddSupplier_Click;

            btnSaveSupplierEdit = new Guna2Button() { Text = "حفظ التعديل 💾", Location = new Point(20, 204), Width = 125, Height = 30, FillColor = ColorWarning, Enabled = false, BorderRadius = 9 };
            btnSaveSupplierEdit.Click += BtnSaveSupplierEdit_Click;

            Guna2Button btnDeleteSupplier = new Guna2Button() { Text = "حذف المورد ❌", Location = new Point(155, 204), Width = 125, Height = 30, FillColor = ColorDanger, BorderRadius = 9 };
            btnDeleteSupplier.Click += BtnDeleteSupplier_Click;

            gbSupplier.Controls.AddRange(new Control[] { lblSupplierTitle, lblSupplierName, txtSupplierName, lblSupplierPhone, txtSupplierPhone, btnAddSupplier, btnSaveSupplierEdit, btnDeleteSupplier });

            // ---------- كارت العمليات (فاتورة شراء / سداد) ----------
            Guna2Panel pnlOperationCard = new Guna2Panel() { Location = new Point(20, 285), Size = new Size(300, 900), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };

            Label lblOpType = new Label() { Text = "نوع العملية:", Location = new Point(20, 18), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = ColorPrimary };
            cmbSupplierOperationType = new Guna2ComboBox() { Location = new Point(20, 38), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbSupplierOperationType.Items.AddRange(new string[] { "فاتورة شراء جديدة 📦", "سداد لمورد 💵" });
            cmbSupplierOperationType.SelectedIndexChanged += CmbSupplierOperationType_SelectedIndexChanged;

            pnlNewPurchase = new Panel() { Location = new Point(20, 78), Size = new Size(260, 800), AutoScroll = false };
            pnlSupplierPayment = new Panel() { Location = new Point(20, 78), Size = new Size(260, 420) };

            BuildNewPurchasePanel();
            BuildSupplierPaymentPanel();
            pnlSupplierPayment.Visible = false;

            pnlOperationCard.Controls.AddRange(new Control[] { lblOpType, cmbSupplierOperationType, pnlNewPurchase, pnlSupplierPayment });

            // ---------- كارت جدول الموردين ----------
            Guna2Panel pnlSuppliersGridCard = new Guna2Panel() { Location = new Point(340, 20), Size = new Size(780, 335), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblSuppliersGridTitle = new Label() { Text = "🚚 الموردون وأرصدتهم", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };
            dgvSuppliers = new DataGridView() { Location = new Point(20, 50), Size = new Size(740, 270), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvSuppliers.CellClick += DgvSuppliers_CellClick;
            StyleDataGridView(dgvSuppliers);
            pnlSuppliersGridCard.Controls.AddRange(new Control[] { lblSuppliersGridTitle, dgvSuppliers });

            // ---------- كارت كشف حساب المورد ----------
            Guna2Panel pnlStatementCard = new Guna2Panel() { Location = new Point(340, 370), Size = new Size(780, 335), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblStatementTitle = new Label() { Text = "📋 كشف حساب المورد المحدد (دوس على أي صف فوق)", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };
            dgvSupplierStatement = new DataGridView() { Location = new Point(20, 50), Size = new Size(740, 270), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            StyleDataGridView(dgvSupplierStatement);
            pnlStatementCard.Controls.AddRange(new Control[] { lblStatementTitle, dgvSupplierStatement });

            this.Controls.AddRange(new Control[] { gbSupplier, pnlOperationCard, pnlSuppliersGridCard, pnlStatementCard });

            cmbSupplierOperationType.SelectedIndex = 0;
        }

        private void BuildNewPurchasePanel()
        {
            Label lblPurchaseSupplier = new Label() { Text = "المورد:", Location = new Point(0, 0), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbPurchaseSupplier = new Guna2ComboBox() { Location = new Point(0, 20), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };

            Label lblPurchasePaymentTypeLbl = new Label() { Text = "طريقة الدفع للمورد:", Location = new Point(0, 58), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbPurchasePaymentType = new Guna2ComboBox() { Location = new Point(0, 78), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbPurchasePaymentType.Items.AddRange(new string[] { "كاش فوري", "آجل" });
            cmbPurchasePaymentType.SelectedIndex = 1;
            cmbPurchasePaymentType.SelectedIndexChanged += CmbPurchasePaymentType_SelectedIndexChanged;

            Label lblPurchasePayMethodLbl = new Label() { Text = "وسيلة الدفع (لو كاش فوري):", Location = new Point(0, 116), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbPurchasePaymentMethod = new Guna2ComboBox() { Location = new Point(0, 136), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false, BorderRadius = 8 };
            cmbPurchasePaymentMethod.Items.AddRange(UIHelpers.PaymentMethods);

            Label lblBarcode = new Label() { Text = "باركود المنتج (لو موجود):", Location = new Point(0, 174), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtPurchaseBarcode = new Guna2TextBox() { Location = new Point(0, 194), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };
            txtPurchaseBarcode.KeyDown += TxtPurchaseBarcode_KeyDown;

            Label lblProductName = new Label() { Text = "اسم المنتج:", Location = new Point(0, 232), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtPurchaseProductName = new Guna2TextBox() { Location = new Point(0, 252), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            chkPurchaseSerialized = new CheckBox() { Text = "منتج بسيريال/IMEI (موبايل)", Location = new Point(0, 288), AutoSize = true };
            chkPurchaseSerialized.CheckedChanged += ChkPurchaseSerialized_CheckedChanged;

            Label lblQty = new Label() { Text = "الكمية:", Location = new Point(0, 316), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtPurchaseQty = new Guna2TextBox() { Location = new Point(0, 336), Width = 120, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblUnitCost = new Label() { Text = "سعر الشراء للوحدة:", Location = new Point(140, 316), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtPurchaseUnitCost = new Guna2TextBox() { Location = new Point(140, 336), Width = 120, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblImeiList = new Label() { Text = "أرقام الـIMEI (رقم في كل سطر، عدد الأسطر = الكمية):", Location = new Point(0, 372), Size = new Size(260, 30), Font = new Font("Segoe UI", 7.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtPurchaseImeiList = new Guna2TextBox() { Location = new Point(0, 402), Width = 260, Height = 70, Multiline = true, ScrollBars = ScrollBars.Vertical, Visible = false, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };
            lblImeiList.Visible = false;

            Label lblSalePrice = new Label() { Text = "سعر البيع المقترح (لو منتج جديد بس):", Location = new Point(0, 482), Size = new Size(260, 20), Font = new Font("Segoe UI", 7.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtPurchaseSalePrice = new Guna2TextBox() { Location = new Point(0, 502), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Guna2Button btnAddToCart = new Guna2Button() { Text = "إضافة للفاتورة ➕", Location = new Point(0, 538), Width = 260, Height = 34, FillColor = ColorPrimary, BorderRadius = 9 };
            btnAddToCart.Click += BtnAddPurchaseItem_Click;

            dgvPurchaseCart = new DataGridView() { Location = new Point(0, 578), Size = new Size(260, 130), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            StyleDataGridView(dgvPurchaseCart);

            lblPurchaseCartTotal = new Label() { Text = "إجمالي الفاتورة: 0.00 ج.م", Location = new Point(0, 716), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ColorSuccess };

            Guna2Button btnSavePurchase = new Guna2Button() { Text = "حفظ فاتورة الشراء 💾", Location = new Point(0, 745), Width = 260, Height = 36, FillColor = ColorSuccess, BorderRadius = 10 };
            btnSavePurchase.Click += BtnSavePurchase_Click;

            pnlNewPurchase.Controls.AddRange(new Control[] { lblPurchaseSupplier, cmbPurchaseSupplier, lblPurchasePaymentTypeLbl, cmbPurchasePaymentType, lblPurchasePayMethodLbl, cmbPurchasePaymentMethod, lblBarcode, txtPurchaseBarcode, lblProductName, txtPurchaseProductName, chkPurchaseSerialized, lblQty, txtPurchaseQty, lblUnitCost, txtPurchaseUnitCost, lblImeiList, txtPurchaseImeiList, lblSalePrice, txtPurchaseSalePrice, btnAddToCart, dgvPurchaseCart, lblPurchaseCartTotal, btnSavePurchase });
        }

        private void BuildSupplierPaymentPanel()
        {
            Label lblPaymentSupplier = new Label() { Text = "المورد:", Location = new Point(0, 0), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbPaymentSupplier = new Guna2ComboBox() { Location = new Point(0, 20), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };

            Label lblPaymentMethod = new Label() { Text = "وسيلة الدفع:", Location = new Point(0, 58), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbSupplierPaymentMethod = new Guna2ComboBox() { Location = new Point(0, 78), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbSupplierPaymentMethod.Items.AddRange(UIHelpers.PaymentMethods);

            Label lblAmount = new Label() { Text = "المبلغ المدفوع:", Location = new Point(0, 116), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtSupplierPaymentAmount = new Guna2TextBox() { Location = new Point(0, 136), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Guna2Button btnPaySupplier = new Guna2Button() { Text = "تسجيل السداد ✅", Location = new Point(0, 176), Width = 260, Height = 38, FillColor = ColorSuccess, BorderRadius = 10 };
            btnPaySupplier.Click += BtnPaySupplier_Click;

            pnlSupplierPayment.Controls.AddRange(new Control[] { lblPaymentSupplier, cmbPaymentSupplier, lblPaymentMethod, cmbSupplierPaymentMethod, lblAmount, txtSupplierPaymentAmount, btnPaySupplier });
        }

        private void CmbSupplierOperationType_SelectedIndexChanged(object sender, EventArgs e)
        {
            pnlNewPurchase.Visible = cmbSupplierOperationType.SelectedIndex == 0;
            pnlSupplierPayment.Visible = cmbSupplierOperationType.SelectedIndex == 1;
        }

        private void ChkPurchaseSerialized_CheckedChanged(object sender, EventArgs e)
        {
            txtPurchaseImeiList.Visible = chkPurchaseSerialized.Checked;
            txtPurchaseQty.ReadOnly = chkPurchaseSerialized.Checked;
            if (chkPurchaseSerialized.Checked) txtPurchaseQty.Text = "0";
        }

        private void CmbPurchasePaymentType_SelectedIndexChanged(object sender, EventArgs e)
        {
            cmbPurchasePaymentMethod.Enabled = cmbPurchasePaymentType.SelectedItem?.ToString() == "كاش فوري";
        }

        // ==========================================================================
        // نفس تنسيق الجداول المستخدم في كل شاشات Form1.cs
        // ==========================================================================
        private void StyleDataGridView(DataGridView dgv) => UIHelpers.StyleDataGridView(dgv);

        // ==========================================================================
        // فحص هل تاريخ النهاردة تم إقفاله
        // ==========================================================================
        private bool IsTodayClosed() => UIHelpers.IsTodayClosed();

        // ==========================================================================
        // تحميل الكومبوهات وجدول الموردين
        // ==========================================================================
        private void LoadSupplierCombos()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("SupplierId", typeof(int)), new DataColumn("SupplierName") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("SELECT SupplierId, SupplierName FROM Suppliers ORDER BY SupplierName", conn))
                {
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                dt.Rows.Add(Convert.ToInt32(reader["SupplierId"]), reader["SupplierName"].ToString());
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }

            cmbPurchaseSupplier.DataSource = dt.Copy();
            cmbPurchaseSupplier.DisplayMember = "SupplierName";
            cmbPurchaseSupplier.ValueMember = "SupplierId";

            cmbPaymentSupplier.DataSource = dt.Copy();
            cmbPaymentSupplier.DisplayMember = "SupplierName";
            cmbPaymentSupplier.ValueMember = "SupplierId";
        }

        private void LoadSuppliersGrid()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("SupplierId"), new DataColumn("اسم المورد"), new DataColumn("التليفون"), new DataColumn("إجمالي المشتريات"), new DataColumn("إجمالي المسدد"), new DataColumn("المتبقي") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                var suppliers = new List<(int id, string name, string phone)>();
                using (SqliteCommand cmd = new SqliteCommand("SELECT SupplierId, SupplierName, Phone FROM Suppliers ORDER BY SupplierName", conn))
                {
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            suppliers.Add((Convert.ToInt32(reader["SupplierId"]), reader["SupplierName"].ToString(), reader["Phone"]?.ToString()));
                    }
                }

                foreach (var sup in suppliers)
                {
                    decimal totalPurchases = 0, totalPaid = 0;
                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(TotalAmount) FROM Purchases WHERE SupplierId = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", sup.id);
                        var res = cmd.ExecuteScalar();
                        totalPurchases = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                    }
                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE SupplierId = @Id AND MovementType = 'صرف'", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", sup.id);
                        var res = cmd.ExecuteScalar();
                        totalPaid = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                    }

                    dt.Rows.Add(sup.id, sup.name, sup.phone, totalPurchases.ToString("N2"), totalPaid.ToString("N2"), (totalPurchases - totalPaid).ToString("N2"));
                }
            }

            dgvSuppliers.DataSource = dt;
            if (dgvSuppliers.Columns["SupplierId"] != null) dgvSuppliers.Columns["SupplierId"].Visible = false;
        }

        private void DgvSuppliers_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvSuppliers.Rows[e.RowIndex];
            selectedSupplierId = Convert.ToInt32(row.Cells["SupplierId"].Value);
            txtSupplierName.Text = row.Cells["اسم المورد"].Value.ToString();
            txtSupplierPhone.Text = row.Cells["التليفون"].Value?.ToString();
            btnSaveSupplierEdit.Enabled = true;

            LoadSupplierStatement(selectedSupplierId);
        }

        private void LoadSupplierStatement(int supplierId)
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("التاريخ"), new DataColumn("النوع"), new DataColumn("التفاصيل"), new DataColumn("المبلغ") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT PurchaseId, PurchaseDate, TotalAmount, Notes FROM Purchases WHERE SupplierId = @Id ORDER BY PurchaseDate", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", supplierId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            dt.Rows.Add(reader["PurchaseDate"], "فاتورة شراء", "فاتورة رقم " + reader["PurchaseId"], reader["TotalAmount"]);
                    }
                }
                using (SqliteCommand cmd = new SqliteCommand("SELECT CreatedAt, Amount, PaymentMethod FROM CashMovements WHERE SupplierId = @Id AND MovementType = 'صرف' ORDER BY CreatedAt", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", supplierId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            dt.Rows.Add(reader["CreatedAt"], "سداد", "سداد عبر " + reader["PaymentMethod"], "-" + Convert.ToDecimal(reader["Amount"]).ToString("N2"));
                    }
                }
            }

            dgvSupplierStatement.DataSource = dt;
        }

        // ==========================================================================
        // إضافة / تعديل / حذف مورد
        // ==========================================================================
        private void BtnAddSupplier_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSupplierName.Text))
            {
                MessageBox.Show("من فضلك أدخل اسم المورد.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("INSERT INTO Suppliers (SupplierName, Phone, CreatedAt) VALUES (@N, @P, @C)", conn))
                {
                    cmd.Parameters.AddWithValue("@N", txtSupplierName.Text.Trim());
                    cmd.Parameters.AddWithValue("@P", string.IsNullOrWhiteSpace(txtSupplierPhone.Text) ? (object)DBNull.Value : txtSupplierPhone.Text.Trim());
                    cmd.Parameters.AddWithValue("@C", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم إضافة المورد بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearSupplierInputs();
            LoadSuppliersGrid();
            LoadSupplierCombos();
        }

        private void BtnSaveSupplierEdit_Click(object sender, EventArgs e)
        {
            if (selectedSupplierId == -1 || string.IsNullOrWhiteSpace(txtSupplierName.Text)) return;

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("UPDATE Suppliers SET SupplierName = @N, Phone = @P WHERE SupplierId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@N", txtSupplierName.Text.Trim());
                    cmd.Parameters.AddWithValue("@P", string.IsNullOrWhiteSpace(txtSupplierPhone.Text) ? (object)DBNull.Value : txtSupplierPhone.Text.Trim());
                    cmd.Parameters.AddWithValue("@Id", selectedSupplierId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم تعديل بيانات المورد بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearSupplierInputs();
            LoadSuppliersGrid();
            LoadSupplierCombos();
        }

        private void BtnDeleteSupplier_Click(object sender, EventArgs e)
        {
            if (selectedSupplierId == -1)
            {
                MessageBox.Show("من فضلك اختر مورد من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                int purchaseCount;
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM Purchases WHERE SupplierId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", selectedSupplierId);
                    purchaseCount = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (purchaseCount > 0)
                {
                    MessageBox.Show("لا يمكن حذف هذا المورد لأن له فواتير شراء مسجّلة بالفعل.", "غير مسموح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show("هل أنت متأكد من حذف هذا المورد؟", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                using (SqliteCommand cmd = new SqliteCommand("DELETE FROM Suppliers WHERE SupplierId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", selectedSupplierId);
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم حذف المورد بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearSupplierInputs();
            LoadSuppliersGrid();
            LoadSupplierCombos();
        }

        private void ClearSupplierInputs()
        {
            selectedSupplierId = -1;
            txtSupplierName.Clear();
            txtSupplierPhone.Clear();
            btnSaveSupplierEdit.Enabled = false;
        }

        // ==========================================================================
        // فاتورة الشراء الجديدة
        // ==========================================================================
        private void TxtPurchaseBarcode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter || string.IsNullOrWhiteSpace(txtPurchaseBarcode.Text)) return;

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("SELECT ProductName, Price, SalePrice, IsSerialized FROM Products WHERE Barcode = @Barcode", conn))
                {
                    cmd.Parameters.AddWithValue("@Barcode", txtPurchaseBarcode.Text.Trim());
                    conn.Open();
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            txtPurchaseProductName.Text = reader["ProductName"].ToString();
                            txtPurchaseUnitCost.Text = reader["Price"].ToString();
                            txtPurchaseSalePrice.Text = reader["SalePrice"].ToString();
                            chkPurchaseSerialized.Checked = reader["IsSerialized"] != DBNull.Value && Convert.ToInt32(reader["IsSerialized"]) == 1;
                        }
                        else
                        {
                            txtPurchaseProductName.Clear();
                            txtPurchaseSalePrice.Clear();
                            MessageBox.Show("المنتج ده مش موجود في المخزون. اكتب اسمه وسعر بيعه المقترح عشان يتضاف كمنتج جديد.", "منتج جديد", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
        }

        private void BtnAddPurchaseItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPurchaseProductName.Text)
                || !decimal.TryParse(txtPurchaseUnitCost.Text, out decimal unitCost) || unitCost < 0)
            {
                MessageBox.Show("من فضلك أدخل اسم منتج وسعر شراء صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal.TryParse(txtPurchaseSalePrice.Text, out decimal salePrice);
            string barcode = txtPurchaseBarcode.Text.Trim();
            List<string> imeis = null;
            int qty;

            if (chkPurchaseSerialized.Checked)
            {
                imeis = txtPurchaseImeiList.Text
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();

                if (imeis.Count == 0)
                {
                    MessageBox.Show("من فضلك أدخل رقم IMEI واحد على الأقل، رقم في كل سطر.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (imeis.Distinct().Count() != imeis.Count)
                {
                    MessageBox.Show("في أرقام IMEI متكررة في القايمة، من فضلك راجعها.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                qty = imeis.Count;
            }
            else
            {
                if (!int.TryParse(txtPurchaseQty.Text, out qty) || qty <= 0)
                {
                    MessageBox.Show("من فضلك أدخل كمية صحيحة.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            decimal lineTotal = qty * unitCost;
            currentPurchaseItems.Add((barcode, txtPurchaseProductName.Text.Trim(), qty, unitCost, salePrice, lineTotal, chkPurchaseSerialized.Checked, imeis));

            RefreshPurchaseCartGrid();

            txtPurchaseBarcode.Clear();
            txtPurchaseProductName.Clear();
            txtPurchaseQty.Clear();
            txtPurchaseUnitCost.Clear();
            txtPurchaseSalePrice.Clear();
            txtPurchaseImeiList.Clear();
            chkPurchaseSerialized.Checked = false;
            txtPurchaseBarcode.Focus();
        }

        private void RefreshPurchaseCartGrid()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("المنتج"), new DataColumn("الكمية"), new DataColumn("السعر"), new DataColumn("الإجمالي"), new DataColumn("IMEI؟") });

            decimal grandTotal = 0;
            foreach (var item in currentPurchaseItems)
            {
                dt.Rows.Add(item.ProductName, item.Qty, item.UnitCost.ToString("N2"), item.LineTotal.ToString("N2"), item.IsSerialized ? "✅" : "-");
                grandTotal += item.LineTotal;
            }

            dgvPurchaseCart.DataSource = dt;
            lblPurchaseCartTotal.Text = $"إجمالي الفاتورة: {grandTotal:N2} ج.م";
        }

        private void BtnSavePurchase_Click(object sender, EventArgs e)
        {
            if (cmbPurchaseSupplier.SelectedValue == null)
            {
                MessageBox.Show("من فضلك اختر المورد.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (currentPurchaseItems.Count == 0)
            {
                MessageBox.Show("من فضلك ضيف صنف واحد على الأقل للفاتورة.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool payCashNow = cmbPurchasePaymentType.SelectedItem?.ToString() == "كاش فوري";
            string cashMethod = null;
            if (payCashNow)
            {
                if (cmbPurchasePaymentMethod.SelectedItem == null)
                {
                    MessageBox.Show("من فضلك اختر وسيلة الدفع اللي هتدفع بيها كاش.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                cashMethod = cmbPurchasePaymentMethod.SelectedItem.ToString();
            }

            int supplierId = Convert.ToInt32(cmbPurchaseSupplier.SelectedValue);
            decimal totalAmount = currentPurchaseItems.Sum(x => x.LineTotal);

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                foreach (var checkItem in currentPurchaseItems)
                {
                    if (!checkItem.IsSerialized || checkItem.Imeis == null) continue;
                    foreach (string imei in checkItem.Imeis)
                    {
                        using (SqliteCommand cmdCheckImei = new SqliteCommand("SELECT COUNT(*) FROM ProductUnits WHERE IMEI = @IMEI", conn))
                        {
                            cmdCheckImei.Parameters.AddWithValue("@IMEI", imei);
                            if (Convert.ToInt32(cmdCheckImei.ExecuteScalar()) > 0)
                            {
                                MessageBox.Show($"رقم الـIMEI \"{imei}\" مسجل بالفعل في النظام من قبل. راجع القايمة وشيله أو صحّحه.", "رقم مكرر", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                        }
                    }
                }

                if (payCashNow)
                {
                    decimal currentBalance = 0;
                    using (SqliteCommand cmdBal = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn))
                    {
                        cmdBal.Parameters.AddWithValue("@Method", cashMethod);
                        var res = cmdBal.ExecuteScalar();
                        currentBalance = res != null ? Convert.ToDecimal(res) : 0;
                    }
                    if (totalAmount > currentBalance)
                    {
                        MessageBox.Show($"الرصيد المتاح في \"{cashMethod}\" هو {currentBalance:N2} فقط، أقل من إجمالي الفاتورة.", "رصيد غير كافٍ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        int purchaseId;
                        using (SqliteCommand cmd = new SqliteCommand("INSERT INTO Purchases (SupplierId, PurchaseDate, TotalAmount, Notes) VALUES (@S, @D, @T, @N)", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@S", supplierId);
                            cmd.Parameters.AddWithValue("@D", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmd.Parameters.AddWithValue("@T", totalAmount);
                            cmd.Parameters.AddWithValue("@N", DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                        using (SqliteCommand cmdId = new SqliteCommand("SELECT last_insert_rowid();", conn, transaction))
                        {
                            purchaseId = Convert.ToInt32(cmdId.ExecuteScalar());
                        }

                        foreach (var item in currentPurchaseItems)
                        {
                            using (SqliteCommand cmd = new SqliteCommand(
                                "INSERT INTO PurchaseItems (PurchaseId, Barcode, ProductName, Quantity, UnitCost, LineTotal) VALUES (@P, @B, @N, @Q, @U, @L)", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@P", purchaseId);
                                cmd.Parameters.AddWithValue("@B", string.IsNullOrEmpty(item.Barcode) ? (object)DBNull.Value : item.Barcode);
                                cmd.Parameters.AddWithValue("@N", item.ProductName);
                                cmd.Parameters.AddWithValue("@Q", item.Qty);
                                cmd.Parameters.AddWithValue("@U", item.UnitCost);
                                cmd.Parameters.AddWithValue("@L", item.LineTotal);
                                cmd.ExecuteNonQuery();
                            }

                            bool productExists = false;
                            if (!string.IsNullOrEmpty(item.Barcode))
                            {
                                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM Products WHERE Barcode = @B", conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@B", item.Barcode);
                                    productExists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                                }
                            }

                            string finalBarcode = item.Barcode;

                            if (productExists)
                            {
                                using (SqliteCommand cmd = new SqliteCommand(
                                    "UPDATE Products SET Quantity = Quantity + @Q, Price = @U, IsSerialized = CASE WHEN @IsSerialized = 1 THEN 1 ELSE IsSerialized END WHERE Barcode = @B", conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@Q", item.Qty);
                                    cmd.Parameters.AddWithValue("@U", item.UnitCost);
                                    cmd.Parameters.AddWithValue("@IsSerialized", item.IsSerialized ? 1 : 0);
                                    cmd.Parameters.AddWithValue("@B", item.Barcode);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            else
                            {
                                finalBarcode = string.IsNullOrEmpty(item.Barcode) ? ("NEW-" + DateTime.Now.Ticks) : item.Barcode;
                                using (SqliteCommand cmd = new SqliteCommand(
                                    "INSERT INTO Products (Barcode, ProductName, Price, SalePrice, Quantity, IsSerialized) VALUES (@B, @N, @U, @S, @Q, @IsSerialized)", conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@B", finalBarcode);
                                    cmd.Parameters.AddWithValue("@N", item.ProductName);
                                    cmd.Parameters.AddWithValue("@U", item.UnitCost);
                                    cmd.Parameters.AddWithValue("@S", item.SalePrice);
                                    cmd.Parameters.AddWithValue("@Q", item.Qty);
                                    cmd.Parameters.AddWithValue("@IsSerialized", item.IsSerialized ? 1 : 0);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            if (item.IsSerialized && item.Imeis != null)
                            {
                                foreach (string imei in item.Imeis)
                                {
                                    using (SqliteCommand cmd = new SqliteCommand(
                                        "INSERT INTO ProductUnits (Barcode, IMEI, Status, PurchaseId, CreatedAt) VALUES (@B, @IMEI, 'InStock', @P, @C)", conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@B", finalBarcode);
                                        cmd.Parameters.AddWithValue("@IMEI", imei);
                                        cmd.Parameters.AddWithValue("@P", purchaseId);
                                        cmd.Parameters.AddWithValue("@C", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }
                        }

                        if (payCashNow)
                        {
                            using (SqliteCommand cmd = new SqliteCommand(
                                "INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode, SupplierId) VALUES (@Date, 'صرف', @Method, @Amount, @Ref, @Desc, @CreatedAt, 2100, @SupplierId)", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@Method", cashMethod);
                                cmd.Parameters.AddWithValue("@Amount", totalAmount);
                                cmd.Parameters.AddWithValue("@Ref", "فاتورة شراء رقم " + purchaseId);
                                cmd.Parameters.AddWithValue("@Desc", "سداد كاش فوري لفاتورة شراء رقم " + purchaseId);
                                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@SupplierId", supplierId);
                                cmd.ExecuteNonQuery();
                            }

                            using (SqliteCommand cmd = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = CurrentBalance - @Amount WHERE PaymentMethod = @Method", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Amount", totalAmount);
                                cmd.Parameters.AddWithValue("@Method", cashMethod);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show("حصل خطأ أثناء حفظ الفاتورة ولم يتم حفظ أي حاجة: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }

            MessageBox.Show($"تم حفظ فاتورة الشراء بنجاح بإجمالي {totalAmount:N2} ج.م، وتحديث المخزون تلقائي" + (payCashNow ? "، وتم خصم المبلغ فورًا من الرصيد." : "، وسجّلت كدين على المورد."), "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            currentPurchaseItems.Clear();
            RefreshPurchaseCartGrid();
            LoadSuppliersGrid();
        }

        // ==========================================================================
        // سداد مورد
        // ==========================================================================
        private void BtnPaySupplier_Click(object sender, EventArgs e)
        {
            if (cmbPaymentSupplier.SelectedValue == null || cmbSupplierPaymentMethod.SelectedItem == null
                || !decimal.TryParse(txtSupplierPaymentAmount.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("من فضلك اختر المورد ووسيلة الدفع وأدخل مبلغ صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (IsTodayClosed())
            {
                MessageBox.Show("تم إقفال اليوم بالفعل، لا يمكن تسجيل سداد جديد.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int supplierId = Convert.ToInt32(cmbPaymentSupplier.SelectedValue);
            string method = cmbSupplierPaymentMethod.SelectedItem.ToString();
            string supplierName = cmbPaymentSupplier.Text;

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                decimal currentBalance = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn))
                {
                    cmd.Parameters.AddWithValue("@Method", method);
                    var res = cmd.ExecuteScalar();
                    currentBalance = res != null ? Convert.ToDecimal(res) : 0;
                }

                if (amount > currentBalance)
                {
                    MessageBox.Show($"الرصيد المتاح في \"{method}\" هو {currentBalance:N2} فقط.", "رصيد غير كافٍ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                using (SqliteCommand cmd = new SqliteCommand(
                    "INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode, SupplierId) VALUES (@Date, 'صرف', @Method, @Amount, @Ref, @Desc, @CreatedAt, 2100, @SupplierId)", conn))
                {
                    cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@Method", method);
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    cmd.Parameters.AddWithValue("@Ref", "");
                    cmd.Parameters.AddWithValue("@Desc", "سداد لمورد: " + supplierName);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@SupplierId", supplierId);
                    cmd.ExecuteNonQuery();
                }

                using (SqliteCommand cmd = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = CurrentBalance - @Amount WHERE PaymentMethod = @Method", conn))
                {
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    cmd.Parameters.AddWithValue("@Method", method);
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم تسجيل السداد بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtSupplierPaymentAmount.Clear();
            LoadSuppliersGrid();
        }
    }
}
