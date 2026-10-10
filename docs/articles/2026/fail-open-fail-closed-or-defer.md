---
description: Learn why an unavailable authorization decision is not a denial, and how to choose deferral, bounded degraded mode, or rejection per operation.
title: Should Authorization Fail Open, Fail Closed, or Defer?
author: Christopher D. Cavell
published: "2026-10-10"
summary: A protected side effect should not execute without a valid authorization decision, but an unavailable decision is not an explicit denial. Keep the secure state while making deferral, bounded degraded operation, escalation, and recovery deliberate per operation.
feed: true
x_hashtags:
  - DotNet
  - CyberSecurity
---

# Should Authorization Fail Open, Fail Closed, or Defer?

**Pattern classification:** General learning material

**Difficulty:** Advanced

**Prerequisites:** No formal prerequisites. Familiarity with ASP.NET Core endpoints, `HttpClient`, and calling a remote authorization or policy service is helpful, but no AsiBackbone package, policy engine, resilience library, or prior Learning material is required.

**Sample scope:** The C# targets .NET 8 or later and uses `Microsoft.Extensions.Http.Resilience` for transport resilience. Stores, the policy client, and the delivery service are illustrative interfaces, not a runnable sample; the snippets show where each responsibility lives, not a complete implementation.

**What this article covers:** why an explicit denial and an unavailable decision are different facts even though both stop immediate execution; the one invariant every outage behavior must preserve; why timeouts, retries, circuit breakers, and fallbacks cannot manufacture authorization; how to choose a per-operation outage treatment from consequence, reversibility, urgency, freshness, and recoverability; when a last-known-good local policy may be used, and the bounds it needs; what to do when an external side effect may already have happened; what to record and reconcile after recovery; and how to test that nothing protected executes while authority is unavailable.

A protected operation asks a remote service whether it may proceed. The service is slow, then silent. The code that called it now has to do *something*, and whatever it does is an authorization decision, whether anyone designed it or not.

Most teams answer with a slogan. "Fail closed" sounds right, and usually is the right *starting* point. But it hides three questions the slogan cannot answer: what the caller is told, what happens to work that was already in flight, and whether every operation behind the same dependency deserves the same treatment.

> **A protected side effect should not execute without a valid authorization decision, but an unavailable decision is not the same fact as an explicit denial. Preserve the secure state while making deferral, bounded degraded operation, escalation, and recovery deliberate, operation-specific, observable, and testable.**

The opposite mistake matters too. Not every system needs a degraded mode, a deferral queue, or a cached policy. For many operations, the right outage behavior is a plain, honest "unavailable, try again later," and adding machinery beyond that buys new failure modes. Near the end, this article covers when that simple answer is enough.

---

## The Example: A Customer Export During a Policy Outage

The rest of the article uses one system.

A customer-data platform lets business tenants build customer segments and export them to delivery destinations, such as a mailing house's SFTP server. The **export API** is an ASP.NET Core service. It owns segments, export records, and the credentials for each destination, and it is the only component that can send customer data out of the platform.

Before an export runs, the export API asks a remote **policy service**, owned by the privacy and security team, whether it may proceed. The policy service checks facts the export API does not own: whether the requester's role permits exports for this tenant, whether the tenant's data-processing agreement covers the destination's jurisdiction, and, from a separate **consent store**, whether the segment includes people who withdrew marketing consent.

On a Tuesday morning:

| When (UTC) | What happens |
| --- | --- |
| 09:08 | Export `EX-5302` is evaluated, receives `Allowed` under policy revision 41, and starts uploading to `dest-mailhouse-de` |
| 09:12 | The consent store's latency rises sharply. The policy service starts timing out on export decisions |
| 09:14 | An analyst at tenant `retailer-eu` requests export `EX-5310` of segment `lapsed-buyers-q3` to `dest-mailhouse-de`. The decision call times out, the retry times out, and the circuit breaker opens |
| 09:16 | The `EX-5302` upload connection drops after the final bytes were sent but before the server confirmed the file. The delivery worker's retry policy wants to resend |
| 09:20 | A tenant administrator opens the export history page, which also asks the policy service whether they may view it |
| 10:05 | The consent store recovers, the policy service returns to normal, and the circuit closes |

Three operations hit the same outage. They should not all be treated the same way, and none of them should be treated the way a single `catch` block would treat them.

---

## Two Different Facts That Both Stop Execution

At 09:14, the export API does not know whether the analyst may export `lapsed-buyers-q3`. That is a different fact from knowing that they may not.

An **explicit denial** is an answer. The policy service evaluated current inputs under a known policy version and concluded *no*. It has a reason code. Retrying the same request will produce the same answer until a fact or rule changes. The honest response to the caller is "forbidden," and the honest record is "denied by revision 41 because of `consent.withdrawn-members`."

An **unavailable or undeterminable decision** is the absence of an answer. The policy service timed out, the circuit is open, the response was malformed, or a required input such as consent status could not be obtained or was older than its allowed age. Nothing has concluded that the analyst lacks authority. The honest response is "not now," and the honest record is "no decision: `pdp.timeout`."

Both prevent the export from running at 09:14. They differ in everything that follows:

