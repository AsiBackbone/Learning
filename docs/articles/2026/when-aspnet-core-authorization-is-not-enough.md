---
description: A practical ASP.NET Core architecture article on choosing between built-in authorization, resource-based authorization, a simple application service, and a broader decision/execution lifecycle.
title: When ASP.NET Core Authorization Is Not Enough
author: Christopher D. Cavell
published: "2026-08-26"
summary: ASP.NET Core authorization is often the right answer; add a broader decision/execution lifecycle only when workflow, time, authority, or evidence crosses the request boundary.
feed: true
---

# When ASP.NET Core Authorization Is Not Enough

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** Familiarity with ASP.NET Core authentication and authorization is helpful; no ASI Backbone package or prior Learning material is required.

ASP.NET Core authorization is powerful, familiar, and sufficient for a very large class of applications. Roles, claims, named policies, custom requirements and handlers, `IAuthorizationService`, and resource-based authorization already cover far more than a simple `[Authorize]` attribute.

For many operations, that framework is not merely adequate. It is the better architecture.

The harder question appears when the requirement stops being only:

```text
May this actor perform this operation?
```

and becomes something closer to:

```text
What should happen next with this proposed operation,
when should it happen,
and what authority may cross the execution boundary?
```

That broader question can include outcomes such as:

```text
Allow
Deny
Defer
RequireAcknowledgment
Escalate
```

or execution that occurs after the current request, current user session, or current policy evaluation has ended.

The point of this article is proportionality: **start with ASP.NET Core authorization, and add another lifecycle only when the problem actually needs one.** No ASI Backbone package is required.

This article is the **decision guide**: it helps you choose the smallest useful boundary for a concrete application requirement. [When ASP.NET Core Authorization Is Enough](../../architecture/when-aspnet-core-authorization-is-enough.md) is the deeper head-to-head reference that examines framework authorization and governed execution in more detail. The two pages are intentionally complementary rather than inverse duplicates.

## Start with the Smallest Useful Boundary

Use this table as the first pass; the rest of the article explains the pressure behind each row.

| Requirement | Usually the smallest useful boundary |
| --- | --- |
| Role/claim/endpoint access | ASP.NET Core authorization |
| Permission depends on loaded resource | Resource-based authorization |
| Immediate domain/use-case rule after authorization | Simple application service |
| Defer, acknowledge, or escalate | Explicit lifecycle decision |
| Execute later or in another process | Explicit continuation/execution authority |
| Preserve policy identity and decision lineage | Durable decision evidence |
| Facts may change before later execution | Revalidation at the execution boundary |

These are not mutually exclusive. A mature design often uses ASP.NET Core authorization at the edge and a richer lifecycle only for the smaller set of consequential operations that need it.

## Start with What ASP.NET Core Already Gives You

ASP.NET Core authorization supports ordinary endpoint and resource access through established framework concepts:

- roles and claims;
- named policies;
- custom authorization requirements;
- custom authorization handlers;
- `IAuthorizationService` for imperative checks;
- resource-based authorization when the resource must be loaded first;
- authorization failure reasons;
- integration with endpoint routing, MVC, Razor Pages, Blazor, authentication principals, and dependency injection.

That is already a reusable decision mechanism for the question authorization is designed to answer:

```text
Authenticated principal
        +
Operation / endpoint
        +
Optional loaded resource
        ↓
Authorized or not authorized
```

If that is the complete decision, introducing a separate policy pipeline, workflow state machine, capability issuer, or durable decision ledger adds machinery without adding a useful boundary.

## Case 1: Plain ASP.NET Core Authorization Wins

Suppose the complete rule is:

> Only administrators may access the account-maintenance endpoint.

A policy or role check can express that directly:

```csharp
app.MapPost(
        "/accounts/{accountId}/disable",
        DisableAccountAsync)
    .RequireAuthorization(policy =>
        policy.RequireRole("Administrator"));
```

The lifecycle is straightforward:

```text
Request
   ↓
Authenticate
   ↓
Authorize
   ├── fail → reject
   └── pass → application service
```

There is no benefit in translating that result into a custom five-state governance model. The framework has already solved the problem in a way .NET developers recognize immediately.

