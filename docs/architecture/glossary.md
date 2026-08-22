---
description: Canonical quick-reference definitions for ASI Backbone Learning architecture terminology across governance, security, AI integration, and host-owned execution.
---

# Architecture Glossary

This glossary is the canonical vocabulary reference for ASI Backbone Learning.

It defines how recurring architecture terms are used across tutorials, labs, samples, governance material, security material, and AI-integration material. It does not claim that the underlying architecture or security concepts originated with ASI Backbone, and it does not redefine established industry terminology when an established meaning is already sufficient.

> **Canonical here means canonical within the Learning repository.** It does not mean standardized by an external body, universally correct for every system, or permanently coupled to one implementation API.

Use [Terminology and Established Architecture Concepts](terminology-and-established-concepts.md) when you need lineage, comparisons with established concepts, or external terminology anchors. Use this page when you need the shortest consistent answer to "What does this term mean in Learning?"

## How to Read the Definitions

Each definition is labeled with one or more scopes:

- **General architecture** — an established or broadly recognizable software architecture, security, workflow, or provenance concept.
- **Learning usage** — a term whose meaning is narrowed, composed, or emphasized in a specific way by ASI Backbone Learning.
- **Implementation correspondence** — a current `AsiBackbone/AsiBackbone` API or type that embodies the term. Implementation mappings can evolve without changing the architectural definition.

The glossary deliberately separates architectural meaning from product-specific type names. A tutorial may use framework-neutral sample types while the `AsiBackbone` package uses different concrete names.

## Request, Policy, and Decision Vocabulary

### Intent

A structured representation of a proposed operation before the operation causes a side effect.

**Scope:** General architecture; Learning usage.

In Learning, intent is proposal data. Creating, receiving, or validating an intent does not itself authorize or execute the operation.

### Policy Context

The explicit snapshot of decision-relevant facts supplied to policy evaluation, such as actor, operation, resource, tenant or region, risk, classification, policy metadata, and correlation information.

**Scope:** General architecture; Learning usage.

Learning emphasizes that security-sensitive context should come from authoritative host sources when possible. Model-supplied, client-supplied, or stale values may be proposal data without being authoritative policy context.

### Constraint

An independently testable rule or condition that contributes to a governance decision without performing the governed side effect.

**Scope:** General architecture; Learning usage.

A constraint can allow, block, warn, defer, require acknowledgment, recommend escalation, or contribute reason data depending on the decision model. Keeping constraints side-effect free preserves the decision-before-execution boundary.

### Policy Evaluation

The process of applying the active policy structure to a policy context and composing the resulting constraint information into a governance decision.

**Scope:** General architecture; Learning usage.

Policy evaluation answers what should happen next. It is distinct from the executor that performs the real-world operation.

### Decision Outcome

A structured lifecycle state returned by policy evaluation that communicates what the governed workflow should do next.

**Scope:** General architecture; Learning usage.

A decision outcome is not the same as an execution result. The policy may allow an operation that later fails during execution, and a policy denial may prevent execution from starting at all.

### Allow

A decision outcome stating that policy permits immediate continuation through the governed workflow.

**Scope:** Learning usage; implementation correspondence.

Allow is permission to continue, not an instruction to perform the side effect and not an unbounded grant of future authority. The host still owns the execution boundary.

### Deny

A decision outcome stating that the proposed operation must not proceed through the governed execution path.

**Scope:** Learning usage; implementation correspondence.

A denied decision should preserve the architectural invariant that the executor is never invoked for that governed attempt.

### Defer

A decision outcome stating that the operation should pause and be reconsidered later, retried under defined conditions, or routed to another process without treating the current result as final permission.

**Scope:** Learning usage; implementation correspondence.

Deferral is not implicit approval. A later attempt should reconstruct the context and evaluate the policy required for that later execution point.

### Require Acknowledgment

A decision outcome stating that the operation must pause until a defined condition, warning, responsibility statement, or risk is explicitly acknowledged.

**Scope:** Learning usage; implementation correspondence.

Acknowledgment does not override policy. After acknowledgment, the host may still need to rebuild current context, re-evaluate policy, validate authority, and reject execution if conditions changed.

### Escalate

A decision outcome stating that the operation should move to a higher review, authority, or workflow path before execution.

**Scope:** Learning usage; implementation correspondence.

Escalation is a routing decision, not execution authority. A later approval or review must still produce whatever authority the execution boundary requires.

## Accountability, Evidence, and Policy Identity

### Acknowledgment

A recorded response showing that a defined challenge, condition, warning, or responsibility statement was presented and accepted by the identified actor or system.

