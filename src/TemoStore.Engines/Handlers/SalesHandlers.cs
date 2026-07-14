using TemoStore.Core.Abstractions;
using TemoStore.Core.Commands;
using TemoStore.Core.Engines;
using TemoStore.Core.Entities;
using TemoStore.Core.Events;
using TemoStore.Core.Exceptions;

namespace TemoStore.Engines.Handlers
{
    internal static class AccountCodes
    {
        public const int Inventory = 1200;
        public const int Customers = 1300;
        public const int Suppliers = 2100;
        public const int SalesRevenue = 4100;
        public const int Cogs = 5500;

        public static int ForPaymentMethod(string method) => method switch
        {
            "نقدي" => 1100,
            "فوري" => 1110,
            "أمان" => 1120,
            "سهولة" => 1130,
            "فودافون كاش" => 1140,
            "إنستاباي" => 1150,
            _ => 1100
        };
    }

    // ==========================================================================
    // مسار عملية البيع كاملة زي ما هو موصوف في قسم 4 بمستند العمارة: تحقق (خارج
    // أي Transaction) → Transaction واحدة (مخزون + خزنة + قيد محاسبي + فحص تكامل
    // أخير) → Commit → نشر SaleCompleted على الـ Event Bus (بعد الـ Commit بس).
    // ==========================================================================
    public class CreateSaleCommandHandler : ICommandHandler<CreateSaleCommand, SaleResult>
    {
        private readonly IValidationEngine _validation;
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IInventoryEngine _inventory;
        private readonly ICashDrawerEngine _cashDrawer;
        private readonly IProfitEngine _profit;
        private readonly IAccountingEngine _accounting;
        private readonly IIntegrityEngine _integrity;
        private readonly IEventBus _eventBus;

        public CreateSaleCommandHandler(IValidationEngine validation, IUnitOfWorkFactory uowFactory, IInventoryEngine inventory,
            ICashDrawerEngine cashDrawer, IProfitEngine profit, IAccountingEngine accounting, IIntegrityEngine integrity, IEventBus eventBus)
        {
            _validation = validation;
            _uowFactory = uowFactory;
            _inventory = inventory;
            _cashDrawer = cashDrawer;
            _profit = profit;
            _accounting = accounting;
            _integrity = integrity;
            _eventBus = eventBus;
        }

        public SaleResult Handle(CreateSaleCommand command)
        {
            var validation = _validation.ValidateSale(command);
            if (!validation.IsValid)
                throw new ValidationException(validation.Errors);

            using var uow = _uowFactory.Create();

            var product = uow.Products.GetByBarcode(command.Barcode)!;
            decimal cost = product.Price * command.Quantity;

            int saleId = uow.Sales.Insert(new SaleRecord
            {
                Barcode = command.Barcode,
                ProductName = command.ProductName,
                CostPrice = product.Price,
                Price = command.UnitPrice,
                QuantitySold = command.Quantity,
                Total = command.Total,
                CustomerId = command.CustomerId,
                PaymentType = command.PaymentType,
                PaymentMethod = command.PaymentType == "Cash" ? command.PaymentMethod : null,
                Imei = command.Imei
            });

            _inventory.DeductStock(command.Barcode, command.Quantity, uow);
            if (command.Imei != null)
                _inventory.MarkImeiSold(command.Imei, saleId, uow);

            if (command.PaymentType == "Cash")
                _cashDrawer.Credit(command.PaymentMethod!, command.Total, $"قبض كاش من عملية بيع رقم {saleId}", uow, saleId: saleId, accountCode: AccountCodes.SalesRevenue);

            decimal profit = _profit.CalculateProfit(command.Total, cost);

            var lines = new List<JournalLineRequest>
            {
                new() { AccountCode = command.PaymentType == "Cash" ? AccountCodes.ForPaymentMethod(command.PaymentMethod!) : AccountCodes.Customers, Debit = command.Total, Credit = 0 },
                new() { AccountCode = AccountCodes.SalesRevenue, Debit = 0, Credit = command.Total }
            };
            if (cost > 0)
            {
                lines.Add(new JournalLineRequest { AccountCode = AccountCodes.Cogs, Debit = cost, Credit = 0 });
                lines.Add(new JournalLineRequest { AccountCode = AccountCodes.Inventory, Debit = 0, Credit = cost });
            }
            _accounting.Post(new JournalEntryRequest { SourceType = "Sale", SourceId = saleId, Description = $"بيع {command.ProductName}", Lines = lines }, command.PerformedBy, uow);

            var affectedMethods = command.PaymentType == "Cash" ? new[] { command.PaymentMethod! } : null;
            var integrity = _integrity.CheckBeforeCommit(uow, new[] { command.Barcode }, affectedMethods);
            if (!integrity.Passed)
                throw new InvalidOperationException(string.Join("؛ ", integrity.Violations));

            var saleRecord = uow.Sales.GetById(saleId)!;
            int dailyInvoiceNumber = uow.Sales.GetDailyInvoiceNumber(saleId, saleRecord.SaleDate);

            uow.Commit();

            _eventBus.Publish(new SaleCompleted
            {
                SaleId = saleId,
                Barcode = command.Barcode,
                ProductName = command.ProductName,
                QuantitySold = command.Quantity,
                Total = command.Total,
                Cost = cost,
                PaymentType = command.PaymentType,
                PaymentMethod = command.PaymentMethod,
                CustomerId = command.CustomerId,
                PerformedBy = command.PerformedBy
            });

            return new SaleResult { SaleId = saleId, DailyInvoiceNumber = dailyInvoiceNumber };
        }
    }

