---
description: Compare ordinary application-service orchestration with governed execution and learn when additional governance boundaries are justified.
title: When a Simple Application Service Is Enough
author: Christopher D. Cavell
published: 2026-08-19
summary: Use a broader governed-execution lifecycle only when the problem needs boundaries beyond ordinary application orchestration.
feed: true
---

# When a Simple Application Service Is Enough

**Pattern classification:** Alternative Pattern

**Difficulty:** Intermediate

**Prerequisites:** [When ASP.NET Core Authorization Is Enough](when-aspnet-core-authorization-is-enough.md) and [Decision Before Execution](../tutorials/decision-before-execution.md). Familiarity with [Data Access Boundaries and Transaction Reasoning](../aspnetcore/data-access-boundaries-and-transaction-reasoning.md) is helpful.

> **Terminology note:** Learning uses terms such as governed execution, host-owned execution, policy context, scoped authority, and audit residue as teaching labels for architectural boundaries. See [Terminology and Established Architecture Concepts](terminology-and-established-concepts.md) for their relationship to established authorization, application-service, workflow, capability, provenance, and mediation concepts.

A broader governed-execution pipeline is not automatically the best architecture for every mutation.

Many operations are clearer when they remain an ordinary application workflow:

```text
Authenticated actor
        ↓
Authorized operation
        ↓
Validate request
        ↓
Application service
        ↓
Load authoritative resource
        ↓
Apply domain rule
        ↓
Execute mutation
        ↓
Persist
```

That design can still be secure, testable, observable, and host-owned.

It simply does not introduce lifecycle boundaries that the operation does not need.

This comparison asks:

> **When is an ordinary application-service workflow enough, and when do requirements justify a broader governed-execution lifecycle?**

The answer is not based on whether an operation is called `CRUD`, whether the application uses ASP.NET Core, or whether the ASI Backbone package is available.

The useful threshold is whether the operation needs **independent decision, continuation-authority, acknowledgment, mediation, or provenance boundaries** that survive beyond an ordinary application-service call.

---

## This Is a Different Question from Authorization

[When ASP.NET Core Authorization Is Enough](when-aspnet-core-authorization-is-enough.md) asks whether built-in authorization policies, handlers, and resource-based authorization fully express the access-control requirement.

This page begins one step later.

Suppose the application has already answered:

```text
May this actor enter this operation?
```

The next question may still be simple:

```text
Can the application validate current state,
apply the business rule,
and execute the mutation now?
```

If yes, an application service may be the correct boundary.

A developer can therefore reach two conclusions at the same time:

```text
Authorization alone is not the whole use case.
```

and:

```text
A full governance lifecycle is still unnecessary.
```

That middle ground matters.

## The Simple Application-Service Boundary

An application service coordinates a use case inside the application's trusted execution environment.

Depending on the application, it may own or coordinate:

- Request-independent use-case logic.
- Authoritative resource loading.
- Domain-rule evaluation.
- Persistence.
- Local transaction boundaries.
- Calls to application-owned infrastructure abstractions.
- Expected application result types.
- Ordinary audit or operational events.

It does not need to become a policy engine, workflow engine, capability issuer, or durable decision ledger merely because it performs consequential work.

A common shape is:

```text
HTTP endpoint
   ↓
Authentication / authorization
   ↓
Request validation
   ↓
Application service
   ├── load current state
   ├── enforce use-case/domain rules
   ├── perform mutation
   └── persist atomically where possible
   ↓
Application result
   ↓
HTTP result mapping
```

The application service is still part of the host-owned execution boundary.

The important property is not the number of layers.

It is that untrusted input does not silently become a side effect without the checks the use case actually requires.

---

## Example Where the Simpler Design Clearly Wins

Consider an internal case-management application with an operation:

```text
case.archive-draft
```

The requirements are:

- The caller must be authenticated.
- The caller must have permission to edit the case.
- The request must include a valid case identifier.
- The current case must be loaded from the application's authoritative store.
- Only a draft case may be archived.
- Archiving is immediately persisted in the same application database.
- The operation is reversible through an ordinary restore action.
- No human approval or acknowledgment is required.
- No worker executes the action later.
- No separate component needs post-approval authority.
- Normal application audit records are sufficient.

The lifecycle is short:

```text
Request
   ↓
Authorize
   ↓
Validate
   ↓
Load current case
   ↓
Case is still a draft?
   ├── no  → return expected conflict/result
   └── yes → archive
             ↓
          save transaction
             ↓
          return success
```

