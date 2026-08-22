---
description: Learn to use explicit, reviewable risk information as one input to governance decisions without turning a score or model into hidden authorization or execution authority.
---

# Risk-Based Decisions in Governed Systems

**Learning objective:** Understand how consequence, uncertainty, exposure, resource sensitivity, and environmental conditions can influence a governance decision while risk assessment remains reviewable, bounded, versioned, and separate from authorization and host-owned execution.

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) and [Constraint Composition and Policy Precedence](constraint-composition-and-policy-precedence.md)

## At a Glance

> **Problem:** The same proposed operation can have very different consequences depending on actor, resource, amount, destination, uncertainty, environment, or other risk-relevant facts. A boolean rule may be too coarse, but an opaque score can become hidden policy.
>
> **Core idea:** Make risk factors and evidence explicit, assess them separately from authorization, and let a versioned governance policy map the resulting risk posture into an explicit outcome.
>
> **Why it matters:** Risk may influence a governance decision, but risk scoring should not silently become execution authority.
>
> **Prefer something simpler when:** Deterministic rules already express the requirement clearly, the consequence does not vary materially, or a numerical or categorical risk model would add ceremony without improving the decision.
>
> **Observe:** The same operation can produce different governance outcomes because explicitly modeled risk context changed, while the host still owns the protected side effect.

A useful conceptual flow is:

```text
Proposed operation
        ↓
Authoritative policy context
        ↓
Deterministic constraints
        ↓
Risk assessment
        ↓
Governance decision
        ↓
Host-owned continuation or execution
```

The central lesson is:

> **Risk assessment is an input to governance. It is not authorization, a governance decision, or execution authority.**

That distinction matters most when a system introduces scores, categories, classifiers, threat signals, fraud indicators, confidence values, or other signals that can look authoritative simply because they are numeric or machine-generated.

---

## The Problem: Same Operation, Different Consequence

Consider a governed data export.

The operation name may be identical:

```text
data.export
```

But the surrounding facts can vary dramatically.

### Low-consequence instance

```text
Actor:            employee
Resource:         internal test dataset
Records:          200
Destination:      approved internal storage
Classification:   Internal
Environment:      Normal
```

### Higher-consequence instance

```text
Actor:            contractor
Resource:         customer dataset
Records:          250000
Destination:      external partner
Classification:   Restricted
Environment:      Elevated incident posture
```

A policy expressed only as:

```text
Actor has Export role
      ↓
Allowed
```

may be too coarse for the second case.

But replacing it with:

```text
Risk score = 87
      ↓
Denied
```

does not automatically improve the architecture.

The new design now has different questions:

```text
Where did 87 come from?
Which facts contributed?
What does 87 mean?
Which threshold makes 87 a denial?
Who owns that threshold?
Was the score current?
What happens if one input is unavailable?
Can the score be reproduced?
Can a regional or tenant policy change the mapping?
Did the risk service just become the authorization system?
```

The goal is therefore not to add a risk score.

The goal is to make variable consequence **explicit and governable**.

---

## Keep Risk Assessment in Its Own Boundary

Risk assessment answers a narrower question than authorization or execution:

> **Given these observed factors, what risk posture and supporting evidence should policy consider?**

It should return reviewable posture, reasons, and evidence rather than silently deciding whether the caller is authorized or performing the protected operation itself.

The broader separations are covered canonically elsewhere:

- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) explains how context becomes an explicit governance outcome.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) explains why an allowed decision still does not transfer execution ownership away from the host.

The risk-specific rule for this article is therefore:

```text
Risk assessment
      ↓
Governance policy interprets the assessment
      ↓
Host enforces the resulting decision
```

Keeping that boundary explicit prevents a scorer, classifier, or risk service from quietly becoming the authorization system or executor.

---

## Start with Authoritative Policy Context

Risk evaluation should consume facts that the host is prepared to treat as authoritative for the decision.

A framework-neutral context might include:

```csharp
public sealed record ExportRiskContext(
    string ActorId,
    string ActorType,
    string ActorTenant,
    string ResourceId,
    string ResourceTenant,
    string Classification,
    int RecordCount,
    string DestinationKind,
    string Region,
    bool ElevatedIncidentPosture,
    decimal? ExternalThreatScore,
    DateTimeOffset? ExternalThreatObservedAt,
    string CorrelationId,
    string PolicyVersion);
```

This record intentionally mixes:

- Actor facts.
- Resource facts.
- Operation magnitude.
- Destination facts.
- Environmental state.
- An optional external signal.
- Policy and correlation identity.

It does **not** contain:

