---
description: Compare legitimate policy and governance architectures across realistic scenarios, then defend the smallest design that satisfies the required lifecycle, trust boundaries, evidence needs, and failure model.
---

# Lab — Compare Competing Policy Architectures

**Learning objective:** Choose among competing policy architectures from stated requirements, defend the choice, identify the strongest rejected alternative, and explain what changed requirement would justify a different or more complex design.

**Difficulty:** Advanced

**Pattern classification:** Comparison of multiple approaches

**Prerequisites:** Recommended — [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md), [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md), [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md), [Constraint Composition and Policy Precedence](../governance/constraint-composition-and-policy-precedence.md), [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md), and [Safe Degraded Mode and Fail-Safe Governance](safe-degraded-mode-and-fail-safe-governance.md). [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) is especially useful for Scenario C.

**Suggested effort:** Treat this as a multi-session advanced lab. For the shorter path, complete Parts 1–2, all seven outputs for Scenarios A and C, Scenario C's revocation/drift analysis, and one-page decision records for A and C. You may skip Scenario B's two-sided deployment comparison and failure prompt, Scenario D's freshness-clock/timeline exercise, and the B/D aggregate-consistency evidence until the full submission. A full submission covers all four scenarios and every scenario-specific required comparison or failure exercise.

This is an architecture-selection lab.

It is not a product-selection exercise.

It is also not a maturity ladder in which every system should eventually move from embedded rules to an external policy engine and then to a larger governance framework.

The central lesson is:

> **Architecture selection should follow the required decision lifecycle, trust boundaries, operational constraints, and failure model — not terminology preference.**

A good solution should be able to defend both sides of the decision:

```text
Chosen architecture
      ↓
Why is a simpler option insufficient here?
      ↓
Why would the simpler option be better
if these requirements were absent?
```

At least one scenario intentionally permits more than one defensible answer.

You are expected to make assumptions visible rather than invent facts that make one architecture win automatically.

---

## Quick Glossary

- **PDP — Policy Decision Point:** the component that evaluates policy and produces a decision.
- **PEP — Policy Enforcement Point:** the component that can actually permit or prevent the protected operation at the execution boundary.
- **Policy artifact:** the versioned policy content used by an evaluator, including enough identity and integrity metadata to know what was active.
- **Scoped capability:** a bounded continuation-authority artifact for a later execution boundary; it is not automatically the same thing as an earlier `Allow` decision.

## Architecture Candidates

Use these as the primary comparison set.

You may propose a hybrid when the scenario genuinely needs one, but identify the primary responsibility of each component rather than hiding several architectures behind one label.

A clean hybrid keeps responsibility names visible:

```text
Framework authorization
        ↓
Policy evaluator
        ↓
Application-owned workflow state
        ↓
Worker PEP
        ↓
Host-owned executor
```

A muddy hybrid says only "governance service" while the same component quietly authenticates callers, invents resource facts, evaluates policy, issues authority, and executes with broad credentials. If you cannot name who owns each fact, decision, and side effect, the hybrid has hidden the boundary instead of composing it.

### Candidate A — Embedded Application Rules

```text
Application
   ↓
Ordinary code / domain service / embedded rules engine
   ↓
Application-owned result
   ↓
Same application continues
```

Natural strengths include:

- Low deployment complexity.
- No synchronous policy network dependency.
- Easy access to application-owned facts.
- Straightforward debugging when the rule set is small.
- Good fit when the problem is domain-rule evaluation rather than independent policy authority.

Potential costs include:

- Rules can become scattered if the boundary is not kept explicit.
- Cross-service consistency may depend on coordinated application releases.
- Independent policy ownership and rollout may be awkward.

Do not assume embedded rules are primitive.

They may be the correct architecture when one application owns the problem.

### Candidate B — Framework-Native ASP.NET Core Authorization

```text
Authenticated principal
   +
resource / request context
   ↓
ASP.NET Core authorization policy / handler
   ↓
Succeeded or Failed
   ↓
Host executes or rejects
```

Natural strengths include:

- Native integration with authentication, endpoints, resources, and dependency injection.
- Familiar policy and handler model.
- Low custom infrastructure cost.
- Excellent fit for request-local access control.

Potential limits include:

- `AuthorizationResult` is fundamentally success/failure, although failure reasons can be preserved and middleware result handling can distinguish framework outcomes such as challenge and forbid.
- Authentication challenge or step-up behavior can satisfy some request-local needs, but it is not automatically the same thing as a durable `Defer`, acknowledgment lifecycle, reviewer escalation workflow, or portable execution authority.
- Acknowledgment, escalation, deferred continuation, and portable execution authority belong to additional application architecture when those states must survive beyond the authorization check.
- Shared cross-service policy may require duplication or another distribution model.

Do not add a broader governance pipeline merely because Learning demonstrates one elsewhere.

ASP.NET Core is the concrete framework used in this lab, but the same comparison logic applies to equivalent framework-native authorization models in other application stacks.

### Candidate C — External Policy Engine

```text
Authoritative context
        ↓
Policy Decision Point
        ↓
structured decision
        ↓
caller / gateway enforcement
```

The evaluator may be remote, sidecar-local, process-local, or another clearly separated policy runtime.

Natural strengths include:

- Independent policy lifecycle.
- Shared evaluation semantics across heterogeneous callers.
- Policy-as-code, versioning, testing, and staged rollout opportunities.
- Clear Policy Decision Point responsibility.

Potential costs include:

- Network latency and availability if evaluation is remote.
- Policy distribution and staleness if evaluation is local.
- Additional runtime, authoring, and operational complexity.
- Trustworthy context construction still belongs somewhere else.

An external policy engine can be the complete policy architecture when the requirement is consistent policy evaluation and immediate enforcement.

### Candidate D — Central Decision Service + Distributed Enforcement

```text
Multiple callers
      ↓
Central decision authority / PDP
      ↓
decision + bindings + provenance
      ↓
multiple PEPs / protected hosts
      ↓
protected operations
```

Natural strengths include:

- Shared decision semantics.
- Explicit separation of Policy Decision Point and Policy Enforcement Point.
- Central visibility into policy decisions.
- Clear basis for distributed enforcement contracts.

Potential costs include:

- Decision freshness across time and process boundaries.
- Network and central-service availability concerns.
- Binding, replay, and stale-decision problems.
- More complicated evidence correlation across decision and enforcement.

This architecture does not automatically require a capability token.

If the PEP can safely reevaluate current policy, that may be the better choice.

If approval must cross a later trust boundary, narrow continuation authority may become useful.

> **Candidate C versus Candidate D:** Candidate C names the policy-evaluation responsibility. Candidate D becomes a distinct architecture when one decision authority is intentionally relied on by multiple PEPs or protected hosts and the binding, freshness, replay, availability, and partition semantics across that separation become first-class design concerns. A remote PDP used by one caller is not automatically "distributed enforcement."

### Candidate E — Governed Decision Pipeline with Richer Lifecycle Outcomes

```text
Intent
   ↓
Authoritative context
   ↓
Policy / constraints
   ↓
Allow / Deny / Defer / Acknowledge / Escalate
   ↓
Acknowledgment or escalation when required
   ↓
Scoped execution authority when needed
   ↓
Host-owned execution
   ↓
Audit residue
```

Natural strengths include:

- Explicit non-final outcomes.
- Deliberate acknowledgment and escalation boundaries.
- Ability to separate approval from later execution authority.
- Rich decision and execution provenance.
- Good fit for consequential multi-stage workflows.

Potential costs include:

- More state transitions.
- Resume and expiry logic.
- More persistence and evidence obligations.
- More testing, observability, and failure modes.
- Higher migration and maintenance burden.

This is not the default answer merely because it is the richest model.

Use it when the lifecycle actually requires those responsibilities.

---

# Part 1 — Build the Comparison Lens Before Reading the Scenarios

Do not start by choosing a favorite tool.

Create a worksheet with these decision dimensions.

## Problem Shape

Ask:

- Is the primary problem domain-rule evaluation?
- Is the primary problem request-local access control?
- Is the primary problem shared policy consistency across services?
- Is the primary problem distributed enforcement?
- Is the primary problem a consequential multi-stage lifecycle?

## Policy Ownership and Change Control

Ask:

- Who authors policy?
- Who approves policy changes?
- Does policy ship on the application's release train or independently?
- Must one policy vocabulary be shared across separately deployed services?
- How quickly must a bad or withdrawn policy be rolled back or revoked?
- What evidence proves which release was active when a decision was made?
- Does a convergence or revocation deadline turn policy distribution lag into a security property rather than only an operational concern?

Policy authorship and release cadence are often sharper architecture discriminators than the policy language itself.

## Decision Lifecycle

Record whether the system needs:

```text
Allow / Deny only

or

Allow / Deny / Defer / Acknowledge / Escalate
```

Then ask:

- Does a decision end the workflow?
- Can work pause and resume?
- Is acknowledgment distinct from authorization?
- Is escalation a routing outcome rather than a denial?
- Does execution happen later or in another host?
- Must approval be transformed into narrower execution authority?

## Trust Boundaries

Identify who owns:

- Actor identity.
- Resource state.
- Tenant, region, or jurisdiction facts.
- Policy content.
- Policy evaluation.
- Enforcement.
- Credentials for the protected side effect.
- Acknowledgment state.
- Capability issuance and validation, when present.
- Audit or decision evidence.

Do not treat caller-provided facts as authoritative merely because a policy engine accepts them.

## Operational Constraints

Record:

- Latency budget.
- Availability target.
- Whether a network call on every decision is acceptable.
- Whether disconnected operation is required.
- Local autonomy requirements.
- Policy distribution cadence.
- Acceptable policy staleness.
- Deployment complexity.
- Debuggability.
- Testability.
- Team capability and maintenance burden.
- Migration cost.
- Vendor or tool coupling.

## Failure Model

Ask what should happen when:

- Policy evaluation is unavailable.
- Policy is stale.
- Authoritative context cannot be resolved.
- The network partitions.
- A local policy bundle cannot be verified.
- Acknowledgment state is unavailable.
- Capability signing or verification is unavailable.
- Evidence persistence is unavailable.
- The protected executor is unavailable.

A timeout is not a governance decision.

A circuit breaker state is not a governance decision.

Fallback behavior is part of the architecture.

## Evidence Requirements

Decide what must survive to reconstruct an important decision.

Possible evidence includes:

