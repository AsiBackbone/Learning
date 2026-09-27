---
description: Authorization, approval, acknowledgment, and execution permission answer different questions. Why none should silently authorize a delayed .NET operation.
title: "Authorization vs. Approval vs. Acknowledgment: Which Decision Do You Actually Have?"
author: Christopher D. Cavell
published: "2026-09-27"
summary: Authorization, approval, and acknowledgment answer different questions, bind to different things, and expire on different clocks. This guide uses one sensitive-data export to show what each decision proves, what it does not, and why none of them should silently become permission to execute later.
feed: true
x_hashtags:
  - DotNet
  - SoftwareArchitecture
---

# Authorization vs. Approval vs. Acknowledgment: Which Decision Do You Actually Have?

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** Familiarity with C# and ASP.NET Core authorization is helpful. No AsiBackbone package, workflow engine, or prior Learning material is required. The code uses `TimeProvider`, available since .NET 8.

**What this article covers:** what authorization, approval, acknowledgment, and execution permission each mean in plain terms; who produces each one, what it binds to, how long it lasts, and what evidence it leaves; what each decision does *not* prove; when ordinary ASP.NET Core authorization followed by immediate execution is enough; how a delayed operation should re-check permission at the point of execution; and the failure modes that appear when one decision quietly stands in for another.

A background worker picks up a queued job and runs this:

```csharp
var job = await _jobs.GetAsync(jobId, ct);

if (job.Status == JobStatus.Approved && job.Acknowledged)
{
    await _exporter.ExportCustomerDataAsync(job.TenantId, job.Fields, job.Destination, ct);
}
```

Every word in that condition sounds like permission. `Approved` sounds like someone with authority said yes. `Acknowledged` sounds like the risks were accepted. The requester was certainly authorized when they created the job, or the API would have rejected it.

Now ask four ordinary questions about that export:

- The requester was authorized on Monday. Are they still allowed to export this tenant's data on Thursday, when the worker runs?
- The reviewer approved an export of three fields. The job now lists five. Did anyone approve five?
- Someone ticked a box saying they understood the file contains personal data. Who ticked it, what text did they see, and was that box ever meant to grant anything?
- The tenant was placed on a legal hold on Wednesday. Does any of the stored state know that?

The code cannot answer any of them, because it collapsed four different decisions into two flags.

> Authorization, approval, and acknowledgment answer different questions. None should silently inherit the authority or evidence semantics of another.

## Four Questions, Four Decisions

Teams use *authorized*, *approved*, *confirmed*, and *acknowledged* almost interchangeably. In a system that executes consequential operations, they should mean distinct things:

| Decision | The question it answers |
| --- | --- |
| **Authorization** | May this identified actor request or perform this kind of operation on this resource, under current access policy? |
| **Approval** | Did an eligible reviewer, other than the requester where required, accept this specific proposal at this specific revision? |
| **Acknowledgment** | Did a specific person demonstrate awareness of a specific warning, consequence, or condition? |
| **Execution permission** | Is this exact operation still allowed at the moment the protected side effect is about to happen? |

*Confirmed* is deliberately missing. In practice it is used for all four, and also for "the user clicked OK so they didn't fat-finger the button." When you see it in a design, ask which of the four it means. If the answer is "it prevents accidental clicks," it is a user-interface safeguard, which is useful and legitimate, and it should not be recorded or consumed as any of the four decisions above.

These are working definitions for this article. Standards, products, and teams use the words differently, and some regulated domains define them precisely. What matters is that *your* system gives each one a single meaning and never lets one stand in for another by accident.

## The Example: A Customer Data Export

A support analyst at a software company needs a full export of one tenant's customer records to help with a data migration. The export includes names, email addresses, and phone numbers. Company policy says:

- Only analysts assigned to the tenant may request an export of its data.
- Any export containing personal data must be approved by a data steward for that tenant, who may not be the requester.
- Before an export containing personal data is submitted, the requester must acknowledge that the file contains personal data and must be deleted within seven days.
- Exports run overnight in a background worker, because large tenants take hours.
- Nothing may be exported from a tenant on legal hold.

That one scenario contains all four decisions:

```text
Monday 10:02   Analyst (authenticated) requests export, revision 1: name, email
               → authorization: analyst is assigned to tenant       ✔
Monday 10:03   Analyst edits proposal, revision 2: name, email, phone
Monday 10:04   Analyst acknowledges personal-data handling notice v3 for revision 2
Tuesday 09:15  Steward approves revision 2, valid for 72 hours
Wednesday      Tenant placed on legal hold
Thursday 02:00 Worker picks up the job
               → execution permission: ?
```

