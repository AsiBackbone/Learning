---
description: Learn to model human review as an explicit governed workflow state with bound reviewer decisions, revalidation, scoped authority, and host-owned execution.
---

# Human-in-the-Loop Governance Workflows

**Learning objective:** Understand how a consequential workflow can pause for human review, preserve a pending governance state, bind a human disposition to an exact proposed intent, revalidate policy and context, and resume only through scoped host-controlled execution.

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md), [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md), and [Policy Versioning and Decision Provenance](policy-versioning-and-decision-provenance.md)

## At a Glance

> **Problem:** A consequential workflow may need to stop and wait for a person, but a generic `Approve` button can blur acknowledgment, approval, override, authorization, and execution authority into one opaque step.
>
> **Core idea:** Represent human review as an explicit pending state. Bind the review to an exact intent and eligible reviewer scope, record the human disposition, refresh authoritative context, re-evaluate policy, and issue only the execution authority the host still considers valid.
>
> **Why it matters:** A delayed human decision can outlive the policy, resource state, requester authority, or risk context that produced the original review requirement.
>
> **Prefer something simpler when:** The operation is immediate, low consequence, ordinary authorization is sufficient, or a conventional confirmation interaction fully expresses the requirement without a durable review lifecycle.
>
> **Observe:** While review is pending, or when no valid human disposition exists, the protected executor is never invoked.

The representative lifecycle is:

```text
Intent
   ↓
Policy evaluation
   ↓
Human review required
   ↓
Pending governance state
   ↓
Human disposition
   ↓
Policy/context revalidation
   ↓
Scoped execution authority
   ↓
Host-owned execution
```

The central lesson is:

> **Human participation is another governed boundary, not an escape hatch around governance.**

A person may contribute judgment, approval, rejection, rationale, or a formally delegated override.

That does not mean the person automatically acquires unlimited execution authority.

---

## The Problem: A Workflow That Outlives the Request

Synchronous request/response code encourages a simple mental model:

```text
Request
   ↓
Decision
   ↓
Execution
```

Human review changes the timeline.

For example:

```text
09:00  Request submitted
09:01  Policy requires human review
11:00  Policy version changes
13:30  Reviewer opens the task
13:36  Reviewer approves
13:37  Host prepares execution
```

The system now has to answer:

- Is the original decision still valid?
- Which context facts changed?
- Which policy version was reviewed?
- What exact intent did the reviewer approve?
- Was the reviewer eligible for this resource and operation?
- Did the requester modify the request after review began?
- Does approval satisfy policy, or does it exercise an explicit override?
- What happens if the review expires?
- What happens if the reviewer becomes unavailable?
- Does approval itself create execution authority?
- Which evidence must remain afterward?

These are not merely UI questions.

They are lifecycle, authority, and provenance questions.

---

## Do Not Collapse Every Human Action into "Approval"

A human can participate in several different ways.

Keep the vocabulary explicit.

| Concept | Meaning | Does it itself authorize execution? |
| --- | --- | --- |
| Acknowledgment | An identified actor accepts or confirms a defined condition | No |
| Approval | An eligible reviewer gives a positive disposition for a defined review request | Not necessarily |
| Authorization | A rule determines whether an actor may perform or request an operation | No direct side effect |
| Governance decision | Policy determines the system's current governed outcome | No |
| Override | A specifically delegated authority changes or supersedes a policy result within defined limits | Not by itself |
| Execution authority | Narrow authority accepted at the protected execution boundary | Only when the host validates and uses it |
| Execution | The protected side effect actually occurs | Yes; this is the side effect |

The distinction can be summarized as:

```text
Acknowledgment
      ≠
Approval
      ≠
Authorization
      ≠
Governance decision
      ≠
Execution authority
      ≠
Execution
```

This prevents a generic human action from silently becoming all of those things at once.

---

## Acknowledgment and Approval Are Different Boundaries

[Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) models a person or system responding to a defined challenge.

An acknowledgment means something like:

> I received and accept this stated condition.

An approval means something closer to:

> Within my review role and scope, I give a positive disposition for this exact proposed operation.

Those are different claims.

A reviewer might acknowledge that a request is high risk and still reject it.

A requester might acknowledge a warning but have no authority to approve the operation.

A system should therefore avoid:

```text
Acknowledged = true
      ↓
Approved = true
      ↓
Execute
```

unless the domain has intentionally defined those states as equivalent.

For most governed workflows, they should remain separate.

---

## Represent Human Review as an Explicit State

A review requirement should survive independently of the original HTTP request, UI session, or process lifetime.

A small state model could be:

```csharp
public enum HumanReviewStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
    Withdrawn,
    Expired,
    Superseded
}
```

The important state is `Pending`.

`Pending` means:

```text
Review has been requested
      +
No valid terminal human disposition exists
      ↓
Protected execution remains blocked
```

