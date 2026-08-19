# Replay Protection and Bounded-Use Authority

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** [Trust Boundaries and Least Privilege](trust-boundaries-and-least-privilege.md) and [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md). Familiarity with the [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) is helpful but not required.

**Learning objective:** Treat replay protection as a state-management and execution-boundary problem. Distinguish static capability validation from stateful consumption, design one-time or bounded-use authority with atomic host-owned state, reason about multi-instance and restart behavior, and explain why replay resistance is different from request idempotency and exactly-once execution.

## Pattern Card

> **Problem:** A capability, grant, request, or message can remain structurally valid even after the authority it represents should no longer be accepted. If the host does not remember prior use, a captured or duplicated operation may reach the execution boundary again.
>
> **Pattern:** Give replay-sensitive authority a stable identity and explicit use policy, validate its static bindings, then perform an **atomic state transition** at the execution boundary that determines whether another use is still allowed.
>
> **Use when:** Reusing the same authority could repeat a consequential side effect, cross a trust boundary again, exceed a bounded-use policy, or violate a one-time workflow.
>
> **Prefer something simpler when:** The operation is read-only or naturally safe to repeat, no reusable authority exists, and ordinary authorization plus current-state validation fully covers the risk.
>
> **Observe:** Two racing consumers cannot both consume the same final permitted use, process restart does not erase production replay state, and a rejected replay never reaches the protected executor.

The central principle is:

> **Replay protection is stateful. A token cannot prove by itself that it has never been used before.**

A signature can help prove that an artifact has not been altered.

An expiration can limit how long an artifact remains acceptable.

Scope can limit what the artifact authorizes.

None of those facts answer:

> **Has this authority already been consumed as many times as policy permits?**

That question requires state somewhere the execution boundary trusts.

---

## Replay Is a Security and Distributed-Systems Concern

Replay is often introduced as an attack in which a valid request or credential is captured and submitted again.

That is important, but the architectural problem is broader.

Duplicate execution can also arise without an attacker:

- A client times out and retries.
- A queue redelivers a message.
- A load balancer sends retries to another instance.
- A worker crashes after a remote call but before recording completion.
- Two application instances receive the same capability at nearly the same time.
- A user double-submits an operation.
- A network response is lost even though the remote side effect succeeded.
- A process restarts and forgets in-memory use state.

The system therefore needs to distinguish two questions:

```text
Is this artifact valid?
        ↓
Static / contextual validation
```

and:

```text
May this authority be consumed again?
        ↓
Stateful replay / use control
```

A secure implementation may need both.

## What Makes an Operation Replay-Sensitive?

An operation is replay-sensitive when repeating an otherwise valid attempt can create an unacceptable result.

Examples include:

```text
Transfer funds
Disable account
Send external notification
Create deployment
Issue credential
Submit legal acknowledgment
Approve release
Rotate key
Delete resource
Trigger physical action
```

The exact risk depends on the operation.

A repeated `disable account` request may be operationally harmless if disabling an already-disabled account is idempotent.

A repeated `send payment` request may create a second transfer.

A repeated `issue credential` request may create additional authority even if the underlying account state does not change.

A repeated AI tool proposal may also be harmless until a host turns it into execution authority.

The useful question is not merely:

> Can this request arrive twice?

Assume that it can.

Ask instead:

> **What must remain true if it arrives twice?**

## Replay Attack Versus Accidental Duplicate

The control should not depend on knowing why the duplicate appeared.

```text
Captured valid capability
        ↓
Attacker replays it
```

and:

```text
Valid capability
        ↓
Client retries after timeout
```

may look identical at the execution boundary.

Both may present:

- The same capability identity.
- The same actor.
- The same resource.
- The same operation.
- A still-valid signature.
- A still-valid expiration.
- The same policy binding.

Replay protection should therefore be defined in terms of **authority-use semantics**, not attacker detection.

---

## Static Validation Does Not Consume Authority

Consider a capability with:

```text
CapabilityId: cap-123
Actor: admin-42
Operation: account.disable
Resource: user-123
Audience: account-admin-gateway
Expires: 2026-08-19T15:10:00Z
MaximumUses: 1
```

A static validator can confirm:

```text
Correct issuer
Correct audience
Correct actor
Correct operation
Correct resource
Required scope present
Not expired
Proof valid
```

