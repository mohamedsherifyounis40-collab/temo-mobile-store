using TemoStore.Core.Abstractions;
using TemoStore.Core.Commands;
using TemoStore.Core.Engines;
using TemoStore.Core.Entities;
using TemoStore.Core.Events;
using TemoStore.Core.Exceptions;

namespace TemoStore.Engines.Handlers
{
    public class CreatePurchaseCommandHandler : ICommandHandler<CreatePurchaseCommand, PurchaseResult>
    {
        private readonly IValidationEngine _validation;
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IInventoryEngine _inventory;
        private readonly ICashDrawerEngine _cashDrawer;
        private readonly IAccountingEngine _accounting;
        private readonly IIntegrityEngine _integrity;
        private readonly IEventBus _eventBus;

        public CreatePurchaseCommandHandler(IValidationEngine validation, IUnitOfWorkFactory uowFactory, IInventoryEngine inventory,
            ICashDrawerEngine cashDrawer, IAccountingEngine accounting, IIntegrityEngine integrity, IEventBus eventBus)
        {
            _validation = validation;
            _uowFactory = uowFactory;
            _inventory = inventory;
            _cashDrawer = cashDrawer;
            _accounting = accounting;
            _integrity = integrity;
            _eventBus = eventBus;
        }

        public PurchaseResult Handle(CreatePurchaseCommand command)
        {
            var validation = _validation.ValidatePurchase(command);
            if (!validation.IsValid)
                throw new ValidationException(validation.Errors);

            decimal totalAmount = 0;
            foreach (var line in command.Lines) totalAmount += line.LineTotal;

            using var uow = _uowFactory.Create();

            int purchaseId = uow.Purchases.InsertPurchase(command.SupplierId, totalAmount);
            ApplyLines(uow, purchaseId, command.Lines);

            if (command.PayCashNow)
                _cashDrawer.Debit(command.CashMethod!, totalAmount, $"سداد كاش فوري لفاتورة شراء رقم {purchaseId}", uow, purchaseId: purchaseId, accountCode: AccountCodes.Suppliers);

            var lines = new List<JournalLineRequest>
            {
                new() { AccountCode = AccountCodes.Inventory, Debit = totalAmount, Credit = 0 },
                new() { AccountCode = command.PayCashNow ? AccountCodes.ForPaymentMethod(command.CashMethod!) : AccountCodes.Suppliers, Debit = 0, Credit = totalAmount }
            };
            _accounting.Post(new JournalEntryRequest { SourceType = "Purchase", SourceId = purchaseId, Description = $"فاتورة شراء رقم {purchaseId}", Lines = lines }, command.PerformedBy, uow);

            var affectedBarcodes = new List<string>();
            foreach (var l in command.Lines) if (!string.IsNullOrEmpty(l.Barcode)) affectedBarcodes.Add(l.Barcode);
            var affectedMethods = command.PayCashNow ? new[] { command.CashMethod! } : null;
            var integrity = _integrity.CheckBeforeCommit(uow, affectedBarcodes, affectedMethods);
            if (!integrity.Passed)
                throw new InvalidOperationException(string.Join("؛ ", integrity.Violations));

            uow.Commit();

            _eventBus.Publish(new PurchaseCompleted { PurchaseId = purchaseId, SupplierId = command.SupplierId, Total = totalAmount, PaidCashNow = command.PayCashNow, PaymentMethod = command.CashMethod, PerformedBy = command.PerformedBy });

            return new PurchaseResult { PurchaseId = purchaseId, TotalAmount = totalAmount };
        }

        private static void ApplyLines(IUnitOfWork uow, int purchaseId, List<PurchaseLine> lines)
        {
            foreach (var line in lines)
            {
                uow.Purchases.InsertItem(purchaseId, line);

                if (line.SkipInventory) continue;

                string barcode = line.Barcode ?? string.Empty;
                var existing = string.IsNullOrEmpty(barcode) ? null : uow.Products.GetByBarcode(barcode);
                string finalBarcode = existing != null ? barcode
                    : (string.IsNullOrEmpty(barcode) ? "NEW-" + DateTime.Now.Ticks : barcode);

                uow.Products.UpsertOnPurchase(finalBarcode, line.ProductName, line.UnitCost, line.SalePrice, line.Qty, line.IsSerialized);

                if (line.IsSerialized && line.Imeis != null)
                {
                    foreach (var imei in line.Imeis)
                        uow.Products.InsertProductUnit(finalBarcode, imei, purchaseId);
                }
            }
        }
    }

