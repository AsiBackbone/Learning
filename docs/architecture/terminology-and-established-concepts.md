---
description: Map ASI Backbone Learning terminology to established software architecture, authorization, security, workflow, provenance, and AI-governance concepts.
---

# Terminology and Established Architecture Concepts

ASI Backbone Learning uses a consistent vocabulary to make recurring architectural boundaries easier to teach, test, and compare.

That vocabulary is **not** a claim that the underlying software architecture, security, authorization, workflow, provenance, or AI-governance ideas originated with ASI Backbone.

Many of the related concepts predate this repository by years or decades.

Some labels, including `Governed Execution`, `Audit Residue`, `Host-Owned Execution`, and `Governed AI Tool Gateway`, are repository-local teaching or composition terms rather than external standards terminology. They are signposts for boundaries that the tutorials want to keep visible.

> **ASI Backbone Learning often gives a consistent name to a boundary or composition of established architectural ideas. The terminology is intended to make those boundaries teachable and reusable, not to erase their technical lineage.**

The intended relationship is:

```text
Established concept
        ↓
ASI Backbone Learning terminology
        ↓
Specific teaching boundary or composition
```

not:

```text
Established concept
        ↓
Renamed as something new
```

## Terminology Map

| Learning term | Related established concepts | Important distinction in Learning |
| --- | --- | --- |
| **Governed Execution** | Policy Decision Point (PDP) / Policy Enforcement Point (PEP) separation, workflow governance, policy engines, reference-monitor-style mediation | A proposed operation may have lifecycle outcomes beyond allow/deny, including defer, acknowledgment, or escalation, before any side effect is permitted. |
| **Decision Before Execution** | Complete mediation, command validation, policy enforcement, command handling, CQRS-adjacent separation | Intent exists independently of the side effect. A denied decision should make it possible to prove that the executor was never invoked. This is not a claim that the pattern is CQRS. |
| **Policy Context** | ABAC subject/object/action/environment attributes, authorization context, request/resource context | Decision-relevant facts are assembled explicitly and preferably from authoritative sources rather than discovered implicitly throughout execution. |
| **Explicit Decision Outcomes** | Result types, workflow states, policy decisions, state-machine transitions | The decision communicates what should happen next instead of compressing all non-success states into `false` or an authorization failure. |
| **Acknowledgment Boundary** | Consent flows, attestation, approval workflows, human-in-the-loop controls | Acknowledgment records that a condition was presented and accepted. It remains distinct from authentication, authorization, approval by another authority, and execution authority. |
| **Audit Residue** | Audit trails, decision logs, event records, provenance | The term describes structured evidence left by the governed lifecycle, including correlation, policy identity, reasons, acknowledgment, authority, and execution evidence. It does not prescribe one logging or storage technology. |
| **Capability-Scoped Authority** | Capability-based security, least privilege, scoped credentials, short-lived access tokens | Authority is narrowly bound to the actor, operation, resource, audience, state, lifetime, and when needed use count or acknowledgment. Merely using a short-lived token does not by itself create the full boundary. |
| **Host-Owned Execution** | Reference monitor, complete mediation, trusted execution boundary, application service boundary | A policy evaluator or model may recommend or permit an action, but the trusted host retains control of the component that performs the real side effect. |
| **Governed AI Tool Gateway** | Tool mediation, agent/tool gateways, reference-monitor patterns, human oversight, application authorization | Model output is treated as a proposal, not authority. The host owns the tool registry, reconstructs authoritative context, evaluates policy, validates execution authority, and invokes the tool. |
| **Governance Spine** | Policy pipeline, orchestration pipeline, control plane, workflow state machine | Learning uses the term for the end-to-end sequence that preserves the boundaries among intent, context, decision, acknowledgment, scoped authority, execution, and evidence. |
| **Canonical Pattern** | Reference architecture, preferred implementation, repository convention | `Canonical` means aligned with the current ASI Backbone reference implementations and teaching path. It does **not** mean an industry standard, universally correct architecture, or requirement for adopters. |

The established concepts in the middle column are conceptual anchors, not declarations of exact equivalence.

A Learning term may intentionally combine several established ideas because the teaching goal is to preserve a lifecycle boundary across them.

## Authorization Versus Governed-Execution Workflow

Authorization and governed execution overlap, but they do not always answer the same question.

A typical authorization question is:

```text
May this actor perform this operation on this resource?
```