Those checks can all pass twice.

If `MaximumUses = 1`, the second attempt must still be rejected.

That means the execution boundary needs a separate state transition:

```text
Static validation succeeds
        ↓
Atomic use consumption
        ↓
Accepted or already consumed
```

The distinction is:

```text
Validation
=
Does the artifact satisfy the current validation rules?
```

versus:

```text
Consumption
=
Does the host still permit another use, and can this attempt claim that use?
```

Do not compress those questions into one undocumented boolean.

## One-Time and Bounded-Use Grants

A one-time grant is a special case of bounded use:

```text
MaximumUses = 1
```

A bounded-use grant may allow:

```text
MaximumUses = 3
```

For example, a capability could permit three controlled attempts against the same operation and resource.

The required state then changes from:

```text
Seen / not seen
```

to:

```text
Observed use count
Maximum use count
Stopped / cancelled state when relevant
```

A useful abstract state model is:

```text
Grant created
   ↓
Remaining uses = N
   ↓
TryConsume
   ├── remaining > 0 → decrement / record use → accepted
   └── remaining = 0 → rejected
```

The state transition must be defined by the host.

## Capability Identity Must Be Stable

Replay state needs a stable key.

For capability-oriented systems that key is often a grant or token identifier such as:

```text
CapabilityId
TokenId
GrantId
Nonce
JTI-like identifier
```

The name matters less than the semantics.

A useful replay identity should be:

- Stable for the lifetime of the authority.
- Unique enough within the relevant authority domain.
- Bound to the artifact or workflow whose use is being controlled.
- Safe to retain as replay state.
- Not confused with a human-readable correlation identifier.

A correlation ID answers:

> Which events belong to the same operational flow?

A capability ID answers:

> Which authority artifact is this use attempt consuming?

Those identifiers may be related, but they are not interchangeable.

## A Nonce Is Only Useful If Its Semantics Are Enforced

A nonce can help create uniqueness.

But this alone:

```text
nonce = 8f1a...
```

does not prevent replay.

The host must still know whether that nonce has already been accepted.

Conceptually:

```text
Nonce present
        ↓
Lookup / atomic consume
        ↓
Fresh or already used
```

If the application merely checks that a nonce field exists, replay remains possible.

If the application stores nonces only in process memory, restart can erase the guarantee.

If each application instance has a separate nonce set, another instance may accept the same nonce.

The nonce is an identity input.

The replay guarantee comes from the **state transition and its persistence/concurrency semantics**.

---

## The Check-Then-Act Race

A common unsafe design is:

```csharp
if (!useStore.HasBeenUsed(capabilityId))
{
    await executor.ExecuteAsync(
        cancellationToken);

    await useStore.MarkUsedAsync(
        capabilityId,
        cancellationToken);
}
```

This looks reasonable in a single sequential trace.

Under concurrency:

```text
Instance A                    Instance B
----------                    ----------
Check: unused
                              Check: unused
Execute
                              Execute
Mark used
                              Mark used
```

Both consumers observed the same precondition before either changed it.

The bug is not solved by making `HasBeenUsed` and `MarkUsed` individually thread-safe.

The required decision is atomic:

> **Check whether a use remains and claim that use as one indivisible operation.**

## Atomic Consume Semantics

Prefer a store boundary shaped like:

```csharp
public interface ICapabilityUseStore
{
    ValueTask<CapabilityUseResult> TryConsumeAsync(
        string capabilityId,
        int maximumUses,
        DateTimeOffset usedUtc,
        CancellationToken cancellationToken);
}
```

The important word is:

```text
TryConsume
```

not:

```text
CheckThenMaybeRecord
```

The store contract should make the authority transition explicit:

```text
Current state
        +
Maximum uses
        ↓
Atomic state transition
        ↓
Accepted with new count
or
Use limit exceeded
```

Possible implementation techniques include:

- A database unique constraint for a one-time-use row.
- A conditional update that succeeds only while the current use count is below the maximum.
- A compare-and-set operation supported by the selected durable store.
- A transaction that serializes competing consumption attempts for the same grant.

The implementation mechanism is secondary.

The architectural requirement is:

> **Competing consumers must agree on which attempt, if any, claimed the remaining authority.**

## Threat Diagram: Racing Consumers

