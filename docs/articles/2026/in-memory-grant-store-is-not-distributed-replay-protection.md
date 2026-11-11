---
description: A one-time grant tracked in each instance's memory can be accepted twice after a retry or restart. Replay protection needs shared, durable, atomic consumption.
title: An In-Memory Grant Store Is Not Distributed Replay Protection
author: Christopher D. Cavell
published: "2026-11-11"
summary: A one-time grant whose use is recorded in each application instance's memory is one-time only per process. A retry that reaches another instance, or a request after a restart, finds no record and accepts the grant again. Replay protection across instances and restarts needs an authoritative store that every executing instance shares, that keeps its records across restarts, and that admits a grant with one atomic conditional write before the protected operation runs. That limits who may run the operation. It does not make the operation idempotent or its external effect exactly-once.
feed: true
x_hashtags:
  - DotNet
  - CyberSecurity
x_question: "Has an in-memory grant or capability store ever let the same operation through twice in production?"
---

# An In-Memory Grant Store Is Not Distributed Replay Protection

**Pattern classification:** General learning material

**Difficulty:** Advanced

**Prerequisites:** The opening scenario needs no specialist background. The implementation sections assume familiarity with ASP.NET Core dependency injection, running more than one instance of a service behind a load balancer, SQL transactions and unique constraints, and basic distributed-system failure modes. No AsiBackbone package or prior Learning material is required.

**Sample scope:** The C# targets .NET 8 or later, and the SQL uses PostgreSQL syntax. Other stores that offer a conditional write or a unique constraint inside a transaction can meet the same contract. The stores, the billing client, the operation records, the helpers, and the tests are illustrative sketches, not a runnable sample, a production-ready pattern, or a recommended schema. The scenario and its incidents are hypothetical. Original prose is licensed under [Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/), and original code examples are additionally offered under the [MIT License](https://opensource.org/license/mit), as described in the repository's [licensing terms](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md). Third-party material, where identified, remains under its original license. External sources were last verified on November 11, 2026.

**What this article covers:** why process-local atomicity stops at the process; two hypothetical incidents; why common fixes narrow the gap without closing it; the store contract; admitting a grant and recording the operation in one write; revocation ordering; where admission belongs, and how retries and status work; how recovery and the original executor share ownership, and the timing values that hold it together; how replay protection differs from idempotency and exactly-once effects; outages, retention, and tests; and when an in-memory store is the right choice.

**In plain terms:** picture a voucher that can be redeemed once, at a shop with two tills. Each cashier keeps a private list of vouchers already redeemed. A customer redeems the voucher at till A, then walks to till B, where the cashier checks a list that has never seen it. At closing time, both lists go in the bin, and tomorrow the voucher looks new again everywhere. The voucher was never one-time. It was one-time per cashier, per day. Making it one-time means one shared list, kept overnight, where crossing out a voucher and checking it are the same act.

Consider a hypothetical customer-support application. A support agent can apply a goodwill credit to a customer's account, but only with a supervisor's approval. When a supervisor approves, the application issues a one-time grant: a signed token that names its issuer, a grant ID, the agent, the account, the amount and currency, and an expiry 15 minutes after issuance. The credit itself is created through a billing provider's API.

The grant code was written carefully. It validates the signature, checks expiry, checks that the agent, account, and amount match the request, and then consumes the grant before calling billing:

```csharp
// Illustrative: one-time within this process only. It also never evicts,
// so even in one process it grows until restart.
public sealed class InMemoryGrantConsumptionStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> consumed = new();

    public bool TryConsume(string grantId, DateTimeOffset now) =>
        consumed.TryAdd(grantId, now);
}
```

`TryAdd` is atomic. If two threads in the same process present the same grant at the same moment, exactly one of them gets `true`. The unit tests demonstrate it, including a test that races a hundred tasks. The store is registered as a singleton, and in development the application runs as one process.

In production, it runs as two instances behind a round-robin load balancer, and deployments are rolling. In this hypothetical incident, grant `g-41c9` allowed one credit of $250 to account `A-1187`:

```text
10:02:11.204  instance-a  POST /accounts/A-1187/credits  grant=g-41c9
10:02:11.209  instance-a  Grant g-41c9 valid, consumed
10:02:11.215  instance-a  Calling billing: create credit 250.00 USD
10:02:21.215  gateway     Upstream timeout after 10 s, returned 504 to client
10:02:23.840  instance-a  Billing returned 201 Created  credit=cr_8812
10:02:29.102  instance-b  POST /accounts/A-1187/credits  grant=g-41c9
10:02:29.106  instance-b  Grant g-41c9 valid, consumed
10:02:29.111  instance-b  Calling billing: create credit 250.00 USD
10:02:29.497  instance-b  Billing returned 201 Created  credit=cr_8817
```

The gateway gave up on instance A, but instance A did not give up on billing, and billing returned a successful creation response about 2.6 seconds after the gateway timeout. The agent clicked **Apply credit** again, and round-robin routing sent the retry to instance B. Another routing policy might have sent it back to A; correctness should not depend on where a retry lands. Instance B's dictionary had never seen `g-41c9`, so `TryAdd` returned `true`, and the customer received $500. Each instance's log accurately reflects its local decision. Each instance enforced one use. The application did not.

The same week, the team found a second path. Grant `g-52d0` was issued at 10:30 and expired at 10:45. Instance A consumed it at 10:38. A rolling deployment restarted instance A at 10:40. At 10:44, a resubmission from a stale browser tab presented the same grant, and instance A accepted it. Its dictionary was new.

> **A grant is single-use only within the scope of the state that records its use. In-process memory gives you one process, until it restarts. Replay protection across instances and restarts needs an authoritative store that every executing instance shares, that keeps its records across restarts, and that admits a grant with one atomic conditional write before the protected operation runs. That limits who may run the operation. It does not establish whether the operation's effect happened, or make it exactly-once.**

None of this means the in-memory store was a mistake. It was a correct implementation of a narrower claim. The defect was deploying it where the wider claim was assumed.

---

## Correct in One Process, Wrong Across Two

`ConcurrentDictionary` solves one problem: threads sharing one object. It makes the check and the claim a single step for every caller that holds a reference to that object. Nothing in it can coordinate with an object in another process, and nothing in it survives the process.

The replay guarantee is only as wide as the state behind it:

| Where consumption is recorded | Who agrees a grant is spent | What erases the record |
| --- | --- | --- |
| A `ConcurrentDictionary` in a singleton | Threads in that one process | Restart, crash, deployment, scale-in, app pool recycle |
| A `ConcurrentDictionary` in a scoped service | Code within one dependency injection scope, which in ASP.NET Core is normally one request | The end of the scope |
| A `ConcurrentDictionary` in a transient service | Only the code holding that one resolved instance | The next resolution creates a new, empty store |
| A distributed cache, read and then written | Nobody reliably; two instances can both read "absent" | Depends on the engine: eviction, expiry, failover, flush |
| An authoritative shared store with an atomic conditional write | Every instance that consults it | Within the stated failure model, only controlled cleanup after no admission can still succeed |

The first row is what the support application had. The tests passed because they exercised the first row's guarantee, and the incident occurred because production needed the last row's.

There are two ways to lose the record, and they need slightly different things:

- **Another instance** needs *shared* state: the consumption must be visible to whichever instance handles the next presentation, before it decides.
- **A restart** needs *durable* state: the consumption must be recorded somewhere the next process will read, and it must be recorded before the protected operation runs, so a crash between the two cannot lose it.

A store can be shared and not durable enough, such as a cache that can lose keys on failover, or durable and not shared, such as a local file on each instance. Neither closes both paths.

---

## Three Fixes That Narrow the Gap Without Closing It

Teams that find this bug usually try one of these first. Each reduces how often it happens. None of them establishes the guarantee by itself.

