---
description: Diagnose governance checks scattered around account mutation and external calls, then refactor the workflow into an explicit decision pipeline whose blocked outcomes cannot reach protected execution.
---

# Lab — Refactor Scattered Governance Checks into an Explicit Decision Pipeline

**Learning objective:** Diagnose a service where governance logic is mixed with mutation, external calls, exception handling, and event publication, then refactor it so authoritative context, decision outcomes, continuation requirements, protected execution, and evidence are explicit and testable.

**Difficulty:** Intermediate  

**Pattern classification:** General learning material  

**Prerequisites:** Complete [Decision Before Execution](../tutorials/decision-before-execution.md) and [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md). Running the [Decision Before Execution sample](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-before-execution/README.md) first is recommended.

This lab is deliberately a **refactoring** exercise rather than a greenfield design exercise.

The starting application already works in the narrow sense that it returns structured decision values.

Its defect is architectural:

> **Some blocked decisions are produced only after the protected operation has already begun.**

The central invariant is:

```text
Denied / Deferred / AcknowledgmentRequired / EscalationRecommended
                              ↓
                    Protected Executor Calls = 0
```

and:

```text
Allowed
   ↓
Protected Executor Calls = 1
```

The goal is not to maximize the number of abstractions.

The goal is to make the point of no return obvious and ensure governance completes before that point is reachable.

---

## Scenario

The fictional administrative operation is:

```text
account.disable
```

The request contains:

- an actor identity;
- an account identity;
- administrator status supplied by the host-side test fixture;
- a correlation identifier;
- whether a required acknowledgment has been satisfied.

Current account state contains:

- tenant identity;
- protected-account status;
- pending-investigation status;
- manual-review status;
- disabled status;
- resource version.

The operation is intentionally local and deterministic. No real account system, notification service, or event broker is used.

---

# Part 1 — Run the Deliberately Flawed Starter

The runnable companion lives at:

```text
samples/decision-pipeline-refactoring/
```

Run it from the repository root:

```bash
dotnet run --project samples/decision-pipeline-refactoring/DecisionPipelineRefactoring/DecisionPipelineRefactoring.csproj
```

The first output section is the intentionally flawed `ScatteredAccountDisableService`.

Its control flow is approximately:

```text
Request
   ↓
Role check
   ↓
Load current account state
   ↓
Mutation branch
   ├── Disable account                ← mutation occurs
   └── Pending-investigation check    ← may now return Deferred
   ↓
Send notification                    ← external side effect occurs
   ↓
Protected-account policy check       ← may now return Denied
   ↓
Manual-review exception handling     ← may now return EscalationRecommended
   ↓
Acknowledgment check                 ← may now return AcknowledgmentRequired
   ↓
Publish event
   ↓
Return Allowed
```

For the protected-account scenario, you should observe a shape like:

```text
Decision = Denied
Account mutations = 1
Notifications = 1
```

The decision value is correct.

The system behavior is not.

A caller cannot undo the fact that the account mutation and notification were already reached before the `Denied` value was returned.

---

# Part 2 — Diagnose Before Refactoring

Do not begin by moving lines.

Read `ScatteredAccountDisableService.Handle` and classify each operation as one of these responsibilities:

```text
Request validation
Authoritative context construction
Governance decision
Continuation requirement
Protected execution
Evidence / observability
```

Then answer these questions:

1. Which line first crosses the protected side-effect boundary?
2. Which checks happen before current account state is available?
3. Which decisions occur after the mutation?
4. Which decisions occur after an external notification?
5. Why is `EscalationRecommended` inside exception handling harder to reason about than an explicit policy result?
6. Why is an acknowledgment check meaningless as a guard once the operation has already mutated state?
7. Is event publication part of the governed operation, evidence about the operation, or a downstream consequence? What consistency assumption are you making?
8. Which facts should be gathered once into authoritative context instead of being rediscovered throughout the method?
9. What should the executor know about policy rules?
10. What should the policy evaluator know about notification or event-publisher clients?

Keep ordinary input validation separate from this governance diagnosis. A malformed or missing `AccountId` can be rejected as invalid input before policy evaluation; administrator status, protected-resource state, investigation state, acknowledgment, and escalation are governance facts or outcomes. The refactor should not turn every guard clause into policy merely because the method is being reorganized.

A useful diagnosis should identify at least these problems:

