# Replay Protection and Bounded-Use Authority Sample

This executable companion sample demonstrates the stateful execution-boundary invariant taught in [Replay Protection and Bounded-Use Authority](../../docs/security/replay-protection-and-bounded-use.md).

The sample isolates one question:

> **When one permitted use remains, can two concurrent consumers both claim it?**

The safe path is:

```text
Statically valid capability
        ↓
Atomic TryConsume
        ↓
Exactly one final use is claimed
        ↓
Exactly one consumer reaches protected execution
```

The sample also contains a **deliberately unsafe check-then-act store** whose only purpose is to reproduce the race the atomic store prevents.

## Learning Objective

Observe the difference between:

```text
Static capability validation
```

and:

```text
Stateful capability consumption
```

Then run two concurrent consumers against `MaximumUses = 1` and verify that the atomic in-memory store accepts one consumer, rejects the other, and allows only one protected execution.

The sample also makes these boundaries explicit:

- A stable `CapabilityId` identifies the authority being consumed.
- `MaximumUses` controls one-time and bounded-use authority.
- Invalid or expired authority is rejected before consumption.
- A rejected replay leaves evidence with `ExecutionAttempted = false`.
- Replay-store unavailability does not become permission.
- Cancellation before the state transition does not spend authority.
- Consumption before execution can leave authority spent even when the executor later fails.
- Successful consumption is **not** an exactly-once guarantee for an external side effect.

## Difficulty

Intermediate

## Prerequisites

- .NET 10 SDK
- [Replay Protection and Bounded-Use Authority](../../docs/security/replay-protection-and-bounded-use.md)
- [Scoped Capability and Host-Owned Execution](../../docs/tutorials/scoped-capability-and-host-owned-execution.md)

The [Data Access Boundaries and Transaction Reasoning](../../docs/aspnetcore/data-access-boundaries-and-transaction-reasoning.md) tutorial is useful when you are ready to replace the teaching store with durable state.

## Project Structure

```text
samples/replay-protection-and-bounded-use/
├── ReplayProtectionAndBoundedUse/
│   ├── Program.cs
│   ├── ReplayProtection.cs
│   └── ReplayProtectionAndBoundedUse.csproj
├── ReplayProtectionAndBoundedUse.Tests/
│   ├── ReplayProtectionBoundaryTests.cs
│   └── ReplayProtectionAndBoundedUse.Tests.csproj
└── README.md
```

`ReplayProtection.cs` contains the small framework-neutral teaching types so the executable and tests exercise the same boundary.

## Run the Sample

From the repository root:

```bash
dotnet run --project samples/replay-protection-and-bounded-use/ReplayProtectionAndBoundedUse/ReplayProtectionAndBoundedUse.csproj
```

The program runs four observable comparisons.

### Atomic One-Time Race

```text
MaximumUses = 1
        ↓
Two concurrent consumers
        ↓
One accepted
One rejected
        ↓
Protected execution count = 1
```

The rejected consumer receives:

```text
capability.use-limit-exceeded
```

### Sequential Replay

After the concurrent race, the same capability is presented again.

The additional replay is rejected and the protected execution count remains one.

### Deliberately Unsafe Check-Then-Act Race

The sample then uses `DeliberatelyUnsafeCheckThenActCapabilityUseStore`.

That store intentionally separates:

```text
Read current use count
        ↓
Wait while another consumer reads the same state
        ↓
Write updated use count
```

Both consumers therefore observe `0` before either writes.

The expected teaching failure is:

```text
MaximumUses = 1
        ↓
Two concurrent consumers
        ↓
Both accepted
        ↓
Protected execution count = 2
```

The unsafe store is not an alternative implementation recommendation. It exists only to make the race deterministic and visible.

### Static Validation Before Consumption

An expired capability is rejected before the use store changes state:

```text
Expired capability
        ↓
Static validation rejects
        ↓
Consumed uses = 0
        ↓
Protected executions = 0
```

This demonstrates why artifact validity and use-state consumption are distinct operations even when a production API later composes them behind one method.

## Run the Tests

From the repository root:

```bash
dotnet test samples/replay-protection-and-bounded-use/ReplayProtectionAndBoundedUse.Tests/ReplayProtectionAndBoundedUse.Tests.csproj
```