```text
                         Valid capability
                               │
                ┌──────────────┴──────────────┐
                │                             │
          First consumer                Racing / replay
                │                             │
                └──────────────┬──────────────┘
                               │
                    Durable use-store boundary
                               │
                        atomic TryConsume
                               │
                ┌──────────────┴──────────────┐
                │                             │
             accepted                     rejected
        remaining use claimed       no permitted use remains
                │                             │
       protected execution              no execution
```

The decisive boundary is the atomic state transition.

The diagram does **not** claim that the protected side effect itself is exactly-once.

That is a separate problem discussed later.

---

## Multiple Application Instances Change the Requirement

An in-process lock can serialize threads inside one process.

It cannot coordinate another process that has its own memory.

Consider:

```text
Application instance A
    useStoreA = new HashSet<string>()

Application instance B
    useStoreB = new HashSet<string>()
```

Both receive:

```text
cap-123
```

Instance A records it locally.

Instance B has never seen it.

The second process can therefore accept the same capability.

If production can execute through multiple instances, replay state must be shared or otherwise coordinated at the scope where the replay guarantee is claimed.

That may mean:

- Shared durable database state.
- A strongly consistent key/value boundary.
- A single authoritative execution service.
- Region-local authority with explicit region binding.
- Another architecture whose consistency semantics are documented.

Do not claim distributed replay protection from a process-local collection.

## Process Restart Is Also a Boundary

Even one application instance can lose replay state:

```text
Capability consumed
        ↓
In-memory store records use
        ↓
Process restarts
        ↓
Memory is empty
        ↓
Same capability arrives again
```

The artifact may still be unexpired.

Without durable use state, the restarted process cannot distinguish the replay from the first use.

This is why in-memory stores are useful teaching tools but weak production replay guarantees.

They make the state transition visible without requiring infrastructure.

They do not make that state durable.

## In-Memory Stores Are Still Valuable Teaching Tools

A `HashSet<string>` or in-memory counter can demonstrate:

- Stable capability identity.
- First use succeeds.
- Second use fails.
- Bounded-use counts.
- Revocation/cancellation state.
- The location of the execution boundary.
- The difference between validation and consumption.

That is valuable.

The correct claim is:

> **This sample demonstrates replay-use semantics inside one process.**

Avoid:

> **This sample provides production replay protection.**

The production claim requires evidence about:

- Durability.
- Concurrency.
- Atomicity.
- Replication.
- Failure behavior.
- Retention.
- Region topology.
- Recovery.

---

## Expiration and Consumption Answer Different Questions

Expiration answers:

> Is this authority too old to accept now?

Consumption answers:

> Has this authority already been used as many times as permitted?

A capability can be:

```text
Unexpired
+
Already consumed
=
Reject
```

It can also be:

```text
Expired
+
Never consumed
=
Reject
```

Short expiration reduces the replay window.

It does not eliminate the need for use state when one-time or bounded-use semantics matter.

Likewise, durable consumption does not make an expired capability valid.

Treat both as independent conditions.

## Revocation, Cancellation, and Consumption Are Distinct States

A replay/use store may also participate in host-owned stop or cancellation behavior.

Conceptually:

```text
Active
Consumed to limit
Stopped / revoked
Cancelled
Unavailable
```

Those states answer different operational questions.

For example:

```text
Use limit exceeded
=
The authority was valid but no permitted uses remain.
```

```text
Stopped
=
The host intentionally ended authority before natural expiration.
```

```text
Cancelled
=
The associated operation or workflow was cancelled.
```

```text
Store unavailable
=
The host cannot establish current use state.
```

Do not report all of these as:

```text
invalid token
```

Stable categories and reason codes make behavior easier to diagnose and audit.

---

## Replay Store Unavailability Is a Policy Decision

A consequential execution boundary may depend on durable replay state.

What should happen if that state cannot be read or updated?

Dangerous fallback:

```text
Replay store unavailable
        ↓
Assume unused
        ↓
Execute
```

That converts loss of a security dependency into additional authority.

Safer choices may include:

```text
Defer
Deny
Queue for later validation
Escalate
Enter a documented reduced-capability mode
```

The correct choice depends on the operation.

For replay-sensitive mutation, a conservative default is often to avoid execution when current use state cannot be established.

High-availability or safety-critical systems may require a more nuanced degraded-mode design.

