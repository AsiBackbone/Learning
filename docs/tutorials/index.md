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

### Learning Path at a Glance

| Tutorial | Difficulty | Prerequisites |
| --- | --- | --- |
| [Decision Before Execution](decision-before-execution.md) | Beginner | None |
| [Policy Context and Explicit Decision Outcomes](policy-context-and-explicit-decision-outcomes.md) | Beginner | Tutorial 1 |
| [Acknowledgment and Audit Residue](acknowledgment-and-audit-residue.md) | Intermediate | Tutorials 1–2 |
| [Scoped Capability and Host-Owned Execution](scoped-capability-and-host-owned-execution.md) | Intermediate | Tutorials 1–3 |
| [Governed AI Tool Gateway](governed-ai-tool-gateway.md) | Intermediate | Tutorials 1–4 |

Difficulty reflects the conceptual complexity of the learning material rather than the production-readiness of the demonstrated patterns.

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

### 5. [Governed AI Tool Gateway](governed-ai-tool-gateway.md)

Compose the first four patterns into an end-to-end AI-assisted execution gateway where the model may propose an action but the host retains authoritative context and execution authority.

Topics include:

- AI proposal versus authority
- Host-owned tool registry
- Proposal and argument validation
- Authoritative policy context
- Prompt guidance versus enforcement
- Explicit governance decisions
- Human acknowledgment
- Scoped capability issuance
- Execution-boundary validation
- Semantic tool design
- Secret isolation
- Egress and destination control
- Replay versus idempotency
- Dry-run adoption
- End-to-end testing and audit continuity

The complete foundational flow is:

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
Audit residue
```

> **The model may propose. The host retains execution authority.**

## Continue Beyond the Tutorials

The tutorials are the explanation layer of the Learning repository.

The broader learning path is:

```text
Tutorial
   ↓
Executable Sample
   ↓
Hands-On Lab
   ↓
Working Repository
```

After studying a tutorial:

* [Browse Executable Samples](../samples/index.md) for the published sample guide, run commands, architectural invariants, and links to the canonical companion READMEs.
* [Browse Labs](../labs/index.md) for hands-on exercises and architecture challenges as they are published.
* [Explore AsiBackbone](https://github.com/AsiBackbone/AsiBackbone) for fuller governance and policy-control implementations.
* [Explore NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate) for a fuller ASP.NET Core reference architecture.

The teaching material is intentionally smaller than the working repositories. Use the larger implementations to see how similar ideas behave when more production concerns are present.


## Foundational Sequence Complete

The five tutorials form the initial governed-execution curriculum.

They are intended to be reused, questioned, simplified, or adapted rather than treated as a mandatory framework adoption path.

Good next steps include hands-on labs, alternative implementations, ASP.NET Core integration examples, AI gateway simulations, and architecture comparisons.

---

> **Read it. Run it. Question it. Improve it.**
