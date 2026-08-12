# Getting Started

Welcome to **ASI Backbone Learning**.

This section introduces the core ideas used throughout the Learning repository and provides a practical starting point for developers who want to understand the architecture before moving into tutorials, labs, or advanced topics.

The goal is not to require adoption of a specific framework. The goal is to make the architectural reasoning clear enough that you can evaluate, adapt, challenge, or reuse the patterns in your own systems.

## What You Will Learn

The Learning material is built around a recurring separation of responsibilities:

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

This flow is useful when an application performs operations where a simple request-to-execution path does not provide enough control, explanation, or evidence.

You will encounter several recurring concepts:

- **Intent** — what action is being proposed.
- **Policy context** — the facts needed to evaluate that proposal.
- **Constraints** — the rules or conditions that shape what is allowed.
- **Decision** — an explicit result such as allow, deny, defer, require acknowledgment, or escalate.
- **Acknowledgment** — an explicit boundary when a human or system must consciously accept a condition before proceeding.
- **Scoped authority** — narrow and temporary authority to perform a specific operation.
- **Host-owned execution** — the application or execution host remains responsible for performing the real-world action.
- **Audit residue** — structured evidence that explains what was proposed, how it was evaluated, and what happened afterward.

## Where to Begin

If you are new to the project, the recommended learning order is:

1. **Decision Before Execution**  
   Understand why a consequential operation should be represented as proposed intent before it becomes execution.

2. **Policy Context and Explicit Decision Outcomes**  
   Learn how decision inputs and outcomes can be modeled explicitly instead of being scattered across application code.

3. **Acknowledgment and Audit Residue**  
   Explore how workflows can pause for acknowledgment and preserve structured evidence of a decision.

4. **Scoped Capability and Host-Owned Execution**  
   Understand why approval does not necessarily imply broad or permanent authority, and why the host should retain final execution responsibility.

5. **Governed AI Tool Gateway**  
   Apply the earlier ideas to an AI-assisted workflow in which a model may propose an operation but does not own execution authority.

The tutorials will expand incrementally as the repository develops.

## The Core Boundary

A recurring design principle throughout this repository is:

> **The model may propose. The host retains execution authority.**

This principle is especially important in AI-assisted systems, but the underlying separation is broader than AI.

The same reasoning can apply to:

- Administrative operations
- Deployment workflows
- Infrastructure changes
- Sensitive data access
- Background jobs
- Human approval workflows
- API tool execution
- Multi-tenant policy decisions

The governance layer may determine whether an operation should proceed without becoming the component that performs the operation itself.

## Learning Is Problem-First

Tutorials in this repository should generally follow a progression like:

```text
Problem
   ↓
Common or naive implementation
   ↓
Failure mode or limitation
   ↓
Architectural pattern
   ↓
Minimal teaching example
   ↓
Tradeoffs and alternatives
   ↓
Working repository example
```

This keeps the material useful even if you never install the `AsiBackbone` package or use `NetCoreApplicationTemplate`.

The objective is to understand the architectural boundary first and the implementation second.

## Relationship to the Working Repositories

ASI Backbone Learning is the educational layer of the organization.

The working repositories provide fuller implementation examples:

### AsiBackbone

[`AsiBackbone/AsiBackbone`](https://github.com/AsiBackbone/AsiBackbone)

A .NET governance and policy-control framework that implements patterns such as:

- Policy evaluation
- Structured decision results
- Acknowledgment workflows
- Audit residue and provenance
- Capability-scoped authority
- Host-owned execution boundaries
- AI and application governance

### .NET Core Application Template

[`AsiBackbone/NetCoreApplicationTemplate`](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

An enterprise-oriented ASP.NET Core reference implementation demonstrating areas such as:

- Middleware organization
- Structured logging
- Security defaults
- Error handling
- Rate limiting
- Authentication-ready architecture
- Data access
- Architecture Decision Records

Learning uses those repositories as working architectural specimens while keeping its own examples intentionally smaller and easier to study.

## You Do Not Need to Adopt the Framework

A Learning tutorial should still be useful if you decide that the demonstrated pattern does not fit your application.

You are encouraged to:

- Reimplement a pattern differently.
- Compare it with another architecture.
- Remove unnecessary complexity.
- Identify cases where a simpler design is better.
- Challenge assumptions.
- Document alternative approaches.
- Use the working repositories only as reference material.

A pattern that survives criticism is more useful than one that is accepted without examination.

## Canonical and Alternative Patterns

Some Learning material may be identified as:

### Canonical Pattern

A pattern aligned with the current architecture of one or more ASI Backbone organization repositories.

### Alternative Pattern

A technically grounded approach that solves the same problem differently.

Canonical does not mean universally correct.

Alternative does not mean incorrect.

The purpose of the distinction is to help readers understand what the working repositories currently implement while preserving room for comparison and experimentation.

## Tutorials and Labs

The repository uses two complementary learning formats.

### Tutorials

Tutorials explain the problem, architecture, implementation approach, tradeoffs, and working references.

### Labs

Labs ask you to actively reason about an architecture.

A lab may provide:

- A partially implemented application
- A deliberately weak design
- Failing tests
- A policy-design exercise
- A governance scenario
- A security-boundary problem
- An architecture to critique or improve

Tutorials explain.

Labs make you decide.

## Scope and Boundaries

ASI Backbone Learning is an educational and architectural resource.

It is not:

- A compliance certification
- A legal standard
- A security guarantee
- An AI model
- An AGI or ASI implementation
- A robotics controller
- A replacement for application-specific security review
- A requirement to use any ASI Backbone package
- A claim that one architecture is universally correct

Examples are teaching artifacts.

Production systems remain responsible for their own authentication, authorization, infrastructure, persistence, safety controls, regulatory requirements, threat modeling, and operational execution.

## How to Participate

You can participate without writing framework code.

Useful contributions include:

- Questions
- Corrections
- Tutorials
- Labs
- Diagrams
- Alternative implementations
- Failure-mode analysis
- Architecture critiques
- Better examples
- Documentation improvements

See the repository contribution guidance for more information:

- [`CONTRIBUTING.md`](../../CONTRIBUTING.md)
- [`CODE_OF_CONDUCT.md`](../../CODE_OF_CONDUCT.md)
- [`GOVERNANCE.md`](../../GOVERNANCE.md)
- [`ROADMAP.md`](../../ROADMAP.md)

## Next Step

When you are ready, continue into the foundational tutorials beginning with **Decision Before Execution**.

That pattern establishes the main architectural boundary used throughout the rest of the Learning material:

> A proposed action should become a governed decision before it becomes real-world execution.

---

> **Read it. Run it. Question it. Improve it.**
