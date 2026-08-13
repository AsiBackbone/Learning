# Acknowledgment and Audit Residue

**Learning objective:** Understand how a consequential operation can pause for explicit acknowledgment, resume through a governed boundary, and leave structured evidence explaining what was proposed, decided, acknowledged, and ultimately performed.

This is the third foundational tutorial in ASI Backbone Learning.

It builds on:

1. [Decision Before Execution](decision-before-execution.md)
2. [Policy Context and Explicit Decision Outcomes](policy-context-and-explicit-decision-outcomes.md)

The first tutorial separated a proposal from execution.

The second made the decision inputs and outcomes explicit.

This tutorial adds two more pieces:

```text
Intent
   ↓
Context
   ↓
Constraints
   ↓
Decision
   ↓
Acknowledgment when required
   ↓
Host-owned continuation
   ↓
Audit residue
```

The core ideas are:

> **Acknowledgment is a governance boundary, not an execution bypass.**

and:

> **Audit residue should explain the governed path without pretending that an ordinary log line is durable proof.**

## The Problem

Some operations should not proceed immediately even when they are not permanently denied.

Examples include:

- Deleting a large set of records.
- Releasing sensitive information.
- Executing an unusual administrative action.
- Approving a high-cost operation.
- Allowing an AI-generated tool action with meaningful consequences.
- Performing an operation whose risk must be explicitly accepted.
- Continuing after a policy warning that requires human responsibility.

A simple `Allowed` or `Denied` result is not enough.

The system may instead need to say:

> The operation may continue only after a specific actor explicitly acknowledges a specific condition.

That creates a new lifecycle:

```text
Decision
   ↓
AcknowledgmentRequired
   ↓
Pause
   ↓
Present challenge
   ↓
Actor accepts or rejects
   ↓
Host validates response
   ↓
Governed continuation or stop
```

At the same time, the system may need to answer later:

- What operation was proposed?
- Which policy required acknowledgment?
- What exactly was presented?
- Who responded?
- Did they accept or reject?
- When did the response occur?
- Did execution happen afterward?
- Which correlation identifier connects these events?

Those questions motivate structured audit residue.

## A Naive Confirmation Dialog

A first implementation may look like this:

```csharp
if (request.IsHighRisk)
{
    return Results.Conflict(new
    {
        message = "Are you sure?"
    });
}
```

The client later sends:

```json
{
  "confirmed": true
}
```

and the server executes:

```csharp
if (request.Confirmed)
{
    await operation.ExecuteAsync(cancellationToken);
}
```

This may provide a user-interface confirmation, but several governance questions remain unanswered:

- What exactly was acknowledged?
- Does `confirmed = true` belong to the same operation?
- Which actor confirmed it?
- Was the original decision still valid?
- Did the policy or resource state change between challenge and response?
- Can the response be replayed for a different operation?
- Is there a stable acknowledgment identifier?
- Is there evidence connecting the decision, acknowledgment, and later execution?

The problem is not the confirmation dialog itself.

The problem is treating an unbound boolean as sufficient evidence and authority.

## Acknowledgment Is Not Permission

An important distinction is:

```text
Acknowledgment
≠
Authorization
≠
Execution authority
```

An acknowledgment means something closer to:

> An identified actor responded to a defined acknowledgment challenge.

It does **not** necessarily mean:

> Execute the operation immediately.

The host may still need to:

- Verify the actor.
- Verify the challenge identifier.
- Verify the acknowledgment code.
- Verify that the response has not expired.
- Re-evaluate policy.
- Verify that relevant context has not materially changed.
- Issue a scoped capability.
- Validate execution-specific constraints.

This is especially important when the operation is delayed between the original decision and the response.

## Model the Challenge Explicitly

Suppose an account-disable policy returns:

```text
AcknowledgmentRequired
```

Instead of returning only a message string, the host can construct a challenge:

```csharp
public sealed record AcknowledgmentChallenge(
    string ChallengeId,
    string ActorId,
    string OperationName,
    string ResourceId,
    string ReasonCode,
    string Message,
    string RequiredAcknowledgmentCode,
    string RequiredAcknowledgmentText,
    string CorrelationId,
    string PolicyVersion,
    DateTimeOffset ExpiresUtc);
```

