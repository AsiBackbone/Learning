---
description: A practical .NET testing article on proving that blocked decisions stop before repositories, external APIs, event publishers, or other protected side effects execute.
title: How to Test That a Denied Operation Never Executes
author: Christopher D. Cavell
published: "2026-08-26"
summary: A denied result is useful evidence, but a denied result plus zero protected executor calls proves the execution boundary held.
feed: true
---

# How to Test That a Denied Operation Never Executes

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** Familiarity with asynchronous C# and basic xUnit tests is helpful. The examples use C# 12 syntax; a .NET 8 or later SDK can compile them as written, but the testing pattern applies equally to older C#/.NET versions with equivalent syntax.

## Core Pattern in 60 Seconds

A .NET test passes:

```csharp
Assert.Equal(DecisionOutcome.Denied, result.Outcome);
```

That is useful: it proves the code returned `Denied`. It does **not** prove that the protected operation never happened. The service may have already written to a repository, called an external API, published an event, sent a notification, or invoked some other side-effecting dependency before it returned the correct result.

The stronger test asks a second question:

> **How do I test the absence of execution rather than only the decision result?**

For a deliberately visible execution boundary, the answer can be as small as:

```csharp
Assert.Equal(DecisionOutcome.Denied, result.Outcome);
Assert.Empty(executor.AccountIds);
```

Those assertions prove two different properties:

```text
Decision assertion
    ↓
Did the application reach the expected conclusion?

Execution assertion
    ↓
Did protected work remain unreachable?
```

For consequential operations, both can matter.

The central invariant is:

```text
Blocked decision
      ↓
Protected executor invocation count = 0
```

In this article, **protected operation** means the consequential state change or externally visible action that must not occur on a blocked path. **Executor** is the example host-owned boundary used to reach that operation; some systems will have several protected boundaries instead of one executor method.

This article uses ordinary C# and xUnit-style tests. No ASI Backbone package is required.

A minimal decision model for the examples is:

```csharp
public enum DecisionOutcome
{
    Allowed,
    Denied,
    Deferred,
    AcknowledgmentRequired,
    EscalationRecommended
}

public sealed record DisableAccountContext(
    string AccountId,
    bool RequesterIsAdministrator,
    bool IsProtectedAccount,
    bool MaintenanceHoldActive,
    bool RequiresAcknowledgment,
    bool AcknowledgmentSatisfied,
    bool RequiresEscalation)
{
    public static DisableAccountContext Standard() =>
        new(
            AccountId: "account-123",
            RequesterIsAdministrator: true,
            IsProtectedAccount: false,
            MaintenanceHoldActive: false,
            RequiresAcknowledgment: false,
            AcknowledgmentSatisfied: false,
            RequiresEscalation: false);

    public static DisableAccountContext ProtectedAccount() =>
        Standard() with { IsProtectedAccount = true };
}

public sealed record DisableAccountDecision(
    DecisionOutcome Outcome,
    string ReasonCode)
{
    public bool CanExecute => Outcome == DecisionOutcome.Allowed;

    public static DisableAccountDecision Allow() =>
        new(DecisionOutcome.Allowed, "account.disable.allowed");

    public static DisableAccountDecision Deny(string reasonCode) =>
        new(DecisionOutcome.Denied, reasonCode);

    public static DisableAccountDecision Defer(string reasonCode) =>
        new(DecisionOutcome.Deferred, reasonCode);

    public static DisableAccountDecision RequireAcknowledgment(
        string reasonCode) =>
        new(DecisionOutcome.AcknowledgmentRequired, reasonCode);

    public static DisableAccountDecision Escalate(string reasonCode) =>
        new(DecisionOutcome.EscalationRecommended, reasonCode);
}

public interface IDisableAccountPolicy
{
    DisableAccountDecision Evaluate(DisableAccountContext context);
}

public sealed class DisableAccountPolicy : IDisableAccountPolicy
{
    public DisableAccountDecision Evaluate(DisableAccountContext context)
    {
        if (!context.RequesterIsAdministrator)
        {
            return DisableAccountDecision.Deny(
                "account.disable.requester-not-administrator");
        }

        if (context.IsProtectedAccount)
        {
            return DisableAccountDecision.Deny(
                "account.disable.protected-account");
        }

        if (context.MaintenanceHoldActive)
        {
            return DisableAccountDecision.Defer(
                "account.disable.maintenance-hold");
        }

        if (context.RequiresAcknowledgment &&
            !context.AcknowledgmentSatisfied)
        {
            return DisableAccountDecision.RequireAcknowledgment(
                "account.disable.acknowledgment-required");
        }

        if (context.RequiresEscalation)
        {
            return DisableAccountDecision.Escalate(
                "account.disable.escalation-required");
        }

        return DisableAccountDecision.Allow();
    }
}
```

