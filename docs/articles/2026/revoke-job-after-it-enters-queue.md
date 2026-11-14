---
description: Canceling a queued job races the worker that claims it. Make cancel and claim one conditional write on the job row, and record what happens when cancel loses.
title: Canceling a Queued Job Can Race With Execution
author: Christopher D. Cavell
published: "2026-11-14"
summary: An operator who cancels a queued job is racing the worker that is about to start it. Deleting the queue message or setting a status column does not decide that race, and a worker that reads the status and then writes over it can start a job that was canceled a few milliseconds earlier. Make the claim that starts the job and the cancellation that stops it conditional writes on the same row, so exactly one of them wins. Add a second conditional write before the first effect that cannot be taken back. After the claim, treat cancellation as a request to stop, not a change of state, and record which attempt it lost to and what that attempt went on to do.
feed: true
x_post: Cancel races the claim that starts a queued job. Make both conditional writes on one row so one wins, and record when cancel loses.
x_hashtags:
  - DotNet
  - SoftwareArchitecture
x_question: "Has a job ever run after you canceled it?"
---

# Canceling a Queued Job Can Race With Execution

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** No formal prerequisites. Familiarity with ASP.NET Core, `BackgroundService` or a job library, a message queue, and EF Core is helpful. No AsiBackbone package or prior Learning material is required.

**Sample scope:** The C# targets .NET 8 or later and uses EF Core's `ExecuteUpdateAsync`, available from EF Core 7. Statements about concurrent updates describe PostgreSQL's documented `READ COMMITTED` behavior; confirm the equivalent behavior for your database and isolation level, and prove it with the concurrent test near the end. The CRM, the export, the partner, and the timings are hypothetical. The stores, the builder, the destination client, the watcher, and the test harness are illustrative sketches, not a runnable sample or a recommended persistence API. Original prose is licensed under [Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/), and original code examples are additionally offered under the [MIT License](https://opensource.org/license/mit), as described in the repository's [licensing terms](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md). Third-party material, where identified, remains under its original license. External sources were last verified on November 14, 2026.

**What this article covers:** a cancellation that committed 21 milliseconds before a worker started the job, was overwritten, and was reported as a success; four things that are easy to call "the job" and that cancellation affects differently; why re-reading the status, deleting the message, and disabling the button narrow the window without closing it; a conditional claim that makes the pre-start cutoff explicit, and a cancellation that competes for the same row; a second cutoff before the first irreversible effect; what cancellation can still do after the work has begun; what to record when cancellation loses, and what to tell the operator; recovery after a crash; tests that put the two writers in a real race; and when one conditional status update is the whole design.

**In plain terms:** you phone a print shop to cancel an order of flyers. The receptionist looks at the order board, sees "waiting," crosses it out, and tells you it is canceled. A minute earlier, the press operator lifted that ticket off the board and started making plates. The board and the press were never the same thing. The fix is not a faster receptionist. It is a rule that a ticket moves from the board to the press only when the operator signs it out at the desk where cancellations are also written, so whoever signs first wins. If the operator signed first, the honest answer to your call is "it has started; I've asked them to stop before the run," and later, either "they stopped before printing" or "the run had begun, and here is how many were printed."

Consider a hypothetical multi-tenant CRM. A tenant administrator can export a contact list to a marketing partner's SFTP server. Exports are rate-limited per destination, so the API records the export and puts a message on a queue, and a pool of report workers processes them. Each export builds a CSV file locally, which takes a while for a large list, and then uploads it. A support engineer can cancel a queued export from an operations console.

On a Saturday afternoon, a customer phones support. Their administrator has just queued an export to their old agency, whose contract ended last month, and the list should not go there. Sam, the support engineer, opens the console. Export `EXP-7731` shows **Queued**. Sam clicks **Cancel**.

```text
14:01:55.004  api        EXP-7731 accepted: tenant-acme, 48,112 contacts → partner-old; status Queued; message published
14:02:07.380  worker-3   Received { JobId: EXP-7731 }, message locked for 60 s
14:02:07.386  worker-3   Loaded EXP-7731: status Queued
14:02:07.391  admin-api  Cancel EXP-7731 by sam.ops: UPDATE ... SET status = 'Canceled', canceled_by = 'sam.ops'; 1 row
14:02:07.393  admin-api  Remove queued message for EXP-7731: no matching message available
14:02:07.402  console    "Export EXP-7731 canceled."
14:02:07.412  worker-3   UPDATE ... SET status = 'Running'; 1 row
14:02:07.455  worker-3   Building contacts-2026-11-14.csv: 48,112 rows
14:02:31.861  worker-3   Uploading to sftp.partner-old.example
14:02:32.880  worker-3   Upload complete, 31.4 MB
14:02:32.904  worker-3   UPDATE ... SET status = 'Completed'; 1 row
```

Sam tells the customer the export was canceled. On Monday, the old agency's account manager emails to ask why 48,112 contacts arrived over the weekend. The export's row now says `status = Completed` and `canceled_by = sam.ops`, both true and contradictory, and nobody can tell from it which happened first.

Every database write in that log succeeded. Worker 3 read `Queued` at 14:02:07.386 and wrote `Running` at 14:02:07.412. Sam's cancellation committed in the 26 milliseconds between them and was overwritten. Removing the message found nothing, because the worker already held it, and nobody read that as a warning. The console reported the result of the cancellation's own write, which was true and irrelevant: the question that mattered was whether the export would run, and nothing in the cancellation path asked it.