**Sticky sessions.** How affinity behaves depends on the load balancer: it may key on a cookie, a source address, or a connection, and it may or may not survive a new connection. Even when it holds, it breaks when an instance is removed during deployment or scale-in, or fails over. It does not preserve consumption history across a restart, and it does not guarantee that every possible presenter of the grant reaches the instance that recorded its use.

**A distributed cache.** Microsoft's documentation describes a distributed cache as coherent across servers and able to survive server restarts and deployments, and for caching it is. The `IDistributedCache` interface still exposes only get, set, refresh, and remove, with their async forms. Reading a key and then setting it is the same check-then-act race as before, only across the network: two instances can both read "absent" and both proceed. Some cache engines offer an atomic set-if-absent operation, which fixes the race, but `IDistributedCache` does not expose it, so using it means calling the engine's own client. Even then, it is enough only if the engine's durability and retention also meet the contract. Can the engine evict the key under memory pressure? Can a failover lose a write that was acknowledged? Can the key expire or be removed while the grant can still be admitted? If any answer is yes, a consumed record can disappear while the grant is still acceptable. The store's behavior settles the question, not the product name.

**Shorter expiry.** A five-minute grant has a smaller replay window than a fifteen-minute grant. It still has one. In the incident, the retry came 18 seconds after the first request, well inside any expiry that leaves room for a human to act. Expiry limits *when* a grant may be admitted. Consumption limits *how many times*. They are separate checks, and a short expiry is a reason to keep consumption records for less time, not a reason to skip them.

The replay problem itself is simple: every instance must agree whether a grant has already been used. The rest of the article covers the harder production questions. How do you make that decision durable and atomic? How do you recover when an outcome is uncertain? How do you keep the guarantee through revocation, failures, and cleanup? Readers who only need the first answer can stop after the minimum safety contract.

---

## The Store Contract

Two lists belong here, and it helps to keep them apart. The first is what any store needs to keep a grant from being admitted twice. The second is what this application wants on top, so that retries, recovery, and diagnostics work. They describe behavior, not a product. A relational database, a strongly consistent key-value store, or a single service that owns grant state can each meet them.

### The minimum safety contract

1. **Authoritative and shared at the scope of the claim.** Every instance that can execute the protected operation consults the same authoritative state. If grants are deliberately bound to one region or partition, the claim is scoped to that region or partition and says so.
2. **Atomic admission.** Checking that a use remains and recording the use are one atomic operation. At most one competing attempt is committed as admitted. Its caller may still receive an unknown result if the acknowledgment is lost.
3. **Durable before execution, for the failures you claim to survive.** The admission is committed, and the commit is acknowledged, before the protected operation starts. If the claim covers a database failover, that means a commit the promoted replica is guaranteed to have, not one an asynchronous replica can lose. A crash after a durable commit leaves the grant spent. A crash before it leaves the grant unspent and the operation not started.
4. **The deadline is enforced where the decision is made, on one clock.** Expiry, plus a named leeway for clock differences, is checked in the same statement that claims the grant, against the store's clock. Any earlier check on an application instance is only a filter: it uses the same leeway and must not be stricter than admission. In a multi-region store, also state which node's clock and which replication guarantee the decision relies on. In this article, expiry is an *admission* deadline: it limits when an admission statement may begin. It does not require the admission to commit, or billing to run, before expiry, and an admitted operation may complete or be recovered afterwards. If the business rule is that no credit may be applied after a certain time, that needs its own operation deadline and recovery policy; grant expiry does not implement it.
5. **Retained until no admission can still succeed.** A consumption record lives at least until the grant's deadline has passed and no admission attempt that began before the deadline can still commit.
6. **No execution without a confirmed admission.** A timeout, a cancellation, an error, or an unreachable store means the operation does not run on this attempt. None of those is ever read as "unused".

The identity being claimed must be unambiguous. The examples use the issuer plus the grant ID, both taken from the validated token rather than from request fields, and assume each issuer never reuses a grant ID. RFC 7519 requires that, when an application uses several issuers, their `jti` values must not collide; keying on the issuer as well makes that explicit. If grant IDs are scoped more narrowly by design, for example per tenant, the uniqueness key must match that scope exactly. Adding a tenant column to the key merely because the system is multi-tenant would let a grant valid in two tenants be admitted once in each.

The types and schema below assume that grant IDs are unique per issuer, that caller and account identifiers are globally unambiguous, and that tenant ownership can be derived from them authoritatively. Where that is not true, the namespace becomes part of the data. If grant IDs are per tenant, every grant key, here `(issuer, grant_id)`, becomes `(issuer, tenant, grant_id)` in the tables, the `ON CONFLICT` target, and the operations unique key. If caller or account identifiers are local to a tenant or an authentication issuer, add that namespace to `ValidatedGrant`, `OperationIntent`, and the operations table, and compare it in every status check.

### The application contract

These make the system usable. They are not needed to prevent double admission, and a store that simply refuses every losing attempt is still safe.

- **Operation linkage.** The admission records which operation it admitted, with that operation's immutable details, in the same transaction.
- **Retry status.** While the grant still validates, a retry of the same operation, by the same caller, with the same details, gets that operation's status instead of a refusal. After the grant expires, the operation is still reachable through a status endpoint authorized on the caller and tenant, never through the expired grant.
- **Revocation.** A withdrawn grant cannot be newly admitted, and revocation and admission are ordered so that whichever commits first wins. Revocation does not erase an operation admitted before it: a matching retry may still get that operation's status, and the status endpoint still serves it.
- **Diagnostics.** Each refusal reason comes from the component that actually knows it: token validation knows about signatures, expiry, and mismatched details; the revocation record knows about withdrawals; the consumption record knows about prior use; and the operation record knows about status. Callers get a small set of stable reasons. Logs get the detail.

In C#, the boundary the handler sees can be small:

```csharp
// Produced by token validation from the signed grant, never from request fields.
public sealed record ValidatedGrant(
    string Issuer,
    string GrantId,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    string AgentId,      // the validator checked it equals the caller's authenticated subject
    string AccountId,
    decimal Amount,
    string Currency);

// One immutable intended operation. The admission binds it to the grant.
public sealed record OperationIntent(
    Guid OperationId,
    string CallerId,
    string AccountId,
    decimal Amount,
    string Currency);

public enum AdmissionOutcome
{
    Admitted,
    AlreadyAdmittedForThisOperation,
    Refused,
}

public sealed record AdmissionResult(
    AdmissionOutcome Outcome,
    OperationRecord? ExistingOperation);

public interface IGrantAdmissionStore
{
    // In one statement: claim the grant's single use and record the operation
    // intent. Maps a conflict on the operation ID, or on the operation's grant
    // key, to Refused. Throws
    // GrantStoreUnavailableException when the store cannot be reached or does
    // not confirm the commit, including a timeout after which the commit may
    // or may not have happened.
    Task<AdmissionResult> TryAdmitAsync(
        ValidatedGrant grant,
        OperationIntent intent,
        CancellationToken cancellationToken);
}
```

The name matters. `TryAdmit` claims the use. A pair of methods named `IsConsumed` and `MarkConsumed` invites the race back in, however well each method is written.

---

## One Write Admits the Grant and Records the Operation

The examples in this section assume three things. Each admission runs as its own short statement, in an implicit transaction under PostgreSQL's default Read Committed isolation, not inside a longer caller transaction. Timestamp parameters use `timestamptz`, so comparisons do not depend on the session's time zone, and duration parameters such as `@admission_leeway`, `@execution_lease`, `@recovery_lease`, `@submission_window`, and the fallback's `@maximum_grant_lifetime` and `@issue_time_skew` use `interval`. The handler's `InitialOwnerVersion` constant supplies `@initial_owner_version`, so the two cannot drift apart. The leeway is fixed while any grant issued under it can still be admitted, as discussed below; a versioned leeway would add a policy version to the signed token and to `ValidatedGrant`, and admission and retention would look up the leeway for that version rather than read current configuration. And the uniqueness the examples rely on exists in the schema:

```sql
-- Row form: grants are stored when issued.
CREATE TABLE grants (
    issuer                 text        NOT NULL,
    grant_id               text        NOT NULL,
    expires_at             timestamptz NOT NULL,
    revoked_at             timestamptz,
    consumed_at            timestamptz,
    consumed_by_operation  uuid,
    PRIMARY KEY (issuer, grant_id)
);

-- Token form: one row per claimed key, whether by an admission or a revocation.
CREATE TABLE grant_consumptions (
    issuer        text        NOT NULL,
    grant_id      text        NOT NULL,
    operation_id  uuid,                     -- null only for a revocation recorded under this key
    consumed_at   timestamptz NOT NULL,     -- when the key was claimed, by an admission or a revocation
    retain_until  timestamptz NOT NULL,
    PRIMARY KEY (issuer, grant_id)          -- the ON CONFLICT target
);

CREATE TABLE operations (
    operation_id  uuid        PRIMARY KEY,  -- globally unique, server-issued
    issuer        text        NOT NULL,
    grant_id      text        NOT NULL,
    caller_id     text        NOT NULL,
    account_id    text        NOT NULL,
    amount        numeric     NOT NULL,
    currency      text        NOT NULL,
    status        text        NOT NULL,     -- Admitted, Succeeded, Failed, Unknown, NeedsReconciliation
    owner_version integer     NOT NULL,
    lease_until   timestamptz NOT NULL,
    submit_by     timestamptz NOT NULL,     -- latest time a create request may be sent
    created_at    timestamptz NOT NULL,
    UNIQUE (issuer, grant_id)               -- defense in depth: one operation per grant
);
```

Other isolation levels change which rows a statement sees and add serialization failures that callers must retry, so they need their own reasoning.

When the application stores grants as rows, the claim is a conditional update. Under Read Committed, a second updater of the same row waits for the first to commit and then re-evaluates its `WHERE` clause against the updated row, so only one statement can move the row from unconsumed to consumed. The operation insert runs in the same statement and depends on the update's `RETURNING` rows:

```sql
WITH claim AS (
    UPDATE grants
    SET consumed_at = statement_timestamp(),
        consumed_by_operation = @operation_id
    WHERE issuer = @issuer
      AND grant_id = @grant_id
      AND consumed_at IS NULL
      AND revoked_at IS NULL
      AND expires_at + @admission_leeway > statement_timestamp()
    RETURNING issuer, grant_id
)
INSERT INTO operations (
    operation_id, issuer, grant_id, caller_id, account_id, amount, currency,
    status, owner_version, lease_until, submit_by, created_at)
SELECT @operation_id, issuer, grant_id, @caller_id, @account_id, @amount, @currency,
       'Admitted', @initial_owner_version, clock_timestamp() + @execution_lease,
       clock_timestamp() + @submission_window, clock_timestamp()
FROM claim
RETURNING operation_id;
```

When the grant is a self-contained signed token with no server-side row, as in the support application, the claim is an insert against the unique key. The token's expiry, taken from the validated token, is checked in the same statement against the database clock:

```sql
WITH claim AS (
    INSERT INTO grant_consumptions (issuer, grant_id, operation_id, consumed_at, retain_until)
    SELECT @issuer, @grant_id, @operation_id, statement_timestamp(), @expires_at + @admission_leeway
    WHERE @expires_at + @admission_leeway > statement_timestamp()
    ON CONFLICT (issuer, grant_id) DO NOTHING
    RETURNING issuer, grant_id
)
INSERT INTO operations (
    operation_id, issuer, grant_id, caller_id, account_id, amount, currency,
    status, owner_version, lease_until, submit_by, created_at)
SELECT @operation_id, issuer, grant_id, @caller_id, @account_id, @amount, @currency,
       'Admitted', @initial_owner_version, clock_timestamp() + @execution_lease,
       clock_timestamp() + @submission_window, clock_timestamp()
FROM claim
RETURNING operation_id;
```

A few details carry the guarantee:

- **The unique key, or the row lock, is the atomic check.** One statement claims the grant. Every competing statement finds the conflict, or the already-consumed row, and claims nothing.
- **The operation insert sees only what the claim returned.** All parts of a statement with data-modifying `WITH` clauses share one snapshot, and `RETURNING` is how the claim makes its row available to the operation insert. If no grant was claimed, no operation is recorded.
- **A conflicting operation refuses the whole admission.** If `operation_id` already exists, or an operation already holds this grant's key, the unique violation aborts the statement, including the claim. The store catches those two specific violations, maps them to `Refused`, and reveals nothing about the existing operation. Any other constraint failure is a defect: it surfaces as a server error, nothing executes, and it is not reported as the store being unavailable or retried automatically.
- **`statement_timestamp()` for the deadline, `clock_timestamp()` for the lease.** In PostgreSQL, `now()` is the start time of the current *transaction*, which can be well in the past if admission is wrapped in a longer transaction. `statement_timestamp()` is the start of the current statement, which keeps the deadline check stable however long the statement waits. A lease measured from statement start would be eaten by that wait, so the operation's `lease_until` and `created_at` use `clock_timestamp()`, the actual time when the operation row is written, after the claim.
- **`@admission_leeway` is one named value, fixed while any grant issued under it can still be admitted.** Token validation, admission, and retention all use it. Changing it is not a configuration tweak: a grant admitted under the old value keeps a `retain_until` computed from it, so raising the leeway can move that grant's deadline past its stored `retain_until`, and cleanup could then delete the record while the grant passes the check again. Either bind each grant to a versioned leeway policy at issuance, carried in the signed token and looked up server-side at admission and retention, or wait until every grant issued under the old value, and every admission attempt for it, has aged out.

Zero rows returned means this caller did not admit the grant. The store then reads the consumption and operation records to decide what to report. If the grant was consumed by this same operation ID, and the stored caller, account, amount, and currency match this request, the result is `AlreadyAdmittedForThisOperation` with that operation's record. Anything else is `Refused`, including a key held by a revocation, whose `operation_id` is null. The follow-up read is a new statement with a fresh snapshot, so any reason it logs describes the state after the attempt, not necessarily the state at the instant of the claim. That read is only for reporting. The claim already made the decision, so a change between the claim and the read cannot turn a refusal into an admission.

Either form meets the minimum contract only if the commit is durable in the sense of the failures the claim covers, on a primary that every instance writes to, before the call returns. A replica that can be promoted without the latest acknowledged writes reopens the restart path in a different shape.

### Revocation contends for the same key

Revocation and admission should contend for the same row or key, so the database orders them and whichever commits first wins. A separate check, such as `NOT EXISTS` against a revocation table inside the admission statement, does not give that ordering: it reads the statement's snapshot, so a revocation that commits while an admission is already running can be missed.

In the row form, revocation updates the same row, and only an unconsumed one:

```sql
UPDATE grants
SET revoked_at = statement_timestamp()
WHERE issuer = @issuer
  AND grant_id = @grant_id
  AND consumed_at IS NULL
  AND revoked_at IS NULL
RETURNING grant_id;
```

In the token form, revocation claims the grant's key in `grant_consumptions` with no operation. Unlike the row form, an insert succeeds for any key it is given, so the revoker must first prove that the grant was issued: by presenting the signed grant, which the revocation service verifies, or by finding it in the issuer's issuance log. Either source also supplies the grant's trusted expiry, which sets how long the revocation row is kept:

```sql
-- @expires_at comes from the verified grant or the issuance log, never from the request.
INSERT INTO grant_consumptions (issuer, grant_id, operation_id, consumed_at, retain_until)
VALUES (@issuer, @grant_id, NULL, statement_timestamp(), @expires_at + @admission_leeway)
ON CONFLICT (issuer, grant_id) DO NOTHING
RETURNING grant_id;
```

