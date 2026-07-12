using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // AccountsRepository: كل الوصول لقاعدة البيانات الخاص بشاشة الحسابات -
    // شجرة الحسابات، قائمة الدخل، وميزان المراجعة.
    // ==========================================================================
    public static class AccountsRepository
    {
        private static readonly Dictionary<string, int> PaymentMethodAccountCodes = new Dictionary<string, int>
        {
            { "نقدي", 1100 }, { "فوري", 1110 }, { "أمان", 1120 }, { "سهولة", 1130 }, { "فودافون كاش", 1140 }, { "إنستاباي", 1150 }
        };

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

        public static DataTable GetIncomeStatement(DateTime from, DateTime to)
        {
            string fromDateTime = from.ToString("yyyy-MM-dd") + " 00:00:00";
            string toDateTime = to.ToString("yyyy-MM-dd") + " 23:59:59";
            string fromDateOnly = from.ToString("yyyy-MM-dd");
            string toDateOnly = to.ToString("yyyy-MM-dd");

            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("البند"), new DataColumn("المبلغ") });

            decimal totalRevenue = 0, totalExpenses = 0;

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                decimal salesRevenue = 0, cogs = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Total) AS T, SUM(CostPrice * QuantitySold) AS C FROM Sales WHERE SaleDate BETWEEN @From AND @To", conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDateTime);
                    cmd.Parameters.AddWithValue("@To", toDateTime);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            salesRevenue = reader["T"] != DBNull.Value ? Convert.ToDecimal(reader["T"]) : 0;
                            cogs = reader["C"] != DBNull.Value ? Convert.ToDecimal(reader["C"]) : 0;
                        }
                    }
                }
                dt.Rows.Add("إيرادات مبيعات الموبايلات والإكسسوارات", salesRevenue.ToString("N2"));
                totalRevenue += salesRevenue;

                string revQuery = @"SELECT AC.AccountName, SUM(CM.Amount) AS Total
                    FROM CashMovements CM INNER JOIN AccountsTree AC ON CM.AccountCode = AC.AccountCode
                    WHERE CM.MovementType = 'قبض' AND AC.AccountCode >= 4000 AND AC.AccountCode < 5000
                      AND CM.MovementDate BETWEEN @From AND @To
                    GROUP BY AC.AccountCode, AC.AccountName ORDER BY AC.AccountCode";
                using (SqliteCommand cmd = new SqliteCommand(revQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDateOnly);
                    cmd.Parameters.AddWithValue("@To", toDateOnly);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            decimal amt = Convert.ToDecimal(reader["Total"]);
                            dt.Rows.Add(reader["AccountName"].ToString(), amt.ToString("N2"));
                            totalRevenue += amt;
                        }
                    }
                }

                dt.Rows.Add("إجمالي الإيرادات", totalRevenue.ToString("N2"));
                dt.Rows.Add("", "");
                dt.Rows.Add("تكلفة البضاعة المباعة", cogs.ToString("N2"));
                decimal grossProfit = totalRevenue - cogs;
                dt.Rows.Add("مجمل الربح", grossProfit.ToString("N2"));
                dt.Rows.Add("", "");

                var expenseTotals = new Dictionary<string, decimal>();
                var expenseOrder = new List<(int code, string name)>();

                string expQuery = @"SELECT A.AccountCode, A.AccountName, SUM(E.Amount) AS Total
                    FROM Expenses E INNER JOIN AccountsTree A ON E.AccountCode = A.AccountCode
                    WHERE E.ExpenseDate BETWEEN @From AND @To
                    GROUP BY A.AccountCode, A.AccountName";
                using (SqliteCommand cmd = new SqliteCommand(expQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDateTime);
                    cmd.Parameters.AddWithValue("@To", toDateTime);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int code = Convert.ToInt32(reader["AccountCode"]);
                            string name = reader["AccountName"].ToString();
                            decimal amt = Convert.ToDecimal(reader["Total"]);
                            string key = code + "|" + name;
                            if (!expenseTotals.ContainsKey(key)) { expenseTotals[key] = 0; expenseOrder.Add((code, name)); }
                            expenseTotals[key] += amt;
                        }
                    }
                }

                string movExpQuery = @"SELECT AC.AccountCode, AC.AccountName, SUM(CM.Amount) AS Total
                    FROM CashMovements CM INNER JOIN AccountsTree AC ON CM.AccountCode = AC.AccountCode
                    WHERE CM.MovementType = 'صرف' AND AC.AccountCode >= 5000 AND AC.AccountCode < 6000
                      AND CM.MovementDate BETWEEN @From AND @To
                    GROUP BY AC.AccountCode, AC.AccountName";
                using (SqliteCommand cmd = new SqliteCommand(movExpQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDateOnly);
                    cmd.Parameters.AddWithValue("@To", toDateOnly);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int code = Convert.ToInt32(reader["AccountCode"]);
                            string name = reader["AccountName"].ToString();
                            decimal amt = Convert.ToDecimal(reader["Total"]);
                            string key = code + "|" + name;
                            if (!expenseTotals.ContainsKey(key)) { expenseTotals[key] = 0; expenseOrder.Add((code, name)); }
                            expenseTotals[key] += amt;
                        }
                    }
                }

                foreach (var item in expenseOrder.OrderBy(x => x.code))
                {
                    string key = item.code + "|" + item.name;
                    decimal amt = expenseTotals[key];
                    dt.Rows.Add(item.name, amt.ToString("N2"));
                    totalExpenses += amt;
                }

                dt.Rows.Add("إجمالي المصروفات", totalExpenses.ToString("N2"));
                dt.Rows.Add("", "");
                decimal netProfit = grossProfit - totalExpenses;
                dt.Rows.Add("صافي الربح النهائي", netProfit.ToString("N2"));
            }

            return dt;
        }

        // ---------- ميزان المراجعة ----------

        public static DataTable GetTrialBalance()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("كود الحساب"), new DataColumn("اسم الحساب"), new DataColumn("مدين"), new DataColumn("دائن") });

            decimal totalDebit = 0, totalCredit = 0;

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                var accounts = new List<(int code, string name)>();
                using (SqliteCommand cmd = new SqliteCommand("SELECT AccountCode, AccountName FROM AccountsTree ORDER BY AccountCode", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        accounts.Add((Convert.ToInt32(reader["AccountCode"]), reader["AccountName"].ToString()));
                }

                foreach (var acc in accounts)
                {
                    decimal debit = 0, credit = 0;

                    string matchingMethod = PaymentMethodAccountCodes.FirstOrDefault(x => x.Value == acc.code).Key;
                    if (matchingMethod != null)
                    {
                        using (SqliteCommand cmd = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn))
                        {
                            cmd.Parameters.AddWithValue("@Method", matchingMethod);
                            var res = cmd.ExecuteScalar();
                            debit = res != null ? Convert.ToDecimal(res) : 0;
                        }
                    }
                    else if (acc.code == 1200)
                    {
                        using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Quantity * Price) FROM Products", conn))
                        {
                            var res = cmd.ExecuteScalar();
                            debit = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                        }
                    }
                    else if (acc.code >= 4000 && acc.code < 5000)
                    {
                        using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE MovementType = 'قبض' AND AccountCode = @Code", conn))
                        {
                            cmd.Parameters.AddWithValue("@Code", acc.code);
                            var res = cmd.ExecuteScalar();
                            credit = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                        }
                    }
                    else if (acc.code >= 5000 && acc.code < 6000)
                    {
                        using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM Expenses WHERE AccountCode = @Code", conn))
                        {
                            cmd.Parameters.AddWithValue("@Code", acc.code);
                            var res = cmd.ExecuteScalar();
                            debit += (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                        }
                        using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE MovementType = 'صرف' AND AccountCode = @Code", conn))
                        {
                            cmd.Parameters.AddWithValue("@Code", acc.code);
                            var res = cmd.ExecuteScalar();
                            debit += (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                        }
                    }
                    else
                    {
                        decimal inAmount = 0, outAmount = 0;
                        using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE MovementType = 'قبض' AND AccountCode = @Code", conn))
                        {
                            cmd.Parameters.AddWithValue("@Code", acc.code);
                            var res = cmd.ExecuteScalar();
                            inAmount = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                        }
                        using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE MovementType = 'صرف' AND AccountCode = @Code", conn))
                        {
                            cmd.Parameters.AddWithValue("@Code", acc.code);
                            var res = cmd.ExecuteScalar();
                            outAmount = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                        }
                        decimal net = inAmount - outAmount;
                        if (net >= 0) credit = net; else debit = Math.Abs(net);
                    }

                    if (debit == 0 && credit == 0) continue;

                    dt.Rows.Add(acc.code, acc.name, debit == 0 ? "" : debit.ToString("N2"), credit == 0 ? "" : credit.ToString("N2"));
                    totalDebit += debit;
                    totalCredit += credit;
                }

                decimal allSales = 0, allCogs = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Total) AS T, SUM(CostPrice * QuantitySold) AS C FROM Sales", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        allSales = reader["T"] != DBNull.Value ? Convert.ToDecimal(reader["T"]) : 0;
                        allCogs = reader["C"] != DBNull.Value ? Convert.ToDecimal(reader["C"]) : 0;
                    }
                }
                if (allSales > 0)
                {
                    dt.Rows.Add("", "إيرادات المبيعات (من جدول المبيعات مباشرة)", "", allSales.ToString("N2"));
                    totalCredit += allSales;
                }
                if (allCogs > 0)
                {
                    dt.Rows.Add("", "تكلفة البضاعة المباعة (تقديري)", allCogs.ToString("N2"), "");
                    totalDebit += allCogs;
                }
            }

            dt.Rows.Add("", "", "", "");
            dt.Rows.Add("", "الإجمالي", totalDebit.ToString("N2"), totalCredit.ToString("N2"));

            return dt;
        }
    }
}
