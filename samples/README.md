# ASI Backbone Learning Samples

This directory contains executable sample code supporting the tutorials and architectural material in **ASI Backbone Learning**.

The samples are intentionally small.

Their purpose is not to reproduce the full `AsiBackbone` framework or `NetCoreApplicationTemplate`. Instead, they provide runnable implementations of the architectural boundaries discussed throughout the Learning repository.

> **Read it. Run it. Question it. Improve it.**

## Purpose

The documentation explains architectural reasoning.

The samples demonstrate that reasoning in executable .NET code.

A typical relationship is:

```text
Learning Tutorial
   ↓
Minimal code examples
   ↓
Runnable sample
   ↓
Tests
   ↓
Working repository implementation
````

Each sample should remain focused enough that the architectural lesson is easy to see.

Production-oriented complexity belongs primarily in the working implementation repositories:

* [AsiBackbone](https://github.com/AsiBackbone/AsiBackbone)
* [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

## Foundational Samples

The initial sample set follows the five foundational tutorials.

### Decision Before Execution

Demonstrates the separation between:

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

The important invariant is that a blocked decision never reaches the executor.

Related tutorial:

[Decision Before Execution](../docs/tutorials/decision-before-execution.md)

### Policy Context and Explicit Decision Outcomes

Demonstrates how the facts used for governance evaluation can be represented explicitly rather than scattered throughout application code.

Example outcomes include:

```text
Allow
Deny
Defer
Require Acknowledgment
Escalate
```

Related tutorial:

[Policy Context and Explicit Decision Outcomes](../docs/tutorials/policy-context-and-explicit-decision-outcomes.md)

### Acknowledgment and Audit Residue

Demonstrates how a consequential workflow can pause for explicit acknowledgment while preserving structured evidence of the governed path.

The sample should preserve the distinction:

```text
Acknowledgment
≠
Authorization
≠
Execution Authority
```

Related tutorial:

[Acknowledgment and Audit Residue](../docs/tutorials/acknowledgment-and-audit-residue.md)

### Scoped Capability and Host-Owned Execution

Demonstrates how an allowed operation can produce narrow, short-lived execution authority without granting broad or permanent permission.

The sample should preserve bindings such as:

* Actor
* Operation
* Resource
* Audience
* Policy version
* Acknowledgment reference
* Expiration
* Intended use

Related tutorial:

[Scoped Capability and Host-Owned Execution](../docs/tutorials/scoped-capability-and-host-owned-execution.md)

### Governed AI Tool Gateway

Composes the earlier patterns into an AI-assisted execution boundary.

The central rule is:

> **The model may propose. The host retains execution authority.**

A typical sample flow is:

```text
User request
   ↓
AI proposes tool action
   ↓
Host validates proposal
   ↓
Host constructs authoritative context
   ↓
Governance decision
   ↓
Acknowledgment when required
   ↓
Scoped capability
   ↓
Execution-boundary validation
   ↓
Host-owned tool execution
   ↓
Audit residue
```

Related tutorial:

[Governed AI Tool Gateway](../docs/tutorials/governed-ai-tool-gateway.md)

## Sample Design Principles

Samples should optimize for **learning value**, not production completeness.

### Keep Samples Small

Avoid introducing infrastructure or abstractions that are not necessary to demonstrate the architectural lesson.

### Keep Side Effects Visible

Governance evaluation should not silently perform the operation it evaluates.

The transition from decision to execution should remain explicit and testable.

### Prefer Framework-Neutral Concepts

Where practical, samples should demonstrate the underlying architectural pattern without requiring the `AsiBackbone` package.

The working framework can then be referenced as a fuller implementation.

### Test Architectural Invariants

Tests should verify behavioral boundaries rather than only object values.

Examples include:

```text
Denied decision
   ↓
Executor invocation count = 0
```

```text
Expired capability
   ↓
Execution blocked
```

```text
Changed resource after approval
   ↓
Capability validation fails
```

```text
AI proposes unknown tool
   ↓
Host rejects proposal
   ↓
