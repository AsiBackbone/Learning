---
description: Why a MediatR pipeline behavior or .NET decorator is a strong home for uniform mechanics but a weak sole owner of resource-dependent authorization decisions.
title: Your Pipeline Behavior Can Watch the Operation. It Should Not Decide It.
author: Christopher D. Cavell
summary: A generic, operation-agnostic pipeline behavior wraps a call that has already been chosen; it sees the request rather than the resource, cannot express operation-specific outcomes without an operation-specific contract, and can be bypassed by registration order or a second entry path.
feed: true
---

# Your Pipeline Behavior Can Watch the Operation. It Should Not Decide It.

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** Familiarity with dependency injection and the decorator pattern in .NET is helpful. No MediatR, Scrutor, AsiBackbone package, or prior Learning material is required.

**What this article covers:** why a MediatR pipeline behavior or decorator is an excellent place for uniform mechanics and a weak sole owner of consequential decisions; five ways a generic pipeline check fails quietly; how to move the decision into the operation so the executor cannot run on a raw command; how to keep the decision valid until the change is written; how to test the path the application actually uses; and when a pipeline check really is enough.

Most .NET codebases that dispatch commands through a handler abstraction eventually grow a stack of wrappers around every handler. In MediatR this is an `IPipelineBehavior`. Without MediatR it is an ordinary decorator registered in the container, often with Scrutor's `Decorate` extension. The shape is the same either way:

```text
Logging
   ↓
Validation
   ↓
Authorization
   ↓
Transaction
   ↓
Handler
```

It is a good design. One class adds timing to every command. Another opens a unit of work. Another emits a trace span. No handler has to remember any of it.

Then a reasonable next step follows: if logging and transactions can live in the pipeline, why not the permission check too?

For a coarse check, that is often fine. This article is about a narrower case: the **consequential decision** — whether a specific side effect may run, judged against the current state of the resource it touches, with an explicit outcome that may be more than yes or no. For that decision, a generic wrapper is the wrong *sole* owner.

To be precise about what is not being claimed: a pipeline behavior *can* short-circuit and return a response without calling the next handler, and a purpose-built behavior *can* load a resource and return a typed result. The objection is to a generic wrapper, written once for every command, being the only thing that decides whether a consequential, resource-dependent operation runs.

## A Plausible Authorization Check

In MediatR, the familiar form is a behavior:

```csharp
public sealed class AuthorizationBehavior<TRequest, TResponse>(
    ICurrentUser user,
    IPermissionService permissions)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IRequirePermission required &&
            !await permissions.HasAsync(user.Id, required.Permission, cancellationToken))
        {
            throw new ForbiddenException(required.Permission);
        }

        return await next();
    }
}
```

registered with `cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>))`. The order of `AddOpenBehavior` calls determines nesting: the first behavior registered is the outermost.

The rest of this article uses a library-neutral decorator so the examples are not coupled to MediatR. The failure modes are identical. Start with a minimal command-handler abstraction:

```csharp
public interface ICommandHandler<TCommand>
{
    Task<Result> HandleAsync(TCommand command, CancellationToken ct);
}

public interface IRequirePermission
{
    string Permission { get; }
}

public sealed record DisableAccount(Guid AccountId, Guid TenantId)
    : IRequirePermission
{
    public string Permission => "accounts.disable";
}
```

and the same check as a decorator:

```csharp
public sealed class AuthorizationDecorator<TCommand>(
    ICommandHandler<TCommand> inner,
    ICurrentUser user,
    IPermissionService permissions)
    : ICommandHandler<TCommand>
{
    public async Task<Result> HandleAsync(TCommand command, CancellationToken ct)
    {
        if (command is IRequirePermission required &&
            !await permissions.HasAsync(user.Id, required.Permission, ct))
        {
            throw new ForbiddenException(required.Permission);
        }

        return await inner.HandleAsync(command, ct);
    }
}
```

registered once for every handler:

```csharp
services.AddScoped<ICommandHandler<DisableAccount>, DisableAccountHandler>();

services.Decorate(typeof(ICommandHandler<>), typeof(TransactionDecorator<>));
services.Decorate(typeof(ICommandHandler<>), typeof(AuthorizationDecorator<>));
services.Decorate(typeof(ICommandHandler<>), typeof(RetryDecorator<>));
services.Decorate(typeof(ICommandHandler<>), typeof(LoggingDecorator<>));
```

Each later `Decorate` call wraps the previous result, so the final chain is `Logging → Retry → Authorization → Transaction → Handler`.

