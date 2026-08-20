# Regional and Tenant Policy Overlays

**Learning objective:** Understand how multiple policy authorities can participate in one governance decision without allowing precedence, override behavior, or decision provenance to emerge accidentally from registration order.

**Pattern classification:** General learning material

**Difficulty:** Advanced

**Prerequisites:** [Constraint Composition and Policy Precedence](../governance/constraint-composition-and-policy-precedence.md), [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md), and [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md).

## At a Glance

> **Problem:** Global, regional, tenant, application, and operation-specific policy can all apply to one decision, allowing registration order or implicit overrides to become accidental authority.
>
> **Core idea:** Resolve applicable policy scopes from authoritative host context, evaluate each contribution explicitly, compose them through a documented overlay contract, and preserve participating policy identities in decision provenance.
>
> **Why it matters:** Policy scope alone does not define authority. Without explicit overlay rules, a lower layer may silently broaden a restriction, conflicts may disappear, and reviewers may be unable to reconstruct the decision.
>
> **Read this if:** A consequential operation is governed by multiple independently owned or versioned policy authorities across jurisdictions, organizations, tenants, applications, or delegated exception paths.

A useful conceptual structure is:

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

That diagram is intentionally **not** a universal precedence hierarchy.

It is a list of possible policy scopes.

The architecture still needs to define:

```text
Which layers are required?
Which layers may narrow authority?
Which layers may broaden a default?
Which denials are mandatory?
Which overrides are explicitly delegated?
How are conflicts resolved?
Which policy identities must survive in provenance?
```

The central lesson is:

> **Policy-overlay behavior should be explicit, deterministic, reviewable, and reconstructable from decision evidence.**

---

## Multiple Constraints and Multiple Authorities Are Different Problems

[Constraint Composition and Policy Precedence](../governance/constraint-composition-and-policy-precedence.md) focuses on several constraints participating inside one policy-evaluation boundary:

```text
Constraint A
Constraint B
Constraint C
      ↓
Explicit composition
      ↓
One base decision
```

Policy overlays add a different question:

```text
Global authority
Regional authority
Tenant authority
Application authority
Operation authority
      ↓
Explicit overlay rules
      ↓
One reconstructable decision
```

The distinction matters because policy authorities may have different:

- Owners.
- Deployment schedules.
- Scope.
- Version history.
- Override rights.
- Failure behavior.
- Sources of truth.
- Review requirements.

A tenant rule and a global baseline are not merely two entries in an unordered list.

They may represent different authority relationships that the system must preserve deliberately.

---

## Separate Policy Scope, Authority, and Ownership

Three concepts are easy to blur together.

### Policy Scope

Scope answers:

> **Where does this policy apply?**

Examples:

```text
Global
Region = EU
Tenant = tenant-a
Application = billing
Operation = export.records
```

### Policy Authority

Authority answers:

> **What may this policy contribution change?**

Examples:

```text
May only narrow an upstream permission
May require acknowledgment
May recommend escalation
May override a default under a named exception grant
May never override a mandatory denial
```

### Policy Ownership

Ownership answers:

> **Who controls and versions this policy?**

Examples may include:

```text
Platform team
Regional operations team
Tenant administrator
Application team
Security team
```

Scope does not automatically determine authority.

A tenant policy is not weaker or stronger merely because it is tenant-scoped.

Its authority comes from the overlay contract the host has defined.

---

## Resolve Policy Coordinates from Authoritative Context

Before resolving overlays, the host should determine the coordinates that select them.

For example:

```text
Authenticated actor
Resource owner
Resource tenant
Current resource region
Application
Requested operation
Current environment
      ↓
Policy-set resolution
```

Do not let an untrusted request manufacture policy scope by asserting:

```text
region = preferred-region
tenant = preferred-tenant
```

when the host has authoritative sources for those facts.

A policy resolver might receive a context such as:

```csharp
public sealed record PolicyCoordinates(
    string Region,
    string TenantId,
    string ApplicationId,
    string OperationName);
```

The record is only useful if those values were resolved through the host's trust model.