A compact application-service result might be enough:

```csharp
public enum ArchiveCaseOutcome
{
    Succeeded,
    NotFound,
    NotPermitted,
    NotDraft
}

public sealed record ArchiveCaseResult(
    ArchiveCaseOutcome Outcome,
    string? ReasonCode = null);
```

That result type does **not** automatically imply a governance decision model.

It is simply an application contract that makes expected use-case outcomes explicit.

A service can remain small:

```csharp
public sealed class ArchiveCaseService(
    ICaseRepository cases,
    IUnitOfWork unitOfWork)
{
    public async Task<ArchiveCaseResult> ArchiveAsync(
        ArchiveCaseCommand command,
        CancellationToken cancellationToken)
    {
        CaseFile? caseFile = await cases.FindAsync(
            command.CaseId,
            cancellationToken);

        if (caseFile is null)
        {
            return new(ArchiveCaseOutcome.NotFound);
        }

        if (!caseFile.CanBeEditedBy(command.ActorId))
        {
            return new(
                ArchiveCaseOutcome.NotPermitted,
                "case.archive.not-permitted");
        }

        if (!caseFile.IsDraft)
        {
            return new(
                ArchiveCaseOutcome.NotDraft,
                "case.archive.not-draft");
        }

        caseFile.Archive(command.ActorId, command.OccurredUtc);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new(ArchiveCaseOutcome.Succeeded);
    }
}
```

A production application may place resource authorization in ASP.NET Core's `IAuthorizationService`, in a domain/application permission abstraction, or in another established authorization boundary.

That placement choice does not change the central comparison.

The important point is that the operation is decided and executed inside one immediate use-case boundary.

Adding all of the following would create little value for these requirements:

```text
ArchiveCaseIntent
        ↓
PolicyContext
        ↓
Constraint pipeline
        ↓
GovernanceDecision
        ↓
Capability issuance
        ↓
Capability validation
        ↓
Replay store
        ↓
Decision provenance ledger
        ↓
ArchiveCaseExecutor
```

The additional objects are not wrong in isolation.

They are unnecessary if no requirement needs the boundaries they create.

> **A boundary should earn its place by protecting a real lifecycle distinction.**

---

## What the Simple Design Still Needs

Choosing an application service does not mean choosing weak architecture.

The simple path may still need strong controls.

### Authentication and Authorization

The host still needs to establish who the caller is and whether the caller may enter the use case or act on the resource.

Use framework-native authorization when it expresses the requirement well.

### Input Validation

Malformed or structurally invalid requests should not reach business execution merely because the endpoint was authorized.

### Authoritative State

Security-sensitive or business-critical state should come from the application's trusted sources rather than from caller assertions.

For example:

```text
Caller says: case is draft
        ≠
Repository proves: current case is draft
```

### Domain Rules

Business invariants should remain explicit and testable.

An application service may coordinate them while the domain model owns the invariant itself.

### Transaction Reasoning

If the use case changes several local records, the application still needs a deliberate transaction boundary.

The absence of a governance pipeline does not remove consistency requirements.

### Error and Result Mapping

Expected outcomes should normally remain explicit results.

Unexpected failures should cross to the application's normal exception/error boundary rather than being mislabeled as governance denial.

### Audit and Observability

The operation may still emit:

```text
actor
operation
resource
result
correlation identifier
timestamp
```

Normal application audit or structured logging can be sufficient when the organization does not need to reconstruct a separate historical policy decision.

The simple architecture is therefore not:

```text
Controller calls database and hopes for the best.
```

It is:

```text
Use only the boundaries the use case actually needs.
```

---

## When the Simple Application Service Is Preferable

Prefer the simpler application-service approach when most of the following are true.

### 1. Execution Is Immediate

The decision and side effect belong to one request or one tightly coupled application call.

```text
Validate current state
   ↓
Execute now
```

There is no meaningful pause during which authority must survive independently.

### 2. One Trusted Application Boundary Owns the Use Case

The endpoint, application service, persistence boundary, and executor are controlled by the same application trust domain.

No separate worker, remote gateway, model, or external actor needs a portable authorization artifact.

### 3. Ordinary Authorization Expresses Actor/Resource Access

Roles, claims, policies, resource-based authorization, or an existing application permission model answer who may invoke the operation.

There is no need to create a second authorization vocabulary merely to express the same access rule.

### 4. Validation and Business Rules Are Local

