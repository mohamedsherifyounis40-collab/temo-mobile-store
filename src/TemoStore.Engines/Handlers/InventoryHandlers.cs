using TemoStore.Core.Abstractions;
using TemoStore.Core.Commands;
using TemoStore.Core.Engines;
using TemoStore.Core.Entities;

namespace TemoStore.Engines.Handlers
{
    // كل تغيير في كمية أو تكلفة صنف بيغيّر قيمة المخزون (1200) الفعلية - أي تغيير مش
    // جاي من فاتورة شراء أو بيع (إضافة/تعديل/حذف يدوي، جرد) بيتسجل مقابل حساب
    // "فروق وتسويات المخزون" (5600): زيادة قيمة = مدين مخزون/دائن فروق، نقصان = العكس.
    public class AddAccessoryProductCommandHandler : ICommandHandler<AddAccessoryProductCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IInventoryEngine _inventory;
        private readonly IAccountingEngine _accounting;

        public AddAccessoryProductCommandHandler(IUnitOfWorkFactory uowFactory, IInventoryEngine inventory, IAccountingEngine accounting)
        {
            _uowFactory = uowFactory;
            _inventory = inventory;
            _accounting = accounting;
        }

        public bool Handle(AddAccessoryProductCommand command)
        {
            using var uow = _uowFactory.Create();
            _inventory.AddAccessory(command.Barcode, command.ProductName, command.CostPrice, command.SalePrice, command.Quantity, uow);

            decimal value = command.CostPrice * command.Quantity;
            if (value != 0)
            {
                var lines = new List<JournalLineRequest>
                {
                    new() { AccountCode = AccountCodes.Inventory, Debit = value, Credit = 0 },
                    new() { AccountCode = AccountCodes.InventoryAdjustment, Debit = 0, Credit = value }
                };
                _accounting.Post(new JournalEntryRequest { SourceType = "InventoryAdd", Description = $"إضافة صنف جديد للمخزون: {command.ProductName}", Lines = lines }, command.PerformedBy, uow);
            }

            uow.Commit();
            return true;
        }
    }

    public class UpdateAccessoryProductCommandHandler : ICommandHandler<UpdateAccessoryProductCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IInventoryEngine _inventory;
        private readonly IAccountingEngine _accounting;

        public UpdateAccessoryProductCommandHandler(IUnitOfWorkFactory uowFactory, IInventoryEngine inventory, IAccountingEngine accounting)
        {
            _uowFactory = uowFactory;
            _inventory = inventory;
            _accounting = accounting;
        }

        public bool Handle(UpdateAccessoryProductCommand command)
        {
            using var uow = _uowFactory.Create();
            var existing = uow.Products.GetByBarcode(command.Barcode);
            decimal oldValue = existing != null ? existing.Quantity * existing.Price : 0;

            _inventory.UpdateAccessory(command.Barcode, command.ProductName, command.CostPrice, command.SalePrice, command.Quantity, uow);

            decimal delta = (command.CostPrice * command.Quantity) - oldValue;
            InventoryAccountingHelper.PostInventoryDelta(_accounting, uow, delta, "InventoryEdit", $"تعديل صنف: {command.ProductName}", command.PerformedBy);

            uow.Commit();
            return true;
        }
    }

    public class DeleteProductCommandHandler : ICommandHandler<DeleteProductCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IInventoryEngine _inventory;
        private readonly IAccountingEngine _accounting;

        public DeleteProductCommandHandler(IUnitOfWorkFactory uowFactory, IInventoryEngine inventory, IAccountingEngine accounting)
        {
            _uowFactory = uowFactory;
            _inventory = inventory;
            _accounting = accounting;
        }

        public bool Handle(DeleteProductCommand command)
        {
            using var uow = _uowFactory.Create();
            var existing = uow.Products.GetByBarcode(command.Barcode);
            decimal value = existing != null ? existing.Quantity * existing.Price : 0;

            _inventory.DeleteProduct(command.Barcode, uow);

            InventoryAccountingHelper.PostInventoryDelta(_accounting, uow, -value, "InventoryDelete", $"حذف صنف: {command.Barcode}", command.PerformedBy);

            uow.Commit();
            return true;
        }
    }

    public class UpdateModelPriceCommandHandler : ICommandHandler<UpdateModelPriceCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IInventoryEngine _inventory;
        private readonly IAccountingEngine _accounting;

        public UpdateModelPriceCommandHandler(IUnitOfWorkFactory uowFactory, IInventoryEngine inventory, IAccountingEngine accounting)
        {
            _uowFactory = uowFactory;
            _inventory = inventory;
            _accounting = accounting;
        }

        public bool Handle(UpdateModelPriceCommand command)
        {
            using var uow = _uowFactory.Create();
            var existing = uow.Products.GetByBarcode(command.Barcode);
            int qty = existing?.Quantity ?? 0;
            decimal oldPrice = existing?.Price ?? 0;

            _inventory.UpdateModelPrice(command.Barcode, command.ProductName, command.CostPrice, command.SalePrice, uow);

            decimal delta = (command.CostPrice - oldPrice) * qty;
            InventoryAccountingHelper.PostInventoryDelta(_accounting, uow, delta, "InventoryPriceRevaluation", $"تعديل سعر موديل: {command.ProductName}", command.PerformedBy);

            uow.Commit();
            return true;
        }
    }

    public class AddDeviceCommandHandler : ICommandHandler<AddDeviceCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IInventoryEngine _inventory;
        private readonly IAccountingEngine _accounting;

        public AddDeviceCommandHandler(IUnitOfWorkFactory uowFactory, IInventoryEngine inventory, IAccountingEngine accounting)
        {
            _uowFactory = uowFactory;
            _inventory = inventory;
            _accounting = accounting;
        }

        public bool Handle(AddDeviceCommand command)
        {
            using var uow = _uowFactory.Create();
            var existing = string.IsNullOrEmpty(command.Barcode) ? null : uow.Products.GetByBarcode(command.Barcode);
            decimal oldValue = existing != null ? existing.Quantity * existing.Price : 0;

            _inventory.AddDeviceManually(command.Barcode, command.ProductName, command.CostPrice, command.SalePrice, command.Imei, uow);

            // AddDeviceManually بيستخدم UpsertOnPurchase: لو الصنف موجود، الكمية بتزيد
            // بواحد والسعر بيتحدّث لكل الكمية (مش بس الوحدة الجديدة) - نفس السلوك هنا
            decimal newValue = existing != null ? (existing.Quantity + 1) * command.CostPrice : command.CostPrice;
            InventoryAccountingHelper.PostInventoryDelta(_accounting, uow, newValue - oldValue, "InventoryDeviceAdd", $"إضافة جهاز يدويًا: {command.ProductName}", command.PerformedBy);

            uow.Commit();
            return true;
        }
    }

    public class SaveInventoryAdjustmentsCommandHandler : ICommandHandler<SaveInventoryAdjustmentsCommand, int>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IInventoryEngine _inventory;
        private readonly IAccountingEngine _accounting;

        public SaveInventoryAdjustmentsCommandHandler(IUnitOfWorkFactory uowFactory, IInventoryEngine inventory, IAccountingEngine accounting)
        {
            _uowFactory = uowFactory;
            _inventory = inventory;
            _accounting = accounting;
        }

        public int Handle(SaveInventoryAdjustmentsCommand command)
        {
            using var uow = _uowFactory.Create();

            // القيمة بتتحسب بسعر التكلفة الحالي قبل التسوية لكل صنف، وبيتسجل صافي الفرق
            // كله في قيد واحد (مش قيد لكل صنف) عشان يفضل متسق مع طبيعة عملية الجرد كوحدة واحدة
            decimal netDelta = 0;
            foreach (var row in command.Rows)
            {
                var product = uow.Products.GetByBarcode(row.Barcode);
                decimal costPrice = product?.Price ?? 0;
                netDelta += (row.CountedQty - row.SystemQty) * costPrice;
            }

            int count = _inventory.ApplyInventoryCount(command.Rows, uow);

            InventoryAccountingHelper.PostInventoryDelta(_accounting, uow, netDelta, "InventoryCount", "تسوية جرد المخزون", command.PerformedBy);

            uow.Commit();
            return count;
        }
    }

    internal static class InventoryAccountingHelper
    {
        public static void PostInventoryDelta(IAccountingEngine accounting, IUnitOfWork uow, decimal delta, string sourceType, string description, string performedBy)
        {
            if (delta == 0) return;

            var lines = delta > 0
                ? new List<JournalLineRequest>
                {
                    new() { AccountCode = AccountCodes.Inventory, Debit = delta, Credit = 0 },
                    new() { AccountCode = AccountCodes.InventoryAdjustment, Debit = 0, Credit = delta }
                }
                : new List<JournalLineRequest>
                {
                    new() { AccountCode = AccountCodes.InventoryAdjustment, Debit = -delta, Credit = 0 },
                    new() { AccountCode = AccountCodes.Inventory, Debit = 0, Credit = -delta }
                };
            accounting.Post(new JournalEntryRequest { SourceType = sourceType, Description = description, Lines = lines }, performedBy, uow);
        }
    }
}
