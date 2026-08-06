using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;
using TemoStore.Core.Entities;

namespace TemoStore.Data.Repositories
{
    public class ClosureRepository : IClosureRepository
    {
        private readonly SqliteConnection _conn;
        private readonly SqliteTransaction _tx;

        public ClosureRepository(SqliteConnection conn, SqliteTransaction tx)
        {
            _conn = conn;
            _tx = tx;
        }

        public decimal? GetLastActualClosingBalance(string paymentMethod)
        {
            using var cmd = new SqliteCommand(
                "SELECT ActualClosingBalance FROM DailyClosures WHERE PaymentMethod = @Method ORDER BY ClosureDate DESC LIMIT 1", _conn, _tx);
            cmd.Parameters.AddWithValue("@Method", paymentMethod);
            var res = cmd.ExecuteScalar();
            return res != null && res != DBNull.Value ? Convert.ToDecimal(res) : (decimal?)null;
        }

        public decimal GetTodayMovementsTotal(string paymentMethod, string movementType, DateTime date)
        {
            using var cmd = new SqliteCommand(
                "SELECT SUM(Amount) FROM CashMovements WHERE PaymentMethod = @Method AND MovementType = @Type AND MovementDate = @Date", _conn, _tx);
            cmd.Parameters.AddWithValue("@Method", paymentMethod);
            cmd.Parameters.AddWithValue("@Type", movementType);
            cmd.Parameters.AddWithValue("@Date", date.ToString("yyyy-MM-dd"));
            var res = cmd.ExecuteScalar();
            return res != null && res != DBNull.Value ? Convert.ToDecimal(res) : 0;
        }

        public decimal GetTodayExpensesTotal(DateTime date)
        {
            using var cmd = new SqliteCommand("SELECT SUM(Amount) FROM Expenses WHERE ExpenseDate LIKE @Date", _conn, _tx);
            cmd.Parameters.AddWithValue("@Date", date.ToString("yyyy-MM-dd") + "%");
            var res = cmd.ExecuteScalar();
            return res != null && res != DBNull.Value ? Convert.ToDecimal(res) : 0;
        }

        public int InsertClosure(DateTime date, string paymentMethod, decimal opening, decimal totalIn, decimal totalOut, decimal actual, DateTime closedAt)
        {
            using (var cmd = new SqliteCommand(@"INSERT INTO DailyClosures
                (ClosureDate, PaymentMethod, OpeningBalance, TotalIn, TotalOut, ExpectedClosingBalance, ActualClosingBalance, Difference, ClosedAt, AdjustmentMovementId)
                VALUES (@Date, @Method, @Opening, @TotalIn, @TotalOut, @Actual, @Actual, 0, @ClosedAt, NULL)", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@Date", date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@Method", paymentMethod);
                cmd.Parameters.AddWithValue("@Opening", opening);
                cmd.Parameters.AddWithValue("@TotalIn", totalIn);
                cmd.Parameters.AddWithValue("@TotalOut", totalOut);
                cmd.Parameters.AddWithValue("@Actual", actual);
                cmd.Parameters.AddWithValue("@ClosedAt", closedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.ExecuteNonQuery();
            }

            using var cmdId = new SqliteCommand("SELECT last_insert_rowid();", _conn, _tx);
            return Convert.ToInt32(cmdId.ExecuteScalar());
        }

        public void InsertDenominationLine(int closureId, decimal denominationValue, int count)
        {
            using var cmd = new SqliteCommand(
                "INSERT INTO CashDenominations (ClosureId, DenominationValue, DenominationCount, LineTotal) VALUES (@ClosureId, @Value, @Count, @LineTotal)", _conn, _tx);
            cmd.Parameters.AddWithValue("@ClosureId", closureId);
            cmd.Parameters.AddWithValue("@Value", denominationValue);
            cmd.Parameters.AddWithValue("@Count", count);
            cmd.Parameters.AddWithValue("@LineTotal", denominationValue * count);
            cmd.ExecuteNonQuery();
        }

        public IReadOnlyList<ClosureRow> GetClosuresForDate(DateTime date)
        {
            var rows = new List<ClosureRow>();
            using var cmd = new SqliteCommand(
                "SELECT Id, PaymentMethod, ExpectedClosingBalance, AdjustmentMovementId FROM DailyClosures WHERE ClosureDate = @Date", _conn, _tx);
            cmd.Parameters.AddWithValue("@Date", date.ToString("yyyy-MM-dd"));
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new ClosureRow
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    PaymentMethod = reader["PaymentMethod"].ToString()!,
                    ExpectedClosingBalance = Convert.ToDecimal(reader["ExpectedClosingBalance"]),
                    AdjustmentMovementId = reader["AdjustmentMovementId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AdjustmentMovementId"])
                });
            }
            return rows;
        }

        public void DeleteDenominationsForClosure(int closureId)
        {
            using var cmd = new SqliteCommand("DELETE FROM CashDenominations WHERE ClosureId = @Id", _conn, _tx);
            cmd.Parameters.AddWithValue("@Id", closureId);
            cmd.ExecuteNonQuery();
        }

        public void DeleteClosuresForDate(DateTime date)
        {
            using var cmd = new SqliteCommand("DELETE FROM DailyClosures WHERE ClosureDate = @Date", _conn, _tx);
            cmd.Parameters.AddWithValue("@Date", date.ToString("yyyy-MM-dd"));
            cmd.ExecuteNonQuery();
        }
    }
}
