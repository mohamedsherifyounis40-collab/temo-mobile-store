using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // InventoryPageControl: نسخة مستقلة تمامًا من شاشة "إدارة المخزن والبضاعة"
    // الموجودة في Form1.cs (CreateInventoryDesign). نفس مبدأ SalesPageControl:
    // منفصل بالكامل عن Form1.cs، بيقرا ويكتب على نفس قاعدة البيانات بالظبط.
    // ==========================================================================
    public partial class InventoryPageControl : UserControl
    {
        private static readonly Color ColorPrimary = UIHelpers.ColorPrimary;
        private static readonly Color ColorSuccess = UIHelpers.ColorSuccess;
        private static readonly Color ColorDanger = UIHelpers.ColorDanger;
        private static readonly Color ColorWarning = UIHelpers.ColorWarning;
        private static readonly Color ColorNeutral = UIHelpers.ColorNeutral;
        private static readonly Color ColorBackground = UIHelpers.ColorBackground;

        private string connectionString = $"Data Source={System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TemoStoreDB.db")};";

        private Guna2TextBox txtBarcode, txtProductName, txtCostPrice, txtSalePrice, txtQuantity;
        private Label lblCostPrice;
        private CheckBox chkIsSerialized;
        private Guna2Button btnAddProduct, btnEditMode, btnSaveUpdate, btnDeleteProduct, btnClear;
        private DataGridView dgvProducts;

        public InventoryPageControl()
        {
            this.Dock = DockStyle.Fill;
            this.AutoScroll = true;
            this.BackColor = ColorBackground;

            BuildUI();
            LoadProductsData();
            ApplyEmployeeRestrictionsIfNeeded();
        }

        // ==========================================================================
        // لو المستخدم موظف عادي (مش أدمن)، نطبّق نفس القيود اللي في Form1.cs بالظبط:
        // مايشوفش سعر الشراء، ومايقدرش يضيف/يعدّل/يحذف منتجات
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
        }

        // ==========================================================================
        // بناء شكل الشاشة - نفس تصميم وأماكن Form1.cs (CreateInventoryDesign) بالظبط
        // ==========================================================================
        private void BuildUI()
        {
            Guna2Panel pnlCard = new Guna2Panel()
            {
                Location = new Point(20, 20),
                Size = new Size(300, 520),
                FillColor = Color.White,
                BorderRadius = 14,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1
            };

            Label lblCardTitle = new Label() { Text = "🗄️ إضافة / تعديل منتج", Location = new Point(20, 18), AutoSize = true, Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = ColorPrimary };

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

            chkIsSerialized = new CheckBox() { Text = "منتج بسيريال/IMEI (موبايل)", Location = new Point(20, 350), AutoSize = true };

            btnAddProduct = new Guna2Button() { Text = "إضافة منتج جديد", Location = new Point(20, 385), Width = 260, Height = 38, FillColor = ColorSuccess, BorderRadius = 10 };
            btnAddProduct.Click += BtnAddProduct_Click;

            btnEditMode = new Guna2Button() { Text = "تعديل البند المحدّد", Location = new Point(20, 430), Width = 260, Height = 36, FillColor = ColorPrimary, BorderRadius = 10 };
            btnEditMode.Click += BtnEditMode_Click;

            Guna2Button btnPrintBarcode = new Guna2Button() { Text = "طباعة باركود 🏷️", Location = new Point(20, 470), Width = 260, Height = 34, FillColor = ColorNeutral, ForeColor = ColorPrimary, BorderRadius = 9 };
            btnPrintBarcode.Click += (s, e) => BarcodeHelper.PrintBarcodeLabel(txtBarcode.Text, txtProductName.Text, this.FindForm());

            pnlCard.Controls.AddRange(new Control[] {
                lblCardTitle, lblBarcode, txtBarcode, lblProductName, txtProductName, lblCostPrice, txtCostPrice,
                lblSalePrice, txtSalePrice, lblQuantity, txtQuantity, chkIsSerialized, btnAddProduct, btnEditMode, btnPrintBarcode
            });

            // ---------- كارت ثاني (حفظ / حذف / تفريغ) ----------
            Guna2Panel pnlActions = new Guna2Panel()
            {
                Location = new Point(20, 555),
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
                Location = new Point(340, 20),
                Size = new Size(780, 690),
                FillColor = Color.White,
                BorderRadius = 14,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1
            };
            Label lblGridTitle = new Label() { Text = "📦 المنتجات المسجّلة", Location = new Point(20, 18), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = ColorPrimary };

            dgvProducts = new DataGridView() { Location = new Point(20, 55), Size = new Size(740, 615), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvProducts.CellClick += DgvProducts_CellClick;
            StyleDataGridView(dgvProducts);

            pnlGridCard.Controls.AddRange(new Control[] { lblGridTitle, dgvProducts });

            this.Controls.AddRange(new Control[] { pnlCard, pnlActions, pnlGridCard });
        }

        // ==========================================================================
        // نفس تنسيق الجداول المستخدم في كل شاشات Form1.cs (StyleDataGridView)
        // ==========================================================================
        private void StyleDataGridView(DataGridView dgv) => UIHelpers.StyleDataGridView(dgv);

        // ==========================================================================
        // تحميل بيانات المنتجات
        // ==========================================================================
        private void LoadProductsData()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("الباركود"), new DataColumn("اسم المنتج"), new DataColumn("سعر الشراء"), new DataColumn("سعر البيع"), new DataColumn("الكمية"), new DataColumn("IsSerialized") });
            string query = "SELECT Barcode, ProductName, Price, SalePrice, Quantity, IsSerialized FROM Products ORDER BY ROWID ASC";
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                dt.Rows.Add(reader["Barcode"], reader["ProductName"], reader["Price"], reader["SalePrice"], reader["Quantity"], reader["IsSerialized"] == DBNull.Value ? 0 : reader["IsSerialized"]);
                        }
                        dgvProducts.DataSource = dt;
                        HighlightOutOfStockRows();
                        if (dgvProducts.Columns["IsSerialized"] != null) dgvProducts.Columns["IsSerialized"].Visible = false;
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
                chkIsSerialized.Checked = Convert.ToInt32(row.Cells["IsSerialized"].Value) == 1;
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

            string query = "INSERT INTO Products (Barcode, ProductName, Price, SalePrice, Quantity, IsSerialized) VALUES (@Barcode, @ProductName, @Price, @SalePrice, @Quantity, @IsSerialized)";
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Barcode", txtBarcode.Text);
                    cmd.Parameters.AddWithValue("@ProductName", txtProductName.Text);
                    cmd.Parameters.AddWithValue("@Price", costPrice);
                    cmd.Parameters.AddWithValue("@SalePrice", salePrice);
                    cmd.Parameters.AddWithValue("@Quantity", quantity);
                    cmd.Parameters.AddWithValue("@IsSerialized", chkIsSerialized.Checked ? 1 : 0);
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

            string query = "UPDATE Products SET ProductName = @ProductName, Price = @Price, SalePrice = @SalePrice, Quantity = @Quantity, IsSerialized = @IsSerialized WHERE Barcode = @Barcode";
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Barcode", txtBarcode.Text);
                    cmd.Parameters.AddWithValue("@ProductName", txtProductName.Text);
                    cmd.Parameters.AddWithValue("@Price", costPrice);
                    cmd.Parameters.AddWithValue("@SalePrice", salePrice);
                    cmd.Parameters.AddWithValue("@Quantity", quantity);
                    cmd.Parameters.AddWithValue("@IsSerialized", chkIsSerialized.Checked ? 1 : 0);
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
            chkIsSerialized.Checked = false;
            btnSaveUpdate.Enabled = false;
            txtBarcode.Focus();
        }

        private void BtnDeleteProduct_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtBarcode.Text)) return;
            if (MessageBox.Show("حذف المنتج؟", "تحذير", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                string query = "DELETE FROM Products WHERE Barcode = @Barcode";
                using (SqliteConnection conn = new SqliteConnection(connectionString))
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
    }
}
