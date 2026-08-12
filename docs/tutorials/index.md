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

## Planned Next Topics

The foundational sequence will continue with:

3. **Acknowledgment and Audit Residue**
4. **Scoped Capability and Host-Owned Execution**
5. **Governed AI Tool Gateway**

Each tutorial will build on the earlier concepts while remaining readable independently where practical.

---

> **Read it. Run it. Question it. Improve it.**
