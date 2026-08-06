using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    public class GlobalSearchProductResult
    {
        public required string Barcode { get; set; }
        public required string ProductName { get; set; }
    }

    public class GlobalSearchCustomerResult
    {
        public int CustomerId { get; set; }
        public required string CustomerName { get; set; }
        public string Phone { get; set; } = "";
    }

    // ==========================================================================
    // GlobalSearchRepository: نفس منطق RunHeaderQuickSearch القديم بالظبط (البحث
    // السريع في الهيدر) - قراءة بس، عشان تستخدمها شاشات Blazor كلها.
    // ==========================================================================
    public static class GlobalSearchRepository
    {
        public static (List<GlobalSearchProductResult> Products, List<GlobalSearchCustomerResult> Customers) Search(string term)
        {
            var products = new List<GlobalSearchProductResult>();
            var customers = new List<GlobalSearchCustomerResult>();

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand(
                    "SELECT Barcode, ProductName FROM Products WHERE ProductName LIKE @q OR Barcode LIKE @q LIMIT 6", conn))
                {
                    cmd.Parameters.AddWithValue("@q", "%" + term + "%");
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            products.Add(new GlobalSearchProductResult { Barcode = reader["Barcode"].ToString() ?? "", ProductName = reader["ProductName"].ToString() ?? "" });
                    }
                }

                using (SqliteCommand cmd = new SqliteCommand(
                    "SELECT CustomerId, CustomerName, Phone FROM Customers WHERE CustomerName LIKE @q OR Phone LIKE @q LIMIT 6", conn))
                {
                    cmd.Parameters.AddWithValue("@q", "%" + term + "%");
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            customers.Add(new GlobalSearchCustomerResult
                            {
                                CustomerId = Convert.ToInt32(reader["CustomerId"]),
                                CustomerName = reader["CustomerName"].ToString() ?? "",
                                Phone = reader["Phone"] == DBNull.Value ? "" : reader["Phone"].ToString() ?? ""
                            });
                    }
                }
            }

            return (products, customers);
        }
    }
}
