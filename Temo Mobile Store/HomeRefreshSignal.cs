using System;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // HomeRefreshSignal: قناة بسيطة بين MainShell (WinForms) وHomePage.razor (Blazor)
    // عشان نطلب تحديث فوري للداشبورد لحظة ما المستخدم يرجع لشاشة الرئيسية، بدل
    // ما يستنى تايمر الـ 30 ثانية الداخلي بتاعها. الداشبورد نفسه مبني مرة واحدة
    // بس وبيتخبى/يتظهر (راجع MainShell.ShowPageInline)، فمفيش طريقة تانية نوصل
    // بيها من كود الشاشة (WinForms) لكومبوننت Blazor حي غير حدث Static زي ده.
    // ==========================================================================
    public static class HomeRefreshSignal
    {
        public static event Action? Requested;
        public static void Request() => Requested?.Invoke();
    }
}
