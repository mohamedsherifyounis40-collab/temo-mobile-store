using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using Microsoft.Data.Sqlite;
using TemoStore.Core.Commands;
using TemoStore.Core.Exceptions;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // ReportsPageControl: نسخة مستقلة من شاشة "التقارير والأرباح" الموجودة في
    // Form1.cs (CreateReportsDesign) - بما فيها نظام إقفال اليوم المالي الكامل،
    // وكمان "سجل إقفال الأيام" و"كشف حساب الوسائل" كسويتش إضافي فوق.
    // ==========================================================================
    public partial class ReportsPageControl : UserControl
    {
        private static readonly Color ColorPrimary = UIHelpers.ColorPrimary;
        private static readonly Color ColorSuccess = UIHelpers.ColorSuccess;
        private static readonly Color ColorDanger = UIHelpers.ColorDanger;
        private static readonly Color ColorWarning = UIHelpers.ColorWarning;
        private static readonly Color ColorNeutral = UIHelpers.ColorNeutral;
        private static readonly Color ColorBackground = UIHelpers.ColorBackground;

        private static readonly string[] AllPaymentMethods = UIHelpers.PaymentMethods;

        private Label lblTotalSalesVal, lblTotalCapitalVal, lblTotalExpensesVal, lblTotalNetProfitVal;
        private DateTimePicker dtpFrom, dtpTo;
        private Guna2Button btnFilterReports, btnCloseDay;
        private Label lblOpeningBalanceVal, lblExpectedClosingVal;
        private DataGridView dgvClosureSummary;
        private DataGridView dgvReports;

        private Guna2ComboBox cmbReportsViewType;
        private Panel pnlSummary, pnlClosuresLog, pnlStatements;

        // ---------- سجل إقفال الأيام ----------
        private DataGridView dgvClosuresLog, dgvClosureDetails;

        // ---------- كشف حساب الوسائل ----------
        private Guna2ComboBox cmbStatementMethod;
        private DateTimePicker dtpStatementFrom, dtpStatementTo;
        private Label lblStatementTotalInVal, lblStatementTotalOutVal, lblStatementNetVal, lblStatementCurrentBalanceVal;
        private DataGridView dgvStatement;

        public ReportsPageControl()
        {
            this.Dock = DockStyle.Fill;
            this.Size = new Size(1150, 1150); // مقاس مبدئي واقعي قبل بناء الشاشة، عشان حسابات Anchor متبقاش غلط (راجع نفس التعليق في SalesPageControl)
            this.AutoScroll = true;
            this.BackColor = ColorBackground;

            BuildUI();
            CalculateBusinessMetrics();
            RefreshClosureSummary();
        }

        // ==========================================================================
        // الهيكل العام: سويتش فوق + 3 بانلات يتبادلوا الظهور
        // ==========================================================================
        private void BuildUI()
        {
            Label lblViewType = new Label() { Text = "العرض:", Location = new Point(20, 20), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            cmbReportsViewType = new Guna2ComboBox() { Location = new Point(90, 17), Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbReportsViewType.Items.AddRange(new string[] { "الملخص المالي وإقفال اليوم 📊", "سجل إقفال الأيام 🔒", "كشف حساب الوسائل 📋" });
            cmbReportsViewType.SelectedIndexChanged += CmbReportsViewType_SelectedIndexChanged;

            AnchorStyles fillAnchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlSummary = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 1050), AutoScroll = true, Anchor = fillAnchor };
            pnlClosuresLog = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 680), Anchor = fillAnchor };
            pnlStatements = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 710), Anchor = fillAnchor };

            BuildSummaryPanel();
            BuildClosuresLogPanel();
            BuildStatementsPanel();

            pnlClosuresLog.Visible = false;
            pnlStatements.Visible = false;

            this.Controls.AddRange(new Control[] { lblViewType, cmbReportsViewType, pnlSummary, pnlClosuresLog, pnlStatements });

            cmbReportsViewType.SelectedIndex = 0;
        }

        private void CmbReportsViewType_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = cmbReportsViewType.SelectedIndex;
            pnlSummary.Visible = idx == 0;
            pnlClosuresLog.Visible = idx == 1;
            pnlStatements.Visible = idx == 2;

            if (idx == 1) LoadClosuresLog();
            else if (idx == 2) ShowStatement(false);
        }

        // ==========================================================================
        // بانل الملخص المالي وإقفال اليوم - نفس تصميم Form1.cs (CreateReportsDesign) بالظبط
        // ==========================================================================
        private void BuildSummaryPanel()
        {
            Guna2Panel gbSummary = new Guna2Panel() { Location = new Point(0, 0), Size = new Size(280, 300), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblSummaryTitle = new Label() { Text = "📊 خلاصة حركة المال", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblTotalSales = new Label() { Text = "إجمالي المبيعات (الدرج):", Location = new Point(20, 55), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            lblTotalSalesVal = new Label() { Text = "0.00 ج.م", Location = new Point(20, 75), AutoSize = true, ForeColor = Color.FromArgb(41, 98, 189), Font = new Font("Segoe UI", 12, FontStyle.Bold) };

            Label lblTotalCapital = new Label() { Text = "تكلفة البضاعة المباعة:", Location = new Point(20, 115), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            lblTotalCapitalVal = new Label() { Text = "0.00 ج.م", Location = new Point(20, 135), AutoSize = true, ForeColor = Color.FromArgb(39, 174, 96), Font = new Font("Segoe UI", 12, FontStyle.Bold) };

            Label lblTotalExpenses = new Label() { Text = "إجمالي المصروفات العمومية:", Location = new Point(20, 175), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            lblTotalExpensesVal = new Label() { Text = "0.00 ج.م", Location = new Point(20, 195), AutoSize = true, ForeColor = ColorDanger, Font = new Font("Segoe UI", 12, FontStyle.Bold) };

            Guna2Panel pnlNetProfit = new Guna2Panel() { Location = new Point(20, 235), Size = new Size(240, 50), FillColor = Color.FromArgb(240, 245, 255), BorderRadius = 10 };
            Label lblTotalNetProfit = new Label() { Text = "الصافي الفعلي في جيبك 💰", Location = new Point(12, 6), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(85, 92, 102) };
            lblTotalNetProfitVal = new Label() { Text = "0.00 ج.م", Location = new Point(12, 22), AutoSize = true, ForeColor = ColorPrimary, Font = new Font("Segoe UI", 13, FontStyle.Bold) };
            pnlNetProfit.Controls.AddRange(new Control[] { lblTotalNetProfit, lblTotalNetProfitVal });

            gbSummary.Controls.AddRange(new Control[] { lblSummaryTitle, lblTotalSales, lblTotalSalesVal, lblTotalCapital, lblTotalCapitalVal, lblTotalExpenses, lblTotalExpensesVal, pnlNetProfit });

            Guna2Panel gbFilter = new Guna2Panel() { Location = new Point(0, 315), Size = new Size(280, 210), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblFilterTitle = new Label() { Text = "🔍 فلترة بالفترة الزمنية", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };
            Label lblFrom = new Label() { Text = "من تاريخ:", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            dtpFrom = new DateTimePicker() { Location = new Point(20, 70), Width = 240, Format = DateTimePickerFormat.Short };

            Label lblTo = new Label() { Text = "إلى تاريخ:", Location = new Point(20, 108), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            dtpTo = new DateTimePicker() { Location = new Point(20, 128), Width = 240, Format = DateTimePickerFormat.Short };

            btnFilterReports = new Guna2Button() { Text = "تطبيق الفلتر 🔍", Location = new Point(20, 166), Width = 240, Height = 34, FillColor = ColorSuccess, BorderRadius = 9 };
            btnFilterReports.Click += BtnFilterReports_Click;
            gbFilter.Controls.AddRange(new Control[] { lblFilterTitle, lblFrom, dtpFrom, lblTo, dtpTo, btnFilterReports });

            Guna2Panel gbClosure = new Guna2Panel() { Location = new Point(0, 530), Size = new Size(280, 400), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblClosureTitle = new Label() { Text = "🔒 إقفال اليوم", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };
            Label lblOpeningBalance = new Label() { Text = "الرصيد الافتتاحي (نقدي):", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            lblOpeningBalanceVal = new Label() { Text = "0.00 ج.م", Location = new Point(20, 68), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblExpectedClosing = new Label() { Text = "الرصيد الختامي المتوقع:", Location = new Point(20, 95), AutoSize = true, Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(85, 92, 102) };
            lblExpectedClosingVal = new Label() { Text = "0.00 ج.م", Location = new Point(20, 113), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ColorWarning };

            Label lblAllMethodsTitle = new Label() { Text = "الرصيد الافتتاحي لكل الوسائل (دبل كليك للتعديل):", Location = new Point(20, 145), Size = new Size(240, 24), Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), ForeColor = Color.FromArgb(85, 92, 102) };

            dgvClosureSummary = new DataGridView() { Location = new Point(20, 170), Size = new Size(240, 220), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvClosureSummary.CellDoubleClick += DgvClosureSummary_CellDoubleClick;
            dgvClosureSummary.DefaultCellStyle.Font = new Font("Segoe UI", 8F);
            dgvClosureSummary.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            StyleDataGridView(dgvClosureSummary);

            gbClosure.Controls.AddRange(new Control[] { lblClosureTitle, lblOpeningBalance, lblOpeningBalanceVal, lblExpectedClosing, lblExpectedClosingVal, lblAllMethodsTitle, dgvClosureSummary });

            Guna2Panel pnlActions = new Guna2Panel() { Location = new Point(0, 940), Size = new Size(280, 165), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Guna2Button btnRefreshReports = new Guna2Button() { Text = "عرض الكل وتحديث الحسابات 🔄", Location = new Point(20, 15), Width = 240, Height = 38, FillColor = ColorPrimary, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), BorderRadius = 10 };
            btnRefreshReports.Click += (s, e) => { CalculateBusinessMetrics(); RefreshClosureSummary(); };

            btnCloseDay = new Guna2Button() { Text = "إقفال اليوم 🔒", Location = new Point(20, 60), Width = 240, Height = 38, FillColor = ColorDanger, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), BorderRadius = 10 };
            btnCloseDay.Click += BtnCloseDay_Click;

            Guna2Button btnReopenDay = new Guna2Button() { Text = "فتح اليوم تاني 🔓", Location = new Point(20, 105), Width = 240, Height = 34, FillColor = ColorNeutral, ForeColor = ColorPrimary, BorderRadius = 10 };
            btnReopenDay.Click += BtnReopenDay_Click;

            pnlActions.Controls.AddRange(new Control[] { btnRefreshReports, btnCloseDay, btnReopenDay });

            AnchorStyles gridFillAnchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Guna2Panel pnlGridCard = new Guna2Panel() { Location = new Point(300, 0), Size = new Size(800, 1000), Anchor = gridFillAnchor, FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblTableTitle = new Label() { Text = "📈 سجل الأرباح التفصيلي", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = ColorPrimary };

            Guna2Button btnPrintReport = new Guna2Button() { Text = "طباعة / PDF 🖨️", Location = new Point(640, 12), Width = 140, Height = 30, FillColor = ColorNeutral, ForeColor = ColorPrimary, BorderRadius = 8 };
            btnPrintReport.Click += (s, e) => GridPrintHelper.Print(dgvReports, "سجل الأرباح التفصيلي", this.FindForm());

            dgvReports = new DataGridView() { Location = new Point(20, 50), Size = new Size(760, 935), Anchor = gridFillAnchor, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            StyleDataGridView(dgvReports);
            pnlGridCard.Controls.AddRange(new Control[] { lblTableTitle, btnPrintReport, dgvReports });

            pnlSummary.Controls.AddRange(new Control[] { gbSummary, gbFilter, gbClosure, pnlActions, pnlGridCard });
        }

        // ==========================================================================
        // بانل سجل إقفال الأيام
        // ==========================================================================
        private void BuildClosuresLogPanel()
        {
            // الكارتين دول واقفين فوق بعض عموديًا، فبنمدّهم عرضًا بس (Top|Left|Right) من غير Bottom عشان محدش يغطي التاني
            AnchorStyles widenAnchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Guna2Panel pnlLogCard = new Guna2Panel() { Location = new Point(0, 0), Size = new Size(1100, 385), Anchor = widenAnchor, FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblLogTitle = new Label() { Text = "🔒 سجل الأيام المُقفلة بالتفصيل", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = ColorPrimary };

            dgvClosuresLog = new DataGridView() { Location = new Point(20, 50), Size = new Size(1060, 320), Anchor = widenAnchor, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells, ReadOnly = true, AllowUserToAddRows = false };
            dgvClosuresLog.CellClick += DgvClosuresLog_CellClick;
            dgvClosuresLog.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F);
            dgvClosuresLog.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            dgvClosuresLog.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvClosuresLog.RowTemplate.Height = 38;
            StyleDataGridView(dgvClosuresLog);
            pnlLogCard.Controls.AddRange(new Control[] { lblLogTitle, dgvClosuresLog });

            Guna2Panel pnlDetailsCard = new Guna2Panel() { Location = new Point(0, 400), Size = new Size(1100, 260), Anchor = widenAnchor, FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblClosureDetailsTitle = new Label() { Text = "🧾 تفاصيل فئات الكاش الفعلي لليوم المحدد (دوس على أي صف فوق)", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ColorPrimary };

            dgvClosureDetails = new DataGridView() { Location = new Point(20, 50), Size = new Size(1060, 195), Anchor = widenAnchor, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells, ReadOnly = true, AllowUserToAddRows = false };
            dgvClosureDetails.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F);
            dgvClosureDetails.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            dgvClosureDetails.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvClosureDetails.RowTemplate.Height = 38;
            StyleDataGridView(dgvClosureDetails);
            pnlDetailsCard.Controls.AddRange(new Control[] { lblClosureDetailsTitle, dgvClosureDetails });

            pnlClosuresLog.Controls.AddRange(new Control[] { pnlLogCard, pnlDetailsCard });
        }

        private void LoadClosuresLog()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] {
                new DataColumn("Id"), new DataColumn("التاريخ"), new DataColumn("الوسيلة"), new DataColumn("رصيد افتتاحي"),
                new DataColumn("إجمالي وارد"), new DataColumn("إجمالي منصرف"),
                new DataColumn("ختامي متوقع"), new DataColumn("ختامي فعلي"), new DataColumn("الفرق"), new DataColumn("وقت الإقفال")
            });

            string query = "SELECT * FROM DailyClosures ORDER BY ClosureDate DESC, PaymentMethod ASC";
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
                            {
                                dt.Rows.Add(reader["Id"], reader["ClosureDate"], reader["PaymentMethod"], reader["OpeningBalance"],
                                    reader["TotalIn"], reader["TotalOut"], reader["ExpectedClosingBalance"],
                                    reader["ActualClosingBalance"], reader["Difference"], reader["ClosedAt"]);
                            }
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
            dgvClosuresLog.DataSource = dt;
            if (dgvClosuresLog.Columns["Id"] != null) dgvClosuresLog.Columns["Id"].Visible = false;
        }

        private void DgvClosuresLog_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            int closureId = Convert.ToInt32(dgvClosuresLog.Rows[e.RowIndex].Cells["Id"].Value);

            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("الفئة"), new DataColumn("العدد"), new DataColumn("الإجمالي") });

            string query = "SELECT DenominationValue, DenominationCount, LineTotal FROM CashDenominations WHERE ClosureId = @ClosureId ORDER BY DenominationValue DESC";
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ClosureId", closureId);
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                dt.Rows.Add(reader["DenominationValue"], reader["DenominationCount"], reader["LineTotal"]);
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
            dgvClosureDetails.DataSource = dt;
        }

        // ==========================================================================
        // بانل كشف حساب الوسائل
        // ==========================================================================
        private void BuildStatementsPanel()
        {
            Guna2Panel gbFilter2 = new Guna2Panel() { Location = new Point(0, 0), Size = new Size(280, 250), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblFilter2Title = new Label() { Text = "📋 اختيار الوسيلة والفترة", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblMethod = new Label() { Text = "وسيلة الدفع:", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbStatementMethod = new Guna2ComboBox() { Location = new Point(20, 70), Width = 240, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbStatementMethod.Items.AddRange(AllPaymentMethods);
            cmbStatementMethod.SelectedIndex = 0;

            Label lblFrom2 = new Label() { Text = "من تاريخ:", Location = new Point(20, 108), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            dtpStatementFrom = new DateTimePicker() { Location = new Point(20, 128), Width = 240, Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddMonths(-1) };

            Label lblTo2 = new Label() { Text = "إلى تاريخ:", Location = new Point(20, 156), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            dtpStatementTo = new DateTimePicker() { Location = new Point(20, 176), Width = 240, Format = DateTimePickerFormat.Short, Value = DateTime.Now };

            Guna2Button btnShowStatement = new Guna2Button() { Text = "عرض كشف الحساب 📋", Location = new Point(20, 208), Width = 240, Height = 32, FillColor = ColorPrimary, BorderRadius = 9 };
            btnShowStatement.Click += (s, e) => ShowStatement(true);

            gbFilter2.Controls.AddRange(new Control[] { lblFilter2Title, lblMethod, cmbStatementMethod, lblFrom2, dtpStatementFrom, lblTo2, dtpStatementTo, btnShowStatement });

            Guna2Panel gbSummary2 = new Guna2Panel() { Location = new Point(0, 265), Size = new Size(280, 250), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblSummary2Title = new Label() { Text = "📊 ملخص الفترة المعروضة", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblIn = new Label() { Text = "إجمالي الوارد:", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            lblStatementTotalInVal = new Label() { Text = "0.00 ج.م", Location = new Point(20, 70), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = ColorSuccess };

            Label lblOut = new Label() { Text = "إجمالي المنصرف:", Location = new Point(20, 100), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            lblStatementTotalOutVal = new Label() { Text = "0.00 ج.م", Location = new Point(20, 120), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = ColorDanger };

            Label lblNet = new Label() { Text = "صافي حركة الفترة:", Location = new Point(20, 150), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            lblStatementNetVal = new Label() { Text = "0.00 ج.م", Location = new Point(20, 170), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblCurrentBalance = new Label() { Text = "الرصيد الحالي المسجل فعليًا:", Location = new Point(20, 200), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            lblStatementCurrentBalanceVal = new Label() { Text = "0.00 ج.م", Location = new Point(20, 220), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = ColorWarning };

            gbSummary2.Controls.AddRange(new Control[] { lblSummary2Title, lblIn, lblStatementTotalInVal, lblOut, lblStatementTotalOutVal, lblNet, lblStatementNetVal, lblCurrentBalance, lblStatementCurrentBalanceVal });

            Guna2Button btnShowAllStatement = new Guna2Button() { Text = "عرض كل الفترات 🔄", Location = new Point(0, 530), Width = 280, Height = 34, FillColor = ColorNeutral, ForeColor = ColorPrimary, BorderRadius = 9 };
            btnShowAllStatement.Click += (s, e) => ShowStatement(false);

            AnchorStyles gridFillAnchor2 = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Guna2Panel pnlStatementGridCard = new Guna2Panel() { Location = new Point(300, 0), Size = new Size(800, 700), Anchor = gridFillAnchor2, FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblGridTitle2 = new Label() { Text = "📄 كشف الحساب بالتفصيل", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = ColorPrimary };

            dgvStatement = new DataGridView() { Location = new Point(20, 50), Size = new Size(760, 635), Anchor = gridFillAnchor2, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells, ReadOnly = true, AllowUserToAddRows = false };
            dgvStatement.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dgvStatement.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvStatement.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            StyleDataGridView(dgvStatement);
            pnlStatementGridCard.Controls.AddRange(new Control[] { lblGridTitle2, dgvStatement });

            pnlStatements.Controls.AddRange(new Control[] { gbFilter2, gbSummary2, btnShowAllStatement, pnlStatementGridCard });
        }

        // بيعرض كشف حساب الوسيلة المختارة. useDateFilter بتحدد نطبق فلتر التاريخ ولا نعرض كل الفترات
        private void ShowStatement(bool useDateFilter)
        {
            if (cmbStatementMethod.SelectedItem == null) return;
            string method = cmbStatementMethod.SelectedItem.ToString();

            string fromDate = useDateFilter ? dtpStatementFrom.Value.ToString("yyyy-MM-dd") + " 00:00:00" : "0000-01-01 00:00:00";
            string toDate = useDateFilter ? dtpStatementTo.Value.ToString("yyyy-MM-dd") + " 23:59:59" : "9999-12-31 23:59:59";

            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] {
                new DataColumn("التاريخ والوقت"), new DataColumn("نوع الحركة"), new DataColumn("البيان"),
                new DataColumn("وارد"), new DataColumn("منصرف"), new DataColumn("الرصيد بعد الحركة")
            });

            string query;
            if (method == "نقدي")
            {
                query = @"
                    SELECT SaleDate AS OpDate, 'بيع' AS OpType, ProductName AS Details, Total AS Amount, 1 AS IsIn FROM Sales
                    WHERE SaleDate BETWEEN @From AND @To
                    UNION ALL
                    SELECT E.ExpenseDate AS OpDate, 'مصروف' AS OpType, A.AccountName AS Details, E.Amount AS Amount, 0 AS IsIn
                        FROM Expenses E INNER JOIN AccountsTree A ON E.AccountCode = A.AccountCode
                        WHERE E.ExpenseDate BETWEEN @From AND @To
                    UNION ALL
                    SELECT CM.CreatedAt AS OpDate, CM.MovementType AS OpType,
                           (CASE WHEN AC.AccountName IS NOT NULL THEN AC.AccountName ELSE 'بدون تصنيف' END ||
                            CASE WHEN CM.Description IS NOT NULL AND CM.Description <> '' THEN ' - ' || CM.Description ELSE '' END) AS Details,
                           CM.Amount, CASE WHEN CM.MovementType = 'قبض' THEN 1 ELSE 0 END AS IsIn
                        FROM CashMovements CM LEFT JOIN AccountsTree AC ON CM.AccountCode = AC.AccountCode
                        WHERE CM.PaymentMethod = 'نقدي' AND CM.CreatedAt BETWEEN @From AND @To
                    ORDER BY OpDate ASC";
            }
            else
            {
                query = @"
                    SELECT CM.CreatedAt AS OpDate, CM.MovementType AS OpType,
                           (CASE WHEN AC.AccountName IS NOT NULL THEN AC.AccountName ELSE 'بدون تصنيف' END ||
                            CASE WHEN CM.Description IS NOT NULL AND CM.Description <> '' THEN ' - ' || CM.Description ELSE '' END) AS Details,
                           CM.Amount, CASE WHEN CM.MovementType = 'قبض' THEN 1 ELSE 0 END AS IsIn
                        FROM CashMovements CM LEFT JOIN AccountsTree AC ON CM.AccountCode = AC.AccountCode
                        WHERE CM.PaymentMethod = @Method AND CM.CreatedAt BETWEEN @From AND @To
                    ORDER BY OpDate ASC";
            }

            decimal totalIn = 0, totalOut = 0, runningBalance = 0;

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDate);
                    cmd.Parameters.AddWithValue("@To", toDate);
                    if (method != "نقدي") cmd.Parameters.AddWithValue("@Method", method);

                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                bool isIn = Convert.ToInt32(reader["IsIn"]) == 1;
                                decimal amount = Convert.ToDecimal(reader["Amount"]);
                                runningBalance += isIn ? amount : -amount;
                                if (isIn) totalIn += amount; else totalOut += amount;

                                dt.Rows.Add(
                                    reader["OpDate"],
                                    reader["OpType"],
                                    reader["Details"],
                                    isIn ? amount.ToString("N2") : "",
                                    !isIn ? amount.ToString("N2") : "",
                                    runningBalance.ToString("N2")
                                );
                            }
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }

                decimal currentBalance = 0;
                using (SqliteCommand cmdBal = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn))
                {
                    cmdBal.Parameters.AddWithValue("@Method", method);
                    try
                    {
                        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
                        var res = cmdBal.ExecuteScalar();
                        if (res != null) currentBalance = Convert.ToDecimal(res);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
                lblStatementCurrentBalanceVal.Text = currentBalance.ToString("N2") + " ج.م";
            }

            dgvStatement.DataSource = dt;
            lblStatementTotalInVal.Text = totalIn.ToString("N2") + " ج.م";
            lblStatementTotalOutVal.Text = totalOut.ToString("N2") + " ج.م";
            lblStatementNetVal.Text = (totalIn - totalOut).ToString("N2") + " ج.م";
        }

        // ==========================================================================
        // نفس تنسيق الجداول المستخدم في كل شاشات Form1.cs
        // ==========================================================================
        private void StyleDataGridView(DataGridView dgv) => UIHelpers.StyleDataGridView(dgv);

        // ==========================================================================
        // فحص هل تاريخ النهاردة تم إقفاله بالفعل
        // ==========================================================================
        private bool IsTodayClosed()
        {
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM DailyClosures WHERE ClosureDate = @Date AND PaymentMethod = 'نقدي'", conn))
                {
                    cmd.Parameters.AddWithValue("@Date", dateStr);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        // ==========================================================================
        // حساب الملخص المالي الإجمالي (كل الوقت)
        // ==========================================================================
        private void CalculateBusinessMetrics()
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                try
                {
                    conn.Open();
                    decimal totalSales = 0, totalCost = 0;
                    string salesQuery = "SELECT SUM(Total) as TotalSales, SUM(CostPrice * QuantitySold) as TotalCost FROM Sales";
                    using (SqliteCommand cmd = new SqliteCommand(salesQuery, conn))
                    {
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                totalSales = reader["TotalSales"] != DBNull.Value ? Convert.ToDecimal(reader["TotalSales"]) : 0;
                                totalCost = reader["TotalCost"] != DBNull.Value ? Convert.ToDecimal(reader["TotalCost"]) : 0;
                            }
                        }
                    }

                    decimal totalExpenses = 0;
                    string expenseQuery = "SELECT SUM(Amount) FROM Expenses";
                    using (SqliteCommand cmdExp = new SqliteCommand(expenseQuery, conn))
                    {
                        var res = cmdExp.ExecuteScalar();
                        totalExpenses = res != DBNull.Value ? Convert.ToDecimal(res) : 0;
                    }

                    decimal netProfit = (totalSales - totalCost) - totalExpenses;

                    lblTotalSalesVal.Text = totalSales.ToString("N2") + " ج.م";
                    lblTotalCapitalVal.Text = totalCost.ToString("N2") + " ج.م";
                    lblTotalExpensesVal.Text = totalExpenses.ToString("N2") + " ج.م";
                    lblTotalNetProfitVal.Text = netProfit.ToString("N2") + " ج.م";
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        // ==========================================================================
        // فلترة السجل التفصيلي بفترة زمنية معينة
        // ==========================================================================
        private void BtnFilterReports_Click(object sender, EventArgs e)
        {
            string fromDate = dtpFrom.Value.ToString("yyyy-MM-dd") + " 00:00:00";
            string toDate = dtpTo.Value.ToString("yyyy-MM-dd") + " 23:59:59";

            DataTable dtReports = new DataTable();
            dtReports.Columns.AddRange(new DataColumn[] {
                new DataColumn("رقم الحركة"), new DataColumn("المنتج"), new DataColumn("سعر الشراء"),
                new DataColumn("سعر البيع"), new DataColumn("الكمية"), new DataColumn("الربح الصافي"), new DataColumn("التاريخ والوقت ⏰")
            });

            string query = "SELECT SaleID, ProductName, CostPrice, Price, QuantitySold, Total, SaleDate FROM Sales WHERE SaleDate BETWEEN @From AND @To ORDER BY SaleID ASC";

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDate);
                    cmd.Parameters.AddWithValue("@To", toDate);
                    try
                    {
                        conn.Open();
                        decimal totalSales = 0, totalCost = 0;
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                decimal cost = Convert.ToDecimal(reader["CostPrice"]);
                                decimal sell = Convert.ToDecimal(reader["Price"]);
                                int qty = Convert.ToInt32(reader["QuantitySold"]);
                                decimal total = Convert.ToDecimal(reader["Total"]);

                                totalSales += total;
                                totalCost += (cost * qty);

                                dtReports.Rows.Add(reader["SaleID"], reader["ProductName"], cost, sell, qty, (sell - cost) * qty, reader["SaleDate"]);
                            }
                        }
                        dgvReports.DataSource = dtReports;

                        decimal totalExpenses = 0;
                        string expQuery = "SELECT SUM(Amount) FROM Expenses WHERE ExpenseDate BETWEEN @From AND @To";
                        using (SqliteCommand cmdExp = new SqliteCommand(expQuery, conn))
                        {
                            cmdExp.Parameters.AddWithValue("@From", fromDate);
                            cmdExp.Parameters.AddWithValue("@To", toDate);
                            var res = cmdExp.ExecuteScalar();
                            totalExpenses = res != DBNull.Value ? Convert.ToDecimal(res) : 0;
                        }

                        decimal netProfit = (totalSales - totalCost) - totalExpenses;

                        lblTotalSalesVal.Text = totalSales.ToString("N2") + " ج.م";
                        lblTotalCapitalVal.Text = totalCost.ToString("N2") + " ج.م";
                        lblTotalExpensesVal.Text = totalExpenses.ToString("N2") + " ج.م";
                        lblTotalNetProfitVal.Text = netProfit.ToString("N2") + " ج.م";
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        // ==========================================================================
        // حساب الرصيد الافتتاحي لوسيلة معينة (من آخر إقفال فعلي، أو من الرصيد الحالي لو مفيش إقفالات قبل كده)
        // ==========================================================================
        private decimal GetMethodOpeningBalance(SqliteConnection conn, string method)
        {
            decimal opening = 0;
            using (SqliteCommand cmd = new SqliteCommand("SELECT ActualClosingBalance FROM DailyClosures WHERE PaymentMethod = @Method ORDER BY ClosureDate DESC LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@Method", method);
                var result = cmd.ExecuteScalar();
                if (result != null) opening = Convert.ToDecimal(result);
                else
                {
                    using (SqliteCommand cmdBal = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn))
                    {
                        cmdBal.Parameters.AddWithValue("@Method", method);
                        var balRes = cmdBal.ExecuteScalar();
                        if (balRes != null) opening = Convert.ToDecimal(balRes);
                    }
                }
            }
            return opening;
        }

        // ==========================================================================
        // حساب ملخص الإقفال المتوقع لليوم الحالي لكل وسيلة دفع مع بعض
        // ==========================================================================
        private Dictionary<string, (decimal opening, decimal totalIn, decimal totalOut, decimal expectedClosing)> GetAllMethodsClosureSummary()
        {
            var result = new Dictionary<string, (decimal opening, decimal totalIn, decimal totalOut, decimal expectedClosing)>();
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                // مبيعات الكاش متسجلة بالفعل كحركة "قبض" في CashMovements عن طريق محرك
                // الخزنة (CashDrawerEngine.Credit)، فبتتجمع أوتوماتيك في methodIn تحت
                // وسيلة الدفع الصح - مفيش داعي لجمعها تاني من جدول Sales هنا (كان ده
                // بيضاعف قيمة مبيعات النهار على وسيلة "نقدي" ويضخّم الرصيد المتوقع).
                decimal expensesTotal = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM Expenses WHERE ExpenseDate LIKE @Today", conn))
                {
                    cmd.Parameters.AddWithValue("@Today", today + "%");
                    var r = cmd.ExecuteScalar();
                    expensesTotal = (r != null && r != DBNull.Value) ? Convert.ToDecimal(r) : 0;
                }

                foreach (string method in AllPaymentMethods)
                {
                    decimal opening = GetMethodOpeningBalance(conn, method);

                    decimal methodIn = 0, methodOut = 0;
                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE PaymentMethod = @Method AND MovementType = 'قبض' AND MovementDate = @Today", conn))
                    {
                        cmd.Parameters.AddWithValue("@Method", method);
                        cmd.Parameters.AddWithValue("@Today", today);
                        var r = cmd.ExecuteScalar();
                        methodIn = (r != null && r != DBNull.Value) ? Convert.ToDecimal(r) : 0;
                    }
                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE PaymentMethod = @Method AND MovementType = 'صرف' AND MovementDate = @Today", conn))
                    {
                        cmd.Parameters.AddWithValue("@Method", method);
                        cmd.Parameters.AddWithValue("@Today", today);
                        var r = cmd.ExecuteScalar();
                        methodOut = (r != null && r != DBNull.Value) ? Convert.ToDecimal(r) : 0;
                    }

                    decimal totalIn = methodIn;
                    decimal totalOut = methodOut + (method == "نقدي" ? expensesTotal : 0);
                    decimal expectedClosing = opening + totalIn - totalOut;

                    result[method] = (opening, totalIn, totalOut, expectedClosing);
                }
            }

            return result;
        }

        // ==========================================================================
        // تحديث كروت وجدول الإقفال
        // ==========================================================================
        private void RefreshClosureSummary()
        {
            if (IsTodayClosed())
            {
                lblOpeningBalanceVal.Text = "تم إقفال اليوم بالفعل ✅";
                lblExpectedClosingVal.Text = "--";
                btnCloseDay.Enabled = false;

                DataTable dtClosed = new DataTable();
                dtClosed.Columns.AddRange(new DataColumn[] { new DataColumn("الوسيلة"), new DataColumn("افتتاحي"), new DataColumn("ختامي فعلي") });
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
                {
                    conn.Open();
                    using (SqliteCommand cmd = new SqliteCommand("SELECT PaymentMethod, OpeningBalance, ActualClosingBalance FROM DailyClosures WHERE ClosureDate = @Date ORDER BY PaymentMethod", conn))
                    {
                        cmd.Parameters.AddWithValue("@Date", today);
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                dtClosed.Rows.Add(reader["PaymentMethod"], Convert.ToDecimal(reader["OpeningBalance"]).ToString("N2"), Convert.ToDecimal(reader["ActualClosingBalance"]).ToString("N2"));
                        }
                    }
                }
                dgvClosureSummary.DataSource = dtClosed;
                return;
            }

            btnCloseDay.Enabled = true;
            var summaries = GetAllMethodsClosureSummary();
            var cashSummary = summaries["نقدي"];
            lblOpeningBalanceVal.Text = cashSummary.opening.ToString("N2") + " ج.م";
            lblExpectedClosingVal.Text = cashSummary.expectedClosing.ToString("N2") + " ج.م";

            DataTable dtSummary = new DataTable();
            dtSummary.Columns.AddRange(new DataColumn[] { new DataColumn("الوسيلة"), new DataColumn("افتتاحي"), new DataColumn("ختامي متوقع") });
            foreach (var method in AllPaymentMethods)
            {
                var s = summaries[method];
                dtSummary.Rows.Add(method, s.opening.ToString("N2"), s.expectedClosing.ToString("N2"));
            }
            dgvClosureSummary.DataSource = dtSummary;
        }

        // ==========================================================================
        // تعديل الرصيد الافتتاحي يدويًا بدبل كليك (قبل الإقفال بس)
        // ==========================================================================
        private void DgvClosureSummary_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            if (IsTodayClosed())
            {
                MessageBox.Show("اليوم مقفول بالفعل. لو عايز تعدّل الأرصدة، افتح اليوم تاني الأول من زرار \"فتح اليوم تاني\".", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string method = dgvClosureSummary.Rows[e.RowIndex].Cells["الوسيلة"].Value.ToString();
            string currentOpeningStr = dgvClosureSummary.Rows[e.RowIndex].Cells["افتتاحي"].Value.ToString();

            string input = ShowInputDialog($"الرصيد الافتتاحي الجديد لـ \"{method}\":", currentOpeningStr);
            if (input == null) return;

            if (!decimal.TryParse(input, out decimal newBalance) || newBalance < 0)
            {
                MessageBox.Show("من فضلك أدخل رصيد صحيح وموجب.", "قيمة غير صحيحة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal currentBalance;
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn))
                {
                    cmd.Parameters.AddWithValue("@Method", method);
                    var res = cmd.ExecuteScalar();
                    currentBalance = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                }
            }

            decimal delta = newBalance - currentBalance;
            if (delta != 0)
            {
                try
                {
                    // أي تعديل يدوي للرصيد لازم يتسجل كحركة حقيقية (قبض/صرف) على حساب
                    // "عجز وزيادة الخزينة" (5700) بدل تحديث الرصيد مباشرة من غير أثر محاسبي -
                    // نفس مبدأ فرق الإقفال اليومي (BtnCloseDay_Click) - عشان ميفضلش فرق دائم
                    // بين رصيد الخزينة الفعلي وميزان المراجعة المحسوب من دفتر اليومية.
                    AppServices.CoreEngine.Execute(new AddMovementCommand
                    {
                        Type = delta > 0 ? "قبض" : "صرف",
                        Method = method,
                        Amount = Math.Abs(delta),
                        AccountCode = 5700,
                        Description = $"تعديل يدوي لرصيد \"{method}\"",
                        PerformedBy = AuthManager.CurrentUsername
                    });
                }
                catch (InsufficientBalanceException ex)
                {
                    MessageBox.Show(ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("حصل خطأ أثناء تحديث الرصيد: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            MessageBox.Show($"تم تحديث رصيد \"{method}\" بنجاح.\n\nملحوظة: لو الوسيلة دي كان ليها إقفالات قبل كده، الرصيد الافتتاحي هيفضل ياخد من آخر إقفال فعلي مش من هنا.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshClosureSummary();
        }

        // بوب-أب صغير لإدخال قيمة نصية
        private string ShowInputDialog(string prompt, string defaultValue)
        {
            using (Form inputForm = new Form())
            {
                inputForm.Text = "إدخال قيمة";
                inputForm.Size = new Size(340, 160);
                inputForm.StartPosition = FormStartPosition.CenterParent;
                inputForm.RightToLeft = RightToLeft.Yes;
                inputForm.RightToLeftLayout = true;
                inputForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                inputForm.MaximizeBox = false;
                inputForm.MinimizeBox = false;

                Label lbl = new Label() { Text = prompt, Location = new Point(15, 15), Size = new Size(300, 40) };
                TextBox txt = new TextBox() { Text = defaultValue, Location = new Point(15, 60), Width = 300 };

                Guna2Button btnOk = new Guna2Button() { Text = "موافق", Location = new Point(15, 95), Width = 140, Height = 32, FillColor = ColorSuccess };
                Guna2Button btnCancelInput = new Guna2Button() { Text = "إلغاء", Location = new Point(175, 95), Width = 140, Height = 32, FillColor = ColorNeutral, ForeColor = ColorPrimary };

                string resultValue = null;
                btnOk.Click += (s, e) => { resultValue = txt.Text; inputForm.DialogResult = DialogResult.OK; inputForm.Close(); };
                btnCancelInput.Click += (s, e) => { inputForm.DialogResult = DialogResult.Cancel; inputForm.Close(); };

                inputForm.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancelInput });
                inputForm.AcceptButton = btnOk;

                return inputForm.ShowDialog() == DialogResult.OK ? resultValue : null;
            }
        }

        // ==========================================================================
        // فتح فورم عدّ الكاش، وحفظ إقفال اليوم لكل الوسائل - أهم وأخطر دالة في الشاشة
        // ==========================================================================
        private void BtnCloseDay_Click(object sender, EventArgs e)
        {
            if (IsTodayClosed())
            {
                MessageBox.Show("تم إقفال اليوم بالفعل.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var summaries = GetAllMethodsClosureSummary();
            var cashSummary = summaries["نقدي"];

            var otherExpected = new Dictionary<string, decimal>();
            foreach (var method in AllPaymentMethods)
            {
                if (method == "نقدي") continue;
                otherExpected[method] = summaries[method].expectedClosing;
            }

            using (DenominationEntryForm denomForm = new DenominationEntryForm(cashSummary.expectedClosing, otherExpected))
            {
                if (denomForm.ShowDialog() != DialogResult.OK) return;

                decimal actualCash = denomForm.TotalCounted;
                decimal cashDifference = actualCash - cashSummary.expectedClosing;

                StringBuilder message = new StringBuilder();
                message.AppendLine("ملخص إقفال اليوم لكل الوسائل:\n");
                message.AppendLine($"نقدي: متوقع {cashSummary.expectedClosing:N2} | فعلي {actualCash:N2} | الفرق {cashDifference:N2}");
                foreach (var method in AllPaymentMethods)
                {
                    if (method == "نقدي") continue;
                    decimal actual = denomForm.OtherMethodsActual[method];
                    decimal diff = actual - summaries[method].expectedClosing;
                    message.AppendLine($"{method}: متوقع {summaries[method].expectedClosing:N2} | فعلي {actual:N2} | الفرق {diff:N2}");
                }
                message.AppendLine("\nهل تريد تأكيد إقفال اليوم لكل الوسائل؟ لن يمكن التعديل أو الحذف في حركات اليوم بعد الإقفال.");

                if (MessageBox.Show(message.ToString(), "تأكيد إقفال اليوم", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                string today = DateTime.Now.ToString("yyyy-MM-dd");
                try
                {
                    // أي فرق بين المتوقع والفعلي (عجز أو زيادة) لازم يتسجل كحركة قبض/صرف حقيقية
                    // على حساب "عجز وزيادة الخزينة" (5700) عن طريق محرك الحسابات - مش تحديث مباشر
                    // للرصيد - عشان يفضل الرصيد الفعلي متسق مع ميزان المراجعة المحسوب من دفتر
                    // اليومية. ده بيتنفذ الأول قبل أي تسجيل لسجل الإقفال نفسه، عشان لو فشل لأي
                    // سبب (رصيد غير كافي مثلاً)ميتسجلش إقفال ناقص.
                    var adjustmentMovementIds = new Dictionary<string, int?>();
                    foreach (var method in AllPaymentMethods)
                    {
                        decimal actual = method == "نقدي" ? actualCash : denomForm.OtherMethodsActual[method];
                        decimal expected = summaries[method].expectedClosing;
                        decimal delta = actual - expected;

                        if (delta == 0) { adjustmentMovementIds[method] = null; continue; }

                        var result = AppServices.CoreEngine.Execute(new AddMovementCommand
                        {
                            Type = delta > 0 ? "قبض" : "صرف",
                            Method = method,
                            Amount = Math.Abs(delta),
                            AccountCode = 5700,
                            Description = $"فرق إقفال يومي ({today})",
                            PerformedBy = AuthManager.CurrentUsername
                        });
                        adjustmentMovementIds[method] = result.MovementId;
                    }

                    using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
                    {
                        conn.Open();
                        using (var transaction = conn.BeginTransaction())
                        {
                            int cashClosureId = InsertClosureRow(conn, transaction, today, "نقدي", cashSummary, actualCash, adjustmentMovementIds["نقدي"]);

                            foreach (var kvp in denomForm.DenominationCounts)
                            {
                                if (kvp.Value <= 0) continue;
                                using (SqliteCommand cmd = new SqliteCommand(
                                    "INSERT INTO CashDenominations (ClosureId, DenominationValue, DenominationCount, LineTotal) VALUES (@ClosureId, @Value, @Count, @LineTotal)",
                                    conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ClosureId", cashClosureId);
                                    cmd.Parameters.AddWithValue("@Value", kvp.Key);
                                    cmd.Parameters.AddWithValue("@Count", kvp.Value);
                                    cmd.Parameters.AddWithValue("@LineTotal", kvp.Key * kvp.Value);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            foreach (var method in AllPaymentMethods)
                            {
                                if (method == "نقدي") continue;
                                decimal actual = denomForm.OtherMethodsActual[method];
                                InsertClosureRow(conn, transaction, today, method, summaries[method], actual, adjustmentMovementIds[method]);
                            }

                            transaction.Commit();
                        }
                    }

                    MessageBox.Show("تم إقفال اليوم بنجاح لكل الوسائل وتسجيله في السجل. 🔒", "تم الإقفال", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshClosureSummary();
                }
                catch (InsufficientBalanceException ex)
                {
                    MessageBox.Show(ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("حدث خطأ أثناء الإقفال: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private int InsertClosureRow(SqliteConnection conn, SqliteTransaction transaction, string date, string method,
            (decimal opening, decimal totalIn, decimal totalOut, decimal expectedClosing) summary, decimal actual, int? adjustmentMovementId = null)
        {
            decimal difference = actual - summary.expectedClosing;
            string insertClosure = @"INSERT INTO DailyClosures
                (ClosureDate, PaymentMethod, OpeningBalance, TotalIn, TotalOut, ExpectedClosingBalance, ActualClosingBalance, Difference, ClosedAt, AdjustmentMovementId)
                VALUES (@Date, @Method, @Opening, @TotalIn, @TotalOut, @Expected, @Actual, @Diff, @ClosedAt, @AdjustmentMovementId)";

            using (SqliteCommand cmd = new SqliteCommand(insertClosure, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Date", date);
                cmd.Parameters.AddWithValue("@Method", method);
                cmd.Parameters.AddWithValue("@Opening", summary.opening);
                cmd.Parameters.AddWithValue("@TotalIn", summary.totalIn);
                cmd.Parameters.AddWithValue("@TotalOut", summary.totalOut);
                cmd.Parameters.AddWithValue("@Expected", summary.expectedClosing);
                cmd.Parameters.AddWithValue("@Actual", actual);
                cmd.Parameters.AddWithValue("@Diff", difference);
                cmd.Parameters.AddWithValue("@ClosedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@AdjustmentMovementId", (object)adjustmentMovementId ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            int closureId;
            using (SqliteCommand cmdId = new SqliteCommand("SELECT last_insert_rowid();", conn, transaction))
            {
                closureId = Convert.ToInt32(cmdId.ExecuteScalar());
            }
            return closureId;
        }

        // ==========================================================================
        // فتح اليوم تاني (إلغاء الإقفال والرجوع للأرصدة قبله)
        // ==========================================================================
        private void BtnReopenDay_Click(object sender, EventArgs e)
        {
            if (!IsTodayClosed())
            {
                MessageBox.Show("اليوم مش مقفول أصلاً، مفيش داعي تفتحه.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("هل أنت متأكد من فتح اليوم تاني؟\nده هيلغي إقفال اليوم لكل الوسائل ويرجّع كل رصيد للي كان عليه قبل الإقفال مباشرة، وهتقدر تضيف وتعدّل حركات النهاردة تاني.", "تأكيد فتح اليوم", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            string today = DateTime.Now.ToString("yyyy-MM-dd");
            var closureRows = new List<(int id, string method, decimal expected, int? adjustmentMovementId)>();
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT Id, PaymentMethod, ExpectedClosingBalance, AdjustmentMovementId FROM DailyClosures WHERE ClosureDate = @Date", conn))
                {
                    cmd.Parameters.AddWithValue("@Date", today);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            closureRows.Add((Convert.ToInt32(reader["Id"]), reader["PaymentMethod"].ToString(),
                                Convert.ToDecimal(reader["ExpectedClosingBalance"]),
                                reader["AdjustmentMovementId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AdjustmentMovementId"])));
                    }
                }
            }

            if (closureRows.Count == 0)
            {
                MessageBox.Show("لم يتم العثور على إقفال اليوم.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                // إلغاء حركة فرق الإقفال (لو كانت موجودة) بيرجّع الرصيد للي كان عليه ويعكس
                // القيد المحاسبي تلقائيًا (نفس آلية إلغاء أي حركة قبض/صرف عادية في الخزينة)
                foreach (var row in closureRows)
                {
                    if (row.adjustmentMovementId.HasValue)
                        AppServices.CoreEngine.Execute(new CancelMovementCommand { MovementId = row.adjustmentMovementId.Value, PerformedBy = AuthManager.CurrentUsername });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("حدث خطأ أثناء التراجع عن فروق الإقفال: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                foreach (var row in closureRows)
                {
                    using (SqliteCommand cmd = new SqliteCommand("DELETE FROM CashDenominations WHERE ClosureId = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", row.id);
                        cmd.ExecuteNonQuery();
                    }

                    // تصحيح احترازي (يفضل بلا أي أثر لو الإلغاء فوق نجح صح، لأن الرصيد هيبقى
                    // بالفعل مساوي للمتوقع بعد عكس حركة الفرق)
                    using (SqliteCommand cmd = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = @Balance WHERE PaymentMethod = @Method", conn))
                    {
                        cmd.Parameters.AddWithValue("@Balance", row.expected);
                        cmd.Parameters.AddWithValue("@Method", row.method);
                        cmd.ExecuteNonQuery();
                    }
                }

                using (SqliteCommand cmd = new SqliteCommand("DELETE FROM DailyClosures WHERE ClosureDate = @Date", conn))
                {
                    cmd.Parameters.AddWithValue("@Date", today);
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم فتح اليوم تاني بنجاح لكل الوسائل. تقدر تضيف وتعدّل حركات النهاردة عادي دلوقتي.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshClosureSummary();
        }
    }
}
