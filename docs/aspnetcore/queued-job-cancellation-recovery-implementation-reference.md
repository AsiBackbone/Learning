---
description: Implement queued-job cancellation in ASP.NET Core with PostgreSQL, covering single-statement claims, leases, recovery, the outbox, request records, and tests.
---

# Queued Job Cancellation and Recovery in ASP.NET Core: An Implementation Reference

**Learning objective:** Implement the rules from [Canceling a Queued Job Can Race With Execution](../articles/2026/revoke-job-after-it-enters-queue.md) in an ASP.NET Core worker with a relational store: claim and authorize delivery in single conditional statements that record their own history, hold work with leases that recovery can take over safely, keep queued work from being stranded by settled messages, record every cancellation request under a row lock, and test the paths that rarely happen in development.

**Pattern classification:** General learning material

**Difficulty:** Advanced

**Prerequisites:** [Canceling a Queued Job Can Race With Execution](../articles/2026/revoke-job-after-it-enters-queue.md), which explains why each rule here exists. Familiarity with ASP.NET Core, `BackgroundService`, EF Core, and PostgreSQL transactions is assumed. [Data-Access Boundaries and Transaction Reasoning with EF Core](data-access-boundaries-and-transaction-reasoning.md) covers the transaction background.

**Sample scope:** The C# targets .NET 8 or later and uses EF Core's `ExecuteUpdateAsync` and, in its current form, `FromSql`, both available from EF Core 7. The SQL uses PostgreSQL syntax, and statements about concurrency, locking, and time functions describe PostgreSQL's documented behavior; confirm the equivalent for your database and isolation level. The CRM, the export, and the partner are the hypothetical ones from the article. The stores, builder, destination client, watcher, and test harness are illustrative sketches, not a runnable sample or a recommended persistence API. The SQL blocks on this page were run against PostgreSQL 16, covering the constraints, creation, replacement, recovery, correction, and sweeper cases in the test matrix, including both lock orders between a correction and a replacement's cutoff 2; the C# has been reviewed but not compiled. Authenticating the operator and authorizing their access to the tenant, the outbox dispatcher, the scheduling of the sweeper and recovery runs, and the orphan-file scavenger are omitted. Size connection pools and command timeouts with the row locks in mind: a cancellation that holds a job row briefly can still make claims on that row wait. The explicit transactions assume no retrying EF Core execution strategy; with one enabled, run each whole transaction through `CreateExecutionStrategy().ExecuteAsync`, as the [connection resiliency documentation](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency) describes, with a fresh `DbContext` from a factory created inside the retried delegate, and keep the client's request ID the same across those retries. The injected-context methods below are not retry-safe as written. This is not legal, privacy, or compliance advice for exporting personal data. Original prose is licensed under [Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/), and original code examples are additionally offered under the [MIT License](https://opensource.org/license/mit), as described in the repository's [licensing terms](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md). Third-party material, where identified, remains under its original license. External sources were last verified on November 14, 2026.

The article makes the argument: canceling a queued job is a race with the worker about to start it, settled by competing conditional writes on one row, with a second cutoff before the first effect that cannot be undone, and cancellation after the claim treated as a request. This page is the implementation that argument implies once crashes, paused workers, lost database responses, redelivered messages, and operators who click twice are involved.

The design rests on a handful of invariants, each enforced in one place below:

- **Every writer is conditional.** The claim, cancellation, stop request, delivery authorization, lease renewal, recovery, terms edits, and the sweeper change the row only from the state, attempt, version, or lease they checked.
- **A transition and its proof commit together.** Each state change that an attempt event proves is one statement with that event, using a data-modifying `WITH` clause. Where a change needs several statements, such as recording a cancellation request or editing a job's terms with its outbox row, they share one short transaction.
- **Ownership ends only by takeover.** An expired lease makes an attempt eligible for takeover; a committed takeover ends it. The irreversible boundary additionally requires a current lease.
- **No queued job without a live message.** Every write that leaves a job queued arranges a new message through the outbox, and a sweeper backs that up.
- **Uncertainty is recorded, not rounded off.** A lost database response is resolved by re-reading the row; an interrupted delivery is `OutcomeUnknown` until evidence resolves it, and late evidence is kept.

---

## The Tables

Five tables carry the design. The job row holds current state, `deliveries` records each delivery key once, and the other three are append-only records.

```sql
CREATE TABLE export_jobs (
    id                       uuid PRIMARY KEY,          -- globally unique
    tenant_id                uuid NOT NULL,
    status                   text NOT NULL CHECK (status IN ('Queued', 'Running', 'Delivering', 'Canceled',
                                 'Rejected', 'Stopped', 'Failed', 'Completed', 'OutcomeUnknown')),
    version                  int  NOT NULL,             -- version of the terms; every terms edit increments it
    destination_id           uuid NOT NULL,
    attempt_id               uuid,                      -- current attempt, if any
    attempt_count            int  NOT NULL DEFAULT 0,
    claimed_by               text,
    lease_expires_at         timestamptz,
    stop_request_id          uuid,                      -- the first stop request
    cancellation_request_id  uuid,                      -- the request that canceled it while queued
    expected_file_name       text,                      -- recorded at cutoff 2
    expected_file_bytes      bigint,
    expected_file_sha256     bytea,
    delivery_authorized_at   timestamptz,
    woken_at                 timestamptz NOT NULL,      -- last wake-up message written for this job
    failure_reason           text CHECK (failure_reason IN ('BuildFailed', 'AttemptsExhausted', 'NotDelivered',
                                 'FinalNameOccupied', 'OriginalDelivered')),   -- codes, so alerts and logic can rely on them
    delivery_key             text NOT NULL,             -- partner-facing identity; the final file name is built from it
    replaces_job_id          uuid,                      -- set on a replacement job; always names the original
    created_by               text NOT NULL,             -- the operator or client that created the job
    replacement_reason       text,                      -- replacements only: why, in the creator's words
    overwrite_agreement_ref  text,                      -- replacements only: the partner's recorded overwrite agreement
    sweep_count              int  NOT NULL DEFAULT 0,   -- times the sweeper republished this job
    CONSTRAINT uq_export_jobs_tenant UNIQUE (tenant_id, id),
    CONSTRAINT ck_export_jobs_replacement CHECK ((replaces_job_id IS NULL) = (replacement_reason IS NULL)),
    CONSTRAINT ck_export_jobs_overwrite CHECK (overwrite_agreement_ref IS NULL OR replaces_job_id IS NOT NULL),
    CONSTRAINT ck_export_jobs_failure CHECK ((status = 'Failed') = (failure_reason IS NOT NULL))
);

-- One row per delivery key, written by the statement that creates the original job.
CREATE TABLE deliveries (
    tenant_id        uuid NOT NULL,
    delivery_key     text NOT NULL,
    original_job_id  uuid NOT NULL,
    CONSTRAINT pk_deliveries PRIMARY KEY (tenant_id, delivery_key),
    CONSTRAINT uq_deliveries_original UNIQUE (tenant_id, delivery_key, original_job_id),
    CONSTRAINT fk_deliveries_original FOREIGN KEY (tenant_id, original_job_id)
        REFERENCES export_jobs (tenant_id, id) DEFERRABLE INITIALLY DEFERRED   -- the two rows reference each other
);

ALTER TABLE export_jobs
    ADD CONSTRAINT fk_export_jobs_delivery FOREIGN KEY (tenant_id, delivery_key)
        REFERENCES deliveries (tenant_id, delivery_key),
    ADD CONSTRAINT fk_export_jobs_replaces FOREIGN KEY (tenant_id, delivery_key, replaces_job_id)
        REFERENCES deliveries (tenant_id, delivery_key, original_job_id);   -- same tenant, same key, the original

-- Only an original mints a key, so two originals can never share one, even if inserted directly.
CREATE UNIQUE INDEX uq_export_jobs_original_key ON export_jobs (tenant_id, delivery_key) WHERE replaces_job_id IS NULL;

-- At most one replacement per original that is live, has delivered, or found the original's file. One that
-- ended without delivering anything frees the slot.
CREATE UNIQUE INDEX uq_export_jobs_one_replacement ON export_jobs (tenant_id, replaces_job_id)
    WHERE replaces_job_id IS NOT NULL
      AND (status NOT IN ('Canceled', 'Rejected', 'Stopped', 'Failed') OR failure_reason = 'OriginalDelivered');

-- Recovery finds expired leases; the sweeper finds queued jobs whose last wake-up is old.
CREATE INDEX ix_export_jobs_lease ON export_jobs (lease_expires_at) WHERE status IN ('Running', 'Delivering');
CREATE INDEX ix_export_jobs_woken ON export_jobs (woken_at) WHERE status = 'Queued';

CREATE TABLE export_attempt_events (
    id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    job_id       uuid NOT NULL REFERENCES export_jobs (id),
    attempt_id   uuid NOT NULL,
    kind         text NOT NULL CHECK (kind IN ('Claimed', 'DeliveryAuthorized', 'Stopped', 'Failed',
                     'Finished', 'RequeuedByRecovery', 'StoppedByRecovery', 'AttemptsExhausted',
                     'ResolvedByRecovery', 'Reconciled', 'Corrected', 'DeliveryEvidence')),
    version      int,
    worker_id    text,
    observed_by  text,               -- for delivery evidence: the worker attempt or the recovery run
    detail       text,
    recorded_at  timestamptz NOT NULL
);

-- Each transition happens at most once per attempt; evidence may be appended many times.
CREATE UNIQUE INDEX uq_attempt_transition ON export_attempt_events (job_id, attempt_id, kind)
    WHERE kind <> 'DeliveryEvidence';

CREATE TABLE cancellation_requests (
    tenant_id                     uuid NOT NULL,
    id                            uuid NOT NULL,       -- supplied by the client, once per click
    job_id                        uuid NOT NULL,
    requested_by                  text NOT NULL,
    reason                        text,
    outcome                       text NOT NULL,
    found_status                  text NOT NULL,
    found_attempt_id              uuid,
    found_attempt_count           int  NOT NULL,
    found_delivery_authorized_at  timestamptz,
    recorded_at                   timestamptz NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT pk_cancellation_requests PRIMARY KEY (tenant_id, id),
    CONSTRAINT fk_cancellation_requests_job FOREIGN KEY (tenant_id, job_id)
        REFERENCES export_jobs (tenant_id, id)            -- a request cannot name another tenant's job
);

CREATE TABLE outbox (
    message_id    uuid PRIMARY KEY,    -- a new ID for every row; reused only when the dispatcher retries this row
    kind          text NOT NULL,
    job_id        uuid NOT NULL,
    created_at    timestamptz NOT NULL,
    published_at  timestamptz
);
```

