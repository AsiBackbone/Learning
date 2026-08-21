---
description: Learn to distinguish authoritative deterministic policy facts from probabilistic or model-derived signals while preserving uncertainty, provenance, freshness, and host-owned execution.
---

# Deterministic and Probabilistic Inputs in Policy Evaluation

**Learning objective:** Understand how deterministic facts and probabilistic observations can coexist in policy context without disguising uncertainty as authority, and how a host can preserve provenance, threshold policy, freshness, and execution boundaries around model-derived or statistical signals.

**Pattern classification:** General learning material

**Difficulty:** Advanced

**Prerequisites:** [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md), [Constraint Composition and Policy Precedence](constraint-composition-and-policy-precedence.md), and [Risk-Based Decisions in Governed Systems](risk-based-decisions-in-governed-systems.md)

## At a Glance

> **Problem:** Governance decisions increasingly consume scores, classifications, anomaly indicators, forecasts, sensor confidence, and model output alongside ordinary application facts. If those signals are flattened into booleans or authoritative-looking fields, uncertainty and provenance disappear.
>
> **Core idea:** Represent deterministic facts and probabilistic signals differently. Preserve what was observed, where it came from, which model or scoring version produced it, how fresh it is, and which policy threshold interprets it.
>
> **Why it matters:** A probabilistic signal may inform policy, but uncertainty should not be disguised as an authoritative fact.
>
> **Prefer something simpler when:** A deterministic rule already expresses the requirement, no uncertain signal materially changes the decision, or the added scoring/model boundary would create complexity without improving governance.
>
> **Observe:** The host can make a deterministic governance decision from a captured probabilistic observation while still preserving that the observation itself was uncertain and may not be reproducible by re-running the upstream model later.

A useful flow is:

```text
Deterministic host facts
          +
Probabilistic observations
          ↓
Typed policy context
          ↓
Explicit policy interpretation
          ↓
Governance decision
          ↓
Scoped authority when needed
          ↓
Host-owned execution
```

For model-derived input, make the boundary even more explicit:

```text
Model output
   ↓
Typed probabilistic signal
   ↓
Host validation + provenance
   ↓
Policy interpretation
   ↓
Governance decision
   ↓
Scoped authority
   ↓
Host execution
```

The central lesson is:

> **A probabilistic signal may inform policy, but uncertainty should not be disguised as an authoritative fact.**

---

## The Architectural Difference

Consider these fields:

```text
ActorRole = Administrator
ResourceOwnerId = 42
AccountStatus = Active
```

and compare them with:

```text
FraudProbability = 0.81
ModelConfidence = 0.67
AnomalyScore = 0.92
RiskEstimate = High
```

They may all appear in a context object.

They do not all make the same kind of claim.

The first group normally represents host-resolved application facts:

```text
ActorRole = Administrator

Meaning:
The host's authoritative identity or role source currently says
the actor has the Administrator role.
```

The second group represents an observation or estimate:

```text
FraudProbability = 0.81

Meaning:
A named scoring process, using a particular model or method,
produced 0.81 for this input at a particular time.
```

That distinction is architectural.

It affects:

- Trust.
- Testing.
- Reproducibility.
- Threshold policy.
- Freshness.
- Audit evidence.
- Failure behavior.
- Human review.
- Execution authority.

---

## Deterministic Facts

A deterministic policy fact is a value the host treats as an authoritative statement for the current decision.

Examples include:

```text
ActorId = actor-42
ActorRole = Administrator
TenantId = tenant-a
ResourceOwnerId = actor-42
AccountStatus = Active
Region = us-east
Operation = payment.release
```

The word **deterministic** here does not mean the wider system can never change.

It means that for the decision snapshot, the host has resolved a concrete value and policy interprets that value directly.

For example:

```csharp
public sealed record DeterministicPaymentFacts(
    string ActorId,
    string ActorRole,
    string TenantId,
    string PaymentId,
    decimal Amount,
    string Currency,
    string AccountStatus,
    string Region);
```

Given the same captured facts and the same policy version, a deterministic policy should normally produce the same result.

That makes the decision easier to:

- Unit test.
- Reproduce.
- Review.
- Explain.
- Compare across policy versions.

---

## Deterministic Does Not Mean Permanently True

A deterministic fact can become stale.

For example:

```text
09:00  AccountStatus = Active
09:05  AccountStatus = Suspended
```

The original snapshot is still deterministic:

```text
At 09:00, the host observed AccountStatus = Active.
```

But it may no longer be fresh enough for execution at 09:06.

This is why deterministic facts can still need:

- Observation time.
- Resource version.
- Context fingerprint.
- Revalidation.
- Execution-freshness rules.

Do not confuse:

```text
Deterministic
```

with:

```text
Timeless
```

---

## Probabilistic Signals

A probabilistic signal expresses uncertainty, likelihood, confidence, score, rank, or model-derived classification.

Examples include:

```text
FraudProbability = 0.81
AnomalyScore = 0.92
FailureProbability = 0.12
Prediction = Suspicious
ClassProbability(Suspicious) = 0.74
SensorConfidence = 0.61
DemandForecast = 1280 ± uncertainty
```

The exact semantics vary.

A value of:

```text
0.81
```

might mean:

- An estimated probability.
- A normalized anomaly score.
- A ranking value.
- A vendor-specific confidence.
- A model logit transformed into a bounded score.
- A calibrated likelihood estimate.
- An uncalibrated heuristic score.

Those are not interchangeable.

Policy should know what kind of signal it is receiving.

