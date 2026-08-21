---
description: Learn to treat escalation as a non-executable governance outcome that transfers a decision problem to another authority while preserving context, provenance, and host-owned execution.
---

# Escalation Patterns in Governed Systems

**Learning objective:** Understand what should happen after `EscalationRecommended`, including how to create and route an escalation, preserve the original decision, gather additional evidence, re-evaluate current context, prevent loops, and keep protected execution separately controlled.

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md), [Constraint Composition and Policy Precedence](constraint-composition-and-policy-precedence.md), and [Policy Versioning and Decision Provenance](policy-versioning-and-decision-provenance.md)

## At a Glance

> **Problem:** A policy can determine that the current decision path should not execute and cannot safely resolve the request without another authority, but returning `EscalationRecommended` alone does not define routing, evidence, expiry, or resolution.
>
> **Core idea:** Treat escalation as a durable, non-executable governance state. Preserve the original decision, create an escalation record bound to the exact intent, route it to an eligible authority, gather additional evidence, and produce a new governance decision after re-evaluation.
>
> **Why it matters:** Escalation should move a decision problem, not silently broaden authority or mutate history so an originally escalated request appears to have been allowed.
>
> **Prefer something simpler when:** The current policy can already make a clear allow, deny, defer, or acknowledgment decision and no additional authority or evidence is actually needed.
>
> **Observe:** `EscalationRecommended` never invokes the protected executor. Any later continuation depends on a separate, current governance decision.

A representative lifecycle is:

```text
Policy cannot safely resolve request
        ↓
EscalationRecommended
        ↓
Create escalation record
        ↓
Route to appropriate authority
        ↓
Review / additional evidence
        ↓
Rebuild current context
        ↓
New governance decision
        ↓
Execution remains separately controlled
```

The central lesson is:

> **Escalation transfers a decision problem to another authority; it does not silently grant execution authority.**

---

## Escalation Is a Governance Outcome, Not an Execution Mode

An escalation result should mean something close to:

> The current decision path cannot authorize execution and recommends that another defined authority evaluate the unresolved decision problem.

It should not mean:

```text
EscalationRecommended
        ↓
Execute with elevated privileges
```

It should also not mean:

```text
EscalationRecommended
        ↓
Ask anyone nearby
        ↓
Execute if they say yes
```

The current path remains blocked.

A minimal invariant is:

```text
Decision = EscalationRecommended
        ↓
Protected executor invocation count = 0
```

That invariant continues to apply while escalation is pending, expired, cancelled, rejected, unroutable, or otherwise unresolved.

---

## Keep the Vocabulary Separate

Several concepts are easy to collapse into one generic "approval" or "exception" step.

| Concept | Primary question | Does it itself create execution authority? |
| --- | --- | --- |
| Escalation | Which other authority should receive this unresolved decision problem? | No |
| Acknowledgment | Did an identified actor accept a defined condition? | No |
| Approval | Did an eligible reviewer give a positive disposition for a defined request? | Not necessarily |
| Override | Did a specifically delegated authority supersede a policy result within defined limits? | Not by itself |
| Governance decision | What does current policy conclude from current context? | No |
| Execution authority | What narrow authority is valid at the protected boundary? | Only when the host validates and uses it |
| Execution | Did the protected side effect occur? | Yes |

The distinction can be written directly:

```text
Escalation
    ≠
Acknowledgment
    ≠
Approval
    ≠
Override
    ≠
Execution
```

An escalation may eventually route to a human approval workflow.

It may eventually route to an automated specialized policy service.

It may eventually produce an authorized override.

Those later outcomes do not change what escalation itself means.

---

## Why Escalation Happens

Escalation is useful when the current policy boundary can identify that ordinary evaluation is insufficient but should not guess at authority.

Common reasons include:

### Insufficient authority

The current actor, service, policy layer, or reviewer does not have enough authority to resolve the request.

```text
Local operator
      ↓
Operation exceeds local delegation
      ↓
EscalationRecommended
```

### Policy ambiguity

The applicable rules conflict, are incomplete, or require interpretation outside the current automated policy.

```text
Two policy sources apply
      ↓
No documented precedence resolves conflict
      ↓
EscalationRecommended
```

### Conflicting constraints

Independent constraints may produce information that the host cannot safely compose into an executable result without a higher-level decision.

```text
Safety constraint: block
Operational exception rule: possible exception
      ↓
Current policy cannot resolve conflict
      ↓
EscalationRecommended
```

### High consequence

The operation may be valid in principle but crosses a consequence threshold that requires a stronger authority.

```text
Normal export
      ↓
Allowed

Very large restricted export
      ↓
EscalationRecommended
```

### Unavailable evidence

A required fact may be unavailable, stale, or below an accepted confidence threshold.

```text
Required classification unavailable
      ↓
Current policy cannot safely conclude
      ↓
EscalationRecommended
```

### Exceptional conditions

An operation may fall outside the normal model:

```text
Declared emergency
Unusual legal hold
Cross-region incident
Exceptional customer remediation
Novel high-impact operation
```

The important design point is that the reason should be explicit.

Avoid:

```text
Escalate because something feels unusual.
```

Prefer stable reason codes that explain why ordinary policy stopped.

---

## Escalation Should Be Deliberate, Not a Catch-All