A governed-execution workflow may need to answer:

```text
What should happen next with this proposed operation?
```

That second question can include states such as:

```text
Allow
Deny
Defer
RequireAcknowledgment
Escalate
```

If ordinary ASP.NET Core authorization policies, requirements, handlers, and resource-based checks fully express the requirement, use them.

A broader pipeline is justified only when the application genuinely needs additional lifecycle, authority, acknowledgment, escalation, or provenance boundaries.

See [When ASP.NET Core Authorization Is Enough](when-aspnet-core-authorization-is-enough.md) for the full comparison.

## Policy Context and ABAC

Learning's `PolicyContext` is closely related to the inputs used by attribute-based access control.

ABAC commonly evaluates attributes associated with subjects, objects/resources, requested operations, and environmental conditions against policy.

Learning preserves that lineage while emphasizing two teaching concerns:

1. The facts should be represented explicitly enough to inspect and test.
2. Security-sensitive facts should come from authoritative host sources when possible.

For example, a model may propose:

```text
tenant = tenant-a
classification = public
```

but the host may independently determine:

```text
tenant = tenant-b
classification = restricted
```

The model-supplied values may be useful proposal data, but they should not automatically become authoritative policy context.

`PolicyContext` is therefore a repository teaching abstraction, not a replacement name for ABAC or ASP.NET Core's `AuthorizationHandlerContext`.

## Approval, Acknowledgment, Authorization, and Authority

These concepts are related but should not be collapsed into one step.

```text
Authentication
   ↓
Authorization
   ↓
Governance decision
   ↓
Approval or acknowledgment when required
   ↓
Scoped execution authority
   ↓
Execution
```

A particular application may omit several stages.

The distinctions matter when they are present:

- **Authorization** determines whether an actor may enter or perform an operation under access-control policy.
- **Approval** commonly represents a decision by an authorized reviewer or workflow authority.
- **Acknowledgment** records that a specified condition, warning, or responsibility statement was presented and accepted.
- **Execution authority** is the permission actually presented and validated at the execution boundary.

An acknowledgment should not silently become an authorization override.

An approval should not automatically become permanent or unbounded authority.

## Capability-Scoped Authority Is More Than a Token Format

Learning often demonstrates scoped authority with a token-like artifact because it makes the boundary concrete.

That does not mean:

```text
JWT = capability security
```

or:

```text
short expiration = least authority
```

A signed token is an implementation mechanism.

The architectural question is what authority the artifact represents and what the execution boundary validates.

A capability-scoped grant may need to bind:

```text
subject
operation
resource
audience
policy identity
acknowledgment state
not-before / expiration
maximum uses
revocation or cancellation state
```

OAuth access tokens provide a familiar example of credentials with scope and lifetime, but Learning's capability boundary can require more application-specific bindings than an ordinary bearer token provides.

The key lesson is to validate the authority **where the side effect is about to occur**, not merely when the artifact is created.

## Audit Residue, Logging, and Provenance

`Audit residue` is a Learning term for evidence that remains from the governed lifecycle.

It may be stored in logs, events, a database, append-only storage, or another audit system.

The term is intentionally broader than a single log message.

Useful residue may connect:

```text
proposal
policy context identity
policy version or hash
decision outcome
reason codes
acknowledgment
capability issuance and validation
execution result
correlation identifiers
```

A structured log can carry some or all of this information.

But:

```text
logging technology ≠ decision provenance model
```

The architecture still has to decide which events exist, how they correlate, which identifiers are stable, and which records require durable or integrity-protected storage.

W3C PROV is one established vocabulary for representing provenance relationships. Learning does not attempt to replace it; `audit residue` is a smaller teaching label for the evidence left by the repository's governed-execution lifecycle.

## Policy Decision Versus Execution Authority

Policy evaluation and execution are deliberately separated in the foundational material.

A policy decision may state:

```text
Allowed
```

That does not require the policy evaluator itself to perform the operation.

A stronger boundary is:

```text
Policy evaluator
   ↓
Decision
   ↓
Host / gateway validates current authority
   ↓
Executor performs side effect
```

This resembles reference-monitor and complete-mediation thinking: access to a protected operation is checked at a trusted boundary rather than delegated implicitly to whichever component proposed the action.

The Learning-specific emphasis is that the separation remains visible across the complete workflow, including acknowledgment, delayed execution, and AI-assisted proposals.