A challenge might contain:

```text
ChallengeId:
ack-8f14c2

ActorId:
admin-42

Operation:
account.disable

Resource:
user-123

ReasonCode:
account.disable.reason-required

RequiredAcknowledgmentCode:
account.disable.accept-responsibility

RequiredAcknowledgmentText:
I acknowledge that disabling this account may interrupt active access.

CorrelationId:
req-7d91

PolicyVersion:
3.2

ExpiresUtc:
2026-08-12T20:00:00Z
```

Now the system has a specific object that can be presented, persisted, validated, and correlated.

## Bind the Challenge to the Operation

A useful acknowledgment challenge should be narrow.

Avoid a challenge such as:

```text
"I accept the risks."
```

with no connection to the operation.

Prefer a challenge that is bound to:

```text
Actor
Operation
Resource
Reason
Policy identity
Correlation
Expiration
```

The goal is to prevent a generic acknowledgment from becoming reusable standing permission.

A challenge should answer:

> What, specifically, is this actor acknowledging?

## Model the Response Explicitly

The actor's response should also be represented as data:

```csharp
public sealed record AcknowledgmentResponse(
    string AcknowledgmentId,
    string ChallengeId,
    string ActorId,
    string AcknowledgmentCode,
    bool Accepted,
    DateTimeOffset OccurredUtc,
    string CorrelationId);
```

Accepted:

```csharp
var response = new AcknowledgmentResponse(
    AcknowledgmentId: Guid.NewGuid().ToString("N"),
    ChallengeId: challenge.ChallengeId,
    ActorId: actor.Id,
    AcknowledgmentCode:
        challenge.RequiredAcknowledgmentCode,
    Accepted: true,
    OccurredUtc: DateTimeOffset.UtcNow,
    CorrelationId: challenge.CorrelationId);
```

Rejected:

```csharp
var response = new AcknowledgmentResponse(
    AcknowledgmentId: Guid.NewGuid().ToString("N"),
    ChallengeId: challenge.ChallengeId,
    ActorId: actor.Id,
    AcknowledgmentCode:
        challenge.RequiredAcknowledgmentCode,
    Accepted: false,
    OccurredUtc: DateTimeOffset.UtcNow,
    CorrelationId: challenge.CorrelationId);
```

The response says what happened.

It does not execute the operation.

## Validate the Response

A minimal validator might look like this:

```csharp
public sealed class AcknowledgmentValidator
{
    public bool IsValid(
        AcknowledgmentChallenge challenge,
        AcknowledgmentResponse response,
        DateTimeOffset nowUtc)
    {
        if (!response.Accepted)
        {
            return false;
        }

        if (!string.Equals(
                challenge.ChallengeId,
                response.ChallengeId,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(
                challenge.ActorId,
                response.ActorId,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(
                challenge.RequiredAcknowledgmentCode,
                response.AcknowledgmentCode,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(
                challenge.CorrelationId,
                response.CorrelationId,
                StringComparison.Ordinal))
        {
            return false;
        }

        return nowUtc <= challenge.ExpiresUtc;
    }
}
```

This is intentionally small.

A production system may also validate:

- Cryptographic binding.
- Nonce or replay state.
- Session identity.
- Tenant or organization.
- Operation hash.
- Resource version.
- Policy hash.
- Challenge consumption state.
- Maximum acknowledgment age.

The architectural lesson is the same:

```text
Response received
   ↓
Response validated
   ↓
Only then may continuation be considered
```

## Re-Evaluate When Context Can Drift

Acknowledgment may occur seconds, minutes, or hours after the original decision.

During that time:

- The resource may change.
- The actor may lose a role.
- A maintenance hold may activate.
- The policy version may change.
- The operation may no longer be valid.
- The resource may already have been modified.

Therefore, avoid treating an old acknowledgment-required decision as permanently authoritative.

A safer conceptual flow is:

```text
Original evaluation
   ↓
AcknowledgmentRequired
   ↓
Challenge
   ↓
Acknowledgment
   ↓
Validate response
   ↓
Reconstruct current context
   ↓
Re-evaluate when required
   ↓
Current decision
   ↓
Host-controlled continuation
```

Whether re-evaluation is mandatory depends on the system and risk model.

What matters is that the architecture makes the choice explicit.

## Acknowledgment Is Not a Policy Override

Avoid:

```csharp
if (response.Accepted)
{
    return GovernanceDecision.Allow();
}
```

if that bypasses all policy.

That turns acknowledgment into a master override.

Instead, acknowledgment should satisfy a specific requirement.

For example:

```csharp
public sealed record DisableAccountPolicyContext(
    DisableAccountIntent Intent,
    ActorContext Actor,
    AccountContext Account,
    EnvironmentContext Environment,
    bool RequiredAcknowledgmentSatisfied,
    string CorrelationId,
    string PolicyVersion);
```

Then the policy can distinguish:

```csharp
if (string.IsNullOrWhiteSpace(context.Intent.Reason) &&
    !context.RequiredAcknowledgmentSatisfied)
{
    return GovernanceDecision.RequireAcknowledgment(
        "account.disable.reason-required",
        "A reason must be acknowledged before proceeding.");
}
```

After valid acknowledgment, the host reconstructs context with:

```text
RequiredAcknowledgmentSatisfied = true
```

and evaluates again.

Other constraints still apply.

A protected account may still require escalation.

A cross-tenant operation may still be denied.

A maintenance hold may still defer the action.

Acknowledgment satisfies one boundary; it does not erase the rest.

## Record the Decision Path

Now consider what should remain after the workflow.

A single log line such as:

```text
User disabled account.
```

does not explain the governed path.

A more useful residue might capture:

```text
EventId
OccurredUtc
ActorId
OperationName
Outcome
ReasonCodes
CorrelationId
PolicyVersion
PolicyHash
DecisionStage
```

A minimal educational model:

```csharp
public sealed record AuditResidue(
    string EventId,
    DateTimeOffset OccurredUtc,
    string ActorId,
    string OperationName,
    string Outcome,
    IReadOnlyList<string> ReasonCodes,
    string CorrelationId,
    string PolicyVersion,
    string? PolicyHash,
    string DecisionStage);
```

Examples of stages:

```text
decision
challenge-issued
acknowledgment-accepted
acknowledgment-rejected
re-evaluation
execution-started
execution-completed
execution-failed
```

A single operation can therefore leave multiple related residues.

## Think in a Timeline

For an acknowledgment-required operation:

```text
T1  Intent received
 |
T2  Decision = AcknowledgmentRequired
 |
T3  Challenge issued
 |
T4  Actor accepts challenge
 |
T5  Policy re-evaluated
 |
T6  Decision = Allowed
 |
T7  Host executes operation
 |
T8  Execution completed
```

Each event can share:

```text
CorrelationId = req-7d91
```

This creates a navigable governance timeline without forcing every fact into one giant record.

## Decision Residue

A helper might create residue from a decision:

```csharp
public static AuditResidue FromDecision(
    string actorId,
    string operationName,
    GovernanceDecision decision,
    string correlationId,
    string policyVersion,
    string? policyHash,
    string stage)
{
    return new AuditResidue(
        EventId: Guid.NewGuid().ToString("N"),
        OccurredUtc: DateTimeOffset.UtcNow,
        ActorId: actorId,
        OperationName: operationName,
        Outcome: decision.Outcome.ToString(),
        ReasonCodes:
            decision.Reasons
                .Select(reason => reason.Code)
                .ToArray(),
        CorrelationId: correlationId,
        PolicyVersion: policyVersion,
        PolicyHash: policyHash,
        DecisionStage: stage);
}
```

The host can create a decision residue before execution:

```csharp
AuditResidue residue =
    AuditResidueFactory.FromDecision(
        actor.Id,
        "account.disable",
        decision,
        context.CorrelationId,
        context.PolicyVersion,
        policyHash: null,
        stage: "decision");
```

## Acknowledgment Residue

The acknowledgment itself can produce a separate event:

```csharp
public static AuditResidue FromAcknowledgment(
    AcknowledgmentChallenge challenge,
    AcknowledgmentResponse response)
{
    return new AuditResidue(
        EventId: response.AcknowledgmentId,
        OccurredUtc: response.OccurredUtc,
        ActorId: response.ActorId,
        OperationName: challenge.OperationName,
        Outcome:
            response.Accepted
                ? "AcknowledgmentAccepted"
                : "AcknowledgmentRejected",
        ReasonCodes:
        [
            challenge.ReasonCode,
            challenge.RequiredAcknowledgmentCode
        ],
        CorrelationId: challenge.CorrelationId,
        PolicyVersion: challenge.PolicyVersion,
        PolicyHash: null,
        DecisionStage: "acknowledgment");
}
```

This allows the system to distinguish:

```text
Policy required acknowledgment
```

from:

```text
Actor accepted acknowledgment
```

and from:

```text
Host later executed operation
```

Those are different events.

## Execution Residue

After the host operation:

```csharp
AuditResidue completed =
    new(
        EventId: Guid.NewGuid().ToString("N"),
        OccurredUtc: DateTimeOffset.UtcNow,
        ActorId: actor.Id,
        OperationName: "account.disable",
        Outcome: "Executed",
        ReasonCodes: [],
        CorrelationId: context.CorrelationId,
        PolicyVersion: context.PolicyVersion,
        PolicyHash: null,
        DecisionStage: "execution-completed");
```

If execution fails:

```csharp
AuditResidue failed =
    new(
        EventId: Guid.NewGuid().ToString("N"),
        OccurredUtc: DateTimeOffset.UtcNow,
        ActorId: actor.Id,
        OperationName: "account.disable",
        Outcome: "ExecutionFailed",
        ReasonCodes:
        [
            "account.disable.execution-failed"
        ],
        CorrelationId: context.CorrelationId,
        PolicyVersion: context.PolicyVersion,
        PolicyHash: null,
        DecisionStage: "execution-failed");
```

The decision and execution are now distinguishable in the evidence.

That matters because:

```text
Allowed
```

does not mean:

```text
Executed successfully
```

## Logging and Audit Residue Are Different

Operational logging asks questions such as:

- What is the application doing?
- Why is this request slow?
- Which exception occurred?
- Which dependency failed?

Audit residue asks questions such as:

- What consequential operation was proposed?
- What governance outcome was produced?
- Which reason codes applied?
- Was acknowledgment required?
- Who acknowledged?
- What execution state followed?

The two can share data, but they should not be treated as identical.

Consider:

```csharp
logger.LogInformation(
    "Account disable acknowledged.");
```

That log line may be useful.

But by itself it may not provide:

```text
ChallengeId
AcknowledgmentId
ActorId
Operation
ReasonCode
PolicyVersion
CorrelationId
Timestamp
Accepted / Rejected
```

Structured audit residue gives those concepts a deliberate shape.

## Audit Residue Is Not Automatically Tamper-Proof

This boundary is important.

Creating:

```csharp
new AuditResidue(...)
```

does not automatically create:

- An immutable ledger.
- Cryptographic proof.
- Non-repudiation.
- Durable storage.
- Tamper-evident history.
- Regulatory compliance.

Those properties depend on additional architecture such as:

- Durable persistence.
- Append-only storage controls.
- Signing.
- Hash chaining.
- Key management.
- Retention policy.
- Access controls.
- External emission.
- Outbox delivery.
- Independent verification.

Use precise language.

A structured audit record is evidence-oriented data.

Its durability and tamper properties depend on how the host stores and protects it.

## Persist Before You Depend on It

If an audit event matters to governance, avoid treating best-effort logging as its only destination.

For example:

```text
Decision created
   ↓
Send directly to remote telemetry
   ↓
Network unavailable
   ↓
Evidence lost
```

A stronger production pattern may be:

```text
Decision created
   ↓
Persist locally / durable outbox
   ↓
Commit
   ↓
Emit externally
   ↓
Mark delivery status
```

This is an implementation concern beyond the minimal tutorial, but it illustrates an important distinction:

```text
Create residue
≠
Persist residue
≠
Deliver residue
```

Those are separate responsibilities.

## Keep Audit Data Purposeful

Audit residue should not become a dumping ground.

Avoid copying:

```text
Entire request body
Full authentication token
Raw secrets
Sensitive personal data
Complete model prompt
Full database entity
```

simply because the data is available.

Prefer:

- Stable identifiers.
- Reason codes.
- Policy identity.
- Correlation identifiers.
- Privacy-preserving hashes when appropriate.
- Minimal metadata needed to explain the decision.

The evidence should be useful without unnecessarily increasing privacy and security risk.

## Correlation Creates the Story

A correlation identifier connects events without requiring one giant object.

For example:

```text
CorrelationId: req-7d91

Event 1:
Outcome = AcknowledgmentRequired

Event 2:
Outcome = AcknowledgmentAccepted

Event 3:
Outcome = Allowed

Event 4:
Outcome = Executed
```

This can answer:

> Which execution followed this acknowledgment?

or:

> Which acknowledgment satisfied this decision?

The correlation identifier should be stable across the governed flow.

Trace and span identifiers can add observability detail, but correlation remains useful even outside distributed tracing.

## Reason Codes Create Explainability

Human messages change.

Reason codes should remain stable where practical.

Example:

```text
account.disable.protected-account
account.disable.reason-required
acknowledgment.rejected
execution.failed
```

These codes support:

- Search.
- Metrics.
- Policy testing.
- Automated routing.
- Incident analysis.
- Reporting.

The message can then remain readable:

```text
A reason must be acknowledged before proceeding.
```

Do not require downstream systems to parse that sentence to understand the event.

## Test the Acknowledgment Boundary

A good test should prove more than:

```text
Acknowledgment = true
```

Test the binding.

Example:

```csharp
[Fact]
public void WrongActor_DoesNotSatisfyChallenge()
{
    var challenge = new AcknowledgmentChallenge(
        ChallengeId: "challenge-1",
        ActorId: "admin-1",
        OperationName: "account.disable",
        ResourceId: "user-123",
        ReasonCode: "account.disable.reason-required",
        Message: "Acknowledgment required.",
        RequiredAcknowledgmentCode:
            "account.disable.accept-responsibility",
        RequiredAcknowledgmentText:
            "I acknowledge the operation.",
        CorrelationId: "corr-1",
        PolicyVersion: "3.2",
        ExpiresUtc:
            DateTimeOffset.UtcNow.AddMinutes(5));

    var response = new AcknowledgmentResponse(
        AcknowledgmentId: "ack-1",
        ChallengeId: "challenge-1",
        ActorId: "admin-2",
        AcknowledgmentCode:
            "account.disable.accept-responsibility",
        Accepted: true,
        OccurredUtc: DateTimeOffset.UtcNow,
        CorrelationId: "corr-1");

    bool valid =
        new AcknowledgmentValidator()
            .IsValid(
                challenge,
                response,
                DateTimeOffset.UtcNow);

    Assert.False(valid);
}
```

Also test:

- Wrong challenge ID.
- Wrong acknowledgment code.
- Rejected acknowledgment.
- Expired challenge.
- Wrong correlation ID.
- Successful acknowledgment.

## Test That Acknowledgment Does Not Bypass Policy

Suppose the operation was acknowledgment-required because a reason was missing.

After valid acknowledgment, the resource becomes protected.

A re-evaluation should still return:

```text
EscalationRecommended
```

not:

```text
Allowed
```

That test proves:

> **Acknowledgment satisfies a requirement; it does not suppress unrelated constraints.**

## Test the Audit Timeline

A useful test can verify that a successful acknowledgment flow produces stages in the expected order:

```text
decision
acknowledgment
re-evaluation
execution-completed
```

Do not overfit production systems to this exact vocabulary.

The learning goal is to verify that evidence reflects the lifecycle instead of only the final side effect.

## Common Failure Modes

### 1. Generic "Confirmed" Boolean

```csharp
if (request.Confirmed)
{
    Execute();
}
```

The confirmation is not bound to a specific challenge, actor, or operation.