This is the first proportionality rule:

> **Do not build a broader lifecycle merely because one exists elsewhere in the system.**

## Case 2: Resource-Based Authorization Is Still Authorization

The next requirement may be more specific:

> An administrator may disable this account only when the account belongs to the same tenant and is not protected.

That does not automatically require a governance pipeline. ASP.NET Core supports resource-based authorization precisely because some authorization decisions require the resource to be loaded first.

A handler can evaluate the current principal against the loaded account:

```csharp
public sealed class DisableAccountRequirement
    : IAuthorizationRequirement
{
}

public sealed record DisableAccountResource(
    string AccountId,
    string TenantId,
    bool IsProtected);

public sealed class DisableAccountHandler
    : AuthorizationHandler<
        DisableAccountRequirement,
        DisableAccountResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DisableAccountRequirement requirement,
        DisableAccountResource resource)
    {
        string? actorTenant =
            context.User.FindFirst("tenant_id")?.Value;

        if (!context.User.IsInRole("Administrator"))
        {
            return Task.CompletedTask;
        }

        if (!string.Equals(
                actorTenant,
                resource.TenantId,
                StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        if (resource.IsProtected)
        {
            return Task.CompletedTask;
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
```

The endpoint can load the resource and authorize it explicitly. In a minimal API, take the authenticated `ClaimsPrincipal` as a handler parameter rather than relying on the controller-only `User` property:

```csharp
app.MapPost(
    "/accounts/{accountId}/disable",
    async (
        string accountId,
        ClaimsPrincipal user,
        IAuthorizationService authorization,
        IAccountRepository accounts,
        IAccountService accountService,
        CancellationToken cancellationToken) =>
    {
        Account? account = await accounts.GetAsync(
            accountId,
            cancellationToken);

        if (account is null)
        {
            return Results.NotFound();
        }

        var resource = new DisableAccountResource(
            account.Id,
            account.TenantId,
            account.IsProtected);

        AuthorizationResult authorizationResult =
            await authorization.AuthorizeAsync(
                user,
                resource,
                new DisableAccountRequirement());

        if (!authorizationResult.Succeeded)
        {
            return Results.Forbid();
        }

        await accountService.DisableAsync(
            account.Id,
            cancellationToken);

        return Results.NoContent();
    });
```

The handler above deliberately leaves a failed requirement unsatisfied rather than calling `context.Fail()`. That permits other handlers to contribute normally. Calling `Fail()` records an explicit failure: other handlers may still run by default, but the overall authorization result cannot succeed once failure is recorded; applications can also configure handler invocation after failure through `AuthorizationOptions.InvokeHandlersAfterFailure`.

If all relevant facts belong to the current authorization decision and execution follows immediately, this is a strong design. The resource was not available at routing time, but the problem is still access control. Project only the fields authorization actually needs, or reuse the already-loaded aggregate when the subsequent operation can do so safely, rather than introducing a second read merely to satisfy the authorization shape.

For multi-tenant resources, also decide whether `403 Forbidden` reveals too much. Some systems intentionally map an unauthorized cross-tenant lookup to `404 Not Found` so callers cannot use identifiers to confirm another tenant's resource exists. That is a transport and disclosure decision layered on top of the authorization result.

One subtle point matters for the rest of this article: `IsProtected` is not intrinsically an authorization fact or a workflow fact. In a simple system, “protected accounts may never be disabled by this actor” is naturally access control. In a richer system, the same current-state fact might mean “this actor may request the operation, but the request must be escalated or acknowledged.” The architecture follows the **meaning of the outcome**, not the name of the input field.

## Case 3: Authorization Can Be Complete and a Simple Application Service Can Still Be Enough

Not every post-authorization rule belongs inside `IAuthorizationService`.

Suppose the caller is authorized to edit a case, but the use case also says a case cannot be archived after it has already been finalized. That may be an ordinary domain or application rule rather than an authorization rule.

A small application service can coordinate the operation:

```csharp
public async Task<ArchiveCaseResult> ArchiveAsync(
    ArchiveCaseCommand command,
    CancellationToken cancellationToken)
{
    CaseFile? caseFile = await cases.FindAsync(
        command.CaseId,
        cancellationToken);

    if (caseFile is null)
    {
        return ArchiveCaseResult.NotFound();
    }

    if (caseFile.IsFinalized)
    {
        return ArchiveCaseResult.Conflict(
            "case.archive.finalized");
    }

    caseFile.Archive(command.ActorId);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return ArchiveCaseResult.Succeeded();
}
```

The result may have several application outcomes without becoming a generalized governance lifecycle. The important question is whether the operation can still be decided and executed inside one immediate, trusted application-service boundary.

This gives a useful middle ground:

```text
Authorization alone is not the whole use case
        ↓
Simple application service handles current-state rules
        ↓
Immediate execution
```

A richer lifecycle is not the automatic next step after authorization.

## The Boundary Changes When the Result Is No Longer Just Access Control

Consider an authorized administrator requesting an account-disable operation. Current operational policy may produce one of these outcomes:

```text
Allowed
Denied
Deferred
AcknowledgmentRequired
EscalationRecommended
```

Only some of those are naturally authorization results.

`Denied` may mean the current actor or path is not permitted.

`Deferred` may mean the actor is permitted, but a temporary operational hold prevents execution now.

`AcknowledgmentRequired` may mean the actor is permitted, but continuation requires an explicit interruption and response.

`EscalationRecommended` may mean the current request should be routed to another authority rather than permanently rejected.

If all of those states are compressed into authorization success/failure, the application loses lifecycle meaning:

```text
Deferred                 ┐
AcknowledgmentRequired   ├──→ authorization failed
EscalationRecommended    ┘
```

That can work mechanically, but it makes different operational states look identical. Once the host must preserve those distinctions and route them differently, the problem has become larger than authorization alone.

## Authorization, Workflow State, and Execution Authority Are Different Questions

A useful design separates three questions that are easy to blur together.

| Concern | Question | Typical artifact |
| --- | --- | --- |
| Authorization | May this actor enter or request this operation? | ASP.NET Core authorization result |
| Workflow / decision state | What should happen next given current facts? | Application-specific structured outcome |
| Execution authority | What may this executor do later, against which resource and under what limits? | Immediate host call or bounded continuation/capability |

For a simple endpoint, all three can collapse into one request. For a longer-lived operation, keep the boundaries conceptually distinct:

```text
Authorization boundary
"May this actor enter or request the operation?"
                ↓
Workflow / decision boundary
"What should happen next given current facts?"
                ↓
Execution-authority boundary
"What may this executor do now or later, and under what limits?"
                ↓
Protected execution
```

The broader shape is justified by lifecycle separation, not by a preference for more abstractions.

## Signal 1: The Operation Can Be Deferred

A temporary maintenance hold is a good example.

The actor may be authorized and the resource may be one the actor normally controls, yet a temporary maintenance hold can make the correct current result `Deferred`. If the application needs to preserve that as a retryable or schedulable state rather than return a generic authorization failure, an explicit workflow outcome earns its keep.

A small application service may still be sufficient when the deferral is request-local—for example, returning `409 Conflict` or `503 Service Unavailable` and asking the caller to try again. A broader lifecycle becomes more useful when the host itself owns the deferred work, preserves it, resumes it later, or must carry decision evidence across that pause.

## Signal 2: Human Acknowledgment or Escalation Interrupts the Flow

Acknowledgment introduces a continuation boundary:

```text
Authorized request
      ↓
Current decision
      ↓
Acknowledgment required
      ↓
Pause
      ↓
Human response
      ↓
Re-evaluate current facts
      ↓
Execute or stop
```

The acknowledgment should not silently become new authorization. It records that a particular interruption step was satisfied; the host still needs to decide whether the operation remains executable when it resumes. If the acknowledgment is satisfied and re-evaluation still returns `Allowed`, the host can proceed once under the current execution contract.

Escalation has a similar distinction. A protected resource may need a higher-authority review path. Treating that as a plain `Forbid` response can erase the fact that the request is valid but must move elsewhere.

Once the application owns pause/resume or route-to-review semantics, it owns workflow state in addition to authorization.

## Signal 3: Execution Happens After the Request Ends

