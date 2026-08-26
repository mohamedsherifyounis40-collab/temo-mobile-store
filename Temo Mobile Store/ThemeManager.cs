namespace Temo_Mobile_Store
{
    // ==========================================================================
    // ThemeManager: شكل الواجهة (Light/Dark) محفوظ في الذاكرة طول عمر تشغيل البرنامج
    // (زي AuthManager بالظبط) عشان كل شاشة (كل واحدة فيها BlazorWebView منفصل بيتبني
    // من جديد وقت التنقل) تعرف تقرأ آخر اختيار من غير ما تعمل رحلة لقاعدة البيانات
    // في كل مرة. أول قراءة بس بتيجي من StoreSettings.Theme، وبعدين بتفضل في الذاكرة.
    // ==========================================================================
    public static class ThemeManager
    {
        private static string? _currentTheme;

        public static string CurrentTheme
        {
            get
            {
                _currentTheme ??= SettingsRepository.GetTheme();
                return _currentTheme;
            }
        }

        public static void SetTheme(string theme)
        {
            _currentTheme = theme == "dark" ? "dark" : "light";
            SettingsRepository.SaveTheme(_currentTheme);
        }

        public static string ToggledTheme() => CurrentTheme == "dark" ? "light" : "dark";
    }
}