A real policy may return any of the listed outcomes. The examples below focus on the execution boundary rather than on a particular policy framework.

## A Correct Result Can Hide an Incorrect Execution Path

Consider a fictional administrative operation that disables an account.

The application knows that protected accounts must not be disabled through the normal path.

A flawed service might still do this:

```csharp
public async Task<DisableAccountDecision> DisableAsync(
    DisableAccountContext context,
    CancellationToken cancellationToken)
{
    await accountExecutor.DisableAsync(
        context.AccountId,
        cancellationToken);

    if (context.IsProtectedAccount)
    {
        return DisableAccountDecision.Deny(
            "account.disable.protected-account");
    }

    return DisableAccountDecision.Allow();
}
```

A decision-only test can pass:

```csharp
DisableAccountDecision result = await service.DisableAsync(
    DisableAccountContext.ProtectedAccount(),
    CancellationToken.None);

Assert.Equal(DecisionOutcome.Denied, result.Outcome);
```

The test is green, but the account executor was still invoked. The failure is architectural rather than syntactic: the side effect occurred before the application discovered the blocking decision. A correct return value cannot retroactively make the earlier side effect disappear.

## Put One Observable Boundary Around the Protected Operation

The easiest way to test non-execution is to make protected execution a dependency that the test can observe.

A small interface is enough:

```csharp
public interface IDisableAccountExecutor
{
    Task DisableAsync(
        string accountId,
        CancellationToken cancellationToken);
}
```

The application service can evaluate first and execute second:

```csharp
public sealed class DisableAccountService(
    IDisableAccountPolicy policy,
    IDisableAccountExecutor executor)
{
    public async Task<DisableAccountDecision> DisableAsync(
        DisableAccountContext context,
        CancellationToken cancellationToken)
    {
        DisableAccountDecision decision = policy.Evaluate(context);

        if (!decision.CanExecute)
        {
            return decision;
        }

        cancellationToken.ThrowIfCancellationRequested();

        await executor.DisableAsync(
            context.AccountId,
            cancellationToken);

        return decision;
    }
}
```

The correct shape is compact:

```csharp
DisableAccountDecision decision = policy.Evaluate(context);

if (!decision.CanExecute)
{
    return decision;
}

cancellationToken.ThrowIfCancellationRequested();
await executor.DisableAsync(context.AccountId, cancellationToken);
```

The executor sits on the other side of the decision guard. The cancellation check is also placed before the protected call so a cancellation already observed by the host does not cross the execution boundary.

```text
Authoritative context
        ↓
     Decision
     /      \
Blocked   Allowed
   ↓         ↓
Return    Protected execution boundary
             ↓
      Consequential side effect
```

That placement gives the test something concrete to prove: blocked outcomes stop before the boundary, while the allowed path crosses it deliberately.

## Use a Recording Executor Instead of Guessing

A mock library can verify calls, but one is not required.

A tiny recording fake keeps the test obvious:

```csharp
public sealed class RecordingDisableAccountExecutor
    : IDisableAccountExecutor
{
    public List<string> AccountIds { get; } = [];

    public Task DisableAsync(
        string accountId,
        CancellationToken cancellationToken)
    {
        AccountIds.Add(accountId);
        return Task.CompletedTask;
    }
}
```

