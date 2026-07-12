using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // AttendancePageControl: شاشة "الحضور والمرتبات" - سويتش فوق للتبديل بين
    // تسجيل الحضور اليومي، كشف الرواتب، وإدارة الموظفين (نفس أسلوب TreasuryPageControl).
    // الشاشة دي أدمن فقط بالكامل (متسجلة في adminOnlyKeys في MainShell)، فمفيش
    // داعي لقيود صلاحيات إضافية جوّاها زي باقي الشاشات المتاحة لكل المستخدمين.
    // ==========================================================================
    public partial class AttendancePageControl : UserControl
    {
        private static readonly Color ColorPrimary = UIHelpers.ColorPrimary;
        private static readonly Color ColorSuccess = UIHelpers.ColorSuccess;
        private static readonly Color ColorDanger = UIHelpers.ColorDanger;
        private static readonly Color ColorWarning = UIHelpers.ColorWarning;
        private static readonly Color ColorBackground = UIHelpers.ColorBackground;

        private Guna2ComboBox cmbAttendanceOperationType;
        private Panel pnlAttendanceOps, pnlPayrollOps, pnlEmployeesOps;

        // ---------- تسجيل الحضور اليومي ----------
        private DateTimePicker dtpAttendanceDate;
        private DataGridView dgvAttendance;

        // ---------- كشف الرواتب ----------
        private DateTimePicker dtpPayrollMonth;
        private DataGridView dgvPayroll;
        private Label lblPayrollTotals;

        // ---------- إدارة الموظفين ----------
        private Guna2TextBox txtEmployeeName, txtEmployeePhone, txtEmployeeSalary;
        private DateTimePicker dtpEmployeeHireDate;
        private Guna2Button btnSaveEmployeeEdit;
        private DataGridView dgvEmployees;
        private int selectedEmployeeId = -1;

        public AttendancePageControl()
        {
            this.Dock = DockStyle.Fill;
            this.AutoScroll = true;
            this.BackColor = ColorBackground;

            BuildUI();
            LoadAttendanceGrid();
            LoadEmployeesGrid();
        }

        // ==========================================================================
        // الهيكل العام: سويتش فوق + 3 بانلات يتبادلوا الظهور
        // ==========================================================================
        private void BuildUI()
        {
            Label lblOpType = new Label() { Text = "نوع العملية:", Location = new Point(20, 20), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            cmbAttendanceOperationType = new Guna2ComboBox() { Location = new Point(130, 17), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbAttendanceOperationType.Items.AddRange(new string[] { "تسجيل الحضور اليومي 📅", "كشف الرواتب 💰", "إدارة الموظفين 👥" });
            cmbAttendanceOperationType.SelectedIndexChanged += CmbAttendanceOperationType_SelectedIndexChanged;

            pnlAttendanceOps = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 660) };
            pnlPayrollOps = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 660) };
            pnlEmployeesOps = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 660) };

            BuildAttendancePanel();
            BuildPayrollPanel();
            BuildEmployeesPanel();

            this.Controls.AddRange(new Control[] { lblOpType, cmbAttendanceOperationType, pnlAttendanceOps, pnlPayrollOps, pnlEmployeesOps });

            cmbAttendanceOperationType.SelectedIndex = 0;
        }

        private void CmbAttendanceOperationType_SelectedIndexChanged(object sender, EventArgs e)
        {
            pnlAttendanceOps.Visible = cmbAttendanceOperationType.SelectedIndex == 0;
            pnlPayrollOps.Visible = cmbAttendanceOperationType.SelectedIndex == 1;
            pnlEmployeesOps.Visible = cmbAttendanceOperationType.SelectedIndex == 2;

            if (cmbAttendanceOperationType.SelectedIndex == 0) LoadAttendanceGrid();
            else if (cmbAttendanceOperationType.SelectedIndex == 1) LoadPayrollGrid();
        }

        private void StyleDataGridView(DataGridView dgv) => UIHelpers.StyleDataGridView(dgv);

        // ==========================================================================
        // تسجيل الحضور اليومي
        // ==========================================================================
        private void BuildAttendancePanel()
        {
            Guna2Panel pnlToolbar = new Guna2Panel() { Location = new Point(0, 0), Size = new Size(1100, 70), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblDate = new Label() { Text = "التاريخ:", Location = new Point(20, 27), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(85, 92, 102) };
            dtpAttendanceDate = new DateTimePicker() { Location = new Point(80, 22), Width = 180, Format = DateTimePickerFormat.Short, Value = DateTime.Now };
            dtpAttendanceDate.ValueChanged += (s, e) => LoadAttendanceGrid();

            Guna2Button btnSaveAttendance = new Guna2Button() { Text = "حفظ الحضور 💾", Location = new Point(290, 18), Width = 160, Height = 34, FillColor = ColorSuccess, BorderRadius = 9 };
            btnSaveAttendance.Click += BtnSaveAttendance_Click;

            pnlToolbar.Controls.AddRange(new Control[] { lblDate, dtpAttendanceDate, btnSaveAttendance });

            Guna2Panel pnlGridCard = new Guna2Panel() { Location = new Point(0, 85), Size = new Size(1100, 575), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            dgvAttendance = new DataGridView() { Location = new Point(20, 20), Size = new Size(1060, 535), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, AllowUserToAddRows = false, RowHeadersVisible = false };
            dgvAttendance.CurrentCellDirtyStateChanged += (s, e) => { if (dgvAttendance.IsCurrentCellDirty) dgvAttendance.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            StyleDataGridView(dgvAttendance);
            pnlGridCard.Controls.Add(dgvAttendance);

            pnlAttendanceOps.Controls.AddRange(new Control[] { pnlToolbar, pnlGridCard });
        }

        private void LoadAttendanceGrid()
        {
            if (dgvAttendance == null) return;
            try
            {
                DataTable dt = AttendanceRepository.GetAttendanceForDate(dtpAttendanceDate.Value.Date);
                dgvAttendance.DataSource = dt;

                if (dgvAttendance.Columns["EmployeeId"] != null) dgvAttendance.Columns["EmployeeId"].Visible = false;

                if (dgvAttendance.Columns["الحالة"] is DataGridViewTextBoxColumn)
                {
                    int idx = dgvAttendance.Columns["الحالة"].Index;
                    dgvAttendance.Columns.RemoveAt(idx);

                    DataGridViewComboBoxColumn statusColumn = new DataGridViewComboBoxColumn
                    {
                        Name = "الحالة",
                        HeaderText = "الحالة",
                        DataPropertyName = "الحالة",
                        FlatStyle = FlatStyle.Flat
                    };
                    statusColumn.Items.AddRange("", AttendanceRepository.StatusPresent, AttendanceRepository.StatusAbsent, AttendanceRepository.StatusLeave);
                    dgvAttendance.Columns.Insert(idx, statusColumn);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnSaveAttendance_Click(object sender, EventArgs e)
        {
            if (!(dgvAttendance.DataSource is DataTable dt)) return;
            dgvAttendance.EndEdit();

            int savedCount = 0;
            try
            {
                foreach (DataRow dr in dt.Rows)
                {
                    string status = dr["الحالة"]?.ToString();
                    if (string.IsNullOrEmpty(status)) continue;

                    int employeeId = Convert.ToInt32(dr["EmployeeId"]);
                    AttendanceRepository.SetAttendanceStatus(employeeId, dtpAttendanceDate.Value.Date, status);
                    savedCount++;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("حصل خطأ أثناء حفظ الحضور: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (savedCount == 0)
            {
                MessageBox.Show("من فضلك حدد حالة موظف واحد على الأقل قبل الحفظ.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show("تم حفظ الحضور بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadAttendanceGrid();
        }

        // ==========================================================================
        // كشف الرواتب
        // ==========================================================================
        private void BuildPayrollPanel()
        {
            Guna2Panel pnlToolbar = new Guna2Panel() { Location = new Point(0, 0), Size = new Size(1100, 70), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblMonth = new Label() { Text = "الشهر:", Location = new Point(20, 27), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(85, 92, 102) };
            dtpPayrollMonth = new DateTimePicker() { Location = new Point(75, 22), Width = 180, Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM yyyy", Value = DateTime.Now };
            dtpPayrollMonth.ValueChanged += (s, e) => LoadPayrollGrid();

            pnlToolbar.Controls.AddRange(new Control[] { lblMonth, dtpPayrollMonth });

            Guna2Panel pnlGridCard = new Guna2Panel() { Location = new Point(0, 85), Size = new Size(1100, 575), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            dgvPayroll = new DataGridView() { Location = new Point(20, 20), Size = new Size(1060, 495), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false };
            StyleDataGridView(dgvPayroll);

            lblPayrollTotals = new Label() { Text = "", Location = new Point(20, 530), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };

            pnlGridCard.Controls.AddRange(new Control[] { dgvPayroll, lblPayrollTotals });

            pnlPayrollOps.Controls.AddRange(new Control[] { pnlToolbar, pnlGridCard });
        }

        private void LoadPayrollGrid()
        {
            if (dgvPayroll == null) return;
            try
            {
                var rows = AttendanceRepository.GetPayrollForMonth(dtpPayrollMonth.Value.Year, dtpPayrollMonth.Value.Month);

                DataTable dt = new DataTable();
                dt.Columns.AddRange(new DataColumn[] { new DataColumn("الاسم"), new DataColumn("المرتب الشهري"), new DataColumn("قيمة اليوم"),
                    new DataColumn("أيام الحضور"), new DataColumn("أيام الغياب"), new DataColumn("أيام الإجازة"), new DataColumn("الراتب الصافي") });

                decimal totalNet = 0;
                foreach (var r in rows)
                {
                    dt.Rows.Add(r.FullName, r.MonthlySalary.ToString("N2"), r.DayValue.ToString("N2"), r.PresentDays, r.AbsentDays, r.LeaveDays, r.NetSalary.ToString("N2"));
                    totalNet += r.NetSalary;
                }

                dgvPayroll.DataSource = dt;
                lblPayrollTotals.Text = $"💰 إجمالي صافي الرواتب لشهر {dtpPayrollMonth.Value:MMMM yyyy}: {totalNet:N2} ج.م";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        // ==========================================================================
        // إدارة الموظفين (إضافة / تعديل / حذف)
        // ==========================================================================
        private void BuildEmployeesPanel()
        {
            Guna2Panel gbEmployee = new Guna2Panel() { Location = new Point(0, 0), Size = new Size(280, 340), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblTitle = new Label() { Text = "🧑‍💼 إضافة / تعديل موظف", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblName = new Label() { Text = "الاسم:", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtEmployeeName = new Guna2TextBox() { Location = new Point(20, 70), Width = 240, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblPhone = new Label() { Text = "رقم التليفون:", Location = new Point(20, 108), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtEmployeePhone = new Guna2TextBox() { Location = new Point(20, 128), Width = 240, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblSalary = new Label() { Text = "المرتب الشهري:", Location = new Point(20, 166), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtEmployeeSalary = new Guna2TextBox() { Location = new Point(20, 186), Width = 240, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblHireDate = new Label() { Text = "تاريخ التعيين:", Location = new Point(20, 224), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            dtpEmployeeHireDate = new DateTimePicker() { Location = new Point(20, 244), Width = 240, Format = DateTimePickerFormat.Short, Value = DateTime.Now };

            Guna2Button btnAddEmployee = new Guna2Button() { Text = "إضافة موظف جديد ✅", Location = new Point(20, 278), Width = 240, Height = 34, FillColor = ColorSuccess, BorderRadius = 9 };
            btnAddEmployee.Click += BtnAddEmployee_Click;

            gbEmployee.Controls.AddRange(new Control[] { lblTitle, lblName, txtEmployeeName, lblPhone, txtEmployeePhone, lblSalary, txtEmployeeSalary, lblHireDate, dtpEmployeeHireDate, btnAddEmployee });

            Guna2Panel gbEmployeeActions = new Guna2Panel() { Location = new Point(0, 355), Size = new Size(280, 110), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            btnSaveEmployeeEdit = new Guna2Button() { Text = "حفظ التعديل 💾", Location = new Point(20, 18), Width = 240, Height = 32, FillColor = ColorWarning, Enabled = false, BorderRadius = 9 };
            btnSaveEmployeeEdit.Click += BtnSaveEmployeeEdit_Click;

            Guna2Button btnDeleteEmployee = new Guna2Button() { Text = "حذف الموظف ❌", Location = new Point(20, 60), Width = 240, Height = 32, FillColor = ColorDanger, BorderRadius = 9 };
            btnDeleteEmployee.Click += BtnDeleteEmployee_Click;

            gbEmployeeActions.Controls.AddRange(new Control[] { btnSaveEmployeeEdit, btnDeleteEmployee });

            Guna2Panel pnlGridCard = new Guna2Panel() { Location = new Point(300, 0), Size = new Size(800, 660), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblGridTitle = new Label() { Text = "👥 الموظفين", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ColorPrimary };
            dgvEmployees = new DataGridView() { Location = new Point(20, 50), Size = new Size(760, 590), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvEmployees.CellClick += DgvEmployees_CellClick;
            StyleDataGridView(dgvEmployees);
            pnlGridCard.Controls.AddRange(new Control[] { lblGridTitle, dgvEmployees });

            pnlEmployeesOps.Controls.AddRange(new Control[] { gbEmployee, gbEmployeeActions, pnlGridCard });
        }

        private void LoadEmployeesGrid()
        {
            if (dgvEmployees == null) return;
            try
            {
                DataTable dt = AttendanceRepository.GetEmployees();
                dgvEmployees.DataSource = dt;
                if (dgvEmployees.Columns["EmployeeId"] != null) dgvEmployees.Columns["EmployeeId"].Visible = false;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void DgvEmployees_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvEmployees.Rows[e.RowIndex];
            selectedEmployeeId = Convert.ToInt32(row.Cells["EmployeeId"].Value);
            txtEmployeeName.Text = row.Cells["الاسم"].Value?.ToString();
            txtEmployeePhone.Text = row.Cells["الهاتف"].Value?.ToString();
            txtEmployeeSalary.Text = row.Cells["المرتب الشهري"].Value?.ToString();
            if (DateTime.TryParse(row.Cells["تاريخ التعيين"].Value?.ToString(), out DateTime hireDate))
                dtpEmployeeHireDate.Value = hireDate;
            btnSaveEmployeeEdit.Enabled = true;
        }

        private bool ValidateEmployeeInputs(out decimal salary)
        {
            salary = 0;
            if (string.IsNullOrWhiteSpace(txtEmployeeName.Text))
            {
                MessageBox.Show("من فضلك أدخل اسم الموظف.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (!decimal.TryParse(txtEmployeeSalary.Text, out salary) || salary < 0)
            {
                MessageBox.Show("من فضلك أدخل مرتب شهري صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private void BtnAddEmployee_Click(object sender, EventArgs e)
        {
            if (!ValidateEmployeeInputs(out decimal salary)) return;

            try
            {
                AttendanceRepository.AddEmployee(txtEmployeeName.Text.Trim(), string.IsNullOrWhiteSpace(txtEmployeePhone.Text) ? null : txtEmployeePhone.Text.Trim(), salary, dtpEmployeeHireDate.Value.Date);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

            MessageBox.Show("تم إضافة الموظف بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearEmployeeInputs();
            LoadEmployeesGrid();
            LoadAttendanceGrid();
        }

        private void BtnSaveEmployeeEdit_Click(object sender, EventArgs e)
        {
            if (selectedEmployeeId == -1) return;
            if (!ValidateEmployeeInputs(out decimal salary)) return;

            try
            {
                AttendanceRepository.UpdateEmployee(selectedEmployeeId, txtEmployeeName.Text.Trim(), string.IsNullOrWhiteSpace(txtEmployeePhone.Text) ? null : txtEmployeePhone.Text.Trim(), salary, dtpEmployeeHireDate.Value.Date);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

            MessageBox.Show("تم تعديل بيانات الموظف بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearEmployeeInputs();
            LoadEmployeesGrid();
            LoadAttendanceGrid();
        }

        private void BtnDeleteEmployee_Click(object sender, EventArgs e)
        {
            if (selectedEmployeeId == -1)
            {
                MessageBox.Show("من فضلك اختر موظف من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("هل أنت متأكد من حذف هذا الموظف؟ هيتحذف معاه كل سجلات حضوره.", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                AttendanceRepository.DeleteEmployee(selectedEmployeeId);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

            MessageBox.Show("تم حذف الموظف بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearEmployeeInputs();
            LoadEmployeesGrid();
            LoadAttendanceGrid();
        }

        private void ClearEmployeeInputs()
        {
            selectedEmployeeId = -1;
            txtEmployeeName.Clear();
            txtEmployeePhone.Clear();
            txtEmployeeSalary.Clear();
            dtpEmployeeHireDate.Value = DateTime.Now;
            btnSaveEmployeeEdit.Enabled = false;
        }
    }
}