The two history tables, `export_attempt_events` and `cancellation_requests`, are strictly append-only, and so is `deliveries`: grant the application role `INSERT` and `SELECT` on them and nothing else. The outbox is insert-only except for its publication status: grant the dispatcher's role column-level `UPDATE (published_at)` and no `DELETE`. Then "append-only" is enforced by the database rather than by convention, and a test can prove the roles lack the rest.

Job creation follows the same rules as every other writer: the job and its first wake-up are written in one statement, so an accepted export always has a message on its way. The API mints `delivery_key` here, for example from a new GUID, and it never changes afterward, not even when a terms edit changes the destination; the final file name is derived from it, for example `contacts-{delivery_key}.csv`, so @final_name at cutoff 2 is a function of the key. The same statement records the key in `deliveries` with this job as its original:

```sql
WITH delivery AS (
    INSERT INTO deliveries (tenant_id, delivery_key, original_job_id)
    VALUES (@tenant_id, @delivery_key, @job_id)),
created AS (
    INSERT INTO export_jobs (id, tenant_id, status, version, destination_id, delivery_key, created_by, woken_at)
    VALUES (@job_id, @tenant_id, 'Queued', 1, @destination_id, @delivery_key, @created_by, clock_timestamp())
    RETURNING id)
INSERT INTO outbox (message_id, kind, job_id, created_at)
SELECT gen_random_uuid(), 'ExportReady', id, clock_timestamp() FROM created;
```

A replacement has its own creation statement, run for a named person. It copies the key from the original, which it locks, and inserts nothing unless the original may be replaced. The original must be an original, in the same tenant, and either `OutcomeUnknown` or `Failed` for a reason that proves nothing was finalized:

```sql
WITH original AS (
    SELECT id, tenant_id, destination_id, delivery_key
    FROM export_jobs
    WHERE tenant_id = @tenant_id AND id = @original_job_id AND replaces_job_id IS NULL
      AND (status = 'OutcomeUnknown'
           OR (status = 'Failed' AND failure_reason IN ('BuildFailed', 'AttemptsExhausted', 'NotDelivered')))
    FOR UPDATE),   -- waits for a concurrent correction of the original, then re-checks its status
created AS (
    INSERT INTO export_jobs (id, tenant_id, status, version, destination_id, delivery_key, replaces_job_id,
                             created_by, replacement_reason, overwrite_agreement_ref, woken_at)
    SELECT @job_id, tenant_id, 'Queued', 1, destination_id, delivery_key, id,
           @created_by, @replacement_reason, @overwrite_agreement_ref, clock_timestamp()
    FROM original
    RETURNING id)
INSERT INTO outbox (message_id, kind, job_id, created_at)
SELECT gen_random_uuid(), 'ExportReady', id, clock_timestamp() FROM created;
```

The API resolves a request to replace a replacement to the original it replaced. The database backs that up: the foreign key to `deliveries` refuses a replacement whose tenant or key differs from its original's, or one that names another replacement. `uq_export_jobs_one_replacement` is the concurrency guard. Of two simultaneous creations, one fails on the index. A replacement that completed, or that found the original's file, keeps its slot for good, so nobody can queue another delivery that has already happened. The statement checks only what the database can see. Whether the original attempt can still upload, and whether the partner's contract makes a duplicate harmless, remain the creator's judgment, recorded in `replacement_reason`. `overwrite_agreement_ref` points to the record of who at the partner agreed to an overwrite, when, and under which contract; without it, the replacement never overwrites.

---

## Claim and Authorization in One Statement Each

The article shows the claim and the delivery authorization as minimal conditional updates. Their full forms also record the attempt event, and the claim sets the lease. Each is one data-modifying statement, so the update and its event commit together. The `INSERT` reads only the rows the `UPDATE` returned, so an update that matches nothing inserts nothing, and one inserted row means the transition happened:

```sql
-- Claim: Queued → Running for the checked version, with the lease and the attempt event.
WITH claimed AS (
    UPDATE export_jobs
    SET status = 'Running', attempt_id = @attempt_id, attempt_count = attempt_count + 1,
        claimed_by = @worker_id, lease_expires_at = clock_timestamp() + interval '60 seconds'
    WHERE id = @job_id AND status = 'Queued' AND version = @checked_version
    RETURNING id, attempt_id, version, claimed_by)
INSERT INTO export_attempt_events (job_id, attempt_id, kind, version, worker_id, recorded_at)
SELECT id, attempt_id, 'Claimed', version, claimed_by, clock_timestamp() FROM claimed;
```

```sql
-- Cutoff 2: Running → Delivering for this attempt, only with no stop request and a current lease. For a
-- replacement, also only while its original is not Completed. FOR SHARE conflicts with the correction's
-- update of the original, so whichever commits first makes the other wait; this statement then re-checks.
WITH original_open AS (
    SELECT 1 FROM export_jobs
    WHERE tenant_id = @tenant_id AND id = @replaces_job_id::uuid AND status <> 'Completed'
    FOR SHARE),
authorized AS (
    UPDATE export_jobs
    SET status = 'Delivering',
        expected_file_name = @final_name, expected_file_bytes = @length, expected_file_sha256 = @sha256,
        delivery_authorized_at = clock_timestamp()
    WHERE id = @job_id AND attempt_id = @attempt_id AND status = 'Running'
      AND stop_request_id IS NULL
      AND lease_expires_at > clock_timestamp()
      AND (@replaces_job_id::uuid IS NULL OR EXISTS (SELECT 1 FROM original_open))   -- typed, so a null binds
    RETURNING id, attempt_id, delivery_authorized_at)
INSERT INTO export_attempt_events (job_id, attempt_id, kind, detail, recorded_at)
SELECT id, attempt_id, 'DeliveryAuthorized', @final_name, delivery_authorized_at FROM authorized;
```

`TryAuthorizeDeliveryAsync` sends that statement with `CancellationToken.None`, so a shutdown lets it finish rather than abandoning it (it still may not answer), and returns whether one row was inserted. `delivery_authorized_at` is recorded so the console can tell an operator roughly when the boundary was crossed. Commit order, not this value, decides who won.

The replacement condition closes a gap the creation statement cannot. Creation locks the original, so a correction to `Completed` that commits first stops it. But a creation that commits first leaves a queued replacement, and a correction can commit after it. Cutoff 2 is where that replacement would first affect the partner, so the check sits there. If the correction commits first, the replacement is refused before anything leaves the process. If the authorization commits first, the correction waits for it, and the replacement is already past cutoff 2. From then on, its check of the final name and the partner's deduplication contract govern the upload. A queued replacement is not stopped by the correction; it is refused when it reaches cutoff 2. The claim deliberately does not apply the condition: a refused claim writes nothing, so the replacement would stay `Queued` and the sweeper would republish it forever, while a refusal at cutoff 2 has an attempt to record it with a terminal write. The cost is one wasted build in a rare race.

