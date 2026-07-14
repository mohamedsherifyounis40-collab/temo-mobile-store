using TemoStore.Core.Abstractions;
using TemoStore.Core.Engines;
using TemoStore.Core.Entities;
using TemoStore.Core.Exceptions;

namespace TemoStore.Engines.CashDrawer
{
    // مسؤول عن الخزنة بس - كل حركة نقدية (زيادة/نقصان) لازم تعدي من هنا، وبيمنع
    // الرصيد السالب دايمًا (بيرمي InsufficientBalanceException بدل ما يسيب الرصيد يتعدى).
    public class CashDrawerEngine : ICashDrawerEngine
    {
        public decimal GetBalance(string paymentMethod, IUnitOfWork uow) => uow.CashDrawer.GetBalance(paymentMethod);

        public int Credit(string paymentMethod, decimal amount, string reason, IUnitOfWork uow, int? saleId = null, int? purchaseId = null, int? customerId = null, int? supplierId = null, int? accountCode = null)
        {
            decimal currentBalance = uow.CashDrawer.GetBalance(paymentMethod);
            int movementId = uow.CashDrawer.InsertMovement(new CashMovementRecord
            {
                MovementType = "قبض",
                PaymentMethod = paymentMethod,
                Amount = amount,
                Description = reason,
                AccountCode = accountCode,
                SaleId = saleId,
                PurchaseId = purchaseId,
                CustomerId = customerId,
                SupplierId = supplierId
            });
            uow.CashDrawer.SetBalance(paymentMethod, currentBalance + amount);
            return movementId;
        }

        public int Debit(string paymentMethod, decimal amount, string reason, IUnitOfWork uow, int? saleId = null, int? purchaseId = null, int? customerId = null, int? supplierId = null, int? accountCode = null)
        {
            decimal currentBalance = uow.CashDrawer.GetBalance(paymentMethod);
            if (amount > currentBalance)
                throw new InsufficientBalanceException(paymentMethod, currentBalance);

            int movementId = uow.CashDrawer.InsertMovement(new CashMovementRecord
            {
                MovementType = "صرف",
                PaymentMethod = paymentMethod,
                Amount = amount,
                Description = reason,
                AccountCode = accountCode,
                SaleId = saleId,
                PurchaseId = purchaseId,
                CustomerId = customerId,
                SupplierId = supplierId
            });
            uow.CashDrawer.SetBalance(paymentMethod, currentBalance - amount);
            return movementId;
        }

        public (int fromMovementId, int toMovementId) Transfer(string fromMethod, string toMethod, decimal amount, string? description, IUnitOfWork uow)
        {
            if (fromMethod == toMethod)
                throw new InvalidOperationException("لازم تختار وسيلتين مختلفتين للتحويل.");

            string desc = string.IsNullOrWhiteSpace(description) ? "" : " - " + description!.Trim();

            int fromId = Debit(fromMethod, amount, $"تحويل إلى \"{toMethod}\"{desc}", uow);
            int toId = Credit(toMethod, amount, $"تحويل من \"{fromMethod}\"{desc}", uow);

            uow.CashDrawer.LinkMovements(fromId, toId);
            return (fromId, toId);
        }
    }
}
