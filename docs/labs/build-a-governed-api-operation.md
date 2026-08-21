---
description: Extend an ASP.NET Core API into governed execution with explicit intent, policy, acknowledgment, scoped authority, and host-owned side effects.
---

# Lab — Build a Governed API Operation

**Learning objective:** Extend a realistic ASP.NET Core API operation from ordinary authorization into an explicit governed-execution flow while preserving host-owned side effects and testable execution boundaries.

**Difficulty:** Intermediate

**Pattern classification:** Canonical pattern

**Prerequisites:** Complete [Decision Before Execution](../tutorials/decision-before-execution.md), [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md), [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md), and [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md). Read [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md) before starting so that the authorization/governance boundary is explicit.

This lab bridges the foundational governance sequence into an ASP.NET Core API operation.

You will work with a fictional endpoint:

```text
POST /accounts/{accountId}/disable
```

The endpoint represents a consequential operation because it changes account access.

The lab begins with an application that already authenticates callers and uses ASP.NET Core authorization.

That existing authorization boundary is **not** a defect.

The exercise asks a narrower question:

> **What additional architecture becomes useful when an already-authorized request can still be denied, deferred, paused for acknowledgment, escalated, or granted narrow execution authority?**

The target flow is:

```text
Request
   ↓
Authentication
   ↓
ASP.NET Core authorization
   ↓
Proposed intent
   ↓
Authoritative policy context
   ↓
Governance decision
   ↓
Acknowledgment when required
   ↓
Scoped authority
   ↓
Host-owned execution
   ↓
Audit residue
```

The primary invariant for the lab is:

```text
Blocked decision
        ↓
Underlying account-service invocation count = 0
```

A second invariant is equally important:

```text
Allowed decision
        ≠
Successful execution
```

An account service can fail after policy has correctly allowed the operation.

The decision and execution result must remain distinguishable.

---

## Starting Architecture

Assume the application already has:

- Authentication configured by the host.
- An ASP.NET Core authorization policy named `CanDisableAccount`.
- An account repository.
- An account service that owns the real side effect.

A simplified endpoint may look like this:

```csharp
app.MapPost(
    "/accounts/{accountId}/disable",
    async (
        string accountId,
        DisableAccountRequest request,
        ClaimsPrincipal user,
        IAuthorizationService authorization,
        IAccountRepository accounts,
        IAccountService accountService,
        CancellationToken cancellationToken) =>
    {
        Account account =
            await accounts.GetAsync(
                accountId,
                cancellationToken);

        AuthorizationResult authorizationResult =
            await authorization.AuthorizeAsync(
                user,
                account,
                "CanDisableAccount");

        if (!authorizationResult.Succeeded)
        {
            return Results.Forbid();
        }

        await accountService.DisableAsync(
            account.Id,
            request.Reason,
            cancellationToken);

        return Results.NoContent();
    });
```

This flow is legitimate when the only application question is:

```text
Is this actor authorized to disable this account now?
```

But suppose the application now has additional requirements:

```text
Legal hold
    → Denied

Temporary maintenance hold
    → Deferred

Sensitive account
    → AcknowledgmentRequired

Protected account
    → EscalationRecommended

Normal account
    → Allowed
```

Those are not all ordinary authorization failures.

This is the boundary you will make explicit.

---

# Part 1 — Identify the Real Execution Boundary

Locate the line that performs the side effect:

```csharp
await accountService.DisableAsync(...);
```

Treat that call as the protected execution boundary for this lab.

Before changing the architecture, add or sketch a recording service:

```csharp
public sealed class RecordingAccountService : IAccountService
{
    public int DisableCallCount { get; private set; }

    public Task DisableAsync(
        string accountId,
        string? reason,
        CancellationToken cancellationToken)
    {
        DisableCallCount++;
        return Task.CompletedTask;
    }
}
```

The exact implementation can differ.

The important observation is measurable:

```text
Did the underlying service execute?
```

Add a baseline test for an authorized normal account and confirm:

```text
HTTP request succeeds
Account service calls = 1
```

Then add a baseline authorization-denied test and confirm:

```text
ASP.NET Core authorization fails
Account service calls = 0
```

Record the distinction in your notes:

```text
Authorization failure
        ↓
Request never enters governed operation
```

Later tests will add a different path:

```text
Authorization succeeds
        ↓
Governance still blocks or pauses execution
```

That difference is the central learning objective.

---

# Part 2 — Make the Exercise Disposable

Do not experiment directly in a production application.

Use one of these approaches:

1. Create a small disposable ASP.NET Core application for the lab.
2. Copy a representative endpoint and supporting abstractions into a temporary project.
3. Use a feature branch or disposable test host in an existing training application.

A minimal disposable structure can be:

```text
GovernedApiLab/
├── Program.cs
├── Governance/
│   ├── DisableAccountIntent.cs
│   ├── DisableAccountPolicyContext.cs
│   ├── GovernanceDecision.cs
│   ├── DisableAccountPolicy.cs
│   ├── AcknowledgmentChallenge.cs
│   ├── ExecutionCapability.cs
│   └── GovernanceResidue.cs
├── Accounts/
│   ├── Account.cs
│   ├── IAccountRepository.cs
│   └── IAccountService.cs
└── Tests/
    └── DisableAccountEndpointTests.cs
```

You may keep everything in fewer files if that makes the exercise easier to inspect.

The objective is architectural visibility, not folder count.

If you use an ASP.NET Core integration-test host, configure test identity using your normal test-auth approach or test-host middleware.

Do not make the governance lesson depend on a particular authentication test package.

---

# Part 3 — Keep Authorization as Authorization

Before adding governance, decide what `CanDisableAccount` should answer.

For this lab, keep authorization narrow:

```text
Is the caller authenticated?
Is the caller permitted to request account-disable operations?
May the caller act on this account or tenant?
```

For example, the authorization layer may establish:

```text
Actor has AccountOperator permission
Actor belongs to tenant-a
Account belongs to tenant-a
        ↓
Authorized
```

Do **not** encode every later workflow state into authorization failure reasons.

Avoid this style:

```text
Authorization failed: maintenance hold
Authorization failed: acknowledgment required
Authorization failed: escalate to senior review
```

Those states mean different things operationally.

Keep this conceptual boundary:

```text
ASP.NET Core authorization
        ↓
"May this actor enter this operation?"

Governance
        ↓
"What should happen next with this proposed operation?"
```

Add a test proving that authorization failure still prevents the governance path from reaching execution.

Then continue only with requests that have already passed authorization.

---

# Part 4 — Represent Proposed Intent

Create an explicit intent model.

For example:

```csharp
public sealed record DisableAccountIntent(
    string AccountId,
    string RequestedBy,
    string? Reason,
    string? AcknowledgmentId);
```

Construct it at the host boundary after authorization.

Do not let the request body choose authoritative actor identity.

For example:

```csharp
string actorId =
    user.FindFirst("sub")?.Value
    ?? throw new InvalidOperationException(
        "Authenticated actor identifier is required.");

var intent = new DisableAccountIntent(
    AccountId: account.Id,
    RequestedBy: actorId,
    Reason: request.Reason,
    AcknowledgmentId: request.AcknowledgmentId);
```

The request may propose:

```text
Reason
Acknowledgment reference
```

The host supplies:

```text
Authenticated actor
Resolved account identity
```

Answer in your notes:

1. Which intent fields came from the caller?
2. Which fields came from host-owned identity or resource state?
3. Could a caller substitute another actor or tenant by modifying JSON?
4. Which values must never be trusted merely because a model binder produced them?

The intent describes the proposal.

It still performs no side effect.

---

# Part 5 — Build Authoritative Policy Context

Create an explicit policy-context snapshot.

One possible model is:

