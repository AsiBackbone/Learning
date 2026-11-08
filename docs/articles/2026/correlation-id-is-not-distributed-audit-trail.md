---
description: A correlation ID across an API, queue, worker, and webhook finds related logs. It cannot show what was decided, which effect each line was about, or the order.
title: A Correlation ID Is Not a Distributed Audit Trail
author: Christopher D. Cavell
published: "2026-11-08"
summary: A correlation ID tells you which records to read together. It does not say what each record means, whether any are missing, which one caused which, or what happened first. Give every decision, intended effect, message, attempt, and external observation its own identifier. Record the intent before each handoff and the result after it, and use the correlation ID only to find the records. Treat sampled traces as navigation, not evidence.
feed: true
x_hashtags:
  - DotNet
  - SoftwareArchitecture
---

# A Correlation ID Is Not a Distributed Audit Trail

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** No formal prerequisites. Familiarity with ASP.NET Core, `ILogger`, a message broker, and the idea of distributed tracing is helpful. No AsiBackbone package or prior Learning material is required.

**Sample scope:** The C# targets .NET 8 or later. The record types, repositories, message envelope, host acknowledgment behavior, and warehouse client are illustrative, not a runnable sample, a production-ready pattern, or a recommended schema. The code relies on helpers that are described but not shown. The scenario and its incident are hypothetical. The prose is licensed under CC BY 4.0, and the code snippets are additionally offered under the MIT License, as described in the repository's [licensing terms](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md). Tracing behavior is described from the W3C Trace Context specification, the OpenTelemetry documentation, and Microsoft's .NET documentation, last verified on November 8, 2026. Check the current documentation for your versions.

**What this article covers:** what a correlation ID does and does not establish; how one accurate log search produced a wrong incident story; the identifiers that should sit beside the correlation ID and the question each answers; what each service should record before and after an asynchronous handoff; claiming work before calling another system; duplicate delivery, redelivery, and out-of-order arrival; why sampled traces are navigation rather than evidence; how to carry identifiers across boundaries without carrying payloads, credentials, or caller-controlled authority; when structured logs plus durable operation records are enough; and how to test the records.

**In plain terms:** a correlation ID is a case number written on every page of a file. It lets you pull every page of that case from the cabinet. It does not tell you which page is the signed decision and which is a phone note, whether a page is missing, or which letter was sent first. Those answers come from what each page records, not from the number they share.

Consider a hypothetical online store. A customer may cancel an order until any of its shipments starts picking in the warehouse. The storefront API makes that decision. A fulfillment worker then asks the warehouse management system (WMS), a separate service run by a logistics partner, to hold each shipment. A payments service issues the refund. The WMS reports shipment progress back to the storefront through webhooks.

Every service writes structured logs. The storefront assigns a correlation ID when the cancellation request arrives, and every service puts that ID in its log scope and passes it along in message headers. The team traces requests with OpenTelemetry at a 10% head-sampling rate.

A customer complains. They received a "cancellation confirmed" email and a refund, and then the parcel arrived anyway. The on-call engineer searches the log store for the request's correlation ID and finds this:

```text
09:14:02.118  storefront   corr=8c1f  Cancel requested for order O-5521
09:14:02.140  storefront   corr=8c1f  Cancellation approved
09:14:02.161  storefront   corr=8c1f  Cancellation confirmation email queued
09:14:03.902  fulfillment  corr=8c1f  Processing HoldShipment for O-5521
09:14:04.010  fulfillment  corr=8c1f  WMS hold request failed: 409 Conflict
09:14:09.377  fulfillment  corr=8c1f  Processing HoldShipment for O-5521
09:14:09.512  fulfillment  corr=8c1f  WMS hold accepted
09:14:09.630  payments     corr=8c1f  Refund issued for O-5521
```

The story writes itself. The cancellation was approved correctly. The first hold request hit a transient conflict, the message was retried, and the retry succeeded. The refund followed. The warehouse must have shipped a held parcel, so the team opens a ticket with the logistics partner.

A week later, the partner's export and a closer look at the message broker tell a different story:

- **There were two messages, not one message retried.** Order `O-5521` had two shipments from two warehouses. The storefront published one hold command per shipment. The two "Processing" lines are different messages about different shipments, `S-1` and `S-2`, and neither line names its shipment. The second one arrived five seconds later because the second warehouse's queue had a backlog.
- **The 409 was not transient.** The WMS response body said `shipment_already_picked`. The worker logged only the status code, and a status code alone does not say whether a conflict is transient or permanent.
- **The storefront decided on stale information.** The WMS recorded the pick of `S-1` at 09:13:58.9 by its own clock. The webhook reporting it reached the storefront at 09:14:06, after the decision, so the decision read `S-1` as still allocated.
- **The refund rule had a bug.** Payments refunded the whole order when *any* hold succeeded, rather than when all of them had.
- **The webhooks were missing from the search.** The WMS does not know the storefront's correlation ID. Its `shipment.picked` and `shipment.dispatched` events were logged under the WMS shipment reference.
- **The trace had no spans.** This request was in the 90% that head sampling did not record, so no spans were exported for it. Its trace ID still appeared on every log line, which made it look as if there should be a trace to open.

No log line was false, although several were underspecified. The correlation ID found every line it was attached to, and the team still told the wrong story. The search could not show which lines were about the same thing, what had been decided and on what facts, what the other side observed, or what happened first.

