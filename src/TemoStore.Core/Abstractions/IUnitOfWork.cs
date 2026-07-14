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
        void InsertProductUnit(string barcode, string imei, int purchaseId);
        void MarkImeiSold(string imei, int saleId);
        void MarkImeiInStock(string imei);
        bool ImeiExists(string imei);
        string GetImeiStatus(string imei);
        void DeleteProductUnitsForPurchase(int purchaseId);
        IReadOnlyList<ProductRecord> GetBelowThreshold(int threshold);
    }

    public interface ISaleRepository
    {
        int Insert(SaleRecord sale);
        SaleRecord? GetById(int saleId);
        void UpdateQuantityAndTotal(int saleId, int qty, decimal total);
        void Delete(int saleId);
        int GetDailyInvoiceNumber(int saleId, string saleDate);
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

    // بينسخ قاعدة البيانات كاملة لمسار معين (VACUUM INTO) - مستخدم من BackupEngine
    public interface IBackupRepository
    {
        void VacuumInto(string destPath);
    }
}