A background operation changes the authority problem even if the initial authorization was correct.

A request may authorize work now, enqueue it, end the HTTP request, and let a worker execute later. The worker no longer has the same request lifecycle: the original `ClaimsPrincipal` may not exist, the user session may have ended, the resource may have changed, and policy may have been updated.

The system therefore has to decide what the later worker is allowed to rely on. Depending on the risk and architecture, valid choices include:

- rebuild current context and re-authorize/re-evaluate at execution time;
- persist a bounded command and require the worker to validate current state;
- issue narrowly scoped, short-lived execution authority bound to actor, operation, resource, audience, version, and expiration;
- reject delayed execution entirely and require a fresh request.

What is unsafe is assuming that an authorization success from an earlier request automatically becomes indefinite execution authority.

## Signal 4: Authority Crosses a Process or Trust Boundary

A same-process application service can often call its dependency directly after authorization and current-state checks. A delegated worker, remote service, tool gateway, or other independently operated executor creates a separate authority-transfer problem. Signal 4 is often Signal 3 with an additional process or trust boundary, although even immediate cross-process delegation can require the same reasoning.

Passing a broad user credential or replaying the original request context may grant more authority than the executor needs. A bounded artifact can instead represent only the continuation authority required for one operation. A minimal application-owned shape might look like this:

```csharp
public sealed record DisableAccountContinuation(
    string ActorId,
    string AccountId,
    string Operation,
    string Audience,
    string ResourceVersion,
    string PolicyVersion,
    DateTimeOffset ExpiresAtUtc);
```

This record is only an illustrative data shape, not a secure token by itself. If it crosses a trust boundary, the receiving host must establish its integrity and provenance, validate its bindings and lifetime, and still enforce whatever freshness checks the operation requires. In many systems, normal service identity plus a fresh destination-side authorization decision is simpler.

The signal is simply that **authority now has a lifecycle separate from the original request**, so the design must make that lifecycle explicit somehow.

## Signal 5: Policy Has Its Own Version and Decision Evidence Matters

ASP.NET Core authorization can call application services and custom handlers can use whatever facts the application supplies. The framework does not prevent policy from being sophisticated.

The architectural pressure changes when the application must later answer questions such as:

- Which policy version produced this outcome?
- Which authoritative resource state was evaluated?
- Which reason code caused deferral or escalation?
- Which acknowledgment belongs to which decision?
- Which later execution consumed the resulting authority?

At that point, ordinary authorization logging may not be enough. The system may need a durable decision record containing policy identity, correlation, actor, operation, resource, reason, and relevant version information. For example:

```csharp
public sealed record DecisionEvidence(
    string CorrelationId,
    string ActorId,
    string Operation,
    string ResourceId,
    string Outcome,
    string ReasonCode,
    string PolicyVersion,
    string ResourceVersion,
    DateTimeOffset DecidedAtUtc);
```

`Outcome` is intentionally shown as a `string` in this durable record: persisted evidence should use a stable storage/wire value rather than depend on CLR enum member names that may be renamed during refactoring. An application that serializes an enum directly should give its persisted values the same stability discipline.

This is evidence, not execution authority. Persisting it does not mean a later worker may execute from the record alone.

That evidence is application architecture layered beside authorization; it is not a reason to replace ASP.NET Core authorization at the edge.

## Signal 6: Resource or Policy State Can Drift Before Execution

An allowed result is a point-in-time judgment.

Suppose the host evaluates:

```text
account = unprotected
policy = v42
      ↓
Allowed
```

but execution occurs later and the world is now:

```text
account = protected
policy = v43
```

The earlier decision may still be useful evidence, but it should not automatically force execution against stale facts.

A later executor may need to validate a resource version, ETag, row version, policy version, lease, freshness token, or freshly loaded state immediately before the protected side effect. The exact mechanism depends on which system owns the decisive facts.

This is the time-of-check/time-of-use boundary:

```text
Decision against snapshot A
        ↓
Time passes
        ↓
Execution against world B
```

If the interval is effectively zero and one transaction or concurrency check protects the relevant state, an ordinary application service may still be enough. If the interval is durable, cross-process, or governed by independently changing policy, a distinct continuation/execution boundary becomes easier to justify.

