---
description: Choose a short, problem-oriented route through ASI Backbone Learning based on common ASP.NET Core, governance, AI, security, and architecture goals.
---

# Find Your Path

ASI Backbone Learning can be used as a sequential course, but you do not need to read it that way.

If you already know the problem you are trying to solve, start with the shortest path that makes the relevant boundary visible. Continue only when the simpler architecture no longer preserves the behavior, evidence, or control you need.

```text
Reader has a problem
      ↓
Choose the shortest relevant path
      ↓
Read or run only what is needed
      ↓
Stop when the simpler design is enough
      ↓
Expand into deeper material when useful
```

This page is a routing layer. It links to canonical material instead of restating the tutorials, samples, or labs.

## I Want to See the Core Boundary Run Quickly

**Problem:** You want to see governed execution behave before reading the deeper architecture.

**Start here:** Run the repository [Quick Start](https://github.com/AsiBackbone/Learning/blob/main/README.md#quick-start--run-it-in-10-minutes). It demonstrates the foundational invariant that blocked decisions do not reach the executor.

**Then read:** [Decision Before Execution](../tutorials/decision-before-execution.md) when you want the reasoning behind the boundary.

**Make it observable:** Use the [Decision Before Execution sample and tests](https://github.com/AsiBackbone/Learning/tree/main/samples/decision-before-execution), then complete the [Decision Before Execution lab](../labs/decision-before-execution.md).

**Prefer something simpler when:** The operation is ordinary application behavior with no meaningful decision boundary, acknowledgment requirement, scoped authority, or audit obligation. [When a Simple Application Service Is Enough](../architecture/when-a-simple-application-service-is-enough.md) is the comparison point.

**Go deeper:** Continue through the [foundational learning path](index.md#the-foundational-learning-path) only if the later policy, acknowledgment, capability, or AI boundaries are relevant to your system.

## I Already Use ASP.NET Core Authorization and Want to Know If That Is Enough

**Problem:** You already have framework-native authentication and authorization and do not want to introduce a broader governance model without a real need.

**Start here:** [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md).

**Then read:** [When a Simple Application Service Is Enough](../architecture/when-a-simple-application-service-is-enough.md). These two pages deliberately put simpler application architecture before a larger governed-execution pipeline.

**Continue only if needed:** Read [Decision Before Execution](../tutorials/decision-before-execution.md) when the operation must become an explicit decision before a consequential side effect can occur.

**Make it observable:** If the broader boundary is justified, run the [Decision Before Execution sample and tests](https://github.com/AsiBackbone/Learning/tree/main/samples/decision-before-execution) and use the [Decision Before Execution lab](../labs/decision-before-execution.md) to separate evaluation from execution yourself.

**Prefer something simpler when:** The question is only whether an authenticated principal may access an endpoint or resource, or when one application service can own validation and execution clearly. Stop at the simpler pattern when it preserves the boundaries you actually need.

**Go deeper:** Use the [Architecture](../architecture/index.md) and [Governance](../governance/index.md) areas when the problem grows beyond authorization into explicit outcomes, acknowledgment, capability, provenance, or policy composition.

## I Need to Govern a Consequential Administrative Operation

**Problem:** An administrative or operational action may require explicit policy context, non-boolean outcomes, acknowledgment, narrow execution authority, and evidence of what happened.

**Start here:** [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md).

**Then read:** Follow with [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md), then [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md).

```text
Administrative intent
      ↓
Policy context and explicit decision
      ↓
Acknowledgment when required
      ↓
Scoped capability
      ↓
Host-owned execution
      ↓
Audit residue
```

**Make it observable:** Use the related [executable sample guide](../samples/index.md), then complete [Build a Governed API Operation](../labs/build-a-governed-api-operation.md) to compose the boundaries around one consequential API action.

**Prefer something simpler when:** Ordinary ASP.NET Core authorization plus a clear application service already answers the access and execution questions. Start with [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md) or [When a Simple Application Service Is Enough](../architecture/when-a-simple-application-service-is-enough.md) before adding governance machinery.

**Go deeper:** Browse [Governance](../governance/index.md) for policy composition, escalation, provenance, risk-based decisions, and testing strategies. For a fuller working implementation, inspect [AsiBackbone/AsiBackbone](https://github.com/AsiBackbone/AsiBackbone).

## I Need to Govern AI-Proposed Tool Execution

**Problem:** A model can propose a tool call or operation, but the host must retain authority over validation, policy, credentials, and real-world execution.

**Start here:** [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md).

**Then read:** Add [Typed AI-Proposed Intent and Schema-Validation Boundaries](../ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md), then [Deterministic and Probabilistic Inputs in Policy Evaluation](../governance/deterministic-and-probabilistic-inputs-in-policy-evaluation.md) when model-derived or risk-derived signals influence a decision.

```text
Model proposal
      ↓
Typed and schema-valid intent
      ↓
Policy evaluation
      ↓
Explicit decision
      ↓
Host-owned tool execution
```

**Make it observable:** Run the [Governed AI Tool Gateway sample and tests](https://github.com/AsiBackbone/Learning/tree/main/samples/governed-ai-tool-gateway), then complete the [Governed AI Tool Gateway lab](../labs/governed-ai-tool-gateway.md).

**Prefer something simpler when:** The model produces suggestions or data that never cross an execution boundary. In that case, ordinary input validation and an application-owned service may be sufficient; do not build an execution gateway for a workflow that does not execute tools.

**Go deeper:** Use the [AI Integration](../ai-integration/index.md) and [Governance](../governance/index.md) areas. If multiple autonomous participants begin proposing work to one another, continue to [Governed Agent-to-Agent Requests and Multi-Agent Execution Boundaries](../advanced/governed-agent-to-agent-requests-and-multi-agent-execution-boundaries.md).

## I Need to Reason About Trust Boundaries and Operational Security

**Problem:** You need to decide where trust changes, where authority should narrow, what secrets may cross a boundary, what may be logged, and which threats the architecture must make visible.

**Start here:** [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md).

**Then read:** Continue with [Secret Handling Across Trust Boundaries](../security/secret-handling-across-trust-boundaries.md), [Secure Logging Across Trust Boundaries](../security/secure-logging-across-trust-boundaries.md), and [Threat Modeling as Architecture Reasoning](../security/threat-modeling-as-architecture-reasoning.md).

**Make it observable:** The [Replay Protection and Bounded Use sample](https://github.com/AsiBackbone/Learning/tree/main/samples/replay-protection-and-bounded-use) and [lab](../labs/replay-protection-and-bounded-use.md) make one concrete authority boundary executable by showing why issued authority still needs bounded, replay-resistant use.

**Prefer something simpler when:** Framework and platform security controls already preserve the boundary. Do not replace established authentication, authorization, secret stores, transport security, or logging controls with custom infrastructure merely to match a diagram.

**Go deeper:** Browse the [Security](../security/index.md) area for signing, verification, key custody, supply-chain integrity, replay protection, and related trust-architecture material.

## I Need to Preserve and Revisit Architecture Decisions

**Problem:** The code shows what the system does, but future maintainers also need to recover why a consequential architectural choice was made, what alternatives existed, and what evidence should trigger review.

**Start here:** [Architecture Decision Records Preserve Architectural Reasoning](../aspnetcore/architecture-decision-records-preserve-architectural-reasoning.md).

**Then read:** Continue with [Architecture Decision Record Lifecycle, Review, Deprecation, and Supersession](../aspnetcore/architecture-decision-record-lifecycle-review-deprecation-and-supersession.md), then study [Working Repository ADR Case Study: NetCoreApplicationTemplate](../aspnetcore/netcoreapplicationtemplate-adr-case-study.md).

**Make it observable:** Complete [Write and Revisit an Architecture Decision Record](../labs/write-and-revisit-an-architecture-decision-record.md). The lab requires you to record a decision, preserve alternatives and consequences, then revisit it after the scenario changes.

**Prefer something simpler when:** The choice is a local implementation detail, routine refactor, or behavior whose reasoning is already obvious from the code. A code comment, pull-request explanation, or implementation guide is often a better fit than an ADR.

**Go deeper:** Browse the [ASP.NET Core](../aspnetcore/index.md) area and inspect the [NetCoreApplicationTemplate ADRs](https://github.com/AsiBackbone/NetCoreApplicationTemplate/tree/main/docs/adr) as a working repository specimen.

## If None of These Paths Match

Use the subject-area landing pages instead of forcing your problem into a path that does not fit:

- [Architecture](../architecture/index.md)
- [Governance](../governance/index.md)
- [ASP.NET Core](../aspnetcore/index.md)
- [Security](../security/index.md)
- [AI Integration](../ai-integration/index.md)
- [Tutorials](../tutorials/index.md)
- [Executable Samples](../samples/index.md)
- [Labs](../labs/index.md)
- [Advanced](../advanced/index.md)

The path chooser is intentionally incomplete. It should remain a compact map of common reader goals rather than another table of contents.

> **Use the smallest architecture that preserves the boundaries you actually need.**