> **A correlation ID tells you which records to read together. It does not tell you what each record means, whether any are missing, which caused which, or what happened first. Give every decision, intended effect, message, attempt, and external observation its own identifier. Record the intent before each handoff and the observed result after it, in a store that outlives the process. Use the correlation ID to find those records, and for nothing else.**

That does not require event sourcing, a cryptographic ledger, or a tracing product. For this store, the fix was a few database tables, consistent log scopes, and changes to what the worker recorded. The last sections cover how little many systems need.

One distinction before going further: better records would have explained this incident quickly, but they would not have prevented it. The confirmation email went out after the local decision, not after the WMS had held every shipment, and payments refunded on a partial result. Those are workflow-design weaknesses. Good evidence exposes them. It does not fix them.

---

## What a Correlation ID Actually Gives You

A correlation ID is a label that code copies onto records so that a search can group them. Its only built-in guarantee is weak: a record carries the value because some code had the value in hand when it wrote the record.

That is useful. Without it, the engineer in the example would have been joining timestamps and order numbers across three services by hand. It is also easy to read more into it than it can carry:

| What a reader concludes from shared correlation | What the correlation ID actually supports |
| --- | --- |
| These records are all the records for this workflow | No. Records written without the value, by a third party, or by code that lost it, are invisible to the search |
| These records describe the same operation | No. One workflow can contain several operations, as the two shipments did |
| Two matching lines are a retry of one thing | No. A retry, a duplicate delivery, and a second operation look the same |
| This record happened before that one | No. Timestamps from different hosts are not a reliable ordering |
| This record caused that one | No. Correlation is grouping, not causation |
| The operation was authorized | No. The value says nothing about who decided what |
| The operation happened | No. A log line says code reached a logging statement |
| The records are unaltered | No. Integrity is a property of the store, not of the label |

The same applies to a trace ID. A trace links spans into a tree for one technical execution path. It is excellent for finding where time went, and it is subject to sampling, retention, and exporter failure. It has a different job from evidence.

---

## One Workflow, Several Identities

The fix starts by giving each thing that can be decided, sent, attempted, or observed its own identity, minted by whoever creates it. The correlation ID stays. It just stops doing everyone else's job.

| Identifier | Minted by | Names | Question it answers |
| --- | --- | --- | --- |
| **Correlation ID** | The edge, once per workflow | The customer's cancellation | Which records belong together? |
| **Decision ID** | The storefront, at policy evaluation | One decision | What was decided, on which facts, under which policy version? |
| **Operation ID** | The storefront, when it plans an effect | One intended effect, such as "hold `S-1`" | Which effect is this record about? Stays the same across retries |
| **Message ID** | The publisher, as the outbox row's ID | One logical message | Is this the same message again? Stays the same when the relay republishes it |
| **Delivery count** | The broker, where it provides one | One delivery of a message | Is this a redelivery, and how many have there been? |
| **Attempt ID** | The worker | One call to the WMS | What did this try send, and what came back? |
| **External reference** | The WMS | The partner's shipment or hold record | What does the other side call it? |
| **External event ID** | The WMS | One webhook event | Have we already recorded this observation? |
| **Trace ID and span ID** | Tracing, through `Activity` | One technical path and one unit of work | Where did time go on this path, if the trace was recorded? |
| **Causation ID** | Whoever writes a record that something else led to | The record that directly led to this one | What caused this? |

Not every broker has a delivery count. Azure Service Bus exposes one, Amazon SQS exposes an approximate receive count, RabbitMQ sets a redelivered flag and counts deliveries only on some queue types, and Kafka has no per-message count at all. Where the broker gives you nothing reliable, count attempts per operation in your own attempt records instead. A delivery count of 1 never proves that an operation has not been processed elsewhere, because a relay that republished a message, or a second message for the same operation, starts its own count.

Four relationships are hiding in that table, and a single correlation ID collapses all of them:

- **Grouping:** these records are part of the same workflow. That is the correlation ID's job.
- **Identity:** these two records are about the same thing. That needs an operation ID, a message ID, or an external event ID, depending on the thing. The message ID catches the same message arriving twice. The operation ID catches two different messages asking for the same effect.
- **Causation:** this record exists because of that one. A hold operation records the decision that planned it. An attempt records the operation and message it served.
- **External linkage:** our record and the partner's record are about the same thing. That needs a stored mapping to the external reference, because the partner will not echo your correlation ID.

