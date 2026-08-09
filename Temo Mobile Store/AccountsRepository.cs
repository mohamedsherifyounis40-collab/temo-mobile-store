using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // AccountsRepository: كل الوصول لقاعدة البيانات الخاص بشاشة الحسابات -
    // شجرة الحسابات، قائمة الدخل، وميزان المراجعة.
    // ==========================================================================
    public static class AccountsRepository
    {
        // ---------- شجرة الحسابات ----------

        public static DataTable GetAccountsTreeGrid()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("كود الحساب"), new DataColumn("اسم الحساب") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT AccountCode, AccountName FROM AccountsTree ORDER BY AccountCode ASC", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        dt.Rows.Add(reader["AccountCode"], reader["AccountName"]);
                }
            }
            return dt;
        }

        public static bool AccountCodeExists(int code)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM AccountsTree WHERE AccountCode = @Code", conn))
                {
                    cmd.Parameters.AddWithValue("@Code", code);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        public static void AddAccount(int code, string name)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("INSERT INTO AccountsTree (AccountCode, AccountName) VALUES (@Code, @Name)", conn))
                {
                    cmd.Parameters.AddWithValue("@Code", code);
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void UpdateAccountName(int code, string name)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("UPDATE AccountsTree SET AccountName = @Name WHERE AccountCode = @Code", conn))
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Code", code);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static int GetAccountUsageCount(int code)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                int usageCount = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM Expenses WHERE AccountCode = @Code", conn))
                {
                    cmd.Parameters.AddWithValue("@Code", code);
                    usageCount += Convert.ToInt32(cmd.ExecuteScalar());
                }
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM CashMovements WHERE AccountCode = @Code", conn))
                {
                    cmd.Parameters.AddWithValue("@Code", code);
                    usageCount += Convert.ToInt32(cmd.ExecuteScalar());
                }
                // القيد المحاسبي نفسه (JournalLines) هو المصدر الحقيقي - حساب ليه قيود
                // فعلية بس معندوش صفوف في Expenses/CashMovements (زي عملاء/موردين/مخزون)
                // كان ممكن يتحذف من غير أي تحذير قبل الفحص ده
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM JournalLines WHERE AccountCode = @Code", conn))
                {
                    cmd.Parameters.AddWithValue("@Code", code);
                    usageCount += Convert.ToInt32(cmd.ExecuteScalar());
                }
                return usageCount;
            }
        }

        public static void DeleteAccount(int code)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("DELETE FROM AccountsTree WHERE AccountCode = @Code", conn))
                {
                    cmd.Parameters.AddWithValue("@Code", code);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ---------- قائمة الدخل ----------

        // بتتحسب من الدفتر المحاسبي الحقيقي (JournalLines) مباشرة - كل حساب إيراد (4xxx)
        // أو مصروف/تكلفة (5xxx) له حركة في الفترة، بدل الحساب اليدوي القديم من جداول
        // Sales/Expenses/CashMovements منفصلة عن بعض. القيود كلها متزنة بالضمان من
        // AccountingEngine.Post، فمفيش احتمال يطلع رقم من غير أصل قيد حقيقي وراه.
        public static DataTable GetIncomeStatement(DateTime from, DateTime to)
        {
            string fromDate = from.ToString("yyyy-MM-dd");
            string toDate = to.ToString("yyyy-MM-dd");

            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("البند"), new DataColumn("المبلغ") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                var revenueRows = new List<(int Code, string Name, decimal Amount)>();
                var expenseRows = new List<(int Code, string Name, decimal Amount)>();
                decimal cogs = 0;

                string query = @"
                    SELECT A.AccountCode, A.AccountName,
                           COALESCE(SUM(L.Debit), 0) AS TotalDebit,
                           COALESCE(SUM(L.Credit), 0) AS TotalCredit
                    FROM AccountsTree A
                    INNER JOIN JournalLines L ON L.AccountCode = A.AccountCode
                    INNER JOIN JournalEntries E ON E.JournalEntryId = L.JournalEntryId
                    WHERE A.AccountCode >= 4000 AND A.AccountCode < 6000
                      AND E.EntryDate BETWEEN @From AND @To
                    GROUP BY A.AccountCode, A.AccountName
                    ORDER BY A.AccountCode";
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDate);
                    cmd.Parameters.AddWithValue("@To", toDate);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int code = Convert.ToInt32(reader["AccountCode"]);
                            string name = reader["AccountName"].ToString();
                            decimal debitSum = Convert.ToDecimal(reader["TotalDebit"]);
                            decimal creditSum = Convert.ToDecimal(reader["TotalCredit"]);

                            if (code < 5000)
                            {
                                // الإيراد طبيعته دائنة - صافي (دائن - مدين) عشان أي إلغاء/تعديل بيع يقلل الإيراد صح
                                decimal net = creditSum - debitSum;
                                if (net != 0) revenueRows.Add((code, name, net));
                            }
                            else if (code == 5500)
                            {
                                cogs = debitSum - creditSum;
                            }
                            else
                            {
                                decimal net = debitSum - creditSum;
                                if (net != 0) expenseRows.Add((code, name, net));
                            }
                        }
                    }
                }

                decimal totalRevenue = 0;
                foreach (var r in revenueRows)
                {
                    dt.Rows.Add(r.Name, r.Amount.ToString("N2"));
                    totalRevenue += r.Amount;
                }
                dt.Rows.Add("إجمالي الإيرادات", totalRevenue.ToString("N2"));
                dt.Rows.Add("", "");

                dt.Rows.Add("تكلفة البضاعة المباعة", cogs.ToString("N2"));
                decimal grossProfit = totalRevenue - cogs;
                dt.Rows.Add("مجمل الربح", grossProfit.ToString("N2"));
                dt.Rows.Add("", "");

                decimal totalExpenses = 0;
                foreach (var e in expenseRows)
                {
                    dt.Rows.Add(e.Name, e.Amount.ToString("N2"));
                    totalExpenses += e.Amount;
                }
                dt.Rows.Add("إجمالي المصروفات", totalExpenses.ToString("N2"));
                dt.Rows.Add("", "");

                decimal netProfit = grossProfit - totalExpenses;
                dt.Rows.Add("صافي الربح النهائي", netProfit.ToString("N2"));
            }

            return dt;
        }

        // ---------- ميزان المراجعة ----------

        // بيتحسب من الدفتر المحاسبي الحقيقي (JournalLines) مباشرة لكل حساب - صافي كل
        // حساب (مدين - دائن) بيتحدد على أي جنب يتعرض حسب الإشارة، فالإجمالي مضمون
        // يتساوى دايمًا (مدين = دائن) لأن كل قيد فرادى أصلًا متزن بالضمان من
        // AccountingEngine.Post قبل ما يتسجل في الدفتر من الأساس.
        public static DataTable GetTrialBalance()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("كود الحساب"), new DataColumn("اسم الحساب"), new DataColumn("مدين"), new DataColumn("دائن") });

            decimal totalDebit = 0, totalCredit = 0;

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                string query = @"
                    SELECT A.AccountCode, A.AccountName,
                           COALESCE(SUM(L.Debit), 0) AS TotalDebit,
                           COALESCE(SUM(L.Credit), 0) AS TotalCredit
                    FROM AccountsTree A
                    LEFT JOIN JournalLines L ON L.AccountCode = A.AccountCode
                    GROUP BY A.AccountCode, A.AccountName
                    ORDER BY A.AccountCode";
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int code = Convert.ToInt32(reader["AccountCode"]);
                        string name = reader["AccountName"].ToString();
                        decimal net = Convert.ToDecimal(reader["TotalDebit"]) - Convert.ToDecimal(reader["TotalCredit"]);
                        if (net == 0) continue;

                        decimal debit = net > 0 ? net : 0;
                        decimal credit = net < 0 ? -net : 0;

                        dt.Rows.Add(code, name, debit == 0 ? "" : debit.ToString("N2"), credit == 0 ? "" : credit.ToString("N2"));
                        totalDebit += debit;
                        totalCredit += credit;
                    }
                }
            }

            dt.Rows.Add("", "", "", "");
            dt.Rows.Add("", "الإجمالي", totalDebit.ToString("N2"), totalCredit.ToString("N2"));

            return dt;
        }

        // دفتر الأستاذ العام - بند بند (كل سطر مدين/دائن على حدة) من JournalLines/JournalEntries
        // الحقيقية، بدل الإجماليات المعروضة في ميزان المراجعة/قائمة الدخل. ده الشاشة الوحيدة في
        // البرنامج اللي بتوريك القيود الخام نفسها - accountCode=null يعني كل الحسابات،
        // sourceType=null يعني كل أنواع المصادر.
        public static DataTable GetGeneralLedger(DateTime from, DateTime to, int? accountCode, string? sourceType)
        {
            string fromDate = from.ToString("yyyy-MM-dd");
            string toDate = to.ToString("yyyy-MM-dd");

            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] {
                new DataColumn("التاريخ"), new DataColumn("كود الحساب"), new DataColumn("اسم الحساب"),
                new DataColumn("البيان"), new DataColumn("نوع المصدر"), new DataColumn("رقم المصدر"),
                new DataColumn("مدين"), new DataColumn("دائن"), new DataColumn("المستخدم")
            });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                string query = @"
                    SELECT E.EntryDate, A.AccountCode, A.AccountName, E.Description, E.SourceType, E.SourceId,
                           L.Debit, L.Credit, E.CreatedBy
                    FROM JournalLines L
                    INNER JOIN JournalEntries E ON E.JournalEntryId = L.JournalEntryId
                    INNER JOIN AccountsTree A ON A.AccountCode = L.AccountCode
                    WHERE E.EntryDate BETWEEN @From AND @To
                          AND (@AccountCode IS NULL OR A.AccountCode = @AccountCode)
                          AND (@SourceType IS NULL OR E.SourceType = @SourceType)
                    ORDER BY E.EntryDate ASC, E.JournalEntryId ASC, L.JournalLineId ASC";

                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDate);
                    cmd.Parameters.AddWithValue("@To", toDate);
                    cmd.Parameters.AddWithValue("@AccountCode", (object?)accountCode ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SourceType", (object?)sourceType ?? DBNull.Value);

                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            decimal debit = Convert.ToDecimal(reader["Debit"]);
                            decimal credit = Convert.ToDecimal(reader["Credit"]);
                            dt.Rows.Add(
                                reader["EntryDate"], reader["AccountCode"], reader["AccountName"],
                                reader["Description"] == DBNull.Value ? "" : reader["Description"].ToString(),
                                reader["SourceType"], reader["SourceId"] == DBNull.Value ? "" : reader["SourceId"].ToString(),
                                debit == 0 ? "" : debit.ToString("N2"), credit == 0 ? "" : credit.ToString("N2"),
                                reader["CreatedBy"] == DBNull.Value ? "" : reader["CreatedBy"].ToString()
                            );
                        }
                    }
                }
            }

            return dt;
        }
    }
}
