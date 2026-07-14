# Temo Mobile Store — Engine Architecture (Design Document)

Status: **DRAFT — not yet implemented.** No engine code exists yet. This document exists so the design is agreed on paper before any code is written, per the requested workflow: document first, then implement one engine (one vertical slice) at a time.

## 0. Why this document exists, and the one tradeoff you need to decide on

The requirements describe a full Clean/Onion Architecture with an event bus, 15 engines, double-entry accounting, and a Core Engine as the only entry point. That is the *right* target architecture if the goal is: multi-branch, API, mobile app, website, cloud sync — all without rewriting business logic later.

Being straight about scope: this is not a refactor, it is a **full rewrite of how every screen talks to the database**. Today, every screen (`SalesPageControl`, `SuppliersPageControl`, `TreasuryPageControl`, …) calls its matching `*Repository` static class directly, which opens a `SqliteConnection` and runs SQL inline. Under the new architecture, no screen is allowed to do that at all — everything goes `UI → Core Engine → Engines → Data layer`. That touches all ~11 screens and ~10 repositories that exist today.

This app has a real client using it in production right now. Two migration strategies are possible:

- **Big-bang**: build all 15 engines, then cut every screen over at once. Higher risk (nothing is provably correct until the very end), but no time spent maintaining two parallel code paths.
- **Incremental (strangler fig), recommended**: build the Core Engine + Event Bus + just enough engines to fully run **one** process end-to-end (proposed first slice: **Sales**, since it touches Inventory, Cash Drawer, Accounting, and Audit — a representative slice, and the one we just finished fixing real bugs in). Ship that, verify it against real usage, then migrate the next screen. The old `*Repository` classes stay untouched and working until the new path proves itself for that screen.

This matches what was actually asked ("وبعدين يبدأ التنفيذ Engine واحد في كل مرة" — implement one engine at a time), just scoped as **one full vertical slice at a time** rather than one engine in isolation — a half-built engine with nothing calling it can't be proven correct. **This is the one open decision this document needs confirmed before Engine #1 starts.**

---

## 1. Target solution structure

```
Temo Mobile Store.slnx
├── src/
│   ├── TemoStore.Core/                 # Pure domain — zero dependency on SQLite, WinForms, or anything external
│   │   ├── Entities/                   # Sale, Purchase, Product, Customer, Supplier, JournalEntry, ...
│   │   ├── Events/                     # IDomainEvent, SaleCompleted, PurchaseCompleted, ...
│   │   ├── Commands/                   # ICommand<TResult>, CreateSaleCommand, CreatePurchaseCommand, ...
│   │   ├── Engines/                    # Interfaces ONLY: IValidationEngine, IInventoryEngine, ICashDrawerEngine, ...
│   │   ├── Abstractions/               # IUnitOfWork, IRepository<T>, ICoreEngine, IEventBus
│   │   └── Exceptions/                 # InsufficientBalanceException, InsufficientStockException, ... (already exist, move here)
│   │
│   ├── TemoStore.Engines/              # One assembly, one folder per engine (see §6 on why not 15 assemblies)
│   │   ├── Validation/ValidationEngine.cs
│   │   ├── Inventory/InventoryEngine.cs
│   │   ├── CashDrawer/CashDrawerEngine.cs
│   │   ├── Customer/CustomerEngine.cs
│   │   ├── Supplier/SupplierEngine.cs
│   │   ├── Pricing/PricingEngine.cs
│   │   ├── Profit/ProfitEngine.cs
│   │   ├── Accounting/AccountingEngine.cs
│   │   ├── Audit/AuditEngine.cs
│   │   ├── Integrity/IntegrityEngine.cs
│   │   ├── Health/HealthEngine.cs
│   │   ├── Notification/NotificationEngine.cs
│   │   ├── Backup/BackupEngine.cs
│   │   ├── Recovery/RecoveryEngine.cs
│   │   └── Core/CoreEngine.cs           # The orchestrator described in §4
│   │
│   ├── TemoStore.EventBus/              # In-process pub/sub now; swappable for a real broker later (see §5)
│   │
│   ├── TemoStore.Data/                  # Repository implementations (Microsoft.Data.Sqlite), migrations
│   │   └── Repositories/                # SaleRepository, ProductRepository, ... implement TemoStore.Core.Abstractions
│   │
│   ├── TemoStore.UI.WinForms/           # = today's "Temo Mobile Store" project, trimmed to pure UI
│   │
│   └── TemoStore.Tests/                 # xUnit, one test class per engine, fakes for IUnitOfWork/repositories
│
├── CatalogWebsite/                      # unchanged, out of scope
└── (future, not built now) TemoStore.Api/   # ASP.NET Core — exposes the same Core/Engines for mobile/web/branches
```