This connects directly to [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md): crossing into a policy boundary should change what the system is willing to believe.

---

## Define an Overlay Contract Before Evaluating Layers

The system should be able to explain the contract that governs policy combination.

A conceptual descriptor might be:

```csharp
public sealed record PolicyLayerDefinition(
    string PolicyId,
    string ScopeKind,
    string AuthorityClass,
    bool Required,
    bool MayNarrow,
    bool MayBroadenByDefault,
    string MissingBehavior);
```

This is a teaching sketch, not a required framework API.

The important point is that questions such as these should have explicit answers:

```text
Is the regional layer mandatory?
Can tenant policy only narrow?
Can application policy require acknowledgment?
Can an operation policy recommend escalation?
Can any layer override a denial?
If an override exists, which authority granted it?
What happens if the regional policy cannot be loaded?
```

Do not encode those semantics in:

```text
DI registration order
List position
Dictionary iteration order
Last writer wins
Enum numeric values
```

Incidental execution order is not a policy model.

---

## Model 1: Monotonic Narrowing

A common overlay model is monotonic narrowing:

> **A lower layer may preserve or reduce authority, but it may not broaden a restriction imposed by a mandatory upstream layer.**

For example:

```text
Global baseline:
Data export is permitted in principle

Regional policy:
Restricted records may not leave the region

Tenant policy:
Tenant permits approved external analytics

Operation policy:
export.records is enabled
```

Suppose the host resolves:

```text
Region: EU
Tenant: tenant-a
Classification: Restricted
Destination: outside-region
Operation: export.records
```

The contributions could be represented as:

| Policy | Contribution | Reason |
| --- | --- | --- |
| `global-baseline` | Allow | — |
| `region-eu` | Deny | `regional.data-residency` |
| `tenant-a` | Allow | — |
| `export-operation` | Allow | — |

Under a documented monotonic-narrowing contract:

```text
Global = Allow
Regional = Deny
Tenant = Allow
Operation = Allow
        ↓
Final = Denied
```

The tenant permission cannot broaden the regional prohibition.

The important reason is not:

```text
Regional policy happened to run after tenant policy.
```

It is:

```text
The overlay contract defines the regional denial as mandatory
for this operation and does not delegate override authority to tenant policy.
```

The host should be able to test that invariant regardless of evaluation order.

---

## Deny Precedence Is Not Automatically Universal

A conservative system may define:

```text
Any mandatory denial wins
```

That is understandable and often useful.

But the Learning material should not turn it into an undocumented universal law.

A system may distinguish among:

```text
Mandatory denial
Default denial
Advisory warning
Required acknowledgment
Escalation recommendation
Not applicable
Delegated exception
```

Those states do not necessarily fit one universal severity ladder.

The host must define the semantics it actually needs.

For example:

```text
Mandatory regional denial
        ≠
Default global deny that explicitly permits a delegated exception
```

That distinction becomes important when lower layers are intentionally allowed to broaden a default.

---

## Model 2: Explicit Delegated Override

A different architecture may permit a lower layer to override a **default** under explicit conditions.

Consider a platform rule:

```text
Global policy:
External analytics is denied by default.

Exception rule:
A tenant may enable external analytics only when:
- the current regional policy permits it;
- an approved tenant exception exists;
- the exception is unexpired;
- the operation is bound to the approved analytics destination;
- the tenant policy explicitly enables the operation.
```

The global rule is not saying:

```text
No lower layer may ever allow this operation.
```

It is saying:

```text
Default = Deny
Override authority = delegated under named conditions
```

The resulting flow might be:

```text
Global default deny
      ↓
Regional policy = Allow
      ↓
Host resolves approved exception grant
      ↓
Tenant policy = Allow under grant
      ↓
Operation binding matches grant
      ↓
Final = Allowed
```

That is a legitimate overlay model **because the broadening path is explicit**.

Decision evidence should preserve the exception path, for example:

```text
Decision = Allowed
OverrideGrant = tenant-analytics-exception-42
Policies:
  global-analytics v5
  region-us v9
  tenant-a v14
  analytics-export v3
```

