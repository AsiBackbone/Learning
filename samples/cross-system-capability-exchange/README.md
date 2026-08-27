# Cross-System Capability Exchange Sample

This sample is the runnable companion for:

[Cross-System Capability Exchange and Delegated Authority](../../docs/advanced/cross-system-capability-exchange-and-delegated-authority.md)

It simulates two independently operated fictional systems exchanging narrow authority for:

```text
records.export
```

The sample is intentionally framework-neutral and local. It does **not** implement a production federation protocol, real digital signatures, proof-of-possession cryptography, durable distributed replay state, or external side effects.

## What the Sample Demonstrates

System A creates a teaching artifact containing narrow continuation authority. System B independently validates:

- Recipient-owned issuer trust.
- Recipient-owned key acceptance **before** simulated proof verification.
- Audience binding.
- Presenter binding.
- Operation and resource binding.
- Exact-snapshot resource-version freshness.
- Purpose and request binding.
- Lifetime under the recipient clock.
- Direct one-hop delegation policy.
- Revocation state.
- Recipient-local policy.
- Atomic single-process bounded-use state.

Only after those checks pass does the gateway create a `ValidatedExportCommand` and invoke the host-owned dry-run executor.

```text
System A artifact
      ↓
System B issuer/key trust
      ↓
Simulated proof verification
      ↓
Bindings + lifetime + revocation
      ↓
Recipient-local decision
      ↓
Atomic use claim
      ↓
ValidatedExportCommand
      ↓
Executor-owned version + destination checks
      ↓
Dry-run side effect
```

The raw cross-system artifact never becomes the executor contract.

## Important Teaching Conventions

### Delegation depth

The sample keeps two concepts separate:

- `HopPosition` describes where a hop appears in a chain.
- `RemainingDelegationDepth` is the forward-looking delegation budget.

A direct System A → System B grant uses:

```text
HopPosition = 0
RemainingDelegationDepth = 0
```

`0` therefore means no further delegation is permitted; it does **not** mean that the current grant is at chain depth zero for comparison against a different counter.

The final recipient identity is not hardcoded inside the validator. It comes from `RecipientIssuerPolicy.RecipientSystemId`, keeping recipient trust configuration recipient-owned.

### Simulated proof

The article keeps `Proof` abstract because a real implementation may use a signature envelope, sender-constrained proof, opaque lookup, or another protocol-specific representation.

The sample uses a concrete `SimulatedProof` record so the verification boundary can be executed without real cryptographic material. Its `IsValid` flag is a test double, not a security control.

The validator intentionally resolves the issuer and confirms that `KeyId` is recipient-accepted **before** it checks the simulated proof. Real cryptographic verification similarly requires selecting an accepted verification key/trust anchor before verifying the presented proof.

### Presenter binding is not proof of possession

The sample compares an authenticated presenter string with `PresenterBinding`.

That is a policy binding only. A real sender-constrained or proof-of-possession protocol must verify control of the holder-specific key, certificate, mTLS identity, or equivalent mechanism required by that protocol.

### Exact-snapshot resource semantics

The sample chooses the strictest of the article's resource-drift options: **exact snapshot match**.

`ResourceVersion` is an opaque string, and System B rejects the grant when its pre-execution context no longer matches that exact version. The executor then checks the expected version again at the side-effect boundary so a later TOCTOU change is still rejected.

The sample does not implement the article's other valid strategies of recipient re-evaluation against a newer resource state or immutable-snapshot resolution.

### Replay-store scope

`ICapabilityUseStore` marks the substitution point for replay/use-state persistence. The included `InMemoryCapabilityUseStore` uses an in-process lock, so its atomicity guarantee covers one process only.

A multi-instance recipient needs a durable or distributed state transition whose consistency model is strong enough for the claimed replay guarantee, such as a conditional write or transaction in an appropriate shared store.

`UnavailableCapabilityUseStore` exists only to demonstrate fail-closed degraded behavior when the recipient cannot establish replay eligibility.

### Burned single-use grant

The gateway claims bounded use before execution. If execution then fails, the teaching grant remains consumed.