This test double is intentionally simple. It records whether the boundary was crossed and which resource would have been affected. The list is deliberately single-threaded for the basic examples; use `Interlocked`, `ConcurrentQueue<T>`, or another thread-safe recorder when the test itself exercises concurrency.

If your project already uses a mocking library, the equivalent assertion is to verify that the protected method was never called. The important choice is the boundary being verified, not the test-double library. For example, a project that already uses Moq can express the same contract as:

```csharp
mockExecutor.Verify(
    executor => executor.DisableAsync(
        It.IsAny<string>(),
        It.IsAny<CancellationToken>()),
    Times.Never);
```

No mocking package is required for the article; this is only the library-specific equivalent of inspecting the recording fake.

For tests that focus only on host behavior for a particular outcome, a fixed policy double is equally small:

```csharp
public sealed class FixedDecisionPolicy(DisableAccountDecision decision)
    : IDisableAccountPolicy
{
    public DisableAccountDecision Evaluate(DisableAccountContext context) =>
        decision;
}
```

The denied-path test can now assert both the decision and the absence of execution:

```csharp
[Fact]
public async Task Denied_operation_never_reaches_executor()
{
    var executor = new RecordingDisableAccountExecutor();
    var policy = new DisableAccountPolicy();
    var service = new DisableAccountService(policy, executor);

    var context = new DisableAccountContext(
        AccountId: "account-123",
        RequesterIsAdministrator: true,
        IsProtectedAccount: true,
        MaintenanceHoldActive: false,
        RequiresAcknowledgment: false,
        AcknowledgmentSatisfied: false,
        RequiresEscalation: false);

    DisableAccountDecision result = await service.DisableAsync(
        context,
        CancellationToken.None);

    Assert.Equal(DecisionOutcome.Denied, result.Outcome);
    Assert.Empty(executor.AccountIds);
}
```

The second assertion is not redundant.

It proves that the returned decision controlled reachability of the protected operation.

## Decision Tests and Execution-Boundary Tests Answer Different Questions

A policy unit test may be perfectly good at proving the policy itself:

```csharp
[Fact]
public void Protected_account_is_denied()
{
    var policy = new DisableAccountPolicy();

    DisableAccountDecision result = policy.Evaluate(
        DisableAccountContext.ProtectedAccount());

    Assert.Equal(DecisionOutcome.Denied, result.Outcome);
}
```

That test asks:

> Did this evaluator produce the expected result for these facts?

An application-boundary test asks:

> Did the host honor that result before protected execution?

Both are useful.

Do not force every policy test to know about an executor. Instead, put the zero-execution assertion at the layer that coordinates decision and execution.

A practical test stack often looks like this:

```text
Policy unit tests
   ↓
Decision correctness

Application / workflow tests
   ↓
Decision controls executor reachability

Selected integration tests
   ↓
Real routing, persistence, transaction, or transport behavior
```

The important point is that at least one test protects the actual boundary rather than assuming a correct decision will be honored automatically.

## Test Every Outcome That Must Block Immediate Execution

`Denied` is not the only result that may need to stop the current execution path.

A richer workflow may use several lifecycle outcomes. For the current execution attempt, the invariant can be expressed as a small decision table:

| Outcome | Immediate protected execution |
| --- | --- |
| `Denied` | 0 calls |
| `Deferred` | 0 calls |
| `AcknowledgmentRequired` without satisfied continuation | 0 calls |
| `EscalationRecommended` | 0 calls |
| `Allowed` | exactly 1 attempt |

The corresponding tests do not need to be clever:

```csharp
[Theory]
[InlineData(DecisionOutcome.Denied)]
[InlineData(DecisionOutcome.Deferred)]
[InlineData(DecisionOutcome.AcknowledgmentRequired)]
[InlineData(DecisionOutcome.EscalationRecommended)]
public async Task Blocking_outcomes_do_not_execute(
    DecisionOutcome outcome)
{
    var executor = new RecordingDisableAccountExecutor();
    var service = new DisableAccountService(
        new FixedDecisionPolicy(
            new DisableAccountDecision(outcome, "test.outcome")),
        executor);

    DisableAccountDecision result = await service.DisableAsync(
        DisableAccountContext.Standard(),
        CancellationToken.None);

    Assert.Equal(outcome, result.Outcome);
    Assert.Empty(executor.AccountIds);
}
```

The allowed path deserves its own positive assertion:

```csharp
[Fact]
public async Task Allowed_operation_executes_exactly_once()
{
    var executor = new RecordingDisableAccountExecutor();
    var service = new DisableAccountService(
        new FixedDecisionPolicy(DisableAccountDecision.Allow()),
        executor);

    DisableAccountDecision result = await service.DisableAsync(
        DisableAccountContext.Standard(),
        CancellationToken.None);

    Assert.Equal(DecisionOutcome.Allowed, result.Outcome);
    Assert.Single(executor.AccountIds);
    Assert.Equal("account-123", executor.AccountIds[0]);
}
```

Why exactly one rather than merely greater than zero?

Because duplicate execution can be a different failure mode. A test that accepts two calls would prove that execution happened, not that the host crossed the protected boundary once as intended. Exactly one attempt is still not an idempotency guarantee: retryable allowed operations may also need an operation identifier, deduplication, or another replay strategy appropriate to the side effect.

An acknowledgment workflow can later resume through a separate validated continuation path. The zero-call assertion above applies to the attempt where acknowledgment is still required; it does not mean an acknowledged operation can never execute later.

### Reuse the invariant as a conformance test

If many handlers share the same lifecycle contract, do not rely on every pull request author remembering four separate tests. A shared test helper or contract fixture can accept the handler, its blocked outcomes, and an observable executor, then assert the same rule for each implementation:

```text
Every non-Allowed outcome
        ↓
Protected execution observations = empty
```

Keep handler-specific policy tests beside that shared contract. The shared fixture protects the cross-cutting execution invariant; it should not erase the domain-specific reasons a handler returns each outcome.

## When an Ordinary Authorization Test Is Enough

Not every endpoint needs a separate decision model or recording executor.

Suppose the complete requirement is:

> Only administrators may view this diagnostics page.

There is no additional workflow state, no protected mutation, no delayed execution, and no richer lifecycle outcome.

An ASP.NET Core integration test that proves a non-administrator receives `403 Forbidden` and that the protected handler is not reached may be enough.

Likewise, if authorization is the last meaningful gate before a simple same-host operation, framework authorization plus a focused integration test can provide the required evidence without introducing a separate governance abstraction.

Add a distinct execution-boundary test when the application actually has a distinct execution boundary worth protecting, for example:

- the operation may be denied after normal access control succeeds;
- there are multiple non-allowed lifecycle outcomes;
- protected work crosses a repository, service, queue, or process boundary;
- the operation can be delayed or resumed;
- a decision must be preserved independently from execution;
- a past defect showed that blocked results did not reliably stop side effects.

The extra seam should earn its complexity.

The core pattern is complete at this point: make the consequential boundary observable, assert the decision, and assert that blocked paths never cross it. The remaining sections are reference checks for operations with continuations, multiple side effects, asynchronous handoffs, concurrency, stale state, transactions, or compensation. Skim them now and return when one of those failure modes applies.

## Treat Resume After Acknowledgment as a New Execution Attempt

A stored `AcknowledgmentRequired` decision is evidence about the earlier attempt. It should not silently become authority for a later one. When a continuation resumes after acknowledgment, rebuild or refresh the authoritative facts that can still change, validate the acknowledgment binding, and re-evaluate before protected execution.

Conceptually, a useful continuation test changes the resource between the two attempts. The following is a sketch: `workflow`, `accountStore`, and `acknowledgment` represent application-specific continuation fixtures and are not defined by the minimal sample above.