- **decision ordering is not the same as decision ownership;**
- **protected execution becomes reachable before all blocking outcomes are known;**
- **policy and continuation logic are scattered across normal control flow and exception flow;**
- **the side-effect boundary cannot be tested with one executor invocation count;**
- **the service makes it difficult to tell whether a final decision describes what may happen or merely what should have happened.**

---

# Part 3 — Write the Target Invariant Before the Target Classes

Before choosing types, write the behavior you need to preserve.

For this lab, use:

```text
Denied
→ executor calls = 0

Deferred
→ executor calls = 0

AcknowledgmentRequired without satisfied continuation
→ executor calls = 0

EscalationRecommended
→ executor calls = 0

Allowed
→ executor calls = 1
```

These are architectural acceptance criteria, not implementation instructions.

Your refactor may use one application-service class or several collaborating classes as long as the boundary remains explicit and the tests can prove it.

---

# Part 4 — Refactor Toward Explicit Phases

Use this responsibility sequence as the target:

```text
Intent
   ↓
Authoritative Context
   ↓
Decision
   ↓
Continuation Requirements
   ↓
Protected Executor
   ↓
Evidence
```

One reference shape in the companion sample is:

```text
AccountDisableRequest
        ↓
AccountDisableContextBuilder
        ↓
AccountDisableContext
        ↓
AccountDisablePolicy
        ↓
GovernanceDecision
        ↓
CanExecute?
   ┌────┴─────┐
   │          │
  No         Yes
   │          │
Evidence      ↓
   │     AccountDisableExecutor
 Return       ↓
         Execution evidence
```

Refactor under these constraints:

1. Load current account state before resource-dependent policy rules are evaluated.
2. Put decision inputs into one explicit context object or equivalent local snapshot.
3. Return an explicit outcome rather than a boolean.
4. Do not let the policy evaluator depend on the account mutation, notification, or event-publisher clients.
5. Do not invoke the protected executor for any blocked outcome.
6. Keep the executor focused on the `account.disable` operation rather than allowing it to reinterpret policy.
7. Preserve enough decision evidence to explain the outcome and resource version used.
8. Do not turn evidence recording into a bypass around the execution guard.

The reference sample represents continuation requirements as explicit outcomes:

```text
AcknowledgmentRequired
EscalationRecommended
Deferred
```

For this exercise, those outcomes return control to the caller rather than starting a durable workflow engine.

That keeps the lesson focused on the decision/execution boundary.

---

# Part 5 — Make Authoritative Context Visible

The starter checks administrator status before loading account state, then evaluates resource rules later.

That creates two different moments at which the method seems to be "deciding."

Refactor toward one context snapshot such as:

```text
CorrelationId
ActorId
Operation = account.disable
RequesterIsAdministrator
AcknowledgmentSatisfied
AccountId
TenantId
ProtectedAccount
PendingInvestigation
RequiresManualReview
ResourceVersion
```

Not every real system needs a single record containing every fact.

The important property is that the policy evaluation receives facts whose trust and freshness are understood.

Ask:

> **Which component is authoritative for each fact, and at what moment was it observed?**

Do not treat an explicit context object as automatically trustworthy merely because the fields are collected together.

---

# Part 6 — Replace Exception-Driven Governance with an Explicit Outcome

The starter uses an exception to manufacture this result:

```text
EscalationRecommended
```

That is intentionally awkward.

Exceptions are useful for exceptional failures.

A known manual-review condition is ordinary policy state in this exercise.

Refactor from a shape like:

```text
if (requiresManualReview)
    throw ...

catch (...)
    return EscalationRecommended
```

into a direct policy result:

```text
if (context.Account.RequiresManualReview)
    return EscalationRecommended
```

Then explain the difference between:

- **an expected governance outcome**, such as escalation;
- **an unexpected technical failure**, such as the account repository becoming unavailable.

Do not convert every exception into a governance outcome merely to keep the method returning a decision object.

---

# Part 7 — Define the Protected Executor Boundary

For the reference sample, the protected executor represents one semantic operation:

```text
account.disable
```

Its teaching implementation performs three simulated consequences:

```text
account state mutation
notification
account-disabled event publication
```

That bundling is intentionally simplified.

In a real system, you may decide that:

- the database mutation is transactional;
- an outbox record is written in the same transaction;
- notification and event publication occur asynchronously;
- each downstream delivery has its own retry and idempotency behavior.

