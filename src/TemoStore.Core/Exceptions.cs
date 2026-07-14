namespace TemoStore.Core.Exceptions
{
    // بترمي لما حركة "صرف"/سحب تتعدى الرصيد المتاح في وسيلة دفع معينة
    public class InsufficientBalanceException : Exception
    {
        public string PaymentMethod { get; }
        public decimal AvailableBalance { get; }

        public InsufficientBalanceException(string paymentMethod, decimal availableBalance)
            : base($"الرصيد المتاح في \"{paymentMethod}\" هو {availableBalance} فقط، لا يمكن صرف مبلغ أكبر منه.")
        {
            PaymentMethod = paymentMethod;
            AvailableBalance = availableBalance;
        }
    }

    // بترمي لما الكمية المطلوبة (بيع/تعديل) تتعدى المتاح فعليًا بالمخزون
    public class InsufficientStockException : Exception
    {
        public int AvailableQuantity { get; }

        public InsufficientStockException(int availableQuantity)
            : base($"الكمية المطلوبة أكبر من المتاح! المتاح حالياً هو: {availableQuantity}")
        {
            AvailableQuantity = availableQuantity;
        }
    }

    // بترمي لما رقم IMEI يكون مسجّل بالفعل في النظام من قبل
    public class DuplicateImeiException : Exception
    {
        public string Imei { get; }
        public DuplicateImeiException(string imei) : base($"رقم الـIMEI \"{imei}\" مسجل بالفعل في النظام من قبل. راجع القايمة وشيله أو صحّحه.")
        {
            Imei = imei;
        }
    }

    // بترمي لما محاولة تعديل/إلغاء عملية تابعة ليوم محاسبي تم إقفاله بالفعل
    public class DateClosedException : Exception
    {
        public DateClosedException(string message) : base(message) { }
    }

    // بترمي لو قيد محاسبي اتبنى وإجمالي المدين ≠ إجمالي الدائن - القيد ده مينفعش يتحفظ خالص
    public class AccountingImbalanceException : Exception
    {
        public decimal TotalDebit { get; }
        public decimal TotalCredit { get; }

        public AccountingImbalanceException(decimal totalDebit, decimal totalCredit)
            : base($"القيد المحاسبي غير متزن: إجمالي المدين {totalDebit:N2} لا يساوي إجمالي الدائن {totalCredit:N2}.")
        {
            TotalDebit = totalDebit;
            TotalCredit = totalCredit;
        }
    }

    // بترمي لما IValidationEngine يرفض عملية - بيحمل كل أسباب الرفض عشان تتعرض للمستخدم كاملة
    public class ValidationException : Exception
    {
        public IReadOnlyList<string> Errors { get; }

        public ValidationException(IReadOnlyList<string> errors)
            : base(errors.Count == 1 ? errors[0] : string.Join(Environment.NewLine, errors))
        {
            Errors = errors;
        }
    }
}