```csharp
DisableAccountDecision first = await workflow.StartAsync(
    accountId: "account-123",
    CancellationToken.None);

Assert.Equal(
    DecisionOutcome.AcknowledgmentRequired,
    first.Outcome);
Assert.Empty(executor.AccountIds);

// The world changes while the workflow is paused.
accountStore.MarkProtected("account-123");

DisableAccountDecision resumed = await workflow.ResumeAsync(
    acknowledgment,
    CancellationToken.None);

Assert.Equal(DecisionOutcome.Denied, resumed.Outcome);
Assert.Empty(executor.AccountIds);
```

The continuation API may look different in a real system. The property to preserve is that a stale stored decision or acknowledgment does not bypass current evaluation. Pair the negative case with a happy-path continuation that satisfies the acknowledgment, rebuilds current facts, re-evaluates to `Allowed`, executes once, and ends with `Assert.Single(executor.AccountIds)`. Together, the tests show that acknowledgment enables a fresh attempt rather than bypassing evaluation.

## The Executor Must Actually Contain the Protected Side Effects

A zero executor count proves only what the executor boundary represents. Suppose the service calls a repository directly before the executor:

```csharp
await accounts.SetDisabledFlagAsync(
    context.AccountId,
    cancellationToken);

DisableAccountDecision decision = policy.Evaluate(context);

if (!decision.CanExecute)
{
    return decision;
}

await executor.DisableAsync(
    context.AccountId,
    cancellationToken);
```

A test could report `Decision = Denied` and `Executor calls = 0` while the account row was already modified. The test is not wrong; the boundary definition is incomplete. Before trusting a zero-executor assertion, identify what counts as protected work and make sure those side effects cannot bypass the boundary. A useful code-review question is:

> **Which dependencies in this flow can change the world?**

Those dependencies deserve explicit ownership and tests.

### Make the boundary harder to bypass

Invocation tests catch a bypass only when the test observes the dependency that was bypassed. Two additional techniques can reduce the chance that a future refactor introduces a direct write around the executor:

- **Narrow execution types.** An executor can accept a host-created allowed command or grant type instead of raw identifiers. Restricting construction to the decision/execution coordination layer makes accidental direct execution harder to express. This is a compile-time design aid, not a substitute for current authorization, policy freshness, or runtime validation.
- **Architecture tests.** Static architecture tests can assert dependency rules such as “only the execution layer may reference `IAccountWriter` or `IExternalAccountGateway`.” Libraries such as ArchUnitNET or NetArchTest can help automate that rule, but a simple project/assembly boundary may be enough.

These techniques complement zero-invocation tests: one protects runtime behavior for known scenarios, while the other constrains where future code is allowed to create side effects.

## Protected Side Effects Are Broader Than One Executor Method

Different applications have different execution boundaries. The same testing idea can be applied to several common side effects.

| Protected effect | Useful observable test boundary |
| --- | --- |
| Repository mutation | Recording repository writer; unchanged persisted state; write-command count |
| External API call | Fake gateway/client; request count; test HTTP handler |
| Event publication | Recording publisher; published-message count |
| Queue or job scheduling | Recording scheduler; enqueued-work count |
| Notification send | Fake sender; send count |
| File or object-store mutation | Recording storage abstraction; write/delete count |
| Protected executor | Invocation count or captured command list |

The goal is not to create one interface per line of code. Place observability around the consequential boundaries that must remain unreachable when the operation is blocked.

### One operation can have several protected boundaries

A workflow may mutate a repository, call an external system, and publish an event as part of one logical operation. In that case, observing only one boundary is incomplete unless one executor truly owns all three effects. Think of execution as plural:

```text
Decision
   ↓
Execution coordinator
   ├── Repository writer
   ├── External gateway
   └── Event publisher
```

A blocked-path test should observe every consequential boundary the host can reach:

```csharp
Assert.Equal(DecisionOutcome.Denied, result.Outcome);
Assert.Empty(accountWriter.AccountIds);
Assert.Empty(externalGateway.Requests);
Assert.Empty(eventPublisher.Messages);
```

