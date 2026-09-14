---
description: Explore software architecture boundaries, responsibilities, failure modes, and tradeoffs for governed systems without prescribing one universal design.
---

# Architecture

The Architecture section examines the boundaries, responsibilities, failure modes, and tradeoffs behind governed software systems.

The goal is not to prescribe one universal design. The goal is to make important boundaries visible enough that you can decide when a governance pattern is justified and when a simpler architecture is better.

> **Good architecture makes important boundaries visible.**

## Start by Question

| If you want to understand... | Start here |
| --- | --- |
| Learning terminology | [Architecture Glossary](glossary.md) |
| How Learning terms relate to established concepts | [Terminology and Established Architecture Concepts](terminology-and-established-concepts.md) |
| The overall governance-spine concept | [Accountable Systems Infrastructure and Governed Execution](accountable-systems-infrastructure-and-governed-execution.md) |
| Why proposal and side effect should be separated | [Intent to Execution: An Accountability Pattern](intent-to-execution-accountability-pattern.md) |
| How active constraints shape decisions | [Constraint-Conditioned Decision Model](constraint-conditioned-decision-model.md) |
| How adjacent governance mechanisms compose | [Governance Tool Selection and Composition](governance-tool-selection-and-composition.md) |
| The architecture visually | [Governance Spine and Capability Validation Diagrams](governance-spine-and-capability-validation-diagrams.md) |
| When ASP.NET Core authorization is already enough | [When ASP.NET Core Authorization Is Enough](when-aspnet-core-authorization-is-enough.md) |
| When a simple application service is enough | [When a Simple Application Service Is Enough](when-a-simple-application-service-is-enough.md) |
| How application structure should grow | [Growing Beyond a Simple Application Structure](growing-beyond-a-simple-application-structure.md) |

## Core Boundary

The foundational material repeatedly separates these responsibilities:

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

This makes it easier to answer six questions:

1. Who proposes the operation?
2. Which facts influence the decision?
3. Where is policy evaluated?
4. What authority exists after approval?
5. Which component performs the real-world side effect?
6. What evidence remains afterward?

If those questions are already answered clearly by a simpler design, additional governance machinery may not be necessary.

## Pattern Classifications

Substantive pages may use a visible `Pattern classification` when architectural status changes how the material should be interpreted:

| Status | Meaning |
| --- | --- |
| **Canonical Pattern** | Aligns with the current architecture of one or more ASI Backbone organization repositories. |
| **Alternative Pattern** | Presents a viable different approach or intentionally departs from the canonical organization pattern. |
| **Experimental** | Explores architecture that is not presented as an established organization pattern or production-ready design. |
| **General learning material** | Teaches useful architecture without making a stronger canonical, alternative, or experimental claim. |

These are descriptive labels, not rankings. Canonical does not mean universally correct, and experimental does not mean low quality.

## Foundational Organization Concepts

| Concept | What it helps you reason about |
| --- | --- |
| [Accountable Systems Infrastructure and Governed Execution](accountable-systems-infrastructure-and-governed-execution.md) | The stack-neutral meaning of the governance-spine idea |
| [Intent to Execution: An Accountability Pattern](intent-to-execution-accountability-pattern.md) | The accountability gap between proposal and side effect |
| [Constraint-Conditioned Decision Model](constraint-conditioned-decision-model.md) | How active constraints narrow an intent toward an outcome |
| [Governance Tool Selection and Composition](governance-tool-selection-and-composition.md) | How adjacent governance mechanisms protect different boundaries without becoming substitutes |

These pages are educational. Concrete package, API, configuration, compatibility, security, and release behavior remains authoritative in the implementation repositories.

## Foundational Learning Path

If these boundaries are new, use the five tutorials in order:

1. [Decision Before Execution](../tutorials/decision-before-execution.md)
2. [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md)
3. [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md)
4. [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md)
5. [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md)

The sequence moves from one execution boundary toward an end-to-end governed workflow.

## Compare Adjacent Architectures

Architecture should be compared against viable alternatives rather than presented as one prescribed design.

| Comparison | Main question |
| --- | --- |
| [When ASP.NET Core Authorization Is Enough](when-aspnet-core-authorization-is-enough.md) | Is framework-native endpoint or resource authorization already sufficient? |
| [Role-Based, Claims-Based, and Capability-Based Authorization](role-based-claims-based-and-capability-based-authorization.md) | When should authority come from roles, claims, narrow capabilities, or a composition of them? |
| [API Gateways, Service Meshes, Zero Trust, and Governed Execution](api-gateways-service-meshes-zero-trust-and-governed-execution.md) | Which concerns belong to transport, workload identity, infrastructure security, or application decision semantics? |
| [When a Simple Application Service Is Enough](when-a-simple-application-service-is-enough.md) | Can one immediate application-service workflow preserve the required boundaries without a broader lifecycle? |
| [Workflow Engines, Human Approval Systems, and Governed Execution](workflow-engines-human-approval-and-governed-execution.md) | Does durable orchestration already provide the process and approval semantics you need? |
| [Policy Engines, Rules Engines, and Distributed Policy Enforcement](policy-engines-rules-engines-and-distributed-policy-enforcement.md) | Where should domain rules, external policy decisions, PDPs, and PEPs live? |
| [Agent and Tool Authorization Models and Host-Owned Execution](agent-and-tool-authorization-models-and-host-owned-execution.md) | When are framework-native agent/tool controls enough, and when is a separate execution-authority boundary justified? |
| [Event Sourcing, Audit Trails, and Governance Decision Provenance](event-sourcing-audit-trails-and-governance-decision-provenance.md) | What evidence problem are logs, audit history, decision receipts, and event sourcing each solving? |
| [CQRS, Command/Query Separation, and Governed Execution](cqrs-command-query-separation-and-governed-execution.md) | When is a command handler already the correct host-owned execution boundary? |

The purpose is not to make adjacent approaches compete. It is to expose their different responsibilities, trust boundaries, and operational costs.

## Application Structure Growth

For general layering guidance, see [Growing Beyond a Simple Application Structure](growing-beyond-a-simple-application-structure.md). It covers when a compact application is enough, what signals justify Application or Domain boundaries, dependency direction, and tradeoffs around CQRS, MediatR, DDD, and premature layering.

## Working Architecture References

Learning uses the organization's implementation repositories as architectural specimens:

- [AsiBackbone/AsiBackbone](https://github.com/AsiBackbone/AsiBackbone) — a .NET governance and policy-control framework demonstrating structured decisions, acknowledgment workflows, audit residue, scoped capabilities, and host-owned execution boundaries.
- [AsiBackbone/NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate) — an ASP.NET Core reference architecture demonstrating middleware organization, secure defaults, logging, error handling, rate limiting, authentication-ready design, and production-oriented application structure.

For the current learning path, continue with the [Foundational Tutorials](../tutorials/index.md).

---

> **Read it. Run it. Question it. Improve it.**