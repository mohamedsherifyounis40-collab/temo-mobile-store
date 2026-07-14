namespace TemoStore.Data
{
    // نفس منطق AuthManager.ConnectionString بالظبط (قاعدة البيانات جنب الـ exe) -
    // مكانها هنا دلوقتي عشان طبقة البيانات ماعادش محتاجة تعتمد على مشروع الواجهة.
    public static class DbConnectionFactory
    {
        public static readonly string ConnectionString =
            $"Data Source={Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TemoStoreDB.db")};";
    }
}
