---
description: Canceling a queued job races the worker that claims it. Make cancel and claim competing conditional writes on one row, and record it when cancel loses.
title: Canceling a Queued Job Can Race With Execution
author: Christopher D. Cavell
published: "2026-11-14"
summary: An operator who cancels a queued job is racing the worker that is about to start it. Deleting the queue message or setting a status column does not decide that race, and a worker that reads the status and then writes over it can start a job that was canceled a few milliseconds earlier. Make the claim that starts the job and the cancellation that stops it competing conditional writes on the same row, so at most one of them can move the job out of the queue. Add a second conditional write before the first effect that cannot be taken back. After the claim, treat cancellation as a request to stop, not a change of state, and record what each request found and what the attempt it lost to went on to do. Hold recovery, leases, and the outbox to the same conditional-write rule, so queued work is recovered rather than stranded and an uncertain delivery is never blindly repeated.
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

**Sample scope:** The C# targets .NET 8 or later and uses EF Core's `ExecuteUpdateAsync`, available from EF Core 7. Statements about concurrent updates describe PostgreSQL's documented behavior. Statements about Amazon SQS, Azure Service Bus, and Hangfire describe their documentation on the verification date below. Database, broker, and job-library behavior varies by product, version, and configuration, so confirm it for yours, and prove the database behavior with the contention test near the end. The CRM, the export, the partner, and the timings are hypothetical. The stores, the builder, the destination client, the watcher, and the test harness are illustrative sketches, not a runnable sample or a recommended persistence API. Leases, recovery, the outbox, and the complete worker and cancellation methods are in the companion [implementation reference](../../aspnetcore/queued-job-cancellation-recovery-implementation-reference.md). Original prose is licensed under [Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/), and original code examples are additionally offered under the [MIT License](https://opensource.org/license/mit), as described in the repository's [licensing terms](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md). Third-party material, where identified, remains under its original license. External sources were last verified on November 14, 2026.

**What this article covers:** a cancellation that committed 21 milliseconds before a worker's claim, was overwritten, and was reported as a success; four things that are easy to call "the job" and that cancellation affects differently; why re-reading the status, deleting the message, and disabling the button narrow the window without closing it; a conditional claim that makes the pre-start cutoff explicit, and a cancellation that competes for the same row; a second cutoff before the first irreversible effect; what cancellation can still do after the work has begun; what to record when cancellation loses, and what to tell the operator; what recovery after a crash must not do; tests that hold one writer's transaction open while the other waits; and when one conditional status update is the whole design. A companion [implementation reference](../../aspnetcore/queued-job-cancellation-recovery-implementation-reference.md) carries the schema, single-statement claims, leases, recovery, the outbox and sweeper, the complete worker and cancellation methods, and the wider test matrix.

**In plain terms:** you phone a print shop to cancel an order of flyers. The receptionist looks at the order board, sees "waiting," crosses it out, and tells you it is canceled. A minute earlier, the press operator took that ticket off the board and started making plates. The board and the press were never the same thing. Answering the phone faster would not have helped. What helps is a rule that the operator must sign a ticket out at the desk before making any plates, and that cancellations are written at that same desk, so whichever signature comes first wins. If the operator signed first, the honest answer to your call is "it has started; I've asked them to stop before the run," and later, either "they stopped before printing" or "the run had already been approved, and here is what was printed."

Consider a hypothetical multi-tenant CRM. A tenant administrator can export a contact list to a marketing partner's SFTP server. Exports are rate-limited per destination, so the API records the export, together with an outbox row that a dispatcher then publishes to a queue, and a pool of report workers processes them. Each export builds a CSV file locally, which takes a while for a large list, and then uploads it. A support engineer can cancel a queued export from an operations console.

On a Saturday afternoon, a customer phones support. Their administrator has just queued an export to their old agency, whose contract ended last month, and the list should not go there. Sam, the support engineer, opens the console. Export `EXP-7731` shows **Queued**. Sam clicks **Cancel**.

