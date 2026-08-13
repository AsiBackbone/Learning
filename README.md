# ASI Backbone Learning

[![Documentation Validation](https://github.com/AsiBackbone/Learning/actions/workflows/docs-validation.yml/badge.svg?branch=main)](https://github.com/AsiBackbone/Learning/actions/workflows/docs-validation.yml)

A community-maintained living tutorial for practical .NET architecture, governed execution, policy-driven systems, secure application design, AI integration, and related architectural patterns.

`AsiBackbone/Learning` is the educational layer of the ASI Backbone organization.

The goal is not to create another framework or package. The goal is to explain architectural ideas clearly, demonstrate them through focused examples, examine their tradeoffs, and connect those lessons to working implementations in the organization's existing repositories.

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

## Proposed Repository Structure

The repository is expected to evolve, but an initial structure may resemble:

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

**Active development — foundational learning path established**

ASI Backbone Learning has moved beyond its initial repository scaffolding and now provides a foundational tutorial sequence covering the core governed-execution model:

1. [Decision Before Execution](docs/tutorials/decision-before-execution.md)
2. [Policy Context and Explicit Decision Outcomes](docs/tutorials/policy-context-and-explicit-decision-outcomes.md)
3. [Acknowledgment and Audit Residue](docs/tutorials/acknowledgment-and-audit-residue.md)
4. [Scoped Capability and Host-Owned Execution](docs/tutorials/scoped-capability-and-host-owned-execution.md)
5. [Governed AI Tool Gateway](docs/tutorials/governed-ai-tool-gateway.md)

Together, these tutorials establish the initial learning path from proposed intent through policy evaluation, acknowledgment, scoped authority, host-owned execution, and durable audit residue.

The repository also includes its DocFX documentation foundation, contribution and governance guidance, and a dedicated `samples/` area for executable companion code.

Current development is shifting from establishing the conceptual foundation toward making the material more runnable, interactive, and extensible. Near-term work includes:

* Executable companion samples paired with tutorials.
* Beginner and intermediate hands-on labs.
* Additional ASP.NET Core, security, governance, and AI-integration material.
* Stronger links between teaching examples and working implementations in `AsiBackbone` and `NetCoreApplicationTemplate`.
* Alternative patterns, tradeoff analysis, and community-contributed examples.

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
