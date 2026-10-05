---
description: Decide whether a policy decision belongs inside your workflow engine or at the protected execution boundary, and why Approved is not current permission.
title: When Should a Workflow Engine Own the Decision?
author: Christopher D. Cavell
published: "2026-10-05"
summary: A workflow engine can own orchestration and durable state without owning current execution permission. Put the decision where authoritative context, policy freshness, and the protected side effect can be evaluated reliably.
feed: true
x_hashtags:
  - DotNet
  - SoftwareArchitecture
---

# When Should a Workflow Engine Own the Decision?

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** No formal prerequisites. Familiarity with a workflow or orchestration engine, deployment pipelines, or background jobs in .NET is helpful, but no AsiBackbone package, workflow product, policy engine, or prior Learning material is required.

**Sample scope:** The C# targets .NET 8 or later. Stores, loaders, and the deployer are illustrative interfaces, not a runnable sample; the snippets show where each responsibility lives, not a complete implementation. Workflow steps are shown as product-neutral pseudocode because workflow engines differ in how they express activities, timers, and retries.

**What this article covers:** what workflow state, approval, a policy decision, and execution permission each mean in plain terms; why reaching `Approved` does not prove that an operation may run now; five ways to divide the work between a workflow engine and the system that performs the side effect; how retries, timers, and compensation interact with policy freshness; what to record when the side effect happens; what to do when policy evaluation is unavailable; and when a workflow that shares one cohesive boundary with the executor can simply own the decision itself.

Your team already has a workflow engine. It models states, waits on timers, retries failed steps with backoff, assigns review tasks to people, and resumes reliably after a restart. A typical release looks like this:

```text
Built → StagingVerified → AwaitingApproval → Approved → WaitingForWindow → Deploying → Verifying → Completed
```

The engine is good at this, and it can usually evaluate rules too. So the obvious design is to put the deployment rules into the workflow definition: check them on the way into `Approved`, and let everything after that follow.

The question this article answers is whether that is enough:

> **A workflow engine may own orchestration and durable state without automatically owning current execution permission. Put the decision where authoritative context, policy freshness, and the protected side effect can be evaluated reliably.**

Sometimes that place is the workflow itself. Often, especially when work is delayed or the rules belong to someone else, it is the system that actually performs the side effect, at the moment it performs it.

The opposite mistake matters as much. Many workflows do not need a separate policy service, a decision store, an execution token, or any new infrastructure at all. Near the end, this article describes when the workflow, the policy, and the executor form one cohesive boundary and the workflow can own the decision outright.

---

## The Example: A Production Deployment

The rest of the article uses one operation: deploying release `2026.10.4` of the `payments-gateway` service to production.

Four parts of the organization are involved:

- The **release workflow** runs in a workflow engine. It sequences build, staging verification, approval, the wait for a change window, the deployment step, and post-deployment verification.
- The **deployment controller** is the only component that holds production deployment credentials. It changes what runs in production. This is the protected executor.
- The **change calendar** is owned by the site reliability (SRE) team. It records change windows and freezes.
- The **artifact scanning service** is owned by the security team. It reports whether a specific artifact digest has unwaived critical findings under the security team's current rules.

Here is what happens:

| When (UTC) | What happens | Owned by |
| --- | --- | --- |
| Mon 14:00 | Release `2026.10.4` builds as artifact `sha256:4b7e…e19a` and passes staging checks | Release workflow |
| Mon 16:40 | The release manager approves change `CR-2291`: this digest, to production, in Tuesday's 02:00–04:00 window | Release management, recorded by the workflow |
| Tue 02:00 | The window timer fires. The workflow moves to `Deploying` and calls the deployment controller. The call fails with a network error, and the engine schedules retries with backoff | Release workflow |
| Tue 02:20 | A payments incident starts. SRE declares a change freeze on payment services until 06:00 | SRE, in the change calendar |
| Tue 03:10 | A newly published advisory affects a library in the artifact. The scanner marks `sha256:4b7e…e19a` as having an unwaived critical finding | Security team |
| Tue 04:00 | The approved change window closes | Release management |
| Tue 04:35 | A retry finally reaches the deployment controller | Release workflow |

At 04:35, the workflow instance says `Deploying`. The approval record says `Approved`. Nothing in the workflow has moved backward, and there are three independent reasons the deployment should not happen.

---

## Four Things That Are Easy to Merge

Workflow products tend to store these side by side, sometimes in the same row. They are still different answers to different questions.