The requirement is:

> **Define replay-store failure behavior explicitly. Do not let missing state silently become permission.**

## Persistence Failure During Consumption

The store can fail at several points:

```text
Read succeeds
Write fails
```

```text
Transaction begins
Process crashes
```

```text
Primary accepts write
Client loses response
```

```text
Region becomes partitioned
```

The host needs to know what the store contract means when the result is ambiguous.

A durable provider should document:

- Whether accepted consumption is transactional.
- Whether a timeout can occur after the state transition commits.
- Whether retries of `TryConsume` are safe.
- What consistency scope applies.
- How stopped/cancelled state interacts with use counts.
- How old use records are retained and cleaned up.

The interface alone cannot provide those guarantees.

---

## Replay Protection Belongs Near the Execution Boundary

Replay state is most useful where authority becomes action.

Weak placement:

```text
API ingress
   ↓
Check replay
   ↓
Several queues / services / delays
   ↓
Protected side effect
```

A later component may receive a duplicated or independently replayed authority after the early check.

Prefer a boundary like:

```text
Current execution context
        ↓
Proof + metadata validation
        ↓
Current authorization / policy checks
        ↓
Atomic capability consumption
        ↓
Protected executor
```

The exact sequence depends on the host.

For example, a host normally should reject malformed, untrusted, expired, wrong-audience, wrong-resource, or wrong-policy grants **before** spending a permitted use.

The important invariant remains:

> **The component that decides whether bounded authority is still available should be part of the trusted execution boundary.**

## Validation and Consumption May Be One Public Operation

An implementation may expose:

```text
ValidateForExecutionAsync(...)
```

that internally performs:

```text
Proof verification
Metadata/binding validation
Use-store consumption
```

That can be a good API.

The conceptual distinction still matters because use checking is stateful while many other checks are not.

A reviewer should be able to answer:

- Which checks can run without changing state?
- Which operation consumes authority?
- At what point is the consumption committed?
- What happens if execution fails after consumption?
- What happens if the use store is unavailable?

The public method name does not remove those architectural questions.

---

## Capability Replay Protection Is Not Request Idempotency

These two controls are related but answer different questions.

### Capability replay protection

Question:

> **Should this authority artifact be accepted again?**

Typical key:

```text
CapabilityId / GrantId / TokenId
```

Typical state:

```text
Use count
Consumed flag
Stopped/cancelled state
```

Typical rejection:

```text
capability.use-limit-exceeded
```

### Request idempotency

Question:

> **If the same logical client operation is submitted again, should the application repeat the side effect or return/reuse the prior result?**

Typical key:

```text
IdempotencyKey / OperationId / CommandId
```

Typical state may include:

```text
Operation fingerprint
Execution status
Prior result or result reference
```

A client can submit two different valid capabilities for the same logical operation.

Each capability may pass replay protection once.

The underlying operation could still occur twice.

Conversely, the same capability could be rejected as replay even when the operation itself is naturally idempotent.

Do not use the terms interchangeably.

## External API Idempotency Keys

Some external providers accept an idempotency key.

When supported, a host may bind a stable operation identifier to the outbound call:

```text
Internal OperationId
        ↓
Provider idempotency key
        ↓
Retry after timeout
        ↓
Provider applies its documented duplicate-handling semantics
```

This can reduce duplicate external effects during ambiguous retries.

It does not change capability-use semantics.

A capability may still need to be consumed once at the host boundary.

The provider's idempotency guarantee also depends on its documented:

- Key scope.
- Retention window.
- Payload matching rules.
- Retry behavior.
- Failure semantics.

Do not claim more than the external provider guarantees.

---

## Replay Resistance Is Not Exactly-Once Execution

This is the most important distinction in this tutorial.

> **Replay resistance is not an exactly-once execution guarantee.**

Consider:

```text
Capability consumed
        ↓
External operation begins
        ↓
Process crashes before result is recorded
```

What happened?

Possible realities include:

```text
External operation never reached provider
```

```text
Provider completed operation
but response was lost
```

```text
Provider received operation
and is still processing
```

The replay store can tell us:

```text
The capability use was consumed.
```

It may not tell us:

```text
The external side effect definitely occurred exactly once.
```

## Failure Window A: Consume Before Execute

