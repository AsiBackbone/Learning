# ASI Backbone Learning Samples

The `samples/` directory is the executable companion-code area for **ASI Backbone Learning**.

It is intended to contain intentionally small .NET examples that complement the architectural tutorials and make important system boundaries observable through runnable code and tests.

> **Read it. Run it. Question it. Improve it.**

## Current Status

**Foundational sample set established — five governance companions plus focused security, advanced governance/trust, and ASP.NET Core architecture samples and invariant tests are available.**

The `samples/` area pairs all five foundational tutorials with runnable companion projects and sibling xUnit test projects, culminating in the Governed AI Tool Gateway capstone. Focused ASP.NET Core samples now make middleware ordering and centralized error-handling behavior executable as the application-architecture path expands.

The security path now includes a replay-protection and bounded-use sample that makes the check-then-act concurrency race, atomic consumption, replay evidence, and exactly-once boundary directly observable.

The advanced trust path now includes a cross-system capability-exchange sample that simulates independently operated issuer and recipient boundaries, recipient-owned trust, request binding, delegation rejection, replay resistance, resource drift, and local policy revalidation.

The governance architecture path includes a federated-governance coordination sample for independent authority composition, a distributed-acknowledgment sample for bound evidence and delayed continuation, a decision-explainability sample for deterministic audience-aware projection of structured governance evidence, and an adaptive-risk sample for explicit freshness, drift, reevaluation, and execution-authority boundaries.

Future sample work can focus on refinement, additional invariants, alternative patterns, and new learning areas rather than filling a gap in the foundational sequence.

For the current learning material, begin with the:

- [Foundational Tutorials](../docs/tutorials/index.md)
- [Getting Started guide](../docs/getting-started/index.md)
- [Hands-On Labs](../docs/labs/index.md)

## Purpose

The documentation explains architectural reasoning.

The samples will provide executable demonstrations of that reasoning.

The intended progression is:

```text
Learning Tutorial
   ↓
Minimal Embedded Example
   ↓
Runnable Sample
   ↓
Tests
   ↓
Hands-On Lab
   ↓
Working Repository Implementation
```

Samples should make architectural boundaries easier to observe without reproducing the complexity of a production application.

Production-oriented implementations remain primarily in:

* [AsiBackbone](https://github.com/AsiBackbone/AsiBackbone)
* [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

## Learning Navigation

The sample area sits between explanation and hands-on practice:

```text
Tutorial
   ↓
Sample
   ↓
Lab
   ↓
Working Repository
```

### Tutorials

[Browse Foundational Tutorials](../docs/tutorials/index.md)

Tutorials explain the architectural problem, common failure modes, proposed pattern, tradeoffs, and implementation references.

### Samples

Samples will demonstrate selected tutorial concepts through small executable projects and tests.

### Labs

[Browse Hands-On Labs](../docs/labs/index.md)

Labs are intended to require the learner to modify, critique, repair, extend, or reason about an architecture.

### Working Repositories

Use the organization's implementation repositories to examine similar ideas under broader production concerns:

* [AsiBackbone](https://github.com/AsiBackbone/AsiBackbone)
* [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

## Foundational Sample Set

The executable sample set follows the five foundational tutorials.

### Decision Before Execution

Related tutorial:

[Decision Before Execution](../docs/tutorials/decision-before-execution.md)

Executable companion:

[Decision Before Execution sample](decision-before-execution/README.md)

The sample demonstrates a flow such as:

```text
Request
   ↓
Intent
   ↓
Policy Context
   ↓
Governance Decision
   ↓
Execution Boundary
   ↓
Host Operation
```

An important invariant will be:

```text
Denied Decision
   ↓
Executor Invocation Count = 0
```

The lesson is that evaluating a proposed operation should remain distinct from performing it.

### Policy Context and Explicit Decision Outcomes

Related tutorial:

[Policy Context and Explicit Decision Outcomes](../docs/tutorials/policy-context-and-explicit-decision-outcomes.md)

Executable companion:

[Policy Context and Explicit Decision Outcomes sample](policy-context-and-explicit-decision-outcomes/README.md)

The sample demonstrates explicit decision inputs and outcomes such as:

```text
Allow
Deny
Defer
RequireAcknowledgment
Escalate
```

It should make information such as actor, resource, operation, and environment visible rather than hiding policy inputs throughout application code.

### Acknowledgment and Audit Residue

Related tutorial:

[Acknowledgment and Audit Residue](../docs/tutorials/acknowledgment-and-audit-residue.md)

Executable companion:

[Acknowledgment and Audit Residue sample](acknowledgment-and-audit-residue/README.md)

The sample demonstrates a workflow that can pause for explicit acknowledgment while preserving evidence of the governed path.

The distinction remains visible:

```text
Acknowledgment
   ≠
Authorization
   ≠
Execution Authority
```

The sample makes these concerns visible:

* Acknowledgment identity
* Actor binding
* Operation binding
* Expiration
* Re-evaluation
* Reason codes
* Correlation
* Audit residue

### Scoped Capability and Host-Owned Execution

Related tutorial:

[Scoped Capability and Host-Owned Execution](../docs/tutorials/scoped-capability-and-host-owned-execution.md)

Executable companion:

[Scoped Capability and Host-Owned Execution sample](scoped-capability-and-host-owned-execution/README.md)

The sample demonstrates narrow, temporary execution authority rather than broad standing permission.

The sample makes these capability bindings visible:

* Actor
* Operation
* Resource
* Resource state version
* Audience
* Policy version
* Acknowledgment reference
* Expiration
* Intended use

An important invariant is:

```text
Expired Capability
   ↓
Execution Blocked
```

Another is:

```text
Resource Changed After Approval
   ↓
Capability Validation Fails
```

### Governed AI Tool Gateway

Related tutorial:

[Governed AI Tool Gateway](../docs/tutorials/governed-ai-tool-gateway.md)

Executable companion:

[Governed AI Tool Gateway sample](governed-ai-tool-gateway/README.md)

The sample composes the earlier concepts into an AI-assisted execution boundary using a local simulated proposal generator and dry-run host-owned handler.

The central rule remains:

> **The model may propose. The host retains execution authority.**

The sample demonstrates:

```text
User Request
   ↓
AI Proposes Tool Action
   ↓
Host Validates Proposal
   ↓
Host Builds Authoritative Context
   ↓
Governance Decision
   ↓
Acknowledgment when required
   ↓
Scoped Capability
   ↓
Execution-Boundary Validation
   ↓
Host-Owned Dry-Run Tool Execution
   ↓
Audit Residue
```

Important invariants include:

```text
AI Proposes Unknown Tool
   ↓
Host Rejects Proposal
   ↓
No Execution
```

and:

```text
Model Claims External Destination Is Internal
   ↓
Host Rebuilds Authoritative Classification
   ↓
AcknowledgmentRequired
```

## Governance and Policy Architecture Samples

Governance samples make policy behavior observable without turning simulation or evaluation into protected execution.

### Decision Pipeline Refactoring

Related learning material:

- [Decision Before Execution](../docs/tutorials/decision-before-execution.md)
- [Policy Context and Explicit Decision Outcomes](../docs/tutorials/policy-context-and-explicit-decision-outcomes.md)
- [Refactor Scattered Governance Checks lab](../docs/labs/refactor-scattered-governance-checks.md)

Executable companion:

[Decision Pipeline Refactoring sample](decision-pipeline-refactoring/README.md)

The sample contrasts an intentionally flawed `account.disable` service with a reference decision pipeline. The flawed path mutates account state and sends a notification before later policy checks can return `Denied`, `Deferred`, `AcknowledgmentRequired`, or `EscalationRecommended`.

The refactored path makes the boundary executable:

```text
Authoritative context
   ↓
Explicit decision
   ↓
Continuation requirements
   ↓
CanExecute?
   ├── No  → evidence + return
   └── Yes → protected executor exactly once
```

Focused tests verify zero executor calls for every blocked outcome and exactly one call for `Allowed`, while a diagnostic starter test preserves evidence of the original side-effect-before-decision defect.

### Minimal Policy Simulation Harness

Related learning material:

- [Practical Policy Testing and Decision-Table Strategies](../docs/governance/practical-policy-testing-and-decision-table-strategies.md)
- [Policy Versioning and Decision Provenance](../docs/governance/policy-versioning-and-decision-provenance.md)
- [Regional and Tenant Policy Overlays](../docs/advanced/regional-and-tenant-policy-overlays.md)

Executable companion:

[Minimal Policy Simulation Harness sample](policy-simulation-harness/README.md)

The sample evaluates fictional `customer.export` scenarios containing explicit actor, resource, operation, region, tenant, risk, environment, and policy-version coordinates.

It demonstrates comparisons such as:

```text
same intent + different region
same intent + different tenant
same intent + different risk
same intent + different policy version
```

Each scenario returns a structured decision with reason code, policy identity/version, and matched constraint evidence.

The central invariant is:

> **Simulation evaluates decisions but never owns or invokes a protected executor.**

Focused tests verify deterministic decision comparisons, explicit policy-version behavior, constraint evidence, unavailable-policy handling, and the non-execution boundary.

### Federated Governance and Independent Authority Coordination

Related advanced learning material:

[Federated Governance and Independent Authority Coordination](../docs/advanced/federated-governance-and-independent-authority-coordination.md)

Executable companion:

[Federated Governance Coordination sample](federated-governance-coordination/README.md)

The sample keeps two fictional authority domains in memory and makes federation composition behavior directly testable without network or policy-engine dependencies.

Its central boundary is:

```text
Current host facts
        |
        v
Authority-set resolution
        |
        v
Independent contributions
        |
        v
Versioned composition contract
        |
        v
Explicit federated outcome
```

Focused tests prove order-independent composition, `Unavailable` remaining distinct from both `Allowed` and `Denied`, explicit invalid-contribution handling, authority-set drift invalidating an old composite decision, coordinator outages preserving federated requirements, and legitimate local-only continuation only when current facts classified the operation as local before the outage.

The sample stops at the federated decision boundary. It does not claim a production federation protocol, distributed consensus, cross-region durability, or protected external execution.

### Distributed Acknowledgment and Continuation Workflows

Related advanced learning material:

[Distributed Acknowledgment and Continuation Workflows](../docs/advanced/distributed-acknowledgment-and-continuation-workflows.md)

Executable companion:

[Distributed Acknowledgment and Continuation sample](distributed-acknowledgment-continuation/README.md)

The sample models a fictional `System A -> System B -> System C` acknowledgment lifecycle for `accounts.bulk-suspend` while keeping the original decision, bound challenge, response evidence, current context, current policy decision, continuation claim, narrow authority, and executor command distinct.

Its central boundary is:

```text
Bound acknowledgment evidence
        |
        v
Recipient trust + binding checks
        |
        v
Current context reconstruction
        |
        v
Current policy re-evaluation
        |
        v
Single continuation claim
        |
        v
Narrow local authority
        |
        v
Host-owned dry-run executor
```

Focused tests cover intent and responder substitution, expiration, current-policy denial, replay, different duplicate responses, evidence-verifier unavailability, untrusted evidence, out-of-order challenge recovery, resource drift, and end-to-end lineage.

The sample uses process-local teaching stores and simulated evidence trust. It does not claim production messaging, distributed atomic state, evidence cryptography, durable workflow recovery, or exactly-once external execution.

### Decision Explainability for Human Operators

Related advanced learning material:

[Decision Explainability for Human Operators](../docs/advanced/decision-explainability-for-human-operators.md)

Executable companion:

[Decision Explainability sample](decision-explainability/README.md)

The sample begins with fictional structured decision evidence and projects it into deterministic end-user or operator explanations. It keeps reason identity, policy provenance, disclosure state, and presentation separate.

Its central boundary is:

```text
Structured decision evidence
        |
        v
Versioned explanation rules
        +
Explicit audience
        |
        v
Minimized human explanation
```

Focused tests verify safe regional data-residency wording, policy-version lineage, `Deferred` versus denial semantics, acknowledgment and escalation wording, deterministic multi-reason ordering, audience-specific withholding, unknown-reason fallback, projection-version identity, and historical policy-version preservation.

The sample does not evaluate policy, authorize explanation viewers, call a generative model, or invoke protected execution. Explanation remains downstream presentation rather than governance authority.

### Adaptive Risk Context, Freshness, and Drift

Related advanced learning material:

[Adaptive Risk Context, Freshness, and Drift](../docs/advanced/adaptive-risk-context-freshness-and-drift.md)

Executable companion:

[Adaptive Risk Context sample](adaptive-risk-context/README.md)

The sample models a fictional `payment.release` path with deterministic payment context, captured fraud-risk observations, explicit decision identity, versioned threshold/freshness policy, narrow execution authority, execution-time drift validation, an atomic in-process single-use claim, and final executor-side authority/command validation.

Its central boundary is:

```text
Captured risk observation
        +
Current deterministic context
        |
        v
Versioned governance policy
        |
        v
Decision
        |
        v
Narrow authority when allowed
        |
        v
Current freshness/drift checks
        |
        v
Atomic single-use claim
        |
        v
Validated command
        |
        v
Dry-run host-owned executor
```

Focused tests cover future/stale/unavailable and unapproved evidence, signal/provider/model/scoring/calibration drift, host-owned maximum age, threshold-policy changes, model-health uncertainty, distinct decision identity, resource/amount/destination/environment drift, hard payment substitution, audience/operation bindings, authority time bounds, sequential and actually concurrent replay, final executor binding checks, observation integrity, historical provenance, and the `0.21 / risk-v7` to `0.76 / risk-v8` reevaluation scenario.

The sample does not perform real model inference, model/data drift detection, retraining, durable distributed replay coordination, payment execution, or production risk-provider integration. Risk remains an input to governance rather than an execution credential.

## Security and Trust Architecture Samples

Security samples isolate state, trust, and execution-boundary behavior that benefits from direct observation under failure or concurrency pressure.

### Replay Protection and Bounded-Use Authority

Related tutorial:

[Replay Protection and Bounded-Use Authority](../docs/security/replay-protection-and-bounded-use.md)

Executable companion:

[Replay Protection and Bounded-Use Authority sample](replay-protection-and-bounded-use/README.md)

The sample contrasts an intentionally unsafe check-then-act use store with an atomic in-process `TryConsumeAsync` boundary.

Its central invariant is:

```text
MaximumUses = 1
        ↓
Two concurrent consumers
        ↓
Exactly one consumption succeeds
        ↓
Exactly one reaches protected execution
```

Focused tests also cover sequential replay rejection, bounded-use counts, static validation before consumption, rejected-replay evidence, cancellation before consumption, replay-store unavailability, and execution failure after authority has already been consumed.

The sample explicitly limits its claim to process-local in-memory coordination. It does not claim durable multi-instance replay protection or exactly-once completion of an external side effect.

Companion lab:

[Replay Protection and Bounded-Use Authority lab](../docs/labs/replay-protection-and-bounded-use.md)

The lab begins from the deliberately unsafe implementation, requires the learner to repair the race, and then extends the reasoning into durable-store semantics, idempotency, and recovery.

### Cross-System Capability Exchange and Delegated Authority

Related advanced learning material:

[Cross-System Capability Exchange and Delegated Authority](../docs/advanced/cross-system-capability-exchange-and-delegated-authority.md)

Executable companion:

[Cross-System Capability Exchange sample](cross-system-capability-exchange/README.md)

The sample simulates a fictional `System A -> System B` handoff for `records.export` while keeping issuer trust, recipient-local policy, bounded use, and host-owned execution separate.

Its central flow is:

```text
System A narrow grant
        ↓
System B verifies + validates
        ↓
System B current local policy
        ↓
Atomic single-use claim
        ↓
Validated local command
        ↓
Dry-run executor
```

Focused tests cover wrong audience, expiry, resource drift, request substitution, unexpected delegation chains, recipient-local denial, unknown trust anchors, replay, an actually concurrent single-use race, and the burned-grant behavior when execution fails after a successful claim.

The proof and presenter bindings are deliberately simulated. The sample does not claim production cryptography, proof-of-possession, distributed replay guarantees, or exactly-once external execution.

## ASP.NET Core Architecture Samples

The sample area also supports focused ASP.NET Core architecture lessons beyond the five foundational governance tutorials.

### Middleware Ordering Changes Behavior

Related tutorial:

[Middleware Ordering Changes Behavior](../docs/aspnetcore/middleware-ordering-changes-behavior.md)

Executable companion:

[Middleware Ordering Changes Behavior sample](middleware-ordering-changes-behavior/README.md)

The sample contains corrected and deliberately incorrect middleware sequences and focused tests for:

```text
Request order
   ↓
Endpoint
   ↓
Reverse response order
```

and:

```text
Exception boundary before fault
   ↓
Failure handled
```

versus:

```text
Fault before exception boundary
   ↓
Custom handler never entered
```

The sample remains deliberately smaller than `NetCoreApplicationTemplate`; the working repository is used to inspect routing, proxy correction, request logging, security headers, rate limiting, authentication, authorization, and other production concerns in a fuller pipeline.

### Centralized Error Handling and Problem Details

Related tutorial:

[Centralized Error Handling and Problem Details](../docs/aspnetcore/centralized-error-handling-and-problem-details.md)

Executable companion:

[Centralized Error Handling and Problem Details sample](centralized-error-handling-and-problem-details/README.md)

The sample uses a small ASP.NET Core API to keep these paths distinct:

```text
Unexpected exception
   ↓
Central IExceptionHandler
   ↓
Safe Problem Details + structured exception log
```

versus:

```text
Expected governance outcome
   ↓
Explicit host HTTP mapping
   ↓
Problem Details when the transport needs one
```

Focused integration tests prove that an unexpected failure becomes a safe `500`, a known dependency failure maps consistently to `503`, sensitive exception detail is not returned publicly, a response `traceId` can be correlated with the handler log, expected governance outcomes do not pass through the exception handler, and an ordinary missing route can use Problem Details without throwing.

## Sample Design Principles

Samples should optimize for **learning value**, not production completeness.

### Keep Samples Small

Include only the abstractions and infrastructure necessary to demonstrate the lesson.

A sample should make the important boundary easier to see, not bury it beneath production scaffolding.

### Keep Side Effects Visible

Governance evaluation should not silently perform the operation it evaluates.

The transition from decision to execution should remain explicit.

Prefer:

```text
Evaluate
   ↓
Decision
   ↓
Execution Boundary
   ↓
Execute
```

over:

```text
Evaluate
   ↓
Hidden Side Effect
```

### Prefer Framework-Neutral Concepts Where Practical

A sample does not need to depend on the `AsiBackbone` package simply because the corresponding architectural concept also exists there.

Where practical, samples should teach the underlying pattern first.

The working implementation repository can then demonstrate how the pattern appears within a fuller framework.

### Test Architectural Invariants

Tests should demonstrate meaningful behavior rather than only object construction.

Examples include:

```text
Denied Decision
   ↓
Executor Invocation Count = 0
```

```text
Expired Capability
   ↓
Execution Blocked
```

```text
Changed Resource
   ↓
Capability Rejected
```

```text
Unknown AI Tool
   ↓
Proposal Rejected
   ↓
No Execution
```

These tests help make the architectural contract executable.

Current test projects are named consistently with their sample using the `<SampleName>.Tests` convention and are included in the shared `Samples.slnx` solution so repository-level `dotnet test` and sample CI execute them automatically.

### Prefer Deterministic Local Behavior

Samples should be easy to run without external infrastructure where practical.

Prefer:

* In-memory state
* Fakes
* Mocks
* Simulated external systems
* Dry-run execution
* Deterministic test data

Avoid requiring production services merely to demonstrate an architectural idea.

### Use Fictional Data

Samples should use fictional or placeholder identities, resources, destinations, policy names, and credentials.

Do not include:

* Real credentials
* Real secrets
* Real access tokens
* Real personal information
* Production connection strings

### Keep Secrets Host-Owned

For AI-related examples, a model or proposal generator should not require infrastructure credentials merely because it proposes an operation.

Prefer:

```text
Model
   ↓
Proposal

Host
   ↓
Validation
   ↓
Policy
   ↓
Credentials
   ↓
Execution
```

### Do Not Hide Policy in Prompt Text

Prompt instructions may influence AI behavior.

They should not be treated as the authoritative execution boundary.

For example:

```text
Prompt:
"Do not delete protected records."
```

may guide the model.

A host-side rule that prevents a protected resource from reaching the executor is the stronger control boundary.

### Prefer Narrow Semantic Operations

Prefer operations such as:

```text
notification.send
account.disable
case.archive
```

over broad primitives such as:

```text
execute_shell
run_sql
invoke_arbitrary_method
```

unless the broad primitive itself is the subject of the lesson.

Narrow operations make validation, governance, testing, and audit behavior easier to reason about.

## Dry-Run First

Samples involving consequential operations should generally begin without performing real external side effects.

A useful initial flow is:

```text
Governance Decision
   ↓
Capability Validation
   ↓
WouldExecute = true
```

rather than immediately performing:

```text
Send Real Email
Delete Real Resource
Modify Real Infrastructure
```

Dry-run behavior makes the execution boundary observable while reducing unnecessary setup and risk.

## Intended Build Experience

Individual executable samples support standard .NET CLI workflows such as:

```bash
dotnet run --project <sample-project>
```

The shared sample solution is [`Samples.slnx`](Samples.slnx).

From the repository root, restore, build, and test all executable samples with:

```bash
dotnet restore samples/Samples.slnx
dotnet build samples/Samples.slnx --no-restore
dotnet test samples/Samples.slnx --no-build
```

From the `samples/` directory, the equivalent commands are:

```bash
dotnet restore Samples.slnx
dotnet build Samples.slnx --no-restore
dotnet test Samples.slnx --no-build
```

Common compiler settings are centralized in [`Directory.Build.props`](Directory.Build.props), so sample projects share the same target framework, nullable configuration, implicit-usings setting, and warnings-as-errors behavior.

Focused xUnit test projects live beside their corresponding executable sample projects and reference those executable projects directly. This keeps the samples small while making their architectural contracts testable without introducing separate class-library layers solely for testing.

## Per-Sample Documentation

Each sample should include its own `README.md` when setup, execution, or interpretation requires additional explanation.

A sample README should normally identify:

* Learning objective
* Related tutorial
* Difficulty
* Prerequisites
* Architectural boundary being demonstrated
* Project structure
* How to run the sample
* How to run its tests
* Expected behavior
* Important invariants
* Tradeoffs
* What the sample intentionally omits
* Links to fuller working implementations

## Relationship to Tutorials

A tutorial explains the architectural reasoning.

A sample should make selected portions of that reasoning executable.

The relationship should remain bidirectional:

```text
Tutorial
   ↕
Sample
```

Each sample should link to its related tutorial.

Each tutorial should link to its executable sample once that sample exists.

## Relationship to Labs

Samples and labs serve different learning purposes.

### Samples Demonstrate

A sample should provide:

* A known implementation
* Runnable behavior
* Focused tests
* Observable architectural boundaries

### Labs Challenge

A lab may instead provide:

* Missing behavior
* Failing tests
* A deliberately weak architecture
* A policy-design exercise
* A security-boundary problem
* A design to critique
* Multiple possible solutions

The intended progression is:

```text
Tutorial
   ↓
Sample
   ↓
Lab
   ↓
Alternative Implementation
```

Tutorials explain.

Samples demonstrate.

Labs require the learner to decide.

## Relationship to the Working Repositories

Samples are teaching artifacts.

They should not duplicate the complete implementation documentation of the organization's working repositories.

### AsiBackbone

[AsiBackbone/AsiBackbone](https://github.com/AsiBackbone/AsiBackbone)

Provides fuller governance and policy-control implementations, including areas such as:

* Policy evaluation
* Structured decisions
* Acknowledgment workflows
* Audit residue
* Capability boundaries
* Host-owned execution
* AI governance scenarios

### NetCoreApplicationTemplate

[AsiBackbone/NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

Provides a fuller ASP.NET Core reference architecture demonstrating:

* Middleware organization
* Structured logging
* Security defaults
* Error handling
* Rate limiting
* Authentication-ready design
* Data-access patterns
* Operational application structure
* Architecture Decision Records

Learning samples should remain smaller than these repositories by design.

## Licensing

ASI Backbone Learning uses component-specific licensing.

Executable source code and sample projects added under `samples/` are licensed under the **MIT License** unless otherwise noted.

Documentation and educational material elsewhere in the repository are licensed under **Creative Commons Attribution 4.0 International (CC BY 4.0)**.

The intended licensing boundary is:

```text
docs/**
community/**
repository educational material
diagrams and exercises
        ↓
CC BY 4.0

samples/**
executable sample code
        ↓
MIT
```

Source-code snippets embedded in documentation are additionally available under the MIT License unless otherwise noted.

See:

* [`LICENSING.md`](../LICENSING.md)
* [`LICENSES/MIT.txt`](../LICENSES/MIT.txt)
* [`LICENSES/CC-BY-4.0.txt`](../LICENSES/CC-BY-4.0.txt)

for the complete component-specific licensing policy.

## Contributing Samples

Sample contributions are welcome as executable companion projects are introduced.

Good sample contributions should:

* Demonstrate a specific architectural lesson.
* Remain intentionally small.
* Compile successfully.
* Include tests when behavior is important to the lesson.
* Avoid unnecessary dependencies.
* Prefer deterministic local behavior.
* Use fictional or placeholder data.
* Avoid credentials, secrets, tokens, and personal information.
* Explain important tradeoffs.
* Preserve explicit execution and trust boundaries.
* Link to the relevant tutorial.
* Identify alternative approaches where useful.

Executable code contributed under `samples/` should be compatible with the MIT License.

See [`CONTRIBUTING.md`](../CONTRIBUTING.md) for the repository's broader contribution guidance.

## Scope and Boundaries

Future samples in this directory will be educational artifacts.

They should not be interpreted as:

* Production applications
* Security guarantees
* Compliance certifications
* Legal standards
* AI models
* AGI or ASI implementations
* Autonomous-agent runtimes
* Robotics controllers
* Complete production threat models
* Substitutes for application-specific security review

A pattern that behaves correctly in a small sample still requires production engineering appropriate to the environment in which it is adopted.

## Next Step

The five foundational tutorials now have executable companion samples, and the capstone AI gateway is paired with an advanced lab. The next development step is to refine invariant coverage, strengthen implementation links, and expand into alternative and deeper architecture topics where they add learning value.

Use the [Foundational Tutorials](../docs/tutorials/index.md) as the primary learning path and the working repositories for fuller implementation examples.

---

> **Read it. Run it. Question it. Improve it.**

