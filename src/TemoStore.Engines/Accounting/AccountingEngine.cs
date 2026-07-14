using TemoStore.Core.Abstractions;
using TemoStore.Core.Engines;
using TemoStore.Core.Entities;
using TemoStore.Core.Exceptions;

namespace TemoStore.Engines.Accounting
{
    // كل عملية لازم تنشئ قيد محاسبي (مدين/دائن) تلقائيًا من خلال المحرك ده. القاعدة
    // الوحيدة اللي بتضمن "محاسبيًا صح وبدون أخطاء": لو إجمالي المدين ≠ إجمالي الدائن،
    // القيد بالكامل بيترفض ومفيش حاجة بتتحفظ - راجع قسم 6 بمستند العمارة.
    public class AccountingEngine : IAccountingEngine
    {
        public int Post(JournalEntryRequest request, string performedBy, IUnitOfWork uow)
        {
            if (request.Lines.Count < 2)
                throw new InvalidOperationException("القيد المحاسبي لازم يحتوي على بندين على الأقل (مدين ودائن).");

            decimal totalDebit = 0, totalCredit = 0;
            foreach (var line in request.Lines)
            {
                totalDebit += line.Debit;
                totalCredit += line.Credit;

                if (!uow.Accounts.Exists(line.AccountCode))
                    throw new UnknownAccountException(line.AccountCode);
            }

            if (Math.Round(totalDebit, 2) != Math.Round(totalCredit, 2))
                throw new AccountingImbalanceException(totalDebit, totalCredit);

            int journalEntryId = uow.Journal.InsertEntry(request.SourceType, request.SourceId, request.Description, performedBy);
            foreach (var line in request.Lines)
                uow.Journal.InsertLine(journalEntryId, line.AccountCode, line.Debit, line.Credit);

            return journalEntryId;
        }
    }
}
