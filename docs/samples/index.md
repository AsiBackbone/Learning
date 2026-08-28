---
description: Browse runnable companion samples that demonstrate ASI Backbone Learning patterns between the problem-first tutorials and hands-on architectural labs.
---

# Executable Samples

Executable samples are the **runnable demonstration layer** of ASI Backbone Learning.

They sit between the problem-first tutorials and the hands-on labs:

```text
Tutorial
   ↓
Executable Sample
   ↓
Hands-On Lab
   ↓
Working Repository
```

The canonical sample source and detailed sample documentation remain under [`samples/`](https://github.com/AsiBackbone/Learning/tree/main/samples) in the GitHub repository. This page keeps the published DocFX learning path intact while giving you enough information to choose and run the right companion sample before following the canonical README for deeper explanation.

## Complete Sample Catalog

All executable sample projects currently under `samples/` are listed below, grouped by the same learning areas used throughout the site.

| Area | Sample | Core boundary |
| --- | --- | --- |
| Foundational | [Decision Before Execution](#decision-before-execution) | A blocked decision never reaches the executor. |
| Foundational | [Policy Context and Explicit Decision Outcomes](#policy-context-and-explicit-decision-outcomes) | Policy consumes explicit facts and returns a structured outcome without performing the side effect. |
| Foundational | [Acknowledgment and Audit Residue](#acknowledgment-and-audit-residue) | Acknowledgment satisfies a governance requirement; it does not become execution authority. |
| Foundational | [Scoped Capability and Host-Owned Execution](#scoped-capability-and-host-owned-execution) | Narrow authority is validated again at the host-owned execution boundary. |
| Foundational | [Governed AI Tool Gateway](#governed-ai-tool-gateway) | The model may propose; the host retains execution authority. |
| Governance and Policy Architecture | [Decision Pipeline Refactoring](#decision-pipeline-refactoring) | Explicit outcomes remain separate from protected execution. |
| Governance and Policy Architecture | [Minimal Policy Simulation Harness](#minimal-policy-simulation-harness) | Simulation evaluates policy behavior without creating execution authority. |
| Governance and Policy Architecture | [Federated Governance and Independent Authority Coordination](#federated-governance-and-independent-authority-coordination) | An outage cannot reclassify a federated operation as local-only. |
| Governance and Policy Architecture | [Distributed Acknowledgment and Continuation Workflows](#distributed-acknowledgment-and-continuation-workflows) | Acknowledgment evidence is not portable execution authority. |
| Governance and Policy Architecture | [Decision Explainability for Human Operators](#decision-explainability-for-human-operators) | Explanation projects governance evidence; it does not replace the evidence or create authority. |
| Governance and Policy Architecture | [Adaptive Risk Context, Freshness, and Drift](#adaptive-risk-context-freshness-and-drift) | Changing risk evidence triggers explicit freshness and reevaluation rules. |
| Security and Trust Architecture | [Replay Protection and Bounded-Use Authority](#replay-protection-and-bounded-use-authority) | Bounded-use authority needs an atomic state transition at the consumption boundary. |
| Security and Trust Architecture | [Cross-System Capability Exchange and Delegated Authority](#cross-system-capability-exchange-and-delegated-authority) | A cross-system artifact is validated into a local executor contract rather than used as one directly. |
| Security and Trust Architecture | [Durable Decision Ledger and Audit Chain](#durable-decision-ledger-and-audit-chain) | Local chain verification and independently retained checkpoints provide different evidence properties. |
| ASP.NET Core Architecture | [Middleware Ordering Changes Behavior](#middleware-ordering-changes-behavior) | Middleware order changes which components can observe and handle a request or failure. |
| ASP.NET Core Architecture | [Centralized Error Handling and Problem Details](#centralized-error-handling-and-problem-details) | Expected governance outcomes remain distinct from unexpected application failures. |

## Foundational Sample Set

The five foundational tutorials each have an executable companion sample.

### Decision Before Execution

**Learning objective:** Observe how an explicit governance decision controls whether a host-owned executor is invoked.

**Difficulty:** Beginner

**Key invariant:**

> **A blocked decision never reaches the executor.**

Run from the repository root:

```bash
dotnet run --project samples/decision-before-execution/DecisionBeforeExecution/DecisionBeforeExecution.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-before-execution/README.md)
- [Read the tutorial](../tutorials/decision-before-execution.md)
- [Continue with the beginner lab](../labs/decision-before-execution.md)

### Policy Context and Explicit Decision Outcomes

**Learning objective:** Observe how actor, resource, operation, environment, correlation, and policy identity become explicit decision inputs, and how policy returns meaningful outcomes instead of collapsing the result into a boolean.

**Difficulty:** Beginner

**Key invariant:**

> **Policy evaluation consumes an explicit context snapshot and returns a structured outcome without performing the governed side effect.**

Run from the repository root:

```bash
dotnet run --project samples/policy-context-and-explicit-decision-outcomes/PolicyContextAndExplicitDecisionOutcomes/PolicyContextAndExplicitDecisionOutcomes.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/policy-context-and-explicit-decision-outcomes/README.md)
- [Read the tutorial](../tutorials/policy-context-and-explicit-decision-outcomes.md)
- [Continue with the learner exercise](../labs/policy-context-and-explicit-decision-outcomes.md)

### Acknowledgment and Audit Residue

**Learning objective:** Observe how a consequential operation can pause for a narrowly bound acknowledgment, validate the response, re-evaluate current policy, and preserve a correlated audit timeline without treating acknowledgment as standing permission.

**Difficulty:** Intermediate

**Key invariant:**

> **Acknowledgment is a governance boundary, not an execution bypass.**

Decision, acknowledgment, re-evaluation, and execution remain distinguishable evidence events throughout the flow.

Run from the repository root:

```bash
dotnet run --project samples/acknowledgment-and-audit-residue/AcknowledgmentAndAuditResidue/AcknowledgmentAndAuditResidue.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/acknowledgment-and-audit-residue/README.md)
- [Read the tutorial](../tutorials/acknowledgment-and-audit-residue.md)
- [Continue with the intermediate lab](../labs/acknowledgment-and-audit-residue.md)

### Scoped Capability and Host-Owned Execution

**Learning objective:** Observe how an allowed decision can produce short-lived, narrowly bound execution authority and how the host validates those bindings again against current execution context immediately before the side effect.

**Difficulty:** Intermediate

**Key invariants:**

> **A blocked decision cannot mint execution authority.**

> **Expired or stale execution authority never reaches the executor.**

Run from the repository root:

```bash
dotnet run --project samples/scoped-capability-and-host-owned-execution/ScopedCapabilityAndHostOwnedExecution/ScopedCapabilityAndHostOwnedExecution.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/scoped-capability-and-host-owned-execution/README.md)
- [Read the tutorial](../tutorials/scoped-capability-and-host-owned-execution.md)
- [Continue with the intermediate lab](../labs/scoped-capability-and-host-owned-execution.md)

### Governed AI Tool Gateway

**Learning objective:** Run an end-to-end AI-assisted governance flow where a simulated model may propose a tool action, but the host owns the tool allowlist, authoritative context, policy decision, acknowledgment, scoped capability, dry-run execution, and audit evidence.

**Difficulty:** Advanced

**Key invariant:**

> **The model may propose. The host retains execution authority.**

Run from the repository root:

```bash
dotnet run --project samples/governed-ai-tool-gateway/GovernedAiToolGateway/GovernedAiToolGateway.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/governed-ai-tool-gateway/README.md)
- [Read the tutorial](../tutorials/governed-ai-tool-gateway.md)
- [Trace the governed proposal end to end](../ai-integration/ai-governance-observability-and-end-to-end-decision-tracing.md)
- [Continue with the advanced lab](../labs/governed-ai-tool-gateway.md)

The same executable includes a deterministic local observability demonstration using `ActivitySource`, trace/span relationships, structured activity events, distinct proposal/correlation identity, policy-version evidence, and the existing audit residue. It prints allowed, denied, and acknowledgment-required traces without requiring a real AI provider or telemetry backend.

## Governance and Policy Architecture Samples

### Decision Pipeline Refactoring

**Learning objective:** Diagnose governance logic scattered around protected side effects, then observe a reference refactor that makes authoritative context, explicit outcomes, continuation requirements, protected execution, and evidence distinct.

**Difficulty:** Intermediate

**Key invariant:**

> **`Denied`, `Deferred`, `AcknowledgmentRequired`, and `EscalationRecommended` produce zero executor calls; `Allowed` produces exactly one.**

Run from the repository root:

```bash
dotnet run --project samples/decision-pipeline-refactoring/DecisionPipelineRefactoring/DecisionPipelineRefactoring.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-pipeline-refactoring/README.md)
- [Read Decision Before Execution](../tutorials/decision-before-execution.md)
- [Continue with the refactoring lab](../labs/refactor-scattered-governance-checks.md)
- [Compare When a Simple Application Service Is Enough](../architecture/when-a-simple-application-service-is-enough.md)

### Minimal Policy Simulation Harness

**Learning objective:** Compare deterministic governance outcomes for the same fictional proposed intent while region, tenant, risk, environment, or policy version changes, without invoking a protected executor.

**Difficulty:** Intermediate

**Key invariant:**

> **Simulation evaluates policy behavior; it does not create execution authority or perform the governed side effect.**

The sample emits structured scenario results with decision outcome, reason code, policy identity/version, matched constraint evidence, and an explicit non-execution marker.

Run from the repository root:

```bash
dotnet run --project samples/policy-simulation-harness/PolicySimulationHarness/PolicySimulationHarness.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/policy-simulation-harness/README.md)
- [Read Practical Policy Testing and Decision-Table Strategies](../governance/practical-policy-testing-and-decision-table-strategies.md)
- [Read Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md)
- [Compare Regional and Tenant Policy Overlays](../advanced/regional-and-tenant-policy-overlays.md)

### Federated Governance and Independent Authority Coordination

**Learning objective:** Observe how independently operated governance authorities contribute to one deterministic decision while authority-set resolution, contribution health, disagreement handling, coordinator failure, and authority-set drift remain explicit.

**Key invariant:**

> **An outage cannot reclassify a federated operation as local-only.**

Run from the repository root:

```bash
dotnet run --project samples/federated-governance-coordination/FederatedGovernanceCoordination/FederatedGovernanceCoordination.csproj
```

Run the focused tests:

```bash
dotnet test samples/federated-governance-coordination/FederatedGovernanceCoordination.Tests/FederatedGovernanceCoordination.Tests.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/federated-governance-coordination/README.md)
- [Read Federated Governance and Independent Authority Coordination](../advanced/federated-governance-and-independent-authority-coordination.md)

### Distributed Acknowledgment and Continuation Workflows

**Learning objective:** Observe acknowledgment evidence crossing system boundaries while recipient-owned trust validation, durable continuation binding, current-context reconstruction, current policy re-evaluation, a single-use continuation claim, and local execution authority remain separate.

**Key invariant:**

> **Acknowledgment evidence is not portable execution authority.**

Run from the repository root:

```bash
dotnet run --project samples/distributed-acknowledgment-continuation/DistributedAcknowledgmentContinuation/DistributedAcknowledgmentContinuation.csproj
```

Run the focused tests:

```bash
dotnet test samples/distributed-acknowledgment-continuation/DistributedAcknowledgmentContinuation.Tests/DistributedAcknowledgmentContinuation.Tests.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/distributed-acknowledgment-continuation/README.md)
- [Read Distributed Acknowledgment and Continuation Workflows](../advanced/distributed-acknowledgment-and-continuation-workflows.md)

### Decision Explainability for Human Operators

**Learning objective:** Observe how structured governance evidence can be projected into deterministic, audience-aware human explanations without rewriting the source decision, disclosing protected context, or creating new policy or execution authority.

**Key invariant:**

> **Human explanation is a projection of structured governance evidence. It is not the evidence itself and it does not create policy or execution authority.**

Run from the repository root:

```bash
dotnet run --project samples/decision-explainability/DecisionExplainability/DecisionExplainability.csproj
```

Run the focused tests:

```bash
dotnet test samples/decision-explainability/DecisionExplainability.Tests/DecisionExplainability.Tests.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-explainability/README.md)
- [Read Decision Explainability for Human Operators](../advanced/decision-explainability-for-human-operators.md)

### Adaptive Risk Context, Freshness, and Drift

**Learning objective:** Observe how a changing risk observation stays bound to its original decision while freshness rules, policy drift, current-context reconstruction, re-evaluation, bounded authority, and final host-owned execution remain explicit.

**Key invariant:**

> **A changing risk signal triggers explicit freshness and reevaluation rules. It does not silently mutate authorization or execution authority.**

Run from the repository root:

```bash
dotnet run --project samples/adaptive-risk-context/AdaptiveRiskContext/AdaptiveRiskContext.csproj
```

Run the focused tests:

```bash
dotnet test samples/adaptive-risk-context/AdaptiveRiskContext.Tests/AdaptiveRiskContext.Tests.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/adaptive-risk-context/README.md)
- [Read Adaptive Risk Context, Freshness, and Drift](../advanced/adaptive-risk-context-freshness-and-drift.md)

## Security and Trust Architecture Samples

### Replay Protection and Bounded-Use Authority

**Learning objective:** Observe why bounded-use authority requires state, reproduce a check-then-act race, and prove that an atomic consume boundary permits exactly one consumer to claim the final use inside the teaching process.

**Difficulty:** Intermediate

**Key invariant:**

> **With `MaximumUses = 1`, two concurrent consumers produce one accepted consumption, one rejected replay, and one protected execution.**

The sample also demonstrates that successful capability consumption does not establish exactly-once completion of an external side effect.

Run from the repository root:

```bash
dotnet run --project samples/replay-protection-and-bounded-use/ReplayProtectionAndBoundedUse/ReplayProtectionAndBoundedUse.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/replay-protection-and-bounded-use/README.md)
- [Read Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md)
- [Continue with the intermediate concurrency lab](../labs/replay-protection-and-bounded-use.md)

### Cross-System Capability Exchange and Delegated Authority

**Learning objective:** Observe how a recipient independently validates issuer/key trust, audience and presenter bindings, operation/resource/request bindings, lifetime, delegation policy, revocation, local policy, and bounded-use state before creating a local command for a host-owned executor.

**Key invariant:**

> **The raw cross-system artifact never becomes the executor contract.**

Run from the repository root:

```bash
dotnet run --project samples/cross-system-capability-exchange/CrossSystemCapabilityExchange/CrossSystemCapabilityExchange.csproj
```

Run the focused tests:

```bash
dotnet test samples/cross-system-capability-exchange/CrossSystemCapabilityExchange.Tests/CrossSystemCapabilityExchange.Tests.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/cross-system-capability-exchange/README.md)
- [Read Cross-System Capability Exchange and Delegated Authority](../advanced/cross-system-capability-exchange-and-delegated-authority.md)

### Durable Decision Ledger and Audit Chain

**Learning objective:** Observe how deterministic canonicalization, idempotent append semantics, predecessor linkage, streaming verification, and an independently retained checkpoint teaching model contribute different evidence properties without turning an in-memory chain into an immutable ledger.

**Difficulty:** Advanced

**Key invariants:**

> **A verified local prefix does not prove the newest records are present.**

> **An independently retained checkpoint can expose a missing checkpointed tail, while a single verifier still cannot detect every split view without cross-verifier comparison.**

Run from the repository root:

```bash
dotnet run --project samples/durable-decision-ledger-audit-chain/DurableDecisionLedgerAuditChain/DurableDecisionLedgerAuditChain.csproj
```

Run the focused tests:

```bash
dotnet test samples/durable-decision-ledger-audit-chain/DurableDecisionLedgerAuditChain.Tests/DurableDecisionLedgerAuditChain.Tests.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/durable-decision-ledger-audit-chain/README.md)
- [Read Durable Decision Ledgers and Cryptographic Audit Chains](../advanced/durable-decision-ledgers-and-cryptographic-audit-chains.md)
- [Compare Signing, Verification, Key Custody, and Tamper Evidence](../security/signing-verification-key-custody-and-tamper-evidence.md)

## ASP.NET Core Architecture Samples

### Middleware Ordering Changes Behavior

**Learning objective:** Observe request/response traversal order and prove that an exception boundary can only handle failures produced by middleware or endpoints that execute downstream from it.

**Difficulty:** Intermediate

**Key invariants:**

> **Requests enter middleware in registration order; responses unwind in reverse order.**

> **An exception boundary cannot catch a failure that occurs before the boundary is entered.**

Run the corrected pipeline from the repository root:

```bash
dotnet run --project samples/middleware-ordering-changes-behavior/MiddlewareOrderingChangesBehavior/MiddlewareOrderingChangesBehavior.csproj -- --PipelineMode=correct --urls http://127.0.0.1:5080
```

Restart with `--PipelineMode=incorrect` to move the fault-producing middleware outside the sample exception boundary and compare the observable behavior.

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/middleware-ordering-changes-behavior/README.md)
- [Read Middleware Ordering Changes Behavior](../aspnetcore/middleware-ordering-changes-behavior.md)
- [Inspect the fuller NetCoreApplicationTemplate pipeline](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/PipelineExtensions.cs)

### Centralized Error Handling and Problem Details

**Learning objective:** Observe how a small ASP.NET Core host keeps unexpected application failures inside a centralized exception boundary while expected governance outcomes are mapped explicitly by the host, with safe Problem Details and trace correlation across the public and operational surfaces.

**Key invariant:**

> **A denied decision is not an exception merely because execution does not proceed.**

Run from the repository root:

```bash
dotnet run --project samples/centralized-error-handling-and-problem-details/CentralizedErrorHandlingAndProblemDetails/CentralizedErrorHandlingAndProblemDetails.csproj --urls http://127.0.0.1:5082
```

Run the focused integration tests:

```bash
dotnet test samples/centralized-error-handling-and-problem-details/CentralizedErrorHandlingAndProblemDetails.Tests/CentralizedErrorHandlingAndProblemDetails.Tests.csproj
```

- [Open the canonical sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/centralized-error-handling-and-problem-details/README.md)
- [Read Centralized Error Handling and Problem Details](../aspnetcore/centralized-error-handling-and-problem-details.md)

## Run the Complete Sample Suite

The shared sample solution is [`samples/Samples.slnx`](https://github.com/AsiBackbone/Learning/blob/main/samples/Samples.slnx).

From the repository root:

```bash
dotnet restore samples/Samples.slnx
dotnet build samples/Samples.slnx --no-restore
dotnet test samples/Samples.slnx --no-build
```

The focused xUnit projects make architectural invariants executable rather than leaving them only as prose claims.

## Sample Design Principles

The canonical [`samples/README.md`](https://github.com/AsiBackbone/Learning/blob/main/samples/README.md) contains the full sample guidance. The recurring principles are:

- **Keep samples small.** Optimize for learning value rather than production completeness.
- **Keep side effects visible.** Evaluation, decision, and execution should remain distinguishable.
- **Test architectural invariants.** Tests should prove meaningful boundaries such as blocked execution, stale capability rejection, and unknown-tool rejection.
- **Prefer deterministic local behavior.** Use in-memory state, fakes, simulation, and dry-run execution where practical.
- **Use fictional data.** Do not place real credentials, secrets, tokens, personal information, or production connection strings in teaching samples.
- **Keep secrets host-owned.** A model or proposal generator should not receive infrastructure credentials merely because it can propose an action.
- **Do not hide policy in prompt text.** Prompt guidance may influence a proposal, but host-side controls remain the authoritative execution boundary.
- **Prefer narrow semantic operations.** Small operations are easier to validate, govern, test, and audit than broad arbitrary-execution primitives.

The samples intentionally omit production infrastructure that is not required to make the lesson visible. Follow each canonical README's **What This Sample Intentionally Omits** section before adapting a sample to a real system.

## Working Implementations

After a sample or lab, compare the deliberately small teaching architecture with fuller implementations:

- [AsiBackbone](https://github.com/AsiBackbone/AsiBackbone) — governance and policy-control implementation.
- [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate) — ASP.NET Core reference architecture.

The goal is not to make the sample imitate production complexity. The goal is to make the architectural boundary recognizable before you inspect how broader operational concerns change it.

## Source of Truth and Licensing

The detailed sample READMEs remain canonical under `samples/`; this DocFX page is a navigation and learning-path summary. When sample setup, behavior, or invariants change, update the canonical sample README first and keep this landing page's summary aligned with it.

Documentation and educational content in `docs/` are licensed under **CC BY 4.0**. Executable sample code and sample projects under `samples/` are licensed under the **MIT License** unless otherwise noted.

See [LICENSING.md](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md) for the repository's component-specific licensing policy.

---

> **Read it. Run it. Question it. Improve it.**