The decision depends on current application state and rules that the service or domain model can evaluate directly.

For example:

```text
Resource exists
Resource belongs to tenant
Resource is in editable state
Requested transition is valid
```

Those rules do not require a separate policy-composition lifecycle.

### 5. Success and Ordinary Application Outcomes Are Enough

The service may need results such as:

```text
Succeeded
NotFound
Conflict
ValidationFailed
NotPermitted
```

It does not need workflow states such as:

```text
AcknowledgmentRequired
EscalationRecommended
DeferredUntilPolicyAvailable
```

### 6. No Delayed Approval or Acknowledgment Exists

The application does not pause, collect a response, re-evaluate, and resume later.

### 7. No Independent Continuation Authority Is Required

A later component does not need to receive a narrowly scoped artifact proving which exact operation it may execute.

### 8. Normal Audit Is Sufficient

Operators need to know who changed what and whether the operation succeeded.

They do not need a durable record of:

```text
policy version
policy hash
constraint set
acknowledgment identity
capability identity
consumption history
```

### 9. The Operation Is Low to Moderate Consequence

Failure matters, but the operation does not justify an additional approval or mediation lifecycle.

Consequence alone is not a mathematical threshold, but it affects how much architectural ceremony can be justified.

### 10. Historical Policy Reconstruction Adds Little Value

If someone asks six months later:

> Why was this archive allowed?

an answer such as:

```text
Actor had edit permission.
Case was a draft.
Archive request passed validation.
Database transaction committed.
```

may be enough.

If that answer is sufficient, a durable policy-decision record may be unnecessary.

---

## A Governance Pipeline Adds Different Boundaries

A broader governed-execution design introduces a different lifecycle:

```text
Intent
   ↓
Policy context
   ↓
Constraints
   ↓
Governance decision
   ↓
Acknowledgment / escalation when required
   ↓
Scoped execution authority
   ↓
Host-owned execution
   ↓
Decision / execution evidence
```

The additional stages are useful only when the system must preserve distinctions among them.

For example:

```text
Decision was allowed
        ≠
Acknowledgment was satisfied
        ≠
Execution authority was issued
        ≠
Execution authority was still valid later
        ≠
Execution was attempted
        ≠
Execution completed
```

An immediate application-service mutation may not need any of those distinctions.

A delayed, mediated, or high-consequence workflow may need several of them.

---

## Signals That Broader Governed Execution Is Becoming Justified

The following signals indicate that the simple boundary may no longer be enough.

### The Decision Survives the Request

The system decides now but executes later.

```text
Request at 10:00
   ↓
Decision recorded
   ↓
Worker executes at 10:30
```

Now the architecture must decide what the later worker trusts.

### Execution Happens in Another Component

A queue consumer, regional gateway, external integration worker, robotics gateway, or another service performs the side effect.

The receiving component may need authority narrower than the caller's standing identity.

### Policy Can Change Between Decision and Execution

If policy version, resource state, classification, or risk context can change during the gap, the system needs an explicit freshness or re-evaluation story.

### Acknowledgment or Approval Interrupts the Flow

The operation pauses for:

```text
human acknowledgment
reviewer approval
specialist escalation
```

That is a workflow boundary, not just a longer application-service method.

### Several Constraints Need Reviewable Composition

The decision may depend on independently owned constraints such as:

```text
regional policy
organizational policy
tenant policy
resource classification
risk threshold
legal hold
safety state
```

A structured decision model can make precedence and reasons easier to inspect.

### Post-Approval Authority Should Be Narrower Than Standing Caller Authority

The caller may be broadly authorized to request operations while the executor should receive authority only for:

```text
one operation
one resource
one audience
short lifetime
bounded use count
```

That is where capability-scoped authority becomes meaningful.

### An Untrusted or Advisory Component Proposes Actions

An AI model, external planner, plugin, user-supplied workflow, or other less-trusted proposer should not inherit execution authority merely because it can describe an action.

The proposal/authority boundary becomes valuable.

### Durable Decision Provenance Matters

The organization may need to reconstruct:

```text
which facts were evaluated
which policy identity applied
which outcome was produced
which reasons were recorded
which acknowledgment was satisfied
which authority was issued
what eventually executed
```

That is more than ordinary request logging.

### Replay-Sensitive Authority Must Be Controlled

If the same post-approval artifact can be presented twice, bounded-use or replay state may be required.

That problem does not exist when no reusable authority artifact is created.

