# Adaptive Risk Context, Freshness, and Drift Sample

This sample is the executable companion to [Adaptive Risk Context, Freshness, and Drift](../../docs/advanced/adaptive-risk-context-freshness-and-drift.md).

It demonstrates one boundary:

> **A changing risk signal triggers explicit freshness and reevaluation rules. It does not silently mutate authorization or execution authority.**

## What the Sample Models

The fictional operation is `payment.release`.

```text
Deterministic payment context
        +
Captured risk observation
        |
        v
Versioned risk governance policy
        |
        v
Explicit governance decision
        |
        v
Narrow authority when allowed
        |
        v
Current-context + freshness validation
        |
        v
Atomic single-use claim
        |
        v
Validated payment command
        |
        v
Final authority-command validation
        |
        v
Dry-run host-owned executor
```

A `RiskObservation` preserves observation identity, probability, provider/model/scoring/calibration identity, model-health state, observation time, and provider validity metadata.

The host policy separately preserves governance policy version, threshold version, freshness-rule version, approved signal/provider/model values, maximum signal age, stale-signal disposition, and threshold values.

The provider's `ProviderValidUntilUtc` is not local authorization. Effective freshness is the earlier of provider validity and the host policy's maximum acceptable age.

Decision identity is also explicit. `RiskPolicyEvaluator` requires the caller to provide a `DecisionId`; it does not derive one from payment/policy/observation fields that can legitimately repeat across separate evaluation events.

The stateful/external seams are represented by interfaces (`IExecutionAuthorityClaimStore` and `IPaymentExecutor`). The policy and freshness evaluators remain concrete because they are deterministic teaching functions with no external state. `GetClaimCount` is exposed on the teaching claim-store interface so diagnostics and tests use the same abstraction as the gateway rather than binding to the in-memory implementation.

`AuthorityIssueResult` deliberately carries one host-internal `ReasonCode`. Issuance does not cross an audience boundary in this sample, so it does not need the internal/public reason split used by gateway results that may be projected outward. If authority issuance were exposed across a lower-trust API or UI boundary, the host should add an audience-safe public projection rather than returning internal reason codes directly.

## Run It

```bash
dotnet run --project samples/adaptive-risk-context/AdaptiveRiskContext/AdaptiveRiskContext.csproj
```

```bash
dotnet test samples/adaptive-risk-context/AdaptiveRiskContext.Tests/AdaptiveRiskContext.Tests.csproj
```

Or validate all samples:

```bash
dotnet build samples/Samples.slnx
dotnet test samples/Samples.slnx
```

## Running Scenario

Initial state:

```text
Payment = pay-981:v1
IncidentPosture = Normal
FraudProbability = 0.21
ModelVersion = risk-v7
        |
        v
Allowed
        |
        v
Narrow authority
```

Before execution:

```text
Payment = pay-981:v2
IncidentPosture = Elevated
FraudProbability = 0.76
ModelVersion = risk-v8
```

The old authority cannot execute under the changed state. Current context is reevaluated and the teaching policy returns `EscalationRecommended`.

The original `0.21 / risk-v7` observation remains attached to the original decision. The new `0.76 / risk-v8` output is a new observation rather than a rewrite of history.

The console also walks the successful execution boundary end to end:

```text
Allowed decision
        |
        v
Bounded authority issuance
        |
        v
Current freshness validation
        |
        v
Single-use claim
        |
        v
Validated command
        |
        v
Dry-run executor
        |
        v
Replay rejected as already claimed
```

This makes `dotnet run` demonstrate the same claim/executor boundary described in the article and README rather than stopping at policy evaluation.

## First-Match Freshness Ordering

`ExecutionFreshnessEvaluator` intentionally uses a first-match order:

```text
1. Hard authority time / audience / operation / payment identity
2. Policy and deterministic-context drift
3. Current evidence availability and time validity
4. Same-observation integrity
5. Current policy acceptance of signal / provider / model
6. Approved risk provenance drift
7. Staleness
8. Proceed
```

This ordering is part of the sample contract. A substituted payment is rejected even if policy also changed. An unapproved provider is reported as unapproved evidence rather than ordinary drift. Approved provenance drift is surfaced before a stale-signal disposition.

Because normal issuance caps authority expiration at the effective risk-freshness boundary, the two stale-disposition tests deliberately extend authority expiry to isolate that evaluator branch. The production-shaped path would usually hit the stricter authority expiration first.

## Bounded Use and Final Executor Validation

The gateway claims a validated authority atomically before calling the executor. The in-memory claim store therefore demonstrates both sequential replay rejection and an actually concurrent two-caller race.

The final executor receives both the `ExecutionAuthority` and a host-created `ValidatedPaymentCommand`. It revalidates time, audience, operation, payment/resource bindings, amount/environment bindings, decision identity, policy identity, and risk-observation identity before recording the dry-run execution.

If executor validation fails after the claim was taken, the claim stays consumed. The sample does not silently restore reusable authority after an ambiguous or failed post-claim attempt.

## Reason-Code Map

