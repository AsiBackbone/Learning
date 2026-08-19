# Policy Versioning and Decision Provenance

**Learning objective:** Understand how to preserve the identity of the policy that produced a governance decision, how to reason about policy drift before later execution, and what policy versions and fingerprints can and cannot prove.

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) and [Constraint Composition and Policy Precedence](constraint-composition-and-policy-precedence.md). Familiarity with [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) is helpful for the continuation examples.

## Pattern Card

> **Problem:** A consequential decision can outlive the policy evaluation that created it. If the decision does not preserve policy identity, later reviewers and execution boundaries may be unable to tell which policy produced the result or whether that authority is still fresh.
>
> **Pattern:** Capture stable policy evidence at decision time, preserve it without rewriting history, and apply an explicit freshness rule when current policy differs before acknowledgment, capability issuance, or execution.
>
> **Use when:** Decisions, acknowledgments, capabilities, queues, workflows, or audit evidence may survive policy deployments, rollbacks, process boundaries, or delayed execution.
>
> **Prefer something simpler when:** A low-consequence operation is evaluated and executed immediately inside one trusted boundary, no durable decision evidence is required, and policy changes cannot race the operation in a meaningful way.
>
> **Observe:** An old decision remains attributable to the policy that created it even after a newer policy becomes current, and policy drift causes an explicit continuation decision rather than silent execution.

The central question is simple:

> **Which policy produced this decision?**

That question becomes surprisingly important once a decision is no longer ephemeral.

A governance result may be:

- Stored for later review.
- Presented to a user for acknowledgment.
- Used to justify a short-lived capability.
- Passed to another process.
- Queued for later execution.
- Correlated with audit evidence.
- Revisited after a policy deployment.
- Investigated after an incident.

If the system preserves only:

```text
Decision = Allowed
Reason = export.allowed
```

then it has preserved the outcome but not necessarily the decision provenance.

This tutorial focuses on that missing layer.

---

## Decision Provenance Is Historical Evidence

Decision provenance answers questions about the decision **as it was created**.

For example:

```text
DecisionId: dec-123
Outcome: Allowed
PolicyId: customer-export
PolicyVersion: 4.2
OccurredUtc: 2026-08-19T14:00:00Z
```

The important word is historical.

If policy `4.3` becomes current ten minutes later, the old decision should still say:

```text
PolicyVersion: 4.2
```

not:

```text
PolicyVersion: 4.3
```

Updating the old record to the new version would not make the evidence more current.

It would make the evidence less truthful.

A useful principle is:

> **Current policy may change. Historical decision provenance should not.**

---

## Policy Identity and Policy Version Are Different

A policy often needs more than one identifier.

Consider:

```text
PolicyId: customer-export
PolicyVersion: 4.2
```

These fields answer different questions.

### PolicyId

`PolicyId` identifies the policy family or logical policy.

Examples:

```text
customer-export
account-disable
payment-release
regional-data-transfer
```

A stable policy identifier should normally survive ordinary revisions.

If `customer-export` changes from `4.2` to `4.3`, it is still the same logical policy family.

### PolicyVersion

`PolicyVersion` identifies a revision, release, deployment version, or other host-defined policy version.

Examples:

```text
4.2
2026.08.19.1
ruleset-173
commit-8b7f2c1
```

The exact format is application-specific.

What matters is that the version has defined semantics.

A team should be able to answer:

```text
Does a version identify source content?
A deployment package?
A configuration snapshot?
A semantic policy release?
A Git commit?
```

Without that definition, a version string may look precise while remaining ambiguous.

### PolicyFingerprint

A fingerprint or hash can optionally identify a canonical byte representation of policy material.

For example:

```text
PolicyFingerprint:
sha256:2d4c...
```

A fingerprint can strengthen content identity, but it answers a different question from a human-managed version.

A useful model is:

```csharp
public sealed record PolicyEvidence(
    string PolicyId,
    string PolicyVersion,
    string? PolicyFingerprint = null);
```

Think of the fields this way:

| Field | Primary question |
| --- | --- |
| `PolicyId` | Which logical policy family? |
| `PolicyVersion` | Which declared revision or release? |
| `PolicyFingerprint` | Which canonical byte representation, if captured? |

Do not collapse all three meanings into one opaque string unless that is a deliberate convention.

---

## Reason Codes Are Not Policy Identity

A reason code may explain **why** a rule returned an outcome:

```text
customer.export.cross-region
customer.export.classification-restricted
customer.export.large-volume
```

That does not necessarily identify the full policy that produced the decision.

The same reason code may legitimately exist in versions `4.2` and `4.3`.

The rule behind the code may also have changed while retaining the same outward reason.

Therefore:

```text
ReasonCode
    ≠
PolicyId
    ≠
PolicyVersion
```

All three can be useful evidence.

They should not be treated as substitutes for one another.

---

## Capture Policy Evidence at Decision Time

The safest point to capture policy evidence is when the decision is created.

Conceptually:

```text
Host builds context
      ↓
Policy 4.2 evaluates context
      ↓
Decision created
      ↓
Decision records policy evidence for 4.2
```

not:

```text
Policy evaluates
      ↓
Decision stored without policy identity
      ↓
Time passes
      ↓
Current policy is now 4.3
      ↓
Old decision is labeled 4.3
```

The second flow rewrites history.

A minimal decision record might be:

```csharp
public sealed record DecisionRecord(
    string DecisionId,
    string CorrelationId,
    GovernanceDecisionOutcome Outcome,
    IReadOnlyList<string> ReasonCodes,
    PolicyEvidence Policy,
    DateTimeOffset OccurredUtc);
```

When evaluation completes:

```csharp
PolicyEvidence policy = new(
    PolicyId: "customer-export",
    PolicyVersion: context.PolicyVersion,
    PolicyFingerprint: context.PolicyFingerprint);

DecisionRecord record = new(
    DecisionId: Guid.NewGuid().ToString("N"),
    CorrelationId: context.CorrelationId,
    Outcome: decision.Outcome,
    ReasonCodes: decision.Reasons
        .Select(reason => reason.Code)
        .ToArray(),
    Policy: policy,
    OccurredUtc: DateTimeOffset.UtcNow);
```

The example is intentionally small.

The important invariant is:

```text
Decision evidence
uses the identity of the policy that actually evaluated the request.
```

---

## Do Not Look Up Current Policy to Explain an Old Decision

Suppose this timeline occurs:

```text
09:00  Policy customer-export / 4.2 deployed
09:05  Decision A = Allowed
09:10  Policy customer-export / 4.3 deployed
10:00  Reviewer opens Decision A
```

The reviewer should see:

```text
Decision A
Policy: customer-export / 4.2
```

The system may also display:

```text
Current policy: customer-export / 4.3
```

Those are two separate facts.

A useful review display could be:

```text
Decision policy: customer-export / 4.2
Current policy:  customer-export / 4.3
Status: policy drift observed
```

The wrong approach is:

```text
Decision A
Policy: customer-export / 4.3
```

if `4.3` did not produce Decision A.

That loses the original decision provenance.

---

## Historical Provenance and Execution Freshness Are Different Questions

Decision provenance asks:

> **What policy produced this decision?**

Execution freshness asks:

> **Is authority created under that policy still acceptable now?**

Those questions should remain separate.

For example:

```text
Decision created under 4.2
        ↓
Policy changes to 4.3
        ↓
Execution requested
```

The historical answer remains:

```text
Decision policy = 4.2
```

The execution boundary now needs a current-state rule.

It might answer:

```text
4.2 is no longer acceptable
        ↓
Re-evaluate under 4.3
```

or:

```text
4.2 remains explicitly compatible
        ↓
Continue after other execution checks
```

The important architecture is:

```text
Historical evidence is preserved
        +
Current freshness is evaluated separately
```

---

## Policy Drift Is a First-Class State

Policy drift occurs when the policy associated with an earlier decision differs from policy considered authoritative later in the workflow.

For example:

```text
DecisionPolicy:
customer-export / 4.2

CurrentPolicy:
customer-export / 4.3
```

Drift is not automatically an error.

It is a condition that requires a defined response.

A system that changes policy frequently should expect drift.

Examples include:

- Security-rule updates.
- Regional policy changes.
- Tenant-specific overrides.
- Risk-threshold changes.
- Emergency controls.
- New legal requirements.
- Configuration rollouts.
- Feature-flag changes that affect policy composition.

The dangerous behavior is not policy drift itself.

The dangerous behavior is allowing drift to be invisible.

---

## Choose an Explicit Execution-Freshness Strategy

There is no universal freshness rule for every operation.

Several strategies are reasonable when their tradeoffs are explicit.

### Strategy A — Exact Policy-Version Match

Require:

```text
Decision PolicyVersion
        =
Current PolicyVersion
```

If they differ:

```text
Re-evaluate
or
Defer
or
Escalate
```

This is easy to understand and conservative.

It may also invalidate substantial in-flight work during routine deployments.

Use it when policy changes are meaningful enough that old decisions should not survive them automatically.

### Strategy B — Exact Fingerprint Match

Require the policy fingerprint to match exactly.

This can be stronger than comparing a mutable or loosely defined version label.

It still depends on the fingerprint being computed from the relevant policy material using a stable canonical representation.

It also does not prove that the policy was authorized or trustworthy.

### Strategy C — Explicit Compatibility Window

A new policy can declare specific prior versions compatible for selected operations.

For example:

```text
Current policy: 4.3
Compatible decision versions:
- 4.2
- 4.3
```

This can reduce unnecessary re-evaluation.

But the compatibility declaration becomes governance material of its own.

You should know:

- Who defines compatibility.
- Which operations it applies to.
- How long it remains valid.
- Whether compatibility is directional.
- How it is reviewed and audited.

Avoid an implicit rule such as:

```text
Any version from the last 30 days is close enough.
```

unless that behavior is intentionally part of the policy model.

### Strategy D — Risk-Based Re-Evaluation

A host can require current-policy evaluation only for selected risk levels or operation types.

For example:

```text
Read-only export preview
    → previous compatible decision may continue

Production export of restricted data
    → current-policy re-evaluation required
```

This is flexible.

It is also more complex and should not become a hidden bypass path.

### Strategy E — Operation-Specific Freshness

Different operations may have different freshness requirements.

For example:

| Operation | Example freshness rule |
| --- | --- |
| Local formatting | No durable decision required |
| Account disable | Exact current version at execution |
| External notification | Compatible-version window |
| Financial transfer | Re-evaluate immediately before execution |
| Long-running batch | Checkpoint-based re-evaluation |

The strongest freshness policy is not automatically the best policy.

The objective is to make the rule fit the consequence and deployment model.

---

## A Small Freshness Evaluator

A teaching model can make the rule visible:

```csharp
public enum PolicyFreshnessOutcome
{
    Current,
    Compatible,
    Reevaluate,
    Defer
}

public sealed class PolicyFreshnessEvaluator
{
    public PolicyFreshnessOutcome Evaluate(
        PolicyEvidence decisionPolicy,
        PolicyEvidence currentPolicy)
    {
        if (!string.Equals(
                decisionPolicy.PolicyId,
                currentPolicy.PolicyId,
                StringComparison.Ordinal))
        {
            return PolicyFreshnessOutcome.Reevaluate;
        }

        if (string.Equals(
                decisionPolicy.PolicyVersion,
                currentPolicy.PolicyVersion,
                StringComparison.Ordinal))
        {
            return PolicyFreshnessOutcome.Current;
        }

        return PolicyFreshnessOutcome.Reevaluate;
    }
}
```

This example uses strict version equality.

A production host may replace it with compatibility or risk-based logic.

What should not disappear is the explicit boundary:

```text
Policy drift
    ↓
Freshness policy
    ↓
Current / compatible / re-evaluate / defer
```

---

## Missing Policy Evidence Is Also a State

A legacy record may contain:

```text
Decision = Allowed
PolicyVersion = null
```

Do not silently interpret that as:

```text
Decision was produced by the current policy.
```

Missing provenance is missing provenance.

A host may choose to:

- Re-evaluate.
- Defer.
- Escalate.
- Permit continuation for low-risk operations under an explicit compatibility rule.

The behavior should be deliberate.

For consequential execution, a useful fail-safe rule is:

```text
Required policy evidence unavailable
        ↓
Do not silently treat the old decision as current
```

---

## Policy Evidence Across Acknowledgment

Acknowledgment introduces time between decision and continuation.

Consider:

```text
Policy 4.2
   ↓
Decision = AcknowledgmentRequired
   ↓
Challenge issued
   ↓
Policy changes to 4.3
   ↓
Actor acknowledges
```

The acknowledgment challenge should preserve the policy evidence associated with the condition that was presented.

A conceptual model might contain:

```text
ChallengeId
DecisionId
CorrelationId
PolicyId
PolicyVersion
PolicyFingerprint
AcknowledgmentCode
ExpiresUtc
```

If the actor was presented a challenge produced under `4.2`, do not relabel that challenge as `4.3` after a deployment.

The historical fact is:

```text
The actor acknowledged a requirement produced under policy 4.2.
```

The current operational question is:

```text
Is that acknowledgment still sufficient under policy 4.3?
```

Those questions can legitimately have different answers.

### Possible Continuation Rules

A host may choose to:

- Reject the old challenge as stale.
- Accept the acknowledgment as historical evidence and re-evaluate current policy.
- Allow current policy to decide whether the earlier acknowledgment remains sufficient.
- Require a new acknowledgment if the relevant reason or displayed text changed materially.

Whatever rule is chosen, avoid:

```text
Acknowledgment accepted
        ↓
Ignore later policy changes
        ↓
Execute
```

Acknowledgment is not a policy override.

---

## Policy Evidence Across Capability Issuance

A decision may justify creation of narrow execution authority.

That creates another timeline:

```text
Decision under policy 4.2
        ↓
Capability issued
        ↓
Policy changes to 4.3
        ↓
Capability presented for execution
```

A capability can preserve:

```text
PolicyVersion
PolicyFingerprint
```

alongside its actor, audience, operation, resource, scope, and time bindings.

The execution boundary can then compare the capability's policy evidence with current expectations.

A key principle is:

> **Capability expiration and policy freshness are different checks.**

A capability can be:

```text
Unexpired
Correct audience
Correct resource
Unused
```

and still be stale under current policy.

Likewise, a capability that carries matching policy evidence may still fail because it is expired, revoked, replayed, or bound to the wrong resource.

Policy evidence contributes to execution validation.

It does not replace the rest of execution validation.

---

## Rollback Does Not Make History Disappear

Policy rollback introduces an easy source of confusion.

Suppose deployments occur in this order:

```text
4.2
 ↓
4.3
 ↓
rollback to 4.2
```

The current policy version may again be `4.2`.

That does not automatically mean every old decision created under the earlier `4.2` deployment should execute.

Other facts may have changed:

- Resource state.
- Actor access.
- Revocation state.
- Risk inputs.
- Acknowledgment freshness.
- External dependencies.
- Deployment configuration.

If it matters to distinguish two deployments of the same policy artifact, consider recording an additional deployment identifier or release identifier.

For example:

```text
PolicyId: customer-export
PolicyVersion: 4.2
PolicyFingerprint: sha256:...
DeploymentId: deploy-20260819-03
```

Do not overload `PolicyVersion` with every possible deployment concept unless that is actually what the field is defined to mean.

---

## Avoid Mutable Version Labels

A dangerous versioning convention is:

```text
PolicyVersion = current
```

or:

```text
PolicyVersion = production
```

when the underlying contents change without the identifier changing.

That makes historical records ambiguous.

Likewise, avoid silently republishing different policy contents under the same immutable-looking version:

```text
4.2 yesterday
≠
4.2 today
```