**Why one `TemoStore.Engines` assembly with folders, not 15 separate `.csproj`s:** loose coupling and testability come from *interfaces + dependency injection*, not from physical assembly boundaries. 15 tiny assemblies means 15x the project-reference bookkeeping for zero extra decoupling — each engine already only depends on `TemoStore.Core` interfaces, never on another engine's concrete class. If a specific engine later needs to ship independently (e.g., as a NuGet package), it can be split out of the folder in an afternoon — the interface boundary already makes that safe.

---

## 2. Core abstractions

```csharp
// TemoStore.Core/Events/IDomainEvent.cs
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAtUtc { get; }
}

// TemoStore.Core/Abstractions/IEventBus.cs
public interface IEventBus
{
    void Publish<TEvent>(TEvent @event) where TEvent : IDomainEvent;
    void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IDomainEvent;
}

public interface IEventHandler<TEvent> where TEvent : IDomainEvent
{
    void Handle(TEvent @event);
}

// TemoStore.Core/Abstractions/ICoreEngine.cs — the ONLY entry point from UI
public interface ICoreEngine
{
    TResult Execute<TResult>(ICommand<TResult> command);
}

public interface ICommand<TResult> { }

// TemoStore.Core/Abstractions/IUnitOfWork.cs — wraps one DB transaction end to end
public interface IUnitOfWork : IDisposable
{
    void Commit();
    void Rollback();
    ISaleRepository Sales { get; }
    IProductRepository Products { get; }
    ICashDrawerRepository CashDrawer { get; }
    IJournalRepository Journal { get; }
    // ... one repository property per aggregate, all sharing the same underlying SqliteTransaction
}
```

### Example commands (one per business operation named in the requirements)

```csharp
public record CreateSaleCommand(string Barcode, int Quantity, string? Imei, string PaymentType, string? PaymentMethod, int? CustomerId) : ICommand<SaleResult>;
public record CreatePurchaseCommand(int SupplierId, List<PurchaseLineDto> Lines, bool PayCashNow, string? CashMethod) : ICommand<PurchaseResult>;
public record CreateReturnCommand(int OriginalSaleId, int Quantity, string Reason) : ICommand<ReturnResult>;
public record PaySupplierCommand(int SupplierId, string Method, decimal Amount) : ICommand<PaymentResult>;
public record CollectFromCustomerCommand(int CustomerId, string Method, decimal Amount) : ICommand<PaymentResult>;
public record RecordExpenseCommand(int AccountCode, decimal Amount, string PaymentMethod) : ICommand<ExpenseResult>;
public record TransferStockCommand(string Barcode, int Quantity, string FromLocation, string ToLocation) : ICommand<TransferResult>;
public record TransferFundsCommand(string FromMethod, string ToMethod, decimal Amount, string? Description) : ICommand<TransferResult>;
```

Every screen change becomes: *build a command object from form inputs → `_coreEngine.Execute(command)` → show the result.* No screen ever opens a `SqliteConnection` again.

---

## 3. Per-engine interfaces (contract only — this is what gets reviewed before any implementation)

