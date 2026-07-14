using TemoStore.Core.Abstractions;
using TemoStore.Core.Engines;
using TemoStore.Core.Entities;
using TemoStore.Core.Exceptions;

namespace TemoStore.Engines.Inventory
{
    // مسؤول عن المخزون بس - خصم/إضافة كميات، وحدات IMEI. ممنوع أي شاشة تعدّل
    // المخزون من غير المحرك ده - كل تعديل بيمر من هنا وبيشتغل جوه الـ UnitOfWork
    // اللي الـ CoreEngine فاتحه للعملية كلها.
    public class InventoryEngine : IInventoryEngine
    {
        public void DeductStock(string barcode, int quantity, IUnitOfWork uow)
        {
            var product = uow.Products.GetByBarcode(barcode)
                ?? throw new InvalidOperationException("المنتج غير موجود بالمخزون.");
            if (product.Quantity < quantity)
                throw new InsufficientStockException(product.Quantity);
            uow.Products.DeductQuantity(barcode, quantity);
        }

        public void AddStock(string barcode, string productName, decimal unitCost, decimal salePrice, int quantity, bool isSerialized, IUnitOfWork uow)
        {
            uow.Products.UpsertOnPurchase(barcode, productName, unitCost, salePrice, quantity, isSerialized);
        }

        public void MarkImeiSold(string imei, int saleId, IUnitOfWork uow)
        {
            uow.Products.MarkImeiSold(imei, saleId);
        }

        public void MarkImeiInStock(string imei, IUnitOfWork uow)
        {
            uow.Products.MarkImeiInStock(imei);
        }

        // بيرجّع أثر فاتورة شراء ملغاة على المخزون - بيتأكد إن مفيش جهاز من الفاتورة
        // اتباع بالفعل (IMEI Status != InStock) قبل ما يرجّع أي حاجة
        public void ReverseStockForCancelledPurchase(int purchaseId, IUnitOfWork uow)
        {
            var purchase = uow.Purchases.GetById(purchaseId)
                ?? throw new InvalidOperationException("لم يتم العثور على فاتورة الشراء.");

            foreach (var line in purchase.Lines)
            {
                if (line.IsSerialized && line.Imeis != null)
                {
                    foreach (var imei in line.Imeis)
                    {
                        string status = uow.Products.GetImeiStatus(imei);
                        if (!string.Equals(status, "InStock", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException($"الجهاز صاحب رقم IMEI \"{imei}\" من الفاتورة دي اتباع بالفعل، لا يمكن إلغاء أو تعديل الفاتورة.");
                    }
                }

                if (string.IsNullOrEmpty(line.Barcode)) continue;
                var product = uow.Products.GetByBarcode(line.Barcode);
                if (product != null && product.Quantity < line.Qty)
                    throw new InvalidOperationException("الكمية المتاحة بالمخزون لبعض أصناف الفاتورة دي أقل من كميتها الأصلية، يبدو إن جزء من البضاعة اتباع بالفعل. لا يمكن إلغاء أو تعديل الفاتورة.");
            }

            foreach (var line in purchase.Lines)
            {
                if (string.IsNullOrEmpty(line.Barcode)) continue;
                uow.Products.DeductQuantity(line.Barcode, line.Qty);
            }

            uow.Products.DeleteProductUnitsForPurchase(purchaseId);
        }

        public IReadOnlyList<LowStockAlert> CheckMinimumThresholds(int threshold, IUnitOfWork uow)
        {
            var products = uow.Products.GetBelowThreshold(threshold);
            var alerts = new List<LowStockAlert>();
            foreach (var p in products)
                alerts.Add(new LowStockAlert { Barcode = p.Barcode, ProductName = p.ProductName, CurrentQuantity = p.Quantity, Threshold = threshold });
            return alerts;
        }

        public void AddAccessory(string barcode, string productName, decimal costPrice, decimal salePrice, int quantity, IUnitOfWork uow)
        {
            uow.Products.InsertAccessory(barcode, productName, costPrice, salePrice, quantity);
        }

        public void UpdateAccessory(string barcode, string productName, decimal costPrice, decimal salePrice, int quantity, IUnitOfWork uow)
        {
            uow.Products.UpdateAccessory(barcode, productName, costPrice, salePrice, quantity);
        }

        public void DeleteProduct(string barcode, IUnitOfWork uow)
        {
            uow.Products.DeleteByBarcode(barcode);
        }

        public void UpdateModelPrice(string barcode, string productName, decimal costPrice, decimal salePrice, IUnitOfWork uow)
        {
            uow.Products.UpdateModelPrice(barcode, productName, costPrice, salePrice);
        }

        // بيضيف جهاز جديد بالـ IMEI يدويًا (مش من فاتورة شراء) - لو الباركود موجود بيزوّد
        // كميته، لو مش موجود بينشئ منتج جديد. بيرمي DuplicateImeiException لو الرقم مسجل قبل كده.
        // بيستخدم UpsertOnPurchase نفسه (زي مسار الشراء) لأن سلوكه مطابق تمامًا للمنطق
        // القديم هنا: لو الباركود موجود بيزوّد الكمية+1 ويحدّث السعر ويفرض IsSerialized=1
        // من غير ما يلمس الاسم/سعر البيع القديمين، ولو مش موجود بينشئ صنف جديد بالكامل.
        public void AddDeviceManually(string barcode, string productName, decimal costPrice, decimal salePrice, string imei, IUnitOfWork uow)
        {
            if (uow.Products.ImeiExists(imei))
                throw new DuplicateImeiException(imei);

            string finalBarcode = string.IsNullOrEmpty(barcode) ? ("NEW-" + DateTime.Now.Ticks) : barcode;
            uow.Products.UpsertOnPurchase(finalBarcode, productName, costPrice, salePrice, 1, true);
            uow.Products.InsertProductUnit(finalBarcode, imei, null);
        }

        public int ApplyInventoryCount(IReadOnlyList<InventoryAdjustmentLine> rows, IUnitOfWork uow)
        {
            foreach (var row in rows)
            {
                uow.Products.SetQuantity(row.Barcode, row.CountedQty);
                uow.InventoryAdjustments.InsertAdjustmentLog(row.Barcode, row.ProductName, row.SystemQty, row.CountedQty, row.Difference);
            }
            return rows.Count;
        }
    }
}
