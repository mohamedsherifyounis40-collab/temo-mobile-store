using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // CustomersRepository: كل الوصول لقاعدة البيانات الخاص بشاشة العملاء -
    // بيانات العملاء، كشف الحساب، وتحصيل دين من عميل.
    // ==========================================================================
    public static class CustomersRepository
    {
        public static DataTable GetCollectCustomerCombo()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("CustomerId", typeof(int)), new DataColumn("CustomerName") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT CustomerId, CustomerName FROM Customers ORDER BY CustomerName", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        dt.Rows.Add(Convert.ToInt32(reader["CustomerId"]), reader["CustomerName"].ToString());
                }
            }
            return dt;
        }

        public static DataTable GetCustomersWithBalances()
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("CustomerId"), new DataColumn("اسم العميل"), new DataColumn("التليفون"), new DataColumn("إجمالي المبيعات بالآجل"), new DataColumn("إجمالي المحصّل"), new DataColumn("المتبقي عليه") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                var customers = new List<(int id, string name, string phone)>();
                using (SqliteCommand cmd = new SqliteCommand("SELECT CustomerId, CustomerName, Phone FROM Customers ORDER BY CustomerName", conn))
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        customers.Add((Convert.ToInt32(reader["CustomerId"]), reader["CustomerName"].ToString(), reader["Phone"]?.ToString()));
                }

                foreach (var cust in customers)
                {
                    decimal totalCredit = 0, totalCollected = 0;
                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Total) FROM Sales WHERE CustomerId = @Id AND PaymentType = 'Credit'", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", cust.id);
                        var res = cmd.ExecuteScalar();
                        totalCredit = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                    }
                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM CashMovements WHERE CustomerId = @Id AND MovementType = 'قبض'", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", cust.id);
                        var res = cmd.ExecuteScalar();
                        totalCollected = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                    }

                    dt.Rows.Add(cust.id, cust.name, cust.phone, totalCredit.ToString("N2"), totalCollected.ToString("N2"), (totalCredit - totalCollected).ToString("N2"));
                }
            }
            return dt;
        }

        public static DataTable GetCustomerStatement(int customerId)
        {
            DataTable dt = new DataTable();
            dt.Columns.AddRange(new DataColumn[] { new DataColumn("التاريخ"), new DataColumn("النوع"), new DataColumn("التفاصيل"), new DataColumn("المبلغ") });

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT SaleDate, ProductName, Total FROM Sales WHERE CustomerId = @Id AND PaymentType = 'Credit' ORDER BY SaleDate", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", customerId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            dt.Rows.Add(reader["SaleDate"], "بيع آجل", reader["ProductName"], reader["Total"]);
                    }
                }
                // بيع نقدي مربوط بالعميل ده (اختياريًا، للمرجعية بس - راجع SalesPage.razor) -
                // ده بيع متسدد بالكامل وقت حصوله فمالوش أي أثر على "المتبقي عليه" في
                // GetCustomersWithBalances (اللي لسه بيحسب بس من البيع الآجل والتحصيل)،
                // هنا بس عشان يظهر في كشف الحساب كمرجع تاريخي لو حصلت أي مشكلة بعدين.
                using (SqliteCommand cmd = new SqliteCommand("SELECT SaleDate, ProductName, Total, PaymentMethod FROM Sales WHERE CustomerId = @Id AND PaymentType = 'Cash' ORDER BY SaleDate", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", customerId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            dt.Rows.Add(reader["SaleDate"], "بيع نقدي (متسدد)", reader["ProductName"] + " - " + reader["PaymentMethod"], reader["Total"]);
                    }
                }
                using (SqliteCommand cmd = new SqliteCommand("SELECT CreatedAt, Amount, PaymentMethod FROM CashMovements WHERE CustomerId = @Id AND MovementType = 'قبض' ORDER BY CreatedAt", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", customerId);
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            dt.Rows.Add(reader["CreatedAt"], "تحصيل", "تحصيل عبر " + reader["PaymentMethod"], "-" + Convert.ToDecimal(reader["Amount"]).ToString("N2"));
                    }
                }
            }
            return dt;
        }

        public static void AddCustomer(string name, string phone)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("INSERT INTO Customers (CustomerName, Phone, CreatedAt) VALUES (@N, @P, @C)", conn))
                {
                    cmd.Parameters.AddWithValue("@N", name);
                    cmd.Parameters.AddWithValue("@P", (object)phone ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@C", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void UpdateCustomer(int customerId, string name, string phone)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("UPDATE Customers SET CustomerName = @N, Phone = @P WHERE CustomerId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@N", name);
                    cmd.Parameters.AddWithValue("@P", (object)phone ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Id", customerId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static bool HasSales(int customerId)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM Sales WHERE CustomerId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", customerId);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        public static void DeleteCustomer(int customerId)
        {
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("DELETE FROM Customers WHERE CustomerId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", customerId);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
