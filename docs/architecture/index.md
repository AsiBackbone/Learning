# Architecture

The Architecture section explores the structural boundaries, responsibilities, and tradeoffs behind governed software systems.

The goal is not to prescribe one universal architecture.

Instead, this section examines why particular boundaries exist, what problems they address, how they can fail, and when a simpler design may be preferable.

> **Good architecture makes important boundaries visible.**

## Current Focus

The current foundational material emphasizes separation among:

```text
Intent
   ↓
Policy Context
   ↓
Governance Decision
   ↓
Acknowledgment when required
   ↓
Scoped Authority
   ↓
Host-Owned Execution
   ↓
Audit Residue
```

This separation makes it easier to reason about:

* Who proposes an operation.
* Which facts influence a decision.
* Where policy is evaluated.
* What authority exists after approval.
* Which component performs the real-world side effect.
* What evidence remains afterward.

## Start with the Foundational Tutorials

If you are new to these architectural ideas, begin with:

* [Decision Before Execution](../tutorials/decision-before-execution.md)
* [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md)
* [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md)
* [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md)
* [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md)

The sequence moves from a basic execution boundary toward an end-to-end governed workflow.

## Architectural Questions

Future material in this section may examine questions such as:

* Where should governance decisions occur?
* Which components should remain independent?
* How should policy evaluation relate to authorization?
* When should acknowledgment interrupt a workflow?
* How narrowly should execution authority be scoped?
* How should failure, retry, replay, and cancellation affect authority?
* Which architectural concerns belong in the host rather than a framework?
* When is a governance pipeline unnecessary complexity?

## Alternative Patterns

Architecture should be compared against viable alternatives rather than presented as a single prescribed design.

Start with:

* [When ASP.NET Core Authorization Is Enough](when-aspnet-core-authorization-is-enough.md) — compares the governed-execution model with ASP.NET Core policies, requirements, handlers, and resource-based authorization, including cases where the built-in authorization model is the simpler and better choice.

The purpose of these comparisons is not to make adjacent approaches compete. It is to make their different responsibilities, trust boundaries, and operational costs visible.

## Working Architecture References

Learning uses the organization's implementation repositories as architectural specimens:

### AsiBackbone

[AsiBackbone/AsiBackbone](https://github.com/AsiBackbone/AsiBackbone)

A .NET governance and policy-control framework demonstrating structured decisions, acknowledgment workflows, audit residue, scoped capabilities, and host-owned execution boundaries.

### NetCoreApplicationTemplate

[AsiBackbone/NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

An ASP.NET Core reference architecture demonstrating middleware organization, secure defaults, logging, error handling, rate limiting, authentication-ready design, and production-oriented application structure.

## Current Status

The Architecture section is established as a learning area and will grow as architectural comparisons, diagrams, alternative patterns, and cross-repository studies are added.

For the current learning path, continue with the [Foundational Tutorials](../tutorials/index.md).

---

> **Read it. Run it. Question it. Improve it.**