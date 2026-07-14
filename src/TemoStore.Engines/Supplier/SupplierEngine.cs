using TemoStore.Core.Abstractions;
using TemoStore.Core.Engines;
using TemoStore.Core.Entities;

namespace TemoStore.Engines.Supplier
{
    public class SupplierEngine : ISupplierEngine
    {
        private readonly ICashDrawerEngine _cashDrawer;

        public SupplierEngine(ICashDrawerEngine cashDrawer)
        {
            _cashDrawer = cashDrawer;
        }

        public void RecordPayment(int supplierId, decimal amount, string method, IUnitOfWork uow)
        {
            _cashDrawer.Debit(method, amount, $"سداد لمورد رقم {supplierId}", uow, supplierId: supplierId, accountCode: 2100);
        }

        public IReadOnlyList<SupplierStatementLine> GetStatement(int supplierId, IUnitOfWork uow) => uow.Suppliers.GetStatement(supplierId);
    }
}