---

## Do Not Turn a Model Statement into an Authoritative Boolean

Suppose a model emits:

```text
"This transaction is fraudulent."
```

A weak translation is:

```csharp
context.Fraudulent = true;
```

That loses:

- Which model produced the output.
- The exact class or score.
- Confidence or probability where available.
- Model version.
- Observation time.
- Threshold policy.
- Whether the result was advisory.
- Whether the signal was calibrated.
- Whether another model disagreed.

Prefer:

```text
Model output
      ↓
Typed observation
      ↓
Policy interpretation
```

For example:

```csharp
public sealed record FraudSignal(
    decimal FraudProbability,
    string PredictedClass,
    decimal? Confidence,
    string Provider,
    string ModelId,
    string ModelVersion,
    DateTimeOffset ObservedAt);
```

The signal says what the model produced.

Policy decides what that output means for governance.

---

## Authoritative Context and Advisory Context

A policy context can contain both authoritative host facts and advisory observations.

Make the distinction visible.

For example:

```csharp
public sealed record PaymentPolicyContext(
    DeterministicPaymentFacts Facts,
    FraudSignal? FraudSignal,
    string CorrelationId,
    string PolicyVersion,
    string? PolicyHash);
```

Conceptually:

```text
Facts
│
├── actor identity
├── account status
├── payment amount
├── tenant
└── resource ownership

Signals
│
├── fraud probability
├── anomaly score
└── confidence / uncertainty metadata
```

The host should not pretend that:

```text
FraudSignal.PredictedClass = Fraudulent
```

has the same authority as:

```text
Facts.AccountStatus = Suspended
```

unless the host's policy explicitly decides to treat that signal as sufficient evidence for a particular outcome.

Even then, the original signal provenance should remain visible.

---

## A Typed Signal Envelope

A reusable teaching shape can make uncertain input explicit:

```csharp
public sealed record ProbabilisticSignal(
    string SignalName,
    decimal Value,
    string ValueMeaning,
    string Source,
    string? ModelId,
    string? ModelVersion,
    DateTimeOffset ObservedAt,
    DateTimeOffset? ValidUntil,
    decimal? Confidence,
    string? CalibrationVersion);
```

Example:

```csharp
var signal = new ProbabilisticSignal(
    SignalName: "payment.fraud-probability",
    Value: 0.81m,
    ValueMeaning: "estimated-probability",
    Source: "fraud-service",
    ModelId: "fraud-detector",
    ModelVersion: "2026.08.3",
    ObservedAt: now,
    ValidUntil: now.AddMinutes(10),
    Confidence: null,
    CalibrationVersion: "calibration-2026-07");
```

This is deliberately descriptive.

It does not say:

```text
Authorized = false
```

or:

```text
Decision = Denied
```

Those remain policy outcomes.

---

## Preserve Signal Provenance

A score without provenance is difficult to interpret later.

Useful provenance can include:

```text
Signal name
Source service
Model identifier
Model version
Scoring-method version
Observed timestamp
Validity window
Input or resource identity
Calibration version when relevant
Schema version
Correlation identifier
```

For example:

```text
Signal:
payment.fraud-probability

Value:
0.81

Source:
fraud-service

Model:
fraud-detector

ModelVersion:
2026.08.3

ObservedAt:
2026-08-21T16:10:00Z

ValidUntil:
2026-08-21T16:20:00Z
```

The provenance answers:

> What exactly did the policy observe?

It does not prove that the model was correct.

That is an important limitation.

---

## Model Identity and Version Matter

A model-derived signal should not be treated as if the producing model were timeless.

Suppose:

```text
Model v7:
FraudProbability = 0.62

Model v8:
FraudProbability = 0.84
```

for the same transaction.

Historical decision analysis needs to know which model produced the observed value.

Otherwise:

```text
PolicyVersion = payment-policy/12
```

is not enough to reconstruct the full decision inputs.

Preserve both where relevant:

```text
PolicyVersion
ModelVersion
```

They answer different questions.

```text
PolicyVersion:
How did the host interpret the input?

ModelVersion:
Which inference or scoring process produced the input?
```

---

## A Confidence Score Is Not Automatically a Probability

The field name:

```text
Confidence = 0.87
```

does not have a universal meaning.

It may represent:

- Class confidence.
- Distance from a model boundary.
- Ensemble agreement.
- Sensor quality.
- Vendor-specific certainty.
- A heuristic transformation.
- An estimated probability.

Policy should not assume:

```text
Confidence 0.87
      =
87% chance the claim is true
```

unless the producing system defines and validates that interpretation.

Prefer metadata that explains the semantics:

```text
SignalName = document.classification
Value = Restricted
Confidence = 0.87
ConfidenceMeaning = model-class-confidence
```

The policy can then decide whether and how confidence matters.

---

## Model Confidence Is Not Authorization Confidence

Avoid the mental model:

```text
Model confidence
      =
Authorization confidence
```

A model can be highly confident about a fact that is irrelevant to authorization.

For example:

```text
Model confidence:
0.99 that a document contains financial data

Authorization question:
May actor-42 export this resource to partner-b?
```

The first may inform classification or risk.

It does not answer the authorization question.

Likewise:

```text
FraudProbability = 0.95
```

does not mean:

```text
95% confidence that the actor should be denied.
```

Policy still needs to interpret the signal together with deterministic rules and the operation's consequence.

---

## Calibration

When a signal is intended to represent probability, calibration becomes relevant.

Conceptually, a well-calibrated probability model should make values such as:

```text
0.80
```

behave like an 80% estimate across comparable observations over time.

Calibration asks whether the numeric probability corresponds reasonably to observed frequencies.

It is different from:

- Classification accuracy.
- Precision.
- Recall.
- Model confidence.
- Policy threshold choice.

A governance system normally should not implement calibration inside the policy evaluator.

Instead, the signal can carry calibration-related provenance when useful:

```text
ModelVersion = fraud-v8
CalibrationVersion = fraud-cal-2026-07
Probability = 0.81
```

Policy can then decide whether that model/calibration version is approved for the operation.

---

## Uncalibrated Scores Can Still Be Useful

Not every score must be a calibrated probability.

An anomaly detector might produce:

```text
AnomalyScore = 0.92
```

where higher means:

```text
more unusual relative to the model's learned baseline
```

That can still be a useful policy input.

But do not label it:

```text
92% chance of attack
```

unless that is actually what the score means.

The safer naming is:

```text
AnomalyScore = 0.92
SignalMeaning = normalized-anomaly-score
```

Then threshold policy can interpret it explicitly.

---

## Thresholds Convert Signals into Policy Rules

Suppose a fraud model produces:

```text
FraudProbability = 0.81
```

The number alone does not determine the governance outcome.

The policy might say:

```text
FraudProbability < 0.50
    → no fraud-specific restriction

0.50 <= FraudProbability < 0.80
    → AcknowledgmentRequired

0.80 <= FraudProbability < 0.95
    → EscalationRecommended

FraudProbability >= 0.95
    → Denied
```

Those thresholds are policy.

They are not objective properties of the model output.

The statement:

```text
0.80 means escalation
```

belongs to the governance policy.

Another application may legitimately use a different threshold.

---

## A Threshold Does Not Turn Uncertainty into Truth

When policy says:

```text
FraudProbability >= 0.80
      ↓
EscalationRecommended
```

it does **not** mean:

```text
Fraudulent = true
```

The correct interpretation is:

```text
Observed probability crossed the policy's escalation threshold.
```

That wording preserves the distinction between:

- Observation.
- Policy interpretation.
- Governance outcome.

This is especially important in audit evidence and user-facing explanations.

Prefer:

```text
ReasonCode:
payment.fraud-threshold.escalation

Evidence:
FraudProbability = 0.81
Threshold = 0.80
```

over:

```text
Reason:
Transaction is fraudulent.
```

unless some authoritative process has actually established that fact.

---

## Version Threshold Policy

Thresholds can change.

For example:

```text
Policy v12:
Escalate at >= 0.80

Policy v13:
Escalate at >= 0.75
```

The same observed signal:

```text
FraudProbability = 0.78
```

can therefore produce:

```text
v12 → not escalated
v13 → EscalationRecommended
```

That is expected if policy changed intentionally.

Preserve:

```text
Observed signal
+
Policy version
+
Threshold version or policy identity
```

so the decision can be interpreted later.

---

## Different Operations May Need Different Thresholds

A single global threshold may hide consequence differences.

For example:

```text
Read low-sensitivity data:
Escalate at >= 0.95

Release $50 payment:
Escalate at >= 0.85

Release $500,000 payment:
Escalate at >= 0.60
```

The signal may be the same.

The acceptable uncertainty differs because the consequence differs.

This is policy.

A useful mental model is:

```text
Probabilistic observation
      +
Operation consequence
      +
Deterministic constraints
      +
Policy thresholds
      ↓
Governance decision
```

---

## Regional and Tenant Thresholds

Regional or tenant overlays may interpret the same observation differently.

For example:

```text
Base policy:
Escalate at >= 0.80

Tenant-a:
Escalate at >= 0.75

Region-x:
Deny at >= 0.95
```

Keep the overlay contribution explicit.

Avoid:

```text
Model score silently changed from 0.81 to 0.91
because tenant policy is stricter.
```

The score should remain the observed score.

The policy should express the stricter threshold.

See [Regional and Tenant Policy Overlays](../advanced/regional-and-tenant-policy-overlays.md) for authority and provenance across multiple policy layers.

---

## Deterministic Policy from Probabilistic Observations

A governance decision can still be deterministic even when one input came from a probabilistic system.

Suppose the context captures:

```text
FraudProbability = 0.81
ModelVersion = fraud-v8
ObservedAt = 16:10
```

and policy v12 says:

```text
>= 0.80 → EscalationRecommended
```

Once the observation is captured, the decision function can be deterministic:

```text
Captured observation
+
Policy version
      ↓
Stable decision
```

This is different from calling the model again during every policy evaluation.

A useful pattern is:

```text
Inference / scoring
      ↓
Capture exact observed result
      ↓
Policy evaluates captured result
```

That separation improves reproducibility.

---

## Re-running the Model May Not Reproduce the Original Signal

Even with the same logical request, a later inference may differ because of:

- Model version changes.
- Stochastic inference.
- Different upstream data.
- Feature-store changes.
- Time-dependent features.
- Different preprocessing.
- Remote-provider changes.
- Floating-point or hardware differences.
- Updated calibration.
- Hidden service-side changes.

Therefore:

```text
Re-run model later
```

is not always equivalent to:

```text
Reconstruct original decision input.
```

Where historical decision evidence matters, preserve the exact observed value used by policy.

---

## Repeatability Has Two Layers

Separate:

### Policy repeatability

Given:

```text
same captured context
+
same policy version
```

the governance result should normally be reproducible.

### Signal-generation repeatability

Given:

```text
same original raw input
```

the external model or scoring system may or may not produce the exact same signal later.

This distinction prevents a governance evaluator from being blamed for nondeterminism introduced upstream.

---

## Capture the Exact Observed Signal

Suppose a decision used:

```text
FraudProbability = 0.8127
```

Do not preserve only:

```text
RiskBand = High
```

if the exact threshold comparison matters.

A useful evidence record might contain:

```csharp
public sealed record SignalEvidence(
    string SignalName,
    decimal Value,
    string ValueMeaning,
    string Source,
    string? ModelId,
    string? ModelVersion,
    DateTimeOffset ObservedAt,
    DateTimeOffset? ValidUntil,
    string? CalibrationVersion);
```

Then decision provenance can record:

```text
Signal evidence
+
Threshold policy
+
Final governance outcome
```

without pretending the score was an authoritative fact.

---

## Combine Multiple Signals Deliberately

A system may receive:

```text
FraudProbability = 0.71
DeviceAnomalyScore = 0.84
IdentityRiskScore = 0.42
SensorConfidence = 0.91
```

Do not automatically average them:

```text
(0.71 + 0.84 + 0.42 + 0.91) / 4 = 0.72
```

These values may measure unrelated things.

The average may have no defensible meaning.

Instead, define an explicit composition rule.

For example:

```text
FraudProbability >= 0.80
    → escalate

OR

FraudProbability >= 0.65
AND DeviceAnomalyScore >= 0.80
    → escalate

OR

IdentityRiskScore >= 0.90
    → deny
```

Now the policy explains how each signal contributes.

---

## Weighted Models Are Policy Too

Sometimes a weighted score is appropriate.

For example:

```text
Composite =
    0.50 * FraudProbability
  + 0.30 * DeviceRisk
  + 0.20 * TransactionRisk
```

If the governance layer owns this formula, the weights are policy.

They should be:

- Named.
- Reviewable.
- Versioned.
- Tested.
- Supported by domain reasoning.
- Preserved in policy identity where relevant.

Avoid a hidden utility function that quietly becomes the real governance policy.

---

## Class Probabilities

A classifier may produce multiple probabilities:

```text
Normal      = 0.12
Suspicious  = 0.63
Fraudulent  = 0.25
```

Do not discard the distribution too early if policy depends on uncertainty.

For example:

```text
Top class = Suspicious
```

does not reveal that:

```text
Fraudulent = 0.25
```

may still matter.

Policy can choose what to preserve.

A compact signal might retain:

```csharp
public sealed record ClassProbabilitySignal(
    string ModelId,
    string ModelVersion,
    IReadOnlyDictionary<string, decimal> Probabilities,
    DateTimeOffset ObservedAt);
```

The exact shape depends on the domain and storage constraints.

---

## Uncertainty Can Be a Policy Input

Sometimes uncertainty itself matters.

Example:

```text
PredictedClass = LowRisk
Confidence = 0.51
```

A policy may decide:

```text
Low-risk prediction
+
Low confidence
      ↓
HumanReviewRequired
```

while:

```text
Low-risk prediction
+
High confidence
      ↓
Allowed
```

The point is not that low confidence always requires a human.

The point is that uncertainty can be modeled explicitly instead of disappearing during translation.

---

## Human Review for High-Consequence Uncertainty

Human review can be useful when:

- Consequence is high.
- Model confidence is low.
- Signals conflict.
- Required evidence is missing.
- Model version is not approved.
- The signal is near a policy threshold.
- The model reports out-of-distribution input.
- A score cannot safely resolve the case.

For example:

```text
Payment amount >= $100,000
AND
FraudProbability between 0.65 and 0.85
      ↓
HumanReviewRequired
```

This is not a universal rule.

It is an example of policy acknowledging uncertainty rather than hiding it.

See [Human-in-the-Loop Governance Workflows](human-in-the-loop-governance-workflows.md) for the delayed review lifecycle and revalidation boundary.

---

## Unknown and Unavailable Are Real States

A missing probabilistic signal should not silently become:

```text
0.00
```

if zero means low risk.

Prefer explicit states.

For example:

```csharp
public enum SignalAvailability
{
    Available,
    Unavailable,
    Stale,
    Invalid
}

public sealed record ObservedSignal(
    SignalAvailability Availability,
    ProbabilisticSignal? Signal,
    string? FailureReasonCode);
```

Now policy can distinguish:

```text
Available score = 0.00
```

from:

```text
No score exists.
```

Those are not the same condition.

---

## Missing Services Need an Explicit Failure Posture

Suppose the fraud service is unavailable.

Possible policies include:

```text
Low-consequence operation
    → continue under deterministic rules

Moderate-consequence operation
    → Deferred

High-consequence operation
    → HumanReviewRequired

Critical operation
    → Denied
```

There is no universal fail-open or fail-closed answer.

The failure posture should depend on:

- Consequence.
- Required evidence.
- Alternative controls.
- Availability objectives.
- Legal or contractual requirements.
- Operational recovery path.

The important rule is:

> Dependency failure should not accidentally masquerade as a low-risk signal.

---

## Fail-Open and Fail-Closed Are Policy Choices

A simplistic design might say:

```csharp
try
{
    signal = await fraudService.ScoreAsync(...);
}
catch
{
    signal = new FraudSignal(0.0m, ...);
}
```

That is effectively fail-open if low scores permit execution.

A different design might always deny on any scoring failure.

That is fail-closed.

Both can be wrong if applied indiscriminately.

Prefer an explicit decision path:

```text
SignalUnavailable
      ↓
Policy chooses:
Allowed / Deferred / HumanReviewRequired / Denied
```

and preserve the reason code.

For broader degraded-mode reasoning, see the [Safe Degraded Mode and Fail-Safe Governance lab](../labs/safe-degraded-mode-and-fail-safe-governance.md).

---

## Freshness of Probabilistic Signals

Many probabilistic signals decay quickly.

Examples include:

- Fraud scores.
- Threat intelligence.
- Sensor confidence.
- Forecasts.
- Behavioral anomaly scores.
- Market or operational predictions.

A signal can carry:

```text
ObservedAt
ValidUntil
```

or a maximum-age policy.

For example:

```text
FraudProbability valid for 10 minutes.
```

After that:

```text
Signal = Stale
      ↓
Re-score or choose explicit stale-signal policy
```

Do not reuse an old score indefinitely merely because the stored number still parses.

---

## Re-evaluate When a Signal Expires

Suppose:

```text
16:10  FraudProbability = 0.42
16:20  Signal expires
16:25  Host attempts execution
```

If the execution decision depended materially on that score, the host may need to:

```text
Refresh signal
      ↓
Rebuild policy context
      ↓
Re-evaluate policy
```

A previously allowed governance decision should not automatically outlive the evidence that justified it.

This mirrors policy and context freshness in other delayed workflows.

---

## Model Drift

Model drift describes change in model behavior over time.

Examples include:

- Performance degrades on new data.
- Class boundaries become less useful.
- Probability calibration worsens.
- Error rates change by population or region.
- Model retraining changes output distribution.

Governance policy should not attempt to solve model monitoring by itself.

But it should preserve enough model identity and signal provenance to support:

- Incident analysis.
- Rollback.
- Model-version comparison.
- Threshold review.
- Policy re-evaluation.

A policy can also reject or defer signals from unapproved model versions.

---

## Data Drift

Data drift means the population or feature distribution changes.

For example:

```text
Training period:
mostly domestic transactions

Current period:
large increase in international transactions
```

The model may still run successfully.

Its score quality may have changed.

Again, the governance evaluator is not the drift detector by default.

But when an upstream monitoring system marks:

```text
ModelHealth = Degraded
```

or:

```text
OutOfDistribution = true
```

those observations can become explicit policy inputs.

Do not hide model-health state behind the same numeric score.

---

## Changed Model Versions

Suppose a host upgrades:

```text
fraud-v8
      ↓
fraud-v9
```

Policy should decide whether:

- v9 is immediately accepted.
- Thresholds change with v9.
- A validation period is required.
- High-consequence operations require human review initially.
- Old pending decisions must refresh their signals.
- v8 observations remain valid until expiration.

The model rollout and the policy rollout are related but separate changes.

Track them separately.

---

## Advisory Does Not Mean Ignorable

Calling a signal advisory does not mean policy must ignore it.

It means:

> The signal contributes evidence but does not independently own the authority boundary.

A policy can still make a strict rule:

```text
ApprovedFraudModel probability >= 0.95
      ↓
Denied
```

That is a deterministic policy rule over a probabilistic observation.

The authority comes from the policy, not from the model secretly executing a decision.

---

## A Worked Payment Example

Consider a payment-release operation.

### Deterministic facts

```text
ActorId = treasury-operator-12
ActorRole = PaymentOperator
TenantId = tenant-a
PaymentId = pay-981
Amount = 250000
Currency = USD
AccountStatus = Active
DestinationApproved = true
```

### Probabilistic observation

```text
SignalName = payment.fraud-probability
Value = 0.81
ValueMeaning = estimated-probability
Source = fraud-service
ModelId = fraud-detector
ModelVersion = 2026.08.3
ObservedAt = 16:10
ValidUntil = 16:20
```

### Policy

```text
If AccountStatus != Active
    → Denied

If ActorRole != PaymentOperator
    → Denied

If Amount >= 100000
AND fraud probability >= 0.80
    → EscalationRecommended

If Amount >= 100000
AND fraud probability is unavailable
    → HumanReviewRequired

Otherwise
    → Allowed
```

Notice the precedence:

```text
Deterministic denial
      ↓
cannot be weakened by a low model score
```

and:

```text
Probabilistic signal
      ↓
cannot execute the payment
```

---

## A Small Policy Example

A framework-neutral policy can make the interpretation visible:

```csharp
public sealed class PaymentReleasePolicy
{
    public GovernanceDecision Evaluate(
        PaymentPolicyContext context,
        DateTimeOffset now)
    {
        if (!string.Equals(
                context.Facts.ActorRole,
                "PaymentOperator",
                StringComparison.Ordinal))
        {
            return GovernanceDecision.Deny(
                "payment.actor.role-required",
                "The actor is not authorized for payment release.");
        }

        if (!string.Equals(
                context.Facts.AccountStatus,
                "Active",
                StringComparison.Ordinal))
        {
            return GovernanceDecision.Deny(
                "payment.account.not-active",
                "The payment account is not active.");
        }

        if (context.FraudSignal is null)
        {
            return context.Facts.Amount >= 100_000m
                ? GovernanceDecision.RequireHumanReview(
                    "payment.fraud-signal.unavailable")
                : GovernanceDecision.Allow();
        }

        FraudSignal signal = context.FraudSignal;

        if (now > signal.ObservedAt.AddMinutes(10))
        {
            return GovernanceDecision.Defer(
                "payment.fraud-signal.stale",
                "The fraud signal must be refreshed.");
        }

        if (context.Facts.Amount >= 100_000m &&
            signal.FraudProbability >= 0.80m)
        {
            return GovernanceDecision.Escalate(
                "payment.fraud-threshold.escalation",
                "The observed fraud probability crossed the escalation threshold.");
        }

        return GovernanceDecision.Allow();
    }
}
```