```csharp
public sealed record DisableAccountPolicyContext(
    DisableAccountIntent Intent,
    string ActorId,
    string ActorTenantId,
    string AccountId,
    string AccountTenantId,
    bool LegalHoldActive,
    bool MaintenanceHoldActive,
    bool RequiresSensitiveAcknowledgment,
    bool IsProtectedAccount,
    bool RequiredAcknowledgmentSatisfied,
    string OperationName,
    string CorrelationId,
    string PolicyId,
    string PolicyVersion);
```

Populate it from authoritative host sources where appropriate.

A useful ownership table is:

| Fact | Example source | Trust assumption |
| --- | --- | --- |
| Actor identity | Authenticated principal | Established by host authentication |
| Actor tenant | Claims or directory lookup | Host-validated |
| Account identity | Route + repository resolution | Host resolves authoritative resource |
| Account tenant | Repository | Resource state, not caller assertion |
| Legal hold | Repository or policy-data source | Host-owned fact |
| Maintenance hold | Configuration/operational state | Host-owned fact |
| Sensitive flag | Resource classification | Host-owned fact |
| Protected flag | Resource metadata | Host-owned fact |
| Correlation ID | Host request context | Operational identity |
| Policy ID/version | Active policy descriptor | Decision provenance |

Do not turn the context object into a service locator.

Prefer:

```text
Host gathers facts
   ↓
Context snapshot
   ↓
Policy evaluates snapshot
```

not:

```text
Policy context contains repositories and HTTP services
   ↓
Facts are discovered unpredictably during evaluation
```

At this stage, add a focused unit test or inspection test confirming that the caller cannot override `ActorTenantId`, `AccountTenantId`, or policy identity through request input.

---

# Part 6 — Evaluate an Explicit Governance Decision

Introduce the decision vocabulary used throughout Learning:

```csharp
public enum GovernanceDecisionOutcome
{
    Allowed,
    Denied,
    Deferred,
    AcknowledgmentRequired,
    EscalationRecommended
}
```

Use structured reasons:

```csharp
public sealed record DecisionReason(
    string Code,
    string Message);

public sealed record GovernanceDecision(
    GovernanceDecisionOutcome Outcome,
    IReadOnlyList<DecisionReason> Reasons,
    string CorrelationId,
    string PolicyId,
    string PolicyVersion)
{
    public bool CanProceed =>
        Outcome == GovernanceDecisionOutcome.Allowed;
}
```

Now implement a small policy.

For the lab, use behavior such as:

```text
Legal hold
    → Denied

Maintenance hold
    → Deferred

Protected account
    → EscalationRecommended

Sensitive account without valid acknowledgment
    → AcknowledgmentRequired

Otherwise
    → Allowed
```

Example reason codes:

```text
account.disable.legal-hold
account.disable.maintenance-hold
account.disable.protected-account
account.disable.acknowledgment-required
```

The policy should return data.

It should not:

- Call `IAccountService`.
- Return `IResult`.
- Return HTTP status codes.
- Write an HTTP response.
- Throw an exception to represent an expected denial.

Write one test per decision outcome before integrating the evaluator into the endpoint.

The policy tests should answer:

```text
Given this context, what decision is produced?
```

They should not yet answer:

```text
Was the account service called?
```

That second question belongs to the host integration tests later in the lab.

---

# Part 7 — Map Governance Outcomes at the HTTP Host Boundary

Return to the endpoint.

After authorization and context construction:

```csharp
GovernanceDecision decision =
    policy.Evaluate(context);
```

The ASP.NET Core host now translates the decision into HTTP behavior.

A possible mapping is:

| Governance outcome | Example host behavior | Executes now? |
| --- | --- | --- |
| `Allowed` | Continue toward capability + execution | Yes, if later validation succeeds |
| `Denied` | `403` Problem Details or application-specific denial response | No |
| `Deferred` | `503` or application-specific retry/defer response | No |
| `AcknowledgmentRequired` | `409` with acknowledgment challenge information | No |
| `EscalationRecommended` | `409` or workflow-routing response | No |