    public class UpdateSaleQuantityCommandHandler : ICommandHandler<UpdateSaleQuantityCommand, SaleResult>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly ICashDrawerEngine _cashDrawer;
        private readonly IAccountingEngine _accounting;
        private readonly IIntegrityEngine _integrity;
        private readonly IDateClosureRepository _dateClosure;
        private readonly IEventBus _eventBus;

        public UpdateSaleQuantityCommandHandler(IUnitOfWorkFactory uowFactory, ICashDrawerEngine cashDrawer, IAccountingEngine accounting,
            IIntegrityEngine integrity, IDateClosureRepository dateClosure, IEventBus eventBus)
        {
            _uowFactory = uowFactory;
            _cashDrawer = cashDrawer;
            _accounting = accounting;
            _integrity = integrity;
            _dateClosure = dateClosure;
            _eventBus = eventBus;
        }

        public SaleResult Handle(UpdateSaleQuantityCommand command)
        {
            using var uow = _uowFactory.Create();

            var sale = uow.Sales.GetById(command.SaleId) ?? throw new InvalidOperationException("لم يتم العثور على عملية البيع.");

            if (_dateClosure.IsClosed(DateTime.Parse(sale.SaleDate).Date))
                throw new DateClosedException("لا يمكن تعديل عملية بيع تابعة ليوم تم إقفاله بالفعل.");

            var product = uow.Products.GetByBarcode(sale.Barcode);
            int currentStock = product?.Quantity ?? 0;
            int availableIfReverted = currentStock + sale.QuantitySold;
            if (command.NewQuantity > availableIfReverted)
                throw new InsufficientStockException(availableIfReverted);

            decimal newTotal = sale.Price * command.NewQuantity;
            decimal totalDelta = newTotal - sale.Total;

            if (sale.PaymentType == "Cash" && sale.PaymentMethod != null && totalDelta != 0)
            {
                if (totalDelta > 0)
                    _cashDrawer.Credit(sale.PaymentMethod, totalDelta, $"تسوية كاش بسبب تعديل الكمية في عملية بيع رقم {command.SaleId}", uow, saleId: command.SaleId, accountCode: AccountCodes.SalesRevenue);
                else
                    _cashDrawer.Debit(sale.PaymentMethod, -totalDelta, $"تسوية كاش بسبب تعديل الكمية في عملية بيع رقم {command.SaleId}", uow, saleId: command.SaleId, accountCode: AccountCodes.SalesRevenue);

                var lines = new List<JournalLineRequest>
                {
                    new() { AccountCode = AccountCodes.ForPaymentMethod(sale.PaymentMethod), Debit = totalDelta > 0 ? totalDelta : 0, Credit = totalDelta < 0 ? -totalDelta : 0 },
                    new() { AccountCode = AccountCodes.SalesRevenue, Debit = totalDelta < 0 ? -totalDelta : 0, Credit = totalDelta > 0 ? totalDelta : 0 }
                };
                _accounting.Post(new JournalEntryRequest { SourceType = "SaleAdjustment", SourceId = command.SaleId, Description = $"تعديل كمية بيع رقم {command.SaleId}", Lines = lines }, command.PerformedBy, uow);
            }

            uow.Sales.UpdateQuantityAndTotal(command.SaleId, command.NewQuantity, newTotal);
            int stockAdjustment = sale.QuantitySold - command.NewQuantity;
            if (stockAdjustment > 0) uow.Products.AddQuantity(sale.Barcode, stockAdjustment);
            else if (stockAdjustment < 0) uow.Products.DeductQuantity(sale.Barcode, -stockAdjustment);

            var affectedMethods = sale.PaymentMethod != null ? new[] { sale.PaymentMethod } : null;
            var integrity = _integrity.CheckBeforeCommit(uow, new[] { sale.Barcode }, affectedMethods);
            if (!integrity.Passed)
                throw new InvalidOperationException(string.Join("؛ ", integrity.Violations));

            uow.Commit();

            return new SaleResult { SaleId = command.SaleId };
        }
    }

    public class CancelSaleCommandHandler : ICommandHandler<CancelSaleCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IInventoryEngine _inventory;
        private readonly ICashDrawerEngine _cashDrawer;
        private readonly IAccountingEngine _accounting;
        private readonly IDateClosureRepository _dateClosure;
        private readonly IEventBus _eventBus;

        public CancelSaleCommandHandler(IUnitOfWorkFactory uowFactory, IInventoryEngine inventory, ICashDrawerEngine cashDrawer,
            IAccountingEngine accounting, IDateClosureRepository dateClosure, IEventBus eventBus)
        {
            _uowFactory = uowFactory;
            _inventory = inventory;
            _cashDrawer = cashDrawer;
            _accounting = accounting;
            _dateClosure = dateClosure;
            _eventBus = eventBus;
        }

        public bool Handle(CancelSaleCommand command)
        {
            using var uow = _uowFactory.Create();

            var sale = uow.Sales.GetById(command.SaleId) ?? throw new InvalidOperationException("لم يتم العثور على عملية البيع.");

            if (_dateClosure.IsClosed(DateTime.Parse(sale.SaleDate).Date))
                throw new DateClosedException("لا يمكن إلغاء عملية بيع تابعة ليوم تم إقفاله بالفعل.");

            uow.Products.AddQuantity(sale.Barcode, sale.QuantitySold);
            if (sale.Imei != null)
                _inventory.MarkImeiInStock(sale.Imei, uow);

            if (sale.PaymentType == "Cash" && sale.PaymentMethod != null)
            {
                _cashDrawer.Debit(sale.PaymentMethod, sale.Total, $"قيد عكسي - إلغاء عملية بيع رقم {command.SaleId}", uow, saleId: command.SaleId, accountCode: AccountCodes.SalesRevenue);

                decimal cost = sale.CostPrice * sale.QuantitySold;
                var lines = new List<JournalLineRequest>
                {
                    new() { AccountCode = AccountCodes.SalesRevenue, Debit = sale.Total, Credit = 0 },
                    new() { AccountCode = AccountCodes.ForPaymentMethod(sale.PaymentMethod), Debit = 0, Credit = sale.Total }
                };
                if (cost > 0)
                {
                    lines.Add(new JournalLineRequest { AccountCode = AccountCodes.Inventory, Debit = cost, Credit = 0 });
                    lines.Add(new JournalLineRequest { AccountCode = AccountCodes.Cogs, Debit = 0, Credit = cost });
                }
                _accounting.Post(new JournalEntryRequest { SourceType = "SaleCancellation", SourceId = command.SaleId, Description = $"إلغاء بيع رقم {command.SaleId}", Lines = lines }, command.PerformedBy, uow);
            }

            uow.Sales.Delete(command.SaleId);

            uow.Commit();
            return true;
        }
    }
}