Do not use escalation as a substitute for defining ordinary outcomes.

For example:

```text
Dependency timed out
      ↓
EscalationRecommended
```

may be appropriate if the domain requires another authority to resolve the failure.

But it may be clearer to return:

```text
Deferred
```

if the only correct behavior is to wait and retry.

Likewise:

```text
Policy clearly prohibits operation
      ↓
EscalationRecommended
```

may be weaker than:

```text
Denied
```

if no override or alternative authority exists.

A useful question is:

> **What decision problem is being transferred, and who has legitimate authority to resolve it?**

If those questions have no answer, escalation may only be hiding an undefined policy.

---

## A Composition Example

Consider three independent contributions:

```text
Constraint A → Allowed
Constraint B → RequiresEscalation
Constraint C → NotApplicable
        ↓
Composed decision → EscalationRecommended
        ↓
No protected execution
```

`RequiresEscalation` in this diagram is a **teaching label for an escalation-producing contribution**.

It is not a claim that the current `AsiBackbone` constraint-result type exposes a `RequiresEscalation` enum value.

A fuller implementation can model the same architecture by allowing ordinary constraints to produce their supported results and using a broader host or decision-policy layer to introduce `EscalationRecommended`.

For example:

```text
Constraint A → Allow
Constraint B → Warning(reason = operation.exceeds-local-authority)
Constraint C → NotApplicable
        ↓
Base composition
        ↓
Host decision policy recognizes escalation condition
        ↓
EscalationRecommended
```

The architectural invariant remains:

> A final escalated decision is non-executable.

---

## Preserve the Original Decision

A later escalation result should not rewrite history.

Avoid this conceptual history:

```text
10:00 Original decision = EscalationRecommended
14:00 Senior reviewer allows
      ↓
Rewrite 10:00 decision to Allowed
```

That destroys the evidence that ordinary policy could not resolve the request.

Prefer:

```text
10:00 Decision A = EscalationRecommended
10:01 Escalation E created
13:55 Additional evidence supplied
14:00 Review completed
14:01 Decision B = Allowed
14:02 Scoped execution authority issued
14:03 Host executes
```

Decision A and Decision B answer different questions at different times.

Both may matter later.

---

## Model the Escalation Record Explicitly

A small teaching record might be:

```csharp
public enum EscalationStatus
{
    Pending,
    Routed,
    Resolved,
    Rejected,
    Cancelled,
    Expired,
    DeadLettered
}

public sealed record EscalationRecord(
    string EscalationId,
    string CorrelationId,
    string OperationName,
    string ResourceId,
    string IntentFingerprint,
    IReadOnlyList<string> ReasonCodes,
    string InitialPolicyVersion,
    string? InitialPolicyHash,
    string TargetKind,
    string TargetId,
    int Depth,
    string? ParentEscalationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    EscalationStatus Status);
```

This record answers:

```text
Which escalation?
Which original flow?
Which operation and resource?
Which exact intent?
Why was escalation recommended?
Which policy produced the recommendation?
Where is the decision problem being routed?
How deep is this escalation chain?
Did another escalation produce this one?
When was it created?
When does it expire?
What is its current lifecycle state?
```

The record does not execute anything.

---

## Bind Escalation to the Original Intent

Escalation should not become reusable standing permission for a broad category.

Avoid:

```text
Escalation:
"Approve exports"
```

Prefer a bound request such as:

```text
Operation:      data.export
Resource:       customer-ledger-2026-08
Destination:    partner-a
RecordCount:    250000
Classification: Restricted
Purpose:        quarterly-reconciliation
```

A canonical intent fingerprint can help preserve this binding:

```text
Canonical intent
      ↓
Intent fingerprint
      ↓
Decision A
      ↓
Escalation E
```

If material intent changes later, the escalation should not silently follow the new request.

Possible behavior:

```text
Intent changes materially
      ↓
Escalation E = Cancelled or Superseded
      ↓
New policy evaluation
```

The exact state vocabulary is application-specific.

The invariant is that an escalation should remain attached to the proposal that caused it.

---

## Correlate Escalation to the Original Decision

The escalation record should preserve enough identity to follow the lifecycle.

Useful fields may include:

```text
CorrelationId
OriginalDecisionId
EscalationId
ParentEscalationId
IntentFingerprint
ReasonCodes
InitialPolicyVersion
InitialPolicyHash
```

Not every application needs every identifier.

The goal is reconstructability:

```text
Original intent
      ↓
Original context
      ↓
Decision A
      ↓
Escalation E1
      ↓
Additional evidence
      ↓
Decision B
```

A reviewer should not need to infer which original request a later escalation belonged to.

---

## Escalation Target Selection

Escalation needs an explicit destination.

A target may be:

```text
Role
Team
Service
Policy authority
Regional authority
Tenant authority
Human review queue
Specialized automated evaluator
```

Target selection is itself a governance boundary.

Avoid:

```text
EscalationRecommended
      ↓
Send to default-admin@example.com
```

unless that destination is actually the defined authority for the request.

A target resolver might receive:

```csharp
public sealed record EscalationRoutingContext(
    string OperationName,
    string TenantId,
    string Region,
    string RiskBand,
    IReadOnlyList<string> ReasonCodes);
```