```text
FinalDecision = Denied
```

or:

```text
RiskService.ExecuteExport()
```

The context describes the situation.

Policy interprets that situation.

### Authoritative does not mean infallible

A host can still receive incorrect data.

The architectural point is narrower:

> The system should know which source supplied each fact and why that source is trusted for this decision.

For example:

```text
Actor identity
    ← authenticated host principal

Resource classification
    ← resource catalog

Record count
    ← export planner

Region
    ← host-resolved resource location

External threat signal
    ← named risk provider observed at a known time
```

Do not let a request self-declare a lower-risk context when the host has a better source.

---

## Risk Factors Are Not Policy Rules

A risk factor describes something that may matter.

A policy rule decides what that fact means.

For example:

```text
RISK FACTOR
Resource classification = Restricted

POLICY INTERPRETATION
Restricted data raises consequence severity.
```

Or:

```text
RISK FACTOR
External threat signal unavailable

POLICY INTERPRETATION
For this operation, missing threat evidence requires deferment.
```

Those are separate statements.

Keeping them separate makes it easier to:

- Review the model.
- Change thresholds.
- Version policy.
- Test boundaries.
- Explain decisions.
- Replace a signal provider.
- Detect when a signal has become stale.
- Prevent a scorer from silently acquiring policy authority.

---

## Static and Contextual Risk

Some risk-relevant facts change slowly.

Others can change between two otherwise identical requests.

### Static or slow-changing factors

Examples include:

- Resource classification.
- Operation category.
- Data-retention class.
- Account protection level.
- Device capability class.
- System criticality tier.

These facts may still change, but they are relatively stable for a decision window.

### Contextual factors

Examples include:

- Transaction value.
- Record count.
- Current destination.
- Current actor assurance.
- Time-sensitive threat intelligence.
- Active incident posture.
- Current system health.
- Current geographic location.
- Current uncertainty in an upstream classification.

The distinction matters for freshness.

A decision that depends only on stable resource classification may remain valid longer than one that depends on a five-minute threat signal.

---

## Actor, Resource, Operation, and Environment Can All Contribute

Risk should not be modeled as a property of only the actor.

A useful review structure is:

```text
Actor
  +
Resource
  +
Operation
  +
Environment
  ↓
Risk-relevant evidence
```

### Actor contribution

Examples:

- Identity assurance.
- Account age.
- Role or employment type.
- Session assurance.
- Prior acknowledgment state.

### Resource contribution

Examples:

- Sensitivity.
- Criticality.
- Tenant ownership.
- Irreversibility of change.
- Recovery cost.

### Operation contribution

Examples:

- Amount.
- Scope.
- Reversibility.
- Number of affected records.
- Privilege change.
- External exposure.

### Environment contribution

Examples:

- Current incident posture.
- Destination jurisdiction.
- Maintenance or degraded mode.
- Network location.
- Time-sensitive threat signals.

This decomposition discourages vague reasoning such as:

```text
userRisk = high
```

when the real issue is:

```text
ordinary user
+
highly sensitive resource
+
large irreversible operation
+
elevated incident posture
```

---

## Qualitative Models Are Often Enough

Not every governed system needs a numerical risk engine.

For many applications, explicit categories are easier to review and test.

For example:

```csharp
public enum ConsequenceLevel
{
    Minor,
    Material,
    Severe,
    Catastrophic
}

public enum LikelihoodLevel
{
    Unlikely,
    Possible,
    Likely
}

public enum RiskBand
{
    Low,
    Moderate,
    High,
    Critical,
    Unknown
}
```

The categories should have domain-specific definitions.

Do not assume words such as `High` or `Critical` are self-explanatory.

A policy document might define:

```text
Severe consequence
    = material customer, security, operational, or legal impact

Likely
    = current evidence indicates the harmful condition is expected
      often enough to require stronger handling
```

The exact definitions belong to the domain.

---

## Separate Consequence from Likelihood Where Useful

A single score can hide why something is risky.

A two-dimensional model keeps two different questions visible:

```text
Consequence:
How bad would the adverse outcome be?

Likelihood:
How plausible is that adverse outcome under current conditions?
```

A qualitative matrix could be:

| Consequence \ Likelihood | Unlikely | Possible | Likely |
| --- | --- | --- | --- |
| Minor | Low | Low | Moderate |
| Material | Low | Moderate | High |
| Severe | Moderate | High | Critical |
| Catastrophic | High | Critical | Critical |

This matrix is itself policy.

Another organization could choose a different mapping.

That is acceptable when the choice is:

