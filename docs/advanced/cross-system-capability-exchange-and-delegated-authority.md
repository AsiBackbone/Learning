---
description: Learn how independently operated systems can exchange narrow delegated authority without turning signatures, token possession, or sender trust into implicit recipient authorization.
---

# Cross-System Capability Exchange and Delegated Authority

**Learning objective:** Understand what changes when scoped continuation authority crosses into a separately operated system with its own trust anchors, identities, policies, clocks, replay state, resource state, and host-owned execution boundary.

**Pattern classification:** General learning material

**Advanced area:** Cross-system security and governance

**Difficulty:** Advanced

**Required prerequisites:** [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) and [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md).

**Recommended background:** [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md), [Signing, Verification, Key Custody, and Tamper Evidence](../security/signing-verification-key-custody-and-tamper-evidence.md), and [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md).

**Glossary:** [Scoped capability](../architecture/glossary.md#scoped-capability), [capability token](../architecture/glossary.md#capability-token), [execution authority](../architecture/glossary.md#execution-authority), [host-owned execution](../architecture/glossary.md#host-owned-execution), and [trust boundary](../architecture/glossary.md#trust-boundary).

> **Framework-neutral scope:** This article teaches authority-transfer semantics and trust boundaries. It does not define a new interoperability standard, token format, signature profile, federation protocol, or production key-management scheme.

## Why This Matters

A capability that is narrow and trustworthy inside one system can become dangerously ambiguous when another independently operated system receives it. The receiver has different trust anchors, policy state, resource state, replay state, clocks, credentials, and operational ownership. A signed handoff therefore creates a new validation problem rather than eliminating authorization.

The core lesson is simple:

> **Authority does not become trustworthy merely because it crossed a boundary in a signed token. The receiving system must independently decide whether the issuer, audience, scope, freshness, delegation chain, and current execution context are acceptable.**

## Pattern Card

> **Problem:** A narrow capability that is meaningful inside System A can be mistaken for unconditional permission when it arrives at independently operated System B.
>
> **Pattern:** Treat the received artifact as evidence of a proposed delegation. System B authenticates the presenter, verifies the artifact against explicitly configured issuer trust, validates audience/scope/lifetime/delegation/replay state, rebuilds current local context, applies recipient-local policy, and only then creates a validated command for host-owned execution.
>
> **Use when:** A protected operation must continue across an organizational, administrative, service-ownership, or trust boundary and the recipient needs narrow authority that is distinct from broad standing service permissions.
>
> **Prefer something simpler when:** One trusted host can authorize and execute immediately, or conventional service authentication plus recipient-local authorization already expresses the required authority without a portable delegated grant.
>
> **Observe:** A correct signature with the wrong audience, an expired grant from a trusted issuer, a replayed single-use grant, or an untrusted delegation chain all produce zero protected executor calls.

---

## 1. The Boundary Changes the Question

Inside one host, capability issuance and validation may share one coherent trust model. Across independently operated systems, that assumption disappears.

System A and System B may differ in identity providers, service identities, trust anchors, key lifecycles, policy engines, resource stores, clocks, replay stores, revocation mechanisms, execution credentials, incident response, and operational ownership.

The sender can state that it authorized a narrow continuation. The receiver still has to determine whether it trusts the issuer for this purpose, whether the artifact is intended for this audience, whether the presenter is acceptable, whether operation/resource/request bindings still match, whether the artifact is fresh and unused, whether the delegation path is acceptable, whether local policy permits continuation, and whether the current resource can still be acted upon safely.

Cross-system exchange therefore adds a second decision boundary rather than removing one.

---

## 2. Running Scenario: `records.export`

Use two fictional, independently operated systems.

```text
System A                      System B
--------                      --------
Request
  |
Current sender policy
  |
Narrow continuation grant
  |-----------------------------> Receive artifact
                                  Authenticate presenter
                                  Verify issuer / proof
                                  Validate audience + bindings
                                  Validate lifetime + revocation
                                  Validate replay eligibility
                                  Rebuild current local context
                                  Apply recipient-local policy
                                  Atomically claim bounded use
                                  Build validated local command
                                  Host-owned execution
```

System A evaluates this fictional request:

| Field | Value |
| --- | --- |
| Originating actor | `analyst-17` |
| Operation | `records.export` |
| Resource | `record-set-42` |
| Resource version | `snapshot-8` |
| Destination | `system-b` |
| Purpose | `regulatory-review` |

System A returns `Allowed` and may create narrow continuation authority for System B.

A minimal JSON-like teaching artifact might contain:

```json
{
  "capabilityId": "cap-a-784",
  "issuer": "system-a",
  "audience": "system-b:records-export",
  "originatingSubject": "analyst-17",
  "presenterBinding": "system-b-export-worker",
  "operation": "records.export",
  "resourceId": "record-set-42",
  "resourceVersion": "snapshot-8",
  "purpose": "regulatory-review",
  "requestDigest": "sha256:example-request-001",
  "issuerDecisionId": "dec-a-551",
  "issuerPolicyVersion": "4.2",
  "issuedAtUtc": "2032-04-10T12:00:00Z",
  "expiresAtUtc": "2032-04-10T12:05:00Z",
  "maxUses": 1,
  "remainingDelegationDepth": 0
}
```

This is illustrative data, not a JWT, CWT, macaroon, or proposed wire format.

`ResourceVersion` is intentionally modeled as an opaque string. Real systems may use an ETag, immutable snapshot identifier, revision hash, database version, or another host-defined concurrency token.

These fields are useful evidence. They do **not** mean that System B must execute.

---

## 3. Terminology Used in This Article

Several terms need precise meanings because delegation bugs often begin as naming ambiguity.

| Term | Meaning in this article |
| --- | --- |
| Issuer | System that creates or protects the authority artifact. |
| Audience | Exact receiving authority boundary permitted to accept the artifact. |
| Originating subject | Human or system principal whose original request is preserved for provenance and scope. |
| Presenter | Workload or channel identity that actually delivers the artifact to the recipient. |
| Request binding | A stable identity or digest for the exact request semantics the grant is intended to authorize. |
| Recipient-local policy | Policy evaluated by the receiving system using its current authoritative facts. |
| Resource drift | Relevant resource state differs from the state assumed when authority was issued. |
| Replay claim | Recipient-owned state transition that determines whether another use is still permitted. |
| Delegation hop position | Descriptive position in a chain. Position `0` is the root issuance, `1` is the first derived grant, and so on. |
| Remaining delegation depth | Forward-looking budget carried by a grant. `0` means the current holder may not delegate further. |

The last two definitions are intentionally different.

For example, a direct one-hop grant from System A to System B may have:

```text
HopPosition = 0
RemainingDelegationDepth = 0
```

If System A intentionally permits System C to derive exactly one child grant, the grant delivered to C may have `RemainingDelegationDepth = 1`; the child grant C derives for B must reduce that budget to `0`.

> **Hop position describes where a grant is in a chain. Remaining delegation depth describes how much future delegation authority is left.**

---

## 4. Capability Exchange Is Not a Token Standard

The lesson is about semantics, not serialization.

Cross-system authority might be represented by a self-contained signed object, an opaque reference resolved against authoritative state, a signed request plus server-side grant state, a mutually authenticated request whose narrow authorization data is looked up separately, or another deliberately bounded mechanism.

A common token format does not by itself solve issuer identity, trust anchors, audience semantics, subject mapping, operation vocabulary, resource identity, delegation rules, clock rules, replay behavior, revocation, policy meaning, error behavior, or evidence retention.

Two systems can parse the same bytes and still disagree about every authority question that matters.

---

## 5. Four Questions Must Remain Separate

| Question | Example | Typical owner |
| --- | --- | --- |
| Who is presenting this request? | `worker-b-7` | Recipient authentication / workload identity |
| Did an expected issuer protect this artifact? | `system-a`, key `a-2032-04` | Recipient verification against configured trust anchors |
| May this operation execute now? | `records.export` on `record-set-42` | Recipient binding/freshness/replay checks + local policy |
| Who performs the side effect? | Export executor using System B credentials | Recipient host |

The useful separation is:

```text
Authentication
    !=
Artifact integrity / issuer authenticity
    !=
Authorization / governance
    !=
Execution
```

A correct signature does not answer the authorization question. Authentication of the presenter does not prove authority for this exact operation. An allowed recipient decision still does not mean the raw artifact should reach an executor.

---

## 6. Issuer Trust Is Recipient-Owned

System A cannot declare itself trusted by writing `Issuer = system-a`.

System B needs recipient-owned configuration defining which issuers and trust anchors are recognized, which artifact types and operations they may delegate, which resource namespaces and audiences are acceptable, whether chained delegation is permitted, and what lifetime/delegation budgets are accepted.

A conceptual recipient rule might be:

```text
Trusted issuer: system-a
Accepted key IDs: a-2032-04, a-2032-05
May delegate: records.export
Resource namespace: shared-records/*
Accepted audience: system-b:records-export
Maximum grant lifetime: 5 minutes
Maximum remaining delegation depth: 0
```

The values above are teaching defaults, not a universal security standard. The example deliberately uses a five-minute maximum lifetime and later examples use 30 seconds of clock skew so learners can see bounded behavior. Production values should be justified by transport latency, failure/retry behavior, operational recovery, and consequence severity rather than copied mechanically.

> **Issuer trust is purpose-bound recipient policy, not universal trust.**

---

## 7. Audience Binding and Request Binding Solve Different Problems

Audience binding prevents a grant intended for one receiving boundary from wandering to another.

A grant for `system-b:records-export` should not be accepted by `system-c:records-import` or even an unrelated System B executor such as `system-b:account-admin`.

That protection is necessary but not sufficient.

A grant can have the correct audience and still be attached to a different request at that same audience. This becomes especially important when `MaxUses > 1`, when payloads can be reordered, or when several operations share the same endpoint.

Bind the authority to the request semantics that would materially change the side effect. Depending on the protocol, that may be a normalized operation/resource/purpose tuple, a canonical body digest, a method/path/body digest, a message ID with recipient-owned lookup, or another stable request identity.

```text
Audience binding
  -> Which authority boundary may consume this?

Request binding
  -> Which exact request semantics may it authorize there?
```

A correct proof with the wrong audience or mismatched request binding must not reach protected execution.

---

## 8. Originating Subject, Presenter, Bearer Authority, and Proof of Possession

Cross-system workflows often contain at least two identities:

- The originating subject, such as `analyst-17`.
- The presenting workload, such as `worker-b-7`.

Do not silently replace one with the other. The first preserves origin and delegated scope. The second is authenticated by the recipient according to its own identity model.

A separate design decision is whether the artifact is **bearer** or **sender-constrained / proof-of-possession** authority.

### Bearer-style authority

Possession of the artifact is enough to present it, subject to every other recipient check. Theft of the artifact therefore creates a replay/presentation risk until expiry, revocation, or use-state enforcement blocks it.

### Sender-constrained or proof-of-possession authority

The recipient additionally requires evidence that the presenter controls a bound key, certificate, mutually authenticated channel identity, or other holder-specific proof. This can reduce the value of artifact theft, but only if the binding is actually cryptographically enforced.

A field such as `PresenterBinding = system-b-export-worker` is only a policy binding. By itself it is **not** cryptographic proof of possession.

The companion sample intentionally simulates presenter binding and fake proof verification; it does not claim to implement production proof-of-possession cryptography.

> **A capability does not replace authentication, and a presenter-class check does not become proof of possession merely because it appears inside a signed artifact.**

---

## 9. Operation, Resource, and Resource Meaning Must Survive the Handoff

If System A authorized `records.export` for `record-set-42`, System B must not let the handoff become `records.*` for every record set.

Bindings may include operation, resource ID, resource version, tenant or namespace, destination, purpose, record subset, output classification, or maximum result size. Bind the values whose substitution would broaden or materially alter the protected side effect.

Cross-system exchange is also unsafe when both systems use the same text identifier with different semantics. `record-set-42` might mean an immutable tenant snapshot in System A and the current global record set in System B.

Recipient-owned mapping should therefore qualify, translate, or reject foreign identifiers. Useful techniques include namespace-qualified IDs, immutable snapshots, tenant-qualified references, mapping registries, and canonical resource identifiers.

The portable artifact should not force System B to treat System A's local database key as universal truth.

---

## 10. Lifetime Is Evaluated by the Recipient's Clock

System A may state that a grant expires at `2032-04-10T12:05:00Z`. System B decides whether that timestamp is acceptable now.

Recipient policy should define the trusted clock source, maximum accepted lifetime, permitted skew, treatment of `NotBefore`/future `IssuedAt` values, and degraded behavior when time synchronization is unhealthy.

For teaching examples in this article:

- Maximum grant lifetime: **5 minutes**.
- Maximum accepted skew: **30 seconds**.

These are concrete sample values, not universal production recommendations.

A recipient should not let an issuer extend authority by asserting that the issuer's clock is behind. If System B cannot trust its own current time strongly enough for a protected time-bounded operation, `Defer` or fail-closed behavior is safer than guessing.

---

## 11. Sender Policy Provenance Is Historical Evidence, Not Current Recipient Authorization

System A may preserve:

```text
IssuerDecisionId: dec-a-551
IssuerPolicyId: records-export
IssuerPolicyVersion: 4.2
IssuerPolicyFingerprint: sha256:example-a42
```

That evidence answers which sender-side policy produced the delegation.

System B still needs its own current decision provenance, for example:

```text
RecipientDecisionId: dec-b-901
RecipientPolicyId: inbound-record-export
RecipientPolicyVersion: 9.1
```

Historical sender evidence should not be rewritten to look current. Recipient-local policy should not be omitted merely because sender evidence exists.

```text
Issuer decision provenance
          +
Recipient current decision
          +
Current execution-boundary validation
```

All three may matter, but they answer different questions.

---

## 12. Signing Proves Less Than Many Designs Assume

Successful signature verification can support statements such as: protected bytes were not modified under the verification model, an accepted key produced the proof, and the configured issuer/key relationship was valid for verification.

It does **not** prove that the audience is correct, the request binding matches, the presenter is acceptable, the resource is current, the artifact is unused or unrevoked, the delegation chain is trusted, recipient policy allows execution, or the side effect is safe now.

> **Signing protects an artifact. It does not create recipient authorization.**

---

## 13. Delegation Is a Constrained Derivation, Not Transitive Trust

The easiest shape to reason about is direct one-hop delegation:

```text
System A
   |
   | direct grant, RemainingDelegationDepth = 0
   v
System B
```

No intermediary may derive new authority.

Now consider a deliberately permitted chain:

```text
System A
   |
   | grant to C, RemainingDelegationDepth = 1
   v
System C
   |
   | derived grant to B, RemainingDelegationDepth = 0
   v
System B
```

This does not mean `A trusts C` plus `C trusts B` automatically creates `A trusts B`, nor that B must trust A because a trusted C said so. The receiving system explicitly decides which roots, intermediaries, operations, audiences, and chain lengths it accepts.

A derived child must not broaden the parent. In prose, the child must stay within the parent's operation/resource/purpose/request scope, must not outlive the parent, must not exceed the parent's current bounded-use allowance, and must reduce the remaining delegation budget.

The bounded-use point needs one nuance: `RemainingAllowedUses` is normally recipient/issuer-side mutable state, not a trustworthy field that can simply be copied from the artifact. A delegating component must consult the authoritative use-state it owns before deriving a child. The child artifact can state its own `MaxUses`, but the derivation rule depends on current parent state.

A useful derivation rule is:

```text
Parent.RemainingDelegationDepth must be > 0
Child.RemainingDelegationDepth < Parent.RemainingDelegationDepth
Child.ExpiresAt <= Parent.ExpiresAt
Child scope cannot broaden parent scope
Child MaxUses cannot exceed current parent use budget
```

`DelegationHop.HopPosition` is only the chain index; it is never compared to the remaining-depth budget.

If every hop carries a separately protected proof, model that as nested or chained protected envelopes. Do not assume one outer signature over a mutable `DelegationChain` list proves every intermediary derivation independently.

---

## 14. Recipient-Local Governance Must Remain Authoritative

System B should not become a remote executor for System A's policy engine.

Even when System A is trusted to create narrow authority, System B still owns facts such as current resource existence, local classification, regional/tenant policy, maintenance state, destination restrictions, rate limits, and current risk conditions.

A useful receiving flow is:

```text
Sender-authorized continuation
          |
          v
Recipient resolves current facts
          |
          v
Recipient policy / authorization
          |
          v
Recipient execution decision
```

The recipient also should not inherit the sender's entire identity, role, or trust model. A sender claim such as `role=Administrator` may be useful provenance, but it should not silently become a standing System B role unless System B explicitly maps and authorizes that relationship.

---

## 15. Resource and Policy Drift Happen Between Issuance and Execution

### Resource drift

```text
12:00  System A authorizes snapshot-8
12:02  System B maps the foreign resource
12:03  Resource advances to snapshot-9
12:04  Capability arrives
```

A still-valid signature does not freeze resource state. Depending on the contract, System B may reject, re-evaluate, resolve an immutable snapshot, or execute only if the executor can enforce the expected version atomically at the data boundary.

### Policy drift

```text
12:00  System A issues under sender policy 4.2
12:02  System B deploys recipient policy 9.2
12:04  Capability arrives
```

Sender provenance remains truthful historical evidence. Recipient current policy still decides whether continuation is acceptable now.

> **Fresh proof is not the same thing as fresh authority.**

---

## 16. Replay State Belongs at the Receiving Execution Boundary

A token can prove neither that it has never been used nor that another recipient replica has not already accepted it.

For replay-sensitive authority, the recipient needs a stable capability identity and recipient-owned use state. Static validation should occur before consumption, then the final use decision should be an atomic state transition immediately before protected execution.

A conceptual single-use claim is:

```text
claim_if_available(capability_id, expected_state):
    atomically compare current use-state
    if already consumed -> reject
    else transition to claimed/consumed
```

In a multi-instance recipient, "atomic" means atomic across every replica covered by the security guarantee. An in-memory lock protects only one process. A production design may need a datastore conditional write, transactional compare-and-swap, strongly consistent coordinator, or another mechanism whose consistency model actually supports the claim. High throughput does not remove this requirement; it makes the storage and partitioning design more important.

### Replay protection is not idempotency

Replay protection decides whether another logical use of authority is accepted. Idempotency controls duplicate effects for one accepted logical execution. Both may be necessary.

### A single-use grant can be burned

If the recipient atomically claims a single-use grant and the executor then fails, the grant is still spent unless the protocol explicitly defines a safe rollback state transition.

```text
Claim succeeds
   |
   v
Execution fails
   |
   v
Grant remains consumed
```

Automatically restoring the grant to `Available` can create duplicate execution when failure is ambiguous. Retry therefore needs a stable execution identity and explicit recovery/reconciliation semantics, not a hidden replay reset.

---

## 17. Revocation, Invalidation, and Trust-Anchor Rotation Need Recipient-Usable Semantics

Short lifetime reduces some revocation pressure but does not eliminate it.

The recipient needs a usable rule for issuer/key compromise, specific grant revocation, subject or resource invalidation, and emergency trust withdrawal. A sender-side revocation database is irrelevant if System B cannot consult or receive it within the required time window.

Key rotation also must not become trust expansion. System B decides which old and new keys are accepted, for which artifact types and time ranges, whether historical verification remains possible, and how compromise changes those rules.

A mathematically valid signature under an unknown key is still an untrusted artifact.

---

## 18. Verification Failure and Degraded Mode Must Be Explicit

Cross-system validation may depend on trust metadata, revocation state, replay state, recipient policy, resource lookup, or time synchronization.

When one of those dependencies is unavailable, the implementation should not accidentally convert uncertainty into permission.

Possible explicit outcomes include `Deny`, `Defer`, `Escalate`, or a narrowly limited degraded path whose safety assumptions are documented. High-consequence operations generally should fail closed when the system cannot establish issuer trust, replay state, or current local authorization.

The exact degraded behavior is application-specific; what matters is that it is an intentional policy decision rather than an exception path that falls through to execution.

---

## 19. Cross-System Evidence Requires Two Audit Obligations

System A and System B are independently operated, so neither should assume that one shared log is the whole system of record.

System A may need evidence for its request, decision, issued capability, and delivery attempt. System B may need evidence for receipt, presenter identity, verification, recipient decision, replay claim, execution attempt, and final result.

Useful cross-system correlation may include:

| Evidence | Typical owner |
| --- | --- |
| `CorrelationId` | Shared workflow identity |
| `IssuerDecisionId` | System A |
| `CapabilityId` | Authority lifecycle |
| `RecipientDecisionId` | System B |
| `ExecutionId` | System B execution lifecycle |
| Delivery/attempt IDs | Transport participants |

B may execute successfully while A never receives the outcome. That is not an audit contradiction; it is a distributed-systems state that needs reconciliation. Decide which facts each system treats as authoritative and how eventual outcome exchange is retried or reconciled.

### Minimize evidence across the boundary

Do not treat correlation as permission to replicate sensitive identities or policy internals everywhere. Where appropriate, use pseudonymous identifiers, stable hashes, coarse reason categories, or locally retained mappings rather than copying raw subject/resource claims into public telemetry.

### Rejection reasons can leak policy

The recipient's internal reason may be `resource.exists-but-classification-blocked` while the external response should be a coarser `request.not-accepted`. Detailed denial reasons can disclose resource existence, key lifecycle, policy thresholds, tenant configuration, or replay state across an administrative boundary.

Preserve detailed internal evidence where justified; expose only the rejection detail the sender is entitled to know.

---

## 20. Recipient Ambient Authority Creates a Confused-Deputy Risk

A narrow delegated grant limits what System B is **asked** to do. It does not automatically limit the credentials held by System B's executor.

The export executor may run under an identity that can read many record sets, write to several destinations, or invoke broader infrastructure than the grant permits. If raw artifact fields flow directly into that executor, the recipient can become a confused deputy: a narrowly scoped request drives a component with much broader ambient authority.

Mitigate this by translating accepted authority into a validated local command containing only the permitted operation/resource/destination values, keeping host credentials private, and enforcing final resource/version/destination constraints at the side-effect boundary.

> **The delegated grant is not the blast radius. The executor's ambient credentials and validation quality determine the real blast radius.**

---

## 21. A Conceptual Teaching Model

A framework-neutral model can keep the relevant semantics visible:

```csharp
public sealed record CrossSystemCapability(
    string CapabilityId,
    string Issuer,
    string Audience,
    string OriginatingSubject,
    string PresenterBinding,
    string Operation,
    string ResourceId,
    string ResourceVersion,
    string Purpose,
    string RequestDigest,
    string IssuerDecisionId,
    string IssuerPolicyVersion,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    int MaxUses,
    int RemainingDelegationDepth,
    IReadOnlyList<DelegationHop> DelegationChain);

public sealed record DelegationHop(
    string Issuer,
    string DelegatedTo,
    int HopPosition,
    int RemainingDelegationDepth);

public sealed record ProtectedCapabilityArtifact(
    CrossSystemCapability Capability,
    string KeyId,
    object Proof);
```

`DelegatedTo` is used instead of `Delegate` to avoid visual ambiguity with `System.Delegate`.

The `Proof` type is deliberately abstract. A real implementation might use a signature envelope, opaque server-side lookup, sender-constrained proof, or another protocol-specific representation. The companion sample makes this abstraction executable with a `SimulatedProof` test double; it does not reduce the general model to a boolean security primitive.

---

## 22. Recipient Validation Pipeline

A useful sequence diagram is:

```text
Presenter       Verifier       Local Policy      Replay Store       Executor
   |                |               |                 |                 |
   | artifact       |               |                 |                 |
   |--------------->|               |                 |                 |
   |                | issuer trust  |                 |                 |
   |                | accepted key  |                 |                 |
   |                | verify proof  |                 |                 |
   |                | audience      |                 |                 |
   |                | bindings      |                 |                 |
   |                | lifetime      |                 |                 |
   |                | delegation    |                 |                 |
   |                |-------------->| current context |                 |
   |                |               | decision        |                 |
   |                |<--------------|                 |                 |
   |                |-------------------------------->| atomic claim    |
   |                |<--------------------------------| accepted        |
   |                | validated local command                           |
   |                |-------------------------------------------------->| execute
```

Order matters.

1. Authenticate the presenter/channel as required.
2. Parse and structurally validate the artifact.
3. Resolve issuer trust and the presented key/trust anchor from recipient-owned configuration.
4. Verify the artifact/proof using only recipient-accepted verification material.
5. Validate audience, presenter binding, operation, resource, purpose, request binding, and delegation rules.
6. Validate lifetime under the recipient clock.
7. Check revocation/invalidation.
8. Rebuild current recipient resource and policy context.
9. Apply recipient-local authorization/governance.
10. Atomically claim bounded use.
11. Create a validated local command.
12. Invoke the host-owned executor.
13. Record recipient evidence and reconcile outcome exchange as required.

Some systems may reorder independent lookups for performance. They should preserve the same authority invariant: no protected execution occurs before all required current validation and bounded-use checks have succeeded.

---

## 23. Keep Raw Artifacts Away From the Executor

Avoid an executor API such as:

```csharp
Task ExportAsync(
    ProtectedCapabilityArtifact artifact,
    CancellationToken cancellationToken);
```

Prefer a local validated command:

```csharp
public sealed record ValidatedExportCommand(
    string ExecutionId,
    string RecipientDecisionId,
    string OriginatingSubject,
    string IssuerDecisionId,
    string ResourceId,
    string ResourceVersion,
    string Destination,
    string Purpose,
    string CapabilityId,
    string CorrelationId);
```

The executor should not need to reinterpret issuer trust, signature formats, or raw foreign claims. Those concerns belong before the execution boundary. The validated command can still carry the minimum provenance needed to connect execution back to the originating subject, issuer decision, recipient decision, capability, execution identity, and correlation record.

The companion sample deliberately derives one stable `ExecutionId` from the capability so the dry-run executor can treat it as an idempotency/reconciliation identity. That is a teaching choice, not a required identifier format, and it remains distinct from the recipient decision/attempt identity.

The executor still owns final constraints that must be atomic with the side effect, such as expected resource version, destination allowlist, or idempotent execution identity.

---

## 24. Threat Model and Required Failure Cases

The required failure cases belong with the threat they mitigate rather than in a separate duplicate list.

| Threat / failure | Required behavior |
| --- | --- |
| Correct proof, wrong audience | No execution. Audience is semantic authorization context, not a cryptographic side effect. |
| Trusted issuer, expired authority | No execution. Recipient time policy controls acceptance. |
| Valid artifact, stale resource state | Reject, bind to immutable snapshot, or explicitly re-evaluate according to local policy. |
| Valid first use, replayed second use | No duplicate protected execution. Replay state must be atomic across the intended recipient scope. |
| Delegated authority, untrusted chain | No implicit trust expansion. |
| Valid artifact, mismatched request digest | No execution for the substituted request. |
| Valid signature under unknown/retired key | No execution unless recipient lifecycle policy explicitly accepts that key for this artifact/time. |
| Authenticated presenter, wrong presenter binding | No execution. Authentication alone does not create operation authority. |
| Valid sender grant, recipient policy denial | No execution. Sender policy cannot override recipient-local governance. |
| Replay-store or trust-metadata outage | No accidental fail-open path. Use explicit degraded behavior. |
| Raw artifact field attempts to broaden executor operation | Rejected before a validated local command is created. |

### STRIDE lens

A compact STRIDE pass helps reviewers look beyond token parsing: **Spoofing** targets issuer or presenter identity; **Tampering** targets artifact/request bindings; **Repudiation** targets decision and execution provenance; **Information Disclosure** targets cross-system evidence and rejection reasons; **Denial of Service** targets verification/replay/revocation dependencies; and **Elevation of Privilege** targets delegation broadening, recipient ambient credentials, or confused-deputy execution.

STRIDE is only one review lens. It does not replace application-specific threat modeling.

### Common mistakes

Three mistakes recur in cross-system designs:

- Treating `KeyId` as self-authenticating and attempting verification before the recipient has resolved that key to an accepted trust anchor.
- Letting a foreign artifact flow directly into an executor instead of translating accepted authority into a recipient-owned validated command.
- Returning detailed internal rejection or executor exception text to the sender, which can leak recipient policy, resource existence, or trust lifecycle information.

---

## 25. Important Invariant Tests

A focused suite should prove the boundaries rather than merely parse artifacts.

| Boundary | Invariant to prove | Companion sample |
| --- | --- | --- |
| Issuer trust / proof | Unknown issuer, unacceptable key, or invalid proof produces zero executor calls. | ✅ Covered |
| Audience | Correct proof + wrong audience produces zero executor calls. | ✅ Covered |
| Presenter | Authenticated but ineligible presenter produces zero executor calls. | ✅ Covered |
| Request binding | Same audience + changed request digest produces zero executor calls. | ✅ Covered |
| Operation/resource | Substitution of operation, resource, namespace, or expected version fails. | ◐ Partial — resource/version paths are covered; broader namespace substitution remains an exercise. |
| Lifetime | Expired/not-yet-valid/excessively long authority fails under recipient clock rules. | ✅ Covered |
| Delegation | Unsupported or untrusted chain fails; child derivation cannot increase remaining delegation budget. | ◐ Partial — unsupported chaining and recipient endpoint ownership are covered; a permitted multi-hop derivation remains an extension. |
| Revocation | Revoked authority produces zero executor calls. | ✅ Covered |
| Replay | Two **actually concurrent** claims against one single-use capability produce at most one accepted protected execution. A sequential test does not prove this race property. | ✅ Covered |
| Burned grant | Execution failure after a successful single-use claim does not silently restore the grant to reusable state. | ✅ Covered |
| Resource drift | Bound version drift triggers rejection or explicit reevaluation. | ✅ Covered — the sample chooses exact-snapshot rejection. |
| Policy drift | Recipient current policy can deny an otherwise valid sender artifact. | ✅ Covered |
| Degraded mode | Required verification/replay/policy dependency failure does not silently fail open. | ◐ Partial — replay-store unavailability is covered; other dependency outages remain exercises. |
| Executor boundary | Raw artifact never reaches the protected executor. | ✅ Covered structurally by the `ValidatedExportCommand` API. |
| TOCTOU | Executor enforces the expected resource/version constraint at the actual data boundary when required. | ✅ Covered |
| Evidence | Issuer decision, capability, recipient decision, claim, and execution identities remain correlated without being conflated. | ✅ Covered for issuer/recipient decision, capability, execution, subject, and correlation identities; durable audit storage is intentionally omitted. |

The coverage column makes the sample's limits explicit. Partial rows are intentional contribution opportunities rather than claims of production completeness.

[Run the Cross-System Capability Exchange sample](https://github.com/AsiBackbone/Learning/blob/main/samples/cross-system-capability-exchange/README.md).

---

## 26. When Cross-System Capability Authority Earns Its Cost

Use the heavier pattern when the boundary changes the authority problem materially.

Signals include:

- A recipient should receive less authority than the sender's standing service identity.
- Authority must survive beyond the initiating request or process.
- Audience, resource, purpose, request binding, or use count must be request-specific.
- Replay/revocation needs an independent lifecycle.
- Independent organizations need reconstructable issuer and recipient decision provenance.
- Recipient policy/resource state can drift between issuance and execution.
- A delegation chain is intentionally required and auditable.

Cross-system capability exchange is not a maturity upgrade. It is a cost that should be paid only when those properties are necessary.

---

## 27. Prefer Simpler Same-Host Authorization When Possible

If one host can authenticate, authorize, validate current resource state, and execute immediately, this shape is usually better:

```text
Authenticated request
      |
      v
Current authorization / policy
      |
      v
Current resource validation
      |
      v
Host-owned execution
```

There is no portable-grant replay state, no cross-system clock contract, no delegation chain, and fewer trust anchors or revocation dependencies.

If authority does not need to survive a boundary, do not manufacture that boundary.

---

## 28. Conventional Service Authorization May Also Be Enough

A common service-to-service shape is:

```text
System A authenticates to System B
      |
      v
System B resolves current identity / roles / claims
      |
      v
System B applies local authorization
      |
      v
System B executes
```

This can be sufficient when standing service authority is intentionally acceptable, the operation does not need request-specific delegated scope, resource scope can be enforced directly by recipient authorization, and no portable authority lifecycle is required.

A capability should solve a scope/lifecycle problem that ordinary service authorization cannot express cleanly.

---

## 29. Compare the Three Shapes

| Architecture | Best fit | Main benefit | Main cost |
| --- | --- | --- | --- |
| Same-host immediate authorization | One host owns decision and immediate execution | Smallest authority surface and fewest distributed failure modes | Does not support delayed/cross-system continuation |
| Conventional service authentication + recipient authorization | Standing service authority is appropriate | Familiar identity/authorization model | May be too broad for one-operation delegation |
| Cross-system scoped capability | Narrow request-specific authority must survive an independent trust boundary | Exact audience/scope/lifetime/provenance/bounded-use semantics | More trust, replay, revocation, clock, delegation, and evidence complexity |

Choose the least complex shape that preserves the real invariant.

---

## 30. Design Review Questions

The test table already covers many implementation failures. The review checklist should stay focused on design decisions that tests alone cannot answer.

1. Why does authority need to cross an independent boundary at all?
2. Could same-host authorization or conventional service authorization preserve the requirement with less machinery?
3. Exactly what issuer purpose, operation, resource namespace, audience, and trust anchors does the recipient accept?
4. Is the artifact bearer-style or sender-constrained, and what proves the presenter/holder binding?
5. What does the recipient consider the canonical request and resource identity?
6. Which clock, lifetime, skew, replay store, and revocation semantics define current acceptance?
7. If delegation is chained, who may derive a child, how is remaining delegation budget reduced, and how are intermediary proofs represented?
8. Which current resource/policy changes invalidate or require reevaluation of old authority?
9. Which system is authoritative for issuance, execution outcome, and reconciliation when outcome delivery fails?
10. What sensitive policy/resource information is withheld from cross-system telemetry and rejection responses?
11. What ambient credentials does the recipient executor hold, and which final checks prevent confused-deputy broadening?
12. Can a reviewer explain why a valid signature and possession of the artifact still might not authorize execution?

If those questions are unanswered, the authority-transfer model is incomplete.

---

## 31. What This Article Intentionally Omits

This material does not prescribe a specific capability-token format, signature algorithm, certificate/PKI profile, cloud identity provider, federation standard, service-mesh product, policy language, production replay database, revocation-distribution protocol, global trust registry, distributed ledger, exactly-once execution model, cross-region consensus protocol, or compliance certification.

The examples are simulated and use fictional identifiers only. They contain no real credentials, keys, JWTs, connection strings, or personal data.

---

## 32. Check Your Understanding

- Explain why a valid signature with the wrong audience must produce zero executor calls.
- Explain the difference between `DelegationHop.HopPosition` and `RemainingDelegationDepth` and how a child grant must change the latter.
- Explain why a recipient executor with broad host credentials can become a confused deputy even when the delegated grant is narrow.
- Explain what happens to a single-use grant when replay claim succeeds but execution later fails.

---

## 33. Related Learning

Continue with:

- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) for the foundational narrow-authority model inside a governed execution path.
- [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) for durable atomic use state and the distinction between replay resistance and idempotency.
- [Signing, Verification, Key Custody, and Tamper Evidence](../security/signing-verification-key-custody-and-tamper-evidence.md) for proof verification, trust anchors, rotation, and why signatures do not imply authorization.
- [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md) for recipient-owned trust decisions and authority narrowing.
- [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) for historical policy identity, current freshness, and policy drift.
- [Capability-Scoped Background Operation](../case-studies/capability-scoped-background-operation.md) for a delayed authority handoff within one governed application boundary before adding independently operated systems.
- [Governed Agent-to-Agent Requests and Multi-Agent Execution Boundaries](governed-agent-to-agent-requests-and-multi-agent-execution-boundaries.md) when AI agents also participate in proposal/delegation workflows.
- [API Gateways, Service Meshes, Zero Trust, and Governed Execution](../architecture/api-gateways-service-meshes-zero-trust-and-governed-execution.md) for comparison with infrastructure-level service trust and authorization boundaries.
- [Cross-System Capability Exchange sample](https://github.com/AsiBackbone/Learning/blob/main/samples/cross-system-capability-exchange/README.md) for a runnable in-memory System A/System B simulation and focused invariant tests.

---

## 34. Closing Principle

Cross-system authority should become **narrower and more explicit** as it crosses trust boundaries, not broader merely because the artifact is portable.

```text
System A governance
      |
      v
Narrow delegation evidence
      |
      v
Cross-system transport
      |
      v
System B authenticates presenter
      |
      v
System B verifies issuer / proof
      |
      v
System B validates audience / request / scope / lifetime / delegation / revocation
      |
      v
System B rebuilds current resource + policy context
      |
      v
System B makes its own decision
      |
      v
System B atomically claims bounded use
      |
      v
Validated local command
      |
      v
Host-owned execution
```

The recurring rule is:

> **The sender may delegate. The artifact may carry evidence. The receiver still owns trust, authorization, and execution.**

And the execution invariant remains:

```text
No valid recipient-accepted current authority
        |
        v
No protected execution
```

---

> **Read it. Run it. Question it. Improve it.**