If the content changed, one of these should normally change:

- The version.
- The fingerprint.
- The deployment identity.

Prefer version semantics that allow an investigator to understand whether two records refer to the same policy artifact.

---

## Policy Composition May Have More Than One Identity

A composed governance decision may not come from one monolithic policy file.

It may involve:

```text
Global constraints
   +
Regional overlay
   +
Tenant policy
   +
Operation-specific constraints
   +
Composition / precedence policy
```

In that case, a single value such as:

```text
PolicyVersion = 12
```

may be insufficient.

A richer provenance model could preserve multiple contributors:

```csharp
public sealed record CompositePolicyEvidence(
    PolicyEvidence CompositionPolicy,
    IReadOnlyList<PolicyEvidence> Contributors);
```

For example:

```text
CompositionPolicy:
  governance-composer / 3

Contributors:
  global-data-policy / 8
  us-regional-overlay / 5
  tenant-a-export-policy / 17
```

This allows later reviewers to distinguish:

```text
Which policies contributed?
```

from:

```text
Which composition rule combined their results?
```

### Preserve Ordering When Ordering Is Semantically Relevant

If policy composition is deliberately order-sensitive, provenance may need to preserve the evaluated order.

If composition is intentionally order-independent, canonicalization can sort contributors deterministically before fingerprinting the policy set.

Do not accidentally erase a behaviorally meaningful ordering.

This connects directly to [Constraint Composition and Policy Precedence](constraint-composition-and-policy-precedence.md): composition behavior is itself part of the policy architecture.

---

## Fingerprints Require Canonicalization

A hash function operates on bytes.

Policy semantics operate on meaning.

Those are not automatically the same thing.

Suppose two JSON documents contain the same logical values with different property order:

```json
{"region":"US","limit":10}
```

and:

```json
{"limit":10,"region":"US"}
```

A raw byte hash will differ even if the application treats the documents as semantically equivalent.

Likewise, these may change bytes without changing intended policy semantics:

- Whitespace.
- Property ordering.
- Line endings.
- Text encoding.
- Comments.
- Default values that are omitted versus written explicitly.
- Numeric formatting.
- Case normalization where the policy language is case-insensitive.

Before a fingerprint can be meaningful, define what representation is being fingerprinted.

A canonicalization process may specify:

```text
Encoding = UTF-8
Line endings = LF
Property ordering = ordinal
Whitespace = normalized
Comments = excluded
Defaults = explicit
Contributor ordering = deterministic
```

The exact rules depend on the policy format.

The important rule is:

> **Hash the canonical representation you intend to identify, not an accidental serialization.**

---

## A Minimal Fingerprint Example

For a deliberately simple teaching format:

```text
policy=customer-export;version=4.2;ruleset=baseline
```

C# can calculate a SHA-256 digest:

```csharp
using System.Security.Cryptography;
using System.Text;

static string CreatePolicyFingerprint(string canonicalPolicy)
{
    byte[] bytes = Encoding.UTF8.GetBytes(canonicalPolicy);
    byte[] hash = SHA256.HashData(bytes);

    return Convert.ToHexString(hash).ToLowerInvariant();
}
```

The digest can help answer:

```text
Does this canonical byte representation match
what was fingerprinted at decision time?
```

That is useful.

But keep the claim narrow.

---

## A Policy Hash Is Not a Trust Proof

This boundary should remain explicit:

```text
Policy version/hash
        ≠
Digital signature
        ≠
Tamper-evident storage
        ≠
Cryptographic proof of trustworthy policy
```

A digest does not, by itself, prove:

- Who authored the policy.
- Who approved the policy.
- Who deployed the policy.
- That the policy was authorized for this environment.
- That the stored digest was not altered with the stored policy.
- That the decision record was not rewritten.
- That the evaluator actually executed the bytes represented by the digest.
- That the canonicalization process included every behaviorally relevant input.

A fingerprint is content identity evidence.

It is not automatically authenticity evidence.

### Hash Versus Digital Signature

A hash answers a question about content equality.