| Reason code | Meaning |
| --- | --- |
| `payment.destination-not-approved` | Deterministic destination rule denied the operation |
| `risk.observation-not-yet-valid` | Observation timestamp is later than the host evaluation time |
| `risk.signal-unapproved` | Current policy does not accept the signal name |
| `risk.provider-unavailable` | Required risk evidence could not be obtained |
| `risk.observation-missing` | Available-state envelope lacks its observation |
| `risk.provider-unapproved` | Current policy does not accept the provider |
| `risk.model-unapproved` | Current policy does not accept the model identity/version |
| `risk.signal-stale` | Observation is outside effective freshness |
| `risk.model-health-degraded` | Explicit model-health input routes to review |
| `risk.probability-denied` | Probability crossed current denial threshold |
| `risk.probability-escalated` | Probability crossed current escalation threshold |
| `risk.incident-posture-escalated` | Environmental posture requires review |
| `risk.acceptable` | Captured inputs map to `Allowed` |
| `risk.policy-version-drift` | Policy identity/version changed before execution |
| `risk.threshold-policy-drift` | Threshold version changed |
| `risk.freshness-policy-drift` | Freshness-rule version changed |
| `risk.signal-drift` | Approved signal identity differs from the authority binding |
| `risk.provider-drift` | Approved provider identity differs from the authority binding |
| `risk.model-drift` | Approved model identity/version differs from the authority binding |
| `risk.scoring-method-drift` | Scoring-method version changed |
| `risk.calibration-drift` | Calibration version changed |
| `risk.model-health-drift` | Model-health state changed |
| `risk.observation-drift` | A new observation replaced the bound observation |
| `risk.observation-integrity-mismatch` | Same observation ID now carries changed captured facts |
| `context.resource-drift` | Resource version changed |
| `context.amount-drift` | Payment amount changed |
| `context.destination-drift` | Destination approval state changed |
| `context.incident-posture-drift` | Incident posture changed |
| `context.environment-drift` | Environment version changed |
| `authority.decision-not-allowed` | A non-allowed decision cannot mint authority |
| `authority.risk-evidence-missing` | Authority issuance lacks the exact available risk evidence |
| `authority.policy-mismatch` | Decision and issuance policy identity do not match |
| `authority.risk-evidence-stale` | Risk evidence is already stale at issuance time |
| `authority.issued` | Narrow authority was issued successfully |
| `authority.not-yet-valid` | Authority was presented before its issued-at time |
| `authority.expired` | Scoped authority expired |
| `authority.audience-mismatch` | Authority was presented to the wrong execution audience |
| `authority.operation-mismatch` | Authority names another operation |
| `authority.resource-mismatch` | Authority targets another payment |
| `authority.already-claimed` | Single-use authority has already been consumed/claimed |
| `authority.binding-mismatch` | Final executor command does not match the authority bindings |
| `freshness.current` | Modeled freshness bindings remain current |
| `execution.completed` | Dry-run executor was invoked after final validation |

## Focused Invariants

Tests verify that:

- low risk remains an input to policy rather than direct authorization;
- deterministic denial cannot be weakened by a low score;
- unavailable, missing, future-dated, stale, and unapproved evidence states remain explicit;
- host maximum age can expire provider-still-valid evidence;
- degraded model health routes to review;
- high/elevated risk and incident posture can produce different deterministic outcomes;
- threshold changes can alter outcome without changing the observation while each evaluation keeps a distinct decision identity;
- model changes create new observations while old evidence remains unchanged;
- non-allowed or stale decisions do not mint authority;
- authority lifetime is bounded by risk freshness;
- `freshness.current` is explicit for unchanged bindings;
- not-yet-valid or expired authority never reaches the executor;
- authority audience, operation, and payment identity are hard execution-boundary bindings;
- payment substitution outranks softer policy drift;
- policy, threshold, freshness-rule, resource, amount, destination, incident, and environment drift require reevaluation;
- provider unavailability, missing current observation, future-dated observation, and current unapproved signal/provider/model defer rather than failing open;
- approved signal/provider/model/scoring/calibration/model-health/observation drift is distinct from unapproved evidence;
- provenance drift is surfaced before stale-signal disposition in the sample's first-match order;
- same observation identity cannot silently mutate captured facts;
- sequential replay and actually concurrent replay result in one execution at most;
- the executor rejects command/resource substitution at the final host boundary;
- an executor rejection after claim does not make the authority reusable;
- the full drift scenario blocks old authority, preserves history, and produces a new current decision.

## What the Sample Does Not Model

This is not a fraud engine or production payment system. It intentionally omits:

- real model inference;
- model/data drift detection;
- model training or retraining;
- feature stores and model registries;
- production calibration measurement;
- current governance-policy provider outage;
- durable or multi-instance authority-claim storage;
- distributed replay coordination across hosts or regions;
- networked risk-provider transport;
- executor re-fetch of external payment state after the gateway's current-context validation;
- real payment side effects;
- human-review workflow implementation;
- fairness or compliance evaluation.

`ModelHealth` is supplied as an explicit teaching input. The governance evaluator does not diagnose model drift itself.

The in-process claim store demonstrates bounded use but is not a production replay store. The dry-run executor validates the authority/command boundary; a raw risk observation is never an execution credential.

> **Read it. Run it. Question it. Improve it.**
