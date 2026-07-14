using TemoStore.Core.Abstractions;
using TemoStore.Core.Events;

namespace TemoStore.EventBus
{
    // Event Bus بسيط داخل نفس العملية - متزامن (بينفذ كل الـ Handlers على نفس الـ Thread
    // وقت الـ Publish). مقصود إنه بسيط: مفيش تخزين، مفيش استمرارية بعد الكراش، لأن
    // مفيش حاجة حرجة ماليًا بتعتمد عليه - هو بس لتبعات ما بعد الـ Commit (راجع قسم 4/5
    // بمستند العمارة). لو احتجنا مستقبلًا نظام موزّع (فروع متعددة/Cloud)، التنفيذ ده
    // بس اللي هيتغيّر، والواجهة IEventBus هتفضل زي ما هي.
    public class InProcessEventBus : IEventBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();
        private readonly object _lock = new();

        public void Publish<TEvent>(TEvent @event) where TEvent : IDomainEvent
        {
            List<object>? handlers;
            lock (_lock)
            {
                if (!_handlers.TryGetValue(typeof(TEvent), out handlers))
                    return;
                handlers = new List<object>(handlers); // نسخة عشان مايتأثرش لو Handler عمل Subscribe جديد وهو شغال
            }

            foreach (var handler in handlers)
            {
                // كل Handler بيشتغل لوحده - فشل واحد ميمنعش الباقيين من الاستماع لنفس الحدث
                try
                {
                    ((IEventHandler<TEvent>)handler).Handle(@event);
                }
                catch
                {
                    // تعمّد عدم الرمي تاني: الحدث ده بعد الـ Commit بالفعل، فشل مستمع
                    // (Audit/Notification) مايرجعش عملية مالية ناجحة بالفعل.
                }
            }
        }

        public void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IDomainEvent
        {
            lock (_lock)
            {
                if (!_handlers.TryGetValue(typeof(TEvent), out var list))
                {
                    list = new List<object>();
                    _handlers[typeof(TEvent)] = list;
                }
                list.Add(handler);
            }
        }
    }
}
