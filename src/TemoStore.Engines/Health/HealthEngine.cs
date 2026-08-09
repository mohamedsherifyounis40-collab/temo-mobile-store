using TemoStore.Core.Abstractions;
using TemoStore.Core.Engines;
using TemoStore.Engines.Handlers;

namespace TemoStore.Engines.Health
{
    // فحص شامل للنظام عند الطلب - مش جزء من مسار أي عملية بيع/شراء، بيتنادى من شاشة
    // منفصلة أو مجدول. القيود المحاسبية نفسها مش محتاجة فحص هنا لأنها ممنوعة أصلًا
    // من إنها تتحفظ وهي مش متزنة (AccountingEngine.Post بيرفضها وقت الكتابة). لكن
    // ده مش ضمان إن الكاش السريع (PaymentMethodBalances) نفسه فضل متزامن مع الدفتر
    // الحقيقي بمرور الوقت - ده بالظبط اللي فحص "مطابقة الخزينة" تحت بيتأكد منه.
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

            var mismatches = new List<string>();
            foreach (var kv in balances)
            {
                int accountCode = AccountCodes.ForPaymentMethod(kv.Key);
                decimal ledgerBalance = uow.Journal.GetAccountBalance(accountCode);
                // فرق أقل من قرش (تقريب فاصلة عائمة) مش حقيقي - بنتجاهله
                if (Math.Abs(kv.Value - ledgerBalance) > 0.01m)
                    mismatches.Add($"{kv.Key}: الكاش السريع {kv.Value:N2} مقابل الدفتر {ledgerBalance:N2}");
            }
            report.Items.Add(new Core.Entities.HealthCheckItem
            {
                Area = "مطابقة الخزينة مع الدفتر",
                Healthy = mismatches.Count == 0,
                Message = mismatches.Count == 0 ? "كل أرصدة وسائل الدفع متطابقة تمامًا مع الدفتر المحاسبي." : "فروقات في: " + string.Join("، ", mismatches)
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
