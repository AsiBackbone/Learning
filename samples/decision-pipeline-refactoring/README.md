# Decision Pipeline Refactoring Sample

This sample supports the intermediate lab [Refactor Scattered Governance Checks into an Explicit Decision Pipeline](../../docs/labs/refactor-scattered-governance-checks.md).

It begins with a deliberately flawed `account.disable` application service whose governance checks are scattered around protected side effects, then provides a small reference refactor that separates authoritative context construction, policy evaluation, continuation requirements, protected execution, and evidence.

## Learning Objective

Recognize the difference between code that merely returns the right decision value and code whose structure prevents blocked outcomes from reaching protected execution.

The central invariant is:

```text
Denied / Deferred / AcknowledgmentRequired / EscalationRecommended
                              ↓
                    Executor Invocation Count = 0
```

while:

```text
Allowed
   ↓
Executor Invocation Count = 1
```

## Difficulty

Intermediate

## Prerequisites

Recommended:

- [Decision Before Execution](../../docs/tutorials/decision-before-execution.md)
- [Policy Context and Explicit Decision Outcomes](../../docs/tutorials/policy-context-and-explicit-decision-outcomes.md)
- [Decision Before Execution sample](../decision-before-execution/README.md)

## Scenario

The fictional operation is:

```text
account.disable
```

The intentionally flawed service performs work in this order:

```text
Request
   ↓
Role check
   ↓
Load account
   ↓
Mutation branch
   ├── Disable account             ← protected side effect already happened
   └── Pending-investigation check ← may now return Deferred
   ↓
Send notification                 ← external side effect
   ↓
Protected-account policy check    ← may now return Denied
   ↓
Exception-driven manual-review outcome
   ↓
Acknowledgment check
   ↓
Publish event
   ↓
Return final decision
```

A `Denied`, `Deferred`, `AcknowledgmentRequired`, or `EscalationRecommended` result can therefore be correct as a value while still arriving after part of the requested operation has already happened.

The refactored reference flow is:

```text
Intent
   ↓
Authoritative Context
   ↓
Decision
   ↓
Continuation Requirements
   ↓
CanExecute?
   ├── No  → preserve decision evidence → return
   └── Yes → protected executor → preserve execution evidence
```

## Project Structure

```text
samples/decision-pipeline-refactoring/
├── DecisionPipelineRefactoring/
│   ├── DecisionPipelineRefactoring.csproj
│   └── Program.cs
├── DecisionPipelineRefactoring.Tests/
│   ├── DecisionPipelineRefactoring.Tests.csproj
│   └── DecisionPipelineInvariantTests.cs
└── README.md
```

`Program.cs` intentionally keeps the teaching types together so the responsibility changes remain easy to inspect.

## Important Types

### `ScatteredAccountDisableService`

This is the deliberately flawed starting point.

It demonstrates several common review problems at once:

- a role check before authoritative resource state is loaded;
- account mutation before policy evaluation is complete;
- an external notification before later policy checks;
- an escalation outcome manufactured inside exception handling;
- acknowledgment checked only after mutation and notification;
- a final governance check immediately before event publication.

The sample does **not** present this service as acceptable architecture.

### `AccountDisableContextBuilder`

Loads current account state and creates one explicit policy-context snapshot.

The context makes these facts visible:

- correlation identity;
- actor identity;
- operation;
- administrator status;
- acknowledgment state;
- account identity and tenant;
- protected/manual-review/investigation state;
- resource version.

### `AccountDisablePolicy`

Returns one of five explicit outcomes without holding an execution-capable dependency:

```text
Allowed
Denied
Deferred
AcknowledgmentRequired
EscalationRecommended
```

### `AccountDisableDecisionPipeline`

Owns the ordering relationship between context, decision, evidence, and execution.

Its important guard is conceptually:

```text
Decision
   ↓
CanExecute = false
   ↓
Return before executor is reachable
```

### `RecordingAccountDisableExecutor`

Represents the protected host-owned operation.

For this teaching sample, one invocation performs the fictional disable bundle:

- mutate the account state;
- send the account-disabled notification;
- publish the account-disabled event.

Real systems may use transactions, an outbox, durable messaging, or separate retry boundaries. The sample keeps the operation bundled only so `InvocationCount` gives learners one unambiguous execution boundary to test.

### `RecordingDecisionEvidenceSink`

Preserves a small decision/execution trail without being treated as execution authority.

Blocked decisions produce one decision record and zero execution records.

