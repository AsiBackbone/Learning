---
description: Learn why Polly, queue, and background-job retries decide when an attempt may run again, not whether an earlier authorization decision still applies.
title: Your Retry Logic May Be Replaying an Authorization Decision
author: Christopher D. Cavell
published: "2026-10-25"
summary: A retry policy decides when an attempt may run again. It does not decide whether the authorization behind the first attempt still holds. Put the eligibility check inside every retry loop that can outlive the decision's validity, resend only what is established as safe to repeat, and reconcile ambiguous outcomes instead of resending them.
feed: true
x_hashtags:
  - DotNet
  - SoftwareArchitecture
---

# Your Retry Logic May Be Replaying an Authorization Decision

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** No formal prerequisites. Familiarity with ASP.NET Core authorization, `HttpClient`, and at least one retry mechanism such as Polly, `Microsoft.Extensions.Http.Resilience`, a message queue, or a background-job library is helpful. No AsiBackbone package or prior Learning material is required.

**Sample scope:** The C# targets .NET 8 or later and uses `Microsoft.Extensions.Http.Resilience`, which is built on Polly v8. The stores, the authorizer, the storage provider, and the test harness are illustrative interfaces and fakes, not a runnable sample or a recommended persistence API. Library defaults are quoted from the vendors' documentation as of October 2026; check the current documentation for your versions.

**What this article covers:** the four mechanisms .NET teams usually mean by "retry," and how long each can stretch; what a retry policy knows and what it does not; why the position of the authorization check relative to each retry loop decides whether old authority is replayed; re-establishing eligibility on every attempt, and what to do when full re-evaluation is not possible; separating failures that are safe to retry from ambiguous outcomes that must be reconciled; why refreshing a service credential is not re-authorizing the request; which identifiers stay stable across retries and which change; tests that prove a retry cannot execute under stale authority; and when ordinary retry plus current authorization is the whole design.

**In plain terms:** a manager signs off on a delivery at 9 a.m. The truck breaks down, and the dispatcher's rule is "keep trying until it gets there." By afternoon, the customer has cancelled the order. The dispatcher's rule was about trucks, not about orders, so the delivery goes ahead anyway. Nobody did anything wrong by their own rules: the manager approved a valid order, and the dispatcher retried a failed delivery. The mistake was letting the dispatcher's rule carry the manager's approval into the afternoon without asking whether it was still true.

A project lead shares a contracts folder with an outside reviewer at 9:14 on a Monday morning. The endpoint checks the lead's role, the project's status, and the reviewer's domain, and every check passes. The storage provider is having an outage, so the background job that creates the invitation fails, and the job library schedules it to run again later. At 11:40, legal places the project under a litigation hold, which forbids any new external sharing. At 14:30 the provider recovers, the job's next retry runs, and the outside reviewer receives edit access to a folder that is now under legal hold.

Every component did exactly what it was configured to do. The endpoint authorized a valid request. The `HttpClient` pipeline retried a `503`. The job library retried a failed job. None of them was asked whether the 9:14 decision was still true at 14:30, and none of them could have answered.

> **A retry policy answers "may this attempt run again, and when?" It does not answer "is the operation still allowed?" Any retry loop that can outlive the validity of the decision behind it should re-establish eligibility inside the loop. Resend only what your system or the provider can establish as safe to repeat, and reconcile ambiguous outcomes instead of resending them.**

The opposite mistake matters too. Most retries are harmless, many need no extra authorization logic at all, and not every retry needs a new token or a new approval. Near the end, this article covers when ordinary retry plus the authorization you already have is enough.

---

## The Example: Sharing a Folder After an Outage

The rest of the article uses one system.

A project-management application lets project leads share folders with external collaborators. The **sharing API** is an ASP.NET Core service. Its endpoint runs an authorization policy that requires three facts: the caller is a lead on the project, the project is not under legal hold, and the collaborator's email domain is on the organization's external-sharing allowlist. When the policy allows the request, the API records the operation and enqueues a background job. A **sharing worker**, which holds the only credentials for the storage provider, runs the job and calls the provider's `POST /invitations` endpoint.

Assume the provider documents two things that matter later. A `503 Service Unavailable` or `429 Too Many Requests` response with a `Retry-After` header means the request was not processed. The provider does not accept an idempotency key on `POST /invitations`, but it does offer a read endpoint that lists a folder's pending invitations and permissions.

On Monday:

| When (UTC) | What happens |
| --- | --- |
| 09:14:02 | The lead requests sharing for folder `PRJ-204/contracts` with `reviewer@partner.example` (edit access). The policy allows it: decision `DEC-5120`, policy revision 41, project version 17. The API records operation `OP-7310` and enqueues job `JOB-88` |
| 09:14:03 | The worker runs `JOB-88`. The provider returns `503` with `Retry-After`. The `HttpClient` resilience pipeline resends three times over about 15 seconds and gets `503` each time |
| 09:14:18 | The job throws. The job library marks the run failed and schedules another one with an increasing delay |
| 09:15 – 14:00 | Further runs fail the same way |
| 11:40 | Legal places project `PRJ-204` under a litigation hold. The project's version becomes 18 |
| 14:30:05 | The provider has recovered. The next run of `JOB-88` creates the invitation |

The outage was ordinary. The failure is that the 14:30 run executed under a decision made at 09:14, after the project's authorization-relevant state had changed.