No execution
```

### Do Not Hide Policy in Prompt Text

For AI-related samples, prompt instructions and tool descriptions may guide model behavior, but they are not execution controls.

Host-side validation and policy remain authoritative.

### Prefer Narrow Semantic Operations

Prefer:

```text
notification.send
account.disable
case.archive
```

over broad interfaces such as:

```text
execute_shell
run_sql
invoke_arbitrary_method
```

unless the broader interface is specifically the subject of the lesson.

### Keep Secrets Host-Owned

Sample AI models or proposal generators should not require infrastructure credentials merely because they propose an operation.

The host-owned execution component should retain responsibility for external credentials and side effects.

## Building the Samples

The sample projects are intended to build with the .NET SDK.

From the repository root, restore dependencies using:

```bash
dotnet restore
```

Build the sample solution using:

```bash
dotnet build
```

Run tests using:

```bash
dotnet test
```

As the sample collection develops, more specific commands may be documented here for the sample solution or individual projects.

## Running Individual Samples

Each sample directory should contain its own `README.md` when additional setup or execution instructions are necessary.

A sample may be runnable using:

```bash
dotnet run --project <sample-project>
```

or tested using:

```bash
dotnet test <test-project>
```

Prefer deterministic local demonstrations where practical.

Examples involving external systems should use simulation, mocks, fakes, or dry-run behavior by default.

## Dry-Run First

Samples involving consequential operations should generally begin without real external side effects.

For example:

```text
Governance decision
   ↓
Capability validation
   ↓
WouldExecute = true
```

instead of immediately:

```text
Governance decision
   ↓
Send real email
Delete real resource
Modify real infrastructure
```

This keeps the learning boundary observable without requiring production credentials or introducing unnecessary risk.

## Relationship to Labs

Samples and labs serve different purposes.

### Samples

Samples provide:

* Runnable reference implementations
* Small demonstrations
* Tests
* Known architectural patterns
* Working examples corresponding to tutorials

### Labs

Labs may provide:

* Incomplete implementations
* Deliberately weak architectures
* Failing tests
* Design challenges
* Policy exercises
* Security-boundary problems
* Architecture critique tasks

A useful progression is:

```text
Tutorial
   ↓
Sample
   ↓
Lab
   ↓
Alternative implementation
```

Tutorials explain.

Samples demonstrate.

Labs require the learner to decide.

## Relationship to the Working Repositories

The samples are teaching artifacts.

They should not duplicate the full implementation documentation of the primary ASI Backbone organization repositories.

### AsiBackbone

[AsiBackbone/AsiBackbone](https://github.com/AsiBackbone/AsiBackbone)

Provides fuller governance and policy-control implementations, including areas such as:

* Policy evaluation
* Structured governance decisions
* Acknowledgment workflows
* Audit residue
* Capability grants
* Execution-boundary guidance
* AI governance scenarios

### NetCoreApplicationTemplate

[AsiBackbone/NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

Provides a fuller ASP.NET Core reference architecture demonstrating areas such as:

* Middleware organization
* Structured logging
* Secure defaults
* Error handling
* Rate limiting
* Authentication-ready design
* Data-access patterns
* Operational application structure

The samples in this directory should remain smaller than those repositories by design.

## Licensing

Executable sample code under this directory is licensed under the **MIT License** unless otherwise noted.

This differs from the documentation and educational material in the Learning repository, which is licensed under **Creative Commons Attribution 4.0 International (CC BY 4.0)**.

The general licensing structure is:

```text
docs/**
community/**
educational material
diagrams and exercises
        ↓
CC BY 4.0

samples/**
executable sample projects
        ↓
MIT
```

Code snippets embedded in documentation are additionally available under the MIT License unless otherwise noted.

See:

* [`LICENSING.md`](../LICENSING.md)
* [`LICENSES/MIT.txt`](../LICENSES/MIT.txt)
* [`LICENSES/CC-BY-4.0.txt`](../LICENSES/CC-BY-4.0.txt)

for the complete component-specific licensing policy.

## Contributing Samples

Contributions are welcome.

Good sample contributions should:

* Demonstrate a specific architectural lesson.
* Remain intentionally small.
* Compile successfully.
* Include tests when behavior is important to the lesson.
* Avoid unnecessary dependencies.
* Use fictional or placeholder data.
* Avoid real credentials, tokens, secrets, or personal information.
* Explain important tradeoffs.
* Preserve clear execution and trust boundaries.
* Link back to the relevant tutorial.
* Identify alternative approaches when useful.

Executable code contributed under `samples/` is expected to be compatible with the MIT License.

See [`CONTRIBUTING.md`](../CONTRIBUTING.md) for broader contribution guidance.

## Scope and Boundaries

These samples are educational artifacts.

They are not:

* Production applications
* Security guarantees
* Compliance certifications
* Legal standards
* AI models
* AGI or ASI implementations
* Autonomous-agent runtimes
* Robotics controllers
* Substitutes for application-specific threat modeling
* Substitutes for security review

A pattern demonstrated successfully in a sample still requires production engineering appropriate to the system in which it is used.

## Learning Principle

The objective is not to copy these samples unchanged.

The objective is to understand why the boundaries exist well enough to decide whether they belong in your own architecture.

> **Read it. Run it. Question it. Improve it.**

