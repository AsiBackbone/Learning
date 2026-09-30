---
description: Policy-as-code makes decisions explicit and testable. Choose between plain code, ASP.NET Core authorization, in-process policy, engines, and remote PDPs.
title: Policy as Code in ASP.NET Core Without Overengineering
author: Christopher D. Cavell
published: "2026-09-30"
summary: Policy-as-code is a way to make decision logic explicit, reviewable, and testable, not a requirement to adopt a separate engine or remote service. This guide follows one ASP.NET Core refund endpoint to show when ordinary code or framework authorization is enough, and which real pressures justify an in-process policy component, an embedded engine, or a remote decision service.
feed: true
x_hashtags:
  - DotNet
  - SoftwareArchitecture
---

# Policy as Code in ASP.NET Core Without Overengineering

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** Familiarity with C# and ASP.NET Core authorization is helpful. No AsiBackbone package, policy engine, or prior Learning material is required.

**What this article covers:** what policy-as-code means in plain terms; five places a policy decision can live in an ASP.NET Core application, from ordinary code to a remote decision service; which pressures justify each step and what each step costs; how to handle latency, availability, caching, freshness, failure, and version provenance when evaluation is remote; what should stay outside the policy evaluator whatever you choose; and the failure modes that show up when policy placement is chosen as a maturity badge instead of for a reason.

A team's refund endpoint has grown a little at a time:

```csharp
if (user.IsInRole("SupportTier1") && request.Amount > 250) return Results.Forbid();
if (user.IsInRole("SupportTier2") && request.Amount > 1000) return Results.Forbid();
if (order.HasOpenChargeback) return Results.BadRequest("Chargeback open");
if ((DateTime.UtcNow - order.PurchasedAt).TotalDays > 90 && !request.AcknowledgedLateRefund)
    return Results.BadRequest("Acknowledge late refund");
```

Finance wants to change the tier limits next quarter. Risk wants high-risk customers held for review. Someone notices that an agent can simply send `"acknowledgedLateRefund": true` in the request body. In the design review, a proposal appears: *adopt policy-as-code*. It comes with a diagram showing a new policy service, a policy language, a deployment pipeline, and a sidecar.

That might be the right answer. More often, it is four steps further than the problem requires. The code above has real defects: rules scattered through an endpoint, Boolean outcomes that erase the difference between "no" and "ask a supervisor," and a trusted fact supplied by the caller. None of those defects is caused by the rules living in the application's own process, and none of them is fixed by moving the rules somewhere else.

**The short version.** Policy-as-code means decision logic you can point to, read, review, test, and identify by version. It does not mean a particular engine, language, or deployment topology. In an ASP.NET Core application, the progression usually looks like this: ordinary code, then framework authorization for actor-and-resource access, then a dedicated in-process policy component with an explicit context and explicit outcomes. An embedded engine and a remote decision service are real options, and each is justified by specific pressures, such as rule authors who are not developers, many services that must enforce the same rules, or a policy owner in a separate trust boundary. "We want to be mature" is not one of those pressures.

> Policy-as-code is a way to make decision logic explicit, reviewable, and testable, not a requirement to adopt a separate engine or remote service. Choose the smallest policy boundary that satisfies the system's real change, reuse, trust, and operational needs.

## What Policy-as-Code Actually Means

Strip away the tooling and policy-as-code has four properties:

| Property | What it means in practice |
| --- | --- |
| **Explicit** | The decision lives in one identifiable place, with named inputs and named outcomes, not in a scatter of `if` statements across endpoints. |
| **Reviewable** | The rules are in source control, changes go through review, and a diff shows what changed in the decision, not only in the plumbing around it. |
| **Testable** | Given the same inputs, the decision is deterministic, and the rules can be exercised directly, without an HTTP request, a database, or a live service. |
| **Identifiable** | You can say which version of the policy produced a decision, and you can keep that alongside the decision when it matters later. |

A C# class in your web project can have all four. A rule in a sophisticated engine can have none of them if it is edited in production through an admin screen with no history and no tests.

This is also why the question "Do we have policy-as-code yet?" is less useful than "Which of these properties is missing, and what is the smallest change that adds it?" The opening example is missing *explicit* and *testable*. Neither requires a new runtime.

## The Example: Refunds in a Support Application

Every option below is applied to one operation. A support agent issues a refund on a customer's order through `POST /orders/{orderId}/refunds`. The current rules, owned by the finance operations team, are:

- Only agents with the refund permission, assigned to the order's region, may issue refunds on it.
- A refund may not exceed what is still refundable on the order.
- Tier 1 agents may refund up to 250, and tier 2 agents up to 1,000, in the store's settlement currency. Larger refunds need a supervisor's approval.
- Orders with an open chargeback may not be refunded.
- Customers the risk service rates as high risk are held for review, whatever the amount.
- Refunds more than 90 days after purchase require the agent to acknowledge a late-refund notice first.

Look closely and that list contains three kinds of rule:

