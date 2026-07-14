using TemoStore.Core.Abstractions;
using TemoStore.Core.Engines;

namespace TemoStore.Engines.Integrity
{
    // آخر بوابة قبل الـ Commit - بيعيد التأكد إن الأصناف/وسائل الدفع اللي اتلمست في
    // العملية دي فضلت في حالة سليمة، باستخدام نفس واجهات IUnitOfWork بس (مفيش وصول
    // مباشر لقاعدة البيانات هنا). في الحالة الطبيعية، كل محرك (Inventory/CashDrawer)
    // بيرمي استثناءه بنفسه قبل ما يوصل هنا أصلًا - البوابة دي شبكة أمان إضافية أخيرة.
    public class IntegrityEngine : IIntegrityEngine
    {
        public IntegrityCheckResult CheckBeforeCommit(IUnitOfWork uow, IReadOnlyList<string>? affectedBarcodes = null, IReadOnlyList<string>? affectedPaymentMethods = null)
        {
            var result = new IntegrityCheckResult();

            if (affectedBarcodes != null)
            {
                foreach (var barcode in affectedBarcodes)
                {
                    var product = uow.Products.GetByBarcode(barcode);
                    if (product != null && product.Quantity < 0)
                        result.Violations.Add($"كمية المنتج \"{barcode}\" أصبحت سالبة ({product.Quantity}).");
                }
            }

            if (affectedPaymentMethods != null)
            {
                foreach (var method in affectedPaymentMethods)
                {
                    decimal balance = uow.CashDrawer.GetBalance(method);
                    if (balance < 0)
                        result.Violations.Add($"رصيد وسيلة الدفع \"{method}\" أصبح سالبًا ({balance}).");
                }
            }

            result.Passed = result.Violations.Count == 0;
            return result;
        }
    }
}