```csharp
public interface IValidationEngine
{
    ValidationResult Validate(ICommand command); // dispatches internally by command type
}

public interface IInventoryEngine
{
    void DeductStock(string barcode, int quantity, IUnitOfWork uow);
    void AddStock(string barcode, int quantity, decimal unitCost, IUnitOfWork uow);
    void MarkImeiSold(string imei, int saleId, IUnitOfWork uow);
    void MarkImeiInStock(string imei, IUnitOfWork uow);
    void RecordStockMovement(StockMovement movement, IUnitOfWork uow);
    IReadOnlyList<LowStockAlert> CheckMinimumThresholds();
}

public interface ICashDrawerEngine
{
    decimal GetBalance(string paymentMethod);
    void Credit(string paymentMethod, decimal amount, string reason, IUnitOfWork uow);
    void Debit(string paymentMethod, decimal amount, string reason, IUnitOfWork uow); // throws InsufficientBalanceException, never allows negative
    void Transfer(string fromMethod, string toMethod, decimal amount, string? description, IUnitOfWork uow);
}

public interface ICustomerEngine
{
    void UpdateBalance(int customerId, decimal delta, IUnitOfWork uow);
    void UpdateLoyaltyPoints(int customerId, int delta, IUnitOfWork uow);
    void RecordInstallment(int customerId, InstallmentPlan plan, IUnitOfWork uow);
    CustomerStatement GetStatement(int customerId);
    void RegisterWarranty(int customerId, string imei, DateTime expiresOn, IUnitOfWork uow);
}

public interface ISupplierEngine
{
    void UpdateBalance(int supplierId, decimal delta, IUnitOfWork uow);
    SupplierStatement GetStatement(int supplierId);
}

public interface IPricingEngine
{
    PriceQuote CalculatePrice(string barcode, int quantity, CustomerTier tier, PromotionContext? promotion);
}

public interface IProfitEngine
{
    void RecordProfit(SaleCompleted evt, IUnitOfWork uow);
}

public interface IAccountingEngine
{
    void Post(JournalEntryRequest request, IUnitOfWork uow); // throws AccountingImbalanceException if Sum(Debit) != Sum(Credit)
}

public interface IAuditEngine
{
    void Log(AuditEntry entry); // fire-and-forget, post-commit only — never blocks a business transaction
}

public interface IIntegrityEngine
{
    IntegrityCheckResult CheckBeforeCommit(IUnitOfWork uow); // last gate before Commit() — see §4 step 8
}

public interface IHealthEngine
{
    HealthReport RunFullCheck(); // on-demand / scheduled, not part of the hot path
}

public interface INotificationEngine
{
    void Notify(NotificationType type, string message, object? context);
}

public interface IBackupEngine
{
    BackupResult CreateBackup();
    bool VerifyLastBackup();
}

public interface IRecoveryEngine
{
    void ResumeOrRollbackPendingOperations(); // runs once at startup — see §7, scope is narrower than it sounds
}
```

---

## 4. End-to-end flow: Sale (the proposed first vertical slice)

This is the key design decision that reconciles two requirements that look like they conflict: **"every operation in one all-or-nothing DB transaction"** and **"event-driven, engines only listen to their own events, no direct dependency on each other."** The resolution: the *write path* is synchronous and transactional; the *event bus* is only used for what happens *after* a successful commit.

```
UI (SalesPageControl)
   builds CreateSaleCommand
        │
        ▼
ICoreEngine.Execute(command)
        │
        ├─ 1. IValidationEngine.Validate(command)
        │      – product exists? qty available? IMEI valid & not duplicated? customer required for "آجل"?
        │      – permissions (is this user allowed to sell)? is the sale date's day already closed?
        │      – INVALID → return failure immediately. Nothing opened, nothing written, no event fired.
        │
        ├─ 2. uow = new UnitOfWork()  → BEGIN TRANSACTION (one SqliteTransaction for everything below)
        │
        ├─ 3. IPricingEngine.CalculatePrice(...)          → final unit price
        ├─ 4. IInventoryEngine.DeductStock(...) [+ MarkImeiSold if serialized]   (uow)
        ├─ 5. IF cash sale: ICashDrawerEngine.Credit(method, total, ...)         (uow)
        ├─ 6. IProfitEngine.RecordProfit(...)                                    (uow)
        ├─ 7. IAccountingEngine.Post(journalEntryRequest)                        (uow)
        │        Dr  Drawer/AR account     total
        │            Cr  4100 إيراد المبيعات    total
        │        Dr  5500 تكلفة البضاعة المباعة  cost   (new account — doesn't exist yet, see §8)
        │            Cr  1200 المخزون              cost
        │
        ├─ 8. IIntegrityEngine.CheckBeforeCommit(uow)
        │      – re-verify: stock didn't go negative, journal is balanced, no duplicate IMEI slipped in
        │      – FAILS → uow.Rollback(). Nothing persisted. Return failure. (This is the "لا يسمح بعملية نصف منفذة" guarantee.)
        │
        ├─ 9. uow.Commit()   → COMMIT TRANSACTION. From here on the sale is real and final.
        │
        └─ 10. eventBus.Publish(new SaleCompleted(saleId, ...))
                     │
                     ├─→ IAuditEngine.Handle(SaleCompleted)          → writes audit row (who/when/what)
                     ├─→ INotificationEngine.Handle(SaleCompleted)   → e.g. low-stock alert if this sale hit the threshold
                     └─→ Dashboard/UI refresh listeners              → MainShell KPI cards, etc.
```

