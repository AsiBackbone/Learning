---
description: Canceling a queued job races the worker that claims it. Make cancel and claim competing conditional writes on one row, and record it when cancel loses.
title: Canceling a Queued Job Can Race With Execution
author: Christopher D. Cavell
published: "2026-11-14"
summary: An operator who cancels a queued job is racing the worker that is about to start it. Deleting the queue message or setting a status column does not decide that race, and a worker that reads the status and then writes over it can start a job that was canceled a few milliseconds earlier. Make the claim that starts the job and the cancellation that stops it competing conditional writes on the same row, so at most one of them can move the job out of the queue. Add a second conditional write before the first effect that cannot be taken back. After the claim, treat cancellation as a request to stop, not a change of state, and record what each request found and what the attempt it lost to went on to do.
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

**Sample scope:** The C# targets .NET 8 or later and uses EF Core's `ExecuteUpdateAsync` and `FromSql`, available from EF Core 7. Statements about concurrent updates describe PostgreSQL's documented behavior. Statements about Amazon SQS, Azure Service Bus, and Hangfire describe their documentation on the verification date below. Database, broker, and job-library behavior varies by product, version, and configuration, so confirm it for yours, and prove the database behavior with the contention test near the end. The CRM, the export, the partner, and the timings are hypothetical. The stores, the builder, the destination client, the watcher, and the test harness are illustrative sketches, not a runnable sample or a recommended persistence API. Authorization of the operator's request, lease renewal, and orphan-file cleanup are omitted. Original prose is licensed under [Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/), and original code examples are additionally offered under the [MIT License](https://opensource.org/license/mit), as described in the repository's [licensing terms](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md). Third-party material, where identified, remains under its original license. External sources were last verified on November 14, 2026.

**What this article covers:** a cancellation that committed 21 milliseconds before a worker's claim, was overwritten, and was reported as a success; four things that are easy to call "the job" and that cancellation affects differently; why re-reading the status, deleting the message, and disabling the button narrow the window without closing it; a conditional claim that makes the pre-start cutoff explicit, and a cancellation that competes for the same row; a second cutoff before the first irreversible effect; what cancellation can still do after the work has begun; what to record when cancellation loses, and what to tell the operator; recovery after a crash; tests that hold one writer's transaction open while the other waits; and when one conditional status update is the whole design.

**In plain terms:** you phone a print shop to cancel an order of flyers. The receptionist looks at the order board, sees "waiting," crosses it out, and tells you it is canceled. A minute earlier, the press operator took that ticket off the board and started making plates. The board and the press were never the same thing. The fix is not a faster receptionist. It is a rule that the operator must sign a ticket out at the desk before making any plates, and that cancellations are written at that same desk, so whichever signature comes first wins. If the operator signed first, the honest answer to your call is "it has started; I've asked them to stop before the run," and later, either "they stopped before printing" or "the run had already been approved, and here is what was printed."

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

> **A queued job can be canceled only until something is allowed to start it. Make that moment one conditional write, the claim, and make cancellation a competing conditional write on the same row, so that at most one of them can move the job out of the queue. After the claim, cancellation is a request to stop work already underway. Record it as one, with what it found and the attempt it lost to, and never rewrite a job that ran as one that was canceled.**

Most of that is one `WHERE` clause on each side. The rest of this article is about the parts a single clause does not cover: work that is already running, effects that cannot be recalled, crashes, and what to tell the person who clicked **Cancel**.

---

## Four Things Called "the Job"

Sam's console showed one word, **Queued**, and Sam's mental model had one object: the export. The system had four, with different owners and different answers to "can this still be stopped?"

| What | In the example | Question it answers | Who changes it | What cancellation can do to it |
| --- | --- | --- | --- | --- |
| **Broker delivery** | The message `{ JobId: EXP-7731 }` | Is there work for someone to look at? | The broker and its consumers | Nothing you can rely on. The message may be locked by a consumer, already received, or redelivered later |
| **Current job state** | The `export_jobs` row | What has the system decided about this export? | The API, the console, the worker, and recovery, through writes to one row | Change it, if it changes it conditionally and the other writers do too |
| **Authority to start** | The claim that moves the row from `Queued` to `Running` and names an attempt | Which attempt, if any, may begin the work? | One successful conditional write | Prevent it, but only by committing first |
| **Execution in progress** | Worker 3 building a CSV file, then uploading it | What has actually been done? | The worker and the systems it calls | Ask it to stop. It cannot undo what has already left the process |

The opening incident used the first two as if they were the fourth. Sam's cancellation changed the job state, tried to change the broker delivery, and reported that execution was prevented. The worker, meanwhile, treated a read of the job state as if it were authority to start, and then used a write that ignored what had happened since the read.

### Delivery is not the job

A broker delivers messages. It does not know what a message means, and you should not assume that your broker lets a producer find and delete one particular message by your job ID. Two widely used brokers show why.