| | Explicit `Denied` | `CannotDetermine` |
| --- | --- | --- |
| What is known | Current policy forbids this operation | No valid decision could be obtained |
| Who produced it | The policy, from current inputs | The host, because the policy could not answer |
| Typical HTTP response | `403 Forbidden` | `503 Service Unavailable` with `Retry-After`, or `202 Accepted` if the host deliberately defers the work |
| Retrying unchanged | Pointless; the answer will not change | Reasonable, later, through the same check |
| Queue for later | No | Possibly, under a bounded deferral rule |
| User-facing message | "You are not permitted to export this segment" | "Exports are temporarily unavailable; your request was not sent" |
| Operational signal | Normal; possibly a security signal if frequent | An incident; alert the dependency's owner and the team that owns the policy |
| Evidence | Decision, reason, policy version | Cause of unavailability, outage treatment applied, no policy version |

The two `CannotDetermine` responses mean different things to the caller. `202 Accepted` means the host has kept the request and taken responsibility for re-checking it later, so the caller should follow the status link rather than resubmit. `503 Service Unavailable` with `Retry-After` means the host kept nothing, and the caller must try again later. Choose one per operation, as described below, and never return `202` for work the host has not durably recorded.

Collapsing them in either direction does damage. Converting `CannotDetermine` into `Allowed` executes protected work without authority. Converting it into `Denied` tells a legitimate user they lack a permission they hold, hides an outage behind ordinary security noise, teaches support staff to "fix" access that was never broken, and makes the audit trail claim that a policy evaluated something it never saw.

Some policy services also return an explicit **deferred** outcome as a real answer, such as "allowed only after a second approval" or "not inside a change window." That is still a decision produced by the policy. This article uses *deferral* for something else: the host's choice of what to do with a decision it could not obtain. Keep the two apart in code, even if both end up as a waiting request.

---

## What "Deny by Default" Actually Requires

The [OWASP Authorization Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html) recommends denying access by default: when no rule explicitly grants access, the application must not proceed. The [OWASP Authorization Patterns Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Patterns_Cheat_Sheet.html) applies the same principle to remote decision points, recommending that errors and timeouts deny protected operations and that a previously loaded policy be used only within defined freshness requirements.

NIST's glossary defines [fail secure](https://csrc.nist.gov/glossary/term/fail_secure) as "a mode of termination of system functions that prevents loss of secure state when a failure occurs or is detected in the system." The secure state is the point. It does not say the failure must be reported as a judgment about the subject.

Read together, those sources set an invariant, not a vocabulary:

> **No protected side effect executes without valid authority for that operation.**

"Valid authority" usually means a current `Allowed` decision. It can also mean a local evaluation that was designed and authorized in advance for exactly this situation, inside documented bounds, as described later. It never means a timeout, an exception, an empty response, or a value a fallback handler produced.

Everything else is open to design: what the caller is told, whether the request is kept, who is notified, and how the work resumes. "Fail closed" names the invariant. It does not decide those other questions, and treating it as though it did is how every outage ends up reported as a permanent `403`.

