---
description: Start AsiBackbone Learning with core governed-execution concepts, the recommended learning path, practical examples, labs, and pattern-evaluation guidance.
---

# Getting Started

Welcome to **AsiBackbone Learning**.

This repository teaches governance and controlled-execution architecture through small explanations, runnable examples, invariant tests, and hands-on labs. You do not need to adopt the `AsiBackbone` package or any specific framework to use the material.

> **Read it. Run it. Question it. Improve it.**

> **Version boundary:** Learning 1.0 remains the historical AsiBackbone 6.0 baseline. Use the [AsiBackbone 7.0 Compatibility and API Boundary](asibackbone-7-api-boundary.md) for current implementation syntax; use the [Learning 1.0 and AsiBackbone 6.0 Compatibility Guide](learning-1-asibackbone-6-compatibility.md) for the versioned 6.0 record.

## Start Here

Choose the shortest path that matches how you want to learn:

| If you want to... | Start here |
|---|---|
| Understand the architecture from the beginning | [**Decision Before Execution**](../tutorials/decision-before-execution.md) |
| Solve a specific architecture problem | [**Find Your Path**](find-your-path.md) |
| See the complete curriculum visually | [**Learning Path Map**](learning-path-map.md) |
| Learn by running code | [**Executable Samples**](../samples/index.md) |
| Copy or prepare current AsiBackbone 7.0 API syntax | [**AsiBackbone 7.0 Compatibility and API Boundary**](asibackbone-7-api-boundary.md) |
| Maintain the historical Learning 1.0 / 6.0 contract or translate older 5.x material | [**Learning 1.0 and AsiBackbone 6.0 Compatibility Guide**](learning-1-asibackbone-6-compatibility.md) |
| Practice by changing or challenging the design | [**Hands-On Labs**](../labs/index.md) |
| Compare a simpler alternative | [**When ASP.NET Core Authorization Is Enough**](../architecture/when-aspnet-core-authorization-is-enough.md) |

New to the project? Start with **Decision Before Execution** and work through the five-part foundation in order. If you already know the problem you need to solve, **Find Your Path** can route you directly to the most relevant material.

## The Architecture in 30 Seconds

The Learning material repeatedly separates **proposing an action** from **authorizing and executing it**:

```text
Proposed Intent
   ↓
Policy Context
   ↓
Constraints
   ↓
Explicit Decision
   ↓
Acknowledgment when required
   ↓
Scoped Authority
   ↓
Host-Owned Execution
   ↓
Audit Evidence
```

The two boundaries to remember are:

> **Acknowledgment ≠ Authorization ≠ Execution Authority**

> **The model may propose. The host retains execution authority.**

In practice, this means the governance layer can determine whether an operation should proceed without becoming the component that performs the real-world operation itself.

### Core Terms

- **Intent** — the action being proposed.
- **Policy context** — the facts needed to evaluate that proposal.
- **Constraints** — the rules or conditions that shape what is allowed.
- **Decision** — an explicit outcome such as allow, deny, defer, require acknowledgment, or escalate.
- **Acknowledgment** — a deliberate acceptance boundary when one is required before proceeding.
- **Scoped authority** — narrow, temporary authority to perform a specific operation.
- **Host-owned execution** — the application or execution host performs the consequential action.
- **Audit evidence** — structured records explaining what was proposed, evaluated, authorized, and executed.

## Five-Part Foundation

The foundation is deliberately progressive. Each topic adds one boundary to the same governed-execution model.

| Step | Topic | Core invariant | Continue with |
|---|---|---|---|
| 1 | [**Decision Before Execution**](../tutorials/decision-before-execution.md) | Denied decision → no execution | [Lab](../labs/decision-before-execution.md) |
| 2 | [**Policy Context and Explicit Decision Outcomes**](../tutorials/policy-context-and-explicit-decision-outcomes.md) | Decisions are explicit, not boolean-only | [Lab](../labs/policy-context-and-explicit-decision-outcomes.md) |
| 3 | [**Decision Receipts and Acknowledgment**](../tutorials/decision-receipts-and-acknowledgment.md) | Acknowledgment does not grant execution authority | [Lab](../labs/decision-receipts-and-acknowledgment.md) |
| 4 | [**Scoped Capability and Host-Owned Execution**](../tutorials/scoped-capability-and-host-owned-execution.md) | Expired or stale authority blocks execution | [Lab](../labs/scoped-capability-and-host-owned-execution.md) |
| 5 | [**Governed AI Tool Gateway**](../tutorials/governed-ai-tool-gateway.md) | Unknown or unauthorized AI tool proposal → no execution | [Lab](../labs/governed-ai-tool-gateway.md) |

The fifth topic is the capstone. It combines intent, policy context, explicit decisions, acknowledgment, scoped authority, host-owned execution, and audit evidence into one governed AI-assisted workflow.

## How Learning Works

Each foundational topic is reinforced across four complementary forms:

```text
Tutorial
   ↓
Runnable Sample
   ↓
Architectural Invariant Tests
   ↓
Hands-On Lab
```

**Tutorials explain. Samples demonstrate. Tests verify. Labs make you decide.**

### Tutorials

Tutorials begin with the problem, show the common or naive implementation, expose its failure mode, and then introduce the architectural pattern, tradeoffs, alternatives, and working repository references.

