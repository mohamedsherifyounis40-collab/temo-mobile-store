using Microsoft.Data.Sqlite;
using System;
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
                    SaleDate TEXT NOT NULL DEFAULT (datetime('now','localtime'))
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
                    LineTotal REAL NOT NULL DEFAULT 0
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
                    SupplierId INTEGER
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS PaymentMethodBalances (
                    PaymentMethod TEXT PRIMARY KEY,
                    CurrentBalance REAL NOT NULL DEFAULT 0
                );");

                ExecuteNonQuery(conn, @"CREATE TABLE IF NOT EXISTS AccountsTree (
                    AccountCode INTEGER PRIMARY KEY,
                    AccountName TEXT NOT NULL
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
                    LogoImage BLOB
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
                    (2100, "موردون (ذمم دائنة)"),
                    (4100, "إيراد المبيعات"),
                    (4200, "إيراد الصيانة"),
                    (5100, "مصروفات عمومية وإدارية"),
                    (5200, "إيجار"),
                    (5300, "كهرباء ومياه"),
                    (5400, "مرتبات"),
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