### Consequential External or Physical Effects Need Mediation

The operation may affect:

- External financial systems.
- Infrastructure control planes.
- Production deployments.
- Credential issuance.
- Legal or regulated records.
- Physical devices.

Externality alone does not automatically require the full governance spine.

But consequence plus delayed authority, independent execution, policy variability, or human oversight often makes the extra boundary worthwhile.

---

## Example of Requirements Evolving Past the Simple Boundary

Return to the case-management system.

The original use case was:

```text
Archive draft case
   ↓
Immediate reversible local mutation
```

Now add a different operation:

```text
case.purge
```

Its requirements evolve:

- The actor may request a purge but may not execute it directly.
- A legal-hold policy provider must confirm that deletion is permitted.
- Retention policy can change between request and execution.
- The user must acknowledge that purge is irreversible.
- Purge occurs after a waiting period.
- A background worker performs the action later.
- Local records and external document storage are affected.
- The worker should receive authority only for one case and one purge operation.
- Duplicate use of that authority must not cause repeated external deletion attempts.
- The organization needs durable evidence of the policy decision, acknowledgment, authority issuance, and eventual execution result.

The old path:

```text
Authorize
   ↓
Application service
   ↓
Delete now
```

no longer preserves the important boundaries.

A richer path may now be justified:

```text
Authenticated actor
        ↓
Authorized to request purge
        ↓
Purge intent
        ↓
Authoritative case + retention context
        ↓
Legal / retention constraints
        ↓
Governance decision
        ↓
AcknowledgmentRequired
        ↓
Acknowledgment recorded
        ↓
Re-evaluate current policy
        ↓
Allowed
        ↓
Short-lived / bounded execution authority
        ↓
Background execution boundary
        ↓
Validate current authority + replay state
        ↓
Host-owned purge executor
        ↓
Decision and execution evidence
```

The key change is not that the code became more important.

The key change is that **decision and execution are now separate lifecycle events with authority that must survive between them**.

That is the threshold the governance boundary is solving.

---

## Do Not Confuse Asynchrony with Governance

A background job does not automatically require a governance spine.

For example:

```text
User saves profile
   ↓
Application commits database change
   ↓
Outbox sends ordinary confirmation email
```

may remain a normal application workflow.

The outbox solves delivery reliability.

It does not necessarily represent a separate policy decision or post-approval authority artifact.

Likewise:

```text
Controller
   ↓
Queue command
   ↓
Worker runs normal application use case
```

can be appropriate when the queue is simply a transport and the worker operates under the application's ordinary authorization/identity model.

Ask:

> **What authority is the worker relying on, and does that authority need independent scope, freshness, replay, or provenance semantics?**

If the answer is no, async processing alone does not justify a capability pipeline.

## Do Not Confuse Distributed Transactions with Governance

A workflow can need an outbox, idempotency key, retry policy, saga, or reconciliation process without needing explicit governance decision objects.

Those patterns solve operational consistency and delivery problems.

Governance becomes relevant when the system must also preserve a decision/authority lifecycle.

For example:

```text
Local transaction + outbox
```

may be enough for a normal business workflow.

Whereas:

```text
Human-approved operation
   +
execution hours later
   +
policy may have changed
   +
worker must prove narrow authority
```

contains a different architectural problem.

Use the pattern that solves the actual failure mode.

---

## Application Result Types Are Not Automatically Governance Decisions

A well-designed application service often returns explicit result data.

For example:

```text
Succeeded
ValidationFailed
Conflict
NotFound
NotPermitted
```

That is good design when it helps the caller handle expected outcomes.

Do not rename every result type to `GovernanceDecision` merely because the result is structured.

A governance decision becomes useful when the result represents a policy/governance lifecycle with semantics that matter independently from the application call.

The distinction can be summarized as:

```text
Application result
=
How did this use case complete?
```

versus:

```text
Governance decision
=
What should happen next with this proposed operation under the governing constraints?
```

Sometimes one object can legitimately serve both purposes.

Do so because the semantics align, not because the repository has a preferred type name.

---

## Normal Application Audit Versus Durable Decision Provenance

An ordinary application mutation may need an audit event such as:

```text
Event: case.archived
Actor: user-42
Case: case-100
OccurredUtc: ...
Result: succeeded
CorrelationId: ...
```

That can be entirely sufficient.

A governed workflow may need additional evidence:

