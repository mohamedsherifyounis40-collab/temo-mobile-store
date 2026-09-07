using System.Text.Json;
using TemoStore.Core.Abstractions;
using TemoStore.Core.Engines;
using TemoStore.Core.Entities;

namespace TemoStore.Engines.Orchestration
{
    // نقطة الدخول الوحيدة لكل عملية. مش بيعرف حاجة عن أي عملية بعينها (بيع/شراء/...) -
    // بيلاقي الـ Handler المناسب للـ Command عن طريق DI وقت التشغيل، وبيسيبه هو
    // المسؤول عن التنسيق بين المحركات. ده اللي بيخلي إضافة عملية جديدة مستقبلًا
    // (Command + Handler جديدين) من غير ما نلمس CoreEngine نفسه خالص.
    //
    // تسجيل الـ Audit (راجع فحص 2026-09-07) بيتم هنا بالظبط - نقطة الدخول الوحيدة دي
    // هي أضمن مكان نضمن بيه تغطية كل Command حالي ومستقبلي من غير ما نلمس كل Handler
    // لوحده. بيتسجل بس بعد نجاح العملية فعليًا (زي ما التصميم الأصلي كان قاصده)،
    // وفشل تسجيل الـ Audit نفسه (لو حصل) متسيبش عملية ناجحة بالفعل تفشل - بيتجاهل بصمت.
    public class CoreEngine : ICoreEngine
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IAuditEngine _audit;

        public CoreEngine(IServiceProvider serviceProvider, IAuditEngine audit)
        {
            _serviceProvider = serviceProvider;
            _audit = audit;
        }

        public TResult Execute<TResult>(ICommand<TResult> command)
        {
            Type handlerType = typeof(ICommandHandler<,>).MakeGenericType(command.GetType(), typeof(TResult));
            object? handler = _serviceProvider.GetService(handlerType);
            if (handler == null)
                throw new InvalidOperationException($"مفيش Handler مسجّل للعملية دي: {command.GetType().Name}");

            var handleMethod = handlerType.GetMethod("Handle")!;
            TResult result;
            try
            {
                result = (TResult)handleMethod.Invoke(handler, new object[] { command })!;
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
            {
                // بنرمي الاستثناء الأصلي (InsufficientBalanceException مثلاً) مش الغلاف بتاع Invoke،
                // عشان الشاشة تقدر تمسكه بالـ catch الصحيح زي ما كانت بتعمل قبل كده
                throw ex.InnerException;
            }

            LogAudit(command);
            return result;
        }

        private void LogAudit(object command)
        {
            try
            {
                Type commandType = command.GetType();
                string performedBy = commandType.GetProperty("PerformedBy")?.GetValue(command)?.ToString() ?? "unknown";
                string newData = JsonSerializer.Serialize(command, commandType);

                _audit.Log(new AuditEntry
                {
                    Username = performedBy,
                    Screen = commandType.Name,
                    Operation = commandType.Name,
                    NewData = newData
                });
            }
            catch
            {
                // تسجيل الـ Audit مش لازم يوقف أو يفشّل عملية حقيقية نجحت بالفعل
            }
        }
    }
}