In Amazon SQS, [`DeleteMessage` identifies a message by the receipt handle from the most recent receive](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/APIReference/API_DeleteMessage.html), so only a party that has received the message can delete it. The handle holder can delete it even while the message is hidden from other consumers by a visibility timeout, but a console that only knows the job ID has no handle. The same page notes that a standard queue can, on rare occasions, return a message after it has been deleted. Azure Service Bus lets a sender [cancel a scheduled message by its sequence number](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-amqp-request-response) before it is enqueued. Once an active message is held under a [peek-lock](https://learn.microsoft.com/en-us/azure/service-bus-messaging/message-transfers-locks-settlement), competing receivers cannot receive it, and if the lock is released or expires, the message is delivered again.

So "remove the message" is housekeeping at best. Even where it works, the message may already be inside a worker's process, as it was at 14:02:07.380. Design the worker so that a message is only a hint to go and look at the job, and so that cancellation never needs to touch the broker to be correct. A message for a canceled job then arrives, finds nothing to do, and is settled.

This also settles a practical question about long jobs. Once the worker's claim on the row has committed, the row carries the work, so the worker can settle the message right away instead of renewing its lock for the length of the build. A redelivered copy would only reach the conditional claim and do nothing. If recovery later returns the job to `Queued`, it publishes a new message, through an outbox in the same transaction, because the original one is gone.

### Job libraries have the same distinction

Job libraries put the queue and the job state in one product, which makes the distinction easier to miss, not absent. Hangfire, for example, [gives a running job a cancellation token](https://docs.hangfire.io/en/latest/background-methods/using-cancellation-tokens.html) that is signaled on server shutdown or when the job's processing is aborted. A background process detects aborted jobs by polling job storage, every five seconds by default, and the job's own code still has to check the token. So between a state change in storage and the job's next check, the dashboard and the running code disagree. Whatever library you use, find out precisely what its delete or cancel operation guarantees for a job that is about to start, one that has started, and one that is in the middle of an external call. Those are three different questions.

---

## Fixes That Narrow the Window Without Closing It

The first reactions to the incident are usually reasonable, and each one makes the race rarer without making it impossible.

**Re-read the status just before starting.** The worker checks again at the last moment. The gap between that read and the write that starts the job is now smaller, but it is still there, and an unconditional `UPDATE ... SET status = 'Running'` still overwrites a cancellation that lands inside it. This is the [time-of-check to time-of-use](https://cwe.mitre.org/data/definitions/367.html) shape, compressed.

**Delete the message as well.** As above, the message may already be held by a consumer, and removing it does not stop a worker that already has it.

**Disable Cancel once the job is running.** The console reads the status a few seconds before Sam clicks, so the button can be enabled for a job that started in the meantime. The button reflects an old read, not a decision.

**Compare timestamps afterward.** If `canceled_at` is earlier than `started_at`, the cancellation "should have won." Those values come from different hosts' clocks, and even from one clock, the moment a value was computed is not the moment its transaction committed. [Timestamps from different hosts are not a reliable ordering](correlation-id-is-not-distributed-audit-trail.md#what-a-correlation-id-actually-gives-you), and they cannot decide anything after the fact anyway.

**Take a distributed lock around cancel and start.** A lock alone does not stop a holder that pauses past the lock's expiry from writing afterward. Making it safe needs a fencing token: a number that increases with each grant of the lock, which the protected resource checks so that it refuses writes carrying an older token. The job row can play both parts. Its conditional writes serialize cancel and start, and the `AttemptId` introduced below fences later writes from an attempt that has lost ownership. That avoids a separate lock service, although row contention and transaction length still matter.

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
             && j.Version == checkedVersion)     // the terms the worker's policy check evaluated
    .ExecuteUpdateAsync(set => set
        .SetProperty(j => j.Status, ExportStatus.Running)
        .SetProperty(j => j.AttemptId, attemptId)
        .SetProperty(j => j.AttemptCount, j => j.AttemptCount + 1)
        .SetProperty(j => j.ClaimedBy, workerId), ct);

if (claimed == 0)
{
    return;   // canceled, claimed by another delivery, or changed since it was checked: do nothing
}
```

```csharp
// Console: Queued → Canceled. Competes with the claim for the same row.
// No version condition: a queued job may be canceled whatever its current terms.
int canceled = await db.ExportJobs
    .Where(j => j.Id == jobId && j.Status == ExportStatus.Queued)
    .ExecuteUpdateAsync(set => set
        .SetProperty(j => j.Status, ExportStatus.Canceled)
        .SetProperty(j => j.CancellationRequestId, requestId), ct);
```

[`ExecuteUpdateAsync`](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete) sends one `UPDATE` with that `WHERE` clause and returns the number of rows changed. The read and the write are no longer two steps that something else can fall between. They are one statement, and the database decides the order. (The redesigned row also drops `canceled_by`. It records the request that canceled the job, `CancellationRequestId`, and keeps every request, including those that changed nothing, in a separate table described below.)

In PostgreSQL's default `READ COMMITTED` level, when two transactions update the same row, the second waits for the first. If the first commits, the second [re-evaluates its `WHERE` clause against the updated row](https://www.postgresql.org/docs/current/transaction-iso.html) and proceeds only if the row still matches. In the opening incident, Sam's cancellation commits at 14:02:07.391, before the worker's claim reaches the row. The claim is evaluated against a row whose status is now `Canceled`, matches nothing, and returns `0`. Had the two overlapped, the later one would have waited for the earlier one's commit and then been evaluated the same way. The worker does not build a file. It settles the message and records that the job was not claimable.

Had the worker's claim committed first, the cancellation would have matched nothing, and the console would have learned that the export had already started. At most one transition out of `Queued` can commit, and neither write can overwrite the other, because neither writes without checking. That is not the same as saying one of the two always wins: a version mismatch, a rollback, or another writer can leave both with nothing.

At `REPEATABLE READ` or `SERIALIZABLE`, PostgreSQL can instead fail the second transaction with a serialization error. PostgreSQL's guidance is to [retry the complete transaction, including the logic that decided which statements to run](https://www.postgresql.org/docs/current/mvcc-serialization-failure-handling.html), not just the failed statement. The retry reads the job as it is then, and its result depends on that state; it is not guaranteed to change nothing.

That is the explicit pre-start cutoff. "Canceled while queued" now has a precise meaning: the cancellation's write committed while the row was `Queued`, before any claim's write did. It does not depend on clocks, on the broker, or on how quickly anyone clicked. The guarantee also has preconditions worth stating:

- **Both writers decide on the same row in the same primary database.** A status read from a replica or a cache can inform a screen, never a claim or a cancellation.
- **Work starts only after the claim has committed.** Here each `ExecuteUpdateAsync` runs as its own statement, and [EF Core does not start a transaction around it implicitly](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete), so a return of `1` means the claim is already durable. If you put the claim inside a larger transaction, the row count says only that the claim will happen if that transaction commits. Begin the work after the commit, and keep such transactions short, because a transaction held open by a paused process holds the row too.
- **Every writer uses a condition.** An administrative screen, an import, a retry button, a recovery job, or a repair script that sets `status = 'Running'` or `status = 'Queued'` without one reopens the race. This is a system-wide write invariant on the job row, not a convention in one class.

Two columns in the claim carry the rest of the design:

- **`AttemptId` fences writes to the row.** It is written in the same statement as the state change. Every later write by this attempt is conditioned on it, so a worker that paused, lost its claim to recovery, and resumed cannot complete, stop, or fail a job that another attempt now owns. It does not fence the destination. The section on recovery comes back to that.
- **`Version` is the version of the job's terms, and it changes only if every writer changes it.** A worker that re-evaluates the requester's authority before starting, as [A Background Job Should Not Inherit Everything the User Could Do](background-job-should-not-inherit-user-authority.md) recommends, evaluates a particular version of the request. The claim's `Version` condition is useful only if each change to a term the policy depends on also increments `Version`. `ExecuteUpdateAsync` [does not apply EF Core's concurrency tokens for you](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete), so the increment is explicit:

```csharp
// API: change the destination of an export that has not started. The terms change, so the version does.
int changed = await db.ExportJobs
    .Where(j => j.Id == jobId && j.Status == ExportStatus.Queued && j.Version == expectedVersion)
    .ExecuteUpdateAsync(set => set
        .SetProperty(j => j.DestinationId, newDestinationId)
        .SetProperty(j => j.Version, j => j.Version + 1), ct);
```

A worker that evaluated the old destination now fails its claim and starts nothing whose terms nobody checked. [Your Security Check Passed. Then the World Changed.](security-check-passed-then-world-changed.md#at-the-moment-of-use-check-then-claim-then-send) develops this check-then-claim pattern and its limits, including facts that live outside the claimed row.

---

## A Second Cutoff Before the Point of No Return

A conditional claim tells you whether cancellation was in time to prevent the job from starting. For many jobs that is the only question, because starting and doing the work are the same thing. The export is different. For its first 24 seconds, it builds a file on the worker's own disk, which can be thrown away. Then it uploads that file to someone else's server, which cannot be called back.

A cancellation that arrives during those 24 seconds has lost the race to start, but it is still in time to prevent the effect that matters. So the export gets a second conditional write, at the boundary between work that can be discarded and work that cannot. Once it commits, delivery is authorized. Nothing may have been sent yet, but cancellation can no longer prevent it.

```text
          claim (cutoff 1)                authorize delivery (cutoff 2)
Queued ───────────────────▶ Running ───────────────────────────────────▶ Delivering ──▶ Completed
  │                            │                                             │            Failed
  │ cancel                     ├── stop requested ──▶ Stopped               └──────────▶ OutcomeUnknown
  │                            ├── build error ─────▶ Failed                              (unresolved)
  │ policy denies at worker    └── crash, lease expired ──▶ Queued (new attempt later)
  ▼                                                          or Stopped, if a stop was requested
Canceled   Rejected
```

| State | Means | Cancellation can |
| --- | --- | --- |
| `Queued` | Accepted and waiting. Possibly returned here by recovery after an earlier attempt | Commit first and move it to `Canceled` |
| `Running` | Claimed by an attempt that has done only reversible work | Record a stop request. The attempt must not cross cutoff 2 while one exists |
| `Delivering` | Delivery authorized for this attempt. The transfer may not have started, may be in progress, or may be finished | Only be recorded |
| `Canceled` | Canceled while queued. No further attempt will start | Only be recorded |
| `Rejected` | The worker's policy check denied the job before any claim | Only be recorded |
| `Stopped` | An attempt stopped before delivery was authorized. Nothing was sent | Only be recorded |
| `Failed` | Ended without delivering: a build error, or a delivery error that proves the file was not finalized | Only be recorded |
| `Completed` | Delivered, with evidence | Only be recorded |
| `OutcomeUnknown` | Delivery was authorized, and nobody can yet say whether it happened. Unresolved, not final | Only be recorded. Reconciliation moves it to `Completed` or `Failed` when evidence arrives |

The second cutoff has the same shape as the first:

```csharp
// Running → Delivering, only while this attempt owns the job and nobody has asked it to stop.
// CancellationToken.None: once this statement is sent, let it finish, so the attempt knows which side of
// the boundary it is on. Shutdown waits for one short statement.
int authorized = await db.ExportJobs
    .Where(j => j.Id == jobId
             && j.AttemptId == attemptId
             && j.Status == ExportStatus.Running
             && j.StopRequestId == null)
    .ExecuteUpdateAsync(set => set
        .SetProperty(j => j.Status, ExportStatus.Delivering)
        .SetProperty(j => j.DeliveryAuthorizedAt, j => DateTimeOffset.UtcNow),   // for people; the database
        CancellationToken.None);                                                  // clock where supported
```

A stop request is a conditional write on the same row too, `Running` with no earlier stop request, so it and the authorization are serialized exactly as the claim and the cancellation were. Either the stop request commits first and the authorization matches nothing, or the authorization commits first and the stop request finds a job in `Delivering`.

Where to put the second cutoff is a design decision, and it is worth writing down. It belongs immediately before the first effect that you cannot undo, or cannot undo cheaply: the upload, the email, the payment request, the message to another team's system. Work before it should be safe to discard. Work after it should be reported, not abandoned halfway, unless you have decided what a half-finished effect means. If a job has several irreversible steps, decide whether each gets its own authorization write or the whole sequence proceeds once the first begins. An export of five files might authorize each file separately, so a stop request can still prevent the files not yet sent. A payment followed by a receipt email usually treats the email as part of the payment, so the email goes out even if a stop request arrives in between.

The second cutoff does not make cancellation work after delivery is authorized. It makes the boundary explicit, so that "too late" has a precise meaning too.

---

## After the Claim, Cancellation Is a Request

Once a job is `Running`, the console cannot stop it by writing a status. Only the worker can stop, and only at places in its code where stopping is safe. .NET's model for this is [cooperative cancellation](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads): the code that requests cancellation signals a token, and the code doing the work decides how and when to respond.

So the worker needs a way to hear the request, and places to act on it:

```csharp
public sealed class ExportRunner(
    ExportJobs jobs, ExportPolicy policy, IExportBuilder builder, IPartnerDestination destination,
    IDeliveryErrorClassifier classifier, StopRequestWatcher watcher, WorkerIdentity worker)
{
    // Every write through `jobs` below is conditional, and those made after cutoff 1 also require
    // this attempt. Writes made while recording an outcome use CancellationToken.None internally.
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
            await jobs.TryRejectAsync(job.Id, job.Version, decision);   // Queued → Rejected, this version only
            return;
        }

        // Cutoff 1. Autocommitted, so `true` means the claim is durable.
        Guid attemptId = Guid.NewGuid();
        if (!await jobs.TryClaimAsync(job.Id, job.Version, attemptId, worker.Id, stoppingToken))
        {
            return;
        }

        ExportFile? file = await PrepareAsync(job, decision, attemptId, stoppingToken);
        if (file is not null)
        {
            await DeliverAsync(job.Id, attemptId, file, stoppingToken);
        }
    }

    // Returns the file only if cutoff 2 committed. Otherwise records why not, and removes the local file.
    private async Task<ExportFile?> PrepareAsync(
        ExportJob job, ExportDecision decision, Guid attemptId, CancellationToken stoppingToken)
    {
        // `stop` is signaled when a recorded stop request for this attempt appears, or on shutdown.
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        await using StopRequestWatch watch = watcher.Watch(job.Id, attemptId, stop);

        ExportFile? file = null;
        bool authorized = false;
        try
        {
            // Reversible: a local file. The builder removes its partial output when canceled.
            file = await builder.BuildAsync(job, decision.Scope, stop.Token);

            // Cutoff 2. This write, not the watcher, guarantees that a stop request committed first prevents delivery.
            authorized = await jobs.TryAuthorizeDeliveryAsync(job.Id, attemptId);
            if (!authorized)
            {
                // Running → Stopped only if a stop request is recorded for this attempt. If the attempt
                // lost ownership instead, this matches nothing, and the new owner's record stands.
                await jobs.TryFinishStoppedAsync(job.Id, attemptId, StopPoint.BeforeDelivery);
            }
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            await jobs.TryFinishStoppedAsync(job.Id, attemptId, StopPoint.WhileBuilding);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await jobs.TryFinishFailedAsync(job.Id, attemptId, ex);   // nothing has left the process
        }
        finally
        {
            if (!authorized && file is not null)
            {
                await file.TryDeleteAsync();   // best effort; never throws over the original exception
            }
        }

        // On shutdown, the exception propagates, the job stays Running, and recovery takes it up later.
        return authorized ? file : null;
    }

    private async Task DeliverAsync(Guid jobId, Guid attemptId, ExportFile file, CancellationToken stoppingToken)
    {
        // Past the point of no return. Stop requests from here on are recorded, not obeyed.
        DeliveryOutcome outcome;
        try
        {
            // Uploads under a temporary name, then renames it to the final name. The receipt records the
            // final name, size, and hash that were confirmed on the partner's server.
            DeliveryReceipt receipt = await destination.UploadAndFinalizeAsync(file, attemptId, stoppingToken);
            outcome = DeliveryOutcome.Completed(receipt);
        }
        catch (Exception ex)
        {
            // Failed only when the error proves the final name was never written; otherwise unknown.
            // Includes shutdown during the transfer.
            outcome = classifier.Classify(ex);
        }

        // Delivering → Completed, Failed, or OutcomeUnknown, for this attempt only. If this write
        // never happens, the job stays Delivering, and recovery resolves it.
        await jobs.TryFinishDeliveryAsync(jobId, attemptId, outcome);
        await file.TryDeleteAsync();
    }
}
```

The details that matter for cancellation:

- **Stopped means a recorded stop request.** `TryFinishStoppedAsync` moves `Running` to `Stopped` only for this attempt and only when `StopRequestId` is set. An attempt that has lost ownership writes nothing at all. Its catch blocks cannot turn a lease takeover into an operator's stop.
- **The watcher is for speed, not correctness.** It can poll the row every few seconds for a stop request, or subscribe to a notification and then confirm against the row. Either way, it lets a stopped export give up after three seconds instead of 24. If it misses the request, or the notification is lost, cutoff 2 still refuses to authorize delivery. Do not make a notification the only thing standing between a stop request and an upload.
- **Stopping happens only where it is safe.** `BuildAsync` passes the token down to the database query and the file writes, so they end promptly. Whatever ends the preparation, a local file that was never authorized for delivery is removed.
- **The upload is made safe to interrupt rather than cancellable.** Writing under a temporary name and renaming to the final name only after the transfer completes means an interrupted upload leaves nothing under the name the partner processes. That works only if the partner ignores temporary names and its server performs the rename as one step, so agree on both with the partner. The design still treats an interruption honestly: if the host shuts down mid-transfer, the classifier records `OutcomeUnknown`, because the rename may or may not have happened. [A Payment API Timeout Leaves the Outcome Unknown](payment-api-timeout-unknown-outcome.md) covers why an interrupted external call needs its own state and is resolved only by evidence.
- **`Completed` needs evidence.** The receipt is the finalized file, with its expected size and hash, or whatever acknowledgment the partner's contract defines as delivery. A file that merely exists, under any name, is not that evidence.

Some effects do support cancellation after they begin: a provider's API to cancel a scheduled payout, a print queue that can drop a job before the run, a long database operation that rolls back. If yours does, a stop request in `Delivering` can turn into a call to that API, and that system's answer, not your intention, decides the outcome. Record the call and its answer as their own operation. [An Allowed Decision Does Not Mean the Operation Succeeded](allowed-decision-does-not-mean-execution-succeeded.md#compensation-is-another-operation) explains why undoing an effect is a new operation with its own decision, not a rewrite of the old one.

---

## When Cancellation Loses, Record That It Lost

A cancellation request is an event with its own identity, whatever it achieves. Record it every time, in the same short transaction that decides what it does. Two things make that record trustworthy. The transaction locks the job row before it classifies anything, so the state it records is the state its write acted on. And the request ID comes from the client, so a retry after a lost response returns the outcome already recorded instead of creating a second request.

```csharp
public async Task<CancellationResult> RequestCancellationAsync(
    Guid requestId, Guid jobId, OperatorId requestedBy, string reason, CancellationToken ct)
{
    // The console generates requestId once per click. A retry returns what that click already decided.
    CancellationRequest? earlier = await db.CancellationRequests.AsNoTracking()
        .SingleOrDefaultAsync(r => r.Id == requestId, ct);
    if (earlier is not null)
    {
        return CancellationResult.From(earlier);
    }

    await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);

    // Lock the job row for the rest of this transaction. A claim, a stop request, an authorization,
    // or a recovery write on this job waits until it commits. Check the generated SQL: the lock must apply.
    ExportJob? found = await db.ExportJobs
        .FromSql($"SELECT * FROM export_jobs WHERE id = {jobId} FOR UPDATE")
        .AsNoTracking()
        .SingleOrDefaultAsync(ct);

    if (found is null)
    {
        return CancellationResult.NotFound(requestId);
    }

    CancellationOutcome outcome = found.Status switch
    {
        ExportStatus.Queued => CancellationOutcome.CanceledWhileQueued,
        ExportStatus.Running when found.StopRequestId is null => CancellationOutcome.StopRequested,
        ExportStatus.Running => CancellationOutcome.StopAlreadyRequested,
        ExportStatus.Canceled => CancellationOutcome.AlreadyCanceled,
        ExportStatus.Rejected or ExportStatus.Stopped or ExportStatus.Failed => CancellationOutcome.AlreadyEnded,
        _ => CancellationOutcome.TooLate,   // Delivering, Completed, OutcomeUnknown: delivery was authorized
    };

    // The conditions are redundant while the row is locked. They stay, so the writes remain correct
    // if someone later removes the lock.
    if (outcome == CancellationOutcome.CanceledWhileQueued)
    {
        await db.ExportJobs
            .Where(j => j.Id == jobId && j.Status == ExportStatus.Queued)
            .ExecuteUpdateAsync(set => set
                .SetProperty(j => j.Status, ExportStatus.Canceled)
                .SetProperty(j => j.CancellationRequestId, requestId), ct);
    }
    else if (outcome == CancellationOutcome.StopRequested)
    {
        await db.ExportJobs
            .Where(j => j.Id == jobId && j.Status == ExportStatus.Running && j.StopRequestId == null)
            .ExecuteUpdateAsync(set => set.SetProperty(j => j.StopRequestId, requestId), ct);
    }

    // No ExportJob is tracked by this context, so these ExecuteUpdate calls and SaveChanges never touch
    // the same entity. A concurrent retry with the same requestId fails on the primary key; reread and return.
    db.CancellationRequests.Add(new CancellationRequest(
        requestId, jobId, requestedBy, reason, outcome,
        FoundStatus: found.Status,
        FoundAttemptId: found.AttemptId,
        FoundAttemptCount: found.AttemptCount,
        FoundDeliveryAuthorizedAt: found.DeliveryAuthorizedAt));

    await db.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);

    return CancellationResult.From(requestId, outcome, found);
}
```

Keep this transaction short: one locking read, at most one update, and one insert. Policy checks, notifications, and anything else that can wait belong outside it, because a worker's claim on the same row waits for it. EF Core [warns against mixing tracked `SaveChanges` changes and `ExecuteUpdate` on the same entities](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete); this method avoids that by never tracking the job. Authorizing the operator for this tenant's job is omitted here and belongs before the transaction.

The job row records only the request that canceled it (`CancellationRequestId`) or the first stop request (`StopRequestId`). The `CancellationRequests` table records every request, including the ones that changed nothing. Neither is edited later to look like a different outcome, and the job is never rewritten from `Completed` to `Canceled` because someone wished it had been. The attempt's own terminal write links back to the stop request it honored, if any, so the full story is two kinds of record that agree:

| Sam's request commits at | What happens | Job ends as | Request records | The console says |
| --- | --- | --- | --- | --- |
| 14:02:07.391 | Commits 21 ms before the worker's claim reaches the row | `Canceled` | `CanceledWhileQueued`, no prior attempts | "Canceled while queued. No attempt had started, and none will." |
| 14:02:07.430 | The claim committed first; the stop request then beats cutoff 2 | `Stopped`, while building | `StopRequested`, found attempt `5c2e…` running on worker 3 | "Already started at 14:02:07.412. Stopped at 14:02:10.452, before delivery was authorized. Nothing was sent." |
| 14:02:31.880 | Cutoff 2 committed at 14:02:31.861 | `Completed` | `TooLate`, found attempt `5c2e…` in `Delivering` | "Too late: delivery was authorized at 14:02:31.861, before your request. Delivery completed at 14:02:32.904: 48,112 contacts to partner-old. Your request CXL-303 is recorded." |

The first row is the opening incident with conditional writes, and nothing else changed. If recovery had returned the job to `Queued` after an earlier attempt, the same outcome would say "Canceled while queued. No further attempt will start," with the earlier attempt's ending beside it; "no attempt had started" is reserved for a job whose attempt count is zero.

The third row is not a failure of the design. It is the design telling the truth: a request 19 milliseconds after the point of no return cannot recall a file from someone else's server, and the operator who clicked needs to know that now, not on Monday. At the moment of the click, the console can say only that delivery was authorized. It reports "completed" once the attempt has recorded evidence, and "unknown" if that is what the attempt recorded. What to do about the file, asking the old agency to delete it and recording whether they confirm, is a new operation with its own owner.

The evidence for the second case reads like this:

```text
Requested   EXP-7731  by admin@acme (tenant-acme)      14:01:55.004  partner-old, 48,112 contacts, v1
Claimed     EXP-7731  attempt 5c2e…  by worker-3      14:02:07.412  checked v1, decision allowed
Cancel      CXL-302   EXP-7731  by sam.ops            14:02:07.430  "wrong destination"
                      outcome StopRequested; found attempt 5c2e… Running
