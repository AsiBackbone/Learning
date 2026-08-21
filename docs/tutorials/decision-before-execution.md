---
description: Learn why consequential operations should become explicit proposed intent and governance decisions before the host performs real-world side effects.
---

# Decision Before Execution

**Learning objective:** Understand why a consequential operation should be represented as proposed intent, evaluated, and converted into an explicit decision before the host performs the real-world action.

**Difficulty:** Beginner  

**Prerequisites:** None. Familiarity with basic application request/response flows is helpful but not required.

## Pattern Card

> **Problem:** Consequential side effects can occur too directly after request handling or authorization, leaving policy reasoning and execution coupled.
>
> **Pattern:** Represent the proposed operation as intent, evaluate it, return an explicit decision, and let the host perform the side effect only when that decision permits execution.
>
> **Use when:** An operation needs more than yes/no authorization, such as policy context, deferral, escalation, acknowledgment, evidence, or an explicit execution boundary.
>
> **Prefer something simpler when:** An ordinary authorization check and direct service call fully express the application's real requirements and no richer decision lifecycle is needed.
>
> **Observe:** A blocked decision never reaches the executor.

This is the first foundational tutorial in ASI Backbone Learning.

The pattern is deliberately broader than the `AsiBackbone` package. You can use the same separation in a small application, an API gateway, an administrative workflow, a background process, or an AI-assisted tool system.

The core idea is:

> **A proposed action should become a governed decision before it becomes real-world execution.**

## The Problem

Many applications move directly from a request to a side effect:

```text
Request
   ↓
Authorization check
   ↓
Business service
   ↓
Execution
```

For ordinary operations, that may be enough.

For consequential operations, however, the application may need to answer more than:

> Is this caller authorized?

It may also need to ask:

- What operation is actually being proposed?
- Which resource will be affected?
- What context applies right now?
- Which policy or constraint produced the decision?
- Should the operation proceed immediately?
- Should it be denied, deferred, acknowledged, or escalated?
- Who retains responsibility for performing the real-world action?
- What evidence should remain after the decision?

When these questions are mixed directly into the execution path, the code can become difficult to explain, test, review, and audit.

## A Naive Implementation

Consider an administrative endpoint that disables a user account.

A direct implementation might look like this:

```csharp
[HttpPost("{userId}/disable")]
public async Task<IActionResult> DisableAccount(
    string userId,
    CancellationToken cancellationToken)
{
    if (!User.IsInRole("Administrator"))
    {
        return Forbid();
    }

    await accountService.DisableAsync(userId, cancellationToken);

    logger.LogInformation(
        "Account {UserId} disabled by {Actor}.",
        userId,
        User.Identity?.Name);

    return NoContent();
}
```

This code is short and understandable.

It is not automatically wrong.

The architectural question is whether the operation is consequential enough that authorization alone is no longer the full decision.

Suppose the organization later adds requirements such as:

- Protected accounts cannot be disabled through the normal workflow.
- Some operations require a stated reason.
- A maintenance hold may temporarily defer account changes.
- Certain accounts require acknowledgment or escalation.
- The decision must retain a correlation identifier and policy version.
- The host must be able to prove that denied operations never reached execution.

The endpoint can absorb those requirements with additional `if` statements, but eventually the request handler becomes both the **decision maker** and the **executor coordinator**.

That is the boundary this pattern separates.

## Authorization Is Not the Whole Decision

Authentication answers:

> Who is this caller?

Authorization often answers:

> Is this caller permitted to perform this category of operation?

A governance decision can ask a wider question:

> Given this actor, operation, resource, context, and active constraints, what should happen next?

Those concepts overlap, but they are not identical.

A caller can be authenticated and authorized while a specific operation is still:

- Denied because of resource state.
- Deferred because a dependency is unavailable.
- Held for acknowledgment.
- Escalated because the operation exceeds a risk threshold.

Authorization remains important. The pattern does not replace it.

Instead, authorization becomes one input into a larger decision when the operation requires that additional reasoning.

## Separate the Proposal from the Side Effect

The first architectural change is small:

**Represent the proposed operation before performing it.**

```csharp
public sealed record DisableAccountIntent(
    string AccountId,
    string RequestedBy,
    string Reason);
```

The intent is data.

Creating an intent does not disable an account.

That distinction matters because the application can now inspect, enrich, reject, defer, record, or route the proposal without causing the side effect.

The flow becomes:

```text
Request
   ↓
Intent
   ↓
Context
   ↓
Decision
   ↓
Execution boundary
   ↓
Host operation
```

## Make the Decision Explicit

A minimal framework-neutral decision model might look like this:

```csharp
public enum DecisionOutcome
{
    Allowed,
    Denied,
    Deferred,
    AcknowledgmentRequired,
    EscalationRecommended
}

public sealed record GovernanceDecision(
    DecisionOutcome Outcome,
    string ReasonCode,
    string Reason)
{
    public bool CanExecute => Outcome == DecisionOutcome.Allowed;

    public static GovernanceDecision Allow() =>
        new(
            DecisionOutcome.Allowed,
            "decision.allowed",
            "The operation may proceed.");

    public static GovernanceDecision Deny(
        string code,
        string reason) =>
        new(
            DecisionOutcome.Denied,
            code,
            reason);

    public static GovernanceDecision Defer(
        string code,
        string reason) =>
        new(
            DecisionOutcome.Deferred,
            code,
            reason);

    public static GovernanceDecision RequireAcknowledgment(
        string code,
        string reason) =>
        new(
            DecisionOutcome.AcknowledgmentRequired,
            code,
            reason);

    public static GovernanceDecision Escalate(
        string code,
        string reason) =>
        new(
            DecisionOutcome.EscalationRecommended,
            code,
            reason);
}
```

The important part is not the enum itself.

The important part is that the application now produces a first-class result describing what should happen **before** execution begins.

A boolean such as `true` or `false` can answer a narrow question.

A structured decision can carry operational meaning.

## Represent the Context

The evaluator needs the facts that matter to the decision.

Keep those facts explicit:

```csharp
public sealed record DisableAccountContext(
    DisableAccountIntent Intent,
    bool RequesterIsAdministrator,
    bool IsProtectedAccount,
    bool MaintenanceHoldActive,
    string CorrelationId,
    string PolicyVersion);
```

This makes the decision inputs visible.

The evaluator no longer needs to discover critical facts through hidden global state or unrelated services during execution.

A minimal evaluator can then be written as:

```csharp
public sealed class DisableAccountPolicy
{
    public GovernanceDecision Evaluate(
        DisableAccountContext context)
    {
        if (!context.RequesterIsAdministrator)
        {
            return GovernanceDecision.Deny(
                "account.disable.not-administrator",
                "Only administrators may disable accounts.");
        }

        if (context.IsProtectedAccount)
        {
            return GovernanceDecision.Escalate(
                "account.disable.protected-account",
                "Protected accounts require escalation.");
        }

        if (context.MaintenanceHoldActive)
        {
            return GovernanceDecision.Defer(
                "account.disable.maintenance-hold",
                "Account changes are temporarily deferred.");
        }

        if (string.IsNullOrWhiteSpace(context.Intent.Reason))
        {
            return GovernanceDecision.RequireAcknowledgment(
                "account.disable.reason-required",
                "A reason must be acknowledged before this operation proceeds.");
        }

        return GovernanceDecision.Allow();
    }
}
```

This is intentionally small.

A production system may compose many constraints, use external policy sources, include policy hashes, or emit richer reason data.

For learning purposes, the important boundary is visible:

```text
Context in
   ↓
Decision out
```

No account is disabled inside the evaluator.

## Keep Execution Host-Owned

Now separate the component that performs the side effect:

```csharp
public interface IDisableAccountExecutor
{
    Task ExecuteAsync(
        DisableAccountIntent intent,
        CancellationToken cancellationToken);
}
```

A host-owned implementation might be:

```csharp
public sealed class DisableAccountExecutor(
    IAccountService accountService)
    : IDisableAccountExecutor
{
    public Task ExecuteAsync(
        DisableAccountIntent intent,
        CancellationToken cancellationToken)
    {
        return accountService.DisableAsync(
            intent.AccountId,
            cancellationToken);
    }
}
```

The governance code determines what should happen.

The executor performs the operation.

Those responsibilities should not silently collapse back into one component.

## Orchestrate the Boundary

The host coordinates evaluation and execution:

```csharp
public sealed class DisableAccountWorkflow(
    DisableAccountPolicy policy,
    IDisableAccountExecutor executor,
    ILogger<DisableAccountWorkflow> logger)
{
    public async Task<GovernanceDecision> ExecuteAsync(
        DisableAccountContext context,
        CancellationToken cancellationToken)
    {
        GovernanceDecision decision = policy.Evaluate(context);

        logger.LogInformation(
            "Disable-account decision {Outcome}. " +
            "ReasonCode: {ReasonCode}; CorrelationId: {CorrelationId}; " +
            "PolicyVersion: {PolicyVersion}",
            decision.Outcome,
            decision.ReasonCode,
            context.CorrelationId,
            context.PolicyVersion);

        if (!decision.CanExecute)
        {
            return decision;
        }

        await executor.ExecuteAsync(
            context.Intent,
            cancellationToken);

        return decision;
    }
}
```

The significant line is:

```csharp
if (!decision.CanExecute)
{
    return decision;
}
```

The execution boundary is explicit.

A denied, deferred, acknowledgment-required, or escalated operation never reaches the executor.

## Sequence of Responsibility

The successful path looks like this:

```text
Caller
  |
  | proposes operation
  v
Host
  |
  | creates Intent + Context
  v
Governance Evaluator
  |
  | evaluates constraints
  v
Decision
  |
  | Allowed
  v
Host
  |
  | crosses execution boundary
  v
Executor
  |
  | performs side effect
  v
External / application state
```

A blocked path stops earlier:

```text
Caller
  |
  v
Host
  |
  v
Governance Evaluator
  |
  v
Decision: Denied / Deferred /
          AcknowledgmentRequired /
          EscalationRecommended
  |
  v
Host returns or routes the decision

Executor is never invoked.
```

That last sentence is an important invariant.

## Test the Boundary, Not Just the Decision

One of the main advantages of this separation is that tests can prove that a blocked decision never reaches execution.

A simple fake executor:

```csharp
public sealed class RecordingDisableAccountExecutor
    : IDisableAccountExecutor
{
    public int InvocationCount { get; private set; }

    public Task ExecuteAsync(
        DisableAccountIntent intent,
        CancellationToken cancellationToken)
    {
        InvocationCount++;
        return Task.CompletedTask;
    }
}
```

A denied-operation test can then verify both the decision and the absence of execution:

```csharp
[Fact]
public async Task NonAdministrator_DoesNotReachExecution()
{
    var executor = new RecordingDisableAccountExecutor();
    var policy = new DisableAccountPolicy();

    using ILoggerFactory loggerFactory =
        LoggerFactory.Create(builder => { });

    var workflow = new DisableAccountWorkflow(
        policy,
        executor,
        loggerFactory.CreateLogger<DisableAccountWorkflow>());

    var intent = new DisableAccountIntent(
        AccountId: "user-123",
        RequestedBy: "operator-7",
        Reason: "Security investigation");

    var context = new DisableAccountContext(
        Intent: intent,
        RequesterIsAdministrator: false,
        IsProtectedAccount: false,
        MaintenanceHoldActive: false,
        CorrelationId: Guid.NewGuid().ToString("N"),
        PolicyVersion: "1.0");

    GovernanceDecision decision =
        await workflow.ExecuteAsync(
            context,
            CancellationToken.None);

    Assert.Equal(
        DecisionOutcome.Denied,
        decision.Outcome);

    Assert.Equal(
        0,
        executor.InvocationCount);
}
```

The architectural property under test is:

> **A blocked decision cannot accidentally fall through into execution.**

That is stronger than testing only that the evaluator returned `Denied`.

## Why Logging Alone Is Not the Boundary

The naive implementation logged after disabling the account.

That log is useful operationally, but it does not create the decision boundary.

Consider:

```csharp
await accountService.DisableAsync(userId, cancellationToken);

logger.LogInformation(
    "Account disabled because policy allowed it.");
```

The log describes an action that already happened.

It does not prove that:

- The proposal existed separately from execution.
- A decision was produced before the side effect.
- A denied path could not reach execution.
- The active policy context was captured.
- The decision and execution can be correlated reliably.

Operational logging and governance evidence can overlap, but they solve different problems.

Later tutorials explore audit residue and provenance in more detail.

## Common Failure Modes

### 1. The Evaluator Performs the Side Effect

Avoid:

```csharp
public async Task<GovernanceDecision> EvaluateAsync(...)
{
    if (allowed)
    {
        await accountService.DisableAsync(...);
        return GovernanceDecision.Allow();
    }

    return GovernanceDecision.Deny(...);
}
```

The evaluator has now become an executor.

The decision no longer exists as a meaningful boundary before the side effect.

### 2. The Host Ignores the Decision

This is equally dangerous:

```csharp
GovernanceDecision decision = policy.Evaluate(context);

await executor.ExecuteAsync(
    context.Intent,
    cancellationToken);
```

The architecture may contain a decision model, but it does not govern anything unless execution is actually conditioned on the result.

### 3. Policy Logic Is Duplicated Around the Executor

If multiple controllers repeat their own checks before calling the same executor, the system can drift into inconsistent behavior.

Prefer a clear orchestration boundary where the relevant decision is made once for the proposed operation.

### 4. The Intent Is Too Vague

An intent such as:

```csharp
new OperationIntent("change-account");
```

may not contain enough information to evaluate or bind authority safely.

An intent should identify the operation and resource with enough precision for the decision being made.

### 5. Every Operation Becomes a Governance Pipeline

This pattern has a cost.

Do not introduce a large decision lifecycle for trivial code merely because the abstraction exists.

Use it where explicit decision boundaries add real value.

## Tradeoffs

Decision-before-execution improves separation, but it is not free.

### Benefits

- Proposed operations become inspectable before side effects occur.
- Decision logic is easier to test independently.
- Blocked decisions can be proven not to reach execution.
- Policy inputs and outcomes become more explicit.
- Host responsibility remains visible.
- Audit and correlation data have a natural place to attach.
- AI or automated systems can propose actions without owning execution.

### Costs

- More types and orchestration code.
- Additional modeling work.
- Potential latency if policy evaluation depends on external systems.
- Poorly designed abstractions can become ceremony.
- A generic policy layer can hide domain meaning if made too broad.
- Teams must agree on where the execution boundary actually lives.

The right question is not:

> Can this pattern be added?

The better question is:

> Does this operation benefit from an explicit, testable decision boundary before execution?

## Good Candidates

The pattern is especially useful for operations such as:

- Destructive administrative actions.
- Privileged configuration changes.
- Sensitive-data release.
- Deployment or infrastructure changes.
- Human approval workflows.
- High-impact background operations.
- External API actions with meaningful side effects.
- AI-proposed tool calls.
- Multi-tenant or region-specific policy decisions.

## Cases Where Simpler May Be Better

A full governance pipeline may be unnecessary for:

- Pure calculations.
- Local formatting or transformations.
- Low-risk read-only operations.
- Simple internal state changes already bounded by a clear domain invariant.
- Code where the additional decision model provides no useful distinction.

Learning should make architectures easier to reason about, not larger by default.

## Working Implementation References

This tutorial is deliberately framework-neutral. The working `AsiBackbone` repository implements fuller versions of the same boundaries with richer policy composition, decision metadata, tests, and ASP.NET Core integration.

Use these references as an implementation map rather than as required dependencies for understanding the pattern.

### Concept-to-Implementation Map