- Intentional.
- Documented.
- Versioned.
- Tested.
- Preserved in decision provenance where needed.

The matrix should not be hidden inside an enum cast or an unexplained multiplication.

---

## Numerical Scores Can Be Useful, but Avoid False Precision

Some domains legitimately use quantitative signals.

For example:

```text
Fraud model probability
Threat score
Transaction amount
Failure probability
Exposure estimate
Confidence value
```

The mistake is treating numerical output as more authoritative than the evidence supports.

Avoid implying:

```text
Risk = 73.42
```

means the system understands risk to two decimal places.

A numerical score may instead be one observed signal:

```csharp
public sealed record ExternalRiskSignal(
    decimal Score,
    decimal? Confidence,
    string Source,
    DateTimeOffset ObservedAt,
    string ModelVersion);
```

The governance policy can then define what to do with that signal.

For example:

```text
Score >= 80
AND signal is no older than 5 minutes
AND model version is approved
      ↓
Treat as High external-risk evidence
```

The threshold and freshness rule are policy.

The external model is not the final decision authority.

---

## Make the Assessment Reviewable

A risk assessment should carry enough structure to explain what mattered.

A teaching model could be:

```csharp
public sealed record RiskReason(
    string Code,
    string Message);

public sealed record RiskEvidence(
    string Factor,
    string Value,
    string Source,
    DateTimeOffset ObservedAt);

public sealed record RiskAssessment(
    RiskBand Band,
    ConsequenceLevel Consequence,
    LikelihoodLevel Likelihood,
    IReadOnlyList<RiskReason> Reasons,
    IReadOnlyList<RiskEvidence> Evidence,
    DateTimeOffset AssessedAt,
    DateTimeOffset ValidUntil,
    string AssessmentPolicyVersion);
```

A result might look conceptually like:

```text
Band:          High
Consequence:   Severe
Likelihood:    Possible

Reasons:
- export.restricted-data
- export.large-volume
- environment.elevated-incident-posture

Assessment policy:
risk/export/7
```

This is more reviewable than:

```text
risk = 87
```

Do not store unnecessary secrets or personal data simply because an audit record exists.

Preserve the evidence needed to understand the decision without turning provenance into a data dump.

---

## A Small Qualitative Assessor

The assessor can remain deterministic and easy to inspect.

```csharp
public sealed class ExportRiskAssessor
{
    public RiskAssessment Assess(
        ExportRiskContext context,
        DateTimeOffset now)
    {
        ConsequenceLevel consequence =
            DetermineConsequence(context);

        LikelihoodLevel likelihood =
            DetermineLikelihood(context);

        RiskBand band =
            MapRisk(consequence, likelihood);

        RiskReason[] reasons =
            BuildReasons(context, consequence, likelihood);

        RiskEvidence[] evidence =
        [
            new(
                "classification",
                context.Classification,
                "resource-catalog",
                now),

            new(
                "record-count",
                context.RecordCount.ToString(),
                "export-planner",
                now),

            new(
                "incident-posture",
                context.ElevatedIncidentPosture
                    ? "elevated"
                    : "normal",
                "host-environment",
                now)
        ];

        return new RiskAssessment(
            band,
            consequence,
            likelihood,
            reasons,
            evidence,
            AssessedAt: now,
            ValidUntil: now.AddMinutes(5),
            AssessmentPolicyVersion: "risk/export/7");
    }

    private static ConsequenceLevel DetermineConsequence(
        ExportRiskContext context)
    {
        if (string.Equals(
                context.Classification,
                "Restricted",
                StringComparison.OrdinalIgnoreCase) &&
            context.RecordCount >= 100_000)
        {
            return ConsequenceLevel.Severe;
        }

        if (context.RecordCount >= 10_000)
        {
            return ConsequenceLevel.Material;
        }

        return ConsequenceLevel.Minor;
    }

    private static LikelihoodLevel DetermineLikelihood(
        ExportRiskContext context)
    {
        if (context.ElevatedIncidentPosture)
        {
            return LikelihoodLevel.Likely;
        }

        if (string.Equals(
                context.DestinationKind,
                "ExternalPartner",
                StringComparison.OrdinalIgnoreCase))
        {
            return LikelihoodLevel.Possible;
        }

        return LikelihoodLevel.Unlikely;
    }

    private static RiskBand MapRisk(
        ConsequenceLevel consequence,
        LikelihoodLevel likelihood)
    {
        return (consequence, likelihood) switch
        {
            (ConsequenceLevel.Minor, LikelihoodLevel.Unlikely)
                => RiskBand.Low,

            (ConsequenceLevel.Minor, LikelihoodLevel.Possible)
                => RiskBand.Low,

            (ConsequenceLevel.Minor, LikelihoodLevel.Likely)
                => RiskBand.Moderate,

            (ConsequenceLevel.Material, LikelihoodLevel.Unlikely)
                => RiskBand.Low,

            (ConsequenceLevel.Material, LikelihoodLevel.Possible)
                => RiskBand.Moderate,

            (ConsequenceLevel.Material, LikelihoodLevel.Likely)
                => RiskBand.High,

            (ConsequenceLevel.Severe, LikelihoodLevel.Unlikely)
                => RiskBand.Moderate,

            (ConsequenceLevel.Severe, LikelihoodLevel.Possible)
                => RiskBand.High,

            (ConsequenceLevel.Severe, LikelihoodLevel.Likely)
                => RiskBand.Critical,

            (ConsequenceLevel.Catastrophic, LikelihoodLevel.Unlikely)
                => RiskBand.High,

            _ => RiskBand.Critical
        };
    }

    private static RiskReason[] BuildReasons(
        ExportRiskContext context,
        ConsequenceLevel consequence,
        LikelihoodLevel likelihood)
    {
        List<RiskReason> reasons = [];

        if (string.Equals(
                context.Classification,
                "Restricted",
                StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(new(
                "export.restricted-data",
                "The export contains restricted data."));
        }

        if (context.RecordCount >= 100_000)
        {
            reasons.Add(new(
                "export.large-volume",
                "The export exceeds the high-volume threshold."));
        }

        if (context.ElevatedIncidentPosture)
        {
            reasons.Add(new(
                "environment.elevated-incident-posture",
                "The environment is operating under an elevated incident posture."));
        }

        reasons.Add(new(
            "risk.consequence",
            $"Consequence was assessed as {consequence}."));

        reasons.Add(new(
            "risk.likelihood",
            $"Likelihood was assessed as {likelihood}."));

        return [.. reasons];
    }
}
```

