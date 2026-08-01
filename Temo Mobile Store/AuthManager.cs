using System;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace Temo_Mobile_Store
{
    public static class AuthManager
    {
        // مسار قاعدة البيانات - بيدوّر جنب ملف البرنامج نفسه، مش على مسار ثابت، عشان يشتغل على أي جهاز
        public static readonly string ConnectionString = $"Data Source={System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TemoStoreDB.db")};";

        public static string CurrentUsername { get; private set; }
        public static string CurrentRole { get; private set; } // "Admin" أو "Employee"

        public static bool IsAdmin => CurrentRole == "Admin";

        // بيمسح المستخدم الحالي عشان نرجع لشاشة تسجيل الدخول (زرار Logout في الـ Sidebar)
        public static void Logout()
        {
            CurrentUsername = null;
            CurrentRole = null;
        }

        // بيعمل جدول المستخدمين لو مش موجود، وبيزرع حساب أدمن افتراضي أول مرة بس
        public static void EnsureUsersTableExists()
        {
            using (SqliteConnection conn = new SqliteConnection(ConnectionString))
            {
                conn.Open();
                string createTable = @"CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE,
                    PasswordHash TEXT NOT NULL,
                    Role TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                );";
                using (SqliteCommand cmd = new SqliteCommand(createTable, conn)) cmd.ExecuteNonQuery();

                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM Users", conn))
                {
                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    if (count == 0)
                    {
                        // أول تشغيل للبرنامج: بينشئ حساب أدمن افتراضي
                        // يوزر: admin | باسورد: admin123 - غيّرها فورًا من شاشة إدارة المستخدمين بعد أول دخول
                        using (SqliteCommand cmdInsert = new SqliteCommand(
                            "INSERT INTO Users (Username, PasswordHash, Role, CreatedAt) VALUES (@U, @P, @R, @C)", conn))
                        {
                            cmdInsert.Parameters.AddWithValue("@U", "admin");
                            cmdInsert.Parameters.AddWithValue("@P", HashPassword("admin123"));
                            cmdInsert.Parameters.AddWithValue("@R", "Admin");
                            cmdInsert.Parameters.AddWithValue("@C", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmdInsert.ExecuteNonQuery();    
                        }
                    }
                }
            }
        }

        private const int SaltSizeBytes = 16;
        private const int HashSizeBytes = 32;
        private const int Pbkdf2Iterations = 100_000;

        // هاش آمن (PBKDF2 + Salt عشوائي لكل مستخدم) - الفورمات: salt:hash:iterations
        public static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, HashSizeBytes);
            return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}:{Pbkdf2Iterations}";
        }

        // الفورمات القديم (SHA256 بدون Salt) - محتفظين بيه بس عشان نتحقق من حسابات
        // اتعملت قبل تحديث الأمان ده، ونرقّيها تلقائيًا لأول مرة تسجل دخول بنجاح
        private static string HashPasswordLegacySha256(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash)) return false;

            if (storedHash.Contains(':'))
            {
                string[] parts = storedHash.Split(':');
                if (parts.Length != 3) return false;
                byte[] salt = Convert.FromBase64String(parts[0]);
                byte[] expectedHash = Convert.FromBase64String(parts[1]);
                int iterations = int.Parse(parts[2]);
                byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }

            return storedHash == HashPasswordLegacySha256(password);
        }

        // بيرجع الدور (Role) لو بيانات الدخول صح، أو null لو غلط
        public static string TryLogin(string username, string password)
        {
            string trimmedUsername = username.Trim();
            using (SqliteConnection conn = new SqliteConnection(ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT PasswordHash, Role FROM Users WHERE Username = @U", conn))
                {
                    cmd.Parameters.AddWithValue("@U", trimmedUsername);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string storedHash = reader["PasswordHash"].ToString();
                            string role = reader["Role"].ToString();
                            if (VerifyPassword(password, storedHash))
                            {
                                CurrentUsername = trimmedUsername;
                                CurrentRole = role;

                                // ترقية تلقائية من الفورمات القديم (بدون Salt) للفورمات الجديد الأأمن
                                if (!storedHash.Contains(':'))
                                    UpgradePasswordHash(trimmedUsername, password);

                                return role;
                            }
                        }
                    }
                }
            }
            return null;
        }

        private static void UpgradePasswordHash(string username, string password)
        {
            try
            {
                using (SqliteConnection conn = new SqliteConnection(ConnectionString))
                {
                    conn.Open();
                    using (SqliteCommand cmd = new SqliteCommand("UPDATE Users SET PasswordHash = @P WHERE Username = @U", conn))
                    {
                        cmd.Parameters.AddWithValue("@P", HashPassword(password));
                        cmd.Parameters.AddWithValue("@U", username);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        // بيتأكد هل تسجيل الدخول ده لسه بيستخدم بيانات الأدمن الافتراضية (admin/admin123)
        // - مستخدم عشان نجبره يغيّر الباسورد قبل ما يدخل البرنامج
        public static bool IsDefaultAdminLogin(string username, string password) =>
            username.Trim().Equals("admin", StringComparison.OrdinalIgnoreCase) && password == "admin123";

        public static void ChangePassword(string username, string newPassword)
        {
            using (SqliteConnection conn = new SqliteConnection(ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("UPDATE Users SET PasswordHash = @P WHERE Username = @U", conn))
                {
                    cmd.Parameters.AddWithValue("@P", HashPassword(newPassword));
                    cmd.Parameters.AddWithValue("@U", username.Trim());
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
