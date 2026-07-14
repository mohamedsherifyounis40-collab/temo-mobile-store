using System;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using Guna.UI2.WinForms;

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

        // ---------- عناصر الشاشة ----------
        private Guna2TextBox txtSaleBarcode, txtSaleName, txtCustomerPrice, txtSaleQty, txtSaleTotal;
        private Label lblSaleImei;
        private Guna2ComboBox cmbSaleImei, cmbSalePaymentType, cmbSalePaymentMethod, cmbSaleCustomer, cmbInvoicePaperSize;
        private Guna2TextBox txtInvoiceNumber, txtInvoiceCustomer;
        private Guna2Button btnAddToBill, btnPrintInvoice, btnEditSaleMode, btnSaveSaleEdit, btnCancelSale;
        private DataGridView dgvSales;

        private int selectedSaleId = -1;

        public SalesPageControl()
        {
            this.Dock = DockStyle.Fill;
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
            // ---------- كارت شاشة البيع (يسار) ----------
            Guna2Panel pnlCard = new Guna2Panel()
            {
                Location = new Point(20, 20),
                Size = new Size(300, 900),
                FillColor = Color.White,
                BorderRadius = 14,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1
            };

            Label lblCardTitle = new Label()
            {
                Text = "🛒 عملية بيع جديدة",
                Location = new Point(20, 18),
                AutoSize = true,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = ColorPrimary
            };

            Label lblBadgeInvTitle = new Label() { Text = "رقم الفاتورة", Location = new Point(20, 52), AutoSize = true, Font = new Font("Segoe UI", 7.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtInvoiceNumber = new Guna2TextBox() { Location = new Point(20, 70), Width = 80, ReadOnly = true, BorderRadius = 8, FillColor = Color.FromArgb(240, 243, 248), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = ColorPrimary, Text = "-" };

            Label lblBadgeCustTitle = new Label() { Text = "العميل", Location = new Point(110, 52), AutoSize = true, Font = new Font("Segoe UI", 7.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtInvoiceCustomer = new Guna2TextBox() { Location = new Point(110, 70), Width = 170, ReadOnly = true, BorderRadius = 8, FillColor = Color.FromArgb(240, 243, 248), Font = new Font("Segoe UI", 9.5F), ForeColor = ColorPrimary, Text = "عميل نقدي" };

            Label lblBarcode = new Label() { Text = "مسح الباركود (Enter):", Location = new Point(20, 116), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtSaleBarcode = new Guna2TextBox() { Location = new Point(20, 136), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };
            txtSaleBarcode.KeyDown += TxtSaleBarcode_KeyDown;

            Label lblName = new Label() { Text = "اسم المنتج:", Location = new Point(20, 174), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtSaleName = new Guna2TextBox() { Location = new Point(20, 194), Width = 260, ReadOnly = true, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblPrice = new Label() { Text = "سعر البيع (قابل للتعديل):", Location = new Point(20, 232), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtCustomerPrice = new Guna2TextBox() { Location = new Point(20, 252), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };
            txtCustomerPrice.TextChanged += TxtSaleQty_TextChanged;

            Label lblQty = new Label() { Text = "الكمية المطلوبة:", Location = new Point(20, 290), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtSaleQty = new Guna2TextBox() { Location = new Point(20, 310), Width = 260, Text = "1", BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };
            txtSaleQty.TextChanged += TxtSaleQty_TextChanged;

            lblSaleImei = new Label() { Text = "اختار الجهاز (IMEI):", Location = new Point(20, 348), AutoSize = true, Visible = false, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbSaleImei = new Guna2ComboBox() { Location = new Point(20, 368), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, Visible = false, BorderRadius = 8 };

            // كارت الإجمالي - بارز بلون مميز
            Guna2Panel pnlTotal = new Guna2Panel()
            {
                Location = new Point(20, 406),
                Size = new Size(260, 60),
                FillColor = Color.FromArgb(255, 249, 230),
                BorderRadius = 10
            };
            Label lblTotal = new Label() { Text = "إجمالي الحساب", Location = new Point(15, 8), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtSaleTotal = new Guna2TextBox()
            {
                Location = new Point(15, 26),
                Width = 230,
                ReadOnly = true,
                BorderRadius = 6,
                FillColor = Color.FromArgb(255, 249, 230),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = ColorWarning
            };
            pnlTotal.Controls.AddRange(new Control[] { lblTotal, txtSaleTotal });

            Label lblPaymentType = new Label() { Text = "نوع البيع:", Location = new Point(20, 481), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbSalePaymentType = new Guna2ComboBox() { Location = new Point(20, 501), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbSalePaymentType.Items.AddRange(new string[] { "كاش", "آجل" });
            cmbSalePaymentType.SelectedIndex = 0;
            cmbSalePaymentType.SelectedIndexChanged += CmbSalePaymentType_SelectedIndexChanged;

            Label lblSalePaymentMethod = new Label() { Text = "وسيلة الدفع (لو كاش):", Location = new Point(20, 539), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbSalePaymentMethod = new Guna2ComboBox() { Location = new Point(20, 559), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbSalePaymentMethod.Items.AddRange(UIHelpers.PaymentMethods);

            Label lblSaleCustomer = new Label() { Text = "العميل (لازم للآجل):", Location = new Point(20, 597), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbSaleCustomer = new Guna2ComboBox() { Location = new Point(20, 617), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false, BorderRadius = 8 };

            btnAddToBill = new Guna2Button() { Text = "إتمام عملية البيع 🛒", Location = new Point(20, 659), Width = 260, Height = 42, FillColor = ColorWarning, BorderRadius = 10, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            btnAddToBill.Click += BtnAddToBill_Click;

            btnPrintInvoice = new Guna2Button() { Text = "طباعة آخر فاتورة 🖨️", Location = new Point(20, 707), Width = 260, Height = 36, FillColor = ColorNeutral, ForeColor = ColorPrimary, BorderRadius = 10 };
            btnPrintInvoice.Click += BtnPrintInvoice_Click;

            Label lblPaperSize = new Label() { Text = "مقاس الطباعة:", Location = new Point(20, 752), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbInvoicePaperSize = new Guna2ComboBox() { Location = new Point(20, 772), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbInvoicePaperSize.Items.AddRange(new string[] { "80 مم (طابعة حرارية)", "58 مم (طابعة حرارية)", "A4 (طابعة عادية)" });
            cmbInvoicePaperSize.SelectedIndex = 0;

            Guna2Button btnSendWhatsApp = new Guna2Button() { Text = "إرسال آخر فاتورة واتساب 📱", Location = new Point(20, 812), Width = 260, Height = 34, FillColor = Color.FromArgb(37, 211, 102), BorderRadius = 9 };
            btnSendWhatsApp.Click += BtnSendInvoiceWhatsApp_Click;

            Guna2Button btnSavePdf = new Guna2Button() { Text = "حفظ آخر فاتورة PDF 📄", Location = new Point(20, 852), Width = 260, Height = 34, FillColor = Color.FromArgb(192, 57, 43), BorderRadius = 9 };
            btnSavePdf.Click += BtnSaveInvoicePdf_Click;

            pnlCard.Controls.AddRange(new Control[] {
                lblCardTitle, lblBadgeInvTitle, txtInvoiceNumber, lblBadgeCustTitle, txtInvoiceCustomer,
                lblBarcode, txtSaleBarcode, lblName, txtSaleName, lblPrice, txtCustomerPrice,
                lblQty, txtSaleQty, lblSaleImei, cmbSaleImei, pnlTotal, lblPaymentType, cmbSalePaymentType,
                lblSalePaymentMethod, cmbSalePaymentMethod, lblSaleCustomer, cmbSaleCustomer, btnAddToBill, btnPrintInvoice, lblPaperSize, cmbInvoicePaperSize, btnSendWhatsApp, btnSavePdf
            });

            // ---------- كارت إدارة العمليات (تعديل/إلغاء) - تحت كارت البيع مباشرة ----------
            Guna2Panel pnlManage = new Guna2Panel()
            {
                Location = new Point(20, 940),
                Size = new Size(300, 150),
                FillColor = Color.White,
                BorderRadius = 14,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1
            };
            Label lblManageTitle = new Label() { Text = "✏️ تعديل / إلغاء بيع محدد", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ColorPrimary };

            btnEditSaleMode = new Guna2Button() { Text = "تعديل البيع المحدد ✏️", Location = new Point(20, 50), Width = 260, Height = 36, FillColor = ColorPrimary, BorderRadius = 9 };
            btnEditSaleMode.Click += BtnEditSaleMode_Click;

            btnSaveSaleEdit = new Guna2Button() { Text = "حفظ تعديل البيع 💾", Location = new Point(20, 93), Width = 125, Height = 33, FillColor = ColorWarning, Enabled = false, BorderRadius = 9 };
            btnSaveSaleEdit.Click += BtnSaveSaleEdit_Click;

            btnCancelSale = new Guna2Button() { Text = "إلغاء البيع ❌", Location = new Point(155, 93), Width = 125, Height = 33, FillColor = ColorDanger, BorderRadius = 9 };
            btnCancelSale.Click += BtnCancelSale_Click;

            pnlManage.Controls.AddRange(new Control[] { lblManageTitle, btnEditSaleMode, btnSaveSaleEdit, btnCancelSale });

            // ---------- كارت الجدول (يمين) ----------
            Guna2Panel pnlGridCard = new Guna2Panel()
            {
                Location = new Point(340, 20),
                Size = new Size(780, 850),
                FillColor = Color.White,
                BorderRadius = 14,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1
            };
            Label lblGridTitle = new Label() { Text = "📋 سجل المبيعات", Location = new Point(20, 18), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = ColorPrimary };

            dgvSales = new DataGridView() { Location = new Point(20, 55), Size = new Size(740, 775), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvSales.CellClick += DgvSales_CellClick;
            StyleDataGridView(dgvSales);

            pnlGridCard.Controls.AddRange(new Control[] { lblGridTitle, dgvSales });

            this.Controls.AddRange(new Control[] { pnlCard, pnlManage, pnlGridCard });
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
            UIHelpers.LoadStoreSettings(out CurrentStoreName, out CurrentStorePhone, out CurrentStoreAddress, out CurrentStoreLogo);
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
                    ProductForSale product = SalesRepository.GetProductForSale(txtSaleBarcode.Text);
                    if (product == null)
                    {
                        MessageBox.Show("هذا الباركود غير مسجل في المخزن!", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        txtSaleBarcode.Clear();
                        txtSaleBarcode.Focus();
                        return;
                    }

                    if (product.Quantity <= 0)
                    {
                        MessageBox.Show("عذراً، هذا المنتج نفذ من المخزن تماماً!", "نفذت الكمية", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    txtSaleName.Text = product.ProductName;
                    txtCustomerPrice.Text = product.SalePrice.ToString();
                    CalculateTotal();

                    lblSaleImei.Visible = product.IsSerialized;
                    cmbSaleImei.Visible = product.IsSerialized;

                    if (product.IsSerialized)
                    {
                        txtSaleQty.Text = "1";
                        txtSaleQty.ReadOnly = true;
                        LoadAvailableImeisForSale(txtSaleBarcode.Text.Trim());
                        cmbSaleImei.Focus();
                    }
                    else
                    {
                        txtSaleQty.ReadOnly = false;
                        txtSaleQty.Focus();
                    }
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        private void TxtSaleQty_TextChanged(object sender, EventArgs e) { CalculateTotal(); }

        private void CalculateTotal()
        {
            if (decimal.TryParse(txtCustomerPrice.Text, out decimal price) && int.TryParse(txtSaleQty.Text, out int qty))
                txtSaleTotal.Text = (price * qty).ToString();
        }

        private void CmbSalePaymentType_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool isCredit = cmbSalePaymentType.SelectedItem?.ToString() == "آجل";
            cmbSaleCustomer.Enabled = isCredit;
            cmbSalePaymentMethod.Enabled = !isCredit;
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
        // تنفيذ عملية البيع
        // ==========================================================================
        private void BtnAddToBill_Click(object sender, EventArgs e)
        {
            if (IsTodayClosed())
            {
                MessageBox.Show("تم إقفال اليوم بالفعل، لا يمكن تسجيل مبيعات جديدة.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(txtSaleName.Text) || !int.TryParse(txtSaleQty.Text, out int qtySold)) return;

            string selectedImei = null;
            if (lblSaleImei.Visible)
            {
                if (cmbSaleImei.SelectedValue == null)
                {
                    MessageBox.Show("من فضلك اختار الجهاز (IMEI) اللي هيتباع.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                selectedImei = cmbSaleImei.SelectedValue.ToString();
            }

            string paymentType = cmbSalePaymentType.SelectedItem?.ToString() == "آجل" ? "Credit" : "Cash";
            object customerIdParam = DBNull.Value;
            if (paymentType == "Credit")
            {
                if (cmbSaleCustomer.SelectedValue == null)
                {
                    MessageBox.Show("البيع بالآجل لازم يكون له عميل محدد. اختار العميل أو ضيفه الأول من تاب العملاء.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                customerIdParam = Convert.ToInt32(cmbSaleCustomer.SelectedValue);
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

            int dailyInvoiceNumber;
            try
            {
                dailyInvoiceNumber = SalesRepository.AddSale(
                    txtSaleBarcode.Text, txtSaleName.Text, Convert.ToDecimal(txtCustomerPrice.Text), qtySold,
                    Convert.ToDecimal(txtSaleTotal.Text), customerIdParam, paymentType, selectedImei, paymentMethod);
            }
            catch (InsufficientStockException ex)
            {
                MessageBox.Show(ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show("حدث خطأ: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            txtInvoiceNumber.Text = dailyInvoiceNumber.ToString();
            txtInvoiceCustomer.Text = paymentType == "Credit" ? cmbSaleCustomer.Text : "عميل نقدي";

            MessageBox.Show($"تمت عملية البيع بنجاح!\nرقم الفاتورة اليوم: {dailyInvoiceNumber}", "نجاح", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadSalesData();
            ClearPOSInputs();
        }

        // ==========================================================================
        // التعديل / الإلغاء
        // ==========================================================================
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
                SalesRepository.UpdateSaleQuantity(selectedSaleId, newQty);
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
                SalesRepository.CancelSale(selectedSaleId, existing.Barcode, existing.QuantitySold, existing.IMEI);
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
            txtSaleBarcode.Clear(); txtSaleName.Clear(); txtCustomerPrice.Clear(); txtSaleQty.Text = "1"; txtSaleTotal.Clear();
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

            int dailyInvoiceNumber = 0;
            try
            {
                dailyInvoiceNumber = SalesRepository.GetDailyInvoiceNumber(lastSale.SaleId, saleDate);
            }
            catch
            {
                // البيع نفسه اتسجل بالفعل قبل الوصول هنا - لو رقم الفاتورة فشل يتجاب، بنكمل
                // برسالة واتساب من غيره بدل ما نوقف/نبلّغ خطأ على حاجة خلصت وسجلت صح
            }

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

            try
            {
                string folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TemoStore_Invoices");
                if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);

                string fileName = $"فاتورة_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.pdf";
                string filePath = System.IO.Path.Combine(folder, fileName);

                PrintDocument pd = new PrintDocument();
                pd.PrintPage += PrintInvoicePage;
                pd.DefaultPageSettings.PaperSize = GetSelectedInvoicePaperSize();
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
            PrintDocument pd = new PrintDocument();
            pd.PrintPage += new PrintPageEventHandler(PrintInvoicePage);
            pd.DefaultPageSettings.PaperSize = GetSelectedInvoicePaperSize();
            PrintPreviewDialog pdd = new PrintPreviewDialog() { Document = pd };
            pdd.ShowDialog();
        }

        private void PrintInvoicePage(object sender, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            float pageWidth = e.PageBounds.Width;
            float pageHeight = e.PageBounds.Height;
            bool isThermal = pageWidth < 500;

            float margin = isThermal ? 10 : 20;
            float fontHeaderSize = isThermal ? 12 : 16;
            float fontStoreSize = isThermal ? 13 : 18;
            float fontBodySize = isThermal ? 9 : 11.5f;
            float fontSmallSize = isThermal ? 7.5f : 9;

            Color colorPrimary = Color.FromArgb(26, 43, 76);
            Color colorAccent = Color.FromArgb(39, 174, 96);
            Font fontStore = new Font("Arial", fontStoreSize, FontStyle.Bold);
            Font fontHeader = new Font("Arial", fontHeaderSize, FontStyle.Bold);
            Font fontBody = new Font("Arial", fontBodySize, FontStyle.Regular);
            Font fontBodyBold = new Font("Arial", fontBodySize, FontStyle.Bold);
            Font fontSmall = new Font("Arial", fontSmallSize, FontStyle.Regular);
            Font fontWatermark = new Font("Arial", isThermal ? 22 : 46, FontStyle.Bold);
            StringFormat centerFormat = new StringFormat() { Alignment = StringAlignment.Center };

            // ---------- 1) الواتر مارك (اسم المحل خفيف بالخلف، مايلة) ----------
            string watermarkText = string.IsNullOrWhiteSpace(CurrentStoreName) ? "TEMO MOBILE STORE" : CurrentStoreName.ToUpper();
            using (Brush watermarkBrush = new SolidBrush(Color.FromArgb(18, colorPrimary.R, colorPrimary.G, colorPrimary.B)))
            {
                var state = g.Save();
                g.TranslateTransform(pageWidth / 2, pageHeight / 2);
                g.RotateTransform(-35);
                SizeF wmSize = g.MeasureString(watermarkText, fontWatermark);
                g.DrawString(watermarkText, fontWatermark, watermarkBrush, -wmSize.Width / 2, -wmSize.Height / 2);
                g.Restore(state);
            }

            // ---------- 2) إطار خارجي للفاتورة كلها ----------
            float frameMargin = margin - 5 > 2 ? margin - 5 : 2;
            using (Pen framePen = new Pen(Color.FromArgb(210, 214, 224), 1.2f))
            {
                g.DrawRectangle(framePen, frameMargin, frameMargin, pageWidth - frameMargin * 2, pageHeight - frameMargin * 2);
            }

            float yPos = margin + 6;

            // ---------- 3) الشعار واسم المحل ----------
            if (CurrentStoreLogo != null && CurrentStoreLogo.Length > 0)
            {
                float logoSize = isThermal ? 42 : 58;
                using (var ms = new System.IO.MemoryStream(CurrentStoreLogo))
                using (var logoImg = Image.FromStream(ms))
                {
                    g.DrawImage(logoImg, (pageWidth - logoSize) / 2, yPos, logoSize, logoSize);
                }
                yPos += logoSize + 6;
            }

            g.DrawString(string.IsNullOrWhiteSpace(CurrentStoreName) ? "Temo Mobile Store" : CurrentStoreName, fontStore, new SolidBrush(colorPrimary), new RectangleF(0, yPos, pageWidth, 26), centerFormat);
            yPos += isThermal ? 22 : 30;

            // خط مزدوج تحت اسم المحل (لمسة شياكة بسيطة)
            using (Pen accentPen = new Pen(colorAccent, 1.6f))
                g.DrawLine(accentPen, pageWidth / 2 - 35, yPos, pageWidth / 2 + 35, yPos);
            using (Pen thinPen = new Pen(Color.FromArgb(200, 205, 215), 0.8f))
                g.DrawLine(thinPen, pageWidth / 2 - 55, yPos + 3, pageWidth / 2 + 55, yPos + 3);
            yPos += isThermal ? 14 : 18;

            g.DrawString("فاتورة مبيعات", fontHeader, new SolidBrush(Color.Black), new RectangleF(0, yPos, pageWidth, 24), centerFormat);
            yPos += isThermal ? 20 : 26;

            if (!string.IsNullOrWhiteSpace(CurrentStorePhone))
            { g.DrawString($"تليفون: {CurrentStorePhone}", fontSmall, Brushes.DimGray, new RectangleF(0, yPos, pageWidth, 16), centerFormat); yPos += 15; }
            if (!string.IsNullOrWhiteSpace(CurrentStoreAddress))
            { g.DrawString($"العنوان: {CurrentStoreAddress}", fontSmall, Brushes.DimGray, new RectangleF(0, yPos, pageWidth, 16), centerFormat); yPos += 15; }

            yPos += 8;
            DrawDashedLine(g, margin, pageWidth - margin, yPos);
            yPos += isThermal ? 12 : 16;

            string productName = null; int quantitySold = 0; decimal total = 0; string saleDate = null; int dailyInvoiceNumber = 0;

            try
            {
                var lastSale = SalesRepository.GetLastSale();
                productName = lastSale.ProductName;
                quantitySold = lastSale.QuantitySold;
                total = lastSale.Total;
                saleDate = lastSale.SaleDate;

                if (lastSale.SaleId > 0)
                    dailyInvoiceNumber = SalesRepository.GetDailyInvoiceNumber(lastSale.SaleId, saleDate);
            }
            catch (Exception ex) { MessageBox.Show("حدث خطأ أثناء تجهيز الفاتورة: " + ex.Message); }

            g.DrawString($"رقم الفاتورة: {dailyInvoiceNumber}", fontSmall, Brushes.DimGray, margin, yPos);
            yPos += isThermal ? 16 : 20;
            g.DrawString($"التاريخ: {saleDate ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}", fontSmall, Brushes.DimGray, margin, yPos);
            yPos += isThermal ? 20 : 24;

            if (productName != null)
            {
                // ---------- سطر المنتج بأسلوب "اسم ...... سعر" (خط منقط رابط) ----------
                g.DrawString(productName, fontBodyBold, Brushes.Black, margin, yPos);
                string qtyText = $"× {quantitySold}";
                SizeF qtySizeMeasure = g.MeasureString(qtyText, fontBody);
                g.DrawString(qtyText, fontBody, Brushes.DimGray, pageWidth - margin - qtySizeMeasure.Width, yPos + 2);
                yPos += isThermal ? 24 : 30;

                DrawDashedLine(g, margin, pageWidth - margin, yPos);
                yPos += isThermal ? 14 : 18;

                // ---------- صندوق الإجمالي البارز ----------
                float boxHeight = isThermal ? 34 : 42;
                RectangleF totalBox = new RectangleF(margin, yPos, pageWidth - margin * 2, boxHeight);
                using (Brush totalBg = new SolidBrush(Color.FromArgb(236, 247, 240)))
                    g.FillRectangle(totalBg, totalBox);
                using (Pen totalBorder = new Pen(colorAccent, 1.2f))
                    g.DrawRectangle(totalBorder, totalBox.X, totalBox.Y, totalBox.Width, totalBox.Height);

                g.DrawString("الإجمالي المستحق", fontSmall, Brushes.DimGray, margin + 10, yPos + (isThermal ? 5 : 7));
                string totalText = $"{total:N2} ج.م";
                Font fontTotal = new Font("Arial", isThermal ? 14 : 18, FontStyle.Bold);
                SizeF totalSize = g.MeasureString(totalText, fontTotal);
                g.DrawString(totalText, fontTotal, new SolidBrush(colorAccent), pageWidth - margin - 10 - totalSize.Width, yPos + (isThermal ? 3 : 5));

                yPos += boxHeight + (isThermal ? 14 : 18);
            }
            else
            {
                g.DrawString("لا توجد عمليات بيع مسجلة بعد.", fontBody, Brushes.Red, margin, yPos);
                yPos += isThermal ? 20 : 26;
            }

            DrawDashedLine(g, margin, pageWidth - margin, yPos);
            yPos += isThermal ? 14 : 18;

            g.DrawString("★ شكراً لتعاملكم معنا ★", fontSmall, new SolidBrush(colorPrimary), new RectangleF(0, yPos, pageWidth, 20), centerFormat);
            yPos += 16;
            g.DrawString("نتمنى لكم يومًا سعيدًا 🌟", fontSmall, Brushes.DimGray, new RectangleF(0, yPos, pageWidth, 20), centerFormat);
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
