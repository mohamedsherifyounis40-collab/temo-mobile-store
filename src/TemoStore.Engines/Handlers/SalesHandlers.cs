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
        public const int TaxPayable = 2200;
        public const int SalesRevenue = 4100;
        public const int SalesDiscounts = 4110;    // حساب مقابل (Contra-Revenue) - خصومات ممنوحة على المبيعات
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
            "ممكن" or "سهولة" => 1130, // "سهولة" اسمها القديم قبل إعادة التسمية - لسه متعرف عشان أي سجل قديم بيها
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
            decimal grandTotal = 0;      // إجمالي الفاتورة قبل الخصم (Gross) - بيتسجل كإيراد كامل في الدفتر
            decimal totalDiscount = 0;   // إجمالي الخصم على كل الأصناف
            decimal totalTax = 0;        // إجمالي الضريبة على كل الأصناف
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
                    Discount = line.Discount,
                    Tax = line.Tax,
                    CustomerId = command.CustomerId,
                    PaymentType = command.PaymentType,
                    PaymentMethod = command.PaymentType == "Cash" ? command.PaymentMethod : null,
                    Imei = line.Imei,
                    SalesInvoiceId = invoiceId, // أول صنف بيتسجل بـ 0 مؤقتًا، وبنرجع نظبطه تحت
                    Notes = command.Notes ?? ""
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
                totalDiscount += line.Discount;
                totalTax += line.Tax;
                totalCost += lineCost;
            }

            // الصافي الفعلي اللي بيتحصّل كاش أو بيتسجل على العميل - بعد خصم الخصومات وإضافة الضريبة
            decimal netAmount = grandTotal - totalDiscount;
            decimal amountDue = netAmount + totalTax;

            // "المدفوع"/"الباقي" (فكة) - حاسبة بس، ومعندهاش أي أثر على المبلغ اللي بيتقبض
            // فعليًا في الخزينة (اللي دايمًا = amountDue بالظبط) لأن الفكة بترجع للعميل فورًا
            decimal amountPaid = 0;
            decimal changeDue = 0;
            if (command.PaymentType == "Cash")
            {
                amountPaid = command.AmountPaid ?? amountDue;
                changeDue = amountPaid - amountDue;
            }

            if (command.PaymentType == "Cash")
                _cashDrawer.Credit(command.PaymentMethod!, amountDue, $"قبض كاش من عملية بيع رقم {invoiceId}", uow, saleId: invoiceId, accountCode: AccountCodes.SalesRevenue);

            decimal profit = _profit.CalculateProfit(netAmount, totalCost);

            // الإيراد بيتسجل كامل (Gross) في SalesRevenue، والخصم بيتسجل منفصل في حساب مقابل
            // (SalesDiscounts) عشان الدفتر يوضّح الإيراد الكامل والخصومات الممنوحة كل واحد لوحده.
            // الضريبة مش إيراد للمحل - بتتسجل كالتزام (TaxPayable) لحد ما تتورد لمصلحة الضرائب.
            var journalLines = new List<JournalLineRequest>
            {
                new() { AccountCode = command.PaymentType == "Cash" ? AccountCodes.ForPaymentMethod(command.PaymentMethod!) : AccountCodes.Customers, Debit = amountDue, Credit = 0 },
                new() { AccountCode = AccountCodes.SalesRevenue, Debit = 0, Credit = grandTotal }
            };
            if (totalDiscount > 0)
                journalLines.Add(new JournalLineRequest { AccountCode = AccountCodes.SalesDiscounts, Debit = totalDiscount, Credit = 0 });
            if (totalTax > 0)
                journalLines.Add(new JournalLineRequest { AccountCode = AccountCodes.TaxPayable, Debit = 0, Credit = totalTax });
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

            uow.Sales.SetAmountPaid(invoiceId, amountPaid);

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

            return new SaleResult
            {
                SaleId = saleIds[0],
                SalesInvoiceId = invoiceId,
                SaleIds = saleIds,
                DailyInvoiceNumber = dailyInvoiceNumber,
                TaxTotal = totalTax,
                AmountPaid = amountPaid,
                ChangeDue = changeDue
            };
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
            // بغض النظر عن نوع البيع، والفرق الوحيد هو الطرف التاني للإيراد (كاش أو عميل).
            // لو كان فيه خصم، بيترجع هو كمان (عكس القيد الأصلي بالظبط): الإيراد الكامل بيترجع،
            // وحساب الخصومات بيترجع، ولو كان فيه ضريبة بترجع هي كمان من TaxPayable، والصافي
            // شامل الضريبة (اللي فعليًا اتحصّل/اتسجل على العميل) هو اللي بيرجع كاش/عميل
            var netAmount = sale.Total - sale.Discount;
            var amountDue = netAmount + sale.Tax;
            var lines = new List<JournalLineRequest>();

            if (sale.Total != 0)
            {
                lines.Add(new() { AccountCode = AccountCodes.SalesRevenue, Debit = sale.Total, Credit = 0 });
                if (sale.Discount > 0)
                    lines.Add(new() { AccountCode = AccountCodes.SalesDiscounts, Debit = 0, Credit = sale.Discount });
                if (sale.Tax > 0)
                    lines.Add(new() { AccountCode = AccountCodes.TaxPayable, Debit = sale.Tax, Credit = 0 });
                if (sale.PaymentType == "Cash" && sale.PaymentMethod != null)
                    lines.Add(new() { AccountCode = AccountCodes.ForPaymentMethod(sale.PaymentMethod), Debit = 0, Credit = amountDue });
                else
                    lines.Add(new() { AccountCode = AccountCodes.Customers, Debit = 0, Credit = amountDue });
            }

            if (sale.PaymentType == "Cash" && sale.PaymentMethod != null && amountDue != 0)
                _cashDrawer.Debit(sale.PaymentMethod, amountDue, $"قيد عكسي - إلغاء عملية بيع رقم {command.SaleId}", uow, saleId: command.SaleId, accountCode: AccountCodes.SalesRevenue);

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

    // ==========================================================================
    // مرتجع (كلي أو جزئي) لصنف من عملية بيع سابقة. على عكس CancelSaleCommand، عملية
    // البيع الأصلية (Sales) بتفضل زي ما هي بالظبط - سجل تاريخي ثابت - وكل حركة مرتجع
    // بتتوثّق في جدول Returns المستقل بسببها ومبلغها. القيد المحاسبي عكس نسبة الكمية
    // المرتجعة من الفاتورة الأصلية بالظبط (نفس منطق CancelSale، لكن على جزء من الكمية
    // مش كلها بالضرورة)، والمرتجع بيتنفذ حتى لو يوم البيع الأصلي مُقفل من زمان - هو
    // نفسه حدث جديد بتاريخ النهارده (ValidationEngine.ValidateReturn بيرفض بس لو
    // *النهارده* مُقفل، زي أي عملية جديدة تانية في النظام).
    // ==========================================================================
    public class CreateReturnCommandHandler : ICommandHandler<CreateReturnCommand, ReturnResult>
    {
        private readonly IValidationEngine _validation;
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IInventoryEngine _inventory;
        private readonly ICashDrawerEngine _cashDrawer;
        private readonly IAccountingEngine _accounting;
        private readonly IIntegrityEngine _integrity;
        private readonly IEventBus _eventBus;

        public CreateReturnCommandHandler(IValidationEngine validation, IUnitOfWorkFactory uowFactory, IInventoryEngine inventory,
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

        public ReturnResult Handle(CreateReturnCommand command)
        {
            var validation = _validation.ValidateReturn(command);
            if (!validation.IsValid)
                throw new ValidationException(validation.Errors);

            using var uow = _uowFactory.Create();

            var sale = uow.Sales.GetById(command.SaleId) ?? throw new InvalidOperationException("لم يتم العثور على عملية البيع الأصلية.");

            int alreadyReturned = uow.Returns.GetReturnedQuantity(command.SaleId);
            int remaining = sale.QuantitySold - alreadyReturned;
            if (command.Quantity > remaining)
                throw new InvalidOperationException($"الكمية المطلوب إرجاعها أكبر من المتاح للإرجاع! المتاح حاليًا هو: {remaining}");

            // حصة الكمية المرتجعة من إجمالي/خصم/ضريبة الفاتورة الأصلية بالظبط - نفس مبدأ
            // UpdateSaleQuantityCommand، لكن هنا القيمة الأصلية (sale.Total/Discount/Tax)
            // بتفضل زي ما هي دايمًا (المرجع دايمًا الفاتورة الأصلية، مش آخر مرتجع)
            decimal fraction = sale.QuantitySold > 0 ? (decimal)command.Quantity / sale.QuantitySold : 0;
            decimal returnGross = Math.Round(sale.Total * fraction, 2);
            decimal returnDiscount = Math.Round(sale.Discount * fraction, 2);
            decimal returnTax = Math.Round(sale.Tax * fraction, 2);
            decimal refundAmount = returnGross - returnDiscount + returnTax;
            decimal returnCost = sale.CostPrice * command.Quantity;

            uow.Products.AddQuantity(sale.Barcode, command.Quantity);
            if (sale.Imei != null)
                _inventory.MarkImeiInStock(sale.Imei, uow);

            var lines = new List<JournalLineRequest>();
            if (returnGross != 0)
            {
                lines.Add(new() { AccountCode = AccountCodes.SalesRevenue, Debit = returnGross, Credit = 0 });
                if (returnDiscount > 0)
                    lines.Add(new() { AccountCode = AccountCodes.SalesDiscounts, Debit = 0, Credit = returnDiscount });
                if (returnTax > 0)
                    lines.Add(new() { AccountCode = AccountCodes.TaxPayable, Debit = returnTax, Credit = 0 });
                if (sale.PaymentType == "Cash" && sale.PaymentMethod != null)
                    lines.Add(new() { AccountCode = AccountCodes.ForPaymentMethod(sale.PaymentMethod), Debit = 0, Credit = refundAmount });
                else
                    lines.Add(new() { AccountCode = AccountCodes.Customers, Debit = 0, Credit = refundAmount });
            }

            if (sale.PaymentType == "Cash" && sale.PaymentMethod != null && refundAmount != 0)
                _cashDrawer.Debit(sale.PaymentMethod, refundAmount, $"مرتجع من عملية بيع رقم {command.SaleId}", uow, saleId: command.SaleId, accountCode: AccountCodes.SalesRevenue);

            if (returnCost > 0)
            {
                lines.Add(new JournalLineRequest { AccountCode = AccountCodes.Inventory, Debit = returnCost, Credit = 0 });
                lines.Add(new JournalLineRequest { AccountCode = AccountCodes.Cogs, Debit = 0, Credit = returnCost });
            }

            if (lines.Count > 0)
                _accounting.Post(new JournalEntryRequest { SourceType = "Return", SourceId = command.SaleId, Description = $"مرتجع {command.Quantity} من {sale.ProductName} (بيع رقم {command.SaleId}) - {command.Reason}", Lines = lines }, command.PerformedBy, uow);

            int returnId = uow.Returns.Insert(new ReturnRecord
            {
                SaleId = command.SaleId,
                Barcode = sale.Barcode,
                ProductName = sale.ProductName,
                Quantity = command.Quantity,
                RefundAmount = refundAmount,
                Reason = command.Reason,
                PaymentType = sale.PaymentType,
                PaymentMethod = sale.PaymentMethod,
                CustomerId = sale.CustomerId,
                PerformedBy = command.PerformedBy
            });

            var affectedMethods = sale.PaymentType == "Cash" && sale.PaymentMethod != null ? new[] { sale.PaymentMethod } : null;
            var integrity = _integrity.CheckBeforeCommit(uow, new[] { sale.Barcode }, affectedMethods);
            if (!integrity.Passed)
                throw new InvalidOperationException(string.Join("؛ ", integrity.Violations));

            uow.Commit();

            _eventBus.Publish(new ReturnCompleted
            {
                OriginalSaleId = command.SaleId,
                Quantity = command.Quantity,
                RefundAmount = refundAmount,
                Reason = command.Reason,
                PerformedBy = command.PerformedBy
            });

            return new ReturnResult { ReturnId = returnId, RefundAmount = refundAmount };
        }
    }
}
