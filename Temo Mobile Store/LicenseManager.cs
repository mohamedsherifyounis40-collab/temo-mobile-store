using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // LicenseManager: نظام ترخيص البرنامج - بيتحقق بس من صحة المفتاح، مش قادر
    // يولّد مفاتيح جديدة (لأنه بيحمل المفتاح العام بس مش الخاص). حتى لو حد شاف
    // الكود ده بالكامل، مش هيقدر يعمل مفتاح صالح لنفسه.
    //
    // المفتاح الخاص (اللي بيولّد التراخيص) موجود في أداة منفصلة تمامًا عند عمر
    // بس - "TemoLicenseGenerator" - ومعمول عمدًا إنها متتبعتش مع البرنامج خالص.
    // ==========================================================================
    public static class LicenseManager
    {
        // المفتاح العام - آمن إنه يكون هنا، لأنه بيتحقق بس ومينفعش يستخدم للتوليد
        private const string PublicKeyBase64 =
            "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAkmK1ORNrH4noBWrHevSpqXNWM4Pi8B65d4E5F6mB0uJokifQ9wkiwqDtYlPifub08bg1ImKvvyMJN6OXpizCmF1a+/zOcegHwXcb2Y25F3lRrewmJU86uz7MXjU6428uaMsKEPtDLOCijEplpLawzNqzR676mACFzuxCEgGVDO0gqX/2sjRy6VT2+MM7lrcSzpEEnw1SlMTYyt2qhouUr6fidgxOxIbzPVpYSKBxcIfaMled1g+ncixGyRdCAbdoSqogGNN3NlhYWnBvnDoI8RZMErzUK4iPupNfwxGWI7NPRyB7KV/T4hTsq//yhDUZ4370mG28DJgl3c3/HlyTcQIDAQAB";

        private static string LicenseFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.dat");

        // ==========================================================================
        // بيولّد بصمة شبه فريدة للجهاز - من رقم اللوحة الأم، وإلا معالج، وإلا اسم الجهاز
        // ==========================================================================
        public static string GetHardwareId()
        {
            string id = "";
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
                {
                    foreach (System.Management.ManagementObject mo in searcher.Get())
                    {
                        id += mo["SerialNumber"]?.ToString() ?? "";
                        break;
                    }
                }
            }
            catch { }

            if (string.IsNullOrWhiteSpace(id))
            {
                try
                {
                    using (var searcher = new System.Management.ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                    {
                        foreach (System.Management.ManagementObject mo in searcher.Get())
                        {
                            id += mo["ProcessorId"]?.ToString() ?? "";
                            break;
                        }
                    }
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(id))
                id = Environment.MachineName;

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(id));
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
            }
        }

        // ==========================================================================
        // التحقق من صحة مفتاح ترخيص معين (بدون توليد أي حاجة، بس فحص)
        // ==========================================================================
        public static (bool IsValid, DateTime? Expiration, string Message) ValidateLicenseKey(string licenseKey)
        {
            try
            {
                string[] mainParts = licenseKey.Trim().Split('.');
                if (mainParts.Length != 2) return (false, null, "مفتاح الترخيص غير صالح.");

                byte[] payloadBytes = Convert.FromBase64String(mainParts[0]);
                byte[] signatureBytes = Convert.FromBase64String(mainParts[1]);
                string payload = Encoding.UTF8.GetString(payloadBytes);

                string[] payloadParts = payload.Split('|');
                if (payloadParts.Length != 2) return (false, null, "مفتاح الترخيص غير صالح.");

                string hardwareId = payloadParts[0];
                string expStr = payloadParts[1];

                using (RSA rsa = RSA.Create())
                {
                    rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(PublicKeyBase64), out _);
                    bool signatureValid = rsa.VerifyData(payloadBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    if (!signatureValid) return (false, null, "مفتاح الترخيص غير صالح (توقيع خاطئ).");
                }

                string currentHardwareId = GetHardwareId();
                if (hardwareId != currentHardwareId) return (false, null, "مفتاح الترخيص ده مش لهذا الجهاز.");

                DateTime expiration = DateTime.ParseExact(expStr, "yyyy-MM-dd", null);
                if (DateTime.Now.Date > expiration.Date) return (false, expiration, "انتهت صلاحية الترخيص بتاريخ " + expiration.ToString("yyyy-MM-dd") + ".");

                return (true, expiration, "الترخيص سليم.");
            }
            catch
            {
                return (false, null, "مفتاح الترخيص غير صالح.");
            }
        }

        // ==========================================================================
        // فحص الترخيص المحفوظ على الجهاز حاليًا (بيتنادى مرة عند فتح البرنامج)
        // ==========================================================================
        public static bool CheckSavedLicense(out DateTime? expiration, out string message)
        {
            expiration = null;
            if (!File.Exists(LicenseFilePath))
            {
                message = "لا يوجد ترخيص مفعّل على هذا الجهاز.";
                return false;
            }

            string key;
            try { key = File.ReadAllText(LicenseFilePath); }
            catch { message = "حصل خطأ أثناء قراءة ملف الترخيص."; return false; }

            var result = ValidateLicenseKey(key);
            expiration = result.Expiration;
            message = result.Message;
            return result.IsValid;
        }

        public static void SaveLicenseKey(string licenseKey)
        {
            File.WriteAllText(LicenseFilePath, licenseKey.Trim());
        }
    }
}
