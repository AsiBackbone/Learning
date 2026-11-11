---
description: A one-time grant tracked in each instance's memory can be accepted twice after a retry or restart. Replay protection needs shared, durable, atomic consumption.
title: An In-Memory Grant Store Is Not Distributed Replay Protection
author: Christopher D. Cavell
published: "2026-11-11"
summary: A one-time grant whose consumption is tracked in each application instance's memory is one-time only per process. A retry that reaches another instance, or a request after a restart, finds no record and accepts the grant again. Replay protection across instances and restarts needs consumption state that every executing instance shares, that survives restarts, and that is claimed with one atomic conditional write at the execution boundary. That guarantee limits who may run the operation. It does not make the operation idempotent or its external effect exactly-once.
feed: true
x_hashtags:
  - DotNet
  - CyberSecurity
x_question: "Has an in-memory grant or capability store ever let the same operation through twice in production?"
---

# An In-Memory Grant Store Is Not Distributed Replay Protection

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** No formal prerequisites. Familiarity with ASP.NET Core, `ConcurrentDictionary`, running more than one instance of a service behind a load balancer, and SQL is helpful. No AsiBackbone package or prior Learning material is required.

**Sample scope:** The C# targets .NET 8 or later. The SQL uses PostgreSQL syntax, and the same contract can be met by other stores that offer a conditional write or a unique constraint. The store, the billing client, the operation records, and the helpers are illustrative, not a runnable sample, a production-ready pattern, or a recommended schema. The scenario and its incident are hypothetical. The prose is licensed under CC BY 4.0, and the code snippets are additionally offered under the MIT License, as described in the repository's [licensing terms](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md). External sources were last verified on November 11, 2026.

**What this article covers:** why a thread-safe in-memory store is correct inside one process and wrong across two; a concrete two-instance double acceptance and a restart that erases consumption history; why sticky sessions, a distributed cache, and short expiry do not close the gap by themselves; the contract a production consumption store must meet; one conditional write that makes the decision; where the consume call belongs; how replay protection differs from idempotency and from exactly-once external effects; what to do when the store is unavailable; how long consumption records must be kept; tests that would have caught the problem; and when an in-memory store is the right choice.

**In plain terms:** picture a voucher that can be redeemed once, at a shop with two tills. Each cashier keeps a private list of vouchers already redeemed. A customer redeems the voucher at till A, then walks to till B, where the cashier checks a list that has never seen it. At closing time, both lists go in the bin, and tomorrow the voucher looks new again everywhere. The voucher was never one-time. It was one-time per cashier, per day. Making it one-time means one shared list, kept overnight, where crossing out a voucher and checking it are the same act.

Consider a hypothetical customer-support application. A support agent can apply a goodwill credit to a customer's account, but only with a supervisor's approval. When a supervisor approves, the application issues a one-time grant: a signed token that names the account, the amount, and the agent, and that expires after 15 minutes. The credit itself is created through a billing provider's API.

The grant code was written carefully. It validates the signature, checks expiry, checks that the account and amount match the request, and then consumes the grant before calling billing:

```csharp
public sealed class InMemoryGrantConsumptionStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> consumed = new();

    public bool TryConsume(string grantId, DateTimeOffset now) =>
        consumed.TryAdd(grantId, now);
}
```

`TryAdd` is atomic. If two threads in the same process present the same grant at the same moment, exactly one of them gets `true`. The unit tests prove it, including a test that races a hundred tasks. The store is registered as a singleton, and in development the application runs as one process.

In production, it runs as two instances behind a round-robin load balancer, and deployments are rolling. This is what happened with grant `g-41c9`, which allowed one credit of $250 to account `A-1187`:

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

The agent saw a gateway timeout and clicked **Apply credit** again. The retry was routed to instance B. Instance B's dictionary had never seen `g-41c9`, so `TryAdd` returned `true`, and the customer received $500. Every line is accurate. Each instance enforced one use. The application did not.

The same week, the team found a second path. A rolling deployment restarted instance A at 10:40. A grant consumed on instance A at 10:38 was presented again at 10:44, still within its 15 minutes, and instance A accepted it. Its dictionary was new.

> **A grant is single-use only within the scope of the state that records its use. In-process memory gives you one process, until it restarts. Replay protection across instances and restarts needs consumption state that every executing instance shares, that outlives any process, and that is claimed with one atomic conditional write immediately before the protected operation runs. That guarantee decides who may run the operation. It does not decide whether the operation's effect happened, and it does not make that effect exactly-once.**

