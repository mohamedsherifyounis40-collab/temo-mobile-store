using System;
using System.Data;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using TemoStore.Core.Commands;
using TemoStore.Core.Exceptions;

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
        private PictureBox picProductImage;
        private byte[] pendingProductImage = null;

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
            this.Size = new Size(1150, 1150); // مقاس مبدئي واقعي قبل بناء الشاشة، عشان حسابات Anchor متبقاش غلط (راجع نفس التعليق في SalesPageControl)
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

            AnchorStyles fillAnchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlAccessoriesOps = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 800), Anchor = fillAnchor };
            pnlDevicesOps = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 800), Anchor = fillAnchor };

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
                Size = new Size(300, 610),
                FillColor = Color.White,
                BorderRadius = UIHelpers.CardBorderRadius,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1
            };

            var badgeCardTitle = UIHelpers.CreateIconBadge("📦", UIHelpers.ColorAccentPrimary, new Point(20, 16));
            Label lblCardTitle = new Label() { Text = "إضافة / تعديل منتج", Location = new Point(64, 24), AutoSize = true, Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblBarcode = new Label() { Text = "باركود المنتج:", Location = new Point(20, 60), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtBarcode = new Guna2TextBox() { Location = new Point(20, 80), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251), BorderColor = Color.FromArgb(225, 228, 235), BorderThickness = 1 };

            Label lblProductName = new Label() { Text = "اسم المنتج:", Location = new Point(20, 118), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtProductName = new Guna2TextBox() { Location = new Point(20, 138), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251), BorderColor = Color.FromArgb(225, 228, 235), BorderThickness = 1 };

            lblCostPrice = new Label() { Text = "سعر الشراء (التكلفة):", Location = new Point(20, 176), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtCostPrice = new Guna2TextBox() { Location = new Point(20, 196), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251), BorderColor = Color.FromArgb(225, 228, 235), BorderThickness = 1 };

            Label lblSalePrice = new Label() { Text = "سعر البيع للجمهور:", Location = new Point(20, 234), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtSalePrice = new Guna2TextBox() { Location = new Point(20, 254), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251), BorderColor = Color.FromArgb(225, 228, 235), BorderThickness = 1 };

            Label lblQuantity = new Label() { Text = "الكمية:", Location = new Point(20, 292), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQuantity = new Guna2TextBox() { Location = new Point(20, 312), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251), BorderColor = Color.FromArgb(225, 228, 235), BorderThickness = 1 };

            // ---------- صورة المنتج (اختيارية، تجميلية) - بتتحفظ مباشرة على المخزون
            // من غير ما تعدّي على CoreEngine، عشان تظهر في كروت "الأكثر مبيعًا" بالمبيعات ----------
            picProductImage = new PictureBox() { Location = new Point(20, 330), Size = new Size(80, 80), SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
            Guna2Button btnUploadProductImage = new Guna2Button() { Text = "رفع صورة 🖼️", Location = new Point(112, 330), Width = 168, Height = 32, FillColor = UIHelpers.ColorAccentPrimary, BorderRadius = 9, Font = new Font("Segoe UI", 8F) };
            btnUploadProductImage.Click += BtnUploadProductImage_Click;
            Guna2Button btnRemoveProductImage = new Guna2Button() { Text = "إزالة الصورة 🗑️", Location = new Point(112, 368), Width = 168, Height = 30, FillColor = UIHelpers.ColorRed, BorderRadius = 9, Font = new Font("Segoe UI", 8F) };
            btnRemoveProductImage.Click += BtnRemoveProductImage_Click;

            Label lblNote = new Label()
            {
                Text = "لإضافة موبايل بسيريال/IMEI، استخدم تبويب \"الأجهزة والسريالات\" بدل كده.",
                Location = new Point(20, 420),
                Size = new Size(260, 32),
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = Color.FromArgb(85, 92, 102)
            };

            btnAddProduct = new Guna2Button() { Text = "إضافة منتج جديد", Location = new Point(20, 458), Width = 260, Height = 38, FillColor = UIHelpers.ColorGreen, BorderRadius = 10 };
            btnAddProduct.Click += BtnAddProduct_Click;
            UIHelpers.MakePill(btnAddProduct);

            btnEditMode = new Guna2Button() { Text = "تعديل البند المحدّد", Location = new Point(20, 503), Width = 260, Height = 36, FillColor = UIHelpers.ColorOrange, BorderRadius = 10 };
            btnEditMode.Click += BtnEditMode_Click;
            UIHelpers.MakePill(btnEditMode);

            Guna2Button btnPrintBarcode = new Guna2Button() { Text = "طباعة باركود 🏷️", Location = new Point(20, 543), Width = 195, Height = 34, FillColor = Color.White, ForeColor = ColorPrimary, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1, BorderRadius = 9 };
            UIHelpers.MakePill(btnPrintBarcode);
            NumericUpDown numPrintCopies = new NumericUpDown() { Location = new Point(222, 543), Width = 58, Height = 34, Minimum = 1, Maximum = 200, Value = 1, TextAlign = HorizontalAlignment.Center, Font = new Font("Segoe UI", 9F) };
            btnPrintBarcode.Click += (s, e) => BarcodeHelper.PrintBarcodeLabel(txtBarcode.Text, txtProductName.Text, this.FindForm(), (int)numPrintCopies.Value);

            pnlCard.Controls.AddRange(new Control[] {
                badgeCardTitle, lblCardTitle, lblBarcode, txtBarcode, lblProductName, txtProductName, lblCostPrice, txtCostPrice,
                lblSalePrice, txtSalePrice, lblQuantity, txtQuantity, picProductImage, btnUploadProductImage, btnRemoveProductImage,
                lblNote, btnAddProduct, btnEditMode, btnPrintBarcode, numPrintCopies
            });

            // ---------- كارت ثاني (حفظ / حذف / تفريغ) ----------
            Guna2Panel pnlActions = new Guna2Panel()
            {
                Location = new Point(0, 622),
                Size = new Size(300, 255),
                FillColor = Color.White,
                BorderRadius = UIHelpers.CardBorderRadius,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1
            };

            btnSaveUpdate = new Guna2Button() { Text = "حفظ التعديلات 💾", Location = new Point(20, 20), Width = 260, Height = 40, FillColor = UIHelpers.ColorGreen, Font = new Font("Segoe UI", 9, FontStyle.Bold), Enabled = false, BorderRadius = 10 };
            btnSaveUpdate.Click += BtnSaveUpdate_Click;
            UIHelpers.MakePill(btnSaveUpdate);

            btnDeleteProduct = new Guna2Button() { Text = "حذف المنتج المحدد", Location = new Point(20, 68), Width = 260, Height = 36, FillColor = UIHelpers.ColorRed, BorderRadius = 10 };
            btnDeleteProduct.Click += BtnDeleteProduct_Click;
            UIHelpers.MakePill(btnDeleteProduct);

            btnClear = new Guna2Button() { Text = "تفريغ الخانات", Location = new Point(20, 112), Width = 260, Height = 34, FillColor = Color.White, ForeColor = ColorPrimary, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1, BorderRadius = 10 };
            btnClear.Click += (s, e) => ClearInputs();
            UIHelpers.MakePill(btnClear);

            Guna2Button btnDownloadTemplate = new Guna2Button() { Text = "تحميل قالب إكسل ⬇️", Location = new Point(20, 156), Width = 260, Height = 34, FillColor = Color.White, ForeColor = ColorPrimary, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1, BorderRadius = 10 };
            btnDownloadTemplate.Click += BtnDownloadTemplate_Click;
            UIHelpers.MakePill(btnDownloadTemplate);

            Guna2Button btnImportExcel = new Guna2Button() { Text = "استيراد من إكسل 📥", Location = new Point(20, 200), Width = 260, Height = 34, FillColor = UIHelpers.ColorAccentPrimary, BorderRadius = 10 };
            btnImportExcel.Click += BtnImportExcel_Click;
            UIHelpers.MakePill(btnImportExcel);

            pnlActions.Controls.AddRange(new Control[] { btnSaveUpdate, btnDeleteProduct, btnClear, btnDownloadTemplate, btnImportExcel });

            // ---------- كارت الجدول ----------
            Guna2Panel pnlGridCard = new Guna2Panel()
            {
                Location = new Point(320, 0),
                Size = new Size(780, 690),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                FillColor = Color.White,
                BorderRadius = UIHelpers.CardBorderRadius,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1
            };
            var badgeGridTitle = UIHelpers.CreateIconBadge("📦", UIHelpers.ColorGreen, new Point(20, 16));
            Label lblGridTitle = new Label() { Text = "المنتجات المسجّلة (إكسسوارات وغيرها)", Location = new Point(64, 24), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = ColorPrimary };

            dgvProducts = new DataGridView() { Location = new Point(20, 55), Size = new Size(740, 615), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvProducts.CellClick += DgvProducts_CellClick;
            StyleDataGridView(dgvProducts);

            pnlGridCard.Controls.AddRange(new Control[] { badgeGridTitle, lblGridTitle, dgvProducts });

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
            try
            {
                dgvProducts.DataSource = InventoryRepository.GetAccessoryProducts();
                HighlightOutOfStockRows();
                if (!AuthManager.IsAdmin && dgvProducts.Columns["سعر الشراء"] != null) dgvProducts.Columns["سعر الشراء"].Visible = false;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void HighlightOutOfStockRows()
        {
            foreach (DataGridViewRow row in dgvProducts.Rows)
            {
                if (row.Cells["الكمية"].Value == null) continue;
                if (Convert.ToInt32(row.Cells["الكمية"].Value) <= 0)
                {
                    row.DefaultCellStyle.ForeColor = UIHelpers.ColorRed;
                    row.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                }
            }
        }

        // بيجهّز فورم "إضافة منتج جديد" فاضي وجاهز للكتابة - مستخدم من كارت "إضافة منتج" السريع في الداشبورد
        public void FocusAddProductEntry()
        {
            cmbInventoryOperationType.SelectedIndex = 0;
            ClearInputs();
            txtBarcode.Focus();
        }

        // بيدوّر على منتج بالباركود ده - في جدول الإكسسوارات الأول، ولو مش لاقيه يبدّل
        // لتاب الأجهزة ويدور في جدول الـIMEI - وبيحدده ويجيب تفاصيله في الخانات
        // (مستخدمة من نتيجة البحث الشامل Ctrl+F)
        public void HighlightProduct(string barcode)
        {
            foreach (DataGridViewRow row in dgvProducts.Rows)
            {
                if (row.Cells["الباركود"].Value?.ToString() == barcode)
                {
                    cmbInventoryOperationType.SelectedIndex = 0;
                    dgvProducts.ClearSelection();
                    row.Selected = true;
                    dgvProducts.CurrentCell = row.Cells[0];
                    dgvProducts.FirstDisplayedScrollingRowIndex = row.Index;
                    DgvProducts_CellClick(dgvProducts, new DataGridViewCellEventArgs(0, row.Index));
                    return;
                }
            }

            foreach (DataGridViewRow row in dgvImeiUnits.Rows)
            {
                if (row.Cells["الباركود"].Value?.ToString() == barcode)
                {
                    cmbInventoryOperationType.SelectedIndex = 1;
                    dgvImeiUnits.ClearSelection();
                    row.Selected = true;
                    dgvImeiUnits.CurrentCell = row.Cells[0];
                    dgvImeiUnits.FirstDisplayedScrollingRowIndex = row.Index;
                    DgvImeiUnits_CellClick(dgvImeiUnits, new DataGridViewCellEventArgs(0, row.Index));
                    return;
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
                LoadProductImagePreview(txtBarcode.Text);
            }
        }

        // ---------- صورة المنتج: تحميل المعاينة، الرفع، الإزالة ----------
        private void LoadProductImagePreview(string barcode)
        {
            pendingProductImage = null;
            try { pendingProductImage = InventoryRepository.GetProductImage(barcode); }
            catch { /* منتج جديد لسه ملوش صف في القاعدة - تجاهل */ }
            RefreshProductImagePreview();
        }

        private void RefreshProductImagePreview()
        {
            if (pendingProductImage != null && pendingProductImage.Length > 0)
            {
                using (var ms = new System.IO.MemoryStream(pendingProductImage))
                    picProductImage.Image = Image.FromStream(ms);
            }
            else
            {
                picProductImage.Image = null;
            }
        }

        private void BtnUploadProductImage_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog() { Filter = "ملفات صور (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp" })
            {
                if (ofd.ShowDialog() != DialogResult.OK) return;
                try
                {
                    byte[] imageBytes = System.IO.File.ReadAllBytes(ofd.FileName);
                    using (var msOriginal = new System.IO.MemoryStream(imageBytes))
                    using (var original = Image.FromStream(msOriginal))
                    {
                        int maxSize = 500;
                        int width, height;
                        if (original.Width > original.Height) { width = maxSize; height = (int)(original.Height * (maxSize / (double)original.Width)); }
                        else { height = maxSize; width = (int)(original.Width * (maxSize / (double)original.Height)); }

                        using (var resized = new Bitmap(original, new Size(width, height)))
                        using (var msResized = new System.IO.MemoryStream())
                        {
                            resized.Save(msResized, System.Drawing.Imaging.ImageFormat.Png);
                            pendingProductImage = msResized.ToArray();
                        }
                    }
                    RefreshProductImagePreview();

                    // لو المنتج موجود بالفعل (مش إضافة جديدة لسه ما اتحفظتش)، نحفظ الصورة فورًا
                    if (!string.IsNullOrEmpty(txtBarcode.Text) && txtBarcode.ReadOnly)
                        InventoryRepository.UpdateProductImage(txtBarcode.Text, pendingProductImage);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("مش قادر يفتح الصورة دي: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnRemoveProductImage_Click(object sender, EventArgs e)
        {
            pendingProductImage = null;
            RefreshProductImagePreview();
            if (!string.IsNullOrEmpty(txtBarcode.Text) && txtBarcode.ReadOnly)
                InventoryRepository.UpdateProductImage(txtBarcode.Text, null);
        }

        private void BtnDownloadTemplate_Click(object sender, EventArgs e)
        {
            using SaveFileDialog dlg = new SaveFileDialog
            {
                Filter = "ملف إكسل (*.xlsx)|*.xlsx",
                FileName = "قالب_استيراد_منتجات.xlsx"
            };
            if (dlg.ShowDialog(this.FindForm()) != DialogResult.OK) return;

            try
            {
                ExcelImportHelper.GenerateProductTemplate(dlg.FileName);
                MessageBox.Show("تم إنشاء القالب بنجاح. املا الأعمدة (باركود، اسم المنتج، سعر الشراء، سعر البيع، الكمية) وارجع استورد الملف.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnImportExcel_Click(object sender, EventArgs e)
        {
            using OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "ملف إكسل (*.xlsx)|*.xlsx"
            };
            if (dlg.ShowDialog(this.FindForm()) != DialogResult.OK) return;

            try
            {
                var result = ExcelImportHelper.ImportProducts(dlg.FileName, AuthManager.CurrentUsername);
                LoadProductsData();

                string summary = $"تم استيراد {result.SuccessCount} منتج بنجاح.";
                if (result.SkippedDuplicateBarcodes.Count > 0)
                    summary += $"\nاتخطّى {result.SkippedDuplicateBarcodes.Count} صف باركود موجود بالفعل: {string.Join(", ", result.SkippedDuplicateBarcodes.Take(10))}{(result.SkippedDuplicateBarcodes.Count > 10 ? " ..." : "")}";
                if (result.RowErrors.Count > 0)
                    summary += $"\n\nأخطاء ({result.RowErrors.Count}):\n{string.Join("\n", result.RowErrors.Take(15))}{(result.RowErrors.Count > 15 ? "\n..." : "")}";

                MessageBox.Show(summary, "نتيجة الاستيراد", MessageBoxButtons.OK,
                    result.RowErrors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
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

            try
            {
                AppServices.CoreEngine.Execute(new AddAccessoryProductCommand { Barcode = txtBarcode.Text, ProductName = txtProductName.Text, CostPrice = costPrice, SalePrice = salePrice, Quantity = quantity, PerformedBy = AuthManager.CurrentUsername });
                if (pendingProductImage != null)
                    InventoryRepository.UpdateProductImage(txtBarcode.Text, pendingProductImage);
                LoadProductsData();
                ClearInputs();
                MessageBox.Show("تم إضافة المنتج بنجاح!", "نجاح", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
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

            try
            {
                AppServices.CoreEngine.Execute(new UpdateAccessoryProductCommand { Barcode = txtBarcode.Text, ProductName = txtProductName.Text, CostPrice = costPrice, SalePrice = salePrice, Quantity = quantity, PerformedBy = AuthManager.CurrentUsername });
                LoadProductsData();
                btnSaveUpdate.Enabled = false;
                MessageBox.Show("تم تعديل المنتج!", "نجاح", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
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
            pendingProductImage = null;
            RefreshProductImagePreview();
            txtBarcode.Focus();
        }

        private void BtnDeleteProduct_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtBarcode.Text)) return;
            if (MessageBox.Show("حذف المنتج؟", "تحذير", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                try { AppServices.CoreEngine.Execute(new DeleteProductCommand { Barcode = txtBarcode.Text, PerformedBy = AuthManager.CurrentUsername }); LoadProductsData(); ClearInputs(); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        // ==========================================================================
        // بانل "الأجهزة والسريالات" - موبايلات بيتباعوا بـ IMEI فردي لكل جهاز
        // ==========================================================================
        private void BuildDevicesPanel()
        {
            // ---------- كارت البحث والفلترة ----------
            Guna2Panel gbSearch = new Guna2Panel() { Location = new Point(0, 0), Size = new Size(300, 220), FillColor = Color.White, BorderRadius = UIHelpers.CardBorderRadius, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            var badgeSearchTitle = UIHelpers.CreateIconBadge("🔍", UIHelpers.ColorPurple, new Point(20, 13));
            Label lblSearchTitle = new Label() { Text = "بحث وفلترة", Location = new Point(64, 21), AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblSearch = new Label() { Text = "بحث برقم الـIMEI أو اسم المنتج:", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtImeiSearch = new Guna2TextBox() { Location = new Point(20, 70), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251), BorderColor = Color.FromArgb(225, 228, 235), BorderThickness = 1 };
            txtImeiSearch.TextChanged += (s, e) => LoadImeiUnitsGrid();

            Label lblStatusFilter = new Label() { Text = "الحالة:", Location = new Point(20, 108), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbImeiStatusFilter = new Guna2ComboBox() { Location = new Point(20, 128), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbImeiStatusFilter.Items.AddRange(new string[] { "الكل", "متاح في المخزون", "مباع" });
            cmbImeiStatusFilter.SelectedIndex = 0;
            cmbImeiStatusFilter.SelectedIndexChanged += (s, e) => LoadImeiUnitsGrid();

            Guna2Button btnRefreshImei = new Guna2Button() { Text = "تحديث 🔄", Location = new Point(20, 168), Width = 260, Height = 34, FillColor = UIHelpers.ColorAccentPrimary, BorderRadius = 9 };
            btnRefreshImei.Click += (s, e) => LoadImeiUnitsGrid();
            UIHelpers.MakePill(btnRefreshImei);

            gbSearch.Controls.AddRange(new Control[] { badgeSearchTitle, lblSearchTitle, lblSearch, txtImeiSearch, lblStatusFilter, cmbImeiStatusFilter, btnRefreshImei });

            // ---------- كارت إضافة جهاز يدوي / تعديل سعر موديل محدد ----------
            Guna2Panel gbQuickAdd = new Guna2Panel() { Location = new Point(0, 235), Size = new Size(300, 545), FillColor = Color.White, BorderRadius = UIHelpers.CardBorderRadius, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            var badgeQaTitle = UIHelpers.CreateIconBadge("➕", UIHelpers.ColorGreen, new Point(20, 13));
            Label lblQaTitle = new Label() { Text = "إضافة جهاز / تعديل سعر موديل", Location = new Point(64, 19), AutoSize = true, Size = new Size(216, 34), Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblQaBarcode = new Label() { Text = "الباركود (سيبها فاضية لو مفيش):", Location = new Point(20, 55), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQaBarcode = new Guna2TextBox() { Location = new Point(20, 75), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251), BorderColor = Color.FromArgb(225, 228, 235), BorderThickness = 1 };

            Label lblQaName = new Label() { Text = "اسم المنتج:", Location = new Point(20, 113), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQaProductName = new Guna2TextBox() { Location = new Point(20, 133), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251), BorderColor = Color.FromArgb(225, 228, 235), BorderThickness = 1 };

            Label lblQaImei = new Label() { Text = "رقم الـIMEI (لجهاز جديد بس):", Location = new Point(20, 171), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQaImei = new Guna2TextBox() { Location = new Point(20, 191), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251), BorderColor = Color.FromArgb(225, 228, 235), BorderThickness = 1 };

            Label lblQaCost = new Label() { Text = "سعر الشراء (التكلفة):", Location = new Point(20, 229), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQaCostPrice = new Guna2TextBox() { Location = new Point(20, 249), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251), BorderColor = Color.FromArgb(225, 228, 235), BorderThickness = 1 };

            Label lblQaSale = new Label() { Text = "سعر البيع للجمهور:", Location = new Point(20, 287), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtQaSalePrice = new Guna2TextBox() { Location = new Point(20, 307), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251), BorderColor = Color.FromArgb(225, 228, 235), BorderThickness = 1 };

            Label lblQaNote = new Label()
            {
                Text = "لإضافة جهاز جديد: املأ كل الخانات وبينها الـIMEI. لتعديل سعر موديل موجود: دوس على أي جهاز من الجدول (هيملأ الخانات، اسيب الـIMEI فاضي) وادوس \"حفظ تعديل السعر\".",
                Location = new Point(20, 345),
                Size = new Size(260, 60),
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = Color.FromArgb(85, 92, 102)
            };

            btnQuickAddDevice = new Guna2Button() { Text = "إضافة جهاز جديد ✅", Location = new Point(20, 410), Width = 260, Height = 38, FillColor = UIHelpers.ColorGreen, BorderRadius = 10 };
            btnQuickAddDevice.Click += BtnQuickAddDevice_Click;
            UIHelpers.MakePill(btnQuickAddDevice);

            btnSaveModelPriceEdit = new Guna2Button() { Text = "حفظ تعديل سعر الموديل 💾", Location = new Point(20, 456), Width = 260, Height = 36, FillColor = UIHelpers.ColorGreen, Font = new Font("Segoe UI", 9, FontStyle.Bold), BorderRadius = 10 };
            btnSaveModelPriceEdit.Click += BtnSaveModelPriceEdit_Click;
            UIHelpers.MakePill(btnSaveModelPriceEdit);

            gbQuickAdd.Controls.AddRange(new Control[] { badgeQaTitle, lblQaTitle, lblQaBarcode, txtQaBarcode, lblQaName, txtQaProductName, lblQaImei, txtQaImei, lblQaCost, txtQaCostPrice, lblQaSale, txtQaSalePrice, lblQaNote, btnQuickAddDevice, btnSaveModelPriceEdit });

            // ---------- كارت الجدول ----------
            AnchorStyles gridFillAnchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Guna2Panel pnlGridCard = new Guna2Panel() { Location = new Point(320, 0), Size = new Size(780, 780), Anchor = gridFillAnchor, FillColor = Color.White, BorderRadius = UIHelpers.CardBorderRadius, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            var badgeDevGridTitle = UIHelpers.CreateIconBadge("📱", UIHelpers.ColorOrange, new Point(20, 13));
            Label lblGridTitle = new Label() { Text = "كل الأجهزة المسجّلة بأرقام الـIMEI", Location = new Point(64, 21), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = ColorPrimary };

            dgvImeiUnits = new DataGridView() { Location = new Point(20, 50), Size = new Size(740, 715), Anchor = gridFillAnchor, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvImeiUnits.CellClick += DgvImeiUnits_CellClick;
            StyleDataGridView(dgvImeiUnits);

            pnlGridCard.Controls.AddRange(new Control[] { badgeDevGridTitle, lblGridTitle, dgvImeiUnits });

            pnlDevicesOps.Controls.AddRange(new Control[] { gbSearch, gbQuickAdd, pnlGridCard });
        }

        // ==========================================================================
        // تحميل جدول الأجهزة مع الفلترة والبحث
        // ==========================================================================
        private void LoadImeiUnitsGrid()
        {
            if (dgvImeiUnits == null) return;

            string statusFilter = cmbImeiStatusFilter?.SelectedItem?.ToString() ?? "الكل";
            string search = txtImeiSearch?.Text?.Trim() ?? "";

            try
            {
                dgvImeiUnits.DataSource = InventoryRepository.GetImeiUnits(statusFilter, search);
                if (dgvImeiUnits.Columns["الباركود"] != null) dgvImeiUnits.Columns["الباركود"].Visible = false;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
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

            try
            {
                ProductForPurchase product = InventoryRepository.GetProductByBarcode(barcode);
                if (product == null) return;

                selectedDeviceBarcode = barcode;
                txtQaBarcode.Text = barcode;
                txtQaProductName.Text = product.ProductName;
                txtQaCostPrice.Text = product.Price.ToString();
                txtQaSalePrice.Text = product.SalePrice.ToString();
                txtQaImei.Clear();
                txtQaBarcode.ReadOnly = true;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
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

            try
            {
                AppServices.CoreEngine.Execute(new UpdateModelPriceCommand { Barcode = selectedDeviceBarcode, ProductName = txtQaProductName.Text, CostPrice = costPrice, SalePrice = salePrice, PerformedBy = AuthManager.CurrentUsername });
                MessageBox.Show("تم تعديل سعر الموديل بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadImeiUnitsGrid();
                ClearDeviceInputs();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
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

            try
            {
                AppServices.CoreEngine.Execute(new AddDeviceCommand { Barcode = barcode, ProductName = txtQaProductName.Text.Trim(), CostPrice = costPrice, SalePrice = salePrice, Imei = imei, PerformedBy = AuthManager.CurrentUsername });
            }
            catch (DuplicateImeiException ex)
            {
                MessageBox.Show(ex.Message, "رقم مكرر", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show("حصل خطأ: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
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
