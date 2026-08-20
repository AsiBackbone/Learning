# Advanced

The Advanced section is reserved for topics that build on the foundational ASI Backbone Learning material and require deeper architectural reasoning, broader system context, or comparison among competing approaches.

Advanced does not mean that a pattern is automatically better.

It means that the problem usually contains more interacting boundaries, assumptions, failure modes, or tradeoffs.

> **Complexity should be earned by the problem, not introduced by habit.**

> **Section status:** This page is the advanced-material overview. The first dedicated advanced article, [Regional and Tenant Policy Overlays](regional-and-tenant-policy-overlays.md), is now published; begin with the [Foundational Tutorials](../tutorials/index.md) and [Hands-On Labs](../labs/index.md) before moving into the advanced material.

## Before Continuing

Readers should generally be familiar with the foundational tutorial sequence:

1. [Decision Before Execution](../tutorials/decision-before-execution.md)
2. [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md)
3. [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md)
4. [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md)
5. [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md)

These tutorials establish the recurring vocabulary used throughout the Learning repository:

```text
Intent
   ↓
Context
   ↓
Constraints
   ↓
Decision
   ↓
Acknowledgment when required
   ↓
Scoped Authority
   ↓
Host-Owned Execution
   ↓
Audit Residue
```

Advanced material may stretch, combine, distribute, or challenge these boundaries, but it should make those changes explicit.

## What Makes a Topic Advanced?

A topic may belong here when it introduces concerns such as:

* Multiple policy authorities
* Distributed execution
* Cross-service trust
* Delegated authority
* Competing decision outcomes
* Replay resistance
* Revocation
* Partial failure
* Regional or tenant-specific policy
* Durable governance evidence
* Multi-agent coordination
* Degraded operation
* Recovery and compensation
* Conflicting architectural goals

The objective is not to maximize architectural sophistication.

The objective is to understand what additional complexity buys and what new failure modes it introduces.

## Potential Advanced Topics

Future material may include:

* Policy composition and conflict resolution
* Delegated and nested authority
* Replay-resistant capability workflows
* Distributed execution boundaries
* Cross-service governance
* Multi-tenant policy systems
* Regional policy overlays
* Policy-version drift
* Durable governance evidence
* Tamper-evident receipts
* Capability revocation and cancellation
* Partial failure and recovery
* Degraded-mode governance
* Distributed acknowledgment workflows
* Cross-boundary correlation
* AI agent orchestration
* Multi-agent execution boundaries
* Threat modeling governed AI gateways
* Alternative policy architectures
* Architecture comparisons
* Failure-mode analysis

## Policy Composition

A simple system may evaluate one clear policy boundary.

More complex environments may combine:

```text
Organization Policy
        ↓
Regional Policy
        ↓
Tenant Policy
        ↓
Application Policy
        ↓
Operation-Specific Constraints
```

This introduces questions such as:

* Which policy has precedence?
* Can one layer narrow authority granted by another?
* Can one layer broaden it?
* What happens when policies disagree?
* Which policy versions participated in the final decision?
* What evidence should be preserved?

An advanced implementation should make precedence and conflict behavior explicit rather than relying on incidental execution order.

## Distributed Governance

A governance boundary becomes more difficult to reason about when evaluation and execution occur in different services.

For example:

```text
Service A
   ↓
Governance Decision
   ↓
Scoped Authority
   ↓
Network Boundary
   ↓
Service B
   ↓
Authority Validation
   ↓
Execution
```

Questions then include:

* How does Service B verify the authority?
* Which service owns authoritative context?
* How is expiration handled?
* What happens if policy changes after issuance?
* Can authority be replayed?
* How is revocation communicated?
* How are events correlated across services?

The separation between **decision** and **execution** remains useful, but the trust boundary becomes more explicit.

## Delegated Authority

Delegation may allow one trusted component to issue narrower authority derived from a broader grant.

A conceptual flow might be:

```text
Original Authority
   ↓
Delegation
   ↓
Narrower Capability
   ↓
Execution Boundary
```

A safe delegation model should consider whether authority is being:

* Narrowed
* Extended in duration
* Transferred to another actor
* Rebound to another resource
* Reissued to another audience
* Used more than once

A derived capability should not silently become more powerful than the authority from which it originated.

## Replay, Revocation, and Bounded Use

Short-lived authority does not automatically eliminate replay risk.

Advanced capability designs may need to consider:

* Single-use semantics
* Nonces
* Replay caches
* Idempotency keys
* Consumption records
* Revocation lists
* Policy-version checks
* State changes between approval and execution

For example:

```text
Capability Issued
   ↓
Resource Changes
   ↓
Capability Presented
   ↓
Binding Revalidation
   ↓
Execute or Reject
```

The correct behavior depends on what the capability represents and what guarantees the system requires.

## Partial Failure

Distributed systems rarely fail cleanly.

A governed operation may successfully pass policy evaluation but fail during execution:

```text
Decision = Allow
   ↓
Capability Valid
   ↓
Execution Starts
   ↓
Partial Side Effect
   ↓
Failure
```

Advanced material should ask:

* Is retry safe?
* Could retry duplicate the operation?
* Is compensation possible?
* Does the original capability remain valid?
* Should a new governance decision be required?
* What audit residue should distinguish attempted, partial, failed, and completed execution?

Governance does not remove distributed-systems failure modes.

It should make their consequences easier to reason about.

