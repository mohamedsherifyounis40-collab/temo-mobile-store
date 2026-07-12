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
    // بتترمي لما حد يحاول يعدّل حضور يوم تابع لشهر اتقفل بالفعل لهذا الموظف
    public class PayrollMonthClosedException : Exception
    {
        public PayrollMonthClosedException(string message) : base(message) { }
    }

    public static class AttendanceRepository
    {
        public const string StatusPresent = "حاضر";
        public const string StatusAbsent = "غائب";
        public const string StatusLeave = "إجازة";

        // حساب "مرتبات" الأساسي في شجرة الحسابات - كل الصرف لموظفين بيتسجل عليه
        public const int SalariesAccountCode = 5400;

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

        // بيحذف الموظف وكل سجلات حضوره جوه Transaction واحدة عشان مايفضلش سجلات يتيمة.
        // ملحوظة: الاستدعاء بيفترض إن الشاشة أصلاً منعت الحذف لو HasPayrollHistory رجعت true،
        // زي ما بيحصل مع الموردين والعملاء.
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

        // بيسجّل/يعدّل حالة حضور موظف في تاريخ معين (upsert بالاعتماد على UNIQUE(EmployeeId, AttendanceDate)).
        // بيرمي PayrollMonthClosedException لو شهر التاريخ ده اتقفل بالفعل لهذا الموظف.
        public static void SetAttendanceStatus(int employeeId, DateTime date, string status)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                if (IsMonthClosed(conn, null, employeeId, date.Year, date.Month))
                    throw new PayrollMonthClosedException($"شهر {date.Month}/{date.Year} مقفول بالفعل لهذا الموظف، لا يمكن تعديل الحضور فيه.");

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

        private static bool IsMonthClosed(SqliteConnection conn, SqliteTransaction transaction, int employeeId, int year, int month)
        {
            using (SqliteCommand cmd = new SqliteCommand(
                "SELECT COUNT(*) FROM PayrollClosures WHERE EmployeeId = @Id AND Year = @Y AND Month = @M", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", employeeId);
                cmd.Parameters.AddWithValue("@Y", year);
                cmd.Parameters.AddWithValue("@M", month);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
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
                    FillPayrollRow(conn, null, row, year, month);
            }
            return rows;
        }

        // بيحسب/يجيب قيم شهر معين لموظف معين (سواء من القفل المجمّد أو حساب حي) ويحطها جوه الـ row
        private static void FillPayrollRow(SqliteConnection conn, SqliteTransaction transaction, PayrollRow row, int year, int month)
        {
            using (SqliteCommand cmdClosure = new SqliteCommand(
                "SELECT MonthlySalary, DayValue, PresentDays, AbsentDays, LeaveDays, NetSalary FROM PayrollClosures WHERE EmployeeId = @Id AND Year = @Y AND Month = @M", conn, transaction))
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
                        row.PresentDays = Convert.ToInt32(reader["PresentDays"]);
                        row.AbsentDays = Convert.ToInt32(reader["AbsentDays"]);
                        row.LeaveDays = Convert.ToInt32(reader["LeaveDays"]);
                        row.NetSalary = Convert.ToDecimal(reader["NetSalary"]);
                        row.IsClosed = true;
                        return;
                    }
                }
            }

            int daysInMonth = DateTime.DaysInMonth(year, month);
            string monthPrefix = $"{year:0000}-{month:00}";
            row.DayValue = daysInMonth > 0 ? row.MonthlySalary / daysInMonth : 0;
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

            row.NetSalary = row.MonthlySalary - (row.AbsentDays * row.DayValue);
            row.IsClosed = false;
        }

        // بيقفل شهر معين لكل الموظفين (بيتخطى أي موظف مقفول بالفعل لنفس الشهر) ويجمّد أرقامه
        // في PayrollClosures نهائيًا. بيرجع عدد الموظفين اللي اتقفلوا فعليًا في الاستدعاء ده.
        public static int ClosePayrollMonthForAll(int year, int month)
        {
            int closedCount = 0;
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        var employees = new System.Collections.Generic.List<(int Id, string Name, decimal Salary)>();
                        using (SqliteCommand cmd = new SqliteCommand("SELECT EmployeeId, FullName, MonthlySalary FROM Employees ORDER BY FullName", conn, transaction))
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                employees.Add((Convert.ToInt32(reader["EmployeeId"]), reader["FullName"].ToString(), Convert.ToDecimal(reader["MonthlySalary"])));
                        }

                        foreach (var emp in employees)
                        {
                            if (IsMonthClosed(conn, transaction, emp.Id, year, month)) continue;

                            var row = new PayrollRow { EmployeeId = emp.Id, FullName = emp.Name, MonthlySalary = emp.Salary };
                            FillPayrollRow(conn, transaction, row, year, month);

                            using (SqliteCommand cmdInsert = new SqliteCommand(
                                @"INSERT INTO PayrollClosures (EmployeeId, Year, Month, MonthlySalary, DayValue, PresentDays, AbsentDays, LeaveDays, NetSalary)
                                  VALUES (@Id, @Y, @M, @Salary, @DayValue, @Present, @Absent, @Leave, @Net)", conn, transaction))
                            {
                                cmdInsert.Parameters.AddWithValue("@Id", emp.Id);
                                cmdInsert.Parameters.AddWithValue("@Y", year);
                                cmdInsert.Parameters.AddWithValue("@M", month);
                                cmdInsert.Parameters.AddWithValue("@Salary", row.MonthlySalary);
                                cmdInsert.Parameters.AddWithValue("@DayValue", row.DayValue);
                                cmdInsert.Parameters.AddWithValue("@Present", row.PresentDays);
                                cmdInsert.Parameters.AddWithValue("@Absent", row.AbsentDays);
                                cmdInsert.Parameters.AddWithValue("@Leave", row.LeaveDays);
                                cmdInsert.Parameters.AddWithValue("@Net", row.NetSalary);
                                cmdInsert.ExecuteNonQuery();
                            }
                            closedCount++;
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
            return closedCount;
        }

        // ---------- كشف حساب الموظف (المستحق ناقص السلف/الدفعات) ----------

        // الرصيد المستحق للموظف = إجمالي الشهور المقفولة - إجمالي المسحوب له من الخزينة
        public static decimal GetEmployeeBalance(int employeeId)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                decimal totalEarned = 0, totalPaid = 0;

                using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(NetSalary) FROM PayrollClosures WHERE EmployeeId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", employeeId);
                    var res = cmd.ExecuteScalar();
                    if (res != null && res != DBNull.Value) totalEarned = Convert.ToDecimal(res);
                }

                using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE EmployeeId = @Id AND MovementType = 'صرف'", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", employeeId);
                    var res = cmd.ExecuteScalar();
                    if (res != null && res != DBNull.Value) totalPaid = Convert.ToDecimal(res);
                }

                return totalEarned - totalPaid;
            }
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
                    "SELECT MovementDate, Description, Amount FROM CashMovements WHERE EmployeeId = @Id AND MovementType = 'صرف' ORDER BY MovementDate", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", employeeId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DateTime moveDate = DateTime.Parse(reader["MovementDate"].ToString());
                            string desc = reader["Description"] == DBNull.Value ? "صرف مرتب/سلفة" : reader["Description"].ToString();
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

        // بيسجّل صرف مرتب/سلفة لموظف من رصيد وسيلة دفع معينة، جوه Transaction واحدة.
        // بيرمي InsufficientBalanceException لو رصيد وسيلة الدفع مايكفيش.
        public static void PayEmployee(int employeeId, string employeeName, string method, decimal amount, string description)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        decimal currentBalance = TreasuryRepository.GetBalanceInTransaction(conn, transaction, method);
                        if (amount > currentBalance)
                            throw new InsufficientBalanceException(method, currentBalance);

                        using (SqliteCommand cmd = new SqliteCommand(
                            @"INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode, EmployeeId)
                              VALUES (@Date, 'صرف', @Method, @Amount, @Ref, @Desc, @CreatedAt, @AccountCode, @EmployeeId)", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@Method", method);
                            cmd.Parameters.AddWithValue("@Amount", amount);
                            cmd.Parameters.AddWithValue("@Ref", "");
                            cmd.Parameters.AddWithValue("@Desc", string.IsNullOrWhiteSpace(description) ? ("صرف مرتب/سلفة: " + employeeName) : description);
                            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmd.Parameters.AddWithValue("@AccountCode", SalariesAccountCode);
                            cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                            cmd.ExecuteNonQuery();
                        }

                        TreasuryRepository.SetBalanceInTransaction(conn, transaction, method, currentBalance - amount);

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
    }
}
