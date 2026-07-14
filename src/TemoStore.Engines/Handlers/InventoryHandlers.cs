using TemoStore.Core.Abstractions;
using TemoStore.Core.Commands;
using TemoStore.Core.Engines;

namespace TemoStore.Engines.Handlers
{
    public class AddAccessoryProductCommandHandler : ICommandHandler<AddAccessoryProductCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IInventoryEngine _inventory;

        public AddAccessoryProductCommandHandler(IUnitOfWorkFactory uowFactory, IInventoryEngine inventory)
        {
            _uowFactory = uowFactory;
            _inventory = inventory;
        }

        public bool Handle(AddAccessoryProductCommand command)
        {
            using var uow = _uowFactory.Create();
            _inventory.AddAccessory(command.Barcode, command.ProductName, command.CostPrice, command.SalePrice, command.Quantity, uow);
            uow.Commit();
            return true;
        }
    }

    public class UpdateAccessoryProductCommandHandler : ICommandHandler<UpdateAccessoryProductCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IInventoryEngine _inventory;

        public UpdateAccessoryProductCommandHandler(IUnitOfWorkFactory uowFactory, IInventoryEngine inventory)
        {
            _uowFactory = uowFactory;
            _inventory = inventory;
        }

        public bool Handle(UpdateAccessoryProductCommand command)
        {
            using var uow = _uowFactory.Create();
            _inventory.UpdateAccessory(command.Barcode, command.ProductName, command.CostPrice, command.SalePrice, command.Quantity, uow);
            uow.Commit();
            return true;
        }
    }

    public class DeleteProductCommandHandler : ICommandHandler<DeleteProductCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IInventoryEngine _inventory;

        public DeleteProductCommandHandler(IUnitOfWorkFactory uowFactory, IInventoryEngine inventory)
        {
            _uowFactory = uowFactory;
            _inventory = inventory;
        }

        public bool Handle(DeleteProductCommand command)
        {
            using var uow = _uowFactory.Create();
            _inventory.DeleteProduct(command.Barcode, uow);
            uow.Commit();
            return true;
        }
    }

    public class UpdateModelPriceCommandHandler : ICommandHandler<UpdateModelPriceCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IInventoryEngine _inventory;

        public UpdateModelPriceCommandHandler(IUnitOfWorkFactory uowFactory, IInventoryEngine inventory)
        {
            _uowFactory = uowFactory;
            _inventory = inventory;
        }

        public bool Handle(UpdateModelPriceCommand command)
        {
            using var uow = _uowFactory.Create();
            _inventory.UpdateModelPrice(command.Barcode, command.ProductName, command.CostPrice, command.SalePrice, uow);
            uow.Commit();
            return true;
        }
    }

    public class AddDeviceCommandHandler : ICommandHandler<AddDeviceCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IInventoryEngine _inventory;

        public AddDeviceCommandHandler(IUnitOfWorkFactory uowFactory, IInventoryEngine inventory)
        {
            _uowFactory = uowFactory;
            _inventory = inventory;
        }

        public bool Handle(AddDeviceCommand command)
        {
            using var uow = _uowFactory.Create();
            _inventory.AddDeviceManually(command.Barcode, command.ProductName, command.CostPrice, command.SalePrice, command.Imei, uow);
            uow.Commit();
            return true;
        }
    }

    public class SaveInventoryAdjustmentsCommandHandler : ICommandHandler<SaveInventoryAdjustmentsCommand, int>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IInventoryEngine _inventory;

        public SaveInventoryAdjustmentsCommandHandler(IUnitOfWorkFactory uowFactory, IInventoryEngine inventory)
        {
            _uowFactory = uowFactory;
            _inventory = inventory;
        }

        public int Handle(SaveInventoryAdjustmentsCommand command)
        {
            using var uow = _uowFactory.Create();
            int count = _inventory.ApplyInventoryCount(command.Rows, uow);
            uow.Commit();
            return count;
        }
    }
}
