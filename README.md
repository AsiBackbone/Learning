# ASI Backbone Learning

[![Documentation Validation](https://github.com/AsiBackbone/Learning/actions/workflows/docs-validation.yml/badge.svg?branch=main)](https://github.com/AsiBackbone/Learning/actions/workflows/docs-validation.yml)
[![Samples Validation](https://github.com/AsiBackbone/Learning/actions/workflows/samples-validation.yml/badge.svg?branch=main)](https://github.com/AsiBackbone/Learning/actions/workflows/samples-validation.yml)
[![Docs](https://img.shields.io/badge/docs-GitHub%20Pages-blue)](https://asibackbone.github.io/Learning/)
[![Zenodo DOI](https://img.shields.io/badge/DOI-10.5281%2Fzenodo.21938556-blue)](https://doi.org/10.5281/zenodo.21938556)


**Practical .NET architecture education for governed execution, secure applications, AI integration, and policy-driven systems.**

`AsiBackbone/Learning` is an open, community-oriented learning resource and the educational layer of the ASI Backbone organization.

The goal is not to create another framework or package. The goal is to explain architectural ideas clearly, demonstrate them through focused examples, examine their tradeoffs, and connect those lessons to working implementations in the organization's existing repositories.

## Quick Start — Run It in 10 Minutes

Prefer to see the architecture run before reading the deeper explanation? The foundational **Decision Before Execution** sample provides the shortest path from clone to observable behavior.

**Prerequisite:** .NET 10 SDK

From a terminal:

```bash
git clone https://github.com/AsiBackbone/Learning.git
cd Learning

dotnet run --project samples/decision-before-execution/DecisionBeforeExecution/DecisionBeforeExecution.csproj

dotnet test samples/decision-before-execution/DecisionBeforeExecution.Tests/DecisionBeforeExecution.Tests.csproj
```

### What to Observe

The sample makes one architectural invariant visible:

> **A blocked decision never reaches the executor.**

```text
Allowed decision
   ↓
Host-owned executor invoked

Denied / deferred / escalation-recommended / acknowledgment-required decision
   ↓
Executor not invoked
```

The console sample evaluates five deterministic scenarios and verifies that exactly one allowed operation crosses the execution boundary. The focused xUnit tests make the same contract repeatable for local development and CI.

Want to understand why this boundary exists or experiment with it?

- [Decision Before Execution sample README](samples/decision-before-execution/README.md)
- [Decision Before Execution tutorial](docs/tutorials/decision-before-execution.md)
- [Getting Started](docs/getting-started/index.md)

This gives code-first readers a **run → observe → understand → experiment** path while preserving the existing explanation-first learning path below.

## Start Here

New to governed-execution architecture? Start with [Decision Before Execution](docs/tutorials/decision-before-execution.md) for the foundational separation between proposed intent, explicit decision-making, and host-owned execution.

Already using ASP.NET Core authorization and wondering whether you need anything broader? Start with [When ASP.NET Core Authorization Is Enough](docs/architecture/when-aspnet-core-authorization-is-enough.md). It demonstrates the framework-native alternative and explains where a broader governance pipeline may add value.

Already know the problem you are trying to solve? Use [Find Your Path](docs/getting-started/find-your-path.md) to route from common goals to the shortest relevant tutorials, samples, labs, simpler alternatives, and deeper implementation references.

You do not need to install an `AsiBackbone` package to use this material. The tutorials, samples, comparisons, and labs are intended to remain useful as independent architecture education.

## Purpose

Modern software systems increasingly need to answer questions that go beyond:

> Is this user authorized?

They also need to ask:

* What action is being proposed?
* What context applies to the decision?
* Which policies and constraints are active?
* Why was an action allowed, denied, deferred, acknowledged, or escalated?
* What authority should exist after approval?
* How long should that authority remain valid?
* Who owns final execution?
* What evidence should remain afterward?
* How can secure defaults and application structure reduce operational risk?

This repository explores those questions through tutorials, diagrams, minimal examples, architectural discussions, and hands-on labs.

## Relationship to the ASI Backbone Organization

The organization currently contains complementary projects with different responsibilities.

### [AsiBackbone](https://github.com/AsiBackbone/AsiBackbone)

A .NET governance and policy-control framework for building explicit, auditable, policy-governed decision pipelines.

It provides working implementations of concepts such as:

* Policy evaluation
* Structured decision results
* Acknowledgment workflows
* Audit residue and provenance
* Capability-scoped authority
* Host-owned execution boundaries
* AI and application governance patterns

### [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

An enterprise-oriented ASP.NET Core reference application demonstrating secure-by-default application architecture and operational structure.

Topics include:

* Middleware organization
* Structured logging
* Security defaults
* Error handling
* Rate limiting
* Authentication-ready architecture
* Data-access patterns
* Production-oriented application structure
* Architecture Decision Records

### [Learning](https://asibackbone.github.io/Learning/)

This repository explains the **why**, demonstrates the **how** in intentionally small examples, and points to the other repositories when readers want to inspect fuller working implementations.

A useful way to think about the relationship is:

```text
Learning
   |
   | teaches concepts, patterns, tradeoffs, and minimal examples
   |
   +--------> AsiBackbone
   |          working governance implementation
   |
   +--------> NetCoreApplicationTemplate
              working ASP.NET Core reference implementation
```

## Learning Philosophy

The material in this repository follows a simple principle:

> **Read it. Run it. Question it. Improve it.**

This is intended to be a living learning resource rather than a static manual.

Readers are encouraged to:

* Study individual patterns without adopting an entire framework.
* Copy or adapt useful ideas into their own systems.
* Compare the demonstrated approach with alternative architectures.
* Question assumptions and identify tradeoffs.
* Submit corrections, examples, diagrams, tutorials, and alternative approaches.
* Use the existing ASI Backbone repositories as working architectural specimens.

Successful adoption does not require installing an `AsiBackbone` package.

If a developer studies a pattern here, improves it, adapts it to another system, or uses it to make a better architectural decision, this project is serving its purpose.

## Tutorial Model

Where practical, tutorials should follow a common progression:

```text
Problem
   |
   v
Naive or common implementation
   |
   v
Failure mode or limitation
   |
   v
Architectural pattern
   |
   v
Minimal teaching example
   |
   v
Tradeoffs and alternatives
   |
   v
Working repository example
```

This keeps the material problem-first rather than product-first.

A tutorial should remain useful even to someone who never installs AsiBackbone or uses NetCoreApplicationTemplate.

## Foundational Learning Areas

The first tutorials are focused on foundational architectural patterns such as:

### Decision Before Execution

Separate a proposed operation from the authority and mechanism that ultimately performs it.

```text
Intent
   ↓
Context
   ↓
Constraints
   ↓
Decision
   ↓
Acknowledgment when required
   ↓
Scoped authority
   ↓
Host-owned execution
   ↓
Audit residue
```

### Policy Context

Represent the information needed to make a governance decision explicitly rather than scattering it across controllers, services, middleware, and logging statements.

### Explicit Decision Outcomes

Model meaningful outcomes such as:

* Allow
* Deny
* Defer
* Require acknowledgment
* Escalate

### Acknowledgment Boundaries

Pause consequential operations when human or system acknowledgment is required before proceeding.

### Audit Residue and Provenance

Preserve structured evidence explaining what was requested, which policy applied, why the decision occurred, and what authority followed from it.

### Capability-Scoped Execution

Represent permission to perform a consequential operation as narrow, short-lived authority instead of broad standing access.

### Host-Owned Execution

Keep policy evaluation separate from real-world execution.

A governance framework may determine whether an operation should proceed without becoming the component that performs the operation itself.

### AI Agent Gateways

Apply the same principles when AI systems propose tool calls, API operations, workflows, or other consequential actions.

A useful design rule is:

> **The model may propose. The host retains execution authority.**

## Repository Structure

The repository structure resembles:

```text
Learning/
│
├── README.md
├── CONTRIBUTING.md
├── CODE_OF_CONDUCT.md
├── GOVERNANCE.md
├── ROADMAP.md
│
├── docs/
│   ├── getting-started/
│   ├── architecture/
│   ├── governance/
│   ├── aspnetcore/
│   ├── security/
│   ├── ai-integration/
│   ├── advanced/
│   ├── tutorials/
│   └── labs/
│
├── samples/
│   └── Samples.slnx
│
└── community/
    ├── tutorial-ideas.md
    ├── requested-topics.md
    └── contributors.md
```

This structure is intentionally flexible and may change as contributors discover better ways to organize the material.

## Tutorials vs. Labs

Tutorials and labs serve different purposes.

### Tutorials

Tutorials explain an architectural problem and walk through one or more approaches to solving it.

They should emphasize:

* Why the problem matters
* Architectural reasoning
* Minimal implementation
* Tradeoffs
* Failure modes
* Alternatives
* Links to fuller working examples

### Labs

Labs are intended for hands-on learning.

A lab may provide:

* A partially implemented application
* A broken or incomplete architecture
* A policy-design exercise
* A security or governance scenario
* A set of tests that must be made to pass
* An architecture that the learner is asked to critique or improve

The goal is to move from reading architectural ideas to actively reasoning about them.

## Canonical and Alternative Patterns

This repository should not exist to prove that one architecture is always correct.

Where useful, material may be identified as:

### Canonical Pattern

A pattern aligned with the current architecture of one or more ASI Backbone organization projects.

### Alternative Pattern

A documented community-supported approach that solves the same problem differently.

Alternative approaches are welcome when they are clearly explained, technically grounded, and presented with their tradeoffs.

The first published alternative-pattern comparison is:

* [When ASP.NET Core Authorization Is Enough](docs/architecture/when-aspnet-core-authorization-is-enough.md)

Architectural disagreement can be educational.

## Contribution

Community participation is a central goal of this repository.

Useful contributions may include:

* New tutorials
* Corrections
* Better explanations
* Minimal code examples
* Diagrams
* Labs
* Architecture critiques
* Alternative implementations
* Failure-mode analysis
* Additional use cases
* Accessibility improvements
* Documentation improvements
* Links between tutorials and working repository implementations

Contributions do not need to involve production framework code to be valuable.

A clearer paragraph, a better diagram, or a simpler example can materially improve a learning resource.

See [`CONTRIBUTING.md`](CONTRIBUTING.md) for contribution guidance.

## Discussions

Architecture questions, tutorial proposals, design alternatives, and broader technical discussion are encouraged through [ASI Backbone Organization Discussions](https://github.com/orgs/AsiBackbone/discussions).

Use Discussions for exploratory topics such as:

- Architecture questions
- Tutorial and lab proposals
- Alternative patterns
- Design debates
- Cross-repository integration ideas
- Community learning topics

Issues should generally be used for concrete repository work, while Discussions are better suited to open architectural questions and exploratory topics.

## Documentation

The Learning project uses **DocFX** for its published documentation, consistent with the other ASI Backbone organization repositories.

The repository Markdown files remain the canonical source material.

For local build, validation, and preview instructions, see [Building Documentation Locally](CONTRIBUTING.md#building-documentation-locally).

As the project develops, the documentation site is expected to provide a more structured learning experience while GitHub remains the primary collaboration and contribution surface.

## Scope and Boundaries

This repository is an educational and architectural resource.

It is not:

* A compliance certification
* A legal standard
* A security guarantee
* An AI model
* An AGI or ASI implementation
* A robotics controller
* A replacement for application-specific security review
* A requirement to use the AsiBackbone package
* A claim that one architecture is universally correct

Examples are intended to teach patterns.

Production systems remain responsible for their own authentication, authorization, persistence, infrastructure, safety controls, threat modeling, regulatory requirements, and operational execution.

## Project Status

**Active development — foundational tutorial, sample, test, and lab path established**

ASI Backbone Learning has moved beyond its initial repository scaffolding and now provides a five-tutorial foundational sequence covering the core governed-execution model:

1. [Decision Before Execution](docs/tutorials/decision-before-execution.md)
2. [Policy Context and Explicit Decision Outcomes](docs/tutorials/policy-context-and-explicit-decision-outcomes.md)
3. [Acknowledgment and Audit Residue](docs/tutorials/acknowledgment-and-audit-residue.md)
4. [Scoped Capability and Host-Owned Execution](docs/tutorials/scoped-capability-and-host-owned-execution.md)
5. [Governed AI Tool Gateway](docs/tutorials/governed-ai-tool-gateway.md)

Together, these tutorials establish the initial learning path from proposed intent through policy evaluation, acknowledgment, scoped authority, host-owned execution, and durable audit residue.

The learning path is now supported end to end:

* All five foundational tutorials have runnable companion samples.
* The samples include focused architectural-invariant tests.
* All five foundational topics have learner exercises or labs.
* DocFX publishes the documentation and executable-samples path.
* The first alternative-pattern comparison, [When ASP.NET Core Authorization Is Enough](docs/architecture/when-aspnet-core-authorization-is-enough.md), is published.
* Contribution, governance, citation, and licensing guidance are established.

Current development has shifted toward working implementation references, deeper labs, ASP.NET Core architecture, security/trust architecture, governance material, and additional architecture comparisons.

This remains a living educational project. Material may evolve as the implementation repositories mature, better teaching approaches emerge, and community feedback identifies new questions or gaps.

See [ROADMAP.md](ROADMAP.md) for planned work and longer-term direction.

## License

ASI Backbone Learning uses component-specific licensing.

Documentation, educational material, and diagrams are licensed under
**CC BY 4.0**.

Executable sample code under `samples/` is licensed under the
**MIT License**.

Source-code snippets embedded in documentation are additionally
available under the MIT License unless otherwise noted.

See [LICENSING.md](LICENSING.md) for the complete licensing policy.

Contributors should ensure that submitted code, diagrams, examples, and written material are compatible with the repository license.

---

**ASI Backbone Learning is not intended to provide doctrine. It is intended to provide patterns worth examining.**

Read them. Test them. Challenge them. Adapt them. Improve them.
