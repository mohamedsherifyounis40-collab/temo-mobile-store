using TemoStore.Core.Abstractions;
using TemoStore.Core.Commands;
using TemoStore.Core.Engines;
using TemoStore.Core.Entities;
using TemoStore.Core.Events;

namespace TemoStore.Engines.Handlers
{
    public class PaySupplierCommandHandler : ICommandHandler<PaySupplierCommand, PaymentResult>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly ISupplierEngine _supplier;
        private readonly IAccountingEngine _accounting;
        private readonly IEventBus _eventBus;

        public PaySupplierCommandHandler(IUnitOfWorkFactory uowFactory, ISupplierEngine supplier, IAccountingEngine accounting, IEventBus eventBus)
        {
            _uowFactory = uowFactory;
            _supplier = supplier;
            _accounting = accounting;
            _eventBus = eventBus;
        }

        public PaymentResult Handle(PaySupplierCommand command)
        {
            using var uow = _uowFactory.Create();

            _supplier.RecordPayment(command.SupplierId, command.Amount, command.Method, uow);

            var lines = new List<JournalLineRequest>
            {
                new() { AccountCode = AccountCodes.Suppliers, Debit = command.Amount, Credit = 0 },
                new() { AccountCode = AccountCodes.ForPaymentMethod(command.Method), Debit = 0, Credit = command.Amount }
            };
            _accounting.Post(new JournalEntryRequest { SourceType = "SupplierPayment", SourceId = command.SupplierId, Description = $"سداد لمورد: {command.SupplierName}", Lines = lines }, command.PerformedBy, uow);

            uow.Commit();
            _eventBus.Publish(new SupplierPaymentRecorded { SupplierId = command.SupplierId, Amount = command.Amount, PaymentMethod = command.Method, PerformedBy = command.PerformedBy });

            return new PaymentResult();
        }
    }

    public class CollectFromCustomerCommandHandler : ICommandHandler<CollectFromCustomerCommand, PaymentResult>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly ICustomerEngine _customer;
        private readonly IAccountingEngine _accounting;
        private readonly IEventBus _eventBus;

        public CollectFromCustomerCommandHandler(IUnitOfWorkFactory uowFactory, ICustomerEngine customer, IAccountingEngine accounting, IEventBus eventBus)
        {
            _uowFactory = uowFactory;
            _customer = customer;
            _accounting = accounting;
            _eventBus = eventBus;
        }

        public PaymentResult Handle(CollectFromCustomerCommand command)
        {
            using var uow = _uowFactory.Create();

            _customer.RecordCollection(command.CustomerId, command.Amount, command.Method, uow);

            var lines = new List<JournalLineRequest>
            {
                new() { AccountCode = AccountCodes.ForPaymentMethod(command.Method), Debit = command.Amount, Credit = 0 },
                new() { AccountCode = AccountCodes.Customers, Debit = 0, Credit = command.Amount }
            };
            _accounting.Post(new JournalEntryRequest { SourceType = "CustomerPayment", SourceId = command.CustomerId, Description = $"تحصيل من عميل: {command.CustomerName}", Lines = lines }, command.PerformedBy, uow);

            uow.Commit();
            _eventBus.Publish(new CustomerPaymentRecorded { CustomerId = command.CustomerId, Amount = command.Amount, PaymentMethod = command.Method, PerformedBy = command.PerformedBy });

            return new PaymentResult();
        }
    }

    public class RecordExpenseCommandHandler : ICommandHandler<RecordExpenseCommand, ExpenseResult>
    {
        private readonly IValidationEngine _validation;
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly ICashDrawerEngine _cashDrawer;
        private readonly IAccountingEngine _accounting;
        private readonly IEventBus _eventBus;

        public RecordExpenseCommandHandler(IValidationEngine validation, IUnitOfWorkFactory uowFactory, ICashDrawerEngine cashDrawer, IAccountingEngine accounting, IEventBus eventBus)
        {
            _validation = validation;
            _uowFactory = uowFactory;
            _cashDrawer = cashDrawer;
            _accounting = accounting;
            _eventBus = eventBus;
        }

        public ExpenseResult Handle(RecordExpenseCommand command)
        {
            var validation = _validation.ValidateExpense(command);
            if (!validation.IsValid)
                throw new Core.Exceptions.ValidationException(validation.Errors);

            using var uow = _uowFactory.Create();

            int movementId = _cashDrawer.Debit(command.PaymentMethod, command.Amount, "مصروف عمومي", uow, accountCode: command.AccountCode);

            var lines = new List<JournalLineRequest>
            {
                new() { AccountCode = command.AccountCode, Debit = command.Amount, Credit = 0 },
                new() { AccountCode = AccountCodes.ForPaymentMethod(command.PaymentMethod), Debit = 0, Credit = command.Amount }
            };
            _accounting.Post(new JournalEntryRequest { SourceType = "Expense", SourceId = movementId, Description = "مصروف عمومي", Lines = lines }, command.PerformedBy, uow);

            uow.Commit();
            _eventBus.Publish(new ExpenseRecorded { AccountCode = command.AccountCode, Amount = command.Amount, PaymentMethod = command.PaymentMethod, PerformedBy = command.PerformedBy });

            return new ExpenseResult { MovementId = movementId };
        }
    }

    public class TransferFundsCommandHandler : ICommandHandler<TransferFundsCommand, TransferResult>
    {
        private readonly IValidationEngine _validation;
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly ICashDrawerEngine _cashDrawer;
        private readonly IAccountingEngine _accounting;
        private readonly IEventBus _eventBus;

        public TransferFundsCommandHandler(IValidationEngine validation, IUnitOfWorkFactory uowFactory, ICashDrawerEngine cashDrawer, IAccountingEngine accounting, IEventBus eventBus)
        {
            _validation = validation;
            _uowFactory = uowFactory;
            _cashDrawer = cashDrawer;
            _accounting = accounting;
            _eventBus = eventBus;
        }

        public TransferResult Handle(TransferFundsCommand command)
        {
            var validation = _validation.ValidateTransfer(command);
            if (!validation.IsValid)
                throw new Core.Exceptions.ValidationException(validation.Errors);

            using var uow = _uowFactory.Create();

            var (fromId, toId) = _cashDrawer.Transfer(command.FromMethod, command.ToMethod, command.Amount, command.Description, uow);

            // تحويل بين وسيلتين تحت سيطرة المحل نفسه - إعادة تصنيف بين حسابين أصل (لا يؤثر
            // على الأرباح/الخسائر)، لكنه لسه لازم يتسجل كقيد متزن زي أي عملية تانية
            var lines = new List<JournalLineRequest>
            {
                new() { AccountCode = AccountCodes.ForPaymentMethod(command.ToMethod), Debit = command.Amount, Credit = 0 },
                new() { AccountCode = AccountCodes.ForPaymentMethod(command.FromMethod), Debit = 0, Credit = command.Amount }
            };
            _accounting.Post(new JournalEntryRequest { SourceType = "Transfer", Description = $"تحويل من {command.FromMethod} إلى {command.ToMethod}", Lines = lines }, command.PerformedBy, uow);

            uow.Commit();
            _eventBus.Publish(new FundsTransferred { FromMethod = command.FromMethod, ToMethod = command.ToMethod, Amount = command.Amount, PerformedBy = command.PerformedBy });

            return new TransferResult { FromMovementId = fromId, ToMovementId = toId };
        }
    }
}
