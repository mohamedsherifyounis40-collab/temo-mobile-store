using System;
using System.Globalization;
using System.Net;
using System.Text;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // DailyReportHtmlBuilder: بيبني صفحة HTML/CSS كاملة ومستقلة (كل الأنماط جوّاها،
    // مفيش أي ملف خارجي) لتقرير "الحركات اليومية والأرصدة" - نفس بالتة ألوان/كروت
    // شاشات البرنامج (shell.css/home.css) بالظبط، عشان التقرير يبان جزء من نفس الهوية
    // البصرية. الصفحة دي بتتحول لـ PDF فعلي عن طريق DailyReportPdfExporter (WebView2).
    // ==========================================================================
    public static class DailyReportHtmlBuilder
    {
        private static readonly CultureInfo ArCulture = new CultureInfo("ar-EG");

        public static string Build(DailyReportData report)
        {
            UIHelpers.LoadStoreSettings(out string storeName, out _, out _, out _, out _);
            if (string.IsNullOrWhiteSpace(storeName)) storeName = "Temo Mobile Store";

            bool isToday = report.Date.Date == DateTime.Now.Date;
            string dateLine = report.Date.ToString("dddd d MMMM yyyy", ArCulture);
            string timeLine = isToday ? $"حتى الساعة {DateTime.Now.ToString("hh:mm tt", ArCulture)}" : "تقرير يوم كامل";
            string balancesNote = isToday ? "لحظة إنشاء التقرير" : "كما كانت في نهاية اليوم ده";

            var sb = new StringBuilder();
            sb.Append("<!doctype html><html lang=\"ar\" dir=\"rtl\"><head><meta charset=\"utf-8\">");
            sb.Append(BuildStyle());
            sb.Append("</head><body>");
            sb.Append("<div class=\"page\">");

            // ---------- الهيدر ----------
            sb.Append("<div class=\"header\">");
            sb.Append("<div class=\"brand\">");
            sb.Append("<div class=\"brand-icon\">📱</div>");
            sb.Append("<div><div class=\"brand-name\">").Append(Enc(storeName)).Append("</div><div class=\"brand-sub\">نظام إدارة المحل</div></div>");
            sb.Append("</div>");
            sb.Append("</div>");

            sb.Append("<h1 class=\"report-title\">تقرير الحركات اليومية والأرصدة</h1>");
            sb.Append("<div class=\"report-subtitle\">ملخص شامل لحركات البيع والشراء والصيانة والمرتجعات، وأرصدة الخزنة والمخزون</div>");

            sb.Append("<div class=\"date-row\">");
            sb.Append("<div class=\"date-main\">").Append(Enc(dateLine)).Append("</div>");
            sb.Append("<div class=\"date-sub\">").Append(Enc(timeLine)).Append("</div>");
            sb.Append("</div>");

            // ---------- ملخص اليوم ----------
            sb.Append("<div class=\"section-title\">ملخص اليوم</div>");
            sb.Append("<div class=\"kpi-row\">");
            sb.Append(BuildKpiCard("📈", "accent-green", "إجمالي المبيعات", Money(report.Summary.NetSales), TrendPctBadge(report.Summary.SalesTrendPct)));
            sb.Append(BuildKpiCard("🧾", "accent-blue", "عدد الفواتير", report.Summary.InvoiceCount.ToString(CultureInfo.InvariantCulture) + " فاتورة", TrendDeltaBadge(report.Summary.InvoiceTrendDelta)));
            sb.Append(BuildKpiCard("📊", "accent-purple", "صافي الربح", Money(report.Summary.NetProfit), TrendPctBadge(report.Summary.ProfitTrendPct)));
            sb.Append(BuildKpiCard("↩️", "accent-red", "المرتجعات", Money(report.Summary.ReturnsAmount), $"<span class=\"trend-neutral\">{report.Summary.ReturnsCount} عمليات</span>"));
            sb.Append("</div>");

            // ---------- أرصدة وسائل الدفع (نفس شكل الداشبورد الرئيسي بالظبط) ----------
            sb.Append("<div class=\"section-title\">أرصدة وسائل الدفع <span class=\"muted-tag\">").Append(Enc(balancesNote)).Append("</span></div>");
            if (!report.BalancesReliable)
            {
                sb.Append("<div class=\"warning-box\">⚠️ الأرصدة دي لتاريخ قديم قبل آخر تسوية للدفتر المحاسبي، فممكن تكون غير دقيقة تمامًا - استخدمها كتقريب بس.</div>");
            }
            sb.Append("<div class=\"balances-row\">");
            foreach (var (method, balance) in report.Balances.MethodBalances)
                sb.Append(BuildBalanceChip(PaymentIcon(method), method, Money(balance), false));
            sb.Append(BuildBalanceChip("Σ", "الإجمالي", Money(report.Balances.TotalMethodBalance), true));
            sb.Append("</div>");

            // ---------- أرصدة تانية (عملاء/مخزون) ----------
            sb.Append("<div class=\"kpi-row\" style=\"grid-template-columns: repeat(2, 1fr);\">");
            sb.Append(BuildBalanceCard("👤", "مستحق على العملاء", Money(report.Balances.CustomersReceivable)));
            sb.Append(BuildBalanceCard("📦", "قيمة المخزون (بالتكلفة)", Money(report.Balances.InventoryValueAtCost)));
            sb.Append("</div>");

            // ---------- تفاصيل الحركات ----------
            sb.Append("<div class=\"section-title\">تفاصيل حركات اليوم</div>");
            sb.Append("<table class=\"movements-table\"><thead><tr>");
            sb.Append("<th>الوقت</th><th>نوع الحركة</th><th>البيان</th><th>السريال / IMEI</th><th>طريقة الدفع</th><th>الموظف</th><th>المبلغ</th>");
            sb.Append("</tr></thead><tbody>");

            if (report.Movements.Count == 0)
            {
                sb.Append("<tr><td colspan=\"7\" class=\"empty-row\">مفيش أي حركة مسجلة في اليوم ده</td></tr>");
            }
            else
            {
                foreach (var m in report.Movements)
                {
                    sb.Append("<tr>");
                    sb.Append("<td class=\"muted-cell\">").Append(m.Timestamp.ToString("hh:mm tt", ArCulture)).Append("</td>");
                    sb.Append("<td>").Append(TypeBadge(m.Type)).Append("</td>");
                    sb.Append("<td class=\"desc-cell\">").Append(Enc(m.Description)).Append("</td>");
                    sb.Append("<td class=\"muted-cell\">").Append(string.IsNullOrEmpty(m.SerialOrImei) ? "—" : MaskSerial(m.SerialOrImei)).Append("</td>");
                    sb.Append("<td class=\"muted-cell\">").Append(Enc(string.IsNullOrEmpty(m.PaymentMethod) ? "—" : m.PaymentMethod)).Append("</td>");
                    sb.Append("<td class=\"muted-cell\">").Append(string.IsNullOrEmpty(m.Employee) ? "—" : Enc(m.Employee)).Append("</td>");
                    sb.Append("<td class=\"amount-cell ").Append(m.Amount < 0 ? "amount-out" : "amount-in").Append("\">")
                      .Append(m.Amount < 0 ? "−" : "+").Append(Math.Abs(m.Amount).ToString("N0", CultureInfo.InvariantCulture)).Append("</td>");
                    sb.Append("</tr>");
                }
            }
            sb.Append("</tbody></table>");

            // ---------- ملخص الحركات (فوتر) ----------
            sb.Append("<div class=\"footer-summary\">");
            sb.Append(BuildFooterItem("عدد الحركات", report.MovementCount.ToString(CultureInfo.InvariantCulture) + " حركة"));
            sb.Append(BuildFooterItem("إجمالي الوارد", Money(report.TotalIn), "footer-in"));
            sb.Append(BuildFooterItem("إجمالي المنصرف", Money(report.TotalOut), "footer-out"));
            sb.Append(BuildFooterItem("الصافي", Money(report.NetTotal)));
            sb.Append("</div>");

            sb.Append("<div class=\"generated-note\">تم إنشاء التقرير تلقائيًا الساعة ").Append(DateTime.Now.ToString("hh:mm tt", ArCulture))
              .Append(" بواسطة نظام ").Append(Enc(storeName)).Append("</div>");

            sb.Append("</div>"); // .page
            sb.Append("</body></html>");
            return sb.ToString();
        }

        private static string BuildKpiCard(string icon, string accentClass, string label, string value, string trendHtml)
        {
            return $@"<div class=""kpi-card"">
                <div class=""kpi-top""><span class=""kpi-icon {accentClass}"">{icon}</span>{trendHtml}</div>
                <div class=""kpi-value"">{value}</div>
                <div class=""kpi-label"">{Enc(label)}</div>
            </div>";
        }

        // نفس أيقونات "أرصدة وسائل الدفع" في HomePage.razor بالظبط (PaymentIcon)
        private static string PaymentIcon(string method) => method switch
        {
            "نقدي" => "💵",
            "فوري" => "⚡",
            "أمان" => "🛡️",
            "ممكن" => "🟢",
            "فودافون كاش" => "📱",
            "إنستاباي" => "🔷",
            _ => "💳"
        };

        private static string BuildBalanceChip(string icon, string label, string value, bool isTotal)
        {
            string cls = isTotal ? "balance-chip balance-total" : "balance-chip";
            return $@"<div class=""{cls}"">
                <span class=""balance-icon"">{icon}</span>
                <div><div class=""balance-value"">{value}</div><div class=""balance-label"">{Enc(label)}</div></div>
            </div>";
        }

        private static string BuildBalanceCard(string icon, string label, string value)
        {
            return $@"<div class=""kpi-card"">
                <div class=""kpi-top""><span class=""kpi-icon"">{icon}</span></div>
                <div class=""kpi-value"">{value}</div>
                <div class=""kpi-label"">{Enc(label)}</div>
            </div>";
        }

        private static string BuildFooterItem(string label, string value, string? extraClass = null)
        {
            return $@"<div class=""footer-item""><div class=""footer-label"">{Enc(label)}</div><div class=""footer-value {extraClass}"">{value}</div></div>";
        }

        private static string TrendPctBadge(decimal? pct)
        {
            if (pct == null) return "<span class=\"trend-neutral\">—</span>";
            string cls = pct.Value >= 0 ? "trend-up" : "trend-down";
            string sign = pct.Value >= 0 ? "+" : "";
            return $"<span class=\"kpi-trend {cls}\">{sign}{pct.Value.ToString("0.#", CultureInfo.InvariantCulture)}%</span>";
        }

        private static string TrendDeltaBadge(int delta)
        {
            if (delta == 0) return "<span class=\"trend-neutral\">=</span>";
            string cls = delta > 0 ? "trend-up" : "trend-down";
            string sign = delta > 0 ? "+" : "";
            return $"<span class=\"kpi-trend {cls}\">{sign}{delta}</span>";
        }

        private static string TypeBadge(string type)
        {
            string cls = type switch
            {
                "بيع" => "badge-sale",
                "شراء" => "badge-purchase",
                "صيانة" => "badge-maintenance",
                "مرتجع" => "badge-return",
                _ => "badge-sale"
            };
            return $"<span class=\"type-badge {cls}\"><span class=\"type-dot\"></span>{Enc(type)}</span>";
        }

        // بيغطي كل السيريال/IMEI ما عدا آخر 4 أرقام - نفس مبدأ إخفاء الأرقام الحساسة
        // المستخدم أصلًا في الشاشات التانية، عشان التقرير المطبوع/الـPDF (اللي ممكن يتشارك
        // بسهولة أكتر من شاشة داخل البرنامج) ميكشفش السيريال كامل
        private static string MaskSerial(string serial)
        {
            if (serial.Length <= 4) return Enc(serial);
            return Enc(serial.Substring(0, 2) + new string('•', 6) + serial.Substring(serial.Length - 4));
        }

        private static string Money(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture) + " ج.م";

        private static string Enc(string s) => WebUtility.HtmlEncode(s ?? "");

        private static string BuildStyle()
        {
            return @"<style>
                * { box-sizing: border-box; }
                html, body { margin:0; padding:0; font-family:'Segoe UI', Tahoma, Arial, sans-serif; background:#fff; color:#1A2B4C; }
                .page { padding: 28px 34px; }

                .header { display:flex; justify-content:flex-end; margin-bottom: 18px; }
                .brand { display:flex; align-items:center; gap:10px; }
                .brand-icon { font-size:28px; }
                .brand-name { font-weight:bold; font-size:15px; }
                .brand-sub { font-size:11px; color:#7C8AA0; }

                .report-title { font-size:22px; margin: 6px 0 4px; }
                .report-subtitle { font-size:12.5px; color:#7C8AA0; margin-bottom: 14px; }

                .date-row { display:flex; justify-content:space-between; align-items:center; padding: 10px 0; border-top:1px solid #E5E9F0; border-bottom:1px solid #E5E9F0; margin-bottom: 20px; }
                .date-main { font-weight:bold; font-size:13px; }
                .date-sub { font-size:12px; color:#7C8AA0; }

                .section-title { font-size:14px; font-weight:bold; margin: 6px 0 10px; }
                .muted-tag { font-size:11px; font-weight:normal; color:#F59E0B; }

                .kpi-row { display:grid; grid-template-columns: repeat(4, 1fr); gap: 12px; margin-bottom: 22px; }
                .kpi-card { border:1px solid #E5E9F0; border-radius:12px; padding:14px; background:#fff; }
                .kpi-top { display:flex; justify-content:space-between; align-items:center; margin-bottom:8px; }
                .kpi-icon { font-size:18px; }
                .accent-green { color:#22C55E; } .accent-blue { color:#2D6CDF; } .accent-purple { color:#8B5CF6; } .accent-red { color:#EF4444; }
                .kpi-trend { font-size:11px; font-weight:bold; white-space:nowrap; direction:ltr; unicode-bidi:embed; }
                .trend-up { color:#22C55E; } .trend-down { color:#EF4444; } .trend-neutral { font-size:11px; color:#7C8AA0; font-weight:bold; }
                .kpi-value { font-size:16px; font-weight:bold; direction:ltr; unicode-bidi:embed; text-align:right; }
                .kpi-label { font-size:11.5px; color:#7C8AA0; margin-top:2px; }

                .movements-table { width:100%; border-collapse:collapse; font-size:12px; margin-bottom: 16px; }
                .movements-table th { background:#F5F7FA; color:#7C8AA0; font-weight:bold; padding:8px 6px; text-align:center; }
                .movements-table td { padding:7px 6px; text-align:center; border-bottom:1px solid #E5E9F0; }
                .muted-cell { color:#7C8AA0; }
                .desc-cell { text-align:right; font-weight:bold; }
                .empty-row { color:#7C8AA0; padding:20px !important; }

                .type-badge { display:inline-flex; align-items:center; gap:5px; padding:3px 9px; border-radius:12px; font-size:11px; font-weight:bold; }
                .type-dot { width:6px; height:6px; border-radius:50%; background:currentColor; }
                .badge-sale { background:#E9F9EF; color:#157347; }
                .badge-purchase { background:#EAF1FD; color:#2D6CDF; }
                .badge-maintenance { background:#FEF3E2; color:#B45309; }
                .badge-return { background:#FDECEC; color:#B42318; }

                .amount-cell { font-weight:bold; direction:ltr; unicode-bidi:embed; }
                .amount-in { color:#157347; } .amount-out { color:#B42318; }

                .footer-summary { display:flex; justify-content:space-between; border-top:2px solid #1A2B4C; padding-top:12px; margin-bottom: 8px; }
                .footer-item { text-align:center; flex:1; }
                .footer-label { font-size:11px; color:#7C8AA0; margin-bottom:3px; }
                .footer-value { font-size:14px; font-weight:bold; direction:ltr; unicode-bidi:embed; }
                .footer-in { color:#157347; } .footer-out { color:#B42318; }

                .generated-note { text-align:center; font-size:10.5px; color:#7C8AA0; margin-top: 14px; }

                .warning-box { background:#FEF3E2; border:1px solid #F5D9A8; color:#B45309; border-radius:9px; padding:9px 12px; font-size:11.5px; margin-bottom:10px; }

                .balances-row { display:flex; gap:10px; flex-wrap:wrap; margin-bottom: 18px; }
                .balance-chip { display:flex; align-items:center; gap:10px; padding:10px 14px; border-radius:12px; border:1px solid #E5E9F0; background:#fff; flex:1; min-width:120px; }
                .balance-icon { font-size:18px; }
                .balance-value { font-size:13px; font-weight:bold; direction:ltr; unicode-bidi:embed; }
                .balance-label { font-size:11px; color:#7C8AA0; }
                .balance-total { background:#2D6CDF; border-color:#2D6CDF; }
                .balance-total .balance-value, .balance-total .balance-label { color:#fff; }
            </style>";
        }
    }
}
