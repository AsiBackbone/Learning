---
description: A correlation ID across an API, queue, worker, and webhook finds related logs. It cannot show what was decided, which effect each line was about, or the order.
title: A Correlation ID Is Not a Distributed Audit Trail
author: Christopher D. Cavell
published: "2026-11-08"
summary: A correlation ID tells you which records to read together. It does not say what each record means, whether any are missing, which one caused which, or what happened first. Give every decision, intended effect, message, attempt, and external observation its own identifier. Record each one durably at the boundary where it happens, and use the correlation ID only to find them. Treat sampled traces as navigation, not evidence.
feed: true
x_hashtags:
  - DotNet
  - SoftwareArchitecture
---

# A Correlation ID Is Not a Distributed Audit Trail

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** No formal prerequisites. Familiarity with ASP.NET Core, `ILogger`, a message broker, and the idea of distributed tracing is helpful. No AsiBackbone package or prior Learning material is required.

**Sample scope:** The C# targets .NET 8 or later. The record types, repositories, message envelope, and warehouse client are illustrative, not a runnable sample or a recommended schema. The tests rely on helpers that are described but not shown. The scenario and its incident are hypothetical. The code snippets in this article are also offered under the MIT License, as described in the repository's [licensing terms](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md). Tracing behavior is described from the W3C Trace Context specification, the OpenTelemetry documentation, and Microsoft's .NET documentation as of November 2026. Check the current documentation for your versions.

**What this article covers:** what a correlation ID does and does not establish; how one accurate log search produced a wrong incident story; the identifiers that should sit beside the correlation ID and the question each answers; what each service should record before and after an asynchronous handoff; duplicate delivery, redelivery, and out-of-order arrival; why sampled traces are navigation rather than evidence; how to carry identifiers across boundaries without carrying payloads, credentials, or caller-controlled authority; when structured logs plus a database row are enough; and how to test the records.

**In plain terms:** a correlation ID is a case number written on every page of a file. It lets you pull every page of that case from the cabinet. It does not tell you which page is the signed decision and which is a phone note, whether a page is missing, or which letter was sent first. Those answers come from what each page records, not from the number they share.

Consider a hypothetical online store. A customer may cancel an order until any of its shipments starts picking in the warehouse. The storefront API makes that decision. A fulfillment worker then asks the warehouse management system (WMS), a separate service run by a logistics partner, to hold each shipment. A payments service issues the refund. The WMS reports shipment progress back to the storefront through webhooks.

Every service writes structured logs. The storefront assigns a correlation ID when the cancellation request arrives, and every service puts that ID in its log scope and passes it along in message headers. The team traces requests with OpenTelemetry at a 10% head-sampling rate.

A customer complains. They received a "cancellation confirmed" email and a refund, and then the parcel arrived anyway. The on-call engineer searches the log store for the request's correlation ID and finds this:

```text
09:14:02.118  storefront   corr=8c1f  Cancel requested for order O-5521
09:14:02.140  storefront   corr=8c1f  Cancellation approved
09:14:02.161  storefront   corr=8c1f  Cancellation confirmation email queued
09:14:03.902  fulfillment  corr=8c1f  Processing CancelOrder for O-5521
09:14:04.010  fulfillment  corr=8c1f  WMS hold request failed: 409 Conflict
09:14:09.377  fulfillment  corr=8c1f  Processing CancelOrder for O-5521
09:14:09.512  fulfillment  corr=8c1f  WMS hold accepted
09:14:09.630  payments     corr=8c1f  Refund issued for O-5521
```

The story writes itself. The cancellation was approved correctly. The first hold request hit a transient conflict, the message was retried, and the retry succeeded. The refund followed. The warehouse must have shipped a held parcel, so the team opens a ticket with the logistics partner.

A week later, the partner's export and a closer look at the message broker tell a different story:

- **There were two messages, not one message retried.** Order `O-5521` had two shipments from two warehouses. The storefront published one hold command per shipment. The two "Processing" lines are different messages about different shipments, `S-1` and `S-2`. The second one arrived five seconds later because the second warehouse's queue had a backlog.
- **The 409 was not transient.** The WMS response body said `shipment_already_picked`. The worker logged only the status code.
- **`S-1` had been picked before the storefront decided.** The WMS recorded the pick at 09:13:58.9 by its own clock. The webhook reporting it reached the storefront at 09:14:06, so the decision read `S-1` as still allocated.
- **The refund rule had a bug.** Payments refunded the whole order when *any* hold succeeded, rather than when all of them had.
- **The webhooks were missing from the search.** The WMS does not know the storefront's correlation ID. Its `shipment.picked` and `shipment.dispatched` events were logged under the WMS shipment reference.
- **The trace did not exist.** This request was in the 90% that head sampling did not record. Every log line carried a trace ID that pointed at nothing.

No log line was false. The correlation ID found every line it was attached to, and the team still told the wrong story. The search could not show which lines were about the same thing, what had been decided and on what facts, what the other side observed, or what happened first.

> **A correlation ID tells you which records to read together. It does not tell you what each record means, whether any are missing, which caused which, or what happened first. Give every decision, intended effect, message, attempt, and external observation its own identifier. Record each one durably at the boundary where it happens. Use the correlation ID to find them, and nothing else.**

That does not require event sourcing, a cryptographic ledger, or a tracing product. For this store, the fix was a few database tables, consistent log scopes, and one change to what the worker logged. The last sections cover how little most systems need.

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
| **Message ID** | The publisher, usually the outbox row | One message | Is this the same message again? |
| **Delivery count** | The broker | One delivery of a message | Is this a redelivery, and how many have there been? |
| **Attempt ID** | The worker | One call to the WMS | What did this try send, and what came back? |
| **External reference** | The WMS | The partner's shipment or hold record | What does the other side call it? |
| **External event ID** | The WMS | One webhook event | Have we already recorded this observation? |
| **Trace ID and span ID** | Tracing, through `Activity` | One technical path and one unit of work | Where did time go on this path, if the trace was recorded? |
| **Causation ID** | Whoever writes a record that something else led to | The record that directly led to this one | What caused this? |

Four relationships are hiding in that table, and a single correlation ID collapses all of them:

- **Grouping:** these records are part of the same workflow. That is the correlation ID's job.
- **Identity:** these two records are about the same thing. That needs an operation ID, a message ID, or an external event ID, depending on the thing.
- **Causation:** this record exists because of that one. A hold operation records the decision that planned it. An attempt records the operation and message it served.
- **External linkage:** our record and the partner's record are about the same thing. That needs a stored mapping to the external reference, because the partner will not echo your correlation ID.