Steps 10's subscribers run **after** the commit and are allowed to fail independently — a notification bug can never roll back a completed, paid-for sale. Steps 1–9 are the "no half-finished operation" guarantee; step 10 is the "event-driven, decoupled" guarantee. Same shape applies to every other command (Purchase, Return, PaySupplier, CollectFromCustomer, RecordExpense, TransferFunds) — only the specific engines called in steps 3–7 differ.

### Purchase flow (same shape, different engines)

```
CreatePurchaseCommand
   → Validate (supplier exists, IMEIs not already registered, quantities > 0)
   → uow.Begin
   → InventoryEngine.AddStock(...) [+ create ProductUnits for serialized items]
   → IF pay-cash-now: CashDrawerEngine.Debit(method, total)   [InsufficientBalanceException if short]
   → SupplierEngine.UpdateBalance(supplierId, -total or 0 depending on credit/cash)
   → AccountingEngine.Post:
        Dr  1200 المخزون                total
            Cr  Drawer account OR 2100 موردون    total
   → IntegrityEngine.CheckBeforeCommit
   → uow.Commit
   → Publish PurchaseCompleted → Audit, Notification, Dashboard
```

---

## 5. Event Bus

For a single-process WinForms desktop app, the event bus starts as a **simple synchronous in-memory pub/sub** (a dictionary of `Type → List<handler>`, `Publish` just iterates and calls handlers on the same thread). This is intentionally the simplest thing that satisfies "engines only depend on events, not on each other directly." It is *not* a message queue, has no persistence, and does not survive a crash — that's fine, because nothing safety-critical is allowed to depend on it (see §4: the event bus only carries *post-commit* side effects).

When multi-branch/cloud sync is eventually built, `TemoStore.EventBus`'s interface (`IEventBus`) doesn't change — only its implementation swaps from in-memory to something like RabbitMQ/Azure Service Bus/SignalR, because every engine already only depends on the interface. This is the concrete payoff of the "loose coupling" requirement.

### Event catalog (initial set)
`SaleCompleted`, `PurchaseCompleted`, `ReturnCompleted`, `SupplierPaymentRecorded`, `CustomerPaymentRecorded`, `ExpenseRecorded`, `FundsTransferred`, `StockBelowThreshold`, `DayClosureRequested`, `BackupCompleted`, `BackupFailed`.

---

## 6. Accounting Engine — proper double-entry (this is the part that directly serves "محاسبيا صح وبدون أخطاء")

**Current state**: `CashMovements` (single row per cash effect, tagged with a payment method) + `AccountsTree` (account codes/names) already exist and already have a real chart of accounts seeded — checked directly in `DatabaseManager.cs`:

```
1100 نقدي - الخزينة      1300 عملاء (ذمم مدينة)
1110 فوري                2100 موردون (ذمم دائنة)
1120 أمان                4100 إيراد المبيعات
1130 سهولة               4200 إيراد الصيانة
1140 فودافون كاش         5100 مصروفات عمومية وإدارية
1150 إنستاباي            5200 إيجار / 5300 كهرباء ومياه / 5400 مرتبات
1200 المخزون (البضاعة)
```

This is genuinely useful — the payment-method accounts (1100–1150) map directly to `PaymentMethodBalances`, and 1200/1300/2100/4100 already anticipate proper double-entry. **Missing today: a COGS (تكلفة البضاعة المباعة) account** — recommend adding `5500 تكلفة البضاعة المباعة` when the Accounting Engine is built.

**New tables required:**

```sql
CREATE TABLE JournalEntries (
    JournalEntryId INTEGER PRIMARY KEY AUTOINCREMENT,
    EntryDate      TEXT NOT NULL,
    SourceType     TEXT NOT NULL,   -- 'Sale' | 'Purchase' | 'Expense' | 'Transfer' | 'SupplierPayment' | 'CustomerPayment'
    SourceId       INTEGER,         -- SaleId / PurchaseId / etc., for traceability back to the business record
    Description    TEXT,
    CreatedAt      TEXT NOT NULL,
    CreatedBy      TEXT             -- username, from AuthManager
);

CREATE TABLE JournalLines (
    JournalLineId   INTEGER PRIMARY KEY AUTOINCREMENT,
    JournalEntryId  INTEGER NOT NULL REFERENCES JournalEntries(JournalEntryId),
    AccountCode     INTEGER NOT NULL REFERENCES AccountsTree(AccountCode),
    Debit           REAL NOT NULL DEFAULT 0,
    Credit          REAL NOT NULL DEFAULT 0
);
```