1. **Access control.** *May this agent act on this order?* That is actor-and-resource authorization.
2. **A domain invariant.** *A refund may not exceed the refundable balance.* That is true regardless of who asks or what finance decides next quarter. It belongs to the order, not to a policy.
3. **An operational decision with more than two outcomes.** Allowed, denied, needs a supervisor, needs an acknowledgment, or cannot be decided right now because the risk service did not answer.

Many policy-as-code discussions go wrong by treating all three as one thing called "policy" and choosing one place for all of them. The design gets simpler once they are separated.

## Five Places a Policy Decision Can Live

The options below are ordered by how much new machinery they introduce, not by how good they are. Every one of them is the right answer for some systems.

### 1. Ordinary application code

If the only rule were "tier 1 agents may refund up to 250," a well-named method in the application service is a complete, respectable implementation:

```csharp
public sealed class RefundService(IOrderStore orders, IRefundExecutor executor)
{
    // Refund limits by agent tier. Owned by finance operations; see ADR-014.
    private static decimal LimitFor(AgentTier tier) => tier switch
    {
        AgentTier.Tier1 => 250m,
        AgentTier.Tier2 => 1_000m,
        _ => 0m,
    };

    public async Task<RefundResult> RefundAsync(Agent agent, string orderId, decimal amount, CancellationToken ct)
    {
        var order = await orders.GetAsync(orderId, ct);

        if (amount > LimitFor(agent.Tier))
        {
            return RefundResult.OverAgentLimit;
        }

        return await executor.RefundAsync(order, amount, ct);
    }
}
```

The rule is in one place, reviewed in pull requests, and testable with a unit test. It changes when the application changes, which is fine because the same team owns both. That is policy-as-code in every sense that matters.

The domain invariant belongs even closer to the data. `Order.RefundableBalance`, enforced by the order itself or by the executor inside the same transaction that records the refund, is not a policy decision and should not move into a policy component. Policy can change on Tuesday; "you cannot refund money you never captured" cannot.

**Stay here when** the rules are few, local, owned by the application team, and answered with success or an ordinary application error. [When a Simple Application Service Is Enough](../../architecture/when-a-simple-application-service-is-enough.md) explores this boundary in depth.

### 2. ASP.NET Core authorization policies and handlers

The first rule, "agents with the refund permission, assigned to the order's region," is access control, and ASP.NET Core has a well-designed home for it: a named policy with a resource-based handler.

```csharp
public sealed class RefundOrderRequirement : IAuthorizationRequirement;

public sealed class RefundOrderHandler : AuthorizationHandler<RefundOrderRequirement, Order>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RefundOrderRequirement requirement,
        Order order)
    {
        if (context.User.HasClaim("permission", "orders.refund")
            && context.User.HasClaim("region", order.Region))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Orders.Refund", policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new RefundOrderRequirement()));

builder.Services.AddSingleton<IAuthorizationHandler, RefundOrderHandler>();
```

This is policy-as-code too. The rule has a name, a single implementation, framework integration with endpoints and `IAuthorizationService`, and it is easy to test by constructing an `AuthorizationHandlerContext`. The endpoint loads the order first, from its own store, and passes it in as the resource, so the region comes from the server's copy of the order, not from the request.

Notice what the handler does *not* do. It does not call the risk service, query the database, or decide whether a supervisor is needed. Authorization answers *may this actor do this to this resource*, and its result is succeed or fail. The refund rules about tier limits, risk holds, and late-refund notices produce outcomes such as "ask a supervisor" or "acknowledge first." Forcing those into `Succeed` and `Fail` either loses them or smuggles them through failure reasons that the rest of the application has to parse.

**Stay here when** the question is fundamentally actor-and-resource access and pass or fail is a complete answer. For most access-control rules in most applications, this is not a stepping stone to something better; it is the destination. [When ASP.NET Core Authorization Is Enough](../../architecture/when-aspnet-core-authorization-is-enough.md) explains why.

### 3. A dedicated in-process policy component

The remaining refund rules have outgrown both an inline method and an authorization handler. They have more than two outcomes, they read facts from several places, finance will change them on its own schedule, and support leads will ask "why was this refund held?" The next step is still in-process. It is a small, explicit boundary with three parts: a context, an evaluator, and a decision.

```csharp
public enum RefundOutcome
{
    Allowed,
    Denied,
    RequiresAcknowledgment,
    Deferred,
    Unavailable,
}

public sealed record RefundDecision(RefundOutcome Outcome, string ReasonCode, string PolicyVersion)
{
    public static RefundDecision Unavailable(string reasonCode) =>
        new(RefundOutcome.Unavailable, reasonCode, PolicyVersion: "none");
}

// Every value is supplied by the host from a trusted source, never copied from the request body.
public sealed record RefundPolicyContext(
    AgentTier AgentTier,
    decimal Amount,                 // the requested amount, already validated against the refundable balance
    int DaysSincePurchase,
    bool HasOpenChargeback,
    RiskBand CustomerRisk,
    bool LateRefundAcknowledged);   // true only if the host holds an acknowledgment record for this refund

public interface IRefundPolicy
{
    RefundDecision Evaluate(RefundPolicyContext context);
}
```