None of this means the in-memory store was a mistake. It was a correct implementation of a narrower claim. The defect was deploying it where the wider claim was assumed. Near the end, this article covers when an in-memory store is the right choice.

---

## Correct in One Process, Wrong Across Two

`ConcurrentDictionary` solves one problem: threads sharing one object. It makes the check and the claim a single step for every caller that holds a reference to that object. Nothing in it can coordinate with an object in another process, and nothing in it survives the process.

The replay guarantee is only as wide as the state behind it:

| Where consumption is recorded | Who agrees a grant is spent | What erases the record |
| --- | --- | --- |
| A `ConcurrentDictionary` in a singleton | Threads in that one process | Restart, crash, deployment, scale-in, app pool recycle |
| A `ConcurrentDictionary` in a scoped or transient service | Nothing useful; each request may get a fresh store | The end of the request |
| A distributed cache read and then written | Nobody reliably; two instances can both read "absent" | Eviction, expiry, failover, flush |
| A shared, durable store with an atomic conditional write | Every instance that consults it | Only a deliberate cleanup after the grant can no longer be accepted |

The first row is what the support application had. The tests passed because they exercised the first row's guarantee, and the incident occurred because production needed the last row's.

There are two ways to lose the record, and they need slightly different things:

- **Another instance** needs *shared* state: the consumption must be visible to whichever instance handles the next presentation, before it decides.
- **A restart** needs *durable* state: the consumption must be recorded somewhere the next process will read, and it must be recorded before the protected operation runs, so a crash between the two cannot lose it.

A store can be shared and not durable, such as a cache that loses keys on failover, or durable and not shared, such as a local file on each instance. Neither closes both paths.

---

## Three Fixes That Narrow the Gap Without Closing It

Teams that find this bug usually try one of these first. Each reduces how often it happens. None of them establishes the guarantee.

**Sticky sessions.** Session affinity routes a client back to the same instance when it can. It is a load-balancing preference, not a correctness boundary. A new connection, a different client, an instance removed during deployment or scale-in, and a failover all break it. It also does nothing for the restart path, and a replay attempt by someone other than the original client was never going to be sticky.

**A distributed cache.** `IDistributedCache` exposes get, set, refresh, and remove. Reading a key and then setting it is the same check-then-act race as before, only across the network: two instances can both read "absent" and both proceed. Some cache engines offer an atomic set-if-absent operation, which fixes the race. It leaves the other questions open. Can the engine evict the key under memory pressure? Can a failover lose a write that was acknowledged? Is the key kept as long as the grant can be presented? If any answer is yes, a consumed record can disappear while the grant is still acceptable. The choice of product does not settle the question; the store's behavior does.

**Shorter expiry.** A five-minute grant has a smaller replay window than a fifteen-minute grant. It still has one. In the incident, the retry came 18 seconds after the first request, well inside any expiry that leaves room for a human to act. Expiry limits *when* a grant may be accepted. Consumption limits *how many times*. They are separate checks, and a short expiry is a reason to keep consumption records for less time, not a reason to skip them.

---

## The Store Contract

A consumption store that supports the claim "this grant is accepted once across every instance and restart" has to meet six requirements. They describe behavior, not a product. A relational database, a strongly consistent key-value store, or a single service that owns grant state can each meet them.

1. **Shared at the scope of the claim.** Every instance that can execute the protected operation consults the same authoritative state. If grants are deliberately bound to one region or partition, the claim is scoped to that region or partition and says so.
2. **Atomic conditional consume.** Checking that a use remains and recording the use are one operation. Exactly one competing caller is told it won.
3. **Durable before execution.** The consumption is committed before the protected operation starts. A crash after the commit leaves the grant spent. A crash before it leaves the grant unspent and the operation not started.
4. **Decides with one clock.** Expiry is evaluated against one authoritative time source, or against tolerances wide enough to cover skew, so two instances with different clocks cannot reach different answers about the same grant.
5. **Distinct outcomes.** The caller learns which of these happened: consumed now; already consumed, and by which operation; expired; revoked; unknown grant. A store that cannot be reached is none of these, and it is never reported as "unused".
6. **Retained while the grant could be accepted.** A consumption record is kept at least until the grant can no longer pass validation anywhere: its expiry, plus any clock tolerance. A grant without an expiry needs its record for as long as the grant exists.

