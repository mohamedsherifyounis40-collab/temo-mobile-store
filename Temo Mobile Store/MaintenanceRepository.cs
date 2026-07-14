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

    }
}
