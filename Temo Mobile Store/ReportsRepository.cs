using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // ReportsRepository: كل قراءات شاشة "التقارير" (الملخص المالي، سجل الأرباح
    // التفصيلي، سجل إقفال الأيام، كشف حساب الوسائل) - منقولة حرفيًا من
    // ReportsPageControl.cs القديمة. الكتابة الفعلية (إقفال/فتح اليوم) بقت
    // Commands حقيقية (CloseDayCommand/ReopenDayCommand) - الملف ده قراءة بس.
    // ==========================================================================
    public static class ReportsRepository
    {
        public static bool IsTodayClosed()
        {
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM DailyClosures WHERE ClosureDate = @Date AND PaymentMethod = 'نقدي'", conn))
                {
                    cmd.Parameters.AddWithValue("@Date", dateStr);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        public static (decimal TotalSales, decimal TotalCost, decimal TotalExpenses, decimal NetProfit) CalculateBusinessMetrics()
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                decimal totalSales = 0, totalCost = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Total - Discount + Tax) as TotalSales, SUM(CostPrice * QuantitySold) as TotalCost FROM Sales", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        totalSales = reader["TotalSales"] != DBNull.Value ? Convert.ToDecimal(reader["TotalSales"]) : 0;
                        totalCost = reader["TotalCost"] != DBNull.Value ? Convert.ToDecimal(reader["TotalCost"]) : 0;
                    }
                }

                decimal totalExpenses = 0;
                using (SqliteCommand cmdExp = new SqliteCommand("SELECT SUM(Amount) FROM Expenses", conn))
                {
                    var res = cmdExp.ExecuteScalar();
                    totalExpenses = res != DBNull.Value ? Convert.ToDecimal(res) : 0;
                }

                decimal netProfit = (totalSales - totalCost) - totalExpenses;
                return (totalSales, totalCost, totalExpenses, netProfit);
            }
        }

        public class DetailedProfitRow
        {
            public int SaleId;
            public string ProductName = "";
            public decimal CostPrice;
            public decimal Price;
            public int Quantity;
            public decimal NetProfit;
            public string SaleDate = "";
        }

        public static (List<DetailedProfitRow> Rows, decimal TotalSales, decimal TotalCost, decimal TotalExpenses, decimal NetProfit) GetDetailedProfit(DateTime from, DateTime to)
        {
            string fromDate = from.ToString("yyyy-MM-dd") + " 00:00:00";
            string toDate = to.ToString("yyyy-MM-dd") + " 23:59:59";

            var rows = new List<DetailedProfitRow>();
            decimal totalSales = 0, totalCost = 0;

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(
                    "SELECT SaleID, ProductName, CostPrice, Price, QuantitySold, Total, Discount, Tax, SaleDate FROM Sales WHERE SaleDate BETWEEN @From AND @To ORDER BY SaleID ASC", conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDate);
                    cmd.Parameters.AddWithValue("@To", toDate);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            decimal cost = Convert.ToDecimal(reader["CostPrice"]);
                            decimal sell = Convert.ToDecimal(reader["Price"]);
                            int qty = Convert.ToInt32(reader["QuantitySold"]);
                            decimal total = Convert.ToDecimal(reader["Total"]);
                            // الإجمالي الصافي الفعلي بعد الخصم + الضريبة - مش Total لوحده (قبل الخصم)
                            decimal netTotal = total - Convert.ToDecimal(reader["Discount"]) + Convert.ToDecimal(reader["Tax"]);

                            totalSales += netTotal;
                            totalCost += cost * qty;

                            rows.Add(new DetailedProfitRow
                            {
                                SaleId = Convert.ToInt32(reader["SaleID"]),
                                ProductName = reader["ProductName"].ToString() ?? "",
                                CostPrice = cost,
                                Price = sell,
                                Quantity = qty,
                                NetProfit = netTotal - cost * qty,
                                SaleDate = reader["SaleDate"].ToString() ?? ""
                            });
                        }
                    }
                }

                decimal totalExpenses = 0;
                using (SqliteCommand cmdExp = new SqliteCommand("SELECT SUM(Amount) FROM Expenses WHERE ExpenseDate BETWEEN @From AND @To", conn))
                {
                    cmdExp.Parameters.AddWithValue("@From", fromDate);
                    cmdExp.Parameters.AddWithValue("@To", toDate);
                    var res = cmdExp.ExecuteScalar();
                    totalExpenses = res != DBNull.Value ? Convert.ToDecimal(res) : 0;
                }

                decimal netProfit = (totalSales - totalCost) - totalExpenses;
                return (rows, totalSales, totalCost, totalExpenses, netProfit);
            }
        }

        private static decimal GetMethodOpeningBalance(SqliteConnection conn, string method)
        {
            decimal opening = 0;
            using (SqliteCommand cmd = new SqliteCommand("SELECT ActualClosingBalance FROM DailyClosures WHERE PaymentMethod = @Method ORDER BY ClosureDate DESC LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@Method", method);
                var result = cmd.ExecuteScalar();
                if (result != null) opening = Convert.ToDecimal(result);
                else
                {
                    using (SqliteCommand cmdBal = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn))
                    {
                        cmdBal.Parameters.AddWithValue("@Method", method);
                        var balRes = cmdBal.ExecuteScalar();
                        if (balRes != null) opening = Convert.ToDecimal(balRes);
                    }
                }
            }
            return opening;
        }

        public static decimal GetCurrentMethodBalance(string method)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                return GetCurrentMethodBalance(conn, method);
            }
        }

        private static decimal GetCurrentMethodBalance(SqliteConnection conn, string method)
        {
            using (SqliteCommand cmd = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn))
            {
                cmd.Parameters.AddWithValue("@Method", method);
                var res = cmd.ExecuteScalar();
                return (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
            }
        }

        // بيرجّع ملخص الإقفال المتوقع لليوم الحالي لكل وسيلة دفع - نفس منطق
        // GetAllMethodsClosureSummary في ReportsPageControl.cs القديمة بالظبط
        public static Dictionary<string, (decimal Opening, decimal TotalIn, decimal TotalOut, decimal RecordedBalance)> GetAllMethodsClosureSummary(string[] paymentMethods)
        {
            var result = new Dictionary<string, (decimal, decimal, decimal, decimal)>();
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();

                decimal expensesTotal = 0;
                using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM Expenses WHERE ExpenseDate LIKE @Today", conn))
                {
                    cmd.Parameters.AddWithValue("@Today", today + "%");
                    var r = cmd.ExecuteScalar();
                    expensesTotal = (r != null && r != DBNull.Value) ? Convert.ToDecimal(r) : 0;
                }

                foreach (string method in paymentMethods)
                {
                    decimal opening = GetMethodOpeningBalance(conn, method);

                    decimal methodIn = 0, methodOut = 0;
                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE PaymentMethod = @Method AND MovementType = 'قبض' AND MovementDate = @Today", conn))
                    {
                        cmd.Parameters.AddWithValue("@Method", method);
                        cmd.Parameters.AddWithValue("@Today", today);
                        var r = cmd.ExecuteScalar();
                        methodIn = (r != null && r != DBNull.Value) ? Convert.ToDecimal(r) : 0;
                    }
                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE PaymentMethod = @Method AND MovementType = 'صرف' AND MovementDate = @Today", conn))
                    {
                        cmd.Parameters.AddWithValue("@Method", method);
                        cmd.Parameters.AddWithValue("@Today", today);
                        var r = cmd.ExecuteScalar();
                        methodOut = (r != null && r != DBNull.Value) ? Convert.ToDecimal(r) : 0;
                    }

                    decimal totalIn = methodIn;
                    decimal totalOut = methodOut + (method == "نقدي" ? expensesTotal : 0);
                    decimal recordedBalance = GetCurrentMethodBalance(conn, method);

                    result[method] = (opening, totalIn, totalOut, recordedBalance);
                }
            }

            return result;
        }

        // لو اليوم مقفول بالفعل - جدول الوسيلة/الافتتاحي/الختامي الفعلي من DailyClosures
        public static DataTable GetClosedTodaySummary()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("الوسيلة"), new DataColumn("افتتاحي"), new DataColumn("ختامي فعلي") });
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT PaymentMethod, OpeningBalance, ActualClosingBalance FROM DailyClosures WHERE ClosureDate = @Date ORDER BY PaymentMethod", conn))
                {
                    cmd.Parameters.AddWithValue("@Date", today);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            dt.Rows.Add(reader["PaymentMethod"], Convert.ToDecimal(reader["OpeningBalance"]).ToString("N2"), Convert.ToDecimal(reader["ActualClosingBalance"]).ToString("N2"));
                    }
                }
            }
            return dt;
        }

        public static DataTable GetClosuresLog()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] {
                new DataColumn("Id"), new DataColumn("التاريخ"), new DataColumn("الوسيلة"), new DataColumn("رصيد افتتاحي"),
                new DataColumn("إجمالي وارد"), new DataColumn("إجمالي منصرف"),
                new DataColumn("ختامي فعلي"), new DataColumn("وقت الإقفال")
            });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT * FROM DailyClosures ORDER BY ClosureDate DESC, PaymentMethod ASC", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dt.Rows.Add(reader["Id"], reader["ClosureDate"], reader["PaymentMethod"], reader["OpeningBalance"],
                            reader["TotalIn"], reader["TotalOut"],
                            reader["ActualClosingBalance"], reader["ClosedAt"]);
                    }
                }
            }
            return dt;
        }

        public static DataTable GetClosureDenominations(int closureId)
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("الفئة"), new DataColumn("العدد"), new DataColumn("الإجمالي") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT DenominationValue, DenominationCount, LineTotal FROM CashDenominations WHERE ClosureId = @ClosureId ORDER BY DenominationValue DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@ClosureId", closureId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            dt.Rows.Add(reader["DenominationValue"], reader["DenominationCount"], reader["LineTotal"]);
                    }
                }
            }
            return dt;
        }

        // كشف حساب وسيلة دفع معينة بالترتيب الزمني - useDateFilter=false بيرجّع كل الفترات
        public static (DataTable Table, decimal TotalIn, decimal TotalOut, decimal CurrentBalance) GetStatement(string method, DateTime from, DateTime to, bool useDateFilter)
        {
            string fromDate = useDateFilter ? from.ToString("yyyy-MM-dd") + " 00:00:00" : "0000-01-01 00:00:00";
            string toDate = useDateFilter ? to.ToString("yyyy-MM-dd") + " 23:59:59" : "9999-12-31 23:59:59";

            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] {
                new DataColumn("التاريخ والوقت"), new DataColumn("نوع الحركة"), new DataColumn("البيان"),
                new DataColumn("وارد"), new DataColumn("منصرف"), new DataColumn("الرصيد بعد الحركة")
            });

            // ملحوظة مهمة: مفيش union مباشر مع جدول Sales هنا لأي وسيلة (حتى نقدي) - كل
            // عملية بيع (وأي تعديل/إلغاء عليها بعد كده) بتتسجل أصلاً كحركة في CashMovements
            // عن طريق CashDrawerEngine.Credit/Debit وقت التنفيذ (شوف SalesHandlers.cs).
            // الاعتماد على الاتنين مع بعض كان بيسبب ظهور كل عملية بيع نقدي مرتين في الكشف
            // (مرة "بيع" من جدول Sales نفسه، ومرة "قبض" من CashMovements) - وبيضاعف
            // إجمالي الوارد والرصيد الجاري المعروض. CashMovements لوحدها كافية ودقيقة:
            // بتعكس كل حركة كاش فعلية بتاريخها الحقيقي، حتى تسويات تعديل/إلغاء البيع اللاحقة.
            string query = @"
                SELECT E.ExpenseDate AS OpDate, 'مصروف' AS OpType, A.AccountName AS Details, E.Amount AS Amount, 0 AS IsIn
                    FROM Expenses E INNER JOIN AccountsTree A ON E.AccountCode = A.AccountCode
                    WHERE E.PaymentMethod = @Method AND E.ExpenseDate BETWEEN @From AND @To
                UNION ALL
                SELECT CM.CreatedAt AS OpDate, CM.MovementType AS OpType,
                       (CASE WHEN AC.AccountName IS NOT NULL THEN AC.AccountName ELSE 'بدون تصنيف' END ||
                        CASE WHEN CM.Description IS NOT NULL AND CM.Description <> '' THEN ' - ' || CM.Description ELSE '' END) AS Details,
                       CM.Amount, CASE WHEN CM.MovementType = 'قبض' THEN 1 ELSE 0 END AS IsIn
                    FROM CashMovements CM LEFT JOIN AccountsTree AC ON CM.AccountCode = AC.AccountCode
                    WHERE CM.PaymentMethod = @Method AND CM.CreatedAt BETWEEN @From AND @To
                ORDER BY OpDate ASC";

            decimal totalIn = 0, totalOut = 0, runningBalance = 0, currentBalance = 0;

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@From", fromDate);
                    cmd.Parameters.AddWithValue("@To", toDate);
                    cmd.Parameters.AddWithValue("@Method", method);

                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            bool isIn = Convert.ToInt32(reader["IsIn"]) == 1;
                            decimal amount = Convert.ToDecimal(reader["Amount"]);
                            runningBalance += isIn ? amount : -amount;
                            if (isIn) totalIn += amount; else totalOut += amount;

                            dt.Rows.Add(
                                reader["OpDate"], reader["OpType"], reader["Details"],
                                isIn ? amount.ToString("N2") : "",
                                !isIn ? amount.ToString("N2") : "",
                                runningBalance.ToString("N2")
                            );
                        }
                    }
                }

                currentBalance = GetCurrentMethodBalance(conn, method);
            }

            return (dt, totalIn, totalOut, currentBalance);
        }
    }
}