In C#, the boundary can be small:

```csharp
public enum GrantConsumeOutcome
{
    Consumed,
    AlreadyConsumed,
    Expired,
    Revoked,
    UnknownGrant,
}

public sealed record GrantConsumeResult(
    GrantConsumeOutcome Outcome,
    string? ConsumedByOperationId);

public interface IGrantConsumptionStore
{
    // Throws when the authoritative store cannot be reached or does not
    // confirm the write. That is not an outcome, and callers must not
    // treat it as "unused".
    Task<GrantConsumeResult> TryConsumeAsync(
        string grantId,
        string operationId,
        CancellationToken cancellationToken);
}
```

The name matters. `TryConsume` claims the use. A pair of methods named `IsConsumed` and `MarkConsumed` invites the race back in, however well each method is written.

---

## One Conditional Write Makes the Decision

When the application stores grants as rows, the decision is a single conditional update. The database evaluates the conditions and applies the change as one statement, and only one of two racing statements can change the row from unconsumed to consumed:

```sql
UPDATE grants
SET consumed_at = now(),
    consumed_by_operation = @operation_id
WHERE grant_id = @grant_id
  AND consumed_at IS NULL
  AND revoked_at IS NULL
  AND expires_at > now()
RETURNING grant_id;
```

One row returned means this caller consumed the grant. No rows means it did not, and a follow-up read of the row says why: already consumed and by which operation, revoked, expired, or missing. That read is only for reporting the reason. The update already made the decision, so a change between the update and the read cannot turn a refusal into an acceptance. Using the database's `now()` for both the expiry check and the timestamp gives every instance the same clock.

When the grant is a self-contained signed token with no server-side row, as in the support application, the instance validates the signature, expiry, and scope locally, and then records the consumption with a unique key:

```sql
INSERT INTO grant_consumptions (grant_id, operation_id, consumed_at, retain_until)
VALUES (@grant_id, @operation_id, now(), @expires_at + interval '5 minutes')
ON CONFLICT (grant_id) DO NOTHING
RETURNING grant_id;
```

The primary key on `grant_id` is the atomic check. A conflict means another caller got there first. `retain_until` is the token's expiry plus the clock tolerance used during validation, which is when cleanup may delete the row. Revocation for this kind of grant needs its own lookup, because nothing in the token can say it was withdrawn after it was signed.

Either form meets the contract only if the write is committed to storage that the deployment treats as durable, on a primary that every instance writes to, before the call returns. A replica that can be promoted without the latest committed writes reopens the restart path in a different shape. If the deployment spans regions, the guarantee is only as strong as the consistency story the store documents for cross-region writes.

---

## Where the Consume Call Belongs

Consumption belongs at the execution boundary: in the component that performs the protected operation, immediately before it does so, after every other check has passed. Consuming earlier, such as in an API gateway filter or when a request is queued, spends the grant on requests that are later rejected for other reasons. Consuming later, after the effect, reopens the race.

```csharp
public sealed class ApplyCreditHandler(
    IGrantValidator grantValidator,
    IGrantConsumptionStore grants,
    IOperationRecords operations,
    IBillingClient billing)
{
    public async Task<CreditResult> HandleAsync(
        ApplyCreditCommand command,
        CancellationToken cancellationToken)
    {
        // Signature, expiry, agent, account, and amount must all match the
        // request. Static validation alone never consumes anything.
        GrantValidation validation = grantValidator.Validate(command);
        if (!validation.IsValid)
        {
            return CreditResult.Rejected(validation.Reason);
        }

        GrantConsumeResult consumption = await grants.TryConsumeAsync(
            command.GrantId,
            command.OperationId,
            cancellationToken);

        if (consumption.Outcome == GrantConsumeOutcome.AlreadyConsumed
            && consumption.ConsumedByOperationId == command.OperationId)
        {
            // A retry of the operation that already spent this grant.
            // Report that operation's state; do not start another.
            return await operations.GetResultAsync(command.OperationId, cancellationToken);
        }

        if (consumption.Outcome != GrantConsumeOutcome.Consumed)
        {
            return CreditResult.Rejected(consumption.Outcome.ToString());
        }

        await operations.RecordStartedAsync(command.OperationId, command.GrantId, cancellationToken);

        try
        {
            BillingCredit credit = await billing.CreateCreditAsync(
                command.AccountId,
                validation.Amount,
                idempotencyKey: command.OperationId,
                cancellationToken);

            await operations.RecordSucceededAsync(command.OperationId, credit.Id, CancellationToken.None);
            return CreditResult.Applied(credit.Id);
        }
        catch (Exception ex) when (BillingErrors.IsOutcomeUnknown(ex))
        {
            // The grant stays consumed. Reconciliation will find out whether
            // the credit exists; a new grant is not the way to find out.
            await operations.RecordUnknownAsync(command.OperationId, CancellationToken.None);
            return CreditResult.Pending(command.OperationId);
        }
    }
}
```

