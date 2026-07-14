using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;

namespace TemoStore.Data.Repositories
{
    public class BackupRepository : IBackupRepository
    {
        public void VacuumInto(string destPath)
        {
            using var conn = new SqliteConnection(DbConnectionFactory.ConnectionString);
            conn.Open();
            string escapedPath = destPath.Replace("'", "''");
            using var cmd = new SqliteCommand($"VACUUM INTO '{escapedPath}';", conn);
            cmd.ExecuteNonQuery();
        }
    }
}
