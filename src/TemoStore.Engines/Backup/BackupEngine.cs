using TemoStore.Core.Abstractions;
using TemoStore.Core.Engines;
using TemoStore.Core.Events;

namespace TemoStore.Engines.Backup
{
    // نسخ احتياطي تلقائي (نفس منطق BackupManager.cs القديم في مشروع الواجهة، منقول
    // هنا عشان يبقى محرك حقيقي يقدر ينادى من غير علاقة بأي شاشة) + التحقق من نجاح آخر نسخة.
    public class BackupEngine : IBackupEngine
    {
        private readonly IBackupRepository _backupRepository;
        private readonly IEventBus _eventBus;

        private static string BackupFolderPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TemoStore_Backups");

        public BackupEngine(IBackupRepository backupRepository, IEventBus eventBus)
        {
            _backupRepository = backupRepository;
            _eventBus = eventBus;
        }

        public Core.Engines.BackupResult CreateBackup()
        {
            try
            {
                if (!Directory.Exists(BackupFolderPath))
                    Directory.CreateDirectory(BackupFolderPath);

                string fileName = $"TemoStoreDB_Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db";
                string destPath = Path.Combine(BackupFolderPath, fileName);

                _backupRepository.VacuumInto(destPath);
                PruneOldBackups();

                _eventBus.Publish(new BackupCompleted { FilePath = destPath });
                return new Core.Engines.BackupResult { Success = true, FilePath = destPath };
            }
            catch (Exception ex)
            {
                _eventBus.Publish(new BackupFailed { Reason = ex.Message });
                return new Core.Engines.BackupResult { Success = false, Error = ex.Message };
            }
        }

        public bool VerifyLastBackup()
        {
            try
            {
                if (!Directory.Exists(BackupFolderPath)) return false;
                var files = Directory.GetFiles(BackupFolderPath, "*.db");
                if (files.Length == 0) return false;

                DateTime mostRecent = files.Select(f => new FileInfo(f).CreationTime).Max();
                return DateTime.Now - mostRecent < TimeSpan.FromDays(2); // آخر نسخة محدّثة (مش قديمة أكتر من يومين)
            }
            catch
            {
                return false;
            }
        }

        private static void PruneOldBackups(int keepCount = 30)
        {
            var files = Directory.GetFiles(BackupFolderPath, "*.db")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            for (int i = keepCount; i < files.Count; i++)
            {
                try { files[i].Delete(); } catch { /* مش قادرة تحذف نسخة معينة (مقفولة مثلًا) - نكمل الباقي */ }
            }
        }
    }
}