**Scope:** General architecture; Learning usage; implementation correspondence.

Acknowledgment is distinct from authentication, authorization, approval by another authority, and execution authority.

### Audit Residue

The structured evidence left by a governed lifecycle so that the path from proposal to decision and execution can be reconstructed.

**Scope:** Learning usage; implementation correspondence.

Audit residue may include correlation identifiers, policy identity, policy version or fingerprint, decision outcomes, reason codes, acknowledgment evidence, capability events, and execution results. The term does not prescribe one storage technology and does not by itself claim durability, immutability, cryptographic signing, or tamper evidence.

### Decision Provenance

The traceable relationship among the inputs, policy material, decisions, acknowledgments, authority grants, and execution events associated with a governed operation.

**Scope:** General architecture; Learning usage.

Decision provenance is broader than one log entry. It is the model that lets a reviewer answer which facts and policy produced a decision and how that decision related to any later side effect.

### Policy Identity

A stable logical identifier for the policy, ruleset, policy family, or policy source that participated in evaluation.

**Scope:** General architecture; Learning usage.

Policy identity answers "which policy was this?" It is distinct from policy version and policy fingerprint. A host may choose a name, URI, key, database identifier, or other stable identifier appropriate to its policy system.

### Policy Version

A human-readable or system-readable version label identifying a generation or revision of policy material.

**Scope:** General architecture; implementation correspondence.

Policy version helps operators compare decisions over time, but two policy documents with the same version label are not automatically proven to contain identical effective rules.

### Policy Fingerprint

A stable content-derived digest, hash, or equivalent fingerprint for the effective policy material used by a decision.

**Scope:** General architecture; Learning usage; implementation correspondence.

A fingerprint helps distinguish exact policy content more strongly than a version label. It identifies bytes or normalized configuration, not the correctness, safety, or semantic equivalence of the policy.

## Authority and Execution Vocabulary

### Scoped Capability

A narrowly bounded grant of authority associated with a specific actor, operation, resource, audience, policy state, acknowledgment state, lifetime, use count, or other execution-relevant bindings.

**Scope:** General architecture; Learning usage; implementation correspondence.

The architectural concept is the narrow grant itself. It is not tied to JWT, OAuth, or any other token format.

### Capability Token

A concrete transferable representation of a scoped capability, commonly short-lived and integrity protected.

**Scope:** General architecture; Learning usage; implementation correspondence.

A token format does not create least privilege by itself. The execution boundary must validate the token's scope, bindings, freshness, audience, policy state, and any replay or revocation rules that matter to the operation.

### Execution Authority

The currently valid permission recognized at the execution boundary to cause a specific side effect.

**Scope:** General architecture; Learning usage.

Execution authority is distinct from a model proposal, an allowed policy decision, an acknowledgment, or an approval record. Those artifacts may contribute to authority, but none should silently become broader authority than the executor is prepared to validate.

### Host-Owned Execution

The Learning boundary in which the trusted application host retains control of the component that performs the real side effect and validates the authority required immediately before that side effect.

**Scope:** Learning usage.

A policy evaluator, AI model, workflow participant, or remote caller may propose or permit an operation without owning the executor. Host ownership keeps the final transition into real-world effects inside a trusted application boundary.

### Governed Execution

The Learning composition in which a proposed operation passes through explicit context, policy evaluation, lifecycle outcomes, acknowledgment or escalation when needed, scoped authority when needed, host-owned execution, and retained evidence.

**Scope:** Learning usage.

Governed execution is broader than ordinary authorization and narrower than a claim about a complete workflow or compliance platform. Use it only when the extra lifecycle boundaries solve a real problem.

### Operational Gateway

A host-owned mediation boundary between a governed decision or authority artifact and an external tool, API, device, workflow engine, or other side-effecting system.

**Scope:** General architecture; Learning usage.

The gateway validates the operation that is actually about to occur. It should not treat an upstream proposal, model output, stale decision, or token as self-validating authority.

### Trust Boundary

A point where data, control, identity, or authority crosses between components with different trust assumptions.

**Scope:** General architecture.

At a trust boundary, inputs may need validation, normalization, reconstruction from authoritative sources, integrity checks, scope checks, or rejection. Learning uses trust boundaries to make explicit where proposal data must stop being trusted implicitly.

### Architectural Invariant

A property that must remain true across valid implementations of an architectural pattern.

**Scope:** General architecture; Learning usage.

Examples in Learning include "a blocked decision never reaches the executor" and "a model proposal is not execution authority." An invariant describes a property to preserve, not one required code shape.