In both forms, no row returned means the grant was already used, or already revoked, and the revocation changes nothing. Revocation cannot recall an operation admitted before it, and it does not erase that operation's record. In the token form, `consumed_at` on a revocation row is the time the key was claimed, and the null `operation_id` is what marks the row as a revocation rather than a use.

Without proof of issuance, a revoker could claim the key of a grant that was never issued and block a future grant that happens to receive that ID. Rate limits and privilege narrow a flood. They do not stop a targeted claim. Proof of issuance and knowledge of the trusted expiry are separate requirements. An issuer may keep a trustworthy record of which grant IDs it issued without keeping their expiry. Revocation then still requires that proof, and only the retention bound changes. If no trusted expiry is available, a fallback is to retain the row from the time it is written for the longest lifetime any grant may have, plus the allowed issue-time skew, plus the leeway. Use `clock_timestamp()` as the base, so a lock wait does not shorten it:

```sql
-- Fallback only, when the grant's trusted expiry is not available.
INSERT INTO grant_consumptions (issuer, grant_id, operation_id, consumed_at, retain_until)
VALUES (@issuer, @grant_id, NULL, statement_timestamp(),
        clock_timestamp() + @maximum_grant_lifetime + @issue_time_skew + @admission_leeway)
ON CONFLICT (issuer, grant_id) DO NOTHING
RETURNING grant_id;
```

That fallback is safe only if validators enforce both limits: they reject a grant whose `ExpiresAt` is more than the maximum lifetime after its `IssuedAt`, or whose `IssuedAt` is ahead of the validator's clock by more than the skew. Like the leeway, neither limit may be shortened while grants issued under it are outstanding.

Revocation is itself a privileged write. Restrict it to the issuer or authorized supervisors, validate the shape and length of the grant ID, and rate-limit it.

---

## Where Admission Belongs

Admission belongs at the execution boundary: in the component that performs the protected operation, after every other check has passed, immediately before the operation runs. Admitting earlier, such as in an API gateway filter or when a request is queued, spends the grant on requests that are later rejected for other reasons. Admitting after the effect reopens the race.

The sketch below relies on types that are described but not shown: `ApplyCreditCommand`, `CreditResult`, `CreditRejection`, `OperationRecord`, `OperationStatus`, `BillingOutcome`, `BillingCredit`, `BillingRejectedException`, and `ExecutionTimings`. The validator's contract is:

```csharp
public interface IGrantValidator
{
    // Verifies signature, issuer, and expiry with the same leeway as admission,
    // and that the grant names the caller's authenticated subject and matches
    // the requested account, amount, and currency. Where the fallback
    // revocation retention is used, it also rejects a grant whose lifetime
    // (ExpiresAt - IssuedAt) exceeds the maximum, or whose IssuedAt is ahead
    // of this clock by more than the allowed skew. This is an early filter;
    // the admission statement makes the decision.
    bool TryValidate(
        ApplyCreditCommand command,
        ClaimsPrincipal caller,
        [NotNullWhen(true)] out ValidatedGrant? grant);
}
```

```csharp
public sealed class ApplyCreditHandler(
    IGrantValidator grantValidator,
    IGrantAdmissionStore admissions,
    IOperationRecords operations,
    IBillingClient billing,
    ExecutionTimings timings,   // one configuration source; see the recovery section
    ILogger<ApplyCreditHandler> logger)
{
    // Admission writes the operation at this owner version.
    private const int InitialOwnerVersion = 1;

    public async Task<CreditResult> HandleAsync(
        ApplyCreditCommand command,
        ClaimsPrincipal caller,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return CreditResult.Rejected(CreditRejection.MalformedRequest);
        }

        if (!grantValidator.TryValidate(command, caller, out ValidatedGrant? grant))
        {
            // Includes a grant past its admission deadline. An operation admitted
            // before then stays reachable through the separately authorized
            // status endpoint.
            return CreditResult.Rejected(CreditRejection.GrantNotValid);
        }

        // Everything below uses the validated grant, not the request's fields.
        var intent = new OperationIntent(
            command.OperationId,
            grant.AgentId,
            grant.AccountId,
            grant.Amount,
            grant.Currency);

        AdmissionResult admission;
        try
        {
            admission = await admissions.TryAdmitAsync(grant, intent, cancellationToken);
        }
        catch (Exception ex) when (ex is GrantStoreUnavailableException or OperationCanceledException)
        {
            // Nothing has executed. The admission may or may not have
            // committed; a retry with the same operation ID will find out.
            // If the client itself cancelled, nobody may read this response,
            // but the store's state is the same either way.
            return CreditResult.RetryLater(intent.OperationId);
        }

        if (admission.Outcome == AdmissionOutcome.Refused)
        {
            return CreditResult.Rejected(CreditRejection.GrantNotAvailable);
        }

        if (admission.Outcome == AdmissionOutcome.AlreadyAdmittedForThisOperation)
        {
            // Report, never execute. Succeeded maps to Applied, Failed to
            // Rejected, and Admitted, Unknown, or NeedsReconciliation to
            // Pending, which means "check again", not "done".
            return admission.ExistingOperation is { } existing
                ? CreditResult.FromOperation(existing)
                : CreditResult.Pending(intent.OperationId);
        }

        // Admitted: the grant is spent, the operation intent is durable, and
        // this attempt owns the operation at the initial owner version until
        // its lease ends.
        //
        // Re-check just before submitting: still Admitted, still this owner
        // version, and before both lease_until and submit_by. This narrows the
        // delayed-submission window; it cannot fence a pause after the check.
        bool maySubmit;
        try
        {
            using var checkTimeout = new CancellationTokenSource(timings.StoreCallTimeout);
            maySubmit = await operations.MaySubmitAsync(
                intent.OperationId, InitialOwnerVersion, checkTimeout.Token);
        }
        catch (Exception ex) when (ex is GrantStoreUnavailableException or OperationCanceledException)
        {
            // The check could not confirm ownership: send nothing to billing.
            // The operation stays Admitted for recovery.
            maySubmit = false;
        }

        if (!maySubmit)
        {
            return CreditResult.Pending(intent.OperationId);
        }

        // The billing call gets its own timeout rather than the request's token,
        // so a client disconnect does not abandon it halfway.
        BillingOutcome outcome;
        using (var billingTimeout = new CancellationTokenSource(timings.BillingCallTimeout))
        {
            try
            {
                BillingCredit credit = await billing.CreateCreditAsync(
                    grant.AccountId,
                    grant.Amount,
                    grant.Currency,
                    idempotencyKey: intent.OperationId,
                    billingTimeout.Token);

                outcome = BillingOutcome.Succeeded(credit.Id);
            }
            catch (BillingRejectedException ex)
            {
                // A definite refusal: billing created nothing.
                outcome = BillingOutcome.Refused(ex.ReasonCode);
            }
            catch (Exception ex)
            {
                // Deliberately conservative: every other failure is an unknown
                // outcome, including our own timeout and errors after billing
                // may have acted, such as a response that fails to deserialize.
                // Cancelling the client call does not cancel billing's work, so
                // the credit may exist. Log it, so programming errors and
                // provider-contract changes do not vanish into pending work.
                // Provider exceptions can carry responses, account data, or
                // credentials, so this logs the exception type, not the
                // exception. Pass the exception only to a logging pipeline
                // known to sanitize it.
                logger.LogWarning(
                    "Billing outcome unknown for operation {OperationId}: {ExceptionType}",
                    intent.OperationId, ex.GetType().FullName);
                outcome = BillingOutcome.Unknown;
            }
        }

        try
        {
            // Conditional: applies only while the operation is still Admitted
            // at the initial owner version. If recovery has taken over,
            // nothing is written.
            bool recorded = await operations.TryRecordOutcomeAsync(
                intent.OperationId,
                expectedStatus: OperationStatus.Admitted,
                expectedOwnerVersion: InitialOwnerVersion,
                outcome,
                CancellationToken.None);

            // Succeeded maps to Applied, Refused to Rejected, Unknown to Pending.
            return recorded
                ? CreditResult.From(outcome, intent.OperationId)
                : CreditResult.Pending(intent.OperationId);
        }
        catch (Exception ex)
        {
            // The outcome is known here but not recorded. Surface it: operators
            // need evidence that recovery has work. Log the operation ID, owner
            // version, and exception type, never the grant token or raw
            // exception details. Recovery reconciles it; this handler never
            // calls billing again.
            logger.LogError(
                "Recording outcome {Outcome} failed for operation {OperationId} at owner version {OwnerVersion}: {ExceptionType}",
                outcome, intent.OperationId, InitialOwnerVersion, ex.GetType().FullName);
            return CreditResult.Pending(intent.OperationId);
        }
    }
}
```

