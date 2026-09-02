---
description: Browse hands-on ASI Backbone Learning labs for diagnosing, modifying, testing, and explaining architectural and governed-execution boundaries.
---

# Labs

Labs are the **practice and reasoning layer** of ASI Backbone Learning.

Tutorials explain architectural boundaries.

Executable samples demonstrate them.

Labs ask you to work with those boundaries yourself.

The intended progression is:

```text
Tutorial
   ↓
Executable Sample
   ↓
Hands-On Lab
   ↓
Alternative Approach
   ↓
Working Repository
```

## Architectural Acceptance Criteria

A lab is not complete merely because the modified program runs.

The stronger question is:

> **Does the result preserve and demonstrate the architectural boundary the exercise was designed to teach?**

Unless a lab states more specific criteria, use this lightweight self-check:

- [ ] **Required boundary demonstrated** — you can point to the responsibility or trust boundary the lab is teaching and show where it is enforced.
- [ ] **Prohibited path absent** — the failure or bypass path the lab is intended to prevent cannot silently reach protected execution or broader authority.
- [ ] **Decision evidence preserved** — when the exercise involves decisions, acknowledgment, capabilities, or lifecycle state, enough evidence remains to explain what happened.
- [ ] **Relevant failure path observable** — at least one important denied, stale, invalid, unavailable, replayed, or otherwise unsafe path can be observed rather than assumed.
- [ ] **Architectural invariant verified** — a focused test, deterministic sample result, or equivalent observation demonstrates the property being taught.
- [ ] **Tradeoff or alternative explained** — you can explain why your design is appropriate for the exercise and name a credible simpler or different approach when one exists.

Individual labs may strengthen, remove, or specialize these items when the subject requires different evidence.

The criteria intentionally evaluate outcomes rather than one prescribed implementation. Two solutions can look different in code and still succeed if both preserve the same architectural invariant.

Automated tests are useful evidence, but they are not the entire learning result. A test can pass while an important responsibility has moved to the wrong component, a bypass path remains reachable, or the learner cannot explain why the boundary matters.

## Current Status

The lab navigation foundation is established, with four beginner labs, eight intermediate labs, and five advanced labs now available.

Additional labs will appear in this section as the learning path expands into deeper architecture, security, and AI-governance topics.

The foundational labs pair all five governance tutorials with executable companion samples and ask learners to modify, challenge, extend, and threat-model the demonstrated boundaries. The governance lab path now also includes policy-version evidence, candidate-policy simulation, decision-pipeline refactoring, competing-policy-architecture selection, regional/tenant overlay design, and a synthesis-oriented flawed high-consequence workflow analysis, while the ASP.NET Core diagnostic lab begins the next learning area by requiring learners to predict, observe, repair, and explain middleware-ordering behavior; the ADR lab extends that application-architecture path by requiring learners to make, record, and revisit a decision under changed constraints.

## Available Labs

### Decision Before Execution

[Decision Before Execution](decision-before-execution.md)

**Difficulty:** Beginner

Break the execution boundary deliberately, observe why correct decision values are insufficient when the host ignores them, repair the boundary, and add a new policy constraint without moving governance logic into the executor.

Related material:

- [Decision Before Execution tutorial](../tutorials/decision-before-execution.md)
- [Decision Before Execution sample](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-before-execution/README.md)

### Policy Context and Explicit Decision Outcomes

[Policy Context and Explicit Decision Outcomes](policy-context-and-explicit-decision-outcomes.md)

**Difficulty:** Beginner

Collapse structured decisions back to a boolean to observe the information loss, extend the explicit policy context, add a stable reason-coded rule, and make rule precedence observable and testable.

Related material:

- [Policy Context and Explicit Decision Outcomes tutorial](../tutorials/policy-context-and-explicit-decision-outcomes.md)
- [Policy Context and Explicit Decision Outcomes sample](https://github.com/AsiBackbone/Learning/blob/main/samples/policy-context-and-explicit-decision-outcomes/README.md)

### Identify and Remove a Hidden Execution Side Effect

[Identify and Remove a Hidden Execution Side Effect](hidden-execution-side-effect.md)

**Difficulty:** Beginner

Start from deliberately flawed policy code that quietly starts an external deployment while it is still "checking" the request, make the denied-side-effect invariant fail visibly, and refactor the workflow so evaluation is observational and execution occurs only after an explicit decision boundary.

Related material:

- [Decision Before Execution tutorial](../tutorials/decision-before-execution.md)
- [Decision Before Execution sample](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-before-execution/README.md)
- [Decision Before Execution lab](decision-before-execution.md)

### Identify Middleware Ordering Problems

[Identify Middleware Ordering Problems](identify-middleware-ordering-problems.md)

**Difficulty:** Beginner

Run the deliberately incorrect ASP.NET Core pipeline, predict and observe the failure boundary, encode the repaired behavior in a focused test, reorder a disposable copy of the sample, and explain the changed request/response behavior in terms of wrapping, reachability, and coverage rather than a memorized middleware list.

Related material:

- [Middleware Ordering Changes Behavior](../aspnetcore/middleware-ordering-changes-behavior.md)
- [Middleware Ordering Changes Behavior sample](https://github.com/AsiBackbone/Learning/blob/main/samples/middleware-ordering-changes-behavior/README.md)
- [ASP.NET Core learning area](../aspnetcore/index.md)

### Acknowledgment and Audit Residue

[Acknowledgment and Audit Residue](acknowledgment-and-audit-residue.md)

**Difficulty:** Intermediate

Break the acknowledgment boundary deliberately, add another response-binding failure, expose replay as a state problem, preserve correlated evidence behind an in-memory store, and distinguish an allowed decision from a failed execution.

Related material:

- [Acknowledgment and Audit Residue tutorial](../tutorials/acknowledgment-and-audit-residue.md)
- [Acknowledgment and Audit Residue sample](https://github.com/AsiBackbone/Learning/blob/main/samples/acknowledgment-and-audit-residue/README.md)

### Scoped Capability and Host-Owned Execution

[Scoped Capability and Host-Owned Execution](scoped-capability-and-host-owned-execution.md)

**Difficulty:** Intermediate

Break expiration and resource-freshness checks deliberately, observe how stale authority can reach execution, restore narrow execution-boundary validation, and extend the sample with additional binding and replay exercises.

Related material:

- [Scoped Capability and Host-Owned Execution tutorial](../tutorials/scoped-capability-and-host-owned-execution.md)
- [Scoped Capability and Host-Owned Execution sample](https://github.com/AsiBackbone/Learning/blob/main/samples/scoped-capability-and-host-owned-execution/README.md)

### Replay Protection and Bounded-Use Authority

[Replay Protection and Bounded-Use Authority](replay-protection-and-bounded-use.md)

**Difficulty:** Intermediate

Reproduce a deterministic check-then-act race, repair it with atomic `TryConsume` semantics, prove that exactly one concurrent final-use consumer reaches protected execution, and reason about evidence, cancellation, durable state, idempotency, and the failure window between consumption and execution.

Related material:

- [Replay Protection and Bounded-Use Authority tutorial](../security/replay-protection-and-bounded-use.md)
- [Replay Protection and Bounded-Use Authority sample](https://github.com/AsiBackbone/Learning/blob/main/samples/replay-protection-and-bounded-use/README.md)
- [Data Access Boundaries and Transaction Reasoning](../aspnetcore/data-access-boundaries-and-transaction-reasoning.md)

### Policy-Version Evidence in Governance Decisions

[Policy-Version Evidence in Governance Decisions](policy-version-evidence-in-governance-decisions.md)

**Difficulty:** Intermediate

Preserve the policy identity that produced a decision, detect policy drift across acknowledgment, capability issuance, and execution, and distinguish useful provenance from perfect replay or cryptographic proof.

Related material:

- [Policy Context and Explicit Decision Outcomes sample](https://github.com/AsiBackbone/Learning/blob/main/samples/policy-context-and-explicit-decision-outcomes/README.md)
- [Acknowledgment and Audit Residue lab](acknowledgment-and-audit-residue.md)
- [Scoped Capability and Host-Owned Execution lab](scoped-capability-and-host-owned-execution.md)

### Policy Simulation and Change-Impact Analysis

[Policy Simulation and Change-Impact Analysis](policy-simulation-and-change-impact-analysis.md)

**Difficulty:** Intermediate

Replay identical deterministic contexts against baseline and candidate policy versions, compare outcome, reason-code, and contributor changes, expose expected and surprising change impact, add tenant and boundary cases, and prove that simulation never invokes protected execution.

Related material:

- [Practical Policy Testing and Decision-Table Strategies](../governance/practical-policy-testing-and-decision-table-strategies.md)
- [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md)
- [Regional and Tenant Policy Overlays](../advanced/regional-and-tenant-policy-overlays.md)
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md)

### Build a Governed API Operation

[Build a Governed API Operation](build-a-governed-api-operation.md)

**Difficulty:** Intermediate

Extend an authorized ASP.NET Core account-disable endpoint into a governed operation with explicit intent, authoritative context, structured outcomes, acknowledgment, scoped authority, host-owned execution, audit residue, and integration tests that prove blocked paths never invoke the underlying account service.

Related material:

- [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md)
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md)
- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md)
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md)
- [ASP.NET Core learning area](../aspnetcore/index.md)

### Refactor Scattered Governance Checks into an Explicit Decision Pipeline

[Refactor Scattered Governance Checks into an Explicit Decision Pipeline](refactor-scattered-governance-checks.md)

**Difficulty:** Intermediate

Start from a deliberately flawed `account.disable` service where role checks, resource-dependent policy, mutation, notification, exception-driven escalation, acknowledgment, and event publication are interleaved. Diagnose the real side-effect boundary, refactor toward explicit context → decision → continuation → execution → evidence phases, and prove every blocked outcome leaves the protected executor at zero calls.

Related material:

- [Decision Before Execution tutorial](../tutorials/decision-before-execution.md)
- [Decision Pipeline Refactoring sample](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-pipeline-refactoring/README.md)
- [Identify and Remove a Hidden Execution Side Effect](hidden-execution-side-effect.md)
- [When a Simple Application Service Is Enough](../architecture/when-a-simple-application-service-is-enough.md)

### Write and Revisit an Architecture Decision Record

[Write and Revisit an Architecture Decision Record](write-and-revisit-an-architecture-decision-record.md)

**Difficulty:** Intermediate

Evaluate a structured-logging architecture under competing constraints, compare credible alternatives, write a concise ADR with explicit consequences and review conditions, then revisit the decision after the telemetry platform changes and decide whether the record should be retained, deprecated, superseded, or left unchanged while only the implementation evolves.

Related material:

- [Architecture Decision Records Preserve Architectural Reasoning](../aspnetcore/architecture-decision-records-preserve-architectural-reasoning.md)
- [Architecture Decision Record Lifecycle, Review, Deprecation, and Supersession](../aspnetcore/architecture-decision-record-lifecycle-review-deprecation-and-supersession.md)
- [Structured Logging Without Sensitive-Data Sprawl](../aspnetcore/structured-logging-without-sensitive-data-sprawl.md)
- [Working Repository ADR Case Study: NetCoreApplicationTemplate](../aspnetcore/netcoreapplicationtemplate-adr-case-study.md)

### Governed AI Tool Gateway

[Governed AI Tool Gateway](governed-ai-tool-gateway.md)

**Difficulty:** Advanced

Compose the foundational patterns into one AI-assisted execution boundary, deliberately weaken proposal, context, acknowledgment, capability, replay, prompt, credential, and failure-mode controls, then threat-model the complete gateway.

Related material:

- [Governed AI Tool Gateway tutorial](../tutorials/governed-ai-tool-gateway.md)
- [Governed AI Tool Gateway sample](https://github.com/AsiBackbone/Learning/blob/main/samples/governed-ai-tool-gateway/README.md)

### Safe Degraded Mode and Fail-Safe Governance

[Safe Degraded Mode and Fail-Safe Governance](safe-degraded-mode-and-fail-safe-governance.md)

**Difficulty:** Advanced

Classify which trust or operational property is unavailable, compare low-consequence and consequential operations, design explicit deny/defer/escalate or bounded degraded behavior, and prove that policy, replay, verification, acknowledgment, evidence, and executor failures do not silently manufacture execution authority.

Related material:

- [Governed AI Tool Gateway sample](https://github.com/AsiBackbone/Learning/blob/main/samples/governed-ai-tool-gateway/README.md)
- [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md)
- [Signing, Verification, Key Custody, and Tamper Evidence](../security/signing-verification-key-custody-and-tamper-evidence.md)
- [Centralized Error Handling and Problem Details](../aspnetcore/centralized-error-handling-and-problem-details.md)
- [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md)

### Compare Competing Policy Architectures

[Compare Competing Policy Architectures](compare-competing-policy-architectures.md)

**Difficulty:** Advanced

Choose among embedded rules, ASP.NET Core authorization, external policy evaluation, distributed enforcement, and richer governed-decision lifecycles across realistic scenarios. Defend each choice from explicit lifecycle, trust-boundary, availability, provenance, and operational constraints, then explain why a rejected alternative may be better under different requirements.

Related material:

- [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md)
- [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md)
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md)
- [Safe Degraded Mode and Fail-Safe Governance](safe-degraded-mode-and-fail-safe-governance.md)

### Design a Regional and Tenant Policy Layer

[Design a Regional and Tenant Policy Layer](design-regional-and-tenant-policy-layer.md)

**Difficulty:** Advanced

Design a conventional enterprise data-export overlay spanning global, regional, tenant, application, and operation-specific authorities. Define explicit precedence and override rules, preserve every contributing policy identity/version, handle conflicts and missing policy sources, detect region and tenant drift, require fresh authority before execution, and prove that evaluator registration order cannot silently determine governance authority.

Related material:

- [Regional and Tenant Policy Overlays](../advanced/regional-and-tenant-policy-overlays.md)
- [Constraint Composition and Policy Precedence](../governance/constraint-composition-and-policy-precedence.md)
- [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md)
- [Safe Degraded Mode and Fail-Safe Governance](safe-degraded-mode-and-fail-safe-governance.md)

### Analyze a Deliberately Flawed High-Consequence Workflow

[Analyze a Deliberately Flawed High-Consequence Workflow](analyze-flawed-high-consequence-workflow.md)

**Difficulty:** Advanced

Inspect a plausible but intentionally unsafe `account.disable` workflow without being given every defect up front. Trace caller-controlled facts, AI influence, policy, acknowledgment, cached approval, standing credentials, retry bypasses, replay, drift, dependency failure, and evidence paths; classify which findings can produce unauthorized or stale execution; then redesign the system around explicit current authority and host-owned execution invariants.

Related material:

- [Threat Modeling as Architecture Reasoning](../security/threat-modeling-as-architecture-reasoning.md)
- [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md)
- [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md)
- [Build a Governed API Operation](build-a-governed-api-operation.md)
- [Safe Degraded Mode and Fail-Safe Governance](safe-degraded-mode-and-fail-safe-governance.md)

## Start with the Tutorials

The foundational tutorial sequence establishes the concepts that the initial labs will build upon:

[Browse Foundational Tutorials](../tutorials/index.md)

Topics include:

* Decision before execution
* Explicit policy context and decision outcomes
* Acknowledgment and audit residue
* Scoped capability and host-owned execution
* Governed AI tool gateways

## Study the Executable Samples

The executable sample area provides small runnable demonstrations corresponding to the tutorial concepts.

[Browse Executable Samples](../samples/index.md)

Samples demonstrate known behavior.

Labs will increasingly ask learners to modify, repair, critique, or extend that behavior.

## Inspect the Working Repositories

After working through a teaching example or lab, compare the smaller architecture with fuller implementations.

## AsiBackbone

[AsiBackbone/AsiBackbone](https://github.com/AsiBackbone/AsiBackbone)

A .NET governance and policy-control framework providing fuller implementations of policy evaluation, structured decisions, acknowledgment workflows, audit residue, scoped capability, and host-owned execution.

## NetCoreApplicationTemplate

[AsiBackbone/NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

An ASP.NET Core reference architecture demonstrating secure defaults, middleware organization, structured logging, rate limiting, authentication-ready design, data-access patterns, and operational application structure.

## Learning Principle

The objective of a lab is not simply to reproduce a tutorial.

A useful lab should require you to make a decision, identify a failure mode, improve an architecture, or explain why one implementation is preferable under a particular set of constraints.

> **Read it. Run it. Question it. Improve it.**