A few choices in this handler carry the rest of the argument:

- **The operation ID is created once per intended credit.** The application generates it when it renders the approval form, so a second click on the same form sends the same value. The retry in the incident would then have reached instance B, found the grant consumed by its own operation, and reported that operation's state instead of creating a second credit. A new approval produces a new operation ID and needs a new grant.
- **The operation ID does not grant anything.** It only matches when the grant was consumed by that same operation, so presenting someone else's operation ID with a spent grant returns that operation's status at most. Scope the status lookup to the grant and the caller if that status is sensitive.
- **A store failure is not caught.** If `TryConsumeAsync` throws, the handler exits without calling billing. Only a confirmed consumption leads to execution.
- **Spent authority is not refunded on failure.** If billing fails with an unknown outcome, the credit may exist. Releasing the grant so the agent can try again is how a double credit gets reintroduced through a different door. The operation record says `Unknown`, and reconciliation against billing settles it.

When the protected effect is a write to the same database as the consumption record, the consumption and the effect can share one transaction, and either both commit or neither does. That is the one case where consumption and effect are atomic together. An external API is never inside that transaction.

---

## What This Guarantee Is, and What It Is Not

Three properties are easy to merge, and the support application needed all three:

| Property | Question it answers | Keyed on | What provides it |
| --- | --- | --- | --- |
| Replay protection | May this authority be accepted again? | The grant | Shared, durable, atomic consumption at the execution boundary |
| Idempotency | Should this operation's effect be produced again? | The operation | An operation ID reused across retries, recorded results, and a counterpart that honors the key |
| Exactly-once external effect | Did the effect happen exactly once in the other system? | The effect | Nothing on its own; at best, recorded attempts plus reconciliation against the counterpart's records |

Replay protection says that at most one claimant reaches the executor with a given grant. It says nothing about what happens next. The billing call can succeed, fail, or time out with an unknown result, and the consumption record is the same in all three cases.

Idempotency handles the next part. If billing honors `command.OperationId` as an idempotency key for long enough, a retried call with the same key does not create a second credit. That protection belongs to the operation, not to the grant: it would not stop someone from presenting a spent grant with a fresh operation ID. Only the consumption record stops that.

Exactly-once is the claim neither of them makes. A process can crash after billing creates the credit and before the operation record says so. Durable consumption ensures the grant cannot be used to try again; the operation record and reconciliation determine whether the credit exists. [Idempotency Does Not Prevent Authority Replay](idempotency-does-not-prevent-authority-replay.md) works through that split in detail, including what makes a later attempt materially new.

---

## When the Store Is Slow or Unavailable

Moving consumption out of process makes it a dependency. When it is slow or down, the protected operation has to wait, fail, or proceed without the check. For a one-time grant, proceeding without the check is the same as having no check, so the default is to not execute: return a retryable error and let the caller try again later with the same operation ID.

Two traps are worth naming:

- **Falling back to local memory during an outage.** It feels like graceful degradation. It recreates the two-instance bug at exactly the moment the shared record cannot help.
- **Treating a timeout on the consume call as a refusal.** The write may have committed. The safe reading is "not known", which means not executing now. A retry with the same operation ID will find either an unconsumed grant or one consumed by this operation, and both are handled.

If the business would rather risk a duplicate than block during an outage, that is a legitimate decision, but it is a policy decision with a named owner, a time limit, and evidence, not a catch block. [Should Authorization Fail Open, Fail Closed, or Defer?](fail-open-fail-closed-or-defer.md) covers how to make it deliberately.

---

## Retention and Cleanup

A consumption record must outlive every chance of the grant being accepted. For an expiring grant, delete it no earlier than its expiry plus the clock tolerance that validation allows, which is what `retain_until` records. A cleanup job that deletes by "consumed more than an hour ago" without looking at expiry will, for a two-hour grant, reopen the second hour.

