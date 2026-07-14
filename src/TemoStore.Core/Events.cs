namespace TemoStore.Core.Events
{
    // كل حدث بيتنشر على الـ Event Bus لازم يطبّق الواجهة دي - بيحصل بعد نجاح الـ Commit بس،
    // أبدًا مش جزء من الـ Transaction نفسه (راجع قسم 4 في مستند العمارة)
    public interface IDomainEvent
    {
        Guid EventId { get; }
        DateTime OccurredAtUtc { get; }
    }

    public abstract class DomainEventBase : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
        public string PerformedBy { get; init; } = "";
    }

    public sealed class SaleCompleted : DomainEventBase
    {
        public int SaleId { get; init; }
        public string Barcode { get; init; } = "";
        public string ProductName { get; init; } = "";
        public int QuantitySold { get; init; }
        public decimal Total { get; init; }
        public decimal Cost { get; init; }
        public string PaymentType { get; init; } = "";
        public string? PaymentMethod { get; init; }
        public int? CustomerId { get; init; }
    }

    public sealed class PurchaseCompleted : DomainEventBase
    {
        public int PurchaseId { get; init; }
        public int SupplierId { get; init; }
        public decimal Total { get; init; }
        public bool PaidCashNow { get; init; }
        public string? PaymentMethod { get; init; }
    }

    public sealed class ReturnCompleted : DomainEventBase
    {
        public int OriginalSaleId { get; init; }
        public int Quantity { get; init; }
        public decimal RefundAmount { get; init; }
        public string Reason { get; init; } = "";
    }

    public sealed class SupplierPaymentRecorded : DomainEventBase
    {
        public int SupplierId { get; init; }
        public decimal Amount { get; init; }
        public string PaymentMethod { get; init; } = "";
    }

    public sealed class CustomerPaymentRecorded : DomainEventBase
    {
        public int CustomerId { get; init; }
        public decimal Amount { get; init; }
        public string PaymentMethod { get; init; } = "";
    }

    public sealed class ExpenseRecorded : DomainEventBase
    {
        public int AccountCode { get; init; }
        public decimal Amount { get; init; }
        public string PaymentMethod { get; init; } = "";
    }

    public sealed class FundsTransferred : DomainEventBase
    {
        public string FromMethod { get; init; } = "";
        public string ToMethod { get; init; } = "";
        public decimal Amount { get; init; }
    }

    public sealed class StockBelowThreshold : DomainEventBase
    {
        public string Barcode { get; init; } = "";
        public string ProductName { get; init; } = "";
        public int CurrentQuantity { get; init; }
        public int Threshold { get; init; }
    }

    public sealed class BackupCompleted : DomainEventBase
    {
        public string FilePath { get; init; } = "";
    }

    public sealed class BackupFailed : DomainEventBase
    {
        public string Reason { get; init; } = "";
    }
}
