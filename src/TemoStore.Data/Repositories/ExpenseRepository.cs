using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;

namespace TemoStore.Data.Repositories
{
    public class ExpenseRepository : IExpenseRepository
    {
        private readonly SqliteConnection _conn;
        private readonly SqliteTransaction _tx;

        public ExpenseRepository(SqliteConnection conn, SqliteTransaction tx)
        {
            _conn = conn;
            _tx = tx;
        }

        public int Insert(int accountCode, decimal amount, string paymentMethod)
        {
            using (var cmd = new SqliteCommand("INSERT INTO Expenses (AccountCode, Amount, PaymentMethod) VALUES (@AccountCode, @Amount, @Method)", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@AccountCode", accountCode);
                cmd.Parameters.AddWithValue("@Amount", amount);
                cmd.Parameters.AddWithValue("@Method", paymentMethod);
                cmd.ExecuteNonQuery();
            }
            using var cmdId = new SqliteCommand("SELECT last_insert_rowid();", _conn, _tx);
            return Convert.ToInt32(cmdId.ExecuteScalar());
        }

        public (int AccountCode, decimal Amount, string PaymentMethod)? GetById(int expenseId)
        {
            using var cmd = new SqliteCommand("SELECT AccountCode, Amount, PaymentMethod FROM Expenses WHERE ExpenseID = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", expenseId);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return (Convert.ToInt32(reader["AccountCode"]), Convert.ToDecimal(reader["Amount"]),
                reader["PaymentMethod"] == DBNull.Value ? "نقدي" : reader["PaymentMethod"].ToString()!);
        }

        public void Update(int expenseId, int accountCode, decimal amount, string paymentMethod)
        {
            using var cmd = new SqliteCommand("UPDATE Expenses SET AccountCode = @AccountCode, Amount = @Amount, PaymentMethod = @Method WHERE ExpenseID = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@AccountCode", accountCode);
            cmd.Parameters.AddWithValue("@Amount", amount);
            cmd.Parameters.AddWithValue("@Method", paymentMethod);
            cmd.Parameters.AddWithValue("@Id", expenseId);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int expenseId)
        {
            using var cmd = new SqliteCommand("DELETE FROM Expenses WHERE ExpenseID = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", expenseId);
            cmd.ExecuteNonQuery();
        }
    }
}