[Idempotency Does Not Prevent Authority Replay](idempotency-does-not-prevent-authority-replay.md#five-identifiers-five-questions) works through the operation, attempt, and idempotency-key identities in depth, including what a provider's idempotency key does and does not promise. This article uses them for a narrower purpose: so that records about different things stop looking alike.

---

## Reading the Incident Again

With those identities recorded, the same correlation ID finds a different set of records, and the questions answer themselves. This is the storefront's evidence for `O-5521`, selected by correlation ID and shown as rows rather than prose:

```text
decision   dec-41a  corr=8c1f  order=O-5521  Allowed  reason=no_shipment_picking  policy=cancel-policy/4
           inputs: S-1.status=Allocated (as of webhook received 09:12:40)
                   S-2.status=Allocated (as of webhook received 09:11:02)

operation  op-17   corr=8c1f  cause=dec-41a  hold S-1   state=Rejected  reason=wms.shipment_already_picked
operation  op-18   corr=8c1f  cause=dec-41a  hold S-2   state=Held      wms=HLD-30944

attempt    att-91  op=op-17  msg=m-5d2  delivery=1  Rejected  reason=wms.shipment_already_picked  trace=4bf9... (not sampled)
attempt    att-92  op=op-18  msg=m-5d3  delivery=1  Held      wms=HLD-30944                       trace=e07a... (not sampled)

observed   wms-evt-88120  shipment=S-1  shipment.picked      wms-time=09:13:58.9  received=09:14:06.204
observed   wms-evt-88347  shipment=S-1  shipment.dispatched  wms-time=11:40:12.0  received=11:40:15.871

refund     rf-3c0  corr=8c1f  cause=op-18  amount=full order
```

The webhook rows carry no correlation ID from the partner. The storefront added it when it recorded them, by mapping shipment `S-1` to the workflow that had an open hold operation for it.

Each question now has an answer that does not depend on inference:

- **Was it a retry?** No. Two operations, two messages, each delivered once.
- **What did the decision know?** It read `S-1` as allocated, from a webhook received at 09:12:40, and it recorded that as an input. The decision was correct for the facts it had. Whether the store should confirm a cancellation before the WMS confirms the hold is a design question, and a different article: [Your Security Check Passed. Then the World Changed.](security-check-passed-then-world-changed.md)
- **What did the WMS say?** It rejected the `S-1` hold because the shipment was already picked.
- **Why was there a refund?** `rf-3c0` was caused by `op-18` alone. The refund rule fired on one held shipment out of two.
- **Was the partner at fault?** No. `S-1` was never held.

Notice what did *not* settle the ordering question. The WMS clock says the pick happened at 09:13:58.9, and the storefront clock says the decision happened at 09:14:02.140. Those are readings from two clocks owned by two organizations. They suggest an order, but they do not prove one. What settles it is that the decision recorded *what it read*, and what it read was older than the pick. The ordering that matters is the order of facts in your own records, not the order of timestamps across systems.

---

## Record at the Boundary Where It Happens

Each record should be written by the component that knows the fact, at the moment it knows it, in a store that outlives the process. The rule at each handoff is the same: **record what you are about to hand off before you hand it off, and record what came back when it comes back.**

### The storefront: decision, planned effects, and messages in one transaction

The decision, the operations it plans, and the outbox messages that carry them are written together. If the transaction fails, none of them exist. If it commits, a message cannot be lost between the database and the broker, because a relay publishes the outbox rows afterward.

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

    db.Outbox.Add(OutboxMessage.Create(
        messageId: Guid.NewGuid(),            // one identity per message, not per workflow
        type: "fulfillment.hold-shipment",
        body: new HoldShipment(operation.OperationId, shipment.Id),
        headers: new Dictionary<string, string>
        {
            ["correlation-id"] = correlationId,
            ["causation-id"] = decision.DecisionId.ToString(),
            // W3C trace context, if your messaging library does not already add it.
            ["traceparent"] = Activity.Current?.Id ?? string.Empty,
        }));
}

await db.SaveChangesAsync(ct);
await tx.CommitAsync(ct);
```

Three details carry most of the value:

- **The decision record holds its inputs.** `S-1.status=Allocated` and the time the storefront learned it are what answered the incident's hardest question. [Your Audit Log Records the Story, Not the Decision](your-audit-log-is-not-evidence.md) covers decision records, reason codes, and policy versions in more depth.
- **Each shipment gets its own operation and message.** The correlation ID is shared. Nothing else is.
- **The trace context travels in a header beside the correlation ID, not instead of it.** With the W3C ID format, the default since .NET 5, `Activity.Id` is a `traceparent` value. ASP.NET Core and `HttpClient` propagate trace context over HTTP without application code. Messaging libraries vary, so check whether yours already writes it before adding it yourself.

### The worker: delivery, attempt, and outcome

The worker records which message and which delivery it is handling, records the attempt before calling the WMS, and records the outcome, including the reason, when the call returns.

```csharp
public async Task HandleAsync(IncomingMessage<HoldShipment> message, CancellationToken ct)
{
    var hold = message.Body;

    using var scope = logger.BeginScope(new Dictionary<string, object?>
    {
        ["CorrelationId"] = message.Headers.GetValueOrDefault("correlation-id"),
        ["OperationId"] = hold.OperationId,
        ["MessageId"] = message.MessageId,
        ["DeliveryCount"] = message.DeliveryCount,
    });

    var operation = await operations.FindAsync(hold.OperationId, ct);
    if (operation is null || operation.IsFinal)
    {
        // A duplicate or late delivery for an operation that already finished. Acknowledge it.
        logger.LogInformation("Hold operation is {State}; acknowledging delivery", operation?.State);
        return;
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
        // The operation ID, not the attempt or message ID, is the idempotency key.
        result = await wms.HoldAsync(hold.ShipmentId, idempotencyKey: hold.OperationId.ToString(), ct);
    }
    catch (Exception ex) when (WmsErrors.IsAmbiguous(ex))
    {
        result = WmsHoldResult.Unknown("wms.no_response");
    }

    // Writes the attempt's result and the operation's new state in one transaction,
    // using a version check so a concurrent delivery cannot overwrite a final state.
    await operations.RecordAttemptResultAsync(attempt.AttemptId, result, ct);

    logger.LogInformation(
        "Hold attempt {AttemptId} finished {Result} with reason {ReasonCode}",
        attempt.AttemptId, result.Kind, result.ReasonCode);
}
```

The log line is still there. It now names which attempt it was, and its reason code comes from the WMS response body rather than the status code alone. The durable record is the attempt row, and the log line is how an engineer finds it quickly.

A timeout is recorded as `Unknown`, not as a failure. The WMS may have applied the hold before the connection dropped. Re-sending with the same operation ID as the idempotency key is safe only if the WMS documents that it honors that key for this request. If it does not, an unknown outcome needs reconciliation, not a resend. [An Allowed Decision Does Not Mean the Operation Succeeded](allowed-decision-does-not-mean-execution-succeeded.md) covers unknown outcomes and what to do with them.

### The webhook: an observation, mapped to your identities

The partner will not send your correlation ID back, and you should not depend on it doing so. Record each event as an observation in the partner's terms, deduplicate it on the partner's event ID, and attach your identities by mapping the partner's reference to records you already have.

```csharp
// Illustrative record of something another system told us.
public sealed record ExternalObservation(
    string Source,                   // "wms"
    string ExternalEventId,          // the partner's event ID; unique per source
    string ExternalReference,        // the partner's shipment or hold reference
    string EventType,                // "shipment.picked"
    DateTimeOffset ExternalTime,     // the partner's clock: their claim, not our ordering
    DateTimeOffset ReceivedAt,       // our clock: when we learned it
    string? ShipmentId,              // resolved through our mapping, if found
    string? CorrelationId);          // attached by us, never supplied by them
