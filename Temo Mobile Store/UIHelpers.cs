using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // UIHelpers: نقطة مركزية واحدة للحاجات اللي كانت متكررة (نسخ/لصق) في كل
    // شاشات PageControl - الألوان، تنسيق الجداول، قائمة وسائل الدفع، فحص إقفال
    // اليوم، وتحميل بيانات إعدادات المحل. أي تعديل هنا بينعكس على كل الشاشات.
    // ==========================================================================
    public static class UIHelpers
    {
        public static readonly Color ColorPrimary = Color.FromArgb(26, 43, 76);
        public static readonly Color ColorSuccess = Color.FromArgb(39, 174, 96);
        public static readonly Color ColorDanger = Color.FromArgb(231, 76, 60);
        public static readonly Color ColorWarning = Color.FromArgb(243, 156, 18);
        public static readonly Color ColorNeutral = Color.FromArgb(236, 240, 241);
        public static readonly Color ColorBackground = Color.FromArgb(245, 246, 250);

        public static readonly string[] PaymentMethods =
            { "نقدي", "فوري", "أمان", "سهولة", "فودافون كاش", "إنستاباي" };

        public static void StyleDataGridView(DataGridView dgv)
        {
            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = ColorPrimary;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            dgv.ColumnHeadersHeight = 40;
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dgv.DefaultCellStyle.BackColor = Color.White;
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(200, 210, 230);
            dgv.DefaultCellStyle.SelectionForeColor = ColorPrimary;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = ColorBackground;
            dgv.GridColor = Color.FromArgb(230, 230, 230);
            dgv.RowTemplate.Height = 32;
            dgv.BorderStyle = BorderStyle.None;
            dgv.BackgroundColor = Color.White;
            dgv.RowHeadersVisible = true;
            dgv.RowHeadersWidth = 40;
            dgv.RowHeadersDefaultCellStyle.BackColor = ColorPrimary;
            dgv.RowHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.RowHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            dgv.RowHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.CellPainting += (s, e) =>
            {
                if (e.ColumnIndex == -1)
                {
                    e.PaintBackground(e.ClipBounds, true);
                    var rowNumber = e.RowIndex == -1 ? "م" : (e.RowIndex + 1).ToString();
                    TextRenderer.DrawText(e.Graphics, rowNumber, new Font("Segoe UI", 9, FontStyle.Bold), e.CellBounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    e.Handled = true;
                }
            };
        }

        // فحص هل تاريخ معين تم إقفاله بالفعل لوسيلة دفع معينة (افتراضيًا "نقدي" زي السلوك الأصلي)
        public static bool IsDateClosed(DateTime date, string paymentMethod = "نقدي")
        {
            string dateStr = date.ToString("yyyy-MM-dd");
            using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
            {
                conn.Open();
                using (SqliteCommand cmd = new SqliteCommand("SELECT COUNT(*) FROM DailyClosures WHERE ClosureDate = @Date AND PaymentMethod = @Method", conn))
                {
                    cmd.Parameters.AddWithValue("@Date", dateStr);
                    cmd.Parameters.AddWithValue("@Method", paymentMethod);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        public static bool IsTodayClosed(string paymentMethod = "نقدي") => IsDateClosed(DateTime.Now, paymentMethod);

        // بيانات المتجر (اسم/تليفون/عنوان/شعار) - مستخدمة في طباعة الفاتورة وشاشة الإعدادات
        public static void LoadStoreSettings(out string storeName, out string phone, out string address, out byte[] logo)
        {
            storeName = "Temo Mobile Store";
            phone = "";
            address = "";
            logo = null;
            try
            {
                using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
                {
                    conn.Open();
                    using (SqliteCommand cmd = new SqliteCommand("SELECT StoreName, Phone, Address, LogoImage FROM StoreSettings WHERE Id = 1;", conn))
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            storeName = reader["StoreName"] == DBNull.Value ? "" : reader["StoreName"].ToString();
                            phone = reader["Phone"] == DBNull.Value ? "" : reader["Phone"].ToString();
                            address = reader["Address"] == DBNull.Value ? "" : reader["Address"].ToString();
                            logo = reader["LogoImage"] == DBNull.Value ? null : (byte[])reader["LogoImage"];
                        }
                    }
                }
            }
            catch
            {
                // لو حصل أي خطأ، الشاشة تفضل شغالة بالقيم الافتراضية (اسم المحل الافتراضي بس)
            }
        }
    }
}
