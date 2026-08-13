# Policy Context and Explicit Decision Outcomes

**Learning objective:** Understand why the facts used to make a governance decision should be represented explicitly, and why the result should describe what happens next rather than collapse every decision into a boolean.

**Difficulty:** Beginner  
**Prerequisites:** [Decision Before Execution](decision-before-execution.md)

This is the second foundational tutorial in ASI Backbone Learning.

It builds on [Decision Before Execution](decision-before-execution.md), which established the first boundary:

```text
Proposed action
   ↓
Governed decision
   ↓
Host-owned execution
```

This tutorial expands the middle of that flow:

```text
Intent
   ↓
Policy Context
   ↓
Constraints
   ↓
Explicit Decision Outcome
```

The core idea is:

> **Make both the decision inputs and the decision result visible.**

## The Problem

A consequential operation is rarely evaluated from one fact.

A real decision may depend on:

- Who is proposing the operation.
- Which operation is being proposed.
- Which resource will be affected.
- The current state of that resource.
- Tenant, regional, or organizational boundaries.
- Environmental conditions.
- Risk or classification information.
- Policy version.
- Correlation and trace information.
- Prior acknowledgment or escalation state.

When these facts are scattered across controllers, services, claims, HTTP headers, global state, database calls, and logging statements, it becomes difficult to answer a basic question:

> What information actually caused this decision?

The problem becomes worse when the result is reduced to:

```csharp
bool allowed
```

A boolean can say yes or no.

It cannot explain whether the operation was:

- Allowed normally.
- Allowed with a warning.
- Denied.
- Deferred until later.
- Held for acknowledgment.
- Recommended for escalation.

A system that hides its inputs and compresses its outputs loses useful architectural information.

## A Scattered Implementation

Consider the account-disable example from the first tutorial.

A controller might grow into something like this:

```csharp
[HttpPost("{accountId}/disable")]
public async Task<IActionResult> DisableAccount(
    string accountId,
    CancellationToken cancellationToken)
{
    bool isAdministrator = User.IsInRole("Administrator");

    string? tenantId =
        User.FindFirst("tenant_id")?.Value;

    string? reason =
        Request.Headers["X-Operation-Reason"]
            .FirstOrDefault();

    Account account =
        await accountRepository.GetAsync(
            accountId,
            cancellationToken);

    bool maintenanceHold =
        await maintenanceService.IsActiveAsync(
            cancellationToken);

    bool allowed =
        isAdministrator &&
        account.TenantId == tenantId &&
        !account.IsProtected &&
        !maintenanceHold &&
        !string.IsNullOrWhiteSpace(reason);

    if (!allowed)
    {
        return Forbid();
    }

    await accountService.DisableAsync(
        accountId,
        cancellationToken);

    return NoContent();
}
```

This may work.

But the decision inputs are now spread across:

```text
Claims
Headers
Repository state
Environmental service
Route values
Local variables
```

And the result is still only:

```text
true / false
```

Important information has been discovered, but not represented.

## Separate Facts from Rules

A useful distinction is:

**Policy context contains facts.**

**Constraints interpret those facts.**

For example:

```text
FACT
Requester is an administrator.

RULE
Only administrators may disable accounts.
```

Those are not the same thing.

Similarly:

```text
FACT
Account is marked as protected.

RULE
Protected accounts require escalation.
```

Keeping facts and rules separate helps prevent a context object from becoming a hidden policy engine.

A context should describe the situation being evaluated.

A constraint or policy should decide what those facts mean.

## Model the Proposed Intent

Start with the operation itself:

```csharp
public sealed record DisableAccountIntent(
    string AccountId,
    string RequestedBy,
    string Reason);
```

The intent answers:

> What is being proposed?

It does not answer:

> Should it happen?

That remains the job of policy evaluation.

## Model Actor, Resource, and Environment

The context can be decomposed into smaller records:

```csharp
public sealed record ActorContext(
    string ActorId,
    string TenantId,
    bool IsAdministrator);

public sealed record AccountContext(
    string AccountId,
    string TenantId,
    bool IsProtected,
    bool IsAlreadyDisabled);

public sealed record EnvironmentContext(
    bool MaintenanceHoldActive,
    string Region);
```

Then combine the decision inputs:

```csharp
public sealed record DisableAccountPolicyContext(
    DisableAccountIntent Intent,
    ActorContext Actor,
    AccountContext Account,
    EnvironmentContext Environment,
    string CorrelationId,
    string PolicyVersion);
```

Now the evaluator receives one explicit snapshot of the facts relevant to the decision.

The structure is visible:

```text
Policy Context
   |
   +-- Intent
   |
   +-- Actor
   |
   +-- Resource
   |
   +-- Environment
   |
   +-- Correlation
   |
   +-- Policy identity
```

## Context Is a Snapshot

A policy context is most useful when it represents the facts **at evaluation time**.

That matters because context can otherwise become a collection of live service dependencies:

```csharp
public sealed class BadPolicyContext
{
    public IUserDirectory UserDirectory { get; init; }
    public IAccountRepository Accounts { get; init; }
    public IMaintenanceService Maintenance { get; init; }
}
```

This object does not really contain context.

It contains ways to discover context later.

That creates several problems:

- Tests need more infrastructure.
- Evaluation may depend on call order.
- Facts may change during evaluation.
- The same policy input is difficult to reproduce.
- Audit evidence cannot easily describe what was actually observed.

Prefer gathering the required facts first:

```text
Host gathers facts
   ↓
Context snapshot created
   ↓
Policy evaluates snapshot
```

This does not mean every value must be immutable forever.

It means the decision should have a clear understanding of the facts it evaluated.

## Do Not Put Everything in Context

Explicit context does not mean unlimited context.

Avoid turning the context into:

```text
Entire HTTP request
Entire user object
Entire database entity graph
Every environment variable
Every claim
Every configuration value
```

Include what is relevant to the decision.

This improves:

- Testability.
- Reviewability.
- Privacy.
- Serialization safety.
- Audit usefulness.
- Policy clarity.

A useful question is:

> If this field changed, could it legitimately change the decision?

If the answer is no, the field may not belong in the policy context.

## Model Outcomes Explicitly

Now make the result equally visible.

A framework-neutral outcome model might be:

```csharp
public enum GovernanceDecisionOutcome
{
    Allowed,
    Warning,
    Denied,
    Deferred,
    AcknowledgmentRequired,
    EscalationRecommended
}
```

Each outcome answers a different operational question.

### Allowed

The operation may cross the execution boundary.

```text
Decision: Allowed
   ↓
Host may execute
```

### Warning

The operation may proceed, but warning information should remain visible to the host or audit path.

```text
Decision: Warning
   ↓
Host may execute
   +
Warning reason retained
```

### Denied

The operation should not execute.

```text
Decision: Denied
   ↓
Stop
```

### Deferred

The operation should not execute now, but the decision does not necessarily represent a permanent rejection.

```text
Decision: Deferred
   ↓
Retry later or route elsewhere
```

Examples include:

- Temporary maintenance holds.
- Dependency unavailability.
- A policy decision requiring later reevaluation.

### Acknowledgment Required

The operation cannot proceed until an acknowledgment boundary has been satisfied.

```text
Decision: AcknowledgmentRequired
   ↓
Pause
   ↓
Obtain acknowledgment
   ↓
Re-enter governed flow
```

This is not the same as `Allowed`.

The acknowledgment requirement is part of the decision lifecycle.

### Escalation Recommended

The operation should be routed to a higher-authority or specialized decision path before execution.

```text
Decision: EscalationRecommended
   ↓
Do not execute
   ↓
Route for escalation
```

Examples include:

- Protected resources.
- High-risk operations.
- Policy ambiguity requiring human review.

## Add Structured Reasons

An explicit outcome becomes much more useful when paired with structured reasons.

Use both:

- A **machine-readable code**.
- A **human-readable explanation**.

For example:

```csharp
public sealed record DecisionReason(
    string Code,
    string Message);
```

A decision can then be modeled as:

```csharp
public sealed record GovernanceDecision(
    GovernanceDecisionOutcome Outcome,
    IReadOnlyList<DecisionReason> Reasons)
{
    public bool CanProceed =>
        Outcome is GovernanceDecisionOutcome.Allowed
            or GovernanceDecisionOutcome.Warning;

    public static GovernanceDecision Allow() =>
        new(
            GovernanceDecisionOutcome.Allowed,
            []);

    public static GovernanceDecision Warning(
        string code,
        string message) =>
        new(
            GovernanceDecisionOutcome.Warning,
            [new DecisionReason(code, message)]);

    public static GovernanceDecision Deny(
        string code,
        string message) =>
        new(
            GovernanceDecisionOutcome.Denied,
            [new DecisionReason(code, message)]);

    public static GovernanceDecision Defer(
        string code,
        string message) =>
        new(
            GovernanceDecisionOutcome.Deferred,
            [new DecisionReason(code, message)]);

    public static GovernanceDecision RequireAcknowledgment(
        string code,
        string message) =>
        new(
            GovernanceDecisionOutcome.AcknowledgmentRequired,
            [new DecisionReason(code, message)]);

    public static GovernanceDecision Escalate(
        string code,
        string message) =>
        new(
            GovernanceDecisionOutcome.EscalationRecommended,
            [new DecisionReason(code, message)]);
}
```

A reason code might be:

```text
account.disable.not-administrator
account.disable.cross-tenant
account.disable.protected-account
account.disable.maintenance-hold
account.disable.reason-required
```

The code is useful to software.

The message is useful to people.

Avoid making software parse human prose to determine what happened.

## Evaluate the Context

A small policy can now consume the explicit context:

```csharp
public sealed class DisableAccountPolicy
{
    public GovernanceDecision Evaluate(
        DisableAccountPolicyContext context)
    {
        if (!context.Actor.IsAdministrator)
        {
            return GovernanceDecision.Deny(
                "account.disable.not-administrator",
                "Only administrators may disable accounts.");
        }

        if (!string.Equals(
                context.Actor.TenantId,
                context.Account.TenantId,
                StringComparison.Ordinal))
        {
            return GovernanceDecision.Deny(
                "account.disable.cross-tenant",
                "The actor and account belong to different tenants.");
        }

        if (context.Account.IsAlreadyDisabled)
        {
            return GovernanceDecision.Warning(
                "account.disable.already-disabled",
                "The account is already disabled.");
        }

        if (context.Account.IsProtected)
        {
            return GovernanceDecision.Escalate(
                "account.disable.protected-account",
                "Protected accounts require escalation.");
        }

        if (context.Environment.MaintenanceHoldActive)
        {
            return GovernanceDecision.Defer(
                "account.disable.maintenance-hold",
                "Account changes are temporarily deferred.");
        }

        if (string.IsNullOrWhiteSpace(
                context.Intent.Reason))
        {
            return GovernanceDecision.RequireAcknowledgment(
                "account.disable.reason-required",
                "A reason must be supplied and acknowledged.");
        }

        return GovernanceDecision.Allow();
    }
}
```

This example intentionally uses one policy class so the mechanics remain easy to see.

A larger system may compose many independent constraints.

The underlying principle stays the same:

```text
Explicit facts
   ↓
Explicit rules
   ↓
Explicit outcome
```

## Why Not Return HTTP Status Codes Directly?

A policy evaluator should usually avoid returning:

```text
200
403
409
423
503
```

Those are transport-level decisions.

A governance outcome is a domain or architectural decision.

The host can translate it:

```csharp
public static IResult ToHttpResult(
    GovernanceDecision decision)
{
    return decision.Outcome switch
    {
        GovernanceDecisionOutcome.Allowed =>
            Results.Ok(),

        GovernanceDecisionOutcome.Warning =>
            Results.Ok(new
            {
                warning = decision.Reasons
            }),

        GovernanceDecisionOutcome.Denied =>
            Results.Forbid(),

        GovernanceDecisionOutcome.Deferred =>
            Results.StatusCode(
                StatusCodes.Status503ServiceUnavailable),

        GovernanceDecisionOutcome.AcknowledgmentRequired =>
            Results.Conflict(new
            {
                state = "acknowledgment-required",
                reasons = decision.Reasons
            }),

        GovernanceDecisionOutcome.EscalationRecommended =>
            Results.Conflict(new
            {
                state = "escalation-recommended",
                reasons = decision.Reasons
            }),

        _ => Results.StatusCode(
            StatusCodes.Status500InternalServerError)
    };
}
```

The exact HTTP mapping is application-specific.

That is the point.

The governance model remains independent of HTTP.

The same decision can later be consumed by:

- A web API.
- A message consumer.
- A background worker.
- A CLI.
- A desktop application.
- An AI tool gateway.

## Context Should Not Decide Its Own Outcome

Avoid methods such as:

```csharp
public sealed record PolicyContext(...)
{
    public bool IsAllowed()
    {
        // policy logic here
    }
}
```

The object now contains both:

```text
Facts
+
Rules
```

That may be appropriate for a small domain object enforcing its own invariant.

But for a reusable governance pipeline, it makes policy harder to replace, compose, test, version, or explain.

Keep the distinction intentional.

## Outcomes Should Describe State, Not Trigger Work

Avoid:

```csharp
public sealed class GovernanceDecision
{
    public async Task ExecuteAsync()
    {
        ...
    }
}
```

A decision describes what the evaluator concluded.

It should not quietly acquire execution authority.

This preserves the boundary established in the first tutorial:

> **The decision informs execution. The host owns execution.**

## Composition Requires a Policy

Once more than one rule can contribute, the system needs an explicit composition strategy.

Suppose evaluation produces:

```text
Warning: account already disabled
Deferred: maintenance hold active
Denied: actor is not authorized
```

Which result wins?

There is no universal answer that every system must use.

The important architectural requirement is that precedence should be **defined**, not accidental.

Possible policies include:

- Denial short-circuits all later rules.
- All constraints run and reasons are accumulated.
- Threat findings take precedence over ordinary warnings.
- Acknowledgment or escalation is introduced by a final decision policy.
- The first terminal outcome wins.
- The most restrictive outcome wins according to a documented ordering.

Do not let precedence emerge from whichever `if` statement happens to execute first unless that ordering is intentional.

## Determinism Matters

Given the same:

```text
Intent
Context
Policy version
```

a deterministic policy should normally produce the same outcome.

That improves:

- Testing.
- Reproduction.
- Incident analysis.
- Audit interpretation.
- Policy review.

Not all inputs are deterministic.

Examples include:

- External risk scores.
- Time-dependent facts.
- Probabilistic classifications.
- Remote policy services.

When nondeterministic inputs matter, represent the **observed result** in context when practical.

For example:

```csharp
public sealed record RiskContext(
    decimal Score,
    string Source,
    DateTimeOffset ObservedAt);
```

The policy can then evaluate what was observed rather than silently invoking an external scorer during the decision.

## Preserve Policy Identity

If policy changes over time, the same context may legitimately produce a different decision under a different policy version.

That is why decision context often benefits from fields such as:

```csharp
string CorrelationId
string PolicyVersion
string? PolicyHash
```

These values do not decide the operation by themselves.

They help explain **which decision process was applied**.

A useful conceptual record is:

```text
Intent
+
Context snapshot
+
Policy identity
=
Decision evidence
```

Later tutorials will expand this into acknowledgment and audit residue.

## Keep Sensitive Data Out of Reason Messages

Structured reasons are useful, but they can become a data-leak path.

Avoid:

```text
Denied because user's SSN is 123-45-6789.
```

Prefer:

```text
Code:
data.release.restricted-classification

Message:
The requested resource has a restricted classification.
```

Decision reasons may reach:

- Logs.
- Audit stores.
- HTTP responses.
- User interfaces.
- Telemetry.
- Support tooling.

Treat them as potentially observable.

## Test the Decision Matrix

Explicit context and outcomes make table-driven testing straightforward.

For example:

| Scenario | Expected outcome | Expected reason |
| --- | --- | --- |
| Non-administrator | `Denied` | `account.disable.not-administrator` |
| Cross-tenant account | `Denied` | `account.disable.cross-tenant` |
| Already disabled | `Warning` | `account.disable.already-disabled` |
| Protected account | `EscalationRecommended` | `account.disable.protected-account` |
| Maintenance hold | `Deferred` | `account.disable.maintenance-hold` |
| Missing reason | `AcknowledgmentRequired` | `account.disable.reason-required` |
| Normal request | `Allowed` | none |

A test can then verify both outcome and reason:

```csharp
[Theory]
[InlineData(
    false,
    false,
    false,
    false,
    "Reason",
    GovernanceDecisionOutcome.Denied,
    "account.disable.not-administrator")]
[InlineData(
    true,
    false,
    false,
    true,
    "Reason",
    GovernanceDecisionOutcome.Deferred,
    "account.disable.maintenance-hold")]
public void Evaluate_ReturnsExpectedOutcome(
    bool isAdministrator,
    bool isProtected,
    bool alreadyDisabled,
    bool maintenanceHold,
    string reason,
    GovernanceDecisionOutcome expectedOutcome,
    string expectedReasonCode)
{
    var context = CreateContext(
        isAdministrator,
        isProtected,
        alreadyDisabled,
        maintenanceHold,
        reason);

    GovernanceDecision decision =
        new DisableAccountPolicy()
            .Evaluate(context);

    Assert.Equal(
        expectedOutcome,
        decision.Outcome);

    Assert.Contains(
        decision.Reasons,
        item => item.Code == expectedReasonCode);
}
```

The test is now describing policy behavior rather than reproducing controller implementation details.

## Test Context Construction Separately

There are really two questions:

```text
1. Did the host construct the correct context?
2. Did the policy evaluate that context correctly?
```

Those deserve separate tests.

A context-construction test can verify that:

```text
Actor claim
   ↓
ActorContext.TenantId

Database state
   ↓
AccountContext.IsProtected

Configuration / service state
   ↓
EnvironmentContext.MaintenanceHoldActive
```

A policy test can then work entirely with in-memory context.

This separation makes failures easier to diagnose.

## Common Failure Modes

### 1. Context Becomes a Service Locator

Avoid putting repositories, HTTP contexts, database contexts, or broad service providers inside policy context.

Gather facts before evaluation.

### 2. Context Contains Policy Conclusions

Avoid fields such as:

```csharp
bool ShouldDeny
bool RequiresEscalation
```

unless those are genuinely upstream facts produced by a separate authoritative subsystem.

Context should not smuggle the final decision into the evaluator.

### 3. Boolean Outcomes Return

A rich context does little good if the policy still ends with:

```csharp
return true;
```

Preserve meaningful decision states.

### 4. Reason Codes Are Unstable Prose

Avoid using human text as the machine identifier:

```text
"You cannot do that right now."
```

Prefer a stable code:

```text
account.disable.maintenance-hold
```

and allow the explanatory message to evolve separately.

### 5. Outcome Meaning Is Ambiguous

If one team interprets `Deferred` as "retry automatically" and another interprets it as "send for human review," the outcome is not sufficiently defined.

Document what each state means to the host.

### 6. Context Is Captured Too Late

If the host performs part of the side effect and only then constructs policy context, the decision boundary has already been crossed.

Context must be available before governed execution.

### 7. Policy Context Becomes an Audit Dump

Not every context field belongs in durable audit storage.

Decision context and audit residue are related but different concepts.

The next tutorial will examine that boundary more closely.

## Tradeoffs

### Benefits

- Decision inputs become reviewable.
- Policies are easier to unit test.
- Outcomes carry operational meaning.
- Reason codes improve diagnostics and automation.
- Policy versions can be correlated with decisions.
- Transport concerns remain outside the core decision model.
- Host execution behavior becomes easier to map explicitly.
- AI-generated proposals can be evaluated through the same contract.

### Costs

- More modeling types.
- Context construction becomes a deliberate responsibility.
- Teams must define outcome semantics.
- Composition rules must be documented.
- Context schemas may evolve with policy requirements.
- Poorly bounded context can become bloated or privacy-sensitive.

The goal is not maximum structure.

The goal is enough structure to make consequential decisions understandable.

## Relationship to AsiBackbone

This tutorial is framework-neutral, but the working `AsiBackbone` repository provides concrete versions of these concepts.

Useful references include:

- [`IAsiBackboneConstraintEvaluationContext`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Constraints/IAsiBackboneConstraintEvaluationContext.cs) — a framework-neutral base for correlation identifiers, policy version/hash, and host-supplied metadata.
- [`GovernanceDecisionOutcome`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecisionOutcome.cs) — the current explicit outcome vocabulary used by the framework.
- [`GovernanceDecision`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecision.cs) — structured outcomes, reasons, reason codes, correlation and trace identifiers, and policy identity.
- [`DefaultAsiBackbonePolicyEvaluator`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Evaluation/DefaultAsiBackbonePolicyEvaluator.cs) — constraint evaluation and composition into a governance decision.

