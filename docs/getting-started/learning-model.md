---
description: Understand ASI Backbone Learning's problem-first teaching model, the relationship among tutorials, samples, tests, and labs, and how canonical and alternative patterns are presented.
---

# Learning Model

ASI Backbone Learning is designed as a living architecture-learning resource rather than a product manual or a framework adoption funnel.

The guiding principle is:

> **Read it. Run it. Question it. Improve it.**

A reader should be able to study an architectural boundary, observe it in a small implementation, test the claimed invariant, challenge the design, and adapt the useful parts without adopting an entire framework.

You do not need to install an `AsiBackbone` package to use this material.

## Problem First, Product Second

Where practical, tutorials follow a common progression:

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

The architectural problem should be understandable before a reader is asked to care about a particular implementation. A tutorial should remain useful even when the reader decides that a simpler design, a framework-native feature, or a different architecture is a better fit.

This is why Learning favors questions such as:

- What boundary is missing?
- What failure becomes possible when responsibilities are combined?
- Which invariant should be observable?
- What is the smallest architecture that preserves the boundary we need?
- What tradeoff are we accepting by adding another policy, acknowledgment, capability, or gateway step?

Working repositories are then used as architectural specimens rather than as unquestioned templates.

## Tutorial, Sample, Test, Lab

The foundational learning path uses four complementary forms:

```text
Tutorial
   ↓
Runnable Sample
   ↓
Architectural Invariant Tests
   ↓
Hands-On Lab
```

Each form has a different job.

### Tutorials Explain

Tutorials introduce an architectural problem and walk through one or more ways to reason about it.

They should emphasize:

- why the problem matters,
- the common or naive implementation,
- failure modes or limitations,
- the architectural pattern being examined,
- a minimal teaching example,
- tradeoffs and alternatives,
- and links to fuller working examples when useful.

A tutorial is not a claim that every application requires the demonstrated pattern.

### Samples Demonstrate

Runnable samples make the architectural boundary observable without requiring the reader to extract it from a large application.

A good teaching sample should be intentionally small, deterministic where practical, and focused on the behavior under examination.

For example:

```text
Denied decision
   ↓
Executor invocation count = 0
```

The sample exists to make the boundary visible, not to serve as a production framework.

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

When a tutorial says that a boundary exists, the companion test should make that boundary difficult to misunderstand.

### Labs Make You Decide

Labs move from explanation to active reasoning.

A lab may provide:

- a partially implemented application,
- a broken or incomplete architecture,
- a policy-design exercise,
- a security or governance scenario,
- a set of tests that must be made to pass,
- or an architecture the learner is asked to critique or improve.

The learner may need to identify hidden side effects, separate evaluation from execution, introduce explicit decision outcomes, preserve acknowledgment boundaries, validate scoped authority, detect stale authority, threat-model an AI gateway, or compare alternate designs.

Tutorials explain. Samples demonstrate. Tests verify. Labs make you decide.

## Canonical and Alternative Patterns

Learning does not exist to prove that one architecture is always correct.

Material may therefore distinguish between two kinds of patterns.

### Canonical Pattern

A canonical pattern is aligned with the current architecture of one or more ASI Backbone organization projects.

It answers a practical question:

> How do the working repositories currently approach this problem?

Canonical does not mean universal, required, or superior in every context.

### Alternative Pattern

An alternative pattern is a technically grounded approach that solves the same problem differently.

Alternative patterns are useful when they make tradeoffs visible, demonstrate that a simpler mechanism is sufficient, or show how another ecosystem solves the same boundary problem.

The first published alternative-pattern comparison is [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md).

A useful rule is:

> **Use the smallest architecture that preserves the boundaries you actually need.**

Architectural disagreement can be educational when the alternatives are explained clearly and evaluated on their tradeoffs.

## Framework Independence

Learning should remain useful to readers who never install `AsiBackbone` and never use `NetCoreApplicationTemplate`.

Readers are encouraged to:

- study individual patterns without adopting an entire framework,
- copy or adapt useful ideas into their own systems,
- compare the demonstrated approach with framework-native or external alternatives,
- remove complexity that their application does not need,
- question assumptions and identify tradeoffs,
- submit corrections, examples, diagrams, tutorials, and alternative approaches,
- and use the implementation repositories as working architectural specimens.

If a developer studies a pattern here, improves it, adapts it to another system, or uses it to make a better architectural decision, Learning is serving its purpose.

## Why the Material Is Structured This Way

The repository teaches architecture through progressive responsibility:

```text
Understand the problem
        ↓
Observe a boundary
        ↓
Verify the invariant
        ↓
Experiment with the design
        ↓
Compare alternatives
        ↓
Inspect fuller implementations when useful
```

That progression keeps explanation close to evidence while avoiding two common failure modes:

1. presenting a large implementation before the reader understands the boundary it is trying to preserve; and
2. presenting an abstract principle without runnable evidence that shows what the principle means in practice.

The result is intentionally layered. The root README is a front door. Getting Started routes readers. Tutorials provide depth. Samples and tests make claims observable. Labs turn reading into architectural judgment. Working repositories provide fuller implementation context.

## Continue Learning

- [Getting Started](index.md)
- [Learning Path Map](learning-path-map.md)
- [Find Your Path](find-your-path.md)
- [Tutorials](../tutorials/index.md)
- [Executable Samples](../samples/index.md)
- [Hands-On Labs](../labs/index.md)

---

> **Read it. Run it. Question it. Improve it.**
