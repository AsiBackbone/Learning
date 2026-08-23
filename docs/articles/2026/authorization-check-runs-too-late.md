---
description: A practical .NET architecture article on separating access control from the final operation-level decision so blocked workflows cannot partially execute.
title: Your Authorization Check Runs Too Late
author: Christopher D. Cavell
published: "2026-08-23"
summary: Authorization can succeed while resource-state or workflow rules still block execution; resolve that decision before protected side effects begin.
feed: true
---

# Your Authorization Check Runs Too Late

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** None

An administrator sends a request to disable an account.

The endpoint checks authorization. The caller is an administrator, so the check succeeds.

Assume this is a workflow where the protected-account classification comes from an operations-policy service that is consulted only by a downstream step. The endpoint has not merely forgotten an `if` statement; a fact capable of blocking the operation is owned and discovered too far downstream.

The application then starts the account-disable workflow:

```text
HTTP request
      ↓
Authorization succeeds
      ↓
Load account
      ↓
Modify account state
      ↓
Send notification
      ↓
Publish event
      ↓
Protected-account rule discovered
      ↓
Operation should require escalation
```

The policy conclusion may be correct, but the timing is not.

At the point where the application discovers that a protected account requires escalation, the useful question is no longer:

> Should this operation proceed?

It is:

> **What has already happened?**

Depending on the implementation:

- state may already have changed;
- an external notification may already have been sent;
- an event may already have been published;
- audit evidence may now describe a failed or partially completed operation rather than a clean decision;
- compensating work may be required to repair what the application already did.

The problem is not that ASP.NET Core authorization is defective. **Authorization and execution-lifecycle governance answer different questions**, and the application waited too long to answer the second one.

## Authorization Can Be Correct and Still Be Incomplete

A normal authorization question is often:

```text
May this authenticated user access this endpoint
or perform this operation on this resource?
```

ASP.NET Core policies, handlers, roles, claims, and resource-based authorization are designed to answer that kind of question. For many applications, they are exactly the right mechanism.

But some operations have another question after access control succeeds:

```text
Given the actor, the exact operation, the current resource state,
and the active operational constraints, what should happen next?
```

That answer may be richer than success or failure:

```text
Allowed
Denied
Deferred
AcknowledgmentRequired
EscalationRecommended
```

An administrator may be authorized to request an account-disable operation while a protected account still requires a different path.

That distinction matters because:

```text
Authorized to request
        ≠
Approved for immediate execution
```

If the application does not model that distinction explicitly, the broader decision can become scattered across controllers, services, repositories, notification code, and downstream integrations. A rule discovered in any of those places may arrive after protected work has begun.

## Put the Full Decision Before Protected Work

The architectural change is small enough to express without a framework. For a one-off operation, it may literally be one well-placed `if` before a service call. Named proposal, context, and decision types earn their keep when the boundary must survive multiple call sites, accumulate rules, produce durable evidence, or be tested independently. They create a greppable seam where the decision happens.

The walkthrough below shows the fuller shape so the boundaries are visible in one place. Most operations need only the subset justified by their actual risks and lifecycle requirements.

The flow is:

```text
Proposal
   ↓
Authoritative context
   ↓
Decision
   ↓
Execution
```

The important property is not the names of the classes.

The important property is that **protected side effects begin only after the application has enough authoritative information to make the decision it actually needs**.

### 1. Represent the proposal

Start by representing the requested operation as data:

```csharp
public sealed record DisableAccountProposal(
    string AccountId,
    string RequestedReason);
```

Creating this object does not disable anything. `RequestedReason` is caller-supplied narrative: the evaluator below deliberately does not treat it as authoritative policy input, but the decision recorder can preserve it as part of the request evidence.

That gives the application something it can evaluate before execution while demonstrating an important distinction: request data can be retained for provenance without being allowed to decide the outcome.

### 2. Build authoritative context

Next, load the facts that actually determine the operation's current status:

```csharp
public sealed record DisableAccountContext(
    DisableAccountProposal Proposal,
    string ActorId,
    bool IsProtectedAccount,
    bool MaintenanceHoldActive,
    string ResourceVersion,
    string OperationsPolicyVersion,
    string? SupersedingPolicyVersion);
```

