using TemoStore.Core.Entities;

namespace TemoStore.Core.Abstractions
{
    // بيلف Transaction واحد لقاعدة البيانات كاملة - كل الـ Repositories تحت بيشتركوا في
    // نفس الـ Connection/Transaction، عشان أي عملية (بيع/شراء/...) تتنفذ كوحدة واحدة:
    // لو أي خطوة فشلت، Rollback() بيرجع كل حاجة، ومفيش عملية نصف منفذة أبدًا.
    public interface IUnitOfWork : IDisposable
    {
        IProductRepository Products { get; }
        ISaleRepository Sales { get; }
        IPurchaseRepository Purchases { get; }
        ICashDrawerRepository CashDrawer { get; }
        ICustomerRepository Customers { get; }
        ISupplierRepository Suppliers { get; }
        IJournalRepository Journal { get; }
        IAuditRepository Audit { get; }
        IExpenseRepository Expenses { get; }
        IInventoryAdjustmentRepository InventoryAdjustments { get; }
        IMaintenanceRepository Maintenance { get; }
        IEmployeeRepository Employees { get; }
        IAttendanceRepository Attendance { get; }
        IAccountRepository Accounts { get; }
        IClosureRepository Closures { get; }

        void Commit();
        void Rollback();
    }

    public interface IUnitOfWorkFactory
    {
        IUnitOfWork Create();
    }

    public interface IProductRepository
    {
        ProductRecord? GetByBarcode(string barcode);
        void DeductQuantity(string barcode, int qty);
        void AddQuantity(string barcode, int qty);
        void UpsertOnPurchase(string barcode, string productName, decimal unitCost, decimal salePrice, int qty, bool isSerialized);
        void InsertProductUnit(string barcode, string imei, int? purchaseId = null);
        void MarkImeiSold(string imei, int saleId);
        void MarkImeiInStock(string imei);
        bool ImeiExists(string imei);
        string GetImeiStatus(string imei);
        void DeleteProductUnitsForPurchase(int purchaseId);
        IReadOnlyList<ProductRecord> GetBelowThreshold(int threshold);
        void InsertAccessory(string barcode, string productName, decimal costPrice, decimal salePrice, int quantity);
        void UpdateAccessory(string barcode, string productName, decimal costPrice, decimal salePrice, int quantity);
        void DeleteByBarcode(string barcode);
        void UpdateModelPrice(string barcode, string productName, decimal costPrice, decimal salePrice);
        void SetQuantity(string barcode, int newQuantity);
    }

    // سجل تسويات الجرد - جدول InventoryAdjustments منفصل عن Products، بيوثّق كل فرق
    // بين الكمية بالنظام والكمية المعدودة فعليًا وقت عملية جرد
    public interface IInventoryAdjustmentRepository
    {
        void InsertAdjustmentLog(string barcode, string productName, int systemQtyBefore, int countedQty, int difference);
    }

    public interface IMaintenanceRepository
    {
        int Insert(string customerName, string? customerPhone, string deviceInfo, string? issueDescription, decimal estimatedCost);
        void UpdateStatus(int ticketId, string newStatus);
        string? GetCustomerName(int ticketId);
        void SetDelivered(int ticketId, decimal actualCost);
    }

    public interface IEmployeeRepository
    {
        void Add(string fullName, string? phone, decimal monthlySalary, decimal standardHoursPerDay, DateTime hireDate);
        void Update(int employeeId, string fullName, string? phone, decimal monthlySalary, decimal standardHoursPerDay, DateTime hireDate);
        void Delete(int employeeId);
        IReadOnlyList<EmployeeRecord> GetAll();
    }

    // حضور الموظفين وقفل/فتح كشف الرواتب الشهري - نفس معادلة الراتب الموجودة في
    // AttendancePageControl/AttendanceRepository القديمة: قيمة اليوم = المرتب ÷ عدد
    // أيام الشهر، والراتب الصافي = قيمة اليوم × (أيام الحضور + أيام الإجازة المدفوعة)
    // + قيمة الساعات الإضافية (قيمة الساعة × 1.5 لكل ساعة إضافية، قيمة الساعة = قيمة اليوم
    // ÷ ساعات العمل القياسية في اليوم بتاعة الموظف)
    public interface IAttendanceRepository
    {
        void UpsertStatus(int employeeId, DateTime date, string status, decimal overtimeHours);
        bool IsMonthClosed(int employeeId, int year, int month);
        (int Present, int Absent, int Leave, decimal OvertimeHours) GetAttendanceCounts(int employeeId, int year, int month);
        void InsertClosure(int employeeId, int year, int month, decimal monthlySalary, decimal dayValue, int presentDays, int absentDays, int leaveDays, decimal overtimeHours, decimal overtimeAmount, decimal netSalary);
        int DeleteClosuresForMonth(int year, int month);
        void DeleteAttendanceForEmployee(int employeeId);
        decimal GetEmployeeBalance(int employeeId);
    }