```text
DecisionId
Actor / workload identity
Operation
Resource identity or version
Authoritative context identity or snapshot reference
Outcome
Reason code
PolicyId
PolicyVersion
PolicyFingerprint
Evaluator identity
Acknowledgment or escalation evidence
Capability identity and scope
PEP / execution-boundary identity
Execution attempt and result
Degraded-mode marker
Correlation / trace identifier
OccurredUtc
```

Do not automatically select every field.

The exercise asks for the **minimum evidence required by the scenario**.

Treat evidence as a budget, not a trophy list. Every retained field should answer a reconstruction, trust, or failure-analysis question. If you add a policy fingerprint, capability identifier, or full context snapshot to a simple same-process scenario, explain what question could not be answered reliably without it.

---

# Part 2 — Complete the Incomplete Decision Matrix

Start with this matrix.

Do not fill it by intuition alone.

Tie every entry to a requirement, assumption, or failure property.

Use this annotation format so two learners' tables remain comparable:

```text
Strong fit
Acceptable
Conditional — <state the condition>
Disproportionate — <the architecture can express the requirement, but its core responsibilities are unnecessary for this concern alone>
Weak fit (fundamental) — <the architecture does not naturally express the requirement without another boundary>
Weak fit (implementation) — <the architecture could express it, but this deployment or implementation makes it a poor fit>
Not applicable
```

One row is pre-filled only to calibrate the notation. It is **not** a universal answer key; if your assumptions differ, annotate the assumption and defend the different label.

| Requirement | Embedded rules | ASP.NET Core authorization | External policy engine | Distributed enforcement | Governed decision pipeline |
| --- | --- | --- | --- | --- | --- |
| Simple route/resource access | Conditional — acceptable when one application owns both the resource and the access rule; weakens when a second deployable must reuse the same rule | Strong fit — native request/resource authorization | Acceptable — can work, but may add an unnecessary policy runtime when no sharing is needed | Disproportionate — distributed decision/enforcement responsibilities exceed this concern alone | Disproportionate — richer lifecycle responsibilities exceed this concern alone |
| Multi-service policy consistency | ? | ? | ? | ? | ? |
| Acknowledgment lifecycle | ? | ? | ? | ? | ? |
| Escalation as a distinct outcome | ? | ? | ? | ? | ? |
| Offline/local evaluation | ? | ? | ? | ? | ? |
| Rich decision provenance | ? | ? | ? | ? | ? |
| Immediate same-request enforcement | ? | ? | ? | ? | ? |
| Delayed cross-process execution | ? | ? | ? | ? | ? |
| Independent policy deployment | ? | ? | ? | ? | ? |
| Low operational complexity | ? | ? | ? | ? | ? |

Use these calibration rules when choosing a label:

- **Strong fit** — the architecture's natural responsibility shape directly matches the requirement with little extra machinery.
- **Acceptable** — the requirement can be satisfied cleanly, but another option may do so with less operational or conceptual cost.
- **Conditional** — the fit depends on a fact such as deployment topology, policy ownership, latency, or failure behavior; state that fact.
- **Disproportionate** — the architecture can express the requirement naturally, but its defining responsibilities are materially broader than this concern alone. Use this when the problem is surplus architecture rather than a missing capability.
- **Weak fit (fundamental)** — satisfying the requirement would require adding a separate lifecycle or trust boundary that changes the architecture you are evaluating.
- **Weak fit (implementation)** — the limitation is not inherent to the architecture, but the stated deployment, team capability, or operational model makes it unattractive here.
- **Not applicable** — the requirement does not meaningfully test that architecture in the scenario being considered.

Do not reduce the table to numeric totals.

A score of `8` versus `7` does not decide a trust boundary.

---

# Part 3 — Scenario A: One ASP.NET Core Application, Ordinary Access Control

You maintain **Harbor Admin**, one ASP.NET Core application used by internal operators.

The application exposes:

```text
GET  /reports/{reportId}
POST /reports/{reportId}/archive
```

Treat these as facts:

1. The authenticated user's roles and claims are already available through the application identity system.
2. A report must be loaded before the archive decision because the decision depends on report ownership and status.
3. The application owns the report repository and can resolve the authoritative resource state locally.
4. The policy question is whether this authenticated actor may view or archive this report, and the result is consumed immediately by the current request.
5. No user-interruption or designated-reviewer workflow is currently specified for this operation.
6. If the request is permitted, execution happens immediately in the same application request.
7. No other service needs to reuse this policy.
8. Policy changes may ship with the application.
9. The team wants to minimize new infrastructure and keep debugging local.
10. A policy failure in Harbor Admin must not become a shared dependency outage for unrelated applications; no shared policy runtime exists today.
11. Current compliance needs are satisfied by normal security and operational logs. If you introduce a separate durable governance receipt, justify the reconstruction question that requires it.

## Your Task

Choose the primary architecture for Scenario A.

Then produce all seven required outputs listed later in the lab.

Do not assume the richest architecture is safer by definition.

Explain which requirement would be violated, if any, by choosing ordinary ASP.NET Core authorization.

Also explain whether embedded domain rules could still exist beside authorization without becoming the authority boundary for route/resource access.

If you conclude that no deferred state or portable execution capability is needed, derive that conclusion from the workflow and trust boundaries rather than from an explicit "not required" fact.

### Failure Prompt

Choose the failure that most influenced your architecture decision.

Possible choices include:

```text
Identity unavailable
Resource repository unavailable
Authorization handler throws
Executor / archive service unavailable
```

Explain whether the failure changes the authorization decision, prevents the decision from being made, or produces a later execution failure.

Do not collapse those states unnecessarily.

---

# Part 4 — Scenario B: Shared Policy Across Independently Deployed Services

You maintain a platform with these independently deployed components:

```text
Customer API
Billing API
Analytics API
Admin Portal
Export Worker
```

They all need to apply the same organization-wide data-access policy.

Treat these as facts:

1. Each service authenticates its own caller or workload.
2. Each service owns or can resolve the authoritative resource facts needed for its decision.
3. The shared policy must use one vocabulary for tenant, region, classification, and operation.
4. Policy authors need to change and test policy independently of application releases.
5. Decisions must preserve `PolicyId` and `PolicyVersion`; a content fingerprint is desirable when available.
6. For the operations in scope, `Allow` and `Deny` are sufficient.
7. No acknowledgment lifecycle is required.
8. No escalation workflow is required.
9. Each protected host can enforce the result immediately before its own side effect.
10. No approval needs to survive as a portable capability.
11. The central network is normally reliable, but a regional link can become unavailable for up to twenty minutes.
12. Read operations may continue under a documented bounded stale-policy rule if the architecture can prove which policy artifact is active.
13. Export and delete operations must not silently continue when current or explicitly permitted policy cannot be established.
14. Median policy-decision latency should remain below 15 ms inside a region.
15. The operations team can support either a shared remote policy runtime or centrally distributed policy evaluated locally, but not both in the first release.
16. The organization wants to avoid duplicating policy logic in every service.
17. When a service can reach the policy control plane, a critical policy withdrawal or emergency replacement must be reflected in its effective decision path within sixty seconds. If the service cannot receive or verify the withdrawal because of a partition, that loss of revocation knowledge must be observable and the architecture must state whether the affected operation class may continue under last-known-good policy or must stop.

This scenario intentionally permits more than one defensible architecture.

For example, a remote shared PDP and local evaluators using centrally distributed policy solve the consistency problem differently.

The lab does **not** tell you which one wins.

Before choosing, write one short paragraph that makes the best case for remote evaluation and one that makes the best case for local evaluation. If you cannot make both cases credible under the stated facts, revisit the scenario before selecting a winner.

Then resolve this deliberate collision explicitly:

> **A critical withdrawal is issued centrally, but the regional link fails before one service receives or verifies it. Does the bounded stale-read allowance in fact 12 survive, or does fact 17 force those reads to stop? State which rule wins for each operation class and why.**

There is no hidden universal answer. The point is to make revocation knowledge, artifact freshness, and partition behavior one coherent contract rather than three independent settings.

## Your Task

Choose one primary architecture and one deployment shape.

Examples of deployment shapes you may defend include:

```text
Remote external PDP

Local / sidecar PDP with centrally distributed policy

Central decision service with local PEPs
```

If you use the phrase `distributed enforcement`, identify the actual PEPs.

If you use local evaluation, define:

- Policy source.
- Policy artifact identity.
- Freshness rule.
- Activation rule.
- Stale-policy observability.
- How the sixty-second withdrawal/replacement target is enforced or how failure to converge is surfaced.

A minimally useful artifact shape might be:

```text
PolicyArtifact = {
  PolicyId,
  Version,
  Fingerprint,
  ActivatedAtUtc,
  MaxAge
}
```

The exact names are not prescribed. The point is to make identity, activation, and freshness testable rather than saying only "latest policy."

If you use remote evaluation, define:

- Timeout behavior.
- Partition behavior.
- Which operations may use a bounded last-known-good policy path, if any.
- Which operations must deny, defer, or otherwise stop.
- How the sixty-second withdrawal/replacement target changes the argument for a central evaluator or a cached/degraded path.

### Required Comparison

Your strongest rejected alternative must be genuinely credible.

Do not reject it with statements such as:

> Remote policy engines are too slow.

or:

> Local policy is always stale.

Use the actual scenario facts.

For example, compare:

- 15 ms latency.
- Twenty-minute regional partitions.
- Independent policy deployment.
- Policy staleness.
- Operational support cost.
- Failure blast radius.

### Failure Prompt

Choose one:

```text
Remote PDP unavailable
Policy bundle distribution delayed
Local policy bundle signature cannot be verified
Two services activate different policy versions
Authoritative resource lookup unavailable
```

Explain:

1. Which trust fact is missing or stale.
2. Whether the system can still make a policy decision.
3. Which operations may continue.
4. Which operations stop.
5. What evidence must show degraded operation.

---

# Part 5 — Scenario C: Consequential Operation with a Rich Decision Lifecycle

You maintain **Atlas Export**, a service that can export sensitive customer records to an external archive provider.

The public API accepts a proposal.

A separate worker owns the archive-provider credential and performs the external side effect later.

Treat these as facts.

### Decision Lifecycle Facts

1. Authentication and ordinary endpoint authorization already happen before the workflow begins.
2. The operation can produce these meaningful governance outcomes:

```text
Allow
Deny
Defer
RequireAcknowledgment
Escalate
```

3. Exports above 10,000 records require explicit user acknowledgment of a retention and privacy warning.
4. Exports containing a protected classification must be escalated to a designated reviewer rather than treated as a permanent denial.
5. A temporary legal hold should produce `Defer`.