The context should come from sources the host trusts for those facts. The proposal may carry requested values, but security- or policy-sensitive state should not become authoritative merely because the caller supplied it. The same rule applies to identity: `ActorId` should be derived from the authenticated principal or another host-trusted identity source, not copied from request JSON. ASP.NET Core authorization has already established that the actor may enter this operation, so the operation-level evaluator intentionally does not repeat the administrator-role check.

If an already-authorized request reaches this code without the required stable actor identifier, the sample treats that as a host configuration or identity-mapping failure rather than inventing a governance decision. Production telemetry should still capture that failure path even though it is outside the decision record shown here.

For this example, the important fact is simple:

```text
account-123
protected = true
```

Loading that state is decision preparation only if the read is observational. A `GetAsync` that lazily provisions data, updates a last-accessed timestamp, or triggers an external call is already performing side effects and should not be smuggled into context assembly.

### 3. Make the decision explicit

A small application-specific result is enough:

```csharp
public enum DisableAccountOutcome
{
    Allowed,
    Denied,
    Deferred,
    AcknowledgmentRequired,
    EscalationRecommended
}

public sealed record DisableAccountDecision(
    DisableAccountOutcome Outcome,
    string Reason)
{
    public bool CanExecute =>
        Outcome == DisableAccountOutcome.Allowed;
}
```

Inside the application service, a small private evaluator can now resolve the meaningful lifecycle state before the executor is called:

```csharp
private static DisableAccountDecision Evaluate(
    DisableAccountContext context)
{
    if (context.SupersedingPolicyVersion is not null)
    {
        return new(
            DisableAccountOutcome.Deferred,
            "Operations policy changed; re-evaluate before execution.");
    }

    if (context.IsProtectedAccount)
    {
        return new(
            DisableAccountOutcome.EscalationRecommended,
            "Protected accounts require escalation.");
    }

    if (context.MaintenanceHoldActive)
    {
        return new(
            DisableAccountOutcome.Deferred,
            "Account changes are temporarily deferred.");
    }

    return new(
        DisableAccountOutcome.Allowed,
        "The operation may proceed.");
}
```

Nothing in this evaluator disables an account, sends a notification, or publishes an event.

It produces a decision.

### 4. Cross one explicit execution boundary

Keep the two host-owned seams explicit: one records the decision evidence and one performs the protected side effect.

```csharp
public interface IDisableAccountDecisionRecorder
{
    Task RecordAsync(
        DisableAccountContext context,
        DisableAccountDecision decision,
        CancellationToken cancellationToken);
}

public interface IDisableAccountExecutor
{
    Task DisableAsync(
        DisableAccountProposal proposal,
        string expectedResourceVersion,
        CancellationToken cancellationToken);
}
```

The application service can then make the boundary visible:

```csharp
public async Task<DisableAccountDecision> HandleAsync(
    DisableAccountProposal proposal,
    ClaimsPrincipal actor,
    CancellationToken cancellationToken)
{
    Account account = await accounts.GetAsync(
        proposal.AccountId,
        cancellationToken);

    var policySnapshot =
        await operations.GetAccountPolicyAsync(
            proposal.AccountId,
            cancellationToken);

    string actorId =
        actor.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new InvalidOperationException(
            "Authenticated actor identifier is required.");

    var context = new DisableAccountContext(
        Proposal: proposal,
        ActorId: actorId,
        IsProtectedAccount: policySnapshot.IsProtectedAccount,
        MaintenanceHoldActive: policySnapshot.AccountChangesOnHold,
        ResourceVersion: account.Version,
        OperationsPolicyVersion: policySnapshot.Version,
        SupersedingPolicyVersion: null);

    DisableAccountDecision decision = Evaluate(context);

    if (decision.CanExecute)
    {
        var latestPolicySnapshot =
            await operations.GetAccountPolicyAsync(
                proposal.AccountId,
                cancellationToken);

        if (!string.Equals(
                latestPolicySnapshot.Version,
                context.OperationsPolicyVersion,
                StringComparison.Ordinal))
        {
            context = context with
            {
                SupersedingPolicyVersion = latestPolicySnapshot.Version
            };
            decision = Evaluate(context);
        }
    }

    await decisionRecorder.RecordAsync(
        context,
        decision,
        cancellationToken);

    if (!decision.CanExecute)
    {
        return decision;
    }

    await executor.DisableAsync(
        proposal,
        expectedResourceVersion: context.ResourceVersion,
        cancellationToken: cancellationToken);

    return decision;
}
```