The evaluator is a pure function of its context:

```csharp
public sealed class RefundPolicy : IRefundPolicy
{
    public const string Version = "refund-policy/2026-09";

    public RefundDecision Evaluate(RefundPolicyContext context)
    {
        // Precedence is part of the policy: a chargeback denies even a small refund,
        // and a risk hold applies before the amount is considered.
        if (context.HasOpenChargeback)
        {
            return Decide(RefundOutcome.Denied, "refund.chargeback-open");
        }

        if (context.CustomerRisk == RiskBand.High)
        {
            return Decide(RefundOutcome.Deferred, "refund.high-risk-review");
        }

        if (context.Amount > LimitFor(context.AgentTier))
        {
            return Decide(RefundOutcome.Deferred, "refund.requires-supervisor");
        }

        if (context.DaysSincePurchase > 90 && !context.LateRefundAcknowledged)
        {
            return Decide(RefundOutcome.RequiresAcknowledgment, "refund.late-refund-notice");
        }

        return Decide(RefundOutcome.Allowed, "refund.within-agent-limit");
    }

    private static decimal LimitFor(AgentTier tier) => tier switch
    {
        AgentTier.Tier1 => 250m,
        AgentTier.Tier2 => 1_000m,
        _ => 0m,
    };

    private static RefundDecision Decide(RefundOutcome outcome, string reasonCode) =>
        new(outcome, reasonCode, Version);
}
```

And the endpoint composes the three kinds of rule, each in its own place:

```csharp
app.MapPost("/orders/{orderId}/refunds", async (
    string orderId,
    RefundRequest request,
    ClaimsPrincipal user,
    IAuthorizationService authorization,
    IOrderStore orders,
    RefundContextBuilder contexts,
    IRefundPolicy policy,
    IRefundDecisionLog decisions,
    IRefundApprovals approvals,
    IRefundExecutor executor,
    CancellationToken ct) =>
{
    var order = await orders.FindAsync(orderId, ct);
    if (order is null)
    {
        return Results.NotFound();
    }

    // 1. Access control: framework authorization against the server's copy of the order.
    var access = await authorization.AuthorizeAsync(user, order, "Orders.Refund");
    if (!access.Succeeded)
    {
        return Results.Forbid();
    }

    // 2. Domain invariant: not policy. The executor enforces it again atomically.
    if (request.Amount <= 0 || request.Amount > order.RefundableBalance)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["amount"] = ["Amount must be positive and no more than the refundable balance."],
        });
    }

    // 3. Operational policy: the host builds trusted context, then asks the policy.
    var built = await contexts.BuildAsync(user, order, request, ct);
    var decision = built.Context is null
        ? RefundDecision.Unavailable(built.ReasonCode)
        : policy.Evaluate(built.Context);

    await decisions.RecordAsync(order.Id, request, decision, ct);

    // The host, not the policy, decides what each outcome does.
    return decision.Outcome switch
    {
        RefundOutcome.Allowed =>
            Results.Ok(await executor.RefundAsync(order.Id, request.Amount, ct)),
        RefundOutcome.RequiresAcknowledgment =>
            Results.Conflict(new { decision.ReasonCode }),
        RefundOutcome.Deferred =>
            Results.Accepted(value: await approvals.OpenAsync(order.Id, request, decision, ct)),
        RefundOutcome.Denied =>
            Results.UnprocessableEntity(new { decision.ReasonCode }),
        RefundOutcome.Unavailable =>
            Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Refund decision unavailable"),
        _ => throw new UnreachableException(),
    };
});
```

`RefundContextBuilder` is where the trust work happens. It reads the agent's tier from the directory or a token claim the identity provider issued, not from the request. It calls the risk service and returns no context at all, with a reason code such as `risk.unavailable`, if the risk service does not answer. It looks up an acknowledgment record bound to this refund instead of believing an `acknowledgedLateRefund` flag in the body. The request says what the agent *wants*; the host says what is *true*.

This step fixes every defect in the opening example without introducing any new infrastructure:

- **Explicit.** One class holds the operational refund rules, with named outcomes and reason codes.
- **Reviewable.** A change to the tier limits is a small diff in one file, and `Version` changes with it.
- **Testable.** `RefundPolicy.Evaluate` is a pure function, so a decision table can cover it exhaustively in milliseconds.
- **Identifiable.** Every recorded decision carries the policy version that produced it.

`Version` is a hand-maintained string here, which is enough when policy ships with the application and its history is in source control. If you need to prove exactly which rules produced a decision, record the build or commit as well, or a fingerprint of the rule content. [Policy Versioning and Decision Provenance](../../governance/policy-versioning-and-decision-provenance.md) covers the difference between a version label and content identity.

**Stay here when** the rules need a clear boundary, richer outcomes, independent tests, or version evidence, but developers still author them and one application enforces them. This is where many teams that ask for policy-as-code actually need to land. [Policy Context and Explicit Decision Outcomes](../../tutorials/policy-context-and-explicit-decision-outcomes.md) develops the context-and-outcome model in more detail.