    // بيرجّع أثر فاتورة شراء (مخزون/IMEI/كاش) بالكامل لحالتها قبل الفاتورة - مستخدم
    // من الإلغاء والتعديل (اللي بيرجّع القديم الأول قبل ما يطبّق الجديد)
    internal static class PurchaseReversal
    {
        public static decimal Reverse(IUnitOfWork uow, IInventoryEngine inventory, ICashDrawerEngine cashDrawer, IAccountingEngine accounting,
            IDateClosureRepository dateClosure, int purchaseId, string performedBy)
        {
            var purchase = uow.Purchases.GetById(purchaseId) ?? throw new InvalidOperationException("لم يتم العثور على فاتورة الشراء.");

            if (dateClosure.IsClosed(DateTime.Parse(purchase.PurchaseDate).Date))
                throw new DateClosedException("لا يمكن إلغاء أو تعديل فاتورة شراء تابعة ليوم تم إقفاله بالفعل.");

            inventory.ReverseStockForCancelledPurchase(purchaseId, uow);
            uow.Purchases.DeleteItems(purchaseId);

            // عكس القيد الأصلي بالكامل: كان "مدين مخزون / دائن (وسيلة الدفع أو موردون)"،
            // فبنعمله "مدين (وسيلة الدفع أو موردون) / دائن مخزون" - يوازي عكس المخزون
            // اللي حصل فعليًا فوق. لو كانت الفاتورة اتسددت كاش، بنرجّع الكاش نفسه كمان
            // (قيد/حركة عكسية، مش حذف الحركة الأصلية) عشان يفضل أثرها في السجل للمراجعة.
            var cashMovement = uow.CashDrawer.FindByPurchaseId(purchaseId);
            int drOrCrAccount = cashMovement != null ? AccountCodes.ForPaymentMethod(cashMovement.PaymentMethod) : AccountCodes.Suppliers;

            var reversalLines = new List<JournalLineRequest>
            {
                new() { AccountCode = drOrCrAccount, Debit = purchase.TotalAmount, Credit = 0 },
                new() { AccountCode = AccountCodes.Inventory, Debit = 0, Credit = purchase.TotalAmount }
            };
            accounting.Post(new JournalEntryRequest { SourceType = "PurchaseReversal", SourceId = purchaseId, Description = $"عكس فاتورة شراء رقم {purchaseId}", Lines = reversalLines }, performedBy, uow);

            if (cashMovement != null)
            {
                cashDrawer.Credit(cashMovement.PaymentMethod, cashMovement.Amount,
                    $"قيد عكسي - استرجاع سداد كاش فوري لفاتورة شراء رقم {purchaseId}", uow, purchaseId: purchaseId, accountCode: AccountCodes.Suppliers);
            }

            return purchase.TotalAmount;
        }
    }

    public class EditPurchaseCommandHandler : ICommandHandler<EditPurchaseCommand, PurchaseResult>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IInventoryEngine _inventory;
        private readonly ICashDrawerEngine _cashDrawer;
        private readonly IAccountingEngine _accounting;
        private readonly IIntegrityEngine _integrity;
        private readonly IDateClosureRepository _dateClosure;
        private readonly IEventBus _eventBus;

        public EditPurchaseCommandHandler(IUnitOfWorkFactory uowFactory, IInventoryEngine inventory, ICashDrawerEngine cashDrawer,
            IAccountingEngine accounting, IIntegrityEngine integrity, IDateClosureRepository dateClosure, IEventBus eventBus)
        {
            _uowFactory = uowFactory;
            _inventory = inventory;
            _cashDrawer = cashDrawer;
            _accounting = accounting;
            _integrity = integrity;
            _dateClosure = dateClosure;
            _eventBus = eventBus;
        }