An alternative is to make one composite executor own those effects and test that the service cannot reach any of them except through that executor. Whichever design you choose, define “execution” broadly enough to include every side effect the decision is supposed to protect. Keep each recorder independent so one boundary cannot reset, mask, or reuse another boundary's observation; use thread-safe per-boundary recorders when the test runs concurrently.

### Repository writes

If the operation is primarily a database mutation, a recording repository can expose attempted writes:

```csharp
public sealed class RecordingAccountWriter : IAccountWriter
{
    public List<string> AccountIds { get; } = [];

    public Task DisableAsync(
        string accountId,
        CancellationToken cancellationToken)
    {
        AccountIds.Add(accountId);
        return Task.CompletedTask;
    }
}
```

Then a blocked-path assertion can be:

```csharp
Assert.Empty(accountWriter.AccountIds);
```

An integration test may go further and verify that the real database state remained unchanged. That is valuable, but it answers a slightly different question: final durable state rather than whether a write attempt crossed the application boundary.

### External-service calls

For an external API, put the call behind an application-owned gateway or use a controlled test HTTP handler.

The blocked test should prove that no outbound request was attempted:

```csharp
Assert.Empty(externalGateway.Requests);
```

This is stronger than checking that the remote test system eventually has the expected state. The latter may miss a request that was sent and later failed or was compensated.

### Event publication

Events are especially important because publication can outlive the request that produced them.

A recording publisher can make absence explicit:

```csharp
Assert.Empty(eventPublisher.Messages);
```

If the system uses a transactional outbox, the relevant protected boundary may be the durable outbox write rather than the broker send. Test the boundary the application actually owns.

## Hidden Side Effects Can Defeat an Apparently Clean Test

Method names are not trust boundaries. A dependency called:

```csharp
policyValidator.ValidateAsync(...)
```

may still:

- update a last-checked timestamp;
- create a review record;
- enqueue background work;
- call an external service that mutates state;
- publish an event as part of validation.

If those actions are consequential, they belong in the side-effect analysis even if the method is named `Check`, `Validate`, `Authorize`, `Evaluate`, or `Get`. These are common refactor regressions: a repository call moves above the decision, a validator updates a timestamp, a helper enqueues work, or an observability hook triggers downstream behavior. Audit context loaders, validators, and helpers as aggressively as the obvious executor. Context gathering should be observational with respect to the operation being decided.

## Async, Fire-and-Forget, and Cancellation Hazards

The invariant applies to asynchronous boundaries too, but asynchronous code creates ways to cross the boundary before the main method appears to do so.

- **Fire-and-forget tasks** started before the decision can outlive the request and mutate state after the blocked result is returned. If the work is consequential, starting or scheduling it is already a protected side effect.
- **Background dispatch** through `Task.Run`, a queue, scheduler, channel, or hosted-service handoff should be observed as execution even if the eventual worker has not run yet. A blocked decision should not enqueue the work.
- **`async void`** makes completion and exceptions difficult to observe outside event-handler scenarios. Avoid it for protected application operations because the test cannot reliably await the boundary.
- **Cancellation** is not a governance outcome, but a cancellation already observed before execution should not cross the protected boundary. If the host performs asynchronous work between decision and execution, check cancellation immediately before invoking the executor.

A focused cancellation test can use a token that is already canceled at the final boundary and assert both the cancellation and non-execution. The `RecordingDisableAccountExecutor` above deliberately ignores the token and records immediately if called, so any `OperationCanceledException` in this test must come from the host's pre-execution guard. If a test double throws on cancellation before recording the call, the test can pass for the wrong reason.

```csharp
var executor = new RecordingDisableAccountExecutor();
var service = new DisableAccountService(
    new FixedDecisionPolicy(DisableAccountDecision.Allow()),
    executor);

using var cancellation = new CancellationTokenSource();
cancellation.Cancel();

await Assert.ThrowsAnyAsync<OperationCanceledException>(
    () => service.DisableAsync(
        DisableAccountContext.Standard(),
        cancellation.Token));

Assert.Empty(executor.AccountIds);
```