A digital signature can additionally provide evidence that a holder of a particular private key signed particular content.

That still does not automatically prove that the signer was authorized to approve the policy.

Authorization of the signer remains a governance and key-custody concern.

### Hash Versus Tamper-Evident Storage

Storing:

```text
Policy
+
PolicyHash
```

in the same freely mutable database row does not make the row tamper-evident.

An attacker or administrator with sufficient write access may be able to replace both.

Tamper-evidence requires additional storage or verification properties.

### Hash Versus Trustworthiness

A policy can have a perfectly valid SHA-256 fingerprint and still be:

- Incorrect.
- Unsafe.
- Unauthorized.
- Misconfigured.
- Applied to the wrong tenant.
- Based on outdated law or business rules.

Cryptographic integrity is not semantic correctness.

---

## Perfect Historical Replay Requires More Than Policy Identity

Policy evidence improves reproducibility and investigation.

It does not automatically reproduce the original decision.

A complete historical replay may additionally require:

- The exact policy context snapshot.
- The evaluator implementation version.
- Constraint implementation versions.
- Composition and precedence behavior.
- External data observed during context construction.
- Feature flags.
- Configuration.
- Time-dependent inputs.
- Randomness or nondeterministic inputs.
- Dependency versions.
- Tenant and regional overlays.

Therefore, make the claim proportional to the evidence.

Reasonable claim:

> The decision record preserves the policy identity associated with evaluation.

Stronger claim that requires more evidence:

> The system can reproduce the decision exactly from historical artifacts.

Do not make the second claim unless the required artifacts are actually retained and replayable.

---

## Preserve Provenance Across Audit Residue

A useful audit timeline can retain both historical and current policy identity without overwriting either.

Suppose:

```text
Decision A under 4.2
        ↓
Policy changes to 4.3
        ↓
Execution freshness check
        ↓
Decision A considered stale
        ↓
Decision B created under 4.3
```

An evidence record for the freshness check could contain:

```csharp
public sealed record PolicyFreshnessResidue(
    string EventId,
    string CorrelationId,
    string DecisionId,
    PolicyEvidence DecisionPolicy,
    PolicyEvidence CurrentPolicy,
    string Outcome,
    DateTimeOffset OccurredUtc);
```

For example:

```text
EventId: evt-71
DecisionId: dec-a
Outcome: reevaluation-required
DecisionPolicy: customer-export / 4.2
CurrentPolicy: customer-export / 4.3
```

The subsequent decision should receive its own identity:

```text
DecisionId: dec-b
Policy: customer-export / 4.3
```

Do not mutate `dec-a` to pretend it was created under `4.3`.

The audit path should preserve the transition.

---

## Preserve Policy Evidence Without Copying Everything

Decision provenance does not require copying the entire policy source into every record.

A compact record may preserve:

```text
PolicyId
PolicyVersion
PolicyFingerprint
Artifact reference
```

while an authoritative policy archive stores the actual historical artifact.

This can reduce:

- Storage duplication.
- Sensitive-policy exposure.
- Audit-record size.
- Retention complexity.

But the reference is useful only if the referenced artifact remains retrievable for the required retention period.

Ask:

1. Where are historical policy artifacts stored?
2. Can the identifier resolve after a rollback or repository cleanup?
3. Who can read historical policy artifacts?
4. How long must they be retained?
5. Is the artifact store itself protected against unauthorized replacement?

Policy provenance is an information-design problem as well as a field-design problem.

---

## Failure Behavior When Policy Identity Is Unavailable

A runtime dependency may fail while the host is trying to determine current policy identity.

For example:

```text
Decision created under 4.2
        ↓
Execution begins later
        ↓
Policy registry unavailable
        ↓
Current version cannot be established
```

For a consequential operation, avoid:

```text
Cannot check current policy
        ↓
Assume old decision is still current
        ↓
Execute
```

Possible fail-safe outcomes include:

- Defer until policy identity can be established.
- Re-evaluate through another authoritative source.
- Escalate for explicit review.
- Deny when the operation requires strict current-policy validation.

