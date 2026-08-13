# Acknowledgment and Audit Residue Sample

This executable companion sample demonstrates the architectural boundary taught in the [Acknowledgment and Audit Residue](../../docs/tutorials/acknowledgment-and-audit-residue.md) tutorial.

The sample makes the acknowledgment lifecycle and its evidence visible:

```text
Intent
   ↓
Policy evaluation
   ↓
AcknowledgmentRequired
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
Host-owned execution or stop
   ↓
Audit residue
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
dotnet run --project samples/acknowledgment-and-audit-residue/AcknowledgmentAndAuditResidue/AcknowledgmentAndAuditResidue.csproj
```

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

Every `AuditResidue` for a scenario carries the same correlation identifier.

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

The sample carries `PolicyVersion` through the challenge and audit residue.

This keeps policy identity connected to the governed path without implying that version metadata alone creates tamper-evident proof.

## Executable Invariants

The program verifies its own expected behavior and throws if an invariant changes unexpectedly.

It checks that:

1. Rejected acknowledgment produces zero executor invocations.
2. A response from the wrong actor produces zero executor invocations.
3. An expired challenge produces zero executor invocations.
4. A valid acknowledgment can continue only after re-evaluation.
5. A newly active protected-resource constraint still blocks execution after acknowledgment.
6. Every residue in one workflow preserves the same correlation identifier.
7. The audit stage sequence matches the expected lifecycle.

This follows the current Learning sample convention of making architectural checks executable inside the sample until a repository-level sample test structure is established.

## Audit Residue Is Not the Same as Logging

The sample prints the timeline to the console so the learner can observe it.

That console output is not presented as durable governance evidence.

The `AuditResidue` objects model evidence-oriented data such as:

- Event identity
- Actor
- Operation
- Outcome
- Reason codes
- Correlation
- Policy version
- Lifecycle stage

A production system would still need to decide how residue is persisted, protected, retained, delivered, and possibly signed.

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
5. Add a durable `IAuditResidueStore` abstraction backed by an in-memory implementation.
6. Simulate an execution failure and add a distinct `execution-failed` residue instead of rewriting the `Allowed` decision.
7. Add a policy hash or fingerprint and discuss what additional architecture is required before calling the resulting history tamper-evident.

## Related Material

- [Acknowledgment and Audit Residue tutorial](../../docs/tutorials/acknowledgment-and-audit-residue.md)
- [Acknowledgment and Audit Residue intermediate lab](../../docs/labs/acknowledgment-and-audit-residue.md)
- [Policy Context and Explicit Decision Outcomes sample](../policy-context-and-explicit-decision-outcomes/README.md)
- [Scoped Capability and Host-Owned Execution](../../docs/tutorials/scoped-capability-and-host-owned-execution.md)
- [`LiabilityHandshakeRequest`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Handshakes/LiabilityHandshakeRequest.cs) - compare the teaching challenge with the fuller working handshake request.
- [`LiabilityHandshakeAcknowledgment`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Handshakes/LiabilityHandshakeAcknowledgment.cs) - inspect the working acknowledgment model.
- [`AuditResidue`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Audit/AuditResidue.cs) - compare the small teaching residue with the framework's richer governance evidence model.
- [`Dynamic Liability Handshake`](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/dynamic-liability-handshake.md) - review the fuller handshake lifecycle.
- [`Durable Audit Outbox Persistence`](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/durable-audit-outbox-persistence.md) - review production-oriented persistence and delivery concerns.

## License

Executable sample code under `samples/` is licensed under the MIT License. See [LICENSING.md](../../LICENSING.md).
