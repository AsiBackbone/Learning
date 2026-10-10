---
description: Retrofit governed execution into a live ASP.NET Core app one operation at a time, with characterization tests, shadow evaluation, and safe rollback.
---

# Retrofitting Governed Execution into an Existing ASP.NET Core Application

**Pattern classification:** General learning material

**Difficulty:** Advanced

**Prerequisites:** Complete the [Refactor Scattered Governance Checks lab](../labs/refactor-scattered-governance-checks.md), which produces the target shape this playbook migrates toward. Read [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md) and [When a Simple Application Service Is Enough](../architecture/when-a-simple-application-service-is-enough.md) first, because the first phase may conclude that you should stop. Familiarity with [Data-Access Boundaries and Transaction Reasoning with EF Core](data-access-boundaries-and-transaction-reasoning.md) is assumed in Phase 8.

**Learning objective:** Introduce an explicit decision boundary into one consequential operation of an established ASP.NET Core application without a rewrite, without a fail-open rollout switch, without duplicated side effects, and without an evidence gap. Keep the old and new paths observable, testable, and safe throughout the transition.

## Pattern Card

> **Problem:** The governed-execution material shows the destination: explicit intent, authoritative context, a side-effect-free decision, and a host-owned executor. A team with a live application already has controllers, services, EF Core transactions, background jobs, queues, audit conventions, and clients that depend on current HTTP behavior. Moving to the destination in one step risks breaking all of them. Moving without a plan risks two authorities deciding the same operation, a rollback switch that quietly reopens an ungoverned path, or an outbox that sends the same message twice.
>
> **Pattern:** Migrate one operation at a time through reviewable phases. Pin current behavior with characterization tests, route every execution path through a single protected seam, introduce the decision beside the legacy logic, observe it without letting it authorize anything, enforce it for one path, and only then remove the legacy route. Each phase has entry criteria, invariants, validation, and a rollback condition.
>
> **Use when:** A specific operation has a demonstrated need that ordinary authorization and a simple application service do not meet, such as outcomes beyond allow and deny, decisions that must be reviewable later, execution in another component, or consequences severe enough that every route to the side effect must be accounted for.
>
> **Prefer something simpler when:** Access control is the whole problem, or the operation is immediate, local, low-consequence, and adequately audited already. The [stopping rule](#the-stopping-rule) applies before and after every phase.
>
> **Observe:** At every phase, exactly one component decides whether the side effect may run, and every route to that side effect is known.

This playbook does not repeat the target architecture. The [Refactor Scattered Governance Checks lab](../labs/refactor-scattered-governance-checks.md) and its [companion sample](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-pipeline-refactoring/README.md) build the target shape for one self-contained service: `AccountDisableContextBuilder`, `AccountDisablePolicy`, `AccountDisableDecisionPipeline`, `IAccountDisableExecutor`, and separate decision evidence. This page uses the same names and the same `account.disable` operation, and asks a different question: how does a team running a real application get there safely?

For exact, version-pinned package names, registration methods, middleware placement, and persistence contracts, use [From Learning Samples to a Production Host](../getting-started/from-learning-samples-to-production-host.md). This playbook stays framework-neutral and does not duplicate that implementation documentation.

---

## The Starting Application

The application is an established ASP.NET Core API for a multi-tenant product. Disabling an account is consequential: it ends the user's sessions, stops their scheduled jobs, and notifies the tenant's administrators.

The visible path is a controller:

```csharp
[ApiController]
[Route("api/accounts")]
public sealed class AccountsController(IAccountService accounts) : ControllerBase
{
    [HttpPost("{accountId}/disable")]
    [Authorize(Policy = "CanDisableAccount")]
    public async Task<IActionResult> Disable(
        string accountId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        DisableAccountResult result =
            await accounts.DisableAsync(accountId, User, idempotencyKey, cancellationToken);

        return result.Status switch
        {
            DisableStatus.Disabled or DisableStatus.AlreadyDisabled => NoContent(),
            DisableStatus.NotFound => NotFound(),
            DisableStatus.UnderInvestigation => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Account is under investigation."),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
```

The application service does the work, and some of the rules, inside one EF Core unit of work:

```csharp
public sealed class AccountService(AppDbContext db, TimeProvider clock) : IAccountService
{
    public async Task<DisableAccountResult> DisableAsync(
        string accountId,
        ClaimsPrincipal user,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        Account? account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account is null)
        {
            return DisableAccountResult.NotFound();
        }

        if (account.IsDisabled)
        {
            return DisableAccountResult.AlreadyDisabled();
        }

        if (account.PendingInvestigation)
        {
            return DisableAccountResult.UnderInvestigation();
        }

        account.Disable(user.GetUserId(), clock.GetUtcNow());
        db.Outbox.Add(OutboxMessage.AccountDisabled(account.Id, account.TenantId));
        db.AuditEntries.Add(AuditEntry.For(user, "account.disable", account.Id));

        await db.SaveChangesAsync(cancellationToken);
        return DisableAccountResult.Disabled();
    }
}
```

An outbox dispatcher later publishes `AccountDisabled`, and a consumer revokes the user's sessions at the identity provider.

Nothing here is wrong in the ordinary sense. Authorization is handled by ASP.NET Core, the database writes are atomic, external effects go through an outbox, and an audit row is written. The rest of this page assumes that a later phase demonstrates a need this design does not meet. If no such need exists, the first phase ends the work.

---

## Phases at a Glance

| Phase | Authority used for execution | Evidence collected | Safe rollback condition | Legacy path remaining |
| --- | --- | --- | --- | --- |
| 0. Decide whether governance is justified | Legacy service and ASP.NET Core authorization | Existing logs and audit | Nothing changed | All |
| 1. Inventory and rank operations | Legacy | Inventory of every route to the side effect | Nothing changed | All |
| 2. Characterize current behavior | Legacy | Characterization test suite | Tests only; revert freely | All |
| 3. Create one protected-execution seam | Legacy rules, invoked through the new seam | Existing audit, plus one execution record per call through the seam | Revert the seam commit; characterization tests still pass | Legacy rules; every route now passes through the seam |
| 4. Introduce intent, context, and decision | Legacy | Unit tests of the policy, compared to legacy rules | Delete unused new types | Legacy rules decide everything |
| 5. Observe in shadow | Legacy | Shadow comparison records, kept separate from authority | Disable shadow evaluation; no behavior change | Legacy rules decide everything |
| 6. Enforce for one route | The decision, for the API route only | Decision record for every outcome, plus execution record | Switch the API route back to legacy rules at the same seam | Legacy rules for other routes; legacy rule set retained for rollback |
| 7. Add receipts, acknowledgment, or scoped authority, if required | The decision, plus continuation evidence where required | Decision, continuation, and execution records | Disable the continuation feature; outcome reverts to `Denied` or `Deferred`, never to `Allowed` | As in Phase 6 |
| 8. Migrate asynchronous and transactional edges | The decision, for every route | Decision and execution records written atomically with state and outbox | Per-route switch back to legacy rules at the same seam | Legacy rule set retained only for rollback |
| 9. Roll out, then remove the legacy path | The decision, everywhere | Decision and execution records; reconciliation reports | Before removal only: switch to legacy rules at the seam | None after removal |

Two columns matter more than the rest. **Authority used for execution** must name exactly one thing in every row. **Legacy path remaining** must never include a route that bypasses the seam once Phase 3 is complete.

---

## Phase 0 — Decide Whether Governance Is Justified

**Entry criteria:** A specific operation, and a specific problem with it.

Start from the problem, not the pattern. Ordinary ASP.NET Core authorization answers whether this user may perform this operation on this resource, and it answers well. A simple application service handles immediate, local workflows well. Neither is a lesser design.

Write down the concrete pressure that ordinary authorization and the existing service do not meet. For `account.disable`, credible examples include:

- **Outcomes beyond allow and deny.** An account under investigation should defer to the case team rather than fail with `409`. An account with active privileged sessions needs an acknowledgment from the operator.
- **Decisions that must be reviewable later.** Auditors ask which rule allowed a particular disable, under which policy version, and the audit row records only that it happened.
- **Execution in another component.** A nightly job disables dormant accounts with no request, no user, and no authorization check.
- **Every route must be accounted for.** An incident showed that an account was disabled through an admin page that skipped the investigation check.

If none of these applies, stop. [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md) and [When a Simple Application Service Is Enough](../architecture/when-a-simple-application-service-is-enough.md) describe what to keep instead. The [Authorization-to-Governance Comparison sample](https://github.com/AsiBackbone/Learning/blob/main/samples/authorization-to-governance-comparison/README.md) shows the same operation at three levels side by side, so the team can see which outcomes and evidence each level actually adds.

**Keep authorization where it is.** Governance does not replace authentication or ASP.NET Core authorization. `[Authorize(Policy = "CanDisableAccount")]` keeps answering the access-control question throughout this migration. The new decision consumes its result as one input and adds the outcomes access control was not designed to express.

**Invariant:** None yet; nothing has changed.

**Exit:** A short written justification naming the operation, the demonstrated failure mode, and the outcome or evidence the current design cannot express. If the justification is weak, this is where the work ends.

---

## Phase 1 — Inventory and Rank Operations

**Entry criteria:** Phase 0 justified governance for at least one operation.

Do not convert the application. Choose one narrow operation with a consequential side effect and find **every route to that side effect**, not only the obvious endpoint.

Identify the side effect first, then work backward. For `account.disable`, the effect is "an account becomes disabled", which means `Account.IsDisabled` changes to `true`. Search for every writer:

| Route | Where | Current checks |
| --- | --- | --- |
| API endpoint | `AccountsController.Disable` → `AccountService.DisableAsync` | Authorization policy, not found, already disabled, under investigation |
| Admin page | `Pages/Admin/Accounts/Disable.cshtml.cs` calls `account.Disable` directly | Authorization policy only |
| Nightly job | `DormantAccountJob : BackgroundService` sets `IsDisabled` through a bulk `ExecuteUpdateAsync` | None |
| Support script | A SQL script in the operations runbook | Database permissions only |
| Downstream effect | `AccountDisabled` consumer revokes sessions at the identity provider | Trusts the message |

Search more than the call graph. Bulk updates such as `ExecuteUpdateAsync` and raw SQL bypass entity methods. Runbooks, migrations, and admin tools are routes too. The downstream consumer is not a route to the decision, but it performs part of the effect, so it matters in Phase 8.

Rank candidate operations by consequence and by how many unaccounted routes they have. Start with one. `account.disable` with four writers and one inconsistent check is a good first candidate.

**Invariant:** The inventory lists every known writer of the protected state.

**Exit:** A reviewed inventory, attached to the migration issue, with an owner for each route.

---

## Phase 2 — Characterize Current Behavior

**Entry criteria:** One chosen operation and its route inventory.

Before moving any boundary, write tests that pin what the application does today, including behavior that seems wrong. These are characterization tests: they describe the system rather than the specification, so a later phase can show that it changed only what it meant to change.

Cover, for the chosen operation:

- **Authentication and authorization:** anonymous `401`, wrong role `403`, cross-tenant `403`.
- **Validation and lookup:** unknown account `404`.
- **HTTP and Problem Details:** `409` with its current title for an account under investigation.
- **Idempotency:** a repeated request with the same `Idempotency-Key`, and a request for an already-disabled account, both return `204` without a second outbox message.
- **Transactions:** the state change, outbox message, and audit row commit together or not at all.
- **Emitted messages:** exactly one `AccountDisabled` per successful disable.
- **Every route from the inventory:** the admin page and the nightly job, not only the API.

A characterization test for the API route, using `WebApplicationFactory` and the production database provider:

```csharp
public sealed class DisableAccountCharacterizationTests(AppFactory factory) : IClassFixture<AppFactory>
{
    [Fact]
    public async Task Account_under_investigation_returns_409_and_changes_nothing()
    {
        await factory.SeedAsync(Accounts.UnderInvestigation("acct-17", tenant: "tenant-a"));
        HttpClient client = factory.CreateClientFor(Actors.TenantAdministrator("tenant-a"));

        HttpResponseMessage response = await client.PostAsync("/api/accounts/acct-17/disable", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.False(await factory.IsDisabledAsync("acct-17"));
        Assert.Empty(await factory.OutboxMessagesAsync("AccountDisabled"));
    }

    [Fact]
    public async Task Repeated_request_with_same_idempotency_key_emits_one_message()
    {
        await factory.SeedAsync(Accounts.Active("acct-21", tenant: "tenant-a"));
        HttpClient client = factory.CreateClientFor(Actors.TenantAdministrator("tenant-a"));

        await client.SendAsync(Disable("acct-21", idempotencyKey: "k-1"));
        HttpResponseMessage second = await client.SendAsync(Disable("acct-21", idempotencyKey: "k-1"));

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Single(await factory.OutboxMessagesAsync("AccountDisabled"));
    }
}
```

Run transaction and concurrency tests against the production provider or a faithful substitute. The EF Core in-memory provider does not model transactions, so it can make a broken unit of work look correct. [Data-Access Boundaries and Transaction Reasoning with EF Core](data-access-boundaries-and-transaction-reasoning.md#why-the-ef-core-in-memory-provider-is-weak-for-transaction-tests) explains why.

Characterization often finds surprises. Here, the admin page disables accounts under investigation, which the API forbids. Record that as a characterized behavior and a known defect. Do not fix it in this phase, because fixing it changes behavior before there is a safe place to change it.

**Invariant:** Every route in the inventory has at least one test that pins its current behavior.

**Exit:** The suite passes against the unmodified application and runs in CI.

---

## Phase 3 — Create One Protected-Execution Seam

**Entry criteria:** Characterization suite green.

Wrap the actual effect in one component, without changing what it does, and route every writer through it. This is the most important structural step. Every later phase relies on there being exactly one way to reach the side effect.

```csharp
public interface IAccountDisableExecutor
{
    Task<AccountDisableExecution> ExecuteAsync(AccountDisableCommand command, CancellationToken cancellationToken);
}

public sealed class AccountDisableExecutor(AppDbContext db, TimeProvider clock) : IAccountDisableExecutor
{
    public async Task<AccountDisableExecution> ExecuteAsync(
        AccountDisableCommand command,
        CancellationToken cancellationToken)
    {
        // The same writes the legacy service made, in the same unit of work.
        Account account = await db.Accounts.SingleAsync(a => a.Id == command.AccountId, cancellationToken);

        account.Disable(command.ActorId, clock.GetUtcNow());
        db.Outbox.Add(OutboxMessage.AccountDisabled(account.Id, account.TenantId));
        db.AuditEntries.Add(AuditEntry.For(command.ActorId, "account.disable", account.Id));
        db.AccountDisableExecutions.Add(AccountDisableExecutionRecord.From(command, clock.GetUtcNow()));

        await db.SaveChangesAsync(cancellationToken);
        return AccountDisableExecution.Completed(command);
    }
}
```

`AccountService.DisableAsync` keeps its checks and now calls the executor instead of writing directly. The admin page and the nightly job call the executor too, each still with its own legacy checks. Behavior is unchanged, and the characterization suite proves it. The only new thing is one execution record per call through the seam.

Then make other routes hard to add and easy to detect:

- **Close the entity method.** Make `IsDisabled` settable only through `Account.Disable`, and make the executor the only caller. Confirm it with "Find All References", and keep the result with the pull request.
- **Test the dependency direction.** Add an architecture test, using an architecture-testing library or a Roslyn-based check, that fails if any type other than the executor calls `Account.Disable` or issues a bulk update against `Accounts.IsDisabled`.
- **Replace the bulk update.** The nightly job's `ExecuteUpdateAsync` bypassed the entity entirely. It now loads candidate identifiers and calls the executor once per account. This costs throughput; accept the cost or batch inside the executor, but do not keep a second writer.
- **Retire or route out-of-band tooling.** The support SQL script either becomes an admin action that calls the executor, or is removed from the runbook, with database permissions narrowed to match.
- **Detect what static checks miss.** Add a reconciliation query, run on a schedule, that reports any account whose `IsDisabled` changed without a matching execution record. It catches raw SQL, restored backups, and future routes nobody added to the inventory.

**Invariant:** Every route that disables an account calls `IAccountDisableExecutor`, and the reconciliation report is empty.

**Validation:** Characterization suite unchanged and green; the architecture test passes; the reconciliation report is empty after a full test run.

**Rollback:** Revert the seam commit. Because behavior did not change, nothing downstream depends on the seam yet.

---

## Phase 4 — Introduce Explicit Intent, Authoritative Context, and Decision

**Entry criteria:** One seam, all routes through it.

Build the decision components from the refactoring lab beside the legacy code, without wiring them into execution:

```csharp
public sealed record AccountDisableIntent(
    string IntentId,         // the Idempotency-Key when present; otherwise generated
    string ActorId,
    string AccountId,
    string Route);           // "api", "admin-page", "dormancy-job"

public sealed record AccountDisableContext(
    AccountDisableIntent Intent,
    bool AccessGranted,      // the ASP.NET Core authorization result, not a replacement for it
    AccountSnapshot Account, // tenant, disabled, investigation status, version
    CaseStatus Investigation,
    bool HasPrivilegedSessions);
```

`AccountDisableContextBuilder` loads the context from authoritative sources, never from request fields. The investigation status comes from the case-management service, not from a cached column. `AccountDisablePolicy.Evaluate` is a pure function of that context. It returns an outcome and a stable reason code, and performs no writes, no calls, and no notifications. That is what lets later phases evaluate it safely more than once.

Carry the legacy semantics forward deliberately:

| Legacy behavior | New outcome | Reason code |
| --- | --- | --- |
| Account not found → `404` | Handled by the host before evaluation, as today | — |
| Already disabled → `204` | Handled by the host before evaluation, as today | — |
| Under investigation → `409` | `Deferred` | `account.under-investigation` |
| Otherwise → disable | `Allowed` | `account.disable.allowed` |
| Admin page disables accounts under investigation | `Deferred`, a deliberate behavior change scheduled for that route's enforcement | `account.under-investigation` |

Keep reason codes stable from this phase onward. Dashboards, support playbooks, and client error handling will depend on them.

**Invariant:** The policy has no side effects, and it is not yet called on any execution route.

**Validation:** Decision-table unit tests for the policy, including one test per legacy rule showing that it reproduces the legacy outcome, and explicit tests for each intended difference.

**Rollback:** Delete the unused types.

---

## Phase 5 — Observe Before Enforcing, Where Safe

**Entry criteria:** Policy tested; legacy still authoritative.

A shadow phase runs the new decision beside the legacy one on real traffic and records whether they agree. It finds context-loading defects, rules that do not match production data, and dependency latency before any of them can affect users.

Shadow evaluation is only safe if it cannot become a second authority. Make that structural, not a matter of discipline:

```csharp
public sealed class ShadowAccountDisableEvaluator(
    AccountDisableContextBuilder contexts,
    IShadowComparisonSink comparisons,
    ILogger<ShadowAccountDisableEvaluator> logger)
{
    // Returns nothing the caller could act on.
    public async Task CompareAsync(
        AccountDisableIntent intent,
        LegacyOutcome legacy,
        CancellationToken cancellationToken)
    {
        try
        {
            AccountDisableContext context = await contexts.BuildAsync(intent, cancellationToken);
            GovernanceDecision shadow = AccountDisablePolicy.Evaluate(context);

            await comparisons.RecordAsync(
                new ShadowComparison(intent.IntentId, legacy, shadow, Agrees(legacy, shadow)),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A shadow failure is a finding, never a change in the response.
            ShadowLog.EvaluationFailed(logger, intent.IntentId, exception);
        }
    }
}
```

The rules that keep shadowing honest:

- **It returns nothing.** `CompareAsync` has no result that a caller could pass to the executor.
- **It never reaches the executor.** The shadow evaluator has no dependency on `IAccountDisableExecutor`. The architecture test from Phase 3 can assert this.
- **Its records are not decision evidence.** Comparisons go to a separate store, labeled as shadow output, so nobody later mistakes "shadow said Allowed" for "this was allowed".
- **Its failures do not change behavior.** A timeout in the shadow context load is logged and counted, and the legacy response is unaffected.
- **It is bounded.** Sample traffic if the context sources cannot take the extra load, and run the comparison after the legacy response where latency matters.

Disagreements are the point. Each one is a defect in the new context or policy, a legacy defect the new policy fixes on purpose, or a case nobody specified. Triage them before Phase 6.

**When not to shadow:**

- **When evaluating has side effects.** If building context reserves quota, consumes a one-time token, or notifies someone, shadowing changes the world. Fix the context builder first, or skip shadowing.
- **When the new outcomes have no legacy counterpart.** Comparing `AcknowledgmentRequired` with a legacy design that cannot express it measures nothing useful. Test those outcomes directly instead.
- **When the change closes a security defect.** If the admin-page route is actively bypassing the investigation check, observing it for a month delays the fix. Enforce that route directly.
- **When the shadow path would send data somewhere new.** If the policy runs in a new service, shadow traffic is a new data flow that needs its own review.

**Invariant:** The executor is invoked by legacy authority only. No shadow result can reach it.

**Validation:** Shadow comparison rate, disagreement rate by reason code, and shadow failure rate, reviewed until disagreements are explained.

**Rollback:** Turn shadow evaluation off. Behavior does not change.

---

## Phase 6 — Enforce for One Route

**Entry criteria:** Disagreements triaged; dependency failure behavior decided.

Make the decision the authority for one route, starting with the API. The legacy checks in `AccountService` are replaced by the pipeline, and the executor is called only for `Allowed`:

```csharp
public sealed class AccountDisableDecisionPipeline(
    AccountDisableContextBuilder contexts,
    IDecisionEvidenceSink decisions,
    IAccountDisableExecutor executor)
{
    public async Task<AccountDisableResult> HandleAsync(AccountDisableIntent intent, CancellationToken cancellationToken)
    {
        AccountDisableContext context;

        try
        {
            context = await contexts.BuildAsync(intent, cancellationToken);
        }
        catch (DependencyUnavailableException exception)
        {
            // An outage is not permission. Without authoritative context there is no decision.
            GovernanceDecision unavailable =
                GovernanceDecision.Defer($"context.unavailable.{exception.DependencyName}");
            await decisions.RecordAsync(intent, unavailable, cancellationToken);
            return AccountDisableResult.NotExecuted(unavailable);
        }

        GovernanceDecision decision = AccountDisablePolicy.Evaluate(context);
        await decisions.RecordAsync(intent, decision, cancellationToken);

        if (!decision.CanExecute)
        {
            return AccountDisableResult.NotExecuted(decision);
        }

        AccountDisableExecution execution =
            await executor.ExecuteAsync(AccountDisableCommand.From(intent, decision), cancellationToken);
        return AccountDisableResult.Executed(decision, execution);
    }
}
```

The controller keeps its authorization attribute and maps outcomes at the HTTP boundary. Preserve characterized responses wherever the meaning is unchanged, and change them only where the migration intends to:

| Outcome | HTTP response | Executor calls |
| --- | --- | --- |
| `Allowed` | `204 No Content`, unchanged | 1 |
| `Denied` | `403` Problem Details with the reason code | 0 |
| `Deferred`, investigation | `409` Problem Details, the characterized response, now with a reason code | 0 |
| `Deferred`, dependency unavailable | `503` with `Retry-After`; a new response for a new condition | 0 |
| `AcknowledgmentRequired` (Phase 7) | `202` with a continuation link | 0 |
| `EscalationRecommended` (Phase 7) | `202` with a status link | 0 |

Keeping the investigation case at `409` is a choice to avoid breaking clients. Changing it to `503` or `202` would be defensible, but it is a contract change and belongs in its own reviewed step. [Centralized Error Handling and Problem Details](centralized-error-handling-and-problem-details.md) covers keeping expected outcomes out of the exception path.

**Dependency failure fails closed.** If the case-management service is down, the decision is `Deferred`, never `Allowed`. A timeout, an open circuit breaker, or an exception in the context builder must not become permission. [Should Authorization Fail Open, Fail Closed, or Defer?](../articles/2026/fail-open-fail-closed-or-defer.md) covers choosing that behavior per operation.

Now add the zero-execution tests that become the long-term guard:

```csharp
[Theory]
[InlineData("acct-under-investigation", HttpStatusCode.Conflict)]
[InlineData("acct-other-tenant", HttpStatusCode.Forbidden)]
[InlineData("acct-case-service-down", HttpStatusCode.ServiceUnavailable)]
public async Task Blocked_outcomes_never_reach_the_executor(string accountId, HttpStatusCode expected)
{
    RecordingExecutor executor = factory.ReplaceExecutorWithRecordingDecorator();
    HttpClient client = factory.CreateClientFor(Actors.TenantAdministrator("tenant-a"));

    HttpResponseMessage response = await client.PostAsync($"/api/accounts/{accountId}/disable", content: null);

    Assert.Equal(expected, response.StatusCode);
    Assert.Equal(0, executor.Invocations);
    Assert.Empty(await factory.OutboxMessagesAsync("AccountDisabled"));
}
```

The recording decorator wraps the real executor rather than replacing it, so the test still exercises the real persistence path when the outcome is `Allowed`. Assert on the executor and on the outbox, not on a status flag. [How to Test That a Denied Operation Never Executes](../articles/2026/test-denied-operation-never-executes.md) covers this assertion pattern in depth.

**Rollback without reopening a bypass.** The rollback switch chooses which rules decide, never whether rules run:

```csharp
public enum AccountDisableRuleSet
{
    Governed,      // AccountDisablePolicy through the pipeline
    LegacyRules    // the characterized legacy checks, wrapped as a policy
}
```

There is no `Off` value. Both rule sets run through the same pipeline, record a decision, and reach the same executor. A missing or unparseable setting resolves to `Governed`, and startup validation rejects unknown values, so a configuration error cannot produce an ungoverned path. Rolling back changes which rules apply. It does not create a second route to the side effect.

**Invariant:** For the API route, the executor is called only for an `Allowed` decision, and every attempt records a decision.

**Validation:** Characterization suite green, except for intended differences that are updated in the same pull request and called out in review. Zero-execution tests green. The reconciliation report is empty.

**Rollback:** Set the API route's rule set to `LegacyRules`. Decisions continue to be recorded, and the executor stays the only writer.

---

## Phase 7 — Add Receipts, Acknowledgment, or Scoped Authority Only When Required

**Entry criteria:** Enforcement stable for at least one route, and a demonstrated need for a continuation or delegated execution.

These are not maturity levels. Add each one only when the use case needs it:

- **Acknowledgment**, when an operator must explicitly accept a consequence. Here, disabling an account with active privileged sessions ends sessions on production systems, so the policy returns `AcknowledgmentRequired`. The operator acknowledges that exact decision, and the next request re-evaluates with the acknowledgment as input. The acknowledgment is evidence for the policy. It is not permission to execute. See [Decision Receipts and Acknowledgment](../tutorials/decision-receipts-and-acknowledgment.md).
- **Decision receipts**, when a later step or another team must verify what was decided without trusting the caller's account of it.
- **Scoped execution authority**, when execution moves to another component after the decision, such as a worker that disables accounts in bulk. The decision issues narrow, short-lived authority for one account and one operation, and the executor validates it. See [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md).

If none of these is needed, skip this phase. An `Allowed`/`Denied`/`Deferred` decision at a single protected seam may be the right final design.

**Rollback:** Turning a continuation feature off must not turn its outcome into `Allowed`. If the acknowledgment flow is disabled, the privileged-session case becomes `Denied` or `Deferred` with an explicit reason, and operators use an escalation path.

---

## Phase 8 — Migrate Asynchronous and Transactional Edges Deliberately

**Entry criteria:** The API route enforced; remaining routes listed with owners.

The remaining routes and the downstream effect are where most migration defects appear. Address each one explicitly.

**Write decision and execution evidence with the state change.** For an `Allowed` decision, the executor writes the account change, the outbox message, and the execution record in one `SaveChangesAsync`. They commit together or not at all. Decision records for non-allowed outcomes are written on their own, because there is no state change to share a transaction with. If the decision record must also be atomic with execution, write it in the same unit of work, and test that with the production provider. A local transaction ends at the database. It cannot include the identity provider, and nothing in this phase should claim otherwise.

**Make repeated execution impossible, not unlikely.** Key the execution record by intent identifier with a unique constraint. A retried HTTP request carrying the same `Idempotency-Key` resolves to the same intent, finds the existing execution, and returns the stored result instead of executing again. A retry after `Deferred` is a new attempt that gets a new decision, because the facts may have changed. A retry after a timeout whose outcome is unknown must look up the execution record first. If the outcome is still unknown, mark it for reconciliation instead of executing again.

**Treat the outbox consumer as an inbox.** The `AccountDisabled` consumer may receive a message more than once. Record processed message identifiers and skip duplicates, and prefer operations that are idempotent at the destination, as revoking sessions usually is. The consumer performs part of the effect but does not decide it. It trusts the message because only the executor writes it. That trust is why Phase 3 mattered.

**Route the background job through the pipeline.** `DormantAccountJob` now builds one intent per candidate account, with the job's workload identity as the actor and `"dormancy-job"` as the route, and calls `AccountDisableDecisionPipeline`. The job no longer decides dormancy alone. The policy can defer an account under investigation, and the job retries deferred accounts on its next run instead of forcing them. The job's identity needs permission to ask, not a separate path to execute.

**Enforce the admin page last, or first if it is a security defect.** The admin page's characterized behavior, disabling accounts under investigation, now becomes `Deferred`. This is the deliberate behavior change recorded in Phase 4. Announce it to the support team before it ships.

**Reconcile continuously.** Keep the Phase 3 reconciliation running and extend it:

- an account disabled with no execution record means a bypass;
- an execution record with no `Allowed` decision means a defect in the pipeline;
- an execution record whose outbox message was never dispatched means stalled delivery;
- an `AccountDisabled` message with sessions still active means a failed downstream effect.

Each finding has an owner and a runbook entry. [Data-Access Boundaries and Transaction Reasoning with EF Core](data-access-boundaries-and-transaction-reasoning.md#use-an-outbox-when-the-boundary-is-durable-messaging) covers the outbox, idempotency, and recovery boundaries in more depth.

**Invariant:** Every route reaches the executor only through the pipeline. Each intent executes at most once. Each published message is processed at most once by each consumer.

**Validation:** Zero-execution and exactly-once tests for every route, including job and admin page. Duplicate-delivery tests for the consumer. Reconciliation reports empty in a full staging run.

**Rollback:** Per route, set the rule set to `LegacyRules`. The seam, the execution keys, and the inbox stay in place, so rolling back cannot duplicate the side effect.

---

## Phase 9 — Roll Out, Then Remove the Legacy Path

**Entry criteria:** Every route enforced in a pre-production environment.

Roll out gradually, by environment and then by tenant if the application supports it. Watch for:

- decision outcome rates by reason code, compared with the shadow phase;
- `Deferred` caused by dependency outages, which is an availability signal for the case-management service;
- execution and reconciliation reports;
- client errors on the changed responses.

The legacy rule set exists only for rollback. Remove it when all of these are true:

1. Every route has run on `Governed` in production for an agreed period with no rollback.
2. Reconciliation has reported no bypass, no execution without an `Allowed` decision, and no duplicate execution in that period.
3. The rule-set switch has not been changed in that period.
4. Clients affected by changed responses have confirmed compatibility.
5. The runbook no longer references the legacy behavior.

Then delete `LegacyRules`, the switch, the shadow evaluator, and its comparison store, in one reviewed change. Keep the seam, the zero-execution tests, the architecture test, and reconciliation permanently. They are what keeps the next route from reopening the problem.

Record the migration's reasoning, including the rejected alternatives and the conditions under which the decision should be revisited, as an [Architecture Decision Record](architecture-decision-records-preserve-architectural-reasoning.md).

**Invariant:** After removal, there is one rule set, one pipeline, and one executor, and no switch can bypass them.

---

## The Stopping Rule

At the start and after any phase, stop when the next phase would not address a demonstrated failure mode.

Stopping early is a valid outcome:

- **After Phase 0**, when ordinary authorization and the existing service meet the requirement. That is the most common result, and it is a good one.
- **After Phase 3**, when the problem was an unaccounted route, not missing decision outcomes. A single protected seam, an architecture test, and reconciliation may be the whole fix.
- **After Phase 6**, when explicit outcomes and decision evidence at one seam are enough. Many operations never need acknowledgment, receipts, or scoped authority.

Do not continue to complete the table. A phase that adds lifecycle without addressing a demonstrated failure mode adds code, state, and new ways to fail. A simple application-service design that the team understands is better than a governed design that nobody can operate.

---

## Common Failure Modes

### Two authorities during transition

The new decision is enforced in the controller while the legacy checks still run in the service, and the two disagree. Each request is decided twice by different rules. Exactly one component decides at every phase; the phase table names it.

### A rollback switch with an `Off` setting

The feature flag routes around the pipeline when disabled, so a configuration mistake or rushed rollback restores an ungoverned path. The switch selects a rule set, both run through the same seam, and an invalid value resolves to the governed rules.

### Shadow output treated as evidence

A dashboard reports shadow `Allowed` counts as decisions, or a later change reads a shadow result to skip evaluation. Shadow records live in a separate store, carry a shadow label, and are deleted with the shadow evaluator.

### Enforcing one route while others still bypass

The API is governed while the nightly job keeps its bulk update. The seam in Phase 3 exists so this cannot happen silently, and reconciliation catches what the inventory missed.

### Outages becoming permission

A timeout in the context builder is caught and the request proceeds. Unavailable context produces `Deferred` and a recorded decision, never `Allowed`.

### Retry duplicates the side effect

A client retries after a timeout, and the operation executes twice. Execution records keyed by intent, an inbox at the consumer, and lookup-before-retry make repeated execution impossible rather than unlikely.

### Fixing characterized defects too early

A known defect, such as the admin page ignoring investigations, is fixed while moving boundaries. The behavior change then hides inside a refactoring. Record it in Phase 2 and change it deliberately in the phase that enforces that route.

---

## Review Checklist

**Before starting**

1. Is there a written, demonstrated failure mode that ordinary authorization and the existing service do not address?
2. Has the team agreed which single operation goes first?

**Structure**

3. Does the inventory list every writer of the protected state, including bulk updates, scripts, and admin tools?
4. Does every route call one protected executor, enforced by an architecture test and checked by reconciliation?
5. Is the policy free of side effects, so it can be evaluated in shadow and in tests?

**Transition**

6. Does every row of the phase table name exactly one authority for execution?
7. Can shadow evaluation reach the executor, or produce a value a caller could act on?
8. Does unavailable context produce `Deferred` with a recorded decision?
9. Can any rollback setting bypass the pipeline or the executor?

**Edges**

10. Are execution records keyed so a retried intent cannot execute twice?
11. Do consumers of the outbox deduplicate deliveries?
12. Does the background job go through the pipeline under its own workload identity?

**Finish**

13. Are the removal criteria for the legacy rule set written down, and measured?
14. Did the team stop at the first phase that resolved the demonstrated failure mode?

---

## Related Content

- [Refactor Scattered Governance Checks lab](../labs/refactor-scattered-governance-checks.md) — builds the target decision pipeline for one self-contained service. This playbook migrates a live application toward it.
- [Decision Pipeline Refactoring sample](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-pipeline-refactoring/README.md) — the runnable flawed and refactored services the type names on this page come from.
- [Build a Governed API Operation lab](../labs/build-a-governed-api-operation.md) — practice the full governed flow, including acknowledgment and scoped authority, in a disposable API.
- [Authorization-to-Governance Comparison sample](https://github.com/AsiBackbone/Learning/blob/main/samples/authorization-to-governance-comparison/README.md) — the same operation at three levels, to check what each phase actually adds.
- [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md) and [When a Simple Application Service Is Enough](../architecture/when-a-simple-application-service-is-enough.md) — the Phase 0 question and the stopping rule.
- [Data-Access Boundaries and Transaction Reasoning with EF Core](data-access-boundaries-and-transaction-reasoning.md) — transactions, idempotency, outbox, and recovery for Phase 8.
- [Should Authorization Fail Open, Fail Closed, or Defer?](../articles/2026/fail-open-fail-closed-or-defer.md) — choosing dependency-failure behavior per operation.
- [How to Test That a Denied Operation Never Executes](../articles/2026/test-denied-operation-never-executes.md) — the zero-execution assertion pattern.
- [From Learning Samples to a Production Host](../getting-started/from-learning-samples-to-production-host.md) — version-pinned package and host integration references for the seams on this page.

---

> **Read it. Run it. Question it. Improve it.**
