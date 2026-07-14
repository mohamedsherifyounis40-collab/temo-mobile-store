using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;
using TemoStore.Core.Entities;

namespace TemoStore.Data.Repositories
{
    public class CustomerRepository : ICustomerRepository
    {
        private readonly SqliteConnection _conn;
        private readonly SqliteTransaction _tx;

        public CustomerRepository(SqliteConnection conn, SqliteTransaction tx)
        {
            _conn = conn;
            _tx = tx;
        }

        public bool Exists(int customerId)
        {
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM Customers WHERE CustomerId = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", customerId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public decimal GetBalance(int customerId)
        {
            decimal totalCredit = 0, totalCollected = 0;
            using (var cmd = new SqliteCommand("SELECT SUM(Total) FROM Sales WHERE CustomerId = @Id AND PaymentType = 'Credit'", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@Id", customerId);
                var res = cmd.ExecuteScalar();
                totalCredit = res != null && res != DBNull.Value ? Convert.ToDecimal(res) : 0;
            }
            using (var cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE CustomerId = @Id AND MovementType = 'قبض'", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@Id", customerId);
                var res = cmd.ExecuteScalar();
                totalCollected = res != null && res != DBNull.Value ? Convert.ToDecimal(res) : 0;
            }
            return totalCredit - totalCollected;
        }

        public IReadOnlyList<CustomerStatementLine> GetStatement(int customerId)
        {
            var list = new List<CustomerStatementLine>();
            using (var cmd = new SqliteCommand("SELECT SaleDate, ProductName, Total FROM Sales WHERE CustomerId = @Id AND PaymentType = 'Credit' ORDER BY SaleDate", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@Id", customerId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    list.Add(new CustomerStatementLine { Date = reader["SaleDate"].ToString()!, Type = "بيع آجل", Details = reader["ProductName"].ToString()!, Amount = Convert.ToDecimal(reader["Total"]) });
            }
            using (var cmd = new SqliteCommand("SELECT CreatedAt, Amount, PaymentMethod FROM CashMovements WHERE CustomerId = @Id AND MovementType = 'قبض' ORDER BY CreatedAt", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@Id", customerId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    list.Add(new CustomerStatementLine { Date = reader["CreatedAt"].ToString()!, Type = "تحصيل", Details = "تحصيل عبر " + reader["PaymentMethod"], Amount = -Convert.ToDecimal(reader["Amount"]) });
            }
            return list;
        }
    }
}
