using System;
using System.Data;
using Microsoft.Data.Sqlite;
using TemoStore.Core.Exceptions;

namespace Temo_Mobile_Store
{
    public class CashMovementRecord
    {
        public int Id;
        public string MovementType;
        public string PaymentMethod;
        public decimal Amount;
        public string ReferenceNumber;
        public string Description;
        public int? AccountCode;
        public string MovementDate;
        public int? LinkedMovementId;
    }

    // ==========================================================================
    // TreasuryRepository: القراءة فقط لشاشة الخزينة (شجرة الحسابات، سجل المصروفات،
    // أرصدة وسائل الدفع، سجل حركات القبض/الصرف). كل عمليات الكتابة المالية
    // (تسجيل/تعديل/حذف مصروف، حركة قبض/صرف، تحويل بين وسائل) بقت بتعدي من
    // خلال ICoreEngine (راجع TemoStore.Engines.Handlers).
    //
    // GetBalanceInTransaction/SetBalanceInTransaction فضلوا هنا (internal) لأن
    // MaintenanceRepository/AttendanceRepository (لسه على المسار القديم) بتستخدمهم.
    // ==========================================================================
    public static class TreasuryRepository
    {
        public static DataTable GetAccountsTree()
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT AccountCode, AccountName FROM AccountsTree ORDER BY AccountCode", conn))
                {
                    DataTable dt = new DataTable();
                    dt.Load(cmd.ExecuteReader());
                    return dt;
                }
            }
        }

        public static DataTable GetExpenses()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] {
                new DataColumn("رقم الحركة"),
                new DataColumn("كود الحساب"),
                new DataColumn("اسم بند المصروف"),
                new DataColumn("المبلغ ج.م"),
                new DataColumn("وسيلة الدفع"),
                new DataColumn("التاريخ والوقت ⏰")
            });

            string query = @"SELECT E.ExpenseID, E.AccountCode, A.AccountName, E.Amount, E.PaymentMethod, E.ExpenseDate
                             FROM Expenses E
                             INNER JOIN AccountsTree A ON E.AccountCode = A.AccountCode
                             ORDER BY E.ExpenseID ASC";

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        dt.Rows.Add(reader["ExpenseID"], reader["AccountCode"], reader["AccountName"], reader["Amount"], reader["PaymentMethod"] == DBNull.Value ? "نقدي" : reader["PaymentMethod"], reader["ExpenseDate"]);
                }
            }
            return dt;
        }

        public static decimal GetPaymentMethodBalance(string method)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                return GetBalanceInTransaction(conn, null, method);
            }
        }

        public static CashMovementRecord GetMovementById(int id)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                return GetMovementInTransaction(conn, null, id);
            }
        }

        public static DataTable GetCashMovements()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("Id"), new DataColumn("النوع"), new DataColumn("الوسيلة"), new DataColumn("المبلغ"), new DataColumn("المرجع"), new DataColumn("الوصف"), new DataColumn("التاريخ والوقت") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT Id, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt FROM CashMovements ORDER BY Id DESC", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        dt.Rows.Add(reader["Id"], reader["MovementType"], reader["PaymentMethod"], reader["Amount"], reader["ReferenceNumber"], reader["Description"], reader["CreatedAt"]);
                }
            }
            return dt;
        }

        private static CashMovementRecord GetMovementInTransaction(SqliteConnection conn, SqliteTransaction transaction, int id)
        {
            using (SqliteCommand cmd = new SqliteCommand("SELECT MovementType, PaymentMethod, Amount, ReferenceNumber, Description, AccountCode, MovementDate, LinkedMovementId FROM CashMovements WHERE Id = @Id", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new CashMovementRecord
                    {
                        Id = id,
                        MovementType = reader["MovementType"].ToString(),
                        PaymentMethod = reader["PaymentMethod"].ToString(),
                        Amount = Convert.ToDecimal(reader["Amount"]),
                        ReferenceNumber = reader["ReferenceNumber"] == DBNull.Value ? null : reader["ReferenceNumber"].ToString(),
                        Description = reader["Description"] == DBNull.Value ? null : reader["Description"].ToString(),
                        AccountCode = reader["AccountCode"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AccountCode"]),
                        MovementDate = reader["MovementDate"].ToString(),
                        LinkedMovementId = reader["LinkedMovementId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["LinkedMovementId"])
                    };
                }
            }
        }

        // internal (مش private) عشان باقي الـ Repositories (Maintenance/Attendance) تقدر
        // تعدّل رصيد وسيلة دفع من غير ما تكرر نفس الـ SELECT/UPDATE لجدول PaymentMethodBalances
        internal static decimal GetBalanceInTransaction(SqliteConnection conn, SqliteTransaction transaction, string method)
        {
            using (SqliteCommand cmd = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Method", method);
                var res = cmd.ExecuteScalar();
                return res != null ? Convert.ToDecimal(res) : 0;
            }
        }

        internal static void SetBalanceInTransaction(SqliteConnection conn, SqliteTransaction transaction, string method, decimal newBalance)
        {
            using (SqliteCommand cmd = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = @Balance WHERE PaymentMethod = @Method", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Balance", newBalance);
                cmd.Parameters.AddWithValue("@Method", method);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
