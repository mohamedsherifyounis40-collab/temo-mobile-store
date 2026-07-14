using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;

namespace TemoStore.Data.Repositories
{
    public class MaintenanceTicketRepository : IMaintenanceRepository
    {
        private readonly SqliteConnection _conn;
        private readonly SqliteTransaction _tx;

        public MaintenanceTicketRepository(SqliteConnection conn, SqliteTransaction tx)
        {
            _conn = conn;
            _tx = tx;
        }

        public int Insert(string customerName, string? customerPhone, string deviceInfo, string? issueDescription, decimal estimatedCost)
        {
            using (var cmd = new SqliteCommand(
                "INSERT INTO MaintenanceTickets (CustomerName, CustomerPhone, DeviceInfo, IssueDescription, ReceivedDate, EstimatedCost, Status) VALUES (@N, @P, @D, @I, @R, @E, 'مستلم')", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@N", customerName);
                cmd.Parameters.AddWithValue("@P", (object?)customerPhone ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@D", deviceInfo);
                cmd.Parameters.AddWithValue("@I", (object?)issueDescription ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@R", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@E", estimatedCost);
                cmd.ExecuteNonQuery();
            }
            using var cmdId = new SqliteCommand("SELECT last_insert_rowid();", _conn, _tx);
            return Convert.ToInt32(cmdId.ExecuteScalar());
        }

        public void UpdateStatus(int ticketId, string newStatus)
        {
            using var cmd = new SqliteCommand("UPDATE MaintenanceTickets SET Status = @S WHERE TicketId = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@S", newStatus);
            cmd.Parameters.AddWithValue("@Id", ticketId);
            cmd.ExecuteNonQuery();
        }

        public string? GetCustomerName(int ticketId)
        {
            using var cmd = new SqliteCommand("SELECT CustomerName FROM MaintenanceTickets WHERE TicketId = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", ticketId);
            var res = cmd.ExecuteScalar();
            return res?.ToString();
        }

        public void SetDelivered(int ticketId, decimal actualCost)
        {
            using var cmd = new SqliteCommand(
                "UPDATE MaintenanceTickets SET Status = 'تم التسليم', ActualCost = @A, DeliveredDate = @D WHERE TicketId = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@A", actualCost);
            cmd.Parameters.AddWithValue("@D", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@Id", ticketId);
            cmd.ExecuteNonQuery();
        }
    }
}
