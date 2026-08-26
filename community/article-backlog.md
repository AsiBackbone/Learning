# Problem-Oriented Standalone Article Backlog

This backlog prioritizes candidate standalone technical articles for `AsiBackbone/Learning` around problems and search language that .NET developers, architects, security practitioners, and AI-integration teams already use.

It is an editorial planning surface, not a publication quota and not a second curriculum roadmap. `ROADMAP.md` remains the strategic source of truth for Learning. [Requested Topics](requested-topics.md) remains the community intake surface for curriculum ideas. This backlog answers a narrower question:

> **Which existing Learning ideas are strong candidates for a standalone, problem-oriented article that can be useful before a reader knows ASI Backbone terminology?**

The current Articles archive demonstrates the intended shape with [Your Authorization Check Runs Too Late](../docs/articles/2026/authorization-check-runs-too-late.md) and [A Green CI Badge Does Not Prove Your .NET Package Is Trustworthy](../docs/articles/2026/ci-badge-does-not-prove-package-integrity.md).

## Selection Principles

Prioritize a candidate when it has all or most of these properties:

- The title can be expressed in terminology ordinary .NET or architecture practitioners recognize.
- The reader arrives with a concrete technical question, failure mode, or design choice.
- Existing Learning material is deep enough to support the article without inventing a new curriculum topic first.
- The article can stand alone without prerequisites or prior knowledge of Learning vocabulary.
- The article adds a problem-first synthesis, walkthrough, checklist, or comparison rather than copying a curriculum page into a second location.
- The argument naturally leads to a small number of deeper tutorials, samples, labs, security pages, or comparisons.
- The article remains useful even when the reader never installs `AsiBackbone` or another organization package.
- The likely subject is evergreen enough to justify a permanent `/articles/<year>/<slug>.html` URL.

Do not create an article merely because a curriculum page already exists or because a publishing cadence needs another item. Search usefulness and technical quality outrank page count and frequency.

The titles below are editorial hypotheses based on recognizable reader problems and the material already present in Learning. They are **not claims about measured search volume**. A dedicated implementation issue may refine the title and slug before publication.

## Priority Model

| Priority | Meaning |
| --- | --- |
| **P0 — next publication set** | Strong standalone search problem, substantial source material, and a clear article/curriculum distinction. These are the first four candidates to consider. |
| **P1 — strong follow-on** | Strong candidate with good source depth, but lower priority than the first publication set or some overlap that should be managed deliberately. |

A backlog item becomes implementation work only when a dedicated issue is opened for that article. The implementation issue should record the final problem statement, working title, proposed permanent slug, supporting Learning sources, and scope boundaries. Record the issue number beside the candidate when that promotion happens.

---

# P0 — Next Publication Set

## 1. When ASP.NET Core Authorization Is Not Enough

- **Priority:** P0
- **Implementation issue:** Open separately when promoted
- **Candidate slug:** `when-aspnet-core-authorization-is-not-enough`

### Reader / search problem

A .NET developer already uses roles, policies, handlers, or resource-based authorization and wants to know when those mechanisms stop expressing the complete lifecycle decision. Typical questions include:

- Is authorization enough if an operation may need deferral, acknowledgment, escalation, or a later execution boundary?
- When should workflow state or operational policy remain separate from `IAuthorizationService`?
- How can authorization succeed while the operation still should not execute yet?

### Existing Learning support

- [When ASP.NET Core Authorization Is Enough](../docs/architecture/when-aspnet-core-authorization-is-enough.md)
- [Your Authorization Check Runs Too Late](../docs/articles/2026/authorization-check-runs-too-late.md)
- [Decision Before Execution](../docs/tutorials/decision-before-execution.md)
- [Policy Context and Explicit Decision Outcomes](../docs/tutorials/policy-context-and-explicit-decision-outcomes.md)

### Article / curriculum distinction

The existing authorization comparison is the deeper architecture treatment of what ASP.NET Core already provides and where a broader lifecycle can become useful. The standalone article should start from recognizable symptoms—late operational checks, richer lifecycle outcomes, delayed execution, or independently changing policy—and give a compact decision guide. It should not reproduce the full framework comparison.

### Natural deeper path

Lead readers first to the ASP.NET Core authorization comparison, then to Decision Before Execution when they genuinely need an explicit operation-level boundary.

---

## 2. How to Test That a Denied Operation Never Executes

