---
description: Separate a user's present authorization from the narrow, short-lived authority a later worker or service needs to perform one delayed operation.
title: How Short-Lived Execution Authority Differs from User Authorization
author: Christopher D. Cavell
published: "2026-10-03"
summary: User authorization says whether an actor may request an operation now. Execution authority delegates only what a later executor needs for one operation, resource, audience, time window, and use count, and the host still checks current state before acting.
feed: true
x_hashtags:
  - DotNet
  - SoftwareArchitecture
---

# How Short-Lived Execution Authority Differs from User Authorization

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** No formal prerequisites. Familiarity with access tokens, background workers, or message queues in .NET is helpful, but no AsiBackbone package, identity provider, token format, or prior Learning material is required.

**What this article covers:** why a user's access token or claims are not the right authority to carry into a background worker, how to separate the actor, the accepted operation, the later executor, the delegated grant, and the protected host, which bindings make delayed authority narrow, what the host must still check at execution time, how one-time use differs from idempotency, and when you should not mint any extra authority at all.

A request arrives. The user is authenticated, your authorization check passes, and the work has to happen later: overnight, after a delay, in another process, or in another service.

The work item needs to carry something that lets the later step do its job. The most convenient thing to hand over is whatever you already have:

```text
User signs in → access token
        ↓
API authorizes the request
        ↓
Job message { payoutId, userAccessToken }
        ↓
Worker calls the payments service with the user's token
```

This works in a demo. It also hands the worker whatever authority that token carries, for as long as the token or anything derived from it stays usable, even though the worker needs to do one thing, once, at one service.

The distinction this article is about is:

> **User authorization answers whether an actor may request or perform an operation now. Short-lived execution authority delegates only the narrow permission a later executor needs, bounded to a specific operation, resource, audience, lifetime, and use count.**

They are evaluated at different times, about different subjects, for different purposes. Treating one as the other gives the later executor either too much authority or authority that has gone stale.

The opposite mistake matters as much. Many background jobs and service calls do not need a delegated grant at all. Near the end, this article covers when ordinary service identity, a fresh authorization check, or immediate execution in the same host is the simpler and safer design.

---

## The Example: A Scheduled Vendor Payout

The rest of the article uses one operation.

On Tuesday, a finance operator schedules payout `P-1042`: **18,400.00 EUR** to vendor `V-311`, sent to that vendor's verified bank account, to run on Friday at 09:00 UTC.

In this example, scheduling is the commitment: the operator's permission to schedule a payout means permission to commit this exact payout for later execution, with no further review. If scheduling only created a proposal that someone else must approve, that approval, not the scheduling request, would be the decision that justifies an executable grant.

The system has three parts:

- The **Finance API**, where the operator signs in and schedules the payout.
- The **payout worker**, a background process that picks up scheduled payouts when they are due.
- The **payments service**, which holds the banking provider credentials and is the only component that can actually move money.

On Friday, the worker asks the payments service to execute `P-1042`.

A lot can change between Tuesday and Friday:

- The operator can change roles or leave the company.
- Someone can edit the payout amount.
- Someone can change the vendor's bank account. Redirecting a payment by changing the recipient's account details is a recognized business email compromise technique, and the FBI's [business email compromise advisory](https://www.ic3.gov/PSA/2022/PSA220504) recommends verifying requests to change account information through a separate channel.
- The vendor can be placed on hold.
- The organization's payout limits can change.

The question is not only "was the operator allowed to schedule this?" That was answered on Tuesday. The question on Friday is "may this worker cause exactly this payout, at this service, now?"

---

## Five Responsibilities, Not One Token

The forwarded-token design merges five separate responsibilities. Naming them makes the problem visible.

| Responsibility | In the example | Question it answers |
| --- | --- | --- |
| **Actor** | The finance operator, authenticated by the Finance API | Who is asking, and are they allowed to request this operation now? |
| **Accepted operation** | Payout `P-1042`: exact amount, currency, vendor, beneficiary account, and execution date | What exactly was requested and accepted? |
| **Later executor** | The payout worker, authenticated by its own workload identity | Which component will carry the operation forward? |
| **Delegated execution grant** | Authority to execute `P-1042` only, at the payments service only, by the payout worker only, during Friday's window only, once | What may the executor do with the accepted operation? |
| **Protected host** | The payments service | Do the grant, the executor, the resource, and the current policy all still permit this side effect right now? |

Each responsibility has its own identity and its own lifetime:

- The actor's authentication and authorization belong to the operator's Tuesday session.
- The accepted operation is a durable record. It is the thing every later decision refers to.
- The executor's identity belongs to the worker, not to the operator.
- The grant exists only to bridge the accepted operation to the later executor.
- The host owns the side effect and the credentials that cause it. It is the last place that can say no.

None of these should silently become another. In particular, the operator's identity should not become the worker's identity, and the grant should not become a substitute for the host's own checks.

---

## What Goes Wrong When the User's Token Rides Along

Forwarding the operator's access token to the worker merges the actor and the executor into one credential. Each problem below follows from that.