If the regional policy instead returns a mandatory denial, the tenant exception should not bypass it unless the regional authority also explicitly delegated that override.

The lesson is:

> **An override is authority. Model who granted it, what it may override, and what bounds it.**

---

## A Lower Layer Should Not Broaden by Accident

Consider this naive loop:

```csharp
GovernanceDecision decision = GovernanceDecision.Deny(
    "policy.default-deny",
    "The operation is denied by default.");

foreach (IPolicyLayer layer in layers)
{
    decision = await layer.EvaluateAsync(context);
}

return decision;
```

The final layer wins simply because it ran last.

A tenant `Allow` can therefore erase a regional `Deny`.

Reordering registrations changes governance behavior.

That is usually a design defect.

Prefer an explicit composer:

```text
Policy contributions
      ↓
Overlay contract
      ↓
Conflict / override checks
      ↓
Final governance decision
```

The composer should understand the policy contribution's authority class, not merely its position in a collection.

---

## Acknowledgment and Escalation Are Overlay Outcomes Too

Policy overlays do not only produce `Allow` or `Deny`.

A regional policy might require acknowledgment:

```text
Global: Allow
Regional: AcknowledgmentRequired
Tenant: Allow
        ↓
Final: AcknowledgmentRequired
```

if the overlay contract says the regional acknowledgment requirement is mandatory.

A tenant policy might recommend escalation for a tenant-specific high-risk resource:

```text
Global: Allow
Regional: Allow
Tenant: EscalationRecommended
Operation: Allow
        ↓
Final: EscalationRecommended
```

if that tenant layer has authority to require review.

The system should not silently collapse those workflow states into a boolean.

Explicit outcomes help preserve the reason that continuation stopped.

Acknowledgment still does not become an override.

After acknowledgment, current policy may need to be re-resolved and re-evaluated before execution continues.

---

## NotApplicable Is Different from Missing Policy

A loaded policy may intentionally return:

```text
NotApplicable
```

because its rule does not apply to the current context.

That means:

> The policy was present, evaluated, and determined that this rule had no contribution.

That is different from:

```text
Regional policy could not be loaded
```

or:

```text
Tenant policy was expected but no artifact was found
```

Those are policy-resolution or dependency states.

Do not silently rewrite:

```text
Missing required policy
```

into:

```text
NotApplicable
```

because doing so can turn a configuration or availability failure into implicit permission.

---

## Detect Conflicts Explicitly

Some disagreements can be resolved directly by the overlay contract.

Others should be surfaced as conflicts.

Examples:

| Situation | Possible documented response |
| --- | --- |
| Mandatory global deny + tenant allow | Deny |
| Regional acknowledgment requirement + tenant allow | Require acknowledgment |
| Tenant escalation requirement + operation allow | Escalate |
| Two peer authorities require incompatible destinations | Conflict → Defer or Escalate |
| Explicit delegated exception satisfies all bounds | Apply override |
| Override attempts to bypass a non-overridable rule | Reject override and preserve block |

A conflict should not be hidden inside whichever evaluator ran last.

A conceptual conflict record might preserve:

```text
ConflictId
Policy contributors
Conflicting outcomes
Reason codes
Composition policy identity
Resolution outcome
```

If the host cannot resolve a consequential conflict safely, fail-safe outcomes may include:

```text
Denied
Deferred
EscalationRecommended
```

The correct choice depends on the operation and availability model.

The key is that unresolved conflict should not silently become `Allowed`.

---

## Preserve Every Material Policy Contributor

A single `PolicyVersion` field becomes ambiguous once several independently versioned authorities participate.

Decision evidence should be capable of answering:

```text
Which policies participated?
Which versions were evaluated?
Which policy contributed the blocking reason?
Which composition rule combined them?
Was an override used?
Which authority granted the override?
```

A conceptual contribution record could be:

```csharp
public sealed record PolicyContributionEvidence(
    string PolicyId,
    string ScopeKind,
    string ScopeValue,
    string PolicyVersion,
    string? PolicyFingerprint,
    string ContributionOutcome,
    IReadOnlyList<string> ReasonCodes,
    string AuthorityClass,
    string? OverrideGrantId);
```