Those choices change the execution topology.

They do not change the precondition for this lab:

> **No blocked decision may invoke the protected execution boundary.**

If you split the side effects into multiple executors, strengthen the tests so every protected path remains zero for blocked outcomes.

---

# Part 8 — Add the Invariant Tests

Run the companion tests:

```bash
dotnet test samples/decision-pipeline-refactoring/DecisionPipelineRefactoring.Tests/DecisionPipelineRefactoring.Tests.csproj
```

The test project contains one diagnostic starter test plus the required refactored invariants.

## Diagnostic starter test

The starter test intentionally proves the defect:

```text
Decision = Denied
Mutation count = 1
Notification count = 1
```

The test passes because it records known broken behavior.

It prevents the exercise from relying on prose alone to claim that the starter is unsafe.

## Required refactored tests

Your refactor should preserve these cases:

```text
Denied_request_never_reaches_the_executor
```

```text
Deferred_request_never_reaches_the_executor
```

```text
Acknowledgment_required_without_satisfied_continuation_never_reaches_the_executor
```

```text
Escalation_recommended_never_reaches_the_executor
```

```text
Allowed_request_reaches_the_executor_exactly_once
```

The strongest assertions combine outcome and behavior:

```csharp
Assert.Equal(DecisionOutcome.Denied, decision.Outcome);
Assert.Equal(0, executor.InvocationCount);
```

and:

```csharp
Assert.Equal(DecisionOutcome.Allowed, decision.Outcome);
Assert.Equal(1, executor.InvocationCount);
```

For blocked paths, also inspect the simulated repository, notification gateway, and event publisher where useful.

A zero executor count is the primary invariant.

The other counters make hidden bypasses easier to detect.

---

# Part 9 — Preserve Evidence Without Confusing It with Authority

The reference pipeline records a decision before it tests `CanExecute`.

For an allowed operation, it records a second execution-stage entry after the executor returns.

That produces a small sequence:

```text
Blocked path:
Decision evidence
→ return

Allowed path:
Decision evidence
→ executor
→ execution evidence
```

The evidence includes the resource version used by the decision.

This helps answer:

- which account state was evaluated;
- which outcome was produced;
- why it was produced;
- whether the executor was reached.

It does **not** prove cryptographic integrity, durable retention, or legal compliance.

Those claims require stronger infrastructure than this lab provides.

---

# Part 10 — Compare Multiple Legitimate Refactoring Shapes

Do not treat the companion sample's class layout as the only correct answer.

## Option A — One Explicit Application Service

A small application may keep the flow in one service:

```text
load authoritative state
→ build local context variables
→ evaluate all blocking rules
→ return blocked outcome if needed
→ execute once
→ record evidence
```

This can be excellent architecture when the method remains short and the decision/execution boundary is obvious.

## Option B — Separate Context, Policy, Pipeline, and Executor

This is the companion sample's reference shape.

It is useful when:

- policy rules deserve focused tests;
- multiple callers share the same decision semantics;
- continuation outcomes are becoming richer;
- execution dependencies should be structurally unavailable to policy code;
- the team benefits from a named decision boundary.

## Option C — External or Shared Policy Evaluation

A larger system may move policy evaluation behind a shared policy engine or Policy Decision Point while keeping enforcement local.

That introduces new questions:

- policy freshness;
- network failure;
- authoritative-input construction;
- PDP/PEP placement;
- caching;
- degraded operation.

Do not add that boundary merely because the local refactor works.

The lab succeeds when you can explain **why your chosen boundary earns its complexity**.

---

# Part 11 — When the Simple Service Should Win

After completing the refactor, challenge it.

A simple application service is often the better answer when:

- one process owns both decision and execution;
- the action executes immediately;
- there are only a few stable rules;
- there is no cross-process delegation;
- there is no delayed continuation authority;
- acknowledgment or escalation does not create a durable workflow;
- the execution line is easy to protect with one visible guard;
- focused tests can prove blocked execution without additional architecture.

In that case, introducing separate context builders, policy objects, pipeline abstractions, and executor interfaces may increase indirection without increasing safety.

The proportionality question is:

> **Does the refactor make the protected boundary easier to see, test, and change, or does it merely distribute a simple method across more files?**

Read [When a Simple Application Service Is Enough](../architecture/when-a-simple-application-service-is-enough.md) before deciding that the reference decomposition should become a default template.

