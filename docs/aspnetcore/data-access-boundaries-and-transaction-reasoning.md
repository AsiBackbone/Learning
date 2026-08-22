---
description: Learn how to place EF Core behind clear boundaries, choose transaction scopes, use interceptors carefully, and separate database work from external effects.
---

# Data-Access Boundaries and Transaction Reasoning with EF Core

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** Basic familiarity with ASP.NET Core dependency injection and EF Core. [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md), [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md), and [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) provide useful governance context.

**Learning objective:** Decide where EF Core belongs in an application, distinguish persistence abstractions from unnecessary wrappers, choose transaction boundaries deliberately, use interceptors only where their lifecycle fits the concern, test relational behavior with an appropriate provider, and preserve the boundary between a local database transaction and an external side effect.

## Pattern Card

> **Problem:** Durable governance state eventually has to live somewhere, but adding EF Core can blur responsibilities. A `DbContext` may leak into policy code, repository layers may hide EF Core without adding a useful boundary, interceptors may become invisible business logic, and a local database transaction may be mistaken for a guarantee about a remote side effect.
>
> **Pattern:** Keep persistence at an application/infrastructure boundary. Use `DbContext` directly when it already expresses the required unit of work; introduce a repository or store interface when it creates a meaningful domain, testing, provider, or ownership boundary. Use the smallest transaction that makes related **local** state atomic, and model remote effects with separate idempotency, messaging, or recovery semantics.
>
> **Use when:** An ASP.NET Core application must persist governance decisions, audit residue, capability-use state, workflow state, or other durable data and the correctness of those writes matters to later execution.
>
> **Prefer something simpler when:** The application has trivial persistence, one request-scoped `DbContext`, one `SaveChangesAsync` call, and no requirement to abstract the store or coordinate several local writes. Do not add repositories, explicit transactions, or interceptors merely to match a pattern catalog.
>
> **Observe:** A transaction can make related writes in one relational database atomic. It cannot, by itself, make an HTTP request, message delivery, email, cloud control-plane call, or physical action part of that same atomic commit.

The central rule is:

> **State the transaction boundary precisely.**

A second rule follows:

> **Persistence abstractions should expose a real architectural boundary, not merely rename EF Core.**

---

## Start with the Persistence Question, Not the EF Core API

The Learning repository intentionally begins many examples with in-memory state because memory keeps the architectural lesson visible.

That is useful for teaching:

```text
Decision
   ↓
Acknowledgment
   ↓
Capability
   ↓
Execution
   ↓
Audit residue
```

But process memory has obvious limits.

A production application may restart.

Several application instances may participate.

A later investigation may need evidence after the request has ended.

A replay-sensitive capability may remain valid longer than one process lifetime.

At that point the architectural question becomes:

> **Which state must survive, who owns it, and what consistency does the next decision depend on?**

Only after answering that question should the application choose a persistence mechanism.

EF Core is one practical option for relational persistence in .NET.

It is not the architecture by itself.

---

## Keep the Layers Distinct

A useful conceptual separation is:

```text
Governance / application rule
        ↓
Persistence requirement
        ↓
Persistence boundary
        ↓
EF Core / relational provider
        ↓
Database
```

For example:

```text
Rule:
A one-time capability cannot be consumed twice.

Persistence requirement:
Prior use must survive restart and be shared by all execution nodes.

Boundary:
ICapabilityUseStore.TryConsumeAsync(...)

Implementation:
EfCoreCapabilityUseStore

Provider:
SQLite locally / production relational provider in deployment
```

The rule should not become:

```csharp
if (dbContext.CapabilityUses.Any(...))
{
    ...
}
```

inside a policy evaluator merely because EF Core is available.

That would couple policy semantics to a storage mechanism and make the state transition easier to misuse.

---

## `DbContext` Already Represents a Unit of Work

EF Core's `DbContext` is designed around a short-lived unit of work.

A typical request-oriented flow is:

```text
Create / resolve DbContext
        ↓
Load entities
        ↓
Change tracked state
        ↓
SaveChangesAsync
        ↓
Dispose context
```

That matters when deciding whether to add another unit-of-work abstraction.