**Workflow state** answers *where is this process in its lifecycle?* `Approved` and `Deploying` describe progress. They are written by the workflow when a transition happens and remain true as history afterward.

**Approval** answers *did an eligible person accept this exact proposal?* It is a human disposition, bound to particular content (here, a digest, an environment, and a window) and given at a particular time. It is evidence that review happened.

**A policy decision** answers *do the current rules allow this operation, given the current facts?* It has an outcome, such as allowed, denied, or deferred, a reason, and the identity of the policy version that produced it. It is only as current as the facts and rules it was evaluated against.

**Execution permission** answers *may this side effect happen now, here?* It belongs to whatever component can actually refuse to perform the side effect. It exists at one moment, for one attempt, and is the only one of the four that the side effect itself depends on.

Two responsibilities sit underneath those answers:

- **Orchestration** decides *what happens next and when*: sequencing, timers, retries, routing to people, compensation.
- **Decision authority** decides *whether the side effect is permitted*: which facts are authoritative, which policy applies, and what to do when the answer cannot be obtained.

| | Answers | Produced by | Bound to | Stays true |
| --- | --- | --- | --- | --- |
| Workflow state | Where is the process? | Workflow engine | The workflow instance | As history, indefinitely |
| Approval | Did an eligible person accept this proposal? | A reviewer | Exact content and scope | Until it expires, is revoked, or the content changes |
| Policy decision | Do current rules allow this, given current facts? | A policy evaluator | A context and a policy version | Only as long as both are still current |
| Execution permission | May the side effect happen now? | The protected executor | One attempt, at one moment | For that attempt only |

A workflow engine can produce the first, record the second, and even compute the third. The fourth is the one that matters at 04:35, and it is the one a state label cannot supply.

---

## Why `Approved` Does Not Prove Current Permission

`Approved` is written in the past tense. It records that a transition happened at Monday 16:40, under Monday's facts and Monday's rules.

By Tuesday 04:35:

- **The approval's scope has lapsed.** The release manager approved a window that has closed. The approval record is accurate and still says `Approved`; it no longer covers this attempt.
- **A fact owned by another team has changed.** The freeze is a fact in SRE's calendar. The workflow did not create it and was not notified of it.
- **A judgment owned by another team has changed.** Whether an artifact with this finding may ship is the security team's rule, applied to the security team's current data.

None of these changes moved the workflow. A workflow advances on its own events: timers, task completions, activity results. Facts and rules owned elsewhere change on their own schedules. Unless something re-reads them, workflow position continues to describe a world that no longer exists.

The same applies before approval too. If the workflow had copied facts into variables on Monday, such as `approverRole = ReleaseManager`, `scanStatus = Clean`, or `freezeChecked = true`, those values are now confident statements about Monday. Workflow variables are a good place for identifiers and process data. They are a poor place for authoritative facts that someone else owns and can change.

[Authorization vs. Approval vs. Acknowledgment](authorization-vs-approval-vs-acknowledgment.md) makes the same point about the approval record itself: a reviewer's disposition is not a standing grant. This article is about the workflow around it: who should ask again, and where.

---

## Five Arrangements, Compared

The useful question is not whether your workflow product *can* evaluate rules. Most can. It is which component is in a position to evaluate them reliably at the moment the side effect happens.

### 1. Same-trust-boundary orchestration

The workflow runs in the same process or service that performs the side effect, using the same authoritative data, under rules the same team owns, with no meaningful delay between deciding and acting.

Picture a smaller organization deploying the same service. Its release workflow runs inside the team's own deployment service, which holds the production credentials. The team writes the only rules that apply: one approval, inside an agreed window, with staging green. The deploy step reads those facts from the service's own database and deploys within seconds.

Here, the workflow can own the decision. There is no second owner whose rules could drift from a copy, no authoritative context living somewhere the workflow cannot read directly, and no orchestration delay separating the decision from the deployment. The ordinary local check-to-act race between reading the facts and acting on them still exists and still has to be accepted or controlled, but it is not a gap measured in hours. The one discipline that still applies is timing: the deploy step evaluates the rules when it runs, not when the workflow passed through `Approved`.

### 2. Approval state owned by the workflow

The workflow assigns the review task, waits for it, records who approved what, and enforces expiry and escalation timers. That is ordinary, valuable workflow responsibility.

What it records is evidence that review happened, bound to the digest, the environment, and the window. It is one input to a later decision. It is not, by itself, a statement that the deployment may run at any later time. A workflow that treats its own `Approved` transition as the decision has quietly merged approval and execution permission.

