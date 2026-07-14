namespace TemoStore.Core.Abstractions
{
    // كل Command له Handler واحد بيعرف يعالجه - CoreEngine بيلاقيه عن طريق DI بالـ
    // Type بتاع الـ Command وقت التشغيل، فمش محتاج يعرف حاجة عن أي Command بعينه.
    public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
    {
        TResult Handle(TCommand command);
    }
}