A wrapper such as:

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken);
}
```

may add little if every implementation simply delegates to:

```csharp
_dbContext.SaveChangesAsync(cancellationToken)
```

The extra type is not automatically harmful.

But it should earn its place by expressing something the application actually needs, such as:

- A domain-level commit boundary independent of EF Core.
- Coordination across several persistence abstractions owned by the same application boundary.
- A restricted surface that prevents application code from reaching arbitrary tables.
- A testing seam where excluding EF Core from a class is materially useful.
- A provider-neutral contract that has more than one legitimate implementation.

If none of those apply, direct `DbContext` usage may be clearer.

---

## Direct `DbContext` Usage Can Be the Better Boundary

Suppose an application service owns a simple relational mutation:

```csharp
public sealed class AccountApplicationService
{
    private readonly ApplicationDbContext _dbContext;

    public AccountApplicationService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task DisableAsync(
        string accountId,
        CancellationToken cancellationToken)
    {
        Account account = await _dbContext.Accounts
            .SingleAsync(
                item => item.Id == accountId,
                cancellationToken);

        account.Disable();

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}
```

If this service is already the application boundary and the persistence model is intentionally EF Core-based, another repository may only create:

```text
Application service
        ↓
AccountRepository
        ↓
DbContext
```

where `AccountRepository` contains nothing except:

```csharp
return _dbContext.Accounts.SingleAsync(...);
```

That can make the architecture harder to follow without changing ownership or semantics.

The question is not:

> Are repositories good or bad?

The question is:

> **What boundary would this repository make explicit?**

---

## When a Repository or Store Interface Adds Value

A repository or store becomes more meaningful when the application wants to expose **application semantics** rather than EF Core operations.

Replay protection is a good example.

The application does not merely need:

```text
Insert CapabilityUse row
```

It needs:

> Determine whether this authority may still be consumed and atomically claim one permitted use.

A useful boundary is therefore:

```csharp
public interface ICapabilityUseStore
{
    ValueTask<CapabilityUseResult> TryConsumeAsync(
        string capabilityId,
        int maximumUses,
        DateTimeOffset usedUtc,
        CancellationToken cancellationToken);
}
```

This interface says something about the domain and consistency contract.

It does not expose:

```csharp
DbSet<CapabilityUseRecord>
IQueryable<CapabilityUseRecord>
DbContext
```

The EF Core implementation can remain an infrastructure concern.

That is a real abstraction because callers depend on:

```text
TryConsume semantics
```

rather than:

```text
how rows are queried and updated
```

---

## A Repository Should Not Leak `IQueryable` by Accident

A repository that returns `IQueryable<T>` often gives the caller the ability to define database queries anyway:

```csharp
public IQueryable<Account> Query() =>
    _dbContext.Accounts;
```

The caller now controls:

- Query shape.
- Provider translation.
- Loading strategy.
- Deferred execution.
- Potentially tracking behavior.

That may be intentional.

But if the purpose of the repository was to hide EF Core or constrain persistence behavior, the abstraction has not accomplished much.

Prefer methods that expose the operation the caller needs when the repository is intended to be a real boundary:

```csharp
Task<Account?> FindForDisableAsync(...)
ValueTask<CapabilityUseResult> TryConsumeAsync(...)
Task AppendAsync(AuditResidue residue, ...)
```

Do not create one method per `DbSet` operation simply to avoid naming `DbContext`.

---

## One `SaveChanges` Call Already Has Transaction Semantics

For a database provider that supports transactions, EF Core applies the changes in a single `SaveChanges` call transactionally.

Conceptually:

```text
Tracked change A
Tracked change B
Tracked change C
        ↓
SaveChangesAsync
        ↓
One database transaction
        ↓
All commit
or
all roll back
```

This means an explicit transaction is not automatically required for every write path.

If the complete local unit of work can be represented before one `SaveChangesAsync` call, the default transaction may already be the clearest solution.

Example:

```csharp
CapabilityUseRecord use = ...;
ExecutionResidueRecord residue = ...;

_dbContext.CapabilityUses.Add(use);
_dbContext.ExecutionResidues.Add(residue);

await _dbContext.SaveChangesAsync(
    cancellationToken);
```

If both rows belong to the same relational database and both are part of the same save, the database can commit or roll them back together.

The important phrase is:

> **the same relational database transaction**

not:

> every consequence of the operation everywhere.

---

## Use an Explicit Transaction When the Local Unit of Work Spans Several Saves

Sometimes the application cannot construct the required local state in one save.

For example:

```text
Create local operation record
        ↓
Save to obtain database-generated identifier
        ↓
Create dependent local record
        ↓
Save again
```

If both writes must succeed or fail together, an explicit transaction can make that boundary visible:

```csharp
await using var transaction =
    await dbContext.Database.BeginTransactionAsync(
        cancellationToken);

try
{
    dbContext.Operations.Add(operation);
    await dbContext.SaveChangesAsync(cancellationToken);

    dbContext.ExecutionResidues.Add(
        CreateResidue(operation.Id));

    await dbContext.SaveChangesAsync(cancellationToken);

    await transaction.CommitAsync(cancellationToken);
}
catch
{
    await transaction.RollbackAsync(
        CancellationToken.None);

    throw;
}
```

The application should be able to explain why the transaction is wider than one save.

Do not begin an explicit transaction around every request merely because the API exists.

Longer transactions can increase:

- Lock duration.
- Contention.
- Retry complexity.
- Deadlock exposure.
- Coupling between unrelated persistence work.

Use the smallest transaction that preserves the invariant you actually require.

---

## Model the Invariant Before Choosing the Isolation Technique

Before choosing transaction APIs or isolation levels, write the invariant in plain language.

For one-time capability consumption:

> **At most one competing attempt may claim the permitted use.**

For local audit pairing:

> **If capability consumption is committed, the execution-start residue must also be committed.**

For an account update plus local outbox message:

> **The account mutation and the intent to publish its event must become durable together.**

Once the invariant is explicit, choose the provider mechanism that actually enforces it.

Possible mechanisms include:

- A unique constraint.
- An atomic conditional update.
- An optimistic concurrency token.
- A row lock or serialization strategy supported by the provider.
- An explicit transaction around several local writes.

The mechanism is provider-specific.

The invariant is architectural.

---

## Avoid Check-Then-Act Replay Logic

Do not implement one-time use as:

```csharp
bool alreadyUsed = await dbContext.CapabilityUses
    .AnyAsync(
        use => use.CapabilityId == capabilityId,
        cancellationToken);

if (!alreadyUsed)
{
    dbContext.CapabilityUses.Add(
        new CapabilityUseRecord(capabilityId));

    await dbContext.SaveChangesAsync(
        cancellationToken);
}
```

Two concurrent requests can both observe:

```text
alreadyUsed = false
```

before either commits.

A one-time-use relational design might instead enforce uniqueness on the capability identifier and treat the insert as the claim:

```text
UNIQUE(CapabilityId)
```

Then:

```text
First insert
   ↓
Commit succeeds

Competing insert
   ↓
Unique constraint rejects duplicate
```

The application still needs to translate the provider-specific failure into its stable `CapabilityUseResult` contract.

For bounded-use counts greater than one, a conditional atomic update or another provider-supported concurrency strategy may be more appropriate.

The important lesson is unchanged:

> **Persistence must enforce the state transition under concurrency.**

---

## Atomic Local Persistence Can Join Related Governance State

Suppose the same application database owns both:

```text
Capability-use state
Execution-start residue
```

The host may require:

```text
Consume capability
        +
Write execution-start residue
        ↓
Commit together
```

A local relational transaction can be a good fit.

Conceptually:

```text
Begin transaction
        ↓
Claim permitted capability use
        ↓
Write execution-start residue
        ↓
Commit
```

If either local write fails:

```text
Rollback
        ↓
Neither local state transition is committed
```

That is a strong and useful guarantee.

It is also a **local** guarantee.

---

## The Database Transaction Ends at the Database Boundary

Now add a remote side effect:

```text
Begin database transaction
        ↓
Consume capability
        ↓
Write execution-start residue
        ↓
Call external service
        ↓
Commit database transaction
```

This does not make the external service part of the relational transaction.

The remote system may have committed independently before the local commit.

The local database may commit while the network call never completed.

The process may crash between any two steps.

The architecture must therefore distinguish:

```text
Local transactional consistency
```

from:

```text
Cross-system execution consistency
```

They are different problems.

---

## Failure Window: Commit Local State Before the External Call

Consider:

```text
Capability marked consumed
        ↓
Execution-start residue written
        ↓
Database transaction commits
        ↓
Process crashes
        ↓
External operation never starts
```

The database is internally consistent.

But the capability has been consumed without a completed external effect.

That may be the correct security tradeoff for one-time authority.

Recovery now needs another state or workflow, for example:

```text
AuthorityConsumed
ExecutionNotStarted
RecoveryRequired
```

The database transaction did not solve the external execution lifecycle.

---

## Failure Window: External Operation Succeeds Before the Local Record

Consider the opposite order:

```text
External operation succeeds
        ↓
Database update begins
        ↓
Database write fails
```

The database may still say:

```text
Not completed
```

while the external world has changed.

Blind retry may repeat the side effect.

This is why a local transaction should never be described as an exactly-once guarantee for an unrelated external system.

---

## Idempotency Is a Separate Boundary

Idempotency asks:

> **If the same logical operation is attempted again, can the system avoid repeating or corrupting the effect?**

That is different from capability replay protection, which asks:

> **Should this authority artifact be accepted again?**

The two can cooperate.

For example:

```text
CapabilityId = cap-123
OperationId  = op-456
```

The host may use:

```text
CapabilityId
   ↓
Replay / bounded-use state
```

and separately:

```text
OperationId
   ↓
Idempotency / recovery state
```

One valid capability can authorize one attempt while the external operation still needs retry-safe semantics.

Conversely, an idempotent external operation does not make an already-consumed capability acceptable again.

---

## Use an Outbox When the Boundary Is Durable Messaging

If the external boundary is a message broker, an outbox pattern can make **local state plus message intent** durable in one database transaction:

```text
Begin transaction
   ├── consume capability
   ├── update local state
   └── insert outbox message
        ↓
Commit
        ↓
Background dispatcher publishes message
```

The outbox does not make the broker transaction identical to the database transaction.

It creates a durable local record that the message should be delivered.

The dispatcher can retry delivery.

The consumer may still need inbox/deduplication or idempotent processing.

The architecture becomes a sequence of owned state transitions rather than one fictional global transaction.

---

## Recovery and Reconciliation Are First-Class Outcomes

Consequential workflows benefit from explicit recovery states such as:

```text
Pending
AuthorityConsumed
DispatchPending
ExecutionStarted
OutcomeUnknown
Completed
Failed
RecoveryRequired
```

These states are often more useful than a broad claim of:

```text
Exactly once
```

A recovery path can then decide whether to:

- Query the external provider.
- Retry with the same idempotency key.
- Reconcile the current resource state.
- Publish an outbox item again.
- Create a new governed operation.
- Escalate to a human reviewer.
- Mark a terminal failure.

Durable persistence makes this reasoning possible because the process does not have to remember everything in memory.

---

## `SaveChanges` Interceptors Are Powerful Cross-Cutting Hooks

EF Core exposes `ISaveChangesInterceptor` / `SaveChangesInterceptor` lifecycle hooks around save operations.

These can observe or influence `SaveChanges` and `SaveChangesAsync`.

That makes interceptors attractive for cross-cutting behavior.

Possible uses include:

- Canonicalizing persisted values.
- Normalizing timestamps.
- Updating application-managed concurrency fields.
- Creating generic database-change audit records.
- Enforcing a persistence convention that truly applies to every save in the configured context.

But an interceptor is not automatically the right place for every concern that happens near persistence.

---

## Prefer Explicit Application Code When the Behavior Is Workflow-Specific

Suppose a governance workflow reaches:

```text
Decision = Allowed
```

and the host needs to write:

```text
DecisionId
PolicyVersion
CapabilityId
ExecutionStage
ReasonCodes
```

A generic `SaveChangesInterceptor` may not have the complete semantic context that created those values.

Trying to infer them from modified entities can turn the interceptor into hidden business logic.

Prefer explicit code when the operation is meaningful because of the workflow:

```csharp
await auditResidueStore.AppendAsync(
    AuditResidue.ExecutionStarted(...),
    cancellationToken);
```

This keeps the governance event visible at the boundary where it happened.

A useful distinction is:

```text
Generic persistence concern
        ↓
Potential interceptor candidate
```

versus:

```text
Specific governance lifecycle event
        ↓
Prefer explicit application behavior
```

---

## Generic Database Auditing Is Not Governance Audit Residue

A database audit record may say:

```text
Table: Accounts
Row: 123
Column: IsDisabled
Old: false
New: true
```

That can be useful.

Governance audit residue may say:

```text
DecisionId: dec-42
Outcome: Allowed
ReasonCodes: [...]
PolicyVersion: account-disable/7
CapabilityId: cap-123
Stage: execution-completed
```

Those records answer different questions.

Database auditing asks:

> What persisted data changed?

Governance residue asks:

> What happened in the governed decision and execution lifecycle?

A system may need both.

Do not assume one automatically replaces the other.

---

## Interceptors Can Create Ordering Dependencies

Several independent interceptors may all modify state before a save.

Now registration order can influence behavior:

```text
Canonicalization interceptor
Timestamp interceptor
Concurrency interceptor
Audit interceptor
```

If the audit snapshot runs before normalization, it may record values that are not the values actually persisted.

If concurrency values change after audit capture, evidence can become confusing.

This is why cross-cutting save behavior should have an explicit ordering strategy when order matters.

The current `NetCoreApplicationTemplate` is one concrete reference: it uses a composite save interceptor that delegates to an application-owned save pipeline so ordering among canonicalization, normalization, timestamps, concurrency values, and audit capture remains deliberate.

That is one architecture, not a universal requirement.

A smaller application may be clearer with:

- One `DbContext.SaveChangesAsync` override.
- One interceptor.
- Explicit application code.
- No cross-cutting save customization at all.

Choose the smallest design whose ordering is understandable.

---

## Do Not Use an Interceptor Merely for Logging

Interceptors can inspect database operations, but ordinary logging and diagnostics are often better suited to observation that does not need to modify or suppress EF Core behavior.

Ask:

> Does this concern need to participate in the EF Core operation, or does it only need to observe it?

If the answer is only observation, prefer the simpler diagnostic mechanism.

Hidden mutation inside an interceptor should be justified by a clear cross-cutting requirement.

---

## Persistence Failures Need an Application Boundary Too

Data access can fail because of:

- Connectivity loss.
- Timeout.
- Deadlock or transient provider failure.
- Unique-constraint violation.
- Optimistic concurrency conflict.
- Invalid query or mapping.
- Disk exhaustion.
- Migration/schema mismatch.
- Permission failure.

Do not convert every provider exception into the same application meaning.

Some failures are expected concurrency outcomes.

Some are dependency unavailability that the application may deliberately map to a retry/defer state.

Some are programming or deployment defects that should reach centralized error handling.

A useful boundary is:

```text
Provider exception
        ↓
Persistence implementation understands provider semantics
        ↓
Application-specific result when meaning is known
        or
Exception crosses to centralized handler when it is not
```

For example, an EF Core implementation of `TryConsumeAsync` may recognize a unique-constraint violation for the capability key and return:

```text
UseLimitExceeded
```

That is an expected replay-state result.

A broken connection is different.

The host may choose to:

```text
Defer
Fail closed
Return dependency unavailable
Queue for later processing
```

according to the operation's requirements.

Do not treat "database unavailable" as "capability unused."

---

## Keep Provider Exceptions Out of Governance Models

Avoid a governance result shaped like:

```csharp
public sealed record GovernanceDecision(
    GovernanceDecisionOutcome Outcome,
    SqliteException? DatabaseException);
```

The decision model should remain about the governed operation.

Provider-specific details belong inside the persistence/diagnostic boundary.

If a known persistence condition affects governance, translate it to an application concept:

```text
Capability state unavailable
```

or:

```text
Concurrent modification detected
```

with a stable reason code where appropriate.

This keeps the governance vocabulary portable across SQLite, SQL Server, PostgreSQL, or another store.

---

## Local Development Storage Is a Convenience, Not a Production Guarantee

SQLite is useful for small local and CI-friendly relational examples because it is lightweight and still exercises relational behaviors such as:

- Constraints.
- Transactions.
- SQL translation.
- Relational schema.

That makes it more suitable than a pure in-memory collection when the lesson is transaction-oriented.

But SQLite is not a perfect substitute for another production database.

Providers can differ in:

- SQL translation.
- Type behavior.
- Case sensitivity and collation.
- Concurrency features.
- Locking.
- Isolation.
- Provider-specific functions.
- Migrations and DDL behavior.

Therefore:

```text
SQLite test passes
```

should mean:

> The relational teaching invariant works against SQLite.

It should not automatically mean:

> The application has proven identical behavior against every production provider.

---

## Why the EF Core In-Memory Provider Is Weak for Transaction Tests

The EF Core in-memory provider is useful only for narrow scenarios where its limitations are acceptable.

It is not a relational database.

In particular, it does not reproduce relational transaction and query behavior faithfully enough for a tutorial whose subject is transaction reasoning.

A test such as:

```text
Begin transaction
Write A
Write B
Rollback
Assert neither persisted
```

needs a provider whose transaction semantics actually exist.

For this Learning topic, prefer:

```text
SQLite relational test
```

over:

```text
EF Core InMemory transaction test
```

when a small self-contained provider is needed.

---

## SQLite In-Memory Mode Can Be Useful in CI

SQLite can also run in-memory while still using the relational provider.

One practical testing shape is:

```text
Open SQLite connection
        ↓
Keep connection open for test lifetime
        ↓
Create schema
        ↓
Run EF Core integration test
        ↓
Close connection
```

The open connection matters because an in-memory SQLite database is tied to its connection lifetime.

This can provide fast isolated tests without leaving database files behind.

It remains SQLite, so provider differences still apply.

---

## Test the Production Provider Where Its Semantics Matter

If the production application relies on behavior specific to its deployed database, at least some tests should run against that provider.

Examples include:

- Provider-native concurrency tokens.
- Provider-specific SQL/functions.
- Isolation behavior.
- Locking behavior.
- Migration behavior.
- Query translation that differs from SQLite.

A containerized database can be useful for those tests.

Testcontainers is one option for orchestrating disposable database instances in integration tests.

It should be viewed here as a production-oriented comparison, not a required Learning dependency.

The teaching progression can remain:

```text
Small SQLite relational test
        ↓
Understand the invariant
        ↓
Production-provider integration test where semantics matter
```

---

## A Focused Transaction Test Should Prove the Invariant

Avoid tests that merely prove EF Core can insert an entity.

For transaction reasoning, assert the architectural behavior.

Example:

```text
Begin local unit of work
        ↓
Claim capability use
        ↓
Write execution-start residue
        ↓
Force second write to fail
        ↓
Rollback
        ↓
Capability use absent
Execution residue absent
```

Another useful test:

```text
Two consumers race for one-time capability
        ↓
Exactly one local claim succeeds
        ↓
Other receives UseLimitExceeded
```

Be careful with the word `exactly` here.

The test proves exactly one **database claim** succeeds under the tested provider and concurrency setup.

It does not prove the external side effect occurs exactly once.

---

## Test the External Failure Window Separately

A local transaction test cannot prove recovery from a remote ambiguous outcome.

Model that separately:

```text
Capability consumed
        ↓
External operation reports timeout
        ↓
Outcome unknown
```

Then test the recovery policy:

```text
Query provider status
or
retry with same idempotency key
or
escalate
```

This keeps the test vocabulary aligned with the ownership boundary.

---

## A Small Persistence Architecture

One intentionally narrow design could look like:

```text
ASP.NET Core endpoint
        ↓
Governed application service
        ↓
Governance decision
        ↓
Execution-boundary service
        ↓
ICapabilityUseStore
IAuditResidueStore
        ↓
EF Core implementations
        ↓
ApplicationDbContext
        ↓
Relational database
```

The governance evaluator does not receive `ApplicationDbContext`.

The executor does not need arbitrary access to every table.

The persistence implementation owns provider translation and atomic state transitions.

The host still owns the real side effect.

That is the boundary this tutorial is trying to preserve.

---

## Example: Local Transaction Around Capability Consumption and Residue

A coordinator can make the intended local transaction explicit:

```csharp
public sealed class ExecutionPersistenceCoordinator
{
    private readonly ApplicationDbContext _dbContext;
    private readonly EfCoreCapabilityUseStore _useStore;
    private readonly EfCoreAuditResidueStore _auditStore;

