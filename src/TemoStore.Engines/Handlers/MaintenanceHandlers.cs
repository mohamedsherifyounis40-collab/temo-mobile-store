using TemoStore.Core.Abstractions;
using TemoStore.Core.Commands;
using TemoStore.Core.Engines;
using TemoStore.Core.Entities;

namespace TemoStore.Engines.Handlers
{
    public class ReceiveDeviceCommandHandler : ICommandHandler<ReceiveDeviceCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;

        public ReceiveDeviceCommandHandler(IUnitOfWorkFactory uowFactory)
        {
            _uowFactory = uowFactory;
        }

        public bool Handle(ReceiveDeviceCommand command)
        {
            using var uow = _uowFactory.Create();
            uow.Maintenance.Insert(command.CustomerName, command.CustomerPhone, command.DeviceInfo, command.IssueDescription, command.EstimatedCost);
            uow.Commit();
            return true;
        }
    }

    public class UpdateMaintenanceStatusCommandHandler : ICommandHandler<UpdateMaintenanceStatusCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;

        public UpdateMaintenanceStatusCommandHandler(IUnitOfWorkFactory uowFactory)
        {
            _uowFactory = uowFactory;
        }

        public bool Handle(UpdateMaintenanceStatusCommand command)
        {
            using var uow = _uowFactory.Create();
            uow.Maintenance.UpdateStatus(command.TicketId, command.NewStatus);
            uow.Commit();
            return true;
        }
    }

    // بيسلّم الجهاز، وبيحصّل الأجرة (لو أكبر من صفر) عن طريق ICashDrawerEngine بدل
    // الوصول المباشر لرصيد الخزنة زي قبل كده - ده كمان بيقفل ثغرة محاسبية حقيقية:
    // النسخة القديمة كانت بتزوّد رصيد وسيلة الدفع من غير ما تسجّل أي قيد محاسبي
    // (نفس شكل ثغرة "مصروفات ما بتأثرش في الخزينة" و"بيع كاش ما بيقيدش في الخزينة"
    // اللي اتصلحوا قبل كده) - دلوقتي بيتسجل قيد متزن (مدين وسيلة الدفع / دائن إيراد صيانة)
    public class DeliverMaintenanceDeviceCommandHandler : ICommandHandler<DeliverMaintenanceDeviceCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly ICashDrawerEngine _cashDrawer;
        private readonly IAccountingEngine _accounting;

        public DeliverMaintenanceDeviceCommandHandler(IUnitOfWorkFactory uowFactory, ICashDrawerEngine cashDrawer, IAccountingEngine accounting)
        {
            _uowFactory = uowFactory;
            _cashDrawer = cashDrawer;
            _accounting = accounting;
        }

        public bool Handle(DeliverMaintenanceDeviceCommand command)
        {
            using var uow = _uowFactory.Create();

            string customerName = uow.Maintenance.GetCustomerName(command.TicketId) ?? "";
            uow.Maintenance.SetDelivered(command.TicketId, command.ActualCost);

            if (command.ActualCost > 0)
            {
                _cashDrawer.Credit(command.Method, command.ActualCost, $"تحصيل أجرة صيانة - تذكرة رقم {command.TicketId} - {customerName}", uow, accountCode: AccountCodes.MaintenanceRevenue);

                var lines = new List<JournalLineRequest>
                {
                    new() { AccountCode = AccountCodes.ForPaymentMethod(command.Method), Debit = command.ActualCost, Credit = 0 },
                    new() { AccountCode = AccountCodes.MaintenanceRevenue, Debit = 0, Credit = command.ActualCost }
                };
                _accounting.Post(new JournalEntryRequest { SourceType = "MaintenanceDelivery", SourceId = command.TicketId, Description = $"تحصيل أجرة صيانة - تذكرة رقم {command.TicketId}", Lines = lines }, command.PerformedBy, uow);
            }

            uow.Commit();
            return true;
        }
    }
}