The framework currently distinguishes these outcomes:

```text
Allowed
Warning
Denied
Deferred
AcknowledgmentRequired
EscalationRecommended
```

The Learning example uses the same vocabulary so the conceptual model maps cleanly to the working implementation.

## Apply the Pattern to AI

An AI tool request makes explicit policy context especially useful.

Avoid:

```text
Model proposes:
"delete customer record"

Host:
"Model is trusted, execute."
```

Prefer:

```text
AI proposal
   ↓
Intent
   ↓
Host constructs context
   |
   +-- authenticated actor
   +-- tenant
   +-- requested tool
   +-- target resource
   +-- classification
   +-- environment
   +-- policy identity
   ↓
Governance decision
   ↓
Allowed / Warning / Denied /
Deferred / AcknowledgmentRequired /
EscalationRecommended
   ↓
Host-controlled next step
```

The model can contribute information to the proposal.

It should not silently define the authoritative security or governance context.

That remains a host responsibility.

> **The model may propose. The host retains execution authority.**

## Exercise

Extend the account-disable example from [Decision Before Execution](decision-before-execution.md).

Create a policy context containing:

```text
Intent
Actor
Account
Environment
CorrelationId
PolicyVersion
```

Then implement at least these outcomes:

```text
Allowed
Denied
Deferred
AcknowledgmentRequired
EscalationRecommended
```

Add `Warning` if you want the host to proceed while retaining a non-blocking concern.

Write tests that prove:

1. Every modeled scenario returns the expected outcome.
2. Every non-allowed outcome has a stable reason code.
3. A normal request is allowed.
4. A blocked decision remains blocked regardless of transport mapping.
5. Context construction and policy evaluation can be tested independently.

For additional practice, add a new context field:

```text
DataClassification
```

Then introduce a rule that changes the decision for restricted data without modifying the executor.

If changing the rule requires changing execution code, examine whether policy and execution have become coupled again.

## Review Questions

Before moving on, you should be able to answer:

1. What is policy context?
2. Why should context contain facts rather than policy conclusions?
3. Why is a context snapshot easier to test than a collection of service dependencies?
4. When is a boolean result insufficient?
5. What is the operational difference between `Denied`, `Deferred`, `AcknowledgmentRequired`, and `EscalationRecommended`?
6. Why should reason codes be machine-readable and stable?
7. Why should governance outcomes remain independent of HTTP status codes?
8. What does policy identity add to a decision?
9. What problems arise when context becomes too broad?
10. Why should the host, not an AI model, construct authoritative policy context?

## Next

The next foundational topic is **Acknowledgment and Audit Residue**.

That tutorial expands the lifecycle after a decision:

```text
Decision
   ↓
Acknowledgment when required
   ↓
Host action
   ↓
Audit residue
```

It will examine how a consequential operation can pause for explicit acknowledgment and how structured evidence can preserve what happened without confusing governance evidence with ordinary application logging.

## Related Content

- [Foundational Tutorial Index](index.md) — view the complete five-tutorial governed-execution learning path.
- [Decision Before Execution](decision-before-execution.md) — revisit the separation between proposed intent, governance decisions, and host-owned execution.
- [Acknowledgment and Audit Residue](acknowledgment-and-audit-residue.md) — continue from explicit decision outcomes into acknowledgment, re-evaluation, correlation, and governance evidence.
- [Scoped Capability and Host-Owned Execution](scoped-capability-and-host-owned-execution.md) — follow allowed or acknowledged decisions into narrowly scoped execution authority.
- [Governed AI Tool Gateway](governed-ai-tool-gateway.md) — see authoritative host context and explicit decision outcomes applied to AI-proposed tool actions.
- [Policy Context and Explicit Decision Outcomes sample](../../samples/policy-context-and-explicit-decision-outcomes/README.md) — run the companion decision matrix and observe explicit context snapshots, structured reason codes, and host-controlled next-step semantics.
- [Executable Samples](https://github.com/AsiBackbone/Learning/tree/main/samples) — explore runnable companion material as the sample set develops.
- [Hands-On Labs](../labs/index.md) — practice constructing policy context, defining decision outcomes, and reasoning about governance boundaries.

---

> **Read it. Run it. Question it. Improve it.**