Low-risk operations may use a different availability policy.

The important point is that dependency failure should not silently become policy compatibility.

---

## Common Failure Modes

### 1. Record Only the Outcome

```text
Allowed
```

survives, but policy identity is lost.

Later reviewers cannot tell what policy produced the result.

### 2. Use Reason Code as Policy Version

```text
Reason = export.cross-region
```

is treated as sufficient policy identity.

The reason can remain stable while policy logic changes.

### 3. Rewrite Old Decisions with Current Policy

Historical records are enriched later by reading the current policy version.

This creates false provenance.

### 4. Reuse Version Labels for Different Policy Contents

`4.2` points to different content at different times.

Historical interpretation becomes ambiguous.

### 5. Treat an Unexpired Capability as Current Policy Authority

Expiration passes, so the host skips a required policy-freshness check.

Time validity and policy validity are different concerns.

### 6. Hash an Unstable Serialization

Insignificant formatting changes produce different fingerprints, or semantically relevant values are accidentally omitted from the fingerprint input.

### 7. Call a Hash a Signature

A digest is described as proof of authorship or approval.

That is an overclaim.

### 8. Treat Policy Match as Complete Execution Authorization

Policy evidence matches, but actor, resource, audience, replay, revocation, or current resource-state validation is skipped.

Policy identity is one execution-boundary input, not the entire authorization model.

### 9. Flatten a Composite Policy Into One Ambiguous Version

Several policy contributors and a composition policy become one opaque string.

Later reviewers cannot reconstruct which policies participated.

### 10. Fail Open When Current Policy Cannot Be Determined

An unavailable policy registry is treated as implicit compatibility.

For consequential operations, this can turn a dependency failure into unintended authority.

---

## Test Architectural Invariants

Tests should prove decision-provenance behavior rather than merely confirm that fields exist.

Useful invariants include:

### Historical Policy Identity Does Not Change

```text
Decision created under 4.2
        ↓
Current policy changes to 4.3
        ↓
Stored decision still says 4.2
```

### Drift Causes an Explicit Outcome

```text
Decision policy = 4.2
Current policy = 4.3
        ↓
Freshness evaluator returns Reevaluate
```

not:

```text
Version mismatch
        ↓
Execution continues implicitly
```

### Reason Codes Do Not Replace Policy Identity

Two decisions can share:

```text
ReasonCode = export.cross-region
```

while carrying different:

```text
PolicyVersion
```

### Fingerprint Changes When Canonical Policy Changes

```text
Canonical policy A
        ↓
Fingerprint A

Canonical policy B
        ↓
Fingerprint B
```

### Canonicalization Is Stable

Representations that are intentionally semantically equivalent should produce the same canonical representation before hashing.

### Capability Policy Drift Is Visible

```text
Capability policy = 4.2
Current execution policy = 4.3
        ↓
Explicit freshness failure or compatibility decision
```

### Composite Provenance Preserves Contributors

A decision created from global, regional, and tenant policies should retain the contributing policy identities required by the host's provenance model.

---

## Working Implementation References

The Learning model in this tutorial is framework-neutral.

The current `AsiBackbone` implementation provides several useful working references for the same concerns.

### GovernanceDecision

[`GovernanceDecision`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecision.cs) carries optional `PolicyVersion` and `PolicyHash` values on the decision itself.

That demonstrates the important boundary that policy evidence can travel with the result rather than remaining only in transient evaluation context.

The current type does not define a dedicated `PolicyId` property. A host that needs a stable logical policy-family identifier should model that requirement explicitly rather than pretending `PolicyVersion` means both identity and revision.

### AuditResidue

[`AuditResidue`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Audit/AuditResidue.cs) carries optional policy version/hash evidence and preserves those values when residue is created from a `GovernanceDecision`.

That is an example of policy evidence propagating into later governance evidence.

### CapabilityTokenGrant

[`CapabilityTokenGrant`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityTokenGrant.cs) can carry optional `PolicyVersion` and `PolicyHash` bindings into short-lived execution authority.

