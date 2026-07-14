using TemoStore.Core.Abstractions;
using TemoStore.Core.Engines;
using TemoStore.Core.Entities;

namespace TemoStore.Engines.Customer
{
    public class CustomerEngine : ICustomerEngine
    {
        private readonly ICashDrawerEngine _cashDrawer;

        public CustomerEngine(ICashDrawerEngine cashDrawer)
        {
            _cashDrawer = cashDrawer;
        }

        public void RecordCollection(int customerId, decimal amount, string method, IUnitOfWork uow)
        {
            _cashDrawer.Credit(method, amount, $"تحصيل من عميل رقم {customerId}", uow, customerId: customerId, accountCode: 1300);
        }

        public IReadOnlyList<CustomerStatementLine> GetStatement(int customerId, IUnitOfWork uow) => uow.Customers.GetStatement(customerId);
    }
}