```text
Atomic consume succeeds
        ↓
Process crashes
        ↓
Executor never starts
```

The authority is spent.

The side effect may not have occurred.

Retrying the same capability is correctly rejected by the replay layer.

Recovery now requires a separate operation/recovery model.

This ordering favors prevention of capability reuse, but it can create a **consumed-without-completed-execution** state.

## Failure Window B: Execute Before Record

```text
External side effect succeeds
        ↓
Process crashes
        ↓
Use record never commits
```

The capability may appear unused after restart.

A retry can repeat the side effect.

This ordering preserves the possibility of retry after pre-execution failure but weakens replay resistance around the crash window.

Neither ordering magically produces exactly-once behavior for an unrelated remote system.

## Failure Window C: Consume Succeeds but Response Is Ambiguous

```text
TryConsume commits
        ↓
Store response is lost
        ↓
Host does not know whether use was consumed
```

The host should not blindly assume the consume failed.

A retryable consumption API may need stable operation identity so the provider can return the already-committed result.

The details are storage-specific.

This is another reason production providers must document timeout and retry semantics.

---

## When a Shared Transaction Can Help

If replay state and the protected local mutation live in the same transactional database, a host may be able to place them in one transaction:

```text
Begin transaction
        ↓
Atomically claim capability use
        ↓
Apply local mutation
        ↓
Commit
```

This can greatly narrow the failure window for **that local transactional boundary**.

It still does not create universal exactly-once semantics for:

- Remote HTTP APIs outside the transaction.
- Emails.
- Cloud control-plane calls.
- Physical devices.
- Independently committed downstream databases.
- Consumers that observe emitted messages before recovery completes.

State the boundary precisely.

## Outbox and Inbox Patterns

When durable messaging participates in execution, outbox/inbox patterns may become relevant.

Example:

```text
Transaction
   ├── consume capability
   ├── update local state
   └── write outbound message to outbox
        ↓
background delivery
        ↓
consumer inbox / deduplication
```

This can make the local decision and message intent durable together.

The consumer may then use its own idempotency or inbox state.

This is not "replay solved once."

It is a chain of explicit state transitions across ownership boundaries.

Each boundary needs its own duplicate/failure semantics.

## Recovery State Is Often More Useful Than "Exactly Once"

For consequential operations, model states such as:

```text
Authority consumed
Execution not started
Execution started
Execution outcome unknown
Execution completed
Execution failed
Recovery required
```

Those states are more operationally useful than claiming:

```text
Exactly once
```

when the architecture cannot prove it.

A recovery workflow can then decide whether to:

- Query the external provider.
- Reconcile current resource state.
- Retry with the same idempotency key.
- Create a new governed operation.
- Escalate for human review.
- Record a terminal failure.

---

## Operational Evidence for Replay Attempts

A rejected replay should be observable.

Useful evidence can include:

```text
Event type
Capability / grant identity
Correlation ID
Actor identity or safe actor reference
Operation
Resource reference when appropriate
Observed use count
Maximum use count
Outcome
Stable reason code
Execution attempted = false
Timestamp
```

Avoid storing the entire raw bearer artifact merely to diagnose replay.

The raw capability may itself be authority-bearing or contain sensitive data.

Prefer stable safe identifiers and reviewed metadata.

A useful event might communicate:

```text
Stage: capability-consumption
Outcome: rejected
Reason: capability.use-limit-exceeded
GrantId: cap-123
ObservedUseCount: 1
MaximumUseCount: 1
ExecutionAttempted: false
```

This is distinct from:

```text
Execution failed
```

because the executor should never have been invoked.

## Correlation Does Not Replace Replay Identity

Correlation is useful for connecting:

```text
Decision
Capability issuance
Consumption attempt
Execution attempt
Execution result
Recovery
```

But two retries may intentionally share one correlation ID.

Two separate capabilities may also be issued within one broader correlation flow.

Therefore:

```text
CorrelationId
≠
CapabilityId
≠
IdempotencyKey
≠
OperationId
```

A system may use some of these values as the same physical string when semantics truly align.

Do so deliberately, not accidentally.

---

## Retention of Replay State

Replay state does not necessarily need to live forever.

It must remain available for as long as the system could otherwise accept the authority or a delayed duplicate under the claimed replay policy.

Relevant considerations include:

- Capability expiration.
- Allowed clock skew.
- Queue redelivery windows.
- Retry windows.
- Offline clients.
- Regional replication delays.
- Incident/audit requirements.
- Provider recovery behavior.

Deleting use state too early can reopen the replay window if the artifact is still considered valid somewhere.

Keeping every record forever creates storage, privacy, and operational cost.

Retention is therefore part of the replay contract.

## Multi-Region Replay Protection Requires an Explicit Consistency Story

Suppose:

```text
Region A accepts cap-123
```

while:

```text
Region B has not yet observed that consumption
```

If Region B can execute the same authority before replication catches up, the system does not have a global single-use guarantee.

Possible architectures include:

- Route a capability to one authoritative region.
- Bind the capability audience to a region-specific executor.
- Use a globally coordinated strongly consistent store.
- Accept region-local replay guarantees and document the limit.
- Design the operation to be idempotent across regions.

Each option trades latency, availability, complexity, and consistency differently.

Do not claim global replay resistance without stating how competing regions coordinate.

---

## A Framework-Neutral Execution-Boundary Sketch

A host-owned gateway can make the state transition explicit:

```csharp
public sealed class ProtectedOperationGateway(
    ICapabilityValidator validator,
    ICapabilityUseStore useStore,
    IProtectedOperationExecutor executor)
{
    public async Task<ExecutionResult> ExecuteAsync(
        ExecutionCapability capability,
        ExecutionRequest request,
        CancellationToken cancellationToken)
    {
        CapabilityValidationResult validation =
            await validator.ValidateAsync(
                capability,
                request,
                cancellationToken);

        if (!validation.IsValid)
        {
            return ExecutionResult.Blocked(
                validation.ReasonCode);
        }

        CapabilityUseResult useResult =
            await useStore.TryConsumeAsync(
                capability.CapabilityId,
                capability.MaximumUses,
                request.NowUtc,
                cancellationToken);

        if (!useResult.Accepted)
        {
            return ExecutionResult.Blocked(
                useResult.ReasonCode);
        }

        return await executor.ExecuteAsync(
            request,
            cancellationToken);
    }
}
```

This sketch intentionally leaves several production questions unanswered:

- Is proof verification part of `validator`?
- Does authorization run before capability validation?
- How is resource freshness checked?
- Is `TryConsumeAsync` durable and distributed?
- Can `TryConsumeAsync` return an ambiguous timeout?
- What happens when execution fails after consumption?
- Is the executor local or remote?
- Does the remote system support idempotency?
- Which evidence is persisted durably?

The point is to make the boundary visible.

## Important Invariants to Test

Replay behavior deserves tests at the architecture boundary.

### First Use Succeeds

```text
Valid one-time capability
        ↓
TryConsume = Accepted
        ↓
Executor invocation count = 1
```

### Second Use Is Rejected

```text
Same capability
        ↓
TryConsume = UseLimitExceeded
        ↓
Executor invocation count remains 1
```

### Racing Consumers Cannot Both Claim the Final Use

```text
Two concurrent attempts
        ↓
One accepted
One rejected
        ↓
Total protected executions = 1
```

This test must exercise the actual store semantics whose guarantee is being claimed.

A process-local concurrency test does not prove distributed atomicity.

### Restart Does Not Reopen the Grant

For a durable production store:

```text
Consume capability
        ↓
Restart application
        ↓
Replay same capability
        ↓
Rejected
```

### Store Failure Does Not Become Permission

```text
Replay store unavailable
        ↓
Configured deny / defer / escalation behavior
        ↓
Executor invocation count = 0
```

### Expiration and Consumption Are Independent

Test:

```text
Expired + unused
Unexpired + consumed
Expired + consumed
Unexpired + remaining use
```

Each case should produce the documented result.

---

## Threat Review Table

