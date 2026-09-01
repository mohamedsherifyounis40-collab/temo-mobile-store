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
        public required List<SaleLine> Lines { get; set; }
        public int? CustomerId { get; set; }
        public required string PaymentType { get; set; }     // "Cash" | "Credit"
        public string? PaymentMethod { get; set; }
        public required string PerformedBy { get; set; }
        public decimal? AmountPaid { get; set; }              // اختياري - للبيع الكاش بس (حاسبة فكة). لو مش مُرسلة = المبلغ المطلوب بالظبط
    }

    public class SaleResult
    {
        public int SaleId { get; set; }
        public int SalesInvoiceId { get; set; }
        public List<int> SaleIds { get; set; } = new();
        public int DailyInvoiceNumber { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal ChangeDue { get; set; }
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

    public class UpdateExpenseCommand : ICommand<bool>
    {
        public required int ExpenseId { get; set; }
        public required int AccountCode { get; set; }
        public required decimal Amount { get; set; }
        public required string PaymentMethod { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class DeleteExpenseCommand : ICommand<bool>
    {
        public required int ExpenseId { get; set; }
        public required string PerformedBy { get; set; }
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

    public class CancelTransferCommand : ICommand<bool>
    {
        public required int MovementId { get; set; }
        public required string PerformedBy { get; set; }
    }

    // حركة قبض/صرف عامة (تختلف عن RecordExpenseCommand - دي بتسجل جوه CashMovements
    // مباشرة بأي نوع/حساب اختياري، مش مربوطة ببند مصروف محدد)
    public class AddMovementCommand : ICommand<PaymentResult>
    {
        public required string Type { get; set; }   // "قبض" | "صرف"
        public required string Method { get; set; }
        public required decimal Amount { get; set; }
        public string? Reference { get; set; }
        public string? Description { get; set; }
        public int? AccountCode { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class UpdateMovementCommand : ICommand<bool>
    {
        public required int MovementId { get; set; }
        public required string NewType { get; set; }
        public required string NewMethod { get; set; }
        public required decimal NewAmount { get; set; }
        public string? Reference { get; set; }
        public string? Description { get; set; }
        public int? AccountCode { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class CancelMovementCommand : ICommand<bool>
    {
        public required int MovementId { get; set; }
        public required string PerformedBy { get; set; }
    }

    // ==========================================================================
    // شاشة المخزون (إكسسوارات + أجهزة/IMEI + جرد المخزن)
    // ==========================================================================

    public class AddAccessoryProductCommand : ICommand<bool>
    {
        public required string Barcode { get; set; }
        public required string ProductName { get; set; }
        public required decimal CostPrice { get; set; }
        public required decimal SalePrice { get; set; }
        public required int Quantity { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class UpdateAccessoryProductCommand : ICommand<bool>
    {
        public required string Barcode { get; set; }
        public required string ProductName { get; set; }
        public required decimal CostPrice { get; set; }
        public required decimal SalePrice { get; set; }
        public required int Quantity { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class DeleteProductCommand : ICommand<bool>
    {
        public required string Barcode { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class UpdateModelPriceCommand : ICommand<bool>
    {
        public required string Barcode { get; set; }
        public required string ProductName { get; set; }
        public required decimal CostPrice { get; set; }
        public required decimal SalePrice { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class AddDeviceCommand : ICommand<bool>
    {
        public required string Barcode { get; set; }
        public required string ProductName { get; set; }
        public required decimal CostPrice { get; set; }
        public required decimal SalePrice { get; set; }
        public required string Imei { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class DeleteDeviceCommand : ICommand<bool>
    {
        public required string Imei { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class SaveInventoryAdjustmentsCommand : ICommand<int>
    {
        public required List<InventoryAdjustmentLine> Rows { get; set; }
        public required string PerformedBy { get; set; }
    }

    // ==========================================================================
    // شاشة الصيانة
    // ==========================================================================

    public class ReceiveDeviceCommand : ICommand<bool>
    {
        public required string CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public required string DeviceInfo { get; set; }
        public string? IssueDescription { get; set; }
        public required decimal EstimatedCost { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class UpdateMaintenanceStatusCommand : ICommand<bool>
    {
        public required int TicketId { get; set; }
        public required string NewStatus { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class DeliverMaintenanceDeviceCommand : ICommand<bool>
    {
        public required int TicketId { get; set; }
        public required decimal ActualCost { get; set; }
        public required string Method { get; set; }
        public required string PerformedBy { get; set; }
    }

    // ==========================================================================
    // شاشة الحضور والمرتبات
    // ==========================================================================

    public class SetAttendanceStatusCommand : ICommand<bool>
    {
        public required int EmployeeId { get; set; }
        public required DateTime Date { get; set; }
        public required string Status { get; set; }
        public decimal OvertimeHours { get; set; } = 0m;
        public required string PerformedBy { get; set; }
    }

    public class ClosePayrollMonthCommand : ICommand<int>
    {
        public required int Year { get; set; }
        public required int Month { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class ReopenPayrollMonthCommand : ICommand<int>
    {
        public required int Year { get; set; }
        public required int Month { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class PayEmployeeCommand : ICommand<bool>
    {
        public required int EmployeeId { get; set; }
        public required string EmployeeName { get; set; }
        public required string Method { get; set; }
        public required decimal Amount { get; set; }
        public string? Description { get; set; }
        public required bool IsAdvance { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class AddEmployeeCommand : ICommand<bool>
    {
        public required string FullName { get; set; }
        public string? Phone { get; set; }
        public required decimal MonthlySalary { get; set; }
        public decimal StandardHoursPerDay { get; set; } = 8m;
        public required DateTime HireDate { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class UpdateEmployeeCommand : ICommand<bool>
    {
        public required int EmployeeId { get; set; }
        public required string FullName { get; set; }
        public string? Phone { get; set; }
        public required decimal MonthlySalary { get; set; }
        public decimal StandardHoursPerDay { get; set; } = 8m;
        public required DateTime HireDate { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class DeleteEmployeeCommand : ICommand<bool>
    {
        public required int EmployeeId { get; set; }
        public required string PerformedBy { get; set; }
    }

    // ==========================================================================
    // شاشة التقارير - إقفال اليوم
    // ==========================================================================

    public class CloseDayCommand : ICommand<int>
    {
        // لازم يشمل كل وسائل الدفع، بما فيها "نقدي" - الـ Handler بيرفض لو "نقدي" ناقصة
        public required Dictionary<string, decimal> MethodActualBalances { get; set; }
        public Dictionary<decimal, int>? CashDenominationCounts { get; set; }
        public required string PerformedBy { get; set; }
    }

    public class ReopenDayCommand : ICommand<int>
    {
        public required string PerformedBy { get; set; }
    }
}