The choices in this handler carry the rest of the argument:

- **Admission and the operation record are one commit.** A crash after admission leaves an `Admitted` operation, never a spent grant with no record. A retry that arrives while the first attempt is still running finds that record and reports it as pending.
- **The operation ID identifies one immutable intended operation.** The server issues a new random GUID when it renders the approval form, so a second click resends the same value, and IDs are unique across all grants and tenants, because the same value becomes billing's idempotency key. The value the client sends back is not authority. Admission binds it to the grant, the caller, and the request details, keeps that binding for every later retry, and refuses a request that reuses it with anything else. A client could still send a value of its own choosing, which billing would then see as an idempotency key. The unique key stops that value from touching another operation of yours. If billing keys are shared with other systems, or you want to reject made-up values outright, have the server send a MAC over the operation ID and grant ID with the form. `ApplyCreditCommand` then carries it, and `TryValidate` verifies it before admission.
- **An operation ID is not a credential.** The store returns an existing operation only when the grant, the caller, and the details all match. Knowing or guessing another operation's ID gets a refusal, not its status.
- **A grant past its admission deadline cannot authorize a new admission, but it does not hide the operation.** The handler rejects such a grant before admission, including on a retry of an operation admitted just before the deadline. That retry's caller finds the operation through a status endpoint that authorizes on the stored caller and tenant, and the operation's recovery obligation does not depend on the grant.
- **A store failure or cancellation means no execution and a retryable response.** It includes an ambiguous timeout after which the admission may have committed.
- **Spent authority is not released when the outcome is unknown.** If billing times out, the credit may exist. Releasing the grant so the agent can try again is how a double credit gets reintroduced through a different door. After a definite refusal, a team may decide to let a supervisor approve again, but that is a new grant, never an un-consumed old one. Predictable refusals, such as a closed account, are better caught by cheap checks before admission, so they do not spend grants at all.
- **Retries never execute; recovery does.** Every status change is conditional on the expected status and owner version, so a stale writer cannot overwrite a newer result, and nothing overwrites a terminal one.

### Recovery and the original executor share one ownership protocol

An operation can stall: the process crashes after admission, the billing outcome is unknown, or recording the outcome fails. A recovery job finds such operations once their lease has ended, and takes ownership with a conditional update:

```sql
UPDATE operations
SET owner_version = owner_version + 1,
    lease_until   = clock_timestamp() + @recovery_lease
WHERE operation_id = @operation_id
  AND status IN ('Admitted', 'Unknown')
  AND lease_until < clock_timestamp()
  AND owner_version = @expected_owner_version
RETURNING owner_version;
```

`Succeeded` and `Failed` are final, and `NeedsReconciliation` waits for manual or provider-assisted resolution, so none of them is taken over. Recovery still takes over an operation whose `submit_by` has passed, because it may still look the credit up and record what it finds; only create calls are gated by `submit_by`. The recovery job keeps the `owner_version` the takeover returns and uses it as the expected version for every write it makes, and for its own submission check, which then tests the recovery lease written at takeover. From then on, a late write from the original handler, still expecting the initial version, changes nothing.

Each lease has to cover everything its owner does while it owns the operation. The admission lease starts when the operation row is written, after any lock wait, and must cover the commit, the gap before the billing call, the billing timeout, recording the outcome, and a margin. The recovery lease must cover recovery's own lookup, any create call, and recording, or recovery must renew it, conditionally on its version, before each step. Keep the leases and the billing timeout in one configuration object, here `ExecutionTimings`, and check at startup that the billing timeout is shorter than each lease minus its margin, so raising one value without the other fails fast.

The owner version fences writes to your own records. It does not fence requests to billing. A billing timeout shorter than the lease reduces ordinary overlap, but a handler paused past its lease, for example by a long garbage collection or a suspended VM, can still send or complete its call while recovery acts, and billing may keep working after the client has given up. Any overlap is safe only if the provider documents that concurrent requests with the same idempotency key are handled atomically, not just that the key is retained. A pause has no upper bound, so waiting cannot prove the original call is finished. If the provider does not document atomic concurrent handling, recovery never sends a create request at all. It only looks up.

That limit also applies to the original handler. If it pauses after admission and before its billing call, recovery may take over and finish, and the provider's retention window may pass before the handler resumes and sends its first request with the old key, which the provider may then treat as new. The operation's `submit_by` is the latest time any create request for it may be sent: admission time plus the provider's documented key retention, minus a safety margin, here `@submission_window`. The handler's `MaySubmitAsync` and recovery both check it, together with ownership, just before any create call:

```sql
SELECT 1
FROM operations
WHERE operation_id = @operation_id
  AND status IN ('Admitted', 'Unknown')    -- the handler expects Admitted
  AND owner_version = @expected_owner_version
  AND clock_timestamp() < least(lease_until, submit_by);
```

`submit_by` is deliberately conservative. The store cannot tell an operation that never reached billing from one whose request was sent and whose outcome is unknown, so after `submit_by` both go to `NeedsReconciliation`, including some where a fresh create would have been safe. A post-`submit_by` reconciliation is therefore not evidence that billing ever saw the key.

That check narrows the window, but nothing local can fence a process paused between the check and the call. So the provider's idempotency guarantee must cover every possible submission, including a delayed original one. Where pauses cannot be bounded, closing the gap needs an external deadline or fencing mechanism that billing enforces. Otherwise, duplicate-effect prevention remains conditional on the provider's retention window.

The recovery job asks billing whether a credit with that reference exists, which requires the provider to document a way to look it up by the original reference or idempotency key. A credit that is found is recorded as `Succeeded`. A lookup that finds nothing is not proof that nothing exists, so recovery never records `Failed` on that basis. Recovery resumes the create call with the same idempotency key only if the provider documents atomic handling of concurrent requests with that key, honors it with the same parameters, and keeps it longer than the operation has been open. Otherwise, or if the provider offers no reliable lookup, or once that window has passed and billing cannot say whether the credit exists, recovery sends no create request. It marks the operation `NeedsReconciliation` for manual or provider-assisted resolution. Some providers prune idempotency keys after a documented period, and a key reused after pruning is treated as a new request.

### The timing values in one place

The design now depends on several durations. They only work together if their relationships hold, so keep the application-side values in one configuration object, here `ExecutionTimings`, check the relationships at startup, and keep the database-side bounds in the admission role's settings:

| Value | What it bounds | Must satisfy |
| --- | --- | --- |
| `@admission_leeway` | Clock tolerance on the admission deadline | One value in validation, admission, and retention; fixed or versioned while grants issued under it can still be admitted |
| Billing call timeout | How long the handler waits for billing | Shorter than the execution lease minus its margin |
| `@execution_lease` | The handler's ownership after admission | Covers the commit, the gap before billing, the billing timeout, recording, and a margin |
| `@recovery_lease` | Recovery's ownership after a takeover | Covers the lookup, any create call, and recording, or is renewed before each step |
| `@submission_window` | How long after admission a create may be sent (`submit_by`) | No longer than the provider's documented key retention, minus a safety margin that also covers clock differences with the provider; longer than the execution lease plus the delay before recovery picks up a stalled operation and one recovery step, or recovery can never resubmit |
| Store call timeout | How long the handler waits for `MaySubmitAsync` and similar reads | Shorter than the execution lease minus its margin |
| Admission transaction bound | How long an admission can live, from statement start to commit | Finite: `transaction_timeout` or an equivalent server-enforced bound, plus a separately bounded synchronous-replication wait |
| `@cleanup_margin` | Extra time before cleanup deletes refusal records | Longer than the admission transaction bound, plus the largest clock step allowed |
| `@maximum_grant_lifetime` and `@issue_time_skew` | Fallback revocation retention only | Enforced by validators; never shortened while grants issued under them are outstanding |

When the protected effect is a write to the same database, it can go in the admission transaction itself, and consumption and effect commit or roll back together. That is an alternative to "durable before execution", not a case of it. An ordinary HTTP API call is not enlisted in that local transaction. If the billing call ran inside the transaction and the transaction then rolled back, the grant would be un-consumed after its effect had already happened.

---

## What This Guarantee Is, and What It Is Not

Three properties are easy to merge, and the support application needed all three:

| Property | Question it answers | Keyed on | What provides it |
| --- | --- | --- | --- |
| Replay protection | May this authority be admitted again? | The grant | Shared, durable, atomic admission at the execution boundary |
| Idempotency | Should this operation's effect be produced again? | The operation | An operation ID reused across retries, recorded results, and a counterpart that honors the key within its contract |
| Exactly-once external effect | Did the effect happen exactly once in the other system? | The effect | No single mechanism. Provider-enforced idempotency suppresses duplicates within its contract; operation records and reconciliation establish what happened when an outcome is uncertain |

Replay protection says that at most one claimant reaches the executor with a given grant. It says nothing about what happens next. The billing call can succeed, fail, or time out with an unknown result, and the consumption record is the same in all three cases.

Idempotency handles part of what follows. If billing honors `intent.OperationId` as an idempotency key, a repeated call with the same key and the same parameters does not create a second credit, but only for as long as billing retains the key. That needs to be longer than your retry and recovery window. That protection belongs to the operation, not to the grant: it would not stop a spent grant from being presented with a fresh operation ID. Only the consumption record stops that.

Reconciliation is the rest. It establishes or repairs what you know about an outcome. It does not stop a duplicate request from producing a second effect; that is the idempotency key's job. A process can crash after billing creates the credit and before the operation record says so. Durable admission ensures the grant cannot be used to try again. The operation record and reconciliation determine whether the credit exists. None of these alone guarantees that every intended operation eventually completes exactly once. [Idempotency Does Not Prevent Authority Replay](idempotency-does-not-prevent-authority-replay.md) works through that split in detail, including what makes a later attempt materially new.

---

## When the Store Is Slow or Unavailable

Moving admission out of process makes it a dependency. When it is slow or down, the protected operation has to wait, fail, or proceed without the check. For a one-time grant, proceeding without the check is the same as having no check, so the default is to not execute: return a retryable response and let the caller try again with the same operation ID.

Two traps are worth naming:

- **Falling back to local memory during an outage.** It feels like graceful degradation. It recreates the two-instance bug at exactly the moment the shared record cannot help.
- **Treating a timeout on the admission call as a refusal.** The write may have committed. The result is unknown, so nothing executes on that attempt. A retry with the same operation ID then meets one of several defined states. If the grant is unclaimed and still valid, it is admitted now. If it was admitted for this operation, the retry reports that operation's status. If the grant has passed its admission deadline, the validator refuses the retry before admission, and an operation this caller did admit is still reachable through the status endpoint. If the grant was revoked before this operation was admitted, or was admitted for another operation, the retry is refused. A revocation that arrives after this operation was admitted changes nothing, so the retry still gets its status. If the store is still unavailable, the retry gets another retryable response. No retry executes an operation that was already admitted; one that was admitted but never sent stays `Admitted` until recovery takes it.

If the business would rather risk a duplicate than block during an outage, that is a legitimate decision, but it deliberately gives up the single-use guarantee for the affected operations. It is not another way to implement that guarantee. It needs a named owner, a time limit, and evidence, not a catch block. [Should Authorization Fail Open, Fail Closed, or Defer?](fail-open-fail-closed-or-defer.md) covers how to make it deliberately.

---

## Retention and Cleanup

Two retention questions are easy to merge. **Refusal retention** is how long a consumption record must exist to keep refusing a grant. **Audit and recovery retention** is how long the operation record, and the evidence of who used which grant for what, must exist. The second is usually much longer, and deleting consumption rows does not mean deleting operation records.

For refusal retention, a consumption record must outlive every admission that could still succeed. That is later than the grant's deadline. In the token form, an admission statement that started just before the deadline evaluates `statement_timestamp()` at its start. If it then waits, and cleanup deletes the consumption row in the meantime, the consumption insert finds no conflict and succeeds. In this sample, a retained operation row is a second barrier: its unique grant key makes the operation insert, and with it the whole statement, fail, and the store reports a refusal. Do not rely on that incidental barrier in place of refusal retention, because operation records can be archived or deleted on their own schedule, and revocation rows have no operation row behind them at all. Close the window with a margin:

```sql
DELETE FROM grant_consumptions
WHERE retain_until < statement_timestamp() - @cleanup_margin;
```

`retain_until` is already the grant's expiry plus the admission leeway, for admissions and for revocations that have the trusted expiry. A fallback revocation row uses its write time plus the maximum grant lifetime, the issue-time skew, and the leeway. `@cleanup_margin` must exceed the longest an admission transaction can live, from the start of its statement through its commit. That includes lock waits and any synchronous-replication wait at commit. That bound only holds under three conditions:

- **The server enforces a bound on the whole admission transaction.** A client-side timeout is not enough, because the server can keep working after the client stops waiting. `statement_timeout` alone does not establish the bound either. PostgreSQL disables it by default, and with the extended query protocol most drivers use, it stops at the end of execution rather than at the end of the implicit transaction. From PostgreSQL 17, `transaction_timeout` bounds explicit transactions and implicit single-statement transactions alike. Prefer a dedicated admission role or connection with a verified `transaction_timeout`, not a server-wide setting or a role that also runs other work, because the setting applies to everything that role or connection does. If per-transaction configuration is required instead, note that `SET LOCAL` has no effect outside an explicit transaction block. That means wrapping the single admission statement in `BEGIN`, `SET LOCAL transaction_timeout`, the statement, and an immediate `COMMIT`, which relaxes the implicit-transaction assumption this section started with, though admission still stays one statement that commits at once. Verify that complete sequence against the deployed PostgreSQL version and driver, for example with a test that holds the statement past the limit, before relying on it. On earlier versions, verify your driver's protocol behavior and establish a finite upper bound on the whole admission transaction, including the commit acknowledgment.

  The synchronous-replication wait at commit needs its own bound. Once the commit is written locally and is waiting for a replica, a timeout cannot cut it short. It must be bounded some other way, for example by standby failover policy, and that bound added to the margin. If any part of the admission transaction cannot be given a finite bound, time-based cleanup is not safe: keep the consumption records, or coordinate cleanup with admission explicitly.
- **Admission runs as a standalone statement whose commit is part of it**, not inside an explicit transaction that could stay open afterwards.
- **The database server's clock does not jump** forward or backward by more than the margin while cleanup runs. Clock synchronization should slew time rather than step it.

If you cannot establish those bounds, keep consumption records rather than deleting them, or coordinate cleanup with admission explicitly. Rechecking expiry in the application just before the insert narrows the window. It does not close it, because the check and the insert are still two steps.