The core invariant is:

```text
Human review required
        ↓
No valid human disposition
        ↓
Executor invocation count = 0
```

This should remain true if:

- The review record exists for minutes or days.
- A worker restarts.
- A UI closes.
- The reviewer never responds.
- A different reviewer opens the task.
- A retry occurs.
- The requester polls repeatedly.

Pending is a governance state, not a temporary UI condition.

---

## Model the Review Request Explicitly

A durable review record should identify what is awaiting human judgment.

For example:

```csharp
public sealed record HumanReviewRequest(
    string ReviewId,
    string RequesterId,
    string OperationName,
    string ResourceId,
    string IntentFingerprint,
    string CorrelationId,
    string TriggerReasonCode,
    string PolicyVersion,
    string? PolicyHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    HumanReviewStatus Status);
```

Useful fields answer:

```text
Which review?
Who requested the operation?
Which operation?
Which resource?
Which exact intent?
Why was review required?
Which policy produced the requirement?
When was the review created?
When does it expire?
What is its current lifecycle state?
```

The review record does not need to contain every request field.

It should preserve enough identity to bind later human activity to the governed proposal.

---

## Bind Review to the Exact Intent

Human approval should not normally mean:

```text
Reviewer approved "exports."
```

That is too broad.

Prefer:

```text
Reviewer approved:
Operation = data.export
Resource = customer-ledger-2026-08
Destination = approved-partner-a
RecordCount = 25000
Classification = Restricted
Purpose = quarterly-reconciliation
```

A useful approach is to derive an intent fingerprint from canonicalized fields that materially define the proposal.

Conceptually:

```text
Canonical intent
      ↓
Fingerprint
      ↓
Human review request
```

For example:

```csharp
public sealed record ExportIntent(
    string ResourceId,
    string Destination,
    int RecordCount,
    string Purpose);

public sealed record BoundReviewIntent(
    ExportIntent Intent,
    string IntentFingerprint);
```

The exact hashing and canonicalization scheme is implementation-specific.

The architectural requirement is:

> A later approval should be demonstrably bound to the proposal the reviewer actually saw.

---

## Request Modification During Review

Suppose the requester changes:

```text
Destination:
approved-partner-a
      ↓
unknown-external-target
```

while the review remains open.

The system should not silently attach the old approval to the new intent.

Useful options include:

### Supersede the review

```text
Original review = Superseded
      ↓
New intent
      ↓
New policy evaluation
      ↓
New review if required
```

### Reject the modification while pending

```text
Pending review
      ↓
Intent is immutable until review terminates
```

### Allow non-material edits only

For example, a display label might change without invalidating the review, while destination, amount, resource, or operation cannot.

Whatever rule is chosen should be explicit and tested.

---

## Model the Human Disposition

A human action should be represented as data before it affects execution.

For example:

```csharp
public enum HumanDispositionKind
{
    Approve,
    Reject,
    Abstain
}

public sealed record HumanReviewDisposition(
    string DispositionId,
    string ReviewId,
    string ReviewerId,
    HumanDispositionKind Kind,
    string ReasonCode,
    string? Rationale,
    string IntentFingerprint,
    DateTimeOffset OccurredAt);
```

This record answers:

- Which review did the person act on?
- Which reviewer acted?
- What was the disposition?
- Which reason code describes it?
- Which exact intent fingerprint was reviewed?
- When did it happen?
- Was rationale captured?

It does not execute the protected operation.

---

## Reviewer Identity Must Be Authoritative

Do not accept reviewer identity from an untrusted form field:

```json
{
  "reviewerId": "security-director"
}
```

The host should resolve reviewer identity through the same trust model used for other consequential operations.

For example:

```text
Authenticated principal
      ↓
Host-resolved identity
      ↓
Review eligibility evaluation
```

Reviewer identity may then participate in:

- Eligibility.
- Separation-of-duty checks.
- Delegation.
- Audit residue.
- Quorum.
- Conflict-of-interest rules.
- Policy revalidation.

---

## Reviewer Role and Scope Matter

A reviewer should not automatically be eligible for every pending request.

Eligibility can depend on:

```text
Reviewer role
Operation
Resource
Tenant
Region
Risk band
Classification
Business unit
Delegated authority
Current reviewer status
```

A conceptual eligibility rule might be:

```csharp
public sealed record ReviewerContext(
    string ReviewerId,
    IReadOnlySet<string> Roles,
    string TenantId,
    string Region);

public sealed class ReviewEligibilityPolicy
{
    public bool CanReview(
        ReviewerContext reviewer,
        HumanReviewRequest request)
    {
        return reviewer.Roles.Contains("DataExportReviewer")
            && string.Equals(
                reviewer.TenantId,
                ResolveRequestTenant(request),
                StringComparison.Ordinal);
    }

    private static string ResolveRequestTenant(
        HumanReviewRequest request)
    {
        // Teaching placeholder. The host should use authoritative
        // resource or workflow context.
        return "tenant-a";
    }
}
```

