using TemoStore.Core.Abstractions;
using TemoStore.Core.Engines;

namespace TemoStore.Engines.Health
{
    // فحص شامل للنظام عند الطلب - مش جزء من مسار أي عملية بيع/شراء، بيتنادى من شاشة
    // منفصلة أو مجدول. القيود المحاسبية نفسها مش محتاجة فحص هنا لأنها ممنوعة أصلًا
    // من إنها تتحفظ وهي مش متزنة (AccountingEngine.Post بيرفضها وقت الكتابة).
    public class HealthEngine : IHealthEngine
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IBackupEngine _backupEngine;

        public HealthEngine(IUnitOfWorkFactory uowFactory, IBackupEngine backupEngine)
        {
            _uowFactory = uowFactory;
            _backupEngine = backupEngine;
        }

        public Core.Entities.HealthReport RunFullCheck()
        {
            var report = new Core.Entities.HealthReport();
            using var uow = _uowFactory.Create();

            var negativeStock = uow.Products.GetBelowThreshold(-1);
            report.Items.Add(new Core.Entities.HealthCheckItem
            {
                Area = "المخزون",
                Healthy = negativeStock.Count == 0,
                Message = negativeStock.Count == 0 ? "لا توجد كميات سالبة." : $"{negativeStock.Count} منتج بكمية سالبة."
            });

            var balances = uow.CashDrawer.GetAllBalances();
            var negativeBalances = new List<string>();
            foreach (var kv in balances)
                if (kv.Value < 0) negativeBalances.Add($"{kv.Key} ({kv.Value:N2})");
            report.Items.Add(new Core.Entities.HealthCheckItem
            {
                Area = "الخزينة",
                Healthy = negativeBalances.Count == 0,
                Message = negativeBalances.Count == 0 ? "كل أرصدة وسائل الدفع سليمة." : "أرصدة سالبة في: " + string.Join("، ", negativeBalances)
            });

            uow.Rollback();

            bool backupHealthy = _backupEngine.VerifyLastBackup();
            report.Items.Add(new Core.Entities.HealthCheckItem
            {
                Area = "النسخ الاحتياطي",
                Healthy = backupHealthy,
                Message = backupHealthy ? "آخر نسخة احتياطية سليمة." : "لا توجد نسخة احتياطية حديثة أو فشل التحقق منها."
            });

            return report;
        }
    }
}
