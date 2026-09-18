---
description: Choose a short, problem-oriented route through AsiBackbone Learning based on common ASP.NET Core, governance, AI, security, and architecture goals.
---

# Find Your Path

AsiBackbone Learning can be used as a sequential course, but you do not need to read it that way.

If you already know the problem you are trying to solve, start with the shortest route that makes the relevant boundary visible. Stop when the simpler design preserves the behavior, evidence, and control you need; go deeper only when it does not.

> **Use the smallest architecture that preserves the boundaries you actually need.**

## Choose Your Problem

| If you want to... | Start here |
| --- | --- |
| See governed execution work before reading the architecture | [See the core boundary run quickly](#i-want-to-see-the-core-boundary-run-quickly) |
| Decide whether ASP.NET Core authorization is already enough | [Evaluate ASP.NET Core authorization first](#i-already-use-aspnet-core-authorization-and-want-to-know-if-that-is-enough) |
| Govern a consequential administrative operation | [Build an explicit governed operation](#i-need-to-govern-a-consequential-administrative-operation) |
| Govern AI-proposed tool execution | [Keep model output as proposal, not authority](#i-need-to-govern-ai-proposed-tool-execution) |
| Reason about trust boundaries and operational security | [Start from trust and least privilege](#i-need-to-reason-about-trust-boundaries-and-operational-security) |
| Preserve and revisit architecture decisions | [Use ADRs to preserve reasoning](#i-need-to-preserve-and-revisit-architecture-decisions) |

This page is a routing layer. It links to canonical material instead of restating the tutorials, samples, or labs.

## How to Use a Path

Each route follows the same decision pattern:

```text
Problem
   ↓
Start with the smallest relevant pattern
   ↓
Read only the supporting material you need
   ↓
Run or modify the executable example
   ↓
Stop if the simpler design is enough
   ↓
Go deeper only when the boundary requires it
```

The goal is not to maximize framework adoption. The goal is to make the architectural boundary visible enough that you can decide whether the additional lifecycle is justified.

## I Want to See the Core Boundary Run Quickly

**Problem:** You want to see governed execution behave before reading the deeper architecture.

**Start:** Run the repository [Quick Start](https://github.com/AsiBackbone/Learning/blob/main/README.md#quick-start--run-it-in-10-minutes). It demonstrates the foundational invariant that blocked decisions do not reach the executor.

**Understand the boundary:** Read [Decision Before Execution](../tutorials/decision-before-execution.md).

**Run and modify it:** Use the [Decision Before Execution sample and tests](https://github.com/AsiBackbone/Learning/tree/main/samples/decision-before-execution), then complete the [Decision Before Execution lab](../labs/decision-before-execution.md).

**Stop here when:** The operation is ordinary application behavior with no meaningful decision boundary, acknowledgment requirement, scoped authority, or audit obligation. [When a Simple Application Service Is Enough](../architecture/when-a-simple-application-service-is-enough.md) is the comparison point.

**Go deeper when:** Later policy, acknowledgment, capability, or AI boundaries matter. Continue through the [foundational learning path](index.md#five-part-foundation).

## I Already Use ASP.NET Core Authorization and Want to Know If That Is Enough

**Problem:** You already have framework-native authentication and authorization and do not want to introduce a broader governance model without a real need.

**Start:** [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md).

**Compare the simpler option:** Read [When a Simple Application Service Is Enough](../architecture/when-a-simple-application-service-is-enough.md). These two pages deliberately put simpler application architecture before a larger governed-execution pipeline.

**Continue only if needed:** Read [Decision Before Execution](../tutorials/decision-before-execution.md) when the operation must become an explicit decision before a consequential side effect can occur.

**Run and modify it:** If the broader boundary is justified, use the [Decision Before Execution sample and tests](https://github.com/AsiBackbone/Learning/tree/main/samples/decision-before-execution) and the [Decision Before Execution lab](../labs/decision-before-execution.md).

**Stop here when:** The question is only whether an authenticated principal may access an endpoint or resource, or when one application service can clearly own validation and execution.

**Go deeper when:** The problem grows beyond authorization into explicit outcomes, acknowledgment, capability, provenance, or policy composition. Use [Architecture](../architecture/index.md) and [Governance](../governance/index.md).

## I Need to Govern a Consequential Administrative Operation

**Problem:** An administrative or operational action may require explicit policy context, non-boolean outcomes, acknowledgment, narrow execution authority, and evidence of what happened.

**Start:** [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md).

**Build the lifecycle:** Continue with [Decision Receipts and Acknowledgment](../tutorials/decision-receipts-and-acknowledgment.md), then [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md).

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
Decision receipt
```

**See the composition:** [Governed Administrative Operation](../case-studies/governed-administrative-operation.md) follows one fictional `account.disable` request through standing authorization, authoritative context, policy evaluation, acknowledgment or escalation, scoped authority, executor invocation, and correlated evidence.

**Run and modify it:** Use the related [executable sample guide](../samples/index.md), then complete [Build a Governed API Operation](../labs/build-a-governed-api-operation.md).

**Stop here when:** Ordinary ASP.NET Core authorization plus a clear application service already answers the access and execution questions. Compare [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md) and [When a Simple Application Service Is Enough](../architecture/when-a-simple-application-service-is-enough.md) before adding governance machinery.

**Go deeper when:** You need policy composition, escalation, provenance, risk-based decisions, or stronger testing strategies. Browse [Governance](../governance/index.md). For a fuller working implementation, inspect [AsiBackbone/AsiBackbone](https://github.com/AsiBackbone/AsiBackbone).

## I Need to Govern AI-Proposed Tool Execution

**Problem:** A model can propose a tool call or operation, but the host must retain authority over validation, policy, credentials, and real-world execution.

**Start:** [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md).

**Strengthen the proposal boundary:** Add [Typed AI-Proposed Intent and Schema-Validation Boundaries](../ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md), then [Deterministic and Probabilistic Inputs in Policy Evaluation](../governance/deterministic-and-probabilistic-inputs-in-policy-evaluation.md) when model-derived or risk-derived signals influence a decision.

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

**Run and modify it:** Use the [Governed AI Tool Gateway sample and tests](https://github.com/AsiBackbone/Learning/tree/main/samples/governed-ai-tool-gateway), then complete the [Governed AI Tool Gateway lab](../labs/governed-ai-tool-gateway.md).

**Stop here when:** The model produces suggestions or data that never cross an execution boundary. Ordinary input validation and an application-owned service may be sufficient; do not build an execution gateway for a workflow that does not execute tools.

**Go deeper when:** You need broader AI governance or policy composition. Use [AI Integration](../ai-integration/index.md) and [Governance](../governance/index.md). If multiple autonomous participants begin proposing work to one another, continue to [Governed Agent-to-Agent Requests and Multi-Agent Execution Boundaries](../advanced/governed-agent-to-agent-requests-and-multi-agent-execution-boundaries.md).

## I Need to Reason About Trust Boundaries and Operational Security

**Problem:** You need to decide where trust changes, where authority should narrow, what secrets may cross a boundary, what may be logged, and which threats the architecture must make visible.

**Start:** [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md).

**Strengthen the trust model:** Continue with [Secret Handling Across Trust Boundaries](../security/secret-handling-across-trust-boundaries.md), [Secure Logging Across Trust Boundaries](../security/secure-logging-across-trust-boundaries.md), and [Threat Modeling as Architecture Reasoning](../security/threat-modeling-as-architecture-reasoning.md).

**Run and modify it:** The [Replay Protection and Bounded Use sample](https://github.com/AsiBackbone/Learning/tree/main/samples/replay-protection-and-bounded-use) and [lab](../labs/replay-protection-and-bounded-use.md) make one concrete authority boundary executable by showing why issued authority still needs bounded, replay-resistant use.

**Stop here when:** Framework and platform security controls already preserve the boundary. Do not replace established authentication, authorization, secret stores, transport security, or logging controls with custom infrastructure merely to match a diagram.

**Go deeper when:** You need signing, verification, key custody, supply-chain integrity, replay protection, or related trust-architecture material. Browse [Security](../security/index.md).

## I Need to Preserve and Revisit Architecture Decisions

**Problem:** The code shows what the system does, but future maintainers also need to recover why a consequential architectural choice was made, what alternatives existed, and what evidence should trigger review.

**Start:** [Architecture Decision Records Preserve Architectural Reasoning](../aspnetcore/architecture-decision-records-preserve-architectural-reasoning.md).

**Build the lifecycle:** Continue with [Architecture Decision Record Lifecycle, Review, Deprecation, and Supersession](../aspnetcore/architecture-decision-record-lifecycle-review-deprecation-and-supersession.md), then study [Working Repository ADR Case Study: NetCoreApplicationTemplate](../aspnetcore/netcoreapplicationtemplate-adr-case-study.md).

**Run and modify it:** Complete [Write and Revisit an Architecture Decision Record](../labs/write-and-revisit-an-architecture-decision-record.md). The lab requires you to record a decision, preserve alternatives and consequences, then revisit it after the scenario changes.

**Stop here when:** The choice is a local implementation detail, routine refactor, or behavior whose reasoning is already obvious from the code. A code comment, pull-request explanation, or implementation guide is often a better fit than an ADR.

**Go deeper when:** You want more ASP.NET Core architecture material or a working repository specimen. Browse [ASP.NET Core](../aspnetcore/index.md) and inspect the [NetCoreApplicationTemplate ADRs](https://github.com/AsiBackbone/NetCoreApplicationTemplate/tree/main/docs/adr).

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
