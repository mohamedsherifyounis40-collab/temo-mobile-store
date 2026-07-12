using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // SettingsPageControl: نسخة مستقلة من ثلاث شاشات: إعدادات المحل، النسخ
    // الاحتياطي، إدارة المستخدمين. مع سويتش فوق للتبديل بينهم.
    //
    // ملحوظة: نظام الصلاحيات (إخفاء الشاشات دي عن الموظف العادي) هنعمله في
    // خطوة منفصلة بعدين زي ما اتفقنا.
    // ==========================================================================
    public partial class SettingsPageControl : UserControl
    {
        private static readonly Color ColorPrimary = UIHelpers.ColorPrimary;
        private static readonly Color ColorSuccess = UIHelpers.ColorSuccess;
        private static readonly Color ColorDanger = UIHelpers.ColorDanger;
        private static readonly Color ColorWarning = UIHelpers.ColorWarning;
        private static readonly Color ColorNeutral = UIHelpers.ColorNeutral;
        private static readonly Color ColorBackground = UIHelpers.ColorBackground;

        private string CurrentStoreName = "Temo Mobile Store";
        private string CurrentStorePhone = "";
        private string CurrentStoreAddress = "";
        private byte[] CurrentStoreLogo = null;

        private string BackupFolderPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TemoStore_Backups");

        private Guna2ComboBox cmbSettingsViewType;
        private Panel pnlStoreSettings, pnlBackup, pnlUsers;

        // ---------- إعدادات المحل ----------
        private PictureBox picStoreLogo;
        private Guna2TextBox txtSettingsStoreName, txtSettingsPhone, txtSettingsAddress;

        // ---------- النسخ الاحتياطي ----------
        private Label lblBackupStatus, lblCloudStatus;
        private DataGridView dgvBackups;

        // ---------- إدارة المستخدمين ----------
        private Guna2TextBox txtNewUsername, txtNewUserPassword;
        private Guna2ComboBox cmbNewUserRole;
        private Guna2Button btnSaveUserEdit;
        private DataGridView dgvUsers;
        private int selectedUserId = -1;

        public SettingsPageControl()
        {
            this.Dock = DockStyle.Fill;
            this.AutoScroll = true;
            this.BackColor = ColorBackground;

            BuildUI();
        }

        // ==========================================================================
        // الهيكل العام: سويتش فوق + 3 بانلات يتبادلوا الظهور
        // ==========================================================================
        private void BuildUI()
        {
            Label lblViewType = new Label() { Text = "الإعدادات:", Location = new Point(20, 20), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            cmbSettingsViewType = new Guna2ComboBox() { Location = new Point(110, 17), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbSettingsViewType.Items.AddRange(new string[] { "إعدادات المحل ⚙️", "النسخ الاحتياطي 💾", "إدارة المستخدمين 👤" });
            cmbSettingsViewType.SelectedIndexChanged += CmbSettingsViewType_SelectedIndexChanged;

            pnlStoreSettings = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 650) };
            pnlBackup = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 650) };
            pnlUsers = new Panel() { Location = new Point(20, 55), Size = new Size(1100, 650) };

            BuildStoreSettingsPanel();
            BuildBackupPanel();
            BuildUsersPanel();

            pnlBackup.Visible = false;
            pnlUsers.Visible = false;

            this.Controls.AddRange(new Control[] { lblViewType, cmbSettingsViewType, pnlStoreSettings, pnlBackup, pnlUsers });

            cmbSettingsViewType.SelectedIndex = 0;
        }

        private void CmbSettingsViewType_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = cmbSettingsViewType.SelectedIndex;
            pnlStoreSettings.Visible = idx == 0;
            pnlBackup.Visible = idx == 1;
            pnlUsers.Visible = idx == 2;

            if (idx == 1) LoadBackupsGrid();
        }

        // ==========================================================================
        // بانل إعدادات المحل
        // ==========================================================================
        private void BuildStoreSettingsPanel()
        {
            Guna2Panel gbLogo = new Guna2Panel() { Location = new Point(0, 0), Size = new Size(260, 280), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblLogoTitle = new Label() { Text = "🖼️ شعار المحل", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };
            picStoreLogo = new PictureBox() { Location = new Point(30, 50), Size = new Size(200, 140), SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
            Guna2Button btnUploadLogo = new Guna2Button() { Text = "رفع شعار 🖼️", Location = new Point(30, 200), Width = 200, Height = 32, FillColor = ColorPrimary, BorderRadius = 9 };
            btnUploadLogo.Click += BtnUploadLogo_Click;
            Guna2Button btnRemoveLogo = new Guna2Button() { Text = "إزالة الشعار 🗑️", Location = new Point(30, 238), Width = 200, Height = 30, FillColor = ColorDanger, BorderRadius = 9 };
            btnRemoveLogo.Click += BtnRemoveLogo_Click;
            gbLogo.Controls.AddRange(new Control[] { lblLogoTitle, picStoreLogo, btnUploadLogo, btnRemoveLogo });

            Guna2Panel gbInfo = new Guna2Panel() { Location = new Point(280, 0), Size = new Size(440, 280), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblInfoTitle = new Label() { Text = "⚙️ بيانات المحل", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblStoreName = new Label() { Text = "اسم المحل:", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtSettingsStoreName = new Guna2TextBox() { Location = new Point(20, 70), Width = 400, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblPhone = new Label() { Text = "رقم التليفون:", Location = new Point(20, 108), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtSettingsPhone = new Guna2TextBox() { Location = new Point(20, 128), Width = 400, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblAddress = new Label() { Text = "العنوان:", Location = new Point(20, 166), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtSettingsAddress = new Guna2TextBox() { Location = new Point(20, 186), Width = 400, Height = 55, Multiline = true, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Guna2Button btnSaveStoreSettings = new Guna2Button() { Text = "حفظ الإعدادات 💾", Location = new Point(20, 232), Width = 400, Height = 36, FillColor = ColorSuccess, BorderRadius = 10 };
            btnSaveStoreSettings.Click += BtnSaveStoreSettings_Click;

            gbInfo.Controls.AddRange(new Control[] { lblInfoTitle, lblStoreName, txtSettingsStoreName, lblPhone, txtSettingsPhone, lblAddress, txtSettingsAddress, btnSaveStoreSettings });

            Label lblNote = new Label()
            {
                Text = "ℹ️ البيانات دي هتظهر تلقائيًا في رأس فاتورة البيع المطبوعة (اسم المحل، التليفون، العنوان).",
                Location = new Point(0, 295),
                Size = new Size(720, 40),
                ForeColor = Color.FromArgb(85, 92, 102)
            };

            pnlStoreSettings.Controls.AddRange(new Control[] { gbLogo, gbInfo, lblNote });

            LoadStoreSettingsIntoMemory();
            txtSettingsStoreName.Text = CurrentStoreName;
            txtSettingsPhone.Text = CurrentStorePhone;
            txtSettingsAddress.Text = CurrentStoreAddress;
            RefreshLogoPreview();
        }

        private void LoadStoreSettingsIntoMemory()
        {
            UIHelpers.LoadStoreSettings(out CurrentStoreName, out CurrentStorePhone, out CurrentStoreAddress, out CurrentStoreLogo);
        }

        private void RefreshLogoPreview()
        {
            if (CurrentStoreLogo != null && CurrentStoreLogo.Length > 0)
            {
                using (var ms = new System.IO.MemoryStream(CurrentStoreLogo))
                    picStoreLogo.Image = Image.FromStream(ms);
            }
            else
            {
                picStoreLogo.Image = null;
            }
        }

        private void BtnUploadLogo_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog() { Filter = "ملفات صور (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
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
                                CurrentStoreLogo = msResized.ToArray();
                            }
                        }
                        RefreshLogoPreview();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("مش قادر يفتح الصورة دي: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnRemoveLogo_Click(object sender, EventArgs e)
        {
            CurrentStoreLogo = null;
            RefreshLogoPreview();
        }

        private void BtnSaveStoreSettings_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSettingsStoreName.Text))
            {
                MessageBox.Show("اسم المحل لازم يتكتب.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                SettingsRepository.SaveStoreSettings(txtSettingsStoreName.Text.Trim(), txtSettingsPhone.Text.Trim(), txtSettingsAddress.Text.Trim(), CurrentStoreLogo);

                LoadStoreSettingsIntoMemory();
                MessageBox.Show("تم حفظ إعدادات المحل بنجاح ✅", "نجاح", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("حصل خطأ أثناء حفظ الإعدادات: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ==========================================================================
        // بانل النسخ الاحتياطي
        // ==========================================================================
        private void BuildBackupPanel()
        {
            Guna2Panel pnlInfoCard = new Guna2Panel() { Location = new Point(0, 0), Size = new Size(1080, 175), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblTitle = new Label() { Text = "💾 النسخ الاحتياطي والاسترجاع", Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = ColorPrimary, Location = new Point(20, 15), AutoSize = true };
            lblBackupStatus = new Label() { Text = "", Location = new Point(20, 50), AutoSize = true, ForeColor = Color.FromArgb(85, 92, 102) };
            lblCloudStatus = new Label() { Text = "", Location = new Point(20, 73), AutoSize = true };

            Guna2Button btnCreateBackupNow = new Guna2Button() { Text = "عمل نسخة احتياطية الآن 💾", Location = new Point(20, 105), Width = 230, Height = 40, FillColor = ColorSuccess, BorderRadius = 10 };
            btnCreateBackupNow.Click += BtnCreateBackupNow_Click;

            Guna2Button btnRestoreBackup = new Guna2Button() { Text = "استرجاع النسخة المحددة ⏪", Location = new Point(260, 105), Width = 230, Height = 40, FillColor = ColorWarning, BorderRadius = 10 };
            btnRestoreBackup.Click += BtnRestoreBackup_Click;

            Guna2Button btnDeleteBackup = new Guna2Button() { Text = "حذف النسخة المحددة 🗑️", Location = new Point(500, 105), Width = 220, Height = 40, FillColor = ColorDanger, BorderRadius = 10 };
            btnDeleteBackup.Click += BtnDeleteBackup_Click;

            Guna2Button btnOpenBackupFolder = new Guna2Button() { Text = "فتح مجلد النسخ 📂", Location = new Point(730, 105), Width = 200, Height = 40, FillColor = ColorNeutral, ForeColor = ColorPrimary, BorderRadius = 10 };
            btnOpenBackupFolder.Click += BtnOpenBackupFolder_Click;

            pnlInfoCard.Controls.AddRange(new Control[] { lblTitle, lblBackupStatus, lblCloudStatus, btnCreateBackupNow, btnRestoreBackup, btnDeleteBackup, btnOpenBackupFolder });

            Label lblNote = new Label()
            {
                Text = "ℹ️ ملاحظة: البرنامج بياخد نسخة احتياطية تلقائية عند فتح البرنامج وعند قفله، وبيحتفظ بآخر 30 نسخة تلقائيًا. الاسترجاع بيستبدل كل بيانات البرنامج الحالية بالنسخة اللي هتختارها.",
                Location = new Point(0, 185),
                Size = new Size(1080, 40),
                ForeColor = Color.FromArgb(85, 92, 102)
            };

            Guna2Panel pnlGridCard = new Guna2Panel() { Location = new Point(0, 230), Size = new Size(1080, 470), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            dgvBackups = new DataGridView() { Location = new Point(20, 20), Size = new Size(1040, 430), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            StyleDataGridView(dgvBackups);
            pnlGridCard.Controls.Add(dgvBackups);

            pnlBackup.Controls.AddRange(new Control[] { pnlInfoCard, lblNote, pnlGridCard });

            LoadBackupsGrid();
        }

        private void EnsureBackupFolderExists()
        {
            if (!System.IO.Directory.Exists(BackupFolderPath))
                System.IO.Directory.CreateDirectory(BackupFolderPath);
        }

        private string CreateBackupFile()
        {
            EnsureBackupFolderExists();
            string fileName = $"TemoStoreDB_Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db";
            string destPath = System.IO.Path.Combine(BackupFolderPath, fileName);

            SettingsRepository.BackupDatabaseTo(destPath);

            TryCopyToCloudBackup(destPath, fileName);
            return destPath;
        }

        private string GetCloudBackupFolder()
        {
            try
            {
                string oneDrive = Environment.GetEnvironmentVariable("OneDrive")
                    ?? Environment.GetEnvironmentVariable("OneDriveConsumer")
                    ?? Environment.GetEnvironmentVariable("OneDriveCommercial");
                if (!string.IsNullOrEmpty(oneDrive) && System.IO.Directory.Exists(oneDrive))
                    return System.IO.Path.Combine(oneDrive, "TemoStore_Backups_Cloud");

                foreach (var drive in System.IO.DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    try
                    {
                        string candidate = System.IO.Path.Combine(drive.RootDirectory.FullName, "My Drive");
                        if (System.IO.Directory.Exists(candidate))
                            return System.IO.Path.Combine(candidate, "TemoStore_Backups_Cloud");
                    }
                    catch { }
                }

                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string googleDriveClassic = System.IO.Path.Combine(userProfile, "Google Drive");
                if (System.IO.Directory.Exists(googleDriveClassic))
                    return System.IO.Path.Combine(googleDriveClassic, "TemoStore_Backups_Cloud");
            }
            catch { }
            return null;
        }

        private void TryCopyToCloudBackup(string sourcePath, string fileName)
        {
            try
            {
                string cloudFolder = GetCloudBackupFolder();
                if (cloudFolder == null) return;

                if (!System.IO.Directory.Exists(cloudFolder))
                    System.IO.Directory.CreateDirectory(cloudFolder);

                string destPath = System.IO.Path.Combine(cloudFolder, fileName);
                System.IO.File.Copy(sourcePath, destPath, true);
            }
            catch { }
        }

        private void PruneOldBackups(int keepCount = 30)
        {
            var files = System.IO.Directory.GetFiles(BackupFolderPath, "*.db")
                .Select(f => new System.IO.FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            for (int i = keepCount; i < files.Count; i++)
            {
                try { files[i].Delete(); } catch { }
            }
        }

        private void LoadBackupsGrid()
        {
            EnsureBackupFolderExists();
            DataTable dt = new DataTable();
            dt.Columns.Add("اسم الملف");
            dt.Columns.Add("التاريخ والوقت");
            dt.Columns.Add("الحجم");
            dt.Columns.Add("المسار الكامل");

            var files = System.IO.Directory.GetFiles(BackupFolderPath, "*.db")
                .Select(f => new System.IO.FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            foreach (var f in files)
            {
                double sizeKb = f.Length / 1024.0;
                string sizeText = sizeKb >= 1024 ? $"{(sizeKb / 1024.0):N2} MB" : $"{sizeKb:N0} KB";
                dt.Rows.Add(f.Name, f.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"), sizeText, f.FullName);
            }

            dgvBackups.DataSource = dt;
            if (dgvBackups.Columns.Contains("المسار الكامل"))
                dgvBackups.Columns["المسار الكامل"].Visible = false;

            lblBackupStatus.Text = files.Any()
                ? $"آخر نسخة احتياطية: {files.First().CreationTime:yyyy-MM-dd HH:mm:ss}   |   عدد النسخ المحفوظة: {files.Count}"
                : "لا توجد نسخ احتياطية بعد.";

            string cloudFolder = GetCloudBackupFolder();
            lblCloudStatus.Text = cloudFolder != null
                ? $"☁️ متصل بخدمة تخزين سحابي، وبيتحفظ فيها نسخة إضافية تلقائيًا: {cloudFolder}"
                : "☁️ مفيش Google Drive أو OneDrive متزامن على الجهاز ده حاليًا، فالنسخ بتتحفظ محليًا بس.";
            lblCloudStatus.ForeColor = cloudFolder != null ? ColorSuccess : ColorDanger;
        }

        private void BtnCreateBackupNow_Click(object sender, EventArgs e)
        {
            try
            {
                CreateBackupFile();
                PruneOldBackups();
                LoadBackupsGrid();
                MessageBox.Show("تم عمل نسخة احتياطية بنجاح ✅", "نجاح", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("حصل خطأ أثناء عمل النسخة الاحتياطية: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRestoreBackup_Click(object sender, EventArgs e)
        {
            if (dgvBackups.SelectedRows.Count == 0)
            {
                MessageBox.Show("من فضلك اختار نسخة احتياطية من الجدول الأول.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string backupPath = dgvBackups.SelectedRows[0].Cells["المسار الكامل"].Value.ToString();
            string fileName = dgvBackups.SelectedRows[0].Cells["اسم الملف"].Value.ToString();

            var confirm = MessageBox.Show(
                $"هتستبدل كل بيانات البرنامج الحالية بالنسخة الاحتياطية دي:\n\n{fileName}\n\nأي بيانات اتسجلت بعد تاريخ النسخة دي هتضيع نهائيًا. متأكد إنك عايز تكمل؟",
                "تأكيد الاسترجاع - عملية خطيرة",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes) return;

            try
            {
                CreateBackupFile();

                string dbFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TemoStoreDB.db");
                System.IO.File.Copy(backupPath, dbFilePath, true);

                MessageBox.Show("تم الاسترجاع بنجاح ✅. البرنامج هيقفل ويفتح تاني عشان البيانات المسترجعة تظهر صح في كل الشاشات.", "تم الاسترجاع", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Application.Restart();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show("حصل خطأ أثناء الاسترجاع: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDeleteBackup_Click(object sender, EventArgs e)
        {
            if (dgvBackups.SelectedRows.Count == 0)
            {
                MessageBox.Show("من فضلك اختار نسخة احتياطية من الجدول الأول.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string backupPath = dgvBackups.SelectedRows[0].Cells["المسار الكامل"].Value.ToString();
            string fileName = dgvBackups.SelectedRows[0].Cells["اسم الملف"].Value.ToString();

            var confirm = MessageBox.Show($"متأكد إنك عايز تحذف النسخة الاحتياطية دي؟\n\n{fileName}", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            try
            {
                System.IO.File.Delete(backupPath);
                LoadBackupsGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show("حصل خطأ أثناء الحذف: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnOpenBackupFolder_Click(object sender, EventArgs e)
        {
            try
            {
                EnsureBackupFolderExists();
                System.Diagnostics.Process.Start("explorer.exe", BackupFolderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("مش قادر يفتح المجلد: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ==========================================================================
        // بانل إدارة المستخدمين
        // ==========================================================================
        private void BuildUsersPanel()
        {
            Guna2Panel gbAddUser = new Guna2Panel() { Location = new Point(0, 0), Size = new Size(280, 400), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblUserTitle = new Label() { Text = "👤 إضافة / تعديل مستخدم", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblUsername = new Label() { Text = "اسم المستخدم:", Location = new Point(20, 50), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtNewUsername = new Guna2TextBox() { Location = new Point(20, 70), Width = 240, BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblPassword = new Label() { Text = "كلمة المرور (سيبها فاضية لو مش عايز تغيّرها):", Location = new Point(20, 108), Size = new Size(240, 30), Font = new Font("Segoe UI", 7.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            txtNewUserPassword = new Guna2TextBox() { Location = new Point(20, 140), Width = 240, PasswordChar = '●', BorderRadius = 8, FillColor = Color.FromArgb(248, 249, 251) };

            Label lblRole = new Label() { Text = "الدور:", Location = new Point(20, 178), AutoSize = true, Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(85, 92, 102) };
            cmbNewUserRole = new Guna2ComboBox() { Location = new Point(20, 198), Width = 240, DropDownStyle = ComboBoxStyle.DropDownList, BorderRadius = 8 };
            cmbNewUserRole.Items.AddRange(new string[] { "Employee", "Admin" });
            cmbNewUserRole.SelectedIndex = 0;

            Guna2Button btnAddUser = new Guna2Button() { Text = "إضافة مستخدم جديد ✅", Location = new Point(20, 238), Width = 240, Height = 34, FillColor = ColorSuccess, BorderRadius = 9 };
            btnAddUser.Click += BtnAddUser_Click;

            Guna2Button btnEditUserMode = new Guna2Button() { Text = "تعديل المستخدم المحدد ✏️", Location = new Point(20, 278), Width = 240, Height = 34, FillColor = ColorPrimary, BorderRadius = 9 };
            btnEditUserMode.Click += BtnEditUserMode_Click;

            btnSaveUserEdit = new Guna2Button() { Text = "حفظ تعديل المستخدم 💾", Location = new Point(20, 318), Width = 240, Height = 34, FillColor = ColorWarning, Enabled = false, BorderRadius = 9 };
            btnSaveUserEdit.Click += BtnSaveUserEdit_Click;

            Guna2Button btnDeleteUser = new Guna2Button() { Text = "حذف المستخدم المحدد ❌", Location = new Point(20, 358), Width = 240, Height = 30, FillColor = ColorDanger, BorderRadius = 9 };
            btnDeleteUser.Click += BtnDeleteUser_Click;

            gbAddUser.Controls.AddRange(new Control[] { lblUserTitle, lblUsername, txtNewUsername, lblPassword, txtNewUserPassword, lblRole, cmbNewUserRole, btnAddUser, btnEditUserMode, btnSaveUserEdit, btnDeleteUser });

            Guna2Panel pnlNote = new Guna2Panel() { Location = new Point(0, 415), Size = new Size(280, 175), FillColor = Color.FromArgb(248, 249, 251), BorderRadius = 12 };
            Label lblNote = new Label()
            {
                Text = "ℹ️ Admin: يشوف ويعمل كل حاجة في البرنامج.\nEmployee: يقدر يبيع ويسجّل مصروفات وحركات بس، من غير تعديل أو إلغاء أو حذف.\n\nللتعديل: دوس على المستخدم في الجدول، وبعدين \"تعديل المستخدم المحدد\"، وبعدين \"حفظ تعديل المستخدم\".",
                Location = new Point(15, 12),
                Size = new Size(250, 150),
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(85, 92, 102)
            };
            pnlNote.Controls.Add(lblNote);

            Guna2Panel pnlGridCard = new Guna2Panel() { Location = new Point(300, 0), Size = new Size(780, 620), FillColor = Color.White, BorderRadius = 14, BorderColor = Color.FromArgb(230, 232, 238), BorderThickness = 1 };
            Label lblGridTitle = new Label() { Text = "👥 المستخدمين المسجّلين", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = ColorPrimary };

            dgvUsers = new DataGridView() { Location = new Point(20, 50), Size = new Size(740, 555), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvUsers.CellClick += DgvUsers_CellClick;
            StyleDataGridView(dgvUsers);
            pnlGridCard.Controls.AddRange(new Control[] { lblGridTitle, dgvUsers });

            pnlUsers.Controls.AddRange(new Control[] { gbAddUser, pnlNote, pnlGridCard });

            LoadUsersGrid();
        }

        private void LoadUsersGrid()
        {
            try
            {
                dgvUsers.DataSource = SettingsRepository.GetUsersGrid();
                if (dgvUsers.Columns["Id"] != null) dgvUsers.Columns["Id"].Visible = false;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void DgvUsers_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvUsers.Rows[e.RowIndex];
            selectedUserId = Convert.ToInt32(row.Cells["Id"].Value);
            txtNewUsername.Text = row.Cells["اسم المستخدم"].Value.ToString();
            txtNewUserPassword.Clear();
            cmbNewUserRole.SelectedItem = row.Cells["الدور"].Value.ToString();
            btnSaveUserEdit.Enabled = false;
        }

        private void BtnEditUserMode_Click(object sender, EventArgs e)
        {
            if (selectedUserId == -1)
            {
                MessageBox.Show("من فضلك اختر مستخدم من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            btnSaveUserEdit.Enabled = true;
        }

        private void BtnSaveUserEdit_Click(object sender, EventArgs e)
        {
            if (selectedUserId == -1) return;

            if (string.IsNullOrWhiteSpace(txtNewUsername.Text))
            {
                MessageBox.Show("اسم المستخدم مايفضلش فاضي.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string newUsername = txtNewUsername.Text.Trim();
            string newRole = cmbNewUserRole.SelectedItem.ToString();

            try
            {
                UserRecord existing = SettingsRepository.GetUser(selectedUserId);
                if (existing == null)
                {
                    MessageBox.Show("لم يتم العثور على المستخدم.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (SettingsRepository.UsernameExists(newUsername, selectedUserId))
                {
                    MessageBox.Show("اسم المستخدم ده مستخدم بالفعل لحساب تاني.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (existing.Role == "Admin" && newRole != "Admin" && SettingsRepository.CountAdmins() <= 1)
                {
                    MessageBox.Show("لا يمكن تغيير دور آخر حساب أدمن في النظام.", "غير مسموح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                SettingsRepository.UpdateUser(selectedUserId, newUsername, newRole, string.IsNullOrEmpty(txtNewUserPassword.Text) ? null : txtNewUserPassword.Text);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

            MessageBox.Show("تم تعديل المستخدم بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            selectedUserId = -1;
            txtNewUsername.Clear();
            txtNewUserPassword.Clear();
            cmbNewUserRole.SelectedIndex = 0;
            btnSaveUserEdit.Enabled = false;
            LoadUsersGrid();
        }

        private void BtnAddUser_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNewUsername.Text) || string.IsNullOrWhiteSpace(txtNewUserPassword.Text))
            {
                MessageBox.Show("من فضلك أدخل اسم مستخدم وكلمة مرور.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                if (SettingsRepository.UsernameExists(txtNewUsername.Text.Trim()))
                {
                    MessageBox.Show("اسم المستخدم ده مستخدم بالفعل.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                SettingsRepository.AddUser(txtNewUsername.Text.Trim(), txtNewUserPassword.Text, cmbNewUserRole.SelectedItem.ToString());
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

            MessageBox.Show("تم إضافة المستخدم بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtNewUsername.Clear();
            txtNewUserPassword.Clear();
            cmbNewUserRole.SelectedIndex = 0;
            selectedUserId = -1;
            btnSaveUserEdit.Enabled = false;
            LoadUsersGrid();
        }

        private void BtnDeleteUser_Click(object sender, EventArgs e)
        {
            if (selectedUserId == -1)
            {
                MessageBox.Show("من فضلك اختر مستخدم من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                UserRecord user = SettingsRepository.GetUser(selectedUserId);
                if (user == null)
                {
                    MessageBox.Show("لم يتم العثور على المستخدم.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (user.Username == AuthManager.CurrentUsername)
                {
                    MessageBox.Show("لا يمكنك حذف حسابك اللي داخل بيه دلوقتي.", "غير مسموح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (user.Role == "Admin" && SettingsRepository.CountAdmins() <= 1)
                {
                    MessageBox.Show("لا يمكن حذف آخر حساب أدمن في النظام.", "غير مسموح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show($"هل أنت متأكد من حذف المستخدم \"{user.Username}\"؟", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                SettingsRepository.DeleteUser(selectedUserId);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return; }

            MessageBox.Show("تم حذف المستخدم بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            selectedUserId = -1;
            txtNewUsername.Clear();
            txtNewUserPassword.Clear();
            cmbNewUserRole.SelectedIndex = 0;
            btnSaveUserEdit.Enabled = false;
            LoadUsersGrid();
        }

        // ==========================================================================
        // نفس تنسيق الجداول المستخدم في كل شاشات Form1.cs
        // ==========================================================================
        private void StyleDataGridView(DataGridView dgv) => UIHelpers.StyleDataGridView(dgv);
    }
}
