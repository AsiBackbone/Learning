---
description: Learn how independently governed authorities can coordinate one operation without turning policy distribution, network topology, or a shared coordinator into implicit global precedence.
---

# Federated Governance and Independent Authority Coordination

**Learning objective:** Distinguish ordinary policy distribution from authority federation, then reason about local autonomy, explicit coordination contracts, conflict outcomes, provenance, authority-set drift, partitions, and narrowly delegated overrides when several independent governance domains participate in one operation.

**Pattern classification:** Experimental

**Advanced area:** Federated governance and independent authority coordination

**Difficulty:** Advanced

**Required prerequisites:** [Regional and Tenant Policy Overlays](regional-and-tenant-policy-overlays.md) and [Constraint Composition and Policy Precedence](../governance/constraint-composition-and-policy-precedence.md).

**Recommended background:** [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md), [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md), and [Safe Degraded Mode and Fail-Safe Governance](../labs/safe-degraded-mode-and-fail-safe-governance.md).

**Glossary:** [Governed execution](../architecture/glossary.md#governed-execution), [host-owned execution](../architecture/glossary.md#host-owned-execution), and [trust boundary](../architecture/glossary.md#trust-boundary).

> **Experimental architecture note:** This article explores coordination boundaries for independently governed policy authorities. It does not define a federation protocol, consensus algorithm, legal hierarchy, interoperability standard, or production-ready distributed governance platform.

## Why This Matters

A distributed policy engine can evaluate one authority model in many places while one organization still owns that authority.

Federation is different. Two or more governance domains may each retain legitimate authority over part of the same operation. A shared coordinator can collect their contributions, but network position does not automatically make it their superior.

The central lesson is:

> **Distributing policy is not the same as federating authority. Independent governance domains require explicit contracts for trust, scope, conflict, local autonomy, provenance, and failure behavior.**

This matters when an operation crosses jurisdictions, organizations, regions, or independently administered systems without one policy owner legitimately controlling the complete decision.

## At a Glance

> **Problem:** Several independently governed authorities participate in one operation, but no single authority automatically owns the complete decision.
>
> **Core idea:** Resolve which authority domains apply from current host facts, obtain explicit contributions from those domains, compose them through a versioned coordination contract, preserve the contributing policy identities, and revalidate the authority set before host-owned execution.
>
> **Do not assume:** A global coordinator outranks local policy, the fastest response wins, service-registration order defines precedence, or temporary coordinator failure removes local restrictions.
>
> **Prefer something simpler when:** One organization owns the relevant policy and merely distributes evaluation, or one host can make the complete current authorization decision without cross-domain authority coordination.

A compact federated shape is:

```text
Shared operation
      |
      v
Current authority-set resolution
      |
      +-- Cedar release authority
      +-- Harbor intake authority
      +-- Local application constraints
      |
      v
Versioned coordination contract
      |
      v
Explicit federated outcome
      |
      v
Current host revalidation
      |
      v
Host-owned execution
```

The word **current** matters twice: when selecting the authorities that apply and again before later continuation or execution.

---

## 1. Scope, Assumptions, and Non-Goals

This article assumes:

- Each governance domain has a stable identity.
- Contributions can be authenticated according to the deployment's trust model.
- The host can resolve current resource, region, tenant, or jurisdiction facts from authoritative sources.
- A versioned coordination contract defines required authority roles, composition behavior, failure semantics, and any delegated override rules.
- Local enforcement remains host-owned.
- Independent authorities may disagree, become unavailable, or change policy at different times.

This article does **not** solve:

- Byzantine consensus among arbitrarily malicious governance authorities.
- Legal interpretation of real jurisdictions.
- Global distributed transactions.
- Exactly-once execution.
- A universal policy language or region hierarchy.
- A universal emergency-override model.
- Cryptographic protocol selection.

The threat discussion still considers accidental overreach, compromised inputs, unsafe override broadening, and coordinator misuse. The non-goal is Byzantine consensus, not the absence of adversarial analysis.

---

## 2. Terminology and Outcome Layers

| Term | Meaning in this article |
| --- | --- |
| **Policy distribution** | Moving or replicating policy material so several runtimes can evaluate substantially the same authority model. |
| **Distributed enforcement** | A decision or authority is enforced by one or more remote hosts or policy enforcement points. |
| **Governance domain** | An independently administered authority with its own policy ownership, versions, trust assumptions, and local restrictions. |
| **Federation** | Coordination among governance domains that retain independent authority rather than surrendering all authority to one policy owner. |
| **Coordination contract** | A versioned rule describing which authority roles apply, how contributions compose, what failures mean, and what overrides are permitted. |
| **Authority set** | The governance domains currently relevant to a specific operation and resource state. |
| **Local autonomy** | A domain's ability to enforce the restrictions it legitimately owns without those restrictions disappearing because another participant is unavailable. |
| **Authority-set drift** | A change in which governance domains apply, distinct from one existing authority changing its policy. |
| **Federated outcome** | The explicit composition result produced from the current authority set under the active coordination contract. |

Three vocabularies appear because they describe different layers:

```csharp
public enum ContributionStatus
{
    Available,
    Unavailable,
    Invalid,
    Stale
}

public enum AuthorityOutcome
{
    Allow,
    Deny,
    Defer,
    EscalationRecommended
}

public enum FederatedOutcome
{
    Allowed,
    Denied,
    Deferred,
    Conflict,
    EscalationRecommended
}
```

`AuthorityOutcome` is a per-domain contribution. `FederatedOutcome` is the result of composing several domain contributions. The deliberate naming asymmetry (`Allow / Deny / Defer` versus `Allowed / Denied / Deferred`) keeps those layers visible in examples. Neither enum is automatically the same as a foundational host decision such as `Allow / Deny / Defer / RequireAcknowledgment / Escalate`; an application may map between layers explicitly.

A domain that does not apply should normally be excluded during authority-set resolution rather than returning a synthetic `NotApplicable` outcome.

### Conflict versus EscalationRecommended

| Outcome | Meaning |
| --- | --- |
| `Conflict` | Valid participating authorities produced contributions that the current contract cannot reconcile. It records **why composition stopped**. |
| `EscalationRecommended` | The contract deliberately routes the case to another authority or review path even though the inputs are otherwise interpretable. |

A public API may expose an internal `Conflict` directly or map it to a coarser `Deferred`. That exposure choice is separate from preserving the internal composition reason.

---

## 3. Policy Distribution Is Not Authority Federation

Distribution:

```text
One policy owner
      |
      v
Policy package v12
      |
      +-- Region A evaluator
      +-- Region B evaluator
      +-- Region C evaluator
```

Federation:

```text
Cedar governance authority
      +
Harbor governance authority
      +
Application-owned constraints
      |
      v
Explicit coordination contract
```

The first deployment is geographically distributed but conceptually centralized. The second contains distinct policy owners with independent lifecycles and legitimate authority scopes.

A useful test is:

> **If one participant changes its policy independently, can another legitimately overwrite it merely because it is "global"?**

If the answer is no, the architecture needs an explicit federation contract rather than an implicit hierarchy.

---

## 4. Running Scenario: `records.transfer`

Use two fictional regions:

- **Region Cedar** owns release rules for records currently governed by Cedar.
- **Region Harbor** owns intake rules for records entering Harbor.
- **Records application** owns current record state, destination configuration, and the transfer executor.

The proposed operation is:

```text
records.transfer
record-204
cedar -> harbor
```

The host resolves the current authority set before asking for contributions:

```json
{
  "authoritySetId": "records.transfer:cedar:harbor",
  "authoritySetVersion": "record-204:v17:contract-4",
  "mode": "Federated",
  "requiredAuthorityDomains": [
    "cedar-release",
    "harbor-intake"
  ]
}
```

This is an illustrative descriptor, not an interoperability format.

A shared coordinator may compose Cedar and Harbor contributions. It does not own either region's policy or the transfer credentials.

---

## 5. Resolve the Authority Set From Current Host Facts

The host should derive policy coordinates from authoritative state, not from an untrusted request.

For the running scenario, relevant facts may include:

- Current record region.
- Requested destination region.
- Resource classification.
- Operation.
- Tenant or legal owner where relevant.
- Current resource version.

A conceptual descriptor is intentionally list-based rather than hardcoded to two domains:

```csharp
public enum CoordinationMode
{
    LocalOnly,
    Federated
}

public sealed record AuthoritySetDescriptor(
    string AuthoritySetId,
    string AuthoritySetVersion,
    CoordinationMode Mode,
    IReadOnlySet<string> RequiredAuthorityDomains);
```

The coordination contract defines which **authority roles** are required. Current resource facts bind those roles to concrete governance domains.

For example:

```text
source-release role      -> cedar-release
destination-intake role  -> harbor-intake
```

If the resource moves, the concrete authority set may change even when the contract itself does not.

---

## 6. Separate Contribution Health From Policy Outcome

A failed contribution lookup is not a policy denial.

A failed contribution lookup is also not permission.

Keep transport/evaluation health separate from semantic outcome:

```csharp
public sealed record AuthorityContribution(
    string AuthorityDomainId,
    ContributionStatus Status,
    AuthorityOutcome? Outcome,
    string PolicyId,
    string PolicyVersion,
    string? PolicyFingerprint,
    DateTimeOffset EvaluatedAtUtc,
    DateTimeOffset? FreshUntilUtc,
    string ReasonCode);
```

Examples:

| Status | Outcome | Interpretation |
| --- | --- | --- |
| `Available` | `AuthorityOutcome.Allow` | Domain evaluated and allowed its part. |
| `Available` | `AuthorityOutcome.Deny` | Domain evaluated and denied its part. |
| `Available` | `AuthorityOutcome.EscalationRecommended` | Domain evaluated and intentionally routed the case. |
| `Unavailable` | `null` | Required authority could not currently be obtained. |
| `Invalid` | `null` | Contribution was malformed, unverifiable, or otherwise unacceptable. |
| `Stale` | previous value or `null` | A historical contribution exists but freshness requirements are not satisfied. |

The composition contract decides how unavailable, invalid, or stale contributions affect the federated outcome. Do not let exception handling invent that meaning.

Stable reason codes may include:

```text
federation.contribution-unavailable
federation.contribution-invalid
federation.contribution-stale
federation.contribution-missing
```

---

## 7. Make the Coordination Contract a Real Model

The article uses three legitimate strategy families, so the model should identify them explicitly rather than hiding them in free text.

```csharp
public enum CompositionStrategy
{
    AllRequiredAuthoritiesMustAllow,
    PartitionByDecisionDimension,
    DelegatedOverride
}

public enum DisagreementDisposition
{
    DenialWins,
    PreserveConflict,
    RouteToEscalation
}

public sealed record DelegatedOverrideGrant(
    string GrantId,
    string IssuerDomainId,
    string TargetDomainId,
    string Operation,
    string ResourceClass,
    string PermittedOverride,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlySet<string> NonOverrideableReasonCodes);

public sealed record FederationContract(
    string ContractId,
    string ContractVersion,
    CompositionStrategy Strategy,
    DisagreementDisposition DisagreementDisposition,
    IReadOnlySet<string> RequiredAuthorityRoles,
    DelegatedOverrideGrant? OverrideGrant);
```

A production contract may need richer selectors and compatibility rules. The important property is that composition behavior and override authority are named, versioned, and reviewable.

---

## 8. Three Legitimate Federation Strategies

There is no universal federation algorithm.

| Strategy | Shape | Strength | Cost / risk |
| --- | --- | --- | --- |
| **All required authorities are gates** | Every required domain must provide an acceptable contribution. | Simple and conservative for mandatory independent restrictions. | Availability can be constrained by the least-available required authority. |
| **Partition by decision dimension** | Each authority owns a defined question such as release, intake, residency, or application safety. | Preserves local ownership without pretending all domains answer the same question. | Ownership boundaries must be explicit; overlap needs a conflict rule. |
| **Delegated narrow override** | A recognized authority relationship permits a bounded exception to a specific local restriction. | Supports legitimate emergency or exceptional workflows. | Adds a high-risk authority lifecycle that must remain narrow, expiring, revocable, and auditable. |

These strategies can coexist only when the contract says exactly how. Do not infer precedence from implementation topology.

### Strategy A: all required authorities are gates

A simple conservative rule is:

```text
all required domain contributions = Allow
      |
      v
Allowed

any required domain contribution = Deny
      |
      v
Denied / Conflict / EscalationRecommended,
according to the coordination contract

required contribution unavailable / invalid / stale
      |
      v
Deferred
```

This is appropriate when each domain owns a mandatory independent restriction.

### Strategy B: partition authority by decision dimension

For a transfer, ownership might be:

| Decision dimension | Owner |
| --- | --- |
| May the source release the record? | Cedar |
| May the destination accept it? | Harbor |
| Is the current resource version executable? | Records application |

If two authorities legitimately overlap on one dimension, the contract must say whether disagreement maps to `FederatedOutcome.Denied`, `FederatedOutcome.Conflict`, or `FederatedOutcome.EscalationRecommended`. Response order is not a conflict rule.

### Strategy C: delegated narrow override

An override grant should look more like this conceptually:

```text
issuer domain       = federation-council
target domain       = cedar-release
operation           = records.transfer
resource class      = emergency-response
permitted override  = retention-delay-only
expires             = bounded time
non-overridable     = cedar.legal-hold
```

The coordinator cannot mint broader override authority merely because ordinary coordination failed.

---

## 9. Coordinator Placement and Network Topology Do Not Define Precedence

A coordinator may be global, centralized, fast, or able to see every contribution. Those are deployment facts, not authority grants.

These facts must not silently become governance precedence:

- Registration order.
- Response arrival order.
- Physical proximity.
- Load-balancer priority.
- Evaluator latency.
- Policy file load order.
- Coordinator centrality.

This code is unsafe:

```csharp
foreach (AuthorityContribution contribution in responses)
{
    final = contribution.Outcome;
}
```

The result depends on iteration order.

Prefer a named composition strategy whose result is deterministic for the same valid contribution set.

> **Permutation invariant:** Reordering the same contributions must not change the federated outcome unless ordering is itself an explicit, justified part of the coordination contract.

The companion sample makes this invariant executable.

---

## 10. Represent Conflict Instead of Hiding It

Suppose two valid peer contributions apply to an overlapping decision dimension:

```text
Cedar  = AuthorityOutcome.Allow
Harbor = AuthorityOutcome.Deny
```

Possible contract-defined responses include:

- `FederatedOutcome.Denied` when denial is explicitly dominant.
- `FederatedOutcome.Conflict` when peer disagreement must remain visible.
- `FederatedOutcome.EscalationRecommended` when the contract defines a separate resolution authority.

What should not happen is:

```text
last response wins
fastest response wins
coordinator chooses silently
```

If the internal result is `Conflict`, a caller-facing API may still expose `Deferred` to avoid leaking policy structure. Preserve the internal reason in evidence even when the public vocabulary is coarser.

---

## 11. Authority-Set Drift Is Different From Policy Drift

Two changes are easy to confuse.

### Policy drift

```text
cedar-release policy v4
      |
      v
cedar-release policy v5
```

The same authority remains applicable, but its rules changed.

### Authority-set drift

```text
record-204 governed by Cedar
      |
      v
record-204 governed by Delta
```

Now a different authority may own release policy.

An earlier composite decision should therefore preserve:

- Authority-set identity/version.
- Contributing domain identities.
- Policy identities/versions.
- Resource version or other facts that selected the authority set.
- Coordination-contract identity/version.

Before delayed continuation, resolve the authority set again.

```text
old decision authority set
      |
      +-- same as current -> continue with normal freshness rules
      |
      +-- different       -> old composite is superseded; reevaluate
```

For long-running asynchronous workflows, a cached Cedar contribution should not be reused after a mid-flight move to Delta merely because the cached entry is still within its time-to-live. Freshness of the contribution does not make the old authority set current.

---

## 12. Cached Contributions Need Explicit Freshness Rules

Caching can improve availability, but it creates authority questions.

A cached contribution should preserve enough context to answer:

- Which governance domain produced it?
- Which policy identity/version produced it?
- Which authority-set identity/version was it for?
- Which resource version or stable binding did it evaluate?
- When was it evaluated?
- Until when may the contract consider it fresh?
- Is cached use permitted for this operation class?

Avoid:

```text
last known Cedar contribution = AuthorityOutcome.Allow
      |
      v
use forever during outage
```

Prefer:

```text
cached contribution
      |
      +-- correct authority set?
      +-- correct resource binding?
      +-- within explicit freshness window?
      +-- cached use permitted by contract?
      |
      v
eligible contribution or stale/unavailable state
```

Cached evidence is never a reason to skip authority-set reevaluation.

---

## 13. Partitions Do Not Erase Local Authority

A partition can make coordination impossible without making local restrictions disappear.

For a cross-region transfer:

```text
Current authority set requires Cedar + Harbor
      |
      v
Shared coordinator unavailable
      |
      v
Cannot establish complete federated outcome
      |
      v
Deferred / locally blocked according to contract
```

The opposite shortcut is also unsafe:

```text
coordinator unavailable
      |
      v
assume local policy no longer applies
      |
      v
execute
```

Local authority survives the outage.

### Legitimate local-only bypass must be classified before failure

There is one important legitimate case: an operation may never have required federation.

For example, a local read might resolve to:

```text
CoordinationMode = LocalOnly
RequiredAuthorityDomains = { records-app-local }
```

That path can remain independent of the shared coordinator **only because current authoritative facts classified it as local before availability was considered**.

Avoid this outage-driven reclassification:

```text
cross-region operation
      +
coordinator unavailable
      |
      v
pretend operation is local
```

The safe order is:

```text
1. Resolve current authority set from operation + resource facts.
2. Determine whether that set is LocalOnly or Federated.
3. Apply dependency-failure behavior for that already-resolved mode.
```

Availability pressure must not broaden authority by changing the operation's governance classification.

---

## 14. Failure Matrix and Worked Scenarios

| Situation | Required architectural behavior |
| --- | --- |
| Cedar `AuthorityOutcome.Allow`, Harbor `AuthorityOutcome.Deny` | Apply the contract's explicit denial/conflict/escalation rule; never use response order. |
| Required authority unavailable | Preserve `Unavailable` as contribution state; normally no federated `Allowed`. |
| Contribution invalid | Reject the contribution and preserve a stable `federation.contribution-invalid` reason. |
| Cached contribution stale | Treat it according to explicit freshness policy; do not silently refresh it. |
| Shared coordinator unavailable for a federated operation | Local restrictions remain; return a non-executing outcome such as `Deferred`. |
| Shared coordinator unavailable for a pre-classified local-only operation | The local path may continue if its own current policy permits it. |
| Resource moves regions | Resolve a new authority set; old composite decisions and cached contributions do not automatically carry forward. |
| Coordination contract changes | Preserve the historical contract version and apply the new contract only to current/future evaluation as defined. |
| Override grant expired or too broad | Reject the override; ordinary coordination failure does not expand it. |

### Required scenario: independent authorities disagree

For the same set of valid contributions:

```text
[Cedar AuthorityOutcome.Allow, Harbor AuthorityOutcome.Deny]
```

and:

```text
[Harbor AuthorityOutcome.Deny, Cedar AuthorityOutcome.Allow]
```

the result must be identical under an order-independent contract.

### Required scenario: coordinator unavailable

If the current authority set is federated, a coordinator outage must not convert missing coordination into permission. If the current authority set is genuinely local-only, the local path may remain available without inventing a federated dependency.

---

## 15. Multi-Authority Provenance Must Survive Composition

One final outcome is not enough evidence.

A reconstructable record may need:

| Evidence | Ownership |
| --- | --- |
| Federated decision ID | Coordination boundary |
| Correlation ID | Cross-domain workflow |
| Authority-set ID/version | Authority resolution |
| Coordination-contract ID/version | Federation contract owner |
| Contributing domain IDs | Participating authorities |
| Policy IDs/versions/fingerprints | Each governance domain |
| Contribution status/outcome | Each governance domain |
| Evaluation/freshness timestamps | Each governance domain |
| Override-grant identity, when used | Override issuer/validator |
| Final federated outcome/reason | Coordination boundary |

### Minimize shared evidence

Cross-domain provenance should not become a policy-data exfiltration channel.

Prefer carrying stable identifiers, versions, namespaced reason codes, and content fingerprints over copying sensitive local policy inputs or full rule text into shared logs. Hash, redact, or retain sensitive inputs inside the owning domain when the cross-domain record does not need their plaintext values.

Correlation should connect evidence, not force every participant to surrender its internal policy data.

---

## 16. Historical Provenance, Current Validity, and Contract Version Are Separate

A historical record should continue to say which policies and coordination contract produced the original decision.

Do not rewrite:

```text
ContractVersion = 4
```

into:

```text
ContractVersion = 5
```

because version 5 is now current.

Instead preserve history and apply current validation separately.

A contract-version change can alter:

- Required authority roles.
- Conflict behavior.
- Cached-contribution freshness.
- Override rules.
- Failure semantics.
- Public outcome mapping.

That makes contract rollout a governance change, not merely a coordinator deployment detail.

---

## 17. Local Enforcement Still Owns the Final Side Effect

A `FederatedOutcome.Allowed` result is not a credential and should not bypass the execution host's current invariants.

Before execution, the host may still need to validate:

- Current resource version.
- Current authority set.
- Current destination.
- Required acknowledgment or capability state.
- Executor-specific allowlists.
- Credentials owned by the execution host.

The safe composition remains:

```text
Federated outcome
      |
      v
Current host facts
      |
      v
Current execution authority
      |
      v
Host-owned executor
```

The coordinator should not receive broad side-effect credentials merely because it calculated the outcome. This companion sample intentionally stops at composition; for executable host-owned executor and TOCTOU checks, continue with the [Cross-System Capability Exchange sample](https://github.com/AsiBackbone/Learning/blob/main/samples/cross-system-capability-exchange/README.md).

---

## 18. Threat and Failure Analysis

| Failure / threat | Safer boundary |
| --- | --- |
| Registration or response order changes outcome | Named deterministic composition + permutation tests |
| Coordinator treats central position as superior authority | Contract-defined authority only; no topology-derived precedence |
| Required authority disappears during outage | Preserve local restrictions and return explicit non-executing state |
| Outage reclassifies federated work as local | Resolve authority set before dependency-failure behavior |
| Stale cached contribution survives jurisdiction move | Bind cache to authority set/resource facts; reevaluate after drift |
| Invalid contribution becomes `AuthorityOutcome.Deny` or `AuthorityOutcome.Allow` by exception mapping | Separate `ContributionStatus` from semantic outcome |
| Override grant broadens over time | Explicit target, operation, resource class, permitted override, expiry, revocation, and non-overridable rules |
| Coordinator or participant leaks sensitive policy inputs | Minimized shared provenance; redaction/hash/reference rather than full local data |
| Global coordinator attempts unauthorized override | Participating domains validate recognized override grants locally |
| Contract rollout silently changes historical evidence | Preserve contract version in provenance; apply current contract separately |

This table is not Byzantine-consensus machinery. It protects the architectural boundaries this learning model actually claims.

---

## 19. When Federation Is Not Worth the Complexity

Do not federate authority merely because services or policy engines are distributed.

Prefer simpler architecture when:

- One organization owns the relevant policy.
- One current host can authorize and immediately execute the operation.
- Regional policy is an overlay inside one legitimate authority hierarchy.
- A remote PDP provides shared evaluation but callers do not retain independent authority ownership.
- The operation is already represented by an authoritative committed fact rather than a new protected transition.

A simpler shape may be:

```text
Current host context
      |
      v
One policy authority
      |
      v
Authorization / decision
      |
      v
Host-owned execution
```

There is no maturity ladder from local rules to federation. Federation earns its cost only when authority is genuinely independent.

---

## 20. Design Review Questions and Unresolved Choices

Before adopting a federation model, ask:

1. Which current facts determine the authority set?
2. Which authority roles are required by the active coordination contract?
3. Can the same request produce the same result regardless of contribution order?
4. What distinguishes `Unavailable`, `Invalid`, `Stale`, and semantic `AuthorityOutcome.Deny`?
5. When does disagreement become `FederatedOutcome.Denied`, `FederatedOutcome.Conflict`, or `FederatedOutcome.EscalationRecommended`?
6. Which local restrictions are non-overridable?
7. Who may issue an override grant, and what exact scope may it change?
8. What freshness rules apply to cached contributions?
9. How is authority-set drift detected in long-running work?
10. What happens when the coordinator is unavailable after the authority set is already known to be federated?
11. Which policy/contract evidence crosses domains, and what must remain local or redacted?
12. Which final invariants remain owned by the execution host?

Some choices remain deployment-specific:

- Whether a peer disagreement is internally `FederatedOutcome.Conflict` or immediately `FederatedOutcome.Denied`.
- Whether a public API exposes `FederatedOutcome.Conflict` or maps it to a coarser deferred response.
- Whether compatible contract versions may coexist during rollout.
- Which low-consequence local-only operations may continue during federation outages.
- Whether emergency override authority exists at all.
- How long cached contributions may remain eligible.

Experimental material should make those choices visible rather than pretending they have one universal answer.

---

## 21. Runnable Companion Sample

The companion [Federated Governance Coordination sample](https://github.com/AsiBackbone/Learning/blob/main/samples/federated-governance-coordination/README.md) keeps the model in-memory and deterministic. It does not perform network I/O or protected external side effects.

It focuses on composition invariants that are easy to implement incorrectly. The test method names are listed so documentation drift is visible when the executable contract changes.

### Executable coverage

| Invariant | Companion sample | Test method |
| --- | --- | --- |
| All required domain contributions permit the operation | ✅ Covered | `AllRequiredAuthoritiesAllowProducesAllowed` |
| Reordering the same valid peer contributions does not change the result | ✅ Covered | `ContributionOrderDoesNotChangePeerConflictOutcome` |
| Required authority unavailable does not become federated `Denied` accidentally | ✅ Covered | `UnavailableRequiredAuthorityDoesNotBecomeDenied` |
| Required authority unavailable does not become federated `Allowed` accidentally | ✅ Covered | `UnavailableRequiredAuthorityDoesNotBecomeAllowed` |
| Invalid contribution produces explicit non-executing evidence | ✅ Covered | `InvalidContributionHasExplicitNonExecutingOutcome` |
| Old federated decision becomes stale after authority-set drift | ✅ Covered | `AuthoritySetDriftMakesOldFederatedDecisionStale` |
| Coordinator outage cannot reclassify a federated operation as local | ✅ Covered | `CoordinatorOutageDoesNotReclassifyFederatedOperationAsLocal` |
| Pre-classified local-only operation can remain independent of coordinator availability | ✅ Covered | `PreclassifiedLocalOnlyOperationCanIgnoreCoordinatorOutage` |
| A denial-dominant contract can map peer disagreement to federated `Denied` | ✅ Covered | `DenialWinsContractProducesDeniedInsteadOfConflict` |
| A preserve-conflict contract keeps peer disagreement explicit as `Conflict` | ✅ Covered | `PreserveConflictContractProducesConflict` |
| A route-to-escalation contract can map peer disagreement to `EscalationRecommended` | ✅ Covered | `RouteToEscalationContractProducesEscalationRecommended` |

### Intentionally outside this companion

| Scope | Companion sample |
| --- | --- |
| Real distributed transport, signatures, and consensus | ◐ Not modeled — requires deployment-specific trust and failure semantics. |
| Production cache replication or multi-region durability | ◐ Not modeled — the sample is deterministic and in-memory. |
| Host-owned consequential executor | ◐ Not modeled here — composition only; the cross-system companion exercises the executor boundary. |

The sample is deliberately smaller than the article. Its purpose is to make the decision semantics executable, not to imply a production federation runtime.

---

## 22. Check Your Understanding

After reading and running the sample, you should be able to:

- Explain why policy distribution can remain centralized authority while federation cannot.
- Show why `Unavailable` must remain distinct from both `AuthorityOutcome.Allow` and `AuthorityOutcome.Deny`.
- Demonstrate that contribution order cannot determine a federated result.
- Explain why authority-set drift supersedes an old composite decision even when old contributions are still within their cache lifetime.
- Distinguish a legitimate pre-classified local-only path from outage-driven authority broadening.
- Explain why a global coordinator needs explicit delegated authority before it may override a local restriction.

---

## 23. Related Learning

Continue with:

- [Regional and Tenant Policy Overlays](regional-and-tenant-policy-overlays.md) for policy composition inside an explicit overlay authority model.
- [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md) for centralized or distributed policy-evaluation placement.
- [Constraint Composition and Policy Precedence](../governance/constraint-composition-and-policy-precedence.md) for deterministic composition inside one governance boundary.
- [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) for historical policy identity and freshness.
- [Safe Degraded Mode and Fail-Safe Governance](../labs/safe-degraded-mode-and-fail-safe-governance.md) for dependency-failure reasoning.
- [Compare Competing Policy Architectures](../labs/compare-competing-policy-architectures.md) for deciding whether federation is actually justified.
- [Cross-System Capability Exchange and Delegated Authority](cross-system-capability-exchange-and-delegated-authority.md) when a federated decision later produces narrow authority that crosses into another independently operated execution boundary.

---

## 24. Closing Principle

The federation boundary should preserve independent authority rather than disguise centralization as coordination.

```text
Current facts
      |
      v
Current authority set
      |
      v
Independent contributions
      |
      v
Versioned coordination contract
      |
      v
Explicit federated outcome
      |
      v
Current authority-set / resource validation
      |
      v
Host-owned continuation or execution
```

The recurring rule is:

> **A coordinator may combine authority that participating domains recognize. It does not gain new authority merely because it is central, global, available, or able to see every contribution.**

And the failure invariant remains:

```text
Required independent authority cannot be established
      |
      v
No silent broadening into Allowed
```

---

> **Read it. Run it. Question it. Improve it.**