The question at 02:00 on Thursday is not "was this authorized, approved, and acknowledged?" All three happened. The question is whether the export is **still** permitted, for **this** revision, under the rules and facts that are true **now**. The answer is no, and nothing in the stored flags would say so.

## What Each Decision Binds To and How Long It Lasts

This table is the core of the article. Each row is a property you should be able to state for every decision your system records.

| | Authorization | Approval | Acknowledgment | Execution permission |
| --- | --- | --- | --- | --- |
| **Question** | May this actor do this kind of thing to this resource? | Did an eligible reviewer accept this exact proposal? | Did this person see and accept this exact notice? | Is this exact operation still allowed right now? |
| **Produced by** | Access policy evaluated by the host | An eligible reviewer, recorded by the host | The affected or responsible person, recorded by the host | The host, at the protected boundary |
| **Binds to** | Actor, operation, resource, current policy | Proposal ID, revision or fingerprint, reviewer, reviewer's authority | Actor, challenge ID and version, proposal fingerprint | The exact operation about to run |
| **Lifetime** | The request or session in which it was evaluated | Until expiry, revocation, rejection, supersession, or material drift | Until expiry, or until the notice or proposal changes | The execution attempt only |
| **Evidence to retain** | Decision, reason code, policy version, inputs used | Reviewer, disposition, bound fingerprint, time, expiry, rationale where required | Actor, challenge ID and version, bound fingerprint, time | Final check result, policy version, the approval and acknowledgment it relied on, outcome |
| **Safe downstream inference** | The requester could legitimately *propose* the operation at that time | The reviewer accepted *this revision* at that time | The person was shown *this notice* at that time | The side effect may run, once, now |

The rows that teams skip are **binds to** and **lifetime**. A decision without a binding can be reused for something it never considered. A decision without a lifetime becomes permanent by default.

## What Each Decision Does Not Prove

The inverse table is just as useful in design review, because most incidents come from inferences nobody wrote down.

| Decision | Does not prove |
| --- | --- |
| **Authorization** | That anyone reviewed the operation. That the actor is still authorized later. That the resource is still in the state it was in. That a worker running later has the same authority. |
| **Approval** | That the requester was authorized. That the reviewer was eligible, unless the host checked. That a different revision is acceptable. That policy, resource state, or the requester's access are unchanged. That execution should happen now. |
| **Acknowledgment** | That the person may perform the operation. That anyone approved it. That the operation is safe or lawful. That the person consented in any legal sense. That a different or updated notice was seen. |
| **Execution permission** | That the effect completed. It is a decision to proceed, not a record of the outcome, which needs its own evidence. |

Two rows deserve emphasis.

**Acknowledgment grants nothing.** It satisfies one specific requirement, such as "the requester has been told about the retention rule," and every other constraint still applies. If a checkbox can turn a denied operation into an allowed one, it has become an override, and overrides need an authorized person, not an informed one.

**Acknowledgment is not legal consent.** This article uses acknowledgment in its engineering sense: evidence that a defined notice was shown to and accepted by an identified person. Whether that is sufficient for consent, notice, or disclosure obligations in your jurisdiction or industry is a legal and compliance question, and this article does not answer it.

## The Proportional Path: When Authorization Is the Whole Story

Most operations do not need approval, acknowledgment, or anything else in this article.

Suppose the same analyst exports a small report about their own team's ticket volume. It contains no personal data, it is generated in a few seconds, and it is returned in the same HTTP response. The honest design is ordinary resource-based authorization followed by the operation:

```csharp
app.MapPost("/reports/{reportId}/export", async (
    string reportId,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    IReportStore reports,
    IReportExporter exporter,
    CancellationToken ct) =>
{
    var report = await reports.FindAsync(reportId, ct);
    if (report is null)
    {
        return Results.NotFound();
    }

    var result = await authorization.AuthorizeAsync(user, report, "Reports.Export");
    if (!result.Succeeded)
    {
        return Results.Forbid();
    }

    var file = await exporter.ExportAsync(report, ct);
    return Results.File(file.Content, file.ContentType, file.FileName);
});
```

```text
authenticated actor
    → authorization (current policy, loaded resource)
    → protected executor, same request
```