---

## Four Things Called "Retry"

When a team says "we retry that," it usually means several mechanisms stacked on one another. They repeat different things and stretch over very different spans of time.

| Mechanism | What repeats | Typical span | Default behavior worth knowing |
| --- | --- | --- | --- |
| `HttpClient` resilience handler | One HTTP request, resent from inside one call | Seconds | `AddStandardResilienceHandler` retries up to 3 times with exponential backoff and jitter from a 2-second base, bounded by a 30-second total timeout and a 10-second timeout per attempt. By default its retry strategy applies to all HTTP methods, including `POST`, unless specific methods or the standard set of unsafe methods are disabled |
| Application retry pipeline | A delegate in your own code, run again by a Polly `ResiliencePipeline` | Seconds to minutes | Everything inside the delegate runs again on each attempt; everything outside it runs once |
| Queue redelivery or job retry | The whole message handler or job, in a later process | Minutes to days | In peek-lock processing, Azure Service Bus increments a message's delivery count when it is abandoned or its lock expires, and moves it to the dead-letter queue when the count exceeds `MaxDeliveryCount`, 10 by default. Hangfire's globally applied `AutomaticRetryAttribute` schedules a failed job for up to 10 retry attempts by default, with increasing delays |
| Operator or user resubmission | The same message or job, started by a person | Hours to weeks | A dead-letter "resend" tool or a "retry" button in an admin page can run work from any point in the past |

