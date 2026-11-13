---
description: A reference implementation for payment attempts whose outcome may be unknown: schema, checkout endpoint, notifications, outbox, recovery loop, and tests.
---

# Payment Attempt Recovery in ASP.NET Core: An Implementation Reference

**Learning objective:** Implement the recovery rules from [A Payment API Timeout Leaves the Outcome Unknown](../articles/2026/payment-api-timeout-unknown-outcome.md) in an ASP.NET Core application with a relational store: persist each payment attempt before its first send, initiate each attempt at most once and resend it only under the original key, classify every ending without an answer as unknown, deduplicate provider notifications and downstream work, escalate durably, recover stalled attempts within the provider's idempotency window, and test the paths that rarely happen in development.

**Pattern classification:** General learning material

**Difficulty:** Advanced

**Prerequisites:** [A Payment API Timeout Leaves the Outcome Unknown](../articles/2026/payment-api-timeout-unknown-outcome.md), which explains why each rule here exists. Familiarity with ASP.NET Core minimal APIs, `HttpClient`, EF Core or ADO.NET, and PostgreSQL transactions is assumed. [Data-Access Boundaries and Transaction Reasoning with EF Core](data-access-boundaries-and-transaction-reasoning.md) is useful background for the transaction boundaries.

**Sample scope:** The C# targets .NET 8 or later, and the SQL uses PostgreSQL syntax. Statements about `Microsoft.Extensions.Http.Resilience` describe its documented defaults as of November 13, 2026; check the package version you use. The store, the order, and the payment provider are the hypothetical ones from the article, and the provider's endpoints, statuses, error codes, and timings are invented. The repositories, helpers, and tests are illustrative sketches, not a runnable sample, a production-ready payment integration, or payment-processing, financial, or compliance advice.

The article makes the argument: a timeout ends your observation, not the provider's work, so an attempt whose outcome you did not observe is `Unknown`, resolved only by evidence, and resent only under the original key while the provider's contract makes that safe. This page is the implementation that argument implies once real concurrency, crashes, redelivered notifications, and operators are involved. Read the article first; this page does not repeat its reasoning about provider contracts, response classification, or when a resend is safe, and links back where it relies on them.

The design rests on a handful of invariants, each enforced in one place below:

- **Record before sending.** No request leaves for an attempt that is not durably `Started`, with a `send_intent` event, in the database.
- **One live attempt per order.** An order lock and a partial unique index make a double click the same attempt, and a paid order cannot acquire another one.
- **Unknown is never rounded off.** Every expected way a call ends without an answer is classified `Unknown`, and only evidence resolves it.
- **Every writer is conditional.** The endpoint, the notification handler, and the recovery loop change state only from the state and version they read.
- **Effects are recorded once.** Notifications, downstream work, and operator alerts are deduplicated by keys derived from what they are, and written in the same transaction as their cause.
- **Local checks have limits.** Leases and deadline checks reduce overlap and late sends; the provider's idempotency contract, within its window, is what makes a remaining overlap harmless.

## Persisting the Attempt

The attempt, its history, its escalations, and its downstream work live in four tables:

```sql
CREATE TABLE payment_attempts (
    attempt_id          uuid        PRIMARY KEY,
    order_id            text        NOT NULL REFERENCES orders (order_id),
    customer_id         text        NOT NULL,
    attempt_number      int         NOT NULL,
    provider_account    text        NOT NULL,  -- internal ID: provider, account, and environment, e.g. 'cardco:acct_store:live'
    endpoint            text        NOT NULL,  -- absolute URL, including any regional host
    api_version         text        NOT NULL,
    client_reference    text        NOT NULL,         -- 'ORD-5521-a1': sent to the provider with the payment
    idempotency_key     text        NOT NULL,         -- generated here, once, never per send
    request_body        text        NOT NULL,         -- the serialized parameters every send of this attempt uses
    amount_minor        bigint      NOT NULL,         -- what a match must agree with
    currency            text        NOT NULL,
    state               text        NOT NULL,
    provider_status     text        NULL,             -- e.g. processing, requires_customer_action
    next_action         jsonb       NULL,             -- the provider's current customer action, as the page needs it
    next_action_expires_at timestamptz NULL,          -- the provider's expiry for that action, when it gives one
    outcome_reason      text        NULL,
    created_at          timestamptz NOT NULL,
    first_sent_at       timestamptz NULL,             -- conservative start of the provider's key window
    escalation_deadline timestamptz NULL,             -- stamped at Started from the account's pinned configuration
    provider_payment_id text        NULL,             -- recorded by the first evidence that carries it
    recovery_owner      text        NULL,
    lease_token         uuid        NULL,             -- new for every claim; a release must present it
    lease_acquired_at   timestamptz NULL,             -- diagnostics: when the current lease was claimed
    lease_expires_at    timestamptz NULL,
    next_check_at       timestamptz NOT NULL,         -- set on creation; advanced by every unresolved path
    version             int         NOT NULL DEFAULT 0,
    CONSTRAINT ck_payment_attempt_state CHECK (state IN
        ('NotStarted', 'Started', 'Unknown', 'Succeeded', 'Failed', 'NotApplied')),
    CONSTRAINT ck_payment_attempt_number CHECK (attempt_number > 0),
    CONSTRAINT ck_payment_amount CHECK (amount_minor > 0),
    CONSTRAINT ck_payment_version CHECK (version >= 0),
    -- Never sent: NotStarted, or NotApplied because recovery closed it as never-sent. Everything else
    -- crossed the send boundary.
    CONSTRAINT ck_payment_attempt_send_boundary CHECK (
        ((state = 'NotStarted' OR (state = 'NotApplied' AND outcome_reason = 'never-sent'))
            AND first_sent_at IS NULL AND escalation_deadline IS NULL)
        OR (state <> 'NotStarted' AND outcome_reason IS DISTINCT FROM 'never-sent'
            AND first_sent_at IS NOT NULL AND escalation_deadline IS NOT NULL)),
    CONSTRAINT ck_payment_attempt_lease CHECK (
        (recovery_owner IS NULL AND lease_token IS NULL AND lease_acquired_at IS NULL
            AND lease_expires_at IS NULL)
        OR (recovery_owner IS NOT NULL AND lease_token IS NOT NULL AND lease_acquired_at IS NOT NULL
            AND lease_expires_at IS NOT NULL)),
    CONSTRAINT ck_payment_attempt_action_expiry CHECK (next_action_expires_at IS NULL OR next_action IS NOT NULL),
    UNIQUE (order_id, attempt_number),
    UNIQUE (provider_account, client_reference),
    UNIQUE (provider_account, idempotency_key)
);

-- At most one attempt per order that might still take effect.
CREATE UNIQUE INDEX one_live_attempt_per_order
    ON payment_attempts (order_id)
    WHERE state IN ('NotStarted', 'Started', 'Unknown');

CREATE INDEX payment_attempts_due
    ON payment_attempts (next_check_at, created_at, attempt_id)
    WHERE state IN ('NotStarted', 'Started', 'Unknown');

-- One attempt per provider payment, so a lookup or notification by provider payment ID is unambiguous.
CREATE UNIQUE INDEX one_attempt_per_provider_payment
    ON payment_attempts (provider_account, provider_payment_id)
    WHERE provider_payment_id IS NOT NULL;

-- Append-only: every send intent and result, notification, lookup, and state change.
CREATE TABLE payment_attempt_events (
    event_id          bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    attempt_id        uuid        NULL REFERENCES payment_attempts (attempt_id),  -- NULL: a notification no attempt matched
    occurred_at       timestamptz NOT NULL,
    kind              text        NOT NULL,  -- send_intent, send_result, send_abandoned, notification, lookup, transition
    actor             text        NOT NULL,  -- endpoint, notification-handler, recovery-<instance>, operator-<id>
    provider_account  text        NULL,      -- notification events only: the verified account it arrived for
    notification_key  text        NULL,      -- notification events only: event ID or canonical synthesized key
    detail            jsonb       NOT NULL,  -- status, reason, provider IDs, resolved endpoint; never card data or secrets
    CONSTRAINT ck_event_kind CHECK (kind IN
        ('send_intent', 'send_result', 'send_abandoned', 'notification', 'lookup', 'transition')),
    CONSTRAINT ck_notification_event_fields CHECK (
        (kind = 'notification' AND provider_account IS NOT NULL)
        OR (kind <> 'notification' AND provider_account IS NULL AND notification_key IS NULL)),
    CONSTRAINT uq_notification UNIQUE (provider_account, notification_key)
);

CREATE INDEX payment_attempt_events_history ON payment_attempt_events (attempt_id, occurred_at);

-- One row per episode of a distinct reason a person must look; its outbox rows deliver the alerts.
CREATE TABLE payment_escalations (
    escalation_id     bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    escalation_key    text        NOT NULL,  -- e.g. 'attempt:<id>:deadline', 'attempt:<id>:mismatch:<notification key>'
    attempt_id        uuid        NULL REFERENCES payment_attempts (attempt_id),
    reason            text        NOT NULL,
    raised_at         timestamptz NOT NULL,
    acknowledged_at   timestamptz NULL,      -- a person has seen it
    reminder_count    int         NOT NULL DEFAULT 0,
    resolved_at       timestamptz NULL,      -- the condition has ended; the episode is closed
    next_reminder_at  timestamptz NULL,      -- when an open episode is next due an alert; see Reminders
    CONSTRAINT ck_escalation_reminder_count CHECK (reminder_count >= 0),
    CONSTRAINT ck_escalation_resolved_reminder CHECK (resolved_at IS NULL OR next_reminder_at IS NULL),
    -- All timestamps come from the one application clock; drop these two if operational repair must
    -- import older timestamps, and validate in the application instead.
    CONSTRAINT ck_escalation_resolved_after_raised CHECK (resolved_at IS NULL OR resolved_at >= raised_at),
    CONSTRAINT ck_escalation_acknowledged_after_raised CHECK (acknowledged_at IS NULL OR acknowledged_at >= raised_at)
);

-- At most one open episode per reason.
CREATE UNIQUE INDEX one_open_escalation_per_key
    ON payment_escalations (escalation_key)
    WHERE resolved_at IS NULL;

CREATE INDEX payment_escalations_by_attempt ON payment_escalations (attempt_id);

-- Transactional outbox: downstream work and alerts, written in the same transaction as their cause.
CREATE TABLE outbox_messages (
    message_id        bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    dedup_key         text        NOT NULL UNIQUE,  -- identifies the effect or alert, never a fresh UUID
    attempt_id        uuid        NULL REFERENCES payment_attempts (attempt_id),  -- traceability only
    ordering_key      text        NULL,             -- rows with the same key are delivered in message_id order;
                                                    -- 'attempt:<id>' for business effects, NULL for operator alerts
    kind              text        NOT NULL,         -- e.g. order-confirmation, fulfillment-request, operator-alert
    payload           jsonb       NOT NULL,         -- carries dedup_key, so consumers can deduplicate too
    created_at        timestamptz NOT NULL,
    delivery_attempts int         NOT NULL DEFAULT 0,
    next_attempt_at   timestamptz NOT NULL,
    last_error        text        NULL,
    dispatched_at     timestamptz NULL,
    dead_lettered_at  timestamptz NULL,
    CONSTRAINT ck_outbox_delivery_attempts CHECK (delivery_attempts >= 0),
    CONSTRAINT ck_outbox_single_terminal_state CHECK (dispatched_at IS NULL OR dead_lettered_at IS NULL)
);

CREATE INDEX outbox_messages_pending
    ON outbox_messages (next_attempt_at, created_at)
    WHERE dispatched_at IS NULL AND dead_lettered_at IS NULL;

-- Finds each ordering key's earliest unfinished row, including a dead-lettered one that blocks successors.
CREATE INDEX outbox_messages_unfinished_by_order
    ON outbox_messages (ordering_key, message_id)
    WHERE dispatched_at IS NULL AND ordering_key IS NOT NULL;
```