Be precise about timing: once the executor has been invoked, a later cancellation may stop completion but cannot truthfully be described as “zero executor calls.” If policy evaluation itself performs I/O, make that evaluation awaitable and propagate the cancellation token through it; cancellation should remain distinguishable from a policy outcome and still must not schedule protected work before evaluation completes.

## Concurrency and Retries Do Not Weaken the Blocked-Path Invariant

For concurrent blocked requests, each attempt should still contribute zero protected executions. If a test runs blocked attempts in parallel, use a thread-safe recorder so the observation itself is trustworthy.

Retries deserve a separate distinction. Retrying a **blocked** decision must not manufacture an allowed path: each blocked attempt still produces zero protected executions. Retrying an **allowed** operation raises a different contract—idempotency, replay, and duplicate-execution handling—that a zero-call blocked-path test does not solve.

## A Blocked-Path Test Does Not Solve Time-of-Check / Time-of-Use

Moving a decision before execution creates a clear gate, but it also makes the decision a point-in-time judgment. An `Allowed` result can become stale if resource state or policy changes before the side effect. For example, an account might become protected after evaluation and before `DisableAsync`.

Treat that as a separate execution-time invariant. Common defenses include a row version, ETag, expected-state token, policy-version stamp, or a final re-query/re-evaluation immediately before the irreversible operation. Suppose a version-aware variant of the context also carries `ResourceVersion`; conceptually:

```csharp
DisableAccountDecision decision = policy.Evaluate(evaluatedContext);

if (!decision.CanExecute)
{
    return decision;
}

AccountSnapshot latest = await accounts.LoadAsync(
    evaluatedContext.AccountId,
    cancellationToken);

if (latest.Version != evaluatedContext.ResourceVersion)
{
    return DisableAccountDecision.Defer(
        "account.disable.resource-changed");
}

await versionedExecutor.DisableAsync(
    evaluatedContext.AccountId,
    expectedVersion: evaluatedContext.ResourceVersion,
    cancellationToken);
```

A test can arrange version `17` during evaluation, expose version `18` before execution, and assert that the stale attempt produces no protected mutation. The executor or repository should still enforce the expected version as close to the write as practical; a pre-execution re-read alone cannot make independent systems atomic.

The article's main invariant remains one-way:

> **If the current decision blocks execution, protected work must be zero.**

An allowed decision needs its own freshness and concurrency contract.

## Transactions Help, but Rollback Is Not the Same as Non-Execution

A database transaction may protect durable state even when the sequence is `begin transaction → write row → discover a late denial → rollback`. After rollback, the database may look unchanged, but that does not prove that no protected work was attempted.

For database-only operations inside one transaction, rollback may be an acceptable safety mechanism. But several limits matter:

- locks may have been acquired;
- triggers or database-side behavior may have run;
- sequences or other database mechanisms may not behave like ordinary rolled-back row state;
- external API calls do not automatically roll back with the database;
- messages sent directly to a broker are not undone by a relational rollback;
- notifications already delivered cannot be made unsent.

If the requirement is specifically:

> No consequential work should begin unless the decision allows it,

then test the pre-execution boundary rather than relying only on final state after rollback.

For a transactional outbox design, be precise about what execution means. Writing the business change and its outbox record in one allowed transaction can be the protected operation. A denied path should create neither. Later broker delivery is a separate stage with its own guarantees.

## Compensation Is Recovery, Not Prevention

A useful comparison is prevention versus recovery:

```text
Prevention                         Compensation
----------                         ------------
Blocked decision                   Operation executes
      ↓                                  ↓
0 protected attempts               Late block discovered
                                         ↓
                                   Compensation attempts repair
                                         ↓
                                   State may look restored
```

Compensation can be necessary in distributed systems. It is not equivalent to preventing the operation.

A compensating action can:

- fail;
- race with another observer;
- leave externally visible history;
- produce duplicate messages or notifications;
- be semantically incomplete;
- require new authorization or operational work of its own.