### 3. Independently owned policy evaluated at execution time

The freeze rule belongs to SRE. The vulnerability rule belongs to security. Both change on their own cadence, sometimes within minutes, and neither team should have to edit, version, and redeploy every release workflow to change them.

Copying those rules into the workflow definition creates two problems. The copy drifts from the owner's current rule, and a workflow instance that started under the old definition may keep running the old copy for days. Evaluating them at the point of execution, against current data from their owners, keeps one authoritative rule and one authoritative answer.

This does not require a remote policy service. The deployment controller can call SRE's calendar and the security team's scanner directly, and evaluate a small in-process policy. What matters is that the rules and facts are current and owned by the right teams, not where the code runs.

### 4. Delayed retries and resumptions

Workflow engines make delay normal: timers, backoff, human tasks that wait for days, instances that resume after a restart. Every delay widens the gap between the last time anyone looked at the facts and the moment the side effect happens.

A retry policy answers *when to try again*. It does not answer *whether trying again is still permitted*. At 04:35, the retry is correctly scheduled and correctly executed by the engine; it is also attempting something that is no longer allowed. A resumed instance, a replayed activity, or a manually restarted step is in the same position.

### 5. Protected execution decides "now"

The deployment controller is the last component that can refuse. Whatever happened before it, it is the one that must answer *may this happen now?* It does so with facts it loads at that moment, under the policy current at that moment, and records the answer.

In this arrangement the workflow still owns the process: it decides when to ask, routes the answer, waits, retries, notifies people, and compensates. The controller owns the decision to change production. The workflow orchestrates a decision without becoming the authority that grants execution.

### Comparing the two ends

Arrangements 1 and 5 are the two ends of the choice. Arrangements 2 to 4 describe the pressures that push a design from one toward the other.

| Concern | Workflow owns the decision (same boundary) | Workflow orchestrates; the protected boundary decides |
| --- | --- | --- |
| Workflow state versus current policy outcome | State and outcome come from the same data, evaluated when the step runs | State says where the process is; the executor computes the current outcome independently |
| Orchestration versus decision authority | One component holds both; keep them as separate steps in code | The workflow holds orchestration; the executor holds decision authority |
| Approval evidence versus execution permission | Approval is an input to the step's own check | Approval is an input to the executor's check, verified against current eligibility and scope |
| Trust boundary and context ownership | The workflow can read every authoritative fact directly | Some facts live with other owners and must be loaded at execution |
| Policy versioning, freshness, revocation, and drift | One owner, one version, evaluated with no meaningful delay | Each owner changes its rules independently; the executor records which version it applied |
| Retries, timers, compensation, and idempotency | Each retry re-enters the same check | Each retry is a new request to the executor, which checks again and claims at most once |
| Decision evidence at execution | Recorded by the step that executes | Recorded by the executor, correlated with the workflow instance |
| Policy evaluation unavailable | The step fails or waits | The executor returns "not now"; the workflow decides how long to wait |

---

## The Decision Flow at Execution Time

When the side effect lives behind a protected boundary, the flow at that boundary is short:

```text
workflow reaches an executable state
    → reload authoritative resource and actor context
    → evaluate current policy at the protected boundary
    → record the decision and policy version
    → execute only when the current decision permits it
```

The workflow sends the controller identifiers, not facts:

```csharp
// What the workflow sends: which governed operation it means, and how to correlate
// this attempt. These are lookup keys and correlation IDs. None of them is trusted as
// a statement about current conditions, and none of them chooses where the
// controller deploys.
public sealed record DeploymentRequest(
    string ChangeId,               // the approved change, for example CR-2291
    string ReleaseId,
    string RequestedEnvironment,
    string WorkflowInstanceId,
    string AttemptId);
```

The controller resolves those identifiers and loads the context itself, from the systems that own each fact:

```csharp
// The governed operation: one approved change, one release, one canonical target.
// A retry of the same operation keeps this identity and gets a new AttemptId.
// A later change that legitimately deploys the same release again is a different operation.
public sealed record GovernedOperation(string ChangeId, string ReleaseId, string TargetId);

public sealed record DeploymentContext(
    GovernedOperation Operation,   // resolved by the controller, not copied from the request
    Release Release,               // artifact digest, service, row version
    DeploymentTarget Target,       // canonical target from the controller's own registry
    ChangeApproval? Approval,      // for this change: digest, target, window; may be revoked
    bool ApproverStillEligible,    // from the directory, now
    FreezeStatus Freeze,           // from SRE's change calendar, now
    ScanVerdict Scan,              // for this exact digest, from the security team's scanner, now
    bool DeploymentInProgress,     // from the controller's own state
    DateTimeOffset Now);           // from a trusted clock
```

