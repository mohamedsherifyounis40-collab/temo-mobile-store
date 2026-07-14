using TemoStore.Core.Engines;

namespace TemoStore.Engines.Profit
{
    // بيحسب الربح مباشرة بعد تنفيذ عملية البيع (التكلفة كانت مسجّلة أصلًا على السطر
    // نفسه في Sales.CostPrice - المحرك ده بيوفر نقطة واحدة لحساب الربح بدل ما كل
    // شاشة/تقرير يحسبه بنفسه بمعادلة مختلفة)
    public class ProfitEngine : IProfitEngine
    {
        public decimal CalculateProfit(decimal total, decimal cost) => total - cost;
    }
}
