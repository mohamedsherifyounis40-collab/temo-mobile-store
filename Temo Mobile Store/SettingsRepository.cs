using System;
using System.Data;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    public class UserRecord
    {
        public int Id;
        public string Username;
        public string Role;
    }

    // ==========================================================================
    // SettingsRepository: كل الوصول لقاعدة البيانات الخاص بشاشة الإعدادات -
    // بيانات المحل، نسخة احتياطية من قاعدة البيانات نفسها، وإدارة المستخدمين.
    // (عمليات ملفات النسخ الاحتياطي نفسها - مجلد/سحابة/حذف ملفات - مش وصول
    // لقاعدة بيانات، فضلة في SettingsPageControl زي ما هي).
    // ==========================================================================
    public static class SettingsRepository
    {
        public static void SaveStoreSettings(string name, string phone, string address, byte[] logo)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("UPDATE StoreSettings SET StoreName = @Name, Phone = @Phone, Address = @Address, LogoImage = @Logo WHERE Id = 1;", conn))
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Phone", phone);
                    cmd.Parameters.AddWithValue("@Address", address);
                    cmd.Parameters.AddWithValue("@Logo", (object)logo ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // بينسخ قاعدة البيانات كاملة لمسار معين (مستخدم في تصدير نسخة احتياطية)
        public static void BackupDatabaseTo(string destPath)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                string escapedPath = destPath.Replace("'", "''");
                using (SqliteCommand cmd = new SqliteCommand($"VACUUM INTO '{escapedPath}';", conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static DataTable GetUsersGrid()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("Id"), new DataColumn("اسم المستخدم"), new DataColumn("الدور"), new DataColumn("تاريخ الإنشاء") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT Id, Username, Role, CreatedAt FROM Users ORDER BY Id", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        dt.Rows.Add(reader["Id"], reader["Username"], reader["Role"], reader["CreatedAt"]);
                }
            }
            return dt;
        }

        public static bool UsernameExists(string username, int excludeId = -1)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                string query = excludeId >= 0
                    ? "SELECT COUNT(*) FROM Users WHERE Username = @U AND Id <> @Id"
                    : "SELECT COUNT(*) FROM Users WHERE Username = @U";
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@U", username);
                    if (excludeId >= 0) cmd.Parameters.AddWithValue("@Id", excludeId);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        public static UserRecord GetUser(int id)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT Username, Role FROM Users WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return null;
                        return new UserRecord { Id = id, Username = reader["Username"].ToString(), Role = reader["Role"].ToString() };
                    }
                }
            }
        }

        public static int CountAdmins()
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM Users WHERE Role = 'Admin'", conn))
                    return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public static void UpdateUser(int id, string username, string role, string newPasswordOrNull)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                if (string.IsNullOrEmpty(newPasswordOrNull))
                {
                    using (SqliteCommand cmd = new SqliteCommand("UPDATE Users SET Username = @U, Role = @R WHERE Id = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@U", username);
                        cmd.Parameters.AddWithValue("@R", role);
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    using (SqliteCommand cmd = new SqliteCommand("UPDATE Users SET Username = @U, Role = @R, PasswordHash = @P WHERE Id = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@U", username);
                        cmd.Parameters.AddWithValue("@R", role);
                        cmd.Parameters.AddWithValue("@P", AuthManager.HashPassword(newPasswordOrNull));
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public static void AddUser(string username, string password, string role)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("INSERT INTO Users (Username, PasswordHash, Role, CreatedAt) VALUES (@U, @P, @R, @C)", conn))
                {
                    cmd.Parameters.AddWithValue("@U", username);
                    cmd.Parameters.AddWithValue("@P", AuthManager.HashPassword(password));
                    cmd.Parameters.AddWithValue("@R", role);
                    cmd.Parameters.AddWithValue("@C", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void DeleteUser(int id)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("DELETE FROM Users WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