The exact implementation is domain-specific.

The lesson is that eligibility is a policy decision, not a property of the UI button.

---

## Separate Requester and Approver Where Required

Some operations require separation of duties.

A simple rule may be:

```text
RequesterId != ReviewerId
```

For example:

```csharp
if (string.Equals(
        request.RequesterId,
        reviewer.ReviewerId,
        StringComparison.Ordinal))
{
    return ReviewEligibility.Deny(
        "review.self-approval-prohibited");
}
```

This can help enforce:

- Four-eyes review.
- Dual control.
- Conflict-of-interest boundaries.
- High-consequence administrative separation.

Do not impose this universally.

For many applications, self-review may be unnecessary ceremony.

Use it when the domain actually requires independent judgment.

---

## Multi-Reviewer Workflows

Some requests need more than one human disposition.

A simple model can remain explicit without building a general workflow engine.

For example:

```text
Required reviewers: 2
Eligible role: SensitiveExportReviewer
Requester may not count
All approvals must bind the same intent fingerprint
Any rejection terminates the review
```

A review rule could be:

```csharp
public sealed record ReviewQuorumPolicy(
    int RequiredApprovals,
    bool RejectOnAnyRejection,
    bool RequesterMayApprove);
```

Then:

```text
Approval from reviewer A
      ↓
1 of 2

Approval from reviewer B
      ↓
2 of 2
      ↓
Human-review requirement satisfied
```

This is sufficient to teach quorum without introducing orchestration machinery unrelated to the lesson.

---

## Dual Control Is Stronger Than a Second Click

Two approvals are useful only when they represent two independently eligible dispositions.

Avoid:

```text
Same reviewer
      ↓
Clicks Approve twice
      ↓
2 approvals
```

or:

```text
Requester creates two accounts
      ↓
Self-approves twice
```

A meaningful dual-control policy should define:

- Distinct reviewer identities.
- Independent eligibility.
- Same bound intent.
- Required roles or scopes.
- How rejection behaves.
- Whether delegated reviewers count.
- What happens if policy changes before quorum is reached.

---

## Policy-Compliant Approval Versus Override

A critical distinction is whether the reviewer is satisfying a policy requirement or changing the policy result.

### Policy-compliant approval

Example:

```text
Policy outcome:
HumanReviewRequired

Rule:
One eligible reviewer must approve

Reviewer approves
      ↓
Requirement satisfied
      ↓
Re-evaluate current policy
```

The person is participating in the normal policy path.

### Override

Example:

```text
Base policy:
Denied

Separate delegated authority:
Named emergency officer may override this denial
under explicitly defined conditions
```

That is a different capability.

Do not model both as:

```text
Approved = true
```

An override should have its own:

- Eligibility rules.
- Scope.
- Reason codes.
- Preconditions.
- Expiration.
- Provenance.
- Review expectations.
- Non-overridable boundaries.

---

## Some Denials Should Not Be Overridable

A system may define rules such as:

```text
Cross-tenant access prohibited
Mandatory legal hold active
Cryptographic verification failed
Required safety interlock unavailable
Requested operation outside system capability
```

and decide that no human reviewer may override them.

That is a legitimate policy posture.

The architecture should be able to say:

```text
Denied
      ↓
OverrideAllowed = false
```

rather than presenting every denial as:

```text
Ask a human
```

Human review is not a universal fallback for policy failure.

---

## Delegated Review

Reviewer authority may be delegated.

For example:

```text
Primary reviewer unavailable
      ↓
Delegation record
      ↓
Named delegate
      ↓
Narrow operation/resource/time scope
```

A useful delegation record might include:

```csharp
public sealed record ReviewDelegation(
    string DelegationId,
    string DelegatorId,
    string DelegateId,
    string OperationName,
    string? ResourceScope,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil,
    string ReasonCode);
```

Delegation should not mean:

```text
Delegate may approve anything the delegator could ever approve.
```

Prefer the smallest useful scope.

---

## Reviewer Unavailability

Human workflows need an explicit posture for unavailable reviewers.

Possible responses include:

```text
Wait
Route to another eligible reviewer
Use an approved delegation
Escalate to a different review tier
Expire the request
Cancel the operation
```

Avoid silently converting:

```text
No reviewer available
      ↓
Allowed
```

unless a documented policy explicitly permits that behavior.

Reviewer unavailability is a workflow condition, not evidence that the operation is safe.

---

## Review Timeout

A pending review should not necessarily live forever.

A review can carry:

```text
CreatedAt
ExpiresAt
```

When the deadline passes:

```text
Pending
      ↓
Expired
      ↓
Executor invocation count = 0
```

After expiration, the host can decide whether the requester may:

- Submit a new request.
- Re-evaluate automatically and create a new review.
- Contact a different review path.
- Abandon the operation.

The expired approval path should not be resurrected by a late click.

---

## Cancellation and Withdrawal

Not every pending review ends in approval or rejection.

### Cancellation

A host or administrator may cancel because:

- The operation is no longer available.
- The resource was deleted.
- A security incident invalidated the workflow.
- A superseding administrative decision exists.

### Withdrawal

The requester may withdraw because:

- The operation is no longer needed.
- The request contained an error.
- A different approach will be used.

Both states should block future execution from the old review.

```text
Pending
      ↓
Cancelled or Withdrawn
      ↓
Later Approve click
      ↓
Rejected as stale
```

---

## Reject and Resubmit

A rejection should not normally mutate into approval through repeated editing of the same historical review.

A clearer lifecycle is:

```text
Review A
      ↓
Rejected
      ↓
Requester changes intent
      ↓
New policy evaluation
      ↓
Review B if required
```

This preserves the fact that Review A ended in rejection.

Review B represents a new proposal.

That improves audit clarity and prevents historical dispositions from being rewritten.

---

## Policy Drift During the Review Window

Consider:

```text
09:00  Policy v12 requires one reviewer
11:00  Policy v13 requires two reviewers
13:30  Reviewer approves
```

Which policy governs continuation?

There is no universal answer.

Possible architectures include:

### Latest-policy revalidation

```text
Human disposition
      ↓
Rebuild current context
      ↓
Evaluate policy v13
      ↓
Current requirements win
```

### Explicit grandfathering

A narrow class of already-pending reviews may continue under the original policy version.

If this is allowed, it should be deliberate and recorded.

### Conditional migration

For example:

```text
Non-breaking policy change
      ↓
Existing review remains valid

Authority-reducing policy change
      ↓
Existing review invalidated
```

Whatever strategy is chosen should be reviewable and testable.

The dangerous design is allowing the old decision to execute simply because a human eventually clicked `Approve`.

---

## Context Drift During the Review Window

Policy may stay the same while context changes.

Examples:

- The requester loses a role.
- The resource classification changes.
- The destination changes.
- The transaction value increases.
- The risk band changes.
- The tenant or region changes.
- A maintenance hold begins.
- The resource version changes.
- The operation becomes irreversible.
- The target account is already modified.

A human disposition should not erase those changes.

A safer flow is:

```text
Human approval
      ↓
Validate review + disposition
      ↓
Rebuild authoritative context
      ↓
Compare bound intent
      ↓
Re-evaluate current policy
      ↓
Current governance decision
```

---

## Revalidation Is a First-Class Step

The issue becomes especially visible when a workflow is delayed.

Avoid:

```csharp
if (review.Status == HumanReviewStatus.Approved)
{
    await executor.ExecuteAsync(cancellationToken);
}
```

Prefer:

```csharp
if (review.Status != HumanReviewStatus.Approved)
{
    return Blocked("review.not-approved");
}

CurrentPolicyContext currentContext =
    await contextFactory.BuildAsync(
        review,
        cancellationToken);

if (!string.Equals(
        currentContext.IntentFingerprint,
        review.IntentFingerprint,
        StringComparison.Ordinal))
{
    return Blocked("review.intent-changed");
}

GovernanceDecision currentDecision =
    await policyEvaluator.EvaluateAsync(
        currentContext,
        cancellationToken);

if (!currentDecision.CanProceed)
{
    return Blocked("review.revalidation-blocked");
}
```

The exact code is illustrative.

The important invariant is:

> Approval makes revalidation eligible to continue; it does not make revalidation unnecessary.

---

## Revalidation Can Require Human Review Again

Suppose the original context required one reviewer.

After approval, revalidation discovers:

```text
Risk changed from Moderate to High
```

and current policy says:

```text
High risk
      ↓
Two-reviewer quorum
```

The correct result may be:

```text
Current decision:
Human review still required
```

not:

```text
Prior approval exists
      ↓
Allowed
```

A human workflow can therefore revisit the pending state.

That is not a failure.

It is the system applying current governance to current facts.

---

## Approval Should Not Directly Invoke Protected Execution

A review handler should avoid:

```csharp
public async Task ApproveAsync(
    string reviewId,
    CancellationToken cancellationToken)
{
    await reviewStore.MarkApprovedAsync(reviewId);
    await protectedExecutor.ExecuteAsync(reviewId);
}
```

This couples:

```text
Human disposition
+
Execution authority
+
Protected side effect
```

Prefer:

```text
Reviewer disposition
      ↓
Persist terminal review state
      ↓
Revalidation
      ↓
Governance decision
      ↓
Capability or other narrow authority
      ↓
Host-owned execution
```