The exact status codes are host choices.

The policy evaluator should not know them.

For example:

```csharp
static IResult MapBlockedDecision(
    GovernanceDecision decision)
{
    return decision.Outcome switch
    {
        GovernanceDecisionOutcome.Denied =>
            Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Operation denied",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = decision.Reasons[0].Code,
                    ["correlationId"] = decision.CorrelationId
                }),

        GovernanceDecisionOutcome.Deferred =>
            Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Operation deferred",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = decision.Reasons[0].Code,
                    ["correlationId"] = decision.CorrelationId
                }),

        GovernanceDecisionOutcome.AcknowledgmentRequired =>
            Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Acknowledgment required",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = decision.Reasons[0].Code,
                    ["correlationId"] = decision.CorrelationId
                }),

        GovernanceDecisionOutcome.EscalationRecommended =>
            Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Escalation recommended",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = decision.Reasons[0].Code,
                    ["correlationId"] = decision.CorrelationId
                }),

        _ => throw new InvalidOperationException(
            "Only blocked outcomes should be mapped here.")
    };
}
```

The guard exception at the end represents an impossible host-programming state in this sketch.

It is not being used to model expected governance denial.

If your application already follows the [Centralized Error Handling and Problem Details](../aspnetcore/centralized-error-handling-and-problem-details.md) convention, reuse its safe Problem Details shape.

This lab does not require that tutorial.

The requirement is simply:

> **Expected governance outcomes remain explicit values and are translated by the host.**

Now add endpoint integration tests for:

```text
Denied
Deferred
EscalationRecommended
```

Each must assert:

```text
Account service calls = 0
```

---

# Part 8 — Add an Acknowledgment Boundary

For a sensitive account, the first decision should be:

```text
AcknowledgmentRequired
```

Do not solve this with:

```json
{
  "confirmed": true
}
```

A generic boolean does not say what was acknowledged or what operation it belongs to.

Create a small challenge model:

```csharp
public sealed record AcknowledgmentChallenge(
    string ChallengeId,
    string ActorId,
    string OperationName,
    string ResourceId,
    string ReasonCode,
    string CorrelationId,
    string PolicyId,
    string PolicyVersion,
    DateTimeOffset ExpiresUtc);
```

When policy returns `AcknowledgmentRequired`:

1. Create a challenge.
2. Store it in an in-memory challenge store for the exercise.
3. Return the challenge identifier to the client.
4. Do not call `IAccountService`.

On the follow-up request:

1. Load the challenge by identifier.
2. Verify actor, operation, resource, policy identity, correlation, and expiration as appropriate.
3. Reconstruct current policy context.
4. Set only the specific acknowledgment fact that was actually satisfied.
5. Re-evaluate policy.

The flow should become:

```text
Authorized request
   ↓
Decision = AcknowledgmentRequired
   ↓
Challenge issued
   ↓
No execution
   ↓
Bound acknowledgment returned
   ↓
Current context reconstructed
   ↓
Policy re-evaluated
   ↓
Current decision
```

Add at least these tests:

```text
AcknowledgmentRequired without acknowledgment
        ↓
Account service calls = 0
```

```text
Acknowledgment for wrong resource
        ↓
Account service calls = 0
```

```text
Expired acknowledgment challenge
        ↓
Account service calls = 0
```

Then create the successful acknowledgment path.

Do not let acknowledgment override unrelated rules.

For example:

```text
Acknowledgment valid
   +
Legal hold becomes active
   ↓
Decision = Denied
   ↓
Account service calls = 0
```

This proves that acknowledgment satisfies a specific boundary rather than becoming master permission.

---

# Part 9 — Issue Narrow Execution Authority

After the current decision is `Allowed`, create a deliberately small execution capability.

For example:

```csharp
public sealed record ExecutionCapability(
    string CapabilityId,
    string SubjectId,
    string OperationName,
    string ResourceId,
    string Audience,
    DateTimeOffset IssuedUtc,
    DateTimeOffset ExpiresUtc,
    string PolicyId,
    string PolicyVersion,
    string? AcknowledgmentId);
```

