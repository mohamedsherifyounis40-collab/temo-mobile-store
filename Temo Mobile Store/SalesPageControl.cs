using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using TemoStore.Core.Commands;
using TemoStore.Core.Entities;
using TemoStore.Core.Exceptions;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // SalesPageControl: نسخة مستقلة تمامًا من شاشة "نقطة البيع" الموجودة في Form1.cs
    // (داخل تبويب "العمليات اليومية" - pnlSaleOps / CreatePOSDesign)
    //
    // ملحوظة مهمة: ده UserControl منفصل بالكامل عن Form1.cs - معندوش أي اتصال
    // بحقوله أو دواله. بيقرا ويكتب على نفس قاعدة البيانات (TemoStoreDB.db) بالظبط.
    // Form1.cs القديم فاضل شغال 100% زي ما هو من غير أي تعديل.
    //
    // الفرق الوحيد عن النسخة الأصلية: لما عملية البيع تتسجل/تتعدل/تتلغي، مش
    // بننادي على دوال شاشات تانية (زي تحديث المخزون أو لوحة التحكم) لأنها مش
    // موجودة هنا - أي شاشة تانية هتاخد بياناتها محدثة تلقائيًا أول ما تتفتح.
    // ==========================================================================
    public partial class SalesPageControl : UserControl
    {
        private static readonly Color ColorPrimary = UIHelpers.ColorPrimary;
        private static readonly Color ColorSuccess = UIHelpers.ColorSuccess;
        private static readonly Color ColorDanger = UIHelpers.ColorDanger;
        private static readonly Color ColorWarning = UIHelpers.ColorWarning;
        private static readonly Color ColorNeutral = UIHelpers.ColorNeutral;
        private static readonly Color ColorBackground = UIHelpers.ColorBackground;

        // ---------- بيانات المتجر (لطباعة الفاتورة) - بنحمّلها مرة وقت فتح الشاشة ----------
        private string CurrentStoreName = "Temo Mobile Store";
        private string CurrentStorePhone = "";
        private string CurrentStoreAddress = "";
        private byte[] CurrentStoreLogo = null;
        private string CurrentStoreWhatsApp = "";

        // ---------- عناصر الشاشة ----------
        private Guna2TextBox txtSaleBarcode, txtSaleName, txtCustomerPrice, txtSaleQty, txtSaleTotal, txtItemDiscount, txtQuickDiscountPercent, txtQuickTaxPercent, txtAmountPaid;
        private Label lblChangeDue;
        private Label lblSaleImei;
        private Guna2ComboBox cmbSaleImei, cmbSalePaymentType, cmbSalePaymentMethod, cmbSaleCustomer, cmbInvoicePaperSize;
        private Guna2TextBox txtInvoiceNumber, txtInvoiceCustomer;
        private Guna2GradientButton btnAddItemToCart, btnAddToBill;
        private Guna2Button btnPrintInvoice, btnEditSaleMode, btnSaveSaleEdit, btnCancelSale;
        private DataGridView dgvSales, dgvSaleCart;
        private Label lblSaleCartTotal;

        // ---------- عناصر التصميم الجديد (تابات البحث/كروت الأكثر مبيعًا/سلة بأزرار كمية/دفع سريع) ----------
        private Guna2Button btnTabBarcode, btnTabProductSearch, btnTabSerialSearch, btnQuickAddCustomer, btnClearCart;
        private Guna2Button btnViewNewSale, btnViewHistory;
        private Guna2ShadowPanel pnlSearchCard, pnlCartCard, pnlPaymentCard, pnlGridCard;
        private Panel pnlTabBarcode, pnlTabProductSearch, pnlTabSerialSearch, pnlPopularProducts;
        private Guna2TextBox txtProductSearch, txtSerialSearch, txtInvoiceDate, txtInvoiceSeller;
        private DataGridView dgvProductSearchResults, dgvSerialSearchResults;
        private Guna2Button[] quickPaymentButtons;
        private Label lblTotalsGrandTotal, lblTotalsStatus, lblTotalsSubtotal, lblTotalsDiscount, lblTotalsTax;
        private System.Windows.Forms.Timer clockTimer;

        private readonly System.Collections.Generic.List<ProductSearchResult> lastProductSearchResults = new System.Collections.Generic.List<ProductSearchResult>();
        private readonly System.Collections.Generic.List<SerialSearchResult> lastSerialSearchResults = new System.Collections.Generic.List<SerialSearchResult>();

        // ---------- قائمة جانبية دائمة وشغالة فعليًا (زي MainShell بالظبط) جوه نافذة المبيعات المستقلة ----------
        private const int SidebarWidth = 170;
        private const int CardLeft = 20 + SidebarWidth + 10;
        private const int CardWidth = 1140 - SidebarWidth - 10;
        private const int InnerContentWidth = CardWidth - 40; // عرض المحتوى جوه الكارت (بعد هامش 20 يمين ويسار)

        private static readonly (string Key, string Text, string Icon)[] ShellNavItems = new (string, string, string)[]
        {
            (MainShell.PageKeys.Home,           "الرئيسية",          "🏠"),
            (MainShell.PageKeys.Sales,          "المبيعات",          "🛒"),
            (MainShell.PageKeys.Inventory,      "المخزون",           "🗄️"),
            (MainShell.PageKeys.InventoryCount, "جرد المخزن",        "📋"),
            (MainShell.PageKeys.Maintenance,    "الصيانة",           "🔧"),
            (MainShell.PageKeys.Customers,      "العملاء",           "👥"),
            (MainShell.PageKeys.Suppliers,      "الموردين",          "🚚"),
            (MainShell.PageKeys.Treasury,       "الخزينة",           "🏦"),
            (MainShell.PageKeys.Reports,        "التقارير",          "📊"),
            (MainShell.PageKeys.Accounts,       "الحسابات",          "📒"),
            (MainShell.PageKeys.Attendance,     "الحضور والمرتبات",  "🧑‍💼"),
            (MainShell.PageKeys.Settings,       "الإعدادات",         "⚙️"),
        };

        private int selectedSaleId = -1;

        // ---------- سلة الفاتورة الحالية - بتتجمع فيها الأصناف قبل ما "إتمام عملية البيع"
        // يسجّلها كلها مرة واحدة تحت رقم فاتورة واحد مشترك ----------
        private readonly List<SaleLine> currentSaleItems = new List<SaleLine>();

        public SalesPageControl()
        {
            this.Dock = DockStyle.Fill;
            // مقاس مبدئي واقعي قبل ما نبني الشاشة: عناصر الشاشة بتتبني وهي متربطة بمقاس UserControl الافتراضي
            // الصغير (150x150) لسه، فلو مفيش مقاس واقعي هنا، أي عنصر معاه Anchor بيحسب هامشه غلط تمامًا
            // (سالب)، وبمجرد ما الشاشة تتفتح في نافذتها الحقيقية الكبيرة بيطلع أعمدة الجدول تختفي/تكبر جدًا.
            this.Size = new Size(1150, 1150);
            this.AutoScroll = true;
            this.BackColor = ColorBackground;

            BuildUI();
            LoadStoreSettingsIntoMemory();
            LoadCustomersIntoCombo();
            LoadSalesData();
            ApplyEmployeeRestrictionsIfNeeded();
        }

        // ==========================================================================
        // لو المستخدم موظف عادي: يقدر يبيع بس، مايقدرش يعدّل أو يلغي بيع
        // ==========================================================================
        private void ApplyEmployeeRestrictionsIfNeeded()
        {
            if (AuthManager.IsAdmin) return;

            btnEditSaleMode.Enabled = false;
            btnSaveSaleEdit.Enabled = false;
            btnCancelSale.Enabled = false;
        }

        // ==========================================================================
        // بناء شكل الشاشة - نفس تصميم وأماكن Form1.cs (CreatePOSDesign) بالظبط
        // ==========================================================================
        private void BuildUI()
        {
            this.Size = new Size(1180, 890);

            BuildSidebarNav();

            // ---------- 1) كارت علوي: العنوان + تبديل العرض + بيانات العميل/الفاتورة (ثابت في العرضين) ----------
            Guna2ShadowPanel pnlCustomerCard = NewCard(new Point(CardLeft, 16), new Size(CardWidth, 92));

            Label lblScreenTitle = new Label() { Text = "🛒 فاتورة بيع جديدة", Location = new Point(20, 10), AutoSize = true, Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = UIHelpers.ColorAccentPrimary };

            btnPrintInvoice = new Guna2Button() { Text = "🖨️", Location = new Point(360, 8), Width = 34, Height = 30, FillColor = Color.White, ForeColor = UIHelpers.ColorAccentPrimary, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1, BorderRadius = 8 };
            btnPrintInvoice.Click += BtnPrintInvoice_Click;
            Guna2Button btnSendWhatsApp = new Guna2Button() { Text = "📱", Location = new Point(398, 8), Width = 34, Height = 30, FillColor = Color.White, ForeColor = UIHelpers.ColorAccentPrimary, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1, BorderRadius = 8 };
            btnSendWhatsApp.Click += BtnSendInvoiceWhatsApp_Click;
            Guna2Button btnSavePdf = new Guna2Button() { Text = "📄", Location = new Point(436, 8), Width = 34, Height = 30, FillColor = Color.White, ForeColor = UIHelpers.ColorAccentPrimary, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1, BorderRadius = 8 };
            btnSavePdf.Click += BtnSaveInvoicePdf_Click;
            cmbInvoicePaperSize = new Guna2ComboBox() { Location = new Point(478, 8), Width = 120, Height = 30, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbInvoicePaperSize.Items.AddRange(new string[] { "80 مم (حرارية)", "58 مم (حرارية)", "A4 (عادية)" });
            cmbInvoicePaperSize.SelectedIndex = 0;

            btnViewNewSale = new Guna2Button() { Text = "🧾 فاتورة جديدة", Location = new Point(610, 8), Width = 130, Height = 32, BorderRadius = 8, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            btnViewHistory = new Guna2Button() { Text = "📋 سجل المبيعات", Location = new Point(746, 8), Width = 154, Height = 32, BorderRadius = 8, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            btnViewNewSale.Click += (s, e) => SwitchMainView(0);
            btnViewHistory.Click += (s, e) => SwitchMainView(1);

            Label lblSaleCustomer = new Label() { Text = "العميل:", Location = new Point(20, 44), AutoSize = true, Font = new Font("Segoe UI", 7.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbSaleCustomer = new Guna2ComboBox() { Location = new Point(20, 60), Width = 170, Height = 26, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false, BorderRadius = 8 };
            btnQuickAddCustomer = new Guna2Button() { Text = "+", Location = new Point(196, 60), Width = 26, Height = 26, FillColor = UIHelpers.ColorAccentPrimary, BorderRadius = 8, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            btnQuickAddCustomer.Click += BtnQuickAddCustomer_Click;

            Label lblPaymentType = new Label() { Text = "نوع البيع:", Location = new Point(232, 44), AutoSize = true, Font = new Font("Segoe UI", 7.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbSalePaymentType = new Guna2ComboBox() { Location = new Point(232, 60), Width = 95, Height = 26, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbSalePaymentType.Items.AddRange(new string[] { "كاش", "آجل" });
            cmbSalePaymentType.SelectedIndex = 0;
            cmbSalePaymentType.SelectedIndexChanged += CmbSalePaymentType_SelectedIndexChanged;

            // وسيلة الدفع نفسها بتتحدد من أزرار "الدفع السريع" تحت - الكومبو ده بيفضل موجود
            // كمخزن قيمة فعلي بس مش ظاهر (نفس منطق BtnAddToBill_Click الأصلي قاري منه)
            cmbSalePaymentMethod = new Guna2ComboBox() { Location = new Point(232, 60), Width = 95, DropDownStyle = ComboBoxStyle.DropDownList, Visible = false };
            cmbSalePaymentMethod.Items.AddRange(UIHelpers.PaymentMethods);

            txtInvoiceNumber = new Guna2TextBox() { Location = new Point(337, 60), Width = 105, Height = 26, ReadOnly = true, BorderRadius = 8, FillColor = Color.FromArgb(240, 243, 248), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = UIHelpers.ColorAccentPrimary, Text = "🧾 فاتورة #-" };
            txtInvoiceCustomer = new Guna2TextBox() { Location = new Point(452, 60), Width = 120, Height = 26, ReadOnly = true, BorderRadius = 8, FillColor = Color.FromArgb(240, 243, 248), Font = new Font("Segoe UI", 8.5F), ForeColor = UIHelpers.ColorAccentPrimary, Text = "عميل نقدي" };
            txtInvoiceDate = new Guna2TextBox() { Location = new Point(582, 60), Width = 155, Height = 26, ReadOnly = true, BorderRadius = 8, FillColor = Color.FromArgb(240, 243, 248), Font = new Font("Segoe UI", 8F), Text = "📅 " + DateTime.Now.ToString("yyyy-MM-dd hh:mm tt") };
            txtInvoiceSeller = new Guna2TextBox() { Location = new Point(747, 60), Width = 115, Height = 26, ReadOnly = true, BorderRadius = 8, FillColor = Color.FromArgb(240, 243, 248), Font = new Font("Segoe UI", 8F), Text = "👤 " + AuthManager.CurrentUsername };

            clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            clockTimer.Tick += (s, e) => { txtInvoiceDate.Text = "📅 " + DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"); };
            clockTimer.Start();

            pnlCustomerCard.Controls.AddRange(new Control[] {
                lblScreenTitle, btnPrintInvoice, btnSendWhatsApp, btnSavePdf, cmbInvoicePaperSize, btnViewNewSale, btnViewHistory,
                lblSaleCustomer, cmbSaleCustomer, btnQuickAddCustomer, lblPaymentType, cmbSalePaymentType, cmbSalePaymentMethod,
                txtInvoiceNumber, txtInvoiceCustomer, txtInvoiceDate, txtInvoiceSeller
            });

            // ---------- 2) كارت البحث عن منتج (تابات: منتج / باركود / سيريال) ----------
            pnlSearchCard = NewCard(new Point(CardLeft, 124), new Size(CardWidth, 300));

            btnTabProductSearch = NewTabButton("منتج", new Point(20, 12));
            btnTabBarcode = NewTabButton("باركود", new Point(140, 12));
            btnTabSerialSearch = NewTabButton("سيريال", new Point(260, 12));
            btnTabProductSearch.Click += (s, e) => SwitchProductTab(1);
            btnTabBarcode.Click += (s, e) => SwitchProductTab(0);
            btnTabSerialSearch.Click += (s, e) => SwitchProductTab(2);

            // ---- تاب "باركود" ----
            pnlTabBarcode = new Panel() { Location = new Point(20, 50), Size = new Size(InnerContentWidth, 34) };
            txtSaleBarcode = new Guna2TextBox() { Location = new Point(0, 0), Width = 420, Height = 30, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251), PlaceholderText = "مسح الباركود (Enter)" };
            txtSaleBarcode.KeyDown += TxtSaleBarcode_KeyDown;
            pnlTabBarcode.Controls.Add(txtSaleBarcode);

            // ---- تاب "منتج" (بحث بالاسم أو الباركود) ----
            pnlTabProductSearch = new Panel() { Location = new Point(20, 50), Size = new Size(InnerContentWidth, 108), Visible = false };
            txtProductSearch = new Guna2TextBox() { Location = new Point(0, 0), Width = 420, Height = 30, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251), PlaceholderText = "بحث بالاسم أو الباركود" };
            txtProductSearch.TextChanged += TxtProductSearch_TextChanged;
            dgvProductSearchResults = new DataGridView() { Location = new Point(0, 36), Size = new Size(InnerContentWidth, 68), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoGenerateColumns = false };
            dgvProductSearchResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPName", HeaderText = "المنتج" });
            dgvProductSearchResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPBarcode", HeaderText = "الباركود" });
            dgvProductSearchResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPPrice", HeaderText = "السعر" });
            dgvProductSearchResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPQty", HeaderText = "المتاح" });
            dgvProductSearchResults.CellDoubleClick += DgvProductSearchResults_CellDoubleClick;
            StyleDataGridView(dgvProductSearchResults);
            pnlTabProductSearch.Controls.AddRange(new Control[] { txtProductSearch, dgvProductSearchResults });

            // ---- تاب "سيريال" (بحث بالـIMEI) ----
            pnlTabSerialSearch = new Panel() { Location = new Point(20, 50), Size = new Size(InnerContentWidth, 108), Visible = false };
            txtSerialSearch = new Guna2TextBox() { Location = new Point(0, 0), Width = 420, Height = 30, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251), PlaceholderText = "بحث بالـIMEI" };
            txtSerialSearch.TextChanged += TxtSerialSearch_TextChanged;
            dgvSerialSearchResults = new DataGridView() { Location = new Point(0, 36), Size = new Size(InnerContentWidth, 68), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoGenerateColumns = false };
            dgvSerialSearchResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSName", HeaderText = "المنتج" });
            dgvSerialSearchResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSBarcode", HeaderText = "الباركود" });
            dgvSerialSearchResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSImei", HeaderText = "IMEI" });
            dgvSerialSearchResults.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSPrice", HeaderText = "السعر" });
            dgvSerialSearchResults.CellDoubleClick += DgvSerialSearchResults_CellDoubleClick;
            StyleDataGridView(dgvSerialSearchResults);
            pnlTabSerialSearch.Controls.AddRange(new Control[] { txtSerialSearch, dgvSerialSearchResults });

            // ---- حقول الصنف المشتركة (اسم/سعر/كمية/IMEI/إجمالي) ----
            int fieldsY = 168;
            Label lblName = new Label() { Text = "اسم المنتج:", Location = new Point(20, fieldsY), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtSaleName = new Guna2TextBox() { Location = new Point(20, fieldsY + 18), Width = 190, Height = 28, ReadOnly = true, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblPrice = new Label() { Text = "سعر البيع:", Location = new Point(220, fieldsY), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtCustomerPrice = new Guna2TextBox() { Location = new Point(220, fieldsY + 18), Width = 80, Height = 28, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };
            txtCustomerPrice.TextChanged += TxtSaleQty_TextChanged;

            Label lblQty = new Label() { Text = "الكمية:", Location = new Point(310, fieldsY), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtSaleQty = new Guna2TextBox() { Location = new Point(310, fieldsY + 18), Width = 55, Height = 28, Text = "1", BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };
            txtSaleQty.TextChanged += TxtSaleQty_TextChanged;

            Label lblItemDiscount = new Label() { Text = "خصم الصنف:", Location = new Point(375, fieldsY), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtItemDiscount = new Guna2TextBox() { Location = new Point(375, fieldsY + 18), Width = 75, Height = 28, Text = "0", BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };
            txtItemDiscount.TextChanged += TxtSaleQty_TextChanged;

            lblSaleImei = new Label() { Text = "اختار IMEI:", Location = new Point(460, fieldsY), AutoSize = true, Visible = false, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbSaleImei = new Guna2ComboBox() { Location = new Point(460, fieldsY + 18), Width = 145, Height = 28, DropDownStyle = ComboBoxStyle.DropDownList, Visible = false, BorderRadius = 8 };

            Guna2Panel pnlTotal = new Guna2Panel() { Location = new Point(615, fieldsY - 6), Size = new Size(140, 60), FillColor = UIHelpers.LightTint(UIHelpers.ColorOrange, 0.85f), BorderRadius = 10 };
            Label lblTotal = new Label() { Text = "إجمالي الصنف", Location = new Point(12, 5), AutoSize = true, Font = new Font("Segoe UI", 7.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtSaleTotal = new Guna2TextBox() { Location = new Point(12, 23), Width = 116, ReadOnly = true, BorderRadius = 6, FillColor = UIHelpers.LightTint(UIHelpers.ColorOrange, 0.85f), Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = UIHelpers.ColorOrange };
            pnlTotal.Controls.AddRange(new Control[] { lblTotal, txtSaleTotal });

            btnAddItemToCart = new Guna2GradientButton() { Text = "➕ إضافة للسلة", Location = new Point(770, fieldsY - 6), Width = 150, Height = 60, FillColor = UIHelpers.ColorAccentPrimary, FillColor2 = Color.FromArgb(20, 90, 190), GradientMode = System.Drawing.Drawing2D.LinearGradientMode.ForwardDiagonal, BorderRadius = 10, ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            btnAddItemToCart.Click += BtnAddItemToCart_Click;

            // ---- صف "الأكثر مبيعًا" ----
            Label lblPopularTitle = new Label() { Text = "⭐ الأكثر مبيعًا:", Location = new Point(20, fieldsY + 66), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = UIHelpers.ColorAccentPrimary };
            pnlPopularProducts = new Panel() { Location = new Point(140, fieldsY + 58), Size = new Size(InnerContentWidth - 120, 64), AutoScroll = true, AutoScrollMinSize = new Size(0, 64) };

            pnlSearchCard.Controls.AddRange(new Control[] {
                btnTabProductSearch, btnTabBarcode, btnTabSerialSearch, pnlTabBarcode, pnlTabProductSearch, pnlTabSerialSearch,
                lblName, txtSaleName, lblPrice, txtCustomerPrice, lblQty, txtSaleQty, lblItemDiscount, txtItemDiscount,
                lblSaleImei, cmbSaleImei, pnlTotal, btnAddItemToCart,
                lblPopularTitle, pnlPopularProducts
            });

            // ---------- 3) جدول السلة الحالية ----------
            pnlCartCard = NewCard(new Point(CardLeft, 440), new Size(CardWidth, 210));
            Label lblCartTitle = new Label() { Text = "🧺 السلة الحالية", Location = new Point(20, 12), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = UIHelpers.ColorAccentPrimary };

            btnClearCart = new Guna2Button() { Text = "مسح الكل 🗑", Location = new Point(20 + InnerContentWidth - 120, 10), Width = 120, Height = 28, FillColor = UIHelpers.ColorRed, BorderRadius = 8, Font = new Font("Segoe UI", 8F) };
            btnClearCart.Click += BtnClearCart_Click;

            dgvSaleCart = new DataGridView() { Location = new Point(20, 44), Size = new Size(InnerContentWidth, 130), ReadOnly = true, AllowUserToAddRows = false, AutoGenerateColumns = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            dgvSaleCart.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProduct", HeaderText = "المنتج", FillWeight = 24 });
            dgvSaleCart.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCode", HeaderText = "الباركود/السيريال", FillWeight = 15 });
            dgvSaleCart.Columns.Add(new DataGridViewButtonColumn { Name = "colDecrease", HeaderText = "", FillWeight = 7 });
            dgvSaleCart.Columns.Add(new DataGridViewTextBoxColumn { Name = "colQty", HeaderText = "الكمية", FillWeight = 8 });
            dgvSaleCart.Columns.Add(new DataGridViewButtonColumn { Name = "colIncrease", HeaderText = "", FillWeight = 7 });
            dgvSaleCart.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPrice", HeaderText = "سعر الوحدة", FillWeight = 12 });
            dgvSaleCart.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDiscount", HeaderText = "الخصم", FillWeight = 10 });
            dgvSaleCart.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTotal", HeaderText = "الإجمالي", FillWeight = 13 });
            dgvSaleCart.Columns.Add(new DataGridViewButtonColumn { Name = "colDelete", HeaderText = "", FillWeight = 7 });
            dgvSaleCart.CellContentClick += DgvSaleCart_CellContentClick;
            dgvSaleCart.CellDoubleClick += DgvSaleCart_CellDoubleClick;
            StyleDataGridView(dgvSaleCart);

            lblSaleCartTotal = new Label() { Text = "عدد الأصناف: 0    |    إجمالي السلة: 0.00 ج.م", Location = new Point(20, 182), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = UIHelpers.ColorGreen };

            pnlCartCard.Controls.AddRange(new Control[] { lblCartTitle, btnClearCart, dgvSaleCart, lblSaleCartTotal });

            // ---------- 4) طرق دفع سريعة + الإجماليات + تأكيد الفاتورة (كارت واحد مدمج) ----------
            pnlPaymentCard = NewCard(new Point(CardLeft, 666), new Size(CardWidth, 216));

            Label lblQuickPayTitle = new Label() { Text = "طرق دفع سريعة", Location = new Point(20, 12), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = UIHelpers.ColorAccentPrimary };
            BuildQuickPaymentButtons(pnlPaymentCard, new Point(20, 32));

            Label lblQuickDiscount = new Label() { Text = "خصم سريع على الفاتورة (%):", Location = new Point(20, 82), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQuickDiscountPercent = new Guna2TextBox() { Location = new Point(230, 78), Width = 70, Height = 26, Text = "0", BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };
            txtQuickDiscountPercent.TextChanged += (s, e) => UpdateTotalsSummary();

            Label lblQuickTax = new Label() { Text = "ضريبة سريعة على الفاتورة (%):", Location = new Point(330, 82), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQuickTaxPercent = new Guna2TextBox() { Location = new Point(560, 78), Width = 70, Height = 26, Text = "0", BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };
            txtQuickTaxPercent.TextChanged += (s, e) => UpdateTotalsSummary();

            Label lblAmountPaid = new Label() { Text = "المبلغ المدفوع (كاش):", Location = new Point(20, 116), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtAmountPaid = new Guna2TextBox() { Location = new Point(180, 112), Width = 90, Height = 26, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251), PlaceholderText = "المبلغ بالظبط" };
            txtAmountPaid.TextChanged += (s, e) => UpdateTotalsSummary();

            Label lblChangeDueCaption = new Label() { Text = "الباقي (الفكة):", Location = new Point(300, 116), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            lblChangeDue = new Label() { Text = "0.00 ج.م", Location = new Point(400, 114), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = UIHelpers.ColorGreen };

            btnAddToBill = new Guna2GradientButton() { Text = "✅ تأكيد الفاتورة", Location = new Point(20, 154), Width = 610, Height = 50, FillColor = UIHelpers.ColorGreen, FillColor2 = Color.FromArgb(14, 130, 70), GradientMode = System.Drawing.Drawing2D.LinearGradientMode.ForwardDiagonal, BorderRadius = 12, ForeColor = Color.White, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
            btnAddToBill.Click += BtnAddToBill_Click;

            Guna2GradientPanel pnlTotalsBox = new Guna2GradientPanel() { Location = new Point(650, 12), Size = new Size(InnerContentWidth - 630, 192), FillColor = UIHelpers.LightTint(UIHelpers.ColorGreen, 0.92f), FillColor2 = UIHelpers.LightTint(UIHelpers.ColorAccentPrimary, 0.9f), GradientMode = System.Drawing.Drawing2D.LinearGradientMode.ForwardDiagonal, BorderRadius = 10 };
            Label lblSubtotalCaption = new Label() { Text = "الإجمالي الفرعي", Location = new Point(16, 8), AutoSize = true, Font = new Font("Segoe UI", 7.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            lblTotalsSubtotal = new Label() { Text = "0.00 ج.م", Location = new Point(16, 22), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = ColorPrimary };
            lblTotalsDiscount = new Label() { Text = "- 0.00 ج.م", Location = new Point(16, 44), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = UIHelpers.ColorRed };
            lblTotalsTax = new Label() { Text = "+ 0.00 ج.م", Location = new Point(16, 64), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = UIHelpers.ColorOrange };
            Label lblTotalsCaption = new Label() { Text = "الإجمالي الصافي", Location = new Point(16, 88), AutoSize = true, Font = new Font("Segoe UI", 7.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            lblTotalsGrandTotal = new Label() { Text = "0.00 ج.م", Location = new Point(16, 104), AutoSize = true, Font = new Font("Segoe UI", 15, FontStyle.Bold), ForeColor = UIHelpers.ColorGreen };
            lblTotalsStatus = new Label() { Text = "مدفوعة بالكامل", Location = new Point(16, 140), Size = new Size(210, 28), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = UIHelpers.ColorGreen };
            pnlTotalsBox.Controls.AddRange(new Control[] { lblSubtotalCaption, lblTotalsSubtotal, lblTotalsDiscount, lblTotalsTax, lblTotalsCaption, lblTotalsGrandTotal, lblTotalsStatus });

            pnlPaymentCard.Controls.AddRange(new Control[] { lblQuickPayTitle, lblQuickDiscount, txtQuickDiscountPercent, lblQuickTax, txtQuickTaxPercent, lblAmountPaid, txtAmountPaid, lblChangeDueCaption, lblChangeDue, btnAddToBill, pnlTotalsBox });

            // ---------- 5) كارت سجل المبيعات (شاشة تانية بالتبديل، بتاخد المساحة كاملة تحت الكارت العلوي) ----------
            pnlGridCard = new Guna2ShadowPanel()
            {
                Location = new Point(CardLeft, 124),
                Size = new Size(CardWidth, 706),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                FillColor = Color.White,
                Radius = UIHelpers.CardBorderRadius,
                ShadowColor = Color.FromArgb(45, 30, 45, 90),
                ShadowDepth = 22,
                ShadowShift = 3,
                ShadowStyle = Guna2ShadowPanel.ShadowMode.Dropped,
                Visible = false
            };
            Label lblGridTitle = new Label() { Text = "📋 سجل المبيعات (دوس على فاتورة عشان تعدلها أو تلغيها)", Location = new Point(20, 18), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = UIHelpers.ColorAccentPrimary };

            dgvSales = new DataGridView()
            {
                Location = new Point(20, 55),
                Size = new Size(InnerContentWidth, 590),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false
            };
            dgvSales.CellClick += DgvSales_CellClick;
            StyleDataGridView(dgvSales);

            // ---------- تعديل/إلغاء البيع المحدد - صف أزرار تحت جدول سجل المبيعات مباشرة
            // (نفس نمط "تعديل/إلغاء فاتورة شراء" في شاشة الموردين) بدل كارت منفصل تحت كارت البيع ----------
            btnEditSaleMode = new Guna2Button() { Text = "تعديل البيع المحدد ✏️", Location = new Point(20, 656), Width = 350, Height = 34, Anchor = AnchorStyles.Bottom | AnchorStyles.Left, FillColor = UIHelpers.ColorOrange, BorderRadius = 9 };
            btnEditSaleMode.Click += BtnEditSaleMode_Click;

            btnSaveSaleEdit = new Guna2Button() { Text = "حفظ تعديل البيع 💾", Location = new Point(390, 656), Width = 350, Height = 34, Anchor = AnchorStyles.Bottom | AnchorStyles.Left, FillColor = UIHelpers.ColorGreen, Enabled = false, BorderRadius = 9 };
            btnSaveSaleEdit.Click += BtnSaveSaleEdit_Click;

            btnCancelSale = new Guna2Button() { Text = "إلغاء البيع ❌", Location = new Point(760, 656), Width = 360, Height = 34, Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, FillColor = UIHelpers.ColorRed, BorderRadius = 9 };
            btnCancelSale.Click += BtnCancelSale_Click;

            pnlGridCard.Controls.AddRange(new Control[] { lblGridTitle, dgvSales, btnEditSaleMode, btnSaveSaleEdit, btnCancelSale });

            this.Controls.AddRange(new Control[] { pnlCustomerCard, pnlSearchCard, pnlCartCard, pnlPaymentCard, pnlGridCard });

            SwitchProductTab(0);
            SwitchMainView(0);
            LoadPopularProductsRow();
            UpdateTotalsSummary();
        }

        // ==========================================================================
        // قائمة جانبية دائمة وشغالة فعليًا (مش ديكور) - بتظهر بس جوه نافذة المبيعات المستقلة دي،
        // وكل بند فيها (غير "المبيعات" نفسها) بينادي MainShell.Instance.OpenPageByKey(key) اللي
        // بتفتح/تفعّل نفس نافذة الصفحة التانية اللي القائمة الجانبية الأصلية في MainShell بتفتحها
        // بالظبط - من غير ما نكرر أي منطق فتح نوافذ هنا.
        // ==========================================================================
        private void BuildSidebarNav()
        {
            Guna2GradientPanel pnlSidebarNav = new Guna2GradientPanel()
            {
                Location = new Point(0, 0),
                Size = new Size(SidebarWidth, 900),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left,
                FillColor = UIHelpers.ColorSidebar,
                FillColor2 = Color.FromArgb(30, 46, 78),
                GradientMode = System.Drawing.Drawing2D.LinearGradientMode.ForwardDiagonal
            };

            Label lblLogo = new Label()
            {
                Text = "📱 TEMO\nMOBILE STORE",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 0),
                Size = new Size(SidebarWidth, 70)
            };
            pnlSidebarNav.Controls.Add(lblLogo);

            var adminOnlyKeys = new System.Collections.Generic.HashSet<string> {
                MainShell.PageKeys.Customers, MainShell.PageKeys.Suppliers, MainShell.PageKeys.Reports,
                MainShell.PageKeys.Accounts, MainShell.PageKeys.Settings, MainShell.PageKeys.InventoryCount, MainShell.PageKeys.Attendance
            };
            var visibleItems = AuthManager.IsAdmin ? ShellNavItems : ShellNavItems.Where(x => !adminOnlyKeys.Contains(x.Key)).ToArray();

            int y = 78;
            foreach (var item in visibleItems)
            {
                bool isCurrent = item.Key == MainShell.PageKeys.Sales;
                Guna2Button btn = new Guna2Button()
                {
                    Text = $"{item.Icon}  {item.Text}",
                    Location = new Point(8, y),
                    Size = new Size(SidebarWidth - 16, 38),
                    FillColor = isCurrent ? UIHelpers.ColorAccentPrimary : Color.Transparent,
                    ForeColor = isCurrent ? Color.White : Color.FromArgb(190, 200, 218),
                    BorderRadius = 8,
                    Font = new Font("Segoe UI", 8.5F, isCurrent ? FontStyle.Bold : FontStyle.Regular)
                };
                string key = item.Key;
                if (!isCurrent)
                    btn.Click += (s, e) => MainShell.Instance?.OpenPageByKey(key);
                pnlSidebarNav.Controls.Add(btn);
                y += 44;
            }

            this.Controls.Add(pnlSidebarNav);
        }

        // ---------- التبديل بين "فاتورة جديدة" و"سجل المبيعات" داخل نفس الشاشة (بدون سكرول) ----------
        private void SwitchMainView(int viewIndex)
        {
            bool newSale = viewIndex == 0;
            pnlSearchCard.Visible = newSale;
            pnlCartCard.Visible = newSale;
            pnlPaymentCard.Visible = newSale;
            pnlGridCard.Visible = !newSale;

            btnViewNewSale.FillColor = newSale ? UIHelpers.ColorAccentPrimary : Color.White;
            btnViewNewSale.ForeColor = newSale ? Color.White : UIHelpers.ColorAccentPrimary;
            btnViewHistory.FillColor = !newSale ? UIHelpers.ColorAccentPrimary : Color.White;
            btnViewHistory.ForeColor = !newSale ? Color.White : UIHelpers.ColorAccentPrimary;

            if (!newSale) LoadSalesData();
        }

        // ---------- كارت أبيض قياسي (نفس الأسلوب المستخدم في كل الكروت الجديدة) ----------
        private static Guna2ShadowPanel NewCard(Point location, Size size) => new Guna2ShadowPanel()
        {
            Location = location,
            Size = size,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            FillColor = Color.White,
            Radius = UIHelpers.CardBorderRadius,
            ShadowColor = Color.FromArgb(45, 30, 45, 90),
            ShadowDepth = 22,
            ShadowShift = 3,
            ShadowStyle = Guna2ShadowPanel.ShadowMode.Dropped
        };

        // ---------- زرار Tab بحث المنتج (يتلوّن لما يبقى نشط في SwitchProductTab) ----------
        private static Guna2Button NewTabButton(string text, Point location) => new Guna2Button()
        {
            Text = text,
            Location = location,
            Width = 110,
            Height = 34,
            FillColor = Color.White,
            ForeColor = UIHelpers.ColorAccentPrimary,
            BorderColor = UIHelpers.ColorAccentPrimary,
            BorderThickness = 1,
            BorderRadius = 8,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };

        // ---------- التبديل بين تابات البحث الثلاثة (منتج / باركود / سيريال) ----------
        private void SwitchProductTab(int tabIndex)
        {
            pnlTabBarcode.Visible = tabIndex == 0;
            pnlTabProductSearch.Visible = tabIndex == 1;
            pnlTabSerialSearch.Visible = tabIndex == 2;

            Guna2Button[] tabs = { btnTabBarcode, btnTabProductSearch, btnTabSerialSearch };
            for (int i = 0; i < tabs.Length; i++)
            {
                bool active = i == tabIndex;
                tabs[i].FillColor = active ? UIHelpers.ColorAccentPrimary : Color.White;
                tabs[i].ForeColor = active ? Color.White : UIHelpers.ColorAccentPrimary;
            }

            if (tabIndex == 0) txtSaleBarcode.Focus();
            else if (tabIndex == 1) txtProductSearch.Focus();
            else txtSerialSearch.Focus();
        }

        // ---------- بناء أزرار "الدفع السريع" الستة (نفس UIHelpers.PaymentMethods بالظبط) ----------
        private void BuildQuickPaymentButtons(Control host, Point start)
        {
            string[] methods = UIHelpers.PaymentMethods;
            quickPaymentButtons = new Guna2Button[methods.Length];
            int x = start.X;
            for (int i = 0; i < methods.Length; i++)
            {
                string method = methods[i];
                Guna2Button btn = new Guna2Button()
                {
                    Text = method,
                    Location = new Point(x, start.Y),
                    Width = 115,
                    Height = 40,
                    FillColor = Color.White,
                    ForeColor = UIHelpers.ColorAccentPrimary,
                    BorderColor = UIHelpers.ColorAccentPrimary,
                    BorderThickness = 1,
                    BorderRadius = 8,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
                };
                btn.Click += (s, e) => SelectQuickPaymentMethod(method);
                quickPaymentButtons[i] = btn;
                host.Controls.Add(btn);
                x += 122;
            }
        }

        // ---------- تحديد وسيلة الدفع من الأزرار السريعة (مفعّلة بس لو البيع كاش) ----------
        private void SelectQuickPaymentMethod(string method)
        {
            if (!cmbSalePaymentMethod.Enabled)
            {
                MessageBox.Show("البيع آجل - اختار العميل من الكارت اللي فوق بدل وسيلة الدفع.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            cmbSalePaymentMethod.SelectedItem = method;
            HighlightSelectedQuickPaymentButton(method);
            UpdateTotalsSummary();
        }

        private void HighlightSelectedQuickPaymentButton(string method)
        {
            if (quickPaymentButtons == null) return;
            foreach (var btn in quickPaymentButtons)
            {
                bool selected = btn.Text == method;
                btn.FillColor = selected ? UIHelpers.ColorAccentPrimary : Color.White;
                btn.ForeColor = selected ? Color.White : UIHelpers.ColorAccentPrimary;
            }
        }

        // ---------- إجماليات السلة الحالية (قبل تسجيل البيع) - الإجمالي الفرعي، خصم الأصناف،
        // خصم الفاتورة السريع (لسه معاينة بس، هيتطبق فعليًا وقت التأكيد)، والصافي النهائي ----------
        private (decimal Subtotal, decimal ItemsDiscount, decimal QuickDiscountAmount, decimal Net, decimal TaxAmount, decimal AmountDue) CalculateCartTotals()
        {
            decimal subtotal = 0, itemsDiscount = 0;
            foreach (var item in currentSaleItems)
            {
                subtotal += item.Total;
                itemsDiscount += item.Discount;
            }

            decimal.TryParse(txtQuickDiscountPercent?.Text, out decimal quickPercent);
            quickPercent = Math.Max(0, Math.Min(100, quickPercent));
            decimal afterItemsDiscount = subtotal - itemsDiscount;
            decimal quickDiscountAmount = afterItemsDiscount * quickPercent / 100m;

            decimal net = subtotal - itemsDiscount - quickDiscountAmount;

            decimal.TryParse(txtQuickTaxPercent?.Text, out decimal quickTaxPercent);
            quickTaxPercent = Math.Max(0, quickTaxPercent);
            decimal taxAmount = net * quickTaxPercent / 100m;
            decimal amountDue = net + taxAmount;

            return (subtotal, itemsDiscount, quickDiscountAmount, net, taxAmount, amountDue);
        }

        // ---------- تحديث كارت الإجماليات (الإجمالي الفرعي/الخصم/الضريبة/الصافي + حالة الفاتورة المتوقعة + الباقي) ----------
        private void UpdateTotalsSummary()
        {
            if (lblTotalsGrandTotal == null) return;
            var totals = CalculateCartTotals();
            decimal totalDiscount = totals.ItemsDiscount + totals.QuickDiscountAmount;

            lblTotalsSubtotal.Text = $"{totals.Subtotal:N2} ج.م";
            lblTotalsDiscount.Text = $"- {totalDiscount:N2} ج.م";
            lblTotalsTax.Text = $"+ {totals.TaxAmount:N2} ج.م";
            lblTotalsGrandTotal.Text = $"{totals.AmountDue:N2} ج.م";

            bool isCredit = cmbSalePaymentType.SelectedItem?.ToString() == "آجل";
            if (isCredit)
            {
                lblTotalsStatus.Text = "آجل - غير مدفوعة";
                lblTotalsStatus.ForeColor = UIHelpers.ColorOrange;
            }
            else
            {
                string method = cmbSalePaymentMethod.SelectedItem?.ToString();
                lblTotalsStatus.Text = string.IsNullOrEmpty(method) ? "مدفوعة بالكامل" : $"مدفوعة بالكامل ({method})";
                lblTotalsStatus.ForeColor = UIHelpers.ColorGreen;
            }

            if (txtAmountPaid != null && lblChangeDue != null)
            {
                if (!isCredit && decimal.TryParse(txtAmountPaid.Text, out decimal amountPaid) && amountPaid > 0)
                {
                    decimal change = amountPaid - totals.AmountDue;
                    lblChangeDue.Text = $"{change:N2} ج.م";
                    lblChangeDue.ForeColor = change < 0 ? UIHelpers.ColorRed : UIHelpers.ColorGreen;
                }
                else
                {
                    lblChangeDue.Text = "0.00 ج.م";
                    lblChangeDue.ForeColor = UIHelpers.ColorGreen;
                }
            }
        }

        // ---------- إضافة عميل جديد سريع (اسم + تليفون) من نفس شاشة البيع ----------
        private void BtnQuickAddCustomer_Click(object sender, EventArgs e)
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "إضافة عميل جديد";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.ClientSize = new Size(340, 190);
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.RightToLeft = RightToLeft.Yes;
                dlg.RightToLeftLayout = true;

                Label lblName = new Label() { Text = "اسم العميل:", Location = new Point(20, 16), AutoSize = true };
                Guna2TextBox txtName = new Guna2TextBox() { Location = new Point(20, 38), Width = 300, BorderRadius = 8 };
                Label lblPhone = new Label() { Text = "رقم الجوال:", Location = new Point(20, 76), AutoSize = true };
                Guna2TextBox txtPhone = new Guna2TextBox() { Location = new Point(20, 98), Width = 300, BorderRadius = 8 };
                Guna2Button btnSave = new Guna2Button() { Text = "حفظ", Location = new Point(20, 138), Width = 300, Height = 34, FillColor = UIHelpers.ColorGreen, BorderRadius = 9 };

                btnSave.Click += (s2, e2) =>
                {
                    if (string.IsNullOrWhiteSpace(txtName.Text))
                    {
                        MessageBox.Show("من فضلك أدخل اسم العميل.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    try { CustomersRepository.AddCustomer(txtName.Text.Trim(), txtPhone.Text.Trim()); }
                    catch (Exception ex) { MessageBox.Show(ex.Message); return; }
                    dlg.DialogResult = DialogResult.OK;
                    dlg.Close();
                };

                dlg.Controls.AddRange(new Control[] { lblName, txtName, lblPhone, txtPhone, btnSave });
                string addedName = txtName.Text;
                if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
                {
                    addedName = txtName.Text.Trim();
                    LoadCustomersIntoCombo();
                    SelectCustomerByName(addedName);
                }
            }
        }

        private void SelectCustomerByName(string name)
        {
            if (!(cmbSaleCustomer.DataSource is DataTable)) return;
            foreach (DataRowView row in cmbSaleCustomer.Items)
            {
                if (row["CustomerName"].ToString() == name)
                {
                    cmbSaleCustomer.SelectedItem = row;
                    return;
                }
            }
        }

        // ---------- تاب "منتج": بحث بالاسم أو الباركود أثناء الكتابة ----------
        private void TxtProductSearch_TextChanged(object sender, EventArgs e)
        {
            string term = txtProductSearch.Text.Trim();
            dgvProductSearchResults.Rows.Clear();
            lastProductSearchResults.Clear();
            if (term.Length == 0) return;

            try
            {
                lastProductSearchResults.AddRange(SalesRepository.SearchProducts(term));
                foreach (var r in lastProductSearchResults)
                    dgvProductSearchResults.Rows.Add(r.ProductName, r.Barcode, r.SalePrice.ToString("N2"), r.Quantity);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void DgvProductSearchResults_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= lastProductSearchResults.Count) return;
            var r = lastProductSearchResults[e.RowIndex];
            LoadProductIntoCurrentItemFields(r.Barcode, r.ProductName, r.SalePrice, r.Quantity, r.IsSerialized);
        }

        // ---------- تاب "سيريال": بحث بالـIMEI أثناء الكتابة ----------
        private void TxtSerialSearch_TextChanged(object sender, EventArgs e)
        {
            string term = txtSerialSearch.Text.Trim();
            dgvSerialSearchResults.Rows.Clear();
            lastSerialSearchResults.Clear();
            if (term.Length == 0) return;

            try
            {
                lastSerialSearchResults.AddRange(SalesRepository.SearchProductsBySerial(term));
                foreach (var r in lastSerialSearchResults)
                    dgvSerialSearchResults.Rows.Add(r.ProductName, r.Barcode, r.IMEI, r.SalePrice.ToString("N2"));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void DgvSerialSearchResults_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= lastSerialSearchResults.Count) return;
            var r = lastSerialSearchResults[e.RowIndex];
            if (currentSaleItems.Exists(x => x.Imei == r.IMEI))
            {
                MessageBox.Show("الجهاز ده اتضاف للسلة بالفعل.", "مكرر", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            LoadProductIntoCurrentItemFields(r.Barcode, r.ProductName, r.SalePrice, 1, true, r.IMEI);
        }

        // ---------- صف "الأكثر مبيعًا" - كروت بدون صور (اسم + سعر + أيقونة) ----------
        private void LoadPopularProductsRow()
        {
            pnlPopularProducts.Controls.Clear();

            System.Collections.Generic.List<ProductSearchResult> popular;
            try { popular = SalesRepository.GetPopularProducts(6); }
            catch { popular = new System.Collections.Generic.List<ProductSearchResult>(); }

            if (popular.Count == 0)
            {
                Label lblEmpty = new Label() { Text = "لسه معندكش مبيعات كفاية لعرض الأكثر مبيعًا", AutoSize = true, ForeColor = Color.FromArgb(150, 155, 165), Font = new Font("Segoe UI", 8.5F), Location = new Point(0, 6) };
                pnlPopularProducts.Controls.Add(lblEmpty);
                return;
            }

            int x = 0;
            foreach (var p in popular)
            {
                Guna2GradientPanel card = new Guna2GradientPanel() { Location = new Point(x, 0), Size = new Size(170, 60), FillColor = UIHelpers.LightTint(UIHelpers.ColorAccentPrimary, 0.94f), FillColor2 = UIHelpers.LightTint(UIHelpers.ColorAccentPrimary, 0.82f), GradientMode = System.Drawing.Drawing2D.LinearGradientMode.Vertical, BorderRadius = 8, Cursor = Cursors.Hand };

                Control thumb;
                if (p.Image != null && p.Image.Length > 0)
                {
                    var pic = new PictureBox() { Location = new Point(6, 8), Size = new Size(44, 44), SizeMode = PictureBoxSizeMode.Zoom, Cursor = Cursors.Hand };
                    using (var ms = new System.IO.MemoryStream(p.Image)) pic.Image = Image.FromStream(ms);
                    thumb = pic;
                }
                else
                {
                    thumb = new Label() { Text = p.IsSerialized ? "📱" : "📦", Location = new Point(6, 6), Size = new Size(44, 44), Font = new Font("Segoe UI Emoji", 18F), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
                }

                Label lblName = new Label() { Text = p.ProductName, Location = new Point(54, 7), Size = new Size(110, 30), Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), ForeColor = UIHelpers.ColorAccentPrimary, AutoEllipsis = true, Cursor = Cursors.Hand };
                Label lblPrice = new Label() { Text = $"{p.SalePrice:N0} ج.م", Location = new Point(54, 37), Size = new Size(110, 18), Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = UIHelpers.ColorGreen, Cursor = Cursors.Hand };

                EventHandler clickHandler = (s, e) => LoadProductIntoCurrentItemFields(p.Barcode, p.ProductName, p.SalePrice, p.Quantity, p.IsSerialized);
                card.Click += clickHandler;
                thumb.Click += clickHandler;
                lblName.Click += clickHandler;
                lblPrice.Click += clickHandler;

                card.Controls.AddRange(new Control[] { thumb, lblName, lblPrice });
                pnlPopularProducts.Controls.Add(card);
                x += 178;
            }
        }

        // ---------- أزرار ➖/➕/🗑 داخل جدول السلة ----------
        private void DgvSaleCart_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            string colName = dgvSaleCart.Columns[e.ColumnIndex].Name;
            if (colName == "colDecrease") DecrementCartQty(e.RowIndex);
            else if (colName == "colIncrease") IncrementCartQty(e.RowIndex);
            else if (colName == "colDelete") RemoveCartRow(e.RowIndex);
        }

        private void IncrementCartQty(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= currentSaleItems.Count) return;
            var item = currentSaleItems[rowIndex];
            if (!string.IsNullOrEmpty(item.Imei)) return; // جهاز بسيريال - الكمية دايمًا 1
            item.Quantity += 1;
            item.Total = item.UnitPrice * item.Quantity;
            RefreshSaleCartGrid();
        }

        private void DecrementCartQty(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= currentSaleItems.Count) return;
            var item = currentSaleItems[rowIndex];
            if (!string.IsNullOrEmpty(item.Imei)) return;
            if (item.Quantity <= 1) return; // لو عايز يشيله يستخدم زرار الحذف
            item.Quantity -= 1;
            item.Total = item.UnitPrice * item.Quantity;
            RefreshSaleCartGrid();
        }

        private void RemoveCartRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= currentSaleItems.Count) return;
            currentSaleItems.RemoveAt(rowIndex);
            RefreshSaleCartGrid();
        }

        private void BtnClearCart_Click(object sender, EventArgs e)
        {
            if (currentSaleItems.Count == 0) return;
            if (MessageBox.Show("هل تريد إفراغ السلة بالكامل؟", "تأكيد", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            currentSaleItems.Clear();
            txtQuickDiscountPercent.Text = "0";
            txtQuickTaxPercent.Text = "0";
            txtAmountPaid.Text = "";
            RefreshSaleCartGrid();
        }

        // ==========================================================================
        // نفس تنسيق الجداول المستخدم في كل شاشات Form1.cs (StyleDataGridView)
        // ==========================================================================
        private void StyleDataGridView(DataGridView dgv) => UIHelpers.StyleDataGridView(dgv);

        // ==========================================================================
        // بيانات المتجر (اسم/تليفون/عنوان/شعار) - محتاجينها وقت طباعة الفاتورة
        // ==========================================================================
        private void LoadStoreSettingsIntoMemory()
        {
            UIHelpers.LoadStoreSettings(out CurrentStoreName, out CurrentStorePhone, out CurrentStoreAddress, out CurrentStoreLogo, out CurrentStoreWhatsApp);
        }

        // ==========================================================================
        // تحميل قائمة العملاء (لازمة لاختيار عميل في حالة البيع بالآجل)
        // ==========================================================================
        private void LoadCustomersIntoCombo()
        {
            if (cmbSaleCustomer == null) return;
            try
            {
                DataTable dt = SalesRepository.GetCustomers();
                cmbSaleCustomer.DataSource = dt;
                cmbSaleCustomer.DisplayMember = "CustomerName";
                cmbSaleCustomer.ValueMember = "CustomerId";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        // ==========================================================================
        // تحميل جدول المبيعات
        // ==========================================================================
        private void LoadSalesData()
        {
            try
            {
                dgvSales.DataSource = SalesRepository.GetSales();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        // ==========================================================================
        // فحص هل تاريخ معين تم إقفاله بالفعل (منع تعديل/إلغاء أيام مقفولة)
        // ==========================================================================
        private bool IsDateClosed(DateTime date) => UIHelpers.IsDateClosed(date);

        private bool IsTodayClosed() => UIHelpers.IsTodayClosed();

        // ==========================================================================
        // قراءة الباركود (Enter) - بيجيب بيانات المنتج ويجهزه للبيع
        // ==========================================================================
        private void TxtSaleBarcode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !string.IsNullOrEmpty(txtSaleBarcode.Text))
            {
                try
                {
                    string barcode = txtSaleBarcode.Text;
                    ProductForSale product = SalesRepository.GetProductForSale(barcode);
                    if (product == null)
                    {
                        MessageBox.Show("هذا الباركود غير مسجل في المخزن!", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        txtSaleBarcode.Clear();
                        txtSaleBarcode.Focus();
                        return;
                    }

                    LoadProductIntoCurrentItemFields(barcode, product.ProductName, product.SalePrice, product.Quantity, product.IsSerialized);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        // ==========================================================================
        // بيملأ حقول "الصنف الحالي" (باركود/اسم/سعر/كمية/IMEI) من بيانات منتج معين - بيستخدمها
        // كل مصادر اختيار المنتج (مسح باركود / بحث بالاسم / بحث بالسيريال / كارت "الأكثر مبيعًا")
        // عشان الكل يشتغل بنفس السلوك بالظبط من غير تكرار منطق. preselectedImei اختياري
        // (تاب "سيريال" بيحدد جهاز بعينه بدل ما يسيب المستخدم يختار من القائمة).
        // ==========================================================================
        private void LoadProductIntoCurrentItemFields(string barcode, string productName, decimal salePrice, int quantity, bool isSerialized, string preselectedImei = null)
        {
            if (quantity <= 0)
            {
                MessageBox.Show("عذراً، هذا المنتج نفذ من المخزن تماماً!", "نفذت الكمية", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            txtSaleBarcode.Text = barcode;
            txtSaleName.Text = productName;
            txtCustomerPrice.Text = salePrice.ToString();
            txtItemDiscount.Text = "0";
            CalculateTotal();

            lblSaleImei.Visible = isSerialized;
            cmbSaleImei.Visible = isSerialized;

            if (isSerialized)
            {
                txtSaleQty.Text = "1";
                txtSaleQty.ReadOnly = true;
                LoadAvailableImeisForSale(barcode.Trim());
                if (!string.IsNullOrEmpty(preselectedImei))
                    cmbSaleImei.SelectedValue = preselectedImei;
                cmbSaleImei.Focus();
            }
            else
            {
                txtSaleQty.ReadOnly = false;
                txtSaleQty.Focus();
            }
        }

        private void TxtSaleQty_TextChanged(object sender, EventArgs e) { CalculateTotal(); }

        private void CalculateTotal()
        {
            if (decimal.TryParse(txtCustomerPrice.Text, out decimal price) && int.TryParse(txtSaleQty.Text, out int qty))
            {
                decimal.TryParse(txtItemDiscount.Text, out decimal discount);
                decimal gross = price * qty;
                txtSaleTotal.Text = (gross - discount).ToString();
            }
        }

        private void CmbSalePaymentType_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool isCredit = cmbSalePaymentType.SelectedItem?.ToString() == "آجل";
            cmbSaleCustomer.Enabled = isCredit;
            cmbSalePaymentMethod.Enabled = !isCredit;
            if (txtAmountPaid != null)
            {
                txtAmountPaid.Enabled = !isCredit;
                if (isCredit) txtAmountPaid.Text = "";
            }
            if (quickPaymentButtons != null)
            {
                foreach (var btn in quickPaymentButtons) btn.Enabled = !isCredit;
                if (isCredit) HighlightSelectedQuickPaymentButton(null);
            }
            UpdateTotalsSummary();
        }

        // ==========================================================================
        // تحميل الأجهزة (IMEI) المتاحة لمنتج معين، لو المنتج ده بيتباع بالسيريال
        // ==========================================================================
        private void LoadAvailableImeisForSale(string barcode)
        {
            DataTable dt;
            try
            {
                dt = SalesRepository.GetAvailableImeisForSale(barcode);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

            cmbSaleImei.DataSource = dt;
            cmbSaleImei.DisplayMember = "IMEI";
            cmbSaleImei.ValueMember = "IMEI";

            if (dt.Rows.Count == 0)
                MessageBox.Show("مفيش أي جهاز متاح لهذا الموديل في المخزون بالـIMEI. راجع فواتير الشراء.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ==========================================================================
        // إضافة الصنف المعروض حاليًا في الخانات للسلة (من غير ما يتسجل في قاعدة البيانات لسه)
        // ==========================================================================
        private void BtnAddItemToCart_Click(object sender, EventArgs e)
        {
            if (IsTodayClosed())
            {
                MessageBox.Show("تم إقفال اليوم بالفعل، لا يمكن تسجيل مبيعات جديدة.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(txtSaleName.Text) || !int.TryParse(txtSaleQty.Text, out int qtySold) || qtySold <= 0)
            {
                MessageBox.Show("من فضلك امسح باركود صنف صحيح وحدد كمية أكبر من صفر.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!decimal.TryParse(txtCustomerPrice.Text, out decimal unitPrice))
            {
                MessageBox.Show("من فضلك أدخل سعر بيع صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal.TryParse(txtItemDiscount.Text, out decimal itemDiscount);
            decimal lineGross = unitPrice * qtySold;
            if (itemDiscount < 0 || itemDiscount > lineGross)
            {
                MessageBox.Show("قيمة خصم الصنف لازم تكون بين صفر وإجمالي الصنف.", "بيانات غير صحيحة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string selectedImei = null;
            if (lblSaleImei.Visible)
            {
                if (cmbSaleImei.SelectedValue == null)
                {
                    MessageBox.Show("من فضلك اختار الجهاز (IMEI) اللي هيتباع.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                selectedImei = cmbSaleImei.SelectedValue.ToString();
                if (currentSaleItems.Exists(x => x.Imei == selectedImei))
                {
                    MessageBox.Show("الجهاز ده اتضاف للسلة بالفعل.", "مكرر", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            currentSaleItems.Add(new SaleLine
            {
                Barcode = txtSaleBarcode.Text.Trim(),
                ProductName = txtSaleName.Text,
                UnitPrice = unitPrice,
                Quantity = qtySold,
                Total = lineGross,
                Discount = itemDiscount,
                Imei = selectedImei
            });

            RefreshSaleCartGrid();
            ClearCurrentItemInputs();
        }

        private void RefreshSaleCartGrid()
        {
            dgvSaleCart.Rows.Clear();

            foreach (var item in currentSaleItems)
            {
                string code = !string.IsNullOrEmpty(item.Imei) ? item.Imei : item.Barcode;
                string stepper = string.IsNullOrEmpty(item.Imei) ? "➖" : "";
                string stepperUp = string.IsNullOrEmpty(item.Imei) ? "➕" : "";
                decimal lineNet = item.Total - item.Discount;
                dgvSaleCart.Rows.Add(item.ProductName, code, stepper, item.Quantity, stepperUp, item.UnitPrice.ToString("N2"), item.Discount.ToString("N2"), lineNet.ToString("N2"), "🗑");
            }

            var totals = CalculateCartTotals();
            lblSaleCartTotal.Text = $"عدد الأصناف: {currentSaleItems.Count}    |    إجمالي السلة: {totals.Net:N2} ج.م";
            UpdateTotalsSummary();
        }

        private void DgvSaleCart_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= currentSaleItems.Count) return;
            currentSaleItems.RemoveAt(e.RowIndex);
            RefreshSaleCartGrid();
        }

        // بيمسح خانات "الصنف الحالي" بس (الباركود/الاسم/السعر/الكمية/الـIMEI) عشان
        // المستخدم يمسح الصنف اللي بعده، من غير ما يلمس السلة أو بيانات العميل/الدفع
        private void ClearCurrentItemInputs()
        {
            txtSaleBarcode.Clear(); txtSaleName.Clear(); txtCustomerPrice.Clear(); txtSaleQty.Text = "1"; txtSaleTotal.Clear(); txtItemDiscount.Text = "0";
            lblSaleImei.Visible = false;
            cmbSaleImei.Visible = false;
            txtSaleQty.ReadOnly = false;
            txtSaleBarcode.Focus();
        }

        // ==========================================================================
        // تنفيذ عملية البيع - بتسجّل كل أصناف السلة مرة واحدة تحت رقم فاتورة واحد مشترك
        // ==========================================================================
        private void BtnAddToBill_Click(object sender, EventArgs e)
        {
            if (IsTodayClosed())
            {
                MessageBox.Show("تم إقفال اليوم بالفعل، لا يمكن تسجيل مبيعات جديدة.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (currentSaleItems.Count == 0)
            {
                MessageBox.Show("من فضلك ضيف صنف واحد على الأقل للسلة أولاً.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string paymentType = cmbSalePaymentType.SelectedItem?.ToString() == "آجل" ? "Credit" : "Cash";
            int? customerId = null;
            if (paymentType == "Credit")
            {
                if (cmbSaleCustomer.SelectedValue == null)
                {
                    MessageBox.Show("البيع بالآجل لازم يكون له عميل محدد. اختار العميل أو ضيفه الأول من تاب العملاء.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                customerId = Convert.ToInt32(cmbSaleCustomer.SelectedValue);
            }

            string paymentMethod = null;
            if (paymentType == "Cash")
            {
                if (cmbSalePaymentMethod.SelectedItem == null)
                {
                    MessageBox.Show("من فضلك اختار وسيلة الدفع اللي هيدفع بيها العميل.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                paymentMethod = cmbSalePaymentMethod.SelectedItem.ToString();
            }

            // خصم سريع على مستوى الفاتورة (لو موجود) بيتوزّع نسبيًا على كل الأصناف وقت التأكيد،
            // فوق أي خصم صنف موجود بالفعل من غير ما يستبدله
            decimal.TryParse(txtQuickDiscountPercent.Text, out decimal quickPercent);
            quickPercent = Math.Max(0, Math.Min(100, quickPercent));
            if (quickPercent > 0)
            {
                foreach (var item in currentSaleItems)
                {
                    decimal afterItemDiscount = item.Total - item.Discount;
                    item.Discount += afterItemDiscount * quickPercent / 100m;
                }
            }

            // ضريبة سريعة على مستوى الفاتورة (لو موجودة) بتتوزّع نسبيًا على كل الأصناف
            // بعد الخصم بالظبط - نفس أسلوب الخصم السريع فوق
            decimal.TryParse(txtQuickTaxPercent.Text, out decimal quickTaxPercent);
            quickTaxPercent = Math.Max(0, quickTaxPercent);
            if (quickTaxPercent > 0)
            {
                foreach (var item in currentSaleItems)
                {
                    decimal lineNet = item.Total - item.Discount;
                    item.Tax = lineNet * quickTaxPercent / 100m;
                }
            }

            // "المبلغ المدفوع" اختياري وبيتفعّل بس مع الكاش - لو فاضي، السيرفر بيعتبر العميل
            // دافع المبلغ المطلوب بالظبط (فكة = صفر)
            decimal? amountPaid = null;
            if (paymentType == "Cash" && decimal.TryParse(txtAmountPaid.Text, out decimal enteredAmountPaid) && enteredAmountPaid > 0)
                amountPaid = enteredAmountPaid;

            int dailyInvoiceNumber;
            decimal changeDue;
            try
            {
                var result = AppServices.CoreEngine.Execute(new CreateSaleCommand
                {
                    Lines = new List<SaleLine>(currentSaleItems),
                    CustomerId = customerId,
                    PaymentType = paymentType,
                    PaymentMethod = paymentMethod,
                    PerformedBy = AuthManager.CurrentUsername,
                    AmountPaid = amountPaid
                });
                dailyInvoiceNumber = result.DailyInvoiceNumber;
                changeDue = result.ChangeDue;
            }
            catch (InsufficientStockException ex)
            {
                MessageBox.Show(ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (ValidationException ex)
            {
                MessageBox.Show(string.Join(Environment.NewLine, ex.Errors), "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show("حدث خطأ: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            txtInvoiceNumber.Text = $"🧾 فاتورة #{dailyInvoiceNumber}";
            txtInvoiceCustomer.Text = paymentType == "Credit" ? cmbSaleCustomer.Text : "عميل نقدي";

            string successMessage = $"تمت عملية البيع بنجاح!\nرقم الفاتورة اليوم: {dailyInvoiceNumber}";
            if (changeDue > 0)
                successMessage += $"\nالباقي (الفكة) للعميل: {changeDue:N2} ج.م";
            MessageBox.Show(successMessage, "نجاح", MessageBoxButtons.OK, MessageBoxIcon.Information);
            currentSaleItems.Clear();
            txtQuickDiscountPercent.Text = "0";
            txtQuickTaxPercent.Text = "0";
            txtAmountPaid.Text = "";
            RefreshSaleCartGrid();
            LoadSalesData();
            ClearPOSInputs();
        }

        // ==========================================================================
        // التعديل / الإلغاء
        // ==========================================================================
        // بيركّز على خانة الباركود فاضية وجاهزة للمسح - مستخدمة من كارت "فاتورة جديدة" السريع في الداشبورد
        public void FocusBarcodeEntry()
        {
            txtSaleBarcode.Clear();
            txtSaleBarcode.ReadOnly = false; // احتياطًا لو كانت آخر حالة للنافذة كانت "عرض فاتورة" (HighlightSale بيخليها ReadOnly)
            txtSaleBarcode.Focus();
        }

        // بيدوّر على عملية بيع برقمها في سجل المبيعات، يحددها ويجيب بياناتها في الخانات
        // (مستخدمة من نتيجة البحث الشامل Ctrl+F)
        public void HighlightSale(string saleIdText)
        {
            if (!int.TryParse(saleIdText, out int saleId)) return;
            foreach (DataGridViewRow row in dgvSales.Rows)
            {
                if (row.Cells["رقم البيع"].Value != null && Convert.ToInt32(row.Cells["رقم البيع"].Value) == saleId)
                {
                    dgvSales.ClearSelection();
                    row.Selected = true;
                    dgvSales.CurrentCell = row.Cells[0];
                    dgvSales.FirstDisplayedScrollingRowIndex = row.Index;
                    DgvSales_CellClick(dgvSales, new DataGridViewCellEventArgs(0, row.Index));
                    return;
                }
            }
        }

        private void DgvSales_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            int saleId = Convert.ToInt32(dgvSales.Rows[e.RowIndex].Cells["رقم البيع"].Value);
            LoadSaleIntoFields(saleId);
        }

        private void LoadSaleIntoFields(int saleId)
        {
            try
            {
                SaleRecord sale = SalesRepository.GetSaleById(saleId);
                if (sale == null) return;

                selectedSaleId = saleId;
                SwitchMainView(0); // يفضل عرض "فاتورة جديدة" ظاهر أثناء التعديل (فيه الحقول المشتركة)
                SwitchProductTab(0); // يفضل الباركود ظاهر أثناء التعديل حتى لو كان تاب تاني نشط
                txtSaleBarcode.Text = sale.Barcode;
                txtSaleName.Text = sale.ProductName;
                txtCustomerPrice.Text = sale.Price.ToString();
                txtSaleQty.Text = sale.QuantitySold.ToString();
                txtSaleBarcode.ReadOnly = true;
                if (btnSaveSaleEdit != null) btnSaveSaleEdit.Enabled = false;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnEditSaleMode_Click(object sender, EventArgs e)
        {
            if (selectedSaleId == -1)
            {
                MessageBox.Show("من فضلك اختر عملية بيع من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            btnSaveSaleEdit.Enabled = true;
        }

        private void BtnSaveSaleEdit_Click(object sender, EventArgs e)
        {
            if (selectedSaleId == -1)
            {
                MessageBox.Show("من فضلك اختر عملية بيع من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!int.TryParse(txtSaleQty.Text, out int newQty) || newQty <= 0)
            {
                MessageBox.Show("من فضلك أدخل كمية صحيحة.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaleRecord existing = SalesRepository.GetSaleById(selectedSaleId);
            if (existing == null)
            {
                MessageBox.Show("لم يتم العثور على عملية البيع.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (IsDateClosed(DateTime.Parse(existing.SaleDate).Date))
            {
                MessageBox.Show("لا يمكن تعديل عملية بيع تابعة ليوم تم إقفاله بالفعل.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                AppServices.CoreEngine.Execute(new UpdateSaleQuantityCommand { SaleId = selectedSaleId, NewQuantity = newQty, PerformedBy = AuthManager.CurrentUsername });
            }
            catch (InsufficientStockException ex)
            {
                MessageBox.Show(ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (DateClosedException ex)
            {
                MessageBox.Show(ex.Message, "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            catch (InsufficientBalanceException ex)
            {
                MessageBox.Show(ex.Message, "رصيد غير كافٍ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show("حصل خطأ أثناء تعديل عملية البيع: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show("تم تعديل عملية البيع بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadSalesData();
            ClearPOSInputs();
        }

        private void BtnCancelSale_Click(object sender, EventArgs e)
        {
            if (selectedSaleId == -1)
            {
                MessageBox.Show("من فضلك اختر عملية بيع من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaleRecord existing = SalesRepository.GetSaleById(selectedSaleId);
            if (existing == null)
            {
                MessageBox.Show("لم يتم العثور على عملية البيع.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (IsDateClosed(DateTime.Parse(existing.SaleDate).Date))
            {
                MessageBox.Show("لا يمكن إلغاء عملية بيع تابعة ليوم تم إقفاله بالفعل.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("هل أنت متأكد من إلغاء عملية البيع دي؟ هيترجع للمخزون تاني.", "تأكيد الإلغاء", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                AppServices.CoreEngine.Execute(new CancelSaleCommand { SaleId = selectedSaleId, PerformedBy = AuthManager.CurrentUsername });
            }
            catch (DateClosedException ex)
            {
                MessageBox.Show(ex.Message, "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            catch (InsufficientBalanceException ex)
            {
                MessageBox.Show(ex.Message, "رصيد غير كافٍ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show("حصل خطأ أثناء إلغاء عملية البيع: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show("تم إلغاء عملية البيع بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadSalesData();
            ClearPOSInputs();
        }

        private void ClearPOSInputs()
        {
            txtSaleBarcode.Clear(); txtSaleName.Clear(); txtCustomerPrice.Clear(); txtSaleQty.Text = "1"; txtSaleTotal.Clear(); txtItemDiscount.Text = "0";
            txtSaleBarcode.ReadOnly = false;
            txtSaleQty.ReadOnly = false;
            if (lblSaleImei != null) lblSaleImei.Visible = false;
            if (cmbSaleImei != null) cmbSaleImei.Visible = false;
            selectedSaleId = -1;
            if (btnSaveSaleEdit != null) btnSaveSaleEdit.Enabled = false;
            txtSaleBarcode.Focus();
        }

        // ==========================================================================
        // الطباعة
        // ==========================================================================
        private PaperSize GetSelectedInvoicePaperSize()
        {
            int selected = cmbInvoicePaperSize?.SelectedIndex ?? 0;
            switch (selected)
            {
                case 1: // 58 مم
                    return new PaperSize("Thermal58", 228, 1100);
                case 2: // A4
                    return new PaperSize("A4", 827, 1169);
                default: // 80 مم (الافتراضي)
                    return new PaperSize("Thermal80", 315, 1100);
            }
        }

        // ==========================================================================
        // إرسال آخر فاتورة بيع للعميل عبر واتساب
        // ==========================================================================
        private void BtnSendInvoiceWhatsApp_Click(object sender, EventArgs e)
        {
            var lastSale = SalesRepository.GetLastSaleWithCustomer();
            if (lastSale.ProductName == null)
            {
                MessageBox.Show("لا توجد عمليات بيع مسجلة بعد.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal total = lastSale.Total;
            string saleDate = lastSale.SaleDate;
            string customerPhone = lastSale.CustomerPhone;
            string customerName = lastSale.CustomerName;
            int dailyInvoiceNumber = lastSale.DailyInvoiceNumber;

            string greetingName = customerName ?? "عميلنا العزيز";
            string displayDate = DateTime.TryParse(saleDate, out DateTime parsedDate) ? parsedDate.ToString("yyyy-MM-dd") : saleDate;
            string separatorLine = "------------------------";

            string message = $"مرحبًا أستاذ/ة {greetingName}\n" +
                              $"نشكرك على ثقتك في Temo Mobile Store\n" +
                              $"تم إصدار فاتورتك بنجاح.\n" +
                              $"{separatorLine}\n" +
                              $"رقم الفاتورة: {dailyInvoiceNumber}\n" +
                              $"التاريخ: {displayDate}\n" +
                              $"الإجمالي: {total:N2} جنيه\n" +
                              $"{separatorLine}\n" +
                              $"إذا كان لديك أي استفسار أو احتجت إلى دعم أو خدمة ما بعد البيع، يسعدنا التواصل معك في أي وقت.\n\n" +
                              $"شكرًا لاختيارك Temo Mobile Store، ونتطلع لخدمتك مرة أخرى.";

            WhatsAppHelper.SendMessage(customerPhone, message, this.FindForm());
        }

        // ==========================================================================
        // حفظ آخر فاتورة كملف PDF مباشرة (باستخدام Microsoft Print to PDF المدمجة في ويندوز)
        // ==========================================================================
        private void BtnSaveInvoicePdf_Click(object sender, EventArgs e)
        {
            bool hasPdfPrinter = false;
            foreach (string printerName in PrinterSettings.InstalledPrinters)
            {
                if (printerName.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasPdfPrinter = true;
                    break;
                }
            }

            if (!hasPdfPrinter)
            {
                MessageBox.Show("مش لاقي طابعة \"Microsoft Print to PDF\" على الجهاز ده. تأكد إنها مفعّلة من إعدادات الطابعات في ويندوز.", "غير متاح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<ReceiptData> items = LoadReceiptForPrinting();
            if (items == null) return;

            try
            {
                string folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TemoStore_Invoices");
                if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);

                string fileName = $"فاتورة_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.pdf";
                string filePath = System.IO.Path.Combine(folder, fileName);

                PrintDocument pd = new PrintDocument();
                pd.PrintPage += (s, ev) => RenderReceiptPage(ev, items);
                pd.DefaultPageSettings.PaperSize = BuildReceiptPaperSize(items);
                pd.PrinterSettings.PrinterName = "Microsoft Print to PDF";
                pd.PrinterSettings.PrintToFile = true;
                pd.PrinterSettings.PrintFileName = filePath;

                pd.Print();

                var result = MessageBox.Show($"تم حفظ الفاتورة بنجاح في:\n{filePath}\n\nتحب تفتح الملف دلوقتي؟", "تم الحفظ", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (result == DialogResult.Yes)
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("حصل خطأ أثناء حفظ الـ PDF: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnPrintInvoice_Click(object sender, EventArgs e)
        {
            List<ReceiptData> items = LoadReceiptForPrinting();
            if (items == null) return;

            PrintDocument pd = new PrintDocument();
            pd.PrintPage += (s, ev) => RenderReceiptPage(ev, items);
            pd.DefaultPageSettings.PaperSize = BuildReceiptPaperSize(items);
            PrintPreviewDialog pdd = new PrintPreviewDialog() { Document = pd };
            pdd.ShowDialog();
        }

        // بيجيب بيانات آخر عملية بيع كاملة (مرة واحدة) عشان نفس البيانات المستخدمة في حساب
        // ارتفاع الصفحة الديناميكي هي بالظبط اللي هتترسم - من غير خطر إن بيع جديد يتسجل
        // في نفس اللحظة بين حساب الارتفاع والطباعة الفعلية
        private List<ReceiptData> LoadReceiptForPrinting()
        {
            List<ReceiptData> items;
            try { items = SalesRepository.GetLastInvoiceForReceipt(); }
            catch (Exception ex)
            {
                MessageBox.Show("حدث خطأ أثناء تجهيز الفاتورة: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            if (items == null || items.Count == 0)
            {
                MessageBox.Show("لا توجد عمليات بيع مسجلة بعد.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            return items;
        }

        // بيبني مقاس الورقة، وللطابعات الحرارية (80/58مم) بيحسب الارتفاع المطلوب فعليًا حسب
        // محتوى الفاتورة (مفيش IMEI؟ عميل نقدي؟ فمفيش أسطر فاضية) بدل ارتفاع ثابت ضخم بيسيب
        // مسافة بيضا في آخر كل إيصال
        private PaperSize BuildReceiptPaperSize(List<ReceiptData> items)
        {
            PaperSize paperSize = GetSelectedInvoicePaperSize();
            if (paperSize.Width < 500)
            {
                using (var tempBmp = new Bitmap(1, 1))
                using (var measureG = Graphics.FromImage(tempBmp))
                {
                    float contentHeight = RenderReceipt(measureG, paperSize.Width, true, items);
                    paperSize.Height = (int)Math.Ceiling(contentHeight) + 20;
                }
            }
            return paperSize;
        }

        private void RenderReceiptPage(PrintPageEventArgs e, List<ReceiptData> items)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            RenderReceipt(g, e.PageBounds.Width, false, items);
        }

        // ==========================================================================
        // رسم/قياس الإيصال - نفس الدالة تستخدم مرتين: مرة "قياس بس" (measureOnly=true،
        // بترجع الارتفاع المطلوب من غير ما ترسم حاجة فعليًا) قبل الطباعة عشان نظبط مقاس
        // الورقة، ومرة تانية "رسم فعلي" وقت الطباعة الحقيقية - بنفس المنطق بالظبط، فمفيش
        // احتمال إن القياس يختلف عن الرسم الفعلي. items ممكن تحتوي أكتر من صنف لو الفاتورة
        // فيها أكتر من منتج - بيانات الفاتورة المشتركة (رقمها/تاريخها/العميل) بتتاخد من أول عنصر.
        // ==========================================================================
        private float RenderReceipt(Graphics g, float pageWidth, bool measureOnly, List<ReceiptData> items)
        {
            ReceiptData receipt = items[0];
            bool isThermal = pageWidth < 500;
            float margin = isThermal ? 14 : 24;
            float contentWidth = pageWidth - margin * 2;

            Color colorPrimary = UIHelpers.ColorPrimary;
            Color colorAccent = UIHelpers.ColorSuccess;
            Color colorWarning = UIHelpers.ColorWarning;
            Color colorMuted = Color.FromArgb(120, 126, 138);
            Color colorLightBg = Color.FromArgb(238, 242, 248);

            using var fontBrand = new Font("Arial", isThermal ? 15 : 20, FontStyle.Bold);
            using var fontTagline = new Font("Arial", isThermal ? 8 : 10, FontStyle.Regular);
            using var fontLabelBold = new Font("Arial", isThermal ? 10 : 12, FontStyle.Bold);
            using var fontBody = new Font("Arial", isThermal ? 9 : 11, FontStyle.Regular);
            using var fontBodyBold = new Font("Arial", isThermal ? 9 : 11, FontStyle.Bold);
            using var fontSmall = new Font("Arial", isThermal ? 7.5f : 9, FontStyle.Regular);
            using var fontItemName = new Font("Arial", isThermal ? 10.5f : 13, FontStyle.Bold);
            using var fontTotalValue = new Font("Arial", isThermal ? 15 : 19, FontStyle.Bold);
            using var fontSectionHeader = new Font("Arial", isThermal ? 9.5f : 11.5f, FontStyle.Bold);

            var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            var farAlign = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
            var nearAlign = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

            float yPos = 0;

            void FillRect(RectangleF rect, Color color) { if (!measureOnly) using (Brush b = new SolidBrush(color)) g.FillRectangle(b, rect); }
            void FillRounded(RectangleF rect, Color color, float radius) { if (!measureOnly) using (Brush b = new SolidBrush(color)) using (var path = RoundedRect(rect, radius)) g.FillPath(b, path); }
            void BorderRounded(RectangleF rect, Color color, float radius, float width) { if (!measureOnly) using (Pen p = new Pen(color, width)) using (var path = RoundedRect(rect, radius)) g.DrawPath(p, path); }
            void Text(string s, Font f, Color color, RectangleF rect, StringFormat fmt) { if (!measureOnly) using (Brush b = new SolidBrush(color)) g.DrawString(s ?? "", f, b, rect, fmt); }
            void Dashed(float y) { if (!measureOnly) DrawDashedLine(g, margin, pageWidth - margin, y); }

            // 1) شريط علوي (لمسة براند)
            FillRect(new RectangleF(0, yPos, pageWidth, isThermal ? 4 : 6), colorPrimary);
            yPos += (isThermal ? 4 : 6) + (isThermal ? 10 : 14);

            // 2) الشعار (من إعدادات المحل - نفس الشعار المستخدم في باقي البرنامج)
            if (CurrentStoreLogo != null && CurrentStoreLogo.Length > 0)
            {
                float logoSize = isThermal ? 48 : 64;
                if (!measureOnly)
                {
                    using (var ms = new System.IO.MemoryStream(CurrentStoreLogo))
                    using (var logoImg = Image.FromStream(ms))
                        g.DrawImage(logoImg, (pageWidth - logoSize) / 2, yPos, logoSize, logoSize);
                }
                yPos += logoSize + (isThermal ? 8 : 12);
            }

            // 3) اسم المحل
            string brandName = string.IsNullOrWhiteSpace(CurrentStoreName) ? "Temo Mobile Store" : CurrentStoreName;
            Text(brandName, fontBrand, colorPrimary, new RectangleF(0, yPos, pageWidth, isThermal ? 24 : 30), center);
            yPos += isThermal ? 24 : 30;

            Text("هواتف وإكسسوارات موبايل", fontTagline, colorMuted, new RectangleF(0, yPos, pageWidth, isThermal ? 16 : 20), center);
            yPos += isThermal ? 18 : 22;

            // 4) العنوان
            if (!string.IsNullOrWhiteSpace(CurrentStoreAddress))
            {
                Text(CurrentStoreAddress, fontSmall, colorMuted, new RectangleF(margin, yPos, contentWidth, isThermal ? 14 : 18), center);
                yPos += isThermal ? 15 : 19;
            }

            // 5) تليفون / واتساب (سطر واحد لو الاتنين موجودين)
            string phoneLine = "";
            if (!string.IsNullOrWhiteSpace(CurrentStorePhone)) phoneLine += $"تليفون: {CurrentStorePhone}";
            if (!string.IsNullOrWhiteSpace(CurrentStoreWhatsApp))
                phoneLine += (phoneLine.Length > 0 ? "   |   " : "") + $"واتساب: {CurrentStoreWhatsApp}";
            if (phoneLine.Length > 0)
            {
                Text(phoneLine, fontSmall, colorMuted, new RectangleF(margin, yPos, contentWidth, isThermal ? 14 : 18), center);
                yPos += isThermal ? 16 : 20;
            }

            yPos += isThermal ? 6 : 8;
            Dashed(yPos);
            yPos += isThermal ? 12 : 16;

            // 6) بيانات الفاتورة
            string invoiceCode = $"INV-{receipt.SaleId:D6}";
            Text($"رقم الفاتورة:  #{receipt.DailyInvoiceNumber}", fontBodyBold, colorPrimary, new RectangleF(margin, yPos, contentWidth, isThermal ? 16 : 20), nearAlign);
            yPos += isThermal ? 18 : 22;

            string dateDisplay = DateTime.TryParse(receipt.SaleDate, out DateTime saleDt) ? saleDt.ToString("yyyy-MM-dd  hh:mm tt") : receipt.SaleDate;

            void InfoLine(string label, string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                Text($"{label}:  {value}", fontBody, Color.Black, new RectangleF(margin, yPos, contentWidth, isThermal ? 16 : 20), nearAlign);
                yPos += isThermal ? 18 : 22;
            }

            InfoLine("التاريخ والوقت", dateDisplay);
            InfoLine("الكاشير", receipt.CashierName);
            InfoLine("العميل", receipt.PaymentType == "Credit" ? receipt.CustomerName : "عميل نقدي");
            InfoLine("هاتف العميل", receipt.PaymentType == "Credit" ? receipt.CustomerPhone : null);

            yPos += isThermal ? 4 : 6;
            Dashed(yPos);
            yPos += isThermal ? 12 : 16;

            // 7) عنوان قسم الأصناف
            float sectionBarH = isThermal ? 22 : 28;
            FillRect(new RectangleF(margin, yPos, contentWidth, sectionBarH), colorPrimary);
            Text("تفاصيل الصنف", fontSectionHeader, Color.White, new RectangleF(margin, yPos, contentWidth, sectionBarH), center);
            yPos += sectionBarH + (isThermal ? 10 : 14);

            // 8) كارت الصنف - بيتكرر لكل صنف في الفاتورة
            foreach (var item in items)
            {
                float iconSize = isThermal ? 34 : 44;
                if (!measureOnly) DrawDeviceIcon(g, new RectangleF(margin, yPos, iconSize, iconSize), colorPrimary);

                float textX = margin + iconSize + (isThermal ? 8 : 12);
                float textW = contentWidth - iconSize - (isThermal ? 8 : 12);
                float itemTop = yPos;

                Text(item.ProductName, fontItemName, Color.Black, new RectangleF(textX, yPos, textW, isThermal ? 16 : 20), nearAlign);
                yPos += isThermal ? 17 : 21;

                if (!string.IsNullOrWhiteSpace(item.Barcode))
                {
                    Text($"باركود: {item.Barcode}", fontSmall, colorMuted, new RectangleF(textX, yPos, textW, isThermal ? 13 : 16), nearAlign);
                    yPos += isThermal ? 14 : 17;
                }
                if (!string.IsNullOrWhiteSpace(item.IMEI))
                {
                    Text($"IMEI/سيريال: {item.IMEI}", fontSmall, colorMuted, new RectangleF(textX, yPos, textW, isThermal ? 13 : 16), nearAlign);
                    yPos += isThermal ? 14 : 17;
                }

                Text($"{item.Quantity} × {item.UnitPrice:N2}  =  {item.Total:N2} ج.م", fontBodyBold, colorPrimary, new RectangleF(textX, yPos, textW, isThermal ? 16 : 20), nearAlign);
                yPos += isThermal ? 18 : 22;

                yPos = Math.Max(yPos, itemTop + iconSize + (isThermal ? 6 : 8));
                yPos += isThermal ? 8 : 10;
            }

            yPos -= 4; // تعويض هامش آخر عنصر زيادة
            Dashed(yPos);
            yPos += isThermal ? 12 : 16;

            // 9) الإجماليات
            decimal subtotal = 0m;
            decimal discount = 0m;
            foreach (var item in items) { subtotal += item.Total; discount += item.Discount; }
            decimal grandTotal = subtotal - discount;

            void TotalLine(string label, decimal amount)
            {
                Text(label, fontBody, Color.Black, new RectangleF(margin, yPos, contentWidth / 2, isThermal ? 16 : 20), nearAlign);
                Text($"{amount:N2} ج.م", fontBody, Color.Black, new RectangleF(margin + contentWidth / 2, yPos, contentWidth / 2, isThermal ? 16 : 20), farAlign);
                yPos += isThermal ? 17 : 21;
            }
            TotalLine("الإجمالي الفرعي", subtotal);
            TotalLine("الخصم", discount);

            yPos += isThermal ? 2 : 4;
            if (!measureOnly) using (Pen p = new Pen(colorPrimary, 1.2f)) g.DrawLine(p, margin, yPos, pageWidth - margin, yPos);
            yPos += isThermal ? 8 : 10;

            float totalBoxH = isThermal ? 34 : 42;
            FillRounded(new RectangleF(margin, yPos, contentWidth, totalBoxH), Color.FromArgb(236, 247, 240), isThermal ? 8 : 10);
            Text("الإجمالي النهائي", fontBodyBold, colorAccent, new RectangleF(margin + 12, yPos, contentWidth / 2, totalBoxH), nearAlign);
            Text($"{grandTotal:N2} ج.م", fontTotalValue, colorAccent, new RectangleF(margin, yPos, contentWidth - 12, totalBoxH), farAlign);
            yPos += totalBoxH + (isThermal ? 12 : 16);

            // 10) صندوق وسيلة الدفع / المدفوع
            bool isCredit = receipt.PaymentType == "Credit";
            decimal paid = isCredit ? 0m : grandTotal;
            decimal remaining = isCredit ? grandTotal : 0m;
            string methodDisplay = isCredit ? "آجل" : (string.IsNullOrWhiteSpace(receipt.PaymentMethod) ? "نقدي" : receipt.PaymentMethod);

            float payBoxH = isThermal ? 46 : 58;
            RectangleF payBox = new RectangleF(margin, yPos, contentWidth, payBoxH);
            FillRounded(payBox, colorLightBg, isThermal ? 8 : 10);
            BorderRounded(payBox, Color.FromArgb(220, 226, 236), isThermal ? 8 : 10, 1f);
            if (!measureOnly) using (Pen p = new Pen(Color.FromArgb(220, 226, 236), 1f)) g.DrawLine(p, margin + contentWidth / 2, yPos + 8, margin + contentWidth / 2, yPos + payBoxH - 8);

            RectangleF methodCol = new RectangleF(margin, yPos, contentWidth / 2, payBoxH);
            RectangleF paidCol = new RectangleF(margin + contentWidth / 2, yPos, contentWidth / 2, payBoxH);
            Text("طريقة الدفع", fontSmall, colorMuted, new RectangleF(methodCol.X, methodCol.Y + 6, methodCol.Width, isThermal ? 13 : 16), center);
            Text(methodDisplay, fontBodyBold, Color.Black, new RectangleF(methodCol.X, methodCol.Y + payBoxH - (isThermal ? 22 : 26), methodCol.Width, isThermal ? 18 : 22), center);
            Text("المدفوع", fontSmall, colorMuted, new RectangleF(paidCol.X, paidCol.Y + 6, paidCol.Width, isThermal ? 13 : 16), center);
            Text($"{paid:N2} ج.م", fontBodyBold, Color.Black, new RectangleF(paidCol.X, paidCol.Y + payBoxH - (isThermal ? 22 : 26), paidCol.Width, isThermal ? 18 : 22), center);
            yPos += payBoxH + (isThermal ? 8 : 10);

            if (remaining > 0)
            {
                Text($"المتبقي: {remaining:N2} ج.م", fontBodyBold, colorWarning, new RectangleF(0, yPos, pageWidth, isThermal ? 18 : 22), center);
                yPos += isThermal ? 20 : 24;
            }

            yPos += isThermal ? 4 : 6;
            Dashed(yPos);
            yPos += isThermal ? 14 : 18;

            // 11) QR + باركود جنبًا لجنب
            float halfW = contentWidth / 2;
            float codeSize = isThermal ? 64 : 80;

            Text("امسح لعرض الفاتورة", fontSmall, colorMuted, new RectangleF(margin, yPos, halfW, isThermal ? 13 : 16), center);
            if (!measureOnly)
            {
                string qrPayload = $"{brandName}\n{invoiceCode}\n{dateDisplay}\n{receipt.ProductName}\nEGP {grandTotal:N2}";
                RectangleF qrRect = new RectangleF(margin + halfW / 2 - codeSize / 2, yPos + 16, codeSize, codeSize);
                QrCodeHelper.DrawQrCode(g, qrPayload, Rectangle.Round(qrRect));
            }

            Text("باركود الفاتورة", fontSmall, colorMuted, new RectangleF(margin + halfW, yPos, halfW, isThermal ? 13 : 16), center);
            float barcodeH = isThermal ? 38 : 48;
            if (!measureOnly)
            {
                RectangleF barcodeRect = new RectangleF(margin + halfW + 12, yPos + 20, halfW - 24, barcodeH);
                BarcodeHelper.DrawCode39(g, invoiceCode, Rectangle.Round(barcodeRect));
            }
            Text(invoiceCode, fontSmall, Color.Black, new RectangleF(margin + halfW, yPos + 20 + barcodeH + 2, halfW, isThermal ? 13 : 16), center);

            yPos += 16 + codeSize + (isThermal ? 6 : 8);
            Dashed(yPos);
            yPos += isThermal ? 14 : 18;

            // 12) خاتمة الشكر
            Text("♥ شكرًا لتعاملكم معنا ♥", fontLabelBold, colorPrimary, new RectangleF(0, yPos, pageWidth, isThermal ? 18 : 22), center);
            yPos += isThermal ? 20 : 24;
            Text($"نتمنى لكم يومًا سعيدًا مع {brandName}", fontSmall, colorMuted, new RectangleF(0, yPos, pageWidth, isThermal ? 16 : 20), center);
            yPos += isThermal ? 18 : 22;

            yPos += isThermal ? 6 : 8;
            FillRect(new RectangleF(0, yPos, pageWidth, isThermal ? 4 : 6), colorPrimary);
            yPos += isThermal ? 4 : 6;

            return yPos;
        }

        // بيرسم أيقونة جهاز عامة (زخرفية بس - مفيش عمود صور منتجات في قاعدة البيانات
        // فمينفعش نعرض صورة المنتج الحقيقية، وأولى من عرض صورة وهمية)
        private static void DrawDeviceIcon(Graphics g, RectangleF bounds, Color color)
        {
            using (Pen pen = new Pen(color, 1.8f))
            using (var path = RoundedRect(bounds, bounds.Width * 0.22f))
                g.DrawPath(pen, path);

            float dotR = bounds.Width * 0.1f;
            using (Brush b = new SolidBrush(color))
                g.FillEllipse(b, bounds.X + bounds.Width / 2 - dotR / 2, bounds.Bottom - bounds.Height * 0.2f - dotR / 2, dotR, dotR);
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // بيرسم خط منقط أفقي - مستخدم كفواصل شيك بدل الخط العادي
        private void DrawDashedLine(Graphics g, float x1, float x2, float y)
        {
            using (Pen dashedPen = new Pen(Color.FromArgb(190, 195, 205), 1f))
            {
                dashedPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawLine(dashedPen, x1, y, x2, y);
            }
        }
    }
}
