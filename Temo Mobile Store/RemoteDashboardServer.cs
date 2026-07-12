using System;
using System.Net;
using System.Text;
using System.Threading;
using Microsoft.Data.Sqlite;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // RemoteDashboardServer: سيرفر ويب صغير جوه البرنامج نفسه، بيوري داشبورد
    // متابعة بسيط (مبيعات، مصروفات، أرصدة) من أي متصفح - محمي بباسورد الأدمن.
    //
    // ملاحظة أمان مهمة: الصفحة دي هتبقى متاحة لأي حد يعرف الرابط + الباسورد.
    // متشاركش الرابط أو الباسورد مع حد مش موثوق فيه.
    // ==========================================================================
    public static class RemoteDashboardServer
    {
        private static HttpListener listener;
        public const int Port = 5050;
        private static string LogPath => System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "remote_server_log.txt");

        public static void Start()
        {
            if (listener != null && listener.IsListening) return;

            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add($"http://+:{Port}/");
                listener.Start();

                Thread serverThread = new Thread(ListenLoop) { IsBackground = true };
                serverThread.Start();

                System.IO.File.WriteAllText(LogPath, "SUCCESS: Server started on port " + Port + " at " + DateTime.Now);
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText(LogPath, "FAILED: " + ex.GetType().Name + " - " + ex.Message);
            }
        }

        private static void ListenLoop()
        {
            while (listener != null && listener.IsListening)
            {
                try
                {
                    HttpListenerContext context = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
                }
                catch { }
            }
        }

        private static void HandleRequest(HttpListenerContext context)
        {
            try
            {
                if (!CheckAuth(context))
                {
                    context.Response.StatusCode = 401;
                    context.Response.AddHeader("WWW-Authenticate", "Basic realm=\"Temo Mobile Store\"");
                    context.Response.Close();
                    return;
                }

                string path = context.Request.Url.AbsolutePath;

                if (path == "/api/dashboard")
                    ServeJson(context, BuildDashboardJson());
                else if (path == "/manifest.json")
                    ServeText(context, ManifestJson, "application/json");
                else
                    ServeText(context, DashboardHtml, "text/html; charset=utf-8");
            }
            catch (Exception ex)
            {
                try
                {
                    context.Response.StatusCode = 500;
                    ServeText(context, "Server error: " + ex.Message, "text/plain");
                }
                catch { }
            }
        }

        // ==========================================================================
        // التحقق من اسم المستخدم وكلمة المرور (نفس بيانات دخول الأدمن في البرنامج)
        // ==========================================================================
        private static bool CheckAuth(HttpListenerContext context)
        {
            string authHeader = context.Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic ")) return false;

            try
            {
                string encoded = authHeader.Substring("Basic ".Length).Trim();
                string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                int sep = decoded.IndexOf(':');
                if (sep < 0) return false;

                string username = decoded.Substring(0, sep);
                string password = decoded.Substring(sep + 1);

                using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
                {
                    conn.Open();
                    using (SqliteCommand cmd = new SqliteCommand("SELECT PasswordHash FROM Users WHERE Username = @U AND Role = 'Admin'", conn))
                    {
                        cmd.Parameters.AddWithValue("@U", username);
                        var storedHash = cmd.ExecuteScalar();
                        return storedHash != null && AuthManager.VerifyPassword(password, storedHash.ToString());
                    }
                }
            }
            catch { return false; }
        }

        private static void ServeJson(HttpListenerContext context, string json)
        {
            ServeText(context, json, "application/json; charset=utf-8");
        }

        private static void ServeText(HttpListenerContext context, string text, string contentType)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }

        // ==========================================================================
        // بناء بيانات الداشبورد (نفس منطق كروت الرئيسية بالظبط) كـ JSON بسيط
        // ==========================================================================
        private static string BuildDashboardJson()
        {
            decimal todaySales = 0, todayCogs = 0, todayExpenses = 0;
            int invoiceCount = 0;
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            var balances = new System.Collections.Generic.List<(string Method, decimal Balance)>();
            decimal totalBalance = 0;

            try
            {
                using (SqliteConnection conn = new SqliteConnection(AuthManager.ConnectionString))
                {
                    conn.Open();

                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Total) AS T, SUM(CostPrice * QuantitySold) AS C, COUNT(*) AS N FROM Sales WHERE SaleDate LIKE @Today", conn))
                    {
                        cmd.Parameters.AddWithValue("@Today", today + "%");
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                todaySales = reader["T"] != DBNull.Value ? Convert.ToDecimal(reader["T"]) : 0;
                                todayCogs = reader["C"] != DBNull.Value ? Convert.ToDecimal(reader["C"]) : 0;
                                invoiceCount = reader["N"] != DBNull.Value ? Convert.ToInt32(reader["N"]) : 0;
                            }
                        }
                    }

                    using (SqliteCommand cmd = new SqliteCommand("SELECT SUM(Amount) FROM Expenses WHERE ExpenseDate LIKE @Today", conn))
                    {
                        cmd.Parameters.AddWithValue("@Today", today + "%");
                        var res = cmd.ExecuteScalar();
                        todayExpenses = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                    }

                    string[] methods = UIHelpers.PaymentMethods;
                    foreach (string method in methods)
                    {
                        using (SqliteCommand cmd = new SqliteCommand("SELECT CurrentBalance FROM PaymentMethodBalances WHERE PaymentMethod = @Method", conn))
                        {
                            cmd.Parameters.AddWithValue("@Method", method);
                            var res = cmd.ExecuteScalar();
                            decimal bal = (res != null && res != DBNull.Value) ? Convert.ToDecimal(res) : 0;
                            balances.Add((method, bal));
                            totalBalance += bal;
                        }
                    }
                }
            }
            catch { }

            decimal todayProfit = todaySales - todayCogs - todayExpenses;

            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"todaySales\":{todaySales.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.Append($"\"todayExpenses\":{todayExpenses.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.Append($"\"todayProfit\":{todayProfit.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.Append($"\"invoiceCount\":{invoiceCount},");
            sb.Append($"\"totalBalance\":{totalBalance.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
            sb.Append("\"balances\":[");
            for (int i = 0; i < balances.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("{\"method\":\"" + balances[i].Method + "\",\"balance\":" + balances[i].Balance.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");
            }
            sb.Append("],");
            sb.Append($"\"updatedAt\":\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\"");
            sb.Append("}");
            return sb.ToString();
        }

        private const string ManifestJson = @"{
            ""name"": ""Temo Mobile Store - متابعة"",
            ""short_name"": ""Temo متابعة"",
            ""start_url"": ""/"",
            ""display"": ""standalone"",
            ""background_color"": ""#1A2B4C"",
            ""theme_color"": ""#1A2B4C"",
            ""icons"": []
        }";

        // ==========================================================================
        // صفحة الويب نفسها (HTML + CSS + JS في ملف واحد)
        // ==========================================================================
        private const string DashboardHtml = @"<!DOCTYPE html>
