using TemoStore.Core.Abstractions;
using TemoStore.Core.Commands;
using TemoStore.Core.Engines;
using TemoStore.Core.Entities;
using TemoStore.Core.Events;
using TemoStore.Core.Exceptions;

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

    // ملحوظة تصميمية: بند "مصروف عمومي" بيتسجل في جدول Expenses المنفصل (مش
    // CashMovements) - اختيار قديم موجود من الأول بيغذّي شاشة/تقارير المصروفات
    // بالتحديد. عشان كده بيتعامل مع الرصيد مباشرة (GetBalance/SetBalance) بدل
    // ICashDrawerEngine.Debit، اللي كان هيضيف حركة في CashMovements كمان ويغيّر
    // سلوك موجود بالفعل (المصروفات معندهاش سجل في دفتر القبض/الصرف العام).
    public class RecordExpenseCommandHandler : ICommandHandler<RecordExpenseCommand, ExpenseResult>
    {
        private readonly IValidationEngine _validation;
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IAccountingEngine _accounting;
        private readonly IEventBus _eventBus;

        public RecordExpenseCommandHandler(IValidationEngine validation, IUnitOfWorkFactory uowFactory, IAccountingEngine accounting, IEventBus eventBus)
        {
            _validation = validation;
            _uowFactory = uowFactory;
            _accounting = accounting;
            _eventBus = eventBus;
        }

        public ExpenseResult Handle(RecordExpenseCommand command)
        {
            var validation = _validation.ValidateExpense(command);
            if (!validation.IsValid)
                throw new ValidationException(validation.Errors);

            using var uow = _uowFactory.Create();

            decimal currentBalance = uow.CashDrawer.GetBalance(command.PaymentMethod);
            if (command.Amount > currentBalance)
                throw new InsufficientBalanceException(command.PaymentMethod, currentBalance);

            int expenseId = uow.Expenses.Insert(command.AccountCode, command.Amount, command.PaymentMethod);
            uow.CashDrawer.SetBalance(command.PaymentMethod, currentBalance - command.Amount);

            var lines = new List<JournalLineRequest>
            {
                new() { AccountCode = command.AccountCode, Debit = command.Amount, Credit = 0 },
                new() { AccountCode = AccountCodes.ForPaymentMethod(command.PaymentMethod), Debit = 0, Credit = command.Amount }
            };
            _accounting.Post(new JournalEntryRequest { SourceType = "Expense", SourceId = expenseId, Description = "مصروف عمومي", Lines = lines }, command.PerformedBy, uow);

            uow.Commit();
            _eventBus.Publish(new ExpenseRecorded { AccountCode = command.AccountCode, Amount = command.Amount, PaymentMethod = command.PaymentMethod, PerformedBy = command.PerformedBy });

            return new ExpenseResult { MovementId = expenseId };
        }
    }

    public class UpdateExpenseCommandHandler : ICommandHandler<UpdateExpenseCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;

        public UpdateExpenseCommandHandler(IUnitOfWorkFactory uowFactory)
        {
            _uowFactory = uowFactory;
        }

        public bool Handle(UpdateExpenseCommand command)
        {
            using var uow = _uowFactory.Create();

            var existing = uow.Expenses.GetById(command.ExpenseId) ?? throw new InvalidOperationException("لم يتم العثور على حركة المصروف.");

            decimal oldMethodBalance = uow.CashDrawer.GetBalance(existing.PaymentMethod);
            decimal revertedOldBalance = oldMethodBalance + existing.Amount;

            decimal newMethodBalance = command.PaymentMethod == existing.PaymentMethod
                ? revertedOldBalance
                : uow.CashDrawer.GetBalance(command.PaymentMethod);

            if (command.Amount > newMethodBalance)
                throw new InsufficientBalanceException(command.PaymentMethod, newMethodBalance);

            uow.CashDrawer.SetBalance(existing.PaymentMethod, revertedOldBalance);
            uow.CashDrawer.SetBalance(command.PaymentMethod, newMethodBalance - command.Amount);
            uow.Expenses.Update(command.ExpenseId, command.AccountCode, command.Amount, command.PaymentMethod);

            uow.Commit();
            return true;
        }
    }

    public class DeleteExpenseCommandHandler : ICommandHandler<DeleteExpenseCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;

        public DeleteExpenseCommandHandler(IUnitOfWorkFactory uowFactory)
        {
            _uowFactory = uowFactory;
        }

        public bool Handle(DeleteExpenseCommand command)
        {
            using var uow = _uowFactory.Create();

            var existing = uow.Expenses.GetById(command.ExpenseId) ?? throw new InvalidOperationException("لم يتم العثور على حركة المصروف.");

            decimal currentBalance = uow.CashDrawer.GetBalance(existing.PaymentMethod);
            uow.CashDrawer.SetBalance(existing.PaymentMethod, currentBalance + existing.Amount);
            uow.Expenses.Delete(command.ExpenseId);

            uow.Commit();
            return true;
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

    // بيلغي تحويل بين وسائل دفع (حركتين مرتبطتين) مع بعض - بيرجّع الرصيدين لحالتهم قبل التحويل
    public class CancelTransferCommandHandler : ICommandHandler<CancelTransferCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;

        public CancelTransferCommandHandler(IUnitOfWorkFactory uowFactory)
        {
            _uowFactory = uowFactory;
        }

        public bool Handle(CancelTransferCommand command)
        {
            using var uow = _uowFactory.Create();

            var first = uow.CashDrawer.GetMovementById(command.MovementId) ?? throw new InvalidOperationException("لم يتم العثور على الحركة.");
            if (first.LinkedMovementId == null)
                throw new InvalidOperationException("الحركة دي مش جزء من عملية تحويل.");

            var second = uow.CashDrawer.GetMovementById(first.LinkedMovementId.Value) ?? throw new InvalidOperationException("لم يتم العثور على الحركة المرتبطة بالتحويل.");

            foreach (var rec in new[] { first, second })
            {
                decimal currentBalance = uow.CashDrawer.GetBalance(rec.PaymentMethod);
                decimal newBalance = rec.MovementType == "قبض" ? currentBalance - rec.Amount : currentBalance + rec.Amount;
                if (newBalance < 0)
                    throw new InsufficientBalanceException(rec.PaymentMethod, currentBalance);
                uow.CashDrawer.SetBalance(rec.PaymentMethod, newBalance);
            }

            uow.CashDrawer.DeleteMovement(first.Id);
            uow.CashDrawer.DeleteMovement(second.Id);

            uow.Commit();
            return true;
        }
    }

    // حركة قبض/صرف عامة (مش مربوطة ببند مصروف) - تسجيل يدوي حر بحساب اختياري
    public class AddMovementCommandHandler : ICommandHandler<AddMovementCommand, PaymentResult>
    {
        private readonly IUnitOfWorkFactory _uowFactory;

        public AddMovementCommandHandler(IUnitOfWorkFactory uowFactory)
        {
            _uowFactory = uowFactory;
        }

        public PaymentResult Handle(AddMovementCommand command)
        {
            using var uow = _uowFactory.Create();

            decimal currentBalance = uow.CashDrawer.GetBalance(command.Method);
            if (command.Type == "صرف" && command.Amount > currentBalance)
                throw new InsufficientBalanceException(command.Method, currentBalance);

            int movementId = uow.CashDrawer.InsertMovement(new CashMovementRecord
            {
                MovementType = command.Type,
                PaymentMethod = command.Method,
                Amount = command.Amount,
                ReferenceNumber = command.Reference,
                Description = command.Description,
                AccountCode = command.AccountCode
            });

            decimal newBalance = command.Type == "قبض" ? currentBalance + command.Amount : currentBalance - command.Amount;
            uow.CashDrawer.SetBalance(command.Method, newBalance);

            uow.Commit();
            return new PaymentResult { MovementId = movementId };
        }
    }

    public class UpdateMovementCommandHandler : ICommandHandler<UpdateMovementCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;

        public UpdateMovementCommandHandler(IUnitOfWorkFactory uowFactory)
        {
            _uowFactory = uowFactory;
        }

        public bool Handle(UpdateMovementCommand command)
        {
            using var uow = _uowFactory.Create();

            var existing = uow.CashDrawer.GetMovementById(command.MovementId) ?? throw new InvalidOperationException("لم يتم العثور على الحركة.");
            if (existing.LinkedMovementId != null)
                throw new InvalidOperationException("الحركة دي جزء من عملية تحويل بين وسائل الدفع، مينفعش تتعدل مباشرة. إلغي التحويل الحالي واعمل تحويل جديد صحيح.");

            decimal oldMethodBalance = uow.CashDrawer.GetBalance(existing.PaymentMethod);
            decimal revertedOldBalance = existing.MovementType == "قبض" ? oldMethodBalance - existing.Amount : oldMethodBalance + existing.Amount;

            decimal newMethodBalance = command.NewMethod == existing.PaymentMethod
                ? revertedOldBalance
                : uow.CashDrawer.GetBalance(command.NewMethod);

            if (command.NewType == "صرف" && command.NewAmount > newMethodBalance)
                throw new InsufficientBalanceException(command.NewMethod, newMethodBalance);

            uow.CashDrawer.SetBalance(existing.PaymentMethod, revertedOldBalance);
            decimal finalNewBalance = command.NewType == "قبض" ? newMethodBalance + command.NewAmount : newMethodBalance - command.NewAmount;
            uow.CashDrawer.SetBalance(command.NewMethod, finalNewBalance);

            uow.CashDrawer.UpdateMovement(command.MovementId, command.NewType, command.NewMethod, command.NewAmount, command.Reference, command.Description, command.AccountCode);

            uow.Commit();
            return true;
        }
    }

    public class CancelMovementCommandHandler : ICommandHandler<CancelMovementCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;

        public CancelMovementCommandHandler(IUnitOfWorkFactory uowFactory)
        {
            _uowFactory = uowFactory;
        }

        public bool Handle(CancelMovementCommand command)
        {
            using var uow = _uowFactory.Create();

            var existing = uow.CashDrawer.GetMovementById(command.MovementId) ?? throw new InvalidOperationException("لم يتم العثور على الحركة.");
            if (existing.LinkedMovementId != null)
                throw new InvalidOperationException("الحركة دي جزء من عملية تحويل - استخدم إلغاء التحويل بدل إلغاء الحركة المفردة.");

            decimal currentBalance = uow.CashDrawer.GetBalance(existing.PaymentMethod);
            decimal newBalance = existing.MovementType == "قبض" ? currentBalance - existing.Amount : currentBalance + existing.Amount;
            uow.CashDrawer.SetBalance(existing.PaymentMethod, newBalance);
            uow.CashDrawer.DeleteMovement(command.MovementId);

            uow.Commit();
            return true;
        }
    }
}
