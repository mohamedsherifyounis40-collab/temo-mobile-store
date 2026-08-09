using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;

namespace TemoStore.Data.Repositories
{
    public class JournalRepository : IJournalRepository
    {
        private readonly SqliteConnection _conn;
        private readonly SqliteTransaction _tx;

        public JournalRepository(SqliteConnection conn, SqliteTransaction tx)
        {
            _conn = conn;
            _tx = tx;
        }

        public int InsertEntry(string sourceType, int? sourceId, string? description, string createdBy)
        {
            using (var cmd = new SqliteCommand(
                "INSERT INTO JournalEntries (EntryDate, SourceType, SourceId, Description, CreatedAt, CreatedBy) VALUES (@Date, @Type, @SourceId, @Desc, @CreatedAt, @By)", _conn, _tx))
            {
                cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@Type", sourceType);
                cmd.Parameters.AddWithValue("@SourceId", (object?)sourceId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Desc", (object?)description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@By", createdBy);
                cmd.ExecuteNonQuery();
            }
            using var cmdId = new SqliteCommand("SELECT last_insert_rowid();", _conn, _tx);
            return Convert.ToInt32(cmdId.ExecuteScalar());
        }

        public void InsertLine(int journalEntryId, int accountCode, decimal debit, decimal credit)
        {
            using var cmd = new SqliteCommand(
                "INSERT INTO JournalLines (JournalEntryId, AccountCode, Debit, Credit) VALUES (@EntryId, @Code, @Debit, @Credit)", _conn, _tx);
            cmd.Parameters.AddWithValue("@EntryId", journalEntryId);
            cmd.Parameters.AddWithValue("@Code", accountCode);
            cmd.Parameters.AddWithValue("@Debit", debit);
            cmd.Parameters.AddWithValue("@Credit", credit);
            cmd.ExecuteNonQuery();
        }

        public decimal GetAccountBalance(int accountCode)
        {
            using var cmd = new SqliteCommand(
                "SELECT COALESCE(SUM(Debit), 0) - COALESCE(SUM(Credit), 0) FROM JournalLines WHERE AccountCode = @Code", _conn, _tx);
            cmd.Parameters.AddWithValue("@Code", accountCode);
            return Convert.ToDecimal(cmd.ExecuteScalar());
        }
    }
}