Stopped     EXP-7731  attempt 5c2e…  by worker-3      14:02:10.452  while building, for CXL-302
                      local file deleted; delivery never authorized
```

The times are each host's own readings, recorded for people. The order that matters is already settled by the row, and the records say so in words: "found attempt `5c2e…` Running," "for CXL-302."

### What the API returns

The cancel endpoint should return the outcome, not an echo of the request. One reasonable mapping:

| Outcome | Response | Body includes |
| --- | --- | --- |
| `CanceledWhileQueued` | `200 OK` | The request ID, and whether any attempt had ever started |
| `AlreadyCanceled` | `200 OK` | That the job was already canceled, by which request and when. This request changed nothing |
| `StopRequested`, `StopAlreadyRequested` | `202 Accepted` | The request ID, the running attempt, and a status URL to poll for the attempt's ending |
| `AlreadyEnded` | `409 Conflict` | The job's ending (`Rejected`, `Stopped`, or `Failed`) and that nothing was delivered |
| `TooLate` | `409 Conflict` | The job's current state, when delivery was authorized, and the delivery outcome if it is known yet |
| `NotFound` | `404 Not Found` | Nothing about whether a job exists in another tenant |

Whatever the status codes, the console should say "canceled" only for the first row, and should name the earlier request for the second. "Already canceled by CXL-301," "cancellation requested," and "too late" are different facts, and the operator will repeat whichever one the screen shows to a customer.

---

## Recovery Must Not Undo a Cancellation or Repeat a Delivery

A worker can crash, or pause long enough to be presumed dead, in any state. A production design gives each attempt a lease, renewed while it works, and a recovery process that takes over a job only when its lease has expired, with a conditional write on the expected state and the old `AttemptId`. That is the same fencing as the claim, applied to a takeover: the original worker, if it resumes, finds that its attempt no longer owns anything. [An In-Memory Grant Store Is Not Distributed Replay Protection](in-memory-grant-store-is-not-distributed-replay-protection.md#recovery-and-the-original-executor-share-one-ownership-protocol) works through that ownership protocol and the timing values it depends on.

Cancellation adds four rules to recovery:

- **A `Running` attempt with a stop request is finished, not restarted.** `Running` means cutoff 2 was never committed, so nothing was delivered. Recovery moves the job to `Stopped`, conditionally on `Running`, the expired attempt, and the recorded stop request. The original worker, if it resumes, cannot authorize delivery either, because the recorded stop request already fails cutoff 2's condition.
- **A `Running` attempt without one may be returned to `Queued`**, with the old attempt fenced off and a new message published through the outbox in the same transaction. That takeover does race a late authorization from the original worker: both require `Running` and the same attempt, so at most one commits. If the takeover wins, the job is ordinary queued work again, and a cancellation that arrives afterward competes with the next claim. If the authorization wins, the job is `Delivering`, and the next rule applies.
- **A `Delivering` attempt is resolved, not stopped, canceled, or retried.** It may have authorized delivery and crashed before sending a byte, or sent everything. Recovery looks for evidence at the destination, the finalized file with the expected size and hash, and records `Completed` if it finds it. A temporary file, or no file at all, is not evidence either way while the original worker might still resume, because `AttemptId` fences writes to your row, not uploads to the partner's server. Without evidence, recovery records `OutcomeUnknown`, and reconciliation, with the partner if necessary, resolves it. A cancellation request against that job is still `TooLate`.
- **A replacement delivery is a new decision.** Recovery does not start one merely because an attempt is old. Start one only if the destination makes a duplicate harmless, for example a fixed final name per job that the partner treats as one file, and only after the original attempt can no longer write.

Recovery is also a writer to the job row, so it follows the same invariant as everything else: no unconditional status changes.

---

## Testing the Race, Not Just the Rule

Tests that call cancel and then run the worker, one after the other, prove the rule and miss the race. The race needs two transactions contending for one row in the real database engine, each with its own `DbContext`, connection, and transaction. A fake that serializes calls, or two writers sharing one connection, cannot show it. Both writers can run from one test process. These tests are specifications of the behavior described above, not evidence of a working implementation, and each shows the behavior only under the conditions it sets up.

```csharp
[Theory]
[InlineData(Writer.Cancel)]
[InlineData(Writer.Claim)]
public async Task The_second_writer_waits_for_the_first_and_then_cannot_leave_Queued(Writer first)
{
    Guid job = await harness.QueueExportAsync();   // one PostgreSQL database for the class; a fresh job per test

    await using WriterSession a = await harness.OpenSessionAsync();   // own context, connection, transaction
    await using WriterSession b = await harness.OpenSessionAsync();

    await a.BeginTransactionAsync();
    await a.WriteAsync(first, job);                                   // row locked, not yet committed

    Task<WriteResult> second = b.WriteInOwnTransactionAsync(first.Other(), job);
    await harness.WaitUntilWaitingOnLockAsync(b);                     // pg_stat_activity: b is waiting on a lock
    Assert.False(second.IsCompleted);

    await a.CommitAsync();
    WriteResult result = await second;

    if (first == Writer.Cancel)
    {
        Assert.Equal(ExportStatus.Canceled, await harness.StatusAsync(job));
        Assert.Equal(WriteResult.NotClaimed, result);
    }
    else
    {
        Assert.Equal(ExportStatus.Running, await harness.StatusAsync(job));
        Assert.Equal(CancellationOutcome.StopRequested, result.CancellationOutcome);
    }
}

