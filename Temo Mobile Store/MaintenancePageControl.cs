using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // MaintenancePageControl: نسخة مستقلة تمامًا من شاشة "الصيانة" الموجودة في
    // Form1.cs (CreateMaintenanceDesign). نفس مبدأ الشاشات السابقة.
    // ==========================================================================
    public partial class MaintenancePageControl : UserControl
    {
        private static readonly Color ColorPrimary = UIHelpers.ColorPrimary;
        private static readonly Color ColorSuccess = UIHelpers.ColorSuccess;
        private static readonly Color ColorBackground = UIHelpers.ColorBackground;

        private static readonly string[] MaintenanceStatuses = { "مستلم", "جاري الفحص", "جاهز للتسليم", "تم التسليم", "ملغي" };

        private Guna2TextBox txtMaintCustomerName, txtMaintCustomerPhone, txtMaintDeviceInfo, txtMaintIssueDescription, txtMaintEstimatedCost, txtMaintActualCost;
        private Guna2ComboBox cmbMaintStatusUpdate, cmbMaintPaymentMethod, cmbMaintStatusFilter;
        private Guna2Button btnSaveStatus, btnDeliverDevice;
        private DataGridView dgvMaintenanceTickets;

        private int selectedTicketId = -1;
        private string selectedTicketCustomerName = null;
        private string selectedTicketCustomerPhone = null;

        public MaintenancePageControl()
        {
            this.Dock = DockStyle.Fill;
            this.AutoScroll = true;
            this.BackColor = ColorBackground;

            BuildUI();
            LoadMaintenanceGrid();
            ApplyEmployeeRestrictionsIfNeeded();
        }

        // ==========================================================================
        // لو المستخدم موظف عادي: يقدر يستلم جهاز جديد بس، مايقدرش يعدّل الحالة أو يسلّم/يحصّل
        // ==========================================================================
        private void ApplyEmployeeRestrictionsIfNeeded()
        {
            if (AuthManager.IsAdmin) return;

            btnSaveStatus.Enabled = false;
            btnDeliverDevice.Enabled = false;
        }

        private void BuildUI()
        {
            Guna2Panel gbReceive = new Guna2Panel() { Location = new Point(20, 20), Size = new Size(300, 405), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblReceiveTitle = new Label() { Text = "🔧 استلام جهاز جديد للصيانة", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblMCName = new Label() { Text = "اسم العميل:", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtMaintCustomerName = new Guna2TextBox() { Location = new Point(20, 70), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblMCPhone = new Label() { Text = "رقم التليفون:", Location = new Point(20, 108), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtMaintCustomerPhone = new Guna2TextBox() { Location = new Point(20, 128), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblMDevice = new Label() { Text = "الجهاز (الموديل):", Location = new Point(20, 166), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtMaintDeviceInfo = new Guna2TextBox() { Location = new Point(20, 186), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblMIssue = new Label() { Text = "العطل / الشكوى:", Location = new Point(20, 224), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtMaintIssueDescription = new Guna2TextBox() { Location = new Point(20, 244), Width = 260, Height = 55, Multiline = true, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblMEst = new Label() { Text = "التكلفة التقديرية:", Location = new Point(20, 308), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtMaintEstimatedCost = new Guna2TextBox() { Location = new Point(20, 328), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Guna2Button btnReceiveDevice = new Guna2Button() { Text = "استلام الجهاز ✅", Location = new Point(20, 365), Width = 260, Height = 36, FillColor = ColorSuccess, BorderRadius = 10 };
            btnReceiveDevice.Click += BtnReceiveDevice_Click;

            gbReceive.Controls.AddRange(new Control[] { lblReceiveTitle, lblMCName, txtMaintCustomerName, lblMCPhone, txtMaintCustomerPhone, lblMDevice, txtMaintDeviceInfo, lblMIssue, txtMaintIssueDescription, lblMEst, txtMaintEstimatedCost, btnReceiveDevice });

            Guna2Panel gbUpdate = new Guna2Panel() { Location = new Point(20, 440), Size = new Size(300, 185), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblUpdateTitle = new Label() { Text = "🔄 تحديث حالة التذكرة", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };
            cmbMaintStatusUpdate = new Guna2ComboBox() { Location = new Point(20, 50), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbMaintStatusUpdate.Items.AddRange(MaintenanceStatuses);

            btnSaveStatus = new Guna2Button() { Text = "حفظ الحالة 💾", Location = new Point(20, 92), Width = 260, Height = 34, FillColor = ColorPrimary, BorderRadius = 9 };
            btnSaveStatus.Click += BtnSaveMaintenanceStatus_Click;

            Guna2Button btnNotifyWhatsApp = new Guna2Button() { Text = "إشعار العميل واتساب 📱", Location = new Point(20, 134), Width = 260, Height = 34, FillColor = Color.FromArgb(37, 211, 102), BorderRadius = 9 };
            btnNotifyWhatsApp.Click += BtnNotifyWhatsApp_Click;

            gbUpdate.Controls.AddRange(new Control[] { lblUpdateTitle, cmbMaintStatusUpdate, btnSaveStatus, btnNotifyWhatsApp });

            Guna2Panel gbDeliver = new Guna2Panel() { Location = new Point(20, 640), Size = new Size(300, 210), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblDeliverTitle = new Label() { Text = "💵 تسليم الجهاز وتحصيل الأجرة", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };
            Label lblActualCost = new Label() { Text = "الأجرة الفعلية المطلوبة:", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtMaintActualCost = new Guna2TextBox() { Location = new Point(20, 70), Width = 260, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblMaintMethod = new Label() { Text = "وسيلة التحصيل:", Location = new Point(20, 108), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbMaintPaymentMethod = new Guna2ComboBox() { Location = new Point(20, 128), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbMaintPaymentMethod.Items.AddRange(UIHelpers.PaymentMethods);

            btnDeliverDevice = new Guna2Button() { Text = "تسليم وتحصيل الأجرة ✅", Location = new Point(20, 165), Width = 260, Height = 36, FillColor = ColorSuccess, BorderRadius = 10 };
            btnDeliverDevice.Click += BtnDeliverMaintenanceDevice_Click;
            gbDeliver.Controls.AddRange(new Control[] { lblDeliverTitle, lblActualCost, txtMaintActualCost, lblMaintMethod, cmbMaintPaymentMethod, btnDeliverDevice });

            Guna2Panel pnlGridCard = new Guna2Panel() { Location = new Point(340, 20), Size = new Size(780, 785), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblGridTitle = new Label() { Text = "📋 تذاكر الصيانة", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblFilterTitle = new Label() { Text = "فلترة حسب الحالة:", Location = new Point(300, 20), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbMaintStatusFilter = new Guna2ComboBox() { Location = new Point(430, 15), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbMaintStatusFilter.Items.Add("الكل");
            cmbMaintStatusFilter.Items.AddRange(MaintenanceStatuses);
            cmbMaintStatusFilter.SelectedIndex = 0;
            cmbMaintStatusFilter.SelectedIndexChanged += (s, e) => LoadMaintenanceGrid();

            dgvMaintenanceTickets = new DataGridView() { Location = new Point(20, 55), Size = new Size(740, 710), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvMaintenanceTickets.CellClick += DgvMaintenanceTickets_CellClick;
            StyleDataGridView(dgvMaintenanceTickets);

            pnlGridCard.Controls.AddRange(new Control[] { lblGridTitle, lblFilterTitle, cmbMaintStatusFilter, dgvMaintenanceTickets });

            this.Controls.AddRange(new Control[] { gbReceive, gbUpdate, gbDeliver, pnlGridCard });
        }

        private void StyleDataGridView(DataGridView dgv) => UIHelpers.StyleDataGridView(dgv);

        private void LoadMaintenanceGrid()
        {
            if (dgvMaintenanceTickets == null) return;

            string statusFilter = cmbMaintStatusFilter?.SelectedItem?.ToString() ?? "الكل";
            try
            {
                dgvMaintenanceTickets.DataSource = MaintenanceRepository.GetMaintenanceTickets(statusFilter);
                if (dgvMaintenanceTickets.Columns["TicketId"] != null) dgvMaintenanceTickets.Columns["TicketId"].Visible = false;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void DgvMaintenanceTickets_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvMaintenanceTickets.Rows[e.RowIndex];
            selectedTicketId = Convert.ToInt32(row.Cells["TicketId"].Value);
            selectedTicketCustomerName = row.Cells["العميل"].Value?.ToString();
            selectedTicketCustomerPhone = row.Cells["التليفون"].Value?.ToString();
            string currentStatus = row.Cells["الحالة"].Value.ToString();
            cmbMaintStatusUpdate.SelectedItem = currentStatus;

            string estCostStr = row.Cells["التقديري"].Value?.ToString();
            if (!string.IsNullOrEmpty(estCostStr)) txtMaintActualCost.Text = estCostStr;
        }

        private void BtnReceiveDevice_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtMaintCustomerName.Text) || string.IsNullOrWhiteSpace(txtMaintDeviceInfo.Text))
            {
                MessageBox.Show("من فضلك أدخل اسم العميل والجهاز على الأقل.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal.TryParse(txtMaintEstimatedCost.Text, out decimal estimatedCost);

            try
            {
                MaintenanceRepository.ReceiveDevice(
                    txtMaintCustomerName.Text.Trim(),
                    string.IsNullOrWhiteSpace(txtMaintCustomerPhone.Text) ? null : txtMaintCustomerPhone.Text.Trim(),
                    txtMaintDeviceInfo.Text.Trim(),
                    string.IsNullOrWhiteSpace(txtMaintIssueDescription.Text) ? null : txtMaintIssueDescription.Text.Trim(),
                    estimatedCost);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

            MessageBox.Show("تم استلام الجهاز وتسجيل التذكرة بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtMaintCustomerName.Clear();
            txtMaintCustomerPhone.Clear();
            txtMaintDeviceInfo.Clear();
            txtMaintIssueDescription.Clear();
            txtMaintEstimatedCost.Clear();
            LoadMaintenanceGrid();
        }

        // ==========================================================================
        // إرسال إشعار واتساب للعميل بحالة تذكرته الحالية
        // ==========================================================================
        private void BtnNotifyWhatsApp_Click(object sender, EventArgs e)
        {
            if (selectedTicketId == -1)
            {
                MessageBox.Show("من فضلك اختر تذكرة من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string status = cmbMaintStatusUpdate.SelectedItem?.ToString() ?? "قيد المتابعة";
            string customerName = selectedTicketCustomerName ?? "عميلنا العزيز";

            string separatorLine = "------------------------";

            string message = $"*صيانة - Temo Mobile Store*\n" +
                              $"{separatorLine}\n" +
                              $"العميل: {customerName}\n" +
                              $"حالة الصيانة: *{status}*\n" +
                              $"{separatorLine}\n" +
                              $"شكرًا لثقتكم بينا";

            WhatsAppHelper.SendMessage(selectedTicketCustomerPhone, message, this.FindForm());
        }

        private void BtnSaveMaintenanceStatus_Click(object sender, EventArgs e)
        {
            if (selectedTicketId == -1)
            {
                MessageBox.Show("من فضلك اختر تذكرة من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (cmbMaintStatusUpdate.SelectedItem == null)
            {
                MessageBox.Show("من فضلك اختر الحالة الجديدة.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string newStatus = cmbMaintStatusUpdate.SelectedItem.ToString();
            if (newStatus == "تم التسليم")
            {
                MessageBox.Show("استخدم زرار \"تسليم وتحصيل الأجرة\" تحت عشان تسجّل التسليم مع تحصيل الفلوس صح.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                MaintenanceRepository.UpdateStatus(selectedTicketId, newStatus);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

            MessageBox.Show("تم تحديث حالة التذكرة بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadMaintenanceGrid();
        }

        private void BtnDeliverMaintenanceDevice_Click(object sender, EventArgs e)
        {
            if (selectedTicketId == -1)
            {
                MessageBox.Show("من فضلك اختر تذكرة من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!decimal.TryParse(txtMaintActualCost.Text, out decimal actualCost) || actualCost < 0)
            {
                MessageBox.Show("من فضلك أدخل الأجرة الفعلية بشكل صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (cmbMaintPaymentMethod.SelectedItem == null)
            {
                MessageBox.Show("من فضلك اختر وسيلة التحصيل.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (IsTodayClosed())
            {
                MessageBox.Show("تم إقفال اليوم بالفعل، لا يمكن تسليم جهاز جديد وتحصيل أجرة النهاردة.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string method = cmbMaintPaymentMethod.SelectedItem.ToString();

            try
            {
                MaintenanceRepository.DeliverDevice(selectedTicketId, actualCost, method);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

            MessageBox.Show("تم تسليم الجهاز وتحصيل الأجرة بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            selectedTicketId = -1;
            txtMaintActualCost.Clear();
            LoadMaintenanceGrid();
        }

        // ==========================================================================
        // فحص هل تاريخ النهاردة تم إقفاله بالفعل
        // ==========================================================================
        private bool IsTodayClosed() => UIHelpers.IsTodayClosed();
    }
}