The approval handler can complete the human boundary.

A separate host-controlled continuation path decides whether execution is currently permitted.

---

## Least-Privilege Authority After Approval

A valid approval should not create a standing permission such as:

```text
Reviewer approved one export
      ↓
Requester may export any data for 24 hours
```

Prefer narrow authority bound to:

```text
Subject
Operation
Resource
Audience
Intent fingerprint
Decision or review identity
Expiration
Use count where needed
```

That maps directly to [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md).

The flow is:

```text
Human approval
      ↓
Required revalidation
      ↓
Scoped authority
      ↓
Host-owned execution
```

not:

```text
Human clicked Approve
      ↓
Anything may execute
```

---

## Pending Review Is Not a Capability

Do not reuse the review token or review identifier as execution authority.

For example:

```text
reviewId = rvw-123
```

should identify the governance workflow.

It should not mean:

```text
Present rvw-123 to executor and execute.
```

Review identity and execution authority serve different purposes.

Keeping them separate helps prevent:

- Replay.
- Scope confusion.
- Accidental broad authority.
- Execution before review is complete.
- Execution after review expires.
- Execution after the underlying intent changes.

---

## Capture Reviewer Rationale Carefully

A reviewer may supply:

```text
ReasonCode:
review.approved.business-need-verified

Rationale:
Quarterly reconciliation request matches the approved partner contract.
```

A stable reason code is useful for:

- Reporting.
- Tests.
- Policy analysis.
- Decision provenance.
- Review analytics.

Human rationale can add context for later readers.

Do not make software parse free-form prose to determine authority.

Also avoid placing secrets, unnecessary personal data, or sensitive request payloads into rationale fields.

---

## Decision Provenance Across the Human Boundary

A durable workflow should make the decision path reconstructable.

Useful evidence can include:

```text
ReviewId
CorrelationId
RequesterId
ReviewerId
IntentFingerprint
OriginalPolicyVersion
OriginalPolicyHash
ReviewTriggerReasonCode
HumanDisposition
HumanReasonCode
DispositionOccurredAt
RevalidationPolicyVersion
RevalidationPolicyHash
FinalGovernanceOutcome
CapabilityId where applicable
ExecutionOutcome
```

The exact storage shape is implementation-specific.

The conceptual history is more important:

```text
Original decision
      ↓
Review created
      ↓
Human disposition
      ↓
Revalidation
      ↓
Authority issuance
      ↓
Execution
```

See [Policy Versioning and Decision Provenance](policy-versioning-and-decision-provenance.md) for policy identity, drift, freshness, and historical evidence.

---

## Audit Residue Should Preserve Lifecycle Events

Human review creates more than one meaningful event.

For example:

```text
review.created
review.assigned
review.delegated
review.approved
review.rejected
review.expired
review.cancelled
review.withdrawn
review.superseded
decision.revalidated
capability.issued
execution.completed
```

A single line such as:

```text
Request approved by Alice.
```

does not explain:

- What was approved.
- Which policy required review.
- Whether the request later changed.
- Whether revalidation occurred.
- Whether execution actually happened.
- Whether approval was policy-compliant or an override.

Keep governance residue distinct from ordinary operational logging, following [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md).

---

## A Minimal Workflow Coordinator

A teaching coordinator can make the boundary visible without becoming a full workflow engine.

```csharp
public sealed class HumanReviewCoordinator
{
    private readonly IHumanReviewStore reviewStore;
    private readonly IPolicyContextFactory contextFactory;
    private readonly IPolicyEvaluator policyEvaluator;
    private readonly ICapabilityIssuer capabilityIssuer;

    public HumanReviewCoordinator(
        IHumanReviewStore reviewStore,
        IPolicyContextFactory contextFactory,
        IPolicyEvaluator policyEvaluator,
        ICapabilityIssuer capabilityIssuer)
    {
        this.reviewStore = reviewStore;
        this.contextFactory = contextFactory;
        this.policyEvaluator = policyEvaluator;
        this.capabilityIssuer = capabilityIssuer;
    }

    public async Task<ContinuationResult> ContinueAsync(
        string reviewId,
        CancellationToken cancellationToken)
    {
        HumanReview review =
            await reviewStore.GetAsync(
                reviewId,
                cancellationToken);

        if (review.Status != HumanReviewStatus.Approved)
        {
            return ContinuationResult.Blocked(
                "review.valid-approval-required");
        }

        CurrentPolicyContext context =
            await contextFactory.BuildAsync(
                review,
                cancellationToken);

        if (!string.Equals(
                context.IntentFingerprint,
                review.IntentFingerprint,
                StringComparison.Ordinal))
        {
            return ContinuationResult.Blocked(
                "review.intent-changed");
        }

        GovernanceDecision decision =
            await policyEvaluator.EvaluateAsync(
                context,
                cancellationToken);

        if (!decision.CanProceed)
        {
            return ContinuationResult.Blocked(
                "review.revalidation-blocked");
        }

        ScopedCapability capability =
            await capabilityIssuer.IssueAsync(
                context,
                decision,
                review,
                cancellationToken);

        return ContinuationResult.Ready(
            decision,
            capability);
    }
}
```

