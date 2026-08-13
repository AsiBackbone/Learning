# Policy Context and Explicit Decision Outcomes Sample

This executable companion sample demonstrates the architectural boundary taught in the [Policy Context and Explicit Decision Outcomes](../../docs/tutorials/policy-context-and-explicit-decision-outcomes.md) tutorial.

The sample makes both the decision inputs and the decision result visible:

```text
Host gathers facts
   ↓
Policy context snapshot
   |
   +-- Intent
   +-- Actor
   +-- Resource
   +-- Environment
   +-- Correlation
   +-- Policy identity
   ↓
Policy evaluation
   ↓
Structured decision outcome
   ↓
Host-controlled next step
```

The key invariant is:

> **Policy evaluation consumes an explicit context snapshot and returns a structured outcome without performing the governed side effect.**

## Learning Objective

Observe how actor, resource, operation, environment, correlation, and policy identity can be modeled as explicit decision inputs, and how a policy can return more useful outcomes than a boolean.

## Difficulty

Beginner

## Prerequisites

- .NET 10 SDK
- [Decision Before Execution](../../docs/tutorials/decision-before-execution.md)

## Run the Sample

From the repository root:

```bash
dotnet run --project samples/policy-context-and-explicit-decision-outcomes/PolicyContextAndExplicitDecisionOutcomes/PolicyContextAndExplicitDecisionOutcomes.csproj
```

The sample evaluates seven deterministic scenarios:

| Scenario | Expected outcome | Expected reason code |
| --- | --- | --- |
| Normal request | `Allowed` | none |
| Non-administrator | `Denied` | `account.disable.not-administrator` |
| Cross-tenant account | `Denied` | `account.disable.cross-tenant` |
| Already disabled | `Warning` | `account.disable.already-disabled` |
| Protected account | `EscalationRecommended` | `account.disable.protected-account` |
| Maintenance hold | `Deferred` | `account.disable.maintenance-hold` |
| Missing reason | `AcknowledgmentRequired` | `account.disable.reason-required` |

For each scenario, the program prints the explicit context fields, the governance outcome, the stable reason code, and the next step the host would take.

The program verifies each scenario against its expected outcome and reason code, and fails if the policy returns a different result. This makes the decision matrix executable without introducing a separate test project before the repository-level sample test structure is established.

## What to Observe

The context contains facts rather than live service dependencies or policy conclusions.

The policy receives one explicit snapshot containing:

- Intent
- Actor
- Account resource state
- Environment
- Correlation identifier
- Policy version

The decision contains an explicit outcome and structured reasons.

The host can interpret the outcome without the decision object performing work itself.

No real account operation occurs. This sample intentionally stops at the host-controlled next-step boundary because Tutorial 2 is about making policy inputs and decision states visible.

## Outcome Semantics

The sample treats the outcomes as distinct host instructions:

```text
Allowed
   ↓
May proceed to host-owned execution

Warning
   ↓
May proceed while retaining the warning

Denied
   ↓
Stop

Deferred
   ↓
Pause and re-evaluate later

AcknowledgmentRequired
   ↓
Pause for acknowledgment, then re-enter governance

EscalationRecommended
   ↓
Route to a higher-authority review path
```

The exact transport mapping remains outside the policy. A web API, message consumer, CLI, or AI tool gateway could translate the same governance decision differently while preserving the decision meaning.

## What This Sample Intentionally Omits

This is a teaching artifact, not a production application. It intentionally omits:

- Authentication infrastructure
- HTTP status-code mapping
- Persistent storage
- Distributed or remote policy sources
- External risk scorers
- Durable audit storage
- A real account executor
- Multi-policy composition
- The fuller `AsiBackbone` package abstractions

The omission of a real executor is deliberate. [Decision Before Execution](../decision-before-execution/README.md) demonstrates the execution boundary directly; this sample focuses on the richer middle of the governed flow.

## Try It

After running the sample, change one fact or one rule and observe which part of the decision changes.

Useful experiments include:

1. Change the actor tenant while leaving the account tenant unchanged and confirm that the outcome becomes `Denied`.
2. Mark a scenario as both protected and under maintenance hold, then change rule order and observe why decision precedence must be intentional.
3. Add a `DataClassification` field to the context and introduce a stable reason code for restricted data.
4. Add a second reason to one outcome and preserve machine-readable codes rather than parsing human-readable messages.
5. Change `PolicyVersion` without changing the rules and discuss why policy identity belongs in decision evidence even when it does not itself determine the outcome.

## Related Material

- [Policy Context and Explicit Decision Outcomes tutorial](../../docs/tutorials/policy-context-and-explicit-decision-outcomes.md)
- [Policy Context and Explicit Decision Outcomes learner exercise](../../docs/labs/policy-context-and-explicit-decision-outcomes.md)
- [Decision Before Execution sample](../decision-before-execution/README.md)
- [Acknowledgment and Audit Residue](../../docs/tutorials/acknowledgment-and-audit-residue.md)
- [`GovernanceDecisionOutcome`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecisionOutcome.cs) - compare the teaching outcome vocabulary with the working framework.
- [`GovernanceDecision`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecision.cs) - inspect the fuller decision model, reason metadata, correlation, and policy identity.
- [`IAsiBackboneConstraintEvaluationContext`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Constraints/IAsiBackboneConstraintEvaluationContext.cs) - compare the sample snapshot with the framework's constraint-evaluation context surface.
- [`DefaultAsiBackbonePolicyEvaluator`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Evaluation/DefaultAsiBackbonePolicyEvaluator.cs) - inspect fuller constraint composition and decision evaluation.

## License

Executable sample code under `samples/` is licensed under the MIT License. See [LICENSING.md](../../LICENSING.md).
