using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;

namespace TemoStore.Data.Repositories
{
    public class AttendanceRecordRepository : IAttendanceRepository
    {
        private const string StatusPresent = "حاضر";
        private const string StatusAbsent = "غائب";
        private const string StatusLeave = "إجازة";

        private readonly SqliteConnection _conn;
        private readonly SqliteTransaction _tx;

        public AttendanceRecordRepository(SqliteConnection conn, SqliteTransaction tx)
        {
            _conn = conn;
            _tx = tx;
        }

        public void UpsertStatus(int employeeId, DateTime date, string status)
        {
            using var cmd = new SqliteCommand(
                @"INSERT INTO AttendanceRecords (EmployeeId, AttendanceDate, Status) VALUES (@Id, @Date, @Status)
                  ON CONFLICT(EmployeeId, AttendanceDate) DO UPDATE SET Status = excluded.Status", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", employeeId);
            cmd.Parameters.AddWithValue("@Date", date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.ExecuteNonQuery();
        }

        public bool IsMonthClosed(int employeeId, int year, int month)
        {
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM PayrollClosures WHERE EmployeeId = @Id AND Year = @Y AND Month = @M", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", employeeId);
            cmd.Parameters.AddWithValue("@Y", year);
            cmd.Parameters.AddWithValue("@M", month);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public (int Present, int Absent, int Leave) GetAttendanceCounts(int employeeId, int year, int month)
        {
            int present = 0, absent = 0, leave = 0;
            string monthPrefix = $"{year:0000}-{month:00}";
            using var cmd = new SqliteCommand("SELECT Status, COUNT(*) FROM AttendanceRecords WHERE EmployeeId = @Id AND AttendanceDate LIKE @Prefix GROUP BY Status", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", employeeId);
            cmd.Parameters.AddWithValue("@Prefix", monthPrefix + "%");
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string status = reader.GetString(0);
                int count = reader.GetInt32(1);
                if (status == StatusPresent) present = count;
                else if (status == StatusAbsent) absent = count;
                else if (status == StatusLeave) leave = count;
            }
            return (present, absent, leave);
        }

        public void InsertClosure(int employeeId, int year, int month, decimal monthlySalary, decimal dayValue, int presentDays, int absentDays, int leaveDays, decimal netSalary)
        {
            using var cmd = new SqliteCommand(
                @"INSERT INTO PayrollClosures (EmployeeId, Year, Month, MonthlySalary, DayValue, PresentDays, AbsentDays, LeaveDays, NetSalary)
                  VALUES (@Id, @Y, @M, @Salary, @DayValue, @Present, @Absent, @Leave, @Net)", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", employeeId);
            cmd.Parameters.AddWithValue("@Y", year);
            cmd.Parameters.AddWithValue("@M", month);
            cmd.Parameters.AddWithValue("@Salary", monthlySalary);
            cmd.Parameters.AddWithValue("@DayValue", dayValue);
            cmd.Parameters.AddWithValue("@Present", presentDays);
            cmd.Parameters.AddWithValue("@Absent", absentDays);
            cmd.Parameters.AddWithValue("@Leave", leaveDays);
            cmd.Parameters.AddWithValue("@Net", netSalary);
            cmd.ExecuteNonQuery();
        }

        public int DeleteClosuresForMonth(int year, int month)
        {
            using var cmd = new SqliteCommand("DELETE FROM PayrollClosures WHERE Year = @Y AND Month = @M", _conn, _tx);
            cmd.Parameters.AddWithValue("@Y", year);
            cmd.Parameters.AddWithValue("@M", month);
            return cmd.ExecuteNonQuery();
        }

        public void DeleteAttendanceForEmployee(int employeeId)
        {
            using var cmd = new SqliteCommand("DELETE FROM AttendanceRecords WHERE EmployeeId = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", employeeId);
            cmd.ExecuteNonQuery();
        }

        public decimal GetEmployeeBalance(int employeeId)
        {
            decimal totalEarned = 0, totalPaid = 0;

            using (var cmd = new SqliteCommand("SELECT SUM(NetSalary) FROM PayrollClosures WHERE EmployeeId = @Id", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@Id", employeeId);
                var res = cmd.ExecuteScalar();
                if (res != null && res != DBNull.Value) totalEarned = Convert.ToDecimal(res);
            }

            using (var cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE EmployeeId = @Id AND MovementType = 'صرف'", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@Id", employeeId);
                var res = cmd.ExecuteScalar();
                if (res != null && res != DBNull.Value) totalPaid = Convert.ToDecimal(res);
            }

            return totalEarned - totalPaid;
        }
    }
}