When either call throws, or returns `0` after a retry, the worker cannot tell whether it committed:

**A lost response is not a failure.** A connection can drop after the database commits and before the client hears about it, and EF Core's [connection resiliency documentation](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency) describes the resulting transaction state as unknown. An execution strategy that retries the claim can even get `0` back from a claim that has already succeeded. So when a claim or a later boundary write throws, or returns `0` after a retry, re-read the row, and confirm state and ownership together: the claim succeeded only if the row is `Running` with this attempt's ID, and delivery was authorized only if it is `Delivering` with this attempt's ID. A matching ID in any other state, or a different ID, means the attempt has ended or been taken over, and the worker writes nothing further to the job row, not even a terminal state. It may still append evidence of what it did, as delivery below shows. If the re-read fails too, stop and let recovery resolve the job. Never infer from an exception that the write did not happen, and never repeat an external call because a database write was uncertain.

The claim wrapper shows the shape:

```csharp
public async Task<ClaimResult> ClaimAsync(
    Guid jobId, int checkedVersion, Guid attemptId, string workerId, CancellationToken ct)
{
    try
    {
        // Sql.Claim returns the statement above as a FormattableString, so its values are parameters. If an
        // EF Core execution strategy retries a failed attempt, a claim that had already committed returns 0.
        int inserted = await db.Database.ExecuteSqlAsync(Sql.Claim(jobId, checkedVersion, attemptId, workerId), ct);
        if (inserted == 1)
        {
            return ClaimResult.Claimed;
        }
    }
    catch (Exception ex) when (IsConnectionOrTimeout(ex))
    {
        // The statement may or may not have committed. Ask the row, not the exception.
    }

    try
    {
        // Bounded: a worker must not hang here during shutdown with the database unreachable.
        using var reread = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var row = await db.ExportJobs.AsNoTracking()
            .Where(j => j.Id == jobId)
            .Select(j => new { j.Status, j.AttemptId })
            .SingleOrDefaultAsync(reread.Token);

        // State and ownership together: Running with this attempt's ID, and nothing else, means claimed.
        return row is { Status: ExportStatus.Running } && row.AttemptId == attemptId
            ? ClaimResult.Claimed
            : ClaimResult.Lost;
    }
    catch (Exception ex) when (IsConnectionOrTimeout(ex) || ex is OperationCanceledException)
    {
        return ClaimResult.Unknown;   // the caller leaves the message unsettled
    }
}

// Transport failures, timeouts, and errors Npgsql marks transient (PostgresException derives from NpgsqlException,
// so a transient server error counts too; check IsTransient's behavior in your Npgsql version). Constraint
// violations and other database errors are real answers,
// and a shutdown cancellation of `ct` propagates instead: the message is then redelivered, and if the claim
// had committed, the redelivery finds the job Running and recovery takes the attempt over when its lease expires.
private static bool IsConnectionOrTimeout(Exception ex) =>
    ex is TimeoutException or IOException or NpgsqlException { IsTransient: true }
    || ex.InnerException is TimeoutException or IOException or NpgsqlException { IsTransient: true };
```

`Lost` normally means another delivery claimed the job, or it was canceled or edited. If the job is in fact still `Queued`, for a reason this worker cannot see, the sweeper wakes it up again later.

`AuthorizeDeliveryAsync` follows the same shape. Its re-read selects `status`, `attempt_id`, `stop_request_id`, whether `lease_expires_at > clock_timestamp()`, and, for a replacement, its original's `status`, and it reports why delivery was refused: `StopRecorded` when a stop request is recorded, `LeaseLapsed` when the attempt still owns the job but its lease has expired, and `NotOwner` when another attempt or a terminal state holds the row, and, for a replacement, `OriginalCompleted` when its original is `Completed`. The re-read is not atomic with the refusal, so a renewal can land in between and leave a row that is `Running`, owned by this attempt, unstopped, and with a current lease; that case is treated as `LeaseLapsed`, with its one retry.

The terminal writes follow it too, each inserting one event kind in the same statement: `TryFinishStoppedAsync` inserts `Stopped`, `TryFinishFailedAsync(jobId, attemptId, code, cause)` inserts `Failed`, with the failure code on the row and the exception, if any, in the event's detail, and `TryFinishDeliveryAsync` inserts `Finished` with the outcome in its detail. Every write to `Failed` sets one failure code, and a check constraint ties the two together, so a `Failed` row always has a code and no other row has one: `BuildFailed` from the builder, `AttemptsExhausted` from recovery, `NotDelivered` when the destination or reconciliation proves the final name was never written, and `FinalNameOccupied` or `OriginalDelivered` from the checks described under [the worker](#the-worker). Their re-reads confirm the expected terminal state with this attempt's ID. A repeated call cannot add a second event, because the status no longer matches and the unique index would refuse one anyway.

---

## Leases, Expiry, and Ownership

Each attempt holds a lease, set by the claim and renewed by a background task in the worker from the confirmed claim until the attempt's last write, as [the worker](#the-worker) shows. Renewal is conditional, so a worker that resumes after a takeover cannot extend a lease it no longer holds. The keeper and the stop watcher each use their own `DbContext`, so their polling never overlaps the worker's other database calls, and the keeper serializes its timer with on-demand renewals such as `TryRenewNowAsync`, since one context cannot run two operations at once. A renewal that throws is retried at the next interval and does not stop anything, so a network blip does not abort a healthy build. The worker calls the keeper's `FinishAsync` right after the attempt's terminal write, the one to `Stopped`, `Failed`, `Completed`, or `OutcomeUnknown`, or as soon as it gives up without one. `FinishAsync` stops the timer and waits for any renewal in flight, and a renewal that then finds no row is ignored rather than reported as lost ownership. The keeper is disposed when the runner returns. A renewal that changes no row means the attempt has lost ownership: before cutoff 2 the worker stops at once; during delivery the upload cannot be stopped safely, so the worker finishes it, appends its evidence, and lets recovery reconcile, because its terminal write will match nothing:

```sql
-- Renewal, every few seconds while the attempt works. It deliberately does not check expiry.
UPDATE export_jobs
SET lease_expires_at = clock_timestamp() + interval '60 seconds'
WHERE id = @job_id AND attempt_id = @attempt_id AND status IN ('Running', 'Delivering');
```

PostgreSQL's `now()` is [fixed at the start of the transaction](https://www.postgresql.org/docs/current/functions-datetime.html#FUNCTIONS-DATETIME-CURRENT), so lease arithmetic here uses `clock_timestamp()`, which reads the clock when it is evaluated. Because the lease condition is inside the takeover write, a renewal that lands after recovery's scan prevents the takeover, and a renewal that is still uncommitted when the takeover arrives makes it wait and re-evaluate, exactly as claim and cancel do; at `REPEATABLE READ` or `SERIALIZABLE`, the takeover can instead fail with a serialization error if a concurrent renewal commits after the recovery transaction's snapshot. Expiry makes an attempt eligible for takeover; it does not end its ownership of the row, which ends only when a takeover commits. That is why renewal omits an expiry check: a slow worker whose lease has expired can still renew until recovery actually takes over. The one place this design treats expiry as revoking authority is the irreversible boundary. Cutoff 2 requires a lease that is current at the moment of the write, so a worker that went quiet long enough to be presumed dead must renew, which succeeds only if no takeover has committed, before it can authorize delivery. The original worker, if it resumes after a takeover, finds that its attempt no longer owns anything. The attempt cap matters too: a job that crashes its worker every time would otherwise be requeued forever. [An In-Memory Grant Store Is Not Distributed Replay Protection](../articles/2026/in-memory-grant-store-is-not-distributed-replay-protection.md#recovery-and-the-original-executor-share-one-ownership-protocol) works through a similar ownership protocol and the timing values it depends on. Like the rest of this design, recovery assumes acknowledged state changes survive a database crash.

---

## Recovery

A recovery run first selects candidates, for example `SELECT id, attempt_id, status, stop_request_id, attempt_count FROM export_jobs WHERE status IN ('Running', 'Delivering') AND lease_expires_at < clock_timestamp()`. That scan only nominates. It takes each job over with one conditional statement per case, repeating every condition the scan relied on, so a renewal that commits after the scan makes the takeover match nothing. Each statement records its event in the same statement:

```sql
-- Recovery, one statement per case, each repeating every condition it relied on. An expired Running
-- attempt with no stop request goes back to the queue, with its event and a new outbox row:
WITH requeued AS (
    UPDATE export_jobs
    SET status = 'Queued', attempt_id = NULL, claimed_by = NULL, lease_expires_at = NULL,
        woken_at = clock_timestamp()
    WHERE id = @job_id AND status = 'Running' AND attempt_id = @expired_attempt_id
      AND stop_request_id IS NULL AND attempt_count < @max_attempts
      AND lease_expires_at < clock_timestamp()
    RETURNING id),
event AS (
    INSERT INTO export_attempt_events (job_id, attempt_id, kind, recorded_at)
    SELECT id, @expired_attempt_id, 'RequeuedByRecovery', clock_timestamp() FROM requeued)
INSERT INTO outbox (message_id, kind, job_id, created_at)
SELECT gen_random_uuid(), 'ExportReady', id, clock_timestamp() FROM requeued;

-- One with a recorded stop request is stopped. Terminal states keep attempt_id for audit; only a
-- requeue clears it, because then no attempt is current.
WITH stopped AS (
    UPDATE export_jobs
    SET status = 'Stopped', lease_expires_at = NULL
    WHERE id = @job_id AND status = 'Running' AND attempt_id = @expired_attempt_id
      AND stop_request_id IS NOT NULL
      AND lease_expires_at < clock_timestamp()
    RETURNING id, attempt_id)
INSERT INTO export_attempt_events (job_id, attempt_id, kind, observed_by, recorded_at)
SELECT id, attempt_id, 'StoppedByRecovery', @recovery_run_id, clock_timestamp() FROM stopped;

-- One that has used up its attempts fails.
WITH exhausted AS (
    UPDATE export_jobs
    SET status = 'Failed', failure_reason = 'AttemptsExhausted', lease_expires_at = NULL
    WHERE id = @job_id AND status = 'Running' AND attempt_id = @expired_attempt_id
      AND stop_request_id IS NULL AND attempt_count >= @max_attempts
      AND lease_expires_at < clock_timestamp()
    RETURNING id, attempt_id)
INSERT INTO export_attempt_events (job_id, attempt_id, kind, observed_by, recorded_at)
SELECT id, attempt_id, 'AttemptsExhausted', @recovery_run_id, clock_timestamp() FROM exhausted;

-- An expired Delivering attempt is resolved from evidence. Recovery decides @resolution before this
-- statement: 'Completed' only when the destination, or a receipt an attempt appended, matches the expected
-- file, with a stricter rule for replacements below; otherwise 'OutcomeUnknown'. A later reconciliation moves
-- OutcomeUnknown on to Completed or Failed (with 'NotDelivered')
-- the same way, conditioned on status = 'OutcomeUnknown' and the attempt, with no lease condition because recovery
-- cleared the lease, and inserting its own 'Reconciled' event. Reconciliation is one-shot per attempt; see below.
WITH resolved AS (
    UPDATE export_jobs
    SET status = @resolution, lease_expires_at = NULL
    WHERE id = @job_id AND status = 'Delivering' AND attempt_id = @expired_attempt_id
      AND lease_expires_at < clock_timestamp()
      AND @resolution IN ('Completed', 'OutcomeUnknown')
    RETURNING id, attempt_id, status)
INSERT INTO export_attempt_events (job_id, attempt_id, kind, observed_by, detail, recorded_at)
SELECT id, attempt_id, 'ResolvedByRecovery', @recovery_run_id, status || ': ' || @evidence_summary, clock_timestamp()
FROM resolved;
```

Cancellation adds four rules to recovery:

- **A `Running` attempt with a stop request is finished, not restarted.** `Running` means cutoff 2 was never committed, so nothing was delivered. Recovery moves the job to `Stopped`, conditionally on `Running`, the expired attempt, its lease, and the recorded stop request. The original worker, if it resumes, cannot authorize delivery either, because the recorded stop request already fails cutoff 2's condition.
- **A `Running` attempt without one may be returned to `Queued`**, conditionally on `Running`, the expired attempt, its lease, `StopRequestId` still being null, and the attempt cap, with a new message written to the outbox in the same transaction. That takeover does race a late authorization from the original worker: both require `Running` and the same attempt, so at most one commits. If the takeover wins, the job is ordinary queued work again, and a cancellation that arrives afterward competes with the next claim. If the authorization wins, the job is `Delivering`, and the next rule applies.
- **A `Delivering` attempt is resolved, not stopped, canceled, or retried.** It may have authorized delivery and crashed before sending a byte, or sent everything and lost the receipt. Recovery compares what the destination shows, and any receipt an attempt appended, with the expected file recorded at cutoff 2, by name and size, and by hash only where the partner exposes one, and records `Completed` only on evidence, through a write conditioned on `Delivering`, the old attempt, and a lease that is still expired, so a renewal during recovery's lookup prevents the takeover. If recovery records `OutcomeUnknown` first, the original worker's later finish write matches nothing, but its receipt is not lost: the worker appends it as delivery evidence for its attempt, and reconciliation weighs it against the expected file and the partner's contract. A temporary file, or no file at all, is not evidence either way while the original worker might still upload, because `AttemptId` protects writes to your row, not uploads to the partner's server. Recovery compares by name and size alone when the partner exposes no hash, which is weaker, so it leans on a final name unique to the delivery key and on the partner's contract. It does not conclude `Failed` from an absent file on its own: a paused worker can outlast any timeout. `Failed` needs affirmative evidence, such as the partner confirming that nothing was published and that the attempt's credentials or upload slot can no longer publish; without it, the job stays `OutcomeUnknown` for manual reconciliation. A replacement needs more: a file under its final name may be the original's, so recovery records `Completed` only from a receipt this attempt appended, or from a listing that changed from this attempt's recorded baseline in a way the worker's evidence rules accept. A replacement that crashed after cutoff 2, before it recorded a baseline, stays `OutcomeUnknown`, and a file there is evidence for the original, not for the replacement, only if it is identified as the original's delivery by the rule under [the worker](#the-worker). A cancellation request against that job is still `TooLate`.
- **A replacement delivery is a new decision.** Recovery never starts one; it reconciles the original attempt. A named, authorized person may create one, through the statement under [The Tables](#the-tables), and only after the original attempt can no longer write to the row or still be uploading, and only if the partner's contract makes a duplicate harmless, for example by deduplicating on the original `delivery_key` and an agreed content identity, or by ignoring a second upload under the same final name. Rebuilding an export can produce different bytes, so agree in advance what the partner does with a reused key and changed content. A fixed name alone is not that contract if the partner processes every upload. A replacement is never a transition of the original job, which has no path back to a runnable state: it is a new job, with its own ID, created by a named person after reconciliation has been tried, with `replaces_job_id` pointing at the original. It carries the original's `delivery_key` forward wherever the partner deduplicates on it, because the internal link between two job rows gives the partner no protection against a duplicate; only the partner-facing identity does. Its final name therefore matches the original's, so a file already found under that name may be the original's own delivery: the replacement checks it against the original's expected file before uploading anything, and never overwrites it without a recorded agreement, as [the worker](#the-worker) describes.

Reconciliation is one-shot: the unique index allows one `Reconciled` event per attempt. Evidence that arrives afterward is appended as `DeliveryEvidence`. If it shows a recorded `Completed` or `Failed` was wrong, for example when a replacement finds the original's file after the original was reconciled to `Failed`, a named person corrects it. Run the correction under a role that only reconciliation operators hold, never the worker's or the API's. Like every other writer, it is conditional, and it records who made it and why in the same statement:

```sql
WITH corrected AS (
    UPDATE export_jobs
    SET status = @corrected_status,
        failure_reason = CASE WHEN @corrected_status = 'Failed' THEN @failure_reason END
    WHERE tenant_id = @tenant_id AND id = @job_id AND attempt_id = @attempt_id
      AND status = @recorded_status AND status IN ('Completed', 'Failed')
      AND @corrected_status IN ('Completed', 'Failed') AND @corrected_status <> @recorded_status
    RETURNING id, attempt_id, status)
INSERT INTO export_attempt_events (job_id, attempt_id, kind, observed_by, detail, recorded_at)
SELECT id, attempt_id, 'Corrected', @corrected_by,
       @recorded_status || ' to ' || status || ': ' || @evidence_ref || '; ' || @why, clock_timestamp()
FROM corrected;
```

The unique index allows one `Corrected` event per attempt. Anything beyond that is handled outside the job row.

Recovery is also a writer to the job row, so it follows the same invariant as everything else: no unconditional status changes.

---

## Messages, the Outbox, and the Sweeper

Because the row, not the message, carries the job once it is claimed, the message is only a wake-up call. If the job table, its recovery process, and an outbox are your durable execution system, that settles a practical question about long jobs. Once the worker's claim has committed, the row carries the work, so the worker can settle the message right away instead of renewing its lock for the length of the build. A redelivered copy would only reach the conditional claim and do nothing. The cost is that the original message is gone, so any write that leaves a job queued without a live message must arrange a new one. Recovery returning a job to `Queued`, and an edit to a queued job's terms, both write an [outbox](../articles/2026/background-job-should-not-inherit-user-authority.md#the-request-records-an-operation) row in the same transaction: a row in the database that a separate dispatcher publishes to the broker after the commit. Give each outbox row its own message ID, reused only when the dispatcher retries that row; if your broker deduplicates by message ID, reusing the job ID would silently drop the second wake-up. A recovery requeue or a terms edit therefore writes a new outbox row with a new ID, never one keyed on the job. A worker that cannot confirm its claim does not settle the message at all, so it stays eligible for redelivery, subject to the broker's own policies such as a maximum delivery count. And because outboxes, dispatchers, settlement, and dead-lettering can all strand a job, a periodic sweeper is a cheap backstop: it writes a fresh outbox row for any job left in `Queued` longer than expected. It is a conditional writer like the rest, inserting the row only for a job that is still `Queued` in the same statement. It does not look for an unpublished row already waiting: duplicate wake-ups can repeat policy evaluation, but only one claim can win the current queued cycle. Every write that publishes a wake-up also sets `woken_at`, so the sweeper can tell how long a job has waited since its last one:

```sql
-- Batched, so a large backlog never becomes one long transaction. SKIP LOCKED leaves rows that another
-- writer is changing to the next sweep.
WITH due AS (
    SELECT id FROM export_jobs
    WHERE status = 'Queued' AND woken_at < clock_timestamp() - interval '30 minutes'
    ORDER BY woken_at
    LIMIT 500
    FOR UPDATE SKIP LOCKED),
swept AS (
    UPDATE export_jobs j
    SET woken_at = clock_timestamp(), sweep_count = j.sweep_count + 1
    FROM due
    WHERE j.id = due.id AND j.status = 'Queued'
    RETURNING j.id)
INSERT INTO outbox (message_id, kind, job_id, created_at)
SELECT gen_random_uuid(), 'ExportReady', id, clock_timestamp() FROM swept;
```

Set the threshold above the longest wait a job can legitimately have in `Queued`, such as the per-destination rate limit at its busiest; a shorter threshold increases broker traffic and policy evaluations without allowing a second claim in the same queued cycle. Each sweep bumps `woken_at`, so a job whose messages keep failing is republished every interval; `sweep_count` records how often, so alert when a job's count keeps rising rather than letting it cycle unnoticed. Because a duplicate wake-up repeats policy evaluation, that evaluation must be free of side effects: it reads and decides, and nothing it does may need to happen exactly once.

An edit to a queued job's terms is the other writer that leaves a job queued without a live message:

```csharp
// API: change the destination of an export that has not started.
await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);

int changed = await db.ExportJobs
    .Where(j => j.Id == jobId && j.Status == ExportStatus.Queued && j.Version == expectedVersion)
    .ExecuteUpdateAsync(set => set
        .SetProperty(j => j.DestinationId, newDestinationId)
        .SetProperty(j => j.Version, j => j.Version + 1)           // the terms changed, so the version does
        .SetProperty(j => j.WokenAt, j => DateTimeOffset.UtcNow), ct);  // Npgsql translates this to now(); confirm
                                                                         // in the logged SQL

if (changed == 0)
{
    return EditResult.NotChanged;   // started, ended, or edited by someone else since it was read
}

// A worker holding the old version will fail its claim and settle its message. Without a new one,
// the edited job would wait in Queued forever.
db.Outbox.Add(OutboxMessage.ExportReady(jobId));
await db.SaveChangesAsync(ct);
await tx.CommitAsync(ct);
return EditResult.Changed;
```

`woken_at` is only a coarse timestamp for the sweeper, so the edit's `now()`, the start of its short transaction, is close enough to the sweeper's `clock_timestamp()` against a 30-minute threshold. A worker that evaluated the old destination now fails its claim and starts nothing whose terms nobody checked, and the outbox message brings the job back to a worker that will evaluate the new terms. An integration test can fail the build when any write to a terms column omits the increment. [Your Security Check Passed. Then the World Changed.](../articles/2026/security-check-passed-then-world-changed.md#at-the-moment-of-use-check-then-claim-then-send) develops this check-then-claim pattern and its limits, including facts that live outside the claimed row.

---

## The Worker

The runner below puts the pieces together: it treats the message as a hint, re-checks policy, claims, builds with a stop token, authorizes delivery, and delivers.

```csharp
public sealed class ExportRunner(
    ExportJobs jobs, ExportPolicy policy, IExportBuilder builder, IPartnerDestination destination,
    IDeliveryErrorClassifier classifier, StopRequestWatcher watcher, LeaseKeeperFactory leases, WorkerIdentity worker)
{
    // Every write through `jobs` is conditional, and those made after cutoff 1 also require this attempt.
    // Writes that record an outcome use CancellationToken.None internally. ClaimAsync and AuthorizeDeliveryAsync
    // wrap the conditional writes shown earlier (TryClaim..., TryAuthorizeDelivery...) and, when a call throws or
    // a retry returns 0, re-read state and ownership as described under "A lost response".
    public async Task RunAsync(ReceivedMessage message, CancellationToken stoppingToken)
    {
        // The message is only a hint. Everything below starts from the row.
        ExportJob? job = await jobs.FindAsync(message.JobId, stoppingToken);
        if (job is not { Status: ExportStatus.Queued })
        {
            await message.SettleAsync();   // canceled, already claimed, finished, or not found
            return;
        }

        ExportDecision decision = await policy.EvaluateAsync(job, stoppingToken);
        if (!decision.IsAllowed)
        {
            // Queued → Rejected, this version only. If the terms changed meanwhile, this matches nothing,
            // and the edit's own outbox message brings the job back for a fresh evaluation.
            await jobs.TryRejectAsync(job.Id, job.Version, decision);
            await message.SettleAsync();
            return;
        }

        // Cutoff 1. On shutdown before this point, the exception propagates and the message is redelivered.
        Guid attemptId = Guid.NewGuid();
        ClaimResult claim = await jobs.ClaimAsync(job.Id, job.Version, attemptId, worker.Id, stoppingToken);
        if (claim == ClaimResult.Unknown)
        {
            return;   // the re-read failed too: leave the message unsettled, so the broker delivers it again
        }

        await message.SettleAsync();   // claimed or lost: either way the row, not the message, now carries the job
        if (claim != ClaimResult.Claimed)
        {
            return;
        }

        // From the confirmed claim to the attempt's last write: one stop source and one lease keeper.
        // `stop` is signaled by a recorded stop request, by a renewal that changes no row, or on shutdown.
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        await using LeaseKeeper lease = leases.KeepRenewed(job.Id, attemptId, every: TimeSpan.FromSeconds(20), stop);
        await using StopRequestWatch watch = watcher.Watch(job.Id, attemptId, stop);

        ExportFile? file = await PrepareAsync(job, decision, attemptId, lease, stop, stoppingToken);
        if (file is not null)
        {
            // The keeper keeps renewing through the upload and the final writes. Stop requests are no longer
            // obeyed here, and a lost lease no longer stops the upload; recovery reconciles that case.
            await DeliverAsync(job.Id, attemptId, file, lease, stoppingToken);
        }
    }

    // Returns the file only if cutoff 2 is confirmed. Otherwise records why not, where it can.
    private async Task<ExportFile?> PrepareAsync(
        ExportJob job, ExportDecision decision, Guid attemptId,
        LeaseKeeper lease, CancellationTokenSource stop, CancellationToken stoppingToken)
    {

        ExportFile? file = null;
        AuthorizationResult cutoff = AuthorizationResult.NotReached;
        try
        {
            // Reversible: a local file. The builder removes its partial output when canceled, cancels only
            // through stop.Token, lets that OperationCanceledException propagate unwrapped, and wraps every other
            // failure, including its own internal timeouts, in ExportBuildException.
            file = await builder.BuildAsync(job, decision.Scope, stop.Token);

            // Cutoff 2. This write, not the watcher, guarantees that a stop request committed first prevents delivery.
            cutoff = await jobs.AuthorizeDeliveryAsync(job.Id, attemptId, file.Expected);
            if (cutoff == AuthorizationResult.LeaseLapsed && await lease.TryRenewNowAsync())
            {
                // Still the owner, only late: renewal succeeds only if no takeover has committed. Retry once.
                // TryRenewNowAsync retries a transient failure a few times with backoff, and returns false only when
                // a renewal changes no row or its retries run out; the attempt then gives up here.
                cutoff = await jobs.AuthorizeDeliveryAsync(job.Id, attemptId, file.Expected);
            }

            if (cutoff == AuthorizationResult.StopRecorded)
            {
                await jobs.TryFinishStoppedAsync(job.Id, attemptId, StopPoint.BeforeDelivery);
            }
            else if (cutoff == AuthorizationResult.OriginalCompleted)
            {
                // A replacement whose original was corrected to Completed. Running → Failed, OriginalDelivered.
                await jobs.TryFinishFailedAsync(job.Id, attemptId, FailureCode.OriginalDelivered, cause: null);
            }
            // LeaseLapsed again, NotOwner, or Unknown: write nothing to the row; recovery takes it from there.
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            await jobs.TryFinishStoppedAsync(job.Id, attemptId, StopPoint.WhileBuilding);
        }
        catch (ExportBuildException ex)
        {
            // Running → Failed; nothing has left the process.
            await jobs.TryFinishFailedAsync(job.Id, attemptId, FailureCode.BuildFailed, cause: ex);
        }
        finally
        {
            if (cutoff != AuthorizationResult.Authorized)
            {
                await lease.FinishAsync();   // this attempt's last write, if it made one, is behind it; never throws
                if (file is not null)
                {
                    await file.TryDeleteAsync();   // best effort, never throws; failures are logged for the scavenger
                }
            }
        }

        // AuthorizationResult.Unknown: the re-read failed too. Stop here; the row says which side of cutoff 2
        // this attempt is on, and recovery reads it. The local file was deleted above on purpose: keeping contact
        // data on disk for a delivery nobody may resume is the worse risk, and recovery never uploads it anyway.
        // If cutoff 2 had in fact committed, nothing was sent; recovery reconciles the original attempt, and any
        // replacement follows the rules under Recovery.
        // On shutdown, the job stays Running, or Delivering if cutoff 2 committed first. The lease keeper, owned by
        // RunAsync, outlives this method.
        return cutoff == AuthorizationResult.Authorized ? file : null;
    }

    private async Task DeliverAsync(
        Guid jobId, Guid attemptId, ExportFile file, LeaseKeeper lease, CancellationToken stoppingToken)
    {
        // Past the point of no return. Stop requests from here on are recorded, not obeyed.
        DeliveryOutcome outcome;
        try
        {
            // Lists the final name first and appends what it finds as baseline evidence. An occupied name stops
            // the upload, except as "An existing final name" below allows. Otherwise uploads under a temporary
            // name, renames it to the job's own final name, then reads back what the server and the partner can
            // confirm: that name and its size, plus a checksum or an acknowledgment where the partner provides one.
            // The classifier treats cancellation and timeouts here as unknown.
            DeliveryReceipt receipt = await destination.UploadAndFinalizeAsync(file, attemptId, stoppingToken);
            outcome = DeliveryOutcome.Completed(receipt);
        }
        catch (Exception ex)
        {
            // Failed only when the error proves the final name was never written; otherwise unknown.
            // Includes shutdown during the transfer.
            outcome = classifier.Classify(ex);
        }

        try
        {
            // Append the evidence for this attempt first, whoever owns the job now: an insert that never touches
            // the job's status, recording who observed what, and when. If the next write matches nothing, recovery
            // weighs this evidence under its Delivering rule.
            // Then try the conditional Delivering → Completed, Failed, or OutcomeUnknown write for this attempt
            // only. A graceful shutdown should allow a bounded window for both; if they never commit, the job
            // stays Delivering, and recovery resolves it.
            await jobs.AppendDeliveryEvidenceAsync(jobId, attemptId, observedBy: worker.Id, outcome);
            await jobs.TryFinishDeliveryAsync(jobId, attemptId, outcome);   // the attempt's terminal write
        }
        finally
        {
            await lease.FinishAsync();      // right after the terminal write, so no renewal reports a false loss
            await file.TryDeleteAsync();   // idempotent, never throws, logs failures for the scavenger
        }
    }
}
```

The details that matter for cancellation:

- **Stopped means a recorded stop request.** `TryFinishStoppedAsync` moves `Running` to `Stopped` only for this attempt and only when `StopRequestId` is set. An attempt that has lost ownership writes nothing to the job row; it may only append delivery evidence. Its catch blocks cannot turn a lease takeover into an operator's stop.
- **The watcher is for speed, not correctness.** It can poll the row every few seconds for a stop request, or subscribe to a notification and then confirm against the row. Either way, it lets a stopped export give up after three seconds instead of 24. If it misses the request, or the notification is lost, cutoff 2 still refuses to authorize delivery. Do not make a notification the only thing standing between a stop request and an upload.
- **Stopping happens only where it is safe.** `BuildAsync` passes the token down to the database query and the file writes, so they end promptly. Whatever ends the preparation, the worker tries to remove a local file that was never authorized for delivery, and logs a failed removal for a scavenger rather than claiming it happened. For exported contact data, that log matters.
- **The upload is made safe to interrupt rather than cancellable.** Writing under a temporary name and renaming to the final name only after the transfer completes prevents partial publication under the name the partner processes. It can still leave partial contact data under the temporary name, which needs its own cleanup agreement. SFTP servers differ here: some refuse to rename onto an existing name, and atomic overwrite is an extension that not every server offers. Agree three things with the partner: that temporary names are ignored or written outside the processed directory, that the server renames in one step, and what happens if the final name already exists. Even then, an interruption during or just after the rename leaves the outcome unknown, and the classifier records `OutcomeUnknown`. [A Payment API Timeout Leaves the Outcome Unknown](../articles/2026/payment-api-timeout-unknown-outcome.md) covers why an interrupted external call needs its own state and is resolved only by evidence.
- **An existing final name is checked, and never overwritten by default.** Before uploading, the destination lists the final name and appends the result as evidence, so recovery and reconciliation see the same baseline. The worker can also list it during preparation, so an occupied name ends the attempt from `Running`, where a stop request can still win; the listing after cutoff 2 is the recorded baseline. For an original job, the name is unique to a delivery key that nothing has published yet. A file already there is a conflict: the attempt uploads nothing, records `Failed` with `FinalNameOccupied`, and an operator investigates. A replacement reuses its original's name, so a file there may be the original's own delivery:
  - **If it is identified as the original's delivery**, the original delivered after all. Identification needs a checksum that matches the original's expected file or an acknowledgment tied to the original, or name and size only where the partner's contract makes those enough to identify it, since different exports can have the same size. The replacement uploads nothing, records `Failed` with `OriginalDelivered`, and a named person corrects the original's record as [Recovery](#recovery) describes.
  - **If it matches nothing or cannot be identified**, the replacement also uploads nothing and records `FinalNameOccupied`, unless its creator recorded the partner's agreement in `overwrite_agreement_ref`. A same-size file that cannot be tied to the original leaves the original's outcome as it was; nobody corrects it on that evidence.
  - **With that agreement**, it uploads. `Completed` then needs evidence tied to this attempt: a hash that matches the expected file, or the partner's acknowledgment of this upload. Name and size cannot show the change, because a rebuilt export can have the same size, and a newer modification time is enough only if the partner's contract says so. Without such evidence, the outcome is `OutcomeUnknown`.

  Every job renames with the form that fails when the final name exists, so a file that appears between the listing and the rename, such as a paused earlier upload landing late, is never replaced. Only a replacement with a recorded agreement uses an overwriting form. A refused rename leaves the final file as it was, so the classifier records `Failed` with `NotDelivered` only when a fresh listing still matches the baseline, and `OutcomeUnknown` otherwise. In each `Failed` case here, the attempt proved it never wrote the final name, which is the rule the classifier already applies.
- **`Completed` needs evidence the partner's side can give.** `Completed` means the delivery condition agreed with this partner was met, not that the partner has processed the contacts. SFTP has no widely supported standard for hashing a remote file, so the evidence depends on what the partner offers. A listing that shows the final name for this delivery key, changed from the baseline recorded before the upload, which for an original is an absent file, with the expected size is reasonable evidence if the partner's server publishes files only by atomic rename; a checksum, where the server offers one, or the partner's acknowledgment, is stronger. A file that merely exists, or one that still matches the baseline, is not enough, and an overwrite needs the stronger evidence above.

---

## Recording Cancellation Requests

The article explains what a cancellation request records and what the console says. Use one `DbContext` per submission, so a rolled-back insert never stays tracked into a retry, and have the client reuse its original request ID after `Busy`: a later success returns the outcome already recorded. The method that does it locks this tenant's job row, binds the client's request ID to the job and the operator, checks every write's row count, and maps a lock timeout to a retryable `Busy` result:

```csharp
public async Task<CancellationResult> RequestCancellationAsync(
    TenantId tenant, OperatorId requestedBy, Guid jobId, Guid requestId, string reason, CancellationToken ct)
{
    // The caller has already authenticated the operator and confirmed their access to this tenant.
    try
    {
        return await DecideAndRecordAsync(tenant, requestedBy, jobId, requestId, reason, ct);
    }
    catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })   // Npgsql, PostgreSQL only
    {
        // Most likely the same (tenant, requestId) committed first under a different job's lock. This
        // transaction has rolled back. ReadRecordedOutcomeAsync reads through a new context from an
        // IDbContextFactory and applies the same job and operator check, so a reused ID still returns
        // RequestIdReused. If no such request exists, the violation was something else, and it rethrows.
        return await ReadRecordedOutcomeAsync(tenant, requestedBy, jobId, requestId, ex, ct);
    }
    catch (Exception ex) when (IsLockTimeout(ex))   // 55P03, bare or wrapped in DbUpdateException by SaveChanges
    {
        // lock_timeout fired: this transaction decided and recorded nothing. An earlier submission with the
        // same request ID may already have committed, so the client retries with the same ID.
        return CancellationResult.Busy(requestId);
    }
}

private static bool IsLockTimeout(Exception ex) =>
    (ex as PostgresException ?? ex.InnerException as PostgresException)?.SqlState == PostgresErrorCodes.LockNotAvailable;

private async Task<CancellationResult> DecideAndRecordAsync(
    TenantId tenant, OperatorId requestedBy, Guid jobId, Guid requestId, string reason, CancellationToken ct)
{
    await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);

    // Bounds each lock wait in this transaction only. If one times out, the statement fails and the
    // transaction is aborted; disposing `tx` without committing rolls it back.
    await db.Database.ExecuteSqlAsync($"SET LOCAL lock_timeout = '2s'", ct);

    // Lock this tenant's job row until commit or rollback. The interpolated values become parameters;
    // never build this SQL by concatenation. After AsAsyncEnumerable no later LINQ operator is composed into
    // the SQL, so nothing wraps the FOR UPDATE in a subquery; a test still proves the lock is taken.
    ExportJob? found = null;
    await foreach (ExportJob row in db.ExportJobs
        .FromSql($"""
            SELECT id, tenant_id, status, version, destination_id, attempt_id, attempt_count, claimed_by,
                   lease_expires_at, stop_request_id, cancellation_request_id, expected_file_name,
                   expected_file_bytes, expected_file_sha256, delivery_authorized_at, woken_at, failure_reason,
                   delivery_key, replaces_job_id, created_by, replacement_reason, overwrite_agreement_ref,
                   sweep_count
                   -- every mapped column, or EF Core cannot materialize the entity; a test compares this list with the model
            FROM export_jobs WHERE id = {jobId} AND tenant_id = {tenant.Value} FOR UPDATE
            """)
        .AsNoTracking()
        .AsAsyncEnumerable()
        .WithCancellation(ct))
    {
        found = row;   // at most one: job IDs are globally unique
        break;
    }

    if (found is null)
    {
        return CancellationResult.NotFound(requestId);   // another tenant's job looks exactly like a missing one
    }

    // Checked under the lock, so two submissions of the same click are serialized here. Request IDs are
    // unique per tenant (primary key: tenant_id, id), so another tenant's IDs never collide or show through.
    // Deliberately, a retry with a different reason is still the same request; the first reason stands.
    CancellationRequest? earlier = await db.CancellationRequests.AsNoTracking()
        .SingleOrDefaultAsync(r => r.TenantId == tenant && r.Id == requestId, ct);
    if (earlier is not null)
    {
        return earlier.JobId == jobId && earlier.RequestedBy == requestedBy
            ? CancellationResult.From(earlier)
            : CancellationResult.RequestIdReused(requestId);   // a client error; says nothing about the other request
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

    int changed = outcome switch
    {
        CancellationOutcome.CanceledWhileQueued => await db.ExportJobs
            .Where(j => j.Id == jobId && j.TenantId == tenant && j.Status == ExportStatus.Queued)
            .ExecuteUpdateAsync(set => set
                .SetProperty(j => j.Status, ExportStatus.Canceled)
                .SetProperty(j => j.CancellationRequestId, requestId), ct),
        CancellationOutcome.StopRequested => await db.ExportJobs
            .Where(j => j.Id == jobId && j.TenantId == tenant
                     && j.Status == ExportStatus.Running && j.StopRequestId == null)
            .ExecuteUpdateAsync(set => set.SetProperty(j => j.StopRequestId, requestId), ct),
        _ => 1,   // these outcomes change nothing on the job
    };

    if (changed != 1)
    {
        // Impossible while the row is locked. If it happens, record nothing: the outcome above is not true.
        // logger: an injected ILogger for this service.
        logger.LogCritical("Export {JobId} changed under its row lock; outcome {Outcome} not recorded", jobId, outcome);
        throw new InvalidOperationException($"Export {jobId} changed under its row lock.");
    }

    // No ExportJob is tracked by this context, so these ExecuteUpdate calls and SaveChanges never touch
    // the same entity.
    db.CancellationRequests.Add(new CancellationRequest(
        tenant, requestId, jobId, requestedBy, reason, outcome,
        FoundStatus: found.Status,
        FoundAttemptId: found.AttemptId,
        FoundAttemptCount: found.AttemptCount,
        FoundDeliveryAuthorizedAt: found.DeliveryAuthorizedAt));

    await db.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);

    return CancellationResult.From(requestId, outcome, found);
}
```

Keep this transaction short: one locking read, one lookup, at most one update, and one insert. The row lock lasts until commit or rollback, so anything added between the locking read and the commit, even a slow query, lengthens the wait for every competing claim on that job. Policy checks and notifications belong outside it, and a [lock timeout](https://www.postgresql.org/docs/current/runtime-config-client.html#GUC-LOCK-TIMEOUT), set for this transaction with `SET LOCAL`, bounds how long each of its statements waits for the row. It does not release a lock someone else holds. When it fires, the statement fails with SQLSTATE `55P03`, the transaction is aborted until the client rolls it back, nothing is recorded for that click, and the method returns `Busy`: the console reports that it could not tell and should be tried again, never "canceled." EF Core [warns against mixing tracked `SaveChanges` changes and `ExecuteUpdate` on the same entities](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete); this method avoids that by never tracking the job.

The conditions in the two updates are defense in depth. The lock is what makes the classification true: without it, the outcome would have to be re-derived from the row counts, and the method redesigned. A request for a job the caller cannot see is not recorded against any job, because there is none to attach it to; log it as an ordinary access event.

---

## Test Matrix

The article's tests cover the race itself: two writers contending for one row, the incident's interleaving, a stop request before cutoff 2, and a cancellation after it. These cover the rest, against the real database engine, each with its own `DbContext`, connection, and transaction. They are specifications of the behavior described above, not evidence of a working implementation.

- **A long upload.** An upload that outlasts the initial lease keeps it renewed and finishes `Completed`, not taken over. If a takeover does commit during the upload, the upload still finishes, its evidence is appended, its terminal write matches nothing, and recovery reconciles from the evidence.
- **Reconciliation.** An attempt resolved to `OutcomeUnknown` by recovery is later reconciled to `Completed` or `Failed`, and both the `ResolvedByRecovery` and the `Reconciled` events survive. A second `Reconciled` event for the same attempt is refused. A `Failed` row without a failure code, or any other status with one, is refused, including through the correction statement in both directions. A correction changes a recorded `Completed` or `Failed` once, as `Corrected`, records who made it and why, and is refused for the worker's and the API's roles.
- **A long build.** A healthy build that outlasts the initial lease keeps renewing and delivers exactly once. A worker paused past its lease is refused at cutoff 2, renews if no takeover has committed, and then delivers; if a takeover has committed, its renewal changes nothing and it stops.
- **Lost responses.** Drop the connection after the claim commits, and again after cutoff 2 commits: the worker re-reads, finds the expected state with its own attempt, and continues. Drop it before the claim commits and fail the re-read: the message stays unsettled, is delivered again, and the next claim runs the job once. Drop it after the claim commits and fail the re-read: the redelivered message finds the job `Running`, does nothing, and recovery takes the attempt over when its lease expires. Let recovery move the attempt to `OutcomeUnknown` before the worker's re-read: the worker sees its own ID in the wrong state and does nothing further.
- **Recovery interleavings.** A cancellation that finds `Queued` after recovery returned the job records `CanceledWhileQueued` with a nonzero attempt count. Recovery's return of an expired `Running` attempt to `Queued` and a late authorization from the original worker cannot both commit. A lease renewed after recovery's scan prevents the takeover. A crash after cutoff 2 but before the upload starts ends `OutcomeUnknown`, or `Completed` only on destination evidence, never in a second upload. A worker that resumes after a takeover cannot renew its lease, while one whose lease merely expired can renew until a takeover commits, in `Running` and in `Delivering`. A worker with an expired lease is refused at cutoff 2 until it renews. An expired attempt with a stop request ends `Stopped`, and one at the attempt cap ends `Failed` with `AttemptsExhausted`, each with its event. A job that crashes its worker on every attempt ends `Failed` at the attempt cap. A late receipt from a superseded attempt is kept as evidence and does not change the job's state.
- **Interrupted uploads, by where they stop.** Interrupted before the rename, with the final name absent beforehand and proof the rename never ran: no file under the final name, and the outcome follows that evidence. For an original, a final name that already existed before the upload ends `Failed` with `FinalNameOccupied`, with nothing uploaded. Interrupted during or just after the rename: the final file may exist, and the outcome is `OutcomeUnknown` until reconciled. Interrupted after confirmation but before the terminal write: the job stays `Delivering`, and recovery records `Completed` from evidence without uploading again.
- **A changed request.** Changing the destination increments `Version`, so a worker that checked the old terms fails its claim and builds nothing, and the outbox message from the edit brings the job back to a worker that evaluates the new terms.
- **A paused attempt.** After recovery takes over an expired attempt, the original worker's authorization and completion writes match nothing, and its catch blocks record nothing.
- **Permissions.** The application role cannot `UPDATE` or `DELETE` either history table or `deliveries`, and the dispatcher's role can change only `published_at` in the outbox.
- **The sweeper.** A job whose only message was lost, with `woken_at` 31 minutes old, is republished; a control at 29 minutes is not. A job that leaves `Queued` while the sweep runs gets no outbox row, and a row locked by a cancellation is skipped and picked up by the next sweep. Each republication increments `sweep_count`.
- **Creation.** The job, its `deliveries` row, and its outbox row are created together or not at all, and a second original with the same key in the same tenant is refused, even when inserted directly rather than through the creation statement.
- **Replacement creation.** The creation statement inserts nothing for an original that is `Completed`, `Running`, `Delivering`, or `Failed` with `FinalNameOccupied`, and nothing for another tenant's job. The database refuses a replacement whose key or tenant differs from its original's, and one that names another replacement. Of two simultaneous creations, one fails on `uq_export_jobs_one_replacement`. After a replacement ends `Completed` or `Failed` with `OriginalDelivered`, another creation for the same original fails, while one that ended `Failed` with `NotDelivered` frees the slot. A creation that races a correction of the original to `Completed` waits for it and then inserts nothing.
- **Correction and replacement, in both orders.** A correction of the original to `Completed` that commits before the replacement's cutoff 2 makes that cutoff report `OriginalCompleted`, and the replacement ends `Failed` with `OriginalDelivered`, having sent nothing; this holds whether the replacement was still queued or already building when the correction committed. A cutoff 2 that commits first makes the correction wait until it commits; the correction then succeeds, and the replacement's final-name check governs its upload.
- **Replacement delivery.** A replacement that finds a file identified as the original's delivery, by a matching checksum or an acknowledgment tied to the original, uploads nothing and ends `Failed` with `OriginalDelivered`. A same-size file with neither, where the partner's contract does not accept name and size, ends `FinalNameOccupied` and leaves the original's outcome unresolved. One that finds a different or unidentifiable file uploads nothing and ends `Failed` with `FinalNameOccupied`, unless `overwrite_agreement_ref` is set. With it set, a rebuilt file of the same size but different bytes ends `Completed` only on a matching hash or the partner's acknowledgment, and `OutcomeUnknown` otherwise. A file that appears between the listing and the rename makes the no-overwrite rename fail, and the outcome is `OutcomeUnknown`, never an overwrite. A replacement that crashes after cutoff 2, before it records a baseline, while the original's file is present, is resolved by recovery to `OutcomeUnknown`, not `Completed`.
- **Renewal.** A renewal that throws does not stop a healthy build, and an on-demand renewal that overlaps a scheduled one is serialized rather than run concurrently on one context. After the terminal write, `FinishAsync` stops renewals, and a renewal already in flight that then finds no row is not reported as lost ownership.
- **Mapping checks.** The locking read's column list matches the columns EF Core maps for `ExportJob`, compared through the model's metadata, so a new mapped column fails the build rather than the cancel method. The SQL logged for a terms edit sets `woken_at` with `now()` rather than a client-side parameter; Npgsql documents that translation for `DateTime.UtcNow`, and the edit uses `DateTimeOffset.UtcNow`.
- **The request record.** Two simultaneous submissions with the same request ID produce one record and the same answer. A request ID reused for another job returns `RequestIdReused`; the same ID used in another tenant is simply a different request. A request for a missing job, or another tenant's job, returns `NotFound`. A cancellation that waits on a held row lock longer than `lock_timeout` returns `Busy`, records nothing, and succeeds when retried with the same request ID. The locking read is proven by its own blocking test: hold a cancellation transaction open after its `FOR UPDATE`, start a claim, confirm with `pg_blocking_pids` that the claim waits on it, then commit.

---

## Implementation Review Checklist

1. Are the claim and the delivery authorization each one statement that also inserts its attempt event, with the claim setting the lease and the authorization requiring a current one?
2. When a claim or boundary write throws or returns `0` after a retry, does the worker re-read state and ownership together, leave an unconfirmed claim's message unsettled, and write nothing to the row for an attempt it no longer owns?
3. Are lease renewal and every takeover conditional, with the lease checked inside each takeover write on `clock_timestamp()`, and is there an attempt cap?
4. Does recovery stop a `Running` attempt that has a stop request, resolve a `Delivering` attempt only from evidence recorded before delivery or appended since, and never conclude `Failed` from an absent file alone?
5. Is a replacement delivery started only after the original attempt can no longer write or upload, only under a partner contract that makes a duplicate harmless, and at most once at a time per original? Does the database block a replacement's authorization when its original is already recorded as `Completed`, leaving the partner's deduplication contract to govern after authorization?
6. Does every requeue, terms edit, and sweep write a new outbox row with its own message ID, reused only when the dispatcher retries that same row, and does job creation use the outbox too?
7. Does every terms edit increment `version` explicitly?
8. Is each cancellation decided under a lock on that tenant's job row, read without letting EF Core compose over the `FOR UPDATE`, with request IDs unique per tenant and bound to the job and operator, and does every write check its row count?
9. Does a lock timeout return a retryable `Busy` result with nothing recorded?
10. Does the builder let stop-token cancellation propagate unwrapped and wrap every other failure, and does the delivery classifier treat cancellation and timeouts during the upload as unknown unless it has proof?
11. Is delivery evidence appended with its provenance, and is it impossible for an evidence write to change the job's status?
12. Are the two history tables append-only, and the outbox insert-only apart from `published_at`, by the database's permissions rather than by convention?
13. Does one lease keeper, owned by the runner, renew from the confirmed claim through delivery and the terminal write, finish right after that write, retry a renewal that throws, and stop preparation, but not an upload in progress, when a renewal changes no row?
14. Does the sweeper find queued jobs by `woken_at` in bounded batches, with a threshold above the longest legitimate queue wait, and is a replacement delivery always a new job created by a named person that carries the original's `delivery_key`?
15. Does the destination record the final name's baseline before uploading, rename only with the no-overwrite form unless a replacement has a recorded overwrite agreement, require a hash or acknowledgment for an overwrite, and treat a file as the original's delivery only when a checksum, an acknowledgment, or a contract that accepts name and size identifies it?
16. Is every `Failed` written with one of the fixed failure codes, and is a correction to a recorded outcome a conditional write by a reconciliation operator, recorded once with who and why?

---

## Related Learning

- [Canceling a Queued Job Can Race With Execution](../articles/2026/revoke-job-after-it-enters-queue.md) is the argument this page implements: the race, the two cutoffs, cancellation as a request, and what to tell the operator.
- [Payment Attempt Recovery in ASP.NET Core: An Implementation Reference](payment-attempt-recovery-implementation-reference.md) applies the same recovery discipline to a payment provider whose response may be lost, with escalations and a recovery loop.
- [An In-Memory Grant Store Is Not Distributed Replay Protection](../articles/2026/in-memory-grant-store-is-not-distributed-replay-protection.md#recovery-and-the-original-executor-share-one-ownership-protocol) works through a similar ownership protocol for one-time grants, and the timing values it depends on.
- [Data-Access Boundaries and Transaction Reasoning with EF Core](data-access-boundaries-and-transaction-reasoning.md) explains why a local transaction cannot include an external side effect, and where outbox and inbox patterns fit.
- [Martin Kleppmann's analysis of distributed locking](https://martin.kleppmann.com/2016/02/08/how-to-do-distributed-locking.html) explains why a lease-based lock needs the protected resource to reject stale holders.
