using TemoStore.Core.Events;

namespace TemoStore.Core.Abstractions
{
    // Event Bus في نفس العملية (in-process) - متزامن ومباشر. لو احتجنا مستقبلًا Multi-branch/Cloud،
    // التنفيذ ده بس اللي هيتغيّر (لـ RabbitMQ/Service Bus مثلًا)، من غير ما أي Engine يعرف الفرق،
    // لأن كل المحركات بتتعامل مع الواجهة دي بس مش مع التنفيذ.
    public interface IEventBus
    {
        void Publish<TEvent>(TEvent @event) where TEvent : IDomainEvent;
        void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IDomainEvent;
    }

    public interface IEventHandler<in TEvent> where TEvent : IDomainEvent
    {
        void Handle(TEvent @event);
    }
}