This example intentionally stops before the protected operation.

A host-controlled executor still needs to:

- Validate the capability.
- Verify audience and scope.
- Verify expiration.
- Verify the operation and resource binding.
- Perform any final freshness checks.
- Execute the real side effect.

The coordinator prepares a governed continuation.

It does not own the final effect.

---

## Human Review and Escalation

`EscalationRecommended` and `HumanReviewRequired` are related but not identical ideas.

Escalation means:

> Route the decision to a different authority or decision path.

Human review means:

> A person must provide a valid disposition within a defined review lifecycle.

An escalation path may lead to:

```text
Specialized automated policy
Higher-authority service
Human reviewer
Multi-reviewer board
Emergency authority
```

A human review may be the target of escalation, but it should still use explicit:

- Reviewer eligibility.
- Bound intent.
- Lifecycle state.
- Expiration.
- Revalidation.
- Provenance.

Later escalation material can build on this lifecycle rather than treating escalation as an unrestricted approval button.

---

## Human Review and Risk-Based Decisions

[Risk-Based Decisions in Governed Systems](risk-based-decisions-in-governed-systems.md) may map a risk band into a human-oriented outcome.

For example:

```text
Low       → Allowed
Moderate  → AcknowledgmentRequired
High      → HumanReviewRequired
Critical  → Denied
```

The mapping is policy.

Human review does not make the risk evidence disappear.

A reviewer should see the evidence and reason codes relevant to the review, while the host still revalidates current risk-relevant context before execution.

If the risk posture changes during the review window, current policy may change the required disposition or block the operation entirely.

---

## Human Review in AI-Assisted Execution

AI-proposed operations make this boundary especially important.

Avoid:

```text
Model proposes tool call
      ↓
Human clicks Approve
      ↓
Model or tool executes with broad credentials
```

Prefer:

```text
AI proposal
      ↓
Host validates proposal schema
      ↓
Host builds authoritative context
      ↓
Governance decision
      ↓
Human review when required
      ↓
Host validates human disposition
      ↓
Host rebuilds context
      ↓
Policy revalidation
      ↓
Scoped capability
      ↓
Host-owned tool execution
```

The model does not become authoritative because a person approved its proposal.

The human does not become the execution engine.

The host retains the final enforcement boundary.

See [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) for the broader AI proposal and execution-control architecture.

---

## Failure and Race Conditions

Human workflows create concurrency that synchronous examples often hide.

### Approval races with cancellation

```text
Reviewer approves at 13:36:01
Requester withdraws at 13:36:02
Worker attempts continuation at 13:36:03
```

The continuation path should observe the current terminal state and reject stale authority.

### Two reviewers act at the same time

If only one disposition is needed, the review store should define which terminal transition wins.

If quorum is required, each disposition should remain independently identifiable.

### Review expires while the UI is open

The server should enforce expiration.

The browser's stale `Approve` button is not authority.

### Policy changes after approval but before execution

The architecture must define whether execution freshness requires another policy check.

For consequential delayed operations, revalidation close to execution is often the safer model.

### Capability expires before execution

A valid human approval does not revive an expired execution capability.

Issue a new capability only through the governed continuation rules.

---

## State Transitions Should Be Explicit

A simple transition table can prevent accidental workflow states.

| Current state | Event | Next state | May execute? |
| --- | --- | --- | --- |
| Pending | Eligible reviewer approves | Approved | No; revalidation still required |
| Pending | Eligible reviewer rejects | Rejected | No |
| Pending | Requester withdraws | Withdrawn | No |
| Pending | Host cancels | Cancelled | No |
| Pending | Deadline passes | Expired | No |
| Pending | Material intent changes | Superseded | No |
| Approved | Revalidation succeeds | Approved / continuation-ready | Only through scoped host-owned authority |
| Approved | Revalidation blocks | Approved but blocked, or superseded according to workflow policy | No |
| Rejected | Late approval arrives | Rejected | No |
| Expired | Late approval arrives | Expired | No |

The exact state machine may differ.

What matters is that terminal states cannot casually transition back into executable authority.

---

## Decision-Table Tests

Human-review policy is a good candidate for focused decision tables.

### Single-reviewer baseline

