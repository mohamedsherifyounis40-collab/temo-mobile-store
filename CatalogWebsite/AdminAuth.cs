using System.Text;

// ==========================================================================
// AdminAuth: حماية بسيطة (HTTP Basic Auth) لصفحة إدارة الموقع - مفصولة تمامًا
// عن نظام مستخدمين برنامج المحل (الموقع مالوش وصول لقاعدة بيانات البرنامج).
// كلمة السر بتتحط كـ Environment Variable وقت النشر (ADMIN_PASSWORD).
// ==========================================================================
public static class AdminAuth
{
    public static bool Check(HttpRequest request, string adminPassword)
    {
        string? authHeader = request.Headers["Authorization"];
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic ")) return false;

        try
        {
            string encoded = authHeader["Basic ".Length..].Trim();
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            int sep = decoded.IndexOf(':');
            if (sep < 0) return false;

            string password = decoded[(sep + 1)..];
            return password == adminPassword;
        }
        catch
        {
            return false;
        }
    }

    // بيرجّع 401 مع الهيدر اللي بيخلي المتصفح يعرض نافذة تسجيل الدخول القياسية بتاعته تلقائيًا
    public static IResult UnauthorizedPrompt() => new BasicAuthChallengeResult();
}

// IResult مخصص بسيط عشان نضيف هيدر WWW-Authenticate (اللي بيخلي المتصفح يفتح نافذة اليوزر/الباسورد
// تلقائيًا) - Results.Text العادية معندهاش طريقة سهلة تضيف هيدر مخصص للرد
public class BasicAuthChallengeResult : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = 401;
        httpContext.Response.Headers.WWWAuthenticate = "Basic realm=\"Temo Catalog Admin\"";
        httpContext.Response.ContentType = "text/plain; charset=utf-8";
        await httpContext.Response.WriteAsync("محتاج تسجيل دخول.");
    }
}
