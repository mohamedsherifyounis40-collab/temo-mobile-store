using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    public class NotificationItem
    {
        public required string Icon { get; set; }
        public required string Text { get; set; }
    }

    // ==========================================================================
    // NotificationsRepository: نفس منطق GetNotificationItems القديم اللي كان private
    // جوه MainShell.cs بالظبط (جرس الإشعارات) - قراءة بس، عشان تستخدمها شاشات
    // Blazor كلها من غير ما تتكرر كل استعلام في كل ملف.
    // ==========================================================================
    public static class NotificationsRepository
    {
        public static List<NotificationItem> GetNotificationItems()
        {
            var items = new List<NotificationItem>();

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                try
                {
                    using (SqliteCommand cmd = new SqliteCommand("SELECT ProductName, Quantity FROM Products WHERE Quantity <= 3 ORDER BY Quantity ASC LIMIT 8", conn))
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int qty = Convert.ToInt32(reader["Quantity"]);
                            string label = qty <= 0 ? "نفدت الكمية" : $"باقي {qty} بس";
                            items.Add(new NotificationItem { Icon = "📦", Text = $"{reader["ProductName"]} — {label}" });
                        }
                    }

                    using (SqliteCommand cmd = new SqliteCommand("SELECT CustomerName, DeviceInfo, ReceivedDate FROM MaintenanceTickets WHERE Status = 'جاهز للتسليم' ORDER BY ReceivedDate ASC LIMIT 8", conn))
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            items.Add(new NotificationItem { Icon = "🔧", Text = $"جهاز {reader["CustomerName"]} ({reader["DeviceInfo"]}) جاهز للتسليم" });
                    }

                    using (SqliteCommand cmd = new SqliteCommand(@"
                        SELECT C.CustomerName,
                               (COALESCE((SELECT SUM(Total) FROM Sales WHERE CustomerId = C.CustomerId AND PaymentType = 'Credit'), 0)
                                - COALESCE((SELECT SUM(Amount) FROM CashMovements WHERE CustomerId = C.CustomerId AND MovementType = 'قبض'), 0)) AS Balance
                        FROM Customers C", conn))
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            decimal balance = Convert.ToDecimal(reader["Balance"]);
                            if (balance > 0)
                                items.Add(new NotificationItem { Icon = "💰", Text = $"{reader["CustomerName"]} عليه {balance:N2} ج.م" });
                        }
                    }
                }
                catch { /* الإشعارات مش لازم توقف باقي الشاشة لو حصل خطأ بسيط */ }
            }

            return items;
        }
    }
}