Or run the complete sample suite:

```bash
dotnet test samples/Samples.slnx
```

The focused tests cover:

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
Two concurrent consumers
MaximumUses = 1
        ↓
One accepted
One rejected
        ↓
Execution count = 1
```

```text
Expired or mismatched capability
        ↓
No consumption
        ↓
Execution count = 0
```

They also cover bounded-use authority, rejected-replay evidence, cancellation before consumption, explicit replay-store unavailability, and executor failure after a use has already been consumed.

## What to Observe

### Static Validation Does Not Consume Authority

`ExecutionCapabilityValidator` checks the capability's actor, operation, resource, audience, validity window, and `MaximumUses` shape.

Those checks do not change replay state.

The gateway only asks the use store to consume authority after static validation succeeds.

That ordering prevents a malformed, expired, wrong-resource, or wrong-audience artifact from spending a legitimate remaining use.

### `TryConsumeAsync` Is the Atomic Boundary

`AtomicInMemoryCapabilityUseStore` exposes one semantic operation:

```text
TryConsumeAsync
```

Inside that operation, a per-capability gate protects the check and increment as one state transition.

The important guarantee is not that the dictionary is thread-safe.

The important guarantee is:

> **Two consumers cannot both observe and claim the same final permitted use inside this process.**

The deliberately unsafe store uses a thread-safe dictionary too, but separates the read from the write. That is enough to reintroduce the race.

### Bounded Use Is the Same State Problem

`MaximumUses = 1` is only the smallest bounded-use case.

The tests also use:

```text
MaximumUses = 2
```

and prove that the third accepted attempt never reaches execution.

The state therefore needs a use count rather than only a token-shape check.

### Rejected Replay Leaves Evidence

`InMemoryReplayEvidenceSink` records safe teaching evidence such as:

```text
Stage: capability-consumption
Outcome: rejected
Reason: capability.use-limit-exceeded
CapabilityId: cap-one-time
ObservedUseCount: 1
MaximumUses: 1
ExecutionAttempted: false
```

The sample does not log a raw bearer token or secret.

In a real system, evidence retention, privacy, integrity, and durability remain host responsibilities.

### Store Failure Does Not Become Permission

The gateway treats a known `CapabilityUseStoreUnavailableException` as:

```text
capability.use-store-unavailable
```

and does not invoke the protected executor.

This is a teaching fail-closed posture for a replay-sensitive mutation.

A production system may choose deny, defer, queue, or escalate depending on risk and availability requirements, but the fallback should be explicit.

### Cancellation Has a Boundary Too

The atomic teaching store waits for its per-capability gate with the caller's cancellation token.

If cancellation is observed before the state transition, the use is not consumed.

Once consumption succeeds, however, later cancellation does not automatically refund authority.

That is a workflow/recovery decision, not something replay protection can infer safely.

## Consumption Does Not Mean Exactly-Once Execution

The gateway deliberately consumes bounded-use authority **before** invoking the protected executor.

That ordering protects the authority from reuse, but it creates a failure window:

```text
TryConsume succeeds
        ↓
Authority is spent
        ↓
Executor starts or is about to start
        ↓
