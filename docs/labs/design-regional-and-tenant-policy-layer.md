---
description: Design and test a regional and tenant policy overlay with explicit authority, precedence, provenance, conflict, degraded-mode, and execution-freshness behavior.
---

# Lab — Design a Regional and Tenant Policy Layer

**Learning objective:** Design a multi-authority policy overlay in which global, regional, tenant, application, and operation-specific rules compose through an explicit contract rather than evaluator order, then test authority, provenance, conflict, degraded-mode, drift, and execution-freshness invariants.

**Difficulty:** Advanced

**Pattern classification:** Canonical pattern

**Prerequisites:** Recommended — [Regional and Tenant Policy Overlays](../advanced/regional-and-tenant-policy-overlays.md), [Constraint Composition and Policy Precedence](../governance/constraint-composition-and-policy-precedence.md), [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md), [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md), and [Safe Degraded Mode and Fail-Safe Governance](safe-degraded-mode-and-fail-safe-governance.md).

This lab begins with an ordinary enterprise data-export workflow. No AGI or ASI system is required.

The central lesson is:

> **Multiple policy authorities require explicit precedence, provenance, and conflict behavior. Registration order or evaluator order must not silently determine authority.**

A useful scope map is:

```text
Global Baseline
      ↓
Regional Policy
      ↓
Tenant Policy
      ↓
Application Policy
      ↓
Operation-Specific Constraints
      ↓
Governance Decision
```

That diagram lists possible policy scopes. It is **not** a complete authority model.

Your design must define which layer may narrow, which layer may broaden, which rules are non-overridable, how conflicts are represented, what missing policy means, and which policy identities survive into decision evidence.

---

## Scenario — Northstar Analytics Export

You maintain a fictional multi-region SaaS platform named **Northstar**.

Northstar exposes this governed operation:

```text
export.records
```

The host can authoritatively resolve:

```text
ActorId
ResourceId
ResourceTenantId
ResourceRegion
RecordClassification
DestinationRegion
RecordCount
ApplicationId
OperationName
CurrentUtc
```

Do not trust caller-provided region or tenant values when the host can resolve them from authenticated identity, resource metadata, or platform configuration.

### Global baseline

```text
PolicyId: global-baseline
Version: v4
Rule: Export is generally permitted when the operation is enabled.
```

### Region A policy

```text
PolicyId: region-a
Version: v12
Rule: Restricted records may not leave Region A.
Authority: Mandatory narrowing rule.
```

### Tenant Contoso policy

```text
PolicyId: tenant-contoso
Version: v7
Rule: The tenant permits approved analytics exports.
Authority: May narrow tenant behavior, but may not override a mandatory regional prohibition.
```

### Export application policy

```text
PolicyId: application-export
Version: v3
Rule: Exports above 10,000 records require acknowledgment.
Authority: May require acknowledgment, but may not convert a denial into an executable result.
```

### Operation policy

```text
PolicyId: operation-export-records
Version: v2
Rule: Restricted records require elevated review.
Authority: May require escalation, but may not convert a denial into an executable result.
```

---

# Part 1 — Define the Overlay Contract

Create an authority table before you implement composition.

| Layer | Required? | May narrow? | May broaden? | May require acknowledgment? | May require escalation? | May override mandatory deny? | Missing-policy behavior |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Global baseline |  |  |  |  |  |  |  |
| Regional |  |  |  |  |  |  |  |
| Tenant |  |  |  |  |  |  |  |
| Application |  |  |  |  |  |  |  |
| Operation |  |  |  |  |  |  |  |

Document:

1. Which policies are required for `export.records`.
2. Whether tenant policy may broaden a global default.
3. Whether tenant policy may broaden a regional prohibition.
4. Whether application acknowledgment can coexist with a denial.
5. Whether operation escalation can coexist with a denial.
6. Whether `NotApplicable` is a contribution outcome.
7. How missing required policy differs from `NotApplicable`.
8. How unresolved conflict is represented.
9. Which authority, if any, may explicitly override another authority.
10. Whether composition is order-independent.

Do not encode authority through:

```text
DI registration order
List position
Dictionary iteration order
Last writer wins
Enum numeric values
```

Incidental execution order is not a policy model.

---

# Part 2 — Model Policy Contributions Explicitly

Do not force every policy into a boolean.

A teaching model might be:

```csharp
public enum PolicyContributionOutcome
{
    Allow,
    Deny,
    AcknowledgmentRequired,
    EscalationRecommended,
    NotApplicable
}

public sealed record PolicyContribution(
    string PolicyId,
    string ScopeKind,
    string ScopeValue,
    string PolicyVersion,
    string? PolicyFingerprint,
    string AuthorityClass,
    PolicyContributionOutcome Outcome,
    IReadOnlyList<string> ReasonCodes,
    string? OverrideGrantId = null);
```

