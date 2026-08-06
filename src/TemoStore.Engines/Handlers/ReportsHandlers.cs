using TemoStore.Core.Abstractions;
using TemoStore.Core.Commands;
using TemoStore.Core.Engines;
using TemoStore.Core.Entities;

namespace TemoStore.Engines.Handlers
{
    // ==========================================================================
    // إقفال اليوم الفعلي - أول Command حقيقي لعملية كانت بالكامل SQL خام في
    // ReportsPageControl.cs (BtnCloseDay_Click/BtnReopenDay_Click). القرار المحاسبي
    // القديم اتحافظ عليه بالظبط: تحديث PaymentMethodBalances مباشرة من غير قيد
    // محاسبي للإقفال نفسه (تأكيد عدّ فعلي مش حدث اقتصادي جديد)، والفرق (Difference)
    // بيتسجل صفر دايمًا لأن مفيش مقارنة متوقع/فعلي أصلًا - نفس القرار القديم بالظبط.
    // ==========================================================================
    public class CloseDayCommandHandler : ICommandHandler<CloseDayCommand, int>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IDateClosureRepository _dateClosure;

        public CloseDayCommandHandler(IUnitOfWorkFactory uowFactory, IDateClosureRepository dateClosure)
        {
            _uowFactory = uowFactory;
            _dateClosure = dateClosure;
        }

        public int Handle(CloseDayCommand command)
        {
            if (_dateClosure.IsClosed(DateTime.Now))
                throw new InvalidOperationException("تم إقفال اليوم بالفعل.");

            // فحص "هل اليوم مقفول" بتاع كل الشاشات التانية بيتأكد بس من وجود صف "نقدي"
            // في DailyClosures - لو الشاشة نسيت تبعت وسيلة "نقدي" جوه القاموس، هيتقفل
            // كل حاجة تاني عادي بس الفحص العام هيفضل شغال وكأن اليوم لسه مفتوح للأبد
            if (!command.MethodActualBalances.ContainsKey("نقدي"))
                throw new InvalidOperationException("لازم تحدد رصيد \"نقدي\" الفعلي عشان يتم إقفال اليوم.");

            DateTime today = DateTime.Now.Date;
            using var uow = _uowFactory.Create();

            int closedCount = 0;
            foreach (var kvp in command.MethodActualBalances)
            {
                string method = kvp.Key;
                decimal actual = kvp.Value;

                decimal opening = uow.Closures.GetLastActualClosingBalance(method) ?? uow.CashDrawer.GetBalance(method);
                decimal totalIn = uow.Closures.GetTodayMovementsTotal(method, "قبض", today);
                decimal totalOut = uow.Closures.GetTodayMovementsTotal(method, "صرف", today);
                if (method == "نقدي")
                    totalOut += uow.Closures.GetTodayExpensesTotal(today);

                int closureId = uow.Closures.InsertClosure(today, method, opening, totalIn, totalOut, actual, DateTime.Now);

                if (method == "نقدي" && command.CashDenominationCounts != null)
                {
                    foreach (var d in command.CashDenominationCounts)
                    {
                        if (d.Value > 0)
                            uow.Closures.InsertDenominationLine(closureId, d.Key, d.Value);
                    }
                }

                uow.CashDrawer.SetBalance(method, actual);
                closedCount++;
            }

            uow.Commit();
            return closedCount;
        }
    }

    public class ReopenDayCommandHandler : ICommandHandler<ReopenDayCommand, int>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly IDateClosureRepository _dateClosure;
        private readonly IAccountingEngine _accounting;

        public ReopenDayCommandHandler(IUnitOfWorkFactory uowFactory, IDateClosureRepository dateClosure, IAccountingEngine accounting)
        {
            _uowFactory = uowFactory;
            _dateClosure = dateClosure;
            _accounting = accounting;
        }

        public int Handle(ReopenDayCommand command)
        {
            if (!_dateClosure.IsClosed(DateTime.Now))
                throw new InvalidOperationException("اليوم مش مقفول أصلاً، مفيش داعي تفتحه.");

            DateTime today = DateTime.Now.Date;
            using var uow = _uowFactory.Create();

            var rows = uow.Closures.GetClosuresForDate(today);
            if (rows.Count == 0)
                throw new InvalidOperationException("لم يتم العثور على إقفال اليوم.");

            // ملحوظة: AdjustmentMovementId مبيتسجلش بقيمة غير NULL في أي حتة تانية في
            // الكود دلوقتي (عمود ترحيل قديم لربط فرق عجز/زيادة بإقفال معين، الربط ده
            // متكملش)، فالفرع ده حاليًا ميتفّذش عمليًا. اتسيب زي ما هو (مش بيتشال) عشان
            // لو اتوصل يومًا ما، لازم يفضل مطابق لمنطق CancelMovementCommandHandler.Handle
            // في TreasuryHandlers.cs بالظبط - أي تعديل هناك لازم ينعكس هنا كمان.
            foreach (var row in rows)
            {
                if (!row.AdjustmentMovementId.HasValue) continue;

                var existing = uow.CashDrawer.GetMovementById(row.AdjustmentMovementId.Value);
                if (existing == null) continue;

                decimal currentBalance = uow.CashDrawer.GetBalance(existing.PaymentMethod);
                decimal newBalance = existing.MovementType == "قبض" ? currentBalance - existing.Amount : currentBalance + existing.Amount;
                uow.CashDrawer.SetBalance(existing.PaymentMethod, newBalance);
                uow.CashDrawer.DeleteMovement(existing.Id);

                if (existing.AccountCode.HasValue)
                {
                    var lines = existing.MovementType == "قبض"
                        ? new List<JournalLineRequest>
                        {
                            new() { AccountCode = existing.AccountCode.Value, Debit = existing.Amount, Credit = 0 },
                            new() { AccountCode = AccountCodes.ForPaymentMethod(existing.PaymentMethod), Debit = 0, Credit = existing.Amount }
                        }
                        : new List<JournalLineRequest>
                        {
                            new() { AccountCode = AccountCodes.ForPaymentMethod(existing.PaymentMethod), Debit = existing.Amount, Credit = 0 },
                            new() { AccountCode = existing.AccountCode.Value, Debit = 0, Credit = existing.Amount }
                        };
                    _accounting.Post(new JournalEntryRequest { SourceType = "DayCloseAdjustmentCancellation", SourceId = existing.Id, Description = existing.Description, Lines = lines }, command.PerformedBy, uow);
                }
            }

            foreach (var row in rows)
            {
                uow.Closures.DeleteDenominationsForClosure(row.Id);
                uow.CashDrawer.SetBalance(row.PaymentMethod, row.ExpectedClosingBalance);
            }

            uow.Closures.DeleteClosuresForDate(today);

            uow.Commit();
            return rows.Count;
        }
    }
}
