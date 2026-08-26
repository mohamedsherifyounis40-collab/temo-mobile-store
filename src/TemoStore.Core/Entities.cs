namespace TemoStore.Core.Entities
{
    // ==========================================================================
    // كيانات مشتركة بين المحركات وطبقة البيانات - كانت متكررة كنسخ منفصلة جوه كل
    // *Repository.cs قديم (SaleRecord في SalesRepository.cs، PurchaseCartItem في
    // SuppliersRepository.cs، CashMovementRecord في TreasuryRepository.cs...)، دلوقتي
    // مكانها الوحيد هنا عشان أي محرك أو Repository يستخدم نفس التعريف بالظبط.
    // ==========================================================================

    public class ProductRecord
    {
        public string Barcode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public decimal Price { get; set; }        // سعر الشراء/التكلفة
        public decimal SalePrice { get; set; }
        public int Quantity { get; set; }
        public bool IsSerialized { get; set; }
    }

    public class SaleRecord
    {
        public int SaleId { get; set; }
        public string Barcode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public decimal CostPrice { get; set; }
        public decimal Price { get; set; }
        public int QuantitySold { get; set; }
        public decimal Total { get; set; }                // إجمالي الصنف قبل الخصم (Gross) - يفضل زي ما هو دايمًا
        public decimal Discount { get; set; }              // مبلغ الخصم على الصنف ده (0 لو مفيش خصم) - الصافي = Total - Discount
        public decimal Tax { get; set; }                    // مبلغ الضريبة على الصنف ده (0 لو مفيش ضريبة)
        public decimal AmountPaid { get; set; }              // على مستوى الفاتورة كلها (مسجّل بس على أول صنف/السطر الأساسي) - المبلغ اللي فعليًا دفعه العميل (للبيع الكاش، لحساب الفكة)
        public int? CustomerId { get; set; }
        public string PaymentType { get; set; } = "";     // "Cash" | "Credit"
        public string? PaymentMethod { get; set; }        // نقدي/فوري/... - لو PaymentType == "Cash"
        public string? Imei { get; set; }
        public string SaleDate { get; set; } = "";
        public int SalesInvoiceId { get; set; }           // بيربط عدة أصناف تحت نفس رقم الفاتورة
    }

    // صنف واحد جوه فاتورة بيع ممكن تحتوي على أكتر من صنف - نفس مبدأ PurchaseLine بالظبط
    public class SaleLine
    {
        public required string Barcode { get; set; }
        public required string ProductName { get; set; }
        public required decimal UnitPrice { get; set; }
        public required int Quantity { get; set; }
        public required decimal Total { get; set; }        // إجمالي الصنف قبل الخصم (Gross)
        public decimal Discount { get; set; } = 0;          // مبلغ الخصم على الصنف ده (اختياري - افتراضي صفر)
        public decimal Tax { get; set; } = 0;                // مبلغ الضريبة على الصنف ده (اختياري - افتراضي صفر)
        public string? Imei { get; set; }
    }

    public class PurchaseLine
    {
        public string? Barcode { get; set; }
        public string ProductName { get; set; } = "";
        public int Qty { get; set; }
        public decimal UnitCost { get; set; }
        public decimal SalePrice { get; set; }
        public decimal LineTotal { get; set; }
        public bool IsSerialized { get; set; }
        public List<string>? Imeis { get; set; }

        // سطر "فاتورة مبلغ إجمالي": مبلغ مستحق للمورد من غير ما يتسجل صنف
        // بالمخزون - بيتسجل في تفاصيل الفاتورة وحساب المورد بس، وبيتجاهله كل
        // منطق المخزون (UpsertOnPurchase/InsertProductUnit/عكس المخزون عند الإلغاء).
        public bool SkipInventory { get; set; }
    }

    public class PurchaseRecord
    {
        public int PurchaseId { get; set; }
        public int SupplierId { get; set; }
        public decimal TotalAmount { get; set; }
        public string PurchaseDate { get; set; } = "";
        public List<PurchaseLine> Lines { get; set; } = new();
    }

    public class CashMovementRecord
    {
        public int Id { get; set; }
        public string MovementType { get; set; } = "";    // "قبض" | "صرف"
        public string PaymentMethod { get; set; } = "";
        public decimal Amount { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? Description { get; set; }
        public int? AccountCode { get; set; }
        public string MovementDate { get; set; } = "";
        public int? LinkedMovementId { get; set; }
        public int? PurchaseId { get; set; }
        public int? SaleId { get; set; }
        public int? CustomerId { get; set; }
        public int? SupplierId { get; set; }
        public int? EmployeeId { get; set; }
        public bool IsAdvance { get; set; }
    }

    public class EmployeeRecord
    {
        public int EmployeeId { get; set; }
        public string FullName { get; set; } = "";
        public decimal MonthlySalary { get; set; }
        public decimal StandardHoursPerDay { get; set; }
    }

    public class ClosureRow
    {
        public int Id { get; set; }
        public string PaymentMethod { get; set; } = "";
        public decimal ExpectedClosingBalance { get; set; }
        public int? AdjustmentMovementId { get; set; }
    }

    public class JournalLineRequest
    {
        public int AccountCode { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    public class JournalEntryRequest
    {
        public string SourceType { get; set; } = "";   // 'Sale' | 'Purchase' | 'Expense' | 'Transfer' | 'SupplierPayment' | 'CustomerPayment'
        public int? SourceId { get; set; }
        public string? Description { get; set; }
        public List<JournalLineRequest> Lines { get; set; } = new();
    }

    public class AuditEntry
    {
        public string Username { get; set; } = "";
        public string Screen { get; set; } = "";
        public string Operation { get; set; } = "";
        public string? OldData { get; set; }
        public string? NewData { get; set; }
        public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class CustomerStatementLine
    {
        public string Date { get; set; } = "";
        public string Type { get; set; } = "";
        public string Details { get; set; } = "";
        public decimal Amount { get; set; }
    }

    public class SupplierStatementLine
    {
        public string Date { get; set; } = "";
        public string Type { get; set; } = "";
        public string Details { get; set; } = "";
        public decimal Amount { get; set; }
    }

    public class InventoryAdjustmentLine
    {
        public string Barcode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public int SystemQty { get; set; }
        public int CountedQty { get; set; }
        public int Difference { get; set; }
    }

    public class LowStockAlert
    {
        public string Barcode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public int CurrentQuantity { get; set; }
        public int Threshold { get; set; }
    }

    public class PriceQuote
    {
        public decimal UnitPrice { get; set; }
        public decimal DiscountApplied { get; set; }
        public string? PromotionName { get; set; }
    }

    public enum CustomerTier { Regular, Wholesale, Vip }

    public class PromotionContext
    {
        public string? PromoCode { get; set; }
    }

    public class HealthCheckItem
    {
        public string Area { get; set; } = "";     // "المخزون", "الدرج", "العملاء", "الموردين", "القيود", "النسخ الاحتياطي"
        public bool Healthy { get; set; }
        public string Message { get; set; } = "";
    }

    public class HealthReport
    {
        public List<HealthCheckItem> Items { get; set; } = new();
        public bool AllHealthy => Items.TrueForAll(i => i.Healthy);
    }

    public enum NotificationType { LowStock, WarrantyExpiring, InstallmentOverdue, NegativeBalance, BackupFailed }
}