Issue it only after the allowed decision.

Bind it to:

```text
Actor
Operation = account.disable
Resource = current account
Audience = account-service-gateway
Short expiration
Policy identity
Acknowledgment when required
```

Do not create:

```text
Scope = account.*
Expires = end of session
Resource = any
Audience = any service
```

Validate the capability immediately before calling the account service.

The validation boundary should answer:

```text
Is this authority valid for this actor,
this operation,
this resource,
this audience,
and this point in time?
```

Add a negative test by deliberately changing one binding after issuance.

Good choices include:

- Wrong resource.
- Wrong actor.
- Wrong audience.
- Expired capability.
- Stale policy version under a strict freshness rule.

The result must preserve:

```text
Capability validation failure
        ↓
Account service calls = 0
```

## Question the Capability Honestly

In a single request, single process, immediately executed endpoint, a separate capability object may be more machinery than the production problem needs.

That is an intentional discussion point.

The exercise uses a capability to make the approval-to-execution handoff visible.

After the lab, ask:

> Would direct host-owned execution after the current allowed decision be simpler and equally safe for this application?

If the answer is yes, the smaller architecture may be preferable.

Capability artifacts become more compelling when approval and execution are separated by time, component, queue, trust boundary, or bounded-use requirements.

---

# Part 10 — Preserve Host-Owned Execution

The governance evaluator still should not perform the side effect.

Keep the final transition in host code or a host-owned gateway:

```text
Current decision = Allowed
   ↓
Scoped capability issued
   ↓
Capability validated
   ↓
Host-owned gateway
   ↓
IAccountService.DisableAsync(...)
```

One possible orchestration sketch is:

```csharp
GovernanceDecision decision =
    policy.Evaluate(context);

await auditSink.WriteDecisionAsync(
    context,
    decision,
    cancellationToken);

if (!decision.CanProceed)
{
    return MapBlockedDecision(decision);
}

ExecutionCapability capability =
    capabilityIssuer.Issue(
        context,
        decision);

CapabilityValidationResult validation =
    capabilityValidator.Validate(
        capability,
        context,
        DateTimeOffset.UtcNow);

if (!validation.IsValid)
{
    await auditSink.WriteExecutionBlockedAsync(
        context,
        validation.Code,
        cancellationToken);

    return Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Execution authority is no longer valid");
}

try
{
    await accountService.DisableAsync(
        context.AccountId,
        context.Intent.Reason,
        cancellationToken);

    await auditSink.WriteExecutionCompletedAsync(
        context,
        cancellationToken);

    return Results.NoContent();
}
catch
{
    await auditSink.WriteExecutionFailedAsync(
        context,
        cancellationToken);

    throw;
}
```

This is a teaching sketch.

The important separation is:

```text
Policy decides
Host coordinates
Account service executes
```

The policy is not the account service.

The capability is not the account service.

The acknowledgment is not the account service.

---

# Part 11 — Record Decision and Execution as Different Evidence

Create a small residue model or recording sink for the exercise.

You need to distinguish at least:

```text
decision
acknowledgment
capability-validation
execution-completed
execution-failed
execution-blocked
```

A small event shape might include:

```csharp
public sealed record GovernanceResidue(
    string EventId,
    string CorrelationId,
    string ActorId,
    string OperationName,
    string ResourceId,
    string Stage,
    string Outcome,
    IReadOnlyList<string> ReasonCodes,
    string PolicyId,
    string PolicyVersion,
    DateTimeOffset OccurredUtc);
```

Do not rewrite an allowed decision as denied merely because execution later failed.

This timeline is valid:

```text
Decision = Allowed
   ↓
Capability validation = Succeeded
   ↓
Execution attempted
   ↓
Account service throws
   ↓
Execution = Failed
```

The evidence should preserve both:

```text
Policy allowed the operation.
```

and:

```text
The side effect did not complete successfully.
```

