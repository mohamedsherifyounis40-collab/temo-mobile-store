using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Printing;
using Guna.UI2.WinForms;
using Microsoft.Data.Sqlite;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WinForms;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Measure;
using SkiaSharp;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // MainShell: الهيكل العام الرئيسي للتطبيق (Sidebar + Top Bar + Content Area)
    // كل الصفحات دلوقتي متربطة بـ UserControls حقيقية (SalesPageControl، InventoryPageControl، إلخ)
    // مش placeholders. الداشبورد بيقرأ بيانات حقيقية من قاعدة البيانات (مبيعات، مخزون، خزينة).
    // ==========================================================================
    public partial class MainShell : Form
    {
        // بيتحط true قبل ما الفورم تتقفل لو المستخدم دوس "تسجيل خروج" من الـ Sidebar،
        // عشان Program.cs يعرف يرجّع يفتح شاشة تسجيل الدخول تاني بدل ما يقفل البرنامج كله
        public bool LogoutRequested { get; private set; } = false;

        // ---------- مفاتيح الصفحات (بدل الـ Magic Strings) ----------
        // استخدام PageKeys.Sales بدل ما نكتب "Sales" كنص حر في أكتر من مكان، عشان لو غلطنا إملائيًا
        // الكومبايلر يوريه لينا خطأ فورًا بدل ما نكتشفه وقت التشغيل بس.
        public static class PageKeys
        {
            // ملحوظة: "Home" لسه بتاعة الداشبورد القديم عمدًا (راجع تعليق navItems) - كل باقي
            // الشاشات القديمة اتشالت خالص والمفاتيح الجديدة (Blazor) بقت هي الأساسية.
            public const string Home = "Home";
            public const string HomeBlazor = "HomeBlazor";
            public const string SalesBlazor = "SalesBlazor";
            public const string InventoryBlazor = "InventoryBlazor";
            public const string InventoryCountBlazor = "InventoryCountBlazor";
            public const string MaintenanceBlazor = "MaintenanceBlazor";
            public const string CustomersBlazor = "CustomersBlazor";
            public const string SuppliersBlazor = "SuppliersBlazor";
            public const string PurchasesBlazor = "PurchasesBlazor";
            public const string TreasuryBlazor = "TreasuryBlazor";
            public const string ReportsBlazor = "ReportsBlazor";
            public const string AccountsBlazor = "AccountsBlazor";
            public const string AttendanceBlazor = "AttendanceBlazor";
            public const string SettingsBlazor = "SettingsBlazor";
        }

        // نفس الـ Connection String المستخدم في Form1.cs بالظبط عشان نقرأ من نفس قاعدة البيانات
        // ---------- ألوان الهيكل العام (مسحوبة من UIHelpers المركزية عشان تفضل موحدة مع باقي الشاشات) ----------
        private static readonly Color ColorSidebarBg = UIHelpers.ColorSidebar;           // كحلي غامق جدًا - خلفية الـ Sidebar
        private static readonly Color ColorSidebarActive = UIHelpers.ColorAccentPrimary; // أزرق - العنصر المفعّل حاليًا
        private static readonly Color ColorSidebarHover = Color.FromArgb(28, 42, 68);    // درجة أفتح شوية من خلفية الـ Sidebar - تفاعل الماوس (Hover)
        private static readonly Color ColorSidebarText = Color.FromArgb(190, 200, 218);  // رمادي فاتح - نص العناصر غير المفعّلة
        private static readonly Color ColorTopBarBg = Color.White;
        private static readonly Color ColorContentBg = UIHelpers.ColorBackgroundApp;
        private static readonly Color ColorTitleText = UIHelpers.ColorPrimary;

        // ---------- حالة طي/فرد الـ Sidebar ----------
        private const int SidebarExpandedWidth = 220;
        private const int SidebarCollapsedWidth = 68;
        private bool sidebarCollapsed = false;
        private System.Windows.Forms.Timer sidebarAnimTimer;
        private Guna2Button btnSidebarToggle;
        private Label lblSidebarLogo;
        private readonly Dictionary<Guna2Button, string> sidebarButtonFullText = new Dictionary<Guna2Button, string>();
        private readonly ToolTip sidebarToolTip = new ToolTip();
        private Panel pnlSidebarFooter;
        private Label lblFooterName, lblFooterRole;
        private Guna2Button btnLogout;

        // ---------- بحث سريع في الهيدر ----------
        private Guna2TextBox txtHeaderSearch;
        private System.Windows.Forms.Timer headerSearchDebounceTimer;
        private Form headerSearchPopup;

        // ---------- تايمرات عدّاد كروت الـ KPI - لازم توقف وتتشال قبل كل إعادة بناء للداشبورد ----------
        private readonly List<System.Windows.Forms.Timer> activeKpiCounterTimers = new List<System.Windows.Forms.Timer>();

        private static readonly string[] AllPaymentMethods = UIHelpers.PaymentMethods;

        // ---------- عناصر الهيكل الرئيسية ----------
        private Panel pnlSidebar;
        private Panel pnlTopBar;
        private Panel pnlContent;
        private Label lblPageTitle;
        private Label lblDate;
        private Label lblBell;
        private Label lblBellBadge;

        private readonly List<Guna2Button> sidebarButtons = new List<Guna2Button>();
        private Guna2Button activeSidebarButton;
        private string currentPageKey = null;

        // نافذة مستقلة لكل صفحة (غير الرئيسية) بتتفتح لما تُضغط من السايدبار.
        // مفتاح الصفحة → الفورم المفتوح ليها حاليًا، عشان لو اتضغطت تاني وهي مفتوحة نجيبها لقدام بدل ما نفتح نسخة جديدة.
        private readonly Dictionary<string, Form> openPageWindows = new Dictionary<string, Form>();

        // تايمر التحديث التلقائي: بيحدّث جرس الإشعارات، وكمان يعيد بناء صفحة الداشبورد
        // لو هي المفتوحة دلوقتي، عشان الأرقام تفضل حديثة من غير ما المستخدم يغيّر الصفحة يدويًا
        private System.Windows.Forms.Timer autoRefreshTimer;

        // ---------- بيانات عناصر القائمة الجانبية (المفتاح، النص الظاهر، الأيقونة) ----------
        // ملحوظة: ده مجرد تعريف مؤقت للتجربة. في خطوة 4 هنربط كل مفتاح بالـ UserControl الحقيقي بتاعه.
        private readonly (string Key, string Text, string Icon)[] navItems = new (string, string, string)[]
        {
            // ملحوظة: "الرئيسية" لسه بتاعة الداشبورد القديم عمدًا (مش Blazor) - هو الوحيد
            // اللي معماريته مختلفة عن باقي الشاشات (مبني جوه pnlContent نفسه وبيتحدّث كل دقيقة
            // تلقائيًا بدل ما يبقى نافذة منفصلة زي الباقي)، فسيبناه لحد ما يتراجع بعناية في مرحلة تانية.
            (PageKeys.Home,           "الرئيسية",   "🏠"),
            (PageKeys.HomeBlazor,     "الرئيسية (تجربة) 🧪", "🧪"),
            (PageKeys.SalesBlazor,    "المبيعات",   "🛒"),
            (PageKeys.InventoryBlazor,"المخزون",    "🗄️"),
            (PageKeys.InventoryCountBlazor, "جرد المخزن", "📋"),
            (PageKeys.MaintenanceBlazor,"الصيانة",    "🔧"),
            (PageKeys.CustomersBlazor,"العملاء",   "👥"),
            (PageKeys.SuppliersBlazor,"الموردين",   "🚚"),
            (PageKeys.PurchasesBlazor,"المشتريات", "🧾"),
            (PageKeys.TreasuryBlazor, "الخزينة",    "🏦"),
            (PageKeys.ReportsBlazor,  "التقارير",   "📊"),
            (PageKeys.AccountsBlazor, "الحسابات",   "📒"),
            (PageKeys.AttendanceBlazor,"الحضور والمرتبات", "🧑‍💼"),
            (PageKeys.SettingsBlazor, "الإعدادات",  "⚙️"),
        };

        // ==========================================================================
        // MainShell عمليًا Singleton - نسخة واحدة بس حية في كل لحظة (راجع حلقة تسجيل
        // الدخول/الخروج في Program.cs). ده مرجع ثابت ليها عشان أي نافذة صفحة تانية (زي
        // نافذة المبيعات المستقلة) تقدر تفتح صفحة تانية بنفس منطق الـ Sidebar الأصلي
        // (OpenPageWindow/openPageWindows) من غير ما نكرر المنطق ده في كل شاشة.
        // ==========================================================================
        public static MainShell Instance { get; private set; }

        public MainShell()
        {
            Instance = this;
            this.FormClosed += (s, e) => { if (Instance == this) Instance = null; };
            InitializeShell();
        }

        // بتفتح/تفعّل صفحة تانية بمفتاحها (نفس اللي بيحصل لو دُست على بند القائمة الجانبية
        // الأصلي بالظبط) - مستخدمة من القائمة الجانبية المكرّرة جوه نافذة المبيعات المستقلة.
        public void OpenPageByKey(string pageKey) => NavigateToPageKey(pageKey);

        // ==========================================================================
        // دالة مساعدة موحّدة لفتح اتصال بقاعدة البيانات - بدل ما نكرر
        // "new SqliteConnection(AuthManager.ConnectionString)" + "conn.Open()" في كل دالة على حدة
        // ==========================================================================
        private SqliteConnection OpenConnection()
        {
            SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString);
            conn.Open();
            return conn;
        }

        // ==========================================================================
        // تجهيز الهيكل العام كله
        // ==========================================================================
        private void InitializeShell()
        {
            RemoteDashboardServer.Start();
            WebCatalogSyncService.Start();

            this.Text = "Temo Mobile Store" + (AuthManager.IsAdmin ? "" : " - وضع الموظف 👤");
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            this.Size = new Size(1366, 768);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Maximized;
            // ملحوظة مهمة: بنسيب RightToLeft = Yes عشان النصوص العربية تتصف صح جوه كل عنصر،
            // بس RightToLeftLayout بنخليها false عمدًا. لو سيبناها true، فورم الويندوز بيعمل
            // "قلب" (Mirror) كامل للتخطيط (Sidebar اللي حطيناه Dock.Left هيظهر فعليًا على اليمين
            // مش الشمال). إحنا عايزين نتحكم في المكان بالظبط زي التصميم في الصور (Sidebar شمال).
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = false;
            this.Font = new Font("Segoe UI", 9.5F);
            this.BackColor = ColorContentBg;
            this.KeyPreview = true;
            this.KeyDown += MainShell_KeyDown;

            BuildContentArea(); // هنضيفه الأول عشان ياخد الفراغ المتبقي صح (Dock.Fill)
            BuildTopBar();
            BuildSidebar();

            // ترتيب الإضافة لـ Controls بالشكل ده يضمن إن الداشبورد الرئيسي يتفتح افتراضيًا صح
            if (sidebarButtons.Count > 0)
                SidebarButton_Click(sidebarButtons[0], EventArgs.Empty);

            StartAutoRefreshTimer();
        }

        // ==========================================================================
        // بدء تايمر التحديث التلقائي (كل دقيقة) - جرس الإشعارات + الداشبورد لو مفتوح
        // ==========================================================================
        private void StartAutoRefreshTimer()
        {
            autoRefreshTimer = new System.Windows.Forms.Timer { Interval = 60000 }; // كل 60 ثانية
            autoRefreshTimer.Tick += (s, e) =>
            {
                RefreshNotificationBadge();
                // الداشبورد بقى دايمًا هو محتوى الشاشة الرئيسية تحت النوافذ التانية، فبيتحدّث كل تيك بغض النظر عن آخر تاب اتضغط
                BuildHomeDashboardPage();
            };
            autoRefreshTimer.Start();

            // إيقاف التايمر لما الفورم تتقفل عشان منسيبش حاجة شغالة في الخلفية من غير داعي
            // + نسخة احتياطية تلقائية عند قفل البرنامج
            this.FormClosed += (s, e) =>
            {
                autoRefreshTimer.Stop();
                WebCatalogSyncService.Stop();
                try
                {
                    BackupManager.CreateBackupFile();
                    BackupManager.PruneOldBackups();
                }
                catch { }
            };
        }

        // ==========================================================================
        // بناء القائمة الجانبية (Sidebar)
        // ==========================================================================
        private void BuildSidebar()
        {
            pnlSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = SidebarExpandedWidth,
                BackColor = ColorSidebarBg
            };

            // شعار المتجر أعلى الـ Sidebar
            lblSidebarLogo = new Label
            {
                Text = "📱 TEMO\nMOBILE STORE",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 90
            };

            // العناصر المخصصة للأدمن بس - نفس القائمة بالظبط اللي في Form1.cs (ApplyEmployeeRestrictions)
            var adminOnlyKeys = new HashSet<string> { PageKeys.CustomersBlazor, PageKeys.SuppliersBlazor, PageKeys.PurchasesBlazor, PageKeys.ReportsBlazor, PageKeys.AccountsBlazor, PageKeys.SettingsBlazor, PageKeys.InventoryCountBlazor, PageKeys.AttendanceBlazor };
            var visibleNavItems = AuthManager.IsAdmin
                ? navItems
                : navItems.Where(x => !adminOnlyKeys.Contains(x.Key)).ToArray();

            // حاوية أزرار التنقل
            Panel pnlNavButtons = new Panel
            {
                Dock = DockStyle.Top,
                Height = visibleNavItems.Length * 46,
                AutoScroll = false
            };

            // بنضيف الأزرار بترتيب عكسي عشان الـ Dock.Top يرصّهم من فوق لتحت بالترتيب الصحيح
            for (int i = visibleNavItems.Length - 1; i >= 0; i--)
            {
                var item = visibleNavItems[i];
                string fullText = "      " + item.Icon + "   " + item.Text;
                Guna2Button btn = new Guna2Button
                {
                    Text = fullText,
                    Dock = DockStyle.Top,
                    Height = 46,
                    TextAlign = HorizontalAlignment.Left,
                    Font = new Font("Segoe UI", 10.5F),
                    ForeColor = ColorSidebarText,
                    FillColor = ColorSidebarBg,
                    BorderRadius = 0,
                    Tag = item.Key
                };
                btn.Click += SidebarButton_Click;
                btn.MouseEnter += (s, e) => { if (btn != activeSidebarButton) btn.FillColor = ColorSidebarHover; };
                btn.MouseLeave += (s, e) => { if (btn != activeSidebarButton) btn.FillColor = ColorSidebarBg; };
                sidebarToolTip.SetToolTip(btn, item.Text);
                sidebarButtonFullText[btn] = fullText;
                sidebarButtons.Insert(0, btn);
                pnlNavButtons.Controls.Add(btn);
            }

            pnlSidebarFooter = BuildSidebarFooter();

            // ترتيب الإضافة هنا مهم: اللوجو Dock.Top الأول، بعدين الأزرار تحته مباشرة، والفوتر Dock.Bottom
            pnlSidebar.Controls.Add(pnlNavButtons);
            pnlSidebar.Controls.Add(pnlSidebarFooter);
            pnlSidebar.Controls.Add(lblSidebarLogo);

            this.Controls.Add(pnlSidebar);
        }

        // ==========================================================================
        // فوتر الـ Sidebar: صورة/حروف المستخدم + اسمه + رتبته + زرار تسجيل الخروج
        // ==========================================================================
        private Panel BuildSidebarFooter()
        {
            Panel footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 112,
                BackColor = ColorSidebarHover
            };

            string username = AuthManager.CurrentUsername ?? "مستخدم";
            string initials = username.Length >= 2 ? username.Substring(0, 2) : username.Substring(0, Math.Min(1, username.Length));

            Guna2Panel avatar = new Guna2Panel
            {
                Size = new Size(36, 36),
                Location = new Point(16, 14),
                BorderRadius = 18,
                FillColor = UIHelpers.ColorAccentPrimary
            };
            Label lblAvatarText = new Label
            {
                Text = initials.ToUpper(),
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            avatar.Controls.Add(lblAvatarText);

            lblFooterName = new Label
            {
                Text = username,
                Location = new Point(62, 14),
                Size = new Size(140, 18),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            lblFooterRole = new Label
            {
                Text = AuthManager.IsAdmin ? "مدير" : "موظف",
                Location = new Point(62, 33),
                Size = new Size(140, 16),
                ForeColor = ColorSidebarText,
                Font = new Font("Segoe UI", 8F)
            };

            btnLogout = new Guna2Button
            {
                Text = "🚪  تسجيل خروج",
                Location = new Point(16, 62),
                Size = new Size(188, 34),
                TextAlign = HorizontalAlignment.Center,
                Font = new Font("Segoe UI", 9F),
                ForeColor = ColorSidebarText,
                FillColor = ColorSidebarBg,
                BorderColor = Color.FromArgb(60, 72, 96),
                BorderThickness = 1,
                BorderRadius = 8
            };
            sidebarButtonFullText[btnLogout] = btnLogout.Text;
            btnLogout.Click += BtnLogout_Click;

            footer.Controls.Add(avatar);
            footer.Controls.Add(lblFooterName);
            footer.Controls.Add(lblFooterRole);
            footer.Controls.Add(btnLogout);
            return footer;
        }

        private void BtnLogout_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("هل تريد تسجيل الخروج من البرنامج؟", "تأكيد تسجيل الخروج", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            LogoutRequested = true;
            this.Close();
        }

        // ==========================================================================
        // طي/فرد الـ Sidebar - تحريك العرض بتايمر بسيط (مفيش محرك أنيميشن حقيقي في WinForms)
        // بس النصوص بتتلخص لأيقونة بس لما يقفل، وترجع تاني لما يفتح
        // ==========================================================================
        private void ToggleSidebar()
        {
            sidebarCollapsed = !sidebarCollapsed;
            btnSidebarToggle.Text = sidebarCollapsed ? "»" : "«";

            // تبديل النصوص فورًا (مش هي اللي بتتحرك، بس عرض البانل)
            lblSidebarLogo.Text = sidebarCollapsed ? "📱" : "📱 TEMO\nMOBILE STORE";
            foreach (var btn in sidebarButtons)
            {
                if (sidebarCollapsed)
                {
                    string key = btn.Tag?.ToString() ?? "";
                    var navItem = Array.Find(navItems, x => x.Key == key);
                    btn.Text = navItem.Icon ?? "";
                }
                else
                {
                    btn.Text = sidebarButtonFullText[btn];
                }
                btn.TextAlign = sidebarCollapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            }
            lblFooterName.Visible = !sidebarCollapsed;
            lblFooterRole.Visible = !sidebarCollapsed;
            btnLogout.Text = sidebarCollapsed ? "🚪" : sidebarButtonFullText[btnLogout];
            btnLogout.Size = sidebarCollapsed ? new Size(36, 34) : new Size(188, 34);

            int targetWidth = sidebarCollapsed ? SidebarCollapsedWidth : SidebarExpandedWidth;

            sidebarAnimTimer?.Stop();
            sidebarAnimTimer = new System.Windows.Forms.Timer { Interval = 15 };
            sidebarAnimTimer.Tick += (s, e) =>
            {
                int current = pnlSidebar.Width;
                int diff = targetWidth - current;
                if (Math.Abs(diff) <= 4)
                {
                    pnlSidebar.Width = targetWidth;
                    sidebarAnimTimer.Stop();
                    sidebarAnimTimer.Dispose();
                    return;
                }
                pnlSidebar.Width = current + diff / 3;
            };
            sidebarAnimTimer.Start();
        }

        // ==========================================================================
        // بناء الشريط العلوي (Top Bar)
        // ==========================================================================
        private void BuildTopBar()
        {
            pnlTopBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = ColorTopBarBg
            };

            lblPageTitle = new Label
            {
                Text = "الرئيسية",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = ColorTitleText,
                AutoSize = true,
                Location = new Point(20, 16),
                Visible = false // مخفي عشان الهيدر يفضل نضيف زي التصميم المرجعي - اسم الصفحة أصلاً واضح من تظليل الـ Sidebar
            };

            // ---------- زرار طي/فرد الـ Sidebar (☰) - أقصى شمال الهيدر ----------
            Panel pnlToggleHolder = new Panel { Dock = DockStyle.Left, Width = 52 };
            btnSidebarToggle = new Guna2Button
            {
                Text = "☰",
                Size = new Size(34, 34),
                Location = new Point(9, 13),
                Font = new Font("Segoe UI", 11F),
                ForeColor = ColorTitleText,
                FillColor = Color.FromArgb(245, 246, 250),
                BorderRadius = 9
            };
            btnSidebarToggle.Click += (s, e) => ToggleSidebar();
            pnlToggleHolder.Controls.Add(btnSidebarToggle);

            string arabicDate;
            try
            {
                CultureInfo arCulture = new CultureInfo("ar-EG");
                arabicDate = DateTime.Now.ToString("dddd d MMMM yyyy", arCulture);
            }
            catch
            {
                arabicDate = DateTime.Now.ToString("yyyy-MM-dd");
            }

            // ---------- بحث سريع (منتجات وعملاء) - Dock.Left بعد زرار الـ ☰ مباشرة ----------
            Panel pnlSearchHolder = new Panel { Dock = DockStyle.Left, Width = 330 };
            txtHeaderSearch = new Guna2TextBox
            {
                Location = new Point(0, 13),
                Size = new Size(310, 34),
                PlaceholderText = "🔍 ابحث عن منتج، عميل، فاتورة...",
                BorderRadius = 10,
                FillColor = Color.FromArgb(245, 246, 250)
            };
            txtHeaderSearch.TextChanged += TxtHeaderSearch_TextChanged;
            pnlSearchHolder.Controls.Add(txtHeaderSearch);

            // ---------- تجمّع أقصى يمين الهيدر: صورة/اسم المستخدم (الحافة الحقيقية) ثم الجرس ثم التاريخ ثم بادچ الترخيص ----------
            Panel pnlHeaderUser = new Panel { Dock = DockStyle.Right, Width = 190 };
            string headerInitials = (AuthManager.CurrentUsername ?? "?").Length >= 1 ? (AuthManager.CurrentUsername ?? "?").Substring(0, 1) : "?";
            Guna2Panel headerAvatar = new Guna2Panel { Size = new Size(38, 38), Location = new Point(12, 11), BorderRadius = 19, FillColor = UIHelpers.ColorAccentPrimary };
            Label lblHeaderAvatarText = new Label { Text = headerInitials.ToUpper(), Dock = DockStyle.Fill, ForeColor = Color.White, Font = new Font("Segoe UI", 11F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };
            headerAvatar.Controls.Add(lblHeaderAvatarText);
            Label lblHeaderUserName = new Label { Text = AuthManager.CurrentUsername ?? "مستخدم", Location = new Point(58, 12), Size = new Size(120, 18), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = ColorTitleText };
            Label lblHeaderUserRole = new Label { Text = AuthManager.IsAdmin ? "مدير النظام" : "موظف", Location = new Point(58, 30), Size = new Size(120, 14), Font = new Font("Segoe UI", 7.5F), ForeColor = Color.FromArgb(120, 126, 138) };
            pnlHeaderUser.Controls.Add(headerAvatar);
            pnlHeaderUser.Controls.Add(lblHeaderUserName);
            pnlHeaderUser.Controls.Add(lblHeaderUserRole);

            // بانل الجرس
            Panel pnlBellHolder = new Panel { Dock = DockStyle.Right, Width = 55 };
            lblBell = new Label
            {
                Text = "🔔",
                Font = new Font("Segoe UI", 13F),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Cursor = Cursors.Hand
            };
            lblBell.Click += LblBell_Click;
            pnlBellHolder.Controls.Add(lblBell);

            lblBellBadge = new Label
            {
                Text = "0",
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(231, 76, 60),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(16, 16),
                Location = new Point(6, 8),
                Visible = false
            };
            pnlBellHolder.Controls.Add(lblBellBadge);
            lblBellBadge.BringToFront();

            // بانل التاريخ
            Panel pnlDateHolder = new Panel { Dock = DockStyle.Right, Width = 205 };
            lblDate = new Label
            {
                Text = "📅 " + arabicDate,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(85, 92, 102),
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill
            };
            pnlDateHolder.Controls.Add(lblDate);

            Panel pnlLicenseHolder = BuildLicenseBadge();

            pnlTopBar.Controls.Add(lblPageTitle);
            pnlTopBar.Controls.Add(pnlHeaderUser);
            pnlTopBar.Controls.Add(pnlBellHolder);
            pnlTopBar.Controls.Add(pnlDateHolder);
            pnlTopBar.Controls.Add(pnlLicenseHolder);
            pnlTopBar.Controls.Add(pnlToggleHolder);
            pnlTopBar.Controls.Add(pnlSearchHolder);
            this.Controls.Add(pnlTopBar);

            RefreshNotificationBadge();
        }

        // ==========================================================================
        // بحث سريع في الهيدر - منتجات وعملاء بس (Debounce بسيط عشان منعملش استعلام على كل ضغطة زرار)
        // ==========================================================================
        private void TxtHeaderSearch_TextChanged(object sender, EventArgs e)
        {
            headerSearchDebounceTimer?.Stop();
            headerSearchDebounceTimer?.Dispose();

            string term = txtHeaderSearch.Text.Trim();
            headerSearchPopup?.Close();

            if (term.Length < 2) return;

            headerSearchDebounceTimer = new System.Windows.Forms.Timer { Interval = 250 };
            headerSearchDebounceTimer.Tick += (s, e2) =>
            {
                headerSearchDebounceTimer.Stop();
                RunHeaderQuickSearch(term);
            };
            headerSearchDebounceTimer.Start();
        }

        private void RunHeaderQuickSearch(string term)
        {
            var products = new List<(string Barcode, string Name)>();
            var customers = new List<(int Id, string Name, string Phone)>();

            try
            {
                using (SqliteConnection conn = OpenConnection())
                {
                    using (SqliteCommand cmd = new SqliteCommand(
                        "SELECT Barcode, ProductName FROM Products WHERE ProductName LIKE @q OR Barcode LIKE @q LIMIT 6", conn))
                    {
                        cmd.Parameters.AddWithValue("@q", "%" + term + "%");
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                products.Add((reader["Barcode"].ToString(), reader["ProductName"].ToString()));
                        }
                    }

                    using (SqliteCommand cmd = new SqliteCommand(
                        "SELECT CustomerId, CustomerName, Phone FROM Customers WHERE CustomerName LIKE @q OR Phone LIKE @q LIMIT 6", conn))
                    {
                        cmd.Parameters.AddWithValue("@q", "%" + term + "%");
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                customers.Add((Convert.ToInt32(reader["CustomerId"]), reader["CustomerName"].ToString(), reader["Phone"] == DBNull.Value ? "" : reader["Phone"].ToString()));
                        }
                    }
                }
            }
            catch { return; }

            ShowHeaderSearchPopup(products, customers);
        }

        private void ShowHeaderSearchPopup(List<(string Barcode, string Name)> products, List<(int Id, string Name, string Phone)> customers)
        {
            headerSearchPopup?.Close();

            if (products.Count == 0 && customers.Count == 0) return;

            Point screenPoint = txtHeaderSearch.PointToScreen(new Point(0, txtHeaderSearch.Height + 2));

            Form popup = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Location = screenPoint,
                Size = new Size(300, 40),
                BackColor = Color.White,
                RightToLeft = RightToLeft.Yes,
                ShowInTaskbar = false
            };
            popup.Deactivate += (s, e) => popup.Close();
            headerSearchPopup = popup;

            Guna2Panel border = new Guna2Panel { Dock = DockStyle.Fill, FillColor = Color.White, BorderRadius = 10, BorderColor = Color.FromArgb(220, 222, 228), BorderThickness = 1 };
            popup.Controls.Add(border);

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Location = new Point(6, 6),
                Size = new Size(288, 20),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };

            Label MakeRow(string icon, string text, EventHandler onClick)
            {
                Label lbl = new Label
                {
                    Text = icon + "  " + text,
                    Width = 270,
                    Height = 32,
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = ColorTitleText,
                    TextAlign = ContentAlignment.MiddleRight,
                    Padding = new Padding(6, 0, 6, 0),
                    Cursor = Cursors.Hand
                };
                lbl.Click += onClick;
                lbl.MouseEnter += (s, e) => lbl.BackColor = Color.FromArgb(245, 246, 250);
                lbl.MouseLeave += (s, e) => lbl.BackColor = Color.White;
                return lbl;
            }

            foreach (var p in products)
            {
                string barcode = p.Barcode;
                flow.Controls.Add(MakeRow("📦", p.Name, (s, e) =>
                {
                    popup.Close();
                    txtHeaderSearch.Clear();
                    NavigateToPageKey(PageKeys.InventoryBlazor, highlightKey: barcode);
                }));
            }
            foreach (var c in customers)
            {
                int customerId = c.Id;
                flow.Controls.Add(MakeRow("👤", c.Name + (string.IsNullOrEmpty(c.Phone) ? "" : " - " + c.Phone), (s, e) =>
                {
                    popup.Close();
                    txtHeaderSearch.Clear();
                    NavigateToPageKey(PageKeys.CustomersBlazor, highlightKey: customerId.ToString());
                }));
            }

            border.Controls.Add(flow);

            int rowCount = products.Count + customers.Count;
            popup.Height = Math.Min(rowCount * 32 + 12, 300);
            flow.Size = new Size(288, popup.Height - 12);

            popup.Show(this);
        }

        // ==========================================================================
        // بادچ مدة انتهاء الترخيص في الشريط العلوي - لونه بيتغيّر حسب قرب الانتهاء
        // (البرنامج أصلاً مبيوصلش لـ MainShell غير لو الترخيص سليم وقت التشغيل،
        // فده مجرد عرض تذكيري بيبقى شكله شيك، مش بوابة فحص تانية)
        // ==========================================================================
        private Panel BuildLicenseBadge()
        {
            Panel holder = new Panel { Dock = DockStyle.Right, Width = 190 };

            if (!LicenseManager.CheckSavedLicense(out DateTime? expiration, out _) || !expiration.HasValue)
                return holder;

            int daysLeft = (expiration.Value.Date - DateTime.Now.Date).Days;
            Color accent;
            string icon, text;

            if (daysLeft < 0)
            {
                accent = Color.FromArgb(231, 76, 60);
                icon = "⛔";
                text = "الترخيص منتهي";
            }
            else if (daysLeft <= 7)
            {
                accent = Color.FromArgb(231, 76, 60);
                icon = "⚠️";
                text = $"باقي {daysLeft} يوم على الترخيص";
            }
            else if (daysLeft <= 30)
            {
                accent = Color.FromArgb(243, 156, 18);
                icon = "⏳";
                text = $"باقي {daysLeft} يوم على الترخيص";
            }
            else
            {
                accent = Color.FromArgb(39, 174, 96);
                icon = "✅";
                text = "الترخيص ساري";
            }

            Guna2Panel pill = new Guna2Panel
            {
                Size = new Size(175, 34),
                Location = new Point(8, 13),
                BorderRadius = 17,
                FillColor = LightTint(accent, 0.88f),
                BorderColor = accent,
                BorderThickness = 1
            };
            Label lblBadge = new Label
            {
                Text = $"{icon} {text}",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = accent,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            var tip = new ToolTip();
            tip.SetToolTip(lblBadge, "تاريخ انتهاء الترخيص: " + expiration.Value.ToString("yyyy-MM-dd"));

            pill.Controls.Add(lblBadge);
            holder.Controls.Add(pill);
            return holder;
        }

        // ==========================================================================
        // الإشعارات: أصناف قاربت تخلص، صيانة جاهزة للتسليم من فترة، عملاء عليهم مديونية
        // ==========================================================================
        private List<(string Icon, string Text)> GetNotificationItems()
        {
            var items = new List<(string Icon, string Text)>();

            using (SqliteConnection conn = OpenConnection())
            {
                try
                {
                    using (SqliteCommand cmd = new SqliteCommand("SELECT ProductName, Quantity FROM Products WHERE Quantity <= 3 ORDER BY Quantity ASC LIMIT 8", conn))
                    {
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int qty = Convert.ToInt32(reader["Quantity"]);
                                string label = qty <= 0 ? "نفدت الكمية" : $"باقي {qty} بس";
                                items.Add(("📦", $"{reader["ProductName"]} — {label}"));
                            }
                        }
                    }

                    using (SqliteCommand cmd = new SqliteCommand("SELECT CustomerName, DeviceInfo, ReceivedDate FROM MaintenanceTickets WHERE Status = 'جاهز للتسليم' ORDER BY ReceivedDate ASC LIMIT 8", conn))
                    {
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                items.Add(("🔧", $"جهاز {reader["CustomerName"]} ({reader["DeviceInfo"]}) جاهز للتسليم"));
                        }
                    }

                    using (SqliteCommand cmd = new SqliteCommand(@"
                        SELECT C.CustomerName, 
                               (COALESCE((SELECT SUM(Total) FROM Sales WHERE CustomerId = C.CustomerId AND PaymentType = 'Credit'), 0) 
                                - COALESCE((SELECT SUM(Amount) FROM CashMovements WHERE CustomerId = C.CustomerId AND MovementType = 'قبض'), 0)) AS Balance
                        FROM Customers C", conn))
                    {
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                decimal balance = Convert.ToDecimal(reader["Balance"]);
                                if (balance > 0)
                                    items.Add(("💰", $"{reader["CustomerName"]} عليه {balance:N2} ج.م"));
                            }
                        }
                    }
                }
                catch { /* الإشعارات مش لازم توقف باقي الشاشة لو حصل خطأ بسيط */ }
            }

            return items;
        }

        private void RefreshNotificationBadge()
        {
            int count = GetNotificationItems().Count;
            lblBellBadge.Text = count > 9 ? "9+" : count.ToString();
            lblBellBadge.Visible = count > 0;
        }

        private void LblBell_Click(object sender, EventArgs e)
        {
            var items = GetNotificationItems();
            RefreshNotificationBadge();

            Point screenPoint = lblBell.PointToScreen(new Point(0, lblBell.Height));
            ShowNotificationsPopup(items, screenPoint);
        }

        private void ShowNotificationsPopup(List<(string Icon, string Text)> items, Point screenLocation)
        {
            Form popup = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Location = screenLocation,
                Size = new Size(360, 420),
                BackColor = Color.White,
                RightToLeft = RightToLeft.Yes,
                ShowInTaskbar = false
            };
            popup.Deactivate += (s, e) => popup.Close();

            Guna2Panel border = new Guna2Panel { Dock = DockStyle.Fill, FillColor = Color.White, BorderRadius = 12, BorderColor = Color.FromArgb(220, 222, 228), BorderThickness = 1 };
            popup.Controls.Add(border);

            Label lblTitle = new Label { Text = "🔔 الإشعارات", Location = new Point(15, 12), AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = ColorTitleText };
            border.Controls.Add(lblTitle);

            if (items.Count == 0)
            {
                Label lblEmpty = new Label { Text = "مفيش إشعارات جديدة دلوقتي 👍", Location = new Point(15, 50), AutoSize = true, ForeColor = Color.FromArgb(85, 92, 102) };
                border.Controls.Add(lblEmpty);
                popup.Height = 100;
            }
            else
            {
                FlowLayoutPanel flow = new FlowLayoutPanel
                {
                    Location = new Point(10, 45),
                    Size = new Size(340, 365),
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    AutoScroll = true
                };
                foreach (var item in items)
                {
                    Label lblItem = new Label
                    {
                        Text = item.Icon + "  " + item.Text,
                        Width = 320,
                        AutoSize = false,
                        Height = 34,
                        Font = new Font("Segoe UI", 9F),
                        ForeColor = ColorTitleText,
                        TextAlign = ContentAlignment.MiddleRight,
                        Padding = new Padding(5, 0, 5, 0)
                    };
                    flow.Controls.Add(lblItem);
                }
                border.Controls.Add(flow);
            }

            popup.Show(this);
        }

        // ==========================================================================
        // بناء منطقة المحتوى (اللي هتتغير محتوياتها حسب الصفحة المختارة)
        // ==========================================================================
        private void BuildContentArea()
        {
            pnlContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorContentBg,
                Padding = new Padding(16),
                AutoScroll = true
            };

            this.Controls.Add(pnlContent);
        }

        // ==========================================================================
        // عند الضغط على أي زرار في القائمة الجانبية
        // ==========================================================================
        private void SidebarButton_Click(object sender, EventArgs e)
        {
            Guna2Button clickedButton = sender as Guna2Button;
            if (clickedButton == null) return;
            string pageKey = clickedButton.Tag?.ToString() ?? "";
            NavigateToPageKey(pageKey);
        }

        // ==========================================================================
        // الملاحة الموحّدة - بتُستخدم من ضغطة السايدبار وكمان من الاختصارات السريعة (F2/F4/F5/F6)
        // ==========================================================================
        private void NavigateToPageKey(string pageKey, string presetMovementType = null, string highlightKey = null, bool landingMode = false)
        {
            Guna2Button targetButton = sidebarButtons.FirstOrDefault(b => (b.Tag?.ToString() ?? "") == pageKey);
            if (targetButton == null) return; // الصفحة مش متاحة (مخفية عن الموظف مثلاً)

            if (activeSidebarButton != null)
            {
                activeSidebarButton.FillColor = ColorSidebarBg;
                activeSidebarButton.ForeColor = ColorSidebarText;
            }
            targetButton.FillColor = ColorSidebarActive;
            targetButton.ForeColor = Color.White;
            activeSidebarButton = targetButton;

            var matchedItem = Array.Find(navItems, x => x.Key == pageKey);
            lblPageTitle.Text = matchedItem.Text;
            currentPageKey = pageKey;

            if (pageKey == PageKeys.Home)
                BuildHomeDashboardPage();
            else if (pageKey == PageKeys.HomeBlazor)
                BuildHomeBlazorPage();
            else if (pageKey == PageKeys.SalesBlazor)
                BuildSalesBlazorPage();
            else if (pageKey == PageKeys.InventoryBlazor)
                BuildInventoryBlazorPage();
            else if (pageKey == PageKeys.InventoryCountBlazor)
                BuildInventoryCountBlazorPage();
            else if (pageKey == PageKeys.CustomersBlazor)
                BuildCustomersBlazorPage();
            else if (pageKey == PageKeys.MaintenanceBlazor)
                BuildMaintenanceBlazorPage();
            else if (pageKey == PageKeys.TreasuryBlazor)
                BuildTreasuryBlazorPage();
            else if (pageKey == PageKeys.SuppliersBlazor)
                BuildSuppliersBlazorPage();
            else if (pageKey == PageKeys.PurchasesBlazor)
                BuildPurchasesBlazorPage();
            else if (pageKey == PageKeys.ReportsBlazor)
                BuildReportsBlazorPage();
            else if (pageKey == PageKeys.AccountsBlazor)
                BuildAccountsBlazorPage();
            else if (pageKey == PageKeys.AttendanceBlazor)
                BuildAttendanceBlazorPage();
            else if (pageKey == PageKeys.SettingsBlazor)
                BuildSettingsBlazorPage();
            else
                ShowPlaceholderPage(matchedItem.Text, matchedItem.Icon);
        }

        // ==========================================================================
        // بيفتح صفحة تاب السايدبار (غير الرئيسية) في نافذة مستقلة بدل ما تستبدل محتوى الشاشة الرئيسية.
        // لو الصفحة دي مفتوحة بالفعل في نافذة، بيجيبها لقدام (Activate/BringToFront) بدل ما يفتح نسخة جديدة.
        // ==========================================================================
        private Form OpenPageWindow(string pageKey, string title, Func<Control> createControl)
        {
            if (openPageWindows.TryGetValue(pageKey, out Form existing) && !existing.IsDisposed)
            {
                if (existing.WindowState == FormWindowState.Minimized)
                    existing.WindowState = FormWindowState.Normal;
                existing.Activate();
                existing.BringToFront();
                return existing;
            }

            Form pageForm = new Form
            {
                Text = title,
                StartPosition = FormStartPosition.Manual,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                Font = this.Font, // لازم نفس فونت MainShell بالظبط، وإلا الـ AutoScale بتاع الفورم بيلخبط كل الإحداثيات الثابتة جوه الصفحة (تراكب اللابلز، وأعمدة الجدول بتختفي)
                Owner = this, // عشان تتقفل تلقائيًا لو البرنامج الرئيسي اتقفل
                KeyPreview = true, // عشان اختصارات F2/F4/F5/F6 وCtrl+F (البحث الشامل) تشتغل من جوه النافذة دي كمان، مش بس من MainShell نفسه
            };
            pageForm.KeyDown += MainShell_KeyDown;
            try { pageForm.Icon = this.Icon; } catch { }

            // بنحط الأبعاد يدويًا على مساحة الشاشة الفعلية (WorkingArea، يعني من غير الـ Taskbar)
            // بدل الاعتماد على WindowState.Maximized قبل ما الفورم يتعرض، عشان ده بيسبب أحيانًا
            // مقاسات غلط (خصوصًا مع الفورم المالكة Owner) بدل ما ياخد المساحة كاملة فعلًا
            pageForm.Bounds = Screen.FromControl(this).WorkingArea;

            Control pageControl = createControl();
            pageControl.Dock = DockStyle.Fill;
            pageForm.Controls.Add(pageControl);

            pageForm.FormClosed += (s, e) => openPageWindows.Remove(pageKey);
            openPageWindows[pageKey] = pageForm;
            pageForm.Show();
            return pageForm;
        }

        // ==========================================================================
        // بناء صفحة "الإعدادات" التجريبية (Blazor Hybrid) - SettingsPageBlazorHost
        // ==========================================================================
        private void BuildSettingsBlazorPage()
        {
            OpenPageWindow(PageKeys.SettingsBlazor, "الإعدادات", () => new SettingsPageBlazorHost());
        }

        // تجربة شاشة حسابات جديدة (Blazor Hybrid) - نافذة مستقلة منفصلة تمامًا عن شاشة
        // الحسابات الأصلية، بنفس نمط InventoryBlazor بالظبط (أدمن بس زي الأصلية)
        private void BuildAccountsBlazorPage()
        {
            OpenPageWindow(PageKeys.AccountsBlazor, "الحسابات", () => new AccountsPageBlazorHost());
        }

        // ==========================================================================
        // بناء صفحة "الحضور والمرتبات" التجريبية (Blazor Hybrid) - AttendancePageBlazorHost
        // ==========================================================================
        private void BuildAttendanceBlazorPage()
        {
            OpenPageWindow(PageKeys.AttendanceBlazor, "الحضور والمرتبات", () => new AttendancePageBlazorHost());
        }

        // ==========================================================================
        // بناء صفحة "التقارير" التجريبية (Blazor Hybrid) - ReportsPageBlazorHost
        // ==========================================================================
        private void BuildReportsBlazorPage()
        {
            OpenPageWindow(PageKeys.ReportsBlazor, "التقارير", () => new ReportsPageBlazorHost());
        }

        // تجربة شاشة جرد مخزن جديدة (Blazor Hybrid) - نافذة مستقلة منفصلة تمامًا عن شاشة
        // جرد المخزن الأصلية، بنفس نمط InventoryBlazor بالظبط (أدمن بس زي الأصلية)
        private void BuildInventoryCountBlazorPage()
        {
            OpenPageWindow(PageKeys.InventoryCountBlazor, "جرد المخزن", () => new InventoryCountPageBlazorHost());
        }

        // تجربة شاشة مبيعات جديدة (Blazor Hybrid) - نافذة مستقلة منفصلة تمامًا عن شاشة
        // المبيعات الأصلية، بنفس نمط فتح أي صفحة تانية بالظبط (OpenPageWindow)
        private void BuildSalesBlazorPage()
        {
            OpenPageWindow(PageKeys.SalesBlazor, "المبيعات (تجربة)", () => new SalesPageBlazorHost());
        }

        // تجربة شاشة رئيسية جديدة (Blazor Hybrid) - نافذة مستقلة منفصلة تمامًا عن داشبورد
        // الرئيسية الأصلي (اللي بيتبني جوه pnlContent نفسه)، بنفس نمط SalesBlazor بالظبط
        private void BuildHomeBlazorPage()
        {
            OpenPageWindow(PageKeys.HomeBlazor, "الرئيسية (تجربة)", () => new HomePageBlazorHost());
        }

        // تجربة شاشة مخزون جديدة (Blazor Hybrid) - نافذة مستقلة منفصلة تمامًا عن شاشة
        // المخزون الأصلية، بنفس نمط SalesBlazor/HomeBlazor بالظبط
        private void BuildInventoryBlazorPage()
        {
            OpenPageWindow(PageKeys.InventoryBlazor, "المخزون (تجربة)", () => new InventoryPageBlazorHost());
        }

        // تجربة شاشة عملاء جديدة (Blazor Hybrid) - نافذة مستقلة منفصلة تمامًا عن شاشة
        // العملاء الأصلية، بنفس نمط SalesBlazor/HomeBlazor/InventoryBlazor بالظبط
        private void BuildCustomersBlazorPage()
        {
            OpenPageWindow(PageKeys.CustomersBlazor, "العملاء (تجربة)", () => new CustomersPageBlazorHost());
        }

        // تجربة شاشة موردين جديدة (Blazor Hybrid) - نافذة مستقلة منفصلة تمامًا عن شاشة
        // الموردين الأصلية، بنفس نمط SalesBlazor/HomeBlazor/InventoryBlazor/CustomersBlazor بالظبط
        private void BuildSuppliersBlazorPage()
        {
            OpenPageWindow(PageKeys.SuppliersBlazor, "الموردين (تجربة)", () => new SuppliersPageBlazorHost());
        }

        // تجربة شاشة مشتريات جديدة (Blazor Hybrid) - مفيش شاشة قديمة مستقلة تتقارن بيها
        // (فاتورة الشراء كانت جوه شاشة الموردين بس)، بنفس نمط SalesBlazor بالظبط
        private void BuildPurchasesBlazorPage()
        {
            OpenPageWindow(PageKeys.PurchasesBlazor, "المشتريات (تجربة)", () => new PurchasesPageBlazorHost());
        }

        // تجربة شاشة صيانة جديدة (Blazor Hybrid) - نافذة مستقلة منفصلة تمامًا عن شاشة
        // الصيانة الأصلية، بنفس نمط InventoryBlazor بالظبط
        private void BuildMaintenanceBlazorPage()
        {
            OpenPageWindow(PageKeys.MaintenanceBlazor, "الصيانة (تجربة)", () => new MaintenancePageBlazorHost());
        }

        // تجربة شاشة خزينة جديدة (Blazor Hybrid) - نافذة مستقلة منفصلة تمامًا عن شاشة
        // الخزينة الأصلية، بنفس نمط InventoryBlazor بالظبط
        private void BuildTreasuryBlazorPage()
        {
            OpenPageWindow(PageKeys.TreasuryBlazor, "الخزينة (تجربة)", () => new TreasuryPageBlazorHost());
        }

        // ==========================================================================
        // بناء صفحة "الرئيسية" بكروت الـ KPI - بيانات حقيقية من قاعدة البيانات (لليوم الحالي)
        // ==========================================================================
        private void BuildHomeDashboardPage()
        {
            // لازم نوقف ونشيل تايمرات عدّاد الكروت القديمة قبل ما نمسح الكونترولز - التايمرات
            // مش Control فمش بتتشال تلقائيًا مع Controls.Clear()، ولو سبناها شغالة هتفضل تحاول
            // تحدّث Label اتشال بالفعل (Object لسه في الذاكرة) كل ما الداشبورد يتبني تاني كل 60 ثانية
            foreach (var t in activeKpiCounterTimers) { t.Stop(); t.Dispose(); }
            activeKpiCounterTimers.Clear();

            pnlContent.Controls.Clear();

            decimal todaySales = 0, todayCogs = 0, todayPurchases = 0, todayExpenses = 0;
            int invoiceCount = 0, maintenanceCount = 0;
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            try
            {
                using (SqliteConnection conn = OpenConnection())
                {
                    using (SqliteCommand cmd = new SqliteCommand(
                        "SELECT SUM(Total) AS T, SUM(CostPrice * QuantitySold) AS C, COUNT(*) AS N FROM Sales WHERE SaleDate LIKE @Today", conn))
                    {
                        cmd.Parameters.AddWithValue("@Today", today + "%");
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                todaySales = reader["T"] != DBNull.Value ? Convert.ToDecimal(reader["T"]) : 0;
                                todayCogs = reader["C"] != DBNull.Value ? Convert.ToDecimal(reader["C"]) : 0;
                                invoiceCount = reader["N"] != DBNull.Value ? Convert.ToInt32(reader["N"]) : 0;
                            }
                        }
                    }

                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(TotalAmount) FROM Purchases WHERE PurchaseDate LIKE @Today", conn))
                    {
                        cmd.Parameters.AddWithValue("@Today", today + "%");
                        var res = cmd.ExecuteScalar();
                        todayPurchases = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                    }

                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM Expenses WHERE ExpenseDate LIKE @Today", conn))
                    {
                        cmd.Parameters.AddWithValue("@Today", today + "%");
                        var res = cmd.ExecuteScalar();
                        todayExpenses = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                    }

                    using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM MaintenanceTickets WHERE ReceivedDate LIKE @Today", conn))
                    {
                        cmd.Parameters.AddWithValue("@Today", today + "%");
                        var res = cmd.ExecuteScalar();
                        maintenanceCount = (res != null && res != DBNull.Value) ? Convert.ToInt32(res) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("حصل خطأ أثناء تحميل بيانات لوحة التحكم: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            decimal todayProfit = todaySales - todayCogs - todayExpenses;

            // ---------- أرصدة وسائل الدفع (الخزن) ----------
            var methodBalances = new List<(string Method, decimal Balance)>();
            decimal totalBalance = 0;
            try
            {
                using (SqliteConnection conn = OpenConnection())
                {
                    foreach (string method in AllPaymentMethods)
                    {
                        using (SqliteCommand cmd = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn))
                        {
                            cmd.Parameters.AddWithValue("@Method", method);
                            var res = cmd.ExecuteScalar();
                            decimal bal = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                            methodBalances.Add((method, bal));
                            totalBalance += bal;
                        }
                    }
                }
            }
            catch { }

            // ---------- بيانات المقارنة بأمس + عملاء جدد اليوم + مخزون منخفض - لكروت الـ KPI (نسبة/فرق "عن أمس") ----------
            var yesterday = GetYesterdayComparisonData();
            int newCustomersToday = 0;
            try { using (SqliteConnection conn = OpenConnection()) newCustomersToday = GetNewCustomersCount(conn, today); } catch { }
            var stockOverview = GetStockOverviewData();
            int lowStockCount = stockOverview.LowStock + stockOverview.OutOfStock;

            double salesPct = yesterday.Sales > 0 ? (double)((todaySales - yesterday.Sales) / yesterday.Sales * 100) : (todaySales > 0 ? 100 : 0);
            double profitPct = yesterday.Profit > 0 ? (double)((todayProfit - yesterday.Profit) / yesterday.Profit * 100) : (todayProfit > 0 ? 100 : 0);
            double invoicePct = yesterday.InvoiceCount > 0 ? ((invoiceCount - yesterday.InvoiceCount) / (double)yesterday.InvoiceCount * 100) : (invoiceCount > 0 ? 100 : 0);
            int newCustomersDiff = newCustomersToday - yesterday.NewCustomers;

            string TrendText(double pct) => (pct >= 0 ? "▲ +" : "▼ ") + Math.Abs(pct).ToString("N0") + "% عن أمس";
            Color TrendColor(double pct) => pct >= 0 ? UIHelpers.ColorGreen : UIHelpers.ColorRed;

            // ---------- اتجابت هنا (قبل كروت الـ KPI مش بعدها زي الأول) عشان نقدر نستخدم بياناتها في الـ Sparkline بتاع كارت المبيعات ----------
            var salesTrend = GetLast7DaysSales();

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                AutoScroll = false,
                BackColor = ColorContentBg
            };

            flow.Controls.Add(CreateKpiCard("📊", UIHelpers.ColorGreen, "المبيعات اليوم", todaySales.ToString("N2") + " ج.م",
                () => NavigateToPageKey(PageKeys.SalesBlazor), sparklineValues: salesTrend.Values, counterTarget: (double)todaySales,
                changeText: TrendText(salesPct), changeColor: TrendColor(salesPct)));
            flow.Controls.Add(CreateKpiCard("💵", UIHelpers.ColorAccentPrimary, "إجمالي الأرباح", todayProfit.ToString("N2") + " ج.م",
                () => NavigateToPageKey(PageKeys.ReportsBlazor), counterTarget: (double)todayProfit,
                changeText: TrendText(profitPct), changeColor: TrendColor(profitPct)));
            flow.Controls.Add(CreateKpiCard("📄", UIHelpers.ColorPurple, "عدد الفواتير", invoiceCount.ToString(),
                () => NavigateToPageKey(PageKeys.SalesBlazor), counterTarget: invoiceCount, isCurrency: false,
                changeText: TrendText(invoicePct), changeColor: TrendColor(invoicePct)));
            flow.Controls.Add(CreateKpiCard("👥", UIHelpers.ColorOrange, "العملاء الجدد", newCustomersToday.ToString(),
                () => NavigateToPageKey(PageKeys.CustomersBlazor), counterTarget: newCustomersToday, isCurrency: false,
                changeText: (newCustomersDiff >= 0 ? "+" : "") + newCustomersDiff + " عن أمس", changeColor: TrendColor(newCustomersDiff)));
            flow.Controls.Add(CreateKpiCard("⚠️", UIHelpers.ColorRed, "المخزون المنخفض", lowStockCount + " منتجات",
                () => NavigateToPageKey(PageKeys.InventoryBlazor), counterTarget: lowStockCount, isCurrency: false,
                changeText: "⚠ تنبيه", changeColor: UIHelpers.ColorRed));

            // ---------- شريط الإجراءات السريعة + زرار طباعة ملخص اليوم ----------
            Panel pnlQuickActions = BuildQuickActionsBar();
            Guna2Button btnPrintSummary = new Guna2Button
            {
                Text = "🖨️ طباعة",
                Dock = DockStyle.Right,
                Width = 110,
                Height = 34,
                FillColor = Color.White,
                ForeColor = ColorTitleText,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1,
                BorderRadius = 8,
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(10, 4, 0, 4)
            };
            btnPrintSummary.Click += (s, e) => PrintHomeDashboardSummary(
                todaySales, todayPurchases, todayExpenses, todayProfit,
                invoiceCount, maintenanceCount, methodBalances, totalBalance);
            pnlQuickActions.Controls.Add(btnPrintSummary);

            // ---------- صف أرصدة وسائل الدفع - كل وسيلة في كارت منفصل، بنفس الدالة اللي بتبني كروت الـ KPI فوق بالظبط (مش جوه كارت واحد مجمّع) ----------
            Panel pnlBalancesRow = BuildBalancesRow(methodBalances, totalBalance);

            // ---------- الصف الأوسط: آخر الفواتير (تاخد الباقي) + التنبيهات ----------
            Panel pnlMiddleRow = new Panel { Dock = DockStyle.Top, Height = 265, BackColor = ColorContentBg };
            // ترتيب الإضافة هنا مهم: أول كارت Dock.Right بيتضاف بيقعد على الحافة اليمين الحقيقية.
            pnlMiddleRow.Controls.Add(CreateNotificationsCard());
            pnlMiddleRow.Controls.Add(CreateLatestInvoicesCard());

            // ---------- الصف السفلي: توزيع المبيعات + آخر العمليات + المخزون المنخفض (بياخد باقي المساحة) ----------
            Panel pnlBottomRow = new Panel { Dock = DockStyle.Top, Height = 225, BackColor = ColorContentBg, Margin = new Padding(0, 10, 0, 0) };
            pnlBottomRow.Controls.Add(CreateSalesDistributionCard());
            pnlBottomRow.Controls.Add(CreateRecentActivityCard());
            pnlBottomRow.Controls.Add(CreateLowStockCard());

            pnlContent.Controls.Add(pnlBottomRow);
            pnlContent.Controls.Add(pnlMiddleRow);
            pnlContent.Controls.Add(pnlBalancesRow);
            pnlContent.Controls.Add(pnlQuickActions);
            pnlContent.Controls.Add(flow);
        }

        // ==========================================================================
        // طباعة/تصدير ملخص اليوم (نفس أسلوب GridPrintHelper: نافذة طباعة ويندوز العادية،
        // المستخدم يختار طابعة حقيقية أو "Microsoft Print to PDF" لحفظه كـ PDF بدون أي مكتبة خارجية)
        // ==========================================================================
        private void PrintHomeDashboardSummary(
            decimal todaySales, decimal todayPurchases, decimal todayExpenses, decimal todayProfit,
            int invoiceCount, int maintenanceCount,
            List<(string Method, decimal Balance)> methodBalances, decimal totalBalance)
        {
            PrintDocument pd = new PrintDocument();
            pd.DefaultPageSettings.Landscape = false;

            pd.PrintPage += (s, e) =>
            {
                Graphics g = e.Graphics;
                float left = e.MarginBounds.Left;
                float width = e.MarginBounds.Width;
                float y = e.MarginBounds.Top;

                Font fontTitle = new Font("Arial", 16, FontStyle.Bold);
                Font fontDate = new Font("Arial", 9);
                Font fontSectionTitle = new Font("Arial", 10.5F, FontStyle.Bold);
                Font fontLabel = new Font("Arial", 10);
                Font fontValue = new Font("Arial", 10, FontStyle.Bold);
                Font fontFooter = new Font("Arial", 8);

                StringFormat sfLeft = new StringFormat { Alignment = StringAlignment.Near };
                StringFormat sfRight = new StringFormat { Alignment = StringAlignment.Far };

                g.DrawString("ملخص يومي — Temo Mobile Store", fontTitle, Brushes.Black, new RectangleF(left, y, width, 30), new StringFormat { Alignment = StringAlignment.Center });
                y += 32;
                g.DrawString(DateTime.Now.ToString("yyyy-MM-dd"), fontDate, Brushes.Gray, new RectangleF(left, y, width, 18), new StringFormat { Alignment = StringAlignment.Center });
                y += 30;
                g.DrawLine(Pens.Black, left, y, left + width, y);
                y += 15;

                void DrawRow(string label, string value)
                {
                    g.DrawString(label, fontLabel, Brushes.Black, new RectangleF(left, y, width, 20), sfRight);
                    g.DrawString(value, fontValue, Brushes.Black, new RectangleF(left, y, width, 20), sfLeft);
                    y += 24;
                }

                DrawRow("إجمالي المبيعات", todaySales.ToString("N2") + " ج.م");
                DrawRow("إجمالي المشتريات", todayPurchases.ToString("N2") + " ج.م");
                DrawRow("إجمالي المصروفات", todayExpenses.ToString("N2") + " ج.م");
                DrawRow("صافي الربح", todayProfit.ToString("N2") + " ج.م");
                DrawRow("عدد الفواتير", invoiceCount.ToString());
                DrawRow("طلبات الصيانة", maintenanceCount.ToString());

                y += 10;
                g.DrawLine(Pens.Black, left, y, left + width, y);
                y += 15;
                g.DrawString("أرصدة وسائل الدفع", fontSectionTitle, Brushes.Black, new RectangleF(left, y, width, 20), sfRight);
                y += 26;

                DrawRow("الإجمالي (كل الوسائل)", totalBalance.ToString("N2") + " ج.م");
                foreach (var mb in methodBalances)
                    DrawRow(mb.Method, mb.Balance.ToString("N2") + " ج.م");

                y += 20;
                g.DrawLine(Pens.Black, left, y, left + width, y);
                y += 12;
                g.DrawString("تاريخ الطباعة: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"), fontFooter, Brushes.Gray, new RectangleF(left, y, width, 16), new StringFormat { Alignment = StringAlignment.Center });

                e.HasMorePages = false;
            };

            using (PrintDialog printDialog = new PrintDialog())
            {
                printDialog.Document = pd;
                printDialog.AllowSomePages = false;
                printDialog.UseEXDialog = true;
                if (printDialog.ShowDialog(this) == DialogResult.OK)
                    pd.Print();
            }
        }

        // ==========================================================================
        // جلب أعداد الأصناف حسب حالة المخزون: متوفر / كمية قليلة / نفدت
        // نفس عتبة "كمية قليلة" المستخدمة في نظام الإشعارات (GetNotificationItems: Quantity <= 3)
        // عشان الرقم يفضل متسق في كل مكان في البرنامج
        // ==========================================================================
        private (int InStock, int LowStock, int OutOfStock, int Total) GetStockOverviewData()
        {
            int inStock = 0, lowStock = 0, outOfStock = 0;
            try
            {
                using (SqliteConnection conn = OpenConnection())
                using (SqliteCommand cmd = new SqliteCommand(@"
                    SELECT
                        SUM(CASE WHEN Quantity > 3 THEN 1 ELSE 0 END) AS InStock,
                        SUM(CASE WHEN Quantity BETWEEN 1 AND 3 THEN 1 ELSE 0 END) AS LowStock,
                        SUM(CASE WHEN Quantity <= 0 THEN 1 ELSE 0 END) AS OutOfStock
                    FROM Products", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        inStock = reader["InStock"] != DBNull.Value ? Convert.ToInt32(reader["InStock"]) : 0;
                        lowStock = reader["LowStock"] != DBNull.Value ? Convert.ToInt32(reader["LowStock"]) : 0;
                        outOfStock = reader["OutOfStock"] != DBNull.Value ? Convert.ToInt32(reader["OutOfStock"]) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("حصل خطأ أثناء تحميل بيانات حالة المخزون: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return (inStock, lowStock, outOfStock, inStock + lowStock + outOfStock);
        }

        // ==========================================================================
        // أكثر 5 منتجات مبيعًا (بالكمية) خلال آخر 7 أيام
        // ==========================================================================
        // ==========================================================================
        // توزيع المبيعات خلال آخر 7 أيام: كاش / آجل / إيرادات صيانة (نفس نافذة شارت المبيعات)
        // ملحوظة: مفيش عمود "فئة/Category" في جدول المنتجات أصلاً، فمينفعش نقسّم المبيعات
        // حسب نوع المنتج (موبايلات/إكسسوارات) زي ما كان متصور في التصميم الأصلي - البديل ده
        // بيانات حقيقية فعلاً موجودة في قاعدة البيانات بدل ما نخترع تصنيف مش موجود.
        // ==========================================================================
        private (decimal Cash, decimal Credit, decimal Maintenance) GetSalesDistributionData()
        {
            decimal cash = 0, credit = 0, maintenance = 0;
            try
            {
                using (SqliteConnection conn = OpenConnection())
                {
                    using (SqliteCommand cmd = new SqliteCommand("SELECT COALESCE(SUM(Total),0) FROM Sales WHERE PaymentType = 'Cash' AND date(SaleDate) >= date('now','-6 days')", conn))
                        cash = Convert.ToDecimal(cmd.ExecuteScalar());

                    using (SqliteCommand cmd = new SqliteCommand("SELECT COALESCE(SUM(Total),0) FROM Sales WHERE PaymentType = 'Credit' AND date(SaleDate) >= date('now','-6 days')", conn))
                        credit = Convert.ToDecimal(cmd.ExecuteScalar());

                    using (SqliteCommand cmd = new SqliteCommand("SELECT COALESCE(SUM(ActualCost),0) FROM MaintenanceTickets WHERE Status = 'تم التسليم' AND date(DeliveredDate) >= date('now','-6 days')", conn))
                        maintenance = Convert.ToDecimal(cmd.ExecuteScalar());
                }
            }
            catch { }
            return (cash, credit, maintenance);
        }

        // ==========================================================================
        // كارت "توزيع المبيعات": Donut بنفس أسلوب كارت حالة المخزون
        // ==========================================================================
        private Guna2Panel CreateSalesDistributionCard()
        {
            var dist = GetSalesDistributionData();
            decimal total = dist.Cash + dist.Credit + dist.Maintenance;

            Guna2Panel card = new Guna2Panel
            {
                Dock = DockStyle.Right,
                Width = 320,
                FillColor = Color.White,
                BorderRadius = UIHelpers.CardBorderRadius,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1,
                Margin = new Padding(15, 0, 0, 0)
            };

            Label lblTitle = new Label
            {
                Text = "🥯 توزيع المبيعات (7 أيام)",
                Location = new Point(15, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ColorTitleText
            };
            card.Controls.Add(lblTitle);

            if (total == 0)
            {
                Label lblEmpty = new Label { Text = "مفيش مبيعات مسجّلة في آخر 7 أيام.", Location = new Point(15, 44), AutoSize = true, ForeColor = Color.FromArgb(85, 92, 102), Font = new Font("Segoe UI", 8.5F) };
                card.Controls.Add(lblEmpty);
                return card;
            }

            Color colorCash = UIHelpers.ColorGreen;
            Color colorCredit = UIHelpers.ColorOrange;
            Color colorMaintenance = UIHelpers.ColorPurple;

            Panel pnlChartHolder = new Panel { Location = new Point(95, 38), Size = new Size(130, 110), BackColor = Color.Transparent };
            PieChart pieChart = new PieChart
            {
                Dock = DockStyle.Fill,
                LegendPosition = LegendPosition.Hidden,
                TooltipPosition = TooltipPosition.Hidden,
                InitialRotation = -90,
                Series = new ISeries[]
                {
                    new PieSeries<double> { Values = new double[] { (double)dist.Cash }, Name = "مبيعات كاش", Fill = new SolidColorPaint(new SKColor(colorCash.R, colorCash.G, colorCash.B)), InnerRadius = 38 },
                    new PieSeries<double> { Values = new double[] { (double)dist.Credit }, Name = "مبيعات آجل", Fill = new SolidColorPaint(new SKColor(colorCredit.R, colorCredit.G, colorCredit.B)), InnerRadius = 38 },
                    new PieSeries<double> { Values = new double[] { (double)dist.Maintenance }, Name = "إيرادات صيانة", Fill = new SolidColorPaint(new SKColor(colorMaintenance.R, colorMaintenance.G, colorMaintenance.B)), InnerRadius = 38 }
                }
            };
            pnlChartHolder.Controls.Add(pieChart);
            card.Controls.Add(pnlChartHolder);

            var legendRows = new (string Label, decimal Amount, Color LegendColor)[]
            {
                ("مبيعات كاش", dist.Cash, colorCash),
                ("مبيعات آجل", dist.Credit, colorCredit),
                ("إيرادات صيانة", dist.Maintenance, colorMaintenance)
            };

            int legendY = 152;
            foreach (var row in legendRows)
            {
                double pct = total > 0 ? (double)(row.Amount * 100 / total) : 0;
                Guna2Panel dot = new Guna2Panel { FillColor = row.LegendColor, BorderRadius = 5, Location = new Point(20, legendY + 3), Size = new Size(10, 10) };
                Label lblName = new Label { Text = row.Label, Font = new Font("Segoe UI", 8.5F), ForeColor = ColorTitleText, AutoSize = true, Location = new Point(38, legendY) };
                Label lblPct = new Label { Text = $"{pct:N0}%", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(85, 92, 102), AutoSize = true, Location = new Point(200, legendY) };
                card.Controls.Add(dot);
                card.Controls.Add(lblName);
                card.Controls.Add(lblPct);
                legendY += 20;
            }

            return card;
        }

        // ==========================================================================
        // أصناف وشيكة تخلص - نفس عتبة الإشعارات بالظبط (Quantity <= 3) عشان الأرقام تفضل متسقة
        // ==========================================================================
        private List<(string Name, int Qty)> GetLowStockProducts()
        {
            var result = new List<(string Name, int Qty)>();
            try
            {
                using (SqliteConnection conn = OpenConnection())
                using (SqliteCommand cmd = new SqliteCommand("SELECT ProductName, Quantity FROM Products WHERE Quantity <= 3 ORDER BY Quantity ASC LIMIT 6", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        result.Add((reader["ProductName"].ToString(), Convert.ToInt32(reader["Quantity"])));
                }
            }
            catch { }
            return result;
        }

        // ==========================================================================
        // كارت "أصناف وشيكة تخلص": اسم الصنف + شريط أحمر متناسب مع الكمية المتبقية
        // ==========================================================================
        private Guna2Panel CreateLowStockCard()
        {
            var lowStock = GetLowStockProducts();

            Guna2Panel card = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                FillColor = Color.White,
                BorderRadius = UIHelpers.CardBorderRadius,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1,
                Margin = new Padding(12, 0, 0, 0)
            };

            Label lblTitle = new Label
            {
                Text = "⚠️ المخزون المنخفض",
                Location = new Point(15, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ColorTitleText
            };
            card.Controls.Add(lblTitle);

            if (lowStock.Count == 0)
            {
                Label lblEmpty = new Label { Text = "كل الأصناف بكميات كافية 👍", Location = new Point(15, 44), AutoSize = true, ForeColor = Color.FromArgb(85, 92, 102), Font = new Font("Segoe UI", 8.5F) };
                card.Controls.Add(lblEmpty);
                return card;
            }

            const int barMax = 3; // نفس عتبة "كمية قليلة" في كل مكان تاني بالبرنامج
            int y = 42;
            foreach (var item in lowStock.Take(4))
            {
                Label lblName = new Label { Text = item.Name, Location = new Point(15, y), Size = new Size(150, 15), Font = new Font("Segoe UI", 8F), ForeColor = ColorTitleText };
                Label lblQty = new Label { Text = item.Qty <= 0 ? "نفدت" : $"{item.Qty} جهاز", Location = new Point(170, y), Size = new Size(65, 15), Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), ForeColor = UIHelpers.ColorRed, TextAlign = ContentAlignment.MiddleLeft };

                Guna2Panel track = new Guna2Panel { Location = new Point(15, y + 16), Size = new Size(220, 5), FillColor = Color.FromArgb(240, 240, 245), BorderRadius = 3 };
                double frac = Math.Max(0, Math.Min(1.0, item.Qty / (double)barMax));
                int fillWidth = Math.Max(5, (int)(220 * frac));
                Guna2Panel fill = new Guna2Panel { Location = new Point(0, 0), Size = new Size(fillWidth, 5), FillColor = UIHelpers.ColorRed, BorderRadius = 3 };
                track.Controls.Add(fill);

                card.Controls.Add(lblName);
                card.Controls.Add(lblQty);
                card.Controls.Add(track);
                y += 34;
            }

            return card;
        }

        // ==========================================================================
        // كارت "آخر الفواتير" - آخر فواتير البيع مع اسم العميل والمبلغ وحالة الدفع
        // ==========================================================================
        private Guna2Panel CreateLatestInvoicesCard()
        {
            var invoices = GetLatestInvoices(5);

            Guna2Panel card = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                FillColor = Color.White,
                BorderRadius = UIHelpers.CardBorderRadius,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1,
                Margin = new Padding(12, 0, 0, 0)
            };

            Label lblTitle = new Label { Text = "🧾 آخر الفواتير", Location = new Point(15, 12), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = ColorTitleText };
            card.Controls.Add(lblTitle);

            if (invoices.Count == 0)
            {
                Label lblEmpty = new Label { Text = "مفيش فواتير مسجّلة لسه.", Location = new Point(15, 44), AutoSize = true, ForeColor = Color.FromArgb(85, 92, 102), Font = new Font("Segoe UI", 8.5F) };
                card.Controls.Add(lblEmpty);
                return card;
            }

            int y = 42;
            foreach (var inv in invoices)
            {
                bool isCash = inv.PaymentType == "Cash";
                Panel row = new Panel { Location = new Point(15, y), Size = new Size(270, 34), Cursor = Cursors.Hand };

                Label lblNum = new Label { Text = $"#INV-{inv.InvoiceId:00000}", Location = new Point(0, 0), Size = new Size(90, 15), Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), ForeColor = UIHelpers.ColorAccentPrimary };
                Label lblCustomer = new Label { Text = inv.CustomerName, Location = new Point(0, 16), Size = new Size(150, 15), Font = new Font("Segoe UI", 8F), ForeColor = ColorTitleText };
                Label lblTotal = new Label { Text = inv.Total.ToString("N0") + " ج.م", Location = new Point(150, 0), Size = new Size(120, 15), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = ColorTitleText, TextAlign = ContentAlignment.MiddleLeft };

                Guna2Panel badge = new Guna2Panel
                {
                    Location = new Point(190, 16),
                    Size = new Size(80, 17),
                    BorderRadius = 8,
                    FillColor = isCash ? LightTint(UIHelpers.ColorGreen, 0.85f) : LightTint(UIHelpers.ColorOrange, 0.85f)
                };
                Label lblBadgeText = new Label { Text = isCash ? "مدفوعة" : "معلقة", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 7F, FontStyle.Bold), ForeColor = isCash ? UIHelpers.ColorGreen : UIHelpers.ColorOrange, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };
                badge.Controls.Add(lblBadgeText);

                row.Controls.Add(lblNum);
                row.Controls.Add(lblCustomer);
                row.Controls.Add(lblTotal);
                row.Controls.Add(badge);

                int saleId = inv.RepresentativeSaleId;
                EventHandler onClick = (s, e) => NavigateToPageKey(PageKeys.SalesBlazor, highlightKey: saleId.ToString());
                row.Click += onClick;
                foreach (Control c in row.Controls) { c.Cursor = Cursors.Hand; c.Click += onClick; }

                card.Controls.Add(row);
                y += 38;
            }

            return card;
        }

        // ==========================================================================
        // كارت "التنبيهات" داخل الداشبورد مباشرة - نفس بيانات جرس الإشعارات + بند جديد لموردين لهم مستحقات
        // ==========================================================================
        private Guna2Panel CreateNotificationsCard()
        {
            var bellItems = GetNotificationItems();
            var supplierDebts = GetSupplierDebtItems();

            var rows = new List<(string Icon, string Text, Color Accent)>();
            foreach (var item in bellItems.Take(3))
            {
                Color accent = item.Icon == "📦" ? UIHelpers.ColorOrange : item.Icon == "🔧" ? UIHelpers.ColorAccentPrimary : UIHelpers.ColorRed;
                rows.Add((item.Icon, item.Text, accent));
            }
            foreach (var sd in supplierDebts.Take(2))
                rows.Add(("🚚", $"مورد {sd.Name} له مستحقات {sd.Balance:N0} ج.م", UIHelpers.ColorPurple));

            Guna2Panel card = new Guna2Panel
            {
                Dock = DockStyle.Right,
                Width = 260,
                FillColor = Color.White,
                BorderRadius = UIHelpers.CardBorderRadius,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1
            };

            Label lblTitle = new Label { Text = "🔔 تنبيهات", Location = new Point(15, 12), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = ColorTitleText };
            card.Controls.Add(lblTitle);

            if (rows.Count == 0)
            {
                Label lblEmpty = new Label { Text = "مفيش تنبيهات دلوقتي 👍", Location = new Point(15, 44), AutoSize = true, ForeColor = Color.FromArgb(85, 92, 102), Font = new Font("Segoe UI", 8.5F) };
                card.Controls.Add(lblEmpty);
                return card;
            }

            int y = 42;
            foreach (var row in rows.Take(4))
            {
                Guna2Panel dot = new Guna2Panel { Location = new Point(15, y + 2), Size = new Size(24, 24), BorderRadius = 12, FillColor = LightTint(row.Accent, 0.85f) };
                Label lblDotIcon = new Label { Text = row.Icon, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };
                dot.Controls.Add(lblDotIcon);

                Label lblText = new Label { Text = row.Text, Location = new Point(45, y), Size = new Size(205, 30), Font = new Font("Segoe UI", 7.8F), ForeColor = ColorTitleText };

                card.Controls.Add(dot);
                card.Controls.Add(lblText);
                y += 38;
            }

            return card;
        }

        // ==========================================================================
        // كارت "آخر العمليات" - Timeline مبسّط لآخر الأحداث في البرنامج بترتيب الوقت
        // ==========================================================================
        private Guna2Panel CreateRecentActivityCard()
        {
            var activity = GetRecentActivity(5);

            Guna2Panel card = new Guna2Panel
            {
                Dock = DockStyle.Right,
                Width = 255,
                FillColor = Color.White,
                BorderRadius = UIHelpers.CardBorderRadius,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1,
                Margin = new Padding(12, 0, 0, 0)
            };

            Label lblTitle = new Label { Text = "🕒 آخر العمليات", Location = new Point(15, 12), AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = ColorTitleText };
            card.Controls.Add(lblTitle);

            if (activity.Count == 0)
            {
                Label lblEmpty = new Label { Text = "مفيش عمليات مسجّلة لسه.", Location = new Point(15, 44), AutoSize = true, ForeColor = Color.FromArgb(85, 92, 102), Font = new Font("Segoe UI", 8.5F) };
                card.Controls.Add(lblEmpty);
                return card;
            }

            int y = 42;
            foreach (var item in activity.Take(4))
            {
                Label lblIcon = new Label { Text = item.Icon, Location = new Point(15, y), Size = new Size(20, 15), Font = new Font("Segoe UI", 9F) };
                Label lblWhen = new Label { Text = FormatRelativeTime(item.When), Location = new Point(38, y), AutoSize = true, Font = new Font("Segoe UI", 7F), ForeColor = Color.FromArgb(150, 155, 165) };
                Label lblText = new Label { Text = item.Text, Location = new Point(38, y + 12), Size = new Size(200, 18), Font = new Font("Segoe UI", 7.5F), ForeColor = ColorTitleText };

                card.Controls.Add(lblIcon);
                card.Controls.Add(lblWhen);
                card.Controls.Add(lblText);
                y += 34;
            }

            return card;
        }

        // ==========================================================================
        // شريط "إجراءات سريعة" - كل زرار بيفتح الصفحة المناسبة في وضع جاهز للاستخدام فورًا
        // (مش مجرد تنقل عادي زي أزرار الـ Sidebar - كل زرار بيجهّز الشاشة جاهزة للكتابة/المسح)
        // ==========================================================================
        // ==========================================================================
        // صف "أرصدة وسائل الدفع" - كل وسيلة دفع في كارت منفصل قائم بذاته، بنفس بالظبط الدالة
        // اللي بتبني كروت المبيعات/الأرباح فوق (CreateKpiCard) - مش جوه كارت واحد مجمّع
        // ==========================================================================
        private Panel BuildBalancesRow(List<(string Method, decimal Balance)> methodBalances, decimal totalBalance)
        {
            var methodIcons = new Dictionary<string, string> { { "نقدي", "💵" }, { "فوري", "📲" }, { "أمان", "🔒" }, { "سهولة", "✅" }, { "فودافون كاش", "🔴" }, { "إنستاباي", "🏛️" } };
            var methodColors = new Dictionary<string, Color> {
                { "نقدي", UIHelpers.ColorGreen }, { "فوري", UIHelpers.ColorOrange },
                { "أمان", UIHelpers.ColorAccentPrimary }, { "سهولة", Color.FromArgb(22, 160, 133) },
                { "فودافون كاش", UIHelpers.ColorRed }, { "إنستاباي", UIHelpers.ColorPurple }
            };

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                AutoScroll = false,
                BackColor = ColorContentBg
            };

            flow.Controls.Add(CreateKpiCard("🏦", UIHelpers.ColorAccentPrimary, "الإجمالي", totalBalance.ToString("N2") + " ج.م",
                () => NavigateToPageKey(PageKeys.TreasuryBlazor), counterTarget: (double)totalBalance));

            foreach (var mb in methodBalances)
            {
                string ic = methodIcons.TryGetValue(mb.Method, out string i) ? i : "💳";
                Color accent = methodColors.TryGetValue(mb.Method, out Color c) ? c : UIHelpers.ColorAccentPrimary;
                flow.Controls.Add(CreateKpiCard(ic, accent, mb.Method, mb.Balance.ToString("N2") + " ج.م",
                    () => NavigateToPageKey(PageKeys.TreasuryBlazor), counterTarget: (double)mb.Balance));
            }

            return flow;
        }

        private Panel BuildQuickActionsBar()
        {
            var actions = new (string Icon, string Text, Action OnClick)[]
            {
                ("➕", "فاتورة جديدة", () => NavigateToPageKey(PageKeys.SalesBlazor, landingMode: true)),
                ("📦", "إضافة منتج", () => NavigateToPageKey(PageKeys.InventoryBlazor, landingMode: true)),
                ("👤", "عميل جديد", () => NavigateToPageKey(PageKeys.CustomersBlazor, landingMode: true)),
                ("🛒", "فاتورة شراء", () => NavigateToPageKey(PageKeys.PurchasesBlazor)),
                ("💳", "إضافة مصروف", () => NavigateToPageKey(PageKeys.TreasuryBlazor, "مصروف")),
                ("📶", "مسح باركود", () => NavigateToPageKey(PageKeys.SalesBlazor, landingMode: true)),
            };

            // بانل خارجي بارتفاع ثابت (صف واحد بس) - زرار الطباعة بينضاف من BuildHomeDashboardPage
            // كـ Dock.Right جواه هو نفسه، مش جوه الـ FlowLayoutPanel (اللي بيتجاهل Dock للعناصر اللي جواه)
            Panel row = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = ColorContentBg };

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                AutoScroll = false,
                BackColor = ColorContentBg
            };

            foreach (var action in actions)
            {
                Guna2Button btn = new Guna2Button
                {
                    Text = action.Icon + "  " + action.Text,
                    Width = 128,
                    Height = 36,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    ForeColor = ColorTitleText,
                    FillColor = Color.White,
                    BorderColor = Color.FromArgb(230, 232, 238),
                    BorderThickness = 1,
                    BorderRadius = 9,
                    Margin = new Padding(0, 4, 8, 4)
                };
                Action onClick = action.OnClick;
                btn.Click += (s, e) => onClick();
                btn.MouseEnter += (s, e) => { btn.FillColor = Color.FromArgb(245, 247, 255); btn.BorderColor = UIHelpers.ColorAccentPrimary; };
                btn.MouseLeave += (s, e) => { btn.FillColor = Color.White; btn.BorderColor = Color.FromArgb(230, 232, 238); };
                flow.Controls.Add(btn);
            }

            row.Controls.Add(flow);
            return row;
        }

        private List<(string Name, int Qty)> GetTopSellingProducts()
        {
            var result = new List<(string Name, int Qty)>();
            using (SqliteConnection conn = OpenConnection())
            {
                try
                {
                    using (SqliteCommand cmd = new SqliteCommand(
                        "SELECT ProductName, SUM(QuantitySold) AS TotalQty FROM Sales WHERE date(SaleDate) >= date('now','-6 days') GROUP BY ProductName ORDER BY TotalQty DESC LIMIT 5", conn))
                    {
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                result.Add((reader["ProductName"].ToString(), Convert.ToInt32(reader["TotalQty"])));
                        }
                    }
                }
                catch { }
            }
            return result;
        }

        // ==========================================================================
        // جلب إجمالي المبيعات لكل يوم من آخر 7 أيام (بما فيهم الأيام اللي مفيهاش مبيعات = صفر)
        // ==========================================================================
        private (double[] Values, string[] Labels) GetLast7DaysSales()
        {
            double[] values = new double[7];
            string[] labels = new string[7];
            DateTime[] dates = new DateTime[7];

            for (int i = 0; i < 7; i++)
                dates[i] = DateTime.Now.Date.AddDays(-6 + i);

            try
            {
                using (SqliteConnection conn = OpenConnection())
                {
                    using (SqliteCommand cmd = new SqliteCommand(
                        "SELECT substr(SaleDate,1,10) AS D, SUM(Total) AS T FROM Sales WHERE date(SaleDate) >= date('now','-6 days') GROUP BY D", conn))
                    {
                        var dailyTotals = new Dictionary<string, double>();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string d = reader["D"].ToString();
                                double t = reader["T"] != DBNull.Value ? Convert.ToDouble(reader["T"]) : 0;
                                dailyTotals[d] = t;
                            }
                        }

                        for (int i = 0; i < 7; i++)
                        {
                            string key = dates[i].ToString("yyyy-MM-dd");
                            values[i] = dailyTotals.ContainsKey(key) ? dailyTotals[key] : 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("حصل خطأ أثناء تحميل بيانات الرسم البياني: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            for (int i = 0; i < 7; i++)
                labels[i] = dates[i].ToString("dd/MM");

            return (values, labels);
        }

        // ==========================================================================
        // مقارنة أمس: نفس أعمدة كارت اليوم بالظبط (مبيعات/ربح/فواتير/عملاء جدد) عشان نحسب نسبة التغيّر
        // ==========================================================================
        private (decimal Sales, decimal Profit, int InvoiceCount, int NewCustomers) GetYesterdayComparisonData()
        {
            decimal sales = 0, cogs = 0, expenses = 0;
            int invoiceCount = 0, newCustomers = 0;
            string yesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");

            try
            {
                using (SqliteConnection conn = OpenConnection())
                {
                    using (SqliteCommand cmd = new SqliteCommand(
                        "SELECT SUM(Total) AS T, SUM(CostPrice * QuantitySold) AS C, COUNT(*) AS N FROM Sales WHERE SaleDate LIKE @D", conn))
                    {
                        cmd.Parameters.AddWithValue("@D", yesterday + "%");
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                sales = reader["T"] != DBNull.Value ? Convert.ToDecimal(reader["T"]) : 0;
                                cogs = reader["C"] != DBNull.Value ? Convert.ToDecimal(reader["C"]) : 0;
                                invoiceCount = reader["N"] != DBNull.Value ? Convert.ToInt32(reader["N"]) : 0;
                            }
                        }
                    }
                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM Expenses WHERE ExpenseDate LIKE @D", conn))
                    {
                        cmd.Parameters.AddWithValue("@D", yesterday + "%");
                        var res = cmd.ExecuteScalar();
                        expenses = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                    }
                    newCustomers = GetNewCustomersCount(conn, yesterday);
                }
            }
            catch { }

            return (sales, sales - cogs - expenses, invoiceCount, newCustomers);
        }

        private int GetNewCustomersCount(SqliteConnection conn, string dateStr)
        {
            try
            {
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM Customers WHERE CreatedAt LIKE @D", conn))
                {
                    cmd.Parameters.AddWithValue("@D", dateStr + "%");
                    var res = cmd.ExecuteScalar();
                    return (res != null && res != DBNull.Value) ? Convert.ToInt32(res) : 0;
                }
            }
            catch { return 0; }
        }

        // ==========================================================================
        // آخر فواتير البيع - مجمّعة حسب SalesInvoiceId (فاتورة واحدة ممكن يكون فيها أكتر من صنف)
        // ==========================================================================
        private List<(int InvoiceId, int RepresentativeSaleId, string CustomerName, string Date, decimal Total, string PaymentType)> GetLatestInvoices(int limit)
        {
            var result = new List<(int, int, string, string, decimal, string)>();
            try
            {
                using (SqliteConnection conn = OpenConnection())
                using (SqliteCommand cmd = new SqliteCommand($@"
                    SELECT S.SalesInvoiceId, MIN(S.SaleID) AS FirstSaleId, MAX(S.SaleDate) AS SaleDate,
                           SUM(S.Total) AS Total, MAX(S.PaymentType) AS PaymentType, C.CustomerName
                    FROM Sales S LEFT JOIN Customers C ON S.CustomerId = C.CustomerId
                    WHERE S.SalesInvoiceId IS NOT NULL
                    GROUP BY S.SalesInvoiceId
                    ORDER BY MAX(S.SaleID) DESC LIMIT {limit}", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add((
                            Convert.ToInt32(reader["SalesInvoiceId"]),
                            Convert.ToInt32(reader["FirstSaleId"]),
                            reader["CustomerName"] == DBNull.Value ? "عميل نقدي" : reader["CustomerName"].ToString(),
                            reader["SaleDate"].ToString(),
                            reader["Total"] != DBNull.Value ? Convert.ToDecimal(reader["Total"]) : 0,
                            reader["PaymentType"] == DBNull.Value ? "Cash" : reader["PaymentType"].ToString()
                        ));
                    }
                }
            }
            catch { }
            return result;
        }

        // ==========================================================================
        // موردين لهم مستحقات (نفس معادلة SuppliersRepository.GetSuppliersWithBalances بالظبط - إعادة استخدام، مش استعلام جديد)
        // ==========================================================================
        private List<(string Name, decimal Balance)> GetSupplierDebtItems()
        {
            var result = new List<(string Name, decimal Balance)>();
            try
            {
                DataTable dt = SuppliersRepository.GetSuppliersWithBalances();
                foreach (DataRow row in dt.Rows)
                {
                    decimal balance = row["المتبقي"] == DBNull.Value ? 0 : Convert.ToDecimal(row["المتبقي"]);
                    if (balance > 0)
                        result.Add((row["اسم المورد"].ToString(), balance));
                }
            }
            catch { }
            return result.OrderByDescending(x => x.Balance).Take(5).ToList();
        }

        // ==========================================================================
        // آخر العمليات: دمج أحدث الصفوف من المبيعات/المشتريات/الصيانة/حركات الخزينة (قبض وصرف) بترتيب الوقت
        // مفيش جدول واحد لسجل العمليات في البرنامج، فبنجمع من الجداول الموجودة فعلاً ونرتب بالتاريخ في الكود
        // ==========================================================================
        private List<(string Icon, string Text, DateTime When)> GetRecentActivity(int limit)
        {
            var items = new List<(string Icon, string Text, DateTime When)>();

            try
            {
                using (SqliteConnection conn = OpenConnection())
                {
                    using (SqliteCommand cmd = new SqliteCommand("SELECT ProductName, Total, SaleDate FROM Sales ORDER BY SaleID DESC LIMIT 8", conn))
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                        while (reader.Read())
                            if (DateTime.TryParse(reader["SaleDate"].ToString(), out DateTime dt))
                                items.Add(("🛒", $"بيع {reader["ProductName"]} بـ {Convert.ToDecimal(reader["Total"]):N0} ج.م", dt));

                    using (SqliteCommand cmd = new SqliteCommand("SELECT TotalAmount, PurchaseDate FROM Purchases ORDER BY PurchaseId DESC LIMIT 5", conn))
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                        while (reader.Read())
                            if (DateTime.TryParse(reader["PurchaseDate"].ToString(), out DateTime dt))
                                items.Add(("🚚", $"فاتورة شراء بـ {Convert.ToDecimal(reader["TotalAmount"]):N0} ج.م", dt));

                    using (SqliteCommand cmd = new SqliteCommand("SELECT CustomerName, DeviceInfo, ReceivedDate FROM MaintenanceTickets ORDER BY TicketId DESC LIMIT 5", conn))
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                        while (reader.Read())
                            if (DateTime.TryParse(reader["ReceivedDate"].ToString(), out DateTime dt))
                                items.Add(("🔧", $"استلام جهاز صيانة من {reader["CustomerName"]}", dt));

                    using (SqliteCommand cmd = new SqliteCommand("SELECT MovementType, Amount, MovementDate FROM CashMovements WHERE MovementType IN ('قبض','صرف') ORDER BY Id DESC LIMIT 8", conn))
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                        while (reader.Read())
                            if (DateTime.TryParse(reader["MovementDate"].ToString(), out DateTime dt))
                            {
                                bool isCollect = reader["MovementType"].ToString() == "قبض";
                                items.Add((isCollect ? "💰" : "💳", (isCollect ? "تحصيل مبلغ " : "سداد مبلغ ") + $"{Convert.ToDecimal(reader["Amount"]):N0} ج.م", dt));
                            }
                }
            }
            catch { }

            return items.OrderByDescending(x => x.When).Take(limit).ToList();
        }

        // ==========================================================================
        // "منذ N دقيقة/ساعة/يوم" - نص وقت نسبي بسيط لعرض آخر العمليات
        // ==========================================================================
        private static string FormatRelativeTime(DateTime when)
        {
            TimeSpan diff = DateTime.Now - when;
            if (diff.TotalMinutes < 1) return "الآن";
            if (diff.TotalMinutes < 60) return $"منذ {(int)diff.TotalMinutes} دقيقة";
            if (diff.TotalHours < 24) return $"منذ {(int)diff.TotalHours} ساعة";
            if (diff.TotalDays < 30) return $"منذ {(int)diff.TotalDays} يوم";
            return when.ToString("dd/MM/yyyy");
        }

        // ==========================================================================
        // بناء كارت KPI واحد (أيقونة + عنوان + قيمة)
        // ==========================================================================
        private Panel CreateKpiCard(string icon, Color accentColor, string title, string value, Action onClick = null,
            double[] sparklineValues = null, double? counterTarget = null, bool isCurrency = true,
            string changeText = null, Color? changeColor = null)
        {
            Guna2Panel card = new Guna2Panel
            {
                Width = 210,
                Height = 118,
                FillColor = Color.White,
                BorderRadius = UIHelpers.CardBorderRadius,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1,
                Margin = new Padding(6)
            };
            Color hoverBorder = accentColor;
            Color idleBorder = Color.FromArgb(230, 232, 238);
            card.MouseEnter += (s, e) => { card.BorderColor = hoverBorder; card.BorderThickness = 2; };
            card.MouseLeave += (s, e) => { card.BorderColor = idleBorder; card.BorderThickness = 1; };

            Guna2Panel lblIcon = new Guna2Panel
            {
                FillColor = LightTint(accentColor, 0.85f),
                BorderRadius = 10,
                Location = new Point(14, 14),
                Size = new Size(38, 38)
            };
            Label lblIconText = new Label
            {
                Text = icon,
                Font = new Font("Segoe UI", 14F),
                ForeColor = accentColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            lblIcon.Controls.Add(lblIconText);

            Label lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(85, 92, 102),
                AutoSize = true,
                Location = new Point(60, 16)
            };

            Label lblValue = new Label
            {
                Text = counterTarget.HasValue ? FormatKpiCounter(0, isCurrency) : value,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = ColorTitleText,
                AutoSize = true,
                Location = new Point(60, 34)
            };

            card.Controls.Add(lblIcon);
            card.Controls.Add(lblTitle);
            card.Controls.Add(lblValue);

            if (!string.IsNullOrEmpty(changeText))
            {
                Label lblChange = new Label
                {
                    Text = changeText,
                    Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                    ForeColor = changeColor ?? UIHelpers.ColorGreen,
                    AutoSize = true,
                    Location = new Point(14, 60)
                };
                card.Controls.Add(lblChange);
            }

            // ---------- ميني-شارت (Sparkline) اختياري - شريط بعرض الكارت كامل تحت (Dock.Bottom) زي التصميم المرجعي ----------
            if (sparklineValues != null && sparklineValues.Length > 1)
            {
                CartesianChart spark = new CartesianChart
                {
                    Dock = DockStyle.Bottom,
                    Height = 34,
                    Margin = new Padding(0, 0, 0, 4),
                    Series = new ISeries[]
                    {
                        new LineSeries<double>
                        {
                            Values = sparklineValues,
                            GeometrySize = 0,
                            LineSmoothness = 0.65,
                            Fill = new SolidColorPaint(new SKColor(accentColor.R, accentColor.G, accentColor.B, 35)),
                            Stroke = new SolidColorPaint(new SKColor(accentColor.R, accentColor.G, accentColor.B), 2)
                        }
                    },
                    XAxes = new Axis[] { new Axis { IsVisible = false } },
                    YAxes = new Axis[] { new Axis { IsVisible = false } },
                    LegendPosition = LegendPosition.Hidden,
                    TooltipPosition = TooltipPosition.Hidden
                };
                card.Controls.Add(spark);
            }

            // ---------- عدّاد متحرك (Count-up) اختياري - بيبدأ من صفر ويوصل للقيمة النهائية بسلاسة ----------
            if (counterTarget.HasValue)
            {
                double target = counterTarget.Value;
                double current = 0;
                System.Windows.Forms.Timer counterTimer = new System.Windows.Forms.Timer { Interval = 20 };
                counterTimer.Tick += (s, e) =>
                {
                    if (card.IsDisposed || lblValue.IsDisposed)
                    {
                        counterTimer.Stop();
                        counterTimer.Dispose();
                        return;
                    }
                    current += (target - current) * 0.25;
                    if (Math.Abs(target - current) < Math.Max(0.5, Math.Abs(target) * 0.002))
                    {
                        lblValue.Text = value; // القيمة النهائية بالظبط زي ما اتبعتت، من غير أي فرق تقريب
                        counterTimer.Stop();
                        counterTimer.Dispose();
                        activeKpiCounterTimers.Remove(counterTimer);
                        return;
                    }
                    lblValue.Text = FormatKpiCounter(current, isCurrency);
                };
                activeKpiCounterTimers.Add(counterTimer);
                counterTimer.Start();
            }

            if (onClick != null)
            {
                EventHandler handler = (s, e) => onClick();
                card.Cursor = Cursors.Hand;
                card.Click += handler;
                foreach (Control child in new Control[] { lblIcon, lblIconText, lblTitle, lblValue })
                {
                    child.Cursor = Cursors.Hand;
                    child.Click += handler;
                }
            }

            return card;
        }

        // ==========================================================================
        // دالة مساعدة: بتفتح لون معين (بتقرّبه من الأبيض) عشان خلفية مربع الأيقونة
        // ==========================================================================
        private static string FormatKpiCounter(double value, bool isCurrency) =>
            isCurrency ? value.ToString("N2") + " ج.م" : Math.Round(value).ToString("N0");

        private static Color LightTint(Color c, float amount) => UIHelpers.LightTint(c, amount);

        // ==========================================================================
        // الاختصارات السريعة: F2 بيع سريع | F4 سند قبض | F5 سند صرف | F6 تقرير يومي | Ctrl+F بحث شامل
        // ==========================================================================
        private void MainShell_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.F)
            {
                OpenUniversalSearch();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F2)
            {
                NavigateToPageKey(PageKeys.SalesBlazor);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F3)
            {
                NavigateToPageKey(PageKeys.PurchasesBlazor);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F4)
            {
                NavigateToPageKey(PageKeys.TreasuryBlazor, "قبض");
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                NavigateToPageKey(PageKeys.TreasuryBlazor, "صرف");
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F6)
            {
                NavigateToPageKey(PageKeys.ReportsBlazor);
                e.Handled = true;
            }
        }

        // ==========================================================================
        // فتح شاشة البحث الشامل والانتقال للصفحة المناسبة حسب اختيار المستخدم
        // ==========================================================================
        private void OpenUniversalSearch()
        {
            using (UniversalSearchForm searchForm = new UniversalSearchForm())
            {
                if (searchForm.ShowDialog(this) == DialogResult.OK && searchForm.SelectedPageKey != null)
                {
                    NavigateToPageKey(searchForm.SelectedPageKey, highlightKey: searchForm.SelectedItemKey);
                }
            }
        }

        // ==========================================================================
        // عرض صفحة مؤقتة (Placeholder) لحد ما ننقل الصفحات الحقيقية في خطوة 4
        // ==========================================================================
        private void ShowPlaceholderPage(string pageName, string icon)
        {
            pnlContent.Controls.Clear();

            Label lblPlaceholder = new Label
            {
                Text = icon + "\n\n" + "صفحة \"" + pageName + "\" هتتحط هنا في الخطوة الجاية",
                Font = new Font("Segoe UI", 16F),
                ForeColor = Color.FromArgb(85, 92, 102),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };

            pnlContent.Controls.Add(lblPlaceholder);
        }
    }
}
