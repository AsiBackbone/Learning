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

The lab navigation foundation is established, with four beginner labs, two intermediate labs, and one advanced capstone lab now available.

Additional labs will appear in this section as the learning path expands into deeper architecture, security, and AI-governance topics.

The foundational labs pair all five governance tutorials with executable companion samples and ask learners to modify, challenge, extend, and threat-model the demonstrated boundaries. The ASP.NET Core diagnostic lab begins the next learning area by requiring learners to predict, observe, repair, and explain middleware-ordering behavior.

## Available Labs

### Decision Before Execution

[Decision Before Execution](decision-before-execution.md)

**Difficulty:** Beginner

Break the execution boundary deliberately, observe why correct decision values are insufficient when the host ignores them, repair the boundary, and add a new policy constraint without moving governance logic into the executor.

Related material:

- [Decision Before Execution tutorial](../tutorials/decision-before-execution.md)
- [Decision Before Execution sample](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-before-execution/README.md)

### Policy Context and Explicit Decision Outcomes

[Policy Context and Explicit Decision Outcomes](policy-context-and-explicit-decision-outcomes.md)

**Difficulty:** Beginner

Collapse structured decisions back to a boolean to observe the information loss, extend the explicit policy context, add a stable reason-coded rule, and make rule precedence observable and testable.

Related material:

- [Policy Context and Explicit Decision Outcomes tutorial](../tutorials/policy-context-and-explicit-decision-outcomes.md)
- [Policy Context and Explicit Decision Outcomes sample](https://github.com/AsiBackbone/Learning/blob/main/samples/policy-context-and-explicit-decision-outcomes/README.md)

### Identify and Remove a Hidden Execution Side Effect

[Identify and Remove a Hidden Execution Side Effect](hidden-execution-side-effect.md)

**Difficulty:** Beginner

Start from deliberately flawed policy code that quietly starts an external deployment while it is still "checking" the request, make the denied-side-effect invariant fail visibly, and refactor the workflow so evaluation is observational and execution occurs only after an explicit decision boundary.

Related material:

- [Decision Before Execution tutorial](../tutorials/decision-before-execution.md)
- [Decision Before Execution sample](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-before-execution/README.md)
- [Decision Before Execution lab](decision-before-execution.md)

### Identify Middleware Ordering Problems

[Identify Middleware Ordering Problems](identify-middleware-ordering-problems.md)

**Difficulty:** Beginner

Run the deliberately incorrect ASP.NET Core pipeline, predict and observe the failure boundary, encode the repaired behavior in a focused test, reorder a disposable copy of the sample, and explain the changed request/response behavior in terms of wrapping, reachability, and coverage rather than a memorized middleware list.

Related material:

- [Middleware Ordering Changes Behavior](../aspnetcore/middleware-ordering-changes-behavior.md)
- [Middleware Ordering Changes Behavior sample](https://github.com/AsiBackbone/Learning/blob/main/samples/middleware-ordering-changes-behavior/README.md)
- [ASP.NET Core learning area](../aspnetcore/index.md)

### Acknowledgment and Audit Residue

[Acknowledgment and Audit Residue](acknowledgment-and-audit-residue.md)

**Difficulty:** Intermediate

Break the acknowledgment boundary deliberately, add another response-binding failure, expose replay as a state problem, preserve correlated evidence behind an in-memory store, and distinguish an allowed decision from a failed execution.

Related material:

- [Acknowledgment and Audit Residue tutorial](../tutorials/acknowledgment-and-audit-residue.md)
- [Acknowledgment and Audit Residue sample](https://github.com/AsiBackbone/Learning/blob/main/samples/acknowledgment-and-audit-residue/README.md)

### Scoped Capability and Host-Owned Execution

[Scoped Capability and Host-Owned Execution](scoped-capability-and-host-owned-execution.md)

**Difficulty:** Intermediate

Break expiration and resource-freshness checks deliberately, observe how stale authority can reach execution, restore narrow execution-boundary validation, and extend the sample with additional binding and replay exercises.

Related material:

- [Scoped Capability and Host-Owned Execution tutorial](../tutorials/scoped-capability-and-host-owned-execution.md)
- [Scoped Capability and Host-Owned Execution sample](https://github.com/AsiBackbone/Learning/blob/main/samples/scoped-capability-and-host-owned-execution/README.md)

### Governed AI Tool Gateway

[Governed AI Tool Gateway](governed-ai-tool-gateway.md)

**Difficulty:** Advanced

Compose the foundational patterns into one AI-assisted execution boundary, deliberately weaken proposal, context, acknowledgment, capability, replay, prompt, credential, and failure-mode controls, then threat-model the complete gateway.

Related material:

- [Governed AI Tool Gateway tutorial](../tutorials/governed-ai-tool-gateway.md)
- [Governed AI Tool Gateway sample](https://github.com/AsiBackbone/Learning/blob/main/samples/governed-ai-tool-gateway/README.md)

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

[Browse Executable Samples](../samples/index.md)

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

