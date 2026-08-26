// بيطبّق شكل الواجهة (Light/Dark) على أول عنصر <html> في الصفحة - بيتنادى من كل
// شاشة Blazor لحظة التحميل (بالقيمة المحفوظة) ولحظة الضغط على زر القمر 🌙 (بالقيمة الجديدة).
window.temoApplyTheme = function (theme) {
    document.documentElement.setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
};