The exact API is not prescribed.

The required invariant is:

> **The composer knows both what a policy contributed and what authority that policy possesses.**

---

# Part 3 — Implement or Pseudocode Explicit Composition

Separate the responsibilities:

```text
Authoritative context
      ↓
Resolve required policy set
      ↓
Evaluate each policy independently
      ↓
Policy contributions
      ↓
Overlay composer
      ↓
Conflict / override checks
      ↓
Composite governance decision
      ↓
Decision evidence
      ↓
Host-owned execution
```

Policy evaluators must not perform the protected export.

Your composer must support at least:

```text
Allow
Deny
AcknowledgmentRequired
EscalationRecommended
NotApplicable
Conflict / unresolved composition
```

You may map unresolved conflict to `Deferred`, `Denied`, or `EscalationRecommended`, but the result must remain non-executable until the conflict is resolved according to your documented contract.

### Required invariant — higher-authority prohibition

```text
Global       = Allow
Regional     = Deny
Tenant       = Allow
Application  = Allow
Operation    = Allow
        ↓
Final        = Denied
Reason       = regional.data-residency
```

The explanation must be:

```text
The regional prohibition is mandatory under the overlay contract.
```

It must not be:

```text
Regional happened to run last.
```

---

# Part 4 — Prove Registration Order Does Not Define Authority

Evaluate the same logical contribution set in several orders:

```text
A: Global → Regional → Tenant → Application → Operation
B: Tenant → Operation → Global → Application → Regional
C: Operation → Regional → Application → Tenant → Global
```

For an order-independent contract, each order must produce the same final governance result for the same policy/context input.

Add a focused invariant:

```text
Same authoritative context
+
Same policy identities
+
Same policy versions
+
Same contributions
+
Same composition policy
        ↓
Same governance result
```

Evaluation order may affect latency or diagnostics when deliberate short-circuiting exists. It must not silently redefine authority.

---

# Part 5 — Preserve Composite Decision Provenance

A single `PolicyVersion` value is insufficient once independently versioned authorities participate.

Your evidence must preserve every material contributor.

For example:

```text
Decision = Denied
Reason = regional.data-residency

Contributing policies:
global-baseline v4
region-a v12
tenant-contoso v7
application-export v3
operation-export-records v2

Composition:
enterprise-overlay v1
```

A conceptual evidence model could be:

```csharp
public sealed record CompositeDecisionEvidence(
    string DecisionId,
    string CorrelationId,
    string CompositionPolicyId,
    string CompositionPolicyVersion,
    IReadOnlyList<PolicyContribution> Contributions);
```

Your evidence should answer:

- Which policies participated?
- Which versions were evaluated?
- Which policy supplied the material blocking or workflow reason?
- Which policies were `NotApplicable`?
- Which composition policy combined them?
- Was an override used?
- Which authority granted the override?
- Which region and tenant coordinates selected the policy set?

If you record policy fingerprints, describe them precisely. A fingerprint can identify content; it does not by itself prove authorship, approval, legal correctness, or compliance.

---

# Part 6 — Complete the Decision Matrix

Use your documented overlay contract, not evaluator order.

| Case | Global | Regional | Tenant | Application | Operation | Expected final result | Required evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A | Allow | Allow | Allow | Allow | Allow |  |  |
| B | Allow | Deny mandatory | Allow | Allow | Allow |  |  |
| C | Allow | Allow | Allow | AcknowledgmentRequired | Allow |  |  |
| D | Allow | Allow | Allow | Allow | EscalationRecommended |  |  |
| E | Allow | Deny mandatory | Allow | AcknowledgmentRequired | EscalationRecommended |  |  |
| F | Allow | NotApplicable | Allow | Allow | Allow |  |  |
| G | Allow | Missing required policy | Allow | Allow | Allow |  |  |
| H | Allow | Allow | No optional tenant customization | Allow | Allow |  |  |

For Case E, decide whether acknowledgment and escalation contributions remain useful evidence even when a mandatory denial determines the non-executable result.

Do not reduce the matrix to "the strongest enum wins." The final behavior must be explained by authority and composition rules.

---

# Part 7 — Handle an Explicit Policy Conflict

Add two required peer authorities:

```text
Regional routing policy:
Restricted exports must use destination analytics-a.

Security routing policy:
Restricted exports must use destination analytics-b.
```

Assume the operation cannot satisfy both simultaneously.

This is not safely represented as:

```text
Regional = Allow
Security = Allow
        ↓
Allowed
```

The constraints are incompatible.

Preserve conflict evidence such as:

```text
ConflictId
Contributing policy identities/versions
Conflicting constraints
Reason codes
Composition policy identity
Resolution outcome
```

Choose and defend a non-executable result such as `Deferred`, `Denied`, or `EscalationRecommended`.

Required invariant:

```text
Unresolved consequential conflict
        ↓
Must not silently become Allowed
        ↓
Executor invocation count = 0
```

---

# Part 8 — Handle Missing Regional Policy

Simulate:

```text
Region = A
Regional policy = required
Regional policy source unavailable
```

Do not rewrite this as `NotApplicable`.

The policy did not evaluate and decide it had no contribution; a required dependency is unavailable.

Reject this fallback:

```text
Global baseline = Allow
Regional lookup fails
        ↓
Use global result
        ↓
Execute
```

Choose an explicit non-executing behavior for the consequential export.

If you permit a last-known-good artifact for any operation, define:

```text
Policy identity
Version / fingerprint
Freshness bound
Operation scope
Region binding
Tenant binding when relevant
DegradedMode marker
Reason code
Recovery behavior
```

Fallback is policy. Treat it as policy.

---

# Part 9 — Distinguish Missing Tenant Policy From No Tenant Customization

Model two different states.

### Optional tenant policy intentionally absent

```text
No tenant customization exists
        ↓
Documented tenant-neutral behavior
```

### Required tenant policy unavailable

```text
Tenant policy should exist
+
Policy source unavailable
        ↓
Dependency failure
```

Do not collapse both into `NotApplicable`.

Your evidence and tests must distinguish intentional absence from unavailable required policy.

---

# Part 10 — Introduce Policy Drift

Create a decision under:

```text
global-baseline v4
region-a v12
tenant-contoso v7
application-export v3
operation-export-records v2
enterprise-overlay v1
```

Before execution, change only:

```text
region-a v12
        ↓
region-a v13
```

Historical evidence must still preserve v12.

Choose a freshness strategy, for example:

```text
Exact contributor-set match
Explicit compatibility
Operation-specific freshness
Always re-evaluate before consequential execution
```

For this lab, explain why a required regional-policy change normally forces reevaluation before the consequential export executes.

---

# Part 11 — Introduce Region and Tenant Drift

First change:

```text
ResourceRegion = Region A
        ↓
ResourceRegion = Region B
```

Then, in a separate case:

```text
ResourceTenantId = tenant-contoso
        ↓
ResourceTenantId = tenant-fabrikam
```

The old decision remains useful historical evidence, but it does not automatically prove that the old regional or tenant authority is still current.

A safe execution path conceptually performs:

```text
Execution requested
      ↓
Resolve current resource coordinates
      ↓
Resolve current required policy set
      ↓
Compare with decision evidence
      ↓
Re-evaluate or apply explicit compatibility rule
      ↓
Only then consider execution
```

Required invariants:

```text
Region changes before execution
        ↓
Original regional authority no longer assumed current
        ↓
Reevaluation or explicit freshness validation required
```

and:

```text
Tenant changes before execution
        ↓
Original tenant overlay no longer assumed current
        ↓
Policy set re-resolution required
```

---

# Part 12 — Define Reevaluation Rules

Complete this table.

| Change before execution | Reevaluate? | Why? |
| --- | --- | --- |
| No context or policy change |  |  |
| Regional policy version changed |  |  |
| Tenant policy version changed |  |  |
| Composition policy version changed |  |  |
| Resource region changed |  |  |
| Resource tenant changed |  |  |
| Record classification changed |  |  |
| Destination region changed |  |  |
| Only telemetry configuration changed |  |  |

Do not use "decision not expired" as proof of current authority.

Expiration can bound staleness. It cannot prove that policy content, region, tenant, resource classification, or destination remained unchanged.

---

# Part 13 — Test Determinism

Add the invariant:

```text
Policy set
+
Policy versions
+
Composition policy
+
Authoritative context
+
Deterministic evaluator inputs
unchanged
        ↓
Decision deterministic
```

Where practical, shuffle the physical order of contributions before composition and assert the same final outcome and material reason set.

If display order is intentionally preserved for evidence, keep presentation order separate from authority semantics.

---

# Part 14 — Prove the Host-Owned Execution Boundary

Use a fake or recording export executor.

At minimum, prove:

```text
Mandatory regional denial
+
Tenant allow
        ↓
Denied
        ↓
Executor invocation count = 0
```

```text
Required regional policy unavailable
        ↓
Explicit non-executing degraded result
        ↓
Executor invocation count = 0
```

```text
Unresolved policy conflict
        ↓
Non-executing result
        ↓
Executor invocation count = 0
```

```text
Region or tenant changed before execution
        ↓
Old decision alone is insufficient
        ↓
Executor invocation count = 0 until current authority is re-established
```

If acknowledgment or escalation is required, prove those outcomes also stop execution until their own continuation rules are satisfied.

---

# Part 15 — Required Invariant Tests

Your solution must include focused tests for these properties:

1. **Higher-authority prohibition cannot be broadened accidentally.**
2. **Registration order does not define authority.**
3. **Identical policy/context input is deterministic.**
4. **Every material policy identity/version is preserved in provenance.**
5. **Missing required regional policy is explicit and non-permissive.**
6. **Unresolved conflict is non-executable.**
7. **Region drift requires freshness handling.**
8. **Tenant drift requires policy re-resolution.**
9. **Required acknowledgment survives lower-layer permission.**
10. **Mandatory denial remains non-executable even when other layers request acknowledgment or escalation.**

A useful minimum decision-evidence assertion is:

```text
Final decision
        ↓
Evidence contains:
global-baseline v4
region-a v12
tenant-contoso v7
application-export v3
operation-export-records v2
enterprise-overlay v1
```

---

# Part 16 — Keep Technical Enforcement Separate From Legal Compliance

Regional and jurisdiction-specific policy may encode rules intended to reflect legal, regulatory, contractual, or organizational requirements.

The architecture can prove a narrower technical fact:

```text
The host resolved Region A,
evaluated region-a v12,
and denied export.records
for regional.data-residency.
```

It cannot prove by architecture alone that:

- The correct law was identified.
- The rule is legally sufficient.
- The jurisdiction was resolved correctly for legal purposes.
- The policy is current with every applicable regulation.
- The organization is compliant.

Add one paragraph to your submission explaining that boundary.

---

# Submission Artifacts

A complete submission contains:

1. **Overlay authority table** — scope, ownership, required/optional status, narrowing/broadening authority, override authority, and missing-policy behavior.
2. **Composition contract** — prose rules that let another engineer predict the result before reading code.
3. **Evaluator/composer code or pseudocode** — with policy resolution, evaluation, composition, and execution kept separate.
4. **Composite provenance model** — policy identities, versions, scope, authority, outcomes, reasons, fingerprints when used, composition-policy identity/version, and override evidence when applicable.
5. **Decision matrix** — including contradictory inputs.
6. **Conflict example** — with an explicit non-executing resolution.
7. **Missing-policy behavior** — distinguishing unavailable required regional policy from intentionally absent optional tenant customization.
8. **Drift/freshness analysis** — policy-version drift, region drift, tenant drift, resource relocation, and reevaluation triggers.
9. **Focused invariant tests** — including executor invocation-count assertions for blocked paths.

---

# Completion Criteria

You have completed the lab when you can answer:

1. Which layer owns each rule?
2. Which layers may narrow authority?
3. Which layers may broaden authority?
4. What explicit grant permits any broadening?
5. Which denials are non-overridable?
6. Can evaluator registration order change the result?
7. How is `NotApplicable` distinguished from unavailable policy?
8. What happens when required regional policy is unavailable?
9. What happens when no optional tenant customization exists?
10. How is an unresolved conflict represented?
11. Which policies contributed to the final decision?
12. Which policy supplied the blocking reason?
13. Which composition policy combined the contributors?
14. What does a fingerprint establish, and what does it not establish?
15. What happens when a contributor changes after the decision?
16. What happens when the resource moves regions?
17. What happens when the resource changes tenants?
18. Which changes require reevaluation before execution?
19. Can historical provenance remain valid when execution authority is stale?
20. Do all non-executable outcomes keep the protected executor at zero invocations?
21. Does identical policy/context input produce the same result?
22. Are technical policy-evaluation claims kept separate from legal-compliance claims?
23. Would one application-owned policy boundary solve the real problem with less machinery?

---

## Optional Extension — Explicit Delegated Override

Add a global **default deny** that may be broadened only through a named exception grant.

For example:

```text
Global default:
External analytics denied by default.

Delegation:
Tenant may enable an approved destination only when:
- current regional policy permits it;
- exception grant is unexpired;
- destination matches the grant;
- tenant policy enables the operation.
```

Prove both:

```text
Default deny
+
valid delegated override
        ↓
May become Allowed
```

and:

```text
Mandatory regional deny
+
same tenant override
        ↓
Still Denied
```

This demonstrates that "deny always wins" is not the real rule.

The real rule is:

> **Explicit authority composes according to the documented contract.**

---

## Related Content

- [Regional and Tenant Policy Overlays](../advanced/regional-and-tenant-policy-overlays.md)
- [Constraint Composition and Policy Precedence](../governance/constraint-composition-and-policy-precedence.md)
- [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md)
- [Practical Policy Testing and Decision-Table Strategies](../governance/practical-policy-testing-and-decision-table-strategies.md)
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md)
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md)
- [Policy-Version Evidence in Governance Decisions](policy-version-evidence-in-governance-decisions.md)
- [Safe Degraded Mode and Fail-Safe Governance](safe-degraded-mode-and-fail-safe-governance.md)
- [Compare Competing Policy Architectures](compare-competing-policy-architectures.md)

---

> **Make authority explicit, preserve every contributor, and re-establish current policy before stale context becomes execution authority.**