## What the Broader Lifecycle Costs

A richer lifecycle is not a free upgrade. It introduces state and failure modes that request-local authorization does not have to own. Depending on the design, teams may need to operate and test:

- durable decision or continuation state and its schema migrations;
- resume paths, retries, poison/stuck work, and operator recovery;
- correlation between decisions, acknowledgments, grants, and executions;
- policy/version freshness and revalidation failures;
- retention, purge, privacy, and access rules for durable decision evidence;
- expiration, revocation, replay, and audience validation for delegated authority;
- additional telemetry for work that no longer completes in one request;
- more integration and failure-mode tests across time and process boundaries.

Those costs are justified when they preserve a boundary the application truly needs. They are waste when the requirement is still “authorize this request, apply the current domain rule, execute now.” Proportional architecture means accounting for both sides of that trade.

## Start Small Without Painting Yourself into a Corner

Choosing ordinary authorization today does not have to make tomorrow's richer lifecycle expensive. Keep the current decision in one obvious place, keep protected side effects behind a narrow application/executor seam, return structured application results when the caller needs more than an exception or boolean, and avoid scattering the same rule across endpoint, handler, service, and repository code.

If requirements later add deferral, acknowledgment, escalation, or delayed execution, those seams give you a place to extract an explicit lifecycle without rewriting every caller. The goal is not to pre-build capabilities or workflow infrastructure “just in case”; it is to keep today's simpler design coherent enough that tomorrow's boundary can be introduced deliberately.

## A Hybrid Design Is Often the Right Answer

The choice is rarely “ASP.NET Core authorization or governance.” A common proportional design is:

```text
Authentication
   ↓
ASP.NET Core authorization
   │
   │ May this actor enter this operation?
   ↓
Application decision / workflow
   │
   │ What should happen next?
   ↓
Acknowledgment / escalation when required
   ↓
Scoped continuation authority only when required
   ↓
Host-owned execution
```

ASP.NET Core remains responsible for the access-control question it handles well. The application adds a richer lifecycle only after authorization, and only for operations where the extra states or execution separation are real requirements.

For an account-disable operation, that might mean:

1. ASP.NET Core authorization verifies that the actor may request account administration.
2. The host loads current account and operational-policy state.
3. An application-specific decision returns `Allowed`, `Deferred`, `AcknowledgmentRequired`, `EscalationRecommended`, or `Denied`.
4. Non-immediate outcomes stop the protected executor and route appropriately.
5. An allowed same-request operation executes directly, or a delayed operation receives only the continuation authority it actually needs.
6. The executor validates current state again when drift matters.

The architecture stays familiar at the edge while becoming explicit where the lifecycle genuinely grows.

## Do Not Hide Workflow Semantics Inside Authorization Failure Reasons

ASP.NET Core authorization failure reasons are useful. They can explain why authorization failed and support diagnostics or custom result handling.

They should not be mistaken for a generalized workflow state machine.

For example:

```csharp
context.Fail(
    new AuthorizationFailureReason(
        this,
        "Deferred: maintenance hold."));
```

may let the application recover a string that says “Deferred,” but the framework result is still failed authorization. ASP.NET Core also provides `IAuthorizationMiddlewareResultHandler`, which can translate challenge/forbid outcomes into application-specific HTTP responses. That is useful transport customization: a team could map a particular failure reason to `503`, for example. It still does not create durable deferral state, a resume path, acknowledgment lineage, escalation routing, or execution authority.

If the host needs to schedule retry, preserve a durable deferral, wait for acknowledgment, or route escalation, that lifecycle has to exist somewhere else in the application. Use authorization failure reasons to explain authorization; use an explicit application outcome when the application must drive workflow from that outcome.

## A Late Authorization-Like Check Does Not Automatically Mean You Need More Architecture

Suppose a blocking rule is currently discovered only after a repository write or external call. The defect is that the check runs too late, but the right repair depends on what kind of rule it is.

If the rule is genuinely access control and depends on a loaded resource, move it into resource-based authorization before execution.

If the rule is an ordinary current-state domain condition, move it into the application service before mutation.

