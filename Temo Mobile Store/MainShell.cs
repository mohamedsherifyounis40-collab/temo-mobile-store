using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using Microsoft.Data.Sqlite;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WinForms;
using LiveChartsCore.Measure;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // MainShell: الهيكل العام الرئيسي للتطبيق (Sidebar + Top Bar + Content Area)
    // كل الصفحات دلوقتي متربطة بـ UserControls حقيقية (SalesPageControl، InventoryPageControl، إلخ)
    // مش placeholders. الداشبورد بيقرأ بيانات حقيقية من قاعدة البيانات (مبيعات، مخزون، خزينة).
    // ==========================================================================
    public partial class MainShell : Form
    {
        // ---------- مفاتيح الصفحات (بدل الـ Magic Strings) ----------
        // استخدام PageKeys.Sales بدل ما نكتب "Sales" كنص حر في أكتر من مكان، عشان لو غلطنا إملائيًا
        // الكومبايلر يوريه لينا خطأ فورًا بدل ما نكتشفه وقت التشغيل بس.
        public static class PageKeys
        {
            public const string Home = "Home";
            public const string Sales = "Sales";
            public const string Inventory = "Inventory";
            public const string Imei = "Imei";
            public const string InventoryCount = "InventoryCount";
            public const string Maintenance = "Maintenance";
            public const string Customers = "Customers";
            public const string Suppliers = "Suppliers";
            public const string Treasury = "Treasury";
            public const string Reports = "Reports";
            public const string Accounts = "Accounts";
            public const string Settings = "Settings";
        }

        // نفس الـ Connection String المستخدم في Form1.cs بالظبط عشان نقرأ من نفس قاعدة البيانات
        // ---------- ألوان الهيكل العام ----------
        private static readonly Color ColorSidebarBg = Color.FromArgb(19, 32, 58);       // كحلي غامق جدًا - خلفية الـ Sidebar
        private static readonly Color ColorSidebarActive = Color.FromArgb(41, 98, 189);  // أزرق - العنصر المفعّل حاليًا
        private static readonly Color ColorSidebarText = Color.FromArgb(190, 200, 218);  // رمادي فاتح - نص العناصر غير المفعّلة
        private static readonly Color ColorTopBarBg = Color.White;
        private static readonly Color ColorContentBg = Color.FromArgb(245, 246, 250);
        private static readonly Color ColorTitleText = Color.FromArgb(26, 43, 76);

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

        // تايمر التحديث التلقائي: بيحدّث جرس الإشعارات، وكمان يعيد بناء صفحة الداشبورد
        // لو هي المفتوحة دلوقتي، عشان الأرقام تفضل حديثة من غير ما المستخدم يغيّر الصفحة يدويًا
        private System.Windows.Forms.Timer autoRefreshTimer;

        // ---------- بيانات عناصر القائمة الجانبية (المفتاح، النص الظاهر، الأيقونة) ----------
        // ملحوظة: ده مجرد تعريف مؤقت للتجربة. في خطوة 4 هنربط كل مفتاح بالـ UserControl الحقيقي بتاعه.
        private readonly (string Key, string Text, string Icon)[] navItems = new (string, string, string)[]
        {
            (PageKeys.Home,           "الرئيسية",   "🏠"),
            (PageKeys.Sales,          "المبيعات",   "🛒"),
            (PageKeys.Inventory,      "المخزون",    "🗄️"),
            (PageKeys.Imei,           "الأجهزة والسيريالات", "📱"),
            (PageKeys.InventoryCount, "جرد المخزن", "📋"),
            (PageKeys.Maintenance,    "الصيانة",    "🔧"),
            (PageKeys.Customers,      "العملاء",    "👥"),
            (PageKeys.Suppliers,      "الموردين",   "🚚"),
            (PageKeys.Treasury,       "الخزينة",    "🏦"),
            (PageKeys.Reports,        "التقارير",   "📊"),
            (PageKeys.Accounts,       "الحسابات",   "📒"),
            (PageKeys.Settings,       "الإعدادات",  "⚙️"),
        };

        public MainShell()
        {
            InitializeShell();
        }

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
                if (currentPageKey == PageKeys.Home)
                    BuildHomeDashboardPage();
            };
            autoRefreshTimer.Start();

            // إيقاف التايمر لما الفورم تتقفل عشان منسيبش حاجة شغالة في الخلفية من غير داعي
            this.FormClosed += (s, e) => autoRefreshTimer.Stop();
        }

        // ==========================================================================
        // بناء القائمة الجانبية (Sidebar)
        // ==========================================================================
        private void BuildSidebar()
        {
            pnlSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 220,
                BackColor = ColorSidebarBg
            };

            // شعار المتجر أعلى الـ Sidebar
            Label lblLogo = new Label
            {
                Text = "📱 TEMO\nMOBILE STORE",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 90
            };

            // العناصر المخصصة للأدمن بس - نفس القائمة بالظبط اللي في Form1.cs (ApplyEmployeeRestrictions)
            var adminOnlyKeys = new HashSet<string> { PageKeys.Customers, PageKeys.Suppliers, PageKeys.Reports, PageKeys.Accounts, PageKeys.Settings, PageKeys.InventoryCount };
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
                Guna2Button btn = new Guna2Button
                {
                    Text = "      " + item.Icon + "   " + item.Text,
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
                sidebarButtons.Insert(0, btn);
                pnlNavButtons.Controls.Add(btn);
            }

            // ترتيب الإضافة هنا مهم: اللوجو Dock.Top الأول، بعدين الأزرار تحته مباشرة
            pnlSidebar.Controls.Add(pnlNavButtons);
            pnlSidebar.Controls.Add(lblLogo);

            this.Controls.Add(pnlSidebar);
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
                Location = new Point(20, 16)
            };

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

            // بانل يمين الشريط العلوي يحمل التاريخ - بنستخدم Dock.Right عشان يظبط مكانه صح
            // مهما كان عرض الشاشة، بدل ما نحسب الإحداثيات يدويًا (اللي ممكن يغلط لو الشاشة اتغير حجمها)
            Panel pnlDateHolder = new Panel
            {
                Dock = DockStyle.Right,
                Width = 220
            };
            lblDate = new Label
            {
                Text = "📅 " + arabicDate,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(85, 92, 102),
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill
            };
            pnlDateHolder.Controls.Add(lblDate);

            // بانل يسار الشريط العلوي يحمل جرس الإشعارات
            Panel pnlBellHolder = new Panel
            {
                Dock = DockStyle.Left,
                Width = 60
            };
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

            pnlTopBar.Controls.Add(lblPageTitle);
            pnlTopBar.Controls.Add(pnlDateHolder);
            pnlTopBar.Controls.Add(pnlBellHolder);
            this.Controls.Add(pnlTopBar);

            RefreshNotificationBadge();
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
                Padding = new Padding(20)
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
        private void NavigateToPageKey(string pageKey, string presetMovementType = null)
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
            else if (pageKey == PageKeys.Sales)
                BuildSalesPage();
            else if (pageKey == PageKeys.Inventory)
                BuildInventoryPage();
            else if (pageKey == PageKeys.Imei)
                BuildImeiPage();
            else if (pageKey == PageKeys.InventoryCount)
                BuildInventoryCountPage();
            else if (pageKey == PageKeys.Customers)
                BuildCustomersPage();
            else if (pageKey == PageKeys.Maintenance)
                BuildMaintenancePage();
            else if (pageKey == PageKeys.Treasury)
                BuildTreasuryPage(presetMovementType);
            else if (pageKey == PageKeys.Suppliers)
                BuildSuppliersPage();
            else if (pageKey == PageKeys.Reports)
                BuildReportsPage();
            else if (pageKey == PageKeys.Accounts)
                BuildAccountsPage();
            else if (pageKey == PageKeys.Settings)
                BuildSettingsPage();
            else
                ShowPlaceholderPage(matchedItem.Text, matchedItem.Icon);
        }

        // ==========================================================================
        // بناء صفحة "الإعدادات" - بتستخدم إعدادات المحل + النسخ الاحتياطي + المستخدمين (SettingsPageControl)
        // ==========================================================================
        private void BuildSettingsPage()
        {
            pnlContent.Controls.Clear();
            SettingsPageControl settingsPage = new SettingsPageControl { Dock = DockStyle.Fill };
            pnlContent.Controls.Add(settingsPage);
        }

        // ==========================================================================
        // بناء صفحة "الحسابات" - بتستخدم شجرة الحسابات وقائمة الدخل وميزان المراجعة (AccountsPageControl)
        // ==========================================================================
        private void BuildAccountsPage()
        {
            pnlContent.Controls.Clear();
            AccountsPageControl accountsPage = new AccountsPageControl { Dock = DockStyle.Fill };
            pnlContent.Controls.Add(accountsPage);
        }

        // ==========================================================================
        // بناء صفحة "التقارير" - بتستخدم شاشة التقارير وإقفال اليوم الحقيقية (ReportsPageControl)
        // ==========================================================================
        private void BuildReportsPage()
        {
            pnlContent.Controls.Clear();
            ReportsPageControl reportsPage = new ReportsPageControl { Dock = DockStyle.Fill };
            pnlContent.Controls.Add(reportsPage);
        }

        // ==========================================================================
        // بناء صفحة "الموردين" - بتستخدم شاشة الموردين والمشتريات الحقيقية (SuppliersPageControl)
        // ==========================================================================
        private void BuildSuppliersPage()
        {
            pnlContent.Controls.Clear();
            SuppliersPageControl suppliersPage = new SuppliersPageControl { Dock = DockStyle.Fill };
            pnlContent.Controls.Add(suppliersPage);
        }

        // ==========================================================================
        // بناء صفحة "الخزينة" - بتستخدم شاشة المصروفات وحركة القبض/الصرف (TreasuryPageControl)
        // ==========================================================================
        private void BuildTreasuryPage(string presetMovementType = null)
        {
            pnlContent.Controls.Clear();
            TreasuryPageControl treasuryPage = new TreasuryPageControl { Dock = DockStyle.Fill };
            pnlContent.Controls.Add(treasuryPage);
            if (presetMovementType != null)
                treasuryPage.ShowMovementEntry(presetMovementType);
        }

        // ==========================================================================
        // بناء صفحة "الصيانة" - بتستخدم شاشة الصيانة الحقيقية (MaintenancePageControl)
        // ==========================================================================
        private void BuildMaintenancePage()
        {
            pnlContent.Controls.Clear();
            MaintenancePageControl maintenancePage = new MaintenancePageControl { Dock = DockStyle.Fill };
            pnlContent.Controls.Add(maintenancePage);
        }

        // ==========================================================================
        // بناء صفحة "العملاء" - بتستخدم شاشة العملاء الحقيقية (CustomersPageControl)
        // ==========================================================================
        private void BuildCustomersPage()
        {
            pnlContent.Controls.Clear();
            CustomersPageControl customersPage = new CustomersPageControl { Dock = DockStyle.Fill };
            pnlContent.Controls.Add(customersPage);
        }

        // ==========================================================================
        // بناء صفحة "المخزون" - بتستخدم شاشة إدارة المخزن الحقيقية (InventoryPageControl)
        // ==========================================================================
        private void BuildInventoryPage()
        {
            pnlContent.Controls.Clear();
            InventoryPageControl inventoryPage = new InventoryPageControl { Dock = DockStyle.Fill };
            pnlContent.Controls.Add(inventoryPage);
        }

        // ==========================================================================
        // بناء صفحة "الأجهزة والسيريالات" - بتستخدم شاشة IMEI الحقيقية (ImeiPageControl)
        // ==========================================================================
        private void BuildImeiPage()
        {
            pnlContent.Controls.Clear();
            ImeiPageControl imeiPage = new ImeiPageControl { Dock = DockStyle.Fill };
            pnlContent.Controls.Add(imeiPage);
        }

        // ==========================================================================
        // بناء صفحة "جرد المخزن" - بتستخدم شاشة الجرد الحقيقية (InventoryCountPageControl)
        // ==========================================================================
        private void BuildInventoryCountPage()
        {
            pnlContent.Controls.Clear();
            InventoryCountPageControl countPage = new InventoryCountPageControl { Dock = DockStyle.Fill };
            pnlContent.Controls.Add(countPage);
        }

        // ==========================================================================
        // بناء صفحة "المبيعات" - بتستخدم شاشة نقطة البيع الحقيقية (SalesPageControl)
        // ==========================================================================
        private void BuildSalesPage()
        {
            pnlContent.Controls.Clear();
            SalesPageControl salesPage = new SalesPageControl { Dock = DockStyle.Fill };
            pnlContent.Controls.Add(salesPage);
        }

        // ==========================================================================
        // بناء صفحة "الرئيسية" بكروت الـ KPI - بيانات حقيقية من قاعدة البيانات (لليوم الحالي)
        // ==========================================================================
        private void BuildHomeDashboardPage()
        {
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

            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                AutoScroll = false,
                BackColor = ColorContentBg
            };

            flow.Controls.Add(CreateKpiCard("💰", Color.FromArgb(41, 98, 189), "إجمالي المبيعات", todaySales.ToString("N2") + " ج.م"));
            flow.Controls.Add(CreateKpiCard("🧾", Color.FromArgb(142, 68, 173), "إجمالي المشتريات", todayPurchases.ToString("N2") + " ج.م"));
            flow.Controls.Add(CreateKpiCard("💸", Color.FromArgb(192, 57, 43), "إجمالي المصروفات", todayExpenses.ToString("N2") + " ج.م"));
            flow.Controls.Add(CreateKpiCard("📈", Color.FromArgb(39, 174, 96), "صافي الربح", todayProfit.ToString("N2") + " ج.م"));
            flow.Controls.Add(CreateKpiCard("📄", Color.FromArgb(22, 160, 133), "عدد الفواتير", invoiceCount.ToString()));
            flow.Controls.Add(CreateKpiCard("🔧", Color.FromArgb(230, 126, 34), "طلبات الصيانة", maintenanceCount.ToString()));

            Label lblBalancesTitle = new Label
            {
                Text = "💳 أرصدة وسائل الدفع (الخزينة)",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ColorTitleText,
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 12, 0, 6)
            };

            FlowLayoutPanel flowBalances = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                AutoScroll = false,
                BackColor = ColorContentBg
            };

            flowBalances.Controls.Add(CreateKpiCard("🏦", ColorTitleText, "الإجمالي (كل الوسائل)", totalBalance.ToString("N2") + " ج.م"));
            var methodIcons = new Dictionary<string, string> { { "نقدي", "💵" }, { "فوري", "📲" }, { "أمان", "🔒" }, { "سهولة", "✅" }, { "فودافون كاش", "🔴" }, { "إنستاباي", "🏛️" } };
            var methodColors = new Dictionary<string, Color> {
                { "نقدي", Color.FromArgb(39, 174, 96) }, { "فوري", Color.FromArgb(230, 126, 34) },
                { "أمان", Color.FromArgb(41, 98, 189) }, { "سهولة", Color.FromArgb(22, 160, 133) },
                { "فودافون كاش", Color.FromArgb(192, 57, 43) }, { "إنستاباي", Color.FromArgb(142, 68, 173) }
            };
            foreach (var mb in methodBalances)
                flowBalances.Controls.Add(CreateKpiCard(methodIcons[mb.Method], methodColors[mb.Method], mb.Method, mb.Balance.ToString("N2") + " ج.م"));

            // ---------- الرسم البياني: المبيعات خلال آخر 7 أيام ----------
            var salesTrend = GetLast7DaysSales();

            CartesianChart chart = new CartesianChart
            {
                Dock = DockStyle.Fill,
                Series = new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Values = salesTrend.Values,
                        Name = "المبيعات",
                        GeometrySize = 8,
                        LineSmoothness = 0.4
                    }
                },
                XAxes = new Axis[] { new Axis { Labels = salesTrend.Labels, TextSize = 11 } },
                LegendPosition = LegendPosition.Hidden
            };

            Label lblChartTitle = new Label
            {
                Text = "📈 المبيعات خلال آخر 7 أيام",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ColorTitleText,
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 15, 0, 8)
            };

            // ---------- كارت أكثر المنتجات مبيعًا خلال آخر 7 أيام ----------
            Guna2Panel pnlTopProducts = new Guna2Panel
            {
                Dock = DockStyle.Right,
                Width = 280,
                FillColor = Color.White,
                BorderRadius = 14,
                BorderColor = Color.FromArgb(230, 232, 238),
                BorderThickness = 1,
                Margin = new Padding(15, 15, 0, 0)
            };
            Label lblTopProductsTitle = new Label
            {
                Text = "🏆 الأكثر مبيعًا (7 أيام)",
                Location = new Point(15, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = ColorTitleText
            };
            pnlTopProducts.Controls.Add(lblTopProductsTitle);

            var topProducts = GetTopSellingProducts();
            if (topProducts.Count == 0)
            {
                Label lblEmpty = new Label { Text = "مفيش مبيعات مسجّلة في آخر 7 أيام.", Location = new Point(15, 55), AutoSize = true, ForeColor = Color.FromArgb(85, 92, 102), Font = new Font("Segoe UI", 8.5F) };
                pnlTopProducts.Controls.Add(lblEmpty);
            }
            else
            {
                int y = 55;
                string[] medals = { "🥇", "🥈", "🥉", "4️⃣", "5️⃣" };
                for (int i = 0; i < topProducts.Count; i++)
                {
                    Label lblItem = new Label
                    {
                        Text = $"{medals[i]}  {topProducts[i].Name}",
                        Location = new Point(15, y),
                        Size = new Size(200, 20),
                        Font = new Font("Segoe UI", 9F),
                        ForeColor = ColorTitleText
                    };
                    Label lblQty = new Label
                    {
                        Text = $"{topProducts[i].Qty} قطعة",
                        Location = new Point(15, y + 20),
                        AutoSize = true,
                        Font = new Font("Segoe UI", 8F),
                        ForeColor = Color.FromArgb(85, 92, 102)
                    };
                    pnlTopProducts.Controls.Add(lblItem);
                    pnlTopProducts.Controls.Add(lblQty);
                    y += 48;
                }
            }

            // ترتيب الإضافة هنا مهم: الرسم البياني (Fill) الأول، بعدين العنوان (Top)،
            // وأخيرًا صف الكروت (Top) عشان يفضل هو أقرب حاجة لحافة الشاشة العليا
            pnlContent.Controls.Add(chart);
            pnlContent.Controls.Add(pnlTopProducts);
            pnlContent.Controls.Add(lblChartTitle);
            pnlContent.Controls.Add(flowBalances);
            pnlContent.Controls.Add(lblBalancesTitle);
            pnlContent.Controls.Add(flow);
        }

        // ==========================================================================
        // أكثر 5 منتجات مبيعًا (بالكمية) خلال آخر 7 أيام
        // ==========================================================================
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
        // بناء كارت KPI واحد (أيقونة + عنوان + قيمة)
        // ==========================================================================
        private Panel CreateKpiCard(string icon, Color accentColor, string title, string value)
        {
            Panel card = new Panel
            {
                Width = 195,
                Height = 105,
                BackColor = Color.White,
                Margin = new Padding(8)
            };

            Label lblIcon = new Label
            {
                Text = icon,
                Font = new Font("Segoe UI", 16F),
                BackColor = LightTint(accentColor, 0.85f),
                ForeColor = accentColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(15, 15),
                Size = new Size(42, 42)
            };

            Label lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(85, 92, 102),
                AutoSize = true,
                Location = new Point(15, 65)
            };

            Label lblValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = ColorTitleText,
                AutoSize = true,
                Location = new Point(15, 84)
            };

            card.Controls.Add(lblIcon);
            card.Controls.Add(lblTitle);
            card.Controls.Add(lblValue);

            return card;
        }

        // ==========================================================================
        // دالة مساعدة: بتفتح لون معين (بتقرّبه من الأبيض) عشان خلفية مربع الأيقونة
        // ==========================================================================
        private static Color LightTint(Color c, float amount)
        {
            int r = c.R + (int)((255 - c.R) * amount);
            int g = c.G + (int)((255 - c.G) * amount);
            int b = c.B + (int)((255 - c.B) * amount);
            return Color.FromArgb(r, g, b);
        }

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
                NavigateToPageKey(PageKeys.Sales);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F3)
            {
                NavigateToPageKey(PageKeys.Suppliers);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F4)
            {
                NavigateToPageKey(PageKeys.Treasury, "قبض");
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                NavigateToPageKey(PageKeys.Treasury, "صرف");
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F6)
            {
                NavigateToPageKey(PageKeys.Reports);
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
                    NavigateToPageKey(searchForm.SelectedPageKey);
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
