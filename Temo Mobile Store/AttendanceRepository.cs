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
    // = قيمة اليوم × (أيام الحضور + أيام الإجازة المدفوعة). يعني الراتب بيتراكم يوم بيوم
    // مع كل تسجيل حضور، مش بيبدأ من المرتب الكامل وينقص منه بالغياب.
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
                new DataColumn("المرتب الشهري"), new DataColumn("ساعات العمل باليوم"), new DataColumn("تاريخ التعيين") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT EmployeeId, FullName, Phone, MonthlySalary, StandardHoursPerDay, HireDate FROM Employees ORDER BY FullName", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dt.Rows.Add(reader["EmployeeId"], reader["FullName"], reader["Phone"] == DBNull.Value ? "" : reader["Phone"],
                            Convert.ToDecimal(reader["MonthlySalary"]).ToString("N2"), Convert.ToDecimal(reader["StandardHoursPerDay"]).ToString("N2"), reader["HireDate"]);
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

        // فيه شهور رواتب مقفولة أو صرف مسجّل لهذا الموظف؟ لو أيوه، مينفعش يتحذف (زي
        // بالظبط منع حذف مورد/عميل ليه فواتير - عشان مايتفقدش الأثر المحاسبي لمبالغ اتصرفت فعليًا)
        public static bool HasPayrollHistory(int employeeId)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM PayrollClosures WHERE EmployeeId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", employeeId);
                    if (Convert.ToInt32(cmd.ExecuteScalar()) > 0) return true;
                }
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM CashMovements WHERE EmployeeId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", employeeId);
                    if (Convert.ToInt32(cmd.ExecuteScalar()) > 0) return true;
                }
                return false;
            }
        }

        // ---------- تسجيل الحضور اليومي ----------

        // كل الموظفين + حالة حضورهم في تاريخ معين (لو لسه متسجلش، الحالة بتطلع فاضية)
        public static DataTable GetAttendanceForDate(DateTime date)
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("EmployeeId"), new DataColumn("الاسم"), new DataColumn("الحالة"), new DataColumn("ساعات إضافية") });

            string dateStr = date.ToString("yyyy-MM-dd");
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(
                    @"SELECT E.EmployeeId, E.FullName, A.Status, A.OvertimeHours
                      FROM Employees E
                      LEFT JOIN AttendanceRecords A ON A.EmployeeId = E.EmployeeId AND A.AttendanceDate = @Date
                      ORDER BY E.FullName", conn))
                {
                    cmd.Parameters.AddWithValue("@Date", dateStr);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            dt.Rows.Add(reader["EmployeeId"], reader["FullName"], reader["Status"] == DBNull.Value ? "" : reader["Status"],
                                reader["OvertimeHours"] == DBNull.Value ? "0" : Convert.ToDecimal(reader["OvertimeHours"]).ToString("N2"));
                    }
                }
            }
            return dt;
        }

        // ---------- كشف الرواتب ----------

        public class PayrollRow
        {
            public int EmployeeId;
            public string FullName;
            public decimal MonthlySalary;
            public decimal StandardHoursPerDay;
            public decimal DayValue;
            public decimal HourValue;
            public int PresentDays;
            public int AbsentDays;
            public int LeaveDays;
            public decimal OvertimeHours;
            public decimal OvertimeAmount;
            public decimal NetSalary;
            public bool IsClosed;
        }

        // بيرجع كشف الشهر: الشهور المقفولة بترجع بالأرقام المجمّدة وقت القفل (ثابتة نهائيًا)،
        // والشهور المفتوحة بترجع بحساب حي من سجلات الحضور والمرتب الحالي.
        public static System.Collections.Generic.List<PayrollRow> GetPayrollForMonth(int year, int month)
        {
            var rows = new System.Collections.Generic.List<PayrollRow>();

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT EmployeeId, FullName, MonthlySalary, StandardHoursPerDay FROM Employees ORDER BY FullName", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add(new PayrollRow
                        {
                            EmployeeId = Convert.ToInt32(reader["EmployeeId"]),
                            FullName = reader["FullName"].ToString(),
                            MonthlySalary = Convert.ToDecimal(reader["MonthlySalary"]),
                            StandardHoursPerDay = Convert.ToDecimal(reader["StandardHoursPerDay"])
                        });
                    }
                }

                foreach (var row in rows)
                    FillPayrollRow(conn, null, row, year, month);
            }
            return rows;
        }

        // بيحسب/يجيب قيم شهر معين لموظف معين (سواء من القفل المجمّد أو حساب حي) ويحطها جوه الـ row
        private static void FillPayrollRow(SqliteConnection conn, SqliteTransaction transaction, PayrollRow row, int year, int month)
        {
            using (SqliteCommand cmdClosure = new SqliteCommand(
                "SELECT MonthlySalary, DayValue, PresentDays, AbsentDays, LeaveDays, OvertimeHours, OvertimeAmount, NetSalary FROM PayrollClosures WHERE EmployeeId = @Id AND Year = @Y AND Month = @M", conn, transaction))
            {
                cmdClosure.Parameters.AddWithValue("@Id", row.EmployeeId);
                cmdClosure.Parameters.AddWithValue("@Y", year);
                cmdClosure.Parameters.AddWithValue("@M", month);
                using (SqliteDataReader reader = cmdClosure.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        row.MonthlySalary = Convert.ToDecimal(reader["MonthlySalary"]);
                        row.DayValue = Convert.ToDecimal(reader["DayValue"]);
                        row.HourValue = row.StandardHoursPerDay > 0 ? row.DayValue / row.StandardHoursPerDay : 0;
                        row.PresentDays = Convert.ToInt32(reader["PresentDays"]);
                        row.AbsentDays = Convert.ToInt32(reader["AbsentDays"]);
                        row.LeaveDays = Convert.ToInt32(reader["LeaveDays"]);
                        row.OvertimeHours = Convert.ToDecimal(reader["OvertimeHours"]);
                        row.OvertimeAmount = Convert.ToDecimal(reader["OvertimeAmount"]);
                        row.NetSalary = Convert.ToDecimal(reader["NetSalary"]);
                        row.IsClosed = true;
                        return;
                    }
                }
            }

            int daysInMonth = DateTime.DaysInMonth(year, month);
            string monthPrefix = $"{year:0000}-{month:00}";
            row.DayValue = daysInMonth > 0 ? row.MonthlySalary / daysInMonth : 0;
            row.HourValue = row.StandardHoursPerDay > 0 ? row.DayValue / row.StandardHoursPerDay : 0;
            row.PresentDays = 0; row.AbsentDays = 0; row.LeaveDays = 0;

            using (SqliteCommand cmd = new SqliteCommand(
                "SELECT Status, COUNT(*) FROM AttendanceRecords WHERE EmployeeId = @Id AND AttendanceDate LIKE @Prefix GROUP BY Status", conn, transaction))
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

            using (SqliteCommand cmd = new SqliteCommand(
                "SELECT SUM(OvertimeHours) FROM AttendanceRecords WHERE EmployeeId = @Id AND AttendanceDate LIKE @Prefix", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", row.EmployeeId);
                cmd.Parameters.AddWithValue("@Prefix", monthPrefix + "%");
                var res = cmd.ExecuteScalar();
                row.OvertimeHours = res != null && res != DBNull.Value ? Convert.ToDecimal(res) : 0;
            }

            // الراتب بيتبني تراكميًا من الأيام المسجّلة فعليًا (حضور + إجازة مدفوعة)، مش بيبدأ
            // من المرتب الكامل وينقص منه. يعني يوم مايتسجلش له حضور أصلًا (لسه ماجاش، أو الأدمن
            // نسي يسجّله) مايتحسبش للموظف تلقائيًا زي ما كان بيحصل قبل كده. الساعات الإضافية
            // بتتحسب فوق كده بـ1.5× قيمة الساعة العادية.
            row.OvertimeAmount = row.OvertimeHours * row.HourValue * 1.5m;
            row.NetSalary = row.DayValue * (row.PresentDays + row.LeaveDays) + row.OvertimeAmount;
            row.IsClosed = false;
        }

        // ---------- كشف حساب الموظف (المستحق ناقص السلف/الدفعات) ----------

        // الرصيد المستحق للموظف = إجمالي الشهور المقفولة - إجمالي المسحوب له من الخزينة
        public static decimal GetEmployeeBalance(int employeeId)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                return GetEmployeeBalanceInTransaction(conn, null, employeeId);
            }
        }

        private static decimal GetEmployeeBalanceInTransaction(SqliteConnection conn, SqliteTransaction transaction, int employeeId)
        {
            decimal totalEarned = 0, totalPaid = 0;

            using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(NetSalary) FROM PayrollClosures WHERE EmployeeId = @Id", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", employeeId);
                var res = cmd.ExecuteScalar();
                if (res != null && res != DBNull.Value) totalEarned = Convert.ToDecimal(res);
            }

            using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE EmployeeId = @Id AND MovementType = 'صرف'", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", employeeId);
                var res = cmd.ExecuteScalar();
                if (res != null && res != DBNull.Value) totalPaid = Convert.ToDecimal(res);
            }

            return totalEarned - totalPaid;
        }

        // كل الموظفين اللي رصيدهم سالب حاليًا (يعني اتصرفلهم سلف أكتر من المستحق ليهم لحد دلوقتي)
        public static DataTable GetOutstandingAdvances()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("الاسم"), new DataColumn("السلفة المستحقة عليه") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                var employees = new System.Collections.Generic.List<(int Id, string Name)>();
                using (SqliteCommand cmd = new SqliteCommand("SELECT EmployeeId, FullName FROM Employees ORDER BY FullName", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        employees.Add((Convert.ToInt32(reader["EmployeeId"]), reader["FullName"].ToString()));
                }

                foreach (var emp in employees)
                {
                    decimal balance = GetEmployeeBalanceInTransaction(conn, null, emp.Id);
                    if (balance < 0)
                        dt.Rows.Add(emp.Name, (-balance).ToString("N2"));
                }
            }
            return dt;
        }

        // كشف حساب الموظف بالترتيب الزمني: كل شهر مقفول (مستحق +) وكل صرف/سلفة (مسحوب -)، مع رصيد متراكم
        public static DataTable GetEmployeeStatement(int employeeId)
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("التاريخ"), new DataColumn("البيان"), new DataColumn("مستحق"), new DataColumn("مسحوب"), new DataColumn("الرصيد") });

            var events = new System.Collections.Generic.List<(DateTime Date, string Description, decimal Credit, decimal Debit)>();

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(
                    "SELECT Year, Month, NetSalary, ClosedAt FROM PayrollClosures WHERE EmployeeId = @Id ORDER BY Year, Month", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", employeeId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int y = Convert.ToInt32(reader["Year"]);
                            int m = Convert.ToInt32(reader["Month"]);
                            DateTime closedAt = DateTime.Parse(reader["ClosedAt"].ToString());
                            events.Add((closedAt, $"كشف راتب شهر {m:00}/{y}", Convert.ToDecimal(reader["NetSalary"]), 0));
                        }
                    }
                }

                using (SqliteCommand cmd = new SqliteCommand(
                    "SELECT MovementDate, Description, Amount, IsAdvance FROM CashMovements WHERE EmployeeId = @Id AND MovementType = 'صرف' ORDER BY MovementDate", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", employeeId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DateTime moveDate = DateTime.Parse(reader["MovementDate"].ToString());
                            bool isAdvance = Convert.ToInt32(reader["IsAdvance"]) == 1;
                            string typeLabel = isAdvance ? "سلفة" : "دفعة مستحق";
                            string desc = reader["Description"] == DBNull.Value ? typeLabel : $"{typeLabel}: {reader["Description"]}";
                            events.Add((moveDate, desc, 0, Convert.ToDecimal(reader["Amount"])));
                        }
                    }
                }
            }

            events.Sort((a, b) => a.Date.CompareTo(b.Date));

            decimal runningBalance = 0;
            foreach (var ev in events)
            {
                runningBalance += ev.Credit - ev.Debit;
                dt.Rows.Add(ev.Date.ToString("yyyy-MM-dd"), ev.Description,
                    ev.Credit > 0 ? ev.Credit.ToString("N2") : "",
                    ev.Debit > 0 ? ev.Debit.ToString("N2") : "",
                    runningBalance.ToString("N2"));
            }
            return dt;
        }

    }
}
