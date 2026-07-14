using TemoStore.Core.Abstractions;

namespace TemoStore.Engines.Orchestration
{
    // نقطة الدخول الوحيدة لكل عملية. مش بيعرف حاجة عن أي عملية بعينها (بيع/شراء/...) -
    // بيلاقي الـ Handler المناسب للـ Command عن طريق DI وقت التشغيل، وبيسيبه هو
    // المسؤول عن التنسيق بين المحركات. ده اللي بيخلي إضافة عملية جديدة مستقبلًا
    // (Command + Handler جديدين) من غير ما نلمس CoreEngine نفسه خالص.
    public class CoreEngine : ICoreEngine
    {
        private readonly IServiceProvider _serviceProvider;

        public CoreEngine(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public TResult Execute<TResult>(ICommand<TResult> command)
        {
            Type handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult));
            object? handler = _serviceProvider.GetService(handlerType);
            if (handler == null)
                throw new InvalidOperationException($"مفيش Handler مسجّل للعملية دي: {command.GetType().Name}");

            var handleMethod = handlerType.GetMethod("Handle")!;
            try
            {
                return (TResult)handleMethod.Invoke(handler, new object[] { command })!;
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
            {
                // بنرمي الاستثناء الأصلي (InsufficientBalanceException مثلاً) مش الغلاف بتاع Invoke،
                // عشان الشاشة تقدر تمسكه بالـ catch الصحيح زي ما كانت بتعمل قبل كده
                throw ex.InnerException;
            }
        }
    }
}