```text
14:01:55.004  api        EXP-7731 accepted: tenant-acme, 48,112 contacts → partner-old; status Queued; outbox row written
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

Sam tells the customer the export was canceled. On Monday, the old agency's account manager emails to ask why 48,112 contacts arrived over the weekend. The export's row now says `status = Completed` and `canceled_by = sam.ops`, a completed export alongside a cancellation, with nothing to say which came first or why.

Every database write in that log succeeded. Worker 3 read `Queued` at 14:02:07.386 and wrote `Running` at 14:02:07.412. Sam's cancellation committed in the 26 milliseconds between them and was overwritten. Removing the message found nothing, because the worker already held it, and nobody read that as a warning. The console reported the result of the cancellation's own write, which was true and irrelevant: the question that mattered was whether the export would run, and nothing in the cancellation path asked it.

> **A queued job can be canceled only until something is allowed to start it. Make that moment one conditional write, the claim, and make cancellation a competing conditional write on the same row, so that at most one of them can move the job out of the queue. After the claim, cancellation is a request to stop work already underway. Record it as one, with what it found and the attempt it lost to, and never rewrite a job that ran as one that was canceled.**

Most of that is one `WHERE` clause on each side. The rest of this article is about the parts a single clause does not cover: work that is already running, effects that cannot be recalled, crashes, and what to tell the person who clicked **Cancel**.

**How to read this article:** this page makes the argument and gives the rules, with short sketches. When you are ready to build, [Queued Job Cancellation and Recovery in ASP.NET Core: An Implementation Reference](../../aspnetcore/queued-job-cancellation-recovery-implementation-reference.md) shows how those rules hold up under crashes, paused workers, lost database responses, redelivered messages, and operators who click twice.

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

In Amazon SQS, [`DeleteMessage` identifies a message by the receipt handle from the most recent receive](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/APIReference/API_DeleteMessage.html), so deleting a message takes a handle obtained by receiving it, plus permission to delete; the message ID returned at send time, or your own job ID, is not enough. A caller holding a current handle can delete the message even while a visibility timeout hides it from other consumers, but a console that only knows the job ID has no handle. The same page notes that a standard queue can, on rare occasions, return a message after it has been deleted. Azure Service Bus lets a sender [cancel a scheduled message by its sequence number](https://learn.microsoft.com/en-us/azure/service-bus-messaging/message-sequencing) before its enqueue time, and the same page warns that the two operations do not lock each other, so a cancellation that arrives while the message is becoming active does not stop it. Once an active message is held under a [peek-lock](https://learn.microsoft.com/en-us/azure/service-bus-messaging/message-transfers-locks-settlement), competing receivers cannot receive it, and if the lock is released or expires, the message is delivered again, until its maximum delivery count moves it to the dead-letter queue.

So "remove the message" is housekeeping at best. Even where it works, the message may already be inside a worker's process, as it was at 14:02:07.380. Design the worker so that a message is only a hint to go and look at the job, and so that cancellation never needs to touch the broker to be correct. A message for a canceled job then arrives, finds nothing to do, and is settled.

Once the worker's claim has committed, the row carries the work, so the worker can settle the message right away instead of renewing its lock for the length of the build; a redelivered copy would only reach the conditional claim and do nothing. The cost is that any write that leaves a job queued without a live message, such as recovery returning a job to the queue or an edit to a queued job's terms, must arrange a new message through an outbox. The [implementation reference](../../aspnetcore/queued-job-cancellation-recovery-implementation-reference.md#messages-the-outbox-and-the-sweeper) covers settlement, outbox message IDs, and a sweeper that keeps queued work from being stranded.

### Job libraries have the same distinction

Job libraries put the queue and the job state in one product, which makes the distinction easier to miss, not absent. Hangfire, for example, [gives a running job a cancellation token](https://docs.hangfire.io/en/latest/background-methods/using-cancellation-tokens.html) that is signaled on server shutdown or when the job's processing is aborted. A background process detects aborted jobs by polling job storage, every five seconds by default, and the job's own code still has to check the token. So between a state change in storage and the job's next check, the dashboard and the running code disagree. Whatever library you use, find out precisely what its delete or cancel operation guarantees for a job that is about to start, one that has started, and one that is in the middle of an external call. Those are three different questions.

---

## Fixes That Narrow the Window Without Closing It

The first reactions to the incident are usually reasonable, and each one makes the race rarer without making it impossible.

**Re-read the status just before starting.** The worker checks again at the last moment. The gap between that read and the write that starts the job is now smaller, but it is still there, and an unconditional `UPDATE ... SET status = 'Running'` still overwrites a cancellation that lands inside it. This is the [time-of-check to time-of-use](https://cwe.mitre.org/data/definitions/367.html) shape, compressed.

**Delete the message as well.** As above, the message may already be held by a consumer, and removing it does not stop a worker that already has it.

**Disable Cancel once the job is running.** The console reads the status a few seconds before Sam clicks, so the button can be enabled for a job that started in the meantime. The button reflects an old read, not a decision.

**Compare timestamps afterward.** If `canceled_at` is earlier than `started_at`, the cancellation "should have won." Those values come from different hosts' clocks, and even from one clock, the moment a value was computed is not the moment its transaction committed. [Timestamps from different hosts are not a reliable ordering](correlation-id-is-not-distributed-audit-trail.md#what-a-correlation-id-actually-gives-you), and they cannot decide anything after the fact anyway.

**Take a distributed lock around cancel and start.** A lock alone does not stop a holder that pauses past the lock's expiry from writing afterward. Martin Kleppmann's [analysis of distributed locking](https://martin.kleppmann.com/2016/02/08/how-to-do-distributed-locking.html) explains the remedy: each grant of the lock carries a fencing token that only ever goes up, and the protected resource rejects any write presenting a token lower than one it has already seen. The job row can play both parts. Its conditional writes serialize cancel and start, and the `AttemptId` introduced below protects later writes from an attempt that has lost ownership. `AttemptId` is not a monotonic fencing number. It is an ownership token that the row itself compares for equality, which works here because the row is both the lock and the resource. That avoids a separate lock service, although row contention and transaction length still matter.

Each of these is trying to make one actor's read stay true until its write. The database can do that directly, if the write carries the condition.

---

## The Cutoff Is a Conditional Write

The claim and the cancellation both move a job out of `Queued`. Write both as conditional updates on the same row, with the condition the other one would violate. These are the minimal forms, showing only the conditional `UPDATE`; `checkedVersion`, `workerId`, `requestId`, and `ct` come from the caller. The implementation reference gives their [full forms](../../aspnetcore/queued-job-cancellation-recovery-implementation-reference.md#claim-and-authorization-in-one-statement-each), and its [cancellation method](../../aspnetcore/queued-job-cancellation-recovery-implementation-reference.md#recording-cancellation-requests) adds the row lock, request record, and honest answer to the operator that a production implementation also needs.

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
// The full claim also sets a lease and records an attempt event in the same statement;
// see the implementation reference.

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

if (canceled == 0)
{
    return CancelResult.NotQueued;   // never report "canceled" here; find out what the job is doing instead
}
```