The line that matters is not complicated:

```csharp
if (!decision.CanExecute)
{
    return decision;
}
```

That line gives the system a testable boundary:

> **A blocked decision never reaches the executor.**

The executor owns the protected side effects. The decision code does not. The `decisionRecorder` call is intentionally before the branch so systems that require durable decision evidence can preserve what was decided before any allowed execution begins; a logger, database, or append-only audit store can implement that dependency according to the application's needs.

That choice also makes the recorder an availability dependency: this sample effectively fails closed if recording throws. A production system should decide explicitly whether required decision evidence must succeed synchronously, can use a durable outbox or buffer, or may follow another failure policy appropriate to the operation.

### 5. Treat the decision as a point-in-time judgment

Moving the decision earlier does not freeze the facts used to make it. In this example, `ResourceVersion` protects the account row, but the decisive protected-account classification and maintenance hold come from a separate operations-policy service. The account version therefore cannot prove that those externally owned policy facts are still current.

For resource state, a common approach is to carry a version, ETag, row version, or other concurrency token into the execution boundary and require the executor's write to succeed only against that expected version. For externally owned policy facts, capture whatever freshness signal that source can provide: a policy-version stamp, lease, bounded cache lifetime, change token, or an execution-time re-query. The sample re-fetches the operations-policy snapshot before recording the final decision. If the version changed, the host preserves the version originally evaluated in `OperationsPolicyVersion`, captures the newer version in `SupersedingPolicyVersion`, and sends that context back through the same evaluator. The evaluator returns `Deferred`; the host does not manufacture a separate lifecycle outcome.

The sample deliberately defers instead of overwriting the original policy facts with the newer snapshot and immediately deciding again. That keeps the recorded facts tied to the facts actually evaluated, records which newer policy version caused the staleness detection, and avoids turning one request into an unbounded re-evaluation loop. A later attempt can assemble a fresh context from the newer policy snapshot. How that attempt is initiated is host-owned: use bounded retry with backoff when automatic retry is safe, or require explicit re-submission when it is not. Each attempt should assemble fresh context, and the host should cap retries so repeated policy churn cannot become an unbounded loop.

That placement is a deliberate trade-off. Recording after the re-fetch keeps the durable evidence aligned with the final decision the host intends to honor, but it leaves a residual window between that freshness check and the irreversible side effect. The executor's `expectedResourceVersion` check is still the last line of defense for account-row concurrency; an operation that also requires stronger policy freshness should repeat or enforce the policy-version check at the execution gateway immediately before the side effect.

No single token automatically makes independent systems atomic. If a database row and an external policy service can change independently, the application has to choose an appropriate consistency strategy and place the final revalidation as close to the irreversible side effect as practical.

The important distinction is:

```text
Decision-before-execution
    +
Resource concurrency validation
    +
Policy-fact freshness validation
    ↓
No knowingly stale decision silently crosses the protected boundary
```

## One Decision Trace Makes the Boundary Concrete

For the protected-account case, separate what the decision recorder can preserve from what the test or runtime observation proves afterward.

**Recorded decision evidence:**

```text
Operation:
account.disable

Actor:
administrator-17

Resource:
account-123

Requested reason:
Security incident investigation

Policy snapshot:
operations-policy-v42

Current policy facts:
protected = true

Decision:
EscalationRecommended
```

**Observed execution invariant:**

```text
Executor invocation:
0
```

The recorder can know the proposal, including its non-authoritative request narrative, the authoritative actor identity, policy snapshot, current decision facts, and decision before execution begins. The executor count is a separate observation made by the test or host instrumentation after the branch.

