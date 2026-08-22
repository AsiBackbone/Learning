# Minimal Policy Simulation Harness Sample

This sample is an executable companion for the governance and policy architecture material in ASI Backbone Learning.

**Learning objective:** Observe how the same proposed intent can produce different structured governance decisions when authoritative policy context or the selected policy version changes, without invoking a protected executor or producing a real-world side effect.

**Difficulty:** Intermediate

**Pattern classification:** General learning material

Useful prerequisites:

- [Policy Context and Explicit Decision Outcomes](../../docs/tutorials/policy-context-and-explicit-decision-outcomes.md)
- [Constraint Composition and Policy Precedence](../../docs/governance/constraint-composition-and-policy-precedence.md)
- [Policy Versioning and Decision Provenance](../../docs/governance/policy-versioning-and-decision-provenance.md)
- [Practical Policy Testing and Decision-Table Strategies](../../docs/governance/practical-policy-testing-and-decision-table-strategies.md)

The central boundary is:

> **Simulation evaluates policy behavior. Simulation does not create execution authority or perform the governed side effect.**

## What This Sample Demonstrates

The sample evaluates fictional `customer.export` scenarios containing:

```text
Actor
Resource
Operation
Region
Tenant
Risk
Environment
Policy version
```

Every scenario produces a structured result containing:

```text
Scenario identifier
Decision
Reason code
Policy identifier
Policy version
Matched constraint evidence
ExecutionAttempted = false
```

The decision vocabulary follows the Learning material:

```text
Allowed
Denied
Deferred
AcknowledgmentRequired
EscalationRecommended
```

No model provider, database, network service, external policy engine, or protected executor is required.

## Run the Sample

From the repository root:

```bash
dotnet run --project samples/policy-simulation-harness/PolicySimulationHarness/PolicySimulationHarness.csproj
```

The console report includes comparisons for:

```text
same intent + different region
same intent + different tenant
same intent + different risk
same intent + different policy version
```

It also includes a degraded-environment scenario that produces `Deferred`.

## Run the Tests

Run the focused test project:

```bash
dotnet test samples/policy-simulation-harness/PolicySimulationHarness.Tests/PolicySimulationHarness.Tests.csproj
```

Or run the complete Learning sample suite:

```bash
dotnet test samples/Samples.slnx
```

## The Synthetic Scenario Is the Input Boundary

A simulation scenario is deliberately explicit:

```csharp
public sealed record SimulationScenario(
    string ScenarioId,
    string ActorId,
    string ResourceId,
    string OperationName,
    string Region,
    string TenantId,
    RiskLevel Risk,
    EnvironmentState Environment,
    string PolicyVersion);
```

The sample does not discover hidden ambient state while evaluating policy.

That makes each comparison reviewable:

```text
Same actor
Same resource
Same operation
        +
one changed policy coordinate
        ↓
possibly different decision
```

For example:

```text
Region = US
Risk = Low
Policy = 1.0
        ↓
Allowed
```

versus:

```text
Region = EU
Risk = Low
Policy = 1.0
        ↓
AcknowledgmentRequired
```

The changed outcome is attributable to an explicit context difference rather than an invisible dependency.

## Policy Versions Are Selected Explicitly

The sample contains two deterministic fictional policy revisions:

```text
customer-export@1.0
customer-export@2.0
```

Both versions share the same logical policy identifier:

```text
PolicyId = customer-export
```

Version `2.0` introduces an additional rule:

```text
Medium risk
    ↓
AcknowledgmentRequired
```

Version `1.0` does not contain that rule.

The same medium-risk scenario therefore demonstrates:

```text
customer-export@1.0
    ↓
Allowed

customer-export@2.0
    ↓
AcknowledgmentRequired
```

This is a teaching example of policy comparison, not a claim that production policy versions should be represented by these exact types or version strings.

An unavailable policy version produces a deterministic `Deferred` result with:

```text
policy.version-unavailable
```

rather than silently selecting a different policy.

## Constraint Contributions Remain Visible

The evaluator records the constraint contributions that affected the result.

Examples include:

```text
risk.high
environment.degraded
tenant.regulated
region.eu-export
risk.medium-v2
baseline.allow
```

A result may contain more than one contribution.

For example:

```text
Tenant = tenant-regulated
Region = EU
```

can produce both:

```text
tenant.regulated
    ↓
EscalationRecommended
```

and:

```text
region.eu-export
    ↓
AcknowledgmentRequired
```

The report preserves both observations.

The composer then applies explicit precedence:

```text
Denied
    >
Deferred
    >
EscalationRecommended
    >
AcknowledgmentRequired
    >
Allowed
```

That ordering is a teaching contract for this sample.

It is not a universal governance hierarchy.

A real application must define precedence according to its own policy authority, safety requirements, and domain semantics.

## Region and Tenant Are Policy Coordinates, Not Authority by Themselves

The sample intentionally varies region and tenant because the Learning material discusses regional and tenant overlays.

The sample does **not** imply:

```text
tenant policy always overrides regional policy
```

or:

```text
regional policy always overrides tenant policy
```

Instead, both can contribute a structured constraint result and the explicit composer resolves the final outcome.

This keeps the distinction visible:

```text
Policy scope
    ≠
Policy authority
    ≠
Composition precedence
```