[Idempotency Does Not Prevent Authority Replay](idempotency-does-not-prevent-authority-replay.md#five-identifiers-five-questions) works through the operation, attempt, and idempotency-key identities in depth, including what a provider's idempotency key does and does not promise. This article uses them for a narrower purpose: so that records about different things stop looking alike.

---

## Reading the Incident Again

With those identities recorded, the same correlation ID finds a different set of records, and most of the questions answer themselves. This is the storefront's evidence for `O-5521`, selected by correlation ID and shown as rows rather than prose:

```text
decision   dec-41a  corr=8c1f  order=O-5521  Allowed  reason=no_shipment_picking  policy=cancel-policy/4
           decided: 09:14:02.140 (storefront clock)
           inputs: S-1.status=Allocated (as of webhook received 09:12:40)
                   S-2.status=Allocated (as of webhook received 09:11:02)

operation  op-17   corr=8c1f  cause=dec-41a  hold S-1   state=Rejected  reason=wms.shipment_already_picked
operation  op-18   corr=8c1f  cause=dec-41a  hold S-2   state=Held      wms=HLD-30944

attempt    att-91  op=op-17  msg=m-5d2  delivery=1  Rejected  reason=wms.shipment_already_picked  trace=1111... (no spans)
attempt    att-92  op=op-18  msg=m-5d3  delivery=1  Held      wms=HLD-30944                       trace=2222... (no spans)

observed   wms-evt-88120  shipment=S-1  shipment.picked      wms-time=09:13:58.9  received=09:14:06.204
observed   wms-evt-88347  shipment=S-1  shipment.dispatched  wms-time=11:40:12.0  received=11:40:15.871

refund     rf-3c0  corr=8c1f  cause=op-18  amount=full order
```

The webhook rows carry no correlation ID from the partner. The storefront added it when it recorded them, by looking up the WMS shipment reference in its own durable mapping from partner references to shipments, and from the shipment to its order and cancellation workflow. The lookup does not depend on any operation still being open: `op-17` was already `Rejected` when the `picked` event arrived, and the `dispatched` event came hours later.

Each question now has an answer that does not depend on inference:

- **Was it a retry?** No. Two operations, two messages, each delivered once.
- **What did the decision know?** It read `S-1` as allocated, from a webhook received at 09:12:40, about 80 seconds before the decision. Policy version 4 had no rule about how fresh shipment status had to be. The recorded inputs explain why the storefront returned `Allowed` under its local policy. They do not show that the shipment state was current, or that confirming a cancellation before the WMS accepted every hold was safe. That is a design question for a different article: [Your Security Check Passed. Then the World Changed.](security-check-passed-then-world-changed.md)
- **What did the WMS say?** It rejected the `S-1` hold because the shipment was already picked.
- **Why was there a refund?** `rf-3c0` was caused by `op-18` alone. The refund rule fired on one held shipment out of two.
- **Was the partner at fault?** No. `S-1` was never held.

Notice what settles the ordering question, and what does not. The WMS clock says the pick happened at 09:13:58.9, and the storefront clock says the decision happened at 09:14:02.140. Those are readings from two clocks owned by two organizations. They suggest an order, but they do not prove one.

The proof that matters uses one clock. On the storefront's clock, the decision was made at 09:14:02.140, and the `picked` webhook was received at 09:14:06.204. The decision recorded that its latest information about `S-1` came from 09:12:40. So the storefront did not know about the pick when it decided. That is the question the team actually needed answered. The WMS event is separate evidence of when the partner says the pick occurred. Establishing the exact order across both systems would take more: synchronized clocks, partner sequence numbers, or authoritative records from the system that did the work.

---

## Record the Intent Before the Handoff, and the Result After

Each record should be written by the component that knows the fact, at the moment it knows it, in a store that outlives the process. The rule at each handoff is the same: **record what you are about to hand off before you hand it off, and record what came back when it comes back.** There is always a gap between your local write and the other system's effect. These records make the gap visible instead of hiding it.

### The storefront: decision, planned effects, and messages in one transaction

The decision, the operations it plans, and the outbox messages that carry them are written together. If the transaction fails, none of them exist. If it commits, the intent to send each message is durably recorded. A relay then publishes the outbox rows and retries until the broker acknowledges them, provided the relay is running, its storage is durable, and someone notices when it stalls. A relay can also crash after publishing and before marking a row as sent, so it may publish the same message again. That is why consumers must tolerate duplicates, and why the message ID is the outbox row's ID rather than a value minted at publish time.

```csharp
// Illustrative. The decision service reads the facts it needs and returns them as recorded inputs.
await using var tx = await db.Database.BeginTransactionAsync(ct);

var decision = await decisions.EvaluateCancellationAsync(order, ct);
db.CancellationDecisions.Add(decision.ToRecord(correlationId));

if (decision.Outcome is not DecisionOutcome.Allowed)
{
    await db.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);
    return Results.Conflict(new { reason = decision.ReasonCode });
}

foreach (var shipment in order.Shipments)
{
    var operation = HoldOperation.Plan(
        decisionId: decision.DecisionId,      // causation: the decision that planned this effect
        correlationId: correlationId,
        shipmentId: shipment.Id);

    db.HoldOperations.Add(operation);

    var headers = new Dictionary<string, string>
    {
        ["correlation-id"] = correlationId,
        ["causation-id"] = decision.DecisionId.ToString(),
    };

    // W3C trace context, only if there is an activity and your messaging library
    // does not already add it. Never write an empty header.
    if (Activity.Current?.Id is { } traceParent)
    {
        headers["traceparent"] = traceParent;
    }

    db.Outbox.Add(new OutboxMessage(
        Id: Guid.NewGuid(),                   // the message ID; the relay reuses it on every publish
        Type: "fulfillment.hold-shipment",
        Body: new HoldShipment(operation.OperationId, shipment.Id),
        Headers: headers));
}

await db.SaveChangesAsync(ct);
await tx.CommitAsync(ct);
```

Three details carry most of the value:

- **The decision record holds its inputs, and how current they were.** `S-1.status=Allocated` and the time the storefront learned it are what answered the incident's hardest question. [Your Audit Log Records the Story, Not the Decision](your-audit-log-is-not-evidence.md) covers decision records, reason codes, and policy versions in more depth.
- **Each shipment gets its own operation and message.** The correlation ID is shared. Nothing else is.
- **The trace context travels in a header beside the correlation ID, not instead of it.** With the W3C ID format, the default since .NET 5, `Activity.Id` is a `traceparent` value. ASP.NET Core and `HttpClient` propagate trace context over HTTP without application code. Messaging libraries vary. If yours propagates trace context, let it, and do not add a second header. If you write your own integration, prefer injecting through the configured propagator, such as `DistributedContextPropagator.Current` or OpenTelemetry's propagator API, so that `tracestate` is carried too. Copying `Activity.Id` is the minimal version.

### The worker: claim, attempt, and outcome

The worker first claims the operation, so that two deliveries cannot call the WMS at the same time. It then records the attempt before calling the WMS, and records the outcome, including the WMS's reason, whatever happens.

```csharp
public async Task HandleAsync(IncomingMessage<HoldShipment> message, CancellationToken ct)
{
    // In this illustrative host, returning normally acknowledges the message.
    // Throwing leaves it to the broker's redelivery and dead-letter policy.
    var hold = message.Body;

    using var scope = logger.BeginScope(new Dictionary<string, object?>
    {
        ["CorrelationId"] = message.Headers.GetValueOrDefault("correlation-id"),
        ["OperationId"] = hold.OperationId,
        ["MessageId"] = message.MessageId,
        ["DeliveryCount"] = message.DeliveryCount,   // null where the broker provides none
    });

    // Atomically moves the operation to Sending, under a lease, if it is Planned, Unknown,
    // or Sending with an expired lease. Only one worker can win the claim.
    var claim = await operations.TryClaimAsync(hold.OperationId, lease: TimeSpan.FromMinutes(2), ct);

    switch (claim.Status)
    {
        case ClaimStatus.NotFound:
            // Missing state is not a duplicate. It may be a lagging replica or a bad message.
            // Let bounded redelivery and dead-lettering handle it.
            throw new OperationNotFoundException(hold.OperationId);

        case ClaimStatus.AlreadyFinal:
            logger.LogInformation("Hold operation is {State}; acknowledging duplicate delivery", claim.State);
            return;

        case ClaimStatus.ClaimedElsewhere:
            // Another delivery is calling the WMS now. Try again later rather than call twice.
            throw new OperationBusyException(hold.OperationId);
    }

    var attempt = await attempts.StartAsync(
        operationId: hold.OperationId,
        messageId: message.MessageId,
        deliveryCount: message.DeliveryCount,
        traceId: Activity.Current?.TraceId.ToHexString(),   // navigation aid only
        ct);

    WmsHoldResult result;
    try
    {
        // The WMS client turns every HTTP response, including a 409 and its reason code, into a result.
        // The operation ID, not the attempt or message ID, is the idempotency key.
        result = await wms.HoldAsync(hold.ShipmentId, idempotencyKey: hold.OperationId.ToString(), ct);
    }
    catch (Exception ex) when (WmsErrors.FailedBeforeSending(ex))
    {
        result = WmsHoldResult.NotSent(WmsErrors.ReasonCode(ex));
    }
    catch (Exception ex)
    {
        // A timeout, a dropped connection, or anything else after the request may have left.
        result = WmsHoldResult.Unknown(WmsErrors.ReasonCode(ex));
    }

    // Records the attempt's result and the operation's next state in one transaction,
    // with a version check so that a stale writer cannot overwrite a final state.
    await operations.RecordAttemptResultAsync(attempt.AttemptId, result, ct);

    logger.LogInformation(
        "Hold attempt {AttemptId} finished {Result} with reason {ReasonCode}",
        attempt.AttemptId, result.Kind, result.ReasonCode);
}
```

The operation moves through a small set of states:

| State | Meaning | Final? |
| --- | --- | --- |
| `Planned` | Recorded with the decision; not yet claimed | No |
| `Sending` | Claimed by one worker under a lease | No |
| `Held` | The WMS confirmed the hold | Yes |
| `Rejected` | The WMS refused, with a reason such as `shipment_already_picked` | Yes |
| `NotSent` | The request failed before it could have left; returns to `Planned` for a later delivery | No |
| `Unknown` | The request may have reached the WMS, but no answer came back | No |

The claim is the first layer of protection against two workers calling out at once. The WMS idempotency key is a second layer, not a substitute. A `Sending` operation whose lease has expired is treated like `Unknown`, because the worker that held it may have sent the request before it died.

An `Unknown` outcome is not a failure. The WMS may have applied the hold before the connection dropped. A later delivery may claim the operation and send again with the same operation ID, but only if the WMS documents that it honors that idempotency key for this request. Without that guarantee, an unknown outcome needs reconciliation, such as asking the WMS for the shipment's hold status, not a resend. [An Allowed Decision Does Not Mean the Operation Succeeded](allowed-decision-does-not-mean-execution-succeeded.md) covers unknown outcomes and what to do with them.

The log lines are still there. They now name which operation and attempt they were about, and the reason code comes from the WMS response body rather than the status code alone. The durable record is the attempt row, and the log line is how an engineer finds it quickly. Duplicate deliveries for finished operations do not create attempt rows. Their log line, with message ID and delivery count in scope, is enough to show that they arrived.

### The webhook: an observation, mapped to your identities

The partner will not send your correlation ID back, and you should not depend on it doing so. Record each event as an observation in the partner's terms, deduplicate it, and attach your identities through a durable mapping from the partner's references to your own records.

```csharp
// Illustrative record of something another system told us.
public sealed record ExternalObservation(
    string Source,                   // "wms"
    string ExternalEventId,          // the partner's event ID
    string ExternalReference,        // the partner's shipment or hold reference
    string EventType,                // "shipment.picked"
    DateTimeOffset ExternalTime,     // the partner's clock: their claim, not our ordering
    DateTimeOffset ReceivedAt,       // our clock: when we learned it
    string? ShipmentId,              // resolved through our mapping, if found
    string? CorrelationId);          // attached by us, never supplied by them
```

Base the mapping on stable business identity, such as the WMS shipment reference recorded when the shipment was created, not on whichever workflow happens to be open. A shipment can outlive a cancellation workflow, and one external event can be relevant to several workflows over time. Where that happens, link the observation to the shipment and let each workflow find it from there, rather than forcing a single correlation ID onto it.

Store both timestamps. `ExternalTime` is what the partner says. `ReceivedAt` is when your system could first have acted on it. In the incident, `ReceivedAt` was the timestamp that answered the question.

Deduplicating on the partner's event ID works only if the partner guarantees that it is unique and stable across retries. Some senders issue a new event ID when they redeliver. Check the partner's contract. Where it is weak, add a second check on a business key, such as shipment, event type, and external time together.

Verify the webhook's signature or other sender authentication before recording anything from it as an observation. An unauthenticated request is a claim from anyone. If you keep a security record of rejected webhook requests, keep it minimal and separate from authenticated business observations.

---

## Duplicates, Redelivery, and Out-of-Order Arrival

Most brokers deliver at least once by default. Some offer at-most-once modes, and some offer exactly-once features with conditions attached, but at-least-once is the common case to design for. Webhook senders retry. Messages for related operations travel through different queues at different speeds. The records have to stay correct under all three, and a shared correlation ID helps with none of them.

**A duplicate and a second operation look identical by correlation ID.** In the incident, the two "Processing" lines were two operations that the team read as one retry. The opposite mistake is worse. A worker that deduplicates on correlation ID would have dropped the hold for `S-2` as a "duplicate" of `S-1`. Deduplicate on the identity of the thing that can be duplicated:

| What can arrive twice | Deduplicate on |
| --- | --- |
| A broker message, redelivered or republished by the relay | Message ID, plus delivery count where the broker provides one |
| A request to perform an effect | Operation ID, through the claim, state checks, and the downstream idempotency key |
| A webhook event | The sender's event ID, scoped to the sender, if the sender guarantees it is stable; otherwise a business key as well |
| A client resubmission | A client-generated idempotency key, scoped to the caller |

**Arrival order is not occurrence order.** The `shipment.picked` webhook for `S-1` arrived after the decision that it would have changed. A `shipment.dispatched` event can arrive before `shipment.picked` if the sender retries one and not the other. Do not sort events from different systems by timestamp and call the result a history. Use each record's own state and version instead:

- **Make state transitions conditional.** An operation moves between the states above only with a version check. A late or duplicate result cannot move a final state backward.
- **Accept facts that arrive early.** If the WMS reports a hold before the worker has recorded its own result, for example because the worker crashed after the call, record the observation and let reconciliation settle the operation. Do not drop it because the local state "isn't there yet."
- **Keep your own sequence where order matters.** Within one service, a database sequence or a per-operation version orders records reliably. Across services, order comes from causation links and from what each decision recorded that it had read.

[Distributed Acknowledgment and Continuation Workflows](../../advanced/distributed-acknowledgment-and-continuation-workflows.md#11-out-of-order-delivery-is-normal-not-exceptional) treats out-of-order delivery and duplicate delivery for a pause-and-resume workflow in more detail.

---

## Traces Are Navigation, Not Evidence

Distributed tracing is the most natural place to look for a cross-service story, and in the incident it held nothing.

Tracing systems sample. With head sampling, the decision is made at the start of a trace and propagated in the `sampled` flag of the W3C `traceparent` header. The specification describes that flag as meaning the caller *may* have recorded trace data. A trace that was not sampled exports no spans, although log records written during it may still carry its trace ID, as they did here. Tail sampling, which decides after a trace completes, can keep more of the traces you care about, such as those with errors, but it is still a selection made for diagnosis. A trace that *was* kept may still be lost to exporter backpressure, collector failure, or a retention window shorter than your audit obligations. None of that is a defect. It is what makes tracing cheap enough to leave on.

So the rule from [AI Governance Observability and End-to-End Decision Tracing](../../ai-integration/ai-governance-observability-and-end-to-end-decision-tracing.md#sampling-and-the-evidence-boundary) applies to any workflow:

```text
Trace missing  ≠  operation did not happen
Trace present  ≠  durable or tamper-evident record
```

Asynchronous hops make traces less complete still. OpenTelemetry's messaging semantic conventions, still marked as in development as of November 2026, use span *links* by default to connect a consumer's processing to the context that created a message. Using the creation context as the parent of a processing span is allowed only for single-message processing, and is not the default. A batch consumer can only use links, because a span has only one parent. Your tracing backend may therefore show a queue-crossing workflow as several linked traces rather than one tree. Links describe how telemetry relates. They are not the durable, domain-level causation records described above.

Use each for what it is good at:

| | Telemetry (traces, metrics, most logs) | Evidence records |
| --- | --- | --- |
| **Purpose** | Diagnose behavior and performance | Answer what was decided and what happened |
| **Completeness** | Sampled, rate-limited, best-effort export | Written for every operation in scope |
| **Lifetime** | Days to weeks | As long as your obligations require |
| **Write path** | Asynchronous, off the request path | Same transaction as the state it describes, or a durable outbox |
| **Failure behavior** | Drop and carry on | Defined by policy; may block the operation |
| **Identity** | Trace and span IDs | Decision, operation, message, attempt, and event IDs |

Put the trace ID on evidence records as a convenience, so an engineer can jump to a trace when one exists. Never make an evidence query depend on it.

---

## Carrying Identifiers Without Carrying Everything Else

The fix in the example adds identifiers to every boundary. That raises a fair worry: what else starts crossing boundaries with them?

**Identifiers, not payloads.** Messages and headers should carry the IDs needed to find records, not copies of the records. A hold command needs the operation ID and the shipment ID. It does not need the customer's address, the order lines, or the decision's inputs. Each service can look those up in its own store if it is allowed to. [Secure Logging Across Trust Boundaries](../../security/secure-logging-across-trust-boundaries.md#stable-event-identity-reduces-pressure-to-preserve-payloads) explains why stable identities reduce the pressure to log payloads.

**Never credentials.** A bearer token in a message header becomes a credential at rest in the broker, in dead-letter queues, and in every log that dumps headers. [A Background Job Should Not Inherit Everything the User Could Do](background-job-should-not-inherit-user-authority.md) covers what should cross to a worker instead.

**Caller-supplied correlation IDs are input.** If clients may send an `X-Correlation-ID` header, check its length and characters, and generate your own value when it fails. Never let it select a tenant, a user, or a record. [Secure Logging Across Trust Boundaries](../../security/secure-logging-across-trust-boundaries.md#caller-supplied-correlation-values-are-untrusted-input) gives a safe flow. The same applies to an inbound `traceparent` from the public internet. The W3C Trace Context specification's security and privacy considerations discuss restarting the trace where requests enter a trusted network, and treating authenticated and unauthenticated callers differently, because an untrusted caller can otherwise force sampling or collide with your trace IDs. Restarting at that trust boundary is a deployment choice consistent with that guidance. If you restart, you can still keep the caller's value as a link or an attribute for support purposes.

**Baggage can cross every instrumented boundary.** W3C Baggage is forwarded wherever a baggage propagator is configured, and OpenTelemetry's default propagator configuration includes one. With typical settings, baggage set through the OpenTelemetry API or on an `Activity` is then added to instrumented outbound calls, including calls to third parties. The OpenTelemetry documentation warns that it can reach unintended recipients and has no integrity checks. Check your propagator configuration, use baggage only for small, non-sensitive routing hints, and never treat a baggage value as a trusted fact.

**Decide what the partner sees.** Sending your internal operation ID to the WMS as an idempotency key is a deliberate choice. The partner needs a stable key, and a random version 4 GUID reveals little and no customer data, although the partner can, by design, tell that two requests carry the same key. A time-ordered GUID, such as version 7, also reveals when it was created. Sending your correlation ID, customer ID, or decision ID to a third party is a different choice, and usually unnecessary. Keep the mapping on your side.

**A correlation ID is not authority.** A worker must not conclude that a message is allowed, or which tenant it belongs to, from the fact that its correlation ID matches an earlier, authorized request. The correlation ID connects records. It never grants anything.

---

## When Structured Logs Plus Durable Operation Records Are Enough

For many ordinary application workflows, a correlation ID in every log scope plus a durable record per operation provides enough accountability. That record holds the operation's state, its decision or reason code, its idempotency key, and the external reference. You do not need event sourcing, a separate audit store, or signed records to answer "what happened to this order?"

Be careful with what "a row" means, though. A single mutable row shows the current state, not how it got there. Where the path matters, keep attempts and external observations as rows that are added and never updated, as the example does.

That is what the store in the example ended up with: a decision table, a hold-operation table with an attempts child table, an outbox keyed by message ID, a table of webhook observations, and the shipment reference mapping it already had. All of them are in the same database the storefront already used. The logs gained four scope fields and a reason code.

Go further only when a specific need appears:

- **Durable decision records with inputs and policy versions**, when someone outside the team may ask why an operation was allowed, or when the facts a decision read will have changed by the time anyone looks. [Your Audit Log Records the Story, Not the Decision](your-audit-log-is-not-evidence.md#failure-1--the-record-describes-the-outcome-not-the-decision) lists the fields.
- **Per-attempt records**, when calls to another system can time out with an unknown outcome or carry money, inventory, or access.
- **External observation records**, when a partner's events change what your system believes or does.
- **A separate, append-only, or tamper-evident store**, when the threat model includes someone who can edit the records, and you can name who that is. A database transaction gives atomicity. It does not stop a sufficiently privileged user from changing the rows later. [Your Audit Log Records the Story, Not the Decision](your-audit-log-is-not-evidence.md#failure-4--integrity-ends-at-the-log-sink) lays out that progression.
- **Event sourcing**, when the domain itself benefits from it, not as an audit mechanism. [Event Sourcing, Audit Trails, and Governance Decision Provenance](../../architecture/event-sourcing-audit-trails-and-governance-decision-provenance.md) compares the record models.

A single-service CRUD endpoint with no asynchronous handoff needs none of the cross-service identities in this article. It may still need decision or attempt records if it moves money, inventory, or access, and an audit column that holds only the last modifier and timestamp does not show history.

---

## Testing the Records

Records that nobody asserts on decay quietly. A refactor reuses the correlation ID as the message ID "for simplicity," and nothing fails until the next incident. Test the identities, not only the behavior. The harness types in these tests are illustrative helpers that are not shown.

The first test pins down that one workflow fans out into distinct identities with shared grouping and shared causation:

```csharp
[Fact]
public async Task Cancelling_a_two_shipment_order_plans_one_operation_and_message_per_shipment()
{
    var harness = await StorefrontHarness.WithOrderAsync("O-5521", shipments: ["S-1", "S-2"]);

    await harness.CancelAsync("O-5521", correlationId: "8c1f");

    var decision = Assert.Single(harness.Decisions);
    Assert.Equal(DecisionOutcome.Allowed, decision.Outcome);
    Assert.Equal("cancel-policy/4", decision.PolicyVersion);
    Assert.Equal("Allocated", decision.Inputs["S-1.status"]);

    Assert.Equal(2, harness.HoldOperations.Count);
    Assert.All(harness.HoldOperations, op =>
    {
        Assert.Equal("8c1f", op.CorrelationId);
        Assert.Equal(decision.DecisionId, op.DecisionId);
    });

    // Every identity is distinct: no message, operation, or decision reuses another's value.
    var operationIds = harness.HoldOperations.Select(op => op.OperationId).ToList();
    var messageIds = harness.Outbox.Select(m => m.Id).ToList();
    var allIds = operationIds.Concat(messageIds).Append(decision.DecisionId).ToList();
    Assert.Equal(allIds.Count, allIds.Distinct().Count());

    Assert.All(harness.Outbox, m => Assert.Equal("8c1f", m.Headers["correlation-id"]));
}
```

The second test assumes that the WMS contract guarantees idempotent handling of repeated requests with the same key. Without that guarantee, the right behavior after an unknown outcome is reconciliation, not a resend, and a companion test should assert that no second request is sent. Under the assumption, a redelivery after an unknown outcome is recorded as another attempt at the same operation, with the same idempotency key, and a late duplicate does not change a final state:

```csharp
[Fact]
public async Task Redelivery_after_unknown_outcome_reuses_the_operation_identity()
{
    var operationId = Guid.NewGuid();
    var messageId = Guid.NewGuid();
    var wms = new ScriptedWms(
        WmsHoldResult.Unknown("wms.no_response"),
        WmsHoldResult.Held("HLD-30944"));
    var harness = await WorkerHarness.WithPlannedHoldAsync(operationId, "S-2", wms);

    await harness.DeliverAsync(messageId, deliveryCount: 1);
    await harness.DeliverAsync(messageId, deliveryCount: 2);
    await harness.DeliverAsync(messageId, deliveryCount: 3);   // late duplicate after success

    Assert.Equal(2, wms.IdempotencyKeysReceived.Count);
    Assert.All(wms.IdempotencyKeysReceived, key => Assert.Equal(operationId.ToString(), key));

    var attempts = harness.AttemptsFor(operationId);
    Assert.Equal(new int?[] { 1, 2 }, attempts.Select(a => a.DeliveryCount));
    Assert.Equal(new[] { "Unknown", "Held" }, attempts.Select(a => a.Result));

    Assert.Equal("Held", harness.Operation(operationId).State);
}
```

Two more tests are worth adding. For the worker, deliver a message whose operation does not exist and assert that the handler does not acknowledge it. For webhooks, deliver the same partner event ID twice and assert that one observation is recorded, then deliver a `dispatched` event before a `picked` event and assert that both are recorded with their external and received times intact.

---

## Failure Modes

**1. Deduplicating on correlation ID.** Every operation in the workflow after the first is discarded as a duplicate. In the example, `S-2` would never have been held.

**2. One identifier for everything.** The correlation ID doubles as the message ID, the idempotency key, or the decision ID. Records about different things become indistinguishable, and a downstream idempotency check suppresses a legitimate second operation.

**3. A new correlation ID at every hop.** Each service generates its own value instead of carrying the incoming one, so the search finds one service's records at a time. Generate once at the edge, carry it, and record a causation ID where one record leads to another.

**4. Ordering by timestamps across systems.** Clocks on different hosts and in different organizations disagree, and arrival order is not occurrence order. Order within a service by sequence or version, and across services by causation and recorded inputs.

**5. Treating the trace as the record.** A missing trace is read as "did not happen," or a present trace is presented as proof. Sampling, export loss, and retention make traces navigation only.

**6. Logging the status code without the reason.** "409 Conflict" read as transient was the hinge of the wrong story. Record the counterpart's reason code with the attempt.

**7. Expecting the partner to echo your correlation ID.** External events are logged under the partner's references and never found. Keep a durable mapping from external references to your own records, and attach your identities when you record the observation.

**8. Copying payloads into headers, baggage, or every log line.** Identifiers were meant to make that unnecessary. Baggage in particular can propagate to third parties.

**9. Using correlation as authority or tenancy.** A worker trusts a message because its correlation ID matches an authorized request, or resolves its tenant from it. Correlation connects records. It never decides.

**10. Recording after the handoff.** A worker that calls the partner and then writes the attempt row loses every attempt that crashes in between. Record the attempt before the call and its result after.

**11. Treating missing state as a duplicate.** A worker that acknowledges a message because it cannot find the operation discards legitimate work whenever a replica lags or a write is delayed.

**12. Relying on the partner's idempotency alone.** Two workers that both see a non-final operation both call out. Claim the operation first, and treat the downstream key as a second layer.

---

## A Short Review Checklist

**Identity**

1. Is the correlation ID generated once at the edge, validated if a caller supplies it, and carried unchanged across every hop?
2. Do decisions, intended effects, messages, attempts, and external events each have their own identifier, minted by whoever creates them?
3. Is nothing deduplicated, keyed, or authorized by correlation ID?
4. Does each record that something else led to carry a causation ID pointing at its cause?

**Records at each boundary**

5. Is the decision recorded with its reason code, policy version, and the inputs it read, including how current those inputs were?
6. Are planned effects and their outbox messages written in the same transaction as the decision, with a message ID the relay reuses on every publish?
7. Does the worker claim the operation before calling out, and treat a missing operation as an error rather than a duplicate?
8. Does every started attempt get an outcome, including the counterpart's reason code, `NotSent`, or `Unknown`?
9. Is a resend after `Unknown` allowed only where the counterpart documents that it honors the idempotency key, with reconciliation otherwise?
10. Are external events recorded with the partner's event ID, the partner's time, your received time, and a mapping to your identities based on stable business references?

**Ordering and duplicates**

11. Are state transitions conditional, so late or duplicate results cannot move a final state backward?
12. Is order established by sequence, version, causation, and recorded inputs rather than by timestamps from different hosts?
13. Have you checked what your broker provides for redelivery counts, and what your partners guarantee about event ID uniqueness?

**Telemetry and leakage**

14. Can every evidence question be answered without a trace existing?
15. Do headers, baggage, and log scopes carry identifiers only, with no credentials, payloads, or sensitive facts?
16. Is an inbound `traceparent` from untrusted callers either restarted or deliberately accepted?

**Proportion and tests**

17. Is the extra recording limited to workflows that cross an asynchronous boundary, call another system, or may be questioned later?
18. Do tests assert that identities are distinct, that redelivery reuses the operation identity, that missing state is not acknowledged, and that duplicate external events are recorded once?

---

## Continue Deeper

For what a single durable record needs, including decision capture, binding to the operation, write ordering, and integrity, read [Your Audit Log Records the Story, Not the Decision](your-audit-log-is-not-evidence.md).

To choose between operational logs, audit history, decision records, and event sourcing as record models, [Event Sourcing, Audit Trails, and Governance Decision Provenance](../../architecture/event-sourcing-audit-trails-and-governance-decision-provenance.md#correlation) compares them and separates correlation from causation and authority.

For trace and span relationships, sampling, and telemetry that must not become authority, [AI Governance Observability and End-to-End Decision Tracing](../../ai-integration/ai-governance-observability-and-end-to-end-decision-tracing.md#keep-identity-layers-distinct) applies the same identity layers to an AI-governed pipeline.

When a decision pauses for a human response and resumes on another host, [Distributed Acknowledgment and Continuation Workflows](../../advanced/distributed-acknowledgment-and-continuation-workflows.md#20-preserve-end-to-end-lineage-without-conflating-ids) shows how to keep end-to-end lineage without conflating identifiers.

### Related Work

Each source below was last verified on November 8, 2026.

- [W3C Trace Context](https://www.w3.org/TR/trace-context/) defines the `traceparent` and `tracestate` headers and the `sampled` flag, and covers privacy and security considerations, including restarting traces at trust boundaries.
- [W3C Baggage](https://www.w3.org/TR/baggage/) defines the `baggage` header. [OpenTelemetry's baggage overview](https://opentelemetry.io/docs/concepts/signals/baggage/) explains why it should not carry sensitive data.
- [OpenTelemetry semantic conventions for messaging spans](https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/), marked as in development, describe creation context and the use of span links between producers and consumers.
- [Distributed tracing concepts](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-concepts), in the .NET documentation, covers `Activity`, the W3C ID format, propagation over HTTP, and sampling.
- [Transactional outbox](https://microservices.io/patterns/data/transactional-outbox.html), Chris Richardson's description of the pattern on microservices.io, covers writing outbound messages in the same transaction as the state change, and notes that a relay may publish a message more than once.

---

## The Short Answer

A correlation ID is a search key. It finds the records that carry it, and it says nothing about what they mean, whether any are missing, which caused which, or in what order things happened. Keep it, carry it from the edge, and give everything else its own identity: the decision, each intended effect, each message, each attempt, and each external event. Record the intent before each handoff and the result after it, with the inputs a decision read and the reason a counterpart gave. Order things by state, version, and causation, not by clocks. Treat traces as a fast way to look around, not as the record. For many workflows, that is a few database tables and some log scope fields, not a new system.