| Review state | Reviewer eligible | Intent matches | Review fresh | Revalidation | Expected result |
| --- | --- | --- | --- | --- | --- |
| Pending | Yes | Yes | Yes | Allowed | Blocked: no disposition |
| Approved | No | Yes | Yes | Allowed | Blocked: invalid reviewer |
| Approved | Yes | No | Yes | Allowed | Blocked: intent changed |
| Approved | Yes | Yes | No | Allowed | Blocked: review expired |
| Approved | Yes | Yes | Yes | Denied | Blocked: current policy denial |
| Approved | Yes | Yes | Yes | Human review still required | Remain governed; no execution |
| Approved | Yes | Yes | Yes | Allowed | Eligible for scoped continuation |

### Separation of duties

| Requester | Reviewer | Self-approval allowed | Expected |
| --- | --- | --- | --- |
| actor-a | actor-a | No | Reject disposition |
| actor-a | actor-b | No | Eligible if other scope rules pass |
| actor-a | actor-a | Yes | Eligible if other scope rules pass |

### Quorum

| Required approvals | Valid approvals | Rejections | Expected |
| ---: | ---: | ---: | --- |
| 2 | 0 | 0 | Pending |
| 2 | 1 | 0 | Pending |
| 2 | 2 | 0 | Human-review requirement satisfied |
| 2 | 1 | 1 | Rejected when policy is reject-on-any-rejection |

These tests turn review semantics into executable policy rather than undocumented UI behavior.

---

## Core Architectural Invariant Tests

At minimum, test:

### Pending Never Executes

```text
Review status = Pending
      ↓
Executor invocation count = 0
```

### Rejected Never Executes

```text
Review status = Rejected
      ↓
Executor invocation count = 0
```

### Expired Approval Does Not Execute

```text
Approval exists
+
Review expired
      ↓
Executor invocation count = 0
```

### Intent Drift Invalidates Continuation

```text
Reviewed fingerprint != current fingerprint
      ↓
Executor invocation count = 0
```

### Policy Drift Can Block a Prior Approval

```text
Original review approved
+
Current policy = Denied
      ↓
Executor invocation count = 0
```

### Approval Does Not Skip Revalidation

```text
Human approval
      ↓
Revalidation invocation count = 1
```

### Scoped Authority Is Required When the Architecture Uses Capabilities

```text
Approved + revalidated
+
No valid capability
      ↓
Executor invocation count = 0
```

### Valid Continuation Executes Only Once Where Required

```text
Valid approval
+
Current policy allows
+
Valid scoped capability
+
Single-use boundary
      ↓
Executor invocation count = 1
```

---

## Common Failure Modes

### 1. Approval Is a Boolean on the Original Request

```json
{
  "approved": true
}
```

There is no durable reviewer identity, review lifecycle, intent binding, or provenance.

### 2. Approval Directly Calls the Executor

The review controller becomes the execution boundary.

### 3. Any Authenticated User Can Review

Authentication is mistaken for reviewer eligibility.

### 4. Reviewer Scope Is Unlimited

A reviewer for one tenant or operation can approve unrelated resources.

### 5. Self-Approval Accidentally Defeats Separation of Duties

The requester satisfies a review requirement intended to provide independent judgment.

### 6. Old Approval Survives Material Intent Changes

The destination, amount, resource, or operation changes after review.

### 7. Old Approval Survives Policy Drift

A prior review decision silently outranks a newer policy.

### 8. Rejection Is Overwritten

The same review record is edited from `Rejected` to `Approved`, destroying history.

### 9. Review Never Expires

A years-old approval can still create continuation authority.

### 10. Delegation Is Broad and Permanent

Temporary review coverage becomes standing administrative authority.

### 11. Multi-Reviewer Logic Counts Duplicate Identities

Two clicks are mistaken for two independent approvals.

### 12. Human Override Is Implicit

A generic approval silently defeats mandatory denials.

### 13. Review Identifier Becomes Execution Token

Knowledge of a workflow ID becomes sufficient to invoke a protected operation.

### 14. AI Proposal Becomes Trusted After a Click

Human review is used as a substitute for host-side schema validation, policy, capability, and execution controls.

---

## When a Simpler Pattern Is Better

Do not add a durable human-review workflow merely because an operation feels important.

A conventional confirmation may be enough when:

- The same actor is simply confirming their own immediate action.
- The action remains in one request/session.
- No independent reviewer is required.
- Policy and context cannot materially drift during the interaction.
- No durable reviewer provenance is needed.

Ordinary authorization may be enough when:

- The core question is whether an actor may perform the operation.
- A role, claim, or resource-based policy fully expresses the rule.
- No delayed governance state exists.
- Execution follows immediately after the authorization decision.

A human-review workflow becomes more useful when:

- The operation must wait for another person.
- Review survives beyond the original request.
- Policy or context may drift while waiting.
- Reviewer eligibility or separation of duties matters.
- Multi-reviewer or delegated review exists.
- The exact reviewed intent must be preserved.
- Review evidence must be reconstructable.
- Approval and execution are deliberately separate stages.