The final evidence can preserve the contributors:

```csharp
public sealed record CompositeDecisionEvidence(
    string DecisionId,
    string CorrelationId,
    string CompositionPolicyId,
    string CompositionPolicyVersion,
    IReadOnlyList<PolicyContributionEvidence> Policies);
```

For the regional data-residency example:

```text
Decision = Denied
Reason = regional.data-residency
Policies:
  global-baseline v4
  region-eu v12
  tenant-a v7
  export-operation v2
Composition:
  enterprise-overlay v3
```

That evidence is more informative than:

```text
PolicyVersion = 12
```

because `12` does not explain which policy family it belongs to or which other authorities participated.

---

## Policy Fingerprints Help Identify Content, Not Authority

A policy contribution may also carry a fingerprint or hash.

That can help identify the canonical policy content used at decision time.

It does not automatically prove:

- Who authored the policy.
- Who approved it.
- That the policy was legally correct.
- That the evaluator was authorized to use it.
- That the decision record is tamper-evident.

The same caution from [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) applies to every layer.

A multi-policy decision can preserve multiple fingerprints without turning those hashes into signatures or compliance evidence.

---

## Composition Policy Has Provenance Too

Suppose the same four policy contributions are evaluated under two different overlay contracts:

```text
Overlay Contract A:
Mandatory denial cannot be overridden

Overlay Contract B:
Named exception authority may override selected defaults
```

The same policy contributions may legitimately produce different final results.

Therefore, provenance should preserve not only the contributing policy layers but also the composition rule that combined them.

For example:

```text
CompositionPolicyId: enterprise-overlay
CompositionPolicyVersion: 3
```

Without that evidence, a later reviewer may know the inputs but still be unable to explain the output.

---

## Policy Drift Becomes a Set Problem

With one policy, drift may look like:

```text
Decision policy = 4.2
Current policy = 4.3
```

With overlays, drift can occur in any contributor:

```text
Decision set:
  global v4
  region-eu v12
  tenant-a v7
  export v2

Current set:
  global v4
  region-eu v13
  tenant-a v7
  export v2
```

The historical decision should still preserve `region-eu v12`.

The execution boundary now needs an explicit freshness strategy for the current set.

Possible strategies include:

| Strategy | Behavior |
| --- | --- |
| Exact contributor-set match | Re-evaluate when any required policy version changes |
| Explicit compatibility | Permit only versions declared compatible by the relevant policy authority |
| Operation-specific freshness | Re-evaluate selected high-consequence operations more aggressively |
| Risk-based freshness | Re-evaluate when changed policy affects a risk-relevant scope |
| Always re-evaluate before execution | Strong freshness at the cost of additional policy work and availability dependency |

No strategy is universally correct.

The host should document the one it relies on.

---

## Region and Tenant Can Drift Too

Policy versions are not the only source of drift.

The authoritative coordinates themselves may change between decision and execution.

Examples include:

```text
Resource moved from EU to US
Tenant ownership changed
Actor changed tenant
Application route changed
Operation target changed
Regional classification changed
```

Consider:

```text
Decision created:
Region = EU
Tenant = tenant-a

Later execution:
Resource now belongs to tenant-b
```

An unexpired capability or old decision does not prove that the original tenant policy is still the correct authority.

A freshness check may need to re-resolve:

```text
Current resource
Current tenant
Current region
Current operation target
Current required policy set
```

before trusting the old decision for execution.

This is another reason to separate historical provenance from current execution authority.

---

## Resource Relocation Requires Policy Re-Resolution

A particularly important case is resource relocation.

Suppose:

```text
09:00
Resource region = EU
Decision = Allowed under region-eu v12

09:10
Resource replicated or moved to another region

09:15
Execution requested
```

The execution boundary should not assume that the old regional policy remains applicable merely because the decision is recent.

A safe flow may be:

```text
Execution requested
      ↓
Resolve current resource coordinates
      ↓
Resolve current required policy set
      ↓
Compare with decision evidence
      ↓
Re-evaluate / accept compatibility / defer
      ↓
Execution consideration
```

The exact action depends on the host's freshness model.

The important boundary is that resource location is policy input, not immutable historical truth.

---

## Caching Does Not Remove Freshness Requirements

Policy artifacts may be expensive to load.

Caching can be reasonable, but the cache becomes part of the policy-resolution architecture.

A cache design should answer:

```text
What is the cache key?
Which policy scope does the entry belong to?
Which version or fingerprint does it represent?
How is invalidation handled?
How stale may the entry become?
What happens if the authoritative policy source is unavailable?
```

Useful cache keys may include:

```text
PolicyId
Region
TenantId
ApplicationId
OperationName
Declared version
```

as appropriate to the policy source.

A time-to-live is an operational mechanism.

It is not proof that the cached policy is still current.

If a host intentionally permits last-known-good policy during an outage, that is a degraded-mode policy and should be documented as such.

---

## Missing Regional or Tenant Policy Is an Architectural State

Suppose a request requires:

```text
Global baseline
+
Regional policy
+
Tenant policy
```

but the regional policy source is unavailable.

Avoid:

```text
Regional policy unavailable
      ↓
Use global allow
      ↓
Execute
```

unless the architecture explicitly defines that as acceptable for the specific operation.

Possible responses include:

```text
Defer until the policy source recovers
Deny because required policy is unavailable
Escalate for explicit review
Use a bounded last-known-good artifact
Permit only a deliberately reduced low-risk operation set
```

Different layers may have different missing-policy semantics.

For example:

```text
Tenant has no optional customization
      ↓
Use documented tenant-neutral behavior
```

is different from:

```text
Required tenant policy exists but cannot be retrieved
      ↓
Dependency failure
```

Make that distinction observable.

The [Safe Degraded Mode and Fail-Safe Governance lab](../labs/safe-degraded-mode-and-fail-safe-governance.md) provides a useful companion exercise for this boundary.

---

## Do Not Fall Back to Global Permission Implicitly

A common failure mode is:

```text
Global policy loaded = Allow
Regional lookup failed
Tenant lookup failed
        ↓
Continue with Global = Allow
```

That may look like graceful degradation.

For a consequential operation, it can also erase the very overlays that were expected to constrain the global baseline.

If fallback exists, it should answer:

```text
Which layers may be omitted?
For which operations?
For how long?
Under what evidence?
With which reduced authority?
How is the degraded state surfaced and audited?
```

Fallback is policy.

Treat it as policy.

---

## Determinism Requires a Stable Policy Set and Composition Rule

A useful overlay invariant is:

```text
Same authoritative context
+
Same resolved policy contributors
+
Same contributor versions
+
Same composition policy
+
Same deterministic inputs
        ↓
Same governance result
```

This means the system should not change outcome because:

```text
Tenant policy registered first today
Regional policy registered first tomorrow
```

Evaluation order may still affect latency or which diagnostics are observed when deliberate short-circuiting is enabled.

It should not silently redefine authority.

A simple test is to evaluate the same policy contributions in multiple registration orders and assert the same final decision when the documented overlay contract is order-independent.

---

## Test Overlay Matrices, Not Just Individual Layers

Each policy layer still deserves focused tests.

But a multi-authority system also needs composition tests.

A decision table might include:

| Global | Regional | Tenant | Operation | Expected |
| --- | --- | --- | --- | --- |
| Allow | Allow | Allow | Allow | Allowed |
| Allow | Deny mandatory | Allow | Allow | Denied |
| Allow | AcknowledgmentRequired | Allow | Allow | AcknowledgmentRequired |
| Allow | Allow | EscalationRecommended | Allow | EscalationRecommended |
| Default deny | Allow | Delegated exception valid | Allow | Allowed under explicit override |
| Default deny | Deny mandatory | Delegated exception valid | Allow | Denied |
| Allow | Missing required policy | Allow | Allow | Documented degraded outcome |

Then add invariants around the table.