Nothing here is careless. Code review would pass it. For a coarse rule — "only authenticated administrators may send administrative commands" — it may be entirely adequate.

The trouble starts when the rule is not coarse.

## Failure 1 — The Wrapper Has No Outcome Vocabulary

A wrapper sits around a call that has already been chosen. Generically, it can do three things: call the next handler, return some `TResponse` without calling it, or throw.

Real decisions about consequential operations frequently need more than "yes" or "no":

| Outcome | Meaning |
| --- | --- |
| Allowed | Execute now |
| Denied | Never execute this request |
| Deferred | Not yet — required state or information is missing |
| Acknowledgment required | Execute only after a named person confirms the consequence |
| Escalation recommended | A different authority should decide |

A generic wrapper cannot construct a meaningful "pending second approver" response for an arbitrary `TResponse` without knowing what each operation means — which is Failure 3. So in practice those outcomes are squeezed into exceptions, and every *other* wrapper in the chain now has an opinion about them.

Look at the registration order again. `RetryDecorator` sits outside `AuthorizationDecorator`. If the retry policy is written to retry on any exception, a `ForbiddenException` is retried three times before surfacing. That is merely wasteful. Now suppose a later change teaches the authorization decorator to throw `PendingApprovalException` for the "second approver required" case, and the retry decorator is later broadened to treat "pending" as transient. The operation is re-attempted until the approval appears — without anyone having decided that approval should resume execution automatically.

No single change in that sequence looks wrong. The bug lives in the interaction between classes that were designed to be independent.

A decision with more than two outcomes needs a representation the caller must handle explicitly, not an exception that an unrelated wrapper may catch.

## Failure 2 — This Marker Check Fails Open

The decorator above only enforces anything when `command is IRequirePermission`.

Next quarter, someone adds:

```csharp
public sealed record TransferAccountOwnership(Guid AccountId, Guid NewOwnerId);
```

They forget the marker interface. Everything compiles. Every test passes. The command runs through the authorization decorator, which checks nothing and calls `inner`.

The implementation shown is **fail-open**: the safe state requires every contributor to remember something, and forgetting produces no error. Attribute-driven variants — `[RequiresPermission("…")]` read by reflection — and policy tables that return "no policy found, continue" behave the same way.

A marker design does not have to fail open. The minimum repair is to make absence fail closed: a command with no declared policy is denied, and a test (shown later) proves every dispatched command declares one. That repair helps. It does not address the next three failures.

## Failure 3 — The Wrapper Sees the Request, Not the Resource

A generic decorator receives `TCommand`. That is the *proposal* — the values the caller supplied.

The rule that actually matters is rarely about the proposal alone:

> An administrator may disable an account **in their own tenant**, unless the account is **protected**, in which case a second approver is required.

To evaluate that, the check needs the account's tenant and its protected flag as they exist *now*, in authoritative storage. A generic `AuthorizationDecorator<TCommand>` has no idea how to load an account, a document, or an invoice. Two things tend to happen next:

1. **The decorator trusts the request.** `DisableAccount` already carries `TenantId`, so the check compares `command.TenantId` to the user's tenant. But `TenantId` was supplied by the caller. A request naming the caller's own tenant and someone else's `AccountId` passes the check and reaches a handler that loads the account by ID.
2. **The decorator grows per-command branches.** A `switch` on command type appears inside the "generic" decorator, each branch loading different state. The cross-cutting abstraction has quietly become a second, hidden copy of the domain.

Either way, the check and the operation read authoritative state at different times, through different code, possibly in different transactions. The check decided about one version of the world; the handler acts on another.

This is the same gap described in [Your Authorization Check Runs Too Late](authorization-check-runs-too-late.md): the decision must be made against the resource the operation will actually touch.

## Failure 4 — Registration Order Is the Boundary

In the example, the protection is not really in `AuthorizationDecorator`. It is in four lines of `Program.cs`, ordered correctly today.

Two kinds of wrapper commonly arrive later, for good reasons, and land outside the check:

**A caching wrapper.** For queries, an outer cache keyed only by request parameters returns data cached from an earlier, authorized caller to a later caller who was never evaluated. That is a direct authorization bypass — a disclosure, with no handler involved.

**An idempotency wrapper.** Clients retry, so duplicate commands should return the original result:

```csharp
services.Decorate(typeof(ICommandHandler<>), typeof(IdempotencyDecorator<>));
```