That second fact is the architectural claim worth testing: the application did not merely return an escalation result; it also preserved the stronger invariant that the protected executor was never invoked.

## Test the Absence of Execution

A decision test that checks only the returned enum is useful, but it does not prove the execution boundary held.

Because the application service depends on `IDisableAccountExecutor`, the test can substitute a recording implementation without changing the decision code:

```csharp
public sealed class RecordingExecutor : IDisableAccountExecutor
{
    public int InvocationCount { get; private set; }

    public Task DisableAsync(
        DisableAccountProposal proposal,
        string expectedResourceVersion,
        CancellationToken cancellationToken)
    {
        InvocationCount++;
        return Task.CompletedTask;
    }
}
```

Then the blocked path can assert both facts. The full test still needs the normal arrangement and dependency substitution; this excerpt focuses on the two architectural assertions:

```csharp
Assert.Equal(
    DisableAccountOutcome.EscalationRecommended,
    decision.Outcome);

Assert.Equal(
    0,
    executor.InvocationCount);
```

The first assertion proves the evaluator reached the expected conclusion.

The second proves the application respected it.

Those are different properties.

## Why Logging Afterward Does Not Fix the Boundary

Suppose the application performs the action and then records why it should not have happened:

```csharp
await accountService.DisableAsync(accountId, cancellationToken);

logger.LogWarning(
    "Protected account required escalation.");
```

The log may be accurate.

It is still evidence about an action that already crossed the boundary.

Logging can help explain what happened. It does not transform a late decision into a pre-execution decision, and it does not undo notifications, events, or other side effects that already occurred.

A cleaner lifecycle is:

```text
Proposed operation
      ↓
Current context assembled
      ↓
Decision recorded
      ↓
Allowed?
  ├── no  → stop or route elsewhere
  └── yes → protected execution
```

That structure gives audit evidence a cleaner story to tell because the decision exists before the side effect.

## Choose the Smallest Boundary That Works

The point of an explicit operation-level decision is not to replace ASP.NET Core authorization. It is to keep access control and workflow lifecycle semantics from being forced into one result when the application genuinely needs both.

### Do not hide workflow semantics inside authorization

A protected account can be represented mechanically as an authorization failure, but that can erase distinctions the rest of the workflow needs.

| Outcome | What it means here | Typical next step |
| --- | --- | --- |
| `Denied` | The current path is not permitted | Reject the operation |
| `Deferred` | The operation is valid but should wait | Hold or schedule retry |
| `AcknowledgmentRequired` | The actor may proceed only after an explicit interruption step | Present and record acknowledgment |
| `EscalationRecommended` | The operation should move to another authority or workflow | Route for review or approval |

The sample `HandleAsync` returns every non-`Allowed` decision to keep the protected-execution boundary easy to see. In a real host, those outcomes can route differently without invoking the protected executor. Their shared invariant is not that they are identical; it is that none of them silently falls through into immediate execution.

### When authorization alone is enough

Do not build a governance pipeline for every endpoint. If the complete requirement is `Only administrators may disable accounts`, ordinary ASP.NET Core authorization may be the better design. If the complete requirement is `An administrator may disable this loaded account only when the resource satisfies these conditions`, resource-based authorization may also be enough.

ASP.NET Core authorization already supports policies, requirements, handlers, failure reasons, imperative authorization, and resource-aware checks. Use those capabilities when the problem is fundamentally access control and success/failure expresses the full decision. A broader decision model becomes useful only when the application genuinely needs additional lifecycle states, durable decision evidence, or a separate execution boundary.

The smallest architecture that preserves the required boundary is usually the better architecture.

### Use a hybrid when both questions exist

Authorization and a broader execution decision do not need to compete. A practical ASP.NET Core flow can be:

```text
Authentication
      ↓
ASP.NET Core authorization
      ↓
Build authoritative operation context
      ↓
Evaluate lifecycle decision
      ↓
Allowed?
  ├── no  → deny, defer, acknowledge, or escalate
  └── yes → host-owned executor
```

