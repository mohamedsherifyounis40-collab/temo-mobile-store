using System;
using System.Windows.Forms;

namespace Temo_Mobile_Store
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // ---------- إنشاء كل جداول قاعدة البيانات تلقائيًا لو الجهاز جديد ----------
            Temo_Mobile_Store.Database.DatabaseManager.EnsureSchema();

            // ---------- تجهيز كل المحركات (Core/Business Engines) قبل أي حاجة تانية ----------
            AppServices.Initialize();

            // ---------- نسخة احتياطية تلقائية عند فتح البرنامج ----------
            try
            {
                BackupManager.CreateBackupFile();
                BackupManager.PruneOldBackups();
            }
            catch { }

            // ---------- فحص الترخيص - قبل أي حاجة تانية ----------
            if (!LicenseManager.CheckSavedLicense(out DateTime? expiration, out string licenseMessage))
            {
                using (LicenseActivationForm activationForm = new LicenseActivationForm(licenseMessage))
                {
                    if (activationForm.ShowDialog() != DialogResult.OK || !activationForm.Activated)
                    {
                        Application.Exit();
                        return;
                    }
                }
            }

            AuthManager.EnsureUsersTableExists();

            LoginForm loginForm = new LoginForm();
            if (loginForm.ShowDialog() == DialogResult.OK)
            {
                Application.Run(new MainShell());
            }
            else
            {
                Application.Exit();
            }
        }
    }
}
