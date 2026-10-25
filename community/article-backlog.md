# Problem-Oriented Standalone Article Backlog

This backlog prioritizes candidate standalone technical articles for `AsiBackbone/Learning` around problems and search language that .NET developers, architects, security practitioners, and AI-integration teams already use.

It is an editorial planning surface, not a publication quota and not a second curriculum roadmap. `ROADMAP.md` remains the strategic source of truth for Learning. [Requested Topics](requested-topics.md) remains the community intake surface for curriculum ideas. This backlog answers a narrower question:

> **Which existing Learning ideas are strong candidates for a standalone, problem-oriented article that can be useful before a reader knows AsiBackbone terminology?**

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
| **P0 — next publication set** | Strong standalone search problem, substantial source material, and a clear article/curriculum distinction. These are the candidates to consider first. |
| **P1 — strong follow-on** | Strong candidate with good source depth, but lower priority than the first publication set or some overlap that should be managed deliberately. |

A backlog item becomes implementation work only when a dedicated issue is opened for that article. The implementation issue should record the final problem statement, working title, proposed permanent slug, supporting Learning sources, and scope boundaries. Record the issue number beside the candidate when that promotion happens.

---

## Published P0 Candidates

### 1. When ASP.NET Core Authorization Is Not Enough

- **Priority:** P0
- **Implementation issue:** [#218](https://github.com/AsiBackbone/Learning/issues/218)
- **Publication:** [Published August 26, 2026](../docs/articles/2026/when-aspnet-core-authorization-is-not-enough.md)
- **Permanent slug:** `when-aspnet-core-authorization-is-not-enough`

#### Reader / search problem

A .NET developer already uses roles, policies, handlers, or resource-based authorization and wants to know when those mechanisms stop expressing the complete lifecycle decision. Typical questions include:

- Is authorization enough if an operation may need deferral, acknowledgment, escalation, or a later execution boundary?
- When should workflow state or operational policy remain separate from `IAuthorizationService`?
- How can authorization succeed while the operation still should not execute yet?

#### Existing Learning support

- [When ASP.NET Core Authorization Is Enough](../docs/architecture/when-aspnet-core-authorization-is-enough.md)
- [Your Authorization Check Runs Too Late](../docs/articles/2026/authorization-check-runs-too-late.md)
- [Decision Before Execution](../docs/tutorials/decision-before-execution.md)
- [Policy Context and Explicit Decision Outcomes](../docs/tutorials/policy-context-and-explicit-decision-outcomes.md)

#### Article / curriculum distinction

The existing authorization comparison is the deeper architecture treatment of what ASP.NET Core already provides and where a broader lifecycle can become useful. The standalone article should start from recognizable symptoms—late operational checks, richer lifecycle outcomes, delayed execution, or independently changing policy—and give a compact decision guide. It should not reproduce the full framework comparison.

#### Natural deeper path

Lead readers first to the ASP.NET Core authorization comparison, then to Decision Before Execution when they genuinely need an explicit operation-level boundary.

---

### 2. How to Test That a Denied Operation Never Executes

- **Priority:** P0
- **Implementation issue:** [#217](https://github.com/AsiBackbone/Learning/issues/217)
- **Publication:** [Published August 26, 2026](../docs/articles/2026/test-denied-operation-never-executes.md)
- **Permanent slug:** `test-denied-operation-never-executes`

#### Reader / search problem

A developer can test that a method returns `Denied`, `Forbid`, or another blocked result, but wants evidence that no protected side effect occurred. The practical question is:

> How do I test the absence of execution rather than only the decision result?

#### Existing Learning support

- [Decision Before Execution](../docs/tutorials/decision-before-execution.md)
- [Decision Before Execution runnable sample](../samples/decision-before-execution/README.md)
- [Decision Before Execution lab](../docs/labs/decision-before-execution.md)
- [Your Authorization Check Runs Too Late](../docs/articles/2026/authorization-check-runs-too-late.md)

#### Article / curriculum distinction

The foundational tutorial teaches the whole proposal-to-decision-to-execution pattern. This article should be narrower and test-driven: isolate the executor, instrument or substitute it, assert invocation count or another deterministic side-effect boundary, and explain why a returned denial is weaker evidence than a denial plus zero executor calls.

#### Natural deeper path

Send readers to the runnable sample for executable evidence and then to the lab if they want to practice moving a late or coupled check in front of the executor.

---

### 3. Do You Need a Capability Token, or Are Roles and Claims Enough?

- **Priority:** P0
- **Implementation issue:** [#220](https://github.com/AsiBackbone/Learning/issues/220)
- **Publication:** [Published September 2, 2026](../docs/articles/2026/roles-claims-or-capability-token-dotnet.md)
- **Permanent slug:** `roles-claims-or-capability-token-dotnet`

#### Reader / search problem

A .NET architect understands roles and claims but is considering a short-lived capability or continuation token and wants to know whether that extra authority model is justified. Common questions include:

- When are normal role or claims policies sufficient?
- When does delayed, delegated, or cross-process execution need narrower authority?
- Is a capability token just another JWT with different claims, or does it represent a different lifecycle responsibility?

#### Existing Learning support

- [Role-Based, Claims-Based, and Capability-Based Authorization](../docs/architecture/role-based-claims-based-and-capability-based-authorization.md)
- [Scoped Capability and Host-Owned Execution](../docs/tutorials/scoped-capability-and-host-owned-execution.md)
- [Replay Protection and Bounded Use](../docs/security/replay-protection-and-bounded-use.md)
- [Agent and Tool Authorization Models and Host-Owned Execution](../docs/architecture/agent-and-tool-authorization-models-and-host-owned-execution.md)

#### Article / curriculum distinction

The architecture comparison provides the full semantic treatment of role, claim, and capability authority. The standalone article should be a practical selection guide built around recognizable .NET scenarios: immediate same-host execution, queued work, delegated workers, cross-service calls, and bounded one-time operations. It should emphasize when **not** to introduce capability infrastructure.

#### Natural deeper path

Use the role/claims/capability comparison for the full model, then Scoped Capability and Host-Owned Execution for lifecycle design and replay protection when a bounded token is actually justified.

---

### 4. Why an AI Tool Call Is a Proposal, Not Authority

- **Priority:** P0
- **Implementation issue:** [#219](https://github.com/AsiBackbone/Learning/issues/219)
- **Publication:** [Published August 28, 2026](../docs/articles/2026/why-ai-tool-call-is-only-a-proposal.md)
- **Permanent slug:** `why-ai-tool-call-is-only-a-proposal`

#### Reader / search problem

A developer is wiring model function/tool calling into application code and needs to know whether a syntactically valid model-generated call can be treated as permission to invoke the underlying tool.

The recognizable failure path is:

```text
model output
    ↓
valid tool call
    ↓
real side effect
```

#### Existing Learning support

- [Governed AI Tool Gateway](../docs/tutorials/governed-ai-tool-gateway.md)
- [Typed AI Proposed Intent and Schema-Validation Boundaries](../docs/ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md)
- [Agent and Tool Authorization Models and Host-Owned Execution](../docs/architecture/agent-and-tool-authorization-models-and-host-owned-execution.md)
- [Governed AI Tool Gateway runnable sample](../samples/governed-ai-tool-gateway/README.md)
- [Governed AI Tool Gateway lab](../docs/labs/governed-ai-tool-gateway.md)

#### Article / curriculum distinction

The gateway tutorial composes the full proposal, context, decision, acknowledgment, capability, execution, and evidence lifecycle. The standalone article should make one narrower argument: model output is untrusted proposed intent, and successful parsing or schema validation does not grant authority. A minimal host-owned tool loop can make that distinction concrete without requiring the full curriculum.

#### Natural deeper path

Point implementation-focused readers to typed proposal/schema validation and then to the runnable gateway sample when they need the complete host-owned boundary.

---

## Published P1 Candidates

### 5. Authorization vs. Approval vs. Acknowledgment: Which Decision Do You Actually Have?

- **Priority:** P1
- **Implementation issue:** [#375](https://github.com/AsiBackbone/Learning/issues/375)
- **Publication:** [Published September 27, 2026](../docs/articles/2026/authorization-vs-approval-vs-acknowledgment.md)
- **Permanent slug:** `authorization-vs-approval-vs-acknowledgment`

#### Reader / search problem

Teams often use `authorized`, `approved`, `confirmed`, and `acknowledged` as if they meant the same thing. That creates ambiguity around who may request an operation, who may review it, what a user merely accepts, and what authority the executor still needs.

#### Existing Learning support

- [Human-in-the-Loop Governance Workflows](../docs/governance/human-in-the-loop-governance-workflows.md)
- [Decision Receipts and Acknowledgment](../docs/tutorials/decision-receipts-and-acknowledgment.md)
- [Workflow Engines, Human Approval Systems, and Governed Execution](../docs/architecture/workflow-engines-human-approval-and-governed-execution.md)
- [When ASP.NET Core Authorization Is Enough](../docs/architecture/when-aspnet-core-authorization-is-enough.md)

#### Article / curriculum distinction

The deeper pages model complete review and workflow lifecycles. This article should be a terminology-and-failure-mode guide for ordinary application teams: define each decision in plain language, show what evidence each one creates, and demonstrate why `Acknowledged = true` or workflow state `Approved` should not silently become execution permission.

#### Natural deeper path

Lead to Human-in-the-Loop Governance Workflows for long-running review and to Decision Receipts and Acknowledgment for the pause/resume evidence model.

---

### 6. Your Audit Log Records the Story, Not the Decision

- **Priority:** P1
- **Implementation issue:** Not opened; published via [#286](https://github.com/AsiBackbone/Learning/pull/286)
- **Publication:** [Published September 7, 2026](../docs/articles/2026/your-audit-log-is-not-evidence.md)
- **Permanent slug:** `your-audit-log-is-not-evidence`

#### Reader / search problem

A team already records application or security logs and wants to know whether those logs are enough to explain why a consequential operation was allowed, denied, deferred, acknowledged, or executed.

#### Existing Learning support

- [Decision Receipts and Acknowledgment](../docs/tutorials/decision-receipts-and-acknowledgment.md)
- [Event Sourcing, Audit Trails, and Governance Decision Provenance](../docs/architecture/event-sourcing-audit-trails-and-governance-decision-provenance.md)
- [Secure Logging Across Trust Boundaries](../docs/security/secure-logging-across-trust-boundaries.md)
- [Policy Versioning and Decision Provenance](../docs/governance/policy-versioning-and-decision-provenance.md)

#### Article / curriculum distinction

The architecture comparison distinguishes operational logs, audit trails, decision receipts, and event sourcing in depth. The standalone article should start with a familiar log entry and ask what it cannot reconstruct: exact intent, authoritative context, policy identity, reason codes, acknowledgment lineage, and whether execution followed. It should avoid implying that every application needs a specialized governance store.

#### Natural deeper path

Use the event-sourcing/audit/provenance comparison for record-model selection and Secure Logging Across Trust Boundaries when the immediate concern is safe operational telemetry.

---

### 7. Policy as Code in ASP.NET Core Without Overengineering

- **Priority:** P1
- **Implementation issue:** [#377](https://github.com/AsiBackbone/Learning/issues/377)
- **Publication:** [Published September 30, 2026](../docs/articles/2026/policy-as-code-aspnet-core-without-overengineering.md)
- **Permanent slug:** `policy-as-code-aspnet-core-without-overengineering`

#### Reader / search problem

A team wants policy-as-code or more maintainable decision logic but does not know whether it needs custom application code, ASP.NET Core authorization policies, a rules engine, an embedded policy engine, or a remote policy service.

#### Existing Learning support

- [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../docs/architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md)
- [When ASP.NET Core Authorization Is Enough](../docs/architecture/when-aspnet-core-authorization-is-enough.md)
- [When a Simple Application Service Is Enough](../docs/architecture/when-a-simple-application-service-is-enough.md)
- [Policy Context and Explicit Decision Outcomes](../docs/tutorials/policy-context-and-explicit-decision-outcomes.md)

#### Article / curriculum distinction

The policy-engine comparison is advanced and covers PDP/PEP placement, distribution, caching, freshness, failure modes, and multiple engine categories. The standalone article should be a proportionality guide for an ASP.NET Core application: start with ordinary code or framework policies, identify the pressure that justifies externalized policy, and explain why a remote engine is not a maturity requirement.

#### Natural deeper path

Send readers to the full policy-engine comparison only after the simpler application-service and ASP.NET Core authorization options have been considered.

---

### 8. When Should a Workflow Engine Own the Decision?

- **Priority:** P1
- **Implementation issue:** [#394](https://github.com/AsiBackbone/Learning/issues/394)
- **Publication:** [Published October 5, 2026](../docs/articles/2026/when-workflow-engine-should-own-decision.md)
- **Permanent slug:** `when-workflow-engine-should-own-decision`

#### Reader / search problem

A team already has a workflow engine with states, retries, timers, and approvals and wants to know whether policy decisions should live inside that workflow or remain separately evaluated at a protected boundary.

#### Existing Learning support

- [Workflow Engines, Human Approval Systems, and Governed Execution](../docs/architecture/workflow-engines-human-approval-and-governed-execution.md)
- [Human-in-the-Loop Governance Workflows](../docs/governance/human-in-the-loop-governance-workflows.md)
- [Decision Before Execution](../docs/tutorials/decision-before-execution.md)
- [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../docs/architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md)

#### Article / curriculum distinction

The existing workflow comparison gives the complete responsibility model. This article should be organized around concrete choices: same-trust-boundary orchestration, approval state, independently changing policy, delayed retries, and execution authority. The key question is not whether a workflow product is capable of running rules, but whether workflow position is being mistaken for current permission.

#### Natural deeper path

Lead to the workflow-engine comparison for detailed scenarios and to Human-in-the-Loop Governance Workflows when durable review state is the main problem.

---

### 9. How Short-Lived Execution Authority Differs from User Authorization

- **Priority:** P1
- **Implementation issue:** [#395](https://github.com/AsiBackbone/Learning/issues/395)
- **Publication:** [Published October 3, 2026](../docs/articles/2026/short-lived-execution-authority-vs-user-authorization.md)
- **Permanent slug:** `short-lived-execution-authority-vs-user-authorization`

#### Reader / search problem

A developer is considering a short-lived token for a worker, background job, gateway, or delayed action and needs to understand why the user's original authorization is not automatically the right credential or authority to carry forward.

#### Existing Learning support

- [Scoped Capability and Host-Owned Execution](../docs/tutorials/scoped-capability-and-host-owned-execution.md)
- [Role-Based, Claims-Based, and Capability-Based Authorization](../docs/architecture/role-based-claims-based-and-capability-based-authorization.md)
- [Replay Protection and Bounded Use](../docs/security/replay-protection-and-bounded-use.md)
- [Replay Protection and Bounded Use runnable sample](../samples/replay-protection-and-bounded-use/README.md)

#### Article / curriculum distinction

This is narrower than the roles/claims/capabilities selection article. It should focus on the **time and execution-boundary handoff**: an actor may be authorized now, but a later executor should receive only the resource, operation, audience, lifetime, and use count it needs. The article should also state clearly when the same trusted host can simply execute immediately without minting another token.

#### Natural deeper path

Lead to Scoped Capability and Host-Owned Execution for the complete lifecycle and replay protection for one-time, stale, or reused authority.

---

### 10. What Should an AI Tool Gateway Validate Before Execution?

- **Priority:** P1
- **Implementation issue:** [#371](https://github.com/AsiBackbone/Learning/issues/371)
- **Publication:** [Published September 27, 2026](../docs/articles/2026/validate-ai-tool-call-before-execution.md)
- **Permanent slug:** `validate-ai-tool-call-before-execution`

#### Reader / search problem

A developer already accepts AI tool/function calls and wants a concrete host-side validation checklist before invoking a consequential handler. Typical questions are:

- Is valid JSON enough?
- Where should tool allowlisting and schema validation occur?
- Which values can come from the model and which must come from authenticated or authoritative host context?
- What should happen to unknown tools or semantically invalid arguments?

#### Existing Learning support

- [Typed AI Proposed Intent and Schema-Validation Boundaries](../docs/ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md)
- [Governed AI Tool Gateway](../docs/tutorials/governed-ai-tool-gateway.md)
- [Trust Boundaries and Least Privilege](../docs/security/trust-boundaries-and-least-privilege.md)
- [Governed AI Tool Gateway runnable sample](../samples/governed-ai-tool-gateway/README.md)

#### Article / curriculum distinction

Candidate 4 establishes the authority model: a tool call is a proposal. This candidate should be implementation-oriented and begin one step later, with the host receiving structured output. It should walk through structural parsing, allowlisting, schema validation, semantic validation, authoritative-context resolution, authorization/policy evaluation, and the final execution check without duplicating the full gateway tutorial.

#### Natural deeper path

Lead to Typed AI Proposed Intent for the four-stage acceptance model and to the runnable gateway sample for end-to-end behavior.

---

### 11. Can One Worker Safely Execute Delayed Operations for Many Tenants?

- **Priority:** P1
- **Implementation issue:** [#398](https://github.com/AsiBackbone/Learning/issues/398)
- **Publication:** [Published October 6, 2026](../docs/articles/2026/multi-tenant-worker-execution-authority.md)
- **Permanent slug:** `multi-tenant-worker-execution-authority`

#### Reader / search problem

A team runs one shared background worker, gateway, or executor that performs delayed operations for many tenants, and wants to know what actually keeps one tenant's authority from reaching another tenant's resources. Typical questions include:

- Is checking that the grant's tenant matches the resource's tenant enough?
- How should a workload identity be represented so that it is unambiguous across issuers and tenant directories?
- When does tenant isolation require separate worker identities, credentials, or execution partitions rather than one shared worker?
- Can token exchange produce a tenant-bound execution credential, and who must decide which tenant it is bound to?
- How do claim mapping and identity normalization in ASP.NET Core affect execution-time identity checks?

#### Existing Learning support

- [How Short-Lived Execution Authority Differs from User Authorization](../docs/articles/2026/short-lived-execution-authority-vs-user-authorization.md)
- [Capability-Scoped Background Operation](../docs/case-studies/capability-scoped-background-operation.md)
- [Multi-Tenant and Regional Policy Overlay](../docs/case-studies/multi-tenant-and-regional-policy-overlay.md)
- [Trust Boundaries and Least Privilege](../docs/security/trust-boundaries-and-least-privilege.md)
- [Scoped Capability and Host-Owned Execution](../docs/tutorials/scoped-capability-and-host-owned-execution.md)

#### Article / curriculum distinction

The execution-authority article deliberately keeps its tenant caveat short. This candidate should take up the distinctions that review of that article surfaced:

- Tenant equality between grant and resource prevents cross-tenant substitution, but does not isolate tenants from a compromised shared worker that is legitimately authorized for all of them. Compromise isolation requires separately enforced identities, credentials, permissions, or execution partitions, never a tenant value supplied by the worker.
- Workload identity should be a validated, structured identity, such as issuer and subject, or issuer, tenant, and principal object ID, compared as fields or one canonical encoding. Application or client IDs and tenant-local service principals answer different questions, and the article should describe them accurately for the identity providers it names.
- Regional or partition-specific worker identities create a boundary only when they have genuinely separate credentials and permissions. A different name on shared credentials does not, and regional identity alone does not establish data-sovereignty compliance.
- Tenant-scoped token exchange is an optional additional control. The issuer, not the worker, must authorize the tenant binding, and the protected host still enforces the tenant boundary.
- Execution-time identity comparison depends on deliberate claim mapping: derive one canonical identity after token validation, and reject missing or conflicting required claims rather than falling back silently.

The article should remain representation- and vendor-neutral in its recommendations, using a specific identity provider only to illustrate identifier semantics, and should state when a single shared worker with ordinary tenant checks is sufficient.

#### Natural deeper path

Lead to Capability-Scoped Background Operation for the queue and worker lifecycle, Multi-Tenant and Regional Policy Overlay for tenant and regional policy composition, and Trust Boundaries and Least Privilege for credential and identity separation.

---

### 12. Should Authorization Fail Open, Fail Closed, or Defer?

- **Priority:** P1
- **Implementation issue:** [#403](https://github.com/AsiBackbone/Learning/issues/403)
- **Publication:** [Published October 10, 2026](../docs/articles/2026/fail-open-fail-closed-or-defer.md)
- **Permanent slug:** `fail-open-fail-closed-or-defer`

#### Reader / search problem

A protected operation depends on a remote policy decision point, risk service, attribute source, or other authorization dependency. When that dependency times out or returns incomplete evidence, the team must decide whether to fail open, fail closed, defer the operation, or use a bounded degraded mode. Typical questions include:

- Does "deny by default" mean every dependency outage must look like a permanent authorization denial?
- How should the system distinguish an explicit denial from a deferred, unavailable, or undeterminable decision?
- Can a last-known-good policy or cached attribute set be used safely, and how fresh may it be?
- Do retries, circuit breakers, or fallback handlers change who is authorized to perform the side effect?
- What should happen when the system cannot tell whether an external side effect already occurred?

#### Existing Learning support

- [Safe Degraded Mode and Fail-Safe Governance](../docs/labs/safe-degraded-mode-and-fail-safe-governance.md)
- [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../docs/architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md)
- [Policy Context and Explicit Decision Outcomes](../docs/tutorials/policy-context-and-explicit-decision-outcomes.md)
- [Escalation Patterns in Governed Systems](../docs/governance/escalation-patterns-in-governed-systems.md)
- [Trust Boundaries and Least Privilege](../docs/security/trust-boundaries-and-least-privilege.md)

#### Article / curriculum distinction

The degraded-mode lab is a deep hands-on exercise, and the policy-engine architecture page treats partition behavior as one placement concern among many. This candidate is a concise, problem-first decision guide for teams facing the operational choice. It should reconcile OWASP's deny-by-default guidance with OPA's observation that the calling software owns fail-open versus fail-closed behavior, keep the invariant that no protected side effect executes without valid authority, keep explicit denial and unavailable evidence as different facts, separate transport resilience from authorization semantics, and require predetermined scope, freshness, version, and operation limits before any last-known-good policy is used. It should also state when a plain unavailable response is sufficient.

#### Natural deeper path

Lead to Safe Degraded Mode and Fail-Safe Governance for hands-on practice, Policy Engines, Rules Engines, and Distributed Policy Enforcement for placement, distribution, and staleness, and Escalation Patterns in Governed Systems for routing decisions to an independent authority.

---

### 13. Idempotency Does Not Prevent Authority Replay

- **Priority:** P1
- **Implementation issue:** [#417](https://github.com/AsiBackbone/Learning/issues/417)
- **Publication:** [Published October 12, 2026](../docs/articles/2026/idempotency-does-not-prevent-authority-replay.md)
- **Permanent slug:** `idempotency-does-not-prevent-authority-replay`

#### Reader / search problem

A team running queued work, background workers, or external API calls treats idempotency keys, duplicate-message handling, capability replay protection, and "exactly once" execution as interchangeable. A request can be idempotent while still accepting reused authority, and a one-use capability can still lead to a duplicated external effect after an ambiguous timeout. Typical questions include:

- If the downstream API accepts an idempotency key, is single-use authority redundant?
- If a capability can be used only once, is it safe to retry after a timeout?
- Does an inbox table or broker duplicate detection stop a resubmitted message from reusing authority?
- Which identifier should key replay state, operation deduplication, and the provider request?
- What should happen when the system cannot tell whether an external side effect already occurred?

#### Existing Learning support

- [Replay Protection and Bounded-Use Authority](../docs/security/replay-protection-and-bounded-use.md)
- [Cross-System Capability Exchange and Delegated Authority](../docs/advanced/cross-system-capability-exchange-and-delegated-authority.md)
- [Governed AI Tool Gateway](../docs/tutorials/governed-ai-tool-gateway.md)
- [Capability-Scoped Background Operation](../docs/case-studies/capability-scoped-background-operation.md)
- [When Should a Workflow Engine Own the Decision?](../docs/articles/2026/when-workflow-engine-should-own-decision.md)
- [Should Authorization Fail Open, Fail Closed, or Defer?](../docs/articles/2026/fail-open-fail-closed-or-defer.md)

#### Article / curriculum distinction

The replay-protection security page, lab ([#52](https://github.com/AsiBackbone/Learning/issues/52)), and executable sample ([#65](https://github.com/AsiBackbone/Learning/issues/65)) provide the deep distributed-state treatment, and the case study and cross-system page each distinguish replay from idempotency inside a larger design. This candidate is a concise, problem-first article a reader can find before learning the repository's terminology. It should start from one recognizable .NET scenario, define replay protection and idempotency separately in plain language, distinguish duplicate transport delivery, atomic capability consumption, operation identity, provider idempotency keys, and inbox/outbox deduplication, show both directions of the distinction with invariant tests, keep unknown execution outcomes separate from failures, and explain when ordinary idempotency is sufficient. It must not claim exactly-once effects or that an idempotency key is sufficient without authoritative storage and downstream semantics.

#### Natural deeper path

Lead to Replay Protection and Bounded-Use Authority for atomic consumption and failure windows, its lab and sample for hands-on practice, Capability-Scoped Background Operation for redelivery, execution identity, and ambiguous publication, and Cross-System Capability Exchange and Delegated Authority when authority crosses an independent boundary.

---

### 14. An Allowed Decision Does Not Mean the Operation Succeeded

- **Priority:** P1
- **Implementation issue:** [#418](https://github.com/AsiBackbone/Learning/issues/418)
- **Publication:** [Published October 13, 2026](../docs/articles/2026/allowed-decision-does-not-mean-execution-succeeded.md)
- **Permanent slug:** `allowed-decision-does-not-mean-execution-succeeded`

#### Reader / search problem

A team treats an `Allowed` decision, a completed approval, or a valid capability as evidence that the protected operation completed. When execution later fails or times out, the decision is rewritten, the operation is retried blindly, or the decision is read as proof of an effect that may not exist. Typical questions include:

- Why does the record say an operation was approved when it never happened?
- Should a decision be changed to `Denied` when the operation fails?
- How should a system record an operation whose outcome is unknown?
- When is a retry the same operation, and when does it need a new decision?
- Is undoing a completed operation part of the original operation, or a new one?

#### Existing Learning support

- [Deployment Approval and Infrastructure Change Gates](../docs/case-studies/deployment-approval-and-infrastructure-change-gates.md)
- [Sensitive-Data Access Decision](../docs/case-studies/sensitive-data-access-decision.md)
- [Replay Protection and Bounded-Use Authority](../docs/security/replay-protection-and-bounded-use.md)
- [Authorization vs. Approval vs. Acknowledgment](../docs/articles/2026/authorization-vs-approval-vs-acknowledgment.md)
- [Should Authorization Fail Open, Fail Closed, or Defer?](../docs/articles/2026/fail-open-fail-closed-or-defer.md)
- [Your Audit Log Records the Story, Not the Decision](../docs/articles/2026/your-audit-log-is-not-evidence.md)

#### Article / curriculum distinction

The deployment and sensitive-data case studies, the replay-protection page, and the related articles each explain the decision/execution distinction inside a larger design, and [#191](https://github.com/AsiBackbone/Learning/issues/191) and [#409](https://github.com/AsiBackbone/Learning/issues/409) cover deeper case-study and sample material. This candidate is a short, problem-first entry point for a reader whose approved or authorized operation failed. It should follow one operation through each lifecycle stage, keep the historical decision unchanged, separate decision, attempt, outcome, and reconciliation evidence, cover deterministic failure, ambiguous completion, retry, reconciliation, and compensation boundaries, and state when a synchronous result and ordinary logs are enough. It must not prescribe one storage engine, workflow product, or universal execution-state enum, imply that decision receipts prove external effects, or claim exactly-once execution.

#### Natural deeper path

Lead to the deployment case study for rollback and re-planning, the sensitive-data case study for evidence-failure ordering, Replay Protection and Bounded-Use Authority for consume-before-execute failure windows, and Idempotency Does Not Prevent Authority Replay for execution leases and reconciliation after an ambiguous timeout.

---

### 15. You Don't Need to Roll Back What You Never Merged

- **Priority:** P1
- **Implementation issue:** [#445](https://github.com/AsiBackbone/Learning/issues/445)
- **Publication:** [Published October 23, 2026](../docs/articles/2026/you-dont-need-to-roll-back-what-you-never-merged.md)
- **Permanent slug:** `you-dont-need-to-roll-back-what-you-never-merged`

#### Reader / search problem

A team maintains a long-lived .NET application derived from an upstream template, reference architecture, or framework baseline. The next .NET LTS generation changes the runtime, SDK, ASP.NET Core, EF Core, dependencies, and possibly the generated structure at once. The usual plan edits `main` and repairs it until CI is green, so an unfinished platform experiment becomes entangled with feature, maintenance, and security work, and the team ends up discussing rollback. Typical questions include:

- How can we evaluate a new .NET LTS without destabilizing the branch we support production from?
- When an upgraded build fails, how do we tell whether the template, the platform, or our own changes caused it?
- How do we keep shipping security fixes while a migration is in progress?
- What evidence should decide that a migration is ready to merge?
- Do we need a permanent baseline branch, or is a tag or a reproducible generator enough?

#### Existing Learning support

- [Retrofitting Governed Execution into an Existing ASP.NET Core Application](../docs/aspnetcore/retrofit-governed-execution-into-an-existing-application.md)
- [Architecture Decision Records Preserve Architectural Reasoning](../docs/aspnetcore/architecture-decision-records-preserve-architectural-reasoning.md)
- [A Green CI Badge Does Not Prove Your .NET Package Is Trustworthy](../docs/articles/2026/ci-badge-does-not-prove-package-integrity.md)

#### Article / curriculum distinction

The retrofit playbook covers phased changes to application behavior, and the ADR page covers recording architectural reasoning. Neither addresses isolating a platform-generation experiment from the supported maintenance line. This article explains a preserved upstream baseline A, the application delta, a new baseline B generated with the same inputs, and an isolated integration candidate; distinguishes the three-way integration model from a literal Git merge; separates raw-baseline qualification from integrated-candidate qualification and "runs on the new LTS" from "ready to adopt it"; and covers maintenance on `main`, adoption evidence, discarding a failed candidate, post-adoption rollback, and the pattern's costs. It uses ReferenceApplication and NCAT as a bounded case study and must stay useful to readers who use neither. It must not present the pattern as a replacement for backups, deployment rollback, or recovery, claim that a baseline prevents breaking changes, or prescribe a universal branch name.

#### Natural deeper path

Lead to the retrofit playbook for phased change and stopping rules, the ADR page for recording why the baseline exists and when the strategy should change, and the ReferenceApplication refresh procedure for one concrete implementation.

---

## P0 — Next Publication Set

### 16. Your Retry Logic May Be Replaying an Authorization Decision

- **Priority:** P0
- **Implementation issue:** Not opened
- **Permanent slug:** `retry-logic-may-replay-authorization-decision`

#### Reader / search problem

A .NET team uses Polly, a queue, a worker, or another resilience mechanism to retry a failed operation and assumes that retrying execution also permits reuse of the decision or authority from the first attempt. Typical questions include:

- When a transient failure is retried, is the system retrying transport, one execution attempt, the logical operation, or the authority behind it?
- Can an earlier authorization decision be reused after policy, resource, actor, or risk context changes?
- Does an idempotency key make old authority safe to present again?
- When should a retry preserve the same operation identity, and when does it require reevaluation or new authority?

#### Existing Learning support

- [Replay Protection and Bounded-Use Authority](../docs/security/replay-protection-and-bounded-use.md)
- [Idempotency Does Not Prevent Authority Replay](../docs/articles/2026/idempotency-does-not-prevent-authority-replay.md)
- [An Allowed Decision Does Not Mean the Operation Succeeded](../docs/articles/2026/allowed-decision-does-not-mean-execution-succeeded.md)
- [Delayed Financial Payout and Ambiguous External Commit](../docs/case-studies/delayed-financial-payout-and-ambiguous-external-commit.md)

#### Article / curriculum distinction

The replay-protection material explains atomic consumption, and the existing articles separate idempotency, decisions, attempts, outcomes, and reconciliation in depth. This article should begin with automatic .NET retry behavior and make one narrower distinction: retry policy answers when an attempt may run again, not whether an old decision or grant remains valid. It should separate transport redelivery, execution retry, operation identity, authority reuse, reevaluation, and reconciliation; show at least one retryable pre-execution failure and one ambiguous post-send outcome; and explain when ordinary retry plus current authorization is sufficient. It must not imply that every retry needs a new token, that transient-fault libraries own authorization policy, or that idempotency proves authority freshness or exactly-once effects.

#### Natural deeper path

Lead to Idempotency Does Not Prevent Authority Replay for the identifier and ambiguous-outcome model, Replay Protection and Bounded-Use Authority for atomic consumption, and the delayed-payout case study for a complete reconciliation path.

---

### 17. Where Should Authorization Live in a .NET Application?

- **Priority:** P0
- **Implementation issue:** Not opened
- **Permanent slug:** `where-authorization-should-live-dotnet-application`

#### Reader / search problem

A .NET developer knows an operation needs protection but is unsure whether the check belongs in middleware, an endpoint or controller, an authorization handler, an application service, a domain boundary, a policy engine, or the final execution gateway. The practical questions are:

- Which layer should authenticate the caller, resolve authoritative resource context, decide permission, and enforce the result?
- How close should enforcement be to the protected side effect?
- When is ASP.NET Core authorization sufficient, and when does delayed or delegated execution require another boundary?
- How can a team avoid both scattered checks and a centralized component that owns responsibilities it cannot safely know?

#### Existing Learning support

- [When ASP.NET Core Authorization Is Enough](../docs/architecture/when-aspnet-core-authorization-is-enough.md)
- [When a Simple Application Service Is Enough](../docs/architecture/when-a-simple-application-service-is-enough.md)
- [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../docs/architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md)
- [Decision Before Execution](../docs/tutorials/decision-before-execution.md)

#### Article / curriculum distinction

The existing comparisons examine each mechanism separately. This article should synthesize them into a placement guide organized around responsibilities rather than framework features: authenticate at the host boundary, rebuild security-sensitive facts from authoritative sources, evaluate policy where the required facts and policy are available, and enforce immediately before the protected effect. It should compare common placements with concrete strengths and failure modes, include a proportional path from endpoint policy to application service to separate decision and execution boundaries, and make clear that one operation can legitimately involve more than one layer. It must not prescribe one universal layer, treat domain validation as a substitute for authorization, or imply that every application needs a remote policy engine or governed-execution pipeline.

#### Natural deeper path

Lead first to When ASP.NET Core Authorization Is Enough and When a Simple Application Service Is Enough, then to Decision Before Execution or the policy-engine comparison only when the operation's lifecycle or distribution justifies the additional boundary.

---

### 18. A Background Job Should Not Inherit Everything the User Could Do

- **Priority:** P0
- **Implementation issue:** Not opened
- **Permanent slug:** `background-job-should-not-inherit-user-authority`

#### Reader / search problem

An authenticated user starts work in an ASP.NET Core request, but a queue or `BackgroundService` performs it later. The team needs to decide what identity and authority cross that asynchronous boundary. Typical questions include:

- Should the job carry the user's access token, claims, roles, or service credential?
- How can the worker receive permission for one report, export, or command without inheriting everything the user or worker service can do?
- Which actor should appear in audit evidence when a service executes work requested by a user?
- What changes when the user, resource, tenant, policy, or job state changes before execution?

#### Existing Learning support

- [How Short-Lived Execution Authority Differs from User Authorization](../docs/articles/2026/short-lived-execution-authority-vs-user-authorization.md)
- [Capability-Scoped Background Operation](../docs/case-studies/capability-scoped-background-operation.md)
- [Scoped Capability and Host-Owned Execution](../docs/tutorials/scoped-capability-and-host-owned-execution.md)
- [Shared Multi-Tenant Worker and Delayed Execution Isolation](../docs/case-studies/shared-multi-tenant-worker-and-delayed-execution-isolation.md)

#### Article / curriculum distinction

The existing article explains the conceptual distinction between user authorization and execution authority, while the case studies model complete delayed-execution designs. This article should use one ordinary `Generate Report` flow to teach least privilege across the HTTP-to-queue-to-worker boundary before introducing capability terminology. It should distinguish requester identity, worker identity, operation-specific authority, job delivery, and current host validation; compare simple same-host work with delayed or cross-process work; and explain when a stored job identifier plus current authorization is enough. It must not recommend copying bearer tokens into queue messages, erase the requesting actor from provenance, assume every background job needs a custom token, or present a worker's service identity as proof that each requested operation is allowed.

#### Natural deeper path

Lead to How Short-Lived Execution Authority Differs from User Authorization for the authority model, Capability-Scoped Background Operation for the end-to-end design, and the multi-tenant worker case study when one worker serves several tenant trust boundaries.

---

## P1 — Strong Follow-On Candidates

### 19. Your Security Check Passed. Then the World Changed.

- **Priority:** P1
- **Implementation issue:** Not opened
- **Permanent slug:** `security-check-passed-then-world-changed`

#### Reader / search problem

A security or policy check succeeds, but execution happens later after the resource, role, tenant, risk signal, policy, or environment may have changed. The team needs to know when the earlier decision remains usable and when the system must reevaluate. Typical questions include:

- How long can an authorization or approval be trusted?
- Which changes invalidate old authority, and which changes are only historical context?
- Should policy deployment, resource-version drift, role removal, or a changed incident posture block delayed execution?
- How can a system preserve the original decision without pretending it is still current?

#### Existing Learning support

- [Adaptive Risk Context, Freshness, and Drift](../docs/advanced/adaptive-risk-context-freshness-and-drift.md)
- [Policy Versioning and Decision Provenance](../docs/governance/policy-versioning-and-decision-provenance.md)
- [Scoped Capability and Host-Owned Execution](../docs/tutorials/scoped-capability-and-host-owned-execution.md)
- [Delayed Financial Payout and Ambiguous External Commit](../docs/case-studies/delayed-financial-payout-and-ambiguous-external-commit.md)

#### Article / curriculum distinction

The advanced material covers detailed risk provenance, policy identity, freshness rules, and drift. This article should turn the broader time-of-check/time-of-use problem into an approachable practitioner guide. It should follow one delayed operation, distinguish historical decision truth from current execution eligibility, classify changes that require rejection, reevaluation, or no action, and show how resource versions, expiry, and explicit freshness rules can bound reuse. It must not claim that every state change invalidates a decision, prescribe one universal lifetime, rewrite historical decisions when current conditions differ, or imply that reevaluation guarantees the external operation will succeed.

#### Natural deeper path

Lead to Policy Versioning and Decision Provenance for policy drift, Scoped Capability and Host-Owned Execution for bounded continuation authority, and Adaptive Risk Context, Freshness, and Drift when probabilistic observations or changing risk inputs matter.

---

### 20. Passing Authentication Does Not Make Input Trustworthy

- **Priority:** P1
- **Implementation issue:** Not opened
- **Permanent slug:** `authentication-does-not-make-input-trustworthy`

#### Reader / search problem

An application receives a valid authenticated request and downstream code begins treating its identifiers, tenant values, prices, resource state, or requested operation as trusted facts. The recurring misconception is:

> The caller is authenticated, so the request values are trustworthy.

The reader needs to separate identity proof, input validation, authoritative context, authorization, policy decisions, and execution authority.

#### Existing Learning support

- [Trust Boundaries and Least Privilege](../docs/security/trust-boundaries-and-least-privilege.md)
- [Policy Context and Explicit Decision Outcomes](../docs/tutorials/policy-context-and-explicit-decision-outcomes.md)
- [Role-Based, Claims-Based, and Capability-Based Authorization](../docs/architecture/role-based-claims-based-and-capability-based-authorization.md)
- [Typed AI Proposed Intent and Schema-Validation Boundaries](../docs/ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md)

#### Article / curriculum distinction

The trust-boundary and policy-context material provides the deeper architecture treatment, while the AI page shows an especially sharp form of untrusted proposed input. This article should start with an ordinary authenticated ASP.NET Core request, show how a valid identity can still submit malformed, stale, unauthorized, cross-tenant, or adversarial values, and map each concern to the component responsible for it. It should explain that authentication can make some identity claims authoritative without making every request field true. It must not imply that all token claims are attacker-controlled, collapse validation into authorization, prescribe duplicate database reads for every field, or treat an authenticated caller as inherently malicious.

#### Natural deeper path

Lead to Trust Boundaries and Least Privilege for authoritative-context reconstruction, Policy Context and Explicit Decision Outcomes for separating facts from decisions, and the typed-AI proposal page for the same boundary when proposed input originates from a model.

---

## Promotion and Publication Workflow

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

### Publication Contract Reminder

A selected standalone article continues to use the existing frontmatter contract:

```yaml
---
title:
description:
author:
published: "YYYY-MM-DD"
summary:
feed: true
x_hashtags:
  - DotNet
  - SoftwareArchitecture
---
```

`x_hashtags` is optional and defaults to `#DotNet` when omitted. When present, it must include `DotNet` and may include one allowed topical value; see the canonical contract for the complete list.

The permanent source path is:

```text
docs/articles/<year>/<slug>.md
```

which publishes as:

```text
https://asibackbone.github.io/Learning/articles/<year>/<slug>.html
```

The publication contract, canonical-host guidance, permanent-path rule, and full article checklist remain defined in [CONTRIBUTING.md](../CONTRIBUTING.md#publishing-authored-articles).