For grants stored as rows, keeping the consumed row is often simpler than deleting it, and it doubles as evidence of who used the grant, for which operation, and when. If rows are archived, the archive must still be consulted, or the grant must be unable to pass validation once archived.

Retention is also a capacity question. Self-contained grants with short expiry keep the consumption table small. If identifiers in that table come from callers, bound their length or store a hash, so the table cannot be used to exhaust storage.

---

## Testing for Instances and Restarts

The original tests could not have found the bug, because each test constructed one store object and exercised it in one process. Tests for the distributed claim have to model what production does: more than one instance and more than one process lifetime, against the real store engine.

```csharp
[Fact]
public async Task Two_instances_cannot_both_consume_the_same_grant()
{
    // Two store objects with separate connections stand in for two
    // application instances. They share only the database, as production does.
    var instanceA = new SqlGrantConsumptionStore(database.ConnectionString);
    var instanceB = new SqlGrantConsumptionStore(database.ConnectionString);
    string grantId = await database.IssueGrantAsync(expiresIn: TimeSpan.FromMinutes(15));

    GrantConsumeResult[] results = await Task.WhenAll(
        Enumerable.Range(0, 50).Select(i =>
            (i % 2 == 0 ? instanceA : instanceB)
                .TryConsumeAsync(grantId, $"op-{i}", CancellationToken.None)));

    Assert.Single(results, r => r.Outcome == GrantConsumeOutcome.Consumed);
    Assert.All(
        results.Where(r => r.Outcome != GrantConsumeOutcome.Consumed),
        r => Assert.Equal(GrantConsumeOutcome.AlreadyConsumed, r.Outcome));
}

[Fact]
public async Task A_restart_does_not_reopen_a_consumed_grant()
{
    string grantId = await database.IssueGrantAsync(expiresIn: TimeSpan.FromMinutes(15));

    await using (var beforeRestart = new SqlGrantConsumptionStore(database.ConnectionString))
    {
        await beforeRestart.TryConsumeAsync(grantId, "op-1", CancellationToken.None);
    }

    var afterRestart = new SqlGrantConsumptionStore(database.ConnectionString);
    GrantConsumeResult result =
        await afterRestart.TryConsumeAsync(grantId, "op-2", CancellationToken.None);

    Assert.Equal(GrantConsumeOutcome.AlreadyConsumed, result.Outcome);
    Assert.Equal("op-1", result.ConsumedByOperationId);
}
```

Run these against a real instance of the store engine, for example in a disposable container, not against a fake. A fake shares memory between the two "instances" by construction, which is exactly the property under test. The in-memory store fails both tests as soon as each "instance" gets its own object, and that failure is the useful result.

Three more tests complete the set:

- **An unreachable store means no execution.** Point the handler at a store that throws, and assert that the billing client was never called and the response is retryable.
- **A retry with the same operation ID returns the original operation's state.** It must not create a second operation or call billing again.
- **Cleanup respects expiry.** A record whose grant has not passed `retain_until` survives the cleanup job, and the grant is still refused afterwards.

For a deployment-level check, run two instances of the application behind a load balancer in a test environment, present one grant to each, and restart one instance between presentations. That test exercises the routing and configuration, such as an accidental in-memory registration in one environment, that unit and integration tests cannot see.

---

## When an In-Memory Store Is Enough

An in-memory consumption store is the right choice when the claim it supports is the one the system needs:

- **Teaching, demos, and unit tests**, where the point is to show the consume step and its outcomes without infrastructure. Say "one-time within this process" in the code and the documentation.
- **A genuinely single-process host** where a replay after restart is acceptable, documented, and owned, for example a local tool whose grants die with the process because they are issued by it.
- **A local fast path in front of an authoritative store.** A process-local cache of grants already known to be consumed can refuse obvious replays without a round trip. It can only ever add refusals. An entry missing from it means "ask the store", never "unused".

It is the wrong choice whenever more than one instance can execute the operation, the process can restart while grants are still valid, or the documentation says the grant is single-use without saying "per process".

---

## Failure Modes

