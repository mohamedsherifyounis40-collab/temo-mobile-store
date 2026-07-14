using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;
using TemoStore.Data.Repositories;

namespace TemoStore.Data
{
    // Unit of Work حقيقي - بيفتح Connection واحد وTransaction واحد، وكل الـ Repositories
    // تحت بتشتغل عليه هو بالظبط. Commit()/Rollback() بيأثروا على كل حاجة اتعملت من
    // خلال أي Repository في نفس الـ Scope ده مرة واحدة - مفيش عملية نصف منفذة أبدًا.
    public class SqliteUnitOfWork : IUnitOfWork
    {
        private readonly SqliteConnection _conn;
        private readonly SqliteTransaction _tx;
        private bool _completed;

        public SqliteUnitOfWork()
        {
            _conn = new SqliteConnection(DbConnectionFactory.ConnectionString);
            _conn.Open();
            _tx = _conn.BeginTransaction();

            Products = new ProductRepository(_conn, _tx);
            Sales = new SaleRepository(_conn, _tx);
            Purchases = new PurchaseRepository(_conn, _tx);
            CashDrawer = new CashDrawerRepository(_conn, _tx);
            Customers = new CustomerRepository(_conn, _tx);
            Suppliers = new SupplierRepository(_conn, _tx);
            Journal = new JournalRepository(_conn, _tx);
            Audit = new AuditRepository();
            Expenses = new ExpenseRepository(_conn, _tx);
            InventoryAdjustments = new InventoryAdjustmentRepository(_conn, _tx);
        }

        public IProductRepository Products { get; }
        public ISaleRepository Sales { get; }
        public IPurchaseRepository Purchases { get; }
        public ICashDrawerRepository CashDrawer { get; }
        public ICustomerRepository Customers { get; }
        public ISupplierRepository Suppliers { get; }
        public IJournalRepository Journal { get; }
        public IAuditRepository Audit { get; }
        public IExpenseRepository Expenses { get; }
        public IInventoryAdjustmentRepository InventoryAdjustments { get; }

        public void Commit()
        {
            _tx.Commit();
            _completed = true;
        }

        public void Rollback()
        {
            _tx.Rollback();
            _completed = true;
        }

        public void Dispose()
        {
            if (!_completed)
            {
                try { _tx.Rollback(); } catch { /* الاتصال ممكن يكون اتقفل بالفعل */ }
            }
            _tx.Dispose();
            _conn.Dispose();
        }
    }

    public class SqliteUnitOfWorkFactory : IUnitOfWorkFactory
    {
        public IUnitOfWork Create() => new SqliteUnitOfWork();
    }
}
