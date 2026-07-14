using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;
using TemoStore.Core.Entities;

namespace TemoStore.Data.Repositories
{
    // مقصود إن الكيان ده يفتح Connection مستقل بتاعه، مش شريك في Transaction أي عملية
    // مالية - تسجيل الـ Audit بيحصل بعد الـ Commit (من خلال Event Bus)، فمينفعش يشارك
    // نفس الـ Transaction اللي أصلاً خلصت وقفلت. فشل تسجيل الـ Audit (لو حصل) ميرجعش
    // عملية مالية ناجحة بالفعل - راجع قسم 4 بمستند العمارة.
    public class AuditRepository : IAuditRepository
    {
        public void Insert(AuditEntry entry)
        {
            using var conn = new SqliteConnection(DbConnectionFactory.ConnectionString);
            conn.Open();
            using var cmd = new SqliteCommand(
                "INSERT INTO AuditLog (Username, Screen, Operation, OldData, NewData, CreatedAt) VALUES (@User, @Screen, @Op, @Old, @New, @CreatedAt)", conn);
            cmd.Parameters.AddWithValue("@User", entry.Username);
            cmd.Parameters.AddWithValue("@Screen", entry.Screen);
            cmd.Parameters.AddWithValue("@Op", entry.Operation);
            cmd.Parameters.AddWithValue("@Old", (object?)entry.OldData ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@New", (object?)entry.NewData ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedAt", entry.OccurredAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();
        }
    }
}