The [Open Policy Agent operations guidance](https://www.openpolicyagent.org/docs/operations) makes the ownership explicit: when the policy engine cannot answer, the software asking for the decision must decide, and the right choice depends on factors such as the likelihood of no decision and the cost of allowing or denying incorrectly. OPA cannot answer that question for you, and neither can a resilience library.

---

## Resilience Is Transport, Not Authority

The export API calls the policy service through an `HttpClient` with a standard resilience pipeline:

```csharp
builder.Services
    .AddHttpClient<IPolicyClient, RemotePolicyClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["Policy:BaseAddress"]!);
    })
    .AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(800);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(2);
        options.Retry.MaxRetryAttempts = 1;
    });
```

That pipeline is a good idea. The standard handler combines rate limiting, timeouts, retries, and a circuit breaker, and each of those parts answers a transport question:

- **Timeouts** answer *how long do we wait for this call?*
- **Retries** answer *should we ask the same question again?* Retrying the question is safe only when asking it has no side effect. Here, the policy service's decision endpoint is documented as side-effect free: it evaluates and returns an answer, without reserving, consuming, or changing anything. The standard handler retries every HTTP method by default, including this `POST`. If your decision endpoint is not side-effect free, for example because it consumes a one-time approval or counts against a quota, call `options.Retry.DisableForUnsafeHttpMethods()` or give each decision request an idempotency key the service honors. Either way, retrying the question says nothing about retrying the protected operation.
- **Circuit breakers** answer *should we stop asking for a while because the dependency is unhealthy?*

The standard pipeline has no fallback strategy. Teams often add one, through a Polly fallback strategy or, more often, a `catch` block, and a **fallback** answers a fourth transport question: *what value do we return to our own caller when the call fails?*

None of them answers *may this analyst export this segment?* A resilience pipeline can make the policy service's answer arrive more reliably. It cannot become the answer.

The failure is usually one line in a catch block or a fallback handler:

```csharp
// Anti-pattern: a resilience path that manufactures authority.
catch (Exception)
{
    return PolicyDecision.Allow("pdp-unavailable"); // an outage is now a grant
}
```

The mirror image is subtler and more common:

```csharp
// Anti-pattern: an outage reported as a judgment about the user.
catch (Exception)
{
    return PolicyDecision.Deny("forbidden"); // true about the outcome, false about the reason
}
```

The client should report what actually happened, in a type that cannot be mistaken for a decision:

```csharp
public enum PolicyOutcome { Allowed, Denied }

public sealed record PolicyDecision(
    PolicyOutcome Outcome,
    string ReasonCode,
    long PolicyRevision,
    DateTimeOffset EvaluatedAt);

public abstract record AuthorizationResult
{
    // The policy service answered with a recognized, explicit outcome.
    public sealed record Decided(PolicyDecision Decision) : AuthorizationResult;

    // No valid answer. Cause is a stable code such as "pdp.timeout",
    // "pdp.circuit-open", "pdp.malformed-response", or "input.consent.stale".
    public sealed record CannotDetermine(string Cause) : AuthorizationResult;
}
```

```csharp
public sealed class RemotePolicyClient(HttpClient http) : IPolicyClient
{
    public async Task<AuthorizationResult> EvaluateAsync(PolicyRequest request, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("v1/decisions/customer-export", request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new AuthorizationResult.CannotDetermine($"pdp.http-{(int)response.StatusCode}");
            }

            // ReadFromJsonAsync does not check the media type; it will parse a JSON-shaped
            // body labeled text/html. The decision contract requires application/json.
            if (!string.Equals(
                    response.Content.Headers.ContentType?.MediaType,
                    "application/json",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new AuthorizationResult.CannotDetermine("pdp.unexpected-content-type");
            }

            PolicyResponse? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<PolicyResponse>(ct);
            }
            catch (JsonException)
            {
                // The body was not valid JSON for the decision contract.
                return new AuthorizationResult.CannotDetermine("pdp.malformed-response");
            }
            catch (InvalidOperationException)
            {
                // Among other causes, ReadFromJsonAsync throws this for an invalid charset.
                return new AuthorizationResult.CannotDetermine("pdp.malformed-response");
            }

            // The policy service reports its own missing or stale inputs explicitly,
            // with a stable cause such as "input.consent.stale".
            if (body?.IndeterminateCause is { Length: > 0 } cause)
            {
                return new AuthorizationResult.CannotDetermine(cause);
            }

            // Only an explicit, well-formed "allow" or "deny" is a decision. A missing,
            // undefined, or unrecognized result is not permission, and it is not a
            // judgment about the subject either.
            return body?.TryToDecision() is { } decision
                ? new AuthorizationResult.Decided(decision)
                : new AuthorizationResult.CannotDetermine("pdp.malformed-response");
        }
        catch (BrokenCircuitException)
        {
            return new AuthorizationResult.CannotDetermine("pdp.circuit-open");
        }
        catch (TimeoutRejectedException)
        {
            return new AuthorizationResult.CannotDetermine("pdp.timeout");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // HttpClient.Timeout elapsed without the caller cancelling.
            return new AuthorizationResult.CannotDetermine("pdp.timeout");
        }
        catch (HttpRequestException)
        {
            return new AuthorizationResult.CannotDetermine("pdp.unreachable");
        }
        // Cancellation from the caller's own token propagates: the request was
        // abandoned, which is neither a decision nor a dependency outage.
    }
}
```

`BrokenCircuitException` and `TimeoutRejectedException` come from Polly, which `Microsoft.Extensions.Http.Resilience` uses. The point of the list is not the exact exception types, which depend on your stack. It is that every way the call can fail ends in `CannotDetermine` with a stable cause, and nothing except a well-formed explicit answer ends in `Decided`. Malformed responses are the cases most often missed. `ReadFromJsonAsync` throws `JsonException` for invalid JSON and `InvalidOperationException` for an invalid charset instead of returning `null`, so without their own `catch` blocks they escape the result model entirely. It also does not check the media type, which is why the client checks for `application/json` itself before reading.

The policy service follows the same rule for its own inputs. When the consent store is down or its replica is older than the allowed bound, the policy service does not evaluate "consent: unknown" as "consent: present." It reports that it cannot determine the outcome, and the export API receives `CannotDetermine("input.consent.stale")`. The [OWASP Authorization Policy and Data Distribution Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Policy_And_Data_Distribution_Cheat_Sheet.html) makes the same point about missing, invalid, or stale attributes.

If you implement the remote check as an ASP.NET Core [authorization handler](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0), the same discipline applies: a non-decision must never call `context.Succeed`. By default, when an authenticated user fails a requirement, the authorization middleware calls `ForbidAsync`, and the authentication handler decides what that looks like: `403 Forbidden` for JWT bearer authentication, or a redirect to the access-denied page for cookie authentication. Either way the caller sees "forbidden," which collapses the distinction this article is about. Either attach an `AuthorizationFailureReason` and map it in a custom `IAuthorizationMiddlewareResultHandler`, or, as in this example, keep coarse checks such as "authenticated tenant member" in ASP.NET Core policies and make the operation-specific remote decision in the handler that already loads the segment and destination.

---

## The Decision Path

The export API handles every protected request through the same short path:

```text
request reaches the export API (the protected host)
    → authoritative policy inputs are resolved from host-owned records
    → policy decision requested
    → Allowed / Denied / CannotDetermine
    → host applies the operation's outage rule, only for CannotDetermine
    → execute, defer, escalate, or reject
    → record evidence; reconcile after recovery
```

Two properties matter more than the shape.

**The host owns the outage rule.** The analyst's browser, the delivery worker, and the request body cannot choose degraded mode, request a fallback, or declare an operation low-risk. The rule is configuration the host loads, reviewed by whoever owns the policy, and versioned like policy.

**The outage rule is consulted only when there is no decision.** An explicit `Denied` is final for this attempt; no outage treatment can soften it. An explicit `Allowed` proceeds through the normal execution path. Outage rules exist for the gap between them.

```csharp
public enum OutageTreatment { Reject, Defer, Escalate, EvaluateLocally }

public sealed record OperationOutageRule(
    string Operation,
    OutageTreatment Treatment,
    TimeSpan? MaxPolicyAge = null,       // EvaluateLocally only
    long? MinimumPolicyRevision = null,  // EvaluateLocally only
    TimeSpan? MaxDeferral = null);       // Defer only

public static class OutageRules
{
    public const string Version = "export-outage-rules/2026-10-01";

    private static readonly FrozenDictionary<string, OperationOutageRule> Rules =
        new OperationOutageRule[]
        {
            new("export-history.read",         OutageTreatment.EvaluateLocally,
                MaxPolicyAge: TimeSpan.FromMinutes(30), MinimumPolicyRevision: 41),
            new("customer-segment.export",     OutageTreatment.Defer,
                MaxDeferral: TimeSpan.FromHours(4)),
            new("data-subject-access.export",  OutageTreatment.Escalate),
            new("export-destination.register", OutageTreatment.Reject),
        }.ToFrozenDictionary(r => r.Operation);

    // An operation nobody classified gets the most conservative treatment.
    public static OperationOutageRule For(string operation) =>
        Rules.TryGetValue(operation, out var rule) ? rule : new(operation, OutageTreatment.Reject);
}
```

The handler for the 09:14 export:

```csharp
public sealed class CustomerExportHandler(
    IExportInputResolver inputs,
    IPolicyClient policy,
    IDecisionLog decisions,
    IExportStore exports,
    IDeferredExports deferred,
    TimeProvider clock)
{
    private const string Operation = "customer-segment.export";

    public async Task<ExportResult> RequestAsync(
        string segmentId, string destinationId, ClaimsPrincipal user, CancellationToken ct)
    {
        // Tenant, segment classification, and destination jurisdiction come from the
        // export API's own records. The request supplies identifiers, not facts.
        var resolved = await inputs.ResolveAsync(segmentId, destinationId, user, ct);
        if (resolved.Rejection is { } rejection)
        {
            await decisions.RecordRejectedAsync(Operation, rejection, ct);
            return ExportResult.Rejected(rejection);
        }

        var input = resolved.Input;
        var result = await policy.EvaluateAsync(input.ToPolicyRequest(Operation), ct);

        switch (result)
        {
            case AuthorizationResult.Decided { Decision.Outcome: PolicyOutcome.Allowed } allowed:
            {
                var decisionId = await decisions.RecordAsync(input, allowed.Decision, ct);
                // Atomic claim, then delivery; covered later under ambiguous side effects.
                return await exports.StartAsync(input, decisionId, ct);
            }

            case AuthorizationResult.Decided { Decision.Outcome: PolicyOutcome.Denied } denied:
                await decisions.RecordAsync(input, denied.Decision, ct);
                return ExportResult.Denied(denied.Decision.ReasonCode);

            case AuthorizationResult.CannotDetermine unavailable:
            {
                var rule = OutageRules.For(Operation);
                await decisions.RecordUnavailableAsync(input, unavailable.Cause, rule, OutageRules.Version, ct);

                return rule.Treatment switch
                {
                    OutageTreatment.Defer => await deferred.AcceptAsync(
                        input, unavailable.Cause, expiresAt: clock.GetUtcNow() + rule.MaxDeferral!.Value, ct),
                    // Export never uses local evaluation, so any other treatment
                    // means "not now" without keeping the request.
                    _ => ExportResult.Unavailable(unavailable.Cause),
                };
            }

            default:
                // A result type this code does not recognize is not permission.
                return ExportResult.Unavailable("authorization.unrecognized-result");
        }
    }
}
```

At 09:14, `EX-5310` is recorded as `AwaitingAuthorization`, the analyst receives `202 Accepted` with a status link and the message "your export was not sent; it will be re-checked when authorization is available," and nothing leaves the platform.

---

## Choosing an Outage Treatment per Operation

The four entries in `OutageRules` are not arbitrary. They come from asking the same questions about each operation, before any outage happens.

| Question | Points toward rejecting or deferring | Points toward bounded local evaluation |
| --- | --- | --- |
| **Consequence of an incorrect allow:** what happens if this executes for someone who should have been denied? | Data leaves the platform, money moves, records are destroyed | A user briefly sees metadata they could see an hour ago |
| **Reversibility:** can the effect be undone? | No; a disclosure cannot be recalled | Yes, or there is no lasting effect |
| **Consequence of an incorrect deny:** what does a delay or refusal cost? | An inconvenience; the work can wait | Safety, a legal deadline, or a core function stops |
| **Urgency:** can it wait for recovery? | Yes, within a stated bound | No, and a human with independent authority is available |
| **Freshness sensitivity:** how quickly must a rule or attribute change take effect? | Consent withdrawal or revocation must apply at once | The relevant rules and attributes change rarely |
| **Recoverability:** can deferred work be resumed safely later? | Yes, by re-running the full decision | Not applicable |
| **Scope:** does the decision depend on tenant or jurisdiction facts that only the remote source holds? | Yes | No; the needed facts are local and authoritative |

Applied to the example:

- **`customer-segment.export` → Defer.** An incorrect allow is an irreversible disclosure that may include people who withdrew consent, which is exactly the fact that is unavailable. An incorrect deny costs a few hours. The request is kept so the analyst does not have to resubmit, but only for four hours, after which it expires.
- **`export-destination.register` → Reject.** Registering a new external destination is high impact and never urgent. Keeping the request adds state nobody needs; the administrator can retry after recovery.
- **`data-subject-access.export` → Escalate.** A person's request for their own data can have a legal deadline; for this EU tenant, Article 12(3) of the GDPR requires a response without undue delay and generally within one month. Waiting indefinitely is itself a harm, but the normal policy path cannot answer. The request goes to the designated privacy officer, who has their own authority and records their own decision. Escalation is not a way to skip the check; it routes the decision to a different, deliberately designed authority. That authority needs written decision rules of its own, such as what the officer must verify about the requester and the data before approving, and its decisions go into the same evidence trail as the policy service's. Without both, escalation becomes an ad hoc approval that nobody can review afterward. [Escalation Patterns in Governed Systems](../../governance/escalation-patterns-in-governed-systems.md) covers how to design that path.
- **`export-history.read` → Evaluate locally.** Viewing a list of past exports is read-only, discloses only metadata, depends on rules that change rarely, and needs no attribute from the consent store. It may continue under a local policy snapshot, within the bounds below.

There is no universal rule here. A different organization might reject exports outright instead of deferring them, or decide that history pages simply show "temporarily unavailable." What matters is that the treatment was chosen per operation, written down, and reviewed, rather than produced by whichever exception handler ran first.

---

## When a Last-Known-Good Policy May Be Used

Bounded local evaluation is the option most easily abused, because it looks like the system is still making decisions. It is never risk-free: the snapshot may miss a rule change or revocation that happened after it was last confirmed current. It is a defensible, accepted risk only when the policy owner designed and authorized degraded operation in advance, decided that this residual risk is acceptable for the listed operations, and made every one of these bounds explicit:

- **Operation scope.** A fixed list of operations may use it. Here, one: `export-history.read`. The list is part of the outage rules, not something a caller requests.
- **Version bound.** The local policy must be at or above a minimum revision. If an emergency change ships revision 42 to close a gap, the minimum moves to 42 and older snapshots stop qualifying.
- **Freshness bound.** The snapshot's age is measured from the last time the host confirmed it was current, not from when the outage started. A snapshot that silently stopped syncing three days ago is not "last-known-good" for an outage that began five minutes ago.
- **Input bound.** Every attribute the local policy needs must also be available locally and within its own freshness limit. A fresh policy evaluated against stale or missing attributes is not a fresh decision.
- **Integrity.** The snapshot was verified when it was activated, through the same channel as normal policy distribution.
- **Time bound on degraded mode itself.** Degraded operation ends when the freshness bound is exceeded, even if the outage has not.

```csharp
public sealed record LocalPolicySnapshot(
    string PolicyId,
    long Revision,
    string Digest,
    bool Verified,                          // digest and signature checked at activation
    DateTimeOffset LastConfirmedCurrentAt); // advanced only by a successful, verified sync

public static class LocalEvaluationGate
{
    public static bool IsUsable(
        LocalPolicySnapshot snapshot, OperationOutageRule rule, DateTimeOffset now) =>
        rule.Treatment == OutageTreatment.EvaluateLocally
        && snapshot.Verified
        && rule.MinimumPolicyRevision is { } minimum && snapshot.Revision >= minimum
        && rule.MaxPolicyAge is { } maxAge && now - snapshot.LastConfirmedCurrentAt <= maxAge;
}
```

When the gate passes, the local evaluation produces a real `Allowed` or `Denied` decision, recorded as a degraded-mode decision with the snapshot's revision, digest, and age. When the gate fails, the operation is treated exactly as if no local policy existed: `CannotDetermine`, then the operation's fallback treatment, which for a read is usually "temporarily unavailable."

At 09:20, the tenant administrator sees their export history, evaluated locally under revision 41 confirmed current at 09:11. At 09:42, the snapshot passes its 30-minute bound, and the same page starts returning "temporarily unavailable." That transition is designed behavior, and it should raise an alert, not surprise anyone.

A local snapshot is policy, not a cached decision. Reusing an earlier `Allowed` for the same user and operation is a different and riskier technique, because it repeats a conclusion about inputs that may have changed. [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../../architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md#6-policy-caching-and-decision-caching-are-different) explains the difference.

---

## Deferral Is Not a Promise to Execute

`EX-5310` is now `AwaitingAuthorization`. That record keeps the analyst's request. It does not carry any authority.

When the policy service recovers, a re-evaluation worker picks up deferred exports. For each one, it asks the export API to run the same path again: resolve current inputs, request a current decision, act on the answer. The worker's identity is permitted to ask for re-evaluation. It cannot select an outage treatment, mark an export as authorized, or reach the destination credentials.

```text
for each deferred export, oldest first, at a controlled rate:
    if now > expiresAt          → mark Expired; notify requester; never execute
    result = export API re-evaluates (exportId)   // same resolver, same policy, current facts

    Allowed           → claim and deliver once; record decision
    Denied            → mark Denied with the current reason; notify requester
    CannotDetermine   → leave deferred; try again later
```

Three consequences follow:

- **The re-evaluation can be a denial.** If one of the segment's members withdrew consent at 09:30, the 10:05 evaluation sees it. The request was accepted at 09:14; permission is decided at execution.
- **Deferral needs an end.** Without `expiresAt`, a queue of deferred requests becomes a queue of executions that happen at an unpredictable time, for people who may no longer expect them.
- **Recovery is not a flood.** Re-evaluating a backlog at full speed against a policy service that just recovered can cause the next outage.

---

## When the Side Effect May Already Have Happened

`EX-5302` is a different problem. It received a valid `Allowed` at 09:08. At 09:16, the upload connection dropped after the last bytes were sent and before the SFTP server confirmed the file. The export API does not know whether the mailing house has the file.

The delivery worker's retry policy treats the dropped connection as a transient error and wants to send again. Retrying the *question* to a policy service is harmless. Retrying the *delivery* is not:

- **It may be a second disclosure.** If the first file arrived, a resend sends the same customer data again, possibly under a different name, possibly to a partner process that ingests both.
- **The original decision covered one delivery.** A resend is a new attempt. Whether it is still permitted is a current question, and at 09:16 the current answer is `CannotDetermine`.
- **The resilience layer cannot see either issue.** It sees a failed network call.

The correct state is neither "failed" nor "completed." It is **indeterminate**, and an indeterminate external side effect stops automatic execution:

```text
delivery attempt ended without confirmation
    → mark execution Indeterminate (not Failed, not Completed)
    → stop automatic retries for this export
    → reconcile: ask the destination what it has
          file present with the expected checksum → mark Completed
          file absent                              → mark NotDelivered; a re-delivery is a
                                                     new attempt that needs a current decision
          cannot tell                              → escalate to an operator
```

Automatic retry is safe only when the destination makes it safe: when it accepts an idempotency key, deduplicates by a deterministic identifier and checksum, or offers an atomic commit that the host can query. Many external destinations, including plain SFTP, do not. Where they do not, the host has to find out what happened before it acts again, and during an authorization outage it cannot get permission to act again anyway.

The same claim that prevents duplicate execution in normal operation still applies: the export is claimed atomically before delivery, under a unique execution ID, and a later attempt reads that claim instead of starting a second delivery. [When Should a Workflow Engine Own the Decision?](when-workflow-engine-should-own-decision.md#retries-timers-and-compensation) covers that claim and why an unknown outcome is not a reason to execute again.

---

## A Compact Decision Matrix

| Situation | Decision fact | Expected treatment |
| --- | --- | --- |
| Explicit current denial | `Denied` | Deny (`403`); record the reason and policy version; do not queue or retry |
| Explicit current allow | `Allowed` | Claim atomically, execute once, record the decision |
| Decision dependency unavailable; irreversible high-impact operation | `CannotDetermine` | Defer or reject without executing; record the cause and the outage rule applied |
| Required attribute missing or older than its bound | `CannotDetermine` | Same as an unavailable dependency; never substitute a default attribute value |
| Preauthorized bounded degraded mode with fresh, versioned local policy | Remote `CannotDetermine`; local `Allowed` or `Denied` | Evaluate locally within the documented bounds; record as a degraded-mode decision |
| Local policy exists but is past its age or below its minimum revision | `CannotDetermine` | Treat as unavailable for that operation |
| Urgent operation with a hard deadline | `CannotDetermine` | Escalate to a designated independent authority; record their decision |
| External side effect may already have occurred | Earlier `Allowed`; current state unknown | Stop automatic execution and reconcile before any retry; a retry is a new attempt needing a current decision |
| Operation with no outage rule | `CannotDetermine` | Reject |

---

## Evidence During and After an Outage

Every request handled during the outage should leave a record that explains what happened without reconstructing it from logs. For each attempt, capture at least:

- The operation, the requester, the tenant, and the resource identifiers.
- The result category: `Allowed`, `Denied`, or `CannotDetermine`, and for `CannotDetermine`, its cause, such as `pdp.timeout` or `input.consent.stale`.
- The disposition the host chose: executed, denied, deferred, escalated, rejected, or unavailable.
- The outage-rule version that chose it.
- For degraded-mode decisions, the local policy revision, digest, and age at evaluation, and an explicit degraded-mode flag.
- For deferred and escalated requests, their identifiers and expiry, and later, their final outcome.
- For executions, the claim's execution ID and the delivery outcome, including `Indeterminate`.
- When the host entered and left degraded mode for each operation.

Leave out the sensitive attributes themselves. A reason code such as `consent.withdrawn-members` is useful evidence; the list of members is not. [Your Audit Log Records the Story, Not the Decision](your-audit-log-is-not-evidence.md) explains why an ordinary log line rarely carries enough to reconstruct a decision.

Records explain an outage afterward; alerts make it visible while it is happening. Alert on the rate of `CannotDetermine` results, on entry into degraded mode, on deferred requests nearing expiry, and on indeterminate executions. Route those alerts to the team that operates the policy service and to the team that owns the policy, because an authorization outage is both an availability problem and a security-relevant change in how decisions are being made.

---

## Recovery and Reconciliation

The circuit closing at 10:05 does not end the incident. Normal service resumes safely only after the work the outage created is resolved:

1. **Re-evaluate deferred work** through the full decision path, at a controlled rate, expiring anything past its bound.
2. **Resolve indeterminate executions** such as `EX-5302` before any of them is retried.
3. **Close escalations** and record the outcome alongside the original request.
4. **Review degraded-mode decisions** against current policy. If a page view allowed locally at 09:20 would be denied under the current revision, that is a finding to report and investigate, not something to undo silently.
5. **Confirm the local snapshot** is current again, so the next outage starts from a fresh last-known-good.
6. **Record the exit** from degraded mode for each operation that entered it.

If any of these steps is skipped, the outage has not ended; it has only stopped being visible.

---

## Testing That Nothing Executes Without Authority

An outage policy that is not tested is an assumption. The tests that matter prove two things at once: the decision outcome the caller sees, and that the protected side effect did not happen.

```csharp
[Fact]
public async Task Export_is_deferred_and_never_delivered_when_policy_service_times_out()
{
    var policy = new StubPolicyClient(new AuthorizationResult.CannotDetermine("pdp.timeout"));
    var delivery = new RecordingDelivery();
    var handler = ExportHandlerFactory.Create(policy, delivery);

    var result = await handler.RequestAsync(
        "lapsed-buyers-q3", "dest-mailhouse-de", TestUsers.RetailerEuAnalyst, CancellationToken.None);

    Assert.Equal(ExportDisposition.Deferred, result.Disposition);
    Assert.Equal("pdp.timeout", result.Cause);
    Assert.Empty(delivery.Calls);
}

[Fact]
public async Task Explicit_denial_is_reported_as_denied_and_is_not_queued()
{
    var policy = StubPolicyClient.Denied("consent.withdrawn-members");
    var deferred = new RecordingDeferredExports();
    var delivery = new RecordingDelivery();
    var handler = ExportHandlerFactory.Create(policy, delivery, deferred);

    var result = await handler.RequestAsync(
        "lapsed-buyers-q3", "dest-mailhouse-de", TestUsers.RetailerEuAnalyst, CancellationToken.None);

    Assert.Equal(ExportDisposition.Denied, result.Disposition);
    Assert.Empty(deferred.Accepted);
    Assert.Empty(delivery.Calls);
}
```

Cover at least these cases:

- **Each failure cause maps to `CannotDetermine`:** timeout, open circuit, connection failure, non-success status, invalid JSON, a non-JSON content type even with a JSON-shaped body, an invalid charset, a well-formed body with an undefined or unrecognized result, an explicit indeterminate response from the policy service, and stale or missing required attributes. Also verify the opposite: a well-formed response labeled `Application/JSON` is still accepted, because media types are case-insensitive. Exercise the real `HttpClient` pipeline with a fake message handler, so the resilience configuration is part of what is tested.
- **No resilience path produces `Allowed`.** Include any fallback strategy in the pipeline under test.
- **Explicit `Denied` and `CannotDetermine` produce different HTTP results:** `403` for one, `503` or `202` for the other.
- **Every unavailable case records zero protected executions,** asserted on the component that performs the side effect, not on a status flag.
- **Local evaluation is refused** when the snapshot is past its age, below its minimum revision, unverified, or used for an operation not on the list.
- **An unclassified operation is rejected.**
- **An indeterminate delivery is not retried automatically,** and reconciliation is required before any new attempt.
- **A deferred request re-evaluated after recovery** can be denied by facts that changed while it waited, and an expired request never executes.
- **Callers cannot select outage behavior:** a request body, header, or worker message that names a degraded mode has no effect.

[How to Test That a Denied Operation Never Executes](test-denied-operation-never-executes.md) covers the side-effect assertions in more depth.

---

## Failure Modes

Each of these usually starts as a reasonable shortcut during an incident.

### 1. Converting a timeout or exception into `Allowed`

A `catch` block or fallback returns permission because availability was the priority that day. An outage now grants whatever the dependency would have denied. This is the one outage behavior that is not a trade-off.

### 2. Reporting every unavailable decision as an explicit denial

Users are told they lack permissions they hold, support teams start "fixing" access that was never broken, security dashboards fill with denials that are really an outage, and the audit trail records policy judgments the policy never made.

### 3. Treating a retry, circuit breaker, or fallback value as authorization

A circuit breaker's fallback returns a cached response object, and downstream code reads its `allow` field. The resilience layer has quietly become the authority. Keep resilience types and decision types separate, so a fallback can only ever produce `CannotDetermine`.

### 4. Using cached policy without bounds

The policy service is down, so the host keeps evaluating the last policy it loaded, with no maximum age, no minimum revision, and no operation restriction. Degraded mode is now unbounded, and nobody can say when it started.

### 5. Retrying a non-idempotent operation after the side effect may have occurred

The delivery fails ambiguously and the retry policy resends. The data is now disclosed twice, the second time without a current decision. Mark it indeterminate and reconcile first.

### 6. Letting a client or worker choose degraded mode

A request flag, a header, or a worker's configuration says `allowDegraded: true`. Whoever can set that flag now holds authority the policy owner never granted. Outage rules belong to the host and the policy owner.

### 7. Omitting degraded-mode state from evidence

The record says `Allowed` with no indication that it came from a 25-minute-old local snapshot during an outage. After recovery, nobody can find the decisions that deserve review.

### 8. Restoring normal service without reconciling

The circuit closes and everyone moves on. Deferred requests execute in an unplanned burst or sit forever, indeterminate deliveries are never checked, and degraded-mode decisions are never reviewed.

### 9. One universal outage rule for every operation

A single global switch sets fail-open or fail-closed for the whole service. Either the history page goes dark for no reason, or the export runs without a decision. Operations with different consequences need different rules.

---

## When the Simple Answer Is Enough

Many systems need much less than this article describes.

If the policy is evaluated in-process from code or configuration that ships with the application, there is no remote dependency to lose. ASP.NET Core's built-in policies and handlers have no outage mode of their own, and an outage policy is not needed for them. [Policy as Code in ASP.NET Core Without Overengineering](policy-as-code-aspnet-core-without-overengineering.md) describes when moving policy out of process is worth that new dependency in the first place.

If the decision is remote but the operations behind it can all simply wait, the complete design can be:

- Map every non-decision to `CannotDetermine`.
- Return `503 Service Unavailable` with `Retry-After`, and a message that says the request was not performed.
- Record the cause and alert the owner of the dependency.
- Test that nothing executes.

That design has no deferral queue, no local snapshot, no escalation path, and nothing to reconcile, and for many services it is exactly right. Add deferral when callers genuinely benefit from not resubmitting, local evaluation when a specific low-consequence operation must stay available and its bounds can be written down, and escalation when urgency and an independent authority both exist. Each one is a deliberate addition, not a sign of maturity.

What should not be simplified away is the distinction itself. Even the smallest design reports "unavailable" and "denied" as different things.

---

## A Short Review Checklist

**Semantics**

1. Does the policy client return a type in which a timeout, an open circuit, a malformed response, and an explicit denial are distinguishable?
2. Can any resilience path, including fallbacks, produce `Allowed`?
3. Do callers receive different responses for `Denied` and `CannotDetermine`?
4. Is an undefined, unrecognized, or missing-attribute result treated as `CannotDetermine` rather than as permission?

**Outage rules**

5. Does every protected operation have a written outage treatment, chosen from its consequence, reversibility, urgency, freshness sensitivity, and recoverability?
6. Is the outage rule owned by the host and the policy owner, versioned, and impossible for a caller or worker to select?
7. Do unclassified operations default to rejection?
8. If local evaluation is allowed, are its operation list, minimum revision, freshness bound, input bounds, and integrity check all explicit?

**Execution and recovery**

9. Does deferred work re-run the full decision at execution, with an expiry?
10. Is an ambiguous external side effect marked indeterminate, with automatic retry stopped until reconciliation?
11. After recovery, are deferred requests, indeterminate executions, escalations, and degraded-mode decisions all resolved and recorded?

**Evidence and tests**

12. Does each record capture the result category, cause, disposition, outage-rule version, and any degraded-mode details?
13. Do tests assert both the outcome and zero protected executions for every unavailable case?

---

## Continue Deeper

To practice this boundary hands-on, including classifying operations, keeping "cannot determine" as a real state, designing a bounded cached-policy mode, separating executor failure from governance denial, and adding recovery and reconciliation, work through [Safe Degraded Mode and Fail-Safe Governance](../../labs/safe-degraded-mode-and-fail-safe-governance.md).

If the question is where the policy engine should run, and how policy distribution, staleness, invalidation, and partition behavior shape your outage options, read [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../../architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md#fail-open-and-fail-closed-are-not-complete-strategies).

For modeling decision outcomes as explicit values with stable reason codes, rather than booleans or HTTP status codes, see [Policy Context and Explicit Decision Outcomes](../../tutorials/policy-context-and-explicit-decision-outcomes.md).

When an outage must route a decision to a person, [Escalation Patterns in Governed Systems](../../governance/escalation-patterns-in-governed-systems.md) explains how to keep escalation a deliberate authority path rather than a catch-all. For keeping the delivery worker, the re-evaluation worker, and the destination credentials on the right sides of their boundaries, see [Trust Boundaries and Least Privilege](../../security/trust-boundaries-and-least-privilege.md).

### Related Work

These references address related aspects of the problem:

- The [OWASP Authorization Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html) recommends deny-by-default and safe handling of failed authorization checks.
- The [OWASP Authorization Patterns Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Patterns_Cheat_Sheet.html) recommends configuring decision-point errors and timeouts to deny protected operations and keeps enforcement responsibility with the service.
- The [OWASP Authorization Policy and Data Distribution Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Policy_And_Data_Distribution_Cheat_Sheet.html) separates loss of policy distribution from loss of a usable decision and sets freshness bounds for continued use of previously loaded policy.
- [Open Policy Agent — Operations](https://www.openpolicyagent.org/docs/operations) places fail-open versus fail-closed behavior with the software that requests the decision.
- The NIST glossary entry for [fail secure](https://csrc.nist.gov/glossary/term/fail_secure) defines failure behavior in terms of preserving secure state.
- [.NET resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/) documents the timeout, retry, circuit-breaker, and fallback strategies discussed here as transport mechanisms.

---

## The Short Answer

Neither, by slogan. Fail open is not an authorization fallback; it turns an outage into a grant. Fail closed is the right invariant, no protected side effect without valid authority, but it is not a complete design.

Keep an explicit denial and an unavailable decision as different facts, even when both stop execution. Let resilience mechanisms make the question more reliable without letting them answer it. Give each operation a written outage treatment: reject, defer with an expiry, escalate to an independent authority, or, only when designed in advance, evaluate locally within strict version, freshness, and scope bounds. Stop and reconcile when a side effect may already have happened. Record what the host did and why, resolve everything the outage left behind, and test that nothing protected executed while authority was unavailable.