This is a teaching example.

A production system may use a rules engine, policy engine, statistical model, external risk service, or another mechanism.

The architectural lesson remains the same:

> **The assessor produces evidence and a risk posture. It does not execute the operation.**

---

## Mapping Risk to Governance Outcomes Is Policy

A risk band becomes operationally meaningful only when a policy says what the band means.

For example:

| Risk band | Governance outcome |
| --- | --- |
| Low | `Allowed` |
| Moderate | `AcknowledgmentRequired` |
| High | `EscalationRecommended` |
| Critical | `Denied` |
| Unknown | Host-defined failure posture |

That table is not a universal truth.

It is a policy decision.

Therefore it should be:

- Named.
- Versioned.
- Reviewable.
- Tested.
- Included in provenance where the mapping materially affected the decision.

A framework-neutral policy might be:

```csharp
public sealed class RiskDecisionPolicy
{
    public GovernanceDecision Apply(
        GovernanceDecision baseDecision,
        RiskAssessment risk)
    {
        // Risk policy may narrow or reshape a proceedable result.
        // It must not silently loosen an existing block.
        if (!baseDecision.CanProceed)
        {
            return baseDecision;
        }

        return risk.Band switch
        {
            RiskBand.Low =>
                baseDecision,

            RiskBand.Moderate =>
                GovernanceDecision.RequireAcknowledgment(
                    "risk.moderate",
                    "The operation requires acknowledgment under risk policy risk/export/7."),

            RiskBand.High =>
                GovernanceDecision.Escalate(
                    "risk.high",
                    "The operation requires escalation under risk policy risk/export/7."),

            RiskBand.Critical =>
                GovernanceDecision.Deny(
                    "risk.critical",
                    "The operation is denied under risk policy risk/export/7."),

            RiskBand.Unknown =>
                GovernanceDecision.Defer(
                    "risk.unavailable",
                    "Required risk evidence is unavailable."),

            _ =>
                GovernanceDecision.Defer(
                    "risk.unrecognized",
                    "The risk posture could not be interpreted.")
        };
    }
}
```

The exact type names are illustrative.

The important boundary is:

```text
Base decision
      +
Risk assessment
      +
Versioned risk mapping policy
      ↓
Final governance decision
```

not:

```text
Risk service
      ↓
Protected side effect
```

---

## Deterministic Constraints Should Still Matter

Risk should usually be one input among multiple composed constraints.

Suppose a policy has a deterministic tenant rule:

```text
Actor tenant must equal resource tenant.
```

If that rule returns `Denied`, a low risk score should not convert the operation into `Allowed`.

