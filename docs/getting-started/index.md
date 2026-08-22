---
description: Start ASI Backbone Learning with core governed-execution concepts, the recommended learning path, practical examples, labs, and pattern-evaluation guidance.
---

# Getting Started

Welcome to **ASI Backbone Learning**.

This section introduces the core ideas used throughout the Learning repository and provides a practical starting point for developers who want to understand the architecture before moving into deeper tutorials, samples, labs, comparisons, or advanced topics.

The goal is not to require adoption of a specific framework. The goal is to make the architectural reasoning clear enough that you can evaluate, adapt, challenge, or reuse the patterns in your own systems.

> **Read it. Run it. Question it. Improve it.**

Already know the problem you need to solve? [**Find Your Path**](find-your-path.md) routes common reader goals to the shortest relevant sequence of tutorials, samples, labs, simpler alternatives, and deeper references.

Use the problem-oriented path chooser when you do not need to work through the foundational sequence from the beginning.

Prefer a visual curriculum overview? [**Learning Path Map**](learning-path-map.md) shows the recommended foundational sequence, optional problem-first entry points, branch-specific advanced paths, and where hands-on practice reinforces the curriculum.

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

## The Foundational Learning Path

The initial five-part learning path is now established across four complementary forms:

```text
Tutorial
   ↓
Runnable Sample
   ↓
Architectural Invariant Tests
   ↓
Hands-On Lab
```

Tutorials explain the reasoning.

Samples make the boundary observable.

Tests make the architectural contract executable.

Labs require the learner to modify, critique, repair, or extend the architecture.

If you are new to the project, work through the five foundational topics in order.

### 1. Decision Before Execution

[**Read the tutorial**](../tutorials/decision-before-execution.md)

Understand why a consequential operation should be represented as proposed intent before it becomes execution.

The central invariant is simple:

```text
Denied Decision
   ↓
Executor Invocation Count = 0
```

After the tutorial:

- [Browse the executable samples](../samples/index.md)
- [Complete the Decision Before Execution lab](../labs/decision-before-execution.md)

### 2. Policy Context and Explicit Decision Outcomes

[**Read the tutorial**](../tutorials/policy-context-and-explicit-decision-outcomes.md)

Learn how decision inputs and outcomes can be modeled explicitly instead of being scattered across application code.

The lesson moves beyond a boolean result and makes outcomes such as these explicit:

```text
Allow
Deny
Defer
RequireAcknowledgment
Escalate
```

After the tutorial:

- [Browse the executable samples](../samples/index.md)
- [Complete the Policy Context and Explicit Decision Outcomes lab](../labs/policy-context-and-explicit-decision-outcomes.md)

### 3. Acknowledgment and Audit Residue

[**Read the tutorial**](../tutorials/acknowledgment-and-audit-residue.md)

Explore how workflows can pause for acknowledgment while preserving structured evidence of the governed path.

The material preserves the distinction:

```text
Acknowledgment
   ≠
Authorization
   ≠
Execution Authority
```

After the tutorial:

- [Browse the executable samples](../samples/index.md)
- [Complete the Acknowledgment and Audit Residue lab](../labs/acknowledgment-and-audit-residue.md)

### 4. Scoped Capability and Host-Owned Execution

[**Read the tutorial**](../tutorials/scoped-capability-and-host-owned-execution.md)

Understand why approval does not necessarily imply broad or permanent authority, and why the host should retain final execution responsibility.

The companion material makes boundaries such as these observable:

```text
Expired Capability
   ↓
Execution Blocked
```

```text
Resource Changed After Approval
   ↓
Capability Validation Fails
```

After the tutorial:

- [Browse the executable samples](../samples/index.md)
- [Complete the Scoped Capability and Host-Owned Execution lab](../labs/scoped-capability-and-host-owned-execution.md)

### 5. Governed AI Tool Gateway

[**Read the tutorial**](../tutorials/governed-ai-tool-gateway.md)

Apply the earlier ideas to an AI-assisted workflow in which a model may propose an operation but does not own execution authority.

The central rule is:

> **The model may propose. The host retains execution authority.**

A representative invariant is:

```text
AI Proposes Unknown Tool
   ↓
Host Rejects Proposal
   ↓
No Execution
```

After the tutorial:

- [Browse the executable samples](../samples/index.md)
- [Complete the Governed AI Tool Gateway lab](../labs/governed-ai-tool-gateway.md)

The fifth topic serves as the capstone for the foundational sequence because it composes intent, context, explicit decisions, acknowledgment, scoped authority, host-owned execution, and audit residue into one governed flow.

## How to Use the Executable Samples

The `samples/` area contains intentionally small .NET companion implementations for the five foundational tutorials.

The samples are teaching artifacts rather than production frameworks.

They are designed to make architectural boundaries visible through:

- deterministic local behavior,
- explicit execution boundaries,
- focused domain examples,
- dry-run or simulated side effects where appropriate,
- and tests for important architectural invariants.

Use the published sample guide to choose the relevant executable companion:

[**Browse Executable Samples**](../samples/index.md)

From there, you can move to the canonical sample README and source code in the repository.

## How to Use the Tests

The sample projects include focused tests intended to demonstrate architectural behavior rather than only object construction.

Examples include:

```text
Denied Decision
   ↓
No Execution
```

```text
Expired Capability
   ↓
Execution Blocked
```

```text
Unknown AI Tool
   ↓
Proposal Rejected
   ↓
No Execution
```

The purpose of these tests is not broad code coverage.

The purpose is to make important architectural claims independently observable and repeatable.

## How to Use the Labs

The labs move beyond reading and demonstration.

They ask you to reason about the architecture by working with incomplete, deliberately weak, or challenge-oriented scenarios.

A lab may ask you to:

- identify hidden side effects,
- separate evaluation from execution,
- introduce explicit decision outcomes,
- preserve acknowledgment boundaries,
- add or validate scoped execution authority,
- detect stale authority,
- threat-model an AI tool gateway,
- or compare alternative designs.

[**Browse Hands-On Labs**](../labs/index.md)

Tutorials explain.

Samples demonstrate.

Tests verify.

Labs make you decide.

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

Tutorials in this repository generally follow a progression like:

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

The working repositories provide fuller implementation examples.

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

The first published alternative-pattern comparison is:

- [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md)

A useful principle is:

> **Use the smallest architecture that preserves the boundaries you actually need.**

## Where the Project Goes Next

The foundational tutorial → sample → test → lab path is established.

Current development is moving toward:

- stronger links to fuller working implementations,
- deeper diagnostic and failure-mode labs,
- ASP.NET Core architecture lessons,
- security and trust architecture,
- broader governance and policy architecture,
- additional alternative-pattern comparisons,
- reference-architecture case studies,
- and clearly labeled advanced or experimental material.

The repository intentionally favors **depth before breadth**.

A well-connected tutorial with runnable code, meaningful tests, a useful lab, and clear implementation references is more valuable than several disconnected pages of new material.

See [ROADMAP.md](https://github.com/AsiBackbone/Learning/blob/main/ROADMAP.md) for current priorities and longer-term direction.

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

- [CONTRIBUTING.md](https://github.com/AsiBackbone/Learning/blob/main/CONTRIBUTING.md)
- [CODE_OF_CONDUCT.md](https://github.com/AsiBackbone/Learning/blob/main/CODE_OF_CONDUCT.md)
- [GOVERNANCE.md](https://github.com/AsiBackbone/Learning/blob/main/GOVERNANCE.md)
- [ROADMAP.md](https://github.com/AsiBackbone/Learning/blob/main/ROADMAP.md)

## Next Step

Begin with [**Decision Before Execution**](../tutorials/decision-before-execution.md).

Then follow the foundational sequence through the related samples, tests, and labs until you reach [**Governed AI Tool Gateway**](../tutorials/governed-ai-tool-gateway.md), which composes the earlier patterns into one end-to-end example.

After completing the foundation, explore the broader [Architecture](../architecture/index.md), [ASP.NET Core](../aspnetcore/index.md), [Security](../security/index.md), [AI Integration](../ai-integration/index.md), and [Advanced](../advanced/index.md) learning areas.

---

> **Read it. Run it. Question it. Improve it.**
