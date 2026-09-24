# Decision Receipts and Acknowledgment Sample

This executable companion sample demonstrates the architectural boundary taught in the [Decision Receipts and Acknowledgment](../../docs/tutorials/decision-receipts-and-acknowledgment.md) tutorial.

The sample makes the acknowledgment lifecycle and its evidence visible:

```text
Intent
   ↓
Policy evaluation
   ↓
AcknowledgmentRequired
   ↓
Decision receipt
   ↓
Challenge issued
   ↓
Actor response
   ↓
Response validation
   ↓
Current context reconstructed
   ↓
Policy re-evaluated
   ↓
Fresh decision receipt
   ↓
Host-owned execution or stop
   ↓
Correlated lifecycle evidence
```

The central invariants are:

> **Acknowledgment is a governance boundary, not an execution bypass.**

and:

> **Decision, acknowledgment, re-evaluation, and execution should remain distinguishable evidence events.**

## Learning Objective

Observe how a consequential operation can pause for a narrowly bound acknowledgment, validate the response, re-evaluate current policy, and preserve a correlated audit timeline without treating acknowledgment as standing permission.

## Difficulty

Intermediate

## Prerequisites

- .NET 10 SDK
- [Decision Before Execution](../../docs/tutorials/decision-before-execution.md)
- [Policy Context and Explicit Decision Outcomes](../../docs/tutorials/policy-context-and-explicit-decision-outcomes.md)

## Run the Sample

From the repository root:

```bash
dotnet run --project samples/decision-receipts-and-acknowledgment/Sample/DecisionReceiptsAndAcknowledgment.csproj
```

## Run the Tests

From the repository root:

```bash
dotnet test samples/decision-receipts-and-acknowledgment/Tests/DecisionReceiptsAndAcknowledgment.Tests.csproj
```

The focused xUnit tests cover every policy outcome and acknowledgment binding failure, including the expiration boundary and stable reason codes. They also prove that acknowledgment does not grant execution authority, changed resource state can still block execution, and the executable scenarios preserve their correlated audit timelines.

The sample uses deterministic local data and does not call external services.

## Scenarios

The program evaluates five workflows:

| Scenario | Expected final state | Executor calls |
| --- | --- | ---: |
| Valid acknowledgment | `Executed` after re-evaluation returns `Allowed` | 1 |
| Rejected acknowledgment | `AcknowledgmentRejected` | 0 |
| Wrong actor | `AcknowledgmentInvalid` | 0 |
| Expired challenge | `AcknowledgmentInvalid` | 0 |
| Context drift after acknowledgment | `BlockedAfterReevaluation` with `EscalationRecommended` | 0 |

The context-drift scenario is especially important. The actor validly acknowledges the original requirement, but the resource becomes protected before continuation. Re-evaluation therefore recommends escalation instead of allowing execution.

That demonstrates:

```text
Valid acknowledgment
   ≠
Policy override
```

## What to Observe

### 1. The Challenge Is Narrowly Bound

The `AcknowledgmentChallenge` binds the request to:

- Challenge identity
- Actor
- Operation
- Resource
- Reason code
- Required acknowledgment code
- Correlation identifier
- Policy version
- Expiration

A generic `confirmed = true` value does not provide those bindings.

### 2. The Response Is Data, Not Authority

`AcknowledgmentResponse` records what the actor did.

The response is validated before the host considers continuation.

The validator rejects:

- Rejected acknowledgments
- Wrong challenge identity
- Wrong actor
- Wrong acknowledgment code
- Wrong correlation identifier
- Expired challenges

### 3. Acknowledgment Satisfies One Requirement

After a valid response, the host reconstructs current policy context with:

```text
RequiredAcknowledgmentSatisfied = true
```

The policy runs again.

Other constraints still apply.

The context-drift scenario changes the resource to protected after acknowledgment and confirms that the executor remains untouched.

### 4. Correlation Connects the Timeline

Every lifecycle event for a scenario carries the same correlation identifier, and decision-derived events reference a distinct `DecisionReceipt`.

A successful flow produces stages such as:

```text
decision
challenge-issued
acknowledgment-accepted
re-evaluation
execution-completed
```

A rejected or invalid response stops earlier and therefore leaves a shorter timeline.

### 5. Policy Identity Remains Visible

The sample carries `PolicyVersion` through the challenge and each decision receipt.

This keeps policy identity connected to the governed path without implying that version metadata alone creates tamper-evident proof.

## Executable Invariants

The program verifies its own expected behavior and throws if an invariant changes unexpectedly.

