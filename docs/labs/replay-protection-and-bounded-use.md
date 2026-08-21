---
description: Reproduce a replay race, repair it with atomic bounded-use consumption, prove the concurrency invariant, and reason about guarantees outside the replay store.
---

# Lab — Replay Protection and Bounded-Use Authority

**Learning objective:** Reproduce a check-then-act replay race, repair it with atomic bounded-use consumption, prove the concurrency invariant with executable tests, and explain which guarantees remain outside the replay store.

**Difficulty:** Intermediate

**Prerequisites:** Complete [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md), run the [Replay Protection and Bounded-Use Authority sample](https://github.com/AsiBackbone/Learning/blob/main/samples/replay-protection-and-bounded-use/README.md), and be comfortable with the execution boundary from [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md).

This lab begins with the sample's intentionally unsafe implementation so you can observe the failure before repairing it.

> **The invariant is not “the token looked valid.” The invariant is “only one consumer claimed the final permitted use.”**

---

## Starting Architecture

The companion sample separates static validation from stateful consumption:

```text
Capability presented
        ↓
Static validation
        ↓
Atomic TryConsume
        ↓
Accepted or rejected
        ↓
Protected executor or stop
```

For the one-time case:

```text
MaximumUses = 1
        ↓
Two concurrent consumers
        ↓
Exactly one accepted
Exactly one rejected
        ↓
Protected execution count = 1
```

The sample also includes:

```text
DeliberatelyUnsafeCheckThenActCapabilityUseStore
```

which forces two callers to observe the same pre-consumption state before either writes it.

That type exists only for this learning exercise.

## Prepare the Lab

Work on a temporary branch or disposable copy of the repository.

```bash
git switch -c lab/replay-protection-concurrency
```

Run the sample and focused tests before making changes:

```bash
dotnet run --project samples/replay-protection-and-bounded-use/ReplayProtectionAndBoundedUse/ReplayProtectionAndBoundedUse.csproj

dotnet test samples/replay-protection-and-bounded-use/ReplayProtectionAndBoundedUse.Tests/ReplayProtectionAndBoundedUse.Tests.csproj
```

Locate these types in `ReplayProtection.cs`:

1. `ExecutionCapability`
2. `ExecutionCapabilityValidator`
3. `ICapabilityUseStore`
4. `AtomicInMemoryCapabilityUseStore`
5. `DeliberatelyUnsafeCheckThenActCapabilityUseStore`
6. `ProtectedOperationGateway`
7. `InMemoryReplayEvidenceSink`

Locate these tests in `ReplayProtectionBoundaryTests.cs`:

```text
TwoConcurrentConsumersOfOneTimeCapabilityExactlyOneExecutes
DeliberatelyUnsafeCheckThenActAllowsBothConcurrentConsumers
ExecutorFailureAfterConsumptionDoesNotRestoreAuthority
```

Before changing code, explain which test represents the intended invariant and which test intentionally demonstrates a broken design.

---

# Part 1 — Observe the Check-Then-Act Failure

Start with `DeliberatelyUnsafeCheckThenActCapabilityUseStore`.

Its logic is intentionally equivalent to:

```text
Read use count
        ↓
If count < MaximumUses, continue
        ↓
Another consumer may read the same count
        ↓
Write count + 1
```

Run:

```bash
dotnet test samples/replay-protection-and-bounded-use/ReplayProtectionAndBoundedUse.Tests/ReplayProtectionAndBoundedUse.Tests.csproj --filter DeliberatelyUnsafeCheckThenActAllowsBothConcurrentConsumers
```

The test should pass because it is asserting the **known teaching failure**:

```text
MaximumUses = 1
Concurrent consumers = 2
Accepted consumptions = 2
Protected executions = 2
```

It may also show a lost update:

```text
Observed store count = 1
Actual accepted executions = 2
```

## Explain the Race

Answer:

1. Which value did both consumers observe before either wrote state?
2. Why does a thread-safe dictionary not make the compound check-and-write atomic?
3. Why would separate `HasBeenUsed` and `MarkUsed` methods have the same architectural problem?
4. Which state transition must become indivisible?

Write the transition in one sentence before continuing.

A good answer has the shape:

> Check whether another use remains and claim that use as one atomic operation.

---

# Part 2 — Repair the Consumption Boundary

Create a temporary learner implementation named something like:

```text
LearnerAtomicCapabilityUseStore
```

Do not change the gateway contract.

Keep the semantic boundary:

```csharp
ValueTask<CapabilityUseResult> TryConsumeAsync(
    string capabilityId,
    int maximumUses,
    DateTimeOffset usedUtc,
    CancellationToken cancellationToken);
```

Choose one in-process atomic technique, for example:

- A per-capability `SemaphoreSlim`.
- A lock around the full check-and-increment transition.
- A compare-and-update loop whose semantics you can explain.

The exercise is not to reproduce one exact implementation.

The exercise is to preserve this invariant:

```text
Current use count
        +
MaximumUses
        ↓
One atomic decision
        ↓
Accepted with new count
or
Use limit exceeded
```

Wire your learner store into a copy of the concurrent test.

Your repaired test must prove:

```text
MaximumUses = 1
        ↓
Two concurrent consumers
        ↓
Accepted count = 1
Rejected count = 1
Executor invocation count = 1
```

After your test passes, compare your approach with `AtomicInMemoryCapabilityUseStore`.

## Review the Atomicity Scope

State precisely what you proved.

For an in-memory per-process gate, the strongest justified claim is:

> Competing consumers using this store instance cannot both claim the same final permitted use.

Do not write:

> The capability can never be replayed anywhere.

That broader claim requires deployment and persistence evidence you have not yet implemented.

---

# Part 3 — Race for the Final Use of a Bounded Grant

Change the scenario to:

```text
MaximumUses = 2
```

First consume one use sequentially.

Then start two concurrent attempts for the one remaining use:

```text
Initial count = 1
MaximumUses = 2
        ↓
Two concurrent consumers
        ↓
One claims use 2
One is rejected
        ↓
Total protected executions = 2
```

Add a focused test for this case.

The purpose is to show that one-time authority is not a separate mechanism.

It is the smallest bounded-use case.

## Optional Stress Extension

Start a larger number of concurrent consumers against:

```text
MaximumUses = 3
```

Assert only three reach protected execution.

Do not interpret a local stress test as proof of distributed correctness.

Use it only to exercise the implementation more aggressively inside the process.

---

# Part 4 — Keep Static Validation Separate from Consumption

Use an expired capability or change one binding such as:

```text
ResourceId
Audience
SubjectId
OperationName
```

The expected path is:

```text
Static validation rejects
        ↓
TryConsume is not granted a use
        ↓
Observed use count remains 0
        ↓
Executor invocation count = 0
```

Add or extend a test that proves both **no consumption** and **no execution**.

## Explain Why Order Matters

Suppose the host consumed authority before checking whether the capability targeted the correct resource.

A malformed or attacker-controlled request could spend legitimate authority without ever being eligible to execute.

That may be a denial-of-service path even if the protected side effect remains blocked.

The exact validation sequence can vary by host, but invalid authority should not casually consume a legitimate remaining use.

---

# Part 5 — Preserve Evidence for the Rejected Replay

Inspect `InMemoryReplayEvidenceSink`.

For a replay that loses the race, preserve evidence with the shape:

```text
Stage = capability-consumption
Outcome = rejected
ReasonCode = capability.use-limit-exceeded
CapabilityId = stable safe identifier
ObservedUseCount = 1
MaximumUses = 1
ExecutionAttempted = false
```

Add an assertion for every field you think is necessary to distinguish:

```text
Replay rejected before execution
```

from:

```text
Execution attempted and failed
```

Do not add the entire raw authority artifact merely to make debugging easier.

## Correlation Extension

Optionally add a separate `CorrelationId` to the evidence.

Keep it distinct from `CapabilityId` and explain why:

```text
CorrelationId
≠
CapabilityId
```

A single broader operation may issue more than one capability, and retries may share one correlation flow.

---

# Part 6 — Exercise Cancellation and Store Failure

The teaching atomic store accepts a cancellation token while waiting for its per-capability gate.

Run or extend:

```text
CancellationBeforeConsumptionDoesNotSpendAuthority
```

Verify:

```text
Cancellation observed before transition
        ↓
Use count remains 0
```

Then inspect:

```text
UnavailableUseStoreDoesNotBecomePermission
```

The sample maps a known store-unavailable failure to:

```text
capability.use-store-unavailable
```

with:

```text
Executor invocation count = 0
```

## Choose a Production Failure Posture

For one replay-sensitive mutation, choose one:

```text
Deny
Defer
Queue
Escalate
Reduced-capability mode
```

Explain why your choice is safer than:

```text
Store unavailable
        ↓
Assume unused
        ↓
Execute
```

Then answer:

1. Can your production `TryConsumeAsync` time out after the state transition commits?
2. If yes, how would a retry determine whether the original consume committed?
3. Does the store support a stable operation identity for retrying an ambiguous consume?
4. Which failure result is safe to expose to the caller without inventing certainty?

---

# Part 7 — Observe the Consume-Before-Execute Failure Window

Run:

```text
ExecutorFailureAfterConsumptionDoesNotRestoreAuthority
```

The test demonstrates:

```text
TryConsume accepted
        ↓
Use count = 1
        ↓
Executor invoked
        ↓
Executor throws
        ↓
Replay of same capability rejected
```

Now answer the most important question in this lab:

> Did the external side effect happen exactly once?

The correct answer is:

> The replay layer cannot establish that from consumption state alone.

The executor may have:

- Failed before changing anything.
- Changed local state and then thrown.
- Sent a remote request that succeeded before the response was lost.
- Started a long-running operation whose final state is unknown.

Do not automatically refund a use after an execution exception. Doing so can reopen authority after a side effect that may actually have occurred.

## Design Recovery State

Sketch a separate operation-state model such as:

```text
Authority consumed
Execution not started
Execution started
Execution outcome unknown
Execution completed
Execution failed
Recovery required
```

Explain which component owns that state.

---

# Part 8 — Separate Replay Protection from Idempotency

Create two distinct identifiers on paper or in code:

```text
CapabilityId = cap-123
OperationId = op-900
```

Now consider two different one-time capabilities:

```text
cap-123 → op-900
cap-456 → op-900
```

Each capability can be consumed exactly once.

The logical operation can still be attempted twice.

Answer:

1. Which identifier controls reuse of authority?
2. Which identifier could support request/operation idempotency?
3. If the downstream provider accepts an idempotency key, which identity should be stable across an ambiguous retry?
4. Why does replay protection not eliminate the need for downstream idempotency or reconciliation?

Your final explanation should distinguish:

```text
Capability replay protection
Request / command idempotency
Downstream operation idempotency
Exactly-once claims
```

---

# Part 9 — Design a Durable `TryConsumeAsync`

Do not implement a full production database unless you want the extension.

Instead, use [Data Access Boundaries and Transaction Reasoning](../aspnetcore/data-access-boundaries-and-transaction-reasoning.md) and design a durable store contract whose persistence operation preserves `TryConsumeAsync` semantics.

Choose one strategy:

### One-Time Unique Row

```text
Insert use row keyed by CapabilityId
        ↓
Unique constraint wins once
        ↓
Competing insert rejected
```

### Conditional Counter Update

```text
UPDATE capability_use
SET use_count = use_count + 1
WHERE capability_id = @id
  AND use_count < maximum_uses
```

Then interpret affected-row count as the consume result.

### Serializable / Locked Transaction

```text
Begin transaction
        ↓
Read current use state under appropriate lock/isolation
        ↓
Check + increment
        ↓
Commit
```

For your chosen design, document:

- Atomicity mechanism.
- Unique key or concurrency token.
- Timeout behavior.
- Retry behavior.
- Persistence after process restart.
- Multi-instance coordination.
- Retention window.
- Regional consistency scope.

The persistence abstraction should expose the semantic operation `TryConsumeAsync`, not merely generic CRUD methods that force every caller to reconstruct the race-prone sequence.

---

# Part 10 — Test the Boundary You Actually Claim

Your final local test suite should include at least:

```text
Valid unused capability
        ↓
Execution count = 1
```

```text
Second sequential use
        ↓
Rejected
        ↓
Execution count remains 1
```

```text
Two concurrent consumers of final use
        ↓
One accepted
One rejected
        ↓
Execution count increases by only 1
```

```text
Expired or mismatched capability
        ↓
No consumption
        ↓
Execution count = 0
```

```text
Replay store unavailable
        ↓
No fallback execution
```

If you implement a durable provider, add tests that match the stronger guarantee you now claim:

```text
Process restart
Multi-instance race
Ambiguous timeout / retry
```

A process-local unit test cannot prove those distributed properties.

---

# Final Validation

Run the complete sample suite:

```bash
dotnet test samples/Samples.slnx
```

Confirm that you can explain all of these statements without treating them as synonyms:

```text
The capability is statically valid.
The capability still has a permitted use.
This consumer atomically claimed that use.
The host invoked the protected executor.
The protected executor reported success or failure.
The downstream side effect may require its own idempotency/recovery semantics.
```

The final architecture should preserve:

```text
Static validation
        ↓
Atomic capability consumption
        ↓
Host-owned execution
        ↓
Separate operation/recovery semantics where required
```

not:

```text
Token valid
        ↓
Check unused
        ↓
Execute
        ↓
Mark used
```

---

# Completion Criteria

You have completed the lab when you can:

1. Reproduce the check-then-act race deliberately.
2. Explain why individually thread-safe reads and writes do not make the compound transition atomic.
3. Implement or defend an atomic `TryConsumeAsync` boundary.
4. Prove that two concurrent consumers cannot both claim one final use inside the tested consistency scope.
5. Demonstrate one-time and bounded-use variants.
6. Reject expired or mismatched authority without spending a use.
7. Preserve useful evidence for a rejected replay without logging raw authority.
8. Explain cancellation and replay-store failure behavior.
9. Explain what happens when execution fails after consumption.
10. Distinguish replay protection from request and downstream idempotency.
11. State why the sample's in-memory store does not survive restart or coordinate multiple processes.
12. Describe what a durable provider must add before stronger production claims are justified.
13. Avoid claiming exactly-once external execution from successful capability consumption.

## Resetting the Sample

If you created a temporary branch only for the exercise, inspect your changes before discarding them:

```bash
git status
git diff
```

To restore the canonical sample:

```bash
git restore samples/replay-protection-and-bounded-use
```

Use `git status` first so you understand which local work will be affected.

---

## Related Content

- [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) — canonical explanation of replay state, atomic consumption, failure windows, idempotency, and distributed scope.
- [Replay Protection and Bounded-Use Authority sample](https://github.com/AsiBackbone/Learning/blob/main/samples/replay-protection-and-bounded-use/README.md) — runnable safe and deliberately unsafe implementations used by this lab.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — review how narrow authority is validated where it becomes action.
- [Scoped Capability and Host-Owned Execution lab](scoped-capability-and-host-owned-execution.md) — revisit the broader capability boundary and its introductory single-use exercise.
- [Data Access Boundaries and Transaction Reasoning](../aspnetcore/data-access-boundaries-and-transaction-reasoning.md) — bridge `TryConsumeAsync` semantics into durable transaction and persistence design.
- [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) — see bounded authority inside a larger AI-assisted execution boundary.
- [`ICapabilityGrantUseStore`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/ICapabilityGrantUseStore.cs) — compare the lab contract with the working framework seam.
- [`InMemoryCapabilityGrantUseStore`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Storage.InMemory/CapabilityTokens/InMemoryCapabilityGrantUseStore.cs) — inspect the working local reference provider and its limitations.
- [`InMemoryCapabilityGrantUseStoreTests`](https://github.com/AsiBackbone/AsiBackbone/blob/main/tests/AsiBackbone.Core.Tests/CapabilityTokens/InMemoryCapabilityGrantUseStoreTests.cs) — compare local concurrency invariant coverage.

---

> **Read it. Run it. Question it. Improve it.**