```

Store both timestamps. `ExternalTime` is what the partner says. `ReceivedAt` is when your system could first have acted on it. In the incident, the gap between them was the whole explanation.

Verify the webhook's signature or other sender authentication before recording anything from it. An observation from an unauthenticated sender is a claim from anyone.

---

## Duplicates, Redelivery, and Out-of-Order Arrival

Most brokers deliver at least once. Webhook senders retry. Messages for related operations travel through different queues at different speeds. The records have to stay correct under all three, and a shared correlation ID helps with none of them.

**A duplicate and a second operation look identical by correlation ID.** In the incident, the two "Processing" lines were two operations that the team read as one retry. The opposite mistake is worse. A worker that deduplicates on correlation ID would have dropped the hold for `S-2` as a "duplicate" of `S-1`. Deduplicate on the identity of the thing that can be duplicated:

| What can arrive twice | Deduplicate on |
| --- | --- |
| A broker message | Message ID, and use delivery count to tell a redelivery from a first delivery |
| A request to perform an effect | Operation ID, through state checks and the downstream idempotency key |
| A webhook event | The sender's event ID, scoped to the sender |
| A client resubmission | A client-generated idempotency key, scoped to the caller |

**Arrival order is not occurrence order.** The `shipment.picked` webhook for `S-1` arrived after the decision that it would have changed. A `shipment.dispatched` event can arrive before `shipment.picked` if the sender retries one and not the other. Do not sort events from different systems by timestamp and call the result a history. Use each record's own state and version instead:

- **Make state transitions conditional.** A hold operation moves from `Sending` to `Held`, `Rejected`, or `Unknown`, and from `Unknown` to `Held` or `Rejected`, with a version check. A late or duplicate result cannot move a final state backward.
- **Accept facts that arrive early.** If the WMS reports a hold before the worker has recorded its own result, for example because the worker crashed after the call, record the observation and let reconciliation settle the operation. Do not drop it because the local state "isn't there yet."
- **Keep your own sequence where order matters.** Within one service, a database sequence or a per-operation version orders records reliably. Across services, order comes from causation links and from what each decision recorded that it had read.

[Distributed Acknowledgment and Continuation Workflows](../../advanced/distributed-acknowledgment-and-continuation-workflows.md#11-out-of-order-delivery-is-normal-not-exceptional) treats out-of-order delivery and duplicate delivery for a pause-and-resume workflow in more detail.

---

## Traces Are Navigation, Not Evidence

Distributed tracing is the most natural place to look for a cross-service story, and in the incident it held nothing.

Tracing systems sample. With head sampling, the decision is made at the start of a trace and propagated in the `sampled` flag of the W3C `traceparent` header. The specification describes that flag as meaning the caller *may* have recorded trace data. A trace that was not sampled leaves no spans, although log records written during it may still carry its trace ID, as they did here. A trace that *was* sampled may still be lost to exporter backpressure, collector failure, or a retention window shorter than your audit obligations. None of that is a defect. It is what makes tracing cheap enough to leave on.

So the rule from [AI Governance Observability and End-to-End Decision Tracing](../../ai-integration/ai-governance-observability-and-end-to-end-decision-tracing.md#sampling-and-the-evidence-boundary) applies to any workflow:

```text
Trace missing  ≠  operation did not happen
Trace present  ≠  durable or tamper-evident record
```

Asynchronous hops make traces less complete still. OpenTelemetry's messaging conventions, still marked as in development, use span *links* by default to connect a consumer's processing to the context that created a message, rather than making it a child span. A batch consumer cannot do otherwise, because a span has only one parent. Your tracing backend may show a queue-crossing workflow as several linked traces rather than one tree.

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

**Caller-supplied correlation IDs are input.** If clients may send an `X-Correlation-ID` header, check its length and characters, and generate your own value when it fails. Never let it select a tenant, a user, or a record. [Secure Logging Across Trust Boundaries](../../security/secure-logging-across-trust-boundaries.md#caller-supplied-correlation-values-are-untrusted-input) gives a safe flow. The same applies to an inbound `traceparent` from the public internet. The W3C Trace Context specification describes restarting the trace at the "front gate" into a secure network, or treating authenticated and unauthenticated callers differently, because an untrusted caller can otherwise force sampling or collide with your trace IDs. If you restart, you can still keep the caller's value as a link or an attribute for support purposes.

**Baggage goes everywhere.** W3C Baggage, set through `Activity.AddBaggage` or the OpenTelemetry API, propagates on outbound calls by default, including calls to third parties. The OpenTelemetry documentation warns that it can reach unintended recipients and has no integrity checks. Use it for small, non-sensitive routing hints, never for user data or anything a downstream service might trust as a fact.

**Decide what the partner sees.** Sending your internal operation ID to the WMS as an idempotency key is a deliberate choice: the partner needs a stable key, and an opaque GUID reveals nothing. Sending your correlation ID, customer ID, or decision ID to a third party is a different choice, and usually unnecessary. Keep the mapping on your side.

**A correlation ID is not authority.** A worker must not conclude that a message is allowed, or which tenant it belongs to, from the fact that its correlation ID matches an earlier, authorized request. The correlation ID connects records. It never grants anything.

---

## When Structured Logs Plus a Database Row Are Enough

For most workflows, a correlation ID in every log scope plus an ordinary database row per operation is enough. The row records the operation's state, its decision or reason code, its idempotency key, and the external reference. You do not need event sourcing, a separate audit store, or signed records to answer "what happened to this order?"

That is what the store in the example ended up with: a decision table, a hold-operation table with an attempts child table, an outbox with message IDs, and a table of webhook observations. All four are in the same database the storefront already used. The logs gained four scope fields and a reason code.

Go further only when a specific need appears:

- **Durable decision records with inputs and policy versions**, when someone outside the team may ask why an operation was allowed, or when the facts a decision read will have changed by the time anyone looks.
- **Per-attempt records**, when calls to another system can time out with an unknown outcome or carry money, inventory, or access.
- **External observation records**, when a partner's events change what your system believes or does.
- **A separate, append-only, or tamper-evident store**, only when the threat model includes someone who can edit the records, and you can name who that is. [Your Audit Log Records the Story, Not the Decision](your-audit-log-is-not-evidence.md#failure-4--integrity-ends-at-the-log-sink) lays out that progression.
- **Event sourcing**, when the domain itself benefits from it, not as an audit mechanism. [Event Sourcing, Audit Trails, and Governance Decision Provenance](../../architecture/event-sourcing-audit-trails-and-governance-decision-provenance.md) compares the record models.

A single-service CRUD endpoint with no asynchronous handoff needs none of this. Its database transaction and an ordinary audit column already say what happened.

---

## Testing the Records

Records that nobody asserts on decay quietly. A refactor reuses the correlation ID as the message ID "for simplicity," and nothing fails until the next incident. Test the identities, not only the behavior.

The first test pins down that one workflow fans out into distinct identities with shared grouping and shared causation:

```csharp
[Fact]
public async Task Cancelling_a_two_shipment_order_plans_one_operation_and_message_per_shipment()
{
    var harness = await StorefrontHarness.WithOrderAsync("O-5521", shipments: ["S-1", "S-2"]);

    await harness.CancelAsync("O-5521", correlationId: "8c1f");

    var decision = Assert.Single(harness.Decisions);
    Assert.Equal("Allowed", decision.Outcome);
    Assert.Equal("cancel-policy/4", decision.PolicyVersion);
    Assert.Equal("Allocated", decision.Inputs["S-1.status"]);

    Assert.Equal(2, harness.HoldOperations.Count);
    Assert.All(harness.HoldOperations, op =>
    {
        Assert.Equal("8c1f", op.CorrelationId);
        Assert.Equal(decision.DecisionId, op.DecisionId);
    });
    Assert.Equal(2, harness.HoldOperations.Select(op => op.OperationId).Distinct().Count());

    Assert.Equal(2, harness.Outbox.Select(m => m.MessageId).Distinct().Count());
    Assert.DoesNotContain(harness.Outbox, m => m.MessageId.ToString() == "8c1f");
}
```

The second pins down that a redelivery after an unknown outcome is recorded as another attempt at the same operation, with the same idempotency key, and that a late duplicate does not change a final state:

```csharp
[Fact]
public async Task Redelivery_after_unknown_outcome_reuses_the_operation_identity()
{
    var wms = new ScriptedWms(
        WmsHoldResult.Unknown("wms.no_response"),
        WmsHoldResult.Held("HLD-30944"));
    var harness = await WorkerHarness.WithPlannedHoldAsync("op-18", "S-2", wms);

    await harness.DeliverAsync("m-5d3", deliveryCount: 1);
    await harness.DeliverAsync("m-5d3", deliveryCount: 2);
    await harness.DeliverAsync("m-5d3", deliveryCount: 3);   // late duplicate after success

    Assert.Equal(new[] { "op-18", "op-18" }, wms.IdempotencyKeysReceived);

    var attempts = harness.AttemptsFor("op-18");
    Assert.Equal(new[] { 1, 2 }, attempts.Select(a => a.DeliveryCount));
    Assert.Equal(new[] { "Unknown", "Held" }, attempts.Select(a => a.Result));

    Assert.Equal("Held", harness.Operation("op-18").State);
}
```

A third test is worth adding wherever the system consumes webhooks: deliver the same partner event ID twice and assert that one observation is recorded, then deliver a `dispatched` event before a `picked` event and assert that both are recorded with their external and received times intact.

---

## Failure Modes

**1. Deduplicating on correlation ID.** Every operation in the workflow after the first is discarded as a duplicate. In the example, `S-2` would never have been held.

**2. One identifier for everything.** The correlation ID doubles as the message ID, the idempotency key, or the decision ID. Records about different things become indistinguishable, and a downstream idempotency check suppresses a legitimate second operation.

**3. A new correlation ID at every hop.** Each service generates its own value instead of carrying the incoming one, so the search finds one service's records at a time. Generate once at the edge, carry it, and record a causation ID where one record leads to another.

**4. Ordering by timestamps across systems.** Clocks on different hosts and in different organizations disagree, and arrival order is not occurrence order. Order within a service by sequence or version, and across services by causation and recorded inputs.

**5. Treating the trace as the record.** A missing trace is read as "did not happen," or a present trace is presented as proof. Sampling, export loss, and retention make traces navigation only.

**6. Logging the status code without the reason.** "409 Conflict" read as transient was the hinge of the wrong story. Record the counterpart's reason code with the attempt.

**7. Expecting the partner to echo your correlation ID.** External events are logged under the partner's references and never found. Store the mapping from external reference to your operation, and attach your identities when you record the observation.

**8. Copying payloads into headers, baggage, or every log line.** Identifiers were meant to make that unnecessary. Baggage in particular propagates to third parties.

**9. Using correlation as authority or tenancy.** A worker trusts a message because its correlation ID matches an authorized request, or resolves its tenant from it. Correlation connects records. It never decides.

**10. Recording after the handoff.** A worker that calls the partner and then writes the attempt row loses every attempt that crashes in between. Record the attempt before the call and its result after.

---

## A Short Review Checklist

**Identity**

1. Is the correlation ID generated once at the edge, validated if a caller supplies it, and carried unchanged across every hop?
2. Do decisions, intended effects, messages, attempts, and external events each have their own identifier, minted by whoever creates them?
3. Is nothing deduplicated, keyed, or authorized by correlation ID?
4. Does each record that something else led to carry a causation ID pointing at its cause?

**Records at each boundary**

5. Is the decision recorded with its reason code, policy version, and the inputs it read, including how current those inputs were?
6. Are planned effects and their outbound messages written in the same transaction as the decision?
7. Does the worker record message ID and delivery count, record the attempt before calling out, and record the outcome with the counterpart's reason code afterward?
8. Is a timeout recorded as `Unknown` rather than as a failure?
9. Are external events recorded with the partner's event ID, the partner's time, your received time, and a mapping to your identities?

**Ordering and duplicates**

10. Are state transitions conditional, so late or duplicate results cannot move a final state backward?
11. Is order established by sequence, version, causation, and recorded inputs rather than by timestamps from different hosts?

**Telemetry and leakage**

12. Can every evidence question be answered without a trace existing?
13. Do headers, baggage, and log scopes carry identifiers only, with no credentials, payloads, or sensitive facts?
14. Is an inbound `traceparent` from untrusted callers either restarted or deliberately accepted?

**Proportion and tests**

15. Is the extra recording limited to workflows that cross an asynchronous boundary, call another system, or may be questioned later?
16. Do tests assert that identities are distinct, that redelivery reuses the operation identity, and that duplicate external events are recorded once?

---

## Continue Deeper

For what a single durable record needs, including decision capture, binding to the operation, write ordering, and integrity, read [Your Audit Log Records the Story, Not the Decision](your-audit-log-is-not-evidence.md).

To choose between operational logs, audit history, decision records, and event sourcing as record models, [Event Sourcing, Audit Trails, and Governance Decision Provenance](../../architecture/event-sourcing-audit-trails-and-governance-decision-provenance.md#correlation) compares them and separates correlation from causation and authority.

For trace and span relationships, sampling, and telemetry that must not become authority, [AI Governance Observability and End-to-End Decision Tracing](../../ai-integration/ai-governance-observability-and-end-to-end-decision-tracing.md#keep-identity-layers-distinct) applies the same identity layers to an AI-governed pipeline.

When a decision pauses for a human response and resumes on another host, [Distributed Acknowledgment and Continuation Workflows](../../advanced/distributed-acknowledgment-and-continuation-workflows.md#20-preserve-end-to-end-lineage-without-conflating-ids) shows how to keep end-to-end lineage without conflating identifiers.

### Related Work

- [W3C Trace Context](https://www.w3.org/TR/trace-context/) defines the `traceparent` and `tracestate` headers and the `sampled` flag, and covers privacy and security considerations, including restarting traces at trust boundaries.
- [W3C Baggage](https://www.w3.org/TR/baggage/) defines the `baggage` header. [OpenTelemetry's baggage overview](https://opentelemetry.io/docs/concepts/signals/baggage/) explains why it should not carry sensitive data.
- [OpenTelemetry semantic conventions for messaging spans](https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/) describe creation context and the use of span links between producers and consumers.
- [Distributed tracing concepts](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-concepts), in the .NET documentation, covers `Activity`, W3C ID format, propagation over HTTP, and sampling.
- [Transactional outbox](https://microservices.io/patterns/data/transactional-outbox.html), on microservices.io, describes writing outbound messages in the same transaction as the state change.

---

## The Short Answer

A correlation ID is a search key. It finds the records that carry it, and it says nothing about what they mean, whether any are missing, which caused which, or in what order things happened. Keep it, carry it from the edge, and give everything else its own identity: the decision, each intended effect, each message, each attempt, and each external event. Record each one durably where it happens, with the inputs a decision read and the reason a counterpart gave. Order things by state, version, and causation, not by clocks. Treat traces as a fast way to look around, not as the record. For most workflows, that is a few database rows and some log scope fields, not a new system.
