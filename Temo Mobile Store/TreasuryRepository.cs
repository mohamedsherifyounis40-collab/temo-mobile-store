using System;
using System.Data;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    // بتترمي لما حركة "صرف" تتعدى الرصيد المتاح في وسيلة الدفع - الشاشة بتمسكها
    // وتعرض رسالة ودّية بدل ما تسيب استثناء عام يوصل للمستخدم
    public class InsufficientBalanceException : Exception
    {
        public string PaymentMethod { get; }
        public decimal AvailableBalance { get; }

        public InsufficientBalanceException(string paymentMethod, decimal availableBalance)
            : base($"الرصيد المتاح في \"{paymentMethod}\" هو {availableBalance} فقط، لا يمكن صرف مبلغ أكبر منه.")
        {
            PaymentMethod = paymentMethod;
            AvailableBalance = availableBalance;
        }
    }

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
    }

    // ==========================================================================
    // TreasuryRepository: كل الوصول لقاعدة البيانات الخاص بشاشة الخزينة (مصروفات
    // عمومية + حركات قبض/صرف وأرصدة وسائل الدفع) في مكان واحد، بدل ما يكون
    // متوزع جوه event handlers الشاشة. TreasuryPageControl بقى مسؤول بس عن
    // الواجهة، والريبوزيتوري ده مسؤول عن الداتا والقواعد المالية (زي منع الصرف
    // من غير رصيد كافي).
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

        public static void AddExpense(int accountCode, decimal amount)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("INSERT INTO Expenses (AccountCode, Amount) VALUES (@AccountCode, @Amount)", conn))
                {
                    cmd.Parameters.AddWithValue("@AccountCode", accountCode);
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void UpdateExpense(int expenseId, int accountCode, decimal amount)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("UPDATE Expenses SET AccountCode = @AccountCode, Amount = @Amount WHERE ExpenseID = @ExpenseID", conn))
                {
                    cmd.Parameters.AddWithValue("@AccountCode", accountCode);
                    cmd.Parameters.AddWithValue("@Amount", amount);
                    cmd.Parameters.AddWithValue("@ExpenseID", expenseId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void DeleteExpense(int expenseId)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("DELETE FROM Expenses WHERE ExpenseID = @ExpenseID", conn))
                {
                    cmd.Parameters.AddWithValue("@ExpenseID", expenseId);
                    cmd.ExecuteNonQuery();
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
                new DataColumn("التاريخ والوقت ⏰")
            });

            string query = @"SELECT E.ExpenseID, E.AccountCode, A.AccountName, E.Amount, E.ExpenseDate
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
                        dt.Rows.Add(reader["ExpenseID"], reader["AccountCode"], reader["AccountName"], reader["Amount"], reader["ExpenseDate"]);
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

        public static void AddMovement(string type, string method, decimal amount, string reference, string description, int? accountCode)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        decimal currentBalance = GetBalanceInTransaction(conn, transaction, method);

                        if (type == "صرف" && amount > currentBalance)
                            throw new InsufficientBalanceException(method, currentBalance);

                        using (SqliteCommand cmdInsert = new SqliteCommand(
                            "INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode) VALUES (@Date, @Type, @Method, @Amount, @Ref, @Desc, @CreatedAt, @AccountCode)", conn, transaction))
                        {
                            cmdInsert.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                            cmdInsert.Parameters.AddWithValue("@Type", type);
                            cmdInsert.Parameters.AddWithValue("@Method", method);
                            cmdInsert.Parameters.AddWithValue("@Amount", amount);
                            cmdInsert.Parameters.AddWithValue("@Ref", reference);
                            cmdInsert.Parameters.AddWithValue("@Desc", description);
                            cmdInsert.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmdInsert.Parameters.AddWithValue("@AccountCode", (object)accountCode ?? DBNull.Value);
                            cmdInsert.ExecuteNonQuery();
                        }

                        decimal newBalance = type == "قبض" ? currentBalance + amount : currentBalance - amount;
                        SetBalanceInTransaction(conn, transaction, method, newBalance);

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

        public static CashMovementRecord GetMovementById(int id)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                return GetMovementInTransaction(conn, null, id);
            }
        }

        public static void UpdateMovement(int id, string newType, string newMethod, decimal newAmount, string reference, string description, int? accountCode)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        CashMovementRecord old = GetMovementInTransaction(conn, transaction, id)
                            ?? throw new InvalidOperationException("لم يتم العثور على الحركة.");

                        decimal oldMethodBalance = GetBalanceInTransaction(conn, transaction, old.PaymentMethod);
                        decimal revertedOldBalance = old.MovementType == "قبض" ? oldMethodBalance - old.Amount : oldMethodBalance + old.Amount;

                        decimal newMethodBalance = newMethod == old.PaymentMethod
                            ? revertedOldBalance
                            : GetBalanceInTransaction(conn, transaction, newMethod);

                        if (newType == "صرف" && newAmount > newMethodBalance)
                            throw new InsufficientBalanceException(newMethod, newMethodBalance);

                        SetBalanceInTransaction(conn, transaction, old.PaymentMethod, revertedOldBalance);

                        decimal finalNewBalance = newType == "قبض" ? newMethodBalance + newAmount : newMethodBalance - newAmount;
                        SetBalanceInTransaction(conn, transaction, newMethod, finalNewBalance);

                        using (SqliteCommand cmd = new SqliteCommand(
                            "UPDATE CashMovements SET MovementType = @Type, PaymentMethod = @Method, Amount = @Amount, ReferenceNumber = @Ref, Description = @Desc, AccountCode = @AccountCode WHERE Id = @Id", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Type", newType);
                            cmd.Parameters.AddWithValue("@Method", newMethod);
                            cmd.Parameters.AddWithValue("@Amount", newAmount);
                            cmd.Parameters.AddWithValue("@Ref", reference);
                            cmd.Parameters.AddWithValue("@Desc", description);
                            cmd.Parameters.AddWithValue("@AccountCode", (object)accountCode ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Id", id);
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

        public static void CancelMovement(int id, string type, string method, decimal amount)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        decimal currentBalance = GetBalanceInTransaction(conn, transaction, method);
                        decimal newBalance = type == "قبض" ? currentBalance - amount : currentBalance + amount;
                        SetBalanceInTransaction(conn, transaction, method, newBalance);

                        using (SqliteCommand cmdDelete = new SqliteCommand("DELETE FROM CashMovements WHERE Id = @Id", conn, transaction))
                        {
                            cmdDelete.Parameters.AddWithValue("@Id", id);
                            cmdDelete.ExecuteNonQuery();
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
            using (SqliteCommand cmd = new SqliteCommand("SELECT MovementType, PaymentMethod, Amount, ReferenceNumber, Description, AccountCode, MovementDate FROM CashMovements WHERE Id = @Id", conn, transaction))
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
                        MovementDate = reader["MovementDate"].ToString()
                    };
                }
            }
        }

        private static decimal GetBalanceInTransaction(SqliteConnection conn, SqliteTransaction transaction, string method)
        {
            using (SqliteCommand cmd = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Method", method);
                var res = cmd.ExecuteScalar();
                return res != null ? Convert.ToDecimal(res) : 0;
            }
        }

        private static void SetBalanceInTransaction(SqliteConnection conn, SqliteTransaction transaction, string method, decimal newBalance)
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
