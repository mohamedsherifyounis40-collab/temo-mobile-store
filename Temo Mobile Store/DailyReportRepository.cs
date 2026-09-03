using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    public class DailyReportSummary
    {
        public decimal NetSales;
        public int InvoiceCount;
        public decimal NetProfit;
        public int ReturnsCount;
        public decimal ReturnsAmount;
        public decimal? SalesTrendPct;     // null لو مفيش بيانات كافية للمقارنة (مثلًا أمس = صفر)
        public int InvoiceTrendDelta;
        public decimal? ProfitTrendPct;
    }

    public class DailyReportBalances
    {
        // رصيد كل وسيلة دفع لوحدها بالظبط زي "أرصدة وسائل الدفع" في الداشبورد الرئيسي -
        // بترتيب UIHelpers.PaymentMethods نفسه (نقدي، فوري، أمان، ممكن، فودافون كاش، إنستاباي)
        public List<(string Method, decimal Balance)> MethodBalances = new();
        public decimal TotalMethodBalance;
        public decimal CustomersReceivable;
        public decimal InventoryValueAtCost;
    }

    public class DailyMovementRow
    {
        public DateTime Timestamp;
        public string Type = "";           // بيع / شراء / صيانة / مرتجع / مصروف / سداد مورد / تحصيل من عميل / تحويل / مرتبات
        public string Description = "";
        public string? SerialOrImei;
        public string PaymentMethod = "";
        public string Employee = "";
        public decimal Amount;             // موجب = وارد، سالب = منصرف
    }

    public class DailyReportData
    {
        public DateTime Date;
        public DailyReportSummary Summary = new();
        public DailyReportBalances Balances = new();
        // false لو التاريخ المطلوب قبل تاريخ "تسوية الخزينة التاريخية" (TreasuryReconciliationMarker) -
        // يعني الدفتر المحاسبي قبل التاريخ ده اتبنى بأثر رجعي (BackfillHistoricalJournalEntries)
        // وبيعكس "الحالة النهائية" مش كل تعديل حصل وقتها بالظبط، فحساب "الرصيد كما كان" من
        // الدفتر لوحده مش موثوق قبل التسوية دي - لازم نعرض تحذير بدل رقم يوهم بالدقة
        public bool BalancesReliable = true;
        public List<DailyMovementRow> Movements = new();
        public int MovementCount => Movements.Count;
        public decimal TotalIn => Movements.Sum(m => m.Amount > 0 ? m.Amount : 0);
        public decimal TotalOut => Movements.Sum(m => m.Amount < 0 ? -m.Amount : 0);
        public decimal NetTotal => TotalIn - TotalOut;
    }

    // ==========================================================================
    // DailyReportRepository: بيانات تقرير "الحركات اليومية والأرصدة" - نفس فكرة
    // DailySummaryPrintHelper القديمة، لكن موسّعة لتقبل أي تاريخ (مش النهارده بس) ولتشمل
    // كل أنواع الحركات (بيع/شراء/صيانة/مرتجع) في جدول موحّد، بالإضافة لأرصدة حقيقية
    // "كما كانت وقتها" لو التاريخ المطلوب يوم قديم - مش الرصيد الحالي دايمًا.
    //
    // أكواد الحسابات (1100..1150 وسائل الدفع، 1200 المخزون، 1300 العملاء) منسوخة يدويًا هنا
    // من TemoStore.Engines.Handlers.AccountCodes (internal في مشروع تاني، مش متاحة من هنا) -
    // لازم تتزامن يدويًا لو الأكواد دي اتغيرت هناك.
    // ==========================================================================
    public static class DailyReportRepository
    {
        private static readonly (string Method, int Code)[] PaymentMethodCodes =
        {
            ("نقدي", 1100), ("فوري", 1110), ("أمان", 1120), ("ممكن", 1130), ("فودافون كاش", 1140), ("إنستاباي", 1150)
        };
        private const int AccountInventory = 1200;
        private const int AccountCustomers = 1300;
        private const int AccountSuppliers = 2100;
        private const int AccountMaintenanceRevenue = 4200;

        public static DailyReportData GetReport(DateTime date)
        {
            var report = new DailyReportData { Date = date.Date };
            string dateStr = date.ToString("yyyy-MM-dd");
            string prevDateStr = date.AddDays(-1).ToString("yyyy-MM-dd");

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                var (sales, cogs, invoices) = GetDayTotals(conn, dateStr);
                decimal expenses = GetSumScalar(conn, "SELECT SUM(Amount) FROM Expenses WHERE date(ExpenseDate) = date(@D)", dateStr);
                var (returnsCount, returnsAmount) = GetDayReturnsTotals(conn, dateStr);

                var (prevSales, prevCogs, prevInvoices) = GetDayTotals(conn, prevDateStr);
                decimal prevExpenses = GetSumScalar(conn, "SELECT SUM(Amount) FROM Expenses WHERE date(ExpenseDate) = date(@D)", prevDateStr);
                decimal profit = sales - cogs - expenses;
                decimal prevProfit = prevSales - prevCogs - prevExpenses;

                report.Summary = new DailyReportSummary
                {
                    NetSales = sales,
                    InvoiceCount = invoices,
                    NetProfit = profit,
                    ReturnsCount = returnsCount,
                    ReturnsAmount = returnsAmount,
                    SalesTrendPct = prevSales > 0 ? Math.Round((sales - prevSales) / prevSales * 100, 1) : (decimal?)null,
                    InvoiceTrendDelta = invoices - prevInvoices,
                    ProfitTrendPct = prevProfit > 0 ? Math.Round((profit - prevProfit) / prevProfit * 100, 1) : (decimal?)null,
                };

                var methodBalances = new List<(string Method, decimal Balance)>();
                decimal totalMethodBalance = 0;
                foreach (var (method, code) in PaymentMethodCodes)
                {
                    decimal balance = GetAccountBalanceAsOf(conn, new[] { code }, dateStr);
                    methodBalances.Add((method, balance));
                    totalMethodBalance += balance;
                }
                decimal customers = GetAccountBalanceAsOf(conn, new[] { AccountCustomers }, dateStr);
                decimal inventory = GetAccountBalanceAsOf(conn, new[] { AccountInventory }, dateStr);
                report.Balances = new DailyReportBalances
                {
                    MethodBalances = methodBalances,
                    TotalMethodBalance = totalMethodBalance,
                    CustomersReceivable = customers,
                    InventoryValueAtCost = inventory,
                };
                report.BalancesReliable = IsDateAfterTreasuryReconciliation(conn, dateStr);

                report.Movements.AddRange(GetSalesMovements(conn, dateStr));
                report.Movements.AddRange(GetReturnMovements(conn, dateStr));
                report.Movements.AddRange(GetPurchaseMovements(conn, dateStr));
                report.Movements.AddRange(GetMaintenanceMovements(conn, dateStr));
                report.Movements.AddRange(GetExpenseMovements(conn, dateStr));
                report.Movements.AddRange(GetTreasuryMovements(conn, dateStr));
                report.Movements.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            }

            return report;
        }

        private static (decimal Sales, decimal Cogs, int Invoices) GetDayTotals(SqliteConnection conn, string dateStr)
        {
            using var cmd = new SqliteCommand(
                "SELECT SUM(Total - Discount + Tax) AS T, SUM(CostPrice * QuantitySold) AS C, COUNT(DISTINCT SalesInvoiceId) AS N FROM Sales WHERE date(SaleDate) = date(@D)", conn);
            cmd.Parameters.AddWithValue("@D", dateStr);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                decimal sales = r["T"] == DBNull.Value ? 0 : Convert.ToDecimal(r["T"]);
                decimal cogs = r["C"] == DBNull.Value ? 0 : Convert.ToDecimal(r["C"]);
                int count = r["N"] == DBNull.Value ? 0 : Convert.ToInt32(r["N"]);
                return (sales, cogs, count);
            }
            return (0, 0, 0);
        }

        private static (int Count, decimal Amount) GetDayReturnsTotals(SqliteConnection conn, string dateStr)
        {
            using var cmd = new SqliteCommand("SELECT COUNT(*) AS N, SUM(RefundAmount) AS T FROM SalesReturns WHERE date(ReturnDate) = date(@D)", conn);
            cmd.Parameters.AddWithValue("@D", dateStr);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                int count = r["N"] == DBNull.Value ? 0 : Convert.ToInt32(r["N"]);
                decimal amount = r["T"] == DBNull.Value ? 0 : Convert.ToDecimal(r["T"]);
                return (count, amount);
            }
            return (0, 0);
        }

        private static decimal GetSumScalar(SqliteConnection conn, string sql, string dateStr)
        {
            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@D", dateStr);
            var res = cmd.ExecuteScalar();
            return (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
        }

        // بيتأكد إن التاريخ المطلوب بعد (أو يوم) تاريخ آخر تسوية خزينة تاريخية (TreasuryReconciliationMarker
        // في DatabaseManager.ReconcileTreasuryLedgerHistoricalGap) - قبل التسوية دي الدفتر المحاسبي
        // اتبنى بأثر رجعي (BackfillHistoricalJournalEntries) وبيعكس "الحالة النهائية دلوقتي" لكل عملية
        // قديمة مش كل تعديل حصل وقتها بالظبط، فحساب "الرصيد كما كان" من الدفتر مش موثوق قبل التسوية دي.
        // لو مفيش تسوية اتسجلت خالص (تركيب جديد من الأول)، كل التواريخ موثوقة.
        private static bool IsDateAfterTreasuryReconciliation(SqliteConnection conn, string dateStr)
        {
            using var cmd = new SqliteCommand("SELECT MAX(EntryDate) FROM JournalEntries WHERE SourceType = 'TreasuryReconciliationMarker'", conn);
            var res = cmd.ExecuteScalar();
            if (res == null || res == DBNull.Value) return true;
            string reconciliationDate = res.ToString()!;
            return string.Compare(dateStr, reconciliationDate, StringComparison.Ordinal) >= 0;
        }

        // رصيد حساب (أو مجموعة حسابات) من دفتر اليومية الحقيقي "كما كان" حتى نهاية تاريخ معين
        // (شامل) - نفس مبدأ IJournalRepository.GetAccountBalance بالظبط، بس بحد تاريخ. ده
        // اللي بيخلي "الأرصدة" في التقرير صحيحة تاريخيًا لو المستخدم اختار يوم قديم، مش
        // بس الرصيد الحالي النهارده.
        private static decimal GetAccountBalanceAsOf(SqliteConnection conn, int[] accountCodes, string asOfDateStr)
        {
            string placeholders = string.Join(",", accountCodes);
            using var cmd = new SqliteCommand(
                $@"SELECT COALESCE(SUM(JL.Debit),0) - COALESCE(SUM(JL.Credit),0)
                   FROM JournalLines JL
                   JOIN JournalEntries JE ON JL.JournalEntryId = JE.JournalEntryId
                   WHERE JL.AccountCode IN ({placeholders}) AND JE.EntryDate <= @D", conn);
            cmd.Parameters.AddWithValue("@D", asOfDateStr);
            var res = cmd.ExecuteScalar();
            return (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
        }

        private static List<DailyMovementRow> GetSalesMovements(SqliteConnection conn, string dateStr)
        {
            var rows = new List<DailyMovementRow>();
            using var cmd = new SqliteCommand(@"
                SELECT S.SaleDate, S.ProductName, S.IMEI, S.PaymentType, S.PaymentMethod, S.SalesInvoiceId,
                       (S.Total - S.Discount + S.Tax) AS NetAmount,
                       (SELECT CreatedBy FROM JournalEntries WHERE SourceType = 'Sale' AND SourceId = S.SalesInvoiceId ORDER BY JournalEntryId LIMIT 1) AS Employee
                FROM Sales S
                WHERE date(S.SaleDate) = date(@D)
                ORDER BY S.SaleDate", conn);
            cmd.Parameters.AddWithValue("@D", dateStr);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                bool isCredit = reader["PaymentType"] != DBNull.Value && reader["PaymentType"].ToString() == "Credit";
                rows.Add(new DailyMovementRow
                {
                    Timestamp = DateTime.Parse(reader["SaleDate"].ToString()!),
                    Type = "بيع",
                    Description = reader["ProductName"].ToString() ?? "",
                    SerialOrImei = reader["IMEI"] == DBNull.Value ? null : reader["IMEI"].ToString(),
                    PaymentMethod = isCredit ? "آجل" : (reader["PaymentMethod"] == DBNull.Value ? "نقدي" : reader["PaymentMethod"].ToString()!),
                    Employee = reader["Employee"] == DBNull.Value ? "" : reader["Employee"].ToString() ?? "",
                    Amount = Convert.ToDecimal(reader["NetAmount"]),
                });
            }
            return rows;
        }

        private static List<DailyMovementRow> GetReturnMovements(SqliteConnection conn, string dateStr)
        {
            var rows = new List<DailyMovementRow>();
            using var cmd = new SqliteCommand(@"
                SELECT R.ReturnDate, R.ProductName, R.PerformedBy, R.PaymentType, R.PaymentMethod, R.RefundAmount, S.IMEI
                FROM SalesReturns R
                LEFT JOIN Sales S ON R.SaleId = S.SaleID
                WHERE date(R.ReturnDate) = date(@D)
                ORDER BY R.ReturnDate", conn);
            cmd.Parameters.AddWithValue("@D", dateStr);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                bool isCredit = reader["PaymentType"] != DBNull.Value && reader["PaymentType"].ToString() == "Credit";
                rows.Add(new DailyMovementRow
                {
                    Timestamp = DateTime.Parse(reader["ReturnDate"].ToString()!),
                    Type = "مرتجع",
                    Description = reader["ProductName"].ToString() ?? "",
                    SerialOrImei = reader["IMEI"] == DBNull.Value ? null : reader["IMEI"].ToString(),
                    PaymentMethod = isCredit ? "آجل" : (reader["PaymentMethod"] == DBNull.Value ? "نقدي" : reader["PaymentMethod"].ToString()!),
                    Employee = reader["PerformedBy"] == DBNull.Value ? "" : reader["PerformedBy"].ToString() ?? "",
                    Amount = -Convert.ToDecimal(reader["RefundAmount"]),
                });
            }
            return rows;
        }

        private static List<DailyMovementRow> GetPurchaseMovements(SqliteConnection conn, string dateStr)
        {
            var rows = new List<DailyMovementRow>();
            using var cmd = new SqliteCommand(@"
                SELECT P.PurchaseId, P.PurchaseDate, PI.ProductName, PI.LineTotal, PI.Barcode
                FROM PurchaseItems PI
                JOIN Purchases P ON PI.PurchaseId = P.PurchaseId
                WHERE date(P.PurchaseDate) = date(@D) AND PI.SkipInventory = 0
                ORDER BY P.PurchaseDate", conn);
            cmd.Parameters.AddWithValue("@D", dateStr);

            var pending = new List<(int PurchaseId, DateTime Time, string ProductName, decimal LineTotal, string? Barcode)>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    pending.Add((
                        Convert.ToInt32(reader["PurchaseId"]),
                        DateTime.Parse(reader["PurchaseDate"].ToString()!),
                        reader["ProductName"].ToString() ?? "",
                        Convert.ToDecimal(reader["LineTotal"]),
                        reader["Barcode"] == DBNull.Value ? null : reader["Barcode"].ToString()));
                }
            }

            foreach (var line in pending)
            {
                var (employee, paymentMethod) = GetSourceAttribution(conn, new[] { "Purchase", "PurchaseEdit" }, line.PurchaseId);
                string? imei = string.IsNullOrEmpty(line.Barcode) ? null : GetImeiForPurchaseLine(conn, line.PurchaseId, line.Barcode);
                rows.Add(new DailyMovementRow
                {
                    Timestamp = line.Time,
                    Type = "شراء",
                    Description = line.ProductName,
                    SerialOrImei = imei,
                    PaymentMethod = paymentMethod,
                    Employee = employee,
                    Amount = -line.LineTotal,
                });
            }
            return rows;
        }

        private static string? GetImeiForPurchaseLine(SqliteConnection conn, int purchaseId, string barcode)
        {
            using var cmd = new SqliteCommand("SELECT IMEI FROM ProductUnits WHERE PurchaseId = @P AND Barcode = @B LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@P", purchaseId);
            cmd.Parameters.AddWithValue("@B", barcode);
            var res = cmd.ExecuteScalar();
            return (res != null && res != DBNull.Value) ? res.ToString() : null;
        }

        private static List<DailyMovementRow> GetMaintenanceMovements(SqliteConnection conn, string dateStr)
        {
            var rows = new List<DailyMovementRow>();
            using var cmd = new SqliteCommand(@"
                SELECT TicketId, DeliveredDate, IssueDescription, DeviceInfo, ActualCost
                FROM MaintenanceTickets
                WHERE DeliveredDate IS NOT NULL AND date(DeliveredDate) = date(@D)
                ORDER BY DeliveredDate", conn);
            cmd.Parameters.AddWithValue("@D", dateStr);

            var pending = new List<(int TicketId, DateTime Time, string Description, decimal ActualCost)>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string issue = reader["IssueDescription"] == DBNull.Value ? "" : reader["IssueDescription"].ToString() ?? "";
                    string device = reader["DeviceInfo"] == DBNull.Value ? "" : reader["DeviceInfo"].ToString() ?? "";
                    string description = string.IsNullOrWhiteSpace(issue) ? device : $"{issue} - {device}";
                    decimal actualCost = reader["ActualCost"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["ActualCost"]);
                    pending.Add((Convert.ToInt32(reader["TicketId"]), DateTime.Parse(reader["DeliveredDate"].ToString()!), description, actualCost));
                }
            }

            foreach (var t in pending)
            {
                var (employee, paymentMethod) = GetSourceAttribution(conn, new[] { "MaintenanceDelivery" }, t.TicketId);
                rows.Add(new DailyMovementRow
                {
                    Timestamp = t.Time,
                    Type = "صيانة",
                    Description = t.Description,
                    SerialOrImei = null,
                    PaymentMethod = t.ActualCost > 0 ? paymentMethod : "—",
                    Employee = employee,
                    Amount = t.ActualCost,
                });
            }
            return rows;
        }

        private static List<DailyMovementRow> GetExpenseMovements(SqliteConnection conn, string dateStr)
        {
            var rows = new List<DailyMovementRow>();
            using var cmd = new SqliteCommand(@"
                SELECT E.ExpenseID, E.ExpenseDate, E.Amount, E.PaymentMethod, A.AccountName,
                       (SELECT CreatedBy FROM JournalEntries WHERE SourceType = 'Expense' AND SourceId = E.ExpenseID ORDER BY JournalEntryId LIMIT 1) AS Employee
                FROM Expenses E
                LEFT JOIN AccountsTree A ON A.AccountCode = E.AccountCode
                WHERE date(E.ExpenseDate) = date(@D)
                ORDER BY E.ExpenseDate", conn);
            cmd.Parameters.AddWithValue("@D", dateStr);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new DailyMovementRow
                {
                    Timestamp = DateTime.Parse(reader["ExpenseDate"].ToString()!),
                    Type = "مصروف",
                    Description = reader["AccountName"] == DBNull.Value ? "مصروف عمومي" : reader["AccountName"].ToString()!,
                    SerialOrImei = null,
                    PaymentMethod = reader["PaymentMethod"] == DBNull.Value ? "نقدي" : reader["PaymentMethod"].ToString()!,
                    Employee = reader["Employee"] == DBNull.Value ? "" : reader["Employee"].ToString() ?? "",
                    Amount = -Convert.ToDecimal(reader["Amount"]),
                });
            }
            return rows;
        }

        // باقي حركات الخزينة (CashMovements) اللي مش بيع/شراء/صيانة - سداد مورد، تحصيل من
        // عميل، تحويل بين وسائل، مرتبات/سلف موظفين. كل عملية في النظام بتعدي من
        // ICashDrawerEngine.Credit/Debit فبتتسجل هنا (ما عدا المصروفات العمومية - دي بالذات
        // بتتعامل مباشرة مع الرصيد من غير صف CashMovements، عشان كده ليها Query منفصل فوق).
        // بنستبعد أي صف مرتبط ببيع/شراء (SaleId/PurchaseId) أو صيانة (AccountCode=4200)
        // عشان دول متغطّيين من الـ Queries التانية فوق بالفعل، وميتكرروش هنا.
        private static List<DailyMovementRow> GetTreasuryMovements(SqliteConnection conn, string dateStr)
        {
            var rows = new List<DailyMovementRow>();
            using var cmd = new SqliteCommand(@"
                SELECT CM.CreatedAt, CM.MovementType, CM.PaymentMethod, CM.Amount, CM.Description,
                       CM.CustomerId, CM.SupplierId, CM.EmployeeId, CM.LinkedMovementId,
                       C.CustomerName, S.SupplierName
                FROM CashMovements CM
                LEFT JOIN Customers C ON CM.CustomerId = C.CustomerId
                LEFT JOIN Suppliers S ON CM.SupplierId = S.SupplierId
                WHERE date(CM.CreatedAt) = date(@D)
                  AND CM.SaleId IS NULL AND CM.PurchaseId IS NULL
                  AND (CM.AccountCode IS NULL OR CM.AccountCode != @MaintCode)
                ORDER BY CM.CreatedAt", conn);
            cmd.Parameters.AddWithValue("@D", dateStr);
            cmd.Parameters.AddWithValue("@MaintCode", AccountMaintenanceRevenue);

            var pending = new List<(DateTime Time, string MovementType, string PaymentMethod, decimal Amount, string Description,
                int? CustomerId, int? SupplierId, int? EmployeeId, bool IsLinkedTransfer, string? CustomerName, string? SupplierName)>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    pending.Add((
                        DateTime.Parse(reader["CreatedAt"].ToString()!),
                        reader["MovementType"].ToString() ?? "",
                        reader["PaymentMethod"].ToString() ?? "",
                        Convert.ToDecimal(reader["Amount"]),
                        reader["Description"] == DBNull.Value ? "" : reader["Description"].ToString() ?? "",
                        reader["CustomerId"] == DBNull.Value ? null : Convert.ToInt32(reader["CustomerId"]),
                        reader["SupplierId"] == DBNull.Value ? null : Convert.ToInt32(reader["SupplierId"]),
                        reader["EmployeeId"] == DBNull.Value ? null : Convert.ToInt32(reader["EmployeeId"]),
                        reader["LinkedMovementId"] != DBNull.Value,
                        reader["CustomerName"] == DBNull.Value ? null : reader["CustomerName"].ToString(),
                        reader["SupplierName"] == DBNull.Value ? null : reader["SupplierName"].ToString()));
                }
            }

            foreach (var m in pending)
            {
                string type;
                string description;
                string[] sourceTypes;
                if (m.SupplierId != null) { type = "سداد مورد"; description = m.SupplierName ?? m.Description; sourceTypes = new[] { "SupplierPayment" }; }
                else if (m.CustomerId != null) { type = "تحصيل من عميل"; description = m.CustomerName ?? m.Description; sourceTypes = new[] { "CustomerPayment" }; }
                else if (m.EmployeeId != null) { type = "مرتبات"; description = m.Description; sourceTypes = new[] { "EmployeeAdvance", "EmployeePayment" }; }
                else if (m.IsLinkedTransfer) { type = "تحويل"; description = m.Description; sourceTypes = new[] { "Transfer" }; }
                // حركة قبض/صرف يدوية عامة (AddMovementCommand) - مش مربوطة بعميل/مورد/موظف
                // ومش نص تحويل مرتبط، بس ممكن يكون ليها بند حساب اختياري (قيد SourceType='Movement')
                else { type = "حركة يدوية"; description = string.IsNullOrWhiteSpace(m.Description) ? "حركة قبض/صرف" : m.Description; sourceTypes = new[] { "Movement" }; }

                string employee = GetEmployeeByNearestJournalEntry(conn, sourceTypes, dateStr, m.Time);
                decimal signedAmount = m.MovementType == "صرف" ? -m.Amount : m.Amount;

                rows.Add(new DailyMovementRow
                {
                    Timestamp = m.Time,
                    Type = type,
                    Description = description,
                    SerialOrImei = null,
                    PaymentMethod = m.PaymentMethod,
                    Employee = employee,
                    Amount = signedAmount,
                });
            }
            return rows;
        }

        // بيدوّر على أقرب قيد محاسبي (بالوقت) من نوع/أنواع معينة في نفس اليوم - مستخدم هنا
        // لأن CashMovements معندهاش عمود "مين نفّذ العملية" مباشرة، والربط بمصدر واحد
        // (SourceId) مش كافي لوحده لو نفس المورد/العميل/الموظف عليه أكتر من عملية في نفس اليوم
        private static string GetEmployeeByNearestJournalEntry(SqliteConnection conn, string[] sourceTypes, string dateStr, DateTime movementTime)
        {
            string placeholders = string.Join(",", sourceTypes.Select((_, i) => $"@T{i}"));
            using var cmd = new SqliteCommand(
                $@"SELECT CreatedBy FROM JournalEntries
                   WHERE SourceType IN ({placeholders}) AND date(CreatedAt) = date(@D)
                   ORDER BY ABS(strftime('%s', CreatedAt) - strftime('%s', @T)) LIMIT 1", conn);
            for (int i = 0; i < sourceTypes.Length; i++) cmd.Parameters.AddWithValue($"@T{i}", sourceTypes[i]);
            cmd.Parameters.AddWithValue("@D", dateStr);
            cmd.Parameters.AddWithValue("@T", movementTime.ToString("yyyy-MM-dd HH:mm:ss"));
            var res = cmd.ExecuteScalar();
            return (res != null && res != DBNull.Value) ? res.ToString()! : "";
        }

        // بيرجّع (اسم الموظف، طريقة الدفع) لآخر قيد محاسبي مرتبط بمصدر معين - طريقة الدفع
        // بتتحدد من كود الحساب اللي اتقيّد عليه المبلغ (كاش/بنك = وسيلة دفع حقيقية، موردين = "آجل")
        private static (string Employee, string PaymentMethod) GetSourceAttribution(SqliteConnection conn, string[] sourceTypes, int sourceId)
        {
            string placeholders = string.Join(",", sourceTypes.Select((_, i) => $"@T{i}"));
            using var cmd = new SqliteCommand(
                $"SELECT JournalEntryId, CreatedBy FROM JournalEntries WHERE SourceType IN ({placeholders}) AND SourceId = @Id ORDER BY JournalEntryId DESC LIMIT 1", conn);
            for (int i = 0; i < sourceTypes.Length; i++) cmd.Parameters.AddWithValue($"@T{i}", sourceTypes[i]);
            cmd.Parameters.AddWithValue("@Id", sourceId);

            int? entryId = null;
            string employee = "";
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    entryId = Convert.ToInt32(reader["JournalEntryId"]);
                    employee = reader["CreatedBy"] == DBNull.Value ? "" : reader["CreatedBy"].ToString() ?? "";
                }
            }
            if (entryId == null) return ("", "—");

            using var cmdLines = new SqliteCommand("SELECT AccountCode, Credit FROM JournalLines WHERE JournalEntryId = @E AND Credit > 0", conn);
            cmdLines.Parameters.AddWithValue("@E", entryId.Value);
            using var linesReader = cmdLines.ExecuteReader();
            while (linesReader.Read())
            {
                int code = Convert.ToInt32(linesReader["AccountCode"]);
                if (code == AccountSuppliers) return (employee, "آجل");
                foreach (var (method, methodCode) in PaymentMethodCodes)
                    if (code == methodCode) return (employee, method);
            }
            return (employee, "—");
        }
    }
}
