using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using TemoStore.Core.Commands;
using TemoStore.Core.Exceptions;

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
        private static readonly Color ColorNeutral = UIHelpers.ColorNeutral;
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
        private List<PurchaseCartItem> currentPurchaseItems = new List<PurchaseCartItem>();
        private Guna2Button btnSavePurchase, btnCancelEditPurchase, btnViewFullCart;
        private int? editingPurchaseId = null;

        // ---------- تعديل/إلغاء فاتورة شراء محفوظة (من جدول كشف الحساب) ----------
        private Guna2Button btnEditPurchaseInvoice, btnCancelPurchaseInvoice, btnViewPurchaseInvoice;
        private int? selectedStatementPurchaseId = null;

        // ---------- سداد مورد ----------
        private Guna2ComboBox cmbPaymentSupplier, cmbSupplierPaymentMethod;
        private Guna2TextBox txtSupplierPaymentAmount;

        public SuppliersPageControl()
        {
            this.Dock = DockStyle.Fill;
            this.Size = new Size(1150, 1150); // مقاس مبدئي واقعي قبل بناء الشاشة، عشان حسابات Anchor متبقاش غلط (راجع نفس التعليق في SalesPageControl)
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
            Guna2Panel gbSupplier = new Guna2Panel() { Location = new Point(20, 20), Size = new Size(300, 250), FillColor = Color.White, BorderRadius = UIHelpers.CardBorderRadius, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblSupplierTitle = new Label() { Text = "🚚 إضافة / تعديل مورد", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = ColorPrimary };
            Label lblSupplierName = new Label() { Text = "اسم المورد:", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtSupplierName = new Guna2TextBox() { Location = new Point(20, 70), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };
            Label lblSupplierPhone = new Label() { Text = "رقم التليفون:", Location = new Point(20, 108), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtSupplierPhone = new Guna2TextBox() { Location = new Point(20, 128), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Guna2Button btnAddSupplier = new Guna2Button() { Text = "إضافة مورد جديد ✅", Location = new Point(20, 166), Width = 260, Height = 32, FillColor = UIHelpers.ColorGreen, BorderRadius = 9 };
            btnAddSupplier.Click += BtnAddSupplier_Click;

            btnSaveSupplierEdit = new Guna2Button() { Text = "حفظ التعديل 💾", Location = new Point(20, 204), Width = 125, Height = 30, FillColor = UIHelpers.ColorOrange, Enabled = false, BorderRadius = 9 };
            btnSaveSupplierEdit.Click += BtnSaveSupplierEdit_Click;

            Guna2Button btnDeleteSupplier = new Guna2Button() { Text = "حذف المورد ❌", Location = new Point(155, 204), Width = 125, Height = 30, FillColor = UIHelpers.ColorRed, BorderRadius = 9 };
            btnDeleteSupplier.Click += BtnDeleteSupplier_Click;

            gbSupplier.Controls.AddRange(new Control[] { lblSupplierTitle, lblSupplierName, txtSupplierName, lblSupplierPhone, txtSupplierPhone, btnAddSupplier, btnSaveSupplierEdit, btnDeleteSupplier });

            // ---------- كارت العمليات (فاتورة شراء / سداد) ----------
            Guna2Panel pnlOperationCard = new Guna2Panel() { Location = new Point(20, 285), Size = new Size(300, 1030), FillColor = Color.White, BorderRadius = UIHelpers.CardBorderRadius, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };

            Label lblOpType = new Label() { Text = "نوع العملية:", Location = new Point(20, 18), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = ColorPrimary };
            cmbSupplierOperationType = new Guna2ComboBox() { Location = new Point(20, 38), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbSupplierOperationType.Items.AddRange(new string[] { "فاتورة شراء جديدة 📦", "سداد لمورد 💵" });
            cmbSupplierOperationType.SelectedIndexChanged += CmbSupplierOperationType_SelectedIndexChanged;

            pnlNewPurchase = new Panel() { Location = new Point(20, 78), Size = new Size(260, 930), AutoScroll = false };
            pnlSupplierPayment = new Panel() { Location = new Point(20, 78), Size = new Size(260, 420) };

            BuildNewPurchasePanel();
            BuildSupplierPaymentPanel();
            pnlSupplierPayment.Visible = false;

            pnlOperationCard.Controls.AddRange(new Control[] { lblOpType, cmbSupplierOperationType, pnlNewPurchase, pnlSupplierPayment });

            // ---------- كارت جدول الموردين ----------
            // ملحوظة: الكارتين دول واقفين فوق بعض عموديًا في نفس العمود، فبنمدّهم عرضًا بس (Top|Left|Right)
            // من غير Bottom عشان محدش يكبر لتحت ويغطي التاني
            AnchorStyles widenAnchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Guna2Panel pnlSuppliersGridCard = new Guna2Panel() { Location = new Point(340, 20), Size = new Size(780, 335), Anchor = widenAnchor, FillColor = Color.White, BorderRadius = UIHelpers.CardBorderRadius, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblSuppliersGridTitle = new Label() { Text = "🚚 الموردون وأرصدتهم", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };
            dgvSuppliers = new DataGridView() { Location = new Point(20, 50), Size = new Size(740, 270), Anchor = widenAnchor, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvSuppliers.CellClick += DgvSuppliers_CellClick;
            StyleDataGridView(dgvSuppliers);
            pnlSuppliersGridCard.Controls.AddRange(new Control[] { lblSuppliersGridTitle, dgvSuppliers });

            // ---------- كارت كشف حساب المورد ----------
            Guna2Panel pnlStatementCard = new Guna2Panel() { Location = new Point(340, 370), Size = new Size(780, 375), Anchor = widenAnchor, FillColor = Color.White, BorderRadius = UIHelpers.CardBorderRadius, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblStatementTitle = new Label() { Text = "📋 كشف حساب المورد المحدد (دوس على فاتورة شراء عشان تعدلها أو تلغيها)", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };
            dgvSupplierStatement = new DataGridView() { Location = new Point(20, 50), Size = new Size(740, 230), Anchor = widenAnchor, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            dgvSupplierStatement.CellClick += DgvSupplierStatement_CellClick;
            StyleDataGridView(dgvSupplierStatement);

            btnEditPurchaseInvoice = new Guna2Button() { Text = "تعديل الفاتورة المحددة ✏️", Location = new Point(20, 290), Width = 240, Height = 34, FillColor = UIHelpers.ColorOrange, BorderRadius = 9 };
            btnEditPurchaseInvoice.Click += BtnEditPurchaseInvoice_Click;

            btnCancelPurchaseInvoice = new Guna2Button() { Text = "إلغاء الفاتورة المحددة ❌", Location = new Point(270, 290), Width = 240, Height = 34, FillColor = UIHelpers.ColorRed, BorderRadius = 9 };
            btnCancelPurchaseInvoice.Click += BtnCancelPurchaseInvoice_Click;

            btnViewPurchaseInvoice = new Guna2Button() { Text = "عرض الفاتورة 👁️", Location = new Point(520, 290), Width = 240, Height = 34, FillColor = Color.White, ForeColor = ColorPrimary, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1, BorderRadius = 9 };
            btnViewPurchaseInvoice.Click += BtnViewPurchaseInvoice_Click;

            pnlStatementCard.Controls.AddRange(new Control[] { lblStatementTitle, dgvSupplierStatement, btnEditPurchaseInvoice, btnCancelPurchaseInvoice, btnViewPurchaseInvoice });

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

            Guna2Button btnAddToCart = new Guna2Button() { Text = "إضافة للمخزن ➕", Location = new Point(0, 538), Width = 260, Height = 34, FillColor = UIHelpers.ColorAccentPrimary, BorderRadius = 9 };
            btnAddToCart.Click += BtnAddPurchaseItem_Click;

            dgvPurchaseCart = new DataGridView() { Location = new Point(0, 578), Size = new Size(260, 170), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvPurchaseCart.CellDoubleClick += DgvPurchaseCart_CellDoubleClick;
            StyleDataGridView(dgvPurchaseCart);

            btnViewFullCart = new Guna2Button() { Text = "🔍 عرض الفاتورة كاملة", Location = new Point(0, 756), Width = 260, Height = 30, FillColor = Color.White, ForeColor = ColorPrimary, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1, BorderRadius = 9 };
            btnViewFullCart.Click += BtnViewFullCart_Click;

            Label lblCartHint = new Label() { Text = "دبل كليك على صنف في الجدول عشان تشيله من الفاتورة", Location = new Point(0, 794), Size = new Size(260, 15), Font = new Font("Segoe UI", 7F), ForeColor = Color.FromArgb(150, 155, 165) };

            lblPurchaseCartTotal = new Label() { Text = "إجمالي الفاتورة: 0.00 ج.م", Location = new Point(0, 812), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = UIHelpers.ColorGreen };

            btnSavePurchase = new Guna2Button() { Text = "حفظ فاتورة الشراء 💾", Location = new Point(0, 841), Width = 260, Height = 36, FillColor = UIHelpers.ColorGreen, BorderRadius = 10 };
            btnSavePurchase.Click += BtnSavePurchase_Click;

            btnCancelEditPurchase = new Guna2Button() { Text = "إلغاء التعديل ⬅️", Location = new Point(0, 881), Width = 260, Height = 30, FillColor = Color.White, ForeColor = ColorPrimary, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1, BorderRadius = 9, Visible = false };
            btnCancelEditPurchase.Click += BtnCancelEditPurchase_Click;

            pnlNewPurchase.Controls.AddRange(new Control[] { lblPurchaseSupplier, cmbPurchaseSupplier, lblPurchasePaymentTypeLbl, cmbPurchasePaymentType, lblPurchasePayMethodLbl, cmbPurchasePaymentMethod, lblBarcode, txtPurchaseBarcode, lblProductName, txtPurchaseProductName, chkPurchaseSerialized, lblQty, txtPurchaseQty, lblUnitCost, txtPurchaseUnitCost, lblImeiList, txtPurchaseImeiList, lblSalePrice, txtPurchaseSalePrice, btnAddToCart, dgvPurchaseCart, btnViewFullCart, lblCartHint, lblPurchaseCartTotal, btnSavePurchase, btnCancelEditPurchase });
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

            Guna2Button btnPaySupplier = new Guna2Button() { Text = "تسجيل السداد ✅", Location = new Point(0, 176), Width = 260, Height = 38, FillColor = UIHelpers.ColorGreen, BorderRadius = 10 };
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
            try
            {
                DataTable dt = SuppliersRepository.GetSupplierCombos();

                cmbPurchaseSupplier.DataSource = dt.Copy();
                cmbPurchaseSupplier.DisplayMember = "SupplierName";
                cmbPurchaseSupplier.ValueMember = "SupplierId";

                cmbPaymentSupplier.DataSource = dt.Copy();
                cmbPaymentSupplier.DisplayMember = "SupplierName";
                cmbPaymentSupplier.ValueMember = "SupplierId";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void LoadSuppliersGrid()
        {
            try
            {
                DataTable dt = SuppliersRepository.GetSuppliersWithBalances();
                dgvSuppliers.DataSource = dt;
                if (dgvSuppliers.Columns["SupplierId"] != null) dgvSuppliers.Columns["SupplierId"].Visible = false;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        // بيحوّل لتاب "فاتورة شراء جديدة" ويجهّزه فاضي - مستخدم من كارت "فاتورة شراء" السريع في الداشبورد
        public void FocusNewPurchaseEntry()
        {
            cmbSupplierOperationType.SelectedIndex = 0;
            pnlNewPurchase.Visible = true; // احتياطًا: لو كانت أصلاً على index 0 (فمفيش SelectedIndexChanged هيتنادى) والنافذة كانت متسيبة على تاب السداد
            pnlSupplierPayment.Visible = false;
            txtPurchaseBarcode.Focus();
        }

        // بيدوّر على مورد برقمه، يحدده في الجدول، ويجيب بياناته وكشف حسابه (مستخدمة من نتيجة البحث الشامل Ctrl+F)
        public void HighlightSupplier(string supplierIdText)
        {
            if (!int.TryParse(supplierIdText, out int supplierId)) return;
            foreach (DataGridViewRow row in dgvSuppliers.Rows)
            {
                if (row.Cells["SupplierId"].Value != null && Convert.ToInt32(row.Cells["SupplierId"].Value) == supplierId)
                {
                    dgvSuppliers.ClearSelection();
                    row.Selected = true;
                    dgvSuppliers.CurrentCell = row.Cells[0];
                    dgvSuppliers.FirstDisplayedScrollingRowIndex = row.Index;
                    DgvSuppliers_CellClick(dgvSuppliers, new DataGridViewCellEventArgs(0, row.Index));
                    return;
                }
            }
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
            try
            {
                dgvSupplierStatement.DataSource = SuppliersRepository.GetSupplierStatement(supplierId);
                if (dgvSupplierStatement.Columns.Contains("PurchaseId"))
                    dgvSupplierStatement.Columns["PurchaseId"].Visible = false;
                selectedStatementPurchaseId = null;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void DgvSupplierStatement_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var cell = dgvSupplierStatement.Rows[e.RowIndex].Cells["PurchaseId"].Value;
            selectedStatementPurchaseId = (cell == null || cell == DBNull.Value) ? (int?)null : Convert.ToInt32(cell);
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

            try
            {
                SuppliersRepository.AddSupplier(txtSupplierName.Text.Trim(), string.IsNullOrWhiteSpace(txtSupplierPhone.Text) ? null : txtSupplierPhone.Text.Trim());
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

            MessageBox.Show("تم إضافة المورد بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearSupplierInputs();
            LoadSuppliersGrid();
            LoadSupplierCombos();
        }

        private void BtnSaveSupplierEdit_Click(object sender, EventArgs e)
        {
            if (selectedSupplierId == -1 || string.IsNullOrWhiteSpace(txtSupplierName.Text)) return;

            try
            {
                SuppliersRepository.UpdateSupplier(selectedSupplierId, txtSupplierName.Text.Trim(), string.IsNullOrWhiteSpace(txtSupplierPhone.Text) ? null : txtSupplierPhone.Text.Trim());
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

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

            try
            {
                if (SuppliersRepository.HasPurchases(selectedSupplierId))
                {
                    MessageBox.Show("لا يمكن حذف هذا المورد لأن له فواتير شراء مسجّلة بالفعل.", "غير مسموح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show("هل أنت متأكد من حذف هذا المورد؟", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                SuppliersRepository.DeleteSupplier(selectedSupplierId);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

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

            try
            {
                ProductForPurchase product = SuppliersRepository.GetProductForPurchase(txtPurchaseBarcode.Text.Trim());
                if (product != null)
                {
                    txtPurchaseProductName.Text = product.ProductName;
                    txtPurchaseUnitCost.Text = product.Price.ToString();
                    txtPurchaseSalePrice.Text = product.SalePrice.ToString();
                    chkPurchaseSerialized.Checked = product.IsSerialized;
                }
                else
                {
                    txtPurchaseProductName.Clear();
                    txtPurchaseSalePrice.Clear();
                    MessageBox.Show("المنتج ده مش موجود في المخزون. اكتب اسمه وسعر بيعه المقترح عشان يتضاف كمنتج جديد.", "منتج جديد", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
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
            currentPurchaseItems.Add(new PurchaseCartItem
            {
                Barcode = barcode,
                ProductName = txtPurchaseProductName.Text.Trim(),
                Qty = qty,
                UnitCost = unitCost,
                SalePrice = salePrice,
                LineTotal = lineTotal,
                IsSerialized = chkPurchaseSerialized.Checked,
                Imeis = imeis
            });

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

        private void BtnViewFullCart_Click(object sender, EventArgs e)
        {
            if (currentPurchaseItems.Count == 0)
            {
                MessageBox.Show("مفيش أصناف في الفاتورة لسه.", "الفاتورة فارغة", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ShowInvoiceItemsPopup(currentPurchaseItems, "🔍 عرض الفاتورة كاملة");
        }

        private void BtnViewPurchaseInvoice_Click(object sender, EventArgs e)
        {
            if (selectedStatementPurchaseId == null)
            {
                MessageBox.Show("من فضلك اختر فاتورة شراء من كشف الحساب فوق أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<PurchaseCartItem> items;
            try
            {
                items = SuppliersRepository.GetPurchaseItemsForInvoice(selectedStatementPurchaseId.Value);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (items.Count == 0)
            {
                MessageBox.Show("لم يتم العثور على بنود لهذه الفاتورة.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ShowInvoiceItemsPopup(items, "👁️ عرض فاتورة شراء محفوظة");
        }

        private void ShowInvoiceItemsPopup(List<PurchaseCartItem> items, string title)
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("المنتج"), new DataColumn("الكمية"), new DataColumn("السعر"), new DataColumn("الإجمالي"), new DataColumn("IMEI؟") });

            decimal grandTotal = 0;
            foreach (var item in items)
            {
                dt.Rows.Add(item.ProductName, item.Qty, item.UnitCost.ToString("N2"), item.LineTotal.ToString("N2"), item.IsSerialized ? "✅" : "-");
                grandTotal += item.LineTotal;
            }

            using Form popup = new Form()
            {
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(900, 600),
                MinimumSize = new Size(650, 400),
                Font = new Font("Segoe UI", 9F),
                BackColor = ColorBackground
            };

            DataGridView dgvFull = new DataGridView()
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowTemplate = { Height = 32 },
                DataSource = dt
            };
            StyleDataGridView(dgvFull);

            Label lblTotal = new Label()
            {
                Text = $"إجمالي الفاتورة: {grandTotal:N2} ج.م",
                Dock = DockStyle.Bottom,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = UIHelpers.ColorGreen
            };

            popup.Controls.Add(dgvFull);
            popup.Controls.Add(lblTotal);
            popup.ShowDialog(this.FindForm());
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
            bool isEditing = editingPurchaseId.HasValue;

            var lines = currentPurchaseItems.Select(x => new TemoStore.Core.Entities.PurchaseLine
            {
                Barcode = x.Barcode,
                ProductName = x.ProductName,
                Qty = x.Qty,
                UnitCost = x.UnitCost,
                SalePrice = x.SalePrice,
                LineTotal = x.LineTotal,
                IsSerialized = x.IsSerialized,
                Imeis = x.Imeis
            }).ToList();

            try
            {
                if (isEditing)
                    AppServices.CoreEngine.Execute(new EditPurchaseCommand { PurchaseId = editingPurchaseId.Value, SupplierId = supplierId, Lines = lines, PayCashNow = payCashNow, CashMethod = cashMethod, PerformedBy = AuthManager.CurrentUsername });
                else
                    AppServices.CoreEngine.Execute(new CreatePurchaseCommand { SupplierId = supplierId, Lines = lines, PayCashNow = payCashNow, CashMethod = cashMethod, PerformedBy = AuthManager.CurrentUsername });
            }
            catch (DuplicateImeiException ex)
            {
                MessageBox.Show(ex.Message, "رقم مكرر", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (InsufficientBalanceException ex)
            {
                MessageBox.Show(ex.Message, "رصيد غير كافٍ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (DateClosedException ex)
            {
                MessageBox.Show(ex.Message, "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            catch (ValidationException ex)
            {
                MessageBox.Show(string.Join(Environment.NewLine, ex.Errors), "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show((isEditing ? "حصل خطأ أثناء تعديل الفاتورة: " : "حصل خطأ أثناء حفظ الفاتورة ولم يتم حفظ أي حاجة: ") + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show(isEditing
                ? $"تم تعديل فاتورة الشراء بنجاح، الإجمالي الجديد {totalAmount:N2} ج.م."
                : $"تم حفظ فاتورة الشراء بنجاح بإجمالي {totalAmount:N2} ج.م، وتحديث المخزون تلقائي" + (payCashNow ? "، وتم خصم المبلغ فورًا من الرصيد." : "، وسجّلت كدين على المورد."),
                "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);

            ExitEditMode();
            currentPurchaseItems.Clear();
            RefreshPurchaseCartGrid();
            LoadSuppliersGrid();
            if (selectedSupplierId != -1) LoadSupplierStatement(selectedSupplierId);
        }

        private void EnterEditMode(int purchaseId, int supplierId)
        {
            editingPurchaseId = purchaseId;
            btnSavePurchase.Text = "حفظ التعديل ✏️";
            btnCancelEditPurchase.Visible = true;
            cmbSupplierOperationType.SelectedIndex = 0;
            cmbPurchaseSupplier.SelectedValue = supplierId;
        }

        private void ExitEditMode()
        {
            editingPurchaseId = null;
            btnSavePurchase.Text = "حفظ فاتورة الشراء 💾";
            btnCancelEditPurchase.Visible = false;
        }

        private void BtnCancelEditPurchase_Click(object sender, EventArgs e)
        {
            ExitEditMode();
            currentPurchaseItems.Clear();
            RefreshPurchaseCartGrid();
            txtPurchaseBarcode.Clear();
            txtPurchaseProductName.Clear();
            txtPurchaseQty.Clear();
            txtPurchaseUnitCost.Clear();
            txtPurchaseSalePrice.Clear();
            txtPurchaseImeiList.Clear();
            chkPurchaseSerialized.Checked = false;
        }

        private void DgvPurchaseCart_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= currentPurchaseItems.Count) return;
            currentPurchaseItems.RemoveAt(e.RowIndex);
            RefreshPurchaseCartGrid();
        }

        private void BtnEditPurchaseInvoice_Click(object sender, EventArgs e)
        {
            if (selectedStatementPurchaseId == null)
            {
                MessageBox.Show("من فضلك اختر فاتورة شراء من كشف الحساب فوق أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (selectedSupplierId == -1) return;

            List<PurchaseCartItem> items;
            try
            {
                items = SuppliersRepository.GetPurchaseItemsForInvoice(selectedStatementPurchaseId.Value);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (items.Count == 0)
            {
                MessageBox.Show("لم يتم العثور على بنود لهذه الفاتورة.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            currentPurchaseItems = items;
            RefreshPurchaseCartGrid();
            EnterEditMode(selectedStatementPurchaseId.Value, selectedSupplierId);

            MessageBox.Show("اتحمّلت بنود الفاتورة في العربة على اليسار. تقدر تشيل أي صنف بدبل كليك عليه، أو تضيف أصناف جديدة، وبعدين اضغط \"حفظ التعديل\" أو \"إلغاء التعديل\" لو غيّرت رأيك.", "وضع التعديل", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnCancelPurchaseInvoice_Click(object sender, EventArgs e)
        {
            if (selectedStatementPurchaseId == null)
            {
                MessageBox.Show("من فضلك اختر فاتورة شراء من كشف الحساب فوق أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("هل أنت متأكد من إلغاء فاتورة الشراء دي؟ هيترجع المخزون وأي مبلغ اتسدد كاش هيرجع للخزينة تلقائيًا.", "تأكيد الإلغاء", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                AppServices.CoreEngine.Execute(new CancelPurchaseCommand { PurchaseId = selectedStatementPurchaseId.Value, PerformedBy = AuthManager.CurrentUsername });
            }
            catch (DateClosedException ex)
            {
                MessageBox.Show(ex.Message, "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "لا يمكن الإلغاء", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (editingPurchaseId == selectedStatementPurchaseId)
            {
                ExitEditMode();
                currentPurchaseItems.Clear();
                RefreshPurchaseCartGrid();
            }

            MessageBox.Show("تم إلغاء فاتورة الشراء بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadSuppliersGrid();
            if (selectedSupplierId != -1) LoadSupplierStatement(selectedSupplierId);
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

            try
            {
                AppServices.CoreEngine.Execute(new PaySupplierCommand { SupplierId = supplierId, SupplierName = supplierName, Method = method, Amount = amount, PerformedBy = AuthManager.CurrentUsername });
            }
            catch (InsufficientBalanceException ex)
            {
                MessageBox.Show(ex.Message, "رصيد غير كافٍ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show("تم تسجيل السداد بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtSupplierPaymentAmount.Clear();
            LoadSuppliersGrid();
        }
    }
}
