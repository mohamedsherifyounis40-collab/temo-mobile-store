using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // WebCatalogSyncService: بيبعت كل شوية (5 دقايق) قائمة المنتجات كاملة لموقع
    // الكتالوج البارة (CatalogWebsite) عن طريق POST لـ /api/sync، لو المزامنة
    // مفعّلة في الإعدادات. فشل المزامنة (النت واقع، الموقع نايم، ...) بيتجاهل
    // بصمت وبيتعاد المحاولة في الدورة الجاية - مفيش داعي يوقف شغل البرنامج.
    // ==========================================================================
    public static class WebCatalogSyncService
    {
        private static System.Threading.Timer timer;
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(5);

        public static void Start()
        {
            if (timer != null) return;
            timer = new System.Threading.Timer(async _ => await SyncOnceSafe(), null, TimeSpan.FromSeconds(10), SyncInterval);
        }

        public static void Stop()
        {
            timer?.Dispose();
            timer = null;
        }

        // بينفّذها زرار "مزامنة الآن" في الإعدادات - بيرجّع رسالة نتيجة واضحة للمستخدم
        public static async Task<(bool Success, string Message)> SyncNowAsync()
        {
            var (url, secret, enabled) = SettingsRepository.GetCatalogSyncSettings();
            if (string.IsNullOrWhiteSpace(url))
                return (false, "من فضلك حدد رابط موقع الكتالوج أولًا.");

            try
            {
                var products = InventoryRepository.GetProductsForCatalogSync();
                string json = JsonSerializer.Serialize(products);

                var request = new HttpRequestMessage(HttpMethod.Post, url.TrimEnd('/') + "/api/sync")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("X-Sync-Secret", secret ?? "");

                using (var response = await httpClient.SendAsync(request))
                {
                    if (response.IsSuccessStatusCode)
                        return (true, $"تمت المزامنة بنجاح ({products.Count} منتج).");

                    if ((int)response.StatusCode == 401)
                        return (false, "رفض الموقع الطلب - المفتاح السري (Secret) مش مطابق.");

                    return (false, $"الموقع رد بخطأ ({(int)response.StatusCode}).");
                }
            }
            catch (Exception ex)
            {
                return (false, "تعذّر الاتصال بموقع الكتالوج: " + ex.Message);
            }
        }

        private static async Task SyncOnceSafe()
        {
            try
            {
                var (url, secret, enabled) = SettingsRepository.GetCatalogSyncSettings();
                if (!enabled || string.IsNullOrWhiteSpace(url)) return;

                var products = InventoryRepository.GetProductsForCatalogSync();
                string json = JsonSerializer.Serialize(products);

                var request = new HttpRequestMessage(HttpMethod.Post, url.TrimEnd('/') + "/api/sync")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("X-Sync-Secret", secret ?? "");

                using (var response = await httpClient.SendAsync(request)) { /* نتيجة الدورة التلقائية مش لازم تتعرض للمستخدم */ }
            }
            catch
            {
                // هيتعاد المحاولة في الدورة الجاية بعد 5 دقايق - مفيش داعي نوقف أو نبلّغ المستخدم كل مرة
            }
        }
    }
}