### Registration Order Does Not Change Authority

```text
Same contributors
Different registration order
        ↓
Same final outcome
```

### Unauthorized Broadening Is Rejected

```text
Mandatory regional deny
+
Tenant allow without override authority
        ↓
Denied
```

### Required Acknowledgment Survives Lower Allows

```text
Regional acknowledgment requirement
+
Tenant allow
        ↓
AcknowledgmentRequired
```

### Missing Required Policy Does Not Become NotApplicable

```text
Required regional policy unavailable
        ↓
Explicit degraded outcome
```

### Provenance Preserves Contributors

```text
Final decision
        ↓
Evidence contains every material policy identity and version
```

### Drift Causes Explicit Freshness Behavior

```text
Decision used region-eu v12
Current region-eu = v13
        ↓
Re-evaluate / compatible / defer according to documented rule
```

### Non-Executable Outcome Never Reaches the Executor

```text
Final overlay outcome is non-executable
        ↓
Protected executor invocation count = 0
```

For a broader testing strategy, see [Practical Policy Testing and Decision-Table Strategies](../governance/practical-policy-testing-and-decision-table-strategies.md).

---

## Common Failure Modes

### 1. Last Policy Wins

The final evaluator result replaces all prior results.

Registration order becomes hidden precedence.

### 2. Tenant Allow Erases a Mandatory Regional Denial

A lower layer broadens authority without an explicit delegation path.

### 3. Every Denial Is Treated as Equally Overridable

The architecture does not distinguish mandatory restrictions from defaults that permit exceptions.

### 4. Override Authority Is Implied

A tenant or application can override because its evaluator happens to run later, not because an upstream authority delegated the right.

### 5. Only the Winning Policy Is Recorded

The decision says `region-eu v12` but loses the global, tenant, operation, and composition-policy contributors.

### 6. Missing Policy Is Treated as NotApplicable

A failed dependency silently disappears from the policy set.

### 7. Policy Cache Staleness Is Invisible

A cached regional policy is used without preserving which version or freshness rule justified it.

### 8. Historical Decisions Are Rewritten with Current Versions

Old evidence is relabeled after a policy deployment.

### 9. Region or Tenant Drift Is Ignored

Execution validates the old policy versions but never re-resolves the current resource coordinates.

### 10. A Policy Hash Is Treated as Legal Proof

Content identity is described as evidence that the policy correctly implements law or regulation.

### 11. Global Permission Becomes the Failure Fallback

Unavailable overlays disappear and broad baseline permission remains.

### 12. Overlay Logic Performs the Side Effect

Policy composition becomes an execution engine instead of returning control to the host.

---

## Host-Owned Execution Remains the Final Boundary

No overlay model changes the execution rule.

The policy system returns a decision.

The host enforces it.

A conceptual flow remains:

```text
Authoritative context
      ↓
Resolve policy set
      ↓
Evaluate contributors
      ↓
Compose overlay decision
      ↓
Preserve provenance
      ↓
Freshness / acknowledgment / capability checks when required
      ↓
Host-owned execution
```

If the final result is non-executable:

```text
Denied
Deferred
AcknowledgmentRequired
EscalationRecommended
```

then the protected operation should not execute through that path.

---

## Relationship to the Broader ASI Backbone Concept

The broader ASI Backbone concept has used regional policy mediation as one architectural illustration: globally useful intent can be constrained by regional rules before consequential local execution.

This Learning article does not require an AGI or ASI system.

The same architecture can be demonstrated with an ordinary enterprise platform:

```text
Corporate baseline
      ↓
Regional business rules
      ↓
Customer tenant policy
      ↓
Application rules
      ↓
Data-export operation
```

That conventional example is sufficient to study authority, precedence, drift, provenance, and degraded behavior.

The value of the pattern is the explicit policy boundary, not the intelligence level of the proposer.

---

## Working Implementation References

The Learning model here is intentionally broader than one framework implementation.

The current `AsiBackbone` repository provides useful specimens for several pieces of the architecture.

### Custom Decision Policy Examples