A safe sequence is:

```text
Authoritative context
      ↓
Deterministic constraints
      ↓
Base decision

If base decision blocks:
      ↓
Preserve block

If base decision can proceed:
      ↓
Risk assessment
      ↓
Risk-aware decision policy
```

This keeps risk from becoming a bypass around explicit constraints.

The same principle applies when a mandatory legal, security, or business rule already prohibits an operation.

Risk can narrow a proceedable path.

It should not silently erase a mandatory denial.

---

## Worked Example: Same Export, Different Outcomes

Assume the active mapping is:

```text
Low       → Allowed
Moderate  → AcknowledgmentRequired
High      → EscalationRecommended
Critical  → Denied
```

Now evaluate the same `data.export` operation under different contexts.

| Case | Resource / operation context | Environment | Risk band | Governance outcome |
| --- | --- | --- | --- | --- |
| A | 200 internal test records to approved storage | Normal | Low | `Allowed` |
| B | 25,000 internal records to an approved external partner | Normal | Moderate | `AcknowledgmentRequired` |
| C | 250,000 restricted records to an approved external partner | Normal | High | `EscalationRecommended` |
| D | 250,000 restricted records to an external partner | Elevated incident posture | Critical | `Denied` |

The operation name did not change.

The risk-relevant context did.

That is the point of the pattern.

---

## Risk Categories Versus Raw Scores

A risk category and a raw numerical score serve different purposes.

### Raw score

Useful when:

- A mature domain already defines the score.
- Thresholds have an established operational meaning.
- Calibration is measured.
- Score provenance is available.
- Consumers understand uncertainty.

Risks:

- False precision.
- Threshold gaming.
- Hidden weighting.
- Calibration drift.
- Teams treating the number as unquestionable authority.

### Risk category

Useful when:

- The domain can express meaningful ordinal states.
- Reviewability matters more than numerical granularity.
- The decision policy uses a small number of operational postures.

Risks:

- Category boundaries can still be arbitrary.
- Different teams may interpret labels differently.
- A category can hide the underlying evidence if reasons are not preserved.

Neither representation is inherently safer.

The safer architecture is the one that keeps:

```text
Inputs
Assessment method
Category or score
Thresholds
Mapping policy
Decision
```

visible and reviewable.

---

## Handling Unavailable Risk Signals

Risk inputs fail.

Examples include:

- A threat-intelligence service is unavailable.
- A classifier times out.
- A model version is not approved.
- A sensor stops reporting.
- A resource classification is missing.
- A score is stale.
- A confidence value falls below the accepted threshold.

Do not let an unavailable signal silently become:

```text
Risk = Low
```

Represent uncertainty explicitly.

For example:

```text
RiskBand.Unknown
```

or:

```csharp
public sealed record RiskSignalState(
    bool Available,
    bool Fresh,
    string Source,
    string? FailureCode);
```

Then let policy decide the failure posture.

Possible mappings include:

```text
Unknown → Deferred
Unknown → EscalationRecommended
Unknown → Denied
Unknown → Allowed with warning
```

There is no universal answer.

For a low-consequence reversible operation, fail-open-with-warning may be acceptable.

For an irreversible high-consequence operation, fail-closed may be more appropriate.

The important lesson is:

> **Fail-open versus fail-closed behavior is policy, not an accidental default from a failed risk dependency.**

See [Safe Degraded Mode and Fail-Safe Governance lab](../labs/safe-degraded-mode-and-fail-safe-governance.md) for the broader failure-posture discussion.

---

## Re-Evaluate When Risk-Relevant Context Changes

A risk-based decision is only meaningful for the context it assessed.

Suppose an operation is evaluated at 10:00:

```text
Destination: approved internal storage
Incident posture: normal
Risk: Low
Decision: Allowed
```

At 10:03, before execution:

```text
Destination changed to external partner
Incident posture changed to elevated
```

The original decision should not automatically remain valid.

Useful freshness evidence may include:

```csharp
public sealed record RiskFreshness(
    DateTimeOffset AssessedAt,
    DateTimeOffset ValidUntil,
    string ContextFingerprint,
    string AssessmentPolicyVersion);
```

Before execution, the host can ask:

```text
Is the decision still within its validity window?
Did a risk-relevant fact change?
Did the risk policy change?
Did an external signal expire?
Did the regional or tenant policy set change?
```

If yes:

```text
Rebuild authoritative context
      ↓
Reassess risk
      ↓
Re-evaluate governance
```

This is risk-based execution freshness.

The execution boundary should not assume that a previously favorable risk assessment is valid forever.