## AI Tool-Mediation Vocabulary

### Tool Proposal

A structured request, often emitted by an AI model, naming a candidate tool or operation and proposed arguments for the host to consider.

**Scope:** General AI integration; Learning usage.

A tool proposal is untrusted proposal data. It is not evidence that the tool exists, that the arguments are valid, that the model supplied authoritative context, or that execution is authorized.

### Tool Allowlist

The host-owned set of tools, operations, or handlers that are eligible to be considered for invocation.

**Scope:** General security and AI integration.

An allowlist blocks unknown or unapproved tool names from reaching handlers. Membership in the allowlist does not by itself authorize a specific invocation; policy, argument validation, context, and execution authority may still block the operation.

## Current AsiBackbone Implementation Correspondence

The Learning glossary is architectural first. The current [`AsiBackbone/AsiBackbone` implementation glossary](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/glossary.md) provides the implementation-side vocabulary and API cross-references.

The most direct correspondences are:

| Learning term | Current AsiBackbone correspondence |
| --- | --- |
| Intent / request | `AsiBackboneConstraintEvaluationContext` carries proposed operation data; there is not one universal `Intent` base type. |
| Policy context | `IAsiBackboneConstraintEvaluationContext`, `AsiBackboneConstraintEvaluationContext` |
| Constraint | `IAsiBackboneConstraint<TContext>`, `ConstraintEvaluationResult` |
| Policy evaluation | `IAsiBackbonePolicyEvaluator<TContext>`, `DefaultAsiBackbonePolicyEvaluator<TContext>` |
| Decision outcome | `GovernanceDecision`, `GovernanceDecisionOutcome` |
| Allow | `GovernanceDecisionOutcome.Allowed` |
| Deny | `GovernanceDecisionOutcome.Denied` |
| Defer | `GovernanceDecisionOutcome.Deferred` |
| Require acknowledgment | `GovernanceDecisionOutcome.AcknowledgmentRequired` |
| Escalate | `GovernanceDecisionOutcome.EscalationRecommended` |
| Acknowledgment | `LiabilityHandshakeAcknowledgment`; ASP.NET Core challenge support also exposes acknowledgment challenge types and services. |
| Audit residue | `AuditResidue`; durable ledger support includes `AuditLedgerRecord` and `IAsiBackboneAuditLedgerStore`. |
| Policy version | `GovernanceDecision.PolicyVersion` |
| Policy fingerprint | `GovernanceDecision.PolicyHash` |
| Scoped capability / capability token | `CapabilityTokenGrant`, `CapabilityGrantValidator` |
| Operational gateway | Implemented as a host-owned architecture pattern rather than one universal gateway base type. |

The current implementation also defines `GovernanceDecisionOutcome.Warning`, which permits continuation while retaining warning reasons. Foundational Learning tutorials may omit `Warning` when a smaller outcome set keeps the teaching example focused; that simplification should not be read as a claim that the implementation enum has only five members.

Not every Learning term has, or should have, a one-to-one concrete type. Terms such as host-owned execution, governed execution, trust boundary, decision provenance, and architectural invariant describe relationships across components rather than one class or interface.

## Important Distinctions

| Do not collapse these concepts | Why the distinction matters |
| --- | --- |
| Allow decision ≠ execution | Policy can permit continuation while the host still controls whether and how the side effect occurs. |
| Acknowledgment ≠ authorization | Accepting a warning or responsibility statement does not grant access-control permission. |
| Acknowledgment ≠ execution authority | A valid acknowledgment may still require re-evaluation and a fresh scoped grant. |
| Scoped capability ≠ token format | The authority model matters more than whether the artifact is a JWT or another encoding. |
| Policy version ≠ policy fingerprint | A version is a label; a fingerprint is content-derived identity. |
| Tool proposal ≠ tool invocation | Model output remains proposal data until the host validates and authorizes execution. |
| Tool allowlist ≠ permission to invoke | Eligibility is only one gate in a governed execution path. |
| Audit residue ≠ tamper-proof evidence | Storage, durability, signing, integrity, and retention are separate implementation concerns. |
| Decision outcome ≠ operation result | Governance may block execution entirely, while an allowed operation can still fail when executed. |

## Related Learning Material

Foundational tutorials introduce the terms in context:

1. [Decision Before Execution](../tutorials/decision-before-execution.md)
2. [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md)
3. [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md)
4. [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md)
5. [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md)

For lineage and established terminology, continue with [Terminology and Established Architecture Concepts](terminology-and-established-concepts.md).