### 4. An embedded rules or policy engine

Suppose the refund rules grow in a different direction. Finance now maintains dozens of limits by region, product line, payment method, and customer segment. They want to edit a decision table themselves, preview the effect on last month's refunds, and publish a change without waiting for a sprint. The rules have overlapping conditions, and "which rule wins" has become a real question.

That is a pressure an engine is built for: specialized rule authoring, composition and conflict semantics, and simulation. An embedded engine runs in the application's process, evaluating rules loaded as data. Keep the boundary you built in step 3, and put the engine behind it:

```csharp
public sealed class RuleSetRefundPolicy(IRuleEngine engine, IActiveRuleSet rules) : IRefundPolicy
{
    public RefundDecision Evaluate(RefundPolicyContext context)
    {
        // An immutable, validated rule set. Activation replaces it atomically;
        // evaluation never sees a half-loaded set.
        var ruleSet = rules.Current;
        var result = engine.Evaluate(ruleSet, context);

        return result.Outcome switch
        {
            "allow" => new(RefundOutcome.Allowed, result.ReasonCode, ruleSet.Version),
            "deny" => new(RefundOutcome.Denied, result.ReasonCode, ruleSet.Version),
            "defer" => new(RefundOutcome.Deferred, result.ReasonCode, ruleSet.Version),
            "acknowledge" => new(RefundOutcome.RequiresAcknowledgment, result.ReasonCode, ruleSet.Version),

            // An outcome the host does not understand is never treated as permission.
            _ => RefundDecision.Unavailable("policy.unrecognized-outcome"),
        };
    }
}
```

The endpoint, the context builder, and the tests of the endpoint do not change. What changes is where the rules live and who can change them, and that brings new obligations:

- **The rule set is now a deployable artifact.** It needs the same review, validation, and rollback discipline as code, or it becomes an unreviewed production edit with a nicer editor.
- **Version provenance moves with it.** `ruleSet.Version` should identify the exact published content, ideally with a content hash, and rule sets should be immutable once published.
- **Testing needs a second layer.** The decision table that tested `RefundPolicy` now has to run against each published rule set, including its conflict resolution.
- **Debugging crosses a language boundary.** "Why was this held?" is answered by the engine's trace, not by stepping through C#.

**Choose this when** rule authoring, composition, or evaluation semantics genuinely need specialized machinery. Do not choose it to make a dozen `if` statements look more serious.

### 5. A remote policy decision point

The last option moves evaluation out of the process entirely: the application sends context to a policy service and enforces the decision it returns. In common terminology, the policy service is a *policy decision point* (PDP) and the code that acts on its answer is a *policy enforcement point* (PEP).

The pressures that justify this are specific:

- **Cross-service reuse.** Refunds can now be issued by the support application, a partner API written in another language, and a back-office batch job, and all three must apply the same rules.
- **Independent deployment and ownership.** A risk team owns refund policy, changes it several times a week, and must not wait for three application release trains.
- **Centralized administration.** Auditors need one place to see which refund policy was in force, everywhere, at a given time.
- **A distinct trust boundary.** The team that writes the application should not be able to change the rules that constrain it.

If none of those is true, a remote PDP adds a network dependency to every refund and buys nothing the in-process component did not already provide. It is not more secure by default. It is a different trust and failure model, and it is only better when that model matches the system.

When it is justified, the change in the contract is the honest part:

```csharp
public sealed class RemoteRefundPolicyClient(HttpClient http)
{
    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(300);

    public async Task<RefundDecision> EvaluateAsync(RefundPolicyContext context, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Budget);

        try
        {
            using var response = await http.PostAsJsonAsync("v1/decisions/refund", context, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return RefundDecision.Unavailable("policy.service-error");
            }

            var body = await response.Content.ReadFromJsonAsync<RemoteDecision>(timeout.Token);
            return Map(body);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return RefundDecision.Unavailable("policy.timeout");
        }
        catch (HttpRequestException)
        {
            return RefundDecision.Unavailable("policy.unreachable");
        }
        catch (JsonException)
        {
            return RefundDecision.Unavailable("policy.malformed-response");
        }
    }

    private static RefundDecision Map(RemoteDecision? body)
    {
        // No policy version means no provenance: the answer cannot be attributed or trusted.
        if (body is null || string.IsNullOrEmpty(body.PolicyVersion) || string.IsNullOrEmpty(body.ReasonCode))
        {
            return RefundDecision.Unavailable("policy.incomplete-response");
        }

        return body.Outcome switch
        {
            "allow" => new(RefundOutcome.Allowed, body.ReasonCode, body.PolicyVersion),
            "deny" => new(RefundOutcome.Denied, body.ReasonCode, body.PolicyVersion),
            "defer" => new(RefundOutcome.Deferred, body.ReasonCode, body.PolicyVersion),
            "acknowledge" => new(RefundOutcome.RequiresAcknowledgment, body.ReasonCode, body.PolicyVersion),
            _ => RefundDecision.Unavailable("policy.unrecognized-outcome"),
        };
    }

    private sealed record RemoteDecision(string? Outcome, string? ReasonCode, string? PolicyVersion);
}
```