```text
DecisionId
PolicyVersion
PolicyFingerprint
ReasonCodes
AcknowledgmentId
CapabilityId
CapabilityValidationOutcome
UseCount
ExecutionAttempted
ExecutionResult
```

Do not collect the larger evidence model merely because more fields appear safer.

More evidence creates:

- Storage cost.
- Retention obligations.
- Privacy considerations.
- Schema/versioning work.
- Operational dependencies.
- Additional failure modes.

Preserve the evidence required by the consequence, audit need, security model, and dispute/recovery requirements.

---

## Comparison Matrix

| Concern | Simple application service | Broader governed execution |
| --- | --- | --- |
| Primary question | Can this use case execute correctly now? | What should happen next with this proposed governed operation? |
| Typical lifetime | Current request/application call | May span requests, people, queues, workers, or systems |
| Authorization | Framework/application authorization | Still required; usually not replaced |
| Validation | Request + current domain/application state | Request + explicit policy/governance context + execution-time validation |
| Business rules | Application/domain service | May remain there; governance adds cross-cutting decision constraints |
| Result model | Success, validation, conflict, not found, not permitted | Allow, deny, defer, acknowledgment, escalation, plus later execution states |
| Human interruption | Usually absent | May be first-class |
| Separate execution authority | Usually unnecessary | Useful when approval and execution are separated |
| Replay state | Usually no reusable authority artifact to replay | May be required for bounded-use authority |
| Policy identity/provenance | Ordinary app audit may be enough | Often preserved explicitly when historically important |
| Execution location | Same trusted application boundary | May be another component or later execution boundary |
| Policy change between decision/execution | Usually no meaningful gap | Must be handled deliberately |
| Operational complexity | Lower | Higher |
| Best fit | Immediate local use case | Delayed, mediated, multi-stage, high-consequence lifecycle |

Neither column is a maturity level.

A system does not "graduate" from application services to governed execution merely because it becomes larger.

The architecture should remain proportional to the problem.

---

## A Practical Decision Guide

Start with the simple path:

```text
Authorize
   ↓
Validate
   ↓
Application service
   ↓
Domain rules
   ↓
Execute / persist
```

Keep it when the important facts are true at the same execution boundary and the operation completes immediately.

Introduce additional governance boundaries only when a requirement appears that the simple path cannot represent cleanly.

Useful questions include:

1. Does the decision need to survive the current request?
2. Can execution happen minutes, hours, or days later?
3. Can another component execute without the original caller being present?
4. Does that component need authority narrower than the caller's standing permission?
5. Can policy or resource state change between decision and execution?
6. Is acknowledgment, approval, deferral, or escalation a real workflow state?
7. Does the system need to distinguish approval from later execution authority?
8. Can a less-trusted component propose an action without being allowed to execute it?
9. Does reusable authority need replay or bounded-use controls?
10. Must investigators reconstruct the exact policy identity and reasons that justified the action?
11. Does an irreversible external or physical side effect require stronger mediation?
12. Would the additional lifecycle objects make a real failure mode easier to prevent, detect, or recover from?

If most answers are **no**, an ordinary application service is probably the better starting point.

If several answers are **yes**, a broader governance lifecycle may now be earning its complexity.

---

## Watch for Both Extremes

### Extreme 1 — Every CRUD Endpoint Needs a Governance Pipeline

This produces architecture such as:

```text
Update display name
   ↓
Intent
   ↓
Policy context
   ↓
Decision ledger
   ↓
Capability
   ↓
Gateway
   ↓
Update display name
```

when the actual requirement may be:

```text
Authenticated owner
   ↓
Validate display name
   ↓
Update row
```

The larger design can create:

- More types.
- More storage.
- More failure dependencies.
- More testing surface.
- More onboarding cost.
- More ways to misunderstand which layer owns the real rule.

Complexity is not evidence of stronger governance.

### Extreme 2 — Authorization + Validation Always Covers Workflow Governance

The opposite failure is to keep forcing every requirement into one synchronous service after the lifecycle has changed.

Symptoms include:

```text
"approved" boolean stored for later
```

```text
acknowledgment represented as a permanent user flag
```

```text
worker trusts a queue message because an endpoint once authorized it
```

```text
policy changed but delayed job still executes old assumptions
```

```text
AI proposal reaches executor through the same service credential
```

At that point, the architecture is hiding authority and lifecycle state rather than simplifying them.

The smallest correct architecture may now be larger than an ordinary application service.

---

## Application Services and Governed Execution Can Coexist

Introducing governance does not make application services obsolete.