> **A queued job can be canceled only until something is allowed to start it. Make that moment one conditional write, the claim, and make cancellation a competing conditional write on the same row, so that exactly one of them wins. After the claim, cancellation is a request to stop work already underway. Record it as one, with the attempt it lost to and what that attempt did, and never rewrite a job that ran as one that was canceled.**

Most of that is one `WHERE` clause on each side. The rest of this article is about the parts a single clause does not cover: work that is already running, effects that cannot be recalled, crashes, and what to tell the person who clicked **Cancel**.

---

## Four Things Called "the Job"

Sam's console showed one word, **Queued**, and Sam's mental model had one object: the export. The system had four, with different owners and different answers to "can this still be stopped?"

| What | In the example | Question it answers | Who changes it | What cancellation can do to it |
| --- | --- | --- | --- | --- |
| **Broker delivery** | The message `{ JobId: EXP-7731 }` | Is there work for someone to look at? | The broker and its consumers | Nothing you can rely on. The message may be locked by a consumer, already received, or redelivered later |
| **Current job state** | The `export_jobs` row | What has the system decided about this export? | The API, the console, and the worker, through writes to one row | Change it, if it changes it conditionally and the other writers do too |
| **Authority to start** | The claim that moves the row from `Queued` to `Running` and names an attempt | Which attempt, if any, may begin the work? | Exactly one successful conditional write | Prevent it, but only by winning the same write first |
| **Execution in progress** | Worker 3 building a CSV file, then uploading it | What has actually been done? | The worker and the systems it calls | Ask it to stop. It cannot undo what has already left the process |

The opening incident used the first two as if they were the fourth. Sam's cancellation changed the job state, tried to change the broker delivery, and reported that execution was prevented. The worker, meanwhile, treated a read of the job state as if it were authority to start, and then used a write that ignored what had happened since the read.

### Delivery is not the job

