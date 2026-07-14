using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;
using TemoStore.Core.Entities;

namespace TemoStore.Data.Repositories
{
    public class EmployeeRepository : IEmployeeRepository
    {
        private readonly SqliteConnection _conn;
        private readonly SqliteTransaction _tx;

        public EmployeeRepository(SqliteConnection conn, SqliteTransaction tx)
        {
            _conn = conn;
            _tx = tx;
        }

        public void Add(string fullName, string? phone, decimal monthlySalary, DateTime hireDate)
        {
            using var cmd = new SqliteCommand("INSERT INTO Employees (FullName, Phone, MonthlySalary, HireDate) VALUES (@N, @P, @S, @H)", _conn, _tx);
            cmd.Parameters.AddWithValue("@N", fullName);
            cmd.Parameters.AddWithValue("@P", (object?)phone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@S", monthlySalary);
            cmd.Parameters.AddWithValue("@H", hireDate.ToString("yyyy-MM-dd"));
            cmd.ExecuteNonQuery();
        }

        public void Update(int employeeId, string fullName, string? phone, decimal monthlySalary, DateTime hireDate)
        {
            using var cmd = new SqliteCommand("UPDATE Employees SET FullName = @N, Phone = @P, MonthlySalary = @S, HireDate = @H WHERE EmployeeId = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@N", fullName);
            cmd.Parameters.AddWithValue("@P", (object?)phone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@S", monthlySalary);
            cmd.Parameters.AddWithValue("@H", hireDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@Id", employeeId);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int employeeId)
        {
            using var cmd = new SqliteCommand("DELETE FROM Employees WHERE EmployeeId = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", employeeId);
            cmd.ExecuteNonQuery();
        }

        public IReadOnlyList<EmployeeRecord> GetAll()
        {
            var list = new List<EmployeeRecord>();
            using var cmd = new SqliteCommand("SELECT EmployeeId, FullName, MonthlySalary FROM Employees ORDER BY FullName", _conn, _tx);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new EmployeeRecord
                {
                    EmployeeId = Convert.ToInt32(reader["EmployeeId"]),
                    FullName = reader["FullName"].ToString()!,
                    MonthlySalary = Convert.ToDecimal(reader["MonthlySalary"])
                });
            }
            return list;
        }
    }
}