Appended at the end, it becomes the outermost wrapper. A request carrying a previously seen key returns the cached result without reaching authorization. That does **not** re-run the protected effect — the account was disabled once. The risks are narrower but real: a different caller who presents the same key receives a success response, and possibly data, for an operation they were never evaluated for; and the replay bypasses both the authorization and logging decorators shown here, so unless the idempotency wrapper records replays itself, nothing records a decision for that request. The repair is to bind idempotency keys to the actor, operation, and resource — and ideally a hash of the payload — so a replayed key from anyone else is a miss.

Nothing in any of these wrappers is wrong in isolation. The protection depended on an ordering that no type, test, or compiler enforced.

ASP.NET Core middleware has exactly the same property; [Middleware Ordering Changes Behavior](../../aspnetcore/middleware-ordering-changes-behavior.md) walks through it in depth. The lesson carries over: when order is load-bearing, it needs an observable failure, not a comment.

## Failure 5 — The Handler Has Other Entry Paths

A decorator protects calls that arrive through the decorated service registration. It does nothing for calls that do not.

In a growing application, those paths appear naturally:

- a background job resolves `DisableAccountHandler` by its concrete type,
- a message consumer or scheduled task constructs the handler through a different factory,
- another handler calls `DisableAccountHandler.HandleAsync` directly to reuse its logic,
- an administrative CLI or data-fix script shares the application assembly but not its container configuration,
- in MediatR specifically, notifications sent with `Publish` do not pass through request pipeline behaviors at all, so a notification handler that performs a side effect is outside the pipeline by design.

Each of those invokes the side effect with the pipeline's protection stripped away, and `DisableAccountHandler` itself contains nothing that notices. Its signature accepts a `DisableAccount` command. It does not demand evidence that anyone decided the command should run.

That is the core structural issue. **With a wrapper, permission is a property of the path, not of the operation.**

## What the Pipeline Is Good At

None of this is an argument against decorators or pipeline behaviors. They are an excellent tool for concerns that share three properties:

- they behave the same regardless of what the operation means,
- skipping them degrades quality, not safety,
- their outcome is not a decision about whether the operation is permitted.

That describes observability and most uniform mechanics:

| Concern | Good pipeline fit? | Why |
| --- | --- | --- |
| Timing, metrics, tracing spans | Yes | Uniform; skipping loses telemetry, not protection |
| Structured request/outcome logging | Yes | Records what happened; does not decide it |
| Input shape validation (required fields, formats) | Usually | Rejects malformed proposals; not a permission decision |
| Transient-fault retry | Yes, with care | Must not reinterpret decision outcomes as transient |
| Unit of work / transaction scope | Usually | Wrapping the whole handler also opens a transaction for denied and pending paths; scoping it to the executor avoids that but leaves the decision's reads outside it (see "Keep the Decision True Until the Write") |
| Response caching and idempotency | With care | Keys must be bound to the caller and resource, or they answer for someone who was never evaluated |
| Coarse gate: "authenticated", "has admin role" | Sometimes | See the final section |
| Resource-, state-, or time-dependent permission | No | Needs authoritative context a generic wrapper does not own |
| Approval, acknowledgment, escalation | No | Outcomes a generic wrapper cannot represent |

The pipeline is the right place to *observe* decisions. A logging behavior that records every command's outcome, reason code, and duration is valuable precisely because it is uniform. It watches every operation. It does not need to understand any of them.

## Move the Decision Into the Operation

The alternative is not a bigger decorator. It is making the decision an explicit step of the use case, performed against authoritative state, whose result the executor requires. None of what follows needs a library; the types are small, though the pattern as a whole — context, policy, executor, conditional store write, and tests — is more code than a decorator, which is why it belongs only on operations that justify it.

Model the decision as a value with every outcome the operation actually has, using a stable reason code rather than display text:

```csharp
public abstract record Decision<TCommand>(string ReasonCode)
{
    public sealed record Allowed(Permit<TCommand> Permit)
        : Decision<TCommand>("allowed");

    public sealed record Deferred(string ReasonCode)
        : Decision<TCommand>(ReasonCode);

    public sealed record AcknowledgmentRequired(string ReasonCode)
        : Decision<TCommand>(ReasonCode);

    public sealed record EscalationRecommended(string ReasonCode)
        : Decision<TCommand>(ReasonCode);

    public sealed record Denied(string ReasonCode)
        : Decision<TCommand>(ReasonCode);
}
```

Then make the executor require a permit rather than a raw command:

```csharp
public sealed class Permit<TCommand>
{
    internal Permit(TCommand command, long resourceVersion, string policyVersion)
    {
        Command = command;
        ResourceVersion = resourceVersion;
        PolicyVersion = policyVersion;
    }

    public TCommand Command { get; }
    public long ResourceVersion { get; }
    public string PolicyVersion { get; }   // recorded for audit; not re-checked
}

public sealed class DisableAccountExecutor(IAccountStore accounts)
{
    public async Task<Result> ExecuteAsync(
        Permit<DisableAccount> permit,
        CancellationToken ct)
    {
        var applied = await accounts.TryDisableAsync(
            permit.Command.AccountId,
            expectedVersion: permit.ResourceVersion,
            ct);

        return applied
            ? Result.Success()
            : Result.Conflict("account.changed_since_decision");
    }
}
```

The executor acts on the command *inside* the permit, so the thing executed is exactly the thing decided. `ResourceVersion` is explained in the next section.

The policy is the only intended caller of the `Permit` constructor, and it stamps the permit from the context it just evaluated:

```csharp
public Decision<DisableAccount> Evaluate(DisableAccount command, DisableAccountContext context)
{
    if (context.AccountTenantId != context.Actor.TenantId)
        return new Decision<DisableAccount>.Denied("account.cross_tenant");

    if (context.AccountIsProtected)
        return new Decision<DisableAccount>.AcknowledgmentRequired("protected_account.second_approver_required");

    return new Decision<DisableAccount>.Allowed(
        new Permit<DisableAccount>(command, context.AccountVersion, PolicyVersion));
}
```

If a handler constructs the permit itself "to save a step," the design has quietly recreated Failure 2 inside the replacement.

### What the Permit Does and Does Not Guarantee

Be exact about this, because it is easy to overstate.

- **It does not prove a policy ran.** `internal` limits construction to one assembly. If the handler, policy, and executor all live in a single Application assembly — the common layout — any code in that assembly can construct a permit. Even with policy evaluation in its own assembly, `internal` is not a boundary against hostile code in the same process.
- **It does make skipping the policy a visible act.** Calling the executor now requires either a real decision or a deliberate `new Permit<…>(…)` that a reviewer, a text search, or an architecture test can flag. That is protection against the *accidental* bypass in Failures 2 and 5, which is the common one.
- **It only protects the effect if the executor is the only path to the effect.** If any code can call `IAccountStore.TryDisableAsync` directly, the permit is documentation. Keep the mutating method off general-purpose repositories, expose it only where the executor needs it, and treat a new caller of it as a review event.
- **It is ephemeral.** A permit is created and consumed inside one handler call. Do not persist or queue it. When execution must happen later, in another process, or on another actor's behalf, you need a different artifact — a bounded, expiring, replay-protected capability — which [Do You Need a Capability Token, or Are Roles and Claims Enough?](roles-claims-or-capability-token-dotnet.md) covers. `PolicyVersion` is carried here for the audit record, not re-checked, precisely because the permit never outlives the decision.

### Keep the Decision True Until the Write

Moving the check into the handler does not by itself remove the race that Failure 3 described. The handler still reads the account, evaluates, then writes. Between those moments another request can mark the account protected or move it to another tenant.

Close that window by binding the effect to the state that was evaluated. The context records the version it read, the permit carries it, and the store applies the change only if that version is still current. This protects the decision only if **every** change to state the decision depends on — tenant, protected flag, ownership — advances the checked version; a column updated without bumping the version slips past the check:

```sql
UPDATE Accounts
SET    IsDisabled = 1, Version = Version + 1
WHERE  Id = @AccountId AND Version = @ExpectedVersion;
-- zero rows affected → the account changed after the decision
```

In EF Core the same idea is a concurrency token (for example a `rowversion` column), with `DbUpdateConcurrencyException` mapped to the `Conflict` result. Running the evaluation and the write in one sufficiently isolated transaction is the other option; the conditional update is usually cheaper and keeps denied and pending paths out of the transaction.

When the write is rejected, the caller receives an explicit conflict and can re-submit, which re-evaluates the policy against the new state. What must not happen is a silent success against state the decision never saw.

### The Handler

The handler becomes an explicit sequence:

```csharp
public async Task<Result> HandleAsync(DisableAccount command, CancellationToken ct)
{
    var account = await _accounts.FindAsync(command.AccountId, ct);
    if (account is null)
    {
        return Result.NotFound();
    }

    var context = new DisableAccountContext(
        Actor: _user.Current,
        AccountTenantId: account.TenantId,
        AccountIsProtected: account.IsProtected,
        AccountVersion: account.Version,
        EvaluatedAt: _clock.UtcNow);

    var decision = _policy.Evaluate(command, context);

    return decision switch
    {
        Decision<DisableAccount>.Allowed allowed =>
            await _executor.ExecuteAsync(allowed.Permit, ct),

        Decision<DisableAccount>.Deferred d => Result.Deferred(d.ReasonCode),
        Decision<DisableAccount>.AcknowledgmentRequired a => Result.Pending(a.ReasonCode),
        Decision<DisableAccount>.EscalationRecommended e => Result.Escalated(e.ReasonCode),
        Decision<DisableAccount>.Denied n => Result.Denied(n.ReasonCode),

        _ => Result.Denied("decision.unrecognized_outcome"),
    };
}
```

Compare this with each failure above:

- **Outcome vocabulary.** Deferred, pending, escalated, and denied are ordinary return values. No exception crosses a retry wrapper.
- **Forgotten markers.** There is no marker to forget. The executor cannot be called without a permit, and producing one outside the policy is a visible act.
- **Request versus resource.** The context is built from the loaded account, not from `command.TenantId`, and the write is conditional on the version that was evaluated.
- **Registration order.** Reordering wrappers can no longer let a request reach the executor without a permit. An outer cache or idempotency wrapper can still answer *without* reaching the handler, so response replay and audit coverage remain a separate review item — which is why the table above asks for bound keys.
- **Other entry paths.** A background job or notification handler that wants to disable an account also needs a permit. Bypassing the policy now requires deliberately constructing one, which is visible in review.

The decorators stay. `LoggingDecorator` still wraps the handler and now logs a richer result: the outcome and reason code for every command that reaches it, uniformly.

This separation — proposal, then decision against authoritative context, then host-owned execution that requires the decision — is the pattern covered step by step in [Decision Before Execution](../../tutorials/decision-before-execution.md).

## Test the Path the Application Actually Uses

Pipeline-based protection is routinely under-tested for a structural reason: unit tests construct the handler directly, so they never pass through the pipeline at all. The protection exists only in the composed container, and the composed container is rarely under test.

Whichever design you choose, test through the real composition root:

```csharp
[Fact]
public async Task Protected_account_is_not_disabled_through_the_registered_handler()
{
    var store = new RecordingAccountStore();

    await using var provider = new ServiceCollection()
        .AddApplication()                       // the production registrations
        .Replace(ServiceDescriptor.Scoped<IAccountStore>(_ => store))
        .AddSingleton<ICurrentUser>(TenantAdministrator)
        .BuildServiceProvider();

    await using var scope = provider.CreateAsyncScope();
    var handler = scope.ServiceProvider
        .GetRequiredService<ICommandHandler<DisableAccount>>();

    var result = await handler.HandleAsync(
        new DisableAccount(ProtectedAccountId, TenantAdministrator.TenantId),
        CancellationToken.None);

    Assert.Equal(ResultKind.Pending, result.Kind);   // protected → acknowledgment required
    Assert.Empty(store.DisabledAccountIds);
}
```

The assertion that carries the weight is `Assert.Empty(store.DisabledAccountIds)`. A returned `Pending` result proves the handler *reported* the right outcome; zero calls to the store proves the protected effect did not occur. [How to Test That a Denied Operation Never Executes](test-denied-operation-never-executes.md) develops that distinction fully. A second test worth writing changes the account's version between evaluation and execution and asserts a `Conflict` with no effect.

If some checks remain in the pipeline, add a fail-closed coverage test so a forgotten declaration breaks the build rather than the audit. This sketch finds every **dispatched** command type — every `T` with a registered `ICommandHandler<T>`:

```csharp
[Fact]
public void Every_dispatched_command_declares_a_policy()
{
    var commandTypes = typeof(DisableAccount).Assembly
        .GetTypes()
        .SelectMany(t => t.GetInterfaces())
        .Where(i => i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(ICommandHandler<>))
        .Select(i => i.GetGenericArguments()[0])
        .Where(command => !command.IsGenericParameter)   // skip open decorators
        .Distinct();

    var undeclared = commandTypes
        .Where(t => !PolicyRegistry.HasPolicyFor(t))
        .Select(t => t.Name)
        .ToList();

    Assert.Empty(undeclared);
}
```

It will not see a command type that has no handler yet. If commands are defined separately from handlers, enumerate a command marker or registry instead. The same rule can also be enforced earlier, with an architecture-test library or a Roslyn analyzer, when the codebase is large enough to justify one.

