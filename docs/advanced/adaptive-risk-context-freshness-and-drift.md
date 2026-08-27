---
description: Learn how changing risk observations, model versions, thresholds, and context trigger explicit reevaluation without turning risk signals into authorization or execution authority.
---

# Adaptive Risk Context, Freshness, and Drift

**Learning objective:** Understand how time-sensitive and probabilistic risk observations remain historical evidence while current policy decides when stale signals, provider/model changes, threshold changes, resource drift, or environmental drift require reevaluation before consequential execution.

**Pattern classification:** General learning material

**Advanced area:** Adaptive risk context, freshness, and drift

**Difficulty:** Advanced

**Required prerequisites:** [Risk-Based Decisions in Governed Systems](../governance/risk-based-decisions-in-governed-systems.md) and [Deterministic and Probabilistic Inputs in Policy Evaluation](../governance/deterministic-and-probabilistic-inputs-in-policy-evaluation.md).

**Recommended background:** [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md), [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md), [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md), and [Safe Degraded Mode and Fail-Safe Governance](../labs/safe-degraded-mode-and-fail-safe-governance.md).

**Glossary:** [Decision provenance](../architecture/glossary.md#decision-provenance), [execution authority](../architecture/glossary.md#execution-authority), [host-owned execution](../architecture/glossary.md#host-owned-execution), and [trust boundary](../architecture/glossary.md#trust-boundary).

> **Framework-neutral scope:** This article teaches freshness, provenance, reevaluation, degraded-mode, and execution-boundary semantics around changing risk inputs. It does not define a fraud model, adaptive-learning algorithm, drift detector, MLOps platform, compliance standard, or production risk service.

## 1. Assumptions and Non-Goals

This treatment assumes:

- A host can distinguish authoritative deterministic facts from probabilistic or model-derived observations.
- Risk observations can carry provider, model, scoring-method, calibration, and observation-time provenance when those identities matter.
- Governance policy owns the interpretation of risk observations, including threshold and degraded-mode rules.
- Consequential delayed execution can validate current host context before invoking the protected executor.
- Historical decision evidence can preserve the exact observation used rather than silently replacing it with a later score.

This treatment does **not** assume:

- A probability is perfectly calibrated.
- A model output is an authoritative fact.
- Newer model versions are automatically safer or more permissive.
- A risk provider owns authorization.
- Every stale observation must produce the same outcome.
- Every model or data drift event must be detected inside the governance evaluator.
- A feedback loop should automatically retrain, deploy, or change policy.
- One universal risk threshold is correct across operations or consequence levels.

The central lesson is:

> **A changing risk signal should trigger explicit reevaluation rules, not silently mutate authorization or execution authority.**

---

## 2. At a Glance

A useful lifecycle is:

```text
Authoritative host facts
        +
Captured risk observation
        |
        v
Versioned governance policy
        |
        v
Governance decision
        |
        v
Narrow execution authority when allowed
        |
        v
Current-context + freshness validation
        |
        +-- current ----------> host-owned execution
        |
        +-- drifted ----------> reevaluate
        |
        +-- unavailable ------> explicit degraded posture
```

Keep these layers distinct:

```text
Risk observation
!= governance decision
!= execution authority
!= protected side effect
```

A risk observation can be necessary evidence without becoming permission.

---

## 3. Keep the Artifacts Distinct

A delayed risk-aware workflow commonly contains at least five different artifacts:

| Artifact | Meaning | What it does not mean |
| --- | --- | --- |
| Risk observation | What a named provider/model reported at a known time | Authorization |
| Deterministic context | Host-owned facts such as resource version, amount, destination, or incident posture | Risk score |
| Governance policy | How rules interpret deterministic and probabilistic inputs | Model output |
| Governance decision | Explicit policy outcome for one captured context | Permanent execution permission |
| Execution authority | Narrow host-issued authority for a specific execution boundary | A reusable risk assessment |

A risk service can legitimately report:

```text
FraudProbability = 0.21
```

without being allowed to say:

```text
Therefore release the payment.
```

The host's versioned governance policy owns that interpretation.

---

## 4. Running Scenario: `payment.release`

At 10:00, a fictional payment-release workflow captures:

```text
PaymentId = pay-981
ResourceVersion = pay-981:v1
Amount = 250000 USD
DestinationApproved = true
IncidentPosture = Normal
EnvironmentVersion = env-normal-v1

FraudProbability = 0.21
Provider = fraud-service
Model = fraud-detector
ModelVersion = risk-v7
ScoringMethodVersion = fraud-score-v3
CalibrationVersion = fraud-cal-2026-08
ObservedAt = 10:00
ProviderValidUntil = 10:10
```

Policy returns:

```text
Decision = Allowed
PolicyVersion = payment-policy-v12
ThresholdVersion = threshold-v12
FreshnessRuleVersion = freshness-v1
```

Before execution, current state becomes:

```text
Payment resource = pay-981:v2
Incident posture = Elevated
EnvironmentVersion = env-elevated-v2

New observation:
FraudProbability = 0.76
ModelVersion = risk-v8
ObservedAt = 10:04
```

The question is not which number should overwrite `0.21`. The questions are:

- Which facts remain historical evidence?
- Which current facts changed?
- Which changes invalidate the old authority?
- What does current policy require before execution?

The original `0.21 / risk-v7` observation remains part of the original decision provenance. The `0.76 / risk-v8` output is a **new observation**.

---

## 5. A Risk Observation Needs Identity, Not Only a Number

A useful teaching envelope is:

```csharp
public sealed record RiskObservation(
    string ObservationId,
    string SignalName,
    decimal FraudProbability,
    string ProviderId,
    string ModelId,
    string ModelVersion,
    string ScoringMethodVersion,
    string CalibrationVersion,
    ModelHealth ModelHealth,
    DateTimeOffset ObservedAtUtc,
    DateTimeOffset ProviderValidUntilUtc);
```

The identities answer different questions:

| Field | Question |
| --- | --- |
| `ObservationId` | Which exact captured output did policy consume? |
| `SignalName` | Which named risk signal does the value represent? |
| `ProviderId` | Which source asserted the observation? |
| `ModelId` / `ModelVersion` | Which model family/revision produced it? |
| `ScoringMethodVersion` | Which scoring/preprocessing contract applied? |
| `CalibrationVersion` | Which calibration treatment applied? |
| `ModelHealth` | What explicit health state accompanied the observation? |
| `ObservedAtUtc` | When was the signal observed? |
| `ProviderValidUntilUtc` | Until when did the provider claim applicability? |

Provenance does not prove the model was correct. It makes later reasoning possible.

---

## 6. New Model Output Is New Historical Evidence

Suppose a decision used:

```text
ObservationId = risk-observation-1001
FraudProbability = 0.21
ModelVersion = risk-v7
```

A later run produces:

```text
ObservationId = risk-observation-2001
FraudProbability = 0.76
ModelVersion = risk-v8
```

Do not rewrite the old decision record.

```text
Historical decision
        |
        +-- observation 1001 / risk-v7 / 0.21

New evaluation
        |
        +-- observation 2001 / risk-v8 / 0.76
```

Re-running the model later is not equivalent to reconstructing the original input. Model version, calibration, upstream data, preprocessing, time-sensitive features, or provider behavior may have changed.

### Required invariant

```text
Model version changes
        |
        v
Prior observation remains historical evidence
        +
New output becomes a new observation
```

Decision identity is separate from risk-observation identity. Two evaluations can consume the same observation and still be different governance events because decision time, threshold policy, freshness policy, or deterministic context changed. The companion sample therefore requires the host to supply a distinct `DecisionId` for each decision event rather than deriving identity only from payment, policy, and observation fields.

```text
Same payment + same observation
+ different evaluation event
        |
        v
Distinct DecisionId
Preserved historical outcomes
```

---

## 7. Provider Validity and Host Freshness Policy Are Different

A provider may report:

```text
ProviderValidUntil = 10:30
```

The host may still require:

```text
MaximumSignalAge = 5 minutes
```

Then the effective acceptance window is the earlier of:

```text
provider-valid-until
host-observed-at + maximum-policy-age
```

For example:

```text
ObservedAt = 10:00
ProviderValidUntil = 10:30
Host MaximumSignalAge = 5 minutes

Effective validity = 10:05
```

The provider supplies evidence and validity metadata. The governance policy decides whether that evidence is fresh enough for this operation.

---

## 8. Staleness Is a Policy State, Not an Automatic Denial

When an observation is stale, legitimate current policies can choose different responses:

```text
Re-score and reevaluate
Defer until current evidence is available
Escalate to human review
Deny for selected critical operations
Continue under deterministic rules for a low-consequence operation
```

There is no universal stale-signal outcome.

The companion sample models two explicit execution-freshness dispositions:

```text
StaleSignalDisposition = Reevaluate
```

or:

```text
StaleSignalDisposition = Defer
```

The important requirement is that the disposition is named policy, not a cache default.

The companion issuer normally makes authority expire no later than the same effective risk-freshness boundary. The focused stale-execution tests deliberately extend authority expiry to isolate the evaluator's stale-signal disposition. That branch is therefore defense in depth; normal issuance often rejects the authority at its tighter expiration boundary first.

### Required invariant

```text
Risk signal becomes stale
        |
        v
Current policy decides the required next step
```

---

## 9. Provider Unavailable Is Not Low Risk

Unsafe collapse:

```text
fraud provider unavailable
        |
        v
FraudProbability = 0.00
        |
        v
Allowed
```

Safer representation:

```text
RiskSignalAvailability = Unavailable
        |
        v
Current degraded-mode policy
        |
        +-- Defer
        +-- Escalate
        +-- Deny for selected operations
        +-- Continue only where policy explicitly permits it
```

The companion sample chooses `Deferred`. That is a teaching choice, not a universal mandate.

### Required invariant

```text
Risk provider unavailable
        |
        v
No manufactured low-risk observation
No execution authority from missing evidence
```

---

## 10. Model Version and Threshold Version Are Different Drift Dimensions

A model change can alter the observed signal:

```text
Same threshold policy
        +
risk-v7 -> risk-v8
        |
        v
Potentially different observation
```

A threshold-policy change can alter the governance outcome without changing the observation:

```text
Same captured observation = 0.76
        +
threshold-v12 -> threshold-v13
        |
        v
Potentially different outcome
```

For example:

```text
threshold-v12: Escalate at >= 0.80
threshold-v13: Escalate at >= 0.75
```

Preserve both `ModelVersion` and `ThresholdVersion`. They answer different reconstruction questions.

Scoring-method and calibration versions can be separate again. If those identities materially affect what the number means, a change should be visible rather than hidden behind an unchanged model label.

---

## 11. Define Reevaluation Triggers Explicitly

For `payment.release`, the sample treats evaluation order as part of the teaching contract. First-match behavior is intentional:

```text
1. Hard authority time / audience / operation / payment-identity rejects
2. Policy and deterministic-context drift
3. Current evidence availability and time validity
4. Same-observation integrity
5. Current policy acceptance of signal / provider / model
6. Approved risk provenance drift
7. Staleness under the current freshness rule
8. Proceed only when every prior check remains current
```

This ordering prevents a softer result from hiding a harder problem. Payment substitution is rejected even if policy also changed. An unapproved current provider is reported as unapproved evidence, not merely as drift. The teaching policy intentionally approves more than one signal/provider value so "unapproved" and "approved but different from the authority binding" are both reachable states. Within the risk checks, a reused observation identity is treated as immutable evidence: if any captured bound fact changes under the same `ObservationId`, the sample rejects it as an integrity mismatch. Only a different observation identity enters ordinary provenance-drift classification. Provenance drift is surfaced before a stale-signal disposition so a stale replacement model does not look like only an age problem.

| Change | Teaching response |
| --- | --- |
| Execution authority not yet valid or expired | Reject old authority |
| Authority audience or operation mismatched | Reject |
| Payment identity substituted | Reject |
| Policy version changed | Reevaluate |
| Threshold version changed | Reevaluate |
| Freshness-rule version changed | Reevaluate |
| Payment resource version changed | Reevaluate |
| Payment amount changed | Reevaluate |
| Destination approval state changed | Reevaluate |
| Incident posture changed | Reevaluate |
| Environment version changed | Reevaluate |
| Required risk provider unavailable | Defer in the sample |
| Current observation missing | Defer in the sample |
| Observation timestamp is in the future | Defer; future-dated evidence is not current evidence |
| Signal is not approved by current policy | Defer as `risk.signal-unapproved` |
| Provider is not approved by current policy | Defer as `risk.provider-unapproved` |
| Model identity/version is not approved by current policy | Defer as `risk.model-unapproved` |
| Approved signal name changed from authority binding | Reevaluate |
| Approved provider identity changed from authority binding | Reevaluate |
| Model version changed | Reevaluate |
| Scoring-method version changed | Reevaluate |
| Calibration version changed | Reevaluate |
| Model-health state changed | Reevaluate |
| New observation replaced the one bound to authority | Reevaluate |
| Same observation identity now carries changed captured facts | Reject as integrity mismatch |
| Signal became stale | Reevaluate or defer according to current policy |
| Authority was already claimed | Reject replay / duplicate use |
| Final executor sees an authority-command binding mismatch | Reject |

A production system may choose a different precedence. What should not remain implicit is the assumption that an unexpired authority makes every upstream fact current or that every non-current condition has the same remediation.

---

## 12. Resource and Environmental Drift Are Separate From Risk Drift

A fresh risk score can still be bound to stale deterministic context.

```text
Decision time:
PaymentVersion = pay-981:v1

Execution time:
PaymentVersion = pay-981:v2
```

The host must ask both:

```text
Is the risk evidence fresh?
```

and:

```text
Is this still the resource/environment state the decision authorized?
```

Environmental examples include incident posture, regional status, threat posture, session assurance, or other host-defined state.

The teaching sample binds:

```text
ResourceVersion
Amount
DestinationApproved
IncidentPosture
EnvironmentVersion
```

### Required invariant

```text
Material context changes before execution
        |
        v
Old authority does not silently execute
Revalidate or reevaluate as defined
```

---

## 13. Execution Freshness Is a Separate Boundary

A conceptual execution authority can bind:

```text
DecisionId
Operation
PaymentId
Resource version
Amount
Destination approval state
Incident posture
Environment version
Risk observation identity/value/time
Signal/provider/model/scoring/calibration identity
Model-health state
Policy version
Threshold version
Freshness-rule version
Issued-at time
Expiration
Audience
```

The executor should not accept a raw fraud probability as a credential.

```text
Risk observation
        |
        v
Governance decision
        |
        v
Scoped authority
        |
        v
Execution-freshness validation
        |
        v
Atomic single-use claim
        |
        v
Validated local command
        |
        v
Final authority-command validation
        |
        v
Host-owned executor
```

The companion sample bounds authority expiration by the effective risk-evidence freshness window, so a newly minted grant cannot silently outlive the evidence that justified it.

It also models bounded use in process. After freshness validation succeeds, the gateway atomically claims the authority before invoking the executor. A replay of the same authority cannot create a second protected execution, including when two callers race concurrently.

The final executor receives both the authority and a host-created `ValidatedPaymentCommand`. It rechecks time, audience, operation, and command bindings including payment/resource identity, amount, environment, decision identity, policy identity, and observation identity. This does not replace the gateway's current-context refresh; it makes the final host boundary reject command substitution or an authority that became invalid between validation and execution.

If the executor rejects after the single-use claim, the sample keeps the claim consumed. A real system would reconcile the failed/ambiguous attempt explicitly rather than silently restoring reusable authority.

The issuer also treats stale evidence as a runtime issuance result rather than an exception. Programming/configuration mismatches remain explicit reason-coded issuance failures in the teaching API.

---

## 14. The Running Drift Scenario Must Reevaluate

Initial state:

```text
0.21 / risk-v7 / Normal / pay-981:v1
        |
        v
Allowed
        |
        v
Authority A
```

Current state:

```text
0.76 / risk-v8 / Elevated / pay-981:v2
```

Unsafe behavior:

```text
Authority A
+ overwrite stored score
        |
        v
Continue under old authority
```

Safer behavior:

```text
Current context differs
        |
        v
Authority A cannot execute
        |
        v
Capture current observation
        |
        v
Rebuild current policy context
        |
        v
Reevaluate
        |
        v
EscalationRecommended in the teaching policy
```

The new decision does not rewrite the old one. Both remain reconstructable.

---

## 15. Model Drift, Data Drift, and Model Health Are Inputs to Governance

Upstream monitoring may report states such as:

```text
ModelHealth = Degraded
```

or:

```text
OutOfDistribution = true
```

Those can become explicit policy inputs. The governance evaluator does not need to become the drift detector.

Avoid one opaque loop that:

```text
Detects drift
        |
        v
Retrains model
        |
        v
Deploys replacement
        |
        v
Changes thresholds
        |
        v
Changes authorization
```

Model monitoring, model deployment, threshold policy, and execution authorization have different owners and failure modes.

The sample accepts `ModelHealth` as an input and routes degraded health to `EscalationRecommended`; it does not diagnose drift itself.

---

## 16. Feedback Loops Need Governance of Their Own

Decision policy can change the data-generating process.

```text
High risk score
        |
        v
Request denied
        |
        v
No downstream outcome observed
        |
        v
Training data differs from the population originally scored
```

Or review policy can create more labels for one subset of cases than another.

The point is not that any one loop is inherently wrong. The point is that operational decisions should not be treated as unquestioned ground truth for automatic retraining or policy changes.

Separate lifecycles for:

- operational decision evidence;
- outcome/label collection;
- training-data selection;
- model evaluation and approval;
- model deployment;
- threshold-policy deployment.

Do not make the hidden loop:

```text
Decision outcome
        |
        v
Automatically changes model or threshold
        |
        v
Immediately changes future authorization
```

without an explicit reviewable update process.

This article does not prescribe one retraining, fairness, drift-detection, or model-governance algorithm.

---

## 17. Human Review Is a Legitimate Uncertainty Response

For high-consequence operations, uncertainty can itself be policy-relevant:

```text
ModelHealth = Degraded
Calibration version not approved
Signals disagree
Out-of-distribution indication
```

A legitimate policy may return `EscalationRecommended` instead of pretending uncertainty is low risk or a deterministic denial.

Human review does not make the model correct and does not create execution authority by itself. After a delayed review, current context and risk evidence may still need refresh and current policy reevaluation.

---

## 18. Deterministic Policy Can Interpret Probabilistic Observations

A policy can remain deterministic even when one input is probabilistic:

```text
Captured FraudProbability = 0.76
ThresholdVersion = threshold-v13

If DestinationApproved = false
    -> Denied

Else if FraudProbability >= 0.90
    -> Denied

Else if FraudProbability >= 0.75
    -> EscalationRecommended

Else
    -> Allowed
```

This preserves an important distinction:

```text
Signal generation may not be reproducible
        +
Captured signal is preserved
        +
Policy interpretation can be replayed deterministically
```

A low probabilistic score should not weaken an authoritative deterministic denial.

---

## 19. Failure and Drift Matrix

| Situation | Teaching interpretation | Consequential execution |
| --- | --- | --- |
| Fresh approved observation + unchanged bound context | Current | May proceed under valid, unclaimed authority |
| Fresh observation says high risk | New policy input | Reevaluate; outcome may deny/escalate |
| Observation stale | Freshness state | Reevaluate or defer according to current policy |
| Risk provider unavailable | Missing required evidence | `Deferred` in sample; no synthetic low score |
| Observation timestamp is in the future | Invalid current-time relationship | `Deferred` in sample |
| Signal/provider/model rejected by current policy | Unapproved evidence | `Deferred`; do not relabel as ordinary drift |
| Approved signal/provider/model identity changed | New observation provenance | Reevaluate |
| Threshold version changed | Policy drift | Reevaluate |
| Scoring/calibration version changed | Signal-semantics drift | Reevaluate |
| Model health degraded | Explicit uncertainty input | Escalation in sample |
| Resource version, amount, or destination state changed | Deterministic context drift | Reevaluate |
| Incident/environment changed | Environmental drift | Reevaluate |
| Payment identity substituted | Hard authority-binding failure | Reject even when softer drift also exists |
| Authority not yet valid or expired | Authority freshness failure | Reject old authority |
| Audience or operation mismatched | Authority binding failure | Reject |
| Same observation ID carries changed captured facts | Evidence integrity failure | Reject |
| Authority already claimed | Replay / duplicate use | Reject; no second executor call |
| Executor rejects after claim | Post-claim execution failure | Keep claim consumed; reconcile explicitly |
| Executor command differs from authority bindings | Final host-boundary mismatch | Reject |

The matrix distinguishes:

```text
risk says high
```

from:

```text
risk cannot currently be established
```

and from:

```text
current evidence is not approved by policy
```

Those are different governance facts and can require different remediation.

---

## 20. Common Failure Modes

| Failure mode | Why it is dangerous |
| --- | --- |
| Score directly gates executor | Risk service quietly becomes authorization authority |
| Missing provider becomes zero | Dependency failure masquerades as low risk |
| Stale score reused indefinitely | Old evidence silently outlives intended context |
| New score overwrites old decision evidence | Historical reconstruction becomes false |
| Distinct decisions reuse one derived `DecisionId` | Different outcomes become ambiguous in audit/provenance history |
| Model and threshold versions collapse together | Cannot distinguish scoring drift from policy drift |
| Resource substitution checked after soft drift | Reevaluate can hide a harder wrong-resource reject |
| Unapproved provider/signal is reported only as drift | Trust/acceptance failure loses its distinct remediation |
| Resource drift ignored because score is fresh | Evidence may refer to the wrong resource state |
| Provider `ValidUntil` treated as host policy | External source decides local freshness semantics |
| Model health hidden inside numeric score | Uncertainty becomes invisible to policy |
| Authority remains reusable after execution | Replay can duplicate a protected side effect |
| Executor trusts a gateway-created command without binding validation | TOCTOU/substitution can bypass the final host boundary |
| Decision outcome feeds model automatically | Learning and authorization loops become circular |
| Current model rerun explains old decision | New observation is mistaken for historical input |
| Low score overrides deterministic denial | Advisory evidence weakens authoritative fact |

---

## 21. When Simpler Deterministic Policy Is Better

Prefer a direct authoritative rule when the host already knows the relevant fact.

```text
DestinationApproved = false
        |
        v
Denied
```

is better than manufacturing a destination-trust score when the allowlist fact already expresses the requirement.

A low-consequence synchronous operation may not need a risk provider, observation versioning, delayed freshness validation, or adaptive feedback. Use the extra boundary when uncertainty materially improves the decision.

---

## 22. Design Checklist

Before using changing risk context in a governed workflow, answer:

1. What exact observation did policy consume?
2. Who provided it?
3. Which model, scoring-method, and calibration versions produced it?
4. When was it observed?
5. What freshness rule does the **host policy** apply?
6. What happens when the provider is unavailable?
7. Which model versions are accepted?
8. Which threshold policy interpreted the observation?
9. Which deterministic facts take precedence?
10. What resource/environment changes require reevaluation?
11. What is bound into execution authority?
12. Does a new observation create new evidence rather than mutate history?
13. Can history be reconstructed without re-running the model?
14. Is model-health/drift state explicit rather than hidden in a score?
15. Can feedback alter model or policy only through a separate reviewed lifecycle?
16. Would a deterministic rule be clearer?

---

## 23. Companion Sample

[Run the Adaptive Risk Context sample](https://github.com/AsiBackbone/Learning/blob/main/samples/adaptive-risk-context/README.md).

The sample is local and deterministic. It models:

- captured risk observations with provider/model/scoring/calibration provenance;
- caller-owned decision identity for each evaluation event;
- versioned threshold and freshness policy;
- deterministic policy interpretation;
- result-based narrow authority issuance;
- authority lifetime bounded by effective risk freshness;
- execution-time drift validation with explicit precedence;
- in-process atomic single-use authority claims;
- final executor validation of authority-command bindings;
- a dry-run host-owned payment executor.

It does **not** perform fraud inference or drift detection. Its purpose is to make freshness, bounded-use, provenance, and authority boundaries executable. The console demonstration also walks one `Allowed` decision through bounded authority issuance, freshness validation, single-use claim, validated command construction, dry-run execution, and a rejected replay so `dotnet run` reaches the same final boundary described here.

---

## 24. Executable Coverage

| Invariant | Coverage |
| --- | --- |
| Low risk remains an input to governance | ✅ `LowRiskObservationProducesAllowedDecision` |
| Deterministic destination denial cannot be weakened by low risk | ✅ `LowRiskCannotOverrideDestinationDenial` |
| Provider unavailability becomes an explicit deferred decision | ✅ `UnavailableProviderProducesDeferredDecision` |
| Missing observation remains explicit | ✅ `MissingObservationProducesDeferredDecision` |
| Future-dated observation is not treated as current evidence | ✅ `FutureObservationProducesDeferredDecision` |
| Unapproved signal is distinct at decision time | ✅ `UnapprovedSignalNameIsDeferred` |
| Unapproved provider is distinct at decision time | ✅ `UnapprovedProviderIsDeferred` |
| Unapproved model is distinct at decision time | ✅ `UnapprovedModelIsDeferred` |
| Stale observation becomes an explicit decision state | ✅ `StaleObservationProducesDeferredDecision` |
| Host maximum age can expire provider-still-valid evidence | ✅ `HostMaximumAgeCanExpireProviderStillValidObservation` |
| Degraded model health can route to review | ✅ `DegradedModelHealthProducesEscalationRecommended` |
| High risk can deterministically deny under policy | ✅ `HighRiskProducesDeniedDecision` |
| Elevated risk can route to escalation | ✅ `ElevatedRiskProducesEscalationRecommended` |
| Elevated incident posture can change outcome even with a low score | ✅ `ElevatedIncidentPostureCanEscalateLowScore` |
| Same observation plus changed threshold policy can change outcome without reusing decision identity | ✅ `ThresholdChangeCanChangeOutcomeWithoutChangingObservation` |
| Model change preserves prior observation as historical evidence | ✅ `ModelChangePreservesHistoricalObservation` |
| Separate evaluations keep distinct decision identities even when they reuse one observation | ✅ `SeparateEvaluationsKeepDistinctDecisionIdentityWhenOutcomeChanges` |
| Non-allowed decision cannot mint authority | ✅ `NonAllowedDecisionCannotMintAuthority` |
| Allowed-looking decision without the bound risk evidence cannot mint authority | ✅ `MissingRiskEvidenceCannotMintAuthority` |
| Decision/issuance policy mismatch cannot mint authority | ✅ `PolicyMismatchCannotMintAuthority` |
| Authority expiry is capped by effective risk freshness | ✅ `AuthorityExpiryIsBoundedByRiskFreshness` |
| Already-stale risk evidence returns a non-issued authority result | ✅ `StaleRiskEvidenceCannotMintNewAuthority` |
| Unchanged authority produces the explicit `freshness.current` success value | ✅ `FreshnessEvaluatorReturnsCurrentForUnchangedAuthority` |
| Current authority reaches executor only after freshness and claim validation | ✅ `CurrentAuthorityReachesExecutorAfterValidation` |
| Sequential replay cannot execute the same authority twice | ✅ `ReplayedAuthorityDoesNotExecuteTwice` |
| Two actually concurrent claims produce one execution | ✅ `TwoActuallyConcurrentAuthorityClaimsProduceOneExecution` |
| Final executor rejects command/resource substitution | ✅ `ExecutorRejectsCommandResourceSubstitution` |
| Executor rejection after claim leaves single-use authority consumed | ✅ `ExecutorRejectionAfterClaimLeavesAuthorityConsumed` |
| Not-yet-valid authority is rejected before execution | ✅ `NotYetValidAuthorityIsRejectedBeforeExecution` |
| Audience and operation bindings are enforced | ✅ `AuthorityExecutionBoundaryBindingsAreValidated` |
| Expired authority is rejected before execution | ✅ `ExpiredAuthorityIsRejectedBeforeExecution` |
| Policy/threshold/freshness-rule drift requires reevaluation | ✅ `PolicyIdentityDriftRequiresReevaluation` |
| Payment identity substitution is rejected | ✅ `ResourceIdentitySubstitutionIsRejected` |
| Payment substitution outranks softer policy drift | ✅ `ResourceIdentitySubstitutionTakesPrecedenceOverPolicyDrift` |
| Resource/amount/destination/incident/environment drift requires reevaluation | ✅ `MaterialContextDriftRequiresReevaluation` |
| Current provider unavailability defers execution | ✅ `CurrentProviderUnavailableDefersExecution` |
| Current observation missing defers execution | ✅ `CurrentObservationMissingDefersExecution` |
| Current unapproved signal is not mislabeled as ordinary drift | ✅ `CurrentUnapprovedSignalDefersInsteadOfReportingDrift` |
| Current unapproved provider is not mislabeled as ordinary drift | ✅ `CurrentUnapprovedProviderDefersInsteadOfReportingDrift` |
| Current unapproved model is not mislabeled as ordinary drift | ✅ `CurrentUnapprovedModelDefersInsteadOfReportingDrift` |
| Future current observation defers execution | ✅ `FutureCurrentObservationDefersExecution` |
| Stale signal can require reevaluation under the named disposition | ✅ `StaleSignalCanRequireReevaluation` |
| Stale signal can defer under the named disposition | ✅ `StaleSignalCanDefer` |
| Provenance drift outranks stale disposition in the sample's first-match order | ✅ `ProvenanceDriftTakesPrecedenceOverStaleDisposition` |
| Approved signal/provider/model/scoring/calibration/health/observation drift is explicit | ✅ `RiskProvenanceDriftRequiresReevaluation` |
| Same observation identity cannot mutate captured facts | ✅ `MutatedObservationWithSameIdentityIsRejected` |
| Same observation identity plus model mutation is an integrity reject, not ordinary drift | ✅ `SameObservationIdentityWithModelMutationIsRejected` |
| Running scenario blocks old authority, preserves history, and reevaluates current state | ✅ `MaterialDriftBlocksOldAuthorityAndCurrentReevaluationEscalates` |

### Intentionally not modeled

| Area | Status |
| --- | --- |
| Real fraud/model inference | ◐ Not modeled; observations are fictional records |
| Model/data drift detection | ◐ Not modeled; model-health state is supplied as input |
| Model training/retraining | ◐ Not modeled |
| Feature store/model registry | ◐ Not modeled |
| Production calibration measurement | ◐ Not modeled |
| Current governance-policy provider outage | ◐ Not modeled; see degraded-mode material for the broader dependency pattern |
| Durable or multi-instance authority claim storage | ◐ Not modeled; atomic single-use claim is process-local only |
| Distributed replay coordination across hosts/regions | ◐ Not modeled |
| Executor re-fetch of external payment state | ◐ Not modeled; final executor validates authority-command/time bindings while the gateway owns current-context reconstruction |
| Real payment side effects | ◐ Not modeled; executor is dry-run/in-memory |
| Human review workflow | ◐ Not modeled; escalation is only an outcome |
| Fairness/compliance evaluation | ◐ Not modeled |

---

## 25. Related Learning

Continue with:

- [Risk-Based Decisions in Governed Systems](../governance/risk-based-decisions-in-governed-systems.md) for the foundational risk-assessment boundary.
- [Deterministic and Probabilistic Inputs in Policy Evaluation](../governance/deterministic-and-probabilistic-inputs-in-policy-evaluation.md) for typed observations, calibration, thresholds, and model/data drift concepts.
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) for explicit governance outcomes.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) for narrow authority and execution validation.
- [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) for historical policy identity versus execution freshness.
- [Safe Degraded Mode and Fail-Safe Governance](../labs/safe-degraded-mode-and-fail-safe-governance.md) for dependency-failure exercises.

---

## 26. Closing Principle

A changing risk signal is evidence that the world or scoring system may have changed. It is not permission to rewrite the old decision, and it is not authority to execute under the new state.

```text
Risk observation changes
        |
        v
Preserve old evidence
        +
Apply explicit current freshness/reevaluation policy
        +
Mint or accept execution authority only for the resulting current decision
```

> **Changing evidence should cause explicit reasoning, not silent authority mutation.**

> **Read it. Run it. Question it. Improve it.**
