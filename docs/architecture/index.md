---
description: Explore software architecture boundaries, responsibilities, failure modes, and tradeoffs for governed systems without prescribing one universal design.
---

# Architecture

The Architecture section explores the structural boundaries, responsibilities, and tradeoffs behind governed software systems.

The goal is not to prescribe one universal architecture.

Instead, this section examines why particular boundaries exist, what problems they address, how they can fail, and when a simpler design may be preferable.

> **Good architecture makes important boundaries visible.**

## Terminology and Lineage

Learning uses a consistent vocabulary for recurring boundaries, but that vocabulary is not a claim that the underlying architectural ideas originated with ASI Backbone.

Start with the [Architecture Glossary](glossary.md) for canonical Learning definitions of terms such as intent, policy context, decision outcome, acknowledgment, audit residue, scoped capability, execution authority, tool proposal, and trust boundary.

Then use [Terminology and Established Architecture Concepts](terminology-and-established-concepts.md) to connect Learning vocabulary to established software architecture, authorization, security, workflow, provenance, and AI-governance concepts.

## Architectural Status Labels

Substantive pages use a visible `Pattern classification` line when architectural status changes how the material should be interpreted:

| Status | Meaning |
| --- | --- |
| **Canonical Pattern** | Aligns with the current architecture of one or more ASI Backbone organization repositories. |
| **Alternative Pattern** | Presents a viable different approach or a comparison that intentionally departs from the canonical organization pattern. |
| **Experimental** | Explores architecture that is not presented as an established organization pattern or production-ready design. |
| **General learning material** | Teaches useful architecture without making a stronger canonical, alternative, or experimental claim. |

Not every page needs a classification. The labels are descriptive rather than rankings: canonical does not mean universally correct, and experimental does not mean low quality.

## Foundational Organization Concepts

For the broad organization-level concepts that previously appeared inside product documentation, start with:

* [Accountable Systems Infrastructure and Governed Execution](accountable-systems-infrastructure-and-governed-execution.md) — the stack-neutral meaning of the ASI Backbone governance-spine idea.
* [Intent to Execution: An Accountability Pattern](intent-to-execution-accountability-pattern.md) — the accountability gap between proposal and side effect.
* [Constraint-Conditioned Decision Model](constraint-conditioned-decision-model.md) — the conceptual structure behind narrowing intent through active constraints.
* [Governance Tool Selection and Composition](governance-tool-selection-and-composition.md) — how adjacent governance mechanisms protect different boundaries and compose without becoming substitutes.

These pages are educational. Concrete package, API, configuration, compatibility, security, and release behavior remains authoritative in the implementation repositories.

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

## Visual Reference

For a compact orientation to the major boundaries, see [Governance Spine and Capability Validation Diagrams](governance-spine-and-capability-validation-diagrams.md).

The visual reference covers:

* The governance spine from intent through host-owned execution.
* Policy context, independent constraints, and explicit decision composition.
* Metadata inspection versus execution-boundary capability validation.
* AI proposal versus host authority in a governed tool gateway.

The diagrams are reference aids rather than substitutes for the tutorials, samples, tests, and labs.

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

## Application Structure Growth

For general application-layering guidance, see [Growing Beyond a Simple Application Structure](growing-beyond-a-simple-application-structure.md). It explains when a compact application is enough, what signals justify Application or Domain boundaries, how dependency direction should be reasoned about, and the tradeoffs around CQRS, MediatR, DDD, and premature layering. NetCoreApplicationTemplate is used as one working reference rather than a universal pattern.

## Alternative Patterns

Architecture should be compared against viable alternatives rather than presented as a single prescribed design.

Start with:

* [When ASP.NET Core Authorization Is Enough](when-aspnet-core-authorization-is-enough.md) — compares the governed-execution model with ASP.NET Core policies, requirements, handlers, and resource-based authorization, including cases where the built-in authorization model is the simpler and better choice.
* [Role-Based, Claims-Based, and Capability-Based Authorization](role-based-claims-based-and-capability-based-authorization.md) — compares stable role membership, richer claims policies, and narrowly scoped capabilities, including when each model wins and when composition is preferable to replacement.
* [API Gateways, Service Meshes, Zero Trust, and Governed Execution](api-gateways-service-meshes-zero-trust-and-governed-execution.md) — separates transport, workload identity, infrastructure security strategy, application-level decision semantics, and execution ownership, then shows how the boundaries can be layered without treating them as substitutes.
* [When a Simple Application Service Is Enough](when-a-simple-application-service-is-enough.md) — examines the middle ground where authorization alone is not the whole use case, but an immediate application-service workflow still expresses the required validation, domain rules, persistence, execution, and audit boundaries without a broader governance lifecycle.
* [Workflow Engines, Human Approval Systems, and Governed Execution](workflow-engines-human-approval-and-governed-execution.md) — separates durable process orchestration, bound human dispositions, current policy decisions, and scoped execution authority, including cases where a workflow engine already provides the required governance semantics and a second layer would only duplicate them.
* [Policy Engines, Rules Engines, and Distributed Policy Enforcement](policy-engines-rules-engines-and-distributed-policy-enforcement.md) — distinguishes domain rule evaluation from externalized policy decisions and from the distributed placement of PDPs and PEPs, including policy distribution, stale-policy handling, partitions, local autonomy, and when broader governance lifecycle responsibilities remain separate.
* [Agent and Tool Authorization Models and Host-Owned Execution](agent-and-tool-authorization-models-and-host-owned-execution.md) — compares model-visible tool selection, framework registration and per-agent permissions, schema validation, host-side authorization, and capability-scoped execution, including when framework-native tool controls are sufficient and when a separate execution-authority boundary is justified.
* [Event Sourcing, Audit Trails, and Governance Decision Provenance](event-sourcing-audit-trails-and-governance-decision-provenance.md) — compares operational logs, ordinary audit history, governance decision receipts, and event sourcing, including denied decisions, replay, projections, tamper evidence, privacy/deletion tradeoffs, and historical policy reconstruction.
* [CQRS, Command/Query Separation, and Governed Execution](cqrs-command-query-separation-and-governed-execution.md) — compares command/query separation, immediate command handlers, explicit policy evaluation, and delayed execution with scoped authority, including when a command handler is already the correct host-owned execution boundary.

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

The Architecture section is established as a learning area and now includes general application-structure growth guidance, a governed-execution visual reference, and concrete alternative-pattern comparisons. It will continue to grow through additional comparisons, diagrams, and cross-repository studies.

For the current learning path, continue with the [Foundational Tutorials](../tutorials/index.md).

---

> **Read it. Run it. Question it. Improve it.**