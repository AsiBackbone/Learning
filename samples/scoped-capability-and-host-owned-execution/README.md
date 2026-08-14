# Scoped Capability and Host-Owned Execution Sample

This executable companion sample demonstrates the architectural boundary taught in the [Scoped Capability and Host-Owned Execution](../../docs/tutorials/scoped-capability-and-host-owned-execution.md) tutorial.

The sample keeps approval, narrow execution authority, validation, and the host-owned side effect distinct:

```text
Governance decision
   ↓
Capability issued
   ↓
Host reconstructs current execution context
   ↓
Capability validated against that current context
   ↓
Host-owned gateway
   ↓
Simulated executor or stop
```

The central invariants are:

> **A blocked decision cannot mint execution authority.**

> **Expired or stale execution authority never reaches the executor.**

## Learning Objective

Observe how an allowed decision can produce a short-lived capability that remains bound to a specific actor, operation, resource, audience, policy identity, acknowledgment reference, intended use, and expiration window.

Then observe how the host validates those bindings immediately before execution rather than treating issuance-time approval as standing permission.

The sample begins at the Tutorial 4 boundary after the earlier governance flow has produced an `Allowed` decision. The fixed `ack-77` value represents a prior acknowledgment reference being carried forward; this sample does not repeat the acknowledgment workflow already demonstrated by Tutorial 3.

## Difficulty

Intermediate

## Prerequisites

- .NET 10 SDK
- [Decision Before Execution](../../docs/tutorials/decision-before-execution.md)
- [Policy Context and Explicit Decision Outcomes](../../docs/tutorials/policy-context-and-explicit-decision-outcomes.md)
- [Acknowledgment and Audit Residue](../../docs/tutorials/acknowledgment-and-audit-residue.md)

## Run the Sample

From the repository root:

```bash
dotnet run --project samples/scoped-capability-and-host-owned-execution/ScopedCapabilityAndHostOwnedExecution/ScopedCapabilityAndHostOwnedExecution.csproj
```

The sample evaluates deterministic execution-boundary scenarios:

| Scenario | Expected result | Executor calls |
| --- | --- | ---: |
| Valid capability | `capability.valid` | 1 |
| Expired capability | `capability.expired` | 0 |
| Resource changed after approval | `capability.resource-version-mismatch` | 0 |
| Wrong resource | `capability.resource-mismatch` | 0 |
| Wrong actor | `capability.subject-mismatch` | 0 |
| Wrong operation | `capability.operation-mismatch` | 0 |
| Wrong audience | `capability.audience-mismatch` | 0 |

It also verifies that a denied decision cannot produce a capability at all.

No real account operation occurs. The executor records and prints a simulated host action.

## Run the Tests

From the repository root:

```bash
dotnet test samples/scoped-capability-and-host-owned-execution/ScopedCapabilityAndHostOwnedExecution.Tests/ScopedCapabilityAndHostOwnedExecution.Tests.csproj
```

Or run the complete sample suite:

```bash
dotnet test samples/Samples.slnx
```

The focused tests make these architectural contracts independently executable:

```text
Blocked decision
   ↓
No capability
```

```text
Expired capability
   ↓
Validation fails
   ↓
Executor invocation count = 0
```

```text
Resource changed after approval
   ↓
Resource-version binding no longer matches
   ↓
Executor invocation count = 0
```

## What to Observe

### Capability Issuance Follows the Decision

`ExecutionCapabilityFactory` refuses to create execution authority from a blocked decision.

The capability is therefore a consequence of governance, not a substitute for governance.

### The Capability Is Narrow

The teaching capability carries:

- Issuer
- Audience
- Subject / actor
- Operation
- Resource identity
- Resource version
- Scope
- Issued time
- Expiration
- Policy version
- Acknowledgment reference
- Intended use

A broad session flag such as `ApprovedForAdminActions = true` would discard most of that scope and lineage information.

### Validation Happens at the Execution Boundary

`CapabilityValidationRequest` represents the host's current execution context. In this teaching sample, those facts are constructed deterministically; in a real host they should come from authoritative actor, resource, policy, and gateway state rather than being trusted merely because a caller supplied them.

`DisableAccountGateway` validates the capability immediately before invoking the host-owned executor.

The capability itself performs no side effect.

The validator performs no side effect.

The policy decision performs no side effect.

Only the host-owned gateway may call the executor after validation succeeds.

### Resource Identity and Resource State Are Different Bindings

`ResourceId` protects against substituting a different target.

The sample also carries a simple integer `ResourceVersion` so a learner can observe a second problem: the same resource may change after approval.

```text
Approved resource
user-100 version 7
   ↓
Capability issued
   ↓
Current resource becomes version 8
   ↓
Capability rejected as stale
```

`ResourceVersion` is an educational stand-in for a host-specific state binding such as an ETag, row version, revision number, fingerprint, or a fresh authorization/resource check.

A production system should choose a binding appropriate to the resource and risk model rather than copying this integer mechanically.

## Important Time Semantics

The sample treats:

```text
IssuedUtc <= NowUtc < ExpiresUtc
```

as the valid time window.

Expiration is therefore exclusive. A request evaluated exactly at `ExpiresUtc` is rejected.

The sample does not add clock-skew tolerance. Production hosts that need tolerance should make it explicit and bounded because tolerance enlarges the effective authority window.

## What This Sample Intentionally Omits

This is a teaching artifact, not a production authorization service. It intentionally omits:

- Authentication infrastructure
- Cryptographic signing or proof verification
- Secure capability transport
- Durable capability storage
- Durable replay or use-count enforcement
- Revocation storage
- Distributed clock coordination
- Database transactions
- Real account modification
- Production resource-version mechanics
- The fuller `AsiBackbone` package abstractions

A plain in-memory capability object is not presented as a secure bearer token.

## Try It

Useful experiments include:

1. Remove the expiration check and rerun the invariant tests.
2. Remove the resource-version check and observe why resource identity alone does not detect state drift.
3. Broaden the scope from `account.disable` to `account.*` and identify the additional authority exposed if the capability leaks.
4. Change the audience to another gateway and decide whether cross-gateway reuse should be allowed.
5. Add `NotBeforeUtc` and test the exact lower time boundary.
6. Add a single-use store and demonstrate first-use success followed by replay rejection.
7. Record capability issuance and validation as distinct audit-residue events.

## Related Material

- [Scoped Capability and Host-Owned Execution tutorial](../../docs/tutorials/scoped-capability-and-host-owned-execution.md)
- [Scoped Capability and Host-Owned Execution intermediate lab](../../docs/labs/scoped-capability-and-host-owned-execution.md)
- [Acknowledgment and Audit Residue sample](../acknowledgment-and-audit-residue/README.md)
- [`CapabilityTokenGrant`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityTokenGrant.cs) - compare the teaching capability with the working framework's provider-neutral grant metadata.
- [`CapabilityGrantValidator`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityGrantValidator.cs) - inspect fuller execution-context validation.
- [`ICapabilityGrantUseStore`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/ICapabilityGrantUseStore.cs) - review the working seam for bounded-use and replay-state enforcement.
- [Capability Grant Hardening](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/capability-grant-hardening.md) - review production-oriented proof, binding, time, replay, and failure guidance.
- [Intent to Execution Pattern](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/intent-to-execution-pattern.md) - place capability validation in the fuller governed flow.

## License

Executable sample code under `samples/` is licensed under the MIT License. See [LICENSING.md](../../LICENSING.md).
