using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;

namespace TemoStore.Data.Repositories
{
    public class AccountRepository : IAccountRepository
    {
        private readonly SqliteConnection _conn;
        private readonly SqliteTransaction _tx;

        public AccountRepository(SqliteConnection conn, SqliteTransaction tx)
        {
            _conn = conn;
            _tx = tx;
        }

        public bool Exists(int accountCode)
        {
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM AccountsTree WHERE AccountCode = @Code", _conn, _tx);
            cmd.Parameters.AddWithValue("@Code", accountCode);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
    }
}