[`ExecuteUpdateAsync`](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete) sends one `UPDATE` with that `WHERE` clause and returns the number of rows changed. The read and the write are no longer two steps that something else can fall between. They are one statement, and the database decides the order. (The redesigned row also drops `canceled_by`. It records the request that canceled the job, `CancellationRequestId`, and keeps every request, including those that changed nothing, in a separate table described below.)

In PostgreSQL's default `READ COMMITTED` level, when two transactions update the same row, the second waits for the first. If the first commits, the second [re-evaluates its `WHERE` clause against the updated row](https://www.postgresql.org/docs/current/transaction-iso.html) and proceeds only if the row still matches. In the opening incident, Sam's cancellation commits at 14:02:07.391, before the worker's claim reaches the row. The claim is evaluated against a row whose status is now `Canceled`, matches nothing, and returns `0`. Had the two overlapped, the later one would have waited for the earlier one's commit and then been evaluated the same way. The worker does not build a file. It settles the message and records that the job was not claimable.

Had the worker's claim committed first, the cancellation would have matched nothing, and the console would have learned that the export had already started. For each period the job spends in `Queued`, at most one transition out of it can commit, and neither write can overwrite the other, because neither writes without checking. That is not the same as saying one of the two always wins: a version mismatch, a rollback, or another writer can leave both with nothing.

