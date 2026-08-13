# ASI Backbone Learning Samples

The `samples/` directory is the executable companion-code area for **ASI Backbone Learning**.

It is intended to contain intentionally small .NET examples that complement the architectural tutorials and make important system boundaries observable through runnable code and tests.

> **Read it. Run it. Question it. Improve it.**

## Current Status

**Sample foundation established — first executable companion sample available.**

The `samples/` area defines the structure, expectations, and design principles for executable examples and now includes the first runnable companion project.

Individual sample projects will continue to be added incrementally as the foundational tutorials are paired with runnable implementations.

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

## Planned Foundational Sample Set

The initial executable sample set is intended to follow the five foundational tutorials.

### Decision Before Execution

Related tutorial:

[Decision Before Execution](../docs/tutorials/decision-before-execution.md)

Executable companion:

[Decision Before Execution sample](decision-before-execution/README.md)

The sample is expected to demonstrate a flow such as:

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

The sample is expected to demonstrate explicit decision inputs and outcomes such as:

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

The sample is expected to demonstrate a workflow that can pause for explicit acknowledgment while preserving evidence of the governed path.

The distinction should remain visible:

```text
Acknowledgment
   ≠
Authorization
   ≠
Execution Authority
```

Possible sample concerns include:

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

The sample is expected to demonstrate narrow, temporary execution authority rather than broad standing permission.

Possible capability bindings include:

* Actor
* Operation
* Resource
* Audience
* Policy version
* Acknowledgment reference
* Expiration
* Intended use

An important invariant may be:

```text
Expired Capability
   ↓
Execution Blocked
```

Another may be:

```text
Resource Changed After Approval
   ↓
Capability Validation Fails
```

### Governed AI Tool Gateway

Related tutorial:

[Governed AI Tool Gateway](../docs/tutorials/governed-ai-tool-gateway.md)

The sample is expected to compose the earlier concepts into an AI-assisted execution boundary.

The central rule remains:

> **The model may propose. The host retains execution authority.**

A representative flow may be:

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
Host-Owned Tool Execution
   ↓
Audit Residue
```

An important invariant may be:

```text
AI Proposes Unknown Tool
   ↓
Host Rejects Proposal
   ↓
No Execution
```

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

Once executable projects are added, the sample set is intended to support standard .NET CLI workflows.

The target repository-level experience is:

```bash
dotnet restore
dotnet build
dotnet test
```

Individual samples may also support commands such as:

```bash
dotnet run --project <sample-project>
```

The exact commands will be documented when the executable sample solution and projects are introduced.

Until then, the commands above describe the **intended sample-development workflow**, not a guarantee that the current `samples/` directory contains runnable projects.

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

The next development step for this directory is to establish the first buildable companion sample and repository-level sample solution structure.

Until then, use the [Foundational Tutorials](../docs/tutorials/index.md) as the primary learning path and the working repositories for fuller implementation examples.

---

> **Read it. Run it. Question it. Improve it.**