### Execution Lifecycle Facts

6. Approved work is placed on a queue for later execution.
7. The worker is independently deployed and must not receive the caller's standing credentials.
8. Immediately before calling the archive provider, the worker must establish that this exact export request is currently permitted to execute. The architecture may satisfy that requirement with a scoped capability, execution-time reevaluation, or another defensible mechanism; the fact does not prescribe one.
9. The archive-provider credential remains host-owned by the worker.
10. The decision record must preserve policy identity/version, stable reason codes, and correlation to acknowledgment or escalation evidence.
11. The system must distinguish `Decision = Allowed` from `Execution = Failed`.
12. If required policy, acknowledgment state, authority verification, or durable evidence cannot be established, the system must not manufacture execution authority.
13. The team accepts additional state and operational complexity only where a stated lifecycle or trust boundary requires it; no component is justified merely for architectural symmetry.

## Your Task

Choose the architecture that best represents the full lifecycle.

Then identify which responsibilities may still be implemented by narrower components.

For example, your design may legitimately use:

```text
ASP.NET Core authorization
        ↓
External policy engine as evaluator
        ↓
Application-owned acknowledgment / escalation workflow
        ↓
Scoped capability issuer
        ↓
Worker PEP
        ↓
Host-owned executor
```

or another defensible composition.

The question is not:

> Which product does everything?

The question is:

> **Where does each lifecycle responsibility live, and which boundary prevents a blocked or incomplete decision from reaching execution?**

### Required Authority Explanation

Explain why this artifact:

```text
Decision = Allow
```

is or is not sufficient for the later worker.

If your architecture introduces a scoped capability, define at minimum:

```text
Operation
Resource / export request
Audience
Expiration
Use bound or replay semantics
Decision correlation
```

A bounded teaching example might look like:

```json
{
  "operation": "archive.export",
  "resource": "export-123",
  "audience": "atlas-export-worker",
  "expires_at": "2026-08-26T19:15:00Z",
  "max_uses": 1,
  "decision_id": "dec-123"
}
```

Possession of that object is not enough. Explain how the worker validates issuer trust or signature, audience, operation/resource binding, expiration, replay/use state, and any revocation or policy-freshness requirement that still applies at execution time.

If you choose reevaluation instead of a capability, define an execution-time check with comparable specificity. A teaching shape might be:

```text
ReexecutionCheck = {
  ExportRequestId,
  CurrentPolicyId + Version,
  ResourceFactsLoadedAtExecution,
  AcknowledgmentOrEscalationRef,
  WorkerAudience,
  DecisionCorrelationId
}
```

Then explain:

- Which policy is reevaluated and how current policy identity/freshness is established.
- Which current resource facts are loaded and which component is authoritative for them.
- How the original acknowledgment or escalation state is bound to the exact export request.
- How worker identity/audience is established at the execution boundary.
- How the later result correlates to the original decision without treating that decision as reusable authority.
- Why the worker still does not need the caller's standing credentials.

The two branches may converge more than their labels suggest. A short-lived capability that still requires current-policy freshness and a reevaluation path that binds prior acknowledgment/reviewer state can preserve nearly the same invariants; they place the trust anchor and continuation proof in different places. Explain that placement rather than treating `capability` and `reevaluate` as opposites.

### Required Revocation and Drift Explanation

Analyze this sequence even if it is not your primary failure mode:

```text
Decision = Allow
        ↓
scoped capability issued or work queued
        ↓
new legal hold or policy withdrawal becomes effective
        ↓
worker has not executed yet
```

Explain what blocks or permits the later side effect.

Your answer must state whether the design uses:

- active revocation;
- execution-time policy or resource-state reevaluation;
- a very short authority lifetime plus explicit freshness contract;
- or another mechanism with equivalent semantics.

Preserve this symmetry:

> **A prior `Allow` is not automatically a capability, and a cryptographically valid capability is not automatically proof that current policy still permits execution.**

### Failure Prompt

Choose the failure that most influenced your design:

```text
Policy evaluator unavailable
Acknowledgment store unavailable
Reviewer approved, but policy changed before execution
Capability signer unavailable
Capability verifier unavailable
Queue replays a message
Audit/evidence store unavailable
Archive provider unavailable after policy allowed the operation
```

Explain which stage fails and whether the original governance decision remains valid.

Do not rewrite an executor outage as a policy denial.

---

# Part 6 — Scenario D: Regional Facility with Intermittent Connectivity

A remote industrial facility must continue selected safety operations even when its connection to the central control plane is unavailable.

Treat these as facts:

1. The organization owns one centrally governed policy source.
2. The facility can be disconnected for up to eight hours.
3. A synchronous central policy call cannot be a hard prerequisite for every local safety decision.
4. The local facility can run a policy evaluator and enforce decisions beside the equipment gateway.
5. Policy artifacts can be distributed and verified before activation.
6. The local evaluator must expose active `PolicyId`, `PolicyVersion`, activation time, last successful verification/synchronization time, and degraded-mode state.
7. Last-known-good policy may be used for at most twelve hours, but the policy contract has not yet defined whether that window is measured from central issuance, local activation, or the facility's last successful verified synchronization. You must choose the reference clock and justify it.
8. While disconnected, these operations may continue if the policy artifact is still within the permitted freshness window:

```text
equipment.stop
equipment.safe-mode
status.read
```

9. While disconnected, these operations must not proceed:

```text
firmware.update
safety-limit.expand
new-device.enroll
```

10. Local resource and equipment state remain authoritative for the facility while disconnected.
11. Local decision and execution evidence must be retained and reconciled when connectivity returns.
12. The facility must not interpret `central unavailable` as `policy disabled`.
13. The team prefers the smallest architecture that preserves central policy ownership and bounded local autonomy.

## Your Task

Choose the primary policy architecture and enforcement location.

Your answer must explain the difference between:

```text
Centralized policy ownership
```

and:

```text
Centralized synchronous evaluation
```

They are not the same architectural decision.

If you choose locally evaluated policy, explain:

- How policy arrives.
- How it is authenticated or otherwise verified.
- How activation is recorded.
- Which event starts the twelve-hour freshness clock and why.
- How staleness is detected.
- Which PEP blocks non-degraded operations.
- What happens after twelve hours without a valid refresh.
- How local evidence is reconciled later.

Use this timeline to keep artifact age, connectivity state, and the degraded-mode window separate:

```text
Tref = freshness reference event chosen by you
  |
  +---- t + 8h  : maximum expected disconnection envelope
  |
  +---- t + 11h : stress point, still inside 12h freshness limit
  |
  +---- t + 12h : last-known-good limit expires
  |
  +---- t + 13h : stress point beyond both the freshness limit
                  and the stated 8h disconnection envelope
```

The `13h` case is intentionally outside the normal disconnection envelope. Treat it as a failure-model stress test, not as a contradiction in the scenario. Depending on the reference clock you chose, the `11h` point may also fall outside the stated eight-hour disconnection envelope—for example, if freshness starts at the last successful verified synchronization. State whether it does in your design and why; artifact age and time-since-disconnection are intentionally not assumed to be the same clock.

### Failure Prompt

Analyze:

```text
Central connectivity lost
        +
active local policy is 11 hours old
according to your chosen freshness reference clock
```

and then:

```text
Central connectivity still lost
        +
active local policy becomes 13 hours old
according to the same reference clock
```

Explain which operations change behavior and why.

Do not answer only with `fail open` or `fail closed`.

Name the operation class, trust property, freshness rule, and explicit behavior.

---

# Part 7 — Produce the Required Learner Outputs

For **each** scenario in your chosen path, produce these seven outputs. Short-path learners produce them for Scenarios A and C; full-submission learners produce them for all four scenarios.

## 1. Selected Architecture

Name the primary architecture and any essential composition.

Examples:

```text
ASP.NET Core resource-based authorization

External PDP + local PEPs

Local PDP + centrally distributed policy

Governed decision pipeline + external PDP + scoped worker capability
```

Do not answer with a vendor name alone.

## 2. Rationale Tied to Explicit Requirements

Cite at least **four** scenario facts that drove the selection.

Use this shape:

```text
Requirement / constraint
        ↓
Architectural consequence
        ↓
Selected responsibility or boundary
```

## 3. Strongest Rejected Alternative

Choose the alternative you would be most comfortable shipping if the selected design were unavailable.

Then explain why it lost **under the current facts**.

A weak rejected alternative is not useful.

## 4. Operational Failure Mode That Most Influenced the Decision

Name one failure and explain:

- What becomes unavailable, stale, or ambiguous.
- Which trust property can no longer be established.
- What the host or PEP should do.
- Whether the result is denial, deferral, escalation, bounded degraded behavior, or operational failure after an earlier decision.

## 5. Trust Boundary and Enforcement Location

Draw a small responsibility map.

For example:

```text
Caller
  ↓
Authoritative context owner
  ↓
PDP / evaluator
  ↓
PEP
  ↓
Protected executor
```

Annotate who is trusted for:

```text
Identity
Resource facts
Policy
Decision
Enforcement
Credentials
```

If several responsibilities live in one process, say so.

Logical separation still matters.

## 6. Minimum Evidence Required to Reconstruct an Important Decision

Choose the minimum set needed for that scenario.

Do not copy the entire evidence inventory automatically.

Use this falsifiable reconstruction test:

> **A reviewer holding only the retained decision record and the authoritative policy repository should be able to identify the policy and trusted inputs needed to explain or re-derive the same outcome, while still distinguishing that outcome from what execution later attempted or did.**

If the reviewer cannot answer a required reconstruction question, the record is too small. If a field does not help answer a required reconstruction, trust, or failure question, omit it or justify why it remains worth retaining.

Explain what question each retained field helps answer.

Also identify one tempting evidence field you deliberately **did not** retain. Weigh the audit or incident-response risk of omitting it against storage, retention, privacy, or high-cardinality cost.

For example:

```text
PolicyId + PolicyVersion
        ↓
Which policy release evaluated this request?
```

## 7. What Must Change Before More Complexity Is Justified

State at least **two** requirement changes that would cause you to reconsider the architecture.

Examples of change categories include:

- One application becomes many independent services.
- Policy ownership separates from application ownership.
- Acknowledgment becomes a legal or operational requirement.
- Escalation becomes a distinct state.
- Execution moves to a later worker.
- Disconnected operation becomes mandatory.
- A policy network hop becomes unacceptable.
- Durable provenance becomes mandatory.
- A portable narrow execution grant becomes necessary.