The sample deliberately does not reset it to `Available`, because an ambiguous failure may already have caused an external side effect in a real system. The gateway returns a structured `execution.failed` result instead of allowing the executor exception to escape across the boundary.

### Recipient decision and execution identities

Every recipient evaluation receives a `RecipientDecisionId`. Successful claims also receive an `ExecutionId`.

The sample deliberately derives one stable execution identity from the capability:

```text
ExecutionId = exec-{CapabilityId}
```

That teaching choice models an idempotency/reconciliation key for the one delegated operation. It is **not** an attempt identifier. `RecipientDecisionId`, `IssuerDecisionId`, `CapabilityId`, `ExecutionId`, and `CorrelationId` remain distinct so the evidence chain can be reconstructed without conflating their meanings.

### Executor-owned final invariants

The dry-run executor has its own current resource version and destination allowlist. Those checks happen after gateway validation and after the use claim, immediately before the simulated side effect.

This makes the article's confused-deputy and TOCTOU boundary observable: a valid delegated grant cannot force an executor with broader ambient credentials to use an unapproved destination or stale resource snapshot.

## Source Layout

The sample is split by responsibility so the trust boundary is easy to browse:

- `Program.cs` — console scenarios only.
- `SampleScenarios.cs` — fictional factories and recipient configuration.
- `Models.cs` — capability, policy, command, and result records.
- `Stores.cs` — trust, revocation, and replay/use-state abstractions plus in-memory teaching stores.
- `CrossSystemCapabilityValidator.cs` — recipient validation rules.
- `CrossSystemGateway.cs` — recipient decision, bounded-use claim, command creation, and failure mapping.
- `Executors.cs` — final executor invariants and simulated side effects.

## Run the Sample

From the repository root:

```bash
dotnet run --project samples/cross-system-capability-exchange/CrossSystemCapabilityExchange/CrossSystemCapabilityExchange.csproj
```

The console runs a valid exchange, wrong-audience rejection, resource-drift rejection, and a sequential replay attempt.

## Run the Tests

```bash
dotnet test samples/cross-system-capability-exchange/CrossSystemCapabilityExchange.Tests/CrossSystemCapabilityExchange.Tests.csproj
```

The focused xUnit suite protects these boundaries:

- Valid direct grant reaches the executor exactly once.
- Correct simulated proof + wrong audience produces zero executor calls.
- Trusted issuer + expired authority produces zero executor calls.
- Excessively long authority is rejected by recipient lifetime policy.
- Authority issued too far in the future is rejected under recipient clock-skew policy.
- Resource drift produces zero executor calls.
- Replayed second use does not duplicate protected execution.
- Unexpected chained delegation does not create implicit trust expansion.
- Recipient identity for delegation validation comes from recipient policy.
- Recipient-local policy can deny an otherwise valid sender artifact.
- Same audience + changed request binding produces zero executor calls.
- Authenticated but mismatched presenter produces zero executor calls.
- Unknown issuer produces zero executor calls.
- Unknown trust anchor is rejected before simulated proof validation can matter.
- Invalid proof under an accepted key produces zero executor calls.
- Revoked authority produces zero executor calls.
- Replay-store unavailability fails closed.
- Two **actually concurrent** single-use claims produce exactly one protected execution.
- Executor failure becomes a structured result while the single-use grant stays consumed.
- Executor-side resource-version drift blocks the side effect after gateway validation.
- Executor destination allowlist blocks an unapproved destination.
- Successful execution preserves originating subject and distinct issuer-decision, recipient-decision, capability, execution, and correlation identities.

## What This Sample Does Not Prove

The sample does not provide:

- Real signature verification.
- Production key custody or rotation.
- Cryptographic proof of possession.
- Cross-process or cross-region replay guarantees.
- Production revocation distribution.
- Exactly-once external execution.
- A federation/interoperability standard.
- A production export service.
- Compliance or security certification.

It exists to make the cross-system authority boundary executable without hiding it beneath production infrastructure.

---

> **Read it. Run it. Question it. Improve it.**