`RequireHumanReview` is teaching shorthand for a host-defined human-review outcome or workflow transition.

The important boundary is the evaluation logic:

```text
Captured facts + captured signal
      ↓
Explicit policy
      ↓
Decision
```

No payment is released inside the policy.

---

## Keep Deterministic Denials Stronger Than Advisory Signals

Suppose:

```text
ActorRole = Viewer
FraudProbability = 0.01
```

A low fraud score should not produce:

```text
Allowed
```

if deterministic authorization says the actor cannot release payments.

Prefer:

```text
Actor lacks required role
      ↓
Denied
```

The probabilistic signal never gets to broaden deterministic authority.

This mirrors the constraint-composition principle:

> A lower-risk observation should not silently weaken a mandatory constraint.

---

## Threshold Boundary Tests

Probabilistic inputs make boundary testing especially important.

If the policy threshold is:

```text
Escalate at >= 0.80
```

test:

| Observed value | Expected |
| ---: | --- |
| `0.7999` | Below escalation threshold |
| `0.8000` | `EscalationRecommended` |
| `0.8001` | `EscalationRecommended` |

Also test:

- Exactly missing.
- Stale.
- Invalid range.
- Unsupported model version.
- Wrong signal meaning.
- Approved model version.
- Model version change.
- Threshold version change.
- High-consequence amount just below and above its threshold.

The goal is not to test every floating-point value.

It is to make policy boundaries explicit.

---

## Test the Difference Between Score and Policy

A useful regression test keeps the observed score constant and changes only policy.

For example:

```text
Observed signal:
0.78
```

Policy v12:

```text
Escalate at >= 0.80
      ↓
Allowed
```

Policy v13:

```text
Escalate at >= 0.75
      ↓
EscalationRecommended
```

This proves that:

```text
The score did not change.
The policy interpretation changed.
```

That distinction is central to explainability.

---

## Test Unavailable Service Behavior

For each consequence class, define what a missing service means.

Example table:

| Consequence | Signal state | Expected outcome |
| --- | --- | --- |
| Low | Unavailable | Continue under deterministic policy |
| Moderate | Unavailable | Deferred |
| High | Unavailable | Human review |
| Critical | Unavailable | Denied |

The exact mapping belongs to the application.

The test should prove that unavailability cannot accidentally fall through to the low-risk path.

---

## Test Conflicting Signals

Suppose:

```text
FraudProbability = 0.32
DeviceAnomalyScore = 0.97
```

If policy says either high signal triggers escalation, test that exact rule.

If policy requires both, test that instead.

Do not let the implementation invent a combination strategy.

The decision table should reflect the documented composition policy.

---

## Preserve Provenance in the Final Decision

A governance record can preserve:

```json
{
  "correlationId": "corr-981",
  "operation": "payment.release",
  "outcome": "EscalationRecommended",
  "reasonCodes": [
    "payment.fraud-threshold.escalation"
  ],
  "policyVersion": "payment-policy/12",
  "policyHash": "sha256:...",
  "observedSignals": [
    {
      "name": "payment.fraud-probability",
      "value": 0.81,
      "valueMeaning": "estimated-probability",
      "source": "fraud-service",
      "modelId": "fraud-detector",
      "modelVersion": "2026.08.3",
      "observedAt": "2026-08-21T16:10:00Z",
      "validUntil": "2026-08-21T16:20:00Z",
      "calibrationVersion": "calibration-2026-07"
    }
  ]
}
```

This record supports the statement:

```text
Policy escalated because the observed value crossed its threshold.
```

It does not justify the stronger statement:

```text
The transaction was definitely fraudulent.
```

---

## Do Not Store More Model Input Than You Need

Provenance does not require copying:

- Entire prompts.
- Entire feature vectors.
- Raw private documents.
- Every sensor frame.
- Full customer records.
- Hidden model reasoning.
- Secrets or credentials.

Preserve enough structured evidence to understand the decision boundary.

If deeper forensic retention is required, use a separately governed storage design with explicit access, retention, and privacy controls.

---

## Policy Reasons Should Describe Interpretation

Prefer reason codes such as:

```text
payment.fraud-threshold.escalation
signal.fraud.unavailable
signal.fraud.stale
signal.model-version.unapproved
signal.confidence.insufficient
```

Avoid:

```text
model.says.bad
```

Stable reason codes should identify the policy condition that mattered.

Human-readable text can explain:

```text
The observed fraud probability crossed the escalation threshold
for this payment amount.
```

That is more accurate than asserting uncertain output as fact.

---

## Probabilistic Inputs Are Not AI-Specific

The same architecture appears without modern AI.

Examples include:

### Fraud engines

```text
FraudProbability = 0.81
```

### Statistical anomaly detection

```text
AnomalyScore = 0.92
```

### Sensor systems

```text
ObjectDetected = Person
DetectionConfidence = 0.74
```

### Forecasting

```text
FailureProbabilityNext24Hours = 0.28
```

### Credit or risk scoring

```text
RiskScore = 712
```

### Quality systems

```text
DefectProbability = 0.17
```

### Spam or abuse detection

```text
AbuseProbability = 0.89
```

The architectural question remains:

> How does uncertain evidence become an explicit, reviewable policy input without becoming hidden execution authority?

---

## AI-Specific Application

When an AI system contributes a score or classification, apply the same discipline used for AI tool proposals.

Avoid:

```text
Model says:
ResourceSensitivity = Low
      ↓
Policy context:
ResourceSensitivity = Low
```

when the resource catalog is the authoritative source.

But a model-derived field may legitimately be represented as:

```text
AdvisoryClassification = Low
ClassificationConfidence = 0.67
ModelId = classifier-v3
```

Policy can compare it with authoritative facts or route disagreement for review.

This connects directly to [Typed AI Proposed Intent and Schema-Validation Boundaries](../ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md).

---

## Host Validation Still Applies

A typed signal is not automatically trustworthy just because it deserializes.

The host may need to validate:

- Signal name.
- Numeric range.
- Source identity.
- Model identifier.
- Model version.
- Timestamp.
- Validity window.
- Calibration version.
- Allowed schema version.
- Correlation to the intended resource.
- Signature or provider authentication where applicable.

For example:

```text
FraudProbability = 4.7
```

should fail range validation if the contract requires:

```text
0.0 <= probability <= 1.0
```

That is input validation.

It is separate from the later policy threshold.

---

## Signal Validation Is Not the Governance Decision

Keep stages distinct:

| Stage | Question | Example result |
| --- | --- | --- |
| Parse | Can the signal representation be read? | Parsed |
| Contract validation | Does the signal satisfy schema/range rules? | Valid |
| Source validation | Is this an accepted source/model/version? | Accepted |
| Freshness | Is the signal current enough? | Current |
| Policy | What does this observation mean for this operation? | `EscalationRecommended` |
| Execution boundary | Is current scoped authority valid? | Allowed to execute or blocked |

A valid signal can still produce a denial.

An invalid signal may never reach policy.

A successful policy decision still does not execute the protected operation by itself.

---

## Re-evaluation Before Execution

Suppose a delayed workflow has:

```text
Initial signal:
FraudProbability = 0.42
ValidUntil = 16:20

Human review completes:
16:35
```

The original signal is stale.

The host should not silently reuse it.

A safer flow is:

```text
Human review satisfied
      ↓
Refresh authoritative facts
      ↓
Refresh expired probabilistic signals
      ↓
Rebuild policy context
      ↓
Re-evaluate current policy
      ↓
Issue scoped authority when appropriate
      ↓
Host-owned execution
```

Probabilistic freshness fits the same broader revalidation architecture as policy drift and resource drift.

---

## Scoped Authority Remains Separate

Even after policy returns:

```text
Allowed
```

the observed probability does not become an execution credential.

The final flow remains:

```text
Model / scoring service
      ↓
Observed signal
      ↓
Policy decision
      ↓
Scoped authority
      ↓
Execution-boundary validation
      ↓
Host-owned execution
```

No score should be accepted by the protected executor as a substitute for current authority.

---

## Common Failure Modes

### 1. Model Output Becomes an Authoritative Fact

```text
Model says "fraud"
      ↓
Fraudulent = true
```

Uncertainty and provenance disappear.

### 2. Confidence Is Treated as Probability Without Definition

```text
confidence = 0.9
      ↓
90% chance of truth
```

The producing system never promised that meaning.

### 3. Thresholds Are Hidden in Model Code

The governance team cannot identify which policy turned a score into a denial.

### 4. Threshold Crossing Becomes Objective Truth

```text
score >= 0.80
      ↓
Fraudulent = true
```

The policy rule is disguised as a fact.

### 5. Missing Signals Become Zero

Dependency failure looks like low risk.

### 6. Stale Signals Are Reused

A delayed workflow executes using expired evidence.

### 7. Model Version Is Not Preserved

Historical decisions cannot explain why a score changed after a model rollout.

### 8. Policy Version Is Preserved but Threshold Version Is Not

The interpretation boundary becomes difficult to reconstruct.

### 9. Unrelated Scores Are Averaged

Different units and meanings collapse into a meaningless composite.

### 10. Deterministic Denials Are Weakened by Low Risk

A model score becomes a bypass around authorization or mandatory policy.

### 11. Re-running the Model Is Treated as Reconstructing History

The new run differs from the actual signal used in the original decision.

### 12. Model Confidence Becomes Authorization Confidence

Prediction certainty is confused with actor authority.

### 13. The Model or Scoring Service Executes the Operation

The inference service becomes the protected executor.

### 14. Human Review Is Used Only as a Catch-All

Uncertain cases are routed to people without explicit eligibility, intent binding, freshness, or revalidation.

### 15. Raw Model Inputs Are Dumped into Audit Storage

Governance evidence becomes a secondary sensitive-data store.

---

## When Simpler Deterministic Policy Is Better

Probabilistic input is not inherently more sophisticated or more correct.

Prefer a direct deterministic rule when the requirement is already clear.

For example:

```text
If AccountStatus = Suspended
    → Denied
```

is better than:

```text
SuspensionRiskScore = 0.99
    → probably deny
```

when account status is already an authoritative fact.

Likewise:

```text
Destination is not on approved allowlist
    → Denied
```

may be clearer than asking a model whether the destination "looks trusted."

Use probabilistic input when uncertainty is real and the signal provides useful evidence.

Do not manufacture uncertainty where the host already has authoritative facts.

---

## Working Implementation References

This tutorial is framework-neutral.