[Fact]
public async Task Cancellation_between_load_and_claim_prevents_the_export()
{
    Guid job = await harness.QueueExportAsync(contacts: 48_112, destination: "partner-old");

    // Reproduce the incident's interleaving deterministically. The hook is awaited before the claim runs.
    harness.Worker.AfterLoad = () => harness.Console.CancelAsync(job, "sam.ops", "wrong destination");
    await harness.Worker.ProcessNextMessageAsync();

    Assert.Equal(ExportStatus.Canceled, await harness.StatusAsync(job));
    Assert.Empty(harness.Destination.Uploads);
    Assert.Empty(harness.Worker.FilesOnDisk);
    Assert.Equal(CancellationOutcome.CanceledWhileQueued, (await harness.CancellationsAsync(job)).Single().Outcome);
}

[Fact]
public async Task Stop_request_before_cutoff_2_sends_nothing_even_without_the_watcher()
{
    Guid job = await harness.QueueExportAsync();
    harness.Worker.Watcher.Disable();

    harness.Worker.BeforeAuthorizeDelivery = () => harness.Console.CancelAsync(job, "sam.ops", "late");
    await harness.Worker.ProcessNextMessageAsync();

    Assert.Equal(ExportStatus.Stopped, await harness.StatusAsync(job));
    Assert.Empty(harness.Destination.Uploads);
    Assert.Empty(harness.Worker.FilesOnDisk);
}