Add a test in which the recording account service throws after incrementing its invocation count.

The test should confirm:

```text
Decision residue outcome = Allowed
Account service calls = 1
Execution residue outcome = Failed
```

If your application has centralized exception handling, the HTTP result may become a safe `500` Problem Details response.

If it does not, the integration-test host may observe the exception directly.

This lab does not require one specific exception-transport mechanism.

The required invariant is the decision/execution distinction.

---

# Part 12 — Add Integration Tests for the Architectural Invariants

Your final integration-test matrix should include at least these scenarios.

| Scenario | Authorization | Governance | Expected service calls |
| --- | --- | --- | ---: |
| Caller not authorized | Failed | Not reached | 0 |
| Legal hold | Succeeded | `Denied` | 0 |
| Maintenance hold | Succeeded | `Deferred` | 0 |
| Sensitive account, no acknowledgment | Succeeded | `AcknowledgmentRequired` | 0 |
| Protected account | Succeeded | `EscalationRecommended` | 0 |
| Allowed + invalid capability | Succeeded | `Allowed` | 0 |
| Allowed + valid capability | Succeeded | `Allowed` | 1 |
| Allowed + service throws | Succeeded | `Allowed` | 1 |

A test can use a recording service such as:

```csharp
Assert.Equal(0, accountService.DisableCallCount);
```

for every blocked path.

The most important tests are not assertions about returned enum values.

They cross the host boundary and prove whether the side-effect service was invoked.

## Suggested Test Names

Use names that state the invariant directly.

For example:

```text
AuthorizationDenied_DoesNotInvokeAccountService
GovernanceDenied_DoesNotInvokeAccountService
Deferred_DoesNotInvokeAccountService
AcknowledgmentRequiredWithoutAcknowledgment_DoesNotInvokeAccountService
InvalidCapability_DoesNotInvokeAccountService
AllowedWithValidCapability_InvokesAccountServiceOnce
ExecutionFailure_PreservesAllowedDecisionAndRecordsExecutionFailure
```

Avoid tests whose only assertion is:

```text
decision.Outcome == Denied
```

That proves policy evaluation.

It does not prove that the host respected the decision.

---

# Part 13 — Deliberately Break the Boundary

Choose one failure mode and introduce it temporarily.

### Option A — Ignore the Governance Decision

Change the host so it always calls the account service after evaluation.

Your denied/deferred/acknowledgment tests should fail because the service call count becomes nonzero.

### Option B — Treat Acknowledgment as Permission

Skip re-evaluation after a valid acknowledgment.

Then activate a legal hold between challenge issuance and continuation.

A good test should expose the stale decision.

### Option C — Skip Capability Validation

Issue a capability for account A and execute account B.

The invalid-capability test should fail if resource binding is ignored.

### Option D — Move the Side Effect into Policy

Call `IAccountService` from the evaluator when it reaches `Allowed`.

Then ask:

```text
Can the host still inspect or map the decision before execution?
Can a test prove that blocked outcomes never executed?
Can another transport reuse the policy without inheriting side effects?
```

Restore the correct architecture after observing the failure.

The point of deliberately breaking the design is to make the boundary causal rather than ceremonial.

---

# Part 14 — Compare the Final Design with Plain Authorization

Now compare your completed endpoint with the starting endpoint.

The starting design was approximately:

```text
Request
   ↓
Authentication
   ↓
Authorization
   ↓
Account service
```

The governed version is approximately:

```text
Request
   ↓
Authentication
   ↓
Authorization
   ↓
Intent
   ↓
Authoritative context
   ↓
Governance decision
   ├── Denied → host response
   ├── Deferred → host response
   ├── AcknowledgmentRequired → challenge / pause
   ├── EscalationRecommended → alternate workflow
   └── Allowed
          ↓
     Scoped authority
          ↓
     Execution-boundary validation
          ↓
     Host-owned account service
          ↓
     Execution residue
```

Answer these questions:

1. Which requirements genuinely required more than authorization?
2. Which requirements would still be clearer as ASP.NET Core authorization rules?
3. Which governance outcomes represent workflow states rather than access-control failures?
4. Did any caller-controlled field accidentally become authoritative policy context?
5. Can every blocked path prove `Account service calls = 0`?
6. Can an allowed decision be distinguished from execution success?
7. Is acknowledgment bound to the operation, actor, resource, and current policy path?
8. Is execution authority narrower than the user's broad session identity?
9. Is capability machinery justified in your real deployment topology?
10. Could a simpler architecture preserve the same invariants?

The canonical pattern is not a requirement to use every layer everywhere.

The architectural standard is:

> **Use the smallest design that preserves the boundaries the operation actually needs.**

---

# Final Validation

Before completing the lab, verify all of the following.

- Authentication remains a host concern.
- ASP.NET Core authorization still answers an access-control question.
- Authorization failure prevents the governed operation from executing.
- Proposed intent is explicit and performs no side effect.
- Actor, tenant, resource, correlation, and policy identity come from authoritative host sources where appropriate.
- Policy context is a decision-time snapshot rather than a service locator.
- Governance produces explicit `Allowed`, `Denied`, `Deferred`, `AcknowledgmentRequired`, and `EscalationRecommended` outcomes.
- Expected governance outcomes are not represented by throwing exceptions.
- HTTP status codes are chosen by the ASP.NET Core host, not the policy evaluator.
- Acknowledgment is bound and does not become an authorization override.
- Current context is re-evaluated after acknowledgment when the exercise requires it.
- Scoped authority is issued only after the current decision permits execution.
- Scoped authority is validated immediately before the host-owned side effect.
- Every blocked path leaves `IAccountService.DisableAsync` uncalled.
- An allowed path with valid execution authority calls the service exactly once.
- A service failure after approval leaves the original decision as `Allowed` and records execution failure separately.
- Decision evidence and operational execution evidence remain distinguishable.

The final invariant should be mechanically testable:

```text
Blocked or stale path
        ↓
Account service calls = 0
```

and the successful path should be equally explicit:

```text
Authorized
   +
Current governance decision = Allowed
   +
Valid scoped authority
        ↓
Account service calls = 1
```

---

# Completion Criteria

You have completed the lab when you can explain why each statement answers a different question:

```text
The caller is authenticated.
The caller is authorized to request account disablement.
The proposed operation is currently allowed by governance policy.
Any required acknowledgment has been satisfied.
The execution authority is valid for this exact operation and resource.
The host invoked the account service.
The account service completed successfully.
```

You should also be able to explain why this statement is insufficient:

```text
The request returned success, therefore every architectural boundary was correct.
```

A useful architecture makes the intermediate boundaries observable and testable rather than inferring them from the final HTTP status alone.

---

## Related Content

- [Decision Before Execution](../tutorials/decision-before-execution.md) — revisit the boundary between proposed intent, governance decision, and side effect.
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) — review authoritative decision-time facts, explicit outcomes, and reason codes.
- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) — review bound acknowledgment, re-evaluation, correlation, and separate decision/execution evidence.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — review narrow authority and execution-boundary validation.
- [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md) — compare the governed flow with the simpler authorization-only architecture and the hybrid pattern.
- [Centralized Error Handling and Problem Details](../aspnetcore/centralized-error-handling-and-problem-details.md) — optionally reuse the repository's safe host-side response convention for expected governance mapping and unexpected failures.
- [ASP.NET Core learning area](../aspnetcore/index.md) — connect the exercise to middleware, configuration, logging, and error-handling architecture.
- [AsiBackbone/AsiBackbone](https://github.com/AsiBackbone/AsiBackbone) — inspect fuller governance decision, audit-residue, capability, and host-integration concepts after completing the teaching exercise.
- [AsiBackbone/NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate) — compare the disposable lab with a broader ASP.NET Core reference architecture.

---

> **Read it. Run it. Question it. Improve it.**
