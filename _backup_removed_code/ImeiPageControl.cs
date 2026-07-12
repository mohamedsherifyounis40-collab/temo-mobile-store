using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // ImeiPageControl: نسخة مستقلة من شاشة "الأجهزة والسيريالات" الموجودة في
    // Form1.cs (CreateImeiDesign). نفس مبدأ باقي الشاشات: منفصل بالكامل عن
    // Form1.cs، بيقرا ويكتب على نفس قاعدة البيانات بالظبط.
    // ==========================================================================
    public partial class ImeiPageControl : UserControl
    {
        private static readonly Color ColorPrimary = UIHelpers.ColorPrimary;
        private static readonly Color ColorSuccess = UIHelpers.ColorSuccess;
        private static readonly Color ColorBackground = UIHelpers.ColorBackground;

        private Guna2TextBox txtImeiSearch;
        private Guna2ComboBox cmbImeiStatusFilter;
        private DataGridView dgvImeiUnits;

        private Guna2TextBox txtQaBarcode, txtQaProductName, txtQaImei, txtQaCostPrice, txtQaSalePrice;

        public ImeiPageControl()
        {
            this.Dock = DockStyle.Fill;
            this.AutoScroll = true;
            this.BackColor = ColorBackground;

            BuildUI();
            LoadImeiUnitsGrid();
        }

        private void BuildUI()
        {
            // ---------- كارت البحث والفلترة ----------
            Guna2Panel gbSearch = new Guna2Panel() { Location = new Point(20, 20), Size = new Size(300, 220), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
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

            // ---------- كارت إضافة جهاز يدوي ----------
            Guna2Panel gbQuickAdd = new Guna2Panel() { Location = new Point(20, 255), Size = new Size(300, 470), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblQaTitle = new Label() { Text = "➕ إضافة جهاز يدوي", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblQaBarcode = new Label() { Text = "الباركود (سيبها فاضية لو مفيش):", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQaBarcode = new Guna2TextBox() { Location = new Point(20, 70), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblQaName = new Label() { Text = "اسم المنتج:", Location = new Point(20, 108), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQaProductName = new Guna2TextBox() { Location = new Point(20, 128), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblQaImei = new Label() { Text = "رقم الـIMEI:", Location = new Point(20, 166), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQaImei = new Guna2TextBox() { Location = new Point(20, 186), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblQaCost = new Label() { Text = "سعر الشراء (التكلفة):", Location = new Point(20, 224), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQaCostPrice = new Guna2TextBox() { Location = new Point(20, 244), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblQaSale = new Label() { Text = "سعر البيع للجمهور:", Location = new Point(20, 282), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQaSalePrice = new Guna2TextBox() { Location = new Point(20, 302), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblQaNote = new Label()
            {
                Text = "الجهاز ده هيتضاف مباشرة للمخزون بكميته وسيريال، من غير ما يتسجل على أي مورد أو فاتورة شراء.",
                Location = new Point(20, 342),
                Size = new Size(260, 45),
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = Color.FromArgb(85, 92, 102)
            };

            Guna2Button btnQuickAddDevice = new Guna2Button() { Text = "إضافة الجهاز ✅", Location = new Point(20, 400), Width = 260, Height = 38, FillColor = ColorSuccess, BorderRadius = 10 };
            btnQuickAddDevice.Click += BtnQuickAddDevice_Click;

            gbQuickAdd.Controls.AddRange(new Control[] { lblQaTitle, lblQaBarcode, txtQaBarcode, lblQaName, txtQaProductName, lblQaImei, txtQaImei, lblQaCost, txtQaCostPrice, lblQaSale, txtQaSalePrice, lblQaNote, btnQuickAddDevice });

            // ---------- كارت الجدول ----------
            Guna2Panel pnlGridCard = new Guna2Panel() { Location = new Point(340, 20), Size = new Size(780, 705), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblGridTitle = new Label() { Text = "📱 كل الأجهزة المسجّلة بأرقام الـIMEI", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = ColorPrimary };

            dgvImeiUnits = new DataGridView() { Location = new Point(20, 50), Size = new Size(740, 640), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            StyleDataGridView(dgvImeiUnits);

            pnlGridCard.Controls.AddRange(new Control[] { lblGridTitle, dgvImeiUnits });

            this.Controls.AddRange(new Control[] { gbSearch, gbQuickAdd, pnlGridCard });
        }

        // ==========================================================================
        // نفس تنسيق الجداول المستخدم في كل شاشات Form1.cs
        // ==========================================================================
        private void StyleDataGridView(DataGridView dgv) => UIHelpers.StyleDataGridView(dgv);

        // ==========================================================================
        // تحميل جدول الأجهزة مع الفلترة والبحث
        // ==========================================================================
        private void LoadImeiUnitsGrid()
        {
            if (dgvImeiUnits == null) return;

            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("IMEI"), new DataColumn("المنتج"), new DataColumn("الحالة"), new DataColumn("تاريخ الإضافة") });

            string statusFilter = cmbImeiStatusFilter?.SelectedItem?.ToString() ?? "الكل";
            string search = txtImeiSearch?.Text?.Trim() ?? "";

            string query = @"SELECT PU.IMEI, PU.Status, PU.CreatedAt, P.ProductName
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
                                dt.Rows.Add(reader["IMEI"], reader["ProductName"], statusArabic, reader["CreatedAt"]);
                            }
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }

            dgvImeiUnits.DataSource = dt;
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
            txtQaBarcode.Clear();
            txtQaProductName.Clear();
            txtQaImei.Clear();
            txtQaCostPrice.Clear();
            txtQaSalePrice.Clear();
            LoadImeiUnitsGrid();
        }
    }
}