---

# Reference Discussion

The companion sample's reference solution uses five distinct responsibilities.

## 1. Context construction

`AccountDisableContextBuilder` loads current account state before resource-dependent policy evaluation.

It does not mutate the account.

## 2. Policy evaluation

`AccountDisablePolicy` returns explicit outcomes and has no dependency capable of disabling an account, sending a notification, or publishing an event.

That makes this property structural:

```text
Policy evaluator
   ↓
Cannot directly perform account.disable
```

## 3. Continuation interpretation

`CanExecute` is true only for `Allowed`.

`Deferred`, `AcknowledgmentRequired`, and `EscalationRecommended` remain blocked until some later application behavior satisfies or resolves them.

The sample does not pretend that merely returning one of those outcomes completes a workflow.

## 4. Protected execution

`RecordingAccountDisableExecutor` owns the simulated side-effect bundle.

The pipeline invokes it only after the decision guard.

## 5. Evidence

`RecordingDecisionEvidenceSink` records the decision path without granting execution authority.

The pipeline remains responsible for ordering.

This is one valid decomposition.

A smaller service can preserve the same invariant with fewer types, and a distributed system may require more boundaries.

---

# Architectural Acceptance Criteria

You have completed the lab when you can demonstrate all of the following:

- [ ] You identified the first protected side effect in the starter.
- [ ] You can explain why the starter's correct `Denied` value is not sufficient.
- [ ] Current resource state is available before resource-dependent policy evaluation.
- [ ] Decision inputs are explicit enough to review their source and freshness.
- [ ] Policy evaluation does not own an execution-capable dependency for `account.disable`.
- [ ] `Denied` produces zero executor calls.
- [ ] `Deferred` produces zero executor calls.
- [ ] unsatisfied `AcknowledgmentRequired` produces zero executor calls.
- [ ] `EscalationRecommended` produces zero executor calls.
- [ ] `Allowed` produces exactly one executor call.
- [ ] Decision evidence remains available for blocked paths.
- [ ] You can explain at least one alternative refactoring shape that also preserves the invariant.
- [ ] You can identify a scenario where keeping the logic in one simple application service is preferable.

The target architecture should make this relationship visible:

```text
Decision completed
   ↓
Blocked? ── Yes ──> evidence + return
   │
   No
   ↓
One explicit protected execution attempt
```

---

## Optional Extensions

### Extension 1 — Resource Drift

After policy evaluation but before execution, change the account version.

Decide whether the pipeline should:

- re-read the resource;
- re-evaluate policy;
- reject stale continuation;
- or allow the original snapshot to remain authoritative for this immediate same-host operation.

Explain the consistency contract you choose.

### Extension 2 — Durable Escalation

Persist `EscalationRecommended` as workflow state and resume later.

Then answer:

> Does the original allowed/blocked decision remain sufficient authority for later execution?

If not, what must be revalidated?

### Extension 3 — Outbox Boundary

Replace direct event publication with a simulated outbox entry written with the account mutation.

Explain which action now counts as the protected execution attempt and which later deliveries are consequences of that committed operation.

### Extension 4 — Remove the Extra Abstractions

Refactor the reference implementation back into one application service while keeping all invariant tests green.

If the result is easier to understand, explain why the simpler shape is better for this scenario.

---

## Related Content

- [Decision Before Execution tutorial](../tutorials/decision-before-execution.md) — foundational separation between deciding and acting.
- [Decision Before Execution sample](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-before-execution/README.md) — smaller runnable demonstration of the core invariant.
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) — explicit facts and non-boolean outcomes.
- [Identify and Remove a Hidden Execution Side Effect](hidden-execution-side-effect.md) — beginner diagnostic exercise focused on one concealed side effect.
- [Build a Governed API Operation](build-a-governed-api-operation.md) — extend an ASP.NET Core operation through the broader governed lifecycle.
- [Decision Pipeline Refactoring sample](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-pipeline-refactoring/README.md) — runnable starter, reference refactor, and invariant tests for this lab.
- [When a Simple Application Service Is Enough](../architecture/when-a-simple-application-service-is-enough.md) — proportionality guidance for avoiding unnecessary governance machinery.
- [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md) — compare local decision logic with externalized policy evaluation and enforcement placement.

---

> **Read it. Run it. Question it. Improve it.**