An allowed operation produces a decision record followed by an execution record.

## Run the Sample

From the repository root:

```bash
dotnet run --project samples/decision-pipeline-refactoring/DecisionPipelineRefactoring/DecisionPipelineRefactoring.csproj
```

The first section intentionally demonstrates the broken architecture. A protected account is denied **after** the account mutation and notification have already occurred.

The second section runs the same decision categories through the refactored pipeline and prints executor counts.

Expected shape:

```text
Intentionally flawed scattered service:
  decision: Denied (...)
  account mutations before denial: 1
  notifications before denial: 1

Refactored decision pipeline:
  Denied                     ... executorCalls=0
  Deferred                   ... executorCalls=0
  AcknowledgmentRequired     ... executorCalls=0
  EscalationRecommended      ... executorCalls=0
  Allowed                    ... executorCalls=1
```

Exact spacing is not part of the contract.

## Run the Tests

From the repository root:

```bash
dotnet test samples/decision-pipeline-refactoring/DecisionPipelineRefactoring.Tests/DecisionPipelineRefactoring.Tests.csproj
```

Or run the complete sample suite:

```bash
dotnet test samples/Samples.slnx
```

## What the Tests Prove

The test suite includes one diagnostic test for the flawed starter and focused invariants for the refactored pipeline.

### Starter diagnosis

```text
Scattered service
   ↓
Decision = Denied
   ↓
Account mutation count = 1
Notification count = 1
```

The test passes because it is documenting a known architectural defect, not because the behavior is desirable.

### Refactored invariants

```text
Denied
→ executor calls = 0
```

```text
Deferred
→ executor calls = 0
```

```text
AcknowledgmentRequired without satisfied continuation
→ executor calls = 0
```

```text
EscalationRecommended
→ executor calls = 0
```

```text
Allowed
→ executor calls = 1
```

The blocked-path tests also verify that the repository mutation count remains zero. The denied test additionally verifies that notifications and events remain untouched.

## Why the Reference Refactor Is Not the Only Valid Shape

The reference solution uses separate context-builder, policy, pipeline, executor, and evidence types because those responsibilities are the subject of the exercise.

That does not mean every application needs that many types.

A small same-host application may keep the same responsibilities inside one application service if the sequence remains explicit:

```text
load authoritative state
→ evaluate all blocking conditions
→ return blocked outcome when needed
→ execute once
→ preserve evidence
```

The stronger requirement is the invariant, not a prescribed class count.

## When a Simpler Application Service Is Better

Prefer a simpler application service when most of these conditions are true:

- one host owns decision and execution;
- the operation executes immediately;
- there is no delayed continuation authority;
- there is no cross-process policy boundary;
- the rule set is small and stable;
- acknowledgment/escalation do not create a long-lived workflow;
- one clear execution guard is easy to review and test;
- the extra abstractions would hide rather than clarify the operation.

See [When a Simple Application Service Is Enough](../../docs/architecture/when-a-simple-application-service-is-enough.md) for the broader proportionality argument.

## What This Sample Intentionally Omits

This sample does not attempt to model:

- authentication infrastructure;
- durable databases;
- distributed transactions;
- outbox/inbox processing;
- durable workflow state;
- cryptographic capability tokens;
- real notification systems;
- real event brokers;
- multi-instance concurrency;
- retry/idempotency design;
- production audit storage;
- legal or compliance guarantees.

Those concerns can change the implementation shape without changing the central learning question:

> **Can a blocked decision reach the protected executor?**

## Related Learning Material

- [Refactor Scattered Governance Checks lab](../../docs/labs/refactor-scattered-governance-checks.md)
- [Decision Before Execution tutorial](../../docs/tutorials/decision-before-execution.md)
- [Decision Before Execution sample](../decision-before-execution/README.md)
- [Policy Context and Explicit Decision Outcomes](../../docs/tutorials/policy-context-and-explicit-decision-outcomes.md)
- [Build a Governed API Operation lab](../../docs/labs/build-a-governed-api-operation.md)
- [When a Simple Application Service Is Enough](../../docs/architecture/when-a-simple-application-service-is-enough.md)

## Working Implementation References

For fuller implementation guidance, compare the teaching sample with:

- [AsiBackbone](https://github.com/AsiBackbone/AsiBackbone) — governance and policy-control implementation.
- [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate) — ASP.NET Core reference architecture.

---

> **Read it. Run it. Question it. Improve it.**
