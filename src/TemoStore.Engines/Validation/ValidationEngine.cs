using TemoStore.Core.Abstractions;
using TemoStore.Core.Commands;
using TemoStore.Core.Engines;

namespace TemoStore.Engines.Validation
{
    // أول محطة لأي Command جوه CoreEngine - بيفتح UnitOfWork للقراءة بس (بيتعمله
    // Rollback بعد ما يخلص، مفيش أي كتابة هنا خالص) عشان يتحقق من البيانات قبل ما
    // أي Transaction حقيقية تتفتح. لو رجّع IsValid = false، العملية بتتوقف بالكامل.
    public class ValidationEngine : IValidationEngine
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IDateClosureRepository _dateClosure;

        public ValidationEngine(IUnitOfWorkFactory uowFactory, IDateClosureRepository dateClosure)
        {
            _uowFactory = uowFactory;
            _dateClosure = dateClosure;
        }

        public ValidationResult ValidateSale(CreateSaleCommand command)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(command.Barcode)) errors.Add("لازم باركود للمنتج.");
            if (command.Quantity <= 0) errors.Add("الكمية لازم تكون أكبر من صفر.");

            if (_dateClosure.IsClosed(DateTime.Now))
                errors.Add("تم إقفال اليوم بالفعل، لا يمكن تسجيل مبيعات جديدة.");

            using (var uow = _uowFactory.Create())
            {
                var product = uow.Products.GetByBarcode(command.Barcode);
                if (product == null)
                    errors.Add("المنتج غير موجود بالمخزون.");
                else
                {
                    if (product.Quantity < command.Quantity)
                        errors.Add($"الكمية المطلوبة أكبر من المتاح! المتاح حاليًا هو: {product.Quantity}");
                    if (product.IsSerialized && string.IsNullOrWhiteSpace(command.Imei))
                        errors.Add("لازم تحدد رقم IMEI لمنتج بسيريال.");
                }

                if (command.PaymentType == "Credit")
                {
                    if (command.CustomerId == null)
                        errors.Add("البيع بالآجل لازم يكون له عميل محدد.");
                    else if (!uow.Customers.Exists(command.CustomerId.Value))
                        errors.Add("العميل المحدد غير موجود.");
                }
                else if (command.PaymentType == "Cash" && string.IsNullOrWhiteSpace(command.PaymentMethod))
                {
                    errors.Add("لازم تحدد وسيلة الدفع للبيع الكاش.");
                }

                uow.Rollback();
            }

            return errors.Count == 0 ? ValidationResult.Ok() : new ValidationResult { IsValid = false, Errors = errors };
        }

        public ValidationResult ValidatePurchase(CreatePurchaseCommand command)
        {
            var errors = new List<string>();

            if (command.Lines == null || command.Lines.Count == 0)
                errors.Add("لازم صنف واحد على الأقل في فاتورة الشراء.");

            if (_dateClosure.IsClosed(DateTime.Now))
                errors.Add("تم إقفال اليوم بالفعل، لا يمكن تسجيل فواتير شراء جديدة.");

            using (var uow = _uowFactory.Create())
            {
                if (!uow.Suppliers.Exists(command.SupplierId))
                    errors.Add("المورد المحدد غير موجود.");

                if (command.Lines != null)
                {
                    foreach (var line in command.Lines)
                    {
                        if (line.Qty <= 0) errors.Add($"كمية غير صحيحة للصنف \"{line.ProductName}\".");
                        if (line.IsSerialized && line.Imeis != null)
                        {
                            foreach (var imei in line.Imeis)
                            {
                                if (uow.Products.ImeiExists(imei))
                                    errors.Add($"رقم الـIMEI \"{imei}\" مسجل بالفعل في النظام من قبل.");
                            }
                        }
                    }
                }

                if (command.PayCashNow && string.IsNullOrWhiteSpace(command.CashMethod))
                    errors.Add("لازم تحدد وسيلة الدفع للشراء الكاش الفوري.");

                uow.Rollback();
            }

            return errors.Count == 0 ? ValidationResult.Ok() : new ValidationResult { IsValid = false, Errors = errors };
        }

        public ValidationResult ValidateExpense(RecordExpenseCommand command)
        {
            var errors = new List<string>();
            if (command.Amount <= 0) errors.Add("المبلغ لازم يكون أكبر من صفر.");
            if (string.IsNullOrWhiteSpace(command.PaymentMethod)) errors.Add("لازم تحدد وسيلة الدفع.");
            if (_dateClosure.IsClosed(DateTime.Now)) errors.Add("تم إقفال اليوم بالفعل، لا يمكن تسجيل مصروفات جديدة.");
            return errors.Count == 0 ? ValidationResult.Ok() : new ValidationResult { IsValid = false, Errors = errors };
        }

        public ValidationResult ValidateTransfer(TransferFundsCommand command)
        {
            var errors = new List<string>();
            if (command.Amount <= 0) errors.Add("المبلغ لازم يكون أكبر من صفر.");
            if (command.FromMethod == command.ToMethod) errors.Add("لازم تختار وسيلتين مختلفتين للتحويل.");
            if (_dateClosure.IsClosed(DateTime.Now)) errors.Add("تم إقفال اليوم بالفعل، لا يمكن تسجيل تحويلات جديدة.");
            return errors.Count == 0 ? ValidationResult.Ok() : new ValidationResult { IsValid = false, Errors = errors };
        }
    }
}