### Samples

The [`samples/`](../samples/index.md) area contains intentionally small .NET teaching implementations. They favor deterministic local behavior, explicit execution boundaries, focused domain examples, and simulated side effects where appropriate.

They are teaching artifacts rather than production frameworks.

Their local types are not package API signatures. Use the [AsiBackbone 7.0 Compatibility and API Boundary](asibackbone-7-api-boundary.md) when you need exact current namespaces, supported construction paths, security changes, or migration guidance.

### Tests

Sample tests make important architectural claims repeatable and observable. Typical invariants include:

```text
Denied Decision → No Execution
Expired Capability → Execution Blocked
Unknown AI Tool → Proposal Rejected → No Execution
```

Their purpose is architectural verification, not broad code-coverage demonstration.

### Labs

[Hands-On Labs](../labs/index.md) ask you to modify, critique, repair, or extend the design. Exercises may require you to identify hidden side effects, separate evaluation from execution, preserve acknowledgment boundaries, validate scoped authority, detect stale authority, threat-model an AI tool gateway, or compare alternatives.

## Where the Pattern Applies

The core separation is broader than AI. The same reasoning can help with:

- administrative operations,
- deployment workflows,
- infrastructure changes,
- sensitive data access,
- background jobs,
- human approval workflows,
- API tool execution,
- and multi-tenant policy decisions.

Use the smallest architecture that preserves the boundaries you actually need. In some applications, ordinary ASP.NET Core authorization is enough; in others, the proposal → decision → scoped authority → execution separation adds useful control and evidence.

## Working Repository References

AsiBackbone Learning is the educational layer of the organization. The working repositories provide fuller implementation examples:

- [`AsiBackbone/AsiBackbone`](https://github.com/AsiBackbone/AsiBackbone) — a .NET governance and policy-control framework covering policy evaluation, structured decisions, acknowledgment workflows, audit/provenance, capability-scoped authority, host-owned execution, and AI/application governance.
- [`AsiBackbone/NetCoreApplicationTemplate`](https://github.com/AsiBackbone/NetCoreApplicationTemplate) — an enterprise-oriented ASP.NET Core reference implementation demonstrating middleware organization, structured logging, security defaults, error handling, rate limiting, authentication-ready architecture, data access, and Architecture Decision Records.

Learning uses these repositories as architectural specimens while keeping its teaching examples intentionally smaller and easier to study.

## Adoption Is Optional

The goal is to make the reasoning clear enough that you can evaluate, adapt, challenge, or reuse the patterns in your own systems.

You are encouraged to reimplement a pattern differently, remove unnecessary complexity, identify cases where a simpler design is better, compare competing architectures, and use the working repositories only as reference material.

Learning may describe a pattern as **canonical** when it aligns with the current organization repositories, or **alternative** when it solves the same problem differently. Canonical does not mean universally correct, and alternative does not mean incorrect.

For a concrete comparison, see [**When ASP.NET Core Authorization Is Enough**](../architecture/when-aspnet-core-authorization-is-enough.md).

## Scope and Boundaries

AsiBackbone Learning is an educational and architectural resource. It is **not** a compliance certification, legal standard, security guarantee, AI model, AGI/ASI implementation, robotics controller, or substitute for application-specific security review.

Production systems remain responsible for their own authentication, authorization, infrastructure, persistence, safety controls, regulatory requirements, threat modeling, and operational execution.

## Where to Go Next

After completing the foundation, continue into the area that matches your problem:

- [**Architecture**](../architecture/index.md) — patterns, comparisons, boundaries, and system structure.
- [**ASP.NET Core**](../aspnetcore/index.md) — application-level integration and implementation concerns.
- [**Security**](../security/index.md) — trust boundaries, failure modes, and defensive architecture.
- [**AI Integration**](../ai-integration/index.md) — governed AI-assisted workflows and tool execution.
- [**Advanced**](../advanced/index.md) — deeper or more experimental material.
- [**ROADMAP.md**](https://github.com/AsiBackbone/Learning/blob/main/ROADMAP.md) — current priorities and longer-term direction.

The repository intentionally favors **depth before breadth**: a well-connected tutorial with runnable code, meaningful tests, a useful lab, and clear implementation references is more valuable than several disconnected pages.

## Participate

You can contribute without writing framework code. Questions, corrections, tutorials, labs, diagrams, alternative implementations, failure-mode analysis, architecture critiques, better examples, and documentation improvements are all useful.

- [CONTRIBUTING.md](https://github.com/AsiBackbone/Learning/blob/main/CONTRIBUTING.md)
- [CODE_OF_CONDUCT.md](https://github.com/AsiBackbone/Learning/blob/main/CODE_OF_CONDUCT.md)
- [GOVERNANCE.md](https://github.com/AsiBackbone/Learning/blob/main/GOVERNANCE.md)
- [ROADMAP.md](https://github.com/AsiBackbone/Learning/blob/main/ROADMAP.md)

## Next Step

Begin with [**Decision Before Execution**](../tutorials/decision-before-execution.md), then follow the foundation through [**Governed AI Tool Gateway**](../tutorials/governed-ai-tool-gateway.md).

If you already know what you need, use [**Find Your Path**](find-your-path.md) instead.

---

> **Read it. Run it. Question it. Improve it.**