### The Attempt Row

Each column answers a question that recovery will ask:

- **`client_reference`** is your own identifier for this attempt, sent to the provider in whatever field it offers for that purpose, such as a merchant reference or metadata. It is how a lookup or a notification finds its way back to the attempt even when the provider's response, and with it the provider's payment ID, never arrived. In the incident, this is the field whose absence made the first charge unmatchable. It identifies the *attempt*, not the order: once an order has two attempts, a lookup by order ID alone could resolve one with the other's record.
- **`idempotency_key`** is generated when the attempt is created and stored with it. Do not generate the key inside the transport pipeline. A handler that generates or replaces a key on each retry turns repeats into different payments, which is the incident again, and a key generated by the pipeline does not survive separate calls or a process restart in any case. The attempt owns the key; transport code forwards it unchanged. Use the format and uniqueness your provider requires; a random UUID is a common choice. Do not put personal data in it. Providers scope keys, for example to an account, so the uniqueness constraints on both the key and the client reference include the provider account and environment; a database that ever holds test and live data, or several provider accounts, must not let them collide or be confused.
- **`provider_account`** is a globally unique internal identifier that covers the provider, the merchant or connected account, and the environment, so `cardco:acct_store:live` and `cardco:acct_store:test` are different scopes, and so are two providers that use the same account label. Every uniqueness constraint that includes it inherits that scope. In production, it is better as a foreign key to a table of configured provider accounts than as free text, so a spelling variation cannot quietly create a new scope.
- **`request_body`, `endpoint`, `api_version`, and `provider_account`** together are the request. Everything that affects the payment operation stays fixed for the life of the attempt: the parameters, the endpoint and any regional routing, the API version, and the merchant or connected-account context sent in headers. Providers compare a repeat's parameters with the original's, and some scope duplicate detection more narrowly than you might expect; Adyen, for example, documents that keys are stored at company-account level and are not checked for duplication across regional endpoints. Storing the exact serialized body and resending it unchanged is a conservative application rule rather than a universal provider requirement: a provider may compare normalized parameters rather than bytes, but sending the same bytes rules out accidental drift, such as a resend rebuilt after the customer edited the cart or the tax was recalculated. Store a payment-method token, never card details.
- **What is persisted is the payment, not the transport.** `provider_account` identifies which account and credential scope to use; it is not a credential. API keys, authorization headers, and request signatures stay in the application's secret store and are applied at send time, so credentials can rotate without changing the identity of the attempt. For the same reason, keep regenerated transport values, such as signatures and request timestamps, out of `request_body` unless the provider requires them as immutable request parameters. Values in the body that expire, such as short-lived payment tokens or session identifiers, limit how long a resend of the stored body can work, which the [resend conditions](../articles/2026/payment-api-timeout-unknown-outcome.md#when-resending-with-the-original-key-is-safe) take into account.
- **`amount_minor` and `currency`** are what any later evidence must match before it resolves the attempt. The table models purchase payments, so a zero amount is rejected; a zero-value verification or setup flow is a different operation and deserves its own record.
- **`first_sent_at`** is stamped when the attempt moves to `Started`, before the send, so it is never later than the provider's own record of first submission. Providers measure key retention from the first request; this column is how the store knows when its window closes. **`escalation_deadline`** is stamped in the same transaction: `first_sent_at` plus the provider's minimum key retention, minus the safety margin, from the provider-account configuration in force at that moment. Stamping it pins the deadline, so a later configuration change cannot move it for an attempt already in flight, either past the provider's window or into a premature escalation.
- **`recovery_owner`, `lease_expires_at`, and `next_check_at`** let one recovery worker at a time own an attempt and record when it is next due; [the recovery loop](#the-recovery-loop) states the exact predicates and their limits. `next_check_at` is never empty for a live attempt, because a `NULL` would never satisfy `next_check_at <= :now` and recovery would never find it. The endpoint sets it when it inserts a `NotStarted` attempt, to `created_at` plus the abandonment threshold; moves it when the attempt becomes `Started`, to after the call and record budgets plus the provider's normal processing allowance; and `RecordAsync` moves it again for any outcome that leaves the attempt unresolved. Claiming an attempt and scheduling its next check are not state changes, so they do not change `version`. Every claim also sets a fresh `lease_token`, and releasing a lease requires presenting it, so a worker whose lease expired and was reclaimed cannot clear the new owner's lease even if it still runs under the same `recovery_owner` name. After an attempt is resolved it keeps its last `next_check_at` for history; the partial due index excludes final states, so the value no longer schedules anything.
- **`provider_payment_id`** is recorded by the first evidence that carries it, whether a response, a notification, or a lookup, so later lookups and notification matching can use the provider's own ID. Every writer follows the same rule: set it only when it is still `NULL`, check equality when it is already set, and treat a different ID for the same attempt as a contradiction escalation; it never silently replaces the recorded ID. The partial unique index `one_attempt_per_provider_payment` also makes the ID unique across attempts within its account, so a lookup or notification by provider payment ID finds at most one attempt. If recording an ID conflicts with another attempt that already owns it, the write fails; the application keeps both attempts exactly as they are, raises an investigation, and treats the conflict as proof of nothing about either payment, least of all that one of them failed.
- **`next_action` and `next_action_expires_at`** hold the provider's current customer action, such as a 3-D Secure challenge, and its expiry when the provider gives one. The status `GET` returns the stored action only while it is current, which survives a restart, and recovery schedules from the stored expiry rather than a guessed default. Every writer that records a later outcome, or a newer action, replaces or clears both in the same transaction.
- **`version`** makes every state change conditional, so a late writer cannot overwrite a newer result.
- **The named `CHECK` constraints** keep obviously invalid rows out and give clear errors. The one on `state` matters most: a misspelled state, such as `Unkown`, would fall outside the partial unique index and quietly allow a second live attempt. `ck_payment_attempt_send_boundary` states the send boundary both ways. An attempt that was never sent, either still `NotStarted` or closed by recovery as `NotApplied` with reason `never-sent`, has neither `first_sent_at` nor `escalation_deadline`, so it can never carry stale send history and no send timestamps have to be invented to close it. Every attempt that crossed the send boundary has both, so a `NULL` can never make a deadline comparison silently unknown. `ck_payment_attempt_lease` keeps the lease fields together, so there is never an owner without a fencing token or an expiry; `lease_acquired_at` is there for diagnostics, to tell a long but normal pass from repeated lease expiry, a consistently slow worker, or a host clock problem. `ck_payment_attempt_action_expiry` rejects an expiry without an action, while still allowing an action without one, because some providers give none. These constraints protect the attempt row's own columns; they cannot see the events table, so the rule that a `send_intent` is committed only with the transition to `Started`, or by a worker that owns a sendable attempt, is enforced by the repository transactions, and tested there, unless you add a trigger for it.

The order lock in [the checkout endpoint](#the-checkout-endpoint), backed by the partial unique index, is what turns a double click, a second browser tab, or a refreshed checkout page into the *same* attempt instead of a second payment. It is the store's own idempotency, at the order level, and it does not depend on the provider at all. The index is deliberately keyed on the order alone, not on the provider account: moving an order to another account or provider does not permit a concurrent attempt while the original is unresolved, and adding `provider_account` to the index would weaken the design. The index only covers live attempts, though; a `Succeeded` attempt is not in it. Keeping a paid order from acquiring another attempt is the job of the order row.

Storing the absolute endpoint is the simplest way to keep routing fixed in a sketch. A production design may prefer structured routing identity, such as provider, environment, region, account, API version, and operation, resolved to a URL at send time and checked for equivalence before every resend, with the resolved endpoint recorded in each `send_intent` event. Hostnames then can change for legitimate reasons without the attempt silently moving to another region or account.

### Event History

The events table is the history an operator needs when an attempt ends up with a person: every send intent and send result with its time, actor, and resolved endpoint, every notification and lookup, and every transition. A `send_intent` event means the application crossed the point after which transmission may have occurred, not that any bytes reached the provider; the outcome of the send is a separate `send_result` event, and a send deliberately abandoned after its intent was committed gets a `send_abandoned` event. `send_abandoned` proves only that this worker chose not to dispatch after this particular intent. It does not resolve the attempt or establish `NotApplied`, because another worker's send, or an earlier one, may still have taken effect. A `send_intent` with neither stays ambiguous, even when a local error is observed afterwards, unless the implementation can show that the error happened before transmission and that the attempt had no earlier send.

Only notification events carry `provider_account` and `notification_key`. Every other event, including the transition a notification causes, leaves both `NULL`, so it never competes with the notification for the unique constraint.

Append-only is a requirement to enforce, not a property this schema has by itself: in production, revoke update and delete permissions on the table from the application's database role, and define retention, backup, and access-control policies for it. The attempt row is the current state; the events are the evidence for it.

### Notification Deduplication

Notifications are deduplicated by `notification_key` within the verified `provider_account`. Where the provider supplies an event ID that it documents as stable across redeliveries, that is the key; an ID the provider regenerates per delivery deduplicates nothing. An empty or whitespace event ID, or client reference, is treated as missing before any key is built or lookup made; otherwise every notification with an empty ID would share one key and collide.

Where it does not, a synthesized key is safe only if the provider's event semantics establish that the chosen fields identify one logical event. Choose a provider-specific set of stable business identifiers, for example the provider payment ID, the client reference, the event type, the reported status, the amount and currency, and any provider sequence number or event-occurrence time the provider guarantees to repeat unchanged on redelivery. Payment ID, event type, and status alone are not universally sufficient: a payment can legitimately enter the same status more than once, and two such notifications would collide. Exclude every per-delivery value, such as a delivery timestamp, a delivery attempt count, or a signature, which would give a redelivery a new key, and exclude personal data. Compute the key from a canonical representation of the selected fields, never raw serialized JSON: normalize case, whitespace, number formatting, timestamp precision, status aliases, and Unicode, then hash the result and prefix it with an algorithm version, so the same logical notification produces the same key across deployments. The version prefix makes a change of algorithm visible; it does not keep old keys valid, and the single `notification_key` column with one unique constraint can only deduplicate against one version at a time. So an algorithm change is a migration, not a dual check: store the canonical fields in the event's `detail` so that keys can be recomputed, backfill the new key for every notification still inside the provider's redelivery horizon, and switch the handler to the new version only after the backfill completes. If the provider's semantics do not establish that uniqueness, synthesize nothing: record each delivery separately with a `NULL` key, which PostgreSQL treats as distinct, and rely on the attempt's conditional transitions and on outbox keys that identify each effect, described under [Downstream Work](#downstream-work), to prevent duplicate business effects. Deduplication also lasts only as long as its records do. Keep notification events, and outbox rows with their `dedup_key`, for at least the provider's documented redelivery horizon and the system's own replay horizon, or archive the keys in a form that still detects duplicates; deleting them earlier lets a late redelivery or replay count as new. Provider horizons differ, so there is no universal duration. PostgreSQL 15 and later can declare a unique constraint `NULLS NOT DISTINCT`; do not use it here, because it would collapse every unkeyed notification for an account into one row and silently drop the rest.

The `ck_notification_event_fields` constraint requires an account on every notification event, so a keyed row cannot escape the unique constraint through a `NULL` account, and it keeps the notification columns off every other kind of event; `ck_event_kind` keeps the set of kinds that rule depends on under control. The handler records the notification first, inside the same transaction as everything it then does, with `ON CONFLICT (provider_account, notification_key) DO NOTHING`, naming the arbiter so that a future unique constraint cannot change what is silently swallowed. If another transaction has inserted the same key but not yet committed, PostgreSQL makes this insert wait for it: if that transaction commits, this one sees the conflict and performs no effects; if it rolls back, this insert proceeds and handles the notification. For a keyed notification, no row back means another transaction committed the same key, and the handler returns before any transition, alert, or downstream work. An unkeyed notification is always inserted as a distinct delivery and always returns its new event ID; the repository's result type keeps those two cases apart rather than overloading `null`. The handler does not depend on serializable isolation: the key constraint handles duplicate receipt, and the attempt's conditional state and version update handles concurrent writers. A plain insert that violated the constraint would instead abort the PostgreSQL transaction and return an error for a harmless redelivery.

### Downstream Work

Recording a notification once does not make its downstream work happen once by itself, and a payment can be resolved by more than notifications: the endpoint and the recovery loop's lookups resolve attempts too. Downstream work, such as a confirmation email, a fulfillment request, or a refund, is therefore written as rows in `outbox_messages` in the same local transaction as the state transition that causes it, by every writer that can cause it: the endpoint's `RecordAsync`, the notification handler, and the recovery loop's `resolve`. Each row's `dedup_key` identifies the *effect*, not its trigger, and includes the attempt where there is one: `attempt:<id>:order-confirmation` is the same key whether the endpoint, a notification, or a lookup resolved the attempt, so the confirmation is queued once. Inserts use `ON CONFLICT (dedup_key) DO NOTHING`, so a retried cause is a no-op rather than a unique violation, and the payload carries the key so that consumers can deduplicate a redelivered message too.

The outbox guarantees that the intent survives a process crash, provided the database commit is durable. Delivery still needs a functioning dispatcher, a retry policy, and monitoring. Per-attempt ordering is a dispatcher invariant, not something the table provides. Business effects for an attempt carry `ordering_key = 'attempt:<id>'`, and rows with the same ordering key are delivered in `message_id` order. An identity column orders allocation, not commit: two transactions inserting rows for the same ordering key can commit in the opposite order to their IDs. The order is meaningful here because every transition that queues an effect for an attempt holds that attempt's row lock, so those transactions are serialized and their effects are allocated in business order; anything else that writes business effects must serialize the same way. Eligibility comes first: a row is eligible only if no earlier row with the same ordering key remains undispatched, whether that row is pending, currently claimed by another dispatcher, backing off to a later `next_attempt_at`, or dead-lettered; the `outbox_messages_unfinished_by_order` index serves that check. Only then is the row claimed, atomically, for example with `FOR UPDATE SKIP LOCKED`. `SKIP LOCKED` coordinates concurrent claims, but it skips rows other transactions have locked, so a dispatcher that simply orders pending rows and skips locked ones could pick a later message while its predecessor is in flight; skipping a locked predecessor must never make a successor eligible. A refund therefore cannot overtake its confirmation, even when the confirmation is backing off. Rows for different ordering keys proceed independently.

Operator alerts have no ordering key. They still carry `attempt_id` for traceability, but they must never wait behind the attempt's business effects: an alert about a dead-lettered confirmation, a missed deadline, or a mismatch would otherwise be blocked by the very row it reports. A dead-lettered business row keeps blocking later rows with the same ordering key until an operator resolves it, because letting a refund through after a lost confirmation is a business decision, not a dispatcher default; dead-lettering raises an escalation, whose alert is delivered regardless. The dispatcher counts attempts, records the last error, backs off using `next_attempt_at`, and dead-letters a row after a bounded number of failures. Consumers must remain idempotent within their own guarantees and tolerate lifecycle effects arriving late or more than once.

### Escalations

Escalations follow the same pattern. Raising one inserts a `payment_escalations` row with `ON CONFLICT (escalation_key) WHERE resolved_at IS NULL DO NOTHING` and, only if that insert added a row, an `operator-alert` outbox row in the same transaction. The alert's `dedup_key` identifies the episode, not the reusable reason: `escalation:<escalation_id>:initial`, then `escalation:<escalation_id>:reminder:<n>`. A reason key such as `attempt:<id>:deadline` identifies an open condition and can recur after an episode is resolved; an outbox key built from it would collide with the first episode's alert and silently drop the second. The payload carries both the escalation ID and the escalation key. Two workers, two recovery passes, or a redelivered notification therefore produce one alert per open episode, and a crash between deciding to escalate and notifying a person cannot lose the alert.

An episode lasts from `raised_at` until `resolved_at`, when the condition has ended, for example because the attempt was resolved or an operator closed the investigation. Acknowledgement only means a person has seen it. While an episode stays open, [reminders](#reminders) are due at `next_reminder_at`. Every writer that resolves an attempt resolves that attempt's open escalations in the same transaction, so a deadline or resend-not-safe escalation stops reminding once the payment is settled. After an episode is resolved, the same condition recurring opens a new episode and alerts again. Each distinct problem has its own key: `attempt:<id>:deadline` for an expired key window, and, because every notification is its own evidence, `attempt:<id>:mismatch:<notification key>` and `attempt:<id>:contradiction:<notification key>` for a notification whose amount does not match or whose outcome contradicts the record, and `notification:<account>:<notification key>:unmatched` for one no attempt matched. An unkeyed notification has no stable identity to put in those keys, so the handler uses coarser keys for it: `attempt:<id>:mismatch` and `attempt:<id>:contradiction` per attempt, and an unmatched key built from the provider payment ID or client reference when one is present. An unkeyed notification with neither has nothing stable to name it by, so it falls back to one coarse key per account, `notification:<account>:unmatched-unidentified`, never to a per-delivery event ID: every such notification joins the account's one open episode, and the operator works through the stored notifications behind it. Distinct unkeyed notifications about the same attempt then share one open episode, which is acceptable for alerting, since an open episode already asks a person to look; without a stable key, that is the honest limit of deduplication.

When the recovery loop raises an escalation for an attempt, the same transaction first re-reads the attempt with `FOR UPDATE` and raises it only if the attempt is still `Started` or `Unknown` and the worker still owns its lease; a notification that resolved the attempt in the meantime wins, and nobody is alerted about a payment that has already succeeded. Escalation never updates the attempt row, so it does not change `version` and cannot make a concurrent outcome write retry.

---

## The Checkout Endpoint

The checkout endpoint initiates each recorded payment attempt at most once, and it never sends a request it did not itself move from `NotStarted` to `Started`. Recovery may later resend the same logical attempt, with the original idempotency key and unchanged request, only within the provider's documented safety conditions. A separate `GET` endpoint lets the page poll the attempt's status without any chance of starting a payment:

```csharp
app.MapPost("/orders/{orderId}/payment", async (
    string orderId, ClaimsPrincipal user, HttpResponse response, CheckoutPayments payments,
    CancellationToken requestAborted) =>
{
    PaymentStatus status = await payments.PayAsync(orderId, user.GetCustomerId(), requestAborted);

    if (status.State is AttemptState.NotStarted)
    {
        // This endpoint could not begin the recorded attempt, and nothing was sent. The page may retry
        // this POST, a bounded number of times; the delay here is illustrative.
        response.Headers["Retry-After"] = "2";
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable, title: "Payment not started; retry");
    }

    return ToResult(orderId, status);
}).RequireAuthorization();

// Observation only: loads the order's latest attempt for this customer and never sends anything.
app.MapGet("/orders/{orderId}/payment/status", async (
    string orderId, ClaimsPrincipal user, CheckoutPayments payments, CancellationToken ct) =>
    await payments.GetStatusAsync(orderId, user.GetCustomerId(), ct) is { } status
        ? ToResult(orderId, status)
        : Results.NotFound()
).RequireAuthorization();

static IResult ToResult(string orderId, PaymentStatus status) => status switch
{
    { State: AttemptState.Succeeded } => Results.Ok(status),
    { State: AttemptState.Failed or AttemptState.NotApplied } => Results.Ok(status),   // offer another try
    { State: AttemptState.Started, NextAction: { IsCurrent: true } } => Results.Ok(status), // the customer's step
    _ => Results.Accepted($"/orders/{orderId}/payment/status", status),                  // NotStarted, Started, Unknown
};
```

```csharp
public async Task<PaymentStatus> PayAsync(string orderId, string customerId, CancellationToken requestAborted)
{
    // One transaction: lock the order row, refuse an order that is Paid, cancelled, or not this
    // customer's, and return its live attempt if there is one. Otherwise insert attempt N+1 with a new
    // key, the exact request and context, and next_check_at = created_at + abandonment threshold. The
    // order lock serializes cooperating callers; the partial unique index is the backstop for any other
    // writer (see the second detail below).
    PaymentAttempt attempt = await attempts.GetOrCreateLiveAttemptAsync(orderId, customerId, requestAborted);

    if (attempt.State is not AttemptState.NotStarted)
    {
        // Someone already sent it: this click, tab, or browser retry only reports.
        return PaymentStatus.From(attempt);
    }

    // One transaction: conditional NotStarted -> Started on the attempt's version, stamping
    // first_sent_at from the same TimeProvider every deadline uses, appending a send_intent event, and
    // moving next_check_at past the call and record budgets plus the provider's normal processing time.
    // It also stamps escalation_deadline, and re-checks under the order lock that the order is still
    // payable, so a cancellation cannot race the first send. Exactly one caller wins.
    // Contract: false means only a confirmed conditional miss. A database failure or an unconfirmed
    // commit throws; the write may still have committed, so the caller reloads durable state.
    // Once the attempt exists, its own short budget replaces the customer's token, so a closed tab
    // cannot make this transition's outcome ambiguous.
    bool started;
    // Budgets run on the injected TimeProvider, so FakeTimeProvider controls them in tests too.
    using CancellationTokenSource startBudget = new(TimeSpan.FromSeconds(5), clock);
    try
    {
        started = await attempts.TryMarkStartedAsync(attempt.AttemptId, attempt.Version, clock.GetUtcNow(), startBudget.Token);
    }
    catch (Exception ex) when (ex is DbException or DbUpdateException or OperationCanceledException)
    {
        // Unconfirmed: reload. Still NotStarted means the write did not commit and nobody is about to
        // send; the POST endpoint reports a retryable 503, and the page's retry tries this attempt again.
        diagnostics.TryReportStartNotConfirmed(attempt.AttemptId, ex);
        return PaymentStatus.From(await attempts.GetAsync(attempt.AttemptId, CancellationToken.None));
    }

    if (!started)
    {
        // The conditional transition affected no row: another writer moved the attempt, such as a
        // concurrent click that started it or recovery closing it. Reload and report the durable state.
        return PaymentStatus.From(await attempts.GetAsync(attempt.AttemptId, CancellationToken.None));
    }

    // From here on, a closed browser tab must not cancel the call or the record of its outcome.
    using CancellationTokenSource callBudget = new(TimeSpan.FromSeconds(15), clock);
    ProviderOutcome outcome = await provider.CreatePaymentAsync(attempt.ToProviderRequest(), callBudget.Token);

    using CancellationTokenSource recordBudget = new(TimeSpan.FromSeconds(5), clock);
    try
    {
        // One transaction, conditional on the attempt still being Started or Unknown: records the
        // outcome, appends a send_result event, clears any provider status the outcome supersedes,
        // records the provider payment ID if the response carries one, and either resolves the attempt,
        // with the order marked Paid on Succeeded, effect-keyed outbox rows, and open escalations
        // resolved, or moves next_check_at for an outcome that leaves it unresolved. A notification that already resolved the attempt
        // wins, and an Unknown outcome never overwrites a known one. Optimistic-concurrency loss,
        // including EF Core's DbUpdateConcurrencyException, is handled inside RecordAsync: if the winner
        // is Succeeded, Failed, or NotApplied, it returns the winning state; if the winner is still
        // Started or Unknown, it reloads and applies this outcome against the new version, a bounded
        // number of times, so that a Succeeded outcome replaces Unknown and repeated provider-status
        // changes cannot livelock the call. Only a concurrency conflict is retried; any other
        // DbUpdateException, such as a constraint failure, and exhausted retries surface as an
        // unconfirmed write.
        return PaymentStatus.From(await attempts.RecordAsync(attempt.AttemptId, outcome, recordBudget.Token));
    }
    // OperationCanceledException is caught without an IsCancellationRequested filter on purpose: the
    // only token RecordAsync receives is its own record budget, never the caller's abort token, so any
    // cancellation that escapes it leaves the write unconfirmed. Do not narrow this filter.
    catch (Exception ex) when (ex is DbException or DbUpdateException or OperationCanceledException)
    {
        // The write failed, or its commit could not be confirmed. Do not assume it rolled back:
        // it may have committed, and a notification may have resolved the attempt meanwhile.
        // Report what is durable now; the recovery loop establishes the provider's outcome and escalates
        // through the outbox if it cannot. With the database failing, a durable alert could fail too, so
        // this is a best-effort log and metric that never throws past the status return.
        diagnostics.TryReportOutcomeNotRecorded(attempt.AttemptId, outcome, ex);
        return PaymentStatus.From(await attempts.GetAsync(attempt.AttemptId, CancellationToken.None));
    }
}
```

Each step is its own short transaction rather than one long one, because a database transaction must not stay open across a call to the provider. The steps are held together by conditional writes on the attempt's `state` and `version`, and by the lock on the order row, not by an enclosing `TransactionScope`. EF Core has no provider-agnostic LINQ operator for a row lock, so the order lock is a raw SQL `SELECT ... FOR UPDATE` inside an explicit transaction, for example through `FromSql`. The `catch` names the database failures it expects: `DbException` from ADO.NET, and EF Core's `DbUpdateException`, which wraps the provider's exception rather than deriving from `DbException`. A repository that translates persistence failures into its own exception type should catch that instead. Because `RecordAsync` handles an expected concurrency race itself, a `DbUpdateException` that reaches this `catch` is a genuine failure to persist or to confirm persistence, not a lost race. The only token passed to `RecordAsync` is the record budget, so any cancellation from that call, whether the budget expired or the database provider cancelled its own command, is likewise an unconfirmed write. If the reload in the `catch` also fails, the endpoint returns an error, but the attempt is still durable: the page's next status request, or the customer's next click, finds it.

**Lock order is fixed across every writer.** A transaction that touches both the order and an attempt locks the order row first, then the attempt row; rows in `payment_escalations`, `payment_attempt_events`, and `outbox_messages` are written only after the attempt is locked. The endpoint, the notification handler, the recovery loop, the reminder job, and operator tools all follow the same order, which keeps them from deadlocking one another. If PostgreSQL still reports a deadlock, retry the whole local transaction a bounded number of times, and only where replaying that transaction is safe; never retry part of one.

**Cancellation goes through the same lock.** Cancelling an order locks the order row. With no live attempt, the order is cancelled outright. With a live attempt, the cancellation is recorded as an intent on the order. The `Started` transition and every recovery `send_intent` transaction re-check under the order lock that the order is still payable, so no new send intent can be committed after a cancellation has committed. That is as far as a local lock reaches: a cancellation that commits just after a send intent cannot recall the dispatch that intent already authorized. The attempt is then left to resolve through reconciliation, and if it turns out to have succeeded, the refund is its own operation.

Five details carry most of the weight.

**A paid order cannot get another attempt.** The order row is the serialization point. Recording `Succeeded` and marking the order `Paid` happen in one local transaction, and creating an attempt locks the same order row and refuses a paid order. Without that, a checkout request could see "no live attempt" just after the successful attempt left the index and just before the order became `Paid`.

**A race lost to the index is handled, not continued.** Callers that lock the order row never collide on the partial unique index; it exists for writers that do not take the lock, such as an administrative tool. If an insert does violate it, PostgreSQL aborts the transaction, and no ordinary query can run in it afterwards. Roll back and reload the winning attempt in a new transaction, use a savepoint around the insert, or write the insert as `INSERT ... ON CONFLICT (order_id) WHERE state IN ('NotStarted', 'Started', 'Unknown') DO NOTHING` and reload when no row comes back. Keep that conflict predicate textually identical to the index predicate, and cover it with an integration test against the PostgreSQL version the application uses, so a migration cannot change one without the other.

**The send happens only after a confirmed `Started`.** A crash between `Started` and the send leaves an attempt that may or may not have been sent, which recovery treats as `Unknown`. That is safe. The reverse order, send first and record later, leaves a payment with no record at all, which is not. Because every sender in this design sends only after committing `Started`, a crash *before* that transition leaves a `NotStarted` attempt that no cooperating sender could have transmitted; the recovery loop closes it so that the order can be paid again. The proof is only as strong as that invariant, so no other code path may call the provider for an attempt.

**The customer's request token does not cancel the provider call.** Passing `HttpContext.RequestAborted` to the provider call turns every customer who closes the tab during a slow payment into an unknown outcome the store created for itself. The call gets its own budget, and recording the result gets another, so that a cancelled caller cannot also cancel the evidence. The 15-second budget here and the incident's 10-second timeout are both illustrative. A budget also caps everything inside it: if a resilience pipeline is ever added to this client, its 10-second attempt timeout fires first, and a 15-second budget would cut off its 30-second total timeout. Size the budget to cover the whole pipeline, or the pipeline's settings are partly dead configuration.

**The response says what the customer should do next.** While confirmation is pending, the endpoint returns `202 Accepted` with the status URL. The status `GET` returns `202` for an attempt that has not started yet, too, so polling always gets the attempt's state, never a service error. The checkout page shows "We're confirming your payment", polls that `GET` endpoint, and does not show a **Pay** button for the same order. Telling the customer "Payment failed. Please try again." is a statement about the provider that the store cannot make, and in the incident it is what produced the second charge. When the provider asks for customer action, such as a 3-D Secure challenge, the endpoint returns the provider's next action instead, and the page presents it. That branch applies only to a `Started` attempt whose next action is still current; recording any later outcome clears it, and an expired or superseded action is never shown, so an `Unknown` attempt cannot surface a stale challenge. One response is different: if the `POST` could not move a `NotStarted` attempt to `Started`, it returns `503 Service Unavailable` with a `Retry-After` header. That status describes this endpoint's temporary inability to begin the recorded attempt. It says nothing about the payment provider, because no cooperating sender transmitted the request while the attempt was `NotStarted`. The page may retry the `POST` after the indicated delay, a bounded number of times, and then tell the customer to try again later; the `503` is not permission for an unrestricted automatic retry policy, and it provides no idempotency by itself. The order lock and the conditional transition still decide what a retry does.

The payment client is registered without automatic resends, so that only the code above and the recovery loop decide when a payment request goes out:

```csharp
builder.Services
    .AddHttpClient<PaymentProviderClient>(client =>
    {
        // Disables HttpClient.Timeout (100 seconds by default): the per-call budget is the client-side
        // deadline. Every caller, the checkout endpoint and the recovery loop alike, passes one, and the
        // payment client also enforces a maximum call duration, so a caller that forgets cannot wait
        // indefinitely.
        client.Timeout = Timeout.InfiniteTimeSpan;
    })
    // Removes every resilience handler registered for this client before this point, including any
    // that ConfigureHttpClientDefaults adds to all clients. RemoveAllResilienceHandlers comes from
    // Microsoft.Extensions.Http.Resilience; check its status in the package version you use.
    .RemoveAllResilienceHandlers();

builder.Services
    .AddOptions<PaymentProviderOptions>()
    .BindConfiguration("Payments:Provider")
    // A CancellationTokenSource delay that is negative or out of range throws, so fail at startup instead.
    // int.MaxValue milliseconds is a conservative bound that every supported .NET version accepts.
    .Validate(o => o.MaximumCallDuration > TimeSpan.Zero
                   && o.MaximumCallDuration.TotalMilliseconds <= int.MaxValue,
        "Payments:Provider:MaximumCallDuration must be positive and within the supported cancellation range.")
    .ValidateOnStart();
```

The client maps the expected ways the call can end without an answer, a stored request it cannot build, timeouts, cancellation, pipeline rejections, transport failures, incomplete or unreadable responses, to `Unknown`, and generates no new payment identity, idempotency key, or business parameters; it only builds the transport request and applies current credentials:

```csharp
public async Task<ProviderOutcome> CreatePaymentAsync(ProviderRequest stored, CancellationToken ct)
{
    // HttpClient.Timeout is disabled, so the client enforces a ceiling of its own rather than trusting
    // every caller to pass a budget: the caller's token is linked to a maximum call duration, and
    // whichever fires first cancels the call. MaximumCallDuration is validated at startup as positive and
    // within the range a cancellation delay accepts.
    CancellationToken callerToken = ct;
    using CancellationTokenSource maximumDuration = new(options.MaximumCallDuration, clock);   // TimeProvider-backed
    using CancellationTokenSource ceiling =
        CancellationTokenSource.CreateLinkedTokenSource(callerToken, maximumDuration.Token);
    ct = ceiling.Token;

    HttpRequestMessage request;
    try
    {
        // Builds the request from the stored attempt: endpoint, body, Idempotency-Key, the fictional
        // provider's version header, and, through providerContext, the credentials and account header
        // for the stored provider_account. It never falls back to whichever account is the client's
        // current default. A stored value that cannot be applied, such as an account that is no longer
        // configured or an invalid header value, throws StoredRequestException before anything is sent.
        request = requests.Build(stored);
    }
    catch (StoredRequestException ex)
    {
        // Deliberately conservative: still Unknown, not NotApplied, because this call cannot prove the
        // attempt had no earlier send. Recovery escalates it rather than retrying a request it cannot build.
        return ProviderOutcome.Unknown($"request.not-buildable.{ex.Reason}");
    }

    using (request)
    {
        try
        {
            // The default HttpCompletionOption buffers the whole body inside SendAsync, so a response
            // that ends early surfaces here as an HttpRequestException, not later while classifying.
            using HttpResponseMessage response = await http.SendAsync(request, ct);

            // Maps documented statuses and bodies; an empty, malformed, or unrecognized body is Unknown.
            return await ProviderResponses.ClassifyAsync(response, ct);
        }
        catch (OperationCanceledException)
        {
            // This call's budget. (HttpClient.Timeout, which would surface as a TaskCanceledException with an
            // inner TimeoutException, is disabled for this client.)
            // One business outcome, Unknown, with the internal reason kept for telemetry.
            // "caller" covers the caller's budget and any external cancellation through its token.
            string source = callerToken.IsCancellationRequested ? "caller" : "client-ceiling";
            return ProviderOutcome.Unknown($"provider.no-response.{source}");
        }
        catch (TimeoutRejectedException)
        {
            // A Polly timeout strategy, if a pipeline is ever added to this client.
            return ProviderOutcome.Unknown("provider.pipeline-timeout");
        }
        catch (ExecutionRejectedException ex)
        {
            // Other Polly rejections, such as an open circuit breaker or a rate limiter. They refuse this
            // send, but do not prove that the attempt had no earlier send.
            return ProviderOutcome.Unknown($"provider.pipeline-rejected.{ex.GetType().Name}");
        }
        catch (HttpRequestException ex)
        {
            return ProviderOutcome.Unknown($"provider.transport.{ex.HttpRequestError}");
        }
    }
}
```

Every branch says `Unknown`, so the code does not need to tell a client timeout from a cancellation from a reset; each one means the same thing for the payment. The catches are deliberately limited to those expected paths. A programming or configuration error, such as a null reference or an invalid endpoint, is allowed to surface for alerting rather than being disguised as a provider outcome. Because the attempt is already durably `Started` with a `send_intent` event, an unexpected exception never leaves a payment without a record; the recovery loop resolves the attempt, unless a notification already has.

---

## The Notification Handler

The rules for notifications are in the article's section on [notifications](../articles/2026/payment-api-timeout-unknown-outcome.md#the-provider-tells-you-notifications). This handler implements them:

```csharp
// After verifying the notification's signature. The provider account and environment come from that
// verified context, such as which configured endpoint secret validated the signature, never from a
// field in the payload alone; a test-mode event must never resolve a live attempt. Notifications can
// arrive late, more than once, out of order, and before the endpoint has recorded its own outcome.
ProviderAccount account = verified.ProviderAccount;

// The provider's event ID, or, where the provider's semantics allow it, a versioned hash of a canonical
// set of stable fields; otherwise null, and each delivery is recorded separately.
// Empty or whitespace values count as missing.
string? notificationKey = NullIfBlank(notification.EventId) ?? NotificationKeys.TrySynthesize(notification);

await using DbTransaction tx = await db.BeginTransactionAsync(ct);

string? reference = NullIfBlank(notification.Payment.Reference);   // the client_reference sent with the request
string? providerPaymentId = NullIfBlank(notification.Payment.ProviderPaymentId);

// Resolve both identifiers independently. If they identify different attempts, the notification is
// contradictory evidence: it is recorded and escalated below, and applied to neither attempt. A newly
// observed provider payment ID is associated with an attempt only after its other verified attributes
// match and no other attempt owns that ID.
PaymentAttempt? byReference = reference is null
    ? null : await attempts.FindByClientReferenceAsync(account, reference, tx, ct);
PaymentAttempt? byPaymentId = providerPaymentId is null
    ? null : await attempts.FindByProviderPaymentIdAsync(account, providerPaymentId, tx, ct);
bool identitiesConflict = byReference is not null && byPaymentId is not null
    && byReference.AttemptId != byPaymentId.AttemptId;
PaymentAttempt? attempt = identitiesConflict ? null : byReference ?? byPaymentId;

// Record the notification before acting on it, in this transaction. A keyed notification is inserted
// with ON CONFLICT (provider_account, notification_key) DO NOTHING; no row back means another
// transaction committed the same key, so return success before any alert, transition, or outbox row.
// An unkeyed notification is a plain insert: always a distinct delivery, never a duplicate.
NotificationInsertResult recorded =
    await events.TryRecordNotificationAsync(account, notificationKey, attempt?.AttemptId, notification, tx, ct);
if (recorded.IsDuplicate)
{
    return;
}

// What identifies this notification in escalation keys. Without a stable key, coarser per-attempt or
// per-payment keys are the honest limit of deduplication (see Escalations).
string? evidenceKey = notificationKey;

if (identitiesConflict)
{
    // A conflict needs both identifiers, so the provider payment ID is non-null here; say so explicitly
    // rather than risk a key such as "notification:<account>::identity-conflict" after a refactor.
    string conflictIdentity = evidenceKey ?? providerPaymentId
        ?? throw new InvalidOperationException("An identity conflict requires a provider payment ID.");

    // Attached to the reference-matched attempt for navigation; the payload records both attempts,
    // so neither is hidden from the operator. The notification is applied to neither.
    await escalations.RaiseAsync(
        $"notification:{account}:{conflictIdentity}:identity-conflict",
        attemptId: byReference!.AttemptId, reason: "identity-conflict",
        details: new
        {
            ReferenceAttemptId = byReference.AttemptId,
            ProviderPaymentIdAttemptId = byPaymentId!.AttemptId,
            ProviderAccount = account,
            ClientReference = reference,
            ProviderPaymentId = providerPaymentId,
            NotificationEventId = recorded.EventId,
        },
        tx, ct);
}
else if (attempt is null)
{
    // Never silently ignore a payment you cannot place. The notification stays stored for reconciliation.
    // Never a per-delivery value such as the event row's ID: that would open a new episode per delivery.
    string subject = evidenceKey ?? providerPaymentId ?? reference ?? "unmatched-unidentified";
    await escalations.RaiseAsync(subject == "unmatched-unidentified"
            ? $"notification:{account}:unmatched-unidentified"
            : $"notification:{account}:{subject}:unmatched",
        attemptId: null, reason: "unmatched-payment", tx, ct);
}
else if (!attempt.Matches(notification.Payment.Amount, notification.Payment.Currency, account))
{
    await escalations.RaiseAsync(
        evidenceKey is null ? $"attempt:{attempt.AttemptId}:mismatch" : $"attempt:{attempt.AttemptId}:mismatch:{evidenceKey}",
        attempt.AttemptId, reason: "mismatched-payment", tx, ct);
}
else
{
    // Conditional: only Started or Unknown may be resolved, with the order update and outbox rows keyed
    // by the notification. A later lifecycle event, such as a refund, a dispute, or a settlement, is
    // recorded against the resolved payment; it does not contradict it. A genuinely incompatible outcome,
    // such as "failed" for an attempt recorded as Succeeded, raises an escalation keyed by this
    // notification instead of overwriting the record. A provider payment ID different from the one already
    // recorded means this is evidence about a different payment: escalate, and apply nothing from it.
    // Non-final evidence, such as processing or a required customer action, moves an Unknown attempt to
    // Started through the same versioned conditional write, and stores the provider status and any next
    // action with its expiry, so the customer is shown the action. Outbox rows are keyed by attempt and effect, and resolving the attempt
    // resolves its open escalations. If a concurrent writer changes the attempt first, this reloads and
    // reapplies against the new version a bounded number of times while the winner is Started or
    // Unknown, and stops when the winner is final; it does not roll back the notification record and
    // wait for a redelivery that may never come.
    await attempts.ApplyProviderEventAsync(attempt.AttemptId, account, evidenceKey, notification, tx, ct);
}

await tx.CommitAsync(ct);
// Only now may the webhook endpoint return 2xx. Any exception before this point returns an error, so the
// provider redelivers; a notification that failed to record is never acknowledged as handled.
```

Resolving an identity-conflict escalation means determining which attempt, if either, owns the provider record, and then applying the stored notification through the ordinary reconciliation transaction, with its conditional transition, effect-keyed outbox rows, and provider payment ID rules. Closing the alert alone must not alter either attempt.

The handler matches by the account and reference the store generated and sent, the same pair the schema makes unique, not by an ID only the lost response would have carried. Notifications with reliable deduplication keys are recorded once per logical event, so a redelivered keyed notification, matched or not, neither changes state, nor queues work, nor alerts a person again. Notifications without reliable keys are recorded separately on each delivery, while conditional state transitions and effect-keyed outbox rows prevent repeated business effects within their documented guarantees. A payment the handler cannot place, or one whose amount does not match, is an escalation with a durable alert, not a log line. The resolution is conditional, so a notification and the endpoint's own late write cannot overwrite each other, and a contradiction is surfaced rather than decided by whichever write came last.

The webhook endpoint acknowledges a notification only after that transaction commits. A failure anywhere before the commit returns an error status, allowing the provider to retry according to its notification-delivery policy; a duplicate that returns early is a success. A notification that failed to record must never be acknowledged as handled: a success response may cause the provider to regard the delivery as complete, and redelivery cannot be assumed outside the provider's documented notification contract and retry limits.

An unmatched notification is durably consumed and escalated, and a later redelivery with the same key is treated as already handled; it is not matched again. In this design that should be rare, because the attempt and its client reference are persisted before the first send, so an unmatched notification usually points to an integration or data problem. The stored notification remains available for investigation. Once the underlying reference or account problem is corrected, an authorized reconciliation process attaches the stored notification to its attempt and applies its verified outcome without pretending that it was newly delivered, through the same conditional transitions, effect-keyed outbox rows, and escalation resolution as normal handling. A design that expects this often can model notification receipt separately from handling status, so that stored notifications can be retried internally without relying on provider redelivery.

---

## The Recovery Loop

A small background service drives every attempt that has stalled:

```text
definitions (:now = the application's TimeProvider, read fresh for each statement and passed to SQL as a
parameter, never frozen for a whole sweep):

    owns the lease  = recovery_owner = me AND lease_token = my token AND lease_expires_at > :now

    claim           = UPDATE payment_attempts
                      SET recovery_owner = me, lease_token = a new token, lease_acquired_at = :now,
                          lease_expires_at = :now + lease
                      WHERE attempt_id = :id
                        AND state IN ('NotStarted', 'Started', 'Unknown')
                        AND next_check_at <= :now
                        AND (recovery_owner IS NULL OR lease_expires_at <= :now)
                      -- does not change version; it is not a state change

    finish (delay)  = UPDATE payment_attempts
                      SET next_check_at = :now + delay,
                          recovery_owner = NULL, lease_token = NULL, lease_acquired_at = NULL,
                          lease_expires_at = NULL
                      WHERE attempt_id = :id AND recovery_owner = me AND lease_token = my token
                        AND lease_expires_at > :now
                      -- schedules the next check and releases the lease in one conditional write;
                      -- a stale worker cannot clear another worker's lease. If this worker has
                      -- crashed instead, the lease simply expires and the next claim succeeds.

    resolve (outcome) = one transaction, conditional on the state and version read: record the outcome,
                      mark the order Paid on Succeeded, insert effect-keyed outbox rows, resolve the
                      attempt's open escalations, and release the lease only if this worker's owner and
                      lease token still match. A provider outcome stays valid evidence after the
                      reporting worker's lease expires, so the outcome is applied under the state rules
                      alone; the authority to release coordination is not the authority to record evidence

    pause the sweep = on a lookup answered with 429, stop starting lookups in the affected rate-limit
                      scope, such as the provider account and endpoint the provider documents, until its
                      Retry-After or a backoff has passed, and finish this attempt; other providers and
                      accounts continue unless the provider documents the limit as global

    escalate and finish (reason, delay) = in one transaction: re-read the attempt FOR UPDATE; if it is
                      still Started or Unknown and this worker owns the lease, insert payment_escalations
                      'attempt:<id>:<reason>' ON CONFLICT (escalation_key) WHERE resolved_at IS NULL
                      DO NOTHING, and, only if that added a row, an operator-alert outbox row keyed
                      'escalation:<escalation_id>:initial'; then set next_check_at = :now + delay and
                      release the lease. Escalation, alert intent, schedule, and release commit together;
                      the attempt's state and version are not touched

every minute, at a rate the provider's limits allow, for attempts with next_check_at <= :now,
in (next_check_at, created_at, attempt_id) order:

    claim it; if no row changed: next attempt

    if NotStarted for longer than the abandonment threshold (seconds: the longest expected gap
    between inserting an attempt and moving it to Started, not the call or record budgets):
        try the conditional NotStarted → NotApplied transition (reason never-sent), releasing the lease
        if it succeeds: the attempt is final and the lease is released; next attempt
        if it fails, because the endpoint moved it to Started first:
            re-read the attempt; if it is still live and this worker still owns the lease, finish (the
            delay its current state calls for); otherwise next attempt

    if NotStarted:
        the endpoint is about to send it; recovery never sends a first attempt
        finish (the rest of the abandonment threshold); next attempt

    if Started with a customer action whose stored next_action_expires_at is at or before :now:
        the action has expired; treat it as below, never schedule a zero or negative delay

    if Started with a current customer action:
        never resend
        delay = until the earlier of the stored next_action_expires_at, which is after :now, and the
                next status check; without a provider expiry, the integration's documented polling interval
        if :now ≥ the stamped escalation_deadline:
            escalate and finish (deadline, delay); the key window is closing whether or not the
            customer acts
        else:
            finish (delay)
        next attempt

    if Started with an expired or superseded customer action (including the case above):
        stop presenting it; the expiry does not establish that the payment failed, and does not
        permit a resend; reconcile below like any stalled attempt

    if Started, and younger than the endpoint's call and record budgets plus the provider's
    normal processing time:
        finish (until that point); next attempt

    -- the attempt is Unknown, or a stalled Started attempt --

    inspect the latest send history first:
        a send_intent with neither a send_result nor a send_abandoned is an ambiguous send boundary;
        reconcile it through the lookup below, and append no further send_intent until every resend
        condition has been re-established for the attempt as it now stands

    look it up: by provider payment ID if recorded; if that misses or finds a record that does not
    match, by provider account and client reference too, before concluding anything (an ID lookup can
    miss through replication lag, and a recorded ID can be wrong)
        the lookup itself fails (transport error, 5xx):
            not "not found": the attempt stays Unknown; finish (the next backoff step)
            next attempt
        the lookup is answered with 429:
            not "not found"; pause the sweep for that rate-limit scope
            next attempt
        a record whose provider payment ID differs from the one already recorded, or is already
        recorded by another attempt:
            evidence about a different payment: send nothing, apply and resolve nothing;
            escalate and finish (provider-id-conflict, the slower read-only cadence)
            next attempt
        otherwise, evidence carrying a provider payment ID records it if the attempt lacks one
        found, but amount, currency, or account does not match:
            send nothing; escalate and finish (mismatch, the slower read-only cadence)
            next attempt
        found, succeeded, declined, or failed:
            resolve (that outcome)
            next attempt
        found, processing:
            record the observation; an Unknown attempt moves to Started, since the provider has it,
            through a versioned conditional write; finish (the next backoff step)
            next attempt
        found, requires customer action:
            record the current action and its expiry; an Unknown attempt moves to Started, so the
            status GET presents the action; never resend; finish (as for a current customer action)
            next attempt
        not found:
            the attempt stays Unknown; continue below

    if :now ≥ the attempt's stamped escalation_deadline:
        send nothing, under any key; escalate and finish (deadline, the slower read-only cadence)
        next attempt

    if a condition in the article's resend conditions does not hold:
        send nothing; escalate and finish (resend-not-safe:<the failing condition>, such as
        resend-not-safe:token-expired or resend-not-safe:order-not-payable, the slower read-only cadence)
        next attempt

    commit a send_intent event, in one transaction that locks the order then the attempt, and
    requires that this worker owns the lease, the order is still payable, the attempt is still
    sendable, and :now is before the stamped escalation_deadline, the safe cutoff well inside the
    provider's key window
    if the transaction is confirmed not committed (no row changed, or it rolled back):
        send nothing; finish (the next backoff step)
        next attempt
    if its commit outcome is unknown (the acknowledgement was lost):
        send nothing; do not call finish, which depends on ownership this worker cannot confirm
        a later claimed pass re-reads the attempt and its history before any send: if the intent is
        present, the attempt has crossed an ambiguous send boundary and continues through
        reconciliation, never by creating another intent blindly; if it is absent, that pass decides
        afresh
        next attempt

    final local check just before dispatch:
        lease gone:
            commit send_abandoned; send nothing; the new owner schedules the attempt
            next attempt
        escalation deadline gone:
            commit send_abandoned; send nothing;
            escalate and finish (deadline, the slower read-only cadence)
            next attempt

    resend the stored request, unchanged, under its own call budget
    commit a send_result event and classify the response:
        "already processed" with the original result → resolve (that result)
        "already processed" without it               → leave Unknown; look it up next time
        "still in progress"                          → leave it; check again later
        replayed 5xx, timeout, or a no-effect
        rejection of this send only                  → leave Unknown
        a documented replay of the original outcome  → resolve (that outcome)
    if unresolved: finish (the next backoff step)
    next attempt
```

Every branch either resolves the attempt, schedules and releases it through `finish` or `escalate and finish`, or enters one of two deliberate exceptions: a `send_intent` whose commit is unknown, where this worker cannot confirm the ownership `finish` depends on, and a lost lease, where the new owner schedules. In both, the lease expires or has already passed to someone else, so the attempt is still found. The schedule therefore survives restarts, and a completed pass releases its lease instead of making the next due check wait for the lease to expire; a resolved attempt drops out of the `payment_attempts_due` index. Escalating does not stop reconciliation, and a recovery-raised escalation commits with its schedule and lease release, so a pause between them cannot leave the attempt escalated but unscheduled: an escalated attempt keeps being looked up on the slower read-only cadence until it is resolved, at which point its open escalations are resolved too. Every transition the loop makes is conditional on the state and version it read, like the endpoint's and the notification handler's, so the three writers cannot overwrite one another's results. The escalation deadline is checked before any resend, and again in the transaction that commits the send intent; past it, the loop only reads.

The lease must be longer than the worst case of one pass, a lookup plus a send plus recording its result, plus the clock skew between hosts; otherwise a slow but healthy pass loses its lease midway. It should not be much longer, because a crashed worker's attempts wait for its lease to expire before another worker can claim them.

Every deadline, lease, and schedule in this design is computed from one clock: the application's `TimeProvider`, passed into SQL as a parameter rather than mixed with the database's `now()`. The endpoint stamps `first_sent_at` from the same source, and the cancellation budgets and the payment client's maximum call duration are `TimeProvider`-backed `CancellationTokenSource` instances on it too. That keeps production consistent and lets a test's `FakeTimeProvider` move every deadline together. The application hosts must keep their clocks synchronized, and the escalation deadline's margin must cover the skew between them as well as the skew between your hosts and the provider.

The claim is local coordination, and it is weaker than it looks. A lease keeps two healthy recovery workers from working the same attempt at the same time. It cannot stop a worker that pauses after its checks from waking after its lease has expired and sending a request it had already prepared, while another worker now owns the attempt. If both workers send the same stored request under the same key, and the provider receives both within its documented idempotency scope and retention guarantee, the provider's contract makes the overlap harmless. Nothing local can guarantee that "within": a worker can pause past the end of the key window as easily as past the end of its lease. That is why the escalation deadline sits well inside the provider's minimum key window, why it is re-checked immediately before dispatch, why the loop never sends anything that is not the attempt's stored request, and why the residual risk belongs in the design's stated limits rather than being assumed away.

The timings are policy, not physics: checks on a backoff such as 1, 2, 5, and 15 minutes, and an escalation deadline well inside the provider's minimum key window, are a starting point to tune against the provider's documented processing times and limits.

The loop never creates an attempt. A new attempt for the order starts only from the checkout endpoint, and only after every earlier attempt is `Failed` or `NotApplied`.

### Reminders

A separate scheduled job, or a step in the same service, drives open escalations:

- **Raising** an episode sets `next_reminder_at` to the time by which someone should have acknowledged it, and `reminder_count` to zero.
- **Reminding** is one conditional transaction per episode: `UPDATE payment_escalations SET reminder_count = reminder_count + 1, next_reminder_at = :now + interval WHERE escalation_id = :id AND resolved_at IS NULL AND next_reminder_at = :read_next_reminder_at RETURNING reminder_count`. Only the job instance whose update changes the row inserts the `operator-alert` outbox row keyed `escalation:<escalation_id>:reminder:<reminder_count>`, with `ON CONFLICT (dedup_key) DO NOTHING`. Two instances racing the same episode therefore send one reminder.
- **Acknowledging** is the operator's transaction: set `acknowledged_at`, conditional on the episode still being open, and reset `next_reminder_at` to the team's reminder interval for acknowledged episodes.
- **Resolving** sets `resolved_at` and clears `next_reminder_at`, in the same transaction as the attempt's resolution when that is what ended the condition.

---

## Test Matrix

The article shows the central test: a fake provider takes a payment and drops the response, and the store must record `Unknown`, refuse a second payment, and let recovery find the first. `TestCheckout` there is a wrapper that supplies a test customer and cancellation token to `PayAsync` and runs the status `GET` and the recovery loop on demand. The full matrix for the implementation in this reference covers:

- **A payment with a lost response** is recorded as `Unknown`, never `Failed`, and the endpoint returns `202` with the status URL.
- **A second click, a second tab, or a browser retry** while the attempt is live returns its status and sends nothing; the status `GET` never sends.
- **Success recorded at the same moment as a new checkout request** leaves the order `Paid` with exactly one attempt that took effect.
- **An insert that violates the partial unique index** from a writer that skipped the order lock returns the winning attempt without querying inside an aborted transaction.
- **Recovery that finds the payment** resolves it to `Succeeded` with one payment in total.
- **A request that never arrived**: the lookup returns "not found", the attempt *stays* `Unknown`, and a same-key resend inside the window produces exactly one payment. A variant in which the original request arrives late, after the lookup, must also produce exactly one payment.
- **Every resend** carries the same key, endpoint, API version, account context, and byte-identical body as the first send, captured by the fake. This is the sample's conservative invariant, stricter than a provider that compares parameters.
- **A changed endpoint, region, or account between sends**, or a stored account that is no longer configured, is refused before anything is sent.
- **A resend after the minimum key window**, using `FakeTimeProvider` to move past it, does not happen; the attempt is escalated.
- **A recovery worker that pauses before its final dispatch check** and resumes after the escalation deadline records `send_abandoned` and does not send.
- **A recovery worker that pauses after its final check** documents the residual limit: local checks cannot stop that send. Assert only what the design guarantees, for example that the send is recorded and later reconciled, or, if the provider enforces a request expiry, that the provider rejects the late request.
- **A recovery worker whose lease expires while its send is in flight** does not cause a second payment when another worker resends inside the window.
- **An attempt past its escalation deadline** is never resent, but is still resolved by a later lookup or notification.
- **A replayed `500`, or a same-key resend rejected before execution,** leaves an attempt whose first send timed out `Unknown`.
- **A documented validation rejection of an attempt's only send** records `NotApplied` with the error, not a customer decline.
- **A `TimeoutRejectedException`, an open-circuit rejection, a malformed body, and an empty body** each produce `Unknown`.
- **A response requiring customer action** returns the provider's next action to the page, and the recovery loop never resends it; an expired action is not shown, and an `Unknown` attempt never surfaces one.
- **A crash after inserting `NotStarted`** and before the first send leaves an attempt the recovery loop closes as `NotApplied` after the abandonment threshold, after which a new checkout succeeds.
- **A crash between the `send_intent` commit and the send** leaves an attempt that recovery resolves as `Unknown`, and the history shows an intent with no result rather than a send that did not happen.
- **A failed or unconfirmed `send_intent` write** results in no request to the provider in that cycle, and the write is treated as possibly committed: the state is re-read before any later send.
- **A recording failure or lost commit acknowledgement after the provider responds**, including an EF Core `DbUpdateException`, returns the attempt's current durable status when the reload succeeds, and recovery reaches the same result. A concurrency conflict with a notification returns the notification's result without an alert.
- **A stored request that cannot be built**, such as an account no longer configured, records `Unknown` with its reason and is escalated without a resend.
- **A notification that arrives before the endpoint records its timeout** wins: the late `Unknown` write does not overwrite `Succeeded`.
- **A notification with an unknown reference, a mismatched amount, or the wrong account or environment** raises an alert and resolves nothing; a later refund notification for a `Succeeded` attempt is recorded without being treated as a contradiction.
- **The same client reference in two provider accounts**, such as test and live, does not collide, and a notification or lookup for one account never resolves the other's attempt.
- **A redelivered notification** with the same provider event ID for the same account is recorded once, returns success rather than a constraint error, and queues no downstream work a second time; the same event ID under another account is recorded separately.
- **A notification without an event ID**, delivered twice, produces the same synthesized key from its canonical fields, despite different delivery timestamps and signatures: one event row, one transition, and no second outbox row. Two distinct notifications that report the same status for the same payment do not collide.
- **A notification whose provider gives no basis for a synthesized key**, delivered twice, is recorded twice, changes the attempt once, and queues its downstream work once through effect-keyed outbox rows.
- **The same effect reached by different routes**, such as a lookup and then a notification resolving the same attempt, queues one confirmation.
- **Two concurrent deliveries of the same notification** produce one handling: the second waits on the first's uncommitted insert and returns success after it commits.
- **A failure before the notification transaction commits** returns an error status, and the provider's redelivery is handled normally.
- **A stored unmatched notification**, after its reference problem is corrected, is applied by reconciliation through the normal conditional transition and resolves its escalation.
- **A redelivered unmatched or mismatched notification** alerts a person once, while two distinct mismatched notifications for one attempt alert separately.
- **A notification event with a key but no account** is rejected by the database.
- **A `NotStarted` attempt whose `Started` transition failed** makes the `POST` return `503` with `Retry-After`, while the status `GET` returns `202` with the `NotStarted` state; the page's bounded retry sends it exactly once, and stops after its limit if the transition keeps failing.
- **An outcome write that loses a race to a `Started` or `Unknown` winner**, such as a provider-status update or recovery marking the attempt `Unknown`, is reapplied against the new version, so a `Succeeded` outcome replaces `Unknown`; one that loses to `Succeeded`, `Failed`, or `NotApplied` returns that outcome; continuous status churn exhausts the bounded retries and takes the unconfirmed-write path.
- **An attempt that meets its escalation condition on several recovery passes, or in two workers at once,** alerts a person once for that open episode; a different later reason alerts again, an acknowledged but open episode sends reminders on its interval, and the same condition recurring after resolution opens a new episode.
- **An outbox row that can never be delivered** is dead-lettered after its retry limit and escalated, and does not delay other attempts' rows.
- **A young `NotStarted` attempt** that recovery picks up is rescheduled, never sent by recovery.
- **A recovery send whose final local check fails after its intent committed** records `send_abandoned` and sends nothing.
- **A `send_intent` commit attempted after the escalation deadline** is refused, with `FakeTimeProvider` moving the endpoint's `first_sent_at`, the deadline, and the recovery loop's `:now` together.
- **A crash right after inserting `NotStarted`, or right after committing `Started`,** leaves an attempt with a `next_check_at` that recovery finds on time.
- **A recovery pass that finishes** releases its lease, so the next due check can claim the attempt without waiting for the lease to expire; a stale worker's `finish` cannot clear another worker's lease.
- **An attempt waiting for customer action** is rescheduled to the action's expiry or the next status check, and an escalated attempt keeps being looked up on the slower cadence.
- **Resolving an attempt** resolves its open escalations, so no reminder follows; an open episode sends reminders from `next_reminder_at`, starting before acknowledgement.
- **An unkeyed notification** is always recorded and handled, never mistaken for a duplicate; an empty event ID is treated as missing.
- **A refund effect queued after a confirmation for the same attempt** is not delivered before it.
- **A failure to report an unrecorded outcome** does not prevent the endpoint from returning the attempt's durable status.
- **A failed provider lookup** (transport error, `429`, or `5xx`) leaves the attempt `Unknown`, is not counted as "not found", and reschedules on backoff.
- **A `send_intent` commit confirmed not to have happened** reschedules; **one whose acknowledgement is lost** sends nothing and leaves the next claimed pass to find the intent present or absent and act accordingly; both cases are tested with the intent present and absent.
- **A recovery-raised escalation** commits with its schedule and lease release; a crash after it leaves no attempt escalated but unscheduled.
- **A resolved escalation that recurs** opens a new episode whose alert is delivered, not suppressed by the first episode's outbox key.
- **Two reminder job instances** racing one episode send one reminder, and acknowledgement resets the reminder schedule.
- **A confirmation backing off to a later `next_attempt_at`** still blocks the same attempt's refund, while other attempts' rows are delivered; a dead-lettered confirmation keeps blocking until an operator resolves it.
- **A notification with no or a wrong client reference** but a recorded provider payment ID matches its attempt; one carrying a different provider payment ID raises a contradiction escalation and replaces nothing.
- **A concurrent writer changing the attempt** while a notification is applied makes the notification reapply against the new version, or stop at a final winner, without rolling back its record.
- **A provider-account configuration change** does not move the stamped `escalation_deadline` of an attempt already `Started`.
- **A stored customer action** is returned by the status `GET` after an application restart and drives recovery's schedule through its stored expiry.
- **A `TryMarkStartedAsync` whose commit is unconfirmed** returns the attempt's durable status rather than an error.
- **A caller that passes no budget** is still cut off by the payment client's maximum call duration.
- **Two different failed resend conditions**, such as an expired token and a later order cancellation, open separate escalations.
- **The `ON CONFLICT` predicate for the live-attempt insert** matches the partial index on the PostgreSQL version in use.
- **An operator alert for an attempt whose confirmation is dead-lettered or backing off** is delivered without waiting behind it.
- **A dispatcher that finds an attempt's earliest row locked by another dispatcher** does not deliver that attempt's later row.
- **A stale `NotStarted` attempt the endpoint starts just as recovery tries to close it** is rescheduled for its new state, not closed and not double-finished.
- **A customer closing the tab during the `Started` transition** leaves that transition confirmed, because it runs under its own budget.
- **An order cancellation committed before the `Started` transition or a recovery `send_intent`** prevents that send; **one committed just after the intent** does not stop that dispatch, and the attempt resolves through reconciliation, with a refund as a separate operation if it succeeded.
- **An unkeyed unmatched notification with no payment ID or reference**, delivered twice, raises one alert for the account's open episode.
- **A lookup by recorded provider payment ID that misses** falls back to the client reference before the attempt is treated as not found.
- **A record carrying a different provider payment ID**, or one already owned by another attempt, resolves nothing and escalates.
- **An `Unknown` attempt found to require customer action** moves to `Started` and shows the action to the customer.
- **A `429` from a lookup** pauses new lookups within the provider's documented rate-limit scope, not merely for the attempt being checked; unrelated scopes continue unless the provider documents the limit as global.
- **A worker applying a valid provider outcome after its lease was reclaimed** records the outcome but cannot release the new owner's lease.
- **A synthesized-key algorithm change** is applied only after stored keys inside the redelivery horizon are backfilled.
- **A `MaximumCallDuration` that is zero, negative, or out of range** fails at startup.
- **A notification whose client reference and provider payment ID identify different attempts** is recorded, raises an identity-conflict escalation, and is applied to neither.
- **A recovery pass that finds a `send_intent` without a result** reconciles it before any new intent, and appends one only once every resend condition holds again.
- **An attempt waiting for customer action past its escalation deadline** escalates and schedules in one transaction, releasing its lease once.
- **A `429` in one provider account** pauses lookups for that rate-limit scope only.
- **A `NotStarted` row with a `first_sent_at` or `escalation_deadline`, a lease missing its token or acquisition time, an action expiry without an action, or a resolved escalation with a reminder scheduled** is rejected by the database.
- **The repository** cannot commit a `send_intent` for an attempt without the transition to `Started` or a recovery owner's lease on a sendable attempt; this is a repository-level test, since no table constraint spans the two tables.
- **Advancing `FakeTimeProvider`** fires the start, call, and record budgets and the client's maximum call duration.
- **An identity-conflict escalation** records both attempt IDs and the notification's identifiers.
- **Recovery closing a stale `NotStarted` attempt as `NotApplied` (`never-sent`)** commits with both send timestamps empty, and any other `NotApplied` without them is rejected.
- **Closing an identity-conflict escalation** without reconciliation changes neither attempt.
- **A current-action branch reached with an already expired stored action** reconciles instead of scheduling a non-positive delay.
- **A constraint failure while recording an outcome** is not retried as a concurrency conflict; it takes the unconfirmed-write path.
- **A notification that resolves an attempt while recovery is about to escalate it** wins: no escalation is raised for the resolved attempt.
- **A crash after an escalation commits but before the alert is sent** still alerts a person, from the outbox.
- **A state value outside the allowed set** is rejected by the database.
- **A closed browser tab** during the call does not cancel the provider call or the recording of its outcome.

---

## Implementation Review Checklist

1. Is every payment attempt written durably, with its client reference, idempotency key, request body, endpoint, API version, and account context, before the first send, with credentials and regenerated transport values kept out of it?
2. Is the key generated once per attempt, are the key and the client reference both unique per provider account and environment, and is the key never generated by the HTTP pipeline or per send?
3. Is the client reference sent to the provider in a field that lookups and notifications return?
4. Does the code send only after a confirmed `NotStarted` to `Started` transition that also commits a `send_intent` event, does a lock on the order row keep a paid order from getting another attempt, and does a partial unique index, with conflicts handled outside the aborted transaction, back that up?
5. Are timeouts, including `TimeoutRejectedException` and other Polly rejections, cancellation, transport errors, unbuildable stored requests, incomplete or unreadable bodies, and undocumented statuses classified as `Unknown`, while unexpected programming errors surface for alerting, and does a failed outcome write, including EF Core's `DbUpdateException`, return the attempt's durable status?
6. Does the checkout page poll a status `GET` that never sends, show "confirming" and suppress **Pay** while an attempt is `Started` or `Unknown`, and present the provider's next action only while it is current?
7. Is the provider call's cancellation independent of the customer's request?
8. Do notifications and lookups find attempts by provider account and client reference, with the account taken from the verified notification context, are redelivered notifications deduplicated by verified account and event ID, with a canonical, versioned synthesized key only where the provider's semantics support one, an early return before any alert or downstream work on a redelivery, outbox rows keyed by attempt and effect, acknowledgement only after commit, and a reconciliation path for stored unmatched notifications, and do all writers resolve attempts only through conditional writes, with genuine contradictions alerted and later lifecycle events recorded separately?
9. Does a lookup verify amount, currency, and account before it resolves an attempt, and does "not found" leave the attempt `Unknown`?
10. Are same-key resends limited to endpoints whose provider documents them as safe, inside the provider's minimum key window measured from `first_sent_at` and re-checked immediately before dispatch, with the stored request and context, while the order is still payable and the payment method still usable?
11. Is the payment client free of automatic resends, or limited to the same stored key and documented no-effect responses?
12. Is every deadline, lease, and schedule computed from one clock passed into SQL, does every live attempt always have a `next_check_at`, set at creation and advanced by every unresolved path, and does the recovery loop claim only due, unresolved attempts whose lease is free or expired, treat ownership as owner and an unexpired lease, release its lease when it finishes a pass, leave young `NotStarted` attempts to the endpoint, close stale ones after a short abandonment threshold, commit each send intent only before the escalation deadline, never resend an attempt waiting for the customer, check the escalation deadline before any resend, escalate each attempt once per reason, only while it is unresolved and owned, with the alert delivered through the outbox, and keep investigating, without sending, once the deadline has passed?
13. Is every send intent, send result, notification, lookup, and transition recorded in a history whose append-only rule the database enforces, with intent kept distinct from confirmed transmission?
14. Do fulfillment, inventory, cancellation, and support tools all treat `Unknown` as "not yet known", and is any pay-another-way policy designed as its own architecture rather than a bypass of the one-live-attempt rule?
15. Do tests cover a lost response, a double click, a late original, a resend after the window, a worker paused past the window, a replayed `500`, a resilience timeout, a stranded `NotStarted`, a lost outcome write, and an early notification?
16. Is the escalation deadline stamped on the attempt at `Started`, is the customer's next action stored with its expiry, and does every writer set the provider payment ID only when empty and escalate a conflicting one?
17. Are operator alerts keyed by escalation episode rather than by reason, do reminders use a conditional claim and a stored count, and does an acknowledgement have its own transaction?
18. Does the outbox dispatcher enforce per-attempt order by claiming only each attempt's earliest unfinished row, and is the effect of a dead-lettered row on later rows a stated policy?
19. Does the recovery loop distinguish a failed lookup from "not found", and a send intent that certainly did not commit from one whose commit is unknown?
20. Is the provider payment ID unique within its account, does a conflict stop resolution, and does recovery fall back from a missed ID lookup to the client reference?
21. Do operator alerts bypass per-attempt outbox ordering, and does eligibility treat a locked, backing-off, or dead-lettered predecessor as blocking?
22. Is there one lock order, order then attempt then escalation, event, and outbox rows, across every writer, with cancellation re-checked under the order lock before any send intent is committed, and a cancellation that arrives after one left to reconciliation and a separate refund?
23. Are notification events and outbox keys retained for at least the provider's redelivery horizon and the system's replay horizon?

---

## Related Learning

- [A Payment API Timeout Leaves the Outcome Unknown](../articles/2026/payment-api-timeout-unknown-outcome.md) is the argument this page implements: what a timeout tells you, the attempt states, response classification, and the conditions for a same-key resend.
- [Delayed Financial Payout and Ambiguous External Commit](../case-studies/delayed-financial-payout-and-ambiguous-external-commit.md) applies the same recovery discipline to a delayed payout under short-lived execution authority, with a provider-enforced request expiry.
- [Data-Access Boundaries and Transaction Reasoning with EF Core](data-access-boundaries-and-transaction-reasoning.md) explains why a local transaction cannot include an external side effect, and where outbox and inbox patterns fit.
- [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md#outbox-and-inbox-patterns) describes the outbox and inbox patterns as explicit state transitions with their own duplicate semantics.