At `REPEATABLE READ` or `SERIALIZABLE`, PostgreSQL can instead fail the second transaction with a serialization error. The documented remedy is to [roll back and run the whole transaction again](https://www.postgresql.org/docs/current/mvcc-serialization-failure-handling.html), re-making every decision it contained, not to repeat the failed statement. The retry reads the job as it is then, and its result depends on that state; it is not guaranteed to change nothing.

That is the explicit pre-start cutoff. "Canceled while queued" now has a precise meaning: the cancellation's write committed against the current queued state, before a competing claim moved it to `Running`. If recovery had returned the job to the queue after an earlier attempt, that attempt may already have done reversible work. The meaning does not depend on clocks, on the broker, or on how quickly anyone clicked. The guarantee also has preconditions worth stating:

- **Both writers decide on the same row in the same primary database.** A status read from a replica or a cache can inform a screen, never a claim or a cancellation.
- **Work starts only after the claim has committed.** Here each `ExecuteUpdateAsync` runs as its own statement, and [EF Core does not start a transaction around it implicitly](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete), so a return of `1` means the claim has committed. The full claim in the implementation reference also sets a lease and records an attempt event in that same single statement, so the same holds for it. If you put a claim inside a larger transaction instead, begin the work only after that transaction commits, and keep such transactions short, because a transaction held open by a paused process holds the row too. Whether a commit survives a database crash or failover depends on your durability settings; PostgreSQL's [asynchronous commit](https://www.postgresql.org/docs/current/wal-async-commit.html), for example, can lose recently acknowledged transactions in a crash. This design assumes acknowledged job-state changes are not lost.
- **A lost response is not a failure.** A connection can drop after the database commits and before the client hears about it, and EF Core's [connection resiliency documentation](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency) describes the resulting transaction state as unknown. When a claim or a later boundary write throws, or returns `0` after a retry, re-read the row and confirm state and ownership together before doing anything else, and never repeat an external call because a database write was uncertain. The [implementation reference](../../aspnetcore/queued-job-cancellation-recovery-implementation-reference.md#claim-and-authorization-in-one-statement-each) spells out the rules.
- **Every writer uses a condition.** An administrative screen, an import, a retry button, a recovery job, or a repair script that sets `status = 'Running'` or `status = 'Queued'` without one reopens the race. This is a system-wide write invariant on the job row, not a convention in one class.

Two columns in the claim carry the rest of the design:

- **`AttemptId` fences writes to the row.** It is written in the same statement as the state change. Every later write by this attempt is conditioned on it, so a worker that paused, lost its claim to recovery, and resumed cannot complete, stop, or fail a job that another attempt now owns. It does not fence the destination. The section on recovery comes back to that. Job IDs here are globally unique GUIDs, so most writes identify the job by ID alone; the cancellation path, which receives a tenant from the operator's request, also filters by it as defense in depth.
- **`Version` is the version of the job's terms, and it changes only if every writer changes it.** A worker that re-evaluates the requester's authority before starting, as [A Background Job Should Not Inherit Everything the User Could Do](background-job-should-not-inherit-user-authority.md) recommends, evaluates a particular version of the request. The claim's `Version` condition is useful only if each change to a term the policy reads, such as the destination, filters, or field scope, also increments `Version`. `ExecuteUpdateAsync` [does not apply EF Core's concurrency tokens for you](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete), so every edit to those terms increments `Version` explicitly, and publishes a new message so the edited job is not stranded; the implementation reference [shows that edit](../../aspnetcore/queued-job-cancellation-recovery-implementation-reference.md#messages-the-outbox-and-the-sweeper). A worker that evaluated the old destination then fails its claim and starts nothing whose terms nobody checked. [Your Security Check Passed. Then the World Changed.](security-check-passed-then-world-changed.md#at-the-moment-of-use-check-then-claim-then-send) develops this check-then-claim pattern and its limits, including facts that live outside the claimed row.

---

## A Second Cutoff Before the Point of No Return

A conditional claim tells you whether cancellation was in time to prevent the job from starting. For many jobs that is the only question, because starting and doing the work are the same thing. The export is different. For its first 24 seconds, it builds a file on the worker's own disk, which can be thrown away. Then it uploads that file to someone else's server, which cannot be called back.

A cancellation that arrives during those 24 seconds has lost the race to start, but it is still in time to prevent the effect that matters. So the export gets a second conditional write, at the boundary between work that can be discarded and work that cannot. Once it commits, delivery is authorized. Nothing may have been sent yet, but cancellation can no longer prevent it.

```text
             claim (cutoff 1)                authorize delivery (cutoff 2)
Queued ─────────────────────▶ Running ───────────────────────────────────▶ Delivering ──▶ Completed
  │                           │                                            │              Failed
  ├── cancel ─▶ Canceled      ├── stop requested ──▶ Stopped               └─────────────▶ OutcomeUnknown
  │                           ├── build error ─────▶ Failed                               (unresolved)
  └── denied ─▶ Rejected      └── lease expired ───▶ Queued again, Stopped, or Failed at the attempt cap
```

| State | Means | Cancellation can |
| --- | --- | --- |
| `Queued` | Accepted and waiting. Possibly returned here by recovery after an earlier attempt | Commit first and move it to `Canceled` |
| `Running` | Claimed by an attempt that has done only reversible work | Record a stop request. The attempt must not cross cutoff 2 while one exists |
| `Delivering` | Delivery authorized for this attempt. The transfer may not have started, may be in progress, or may be finished | Only be recorded |
| `Canceled` | Canceled while queued. No further attempt will start | Only be recorded |
| `Rejected` | The worker's policy check denied the job while it was queued, before a new claim | Only be recorded |
| `Stopped` | An attempt stopped before delivery was authorized. Nothing was sent | Only be recorded |
| `Failed` | This job ended without delivering, with a failure code: a build error; attempts exhausted after repeated crashes (the console says "gave up after 3 attempts," not "build failed"); a delivery error that proves the file was never finalized; or, for a replacement, a final name already occupied, or the original's file found there (the console then says the delivery happened under the original job, not that it failed) | Only be recorded. A named operator can correct a recorded `Failed` or `Completed` once, from new evidence |
| `Completed` | Delivered, with evidence | Only be recorded, subject to the same one-time correction |
| `OutcomeUnknown` | Delivery was authorized, and nobody can yet say whether it happened. Unresolved, not final | Only be recorded. Reconciliation moves it to `Completed` or `Failed` when evidence arrives |

The second cutoff has the same shape as the first:

```csharp
// Running → Delivering, only while this attempt owns the job and nobody has asked it to stop.
int authorized = await db.ExportJobs
    .Where(j => j.Id == jobId
             && j.AttemptId == attemptId
             && j.Status == ExportStatus.Running
             && j.StopRequestId == null)
    .ExecuteUpdateAsync(set => set.SetProperty(j => j.Status, ExportStatus.Delivering),
        CancellationToken.None);   // let the statement finish during shutdown; it still may not answer
```

Its [full form](../../aspnetcore/queued-job-cancellation-recovery-implementation-reference.md#claim-and-authorization-in-one-statement-each) does more in the same single statement: it records what a delivered file must look like, so recovery later has something to compare against, requires a lease that is current when delivery is authorized, so a worker presumed dead cannot cross the point of no return, and inserts the attempt event. A worker that pauses just after authorization can still upload once its lease has expired, which is why delivery outcomes are settled by evidence rather than by the lease.

A stop request is a conditional write on the same row too, `Running` with no earlier stop request, so it and the authorization are serialized exactly as the claim and the cancellation were. If the stop request commits while the authorization statement is in flight, the authorization waits, re-evaluates, and matches nothing; if the authorization commits first, the stop request finds a job in `Delivering`.

Where to put the second cutoff is a design decision, and it is worth writing down. It belongs immediately before the first effect that you cannot undo, or cannot undo cheaply: the upload, the email, the payment request, the message to another team's system. Work before it should be safe to discard. Work after it should be reported, not abandoned halfway, unless you have decided what a half-finished effect means. If a job has several irreversible steps, decide whether each gets its own authorization write or the whole sequence proceeds once the first begins. An export of five files might authorize each file separately, so a stop request can still prevent the files not yet sent. A payment followed by a receipt email usually treats the email as part of the payment, so the email goes out even if a stop request arrives in between.

The second cutoff does not make cancellation work after delivery is authorized. It makes the boundary explicit, so that "too late" has a precise meaning too.

---

## After the Claim, Cancellation Is a Request

Once a job is `Running`, the console cannot stop it by writing a status. Only the worker can stop, and only at places in its code where stopping is safe. .NET's model for this is [cooperative cancellation](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads): the code that requests cancellation signals a token, and the code doing the work decides how and when to respond.

So the worker needs a way to hear the request, and places to act on it:

```csharp
// Inside the worker, after a confirmed claim. The complete runner is in the implementation reference.
using var stop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
await using StopRequestWatch watch = watcher.Watch(job.Id, attemptId, stop);   // signals on a recorded stop request

try
{
    ExportFile file = await builder.BuildAsync(job, decision.Scope, stop.Token);   // reversible: a local file

    // Cutoff 2. This write, not the watcher, guarantees that a stop request committed first prevents delivery.
    AuthorizationResult result = await jobs.AuthorizeDeliveryAsync(job.Id, attemptId, file.Expected);
    if (result == AuthorizationResult.Authorized)
    {
        await DeliverAsync(job.Id, attemptId, file, stoppingToken);   // past the point of no return
        return;
    }

    await file.TryDeleteAsync();
    if (result == AuthorizationResult.StopRecorded)
    {
        await jobs.TryFinishStoppedAsync(job.Id, attemptId, StopPoint.BeforeDelivery);
    }
    // A lapsed lease, lost ownership, or an unknown result: write nothing; recovery takes it from there.
}
catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
{
    await jobs.TryFinishStoppedAsync(job.Id, attemptId, StopPoint.WhileBuilding);   // the builder cleans up
}
```

The details that matter for cancellation:

- **Stopped means a recorded stop request.** `TryFinishStoppedAsync` moves `Running` to `Stopped` only for this attempt and only when `StopRequestId` is set. An attempt that has lost ownership writes nothing to the job row, so a lease takeover is never recorded as an operator's stop.
- **The watcher is for speed, not correctness.** It can poll the row every few seconds for a stop request, or subscribe to a notification and then confirm against the row. Either way, it lets a stopped export give up after three seconds instead of 24. If it misses the request, or the notification is lost, cutoff 2 still refuses to authorize delivery. Do not make a notification the only thing standing between a stop request and an upload.
- **Stopping happens only where it is safe.** `BuildAsync` passes the token down to the database query and the file writes, so they end promptly, and a local file that was never authorized for delivery is removed.
- **The upload is made safe to interrupt rather than cancellable.** Writing under a temporary name and renaming to the final name only after the transfer completes prevents partial publication under the name the partner processes. SFTP servers differ in how they rename, so agree the details with the partner. An interruption during or just after the rename still leaves the outcome unknown, and it is recorded as `OutcomeUnknown`. [A Payment API Timeout Leaves the Outcome Unknown](payment-api-timeout-unknown-outcome.md) covers why an interrupted external call needs its own state and is resolved only by evidence.
- **`Completed` needs evidence the partner's side can give**, and means the agreed delivery condition was met, not that the partner has processed the contacts. The [implementation reference](../../aspnetcore/queued-job-cancellation-recovery-implementation-reference.md#the-worker) discusses what counts.

Some effects do support cancellation after they begin: a provider's API to cancel a scheduled payout, a print queue that can drop a job before the run, a long database operation that rolls back. If yours does, a stop request in `Delivering` can turn into a call to that API, and that system's answer, not your intention, decides the outcome. Record the call and its answer as their own operation. [An Allowed Decision Does Not Mean the Operation Succeeded](allowed-decision-does-not-mean-execution-succeeded.md#compensation-is-another-operation) explains why undoing an effect is a new operation with its own decision, not a rewrite of the old one.

---

## When Cancellation Loses, Record That It Lost

A cancellation request is an event with its own identity, whatever it achieves. For a job the caller can see, record it every time, in the same short transaction that decides what it does. Three things make that record trustworthy. The transaction locks this tenant's job row before it classifies anything, so the state it records is the state its write acted on. The request ID comes from the client, is unique within the tenant, and is bound to the job and the operator, so a retry after a lost response returns the outcome already recorded, and the endpoint is safe to retry on a timeout. And every write checks its own row count.

The outcome follows from the state the locked row shows:

```csharp
CancellationOutcome outcome = found.Status switch
{
    ExportStatus.Queued => CancellationOutcome.CanceledWhileQueued,       // then Queued → Canceled
    ExportStatus.Running when found.StopRequestId is null => CancellationOutcome.StopRequested,   // then record the stop
    ExportStatus.Running => CancellationOutcome.StopAlreadyRequested,
    ExportStatus.Canceled => CancellationOutcome.AlreadyCanceled,
    ExportStatus.Rejected or ExportStatus.Stopped or ExportStatus.Failed => CancellationOutcome.AlreadyEnded,
    _ => CancellationOutcome.TooLate,   // Delivering, Completed, OutcomeUnknown: delivery was authorized
};
```

The [complete method](../../aspnetcore/queued-job-cancellation-recovery-implementation-reference.md#recording-cancellation-requests) takes the lock with a bounded wait, handles retries and reused request IDs, checks every write's row count, and records the request in the same short transaction. Keep that transaction short, because a worker's claim on the same row waits for it. A request for a job the caller cannot see is not recorded against any job; log it as an ordinary access event.

The job row records only the request that canceled it (`CancellationRequestId`) or the first stop request (`StopRequestId`). The `CancellationRequests` table records every request, including the ones that changed nothing. Attempt history lives in its own append-only table of attempt events, because recovery and later claims overwrite the job row's current-attempt columns: claimed (with the version checked and the worker), delivery authorized (with the expected file), stopped, finished, and delivery evidence. Each event that proves a transition is inserted in the same transaction as that transition, which is why the full claim and authorization are single statements that update the row and insert the event together. Delivery evidence is the exception: it is appended on its own, as an insert that never changes the job's status, recording whether a worker attempt or a recovery run observed it, when, and what it saw, so reconciliation can weigh it. None of these records is edited later to look like a different outcome, and the job is never rewritten from `Completed` to `Canceled` because someone wished it had been. The attempt's own terminal record links back to the stop request it honored, if any, so the full story is records that agree:

| Sam's request commits at | What happens | Job ends as | Request records | The console says |
| --- | --- | --- | --- | --- |
| 14:02:07.391 | Commits 21 ms before the worker's claim reaches the row | `Canceled` | `CanceledWhileQueued`, no prior attempts | "Canceled while queued. No attempt had started, and none will." |
| 14:02:07.430 | The claim committed first; the stop request then beats cutoff 2 | `Stopped`, while building | `StopRequested`, found attempt `5c2e…` running on worker 3 | "Already started at 14:02:07.412. Stopped at 14:02:10.452, before delivery was authorized. Nothing was sent." |
| 14:02:31.880 | Cutoff 2 committed first, at about 14:02:31.861 | `Completed` | `TooLate`, found attempt `5c2e…` in `Delivering` | "Too late: delivery was authorized at about 14:02:31.861, before your request. Delivery completed at 14:02:32.904: 48,112 contacts to partner-old. Your request CXL-303 is recorded." |

The first row is the opening incident with conditional writes, and nothing else changed. If recovery had returned the job to `Queued` after an earlier attempt, the same outcome would say "Canceled while queued. No further attempt will start," with the earlier attempt's ending beside it; "no attempt had started" is reserved for a job whose attempt count is zero.

The third row is not a failure of the design. It is the design telling the truth: a request 19 milliseconds after the point of no return can no longer guarantee that delivery will be prevented, and the operator who clicked needs to know that now, not on Monday. At the moment of the click, the console can say only that delivery was authorized. It reports "completed" once the attempt has recorded evidence, and "unknown" if that is what the attempt recorded. What to do about the file, asking the old agency to delete it and recording whether they confirm, is a new operation with its own owner.

The evidence for the second case reads like this:

```text
Requested   EXP-7731  by admin@acme (tenant-acme)      14:01:55.004  partner-old, 48,112 contacts, v1
Claimed     EXP-7731  attempt 5c2e…  by worker-3      14:02:07.412  checked v1, decision allowed
Cancel      CXL-302   EXP-7731  by sam.ops            14:02:07.430  "wrong destination"
                      outcome StopRequested; found attempt 5c2e… Running
Stopped     EXP-7731  attempt 5c2e…  by worker-3      14:02:10.452  while building, for CXL-302
                      delivery never authorized; local file removal confirmed
```

The times are each host's own readings, recorded for people. The order that matters is already settled by the row, and the records say so in words: "found attempt `5c2e…` Running," "for CXL-302."

### What the API returns

The cancel endpoint should return the outcome, not an echo of the request. One reasonable mapping:

| Outcome | Response | Body includes |
| --- | --- | --- |
| `CanceledWhileQueued` | `200 OK` | The request ID, and whether any attempt had ever started |
| `AlreadyCanceled` | `200 OK` | That the job was already canceled, by which request and when. This request changed nothing |
| `StopRequested`, `StopAlreadyRequested` | `202 Accepted` | The request ID, the running attempt, and a status URL to poll for the attempt's ending |
| `AlreadyEnded` | `409 Conflict` | The job's ending (`Rejected`, `Stopped`, or `Failed`), the stop request it honored if any, and that this job delivered nothing; for a replacement that found the original's file, that the delivery happened under the original job |
| `TooLate` | `409 Conflict` | The job's current state, roughly when delivery was authorized, and the delivery outcome if it is known yet |
| `RequestIdReused` | `400 Bad Request` | That this request ID belongs to a different request. Nothing about that request |
| `NotFound` | `404 Not Found` | Nothing about whether a job exists in another tenant |
| `Busy` (lock timeout) | `503 Service Unavailable`, with `Retry-After` | That this submission made no new decision and recorded nothing. Retry with the same request ID; an earlier submission may already have committed, and the retry then returns its outcome |

Whatever the status codes, the console should say "canceled" only for the first row, and should name the earlier request for the second. "Already canceled by CXL-301," "cancellation requested," and "too late" are different facts, and the operator will repeat whichever one the screen shows to a customer.

---

## Recovery Must Not Undo a Cancellation or Repeat a Delivery

A worker can crash, or pause long enough to be presumed dead, in any state. A production design gives each attempt a lease, renewed while it works, and a recovery process that takes over a job only through a conditional write that repeats every condition it relied on, including the lease. Cancellation adds four rules to recovery:

- **A `Running` attempt with a stop request is finished, not restarted.** `Running` means cutoff 2 was never committed, so nothing was delivered. Recovery moves the job to `Stopped`.
- **A `Running` attempt without one may be returned to `Queued`,** with a new message, until an attempt cap ends a job that crashes its worker every time. A cancellation that arrives afterward competes with the next claim as usual.
- **A `Delivering` attempt is resolved, not stopped, canceled, or retried.** It may have crashed before sending a byte, or sent everything and lost the receipt. Recovery records `Completed` only on evidence, never concludes `Failed` from an absent file alone, because a paused worker can outlast any timeout, and otherwise leaves the job `OutcomeUnknown` for reconciliation. A cancellation request against it is still `TooLate`.
- **A replacement delivery is a new decision.** Recovery reconciles the original attempt and never starts a replacement itself. A named, authorized person may then create one, as a [new job](../../aspnetcore/queued-job-cancellation-recovery-implementation-reference.md#recovery) that keeps the original's partner-facing delivery key, but only when the original attempt can no longer write or upload and the partner's contract makes a duplicate harmless.

Recovery is also a writer to the job row, so it follows the same invariant as everything else: no unconditional status changes. The [implementation reference](../../aspnetcore/queued-job-cancellation-recovery-implementation-reference.md#recovery) gives the lease, renewal, and takeover SQL, explains why an expired lease makes an attempt eligible for takeover without ending its ownership, and shows how late delivery evidence is kept and weighed.

---

## Testing the Race, Not Just the Rule

Tests that call cancel and then run the worker, one after the other, prove the rule and miss the race. The race needs two transactions contending for one row in the real database engine, each with its own `DbContext`, connection, and transaction. A fake that serializes calls, or two writers sharing one connection, cannot show it. Both writers can run from one test process. These tests are specifications of the behavior described above, not evidence of a working implementation, and each shows the behavior only under the conditions it sets up. The harness methods take fewer arguments than the production ones; they supply the tenant, operator, and a fresh request ID themselves.

```csharp
[Theory]
[InlineData(Writer.Cancel)]
[InlineData(Writer.Claim)]
public async Task The_second_writer_waits_for_the_first_and_then_cannot_leave_Queued(Writer first)
{
    Guid job = await harness.QueueExportAsync();   // one PostgreSQL database for the class; a fresh job per test

    await using WriterSession a = await harness.OpenSessionAsync();   // own context, connection, transaction
    await using WriterSession b = await harness.OpenSessionAsync();

    // Test-only: the first writer's transaction is held open so the test can keep its row lock. In
    // production, the claim's short transaction commits immediately.
    await a.BeginTransactionAsync();
    await a.WriteAsync(first, job);                                   // UPDATE sent, not yet committed

    Task<WriteResult> second = b.WriteInOwnTransactionAsync(first.Other(), job);
    await harness.WaitUntilBlockedByAsync(b, a, TimeSpan.FromSeconds(5));   // pg_blocking_pids(b) contains a; fails on timeout
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
    harness.Worker.AfterLoad = () => harness.Console.CancelAsync(job, "wrong destination");
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

    harness.Worker.BeforeAuthorizeDelivery = () => harness.Console.CancelAsync(job, "late");
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
        result = await harness.Console.CancelAsync(job, "late");
    await harness.Worker.ProcessNextMessageAsync();

    Assert.Equal(CancellationOutcome.TooLate, result!.Outcome);
    Assert.Equal(ExportStatus.Delivering, result.Found.Status);
    Assert.Equal(ExportStatus.Completed, await harness.StatusAsync(job));   // not rewritten to Canceled
    Assert.Single(harness.Destination.Uploads);
}
```

The first test is deterministic: it holds one writer's transaction open, confirms with `pg_blocking_pids` that the other is waiting on that session in particular, within a bounded wait, and only then commits. Run it for both orders, and add a variant where the first writer rolls back, after which the second must succeed. A loop that releases two tasks from one gate many times can still be useful as a soak test, against one database with a fresh job per iteration, but it shows overlap only by chance.

Also cover:

- **Stop request against cutoff 2.** The same held-transaction technique, with the stop request and the authorization as the two writers.
- **Redelivery.** The message for a canceled, stopped, completed, or unknown job does nothing and is settled.
- **A changed request.** Changing the destination increments `Version`, so a worker that checked the old terms fails its claim and builds nothing.
- **Every writer.** Each path that can change `Status`, including administrative and repair paths, refuses to move a `Canceled` job back to `Queued` or `Running`. An architecture test can fail the build when code outside the job store writes the status column.

The implementation reference's [test matrix](../../aspnetcore/queued-job-cancellation-recovery-implementation-reference.md#test-matrix) adds lost database responses, recovery interleavings and leases, interrupted uploads by where they stop, and the cancellation request record.

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

**2. A version nobody increments, or a job left without a message.** The endpoint that changes the destination does not bump `Version`, so the claim passes on terms nobody evaluated; or it bumps the version but publishes nothing, so the job waits in `Queued` forever.

**3. A watcher trusted for correctness.** The stop notification is lost, or the poll interval is longer than the remaining build, and nothing else prevents the upload.

**4. Rewriting history.** A job that delivered is marked `Canceled` because someone requested cancellation, or a request that changed nothing is deleted. The record no longer says what happened.

**5. Recovery that restarts what someone asked to stop, delivers twice, or never gives up.** A crashed `Running` attempt with a stop request is requeued, a `Delivering` attempt is retried because no file was found, or a job that crashes its worker is requeued forever.

**6. Treating an interrupted effect as stopped or failed.** An upload cut off by shutdown is recorded as `Stopped` or `Failed`, when nobody knows whether the final file was written.

---

## A Short Review Checklist

**The race**

1. Is the claim a single conditional write on the job's state and the version that was checked, and does work begin only after it has committed?
2. Is cancellation a conditional write on the same row, so it and the claim cannot both leave `Queued`?
3. Does every writer of the status, including recovery and repair paths, use a condition, and does every change to the job's terms increment its version?
4. Is the order decided by the database's commit order on the primary, not by timestamps or replica reads, and is a serialization failure handled by retrying the whole transaction?
5. When a write's response is lost, does the worker re-read state and ownership instead of assuming it failed?

**The work in progress**

6. Is there a second conditional write immediately before the first effect that cannot be undone, conditioned on the attempt and on the absence of a stop request?
7. Is work before that boundary safe to discard, and is it cleaned up when the attempt does not cross it?
8. Does correctness hold if the watcher never fires?
9. Is the effect made safe to interrupt, and is an interrupted effect recorded as unknown?

**The records**

10. Is each cancellation decided under a lock on the job row, with a client-supplied request ID, and recorded with the state and attempt it found?
11. Is a job that ran never rewritten as canceled, and is a request that changed nothing still kept?
12. Does the console distinguish "canceled while queued," "already canceled," "stop requested," and "too late," and say what was delivered, once that is known?

**Recovery and proportion**

13. Does recovery stop a `Running` attempt that has a stop request, resolve a `Delivering` attempt only from evidence, and never start a second delivery merely because an attempt is old? The implementation reference's [review checklist](../../aspnetcore/queued-job-cancellation-recovery-implementation-reference.md#implementation-review-checklist) covers leases, the outbox, and the request record in detail.
14. Does the design match the job? Two `WHERE` clauses may be all it needs.

---

## Continue Deeper

To build this, start with [Queued Job Cancellation and Recovery in ASP.NET Core: An Implementation Reference](../../aspnetcore/queued-job-cancellation-recovery-implementation-reference.md). It turns each rule here into a schema, single-statement claims, leases and recovery, an outbox and sweeper, a complete worker and cancellation method, and a test matrix.

For what a queued job should carry, how the worker re-checks the requester's authority before it claims, and why delivery is not authority, read [A Background Job Should Not Inherit Everything the User Could Do](background-job-should-not-inherit-user-authority.md). [Your Security Check Passed. Then the World Changed.](security-check-passed-then-world-changed.md) develops the check-then-claim pattern in this article and covers the facts that a conditional write on one row cannot see.

When the job carries a one-time grant rather than relying on a job row, cancellation becomes revocation, and it contends with consumption of the grant. [An In-Memory Grant Store Is Not Distributed Replay Protection](in-memory-grant-store-is-not-distributed-replay-protection.md) explains why that consumption has to be shared, durable, and atomic across instances, and [Capability-Scoped Background Operation](../../case-studies/capability-scoped-background-operation.md#15-revocation-is-useful-but-has-a-boundary) defines what happens when revocation and claim race for the same grant.

If cancellation loses after an external call has begun, [A Payment API Timeout Leaves the Outcome Unknown](payment-api-timeout-unknown-outcome.md) covers how to resolve an outcome that nobody observed, and [Your Retry Logic May Be Replaying an Authorization Decision](retry-logic-may-replay-authorization-decision.md) covers what each retry and recovery layer can and cannot know.

### Related Work

- [Transaction Isolation](https://www.postgresql.org/docs/current/transaction-iso.html), [Serialization Failure Handling](https://www.postgresql.org/docs/current/mvcc-serialization-failure-handling.html), and [Asynchronous Commit](https://www.postgresql.org/docs/current/wal-async-commit.html), in the PostgreSQL documentation, describe how a concurrent `UPDATE` waits and re-evaluates its `WHERE` clause, why a failed serializable transaction is retried as a whole, and when an acknowledged commit can still be lost.
- [ExecuteUpdate and ExecuteDelete](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete), in the EF Core documentation, covers set-based conditional updates, their row counts, transactions, and why concurrency control is your responsibility, and [Connection Resiliency](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency) explains why a commit interrupted by a dropped connection leaves its outcome unknown.
- [Cancellation in Managed Threads](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads) describes .NET's cooperative cancellation model.
- [Message transfers, locks, and settlement](https://learn.microsoft.com/en-us/azure/service-bus-messaging/message-transfers-locks-settlement), in the Azure Service Bus documentation, explains peek-lock, lock expiry, redelivery, and the maximum delivery count, and [Message sequencing and timestamps](https://learn.microsoft.com/en-us/azure/service-bus-messaging/message-sequencing) documents canceling scheduled messages by sequence number and its race with activation.
- [DeleteMessage](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/APIReference/API_DeleteMessage.html), in the Amazon SQS API reference, describes deletion by receipt handle and occasional redelivery after deletion from standard queues.
- [How to do distributed locking](https://martin.kleppmann.com/2016/02/08/how-to-do-distributed-locking.html), by Martin Kleppmann, explains why a lease-based lock needs a fencing token that the protected resource checks.
- [Using cancellation tokens](https://docs.hangfire.io/en/latest/background-methods/using-cancellation-tokens.html), in the Hangfire documentation, describes how a running job learns of shutdown or an aborted job, and how often the server checks.
- [CWE-367: Time-of-check Time-of-use (TOCTOU) Race Condition](https://cwe.mitre.org/data/definitions/367.html) describes the general weakness behind the opening incident.

---

## The Short Answer

Canceling a queued job is a race with the worker about to start it, and neither the queue message nor a status column decides that race on its own. Make starting and canceling competing conditional writes on the same row, so at most one can move the job out of `Queued`, and start work only after the claim has committed. If the job does reversible work before an effect that cannot be undone, add a second conditional write at that boundary, which a stop request can still beat.

After the claim, cancellation is a request, not a state. Record every request with what it found, let the worker stop only where stopping is safe, and tell the operator plainly whether the job was canceled while queued, stopped before delivery was authorized, or had already passed that point. For many jobs, that is two `WHERE` clauses and one honest message.