    public interface ISaleRepository
    {
        int Insert(SaleRecord sale);
        SaleRecord? GetById(int saleId);
        void UpdateQuantityAndTotal(int saleId, int qty, decimal total);
        void Delete(int saleId);
        int GetDailyInvoiceNumber(int invoiceId, string saleDate);
        void SetInvoiceId(int saleId, int invoiceId);
        void SetAmountPaid(int invoiceId, decimal amountPaid);
        IReadOnlyList<SaleRecord> GetByInvoiceId(int invoiceId);
    }

    public interface IPurchaseRepository
    {
        int InsertPurchase(int supplierId, decimal totalAmount);
        void UpdatePurchase(int purchaseId, int supplierId, decimal totalAmount);
        void InsertItem(int purchaseId, PurchaseLine line);
        void DeleteItems(int purchaseId);
        void DeletePurchase(int purchaseId);
        PurchaseRecord? GetById(int purchaseId);
    }

    public interface ICashDrawerRepository
    {
        decimal GetBalance(string method);
        IReadOnlyDictionary<string, decimal> GetAllBalances();
        void SetBalance(string method, decimal newBalance);
        int InsertMovement(CashMovementRecord movement);
        CashMovementRecord? GetMovementById(int id);
        CashMovementRecord? FindByPurchaseId(int purchaseId);
        void UpdateMovement(int id, string type, string method, decimal amount, string? reference, string? description, int? accountCode);
        void DeleteMovement(int id);
        void LinkMovements(int firstId, int secondId);
    }

    // بند مصروف عمومي - جدول Expenses منفصل عن CashMovements (اختيار تصميمي قديم
    // موجود من الأول، بيغذّي شاشة/تقارير المصروفات بالتحديد)
    public interface IExpenseRepository
    {
        int Insert(int accountCode, decimal amount, string paymentMethod);
        (int AccountCode, decimal Amount, string PaymentMethod)? GetById(int expenseId);
        void Update(int expenseId, int accountCode, decimal amount, string paymentMethod);
        void Delete(int expenseId);
    }

    public interface ICustomerRepository
    {
        decimal GetBalance(int customerId);
        bool Exists(int customerId);
        IReadOnlyList<CustomerStatementLine> GetStatement(int customerId);
    }

    public interface ISupplierRepository
    {
        decimal GetBalance(int supplierId);
        bool Exists(int supplierId);
        IReadOnlyList<SupplierStatementLine> GetStatement(int supplierId);
    }

    public interface IJournalRepository
    {
        int InsertEntry(string sourceType, int? sourceId, string? description, string createdBy);
        void InsertLine(int journalEntryId, int accountCode, decimal debit, decimal credit);

        // صافي رصيد حساب معين من الدفتر الحقيقي (SUM(Debit) - SUM(Credit)) - مستخدم
        // للمطابقة بين الدفتر وبين الكاش السريع (PaymentMethodBalances) في HealthEngine
        decimal GetAccountBalance(int accountCode);
    }

    // شجرة الحسابات - مستخدمة من AccountingEngine عشان يتأكد إن أي كود حساب بيتسجل
    // بيه قيد فعلًا موجود في الشجرة، قبل ما يتكتب في JournalLines
    public interface IAccountRepository
    {
        bool Exists(int accountCode);
    }

    public interface IAuditRepository
    {
        void Insert(AuditEntry entry);
    }

    // فحص مستقل (مش جزء من أي Transaction) لمعرفة هل يوم محاسبي معين تم إقفاله بالفعل -
    // نفس منطق UIHelpers.IsDateClosed القديم، هنا عشان ValidationEngine يقدر يستخدمه
    public interface IDateClosureRepository
    {
        bool IsClosed(DateTime date, string paymentMethod = "نقدي");
    }

    // إقفال اليوم الفعلي (DailyClosures/CashDenominations) - جزء من الـ UnitOfWork عشان
    // كل عمليات الإقفال/الفتح تتم كوحدة واحدة زي أي عملية تانية في النظام
    public interface IClosureRepository
    {
        decimal? GetLastActualClosingBalance(string paymentMethod);
        // CashMovements.MovementDate = date (مطابقة نصية تامة "yyyy-MM-dd")
        decimal GetTodayMovementsTotal(string paymentMethod, string movementType, DateTime date);
        // Expenses.ExpenseDate فيه وقت كمان، فلازم LIKE مش مطابقة تامة
        decimal GetTodayExpensesTotal(DateTime date);
        int InsertClosure(DateTime date, string paymentMethod, decimal opening, decimal totalIn, decimal totalOut, decimal actual, DateTime closedAt);
        void InsertDenominationLine(int closureId, decimal denominationValue, int count);
        IReadOnlyList<ClosureRow> GetClosuresForDate(DateTime date);
        void DeleteDenominationsForClosure(int closureId);
        void DeleteClosuresForDate(DateTime date);
    }

    // بينسخ قاعدة البيانات كاملة لمسار معين (VACUUM INTO) - مستخدم من BackupEngine
    public interface IBackupRepository
    {
        void VacuumInto(string destPath);
    }
}