## Degraded-Mode Governance

Production systems may need to decide what happens when a dependency is unavailable.

For example:

```text
Request
   ↓
Policy Dependency Unavailable
   ↓
?
```

Possible responses include:

```text
Fail Closed
Fail Open
Defer
Escalate
Use Cached Decision Data
Permit Limited Operation
```

There is no universal answer.

A safe degraded-mode strategy depends on:

* Consequence severity
* Data sensitivity
* Operational urgency
* Policy freshness
* Available evidence
* Recovery behavior

The important architectural requirement is that degraded behavior should be explicit rather than accidental.

## Regional and Tenant-Specific Policy

A shared platform may need to support policy differences across jurisdictions, organizations, or tenants.

A possible model is:

```text
Global Baseline
   ↓
Regional Constraints
   ↓
Tenant Constraints
   ↓
Operation Context
   ↓
Decision
```

Advanced questions include:

* Which rules are mandatory globally?
* Which may vary by tenant?
* Can regional policy only narrow the baseline?
* How are policy identities recorded?
* How is configuration drift detected?
* How are conflicting requirements surfaced?

These concerns become increasingly important when governance spans organizational or geographic boundaries.

Continue with [Regional and Tenant Policy Overlays](regional-and-tenant-policy-overlays.md) for a dedicated treatment of policy authority, narrowing versus broadening, explicit override paths, conflict handling, multi-policy provenance, drift, degraded mode, and overlay testing.

## AI Agent Orchestration

The foundational AI material assumes that a model may propose an operation while the host retains execution authority.

Multi-agent systems add another layer:

```text
Agent A
   ↓
Proposal

Agent B
   ↓
Interpretation or Planning

Host
   ↓
Authoritative Context
   ↓
Governance
   ↓
Execution
```

The addition of more agents should not automatically distribute execution authority.

Useful questions include:

* Which outputs are proposals?
* Which component owns authoritative facts?
* Can one agent authorize another?
* How are tool requests normalized?
* Where are arguments validated?
* Which component owns credentials?
* How are multi-step workflows bounded?
* How is authority constrained between steps?

The foundational rule still applies:

> **The model may propose. The host retains execution authority.**

## Alternative Architectures

Advanced material is a natural place to compare competing approaches.

A useful comparison may examine:

```text
Approach A
   ↓
Benefits
Costs
Failure Modes
Operational Assumptions

        versus

Approach B
   ↓
Benefits
Costs
Failure Modes
Operational Assumptions
```

The objective is not to prove that one approach is universally correct.

The objective is to make the consequences of each design visible.

## Challenge the Canonical Pattern

Some Learning material reflects architectural patterns currently used by ASI Backbone organization repositories.

Those patterns should remain open to criticism.

Advanced learning may deliberately ask:

* Can this boundary be simplified?
* Can the same invariant be achieved with fewer abstractions?
* Which assumptions fail under distributed execution?
* What happens during partial failure?
* What happens when policy versions diverge?
* What happens when authority crosses service boundaries?
* What happens when acknowledgment becomes stale?
* What evidence is actually necessary?
* Which protections belong in infrastructure instead?
* When should a framework step aside and let the host own the concern directly?

A canonical pattern documents a useful current approach.

It is not immune from improvement.

## Experiments and Failure Injection

Advanced topics are especially well suited to experimentation.

Useful exercises may include:

* Expiring authority immediately before execution
* Changing a resource after approval
* Replaying a consumed capability
* Removing a policy dependency
* Introducing conflicting policy outcomes
* Simulating network partitions
* Failing halfway through a multi-step operation
* Sending an AI-generated unknown tool request
* Changing tenant or regional policy during a workflow
* Attempting execution with stale policy evidence

These scenarios help expose architectural assumptions that may remain hidden during normal execution.

## Working Repository References

Advanced Learning material may use both primary ASI Backbone organization repositories as implementation specimens.

### AsiBackbone

[AsiBackbone/AsiBackbone](https://github.com/AsiBackbone/AsiBackbone)

Provides fuller governance and policy-control implementations that can be studied for:

* Decision pipelines
* Acknowledgment
* Audit residue
* Capability boundaries
* Host-owned execution
* AI governance integration

### NetCoreApplicationTemplate

[AsiBackbone/NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

Provides a fuller ASP.NET Core reference architecture for studying:

* Application boundaries
* Middleware
* Security defaults
* Logging
* Failure handling
* Rate limiting
* Authentication-ready design
* Operational structure

Learning should use these repositories as specimens rather than duplicate their complete implementation documentation.

## Scope and Boundaries

Advanced examples remain educational artifacts.

They do not automatically provide:

* Production readiness
* Security certification
* Compliance
* Legal conformity
* Distributed-systems correctness
* Replay protection
* High availability
* Fault tolerance
* AI safety guarantees

Each pattern still requires evaluation against the actual application's threat model, operational environment, and consequences of failure.

## Current Status

The Advanced section is established as a destination for later-stage architectural material.

The current repository priority is to strengthen the foundational learning path through executable companion samples, tests, labs, and working implementation references before substantially expanding the advanced curriculum.

For now:

* Complete the [Foundational Tutorials](../tutorials/index.md).
* Explore the [Hands-On Labs](../labs/index.md) as they are published.
* Compare the smaller teaching patterns with the working implementation repositories.

---

> **Read it. Run it. Question it. Improve it.**

