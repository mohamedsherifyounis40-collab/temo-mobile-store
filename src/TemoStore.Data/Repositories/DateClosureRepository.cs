using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;

namespace TemoStore.Data.Repositories
{
    public class DateClosureRepository : IDateClosureRepository
    {
        public bool IsClosed(DateTime date, string paymentMethod = "نقدي")
        {
            using var conn = new SqliteConnection(DbConnectionFactory.ConnectionString);
            conn.Open();
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM DailyClosures WHERE ClosureDate = @Date AND PaymentMethod = @Method", conn);
            cmd.Parameters.AddWithValue("@Date", date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@Method", paymentMethod);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
    }
}
