using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using TemoStore.Core.Commands;
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
                var result = AppServices.CoreEngine.Execute(new CreateSaleCommand
                {
                    Barcode = txtSaleBarcode.Text,
                    ProductName = txtSaleName.Text,
                    UnitPrice = Convert.ToDecimal(txtCustomerPrice.Text),
                    Quantity = qtySold,
                    Total = Convert.ToDecimal(txtSaleTotal.Text),
                    CustomerId = customerIdParam == DBNull.Value ? (int?)null : Convert.ToInt32(customerIdParam),
                    PaymentType = paymentType,
                    PaymentMethod = paymentMethod,
                    Imei = selectedImei,
                    PerformedBy = AuthManager.CurrentUsername
                });
                dailyInvoiceNumber = result.DailyInvoiceNumber;
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

            ReceiptData receipt = LoadReceiptForPrinting();
            if (receipt == null) return;

            try
            {
                string folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TemoStore_Invoices");
                if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);

                string fileName = $"فاتورة_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.pdf";
                string filePath = System.IO.Path.Combine(folder, fileName);

                PrintDocument pd = new PrintDocument();
                pd.PrintPage += (s, ev) => RenderReceiptPage(ev, receipt);
                pd.DefaultPageSettings.PaperSize = BuildReceiptPaperSize(receipt);
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
            ReceiptData receipt = LoadReceiptForPrinting();
            if (receipt == null) return;

            PrintDocument pd = new PrintDocument();
            pd.PrintPage += (s, ev) => RenderReceiptPage(ev, receipt);
            pd.DefaultPageSettings.PaperSize = BuildReceiptPaperSize(receipt);
            PrintPreviewDialog pdd = new PrintPreviewDialog() { Document = pd };
            pdd.ShowDialog();
        }

        // بيجيب بيانات آخر عملية بيع كاملة (مرة واحدة) عشان نفس البيانات المستخدمة في حساب
        // ارتفاع الصفحة الديناميكي هي بالظبط اللي هتترسم - من غير خطر إن بيع جديد يتسجل
        // في نفس اللحظة بين حساب الارتفاع والطباعة الفعلية
        private ReceiptData LoadReceiptForPrinting()
        {
            ReceiptData receipt;
            try { receipt = SalesRepository.GetLastSaleForReceipt(); }
            catch (Exception ex)
            {
                MessageBox.Show("حدث خطأ أثناء تجهيز الفاتورة: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            if (receipt == null)
            {
                MessageBox.Show("لا توجد عمليات بيع مسجلة بعد.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            return receipt;
        }

        // بيبني مقاس الورقة، وللطابعات الحرارية (80/58مم) بيحسب الارتفاع المطلوب فعليًا حسب
        // محتوى الفاتورة (مفيش IMEI؟ عميل نقدي؟ فمفيش أسطر فاضية) بدل ارتفاع ثابت ضخم بيسيب
        // مسافة بيضا في آخر كل إيصال
        private PaperSize BuildReceiptPaperSize(ReceiptData receipt)
        {
            PaperSize paperSize = GetSelectedInvoicePaperSize();
            if (paperSize.Width < 500)
            {
                using (var tempBmp = new Bitmap(1, 1))
                using (var measureG = Graphics.FromImage(tempBmp))
                {
                    float contentHeight = RenderReceipt(measureG, paperSize.Width, true, receipt);
                    paperSize.Height = (int)Math.Ceiling(contentHeight) + 20;
                }
            }
            return paperSize;
        }

        private void RenderReceiptPage(PrintPageEventArgs e, ReceiptData receipt)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            RenderReceipt(g, e.PageBounds.Width, false, receipt);
        }

        // ==========================================================================
        // رسم/قياس الإيصال - نفس الدالة تستخدم مرتين: مرة "قياس بس" (measureOnly=true،
        // بترجع الارتفاع المطلوب من غير ما ترسم حاجة فعليًا) قبل الطباعة عشان نظبط مقاس
        // الورقة، ومرة تانية "رسم فعلي" وقت الطباعة الحقيقية - بنفس المنطق بالظبط، فمفيش
        // احتمال إن القياس يختلف عن الرسم الفعلي.
        // ==========================================================================
        private float RenderReceipt(Graphics g, float pageWidth, bool measureOnly, ReceiptData receipt)
        {
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

            // 8) كارت الصنف - بيتكرر لكل صنف في الفاتورة (النظام الحالي بيسجل صنف واحد بالظبط
            // لكل عملية بيع، فالحلقة دي هتلف مرة واحدة بس - جاهزة تلقائيًا لو تعدد الأصناف
            // اتضاف مستقبلًا من غير ما يتغير شكل الطباعة)
            var items = new List<ReceiptData> { receipt };
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

            // 9) الإجماليات (خصم=0 دايمًا حاليًا - النظام مفيهوش خاصية خصم على مستوى البيع)
            decimal subtotal = receipt.Total;
            decimal discount = 0m;
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