Here, authorization *is* execution permission, because nothing separates them: the same principal, the same resource instance, the same policy, the same process, and a gap of milliseconds. There is no revision to drift, no second person, and no later worker. Adding a proposal store, an approval queue, and an execution gate to this endpoint would add cost and a false impression of rigor.

[When ASP.NET Core Authorization Is Enough](../../architecture/when-aspnet-core-authorization-is-enough.md) explores this boundary in depth. The short test is: **if the decision and the side effect happen in the same request, against the same loaded state, and nobody else needs to agree, authorization is usually sufficient.**

The distinctions start to matter when at least one of these is true:

- A second person must agree, or policy requires separation of duties.
- A person must be told something specific before the operation proceeds.
- Execution is delayed, queued, retried, or runs under a different identity.
- The proposal can be edited after someone has looked at it.
- The operation is expensive or irreversible enough that "which decision allowed this?" will be asked afterward.

## The Delayed Path: Keep the Decisions Separate, Then Check Again

For the customer export, the flow has more steps, and each step produces a distinct record:

```text
authorized request
    → exact proposal (ID + revision + fingerprint)
    → eligible reviewer approval, bound to the fingerprint
    → bound acknowledgment, when policy requires it
    → current policy re-evaluation at execution time
    → narrow execution authority or a direct executor check
    → protected side effect
```

### The proposal is the thing everyone decides about

Approval and acknowledgment are only meaningful if they refer to something exact. Model the export as a proposal with a revision, and have the host compute a fingerprint over every field that matters to the decision:

```csharp
public sealed record ExportProposal(
    string ProposalId,
    int Revision,
    string TenantId,
    string RequesterId,
    IReadOnlyList<string> Fields,
    string Destination,
    string Purpose);

public static class ProposalFingerprint
{
    public static string Compute(ExportProposal proposal)
    {
        // Material fields only, in a canonical order. Changing any of them
        // produces a new fingerprint and invalidates earlier decisions.
        var canonical = string.Join('\n',
            proposal.ProposalId,
            proposal.TenantId,
            proposal.RequesterId,
            string.Join(',', proposal.Fields.Order(StringComparer.Ordinal)),
            proposal.Destination,
            proposal.Purpose);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
```

The revision number is useful for people; the fingerprint is what the host compares. A revision counter alone can be reset or reused by a bug. A fingerprint changes whenever the content changes. Deciding which fields are material is a domain decision. Here, adding a field or changing the destination is material; fixing a typo in an internal note is not, which is why the note is not in the fingerprint.

### Approval, acknowledgment, and authorization are separate records

Keep each decision as its own type, even if all three end up in the same database:

```csharp
public sealed record ExportApproval(
    string ApprovalId,
    string ProposalId,
    string ProposalFingerprint,
    string ReviewerId,
    string ReviewerScope,           // e.g. "data-steward:tenant-contoso"
    string PolicyVersion,
    DateTimeOffset ApprovedAt,
    DateTimeOffset ExpiresAt);

public sealed record ExportAcknowledgment(
    string AcknowledgmentId,
    string ProposalId,
    string ProposalFingerprint,
    string ActorId,
    string ChallengeId,             // e.g. "personal-data-export-notice"
    int ChallengeVersion,
    DateTimeOffset AcknowledgedAt,
    DateTimeOffset ExpiresAt);
```

Notice what these records do *not* contain: a `Status` field that means "go," or the requester's claims, token, or session. Approval records that a reviewer accepted a fingerprint. Acknowledgment records that a person accepted a notice for a fingerprint. Neither is an instruction to execute.

At request time, the host evaluates the requester's authorization and stores that decision with its reason code and policy version, as described in [Your Audit Log Records the Story, Not the Decision](your-audit-log-is-not-evidence.md). That record is evidence that the request was legitimate *when it was made*. It is not a ticket the worker may carry forward.

### Execution permission is decided again, at the boundary

The worker does not trust any stored flag. Immediately before the side effect, it asks the host a fresh question:

```csharp
public sealed class ExportExecutionGate(
    IProposalStore proposals,
    IApprovalStore approvals,
    IAcknowledgmentStore acknowledgments,
    IAccessPolicy access,
    IExportPolicy exportPolicy,
    TimeProvider time)
{
    public async Task<ExecutionCheck> CheckAsync(string proposalId, CancellationToken ct)
    {
        var proposal = await proposals.GetCurrentAsync(proposalId, ct);
        if (proposal is null)
        {
            return ExecutionCheck.Deny("proposal.not-found");
        }

        var fingerprint = ProposalFingerprint.Compute(proposal);
        var now = time.GetUtcNow();

        // 1. The requester must still be authorized now, not merely on Monday.
        var requesterAccess = await access.EvaluateAsync(
            proposal.RequesterId, "customer-data.export", proposal.TenantId, ct);
        if (!requesterAccess.Allowed)
        {
            return ExecutionCheck.Deny(requesterAccess.ReasonCode);
        }

        // 2. Evaluate current policy against current facts, and ask what it requires.
        var requirements = await exportPolicy.EvaluateAsync(proposal, ct);
        if (requirements.Denied)
        {
            return ExecutionCheck.Deny(requirements.ReasonCode); // e.g. "tenant.legal-hold"
        }

        // 3. If approval is required, it must be bound to this exact fingerprint,
        //    unexpired, and from a reviewer who is still eligible and is not the requester.
        ExportApproval? approval = null;
        if (requirements.ApprovalRequired)
        {
            approval = await approvals.FindAsync(proposalId, fingerprint, ct);
            if (approval is null)
            {
                return ExecutionCheck.Deny("approval.missing-for-current-revision");
            }

            if (approval.ExpiresAt <= now)
            {
                return ExecutionCheck.Deny("approval.expired");
            }

            if (approval.ReviewerId == proposal.RequesterId)
            {
                return ExecutionCheck.Deny("approval.self-approval");
            }

            var reviewerAccess = await access.EvaluateAsync(
                approval.ReviewerId, "customer-data.approve-export", proposal.TenantId, ct);
            if (!reviewerAccess.Allowed)
            {
                return ExecutionCheck.Deny("approval.reviewer-no-longer-eligible");
            }
        }

        // 4. If acknowledgment is required, it must be for this fingerprint and the
        //    notice version current policy requires.
        ExportAcknowledgment? acknowledgment = null;
        if (requirements.AcknowledgmentRequired)
        {
            acknowledgment = await acknowledgments.FindAsync(proposalId, fingerprint, ct);
            if (acknowledgment is null
                || acknowledgment.ActorId != proposal.RequesterId
                || acknowledgment.ChallengeId != requirements.ChallengeId
                || acknowledgment.ChallengeVersion != requirements.ChallengeVersion
                || acknowledgment.ExpiresAt <= now)
            {
                return ExecutionCheck.Deny("acknowledgment.missing-or-stale");
            }
        }

        return ExecutionCheck.Allow(
            proposalId, fingerprint, requirements.PolicyVersion, approval, acknowledgment);
    }
}
```

Several choices in that code are deliberate:

- **Authorization is re-evaluated, not remembered.** An analyst removed from the tenant on Tuesday should not have an export delivered on Thursday.
- **Policy is evaluated against current facts.** The legal hold placed on Wednesday is a current fact. Step 2 sees it; a stored `Approved` flag cannot.
- **Approval is looked up by fingerprint.** An approval for revision 2 cannot be found for revision 3. There is no code path that approves one revision and executes another.
- **Requirements come from current policy.** If policy now requires notice version 4, an acknowledgment of version 3 no longer satisfies it.
- **Neither approval nor acknowledgment can skip a check.** Each one only satisfies its own requirement. The legal hold still denies the export even though both records are valid.

The result, `ExecutionCheck`, records which approval and acknowledgment it relied on and which policy version it applied, so the evidence for *this execution* points to the evidence for the decisions behind it.

### The worker has its own, narrow authority

The worker does not run as the analyst. It has no copy of the analyst's token, and it should not have one. Carrying a user's session into a job that runs days later extends that session's authority far beyond the request that created it and ties the job's permissions to whatever the token happened to contain.

The worker runs under its own service identity, and its permission is narrow: *run the export for a proposal that passes the gate*. There are two common ways to enforce that:

- **Direct executor check.** The executor calls the gate itself, immediately before writing the file, and refuses to proceed on anything but `Allow`. This is the simplest option and is enough for most applications.
- **Narrow execution authority.** The gate issues a short-lived, single-use grant bound to the proposal ID and fingerprint, and the executor accepts only that grant. This is useful when the executor is a separate service or trust boundary. [Do You Need a Capability Token, or Are Roles and Claims Enough?](roles-claims-or-capability-token-dotnet.md) covers when this is worth it.

Either way, the approval should be consumed, or marked used, in the same transaction that records the execution attempt, so a retried or duplicated job cannot run the same approval twice.

## Failure Modes

Each of these starts as a reasonable shortcut.

### 1. `Acknowledged = true` treated as authorization