Neither test is sophisticated. Both exist to fail during a future refactor — which is the only moment they matter.

## When a Pipeline Check Really Is Enough

A pipeline authorization check is a reasonable choice, and common in Clean Architecture-style templates, when **all** of these hold:

- The rule is coarse and uniform: authenticated, member of a role, holds a scope. It does not depend on the state of the specific resource.
- The outcome is genuinely binary. Nothing is ever deferred, escalated, or waiting on acknowledgment.
- Execution is immediate, in the same process and trust boundary, within the same request.
- The dispatcher is the only way handlers are invoked, and a test proves the composed pipeline contains the check.
- The consequence of a missed check is recoverable.

Many applications meet every condition for most of their commands. Queries often do too: there is no executor and no multi-outcome lifecycle, so a behavior or endpoint filter is frequently the right home — provided resource-scoped reads still filter by the caller's tenant or ownership in the query itself, and any response cache is keyed by caller.

[When ASP.NET Core Authorization Is Enough](../../architecture/when-aspnet-core-authorization-is-enough.md) and [When a Simple Application Service Is Enough](../../architecture/when-a-simple-application-service-is-enough.md) make the simpler case in detail.

### Three Layers, Not One

The practical split that works in many codebases has three layers, and each complements the others rather than replacing them:

- **Host:** authentication, endpoint authorization, and a fallback policy so no endpoint reaches the dispatcher anonymously by accident.
- **Pipeline:** coarse role or scope checks, input validation, telemetry, retries, transactions, and correctly keyed caching or idempotency.
- **Inside the operation:** anything that reads resource state, depends on time, has more than two outcomes, or would be expensive to get wrong.

Adoption does not have to be all at once. Run the checklist below across your commands, move only the highest-consequence ones — destructive, cross-tenant, financial, irreversible — into explicit decisions first, add the composition-root and conflict tests for each, and leave everything else in the pipeline until it earns the change.

The mistake is not using a pipeline. It is letting a pipeline designed for uniform mechanics quietly become the only thing deciding whether a consequential operation runs.

## A Short Review Checklist

1. **Can you name every entry path to this side effect?** Dispatcher, background jobs, message consumers, notification handlers, direct calls from other handlers, scripts. Does each path pass through the decision?
2. **Does the check read authoritative resource state, or only fields from the request?** Caller-supplied identifiers are proposals, not facts.
3. **How many outcomes does the rule really have?** If more than two, a generic wrapper is translating them into exceptions or sentinels.
4. **What happens when a command forgets its marker, attribute, or policy registration?** If the answer is "it runs," the check fails open.
5. **Which wrappers sit outside the authorization check, and what do they do with its exceptions or with cached results?** Retry, caching, idempotency, and exception-mapping wrappers are the usual suspects; cache and idempotency keys should be bound to caller and resource.
6. **Is the order of registration covered by a test that resolves the real container?**
7. **Could the effect run without a decision?** If the executor accepts a raw command, or the mutating store method has other callers, the answer is yes.
8. **Can the resource change between decision and write?** If so, is the write conditional on the evaluated version?
9. **Does the logging behavior record the decision outcome and reason code, not just success or failure?** That is the pipeline doing the job it is good at.

## Continue Deeper

To see the full proposal → decision → host-owned execution pattern with a runnable sample and tests, start with [Decision Before Execution](../../tutorials/decision-before-execution.md).

To practice the refactoring this article describes — pulling checks scattered across a codebase into one explicit decision step whose blocked outcomes cannot reach the side effect — work through the lab [Refactor Scattered Governance Checks into an Explicit Decision Pipeline](../../labs/refactor-scattered-governance-checks.md). Its companion, [Identify and Remove a Hidden Execution Side Effect](../../labs/hidden-execution-side-effect.md), covers the inverse mistake of evaluation code that performs the effect itself.

For the observability half — what a decision trace should contain, and why telemetry status must never become authorization state — see [AI Governance Observability and End-to-End Decision Tracing](../../ai-integration/ai-governance-observability-and-end-to-end-decision-tracing.md). The AI framing is incidental; the tracing guidance applies to any governed operation.

And when the question is what the decision record itself must prove afterwards, [Your Audit Log Records the Story, Not the Decision](your-audit-log-is-not-evidence.md) picks up where this article stops.

The rule to keep is short: **a generic pipeline by itself cannot know what any one operation means.** Put the watching in the pipeline, and put the deciding where the meaning is.