and return:

```csharp
public sealed record EscalationTarget(
    string Kind,
    string Id,
    string RequiredAuthority);
```

The resolver should use host-authoritative facts.

An untrusted requester should not be able to select the authority that will review its own request.

---

## Role-Based Escalation

A simple routing model may select a role.

For example:

```text
Reason:
account.disable.protected-account

Target role:
ProtectedAccountReviewer
```

or:

```text
Reason:
data.export.high-consequence

Target role:
SensitiveExportAuthority
```

The role should represent actual decision authority, not merely application access.

A person with permission to view the review page is not automatically authorized to resolve every escalation shown there.

---

## Tiered Escalation

Some domains use explicit tiers.

For example:

```text
Tier 0
Normal automated policy

Tier 1
Operations reviewer

Tier 2
Security or risk authority

Tier 3
Executive / emergency authority
```

A tiered model can be useful when authority increases with consequence.

It can also become ceremonial bureaucracy if every request climbs the same ladder.

A good tiered policy should define:

- Which reason codes map to which tier.
- Which scopes the tier may resolve.
- Whether the tier may allow, deny, defer, or escalate again.
- Which outcomes are non-overridable.
- Maximum depth.
- Time limits.
- Evidence requirements.

Do not let tier number alone imply unlimited authority.

---

## Regional and Tenant-Specific Escalation

Escalation targets may depend on jurisdiction or ownership.

For example:

```text
Tenant = tenant-a
Region = US
      ↓
US tenant-a review authority
```

versus:

```text
Tenant = tenant-a
Region = EU
      ↓
EU tenant-a review authority
```

The host should resolve those coordinates from authoritative context.

Do not trust:

```json
{
  "preferredEscalationRegion": "region-with-easier-policy"
}
```

as the source of routing authority.

This connects directly to [Regional and Tenant Policy Overlays](../advanced/regional-and-tenant-policy-overlays.md).

Policy scope and escalation authority should both be explicit.

---

## Human Versus Automated Escalation Targets

Escalation does not always require a person.

### Human target

Use when the unresolved question genuinely requires:

- Judgment.
- Accountability.
- Interpretation.
- Independent review.
- Business authority.
- Exceptional approval.

A human escalation can enter the lifecycle described in [Human-in-the-Loop Governance Workflows](human-in-the-loop-governance-workflows.md).

### Automated target

Use when another bounded system has the authority or evidence needed to resolve the question.

Examples:

```text
Specialized policy service
Regional policy evaluator
Risk adjudication service
Authoritative classification service
```

An automated target should still be treated as a separate authority boundary.

Its output should become evidence or a new decision input, not an invisible side effect.

---

## Additional Evidence During Escalation

Escalation often exists because the original decision lacked sufficient evidence.

A later stage may add:

```text
Manager justification
Risk assessment
Legal interpretation
Resource classification
Regional policy result
Incident status
Identity assurance
External approval reference
```

Represent additional evidence explicitly.

For example:

```csharp
public sealed record EscalationEvidence(
    string EvidenceId,
    string EscalationId,
    string EvidenceCode,
    string Source,
    string Value,
    DateTimeOffset ObservedAt);
```

The exact `Value` representation is domain-specific.

Do not copy unnecessary secrets, raw payloads, or personal data merely because the record is called evidence.

---

## Evidence Does Not Automatically Decide the Request

New evidence is an input.

It is not the final decision.

Avoid:

```text
Manager justification exists
      ↓
Allowed
```

Prefer:

```text
Manager justification
      +
Current actor facts
      +
Current resource state
      +
Current policy
      ↓
New governance decision
```

This preserves the same decision-before-execution architecture used throughout Learning.

---

## Re-Evaluate After Escalation

A successful escalation should normally produce a **new** decision.

Conceptually:

```text
Decision A:
EscalationRecommended
      ↓
Escalation E
      ↓
Additional evidence or higher authority
      ↓
Current context rebuilt
      ↓
Current policy evaluated
      ↓
Decision B:
Allowed / Warning / Denied /
Deferred / AcknowledgmentRequired /
EscalationRecommended
```

Decision B may still recommend escalation.

Decision B may deny.

Decision B may require acknowledgment.

Decision B may allow.

The escalation outcome does not predetermine the later decision.

---

## Context Freshness Matters

An escalation can outlive the original request by minutes, hours, or days.

During that time:

- Actor authorization can change.
- Resource ownership can change.
- Classification can change.
- Tenant can change.
- Region can change.
- Risk can change.
- Destination can change.
- System state can change.
- Policy can change.
- The requested operation may no longer be meaningful.

Therefore, avoid:

```text
Old context
      +
New approval
      ↓
Execute
```

Prefer:

```text
Escalation resolution
      ↓
Rebuild authoritative context
      ↓
Verify intent binding
      ↓
Evaluate current policy
```

A higher authority should not make stale facts current simply by reviewing them.

---

## Policy Identity Across Escalation

Preserve the policy identity that produced the initial escalation.

For example:

```text
Decision A
PolicyVersion = export/12
PolicyHash    = abc123
Outcome       = EscalationRecommended
```

Later:

```text
Decision B
PolicyVersion = export/13
PolicyHash    = def456
Outcome       = Allowed
```

The difference is meaningful.