## AI Tool Calling Versus Host-Governed Tool Execution

AI tool or function calling commonly lets a model emit a structured request describing a tool and arguments.

Learning treats that output as:

```text
proposal
```

not:

```text
authority
```

The host remains responsible for deciding:

- Whether the tool exists.
- Whether the arguments are structurally and semantically valid.
- Which actor and tenant are authoritative.
- Which resource is actually affected.
- Which data classification and risk state apply.
- Which policy version governs the action.
- Whether acknowledgment or escalation is required.
- Whether scoped authority is still valid at execution time.
- Whether the side effect should execute at all.

This is why the [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) composes several older architectural ideas rather than presenting model tool calling as a new security primitive.

NIST's AI Risk Management Framework provides a broader governance and human-oversight context for AI systems. Learning's gateway is a narrower application-architecture teaching pattern inside that larger problem space.

## What `Canonical` Means Here

Within ASI Backbone Learning, **Canonical Pattern** means:

> The pattern currently aligned with the organization's reference implementations and foundational teaching sequence.

It does not mean:

- Standardized by an external standards body.
- The only safe implementation.
- The most appropriate implementation for every application.
- A claim of architectural invention.
- A requirement to adopt the `AsiBackbone` package.

An alternative pattern can be simpler, safer, cheaper, or more familiar for a particular problem.

The repository intentionally preserves that possibility.

## Prefer Established or Framework-Native Approaches When They Are Enough

The vocabulary on this page should not create pressure to introduce new abstractions.

Prefer the smallest architecture that preserves the boundaries you actually need.

Examples:

- Use ASP.NET Core authorization when the problem is ordinary access control and success/failure is sufficient.
- Use normal command validation when a request is synchronous and no durable governance lifecycle exists.
- Use an existing workflow engine when approval, waiting, retry, and escalation are already modeled well there.
- Use established OAuth/OIDC mechanisms for identity and delegated API access rather than inventing replacement authentication protocols.
- Use your existing logging, SIEM, or provenance platform when it can preserve the required evidence.
- Keep AI advisory or read-only when host-side execution governance would add ceremony without reducing meaningful risk.

Learning terminology is useful only when it makes a real boundary clearer.

## Related Learning Material

Foundational tutorials:

1. [Decision Before Execution](../tutorials/decision-before-execution.md)
2. [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md)
3. [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md)
4. [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md)
5. [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md)

Architecture comparisons and references:

- [When ASP.NET Core Authorization Is Enough](when-aspnet-core-authorization-is-enough.md)
- [Governance Spine and Capability Validation Diagrams](governance-spine-and-capability-validation-diagrams.md)
- [Architecture](index.md)

## External Terminology Anchors

These references are intentionally selective. They provide established terminology and lineage without attempting an exhaustive survey.

- [NIST SP 800-162 — Guide to Attribute Based Access Control (ABAC)](https://csrc.nist.gov/pubs/sp/800/162/upd2/final) — subject, object, operation, environment, and policy-oriented access-control context.
- [Microsoft Learn — Policy-based authorization in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authorization/policies) — policies, requirements, handlers, and `IAuthorizationService`.
- [Microsoft Learn — Resource-based authorization in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authorization/resource-based) — authorization decisions that depend on a loaded resource.
- [NIST CSRC Glossary — Reference Monitor](https://csrc.nist.gov/glossary/term/reference_monitor) — complete mediation and trusted access-control enforcement.
- [NIST CSRC Glossary — Least Privilege](https://csrc.nist.gov/glossary/term/least_privilege) — limiting authorizations and resources to what an entity needs.
- [RFC 6749 — The OAuth 2.0 Authorization Framework](https://www.rfc-editor.org/rfc/rfc6749.html) — scoped, time-bounded access-token concepts useful as a comparison point for delegated authority.
- [W3C PROV-O](https://www.w3.org/TR/prov-o/) — an established provenance vocabulary for representing relationships among entities, activities, and agents.
- [NIST AI Risk Management Framework](https://www.nist.gov/itl/ai-risk-management-framework) — broader AI risk-management, governance, documentation, and human-oversight context.

## Summary

The useful question is not:

> Which familiar idea has ASI Backbone renamed?

It is:

> Which established ideas are being composed here, and which boundary is the composition trying to keep visible?

That distinction is central to the Learning repository.

The terminology should help a reader reason about architecture, not obscure its lineage.
