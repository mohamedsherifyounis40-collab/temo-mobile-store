using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // InventoryPageControl: شاشة إدارة المخزون بنوعيها - سويتش فوق للتبديل بين
    // "المخزون" (أي منتج/إكسسوار عادي، من غير سيريال) و"الأجهزة والسريالات"
    // (الموبايلات اللي بتتباع بـ IMEI فردي لكل جهاز). كانوا شاشتين منفصلتين
    // (InventoryPageControl + ImeiPageControl القديمة) ودُمجوا هنا زي ما اتعمل
    // بالظبط مع "الخزينة" (مصروف/قبض/صرف في مكان واحد).
    // ==========================================================================
    public partial class InventoryPageControl : UserControl
    {
        private static readonly Color ColorPrimary = UIHelpers.ColorPrimary;
        private static readonly Color ColorSuccess = UIHelpers.ColorSuccess;
        private static readonly Color ColorDanger = UIHelpers.ColorDanger;
        private static readonly Color ColorWarning = UIHelpers.ColorWarning;
        private static readonly Color ColorNeutral = UIHelpers.ColorNeutral;
        private static readonly Color ColorBackground = UIHelpers.ColorBackground;

        // ---------- عناصر عامة ----------
        private Guna2ComboBox cmbInventoryOperationType;
        private Panel pnlAccessoriesOps, pnlDevicesOps;

        // ---------- المخزون (إكسسوارات ومنتجات عادية) ----------
        private Guna2TextBox txtBarcode, txtProductName, txtCostPrice, txtSalePrice, txtQuantity;
        private Label lblCostPrice;
        private Guna2Button btnAddProduct, btnEditMode, btnSaveUpdate, btnDeleteProduct, btnClear;
        private DataGridView dgvProducts;

        // ---------- الأجهزة والسريالات (موبايلات بـ IMEI) ----------
        private Guna2TextBox txtImeiSearch;
        private Guna2ComboBox cmbImeiStatusFilter;
        private DataGridView dgvImeiUnits;
        private Guna2TextBox txtQaBarcode, txtQaProductName, txtQaImei, txtQaCostPrice, txtQaSalePrice;
        private Guna2Button btnQuickAddDevice, btnSaveModelPriceEdit;
        private string selectedDeviceBarcode = null;

        public InventoryPageControl()
        {
            this.Dock = DockStyle.Fill;
            this.AutoScroll = true;
            this.BackColor = ColorBackground;

            BuildUI();
            LoadProductsData();
            LoadImeiUnitsGrid();
            ApplyEmployeeRestrictionsIfNeeded();
        }

        // ==========================================================================
        // لو المستخدم موظف عادي (مش أدمن): مايشوفش سعر الشراء، ومايقدرش يضيف/يعدّل/يحذف
        // منتجات أو يعدّل سعر موديل جهاز - بس يقدر يضيف جهاز جديد بالـ IMEI عادي
        // ==========================================================================
        private void ApplyEmployeeRestrictionsIfNeeded()
        {
            if (AuthManager.IsAdmin) return;

            lblCostPrice.Visible = false;
            txtCostPrice.Visible = false;
            btnAddProduct.Enabled = false;
            btnEditMode.Enabled = false;
            btnSaveUpdate.Enabled = false;
            btnDeleteProduct.Enabled = false;

            btnSaveModelPriceEdit.Enabled = false;
        }

        // ==========================================================================
        // الهيكل العام: سويتش فوق + بانلين يتبادلوا الظهور (نفس أسلوب شاشة الخزينة)
        // ==========================================================================
        private void BuildUI()
        {
            Label lblOpType = new Label() { Text = "نوع الإدارة:", Location = new Point(20, 20), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            cmbInventoryOperationType = new Guna2ComboBox() { Location = new Point(130, 17), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbInventoryOperationType.Items.AddRange(new string[] { "المخزون (إكسسوارات) 📦", "الأجهزة والسريالات (موبايلات) 📱" });
            cmbInventoryOperationType.SelectedIndexChanged += CmbInventoryOperationType_SelectedIndexChanged;

            pnlAccessoriesOps = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 740) };
            pnlDevicesOps = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 740) };

            BuildAccessoriesPanel();
            BuildDevicesPanel();
            pnlDevicesOps.Visible = false;

            this.Controls.AddRange(new Control[] { lblOpType, cmbInventoryOperationType, pnlAccessoriesOps, pnlDevicesOps });

            cmbInventoryOperationType.SelectedIndex = 0;
        }

        private void CmbInventoryOperationType_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = cmbInventoryOperationType.SelectedIndex;
            pnlAccessoriesOps.Visible = idx == 0;
            pnlDevicesOps.Visible = idx == 1;
        }

        // ==========================================================================
        // بانل "المخزون" - إضافة/تعديل/حذف منتج عادي (إكسسوار أو أي حاجة من غير سيريال)
        // ==========================================================================
        private void BuildAccessoriesPanel()
        {
            Guna2Panel pnlCard = new Guna2Panel()
            {
                Location = new Point(0, 0),
                Size = new Size(300, 520),
                FillColor = Color.White,
                BorderRadius = 14,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1
            };

            Label lblCardTitle = new Label() { Text = "📦 إضافة / تعديل منتج", Location = new Point(20, 18), AutoSize = true, Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblBarcode = new Label() { Text = "باركود المنتج:", Location = new Point(20, 60), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtBarcode = new Guna2TextBox() { Location = new Point(20, 80), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblProductName = new Label() { Text = "اسم المنتج:", Location = new Point(20, 118), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtProductName = new Guna2TextBox() { Location = new Point(20, 138), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            lblCostPrice = new Label() { Text = "سعر الشراء (التكلفة):", Location = new Point(20, 176), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtCostPrice = new Guna2TextBox() { Location = new Point(20, 196), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblSalePrice = new Label() { Text = "سعر البيع للجمهور:", Location = new Point(20, 234), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtSalePrice = new Guna2TextBox() { Location = new Point(20, 254), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblQuantity = new Label() { Text = "الكمية:", Location = new Point(20, 292), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQuantity = new Guna2TextBox() { Location = new Point(20, 312), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblNote = new Label()
            {
                Text = "لإضافة موبايل بسيريال/IMEI، استخدم تبويب \"الأجهزة والسريالات\" بدل كده.",
                Location = new Point(20, 350),
                Size = new Size(260, 32),
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = Color.FromArgb(85, 92, 102)
            };

            btnAddProduct = new Guna2Button() { Text = "إضافة منتج جديد", Location = new Point(20, 388), Width = 260, Height = 38, FillColor = ColorSuccess, BorderRadius = 10 };
            btnAddProduct.Click += BtnAddProduct_Click;

            btnEditMode = new Guna2Button() { Text = "تعديل البند المحدّد", Location = new Point(20, 433), Width = 260, Height = 36, FillColor = ColorPrimary, BorderRadius = 10 };
            btnEditMode.Click += BtnEditMode_Click;

            Guna2Button btnPrintBarcode = new Guna2Button() { Text = "طباعة باركود 🏷️", Location = new Point(20, 473), Width = 260, Height = 34, FillColor = ColorNeutral, ForeColor = ColorPrimary, BorderRadius = 9 };
            btnPrintBarcode.Click += (s, e) => BarcodeHelper.PrintBarcodeLabel(txtBarcode.Text, txtProductName.Text, this.FindForm());

            pnlCard.Controls.AddRange(new Control[] {
                lblCardTitle, lblBarcode, txtBarcode, lblProductName, txtProductName, lblCostPrice, txtCostPrice,
                lblSalePrice, txtSalePrice, lblQuantity, txtQuantity, lblNote, btnAddProduct, btnEditMode, btnPrintBarcode
            });

            // ---------- كارت ثاني (حفظ / حذف / تفريغ) ----------
            Guna2Panel pnlActions = new Guna2Panel()
            {
                Location = new Point(0, 535),
                Size = new Size(300, 175),
                FillColor = Color.White,
                BorderRadius = 14,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1
            };

            btnSaveUpdate = new Guna2Button() { Text = "حفظ التعديلات 💾", Location = new Point(20, 20), Width = 260, Height = 40, FillColor = ColorWarning, Font = new Font("Segoe UI", 9, FontStyle.Bold), Enabled = false, BorderRadius = 10 };
            btnSaveUpdate.Click += BtnSaveUpdate_Click;

            btnDeleteProduct = new Guna2Button() { Text = "حذف المنتج المحدد", Location = new Point(20, 68), Width = 260, Height = 36, FillColor = ColorDanger, BorderRadius = 10 };
            btnDeleteProduct.Click += BtnDeleteProduct_Click;

            btnClear = new Guna2Button() { Text = "تفريغ الخانات", Location = new Point(20, 112), Width = 260, Height = 34, FillColor = ColorNeutral, ForeColor = ColorPrimary, BorderRadius = 10 };
            btnClear.Click += (s, e) => ClearInputs();

            pnlActions.Controls.AddRange(new Control[] { btnSaveUpdate, btnDeleteProduct, btnClear });

            // ---------- كارت الجدول ----------
            Guna2Panel pnlGridCard = new Guna2Panel()
            {
                Location = new Point(320, 0),
                Size = new Size(780, 690),
                FillColor = Color.White,
                BorderRadius = 14,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1
            };
            Label lblGridTitle = new Label() { Text = "📦 المنتجات المسجّلة (إكسسوارات وغيرها)", Location = new Point(20, 18), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = ColorPrimary };

            dgvProducts = new DataGridView() { Location = new Point(20, 55), Size = new Size(740, 615), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvProducts.CellClick += DgvProducts_CellClick;
            StyleDataGridView(dgvProducts);

            pnlGridCard.Controls.AddRange(new Control[] { lblGridTitle, dgvProducts });

            pnlAccessoriesOps.Controls.AddRange(new Control[] { pnlCard, pnlActions, pnlGridCard });
        }

        // ==========================================================================
        // نفس تنسيق الجداول المستخدم في كل شاشات Form1.cs (StyleDataGridView)
        // ==========================================================================
        private void StyleDataGridView(DataGridView dgv) => UIHelpers.StyleDataGridView(dgv);

        // ==========================================================================
        // تحميل بيانات المنتجات - المخزون هنا بيعرض بس المنتجات العادية (من غير سيريال)
        // ==========================================================================
        private void LoadProductsData()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("الباركود"), new DataColumn("اسم المنتج"), new DataColumn("سعر الشراء"), new DataColumn("سعر البيع"), new DataColumn("الكمية") });
            string query = "SELECT Barcode, ProductName, Price, SalePrice, Quantity FROM Products WHERE IsSerialized = 0 OR IsSerialized IS NULL ORDER BY ROWID ASC";
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
                                dt.Rows.Add(reader["Barcode"], reader["ProductName"], reader["Price"], reader["SalePrice"], reader["Quantity"]);
                        }
                        dgvProducts.DataSource = dt;
                        HighlightOutOfStockRows();
                        if (!AuthManager.IsAdmin && dgvProducts.Columns["سعر الشراء"] != null) dgvProducts.Columns["سعر الشراء"].Visible = false;
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void HighlightOutOfStockRows()
        {
            foreach (DataGridViewRow row in dgvProducts.Rows)
            {
                if (row.Cells["الكمية"].Value == null) continue;
                if (Convert.ToInt32(row.Cells["الكمية"].Value) <= 0)
                {
                    row.DefaultCellStyle.ForeColor = ColorDanger;
                    row.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                }
            }
        }

        private void DgvProducts_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                DataGridViewRow row = dgvProducts.Rows[e.RowIndex];
                txtBarcode.Text = row.Cells["الباركود"].Value.ToString();
                txtProductName.Text = row.Cells["اسم المنتج"].Value.ToString();
                txtCostPrice.Text = row.Cells["سعر الشراء"].Value.ToString();
                txtSalePrice.Text = row.Cells["سعر البيع"].Value.ToString();
                txtQuantity.Text = row.Cells["الكمية"].Value.ToString();
                txtBarcode.ReadOnly = true;
                btnSaveUpdate.Enabled = false;
            }
        }

        private void BtnAddProduct_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtBarcode.Text) || string.IsNullOrEmpty(txtProductName.Text)) return;

            if (!decimal.TryParse(txtCostPrice.Text, out decimal costPrice))
            {
                MessageBox.Show("من فضلك أدخل سعر شراء صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!decimal.TryParse(txtSalePrice.Text, out decimal salePrice))
            {
                MessageBox.Show("من فضلك أدخل سعر بيع صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!int.TryParse(txtQuantity.Text, out int quantity))
            {
                MessageBox.Show("من فضلك أدخل كمية صحيحة.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string query = "INSERT INTO Products (Barcode, ProductName, Price, SalePrice, Quantity, IsSerialized) VALUES (@Barcode, @ProductName, @Price, @SalePrice, @Quantity, 0)";
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Barcode", txtBarcode.Text);
                    cmd.Parameters.AddWithValue("@ProductName", txtProductName.Text);
                    cmd.Parameters.AddWithValue("@Price", costPrice);
                    cmd.Parameters.AddWithValue("@SalePrice", salePrice);
                    cmd.Parameters.AddWithValue("@Quantity", quantity);
                    try
                    {
                        conn.Open();
                        cmd.ExecuteNonQuery();
                        LoadProductsData();
                        ClearInputs();
                        MessageBox.Show("تم إضافة المنتج بنجاح!", "نجاح", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void BtnEditMode_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtBarcode.Text))
            {
                MessageBox.Show("اختر منتجاً أولاً!", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            btnSaveUpdate.Enabled = true;
        }

        private void BtnSaveUpdate_Click(object sender, EventArgs e)
        {
            if (!decimal.TryParse(txtCostPrice.Text, out decimal costPrice))
            {
                MessageBox.Show("من فضلك أدخل سعر شراء صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!decimal.TryParse(txtSalePrice.Text, out decimal salePrice))
            {
                MessageBox.Show("من فضلك أدخل سعر بيع صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!int.TryParse(txtQuantity.Text, out int quantity))
            {
                MessageBox.Show("من فضلك أدخل كمية صحيحة.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string query = "UPDATE Products SET ProductName = @ProductName, Price = @Price, SalePrice = @SalePrice, Quantity = @Quantity WHERE Barcode = @Barcode";
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Barcode", txtBarcode.Text);
                    cmd.Parameters.AddWithValue("@ProductName", txtProductName.Text);
                    cmd.Parameters.AddWithValue("@Price", costPrice);
                    cmd.Parameters.AddWithValue("@SalePrice", salePrice);
                    cmd.Parameters.AddWithValue("@Quantity", quantity);
                    try
                    {
                        conn.Open();
                        cmd.ExecuteNonQuery();
                        LoadProductsData();
                        btnSaveUpdate.Enabled = false;
                        MessageBox.Show("تم تعديل المنتج!", "نجاح", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void ClearInputs()
        {
            txtBarcode.ReadOnly = false;
            txtBarcode.Clear();
            txtProductName.Clear();
            txtCostPrice.Clear();
            txtSalePrice.Clear();
            txtQuantity.Clear();
            btnSaveUpdate.Enabled = false;
            txtBarcode.Focus();
        }

        private void BtnDeleteProduct_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtBarcode.Text)) return;
            if (MessageBox.Show("حذف المنتج؟", "تحذير", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                string query = "DELETE FROM Products WHERE Barcode = @Barcode";
                using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
                {
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Barcode", txtBarcode.Text);
                        try { conn.Open(); cmd.ExecuteNonQuery(); LoadProductsData(); ClearInputs(); }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                    }
                }
            }
        }

        // ==========================================================================
        // بانل "الأجهزة والسريالات" - موبايلات بيتباعوا بـ IMEI فردي لكل جهاز
        // ==========================================================================
        private void BuildDevicesPanel()
        {
            // ---------- كارت البحث والفلترة ----------
            Guna2Panel gbSearch = new Guna2Panel() { Location = new Point(0, 0), Size = new Size(300, 220), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblSearchTitle = new Label() { Text = "🔍 بحث وفلترة", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblSearch = new Label() { Text = "بحث برقم الـIMEI أو اسم المنتج:", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtImeiSearch = new Guna2TextBox() { Location = new Point(20, 70), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };
            txtImeiSearch.TextChanged += (s, e) => LoadImeiUnitsGrid();

            Label lblStatusFilter = new Label() { Text = "الحالة:", Location = new Point(20, 108), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbImeiStatusFilter = new Guna2ComboBox() { Location = new Point(20, 128), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbImeiStatusFilter.Items.AddRange(new string[] { "الكل", "متاح في المخزون", "مباع" });
            cmbImeiStatusFilter.SelectedIndex = 0;
            cmbImeiStatusFilter.SelectedIndexChanged += (s, e) => LoadImeiUnitsGrid();

            Guna2Button btnRefreshImei = new Guna2Button() { Text = "تحديث 🔄", Location = new Point(20, 168), Width = 260, Height = 34, FillColor = ColorPrimary, BorderRadius = 9 };
            btnRefreshImei.Click += (s, e) => LoadImeiUnitsGrid();

            gbSearch.Controls.AddRange(new Control[] { lblSearchTitle, lblSearch, txtImeiSearch, lblStatusFilter, cmbImeiStatusFilter, btnRefreshImei });

            // ---------- كارت إضافة جهاز يدوي / تعديل سعر موديل محدد ----------
            Guna2Panel gbQuickAdd = new Guna2Panel() { Location = new Point(0, 235), Size = new Size(300, 545), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblQaTitle = new Label() { Text = "➕ إضافة جهاز / تعديل سعر موديل", Location = new Point(20, 15), AutoSize = true, Size = new Size(260, 34), Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblQaBarcode = new Label() { Text = "الباركود (سيبها فاضية لو مفيش):", Location = new Point(20, 55), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQaBarcode = new Guna2TextBox() { Location = new Point(20, 75), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblQaName = new Label() { Text = "اسم المنتج:", Location = new Point(20, 113), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQaProductName = new Guna2TextBox() { Location = new Point(20, 133), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblQaImei = new Label() { Text = "رقم الـIMEI (لجهاز جديد بس):", Location = new Point(20, 171), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQaImei = new Guna2TextBox() { Location = new Point(20, 191), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblQaCost = new Label() { Text = "سعر الشراء (التكلفة):", Location = new Point(20, 229), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQaCostPrice = new Guna2TextBox() { Location = new Point(20, 249), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblQaSale = new Label() { Text = "سعر البيع للجمهور:", Location = new Point(20, 287), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQaSalePrice = new Guna2TextBox() { Location = new Point(20, 307), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblQaNote = new Label()
            {
                Text = "لإضافة جهاز جديد: املأ كل الخانات وبينها الـIMEI. لتعديل سعر موديل موجود: دوس على أي جهاز من الجدول (هيملأ الخانات، اسيب الـIMEI فاضي) وادوس \"حفظ تعديل السعر\".",
                Location = new Point(20, 345),
                Size = new Size(260, 60),
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = Color.FromArgb(85, 92, 102)
            };

            btnQuickAddDevice = new Guna2Button() { Text = "إضافة جهاز جديد ✅", Location = new Point(20, 410), Width = 260, Height = 38, FillColor = ColorSuccess, BorderRadius = 10 };
            btnQuickAddDevice.Click += BtnQuickAddDevice_Click;

            btnSaveModelPriceEdit = new Guna2Button() { Text = "حفظ تعديل سعر الموديل 💾", Location = new Point(20, 456), Width = 260, Height = 36, FillColor = ColorWarning, Font = new Font("Segoe UI", 9, FontStyle.Bold), BorderRadius = 10 };
            btnSaveModelPriceEdit.Click += BtnSaveModelPriceEdit_Click;

            gbQuickAdd.Controls.AddRange(new Control[] { lblQaTitle, lblQaBarcode, txtQaBarcode, lblQaName, txtQaProductName, lblQaImei, txtQaImei, lblQaCost, txtQaCostPrice, lblQaSale, txtQaSalePrice, lblQaNote, btnQuickAddDevice, btnSaveModelPriceEdit });

            // ---------- كارت الجدول ----------
            Guna2Panel pnlGridCard = new Guna2Panel() { Location = new Point(320, 0), Size = new Size(780, 780), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblGridTitle = new Label() { Text = "📱 كل الأجهزة المسجّلة بأرقام الـIMEI", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = ColorPrimary };

            dgvImeiUnits = new DataGridView() { Location = new Point(20, 50), Size = new Size(740, 715), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvImeiUnits.CellClick += DgvImeiUnits_CellClick;
            StyleDataGridView(dgvImeiUnits);

            pnlGridCard.Controls.AddRange(new Control[] { lblGridTitle, dgvImeiUnits });

            pnlDevicesOps.Controls.AddRange(new Control[] { gbSearch, gbQuickAdd, pnlGridCard });
        }

        // ==========================================================================
        // تحميل جدول الأجهزة مع الفلترة والبحث
        // ==========================================================================
        private void LoadImeiUnitsGrid()
        {
            if (dgvImeiUnits == null) return;

            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("IMEI"), new DataColumn("الباركود"), new DataColumn("المنتج"), new DataColumn("الحالة"), new DataColumn("تاريخ الإضافة") });

            string statusFilter = cmbImeiStatusFilter?.SelectedItem?.ToString() ?? "الكل";
            string search = txtImeiSearch?.Text?.Trim() ?? "";

            string query = @"SELECT PU.IMEI, PU.Barcode, PU.Status, PU.CreatedAt, P.ProductName
                FROM ProductUnits PU LEFT JOIN Products P ON PU.Barcode = P.Barcode
                WHERE (PU.IMEI LIKE @Search OR P.ProductName LIKE @Search)";

            if (statusFilter == "متاح في المخزون") query += " AND PU.Status = 'InStock'";
            else if (statusFilter == "مباع") query += " AND PU.Status = 'Sold'";

            query += " ORDER BY PU.CreatedAt DESC";

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Search", "%" + search + "%");
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string statusArabic = reader["Status"].ToString() == "InStock" ? "متاح في المخزون" : (reader["Status"].ToString() == "Sold" ? "مباع" : reader["Status"].ToString());
                                dt.Rows.Add(reader["IMEI"], reader["Barcode"], reader["ProductName"], statusArabic, reader["CreatedAt"]);
                            }
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }

            dgvImeiUnits.DataSource = dt;
            if (dgvImeiUnits.Columns["الباركود"] != null) dgvImeiUnits.Columns["الباركود"].Visible = false;
        }

        // ==========================================================================
        // دوس على جهاز في الجدول: يحمّل بيانات موديله (باركود/اسم/سعر) عشان تعديل السعر -
        // من غير ما يمس رقم الـIMEI بتاع الوحدة دي (مينفعش يتغيّر)
        // ==========================================================================
        private void DgvImeiUnits_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            string barcode = dgvImeiUnits.Rows[e.RowIndex].Cells["الباركود"].Value?.ToString();
            if (string.IsNullOrEmpty(barcode)) return;

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("SELECT ProductName, Price, SalePrice FROM Products WHERE Barcode = @B", conn))
                {
                    cmd.Parameters.AddWithValue("@B", barcode);
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read()) return;
                            selectedDeviceBarcode = barcode;
                            txtQaBarcode.Text = barcode;
                            txtQaProductName.Text = reader["ProductName"].ToString();
                            txtQaCostPrice.Text = reader["Price"].ToString();
                            txtQaSalePrice.Text = reader["SalePrice"].ToString();
                            txtQaImei.Clear();
                            txtQaBarcode.ReadOnly = true;
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        // ==========================================================================
        // حفظ تعديل سعر موديل جهاز محدد (من غير ما يضيف IMEI جديد) - أدمن بس
        // ==========================================================================
        private void BtnSaveModelPriceEdit_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(selectedDeviceBarcode))
            {
                MessageBox.Show("من فضلك اختر جهاز من الجدول أولاً عشان تعدّل سعر موديله.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!decimal.TryParse(txtQaCostPrice.Text, out decimal costPrice) || !decimal.TryParse(txtQaSalePrice.Text, out decimal salePrice))
            {
                MessageBox.Show("من فضلك أدخل سعري شراء وبيع صحيحين.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("UPDATE Products SET ProductName = @Name, Price = @Cost, SalePrice = @Sale WHERE Barcode = @Barcode", conn))
                {
                    cmd.Parameters.AddWithValue("@Name", txtQaProductName.Text);
                    cmd.Parameters.AddWithValue("@Cost", costPrice);
                    cmd.Parameters.AddWithValue("@Sale", salePrice);
                    cmd.Parameters.AddWithValue("@Barcode", selectedDeviceBarcode);
                    try
                    {
                        conn.Open();
                        cmd.ExecuteNonQuery();
                        MessageBox.Show("تم تعديل سعر الموديل بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadImeiUnitsGrid();
                        ClearDeviceInputs();
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        // ==========================================================================
        // إضافة جهاز يدوي مباشرة للمخزون (من غير فاتورة مورد)
        // ==========================================================================
        private void BtnQuickAddDevice_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtQaProductName.Text) || string.IsNullOrWhiteSpace(txtQaImei.Text)
                || !decimal.TryParse(txtQaCostPrice.Text, out decimal costPrice) || costPrice < 0
                || !decimal.TryParse(txtQaSalePrice.Text, out decimal salePrice) || salePrice < 0)
            {
                MessageBox.Show("من فضلك أدخل اسم المنتج، رقم الـIMEI، وسعري الشراء والبيع بشكل صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string imei = txtQaImei.Text.Trim();
            string barcode = txtQaBarcode.Text.Trim();

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                using (SqliteCommand cmdCheckImei = new SqliteCommand("SELECT COUNT(*) FROM ProductUnits WHERE IMEI = @IMEI", conn))
                {
                    cmdCheckImei.Parameters.AddWithValue("@IMEI", imei);
                    if (Convert.ToInt32(cmdCheckImei.ExecuteScalar()) > 0)
                    {
                        MessageBox.Show($"رقم الـIMEI \"{imei}\" مسجل بالفعل في النظام من قبل.", "رقم مكرر", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                bool productExists = false;
                if (!string.IsNullOrEmpty(barcode))
                {
                    using (SqliteCommand cmdCheck = new SqliteCommand("SELECT COUNT(*) FROM Products WHERE Barcode = @B", conn))
                    {
                        cmdCheck.Parameters.AddWithValue("@B", barcode);
                        productExists = Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0;
                    }
                }

                string finalBarcode = barcode;

                try
                {
                    if (productExists)
                    {
                        using (SqliteCommand cmd = new SqliteCommand(
                            "UPDATE Products SET Quantity = Quantity + 1, Price = @U, IsSerialized = 1 WHERE Barcode = @B", conn))
                        {
                            cmd.Parameters.AddWithValue("@U", costPrice);
                            cmd.Parameters.AddWithValue("@B", barcode);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        finalBarcode = string.IsNullOrEmpty(barcode) ? ("NEW-" + DateTime.Now.Ticks) : barcode;
                        using (SqliteCommand cmd = new SqliteCommand(
                            "INSERT INTO Products (Barcode, ProductName, Price, SalePrice, Quantity, IsSerialized) VALUES (@B, @N, @U, @S, 1, 1)", conn))
                        {
                            cmd.Parameters.AddWithValue("@B", finalBarcode);
                            cmd.Parameters.AddWithValue("@N", txtQaProductName.Text.Trim());
                            cmd.Parameters.AddWithValue("@U", costPrice);
                            cmd.Parameters.AddWithValue("@S", salePrice);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    using (SqliteCommand cmd = new SqliteCommand(
                        "INSERT INTO ProductUnits (Barcode, IMEI, Status, PurchaseId, CreatedAt) VALUES (@B, @IMEI, 'InStock', NULL, @C)", conn))
                    {
                        cmd.Parameters.AddWithValue("@B", finalBarcode);
                        cmd.Parameters.AddWithValue("@IMEI", imei);
                        cmd.Parameters.AddWithValue("@C", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("حصل خطأ: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            MessageBox.Show("تم إضافة الجهاز للمخزون بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearDeviceInputs();
            LoadImeiUnitsGrid();
        }

        private void ClearDeviceInputs()
        {
            selectedDeviceBarcode = null;
            txtQaBarcode.ReadOnly = false;
            txtQaBarcode.Clear();
            txtQaProductName.Clear();
            txtQaImei.Clear();
            txtQaCostPrice.Clear();
            txtQaSalePrice.Clear();
        }
    }
}
