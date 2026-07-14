namespace TemoStore.Core.Abstractions
{
    // نقطة الدخول الوحيدة لأي عملية جوه النظام. ممنوع أي شاشة تتعامل مع قاعدة
    // البيانات مباشرة - كل حاجة لازم تعدي من هنا كـ Command.
    public interface ICoreEngine
    {
        TResult Execute<TResult>(ICommand<TResult> command);
    }

    public interface ICommand<TResult>
    {
    }
}