`RequestedEnvironment` is a name the workflow uses to say which operation it means. The loader resolves it through the controller's own registry of deployment targets. An unknown name, an unknown change, or a release that does not belong to the change is a denial, not a fallback. Even a resolved target is only a candidate until the policy confirms that the approval covers it. The caller never chooses the execution target by supplying a string.

The policy is a pure function of that context. It has no side effects and can safely be evaluated any number of times:

```csharp
public enum DeploymentOutcome { Allowed, Denied, Deferred }

public sealed record DeploymentDecision(
    DeploymentOutcome Outcome,
    string ReasonCode,
    string PolicyVersion,
    DateTimeOffset? RetryAfter);

public static class ProductionDeploymentPolicy
{
    public const string Version = "prod-deploy/2026-10-01";

    public static DeploymentDecision Evaluate(DeploymentContext c)
    {
        if (c.Approval is null || c.Approval.RevokedAt is not null)
        {
            return Deny("approval.missing-or-revoked");
        }

        if (c.Approval.ArtifactDigest != c.Release.ArtifactDigest
            || c.Approval.TargetId != c.Target.Id)
        {
            return Deny("approval.does-not-cover-operation");
        }

        if (!c.ApproverStillEligible)
        {
            return Deny("approval.approver-no-longer-eligible");
        }

        if (c.Now >= c.Approval.WindowEnd)
        {
            return Deny("approval.window-closed");
        }

        if (c.Scan.HasUnwaivedCriticalFinding)
        {
            return Deny("artifact.critical-finding");
        }

        if (c.Now < c.Approval.WindowStart)
        {
            return Defer("window.not-yet-open", c.Approval.WindowStart);
        }

        if (c.Freeze.Active)
        {
            return Defer("change-freeze.active", c.Freeze.ReviewAt);
        }

        if (c.DeploymentInProgress)
        {
            return Defer("environment.deployment-in-progress", c.Now.AddMinutes(10));
        }

        return new(DeploymentOutcome.Allowed, "allowed", Version, null);
    }

    private static DeploymentDecision Deny(string reason) =>
        new(DeploymentOutcome.Denied, reason, Version, null);

    private static DeploymentDecision Defer(string reason, DateTimeOffset retryAfter) =>
        new(DeploymentOutcome.Deferred, reason, Version, retryAfter);
}
```

The controller ties it together. Resolving the operation, loading context, evaluating, and recording happen before anything touches production:

```csharp
public sealed class ProductionDeploymentController(
    IDeploymentContextLoader contexts,
    IDecisionLog decisions,
    IDeploymentClaims claims,
    IDeployer deployer)
{
    public async Task<DeploymentResult> HandleAsync(DeploymentRequest request, CancellationToken ct)
    {
        DeploymentContext context;
        try
        {
            // Resolves the change, release, and canonical target, then loads each
            // fact from its owner, now. Nothing is read from workflow variables.
            // Unresolvable identifiers come back as a Denied result, not a context.
            var loaded = await contexts.LoadAsync(request, ct);
            if (loaded.Rejection is not null)
            {
                await decisions.RecordAsync(request, loaded.Rejection, context: null, ct);
                return DeploymentResult.NotExecuted(loaded.Rejection);
            }

            context = loaded.Context;
        }
        catch (DependencyUnavailableException ex)
        {
            // The current answer cannot be known, so the answer is "not now", never "allowed".
            var unavailable = new DeploymentDecision(
                DeploymentOutcome.Deferred,
                $"context.unavailable.{ex.DependencyName}",
                ProductionDeploymentPolicy.Version,
                RetryAfter: null);

            await decisions.RecordAsync(request, unavailable, context: null, ct);
            return DeploymentResult.NotExecuted(unavailable);
        }

        var decision = ProductionDeploymentPolicy.Evaluate(context);
        var decisionId = await decisions.RecordAsync(request, decision, context, ct);

        if (decision.Outcome != DeploymentOutcome.Allowed)
        {
            return DeploymentResult.NotExecuted(decision);
        }

        // One durable, atomic conditional transition: this governed operation moves
        // from Pending to Deploying only if no execution exists for it yet and the
        // release row version is still the one the policy saw. A duplicate or late
        // attempt for the same operation loses here instead of deploying twice.
        var claim = await claims.TryClaimAsync(
            context.Operation, context.Release.RowVersion, decisionId, request.AttemptId, ct);
        if (claim is null)
        {
            // Reports the existing execution and its recorded state: completed,
            // failed, in progress, or unknown pending reconciliation. Or reports that
            // the release changed after evaluation, so the operation must be re-evaluated.
            return await claims.DescribeAsync(context.Operation, ct);
        }

        // Deploys to the canonical target the controller resolved and the policy
        // checked, never to a name taken from the request.
        var outcome = await deployer.DeployAsync(
            context.Release.ArtifactDigest,
            context.Target,
            idempotencyKey: claim.ExecutionId,
            ct);

        await claims.CompleteAsync(claim, outcome, CancellationToken.None);
        return DeploymentResult.Executed(decisionId, claim.ExecutionId, outcome);
    }
}
```

