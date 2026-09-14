---
description: Understand Learning's problem-first model, the roles of tutorials, samples, tests, and labs, and how canonical and alternative patterns are presented.
---

# Learning Model

ASI Backbone Learning is a living architecture-learning resource, not a product manual or framework adoption funnel.

Its purpose is to help a reader understand an architectural boundary, observe it in a small implementation, verify the claimed invariant, challenge the design, and adapt only what is useful.

> **Read it. Run it. Question it. Improve it.**

You do not need to install an `AsiBackbone` package to use this material.

## The Model at a Glance

| Stage | Purpose | Reader question |
| --- | --- | --- |
| **Problem** | Establish the architectural need before discussing implementation | What boundary or failure mode actually exists? |
| **Tutorial** | Explain the reasoning, pattern, tradeoffs, and alternatives | Why might this pattern help? |
| **Sample** | Make the boundary observable in a small implementation | What does the pattern look like when it runs? |
| **Test** | Turn the architectural claim into a repeatable contract | Can I verify the invariant independently? |
| **Lab** | Require the learner to modify, critique, repair, or extend the design | Do I understand the tradeoff well enough to make a decision? |
| **Comparison** | Evaluate simpler and alternative approaches | Is this still the smallest architecture that preserves the boundary? |
| **Working repository** | Inspect fuller implementation context when useful | How is the pattern realized in a larger system? |

The sequence is intentionally evidence-driven:

```text
Understand the problem
        ↓
Observe the boundary
        ↓
Verify the invariant
        ↓
Experiment with the design
        ↓
Compare alternatives
        ↓
Inspect fuller implementations when useful
```

## Problem First, Product Second

Where practical, tutorials follow this progression:

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

The order matters.

The architectural problem should be understandable before a reader is asked to care about a particular implementation. A tutorial should remain useful even when the reader concludes that a simpler design, a framework-native feature, or a different architecture is the better fit.

Learning therefore favors questions such as:

- What boundary is missing?
- What failure becomes possible when responsibilities are combined?
- Which invariant should be observable?
- What is the smallest architecture that preserves the boundary we need?
- What tradeoff are we accepting by adding another policy, acknowledgment, capability, or gateway step?

Working repositories are architectural specimens, not unquestioned templates.

## Tutorial → Sample → Test → Lab

The foundational path uses four complementary forms:

```text
Tutorial
   ↓
Runnable Sample
   ↓
Architectural Invariant Tests
   ↓
Hands-On Lab
```

Each form has one primary job.

### Tutorials Explain

Tutorials introduce an architectural problem and show one or more ways to reason about it.

They should emphasize:

- why the problem matters,
- the common or naive implementation,
- failure modes or limitations,
- the architectural pattern being examined,
- a minimal teaching example,
- tradeoffs and alternatives,
- and fuller working examples when useful.

A tutorial explains a pattern. It does not claim that every application requires it.

### Samples Demonstrate

Runnable samples make the architectural boundary observable without requiring the reader to extract it from a large application.

A good teaching sample is intentionally small, deterministic where practical, and focused on the behavior under examination.

```text
Denied decision
   ↓
Executor invocation count = 0
```

The sample exists to expose the boundary, not to serve as a production framework.

### Tests Verify

Focused tests turn important architectural claims into repeatable contracts.

Their purpose is not broad coverage for its own sake. Their purpose is to make statements such as these independently observable:

```text
Expired capability
   ↓
Execution blocked
```

```text
Unknown AI tool
   ↓
Proposal rejected
   ↓
No execution
```

When a tutorial says a boundary exists, the companion test should make that claim difficult to misunderstand.

### Labs Make You Decide

Labs move from explanation to active architectural judgment.

A lab may provide:

- a partially implemented application,
- a broken or incomplete architecture,
- a policy-design exercise,
- a security or governance scenario,
- tests that must be made to pass,
- or an architecture the learner is asked to critique or improve.

The learner may need to identify hidden side effects, separate evaluation from execution, introduce explicit decision outcomes, preserve acknowledgment boundaries, validate scoped authority, detect stale authority, threat-model an AI gateway, or compare alternate designs.

> **Tutorials explain. Samples demonstrate. Tests verify. Labs make you decide.**

## Canonical and Alternative Patterns

Learning does not exist to prove that one architecture is always correct.

Material may therefore distinguish between two pattern types.

| Pattern type | What it means | What it does not mean |
| --- | --- | --- |
| **Canonical** | Aligned with the current architecture of one or more ASI Backbone organization projects | Universal, mandatory, or superior in every context |
| **Alternative** | A technically grounded approach that solves the same problem differently | Incorrect merely because it differs from the working repositories |

A canonical pattern answers:

> How do the working repositories currently approach this problem?

An alternative pattern is especially useful when it exposes tradeoffs, demonstrates that a simpler mechanism is sufficient, or shows how another ecosystem handles the same boundary.

The first published alternative-pattern comparison is [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md).

> **Use the smallest architecture that preserves the boundaries you actually need.**

Architectural disagreement is useful when the alternatives are explained clearly and evaluated on their tradeoffs.

## Framework Independence

Learning should remain useful to readers who never install `AsiBackbone` and never use `NetCoreApplicationTemplate`.

Readers are encouraged to:

- study individual patterns without adopting an entire framework,
- copy or adapt useful ideas into their own systems,
- compare demonstrated approaches with framework-native or external alternatives,
- remove complexity their application does not need,
- question assumptions and identify tradeoffs,
- submit corrections, examples, diagrams, tutorials, and alternative approaches,
- and use implementation repositories as working architectural specimens.

If a developer studies a pattern here and uses it to make a better architectural decision—even by rejecting the demonstrated pattern—Learning is serving its purpose.

## Why the Material Is Structured This Way

The repository keeps explanation close to evidence while avoiding two common failure modes:

1. presenting a large implementation before the reader understands the boundary it is trying to preserve; and
2. presenting an abstract principle without runnable evidence that shows what the principle means in practice.

The material is intentionally layered:

| Layer | Role |
| --- | --- |
| **Root README** | Front door and quick orientation |
| **Getting Started** | Routes readers toward the right entry point |
| **Tutorials** | Explain problems, boundaries, patterns, and tradeoffs |
| **Samples** | Demonstrate the boundary in small executable form |
| **Tests** | Verify architectural invariants |
| **Labs** | Turn reading into architectural judgment |
| **Working repositories** | Provide fuller implementation context |

The structure is meant to support progressive responsibility rather than passive reading.

## Continue Learning

- [Getting Started](index.md)
- [Learning Path Map](learning-path-map.md)
- [Find Your Path](find-your-path.md)
- [Tutorials](../tutorials/index.md)
- [Executable Samples](../samples/index.md)
- [Hands-On Labs](../labs/index.md)

---

> **Read it. Run it. Question it. Improve it.**
