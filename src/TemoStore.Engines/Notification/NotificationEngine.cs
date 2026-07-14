using TemoStore.Core.Engines;
using TemoStore.Core.Entities;

namespace TemoStore.Engines.Notification
{
    // مسؤول عن التنبيهات (نقص مخزون/ضمان قرب الانتهاء/قسط متأخر/رصيد سالب/فشل Backup) -
    // بينشر حدث .NET عادي بدل ما يكتب في قاعدة البيانات أو يعرض UI بنفسه؛ أي شاشة
    // تقدر تسمع الحدث ده وتقرر هي إزاي تعرضه (MessageBox/Toast/إلخ) - المحرك نفسه
    // مايعرفش حاجة عن الواجهة.
    public class NotificationEngine : INotificationEngine
    {
        public event Action<NotificationType, string, object?>? OnNotify;

        public void Notify(NotificationType type, string message, object? context = null)
        {
            OnNotify?.Invoke(type, message, context);
        }
    }
}