Process / dependency / operation fails
```

The test `ExecutorFailureAfterConsumptionDoesNotRestoreAuthority` makes this visible.

After the executor throws:

```text
Observed use count = 1
Replay of same capability = rejected
```

That proves only that the authority was consumed.

It does **not** prove that an external side effect:

- Never started.
- Completed successfully.
- Failed before changing remote state.
- Occurred exactly once.

A real external operation may additionally require:

- Request or command idempotency.
- Provider idempotency keys.
- Operation-status state.
- Reconciliation.
- Outbox/inbox patterns.
- A shared transaction when the mutation and replay state truly share one transactional boundary.
- Recovery or human escalation for ambiguous outcomes.

The sample intentionally does not collapse those concerns into the replay store.

## In-Memory Is a Teaching Scope, Not a Production Guarantee

`AtomicInMemoryCapabilityUseStore` is thread-safe for the process that owns it.

It does not survive:

```text
Process restart
```

and it does not coordinate:

```text
Application instance A
Application instance B
```

or:

```text
Region A
Region B
```

Therefore the correct claim is:

> **This sample demonstrates atomic replay-use semantics inside one process.**

It does not claim durable or globally distributed replay protection.

A production provider must define its own durability, consistency, timeout, retry, retention, replication, and recovery semantics.

## What This Sample Intentionally Omits

This is a teaching artifact. It intentionally omits:

- Cryptographic capability proof or bearer-token transport.
- Authentication and authorization infrastructure.
- Durable database-backed replay state.
- Cross-process or cross-region coordination.
- Distributed locking or consensus.
- Replay-state retention and cleanup policy.
- Real external side effects.
- Request idempotency storage.
- Provider idempotency keys.
- Outbox/inbox infrastructure.
- Exactly-once execution claims.
- Production monitoring or alerting.
- The fuller `AsiBackbone` package abstractions.

## Compare with the Working Framework

The sample stays framework-neutral so the atomic state transition remains easy to see. The working `AsiBackbone` repository provides fuller seams and tests around the same concern.

| Teaching sample | Working reference | What to inspect |
| --- | --- | --- |
| `ICapabilityUseStore` | [`ICapabilityGrantUseStore`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/ICapabilityGrantUseStore.cs) | Provider-neutral `TryConsumeAsync` semantics and the boundary between Core behavior and host-owned durable state. |
| `AtomicInMemoryCapabilityUseStore` | [`InMemoryCapabilityGrantUseStore`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Storage.InMemory/CapabilityTokens/InMemoryCapabilityGrantUseStore.cs) | Thread-safe local use counts and explicit non-durable/non-distributed limitations. |
| Concurrent invariant tests | [`InMemoryCapabilityGrantUseStoreTests`](https://github.com/AsiBackbone/AsiBackbone/blob/main/tests/AsiBackbone.Core.Tests/CapabilityTokens/InMemoryCapabilityGrantUseStoreTests.cs) | Accepted use, use limits, stop/cancel state, and local concurrency behavior. |
| `ProtectedOperationGateway` | [`CapabilityGrantValidator`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityGrantValidator.cs) | Static/proof validation composed with optional stateful use checking before host-owned execution. |

The framework implementation remains a specimen. Durable replay guarantees are still defined by the host and selected provider.

## Try It

1. Change `MaximumUses` from `1` to `2` and race two consumers for the final remaining use after one successful consumption.
2. Remove the per-capability gate from `AtomicInMemoryCapabilityUseStore` and rerun the concurrency test.
3. Add a `Stopped` or `Cancelled` state and decide how it should interact with remaining uses.
4. Replace the teaching store with a database-backed `TryConsumeAsync` design and state what database constraint or conditional update makes the transition atomic.
5. Add an operation/idempotency key that is intentionally different from `CapabilityId` and explain which duplicate each key controls.
6. Simulate an ambiguous store timeout after commit and design a retry contract that does not accidentally claim another use.
7. Add a recovery state for `consumed-but-execution-outcome-unknown` rather than pretending the side effect was exactly once.

Continue with the [Replay Protection and Bounded-Use Authority lab](../../docs/labs/replay-protection-and-bounded-use.md) for a guided concurrency exercise that begins from the intentionally unsafe implementation.

## Related Material

- [Replay Protection and Bounded-Use Authority](../../docs/security/replay-protection-and-bounded-use.md)
- [Replay Protection and Bounded-Use Authority lab](../../docs/labs/replay-protection-and-bounded-use.md)
- [Scoped Capability and Host-Owned Execution](../../docs/tutorials/scoped-capability-and-host-owned-execution.md)
- [Scoped Capability and Host-Owned Execution sample](../scoped-capability-and-host-owned-execution/README.md)
- [Data Access Boundaries and Transaction Reasoning](../../docs/aspnetcore/data-access-boundaries-and-transaction-reasoning.md)
- [Governed AI Tool Gateway](../../docs/tutorials/governed-ai-tool-gateway.md)
- [Capability Grant Hardening](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/capability-grant-hardening.md)

## License

Executable sample code under `samples/` is licensed under the MIT License. See [LICENSING.md](../../LICENSING.md).