[Fact]
public async Task Cancellation_after_delivery_was_authorized_is_too_late_and_changes_nothing()
{
    Guid job = await harness.QueueExportAsync();

    CancellationResult? result = null;
    harness.Destination.DuringUpload = async () =>   // awaited: the upload continues only after cancel returns
        result = await harness.Console.CancelAsync(job, "sam.ops", "late");
    await harness.Worker.ProcessNextMessageAsync();

    Assert.Equal(CancellationOutcome.TooLate, result!.Outcome);
    Assert.Equal(ExportStatus.Delivering, result.Found.Status);
    Assert.Equal(ExportStatus.Completed, await harness.StatusAsync(job));   // not rewritten to Canceled
    Assert.Single(harness.Destination.Uploads);
}
```

The first test is deterministic: it holds one writer's transaction open, confirms from `pg_stat_activity` that the other is waiting on a lock, and only then commits. Run it for both orders, and add a variant where the first writer rolls back, after which the second must succeed. A loop that releases two tasks from one gate many times can still be useful as a soak test, against one database with a fresh job per iteration, but it shows overlap only by chance.

Also cover:

- **Stop request against cutoff 2.** The same held-transaction technique, with the stop request and the authorization as the two writers.
- **Recovery interleavings.** A cancellation that finds `Queued` after recovery returned the job records `CanceledWhileQueued` with a nonzero attempt count. Recovery's return of an expired `Running` attempt to `Queued` and a late authorization from the original worker cannot both commit. A crash after cutoff 2 but before the upload starts ends `OutcomeUnknown` or `Completed` from destination evidence, never a second upload.
- **Redelivery.** The message for a canceled, stopped, completed, or unknown job does nothing and is settled.
- **A changed request.** Changing the destination increments `Version`, so a worker that checked the old terms fails its claim and builds nothing.
- **Every writer.** Each path that can change `Status`, including administrative and repair paths, refuses to move a `Canceled` job back to `Queued` or `Running`. An architecture test can fail the build when code outside the job store writes the status column.
- **A paused attempt.** After recovery takes over an expired attempt, the original worker's authorization and completion writes match nothing, and its catch blocks record nothing.
- **Shutdown during upload.** The attempt ends `OutcomeUnknown`, not `Stopped` or `Failed`, and no file appears under the final name.
- **The request record.** A retried request with the same ID returns the recorded outcome and adds no row. A request for a missing job, or another tenant's job, returns `NotFound`.

[How to Test That a Denied Operation Never Executes](test-denied-operation-never-executes.md) develops the technique of asserting that an effect never happened, which most of these tests rely on.

---

## When One Conditional Update Is the Whole Design

Many jobs do not need two cutoffs, a watcher, and a stop-request protocol. Choose the first row that fits:

| Shape | What cancellation needs |
| --- | --- |
| **The job runs within seconds, or nobody needs to cancel it** | Possibly nothing. If the product does offer cancel, the conditional claim and conditional cancel below cost two `WHERE` clauses |
| **Cheap, repeatable work with no external effect**, such as rebuilding a cached report | A conditional claim and a conditional cancel. After the claim, report "already running" and let it finish. No second cutoff, because there is no point of no return |
| **Reversible preparation followed by an irreversible effect**, like this export | Both cutoffs, a stop request on `Running`, a watcher if early stopping is worth the effort, and a record of each request's outcome |
| **The effect lives in a system that can cancel it**, such as a provider-scheduled payout | Cancellation calls that system, and its answer decides. Record the call as its own operation |
| **The executor holds operation-specific authority across a trust boundary** | Revocation and claim contend on the authority's own state, not only on the job row. [Capability-Scoped Background Operation](../../case-studies/capability-scoped-background-operation.md#15-revocation-is-useful-but-has-a-boundary) defines that race, and [An In-Memory Grant Store Is Not Distributed Replay Protection](in-memory-grant-store-is-not-distributed-replay-protection.md) covers the shared, durable, atomic consumption it depends on |

The conditional claim is worth having even when nobody can cancel anything, because duplicate deliveries race in exactly the same way. If two copies of the same message reach two workers, the same `WHERE j.Status == ExportStatus.Queued` that makes cancellation decidable also lets at most one of them start.

---

## Failure Modes

Most of what goes wrong has been covered above. These six are the ones that keep reappearing after the main fix is in place.

**1. One writer without a condition.** A retry button, import, repair script, or recovery job sets the status unconditionally and resurrects a canceled job or overwrites a stop.

**2. A version nobody increments.** The claim checks `Version`, but the endpoint that changes the destination does not bump it, so the check passes on terms nobody evaluated.

**3. A watcher trusted for correctness.** The stop notification is lost, or the poll interval is longer than the remaining build, and nothing else prevents the upload.

**4. Rewriting history.** A job that delivered is marked `Canceled` because someone requested cancellation, or a request that changed nothing is deleted. The record no longer says what happened.

**5. Recovery that restarts what someone asked to stop, or delivers twice.** A crashed `Running` attempt with a stop request is requeued, or a `Delivering` attempt is retried because no file was found.

**6. Treating an interrupted effect as stopped or failed.** An upload cut off by shutdown is recorded as `Stopped` or `Failed`, when nobody knows whether the final file was written.

---

## A Short Review Checklist

**The race**

1. Is the claim a single conditional write on the job's state and the version that was checked, and does work begin only after it has committed?
2. Is cancellation a conditional write on the same row, so it and the claim cannot both leave `Queued`?
3. Does every writer of the status, including recovery and repair paths, use a condition, and does every change to the job's terms increment its version?
4. Is the order decided by the database's commit order on the primary, not by timestamps or replica reads?
5. At stricter isolation levels, is a serialization failure handled by retrying the whole transaction?

**The work in progress**

6. Is there a second conditional write immediately before the first effect that cannot be undone, conditioned on the attempt and on the absence of a stop request?
7. Is work before that boundary safe to discard, and is it cleaned up when the attempt does not cross it?
8. Does correctness hold if the watcher never fires?
9. Is the effect made safe to interrupt, for example by a temporary name and a final rename, and is an interrupted effect recorded as unknown?

**The records**

10. Does every cancellation request get its own record, with a client-supplied ID, the state it found, and the attempt it found?
11. Is a job that ran never rewritten as canceled, and is a request that changed nothing still kept?
12. Does the console distinguish "canceled while queued," "already canceled," "stop requested," and "too late," and say what was delivered, once that is known?

**Recovery and proportion**

13. Does recovery finish a `Running` attempt with a stop request as stopped, resolve a `Delivering` attempt from evidence, and never start a second delivery merely because an attempt is old?
14. Does the design match the job? Two `WHERE` clauses may be all it needs.

---

## Continue Deeper

For what a queued job should carry, how the worker re-checks the requester's authority before it claims, and why delivery is not authority, read [A Background Job Should Not Inherit Everything the User Could Do](background-job-should-not-inherit-user-authority.md). [Your Security Check Passed. Then the World Changed.](security-check-passed-then-world-changed.md) develops the check-then-claim pattern in this article and covers the facts that a conditional write on one row cannot see.

When the job carries a one-time grant rather than relying on a job row, cancellation becomes revocation, and it contends with consumption of the grant. [An In-Memory Grant Store Is Not Distributed Replay Protection](in-memory-grant-store-is-not-distributed-replay-protection.md) explains why that consumption has to be shared, durable, and atomic across instances, and [Capability-Scoped Background Operation](../../case-studies/capability-scoped-background-operation.md#15-revocation-is-useful-but-has-a-boundary) defines what happens when revocation and claim race for the same grant.

If cancellation loses after an external call has begun, [A Payment API Timeout Leaves the Outcome Unknown](payment-api-timeout-unknown-outcome.md) covers how to resolve an outcome that nobody observed, and [Your Retry Logic May Be Replaying an Authorization Decision](retry-logic-may-replay-authorization-decision.md) covers what each retry and recovery layer can and cannot know.

### Related Work

- [Transaction Isolation](https://www.postgresql.org/docs/current/transaction-iso.html) and [Serialization Failure Handling](https://www.postgresql.org/docs/current/mvcc-serialization-failure-handling.html), in the PostgreSQL documentation, describe how a concurrent `UPDATE` waits and re-evaluates its `WHERE` clause, and why a failed serializable transaction is retried as a whole.
- [ExecuteUpdate and ExecuteDelete](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete), in the EF Core documentation, covers set-based conditional updates, their row counts, transactions, and why concurrency control is your responsibility.
- [Cancellation in Managed Threads](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads) describes .NET's cooperative cancellation model.
- [Message transfers, locks, and settlement](https://learn.microsoft.com/en-us/azure/service-bus-messaging/message-transfers-locks-settlement), in the Azure Service Bus documentation, explains peek-lock, lock expiry, and redelivery, and [AMQP request-response operations](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-amqp-request-response) documents canceling scheduled messages by sequence number.
- [DeleteMessage](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/APIReference/API_DeleteMessage.html), in the Amazon SQS API reference, describes deletion by receipt handle and occasional redelivery after deletion from standard queues.
- [Using cancellation tokens](https://docs.hangfire.io/en/latest/background-methods/using-cancellation-tokens.html), in the Hangfire documentation, describes how a running job learns of shutdown or an aborted job, and how often the server checks.
- [CWE-367: Time-of-check Time-of-use (TOCTOU) Race Condition](https://cwe.mitre.org/data/definitions/367.html) describes the general weakness behind the opening incident.

---

## The Short Answer

Canceling a queued job is a race with the worker about to start it, and neither the queue message nor a status column decides that race on its own. Make starting and canceling competing conditional writes on the same row, so at most one can move the job out of `Queued`, and start work only after the claim has committed. If the job does reversible work before an effect that cannot be undone, add a second conditional write at that boundary, which a stop request can still beat.

After the claim, cancellation is a request, not a state. Record every request with what it found, let the worker stop only where stopping is safe, and tell the operator plainly whether the job was canceled while queued, stopped before delivery was authorized, or had already passed that point. For many jobs, that is two `WHERE` clauses and one honest message.