A requester who lacks export permission sees a warning, ticks "I understand," and the export proceeds. The checkbox proved awareness. It never proved authority. Acknowledgment should satisfy a named requirement and nothing else, and the actor who acknowledges must be the one the requirement names, not "anyone who can reach the button."

### 2. Workflow state `Approved` treated as indefinite permission

The job row says `Approved`, so the worker runs it whenever it gets to it, whether that is in an hour or next quarter. Every approval needs an expiry, and the executor must enforce it with a trusted clock. `Approved` is a disposition by a reviewer at a point in time, not a standing grant.

### 3. Approving one revision and executing another

The steward approves name and email. The analyst adds phone after approval, and the job keeps its `Approved` status because approval was stored on the job, not on the content. Bind approval to a fingerprint of the material fields, and treat any material edit as a new proposal that needs a new approval. [Human-in-the-Loop Governance Workflows](../../governance/human-in-the-loop-governance-workflows.md) discusses the options for edits during review in detail.

### 4. Retaining approval after material resource or policy drift

The approval was correct when given. Since then, the tenant went on legal hold, the policy version changed, or the reviewer lost their steward role. An execution-time policy evaluation catches the first two; re-checking the reviewer's eligibility catches the third. Whether an existing approval survives a policy change should be a deliberate rule, such as "revalidate under the latest policy," rather than an accident of which fields the worker happens to read.

### 5. Carrying the requester's session authority into a later worker

The API serializes the user's claims or access token into the job so the worker can "act as" them. The worker now acts with whatever authority the token had on Monday, which may include far more than this export, and which does not reflect removals since. Give the worker its own narrow identity, and re-evaluate the requester's current authorization instead of replaying their old one.

### 6. A generic audit message with no binding to the decision

The logs contain `"Export approved by j.doe"` and `"Export completed"`. Neither line names the proposal fingerprint, the policy version, the notice version, or which approval the execution relied on. A reviewer cannot tell whether the approved export and the executed export were the same thing. Record each decision as its own structured record bound to the proposal fingerprint, and have the execution record point to them. [Decision Receipts and Acknowledgment](../../tutorials/decision-receipts-and-acknowledgment.md) develops this evidence model.

## Storing Them Together Is Fine; Merging Them Is Not

None of this requires a workflow engine, a specialized governance store, or a capability token. A single `export_requests` table with an `approvals` table and an `acknowledgments` table beside it, plus a gate method the worker calls, is a perfectly good implementation for many applications.

What matters is that the *meanings* stay distinct even when the storage is shared:

- No single column should mean both "a reviewer accepted this" and "the worker may run this."
- An acknowledgment row should never be counted as an approval row, even if a later query finds that convenient.
- The job's status should describe where the proposal is in its lifecycle, such as `Pending`, `Approved`, `Rejected`, `Expired`, `Executed`, or `Failed`, not replace the execution check.

If you later adopt a workflow engine, keep the same separation: let the engine own waiting, reminders, and routing, and keep the final execution check in the host that owns the side effect. [Workflow Engines, Human Approval Systems, and Governed Execution](../../architecture/workflow-engines-human-approval-and-governed-execution.md) covers that division of responsibility.

## Test the Distinctions, Not Only the Happy Path

The most valuable tests prove that one decision cannot stand in for another. Using a small harness that seeds proposals, approvals, acknowledgments, and policy facts, and records executor calls:

```csharp
[Fact]
public async Task Acknowledgment_without_approval_does_not_execute()
{
    var harness = ExportHarness.WithProposal(PersonalDataExport);
    harness.Acknowledge(by: Analyst, notice: PersonalDataNoticeV3);

    await harness.RunWorkerAsync();

    Assert.Empty(harness.Executor.Calls);
    Assert.Equal("approval.missing-for-current-revision", harness.LastCheck.ReasonCode);
}

[Fact]
public async Task Approval_of_an_earlier_revision_does_not_execute_the_current_one()
{
    var harness = ExportHarness.WithProposal(PersonalDataExport);
    harness.Acknowledge(by: Analyst, notice: PersonalDataNoticeV3);
    harness.Approve(by: Steward);

    harness.EditProposal(p => p with { Fields = [.. p.Fields, "phone"] });
    harness.Acknowledge(by: Analyst, notice: PersonalDataNoticeV3);

    await harness.RunWorkerAsync();

    Assert.Empty(harness.Executor.Calls);
    Assert.Equal("approval.missing-for-current-revision", harness.LastCheck.ReasonCode);
}

[Fact]
public async Task Valid_approval_and_acknowledgment_do_not_override_a_legal_hold()
{
    var harness = ExportHarness.WithProposal(PersonalDataExport);
    harness.Acknowledge(by: Analyst, notice: PersonalDataNoticeV3);
    harness.Approve(by: Steward);

    harness.PlaceLegalHold(PersonalDataExport.TenantId);

    await harness.RunWorkerAsync();

    Assert.Empty(harness.Executor.Calls);
    Assert.Equal("tenant.legal-hold", harness.LastCheck.ReasonCode);
}
```