The `AsiBackbone/AsiBackbone` repository provides useful implementation surfaces for carrying host-resolved context, evaluating constraints, applying final decision policy, and returning structured outcomes without turning signal generation into execution.

| Learning concept | Working implementation reference | What to inspect |
| --- | --- | --- |
| Policy-context contract | [`IAsiBackboneConstraintEvaluationContext`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Constraints/IAsiBackboneConstraintEvaluationContext.cs) | The minimal context boundary consumed by constraints. |
| Concrete host-provided context | [`AsiBackboneConstraintEvaluationContext`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Constraints/AsiBackboneConstraintEvaluationContext.cs) | Correlation, policy identity, and normalized metadata supplied by the host. |
| Policy evaluation | [`DefaultAsiBackbonePolicyEvaluator`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Evaluation/DefaultAsiBackbonePolicyEvaluator.cs) | Constraint evaluation and base decision composition. |
| Post-composition decision policy | [`IAsiBackboneDecisionPolicy`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Evaluation/IAsiBackboneDecisionPolicy.cs) | A host/domain boundary where broader policy can interpret composed results and context. |
| Structured governance result | [`GovernanceDecision`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecision.cs) | Outcome, reasons, correlation, and policy identity returned to the host. |
| Decision-policy examples | [Custom Decision Policy Examples](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/custom-decision-policy-examples.md) | Examples of host-provided risk metadata influencing a final decision while execution remains host-owned. |
| Execution enforcement | [Host-Owned Execution Enforcement](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/host-owned-execution-enforcement.md) | The boundary that keeps decisions and context separate from the protected side effect. |

The implementation does not require a particular ML platform, scoring service, probability representation, or calibration method.

A host can carry an observed signal as explicit metadata or a richer application-specific context type while preserving the same architecture:

```text
Host observes or receives signal
      ↓
Host validates and records provenance
      ↓
AsiBackbone-compatible policy context
      ↓
Constraint / decision policy interpretation
      ↓
GovernanceDecision
      ↓
Host-owned enforcement
```

---

## Review Questions

Before using probabilistic input in a governed decision, you should be able to answer:

1. Which fields are authoritative deterministic facts?
2. Which fields are probabilistic or model-derived observations?
3. What exactly does each score mean?
4. Is a "confidence" value actually a calibrated probability?
5. Which source produced the signal?
6. Which model or scoring version produced it?
7. When was it observed?
8. How long is it considered fresh?
9. What happens when it is stale?
10. Which policy threshold interprets it?
11. Is that threshold versioned?
12. Does the threshold vary by operation or consequence?
13. Can regional or tenant policy use a different threshold?
14. Does crossing a threshold produce a policy outcome rather than rewrite the observation as objective truth?
15. What happens when the signal service is unavailable?
16. Is missing evidence distinct from a low score?
17. Can deterministic denial still win over a favorable probabilistic signal?
18. How are multiple signals combined?
19. Are unrelated probabilities being averaged without justification?
20. Is the exact observed signal preserved in decision provenance?
21. Can a later model re-run differ from the original observation?
22. What happens when the model version changes?
23. What happens when calibration changes?
24. How are model drift and data drift surfaced to governance when relevant?
25. Which uncertain or high-consequence cases require human review?
26. Can the scoring or model service perform the protected operation?
27. Does an allowed decision still require current scoped authority and host enforcement?
28. Would a deterministic rule express the requirement more clearly?

If several answers are unclear, the system may have model scores in policy context, but it does not yet have a well-defined probabilistic-input governance boundary.

---

## Related Content

- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) — begin with explicit, authoritative decision-time facts and structured outcomes.
- [Risk-Based Decisions in Governed Systems](risk-based-decisions-in-governed-systems.md) — apply probabilistic evidence to explicit consequence, likelihood, risk bands, threshold policy, and freshness.
- [Constraint Composition and Policy Precedence](constraint-composition-and-policy-precedence.md) — preserve deterministic constraints and explicit precedence when uncertain observations enter the pipeline.
- [Policy Versioning and Decision Provenance](policy-versioning-and-decision-provenance.md) — preserve policy identity, drift, exact observed values, and historical decision evidence.
- [Practical Policy Testing and Decision-Table Strategies](practical-policy-testing-and-decision-table-strategies.md) — test threshold boundaries, missing signals, model-version changes, and failure posture.
- [Human-in-the-Loop Governance Workflows](human-in-the-loop-governance-workflows.md) — route uncertain or high-consequence cases into explicit human review without converting approval into execution authority.
- [Escalation Patterns in Governed Systems](escalation-patterns-in-governed-systems.md) — route a decision problem to another authority when probabilistic evidence cannot safely resolve it.
- [Regional and Tenant Policy Overlays](../advanced/regional-and-tenant-policy-overlays.md) — make threshold changes and local authority explicit rather than mutating observed scores.
- [Typed AI Proposed Intent and Schema-Validation Boundaries](../ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md) — apply the same host-owned acceptance discipline to model output that proposes operations or supplies model-derived context.
- [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) — follow model output through host validation, policy, scoped authority, and host-owned execution.
- [Safe Degraded Mode and Fail-Safe Governance lab](../labs/safe-degraded-mode-and-fail-safe-governance.md) — choose explicit behavior when a required probabilistic service is unavailable.
- [Threat Modeling as Architecture Reasoning](../security/threat-modeling-as-architecture-reasoning.md) — analyze spoofed signals, stale evidence, model substitution, provenance loss, threshold manipulation, and execution-bypass threats.

---

> **Preserve the uncertainty in the input and the authority in the policy.**