If the rule represents deferral, acknowledgment, escalation, independently versioned policy, or later execution authority, model the broader lifecycle explicitly.

The sequence is therefore:

```text
Blocking rule discovered too late
        ↓
What question does the rule answer?
        ├── access control → authorization
        ├── immediate use-case/domain rule → application service
        └── lifecycle / continuation / authority → broader decision flow
```

This distinction prevents “decision before execution” from becoming “build a governance pipeline for every `if` statement.”

## Test the Boundary You Actually Chose

The testing strategy should match the architecture.

For ordinary ASP.NET Core authorization, test the relevant policy or handler behavior and the endpoint's authorized/forbidden result.

For resource-based authorization, include representative resource states and principals.

For an application service, test the domain/use-case result and the mutation or transaction behavior that matters.

For a broader lifecycle, add the stronger invariant that every non-immediate outcome leaves protected execution unreachable:

```text
Denied                  → protected execution = 0
Deferred                → protected execution = 0
AcknowledgmentRequired  → protected execution = 0 until satisfied and re-evaluated
EscalationRecommended   → protected execution = 0 on the current path
Allowed                 → execution follows the selected execution contract
```

Do not make every endpoint prove all of those states if the endpoint does not have them. The test suite should preserve the boundaries the application actually chose, not force every operation into the richest model. When one operation has several protected side-effect boundaries, observe each with an independent recorder or test double so activity at one boundary cannot mask what happened at another.

## Three Shortcuts to Avoid

- **Encoding workflow state only in authorization failure reasons.** A different HTTP mapping does not create the state, routing, or continuation semantics the workflow needs.
- **Passing the user's broad credential to a background or remote executor.** Prefer fresh destination-side authorization or narrowly bounded continuation authority appropriate to that executor.
- **Treating delayed execution as request-local.** A queued or resumed operation must account for changed resource state, policy, identity/session lifetime, and cancellation/retry behavior instead of assuming the earlier request still exists.

## Questions to Ask Before Adding Another Layer

Use these questions as a final proportionality check:

1. Is the real question access control, domain/use-case state, or workflow lifecycle?
2. Can ASP.NET Core authorization already express the access-control requirement clearly?
3. Does resource-based authorization solve the fact that the resource must be loaded first?
4. Would a small application service handle the remaining rule without losing important semantics?
5. Is `Deferred` meaningfully different from `Denied` in this application?
6. Can an already-authorized actor still need acknowledgment or escalation?
7. Does execution occur after the request or in another process?
8. Does a later executor need authority narrower than the original session or service identity?
9. Must the system preserve policy version, reason, and decision lineage durably?
10. Can resource or policy state change between decision and execution?
11. What must be revalidated at the protected boundary?
12. Are you adding infrastructure because the lifecycle requires it, or because the architecture already has that infrastructure elsewhere?

The last question is the guardrail against overengineering.

> **Use the smallest architecture that preserves the boundaries you actually need.**

## Continue Deeper

- [When ASP.NET Core Authorization Is Enough](../../architecture/when-aspnet-core-authorization-is-enough.md) is the deeper head-to-head reference for framework authorization versus a governed-execution lifecycle; this article is the shorter reach-for-what decision guide.
- [When a Simple Application Service Is Enough](../../architecture/when-a-simple-application-service-is-enough.md) examines the middle ground between authorization and a broader governance lifecycle.
- [Your Authorization Check Runs Too Late](authorization-check-runs-too-late.md) starts from a failure where a blocking rule is discovered after protected work has already begun.
- [Decision Before Execution](../../tutorials/decision-before-execution.md) explains the foundational proposal → context → decision → host-owned execution boundary.
- [Policy Context and Explicit Decision Outcomes](../../tutorials/policy-context-and-explicit-decision-outcomes.md) develops the richer `Allowed` / `Denied` / `Deferred` / acknowledgment / escalation decision vocabulary.
- [Scoped Capability and Host-Owned Execution](../../tutorials/scoped-capability-and-host-owned-execution.md) covers the case where execution authority becomes a narrow artifact distinct from the original authorization result.

---

> **Read it. Run it. Question it. Improve it.**
