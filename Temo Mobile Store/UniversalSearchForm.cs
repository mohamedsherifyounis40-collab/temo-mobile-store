using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // UniversalSearchForm: نسخة مستقلة من نظام "البحث الشامل" (Ctrl+F) الموجود
    // في Form1.cs. بيدور في المنتجات، العملاء، الموردين، الصيانة، والمبيعات
    // مرة واحدة، وبيرجّع للمستخدم مفتاح الصفحة اللي يفتحها في MainShell.
    // ==========================================================================
    public class UniversalSearchForm : Form
    {
        private enum SearchResultKind { Product, Customer, Supplier, Maintenance, Sale }

        private class SearchResultItem
        {
            public SearchResultKind Kind;
            public string Type;
            public string Title;
            public string Subtitle;
        }

        private static readonly Color ColorPrimary = Color.FromArgb(26, 43, 76);
        private Guna2TextBox txtSearch;
        private ListView lvResults;
        private List<SearchResultItem> currentResults = new List<SearchResultItem>();

        public string SelectedPageKey { get; private set; } = null;

        public UniversalSearchForm()
        {
            this.Text = "البحث الشامل 🔍";
            this.Size = new Size(650, 520);
            this.StartPosition = FormStartPosition.CenterParent;
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(245, 246, 250);

            BuildUI();
        }

        private void BuildUI()
        {
            Label lblTitle = new Label { Text = "🔍 دوّر في المنتجات، العملاء، الموردين، الصيانة، والمبيعات مرة واحدة", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = ColorPrimary };

            txtSearch = new Guna2TextBox { Location = new Point(20, 45), Width = 590, BorderRadius = 8, FillColor = Color.White, PlaceholderText = "اكتب اسم، باركود، رقم تليفون..." };
            txtSearch.TextChanged += (s, e) => RunSearch();
            txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter && lvResults.Items.Count > 0) { lvResults.Items[0].Selected = true; SelectCurrentAndClose(); } };

            lvResults = new ListView
            {
                Location = new Point(20, 85),
                Size = new Size(590, 370),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                Font = new Font("Segoe UI", 9.5F)
            };
            lvResults.Columns.Add("النوع", 90);
            lvResults.Columns.Add("العنوان", 220);
            lvResults.Columns.Add("تفاصيل", 260);
            lvResults.DoubleClick += (s, e) => SelectCurrentAndClose();
            lvResults.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) SelectCurrentAndClose(); };

            Guna2Button btnOpen = new Guna2Button { Text = "فتح ↩️", Location = new Point(20, 465), Width = 140, Height = 34, FillColor = Color.FromArgb(39, 174, 96), BorderRadius = 9 };
            btnOpen.Click += (s, e) => SelectCurrentAndClose();

            Guna2Button btnClose = new Guna2Button { Text = "إغلاق", Location = new Point(470, 465), Width = 140, Height = 34, FillColor = Color.FromArgb(236, 240, 241), ForeColor = ColorPrimary, BorderRadius = 9 };
            btnClose.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.AddRange(new Control[] { lblTitle, txtSearch, lvResults, btnOpen, btnClose });
            this.AcceptButton = null;

            txtSearch.Focus();
        }

        // بيدور في كل الجداول المهمة مرة واحدة: المنتجات، العملاء، الموردين، الصيانة، والمبيعات
        private void RunSearch()
        {
            currentResults.Clear();
            lvResults.Items.Clear();

            string term = txtSearch.Text.Trim();
            if (term.Length < 2) return;

            string like = $"%{term}%";

            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                try
                {
                    conn.Open();

                    using (SqliteCommand cmd = new SqliteCommand("SELECT Barcode, ProductName, Quantity FROM Products WHERE ProductName LIKE @q OR Barcode LIKE @q LIMIT 15", conn))
                    {
                        cmd.Parameters.AddWithValue("@q", like);
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                currentResults.Add(new SearchResultItem { Kind = SearchResultKind.Product, Type = "منتج 📦", Title = reader["ProductName"].ToString(), Subtitle = $"باركود: {reader["Barcode"]} | الكمية: {reader["Quantity"]}" });
                        }
                    }

                    using (SqliteCommand cmd = new SqliteCommand("SELECT CustomerName, Phone FROM Customers WHERE CustomerName LIKE @q OR Phone LIKE @q LIMIT 15", conn))
                    {
                        cmd.Parameters.AddWithValue("@q", like);
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                currentResults.Add(new SearchResultItem { Kind = SearchResultKind.Customer, Type = "عميل 👤", Title = reader["CustomerName"].ToString(), Subtitle = $"تليفون: {reader["Phone"]}" });
                        }
                    }

                    using (SqliteCommand cmd = new SqliteCommand("SELECT SupplierName, Phone FROM Suppliers WHERE SupplierName LIKE @q OR Phone LIKE @q LIMIT 15", conn))
                    {
                        cmd.Parameters.AddWithValue("@q", like);
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                currentResults.Add(new SearchResultItem { Kind = SearchResultKind.Supplier, Type = "مورد 🚚", Title = reader["SupplierName"].ToString(), Subtitle = $"تليفون: {reader["Phone"]}" });
                        }
                    }

                    using (SqliteCommand cmd = new SqliteCommand("SELECT CustomerName, CustomerPhone, DeviceInfo, Status FROM MaintenanceTickets WHERE CustomerName LIKE @q OR CustomerPhone LIKE @q OR DeviceInfo LIKE @q LIMIT 15", conn))
                    {
                        cmd.Parameters.AddWithValue("@q", like);
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                currentResults.Add(new SearchResultItem { Kind = SearchResultKind.Maintenance, Type = "صيانة 🔧", Title = $"{reader["CustomerName"]} - {reader["DeviceInfo"]}", Subtitle = $"الحالة: {reader["Status"]} | {reader["CustomerPhone"]}" });
                        }
                    }

                    using (SqliteCommand cmd = new SqliteCommand("SELECT ProductName, Total, SaleDate, Barcode FROM Sales WHERE ProductName LIKE @q OR Barcode LIKE @q ORDER BY SaleID DESC LIMIT 15", conn))
                    {
                        cmd.Parameters.AddWithValue("@q", like);
                        using (SqliteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                currentResults.Add(new SearchResultItem { Kind = SearchResultKind.Sale, Type = "عملية بيع 🧾", Title = reader["ProductName"].ToString(), Subtitle = $"الإجمالي: {reader["Total"]} ج.م | {reader["SaleDate"]}" });
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }

            foreach (var r in currentResults)
            {
                var lvi = new ListViewItem(r.Type);
                lvi.SubItems.Add(r.Title);
                lvi.SubItems.Add(r.Subtitle);
                lvResults.Items.Add(lvi);
            }
        }

        private void SelectCurrentAndClose()
        {
            if (lvResults.SelectedIndices.Count == 0) return;
            int idx = lvResults.SelectedIndices[0];
            if (idx < 0 || idx >= currentResults.Count) return;

            var result = currentResults[idx];
            switch (result.Kind)
            {
                case SearchResultKind.Product: SelectedPageKey = "Inventory"; break;
                case SearchResultKind.Customer: SelectedPageKey = "Customers"; break;
                case SearchResultKind.Supplier: SelectedPageKey = "Suppliers"; break;
                case SearchResultKind.Maintenance: SelectedPageKey = "Maintenance"; break;
                case SearchResultKind.Sale: SelectedPageKey = "Sales"; break;
                default: SelectedPageKey = null; break;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