ASP.NET Core authorization answers, `May this actor enter this operation?` The application decision answers, `What should happen next with this exact proposal under current conditions?` The executor answers neither question; its job is to perform the side effect only after the host has crossed the decision boundary.

## This Is Established Architecture, Not a New Authorization Primitive

The separation has clear relatives in established software and security architecture:

- policy decision point and policy enforcement point separation;
- reference-monitor-style mediation;
- complete mediation;
- command validation and application-service boundaries;
- workflow state machines and explicit result types.

ASI Backbone Learning uses terms such as **Decision Before Execution**, **Governed Execution**, and **Host-Owned Execution** to keep the composed boundary visible while teaching it. Those labels are not claims that the underlying ideas originated in this repository.

The reusable lesson is independent of the vocabulary:

```text
Do not let a component that proposes or evaluates an operation
silently become the component that performs it.
```

## Common Ways the Boundary Fails

### The decisive rule is checked after a side effect

This is the failure the article began with. The rule may be correct, but it arrives too late to prevent partial action.

### The evaluator performs the side effect

If policy evaluation disables the account as part of returning `Allowed`, the decision no longer exists as a meaningful pre-execution boundary.

### The host ignores the decision

A beautiful decision model does not govern anything if the host invokes the executor regardless of the result.

### Decision facts are discovered opportunistically during execution

If important facts are scattered across downstream components, the application may not know the complete decision context until after execution has started. The opening example deliberately uses this failure mode: the protected-account classification is owned by a downstream policy service, so the architecture cannot make the complete decision at the endpoint until that fact is exposed earlier as authoritative context.

### Context assembly performs hidden side effects

A context-building step is not automatically safe merely because it is called a read. Lazy provisioning, last-access updates, implicit writes, cache fills with external consequences, or downstream calls can make context assembly part of execution. Keep decision preparation observational where practical, or explicitly govern any side effect it must perform.

### Resource state changes after the decision

A correct decision can become stale before execution. Resource state may need optimistic concurrency or a version/ETag check, while externally owned policy facts may need a policy-version check, bounded freshness window, or execution-time re-query. The policy boundary and these freshness/concurrency boundaries solve different problems and often belong together.

### Every CRUD operation gets a governance workflow

Extra lifecycle modeling has a cost. If ordinary authorization and a direct service call fully express the requirement, keep the simpler design.

## A Short Review Checklist

Before a consequential operation crosses into execution, ask:

1. What exact operation is being proposed?
2. Which resource will change?
3. Which facts must be current before the decision is valid?
4. Are those facts coming from authoritative sources?
5. Is success/failure enough, or are `Deferred`, `AcknowledgmentRequired`, or `EscalationRecommended` meaningful states?
6. Which component makes the decision?
7. Which component performs the protected side effect?
8. Can a test prove that a blocked decision never invokes that executor?

If those answers are clear, the execution boundary is probably clear too.

## Further Reading

- [Decision Before Execution](../../tutorials/decision-before-execution.md) — the full foundational tutorial and implementation walkthrough.
- [Decision Before Execution executable sample](https://github.com/AsiBackbone/Learning/tree/main/samples/decision-before-execution) — a small .NET sample with focused invariant tests.
- [When ASP.NET Core Authorization Is Enough](../../architecture/when-aspnet-core-authorization-is-enough.md) — the simpler framework-native alternative and the boundary where the problem becomes larger than authorization.
- [Terminology and Established Architecture Concepts](../../architecture/terminology-and-established-concepts.md) — mappings from Learning terminology to established authorization, policy, workflow, provenance, and mediation concepts.
- [AsiBackbone](https://github.com/AsiBackbone/AsiBackbone) — an optional working implementation reference. The article does not require adopting it.

## The Rule to Keep

Authorization should happen before protected access.

But for consequential workflows, that is not always the last decision the system needs.

The application should assemble the authoritative context, resolve the full operation-level decision, and only then cross into protected execution.

The invariant is intentionally simple:

> **A blocked decision never reaches the executor.**

If your application discovers the block after the executor has already started, the check was not early enough.
