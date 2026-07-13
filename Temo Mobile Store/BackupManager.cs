using System;
using System.IO;
using System.Linq;

namespace Temo_Mobile_Store
{
    // ==========================================================================
    // BackupManager: عمليات ملفات النسخ الاحتياطي (إنشاء/نسخ سحابي/تقليم) -
    // مستقلة عن SettingsPageControl عشان تتنادى من Program.cs (بدء التشغيل)
    // و MainShell (قفل البرنامج) بدون الحاجة لعمل instance من شاشة الإعدادات.
    // ==========================================================================
    public static class BackupManager
    {
        public static string BackupFolderPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TemoStore_Backups");

        public static void EnsureBackupFolderExists()
        {
            if (!Directory.Exists(BackupFolderPath))
                Directory.CreateDirectory(BackupFolderPath);
        }

        public static string CreateBackupFile()
        {
            EnsureBackupFolderExists();
            string fileName = $"TemoStoreDB_Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db";
            string destPath = Path.Combine(BackupFolderPath, fileName);

            SettingsRepository.BackupDatabaseTo(destPath);

            TryCopyToCloudBackup(destPath, fileName);
            return destPath;
        }

        public static string GetCloudBackupFolder()
        {
            try
            {
                string oneDrive = Environment.GetEnvironmentVariable("OneDrive")
                    ?? Environment.GetEnvironmentVariable("OneDriveConsumer")
                    ?? Environment.GetEnvironmentVariable("OneDriveCommercial");
                if (!string.IsNullOrEmpty(oneDrive) && Directory.Exists(oneDrive))
                    return Path.Combine(oneDrive, "TemoStore_Backups_Cloud");

                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    try
                    {
                        string candidate = Path.Combine(drive.RootDirectory.FullName, "My Drive");
                        if (Directory.Exists(candidate))
                            return Path.Combine(candidate, "TemoStore_Backups_Cloud");
                    }
                    catch { }
                }

                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string googleDriveClassic = Path.Combine(userProfile, "Google Drive");
                if (Directory.Exists(googleDriveClassic))
                    return Path.Combine(googleDriveClassic, "TemoStore_Backups_Cloud");
            }
            catch { }
            return null;
        }

        public static void TryCopyToCloudBackup(string sourcePath, string fileName)
        {
            try
            {
                string cloudFolder = GetCloudBackupFolder();
                if (cloudFolder == null) return;

                if (!Directory.Exists(cloudFolder))
                    Directory.CreateDirectory(cloudFolder);

                string destPath = Path.Combine(cloudFolder, fileName);
                File.Copy(sourcePath, destPath, true);
            }
            catch { }
        }

        public static void PruneOldBackups(int keepCount = 30)
        {
            var files = Directory.GetFiles(BackupFolderPath, "*.db")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            for (int i = keepCount; i < files.Count; i++)
            {
                try { files[i].Delete(); } catch { }
            }
        }
    }
}
