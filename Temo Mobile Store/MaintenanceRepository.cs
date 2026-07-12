using System;
using System.Data;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // MaintenanceRepository: كل الوصول لقاعدة البيانات الخاص بشاشة الصيانة -
    // استلام جهاز، تحديث حالة تذكرة، وتسليم/تحصيل أجرة الصيانة.
    // ==========================================================================
    public static class MaintenanceRepository
    {
        public static DataTable GetMaintenanceTickets(string statusFilter)
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("TicketId"), new DataColumn("العميل"), new DataColumn("التليفون"), new DataColumn("الجهاز"),
                new DataColumn("العطل"), new DataColumn("تاريخ الاستلام"), new DataColumn("التقديري"), new DataColumn("الفعلي"), new DataColumn("الحالة"), new DataColumn("تاريخ التسليم") });

            string query = "SELECT * FROM MaintenanceTickets";
            if (statusFilter != "الكل") query += " WHERE Status = @Status";
            query += " ORDER BY ReceivedDate DESC";

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(query, conn))
                {
                    if (statusFilter != "الكل") cmd.Parameters.AddWithValue("@Status", statusFilter);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            dt.Rows.Add(reader["TicketId"], reader["CustomerName"], reader["CustomerPhone"], reader["DeviceInfo"],
                                reader["IssueDescription"], reader["ReceivedDate"],
                                reader["EstimatedCost"] == DBNull.Value ? "" : Convert.ToDecimal(reader["EstimatedCost"]).ToString("N2"),
                                reader["ActualCost"] == DBNull.Value ? "" : Convert.ToDecimal(reader["ActualCost"]).ToString("N2"),
                                reader["Status"], reader["DeliveredDate"] == DBNull.Value ? "" : reader["DeliveredDate"]);
                        }
                    }
                }
            }
            return dt;
        }

        public static void ReceiveDevice(string customerName, string customerPhone, string deviceInfo, string issueDescription, decimal estimatedCost)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(
                    "INSERT INTO MaintenanceTickets (CustomerName, CustomerPhone, DeviceInfo, IssueDescription, ReceivedDate, EstimatedCost, Status) VALUES (@N, @P, @D, @I, @R, @E, 'مستلم')", conn))
                {
                    cmd.Parameters.AddWithValue("@N", customerName);
                    cmd.Parameters.AddWithValue("@P", (object)customerPhone ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@D", deviceInfo);
                    cmd.Parameters.AddWithValue("@I", (object)issueDescription ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@R", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@E", estimatedCost);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void UpdateStatus(int ticketId, string newStatus)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("UPDATE MaintenanceTickets SET Status = @S WHERE TicketId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@S", newStatus);
                    cmd.Parameters.AddWithValue("@Id", ticketId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // بيسلّم الجهاز، ويحصّل الأجرة (لو أكبر من صفر) ويزوّد رصيد وسيلة الدفع، جوه Transaction واحدة
        public static void DeliverDevice(int ticketId, decimal actualCost, string method)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string customerName = "";
                        using (SqliteCommand cmdGet = new SqliteCommand("SELECT CustomerName FROM MaintenanceTickets WHERE TicketId = @Id", conn, transaction))
                        {
                            cmdGet.Parameters.AddWithValue("@Id", ticketId);
                            var res = cmdGet.ExecuteScalar();
                            customerName = res?.ToString() ?? "";
                        }

                        using (SqliteCommand cmd = new SqliteCommand(
                            "UPDATE MaintenanceTickets SET Status = 'تم التسليم', ActualCost = @A, DeliveredDate = @D WHERE TicketId = @Id", conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@A", actualCost);
                            cmd.Parameters.AddWithValue("@D", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            cmd.Parameters.AddWithValue("@Id", ticketId);
                            cmd.ExecuteNonQuery();
                        }

                        if (actualCost > 0)
                        {
                            using (SqliteCommand cmd = new SqliteCommand(
                                "INSERT INTO CashMovements (MovementDate, MovementType, PaymentMethod, Amount, ReferenceNumber, Description, CreatedAt, AccountCode) VALUES (@Date, 'قبض', @Method, @Amount, @Ref, @Desc, @CreatedAt, 4200)", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd"));
                                cmd.Parameters.AddWithValue("@Method", method);
                                cmd.Parameters.AddWithValue("@Amount", actualCost);
                                cmd.Parameters.AddWithValue("@Ref", "تذكرة صيانة رقم " + ticketId);
                                cmd.Parameters.AddWithValue("@Desc", "أجرة صيانة - " + customerName);
                                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                                cmd.ExecuteNonQuery();
                            }

                            using (SqliteCommand cmd = new SqliteCommand("UPDATE PaymentMethodBalances SET CurrentBalance = CurrentBalance + @Amount WHERE PaymentMethod = @Method", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Amount", actualCost);
                                cmd.Parameters.AddWithValue("@Method", method);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}
