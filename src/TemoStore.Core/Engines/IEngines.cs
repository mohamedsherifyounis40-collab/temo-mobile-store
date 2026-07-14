using TemoStore.Core.Abstractions;
using TemoStore.Core.Commands;
using TemoStore.Core.Entities;

namespace TemoStore.Core.Engines
{
    public class ValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new();

        public static ValidationResult Ok() => new();
        public static ValidationResult Fail(string error) => new() { IsValid = false, Errors = { error } };
    }

    // مسؤول عن التحقق قبل تنفيذ أي عملية - أول محطة داخل CoreEngine، قبل ما أي
    // Transaction تتفتح. لو رجّع IsValid = false، العملية بتتوقف بالكامل من غير
    // ما تلمس قاعدة البيانات خالص.
    public interface IValidationEngine
    {
        ValidationResult ValidateSale(CreateSaleCommand command);
        ValidationResult ValidatePurchase(CreatePurchaseCommand command);
        ValidationResult ValidateExpense(RecordExpenseCommand command);
        ValidationResult ValidateTransfer(TransferFundsCommand command);
    }

    // مسؤول عن المخزون بس - خصم/إضافة كميات، وحدات IMEI، حركة مخزن، مراجعة الحد
    // الأدنى. ممنوع أي شاشة تعدّل المخزون من غير المحرك ده.
    public interface IInventoryEngine
    {
        void DeductStock(string barcode, int quantity, IUnitOfWork uow);
        void AddStock(string barcode, string productName, decimal unitCost, decimal salePrice, int quantity, bool isSerialized, IUnitOfWork uow);
        void MarkImeiSold(string imei, int saleId, IUnitOfWork uow);
        void MarkImeiInStock(string imei, IUnitOfWork uow);
        void ReverseStockForCancelledPurchase(int purchaseId, IUnitOfWork uow);
        IReadOnlyList<LowStockAlert> CheckMinimumThresholds(int threshold, IUnitOfWork uow);
        void AddAccessory(string barcode, string productName, decimal costPrice, decimal salePrice, int quantity, IUnitOfWork uow);
        void UpdateAccessory(string barcode, string productName, decimal costPrice, decimal salePrice, int quantity, IUnitOfWork uow);
        void DeleteProduct(string barcode, IUnitOfWork uow);
        void UpdateModelPrice(string barcode, string productName, decimal costPrice, decimal salePrice, IUnitOfWork uow);
        void AddDeviceManually(string barcode, string productName, decimal costPrice, decimal salePrice, string imei, IUnitOfWork uow);
        int ApplyInventoryCount(IReadOnlyList<InventoryAdjustmentLine> rows, IUnitOfWork uow);
    }

    // مسؤول عن الخزنة - إضافة/خصم نقدية، مراجعة الرصيد، منع الرصيد السالب، تسجيل حركة الدرج
    public interface ICashDrawerEngine
    {
        decimal GetBalance(string paymentMethod, IUnitOfWork uow);
        int Credit(string paymentMethod, decimal amount, string reason, IUnitOfWork uow, int? saleId = null, int? purchaseId = null, int? customerId = null, int? supplierId = null, int? accountCode = null);
        int Debit(string paymentMethod, decimal amount, string reason, IUnitOfWork uow, int? saleId = null, int? purchaseId = null, int? customerId = null, int? supplierId = null, int? accountCode = null);
        (int fromMovementId, int toMovementId) Transfer(string fromMethod, string toMethod, decimal amount, string? description, IUnitOfWork uow);
    }

    // مسؤول عن العملاء - كشف الحساب وتحصيل الدين. رصيد العميل نفسه محسوب (Derived)
    // من مجموع مبيعاته الآجلة ناقص مجموع تحصيلاته - مفيش عمود "رصيد" بيتحدّث مباشرة،
    // فالبيع الآجل نفسه (تسجيله في IInventoryEngine/ISaleRepository) هو اللي بيزوّد
    // "دين" العميل تلقائيًا، من غير حاجة لخطوة منفصلة هنا.
    public interface ICustomerEngine
    {
        void RecordCollection(int customerId, decimal amount, string method, IUnitOfWork uow);
        IReadOnlyList<CustomerStatementLine> GetStatement(int customerId, IUnitOfWork uow);
    }

    // مسؤول عن الموردين - تحديث الرصيد، كشف الحساب، المدفوع والمتبقي
    public interface ISupplierEngine
    {
        void RecordPayment(int supplierId, decimal amount, string method, IUnitOfWork uow);
        IReadOnlyList<SupplierStatementLine> GetStatement(int supplierId, IUnitOfWork uow);
    }

    // مسؤول عن حساب السعر النهائي (خصومات/عروض/جملة/قطاعي/VIP) - مفيش شاشة
    // تحسب سعر بنفسها، كل حاجة بتعدي من هنا
    public interface IPricingEngine
    {
        PriceQuote CalculatePrice(ProductRecord product, int quantity, CustomerTier tier, PromotionContext? promotion);
    }

    // بيحفظ تكلفة البيع والربح مباشرة بعد تنفيذ العملية
    public interface IProfitEngine
    {
        decimal CalculateProfit(decimal total, decimal cost);
    }

    // كل عملية لازم تنشئ قيد محاسبي تلقائيًا - القيد لازم يكون متزن (مدين = دائن)
    // وإلا بيترمي AccountingImbalanceException ومفيش حاجة بتتحفظ
    public interface IAccountingEngine
    {
        int Post(JournalEntryRequest request, string performedBy, IUnitOfWork uow);
    }

    // بيسجل كل العمليات (إضافة/تعديل/حذف/تسجيل دخول/طباعة/Backup) - بعد الـ Commit بس،
    // مش جزء من الـ Transaction، عشان فشل تسجيل الـ Audit نفسه ميرجعش عملية مالية ناجحة
    public interface IAuditEngine
    {
        void Log(AuditEntry entry);
    }

    public class IntegrityCheckResult
    {
        public bool Passed { get; set; } = true;
        public List<string> Violations { get; set; } = new();
    }

    // آخر بوابة قبل الـ Commit - بيتأكد إن الأصناف/وسائل الدفع اللي اتلمست في العملية
    // دي تحديدًا فضلت في حالة سليمة (كمية مش سالبة، رصيد مش سالب) حتى لو كل محرك على
    // حدة اشتغل صح. بيستخدم نفس واجهات IUnitOfWork اللي باقي المحركات بتستخدمها -
    // مفيش وصول مباشر لقاعدة البيانات هنا خالص، عشان الطبقة دي تفضل مستقلة عن أي
    // تفاصيل تنفيذ (SQLite أو غيره). لو فشل، Rollback لكل حاجة، مفيش عملية نصف منفذة.
    public interface IIntegrityEngine
    {
        IntegrityCheckResult CheckBeforeCommit(IUnitOfWork uow, IReadOnlyList<string>? affectedBarcodes = null, IReadOnlyList<string>? affectedPaymentMethods = null);
    }

    // فحص شامل للنظام (مخزون/درج/عملاء/موردين/قيود/حركات يومية/نسخ احتياطي) - عند
    // الطلب أو مجدول، مش جزء من مسار أي عملية بيع/شراء
    public interface IHealthEngine
    {
        HealthReport RunFullCheck();
    }

    // مسؤول عن التنبيهات (نقص مخزون/ضمان قرب الانتهاء/قسط متأخر/رصيد سالب/فشل Backup)
    public interface INotificationEngine
    {
        void Notify(NotificationType type, string message, object? context = null);
    }

    public class BackupResult
    {
        public bool Success { get; set; }
        public string? FilePath { get; set; }
        public string? Error { get; set; }
    }

    // نسخ احتياطي تلقائي + التحقق من نجاح النسخة
    public interface IBackupEngine
    {
        BackupResult CreateBackup();
        bool VerifyLastBackup();
    }

    // بيشتغل مرة عند فتح البرنامج - بيكمل أو يلغي أي عملية Post-Commit معلقة
    // (زي مزامنة سحابية لسه ما اتبعتتش) عن طريق Outbox. مش مسؤول عن استرجاع
    // الـ DB نفسها لأن SQLite Transactions أصلًا بتضمن كده تلقائيًا (راجع قسم 7 بالمستند)
    public interface IRecoveryEngine
    {
        void ResumeOrRollbackPendingOperations();
    }
}
