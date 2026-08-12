# Tutorials

Tutorials in ASI Backbone Learning are **problem-first**.

They begin with an architectural problem, examine a common or naive implementation, expose its limitations, introduce a pattern, and then connect the smaller teaching example to fuller working implementations.

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

The goal is not to require adoption of a specific framework.

A tutorial should remain useful even if you never install the `AsiBackbone` package or use `NetCoreApplicationTemplate`.

## Foundational Tutorials

### 1. [Decision Before Execution](decision-before-execution.md)

Understand why a consequential operation should be represented as proposed intent, evaluated, and converted into an explicit decision before the host performs the real-world action.

Topics include:

- Intent versus execution
- Authorization versus governance
- Explicit decision results
- Policy context
- Host-owned execution
- Testing the execution boundary
- Audit evidence
- Tradeoffs and failure modes
- AI-proposed tool actions

This tutorial establishes the boundary used throughout the rest of the Learning material:

> **A proposed action should become a governed decision before it becomes real-world execution.**

### 2. [Policy Context and Explicit Decision Outcomes](policy-context-and-explicit-decision-outcomes.md)

Understand why the facts used to make a governance decision should be represented explicitly and why the result should describe what happens next rather than collapse every decision into a boolean.

Topics include:

- Facts versus policy rules
- Actor, resource, operation, and environmental context
- Context snapshots
- Explicit governance outcomes
- Stable reason codes
- Policy identity
- Decision composition
- Determinism
- Context and outcome testing
- AI policy context

This tutorial expands the middle of the governed-execution flow:

```text
Intent
   ↓
Policy Context
   ↓
Constraints
   ↓
Explicit Decision Outcome
```

### 3. [Acknowledgment and Audit Residue](acknowledgment-and-audit-residue.md)

Understand how a consequential operation can pause for explicit acknowledgment, resume through a governed boundary, and leave structured evidence explaining the decision path.

Topics include:

- Acknowledgment challenges
- Actor and operation binding
- Accepted and rejected responses
- Expiration and replay considerations
- Re-evaluation after acknowledgment
- Acknowledgment versus policy override
- Decision, acknowledgment, and execution evidence
- Audit residue versus operational logging
- Correlation and reason codes
- Durable persistence boundaries
- AI acknowledgment workflows

This tutorial expands the lifecycle after a decision:

```text
Decision
   ↓
Acknowledgment when required
   ↓
Host-owned continuation
   ↓
Audit residue
```

### 4. [Scoped Capability and Host-Owned Execution](scoped-capability-and-host-owned-execution.md)

Understand why an allowed or acknowledged operation should not automatically become broad execution authority and how a short-lived, narrowly scoped capability can preserve a host-controlled execution boundary.

Topics include:

- Approval versus authority
- Least-privilege scopes
- Subject, operation, and resource binding
- Audience and gateway binding
- Time-bounded authority
- Policy and acknowledgment binding
- Execution-boundary validation
- Replay and bounded use
- Revocation and cancellation
- Proof and integrity considerations
- Host-owned execution
- AI capability-scoped tool execution

This tutorial expands the transition from governance into execution:

```text
Decision
   ↓
Acknowledgment when required
   ↓
Scoped capability
   ↓
Capability validation
   ↓
Host-owned execution
```

## Planned Next Topic

The foundational sequence concludes with:

5. **Governed AI Tool Gateway**

The final foundational tutorial will compose the first four patterns into one end-to-end AI-assisted execution workflow.

---

> **Read it. Run it. Question it. Improve it.**