### 2. Acknowledgment Becomes Permanent Permission

An old acknowledgment is reused for future operations.

Acknowledgment should normally be scoped narrowly enough that this cannot happen accidentally.

### 3. Acknowledgment Bypasses Policy

```csharp
if (acknowledged)
{
    Execute();
}
```

No re-validation or policy continuation occurs.

### 4. The Wrong Actor Can Acknowledge

The challenge was issued for one actor, but the server accepts a response from another.

Actor binding must be explicit when identity matters.

### 5. Challenge Text Changes Without Identity

The host accepts a generic `"yes"` even though the displayed condition changed.

Use a stable acknowledgment code and challenge identifier.

### 6. Decision and Execution Are Recorded as One Event

The audit record says:

```text
Allowed and executed.
```

but execution actually failed.

Keep decision and execution state distinguishable.

### 7. Audit Evidence Exists Only in Console Logs

A production workflow depends on evidence that may disappear with process restarts, log rotation, or remote telemetry failure.

Durability requires a persistence design.

### 8. Audit Residue Stores Too Much Sensitive Data

Evidence becomes a secondary data breach surface.

Collect intentionally.

### 9. Audit Records Are Called Immutable Without Enforcement

A database row that administrators can freely update is not immutable simply because it is named "audit."

Use language that matches the actual storage and signing guarantees.

### 10. Correlation Is Lost Between Stages

The decision, acknowledgment, and execution cannot be connected later.

Carry stable correlation through the workflow.

## Tradeoffs

### Benefits

- Human or system responsibility becomes explicit.
- High-consequence operations can pause without being permanently denied.
- Challenges can be bound to actors, operations, and reasons.
- Rejected acknowledgments become first-class events.
- Policy can continue after acknowledgment rather than being bypassed.
- Decision, acknowledgment, and execution can be distinguished.
- Structured evidence improves investigation and review.
- Correlation provides a coherent lifecycle.
- AI-proposed actions can require explicit host or human acknowledgment.

### Costs

- Multi-step workflows require state.
- Challenge persistence may be necessary.
- Expiration and replay behavior must be defined.
- Context may need to be reconstructed and re-evaluated.
- Durable audit storage adds operational complexity.
- Evidence schemas require versioning.
- Retention and privacy policies become important.
- Poorly designed acknowledgment can create "click-through" ceremony without meaningful governance value.

An acknowledgment step is useful only if it represents a real decision boundary.

## Relationship to AsiBackbone

This tutorial is framework-neutral, but the working `AsiBackbone` repository contains richer versions of these concepts.

Useful references include:

- [`LiabilityHandshakeRequest`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Handshakes/LiabilityHandshakeRequest.cs) — a framework-neutral request containing handshake identity, actor, operation, reason, required acknowledgment code/text, risk information, correlation, trace, and policy identity.
- [`LiabilityHandshakeAcknowledgment`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Handshakes/LiabilityHandshakeAcknowledgment.cs) — an explicit accepted or rejected response linked to the handshake and actor.
- [`AuditResidue`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Audit/AuditResidue.cs) — structured governance evidence including actor, operation, outcome, reason codes, correlation/trace data, policy identity, decision-stage data, and optional observability metadata.
- [`DefaultAsiBackboneAcknowledgmentChallengeService`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.AspNetCore/Handshakes/DefaultAsiBackboneAcknowledgmentChallengeService.cs) — ASP.NET Core integration for acknowledgment challenge handling.
- [`Dynamic Liability Handshake`](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/dynamic-liability-handshake.md) — fuller documentation of the handshake pattern.
- [`Durable Audit Outbox Persistence`](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/durable-audit-outbox-persistence.md) — production-oriented persistence and delivery considerations for audit evidence.

The production framework carries considerably more metadata than the teaching model because it supports broader integration, persistence, observability, and governance scenarios.

Learning keeps the example smaller so the lifecycle remains visible.

## Apply the Pattern to AI

Consider an AI assistant that proposes:

```text
Delete 2,000 archived customer records.
```

The model may have produced a valid tool call.

That does not mean the tool should execute.

A governed flow can be:

```text
User request
   ↓
Model proposes delete operation
   ↓
Host constructs authoritative context
   ↓
Decision = AcknowledgmentRequired
   ↓
Host presents:
"2,000 records will be deleted."
   ↓
Authorized actor accepts or rejects
   ↓
Host validates acknowledgment
   ↓
Host re-evaluates current context
   ↓
Current decision
   ↓
Host-controlled execution
   ↓
Audit residue
```

The evidence can distinguish:

```text
AI proposed action
Human acknowledged risk
Policy allowed current operation
Host executed tool
```

Those are four different responsibilities.

> **The model may propose. The host retains execution authority.**

## Exercise

Extend the account-disable workflow from the first two tutorials.

Add:

```text
AcknowledgmentChallenge
AcknowledgmentResponse
AuditResidue
```

Implement this flow:

```text
Request
   ↓
Intent
   ↓
Context
   ↓
Decision = AcknowledgmentRequired
   ↓
Challenge issued
   ↓
Actor accepts
   ↓
Response validated
   ↓
Policy re-evaluated
   ↓
Decision = Allowed
   ↓
Executor invoked
   ↓
Execution residue created
```

Write tests proving:

1. A rejected acknowledgment never reaches execution.
2. An acknowledgment from the wrong actor is invalid.
3. An expired challenge is invalid.
4. A valid acknowledgment satisfies only the intended requirement.
5. A newly introduced denial still blocks execution after acknowledgment.
6. Decision, acknowledgment, and execution residues share the same correlation identifier.
7. An allowed decision and a successful execution are recorded as distinct states.

For additional practice, persist challenge state and audit residue using an in-memory repository abstraction.

Then simulate a process restart and ask:

> Which parts of the workflow survive?

That question leads naturally toward durable persistence and scoped execution authority.

## Review Questions

Before moving on, you should be able to answer:

1. Why is acknowledgment different from authorization?
2. Why should an acknowledgment be bound to a specific challenge?
3. Why is a generic `confirmed = true` weaker than a structured acknowledgment response?
4. Why might policy need to be re-evaluated after acknowledgment?
5. Why should acknowledgment satisfy a requirement rather than bypass all policy?
6. What is the difference between a decision event and an execution event?
7. What does audit residue provide that an ordinary log message may not?
8. Why is structured audit residue not automatically immutable or tamper-proof?
9. Why should correlation identifiers be preserved across decision, acknowledgment, and execution?
10. Why should sensitive data be minimized in governance evidence?
11. How does acknowledgment apply to AI-proposed tool calls?
12. What additional responsibilities appear when acknowledgment state must survive process restarts?

## Next

The next foundational topic is **Scoped Capability and Host-Owned Execution**.

Acknowledgment answers:

> Has the required responsibility boundary been satisfied?

It does not necessarily answer:

> What exact authority should now exist, for which operation, resource, and period of time?

The next tutorial introduces scoped capability as a way to represent narrow execution authority:

```text
Decision
   ↓
Acknowledgment when required
   ↓
Scoped capability
   ↓
Host validates capability
   ↓
Host-owned execution
```

This continues the same principle established throughout Learning:

> **Approval should not silently become broad or permanent authority.**

## Related Content

- [Foundational Tutorial Index](index.md) — view the complete five-tutorial learning path.
- [Policy Context and Explicit Decision Outcomes](policy-context-and-explicit-decision-outcomes.md) — revisit the explicit decision inputs and outcomes that can lead to an acknowledgment requirement.
- [Scoped Capability and Host-Owned Execution](scoped-capability-and-host-owned-execution.md) — continue from acknowledgment into narrow, short-lived execution authority.
- [Governed AI Tool Gateway](governed-ai-tool-gateway.md) — see acknowledgment, capability, execution, and audit residue composed around AI-proposed actions.
- [Executable Samples](https://github.com/AsiBackbone/Learning/tree/main/samples) — explore runnable companion material as the sample set develops.
- [Hands-On Labs](../labs/index.md) — practice acknowledgment, evidence, and governed-continuation boundaries through hands-on exercises.

---

> **Read it. Run it. Question it. Improve it.**