---

## Policy Changes Can Alter Risk Thresholds

Suppose version 7 defines:

```text
Large export threshold = 100000 records
```

and version 8 changes it to:

```text
Large export threshold = 50000 records
```

The same context can now legitimately produce a different risk assessment.

That is why risk policy identity matters.

Useful provenance may include:

```text
riskPolicyId
riskPolicyVersion
riskPolicyHash
assessmentPolicyVersion
decisionPolicyVersion
```

Not every application needs all five fields.

The general requirement is:

> A reviewer should be able to determine which policy interpretation turned observed evidence into the risk posture and which policy mapping turned that posture into the governance decision.

See [Policy Versioning and Decision Provenance](policy-versioning-and-decision-provenance.md) for the broader versioning and provenance model.

---

## Regional and Tenant Policy Overlays

Risk posture can interact with more than one policy authority.

For example:

```text
Global policy:
Moderate → Allowed with warning

Regional policy:
Moderate export of Restricted data → AcknowledgmentRequired

Tenant policy:
Any external Restricted export → EscalationRecommended
```

The important question is not which layer appears last in a list.

The important questions are:

```text
Which policy scopes apply?
Which layers may narrow authority?
Which layers may broaden a default?
Which denials are mandatory?
Which overrides are explicitly delegated?
Which risk thresholds belong to which authority?
Which policy identities survive in provenance?
```

A regional or tenant overlay should not silently mutate the risk score until nobody can explain the original evidence.

Prefer preserving the distinction:

```text
Base risk assessment
      ↓
Applicable policy overlays
      ↓
Explicit final governance outcome
```

This keeps the assessment reviewable while allowing different policy authorities to respond differently to the same risk posture.

See [Regional and Tenant Policy Overlays](../advanced/regional-and-tenant-policy-overlays.md) for the overlay authority and precedence model.

---

## Risk Evidence Belongs in Decision Provenance

If risk materially changed the outcome, preserve enough evidence to explain that change.

Useful fields can include:

```text
Risk band
Consequence level
Likelihood level
Risk reason codes
Signal sources
Signal observation times
Signal/model version where relevant
Assessment policy version
Mapping policy version
Missing-signal state
Correlation ID
```

For example:

```json
{
  "correlationId": "corr-123",
  "riskBand": "High",
  "consequence": "Severe",
  "likelihood": "Possible",
  "riskReasons": [
    "export.restricted-data",
    "export.large-volume"
  ],
  "riskPolicyVersion": "risk/export/7",
  "decisionPolicyVersion": "governance/export/12",
  "assessedAt": "2026-08-21T16:00:00Z",
  "validUntil": "2026-08-21T16:05:00Z"
}
```

The receipt does not need to copy every raw signal.

Preserve what is needed for:

- Review.
- Reproduction.
- Incident analysis.
- Threshold-change analysis.
- Policy drift analysis.

Apply normal privacy and data-minimization rules.

---

## Testing Risk Threshold Boundaries

Risk policy is especially vulnerable to off-by-one and threshold mistakes.

Suppose the consequence rule is:

```text
RecordCount < 10000
    → Minor

10000 <= RecordCount < 100000
    → Material

RecordCount >= 100000
    → Severe
```

Do not test only:

```text
500
50000
250000
```

Test the boundaries:

```text
9999
10000
10001

99999
100000
100001
```

A focused decision table might be:

| Record count | Classification | Incident posture | Expected consequence | Expected risk | Expected outcome |
| ---: | --- | --- | --- | --- | --- |
| 9,999 | Internal | Normal | Minor | Low | `Allowed` |
| 10,000 | Internal | Normal | Material | Low | `Allowed` |
| 10,001 | Internal | Normal | Material | Low | `Allowed` |
| 99,999 | Restricted | Normal | Material | Moderate | `AcknowledgmentRequired` |
| 100,000 | Restricted | Normal | Severe | High | `EscalationRecommended` |
| 100,001 | Restricted | Elevated | Severe | Critical | `Denied` |

Then add missing-signal cases:

| Signal state | Expected risk posture | Expected governance behavior |
| --- | --- | --- |
| Required external signal available and fresh | Normal assessment | Use configured mapping |
| Required signal stale | `Unknown` | Defer, escalate, or deny according to policy |
| Required signal unavailable | `Unknown` | Use explicit failure posture |
| Unapproved model version | `Unknown` | Do not silently trust the score |

The exact expected results depend on the domain.

What matters is that the boundary behavior is executable and reviewable.

