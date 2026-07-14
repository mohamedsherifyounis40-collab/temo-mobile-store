using TemoStore.Core.Abstractions;
using TemoStore.Core.Engines;
using TemoStore.Core.Entities;

namespace TemoStore.Engines.Audit
{
    // بيسجل كل العمليات بعد نجاحها (Commit) - بيستخدم Connection مستقل بتاعه (عن طريق
    // IAuditRepository)، مش جزء من أي Transaction مالية، عشان فشل تسجيل الـ Audit
    // نفسه ميرجعش عملية ناجحة بالفعل.
    public class AuditEngine : IAuditEngine
    {
        private readonly IAuditRepository _auditRepository;

        public AuditEngine(IAuditRepository auditRepository)
        {
            _auditRepository = auditRepository;
        }

        public void Log(AuditEntry entry) => _auditRepository.Insert(entry);
    }
}