| Threat / failure | What can go wrong? | Boundary to inspect |
| --- | --- | --- |
| Captured capability replay | Same authority is presented again | Atomic use-store consumption |
| Client retry | Duplicate attempt may look like replay | Capability use + request idempotency |
| Two app instances | Both may observe unused state | Shared durable atomic store |
| Process restart | In-memory history disappears | Durable replay state |
| Check-then-act | Two consumers pass the check | Single atomic consume operation |
| Early replay-state deletion | Old authority can become reusable | Retention policy |
| Store unavailable | Missing state becomes accidental allow | Explicit failure posture |
| Region replication lag | Same grant executes in two regions | Consistency / regional binding |
| Crash after consume | Authority spent, side effect uncertain/not started | Recovery state |
| Crash after remote success | Side effect occurred, local completion unknown | Provider idempotency + reconciliation |
| New capability for same operation | Each capability is single-use but operation duplicates | Operation/request idempotency |
| Raw capability logged | Replay credential leaks through telemetry | Logging/data-minimization boundary |

A review should ask what guarantee each control actually provides.

---

## Working Implementation References

Learning keeps this tutorial provider-neutral.

The current `AsiBackbone/AsiBackbone` repository contains a fuller capability-use seam that demonstrates the same architectural separation.

| Learning concept | Working reference | What to inspect |
| --- | --- | --- |
| Provider-neutral bounded-use state | [`ICapabilityGrantUseStore`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/ICapabilityGrantUseStore.cs) | `TryConsumeAsync` combines checking and consumption; Core explicitly leaves durable state, distributed locking, cache consistency, database schema, and replay-window guarantees to the host/provider. |
| Teaching/local in-memory provider | [`InMemoryCapabilityGrantUseStore`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Storage.InMemory/CapabilityTokens/InMemoryCapabilityGrantUseStore.cs) | Thread-safe in-process use counts and stopped/cancelled state, with explicit documentation that the provider is non-durable, non-distributed, and not production replay protection. |
| Execution validation pipeline | [`CapabilityGrantValidator`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityGrantValidator.cs) | Proof checks, metadata/binding checks, then optional use-store consumption; missing or unavailable replay state maps to an explicit non-success validation outcome instead of silent execution. |
| Use-check configuration | [`CapabilityGrantValidationOptions`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityGrantValidationOptions.cs) | `RequireUseCheck`, `MaxUseCount`, validation time, scope, policy, binding, and proof options. |
| Broader capability guidance | [Capability Grant Hardening](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/capability-grant-hardening.md) | Proof, issuer/audience/scope, replay/use limits, cancellation/revocation, time windows, and the host-owned security boundary. |
| Executable use-store behavior | [`InMemoryCapabilityGrantUseStoreTests`](https://github.com/AsiBackbone/AsiBackbone/blob/main/tests/AsiBackbone.Core.Tests/CapabilityTokens/InMemoryCapabilityGrantUseStoreTests.cs) | In-process accepted use, use-limit, stopped/cancelled, and local concurrency behavior. |

The implementation repository is a specimen, not a universal storage prescription.

The durable replay guarantee remains host-owned.

---

## Relationship to Existing Learning Samples and Labs

The foundational capability sample teaches:

```text
Decision
   ↓
Scoped capability
   ↓
Execution-boundary validation
   ↓
Host-owned execution
```

The scoped-capability lab then asks the learner to add a small in-memory single-use store.

The governed AI tool-gateway sample includes single-use consumption so a replayed capability does not reach the dry-run handler twice.

Those examples are intentionally local and deterministic.

This tutorial supplies the larger conceptual frame:

```text
Local in-memory demonstration
        ↓
Understand the state transition
        ↓
Production deployment topology
        ↓
Choose durable atomic semantics that match the claimed guarantee
```

Use the existing samples to observe the boundary.

Use this tutorial to reason about the production state and failure model.

---

## When a Simpler Pattern Is Better

Do not add a durable replay subsystem merely because a capability exists.

A simpler design may be enough when:

- The operation is read-only.
- Repetition is harmless and expected.
- The host immediately executes locally and no reusable authority crosses time/process boundaries.
- Existing database constraints already make the consequential state transition idempotent.
- An ordinary authorization check is the only security decision required.
- The system can safely derive current truth from resource state rather than remembering every prior request.

Even then, distinguish:

```text
Operation safe to repeat
```

from:

```text
Authority safe to reuse
```

They may not be the same.

## Tradeoffs

### Benefits

- Prevents a valid authority artifact from being reused beyond its intended limit.
- Makes one-time and bounded-use semantics explicit.
- Gives multi-instance systems a clear state-consistency requirement.
- Produces observable replay-rejection evidence.
- Separates artifact validity from current operational acceptability.
- Makes failure windows easier to reason about.
- Reduces pressure to pretend remote side effects are exactly-once.

### Costs

- Durable replay state adds a storage dependency.
- Atomic consumption can add latency and contention.
- Global replay guarantees can conflict with regional availability and latency goals.
- Retention and cleanup require lifecycle design.
- Ambiguous store failures require recovery semantics.
- Consuming before execution can spend authority when execution never completes.
- Idempotency and reconciliation may still be required after replay protection is correct.

Replay protection is therefore not "free security."

It is a deliberate state-management contract.

---

## Review Checklist

Before claiming replay resistance for a consequential path, ask:

1. What exact operation is replay-sensitive?
2. What stable identity represents the authority being consumed?
3. Is the grant one-time or bounded-use?
4. Where is maximum-use policy defined?
5. Which checks are static validation and which check changes state?
6. Is check-and-consume one atomic operation?
7. What happens when two threads race?
8. What happens when two application instances race?
9. What happens after process restart?
10. Is replay state durable for the full acceptance/retry window?
11. What happens when the replay store is unavailable?
12. Can a timeout occur after consumption commits?
13. Is retrying the consume operation safe?
14. How are stopped, cancelled, expired, and consumed states distinguished?
15. Is replay identity different from correlation identity?
16. Is request idempotency required in addition to capability replay protection?
17. Can two different valid capabilities cause the same logical operation twice?
18. Does an external provider support an idempotency key?
19. What happens if the process crashes after consumption but before execution?
20. What happens if the remote side effect succeeds but the host never records completion?
21. Is transaction/outbox/inbox coordination relevant?
22. What evidence records rejected replay attempts without logging the raw authority artifact?
23. How long is replay state retained?
24. What consistency guarantee exists across regions?
25. Can the team state the guarantee without using "exactly once" more strongly than the architecture supports?

If those questions are unanswered, the replay model is probably still implicit.

---

## Review Questions

Before moving on, you should be able to answer:

1. Why can a correctly signed, unexpired capability still be a replay?
2. Why is a nonce not replay protection by itself?
3. What is wrong with separate `HasBeenUsed` and `MarkUsed` operations?
4. Why must a bounded-use store expose atomic consume semantics?
5. Why is an in-memory `HashSet` useful in a sample but insufficient for multi-instance production?
6. What happens to in-memory replay protection after restart?
7. How do one-time and bounded-use grants differ?
8. Why are expiration and consumption independent?
9. Why should replay protection be evaluated near the execution boundary?
10. What should happen when required replay state is unavailable?
11. What is the difference between capability replay protection and request idempotency?
12. Why can two different single-use capabilities still duplicate one logical side effect?
13. Why does consuming a capability before execution create a recovery window?
14. Why does recording consumption after a remote side effect create a duplicate window?
15. How can external idempotency keys reduce ambiguous-retry risk?
16. When can a shared local transaction narrow the failure window?
17. When do outbox/inbox patterns become relevant?
18. Why is replay resistance not an exactly-once execution guarantee?
19. What evidence should remain after a replay attempt is rejected?
20. What must change when the application runs in multiple regions?

---

## Related Content

- [Security](index.md) — return to the Security learning-area overview.
- [Trust Boundaries and Least Privilege](trust-boundaries-and-least-privilege.md) — review authority ownership and validation across trust boundaries.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — review how narrow execution authority is created and validated.
- [Scoped Capability and Host-Owned Execution sample](https://github.com/AsiBackbone/Learning/blob/main/samples/scoped-capability-and-host-owned-execution/README.md) — run the existing capability boundary and focused invariant tests.
- [Scoped Capability and Host-Owned Execution lab](../labs/scoped-capability-and-host-owned-execution.md) — add local single-use state and observe its limits.
- [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) — see single-use capability validation composed around AI-proposed actions.
- [Governed AI Tool Gateway sample](https://github.com/AsiBackbone/Learning/blob/main/samples/governed-ai-tool-gateway/README.md) — observe single-use consumption inside the dry-run AI gateway.
- [Governed AI Tool Gateway lab](../labs/governed-ai-tool-gateway.md) — break single-use enforcement and threat-model replay versus idempotency.
- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) — preserve evidence across decision, acknowledgment, execution, and failure stages.

---

> **Read it. Run it. Question it. Improve it.**