### CapabilityGrantValidationOptions

[`CapabilityGrantValidationOptions`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityGrantValidationOptions.cs) allows an execution boundary to state expected policy version/hash values during capability validation.

These references show concrete implementation seams.

They do not change the Learning boundary:

> Policy evidence supports attribution and freshness checks; it does not, by itself, establish trustworthiness, authorization, or tamper-evidence.

---

## When a Simpler Design Is Enough

Not every operation needs durable policy provenance.

A simpler design may be appropriate when:

- The operation is low consequence.
- Evaluation and execution happen immediately in one process.
- No acknowledgment or delayed continuation exists.
- No capability is issued.
- Historical reconstruction is unnecessary.
- Policy changes cannot meaningfully race the execution.
- Ordinary application logs already meet the operational need.

For example:

```text
Local UI formatting preference
        ↓
Simple guard clause
        ↓
Immediate local effect
```

may not benefit from a persistent `PolicyEvidence` model.

The purpose of policy provenance is not to add ceremony to every branch.

It is to preserve meaning when a consequential decision must survive time, boundaries, or policy change.

---

## Review Questions

Use these questions when reviewing a policy-governed workflow:

1. Can a durable decision answer which logical policy produced it?
2. Is `PolicyId` stable across ordinary revisions?
3. Are the semantics of `PolicyVersion` documented?
4. Is policy evidence captured at decision time?
5. Can old decision records be accidentally relabeled with current policy?
6. What happens when policy changes between decision and execution?
7. Is the execution-freshness strategy explicit?
8. Does acknowledgment preserve the policy evidence associated with the challenge?
9. Can a capability outlive the policy assumptions that justified it?
10. Are capability expiration and policy freshness treated separately?
11. What happens during policy rollback?
12. Are policy version labels immutable enough for historical interpretation?
13. If a hash is used, what exactly is canonicalized and hashed?
14. Is a hash ever described as proving authorship, approval, or tamper-evidence?
15. If several policies contribute, can the system identify the contributors and composition policy?
16. What happens when current policy identity cannot be determined?
17. Which historical artifacts are required for the level of reproducibility being claimed?
18. Is policy evidence minimized enough to avoid copying unnecessary sensitive material?

If those answers are unclear, the system may preserve decisions without preserving enough provenance to interpret or safely continue them later.

---

## Next: Practice the Boundary

Continue with the [Policy-Version Evidence in Governance Decisions lab](../labs/policy-version-evidence-in-governance-decisions.md).

The lab starts from the existing Policy Context sample and asks you to:

- Preserve policy evidence in durable decision records.
- Simulate policy drift.
- Choose an execution-freshness rule.
- Carry policy evidence through acknowledgment and capability issuance.
- Correlate historical and current policy identity in audit residue.
- Add a policy fingerprint without overclaiming what it proves.

The preferred progression is now:

```text
Policy Versioning and Decision Provenance
        ↓
Minimal policy-evidence model
        ↓
Policy-Version Evidence lab
        ↓
AsiBackbone working implementation references
```

---

## Related Content

- [Governance Index](index.md) — view the deeper governance learning path.
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) — begin with explicit decision-time facts, policy identity, and structured outcomes.
- [Constraint Composition and Policy Precedence](constraint-composition-and-policy-precedence.md) — examine how several rule results and composition behavior become one final governance decision.
- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) — connect decision provenance to acknowledgment, re-evaluation, and durable governance evidence.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — see policy identity carried into narrow execution authority.
- [Policy-Version Evidence in Governance Decisions lab](../labs/policy-version-evidence-in-governance-decisions.md) — practice preserving provenance and detecting policy drift.
- [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) — compare policy freshness with replay/use-state checks at the execution boundary.
- [Signing, Verification, Key Custody, and Tamper Evidence](../security/signing-verification-key-custody-and-tamper-evidence.md) — continue from policy fingerprints into cryptographic authenticity, verifier trust policy, historical key verification, and tamper-evidence boundaries.

---

> **Read it. Run it. Question it. Improve it.**