How much authority the forwarded token carries depends on how it was issued. A well-designed token may already be restricted by audience, scope, and resource, as [OAuth 2.0 Security Best Current Practice (RFC 9700)](https://www.rfc-editor.org/rfc/rfc9700.html) recommends. The problems below are sharpest with the coarse tokens many applications issue in practice, but most of them remain even when the token is well scoped, because it was scoped for the user's request, not for Friday's execution.

**The scope is the user's, not the operation's.** An access token represents what the user's client may do at one or more APIs. Coarse-grained tokens are common: a token issued for the Finance UI may also allow listing vendors, reading invoices, and scheduling other payouts. Even a token limited to `payouts:schedule` describes a kind of action, not this payout. The worker needs to execute one payout.

**The audience is wrong.** In this example, the token was issued for the Finance API. If the payments service accepts it, the payments service is trusting a credential that was never intended for it. If it rejects it, the design does not work.

**The lifetime is wrong in both directions.** Access-token lifetime is set by the issuer and is separate from the user's session. Current guidance favors short lifetimes, often minutes, though longer ones still exist. A short-lived token issued on Tuesday is useless on Friday. Teams then extend token lifetimes or store refresh tokens in job messages so the worker can obtain new user tokens. A stored refresh token can extend usable authority well beyond the original access token's lifetime, subject to the issuer's expiration, revocation, rotation, and client-binding rules. Each of those choices makes the stored credential more valuable to anyone who can obtain and use it.

**It ordinarily describes delegated access, not this accepted payout.** The token shows that the operator authenticated and that their client was granted certain scopes or claims. OAuth tokens can be restricted to particular resources and actions, but the token issued for the operator's Tuesday session was not issued to record that this specific payout was accepted, with this amount and this beneficiary.

**It can be stale.** If the operator's role is revoked on Wednesday, a stored token or refresh token may still produce something the payments service accepts on Friday. Whether the payout should still run is a real policy question, discussed later. It should be answered deliberately, not by whatever a cached credential happens to allow.

**It widens exposure.** Once a bearer token is in a queue message, anything that can read the queue may be able to use it: other consumers, dead-letter queues, message inspection tools, diagnostic logs, retention, and backups. Sender-constrained tokens reduce this risk, but they are bound to the client that obtained them, which is usually not the worker.

Forwarding the original credential is different from deliberately obtaining downstream authority. Delegation flows such as [OAuth 2.0 Token Exchange (RFC 8693)](https://www.rfc-editor.org/rfc/rfc8693.html) and Microsoft's [On-Behalf-Of flow](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-on-behalf-of-flow) issue a separate token for the downstream service instead of reusing the original. That is a better starting point. On its own, though, it does not bind the token to one payout, consume it after one use, or cancel it when the payout is cancelled. Those properties still have to be designed.

A common variant forwards **claims** instead of the token: the API copies the operator's user ID and roles into the job message, and the worker trusts them. A properly validated token has an issuer-backed verification mechanism, such as a signature or introspection. Copied claims have whatever integrity the message itself provides. If the worker treats them as authoritative without a trusted origin and integrity check, anyone who can write to the queue can write `"role": "FinanceAdmin"`.

Another variant treats **possession of the queued message as authority**: if a message saying "execute P-1042" arrived on the payouts queue, the worker executes it. The queue's access control then becomes the authorization model for moving money. Authenticated or signed messages help, because they prove where a message came from and that it was not altered. They do not, by themselves, say what the sender was allowed to ask for, or whether that permission still holds.

---

## Two Questions on Two Clocks

The cleanest way to keep the two concepts apart is to write down the question each one answers and when.

**User authorization, Tuesday, in the Finance API:**

> May this authenticated operator schedule a payout of 18,400.00 EUR to vendor `V-311`?

This uses the operator's identity, roles or claims, and resource rules, for example whether the operator belongs to the right cost center and whether the amount is within their scheduling limit. It is evaluated now, and its answer is about now.

**Execution authority, Friday, at the payments service:**

> May the payout worker execute exactly payout `P-1042`, at this service, inside Friday's window, for the first and only time?

This is about a different subject (the worker), a different audience (the payments service), a different time, and a much narrower scope (one operation on one resource).

The first decision is an input to the second. If the operator was not authorized on Tuesday, no grant should be issued. A Tuesday "yes" does not make Friday's question disappear, though. It is why the grant exists. It is not, by itself, permission to move money on Friday.

---

## The Bounded Handoff

The narrow design replaces "forward what the user had" with "issue what the executor needs":

```text
Operator's request (Tuesday)
    → Finance API authenticates the operator and authorizes the request
    → Exact operation recorded: payout P-1042, 18,400.00 EUR, vendor V-311,
      beneficiary account, execution date
    → Narrow execution grant issued
        operation: payout.execute
        resource:  P-1042 (and a fingerprint of the accepted payout)
        executor:  payout-worker
        audience:  payments-service
        window:    Friday 09:00–12:00 UTC
        uses:      1
        decision:  reference to Tuesday's authorization decision
    → Payout scheduled; job message carries the payout ID and a grant reference

Worker (Friday)
    → Authenticates to the payments service with its own workload identity
    → Presents the grant reference and asks to execute P-1042

Payments service
    → Validates the grant's bindings against the caller and the request
    → Re-checks the current payout, vendor, and policy
    → Atomically consumes the grant and claims the payout
    → Executes with its own banking credentials
    → Records evidence linking operator, decision, grant, worker, and side effect
```

Each binding removes a specific kind of misuse:

| Binding | What it prevents |
| --- | --- |
| **Operation** (`payout.execute`) | Using the grant to cancel, edit, or approve anything, or to perform a broader `payout.*` action. |
| **Resource** (`P-1042` plus a fingerprint of the accepted values) | Executing a different payout, or the same payout after its amount or beneficiary changed. |
| **Executor** (`payout-worker`) | Another component, or an attacker who copied the grant, presenting it. |
| **Audience** (`payments-service`) | Replaying the grant at a different service that might interpret it differently. |
| **Window** (not before / expires at) | Executing early, or keeping usable authority around indefinitely. |
| **Use count** (1) | Executing the same accepted operation twice by presenting the grant again. |
| **Decision reference** | Losing the link between this execution and the authorization that justified it. |

Notice what is **not** in the grant: the operator's roles, scopes, or access token. The grant records that a decision was made and by whom. It does not carry the operator's standing authority forward.

The window is short and specific, but it is not "a few seconds." Execution authority should live as long as the delayed operation reasonably needs and no longer. "Short-lived" means proportional to the operation, not a fixed number.

Distinguish the executable window from the exposure period. The grant can only be used during Friday's three hours, but it exists from the moment it is issued on Tuesday, so a reference leaked on Tuesday stays sensitive until Friday's expiry. That is acceptable here because the grant is useless without the worker's own authenticated identity, can be cancelled, and has its use state held by the host. A self-contained grant that any holder could present would justify a much shorter lifetime, or issuance closer to execution.

---

## User Authorization Versus Execution Authority at a Glance

The left column describes the user's authorization decision and the credential that typically accompanies it. The right column describes a delegated grant and its execution lifecycle. They are not two versions of the same thing.

| Dimension | User authorization and the user's credential | Short-lived execution authority |
| --- | --- | --- |
| **Subject** | The authenticated user or calling actor | The specific executor that will act, such as a worker or gateway |
| **Audience** | The application or API the user's client is calling | The one host that performs the side effect |
| **Resource scope** | Often a class of resources, filtered by claims, ownership, or rules | One resource, ideally bound to the exact accepted values |
| **Operation scope** | Whatever the user's roles, scopes, or policies allow | One named operation |
| **Issued and lifetime** | Each decision is made per request; access-token lifetime is set by the issuer and is separate from the session | Issued after a specific decision; valid only for the execution window |
| **Revocation** | Session or token revocation; role changes take effect when a check reads current authorization state, not while cached claims are still trusted | Grant-level cancellation, plus the host's own current checks |
| **Use count** | Each operation can be authorized or denied on its own; a token can usually be presented repeatedly until it expires | One use, or a small explicit bound, tied to the accepted operation |
| **Delegation** | The original credential is not meant to be forwarded; downstream authority should be obtained deliberately, for example through token exchange | Exists specifically to delegate across a time or trust boundary |
| **Replay protection** | Bearer-token theft and replay remain concerns, addressed with short lifetimes, sender constraints, and audience restriction | Additionally, the host must refuse a grant whose permitted uses are already spent |
| **Freshness at execution** | Answers about the moment of the request | Must be combined with current resource and policy checks at execution |
| **Evidence** | Who was allowed to ask | Which decision authorized which executor to perform which side effect |

The right column does not replace the left. User authorization still decides whether a grant should exist in the first place.

---

## The Grant Is a Lifecycle, Not a Token Format

It is tempting to read the previous sections as "issue a JWT with different claims." The representation is the least important part.

An execution grant can reasonably be:

- a **server-side grant record** that the worker references by an unguessable ID, with all bindings and use state held by the issuer or the host;
- a **signed, self-contained token** that the host verifies cryptographically, paired with server-side state for consumption and cancellation;
- a **protected message envelope** in which the bindings travel with the work item and are verified by the host;
- or a standards-based mechanism your platform already supports, such as a token exchange that produces a narrowly scoped, audience-restricted token for the worker, combined with the one-time consumption, payout binding, and cancellation that the exchange itself does not provide.

For a one-time operation, a server-side grant record is often the simplest choice. Consumption and cancellation need server-side state anyway, so a self-contained token adds key management without removing the store.

What makes any of these an execution grant is its lifecycle:

1. **It is issued only after a decision.** The Finance API issues it after the operator was authorized and the exact payout was recorded. A request alone is not enough; issuance requires the recorded authorization decision.
2. **It is derived from the accepted operation, not from the user's credential.** Its bindings come from the payout record, not from copying the operator's scopes.
3. **It is validated at the host that performs the side effect.** A check in the worker is useful, but the payments service is the only place where validation and execution happen together.
4. **It is consumed.** A one-time grant is spent when it is used, and the host must refuse to accept it again.
5. **It can be cancelled.** If the payout is cancelled on Thursday, the grant should stop working on Thursday.
6. **It leaves evidence.** Issuance, presentation, the host's decision, and the side effect can be linked afterward.

A signature and an expiry time cover only a small part of this. A signed token that is never consumed is reusable until it expires. A signed token that the host validates without checking the current payout will execute a payout that has since been altered.

The grant also does not authenticate the worker. The worker proves who it is with its own workload identity: a managed identity, a client certificate, a platform-issued service token, or whatever your environment provides. The grant says what that authenticated worker may do with this one operation. Bind the grant to a stable identifier for that workload, such as the identity provider's issuer, tenant, and subject or application ID, rather than a display name that another workload could share. The article writes `payout-worker` for readability. Requiring both means a copied grant reference is useless to anyone who is not the worker, and a compromised worker can execute only the operations it holds grants for.

---

## What the Protected Host Checks on Friday

The payments service receives a call from an authenticated workload asking to execute `P-1042` with a grant reference. The order of its checks matters.

The sketch below uses a server-side grant record. The same checks apply to other representations. It is illustrative, not a complete payments implementation.

```csharp
public sealed record ExecutionGrant(
    string GrantId,
    string Operation,            // "payout.execute"
    string ResourceId,           // "P-1042"
    string ResourceFingerprint,  // PayoutFingerprint.Compute of the accepted payout
    string ExecutorId,           // stable workload identifier, such as issuer + subject
    string Audience,             // "payments-service"
    DateTimeOffset NotBefore,
    DateTimeOffset ExpiresAt,
    int MaxUses,                 // 1
    string DecisionId);          // Tuesday's authorization decision

public enum ClaimResult { Claimed, GrantSpent, GrantCancelled, GrantOutsideWindow, PayoutChanged }

public static class PayoutFingerprint
{
    // A versioned, canonical encoding of every value that affects execution. Every component
    // that computes or compares the fingerprint must use exactly this encoding.
    public static string Compute(Payout payout)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[4];  // reused for each field's length prefix

        foreach (string field in new[]
        {
            "payout-fingerprint/v1",
            payout.Id,
            payout.VendorId,
            payout.BeneficiaryAccountVersionId,  // immutable record of the accepted bank destination
            payout.AmountMinorUnits.ToString(CultureInfo.InvariantCulture),
            payout.Currency,
            payout.ExecuteOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),  // date-only DateOnly value
        })
        {
            // Each field is prefixed with its UTF-8 byte length as a 4-byte big-endian integer.
            byte[] bytes = Encoding.UTF8.GetBytes(field);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }
}

public sealed class PayoutExecutionHost(
    IGrantStore grants,
    IPayoutStore payouts,
    IPayoutExecutionPolicy policy,
    IBankingProvider bank,
    TimeProvider clock)
{
    private const string Audience = "payments-service";

    public async Task<ExecutionResult> ExecuteAsync(
        WorkloadIdentity caller,   // established by transport authentication, never read from the message
        string grantId,
        string payoutId,
        CancellationToken cancellationToken)
    {
        ExecutionGrant? grant = await grants.FindAsync(grantId, cancellationToken);
        if (grant is null)
            return ExecutionResult.Rejected("grant.unknown");

        // Bindings: is this grant meant for this host, this caller, this operation, and this payout?
        if (grant.Audience != Audience)
            return ExecutionResult.Rejected("grant.wrong-audience");
        if (grant.ExecutorId != caller.StableId)
            return ExecutionResult.Rejected("grant.wrong-executor");
        if (grant.Operation != "payout.execute" || grant.ResourceId != payoutId)
            return ExecutionResult.Rejected("grant.scope-mismatch");

        // An early rejection only. The window is enforced again when the grant is consumed.
        DateTimeOffset now = clock.GetUtcNow();
        if (now < grant.NotBefore || now >= grant.ExpiresAt)
            return ExecutionResult.Rejected("grant.outside-window");

        // Resource freshness: is the payout still the one that was authorized?
        Payout? payout = await payouts.FindAsync(payoutId, cancellationToken);
        if (payout is null || payout.Status != PayoutStatus.Scheduled)
            return ExecutionResult.Rejected("payout.not-executable");
        if (PayoutFingerprint.Compute(payout) != grant.ResourceFingerprint)
            return ExecutionResult.Rejected("payout.changed-since-authorization");

        // Current policy: vendor hold, current verified bank account, limits, originating-actor rules.
        PolicyResult current = await policy.EvaluateAsync(payout, grant, cancellationToken);
        if (!current.Allowed)
            return ExecutionResult.Rejected(current.Reason);

        // One transaction in a store that holds both grant and payout state: consume a grant use
        // (not cancelled, uses remaining, inside the window by authoritative current time evaluated
        // in the conditional transition itself),
        // move the payout from Scheduled to Executing if its version is unchanged, and insert the
        // durable execution-attempt record. If the store is unavailable this throws, and nothing executes.
        var attempt = new ExecutionAttempt(
            AttemptId: Guid.NewGuid(), PayoutId: payout.Id, GrantId: grant.GrantId, ExecutorId: caller.StableId);
        ClaimResult claim = await grants.TryConsumeAndClaimAsync(
            attempt, expectedPayoutVersion: payout.Version, cancellationToken);
        if (claim != ClaimResult.Claimed)
        {
            return ExecutionResult.Rejected(claim switch
            {
                ClaimResult.GrantSpent => "grant.already-used",
                ClaimResult.GrantCancelled => "grant.cancelled",
                ClaimResult.GrantOutsideWindow => "grant.outside-window",
                _ => "payout.changed-since-check",
            });
        }

        // The host's own credentials perform the side effect. The idempotency key is the
        // accepted operation's identity, not the grant's.
        try
        {
            BankResult result = await bank.SendAsync(payout, idempotencyKey: payout.Id, cancellationToken);
            await payouts.RecordOutcomeAsync(attempt.AttemptId, result, CancellationToken.None);
            return ExecutionResult.FromBank(result);
        }
        catch (Exception)
        {
            // Timeout, cancellation, lost response, or a failed outcome write after the bank call:
            // the outcome is unknown, not failed, and goes to reconciliation. This conservatively
            // includes failures before the provider was contacted. A production adapter would
            // separate those, and definitive provider rejections, from ambiguous failures, and
            // keep the diagnostic details.
            await payouts.MarkUnknownAsync(attempt.AttemptId, CancellationToken.None);
            return ExecutionResult.Unknown("payout.outcome-unknown");
        }
    }
}
```

A few points in this sketch carry most of the weight.

**The caller's identity comes from the connection, not from the message.** `WorkloadIdentity` is whatever mutual TLS, a platform identity, or a service token established, and `StableId` is its stable identifier, not a display name. If the executor identity came from the request body, the executor binding would mean nothing.

**Finding a genuine, in-date grant is the start of the decision, not the end.** With a server-side record, the lookup establishes that the grant is genuine. With a self-contained token, signature verification does. Either way, the binding and window checks only establish that the grant applies to this request. Everything after them establishes that acting on it is still correct.

**Freshness is checked against the current payout, not the grant's copy of it.** The fingerprint lets the host detect that the amount, currency, vendor, beneficiary account, or date changed after Tuesday's decision. It is only as good as its encoding. It must include every value that affects execution, and the issuer and the host must serialize those values identically. Otherwise a legitimate payout is rejected because two components formatted an amount differently, or an omitted field changes without detection. The sketch uses a versioned encoding with each field prefixed by its UTF-8 byte length and the amount in minor currency units.

The sketch also makes two modeling assumptions explicit. `BeneficiaryAccountVersionId` identifies an immutable version of the accepted bank destination, so any change to the account details produces a new version and a different fingerprint. If your account IDs refer to records that can be edited in place, hash the destination details themselves, or an account version, instead of the ID. `ExecuteOn` is a date-only business value; the grant's window, not the fingerprint, controls the permitted execution time. If the scheduled time matters to the operation, include the full timestamp.

The fingerprint detects a change to the payout. The Friday policy check covers the other direction: the vendor's *currently* verified destination must still be the account version the payout targets. Both checks are needed, and the policy check must compare account versions or details, not just the same stable ID.

**The consume and the claim are one atomic step, and that step protects only what it checks.** If the grant were consumed in one call and the payout status changed in another, two workers racing on the same payout, or an edit landing between the check and the update, could slip through. Making both conditional in one transaction means the grant's use state and the payout's version are exactly what the host checked. It does not freeze facts held elsewhere. The vendor's hold status, the bank account's verification status, organizational limits, and the operator's current authority could all change between the policy evaluation and the claim. For each such fact, decide whether to include it in the transaction's conditions, take a lock or reservation, compare a version, or accept a documented freshness rule such as "evaluated within the same request." The [replay protection guide](../../security/replay-protection-and-bounded-use.md) covers atomic consumption, distributed races, and what to do when the grant store is unavailable.

**The atomicity has to be real.** `TryConsumeAndClaimAsync` is atomic only if grant state and payout state live behind one transactional boundary, such as one database, or a coordination mechanism designed for the purpose. Calling one method does not make two separate stores atomic.

**The window is enforced with the clock at consumption.** Loading the payout and evaluating policy take time, and the grant could expire in between. The early window check is a fast rejection only. The consume operation checks both `NotBefore` and `ExpiresAt` using authoritative current time as part of the atomic conditional state transition, for example by comparing the window in the same conditional update that consumes the grant. Database clock functions differ: some return the transaction's start time, others the statement's start or the actual current time, so choose the one that means what you intend. A timestamp read earlier, by the application or at the start of the transaction, does not establish validity at the transition.

In this design, expiry limits *admission* at that transition. The transaction may commit, and the banking call may happen, afterward. If the claim must commit before expiry, treat that as a stronger implementation requirement; the conditional update alone does not guarantee it. If the side effect itself must start or complete before a deadline, that is a different guarantee again and needs its own enforcement.

**Every pre-execution failure stops before the bank is called.** None of the rejection paths fall back to executing. An unavailable policy dependency or grant store should produce a rejection or a retryable deferral, never a payout. Failures after the banking call has started are different, because the side effect may already have happened.

**An unknown outcome is recorded as unknown.** If the banking call times out, the payout may or may not have been sent. The host marks it for reconciliation instead of treating it as failed. Finalization writes use `CancellationToken.None` so that a caller giving up does not cancel them.

That is not the same as guaranteeing they persist. A production design also needs recovery for the failures the `catch` block cannot see:

- The process can crash after the claim, leaving the payout in `Executing` with no outcome.
- The process can crash after the bank accepts the payment and before any outcome is written.
- `MarkUnknownAsync` itself can fail.
- The claim transaction's commit can be ambiguous, leaving the worker unsure whether the grant was consumed.

The usual answer is a durable execution-attempt record written in the same transaction as the claim, the same principle as the [transactional outbox pattern](https://learn.microsoft.com/en-us/azure/architecture/databases/guide/transactional-outbox-cosmos). A recovery process then finds attempts stranded in `Executing`, reconciles them with the provider using the operation's identity, and retries failed finalization writes. The [replay protection guide's failure windows](../../security/replay-protection-and-bounded-use.md#replay-resistance-is-not-exactly-once-execution) walk through the same cases in more depth.

---

## Which Facts Must Still Be True on Friday?

A grant records what was decided. It does not freeze the world. The host has to decide which facts it rechecks at execution and which it accepts from Tuesday. For each fact it rechecks, it also has to decide how fresh the check must be, as described in the previous section.

| Fact | Established Tuesday | Rechecked Friday? |
| --- | --- | --- |
| The payout's amount, currency, vendor, account, and date are as accepted | Yes, recorded and fingerprinted | **Yes.** Any change means the grant no longer describes the operation. |
| The vendor's currently verified bank destination is the account version the payout targets | Yes | **Yes.** A recent beneficiary change should stop execution or require re-verification. |
| The vendor is not on hold or a restricted-party list | Yes | **Yes.** Holds are exactly the kind of fact that changes between scheduling and payment. |
| The payout is within current organizational limits | Yes | **Usually yes.** If limits tightened, decide whether in-flight payouts are grandfathered or re-checked. |
| The grant has not been cancelled | Not applicable | **Yes**, as part of the atomic consume. |
| The operator was authorized to schedule the payout | Yes | **A policy choice.** See below. |

The last row deserves its own discussion. If the operator leaves on Wednesday, should Friday's payout still run?

There are legitimate answers either way:

- **The decision stands.** The operator was authorized when they made it, the organization accepted the payout, and payments to vendors should not stop because an employee changed jobs. The evidence still records who scheduled it.
- **The decision must still be supported.** The payout requires that the person who scheduled it still holds the authority, or a second person must re-approve it.

Either answer can be right for a given organization. The failure is having no answer: letting the outcome depend on whether a cached token happened to remain valid. Write the rule down and enforce it in the host's execution policy, where it can be tested.

Forwarding the operator's token answers this question accidentally. A grant that records the decision, combined with an explicit execution policy, answers it deliberately.

---

## One-Time Use Is Not the Same as Idempotency

The grant is single-use. The banking call uses an idempotency key. These solve different problems, and conflating them leaves a gap.

**Replay protection** answers: *should this authority be accepted again?* The key is the grant ID. The host rejects a second presentation of the same grant, whether it comes from a duplicated queue message, a retried worker, or someone who copied the reference.

**Idempotency** answers: *if the same logical operation is attempted again, should the side effect happen again?* The key is the operation, here the payout ID. In this example two mechanisms provide it. The payout's own state transition from `Scheduled` to `Executing` is one barrier, because a payout that has left `Scheduled` cannot be claimed again. The banking provider's idempotency key is another, for repeated requests that reach the provider.

Each covers a case the other does not:

- If someone issues a **second valid grant** for the same payout, perhaps because a retry path re-issued authority, replay protection accepts it, because it is a different grant. Whether the vendor could be paid twice then depends on the operation-level controls. Here, the payout state transition refuses a second claim. In a design without that barrier, or one where a recovery path returns the payout to an executable state, a second payment becomes possible unless the provider deduplicates it.
- If the worker **re-presents the same grant** after the bank call timed out, replay protection rejects it, which is correct. The payout is now in the unknown state and needs reconciliation against the provider using the idempotency key. Re-presenting authority does not recover it.

Provider idempotency has limits of its own. It applies only within the provider's key namespace, it typically requires the repeated request's parameters to match the original, and keys are retained for a limited time. [Stripe's idempotency documentation](https://docs.stripe.com/api/idempotent_requests), for example, says keys may be pruned once they are at least 24 hours old, after which a reused key is treated as a new request. Do not treat a provider's idempotency key as indefinite deduplication.

Using the payout ID as the idempotency key binds it to "this accepted operation." Distinguish retrying that operation from authorizing a genuinely new payment attempt. Whether a confirmed failure permits another attempt under the same identity depends on the provider's documented retry semantics and the application's recovery policy. Some providers, Stripe among them, allow a request to be retried under the original key when execution never began, for example after a validation failure. A genuinely new payment attempt needs an explicitly authorized operation or attempt identity, and in this design a new decision and grant. Never change the idempotency key merely to get past an earlier outcome that has not been resolved.

Neither replay protection nor idempotency gives exactly-once execution against an external system. They narrow the windows where duplication or loss can occur, and the unknown state makes the remaining window visible.

Some operations legitimately need **bounded use** rather than single use. A grant to download a generated export might allow three retrievals within 24 hours. The same rules apply: the bound is explicit, consumption is atomic, and the count is enforced at the host, not by the presenter.

---

## When You Do Not Need an Execution Grant

Everything above adds an issuer, a grant store or verification keys, consumption state, cancellation, and evidence. That cost is justified only when the grant actually narrows authority across a boundary. Several common designs do not need one.

### Immediate execution in the same trusted host

If the operator clicks **Pay now** and the Finance API authorizes the request and calls the payments code in the same process, there is no handoff. The decision and the side effect happen together, under the same trust boundary, with current state.

```text
Authenticated operator
    → authorize the request against the current payout
    → execute in the same host
```

Minting a grant for the host to hand back to itself adds an issuer, a validator, and a store without narrowing anything. Authorize and execute.

### System-owned work

A nightly reconciliation job that matches bank statements to payouts acts on behalf of the system, not on behalf of any user. No user decision needs to be preserved. The job should run under its own workload identity with service authorization limited to what reconciliation requires.

### A worker that can re-authorize safely

A background worker can sometimes make a fresh decision from durable state:

```text
Request creates a job: { payoutId }
    → Worker loads the current payout
    → Worker applies current policy under its own service identity
    → Worker executes
```

If the worker runs in the same trust boundary as the data, holding standing permission for its job type is acceptable, and the business rule does not depend on preserving a specific earlier decision, this is simpler than a grant and equally correct.

### Authenticated service-to-service calls

If the payments service authenticates the calling workload and evaluates current authorization for each request, ordinary service authorization may be enough. Authenticated or signed messages are a good complement: they prove origin and integrity. They become insufficient only when the message itself is being treated as the authority to act.

### When a grant earns its place

An execution grant is worth its cost when one or more of these is true:

- The executor **should not hold standing authority** for every operation of its kind. A worker with permanent "send any payout" permission is a high-value target. A worker that can send only payouts it holds unexpired, unused grants for limits the damage if it is compromised.
- The host **cannot reconstruct the original decision** on its own, because it sits in a different service or trust boundary from where the decision was made.
- A **specific earlier decision must be preserved**: a user's consent, an approval, or an accepted proposal that the later executor must not broaden.
- You need **evidence linking** a later side effect to the decision and actor that justified it.

If none of these apply, prefer the simpler design. The question is never "is there a queue?" It is "what exact authority should the later executor have, and does its ordinary identity already express that safely?"

The [roles, claims, and capability token selection guide](roles-claims-or-capability-token-dotnet.md) works through this choice across several scenarios.

---

## Failure Modes

**1. Forwarding the user's bearer token to the worker.** The worker gains whatever scope and audience that token carries, for its lifetime, and stored refresh tokens can extend usable authority well beyond it.

**2. Treating possession of a queued message as authority.** Anyone who can write to the queue can cause the side effect. Message authentication proves who sent it, not what they were allowed to ask for.

**3. Assuming the claim transaction freezes every policy fact.** An atomic consume-and-claim protects the states in its conditions. Vendor holds, account verification, limits, and the originating actor's authority need their own locking, versioning, reservation, or documented freshness rule.

**4. Issuing a reusable grant for a one-time operation.** A duplicated message or a retry can present it again. One-time operations need one-time grants, consumed atomically at the host.

**5. Omitting a binding.** A grant without an audience can be replayed at another service. Without an operation, it can be used for a different action. Without a resource or fingerprint, it can execute a different or altered payout. Without an expiry, it remains usable indefinitely.

**6. Letting the worker expand the scope.** The worker should present the grant, not mint or widen one. If the worker can request "the same, but for P-1043," the issuer has become a service that hands out authority on demand.

**7. Validating the signature and expiry, and nothing else.** A genuine, unexpired grant can still be replayed, cancelled, or bound to a payout that has since changed. Replay state, cancellation, current resource state, and current policy are separate checks.

**8. Minting a grant where immediate execution would do.** When the same trusted host can authorize and execute with current state, a grant adds infrastructure without reducing authority.

---

## Evidence: Linking the Actor, Decision, Grant, Executor, and Side Effect

When someone asks on Monday why vendor `V-311` received 18,400.00 EUR, the answer should be a chain of records, not a reconstruction from logs:

```text
Decision D-7781    operator O-17 authorized to schedule P-1042 (Tuesday),
                   payout fingerprint F, policy version 12
      ↓
Grant G-5503       issued from D-7781: payout.execute, P-1042, fingerprint F,
                   payout-worker, payments-service, Friday 09:00–12:00 UTC, 1 use
      ↓
Presentation       payout-worker presented G-5503 at 09:00:04 UTC (Friday)
      ↓
Host decision      fingerprint matched, vendor not on hold, account verified,
                   limits satisfied under policy version 13, grant consumed
      ↓
Side effect        bank reference B-99120, idempotency key P-1042, status Completed
```

Each link points to the one before it. The original decision anchors the operator's identity, and the presentation anchors the worker's. Later records refer back to them rather than having one identity stand in for the other.

A single log line written after the payment is a description of what happened. The chain above is evidence of why it was allowed. [Your Audit Log Records the Story, Not the Decision](your-audit-log-is-not-evidence.md) explains the difference in more depth.

---

## A Short Review Checklist

**Before issuing**

1. Is a grant needed at all, or can the same host execute immediately, or the worker re-authorize under its own identity?
2. Is the grant issued only after the actor was authorized and the exact operation was recorded?
3. Are its bindings derived from the accepted operation rather than copied from the user's token or claims?

**The grant**

4. Does it bind operation, resource, executor, audience, time window, and use count?
5. Does the resource fingerprint use a versioned, canonical encoding of every execution-relevant value?
6. Is the executor bound by a stable workload identifier rather than a display name?
7. Does it reference the decision that justified it, without carrying the user's standing authority?
8. Is its lifetime proportional to the delayed operation, not to a session or an arbitrary default?

**At the host**

9. Does the host take the executor's identity from transport authentication, never from the message?
10. Does it check current resource state and current policy, not only the signature and expiry?
11. Is the rule for an originating actor who lost authority written down and enforced?
12. Are grant consumption and the resource state transition one atomic step behind a real transactional boundary, with the window checked against authoritative current time inside that transition?
13. For policy facts outside that transaction, is the freshness rule explicit?
14. Does every pre-execution failure, including unavailable dependencies, stop before the side effect?
15. Is the side effect idempotent on the operation's identity, within the provider's documented limits, and is an unknown outcome reconciled rather than retried with the same authority?
16. Can stranded `Executing` attempts and failed finalization writes be recovered?

**Afterward**

17. Can you trace a side effect back through the host decision, the grant, and the original authorization decision?

---

## Continue Deeper

For the complete lifecycle of a scoped execution grant, including subject, audience, time, policy, and acknowledgment scope, issuance after the decision, validation near execution, and the failure modes of each binding, continue with [Scoped Capability and Host-Owned Execution](../../tutorials/scoped-capability-and-host-owned-execution.md).

If you are still deciding whether you need a grant at all, [Do You Need a Capability Token, or Are Roles and Claims Enough?](roles-claims-or-capability-token-dotnet.md) compares roles, claims with resource authorization, and separately issued capabilities with worked scenarios. [Role-Based, Claims-Based, and Capability-Based Authorization](../../architecture/role-based-claims-based-and-capability-based-authorization.md) is the deeper architecture comparison behind it, including standing versus continuation authority.

For atomic consumption, racing consumers, process restarts, replay store failure, and the difference between replay resistance and exactly-once execution, read [Replay Protection and Bounded-Use Authority](../../security/replay-protection-and-bounded-use.md), then run the [Replay Protection and Bounded Use sample](https://github.com/AsiBackbone/Learning/blob/main/samples/replay-protection-and-bounded-use/README.md) to watch second uses and racing consumers being rejected.

If the delayed operation originates from an AI agent or tool call rather than a person, [Agent and Tool Authorization Models and Host-Owned Execution](../../architecture/agent-and-tool-authorization-models-and-host-owned-execution.md) covers delayed execution, policy changes after a proposal, and where credentials should live.

If the payout also needs a reviewer's approval before it runs, [Authorization vs. Approval vs. Acknowledgment: Which Decision Do You Actually Have?](authorization-vs-approval-vs-acknowledgment.md) separates those decisions from the permission to execute.

### Related Work

These references address related aspects of the problem:

- [OAuth 2.0 Token Exchange (RFC 8693)](https://www.rfc-editor.org/rfc/rfc8693.html) standardizes obtaining a separate, audience- and scope-targeted token for delegation and impersonation.
- [OAuth 2.0 Security Best Current Practice (RFC 9700)](https://www.rfc-editor.org/rfc/rfc9700.html) covers audience restriction, sender-constrained tokens, and refresh-token protection.
- The IETF OAuth working group's [Transaction Tokens](https://datatracker.ietf.org/doc/draft-ietf-oauth-transaction-tokens/) draft propagates user, workload, and authorization context through a call chain inside a trusted domain, using very short-lived tokens.
- [Possession Is Not Authority: Execution Handle, Sink Verification, Atomic Consumption, and Finality Receipt](https://datatracker.ietf.org/doc/draft-das-execution-handle/) is an individual Internet-Draft that binds authority to a specific pending operation and consumes it at the point of effect. Individual drafts are not endorsed by the IETF.

None of these, on its own, decides which policy facts the executing host must recheck, or what happens when the originating actor loses authority. Those remain application decisions.

---

## The Short Answer

Do not hand a later executor the user's credential. The user's authorization answers whether they could ask, at the time they asked.

When work genuinely crosses a time or trust boundary, issue the executor only what it needs: one operation, one resource, one audience, one window, and a bounded number of uses, linked to the decision that justified it. Then let the host that owns the side effect check current state and current policy, consume the grant atomically, and record the result.

When the same trusted host can authorize and execute immediately, or a worker can make a fresh decision under its own identity, skip the grant. The goal is the narrowest authority that is actually needed, not more tokens.