    public async Task<CapabilityUseResult> TryStartAsync(
        string capabilityId,
        AuditResidue executionStart,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        CapabilityUseResult use =
            await _useStore.TryConsumeAsync(
                capabilityId,
                maximumUses: 1,
                DateTimeOffset.UtcNow,
                cancellationToken);

        if (!use.Accepted)
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            return use;
        }

        await _auditStore.AppendAsync(
            executionStart,
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return use;
    }
}
```

Treat this as a boundary sketch, not drop-in production code.

The exact implementation depends on whether the stores call `SaveChanges` internally, whether they share one `DbContext`, how retries are handled, and how the provider atomically claims use state.

The important review questions are:

1. Do both stores participate in the same `DbContext`/connection/transaction?
2. Is capability consumption actually atomic under concurrency?
3. What happens if the transaction result is ambiguous to the caller?
4. Is the external executor called before or after local commit?
5. What recovery state remains if the external call fails?

---

## Do Not Hide Transaction Ownership Across Repositories

A common repository design lets every repository call `SaveChangesAsync` internally:

```text
CapabilityRepository.Save()
AuditRepository.Save()
AccountRepository.Save()
```

Now the application may not know whether those operations are part of one transaction or three independent commits.

If several repositories must participate in one local unit of work, transaction ownership should be visible somewhere.

Options include:

- A shared `DbContext` with one application-level `SaveChangesAsync`.
- An explicit coordinator that owns the transaction.
- A domain-specific persistence service that owns the complete local invariant.

Avoid accidental transaction boundaries caused by whichever repository happens to save first.

---

## `DbContext` Lifetime Is Part of the Boundary

A `DbContext` is not thread-safe and should not be treated as a singleton cache.

In an ASP.NET Core application, a scoped context commonly aligns with one request-oriented unit of work.

Background processing may need a separate context per operation or an `IDbContextFactory<TContext>`-style boundary.

The important principle is:

```text
One logical unit of work
        ↓
One intentionally owned context lifetime
```

not:

```text
One global context shared by unrelated concurrent operations
```

Context lifetime, transaction lifetime, and workflow lifetime are related but not necessarily identical.

A long-running acknowledgment workflow should not keep one `DbContext` open for hours.

Persist the workflow state, release the context, and reconstruct current state when the workflow resumes.

---

## Long-Running Governance Workflows Should Persist State, Not Hold Transactions Open

Consider:

```text
Decision = AcknowledgmentRequired
        ↓
Wait 20 minutes for human response
        ↓
Resume
```

Do not keep a database transaction open for the 20-minute pause merely to preserve continuity.

Instead:

```text
Persist challenge + decision evidence
        ↓
Commit
        ↓
Release request / DbContext
        ↓
Later response arrives
        ↓
Load durable state
        ↓
Validate and re-evaluate
```

The workflow can be long-lived.

The transaction should remain short-lived.

This preserves database health and current-state reasoning at continuation.

---

## Decision Evidence and Persistence Evidence Are Different

A successful `SaveChangesAsync` proves something about the database operation.

It does not prove:

- The policy was correct.
- The actor was authorized.
- The remote side effect occurred.
- The audit record is tamper-evident.
- The event was delivered to an external sink.
- The complete workflow is recoverable.

Likewise, a governance decision marked `Allowed` does not prove the database commit succeeded.

Keep these stages distinct:

```text
Governance decision
        ↓
Persistence attempt
        ↓
Local commit result
        ↓
External execution attempt
        ↓
Execution result / recovery state
```

That separation makes incident analysis far easier.

---

## When a Simpler Persistence Boundary Is Better

Do not introduce repositories, explicit transaction coordinators, save interceptors, outboxes, or durable capability-use state merely because EF Core can support them.

A smaller design is usually preferable when:

- One `DbContext` and one `SaveChangesAsync` already define the local unit of work.
- One application service owns the mutation clearly.
- No invariant spans several persistence abstractions.
- No durable replay, use-limit, or acknowledgment state is required.
- No remote side effect creates an ambiguous post-commit recovery window.

In those cases, framework-native EF Core transactions and an ordinary application-service boundary may express the behavior more clearly than additional persistence abstractions.

Introduce the heavier boundaries only when a concrete invariant requires them, such as atomic local writes across concerns, ordered save-pipeline behavior, durable replay or use state, reliable asynchronous handoff, or explicit recovery after external side effects.

Use the smallest persistence model that makes the real transaction and recovery ownership visible.

---

## Working Reference: NetCoreApplicationTemplate

`NetCoreApplicationTemplate` is a useful working specimen because it contains several of the boundaries discussed here without requiring Learning to reproduce the entire implementation.

Relevant references include:

- [`Data Access`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/articles/data-access.md) — provider selection, SQLite local development, migrations, auditing, and disabled data-access mode.
- [`EF Core Save Pipeline`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/articles/ef-core-save-pipeline.md) — composite save interceptor and explicit ordering of cross-cutting persistence behavior.
- [ADR 0004: Keep the Composite EF Core SaveChanges Interceptor](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/adr/0004-keep-composite-savechanges-interceptor.md) — records why the template currently keeps ordered save concerns behind one composite interceptor and which concrete extension or maintenance needs would justify revisiting that repository-specific choice.
- [`ApplicationDbContext`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Infrastructure/Data/ApplicationDbContext.cs) — the fuller EF Core context.
- [`ApplicationSaveChangesInterceptor`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Infrastructure/Data/ApplicationSaveChangesInterceptor.cs) — the save-lifecycle interception boundary.
- [`ApplicationAuditedTransaction`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Infrastructure/Data/Auditing/ApplicationAuditedTransaction.cs) — explicit relational transaction coordination for a local application mutation and audit/completion state.
- [`Audit Completion Outbox`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/articles/audit-completion-outbox.md) — a fuller example of durable local handoff toward asynchronous delivery.

These references demonstrate one production-oriented architecture.

They are not requirements for every Learning example.

---

## Working Reference: AsiBackbone

The `AsiBackbone` repository provides the governance-side abstractions that make persistence questions concrete.

Relevant references include:

- [`GovernanceDecision`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecision.cs) — carries policy identity and structured outcomes without owning persistence.
- [`AuditResidue`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Audit/AuditResidue.cs) — provider-neutral governance evidence that can be persisted by a host-selected durable store.
- [`CapabilityTokenGrant`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityTokenGrant.cs) — carries scoped authority metadata while leaving storage and execution ownership to the host.

The architectural bridge is:

```text
Provider-neutral governance model
        ↓
Host persistence abstraction
        ↓
EF Core implementation when appropriate
```

not:

```text
Governance model
        ↓
Hard-coded relational provider
```

---

## Official EF Core References

For the EF Core behavior used in this tutorial, see:

- [DbContext lifetime, configuration, and initialization](https://learn.microsoft.com/ef/core/dbcontext-configuration/)
- [Using transactions](https://learn.microsoft.com/ef/core/saving/transactions)
- [Interceptors](https://learn.microsoft.com/ef/core/logging-events-diagnostics/interceptors)
- [Testing EF Core applications](https://learn.microsoft.com/ef/core/testing/)
- [Choosing a testing strategy](https://learn.microsoft.com/ef/core/testing/choosing-a-testing-strategy)
- [Testing without your production database system](https://learn.microsoft.com/ef/core/testing/testing-without-the-database)

---

## Review Checklist

Before calling a persistence design complete, ask:

1. Which application state actually needs durability?
2. Which component owns that state?
3. Is `DbContext` itself a sufficient persistence boundary?
4. If a repository exists, what semantics does it add beyond forwarding EF Core calls?
5. Does any repository leak `IQueryable` while claiming to isolate the persistence model?
6. Where is `SaveChangesAsync` called?
7. Does one save already provide the required local transaction?
8. If an explicit transaction exists, which invariant requires the wider scope?
9. Are transaction boundaries short-lived?
10. Is one-time or bounded-use capability state enforced atomically under concurrency?
11. Are database constraints/concurrency mechanisms part of the guarantee rather than only application-level checks?
12. Are governance audit residue and generic database-change auditing modeled separately?
13. Does an interceptor contain hidden workflow/business logic?
14. Is interceptor ordering deliberate when several save concerns interact?
15. What happens when the database is unavailable?
16. Can a dependency failure accidentally become permission or "unused" state?
17. What happens if the local commit succeeds but the external operation does not start?
18. What happens if the external operation succeeds but the local result cannot be recorded?
19. Is idempotency modeled separately from capability replay protection?
20. Would an outbox/inbox pattern help at a messaging boundary?
21. Is recovery/reconciliation explicit for ambiguous external outcomes?
22. Are tests exercising relational behavior rather than only in-memory object behavior?
23. Which invariants are proven with SQLite, and which require the production provider?
24. Does any documentation claim "exactly once" beyond what the architecture can actually prove?

If those questions have precise answers, the persistence layer is contributing to the architecture rather than merely storing objects.

---

## What This Tutorial Does Not Claim

This tutorial does not provide:

- A universal repository pattern.
- A complete EF Core course.
- A production capability-use-store implementation.
- A universal isolation-level recommendation.
- A distributed transaction solution.
- Exactly-once execution across external systems.
- A complete outbox/inbox framework.
- A compliance-grade audit system.
- A guarantee that SQLite behaves identically to a production provider.
- A replacement for provider-specific transaction and failure documentation.

The purpose is narrower:

> **Make persistence ownership and transaction scope explicit enough that durable state strengthens the governed-execution boundary instead of obscuring it.**

---

## Related Content

- [Centralized Error Handling and Problem Details](centralized-error-handling-and-problem-details.md) — keep expected persistence outcomes distinct from unexpected application failures and map safe public errors at the host boundary.
- [Build a Governed API Operation lab](../labs/build-a-governed-api-operation.md) — assemble authorization, governance, scoped authority, host-owned execution, and audit residue inside an ASP.NET Core operation.
- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) — review why governance evidence may need durable storage beyond ordinary logs.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — review the execution-boundary authority that durable use state may need to protect.
- [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) — examine atomic capability consumption, durable replay state, idempotency, outbox/inbox reasoning, and exactly-once boundaries in more depth.
- [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) — preserve the policy evidence that produced a durable decision without rewriting historical identity later.
- [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md) — compare richer governance/persistence machinery with cases where ordinary request-local authorization is sufficient.

---

> **Read it. Run it. Question it. Improve it.**