The two forms fail in opposite directions when a row disappears. In the row form, a missing grant row refuses admission, because the conditional update finds nothing to claim. In the token form, a missing consumption row admits, because the insert finds no conflict. The cleanup margin is what makes token-form deletion safe. Row-form archiving does not need it, as long as an archived grant can no longer be claimed.

For grants stored as rows, keeping the consumed row is often simpler than deleting it, and it doubles as evidence. If rows are archived, either the archive is still consulted, or the grant can no longer pass the deadline check once archived.

Retention is also a capacity question. Self-contained grants with short expiry keep the consumption table small. If identifiers in that table come from callers, cap their length. You can also store a SHA-256 hash of the issuer and grant ID, computed over an unambiguous encoding such as length-prefixed fields; with plain concatenation, `"ab" + "c"` and `"a" + "bc"` hash the same. With hashing, the hash column becomes the consumption table's primary key and the `ON CONFLICT` target, and every lookup uses the hash. A hash also loses the readable identifiers, so keep them in the operation record for audit. Hashing does not stop a flood of distinct identifiers, so pair it with issuer checks and rate limits. Issuers must never reuse a grant ID, including after the old one's records are deleted.

---

## Testing Across Store Instances and Restarts

The original tests could not have found the bug, because each test constructed one store object and exercised it in one process. Tests for the distributed claim have to model what production shares and what it does not. The two sketches below assume three illustrative pieces:

- a `SqlGrantAdmissionStore` that implements `IAsyncDisposable` and owns its connections;
- a test fixture that issues validated grants and exposes the database's connection string;
- an `IntentFor` helper that builds an operation intent matching a grant, with a new operation ID.

```csharp
[Fact]
public async Task Separate_store_instances_cannot_both_admit_the_same_grant()
{
    // Two store objects, each opening a pooled connection per call. They
    // share only the database, as two application instances would.
    await using var instanceA = new SqlGrantAdmissionStore(database.ConnectionString);
    await using var instanceB = new SqlGrantAdmissionStore(database.ConnectionString);
    ValidatedGrant grant = grants.IssueValid(expiresIn: TimeSpan.FromMinutes(15));

    // Any exception from a call fails the test.
    AdmissionResult[] results = await Task.WhenAll(
        Enumerable.Range(0, 50).Select(i =>
            (i % 2 == 0 ? instanceA : instanceB).TryAdmitAsync(
                grant, IntentFor(grant), CancellationToken.None)));

    Assert.Single(results, r => r.Outcome == AdmissionOutcome.Admitted);
    Assert.All(
        results.Where(r => r.Outcome != AdmissionOutcome.Admitted),
        r => Assert.Equal(AdmissionOutcome.Refused, r.Outcome));
}

[Fact]
public async Task A_recreated_store_client_still_refuses_an_admitted_grant()
{
    ValidatedGrant grant = grants.IssueValid(expiresIn: TimeSpan.FromMinutes(15));

    await using (var beforeRestart = new SqlGrantAdmissionStore(database.ConnectionString))
    {
        await beforeRestart.TryAdmitAsync(grant, IntentFor(grant), CancellationToken.None);
    }

    await using var afterRestart = new SqlGrantAdmissionStore(database.ConnectionString);
    AdmissionResult result = await afterRestart.TryAdmitAsync(
        grant, IntentFor(grant), CancellationToken.None);

    Assert.Equal(AdmissionOutcome.Refused, result.Outcome);
}
```

Name these tests for what they exercise. The first exercises the database's coordination between separate store instances, not every behavior of a deployment. The second simulates an application restart by recreating the client; it does not restart the database or test failover. Run them against a real instance of the store engine, for example in a disposable container. A naive fake that shares one dictionary between the two "instances" passes by construction, which hides exactly the property under test. An in-memory store fails both tests once each "instance" gets its own object and the grant is still valid when it is presented again, and that failure is the useful result.

Additional tests should include:

- **Admission committed, acknowledgment lost.** Inject a connection failure after the database commits and before the client reads the result. Billing must not be called. A retry with the same operation ID must report the admitted operation without creating a second claim. A retry with a different operation ID must be refused. A retry that arrives after the grant has expired must be refused by validation, and the status endpoint must still return the operation to its caller.
- **Crash immediately after admission.** The operation is `Admitted`, the grant is spent, and recovery, not a retry, takes it from there.
- **Same-operation retry while the first attempt is in flight.** The retry reports pending and does not call billing.
- **Operation ID reused with different details or by a different caller.** The request is refused, reveals no status, and leaves the other operation unchanged.
- **An unreachable store, or a cancelled admission.** Billing is never called, and the response is retryable.
- **Billing success followed by a failure to record it.** The caller gets pending, the operation stays `Admitted`, and recovery reconciles it to `Succeeded` without a second credit.
- **A definite billing refusal.** The operation is `Failed`, and the grant stays spent.
- **The original handler pauses past its lease.** Recovery takes ownership, then the handler resumes. Its late write changes nothing, and a late `Unknown` cannot overwrite a recovered `Succeeded`.
- **Recovery after the provider's idempotency window.** If billing cannot confirm the outcome, recovery marks `NeedsReconciliation` and sends no create request.
- **Recovery finds nothing while the original call may be in flight.** Recovery does not record `Failed`, and sends no create request unless the provider documents atomic handling of concurrent requests with the same key.
- **Expiry and cleanup with a delayed admission.** The risk is a statement that starts before the deadline and is delayed before it reaches the unique-key check, while cleanup deletes the earlier, committed consumption row. Use controlled synchronization that holds the admission statement before its claim reaches the uniqueness check. One candidate is a table lock held in another session, such as `LOCK TABLE operations IN SHARE ROW EXCLUSIVE MODE`, but use it only after database instrumentation shows where the statement actually waits. Do not assume that blocking the operation insert also blocks the earlier claim. Start a production-style standalone admission just before the deadline, run cleanup past the boundary, then release the lock. Confirm, for example in `pg_locks`, that the attempt waited before its claim ran. A delay placed after the claim lets the test pass without any bound, because the claim has already seen the old row. If you cannot place the delay reliably, split the test in two: one that the server-side bound aborts a delayed admission, and one that cleanup never removes a row before `retain_until` plus the margin. The server-side bound must abort the delayed attempt first, and the aborted attempt must leave no consumption row, no operation row, and no billing call. Also run the attempt with the operation row archived, so the incidental second barrier does not hide a missing bound. A grant still within `retain_until` is still refused after cleanup. Separately, check that a deployment without the server-side bound is detected as unsupported, for example by a startup check of the admission role's settings.
- **Concurrent revocation and admission.** Exactly one of them wins, and a revocation after admission changes nothing.
- **Revocation rules.** A revoker without the privilege is refused, a revocation for a grant ID with no proof of issuance is refused, and a revocation row survives cleanup until its `retain_until` plus the margin.
- **Submission deadline.** A handler or recovery job that reaches `MaySubmitAsync` after `submit_by`, or after losing ownership, sends nothing to billing.
- **Failover or restore.** Where the claim covers it, an admission acknowledged before a failover or a restore from backup is still present afterwards.

For a deployment-level check, run two instances of the application behind a load balancer in a test environment, present one grant to each, and restart one instance between presentations. That catches routing and configuration problems, such as an accidental in-memory registration in one environment, that unit and integration tests cannot see.

---

## When an In-Memory Store Is Enough

An in-memory consumption store is the right choice when the claim it supports is the one the system needs:

- **Teaching, demos, and unit tests**, where the point is to show the consume step and its outcomes without infrastructure. Say "one-time within this process" in the code and the documentation, and remember the dictionary never evicts.
- **A genuinely single-process host** where grants cannot outlive the process, for example a local tool that signs grants with an ephemeral per-process key, so a new process cannot accept grants issued by the old one. Issuing grants in the same process is not enough on its own: a persistent signing key would let the next process accept them. Otherwise, a replay after restart must be explicitly accepted, documented, and owned.
- **A local fast path in front of an authoritative store.** A process-local cache of grants already known to be consumed can refuse obvious replays without a round trip. It can only ever add refusals. An entry missing from it means "ask the store", never "unused".

