using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using Guna.UI2.WinForms;
using System.Data;
using Microsoft.Data.Sqlite;
using System.Windows.Forms;
using System.Drawing.Printing;

namespace Temo_Mobile_Store
{
    public partial class Form1 : Form
    {
        private string connectionString = $"Data Source={System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TemoStoreDB.db")};";

        // أدوات المخزن
        private TextBox txtBarcode, txtProductName, txtCostPrice, txtSalePrice, txtQuantity;
        private Label lblCostPrice;
        private CheckBox chkIsSerialized;
        private Guna2Button btnAddProduct, btnEditMode, btnSaveUpdate, btnDeleteProduct, btnClear;
        private DataGridView dgvProducts;

        // أدوات المبيعات (الكاشير)
        private TextBox txtSaleBarcode, txtSaleName, txtCustomerPrice, txtSaleQty, txtSaleTotal;
        private Guna2ComboBox cmbSalePaymentType, cmbSaleCustomer, cmbSaleImei;
        private Label lblSaleImei;
        private Guna2Button btnAddToBill;
        private Guna2Button btnPrintInvoice;
        private Guna2ComboBox cmbInvoicePaperSize;
        private DataGridView dgvSales;

        // أدوات شاشة التقارير والأرباح المحدثة 📊
        private Label lblTotalSalesVal, lblTotalCapitalVal, lblTotalExpensesVal, lblTotalNetProfitVal;
        private DataGridView dgvReports;
        private Guna2Button btnRefreshReports, btnFilterReports;
        private DateTimePicker dtpFrom, dtpTo; // أدوات الفلترة بالتواريخ

        // أدوات شاشة المصروفات والشجرة المحدثة
        private ComboBox cmbExpenseAccounts;
        private TextBox txtExpenseAmount;
        private Guna2Button btnAddExpense, btnEditExpenseMode, btnSaveExpenseUpdate, btnDeleteExpense;
        private DataGridView dgvExpenses;
        private int selectedExpenseID = -1; // الهوية اللونية للتطبيق
        private DateTime selectedExpenseDate = DateTime.MinValue;
        private static readonly Color ColorPrimary = Color.FromArgb(26, 43, 76);      // كحلي غامق - العناوين والأساسي
        private static readonly Color ColorSuccess = Color.FromArgb(39, 174, 96);     // أخضر - إضافة وحفظ
        private static readonly Color ColorDanger = Color.FromArgb(231, 76, 60);      // أحمر - حذف وتحذير
        private static readonly Color ColorWarning = Color.FromArgb(243, 156, 18);    // برتقالي - تنبيه/إتمام بيع
        private static readonly Color ColorNeutral = Color.FromArgb(236, 240, 241);   // رمادي فاتح - أزرار ثانوية
        private static readonly Color ColorBackground = Color.FromArgb(245, 246, 250); // خلفية الفورم

        // أدوات النسخ الاحتياطي والاسترجاع 💾
        private DataGridView dgvBackups;
        private Guna2Button btnCreateBackupNow, btnRestoreBackup, btnDeleteBackup, btnOpenBackupFolder;
        private Label lblBackupStatus, lblCloudStatus;
        private string BackupFolderPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TemoStore_Backups");

        // إعدادات المحل: نسخة محفوظة في الذاكرة عشان أي شاشة تانية (زي الطباعة) تقدر تستخدمها فورًا من غير ما تعمل استعلام لقاعدة البيانات كل مرة
        private string CurrentStoreName = "Temo Mobile Store";
        private string CurrentStorePhone = "";
        private string CurrentStoreAddress = "";
        private byte[] CurrentStoreLogo = null;

        private PictureBox picStoreLogo;
        private TextBox txtSettingsStoreName, txtSettingsPhone, txtSettingsAddress;
        private Guna2Button btnUploadLogo, btnRemoveLogo, btnSaveStoreSettings;

        // أدوات شاشة جرد المخزن 📋
        private DataGridView dgvInventoryCount;
        private TextBox txtInventorySearch;
        private Guna2Button btnRefreshInventoryCount, btnSaveInventoryCount, btnViewAdjustmentsLog;

        // مرجع للتاب كنترول الرئيسي، مطلوب عشان اختصارات لوحة المفاتيح تقدر تنقل بين التابات
        private TabControl mainTabControl;

        public Form1()
        {
            this.Text = "نظام إدارة تيمو ستور - إصدار تواريخ وتعديل المصروفات المطور V7";
            this.Size = new Size(1150, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;

            this.FormClosing += Form1_FormClosing;

            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;

            RunAutomaticDailyBackup();

            EnsureClosureTablesExist();
            EnsureCashMovementsAccountColumn();
            EnsureCashMovementsSupplierColumn();
            EnsurePurchasesTablesExist();
            EnsureCustomersTableExists();
            EnsureSalesCreditColumns();
            EnsureCashMovementsCustomerColumn();
            EnsureProductsSerializedColumn();
            EnsureSalesImeiColumn();
            EnsureProductUnitsTableExists();
            EnsureMaintenanceTableExists();
            EnsureInventoryAdjustmentsTableExists();
            EnsureStoreSettingsTableExists();
            LoadStoreSettingsIntoMemory();
            InitializeTabs();
            LoadProductsData();
            LoadSalesData();
            LoadAccountsTreeIntoCombo();
            LoadExpensesData();
            CalculateBusinessMetrics();
            RefreshClosureSummary();
        }

        private static readonly string[] AllPaymentMethods = { "نقدي", "فوري", "أمان", "سهولة", "فودافون كاش", "إنستاباي" };

        // بيضيف عمود AccountCode لجدول CashMovements الموجود عندك بالفعل لو مش موجود، من غير ما يأثر على أي بيانات قديمة
        private void EnsureCashMovementsAccountColumn()
        {
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                bool columnExists = false;
                using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(CashMovements);", conn))
                {
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader["name"].ToString() == "AccountCode")
                            {
                                columnExists = true;
                                break;
                            }
                        }
                    }
                }

                if (!columnExists)
                {
                    using (SqliteCommand cmd = new SqliteCommand("ALTER TABLE CashMovements ADD COLUMN AccountCode INTEGER;", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        // بيضيف عمود SupplierId لجدول CashMovements عشان نربط سداد الموردين بيه
        private void EnsureCashMovementsSupplierColumn()
        {
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                bool columnExists = false;
                using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(CashMovements);", conn))
                {
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader["name"].ToString() == "SupplierId")
                            {
                                columnExists = true;
                                break;
                            }
                        }
                    }
                }

                if (!columnExists)
                {
                    using (SqliteCommand cmd = new SqliteCommand("ALTER TABLE CashMovements ADD COLUMN SupplierId INTEGER;", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        // بيعمل جداول الموردين وفواتير الشراء لو مش موجودين
        private void EnsurePurchasesTablesExist()
        {
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                string createSuppliers = @"CREATE TABLE IF NOT EXISTS Suppliers (
                    SupplierId INTEGER PRIMARY KEY AUTOINCREMENT,
                    SupplierName TEXT NOT NULL,
                    Phone TEXT,
                    CreatedAt TEXT NOT NULL
                );";

                string createPurchases = @"CREATE TABLE IF NOT EXISTS Purchases (
                    PurchaseId INTEGER PRIMARY KEY AUTOINCREMENT,
                    SupplierId INTEGER NOT NULL,
                    PurchaseDate TEXT NOT NULL,
                    TotalAmount DECIMAL NOT NULL,
                    Notes TEXT,
                    FOREIGN KEY (SupplierId) REFERENCES Suppliers(SupplierId)
                );";

                string createPurchaseItems = @"CREATE TABLE IF NOT EXISTS PurchaseItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PurchaseId INTEGER NOT NULL,
                    Barcode TEXT,
                    ProductName TEXT NOT NULL,
                    Quantity INTEGER NOT NULL,
                    UnitCost DECIMAL NOT NULL,
                    LineTotal DECIMAL NOT NULL,
                    FOREIGN KEY (PurchaseId) REFERENCES Purchases(PurchaseId)
                );";

                using (SqliteCommand cmd = new SqliteCommand(createSuppliers, conn)) cmd.ExecuteNonQuery();
                using (SqliteCommand cmd = new SqliteCommand(createPurchases, conn)) cmd.ExecuteNonQuery();
                using (SqliteCommand cmd = new SqliteCommand(createPurchaseItems, conn)) cmd.ExecuteNonQuery();
            }
        }

        // بيعمل جدول العملاء لو مش موجود
        private void EnsureCustomersTableExists()
        {
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                string createCustomers = @"CREATE TABLE IF NOT EXISTS Customers (
                    CustomerId INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerName TEXT NOT NULL,
                    Phone TEXT,
                    CreatedAt TEXT NOT NULL
                );";
                using (SqliteCommand cmd = new SqliteCommand(createCustomers, conn)) cmd.ExecuteNonQuery();
            }
        }

        // بيضيف عمودين لجدول Sales: CustomerId و PaymentType (كاش/آجل) لو مش موجودين
        private void EnsureSalesCreditColumns()
        {
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                var existingColumns = new HashSet<string>();
                using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(Sales);", conn))
                {
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read()) existingColumns.Add(reader["name"].ToString());
                    }
                }

                if (!existingColumns.Contains("CustomerId"))
                {
                    using (SqliteCommand cmd = new SqliteCommand("ALTER TABLE Sales ADD COLUMN CustomerId INTEGER;", conn)) cmd.ExecuteNonQuery();
                }
                if (!existingColumns.Contains("PaymentType"))
                {
                    using (SqliteCommand cmd = new SqliteCommand("ALTER TABLE Sales ADD COLUMN PaymentType TEXT;", conn)) cmd.ExecuteNonQuery();
                }
            }
        }

        // بيضيف عمود CustomerId لجدول CashMovements عشان نربط سداد العملاء بيه
        private void EnsureCashMovementsCustomerColumn()
        {
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                bool columnExists = false;
                using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(CashMovements);", conn))
                {
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader["name"].ToString() == "CustomerId") { columnExists = true; break; }
                        }
                    }
                }

                if (!columnExists)
                {
                    using (SqliteCommand cmd = new SqliteCommand("ALTER TABLE CashMovements ADD COLUMN CustomerId INTEGER;", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        // بيضيف عمود IsSerialized لجدول Products (هل المنتج ده بيتتبع بالـ IMEI ولا لأ)
        private void EnsureProductsSerializedColumn()
        {
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                bool columnExists = false;
                using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(Products);", conn))
                {
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader["name"].ToString() == "IsSerialized") { columnExists = true; break; }
                        }
                    }
                }

                if (!columnExists)
                {
                    using (SqliteCommand cmd = new SqliteCommand("ALTER TABLE Products ADD COLUMN IsSerialized INTEGER DEFAULT 0;", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        // بيضيف عمود IMEI لجدول Sales عشان نسجل رقم الجهاز اللي اتباع بالظبط
        private void EnsureSalesImeiColumn()
        {
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                bool columnExists = false;
                using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(Sales);", conn))
                {
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader["name"].ToString() == "IMEI") { columnExists = true; break; }
                        }
                    }
                }

                if (!columnExists)
                {
                    using (SqliteCommand cmd = new SqliteCommand("ALTER TABLE Sales ADD COLUMN IMEI TEXT;", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        // بيعمل جدول الأجهزة (كل وحدة موبايل لوحدها برقم IMEI وحالتها)
        private void EnsureProductUnitsTableExists()
        {
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                string createTable = @"CREATE TABLE IF NOT EXISTS ProductUnits (
                    UnitId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Barcode TEXT NOT NULL,
                    IMEI TEXT NOT NULL UNIQUE,
                    Status TEXT NOT NULL DEFAULT 'InStock',
                    PurchaseId INTEGER,
                    SaleId INTEGER,
                    CreatedAt TEXT NOT NULL
                );";
                using (SqliteCommand cmd = new SqliteCommand(createTable, conn)) cmd.ExecuteNonQuery();
            }
        }

        // بيعمل جدول تذاكر الصيانة لو مش موجود
        private void EnsureMaintenanceTableExists()
        {
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                string createTable = @"CREATE TABLE IF NOT EXISTS MaintenanceTickets (
                    TicketId INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerName TEXT NOT NULL,
                    CustomerPhone TEXT,
                    DeviceInfo TEXT NOT NULL,
                    IssueDescription TEXT,
                    ReceivedDate TEXT NOT NULL,
                    EstimatedCost DECIMAL,
                    ActualCost DECIMAL,
                    Status TEXT NOT NULL DEFAULT 'مستلم',
                    DeliveredDate TEXT,
                    Notes TEXT
                );";
                using (SqliteCommand cmd = new SqliteCommand(createTable, conn)) cmd.ExecuteNonQuery();
            }
        }

        // جدول إعدادات المحل: صف واحد بس دايمًا (Id = 1). الشعار بيتحفظ كـ BLOB جوه قاعدة البيانات نفسها
        // (مش كملف منفصل على الجهاز) عشان يتضمن تلقائيًا في أي نسخة احتياطية للبرنامج (VACUUM INTO)
        private void EnsureStoreSettingsTableExists()
        {
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                string createTable = @"CREATE TABLE IF NOT EXISTS StoreSettings (
                    Id INTEGER PRIMARY KEY CHECK (Id = 1),
                    StoreName TEXT,
                    Phone TEXT,
                    Address TEXT,
                    LogoImage BLOB
                );";
                using (SqliteCommand cmd = new SqliteCommand(createTable, conn)) cmd.ExecuteNonQuery();

                using (SqliteCommand cmdCheck = new SqliteCommand("SELECT COUNT(*) FROM StoreSettings WHERE Id = 1;", conn))
                {
                    long count = (long)cmdCheck.ExecuteScalar();
                    if (count == 0)
                    {
                        using (SqliteCommand cmdInsert = new SqliteCommand("INSERT INTO StoreSettings (Id, StoreName, Phone, Address, LogoImage) VALUES (1, 'Temo Mobile Store', '', '', NULL);", conn))
                            cmdInsert.ExecuteNonQuery();
                    }
                }
            }
        }

        // بتحمّل إعدادات المحل من قاعدة البيانات لمتغيرات في الذاكرة، يتم استدعاؤها عند فتح البرنامج وبعد أي حفظ للإعدادات
        private void LoadStoreSettingsIntoMemory()
        {
            try
            {
                using (SqliteConnection conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    using (SqliteCommand cmd = new SqliteCommand("SELECT StoreName, Phone, Address, LogoImage FROM StoreSettings WHERE Id = 1;", conn))
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            CurrentStoreName = reader["StoreName"] == DBNull.Value ? "" : reader["StoreName"].ToString();
                            CurrentStorePhone = reader["Phone"] == DBNull.Value ? "" : reader["Phone"].ToString();
                            CurrentStoreAddress = reader["Address"] == DBNull.Value ? "" : reader["Address"].ToString();
                            CurrentStoreLogo = reader["LogoImage"] == DBNull.Value ? null : (byte[])reader["LogoImage"];
                        }
                    }
                }
            }
            catch
            {
                // لو فشل التحميل لأي سبب، البرنامج بيكمل بالقيم الافتراضية بدل ما يقف
            }
        }

        // سجل كل عمليات تسوية الجرد (قبل/بعد/الفرق) عشان يكون فيه مسار تدقيق واضح لأي تعديل في كميات المخزون بسبب الجرد
        private void EnsureInventoryAdjustmentsTableExists()
        {
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                string createTable = @"CREATE TABLE IF NOT EXISTS InventoryAdjustments (
                    AdjustmentId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Barcode TEXT NOT NULL,
                    ProductName TEXT,
                    SystemQuantityBefore INTEGER NOT NULL,
                    CountedQuantity INTEGER NOT NULL,
                    Difference INTEGER NOT NULL,
                    AdjustmentDate TEXT NOT NULL
                );";
                using (SqliteCommand cmd = new SqliteCommand(createTable, conn)) cmd.ExecuteNonQuery();
            }
        }

        private void EnsureClosureTablesExist()
        {
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                string createClosures = @"CREATE TABLE IF NOT EXISTS DailyClosures (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ClosureDate TEXT NOT NULL,
                    PaymentMethod TEXT NOT NULL,
                    OpeningBalance DECIMAL NOT NULL,
                    TotalIn DECIMAL NOT NULL,
                    TotalOut DECIMAL NOT NULL,
                    ExpectedClosingBalance DECIMAL NOT NULL,
                    ActualClosingBalance DECIMAL NOT NULL,
                    Difference DECIMAL NOT NULL,
                    ClosedAt TEXT NOT NULL,
                    UNIQUE(ClosureDate, PaymentMethod)
                );";

                string createDenominations = @"CREATE TABLE IF NOT EXISTS CashDenominations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ClosureId INTEGER NOT NULL,
                    DenominationValue DECIMAL NOT NULL,
                    DenominationCount INTEGER NOT NULL,
                    LineTotal DECIMAL NOT NULL,
                    FOREIGN KEY (ClosureId) REFERENCES DailyClosures(Id)
                );";

                using (SqliteCommand cmd = new SqliteCommand(createClosures, conn)) cmd.ExecuteNonQuery();
                using (SqliteCommand cmd = new SqliteCommand(createDenominations, conn)) cmd.ExecuteNonQuery();
            }
        }

        // بيرجع مقارنة بس على مستوى اليوم (من غير وقت) - بيستخدم "نقدي" كعلامة إن اليوم مقفول لكل الوسائل مع بعض
        private bool IsDateClosed(DateTime date)
        {
            string dateStr = date.ToString("yyyy-MM-dd");
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM DailyClosures WHERE ClosureDate = @Date AND PaymentMethod = 'نقدي'", conn))
                {
                    cmd.Parameters.AddWithValue("@Date", dateStr);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        private bool IsTodayClosed() => IsDateClosed(DateTime.Now);

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

        // بيحسب ملخص الإقفال المتوقع لليوم الحالي لكل وسيلة دفع مع بعض
        // "نقدي" بتضم كمان المبيعات والمصروفات (لأن الدرج بيتأثر بيهم)، وباقي الوسائل بتعتمد بس على حركات القبض والصرف
        private Dictionary<string, (decimal opening, decimal totalIn, decimal totalOut, decimal expectedClosing)> GetAllMethodsClosureSummary()
        {
            var result = new Dictionary<string, (decimal opening, decimal totalIn, decimal totalOut, decimal expectedClosing)>();
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                decimal salesTotal = 0, expensesTotal = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Total) FROM Sales WHERE SaleDate LIKE @Today AND (PaymentType IS NULL OR PaymentType = 'Cash')", conn))
                {
                    cmd.Parameters.AddWithValue("@Today", today + "%");
                    var r = cmd.ExecuteScalar();
                    salesTotal = (r != null && r != DBNull.Value) ? Convert.ToDecimal(r) : 0;
                }
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

                    decimal totalIn = methodIn + (method == "نقدي" ? salesTotal : 0);
                    decimal totalOut = methodOut + (method == "نقدي" ? expensesTotal : 0);
                    decimal expectedClosing = opening + totalIn - totalOut;

                    result[method] = (opening, totalIn, totalOut, expectedClosing);
                }
            }

            return result;
        }

        private void RefreshClosureSummary()
        {
            if (lblOpeningBalanceVal == null || lblExpectedClosingVal == null || btnCloseDay == null) return;

            LoadUnifiedOperations();
            if (dgvStatement != null && cmbStatementMethod != null) ShowStatement(false);
            LoadDashboardData();

            if (IsTodayClosed())
            {
                lblOpeningBalanceVal.Text = "تم إقفال اليوم بالفعل ✅";
                lblExpectedClosingVal.Text = "--";
                btnCloseDay.Enabled = false;

                if (dgvClosureSummary != null)
                {
                    DataTable dtClosed = new DataTable();
                    dtClosed.Columns.AddRange(new DataColumn[] { new DataColumn("الوسيلة"), new DataColumn("افتتاحي"), new DataColumn("ختامي فعلي") });
                    string today = DateTime.Now.ToString("yyyy-MM-dd");
                    using (SqliteConnection conn = new SqliteConnection(connectionString))
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
                }
                return;
            }

            btnCloseDay.Enabled = true;
            var summaries = GetAllMethodsClosureSummary();
            var cashSummary = summaries["نقدي"];
            lblOpeningBalanceVal.Text = cashSummary.opening.ToString("N2") + " ج.م";
            lblExpectedClosingVal.Text = cashSummary.expectedClosing.ToString("N2") + " ج.م";

            if (dgvClosureSummary != null)
            {
                DataTable dtSummary = new DataTable();
                dtSummary.Columns.AddRange(new DataColumn[] { new DataColumn("الوسيلة"), new DataColumn("افتتاحي"), new DataColumn("ختامي متوقع") });
                foreach (var method in AllPaymentMethods)
                {
                    var s = summaries[method];
                    dtSummary.Rows.Add(method, s.opening.ToString("N2"), s.expectedClosing.ToString("N2"));
                }
                dgvClosureSummary.DataSource = dtSummary;
            }
        }

        private void InitializeTabs()
        {
            TabControl tabControl = new TabControl() { Dock = DockStyle.Fill };
            mainTabControl = tabControl;
            TabPage tabDashboard = new TabPage() { Text = "🏠 لوحة التحكم" };
            TabPage tabInventory = new TabPage() { Text = "إدارة المخزن والبضاعة" };
            TabPage tabOperations = new TabPage() { Text = "العمليات اليومية 🧾" };
            TabPage tabReports = new TabPage() { Text = "التقارير والأرباح 📊" };
            TabPage tabClosuresLog = new TabPage() { Text = "سجل إقفال الأيام 🔒" };
            TabPage tabStatements = new TabPage() { Text = "كشف حساب الوسائل 📋" };
            TabPage tabAccountsTree = new TabPage() { Text = "شجرة الحسابات 🌳" };
            TabPage tabIncomeStatement = new TabPage() { Text = "قائمة الدخل 📈" };
            TabPage tabTrialBalance = new TabPage() { Text = "ميزان المراجعة ⚖️" };
            TabPage tabSuppliers = new TabPage() { Text = "الموردون والمشتريات 📦" };
            TabPage tabCustomers = new TabPage() { Text = "العملاء 👥" };
            TabPage tabImei = new TabPage() { Text = "الأجهزة والسيريالات 📱" };
            TabPage tabMaintenance = new TabPage() { Text = "الصيانة 🔧" };
            TabPage tabInventoryCount = new TabPage() { Text = "جرد المخزن 📋" };

            tabControl.TabPages.AddRange(new TabPage[] { tabDashboard, tabInventory, tabOperations, tabReports, tabClosuresLog, tabStatements, tabAccountsTree, tabIncomeStatement, tabTrialBalance, tabSuppliers, tabCustomers, tabImei, tabMaintenance, tabInventoryCount });
            this.Controls.Add(tabControl);

            CreateDashboardDesign(tabDashboard);
            CreateInventoryDesign(tabInventory);
            CreateUnifiedOperationsDesign(tabOperations);
            CreateReportsDesign(tabReports);
            CreateClosuresLogDesign(tabClosuresLog);
            CreateStatementsDesign(tabStatements);
            CreateAccountsTreeDesign(tabAccountsTree);
            CreateIncomeStatementDesign(tabIncomeStatement);
            CreateTrialBalanceDesign(tabTrialBalance);
            CreateSuppliersDesign(tabSuppliers);
            CreateCustomersDesign(tabCustomers);
            CreateImeiDesign(tabImei);
            CreateMaintenanceDesign(tabMaintenance);
            CreateInventoryCountDesign(tabInventoryCount);

            if (!AuthManager.IsAdmin)
            {
                tabControl.TabPages.Remove(tabReports);
                tabControl.TabPages.Remove(tabClosuresLog);
                tabControl.TabPages.Remove(tabStatements);
                tabControl.TabPages.Remove(tabAccountsTree);
                tabControl.TabPages.Remove(tabIncomeStatement);
                tabControl.TabPages.Remove(tabTrialBalance);
                tabControl.TabPages.Remove(tabSuppliers);
                tabControl.TabPages.Remove(tabCustomers);
                tabControl.TabPages.Remove(tabInventoryCount);
                ApplyEmployeeRestrictions();
            }
            else
            {
                TabPage tabUsers = new TabPage() { Text = "إدارة المستخدمين 👤" };
                tabControl.TabPages.Add(tabUsers);
                CreateUsersManagementDesign(tabUsers);

                TabPage tabBackup = new TabPage() { Text = "النسخ الاحتياطي 💾" };
                tabControl.TabPages.Add(tabBackup);
                CreateBackupDesign(tabBackup);

                TabPage tabStoreSettings = new TabPage() { Text = "إعدادات المحل ⚙️" };
                tabControl.TabPages.Add(tabStoreSettings);
                CreateStoreSettingsDesign(tabStoreSettings);
            }
        }

        // بيقفل الصلاحيات الحساسة عن دور "موظف": مايشوفش سعر الشراء، ومايقدرش يعدّل/يلغي/يحذف أي حركة
        private void ApplyEmployeeRestrictions()
        {
            this.Text += " - وضع الموظف 👤";

            // المخزون: مايشوفش سعر الشراء ومايقدرش يضيف/يعدّل/يحذف منتجات
            if (lblCostPrice != null) lblCostPrice.Visible = false;
            if (txtCostPrice != null) txtCostPrice.Visible = false;
            if (btnAddProduct != null) btnAddProduct.Enabled = false;
            if (btnEditMode != null) btnEditMode.Enabled = false;
            if (btnSaveUpdate != null) btnSaveUpdate.Enabled = false;
            if (btnDeleteProduct != null) btnDeleteProduct.Enabled = false;

            // المبيعات: يقدر يبيع بس، مايقدرش يعدّل أو يلغي بيع
            if (btnEditSaleMode != null) btnEditSaleMode.Enabled = false;
            if (btnSaveSaleEdit != null) btnSaveSaleEdit.Enabled = false;
            if (btnCancelSale != null) btnCancelSale.Enabled = false;

            // المصروفات: يقدر يسجّل بس، مايقدرش يعدّل أو يحذف مصروف
            if (btnEditExpenseMode != null) btnEditExpenseMode.Enabled = false;
            if (btnSaveExpenseUpdate != null) btnSaveExpenseUpdate.Enabled = false;
            if (btnDeleteExpense != null) btnDeleteExpense.Enabled = false;

            // حركات القبض والصرف: يقدر يسجّل بس، مايقدرش يعدّل أو يلغي حركة، ومايقدرش يظبط رصيد يدوي
            if (btnEditMovement != null) btnEditMovement.Enabled = false;
            if (btnSaveMovementEdit != null) btnSaveMovementEdit.Enabled = false;
            if (btnCancelMovement != null) btnCancelMovement.Enabled = false;
            if (btnSetBalance != null) btnSetBalance.Enabled = false;
            if (txtNewBalance != null) txtNewBalance.Enabled = false;

            // الأجهزة والسيريالات: مايشوفش قسم الإضافة اليدوي خالص (فيه سعر شراء)
            if (gbQuickAdd != null) gbQuickAdd.Visible = false;

            // الصيانة: يقدر يستلم جهاز جديد بس، مايقدرش يعدّل الحالة أو يسلّم/يحصّل
            if (btnSaveStatus != null) btnSaveStatus.Enabled = false;
            if (btnDeliverDevice != null) btnDeliverDevice.Enabled = false;
        }

        private TextBox txtNewUsername, txtNewUserPassword;
        private Guna2ComboBox cmbNewUserRole;
        private DataGridView dgvUsers;
        private Guna2Button btnSaveUserEdit;

        private TextBox txtSupplierName, txtSupplierPhone;
        private DataGridView dgvSuppliers, dgvSupplierStatement, dgvPurchaseCart;
        private Guna2Button btnSaveSupplierEdit;
        private int selectedSupplierId = -1;

        private Guna2ComboBox cmbSupplierOperationType;
        private Panel pnlNewPurchase, pnlSupplierPayment;

        private Guna2ComboBox cmbPurchaseSupplier;
        private Guna2ComboBox cmbPurchasePaymentType, cmbPurchasePaymentMethod;
        private TextBox txtPurchaseBarcode, txtPurchaseProductName, txtPurchaseQty, txtPurchaseUnitCost, txtPurchaseSalePrice, txtPurchaseImeiList;
        private CheckBox chkPurchaseSerialized;
        private Label lblPurchaseCartTotal;
        private List<(string Barcode, string ProductName, int Qty, decimal UnitCost, decimal SalePrice, decimal LineTotal, bool IsSerialized, List<string> Imeis)> currentPurchaseItems
            = new List<(string Barcode, string ProductName, int Qty, decimal UnitCost, decimal SalePrice, decimal LineTotal, bool IsSerialized, List<string> Imeis)>();

        private Guna2ComboBox cmbPaymentSupplier, cmbSupplierPaymentMethod;
        private TextBox txtSupplierPaymentAmount;

        private void CreateSuppliersDesign(TabPage page)
        {
            GroupBox gbSupplier = new GroupBox() { Text = "إضافة / تعديل مورد", Location = new Point(20, 20), Size = new Size(280, 200) };
            Label lblSupplierName = new Label() { Text = "اسم المورد:", Location = new Point(10, 25), AutoSize = true };
            txtSupplierName = new TextBox() { Location = new Point(10, 45), Width = 250 };
            Label lblSupplierPhone = new Label() { Text = "رقم التليفون:", Location = new Point(10, 80), AutoSize = true };
            txtSupplierPhone = new TextBox() { Location = new Point(10, 100), Width = 250 };

            Guna2Button btnAddSupplier = new Guna2Button() { Text = "إضافة مورد جديد ✅", Location = new Point(10, 135), Width = 250, Height = 32, FillColor = ColorSuccess };
            btnAddSupplier.Click += BtnAddSupplier_Click;

            btnSaveSupplierEdit = new Guna2Button() { Text = "حفظ تعديل المورد 💾", Location = new Point(10, 170), Width = 120, Height = 28, FillColor = ColorWarning, Enabled = false };
            btnSaveSupplierEdit.Click += BtnSaveSupplierEdit_Click;

            Guna2Button btnDeleteSupplier = new Guna2Button() { Text = "حذف المورد ❌", Location = new Point(140, 170), Width = 120, Height = 28, FillColor = ColorDanger };
            btnDeleteSupplier.Click += BtnDeleteSupplier_Click;

            gbSupplier.Controls.AddRange(new Control[] { lblSupplierName, txtSupplierName, lblSupplierPhone, txtSupplierPhone, btnAddSupplier, btnSaveSupplierEdit, btnDeleteSupplier });

            Label lblOpType = new Label() { Text = "نوع العملية:", Location = new Point(20, 235), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            cmbSupplierOperationType = new Guna2ComboBox() { Location = new Point(20, 255), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbSupplierOperationType.Items.AddRange(new string[] { "فاتورة شراء جديدة 📦", "سداد لمورد 💵" });
            cmbSupplierOperationType.SelectedIndexChanged += CmbSupplierOperationType_SelectedIndexChanged;

            pnlNewPurchase = new Panel() { Location = new Point(20, 290), Size = new Size(280, 760) };
            pnlSupplierPayment = new Panel() { Location = new Point(20, 290), Size = new Size(280, 420) };

            BuildNewPurchasePanel();
            BuildSupplierPaymentPanel();
            pnlSupplierPayment.Visible = false;

            Label lblSuppliersGridTitle = new Label() { Text = "الموردون وأرصدتهم:", Location = new Point(320, 20), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            dgvSuppliers = new DataGridView() { Location = new Point(320, 45), Size = new Size(780, 300), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvSuppliers.CellClick += DgvSuppliers_CellClick;
            StyleDataGridView(dgvSuppliers);

            Label lblStatementTitle = new Label() { Text = "كشف حساب المورد المحدد (دوس على أي صف فوق):", Location = new Point(320, 355), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            dgvSupplierStatement = new DataGridView() { Location = new Point(320, 380), Size = new Size(780, 300), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            StyleDataGridView(dgvSupplierStatement);

            page.AutoScroll = true;
            page.Controls.AddRange(new Control[] { gbSupplier, lblOpType, cmbSupplierOperationType, pnlNewPurchase, pnlSupplierPayment, lblSuppliersGridTitle, dgvSuppliers, lblStatementTitle, dgvSupplierStatement });

            cmbSupplierOperationType.SelectedIndex = 0;
            LoadSuppliersGrid();
            LoadSupplierCombos();
        }

        private void BuildNewPurchasePanel()
        {
            Label lblPurchaseSupplier = new Label() { Text = "المورد:", Location = new Point(0, 0), AutoSize = true };
            cmbPurchaseSupplier = new Guna2ComboBox() { Location = new Point(0, 20), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };

            Label lblPurchasePaymentTypeLbl = new Label() { Text = "طريقة الدفع للمورد:", Location = new Point(0, 55), AutoSize = true };
            cmbPurchasePaymentType = new Guna2ComboBox() { Location = new Point(0, 75), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPurchasePaymentType.Items.AddRange(new string[] { "كاش فوري", "آجل" });
            cmbPurchasePaymentType.SelectedIndex = 1;
            cmbPurchasePaymentType.SelectedIndexChanged += CmbPurchasePaymentType_SelectedIndexChanged;

            Label lblPurchasePayMethodLbl = new Label() { Text = "وسيلة الدفع (لو كاش فوري):", Location = new Point(0, 110), AutoSize = true };
            cmbPurchasePaymentMethod = new Guna2ComboBox() { Location = new Point(0, 130), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
            cmbPurchasePaymentMethod.Items.AddRange(new string[] { "نقدي", "فوري", "أمان", "سهولة", "فودافون كاش", "إنستاباي" });

            Label lblBarcode = new Label() { Text = "باركود المنتج (لو موجود):", Location = new Point(0, 165), AutoSize = true };
            txtPurchaseBarcode = new TextBox() { Location = new Point(0, 185), Width = 280 };
            txtPurchaseBarcode.KeyDown += TxtPurchaseBarcode_KeyDown;

            Label lblProductName = new Label() { Text = "اسم المنتج:", Location = new Point(0, 215), AutoSize = true };
            txtPurchaseProductName = new TextBox() { Location = new Point(0, 235), Width = 280 };

            chkPurchaseSerialized = new CheckBox() { Text = "منتج بسيريال/IMEI (موبايل)", Location = new Point(0, 265), AutoSize = true };
            chkPurchaseSerialized.CheckedChanged += ChkPurchaseSerialized_CheckedChanged;

            Label lblQty = new Label() { Text = "الكمية:", Location = new Point(0, 295), AutoSize = true };
            txtPurchaseQty = new TextBox() { Location = new Point(0, 315), Width = 130 };

            Label lblUnitCost = new Label() { Text = "سعر الشراء للوحدة:", Location = new Point(150, 295), AutoSize = true };
            txtPurchaseUnitCost = new TextBox() { Location = new Point(150, 315), Width = 130 };

            Label lblImeiList = new Label() { Text = "أرقام الـIMEI (رقم في كل سطر، عدد الأسطر = الكمية):", Location = new Point(0, 345), Size = new Size(280, 20), Font = new Font("Segoe UI", 7.5F) };
            txtPurchaseImeiList = new TextBox() { Location = new Point(0, 365), Width = 280, Height = 70, Multiline = true, ScrollBars = ScrollBars.Vertical, Visible = false };
            lblImeiList.Visible = false;

            Label lblSalePrice = new Label() { Text = "سعر البيع المقترح (لو منتج جديد بس):", Location = new Point(0, 445), Size = new Size(280, 20), Font = new Font("Segoe UI", 7.5F) };
            txtPurchaseSalePrice = new TextBox() { Location = new Point(0, 465), Width = 280 };

            Guna2Button btnAddToCart = new Guna2Button() { Text = "إضافة للفاتورة ➕", Location = new Point(0, 500), Width = 280, Height = 32, FillColor = ColorPrimary };
            btnAddToCart.Click += BtnAddPurchaseItem_Click;

            dgvPurchaseCart = new DataGridView() { Location = new Point(0, 540), Size = new Size(280, 140), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            StyleDataGridView(dgvPurchaseCart);

            lblPurchaseCartTotal = new Label() { Text = "إجمالي الفاتورة: 0.00 ج.م", Location = new Point(0, 685), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ColorSuccess };

            Guna2Button btnSavePurchase = new Guna2Button() { Text = "حفظ فاتورة الشراء 💾", Location = new Point(0, 715), Width = 280, Height = 35, FillColor = ColorSuccess };
            btnSavePurchase.Click += BtnSavePurchase_Click;

            pnlNewPurchase.Controls.AddRange(new Control[] { lblPurchaseSupplier, cmbPurchaseSupplier, lblPurchasePaymentTypeLbl, cmbPurchasePaymentType, lblPurchasePayMethodLbl, cmbPurchasePaymentMethod, lblBarcode, txtPurchaseBarcode, lblProductName, txtPurchaseProductName, chkPurchaseSerialized, lblQty, txtPurchaseQty, lblUnitCost, txtPurchaseUnitCost, lblImeiList, txtPurchaseImeiList, lblSalePrice, txtPurchaseSalePrice, btnAddToCart, dgvPurchaseCart, lblPurchaseCartTotal, btnSavePurchase });
        }

        private void ChkPurchaseSerialized_CheckedChanged(object sender, EventArgs e)
        {
            txtPurchaseImeiList.Visible = chkPurchaseSerialized.Checked;
            txtPurchaseQty.ReadOnly = chkPurchaseSerialized.Checked;
            if (chkPurchaseSerialized.Checked) txtPurchaseQty.Text = "0";
        }

        private void CmbPurchasePaymentType_SelectedIndexChanged(object sender, EventArgs e)
        {
            cmbPurchasePaymentMethod.Enabled = cmbPurchasePaymentType.SelectedItem?.ToString() == "كاش فوري";
        }

        private void BuildSupplierPaymentPanel()
        {
            Label lblPaymentSupplier = new Label() { Text = "المورد:", Location = new Point(0, 0), AutoSize = true };
            cmbPaymentSupplier = new Guna2ComboBox() { Location = new Point(0, 20), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };

            Label lblPaymentMethod = new Label() { Text = "وسيلة الدفع:", Location = new Point(0, 55), AutoSize = true };
            cmbSupplierPaymentMethod = new Guna2ComboBox() { Location = new Point(0, 75), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbSupplierPaymentMethod.Items.AddRange(new string[] { "نقدي", "فوري", "أمان", "سهولة", "فودافون كاش", "إنستاباي" });

            Label lblAmount = new Label() { Text = "المبلغ المدفوع:", Location = new Point(0, 110), AutoSize = true };
            txtSupplierPaymentAmount = new TextBox() { Location = new Point(0, 130), Width = 280 };

            Guna2Button btnPaySupplier = new Guna2Button() { Text = "تسجيل السداد ✅", Location = new Point(0, 170), Width = 280, Height = 35, FillColor = ColorSuccess };
            btnPaySupplier.Click += BtnPaySupplier_Click;

            pnlSupplierPayment.Controls.AddRange(new Control[] { lblPaymentSupplier, cmbPaymentSupplier, lblPaymentMethod, cmbSupplierPaymentMethod, lblAmount, txtSupplierPaymentAmount, btnPaySupplier });
        }

        private void CmbSupplierOperationType_SelectedIndexChanged(object sender, EventArgs e)
        {
            pnlNewPurchase.Visible = cmbSupplierOperationType.SelectedIndex == 0;
            pnlSupplierPayment.Visible = cmbSupplierOperationType.SelectedIndex == 1;
        }

        private void LoadSupplierCombos()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("SupplierId", typeof(int)), new DataColumn("SupplierName") });

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("SELECT SupplierId, SupplierName FROM Suppliers ORDER BY SupplierName", conn))
                {
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                dt.Rows.Add(Convert.ToInt32(reader["SupplierId"]), reader["SupplierName"].ToString());
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }

            if (cmbPurchaseSupplier != null)
            {
                cmbPurchaseSupplier.DataSource = dt.Copy();
                cmbPurchaseSupplier.DisplayMember = "SupplierName";
                cmbPurchaseSupplier.ValueMember = "SupplierId";
            }
            if (cmbPaymentSupplier != null)
            {
                cmbPaymentSupplier.DataSource = dt.Copy();
                cmbPaymentSupplier.DisplayMember = "SupplierName";
                cmbPaymentSupplier.ValueMember = "SupplierId";
            }
        }

        private void LoadSuppliersGrid()
        {
            if (dgvSuppliers == null) return;

            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("SupplierId"), new DataColumn("اسم المورد"), new DataColumn("التليفون"), new DataColumn("إجمالي المشتريات"), new DataColumn("إجمالي المسدد"), new DataColumn("المتبقي") });

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                var suppliers = new List<(int id, string name, string phone)>();
                using (SqliteCommand cmd = new SqliteCommand("SELECT SupplierId, SupplierName, Phone FROM Suppliers ORDER BY SupplierName", conn))
                {
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            suppliers.Add((Convert.ToInt32(reader["SupplierId"]), reader["SupplierName"].ToString(), reader["Phone"]?.ToString()));
                    }
                }

                foreach (var sup in suppliers)
                {
                    decimal totalPurchases = 0, totalPaid = 0;
                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(TotalAmount) FROM Purchases WHERE SupplierId = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", sup.id);
                        var res = cmd.ExecuteScalar();
                        totalPurchases = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                    }
                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE SupplierId = @Id AND MovementType = 'صرف'", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", sup.id);
                        var res = cmd.ExecuteScalar();
                        totalPaid = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                    }

                    dt.Rows.Add(sup.id, sup.name, sup.phone, totalPurchases.ToString("N2"), totalPaid.ToString("N2"), (totalPurchases - totalPaid).ToString("N2"));
                }
            }

            dgvSuppliers.DataSource = dt;
            if (dgvSuppliers.Columns["SupplierId"] != null) dgvSuppliers.Columns["SupplierId"].Visible = false;
        }

        private void DgvSuppliers_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvSuppliers.Rows[e.RowIndex];
            selectedSupplierId = Convert.ToInt32(row.Cells["SupplierId"].Value);
            txtSupplierName.Text = row.Cells["اسم المورد"].Value.ToString();
            txtSupplierPhone.Text = row.Cells["التليفون"].Value?.ToString();
            btnSaveSupplierEdit.Enabled = true;

            LoadSupplierStatement(selectedSupplierId);
        }

        private void LoadSupplierStatement(int supplierId)
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("التاريخ"), new DataColumn("النوع"), new DataColumn("التفاصيل"), new DataColumn("المبلغ") });

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT PurchaseId, PurchaseDate, TotalAmount, Notes FROM Purchases WHERE SupplierId = @Id ORDER BY PurchaseDate", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", supplierId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            dt.Rows.Add(reader["PurchaseDate"], "فاتورة شراء", "فاتورة رقم " + reader["PurchaseId"], reader["TotalAmount"]);
                    }
                }
                using (SqliteCommand cmd = new SqliteCommand("SELECT CreatedAt, Amount, PaymentMethod FROM CashMovements WHERE SupplierId = @Id AND MovementType = 'صرف' ORDER BY CreatedAt", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", supplierId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            dt.Rows.Add(reader["CreatedAt"], "سداد", "سداد عبر " + reader["PaymentMethod"], "-" + Convert.ToDecimal(reader["Amount"]).ToString("N2"));
                    }
                }
            }

            dgvSupplierStatement.DataSource = dt;
        }

        private void BtnAddSupplier_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSupplierName.Text))
            {
                MessageBox.Show("من فضلك أدخل اسم المورد.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("INSERT INTO Suppliers (SupplierName, Phone, CreatedAt) VALUES (@N, @P, @C)", conn))
                {
                    cmd.Parameters.AddWithValue("@N", txtSupplierName.Text.Trim());
                    cmd.Parameters.AddWithValue("@P", string.IsNullOrWhiteSpace(txtSupplierPhone.Text) ? (object)DBNull.Value : txtSupplierPhone.Text.Trim());
                    cmd.Parameters.AddWithValue("@C", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم إضافة المورد بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearSupplierInputs();
            LoadSuppliersGrid();
            LoadSupplierCombos();
        }

        private void BtnSaveSupplierEdit_Click(object sender, EventArgs e)
        {
            if (selectedSupplierId == -1 || string.IsNullOrWhiteSpace(txtSupplierName.Text)) return;

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("UPDATE Suppliers SET SupplierName = @N, Phone = @P WHERE SupplierId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@N", txtSupplierName.Text.Trim());
                    cmd.Parameters.AddWithValue("@P", string.IsNullOrWhiteSpace(txtSupplierPhone.Text) ? (object)DBNull.Value : txtSupplierPhone.Text.Trim());
                    cmd.Parameters.AddWithValue("@Id", selectedSupplierId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم تعديل بيانات المورد بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearSupplierInputs();
            LoadSuppliersGrid();
            LoadSupplierCombos();
        }

        private void BtnDeleteSupplier_Click(object sender, EventArgs e)
        {
            if (selectedSupplierId == -1)
            {
                MessageBox.Show("من فضلك اختر مورد من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                int purchaseCount;
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM Purchases WHERE SupplierId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", selectedSupplierId);
                    purchaseCount = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (purchaseCount > 0)
                {
                    MessageBox.Show("لا يمكن حذف هذا المورد لأن له فواتير شراء مسجّلة بالفعل.", "غير مسموح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show("هل أنت متأكد من حذف هذا المورد؟", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                using (SqliteCommand cmd = new SqliteCommand("DELETE FROM Suppliers WHERE SupplierId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", selectedSupplierId);
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم حذف المورد بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearSupplierInputs();
            LoadSuppliersGrid();
            LoadSupplierCombos();
        }

        private void ClearSupplierInputs()
        {
            selectedSupplierId = -1;
            txtSupplierName.Clear();
            txtSupplierPhone.Clear();
            btnSaveSupplierEdit.Enabled = false;
        }

        private void TxtPurchaseBarcode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter || string.IsNullOrWhiteSpace(txtPurchaseBarcode.Text)) return;

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("SELECT ProductName, Price, SalePrice, IsSerialized FROM Products WHERE Barcode = @Barcode", conn))
                {
                    cmd.Parameters.AddWithValue("@Barcode", txtPurchaseBarcode.Text.Trim());
                    conn.Open();
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            txtPurchaseProductName.Text = reader["ProductName"].ToString();
                            txtPurchaseUnitCost.Text = reader["Price"].ToString();
                            txtPurchaseSalePrice.Text = reader["SalePrice"].ToString();
                            chkPurchaseSerialized.Checked = reader["IsSerialized"] != DBNull.Value && Convert.ToInt32(reader["IsSerialized"]) == 1;
                        }
                        else
                        {
                            txtPurchaseProductName.Clear();
                            txtPurchaseSalePrice.Clear();
                            MessageBox.Show("المنتج ده مش موجود في المخزون. اكتب اسمه وسعر بيعه المقترح عشان يتضاف كمنتج جديد.", "منتج جديد", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
        }

        private void BtnAddPurchaseItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPurchaseProductName.Text)
                || !decimal.TryParse(txtPurchaseUnitCost.Text, out decimal unitCost) || unitCost < 0)
            {
                MessageBox.Show("من فضلك أدخل اسم منتج وسعر شراء صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal.TryParse(txtPurchaseSalePrice.Text, out decimal salePrice);
            string barcode = txtPurchaseBarcode.Text.Trim();
            List<string> imeis = null;
            int qty;

            if (chkPurchaseSerialized.Checked)
            {
                imeis = txtPurchaseImeiList.Text
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();

                if (imeis.Count == 0)
                {
                    MessageBox.Show("من فضلك أدخل رقم IMEI واحد على الأقل، رقم في كل سطر.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (imeis.Distinct().Count() != imeis.Count)
                {
                    MessageBox.Show("في أرقام IMEI متكررة في القايمة، من فضلك راجعها.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                qty = imeis.Count;
            }
            else
            {
                if (!int.TryParse(txtPurchaseQty.Text, out qty) || qty <= 0)
                {
                    MessageBox.Show("من فضلك أدخل كمية صحيحة.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            decimal lineTotal = qty * unitCost;
            currentPurchaseItems.Add((barcode, txtPurchaseProductName.Text.Trim(), qty, unitCost, salePrice, lineTotal, chkPurchaseSerialized.Checked, imeis));

            RefreshPurchaseCartGrid();

            txtPurchaseBarcode.Clear();
            txtPurchaseProductName.Clear();
            txtPurchaseQty.Clear();
            txtPurchaseUnitCost.Clear();
            txtPurchaseSalePrice.Clear();
            txtPurchaseImeiList.Clear();
            chkPurchaseSerialized.Checked = false;
            txtPurchaseBarcode.Focus();
        }

        private void RefreshPurchaseCartGrid()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("المنتج"), new DataColumn("الكمية"), new DataColumn("السعر"), new DataColumn("الإجمالي"), new DataColumn("IMEI؟") });

            decimal grandTotal = 0;
            foreach (var item in currentPurchaseItems)
            {
                dt.Rows.Add(item.ProductName, item.Qty, item.UnitCost.ToString("N2"), item.LineTotal.ToString("N2"), item.IsSerialized ? "✅" : "-");
                grandTotal += item.LineTotal;
            }

            dgvPurchaseCart.DataSource = dt;
            lblPurchaseCartTotal.Text = $"إجمالي الفاتورة: {grandTotal:N2} ج.م";
        }

        private void BtnSavePurchase_Click(object sender, EventArgs e)
        {
            if (cmbPurchaseSupplier.SelectedValue == null)
            {
                MessageBox.Show("من فضلك اختر المورد.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (currentPurchaseItems.Count == 0)
            {
                MessageBox.Show("من فضلك ضيف صنف واحد على الأقل للفاتورة.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool payCashNow = cmbPurchasePaymentType.SelectedItem?.ToString() == "كاش فوري";
            string cashMethod = null;
            if (payCashNow)
            {
                if (cmbPurchasePaymentMethod.SelectedItem == null)
                {
                    MessageBox.Show("من فضلك اختر وسيلة الدفع اللي هتدفع بيها كاش.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                cashMethod = cmbPurchasePaymentMethod.SelectedItem.ToString();
            }

            int supplierId = Convert.ToInt32(cmbPurchaseSupplier.SelectedValue);
            decimal totalAmount = currentPurchaseItems.Sum(x => x.LineTotal);

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                // بنتأكد الأول إن مفيش أي رقم IMEI في الفاتورة مسجل بالفعل من قبل، عشان منوقعش في خطأ وقت الحفظ
                foreach (var checkItem in currentPurchaseItems)
                {
                    if (!checkItem.IsSerialized || checkItem.Imeis == null) continue;
                    foreach (string imei in checkItem.Imeis)
                    {
                        using (SqliteCommand cmdCheckImei = new SqliteCommand("SELECT COUNT(*) FROM ProductUnits WHERE IMEI = @IMEI", conn))
                        {
                            cmdCheckImei.Parameters.AddWithValue("@IMEI", imei);
                            if (Convert.ToInt32(cmdCheckImei.ExecuteScalar()) > 0)
                            {
                                MessageBox.Show($"رقم الـIMEI \"{imei}\" مسجل بالفعل في النظام من قبل. راجع القايمة وشيله أو صحّحه.", "رقم مكرر", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                        }
                    }
                }

                if (payCashNow)
                {
                    decimal currentBalance = 0;
                    using (SqliteCommand cmdBal = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn))
                    {
                        cmdBal.Parameters.AddWithValue("@Method", cashMethod);
                        var res = cmdBal.ExecuteScalar();
                        currentBalance = res != null ? Convert.ToDecimal(res) : 0;
                    }
                    if (totalAmount > currentBalance)
                    {
                        MessageBox.Show($"الرصيد المتاح في \"{cashMethod}\" هو {currentBalance:N2} فقط، أقل من إجمالي الفاتورة.", "رصيد غير كافٍ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        int purchaseId;
                        using (SqliteCommand cmd = new SqliteCommand("INSERT INTO Purchases (SupplierId, PurchaseDate, TotalAmount, Notes) VALUES (@S, @D, @T, @N)", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@S", supplierId);
                            cmd.Parameters.AddWithValue("@D", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmd.Parameters.AddWithValue("@T", totalAmount);
                            cmd.Parameters.AddWithValue("@N", DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                        using (SqliteCommand cmdId = new SqliteCommand("SELECT last_insert_rowid();", conn, transaction))
                        {
                            purchaseId = Convert.ToInt32(cmdId.ExecuteScalar());
                        }

                        foreach (var item in currentPurchaseItems)
                        {
                            using (SqliteCommand cmd = new SqliteCommand(
                                "INSERT INTO PurchaseItems (PurchaseId, Barcode, ProductName, Quantity, UnitCost, LineTotal) VALUES (@P, @B, @N, @Q, @U, @L)", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@P", purchaseId);
                                cmd.Parameters.AddWithValue("@B", string.IsNullOrEmpty(item.Barcode) ? (object)DBNull.Value : item.Barcode);
                                cmd.Parameters.AddWithValue("@N", item.ProductName);
                                cmd.Parameters.AddWithValue("@Q", item.Qty);
                                cmd.Parameters.AddWithValue("@U", item.UnitCost);
                                cmd.Parameters.AddWithValue("@L", item.LineTotal);
                                cmd.ExecuteNonQuery();
                            }

                            bool productExists = false;
                            if (!string.IsNullOrEmpty(item.Barcode))
                            {
                                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM Products WHERE Barcode = @B", conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@B", item.Barcode);
                                    productExists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                                }
                            }

                            string finalBarcode = item.Barcode;

                            if (productExists)
                            {
                                using (SqliteCommand cmd = new SqliteCommand(
                                    "UPDATE Products SET Quantity = Quantity + @Q, Price = @U, IsSerialized = CASE WHEN @IsSerialized = 1 THEN 1 ELSE IsSerialized END WHERE Barcode = @B", conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@Q", item.Qty);
                                    cmd.Parameters.AddWithValue("@U", item.UnitCost);
                                    cmd.Parameters.AddWithValue("@IsSerialized", item.IsSerialized ? 1 : 0);
                                    cmd.Parameters.AddWithValue("@B", item.Barcode);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            else
                            {
                                finalBarcode = string.IsNullOrEmpty(item.Barcode) ? ("NEW-" + DateTime.Now.Ticks) : item.Barcode;
                                using (SqliteCommand cmd = new SqliteCommand(
                                    "INSERT INTO Products (Barcode, ProductName, Price, SalePrice, Quantity, IsSerialized) VALUES (@B, @N, @U, @S, @Q, @IsSerialized)", conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@B", finalBarcode);
                                    cmd.Parameters.AddWithValue("@N", item.ProductName);
                                    cmd.Parameters.AddWithValue("@U", item.UnitCost);
                                    cmd.Parameters.AddWithValue("@S", item.SalePrice);
                                    cmd.Parameters.AddWithValue("@Q", item.Qty);
                                    cmd.Parameters.AddWithValue("@IsSerialized", item.IsSerialized ? 1 : 0);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            if (item.IsSerialized && item.Imeis != null)
                            {
                                foreach (string imei in item.Imeis)
                                {
                                    using (SqliteCommand cmd = new SqliteCommand(
                                        "INSERT INTO ProductUnits (Barcode, IMEI, Status, PurchaseId, CreatedAt) VALUES (@B, @IMEI, 'InStock', @P, @C)", conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@B", finalBarcode);
                                        cmd.Parameters.AddWithValue("@IMEI", imei);
                                        cmd.Parameters.AddWithValue("@P", purchaseId);
                                        cmd.Parameters.AddWithValue("@C", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }
                        }

                        if (payCashNow)
                        {
                            using (SqliteCommand cmd = new SqliteCommand(
                                "INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode, SupplierId) VALUES (@Date, 'صرف', @Method, @Amount, @Ref, @Desc, @CreatedAt, 2100, @SupplierId)", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@Method", cashMethod);
                                cmd.Parameters.AddWithValue("@Amount", totalAmount);
                                cmd.Parameters.AddWithValue("@Ref", "فاتورة شراء رقم " + purchaseId);
                                cmd.Parameters.AddWithValue("@Desc", "سداد كاش فوري لفاتورة شراء رقم " + purchaseId);
                                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.Parameters.AddWithValue("@SupplierId", supplierId);
                                cmd.ExecuteNonQuery();
                            }

                            using (SqliteCommand cmd = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = CurrentBalance - @Amount WHERE PaymentMethod = @Method", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Amount", totalAmount);
                                cmd.Parameters.AddWithValue("@Method", cashMethod);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show("حصل خطأ أثناء حفظ الفاتورة ولم يتم حفظ أي حاجة: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }

            MessageBox.Show($"تم حفظ فاتورة الشراء بنجاح بإجمالي {totalAmount:N2} ج.م، وتحديث المخزون تلقائي" + (payCashNow ? "، وتم خصم المبلغ فورًا من الرصيد." : "، وسجّلت كدين على المورد."), "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            currentPurchaseItems.Clear();
            RefreshPurchaseCartGrid();
            LoadProductsData();
            LoadSuppliersGrid();
            RefreshClosureSummary();
        }

        private void BtnPaySupplier_Click(object sender, EventArgs e)
        {
            if (cmbPaymentSupplier.SelectedValue == null || cmbSupplierPaymentMethod.SelectedItem == null
                || !decimal.TryParse(txtSupplierPaymentAmount.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("من فضلك اختر المورد ووسيلة الدفع وأدخل مبلغ صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (IsTodayClosed())
            {
                MessageBox.Show("تم إقفال اليوم بالفعل، لا يمكن تسجيل سداد جديد.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int supplierId = Convert.ToInt32(cmbPaymentSupplier.SelectedValue);
            string method = cmbSupplierPaymentMethod.SelectedItem.ToString();
            string supplierName = cmbPaymentSupplier.Text;

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                decimal currentBalance = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn))
                {
                    cmd.Parameters.AddWithValue("@Method", method);
                    var res = cmd.ExecuteScalar();
                    currentBalance = res != null ? Convert.ToDecimal(res) : 0;
                }

                if (amount > currentBalance)
                {
                    MessageBox.Show($"الرصيد المتاح في \"{method}\" هو {currentBalance:N2} فقط.", "رصيد غير كافٍ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                using (SqliteCommand cmd = new SqliteCommand(
                    "INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode, SupplierId) VALUES (@Date, 'صرف', @Method, @Amount, @Ref, @Desc, @CreatedAt, 2100, @SupplierId)", conn))
                {
                    cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@Method", method);
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    cmd.Parameters.AddWithValue("@Ref", "");
                    cmd.Parameters.AddWithValue("@Desc", "سداد لمورد: " + supplierName);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@SupplierId", supplierId);
                    cmd.ExecuteNonQuery();
                }

                using (SqliteCommand cmd = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = CurrentBalance - @Amount WHERE PaymentMethod = @Method", conn))
                {
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    cmd.Parameters.AddWithValue("@Method", method);
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم تسجيل السداد بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtSupplierPaymentAmount.Clear();
            LoadSuppliersGrid();
            RefreshClosureSummary();
        }

        private TextBox txtCustomerName, txtCustomerPhone;
        private DataGridView dgvCustomers, dgvCustomerStatement;
        private Guna2Button btnSaveCustomerEdit;
        private int selectedCustomerId = -1;

        private Guna2ComboBox cmbCollectCustomer, cmbCollectPaymentMethod;
        private TextBox txtCollectAmount;

        private void CreateCustomersDesign(TabPage page)
        {
            GroupBox gbCustomer = new GroupBox() { Text = "إضافة / تعديل عميل", Location = new Point(20, 20), Size = new Size(280, 200) };
            Label lblCustomerName = new Label() { Text = "اسم العميل:", Location = new Point(10, 25), AutoSize = true };
            txtCustomerName = new TextBox() { Location = new Point(10, 45), Width = 250 };
            Label lblCustomerPhone = new Label() { Text = "رقم التليفون:", Location = new Point(10, 80), AutoSize = true };
            txtCustomerPhone = new TextBox() { Location = new Point(10, 100), Width = 250 };

            Guna2Button btnAddCustomer = new Guna2Button() { Text = "إضافة عميل جديد ✅", Location = new Point(10, 135), Width = 250, Height = 32, FillColor = ColorSuccess };
            btnAddCustomer.Click += BtnAddCustomer_Click;

            btnSaveCustomerEdit = new Guna2Button() { Text = "حفظ التعديل 💾", Location = new Point(10, 170), Width = 120, Height = 28, FillColor = ColorWarning, Enabled = false };
            btnSaveCustomerEdit.Click += BtnSaveCustomerEdit_Click;

            Guna2Button btnDeleteCustomer = new Guna2Button() { Text = "حذف العميل ❌", Location = new Point(140, 170), Width = 120, Height = 28, FillColor = ColorDanger };
            btnDeleteCustomer.Click += BtnDeleteCustomer_Click;

            gbCustomer.Controls.AddRange(new Control[] { lblCustomerName, txtCustomerName, lblCustomerPhone, txtCustomerPhone, btnAddCustomer, btnSaveCustomerEdit, btnDeleteCustomer });

            GroupBox gbCollect = new GroupBox() { Text = "تحصيل من عميل (سداد دين)", Location = new Point(20, 235), Size = new Size(280, 260) };
            Label lblCollectCustomer = new Label() { Text = "العميل:", Location = new Point(10, 25), AutoSize = true };
            cmbCollectCustomer = new Guna2ComboBox() { Location = new Point(10, 45), Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };

            Label lblCollectMethod = new Label() { Text = "وسيلة التحصيل:", Location = new Point(10, 80), AutoSize = true };
            cmbCollectPaymentMethod = new Guna2ComboBox() { Location = new Point(10, 100), Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbCollectPaymentMethod.Items.AddRange(new string[] { "نقدي", "فوري", "أمان", "سهولة", "فودافون كاش", "إنستاباي" });

            Label lblCollectAmount = new Label() { Text = "المبلغ المحصّل:", Location = new Point(10, 135), AutoSize = true };
            txtCollectAmount = new TextBox() { Location = new Point(10, 155), Width = 250 };

            Guna2Button btnCollect = new Guna2Button() { Text = "تسجيل التحصيل ✅", Location = new Point(10, 195), Width = 250, Height = 35, FillColor = ColorSuccess };
            btnCollect.Click += BtnCollectFromCustomer_Click;

            gbCollect.Controls.AddRange(new Control[] { lblCollectCustomer, cmbCollectCustomer, lblCollectMethod, cmbCollectPaymentMethod, lblCollectAmount, txtCollectAmount, btnCollect });

            Label lblCustomersGridTitle = new Label() { Text = "العملاء وأرصدتهم:", Location = new Point(320, 20), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            dgvCustomers = new DataGridView() { Location = new Point(320, 45), Size = new Size(780, 300), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvCustomers.CellClick += DgvCustomers_CellClick;
            StyleDataGridView(dgvCustomers);

            Label lblCustomerStatementTitle = new Label() { Text = "كشف حساب العميل المحدد (دوس على أي صف فوق):", Location = new Point(320, 355), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            dgvCustomerStatement = new DataGridView() { Location = new Point(320, 380), Size = new Size(780, 300), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            StyleDataGridView(dgvCustomerStatement);

            page.AutoScroll = true;
            page.Controls.AddRange(new Control[] { gbCustomer, gbCollect, lblCustomersGridTitle, dgvCustomers, lblCustomerStatementTitle, dgvCustomerStatement });

            LoadCustomersGrid();
            LoadCollectCustomerCombo();
        }

        private void LoadCollectCustomerCombo()
        {
            if (cmbCollectCustomer == null) return;
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("CustomerId", typeof(int)), new DataColumn("CustomerName") });

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("SELECT CustomerId, CustomerName FROM Customers ORDER BY CustomerName", conn))
                {
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                dt.Rows.Add(Convert.ToInt32(reader["CustomerId"]), reader["CustomerName"].ToString());
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
            cmbCollectCustomer.DataSource = dt;
            cmbCollectCustomer.DisplayMember = "CustomerName";
            cmbCollectCustomer.ValueMember = "CustomerId";
        }

        private void LoadCustomersGrid()
        {
            if (dgvCustomers == null) return;

            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("CustomerId"), new DataColumn("اسم العميل"), new DataColumn("التليفون"), new DataColumn("إجمالي المبيعات بالآجل"), new DataColumn("إجمالي المحصّل"), new DataColumn("المتبقي عليه") });

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                var customers = new List<(int id, string name, string phone)>();
                using (SqliteCommand cmd = new SqliteCommand("SELECT CustomerId, CustomerName, Phone FROM Customers ORDER BY CustomerName", conn))
                {
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            customers.Add((Convert.ToInt32(reader["CustomerId"]), reader["CustomerName"].ToString(), reader["Phone"]?.ToString()));
                    }
                }

                foreach (var cust in customers)
                {
                    decimal totalCredit = 0, totalCollected = 0;
                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Total) FROM Sales WHERE CustomerId = @Id AND PaymentType = 'Credit'", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", cust.id);
                        var res = cmd.ExecuteScalar();
                        totalCredit = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                    }
                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE CustomerId = @Id AND MovementType = 'قبض'", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", cust.id);
                        var res = cmd.ExecuteScalar();
                        totalCollected = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                    }

                    dt.Rows.Add(cust.id, cust.name, cust.phone, totalCredit.ToString("N2"), totalCollected.ToString("N2"), (totalCredit - totalCollected).ToString("N2"));
                }
            }

            dgvCustomers.DataSource = dt;
            if (dgvCustomers.Columns["CustomerId"] != null) dgvCustomers.Columns["CustomerId"].Visible = false;
        }

        private void DgvCustomers_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvCustomers.Rows[e.RowIndex];
            selectedCustomerId = Convert.ToInt32(row.Cells["CustomerId"].Value);
            txtCustomerName.Text = row.Cells["اسم العميل"].Value.ToString();
            txtCustomerPhone.Text = row.Cells["التليفون"].Value?.ToString();
            btnSaveCustomerEdit.Enabled = true;

            LoadCustomerStatement(selectedCustomerId);
        }

        private void LoadCustomerStatement(int customerId)
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("التاريخ"), new DataColumn("النوع"), new DataColumn("التفاصيل"), new DataColumn("المبلغ") });

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT SaleDate, ProductName, Total FROM Sales WHERE CustomerId = @Id AND PaymentType = 'Credit' ORDER BY SaleDate", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", customerId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            dt.Rows.Add(reader["SaleDate"], "بيع آجل", reader["ProductName"], reader["Total"]);
                    }
                }
                using (SqliteCommand cmd = new SqliteCommand("SELECT CreatedAt, Amount, PaymentMethod FROM CashMovements WHERE CustomerId = @Id AND MovementType = 'قبض' ORDER BY CreatedAt", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", customerId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            dt.Rows.Add(reader["CreatedAt"], "تحصيل", "تحصيل عبر " + reader["PaymentMethod"], "-" + Convert.ToDecimal(reader["Amount"]).ToString("N2"));
                    }
                }
            }

            dgvCustomerStatement.DataSource = dt;
        }

        private void BtnAddCustomer_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCustomerName.Text))
            {
                MessageBox.Show("من فضلك أدخل اسم العميل.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("INSERT INTO Customers (CustomerName, Phone, CreatedAt) VALUES (@N, @P, @C)", conn))
                {
                    cmd.Parameters.AddWithValue("@N", txtCustomerName.Text.Trim());
                    cmd.Parameters.AddWithValue("@P", string.IsNullOrWhiteSpace(txtCustomerPhone.Text) ? (object)DBNull.Value : txtCustomerPhone.Text.Trim());
                    cmd.Parameters.AddWithValue("@C", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم إضافة العميل بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearCustomerInputs();
            LoadCustomersGrid();
            LoadCollectCustomerCombo();
            LoadCustomersIntoCombo();
        }

        private void BtnSaveCustomerEdit_Click(object sender, EventArgs e)
        {
            if (selectedCustomerId == -1 || string.IsNullOrWhiteSpace(txtCustomerName.Text)) return;

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("UPDATE Customers SET CustomerName = @N, Phone = @P WHERE CustomerId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@N", txtCustomerName.Text.Trim());
                    cmd.Parameters.AddWithValue("@P", string.IsNullOrWhiteSpace(txtCustomerPhone.Text) ? (object)DBNull.Value : txtCustomerPhone.Text.Trim());
                    cmd.Parameters.AddWithValue("@Id", selectedCustomerId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم تعديل بيانات العميل بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearCustomerInputs();
            LoadCustomersGrid();
            LoadCollectCustomerCombo();
            LoadCustomersIntoCombo();
        }

        private void BtnDeleteCustomer_Click(object sender, EventArgs e)
        {
            if (selectedCustomerId == -1)
            {
                MessageBox.Show("من فضلك اختر عميل من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                int salesCount;
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM Sales WHERE CustomerId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", selectedCustomerId);
                    salesCount = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (salesCount > 0)
                {
                    MessageBox.Show("لا يمكن حذف هذا العميل لأن له مبيعات مسجّلة بالفعل.", "غير مسموح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show("هل أنت متأكد من حذف هذا العميل؟", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                using (SqliteCommand cmd = new SqliteCommand("DELETE FROM Customers WHERE CustomerId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", selectedCustomerId);
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم حذف العميل بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearCustomerInputs();
            LoadCustomersGrid();
            LoadCollectCustomerCombo();
            LoadCustomersIntoCombo();
        }

        private void ClearCustomerInputs()
        {
            selectedCustomerId = -1;
            txtCustomerName.Clear();
            txtCustomerPhone.Clear();
            btnSaveCustomerEdit.Enabled = false;
        }

        private void BtnCollectFromCustomer_Click(object sender, EventArgs e)
        {
            if (cmbCollectCustomer.SelectedValue == null || cmbCollectPaymentMethod.SelectedItem == null
                || !decimal.TryParse(txtCollectAmount.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("من فضلك اختر العميل ووسيلة التحصيل وأدخل مبلغ صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (IsTodayClosed())
            {
                MessageBox.Show("تم إقفال اليوم بالفعل، لا يمكن تسجيل تحصيل جديد.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int customerId = Convert.ToInt32(cmbCollectCustomer.SelectedValue);
            string method = cmbCollectPaymentMethod.SelectedItem.ToString();
            string customerName = cmbCollectCustomer.Text;

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                using (SqliteCommand cmd = new SqliteCommand(
                    "INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode, CustomerId) VALUES (@Date, 'قبض', @Method, @Amount, @Ref, @Desc, @CreatedAt, 1300, @CustomerId)", conn))
                {
                    cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@Method", method);
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    cmd.Parameters.AddWithValue("@Ref", "");
                    cmd.Parameters.AddWithValue("@Desc", "تحصيل من عميل: " + customerName);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@CustomerId", customerId);
                    cmd.ExecuteNonQuery();
                }

                using (SqliteCommand cmd = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = CurrentBalance + @Amount WHERE PaymentMethod = @Method", conn))
                {
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    cmd.Parameters.AddWithValue("@Method", method);
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم تسجيل التحصيل بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtCollectAmount.Clear();
            LoadCustomersGrid();
            RefreshClosureSummary();
        }

        private TextBox txtImeiSearch;
        private Guna2ComboBox cmbImeiStatusFilter;
        private DataGridView dgvImeiUnits;
        private TextBox txtQaBarcode, txtQaProductName, txtQaImei, txtQaCostPrice, txtQaSalePrice;
        private GroupBox gbQuickAdd;

        private void CreateImeiDesign(TabPage page)
        {
            GroupBox gbSearch = new GroupBox() { Text = "بحث وفلترة", Location = new Point(20, 20), Size = new Size(260, 180) };

            Label lblSearch = new Label() { Text = "بحث برقم الـIMEI أو اسم المنتج:", Location = new Point(10, 25), AutoSize = true };
            txtImeiSearch = new TextBox() { Location = new Point(10, 45), Width = 230 };
            txtImeiSearch.TextChanged += (s, e) => LoadImeiUnitsGrid();

            Label lblStatusFilter = new Label() { Text = "الحالة:", Location = new Point(10, 80), AutoSize = true };
            cmbImeiStatusFilter = new Guna2ComboBox() { Location = new Point(10, 100), Width = 230, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbImeiStatusFilter.Items.AddRange(new string[] { "الكل", "متاح في المخزون", "مباع" });
            cmbImeiStatusFilter.SelectedIndex = 0;
            cmbImeiStatusFilter.SelectedIndexChanged += (s, e) => LoadImeiUnitsGrid();

            Guna2Button btnRefreshImei = new Guna2Button() { Text = "تحديث 🔄", Location = new Point(10, 140), Width = 230, Height = 32, FillColor = ColorPrimary };
            btnRefreshImei.Click += (s, e) => LoadImeiUnitsGrid();

            gbSearch.Controls.AddRange(new Control[] { lblSearch, txtImeiSearch, lblStatusFilter, cmbImeiStatusFilter, btnRefreshImei });

            gbQuickAdd = new GroupBox() { Text = "إضافة جهاز يدوي (من غير فاتورة مورد)", Location = new Point(20, 210), Size = new Size(260, 400) };

            Label lblQaBarcode = new Label() { Text = "الباركود (سيبها فاضية لو مفيش):", Location = new Point(10, 25), AutoSize = true };
            txtQaBarcode = new TextBox() { Location = new Point(10, 45), Width = 230 };

            Label lblQaName = new Label() { Text = "اسم المنتج:", Location = new Point(10, 80), AutoSize = true };
            txtQaProductName = new TextBox() { Location = new Point(10, 100), Width = 230 };

            Label lblQaImei = new Label() { Text = "رقم الـIMEI:", Location = new Point(10, 135), AutoSize = true };
            txtQaImei = new TextBox() { Location = new Point(10, 155), Width = 230 };

            Label lblQaCost = new Label() { Text = "سعر الشراء (التكلفة):", Location = new Point(10, 190), AutoSize = true };
            txtQaCostPrice = new TextBox() { Location = new Point(10, 210), Width = 230 };

            Label lblQaSale = new Label() { Text = "سعر البيع للجمهور:", Location = new Point(10, 245), AutoSize = true };
            txtQaSalePrice = new TextBox() { Location = new Point(10, 265), Width = 230 };

            Label lblQaNote = new Label()
            {
                Text = "الجهاز ده هيتضاف مباشرة للمخزون بكميته وسيريال، من غير ما يتسجل على أي مورد أو فاتورة شراء.",
                Location = new Point(10, 300),
                Size = new Size(230, 45),
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = Color.Gray
            };

            Guna2Button btnQuickAddDevice = new Guna2Button() { Text = "إضافة الجهاز ✅", Location = new Point(10, 350), Width = 230, Height = 35, FillColor = ColorSuccess };
            btnQuickAddDevice.Click += BtnQuickAddDevice_Click;

            gbQuickAdd.Controls.AddRange(new Control[] { lblQaBarcode, txtQaBarcode, lblQaName, txtQaProductName, lblQaImei, txtQaImei, lblQaCost, txtQaCostPrice, lblQaSale, txtQaSalePrice, lblQaNote, btnQuickAddDevice });

            Label lblGridTitle = new Label() { Text = "كل الأجهزة المسجّلة بأرقام الـIMEI:", Location = new Point(310, 20), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

            dgvImeiUnits = new DataGridView() { Location = new Point(310, 45), Size = new Size(780, 620), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            StyleDataGridView(dgvImeiUnits);

            page.AutoScroll = true;
            page.Controls.AddRange(new Control[] { gbSearch, gbQuickAdd, lblGridTitle, dgvImeiUnits });

            LoadImeiUnitsGrid();
        }

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

            using (SqliteConnection conn = new SqliteConnection(connectionString))
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
            LoadProductsData();
            LoadImeiUnitsGrid();
        }

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

            using (SqliteConnection conn = new SqliteConnection(connectionString))
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

        private TextBox txtMaintCustomerName, txtMaintCustomerPhone, txtMaintDeviceInfo, txtMaintIssueDescription, txtMaintEstimatedCost;
        private Guna2ComboBox cmbMaintStatusUpdate, cmbMaintStatusFilter, cmbMaintPaymentMethod;
        private TextBox txtMaintActualCost;
        private DataGridView dgvMaintenanceTickets;
        private int selectedTicketId = -1;

        private static readonly string[] MaintenanceStatuses = { "مستلم", "جاري الفحص", "جاهز للتسليم", "تم التسليم", "ملغي" };
        private Guna2Button btnSaveStatus, btnDeliverDevice;

        private void CreateMaintenanceDesign(TabPage page)
        {
            GroupBox gbReceive = new GroupBox() { Text = "استلام جهاز جديد للصيانة", Location = new Point(20, 20), Size = new Size(280, 375) };

            Label lblMCName = new Label() { Text = "اسم العميل:", Location = new Point(10, 25), AutoSize = true };
            txtMaintCustomerName = new TextBox() { Location = new Point(10, 45), Width = 250 };

            Label lblMCPhone = new Label() { Text = "رقم التليفون:", Location = new Point(10, 80), AutoSize = true };
            txtMaintCustomerPhone = new TextBox() { Location = new Point(10, 100), Width = 250 };

            Label lblMDevice = new Label() { Text = "الجهاز (الموديل):", Location = new Point(10, 135), AutoSize = true };
            txtMaintDeviceInfo = new TextBox() { Location = new Point(10, 155), Width = 250 };

            Label lblMIssue = new Label() { Text = "العطل / الشكوى:", Location = new Point(10, 190), AutoSize = true };
            txtMaintIssueDescription = new TextBox() { Location = new Point(10, 210), Width = 250, Height = 50, Multiline = true };

            Label lblMEst = new Label() { Text = "التكلفة التقديرية:", Location = new Point(10, 270), AutoSize = true };
            txtMaintEstimatedCost = new TextBox() { Location = new Point(10, 290), Width = 250 };

            Guna2Button btnReceiveDevice = new Guna2Button() { Text = "استلام الجهاز ✅", Location = new Point(10, 325), Width = 250, Height = 35, FillColor = ColorSuccess };
            btnReceiveDevice.Click += BtnReceiveDevice_Click;

            gbReceive.Controls.AddRange(new Control[] { lblMCName, txtMaintCustomerName, lblMCPhone, txtMaintCustomerPhone, lblMDevice, txtMaintDeviceInfo, lblMIssue, txtMaintIssueDescription, lblMEst, txtMaintEstimatedCost, btnReceiveDevice });

            GroupBox gbUpdate = new GroupBox() { Text = "تحديث حالة التذكرة المحددة", Location = new Point(20, 405), Size = new Size(280, 110) };
            Label lblStatusUpdate = new Label() { Text = "الحالة الجديدة:", Location = new Point(10, 25), AutoSize = true };
            cmbMaintStatusUpdate = new Guna2ComboBox() { Location = new Point(10, 45), Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMaintStatusUpdate.Items.AddRange(MaintenanceStatuses);

            btnSaveStatus = new Guna2Button() { Text = "حفظ الحالة 💾", Location = new Point(10, 75), Width = 250, Height = 30, FillColor = ColorPrimary };
            btnSaveStatus.Click += BtnSaveMaintenanceStatus_Click;
            gbUpdate.Controls.AddRange(new Control[] { lblStatusUpdate, cmbMaintStatusUpdate, btnSaveStatus });

            GroupBox gbDeliver = new GroupBox() { Text = "تسليم الجهاز وتحصيل الأجرة", Location = new Point(20, 525), Size = new Size(280, 200) };
            Label lblActualCost = new Label() { Text = "الأجرة الفعلية المطلوبة:", Location = new Point(10, 25), AutoSize = true };
            txtMaintActualCost = new TextBox() { Location = new Point(10, 45), Width = 250 };

            Label lblMaintMethod = new Label() { Text = "وسيلة التحصيل:", Location = new Point(10, 80), AutoSize = true };
            cmbMaintPaymentMethod = new Guna2ComboBox() { Location = new Point(10, 100), Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMaintPaymentMethod.Items.AddRange(new string[] { "نقدي", "فوري", "أمان", "سهولة", "فودافون كاش", "إنستاباي" });

            btnDeliverDevice = new Guna2Button() { Text = "تسليم وتحصيل الأجرة ✅", Location = new Point(10, 140), Width = 250, Height = 35, FillColor = ColorSuccess };
            btnDeliverDevice.Click += BtnDeliverMaintenanceDevice_Click;
            gbDeliver.Controls.AddRange(new Control[] { lblActualCost, txtMaintActualCost, lblMaintMethod, cmbMaintPaymentMethod, btnDeliverDevice });

            Label lblFilterTitle = new Label() { Text = "فلترة حسب الحالة:", Location = new Point(310, 20), AutoSize = true };
            cmbMaintStatusFilter = new Guna2ComboBox() { Location = new Point(430, 17), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMaintStatusFilter.Items.Add("الكل");
            cmbMaintStatusFilter.Items.AddRange(MaintenanceStatuses);
            cmbMaintStatusFilter.SelectedIndex = 0;
            cmbMaintStatusFilter.SelectedIndexChanged += (s, e) => LoadMaintenanceGrid();

            dgvMaintenanceTickets = new DataGridView() { Location = new Point(310, 55), Size = new Size(780, 610), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvMaintenanceTickets.CellClick += DgvMaintenanceTickets_CellClick;
            StyleDataGridView(dgvMaintenanceTickets);

            page.AutoScroll = true;
            page.Controls.AddRange(new Control[] { gbReceive, gbUpdate, gbDeliver, lblFilterTitle, cmbMaintStatusFilter, dgvMaintenanceTickets });

            LoadMaintenanceGrid();
        }

        // ======================= النسخ الاحتياطي والاسترجاع 💾 =======================

        private void EnsureBackupFolderExists()
        {
            if (!System.IO.Directory.Exists(BackupFolderPath))
                System.IO.Directory.CreateDirectory(BackupFolderPath);
        }

        // بتتنفذ مرة واحدة أول ما البرنامج يفتح؛ لو مفيش نسخة اتعملت النهاردة، بتعمل واحدة تلقائيًا
        private void RunAutomaticDailyBackup()
        {
            try
            {
                EnsureBackupFolderExists();
                string dbFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TemoStoreDB.db");
                if (!System.IO.File.Exists(dbFilePath)) return; // أول تشغيل للبرنامج، لسه مفيش قاعدة بيانات

                string todayPrefix = $"TemoStoreDB_Backup_{DateTime.Now:yyyy-MM-dd}";
                bool hasTodayBackup = System.IO.Directory.GetFiles(BackupFolderPath, "*.db")
                    .Any(f => System.IO.Path.GetFileName(f).StartsWith(todayPrefix));

                if (!hasTodayBackup)
                    CreateBackupFile();

                PruneOldBackups();
            }
            catch
            {
                // النسخ الاحتياطي التلقائي ميعطلش تشغيل البرنامج لو فشل لأي سبب
            }
        }

        // بتتنفذ لما تقفل البرنامج، عشان آخر حاجة اتباعت النهاردة متضيعش لحد بكرة الصبح
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                CreateBackupFile();
                PruneOldBackups();
            }
            catch
            {
                // مايمنعش قفل البرنامج حتى لو فشلت النسخة الاحتياطية لأي سبب
            }
        }

        // ======================= اختصارات لوحة المفاتيح ⌨️ =======================

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            // F1: عرض قائمة الاختصارات المتاحة
            if (e.KeyCode == Keys.F1)
            {
                ShowShortcutsHelp();
                e.Handled = true;
                return;
            }

            // F2: الانتقال السريع لخانة مسح الباركود في شاشة الكاشير (العمليات اليومية)
            if (e.KeyCode == Keys.F2)
            {
                if (mainTabControl != null)
                {
                    foreach (TabPage tp in mainTabControl.TabPages)
                    {
                        if (tp.Text.Contains("العمليات اليومية")) { mainTabControl.SelectedTab = tp; break; }
                    }
                }
                txtSaleBarcode?.Focus();
                e.Handled = true;
                return;
            }

            // Esc: تفريغ خانات البيع الحالية (إلغاء الإدخال الحالي) لو المستخدم في شاشة الكاشير
            if (e.KeyCode == Keys.Escape)
            {
                if (txtSaleBarcode != null && txtSaleBarcode.Visible)
                {
                    ClearPOSInputs();
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl+P: طباعة آخر فاتورة من أي مكان في البرنامج
            if (e.Control && e.KeyCode == Keys.P)
            {
                BtnPrintInvoice_Click(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+B: عمل نسخة احتياطية فورية (متاح للأدمن بس، زي تاب النسخ الاحتياطي نفسه)
            if (e.Control && e.Shift && e.KeyCode == Keys.B)
            {
                if (AuthManager.IsAdmin)
                    BtnCreateBackupNow_Click(this, EventArgs.Empty);
                else
                    MessageBox.Show("النسخ الاحتياطي متاح للأدمن بس.", "غير مسموح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Handled = true;
                return;
            }

            // Ctrl+F: فتح البحث الشامل
            if (e.Control && e.KeyCode == Keys.F)
            {
                ShowUniversalSearchDialog();
                e.Handled = true;
                return;
            }

            // Ctrl+1 .. Ctrl+9: الانتقال السريع لأول 9 تابات حسب ترتيبها الظاهر
            if (e.Control && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9 && mainTabControl != null)
            {
                int index = (int)e.KeyCode - (int)Keys.D1;
                if (index < mainTabControl.TabPages.Count)
                {
                    mainTabControl.SelectedIndex = index;
                    e.Handled = true;
                }
            }
        }

        private void ShowShortcutsHelp()
        {
            string message =
                "F1  → عرض قائمة الاختصارات دي\n" +
                "F2  → الانتقال السريع لخانة مسح الباركود (شاشة الكاشير)\n" +
                "Esc → تفريغ خانات البيع الحالية في شاشة الكاشير\n" +
                "Ctrl + F → فتح البحث الشامل (منتجات، عملاء، موردين، صيانة، مبيعات)\n" +
                "Ctrl + P → طباعة آخر فاتورة\n" +
                "Ctrl + Shift + B → عمل نسخة احتياطية فورية (أدمن بس)\n" +
                "Ctrl + 1 حتى Ctrl + 9 → الانتقال السريع بين أول 9 تابات\n\n" +
                "ملحوظة: لو تاب معين مش ظاهر عندك، يبقى مش متاح لدورك الوظيفي حاليًا.";

            MessageBox.Show(message, "اختصارات لوحة المفاتيح ⌨️", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ======================= نهاية قسم اختصارات لوحة المفاتيح =======================

        // ======================= البحث الشامل 🔍 =======================

        private enum SearchResultKind { Product, Customer, Supplier, Maintenance, Sale }

        private class UniversalSearchResult
        {
            public SearchResultKind Kind;
            public string Type;
            public string Title;
            public string Subtitle;
            public string TabKeyword;
            public string MatchColumn;
            public object MatchValue;
        }

        // بيدور في كل الجداول المهمة مرة واحدة: المنتجات، العملاء، الموردين، الصيانة، والمبيعات
        private List<UniversalSearchResult> RunUniversalSearch(string term)
        {
            var results = new List<UniversalSearchResult>();
            if (string.IsNullOrWhiteSpace(term)) return results;
            string like = $"%{term.Trim()}%";

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                using (SqliteCommand cmd = new SqliteCommand("SELECT Barcode, ProductName, Quantity FROM Products WHERE ProductName LIKE @q OR Barcode LIKE @q LIMIT 25", conn))
                {
                    cmd.Parameters.AddWithValue("@q", like);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new UniversalSearchResult
                            {
                                Kind = SearchResultKind.Product,
                                Type = "منتج 📦",
                                Title = reader["ProductName"].ToString(),
                                Subtitle = $"باركود: {reader["Barcode"]} | الكمية بالمخزن: {reader["Quantity"]}",
                                TabKeyword = "إدارة المخزن",
                                MatchColumn = "الباركود",
                                MatchValue = reader["Barcode"].ToString()
                            });
                        }
                    }
                }

                using (SqliteCommand cmd = new SqliteCommand("SELECT CustomerId, CustomerName, Phone FROM Customers WHERE CustomerName LIKE @q OR Phone LIKE @q LIMIT 25", conn))
                {
                    cmd.Parameters.AddWithValue("@q", like);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new UniversalSearchResult
                            {
                                Kind = SearchResultKind.Customer,
                                Type = "عميل 👤",
                                Title = reader["CustomerName"].ToString(),
                                Subtitle = $"تليفون: {reader["Phone"]}",
                                TabKeyword = "العملاء",
                                MatchColumn = "CustomerId",
                                MatchValue = Convert.ToInt32(reader["CustomerId"])
                            });
                        }
                    }
                }

                using (SqliteCommand cmd = new SqliteCommand("SELECT SupplierId, SupplierName, Phone FROM Suppliers WHERE SupplierName LIKE @q OR Phone LIKE @q LIMIT 25", conn))
                {
                    cmd.Parameters.AddWithValue("@q", like);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new UniversalSearchResult
                            {
                                Kind = SearchResultKind.Supplier,
                                Type = "مورد 🚚",
                                Title = reader["SupplierName"].ToString(),
                                Subtitle = $"تليفون: {reader["Phone"]}",
                                TabKeyword = "الموردون",
                                MatchColumn = "SupplierId",
                                MatchValue = Convert.ToInt32(reader["SupplierId"])
                            });
                        }
                    }
                }

                using (SqliteCommand cmd = new SqliteCommand("SELECT TicketId, CustomerName, CustomerPhone, DeviceInfo, Status FROM MaintenanceTickets WHERE CustomerName LIKE @q OR CustomerPhone LIKE @q OR DeviceInfo LIKE @q LIMIT 25", conn))
                {
                    cmd.Parameters.AddWithValue("@q", like);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new UniversalSearchResult
                            {
                                Kind = SearchResultKind.Maintenance,
                                Type = "صيانة 🔧",
                                Title = $"{reader["CustomerName"]} - {reader["DeviceInfo"]}",
                                Subtitle = $"الحالة: {reader["Status"]} | تليفون: {reader["CustomerPhone"]}",
                                TabKeyword = "الصيانة",
                                MatchColumn = "TicketId",
                                MatchValue = Convert.ToInt32(reader["TicketId"])
                            });
                        }
                    }
                }

                using (SqliteCommand cmd = new SqliteCommand("SELECT SaleID, ProductName, Total, SaleDate, Barcode FROM Sales WHERE ProductName LIKE @q OR Barcode LIKE @q ORDER BY SaleID DESC LIMIT 25", conn))
                {
                    cmd.Parameters.AddWithValue("@q", like);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new UniversalSearchResult
                            {
                                Kind = SearchResultKind.Sale,
                                Type = "عملية بيع 🧾",
                                Title = reader["ProductName"].ToString(),
                                Subtitle = $"الإجمالي: {reader["Total"]} ج.م | بتاريخ: {reader["SaleDate"]}",
                                TabKeyword = "العمليات اليومية",
                                MatchColumn = "رقم البيع",
                                MatchValue = Convert.ToInt32(reader["SaleID"])
                            });
                        }
                    }
                }
            }

            return results;
        }

        private void ShowUniversalSearchDialog()
        {
            using (Form searchForm = new Form() { Text = "البحث الشامل 🔍", Size = new Size(950, 620), StartPosition = FormStartPosition.CenterParent, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true })
            {
                TextBox txtQuery = new TextBox() { Location = new Point(20, 20), Width = 700, Font = new Font("Segoe UI", 11) };
                Guna2Button btnGo = new Guna2Button() { Text = "بحث 🔍", Location = new Point(730, 17), Width = 100, Height = 32, FillColor = ColorPrimary };

                DataGridView dgvResults = new DataGridView() { Location = new Point(20, 60), Size = new Size(895, 480), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
                StyleDataGridView(dgvResults);

                Label lblHint = new Label() { Text = "دبل كليك على أي نتيجة عشان تفتحها في الشاشة بتاعتها مباشرة.", Location = new Point(20, 548), AutoSize = true, ForeColor = Color.DimGray };

                List<UniversalSearchResult> currentResults = new List<UniversalSearchResult>();

                Action runSearchAndBind = () =>
                {
                    currentResults = RunUniversalSearch(txtQuery.Text);
                    DataTable dt = new DataTable();
                    dt.Columns.Add("النوع");
                    dt.Columns.Add("العنوان");
                    dt.Columns.Add("تفاصيل إضافية");
                    foreach (var r in currentResults) dt.Rows.Add(r.Type, r.Title, r.Subtitle);
                    dgvResults.DataSource = dt;

                    if (currentResults.Count == 0 && !string.IsNullOrWhiteSpace(txtQuery.Text))
                        lblHint.Text = "مفيش أي نتائج مطابقة. جرب كلمة تانية.";
                    else
                        lblHint.Text = "دبل كليك على أي نتيجة عشان تفتحها في الشاشة بتاعتها مباشرة.";
                };

                btnGo.Click += (s, e) => runSearchAndBind();
                txtQuery.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { runSearchAndBind(); e.Handled = true; e.SuppressKeyPress = true; } };

                dgvResults.CellDoubleClick += (s, e) =>
                {
                    if (e.RowIndex < 0 || e.RowIndex >= currentResults.Count) return;
                    var chosen = currentResults[e.RowIndex];
                    searchForm.Close();
                    NavigateToSearchResult(chosen);
                };

                searchForm.Controls.AddRange(new Control[] { txtQuery, btnGo, dgvResults, lblHint });
                searchForm.Shown += (s, e) => txtQuery.Focus();
                searchForm.ShowDialog();
            }
        }

        // بيروح للتاب الصح، ويحدد نفس السجل جوه الجدول بتاعه
        private void NavigateToSearchResult(UniversalSearchResult result)
        {
            if (mainTabControl == null) return;

            TabPage targetTab = null;
            foreach (TabPage tp in mainTabControl.TabPages)
            {
                if (tp.Text.Contains(result.TabKeyword)) { targetTab = tp; break; }
            }

            if (targetTab == null)
            {
                MessageBox.Show("الشاشة دي مش متاحة لدورك الوظيفي الحالي.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            mainTabControl.SelectedTab = targetTab;

            DataGridView grid = null;
            switch (result.Kind)
            {
                case SearchResultKind.Product: grid = dgvProducts; break;
                case SearchResultKind.Customer: grid = dgvCustomers; break;
                case SearchResultKind.Supplier: grid = dgvSuppliers; break;
                case SearchResultKind.Maintenance: grid = dgvMaintenanceTickets; break;
                case SearchResultKind.Sale: grid = dgvSales; break;
            }

            if (grid == null || !grid.Columns.Contains(result.MatchColumn)) return;

            foreach (DataGridViewRow row in grid.Rows)
            {
                object cellValue = row.Cells[result.MatchColumn].Value;
                if (cellValue == null) continue;
                if (cellValue.ToString() == result.MatchValue.ToString())
                {
                    grid.ClearSelection();
                    row.Selected = true;

                    // نلاقي أول عمود ظاهر فعليًا في الصف، لأن بعض الأعمدة (زي الـ Id) بتكون مخفية
                    // وضبط CurrentCell على عمود مخفي بيرمي استثناء "invisible cell"
                    DataGridViewCell firstVisibleCell = null;
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (cell.OwningColumn != null && cell.OwningColumn.Visible)
                        {
                            firstVisibleCell = cell;
                            break;
                        }
                    }
                    if (firstVisibleCell != null) grid.CurrentCell = firstVisibleCell;

                    grid.FirstDisplayedScrollingRowIndex = row.Index;
                    break;
                }
            }
        }

        // ======================= نهاية قسم البحث الشامل =======================

        // بيعمل النسخة عن طريق قاعدة البيانات نفسها (VACUUM INTO) بدل نسخ الملف مباشرة،
        // عشان نضمن إن النسخة سليمة 100% حتى لو حصلت أي عملية كتابة في نفس اللحظة
        private string CreateBackupFile()
        {
            EnsureBackupFolderExists();
            string fileName = $"TemoStoreDB_Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db";
            string destPath = System.IO.Path.Combine(BackupFolderPath, fileName);

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                string escapedPath = destPath.Replace("'", "''");
                using (SqliteCommand cmd = new SqliteCommand($"VACUUM INTO '{escapedPath}';", conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }

            TryCopyToCloudBackup(destPath, fileName);
            return destPath;
        }

        // بيدور على فولدر Google Drive أو OneDrive متزامن على الجهاز، ولو لقى واحد بينسخ نسخة إضافية فيه
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
            catch
            {
                // فشل النسخ للسحابة (مثلاً النت واقع أو الفولدر مش متزامن) مايوقفش النسخة المحلية
            }
        }

        // بيحتفظ بآخر 30 نسخة بس، وبيمسح الأقدم عشان الهارد ميمتلئش بمرور الوقت
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

            if (lblCloudStatus != null)
            {
                string cloudFolder = GetCloudBackupFolder();
                lblCloudStatus.Text = cloudFolder != null
                    ? $"☁️ متصل بخدمة تخزين سحابي، وبيتحفظ فيها نسخة إضافية تلقائيًا: {cloudFolder}"
                    : "☁️ مفيش Google Drive أو OneDrive متزامن على الجهاز ده حاليًا، فالنسخ بتتحفظ محليًا بس.";
                lblCloudStatus.ForeColor = cloudFolder != null ? ColorSuccess : ColorDanger;
            }
        }

        private void CreateBackupDesign(TabPage page)
        {
            Label lblTitle = new Label() { Text = "النسخ الاحتياطي والاسترجاع 💾", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = ColorPrimary, Location = new Point(20, 20), AutoSize = true };
            lblBackupStatus = new Label() { Text = "", Location = new Point(20, 55), AutoSize = true, ForeColor = Color.Gray };
            lblCloudStatus = new Label() { Text = "", Location = new Point(20, 78), AutoSize = true };

            btnCreateBackupNow = new Guna2Button() { Text = "عمل نسخة احتياطية الآن 💾", Location = new Point(20, 105), Width = 230, Height = 40, FillColor = ColorSuccess };
            btnCreateBackupNow.Click += BtnCreateBackupNow_Click;

            btnRestoreBackup = new Guna2Button() { Text = "استرجاع النسخة المحددة ⏪", Location = new Point(260, 105), Width = 230, Height = 40, FillColor = ColorWarning };
            btnRestoreBackup.Click += BtnRestoreBackup_Click;

            btnDeleteBackup = new Guna2Button() { Text = "حذف النسخة المحددة 🗑️", Location = new Point(500, 105), Width = 220, Height = 40, FillColor = ColorDanger };
            btnDeleteBackup.Click += BtnDeleteBackup_Click;

            btnOpenBackupFolder = new Guna2Button() { Text = "فتح مجلد النسخ 📂", Location = new Point(730, 105), Width = 200, Height = 40, FillColor = ColorNeutral, ForeColor = ColorPrimary };
            btnOpenBackupFolder.Click += BtnOpenBackupFolder_Click;

            Label lblNote = new Label()
            {
                Text = "ملاحظة: البرنامج بياخد نسخة احتياطية تلقائية عند فتح البرنامج وعند قفله كمان (مش مرة واحدة بس)، بيحتفظ بآخر 30 نسخة تلقائيًا،\nوبيستخدم طريقة قاعدة البيانات الآمنة (VACUUM INTO) عشان يضمن إن النسخة سليمة 100%. الاسترجاع بيستبدل كل بيانات البرنامج الحالية بالنسخة اللي هتختارها.",
                Location = new Point(20, 155),
                Size = new Size(1050, 40),
                ForeColor = Color.DimGray
            };

            dgvBackups = new DataGridView() { Location = new Point(20, 205), Size = new Size(1080, 465), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
            StyleDataGridView(dgvBackups);

            page.AutoScroll = true;
            page.Controls.AddRange(new Control[] { lblTitle, lblBackupStatus, lblCloudStatus, btnCreateBackupNow, btnRestoreBackup, btnDeleteBackup, btnOpenBackupFolder, lblNote, dgvBackups });

            LoadBackupsGrid();
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
                // نعمل نسخة احتياطية من الوضع الحالي الأول قبل الاسترجاع، احتياطًا لو غلطت في الاختيار
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

        // ======================= نهاية قسم النسخ الاحتياطي =======================

        // ======================= إعدادات المحل ⚙️ =======================

        private void CreateStoreSettingsDesign(TabPage page)
        {
            Label lblTitle = new Label() { Text = "إعدادات المحل ⚙️", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = ColorPrimary, Location = new Point(20, 20), AutoSize = true };

            GroupBox gbLogo = new GroupBox() { Text = "شعار المحل", Location = new Point(20, 65), Size = new Size(240, 260) };
            picStoreLogo = new PictureBox() { Location = new Point(20, 25), Size = new Size(200, 160), SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
            btnUploadLogo = new Guna2Button() { Text = "رفع شعار 🖼️", Location = new Point(20, 195), Width = 200, Height = 30, FillColor = ColorPrimary };
            btnUploadLogo.Click += BtnUploadLogo_Click;
            btnRemoveLogo = new Guna2Button() { Text = "إزالة الشعار 🗑️", Location = new Point(20, 230), Width = 200, Height = 30, FillColor = ColorDanger };
            btnRemoveLogo.Click += BtnRemoveLogo_Click;
            gbLogo.Controls.AddRange(new Control[] { picStoreLogo, btnUploadLogo, btnRemoveLogo });

            GroupBox gbInfo = new GroupBox() { Text = "بيانات المحل", Location = new Point(280, 65), Size = new Size(420, 260) };

            Label lblStoreName = new Label() { Text = "اسم المحل:", Location = new Point(15, 30), AutoSize = true };
            txtSettingsStoreName = new TextBox() { Location = new Point(15, 50), Width = 380 };

            Label lblPhone = new Label() { Text = "رقم التليفون:", Location = new Point(15, 90), AutoSize = true };
            txtSettingsPhone = new TextBox() { Location = new Point(15, 110), Width = 380 };

            Label lblAddress = new Label() { Text = "العنوان:", Location = new Point(15, 150), AutoSize = true };
            txtSettingsAddress = new TextBox() { Location = new Point(15, 170), Width = 380, Height = 60, Multiline = true };

            btnSaveStoreSettings = new Guna2Button() { Text = "حفظ الإعدادات 💾", Location = new Point(15, 210), Width = 380, Height = 35, FillColor = ColorSuccess };
            btnSaveStoreSettings.Click += BtnSaveStoreSettings_Click;

            gbInfo.Controls.AddRange(new Control[] { lblStoreName, txtSettingsStoreName, lblPhone, txtSettingsPhone, lblAddress, txtSettingsAddress, btnSaveStoreSettings });

            Label lblNote = new Label()
            {
                Text = "البيانات دي هتظهر تلقائيًا في رأس فاتورة البيع المطبوعة (اسم المحل، التليفون، العنوان)، وبتتحفظ جوه قاعدة البيانات نفسها فتتضمن تلقائيًا في أي نسخة احتياطية.",
                Location = new Point(20, 335),
                Size = new Size(680, 40),
                ForeColor = Color.DimGray
            };

            page.AutoScroll = true;
            page.Controls.AddRange(new Control[] { lblTitle, gbLogo, gbInfo, lblNote });

            // تعبئة الحقول بالقيم المحفوظة حاليًا
            txtSettingsStoreName.Text = CurrentStoreName;
            txtSettingsPhone.Text = CurrentStorePhone;
            txtSettingsAddress.Text = CurrentStoreAddress;
            RefreshLogoPreview();
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
                        // نقلل حجم الصورة قبل ما نحفظها عشان مايبقاش حجم قاعدة البيانات كبير من غير داعي
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
                using (SqliteConnection conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    using (SqliteCommand cmd = new SqliteCommand("UPDATE StoreSettings SET StoreName = @Name, Phone = @Phone, Address = @Address, LogoImage = @Logo WHERE Id = 1;", conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", txtSettingsStoreName.Text.Trim());
                        cmd.Parameters.AddWithValue("@Phone", txtSettingsPhone.Text.Trim());
                        cmd.Parameters.AddWithValue("@Address", txtSettingsAddress.Text.Trim());
                        cmd.Parameters.AddWithValue("@Logo", (object)CurrentStoreLogo ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }

                LoadStoreSettingsIntoMemory();
                MessageBox.Show("تم حفظ إعدادات المحل بنجاح ✅", "نجاح", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("حصل خطأ أثناء حفظ الإعدادات: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ======================= نهاية قسم إعدادات المحل =======================

        // ======================= جرد المخزن 📋 =======================

        private void CreateInventoryCountDesign(TabPage page)
        {
            Label lblTitle = new Label() { Text = "جرد المخزن 📋", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = ColorPrimary, Location = new Point(20, 20), AutoSize = true };

            Label lblSearch = new Label() { Text = "بحث بالاسم أو الباركود:", Location = new Point(20, 60), AutoSize = true };
            txtInventorySearch = new TextBox() { Location = new Point(190, 57), Width = 250 };
            txtInventorySearch.TextChanged += (s, e) => FilterInventoryCountGrid();

            btnRefreshInventoryCount = new Guna2Button() { Text = "بدء جرد جديد 🔄", Location = new Point(460, 55), Width = 180, Height = 32, FillColor = ColorNeutral, ForeColor = ColorPrimary };
            btnRefreshInventoryCount.Click += BtnRefreshInventoryCount_Click;

            btnViewAdjustmentsLog = new Guna2Button() { Text = "سجل التسويات السابقة 📜", Location = new Point(660, 55), Width = 220, Height = 32, FillColor = ColorNeutral, ForeColor = ColorPrimary };
            btnViewAdjustmentsLog.Click += BtnViewAdjustmentsLog_Click;

            Label lblNote = new Label()
            {
                Text = "اكتب الكمية الفعلية اللي عددتها بإيدك في عمود \"الكمية الفعلية\" لأي صنف جردته، والباقي سيبه فاضي. عمود \"الفرق\" هيتحسب لوحده. لما تخلص، دوس \"حفظ نتيجة الجرد\" تحت.",
                Location = new Point(20, 95),
                Size = new Size(1050, 20),
                ForeColor = Color.DimGray
            };

            dgvInventoryCount = new DataGridView() { Location = new Point(20, 120), Size = new Size(1080, 490), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, AllowUserToAddRows = false, RowHeadersVisible = false };
            StyleDataGridView(dgvInventoryCount);
            dgvInventoryCount.CellValueChanged += DgvInventoryCount_CellValueChanged;
            dgvInventoryCount.CellFormatting += DgvInventoryCount_CellFormatting;
            dgvInventoryCount.CurrentCellDirtyStateChanged += (s, e) => { if (dgvInventoryCount.IsCurrentCellDirty) dgvInventoryCount.CommitEdit(DataGridViewDataErrorContexts.Commit); };

            btnSaveInventoryCount = new Guna2Button() { Text = "حفظ نتيجة الجرد وتسوية الفروقات ✅", Location = new Point(20, 620), Width = 320, Height = 40, FillColor = ColorSuccess };
            btnSaveInventoryCount.Click += BtnSaveInventoryCount_Click;

            page.AutoScroll = true;
            page.Controls.AddRange(new Control[] { lblTitle, lblSearch, txtInventorySearch, btnRefreshInventoryCount, btnViewAdjustmentsLog, lblNote, dgvInventoryCount, btnSaveInventoryCount });

            LoadInventoryCountGrid();
        }

        private void LoadInventoryCountGrid()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("الباركود");
            dt.Columns.Add("اسم المنتج");
            dt.Columns.Add("الكمية بالنظام", typeof(int));
            dt.Columns.Add("الكمية الفعلية");
            dt.Columns.Add("الفرق");

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT Barcode, ProductName, Quantity FROM Products ORDER BY ProductName ASC", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        dt.Rows.Add(reader["Barcode"].ToString(), reader["ProductName"].ToString(), Convert.ToInt32(reader["Quantity"]), "", "");
                }
            }

            dgvInventoryCount.DataSource = dt;
            dgvInventoryCount.Columns["الباركود"].ReadOnly = true;
            dgvInventoryCount.Columns["اسم المنتج"].ReadOnly = true;
            dgvInventoryCount.Columns["الكمية بالنظام"].ReadOnly = true;
            dgvInventoryCount.Columns["الفرق"].ReadOnly = true;
            // عمود "الكمية الفعلية" هو الوحيد القابل للتعديل، عشان المستخدم يكتب فيه بس نتيجة العد اليدوي
        }

        private void FilterInventoryCountGrid()
        {
            if (!(dgvInventoryCount.DataSource is DataTable dt)) return;
            string search = txtInventorySearch.Text.Trim().Replace("'", "''");
            dt.DefaultView.RowFilter = string.IsNullOrEmpty(search) ? "" : $"[اسم المنتج] LIKE '%{search}%' OR [الباركود] LIKE '%{search}%'";
        }

        private void DgvInventoryCount_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgvInventoryCount.Columns[e.ColumnIndex].Name != "الكمية الفعلية") return;

            var row = dgvInventoryCount.Rows[e.RowIndex];
            string countedText = row.Cells["الكمية الفعلية"].Value?.ToString().Trim();
            int systemQty = Convert.ToInt32(row.Cells["الكمية بالنظام"].Value);

            if (string.IsNullOrEmpty(countedText))
            {
                row.Cells["الفرق"].Value = "";
            }
            else if (int.TryParse(countedText, out int countedQty) && countedQty >= 0)
            {
                int diff = countedQty - systemQty;
                row.Cells["الفرق"].Value = diff == 0 ? "مطابق" : (diff > 0 ? $"+{diff}" : diff.ToString());
            }
            else
            {
                MessageBox.Show("من فضلك ادخل رقم صحيح موجب للكمية.", "قيمة غير صحيحة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                row.Cells["الكمية الفعلية"].Value = "";
                row.Cells["الفرق"].Value = "";
            }
        }

        private void DgvInventoryCount_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgvInventoryCount.Columns[e.ColumnIndex].Name != "الفرق") return;

            string val = e.Value?.ToString();
            if (string.IsNullOrEmpty(val)) return;

            if (val == "مطابق") e.CellStyle.ForeColor = ColorSuccess;
            else if (val.StartsWith("+")) e.CellStyle.ForeColor = Color.FromArgb(41, 128, 185); // أزرق: زيادة عن المسجل بالنظام
            else e.CellStyle.ForeColor = ColorDanger; // نقص عن المسجل بالنظام
        }

        private void BtnRefreshInventoryCount_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show("هل تريد بدء جرد جديد؟ أي كميات مكتوبة دلوقتي ومتحفظتش هتتمسح.", "تأكيد", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;
            txtInventorySearch.Clear();
            LoadInventoryCountGrid();
        }

        private void BtnSaveInventoryCount_Click(object sender, EventArgs e)
        {
            if (!(dgvInventoryCount.DataSource is DataTable dt)) return;

            var changedRows = new List<(string Barcode, string ProductName, int SystemQty, int CountedQty, int Difference)>();

            foreach (DataRow dr in dt.Rows)
            {
                string countedText = dr["الكمية الفعلية"]?.ToString().Trim();
                if (string.IsNullOrEmpty(countedText)) continue;
                if (!int.TryParse(countedText, out int countedQty)) continue;

                int systemQty = Convert.ToInt32(dr["الكمية بالنظام"]);
                int diff = countedQty - systemQty;
                if (diff == 0) continue; // مطابق، مفيش داعي نسجله في السجل

                changedRows.Add((dr["الباركود"].ToString(), dr["اسم المنتج"].ToString(), systemQty, countedQty, diff));
            }

            if (changedRows.Count == 0)
            {
                MessageBox.Show("مفيش أي فروقات لتسويتها. إما لسه محددتش كميات، أو كل الكميات المكتوبة مطابقة للنظام.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string summary = string.Join("\n", changedRows.Select(r => $"{r.ProductName}: {r.SystemQty} ← {r.CountedQty} ({(r.Difference > 0 ? "+" : "")}{r.Difference})"));
            var confirm = MessageBox.Show($"هيتم تعديل كمية {changedRows.Count} صنف في المخزون حسب نتيجة الجرد ده:\n\n{summary}\n\nمتأكد إنك عايز تكمل؟", "تأكيد تسوية الجرد", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        foreach (var r in changedRows)
                        {
                            using (SqliteCommand cmdUpdate = new SqliteCommand("UPDATE Products SET Quantity = @NewQty WHERE Barcode = @Barcode", conn, transaction))
                            {
                                cmdUpdate.Parameters.AddWithValue("@NewQty", r.CountedQty);
                                cmdUpdate.Parameters.AddWithValue("@Barcode", r.Barcode);
                                cmdUpdate.ExecuteNonQuery();
                            }

                            using (SqliteCommand cmdLog = new SqliteCommand("INSERT INTO InventoryAdjustments (Barcode, ProductName, SystemQuantityBefore, CountedQuantity, Difference, AdjustmentDate) VALUES (@Barcode, @Name, @Before, @Counted, @Diff, @Date)", conn, transaction))
                            {
                                cmdLog.Parameters.AddWithValue("@Barcode", r.Barcode);
                                cmdLog.Parameters.AddWithValue("@Name", r.ProductName);
                                cmdLog.Parameters.AddWithValue("@Before", r.SystemQty);
                                cmdLog.Parameters.AddWithValue("@Counted", r.CountedQty);
                                cmdLog.Parameters.AddWithValue("@Diff", r.Difference);
                                cmdLog.Parameters.AddWithValue("@Date", nowStr);
                                cmdLog.ExecuteNonQuery();
                            }
                        }
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show("حصل خطأ أثناء حفظ الجرد: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }

            MessageBox.Show($"تم تسوية {changedRows.Count} صنف بنجاح ✅", "نجاح", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadProductsData();
            LoadInventoryCountGrid();
        }

        private void BtnViewAdjustmentsLog_Click(object sender, EventArgs e)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("التاريخ والوقت");
            dt.Columns.Add("الباركود");
            dt.Columns.Add("اسم المنتج");
            dt.Columns.Add("الكمية قبل الجرد");
            dt.Columns.Add("الكمية بعد الجرد");
            dt.Columns.Add("الفرق");

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT AdjustmentDate, Barcode, ProductName, SystemQuantityBefore, CountedQuantity, Difference FROM InventoryAdjustments ORDER BY AdjustmentId DESC", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int diff = Convert.ToInt32(reader["Difference"]);
                        dt.Rows.Add(reader["AdjustmentDate"], reader["Barcode"], reader["ProductName"], reader["SystemQuantityBefore"], reader["CountedQuantity"], diff > 0 ? $"+{diff}" : diff.ToString());
                    }
                }
            }

            using (Form logForm = new Form() { Text = "سجل تسويات الجرد 📜", Size = new Size(900, 600), StartPosition = FormStartPosition.CenterParent, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true })
            {
                DataGridView dgvLog = new DataGridView() { Dock = DockStyle.Fill, DataSource = dt, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
                StyleDataGridView(dgvLog);
                logForm.Controls.Add(dgvLog);
                logForm.ShowDialog();
            }
        }

        // ======================= نهاية قسم جرد المخزن =======================

        private void BtnReceiveDevice_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtMaintCustomerName.Text) || string.IsNullOrWhiteSpace(txtMaintDeviceInfo.Text))
            {
                MessageBox.Show("من فضلك أدخل اسم العميل والجهاز على الأقل.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal.TryParse(txtMaintEstimatedCost.Text, out decimal estimatedCost);

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(
                    "INSERT INTO MaintenanceTickets (CustomerName, CustomerPhone, DeviceInfo, IssueDescription, ReceivedDate, EstimatedCost, Status) VALUES (@N, @P, @D, @I, @R, @E, 'مستلم')", conn))
                {
                    cmd.Parameters.AddWithValue("@N", txtMaintCustomerName.Text.Trim());
                    cmd.Parameters.AddWithValue("@P", string.IsNullOrWhiteSpace(txtMaintCustomerPhone.Text) ? (object)DBNull.Value : txtMaintCustomerPhone.Text.Trim());
                    cmd.Parameters.AddWithValue("@D", txtMaintDeviceInfo.Text.Trim());
                    cmd.Parameters.AddWithValue("@I", string.IsNullOrWhiteSpace(txtMaintIssueDescription.Text) ? (object)DBNull.Value : txtMaintIssueDescription.Text.Trim());
                    cmd.Parameters.AddWithValue("@R", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@E", estimatedCost);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم استلام الجهاز وتسجيل التذكرة بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtMaintCustomerName.Clear();
            txtMaintCustomerPhone.Clear();
            txtMaintDeviceInfo.Clear();
            txtMaintIssueDescription.Clear();
            txtMaintEstimatedCost.Clear();
            LoadMaintenanceGrid();
        }

        private void LoadMaintenanceGrid()
        {
            if (dgvMaintenanceTickets == null) return;

            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("TicketId"), new DataColumn("العميل"), new DataColumn("التليفون"), new DataColumn("الجهاز"),
                new DataColumn("العطل"), new DataColumn("تاريخ الاستلام"), new DataColumn("التقديري"), new DataColumn("الفعلي"), new DataColumn("الحالة"), new DataColumn("تاريخ التسليم") });

            string statusFilter = cmbMaintStatusFilter?.SelectedItem?.ToString() ?? "الكل";
            string query = "SELECT * FROM MaintenanceTickets";
            if (statusFilter != "الكل") query += " WHERE Status = @Status";
            query += " ORDER BY ReceivedDate DESC";

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    if (statusFilter != "الكل") cmd.Parameters.AddWithValue("@Status", statusFilter);
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                dt.Rows.Add(reader["TicketId"], reader["CustomerName"], reader["CustomerPhone"], reader["DeviceInfo"],
                                    reader["IssueDescription"], reader["ReceivedDate"],
                                    reader["EstimatedCost"] == DBNull.Value ? "" : Convert.ToDecimal(reader["EstimatedCost"]).ToString("N2"),
                                    reader["ActualCost"] == DBNull.Value ? "" : Convert.ToDecimal(reader["ActualCost"]).ToString("N2"),
                                    reader["Status"], reader["DeliveredDate"] == DBNull.Value ? "" : reader["DeliveredDate"]);
                            }
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }

            dgvMaintenanceTickets.DataSource = dt;
            if (dgvMaintenanceTickets.Columns["TicketId"] != null) dgvMaintenanceTickets.Columns["TicketId"].Visible = false;
        }

        private void DgvMaintenanceTickets_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvMaintenanceTickets.Rows[e.RowIndex];
            selectedTicketId = Convert.ToInt32(row.Cells["TicketId"].Value);
            string currentStatus = row.Cells["الحالة"].Value.ToString();
            cmbMaintStatusUpdate.SelectedItem = currentStatus;

            string estCostStr = row.Cells["التقديري"].Value?.ToString();
            if (!string.IsNullOrEmpty(estCostStr)) txtMaintActualCost.Text = estCostStr;
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

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("UPDATE MaintenanceTickets SET Status = @S WHERE TicketId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@S", newStatus);
                    cmd.Parameters.AddWithValue("@Id", selectedTicketId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }

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
            string customerName = "";

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                using (SqliteCommand cmdGet = new SqliteCommand("SELECT CustomerName FROM MaintenanceTickets WHERE TicketId = @Id", conn))
                {
                    cmdGet.Parameters.AddWithValue("@Id", selectedTicketId);
                    var res = cmdGet.ExecuteScalar();
                    customerName = res?.ToString() ?? "";
                }

                using (SqliteCommand cmd = new SqliteCommand(
                    "UPDATE MaintenanceTickets SET Status = 'تم التسليم', ActualCost = @A, DeliveredDate = @D WHERE TicketId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@A", actualCost);
                    cmd.Parameters.AddWithValue("@D", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@Id", selectedTicketId);
                    cmd.ExecuteNonQuery();
                }

                if (actualCost > 0)
                {
                    using (SqliteCommand cmd = new SqliteCommand(
                        "INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode) VALUES (@Date, 'قبض', @Method, @Amount, @Ref, @Desc, @CreatedAt, 4200)", conn))
                    {
                        cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@Method", method);
                        cmd.Parameters.AddWithValue("@Amount", actualCost);
                        cmd.Parameters.AddWithValue("@Ref", "تذكرة صيانة رقم " + selectedTicketId);
                        cmd.Parameters.AddWithValue("@Desc", "أجرة صيانة - " + customerName);
                        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.ExecuteNonQuery();
                    }

                    using (SqliteCommand cmd = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = CurrentBalance + @Amount WHERE PaymentMethod = @Method", conn))
                    {
                        cmd.Parameters.AddWithValue("@Amount", actualCost);
                        cmd.Parameters.AddWithValue("@Method", method);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            MessageBox.Show("تم تسليم الجهاز وتحصيل الأجرة بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            selectedTicketId = -1;
            txtMaintActualCost.Clear();
            LoadMaintenanceGrid();
            RefreshClosureSummary();
        }

        private void CreateUsersManagementDesign(TabPage page)
        {
            GroupBox gbAddUser = new GroupBox() { Text = "إضافة / تعديل مستخدم", Location = new Point(20, 20), Size = new Size(260, 380) };

            Label lblUsername = new Label() { Text = "اسم المستخدم:", Location = new Point(10, 25), AutoSize = true };
            txtNewUsername = new TextBox() { Location = new Point(10, 45), Width = 230 };

            Label lblPassword = new Label() { Text = "كلمة المرور (سيبها فاضية لو مش عايز تغيّرها):", Location = new Point(10, 80), Size = new Size(230, 30) };
            txtNewUserPassword = new TextBox() { Location = new Point(10, 110), Width = 230, PasswordChar = '●' };

            Label lblRole = new Label() { Text = "الدور:", Location = new Point(10, 145), AutoSize = true };
            cmbNewUserRole = new Guna2ComboBox() { Location = new Point(10, 165), Width = 230, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbNewUserRole.Items.AddRange(new string[] { "Employee", "Admin" });
            cmbNewUserRole.SelectedIndex = 0;

            Guna2Button btnAddUser = new Guna2Button() { Text = "إضافة مستخدم جديد ✅", Location = new Point(10, 205), Width = 230, Height = 35, FillColor = ColorSuccess };
            btnAddUser.Click += BtnAddUser_Click;

            Guna2Button btnEditUserMode = new Guna2Button() { Text = "تعديل المستخدم المحدد ✏️", Location = new Point(10, 245), Width = 230, Height = 35, FillColor = ColorPrimary };
            btnEditUserMode.Click += BtnEditUserMode_Click;

            btnSaveUserEdit = new Guna2Button() { Text = "حفظ تعديل المستخدم 💾", Location = new Point(10, 285), Width = 230, Height = 35, FillColor = ColorWarning, Enabled = false };
            btnSaveUserEdit.Click += BtnSaveUserEdit_Click;

            Guna2Button btnDeleteUser = new Guna2Button() { Text = "حذف المستخدم المحدد ❌", Location = new Point(10, 325), Width = 230, Height = 30, FillColor = ColorDanger };
            btnDeleteUser.Click += BtnDeleteUser_Click;

            gbAddUser.Controls.AddRange(new Control[] { lblUsername, txtNewUsername, lblPassword, txtNewUserPassword, lblRole, cmbNewUserRole, btnAddUser, btnEditUserMode, btnSaveUserEdit, btnDeleteUser });

            Label lblNote = new Label()
            {
                Text = "Admin: يشوف ويعمل كل حاجة في البرنامج.\nEmployee: يقدر يبيع ويسجّل مصروفات وحركات بس، من غير تعديل أو إلغاء أو حذف، ومن غير ما يشوف سعر الشراء أو التقارير المالية.\n\nللتعديل: دوس على المستخدم في الجدول، وبعدين دوس \"تعديل المستخدم المحدد\"، وبعدين \"حفظ تعديل المستخدم\".",
                Location = new Point(20, 410),
                Size = new Size(260, 140),
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.Gray
            };

            Label lblGridTitle = new Label() { Text = "المستخدمين المسجّلين:", Location = new Point(310, 20), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

            dgvUsers = new DataGridView() { Location = new Point(310, 45), Size = new Size(780, 600), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvUsers.CellClick += DgvUsers_CellClick;
            StyleDataGridView(dgvUsers);

            page.AutoScroll = true;
            page.Controls.AddRange(new Control[] { gbAddUser, lblNote, lblGridTitle, dgvUsers });

            LoadUsersGrid();
        }

        private int selectedUserId = -1;

        private void LoadUsersGrid()
        {
            if (dgvUsers == null) return;

            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("Id"), new DataColumn("اسم المستخدم"), new DataColumn("الدور"), new DataColumn("تاريخ الإنشاء") });

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("SELECT Id, Username, Role, CreatedAt FROM Users ORDER BY Id", conn))
                {
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                dt.Rows.Add(reader["Id"], reader["Username"], reader["Role"], reader["CreatedAt"]);
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
            dgvUsers.DataSource = dt;
            if (dgvUsers.Columns["Id"] != null) dgvUsers.Columns["Id"].Visible = false;
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

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                string oldUsername = null, oldRole = null;
                using (SqliteCommand cmd = new SqliteCommand("SELECT Username, Role FROM Users WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", selectedUserId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            oldUsername = reader["Username"].ToString();
                            oldRole = reader["Role"].ToString();
                        }
                    }
                }

                if (oldUsername == null)
                {
                    MessageBox.Show("لم يتم العثور على المستخدم.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // منع تكرار اسم المستخدم مع مستخدم تاني
                using (SqliteCommand cmdCheck = new SqliteCommand("SELECT COUNT(*) FROM Users WHERE Username = @U AND Id <> @Id", conn))
                {
                    cmdCheck.Parameters.AddWithValue("@U", newUsername);
                    cmdCheck.Parameters.AddWithValue("@Id", selectedUserId);
                    if (Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0)
                    {
                        MessageBox.Show("اسم المستخدم ده مستخدم بالفعل لحساب تاني.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                // منع إنزال آخر أدمن لموظف
                if (oldRole == "Admin" && newRole != "Admin")
                {
                    using (SqliteCommand cmdCount = new SqliteCommand("SELECT COUNT(*) FROM Users WHERE Role = 'Admin'", conn))
                    {
                        int adminCount = Convert.ToInt32(cmdCount.ExecuteScalar());
                        if (adminCount <= 1)
                        {
                            MessageBox.Show("لا يمكن تغيير دور آخر حساب أدمن في النظام.", "غير مسموح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                }

                if (string.IsNullOrEmpty(txtNewUserPassword.Text))
                {
                    // من غير تغيير كلمة المرور
                    using (SqliteCommand cmd = new SqliteCommand("UPDATE Users SET Username = @U, Role = @R WHERE Id = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@U", newUsername);
                        cmd.Parameters.AddWithValue("@R", newRole);
                        cmd.Parameters.AddWithValue("@Id", selectedUserId);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    using (SqliteCommand cmd = new SqliteCommand("UPDATE Users SET Username = @U, Role = @R, PasswordHash = @P WHERE Id = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@U", newUsername);
                        cmd.Parameters.AddWithValue("@R", newRole);
                        cmd.Parameters.AddWithValue("@P", AuthManager.HashPassword(txtNewUserPassword.Text));
                        cmd.Parameters.AddWithValue("@Id", selectedUserId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

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

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                using (SqliteCommand cmdCheck = new SqliteCommand("SELECT COUNT(*) FROM Users WHERE Username = @U", conn))
                {
                    cmdCheck.Parameters.AddWithValue("@U", txtNewUsername.Text.Trim());
                    if (Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0)
                    {
                        MessageBox.Show("اسم المستخدم ده مستخدم بالفعل.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                using (SqliteCommand cmd = new SqliteCommand("INSERT INTO Users (Username, PasswordHash, Role, CreatedAt) VALUES (@U, @P, @R, @C)", conn))
                {
                    cmd.Parameters.AddWithValue("@U", txtNewUsername.Text.Trim());
                    cmd.Parameters.AddWithValue("@P", AuthManager.HashPassword(txtNewUserPassword.Text));
                    cmd.Parameters.AddWithValue("@R", cmbNewUserRole.SelectedItem.ToString());
                    cmd.Parameters.AddWithValue("@C", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم إضافة المستخدم بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtNewUsername.Clear();
            txtNewUserPassword.Clear();
            cmbNewUserRole.SelectedIndex = 0;
            selectedUserId = -1;
            if (btnSaveUserEdit != null) btnSaveUserEdit.Enabled = false;
            LoadUsersGrid();
        }

        private void BtnDeleteUser_Click(object sender, EventArgs e)
        {
            if (selectedUserId == -1)
            {
                MessageBox.Show("من فضلك اختر مستخدم من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                string username = null, role = null;
                using (SqliteCommand cmd = new SqliteCommand("SELECT Username, Role FROM Users WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", selectedUserId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            username = reader["Username"].ToString();
                            role = reader["Role"].ToString();
                        }
                    }
                }

                if (username == null)
                {
                    MessageBox.Show("لم يتم العثور على المستخدم.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (username == AuthManager.CurrentUsername)
                {
                    MessageBox.Show("لا يمكنك حذف حسابك اللي داخل بيه دلوقتي.", "غير مسموح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (role == "Admin")
                {
                    int adminCount;
                    using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM Users WHERE Role = 'Admin'", conn))
                    {
                        adminCount = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    if (adminCount <= 1)
                    {
                        MessageBox.Show("لا يمكن حذف آخر حساب أدمن في النظام.", "غير مسموح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                if (MessageBox.Show($"هل أنت متأكد من حذف المستخدم \"{username}\"؟", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                using (SqliteCommand cmd = new SqliteCommand("DELETE FROM Users WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", selectedUserId);
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم حذف المستخدم بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            selectedUserId = -1;
            txtNewUsername.Clear();
            txtNewUserPassword.Clear();
            cmbNewUserRole.SelectedIndex = 0;
            if (btnSaveUserEdit != null) btnSaveUserEdit.Enabled = false;
            LoadUsersGrid();
        }

        private void CreateUnifiedOperationsDesign(TabPage page)
        {
            Label lblOpType = new Label() { Text = "نوع العملية:", Location = new Point(20, 20), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            cmbOperationType = new Guna2ComboBox() { Location = new Point(130, 17), Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbOperationType.Items.AddRange(new string[] { "بيع منتج 🛒", "مصروف عمومي 💸", "حركة قبض/صرف 💰" });
            cmbOperationType.SelectedIndexChanged += CmbOperationType_SelectedIndexChanged;

            pnlSaleOps = new Panel() { Location = new Point(20, 55), Size = new Size(280, 660) };
            pnlExpenseOps = new Panel() { Location = new Point(20, 55), Size = new Size(280, 660) };
            pnlMovementOps = new Panel() { Location = new Point(20, 55), Size = new Size(280, 660) };

            // كل واحدة من الميثودات دي بتبني خانات وأزرار قسمها زي ما هي، وبتعمل كمان جدول قديم بتاعها
            // مش هيتعرض (هنستخدم بدل منه الجدول الموحد اللي تحت)، فبنشيله من على الشاشة بس نسيبه شغال كمخزن بيانات
            CreatePOSDesign(pnlSaleOps);
            pnlSaleOps.Controls.Remove(dgvSales);

            CreateExpensesDesign(pnlExpenseOps);
            pnlExpenseOps.Controls.Remove(dgvExpenses);

            CreateCashMovementsDesign(pnlMovementOps);
            pnlMovementOps.Controls.Remove(dgvCashMovements);

            pnlExpenseOps.Visible = false;
            pnlMovementOps.Visible = false;

            Label lblGridTitle = new Label() { Text = "كل العمليات (بيع / مصروف / حركة قبض وصرف) مع بعض، الأحدث فالأقدم:", Location = new Point(320, 20), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

            dgvUnifiedOperations = new DataGridView() { Location = new Point(320, 45), Size = new Size(790, 660), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells, ReadOnly = true, AllowUserToAddRows = false };
            dgvUnifiedOperations.CellClick += DgvUnifiedOperations_CellClick;
            dgvUnifiedOperations.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dgvUnifiedOperations.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvUnifiedOperations.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            StyleDataGridView(dgvUnifiedOperations);

            page.AutoScroll = true;
            page.Controls.AddRange(new Control[] { lblOpType, cmbOperationType, pnlSaleOps, pnlExpenseOps, pnlMovementOps, lblGridTitle, dgvUnifiedOperations });

            cmbOperationType.SelectedIndex = 0;
            LoadUnifiedOperations();
        }

        private void CmbOperationType_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = cmbOperationType.SelectedIndex;
            pnlSaleOps.Visible = idx == 0;
            pnlExpenseOps.Visible = idx == 1;
            pnlMovementOps.Visible = idx == 2;
        }

        // بيجمع المبيعات والمصروفات وحركات القبض والصرف في جدول واحد للعرض
        private void LoadUnifiedOperations()
        {
            if (dgvUnifiedOperations == null) return;

            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] {
                new DataColumn("النوع"), new DataColumn("SourceId", typeof(int)),
                new DataColumn("التفاصيل"), new DataColumn("المبلغ"), new DataColumn("التاريخ والوقت")
            });

            string query = @"
                SELECT 'بيع' AS OpType, SaleID AS SourceId, ProductName AS Details, Total AS Amount, SaleDate AS OpDate FROM Sales
                UNION ALL
                SELECT 'مصروف' AS OpType, E.ExpenseID AS SourceId, A.AccountName AS Details, E.Amount AS Amount, E.ExpenseDate AS OpDate
                    FROM Expenses E INNER JOIN AccountsTree A ON E.AccountCode = A.AccountCode
                UNION ALL
                SELECT CM.MovementType AS OpType, CM.Id AS SourceId,
                       (CM.PaymentMethod ||
                        CASE WHEN AC.AccountName IS NOT NULL THEN ' - ' || AC.AccountName ELSE '' END ||
                        CASE WHEN CM.Description IS NOT NULL AND CM.Description <> '' THEN ' - ' || CM.Description ELSE '' END) AS Details,
                       CM.Amount AS Amount, CM.CreatedAt AS OpDate
                    FROM CashMovements CM LEFT JOIN AccountsTree AC ON CM.AccountCode = AC.AccountCode
                ORDER BY OpDate DESC";

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                dt.Rows.Add(reader["OpType"], Convert.ToInt32(reader["SourceId"]), reader["Details"], reader["Amount"], reader["OpDate"]);
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
            dgvUnifiedOperations.DataSource = dt;
            if (dgvUnifiedOperations.Columns["SourceId"] != null) dgvUnifiedOperations.Columns["SourceId"].Visible = false;
        }

        // بيوجّه الكليك على الجدول الموحد للقسم الصح ويحمّل بياناته
        private void DgvUnifiedOperations_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            string opType = dgvUnifiedOperations.Rows[e.RowIndex].Cells["النوع"].Value.ToString();
            int sourceId = Convert.ToInt32(dgvUnifiedOperations.Rows[e.RowIndex].Cells["SourceId"].Value);

            if (opType == "بيع")
            {
                cmbOperationType.SelectedIndex = 0;
                LoadSaleIntoFields(sourceId);
            }
            else if (opType == "مصروف")
            {
                cmbOperationType.SelectedIndex = 1;
                LoadExpenseIntoFields(sourceId);
            }
            else
            {
                cmbOperationType.SelectedIndex = 2;
                LoadMovementIntoFields(sourceId);
            }
        }

        private void LoadExpenseIntoFields(int expenseId)
        {
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("SELECT AccountCode, Amount, ExpenseDate FROM Expenses WHERE ExpenseID = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", expenseId);
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                selectedExpenseID = expenseId;
                                cmbExpenseAccounts.SelectedValue = Convert.ToInt32(reader["AccountCode"]);
                                txtExpenseAmount.Text = reader["Amount"].ToString();
                                selectedExpenseDate = DateTime.Parse(reader["ExpenseDate"].ToString());
                                if (btnSaveExpenseUpdate != null) btnSaveExpenseUpdate.Enabled = false;
                            }
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void LoadMovementIntoFields(int movementId)
        {
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("SELECT MovementType, PaymentMethod, Amount, ReferenceNumber, Description, AccountCode FROM CashMovements WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", movementId);
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                selectedMovementId = movementId;
                                cmbMovementType.Text = reader["MovementType"].ToString();
                                cmbPaymentMethod.Text = reader["PaymentMethod"].ToString();
                                txtMovementAmount.Text = reader["Amount"].ToString();
                                txtMovementReference.Text = reader["ReferenceNumber"]?.ToString();
                                txtMovementDescription.Text = reader["Description"]?.ToString();
                                if (cmbMovementAccount != null)
                                {
                                    if (reader["AccountCode"] != DBNull.Value)
                                        cmbMovementAccount.SelectedValue = Convert.ToInt32(reader["AccountCode"]);
                                    else
                                        cmbMovementAccount.SelectedIndex = -1;
                                }
                                if (btnSaveMovementEdit != null) btnSaveMovementEdit.Enabled = false;
                            }
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void CreateClosuresLogDesign(TabPage page)
        {
            Label lblLogTitle = new Label() { Text = "سجل الأيام المُقفلة بالتفصيل:", Location = new Point(20, 20), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

            dgvClosuresLog = new DataGridView() { Location = new Point(20, 50), Size = new Size(1080, 340), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells, ReadOnly = true, AllowUserToAddRows = false };
            dgvClosuresLog.CellClick += DgvClosuresLog_CellClick;
            dgvClosuresLog.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F);
            dgvClosuresLog.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            dgvClosuresLog.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvClosuresLog.RowTemplate.Height = 38;
            StyleDataGridView(dgvClosuresLog);

            Label lblClosureDetailsTitle = new Label() { Text = "تفاصيل فئات الكاش الفعلي لليوم المحدد (دوس على أي صف فوق لعرض تفاصيله):", Location = new Point(20, 405), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

            dgvClosureDetails = new DataGridView() { Location = new Point(20, 435), Size = new Size(1080, 220), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells, ReadOnly = true, AllowUserToAddRows = false };
            dgvClosureDetails.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F);
            dgvClosureDetails.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            dgvClosureDetails.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvClosureDetails.RowTemplate.Height = 38;
            StyleDataGridView(dgvClosureDetails);

            page.AutoScroll = true;
            page.Controls.AddRange(new Control[] { lblLogTitle, dgvClosuresLog, lblClosureDetailsTitle, dgvClosureDetails });
            LoadClosuresLog();
        }

        private void LoadClosuresLog()
        {
            if (dgvClosuresLog == null) return;

            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] {
                new DataColumn("Id"), new DataColumn("التاريخ"), new DataColumn("الوسيلة"), new DataColumn("رصيد افتتاحي"),
                new DataColumn("إجمالي وارد"), new DataColumn("إجمالي منصرف"),
                new DataColumn("ختامي متوقع"), new DataColumn("ختامي فعلي"), new DataColumn("الفرق"), new DataColumn("وقت الإقفال")
            });

            string query = "SELECT * FROM DailyClosures ORDER BY ClosureDate DESC, PaymentMethod ASC";
            using (SqliteConnection conn = new SqliteConnection(connectionString))
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
            using (SqliteConnection conn = new SqliteConnection(connectionString))
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

        private void CreateStatementsDesign(TabPage page)
        {
            GroupBox gbFilter = new GroupBox() { Text = "اختيار الوسيلة والفترة", Location = new Point(20, 20), Size = new Size(260, 230) };

            Label lblMethod = new Label() { Text = "وسيلة الدفع:", Location = new Point(10, 25), AutoSize = true };
            cmbStatementMethod = new Guna2ComboBox() { Location = new Point(10, 45), Width = 230, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbStatementMethod.Items.AddRange(new string[] { "نقدي", "فوري", "أمان", "سهولة", "فودافون كاش", "إنستاباي" });
            cmbStatementMethod.SelectedIndex = 0;

            Label lblFrom = new Label() { Text = "من تاريخ:", Location = new Point(10, 85), AutoSize = true };
            dtpStatementFrom = new DateTimePicker() { Location = new Point(10, 105), Width = 230, Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddMonths(-1) };

            Label lblTo = new Label() { Text = "إلى تاريخ:", Location = new Point(10, 135), AutoSize = true };
            dtpStatementTo = new DateTimePicker() { Location = new Point(10, 155), Width = 230, Format = DateTimePickerFormat.Short, Value = DateTime.Now };

            Guna2Button btnShowStatement = new Guna2Button() { Text = "عرض كشف الحساب 📋", Location = new Point(10, 190), Width = 230, Height = 32, FillColor = ColorPrimary };
            btnShowStatement.Click += (s, e) => ShowStatement(true);

            gbFilter.Controls.AddRange(new Control[] { lblMethod, cmbStatementMethod, lblFrom, dtpStatementFrom, lblTo, dtpStatementTo, btnShowStatement });

            Guna2Button btnShowAllStatement = new Guna2Button() { Text = "عرض كل الفترات 🔄", Location = new Point(20, 495), Width = 260, Height = 32, FillColor = ColorNeutral, ForeColor = ColorPrimary };
            btnShowAllStatement.Click += (s, e) => ShowStatement(false);

            GroupBox gbSummary = new GroupBox() { Text = "ملخص الفترة المعروضة", Location = new Point(20, 260), Size = new Size(260, 220) };

            Label lblIn = new Label() { Text = "إجمالي الوارد:", Location = new Point(10, 25), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            lblStatementTotalInVal = new Label() { Text = "0.00 ج.م", Location = new Point(10, 45), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ColorSuccess };

            Label lblOut = new Label() { Text = "إجمالي المنصرف:", Location = new Point(10, 75), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            lblStatementTotalOutVal = new Label() { Text = "0.00 ج.م", Location = new Point(10, 95), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ColorDanger };

            Label lblNet = new Label() { Text = "صافي حركة الفترة:", Location = new Point(10, 125), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            lblStatementNetVal = new Label() { Text = "0.00 ج.م", Location = new Point(10, 145), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblCurrentBalance = new Label() { Text = "الرصيد الحالي المسجل فعليًا:", Location = new Point(10, 175), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            lblStatementCurrentBalanceVal = new Label() { Text = "0.00 ج.م", Location = new Point(10, 195), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ColorWarning };

            gbSummary.Controls.AddRange(new Control[] { lblIn, lblStatementTotalInVal, lblOut, lblStatementTotalOutVal, lblNet, lblStatementNetVal, lblCurrentBalance, lblStatementCurrentBalanceVal });

            Label lblNote = new Label()
            {
                Text = "ملحوظة: عمود \"الرصيد بعد الحركة\" بيتحسب من الحركات المعروضة بس، ومش هياخد في اعتباره أي تعديل يدوي للرصيد تم من زرار \"تحديث الرصيد\".",
                Location = new Point(20, 535),
                Size = new Size(260, 90),
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.Gray
            };

            Label lblGridTitle = new Label() { Text = "كشف الحساب بالتفصيل:", Location = new Point(310, 20), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

            dgvStatement = new DataGridView() { Location = new Point(310, 45), Size = new Size(800, 640), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells, ReadOnly = true, AllowUserToAddRows = false };
            dgvStatement.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dgvStatement.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvStatement.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            StyleDataGridView(dgvStatement);

            page.AutoScroll = true;
            page.Controls.AddRange(new Control[] { gbFilter, gbSummary, btnShowAllStatement, lblNote, lblGridTitle, dgvStatement });

            ShowStatement(false);
        }

        // بيعرض كشف حساب الوسيلة المختارة. useDateFilter بتحدد نطبق فلتر التاريخ ولا نعرض كل الفترات
        private void ShowStatement(bool useDateFilter)
        {
            if (cmbStatementMethod == null || cmbStatementMethod.SelectedItem == null) return;
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

            using (SqliteConnection conn = new SqliteConnection(connectionString))
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

        private void CreateAccountsTreeDesign(TabPage page)
        {
            GroupBox gbAccount = new GroupBox() { Text = "إضافة / تعديل حساب", Location = new Point(20, 20), Size = new Size(260, 300) };

            Label lblCode = new Label() { Text = "كود الحساب:", Location = new Point(10, 25), AutoSize = true };
            txtAccountCode = new TextBox() { Location = new Point(10, 45), Width = 230 };

            Label lblName = new Label() { Text = "اسم الحساب:", Location = new Point(10, 80), AutoSize = true };
            txtAccountName = new TextBox() { Location = new Point(10, 100), Width = 230 };

            Guna2Button btnAddAccount = new Guna2Button() { Text = "إضافة حساب جديد ✅", Location = new Point(10, 140), Width = 230, Height = 35, FillColor = ColorSuccess };
            btnAddAccount.Click += BtnAddAccount_Click;

            Guna2Button btnEditAccountMode = new Guna2Button() { Text = "تعديل الحساب المحدد ✏️", Location = new Point(10, 180), Width = 230, Height = 35, FillColor = ColorPrimary };
            btnEditAccountMode.Click += BtnEditAccountMode_Click;

            btnSaveAccountEdit = new Guna2Button() { Text = "حفظ تعديل الحساب 💾", Location = new Point(10, 220), Width = 230, Height = 35, FillColor = ColorWarning, Enabled = false };
            btnSaveAccountEdit.Click += BtnSaveAccountEdit_Click;

            Guna2Button btnDeleteAccount = new Guna2Button() { Text = "حذف الحساب المحدد ❌", Location = new Point(10, 260), Width = 230, Height = 30, FillColor = ColorDanger };
            btnDeleteAccount.Click += BtnDeleteAccount_Click;

            gbAccount.Controls.AddRange(new Control[] { lblCode, txtAccountCode, lblName, txtAccountName, btnAddAccount, btnEditAccountMode, btnSaveAccountEdit, btnDeleteAccount });

            Label lblNote = new Label()
            {
                Text = "ملحوظة: كود الحساب في المحاسبة المصرية بيتقسّم حسب النوع، مثلاً:\n1xxx أصول، 2xxx التزامات، 3xxx حقوق ملكية، 4xxx إيرادات، 5xxx مصروفات.\nحاليًا عندك 5xxx بس (مصروفات) — لو محتاج تسجل قبض زي رأس مال أو قرض، ضيف كود مناسب من الفئة الصح.",
                Location = new Point(20, 330),
                Size = new Size(260, 140),
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.Gray
            };

            Label lblGridTitle = new Label() { Text = "كل الحسابات المسجّلة:", Location = new Point(310, 20), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

            dgvAccountsTree = new DataGridView() { Location = new Point(310, 45), Size = new Size(780, 600), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvAccountsTree.CellClick += DgvAccountsTree_CellClick;
            StyleDataGridView(dgvAccountsTree);

            page.AutoScroll = true;
            page.Controls.AddRange(new Control[] { gbAccount, lblNote, lblGridTitle, dgvAccountsTree });

            LoadAccountsTreeGrid();
        }

        private void LoadAccountsTreeGrid()
        {
            if (dgvAccountsTree == null) return;

            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("كود الحساب"), new DataColumn("اسم الحساب") });

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("SELECT AccountCode, AccountName FROM AccountsTree ORDER BY AccountCode ASC", conn))
                {
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                dt.Rows.Add(reader["AccountCode"], reader["AccountName"]);
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
            dgvAccountsTree.DataSource = dt;
        }

        private void DgvAccountsTree_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvAccountsTree.Rows[e.RowIndex];
            selectedAccountCode = Convert.ToInt32(row.Cells["كود الحساب"].Value);
            txtAccountCode.Text = selectedAccountCode.ToString();
            txtAccountCode.ReadOnly = true;
            txtAccountName.Text = row.Cells["اسم الحساب"].Value.ToString();
            btnSaveAccountEdit.Enabled = false;
        }

        private void BtnAddAccount_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(txtAccountCode.Text, out int code) || string.IsNullOrWhiteSpace(txtAccountName.Text))
            {
                MessageBox.Show("من فضلك أدخل كود حساب رقمي واسم حساب صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                using (SqliteCommand cmdCheck = new SqliteCommand("SELECT COUNT(*) FROM AccountsTree WHERE AccountCode = @Code", conn))
                {
                    cmdCheck.Parameters.AddWithValue("@Code", code);
                    if (Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0)
                    {
                        MessageBox.Show("الكود ده مستخدم بالفعل لحساب تاني.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                using (SqliteCommand cmd = new SqliteCommand("INSERT INTO AccountsTree (AccountCode, AccountName) VALUES (@Code, @Name)", conn))
                {
                    cmd.Parameters.AddWithValue("@Code", code);
                    cmd.Parameters.AddWithValue("@Name", txtAccountName.Text.Trim());
                    try
                    {
                        cmd.ExecuteNonQuery();
                        MessageBox.Show("تم إضافة الحساب بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        ClearAccountInputs();
                        LoadAccountsTreeGrid();
                        LoadAccountsTreeIntoCombo();
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void BtnEditAccountMode_Click(object sender, EventArgs e)
        {
            if (selectedAccountCode == -1)
            {
                MessageBox.Show("من فضلك اختر حساب من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            btnSaveAccountEdit.Enabled = true;
        }

        private void BtnSaveAccountEdit_Click(object sender, EventArgs e)
        {
            if (selectedAccountCode == -1 || string.IsNullOrWhiteSpace(txtAccountName.Text)) return;

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("UPDATE AccountsTree SET AccountName = @Name WHERE AccountCode = @Code", conn))
                {
                    cmd.Parameters.AddWithValue("@Name", txtAccountName.Text.Trim());
                    cmd.Parameters.AddWithValue("@Code", selectedAccountCode);
                    try
                    {
                        conn.Open();
                        cmd.ExecuteNonQuery();
                        MessageBox.Show("تم تعديل اسم الحساب بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        ClearAccountInputs();
                        LoadAccountsTreeGrid();
                        LoadAccountsTreeIntoCombo();
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void BtnDeleteAccount_Click(object sender, EventArgs e)
        {
            if (selectedAccountCode == -1)
            {
                MessageBox.Show("من فضلك اختر حساب من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                int usageCount = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM Expenses WHERE AccountCode = @Code", conn))
                {
                    cmd.Parameters.AddWithValue("@Code", selectedAccountCode);
                    usageCount += Convert.ToInt32(cmd.ExecuteScalar());
                }
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM CashMovements WHERE AccountCode = @Code", conn))
                {
                    cmd.Parameters.AddWithValue("@Code", selectedAccountCode);
                    usageCount += Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (usageCount > 0)
                {
                    MessageBox.Show($"لا يمكن حذف هذا الحساب لأنه مستخدم في {usageCount} حركة/حركات مسجّلة بالفعل.", "غير مسموح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show("هل أنت متأكد من حذف هذا الحساب نهائيًا؟", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                using (SqliteCommand cmd = new SqliteCommand("DELETE FROM AccountsTree WHERE AccountCode = @Code", conn))
                {
                    cmd.Parameters.AddWithValue("@Code", selectedAccountCode);
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم حذف الحساب بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearAccountInputs();
            LoadAccountsTreeGrid();
            LoadAccountsTreeIntoCombo();
        }

        private void ClearAccountInputs()
        {
            selectedAccountCode = -1;
            txtAccountCode.Clear();
            txtAccountCode.ReadOnly = false;
            txtAccountName.Clear();
            btnSaveAccountEdit.Enabled = false;
        }

        private void CreateIncomeStatementDesign(TabPage page)
        {
            GroupBox gbFilter = new GroupBox() { Text = "الفترة الزمنية", Location = new Point(20, 20), Size = new Size(260, 170) };

            Label lblFrom = new Label() { Text = "من تاريخ:", Location = new Point(10, 25), AutoSize = true };
            dtpIncomeFrom = new DateTimePicker() { Location = new Point(10, 45), Width = 230, Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddMonths(-1) };

            Label lblTo = new Label() { Text = "إلى تاريخ:", Location = new Point(10, 85), AutoSize = true };
            dtpIncomeTo = new DateTimePicker() { Location = new Point(10, 105), Width = 230, Format = DateTimePickerFormat.Short, Value = DateTime.Now };

            Guna2Button btnShowIncome = new Guna2Button() { Text = "عرض قائمة الدخل 📈", Location = new Point(10, 135), Width = 230, Height = 35, FillColor = ColorPrimary, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            btnShowIncome.Click += (s, e) => ShowIncomeStatement();

            gbFilter.Controls.AddRange(new Control[] { lblFrom, dtpIncomeFrom, lblTo, dtpIncomeTo, btnShowIncome });

            Label lblNote = new Label()
            {
                Text = "ملحوظة: الإيرادات والمصروفات هنا بتتجمع من جدول المبيعات والمصروفات، وكمان أي حركة قبض/صرف مربوطة بحساب إيرادات (4xxx) أو مصروفات (5xxx).",
                Location = new Point(20, 200),
                Size = new Size(260, 100),
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.Gray
            };

            Label lblGridTitle = new Label() { Text = "قائمة الدخل التفصيلية:", Location = new Point(310, 20), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

            dgvIncomeStatement = new DataGridView() { Location = new Point(310, 45), Size = new Size(780, 660), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvIncomeStatement.DefaultCellStyle.Font = new Font("Segoe UI", 10F);
            StyleDataGridView(dgvIncomeStatement);

            page.AutoScroll = true;
            page.Controls.AddRange(new Control[] { gbFilter, lblNote, lblGridTitle, dgvIncomeStatement });

            ShowIncomeStatement();
        }

        private void ShowIncomeStatement()
        {
            if (dgvIncomeStatement == null) return;

            string fromDateTime = dtpIncomeFrom.Value.ToString("yyyy-MM-dd") + " 00:00:00";
            string toDateTime = dtpIncomeTo.Value.ToString("yyyy-MM-dd") + " 23:59:59";
            string fromDateOnly = dtpIncomeFrom.Value.ToString("yyyy-MM-dd");
            string toDateOnly = dtpIncomeTo.Value.ToString("yyyy-MM-dd");

            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("البند"), new DataColumn("المبلغ") });

            decimal totalRevenue = 0, totalExpenses = 0;

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                decimal salesRevenue = 0, cogs = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Total) AS T, SUM(CostPrice * QuantitySold) AS C FROM Sales WHERE SaleDate BETWEEN @From AND @To", conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDateTime);
                    cmd.Parameters.AddWithValue("@To", toDateTime);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            salesRevenue = reader["T"] != DBNull.Value ? Convert.ToDecimal(reader["T"]) : 0;
                            cogs = reader["C"] != DBNull.Value ? Convert.ToDecimal(reader["C"]) : 0;
                        }
                    }
                }
                dt.Rows.Add("إيرادات مبيعات الموبايلات والإكسسوارات", salesRevenue.ToString("N2"));
                totalRevenue += salesRevenue;

                string revQuery = @"SELECT AC.AccountName, SUM(CM.Amount) AS Total
                    FROM CashMovements CM INNER JOIN AccountsTree AC ON CM.AccountCode = AC.AccountCode
                    WHERE CM.MovementType = 'قبض' AND AC.AccountCode >= 4000 AND AC.AccountCode < 5000
                      AND CM.MovementDate BETWEEN @From AND @To
                    GROUP BY AC.AccountCode, AC.AccountName ORDER BY AC.AccountCode";
                using (SqliteCommand cmd = new SqliteCommand(revQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDateOnly);
                    cmd.Parameters.AddWithValue("@To", toDateOnly);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            decimal amt = Convert.ToDecimal(reader["Total"]);
                            dt.Rows.Add(reader["AccountName"].ToString(), amt.ToString("N2"));
                            totalRevenue += amt;
                        }
                    }
                }

                dt.Rows.Add("إجمالي الإيرادات", totalRevenue.ToString("N2"));
                dt.Rows.Add("", "");
                dt.Rows.Add("تكلفة البضاعة المباعة", cogs.ToString("N2"));
                decimal grossProfit = totalRevenue - cogs;
                dt.Rows.Add("مجمل الربح", grossProfit.ToString("N2"));
                dt.Rows.Add("", "");

                var expenseTotals = new Dictionary<string, decimal>();
                var expenseOrder = new List<(int code, string name)>();

                string expQuery = @"SELECT A.AccountCode, A.AccountName, SUM(E.Amount) AS Total
                    FROM Expenses E INNER JOIN AccountsTree A ON E.AccountCode = A.AccountCode
                    WHERE E.ExpenseDate BETWEEN @From AND @To
                    GROUP BY A.AccountCode, A.AccountName";
                using (SqliteCommand cmd = new SqliteCommand(expQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDateTime);
                    cmd.Parameters.AddWithValue("@To", toDateTime);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int code = Convert.ToInt32(reader["AccountCode"]);
                            string name = reader["AccountName"].ToString();
                            decimal amt = Convert.ToDecimal(reader["Total"]);
                            string key = code + "|" + name;
                            if (!expenseTotals.ContainsKey(key)) { expenseTotals[key] = 0; expenseOrder.Add((code, name)); }
                            expenseTotals[key] += amt;
                        }
                    }
                }

                string movExpQuery = @"SELECT AC.AccountCode, AC.AccountName, SUM(CM.Amount) AS Total
                    FROM CashMovements CM INNER JOIN AccountsTree AC ON CM.AccountCode = AC.AccountCode
                    WHERE CM.MovementType = 'صرف' AND AC.AccountCode >= 5000 AND AC.AccountCode < 6000
                      AND CM.MovementDate BETWEEN @From AND @To
                    GROUP BY AC.AccountCode, AC.AccountName";
                using (SqliteCommand cmd = new SqliteCommand(movExpQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDateOnly);
                    cmd.Parameters.AddWithValue("@To", toDateOnly);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int code = Convert.ToInt32(reader["AccountCode"]);
                            string name = reader["AccountName"].ToString();
                            decimal amt = Convert.ToDecimal(reader["Total"]);
                            string key = code + "|" + name;
                            if (!expenseTotals.ContainsKey(key)) { expenseTotals[key] = 0; expenseOrder.Add((code, name)); }
                            expenseTotals[key] += amt;
                        }
                    }
                }

                foreach (var item in expenseOrder.OrderBy(x => x.code))
                {
                    string key = item.code + "|" + item.name;
                    decimal amt = expenseTotals[key];
                    dt.Rows.Add(item.name, amt.ToString("N2"));
                    totalExpenses += amt;
                }

                dt.Rows.Add("إجمالي المصروفات", totalExpenses.ToString("N2"));
                dt.Rows.Add("", "");
                decimal netProfit = grossProfit - totalExpenses;
                dt.Rows.Add("صافي الربح النهائي", netProfit.ToString("N2"));
            }

            dgvIncomeStatement.DataSource = dt;

            foreach (DataGridViewRow row in dgvIncomeStatement.Rows)
            {
                string label = row.Cells[0].Value?.ToString() ?? "";
                if (label.Contains("إجمالي") || label.Contains("مجمل") || label.Contains("صافي"))
                {
                    row.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
                    row.DefaultCellStyle.BackColor = ColorBackground;
                }
            }
        }

        private static readonly Dictionary<string, int> PaymentMethodAccountCodes = new Dictionary<string, int>
        {
            { "نقدي", 1100 }, { "فوري", 1110 }, { "أمان", 1120 }, { "سهولة", 1130 }, { "فودافون كاش", 1140 }, { "إنستاباي", 1150 }
        };

        private void CreateTrialBalanceDesign(TabPage page)
        {
            Guna2Button btnShowTrial = new Guna2Button() { Text = "عرض ميزان المراجعة ⚖️", Location = new Point(20, 20), Width = 260, Height = 40, FillColor = ColorPrimary, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            btnShowTrial.Click += (s, e) => ShowTrialBalance();

            Label lblNote = new Label()
            {
                Text = "ملحوظة محاسبية مهمة: الميزان ده بيعرض لحظة حالية (دلوقتي)، مش تاريخ معين، ومبني على البيانات المتاحة في النظام فعليًا (مش دفتر أستاذ كامل بقيد مزدوج من أول يوم). لو المدين ما يساويش الدائن بالظبط، الفرق بيعكس حركات لسه مش مربوطة بحساب في شجرة الحسابات.",
                Location = new Point(20, 70),
                Size = new Size(260, 180),
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.Gray
            };

            Label lblGridTitle = new Label() { Text = "ميزان المراجعة - كل الحسابات:", Location = new Point(310, 20), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

            dgvTrialBalance = new DataGridView() { Location = new Point(310, 45), Size = new Size(780, 660), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvTrialBalance.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            StyleDataGridView(dgvTrialBalance);

            page.AutoScroll = true;
            page.Controls.AddRange(new Control[] { btnShowTrial, lblNote, lblGridTitle, dgvTrialBalance });

            ShowTrialBalance();
        }

        private void ShowTrialBalance()
        {
            if (dgvTrialBalance == null) return;

            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("كود الحساب"), new DataColumn("اسم الحساب"), new DataColumn("مدين"), new DataColumn("دائن") });

            decimal totalDebit = 0, totalCredit = 0;

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                var accounts = new List<(int code, string name)>();
                using (SqliteCommand cmd = new SqliteCommand("SELECT AccountCode, AccountName FROM AccountsTree ORDER BY AccountCode", conn))
                {
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            accounts.Add((Convert.ToInt32(reader["AccountCode"]), reader["AccountName"].ToString()));
                    }
                }

                foreach (var acc in accounts)
                {
                    decimal debit = 0, credit = 0;

                    // 1xxx: لو الحساب ده وسيلة دفع معروفة، رصيده بييجي من PaymentMethodBalances مباشرة
                    string matchingMethod = PaymentMethodAccountCodes.FirstOrDefault(x => x.Value == acc.code).Key;
                    if (matchingMethod != null)
                    {
                        using (SqliteCommand cmd = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn))
                        {
                            cmd.Parameters.AddWithValue("@Method", matchingMethod);
                            var res = cmd.ExecuteScalar();
                            debit = res != null ? Convert.ToDecimal(res) : 0;
                        }
                    }
                    else if (acc.code == 1200)
                    {
                        // قيمة المخزون بسعر التكلفة
                        using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Quantity * Price) FROM Products", conn))
                        {
                            var res = cmd.ExecuteScalar();
                            debit = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                        }
                    }
                    else if (acc.code >= 4000 && acc.code < 5000)
                    {
                        // حسابات الإيرادات: إجمالي القبض المرتبط بيها (رصيد دائن)
                        using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE MovementType = 'قبض' AND AccountCode = @Code", conn))
                        {
                            cmd.Parameters.AddWithValue("@Code", acc.code);
                            var res = cmd.ExecuteScalar();
                            credit = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                        }
                    }
                    else if (acc.code >= 5000 && acc.code < 6000)
                    {
                        // حسابات المصروفات: من جدول المصروفات + أي صرف مرتبط (رصيد مدين)
                        using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM Expenses WHERE AccountCode = @Code", conn))
                        {
                            cmd.Parameters.AddWithValue("@Code", acc.code);
                            var res = cmd.ExecuteScalar();
                            debit += (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                        }
                        using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE MovementType = 'صرف' AND AccountCode = @Code", conn))
                        {
                            cmd.Parameters.AddWithValue("@Code", acc.code);
                            var res = cmd.ExecuteScalar();
                            debit += (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                        }
                    }
                    else
                    {
                        // باقي الحسابات (التزامات/حقوق ملكية/أصول تانية): صافي القبض ناقص الصرف المرتبط
                        decimal inAmount = 0, outAmount = 0;
                        using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE MovementType = 'قبض' AND AccountCode = @Code", conn))
                        {
                            cmd.Parameters.AddWithValue("@Code", acc.code);
                            var res = cmd.ExecuteScalar();
                            inAmount = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                        }
                        using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE MovementType = 'صرف' AND AccountCode = @Code", conn))
                        {
                            cmd.Parameters.AddWithValue("@Code", acc.code);
                            var res = cmd.ExecuteScalar();
                            outAmount = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                        }
                        decimal net = inAmount - outAmount;
                        if (net >= 0) credit = net; else debit = Math.Abs(net);
                    }

                    if (debit == 0 && credit == 0) continue; // نتخطى الحسابات اللي مفيهاش حركة خالص

                    dt.Rows.Add(acc.code, acc.name, debit == 0 ? "" : debit.ToString("N2"), credit == 0 ? "" : credit.ToString("N2"));
                    totalDebit += debit;
                    totalCredit += credit;
                }

                // سطر إضافي: مبيعات مش مربوطة بكود حساب معين في جدول المبيعات نفسه (تُحسب هنا لو حساب المبيعات 4100 مش موجود أو الشجرة قديمة)
                decimal allSales = 0, allCogs = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Total) AS T, SUM(CostPrice * QuantitySold) AS C FROM Sales", conn))
                {
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            allSales = reader["T"] != DBNull.Value ? Convert.ToDecimal(reader["T"]) : 0;
                            allCogs = reader["C"] != DBNull.Value ? Convert.ToDecimal(reader["C"]) : 0;
                        }
                    }
                }
                if (allSales > 0)
                {
                    dt.Rows.Add("", "إيرادات المبيعات (من جدول المبيعات مباشرة)", "", allSales.ToString("N2"));
                    totalCredit += allSales;
                }
                if (allCogs > 0)
                {
                    dt.Rows.Add("", "تكلفة البضاعة المباعة (تقديري)", allCogs.ToString("N2"), "");
                    totalDebit += allCogs;
                }
            }

            dt.Rows.Add("", "", "", "");
            dt.Rows.Add("", "الإجمالي", totalDebit.ToString("N2"), totalCredit.ToString("N2"));

            dgvTrialBalance.DataSource = dt;

            foreach (DataGridViewRow row in dgvTrialBalance.Rows)
            {
                string label = row.Cells[1].Value?.ToString() ?? "";
                if (label == "الإجمالي")
                {
                    row.DefaultCellStyle.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
                    row.DefaultCellStyle.BackColor = ColorBackground;
                }
            }
        }

        private void StyleDataGridView(DataGridView dgv)
        {
            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = ColorPrimary;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            dgv.ColumnHeadersHeight = 40;
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dgv.DefaultCellStyle.BackColor = Color.White;
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(200, 210, 230);
            dgv.DefaultCellStyle.SelectionForeColor = ColorPrimary;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = ColorBackground;
            dgv.GridColor = Color.FromArgb(230, 230, 230);
            dgv.RowTemplate.Height = 32;
            dgv.BorderStyle = BorderStyle.None;
            dgv.BackgroundColor = Color.White;
            dgv.RowHeadersVisible = true;
            dgv.RowHeadersWidth = 40;
            dgv.RowHeadersDefaultCellStyle.BackColor = ColorPrimary;
            dgv.RowHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.RowHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            dgv.RowHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.CellPainting += (s, e) =>
            {
                if (e.ColumnIndex == -1)
                {
                    e.PaintBackground(e.ClipBounds, true);
                    var rowNumber = e.RowIndex == -1 ? "م" : (e.RowIndex + 1).ToString();
                    TextRenderer.DrawText(e.Graphics, rowNumber, new Font("Segoe UI", 9, FontStyle.Bold), e.CellBounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    e.Handled = true;
                }
            };
        }
        private Guna2Button btnAddMovement;
        private Guna2Button btnSaveMovementEdit;
        private int selectedMovementId = -1;
        private Guna2ComboBox cmbMovementType, cmbPaymentMethod;
        private TextBox txtMovementAmount, txtMovementDescription, txtMovementReference;
        private DataGridView dgvCashMovements;
        private Label lblMethodBalance;
        private TextBox txtNewBalance;
        private Guna2Button btnSetBalance;
        private Guna2Button btnEditMovement, btnCancelMovement;

        // أدوات إقفال اليوم
        private Guna2Button btnCloseDay;
        private Label lblOpeningBalanceVal, lblExpectedClosingVal;
        private DataGridView dgvClosureSummary;

        // أدوات سجل إقفال الأيام
        private DataGridView dgvClosuresLog;
        private DataGridView dgvClosureDetails;

        // أدوات شاشة العمليات الموحدة (بيع / مصروف / حركة قبض وصرف مع بعض)
        private Guna2ComboBox cmbOperationType;
        private Panel pnlSaleOps, pnlExpenseOps, pnlMovementOps;
        private DataGridView dgvUnifiedOperations;

        // أدوات كشف حساب الوسائل
        private Guna2ComboBox cmbStatementMethod;
        private DateTimePicker dtpStatementFrom, dtpStatementTo;
        private DataGridView dgvStatement;
        private Label lblStatementTotalInVal, lblStatementTotalOutVal, lblStatementNetVal, lblStatementCurrentBalanceVal;

        // أدوات شجرة الحسابات
        private TextBox txtAccountCode, txtAccountName;
        private DataGridView dgvAccountsTree;
        private Guna2Button btnSaveAccountEdit;
        private int selectedAccountCode = -1;
        private Guna2ComboBox cmbMovementAccount;

        // أدوات قائمة الدخل
        private DateTimePicker dtpIncomeFrom, dtpIncomeTo;
        private DataGridView dgvIncomeStatement;

        // أدوات ميزان المراجعة
        private DataGridView dgvTrialBalance;

        private void CreateCashMovementsDesign(Control page)
        {
            Label lblType = new Label() { Text = "نوع الحركة:", Location = new Point(20, 30), AutoSize = true };
            cmbMovementType = new Guna2ComboBox() { Location = new Point(130, 27), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMovementType.Items.AddRange(new string[] { "قبض", "صرف" });

            Label lblMethod = new Label() { Text = "وسيلة الدفع:", Location = new Point(20, 70), AutoSize = true };
            cmbPaymentMethod = new Guna2ComboBox() { Location = new Point(130, 67), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPaymentMethod.Items.AddRange(new string[] { "نقدي", "فوري", "أمان", "سهولة", "فودافون كاش", "إنستاباي" });
            cmbPaymentMethod.SelectedIndexChanged += CmbPaymentMethod_SelectedIndexChanged;

            Label lblAmount = new Label() { Text = "المبلغ:", Location = new Point(20, 110), AutoSize = true };
            txtMovementAmount = new TextBox() { Location = new Point(130, 107), Width = 150 };

            Label lblRef = new Label() { Text = "رقم مرجعي (اختياري):", Location = new Point(20, 150), AutoSize = true };
            txtMovementReference = new TextBox() { Location = new Point(130, 147), Width = 150 };

            Label lblDesc = new Label() { Text = "الوصف:", Location = new Point(20, 190), AutoSize = true };
            txtMovementDescription = new TextBox() { Location = new Point(130, 187), Width = 150, Height = 60, Multiline = true };

            btnAddMovement = new Guna2Button() { Text = "تسجيل الحركة ✅", Location = new Point(130, 260), Width = 150, Height = 40, FillColor = ColorSuccess };
            btnAddMovement.Click += BtnAddMovement_Click;
            btnEditMovement = new Guna2Button() { Text = "تعديل الحركة المحددة ✏️", Location = new Point(130, 310), Width = 150, Height = 35, FillColor = ColorPrimary };
            btnEditMovement.Click += BtnEditMovement_Click;

            btnSaveMovementEdit = new Guna2Button() { Text = "حفظ تعديل الحركة 💾", Location = new Point(130, 350), Width = 150, Height = 35, FillColor = ColorWarning, Enabled = false };
            btnSaveMovementEdit.Click += BtnSaveMovementEdit_Click;

            btnCancelMovement = new Guna2Button() { Text = "إلغاء الحركة المحددة ❌", Location = new Point(130, 395), Width = 150, Height = 35, FillColor = ColorDanger };
            btnCancelMovement.Click += BtnCancelMovement_Click;

            lblMethodBalance = new Label() { Text = "الرصيد الحالي: --", Location = new Point(20, 320), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblMovementAccount = new Label() { Text = "الحساب المرتبط (اختياري):", Location = new Point(20, 425), AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            cmbMovementAccount = new Guna2ComboBox() { Location = new Point(20, 445), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };

            Label lblSetBalance = new Label() { Text = "تظبيط الرصيد يدويًا (مرة واحدة للبدء):", Location = new Point(20, 485), AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            txtNewBalance = new TextBox() { Location = new Point(20, 505), Width = 150 };
            btnSetBalance = new Guna2Button() { Text = "تحديث الرصيد 🔧", Location = new Point(20, 535), Width = 150, Height = 35, FillColor = ColorNeutral, ForeColor = ColorPrimary };
            btnSetBalance.Click += BtnSetBalance_Click;

            dgvCashMovements = new DataGridView() { Location = new Point(310, 27), Size = new Size(800, 600), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells, ReadOnly = true, AllowUserToAddRows = false };
            StyleDataGridView(dgvCashMovements);

            if (page is ScrollableControl scrollablePage) scrollablePage.AutoScroll = true;
            page.Controls.AddRange(new Control[] { lblType, cmbMovementType, lblMethod, cmbPaymentMethod, lblAmount, txtMovementAmount, lblRef, txtMovementReference, lblDesc, txtMovementDescription, btnAddMovement, btnEditMovement, btnSaveMovementEdit, btnCancelMovement, lblMethodBalance, lblMovementAccount, cmbMovementAccount, lblSetBalance, txtNewBalance, btnSetBalance, dgvCashMovements });
            LoadCashMovements();
        }

        private void BtnSetBalance_Click(object sender, EventArgs e)
        {
            if (cmbPaymentMethod.SelectedItem == null)
            {
                MessageBox.Show("من فضلك اختر وسيلة الدفع اللي عايز تظبط رصيدها الأول.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!decimal.TryParse(txtNewBalance.Text, out decimal newBalance) || newBalance < 0)
            {
                MessageBox.Show("من فضلك أدخل رصيد صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string method = cmbPaymentMethod.SelectedItem.ToString();

            if (MessageBox.Show($"هل أنت متأكد من تحديث رصيد \"{method}\" ليصبح {newBalance:N2} ج.م؟\nده تعديل مباشر للرصيد المسجل، مش حركة قبض أو صرف.", "تأكيد التحديث", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = @Balance WHERE PaymentMethod = @Method", conn))
                {
                    cmd.Parameters.AddWithValue("@Balance", newBalance);
                    cmd.Parameters.AddWithValue("@Method", method);
                    try
                    {
                        conn.Open();
                        cmd.ExecuteNonQuery();
                        MessageBox.Show("تم تحديث الرصيد بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        txtNewBalance.Clear();
                        CmbPaymentMethod_SelectedIndexChanged(null, EventArgs.Empty);
                        RefreshClosureSummary();
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        // بيديك تعدّل رصيد وسيلة معينة بدبل كليك على الجدول في تاب التقارير
        // ملحوظة: التعديل ده بيأثر بس على "الرصيد الافتتاحي" لو مفيش إقفالات سابقة للوسيلة دي؛
        // لو فيه إقفالات قبل كده، الرصيد الافتتاحي بياخد تلقائي من آخر إقفال فعلي مش من هنا
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

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = @Balance WHERE PaymentMethod = @Method", conn))
                {
                    cmd.Parameters.AddWithValue("@Balance", newBalance);
                    cmd.Parameters.AddWithValue("@Method", method);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show($"تم تحديث رصيد \"{method}\" بنجاح.\n\nملحوظة: لو الوسيلة دي كان ليها إقفالات قبل كده، الرصيد الافتتاحي هيفضل ياخد من آخر إقفال فعلي مش من هنا.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshClosureSummary();
        }

        // بوب-أب صغير لإدخال قيمة نصية (بديل عن InputBox اللي مش متاح في WinForms مباشرة)
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

        private void BtnAddMovement_Click(object sender, EventArgs e)
        {
            if (IsTodayClosed())
            {
                MessageBox.Show("تم إقفال اليوم بالفعل، لا يمكن تسجيل حركات جديدة.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cmbMovementType.SelectedItem == null || cmbPaymentMethod.SelectedItem == null || !decimal.TryParse(txtMovementAmount.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("من فضلك اختر نوع الحركة ووسيلة الدفع وأدخل مبلغ صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string type = cmbMovementType.SelectedItem.ToString();
            string method = cmbPaymentMethod.SelectedItem.ToString();

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                decimal currentBalance = 0;
                string balanceQuery = "SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method";
                using (SqliteCommand cmdBalance = new SqliteCommand(balanceQuery, conn))
                {
                    cmdBalance.Parameters.AddWithValue("@Method", method);
                    var result = cmdBalance.ExecuteScalar();
                    if (result != null) currentBalance = Convert.ToDecimal(result);
                }

                if (type == "صرف" && amount > currentBalance)
                {
                    MessageBox.Show($"الرصيد الحالي في \"{method}\" هو {currentBalance} فقط، لا يمكن صرف مبلغ أكبر منه.", "رصيد غير كافٍ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                object accountCodeParam = (cmbMovementAccount != null && cmbMovementAccount.SelectedValue != null) ? (object)Convert.ToInt32(cmbMovementAccount.SelectedValue) : DBNull.Value;

                string insertQuery = "INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode) VALUES (@Date, @Type, @Method, @Amount, @Ref, @Desc, @CreatedAt, @AccountCode)";
                using (SqliteCommand cmdInsert = new SqliteCommand(insertQuery, conn))
                {
                    cmdInsert.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                    cmdInsert.Parameters.AddWithValue("@Type", type);
                    cmdInsert.Parameters.AddWithValue("@Method", method);
                    cmdInsert.Parameters.AddWithValue("@Amount", amount);
                    cmdInsert.Parameters.AddWithValue("@Ref", txtMovementReference.Text);
                    cmdInsert.Parameters.AddWithValue("@Desc", txtMovementDescription.Text);
                    cmdInsert.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmdInsert.Parameters.AddWithValue("@AccountCode", accountCodeParam);
                    cmdInsert.ExecuteNonQuery();
                }

                decimal newBalance = type == "قبض" ? currentBalance + amount : currentBalance - amount;
                string updateQuery = "UPDATE PaymentMethodBalances SET CurrentBalance = @NewBalance WHERE PaymentMethod = @Method";
                using (SqliteCommand cmdUpdate = new SqliteCommand(updateQuery, conn))
                {
                    cmdUpdate.Parameters.AddWithValue("@NewBalance", newBalance);
                    cmdUpdate.Parameters.AddWithValue("@Method", method);
                    cmdUpdate.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم تسجيل الحركة بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            txtMovementAmount.Clear();
            txtMovementReference.Clear();
            txtMovementDescription.Clear();
            if (cmbMovementAccount != null) cmbMovementAccount.SelectedIndex = -1;
            LoadCashMovements();
            CmbPaymentMethod_SelectedIndexChanged(null, EventArgs.Empty);
            RefreshClosureSummary();
        }
        private void LoadCashMovements()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("Id"), new DataColumn("النوع"), new DataColumn("الوسيلة"), new DataColumn("المبلغ"), new DataColumn("المرجع"), new DataColumn("الوصف"), new DataColumn("التاريخ والوقت") });

            string query = "SELECT Id, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt FROM CashMovements";
            using (SqliteConnection conn = new SqliteConnection(connectionString))
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
                                dt.Rows.Add(reader["Id"], reader["MovementType"], reader["PaymentMethod"], reader["Amount"], reader["ReferenceNumber"], reader["Description"], reader["CreatedAt"]);
                            }
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
            dgvCashMovements.DataSource = dt;
            if (dgvCashMovements.Columns["Id"] != null) dgvCashMovements.Columns["Id"].Visible = false;
        }

        private void CmbPaymentMethod_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbPaymentMethod.SelectedItem == null) return;

            string method = cmbPaymentMethod.SelectedItem.ToString();
            decimal balance = 0;

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                string query = "SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method";
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Method", method);
                    conn.Open();
                    var result = cmd.ExecuteScalar();
                    if (result != null) balance = Convert.ToDecimal(result);
                }
            }

            lblMethodBalance.Text = $"الرصيد الحالي في \"{method}\": {balance} جنيه";
        }
        private Label lblDashSalesVal, lblDashProfitVal, lblDashExpensesVal, lblDashInventoryVal, lblDashLowStockVal, lblDashTopProductVal;
        private ListBox lstDashAlerts;
        private DataGridView dgvDashInvoices;

        private GroupBox MakeDashCard(string title, Point loc, out Label valueLabel, Color color)
        {
            GroupBox gb = new GroupBox() { Text = title, Location = loc, Size = new Size(255, 90), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            Label lbl = new Label() { Text = "...", Location = new Point(10, 30), AutoSize = true, Font = new Font("Segoe UI", 15, FontStyle.Bold), ForeColor = color };
            gb.Controls.Add(lbl);
            valueLabel = lbl;
            return gb;
        }

        private void CreateDashboardDesign(TabPage page)
        {
            Label lblWelcome = new Label() { Text = "🏠 لوحة التحكم الذكية", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = ColorPrimary };

            Guna2Button btnRefreshDash = new Guna2Button() { Text = "تحديث 🔄", Location = new Point(1000, 15), Width = 100, Height = 32, FillColor = ColorPrimary };
            btnRefreshDash.Click += (s, e) => LoadDashboardData();

            Guna2Button btnShowShortcuts = new Guna2Button() { Text = "⌨️ الاختصارات (F1)", Location = new Point(860, 15), Width = 130, Height = 32, FillColor = ColorNeutral, ForeColor = ColorPrimary };
            btnShowShortcuts.Click += (s, e) => ShowShortcutsHelp();

            Guna2Button btnUniversalSearch = new Guna2Button() { Text = "🔍 بحث شامل (Ctrl+F)", Location = new Point(700, 15), Width = 155, Height = 32, FillColor = ColorPrimary };
            btnUniversalSearch.Click += (s, e) => ShowUniversalSearchDialog();

            GroupBox card1 = MakeDashCard("💰 مبيعات اليوم", new Point(20, 60), out lblDashSalesVal, ColorPrimary);
            GroupBox card2 = MakeDashCard("📈 صافي الربح اليوم", new Point(285, 60), out lblDashProfitVal, ColorSuccess);
            GroupBox card3 = MakeDashCard("💸 مصروفات اليوم", new Point(550, 60), out lblDashExpensesVal, ColorDanger);
            GroupBox card4 = MakeDashCard("📦 قيمة المخزون الحالي", new Point(815, 60), out lblDashInventoryVal, ColorPrimary);

            GroupBox card5 = MakeDashCard("📱 أصناف نفدت من المخزون", new Point(20, 160), out lblDashLowStockVal, ColorDanger);
            GroupBox card6 = MakeDashCard("🏆 أكثر منتج مبيعًا", new Point(285, 160), out lblDashTopProductVal, ColorWarning);

            Label lblAlertsTitle = new Label() { Text = "⚠️ تنبيهات:", Location = new Point(550, 165), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            lstDashAlerts = new ListBox() { Location = new Point(550, 190), Size = new Size(520, 145), Font = new Font("Segoe UI", 9F) };

            Label lblInvoicesTitle = new Label() { Text = "🛒 آخر 10 فواتير:", Location = new Point(20, 265), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            dgvDashInvoices = new DataGridView() { Location = new Point(20, 290), Size = new Size(1080, 380), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            StyleDataGridView(dgvDashInvoices);

            page.AutoScroll = true;
            page.Controls.AddRange(new Control[] { lblWelcome, btnRefreshDash, btnShowShortcuts, btnUniversalSearch, card1, card2, card3, card4, card5, card6, lblAlertsTitle, lstDashAlerts, lblInvoicesTitle, dgvDashInvoices });

            LoadDashboardData();
        }

        private void LoadDashboardData()
        {
            if (lblDashSalesVal == null) return;

            string today = DateTime.Now.ToString("yyyy-MM-dd");

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                decimal todaySales = 0, todayCogs = 0, todayExpenses = 0, inventoryValue = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Total) AS T, SUM(CostPrice * QuantitySold) AS C FROM Sales WHERE SaleDate LIKE @Today", conn))
                {
                    cmd.Parameters.AddWithValue("@Today", today + "%");
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            todaySales = reader["T"] != DBNull.Value ? Convert.ToDecimal(reader["T"]) : 0;
                            todayCogs = reader["C"] != DBNull.Value ? Convert.ToDecimal(reader["C"]) : 0;
                        }
                    }
                }
                using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM Expenses WHERE ExpenseDate LIKE @Today", conn))
                {
                    cmd.Parameters.AddWithValue("@Today", today + "%");
                    var res = cmd.ExecuteScalar();
                    todayExpenses = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                }
                using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Quantity * Price) FROM Products", conn))
                {
                    var res = cmd.ExecuteScalar();
                    inventoryValue = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                }

                decimal todayProfit = todaySales - todayCogs - todayExpenses;
                lblDashSalesVal.Text = todaySales.ToString("N2") + " ج.م";
                lblDashProfitVal.Text = todayProfit.ToString("N2") + " ج.م";
                lblDashExpensesVal.Text = todayExpenses.ToString("N2") + " ج.م";
                lblDashInventoryVal.Text = inventoryValue.ToString("N2") + " ج.م";

                int lowStockCount = 0;
                var lowStockNames = new List<string>();
                using (SqliteCommand cmd = new SqliteCommand("SELECT ProductName FROM Products WHERE Quantity <= 0", conn))
                {
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read()) { lowStockCount++; lowStockNames.Add(reader["ProductName"].ToString()); }
                    }
                }
                lblDashLowStockVal.Text = lowStockCount.ToString() + " صنف";

                string topProduct = "لا يوجد بيانات";
                using (SqliteCommand cmd = new SqliteCommand("SELECT ProductName, SUM(QuantitySold) AS TotalQty FROM Sales GROUP BY ProductName ORDER BY TotalQty DESC LIMIT 1", conn))
                {
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read()) topProduct = $"{reader["ProductName"]} ({reader["TotalQty"]} قطعة)";
                    }
                }
                lblDashTopProductVal.Text = topProduct;
                lblDashTopProductVal.Font = new Font("Segoe UI", 10, FontStyle.Bold);

                lstDashAlerts.Items.Clear();
                if (lowStockCount > 0)
                    lstDashAlerts.Items.Add($"⚠️ {lowStockCount} صنف نفد من المخزون: {string.Join("، ", lowStockNames.Take(5))}" + (lowStockNames.Count > 5 ? " ..." : ""));

                using (SqliteCommand cmd = new SqliteCommand(@"SELECT C.CustomerName, SUM(S.Total) - COALESCE((SELECT SUM(CM.Amount) FROM CashMovements CM WHERE CM.CustomerId = C.CustomerId AND CM.MovementType = 'قبض'), 0) AS Remaining
                    FROM Sales S INNER JOIN Customers C ON S.CustomerId = C.CustomerId
                    WHERE S.PaymentType = 'Credit'
                    GROUP BY C.CustomerId HAVING Remaining > 0", conn))
                {
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            decimal remaining = Convert.ToDecimal(reader["Remaining"]);
                            if (remaining > 0)
                                lstDashAlerts.Items.Add($"💳 العميل \"{reader["CustomerName"]}\" عليه دين متبقي {remaining:N2} ج.م");
                        }
                    }
                }

                if (!IsTodayClosed() && DateTime.Now.Hour >= 22)
                    lstDashAlerts.Items.Add("🔒 اليوم لسه مقفول، متنساش تعمل إقفال اليوم قبل ما تسيب المحل");

                if (lstDashAlerts.Items.Count == 0)
                    lstDashAlerts.Items.Add("✅ مفيش تنبيهات دلوقتي، كل حاجة تمام");

                DataTable dtInvoices = new DataTable();
                dtInvoices.Columns.AddRange(new DataColumn[] { new DataColumn("رقم البيع"), new DataColumn("المنتج"), new DataColumn("الكمية"), new DataColumn("الإجمالي"), new DataColumn("التاريخ والوقت") });
                using (SqliteCommand cmd = new SqliteCommand("SELECT SaleID, ProductName, QuantitySold, Total, SaleDate FROM Sales ORDER BY SaleID DESC LIMIT 10", conn))
                {
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            dtInvoices.Rows.Add(reader["SaleID"], reader["ProductName"], reader["QuantitySold"], reader["Total"], reader["SaleDate"]);
                    }
                }
                dgvDashInvoices.DataSource = dtInvoices;
            }
        }

        private void CreateInventoryDesign(TabPage page)
        {
            Label lblBarcode = new Label() { Text = "باركود المنتج:", Location = new Point(20, 30), AutoSize = true };
            txtBarcode = new TextBox() { Location = new Point(130, 27), Width = 150 };

            Label lblProductName = new Label() { Text = "اسم المنتج:", Location = new Point(20, 70), AutoSize = true };
            txtProductName = new TextBox() { Location = new Point(130, 67), Width = 150 };

            lblCostPrice = new Label() { Text = "سعر الشراء (التكلفة):", Location = new Point(20, 110), AutoSize = true };
            txtCostPrice = new TextBox() { Location = new Point(130, 107), Width = 150 };

            Label lblSalePrice = new Label() { Text = "سعر البيع للجمهور:", Location = new Point(20, 150), AutoSize = true };
            txtSalePrice = new TextBox() { Location = new Point(130, 147), Width = 150 };

            Label lblQuantity = new Label() { Text = "الكمية:", Location = new Point(20, 190), AutoSize = true };
            txtQuantity = new TextBox() { Location = new Point(130, 187), Width = 150 };

            chkIsSerialized = new CheckBox() { Text = "منتج بسيريال/IMEI (موبايل)", Location = new Point(20, 220), AutoSize = true };

            btnAddProduct = new Guna2Button() { Text = "إضافة منتج جديد", Location = new Point(130, 255), Width = 150, Height = 35, FillColor = ColorSuccess };
            btnAddProduct.Click += btnAddProduct_Click;

            btnEditMode = new Guna2Button() { Text = "تعديل البند المحدّد", Location = new Point(130, 300), Width = 150, Height = 35, FillColor = ColorPrimary };
            btnEditMode.Click += btnEditMode_Click;

            btnSaveUpdate = new Guna2Button() { Text = "حفظ التعديلات 💾", Location = new Point(130, 345), Width = 150, Height = 40, FillColor = ColorWarning, Font = new Font("Segoe UI", 9, FontStyle.Bold), Enabled = false };
            btnSaveUpdate.Click += btnSaveUpdate_Click;

            btnDeleteProduct = new Guna2Button() { Text = "حذف المنتج المحدد", Location = new Point(130, 395), Width = 150, Height = 35, FillColor = ColorDanger };
            btnDeleteProduct.Click += btnDeleteProduct_Click;

            btnClear = new Guna2Button() { Text = "تفريغ الخانات", Location = new Point(130, 440), Width = 150, Height = 30, FillColor = ColorNeutral, ForeColor = ColorPrimary };
            btnClear.Click += (s, e) => ClearInputs();

            dgvProducts = new DataGridView() { Location = new Point(310, 27), Size = new Size(800, 620), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvProducts.CellClick += dgvProducts_CellClick;

            StyleDataGridView(dgvProducts);
            page.Controls.AddRange(new Control[] { lblBarcode, txtBarcode, lblProductName, txtProductName, lblCostPrice, txtCostPrice, lblSalePrice, txtSalePrice, lblQuantity, txtQuantity, chkIsSerialized, btnAddProduct, btnEditMode, btnSaveUpdate, btnDeleteProduct, btnClear, dgvProducts });
        }

        private int selectedSaleId = -1;
        private Guna2Button btnEditSaleMode, btnSaveSaleEdit, btnCancelSale;

        private void CreatePOSDesign(Control page)
        {
            Label lblBarcode = new Label() { Text = "مسح الباركود (Enter):", Location = new Point(20, 30), AutoSize = true };
            txtSaleBarcode = new TextBox() { Location = new Point(130, 27), Width = 150 };
            txtSaleBarcode.KeyDown += TxtSaleBarcode_KeyDown;

            Label lblName = new Label() { Text = "اسم المنتج:", Location = new Point(20, 70), AutoSize = true };
            txtSaleName = new TextBox() { Location = new Point(130, 67), Width = 150, ReadOnly = true };

            Label lblPrice = new Label() { Text = "سعر البيع:", Location = new Point(20, 110), AutoSize = true };
            txtCustomerPrice = new TextBox() { Location = new Point(130, 107), Width = 150, ReadOnly = true };

            Label lblQty = new Label() { Text = "الكمية المطلوبة:", Location = new Point(20, 150), AutoSize = true };
            txtSaleQty = new TextBox() { Location = new Point(130, 147), Width = 150, Text = "1" };
            txtSaleQty.TextChanged += TxtSaleQty_TextChanged;

            lblSaleImei = new Label() { Text = "اختار الجهاز (IMEI):", Location = new Point(20, 190), AutoSize = true, Visible = false };
            cmbSaleImei = new Guna2ComboBox() { Location = new Point(130, 187), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, Visible = false };

            Label lblTotal = new Label() { Text = "إجمالي الحساب:", Location = new Point(20, 230), AutoSize = true };
            txtSaleTotal = new TextBox() { Location = new Point(130, 227), Width = 150, ReadOnly = true, BackColor = Color.Yellow, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

            Label lblPaymentType = new Label() { Text = "نوع البيع:", Location = new Point(20, 265), AutoSize = true };
            cmbSalePaymentType = new Guna2ComboBox() { Location = new Point(130, 262), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbSalePaymentType.Items.AddRange(new string[] { "كاش", "آجل" });
            cmbSalePaymentType.SelectedIndex = 0;
            cmbSalePaymentType.SelectedIndexChanged += CmbSalePaymentType_SelectedIndexChanged;

            Label lblSaleCustomer = new Label() { Text = "العميل (لازم للآجل):", Location = new Point(20, 305), AutoSize = true };
            cmbSaleCustomer = new Guna2ComboBox() { Location = new Point(130, 302), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };

            btnAddToBill = new Guna2Button() { Text = "إتمام عملية البيع 🛒", Location = new Point(130, 345), Width = 150, Height = 40, FillColor = ColorWarning }; btnAddToBill.Click += BtnAddToBill_Click;

            btnPrintInvoice = new Guna2Button() { Text = "طباعة آخر فاتورة 🖨️", Location = new Point(130, 400), Width = 150, Height = 40, FillColor = ColorNeutral, ForeColor = ColorPrimary }; btnPrintInvoice.Click += BtnPrintInvoice_Click;

            Label lblInvoicePaperSize = new Label() { Text = "مقاس الطباعة:", Location = new Point(20, 447), AutoSize = true };
            cmbInvoicePaperSize = new Guna2ComboBox() { Location = new Point(20, 467), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbInvoicePaperSize.Items.AddRange(new string[] { "80 مم (طابعة حرارية)", "58 مم (طابعة حرارية)", "A4 (طابعة عادية)" });
            cmbInvoicePaperSize.SelectedIndex = 0;

            Guna2Button btnEditSaleModeBtn = new Guna2Button() { Text = "تعديل البيع المحدد ✏️", Location = new Point(130, 505), Width = 150, Height = 35, FillColor = ColorPrimary };
            btnEditSaleModeBtn.Click += BtnEditSaleMode_Click;
            btnEditSaleMode = btnEditSaleModeBtn;

            btnSaveSaleEdit = new Guna2Button() { Text = "حفظ تعديل البيع 💾", Location = new Point(130, 550), Width = 150, Height = 35, FillColor = ColorWarning, Enabled = false };
            btnSaveSaleEdit.Click += BtnSaveSaleEdit_Click;

            btnCancelSale = new Guna2Button() { Text = "إلغاء البيع المحدد ❌", Location = new Point(130, 595), Width = 150, Height = 35, FillColor = ColorDanger };
            btnCancelSale.Click += BtnCancelSale_Click;

            dgvSales = new DataGridView() { Location = new Point(310, 27), Size = new Size(800, 620), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvSales.CellClick += DgvSales_CellClick;

            StyleDataGridView(dgvSales);
            if (page is ScrollableControl scrollablePOS) scrollablePOS.AutoScroll = true;
            page.Controls.AddRange(new Control[] { lblBarcode, txtSaleBarcode, lblName, txtSaleName, lblPrice, txtCustomerPrice, lblQty, txtSaleQty, lblSaleImei, cmbSaleImei, lblTotal, txtSaleTotal, lblPaymentType, cmbSalePaymentType, lblSaleCustomer, cmbSaleCustomer, btnAddToBill, btnPrintInvoice, lblInvoicePaperSize, cmbInvoicePaperSize, btnEditSaleMode, btnSaveSaleEdit, btnCancelSale, dgvSales });
            LoadCustomersIntoCombo();
        }

        private void CmbSalePaymentType_SelectedIndexChanged(object sender, EventArgs e)
        {
            cmbSaleCustomer.Enabled = cmbSalePaymentType.SelectedItem?.ToString() == "آجل";
        }

        private void LoadCustomersIntoCombo()
        {
            if (cmbSaleCustomer == null) return;
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("CustomerId", typeof(int)), new DataColumn("CustomerName") });

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("SELECT CustomerId, CustomerName FROM Customers ORDER BY CustomerName", conn))
                {
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                dt.Rows.Add(Convert.ToInt32(reader["CustomerId"]), reader["CustomerName"].ToString());
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
            cmbSaleCustomer.DataSource = dt;
            cmbSaleCustomer.DisplayMember = "CustomerName";
            cmbSaleCustomer.ValueMember = "CustomerId";
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

        // بيحمّل بيانات البيع المحدد في الخانات لما تدوس على صف في الجدول، تمهيدًا للتعديل أو الإلغاء
        private void DgvSales_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            int saleId = Convert.ToInt32(dgvSales.Rows[e.RowIndex].Cells["رقم البيع"].Value);
            LoadSaleIntoFields(saleId);
        }

        private void LoadSaleIntoFields(int saleId)
        {
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("SELECT Barcode, ProductName, Price, QuantitySold, SaleDate FROM Sales WHERE SaleID = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", saleId);
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                selectedSaleId = saleId;
                                txtSaleBarcode.Text = reader["Barcode"].ToString();
                                txtSaleName.Text = reader["ProductName"].ToString();
                                txtCustomerPrice.Text = reader["Price"].ToString();
                                txtSaleQty.Text = reader["QuantitySold"].ToString();
                                txtSaleBarcode.ReadOnly = true;
                                if (btnSaveSaleEdit != null) btnSaveSaleEdit.Enabled = false;
                            }
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
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

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                string barcode = null; int oldQty = 0; decimal price = 0; string saleDateStr = null;
                using (SqliteCommand cmd = new SqliteCommand("SELECT Barcode, QuantitySold, Price, SaleDate FROM Sales WHERE SaleID = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", selectedSaleId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            MessageBox.Show("لم يتم العثور على عملية البيع.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        barcode = reader["Barcode"].ToString();
                        oldQty = Convert.ToInt32(reader["QuantitySold"]);
                        price = Convert.ToDecimal(reader["Price"]);
                        saleDateStr = reader["SaleDate"].ToString();
                    }
                }

                if (IsDateClosed(DateTime.Parse(saleDateStr).Date))
                {
                    MessageBox.Show("لا يمكن تعديل عملية بيع تابعة ليوم تم إقفاله بالفعل.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int currentStock = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT Quantity FROM Products WHERE Barcode = @Barcode", conn))
                {
                    cmd.Parameters.AddWithValue("@Barcode", barcode);
                    var res = cmd.ExecuteScalar();
                    if (res != null) currentStock = Convert.ToInt32(res);
                }

                int availableIfReverted = currentStock + oldQty;
                if (newQty > availableIfReverted)
                {
                    MessageBox.Show($"الكمية الجديدة أكبر من المتاح! أقصى كمية ممكنة هي: {availableIfReverted}", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                decimal newTotal = price * newQty;
                using (SqliteCommand cmdUpdateSale = new SqliteCommand("UPDATE Sales SET QuantitySold = @Qty, Total = @Total WHERE SaleID = @Id", conn))
                {
                    cmdUpdateSale.Parameters.AddWithValue("@Qty", newQty);
                    cmdUpdateSale.Parameters.AddWithValue("@Total", newTotal);
                    cmdUpdateSale.Parameters.AddWithValue("@Id", selectedSaleId);
                    cmdUpdateSale.ExecuteNonQuery();
                }

                int stockAdjustment = oldQty - newQty; // موجب لو الكمية الجديدة أقل، سالب لو أكبر
                using (SqliteCommand cmdUpdateStock = new SqliteCommand("UPDATE Products SET Quantity = Quantity + @Adjustment WHERE Barcode = @Barcode", conn))
                {
                    cmdUpdateStock.Parameters.AddWithValue("@Adjustment", stockAdjustment);
                    cmdUpdateStock.Parameters.AddWithValue("@Barcode", barcode);
                    cmdUpdateStock.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم تعديل عملية البيع بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadProductsData(); LoadSalesData(); CalculateBusinessMetrics(); RefreshClosureSummary(); ClearPOSInputs();
        }

        private void BtnCancelSale_Click(object sender, EventArgs e)
        {
            if (selectedSaleId == -1)
            {
                MessageBox.Show("من فضلك اختر عملية بيع من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                string barcode = null; int qty = 0; string saleDateStr = null; string imei = null;
                using (SqliteCommand cmd = new SqliteCommand("SELECT Barcode, QuantitySold, SaleDate, IMEI FROM Sales WHERE SaleID = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", selectedSaleId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            MessageBox.Show("لم يتم العثور على عملية البيع.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        barcode = reader["Barcode"].ToString();
                        qty = Convert.ToInt32(reader["QuantitySold"]);
                        saleDateStr = reader["SaleDate"].ToString();
                        imei = reader["IMEI"] != DBNull.Value ? reader["IMEI"].ToString() : null;
                    }
                }

                if (IsDateClosed(DateTime.Parse(saleDateStr).Date))
                {
                    MessageBox.Show("لا يمكن إلغاء عملية بيع تابعة ليوم تم إقفاله بالفعل.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show("هل أنت متأكد من إلغاء عملية البيع دي؟ هيترجع للمخزون تاني.", "تأكيد الإلغاء", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                using (SqliteCommand cmdUpdateStock = new SqliteCommand("UPDATE Products SET Quantity = Quantity + @Qty WHERE Barcode = @Barcode", conn))
                {
                    cmdUpdateStock.Parameters.AddWithValue("@Qty", qty);
                    cmdUpdateStock.Parameters.AddWithValue("@Barcode", barcode);
                    cmdUpdateStock.ExecuteNonQuery();
                }

                if (imei != null)
                {
                    using (SqliteCommand cmdUnit = new SqliteCommand("UPDATE ProductUnits SET Status = 'InStock', SaleId = NULL WHERE IMEI = @IMEI", conn))
                    {
                        cmdUnit.Parameters.AddWithValue("@IMEI", imei);
                        cmdUnit.ExecuteNonQuery();
                    }
                }

                using (SqliteCommand cmdDelete = new SqliteCommand("DELETE FROM Sales WHERE SaleID = @Id", conn))
                {
                    cmdDelete.Parameters.AddWithValue("@Id", selectedSaleId);
                    cmdDelete.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم إلغاء عملية البيع بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadProductsData(); LoadSalesData(); CalculateBusinessMetrics(); RefreshClosureSummary(); ClearPOSInputs();
        }

        private void CreateReportsDesign(TabPage page)
        {
            GroupBox gbSummary = new GroupBox() { Text = "خلاصة حركة المال والملخص المالي", Location = new Point(20, 20), Size = new Size(260, 310) };

            Label lblTotalSales = new Label() { Text = "إجمالي المبيعات (الدرج):", Location = new Point(15, 30), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            lblTotalSalesVal = new Label() { Text = "0.00 ج.م", Location = new Point(15, 50), AutoSize = true, ForeColor = Color.Blue, Font = new Font("Segoe UI", 11, FontStyle.Bold) };

            Label lblTotalCapital = new Label() { Text = "تكلفة البضاعة المباعة:", Location = new Point(15, 95), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            lblTotalCapitalVal = new Label() { Text = "0.00 ج.م", Location = new Point(15, 115), AutoSize = true, ForeColor = Color.DarkGreen, Font = new Font("Segoe UI", 11, FontStyle.Bold) };

            Label lblTotalExpenses = new Label() { Text = "إجمالي المصروفات العمومية:", Location = new Point(15, 160), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            lblTotalExpensesVal = new Label() { Text = "0.00 ج.م", Location = new Point(15, 180), AutoSize = true, ForeColor = Color.DarkRed, Font = new Font("Segoe UI", 11, FontStyle.Bold) };

            Label lblTotalNetProfit = new Label() { Text = "الصافي الفعلي في جيبك 💰:", Location = new Point(15, 230), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            lblTotalNetProfitVal = new Label() { Text = "0.00 ج.م", Location = new Point(15, 255), AutoSize = true, ForeColor = Color.Purple, Font = new Font("Segoe UI", 14, FontStyle.Bold) };

            gbSummary.Controls.AddRange(new Control[] { lblTotalSales, lblTotalSalesVal, lblTotalCapital, lblTotalCapitalVal, lblTotalExpenses, lblTotalExpensesVal, lblTotalNetProfit, lblTotalNetProfitVal });

            // إضافة أدوات الفلترة بالتاريخ
            GroupBox gbFilter = new GroupBox() { Text = "فلترة التقارير بالفترة الزمنية", Location = new Point(20, 330), Size = new Size(260, 130) };
            Label lblFrom = new Label() { Text = "من تاريخ:", Location = new Point(10, 25), AutoSize = true };
            dtpFrom = new DateTimePicker() { Location = new Point(10, 45), Width = 230, Format = DateTimePickerFormat.Short };

            Label lblTo = new Label() { Text = "إلى تاريخ:", Location = new Point(10, 75), AutoSize = true };
            dtpTo = new DateTimePicker() { Location = new Point(10, 95), Width = 230, Format = DateTimePickerFormat.Short };

            btnFilterReports = new Guna2Button() { Text = "تطبيق الفلتر 🔍", Location = new Point(10, 125), Width = 230, Height = 30, FillColor = ColorSuccess };
            btnFilterReports.Click += btnFilterReports_Click;
            gbFilter.Controls.AddRange(new Control[] { lblFrom, dtpFrom, lblTo, dtpTo, btnFilterReports });

            GroupBox gbClosure = new GroupBox() { Text = "إقفال اليوم 🔒", Location = new Point(20, 470), Size = new Size(260, 380) };
            Label lblOpeningBalance = new Label() { Text = "الرصيد الافتتاحي (نقدي):", Location = new Point(10, 25), AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            lblOpeningBalanceVal = new Label() { Text = "0.00 ج.م", Location = new Point(10, 45), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ColorPrimary };

            Label lblExpectedClosing = new Label() { Text = "الرصيد الختامي المتوقع:", Location = new Point(10, 70), AutoSize = true, Font = new Font("Segoe UI", 8.5F) };
            lblExpectedClosingVal = new Label() { Text = "0.00 ج.م", Location = new Point(10, 90), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ColorWarning };

            Label lblAllMethodsTitle = new Label() { Text = "الرصيد الافتتاحي لكل الوسائل (دبل كليك للتعديل):", Location = new Point(10, 120), Size = new Size(240, 24), Font = new Font("Segoe UI", 7.5F, FontStyle.Bold) };

            dgvClosureSummary = new DataGridView() { Location = new Point(10, 145), Size = new Size(240, 220), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvClosureSummary.CellDoubleClick += DgvClosureSummary_CellDoubleClick;
            dgvClosureSummary.DefaultCellStyle.Font = new Font("Segoe UI", 8F);
            dgvClosureSummary.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            StyleDataGridView(dgvClosureSummary);

            gbClosure.Controls.AddRange(new Control[] { lblOpeningBalance, lblOpeningBalanceVal, lblExpectedClosing, lblExpectedClosingVal, lblAllMethodsTitle, dgvClosureSummary });

            btnRefreshReports = new Guna2Button() { Text = "عرض الكل وتحديث الحسابات 🔄", Location = new Point(20, 860), Width = 260, Height = 40, FillColor = ColorPrimary, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            btnRefreshReports.Click += (s, e) => { CalculateBusinessMetrics(); LoadSalesData(); LoadExpensesData(); RefreshClosureSummary(); };

            btnCloseDay = new Guna2Button() { Text = "إقفال اليوم 🔒", Location = new Point(20, 905), Width = 260, Height = 40, FillColor = ColorDanger, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            btnCloseDay.Click += BtnCloseDay_Click;

            Guna2Button btnReopenDay = new Guna2Button() { Text = "فتح اليوم تاني 🔓", Location = new Point(20, 950), Width = 260, Height = 35, FillColor = ColorNeutral, ForeColor = ColorPrimary };
            btnReopenDay.Click += BtnReopenDay_Click;

            Label lblTableTitle = new Label() { Text = "سجل الأرباح التفصيلي للبضاعة المباعة بتواريخها اللحظية:", Location = new Point(310, 20), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            dgvReports = new DataGridView() { Location = new Point(310, 45), Size = new Size(800, 600), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };

            StyleDataGridView(dgvReports);
            page.AutoScroll = true;
            page.Controls.AddRange(new Control[] { gbSummary, gbFilter, gbClosure, btnRefreshReports, btnCloseDay, btnReopenDay, lblTableTitle, dgvReports });
        }

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
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                var closureRows = new List<(int id, string method, decimal expected)>();
                using (SqliteCommand cmd = new SqliteCommand("SELECT Id, PaymentMethod, ExpectedClosingBalance FROM DailyClosures WHERE ClosureDate = @Date", conn))
                {
                    cmd.Parameters.AddWithValue("@Date", today);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            closureRows.Add((Convert.ToInt32(reader["Id"]), reader["PaymentMethod"].ToString(), Convert.ToDecimal(reader["ExpectedClosingBalance"])));
                    }
                }

                if (closureRows.Count == 0)
                {
                    MessageBox.Show("لم يتم العثور على إقفال اليوم.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                foreach (var row in closureRows)
                {
                    using (SqliteCommand cmd = new SqliteCommand("DELETE FROM CashDenominations WHERE ClosureId = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", row.id);
                        cmd.ExecuteNonQuery();
                    }

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
            LoadClosuresLog();
        }

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
                    using (SqliteConnection conn = new SqliteConnection(connectionString))
                    {
                        conn.Open();
                        using (var transaction = conn.BeginTransaction())
                        {
                            int cashClosureId = InsertClosureRow(conn, transaction, today, "نقدي", cashSummary, actualCash);

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

                            using (SqliteCommand cmdUpdateBalance = new SqliteCommand(
                                "UPDATE PaymentMethodBalances SET CurrentBalance = @Actual WHERE PaymentMethod = 'نقدي'", conn, transaction))
                            {
                                cmdUpdateBalance.Parameters.AddWithValue("@Actual", actualCash);
                                cmdUpdateBalance.ExecuteNonQuery();
                            }

                            foreach (var method in AllPaymentMethods)
                            {
                                if (method == "نقدي") continue;
                                decimal actual = denomForm.OtherMethodsActual[method];
                                InsertClosureRow(conn, transaction, today, method, summaries[method], actual);

                                using (SqliteCommand cmdUpdateOther = new SqliteCommand(
                                    "UPDATE PaymentMethodBalances SET CurrentBalance = @Actual WHERE PaymentMethod = @Method", conn, transaction))
                                {
                                    cmdUpdateOther.Parameters.AddWithValue("@Actual", actual);
                                    cmdUpdateOther.Parameters.AddWithValue("@Method", method);
                                    cmdUpdateOther.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();
                        }
                    }

                    MessageBox.Show("تم إقفال اليوم بنجاح لكل الوسائل وتسجيله في السجل. 🔒", "تم الإقفال", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshClosureSummary();
                    LoadClosuresLog();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("حدث خطأ أثناء الإقفال: " + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private int InsertClosureRow(SqliteConnection conn, SqliteTransaction transaction, string date, string method,
            (decimal opening, decimal totalIn, decimal totalOut, decimal expectedClosing) summary, decimal actual)
        {
            decimal difference = actual - summary.expectedClosing;
            string insertClosure = @"INSERT INTO DailyClosures 
                (ClosureDate, PaymentMethod, OpeningBalance, TotalIn, TotalOut, ExpectedClosingBalance, ActualClosingBalance, Difference, ClosedAt)
                VALUES (@Date, @Method, @Opening, @TotalIn, @TotalOut, @Expected, @Actual, @Diff, @ClosedAt)";

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
                cmd.ExecuteNonQuery();
            }

            int closureId;
            using (SqliteCommand cmdId = new SqliteCommand("SELECT last_insert_rowid();", conn, transaction))
            {
                closureId = Convert.ToInt32(cmdId.ExecuteScalar());
            }
            return closureId;
        }

        private void CreateExpensesDesign(Control page)
        {
            GroupBox gbAddExpense = new GroupBox() { Text = "إدارة وتسجيل بند مصروفات", Location = new Point(20, 20), Size = new Size(260, 340) };

            Label lblExpAcc = new Label() { Text = "اختر بند الحساب المصروف:", Location = new Point(10, 30), AutoSize = true };
            cmbExpenseAccounts = new ComboBox() { Location = new Point(10, 55), Width = 230, DropDownStyle = ComboBoxStyle.DropDownList };

            Label lblExpAmount = new Label() { Text = "المبلغ المدفوع (ج.م):", Location = new Point(10, 105), AutoSize = true };
            txtExpenseAmount = new TextBox() { Location = new Point(10, 130), Width = 230 };

            btnAddExpense = new Guna2Button() { Text = "تسجيل مصروف جديد 💸", Location = new Point(10, 170), Width = 230, Height = 35, FillColor = ColorSuccess, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnAddExpense.Click += BtnAddExpense_Click;

            btnEditExpenseMode = new Guna2Button() { Text = "تعديل البند المحدّد", Location = new Point(10, 210), Width = 230, Height = 35, FillColor = ColorPrimary };
            btnEditExpenseMode.Click += BtnEditExpenseMode_Click;

            btnSaveExpenseUpdate = new Guna2Button() { Text = "حفظ تعديل المصروف 💾", Location = new Point(10, 250), Width = 230, Height = 35, FillColor = ColorWarning, Font = new Font("Segoe UI", 9, FontStyle.Bold), Enabled = false };
            btnSaveExpenseUpdate.Click += BtnSaveExpenseUpdate_Click;

            btnDeleteExpense = new Guna2Button() { Text = "حذف بند المصروف", Location = new Point(10, 295), Width = 230, Height = 30, FillColor = ColorDanger };
            btnDeleteExpense.Click += BtnDeleteExpense_Click;

            gbAddExpense.Controls.AddRange(new Control[] { lblExpAcc, cmbExpenseAccounts, lblExpAmount, txtExpenseAmount, btnAddExpense, btnEditExpenseMode, btnSaveExpenseUpdate, btnDeleteExpense });

            Label lblExpTitle = new Label() { Text = "دفتر وسجل حركات المصروفات العمومية المدفوعة بالتفصيل والتواريخ الحية:", Location = new Point(310, 20), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            dgvExpenses = new DataGridView() { Location = new Point(310, 45), Size = new Size(800, 600), AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ReadOnly = true, AllowUserToAddRows = false };
            dgvExpenses.CellClick += dgvExpenses_CellClick;
            StyleDataGridView(dgvExpenses);

            page.Controls.AddRange(new Control[] { gbAddExpense, lblExpTitle, dgvExpenses });
        }

        private void btnFilterReports_Click(object sender, EventArgs e)
        {
            string fromDate = dtpFrom.Value.ToString("yyyy-MM-dd") + " 00:00:00";
            string toDate = dtpTo.Value.ToString("yyyy-MM-dd") + " 23:59:59";

            DataTable dtReports = new DataTable();
            dtReports.Columns.AddRange(new DataColumn[] {
                new DataColumn("رقم الحركة"), new DataColumn("المنتج"), new DataColumn("سعر الشراء"),
                new DataColumn("سعر البيع"), new DataColumn("الكمية"), new DataColumn("الربح الصافي"), new DataColumn("التاريخ والوقت ⏰")
            });

            string query = "SELECT SaleID, ProductName, CostPrice, Price, QuantitySold, Total, SaleDate FROM Sales WHERE SaleDate BETWEEN @From AND @To ORDER BY SaleID ASC";

            using (SqliteConnection conn = new SqliteConnection(connectionString))
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

                        // حساب مصروفات نفس الفترة للفلترة الذكية
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

        private void LoadAccountsTreeIntoCombo()
        {
            string query = "SELECT AccountCode, AccountName FROM AccountsTree ORDER BY AccountCode";
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    try
                    {
                        conn.Open();
                        DataTable dtExpense = new DataTable();
                        dtExpense.Load(cmd.ExecuteReader());
                        cmbExpenseAccounts.DataSource = dtExpense;
                        cmbExpenseAccounts.DisplayMember = "AccountName";
                        cmbExpenseAccounts.ValueMember = "AccountCode";

                        if (cmbMovementAccount != null)
                        {
                            DataTable dtMovement = dtExpense.Copy();
                            cmbMovementAccount.DataSource = dtMovement;
                            cmbMovementAccount.DisplayMember = "AccountName";
                            cmbMovementAccount.ValueMember = "AccountCode";
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void BtnAddExpense_Click(object sender, EventArgs e)
        {
            if (IsTodayClosed())
            {
                MessageBox.Show("تم إقفال اليوم بالفعل، لا يمكن تسجيل مصروفات جديدة.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cmbExpenseAccounts.SelectedValue == null || !decimal.TryParse(txtExpenseAmount.Text, out decimal amount))
            {
                MessageBox.Show("من فضلك اختر الحساب واكتب المبلغ بشكل صحيح!", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int selectedCode = Convert.ToInt32(cmbExpenseAccounts.SelectedValue);
            string query = "INSERT INTO Expenses (AccountCode, Amount) VALUES (@AccountCode, @Amount)";
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@AccountCode", selectedCode);
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    try
                    {
                        conn.Open(); cmd.ExecuteNonQuery();
                        ClearExpenseInputs();
                        LoadExpensesData();
                        CalculateBusinessMetrics();
                        RefreshClosureSummary();
                        MessageBox.Show("تم تسجيل المصروف بنجاح بالتاريخ والوقت اللحظي!", "تم التسجيل", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void dgvExpenses_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                DataGridViewRow row = dgvExpenses.Rows[e.RowIndex];
                selectedExpenseID = Convert.ToInt32(row.Cells["رقم الحركة"].Value);
                selectedExpenseDate = DateTime.Parse(row.Cells["التاريخ والوقت ⏰"].Value.ToString());
                cmbExpenseAccounts.SelectedValue = Convert.ToInt32(row.Cells["كود الحساب"].Value);
                txtExpenseAmount.Text = row.Cells["المبلغ ج.م"].Value.ToString();
                btnSaveExpenseUpdate.Enabled = false;
            }
        }

        private void BtnEditExpenseMode_Click(object sender, EventArgs e)
        {
            if (selectedExpenseID == -1)
            {
                MessageBox.Show("من فضلك اختر حركة مصروف من الجدول أولاً لتعديلها!", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            btnSaveExpenseUpdate.Enabled = true;
        }

        private void BtnSaveExpenseUpdate_Click(object sender, EventArgs e)
        {
            if (selectedExpenseID == -1 || !decimal.TryParse(txtExpenseAmount.Text, out decimal amount)) return;

            if (IsDateClosed(selectedExpenseDate))
            {
                MessageBox.Show("لا يمكن تعديل مصروف تابع ليوم تم إقفاله بالفعل.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int selectedCode = Convert.ToInt32(cmbExpenseAccounts.SelectedValue);
            string query = "UPDATE Expenses SET AccountCode = @AccountCode, Amount = @Amount WHERE ExpenseID = @ExpenseID";
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@AccountCode", selectedCode);
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    cmd.Parameters.AddWithValue("@ExpenseID", selectedExpenseID);
                    try
                    {
                        conn.Open(); cmd.ExecuteNonQuery();
                        btnSaveExpenseUpdate.Enabled = false;
                        ClearExpenseInputs();
                        LoadExpensesData();
                        CalculateBusinessMetrics();
                        RefreshClosureSummary();
                        MessageBox.Show("تم تعديل قيمة المصروف وتحديث الخلاصة المالية بنجاح!", "تم التعديل", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void BtnDeleteExpense_Click(object sender, EventArgs e)
        {
            if (selectedExpenseID == -1) return;

            if (IsDateClosed(selectedExpenseDate))
            {
                MessageBox.Show("لا يمكن حذف مصروف تابع ليوم تم إقفاله بالفعل.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show("هل أنت متأكد من حذف حركة المصروف هذه نهائياً؟", "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                string query = "DELETE FROM Expenses WHERE ExpenseID = @ExpenseID";
                using (SqliteConnection conn = new SqliteConnection(connectionString))
                {
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@ExpenseID", selectedExpenseID);
                        try
                        {
                            conn.Open(); cmd.ExecuteNonQuery();
                            ClearExpenseInputs();
                            LoadExpensesData();
                            CalculateBusinessMetrics();
                            RefreshClosureSummary();
                            MessageBox.Show("تم حذف حركة المصروف بنجاح!", "تم الحذف", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                    }
                }
            }
        }

        private void ClearExpenseInputs()
        {
            selectedExpenseID = -1;
            txtExpenseAmount.Clear();
            btnSaveExpenseUpdate.Enabled = false;
        }

        private void LoadExpensesData()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] {
                new DataColumn("رقم الحركة"),
                new DataColumn("كود الحساب"),
                new DataColumn("اسم بند المصروف"),
                new DataColumn("المبلغ ج.م"),
                new DataColumn("التاريخ والوقت ⏰")
            });

            string query = @"SELECT E.ExpenseID, E.AccountCode, A.AccountName, E.Amount, E.ExpenseDate 
                             FROM Expenses E 
                             INNER JOIN AccountsTree A ON E.AccountCode = A.AccountCode 
                             ORDER BY E.ExpenseID ASC";

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                dt.Rows.Add(reader["ExpenseID"], reader["AccountCode"], reader["AccountName"], reader["Amount"], reader["ExpenseDate"]);
                        }
                        dgvExpenses.DataSource = dt;
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void CalculateBusinessMetrics()
        {
            using (SqliteConnection conn = new SqliteConnection(connectionString))
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

        private void TxtSaleBarcode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !string.IsNullOrEmpty(txtSaleBarcode.Text))
            {
                string query = "SELECT ProductName, SalePrice, Quantity, IsSerialized FROM Products WHERE Barcode = @Barcode";
                using (SqliteConnection conn = new SqliteConnection(connectionString))
                {
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Barcode", txtSaleBarcode.Text);
                        try
                        {
                            conn.Open();
                            using (SqliteDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    int stockQty = Convert.ToInt32(reader["Quantity"]);
                                    if (stockQty <= 0)
                                    {
                                        MessageBox.Show("عذراً، هذا المنتج نفذ من المخزن تماماً!", "نفذت الكمية", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        return;
                                    }
                                    txtSaleName.Text = reader["ProductName"].ToString();
                                    txtCustomerPrice.Text = reader["SalePrice"].ToString();
                                    CalculateTotal();

                                    bool isSerialized = reader["IsSerialized"] != DBNull.Value && Convert.ToInt32(reader["IsSerialized"]) == 1;
                                    lblSaleImei.Visible = isSerialized;
                                    cmbSaleImei.Visible = isSerialized;

                                    if (isSerialized)
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
                                else
                                {
                                    MessageBox.Show("هذا الباركود غير مسجل في المخزن!", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    txtSaleBarcode.Clear();
                                    txtSaleBarcode.Focus();
                                }
                            }
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                    }
                }
            }
        }

        private void TxtSaleQty_TextChanged(object sender, EventArgs e) { CalculateTotal(); }

        private void CalculateTotal()
        {
            if (decimal.TryParse(txtCustomerPrice.Text, out decimal price) && int.TryParse(txtSaleQty.Text, out int qty))
                txtSaleTotal.Text = (price * qty).ToString();
        }

        private void LoadAvailableImeisForSale(string barcode)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("IMEI");

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand("SELECT IMEI FROM ProductUnits WHERE Barcode = @Barcode AND Status = 'InStock' ORDER BY UnitId", conn))
                {
                    cmd.Parameters.AddWithValue("@Barcode", barcode);
                    try
                    {
                        conn.Open();
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read()) dt.Rows.Add(reader["IMEI"].ToString());
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }

            cmbSaleImei.DataSource = dt;
            cmbSaleImei.DisplayMember = "IMEI";
            cmbSaleImei.ValueMember = "IMEI";

            if (dt.Rows.Count == 0)
                MessageBox.Show("مفيش أي جهاز متاح لهذا الموديل في المخزون بالـIMEI. راجع فواتير الشراء.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

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

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    string checkQuery = "SELECT Quantity, Price FROM Products WHERE Barcode = @Barcode";
                    int currentStock = 0; decimal currentCostPrice = 0;
                    using (SqliteCommand cmdCheck = new SqliteCommand(checkQuery, conn))
                    {
                        cmdCheck.Parameters.AddWithValue("@Barcode", txtSaleBarcode.Text);
                        using (SqliteDataReader reader = cmdCheck.ExecuteReader())
                        {
                            if (reader.Read()) { currentStock = Convert.ToInt32(reader["Quantity"]); currentCostPrice = Convert.ToDecimal(reader["Price"]); }
                        }
                    }

                    if (qtySold > currentStock)
                    {
                        MessageBox.Show($"الكمية المطلوبة أكبر من المتاح! المتاح حالياً هو: {currentStock}", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    string insertSale = "INSERT INTO Sales (Barcode, ProductName, CostPrice, Price, QuantitySold, Total, CustomerId, PaymentType, IMEI) VALUES (@Barcode, @ProductName, @CostPrice, @Price, @QuantitySold, @Total, @CustomerId, @PaymentType, @IMEI)";
                    using (SqliteCommand cmdInsert = new SqliteCommand(insertSale, conn))
                    {
                        cmdInsert.Parameters.AddWithValue("@Barcode", txtSaleBarcode.Text);
                        cmdInsert.Parameters.AddWithValue("@ProductName", txtSaleName.Text);
                        cmdInsert.Parameters.AddWithValue("@CostPrice", currentCostPrice);
                        cmdInsert.Parameters.AddWithValue("@Price", Convert.ToDecimal(txtCustomerPrice.Text));
                        cmdInsert.Parameters.AddWithValue("@QuantitySold", qtySold);
                        cmdInsert.Parameters.AddWithValue("@Total", Convert.ToDecimal(txtSaleTotal.Text));
                        cmdInsert.Parameters.AddWithValue("@CustomerId", customerIdParam);
                        cmdInsert.Parameters.AddWithValue("@PaymentType", paymentType);
                        cmdInsert.Parameters.AddWithValue("@IMEI", (object)selectedImei ?? DBNull.Value);
                        cmdInsert.ExecuteNonQuery();
                    }

                    int newSaleId;
                    using (SqliteCommand cmdId = new SqliteCommand("SELECT last_insert_rowid();", conn))
                    {
                        newSaleId = Convert.ToInt32(cmdId.ExecuteScalar());
                    }

                    if (selectedImei != null)
                    {
                        using (SqliteCommand cmdUnit = new SqliteCommand("UPDATE ProductUnits SET Status = 'Sold', SaleId = @SaleId WHERE IMEI = @IMEI", conn))
                        {
                            cmdUnit.Parameters.AddWithValue("@SaleId", newSaleId);
                            cmdUnit.Parameters.AddWithValue("@IMEI", selectedImei);
                            cmdUnit.ExecuteNonQuery();
                        }
                    }

                    string updateStock = "UPDATE Products SET Quantity = Quantity - @Qty WHERE Barcode = @Barcode";
                    using (SqliteCommand cmdUpdate = new SqliteCommand(updateStock, conn))
                    {
                        cmdUpdate.Parameters.AddWithValue("@Qty", qtySold);
                        cmdUpdate.Parameters.AddWithValue("@Barcode", txtSaleBarcode.Text);
                        cmdUpdate.ExecuteNonQuery();
                    }

                    MessageBox.Show("تمت عملية البيع بنجاح!", "نجاح", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadProductsData(); LoadSalesData(); CalculateBusinessMetrics(); ClearPOSInputs(); RefreshClosureSummary();
                    if (cmbSaleCustomer != null) LoadCustomersGrid();
                }
                catch (Exception ex) { MessageBox.Show("حدث خطأ: " + ex.Message); }
            }
        }

        // بيرجع مقاس الورق المطلوب حسب اختيار المستخدم في الكومبو بوكس
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

        private void BtnPrintInvoice_Click(object sender, EventArgs e)
        {
            PrintDocument pd = new PrintDocument();
            pd.PrintPage += new PrintPageEventHandler(PrintInvoicePage);
            pd.DefaultPageSettings.PaperSize = GetSelectedInvoicePaperSize();
            PrintPreviewDialog pdd = new PrintPreviewDialog() { Document = pd };
            pdd.ShowDialog();
        }

        // بيرسم محتوى الفاتورة، وبيظبط أحجام الخطوط والمسافات تلقائيًا حسب عرض الورقة (حراري 58/80 مم أو A4)
        private void PrintInvoicePage(object sender, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics;
            float pageWidth = e.PageBounds.Width;
            bool isThermal = pageWidth < 500; // A4 ~827، 80مم ~315، 58مم ~228 (بالمئة من البوصة)

            float margin = isThermal ? 10 : 20;
            float fontHeaderSize = isThermal ? 11 : 14;
            float fontBodySize = isThermal ? 9 : 11;
            float fontSmallSize = isThermal ? 7.5f : 9;

            Font fontHeader = new Font("Arial", fontHeaderSize, FontStyle.Bold);
            Font fontBody = new Font("Arial", fontBodySize, FontStyle.Regular);
            Font fontSmall = new Font("Arial", fontSmallSize, FontStyle.Regular);
            StringFormat centerFormat = new StringFormat() { Alignment = StringAlignment.Center };

            float yPos = margin;

            if (CurrentStoreLogo != null && CurrentStoreLogo.Length > 0)
            {
                float logoSize = isThermal ? 45 : 60;
                using (var ms = new System.IO.MemoryStream(CurrentStoreLogo))
                using (var logoImg = Image.FromStream(ms))
                {
                    g.DrawImage(logoImg, (pageWidth - logoSize) / 2, yPos, logoSize, logoSize);
                }
                yPos += logoSize + 6;
            }

            g.DrawString(string.IsNullOrWhiteSpace(CurrentStoreName) ? "Temo Mobile Store" : CurrentStoreName, fontHeader, Brushes.Blue, new RectangleF(0, yPos, pageWidth, 25), centerFormat);
            yPos += isThermal ? 20 : 28;

            g.DrawString("فاتورة مبيعات عميل", fontHeader, Brushes.Black, new RectangleF(0, yPos, pageWidth, 25), centerFormat);
            yPos += isThermal ? 20 : 26;

            if (!string.IsNullOrWhiteSpace(CurrentStorePhone))
            { g.DrawString($"تليفون: {CurrentStorePhone}", fontSmall, Brushes.Black, new RectangleF(0, yPos, pageWidth, 16), centerFormat); yPos += 16; }
            if (!string.IsNullOrWhiteSpace(CurrentStoreAddress))
            { g.DrawString($"العنوان: {CurrentStoreAddress}", fontSmall, Brushes.Black, new RectangleF(0, yPos, pageWidth, 16), centerFormat); yPos += 16; }

            yPos += 8;
            string separator = new string('-', isThermal ? (pageWidth < 250 ? 26 : 38) : 55);

            // نجيب آخر عملية بيع فعلاً من قاعدة البيانات (أعلى SaleID) بدل الاعتماد على ترتيب الجريد
            string productName = null; int quantitySold = 0; decimal total = 0; string saleDate = null;

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    using (SqliteCommand cmd = new SqliteCommand("SELECT ProductName, QuantitySold, Total, SaleDate FROM Sales ORDER BY SaleID DESC LIMIT 1", conn))
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            productName = reader["ProductName"].ToString();
                            quantitySold = Convert.ToInt32(reader["QuantitySold"]);
                            total = Convert.ToDecimal(reader["Total"]);
                            saleDate = reader["SaleDate"].ToString();
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show("حدث خطأ أثناء تجهيز الفاتورة: " + ex.Message); }
            }

            g.DrawString($"التاريخ: {saleDate ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}", fontBody, Brushes.Black, margin, yPos); yPos += isThermal ? 18 : 24;
            g.DrawString(separator, fontBody, Brushes.Black, margin, yPos); yPos += isThermal ? 14 : 18;

            if (productName != null)
            {
                g.DrawString($"المنتج: {productName}", fontBody, Brushes.Black, margin, yPos); yPos += isThermal ? 18 : 22;
                g.DrawString($"الكمية: {quantitySold}", fontBody, Brushes.Black, margin, yPos); yPos += isThermal ? 18 : 22;
                g.DrawString(separator, fontBody, Brushes.Black, margin, yPos); yPos += isThermal ? 16 : 20;
                g.DrawString($"الإجمالي: {total} ج.م", fontHeader, Brushes.Green, margin, yPos);
                yPos += isThermal ? 26 : 32;
            }
            else
            {
                g.DrawString("لا توجد عمليات بيع مسجلة بعد.", fontBody, Brushes.Red, margin, yPos);
                yPos += isThermal ? 20 : 26;
            }

            yPos += isThermal ? 10 : 15;
            g.DrawString("شكراً لتعاملكم معنا!", fontSmall, Brushes.Black, new RectangleF(0, yPos, pageWidth, 20), centerFormat);
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

        private void LoadProductsData()
        {
            DataTable dt = new DataTable(); dt.Columns.AddRange(new DataColumn[] { new DataColumn("الباركود"), new DataColumn("اسم المنتج"), new DataColumn("سعر الشراء"), new DataColumn("سعر البيع"), new DataColumn("الكمية"), new DataColumn("IsSerialized") });
            string query = "SELECT Barcode, ProductName, Price, SalePrice, Quantity, IsSerialized FROM Products ORDER BY ROWID ASC";
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    try { conn.Open(); using (SqliteDataReader reader = cmd.ExecuteReader()) { while (reader.Read()) dt.Rows.Add(reader["Barcode"], reader["ProductName"], reader["Price"], reader["SalePrice"], reader["Quantity"], reader["IsSerialized"] == DBNull.Value ? 0 : reader["IsSerialized"]); } dgvProducts.DataSource = dt; HighlightOutOfStockRows(); if (dgvProducts.Columns["IsSerialized"] != null) dgvProducts.Columns["IsSerialized"].Visible = false; if (!AuthManager.IsAdmin && dgvProducts.Columns["سعر الشراء"] != null) dgvProducts.Columns["سعر الشراء"].Visible = false; }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        // بيلوّن أي منتج كميته صفر باللون الأحمر عشان يبان إنه خلص من المخزن
        private void HighlightOutOfStockRows()
        {
            foreach (DataGridViewRow row in dgvProducts.Rows)
            {
                if (row.Cells["الكمية"].Value == null) continue;
                if (Convert.ToInt32(row.Cells["الكمية"].Value) <= 0)
                {
                    row.DefaultCellStyle.ForeColor = ColorDanger;
                    row.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                }
            }
        }

        private void LoadSalesData()
        {
            DataTable dt = new DataTable(); dt.Columns.AddRange(new DataColumn[] { new DataColumn("رقم البيع"), new DataColumn("المنتج"), new DataColumn("الكمية المباعة"), new DataColumn("الإجمالي"), new DataColumn("التاريخ والوقت ⏰") });
            DataTable dtReports = new DataTable(); dtReports.Columns.AddRange(new DataColumn[] { new DataColumn("رقم الحركة"), new DataColumn("المنتج"), new DataColumn("سعر الشراء"), new DataColumn("سعر البيع"), new DataColumn("الكمية"), new DataColumn("الربح الصافي"), new DataColumn("التاريخ والوقت ⏰") });

            string query = "SELECT SaleID, ProductName, CostPrice, Price, QuantitySold, Total, SaleDate FROM Sales ORDER BY SaleID ASC";
            using (SqliteConnection conn = new SqliteConnection(connectionString))
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
                                dt.Rows.Add(reader["SaleID"], reader["ProductName"], reader["QuantitySold"], reader["Total"], reader["SaleDate"]);
                                decimal cost = Convert.ToDecimal(reader["CostPrice"]); decimal sell = Convert.ToDecimal(reader["Price"]); int qty = Convert.ToInt32(reader["QuantitySold"]);
                                dtReports.Rows.Add(reader["SaleID"], reader["ProductName"], cost, sell, qty, (sell - cost) * qty, reader["SaleDate"]);
                            }
                        }
                        dgvSales.DataSource = dt; dgvReports.DataSource = dtReports;
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void dgvProducts_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                DataGridViewRow row = dgvProducts.Rows[e.RowIndex];
                txtBarcode.Text = row.Cells["الباركود"].Value.ToString(); txtProductName.Text = row.Cells["اسم المنتج"].Value.ToString();
                txtCostPrice.Text = row.Cells["سعر الشراء"].Value.ToString(); txtSalePrice.Text = row.Cells["سعر البيع"].Value.ToString(); txtQuantity.Text = row.Cells["الكمية"].Value.ToString();
                chkIsSerialized.Checked = Convert.ToInt32(row.Cells["IsSerialized"].Value) == 1;
                txtBarcode.ReadOnly = true; btnSaveUpdate.Enabled = false;
            }
        }

        private void btnAddProduct_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtBarcode.Text) || string.IsNullOrEmpty(txtProductName.Text)) return;
            string query = "INSERT INTO Products (Barcode, ProductName, Price, SalePrice, Quantity, IsSerialized) VALUES (@Barcode, @ProductName, @Price, @SalePrice, @Quantity, @IsSerialized)";
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Barcode", txtBarcode.Text); cmd.Parameters.AddWithValue("@ProductName", txtProductName.Text);
                    cmd.Parameters.AddWithValue("@Price", Convert.ToDecimal(txtCostPrice.Text)); cmd.Parameters.AddWithValue("@SalePrice", Convert.ToDecimal(txtSalePrice.Text)); cmd.Parameters.AddWithValue("@Quantity", Convert.ToInt32(txtQuantity.Text));
                    cmd.Parameters.AddWithValue("@IsSerialized", chkIsSerialized.Checked ? 1 : 0);
                    try { conn.Open(); cmd.ExecuteNonQuery(); LoadProductsData(); ClearInputs(); MessageBox.Show("تم إضافة المنتج بنجاح!", "نجاح", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void btnEditMode_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtBarcode.Text)) { MessageBox.Show("اختر منتجاً أولاً!", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            btnSaveUpdate.Enabled = true;
        }

        private void btnSaveUpdate_Click(object sender, EventArgs e)
        {
            string query = "UPDATE Products SET ProductName = @ProductName, Price = @Price, SalePrice = @SalePrice, Quantity = @Quantity, IsSerialized = @IsSerialized WHERE Barcode = @Barcode";
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Barcode", txtBarcode.Text); cmd.Parameters.AddWithValue("@ProductName", txtProductName.Text);
                    cmd.Parameters.AddWithValue("@Price", Convert.ToDecimal(txtCostPrice.Text)); cmd.Parameters.AddWithValue("@SalePrice", Convert.ToDecimal(txtSalePrice.Text)); cmd.Parameters.AddWithValue("@Quantity", Convert.ToInt32(txtQuantity.Text));
                    cmd.Parameters.AddWithValue("@IsSerialized", chkIsSerialized.Checked ? 1 : 0);
                    try { conn.Open(); cmd.ExecuteNonQuery(); LoadProductsData(); btnSaveUpdate.Enabled = false; MessageBox.Show("تم تعديل المنتج!", "نجاح", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void ClearInputs() { txtBarcode.ReadOnly = false; txtBarcode.Clear(); txtProductName.Clear(); txtCostPrice.Clear(); txtSalePrice.Clear(); txtQuantity.Clear(); chkIsSerialized.Checked = false; btnSaveUpdate.Enabled = false; txtBarcode.Focus(); }

        private void btnDeleteProduct_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtBarcode.Text)) return;
            if (MessageBox.Show("حذف المنتج؟", "تحذير", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                string query = "DELETE FROM Products WHERE Barcode = @Barcode";
                using (SqliteConnection conn = new SqliteConnection(connectionString))
                {
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Barcode", txtBarcode.Text);
                        try { conn.Open(); cmd.ExecuteNonQuery(); LoadProductsData(); ClearInputs(); } catch (Exception ex) { MessageBox.Show(ex.Message); }
                    }
                }
            }
        }
        private void BtnEditMovement_Click(object sender, EventArgs e)
        {
            if (selectedMovementId == -1)
            {
                MessageBox.Show("من فضلك اختر حركة من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string movementDateStr = null;
            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT MovementDate FROM CashMovements WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", selectedMovementId);
                    var res = cmd.ExecuteScalar();
                    if (res != null) movementDateStr = res.ToString();
                }
            }

            if (movementDateStr == null)
            {
                MessageBox.Show("لم يتم العثور على الحركة.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (IsDateClosed(DateTime.Parse(movementDateStr).Date))
            {
                MessageBox.Show("لا يمكن تعديل حركة تابعة ليوم تم إقفاله بالفعل.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnSaveMovementEdit.Enabled = true;
        }

        private void BtnSaveMovementEdit_Click(object sender, EventArgs e)
        {
            if (selectedMovementId == -1)
            {
                MessageBox.Show("من فضلك اختر حركة من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cmbMovementType.SelectedItem == null || cmbPaymentMethod.SelectedItem == null || !decimal.TryParse(txtMovementAmount.Text, out decimal newAmount) || newAmount <= 0)
            {
                MessageBox.Show("من فضلك اختر نوع الحركة ووسيلة الدفع وأدخل مبلغ صحيح.", "بيانات ناقصة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string newType = cmbMovementType.SelectedItem.ToString();
            string newMethod = cmbPaymentMethod.SelectedItem.ToString();

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                string oldType = null, oldMethod = null, movementDateStr = null;
                decimal oldAmount = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT MovementType, PaymentMethod, Amount, MovementDate FROM CashMovements WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", selectedMovementId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            MessageBox.Show("لم يتم العثور على الحركة.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        oldType = reader["MovementType"].ToString();
                        oldMethod = reader["PaymentMethod"].ToString();
                        oldAmount = Convert.ToDecimal(reader["Amount"]);
                        movementDateStr = reader["MovementDate"].ToString();
                    }
                }

                if (IsDateClosed(DateTime.Parse(movementDateStr).Date))
                {
                    MessageBox.Show("لا يمكن تعديل حركة تابعة ليوم تم إقفاله بالفعل.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // بنرجّع أثر الحركة القديمة على رصيد وسيلتها الأصلية أولاً
                decimal oldMethodBalance = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn))
                {
                    cmd.Parameters.AddWithValue("@Method", oldMethod);
                    var res = cmd.ExecuteScalar();
                    if (res != null) oldMethodBalance = Convert.ToDecimal(res);
                }
                decimal revertedOldBalance = oldType == "قبض" ? oldMethodBalance - oldAmount : oldMethodBalance + oldAmount;
                using (SqliteCommand cmd = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = @Balance WHERE PaymentMethod = @Method", conn))
                {
                    cmd.Parameters.AddWithValue("@Balance", revertedOldBalance);
                    cmd.Parameters.AddWithValue("@Method", oldMethod);
                    cmd.ExecuteNonQuery();
                }

                // دلوقتي بنجيب رصيد الوسيلة الجديدة (ممكن تكون نفس الوسيلة القديمة بعد ما اترجعت، أو وسيلة تانية)
                decimal newMethodBalance = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn))
                {
                    cmd.Parameters.AddWithValue("@Method", newMethod);
                    var res = cmd.ExecuteScalar();
                    if (res != null) newMethodBalance = Convert.ToDecimal(res);
                }

                if (newType == "صرف" && newAmount > newMethodBalance)
                {
                    // نرجع الرصيد القديم زي ما كان قبل ما نلغي العملية، عشان مانخسرش البيانات بسبب رفض التعديل
                    using (SqliteCommand cmd = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = @Balance WHERE PaymentMethod = @Method", conn))
                    {
                        cmd.Parameters.AddWithValue("@Balance", oldMethodBalance);
                        cmd.Parameters.AddWithValue("@Method", oldMethod);
                        cmd.ExecuteNonQuery();
                    }
                    MessageBox.Show($"الرصيد المتاح في \"{newMethod}\" هو {newMethodBalance} فقط، لا يمكن صرف مبلغ أكبر منه.", "رصيد غير كافٍ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                decimal finalNewBalance = newType == "قبض" ? newMethodBalance + newAmount : newMethodBalance - newAmount;
                using (SqliteCommand cmd = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = @Balance WHERE PaymentMethod = @Method", conn))
                {
                    cmd.Parameters.AddWithValue("@Balance", finalNewBalance);
                    cmd.Parameters.AddWithValue("@Method", newMethod);
                    cmd.ExecuteNonQuery();
                }

                object accountCodeParam = (cmbMovementAccount != null && cmbMovementAccount.SelectedValue != null) ? (object)Convert.ToInt32(cmbMovementAccount.SelectedValue) : DBNull.Value;

                using (SqliteCommand cmd = new SqliteCommand(
                    "UPDATE CashMovements SET MovementType = @Type, PaymentMethod = @Method, Amount = @Amount, ReferenceNumber = @Ref, Description = @Desc, AccountCode = @AccountCode WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Type", newType);
                    cmd.Parameters.AddWithValue("@Method", newMethod);
                    cmd.Parameters.AddWithValue("@Amount", newAmount);
                    cmd.Parameters.AddWithValue("@Ref", txtMovementReference.Text);
                    cmd.Parameters.AddWithValue("@Desc", txtMovementDescription.Text);
                    cmd.Parameters.AddWithValue("@AccountCode", accountCodeParam);
                    cmd.Parameters.AddWithValue("@Id", selectedMovementId);
                    cmd.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم حفظ تعديل الحركة بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            selectedMovementId = -1;
            btnSaveMovementEdit.Enabled = false;
            txtMovementAmount.Clear();
            txtMovementReference.Clear();
            txtMovementDescription.Clear();
            LoadCashMovements();
            CmbPaymentMethod_SelectedIndexChanged(null, EventArgs.Empty);
            RefreshClosureSummary();
        }

        private void BtnCancelMovement_Click(object sender, EventArgs e)
        {
            if (selectedMovementId == -1)
            {
                MessageBox.Show("من فضلك اختر حركة من الجدول أولاً.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SqliteConnection conn = new SqliteConnection(connectionString))
            {
                conn.Open();

                string type = null, method = null, movementDateStr = null;
                decimal amount = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT MovementType, PaymentMethod, Amount, MovementDate FROM CashMovements WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", selectedMovementId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            MessageBox.Show("لم يتم العثور على الحركة.", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        type = reader["MovementType"].ToString();
                        method = reader["PaymentMethod"].ToString();
                        amount = Convert.ToDecimal(reader["Amount"]);
                        movementDateStr = reader["MovementDate"].ToString();
                    }
                }

                if (IsDateClosed(DateTime.Parse(movementDateStr).Date))
                {
                    MessageBox.Show("لا يمكن إلغاء حركة تابعة ليوم تم إقفاله بالفعل.", "اليوم مقفول", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (MessageBox.Show("هل أنت متأكد من إلغاء هذه الحركة؟ سيتم عكس أثرها على الرصيد.", "تأكيد الإلغاء", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                decimal currentBalance = 0;
                using (SqliteCommand cmdBalance = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn))
                {
                    cmdBalance.Parameters.AddWithValue("@Method", method);
                    var result = cmdBalance.ExecuteScalar();
                    if (result != null) currentBalance = Convert.ToDecimal(result);
                }

                decimal newBalance = type == "قبض" ? currentBalance - amount : currentBalance + amount;
                using (SqliteCommand cmdUpdate = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = @NewBalance WHERE PaymentMethod = @Method", conn))
                {
                    cmdUpdate.Parameters.AddWithValue("@NewBalance", newBalance);
                    cmdUpdate.Parameters.AddWithValue("@Method", method);
                    cmdUpdate.ExecuteNonQuery();
                }

                using (SqliteCommand cmdDelete = new SqliteCommand("DELETE FROM CashMovements WHERE Id = @Id", conn))
                {
                    cmdDelete.Parameters.AddWithValue("@Id", selectedMovementId);
                    cmdDelete.ExecuteNonQuery();
                }
            }

            MessageBox.Show("تم إلغاء الحركة بنجاح.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
            selectedMovementId = -1;
            if (btnSaveMovementEdit != null) btnSaveMovementEdit.Enabled = false;
            LoadCashMovements();
            CmbPaymentMethod_SelectedIndexChanged(null, EventArgs.Empty);
            RefreshClosureSummary();
        }
    }
}