What this arrangement buys:

- **The workflow cannot supply facts or choose the target.** It names the change, the release, and the environment it means. The controller resolves those names to a canonical operation and target, and decides what is true about them.
- **Each fact comes from its owner.** The freeze comes from SRE's calendar, the finding from the security team's scanner, eligibility from the directory, at the moment of the request.
- **The policy stays pure.** `Evaluate` does not deploy, notify, open tickets, or retry. Those are side effects, and they belong to the controller and the workflow. A policy evaluator that also performs the deployment cannot be safely re-evaluated, simulated, or tested without moving production.
- **The decision is recorded before the side effect.** The decision record carries the policy version, the reason, the governed operation, the workflow instance, and the attempt.
- **At most one attempt executes, if the claim is genuinely atomic.** That guarantee depends on `TryClaimAsync` using a durable atomic conditional transition, such as a conditional update or compare-and-swap, or a unique constraint inserted in the same transaction. An ordinary read-then-write implementation would let two attempts both see `Pending` and both deploy.
- **The claim protects only what it checks.** The conditional transition guarantees that the release row is what the policy saw and that only one attempt wins. It does not freeze SRE's calendar or the scanner. A freeze declared between the policy evaluation and the deployment call is a gap measured in milliseconds; whether that is acceptable, or whether the deployer itself must enforce the freeze, is a deliberate choice. [Authorization vs. Approval vs. Acknowledgment](authorization-vs-approval-vs-acknowledgment.md) covers the check-then-claim sequence and its limits in more detail.

On the workflow side, the step that calls the controller routes each answer instead of interpreting it:

```text
when the window timer fires, or when a retry is scheduled:
    result = ask deployment controller (changeId, releaseId, environment, workflowInstanceId, attemptId)

    Allowed and executed   → record the execution ID; move to Verifying
    Deferred               → durable timer until RetryAfter, or ordinary backoff; ask again
    Denied                 → move to Blocked; notify the release manager; do not retry automatically
    Existing execution     → follow that execution's state; never start a second deployment
    Call failed or timed out → ask again with a new attempt ID; the controller reports any execution that already exists
```

At 04:35, this returns `Denied` with `approval.window-closed`. The workflow moves to `Blocked`, and the release manager decides whether to schedule a new window, which means a new approval. Had the retry arrived at 02:25 instead, it would have returned `Deferred` with `change-freeze.active`, and the workflow would have waited.

The workflow's service identity also needs only one permission: to ask the controller to deploy releases. It does not need production credentials. That is what keeps decision authority with the controller in practice, not only on a diagram.

---

## Retries, Timers, and Compensation

**Every retry re-enters through the check.** A retry is a new request to the controller, not a continuation of an old permission. The decision made at 02:00, had the call succeeded, would have been valid for the 02:00 attempt. It says nothing about 04:35.

**Retries and idempotency are different protections.** Re-checking stops a retry from deploying something no longer permitted. The claim stops a duplicate from deploying something twice. If an activity times out after the controller has already started a deployment, the workflow's retry should learn about that execution, not start a second one. That is why the claim is keyed to the governed operation, the approved change, release, and target, with each attempt recorded underneath it, and why the controller can report an existing execution.

**The operation identity must be narrow enough to allow a legitimate repeat.** Keying the claim to the release and environment alone would be too broad. If release `2026.10.4` deploys, is rolled back, and a new change later approves deploying it again, a `(release, environment)` key would treat the new, separately authorized deployment as a duplicate of the first. Including the change identifier keeps retries of one approved operation together and makes a newly approved operation a new one.

