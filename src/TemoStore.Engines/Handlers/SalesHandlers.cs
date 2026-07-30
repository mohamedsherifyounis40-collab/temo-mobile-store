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
        public const int MaintenanceRevenue = 4200;
        public const int Cogs = 5500;
        public const int Salaries = 5400;
        public const int EmployeeAdvances = 1400;
        public const int InventoryAdjustment = 5600;

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

            var saleIds = new List<int>();
            var lineCosts = new List<decimal>();
            var affectedBarcodes = new List<string>();
            decimal grandTotal = 0;
            decimal totalCost = 0;
            int invoiceId = 0;

            foreach (var line in command.Lines)
            {
                var product = uow.Products.GetByBarcode(line.Barcode)!;
                decimal lineCost = product.Price * line.Quantity;

                int saleId = uow.Sales.Insert(new SaleRecord
                {
                    Barcode = line.Barcode,
                    ProductName = line.ProductName,
                    CostPrice = product.Price,
                    Price = line.UnitPrice,
                    QuantitySold = line.Quantity,
                    Total = line.Total,
                    CustomerId = command.CustomerId,
                    PaymentType = command.PaymentType,
                    PaymentMethod = command.PaymentType == "Cash" ? command.PaymentMethod : null,
                    Imei = line.Imei,
                    SalesInvoiceId = invoiceId // أول صنف بيتسجل بـ 0 مؤقتًا، وبنرجع نظبطه تحت
                });

                if (invoiceId == 0)
                {
                    invoiceId = saleId;
                    uow.Sales.SetInvoiceId(saleId, invoiceId);
                }

                _inventory.DeductStock(line.Barcode, line.Quantity, uow);
                if (line.Imei != null)
                    _inventory.MarkImeiSold(line.Imei, saleId, uow);

                saleIds.Add(saleId);
                lineCosts.Add(lineCost);
                affectedBarcodes.Add(line.Barcode);
                grandTotal += line.Total;
                totalCost += lineCost;
            }

            if (command.PaymentType == "Cash")
                _cashDrawer.Credit(command.PaymentMethod!, grandTotal, $"قبض كاش من عملية بيع رقم {invoiceId}", uow, saleId: invoiceId, accountCode: AccountCodes.SalesRevenue);

            decimal profit = _profit.CalculateProfit(grandTotal, totalCost);

            var journalLines = new List<JournalLineRequest>
            {
                new() { AccountCode = command.PaymentType == "Cash" ? AccountCodes.ForPaymentMethod(command.PaymentMethod!) : AccountCodes.Customers, Debit = grandTotal, Credit = 0 },
                new() { AccountCode = AccountCodes.SalesRevenue, Debit = 0, Credit = grandTotal }
            };
            if (totalCost > 0)
            {
                journalLines.Add(new JournalLineRequest { AccountCode = AccountCodes.Cogs, Debit = totalCost, Credit = 0 });
                journalLines.Add(new JournalLineRequest { AccountCode = AccountCodes.Inventory, Debit = 0, Credit = totalCost });
            }
            string invoiceDescription = command.Lines.Count == 1 ? $"بيع {command.Lines[0].ProductName}" : $"فاتورة بيع رقم {invoiceId} ({command.Lines.Count} أصناف)";
            _accounting.Post(new JournalEntryRequest { SourceType = "Sale", SourceId = invoiceId, Description = invoiceDescription, Lines = journalLines }, command.PerformedBy, uow);

            var affectedMethods = command.PaymentType == "Cash" ? new[] { command.PaymentMethod! } : null;
            var integrity = _integrity.CheckBeforeCommit(uow, affectedBarcodes, affectedMethods);
            if (!integrity.Passed)
                throw new InvalidOperationException(string.Join("؛ ", integrity.Violations));

            var firstSaleRecord = uow.Sales.GetById(invoiceId)!;
            int dailyInvoiceNumber = uow.Sales.GetDailyInvoiceNumber(invoiceId, firstSaleRecord.SaleDate);

            uow.Commit();

            for (int i = 0; i < command.Lines.Count; i++)
            {
                var line = command.Lines[i];
                _eventBus.Publish(new SaleCompleted
                {
                    SaleId = saleIds[i],
                    Barcode = line.Barcode,
                    ProductName = line.ProductName,
                    QuantitySold = line.Quantity,
                    Total = line.Total,
                    Cost = lineCosts[i],
                    PaymentType = command.PaymentType,
                    PaymentMethod = command.PaymentMethod,
                    CustomerId = command.CustomerId,
                    PerformedBy = command.PerformedBy
                });
            }

            return new SaleResult { SaleId = saleIds[0], SalesInvoiceId = invoiceId, SaleIds = saleIds, DailyInvoiceNumber = dailyInvoiceNumber };
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
            // فرق التكلفة (COGS) بيتحرك مع فرق الكمية بنفس الاتجاه - زوّدت الكمية يبقى
            // تكلفة إضافية اتحمّلت، قلّلتها يبقى تكلفة بترجع للمخزون
            decimal costDelta = sale.CostPrice * (command.NewQuantity - sale.QuantitySold);

            var lines = new List<JournalLineRequest>();

            if (totalDelta != 0)
            {
                if (sale.PaymentType == "Cash" && sale.PaymentMethod != null)
                {
                    if (totalDelta > 0)
                        _cashDrawer.Credit(sale.PaymentMethod, totalDelta, $"تسوية كاش بسبب تعديل الكمية في عملية بيع رقم {command.SaleId}", uow, saleId: command.SaleId, accountCode: AccountCodes.SalesRevenue);
                    else
                        _cashDrawer.Debit(sale.PaymentMethod, -totalDelta, $"تسوية كاش بسبب تعديل الكمية في عملية بيع رقم {command.SaleId}", uow, saleId: command.SaleId, accountCode: AccountCodes.SalesRevenue);

                    lines.Add(new() { AccountCode = AccountCodes.ForPaymentMethod(sale.PaymentMethod), Debit = totalDelta > 0 ? totalDelta : 0, Credit = totalDelta < 0 ? -totalDelta : 0 });
                    lines.Add(new() { AccountCode = AccountCodes.SalesRevenue, Debit = totalDelta < 0 ? -totalDelta : 0, Credit = totalDelta > 0 ? totalDelta : 0 });
                }
                else if (sale.PaymentType == "Credit")
                {
                    // بيع آجل: مفيش كاش يتحرك، الفرق كله بيروح لرصيد العميل (1300) بدل وسيلة الدفع
                    lines.Add(new() { AccountCode = AccountCodes.Customers, Debit = totalDelta > 0 ? totalDelta : 0, Credit = totalDelta < 0 ? -totalDelta : 0 });
                    lines.Add(new() { AccountCode = AccountCodes.SalesRevenue, Debit = totalDelta < 0 ? -totalDelta : 0, Credit = totalDelta > 0 ? totalDelta : 0 });
                }
            }

            if (costDelta != 0)
            {
                lines.Add(new() { AccountCode = AccountCodes.Cogs, Debit = costDelta > 0 ? costDelta : 0, Credit = costDelta < 0 ? -costDelta : 0 });
                lines.Add(new() { AccountCode = AccountCodes.Inventory, Debit = costDelta < 0 ? -costDelta : 0, Credit = costDelta > 0 ? costDelta : 0 });
            }

            if (lines.Count > 0)
                _accounting.Post(new JournalEntryRequest { SourceType = "SaleAdjustment", SourceId = command.SaleId, Description = $"تعديل كمية بيع رقم {command.SaleId}", Lines = lines }, command.PerformedBy, uow);

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

            // عكس أثر البيع بالكامل في الدفتر - الإيراد والتكلفة/المخزون بيترجعوا دايمًا
            // بغض النظر عن نوع البيع، والفرق الوحيد هو الطرف التاني للإيراد (كاش أو عميل)
            var lines = new List<JournalLineRequest>();

            if (sale.Total != 0)
            {
                lines.Add(new() { AccountCode = AccountCodes.SalesRevenue, Debit = sale.Total, Credit = 0 });
                if (sale.PaymentType == "Cash" && sale.PaymentMethod != null)
                    lines.Add(new() { AccountCode = AccountCodes.ForPaymentMethod(sale.PaymentMethod), Debit = 0, Credit = sale.Total });
                else
                    lines.Add(new() { AccountCode = AccountCodes.Customers, Debit = 0, Credit = sale.Total });
            }

            if (sale.PaymentType == "Cash" && sale.PaymentMethod != null && sale.Total != 0)
                _cashDrawer.Debit(sale.PaymentMethod, sale.Total, $"قيد عكسي - إلغاء عملية بيع رقم {command.SaleId}", uow, saleId: command.SaleId, accountCode: AccountCodes.SalesRevenue);

            decimal cost = sale.CostPrice * sale.QuantitySold;
            if (cost > 0)
            {
                lines.Add(new JournalLineRequest { AccountCode = AccountCodes.Inventory, Debit = cost, Credit = 0 });
                lines.Add(new JournalLineRequest { AccountCode = AccountCodes.Cogs, Debit = 0, Credit = cost });
            }
            if (lines.Count > 0)
                _accounting.Post(new JournalEntryRequest { SourceType = "SaleCancellation", SourceId = command.SaleId, Description = $"إلغاء بيع رقم {command.SaleId}", Lines = lines }, command.PerformedBy, uow);

            uow.Sales.Delete(command.SaleId);

            uow.Commit();
            return true;
        }
    }
}