See [Practical Policy Testing and Decision-Table Strategies](practical-policy-testing-and-decision-table-strategies.md) for the broader testing method.

---

## Test Policy Changes, Not Only Assessment Code

A risk engine can remain unchanged while policy changes around it.

For example:

```text
Version 7:
High → EscalationRecommended

Version 8:
High → Denied
```

A regression test should prove that:

```text
Same authoritative context
+
Same risk assessment
+
Different versioned mapping policy
      ↓
Different expected governance outcome
```

That is not nondeterminism.

It is policy evolution.

The receipt should make the version difference visible.

---

## Treat Probabilistic Signals as Observed Inputs

Some risk factors are inherently probabilistic.

For example:

```text
Fraud probability = 0.82
Classification confidence = 0.71
Threat likelihood = 0.64
```

Do not hide the model call inside a constraint if reproducibility matters.

Prefer:

```text
Host or trusted adapter obtains signal
      ↓
Observed score + source + model version + time
      ↓
Policy context
      ↓
Risk assessment
      ↓
Governance policy
```

This allows the decision to explain what it actually observed.

It also keeps a probabilistic model from becoming the invisible source of execution authority.

A later Learning topic can go deeper on deterministic versus probabilistic policy inputs.

For this tutorial, the key rule is:

> **Probabilistic evidence may inform policy. It should not erase the distinction between evidence, policy, decision, and execution.**

---

## Host-Owned Execution Still Comes Last

After the final governance decision exists, the host still owns continuation.

For example:

```csharp
GovernanceDecision decision =
    await evaluator.EvaluateAsync(
        context,
        cancellationToken);

if (!decision.CanProceed)
{
    await auditSink.WriteAsync(
        decision,
        cancellationToken);

    return MapDecision(decision);
}

// If the architecture requires a fresh risk check,
// perform it before issuing or accepting execution authority.

await auditSink.WriteAsync(
    decision,
    cancellationToken);

await exportService.ExecuteAsync(
    request,
    cancellationToken);
```

In a fuller architecture, an allowed or acknowledged decision may still need:

- Capability issuance.
- Capability validation.
- Freshness checks.
- Gateway validation.
- Idempotency protection.
- Final host-side invariant checks.

Risk assessment does not replace those boundaries.

---

## When a Simpler Pattern Is Better

Do not add risk modeling merely because the word "risk" appears in a requirement.

A direct deterministic rule is often better when the requirement is already clear:

```text
Restricted data may never leave Region A.
```

That can remain:

```csharp
if (context.Classification == "Restricted" &&
    context.Region != "RegionA")
{
    return GovernanceDecision.Deny(
        "export.region-prohibited",
        "Restricted data may not leave Region A.");
}
```

There is no benefit in converting it to:

```text
Risk += 37
```

A simpler architecture is often preferable when:

- One explicit rule decides the case.
- The operation has little consequence variation.
- Risk factors would duplicate existing deterministic constraints.
- The team cannot define category semantics clearly.
- No one can explain or validate the proposed score.
- The risk model would be less testable than direct policy.
- The additional machinery would not change host behavior.

Use risk-based decision models when varying consequence is a real domain concern, not as a default architectural fashion.

---

## Common Failure Modes

### 1. The Score Becomes Authorization

```text
risk < 50
      ↓
execute
```

The architecture has skipped authorization, governance, and host enforcement.

### 2. Hidden Weights Create Hidden Policy

```text
risk = actor * 0.2
     + resource * 0.3
     + amount * 0.5
```

If nobody can justify the weights, the formula is policy by accident.

### 3. Missing Signals Become Low Risk

A failed dependency returns zero, which the system interprets as safe.

Unavailable evidence should be represented explicitly.

### 4. A Low Score Overrides a Deterministic Denial

Risk becomes a bypass around mandatory constraints.

Preserve explicit denials unless an authorized override policy says otherwise.

### 5. Thresholds Are Not Versioned

A production threshold changes and historical decisions can no longer be reconstructed.

### 6. Stale Risk Evidence Is Reused

A decision is allowed under one destination or environment and executed after the context changed.

### 7. Reasons Preserve Only the Final Number

```text
Denied because risk = 87.
```

This is weak evidence.

Preserve the factors or reason codes that materially mattered.

### 8. Numerical Precision Exceeds Model Quality

A score displays several decimal places even though the underlying evidence is categorical, subjective, or poorly calibrated.

### 9. Regional or Tenant Overlays Mutate Scores Invisibly

A downstream policy changes the number rather than making its own policy contribution explicit.

### 10. The Risk Service Performs the Side Effect

