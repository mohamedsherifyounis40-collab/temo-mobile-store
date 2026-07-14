using TemoStore.Core.Engines;
using TemoStore.Core.Entities;

namespace TemoStore.Engines.Pricing
{
    // مسؤول عن حساب السعر النهائي - حاليًا بيرجع سعر البيع المسجّل على المنتج زي ما
    // هو (مفيش نظام خصومات/عروض/فئات عملاء (جملة/قطاعي/VIP) في المتجر لسه)، لكن أي
    // شاشة لازم تنادي المحرك ده بدل ما تحسب السعر بنفسها - لو نظام الخصومات اتضاف
    // مستقبلًا، التغيير هيبقى هنا بس.
    public class PricingEngine : IPricingEngine
    {
        public PriceQuote CalculatePrice(ProductRecord product, int quantity, CustomerTier tier, PromotionContext? promotion)
        {
            return new PriceQuote
            {
                UnitPrice = product.SalePrice,
                DiscountApplied = 0,
                PromotionName = null
            };
        }
    }
}
