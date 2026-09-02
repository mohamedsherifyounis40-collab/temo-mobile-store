using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace Temo_Mobile_Store.Database
{
    public static class DatabaseManager
    {
        private static readonly string DatabasePath =
     Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TemoStoreDB.db");

        private static readonly string ConnectionString =
            $"Data Source={DatabasePath}";

        public static SqliteConnection GetConnection()
        {
            return new SqliteConnection(ConnectionString);
        }

        // ==========================================================================
        // بتتأكد إن كل الجداول المطلوبة موجودة، ولو الملف جديد (جهاز جديد)
        // بتنشئهم كلهم من الصفر + تحط بيانات أساسية لازمة عشان البرنامج يشتغل صح
        // (أرصدة وسائل الدفع، شجرة حسابات أساسية، إعدادات المحل الافتراضية).
        // ==========================================================================
        public static void EnsureSchema()
        {
            using (SqliteConnection conn = GetConnection())
            {
                conn.Open();

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS Products (
                    Barcode TEXT PRIMARY KEY,
                    ProductName TEXT NOT NULL,
                    Price REAL NOT NULL DEFAULT 0,
                    SalePrice REAL NOT NULL DEFAULT 0,
                    Quantity INTEGER NOT NULL DEFAULT 0,
                    IsSerialized INTEGER NOT NULL DEFAULT 0
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS Sales (
                    SaleID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Barcode TEXT,
                    ProductName TEXT,
                    CostPrice REAL NOT NULL DEFAULT 0,
                    Price REAL NOT NULL DEFAULT 0,
                    QuantitySold INTEGER NOT NULL DEFAULT 0,
                    Total REAL NOT NULL DEFAULT 0,
                    CustomerId INTEGER,
                    PaymentType TEXT,
                    IMEI TEXT,
                    SaleDate TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                    SalesInvoiceId INTEGER
                );");

                // مرتجعات المبيعات - سجل مستقل عن Sales عمدًا (زي InventoryAdjustments مقابل
                // Products بالظبط): عملية البيع الأصلية بتفضل ثابتة كسجل تاريخي، وكل حركة مرتجع
                // (كلي أو جزئي) بتتوثّق هنا لوحدها. "الكمية المتاحة للإرجاع" = Sales.QuantitySold
                // ناقص مجموع Quantity هنا لنفس SaleId. الاسم "SalesReturns" مش "Returns" عمدًا - فيه
                // جدول قديم فاضي اسمه "Returns" (schema مختلف تمامًا: ReturnType/OriginalSaleInvoiceId/
                // OriginalPurchaseId...) باقي من محاولة قديمة اتلخبطت ومحدش استخدمها (0 صفوف)،
                // ومحتفظين بيه من غير لمس بدل ما نتصادم معاه بنفس الاسم.
                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS SalesReturns (
                    ReturnId INTEGER PRIMARY KEY AUTOINCREMENT,
                    SaleId INTEGER NOT NULL,
                    Barcode TEXT,
                    ProductName TEXT,
                    Quantity INTEGER NOT NULL DEFAULT 0,
                    RefundAmount REAL NOT NULL DEFAULT 0,
                    Reason TEXT,
                    PaymentType TEXT,
                    PaymentMethod TEXT,
                    CustomerId INTEGER,
                    ReturnDate TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                    PerformedBy TEXT
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS Customers (
                    CustomerId INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerName TEXT NOT NULL,
                    Phone TEXT,
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime'))
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS Suppliers (
                    SupplierId INTEGER PRIMARY KEY AUTOINCREMENT,
                    SupplierName TEXT NOT NULL,
                    Phone TEXT,
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime'))
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS Purchases (
                    PurchaseId INTEGER PRIMARY KEY AUTOINCREMENT,
                    SupplierId INTEGER,
                    PurchaseDate TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                    TotalAmount REAL NOT NULL DEFAULT 0,
                    Notes TEXT
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS PurchaseItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PurchaseId INTEGER,
                    Barcode TEXT,
                    ProductName TEXT,
                    Quantity INTEGER NOT NULL DEFAULT 0,
                    UnitCost REAL NOT NULL DEFAULT 0,
                    LineTotal REAL NOT NULL DEFAULT 0,
                    SkipInventory INTEGER NOT NULL DEFAULT 0
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS ProductUnits (
                    UnitId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Barcode TEXT,
                    IMEI TEXT UNIQUE,
                    Status TEXT NOT NULL DEFAULT 'InStock',
                    PurchaseId INTEGER,
                    SaleId INTEGER,
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime'))
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS MaintenanceTickets (
                    TicketId INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerName TEXT,
                    CustomerPhone TEXT,
                    DeviceInfo TEXT,
                    IssueDescription TEXT,
                    ReceivedDate TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                    EstimatedCost REAL DEFAULT 0,
                    ActualCost REAL,
                    Status TEXT NOT NULL DEFAULT 'مستلم',
                    DeliveredDate TEXT
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS Expenses (
                    ExpenseID INTEGER PRIMARY KEY AUTOINCREMENT,
                    AccountCode INTEGER,
                    Amount REAL NOT NULL DEFAULT 0,
                    ExpenseDate TEXT NOT NULL DEFAULT (datetime('now','localtime'))
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS CashMovements (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    MovementDate TEXT NOT NULL DEFAULT (date('now','localtime')),
                    MovementType TEXT NOT NULL,
                    PaymentMethod TEXT NOT NULL,
                    Amount REAL NOT NULL DEFAULT 0,
                    ReferenceNumber TEXT,
                    Description TEXT,
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                    AccountCode INTEGER,
                    CustomerId INTEGER,
                    SupplierId INTEGER,
                    EmployeeId INTEGER,
                    IsAdvance INTEGER NOT NULL DEFAULT 0
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS PaymentMethodBalances (
                    PaymentMethod TEXT PRIMARY KEY,
                    CurrentBalance REAL NOT NULL DEFAULT 0
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS AccountsTree (
                    AccountCode INTEGER PRIMARY KEY,
                    AccountName TEXT NOT NULL
                );");

                // ==========================================================================
                // دفتر اليومية (Double-entry) - كل عملية (بيع/شراء/مصروف/تحويل/سداد) بتنشئ
                // قيد هنا تلقائيًا عن طريق Accounting Engine. القيد ممنوع يتحفظ لو
                // إجمالي المدين ≠ إجمالي الدائن (AccountingEngine.Post بيتأكد من كده بنفسه).
                // ==========================================================================
                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS JournalEntries (
                    JournalEntryId INTEGER PRIMARY KEY AUTOINCREMENT,
                    EntryDate      TEXT NOT NULL,
                    SourceType     TEXT NOT NULL,
                    SourceId       INTEGER,
                    Description    TEXT,
                    CreatedAt      TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                    CreatedBy      TEXT
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS JournalLines (
                    JournalLineId   INTEGER PRIMARY KEY AUTOINCREMENT,
                    JournalEntryId  INTEGER NOT NULL,
                    AccountCode     INTEGER NOT NULL,
                    Debit           REAL NOT NULL DEFAULT 0,
                    Credit          REAL NOT NULL DEFAULT 0
                );");

                // سجل تدقيق (Audit) - بيتسجل بعد نجاح أي عملية (Commit)، من خلال Event Bus،
                // مش جزء من أي Transaction مالية (راجع قسم 4 بمستند العمارة).
                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS AuditLog (
                    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username    TEXT,
                    Screen      TEXT,
                    Operation   TEXT,
                    OldData     TEXT,
                    NewData     TEXT,
                    CreatedAt   TEXT NOT NULL DEFAULT (datetime('now','localtime'))
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS DailyClosures (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ClosureDate TEXT NOT NULL,
                    PaymentMethod TEXT NOT NULL,
                    OpeningBalance REAL NOT NULL DEFAULT 0,
                    TotalIn REAL NOT NULL DEFAULT 0,
                    TotalOut REAL NOT NULL DEFAULT 0,
                    ExpectedClosingBalance REAL NOT NULL DEFAULT 0,
                    ActualClosingBalance REAL NOT NULL DEFAULT 0,
                    Difference REAL NOT NULL DEFAULT 0,
                    ClosedAt TEXT NOT NULL DEFAULT (datetime('now','localtime'))
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS CashDenominations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ClosureId INTEGER,
                    DenominationValue REAL NOT NULL,
                    DenominationCount INTEGER NOT NULL DEFAULT 0,
                    LineTotal REAL NOT NULL DEFAULT 0
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS InventoryAdjustments (
                    AdjustmentId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Barcode TEXT,
                    ProductName TEXT,
                    SystemQuantityBefore INTEGER NOT NULL DEFAULT 0,
                    CountedQuantity INTEGER NOT NULL DEFAULT 0,
                    Difference INTEGER NOT NULL DEFAULT 0,
                    AdjustmentDate TEXT NOT NULL DEFAULT (datetime('now','localtime'))
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS StoreSettings (
                    Id INTEGER PRIMARY KEY,
                    StoreName TEXT,
                    Phone TEXT,
                    Address TEXT,
                    LogoImage BLOB,
                    CatalogSyncUrl TEXT,
                    CatalogSyncSecret TEXT,
                    CatalogSyncEnabled INTEGER NOT NULL DEFAULT 0
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS Employees (
                    EmployeeId INTEGER PRIMARY KEY AUTOINCREMENT,
                    FullName TEXT NOT NULL,
                    Phone TEXT,
                    MonthlySalary REAL NOT NULL DEFAULT 0,
                    HireDate TEXT NOT NULL DEFAULT (date('now','localtime')),
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime'))
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS AttendanceRecords (
                    AttendanceId INTEGER PRIMARY KEY AUTOINCREMENT,
                    EmployeeId INTEGER NOT NULL,
                    AttendanceDate TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                    UNIQUE(EmployeeId, AttendanceDate)
                );");

                // قفل شهر الرواتب: بمجرد ما شهر معين يتقفل لموظف، القيم بتاعته (قيمة اليوم،
                // أيام الحضور/الغياب، الراتب الصافي) بتتجمد هنا نهائيًا - حتى لو المرتب الشهري
                // اتغيّر بعد كده أو حد عدّل سجل حضور قديم، الشهر المقفول مايتأثرش. ده اللي بيضمن
                // إن "المستحق" للموظف رقم ثابت ومايتغيرش تحت رجلينا وقت الحساب.
                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS PayrollClosures (
                    ClosureId INTEGER PRIMARY KEY AUTOINCREMENT,
                    EmployeeId INTEGER NOT NULL,
                    Year INTEGER NOT NULL,
                    Month INTEGER NOT NULL,
                    MonthlySalary REAL NOT NULL,
                    DayValue REAL NOT NULL,
                    PresentDays INTEGER NOT NULL,
                    AbsentDays INTEGER NOT NULL,
                    LeaveDays INTEGER NOT NULL,
                    NetSalary REAL NOT NULL,
                    ClosedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                    UNIQUE(EmployeeId, Year, Month)
                );");

                // ---------- بيانات أساسية لازمة عشان البرنامج يشتغل صح من أول تشغيل ----------

                // صف افتراضي لإعدادات المحل (لو مش موجود)
                ExecuteNonQuery(conn, @"INSERT OR IGNORE INTO StoreSettings (Id, StoreName, Phone, Address, LogoImage)
                                        VALUES (1, 'Temo Mobile Store', '', '', NULL);");

                // رصيد افتتاحي صفر لكل وسائل الدفع (لو مش موجودين)
                string[] paymentMethods = Temo_Mobile_Store.UIHelpers.PaymentMethods;
                foreach (string method in paymentMethods)
                {
                    using (SqliteCommand cmd = new SqliteCommand(
                        "INSERT OR IGNORE INTO PaymentMethodBalances (PaymentMethod, CurrentBalance) VALUES (@Method, 0);", conn))
                    {
                        cmd.Parameters.AddWithValue("@Method", method);
                        cmd.ExecuteNonQuery();
                    }
                }

                // شجرة حسابات أساسية (لو مش موجودة) - يقدر يضيف عليها الأدمن من شاشة الحسابات بعد كده
                var defaultAccounts = new (int Code, string Name)[]
                {
                    (1100, "نقدي - الخزينة"),
                    (1110, "فوري"),
                    (1120, "أمان"),
                    (1130, "سهولة"),
                    (1140, "فودافون كاش"),
                    (1150, "إنستاباي"),
                    (1200, "المخزون (البضاعة)"),
                    (1300, "عملاء (ذمم مدينة)"),
                    (1400, "سلف الموظفين"),
                    (2100, "موردون (ذمم دائنة)"),
                    (2200, "ضريبة القيمة المضافة المستحقة"),
                    (4100, "إيراد المبيعات"),
                    (4110, "خصومات مبيعات"),
                    (4200, "إيراد الصيانة"),
                    (5100, "مصروفات عمومية وإدارية"),
                    (5200, "إيجار"),
                    (5300, "كهرباء ومياه"),
                    (5400, "مرتبات"),
                    (5500, "تكلفة البضاعة المباعة"),
                    (5510, "تكلفة قطع غيار الصيانة"),
                    (5600, "فروق وتسويات المخزون"),
                    (5700, "عجز وزيادة الخزينة"),
                };
                foreach (var acc in defaultAccounts)
                {
                    using (SqliteCommand cmd = new SqliteCommand(
                        "INSERT OR IGNORE INTO AccountsTree (AccountCode, AccountName) VALUES (@Code, @Name);", conn))
                    {
                        cmd.Parameters.AddWithValue("@Code", acc.Code);
                        cmd.Parameters.AddWithValue("@Name", acc.Name);
                        cmd.ExecuteNonQuery();
                    }
                }

                EnsureExpensesPaymentMethodColumn(conn);
                EnsureCashMovementsEmployeeIdColumn(conn);
                EnsureCashMovementsIsAdvanceColumn(conn);
                EnsureCashMovementsPurchaseIdColumn(conn);
                EnsureCashMovementsSaleIdColumn(conn);
                EnsureCashMovementsLinkedMovementIdColumn(conn);
                EnsureSalesPaymentMethodColumn(conn);
                EnsureSalesInvoiceIdColumn(conn);
                EnsureStoreSettingsCatalogSyncColumns(conn);
                EnsureStoreSettingsReceiptPrinterColumn(conn);
                EnsureDailyClosuresAdjustmentMovementIdColumn(conn);
                EnsureEmployeesStandardHoursPerDayColumn(conn);
                EnsureAttendanceRecordsOvertimeHoursColumn(conn);
                EnsurePayrollClosuresOvertimeColumns(conn);
                EnsureProductsImageColumn(conn);
                EnsureSalesDiscountColumn(conn);
                EnsureSalesTaxAndPaymentColumns(conn);
                EnsurePurchaseItemsSkipInventoryColumn(conn);
                EnsureStoreSettingsThemeColumn(conn);
                RenamePaymentMethodSohoulaToMomken(conn);

                BackfillHistoricalJournalEntries(conn);
                ReconcileTreasuryLedgerHistoricalGap(conn);
            }
        }

        // ==========================================================================
        // تسوية تاريخية (مرة واحدة فقط، محمية بعلامة TreasuryReconciliationMarker):
        // "فحص سلامة النظام" كشف إن أرصدة الخزينة الفعلية (PaymentMethodBalances -
        // بتتحدث لحظيًا مع كل عملية حقيقية) مش متطابقة مع رصيد الدفتر المحاسبي (الدفتر
        // اتبنى بأثر رجعي من BackfillHistoricalJournalEntries فوق، واللي بطبيعته بيمشي
        // على "الحالة النهائية دلوقتي" لكل عملية قديمة مش كل تعديل/إلغاء حصل فيها -
        // فرق تراكمي طبيعي نتيجة كده، مش خطأ في العملية الحالية).
        //
        // الحل: الخزينة الفعلية (اللي بتتحدث لحظيًا وبيتم جردها فعليًا) هي مصدر الحقيقة،
        // فبنسوّي الدفتر عليها بقيد تسوية واحد واضح (حساب 2200... لأ، حساب 5700 "عجز
        // وزيادة الخزينة" الموجود أصلًا في شجرة الحسابات بالظبط للحالة دي)، بدل ما نحاول
        // نعيد بناء تاريخ كل عملية اتعدّلت أو اتلغت من زمان (خطر ومش أدق من كده).
        // ==========================================================================
        private static void ReconcileTreasuryLedgerHistoricalGap(SqliteConnection conn)
        {
            using (var checkCmd = new SqliteCommand("SELECT COUNT(*) FROM JournalEntries WHERE SourceType = 'TreasuryReconciliationMarker'", conn))
            {
                if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0) return;
            }

            var balances = new List<(string Method, decimal QuickCash)>();
            using (var cmd = new SqliteCommand("SELECT PaymentMethod, CurrentBalance FROM PaymentMethodBalances", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                    balances.Add((reader["PaymentMethod"].ToString(), Convert.ToDecimal(reader["CurrentBalance"])));
            }

            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    var gapLines = new List<(int AccountCode, decimal Debit, decimal Credit)>();
                    decimal totalGap = 0;

                    foreach (var (method, quickCash) in balances)
                    {
                        int accountCode = HistoricalPaymentMethodAccountCode(method);
                        decimal ledgerBalance;
                        using (var cmd = new SqliteCommand("SELECT COALESCE(SUM(Debit),0) - COALESCE(SUM(Credit),0) FROM JournalLines WHERE AccountCode = @Code", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@Code", accountCode);
                            ledgerBalance = Convert.ToDecimal(cmd.ExecuteScalar());
                        }

                        decimal gap = ledgerBalance - quickCash; // موجب = الدفتر زيادة عن الفعلي
                        if (Math.Abs(gap) <= 0.01m) continue;

                        if (gap > 0)
                            gapLines.Add((accountCode, 0, gap)); // ننزل الدفتر لحد الرصيد الفعلي
                        else
                            gapLines.Add((accountCode, -gap, 0)); // نزوّد الدفتر لحد الرصيد الفعلي
                        totalGap += gap;
                    }

                    if (gapLines.Count > 0)
                    {
                        int journalId = InsertJournalEntry(conn, tx, DateTime.Now.ToString("yyyy-MM-dd"), "TreasuryReconciliation", 1,
                            "تسوية تاريخية - فرق تراكمي بين الخزينة الفعلية والدفتر المرحّل بأثر رجعي (راجع فحص سلامة النظام)", "ترحيل-تاريخي");
                        foreach (var line in gapLines)
                            InsertJournalLine(conn, tx, journalId, line.AccountCode, line.Debit, line.Credit);

                        // حساب موازن واحد (عجز وزيادة الخزينة) بعكس صافي كل الفروق مع بعض
                        if (totalGap > 0)
                            InsertJournalLine(conn, tx, journalId, 5700, totalGap, 0);
                        else if (totalGap < 0)
                            InsertJournalLine(conn, tx, journalId, 5700, 0, -totalGap);
                    }

                    InsertJournalEntry(conn, tx, DateTime.Now.ToString("yyyy-MM-dd"), "TreasuryReconciliationMarker", 1, "علامة انتهاء تسوية الخزينة التاريخية - ما تتحذفش", "ترحيل-تاريخي");

                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        // ==========================================================================
        // ترحيل بأثر رجعي (مرة واحدة فقط في عمر البرنامج، محمي بعلامة BackfillMarker):
        // الدفتر المحاسبي (JournalEntries/JournalLines) اتبنى في وقت متأخر عن أول بيانات
        // حقيقية اتسجلت في البرنامج (مبيعات/مشتريات/مصروفات/حركات خزينة قديمة من غير أي
        // قيد محاسبي خالص). الترحيل ده بيمشي على كل صف قديم زي ما هو دلوقتي (الحالة
        // النهائية بتاعته، مش تاريخ كل خطوة وسطية حصلت فيه) ويبني له القيد المناسب،
        // عشان الدفتر يبقى مصدر صحيح وكامل من أول يوم مش بس من النهارده.
        //
        // أهم حاجة هنا: الترحيل ده بيتنفذ مرة واحدة بس (لو لقى علامة BackfillMarker
        // موجودة، بيطلع فورًا من غير ما يلمس حاجة). العمليات الجديدة (اللي بتتسجل من
        // خلال ICoreEngine بعد النهارده) بيتسجل لها قيد حي لحظة حدوثها زي ما هو، فمفيش
        // داعي (ولا يصح أبدًا) نمرّ عليها هنا تاني في أي تشغيل لاحق - لو مرّينا عليها,
        // هيتضاعف كل رقم في الدفتر مع كل عملية جديدة بعد أول Restart للبرنامج.
        // ==========================================================================
        private static readonly Dictionary<string, int> HistoricalPaymentMethodAccountCodes = new Dictionary<string, int>
        {
            { "نقدي", 1100 }, { "فوري", 1110 }, { "أمان", 1120 }, { "سهولة", 1130 }, { "ممكن", 1130 }, { "فودافون كاش", 1140 }, { "إنستاباي", 1150 }
        };

        private static int HistoricalPaymentMethodAccountCode(string method) =>
            !string.IsNullOrEmpty(method) && HistoricalPaymentMethodAccountCodes.TryGetValue(method, out int code) ? code : 1100;

        private static string DatePartOf(string dateTimeText) =>
            !string.IsNullOrEmpty(dateTimeText) && dateTimeText.Length >= 10 ? dateTimeText.Substring(0, 10) : DateTime.Now.ToString("yyyy-MM-dd");

        private static void BackfillHistoricalJournalEntries(SqliteConnection conn)
        {
            using (var checkCmd = new SqliteCommand("SELECT COUNT(*) FROM JournalEntries WHERE SourceType = 'BackfillMarker'", conn))
            {
                if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0) return;
            }

            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    BackfillSales(conn, tx);
                    BackfillPurchases(conn, tx);
                    BackfillExpenses(conn, tx);
                    BackfillTransfers(conn, tx);
                    BackfillRemainingCashMovements(conn, tx);

                    InsertJournalEntry(conn, tx, DateTime.Now.ToString("yyyy-MM-dd"), "BackfillMarker", 1, "علامة انتهاء ترحيل القيود التاريخية - ما تتحذفش", "ترحيل-تاريخي");

                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        // كل عملية بيع لسه موجودة في الجدول (الحالة النهائية بتاعتها) وملهاش قيد حي بالفعل
        private static void BackfillSales(SqliteConnection conn, SqliteTransaction tx)
        {
            var rows = new List<(int SaleId, decimal CostPrice, int Qty, decimal Total, string PaymentType, string PaymentMethod, string SaleDate)>();
            using (var cmd = new SqliteCommand(@"
                SELECT SaleID, CostPrice, QuantitySold, Total, PaymentType, PaymentMethod, SaleDate
                FROM Sales
                WHERE NOT EXISTS (SELECT 1 FROM JournalEntries J WHERE J.SourceType = 'Sale' AND J.SourceId = Sales.SaleID)", conn, tx))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows.Add((
                        Convert.ToInt32(reader["SaleID"]),
                        Convert.ToDecimal(reader["CostPrice"]),
                        Convert.ToInt32(reader["QuantitySold"]),
                        Convert.ToDecimal(reader["Total"]),
                        reader["PaymentType"] == DBNull.Value ? "" : reader["PaymentType"].ToString(),
                        reader["PaymentMethod"] == DBNull.Value ? null : reader["PaymentMethod"].ToString(),
                        reader["SaleDate"].ToString()));
                }
            }

            foreach (var s in rows)
            {
                // بيع كاش من غير وسيلة دفع معروفة (بيانات قديمة من قبل ما "وسيلة الدفع"
                // تتضاف للمبيعات) بيتفترض "نقدي" كافتراضي معقول، زي AccountCodes.ForPaymentMethod
                int otherAccount = s.PaymentType == "Cash"
                    ? HistoricalPaymentMethodAccountCode(s.PaymentMethod ?? "نقدي")
                    : 1300;

                decimal cost = s.CostPrice * s.Qty;
                int journalId = InsertJournalEntry(conn, tx, DatePartOf(s.SaleDate), "Sale", s.SaleId, $"ترحيل تاريخي - بيع رقم {s.SaleId}", "ترحيل-تاريخي");
                InsertJournalLine(conn, tx, journalId, otherAccount, s.Total, 0);
                InsertJournalLine(conn, tx, journalId, 4100, 0, s.Total);
                if (cost > 0)
                {
                    InsertJournalLine(conn, tx, journalId, 5500, cost, 0);
                    InsertJournalLine(conn, tx, journalId, 1200, 0, cost);
                }
            }
        }

        // كل فاتورة شراء لسه موجودة، ملهاش قيد حي بالفعل - لو فيها حركة "صرف" مرتبطة
        // بيها (سداد كاش فوري)، بناخد آخر وحدة دفع اتسجلت بيها (أدق تمثيل للحالة الحالية
        // لو الفاتورة اتعدّلت أكتر من مرة)، وإلا هي فاتورة آجلة (موردون)
        private static void BackfillPurchases(SqliteConnection conn, SqliteTransaction tx)
        {
            var rows = new List<(int PurchaseId, decimal TotalAmount, string PurchaseDate)>();
            using (var cmd = new SqliteCommand(@"
                SELECT PurchaseId, TotalAmount, PurchaseDate
                FROM Purchases
                WHERE NOT EXISTS (SELECT 1 FROM JournalEntries J WHERE J.SourceType = 'Purchase' AND J.SourceId = Purchases.PurchaseId)", conn, tx))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                    rows.Add((Convert.ToInt32(reader["PurchaseId"]), Convert.ToDecimal(reader["TotalAmount"]), reader["PurchaseDate"].ToString()));
            }

            foreach (var p in rows)
            {
                string cashMethod = null;
                using (var cmd = new SqliteCommand(
                    "SELECT PaymentMethod FROM CashMovements WHERE PurchaseId = @Id AND MovementType = 'صرف' ORDER BY Id DESC LIMIT 1", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@Id", p.PurchaseId);
                    var res = cmd.ExecuteScalar();
                    if (res != null && res != DBNull.Value) cashMethod = res.ToString();
                }

                int otherAccount = cashMethod != null ? HistoricalPaymentMethodAccountCode(cashMethod) : 2100;

                int journalId = InsertJournalEntry(conn, tx, DatePartOf(p.PurchaseDate), "Purchase", p.PurchaseId, $"ترحيل تاريخي - فاتورة شراء رقم {p.PurchaseId}", "ترحيل-تاريخي");
                InsertJournalLine(conn, tx, journalId, 1200, p.TotalAmount, 0);
                InsertJournalLine(conn, tx, journalId, otherAccount, 0, p.TotalAmount);
            }
        }

        // كل مصروف عمومي لسه موجود، ملهوش قيد حي بالفعل. لو AccountCode فاضي (بيانات
        // تالفة/قديمة جدًا) بيتجاهل - مفيش حساب مصروف نعرفه نسجل عليه
        private static void BackfillExpenses(SqliteConnection conn, SqliteTransaction tx)
        {
            var rows = new List<(int ExpenseId, int? AccountCode, decimal Amount, string PaymentMethod, string ExpenseDate)>();
            using (var cmd = new SqliteCommand(@"
                SELECT ExpenseID, AccountCode, Amount, PaymentMethod, ExpenseDate
                FROM Expenses
                WHERE NOT EXISTS (SELECT 1 FROM JournalEntries J WHERE J.SourceType = 'Expense' AND J.SourceId = Expenses.ExpenseID)", conn, tx))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows.Add((
                        Convert.ToInt32(reader["ExpenseID"]),
                        reader["AccountCode"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AccountCode"]),
                        Convert.ToDecimal(reader["Amount"]),
                        reader["PaymentMethod"] == DBNull.Value ? null : reader["PaymentMethod"].ToString(),
                        reader["ExpenseDate"].ToString()));
                }
            }

            foreach (var e in rows)
            {
                if (!e.AccountCode.HasValue) continue;

                int methodAccount = HistoricalPaymentMethodAccountCode(e.PaymentMethod ?? "نقدي");
                int journalId = InsertJournalEntry(conn, tx, DatePartOf(e.ExpenseDate), "Expense", e.ExpenseId, "ترحيل تاريخي - مصروف عمومي", "ترحيل-تاريخي");
                InsertJournalLine(conn, tx, journalId, e.AccountCode.Value, e.Amount, 0);
                InsertJournalLine(conn, tx, journalId, methodAccount, 0, e.Amount);
            }
        }

        // تحويلات بين وسائل الدفع - كل زوج حركات مرتبطة (LinkedMovementId) بيتترحّل
        // كقيد واحد. Sale/Purchase ملهمش تحويلات، فمش محتاجين نستثنيهم هنا
        private static void BackfillTransfers(SqliteConnection conn, SqliteTransaction tx)
        {
            var legsById = new Dictionary<int, (int LinkedId, string Type, string Method, decimal Amount, string Date)>();
            using (var cmd = new SqliteCommand(
                "SELECT Id, LinkedMovementId, MovementType, PaymentMethod, Amount, MovementDate FROM CashMovements WHERE LinkedMovementId IS NOT NULL", conn, tx))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    legsById[Convert.ToInt32(reader["Id"])] = (
                        Convert.ToInt32(reader["LinkedMovementId"]),
                        reader["MovementType"].ToString(),
                        reader["PaymentMethod"].ToString(),
                        Convert.ToDecimal(reader["Amount"]),
                        reader["MovementDate"].ToString());
                }
            }

            var processedPairs = new HashSet<int>();
            foreach (var kvp in legsById)
            {
                int id = kvp.Key;
                var leg = kvp.Value;
                int pairKey = Math.Min(id, leg.LinkedId);
                if (!processedPairs.Add(pairKey)) continue;
                if (!legsById.TryGetValue(leg.LinkedId, out var otherLeg)) continue; // رابط تالف/يتيم - تجاهله بأمان

                using (var checkCmd = new SqliteCommand("SELECT COUNT(*) FROM JournalEntries WHERE SourceType = 'Transfer' AND SourceId = @Id", conn, tx))
                {
                    checkCmd.Parameters.AddWithValue("@Id", pairKey);
                    if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0) continue;
                }

                var fromLeg = leg.Type == "صرف" ? leg : otherLeg;
                var toLeg = leg.Type == "قبض" ? leg : otherLeg;

                int journalId = InsertJournalEntry(conn, tx, DatePartOf(leg.Date), "Transfer", pairKey, $"ترحيل تاريخي - تحويل من {fromLeg.Method} إلى {toLeg.Method}", "ترحيل-تاريخي");
                InsertJournalLine(conn, tx, journalId, HistoricalPaymentMethodAccountCode(toLeg.Method), toLeg.Amount, 0);
                InsertJournalLine(conn, tx, journalId, HistoricalPaymentMethodAccountCode(fromLeg.Method), 0, fromLeg.Amount);
            }
        }

        // باقي حركات الخزينة (مش مرتبطة ببيع/شراء/تحويل): تحصيل من عميل، سداد لمورد،
        // صرف/سلفة مرتب، أو حركة قبض/صرف عامة مصنّفة على حساب. لو الحركة مش مصنّفة على
        // أي حساب معروف (لا AccountCode ولا عميل/مورد/موظف)، بتتجاهل - نفس مبدأ
        // AddMovementCommand: مفيش حساب مقابل حقيقي نخترعه.
        //
        // ملحوظة تصحيح: الكود القديم (قبل commit 162bb24) كان بيسجّل السلف والدفعات
        // العادية للموظفين على نفس حساب المرتبات (5400) - القيم القديمة هنا بتتصحح
        // للتصنيف الصح (1400 للسلف) بدل ما تتوارث كخطأ في الدفتر من الأول
        private static void BackfillRemainingCashMovements(SqliteConnection conn, SqliteTransaction tx)
        {
            var rows = new List<(int Id, string Type, string Method, decimal Amount, int? AccountCode, int? CustomerId, int? SupplierId, int? EmployeeId, bool IsAdvance, string Date)>();
            using (var cmd = new SqliteCommand(@"
                SELECT Id, MovementType, PaymentMethod, Amount, AccountCode, CustomerId, SupplierId, EmployeeId, IsAdvance, MovementDate
                FROM CashMovements
                WHERE LinkedMovementId IS NULL AND SaleId IS NULL AND PurchaseId IS NULL", conn, tx))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows.Add((
                        Convert.ToInt32(reader["Id"]),
                        reader["MovementType"].ToString(),
                        reader["PaymentMethod"].ToString(),
                        Convert.ToDecimal(reader["Amount"]),
                        reader["AccountCode"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AccountCode"]),
                        reader["CustomerId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["CustomerId"]),
                        reader["SupplierId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["SupplierId"]),
                        reader["EmployeeId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["EmployeeId"]),
                        reader["IsAdvance"] != DBNull.Value && Convert.ToInt32(reader["IsAdvance"]) == 1,
                        reader["MovementDate"].ToString()));
                }
            }

            foreach (var m in rows)
            {
                int? otherAccount = null;
                if (m.EmployeeId.HasValue) otherAccount = m.IsAdvance ? 1400 : 5400;
                else if (m.AccountCode.HasValue) otherAccount = m.AccountCode.Value;
                else if (m.CustomerId.HasValue) otherAccount = 1300;
                else if (m.SupplierId.HasValue) otherAccount = 2100;

                if (!otherAccount.HasValue) continue;

                int journalId = InsertJournalEntry(conn, tx, DatePartOf(m.Date), "HistoricalMovement", m.Id, "ترحيل تاريخي - حركة خزينة", "ترحيل-تاريخي");
                if (m.Type == "قبض")
                {
                    InsertJournalLine(conn, tx, journalId, HistoricalPaymentMethodAccountCode(m.Method), m.Amount, 0);
                    InsertJournalLine(conn, tx, journalId, otherAccount.Value, 0, m.Amount);
                }
                else
                {
                    InsertJournalLine(conn, tx, journalId, otherAccount.Value, m.Amount, 0);
                    InsertJournalLine(conn, tx, journalId, HistoricalPaymentMethodAccountCode(m.Method), 0, m.Amount);
                }
            }
        }

        private static int InsertJournalEntry(SqliteConnection conn, SqliteTransaction tx, string entryDate, string sourceType, int sourceId, string description, string createdBy)
        {
            using (var cmd = new SqliteCommand(
                "INSERT INTO JournalEntries (EntryDate, SourceType, SourceId, Description, CreatedAt, CreatedBy) VALUES (@Date, @Type, @Id, @Desc, @CreatedAt, @By)", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Date", entryDate);
                cmd.Parameters.AddWithValue("@Type", sourceType);
                cmd.Parameters.AddWithValue("@Id", sourceId);
                cmd.Parameters.AddWithValue("@Desc", description);
                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@By", createdBy);
                cmd.ExecuteNonQuery();
            }
            using (var idCmd = new SqliteCommand("SELECT last_insert_rowid();", conn, tx))
                return Convert.ToInt32(idCmd.ExecuteScalar());
        }

        private static void InsertJournalLine(SqliteConnection conn, SqliteTransaction tx, int journalEntryId, int accountCode, decimal debit, decimal credit)
        {
            using (var cmd = new SqliteCommand(
                "INSERT INTO JournalLines (JournalEntryId, AccountCode, Debit, Credit) VALUES (@EntryId, @Code, @Debit, @Credit)", conn, tx))
            {
                cmd.Parameters.AddWithValue("@EntryId", journalEntryId);
                cmd.Parameters.AddWithValue("@Code", accountCode);
                cmd.Parameters.AddWithValue("@Debit", debit);
                cmd.Parameters.AddWithValue("@Credit", credit);
                cmd.ExecuteNonQuery();
            }
        }

        // ==========================================================================
        // ترحيل: أعمدة مزامنة الكتالوج (CatalogSyncUrl/Secret/Enabled) لجدول StoreSettings -
        // بتحدد للبرنامج فين يبعت قائمة المنتجات لموقع الكتالوج البارة، ومفتاح التحقق.
        // ==========================================================================
        private static void EnsureStoreSettingsCatalogSyncColumns(SqliteConnection conn)
        {
            var existingColumns = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(StoreSettings);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                    existingColumns.Add(reader["name"].ToString());
            }

            if (!existingColumns.Contains("CatalogSyncUrl"))
                ExecuteNonQuery(conn, "ALTER TABLE StoreSettings ADD COLUMN CatalogSyncUrl TEXT;");
            if (!existingColumns.Contains("CatalogSyncSecret"))
                ExecuteNonQuery(conn, "ALTER TABLE StoreSettings ADD COLUMN CatalogSyncSecret TEXT;");
            if (!existingColumns.Contains("CatalogSyncEnabled"))
                ExecuteNonQuery(conn, "ALTER TABLE StoreSettings ADD COLUMN CatalogSyncEnabled INTEGER NOT NULL DEFAULT 0;");
            if (!existingColumns.Contains("WhatsAppNumber"))
                ExecuteNonQuery(conn, "ALTER TABLE StoreSettings ADD COLUMN WhatsAppNumber TEXT;");
        }

        // ==========================================================================
        // ترحيل: عمود ReceiptPrinterName لجدول StoreSettings - اسم طابعة الفواتير
        // (اللي الدرج متوصل بيها بكابل RJ11) عشان اختصار F8 يعرف يبعتلها نبضة الفتح.
        // ==========================================================================
        private static void EnsureStoreSettingsReceiptPrinterColumn(SqliteConnection conn)
        {
            bool columnExists = false;
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(StoreSettings);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), "ReceiptPrinterName", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
                ExecuteNonQuery(conn, "ALTER TABLE StoreSettings ADD COLUMN ReceiptPrinterName TEXT;");
        }

        // ==========================================================================
        // ترحيل: عمود SkipInventory لجدول PurchaseItems - بيميّز سطر "فاتورة مبلغ
        // إجمالي" (بدون تسجيل صنف بالمخزون) عن سطر صنف حقيقي باركوده فاضي (اللي
        // بيتولّده باركود تلقائي). من غير العمود ده الاتنين بيبانوا نفس الحاجة
        // (Barcode = NULL) ومفيش طريقة نفرّق بينهم لما الفاتورة تتفتح للتعديل.
        // ==========================================================================
        private static void EnsurePurchaseItemsSkipInventoryColumn(SqliteConnection conn)
        {
            bool columnExists = false;
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(PurchaseItems);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), "SkipInventory", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
                ExecuteNonQuery(conn, "ALTER TABLE PurchaseItems ADD COLUMN SkipInventory INTEGER NOT NULL DEFAULT 0;");
        }

        // ==========================================================================
        // ترحيل: عمود Theme لجدول StoreSettings - شكل الواجهة (Light/Dark) اللي المستخدم
        // اختاره من زر القمر 🌙 في كل الشاشات. بيتحفظ مرة واحدة على مستوى الجهاز كله
        // (مش لكل مستخدم) عشان كل الشاشات (اللي كل واحدة فيها BlazorWebView منفصل) تتفق
        // على نفس الشكل لحظة التنقل بينها.
        // ==========================================================================
        private static void EnsureStoreSettingsThemeColumn(SqliteConnection conn)
        {
            bool columnExists = false;
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(StoreSettings);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), "Theme", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
                ExecuteNonQuery(conn, "ALTER TABLE StoreSettings ADD COLUMN Theme TEXT NOT NULL DEFAULT 'light';");
        }

        // ==========================================================================
        // ترحيل بيانات (مرة واحدة، آمن يتنفذ أكتر من مرة - UPDATE عادي مش هيغيّر حاجة
        // لو مفيش صفوف باسم "سهولة" أصلًا): إعادة تسمية وسيلة الدفع "سهولة" إلى "ممكن"
        // في كل الجداول اللي بتسجل اسمها كنص (مش بس شجرة الحسابات). لازم يتنفذ في كل
        // الجداول دي مع بعض عشان القيم القديمة تتوحّد مع الاسم الجديد في القوائم
        // المنسدلة وربط الحساب المحاسبي (راجع AccountCodes.ForPaymentMethod).
        // ==========================================================================
        private static void RenamePaymentMethodSohoulaToMomken(SqliteConnection conn)
        {
            ExecuteNonQuery(conn, "UPDATE CashMovements SET PaymentMethod = 'ممكن' WHERE PaymentMethod = 'سهولة';");
            ExecuteNonQuery(conn, "UPDATE DailyClosures SET PaymentMethod = 'ممكن' WHERE PaymentMethod = 'سهولة';");
            ExecuteNonQuery(conn, "UPDATE Sales SET PaymentMethod = 'ممكن' WHERE PaymentMethod = 'سهولة';");
            ExecuteNonQuery(conn, "UPDATE Expenses SET PaymentMethod = 'ممكن' WHERE PaymentMethod = 'سهولة';");

            // PaymentMethodBalances مفتاحها الأساسي (Primary Key) هو اسم الوسيلة نفسه، وصف
            // "ممكن" برصيد صفر ممكن يكون اتزرع فيه بالفعل (راجع سيدنج PaymentMethodBalances
            // فوق - بيمشي على UIHelpers.PaymentMethods اللي بقى فيها "ممكن" دلوقتي) قبل ما
            // الترحيل ده يتنفذ. فبنتعامل مع الحالتين: لو "ممكن" لسه مش موجود، UPDATE بسيط بيسميه.
            // لو موجود بالفعل (برصيد صفر عادة)، بنضيف عليه رصيد "سهولة" القديم ونمسح صف "سهولة".
            decimal? sohoulaBalance = null;
            using (SqliteCommand cmd = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = 'سهولة';", conn))
            {
                object result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value) sohoulaBalance = Convert.ToDecimal(result);
            }

            if (sohoulaBalance.HasValue)
            {
                bool momkenExists;
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM PaymentMethodBalances WHERE PaymentMethod = 'ممكن';", conn))
                    momkenExists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;

                if (momkenExists)
                {
                    ExecuteNonQuery(conn, $"UPDATE PaymentMethodBalances SET CurrentBalance = CurrentBalance + ({sohoulaBalance.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}) WHERE PaymentMethod = 'ممكن';");
                    ExecuteNonQuery(conn, "DELETE FROM PaymentMethodBalances WHERE PaymentMethod = 'سهولة';");
                }
                else
                {
                    ExecuteNonQuery(conn, "UPDATE PaymentMethodBalances SET PaymentMethod = 'ممكن' WHERE PaymentMethod = 'سهولة';");
                }
            }
        }

        // ==========================================================================
        // ترحيل: عمود EmployeeId لجدول CashMovements (كان ناقص من الأول، عشان كده
        // سلف/دفعات المرتبات كانت هتتسجل من غير ما ترتبط بموظف معين).
        // ==========================================================================
        private static void EnsureCashMovementsEmployeeIdColumn(SqliteConnection conn)
        {
            bool columnExists = false;
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(CashMovements);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), "EmployeeId", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
                ExecuteNonQuery(conn, "ALTER TABLE CashMovements ADD COLUMN EmployeeId INTEGER;");
        }

        // ==========================================================================
        // ترحيل: عمود IsAdvance لجدول CashMovements - بيفرّق بين "دفعة من المستحق"
        // و"سلفة" لصرف المرتبات (كانت كلها نفس النوع من غير تمييز).
        // ==========================================================================
        private static void EnsureCashMovementsIsAdvanceColumn(SqliteConnection conn)
        {
            bool columnExists = false;
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(CashMovements);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), "IsAdvance", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
                ExecuteNonQuery(conn, "ALTER TABLE CashMovements ADD COLUMN IsAdvance INTEGER NOT NULL DEFAULT 0;");
        }

        // ==========================================================================
        // ترحيل: عمود PurchaseId لجدول CashMovements - بيربط سداد كاش فوري لفاتورة
        // شراء بالفاتورة نفسها، عشان لو الفاتورة اتلغت أو اتعدلت نقدر نلاقي حركة
        // الكاش بتاعتها ونعمل لها قيد عكسي بالظبط من غير ما نعتمد على تحليل نص.
        // ==========================================================================
        private static void EnsureCashMovementsPurchaseIdColumn(SqliteConnection conn)
        {
            bool columnExists = false;
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(CashMovements);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), "PurchaseId", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
                ExecuteNonQuery(conn, "ALTER TABLE CashMovements ADD COLUMN PurchaseId INTEGER;");
        }

        // ==========================================================================
        // ترحيل: عمود SaleId لجدول CashMovements - بيربط "قبض" عملية بيع كاش بالبيع
        // نفسه، عشان لو البيع اتعدّل أو اتلغى نقدر نلاقي حركة الكاش بتاعته ونعكسها.
        // ==========================================================================
        private static void EnsureCashMovementsSaleIdColumn(SqliteConnection conn)
        {
            bool columnExists = false;
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(CashMovements);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), "SaleId", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
                ExecuteNonQuery(conn, "ALTER TABLE CashMovements ADD COLUMN SaleId INTEGER;");
        }

        // ==========================================================================
        // ترحيل: عمود LinkedMovementId لجدول CashMovements - بيربط الحركتين اللي
        // بيسجلهم "التحويل بين وسائل الدفع" ببعض (صرف من وسيلة + قبض في وسيلة تانية)
        // عشان لو حد لغى واحدة منهم، الاتنين يترجعوا مع بعض بشكل صحيح.
        // ==========================================================================
        private static void EnsureCashMovementsLinkedMovementIdColumn(SqliteConnection conn)
        {
            bool columnExists = false;
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(CashMovements);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), "LinkedMovementId", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
                ExecuteNonQuery(conn, "ALTER TABLE CashMovements ADD COLUMN LinkedMovementId INTEGER;");
        }

        // ==========================================================================
        // ترحيل: عمود AdjustmentMovementId لجدول DailyClosures - بيربط أي فرق عجز/زيادة
        // اتسجل وقت الإقفال (فرق العدّ الفعلي عن المتوقع) بحركة الخزينة (CashMovements)
        // اللي اتسجلت بيه القيد المحاسبي المقابل، عشان لو حد فتح اليوم تاني نقدر نلغي
        // الحركة دي بالظبط (وتتعكس محاسبيًا) بدل ما نرجّع الرصيد يدويًا من غير أثر محاسبي.
        // ==========================================================================
        private static void EnsureDailyClosuresAdjustmentMovementIdColumn(SqliteConnection conn)
        {
            bool columnExists = false;
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(DailyClosures);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), "AdjustmentMovementId", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
                ExecuteNonQuery(conn, "ALTER TABLE DailyClosures ADD COLUMN AdjustmentMovementId INTEGER;");
        }

        // ==========================================================================
        // ترحيل: عمود PaymentMethod لجدول Sales - كان ناقص من الأول، عشان كده عملية
        // البيع الكاش ما كانتش بتأثر على رصيد أي وسيلة دفع في الخزينة خالص (باگ حقيقي:
        // الفلوس بتتقبض فعليًا بس الخزينة معندهاش أي فكرة). العمود ده بيحدد وسيلة
        // الدفع لأي بيع كاش عشان تقدر تتعدل/تتلغي بشكل صحيح لاحقًا.
        // ==========================================================================
        private static void EnsureSalesPaymentMethodColumn(SqliteConnection conn)
        {
            bool columnExists = false;
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(Sales);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), "PaymentMethod", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
                ExecuteNonQuery(conn, "ALTER TABLE Sales ADD COLUMN PaymentMethod TEXT;");
        }

        // ==========================================================================
        // ترحيل: عمود ProductImage لجدول Products (كان ناقص من الأول) - صورة المنتج (BLOB)،
        // نفس نمط StoreSettings.LogoImage بالظبط. المنتجات القديمة قيمتها NULL (بدون صورة)،
        // والشاشات اللي بتعرض المنتج بترجع لأيقونة افتراضية في الحالة دي.
        // ==========================================================================
        private static void EnsureProductsImageColumn(SqliteConnection conn)
        {
            bool columnExists = false;
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(Products);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), "ProductImage", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
                ExecuteNonQuery(conn, "ALTER TABLE Products ADD COLUMN ProductImage BLOB;");
        }

        // ==========================================================================
        // ترحيل: عمود Discount لجدول Sales (كان ناقص من الأول) - خصم حقيقي على مستوى الصنف
        // (وقد يشمل حصة من خصم سريع على مستوى الفاتورة اتوزّعت عليه وقت التسجيل). Sales.Total
        // يفضل زي ما هو بالظبط (السعر الإجمالي قبل الخصم) عشان أي تقرير قديم بيقرأ منه يفضل
        // صحيح - الصافي المطلوب فعليًا = Total - Discount، بيتحسب وقت الترحيل المحاسبي بس.
        // ==========================================================================
        private static void EnsureSalesDiscountColumn(SqliteConnection conn)
        {
            bool columnExists = false;
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(Sales);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), "Discount", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
                ExecuteNonQuery(conn, "ALTER TABLE Sales ADD COLUMN Discount REAL NOT NULL DEFAULT 0;");
        }

        // ==========================================================================
        // ترحيل: عمودي Tax وAmountPaid لجدول Sales - ضريبة حقيقية على مستوى الصنف (زي
        // Discount بالظبط) + مبلغ "المدفوع" الفعلي على مستوى الفاتورة (لحساب الفكة في
        // البيع الكاش). الفواتير القديمة قيمتها 0 افتراضيًا (بدون ضريبة، ومدفوع = صفر
        // لحد ما يتسجل بيع جديد فعليًا بيها).
        // ==========================================================================
        private static void EnsureSalesTaxAndPaymentColumns(SqliteConnection conn)
        {
            bool taxExists = false;
            bool amountPaidExists = false;
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(Sales);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string name = reader["name"].ToString() ?? "";
                    if (string.Equals(name, "Tax", StringComparison.OrdinalIgnoreCase))
                        taxExists = true;
                    else if (string.Equals(name, "AmountPaid", StringComparison.OrdinalIgnoreCase))
                        amountPaidExists = true;
                }
            }

            if (!taxExists)
                ExecuteNonQuery(conn, "ALTER TABLE Sales ADD COLUMN Tax REAL NOT NULL DEFAULT 0;");
            if (!amountPaidExists)
                ExecuteNonQuery(conn, "ALTER TABLE Sales ADD COLUMN AmountPaid REAL NOT NULL DEFAULT 0;");
        }

        // ==========================================================================
        // ترحيل: عمود SalesInvoiceId لجدول Sales - بيربط عدة أصناف (صفوف) في نفس عملية
        // البيع تحت رقم فاتورة واحد مشترك (بدل ما كل صنف ياخد رقم فاتورة منفصل زي الأول).
        // القيمة = SaleID بتاع أول صنف اتسجل في الفاتورة نفسها. المبيعات القديمة قبل
        // التحديث ده كانت أصلًا صنف واحد بالظبط لكل فاتورة، فبنعتبر كل صف فاتورة مستقلة
        // بنفسه (SalesInvoiceId = SaleID نفسه) عشان ترقيم الفواتير القديم يفضل زي ما هو.
        // ==========================================================================
        private static void EnsureSalesInvoiceIdColumn(SqliteConnection conn)
        {
            bool columnExists = false;
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(Sales);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), "SalesInvoiceId", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
                ExecuteNonQuery(conn, "ALTER TABLE Sales ADD COLUMN SalesInvoiceId INTEGER;");

            ExecuteNonQuery(conn, "UPDATE Sales SET SalesInvoiceId = SaleID WHERE SalesInvoiceId IS NULL;");
        }

        // ==========================================================================
        // ترحيل: عمود StandardHoursPerDay لجدول Employees (كان ناقص من الأول) - ساعات
        // العمل القياسية في اليوم لكل موظف، بتتسجل يدويًا لكل موظف عشان تختلف من واحد
        // للتاني، ومنها بيتحسب "قيمة الساعة" = قيمة اليوم ÷ الرقم ده.
        // ==========================================================================
        private static void EnsureEmployeesStandardHoursPerDayColumn(SqliteConnection conn)
        {
            bool columnExists = false;
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(Employees);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), "StandardHoursPerDay", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
                ExecuteNonQuery(conn, "ALTER TABLE Employees ADD COLUMN StandardHoursPerDay REAL NOT NULL DEFAULT 8;");
        }

        // ==========================================================================
        // ترحيل: عمود OvertimeHours لجدول AttendanceRecords (كان ناقص من الأول) - عدد
        // ساعات العمل الإضافي في يوم معين، بيتسجل مع الحالة (حاضر/غايب/إجازة) وقت
        // تسجيل الحضور اليومي.
        // ==========================================================================
        private static void EnsureAttendanceRecordsOvertimeHoursColumn(SqliteConnection conn)
        {
            bool columnExists = false;
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(AttendanceRecords);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), "OvertimeHours", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
                ExecuteNonQuery(conn, "ALTER TABLE AttendanceRecords ADD COLUMN OvertimeHours REAL NOT NULL DEFAULT 0;");
        }

        // ==========================================================================
        // ترحيل: عمودي OvertimeHours و OvertimeAmount لجدول PayrollClosures - عشان لما
        // شهر يتقفل، قيمة الإضافي المحسوبة تتجمد مع باقي أرقام الشهر نهائيًا زي كل حاجة تانية.
        // ==========================================================================
        private static void EnsurePayrollClosuresOvertimeColumns(SqliteConnection conn)
        {
            var existingColumns = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(PayrollClosures);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                    existingColumns.Add(reader["name"].ToString());
            }

            if (!existingColumns.Contains("OvertimeHours"))
                ExecuteNonQuery(conn, "ALTER TABLE PayrollClosures ADD COLUMN OvertimeHours REAL NOT NULL DEFAULT 0;");
            if (!existingColumns.Contains("OvertimeAmount"))
                ExecuteNonQuery(conn, "ALTER TABLE PayrollClosures ADD COLUMN OvertimeAmount REAL NOT NULL DEFAULT 0;");
        }

        // ==========================================================================
        // ترحيل: عمود PaymentMethod لجدول Expenses (كان ناقص من الأول، عشان كده
        // تسجيل مصروف كان بيسجل بس من غير ما يأثر على رصيد أي وسيلة دفع). بيضيف
        // العمود لو مش موجود، وبيربط أي مصروفات قديمة بوسيلة "نقدي" مع خصم
        // إجماليها مرة واحدة بس من رصيدها (مايتكررش في المرات الجاية، لأن
        // بعد أول مرة كل الصفوف بيبقى ليها PaymentMethod محدد).
        // ==========================================================================
        private static void EnsureExpensesPaymentMethodColumn(SqliteConnection conn)
        {
            bool columnExists = false;
            using (SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(Expenses);", conn))
            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), "PaymentMethod", StringComparison.OrdinalIgnoreCase))
                    {
                        columnExists = true;
                        break;
                    }
                }
            }

            if (!columnExists)
                ExecuteNonQuery(conn, "ALTER TABLE Expenses ADD COLUMN PaymentMethod TEXT;");

            decimal unassignedTotal = 0;
            using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM Expenses WHERE PaymentMethod IS NULL;", conn))
            {
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value) unassignedTotal = Convert.ToDecimal(result);
            }

            if (unassignedTotal == 0) return;

            using (SqliteCommand cmd = new SqliteCommand("UPDATE Expenses SET PaymentMethod = 'نقدي' WHERE PaymentMethod IS NULL;", conn))
                cmd.ExecuteNonQuery();

            using (SqliteCommand cmd = new SqliteCommand(
                "UPDATE PaymentMethodBalances SET CurrentBalance = CurrentBalance - @Total WHERE PaymentMethod = 'نقدي';", conn))
            {
                cmd.Parameters.AddWithValue("@Total", unassignedTotal);
                cmd.ExecuteNonQuery();
            }
        }

        private static void ExecuteNonQuery(SqliteConnection conn, string sql)
        {
            using (SqliteCommand cmd = new SqliteCommand(sql, conn))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }
}