For production-oriented reasoning about those distinctions, see [Regional and Tenant Policy Overlays](../../docs/advanced/regional-and-tenant-policy-overlays.md).

## Risk and Environment Are Deterministic Here

The sample uses a small deterministic vocabulary:

```text
Risk:
Low
Medium
High

Environment:
Normal
Degraded
```

There is no probability score, model confidence, random input, or time-dependent policy behavior.

That is intentional.

The sample isolates policy simulation first.

If probabilistic evidence is introduced later, its provenance and uncertainty should remain explicit rather than being silently converted into an authoritative fact.

See [Deterministic and Probabilistic Inputs in Policy Evaluation](../../docs/governance/deterministic-and-probabilistic-inputs-in-policy-evaluation.md).

## Simulation Is Not Execution

`PolicySimulationHarness` accepts:

```text
Synthetic scenarios
Policy catalog
```

and returns:

```text
Simulation report
```

It does not accept an executor.

It does not mint a capability.

It does not perform acknowledgment.

It does not call an external system.

Every result records:

```text
ExecutionAttempted = false
```

This makes the sample boundary intentionally narrow:

```text
Scenario
   ↓
Policy selection
   ↓
Constraint evaluation
   ↓
Decision composition
   ↓
Structured simulation result
   ↓
Stop
```

There is no transition from the simulation report into a protected operation.

A production system that later uses similar policy results must still enforce its real execution boundary separately.

## Why the Sample Does Not Reuse a Real Executor

A simulator can become misleading if it is implemented as:

```text
Run production workflow
    +
replace external call with a flag
```

That can preserve hidden coupling to credentials, transactions, queues, or side-effect-oriented orchestration.

This sample instead models only the decision path needed for the lesson.

The absence of an executor is intentional architectural evidence.

The focused tests also verify that the harness exposes no executor dependency and that all simulation results remain non-executing.

## Architectural Invariant Tests

The test project verifies that:

1. The same intent can produce a different decision when only the region changes.
2. The same intent can produce a different decision when only the tenant changes.
3. The same intent can produce a different decision when only the risk level changes.
4. The same intent can produce a different decision when only the policy version changes.
5. A degraded environment produces a deterministic deferred result.
6. Policy identity, policy version, reason codes, and matched constraint evidence survive in the structured result.
7. An unavailable policy version is explicit rather than silently substituted.
8. The simulation harness has no executor input and all results report that execution was not attempted.
9. Re-running the same synthetic scenarios produces the same decision evidence.

These tests are not a certification of production policy correctness.

They make the teaching contract executable.

## Example Decision Matrix

The default scenarios are equivalent to a compact decision table:

| Scenario | Region | Tenant | Risk | Environment | Policy | Expected decision |
| --- | --- | --- | --- | --- | --- | --- |
| baseline-us-low-risk | US | tenant-a | Low | Normal | 1.0 | `Allowed` |
| same-intent-eu-region | EU | tenant-a | Low | Normal | 1.0 | `AcknowledgmentRequired` |
| same-intent-regulated-tenant | US | tenant-regulated | Low | Normal | 1.0 | `EscalationRecommended` |
| same-intent-high-risk | US | tenant-a | High | Normal | 1.0 | `Denied` |
| same-intent-degraded-environment | US | tenant-a | Low | Degraded | 1.0 | `Deferred` |
| same-intent-medium-risk-v1 | US | tenant-a | Medium | Normal | 1.0 | `Allowed` |
| same-intent-medium-risk-v2 | US | tenant-a | Medium | Normal | 2.0 | `AcknowledgmentRequired` |

The actor, resource, and operation remain constant across those comparisons.

That makes the changed policy coordinate easy to see.

## What This Sample Intentionally Omits

This sample does not implement:

- Authentication.
- Authorization.
- Real customer data.
- External policy stores.
- Dynamic policy loading.
- Policy signing or fingerprints.
- Policy deployment or rollback.
- Durable audit storage.
- Real regional configuration.
- Real tenant administration.
- Probabilistic risk models.
- Acknowledgment workflows.
- Capability issuance.
- Protected executors.
- External side effects.
- Compliance controls.

Those concerns would make the sample more realistic but would hide the specific lesson.

## Production-Oriented References

The working [AsiBackbone](https://github.com/AsiBackbone/AsiBackbone) repository contains fuller policy evaluation, decision, provenance, and integration concepts.

Use the Learning sample to understand the small simulation boundary first, then compare that boundary with the working implementation.

Useful Learning references include:

- [Constraint Composition and Policy Precedence](../../docs/governance/constraint-composition-and-policy-precedence.md)
- [Policy Versioning and Decision Provenance](../../docs/governance/policy-versioning-and-decision-provenance.md)
- [Practical Policy Testing and Decision-Table Strategies](../../docs/governance/practical-policy-testing-and-decision-table-strategies.md)
- [Regional and Tenant Policy Overlays](../../docs/advanced/regional-and-tenant-policy-overlays.md)
- [Risk-Based Decisions in Governed Systems](../../docs/governance/risk-based-decisions-in-governed-systems.md)
- [Deterministic and Probabilistic Inputs in Policy Evaluation](../../docs/governance/deterministic-and-probabilistic-inputs-in-policy-evaluation.md)

---

> **Read it. Run it. Question it. Improve it.**
