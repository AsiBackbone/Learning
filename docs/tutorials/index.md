---
description: Browse problem-first tutorials that expose failure modes, introduce architectural patterns, and connect focused examples to working implementations.
---

# Tutorials

AsiBackbone Learning tutorials are **problem-first**. They begin with an architectural problem, expose a failure mode or limitation, introduce a pattern, and connect the teaching example to runnable evidence and fuller implementations.

The goal is understanding—not framework adoption.

## Learning Path at a Glance

| Step | Tutorial | Difficulty | Boundary added |
| --- | --- | --- | --- |
| 1 | [Decision Before Execution](decision-before-execution.md) | Beginner | Evaluation is separated from protected execution |
| 2 | [Policy Context and Explicit Decision Outcomes](policy-context-and-explicit-decision-outcomes.md) | Beginner | Decision facts and outcomes become explicit |
| 3 | [Decision Receipts and Acknowledgment](decision-receipts-and-acknowledgment.md) | Intermediate | Decision receipts, acknowledgment, and later lifecycle evidence remain distinct from authority |
| 4 | [Scoped Capability and Host-Owned Execution](scoped-capability-and-host-owned-execution.md) | Intermediate | Execution authority becomes narrow, temporary, and host-validated |
| 5 | [Governed AI Tool Gateway](governed-ai-tool-gateway.md) | Intermediate | AI proposal is composed with host-owned context, policy, authority, and execution |

All five are currently classified as **Canonical Pattern** material. Difficulty describes conceptual complexity, not production readiness.

If you already know authorization, ABAC, capability security, workflow, audit/provenance, or reference-monitor concepts, use [Terminology and Established Architecture Concepts](../architecture/terminology-and-established-concepts.md) to map that vocabulary to the terms used here.

## How a Tutorial Works

A typical tutorial follows this progression:

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

Each foundational tutorial also includes:

- a **Pattern Card** for fast orientation,
- an observable invariant that carries into samples or tests,
- tradeoffs and simpler alternatives,
- and a **Check Your Understanding** checklist focused on what you should be able to explain or demonstrate.

The checklist is not a score or certification.

## The Five Foundations

### 1. [Decision Before Execution](decision-before-execution.md)

Represent a consequential operation as proposed intent, evaluate it, and produce an explicit decision before the host performs the side effect.

**Core idea:** intent, authorization, governance decision, execution, and evidence should not collapse into one opaque operation.

> **A proposed action should become a governed decision before it becomes real-world execution.**

### 2. [Policy Context and Explicit Decision Outcomes](policy-context-and-explicit-decision-outcomes.md)

Represent the facts used by policy explicitly and return outcomes that describe what happens next rather than reducing every decision to a boolean.

**Core ideas:** actor/resource/operation/environment context, context snapshots, stable reason codes, policy identity, determinism, and decision composition.

### 3. [Decision Receipts and Acknowledgment](decision-receipts-and-acknowledgment.md)

Pause a consequential operation for explicit acknowledgment, resume through a governed boundary, and preserve structured evidence of the decision path.

**Core ideas:** response binding, expiration, replay, re-evaluation, acknowledgment versus override, correlation, and durable evidence boundaries.

### 4. [Scoped Capability and Host-Owned Execution](scoped-capability-and-host-owned-execution.md)

Keep approval from becoming broad standing authority by issuing and validating short-lived, narrowly scoped execution authority at the host boundary.

**Core ideas:** subject/operation/resource/audience binding, time bounds, replay, revocation, current-state validation, and host-owned execution.

### 5. [Governed AI Tool Gateway](governed-ai-tool-gateway.md)

Compose the first four patterns around AI-proposed tool execution while keeping authoritative context, credentials, policy, and real-world effects under host control.

```text
AI proposal
   ↓
Host-owned context
   ↓
Governance decision
   ↓
Acknowledgment when required
   ↓
Scoped capability
   ↓
Execution-boundary validation
   ↓
Host-owned tool execution
   ↓
Decision receipt
```

> **The model may propose. The host retains execution authority.**

## Continue into Practice

Tutorials are the explanation layer. The broader learning path is:

```text
Tutorial
   ↓
Executable Sample
   ↓
Hands-On Lab
   ↓
Working Repository
```

After a tutorial:

- [Browse Executable Samples](../samples/index.md) to run focused companion implementations and invariant tests.
- [Browse Labs](../labs/index.md) to modify, break, repair, critique, or extend the architecture.
- [Explore AsiBackbone](https://github.com/AsiBackbone/AsiBackbone) for fuller governance and policy-control implementations.
- [Explore NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate) for a fuller ASP.NET Core reference architecture.

The five tutorials form the initial governed-execution curriculum, but they are meant to be questioned, simplified, adapted, or rejected when another design better fits the problem.

---

> **Read it. Run it. Question it. Improve it.**