[Custom Decision Policy Examples](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/custom-decision-policy-examples.md) includes a regional overlay example that preserves an existing block and applies additional local restrictions or acknowledgment requirements.

That example demonstrates one **narrowing-overlay** model.

It should not be read as a complete universal global/region/tenant hierarchy.

### Policy Evaluator Pipeline

[Policy Evaluator Pipeline](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/policy-evaluator-pipeline.md) shows the distinction between constraint evaluation, base composition, an optional decision policy, and host-owned execution.

Those seams can participate in a host-defined overlay architecture, but the host still needs to define the authority relationship among independently versioned policy layers.

### Current Decision Evidence

The current implementation carries policy version/hash evidence on governance decisions and related artifacts, as described in [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md).

A host that needs first-class provenance for **multiple** policy contributors may need an additional composite evidence model rather than forcing several identities into one ambiguous version string.

That is an architectural requirement to model explicitly, not a capability to assume.

---

## Policy Overlays Are Not Compliance Certification

Regional and jurisdiction-specific policies may encode rules intended to reflect legal, regulatory, contractual, or organizational requirements.

The existence of those overlays does not prove that those requirements were interpreted correctly.

A policy engine cannot establish by architecture alone that:

- The correct law was identified.
- The rule is legally sufficient.
- The jurisdiction was resolved correctly.
- The policy is current.
- Exceptions were interpreted correctly.
- The organization is compliant.

Use precise claims:

```text
The system evaluated region-eu v12
and denied the operation for regional.data-residency.
```

Do not silently upgrade that statement to:

```text
The system is legally compliant in the EU.
```

Technical provenance supports review.

It does not replace legal or regulatory judgment.

---

## Review Questions

When reviewing a policy-overlay architecture, ask:

1. Which policy scopes can participate in this operation?
2. How are region, tenant, application, and operation coordinates resolved authoritatively?
3. Which policy layers are required?
4. Which layers may narrow authority?
5. Which layers may broaden a default?
6. Which denials are explicitly non-overridable?
7. If override is possible, who granted that authority and what bounds it?
8. How are acknowledgment and escalation contributions composed?
9. Is `NotApplicable` distinguishable from missing or unavailable policy?
10. How are peer-policy conflicts detected and resolved?
11. What is the fail-safe behavior for unresolved conflict?
12. Does registration order affect the final outcome?
13. Can the decision identify every material policy contributor?
14. Is the composition-policy identity also preserved?
15. What happens when one contributor changes after the decision?
16. What happens when the region or tenant changes before execution?
17. How are resource relocations detected?
18. What freshness rule governs cached policy artifacts?
19. What happens when required regional or tenant policy is unavailable?
20. Can a non-executable overlay decision ever reach the protected executor?
21. Are claims about legal compliance kept separate from technical policy evaluation?
22. Would a single application policy solve the real problem with less machinery?

If those answers are unclear, the system may have multiple policy files without having a well-defined policy-overlay architecture.

---

## Related Content

- [Advanced Overview](index.md) — place policy overlays in the broader advanced-learning path.
- [Constraint Composition and Policy Precedence](../governance/constraint-composition-and-policy-precedence.md) — begin with explicit composition inside one policy boundary.
- [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) — preserve historical policy identity and reason about drift before execution.
- [Practical Policy Testing and Decision-Table Strategies](../governance/practical-policy-testing-and-decision-table-strategies.md) — turn overlay rules into explicit decision matrices and regression tests.
- [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md) — resolve security-sensitive policy coordinates from authoritative host sources.
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) — revisit explicit decision-time facts and structured outcomes.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — carry bounded authority toward the execution boundary without giving policy ownership of the side effect.
- [Safe Degraded Mode and Fail-Safe Governance lab](../labs/safe-degraded-mode-and-fail-safe-governance.md) — practice explicit behavior when governance dependencies are unavailable.
- [Policy-Version Evidence in Governance Decisions lab](../labs/policy-version-evidence-in-governance-decisions.md) — practice policy provenance, drift, and execution freshness.

---

> **Make policy authority explicit before policy order becomes authority by accident.**
