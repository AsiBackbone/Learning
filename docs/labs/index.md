# Labs

Labs are the **practice and reasoning layer** of ASI Backbone Learning.

Tutorials explain architectural boundaries.

Executable samples demonstrate them.

Labs ask you to work with those boundaries yourself.

The intended progression is:

```text
Tutorial
   ↓
Executable Sample
   ↓
Hands-On Lab
   ↓
Alternative Approach
   ↓
Working Repository
```

## Current Status

The lab navigation foundation is established, and the first beginner hands-on lab is now available.

Additional labs will appear in this section as the foundational learning path becomes increasingly interactive.

The initial lab pairs the Decision Before Execution tutorial with its executable companion sample and asks the learner to break, repair, and extend the execution boundary.

## Available Labs

### Decision Before Execution

[Decision Before Execution](decision-before-execution.md)

**Difficulty:** Beginner

Break the execution boundary deliberately, observe why correct decision values are insufficient when the host ignores them, repair the boundary, and add a new policy constraint without moving governance logic into the executor.

Related material:

- [Decision Before Execution tutorial](../tutorials/decision-before-execution.md)
- [Decision Before Execution sample](../../samples/decision-before-execution/README.md)

## Start with the Tutorials

The foundational tutorial sequence establishes the concepts that the initial labs will build upon:

[Browse Foundational Tutorials](../tutorials/index.md)

Topics include:

* Decision before execution
* Explicit policy context and decision outcomes
* Acknowledgment and audit residue
* Scoped capability and host-owned execution
* Governed AI tool gateways

## Study the Executable Samples

The executable sample area provides small runnable demonstrations corresponding to the tutorial concepts.

[Browse Executable Samples](https://github.com/AsiBackbone/Learning/tree/main/samples)

Samples demonstrate known behavior.

Labs will increasingly ask learners to modify, repair, critique, or extend that behavior.

## Inspect the Working Repositories

After working through a teaching example or lab, compare the smaller architecture with fuller implementations.

## AsiBackbone

[AsiBackbone/AsiBackbone](https://github.com/AsiBackbone/AsiBackbone)

A .NET governance and policy-control framework providing fuller implementations of policy evaluation, structured decisions, acknowledgment workflows, audit residue, scoped capability, and host-owned execution.

## NetCoreApplicationTemplate

[AsiBackbone/NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

An ASP.NET Core reference architecture demonstrating secure defaults, middleware organization, structured logging, rate limiting, authentication-ready design, data-access patterns, and operational application structure.

## Learning Principle

The objective of a lab is not simply to reproduce a tutorial.

A useful lab should require you to make a decision, identify a failure mode, improve an architecture, or explain why one implementation is preferable under a particular set of constraints.

> **Read it. Run it. Question it. Improve it.**

