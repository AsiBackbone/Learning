# Lab — Policy-Version Evidence in Governance Decisions

**Learning objective:** Practice preserving the policy identity that produced a governance decision, detecting policy drift before consequential execution, and distinguishing useful policy provenance from stronger claims such as historical replay or cryptographic proof.

**Difficulty:** Intermediate
**Prerequisites:** Complete the [Policy Versioning and Decision Provenance tutorial](../governance/policy-versioning-and-decision-provenance.md) and run the [Policy Context and Explicit Decision Outcomes sample](https://github.com/AsiBackbone/Learning/blob/main/samples/policy-context-and-explicit-decision-outcomes/README.md).

This lab applies the policy-identity, decision-provenance, drift, and freshness concepts established in the dedicated governance tutorial.

It begins with the second foundational sample because it already carries a policy version in the evaluation context while returning a decision that contains only the outcome and reasons.

That creates a useful question:

> **If the context disappears after evaluation, can the durable decision still tell you which policy produced it?**

The lab asks you to preserve that answer, then reason about what should happen when policy changes before acknowledgment, capability issuance, or execution.

> **A decision record should preserve the policy provenance needed to interpret the decision later without pretending that a version string alone proves the policy contents.**

---

## Starting Architecture

The companion sample uses this simplified flow:

```text
Host gathers facts
   ↓
Policy context snapshot
   |
   +-- CorrelationId
   +-- PolicyVersion = 2.0
   ↓
Policy evaluation
   ↓
GovernanceDecision
   |
   +-- Outcome
   +-- Reasons
```

The context contains policy identity information, but the returned decision does not preserve it.

A later record reduced to this shape:

```text
DecisionId
Outcome
ReasonCode
Timestamp
```

can answer:

```text
What was decided?
Why, at a high level?
When?
```

but not:

```text
Which policy produced this outcome?
Which version was active?
Was the decision created under the policy that is active now?
```

The objective is to make that missing evidence explicit without turning the teaching sample into a production policy registry.

---

## Prepare the Lab

Work on a temporary branch or disposable copy of the repository so that you can safely modify the sample.

For example:

```bash
git switch -c lab/policy-version-evidence
```

From the repository root, run the baseline sample:

```bash
dotnet run --project samples/policy-context-and-explicit-decision-outcomes/PolicyContextAndExplicitDecisionOutcomes/PolicyContextAndExplicitDecisionOutcomes.csproj
```

The baseline should finish with:

```text
Invariant preserved: every explicit context produced the expected structured outcome.
Scenarios verified: 7
```

Before continuing, locate these elements in `Program.cs`:

1. `DisableAccountPolicyContext`
2. `PolicyVersion`
3. `GovernanceDecision`
4. `DecisionReason`
5. `DisableAccountPolicy`
6. `VerifyScenario`
7. `PolicyScenario`

Confirm the current asymmetry:

```text
Policy context knows PolicyVersion
GovernanceDecision does not
```

---

# Part 1 — Preserve a Decision Without Policy Evidence

Create a small record representing evidence that might survive after the in-memory context is gone:

```csharp
public sealed record DecisionRecord(
    string DecisionId,
    GovernanceDecisionOutcome Outcome,
    IReadOnlyList<string> ReasonCodes,
    DateTimeOffset OccurredAt);
```

After one scenario is evaluated, create a `DecisionRecord` from the returned `GovernanceDecision`.

Then imagine that only this record remains available during an incident review.

Answer:

1. Can you tell whether policy `2.0` or `2.1` produced the result?
2. If the current policy now behaves differently, can you determine whether the old result is stale or merely different?
3. Does the reason code identify the rule set, or only the reason returned by the evaluated rule?
4. Could the same reason code legitimately appear across multiple policy versions?
5. Can the decision be interpreted confidently after a policy rollout if its policy identity was discarded?

The intended observation is:

```text
Reason code
≠
Policy identity
```

A reason code explains a decision outcome.

It does not necessarily identify the complete policy artifact that produced it.

---

# Part 2 — Add Stable Policy Identity

Introduce a small policy-evidence model:

```csharp
public sealed record PolicyEvidence(
    string PolicyId,
    string PolicyVersion,
    string? PolicyFingerprint = null);
```

For this exercise, use a stable policy identifier such as:

```text
account-disable
```

The identifier should describe the policy independently of its deployment version.

For example:

```text
PolicyId      = account-disable
PolicyVersion = 2.0
```

When you create decision evidence from the sample, bind the recorded version to the evaluated context rather than duplicating it as an unrelated literal:

```csharp
PolicyEvidence policyEvidence =
    new("account-disable", scenario.Context.PolicyVersion);
```

This keeps the decision record connected to the policy identity that accompanied the evaluated context.

It is preferable to treating this as the only identity:

```text
PolicyId = account-disable-2.0
```

because the stable identifier lets you reason about successive versions of the same policy.

Now evolve the decision record:

```csharp
public sealed record DecisionRecord(
    string DecisionId,
    string CorrelationId,
    GovernanceDecisionOutcome Outcome,
    IReadOnlyList<string> ReasonCodes,
    PolicyEvidence Policy,
    DateTimeOffset OccurredAt);
```

Create the evidence **at decision time** rather than looking up the current policy later.

Conceptually:

```text
Policy evaluation under account-disable / 2.0
   ↓
Decision created
   ↓
Decision record captures account-disable / 2.0
```

not:

```text
Decision created
   ↓
Time passes
   ↓
Current policy looked up later
   ↓
Current version written onto old decision
```

The second path rewrites history.

## Verify the Binding

Add a focused verification that confirms the record contains:

```text
PolicyId = account-disable
PolicyVersion = 2.0
```

Also preserve the sample's correlation identifier so that later audit residue can connect the decision to the same governed workflow.

At this point, the record should be able to answer:

```text
Which policy family produced the decision?
Which recorded version produced it?
Which workflow did the decision belong to?
```

---

# Part 3 — Change Policy After the Decision

Now create the changed-policy scenario required by this lab.

Use two policy descriptors:

```csharp
PolicyEvidence decisionPolicy =
    new("account-disable", "2.0");

PolicyEvidence currentPolicy =
    new("account-disable", "2.1");
```

Model this timeline:

```text
Policy 2.0
   ↓
Decision = Allowed
   ↓
Decision record preserves 2.0
   ↓
Policy changes to 2.1
   ↓
Execution attempted
```

Before execution, compare the decision-time policy evidence with the policy that is authoritative now.

The important question is not merely:

```text
Was the old decision Allowed?
```

It is also:

```text
Is authority created under 2.0 still acceptable under 2.1?
```

## Choose an Execution-Freshness Rule

There is no universal rule for every system.

Choose and document one of these approaches.

### Option A — Exact Version Match

```text
Decision policy version
must equal
Current policy version
```

A mismatch blocks execution and requires re-evaluation.

This is simple and conservative, but frequent policy deployments can invalidate otherwise harmless in-flight work.

### Option B — Explicit Compatibility

A policy deployment can declare that authority created under selected prior versions remains compatible.

For example:

```text
Current version = 2.1
Compatible decision versions = [2.0, 2.1]
```

This reduces unnecessary retries but introduces a new governed artifact: the compatibility rule itself.

### Option C — Risk-Based Freshness

Low-risk operations may tolerate a compatible older decision while high-risk operations require exact current-policy evaluation.

This is more flexible but must not become an undocumented exception path.

Whichever rule you choose, make this invariant visible:

```text
Policy drift detected
   ↓
Host applies an explicit freshness rule
   ↓
Execute, re-evaluate, defer, or escalate
```

not:

```text
Old decision says Allowed
   ↓
Execute automatically
```

---

# Part 4 — Carry Policy Evidence Across Acknowledgment

Now extend the reasoning beyond the initial decision.

Consider a decision that requires acknowledgment:

```text
Policy 2.0
   ↓
Decision = AcknowledgmentRequired
   ↓
Challenge issued
   ↓
Policy changes to 2.1
   ↓
Actor responds
```

The acknowledgment should preserve what the actor was actually asked to acknowledge.

A minimal challenge could bind to:

```text
ChallengeId
DecisionId
CorrelationId
PolicyId
PolicyVersion
ExpiresAt
```

Do not silently replace `PolicyVersion = 2.0` with `2.1` after the policy changes.

The challenge is historical evidence of the condition presented at issuance time.

At continuation, the host should separately determine whether the acknowledged decision is still acceptable under current policy.

This preserves two facts:

```text
The actor acknowledged a requirement produced under policy 2.0.
```

and:

```text
The operation is now being evaluated under policy 2.1.
```

Those facts may legitimately differ.

## Reason About Stale Acknowledgment

Choose and defend one behavior:

- Reject the acknowledgment as stale when policy identity changes.
- Accept it as historical evidence, then re-evaluate under current policy.
- Let the current policy decide whether the earlier acknowledgment remains sufficient.

Whatever you choose, do not convert acknowledgment into a policy override.

A valid acknowledgment can satisfy a specific requirement without freezing all policy state.

---

# Part 5 — Bind Capability Issuance to Decision Evidence

Now consider a later stage:

```text
Decision under policy 2.0
   ↓
Acknowledgment if required
   ↓
Capability issued
   ↓
Policy changes to 2.1
   ↓
Execution attempted
```

A capability should not erase the provenance of the decision that justified its issuance.

For this exercise, imagine a capability record with these fields:

```text
CapabilityId
DecisionId
ActorId
Operation
ResourceId
ExpiresAt
PolicyId
PolicyVersion
```

The exact production token format is outside this lab.

The architectural point is that scoped authority can remain connected to the policy evidence that produced it.

At the execution boundary, ask:

1. Is the capability unexpired?
2. Does it still match actor, operation, resource, and audience requirements?
3. Has it already been consumed if one-time use is required?
4. Does its decision-policy evidence satisfy the current execution-freshness rule?

An unexpired capability is not automatically fresh policy authority.

This distinction matters when policy can change faster than the capability lifetime.

---

# Part 6 — Correlate Policy Evidence with Audit Residue

Create a small audit record for the changed-policy execution attempt.

One possible shape is:

```csharp
public sealed record PolicyEvidenceResidue(
    string EventId,
    string CorrelationId,
    string DecisionId,
    string Stage,
    string Outcome,
    PolicyEvidence DecisionPolicy,
    PolicyEvidence CurrentPolicy,
    DateTimeOffset OccurredAt);
```

For a strict version-matching design, a stale execution attempt might produce:

```text
Stage: execution-policy-freshness
Outcome: blocked
DecisionPolicy: account-disable / 2.0
CurrentPolicy: account-disable / 2.1
```

Then re-evaluation may produce a new decision under `2.1` with a new `DecisionId`.

Do not mutate the original decision record to say that it was produced under `2.1`.

The audit timeline should be able to show:

```text
Decision A created under 2.0
   ↓
Policy drift detected
   ↓
Decision A considered stale for execution
   ↓
Decision B created under 2.1
```

That is more informative than overwriting Decision A with current-state information.

## Reproducibility Is Not Perfect Replay

Policy evidence improves later interpretation, but it does not automatically recreate the entire historical decision environment.

A complete replay may also require:

- The exact evaluated context snapshot.
- The evaluator implementation version.
- External data observed during context construction.
- Rule-composition behavior.
- Feature flags or configuration.
- Dependency versions.
- Random inputs when evaluation is nondeterministic.

The lab's decision record should therefore support a modest claim:

> It preserves policy provenance useful for interpreting the decision.

Do not upgrade that claim to:

> It guarantees perfect historical replay.

unless the required historical artifacts are actually preserved.

---

# Part 7 — Add a Policy Fingerprint Without Overclaiming It

A version string is useful, but a team may also want a compact fingerprint of the policy artifact that was evaluated.

For the exercise, define a canonical fictional policy representation:

```text
policy=account-disable;version=2.0;ruleset=baseline
```

You may calculate a SHA-256 digest with a helper such as:

```csharp
using System.Security.Cryptography;
using System.Text;

static string CreateFingerprint(string canonicalPolicy)
{
    byte[] bytes = Encoding.UTF8.GetBytes(canonicalPolicy);
    byte[] hash = SHA256.HashData(bytes);

    return Convert.ToHexString(hash).ToLowerInvariant();
}
```

Store the result in `PolicyFingerprint`.

Then change one character in the canonical representation and confirm that the fingerprint changes.

## State the Boundary Precisely

A recorded digest can help answer:

```text
Does this byte representation match the representation that was fingerprinted?
```

It does **not** by itself prove:

- Who authored the policy.
- Who approved the policy.
- That the stored policy artifact was never replaced together with the stored digest.
- That the decision record itself is tamper-evident.
- That the policy was actually deployed to the evaluator claimed by the record.
- That the fingerprint was generated from a complete or correctly canonicalized representation.

A digest is not a digital signature.

A version is not a digest.

A digest is not a signed decision receipt.

If stronger provenance is required, additional controls may include signed artifacts, protected key material, immutable or append-only storage with enforceable guarantees, deployment attestations, or independently verifiable transparency records.

Those controls are intentionally outside this lab.

---

# Part 8 — Minimize the Evidence Surface

It is easy to react to audit requirements by copying everything into every decision record.

Do not do that by default.

Review your final `DecisionRecord` and `PolicyEvidenceResidue` models.

For each field, ask whether it is required for:

```text
Decision identity
Correlation
Decision explanation
Policy provenance
Freshness evaluation
Later investigation
```

Avoid copying unrelated sensitive material such as:

- Authentication tokens.
- Full request bodies.
- Complete account objects.
- Secrets or credentials.
- Unredacted prompts.
- Personal data unrelated to the policy decision.
- Entire policy source text when a stable identifier and protected artifact reference are sufficient.

Policy provenance should make a decision easier to interpret without turning the decision record into an unnecessary duplicate of every input and artifact.

## Retention Questions

Answer:

1. How long should decision records be retained?
2. Does policy evidence need the same retention period as operational logs?
3. Where is the authoritative historical policy artifact stored?
4. Can the decision record reference that artifact instead of copying it?
5. Who may read policy provenance if the policy itself contains sensitive operational details?

Data minimization is part of governance design, not an obstacle to it.

---

# Final Validation

Run the modified sample and confirm all of the following:

- The original seven policy scenarios still produce their intended outcomes.
- A durable `DecisionRecord` can preserve `DecisionId`, `CorrelationId`, outcome, reason codes, occurrence time, and policy evidence.
- `PolicyId` remains stable while `PolicyVersion` can change.
- Policy evidence is captured at decision time rather than looked up and rewritten later.
- A decision created under `2.0` can be distinguished from the current `2.1` policy.
- The execution boundary applies an explicit rule when decision policy and current policy differ.
- Acknowledgment remains bound to the decision and policy evidence that produced the challenge.
- Policy changes after acknowledgment do not turn acknowledgment into a policy override.
- Capability authority remains connected to the decision evidence that justified issuance.
- Audit residue can distinguish decision-time policy from current execution-time policy.
- A fingerprint, if added, is described as a digest of a chosen representation rather than as cryptographic proof of authorship or tamper evidence.
- Decision records avoid unnecessary secrets and unrelated personal data.

The final architecture should preserve this distinction:

```text
Decision-time evidence
        ↓
Policy changes
        ↓
Freshness decision at continuation or execution
        ↓
Re-evaluate, execute, defer, or escalate explicitly
```

not:

```text
Old Allowed decision
        ↓
Current execution automatically permitted
```

---

# Completion Criteria

You have completed the lab when you can explain the difference between each of these statements:

```text
The decision was Denied.
The decision was Denied for reason account.disable.not-administrator.
The decision was produced by policy account-disable version 2.0.
The recorded policy artifact has a matching digest.
The decision record is cryptographically signed and tamper-evident.
```

Those statements provide progressively different kinds of evidence.

They are not interchangeable.

You should also be able to reason through this complete changed-policy path:

```text
Policy 2.0
   ↓
Decision created with 2.0 evidence
   ↓
Acknowledgment or capability may be created
   ↓
Policy becomes 2.1
   ↓
Continuation or execution detects drift
   ↓
Explicit freshness rule applied
   ↓
Current policy remains authoritative
```

The goal is not to preserve every historical byte inside every decision.

The goal is to preserve enough stable provenance that a later system or reviewer can understand which policy produced the decision and make an explicit choice when that policy is no longer current.

## Optional Extension — Model a Compatible Rollout

Add a tiny compatibility function:

```csharp
static bool IsDecisionPolicyAcceptable(
    PolicyEvidence decisionPolicy,
    PolicyEvidence currentPolicy)
{
    if (!string.Equals(
            decisionPolicy.PolicyId,
            currentPolicy.PolicyId,
            StringComparison.Ordinal))
    {
        return false;
    }

    return decisionPolicy.PolicyVersion switch
    {
        "2.0" when currentPolicy.PolicyVersion is "2.1" => true,
        _ => decisionPolicy.PolicyVersion == currentPolicy.PolicyVersion
    };
}
```

Treat this only as a teaching sketch.

Then answer:

- Who owns the compatibility rule in a production design?
- How is that rule versioned and audited?
- Could a permissive compatibility table silently defeat an important policy change?
- Should compatibility differ by operation risk?
- When should a rollout invalidate every in-flight decision regardless of expiration?

This extension exposes an important recursive point: once policy compatibility affects execution, **the compatibility rule is itself governance policy** and needs its own ownership and evidence model.

## Resetting the Sample

If you created a temporary branch only for the exercise, inspect your changes before discarding them:

```bash
git status
git diff
```

To restore the companion sample:

```bash
git restore samples/policy-context-and-explicit-decision-outcomes/PolicyContextAndExplicitDecisionOutcomes/Program.cs
```

Use `git status` first so you understand which local work will be affected.

---

## Related Content

- [Policy Versioning and Decision Provenance tutorial](../governance/policy-versioning-and-decision-provenance.md) — review the conceptual model this lab puts into practice, including stable policy identity, drift, freshness, fingerprints, and evidence boundaries.
- [Policy Context and Explicit Decision Outcomes tutorial](../tutorials/policy-context-and-explicit-decision-outcomes.md) — review explicit decision inputs, outputs, reason codes, and policy identity.
- [Policy Context and Explicit Decision Outcomes sample](https://github.com/AsiBackbone/Learning/blob/main/samples/policy-context-and-explicit-decision-outcomes/README.md) — use the intentionally small executable baseline for this lab.
- [Acknowledgment and Audit Residue tutorial](../tutorials/acknowledgment-and-audit-residue.md) — review acknowledgment binding, re-evaluation, correlation, and durable governance evidence.
- [Acknowledgment and Audit Residue lab](acknowledgment-and-audit-residue.md) — compare the broader lifecycle exercise, including policy-identity drift after acknowledgment.
- [Scoped Capability and Host-Owned Execution tutorial](../tutorials/scoped-capability-and-host-owned-execution.md) — continue into short-lived, narrowly bound execution authority and execution-boundary validation.
- [Scoped Capability and Host-Owned Execution lab](scoped-capability-and-host-owned-execution.md) — practice stale authority, resource freshness, expiration, and replay boundaries.
- [AsiBackbone/AsiBackbone](https://github.com/AsiBackbone/AsiBackbone) — inspect the fuller governance implementation after working through the teaching model.
- [Foundational Tutorial Index](../tutorials/index.md) — view the complete foundational governed-execution path.

---

> **Read it. Run it. Question it. Improve it.**