The compensated path may be the best recovery available after failure, but it is weaker evidence for a rule that says the denied operation must never execute. Prevention and compensation solve different problems.

## Do Not Turn Every Test into a Mock-Interaction Test

Zero-execution assertions are valuable when the architectural contract is about reachability of a consequential boundary.

They are less useful when applied indiscriminately to internal implementation details.

A brittle test might verify every helper call, repository read, mapper invocation, and logging statement. That couples the test to one code shape without strengthening the safety property.

Prefer assertions at meaningful boundaries: decision outcome, protected writes, outbound requests, published messages, enqueued work, or executor calls. Avoid using interaction tests merely to freeze harmless internal refactoring.

The question is:

> **Which observation proves the behavior that must not change?**

## A Practical Review Checklist

For a consequential operation, review the test in this order:

1. **What is the protected side effect?** Name every database mutation, external call, event, queue operation, notification, or executor action that must not occur.
2. **Where is the last decision that can block it?** Make sure that decision happens before the side effect becomes reachable.
3. **Can the test observe every consequential boundary?** Use a recording fake, mock verification, controlled HTTP handler, persisted-state check, or another deterministic observation.
4. **Does the blocked test assert both facts?** Check the expected decision and zero protected execution.
5. **Are pre-decision dependencies observational?** Inspect validators, repositories, context loaders, logging hooks, helper methods, background dispatch, and lazy writes for hidden side effects.
6. **Is the boundary difficult to bypass?** Consider narrow execution types, project/assembly dependency rules, or architecture tests when direct writers or gateways must remain isolated.
7. **Do all non-immediate outcomes stop execution?** Cover denial, deferral, unsatisfied acknowledgment, and escalation where those outcomes exist; reuse a shared conformance fixture when many handlers implement the same contract.
8. **Does a continuation revalidate current facts?** Acknowledgment, approval, or stored decision evidence should not bypass re-evaluation on resume.
9. **Does the allowed path execute exactly once and against fresh enough state?** Pair the positive invariant with version/concurrency checks and an idempotency/replay strategy where retries are possible.
10. **Can asynchronous work escape before the decision?** Treat task scheduling, queueing, channel writes, and background dispatch as execution boundaries when they can lead to consequential work.
11. **Can cancellation and retry tests distinguish invocation from completion?** A canceled or retried executor may still have been called; record the attempt before relying on the resulting exception or final state.
12. **Are rollback and compensation being confused with non-execution?** A rolled-back write or compensated effect can restore state without proving that protected work never began.

The checklist is intentionally implementation-neutral. The exact test double or framework matters less than the boundary it makes observable. For high-value boundaries, mutation testing can add another check: remove or invert the `if (!decision.CanExecute) return decision;` guard and confirm the test fails; tools such as Stryker.NET can automate that experiment.

## The Test Should Protect the Architecture, Not Just the Enum

The key difference is small in code:

```csharp
Assert.Equal(DecisionOutcome.Denied, result.Outcome);
```

versus:

```csharp
Assert.Equal(DecisionOutcome.Denied, result.Outcome);
Assert.Empty(executor.AccountIds);
```

But the second test protects a larger claim: the system decided “no,” and the protected operation remained unreachable. That is stronger evidence than a return value by itself.

For consequential operations, design the code so the protected boundary is visible, then make the test prove that blocked decisions cannot cross it.

## Continue Deeper

- [Decision Before Execution](../../tutorials/decision-before-execution.md) explains the broader proposal → context → decision → execution pattern and why a blocked decision should never reach the executor.
- [Decision Before Execution runnable sample](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-before-execution/README.md) provides executable invariant tests using small framework-neutral .NET types.
- [Decision Before Execution lab](../../labs/decision-before-execution.md) asks you to break, observe, and repair the host execution boundary yourself.
- [Your Authorization Check Runs Too Late](authorization-check-runs-too-late.md) starts from the related production failure mode where a blocking rule is discovered only after protected work begins.

---

> **Read it. Run it. Question it. Improve it.**