Each test asserts on the executor's call list, not only the returned result. A denied result proves the gate said no; an empty call list proves the export did not happen. [How to Test That a Denied Operation Never Executes](test-denied-operation-never-executes.md) explains why that difference matters and how to build the recording executor.

Useful additions to the same suite: an expired approval, a reviewer who has since lost eligibility, a requester who has since lost access, a self-approval, an acknowledgment of an outdated notice version, and a duplicate job that tries to reuse a consumed approval.

## How These Terms Map to Learning

If you continue into the rest of the Learning curriculum, the terms line up as follows:

| This article | Learning curriculum |
| --- | --- |
| Authorization | Authorization and policy evaluation, including [Policy Context and Explicit Decision Outcomes](../../tutorials/policy-context-and-explicit-decision-outcomes.md) |
| Approval | Human review with an approval disposition, in [Human-in-the-Loop Governance Workflows](../../governance/human-in-the-loop-governance-workflows.md) |
| Acknowledgment | Acknowledgment challenge and response, in [Decision Receipts and Acknowledgment](../../tutorials/decision-receipts-and-acknowledgment.md) |
| Execution permission | Revalidation plus execution authority accepted at the host-owned execution boundary, in [Scoped Capability and Host-Owned Execution](../../tutorials/scoped-capability-and-host-owned-execution.md) |

These are the terms this repository uses consistently. They are not an industry standard, and your organization may already use different words for the same ideas. Keeping the four decisions distinct matters more than which names you give them.

## A Short Review Checklist

1. **For each flag or status that sounds like permission, which of the four decisions does it record?** If the answer is "several," split it.
2. **What exact thing does each approval and acknowledgment bind to?** If it is a job ID with editable content, bind it to a fingerprint instead.
3. **Does every approval and acknowledgment expire, and does the executor enforce the expiry?**
4. **Is the requester's authorization re-evaluated at execution time, rather than remembered from the request?**
5. **Is policy evaluated against current facts at execution time, so a new hold, restriction, or policy version is seen?**
6. **Can acknowledgment satisfy anything other than its own named requirement?** If so, it has become an override.
7. **Is the reviewer's eligibility checked, including separation from the requester where policy requires it?**
8. **Does the worker run under its own narrow identity, without the requester's token or claims?**
9. **Is an approval consumed with the execution attempt, so a retry cannot use it twice?**
10. **Does the execution record point to the specific approval, acknowledgment, and policy version it relied on?**
11. **For operations that run in the same request with no reviewer and no notice, did you stop at ordinary authorization?**

## Continue Deeper

For long-running review, including reviewer eligibility, separation of duties, delegation, timeouts, and policy or context drift during the review window, continue with [Human-in-the-Loop Governance Workflows](../../governance/human-in-the-loop-governance-workflows.md).

For the acknowledgment challenge and response model, and the decision receipts that record a pause and later resumption, see [Decision Receipts and Acknowledgment](../../tutorials/decision-receipts-and-acknowledgment.md).

If you are deciding whether a workflow engine should own the approval process, [Workflow Engines, Human Approval Systems, and Governed Execution](../../architecture/workflow-engines-human-approval-and-governed-execution.md) separates what an engine does well from what the executing host must still check.

For the boundary between ordinary ASP.NET Core authorization and something larger, read [When ASP.NET Core Authorization Is Enough](../../architecture/when-aspnet-core-authorization-is-enough.md) and [When ASP.NET Core Authorization Is Not Enough](when-aspnet-core-authorization-is-not-enough.md). If the final check happens after the side effect has already begun, [Your Authorization Check Runs Too Late](authorization-check-runs-too-late.md) addresses that ordering problem.

The rule to keep is short: **authorization says who may ask, approval says a reviewer accepted this exact proposal, acknowledgment says a person saw this exact notice, and only a fresh check at the boundary says the operation may run now.**
