using TemoStore.Core.Abstractions;
using TemoStore.Core.Commands;
using TemoStore.Core.Engines;
using TemoStore.Core.Entities;
using TemoStore.Core.Exceptions;

namespace TemoStore.Engines.Handlers
{
    public class SetAttendanceStatusCommandHandler : ICommandHandler<SetAttendanceStatusCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;

        public SetAttendanceStatusCommandHandler(IUnitOfWorkFactory uowFactory)
        {
            _uowFactory = uowFactory;
        }

        public bool Handle(SetAttendanceStatusCommand command)
        {
            using var uow = _uowFactory.Create();

            if (uow.Attendance.IsMonthClosed(command.EmployeeId, command.Date.Year, command.Date.Month))
                throw new PayrollMonthClosedException($"شهر {command.Date.Month}/{command.Date.Year} مقفول بالفعل لهذا الموظف، لا يمكن تعديل الحضور فيه.");

            uow.Attendance.UpsertStatus(command.EmployeeId, command.Date, command.Status);
            uow.Commit();
            return true;
        }
    }

    // بيقفل شهر معين لكل الموظفين (بيتخطى أي موظف مقفول بالفعل لنفس الشهر) ويجمّد أرقامه
    // في PayrollClosures نهائيًا - نفس معادلة FillPayrollRow القديمة بالظبط، لكن هنا بتشتغل
    // جوه Transaction واحدة عبر كل الموظفين مع باقي العملية
    public class ClosePayrollMonthCommandHandler : ICommandHandler<ClosePayrollMonthCommand, int>
    {
        private readonly IUnitOfWorkFactory _uowFactory;

        public ClosePayrollMonthCommandHandler(IUnitOfWorkFactory uowFactory)
        {
            _uowFactory = uowFactory;
        }

        public int Handle(ClosePayrollMonthCommand command)
        {
            using var uow = _uowFactory.Create();

            int daysInMonth = DateTime.DaysInMonth(command.Year, command.Month);
            int closedCount = 0;

            foreach (var emp in uow.Employees.GetAll())
            {
                if (uow.Attendance.IsMonthClosed(emp.EmployeeId, command.Year, command.Month)) continue;

                var (present, absent, leave) = uow.Attendance.GetAttendanceCounts(emp.EmployeeId, command.Year, command.Month);
                decimal dayValue = daysInMonth > 0 ? emp.MonthlySalary / daysInMonth : 0;
                decimal netSalary = dayValue * (present + leave);

                uow.Attendance.InsertClosure(emp.EmployeeId, command.Year, command.Month, emp.MonthlySalary, dayValue, present, absent, leave, netSalary);
                closedCount++;
            }

            uow.Commit();
            return closedCount;
        }
    }

    public class ReopenPayrollMonthCommandHandler : ICommandHandler<ReopenPayrollMonthCommand, int>
    {
        private readonly IUnitOfWorkFactory _uowFactory;

        public ReopenPayrollMonthCommandHandler(IUnitOfWorkFactory uowFactory)
        {
            _uowFactory = uowFactory;
        }

        public int Handle(ReopenPayrollMonthCommand command)
        {
            using var uow = _uowFactory.Create();
            int count = uow.Attendance.DeleteClosuresForMonth(command.Year, command.Month);
            uow.Commit();
            return count;
        }
    }

    // isAdvance=false ("دفعة من المستحق"): لازم المبلغ يكون في حدود رصيد الموظف الفعلي
    // المكتسب (PayrollClosures - المسحوب قبل كده)، وبيرمي InsufficientEarnedBalanceException
    // لو تجاوزه. isAdvance=true ("سلفة"): مفيش حد أقصى مرتبط بالمستحق. في الحالتين،
    // ICashDrawerEngine.Debit نفسه بيرمي InsufficientBalanceException لو رصيد وسيلة
    // الدفع مايكفيش - زي أي عملية صرف تانية في النظام.
    //
    // النسخة القديمة كانت بتسجل الحركة في CashMovements وتزوّد الرصيد مباشرة من غير أي قيد
    // محاسبي - نفس ثغرة الصيانة اللي اتصلحت قبل كده. دلوقتي بيتسجل قيد متزن لكل صرف مرتب أو
    // سلفة، لكن على حسابين مختلفين حسب النوع: "دفعة من المستحق" هي مصروف مؤكد فعلًا حصل
    // (مدين مرتبات 5400 / دائن وسيلة الدفع)، أما "سلفة" فهي مش مصروف لسه - هي مديونية على
    // الموظف لحد ما شهر يتقفل ويستهلكها (مدين سلف الموظفين 1400 كأصل / دائن وسيلة الدفع)،
    // فمابتأثرش على قائمة الدخل وقت الصرف نفسه. ملحوظة: مفيش حاليًا أي تحويل تلقائي من
    // "سلف الموظفين" لـ"مرتبات" وقت قفل الشهر - ده محتاج منطق إضافي (تحديد قد إيه من السلفة
    // القائمة بيتغطى بالشهر ده) لو الشركة عايزة محاسبة استحقاق كاملة، مش موجود دلوقتي.
    public class PayEmployeeCommandHandler : ICommandHandler<PayEmployeeCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;
        private readonly ICashDrawerEngine _cashDrawer;
        private readonly IAccountingEngine _accounting;

        public PayEmployeeCommandHandler(IUnitOfWorkFactory uowFactory, ICashDrawerEngine cashDrawer, IAccountingEngine accounting)
        {
            _uowFactory = uowFactory;
            _cashDrawer = cashDrawer;
            _accounting = accounting;
        }

        public bool Handle(PayEmployeeCommand command)
        {
            using var uow = _uowFactory.Create();

            if (!command.IsAdvance)
            {
                decimal earnedBalance = uow.Attendance.GetEmployeeBalance(command.EmployeeId);
                if (command.Amount > earnedBalance)
                    throw new InsufficientEarnedBalanceException(earnedBalance);
            }

            int accountCode = command.IsAdvance ? AccountCodes.EmployeeAdvances : AccountCodes.Salaries;
            string reason = string.IsNullOrWhiteSpace(command.Description) ? ("صرف مرتب لـ " + command.EmployeeName) : command.Description!;
            _cashDrawer.Debit(command.Method, command.Amount, reason, uow, accountCode: accountCode, employeeId: command.EmployeeId, isAdvance: command.IsAdvance);

            var lines = new List<JournalLineRequest>
            {
                new() { AccountCode = accountCode, Debit = command.Amount, Credit = 0 },
                new() { AccountCode = AccountCodes.ForPaymentMethod(command.Method), Debit = 0, Credit = command.Amount }
            };
            _accounting.Post(new JournalEntryRequest { SourceType = command.IsAdvance ? "EmployeeAdvance" : "EmployeePayment", SourceId = command.EmployeeId, Description = reason, Lines = lines }, command.PerformedBy, uow);

            uow.Commit();
            return true;
        }
    }

    public class AddEmployeeCommandHandler : ICommandHandler<AddEmployeeCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;

        public AddEmployeeCommandHandler(IUnitOfWorkFactory uowFactory)
        {
            _uowFactory = uowFactory;
        }

        public bool Handle(AddEmployeeCommand command)
        {
            using var uow = _uowFactory.Create();
            uow.Employees.Add(command.FullName, command.Phone, command.MonthlySalary, command.HireDate);
            uow.Commit();
            return true;
        }
    }

    public class UpdateEmployeeCommandHandler : ICommandHandler<UpdateEmployeeCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;

        public UpdateEmployeeCommandHandler(IUnitOfWorkFactory uowFactory)
        {
            _uowFactory = uowFactory;
        }

        public bool Handle(UpdateEmployeeCommand command)
        {
            using var uow = _uowFactory.Create();
            uow.Employees.Update(command.EmployeeId, command.FullName, command.Phone, command.MonthlySalary, command.HireDate);
            uow.Commit();
            return true;
        }
    }

    // بيحذف الموظف وكل سجلات حضوره جوه Transaction واحدة عشان مايفضلش سجلات يتيمة.
    // بيفترض إن الشاشة أصلاً منعت الحذف لو HasPayrollHistory رجعت true (زي الموردين/العملاء)
    public class DeleteEmployeeCommandHandler : ICommandHandler<DeleteEmployeeCommand, bool>
    {
        private readonly IUnitOfWorkFactory _uowFactory;

        public DeleteEmployeeCommandHandler(IUnitOfWorkFactory uowFactory)
        {
            _uowFactory = uowFactory;
        }

        public bool Handle(DeleteEmployeeCommand command)
        {
            using var uow = _uowFactory.Create();
            uow.Attendance.DeleteAttendanceForEmployee(command.EmployeeId);
            uow.Employees.Delete(command.EmployeeId);
            uow.Commit();
            return true;
        }
    }
}