A mature design may be:

```text
Governance boundary
   ↓
Allowed + scoped authority
   ↓
Host execution gateway
   ↓
Application service
   ↓
Domain rules / transaction
   ↓
Infrastructure
```

The governance layer answers whether the operation may cross the consequential execution boundary.

The application service still owns the business use case.

For example, a purge gateway may validate the current capability and then call:

```text
PurgeCaseApplicationService
```

which still owns:

- Loading current aggregate state.
- Enforcing domain invariants.
- Coordinating local persistence.
- Calling infrastructure abstractions.
- Returning an execution result.

Governance does not replace normal application architecture.

It adds lifecycle control around the operations that require it.

---

## Working Reference: NetCoreApplicationTemplate

The [NetCoreApplicationTemplate optional application/domain layers guidance](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/articles/optional-application-domain-layers.md) deliberately starts from a compact ASP.NET Core structure and recommends adding layers only when they solve real complexity.

Its guidance is useful here because it treats application services as an incremental architectural tool rather than a mandatory ceremony.

The reference explicitly allows use cases to be organized as:

```text
application services
commands / queries
simple methods
```

and does not require Clean Architecture, CQRS, MediatR, or DDD merely because an application has business logic.

That same proportionality principle applies to governed execution:

> **Add the governance boundary when the lifecycle needs it, not because the vocabulary exists.**

Learning's [Data Access Boundaries and Transaction Reasoning](../aspnetcore/data-access-boundaries-and-transaction-reasoning.md) provides the complementary persistence view: an ordinary application service still needs meaningful transaction, persistence, and failure semantics even when no governance pipeline is present.

---

## Relationship to Existing Learning Material

Use these comparisons in sequence when useful:

1. [When ASP.NET Core Authorization Is Enough](when-aspnet-core-authorization-is-enough.md) — asks whether the problem is primarily access control.
2. **When a Simple Application Service Is Enough** — asks whether the authorized use case can remain an immediate application workflow.
3. [Decision Before Execution](../tutorials/decision-before-execution.md) — introduces an explicit decision/execution boundary when the operation needs one.
4. [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) — expands decisions into reviewable facts and outcomes.
5. [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) — adds interrupted lifecycle and evidence.
6. [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — adds narrow post-decision authority.
7. [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) — controls reuse when authority becomes a reusable artifact.

The sequence is not a mandatory architecture ladder.

It is a way to identify the first point at which a smaller design stops expressing the requirements cleanly.

---

## Review Checklist

Before adding a broader governance pipeline to an ordinary use case, ask:

- [ ] Is the operation already well expressed by framework authorization plus an application service?
- [ ] Does execution happen immediately?
- [ ] Is the authoritative resource available at execution time?
- [ ] Are the relevant rules local to the application/domain?
- [ ] Are ordinary result types sufficient?
- [ ] Is there no delayed approval or acknowledgment?
- [ ] Is there no separate executor that needs portable authority?
- [ ] Is normal application audit sufficient?
- [ ] Is there little value in reconstructing a historical policy decision?
- [ ] Would a capability, replay store, or decision ledger solve a real threat rather than add ceremony?

Before deciding the simple service is always enough, ask:

- [ ] Can policy change between approval and execution?
- [ ] Can a worker or external gateway execute later?
- [ ] Is human acknowledgment or escalation part of the workflow?
- [ ] Does execution authority need to be narrower than standing caller authority?
- [ ] Does an AI or other untrusted component propose actions?
- [ ] Is replay-sensitive authority created?
- [ ] Does durable decision provenance matter?
- [ ] Is the side effect consequential enough to justify mediation?

The correct architecture is the one whose boundaries match the actual lifecycle.

---

## Summary

A simple application service is enough when an authorized use case can validate current authoritative state, apply local business rules, execute immediately, persist safely, and leave ordinary operational/audit evidence inside one trusted application boundary.

That is not a lesser architecture.

It is often the clearer one.

Governed execution becomes valuable when decision and execution separate in time, trust domain, authority, or human workflow; when post-approval authority must be narrowly scoped; when policy changes must be re-evaluated; when replay-sensitive grants exist; or when durable decision provenance materially matters.

Avoid both claims:

```text
Every CRUD endpoint needs a governance pipeline.
```

and:

```text
Authorization + validation always covers consequential workflow governance.
```

Prefer the smallest architecture that preserves the boundaries the operation actually needs.

---

> **Read it. Run it. Question it. Improve it.**