| Tutorial concept | Working reference | What to inspect |
| --- | --- | --- |
| Explicit governance decision | [`GovernanceDecision`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecision.cs) and [`GovernanceDecisionOutcome`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecisionOutcome.cs) | Compare the tutorial's small decision record and outcome enum with the framework's fuller decision model and outcome vocabulary. |
| Context and constraint evaluation | [`DefaultAsiBackbonePolicyEvaluator`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Evaluation/DefaultAsiBackbonePolicyEvaluator.cs) | Inspect how the working framework evaluates policy and composes governance decisions without turning the evaluator into the host operation itself. |
| Decision behavior under tests | [`PolicyEvaluatorEndToEndTests`](https://github.com/AsiBackbone/AsiBackbone/blob/main/tests/AsiBackbone.Core.Tests/Evaluation/PolicyEvaluatorEndToEndTests.cs) | Follow concrete tests that exercise the evaluator and verify decision behavior through the policy pipeline. |
| Intent through execution | [Intent-to-Execution Pattern](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/intent-to-execution-pattern.md) | Compare the tutorial's Request -> Intent -> Context -> Decision -> Execution flow with the fuller documented lifecycle. |
| Host-owned execution | [Host-Owned Execution Enforcement](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/host-owned-execution-enforcement.md) | Examine the framework guidance for keeping execution authority with the host after governance evaluation. |
| Concrete ASP.NET Core host | [`SampleGovernanceController`](https://github.com/AsiBackbone/AsiBackbone/blob/main/samples/PlainAspNetCoreHost/SampleGovernanceController.cs) | See a working host consume governance behavior in an ASP.NET Core application rather than treating the evaluator as the side-effect owner. |
| ASP.NET Core enforcement layer | [`AsiBackboneEndpointGovernanceMiddleware`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.AspNetCore/Endpoints/AsiBackboneEndpointGovernanceMiddleware.cs) and [`AsiBackboneEndpointGovernanceTests`](https://github.com/AsiBackbone/AsiBackbone/blob/main/tests/AsiBackbone.AspNetCore.Tests/Endpoints/AsiBackboneEndpointGovernanceTests.cs) | Inspect one concrete request-pipeline enforcement boundary together with the tests that exercise it. |

### Suggested Reading Order

If you are moving from this tutorial into the production-oriented repository, a useful progression is:

```text
GovernanceDecision + GovernanceDecisionOutcome
        |
        v
DefaultAsiBackbonePolicyEvaluator
        |
        v
PolicyEvaluatorEndToEndTests
        |
        v
Intent-to-Execution Pattern
        |
        v
Host-Owned Execution Enforcement
        |
        v
Plain ASP.NET Core host + endpoint governance middleware
```

The production framework is intentionally richer than the teaching example.

The purpose of these links is not to turn the Learning tutorial into package documentation. They let you move from a deliberately small architectural specimen to the fuller implementation and observe where additional production concerns enter the design.

The core boundary should still be recognizable:

```text
Proposed operation
   |
   v
Governance evaluation
   |
   v
Explicit decision
   |
   v
Host enforcement
   |
   v
Execution only when permitted
```

No single implementation file defines the entire architecture. Read the decision model, evaluator, tests, host guidance, and integration layer together.

## Apply the Pattern to AI

The same separation becomes particularly important when an AI system proposes a consequential tool call.

Do not model:

```text
Model output
   ↓
Tool execution
```

Prefer:

```text
Model proposes action
   ↓
Host creates intent
   ↓
Host creates policy context
   ↓
Governance decision
   ↓
Host-controlled execution boundary
   ↓
Tool invocation
```

This leads to a recurring rule throughout ASI Backbone Learning:

> **The model may propose. The host retains execution authority.**

The model does not become safer merely because it generated a syntactically valid tool call.

The host still owns the decision to perform the action.

## Beginner Exercise

Take an endpoint or service method that currently performs a consequential operation directly.

Refactor it so that you can identify these five distinct elements:

```text
1. Request
2. Intent
3. Context
4. Decision
5. Execution
```

Then write at least two tests:

1. An allowed decision reaches the executor exactly once.
2. A blocked decision never reaches the executor.

For additional practice, add either a `Deferred` or `AcknowledgmentRequired` outcome without modifying the executor.

If adding that outcome requires changing the component that performs the side effect, examine whether your decision and execution responsibilities are still too tightly coupled.

## Review Questions

Before moving on, you should be able to answer:

1. Why is an intent different from execution?
2. Why might authorization be only one input into a consequential decision?
3. What architectural property does an explicit decision result provide?
4. Why should the evaluator avoid performing the side effect?
5. What does host-owned execution mean?
6. Why should tests verify that blocked decisions never invoke the executor?
7. When would this pattern add unnecessary ceremony?
8. How does this pattern apply to an AI-proposed tool call?

## Next

The next foundational topic is **Policy Context and Explicit Decision Outcomes**.

That tutorial expands the middle of the flow:

```text
Intent
   ↓
Context
   ↓
Constraints
   ↓
Decision
```

and examines how the facts and outcomes of a governance decision can be represented explicitly rather than scattered through application code.

## Related Content

- [Foundational Tutorial Index](index.md) — view the complete five-tutorial learning path.
- [Policy Context and Explicit Decision Outcomes](policy-context-and-explicit-decision-outcomes.md) — continue into explicit policy facts, constraints, and structured outcomes.
- [Acknowledgment and Audit Residue](acknowledgment-and-audit-residue.md) — follow the decision lifecycle into acknowledgment and evidence.
- [Governed AI Tool Gateway](governed-ai-tool-gateway.md) — see the proposal-versus-execution boundary composed around AI-proposed tool calls.
- [Decision Before Execution sample](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-before-execution/README.md) — run the framework-neutral companion and observe that blocked decisions never invoke the executor.
- [Executable Samples](../samples/index.md) — explore the published companion-sample guide before following a canonical sample README.
- [Decision Before Execution lab](../labs/decision-before-execution.md) — break, repair, and extend the decision-to-execution boundary yourself.
- [Hands-On Labs](../labs/index.md) — explore the broader practice and reasoning layer.

---

> **Read it. Run it. Question it. Improve it.**