        public PurchaseResult Handle(EditPurchaseCommand command)
        {
            using var uow = _uowFactory.Create();

            PurchaseReversal.Reverse(uow, _inventory, _cashDrawer, _accounting, _dateClosure, command.PurchaseId, command.PerformedBy);

            // فحص تكرار الـIMEI بعد ما الفاتورة القديمة اتشالت (اللي ممكن تكون هي نفسها
            // فرّغت نفس الأرقام دي لو المستخدم مغيرش فيها) - IMEI عمود UNIQUE في الجدول،
            // فمن غير الفحص ده هنا هيطلع Exception خام من قاعدة البيانات بدل رسالة واضحة
            foreach (var line in command.Lines)
            {
                if (!line.IsSerialized || line.Imeis == null) continue;
                foreach (var imei in line.Imeis)
                    if (uow.Products.ImeiExists(imei))
                        throw new DuplicateImeiException(imei);
            }

            decimal totalAmount = 0;
            foreach (var line in command.Lines) totalAmount += line.LineTotal;

            uow.Purchases.UpdatePurchase(command.PurchaseId, command.SupplierId, totalAmount);
            foreach (var line in command.Lines)
            {
                uow.Purchases.InsertItem(command.PurchaseId, line);

                if (line.SkipInventory) continue;

                string barcode = line.Barcode ?? string.Empty;
                var existing = string.IsNullOrEmpty(barcode) ? null : uow.Products.GetByBarcode(barcode);
                string finalBarcode = existing != null ? barcode
                    : (string.IsNullOrEmpty(barcode) ? "NEW-" + DateTime.Now.Ticks : barcode);

                uow.Products.UpsertOnPurchase(finalBarcode, line.ProductName, line.UnitCost, line.SalePrice, line.Qty, line.IsSerialized);
                if (line.IsSerialized && line.Imeis != null)
                    foreach (var imei in line.Imeis)
                        uow.Products.InsertProductUnit(finalBarcode, imei, command.PurchaseId);
            }

            if (command.PayCashNow)
                _cashDrawer.Debit(command.CashMethod!, totalAmount, $"سداد كاش فوري لفاتورة شراء رقم {command.PurchaseId} (بعد تعديل)", uow, purchaseId: command.PurchaseId, accountCode: AccountCodes.Suppliers);

            var lines = new List<JournalLineRequest>
            {
                new() { AccountCode = AccountCodes.Inventory, Debit = totalAmount, Credit = 0 },
                new() { AccountCode = command.PayCashNow ? AccountCodes.ForPaymentMethod(command.CashMethod!) : AccountCodes.Suppliers, Debit = 0, Credit = totalAmount }
            };
            _accounting.Post(new JournalEntryRequest { SourceType = "PurchaseEdit", SourceId = command.PurchaseId, Description = $"تعديل فاتورة شراء رقم {command.PurchaseId}", Lines = lines }, command.PerformedBy, uow);

            uow.Commit();
            return new PurchaseResult { PurchaseId = command.PurchaseId, TotalAmount = totalAmount };
        }
    }

    public class CancelPurchaseCommandHandler : ICommandHandler<CancelPurchaseCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IInventoryEngine _inventory;
        private readonly ICashDrawerEngine _cashDrawer;
        private readonly IAccountingEngine _accounting;
        private readonly IDateClosureRepository _dateClosure;
        private readonly IEventBus _eventBus;

        public CancelPurchaseCommandHandler(IUnitOfWorkFactory uowFactory, IInventoryEngine inventory, ICashDrawerEngine cashDrawer,
            IAccountingEngine accounting, IDateClosureRepository dateClosure, IEventBus eventBus)
        {
            _uowFactory = uowFactory;
            _inventory = inventory;
            _cashDrawer = cashDrawer;
            _accounting = accounting;
            _dateClosure = dateClosure;
            _eventBus = eventBus;
        }

        public bool Handle(CancelPurchaseCommand command)
        {
            using var uow = _uowFactory.Create();
            decimal totalAmount = PurchaseReversal.Reverse(uow, _inventory, _cashDrawer, _accounting, _dateClosure, command.PurchaseId, command.PerformedBy);
            uow.Purchases.DeletePurchase(command.PurchaseId);
            uow.Commit();
            return true;
        }
    }
}