A reviewer should be able to tell that policy changed during the escalation window.

This is not a reason to rewrite Decision A.

It is a reason to preserve both decisions.

See [Policy Versioning and Decision Provenance](policy-versioning-and-decision-provenance.md).

---

## Resolution Should Produce a New Decision

A later authority may return a disposition such as:

```text
Evidence accepted
Exception valid
Additional review completed
Regional authority satisfied
```

That disposition should feed a new governance evaluation.

Avoid mutating:

```text
Decision A.Outcome =
    EscalationRecommended
        ↓
    Allowed
```

Prefer append-style history:

```text
Decision A = EscalationRecommended
Escalation E = Resolved
Decision B = Allowed
```

The same applies when the later result is denial:

```text
Decision A = EscalationRecommended
Escalation E = Rejected
Decision B = Denied
```

Historical decisions should remain historical.

---

## Escalation Is Not an Override

An escalation authority can sometimes possess override authority.

That is not inherent in escalation.

For example:

```text
Escalation target:
SecurityReviewTeam
```

may have authority only to:

```text
Request more evidence
Deny
Recommend a different policy path
```

while:

```text
EmergencyOfficer
```

may have a narrowly delegated override capability.

Keep those contracts separate.

An escalation route should state what the target may do.

Do not infer:

```text
Escalated to senior person
      ↓
Senior person may override anything
```

---

## Some Decisions Should Remain Non-Overridable

An escalation chain should not guarantee eventual approval.

Examples of non-overridable boundaries may include:

```text
Cryptographic verification failure
Cross-tenant prohibition
Mandatory legal hold
Unsupported operation
Safety interlock unavailable
Resource no longer exists
```

A system may legitimately define:

```text
Denied
      ↓
No escalation path
```

or:

```text
Escalation for investigation
      ↓
No authority to convert denial into execution
```

Escalation can support explanation or remediation without granting an override.

---

## Re-Routing

An escalation may reach the wrong authority or become unroutable.

Possible responses include:

```text
Re-route to another eligible authority
Escalate to next tier
Return to origin for missing evidence
Cancel
Expire
Dead-letter
```

A re-route should preserve history:

```text
E1 target = TeamA
E1 routing result = NotApplicable
      ↓
E2 parent = E1
E2 target = TeamB
```

Do not overwrite TeamA with TeamB and erase the first routing attempt if the route matters operationally.

---

## Escalation Loops

Poor routing can create loops:

```text
Team A
  ↓
Team B
  ↓
Regional authority
  ↓
Team A
  ↓
Team B
  ↓
...
```

A system should be able to detect and stop that pattern.

Useful protections include:

- Maximum escalation depth.
- Previously visited target detection.
- Repeated reason-code detection.
- Repeated policy/version detection.
- Explicit terminal routing failures.
- Dead-letter state.
- Operator diagnostics.

The goal is not to prevent every multi-hop escalation.

The goal is to prevent an unresolved decision from circulating forever.

---

## Maximum Escalation Depth

A simple rule can be:

```text
MaximumDepth = 3
```

Then:

```text
E1 depth = 1
E2 depth = 2
E3 depth = 3
Attempt E4
      ↓
DeadLettered
```

The maximum should reflect the domain.

A depth of one may be enough for a simple application.

A distributed policy system may legitimately need more.

The important point is that depth is deliberate and testable.

---

## Dead-Letter and Terminal States

Some escalations cannot be resolved safely.

A terminal state can make that explicit:

```text
DeadLettered
```

Possible reasons include:

```text
escalation.max-depth-exceeded
escalation.no-eligible-target
escalation.routing-loop-detected
escalation.required-authority-unavailable
escalation.invalid-evidence
```

A dead-letter state should not imply execution.

It means the normal governance lifecycle ended without executable authority.

The host may expose remediation, support, or manual investigation separately.

---

## Expiration

Escalations should not necessarily remain valid forever.

A record can include:

```text
CreatedAt
ExpiresAt
```

When it expires:

```text
Pending escalation
      ↓
ExpiresAt passes
      ↓
Expired
      ↓
No execution
```

A late resolution should not automatically revive the old escalation.

The host may require a new policy evaluation and, if still necessary, a new escalation.

---

## Timeout

Timeout is an operational event.

It should not silently become a policy allowance.

Avoid:

```text
Escalation timed out
      ↓
Assume approved
```

unless a domain has explicitly accepted that fail-open posture.

More common responses may be:

```text
Expire
Re-route
Escalate to a fallback authority
Defer
Deny
Dead-letter
```

The choice belongs to policy.

---

## Cancellation

A host may cancel an escalation because:

- The operation was cancelled.
- The resource no longer exists.
- The request was superseded.
- A security incident invalidated the workflow.
- The requester withdrew.
- A new policy made the request irrelevant.

Cancellation should make old escalation responses stale.

```text
Escalation = Cancelled
      ↓
Late "Approve" response
      ↓
Rejected as stale
```

---

## Rejection

An escalation authority may conclude:

```text
No
```

That should be preserved as a terminal disposition or as evidence feeding a new denied governance decision.

Prefer:

```text
Decision A = EscalationRecommended
Escalation = Rejected
Decision B = Denied
```

rather than:

```text
Decision A rewritten to Denied
```