It checks that:

1. Rejected acknowledgment produces zero executor invocations.
2. A response from the wrong actor produces zero executor invocations.
3. An expired challenge produces zero executor invocations.
4. A valid acknowledgment can continue only after re-evaluation.
5. A newly active protected-resource constraint still blocks execution after acknowledgment.
6. Every lifecycle event in one workflow preserves the same correlation identifier.
7. The audit stage sequence matches the expected lifecycle.

The runtime checks remain useful because they make failures visible while learners execute the demonstration directly.

The companion xUnit project now provides structured test results for the same class of architectural invariants and is included in the shared sample solution for CI execution.

## Decision Receipt Is Not the Same as Logging

The sample prints the timeline to the console so the learner can observe it.

That console output is not presented as durable governance evidence.

The `DecisionReceipt` objects record evaluation outcomes and reasons. Separate `GovernanceLifecycleEvent` objects correlate acknowledgment and execution progress without implying that the original decision proves execution.

The combined evidence includes:

- Event identity
- Actor
- Operation
- Outcome
- Reason codes
- Correlation
- Policy version
- Lifecycle stage

A production system would still need to decide how receipts and lifecycle events are persisted, protected, retained, delivered, and possibly signed.

Do not infer from this sample that an in-memory list or console output is:

- Immutable
- Tamper-proof
- Durable
- Non-repudiable
- Compliance-ready

## What This Sample Intentionally Omits

This is a teaching artifact, not a production acknowledgment service. It intentionally omits:

- Authentication infrastructure
- Durable challenge persistence
- Durable audit storage or outbox delivery
- Cryptographic challenge binding
- Signing and key management
- Replay or challenge-consumption storage
- Distributed tracing infrastructure
- HTTP transport
- Database transactions
- Real account modification
- Scoped capability issuance
- The fuller `AsiBackbone` package abstractions

The executor only records invocation count. No account is actually disabled.

Scoped execution authority is intentionally left for the next tutorial.

## Try It

Useful experiments include:

1. Change the response challenge ID and confirm that validation stops the workflow.
2. Change the response correlation identifier and add a scenario for `acknowledgment.correlation-mismatch`.
3. Add a one-time challenge-consumption flag and demonstrate why replay state needs persistence.
4. Add a policy version change between challenge issuance and acknowledgment, then decide whether the host should reject or re-evaluate under the new policy.
5. Add a durable `IDecisionReceiptStore` abstraction backed by an in-memory implementation.
6. Simulate an execution failure and add a distinct `execution-failed` lifecycle event instead of rewriting the `Allowed` decision receipt.
7. Add a policy hash or fingerprint and discuss what additional architecture is required before calling the resulting history tamper-evident.

## Related Material

- [Decision Receipts and Acknowledgment tutorial](../../docs/tutorials/decision-receipts-and-acknowledgment.md)
- [Decision Receipts and Acknowledgment intermediate lab](../../docs/labs/decision-receipts-and-acknowledgment.md)
- [Policy Context and Explicit Decision Outcomes sample](../policy-context-and-explicit-decision-outcomes/README.md)
- [Scoped Capability and Host-Owned Execution](../../docs/tutorials/scoped-capability-and-host-owned-execution.md)
- [`LiabilityHandshakeRequest`](https://github.com/AsiBackbone/AsiBackbone/blob/v6.0.0/src/AsiBackbone.Core/Handshakes/LiabilityHandshakeRequest.cs) - compare the teaching challenge with the fuller working handshake request.
- [`LiabilityHandshakeAcknowledgment`](https://github.com/AsiBackbone/AsiBackbone/blob/v6.0.0/src/AsiBackbone.Core/Handshakes/LiabilityHandshakeAcknowledgment.cs) - inspect the working acknowledgment model.
- [`DecisionReceipt`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Audit/DecisionReceipt.cs) - compare the small teaching receipt with the framework's decision-outcome record.
- [`DecisionReceiptLifecycleEvent`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Audit/DecisionReceiptLifecycleEvent.cs) - compare the sample's correlated lifecycle events with the framework's acknowledgment, capability, gateway, and emission stages.
- [`Dynamic Liability Handshake`](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/dynamic-liability-handshake.md) - review the fuller handshake lifecycle.
- [`Durable Audit Outbox Persistence`](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/durable-audit-outbox-persistence.md) - review production-oriented persistence and delivery concerns.

## License

Executable sample code under `samples/` is licensed under the MIT License. See [LICENSING.md](../../LICENSING.md).