It is the wrong choice whenever more than one instance can execute the operation, the process can restart while grants are still valid, or the documentation says the grant is single-use without saying "per process".

---

## Failure Modes

1. **Testing atomicity in one process and claiming it across many.** Thread-safety tests demonstrate the first row of the table, not the last.
2. **Registering the store with the wrong lifetime.** A scoped in-memory store is reset with each scope and a transient one with each resolution, which makes the grant reusable even on one instance.
3. **Check-then-act through a remote store.** Reading "absent" and then writing "consumed" in two calls is the original race with network latency added.
4. **Admitting at the wrong point.** After execution, two callers can both execute before either records the use. Before every other check, a request rejected for an unrelated reason, or a predictable billing refusal, spends the grant.
5. **Spending the grant and recording the operation in separate commits.** A crash or a fast retry between the two finds a spent grant with nothing to report or recover.
6. **Checking the deadline or revocation only before the claim.** A request can pass an application-side check, wait, and then be admitted although its admission deadline has passed or the grant was revoked in the meantime.
7. **Trusting request fields over the validated grant.** The grant authorizes one account and amount; the request can name another.
8. **Releasing the grant when the outcome is unknown.** The effect may exist, and a released grant permits a second one.
9. **Falling back to memory when the store is down.** It reopens the cross-instance path during exactly the window the shared store cannot help.
10. **Deleting refusal evidence while a qualifying admission can still finish.** A delayed statement that began before the deadline can then claim the grant again, even after the nominal deadline has passed.
11. **Treating an operation ID as a credential.** Matching an ID does not show that the caller may see or resume that operation.
12. **Letting recovery and the original executor race.** Unconditional status writes let a stale worker overwrite a newer result. Recording `Failed` because a lookup found nothing, resubmitting outside the provider's documented idempotency guarantees, or letting a delayed original handler submit after `submit_by`, can leave a second effect or a false terminal state.
13. **Treating idempotency or admission as exactly-once.** An operation key does not stop a spent grant from being presented with a new key, and admission does not show whether, or how many times, the effect happened.

---

## A Short Review Checklist

1. Does every instance that can execute the protected operation consult the same authoritative admission state?
2. Is the admission committed, and durably acknowledged for the failures the claim covers, before execution starts?
3. Is "a use remains" checked and claimed in one atomic operation, backed by a unique key or row lock that actually exists in the schema?
4. Is the admission deadline, with one fixed or versioned leeway, checked in the same statement as the claim against the store's clock, and do revocation and admission contend for the same row or key?
5. Is the claimed identity unambiguous, such as issuer plus grant ID, and taken from the validated grant?
6. Are the grant claim and the operation intent recorded in one statement or transaction, with a reused operation ID mapped to a refusal?
7. Is admission called at the execution boundary, after other validation and immediately before the effect?
8. Does an unreachable store, a cancellation, or an ambiguous admission timeout result in no execution and a retryable response?
9. Does a retry with the same operation ID, caller, and details get the original operation's status without executing, is everything else refused, and can the caller still reach the operation after the grant expires?
10. Are status changes conditional on the expected status and owner version, does each lease cover its owner's whole ownership period, is every create call preceded by a check of ownership and `submit_by`, and does recovery stop at manual reconciliation rather than record `Failed` or resubmit outside the provider's documented guarantees?
11. Are consumption records retained past the deadline by a margin longer than a finite, server-enforced bound on the admission transaction, and are operation records retained for audit separately?
12. Is revocation restricted, rate-limited, and limited to grants proven to have been issued, with its row retained through the grant's trusted expiry, or a validator-enforced maximum lifetime and issue-time skew?
13. Are the timing values kept in one place, with their relationships checked at startup?
14. Do integration tests use separate store instances and a recreated client against the real store engine, and cover a lost acknowledgment, a crash after admission, a paused executor, and delayed admission during cleanup?
15. If the in-memory store remains, is it limited to tests, demos, a documented single-process host, or a refusal-only fast path?

---

## Continue Deeper

For the full treatment of atomic consumption, bounded-use grants, revocation, failure windows, and multi-region consistency, read [Replay Protection and Bounded-Use Authority](../../security/replay-protection-and-bounded-use.md). Its sections on [multiple application instances](../../security/replay-protection-and-bounded-use.md#multiple-application-instances-change-the-requirement) and [process restart](../../security/replay-protection-and-bounded-use.md#process-restart-is-also-a-boundary) are the tutorial versions of this article's two incidents.

For the line between reused authority and repeated effects, and what to do after an ambiguous timeout, read [Idempotency Does Not Prevent Authority Replay](idempotency-does-not-prevent-authority-replay.md).

To decide whether a one-time grant is justified at all, or whether roles and claims are enough, read [Do You Need a Capability Token, or Are Roles and Claims Enough?](roles-claims-or-capability-token-dotnet.md).

For a grant that is issued in a request and consumed later by a background worker, [Capability-Scoped Background Operation](../../case-studies/capability-scoped-background-operation.md) follows the whole lifecycle across the queue.

### Related Work

Each source below was last verified on November 11, 2026.

- [RFC 9449, OAuth 2.0 Demonstrating Proof of Possession (DPoP)](https://www.rfc-editor.org/rfc/rfc9449), section 11.1, describes rejecting replayed proofs by remembering each proof's `jti` for as long as the proof would be accepted, and notes that this may not be feasible when several servers behind one endpoint share no state.
- [RFC 7519, JSON Web Token (JWT)](https://www.rfc-editor.org/rfc/rfc7519), section 4.1.7, defines the `jti` claim as a unique token identifier that can be used to prevent replay. When an application uses multiple issuers, it requires that their values not collide. The claim provides the identity to record; preventing replay still needs a store that records it.
- [`ConcurrentDictionary<TKey,TValue>.TryAdd`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.tryadd), in the .NET API reference, documents the add that returns `false` when the key already exists, which makes the in-memory store correct within one process.
- [Distributed caching in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed), in the ASP.NET Core documentation, describes the `IDistributedCache` interface and its get, set, refresh, and remove operations, which do not include a conditional set.
- PostgreSQL's documentation covers the database behavior these examples rely on:
  - the [date and time functions](https://www.postgresql.org/docs/current/functions-datetime.html) documentation distinguishes `now()`, which returns the transaction's start time, from `statement_timestamp()` and `clock_timestamp()`;
  - [Read Committed isolation](https://www.postgresql.org/docs/current/transaction-iso.html) explains how a concurrent `UPDATE` waits and re-evaluates its `WHERE` clause;
  - [data-modifying statements in `WITH`](https://www.postgresql.org/docs/current/queries-with.html) explains that all parts of the statement share one snapshot and that `RETURNING` makes changed rows available to the rest of the statement;
  - [`INSERT`](https://www.postgresql.org/docs/current/sql-insert.html) documents `ON CONFLICT DO NOTHING`;
  - [client connection defaults](https://www.postgresql.org/docs/current/runtime-config-client.html) documents `statement_timeout`, including its disabled default and when it stops under the extended query protocol, and `transaction_timeout`, added in PostgreSQL 17, which also covers implicit single-statement transactions.

---

## The Short Answer

A `ConcurrentDictionary` makes a grant one-time for one process until it restarts. Across instances and restarts, admit the grant with one conditional write to a store every instance shares and no restart loses. Check expiry and revocation in that same write, and record the operation in the same statement. Run nothing without a confirmed admission. That limits who may run the operation. Everything after admission is a second problem: let only one owner at a time move the operation forward, check ownership and the submission deadline before any external call, and stop at manual reconciliation when the provider can no longer guarantee its idempotency key. Provider-enforced idempotency suppresses duplicate effects within its contract, and operation records and reconciliation establish what happened when an outcome is uncertain.