Both Decision A and Decision B can be meaningful.

---

## Authority Unavailable

The escalation authority itself may be unavailable.

Examples:

```text
Human review queue offline
Regional service unreachable
No eligible reviewer on call
External adjudication service unavailable
```

The system needs an explicit degraded posture.

Possible policy choices:

```text
Defer
Re-route
Escalate to fallback authority
Deny
Dead-letter
```

The unsafe default is:

```text
Escalation authority unavailable
      ↓
Execute anyway
```

Availability problems should not silently broaden authority.

---

## A Small Routing Example

A teaching router might look like:

```csharp
public sealed class EscalationRouter
{
    public EscalationTarget Resolve(
        EscalationRoutingContext context)
    {
        if (context.ReasonCodes.Contains(
                "data.export.high-consequence"))
        {
            return new(
                Kind: "Role",
                Id: "SensitiveExportAuthority",
                RequiredAuthority:
                    "data.export.resolve-escalation");
        }

        if (string.Equals(
                context.Region,
                "EU",
                StringComparison.OrdinalIgnoreCase))
        {
            return new(
                Kind: "Service",
                Id: "eu-policy-authority",
                RequiredAuthority:
                    "policy.eu.resolve-escalation");
        }

        return new(
            Kind: "Role",
            Id: "OperationsReview",
            RequiredAuthority:
                "operations.resolve-escalation");
    }
}
```

This is intentionally small.

A production system may use configuration, a policy engine, a queue, regional services, or organizational workflow tooling.

The lesson is that routing rules should be visible and reviewable.

---

## A Small Escalation Coordinator

The coordinator should stop before protected execution.

```csharp
public sealed class EscalationCoordinator
{
    private readonly IEscalationStore escalationStore;
    private readonly IEscalationTargetResolver targetResolver;
    private readonly IPolicyContextFactory contextFactory;
    private readonly IPolicyEvaluator policyEvaluator;

    public EscalationCoordinator(
        IEscalationStore escalationStore,
        IEscalationTargetResolver targetResolver,
        IPolicyContextFactory contextFactory,
        IPolicyEvaluator policyEvaluator)
    {
        this.escalationStore = escalationStore;
        this.targetResolver = targetResolver;
        this.contextFactory = contextFactory;
        this.policyEvaluator = policyEvaluator;
    }

    public async Task<EscalationResolution> ResolveAsync(
        string escalationId,
        IReadOnlyList<EscalationEvidence> evidence,
        CancellationToken cancellationToken)
    {
        EscalationRecord escalation =
            await escalationStore.GetAsync(
                escalationId,
                cancellationToken);

        if (escalation.Status is
            EscalationStatus.Cancelled or
            EscalationStatus.Expired or
            EscalationStatus.DeadLettered)
        {
            return EscalationResolution.Blocked(
                "escalation.not-active");
        }

        CurrentPolicyContext context =
            await contextFactory.BuildAsync(
                escalation,
                evidence,
                cancellationToken);

        if (!string.Equals(
                context.IntentFingerprint,
                escalation.IntentFingerprint,
                StringComparison.Ordinal))
        {
            return EscalationResolution.Blocked(
                "escalation.intent-changed");
        }

        GovernanceDecision newDecision =
            await policyEvaluator.EvaluateAsync(
                context,
                cancellationToken);

        return EscalationResolution.FromDecision(
            newDecision);
    }
}
```

The coordinator returns a new decision.

It does not call the protected executor.

---

## Execution Remains Separately Controlled

Suppose the new decision is allowed.

The host may still require:

- Acknowledgment.
- Human approval.
- Capability issuance.
- Capability validation.
- Current authorization.
- Idempotency checks.
- Final resource-state checks.
- Gateway validation.

The escalation lifecycle therefore ends at a decision boundary:

```text
Escalation resolution
      ↓
New governance decision
      ↓
Normal governed continuation
```

not:

```text
Escalation resolution
      ↓
Protected side effect
```

---

## Multi-Hop Escalation

A higher authority can legitimately conclude that still another authority is required.

For example:

```text
Decision A = EscalationRecommended
      ↓
E1 → Operations Review
      ↓
Decision B = EscalationRecommended
      ↓
E2 → Security Authority
      ↓
Decision C = Allowed
```

Preserve every hop:

```text
E1.ParentEscalationId = null
E1.Depth = 1

E2.ParentEscalationId = E1
E2.Depth = 2
```

This creates a reconstructable chain.

Do not treat Decision C as evidence that Decision A should have been allowed at the time.

---

## Provenance Across Each Hop

Each escalation hop should preserve enough evidence to answer:

```text
Which decision caused this hop?
Which policy version produced that decision?
Which reason codes triggered routing?
Which target received the escalation?
Which evidence was added?
Which authority responded?
Which policy version produced the next decision?
```

A conceptual timeline may look like:

```text
Decision A
  policy = export/12
  outcome = EscalationRecommended
      ↓
Escalation E1
  target = OperationsReview
  reason = data.export.high-consequence
      ↓
Evidence X
  source = OperationsReview
      ↓
Decision B
  policy = export/12
  outcome = EscalationRecommended
      ↓
Escalation E2
  target = SecurityAuthority
      ↓
Evidence Y
      ↓
Decision C
  policy = export/13
  outcome = Allowed
```