The service that evaluates risk also sends the payment, deletes the record, deploys the release, or executes the robot command.

Risk assessment has crossed into execution authority.

---

## Working Implementation Map

This tutorial is framework-neutral, but the `AsiBackbone/AsiBackbone` repository contains useful implementation references for the same boundaries.

| Learning concept | Working implementation reference | What to inspect |
| --- | --- | --- |
| Host/domain decision policy | [`IAsiBackboneDecisionPolicy`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Evaluation/IAsiBackboneDecisionPolicy.cs) | The post-composition boundary where host policy can refine a decision without executing the protected action. |
| Risk-aware policy example | [Custom Decision Policy Examples](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/custom-decision-policy-examples.md) | The regional overlay example reads host-provided `risk` metadata and can require acknowledgment while preserving host-owned execution. |
| High-risk workflow | [High-Risk Administrative Action Scenario](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/scenarios/high-risk-administrative-action.md) | A concrete scenario where actor, target, risk, policy metadata, acknowledgment, audit residue, and host execution remain separate responsibilities. |
| Structured decision result | [`GovernanceDecision`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecision.cs) | The outcome and reason structure consumed by the host. |
| Policy evaluation | [`DefaultAsiBackbonePolicyEvaluator`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Evaluation/DefaultAsiBackbonePolicyEvaluator.cs) | Constraint evaluation, base composition, and the optional decision-policy boundary. |

The implementation repository does not require every host to adopt the qualitative model used in this tutorial.

The important mapping is architectural:

```text
Host-provided facts
      ↓
Constraints and assessment
      ↓
Decision policy
      ↓
GovernanceDecision
      ↓
Host-owned enforcement and execution
```

---

## Review Questions

Before applying this pattern, you should be able to answer:

1. Which facts make the same operation more or less consequential?
2. Which risk inputs are authoritative, and where do they come from?
3. Which factors are static and which are contextual?
4. Are consequence and likelihood separate dimensions in this domain?
5. Is a qualitative model sufficient?
6. If a numerical score is used, what does it actually measure?
7. Which policy maps a score or category to a governance outcome?
8. Is that mapping versioned?
9. Can a low risk result override a deterministic denial?
10. What happens when a required risk signal is unavailable?
11. How is fail-open versus fail-closed behavior chosen?
12. Which risk reasons and evidence survive in provenance?
13. How long is the assessment valid?
14. Which context changes require re-evaluation?
15. Can regional or tenant policies respond differently to the same risk posture without hiding their authority?
16. Are threshold boundaries covered by decision-table tests?
17. Can the same context, assessment method, and policy version produce a stable result?
18. Does the final governance decision remain separate from execution authority?
19. Would a direct deterministic rule solve the problem more clearly?

If several answers are unclear, the system may have a risk score, but it does not yet have a well-defined risk-based governance architecture.

---

## Related Content

- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) — begin with explicit actor, resource, operation, environment, policy, and correlation facts.
- [Constraint Composition and Policy Precedence](constraint-composition-and-policy-precedence.md) — keep deterministic constraints and composition rules explicit before introducing risk-aware decision policy.
- [Deterministic and Probabilistic Inputs in Policy Evaluation](deterministic-and-probabilistic-inputs-in-policy-evaluation.md) — distinguish authoritative facts from uncertain observations and preserve model identity, calibration, freshness, and threshold policy.
- [Policy Versioning and Decision Provenance](policy-versioning-and-decision-provenance.md) — preserve policy identity, drift, freshness, and reconstructable decision evidence.
- [Practical Policy Testing and Decision-Table Strategies](practical-policy-testing-and-decision-table-strategies.md) — test risk thresholds, equivalence classes, failure posture, and decision boundaries systematically.
- [Regional and Tenant Policy Overlays](../advanced/regional-and-tenant-policy-overlays.md) — model how multiple policy authorities may narrow, override, or otherwise influence the final decision through an explicit overlay contract.
- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) — continue from `AcknowledgmentRequired` into a governed acknowledgment lifecycle and durable evidence.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — keep approval and risk posture separate from the authority used at the execution boundary.
- [Safe Degraded Mode and Fail-Safe Governance lab](../labs/safe-degraded-mode-and-fail-safe-governance.md) — decide deliberately how unavailable dependencies affect governed execution.
- [Threat Modeling as Architecture Reasoning](../security/threat-modeling-as-architecture-reasoning.md) — examine source-of-authority, bypass, tampering, stale-input, and dependency-failure threats around risk signals.

---

> **Risk may influence the decision. Risk scoring should not silently become the authority that makes the operation real.**