The sources are the [standard resilience handler defaults](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience#standard-resilience-handler-defaults), [Service Bus maximum delivery count](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-dead-letter-queues#maximum-delivery-count), and [Hangfire's documentation on dealing with exceptions](https://docs.hangfire.io/en/latest/background-processing/dealing-with-exceptions.html).

These layers multiply. Under the defaults above, one job run can make four HTTP attempts: the original request and three retries. A job retried 10 times runs 11 times, so it can make as many as 44 attempts, spread over hours. Each layer was configured by someone thinking about transport failures. None of them was configured by someone thinking about how long the authorization decision stays true.

---

## What a Retry Policy Knows, and What It Does Not

A retry strategy decides from what it is given:

- the exception type or HTTP status code of the last attempt;
- how many attempts have run;
- how much time has passed, and what the backoff says to wait;
- sometimes a `Retry-After` header.

That is the right input for the question it answers: *is this failure likely to be transient, and is it worth trying again?*

It does not inherently know whether the lead is still a lead, whether the project is under legal hold, whether the reviewer's domain was removed from the allowlist, or whether the sharing policy was revised. Those facts live in the sharing API's database and policy, not in an exception. Polly lets you supply custom predicates and callbacks, so a team *could* query them from inside a retry strategy, but that would make a transport component the owner of authorization policy. A retry policy that ran at 14:30 had every reason to try again, because the provider was finally healthy. That was a correct answer to its own question and an irrelevant answer to the question that mattered.

The two questions also run on different clocks. Waiting can improve the chance that a genuinely transient condition has cleared, and it reduces pressure on an unhealthy dependency; that is why retry systems use backoff. Meanwhile, as elapsed time grows, so does the opportunity for the facts behind an authorization decision to change, and the retry policy has no way of telling whether any of them did. A loop tuned for the first clock will carry a decision past the second.

---

## Where the Decision Sits Relative to the Loop

What a retry repeats is decided by where the loop's boundary falls. Whatever is inside the boundary runs again on each attempt. Whatever is outside it runs once, and its result is reused by every attempt.

### The Version That Replays the Decision

This is the shape of the Monday system. It looks like a normal ASP.NET Core design:

```csharp
// Anti-pattern: the job inherits a decision made in a different process, hours earlier.
app.MapPost("/projects/{projectId}/shares", async (
        string projectId, ShareRequest request, ClaimsPrincipal user,
        IAuthorizationService authorization, IShareStore store, IBackgroundJobs jobs, CancellationToken ct) =>
    {
        // Resource-based authorization: the policy's handler needs the project and the
        // requested share, so it runs here after loading them, once, at 09:14.
        ShareResource resource = await store.LoadShareResourceAsync(projectId, request, ct);
        AuthorizationResult allowed = await authorization.AuthorizeAsync(user, resource, "CanShareExternally");
        if (!allowed.Succeeded) return Results.Forbid();

        string operationId = await store.CreateOperationAsync(resource, user, ct);
        // The job runner supplies the execution-time cancellation token.
        jobs.Enqueue<ShareFolderJob>(job => job.RunAsync(operationId, CancellationToken.None));
        return Results.Accepted($"/operations/{operationId}");
    })
    .RequireAuthorization();

public sealed class ShareFolderJob(IShareStore store, IStorageClient storage)
{
    // The job library retries this method on any exception.
    public async Task RunAsync(string operationId, CancellationToken ct)
    {
        ShareOperation operation = await store.GetAsync(operationId, ct);
        await storage.InviteAsync(operation.Request, ct); // runs whenever the provider is healthy
        await store.MarkCompletedAsync(operationId, ct);
    }
}
```

The authorization check sits outside every retry loop: it ran in the request, and the job assumes its result. Each layer of retry is inside the job, so each one reuses that result. Nothing in `RunAsync` refers to the decision explicitly, which is why the reuse is easy to miss in review. Enqueuing the job *is* the decision, carried forward for as long as the job library keeps trying.

The same shape appears without a job library. A Polly pipeline in a controller can wrap only the provider call while the authorization check sits above it, and a long `MaxRetryAttempts` with exponential backoff can stretch that call over minutes. A message handler can trust that "a message exists" means "the operation was allowed." A saga can treat "the workflow reached step 4" as permission to run step 4 again after a timeout.

### The Rule

None of the libraries above states this rule; it is this article's derived design principle:

> **Re-establish eligibility inside every retry loop whose possible lifetime can exceed the validity boundary of the decision authorizing it.**

That makes validity an explicit property of the decision, not an intuition about elapsed time. An inner loop that resends one request may reuse the decision of the attempt it belongs to when the policy treats that bounded resend window as part of one authorization event. A short window is usually a reasonable choice, but shortness alone does not make it safe: a legal hold or a revoked membership can land a second after evaluation. Operations that need revocation to take effect immediately need enforcement coordinated with dispatch, not just a short window.

An outer loop that can run again minutes, hours, or days later should re-establish eligibility on every iteration. If you do not know how long a loop can run, find out: count the attempts, add the backoff and any server-requested delays, and include the dead-letter resubmission path, which has no upper bound at all.

---

## Re-establishing Eligibility on Every Attempt

The simplest fix is also usually the best: treat each run of the job as one attempt, and make the attempt evaluate current authorization before it does anything with an effect.

```csharp
public sealed class ShareFolderJob(
    IShareStore store,
    IShareAuthorizer authorizer,   // reads roles, holds, and allowlists from authoritative stores
    IStorageClient storage)
{
    // Each run is one attempt. The job library may start another run minutes or hours
    // later, so nothing here relies on a decision made in an earlier run.
    public async Task RunAsync(string operationId, CancellationToken ct)
    {
        ShareOperation operation = await store.GetAsync(operationId, ct);

        if (operation.State is not ShareState.Pending)
        {
            // Completed, Rejected, Executing, or Unknown. An Unknown operation was already
            // handed to reconciliation when it entered that state; another retry must not resend.
            return;
        }

        // Current facts, current policy revision, recorded as a new decision. The operation
        // stores the authenticated requester and an immutable copy of the request, so the
        // authorizer evaluates exactly what was asked, by whom. DEC-5120 is left unchanged.
        ShareDecision decision = await authorizer.EvaluateAsync(operation.Requester, operation.Request, ct);
        await store.RecordDecisionAsync(operationId, decision, ct);

        switch (decision.Outcome)
        {
            case DecisionOutcome.Allowed:
                break;

            case DecisionOutcome.Denied:
                // A retry cannot change a current denial. Finish the job and tell the requester.
                await store.TryMarkRejectedAsync(operationId, decision.DecisionId, ct);
                return;

            case DecisionOutcome.CannotDetermine:
                // The authorizer itself was unavailable: retry the question later,
                // never treat it as permission.
                throw new RetryableShareException(operationId, decision.ReasonCode);

            default:
                // An outcome this code does not recognize is never permission.
                await store.TryMarkNeedsReviewAsync(operationId, decision.DecisionId, ct);
                return;
        }

        // Atomic and durable: succeeds only if the operation is still Pending and the
        // versions this decision read are still current (see the store contract below).
        ClaimResult claim = await store.TryStartAttemptAsync(operationId, decision, ct);
        switch (claim.Status)
        {
            case ClaimStatus.Claimed:
                break;

            case ClaimStatus.OwnedElsewhere or ClaimStatus.Terminal:
                // Another run holds the operation, or it has already finished.
                return;

            case ClaimStatus.FactsChanged:
                // Still Pending, but a fact this decision read changed before the claim.
                // Schedule another run, which evaluates again from current facts.
                throw new RetryableShareException(operationId, "claim.facts-changed");

            default:
                // The claim's commit outcome is uncertain. Nothing has been dispatched;
                // the next run reads the operation's state before doing anything.
                throw new RetryableShareException(operationId, "claim.uncertain");
        }

        InviteResult result = await storage.InviteAsync(operation.Request, ct);

        // If the process stops after the provider applies the effect but before this
        // outcome is committed, recovery classifies the attempt as Unknown and
        // reconciles it before another attempt can begin.
        await store.RecordOutcomeAsync(claim.AttemptId!, result, ct);

        if (result.Outcome == InviteOutcome.NotApplied)
        {
            // The provider documented that nothing happened, so a later run may try again,
            // no earlier than the provider asked, and that run evaluates authorization first.
            throw new RetryableShareException(operationId, "provider.not-applied", result.RetryNotBefore);
        }
    }
}
```

The interfaces are illustrative, but the job is only as safe as the semantics behind them. The store has to guarantee these:

- **`TryStartAttemptAsync` is an atomic, durable claim.** A conditional update, a concurrency token, or a uniqueness constraint moves the operation from `Pending` to `Executing` and records the attempt in one step. An in-memory check followed by a separate write does not provide this. Where the policy requires it, the same conditional write also checks that the project, membership, and allowlist versions the decision read are still current, which shrinks the gap between checking and using the facts. It does not close that gap entirely: the provider call still happens after the claim.
- **The claim reports why it failed.** Another run owning the operation and a terminal operation end the run. A changed fact leaves the operation `Pending` and schedules a fresh evaluation, so the job is not marked finished with work still outstanding. An uncertain commit dispatches nothing and lets the next run read the durable state.
- **Outcomes are durable state transitions.** `Completed` and `Unknown` are recorded against the operation. `NotApplied` returns it to `Pending` atomically, with any provider-requested `NotBefore` time persisted, so the next run waits for it and re-evaluates before trying again.
- **An abandoned `Executing` attempt becomes `Unknown`.** If the worker dies holding a claim, a lease or timeout moves the attempt to `Unknown`, never back to `Pending`.
- **Entering `Unknown` hands the operation to reconciliation.** The job returning early is safe only because something else is responsible for the unresolved operation and it stays visible to operators.
- **Terminal markers are conditional.** `TryMarkRejectedAsync` and `TryMarkNeedsReviewAsync` succeed only from `Pending`, so a concurrent denial cannot overwrite an operation that is executing or already completed.

On Monday, this version behaves differently from 11:40 onward. The 14:30 run evaluates the policy against project version 18, sees the legal hold, records `DEC-5188` (`Denied`, `project.legal-hold`), marks `OP-7310` rejected, and never calls the provider. The lead is told that the share did not happen and why. `DEC-5120` still says `Allowed`, because at 09:14 it was.

Three details matter more than the code's shape:

- **The authorizer reads authoritative state, not the original request's identity artifacts.** The job has no `HttpContext` and no `ClaimsPrincipal`. Re-evaluation works only if the facts the policy needs, such as project membership, legal holds, and allowlists, can be read from the stores that own them. Copying the lead's serialized claims into the job to "re-run" the policy later would only replay the 09:14 identity snapshot. Copying the lead's access token would do the same and also put a bearer credential in a queue.
- **`CannotDetermine` is not `Denied`, and it is not `Allowed`.** If the authorizer cannot answer, the attempt is deferred. Retrying the *question* is safe because asking has no side effect. [Should Authorization Fail Open, Fail Closed, or Defer?](fail-open-fail-closed-or-defer.md#resilience-is-transport-not-authority) covers that case in depth.
- **The evaluation counts as part of the attempt.** It is recorded with the attempt it permitted, so the evidence can show which decision allowed which attempt.

Re-evaluating on every run costs one policy evaluation per attempt. For many applications, that is cheaper than one incident.

### When Full Re-evaluation Is Not Possible

Sometimes the original decision cannot simply be run again. A human approved the operation. A risk check ran against data that is no longer available. The policy needs an input that only existed in the original request.

Then the job cannot ask "is this allowed now?" from scratch, but it can still ask "is the earlier decision still applicable?" That requires the decision to carry its own bounds, recorded when it was made:

```csharp
public sealed record ShareDecision(
    string DecisionId,
    DecisionOutcome Outcome,
    string ReasonCode,
    long PolicyRevision,
    long ProjectVersion,          // project status, including legal holds
    long MembershipVersion,       // the requester's role on the project
    long AllowlistVersion,        // the external-sharing domain allowlist
    DateTimeOffset ValidUntil);   // the operation's freshness rule, set by policy

static bool StillApplies(ShareDecision decision, CurrentFacts current, DateTimeOffset now) =>
    decision.Outcome == DecisionOutcome.Allowed
    && now < decision.ValidUntil
    && decision.PolicyRevision == current.PolicyRevision
    && decision.ProjectVersion == current.ProjectVersion
    && decision.MembershipVersion == current.MembershipVersion
    && decision.AllowlistVersion == current.AllowlistVersion;
```

The bounds have to cover every fact the decision depended on. If the check compared only the project version and policy revision, removing the lead from the project or the reviewer's domain from the allowlist would change nothing it looks at, and `StillApplies` would keep returning `true` after the authority was gone. When a policy deliberately lets some facts stay fixed until expiry, write that down as part of the policy.

When `StillApplies` is false, the job does not retry and does not fail with a generic error. It moves the operation to a state that requests a new decision, which may mean returning it to the approver. A retry loop must never be the thing that extends an approval past its bounds.

Whether a resource-version change or a policy revision should invalidate a decision is itself a policy choice. Comparing the whole project version is conservative: a renamed project would also invalidate the decision. Some teams compare only the facts the decision depended on. Either is reasonable if it is written down and enforced at the execution boundary, not inferred by whichever retry happens to run.

---

## Resend Only What Is Safe to Repeat. Reconcile the Rest.

Re-evaluating authorization stops retries from reusing an earlier decision, within the validity rules and the dispatch gap described above. It does nothing about duplicated effects. The second half of the problem is deciding which failures may be retried at all.

Suppose nothing changed on Monday, and the 14:30 run is allowed. This time the provider is slow: it creates the invitation, but its response takes 12 seconds. The standard resilience handler's 10-second attempt timeout fires, the retry strategy handles the resulting `TimeoutRejectedException`, and about two seconds later it resends the `POST`. The provider has no idempotency key for this endpoint, so it creates a second invitation. The application records one invitation ID. Months later, when someone revokes the reviewer's access, the recorded invitation is revoked and the other one still works.

No authorization rule was broken. The second request was sent under a decision that was current and valid. The problem is that the attempt's outcome was **unknown**, and the retry treated "I did not hear back" as "nothing happened."

For an external operation whose duplicate effect would be unacceptable, configure automatic resending only for outcomes that your system or the provider can establish as safe to repeat. In this example, that means only responses the provider documents as not processed:

```csharp
builder.Services
    .AddHttpClient<IStorageClient, StorageClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["Storage:BaseAddress"]!);
    })
    // If ConfigureHttpClientDefaults adds a standard handler to every client,
    // remove it here rather than stacking a second pipeline on top of it.
    .RemoveAllResilienceHandlers()
    .AddResilienceHandler("storage-invitations", pipeline =>
    {
        // Outermost: bounds the whole resend window, including server-requested delays.
        pipeline.AddTimeout(TimeSpan.FromSeconds(20));
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 2,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldRetryAfterHeader = true,   // chooses the delay; it does not require the header
            // Timeouts, connection resets, and other 5xx responses are not retried here:
            // the request may already have taken effect.
            ShouldHandle = args => ValueTask.FromResult(ShouldResendNow(args.Outcome.Result, TimeProvider.System))
        });
        pipeline.AddTimeout(TimeSpan.FromSeconds(10)); // per attempt, inside the retry
    });

// Evidence: the provider documents "not processed" as the status code *and* a Retry-After
// header, in either its delay-seconds or HTTP-date form. Without the header, there is no
// documented guarantee, and the storage client classifies the response as Unknown.
static bool IsDocumentedNotProcessed(HttpResponseMessage? response) =>
    response is { StatusCode: HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests }
    && response.Headers.RetryAfter is { Delta: not null } or { Date: not null };

// Timing: a separate question. A documented not-processed response whose requested wait
// exceeds the resend window is still NotApplied; it just is not resent inside this attempt.
static bool ShouldResendNow(HttpResponseMessage? response, TimeProvider clock) =>
    IsDocumentedNotProcessed(response)
    && RequestedWait(response!.Headers.RetryAfter!, clock) <= TimeSpan.FromSeconds(5);

static TimeSpan RequestedWait(RetryConditionHeaderValue retryAfter, TimeProvider clock) =>
    retryAfter.Delta ?? (retryAfter.Date!.Value - clock.GetUtcNow());
```

For a client that should never resend a `POST`, `options.Retry.DisableForUnsafeHttpMethods()` on the standard handler is the simpler alternative.

The timeouts bound how long the client waits and keep it from starting new sends; they do not stop the provider from processing a request it already received. That is why a timeout always becomes `Unknown`.

The storage client then maps what it saw into three outcomes: `Completed` when the provider returns the invitation; `NotApplied` when the response is documented as not processed, carrying the provider's requested time as `RetryNotBefore`; and `Unknown` for a timeout (including the outer one firing mid-request), a reset connection, a `503` without `Retry-After`, or any response that does not establish either. An `Unknown` attempt moves the operation to `Unknown`, which the job above refuses to retry. A separate reconciliation step asks the provider what happened before anything else may run.

Reconciliation is only as good as the provider's answers. Listing the folder's invitations resolves the attempt only if a match can be reliably tied to this operation, for example through a client reference the provider stores with the invitation, and not merely to an invitation for the same address that someone created earlier. An empty listing proves `NotApplied` only if the provider guarantees that a request it accepted is visible by the time you look and that no earlier request is still executing. Without those guarantees, the attempt stays `Unknown` and goes to a person; it does not become `NotApplied` because a list came back empty.

If the provider *does* accept an idempotency key, resending the same request with the same key can be safe, but only inside the provider's documented retention window and only with identical parameters. The key must be computed once per attempt, before the pipeline runs, so every resend carries it. A key says nothing about authority freshness: the same key resent at 14:30 still carries whatever decision allowed the attempt, so it is never a substitute for the eligibility check. Nor does any combination of keys and retries make the effect exactly once; it narrows where duplicates can occur, and reconciliation handles the rest. [Idempotency Does Not Prevent Authority Replay](idempotency-does-not-prevent-authority-replay.md#reconciliation-and-what-makes-an-attempt-new) covers keys, unknown outcomes, and reconciliation in detail; the point here is narrower. Whether the evidence is a documented no-effect response or an applicable idempotency guarantee, "the call timed out" never establishes that a resend is safe.

### A Classification That Separates the Two Questions

| What happened | May the same attempt resend? | May a later attempt run? | What must it re-establish first? |
| --- | --- | --- | --- |
| A failure positively classified as before transmission, such as a confirmed DNS-resolution failure or refused connection | Yes; a generic network exception is not enough by itself | Yes | Current eligibility, if the retry can outlive the decision |
| `503` or `429` with `Retry-After`, documented as not processed | Yes, within the attempt's bounded resend window | Yes | Current eligibility, if the retry can outlive the decision |
| An ambiguous outcome, under a provider idempotency key that is still retained | Yes, with the same key and identical parameters | Not until the outcome is known | Nothing new for the resend; the key does not refresh the decision |
| A failure in your own code before the effect, such as a database deadlock reading context | Not applicable: no request was sent | Yes | Current eligibility, which the attempt re-reads anyway |
| Timeout, connection reset, or a `5xx` without a no-effect guarantee | No | Not until reconciled | The outcome of the earlier attempt |
| A documented rejection, such as `400` or the provider's own `403` | No | Only with a different request or a change in the provider's configuration | Usually a new decision, because the request itself would change |
| The authorizer could not answer | Ask the authorizer again; do not send the effect | Yes | A real decision |
| The operation is now denied | No | No | Nothing: the operation is finished, and the denial is recorded |

The left column is the retry policy's question. The right two columns are the questions it cannot answer.

---

## A Refreshed Credential Is Not a Refreshed Decision

One retry looks like authorization but is not. Suppose the 14:30 call returns `401 Unauthorized` because the worker's access token for the storage provider expired during the outage. The worker acquires a new token and resends.

That is correct, and it is a **credential** retry. It answers "can this worker prove its own identity to the provider?" It says nothing about whether the lead may still share this folder with this reviewer. A worker with a fresh, valid service token can send exactly the request that the legal hold forbids.

Keep the two apart in code and in evidence:

- Refreshing the worker's token is transport. It can happen inside one attempt, and it does not create a decision.
- Re-establishing eligibility is authorization. It happens at the start of each attempt that can outlive the earlier decision, and it creates or checks a decision.
- A `403 Forbidden` from the provider normally means the provider refused the worker or this request under its own authorization or policy: a missing service permission, a tenant or resource policy, or a revoked entitlement. Do not treat it as a transient fault unless the provider explicitly documents a retryable condition behind that response. Investigate the provider-side condition before resubmitting; retrying it automatically hides the problem.

The inverse also holds: a request-time authorization decision is not a credential. The lead's 09:14 approval does not give the worker any provider permission, and the worker's provider permission does not mean any particular share is allowed. [How Short-Lived Execution Authority Differs from User Authorization](short-lived-execution-authority-vs-user-authorization.md#which-facts-must-still-be-true-on-friday) explains why the requester's authority and the executor's credentials are different things.

---

## Which Identifiers Stay the Same

Retries are easier to reason about when each identifier means one thing:

| Identifier | Example | Across a resend inside one attempt | Across a later attempt | Across a re-evaluation |
| --- | --- | --- | --- | --- |
| `OperationId` | `OP-7310` | Same | Same | Same: it is still the same requested share |
| `DecisionId` | `DEC-5120`, then `DEC-5188` | Same | New, if the attempt re-evaluates | New; earlier decisions are never rewritten |
| `AttemptId` | `ATT-1`, `ATT-2` | Same | New | Not applicable |
| Idempotency key, when the provider supports one | `invite-OP-7310-a2` | Same; computed once, before the pipeline | New only after the previous attempt is resolved as `NotApplied` | Not applicable |
| Job or message ID | `JOB-88` | Same | Usually same; a resubmission tool may change it | Not applicable |

Two misuses cause most of the trouble. Treating the job ID as the authorization ("the job exists, so the share was approved") replays the decision. Treating a new decision as permission to forget an `Unknown` attempt ("we re-authorized, so try again") replays the effect.

---

## Testing That a Retry Cannot Run Under Stale Authority

These tests have to prove a negative: that the effect did *not* happen. Assert on the fake provider's records, not on a status flag. They are specifications of the behavior described above, not evidence of a working implementation.

The harness stands in for the job library. `RunJobAsync` runs one attempt through the real `storage-invitations` pipeline (two resends after the original request) and returns `JobRunResult.RetryScheduled` when the job throws `RetryableShareException`, the way a job library would record a failed run and schedule the next one.

```csharp
[Fact]
public async Task Retry_after_a_legal_hold_does_not_create_an_invitation()
{
    var provider = new FakeStorageProvider();
    provider.RespondNotProcessed(HttpStatusCode.ServiceUnavailable, retryAfter: TimeSpan.FromSeconds(2), times: 3);
    var harness = SharingHarness.Create(provider);
    string operation = await harness.RequestShareAsync("PRJ-204/contracts", "reviewer@partner.example");

    Assert.Equal(JobRunResult.RetryScheduled, await harness.RunJobAsync(operation));   // the 09:14 outage
    await harness.PlaceLegalHoldAsync("PRJ-204");
    Assert.Equal(JobRunResult.Finished, await harness.RunJobAsync(operation));         // the 14:30 retry

    Assert.Equal(0, provider.InvitationsCreated);
    Assert.Equal(ShareState.Rejected, await harness.StateAsync(operation));
    Assert.Equal(DecisionOutcome.Allowed, (await harness.DecisionsAsync(operation))[0].Outcome);  // DEC-5120 unchanged
    Assert.Equal("project.legal-hold", (await harness.DecisionsAsync(operation))[^1].ReasonCode);
}

[Fact]
public async Task Retry_with_unchanged_facts_creates_one_invitation_under_a_current_decision()
{
    var provider = new FakeStorageProvider();
    provider.RespondNotProcessed(HttpStatusCode.ServiceUnavailable, retryAfter: TimeSpan.FromSeconds(2), times: 3);
    var harness = SharingHarness.Create(provider);
    string operation = await harness.RequestShareAsync("PRJ-204/contracts", "reviewer@partner.example");

    Assert.Equal(JobRunResult.RetryScheduled, await harness.RunJobAsync(operation));
    Assert.Equal(JobRunResult.Finished, await harness.RunJobAsync(operation));

    Assert.Equal(1, provider.InvitationsCreated);
    var decisions = await harness.DecisionsAsync(operation);
    Assert.Equal(decisions[^1].DecisionId, (await harness.AttemptsAsync(operation))[^1].DecisionId);
}

[Fact]
public async Task Timeout_after_the_request_was_sent_is_not_resent()
{
    var provider = new FakeStorageProvider();
    provider.CreateThenDelayResponse(TimeSpan.FromSeconds(12));   // effect happens, response is late
    var harness = SharingHarness.Create(provider);
    string operation = await harness.RequestShareAsync("PRJ-204/contracts", "reviewer@partner.example");

    await harness.RunJobAsync(operation);
    await harness.RunJobAsync(operation);                  // the job library tries again

    Assert.Equal(1, provider.RequestsReceived);
    Assert.Equal(ShareState.Unknown, await harness.StateAsync(operation));
    Assert.True(await harness.IsQueuedForReconciliationAsync(operation));
}

[Fact]
public async Task Unavailable_authorizer_defers_without_calling_the_provider()
{
    var provider = new FakeStorageProvider();
    var harness = SharingHarness.Create(provider);
    string operation = await harness.RequestShareAsync("PRJ-204/contracts", "reviewer@partner.example");
    harness.Authorizer.BecomeUnavailable();

    Assert.Equal(JobRunResult.RetryScheduled, await harness.RunJobAsync(operation));

    Assert.Equal(0, provider.RequestsReceived);
    Assert.Equal(ShareState.Pending, await harness.StateAsync(operation));
}
```

Also cover:

- **Other fact changes.** Removing the lead from the project, or the reviewer's domain from the allowlist, between runs produces no invitation, exactly like the legal hold.
- **Missing `Retry-After`.** A `503` without the header carries no documented no-effect guarantee: it is not resent, and the attempt is `Unknown` unless other evidence establishes that nothing was processed.
- **Long `Retry-After`.** A documented not-processed response whose requested wait exceeds the resend window is not resent inside the attempt; the operation returns to `Pending` with the provider's `NotBefore` time, and no run starts earlier.
- **Changed facts at claim time.** A fact that changes between evaluation and claim leaves the operation `Pending` and schedules another run; the job is not marked finished.
- **Crash after dispatch.** A worker that stops after the provider creates the invitation but before the outcome is recorded leaves the attempt `Unknown`, never `Pending`, and no later run sends again.
- **Inconclusive reconciliation.** An empty or ambiguous invitation listing, without a provider finality guarantee, leaves the attempt `Unknown` and escalates it.
- **Token refresh.** A `401` followed by a successful token refresh resends inside the same attempt, creates no new decision, and does not bypass a legal hold placed before the attempt began.
- **Decision bounds.** When full re-evaluation is not possible, a decision whose `ValidUntil`, policy revision, or any recorded fact version no longer matches leads to a request for a new decision, not to execution and not to another retry.
- **Resubmission.** A job resubmitted from a dead-letter queue a week later is evaluated against current facts, like any other attempt.
- **Concurrency.** Two runs of the same job started at once produce at most one attempt.

[How to Test That a Denied Operation Never Executes](test-denied-operation-never-executes.md) shows the same counting-executor technique for a synchronous denial.

---

## When Ordinary Retry Plus Current Authorization Is Enough

Most retries need nothing more than this article's single rule, and many do not even need that.

**No extra work is needed** when the retry cannot outlive the decision's validity:

- **The user retries.** A failed synchronous request returns an error, and the user clicks the button again. That is a new request, and ASP.NET Core authorization runs again as it does for every request. Running authorization again is not the same as reading current facts, though: if the policy relies on claims cached in a cookie or a long-lived token, those claims can be as old as the session. Policies that must see revocations promptly should read the facts that matter from their authoritative stores.
- **Bounded resends of documented no-effect failures.** An `HttpClient` pipeline that resends a `503` inside a short, bounded window, for a request authorized in the same request, stays within one authorization event, provided the policy accepts that window.
- **System-owned work with no requester.** A nightly cleanup job that runs under its own policy has no earlier user decision to replay; it needs the system's own current rules, which it reads each run.

Read-only calls are a partial exception. Retrying a `GET` cannot create a duplicate effect, so the idempotency half of this article does not apply. The authorization half still does: a read retried after access was revoked can disclose data the caller may no longer see. If a read's retry window can outlive the decision that allowed it, the read needs current authorization too.

**Re-evaluate on every attempt** when a job, message handler, or saga can retry over minutes or longer, and the policy's inputs can be read from authoritative stores at execution time. This is the common case, and it requires no tokens, no capability infrastructure, and no workflow product. It is the `ShareFolderJob` above.

**Carry explicit decision bounds** when the decision cannot be re-evaluated later, such as a human approval or a risk check whose inputs are gone. Bounds can also complement re-evaluation, for example by letting the claim check that the facts the latest evaluation read are still current. Then the decision needs an expiry, the policy revision, and a version for every fact it depended on, and the execution boundary must enforce all of them.

**Narrow, single-use execution authority** becomes worth its cost only when the executor cannot read authoritative state, sits in a different trust boundary, or must be limited to exactly one operation. [Do You Need a Capability Token, or Are Roles and Claims Enough?](roles-claims-or-capability-token-dotnet.md) covers that choice, and most teams that only need safe retries should stop before it.

---

## Failure Modes

### 1. Enqueuing a job as the authorization

The job exists because the request was allowed, so every run of the job assumes it still is. The decision lives as long as the job library keeps retrying.

### 2. Authorizing outside the loop, retrying inside it

A Polly pipeline, a saga step, or a message handler wraps only the effect, while the check runs once above it. A long backoff turns a seconds-old decision into an hours-old one.

### 3. Re-running the policy against the original claims

The job carries the requester's serialized claims or access token and "re-authorizes" with them. It re-reads 09:14, not 14:30, and puts a credential in a queue.

### 4. Treating "cannot determine" as a reason to proceed

The authorizer is down during a retry, and the job proceeds because the original decision was `Allowed`. An unavailable answer is a reason to defer, not a reason to reuse.

### 5. Retrying unsafe requests after a timeout

The default pipeline resends a `POST` after an attempt timeout. The first request may have succeeded, so without an applicable idempotency guarantee the retry can create a second effect under perfectly valid authority.

### 6. Treating a credential refresh as re-authorization

A `401` leads to a new service token and a resend, and the evidence records it as "re-authorized." The requester's eligibility was never checked.

### 7. Rewriting the original decision

The 14:30 denial overwrites `DEC-5120`, so the record now says the request was never allowed. Earlier decisions are history; a re-evaluation adds a new one.

### 8. Unbounded resubmission

A dead-letter "resend all" tool restarts jobs from last month. If eligibility is checked inside each attempt, and unresolved earlier attempts are reconciled first, resubmission does not replay stale authority, though a bulk resend can still overload dependencies. If eligibility is not checked, the resubmission tool is the most powerful authorization bypass in the system.

---

## A Short Review Checklist

**Retry layers**

1. Have you listed every retry layer an operation passes through, including dead-letter resubmission and admin "retry" buttons?
2. Do you know the maximum span of each loop, including backoff, and how the layers multiply?

**Authorization**

3. Is current eligibility established inside every loop whose span can outlive the decision's validity, including retried reads?
4. Does re-evaluation read authoritative stores rather than the original request's claims or tokens?
5. When re-evaluation is not possible, does the decision carry an expiry, a policy revision, and a version for every fact it depended on, and does the execution boundary enforce them?
6. Is "cannot determine" handled as deferral, never as permission and never as a denial of the requester?
7. Are earlier decisions preserved unchanged, with each attempt linked to the decision that allowed it?

**Effects**

8. Does each pipeline that calls an endpoint with side effects resend only outcomes established as safe to repeat, within a bounded total window?
9. Is the attempt claim atomic and durable, and are timeouts, crashes after dispatch, and other ambiguous outcomes recorded as unknown and handed to reconciliation before any further attempt?
10. Is a service-credential refresh kept separate from authorization in both code and evidence?

**Tests**

11. Is there a test that changes an authorization fact between two runs and asserts that the second run produces no effect?

---

## Continue Deeper

If an earlier attempt may already have produced its effect, [Idempotency Does Not Prevent Authority Replay](idempotency-does-not-prevent-authority-replay.md) explains how operation identity, provider keys, replay protection, and reconciliation fit together, and [An Allowed Decision Does Not Mean the Operation Succeeded](allowed-decision-does-not-mean-execution-succeeded.md#retry-or-new-attempt) separates a retry of the same attempt from a materially new one.

If the job carries portable authority, such as a single-use grant, [Replay Protection and Bounded-Use Authority](../../security/replay-protection-and-bounded-use.md) covers atomic consumption and the failure windows around it, and [Capability-Scoped Background Operation](../../case-studies/capability-scoped-background-operation.md#27-operational-retry-versus-authorization-retry) separates operational retries from authorization retries in a longer worked case.

[Delayed Financial Payout and Ambiguous External Commit](../../case-studies/delayed-financial-payout-and-ambiguous-external-commit.md#14-four-kinds-of-retry) follows one payout through delivery retries, execution retries, reconciliation, recovery replay, and authorization retries, with the permission each one needs.

### Related Work

- [Build resilient HTTP apps](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience), Microsoft's documentation for `Microsoft.Extensions.Http.Resilience`, lists the standard handler's defaults and shows how to disable retries for unsafe HTTP methods.
- [Polly's retry strategy documentation](https://www.pollydocs.org/strategies/retry.html) describes how retry predicates, backoff, and delay generators decide whether and when to run the callback again.
- [Timeouts, retries, and backoff with jitter](https://aws.amazon.com/builders-library/timeouts-retries-and-backoff-with-jitter/), from the Amazon Builders' Library, discusses how retries multiply across layers and why retried operations should be safe to repeat.
- [Azure Service Bus dead-letter queues](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-dead-letter-queues) documents maximum delivery counts and resubmitting dead-lettered messages.

---

## The Short Answer

Retry logic and authorization answer different questions on different clocks. A retry policy sees exceptions, status codes, attempt counts, and delays, and it decides whether trying again is worthwhile. It does not see roles, holds, allowlists, or policy revisions, so it is the wrong component to decide whether the operation is still allowed.

Find every retry loop an operation passes through, and put the eligibility check inside each one that can outlive the decision's validity. Re-evaluate against authoritative state when you can; enforce explicit bounds on every fact the decision depended on when you cannot. Resend only what is established as safe to repeat, and reconcile the rest. Keep credential refreshes, re-evaluations, and earlier decisions as separate facts. For a synchronous request, a short resend, or a job that already re-authorizes on every run, that is the whole design.
