using TemoStore.Core.Abstractions;
using TemoStore.Core.Entities;

namespace TemoStore.Core.Commands
{
    // ==========================================================================
    // كل عملية في النظام (بيع/شراء/مرتجع/دفع مورد/قبض عميل/مصروف/تحويل) بتتحول
    // لـ Command بيتبعت لـ ICoreEngine.Execute - الشاشة تبني الـ Command بس، وترسله.
    // ==========================================================================

    public class CreateSaleCommand : ICommand<SaleResult>
    {
        public required string Barcode { get; set; }
        public required string ProductName { get; set; }
        public required decimal UnitPrice { get; set; }
        public required int Quantity { get; set; }
        public required decimal Total { get; set; }
        public int? CustomerId { get; set; }
        public required string PaymentType { get; set; }     // "Cash" | "Credit"
        public string? PaymentMethod { get; set; }
        public string? Imei { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class SaleResult
    {
        public int SaleId { get; set; }
        public int DailyInvoiceNumber { get; set; }
    }

    public class UpdateSaleQuantityCommand : ICommand<SaleResult>
    {
        public required int SaleId { get; set; }
        public required int NewQuantity { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class CancelSaleCommand : ICommand<bool>
    {
        public required int SaleId { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class CreatePurchaseCommand : ICommand<PurchaseResult>
    {
        public required int SupplierId { get; set; }
        public required List<PurchaseLine> Lines { get; set; }
        public required bool PayCashNow { get; set; }
        public string? CashMethod { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class PurchaseResult
    {
        public int PurchaseId { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class EditPurchaseCommand : ICommand<PurchaseResult>
    {
        public required int PurchaseId { get; set; }
        public required int SupplierId { get; set; }
        public required List<PurchaseLine> Lines { get; set; }
        public required bool PayCashNow { get; set; }
        public string? CashMethod { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class CancelPurchaseCommand : ICommand<bool>
    {
        public required int PurchaseId { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class PaySupplierCommand : ICommand<PaymentResult>
    {
        public required int SupplierId { get; set; }
        public required string SupplierName { get; set; }
        public required string Method { get; set; }
        public required decimal Amount { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class CollectFromCustomerCommand : ICommand<PaymentResult>
    {
        public required int CustomerId { get; set; }
        public required string CustomerName { get; set; }
        public required string Method { get; set; }
        public required decimal Amount { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class PaymentResult
    {
        public int MovementId { get; set; }
    }

    public class RecordExpenseCommand : ICommand<ExpenseResult>
    {
        public required int AccountCode { get; set; }
        public required decimal Amount { get; set; }
        public required string PaymentMethod { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class ExpenseResult
    {
        public int MovementId { get; set; }
    }

    public class TransferFundsCommand : ICommand<TransferResult>
    {
        public required string FromMethod { get; set; }
        public required string ToMethod { get; set; }
        public required decimal Amount { get; set; }
        public string? Description { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class TransferResult
    {
        public int FromMovementId { get; set; }
        public int ToMovementId { get; set; }
    }
}
