using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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
            dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = ColorBackground;
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.None;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgv.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgv.GridColor = Color.White;
            dgv.RowTemplate.Height = 38;
            dgv.BorderStyle = BorderStyle.None;
            dgv.BackgroundColor = Color.White;
            dgv.RowHeadersVisible = true;
            dgv.RowHeadersWidth = 40;
            dgv.RowHeadersDefaultCellStyle.BackColor = ColorPrimary;
            dgv.RowHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.RowHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            dgv.RowHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            const int headerCornerRadius = 10;
            dgv.CellPainting += (s, e) =>
            {
                bool isRowHeaderCol = e.ColumnIndex == -1;
                bool isHeaderRow = e.RowIndex == -1;
                bool isRightToLeft = dgv.RightToLeft == RightToLeft.Yes;

                if (isRowHeaderCol && !isHeaderRow)
                {
                    e.PaintBackground(e.ClipBounds, true);
                    var rowNumber = (e.RowIndex + 1).ToString();
                    TextRenderer.DrawText(e.Graphics, rowNumber, new Font("Segoe UI", 9, FontStyle.Bold), e.CellBounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    e.Handled = true;
                    return;
                }

                if (!isHeaderRow) return;

                // الخلية دي طرف الشريط الكحلي بتاع عناوين الأعمدة؟ لو كده نديها زاوية علوية مدورة
                // (بدل الشكل المربع القديم) بدل ما نسيبها للرسم الافتراضي.
                bool roundLeft, roundRight;
                if (isRowHeaderCol)
                {
                    roundLeft = !isRightToLeft;
                    roundRight = isRightToLeft;
                }
                else
                {
                    var lastVisibleCol = dgv.Columns.GetLastColumn(DataGridViewElementStates.Visible, DataGridViewElementStates.None);
                    bool isOuterEnd = lastVisibleCol != null && e.ColumnIndex == lastVisibleCol.Index;
                    if (!isOuterEnd) return;
                    roundLeft = isRightToLeft;
                    roundRight = !isRightToLeft;
                }

                var r = e.CellBounds;
                int d = headerCornerRadius * 2;
                using (var path = new GraphicsPath())
                {
                    if (roundLeft) path.AddArc(r.X, r.Y, d, d, 180, 90);
                    path.AddLine(roundLeft ? r.X + headerCornerRadius : r.X, r.Y, roundRight ? r.Right - headerCornerRadius : r.Right, r.Y);
                    if (roundRight) path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                    path.AddLine(r.Right, roundRight ? r.Y + headerCornerRadius : r.Y, r.Right, r.Bottom);
                    path.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
                    path.AddLine(r.X, r.Bottom, r.X, roundLeft ? r.Y + headerCornerRadius : r.Y);
                    path.CloseFigure();

                    var oldSmoothing = e.Graphics.SmoothingMode;
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var brush = new SolidBrush(ColorPrimary))
                        e.Graphics.FillPath(brush, path);
                    e.Graphics.SmoothingMode = oldSmoothing;
                }

                var headerText = isRowHeaderCol ? "م" : dgv.Columns[e.ColumnIndex].HeaderText;
                TextRenderer.DrawText(e.Graphics, headerText, e.CellStyle.Font, e.CellBounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                e.Handled = true;
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

        // بيانات المتجر (اسم/تليفون/عنوان/شعار/واتساب) - مستخدمة في طباعة الفاتورة وشاشة الإعدادات
        public static void LoadStoreSettings(out string storeName, out string phone, out string address, out byte[] logo, out string whatsApp)
        {
            storeName = "Temo Mobile Store";
            phone = "";
            address = "";
            logo = null;
            whatsApp = "";
            try
            {
                using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
                {
                    conn.Open();
                    using (SqliteCommand cmd = new SqliteCommand("SELECT StoreName, Phone, Address, LogoImage, WhatsAppNumber FROM StoreSettings WHERE Id = 1;", conn))
                    using (SqliteDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            storeName = reader["StoreName"] == DBNull.Value ? "" : reader["StoreName"].ToString();
                            phone = reader["Phone"] == DBNull.Value ? "" : reader["Phone"].ToString();
                            address = reader["Address"] == DBNull.Value ? "" : reader["Address"].ToString();
                            logo = reader["LogoImage"] == DBNull.Value ? null : (byte[])reader["LogoImage"];
                            whatsApp = reader["WhatsAppNumber"] == DBNull.Value ? "" : reader["WhatsAppNumber"].ToString();
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