**An existing claim with an unknown outcome is not a reason to deploy again.** Suppose the claim succeeds, the deployer finishes, and the controller crashes before `CompleteAsync` records the result. On the next attempt the claim already exists, but the outcome is not recorded. The controller reports that execution as in progress or indeterminate, and a reconciliation process asks the deployment platform what actually happened to that execution ID. The workflow waits for that answer; it does not initiate another deployment.

**Pinning a decision is a legitimate choice when it is explicit.** Some organizations decide that an approval given under policy version `v7` remains valid for the whole change window, even if the policy changes during it. That can be reasonable. It should be a written rule enforced at the controller, with the pinned version recorded in the decision, not an accident of which component happened to look last. [Human-in-the-Loop Governance Workflows](../../governance/human-in-the-loop-governance-workflows.md#policy-drift-during-the-review-window) compares latest-policy revalidation, explicit grandfathering, and conditional migration.

**Compensation is a separate operation with its own decision.** If verification fails after deployment, the workflow rolls back to the previous release. Rolling back also changes production. Many freeze policies deliberately allow rollback to a last-known-good release while blocking new changes, which is exactly why rollback should be evaluated as its own operation under its own rule, not waved through because it is "undoing" something. Compensation also does not erase history: the deployment and its decision record remain, and the rollback adds its own.

---

## When Policy Evaluation Is Unavailable

If the controller cannot reach SRE's calendar or the scanner, it does not know whether a freeze is active or whether the artifact may ship. The honest answer is *not now*.

For a production deployment, that means returning a deferred, retryable result and recording why. The workflow is well suited to what happens next: wait, retry with backoff, and alert someone when the outage outlasts the change window. Waiting is what workflow engines are good at, and treating "cannot decide" as "allowed" would turn a dependency outage into an uncontrolled change.

Two related points:

- **Fail-closed is a default, not a complete plan.** Decide how long the workflow waits, who is told, and what happens when the window closes while the dependency is down.
- **Emergency access is a separate path, not a fallback.** If production must change while a policy source is down, use a deliberately designed break-glass procedure with its own authorization and evidence, not a branch in the normal deployment path that skips the check when a call fails.

---

## Choosing Where the Decision Lives

Ask these questions about the operation, not the product:

| Question | The workflow can own the decision when… | Evaluate separately at the protected boundary when… |
| --- | --- | --- |
| **Trust boundary:** does the workflow run where the side effect happens? | It runs in the same service, with the same credentials and data | The side effect happens in another service, host, or environment |
| **Context ownership:** can the workflow read every authoritative fact directly? | Every fact lives in data the workflow's own service owns | Some facts belong to other teams or systems |
| **Policy ownership:** who writes the rules? | The team that owns the workflow | Another team, or several teams |
| **Policy cadence:** how often do the rules change, and how quickly must changes apply? | Rarely, and only with a release of the same service | Independently of workflow definitions, possibly mid-flight |
| **Delay:** how long between the check and the side effect? | Effectively none | Minutes, hours, or days |
| **Retries and resumption:** can an attempt run much later than planned? | Retries are immediate and re-run the same check | Timers, backoff, human tasks, or restarts can separate them |
| **Execution authority:** who can refuse to perform the side effect? | The workflow's own step | A component other than the workflow |
| **Evidence:** who needs to explain the decision later? | The owning team, from its own records | Auditors or other teams, independent of the workflow platform |

If every answer falls in the left column, the workflow can own the decision. One answer in the right column does not demand a new service; it means that fact or rule must be evaluated where it is current, which is usually the executor.

The same reasoning as a flow:

```text
Does the side effect happen inside the workflow's own trusted service?
    no  → the executor decides at execution time; the workflow orchestrates
    yes ↓
Are all rules owned by the same team and all facts held in that service's own data?
    no  → evaluate the externally owned rules and facts at execution time, in the executor
    yes ↓
Can meaningful time pass between checking and acting (timers, retries, human waits)?
    yes → keep the rules in the workflow, but evaluate them in the step that acts, every time it runs
    no  → the workflow owns the decision; evaluate it in the step that acts
```

The last two branches keep the decision in the workflow. Both evaluate in the step that performs the side effect. Location can be flexible; timing cannot.

---

## When the Workflow Can Own the Decision

Return to the smaller organization from arrangement 1. Its workflow, rules, data, and production credentials all live in one deployment service owned by one team. There is no meaningful delay between the check and the deployment, and no other team whose rules or facts could change underneath it.

For that team, a separate policy service would add a network dependency, a second deployment, and a new failure mode, while addressing none of the policy-ownership, freshness, or trust-boundary risks present in this scenario. A separate service can reduce other risks, such as inconsistent rules across many services or rule changes that need independent change control, but this team has none of those pressures. Neither would a decision store separate from its own database, an execution token passed between its own components, or an acknowledgment step nobody asked for. None of these is a maturity requirement.

What that team should still do costs very little:

- Keep the rules as a small, pure function that the deploy step calls, not scattered across transition conditions.
- Evaluate it in the deploy step, every time that step runs, including on retry.
- Read facts from the service's own data at that moment, not from values captured when the workflow started.
- Record the outcome and a policy version alongside the deployment.

If the organization grows, and SRE starts owning freezes or security starts owning artifact rules, the same function moves naturally: the externally owned facts are loaded at execution, and the boundary that holds the credentials evaluates them. Nothing about the workflow's orchestration has to change.

[When a Simple Application Service Is Enough](../../architecture/when-a-simple-application-service-is-enough.md) makes the same proportionality argument for application code generally, and [Policy as Code in ASP.NET Core Without Overengineering](policy-as-code-aspnet-core-without-overengineering.md) explains which pressures justify moving a policy out of process.

---

## Failure Modes

Each of these starts as a reasonable shortcut.

### 1. Treating workflow state `Approved` as indefinite permission

The deployment step checks `instance.State == Approved` and proceeds. That proves a transition happened. It says nothing about whether the approved window, the approver's eligibility, or the approved content still apply. Check the approval's scope and validity at execution, as one input among others.

### 2. Evaluating policy once when work is queued, and never again

The rules run on the way into `Approved` or when the deployment is scheduled, and the result is stored as a flag. Every later step trusts the flag. Freezes, findings, and policy changes that arrive after the evaluation are invisible. Evaluate at the step that acts.

### 3. Embedding stale identity, tenant, or risk facts in workflow variables

`approverRole`, `tenantStatus`, `riskScore`, and `scanStatus` are captured at the start and read later as though they were current. Store identifiers in workflow variables, and load the facts they refer to from their owners when they matter.

### 4. Letting retry position bypass a current decision check

The first attempt passed the check; the retry jumps straight to the side effect because "we already checked." A retry hours later is a new attempt under new conditions. Every attempt passes through the check, and an atomic claim on the governed operation ensures at most one of them executes.

### 5. Confusing human approval with actor authorization or execution authority

The release manager approved, so the workflow is given production credentials to act on that approval. Approval is a reviewer's disposition about a proposal. It does not authorize the reviewer to deploy, and it does not make the workflow engine the authority that changes production. Give the workflow permission to ask; leave the authority to deploy with the controller.

### 6. Placing protected side effects inside a policy evaluator

The policy evaluation function calls the deployer when the answer is allowed, or opens a ticket when it is denied. Now evaluating the policy changes the world, a "what would the policy say?" check deploys to production, and a retried evaluation deploys twice. Keep evaluation pure, and let the controller act on its result.

### 7. Creating a remote policy dependency where a same-host decision is sufficient

A team with one service, one set of rules, and no delay adds a remote policy service because larger organizations use one. The result is a new outage mode and slower deployments, for no change in who owns the rules or how current they are. Move policy out of process when ownership, sharing, or cadence requires it.

### 8. Treating workflow history as decision evidence

The workflow's event history shows that `Deploying` followed `Approved`. It does not show which freeze state, scanner verdict, eligibility check, or policy version allowed the deployment at the moment it happened. Record the decision where it was made, and correlate it with the workflow instance.

---

## Evidence at Execution

When the controller deploys, the record that explains why should capture, at least:

- The release, artifact digest, and environment that were deployed.
- The decision outcome, reason code, and policy version.
- The facts the decision used, or references to versioned facts that can still be retrieved: the freeze state, the scanner verdict and its rule version, the approval record and the approver's eligibility at the time.
- The governed operation (change, release, and canonical target), the workflow instance and attempt that asked, and the execution ID that the claim produced.
- The deployment outcome, linked by execution ID, including outcomes that are not yet known.

Denied and deferred decisions are worth recording as well. "We tried to deploy at 04:35 and the controller refused because the window had closed" is exactly the record an incident review needs, and it is the one a state machine that never left `Deploying` cannot provide. [Your Audit Log Records the Story, Not the Decision](your-audit-log-is-not-evidence.md) explains why an ordinary log entry rarely carries enough to reconstruct a decision.

---

## A Short Review Checklist

**Responsibilities**

1. Can you name the component that can refuse the side effect, and is it the one that decides?
2. Does the workflow hold only the permission to ask, rather than the credentials to act?
3. Are workflow state, approval, policy decision, and execution permission stored as distinct things, even if one product stores all of them?

**Freshness**

4. Is every fact that another team owns loaded from that owner at execution time?
5. Do workflow variables hold identifiers rather than authoritative facts?
6. Does every retry, resumption, and manual restart pass through the current check?
7. If a decision is deliberately pinned to a policy version, is that rule written down, enforced at the executor, and recorded?

**Behavior**

8. Is the policy evaluator free of side effects, so it can be evaluated any number of times?
9. Do unavailable dependencies produce "not now" rather than "allowed," with a defined waiting and alerting plan?
10. Is compensation evaluated as its own operation under its own rule?
11. Can a duplicate or late attempt cause the protected side effect at most once, through a genuinely atomic claim keyed to the approved operation rather than to the release alone?

**Proportionality and evidence**

12. If the workflow, rules, data, and executor form one boundary with no meaningful delay, have you avoided adding remote policy infrastructure you do not need?
13. Can you trace a production change back to its decision, policy version, facts, approval, and workflow instance?

---

## Continue Deeper

For the complete responsibility model behind this article, including five worked scenarios, the conditions under which a workflow engine can carry governance responsibilities, and testing invariants for workflow, review, policy, and execution, continue with [Workflow Engines, Human Approval Systems, and Governed Execution](../../architecture/workflow-engines-human-approval-and-governed-execution.md).

When durable human review is the main problem, including reviewer eligibility, separation of duties, review timeouts, and policy or context drift during the review window, read [Human-in-the-Loop Governance Workflows](../../governance/human-in-the-loop-governance-workflows.md).

For the foundational boundary this article relies on, where a proposed operation, an explicit decision, and the host-owned side effect stay separate, work through [Decision Before Execution](../../tutorials/decision-before-execution.md).

If the question is where evaluation should run, embedded or remote, and how policy distribution, caching, freshness, and partition behavior affect that choice, read [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../../architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md).

If the later executor runs as a separate worker and needs narrow, short-lived authority for one delayed operation, [How Short-Lived Execution Authority Differs from User Authorization](short-lived-execution-authority-vs-user-authorization.md) follows that handoff through one scheduled payout. For recording which policy version produced a decision and how to handle policy changes over time, see [Policy Versioning and Decision Provenance](../../governance/policy-versioning-and-decision-provenance.md).

### Related Work

These references address related aspects of the problem:

- [NIST SP 800-207, Zero Trust Architecture](https://csrc.nist.gov/pubs/sp/800/207/final) (August 2020) separates the policy decision point, made up of a policy engine and a policy administrator, from the policy enforcement point. It decides each access to an individual resource under dynamic policy, with authentication and authorization reevaluated on an ongoing basis using current information, rather than treating an earlier authentication as lasting trust.
- [NIST SP 800-162, Guide to Attribute Based Access Control](https://csrc.nist.gov/pubs/sp/800/162/upd2/final) (originally published 2014 and updated in 2019) describes decisions computed by evaluating subject, object, operation, and environment-condition attributes against policy, with environment conditions such as current time, location, and threat level assessed when access is requested. That is the same freshness question applied to access control.

Neither addresses workflow engines directly. Both provide closely analogous access-control principles that this article applies to workflow-governed execution: the decision belongs where current facts and the protected resource meet.

---

## The Short Answer

A workflow engine is the right owner of the process: what happens next, when to wait, when to retry, who to ask, how to compensate. It may also be the right owner of the decision, when the workflow, its rules, its facts, and the side effect all live in one boundary with no meaningful delay. In that case, evaluate the rules in the step that acts, every time it runs, and add nothing else.

When the rules belong to other teams, the facts live elsewhere, or time can pass between approval and action, let the workflow orchestrate and let the component that performs the side effect decide. It reloads the context, evaluates the current policy, records the decision and its version, and acts only when that decision allows it now.

Either way, `Approved` tells you how the process got here. It does not tell you that the operation may run.