<html lang=""ar"" dir=""rtl"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1, maximum-scale=1"">
<title>Temo Mobile Store - متابعة</title>
<link rel=""manifest"" href=""/manifest.json"">
<style>
  * { box-sizing: border-box; }
  body { margin:0; font-family: 'Segoe UI', Tahoma, Arial, sans-serif; background:#F5F6FA; color:#1A2B4C; }
  header { background:#1A2B4C; color:#fff; padding:18px 20px; text-align:center; }
  header h1 { margin:0; font-size:18px; }
  header p { margin:4px 0 0; font-size:12px; color:#9AA3B2; }
  .wrap { padding:16px; max-width:480px; margin:0 auto; }
  .grid { display:grid; grid-template-columns: 1fr 1fr; gap:12px; margin-bottom:16px; }
  .card { background:#fff; border-radius:14px; padding:14px; box-shadow:0 2px 8px rgba(0,0,0,0.06); }
  .card .label { font-size:12px; color:#5B6675; margin-bottom:6px; }
  .card .value { font-size:20px; font-weight:bold; }
  .balances { background:#fff; border-radius:14px; padding:14px; }
  .balances h2 { font-size:14px; margin:0 0 10px; }
  .bal-row { display:flex; justify-content:space-between; padding:8px 0; border-bottom:1px solid #F0F1F5; font-size:13px; }
  .bal-row:last-child { border-bottom:none; }
  .updated { text-align:center; color:#9AA3B2; font-size:11px; margin-top:16px; }
  .refresh-btn { display:block; width:100%; padding:12px; margin-top:14px; background:#27AE60; color:#fff; border:none; border-radius:10px; font-size:14px; font-weight:bold; }
</style>
</head>
<body>
<header>
  <h1>📱 Temo Mobile Store</h1>
  <p>متابعة لحظية عن بُعد</p>
</header>
<div class=""wrap"">
  <div class=""grid"">
    <div class=""card""><div class=""label"">إجمالي المبيعات (اليوم)</div><div class=""value"" id=""sales"">--</div></div>
    <div class=""card""><div class=""label"">إجمالي المصروفات (اليوم)</div><div class=""value"" id=""expenses"">--</div></div>
    <div class=""card""><div class=""label"">صافي الربح (اليوم)</div><div class=""value"" id=""profit"">--</div></div>
    <div class=""card""><div class=""label"">عدد الفواتير</div><div class=""value"" id=""invoices"">--</div></div>
  </div>
  <div class=""balances"">
    <h2>💳 أرصدة وسائل الدفع</h2>
    <div class=""bal-row""><b>الإجمالي</b><b id=""total"">--</b></div>
    <div id=""balList""></div>
  </div>
  <button class=""refresh-btn"" onclick=""load()"">تحديث 🔄</button>
  <div class=""updated"" id=""updated""></div>
</div>
<script>
async function load() {
  try {
    const res = await fetch('/api/dashboard');
    const data = await res.json();
    document.getElementById('sales').textContent = data.todaySales.toFixed(2) + ' ج.م';
    document.getElementById('expenses').textContent = data.todayExpenses.toFixed(2) + ' ج.م';
    document.getElementById('profit').textContent = data.todayProfit.toFixed(2) + ' ج.م';
    document.getElementById('invoices').textContent = data.invoiceCount;
    document.getElementById('total').textContent = data.totalBalance.toFixed(2) + ' ج.م';
    const balList = document.getElementById('balList');
    balList.innerHTML = '';
    data.balances.forEach(b => {
      const row = document.createElement('div');
      row.className = 'bal-row';
      row.innerHTML = '<span>' + b.method + '</span><span>' + b.balance.toFixed(2) + ' ج.م</span>';
      balList.appendChild(row);
    });
    document.getElementById('updated').textContent = 'آخر تحديث: ' + data.updatedAt;
  } catch (e) {
    document.getElementById('updated').textContent = 'حصل خطأ في التحديث';
  }
}
load();
setInterval(load, 30000);
</script>
</body>
</html>";

        public static void Stop()
        {
            try { listener?.Stop(); } catch { }
        }
    }
}