Evaluation is now asynchronous, has a latency budget, can fail in several distinct ways, and returns a policy version this application did not deploy. Every one of those needs a deliberate answer, covered in [Remote Evaluation Has Its Own Obligations](#remote-evaluation-has-its-own-obligations) below.

The same context builder still runs *before* the call. A remote PDP evaluates what it is given; it does not make the caller's facts true. If the application sends `"customerRisk": "Low"` because the request body said so, the PDP will faithfully allow a refund it should have held.

## Compare the Options

| | Ordinary code | ASP.NET Core authorization | In-process policy component | Embedded engine | Remote PDP |
| --- | --- | --- | --- | --- | --- |
| **Best at** | Local rules owned by the app team | Actor-and-resource access | Operational decisions with rich outcomes | Many composable rules authored outside development | One policy enforced by many services, owned separately |
| **Who changes it** | Application team | Application team | Application team, often with a policy owner reviewing | Rule authors through the engine's tooling | Policy team, on its own release cadence |
| **Change cadence** | With the application | With the application | With the application | Independent of application releases | Independent of all consuming services |
| **Outcomes** | Application results | Succeed or fail | Explicit enum with reason codes | Whatever the engine returns, mapped by the host | Whatever the service returns, mapped by the host |
| **Testing** | Unit tests | Handler tests | Decision tables against a pure function | Decision tables per published rule set, plus simulation | Contract tests against the service and each policy version |
| **Version provenance** | Build or commit | Build or commit | Explicit version recorded with each decision | Rule set version and content hash | Version returned per decision, recorded by the caller |
| **Latency** | None added | None added | None added | Engine evaluation cost | A network round trip per decision, unless cached |
| **Availability** | Same as the app | Same as the app | Same as the app | Same as the app, plus rule-set loading | New dependency with its own outages |
| **Caching and freshness** | Not applicable | Not applicable | Not applicable | Rule-set staleness | Decision or bundle staleness, invalidation |
| **Operational burden** | Lowest | Low | Low | Moderate: tooling, publishing, a second language | Highest: a service, its deployment, monitoring, and failure modes |
| **Portability** | Tied to this app | Tied to ASP.NET Core | Tied to this app, easy to extract later | Tied to the engine | Language-neutral for callers |

Two rows deserve emphasis. **Who changes it** and **change cadence** are the rows that most often justify moving right, and they are organizational facts, not technical ones. **Availability** is the row most often forgotten until the first outage.

## A Selection Flow

```text
Is the rule an invariant of the data itself (balances, uniqueness, state transitions)?
  yes → keep it in the domain model or executor, enforced atomically. It is not policy.

Is it "may this actor do this to this resource", and is pass/fail a complete answer?
  yes → ASP.NET Core authorization: a named policy, a resource-based handler.

Does it fit comfortably in ordinary application code owned by the same team?
  yes → keep it local, explicit, and tested.

Does it need richer outcomes, a trusted context, version evidence, or its own tests?
  yes → a dedicated in-process policy component.

Does it need specialized authoring, composition, or simulation by non-developers?
  yes → an embedded engine behind the same component boundary.

Must several services enforce it, must it deploy independently, or must its owner
sit in a separate trust boundary?
  yes → a remote decision point, with explicit answers for latency, availability,
        caching, freshness, failure, and provenance.
```

The flow is not a ladder you are expected to climb. Most applications stop at the third or fourth line for most rules, and one application commonly uses several rows at once: authorization for access, a domain invariant in the executor, and an in-process component for operational decisions, exactly as in the refund endpoint above.

It is also reversible in one direction. Because step 3 introduced `IRefundPolicy` with an explicit context and outcomes, moving to an engine or a remote service later is an implementation change behind a stable seam. Skipping step 3 and going straight to a remote service tends to leak the service's request format, error model, and outcome strings into every caller.

## Signals That the Current Boundary Is Too Small, or Too Large

**Signs you need more boundary than you have:**

- The same rule is implemented in two places, and they have started to disagree.
- Support or audit asks "why was this held?", and nobody can answer without reading endpoint code.
- An authorization handler has started calling other services or returning failure reasons that callers parse to decide what to do next.
- Rule changes wait weeks for a release even though the policy owner needs them in days.
- Several services, possibly in different languages, must apply the same decision.

**Signs you have more boundary than you need:**

- The remote policy service has one caller, owned by the same team, deployed on the same schedule.
- Every rule in the engine was written by a developer and could be a `switch` expression.
- The policy service's outage runbook is longer than its rule set.
- Nobody can name which of reuse, independent ownership, specialized authoring, or trust separation the extra boundary provides.

## What Stays Outside the Policy Evaluator

Whatever you choose, the evaluator decides. It does not gather its own facts, run the workflow, or perform the side effect. In the refund endpoint, those responsibilities stay with the host:

- **Authoritative context.** The host resolves the agent's tier, the order's region and chargeback status, the customer's risk band, and whether an acknowledgment record exists. The evaluator receives those facts; it does not fetch them from wherever it likes, and it never accepts them from the caller.
- **Workflow state.** `Deferred` means "a supervisor must decide." Opening the approval request, assigning it, reminding, expiring, and recording the supervisor's answer belong to the host or a workflow component. The policy should not hold approval state, and it should not change its answer because an approval row exists unless the host passes that fact in explicitly.
- **Protected side effects.** The refund itself is issued by `IRefundExecutor`, which enforces the refundable-balance invariant atomically and is idempotent on its request ID. A policy that "allows and also issues the refund" cannot be tested without moving money and cannot be reused by a caller that needs to ask before acting.
- **Execution-time re-evaluation.** When a supervisor approves a deferred refund on Wednesday, the executor should not run on the strength of Monday's decision. It builds a fresh context and asks again, now with the approval as an input. [Authorization vs. Approval vs. Acknowledgment](authorization-vs-approval-vs-acknowledgment.md) shows that pattern end to end.

This division is what makes the policy boundary replaceable. An evaluator that only maps a context to a decision can move from a class to an engine to a service. One that also opens tickets and calls the payment gateway cannot move anywhere.

## Remote Evaluation Has Its Own Obligations

If you do adopt a remote decision point, each of these needs a written answer before the first production refund depends on it.

**Latency.** Set a budget per decision, like the 300 ms above, and measure it. A refund endpoint that used to take 80 ms now takes 80 ms plus a round trip, plus retries if you add them. Decide whether retries are worth their latency for this operation, or whether one attempt and `Unavailable` is the better contract.

**Availability and failure behavior.** "Fail closed" is the right default for moving money, and the client above returns `Unavailable` for every failure. But "fail closed" is not a complete plan. Decide what the user sees, whether the refund can be queued and re-evaluated when the service recovers, and who is alerted. An outage of the policy service is now an outage of refunds, in every service that calls it.

**Policy caching versus decision caching.** These solve different problems:

- A **policy cache** keeps a copy of the rules close to the caller, often as a signed bundle evaluated locally. The question is whether *this policy version* is still acceptable to use. Give each bundle a maximum age, and treat a bundle older than that as `Unavailable`, not as current.
- A **decision cache** keeps a prior answer. The question is much broader: are the policy, the agent, the order, the customer's risk band, and the amount all still equivalent? A cached `Allowed` from five minutes ago says nothing about a chargeback opened four minutes ago.

If you cache decisions at all, key them by policy version *and* a fingerprint of the full context, and prefer caching denials over caching permission. For operations that move money or data, local evaluation of a fresh policy bundle is usually a better answer than caching decisions from a remote service.

**Freshness and invalidation.** Decide how quickly a policy change must take effect everywhere. If the answer is "immediately," you need an invalidation channel and a way to know every caller received it. If the answer is "within ten minutes," write that down, because during those ten minutes different services are enforcing different policies.

**Version provenance and evidence.** Record the policy version the service *returned*, not the version you think is deployed. Keep the reason code, the decision time, and enough of the context to explain the decision later, or a fingerprint of it. A log line that says "policy allowed refund" cannot answer which policy, or why. [Your Audit Log Records the Story, Not the Decision](your-audit-log-is-not-evidence.md) covers what decision evidence needs.

**Enforcement is still local.** Centralizing policy administration does not mean every refund path asks the policy. The back-office batch job, the partner API, the admin console's "fix it" button, and the database script a developer runs at 2 a.m. are all enforcement points, or bypasses. Keep an inventory of every path that can issue a refund, and have the executor itself refuse refunds that do not come through a checked path.

## Failure Modes

Each of these starts as a reasonable decision.

### 1. A remote policy service adopted as a maturity badge

The team builds a policy service because "mature organizations externalize policy." It has one caller, the same owners, and the same release cadence as that caller. The application now has a new network dependency, a new deployment, and a new failure mode, and the rules are no more explicit or better tested than a class would have made them. Name the pressure that justifies each boundary. If you cannot, use a smaller one.

### 2. Database and network calls hidden inside authorization handlers

`RefundOrderHandler` grows a call to the risk service and a query for open chargebacks. Now authorization can time out, and when it does, the framework reports it as a failure indistinguishable from "not allowed," or as an unhandled exception. Keep handlers to decisions over the principal and a resource the endpoint already loaded. Put decisions that need other dependencies in a component with an explicit `Unavailable` outcome.

### 3. Callers supplying authoritative facts

The request body includes `agentTier`, `customerRisk`, or `acknowledgedLateRefund`, and the endpoint copies them into the policy context. The policy evaluates flawlessly and returns the answer the caller wanted. Every fact the policy relies on must come from a source the host trusts: the authenticated principal, its own stores, or a service it calls. This matters even more with a remote PDP, where it is easy to forward a request body as the policy input unchanged.

### 4. Caching decisions without binding them to policy and context

A decision cache keyed by `(agentId, orderId)` with a five-minute TTL returns `Allowed` for a refund of 900 because an earlier request for 90 was allowed. Or it keeps returning `Allowed` after the policy version changed. A cached decision is only valid for the exact policy version and context that produced it. If you cannot build that key cheaply, do not cache the decision; cache the policy.

### 5. Flattening rich outcomes into one Boolean

`bool CanRefund(...)` returns `false` for "chargeback open," "needs a supervisor," "acknowledge the late-refund notice," and "risk service down." The endpoint can only show one error, so agents learn to ask a supervisor for everything, or retry until the risk service answers. `Denied`, `Deferred`, `RequiresAcknowledgment`, and `Unavailable` lead to different next steps, and the host can only choose the right one if the decision tells it which.

### 6. Workflow orchestration and side effects inside the evaluator

The policy returns `Deferred` and also creates the approval ticket. Then it starts issuing the refund when the amount is small enough. Now evaluation has side effects, running it twice creates two tickets, and a "what would the policy say?" simulation moves real money. Evaluation should be safe to run any number of times. Anything that changes the world belongs to the host.

### 7. Assuming central administration guarantees enforcement

The risk team publishes a new rule, sees it active in the policy service's console, and considers refunds protected. The partner API was never wired to the service, and the batch job caches decisions for a day. Central administration tells you what the policy *says*. Only an inventory of enforcement points, and tests at each one, tell you where it is *applied*.

## Test the Decision and the Boundary

The in-process component makes the most valuable test cheap: a decision table that states the policy as rows and checks each one against the evaluator.

```csharp
public sealed class RefundPolicyTests
{
    private static readonly RefundPolicy Policy = new();

    private static readonly RefundPolicyContext Baseline = new(
        AgentTier: AgentTier.Tier1,
        Amount: 100m,
        DaysSincePurchase: 10,
        HasOpenChargeback: false,
        CustomerRisk: RiskBand.Low,
        LateRefundAcknowledged: false);

    public static TheoryData<string, RefundPolicyContext, RefundOutcome, string> Rows => new()
    {
        { "tier 1 at limit", Baseline with { Amount = 250m }, RefundOutcome.Allowed, "refund.within-agent-limit" },
        { "tier 1 just over limit", Baseline with { Amount = 250.01m }, RefundOutcome.Deferred, "refund.requires-supervisor" },
        { "tier 2 at limit", Baseline with { AgentTier = AgentTier.Tier2, Amount = 1_000m }, RefundOutcome.Allowed, "refund.within-agent-limit" },
        { "chargeback denies a small refund", Baseline with { Amount = 5m, HasOpenChargeback = true }, RefundOutcome.Denied, "refund.chargeback-open" },
        { "high risk held under limit", Baseline with { CustomerRisk = RiskBand.High }, RefundOutcome.Deferred, "refund.high-risk-review" },
        { "late refund needs notice", Baseline with { DaysSincePurchase = 91 }, RefundOutcome.RequiresAcknowledgment, "refund.late-refund-notice" },
        { "late refund acknowledged", Baseline with { DaysSincePurchase = 91, LateRefundAcknowledged = true }, RefundOutcome.Allowed, "refund.within-agent-limit" },
        { "day 90 is not late", Baseline with { DaysSincePurchase = 90 }, RefundOutcome.Allowed, "refund.within-agent-limit" },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void Refund_policy_decision_table(
        string row, RefundPolicyContext context, RefundOutcome outcome, string reasonCode)
    {
        var decision = Policy.Evaluate(context);

        Assert.True(
            decision.Outcome == outcome && decision.ReasonCode == reasonCode,
            $"{row}: expected {outcome}/{reasonCode}, got {decision.Outcome}/{decision.ReasonCode}");
        Assert.Equal(RefundPolicy.Version, decision.PolicyVersion);
    }
}
```

The rows are the policy, written so that finance can read them. Boundary values, such as exactly 250 and exactly day 90, and precedence cases, such as a chargeback on a small refund, are where real policies break. If you later move the rules into an engine or a remote service, keep the table and run it against the new implementation. Passing the same rows is how you show the move changed where the policy lives, not what it says.

A decision table proves the evaluator. It does not prove the endpoint honors it. Test that separately, and assert on what the executor did, not only on the status code:

```csharp
[Fact]
public async Task Over_limit_refund_opens_an_approval_and_does_not_execute()
{
    await using var app = RefundApi.WithAgent(Tier1Agent).WithOrder(OrderWithBalance(900m));

    var response = await app.ClientFor(Tier1Agent)
        .PostAsJsonAsync($"/orders/{OrderId}/refunds", new { amount = 400m });

    Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    Assert.Empty(app.Executor.Calls);
    Assert.Single(app.Approvals.Opened);
}

[Fact]
public async Task Unavailable_risk_service_does_not_execute()
{
    await using var app = RefundApi.WithAgent(Tier1Agent).WithOrder(OrderWithBalance(900m));
    app.Risk.FailWith(new HttpRequestException());

    var response = await app.ClientFor(Tier1Agent)
        .PostAsJsonAsync($"/orders/{OrderId}/refunds", new { amount = 50m });

    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    Assert.Empty(app.Executor.Calls);
}

[Fact]
public async Task Acknowledgment_flag_in_request_body_is_ignored()
{
    await using var app = RefundApi.WithAgent(Tier1Agent).WithOrder(OrderPurchasedDaysAgo(120));

    var response = await app.ClientFor(Tier1Agent)
        .PostAsJsonAsync($"/orders/{OrderId}/refunds", new { amount = 50m, acknowledgedLateRefund = true });

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Empty(app.Executor.Calls);
}
```

`RefundApi` is a small harness over `WebApplicationFactory` that seeds agents and orders, fakes the risk service, and records executor and approval calls. [How to Test That a Denied Operation Never Executes](test-denied-operation-never-executes.md) explains how to build the recording executor and why an empty call list is stronger evidence than a status code. [Practical Policy Testing and Decision-Table Strategies](../../governance/practical-policy-testing-and-decision-table-strategies.md) covers equivalence classes, boundary values, conflicting rules, and testing historical policy versions.

## How This Maps to Learning

The Learning curriculum describes the same responsibilities with its own vocabulary. You do not need it to apply this article, but it helps when reading further:

| This article | Learning curriculum |
| --- | --- |
| Trusted context built by the host | Authoritative context construction, in [Policy Context and Explicit Decision Outcomes](../../tutorials/policy-context-and-explicit-decision-outcomes.md) |
| `Allowed`, `Denied`, `Deferred`, `RequiresAcknowledgment` | Explicit governance outcomes, including acknowledgment and escalation |
| `Unavailable` | Dependency unavailability, tested separately from explicit denial |
| Policy version recorded with each decision | Policy identity, version, and fingerprint, in [Policy Versioning and Decision Provenance](../../governance/policy-versioning-and-decision-provenance.md) |
| Remote decision point and enforcement points | PDP and PEP placement, in [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../../architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md) |
| The executor that issues the refund | Host-owned execution |

These are the terms this repository uses consistently. They are not an industry standard.

## A Short Review Checklist

**Placement**

1. Can you name the pressure, such as reuse, independent ownership, specialized authoring, or trust separation, that justifies each boundary beyond ordinary code and framework authorization?
2. Are domain invariants kept in the domain or executor rather than moved into policy?
3. Do authorization handlers decide over the principal and an already-loaded resource, without hidden network or database calls?

**Contract**

4. Does the policy boundary have an explicit context and explicit outcomes, rather than a Boolean?
5. Is every fact in the context supplied by a trusted source, never copied from the request?
6. Does an unrecognized or incomplete answer from an engine or service map to `Unavailable`, never to `Allowed`?

**Responsibilities**

7. Does the evaluator stay free of workflow state and side effects, so it is safe to run any number of times?
8. Does a deferred operation get a fresh evaluation at execution time rather than reusing the original decision?

**Operations and evidence**

9. Is each decision recorded with the policy version that produced it?
10. For remote evaluation, are latency budget, failure behavior, caching, freshness, and invalidation written down?
11. Is there an inventory of every path that performs the protected operation, and a test at each one?

## Continue Deeper

For the full comparison of rules engines, policy engines, central and embedded evaluation, PDP and PEP placement, policy distribution, partitions, and caching, continue with [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../../architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md). It is the advanced reference; this article is the selection guide in front of it.

Before reaching for it, confirm that the simpler options really fall short. [When ASP.NET Core Authorization Is Enough](../../architecture/when-aspnet-core-authorization-is-enough.md) and [When a Simple Application Service Is Enough](../../architecture/when-a-simple-application-service-is-enough.md) make the case for framework-native and application-level designs, and [When ASP.NET Core Authorization Is Not Enough](when-aspnet-core-authorization-is-not-enough.md) identifies the signals that the boundary has genuinely grown.

For the context-and-outcome model behind `IRefundPolicy`, read [Policy Context and Explicit Decision Outcomes](../../tutorials/policy-context-and-explicit-decision-outcomes.md). For testing it, [Practical Policy Testing and Decision-Table Strategies](../../governance/practical-policy-testing-and-decision-table-strategies.md). For recording which policy produced a decision, and deciding when an old decision is still fresh enough to act on, [Policy Versioning and Decision Provenance](../../governance/policy-versioning-and-decision-provenance.md).

If policy logic has drifted into MediatR pipeline behaviors or decorators, [Your Pipeline Behavior Can Watch the Operation. It Should Not Decide It.](pipeline-behavior-should-not-decide-the-operation.md) addresses that placement. If a deferred refund needs supervisor approval before it runs, [Authorization vs. Approval vs. Acknowledgment](authorization-vs-approval-vs-acknowledgment.md) shows how to keep each decision separate and check again at execution time.

The rule to keep is short: **make the decision explicit, keep its facts trusted and its side effects outside it, and move it only as far from the application as a real pressure requires.**