A broker delivers messages. It does not know what a message means, and most brokers do not offer a way to delete one specific message that a producer has already sent. Amazon SQS, for example, [deletes a message by its receipt handle, not by the message ID returned when it was sent](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/APIReference/API_DeleteMessage.html), and a receipt handle exists only after someone receives the message. Azure Service Bus can [cancel a scheduled message by its sequence number](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-amqp-request-response) before its enqueue time, but once a message is active and a receiver holds it in peek-lock mode, [other receivers cannot see it](https://learn.microsoft.com/en-us/azure/service-bus-messaging/message-transfers-locks-settlement), and if the lock is released or expires, the message is delivered again.

So "remove the message" is at best housekeeping. Even where it works, the message may already be inside a worker's process, as it was at 14:02:07.380. Design the worker so that a message is only a hint to go and look at the job, and cancellation never needs to touch the broker to be correct. A message for a canceled job then arrives, finds nothing to do, and is completed.

### Job libraries have the same distinction

Job libraries put the queue and the job state in one product, which makes the distinction easier to miss, not absent. Hangfire, for example, documents [cancellation tokens that tell a running job a shutdown began or its performance was aborted](https://docs.hangfire.io/en/latest/background-methods/using-cancellation-tokens.html). The job's own code still has to check the token, so between a deletion and the job's next check, the dashboard and the running code disagree. Whatever library you use, find out precisely what its delete or cancel operation guarantees for a job that is about to start, one that has started, and one that is in the middle of an external call. Those are three different questions.

---

## Fixes That Narrow the Window Without Closing It

The first reactions to the incident are usually reasonable, and each one makes the race rarer without making it impossible.

**Re-read the status just before starting.** The worker checks again at the last moment. The gap between that read and the write that starts the job is now smaller, but it is still there, and an unconditional `UPDATE ... SET status = 'Running'` still overwrites a cancellation that lands inside it. This is the [time-of-check to time-of-use](https://cwe.mitre.org/data/definitions/367.html) shape, compressed.

**Delete the message as well.** As above, the message may already be held by a consumer, and removing it does not stop a worker that already has it. It also makes cancellation depend on a broker feature that many brokers do not have.

**Disable Cancel once the job is running.** The console reads the status a few seconds before Sam clicks, so the button can be enabled for a job that started in the meantime. The button reflects an old read, not a decision.

**Compare timestamps afterward.** If `canceled_at` is earlier than `started_at`, the cancellation "should have won." Those values come from different hosts' clocks, and even from one clock, the moment a value was computed is not the moment its transaction committed. [Timestamps from different hosts are not a reliable ordering](correlation-id-is-not-distributed-audit-trail.md#what-a-correlation-id-actually-gives-you), and they cannot decide anything after the fact anyway.

**Take a distributed lock around cancel and start.** This can work, but now the lock, its expiry, and every writer's discipline in taking it must all be right, and a worker that pauses past the lock's expiry can still write. The job row already serializes writes to itself. Using it costs nothing extra.

Each of these is trying to make one actor's read stay true until its write. The database can do that directly, if the write carries the condition.

---

## The Cutoff Is a Conditional Write

The claim and the cancellation both move a job out of `Queued`. Write both as conditional updates on the same row, with the condition the other one would violate:

```csharp
// Worker: Queued → Running. The pre-start cutoff.
Guid attemptId = Guid.NewGuid();

int claimed = await db.ExportJobs
    .Where(j => j.Id == jobId
             && j.Status == ExportStatus.Queued
             && j.Version == checkedVersion)     // the request the worker checked, unchanged
    .ExecuteUpdateAsync(set => set
        .SetProperty(j => j.Status, ExportStatus.Running)
        .SetProperty(j => j.AttemptId, attemptId)
        .SetProperty(j => j.ClaimedBy, workerId), ct);

if (claimed == 0)
{
    return;   // canceled, claimed by another delivery, or changed since it was checked: do nothing
}
```

```csharp
// Console: Queued → Canceled. Competes with the claim for the same row.
int canceled = await db.ExportJobs
    .Where(j => j.Id == jobId && j.Status == ExportStatus.Queued)
    .ExecuteUpdateAsync(set => set
        .SetProperty(j => j.Status, ExportStatus.Canceled)
        .SetProperty(j => j.CancellationRequestId, requestId), ct);
```

[`ExecuteUpdateAsync`](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete) sends one `UPDATE` with that `WHERE` clause and returns the number of rows changed. The read and the write are no longer two steps that something else can fall between. They are one statement, and the database decides the order.

In PostgreSQL's default `READ COMMITTED` level, when two transactions update the same row, the second waits for the first. If the first commits, the second [re-evaluates its `WHERE` clause against the updated row](https://www.postgresql.org/docs/current/transaction-iso.html) and proceeds only if the row still matches. In the opening incident, Sam's cancellation commits at 14:02:07.391, before the worker's claim reaches the row. The claim is evaluated against a row whose status is now `Canceled`, matches nothing, and returns `0`. Had the two overlapped, the later one would have waited for the earlier one's commit and then been evaluated the same way. The worker does not build a file. It completes the message and records that the job was not claimable.

Had the worker's claim committed first, the cancellation would have matched nothing, and the console would have learned that the export had already started. Exactly one of the two writes changes the row. Neither can be overwritten by the other, because neither writes without checking. At `REPEATABLE READ` or `SERIALIZABLE`, PostgreSQL can report the loser as a serialization failure instead of zero rows; repeat that statement in a new transaction, and it then matches nothing.

That is the explicit pre-start cutoff. "Canceled before it started" now has a precise meaning: the cancellation's write committed before any claim's write did. It does not depend on clocks, on the broker, or on how quickly anyone clicked.

Three details make the cutoff hold:

- **Every writer uses it.** An administrative screen, an import, a retry button, or a repair script that sets `status = 'Running'` or `status = 'Queued'` without the condition reopens the race. The rule is a system-wide write invariant on the job row, not a convention in one class.
- **The claim names an attempt.** `AttemptId` is written in the same statement as the state change. Every later write by this worker is conditioned on it, so a worker that paused, lost its claim to recovery, and resumed cannot complete or fail a job that another attempt now owns.
- **The claim is conditioned on what was checked.** A worker that re-evaluates the requester's authority before starting, as [A Background Job Should Not Inherit Everything the User Could Do](background-job-should-not-inherit-user-authority.md) recommends, checks a particular version of the request. If someone changes the destination after that check, `Version` no longer matches, and the claim fails instead of starting a job whose terms nobody checked. [Your Security Check Passed. Then the World Changed.](security-check-passed-then-world-changed.md#at-the-moment-of-use-check-then-claim-then-send) develops this check-then-claim pattern and its limits, including facts that live outside the claimed row.

---

## A Second Cutoff Before the Point of No Return

A conditional claim tells you whether cancellation was in time to prevent the job from starting. For many jobs that is the only question, because starting and doing the work are the same thing. The export is different. For its first 24 seconds, it builds a file on the worker's own disk, which can be thrown away. Then it uploads that file to someone else's server, which cannot be called back.

A cancellation that arrives during those 24 seconds has lost the race to start, but it is still in time to prevent the effect that matters. So the export gets a second conditional write, at the boundary between work that can be discarded and work that cannot:

```text
          claim (cutoff 1)            commit to deliver (cutoff 2)
Queued ───────────────────▶ Running ─────────────────────────────▶ Delivering ──▶ Completed
  │                            │                                       │            Failed
  │ cancel                     │ stop requested                        │            OutcomeUnknown
  ▼                            ▼                                       │
Canceled                    Stopped                     (stop requests recorded, not obeyed)
(never started)             (started, nothing delivered)
```

| State | Means | Cancellation can |
| --- | --- | --- |
| `Queued` | Accepted, not yet claimed | Win the race and move it to `Canceled` |
| `Running` | Claimed by an attempt that has done only reversible work | Record a stop request. The attempt must not cross cutoff 2 while one exists |
| `Delivering` | The attempt has committed to the irreversible effect | Only be recorded. The effect may already be in another system |
| `Canceled`, `Stopped`, `Completed`, `Failed`, `OutcomeUnknown` | Final for this attempt | Only be recorded, as too late |

The second cutoff has the same shape as the first:

```csharp
// Running → Delivering, only while this attempt owns the job and nobody has asked it to stop.
int committed = await db.ExportJobs
    .Where(j => j.Id == jobId
             && j.AttemptId == attemptId
             && j.Status == ExportStatus.Running
             && j.StopRequestId == null)
    .ExecuteUpdateAsync(set => set.SetProperty(j => j.Status, ExportStatus.Delivering),
        CancellationToken.None);
```

A stop request is a conditional write on the same row too, `Running` with no earlier stop request, so it and the commit-to-deliver write are serialized exactly as the claim and the cancellation were. Either the stop request commits first and the commit-to-deliver write matches nothing, or the commit-to-deliver write commits first and the stop request finds a job in `Delivering`.

Where to put the second cutoff is a design decision, and it is worth writing down. It belongs immediately before the first effect that you cannot undo, or cannot undo cheaply: the upload, the email, the payment request, the message to another team's system. Work before it should be safe to discard. Work after it should be reported, not abandoned halfway, unless you have decided what a half-finished effect means. If a job has several irreversible steps, such as uploading five files, each step needs either its own commit write or a decision that the whole sequence goes ahead once the first one starts.

The second cutoff does not make cancellation work after delivery has begun. It makes the boundary explicit, so that "too late" has a precise meaning too.

---

## After the Claim, Cancellation Is a Request

Once a job is `Running`, the console cannot stop it by writing a status. Only the worker can stop, and only at places in its code where stopping is safe. .NET's model for this is [cooperative cancellation](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads): the code that requests cancellation signals a token, and the code doing the work decides how and when to respond. Nothing is forced on the listener.

So the worker needs a way to hear the request, and a place to act on it:

```csharp
public sealed class ExportRunner(
    ExportJobs jobs, ExportPolicy policy, IExportBuilder builder,
    IPartnerDestination destination, StopRequestWatcher watcher, WorkerIdentity worker)
{
    public async Task RunAsync(Guid jobId, CancellationToken stoppingToken)
    {
        // The message was only a hint. Everything below starts from the row.
        ExportJob? job = await jobs.FindAsync(jobId, stoppingToken);
        if (job is not { Status: ExportStatus.Queued })
        {
            return;   // canceled, already claimed, finished, or unknown
        }

        ExportDecision decision = await policy.EvaluateAsync(job, stoppingToken);
        if (!decision.IsAllowed)
        {
            await jobs.TryRejectAsync(job.Id, job.Version, decision, stoppingToken);
            return;
        }

        // Cutoff 1.
        Guid attemptId = Guid.NewGuid();
        if (!await jobs.TryClaimAsync(job.Id, job.Version, attemptId, worker.Id, stoppingToken))
        {
            return;
        }

        // Signals `stopToken` when a stop request appears for this attempt, or the attempt loses ownership.
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        await using StopRequestWatch watch = watcher.Watch(job.Id, attemptId, stop);

        ExportFile? file = null;
        try
        {
            // Reversible: a local file. A stop request here costs only the work done so far.
            file = await builder.BuildAsync(job, decision.Scope, stop.Token);

            // Cutoff 2. This, not the watcher, is what guarantees that a stop request
            // committed before it prevents delivery.
            if (!await jobs.TryCommitToDeliverAsync(job.Id, attemptId))
            {
                await jobs.TryFinishStoppedAsync(job.Id, attemptId, StopPoint.BeforeDelivery);
                return;
            }
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            // The watcher asked this attempt to stop while it was still reversible.
            await jobs.TryFinishStoppedAsync(job.Id, attemptId, StopPoint.WhileBuilding);
            return;
        }
        finally
        {
            if (file is not null && !await jobs.IsDeliveringAsync(job.Id, attemptId))
            {
                await file.DeleteAsync();
            }
        }

        // Past the point of no return. Stop requests from here on are recorded, not obeyed.
        // `stoppingToken` still lets the host shut down, which leaves the outcome unknown, not stopped.
        DeliveryResult result = await destination.UploadAsync(file, attemptId, stoppingToken);
        await jobs.TryFinishDeliveredAsync(job.Id, attemptId, result);
    }
}
```

Shutdown and lease handling are omitted here; [Recovery](#recovery-must-not-undo-a-cancellation-or-repeat-a-delivery) below covers what they must do. The details that matter for cancellation:

- **Every write after the claim is conditioned on `attemptId`.** `TryFinishStoppedAsync` moves `Running` to `Stopped` only for this attempt. `TryFinishDeliveredAsync` moves `Delivering` to `Completed`, `Failed`, or `OutcomeUnknown` only for this attempt.
- **The watcher is for speed, not correctness.** It can poll the row every few seconds for a stop request, or subscribe to a notification and then confirm against the row. Either way, it lets a stopped export give up after three seconds instead of 24. If it misses the request, or the notification is lost, cutoff 2 still refuses to deliver. Do not make a notification the only thing standing between a stop request and an upload.
- **Stopping is the worker's decision about its own work.** `BuildAsync` passes the token down to the database query and the file writes, so they end promptly, and removes its partial output when it is canceled. The `finally` block removes a finished file that was never delivered. A `Stopped` export leaves no file behind.
- **The upload does not take the stop token.** Aborting an SFTP upload halfway leaves a partial file on the partner's server, which is worse than either outcome. If the host shuts down during the upload, the attempt ends with an outcome that nobody observed, and it is recorded as `OutcomeUnknown`, not `Stopped`. [A Payment API Timeout Leaves the Outcome Unknown](payment-api-timeout-unknown-outcome.md) covers why an interrupted external call needs its own state and is resolved only by evidence.

Some effects do support cancellation after they begin: a provider's API to cancel a scheduled payout, a print queue that can drop a job before the run, a long database operation that rolls back. If yours does, a stop request in `Delivering` can turn into a call to that API, and that system's answer, not your intention, decides the outcome. Record the call and its answer as their own operation. [An Allowed Decision Does Not Mean the Operation Succeeded](allowed-decision-does-not-mean-execution-succeeded.md#compensation-is-another-operation) explains why undoing an effect is a new operation with its own decision, not a rewrite of the old one.

---

## When Cancellation Loses, Record That It Lost

A cancellation request is an event with its own identity, whatever it achieves. Record it every time, in the same transaction as the write it attempted, with what it found:

```csharp
public async Task<CancellationResult> RequestCancellationAsync(
    Guid jobId, OperatorId requestedBy, string reason, CancellationToken ct)
{
    await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
    Guid requestId = Guid.NewGuid();

    // Before cutoff 1: competes with the claim.
    int canceled = await db.ExportJobs
        .Where(j => j.Id == jobId && j.Status == ExportStatus.Queued)
        .ExecuteUpdateAsync(set => set
            .SetProperty(j => j.Status, ExportStatus.Canceled)
            .SetProperty(j => j.CancellationRequestId, requestId), ct);

    // Between the cutoffs: competes with the commit to deliver.
    int stopRequested = canceled == 1 ? 0 : await db.ExportJobs
        .Where(j => j.Id == jobId && j.Status == ExportStatus.Running && j.StopRequestId == null)
        .ExecuteUpdateAsync(set => set.SetProperty(j => j.StopRequestId, requestId), ct);

    // What the request found. After a write that matched nothing, this reads the row as it now stands.
    // If recovery can return an expired attempt to Queued, repeat the request when it finds Queued;
    // the sketch omits that loop.
    ExportJobSnapshot found = await db.ExportJobs
        .Where(j => j.Id == jobId)
        .Select(j => new ExportJobSnapshot(j.Status, j.AttemptId, j.ClaimedBy, j.StopRequestId))
        .SingleAsync(ct);

    CancellationOutcome outcome =
        canceled == 1 ? CancellationOutcome.CanceledBeforeStart :
        stopRequested == 1 ? CancellationOutcome.StopRequested :
        found.Status == ExportStatus.Canceled ? CancellationOutcome.AlreadyCanceled :
        found.Status == ExportStatus.Running ? CancellationOutcome.StopAlreadyRequested :
        CancellationOutcome.TooLate;

    db.CancellationRequests.Add(new CancellationRequest(
        requestId, jobId, requestedBy, reason, outcome,
        FoundStatus: found.Status,
        LostToAttemptId: canceled == 1 ? null : found.AttemptId));

    await db.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);

    return new CancellationResult(requestId, outcome, found);
}
```

Keep this transaction short. While it holds the row, a worker's claim on the same row waits for it, which is what makes the race decidable, and also why a slow operation inside this transaction would hold up workers.

The request record is never edited to look like a different outcome, and the job is never rewritten from `Completed` to `Canceled` because someone wished it had been. The attempt's own terminal write links back to the stop request it honored, if any, so the full story is two kinds of record that agree:

| Sam clicks at | What wins | Job ends as | Cancellation request records | The console says |
| --- | --- | --- | --- | --- |
| 14:02:07.391 | The cancellation, by 21 ms | `Canceled` | `CanceledBeforeStart` | "Canceled before it started. Nothing was built or sent." |
| 14:02:07.430 | The claim; then the stop request beats cutoff 2 | `Stopped`, while building | `StopRequested`, lost to attempt `5c2e…` on worker 3 | "Already started at 14:02:07.412. Stopped at 14:02:10.452, before delivery. Nothing was sent." |
| 14:02:31.880 | The claim, and then cutoff 2 at 14:02:31.861 | `Completed` | `TooLate`, lost to attempt `5c2e…`, found `Delivering` | "Too late: delivery to partner-old began at 14:02:31.861 and completed at 14:02:32.904. 48,112 contacts were sent. Your request CXL-303 is recorded." |

The first row is the opening incident with conditional writes, and nothing else changed. The third row is not a failure of the design. It is the design telling the truth: a cancellation 19 milliseconds after the point of no return cannot recall a file from someone else's server, and the operator who clicked it needs to know that now, not on Monday. What to do about the file, asking the old agency to delete it and recording whether they confirm, is a new operation with its own owner.

The evidence for the second case reads like this:

```text
Requested   EXP-7731  by admin@acme (tenant-acme)      14:01:55.004  partner-old, 48,112 contacts, v1
Claimed     EXP-7731  attempt 5c2e…  by worker-3      14:02:07.412  checked v1, decision allowed
Cancel      CXL-302   EXP-7731  by sam.ops            14:02:07.430  "wrong destination"
                      outcome StopRequested; lost to attempt 5c2e… (found Running)
Stopped     EXP-7731  attempt 5c2e…  by worker-3      14:02:10.452  while building, for CXL-302
                      local file deleted; nothing delivered
```

The times are each host's own readings, recorded for people. The order that matters, which write won, is already settled by the row, and the records say so in words: "lost to attempt `5c2e…`," "for CXL-302."

### What the API returns

The cancel endpoint should return the outcome, not an echo of the request. One reasonable mapping:

| Outcome | Response | Body includes |
| --- | --- | --- |
| `CanceledBeforeStart`, `AlreadyCanceled` | `200 OK` | The request ID and that the job never started |
| `StopRequested`, `StopAlreadyRequested` | `202 Accepted` | The request ID, the attempt it lost to, and a status URL; the final answer is not known yet |
| `TooLate` | `409 Conflict` | The request ID, the job's current state, and when delivery began |

Whatever the status codes, the console text should never say "canceled" for anything except the first row. "Cancellation requested" and "too late" are different facts, and the operator will repeat whichever one the screen shows to a customer.

---

## Recovery Must Not Undo a Cancellation or Repeat a Delivery

A worker can crash, or pause long enough to be presumed dead, in any state. A production design gives each attempt a lease, renewed while it works, and a recovery process that takes over jobs whose lease has expired, with a conditional write on the old `AttemptId` so that the original worker, if it resumes, finds it no longer owns anything. [An In-Memory Grant Store Is Not Distributed Replay Protection](in-memory-grant-store-is-not-distributed-replay-protection.md#recovery-and-the-original-executor-share-one-ownership-protocol) works through that ownership protocol and the timing values it depends on.

Cancellation adds three rules to recovery:

- **A `Running` attempt with a stop request is finished, not restarted.** `Running` means cutoff 2 was never crossed, so nothing was delivered. Recovery moves it to `Stopped`, conditionally on the expired attempt, and records that it did. Without this rule, a crash at the wrong moment turns a stop request into a fresh attempt that nobody asked for.
- **A `Running` attempt without one may be returned to `Queued`**, with the old attempt fenced off. It is then ordinary queued work again, and a cancellation that arrives afterward competes with the next claim as usual.
- **A `Delivering` attempt is not stopped, canceled, or retried blindly.** The upload may have happened. Recovery finds out from the destination, for example by looking for the attempt's file name, and records `Completed` or `OutcomeUnknown` from what it finds. A stop request on that job is still `TooLate`. Retrying the delivery is a separate decision, and it should not be made by recovery merely because the attempt is old.

Recovery is also a writer to the job row, so it follows the same invariant as everything else: no unconditional status changes.

---

## Testing the Race, Not Just the Rule

Tests that call cancel and then run the worker, one after the other, prove the rule and miss the race. The race needs two writers in flight against the real database engine: a fake that serializes calls, or a check that runs in one process, cannot show what happens when two transactions contend for one row. These tests are specifications of the behavior described above, not evidence of a working implementation, and each shows the behavior only under the conditions it sets up.

```csharp
[Fact]
public async Task Cancellation_between_load_and_claim_prevents_the_export()
{
    await using var harness = await ExportHarness.CreateAsync();   // a real PostgreSQL instance
    Guid job = await harness.QueueExportAsync(contacts: 48_112, destination: "partner-old");

    // Reproduce the incident's interleaving deterministically.
    harness.Worker.AfterLoad = () => harness.Console.CancelAsync(job, "sam.ops", "wrong destination");
    await harness.Worker.ProcessNextMessageAsync();

    Assert.Equal(ExportStatus.Canceled, await harness.StatusAsync(job));
    Assert.Empty(harness.Destination.Uploads);
    Assert.Empty(harness.Worker.FilesBuilt);
    Assert.Equal(CancellationOutcome.CanceledBeforeStart, (await harness.CancellationsAsync(job)).Single().Outcome);
}

[Fact]
public async Task Cancel_and_claim_racing_for_one_job_produce_exactly_one_winner()
{
    for (int run = 0; run < 500; run++)
    {
        await using var harness = await ExportHarness.CreateAsync();
        Guid job = await harness.QueueExportAsync();
        var go = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<bool> claim = Task.Run(async () => { await go.Task; return await harness.Worker.TryClaimAsync(job); });
        Task<CancellationResult> cancel = Task.Run(async () =>
        {
            await go.Task;
            return await harness.Console.CancelAsync(job, "sam.ops", "race");
        });

        go.SetResult();
        bool claimed = await claim;
        CancellationResult result = await cancel;

        // Exactly one of the two writes changed the row out of Queued.
        Assert.NotEqual(claimed, result.Outcome == CancellationOutcome.CanceledBeforeStart);

        ExportJobSnapshot row = await harness.SnapshotAsync(job);
        if (claimed)
        {
            Assert.Equal(ExportStatus.Running, row.Status);
            Assert.Equal(CancellationOutcome.StopRequested, result.Outcome);
            Assert.Equal(row.AttemptId, result.Found.AttemptId);   // it names the attempt it lost to
        }
        else
        {
            Assert.Equal(ExportStatus.Canceled, row.Status);
            Assert.Null(row.AttemptId);
        }
    }
}

[Fact]
public async Task Stop_request_before_the_commit_to_deliver_sends_nothing()
{
    await using var harness = await ExportHarness.CreateAsync();
    Guid job = await harness.QueueExportAsync();
    harness.Worker.Watcher.Disable();   // prove cutoff 2 holds even if the watcher never fires

    harness.Worker.BeforeCommitToDeliver = () => harness.Console.CancelAsync(job, "sam.ops", "late");
    await harness.Worker.ProcessNextMessageAsync();

    Assert.Equal(ExportStatus.Stopped, await harness.StatusAsync(job));
    Assert.Empty(harness.Destination.Uploads);
    Assert.Empty(harness.Worker.FilesOnDisk);
}

[Fact]
public async Task Cancellation_after_delivery_began_is_recorded_as_too_late_and_changes_nothing()
{
    await using var harness = await ExportHarness.CreateAsync();
    Guid job = await harness.QueueExportAsync();

    CancellationResult? result = null;
    harness.Destination.DuringUpload = async () => result = await harness.Console.CancelAsync(job, "sam.ops", "late");
    await harness.Worker.ProcessNextMessageAsync();

    Assert.Equal(CancellationOutcome.TooLate, result!.Outcome);
    Assert.Equal(ExportStatus.Delivering, result.Found.Status);
    Assert.Equal(ExportStatus.Completed, await harness.StatusAsync(job));   // not rewritten to Canceled
    Assert.Single(harness.Destination.Uploads);
}
```

The concurrent test is probabilistic: it shows that whenever the two writers overlap, the result is consistent, and the 500 runs make overlap likely. The deterministic tests pin the interleavings that matter, using hooks at the points where the incident's gaps were.

Also cover:

- **Redelivery.** The message for a canceled job, delivered again after the lock expires, does nothing and is completed. So does a message for a `Stopped`, `Completed`, or unknown job.
- **A changed request.** Changing the destination after the worker's policy check makes the claim fail on `Version`, and nothing is built.
- **Every writer.** Each path that can change `Status`, including administrative and repair paths, refuses to move a `Canceled` job back to `Queued` or `Running`. An architecture test can fail the build when code outside the job store writes the status column.
- **A paused attempt.** After recovery takes over an expired attempt, the original worker's `TryCommitToDeliverAsync` and `TryFinishDeliveredAsync` match nothing.
- **Recovery and stop requests.** An expired `Running` attempt with a stop request ends `Stopped` and is not requeued. An expired `Delivering` attempt is looked up at the destination, not restarted, and a cancellation request against it records `TooLate`.
- **Shutdown during upload.** The attempt ends `OutcomeUnknown`, not `Stopped` or `Failed`.
- **The console.** The text for `StopRequested` and `TooLate` never contains the word "canceled" on its own.

[How to Test That a Denied Operation Never Executes](test-denied-operation-never-executes.md) develops the technique of asserting that an effect never happened, which most of these tests rely on.

---

## When One Conditional Update Is the Whole Design

Most jobs do not need two cutoffs, a watcher, and a stop-request protocol. Choose the first row that fits:

| Shape | What cancellation needs |
| --- | --- |
| **The job runs within seconds, or nobody needs to cancel it** | Possibly nothing. If the product does offer cancel, the conditional claim and conditional cancel below cost two `WHERE` clauses |
| **Cheap, repeatable work with no external effect**, such as rebuilding a cached report | A conditional claim and a conditional cancel. After the claim, report "already running" and let it finish. No second cutoff, because there is no point of no return |
| **Reversible preparation followed by an irreversible effect**, like this export | Both cutoffs, a stop request on `Running`, a watcher if early stopping is worth the effort, and records of each cancellation's outcome |
| **The effect lives in a system that can cancel it**, such as a provider-scheduled payout | Cancellation calls that system, and its answer decides. Record the call as its own operation |
| **The executor holds operation-specific authority across a trust boundary** | Revocation and claim contend on the authority's own state, not only on the job row. [Capability-Scoped Background Operation](../../case-studies/capability-scoped-background-operation.md#15-revocation-is-useful-but-has-a-boundary) defines that race, and [An In-Memory Grant Store Is Not Distributed Replay Protection](in-memory-grant-store-is-not-distributed-replay-protection.md) covers the shared, durable, atomic consumption it depends on |

The conditional claim is worth having even when nobody can cancel anything, because duplicate deliveries race in exactly the same way. If two copies of the same message reach two workers, the same `WHERE j.Status == ExportStatus.Queued` that makes cancellation decidable also makes exactly one of them start.

---

## Failure Modes

**1. A read and an unconditional write.** The worker checks `Queued`, then writes `Running`. A cancellation that commits in between is overwritten, and the row ends up saying both.

**2. Cancellation that only touches the broker.** Deleting or purging the message is treated as stopping the job. The message may already be inside a worker, and many brokers cannot remove a specific active message at all.

**3. A console that reports its own write.** "Canceled" means the cancellation's `UPDATE` affected one row, not that the job will not run. If the write was unconditional, it means even less.

**4. One writer that ignores the invariant.** A retry button, import, or repair script sets the status without a condition and resurrects a canceled job.

**5. Deciding the winner by timestamp.** `canceled_at < started_at` is read as "the cancellation should have won." The values come from different clocks and were computed before their transactions committed.

**6. No boundary before the irreversible effect.** A stop request that arrives during a long, reversible build is ignored until the job ends, because the only cutoff was the claim.

**7. A watcher trusted for correctness.** The stop notification is lost, or the poll interval is longer than the remaining build, and nothing else prevents the upload.

**8. Rewriting history.** A job that delivered is marked `Canceled` because someone requested cancellation, or a lost request is deleted because it "didn't do anything." Either way, the record no longer says what happened.

**9. Recovery that restarts what someone asked to stop.** A crashed `Running` attempt with a stop request is requeued as if nothing had been asked, or a `Delivering` attempt is retried without finding out whether its delivery happened.

**10. Treating an interrupted effect as stopped.** An upload aborted by shutdown is recorded as `Stopped` or `Failed`, when nobody knows whether the partner received the file.

---

## A Short Review Checklist

**The race**

1. Is the claim that starts a job a single conditional write, with the job's state and the checked version in its condition?
2. Is cancellation a conditional write on the same row, so that it and the claim cannot both succeed?
3. Does every writer of the job's status, including administrative and repair paths, use a condition?
4. Is the order decided by the database, not by timestamps from different hosts?

**The work in progress**

5. Is there a second conditional write immediately before the first effect that cannot be undone, and is it conditioned on the attempt and on the absence of a stop request?
6. Is the work before that boundary safe to discard, and is it cleaned up when the attempt stops?
7. If a watcher stops work early, does correctness still hold when the watcher never fires?
8. Is an interrupted external effect recorded as unknown, rather than stopped or failed?

**The records**

9. Does every cancellation request get its own record, with what it found and the attempt it lost to?
10. Is a job that ran never rewritten as canceled, and is a lost request never deleted?
11. Does the console distinguish "canceled before it started," "stop requested," and "too late," and say what was delivered when it was too late?
12. Is anything done about an effect that already happened, such as asking a recipient to delete a file, recorded as a separate operation?

**Recovery and proportion**

13. Does recovery finish a `Running` attempt with a stop request as stopped, and look up a `Delivering` attempt instead of retrying it?
14. Does the design match the job? Two `WHERE` clauses may be all it needs.

---

## Continue Deeper

For what a queued job should carry, how the worker re-checks the requester's authority before it claims, and why delivery is not authority, read [A Background Job Should Not Inherit Everything the User Could Do](background-job-should-not-inherit-user-authority.md). [Your Security Check Passed. Then the World Changed.](security-check-passed-then-world-changed.md) develops the check-then-claim pattern in this article and covers the facts that a conditional write on one row cannot see.

When the job carries a one-time grant rather than relying on a job row, cancellation becomes revocation, and it contends with consumption of the grant. [An In-Memory Grant Store Is Not Distributed Replay Protection](in-memory-grant-store-is-not-distributed-replay-protection.md) explains why that consumption has to be shared, durable, and atomic across instances, and [Capability-Scoped Background Operation](../../case-studies/capability-scoped-background-operation.md#15-revocation-is-useful-but-has-a-boundary) defines what happens when revocation and claim race for the same grant.

If cancellation loses after an external call has begun, [A Payment API Timeout Leaves the Outcome Unknown](payment-api-timeout-unknown-outcome.md) covers how to resolve an outcome that nobody observed, and [Your Retry Logic May Be Replaying an Authorization Decision](retry-logic-may-replay-authorization-decision.md) covers what each retry and recovery layer can and cannot know.

### Related Work

- [Transaction Isolation](https://www.postgresql.org/docs/current/transaction-iso.html), in the PostgreSQL documentation, describes how `UPDATE` waits for a concurrent update and re-evaluates its `WHERE` clause in `READ COMMITTED` mode.
- [ExecuteUpdate and ExecuteDelete](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete), in the EF Core documentation, covers set-based conditional updates and their row counts.
- [Cancellation in Managed Threads](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads) describes .NET's cooperative cancellation model.
- [Message transfers, locks, and settlement](https://learn.microsoft.com/en-us/azure/service-bus-messaging/message-transfers-locks-settlement), in the Azure Service Bus documentation, explains peek-lock, lock expiry, and redelivery, and [AMQP request-response operations](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-amqp-request-response) documents canceling scheduled messages by sequence number.
- [DeleteMessage](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/APIReference/API_DeleteMessage.html), in the Amazon SQS API reference, deletes a message by receipt handle.
- [Using cancellation tokens](https://docs.hangfire.io/en/latest/background-methods/using-cancellation-tokens.html), in the Hangfire documentation, describes how a running job learns of shutdown or an aborted performance.
- [CWE-367: Time-of-check Time-of-use (TOCTOU) Race Condition](https://cwe.mitre.org/data/definitions/367.html) describes the general weakness behind the opening incident.

---

## The Short Answer

Canceling a queued job is a race with the worker that is about to start it, and neither the queue message nor a status column decides that race on its own. A worker that reads `Queued` and then writes `Running` can start a job that was canceled in between, and a console that reports its own write will say "canceled" anyway.

Make starting and canceling conditional writes on the same row. The claim moves `Queued` to `Running` only if the job is still queued and unchanged, and names an attempt. The cancellation moves `Queued` to `Canceled` only if the job is still queued. The database lets exactly one of them succeed, and that is the pre-start cutoff. If the job does reversible work before an effect that cannot be undone, add a second conditional write at that boundary, which a stop request can still beat.

After the claim, cancellation is a request, not a state. Record every request with what it found and the attempt it lost to, let the worker stop only where stopping is safe, and tell the operator plainly whether the job never started, stopped before it did anything, or had already done it. For many jobs, that is two `WHERE` clauses and one honest message.
