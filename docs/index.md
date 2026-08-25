---
description: Practical .NET architecture tutorials, labs, and reference patterns for governed execution, secure applications, AI integration, and policy-driven systems.
---

# ASI Backbone Learning

**Practical software architecture for governed execution, secure applications, AI integration, and policy-driven systems.**

ASI Backbone Learning is the educational layer of the ASI Backbone organization.

In this project, **ASI** means **Accountable Systems Infrastructure**. ASI Backbone Learning focuses on software architecture, governed execution, secure .NET applications, and AI integration; it is not affiliated with the Artificial Superintelligence Alliance.

It exists to explain architectural ideas clearly, demonstrate them with focused examples, examine their tradeoffs, and connect those lessons to fuller working implementations.

> **Read it. Run it. Question it. Improve it.**

## Start Here

New to the project?

Begin with [Getting Started](getting-started/index.md) for an introduction to the core concepts and recommended learning path.

Already have a concrete problem in mind? Use [Find Your Path](getting-started/find-your-path.md) to choose a short, goal-specific route through the existing material without reading the repository in sequence.

If the repository's vocabulary is new to you, use [Terminology and Established Architecture Concepts](architecture/terminology-and-established-concepts.md) to map Learning terms to established authorization, policy, capability, provenance, workflow, and mediation concepts.

Already using ASP.NET Core authorization and unsure whether you need a broader governed-execution model? Read [When ASP.NET Core Authorization Is Enough](architecture/when-aspnet-core-authorization-is-enough.md). It presents the simpler framework-native approach first and explains where the architectural problem becomes larger than authorization.

Learning does not require installing an `AsiBackbone` package. The material is intended to be useful as independent .NET architecture education.

Looking for standalone technical writing rather than a curriculum path? Browse [Articles](articles/index.md). Articles are designed for direct external discovery and keep permanent `/articles/<year>/<slug>` publication URLs.

The material is organized around a recurring separation of responsibilities:

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

The central idea is simple:

> **A proposed action should become a governed decision before it becomes real-world execution.**

## Choose a Learning Path

### Architecture

Explore the boundaries, responsibilities, tradeoffs, and structural patterns behind governed systems.

[Explore Architecture →](architecture/index.md)

### Governance

Learn how intent, policy context, constraints, explicit decision outcomes, acknowledgment, scoped authority, and audit evidence fit together.

[Explore Governance →](governance/index.md)

### ASP.NET Core

Study practical application architecture, secure defaults, middleware organization, operational structure, and implementation patterns for modern .NET applications.

[Explore ASP.NET Core →](aspnetcore/index.md)

### Security

Examine security boundaries, least authority, explicit control flow, defensive defaults, and architecture that reduces accidental privilege.

[Explore Security →](security/index.md)

### AI Integration

Apply governed-execution principles to AI-assisted systems, tool calls, agents, workflows, and host-controlled execution.

> **The model may propose. The host retains execution authority.**

[Explore AI Integration →](ai-integration/index.md)

### Tutorials

Follow focused, problem-first lessons that move from a common implementation through failure modes, architectural patterns, tradeoffs, and working references.

[Browse Tutorials →](tutorials/index.md)

### Executable Samples

Move from architectural explanation toward runnable demonstrations of the same boundaries.

The foundational sample set now contains five intentionally small .NET companion implementations, one for each foundational tutorial, with focused tests that make the architectural invariants observable.

[Browse Executable Samples →](samples/index.md)

### Labs

Move from reading to reasoning with hands-on exercises, incomplete implementations, architecture critiques, policy scenarios, and design challenges.

[Browse Labs →](labs/index.md)

### Reference Architecture Case Studies

See several Learning boundaries composed inside realistic, simulated scenarios without treating the specimen as a production framework or prescribed application design.

[Browse Reference Architecture Case Studies →](case-studies/index.md)

### Advanced

Explore deeper architectural questions, alternative approaches, complex integration patterns, and topics that build on the foundational material.

[Explore Advanced Topics →](advanced/index.md)

## How Learning Works

Tutorials generally follow a problem-first progression:

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

The purpose is not to prove that one framework or architecture is always correct.

The purpose is to make the reasoning visible.

You are encouraged to:

- Study a pattern without adopting an entire framework.
- Reimplement it differently.
- Compare it with another architecture.
- Identify cases where a simpler design is better.
- Challenge assumptions.
- Document alternative approaches.
- Use working repositories as architectural specimens rather than unquestioned templates.

## The Working Repositories

Learning connects concepts to two complementary implementation projects.

### ASI Backbone

[AsiBackbone/AsiBackbone](https://github.com/AsiBackbone/AsiBackbone)

A .NET governance and policy-control framework for explicit, auditable, policy-governed decision pipelines.

It provides working implementations of concepts such as policy evaluation, structured decision results, acknowledgment workflows, audit residue, capability-scoped authority, and host-owned execution boundaries.

### .NET Core Application Template

[AsiBackbone/NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

An enterprise-oriented ASP.NET Core reference application demonstrating secure-by-default application architecture, structured logging, middleware organization, error handling, rate limiting, authentication-ready design, data-access patterns, and operational structure.

Learning uses both repositories as working examples while keeping educational material smaller, more focused, and easier to question.

## Canonical Does Not Mean Universal

Some material may describe a **canonical pattern** aligned with the current ASI Backbone repositories.

Other material may present an **alternative pattern** that solves the same problem differently.

Both are useful.

A canonical approach documents what the working implementations currently do.

An alternative approach creates room for comparison, criticism, experimentation, and improvement.

Architectural disagreement can be educational.

## What This Project Is Not

ASI Backbone Learning is an educational and architectural resource.

It is not:

- A compliance certification
- A legal standard
- A security guarantee
- An AI model
- An AGI or ASI implementation
- A robotics controller
- A replacement for application-specific security review
- A requirement to use the AsiBackbone package
- A claim that one architecture is universally correct

Examples are teaching artifacts. Production systems remain responsible for their own security, infrastructure, persistence, regulatory requirements, safety controls, and operational execution.

## Project Status

**Living project — active development**

Learning is expected to grow incrementally through focused tutorials, labs, diagrams, architectural comparisons, community questions, and practical experimentation.

The goal is not to publish a large textbook all at once.

The goal is to build a useful body of architectural knowledge one well-examined pattern at a time.

---

**Start with [Getting Started](getting-started/index.md), then follow the path that best matches the problem you want to understand.**