Do not write:

> We would upgrade when the system gets bigger.

Name the architectural pressure.

---

# Part 8 — Create a One-Page Decision Record for Each Scenario

Use this compact structure for each scenario in your chosen path: A and C for the short path, or A–D for the full submission.

```markdown
## Scenario <letter>

### Selected Architecture
<primary architecture and composition>

### Requirements That Drove the Choice
- ...
- ...
- ...
- ...

### Strongest Rejected Alternative
<alternative>

Why rejected here:
- ...

When it would be better:
- ...

### Most Important Failure Mode
<failure>

Behavior:
- ...

### Trust Boundary and PEP
<small diagram or prose map>

### Minimum Reconstruction Evidence
- ...

### Complexity Trigger
Reconsider this architecture if:
- ...
- ...
```

Keep the record concise enough that another architect can compare the scenarios in your chosen path side by side.

### Calibration Example — Separate Mini-Case

This is **not** an answer to Scenarios A–D. It shows the expected scope of a decision record while deliberately using a different problem shape.

Mini-case: one pricing service owns a set of product-eligibility rules. The question is ordinary domain-rule evaluation, not actor access control. The same service owns the facts and applies the result immediately. No independently deployed policy authority or cross-service enforcement contract exists.

```markdown
## Calibration Mini-Case

### Selected Architecture
Candidate A — embedded application rules / domain service.

### Requirements That Drove the Choice
- One service owns the pricing facts and rule lifecycle.
- The result is a domain outcome such as Eligible / Ineligible / ManualPricingReview.
- Evaluation and continuation remain in the same application boundary.

### Strongest Rejected Alternative
External policy engine.

Why rejected here:
- It could represent the conditions, but independent policy deployment and shared cross-service evaluation are not requirements.

When it would be better:
- Policy ownership separates from the pricing-service release train.
- Several deployables must reuse one rule vocabulary.

### Most Important Failure Mode
Required pricing facts unavailable.

Behavior:
- The domain result cannot be established.
- No price is finalized from guessed inputs.

### Trust Boundary and PEP
Pricing service owns authoritative product/customer facts -> embedded evaluator derives the domain result -> the same host applies that result to the pricing workflow. No distinct PEP exists here: evaluation and application share one host, and the domain outcome is not gating a protected side effect.

### Minimum Reconstruction Evidence
- Rule-set version — which rule release produced this outcome.
- Input/resource reference — which pricing case was evaluated.
- Derived outcome/reason — what the rules concluded and why.
- Correlation id + occurred time — where the decision sits in the request/workflow history.

### Complexity Trigger
Reconsider this architecture if:
- Independently owned policy must be deployed separately.
- Multiple services need one shared evaluation contract.
```

The objective is decision clarity, not document length.

---

# Part 9 — Challenge Your Own Answer

For each scenario in your chosen path, perform these adversarial checks. The aggregate-consistency challenge is optional on the short path unless your A/C design itself introduces multiple evaluators or PEPs; it is required for the full submission where B/D make fleet or regional consistency first-class concerns.

## Simplicity Challenge

Ask:

> What can I remove and still preserve every required lifecycle and trust boundary?

If removing a component does not break a requirement, explain why the component remains justified.

## Richness Challenge

Ask:

> Which requirement would become awkward, ambiguous, or unsafe if I used the next-simpler architecture?

If you cannot name one, you may be overengineering.

## Alternative-Validity Challenge

Ask:

> Under what changed facts would my rejected alternative become the better answer?

If the answer is `never`, re-check whether you selected a genuinely credible alternative.

## Trust Challenge

Ask:

> Which input could be caller-controlled, stale, or supplied by the wrong authority?

A perfect evaluator does not repair untrusted context.

## Failure Challenge

Ask:

> What does the architecture do when the decision dependency fails before policy can be established?

Do not let exception handling define governance accidentally.

## Aggregate Consistency Challenge

Ask:

> Can every individual node report a valid local policy state while the fleet is still inconsistent or outside its convergence contract?

A version-skew or partial-rollout failure may produce **no degraded state at any single evaluator**. Name the evidence or aggregate telemetry that would surface that condition, and distinguish node-local health from fleet-level policy consistency.

## Enforcement Challenge

Ask:

> Which exact component can stop the protected side effect?

A decision service that cannot prevent execution is not the PEP.

## Evidence Challenge

Ask:

> After an incident, can I distinguish what policy decided from what execution actually did?

`Allowed` and `ExecutedSuccessfully` are not synonyms.

---

# Part 10 — Architecture Review Rubric

Use this self-check after completing the scenarios in your chosen path. A `Yes` without an evidence pointer is incomplete.