- **Priority:** P0
- **Implementation issue:** [#217](https://github.com/AsiBackbone/Learning/issues/217)
- **Publication:** [Published August 26, 2026](../docs/articles/2026/test-denied-operation-never-executes.md)
- **Permanent slug:** `test-denied-operation-never-executes`

### Reader / search problem

A developer can test that a method returns `Denied`, `Forbid`, or another blocked result, but wants evidence that no protected side effect occurred. The practical question is:

> How do I test the absence of execution rather than only the decision result?

### Existing Learning support

- [Decision Before Execution](../docs/tutorials/decision-before-execution.md)
- [Decision Before Execution runnable sample](../samples/decision-before-execution/README.md)
- [Decision Before Execution lab](../docs/labs/decision-before-execution.md)
- [Your Authorization Check Runs Too Late](../docs/articles/2026/authorization-check-runs-too-late.md)

### Article / curriculum distinction

The foundational tutorial teaches the whole proposal-to-decision-to-execution pattern. This article should be narrower and test-driven: isolate the executor, instrument or substitute it, assert invocation count or another deterministic side-effect boundary, and explain why a returned denial is weaker evidence than a denial plus zero executor calls.

### Natural deeper path

Send readers to the runnable sample for executable evidence and then to the lab if they want to practice moving a late or coupled check in front of the executor.

---

## 3. Do You Need a Capability Token, or Are Roles and Claims Enough?

- **Priority:** P0
- **Implementation issue:** Open separately when promoted
- **Candidate slug:** `roles-claims-or-capability-token-dotnet`

### Reader / search problem

A .NET architect understands roles and claims but is considering a short-lived capability or continuation token and wants to know whether that extra authority model is justified. Common questions include:

- When are normal role or claims policies sufficient?
- When does delayed, delegated, or cross-process execution need narrower authority?
- Is a capability token just another JWT with different claims, or does it represent a different lifecycle responsibility?

### Existing Learning support

- [Role-Based, Claims-Based, and Capability-Based Authorization](../docs/architecture/role-based-claims-based-and-capability-based-authorization.md)
- [Scoped Capability and Host-Owned Execution](../docs/tutorials/scoped-capability-and-host-owned-execution.md)
- [Replay Protection and Bounded Use](../docs/security/replay-protection-and-bounded-use.md)
- [Agent and Tool Authorization Models and Host-Owned Execution](../docs/architecture/agent-and-tool-authorization-models-and-host-owned-execution.md)

### Article / curriculum distinction

The architecture comparison provides the full semantic treatment of role, claim, and capability authority. The standalone article should be a practical selection guide built around recognizable .NET scenarios: immediate same-host execution, queued work, delegated workers, cross-service calls, and bounded one-time operations. It should emphasize when **not** to introduce capability infrastructure.

### Natural deeper path

Use the role/claims/capability comparison for the full model, then Scoped Capability and Host-Owned Execution for lifecycle design and replay protection when a bounded token is actually justified.

---

## 4. Why an AI Tool Call Is a Proposal, Not Authority

- **Priority:** P0
- **Implementation issue:** Open separately when promoted
- **Candidate slug:** `why-ai-tool-call-is-only-a-proposal`

### Reader / search problem

A developer is wiring model function/tool calling into application code and needs to know whether a syntactically valid model-generated call can be treated as permission to invoke the underlying tool.

The recognizable failure path is:

```text
model output
    ↓
valid tool call
    ↓
real side effect
```

### Existing Learning support

- [Governed AI Tool Gateway](../docs/tutorials/governed-ai-tool-gateway.md)
- [Typed AI Proposed Intent and Schema-Validation Boundaries](../docs/ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md)
- [Agent and Tool Authorization Models and Host-Owned Execution](../docs/architecture/agent-and-tool-authorization-models-and-host-owned-execution.md)
- [Governed AI Tool Gateway runnable sample](../samples/governed-ai-tool-gateway/README.md)
- [Governed AI Tool Gateway lab](../docs/labs/governed-ai-tool-gateway.md)

### Article / curriculum distinction

The gateway tutorial composes the full proposal, context, decision, acknowledgment, capability, execution, and evidence lifecycle. The standalone article should make one narrower argument: model output is untrusted proposed intent, and successful parsing or schema validation does not grant authority. A minimal host-owned tool loop can make that distinction concrete without requiring the full curriculum.

### Natural deeper path

Point implementation-focused readers to typed proposal/schema validation and then to the runnable gateway sample when they need the complete host-owned boundary.

---

# P1 — Strong Follow-On Candidates

## 5. Authorization vs. Approval vs. Acknowledgment: Which Decision Do You Actually Have?

- **Priority:** P1
- **Implementation issue:** Open separately when promoted
- **Candidate slug:** `authorization-vs-approval-vs-acknowledgment`

### Reader / search problem

Teams often use `authorized`, `approved`, `confirmed`, and `acknowledged` as if they meant the same thing. That creates ambiguity around who may request an operation, who may review it, what a user merely accepts, and what authority the executor still needs.

### Existing Learning support

- [Human-in-the-Loop Governance Workflows](../docs/governance/human-in-the-loop-governance-workflows.md)
- [Acknowledgment and Audit Residue](../docs/tutorials/acknowledgment-and-audit-residue.md)
- [Workflow Engines, Human Approval Systems, and Governed Execution](../docs/architecture/workflow-engines-human-approval-and-governed-execution.md)
- [When ASP.NET Core Authorization Is Enough](../docs/architecture/when-aspnet-core-authorization-is-enough.md)

### Article / curriculum distinction

The deeper pages model complete review and workflow lifecycles. This article should be a terminology-and-failure-mode guide for ordinary application teams: define each decision in plain language, show what evidence each one creates, and demonstrate why `Acknowledged = true` or workflow state `Approved` should not silently become execution permission.

### Natural deeper path

Lead to Human-in-the-Loop Governance Workflows for long-running review and to Acknowledgment and Audit Residue for the pause/resume evidence model.

---

## 6. Why Audit Logging Is Not the Same as Decision Evidence

- **Priority:** P1
- **Implementation issue:** Open separately when promoted
- **Candidate slug:** `audit-logging-vs-decision-evidence`

### Reader / search problem

A team already records application or security logs and wants to know whether those logs are enough to explain why a consequential operation was allowed, denied, deferred, acknowledged, or executed.

### Existing Learning support

- [Acknowledgment and Audit Residue](../docs/tutorials/acknowledgment-and-audit-residue.md)
- [Event Sourcing, Audit Trails, and Governance Decision Provenance](../docs/architecture/event-sourcing-audit-trails-and-governance-decision-provenance.md)
- [Secure Logging Across Trust Boundaries](../docs/security/secure-logging-across-trust-boundaries.md)
- [Policy Versioning and Decision Provenance](../docs/governance/policy-versioning-and-decision-provenance.md)

### Article / curriculum distinction

The architecture comparison distinguishes operational logs, audit trails, decision receipts, and event sourcing in depth. The standalone article should start with a familiar log entry and ask what it cannot reconstruct: exact intent, authoritative context, policy identity, reason codes, acknowledgment lineage, and whether execution followed. It should avoid implying that every application needs a specialized governance store.

### Natural deeper path

Use the event-sourcing/audit/provenance comparison for record-model selection and Secure Logging Across Trust Boundaries when the immediate concern is safe operational telemetry.

---

## 7. Policy as Code in ASP.NET Core Without Overengineering

- **Priority:** P1
- **Implementation issue:** Open separately when promoted
- **Candidate slug:** `policy-as-code-aspnet-core-without-overengineering`

### Reader / search problem

A team wants policy-as-code or more maintainable decision logic but does not know whether it needs custom application code, ASP.NET Core authorization policies, a rules engine, an embedded policy engine, or a remote policy service.

### Existing Learning support

- [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../docs/architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md)
- [When ASP.NET Core Authorization Is Enough](../docs/architecture/when-aspnet-core-authorization-is-enough.md)
- [When a Simple Application Service Is Enough](../docs/architecture/when-a-simple-application-service-is-enough.md)
- [Policy Context and Explicit Decision Outcomes](../docs/tutorials/policy-context-and-explicit-decision-outcomes.md)

### Article / curriculum distinction

The policy-engine comparison is advanced and covers PDP/PEP placement, distribution, caching, freshness, failure modes, and multiple engine categories. The standalone article should be a proportionality guide for an ASP.NET Core application: start with ordinary code or framework policies, identify the pressure that justifies externalized policy, and explain why a remote engine is not a maturity requirement.

### Natural deeper path

Send readers to the full policy-engine comparison only after the simpler application-service and ASP.NET Core authorization options have been considered.

---

## 8. When Should a Workflow Engine Own the Decision?

- **Priority:** P1
- **Implementation issue:** Open separately when promoted
- **Candidate slug:** `when-workflow-engine-should-own-decision`

### Reader / search problem

A team already has a workflow engine with states, retries, timers, and approvals and wants to know whether policy decisions should live inside that workflow or remain separately evaluated at a protected boundary.

### Existing Learning support

- [Workflow Engines, Human Approval Systems, and Governed Execution](../docs/architecture/workflow-engines-human-approval-and-governed-execution.md)
- [Human-in-the-Loop Governance Workflows](../docs/governance/human-in-the-loop-governance-workflows.md)
- [Decision Before Execution](../docs/tutorials/decision-before-execution.md)
- [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../docs/architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md)

### Article / curriculum distinction

The existing workflow comparison gives the complete responsibility model. This article should be organized around concrete choices: same-trust-boundary orchestration, approval state, independently changing policy, delayed retries, and execution authority. The key question is not whether a workflow product is capable of running rules, but whether workflow position is being mistaken for current permission.

### Natural deeper path

Lead to the workflow-engine comparison for detailed scenarios and to Human-in-the-Loop Governance Workflows when durable review state is the main problem.

---

## 9. How Short-Lived Execution Authority Differs from User Authorization

- **Priority:** P1
- **Implementation issue:** Open separately when promoted
- **Candidate slug:** `short-lived-execution-authority-vs-user-authorization`

### Reader / search problem

A developer is considering a short-lived token for a worker, background job, gateway, or delayed action and needs to understand why the user's original authorization is not automatically the right credential or authority to carry forward.

### Existing Learning support

- [Scoped Capability and Host-Owned Execution](../docs/tutorials/scoped-capability-and-host-owned-execution.md)
- [Role-Based, Claims-Based, and Capability-Based Authorization](../docs/architecture/role-based-claims-based-and-capability-based-authorization.md)
- [Replay Protection and Bounded Use](../docs/security/replay-protection-and-bounded-use.md)
- [Replay Protection and Bounded Use runnable sample](../samples/replay-protection-and-bounded-use/README.md)

### Article / curriculum distinction

This is narrower than the roles/claims/capabilities selection article. It should focus on the **time and execution-boundary handoff**: an actor may be authorized now, but a later executor should receive only the resource, operation, audience, lifetime, and use count it needs. The article should also state clearly when the same trusted host can simply execute immediately without minting another token.

### Natural deeper path

Lead to Scoped Capability and Host-Owned Execution for the complete lifecycle and replay protection for one-time, stale, or reused authority.

---

## 10. What Should an AI Tool Gateway Validate Before Execution?

- **Priority:** P1
- **Implementation issue:** Open separately when promoted
- **Candidate slug:** `validate-ai-tool-call-before-execution`

### Reader / search problem

A developer already accepts AI tool/function calls and wants a concrete host-side validation checklist before invoking a consequential handler. Typical questions are:

- Is valid JSON enough?
- Where should tool allowlisting and schema validation occur?
- Which values can come from the model and which must come from authenticated or authoritative host context?
- What should happen to unknown tools or semantically invalid arguments?

### Existing Learning support

- [Typed AI Proposed Intent and Schema-Validation Boundaries](../docs/ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md)
- [Governed AI Tool Gateway](../docs/tutorials/governed-ai-tool-gateway.md)
- [Trust Boundaries and Least Privilege](../docs/security/trust-boundaries-and-least-privilege.md)
- [Governed AI Tool Gateway runnable sample](../samples/governed-ai-tool-gateway/README.md)

### Article / curriculum distinction

Candidate 4 establishes the authority model: a tool call is a proposal. This candidate should be implementation-oriented and begin one step later, with the host receiving structured output. It should walk through structural parsing, allowlisting, schema validation, semantic validation, authoritative-context resolution, authorization/policy evaluation, and the final execution check without duplicating the full gateway tutorial.

### Natural deeper path

Lead to Typed AI Proposed Intent for the four-stage acceptance model and to the runnable gateway sample for end-to-end behavior.

---

# Promotion and Publication Workflow

Keep planning, implementation, and publication as separate states:

```text
Candidate in this backlog
        ↓
Maintainer selects candidate
        ↓
Dedicated implementation issue
        ↓
Article drafted under docs/articles/<year>/<slug>.md
        ↓
Documentation / link / feed validation
        ↓
Published permanent article URL
```

When promoting a candidate:

1. Open a dedicated implementation issue instead of treating this backlog item as the implementation ticket.
2. Restate the concrete reader/search problem in ordinary practitioner terminology.
3. Confirm which existing Learning pages are source material and what the standalone article adds that those pages do not.
4. Refine the working title and candidate slug before publication; do not infer search demand solely from internal vocabulary or page-count goals.
5. Keep the article useful without `AsiBackbone` adoption or curriculum prerequisites.
6. Use the contextual-linking convention in `CONTRIBUTING.md` to select a small number of natural deeper destinations.
7. Publish with the existing metadata contract and permanent year/slug path.
8. Record the dedicated issue number in this backlog and remove or mark the candidate as published when the permanent article is live.

## Publication Contract Reminder

A selected standalone article continues to use the existing frontmatter contract:

```yaml
---
title:
description:
author:
published:
summary:
feed: true
---
```

and the permanent source path:

```text
docs/articles/<year>/<slug>.md
```

which publishes as:

```text
https://asibackbone.github.io/Learning/articles/<year>/<slug>.html
```

The publication contract, canonical-host guidance, permanent-path rule, and full article checklist remain defined in [CONTRIBUTING.md](../CONTRIBUTING.md#publishing-authored-articles).
