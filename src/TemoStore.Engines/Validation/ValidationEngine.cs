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

            if (command.Lines == null || command.Lines.Count == 0)
                errors.Add("لازم صنف واحد على الأقل في الفاتورة.");

            if (_dateClosure.IsClosed(DateTime.Now))
                errors.Add("تم إقفال اليوم بالفعل، لا يمكن تسجيل مبيعات جديدة.");

            if (command.Lines != null && command.Lines.Count > 0)
            {
                using (var uow = _uowFactory.Create())
                {
                    // بنجمع الكمية المطلوبة لكل باركود على حدة، عشان لو نفس الصنف اتكرر
                    // في أكتر من سطر في نفس الفاتورة، نتأكد إن الإجمالي مش أكبر من المتاح
                    var requestedQtyByBarcode = new Dictionary<string, int>();
                    var seenImeis = new HashSet<string>();
                    decimal amountDue = 0;

                    foreach (var line in command.Lines)
                    {
                        amountDue += line.Total - line.Discount + line.Tax;

                        if (string.IsNullOrWhiteSpace(line.Barcode))
                        {
                            errors.Add("لازم باركود لكل صنف في الفاتورة.");
                            continue;
                        }
                        if (line.Quantity <= 0)
                        {
                            errors.Add($"كمية الصنف \"{line.ProductName}\" لازم تكون أكبر من صفر.");
                            continue;
                        }

                        var product = uow.Products.GetByBarcode(line.Barcode);
                        if (product == null)
                        {
                            errors.Add($"المنتج \"{line.ProductName}\" غير موجود بالمخزون.");
                            continue;
                        }

                        if (product.IsSerialized && string.IsNullOrWhiteSpace(line.Imei))
                            errors.Add($"لازم تحدد رقم IMEI لمنتج \"{line.ProductName}\" (بسيريال).");

                        if (!string.IsNullOrWhiteSpace(line.Imei) && !seenImeis.Add(line.Imei))
                            errors.Add($"رقم IMEI \"{line.Imei}\" مكرر أكتر من مرة في نفس الفاتورة.");

                        requestedQtyByBarcode.TryGetValue(line.Barcode, out int soFar);
                        requestedQtyByBarcode[line.Barcode] = soFar + line.Quantity;
                        if (requestedQtyByBarcode[line.Barcode] > product.Quantity)
                            errors.Add($"الكمية المطلوبة من \"{line.ProductName}\" أكبر من المتاح! المتاح حاليًا هو: {product.Quantity}");
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

                    if (command.PaymentType == "Cash" && command.AmountPaid.HasValue && command.AmountPaid.Value < amountDue)
                        errors.Add($"المبلغ المدفوع ({command.AmountPaid.Value:N2}) أقل من إجمالي الفاتورة ({amountDue:N2}).");

                    uow.Rollback();
                }
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