| Review question | Yes / No | Evidence pointer |
| --- | --- | --- |
| Did I select an architecture for every scenario in my chosen path? | ? | ? |
| Is each choice tied to explicit scenario requirements rather than product preference? | ? | Fact numbers / notes |
| Did I identify the strongest rejected alternative? | ? | ? |
| Did I explain when that rejected alternative would be better? | ? | Changed requirement |
| Did I identify the most influential operational failure mode? | ? | ? |
| Is the trust boundary explicit? | ? | Diagram / owner map |
| Is the Policy Enforcement Point or protected execution boundary identifiable? | ? | Exact component |
| Did I distinguish caller-controlled facts from host-authoritative facts? | ? | ? |
| Did I preserve acknowledgment as distinct from authorization where required? | ? | ? |
| Did I preserve escalation as distinct from denial where required? | ? | ? |
| Did I avoid treating an `Allow` decision as portable execution authority automatically? | ? | ? |
| Did I also avoid treating a valid capability as automatic proof of current policy? | ? | Scenario C drift analysis |
| Did I define partition or stale-policy behavior where it matters? | ? | ? |
| Did I distinguish node-local health from fleet-level consistency where relevant? | ? | Scenario B or D evidence |
| Did I choose minimum reconstructable evidence rather than maximum telemetry by habit? | ? | Retained + omitted field |
| Did I state what requirement changes would justify more complexity? | ? | ? |
| Did I avoid assuming the richest architecture is automatically the safest? | ? | ? |

Before you mark the rubric complete, answer these required evidence prompts:

1. For each scenario in your chosen path, which **fact number** most directly caused your strongest rejected alternative to lose?
2. Which exact component is the PEP, and what protected side effect can it prevent?
3. Which evidence field did you deliberately omit, and what storage/retention/privacy cost did that avoid versus the audit risk it created?
4. For at least one distributed scenario, name a failure that may be invisible from a single healthy node and show how fleet-level evidence would reveal it.

A strong submission should make it possible for another reviewer to disagree with your choice while still understanding exactly why you made it.

That is a feature, not a failure.

Architecture is often a decision under constraints rather than a theorem with one answer.

---

# Post-Submission Discussion Guidance

Read this section **after** completing the decision records and adversarial checks. It is a set of review lenses, not a mapping from scenarios to answers.

Strong submissions usually distinguish:

- What an architecture **can** implement from what it expresses naturally with the least accidental complexity.
- Policy authorship and release ownership from the physical location where evaluation happens.
- Policy evaluation from enforcement, including the exact component that can stop the side effect.
- Central policy ownership from synchronous central evaluation.
- Request-local authorization states from durable workflow states such as deferral, acknowledgment, and designated-reviewer escalation.
- A prior policy decision from later execution authority, and later execution authority from current policy or resource state.
- Evaluator unavailability, stale policy, fleet inconsistency, and executor failure as different failure classes.
- Node-local health from aggregate convergence across several evaluators or PEPs.
- Minimum reconstruction evidence from maximum telemetry collection.
- A clean hybrid with named responsibility owners from a "hybrid" that hides authority and credentials behind one vague component.

The labels themselves are not graded. A different architecture can be correct when the stated facts, trust boundaries, failure behavior, and evidence remain coherent.

---

# Optional Extension — Change One Requirement and Re-Decide

Choose one scenario and change exactly one requirement.

Examples:

```text
Scenario A now needs independently deployed policy shared by five applications.
```

```text
Scenario B now requires acknowledgment before customer exports.
```

```text
Scenario C now executes immediately in the same process and no longer needs acknowledgment, escalation, or deferred work.
```

```text
Scenario D now has guaranteed low-latency connectivity and no local-autonomy requirement.
```

Re-run the decision.

Do not preserve your original architecture out of loyalty.

Ask whether the changed fact removes the reason for a boundary or creates a new one.

This extension is complete when you can say:

```text
This requirement changed
        ↓
therefore this architectural pressure changed
        ↓
therefore I kept / removed / moved this responsibility
```

---

# Related Learning Material

Use these references to challenge or deepen your reasoning:

- [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md) — detailed comparison of rules engines, PDPs, PEPs, distributed policy, latency, staleness, partitions, and local autonomy.
- [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md) — detailed comparison of framework authorization and broader governed execution.
- [When ASP.NET Core Authorization Is Not Enough](../articles/2026/when-aspnet-core-authorization-is-not-enough.md) — practitioner decision guide for stopping at authorization versus introducing a broader lifecycle.
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) — authoritative context and explicit decision semantics.
- [Constraint Composition and Policy Precedence](../governance/constraint-composition-and-policy-precedence.md) — explicit composition when several constraints contribute to one decision.
- [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) — policy identity, version, fingerprint, and decision reconstruction.
- [Practical Policy Testing and Decision-Table Strategies](../governance/practical-policy-testing-and-decision-table-strategies.md) — systematic policy test design and decision tables.
- [Policy Simulation and Change-Impact Analysis](policy-simulation-and-change-impact-analysis.md) — policy comparison and rollout analysis.
- [Safe Degraded Mode and Fail-Safe Governance](safe-degraded-mode-and-fail-safe-governance.md) — explicit behavior when policy, replay, acknowledgment, verification, evidence, or execution dependencies are unavailable.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — narrow execution authority across a later boundary.
- [Regional and Tenant Policy Overlays](../advanced/regional-and-tenant-policy-overlays.md) — composition of independently owned global, regional, and tenant policy layers.

---

## Final Self-Check

Before calling the lab complete, ask:

```text
Chosen architecture
      ↓
Can I explain why a simpler option is insufficient?
      ↓
Can I also explain when the simpler option would be better?
      ↓
Can I identify who authors policy, who decides, who enforces,
and what happens when a required trust fact is missing?
      ↓
Can I distinguish a healthy local node
from a fleet that is inconsistent overall?
```

If you can answer all four without relying on architecture labels alone, the exercise has done its job.

---

> **Read it. Run it. Question it. Improve it.**