1. **Testing atomicity in one process and claiming it across many.** Thread-safety tests prove the first row of the table, not the last.
2. **Registering the store with the wrong lifetime.** A scoped or transient in-memory store is reset per request or per resolution, which makes the grant reusable even on one instance.
3. **Check-then-act through a remote store.** Reading "absent" and then writing "consumed" in two calls is the original race with network latency added.
4. **Consuming after execution.** Two callers can both execute before either records the use.
5. **Consuming before every other check.** A request rejected for an unrelated reason spends the grant and forces a reissue, which teaches people to reissue grants casually.
6. **Releasing the grant when execution fails.** After an unknown outcome, the effect may exist, and a released grant permits a second one.
7. **Falling back to memory when the store is down.** It reopens the cross-instance path during exactly the window the shared store cannot help.
8. **Deleting consumption records before the grant expires.** The grant becomes acceptable again for the rest of its lifetime.
9. **Treating idempotency as replay protection.** An operation-scoped key does not stop a spent grant from being presented with a new key.
10. **Describing the result as exactly-once.** Consumption limits who may execute. Whether the effect happened, and how many times, is established by operation records and reconciliation.

---

## A Short Review Checklist

1. Does every instance that can execute the protected operation consult the same consumption state?
2. Is the consumption committed to durable storage before execution starts, and is that storage one the deployment does not lose on restart or failover?
3. Is "a use remains" checked and claimed in one atomic operation, such as a conditional update or a unique insert?
4. Is expiry evaluated against one authoritative clock or within an explicit tolerance?
5. Does the store distinguish consumed, already consumed by which operation, expired, revoked, and unknown, and does an unreachable store result in no execution?
6. Is consumption called at the execution boundary, after other validation and immediately before the effect?
7. Does a retry with the same operation ID return the original operation's state instead of being rejected or executed again?
8. Is a consumed grant kept consumed when the effect fails or its outcome is unknown?
9. Are consumption records retained at least until the grant's expiry plus tolerance?
10. Do integration tests use more than one store instance and more than one process lifetime against the real store engine?
11. If the in-memory store remains, is it limited to tests, demos, a documented single-process host, or a refusal-only fast path?

---

## Continue Deeper

For the full treatment of atomic consumption, bounded-use grants, revocation, failure windows, and multi-region consistency, read [Replay Protection and Bounded-Use Authority](../../security/replay-protection-and-bounded-use.md). Its sections on [multiple application instances](../../security/replay-protection-and-bounded-use.md#multiple-application-instances-change-the-requirement) and [process restart](../../security/replay-protection-and-bounded-use.md#process-restart-is-also-a-boundary) are the tutorial versions of this article's two incidents.

For the line between reused authority and repeated effects, and what to do after an ambiguous timeout, read [Idempotency Does Not Prevent Authority Replay](idempotency-does-not-prevent-authority-replay.md).

To decide whether a one-time grant is justified at all, or whether roles and claims are enough, read [Do You Need a Capability Token, or Are Roles and Claims Enough?](roles-claims-or-capability-token-dotnet.md).

For a grant that is issued in a request and consumed later by a background worker, [Capability-Scoped Background Operation](../../case-studies/capability-scoped-background-operation.md) follows the whole lifecycle across the queue.

### Related Work

Each source below was last verified on November 11, 2026.

- [RFC 9449, OAuth 2.0 Demonstrating Proof of Possession (DPoP)](https://www.rfc-editor.org/rfc/rfc9449), section 11.1, describes rejecting replayed proofs by remembering each proof's `jti` for as long as the proof would be accepted, and notes that this may not be feasible when several servers behind one endpoint share no state.
- [RFC 7519, JSON Web Token (JWT)](https://www.rfc-editor.org/rfc/rfc7519), section 4.1.7, defines the `jti` claim as a unique token identifier that can be used to prevent replay. The claim provides the identity to record; preventing replay still needs a store that records it.
- [`ConcurrentDictionary<TKey,TValue>.TryAdd`](https://learn.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2.tryadd), in the .NET API reference, documents the atomic add that makes the in-memory store correct within one process.
- [Distributed caching in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed), in the ASP.NET Core documentation, describes the `IDistributedCache` interface and its get, set, refresh, and remove operations, which do not include a conditional set.

---

## The Short Answer

A one-time grant is one-time only within the state that records its use. A `ConcurrentDictionary` records it for one process until that process restarts, so a retry that reaches another instance, or a request after a deployment, accepts the grant again. To make the claim hold across instances and restarts, record consumption in a store every instance shares and no restart erases, claim it with one conditional write immediately before the protected operation, refuse to execute when that store cannot answer, and keep the record until the grant could no longer be accepted. That limits who may run the operation. Idempotency keys and reconciliation are still what keep the operation's effect from happening twice.
