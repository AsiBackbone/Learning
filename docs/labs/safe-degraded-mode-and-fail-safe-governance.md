---
description: Practice fail-safe governance when policy, replay, acknowledgment, verification, evidence, or execution dependencies are unavailable or uncertain.
---

# Lab — Safe Degraded Mode and Fail-Safe Governance

**Learning objective:** Practice deciding how a governed system should behave when a dependency required to establish policy, authority, replay state, acknowledgment, verification, evidence, or execution status is unavailable. Preserve the distinction between inability to establish a required trust fact, an explicit governance denial, and an operational execution failure.

**Difficulty:** Advanced

**Prerequisites:** Complete [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md), [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md), [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md), and [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md). Run the [Governed AI Tool Gateway sample](https://github.com/AsiBackbone/Learning/blob/main/samples/governed-ai-tool-gateway/README.md) before beginning. [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md), [Signing, Verification, Key Custody, and Tamper Evidence](../security/signing-verification-key-custody-and-tamper-evidence.md), [Centralized Error Handling and Problem Details](../aspnetcore/centralized-error-handling-and-problem-details.md), [Data Access Boundaries and Transaction Reasoning](../aspnetcore/data-access-boundaries-and-transaction-reasoning.md), and [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) provide the deeper failure-model context used throughout the exercise.

This lab extends the failure exercise already present in the Governed AI Tool Gateway lab.

That earlier exercise asks what should happen when policy is unavailable.

This lab broadens the question:

```text
A dependency is unavailable
        ↓
Which trust fact or operational property can no longer be established?
        ↓
Does this operation require that property?
        ↓
What explicit outcome is appropriate now?
```

The central invariant is:

> **A missing trust fact must not silently manufacture authority.**

That is deliberately different from:

> Always fail closed.

Some low-consequence operations may have a legitimate, bounded degraded mode.

Some high-consequence operations may need to defer or escalate.

An unavailable executor may leave an earlier `Allowed` decision intact while execution fails operationally.

The objective is to make those differences explicit and testable rather than hiding them behind one fallback boolean.

---

## Starting Architecture

Use the existing Governed AI Tool Gateway sample as the main executable surface:

```text
Model proposal
   ↓
Host validation
   ↓
Authoritative context
   ↓
Governance decision
   ↓
Acknowledgment when required
   ↓
Scoped capability
   ↓
Replay/use-state check
   ↓
Host-owned dry-run executor
   ↓
Audit residue
```

The sample already demonstrates a useful rule:

> **The model may propose. The host retains execution authority.**

For this lab, add temporary failure seams around the host-owned dependencies.

You are not turning the sample into a production resilience framework.

You are making failure behavior observable.

## Prepare the Lab

Work on a temporary branch or disposable copy.

```bash
git switch -c lab/safe-degraded-mode
```

Run the current sample tests first:

```bash
dotnet test samples/governed-ai-tool-gateway/GovernedAiToolGateway.Tests/GovernedAiToolGateway.Tests.csproj
```

Then run the full sample suite:

```bash
dotnet test samples/Samples.slnx
```

Record the baseline before changing anything.

No real external action should occur during this lab.

---

# Part 1 — Classify the Operation Before Classifying the Failure

Do not begin with:

```text
Dependency unavailable
   ↓
Fail open or fail closed?
```

Begin with the operation.

Define two operation profiles for the exercise.

One should be low consequence and read-only, for example:

```text
case.summary.read
```

The other should be consequential, for example the existing:

```text
notification.send
```

Do not invent a numeric risk score merely to make the exercise look precise.

Use explicit properties instead.

A useful lab-only profile might capture:

```text
Operation
ReadOnly
ExternalSideEffect
Reversible
ReplaySensitive
RequiresVerifiedAuthority
RequiresAcknowledgment
RequiresDurableEvidence
```

For each operation, answer:

1. What state can change?
2. Is an external system involved?
3. Can the effect be reversed reliably?
4. Could replay produce additional consequence?
5. Is signed or otherwise verified authority required?
6. Is acknowledgment part of the policy path?
7. Must durable governance evidence exist before execution?
8. What is the consequence of refusing service temporarily?
9. What is the consequence of continuing with one trust property missing?

The point is not to prove that one operation is objectively "low risk."

The point is to make the assumptions that drive degraded-mode behavior visible.

---

# Part 2 — Separate Dependency Health from Governance Outcome

Create a small availability model for the exercise.

For example:

```csharp
public enum DependencyAvailability
{
    Available,
    Unavailable,
    Stale,
    Unknown
}
```

Do **not** make this enum a governance decision.

This:

```text
Policy provider = Unavailable
```

is a fact about a dependency.

It is not automatically:

```text
Decision = Denied
```

or:

```text
Decision = Allowed
```

The host must still interpret the failure relative to the operation and the trust property that dependency normally establishes.

Likewise, a circuit breaker state such as:

```text
Open
```

means something like:

```text
Calls are currently being suppressed because the dependency is unhealthy.
```

It does not answer:

```text
Should this governed operation execute?
```

## Required Exercise

Create a failure-injection seam that can make these dependencies report an explicit state:

```text
Policy provider
Replay/use store
Signing provider
Verification/trust-anchor provider
Acknowledgment state
Audit/evidence store
External executor
```

Keep the failure seam deterministic.

Do not add real network dependencies merely to simulate unavailability.

---

# Part 3 — Preserve "Cannot Determine" as a Real State

Start with the policy provider.

Introduce this failure:

```text
Policy provider unavailable
```

First implement the dangerous fallback:

```csharp
try
{
    return policyProvider.Evaluate(context);
}
catch
{
    return GovernanceDecision.Allow();
}
```

Run a consequential operation.

Explain exactly what happened.

The dependency failed to establish policy.

The fallback converted:

```text
Cannot evaluate
```

into:

```text
Allowed
```

The system manufactured authority from missing information.

Remove that fallback.

Now choose an explicit outcome for the consequential operation.

Valid candidates may include:

```text
Deferred
Denied
EscalationRecommended
```

Your choice should depend on the operation and deployment policy.

Do not represent a temporary inability to evaluate as a permanent policy rejection unless that is the intended contract.

## Required Test

Create a test with this shape:

```text
Policy provider unavailable
        +
Consequential operation
        ↓
No silent Allowed result
        ↓
Protected executor invocation count = 0
```

The test should also verify a stable reason code such as:

```text
policy.provider-unavailable
```

Use a reason code appropriate to your implementation.

---

# Part 4 — Design a Bounded Cached-Policy Degraded Mode

Now examine a case where a low-consequence read-only operation may legitimately continue while the primary policy source is unavailable.

Do not implement:

```text
Last known decision = Allowed
        ↓
Allowed forever
```

A cached-policy path should answer at least:

```text
Which policy version?
Which policy fingerprint?
Which tenant / region / operation binding?
When was it fetched?
When does the degraded-use window expire?
Was the cached artifact verified?
What operation classes may use it?
```

Create a cache record with explicit freshness metadata.

For example:

```text
PolicyVersion
PolicyFingerprint
Operation
ScopeOrTenant
FetchedUtc
UsableUntilUtc
```

Then decide whether your low-consequence operation may use it.

The important property is:

> **Degraded-mode authority must be explicit, narrow, and freshness-bounded.**

## Required Experiments

Test all three cases:

```text
Primary policy available
        ↓
Current policy used
```

```text
Primary policy unavailable
        +
Fresh explicitly permitted cached policy
        ↓
Bounded degraded continuation
```

```text
Primary policy unavailable
        +
Cached policy outside freshness window
        ↓
No degraded continuation
```

If you permit the middle case, make the result observable.

For example, preserve:

```text
DegradedMode = true
PolicyVersion = ...
PolicyFingerprint = ...
ReasonCode = policy.cached-fallback
```

Do not make the degraded path indistinguishable from the ordinary healthy path.

---

# Part 5 — Make Replay-State Failure Non-Permissive When Replay State Is Required

Use the [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) tutorial and companion sample as the canonical replay reference.

Simulate:

```text
Replay/use store unavailable
```

For an operation marked:

```text
ReplaySensitive = true
```

the host cannot currently establish:

```text
Does another permitted use remain?
```

Implement the dangerous fallback first:

```text
Use store unavailable
        ↓
Assume unused
        ↓
Execute
```

Then remove it.

Choose an explicit non-executing result for the consequential replay-sensitive path.

Possible host behavior includes:

```text
Deferred
Denied
EscalationRecommended
Queue for later validation
```

The exact answer is part of the exercise.

## Contrast With an Operation That Does Not Need Replay State

For the low-consequence read-only operation, decide whether replay state is relevant at all.

If the operation is safe to repeat and no bounded-use authority is being consumed, a replay-store outage may be unrelated to that path.

This distinction matters:

```text
Dependency exists somewhere in the system
```

does not mean:

```text
Every operation must depend on it.
```

## Required Test

Prove:

```text
Replay-sensitive consequential operation
        +
Required use store unavailable
        ↓
Executor invocation count = 0
```

Do not prove only that an exception was thrown.

Prove that the protected side effect boundary was not crossed.

---

# Part 6 — Separate Signing Failure From Verification Failure

Signing and verification fail at different stages.

## Signing Provider Unavailable

Suppose a valid decision has already been produced but a signed capability is required before authority may cross a trust boundary.

Simulate:

```text
Decision = Allowed
        ↓
Signing provider unavailable
```

Do not fall back to:

```text
Emit unsigned capability
```

or:

```text
Reuse an old signature
```

or:

```text
Mark SignatureVerified = true
```

The policy decision may still be `Allowed`.

Capability issuance cannot complete under the required proof policy.

Model that as its own later-stage failure.

A retry may be appropriate after the signing dependency recovers.

## Verification Provider or Trust Anchor Unavailable

Now simulate a capability arriving at the execution boundary when required verification cannot be completed.

Distinguish:

```text
Signature invalid
```

from:

```text
Verification unavailable
```

The first is negative trust evidence.

The second is an inability to establish required trust.

Both may block a consequential path, but they are operationally different and should normally have different reason codes.

If verification is required for that path:

```text
Cannot verify
        ≠
Verified
```

Do not accept a caller-supplied public key as an emergency trust root.

## Required Tests

Add tests that prove:

```text
Signing required
        +
Signing provider unavailable
        ↓
No execution capability minted
```

and:

```text
Verified authority required
        +
Verification unavailable
        ↓
Protected executor invocation count = 0
```

Also verify that the failure is not reported as an ordinary policy denial unless your design intentionally maps it that way.

---

# Part 7 — Do Not Treat Missing Acknowledgment State as Satisfied Acknowledgment

Simulate:

```text
Decision = AcknowledgmentRequired
        ↓
Acknowledgment evidence store unavailable
```

Dangerous fallback:

```text
Could not load acknowledgment
        ↓
Assume acknowledged
        ↓
Continue
```

That converts missing evidence into responsibility acceptance.

Remove it.

Decide whether the correct operational path is:

```text
Deferred
EscalationRecommended
New acknowledgment after recovery
Another policy-defined non-executing state
```

Now compare with a path whose policy result is already:

```text
Allowed
```

and does not require acknowledgment.

Do not make acknowledgment storage a mandatory dependency for operations whose decision path never required acknowledgment.

## Required Test

For a path requiring acknowledgment, prove:

```text
Acknowledgment state unavailable
        ↓
Acknowledgment not treated as satisfied
        ↓
No capability issuance
        ↓
No execution
```

---

# Part 8 — Distinguish Telemetry Failure From Governance-Evidence Failure

Simulate two different failures:

```text
Operational logger unavailable
```

and:

```text
Required audit/evidence store unavailable
```

They are not automatically the same failure.

Operational telemetry may help diagnose the system.

Governance evidence may be part of the host's execution contract.

If the policy requires durable evidence before or atomically with a consequential operation, this is unsafe reasoning:

```text
Audit store unavailable
        ↓
Console log written
        ↓
Evidence requirement satisfied
```

A log line is not automatically a durable governance record.

## Design Two Evidence Policies

For the low-consequence operation, you may choose something like:

```text
Continue
        +
Reduced telemetry
```

if telemetry is not a trust prerequisite.

For the consequential operation, choose deliberately among designs such as:

```text
Defer until evidence store returns
```

```text
Escalate
```

```text
Persist to an approved durable local outbox
        ↓
Execute only after local durable commit
        ↓
Forward evidence later
```

Do not call an in-memory queue a durable buffer.

Do not invent a fallback store whose integrity, retention, or ownership is weaker than the requirement it replaces without documenting that reduction.

## Required Test

Your test should reflect the policy you chose.

If durable evidence is required before execution:

```text
Evidence store unavailable
        ↓
Executor invocation count = 0
```

If you deliberately allow a durable local buffer:

```text
Evidence primary unavailable
        ↓
Local durable record committed
        ↓
Explicit degraded-mode marker
        ↓
Execution may proceed under bounded policy
```

The lab does not prescribe which answer is universal.

It requires you to make the guarantee visible.

---

# Part 9 — Keep Executor Failure Separate From Governance Denial

Now let every governance dependency succeed.

Produce:

```text
Decision = Allowed
```

Issue and validate the appropriate execution authority.

Then simulate:

```text
External executor unavailable
```

Do **not** rewrite the original decision as:

```text
Denied
```

The policy did not deny the operation.

Execution failed operationally.

Preserve separate states such as:

```text
Decision = Allowed
Execution = NotStarted / Unavailable
```

or:

```text
Decision = Allowed
Execution = OutcomeUnknown
```

depending on the failure point.

This distinction is critical for recovery.

## Failure Before the External Call

```text
Capability validated
        ↓
Executor dependency known unavailable
        ↓
External call never attempted
```

The host may be able to retry later, subject to fresh policy and authority.

## Failure After the External Call Becomes Ambiguous

```text
External call sent
        ↓
Response lost
        ↓
Outcome unknown
```

Do not assume:

```text
Failure response
=
Side effect did not occur
```

Use reconciliation, provider idempotency, operation identity, or a new governed recovery path as appropriate.

## Required Test

Prove that executor unavailability preserves the distinction:

```text
Decision = Allowed
Execution = Failed or Unavailable
```

Do not force the failure through the policy engine merely to obtain a `Denied` value.

---

# Part 10 — Build the Scenario Matrix

Complete this matrix before finalizing your implementation.

Do not copy one answer down every column.

| Dependency / failure | Trust or operational property that is missing | Low-consequence read-only operation | Consequential operation | Evidence / reason code |
| --- | --- | --- | --- | --- |
| Policy source unavailable |  |  |  |  |
| Cached policy stale |  |  |  |  |
| Replay/use store unavailable |  |  |  |  |
| Signing provider unavailable |  |  |  |  |
| Verification/trust anchor unavailable |  |  |  |  |
| Acknowledgment state unavailable |  |  |  |  |
| Audit/evidence store unavailable |  |  |  |  |
| Operational logger unavailable |  |  |  |  |
| External executor unavailable |  |  |  |  |
| External execution outcome ambiguous |  |  |  |  |

For every row, answer:

1. What exact property is unavailable?
2. Does this operation require that property?
3. Is the failure temporary, stale, invalid, or unknown?
4. Is the result a governance outcome or an execution status?
5. If degraded continuation is allowed, what narrows it?
6. What freshness limit applies?
7. What evidence proves the degraded path was used?
8. How is recovery triggered?
9. What prevents a fallback from granting broader authority?

The matrix is the main design artifact of the lab.

---

# Part 11 — Encode the Failure Policy in Tests

Do not leave degraded-mode behavior only in comments or runbooks.

Add focused tests for the invariants your matrix claims.

At minimum, cover:

```text
Policy unavailable
        +
Consequential operation
        ↓
No silent authorization
```

```text
Replay store unavailable
        +
Replay-sensitive operation
        ↓
No execution
```

```text
Verification unavailable
        +
Verified authority required
        ↓
No execution
```

```text
Acknowledgment state unavailable
        +
AcknowledgmentRequired
        ↓
No capability issuance
```

```text
Stale cached policy
        ↓
No degraded continuation
```

```text
Executor unavailable
        +
Decision already Allowed
        ↓
Decision remains distinguishable from execution failure
```

If your matrix permits a low-consequence degraded mode, add a positive test too:

```text
Explicitly permitted low-consequence operation
        +
Approved fresh fallback
        ↓
DegradedMode = true
        ↓
Bounded continuation
```

That positive test is important.

Otherwise the system may accidentally become "deny everything during an outage" rather than implementing the degraded mode you intended.

## Test the Boundary, Not Only the Mapper

A test that proves only:

```text
Map(Unavailable) = Deferred
```

is incomplete for consequential execution.

Also verify the execution boundary:

```text
Handler invocation count = 0
```

or the equivalent host-owned side-effect observation.

The contract is behavioral.

---

# Part 12 — Add Recovery and Reconciliation

A degraded-mode policy is incomplete without a return path.

For each temporary failure, define what happens when the dependency recovers.

Possible actions include:

```text
Re-evaluate current policy
Refresh cached policy
Retry verification
Re-load acknowledgment evidence
Re-attempt capability issuance
Reconcile replay state
Flush durable evidence buffer
Query external provider
Create a new governed operation
Escalate unresolved ambiguity
```

Do not assume that recovery means:

```text
Retry the exact old command with the exact old authority.
```

Context may have changed.

Policy may have changed.

The capability may have expired.

The resource may have changed.

The original bounded use may already have been consumed.

Recovery should therefore ask:

> **Which facts and authority must be re-established now?**

---

# Part 13 — Threat-Model the Fallback Path

A degraded mode is itself a security-sensitive execution path.

Review it for bypass behavior.

At minimum, inspect these dangerous patterns:

```text
catch { return Allowed; }
```

```text
authorizationResult ?? true
```

```text
Verification failed
        ↓
Use caller-supplied key
```

```text
Replay store unavailable
        ↓
Skip use check
```

```text
Primary credential unavailable
        ↓
Use broader emergency credential
```

```text
Audit unavailable
        ↓
Disable evidence requirement silently
```

```text
Policy unavailable
        ↓
Use cache with no freshness limit
```

```text
Main gateway unavailable
        ↓
Call executor through an undocumented bypass endpoint
```

For each fallback, ask:

```text
Does this preserve availability?
```

and separately:

```text
Does this create new authority?
```

Those questions are not interchangeable.

---

# Final Validation

Run the focused gateway tests again:

```bash
dotnet test samples/governed-ai-tool-gateway/GovernedAiToolGateway.Tests/GovernedAiToolGateway.Tests.csproj
```

Then run the full sample suite:

```bash
dotnet test samples/Samples.slnx
```

Confirm that you can explain all of these statements independently:

```text
A dependency is healthy.
A dependency is unavailable.
A trust fact was established.
A trust fact could not be established.
Policy explicitly denied the operation.
Policy deferred the operation.
Policy recommended escalation.
A bounded degraded mode was selected.
A capability was successfully issued.
A capability could not be verified.
A required use check could not be completed.
Acknowledgment evidence was unavailable.
Required audit evidence was durably preserved.
The executor was unavailable.
The external execution outcome is unknown.
```

Also confirm:

- No required trust dependency becomes optional because an exception occurred.
- `Unavailable` does not silently become `Allowed`.
- A low-consequence degraded mode, if supported, is explicit and bounded.
- Cached policy use is bound to a freshness window and relevant policy identity.
- Replay-sensitive execution does not proceed when required use state cannot be established.
- Required verification failure does not create an unsigned or self-trusted bypass.
- Missing acknowledgment evidence is not treated as satisfied acknowledgment.
- Operational logging is not confused with required governance evidence.
- Executor failure does not rewrite a prior governance decision.
- Recovery re-establishes current facts and authority rather than blindly replaying stale authority.

---

# Completion Criteria

You have completed the lab when you can answer:

1. Why is `Unavailable` not the same as `Denied`?
2. Why is `Unavailable` not the same as `Allowed`?
3. What makes a degraded mode explicit rather than accidental?
4. Which operation properties should influence failure behavior?
5. When can cached policy be considered at all?
6. What must bound cached-policy freshness?
7. Why should replay-store failure block only paths that actually require replay state?
8. Why is signing-provider failure different from verification failure?
9. Why is "cannot verify" different from "signature invalid"?
10. Why can missing acknowledgment state not be treated as acceptance?
11. When can audit buffering be a legitimate degraded mode?
12. Why is an in-memory evidence queue not automatically a durable fallback?
13. Why should executor unavailability not rewrite `Allowed` as `Denied`?
14. What is the difference between an execution failure and an unknown execution outcome?
15. What must be re-established before recovery retries a consequential operation?
16. Which fallback paths could create more authority than the healthy path?
17. Why can a circuit breaker inform governance without being the governance decision itself?
18. Which invariants should be proved at the host-owned execution boundary?

There is intentionally no universal answer key for the scenario matrix.

A good result is one where another engineer can inspect the operation profile, dependency state, explicit outcome, freshness bound, execution behavior, and evidence trail and understand exactly why the system continued or stopped.

---

## Optional Extension — Add a Degraded-Mode Receipt

Create a small evidence record for any path that continues while a normally used dependency is unavailable.

Include fields such as:

```text
CorrelationId
Operation
Dependency
ObservedAvailability
SelectedOutcome
DegradedMode
PolicyVersion
PolicyFingerprint
FallbackSource
FallbackFreshUntilUtc
ReasonCode
OccurredUtc
```

Do not include secrets or raw bearer authority.

Then answer:

> Can an operator later prove that this action used the normal healthy path or a bounded degraded path?

---

## Optional Extension — Circuit Breaker Versus Governance Policy

Add a simple circuit-breaker state around the simulated policy provider.

Demonstrate:

```text
Circuit closed
        ↓
Provider call attempted
```

```text
Circuit open
        ↓
Provider call suppressed
        ↓
Dependency health reported as unavailable
        ↓
Host still selects explicit governance behavior
```

The circuit breaker protects the dependency and controls call behavior.

It does not grant execution authority.

---

## Resetting the Sample

Inspect your changes first:

```bash
git status
git diff
```

If your lab changes are disposable, restore the baseline sample:

```bash
git restore samples/governed-ai-tool-gateway
```

If you added temporary files under the sample directory, remove only the files you created after reviewing `git status`.

---

## Related Content

- [Governed AI Tool Gateway advanced lab](governed-ai-tool-gateway.md) — begin with the broader composed gateway threat model before specializing in failure policy.
- [Governed AI Tool Gateway sample](https://github.com/AsiBackbone/Learning/blob/main/samples/governed-ai-tool-gateway/README.md) — use the existing deterministic host-owned execution boundary as the main lab surface.
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) — preserve `Deferred`, `AcknowledgmentRequired`, and `EscalationRecommended` instead of collapsing failure behavior into a boolean.
- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) — distinguish responsibility evidence from authorization and execution.
- [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md) — identify which component owns each trust property before choosing degraded behavior.
- [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) — reason about replay-store unavailability, atomic consumption, and failure windows.
- [Signing, Verification, Key Custody, and Tamper Evidence](../security/signing-verification-key-custody-and-tamper-evidence.md) — distinguish cryptographic verification from current authority and safe execution.
- [Centralized Error Handling and Problem Details](../aspnetcore/centralized-error-handling-and-problem-details.md) — keep expected governance outcomes distinct from unexpected operational exceptions.
- [Data Access Boundaries and Transaction Reasoning](../aspnetcore/data-access-boundaries-and-transaction-reasoning.md) — reason about persistence failure, transactions, and durable state ownership.
- [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) — bind cached-policy and degraded-mode reasoning to explicit policy identity and freshness.
- [AsiBackbone](https://github.com/AsiBackbone/AsiBackbone) — inspect fuller implementation seams after completing the framework-neutral exercise.

---

> **Read it. Run it. Question it. Improve it.**
