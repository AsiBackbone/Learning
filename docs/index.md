# ASI Backbone Learning

**Practical software architecture for governed execution, secure applications, AI integration, and policy-driven systems.**

ASI Backbone Learning is the educational layer of the ASI Backbone organization.

It exists to explain architectural ideas clearly, demonstrate them with focused examples, examine their tradeoffs, and connect those lessons to fuller working implementations.

> **Read it. Run it. Question it. Improve it.**

## Start Here

New to the project?

Begin with [Getting Started](getting-started/index.md) for an introduction to the core concepts and recommended learning path.

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

### Labs

Move from reading to reasoning with hands-on exercises, incomplete implementations, architecture critiques, policy scenarios, and design challenges.

[Browse Labs →](labs/index.md)

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