Use the smallest architecture that preserves the real trust and lifecycle boundaries.

---

## Working Implementation References

This tutorial is framework-neutral.

The `AsiBackbone/AsiBackbone` repository contains implementation material that supports adjacent boundaries used by this workflow, even though the teaching example here keeps human-review orchestration explicit and application-owned.

| Learning concept | Working implementation reference | What to inspect |
| --- | --- | --- |
| Decision policy boundary | [Custom Decision Policy Examples](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/custom-decision-policy-examples.md) | How host-owned decision policy can require acknowledgment or escalation without performing the protected action. |
| High-consequence administrative flow | [High-Risk Administrative Action Scenario](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/scenarios/high-risk-administrative-action.md) | Host ownership of identity, authorization, UI, persistence, decision handling, acknowledgment, audit, and execution. |
| Audit lifecycle evidence | [Audit Residue Observability Schema](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/audit-residue-observability-schema.md) | Structured decision and execution evidence suitable for correlation across lifecycle stages. |
| Host enforcement | [Host-Owned Execution Enforcement](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/host-owned-execution-enforcement.md) | Why decisions and policy results do not themselves perform the protected operation. |
| Governance decisions | [`GovernanceDecision`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecision.cs) | The structured outcome consumed by a host-controlled workflow. |
| Audit lifecycle vocabulary | [`AuditResidueLifecycleStage`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Audit/AuditResidueLifecycleStage.cs) | Lifecycle-oriented audit stages that can participate in broader host-owned workflow evidence. |

The implementation references do not require a particular human-review UI, queue, workflow engine, or persistence product.

The architectural mapping is:

```text
Host-owned review orchestration
      ↓
Current policy evaluation
      ↓
GovernanceDecision
      ↓
Scoped continuation authority
      ↓
Host-owned execution
      ↓
Audit residue
```

---

## Review Questions

Before implementing a human-in-the-loop workflow, you should be able to answer:

1. What exact state means that the workflow is waiting for a person?
2. What prevents execution while that state is pending?
3. Who is eligible to review the operation?
4. How is reviewer identity established?
5. Must requester and reviewer be different?
6. What exact intent is the human reviewing?
7. How is the disposition bound to that intent?
8. What happens if the request changes during review?
9. How long is the review valid?
10. What happens when no reviewer responds?
11. Can review be delegated, and how narrowly?
12. Does the workflow require one reviewer, quorum, or dual control?
13. Is the human satisfying normal policy or exercising an override?
14. Which denials cannot be overridden?
15. What policy version produced the review requirement?
16. What happens if policy changes while the review is pending?
17. Which context facts must be refreshed before continuation?
18. Can revalidation require human review again?
19. Does approval issue broad standing permission?
20. What scoped execution authority is required after approval?
21. Can the review identifier be replayed as execution authority?
22. What rationale and reason codes are preserved?
23. How are cancellation, withdrawal, rejection, expiration, and supersession represented?
24. Can a historical reviewer disposition be reconstructed later?
25. Does host-owned execution remain outside the review handler?

If several answers are unclear, the system may have an approval screen, but it does not yet have a well-defined human-in-the-loop governance architecture.

---

## Related Content

- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) — distinguish acknowledgment from approval and preserve decision, acknowledgment, re-evaluation, and execution evidence.
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) — build the authoritative facts and structured outcomes that can lead into a human-review state.
- [Risk-Based Decisions in Governed Systems](risk-based-decisions-in-governed-systems.md) — see how changing consequence, likelihood, uncertainty, and environmental context can change whether human review is required.
- [Policy Versioning and Decision Provenance](policy-versioning-and-decision-provenance.md) — reason about policy drift, decision freshness, and historical evidence across a delayed review window.
- [Practical Policy Testing and Decision-Table Strategies](practical-policy-testing-and-decision-table-strategies.md) — convert reviewer eligibility, quorum, expiration, drift, and continuation rules into explicit test cases.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — preserve least-privilege execution authority after a valid review and revalidation.
- [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) — apply the same human boundary to AI-proposed actions while the host retains context and execution authority.
- [Constraint Composition and Policy Precedence](constraint-composition-and-policy-precedence.md) — ensure a human-review policy does not silently weaken deterministic denials or other composed constraints.
- [Regional and Tenant Policy Overlays](../advanced/regional-and-tenant-policy-overlays.md) — consider reviewer scope and override authority when multiple policy authorities participate.
- [Threat Modeling as Architecture Reasoning](../security/threat-modeling-as-architecture-reasoning.md) — analyze self-approval, stale approval, replay, reviewer impersonation, broad delegation, and execution-bypass threats.

---

> **A human can contribute judgment to governance without becoming a bypass around governance.**