That timeline is more informative than one mutable row called `approval_status`.

---

## Audit Residue

Useful lifecycle events can include:

```text
decision.escalation-recommended
escalation.created
escalation.routed
escalation.rerouted
escalation.evidence-added
escalation.resolved
escalation.rejected
escalation.cancelled
escalation.expired
escalation.dead-lettered
decision.re-evaluated
capability.issued
execution.completed
```

The exact event taxonomy is application-specific.

The important boundary is append-style explanation rather than silent mutation.

As with other governance evidence, avoid claiming immutability or tamper-evidence unless the storage and signing design actually provides those properties.

---

## Escalation and Human Review

Escalation can route into a human workflow:

```text
EscalationRecommended
      ↓
Target = HumanReview
      ↓
Pending human review
      ↓
Human disposition
      ↓
Revalidation
      ↓
New governance decision
```

The human-review lifecycle still applies:

- Reviewer identity.
- Eligibility.
- Scope.
- Separation of duties where required.
- Quorum.
- Expiration.
- Cancellation.
- Policy drift.
- Context drift.
- Bound intent.

Escalation does not bypass those requirements.

See [Human-in-the-Loop Governance Workflows](human-in-the-loop-governance-workflows.md).

---

## Escalation and Acknowledgment

Acknowledgment satisfies a defined responsibility boundary.

Escalation transfers an unresolved decision problem.

For example:

```text
Policy says:
Requester must acknowledge warning
      ↓
AcknowledgmentRequired
```

versus:

```text
Policy says:
Current authority cannot resolve protected-account change
      ↓
EscalationRecommended
```

An escalated authority may later require acknowledgment.

That does not make escalation and acknowledgment equivalent.

---

## Escalation and Override

An escalation route can lead to an authority that is permitted to apply an override.

If so, model the override explicitly.

For example:

```text
Escalation E
      ↓
Emergency authority
      ↓
OverrideGrant
  scope = operation X
  resource = Y
  expires = T
  reason = emergency.exception
      ↓
Current governance evaluation
```

Do not represent the entire sequence as:

```text
Escalated = true
```

The system should be able to explain which authority exercised which delegated exception.

---

## Escalation and Risk-Based Decisions

A risk policy may map:

```text
Low       → Allowed
Moderate  → AcknowledgmentRequired
High      → EscalationRecommended
Critical  → Denied
```

That mapping is policy.

If `High` produces escalation, the escalation record should preserve relevant risk reason codes and policy identity without turning a risk score into execution authority.

If risk changes while escalation is pending, current context should be rebuilt before the new decision.

See [Risk-Based Decisions in Governed Systems](risk-based-decisions-in-governed-systems.md).

---

## Escalation in AI-Assisted Execution

AI-proposed operations make the boundary especially visible.

Avoid:

```text
AI proposes high-risk action
      ↓
Policy returns EscalationRecommended
      ↓
Model chooses escalation target
      ↓
Model calls privileged tool
```

Prefer:

```text
AI proposal
      ↓
Host builds authoritative context
      ↓
Governance decision
      ↓
EscalationRecommended
      ↓
Host creates escalation record
      ↓
Host routes to authorized target
      ↓
Additional evidence / review
      ↓
Host rebuilds context
      ↓
New governance decision
      ↓
Scoped host-owned execution if later permitted
```

The model may propose.

The model should not choose its own higher authority or execute through an escalation channel.

---

## Escalation in Regional or Tenant Policy

A global policy may know that another authority is required without owning that authority.

For example:

```text
Global policy
      ↓
EU-specific unresolved condition
      ↓
EscalationRecommended
      ↓
EU policy authority
```

or:

```text
Platform policy
      ↓
Tenant-owned exception question
      ↓
EscalationRecommended
      ↓
Tenant governance authority
```

The routing decision should preserve:

- Applicable region.
- Applicable tenant.
- Original policy identity.
- Escalation reason.
- Target authority.
- Any override rights.

A regional or tenant target should not silently broaden a mandatory global denial unless the overlay contract explicitly grants that authority.

---

## Decision Tables for Escalation

A small table can make routing behavior explicit.

| Initial condition | Initial outcome | Escalation target | Later evidence | New outcome |
| --- | --- | --- | --- | --- |
| Protected account | `EscalationRecommended` | ProtectedAccountReviewer | Protection exception valid | `Allowed` |
| Protected account | `EscalationRecommended` | ProtectedAccountReviewer | No exception | `Denied` |
| High-consequence export | `EscalationRecommended` | SensitiveExportAuthority | Additional justification | `AcknowledgmentRequired` |
| Regional ambiguity | `EscalationRecommended` | RegionalPolicyAuthority | Regional rule prohibits | `Denied` |
| Missing required evidence | `EscalationRecommended` | EvidenceAuthority | Evidence still unavailable | `Deferred` |

The new outcome should be tested as a separate decision.

---

## Routing Tests

Test target selection directly.

For example:

```text
Reason = data.export.high-consequence
      ↓
Target = SensitiveExportAuthority
```

and:

```text
Region = EU
Reason = regional.policy-ambiguity
      ↓
Target = eu-policy-authority
```

Also test:

- No eligible target.
- Unknown reason.
- Cross-tenant target mismatch.
- Untrusted requester-supplied target.
- Fallback target.
- Re-routing.
- Maximum depth.

---

## Core Escalation Invariants

At minimum, test these architectural properties.

### Escalation Does Not Execute

```text
Final decision = EscalationRecommended
      ↓
Executor invocation count = 0
```

### Pending Escalation Does Not Execute

```text
Escalation status = Pending
      ↓
Executor invocation count = 0
```

### Expired Escalation Does Not Execute

```text
Escalation status = Expired
      ↓
Late resolution
      ↓
Executor invocation count = 0
```

### Cancelled Escalation Does Not Execute

```text
Escalation status = Cancelled
      ↓
Late approval or evidence
      ↓
Executor invocation count = 0
```

### Intent Drift Blocks Resolution

```text
Escalation intent fingerprint
    !=
Current intent fingerprint
      ↓
No executable continuation
```

### New Decision Is Distinct

```text
Decision A = EscalationRecommended
      ↓
Escalation resolved
      ↓
Decision B = Allowed
      ↓
Decision A remains EscalationRecommended
```

### Policy Drift Is Visible

```text
Decision A policy = v12
Decision B policy = v13
      ↓
Both identities preserved
```

### Maximum Depth Stops Loops

```text
Current depth = MaximumDepth
      ↓
Another escalation requested
      ↓
DeadLettered or other terminal posture
```

### Missing Authority Does Not Fail Open

```text
Escalation target unavailable
      ↓
No protected execution
```

---

## A Focused Composition Test

The issue becomes concrete when multiple constraint contributions exist.

A teaching test can represent:

```text
Constraint A → Allowed
Constraint B → escalation signal
Constraint C → NotApplicable
      ↓
Final decision = EscalationRecommended
```

Then assert:

```csharp
Assert.Equal(
    GovernanceDecisionOutcome.EscalationRecommended,
    decision.Outcome);

Assert.Equal(
    0,
    executor.InvocationCount);
```

The second assertion is as important as the first.

A system does not have a meaningful escalation boundary if an escalated decision can still fall through into execution.

---

## Test Historical Integrity

A later resolution should not mutate the earlier record.

For example:

```text
Decision A ID = d-1
Outcome = EscalationRecommended

Decision B ID = d-2
Outcome = Allowed
```

Test that:

```text
Read d-1
      ↓
Still EscalationRecommended
```

after Decision B exists.

This protects provenance from becoming a mutable status summary.

---

## Common Failure Modes

### 1. Escalation Is Just Another Name for Denial

The system returns `EscalationRecommended` but provides no routing or lifecycle.

If no target, evidence, or later decision exists, `Denied` or `Deferred` may be clearer.

### 2. Escalation Automatically Elevates Privileges

```text
Escalated
      ↓
Run as administrator
```

The escalation path has become an execution bypass.

### 3. The Requester Chooses the Escalation Authority

The requester routes itself to the easiest reviewer or policy service.

Target selection should use authoritative policy context.

### 4. Escalation Mutates the Original Decision

Historical evidence no longer shows that ordinary policy could not resolve the request.

### 5. A Senior Reviewer Is Treated as Unlimited Override Authority

Organizational seniority is confused with explicit policy authority.

### 6. Additional Evidence Automatically Allows

Evidence becomes an implicit policy result.

### 7. Stale Context Is Reused

A later authority approves an intent whose resource, risk, region, actor, or policy changed.

### 8. Escalation Has No Expiration

An old escalation can be resolved months later and unexpectedly resume a stale operation.

### 9. Unavailable Authority Fails Open

The escalation service is down, so the operation executes anyway.

### 10. Re-Routing Erases History

The target field is overwritten repeatedly and no one can reconstruct where the request traveled.

### 11. Escalation Loops Forever

No depth, visited-target, or terminal-state protection exists.

### 12. Dead-Letter Means "Someone Will Fix It"

The system has a terminal state but no operational ownership or observability.

### 13. Human Review Is Assumed to Be Escalation

A human workflow is inserted even when the unresolved authority is actually automated or regional.

### 14. Escalation ID Becomes an Execution Token

Knowledge of the workflow identifier is mistaken for capability or permission.

### 15. Escalation Evidence Stores Sensitive Payloads Indiscriminately

Governance evidence becomes a secondary data-exposure surface.

---

## When a Simpler Pattern Is Better

Do not add escalation because an operation sounds important.

A direct denial is better when:

- The policy clearly prohibits the operation.
- No other authority may change that result.
- No remediation or evidence path exists.

A deferment is better when:

- The only issue is temporary unavailability.
- No higher authority is needed.
- The host should simply retry or wait.

Acknowledgment is better when:

- The policy decision is already understood.
- A defined actor must accept a specific condition.
- No transfer of decision authority is required.

Human review is better modeled directly when:

- The requirement is specifically "an eligible reviewer must decide."
- There is no intermediate routing problem.

Escalation becomes useful when:

- The current authority cannot legitimately resolve the request.
- Another authority has defined scope to decide.
- Additional evidence must enter the decision.
- The route itself is meaningful.
- The workflow may require multiple bounded hops.
- Historical provenance across authorities matters.

Use the smallest architecture that represents the actual authority change.

---

## Working Implementation References

