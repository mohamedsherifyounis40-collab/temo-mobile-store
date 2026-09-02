using Microsoft.Extensions.DependencyInjection;
using TemoStore.Core.Abstractions;
using TemoStore.Core.Commands;
using TemoStore.Core.Engines;
using TemoStore.Data;
using TemoStore.Data.Repositories;
using TemoStore.Engines.Accounting;
using TemoStore.Engines.Audit;
using TemoStore.Engines.Backup;
using TemoStore.Engines.CashDrawer;
using TemoStore.Engines.Customer;
using TemoStore.Engines.Handlers;
using TemoStore.Engines.Health;
using TemoStore.Engines.Integrity;
using TemoStore.Engines.Inventory;
using TemoStore.Engines.Notification;
using TemoStore.Engines.Orchestration;
using TemoStore.Engines.Pricing;
using TemoStore.Engines.Profit;
using TemoStore.Engines.Recovery;
using TemoStore.Engines.Supplier;
using TemoStore.Engines.Validation;
using TemoStore.EventBus;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // Composition Root - المكان الوحيد في المشروع اللي بيعرف كل المحركات وتنفيذها
    // الفعلي مع بعض. الشاشات (PageControls) بتوصل لـ ICoreEngine بس من هنا
    // (AppServices.CoreEngine) - ده الاستثناء الوحيد من مبدأ "الشاشة متعرفش تفاصيل
    // التنفيذ": شاشات الـ WinForms الحالية بتتبني بـ new() مباشرة (مش عن طريق DI
    // Container زي ASP.NET)، فمحتاجة نقطة وصول ثابتة (Service Locator) بدل إعادة
    // كتابة كل الـ Constructors. أي كود جديد يقدر يستبدل الجزء ده لاحقًا لو الشاشات
    // اتحولت لـ Constructor Injection كامل، من غير ما يلمس أي محرك.
    // ==========================================================================
    public static class AppServices
    {
        private static IServiceProvider? _provider;

        public static ICoreEngine CoreEngine => _provider!.GetRequiredService<ICoreEngine>();
        public static IHealthEngine HealthEngine => _provider!.GetRequiredService<IHealthEngine>();
        public static IBackupEngine BackupEngine => _provider!.GetRequiredService<IBackupEngine>();
        public static INotificationEngine NotificationEngine => _provider!.GetRequiredService<INotificationEngine>();

        public static void Initialize()
        {
            var services = new ServiceCollection();

            // ---------- طبقة البيانات ----------
            services.AddSingleton<IUnitOfWorkFactory, SqliteUnitOfWorkFactory>();
            services.AddSingleton<IDateClosureRepository, DateClosureRepository>();
            services.AddSingleton<IAuditRepository, AuditRepository>();
            services.AddSingleton<IBackupRepository, BackupRepository>();

            // Event Bus - Singleton عشان الاشتراكات تفضل موجودة طول عمر البرنامج
            services.AddSingleton<IEventBus, InProcessEventBus>();

            // ---------- المحركات ----------
            services.AddSingleton<IValidationEngine, ValidationEngine>();
            services.AddSingleton<IInventoryEngine, InventoryEngine>();
            services.AddSingleton<ICashDrawerEngine, CashDrawerEngine>();
            services.AddSingleton<ICustomerEngine, CustomerEngine>();
            services.AddSingleton<ISupplierEngine, SupplierEngine>();
            services.AddSingleton<IPricingEngine, PricingEngine>();
            services.AddSingleton<IProfitEngine, ProfitEngine>();
            services.AddSingleton<IAccountingEngine, AccountingEngine>();
            services.AddSingleton<IAuditEngine, AuditEngine>();
            services.AddSingleton<IIntegrityEngine, IntegrityEngine>();
            services.AddSingleton<IHealthEngine, HealthEngine>();
            services.AddSingleton<INotificationEngine, NotificationEngine>();
            services.AddSingleton<IBackupEngine, BackupEngine>();
            services.AddSingleton<IRecoveryEngine, RecoveryEngine>();

            // ---------- Command Handlers - واحد لكل عملية (بيع/شراء/مرتجع/...) ----------
            services.AddSingleton<ICommandHandler<CreateSaleCommand, SaleResult>, CreateSaleCommandHandler>();
            services.AddSingleton<ICommandHandler<UpdateSaleQuantityCommand, SaleResult>, UpdateSaleQuantityCommandHandler>();
            services.AddSingleton<ICommandHandler<CancelSaleCommand, bool>, CancelSaleCommandHandler>();
            services.AddSingleton<ICommandHandler<CreateReturnCommand, ReturnResult>, CreateReturnCommandHandler>();
            services.AddSingleton<ICommandHandler<CreatePurchaseCommand, PurchaseResult>, CreatePurchaseCommandHandler>();
            services.AddSingleton<ICommandHandler<EditPurchaseCommand, PurchaseResult>, EditPurchaseCommandHandler>();
            services.AddSingleton<ICommandHandler<CancelPurchaseCommand, bool>, CancelPurchaseCommandHandler>();
            services.AddSingleton<ICommandHandler<PaySupplierCommand, PaymentResult>, PaySupplierCommandHandler>();
            services.AddSingleton<ICommandHandler<CollectFromCustomerCommand, PaymentResult>, CollectFromCustomerCommandHandler>();
            services.AddSingleton<ICommandHandler<RecordExpenseCommand, ExpenseResult>, RecordExpenseCommandHandler>();
            services.AddSingleton<ICommandHandler<UpdateExpenseCommand, bool>, UpdateExpenseCommandHandler>();
            services.AddSingleton<ICommandHandler<DeleteExpenseCommand, bool>, DeleteExpenseCommandHandler>();
            services.AddSingleton<ICommandHandler<TransferFundsCommand, TransferResult>, TransferFundsCommandHandler>();
            services.AddSingleton<ICommandHandler<CancelTransferCommand, bool>, CancelTransferCommandHandler>();
            services.AddSingleton<ICommandHandler<AddMovementCommand, PaymentResult>, AddMovementCommandHandler>();
            services.AddSingleton<ICommandHandler<UpdateMovementCommand, bool>, UpdateMovementCommandHandler>();
            services.AddSingleton<ICommandHandler<CancelMovementCommand, bool>, CancelMovementCommandHandler>();
            services.AddSingleton<ICommandHandler<AddAccessoryProductCommand, bool>, AddAccessoryProductCommandHandler>();
            services.AddSingleton<ICommandHandler<UpdateAccessoryProductCommand, bool>, UpdateAccessoryProductCommandHandler>();
            services.AddSingleton<ICommandHandler<DeleteProductCommand, bool>, DeleteProductCommandHandler>();
            services.AddSingleton<ICommandHandler<UpdateModelPriceCommand, bool>, UpdateModelPriceCommandHandler>();
            services.AddSingleton<ICommandHandler<AddDeviceCommand, bool>, AddDeviceCommandHandler>();
            services.AddSingleton<ICommandHandler<DeleteDeviceCommand, bool>, DeleteDeviceCommandHandler>();
            services.AddSingleton<ICommandHandler<SaveInventoryAdjustmentsCommand, int>, SaveInventoryAdjustmentsCommandHandler>();
            services.AddSingleton<ICommandHandler<ReceiveDeviceCommand, bool>, ReceiveDeviceCommandHandler>();
            services.AddSingleton<ICommandHandler<UpdateMaintenanceStatusCommand, bool>, UpdateMaintenanceStatusCommandHandler>();
            services.AddSingleton<ICommandHandler<DeliverMaintenanceDeviceCommand, bool>, DeliverMaintenanceDeviceCommandHandler>();
            services.AddSingleton<ICommandHandler<SetAttendanceStatusCommand, bool>, SetAttendanceStatusCommandHandler>();
            services.AddSingleton<ICommandHandler<ClosePayrollMonthCommand, int>, ClosePayrollMonthCommandHandler>();
            services.AddSingleton<ICommandHandler<ReopenPayrollMonthCommand, int>, ReopenPayrollMonthCommandHandler>();
            services.AddSingleton<ICommandHandler<PayEmployeeCommand, bool>, PayEmployeeCommandHandler>();
            services.AddSingleton<ICommandHandler<AddEmployeeCommand, bool>, AddEmployeeCommandHandler>();
            services.AddSingleton<ICommandHandler<UpdateEmployeeCommand, bool>, UpdateEmployeeCommandHandler>();
            services.AddSingleton<ICommandHandler<DeleteEmployeeCommand, bool>, DeleteEmployeeCommandHandler>();
            services.AddSingleton<ICommandHandler<CloseDayCommand, int>, CloseDayCommandHandler>();
            services.AddSingleton<ICommandHandler<ReopenDayCommand, int>, ReopenDayCommandHandler>();

            // CoreEngine نفسه - محتاج IServiceProvider عشان يلاقي الـ Handler المناسب
            // لأي Command وقت التشغيل (راجع CoreEngine.Execute)
            services.AddSingleton<ICoreEngine>(sp => new CoreEngine(sp));

            _provider = services.BuildServiceProvider();
        }
    }
}
