using System;
using System.Data;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // AttendanceRepository: كل الوصول لقاعدة البيانات الخاص بشاشة "الحضور والمرتبات" -
    // إدارة الموظفين، تسجيل الحضور اليومي، وحساب كشف الرواتب الشهري.
    //
    // معادلة الراتب: قيمة اليوم = المرتب الشهري ÷ عدد أيام الشهر، والراتب الصافي
    // = المرتب الشهري - (أيام الغياب × قيمة اليوم). أيام الإجازة مدفوعة (مفيش خصم).
    // ==========================================================================
    public static class AttendanceRepository
    {
        public const string StatusPresent = "حاضر";
        public const string StatusAbsent = "غائب";
        public const string StatusLeave = "إجازة";

        public static DataTable GetEmployees()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("EmployeeId"), new DataColumn("الاسم"), new DataColumn("الهاتف"),
                new DataColumn("المرتب الشهري"), new DataColumn("تاريخ التعيين") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT EmployeeId, FullName, Phone, MonthlySalary, HireDate FROM Employees ORDER BY FullName", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dt.Rows.Add(reader["EmployeeId"], reader["FullName"], reader["Phone"] == DBNull.Value ? "" : reader["Phone"],
                            Convert.ToDecimal(reader["MonthlySalary"]).ToString("N2"), reader["HireDate"]);
                    }
                }
            }
            return dt;
        }

        public static DataTable GetEmployeesCombo()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("EmployeeId", typeof(int));
            dt.Columns.Add("FullName", typeof(string));

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT EmployeeId, FullName FROM Employees ORDER BY FullName", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        dt.Rows.Add(Convert.ToInt32(reader["EmployeeId"]), reader["FullName"].ToString());
                }
            }
            return dt;
        }

        public static void AddEmployee(string fullName, string phone, decimal monthlySalary, DateTime hireDate)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(
                    "INSERT INTO Employees (FullName, Phone, MonthlySalary, HireDate) VALUES (@N, @P, @S, @H)", conn))
                {
                    cmd.Parameters.AddWithValue("@N", fullName);
                    cmd.Parameters.AddWithValue("@P", string.IsNullOrEmpty(phone) ? (object)DBNull.Value : phone);
                    cmd.Parameters.AddWithValue("@S", monthlySalary);
                    cmd.Parameters.AddWithValue("@H", hireDate.ToString("yyyy-MM-dd"));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void UpdateEmployee(int employeeId, string fullName, string phone, decimal monthlySalary, DateTime hireDate)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(
                    "UPDATE Employees SET FullName = @N, Phone = @P, MonthlySalary = @S, HireDate = @H WHERE EmployeeId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@N", fullName);
                    cmd.Parameters.AddWithValue("@P", string.IsNullOrEmpty(phone) ? (object)DBNull.Value : phone);
                    cmd.Parameters.AddWithValue("@S", monthlySalary);
                    cmd.Parameters.AddWithValue("@H", hireDate.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@Id", employeeId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // بيحذف الموظف وكل سجلات حضوره جوه Transaction واحدة عشان مايفضلش سجلات يتيمة
        public static void DeleteEmployee(int employeeId)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        using (SqliteCommand cmd = new SqliteCommand("DELETE FROM AttendanceRecords WHERE EmployeeId = @Id", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", employeeId);
                            cmd.ExecuteNonQuery();
                        }
                        using (SqliteCommand cmd = new SqliteCommand("DELETE FROM Employees WHERE EmployeeId = @Id", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", employeeId);
                            cmd.ExecuteNonQuery();
                        }
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // ---------- تسجيل الحضور اليومي ----------

        // كل الموظفين + حالة حضورهم في تاريخ معين (لو لسه متسجلش، الحالة بتطلع فاضية)
        public static DataTable GetAttendanceForDate(DateTime date)
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("EmployeeId"), new DataColumn("الاسم"), new DataColumn("الحالة") });

            string dateStr = date.ToString("yyyy-MM-dd");
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(
                    @"SELECT E.EmployeeId, E.FullName, A.Status
                      FROM Employees E
                      LEFT JOIN AttendanceRecords A ON A.EmployeeId = E.EmployeeId AND A.AttendanceDate = @Date
                      ORDER BY E.FullName", conn))
                {
                    cmd.Parameters.AddWithValue("@Date", dateStr);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            dt.Rows.Add(reader["EmployeeId"], reader["FullName"], reader["Status"] == DBNull.Value ? "" : reader["Status"]);
                    }
                }
            }
            return dt;
        }

        // بيسجّل/يعدّل حالة حضور موظف في تاريخ معين (upsert بالاعتماد على UNIQUE(EmployeeId, AttendanceDate))
        public static void SetAttendanceStatus(int employeeId, DateTime date, string status)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(
                    @"INSERT INTO AttendanceRecords (EmployeeId, AttendanceDate, Status) VALUES (@Id, @Date, @Status)
                      ON CONFLICT(EmployeeId, AttendanceDate) DO UPDATE SET Status = excluded.Status", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", employeeId);
                    cmd.Parameters.AddWithValue("@Date", date.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@Status", status);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ---------- كشف الرواتب ----------

        public class PayrollRow
        {
            public int EmployeeId;
            public string FullName;
            public decimal MonthlySalary;
            public decimal DayValue;
            public int PresentDays;
            public int AbsentDays;
            public int LeaveDays;
            public decimal NetSalary;
        }

        public static System.Collections.Generic.List<PayrollRow> GetPayrollForMonth(int year, int month)
        {
            var rows = new System.Collections.Generic.List<PayrollRow>();
            int daysInMonth = DateTime.DaysInMonth(year, month);
            string monthPrefix = $"{year:0000}-{month:00}";

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT EmployeeId, FullName, MonthlySalary FROM Employees ORDER BY FullName", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add(new PayrollRow
                        {
                            EmployeeId = Convert.ToInt32(reader["EmployeeId"]),
                            FullName = reader["FullName"].ToString(),
                            MonthlySalary = Convert.ToDecimal(reader["MonthlySalary"])
                        });
                    }
                }

                foreach (var row in rows)
                {
                    row.DayValue = daysInMonth > 0 ? row.MonthlySalary / daysInMonth : 0;

                    using (SqliteCommand cmd = new SqliteCommand(
                        "SELECT Status, COUNT(*) FROM AttendanceRecords WHERE EmployeeId = @Id AND AttendanceDate LIKE @Prefix GROUP BY Status", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", row.EmployeeId);
                        cmd.Parameters.AddWithValue("@Prefix", monthPrefix + "%");
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string status = reader.GetString(0);
                                int count = reader.GetInt32(1);
                                if (status == StatusPresent) row.PresentDays = count;
                                else if (status == StatusAbsent) row.AbsentDays = count;
                                else if (status == StatusLeave) row.LeaveDays = count;
                            }
                        }
                    }

                    row.NetSalary = row.MonthlySalary - (row.AbsentDays * row.DayValue);
                }
            }
            return rows;
        }
    }
}