This material is framework-neutral.

The `AsiBackbone/AsiBackbone` repository already exposes the structured outcome and decision-policy seams that can support a host-owned escalation lifecycle, but the Learning pattern here should not be read as a claim that the package owns a complete escalation-routing engine.

| Learning concept | Working implementation reference | What to inspect |
| --- | --- | --- |
| Escalation outcome vocabulary | [`GovernanceDecisionOutcome`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecisionOutcome.cs) | The framework outcome that includes `EscalationRecommended`. |
| Structured decision | [`GovernanceDecision`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecision.cs) | Outcome, reason codes, correlation/trace identifiers, and policy identity that a host can preserve before routing. |
| Post-composition decision policy | [`IAsiBackboneDecisionPolicy`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Evaluation/IAsiBackboneDecisionPolicy.cs) | The host/domain boundary where broader policy can refine a composed result. |
| Escalation example | [Custom Decision Policy Examples](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/custom-decision-policy-examples.md) | A gateway-readiness example that can return escalation without performing the protected action. |
| Audit evidence | [`AuditResidue`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Audit/AuditResidue.cs) | Structured evidence that can preserve decision outcome, reasons, policy identity, and correlation. |
| Host execution boundary | [Host-Owned Execution Enforcement](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/host-owned-execution-enforcement.md) | Why a governance result still requires explicit host enforcement before side effects. |
| High-consequence scenario | [High-Risk Administrative Action Scenario](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/scenarios/high-risk-administrative-action.md) | A scenario where escalation-recommended outcomes remain non-executable and host-controlled. |

A host may implement escalation persistence and routing using:

```text
Application database
Workflow engine
Queue
Ticketing / review system
Regional policy service
Human review service
```

The implementation choice does not change the core boundary:

```text
EscalationRecommended
      ↓
No execution
      ↓
Host-owned routing
      ↓
New evidence / authority
      ↓
New governance decision
      ↓
Host-owned continuation
```

---

## Review Questions

Before implementing escalation, you should be able to answer:

1. What exact decision problem is being transferred?
2. Why can the current policy or authority not resolve it?
3. Which stable reason codes trigger escalation?
4. Which authority is eligible to receive it?
5. How is that target selected from authoritative context?
6. Is the target human or automated?
7. What authority does the target actually possess?
8. Can the target override, or only provide evidence and a disposition?
9. Which outcomes are non-overridable?
10. How is escalation bound to the original intent?
11. Which original policy version and hash are preserved?
12. What additional evidence may be supplied?
13. How is sensitive evidence minimized?
14. Which context facts must be refreshed before resolution?
15. What happens if policy changes while escalation is pending?
16. Does resolution produce a new decision rather than mutate the old one?
17. How are re-routing and parent escalation relationships recorded?
18. What prevents escalation loops?
19. What is the maximum escalation depth?
20. What terminal state applies when no authority can resolve the request?
21. How long may the escalation remain active?
22. What happens on timeout or cancellation?
23. What happens when the escalation authority is unavailable?
24. Can a late response revive an expired or cancelled escalation?
25. Does `EscalationRecommended` always keep the executor invocation count at zero?
26. Does any later allowed decision still pass through normal scoped, host-owned execution controls?

If several answers are unclear, the system may have an escalation label, but it does not yet have a defined escalation architecture.

---

## Related Content

- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) — begin with the structured `EscalationRecommended` outcome and explicit decision-time facts.
- [Decision Before Execution](../tutorials/decision-before-execution.md) — preserve the invariant that escalation never falls through into the protected side effect.
- [Constraint Composition and Policy Precedence](constraint-composition-and-policy-precedence.md) — decide how escalation-producing conditions participate in broader policy composition.
- [Human-in-the-Loop Governance Workflows](human-in-the-loop-governance-workflows.md) — route escalations into bounded human review when a person is the appropriate authority.
- [Risk-Based Decisions in Governed Systems](risk-based-decisions-in-governed-systems.md) — map high consequence or uncertainty into escalation without turning risk itself into authority.
- [Policy Versioning and Decision Provenance](policy-versioning-and-decision-provenance.md) — preserve initial and later policy identities across a multi-stage decision path.
- [Practical Policy Testing and Decision-Table Strategies](practical-policy-testing-and-decision-table-strategies.md) — make routing, timeout, depth, degraded behavior, and execution invariants regression-testable.
- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) — distinguish escalation from acknowledgment and preserve correlated lifecycle evidence.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — keep later executable authority narrow even after an escalation resolves favorably.
- [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) — apply escalation to AI-proposed actions without giving the model routing or execution authority.
- [Regional and Tenant Policy Overlays](../advanced/regional-and-tenant-policy-overlays.md) — align escalation routing with regional, tenant, and policy-authority boundaries.
- [Safe Degraded Mode and Fail-Safe Governance lab](../labs/safe-degraded-mode-and-fail-safe-governance.md) — practice explicit failure posture when required governance dependencies are unavailable.
- [Threat Modeling as Architecture Reasoning](../security/threat-modeling-as-architecture-reasoning.md) — analyze routing manipulation, stale evidence, loop, override, and execution-bypass threats.

---

> **Escalation moves the unresolved decision to another authority; it never makes the protected operation executable by itself.**