`IAccountingEngine.Post(request)` **must** throw `AccountingImbalanceException` and refuse to write anything if `SUM(line.Debit) != SUM(line.Credit)` for the entry — this is the single rule that makes "correct accounting, no errors" enforceable in code rather than just a hope. `PaymentMethodBalances`/`CashMovements` are kept as-is underneath as a fast "current cash position" cache (today's screens already read/write them everywhere), while `JournalEntries`/`JournalLines` becomes the authoritative ledger sitting alongside it — reconciliation between the two is one of `IHealthEngine`'s checks (§ below).

---

## 7. Recovery Engine — honest scope

Worth being precise here: SQLite transactions are already atomic. If the app crashes mid-`CreateSaleCommand`, the `SqliteTransaction` was never committed, so the database is automatically left exactly as it was before — no partial write is possible today, and none will be possible under the new architecture either, because of the `uow.Commit()` gate in §4. So "resume a half-finished DB operation on restart" is already solved by using transactions correctly — there's no separate recovery mechanism needed *for the database itself*.

What a `RecoveryEngine` actually has real work to do on is **post-commit side effects that aren't atomic with the DB write** — e.g., a future "sync this sale to the cloud" or "send WhatsApp receipt" step that might not have finished before a crash. The honest design here is an **outbox table**: anything in §4 step 10 that must eventually happen writes an `OutboxMessages` row *inside the same transaction* as the business write (so it's guaranteed to exist if the sale exists), and `IRecoveryEngine.ResumeOrRollbackPendingOperations()` on startup just re-drives any outbox rows that never got marked "delivered." This is a real, buildable engine — just scoped correctly to what SQLite doesn't already give for free.

---

## 8. What needs to change in the existing database

- Add `JournalEntries` / `JournalLines` tables (§6).
- Add account code `5500 تكلفة البضاعة المباعة` to `AccountsTree`.
- Add `OutboxMessages` table (§7) — only needed once a post-commit side effect that isn't already synchronous exists (none currently is, so this can wait until it's actually needed).
- Everything else already in the schema (`Products`, `Sales`, `Purchases`, `ProductUnits`, `CashMovements`, `PaymentMethodBalances`, `AccountsTree`, `Customers`, `Suppliers`, `CashMovements.LinkedMovementId`/`PurchaseId`/`SaleId` added this session) is reused as-is by the new `TemoStore.Data` repositories — this is a re-architecture of *how code talks to the database*, not a redesign of the database itself.

---

## 9. Testing strategy

Every engine takes its dependencies (repositories, other engines it calls) as constructor-injected interfaces (`IProductRepository`, `ICashDrawerEngine`, etc.) — no `static class` pattern like today's `*Repository.cs`, no `new SqliteConnection(...)` inside engine code. That makes each engine testable in isolation with fakes/mocks, e.g.:

```csharp
[Fact]
public void DeductStock_Throws_When_Insufficient()
{
    var repo = new FakeProductRepository(stock: 2);
    var engine = new InventoryEngine(repo);
    Assert.Throws<InsufficientStockException>(() => engine.DeductStock("BARCODE", 5, fakeUow));
}
```

`TemoStore.Tests` gets one test class per engine from day one of that engine's implementation — not retrofitted later.

---

## 10. Open decisions before Engine #1 starts

1. **Migration strategy** — confirm incremental/strangler-fig (§0) over big-bang. Recommended.
2. **First vertical slice** — confirm **Sales** (Core Engine + Event Bus + Validation + Inventory + CashDrawer + Accounting + Audit, wired end-to-end for one command) as the first thing actually built and proven, rather than building all 15 engine *shells* first with nothing running through them.
3. **DI container** — recommend `Microsoft.Extensions.DependencyInjection` (already the .NET-standard choice, zero extra dependency weight, works fine in a WinForms `Program.cs` composition root).
4. **ORM vs raw ADO.NET in `TemoStore.Data`** — recommend keeping `Microsoft.Data.Sqlite` + hand-written SQL (what the whole app already uses, team already knows it, zero new dependency/learning curve) rather than introducing EF Core at the same time as this rewrite. Can revisit later; not required for any of the architectural goals here.
