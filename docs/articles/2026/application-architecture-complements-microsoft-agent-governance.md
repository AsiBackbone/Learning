---
description: Microsoft.AgentGovernance evaluates agent actions. The host application still owns context, workflow, execution, failure handling, and evidence.
title: How Application Architecture Complements Microsoft.AgentGovernance
author: Christopher D. Cavell
published: "2026-10-02"
summary: Microsoft.AgentGovernance gives .NET teams deterministic runtime policy evaluation for agent actions. That verdict is strongest when the surrounding application still owns authoritative context, workflow state, protected execution, failure handling, and evidence. This guide follows one AI-proposed refund through a host-owned control flow and shows where the governance verdict belongs.
feed: true
x_hashtags:
  - DotNet
  - AIGovernance
---

# How Application Architecture Complements Microsoft.AgentGovernance

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** Familiarity with C# and ASP.NET Core is helpful. You do not need to have installed `Microsoft.AgentGovernance`, and no other governance framework or prior Learning material is required. The architecture applies to any runtime agent-governance library; the package-specific code is isolated in one adapter.

**Package status:** At the time of publication, Microsoft labels the Agent Governance Toolkit a **public preview**: production-quality releases that may still make breaking changes before general availability. The `Microsoft.AgentGovernance` package targets .NET 8 and later. Package details in this article follow Microsoft's current [.NET package documentation](https://microsoft.github.io/agent-governance-toolkit/packages/dotnet-sdk/) and [.NET tutorial](https://microsoft.github.io/agent-governance-toolkit/tutorials/19-dotnet-sdk/). Microsoft's tutorials can lag the published package, so check [NuGet.org](https://www.nuget.org/packages/Microsoft.AgentGovernance) for the current version, and re-run your adapter tests whenever you upgrade. This article is independent and is not affiliated with or endorsed by Microsoft.

**What this article covers:** what `Microsoft.AgentGovernance` is documented to do; one AI-proposed refund walked through a host-owned control flow; why the policy should evaluate host-resolved facts rather than model-supplied values; how to translate a governance verdict into application outcomes without collapsing it to a Boolean; why policy verdicts, authorization, approval, acknowledgment, and execution permission stay separate; when short-lived execution authority is worth adding; failure semantics; the difference between evidence and telemetry; tests that prove every non-allowed path makes zero executor calls; and when a simpler integration is enough.

Your team has added `Microsoft.AgentGovernance` to a support application. An AI assistant can propose refunds, and every proposed tool call now passes through `GovernanceKernel.EvaluateToolCall` before anything happens. A policy file says refunds above $200 need supervisor approval, and the toolkit emits a governance event for each decision.

That is real progress. It also leaves an architectural question that the library cannot answer by itself:

> Where should agent-governance decisions sit inside the application's own trust, workflow, and execution boundaries?

The library evaluates the call it is given. It does not know which order the conversation was opened for, whether that order changed a second ago, which supervisor may approve this refund, how long an approval should last, or whether the payment provider actually moved the money. Those answers belong to the application around it, and the governance verdict is only as strong as the boundaries that application keeps.

> Runtime agent governance is strongest when the application around it still owns authoritative context, workflow state, protected execution, failure handling, and evidence boundaries. A governance verdict should participate in the application's control flow rather than replace the application's architecture.

None of this requires another framework or library. It is ordinary .NET architecture, applied deliberately around the governance call.

## What Microsoft.AgentGovernance Provides

The Agent Governance Toolkit is Microsoft's open-source, MIT-licensed project for runtime governance of autonomous agents, with packages for several languages. `Microsoft.AgentGovernance` is the .NET package. Microsoft's documentation describes these capabilities:

- **Deterministic policy evaluation before an action.** `GovernanceKernel.EvaluateToolCall` evaluates a proposed tool call against YAML or JSON policy rules with conditions, priorities, and conflict-resolution strategies. The result reports whether the call is allowed, a reason, and the underlying policy decision.
- **Agent identity and trust.** DID-based agent identities with delegation, and trust scores that can change over time.
- **Execution constraints.** Execution rings that tie an agent's privileges to its trust score, along with rate limiting, a circuit breaker, a kill switch, and saga orchestration with compensation.
- **Runtime interception.** Integrations that evaluate calls before tools run: Microsoft Agent Framework middleware, Model Context Protocol servers built with the official C# SDK, and a documented Semantic Kernel function-filter pattern.
- **Audit and observability support.** Governance events that application code can subscribe to, and OpenTelemetry-compatible metrics through `System.Diagnostics.Metrics`.
- **Human-oversight hooks.** A `require_approval` policy action that blocks a call pending human approval.

The package also includes prompt-injection detection that scans tool-call arguments before policy evaluation. This article focuses on the decision-and-execution boundary instead.

Microsoft's own documentation draws the line that this article builds on. The toolkit's [known limitations](https://microsoft.github.io/agent-governance-toolkit/LIMITATIONS/) page describes it as governance of agent *actions* rather than agent reasoning. It notes that the audit trail records what an agent attempted, not whether the action succeeded in the outside world, and it recommends a layered architecture in which an application layer validates business logic alongside the governance layer. The [project README](https://github.com/microsoft/agent-governance-toolkit) adds that the policy engine runs as application middleware, in the same process as the agent.

That is not a gap to apologize for. It is a division of labor, and the rest of this article describes the application's half of it.

## The Example: A Support Assistant Proposes a Refund

A support agent, a person, is working a customer conversation that the application opened for one specific order. An AI assistant in that conversation can propose refunds through an `orders.refund` tool. After the model's output has been parsed and schema-checked, the proposal contains only what the model is allowed to suggest:

```json
{
  "name": "orders.refund",
  "arguments": { "amount": 240.00, "currency": "USD", "reason": "Damaged" }
}
```

Everything else comes from somewhere the model cannot write to:

| Fact | Source | Why |
| --- | --- | --- |
| Amount, currency, reason | Model proposes; host validates | Suggesting a refund from the conversation is the model's job. |
| Which order | The conversation the host opened | The model must not redirect the refund to another order. |
| Support agent, tenant, and permissions | Authentication | Identity is never a model output. |
| Assistant identity used for governance | Host configuration | The registered assistant, not a name the model claims. |
| Refundable balance, order revision, fraud hold, risk tier | Order and risk stores, read now | Current facts come from the systems of record. |
| Supervisor approval | Host approval workflow | Consent comes from a person, not from the proposal. |
| Payment credentials | Executor only | The model and the policy never see them. |

The model could also have written `"orderId": "order-2002"` or `"tenantId": "contoso"`. A structurally valid value is not an authoritative one, and a well-formed argument is no evidence that the order belongs to this conversation or the tenant to this user. The schema rejects those fields, and the host never reads them. [What Should an AI Tool Gateway Validate Before Execution?](validate-ai-tool-call-before-execution.md) covers parsing and schema validation step by step. This article starts where those steps end.

## The Host-Owned Control Flow

```text
Agent proposal
    ↓
Parse and validate the proposed tool call          host
    ↓
Resolve authoritative host context                 host
    ↓
Application and resource authorization             host
    ↓
Check domain invariants                            host
    ↓
Microsoft.AgentGovernance policy evaluation        governance library
    ↓
Approval or acknowledgment, when required          host workflow
    ↓
Execution-time revalidation                        host executor
    ↓
Protected side effect                              host executor
    ↓
Outcome and evidence recording                     host, plus governance events
```

Each layer answers a different question:

| Layer | Question it answers |
| --- | --- |
| Validation | Is this a well-formed proposal for a tool this assistant may use? |
| Authoritative context | What is actually true about the actor, tenant, and order right now? |
| Application authorization | May this person act on this order for this tenant? |
| Domain invariants | Is this refund possible at all? |
| Agent-governance policy | Is this agent permitted to take this action under the current governance rules? |
| Workflow | Has an eligible person approved this exact refund, or acknowledged this exact warning? |
| Revalidation | Is the refund still permitted against the state that exists at execution time? |
| Executor | Perform the side effect once, with credentials only it holds. |
| Evidence | What was proposed, decided, approved, and done? |

The governance library sits in the middle, which is where it is strongest: after the host has turned an untrusted proposal into trusted facts, and before anything irreversible happens.

## Give the Policy Authoritative Facts

`EvaluateToolCall` evaluates the agent ID, tool name, and argument dictionary that it receives. A policy condition such as `amount_minor > 20000` is only as trustworthy as the value behind it. If the host forwards the model's raw arguments, the policy is evaluating the model's claims.

Resolve the context first, then build the evaluation input from it:

```csharp
public sealed record RefundProposal(decimal Amount, string Currency, RefundReason Reason);

public sealed record RefundContext(
    Guid OperationId,
    string AssistantAgentId,   // registered assistant identity, from configuration
    string SupportAgentId,     // the authenticated person
    string TenantId,           // from the authenticated session
    string OrderId,            // from the conversation, never from tool arguments
    string CustomerId,
    long OrderRevision,
    string OrderCurrency,
    long RefundableMinor,
    long AmountMinor,          // validated proposal amount, in minor units
    string Currency,           // validated proposal currency
    RefundReason Reason,
    bool FraudHold,
    long RiskRevision,         // the risk snapshot the decision was based on
    RiskTier RiskTier,
    string ProposalFingerprint, // binds approval and acknowledgment to this exact refund
    IReadOnlySet<string> AcknowledgedWarnings); // warning keys confirmed for this fingerprint

public static class RefundProposalFingerprint
{
    // Length-prefixed fields, so no value can run into its neighbor, and a version tag,
    // so changing the canonical form can never silently match approvals made under the old one.
    public static string Compute(
        string tenantId, string orderId, long orderRevision,
        long amountMinor, string currency, RefundReason reason)
    {
        var canonical = new StringBuilder("refund-proposal/v1");
        foreach (string field in new[]
        {
            tenantId,
            orderId,
            orderRevision.ToString(CultureInfo.InvariantCulture),
            amountMinor.ToString(CultureInfo.InvariantCulture),
            currency.ToUpperInvariant(),
            reason.ToString(),
        })
        {
            canonical.Append('|').Append(field.Length).Append(':').Append(field);
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return "v1:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed class RefundContextResolver(
    IOrderStore orders, IRiskStore risk, IAcknowledgmentStore acknowledgments)
{
    public async Task<RefundContext?> ResolveAsync(
        RefundProposal proposal, ConversationSession session, Guid operationId, CancellationToken ct)
    {
        Order? order = await orders.FindForTenantAsync(session.TenantId, session.OrderId, ct);
        if (order is null)
        {
            return null; // not visible to this tenant, which looks the same as "does not exist"
        }

        long amountMinor = Money.ToMinorUnits(proposal.Amount, proposal.Currency);
        RiskSnapshot snapshot = await risk.GetAsync(session.TenantId, order.CustomerId, ct);

        // The fingerprint is recomputed from current, host-resolved values on every resolution.
        string fingerprint = RefundProposalFingerprint.Compute(
            session.TenantId, order.Id, order.Revision, amountMinor, proposal.Currency, proposal.Reason);

        // Unexpired, unconsumed acknowledgments the acting support agent recorded for this exact proposal.
        IReadOnlySet<string> acknowledged = await acknowledgments.GetVerifiedWarningKeysAsync(
            operationId, fingerprint, session.SupportAgentId, ct);

        return new RefundContext(
            operationId,
            session.AssistantAgentId,
            session.SupportAgentId,
            session.TenantId,
            order.Id,
            order.CustomerId,
            order.Revision,
            order.Currency,
            order.RefundableMinor,
            amountMinor,
            proposal.Currency,
            proposal.Reason,
            snapshot.FraudHold,
            snapshot.Revision,
            snapshot.Tier,
            fingerprint,
            acknowledged);
    }
}
```

Three details carry the design:

- **The order and tenant come from the host.** The conversation was opened for one order and the session belongs to one tenant. A tool argument that names a different order or tenant is ignored, not trusted because it parsed.
- **Two identities, two owners.** `AssistantAgentId` is the identity the governance library reasons about: which agent is acting, at which trust level. The support agent and the tenant are application identity semantics that the library does not own. Governance can restrict what this assistant may do; application authorization, for example ordinary ASP.NET Core resource-based authorization, decides what this person may do for this tenant and this order. You usually need both.
- **Approval is not part of the context.** Whether an approval counts depends on which rule of the current policy requires it, and that is not known until the policy has been evaluated. The gateway handles approval as a second step, after the first evaluation names the requirement. Nothing the model writes can mark a refund as approved.

The governance policy then sees only host-built values. Here is a policy in the `governance.toolkit/v1` format that Microsoft documents for the .NET engine:

```yaml
apiVersion: governance.toolkit/v1
version: "2026-10-01"
name: support-assistant-refunds
default_action: deny
rules:
  - name: refund-needs-supervisor-approval
    condition: "tool_name == 'orders.refund' and amount_minor > 20000 and approval != 'verified'"
    action: require_approval
    priority: 100
    approvers:
      - support-supervisors

  - name: allow-assistant-refunds
    condition: "tool_name == 'orders.refund'"
    action: allow
    priority: 10
```

With the documented default conflict strategy, `PriorityFirstMatch`, the highest-priority matching rule wins. A $240 refund without a verified approval matches the approval rule. A $150 refund, or a $240 refund whose approval the host has verified, falls through to the allow rule. Anything else is denied by default.

Two cautions apply to that file. First, Microsoft notes that the .NET engine still uses this format while the Python, Rust, and TypeScript runtimes evaluate a newer manifest format, and that the .NET documentation will change when the engine migrates. That is one more reason to keep package-specific code inside a single adapter with its own tests. Second, the documentation shows numeric comparison operators but does not spell out how every numeric type is compared. Confirm how the version you pin compares the values you pass, and cover the threshold in a policy test.

Notice what is *not* in the policy file: the refundable balance, the currency match, and the fraud hold. Those are domain invariants, and the host checks them before the governance call. The governance policy owns the agent-specific rules: which assistants may refund at all, and the approval threshold for agent-proposed refunds. Each rule has one source of authority. Copying the balance check into the YAML would give the rule two owners and a window in which they disagree.

## Translate the Verdict Without Collapsing It

Microsoft's .NET tutorial documents how each policy action sets the result's `Allowed` flag. `allow`, `warn`, and `log` produce `true`. `deny` and `require_approval` produce `false`. `rate_limit` varies.

A host that checks only `result.Allowed` therefore treats "a supervisor must approve this" exactly like "this is forbidden." The assistant is told no, the support agent sees a refusal, and no approval request is ever created. Worse, someone eventually "fixes" the refusal by retrying until a different path succeeds.

Microsoft's own design direction points the same way. The toolkit's accepted [ADR-0030, the action-bound approval protocol](https://microsoft.github.io/agent-governance-toolkit/adr/0030-action-bound-approval-protocol/), treats `require_approval` as a suspended decision rather than an allow or an ordinary denial, binds an approval to the exact action, and revalidates before execution. Its reference implementation landed in the Python SDK first; the .NET action-bound approval-chain implementation later merged in [PR #3363](https://github.com/microsoft/agent-governance-toolkit/pull/3363), while [issue #3083](https://github.com/microsoft/agent-governance-toolkit/issues/3083) remains open as the cross-language parity tracker. Because repository documentation and published preview packages can differ, check the exact package version you pin rather than assuming that documentation on the repository's main branch matches it. Regardless, the documented `EvaluateToolCall` result reports `Allowed == false` for `require_approval`, so the distinction survives only if the host preserves it.

The application's control flow should preserve the distinctions it acts on. Define the outcomes in the application's own terms:

```csharp
public enum GovernedOutcome
{
    Allowed,
    Denied,
    ApprovalRequired,        // an eligible supervisor must approve this exact refund
    AcknowledgmentRequired,  // the support agent must confirm a specific warning
    Escalated,               // routed to the risk team instead of decided here
    Deferred,                // valid, but must wait, for example after a rate limit
    Unavailable              // no trustworthy decision could be made: fail closed
}

public sealed record GovernedDecision(
    GovernedOutcome Outcome,
    string ReasonCode,
    string? MatchedPolicy,   // policy names and rule names are only unique together
    string? MatchedRule,
    string PolicySetId,
    string? AcknowledgedWarning = null,             // consumed at execution when set
    ApprovalRequirement? ConsumesApproval = null,   // consumed at execution when set
    IReadOnlyList<string>? RequiredApprovers = null); // approver groups the matched rule declares

// The exact requirement an approval satisfies. A different rule, policy set, proposal, approver
// groups, assistant, or risk tier is a different requirement, and an approval for one never counts for another.
public sealed record ApprovalRequirement(
    Guid OperationId,
    string ProposalFingerprint,
    string PolicySetId,
    string Policy,
    string Rule,
    string ApproverGroups,      // canonical form of the groups the policy declared
    string AssistantAgentId,
    RiskTier RiskTier)
{
    public static ApprovalRequirement For(RefundContext context, GovernedDecision decision) =>
        new(context.OperationId,
            context.ProposalFingerprint,
            decision.PolicySetId,
            Named(decision.MatchedPolicy, "An approval rule from an unnamed policy cannot be satisfied."),
            Named(decision.MatchedRule, "An unnamed approval rule cannot be satisfied."),
            CanonicalGroups(decision.RequiredApprovers),
            context.AssistantAgentId,
            context.RiskTier);

    private static string Named(string? value, string error) =>
        string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException(error) : value;

    // Sorted, de-duplicated, length-prefixed: the same set of groups always yields the same string.
    // A rule that names no approver group, or any blank one, cannot be satisfied, so it fails closed here.
    private static string CanonicalGroups(IReadOnlyList<string>? groups) =>
        groups is { Count: > 0 } && !groups.Any(string.IsNullOrWhiteSpace)
            ? string.Concat(groups.Distinct(StringComparer.Ordinal)
                                  .Order(StringComparer.Ordinal)
                                  .Select(g => $"{g.Length}:{g};"))
            : throw new InvalidOperationException("An approval rule without valid approvers cannot be satisfied.");

    public IReadOnlyList<string> ApproverGroupNames() =>
        ApproverGroups.Length == 0 ? [] : ParseGroups(ApproverGroups);

    private static List<string> ParseGroups(string canonical)
    {
        var groups = new List<string>();
        for (int i = 0; i < canonical.Length;)
        {
            int colon = canonical.IndexOf(':', i);
            int length = int.Parse(canonical.AsSpan(i, colon - i), CultureInfo.InvariantCulture);
            groups.Add(canonical.Substring(colon + 1, length));
            i = colon + 1 + length + 1; // skip the trailing ';'
        }

        return groups;
    }

    public bool Covers(RefundContext context, string policySetId) =>
        OperationId == context.OperationId
        && ProposalFingerprint == context.ProposalFingerprint
        && PolicySetId == policySetId
        && AssistantAgentId == context.AssistantAgentId
        && RiskTier == context.RiskTier;
}

public interface IRefundGovernance
{
    // verifiedApproval: an approval the host has matched to the requirement a previous
    // evaluation of this same context reported. Null on the first evaluation.
    GovernedDecision Evaluate(RefundContext context, ApprovalRequirement? verifiedApproval = null);
}
```

These names belong to the application. `Microsoft.AgentGovernance` does not need to expose them, and neither does any other governance library you might use instead. Outside the governance runtime shown later, the adapter is the only class that knows the package's types:

```csharp
using AgentGovernance;

public sealed class AgentGovernanceRefundPolicy(GovernanceRuntimeHolder runtimes) : IRefundGovernance
{
    public GovernedDecision Evaluate(RefundContext context, ApprovalRequirement? verifiedApproval = null)
    {
        // One leased runtime per evaluation: the decision and its policy set ID describe the same
        // kernel, and that kernel cannot be disposed until the lease is released.
        using RuntimeLease lease = runtimes.Acquire();
        GovernanceRuntime runtime = lease.Runtime;
        string policySetId = runtime.PolicySet.Id;

        if (verifiedApproval is not null && !verifiedApproval.Covers(context, policySetId))
        {
            return Decide(GovernedOutcome.Unavailable, "approval_binding_mismatch", null, null, policySetId);
        }

        try
        {
            var result = runtime.EvaluateToolCall(
                agentId: context.AssistantAgentId,
                toolName: "orders.refund",
                args: new()
                {
                    ["operation_id"] = context.OperationId.ToString("N"), // correlation only; policy rules must not depend on it
                    ["tenant_id"] = context.TenantId,
                    ["amount_minor"] = context.AmountMinor,
                    ["currency"] = context.Currency,
                    ["risk_tier"] = context.RiskTier.ToString(),
                    ["approval"] = verifiedApproval is null ? "none" : "verified",
                });

            // Interpolation works whether the pinned version exposes Action as a string or an enum.
            string action = GovernanceActionNames.Normalize($"{result.PolicyDecision?.Action}");
            GovernedDecision decision = Map(
                result.Allowed,
                action,
                result.PolicyDecision?.PolicyName,
                result.PolicyDecision?.MatchedRule,
                context,
                policySetId);

            return decision.Outcome switch
            {
                // An approval requirement with a missing or blank policy name, rule name, or
                // approver entry can never be satisfied. Fail closed here instead of opening a
                // request nobody can decide. Blank counts as missing: the loader accepts "" and " ".
                GovernedOutcome.ApprovalRequired when
                    string.IsNullOrWhiteSpace(decision.MatchedPolicy)
                    || string.IsNullOrWhiteSpace(decision.MatchedRule)
                    || result.PolicyDecision?.Approvers is not { } approvers
                    || !approvers.Any()
                    || approvers.Any(string.IsNullOrWhiteSpace) =>
                    Decide(GovernedOutcome.Unavailable, "approval_rule_without_approvers",
                        decision.MatchedPolicy, decision.MatchedRule, policySetId),

                // Carry the policy-declared approver groups to the host workflow; the host does
                // not look the policy up again, and cannot drift from what the rule required.
                GovernedOutcome.ApprovalRequired => decision with
                {
                    RequiredApprovers = result.PolicyDecision!.Approvers!.ToArray(),
                },

                // Only an allow reached through this approval consumes it.
                GovernedOutcome.Allowed when verifiedApproval is not null =>
                    decision with { ConsumesApproval = verifiedApproval },

                _ => decision,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Decide(GovernedOutcome.Unavailable, "governance_evaluation_failed", null, null, policySetId);
        }
    }

    private static GovernedDecision Map(
        bool allowed, string action, string? policy, string? rule, RefundContext context, string policySetId) =>
        (allowed, action) switch
        {
            // A rate_limit rule matched and the call is still within its window.
            (true, "allow" or "log" or "ratelimit") =>
                Decide(GovernedOutcome.Allowed, "allowed", policy, rule, policySetId),

            // Application choice: a governance warning on a refund needs explicit confirmation.
            // A verified acknowledgment of this exact warning, under this policy set, completes it.
            (true, "warn") when !string.IsNullOrWhiteSpace(policy) && !string.IsNullOrWhiteSpace(rule)
                             && context.AcknowledgedWarnings.Contains(WarningKeys.For(policySetId, policy, rule)) =>
                Decide(GovernedOutcome.Allowed, "warning_acknowledged", policy, rule, policySetId)
                    with { AcknowledgedWarning = WarningKeys.For(policySetId, policy, rule) },

            (true, "warn") =>
                Decide(GovernedOutcome.AcknowledgmentRequired, "governance_warning", policy, rule, policySetId),

            (false, "requireapproval") =>
                Decide(GovernedOutcome.ApprovalRequired, "approval_required", policy, rule, policySetId),

            // The window's limit is exhausted: valid, but must wait.
            (false, "ratelimited" or "ratelimit") =>
                Decide(GovernedOutcome.Deferred, "rate_limited", policy, rule, policySetId),

            (false, _) =>
                Decide(GovernedOutcome.Denied, "governance_denied", policy, rule, policySetId),

            // Allowed by a decision this adapter does not recognize: never infer permission.
            (true, _) =>
                Decide(GovernedOutcome.Unavailable, "unrecognized_governance_decision", policy, rule, policySetId),
        };

    private static GovernedDecision Decide(
        GovernedOutcome outcome, string reasonCode, string? policy, string? rule, string policySetId) =>
        new(outcome, reasonCode, policy, rule, policySetId);
}

public static class WarningKeys
{
    // Policy set, policy, and rule together identify one warning. Length prefixes keep a name
    // containing the separator from colliding with a different policy and rule pair, and binding
    // the key to the policy set means a changed policy requires a fresh acknowledgment.
    public static string For(string policySetId, string policy, string rule) =>
        $"{policySetId}#{policy.Length}:{policy}#{rule.Length}:{rule}";
}

public static class GovernanceActionNames
{
    // "require_approval", "RequireApproval", and "requireapproval" all become "requireapproval".
    public static string Normalize(string action) =>
        action.Replace("_", "", StringComparison.Ordinal)
              .Replace("-", "", StringComparison.Ordinal)
              .ToLowerInvariant();
}
```

A few choices in that mapping are deliberate:

- **The adapter matches normalized action names, not the package's types.** Microsoft's .NET tutorial presents the decision's action as the `PolicyAction` enum, while current releases of the package expose `PolicyDecision.Action` as a lowercase string such as `requireapproval` or `ratelimit`. Normalizing the value to lowercase without separators keeps the mapping independent of that representation, and the adapter contract tests below fail if a future release renames an action.
- **Rate limiting has two states.** In current releases, a call that matches a `rate_limit` rule while its window still has room comes back with `Allowed == true` and the action `rate_limit`; once the limit is exhausted, it comes back with `Allowed == false` and `rate_limited`. The first is an ordinary allow. The second is `Deferred`: the refund is valid but must wait. Mapping only one of them would either defer allowed calls or let exhausted ones fall through to a plain denial.
- **Unrecognized blocks are denials; unrecognized allows are unavailable.** A preview package can add policy actions. If a call is blocked for a reason the adapter does not know, it stays blocked. If a call is allowed by a decision the adapter cannot explain, the application refuses to treat that as permission.
- **A block can arrive without the policy decision you expect.** Microsoft documents that ring checks and prompt-injection checks, when enabled, run before policy evaluation. The `(false, _)` arm treats any such block as a denial without depending on which check produced it.
- **Treating `warn` as acknowledgment is an application decision.** The library says the call may proceed with a flag. For money movement, this application chooses to make the support agent confirm the warning first. That choice needs a completion path, or every re-evaluation would ask again forever: once the support agent confirms, the host records an acknowledgment bound to the proposal fingerprint and a warning key made of the policy set ID, the matched policy's name, and the matched rule's name; rule names are unique only within a policy, so the policy name keeps one policy's acknowledged warning from satisfying another policy's rule of the same name. The next evaluation sees that key in `AcknowledgedWarnings` and allows the refund, and the executor consumes the acknowledgment atomically when it reserves the balance. An acknowledgment of a different warning, a different refund, or a policy set that has since changed does not count, and a warning from a policy or rule with a missing or blank name can never be acknowledged. A read-only tool might simply log it.
- **Not every outcome comes from the library.** `Escalated` comes from the host's own fraud-hold rule, shown in the next section. The outcome type describes the application's control flow, not one component's vocabulary.

## Keep the Protected Side Effect Behind the Boundary

A governance verdict changes nothing unless the side effect cannot happen without it. The gateway composes the steps, and it is the only code that can reach the executor:

```csharp
public sealed class RefundDomainRules
{
    // Domain invariants: the host decides these before any governance call.
    public GovernedDecision? Check(RefundContext context)
    {
        if (context.Currency != context.OrderCurrency)
        {
            return Host(GovernedOutcome.Denied, "currency_mismatch");
        }

        if (context.AmountMinor <= 0 || context.AmountMinor > context.RefundableMinor)
        {
            return Host(GovernedOutcome.Denied, "amount_not_refundable");
        }

        if (context.FraudHold)
        {
            return Host(GovernedOutcome.Escalated, "fraud_hold");
        }

        return null; // no domain objection; the agent-governance policy decides next
    }

    private static GovernedDecision Host(GovernedOutcome outcome, string reasonCode) =>
        new(outcome, reasonCode, MatchedPolicy: null, MatchedRule: null, PolicySetId: "domain-rules");
}

public sealed class RefundGateway(
    RefundContextResolver resolver,
    IAuthorizationService authorization,
    RefundDomainRules domainRules,
    IRefundGovernance governance,
    IApprovalStore approvals,
    IRefundWorkflow workflow,
    IRefundExecutor executor,
    IDecisionRecorder recorder)
{
    public async Task<RefundResult> HandleAsync(
        RefundProposal proposal, ConversationSession session, Guid operationId, CancellationToken ct)
    {
        RefundContext? context = await resolver.ResolveAsync(proposal, session, operationId, ct);
        if (context is null)
        {
            return RefundResult.NotExecuted("order_not_available");
        }

        // Application authorization: may this support agent refund this order for this tenant?
        // Agent governance never answers this question for the application.
        AuthorizationResult access = await authorization.AuthorizeAsync(
            session.User, context, RefundPolicies.SupportAgentMayRefund);
        if (!access.Succeeded)
        {
            return RefundResult.NotExecuted("not_authorized");
        }

        GovernedDecision decision = domainRules.Check(context) ?? governance.Evaluate(context);

        // Approval is matched to the requirement the current policy actually states. Evaluate
        // without approval first; only then look for an approval bound to that exact requirement.
        if (decision is
            {
                Outcome: GovernedOutcome.ApprovalRequired,
                MatchedPolicy: not null,
                MatchedRule: not null,
                RequiredApprovers.Count: > 0,
            })
        {
            ApprovalRequirement requirement = ApprovalRequirement.For(context, decision);
            if (await approvals.IsApprovedAsync(requirement, ct)) // unexpired, eligible, unconsumed
            {
                decision = governance.Evaluate(context, verifiedApproval: requirement);
            }
        }

        await recorder.RecordDecisionAsync(context, decision, ct);

        return decision.Outcome switch
        {
            GovernedOutcome.Allowed =>
                await executor.ExecuteAsync(RefundCommand.From(context, decision), ct),
            GovernedOutcome.ApprovalRequired =>
                await workflow.RequestApprovalAsync(context, decision, ct),
            GovernedOutcome.AcknowledgmentRequired =>
                await workflow.RequestAcknowledgmentAsync(context, decision, ct),
            GovernedOutcome.Escalated =>
                await workflow.EscalateAsync(context, decision, ct),
            _ =>
                RefundResult.NotExecuted(decision.ReasonCode),
        };
    }
}
```

The invariant is short enough to state as a test assertion:

```text
Denied, deferred, unavailable, escalated, or not yet approved or acknowledged
        ↓
Protected executor invocation count = 0
```

The approval step deserves a note. An approval is never looked up before the policy has said what it requires. The first evaluation runs with `approval = 'none'` and reports the rule that requires approval. The host then looks for an approval bound to that exact requirement: this operation, this proposal fingerprint, this policy set, this policy and rule, the approver groups that rule declared, this assistant, and this risk tier. Only if one exists does a second evaluation run with `approval = 'verified'`, and only an allow reached that way carries the approval forward to be consumed. If the risk tier changes, or the same policy set holds a stricter approval rule that now matches, the requirement is different and an approval for the old one does not count. This sketch handles one approval requirement per refund; a policy that stacks several needs the same step repeated for each, with every approval consumed together.

Three practical consequences follow.

**Logging a denial is not the same as preventing execution.** A governance event proves that the library said no. It cannot prove that no other code path called the payment provider. Only the shape of the code, an executor reachable solely through the gateway, and tests that count executor calls can show that.

**Every path to the side effect goes through the boundary.** The assistant's tool is rarely the only way to issue a refund. A background job that retries failed refunds, an internal administration endpoint, and a second agent all need to pass through the same gateway or an equivalent that is just as explicit. In an ASP.NET Core application, keep the executor internal to the module, register it only where the gateway needs it, and keep payment credentials inside it.

**A governance layer that is not configured governs nothing.** Microsoft's limitations page warns that a policy evaluator with no policies loaded defaults to `allow`, and recommends a deny-by-default posture in production. Empty-policy behavior also differs across SDKs and versions: current releases of the .NET package deny an evaluation when no policies are loaded. A probe that only checks for a denial therefore cannot tell "deny-by-default" from "nothing loaded." Make the configuration and its effect explicit startup conditions rather than assumptions:

```csharp
public static class GovernanceStartupChecks
{
    // Call once at startup, before the refund tool is exposed.
    public static void Verify(GovernanceKernel kernel)
    {
        var policies = kernel.PolicyEngine.ListPolicies();
        if (policies.Count == 0)
        {
            throw new InvalidOperationException(
                "No agent-governance policies are loaded. Refusing to start.");
        }

        // Name check: the loader accepts blank policy and rule names, and blank approver entries,
        // but approvals and acknowledgments are bound to these names. Reject them before anything runs.
        var blankNames = policies
            .SelectMany(policy =>
                (string.IsNullOrWhiteSpace(policy.Name) ? ["a policy with a blank name"] : Array.Empty<string>())
                .Concat(policy.Rules
                    .Where(rule => string.IsNullOrWhiteSpace(rule.Name))
                    .Select(_ => $"a rule with a blank name in '{policy.Name}'"))
                .Concat(policy.Rules
                    .Where(rule => rule.Approvers?.Any(string.IsNullOrWhiteSpace) == true)
                    .Select(rule => $"a blank approver in '{policy.Name}/{rule.Name}'")))
            .ToList();

        if (blankNames.Count > 0)
        {
            throw new InvalidOperationException(
                $"Agent-governance configuration has blank identifiers: {string.Join("; ", blankNames)}. Refusing to start.");
        }

        // Identity check: the package accepts duplicate rule names, but parts of it resolve rule
        // configuration (such as a rate limit's Limit) by rule name alone, and approvals and warning
        // acknowledgments here are bound to the rule. Until every package lookup is scoped to
        // (policy, rule), a rule name must be unique across the whole policy set, not just its policy.
        var duplicateRuleNames = policies
            .SelectMany(policy => policy.Rules.Select(rule => new { Policy = policy.Name, Rule = rule.Name }))
            .GroupBy(identity => identity.Rule, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} (in {string.Join(", ", group.Select(identity => identity.Policy))})")
            .ToList();

        if (duplicateRuleNames.Count > 0)
        {
            throw new InvalidOperationException(
                $"Agent-governance rule names are duplicated: {string.Join("; ", duplicateRuleNames)}. Refusing to start.");
        }

        // Configuration check: every loaded policy must declare default_action: deny.
        var permissive = policies
            .Where(policy => GovernanceActionNames.Normalize($"{policy.DefaultAction}") != "deny")
            .Select(policy => policy.Name)
            .ToList();

        if (permissive.Count > 0)
        {
            throw new InvalidOperationException(
                $"Agent-governance policies are not deny-by-default: {string.Join(", ", permissive)}. Refusing to start.");
        }

        // Behavior check: an unregistered tool must actually be denied with these options.
        var probe = kernel.EvaluateToolCall("startup-probe", "governance.unregistered-probe-tool");
        if (probe.Allowed)
        {
            throw new InvalidOperationException(
                "The agent-governance policy set allowed an unregistered tool. Refusing to start.");
        }
    }
}
```

Run the checks with the same `GovernanceOptions` that production uses. Each answers a different question. The policy count confirms that something was loaded. The name check rejects blank policy names, blank rule names, and blank approver entries, which the loader accepts but which could never identify an approval or acknowledgment. The identity check confirms that every rule name is unique across the whole policy set. The package's loader accepts duplicates, and the current .NET middleware resolves some rule configuration, such as a rate limit's `Limit`, by rule name alone, so a match in one policy could be enforced with another policy's settings, and two rules sharing a name could share one approval or acknowledgment. Approvals and acknowledgments still carry the policy name as well; the stricter uniqueness rule only removes the ambiguity in the package's own lookups, and can be relaxed to (policy, rule) pairs once every lookup in the version you pin is scoped that way. The `DefaultAction` check confirms the configuration: a probe alone cannot, because a policy with `default_action: allow` and a rule that happens to deny the probe tool would still return `Allowed == false`. The probe confirms the effective behavior under the real options. None of them verifies individual rules; the adapter contract tests later in this article cover those.

## Policy Verdicts Are Not Approval, Acknowledgment, or Execution Permission

Several decisions in this flow sound like permission. They answer different questions:

| Decision | Question | Produced by | Bound to | Lasts |
| --- | --- | --- | --- | --- |
| Governance verdict | Is this agent permitted to take this action under the current governance rules? | `Microsoft.AgentGovernance` | The evaluated input and the loaded policy set | The moment of evaluation |
| Application authorization | May this support agent act on this order for this tenant? | The application, for example ASP.NET Core authorization | User, tenant, and resource | The request |
| Approval | Has an eligible supervisor approved this exact refund? | Host workflow | Operation, proposal fingerprint, and approver | Until it expires or is used |
| Acknowledgment | Has the support agent confirmed this specific warning? | Host workflow | Operation, proposal fingerprint, and the warning shown | Until it expires or is used |
| Execution permission | Is the refund still permitted against the current state? | Executor revalidation | Current order state | The conditional write |

None of these implies another. A `require_approval` verdict says that approval is needed; it does not say who is eligible to give it. An approval record says that a supervisor agreed; it does not replace a fresh evaluation when the refund finally runs. An acknowledgment proves awareness, not authority. [Authorization vs. Approval vs. Acknowledgment: Which Decision Do You Actually Have?](authorization-vs-approval-vs-acknowledgment.md) works through the same distinctions for a delayed data export.

## Workflow State Belongs to the Application

A policy engine determines whether an action is currently permitted. A workflow answers different questions: whether an eligible reviewer approved a specific proposal, and whether that approval still applies.

Keep application workflow state out of the evaluator. Evaluation is not entirely free of side effects in the current .NET package: it maintains its own rate-limit windows and emits audit events and metrics. Those are the package's concerns. What belongs to the application is the workflow: do not move approval tables into policy conditions, and do not create, change, or consume approvals and acknowledgments from inside evaluation because the hook happens to be convenient. The policy receives one host-verified fact, `approval == 'verified'`, and only on the second evaluation, after the host has matched an approval to the requirement the first evaluation reported. The record behind that fact lives in the application:

```csharp
public sealed record RefundApproval(
    ApprovalRequirement Requirement, // operation, proposal fingerprint, policy set, policy, rule, assistant, risk tier
    long OrderRevision,
    long AmountMinor,
    string RequestedBy,          // the support agent who asked
    string? DecidedBy,           // a member of one of Requirement's approver groups, never the requester
    string? DecidedAsMemberOf,   // the group membership checked when the decision was recorded
    DateTimeOffset ExpiresAt,
    ApprovalStatus Status);      // Pending, Approved, Rejected, Expired, Consumed
```

Acknowledgment follows the same pattern with a smaller record:

```csharp
public sealed record RefundAcknowledgment(
    Guid OperationId,
    string ProposalFingerprint,
    string WarningKey,               // WarningKeys.For(policy set, policy, rule)
    string AcknowledgedBy,           // the acting support agent, not a supervisor
    DateTimeOffset AcknowledgedAt,
    DateTimeOffset ExpiresAt,        // minutes, not days: awareness goes stale too
    bool Consumed);
```

`GetVerifiedWarningKeysAsync` returns only keys whose acknowledgment is unexpired and unconsumed, and the consume at execution checks the expiry again, so an acknowledgment that lapses between evaluation and reservation does not count. The person confirming is the acting support agent; acknowledgment proves awareness of a specific warning, not authority.

The policy rule names the approver group, as `approvers: [support-supervisors]` does above, and the adapter carries that list from the package's decision into `GovernedDecision.RequiredApprovers` and from there into the stored `ApprovalRequirement`. The host never looks the policy up a second time, so the groups it enforces are exactly the groups the rule declared when it required approval. When a supervisor decides, the workflow checks, at that moment, that the person belongs to one of `Requirement.ApproverGroupNames()` for this tenant, rejects self-approval, and records the group it checked. A rule that requires approval but names no approver or a blank one, or comes from a policy or rule with a missing or blank name, can never be satisfied, so the adapter maps it to `Unavailable` and no approval request is ever opened; `ApprovalRequirement.For` refuses to build a requirement for it as a second guard.

When the supervisor approves, the continuation does not replay the original decision. It starts again from current facts:

```text
Supervisor approves operation 7f3c…
      ↓
Reload order and risk state; recompute the proposal fingerprint
      ↓
Domain invariants          (a fraud hold set while waiting now escalates)
      ↓
Governance evaluation      (approval = "none": which rule requires approval now?)
      ↓
Approval lookup            (bound to that exact requirement, or no match)
      ↓
Governance evaluation      (approval = "verified")
      ↓
Executor                   (conditional write that also consumes the approval)
```

The continuation runs under the workflow's own service identity and rechecks the support agent's current access to the order; it does not reuse the original request's token. If the governance policy changed while the approval was pending, the policy set ID changed with it; if the risk tier changed, or a different approval rule in the same policy set now matches, the requirement changed. In each case the old approval matches nothing, and if the current policy still requires approval, the workflow requests a new one under the current rule and approver group. That is the intended behavior: an approval satisfies the requirement it was granted for, not whatever requirement applies when execution resumes. The workflow stores the full `ApprovalRequirement` when it creates the request, and the executor's conditional consume matches it again. [Human-in-the-Loop Governance Workflows](../../governance/human-in-the-loop-governance-workflows.md) goes deeper on reviewer eligibility, expiry, and pending-state design.

## Revalidate at Execution Time, and Scope Authority to the Lifecycle

An allow verdict at 10:02 against order revision 41 is not permission at 14:30 against revision 44. Do not persist an allow decision and replay it later.

The execution boundary revalidates every mutable fact whose change would revoke permission, either directly or through a versioned precondition. The order row is not the only such fact. The fraud hold lives in a separate risk system and can change without changing the order's revision, so a check on the order alone would still let a refund through after a hold was placed. The executor therefore rechecks both:

```csharp
public sealed class RefundExecutor(
    IRefundReservations reservations,
    IRiskStore risk,
    IPaymentProvider payments,
    GovernanceRuntimeHolder governance)
    : IRefundExecutor
{
    public async Task<RefundResult> ExecuteAsync(RefundCommand command, CancellationToken ct)
    {
        // Facts outside the order row can revoke permission without changing the order revision.
        RiskSnapshot current = await risk.GetAsync(command.TenantId, command.CustomerId, ct);
        if (current.FraudHold || current.Revision != command.ExpectedRiskRevision)
        {
            return RefundResult.NotExecuted("stale_context"); // re-run the whole flow
        }

        // One local transaction: reserve the refundable amount, record the operation as Pending,
        // and consume any approval. It succeeds only if the order is unchanged and the balance
        // still covers the refund. Nothing external has been called yet.
        ReservationResult reservation = await reservations.ReserveAsync(command, ct);
        if (!reservation.Reserved)
        {
            return RefundResult.NotExecuted("stale_context"); // re-run the whole flow; never replay the decision
        }

        // The decision was made under command.PolicySetId. If a different policy set became
        // active before the reservation committed, the allow is obsolete: release the reservation
        // and re-run the whole flow under the new policy. Checking after the commit makes the
        // reservation the point at which both the decision's policy and the order state held.
        if (governance.CurrentPolicySet.Id != command.PolicySetId)
        {
            // Not ReleaseAsync: nothing was sent to the provider, so this is a rollback that leaves
            // the operation retryable under the same ID, not a terminal failure.
            await reservations.RollBackAsync(command.OperationId, "policy_set_changed", CancellationToken.None);
            return RefundResult.NotExecuted("policy_set_changed"); // re-run the whole flow, same operation ID
        }

        // Only after the reservation commits, call the provider with an idempotency key derived
        // from the operation ID. Finalization writes use CancellationToken.None so that a caller
        // who gives up waiting cannot leave the recorded state behind the provider's.
        ProviderRefundResult outcome;
        try
        {
            outcome = await payments.RefundAsync(
                command, idempotencyKey: command.OperationId.ToString("N"), ct);
        }
        catch (Exception)
        {
            // A timeout, a dropped connection, or a cancellation after sending: the refund may or
            // may not have happened. Keep the reservation; reconcile with the same key.
            await reservations.MarkUnknownAsync(command.OperationId, CancellationToken.None);
            return RefundResult.OutcomeUnknown("provider_outcome_unknown");
        }

        switch (outcome.Status)
        {
            case ProviderRefundStatus.Succeeded:
                // The reservation becomes the refund.
                await reservations.CompleteAsync(
                    command.OperationId, outcome.ProviderReference, CancellationToken.None);
                return RefundResult.Executed(outcome.ProviderReference);

            case ProviderRefundStatus.Rejected:
                // Terminal: the provider will never perform this refund. Restore the balance.
                await reservations.ReleaseAsync(
                    command.OperationId, outcome.FailureCode, CancellationToken.None);
                return RefundResult.NotExecuted("provider_rejected");

            default:
                // Anything the executor cannot classify is treated as unknown, never as failed.
                await reservations.MarkUnknownAsync(command.OperationId, CancellationToken.None);
                return RefundResult.OutcomeUnknown("provider_outcome_unknown");
        }
    }
}
```

`ReserveAsync` is where the atomicity lives. In an EF Core implementation, it is a conditional `ExecuteUpdateAsync` on the order (matching tenant, order ID, expected revision, and a refundable balance that still covers the amount, then decrementing the balance and incrementing the revision), the insert of the Pending operation, the approval's conditional consume when the decision carries one (matching the decision's `ApprovalRequirement` exactly, still Approved, and unexpired, then marking it Consumed), and, when the decision carries an acknowledged warning, the same conditional consume of that acknowledgment (matching operation ID, fingerprint, and warning key, still unconsumed and unexpired), all inside one database transaction that commits before the provider is called. If any part fails, none of it happened, so a crash can never leave a reserved balance with no operation record or an approval that was used without a reservation.

The finalization methods are just as conditional. `CompleteAsync` moves an operation from Pending or Unknown to Succeeded and records the provider reference. `ReleaseAsync` moves it from Pending or Unknown to Failed and restores the refundable balance in the same transaction, so the balance can be restored only once. `MarkUnknownAsync` moves it from Pending to Unknown and leaves the reservation in place. `RollBackAsync` is different from all three: it is valid only before the provider has been called, moves the operation from Pending to RolledBack, and restores the balance in the same transaction. RolledBack is not terminal. `ReserveAsync` accepts an operation that is either new or RolledBack, so the re-run keeps its operation ID, its idempotency key, and the approval and acknowledgment binding that depend on it. Reusing the key is safe precisely because the provider never saw it; Failed, by contrast, is terminal, and its operation ID is never reserved again. Reconciliation calls the same methods once it learns the provider's answer. If the process crashes before a finalization write lands, the operation is still Pending, and reconciliation treats a Pending operation older than its time budget exactly like an Unknown one.

The risk check uses a revision when the risk system exposes one; if it does not, recheck the specific flags that would revoke permission. Because the risk system cannot join the order database's transaction, a short window remains between that read and the reservation. Where that window matters, ask the risk system for a lease or hold that it honors until the operation settles, or have the payment path check the hold itself, and document whatever window remains rather than assuming it away.

The policy set gets the same treatment as the order and risk state. `RefundCommand.From` copies the decision's policy set ID into the command, and the executor compares it with the active policy set after the reservation commits but before the provider is called. A policy published between evaluation and that point turns the old allow into `policy_set_changed`: the reservation is rolled back and the flow re-runs under the new policy with the same operation ID, which may deny, ask for approval, or allow again. Any approval or acknowledgment the reservation consumed stays consumed; it was bound to the old policy set and could not count under the new one anyway. Holding the runtime lease longer would not achieve this, because a lease keeps an old kernel alive but does not stop a new policy from becoming active. A policy that becomes active after the check governs the next refund, not one that is already reserved.

The reservation is deliberate. Reducing the refundable balance before calling the provider prevents two concurrent refunds from both spending the same balance, and the operation's recorded state decides what happens to that reservation afterward. A permanently failed refund gives the balance back; an unknown one holds it until reconciliation knows the answer.

How much more execution authority to model depends on how far execution travels from the decision:

| Situation | What is usually enough |
| --- | --- |
| Same request, same process, immediate execution | Application authorization, governance evaluation, and a conditional write in the executor. No token. |
| Approval pauses execution | An approval record bound to the proposal fingerprint and revision, with expiry, single use, and re-evaluation on continuation. |
| A queue, worker, or another service executes later | A short-lived, narrowly scoped execution grant that the executor verifies: one operation, one order revision, one amount, an expiry, single use. |
| High-consequence or cross-boundary execution | A signed grant with replay resistance, plus a durable decision record. |

A grant for the queued case can be as small as this:

```csharp
public sealed record RefundExecutionGrant(
    Guid OperationId,
    string TenantId,
    string OrderId,
    long ExpectedRevision,
    long ExpectedRiskRevision,
    long AmountMinor,
    string Currency,
    DateTimeOffset NotAfter); // minutes, not days; consumed on first use
```

Capability tokens are not mandatory for every integration. Most same-request agent tool calls need none. Add bounded execution authority when authority has to survive a delay, a queue, a delegation, or a trust boundary, and not before. [Do You Need a Capability Token, or Are Roles and Claims Enough?](roles-claims-or-capability-token-dotnet.md) walks through that threshold.

## Decide Failure Semantics Explicitly

Even in-process evaluation can fail. A policy file can fail to load or contain an error, evaluation can throw, a circuit breaker that you place around evaluation can open, and a deployment can move evaluation into a sidecar or an external policy backend that adds latency and network failure. Microsoft's limitations page describes a fail-closed posture, but the current .NET policy engine can propagate evaluation exceptions, including cancellation, without producing a decision. The application must therefore handle that path itself: the adapter above maps evaluation exceptions to `Unavailable`, and lets cancellation propagate so the request ends without reaching the executor. Do not remove that handling on the assumption that the package fails closed for you. What the application tells the user and the model is still an architecture decision that the application owns.

| Situation | Application outcome | Retry? | Never |
| --- | --- | --- | --- |
| Evaluation throws, or returns a decision the adapter does not recognize | `Unavailable` | A bounded retry of the evaluation with freshly resolved context | Fall back to an earlier allow |
| Evaluation exceeds its time budget (sidecar or remote backend) | `Unavailable` | A bounded retry with backoff | Treat a timeout as an allow |
| A circuit breaker around evaluation is open | `Unavailable` | After the breaker's retry-after interval | Bypass the breaker for "important" calls |
| A governance rate limit is reached | `Deferred` | After the window | Retry in a tight loop until a call gets through |
| The order or risk state changed before execution | Not executed (`stale_context`) | Re-run the whole flow | Replay the old decision against the new state |
| The active policy set changed after the reservation, before the provider call | Rolled back (`policy_set_changed`) | Re-run the whole flow with the same operation ID | Mark the operation Failed, or send with a new idempotency key |
| The provider call timed out after sending | `Unknown` execution outcome | Reconcile using the same idempotency key | Decide again and send with a new key |

Failure semantics can reasonably differ by risk, but only as an explicit, per-tool decision. Moving money fails closed with no exception. A read-only `orders.lookup` tool might, as a documented and recorded risk decision, fall back to ordinary application authorization while governance is unavailable. Express that choice in code for the specific tool; do not let a generic `catch` block turn every governance failure into "proceed."

After an ambiguous governance result, nothing executes until a fresh decision is made. After an ambiguous execution result, the side effect may already have happened, so no new execution attempt begins until reconciliation determines whether the original attempt succeeded. What the model is told is a separate, smaller message without internal reason codes. After an ambiguous governance result, it can be told that the refund was not performed and may be retried later. After an ambiguous execution result, that would be untrue, so it is told only that the refund's outcome could not be confirmed and must be reconciled before another attempt.

## Evidence Is Not Telemetry

Four kinds of records describe this flow. They are often stored in different places, and they answer different questions:

| Kind | Answers | In this example | Binds to |
| --- | --- | --- | --- |
| Policy decision evidence | What did governance decide, under which rules? | Governance events from the kernel, plus the matched rule and policy set ID in the host's decision record | The evaluated input and the policy set |
| Workflow evidence | Who approved or acknowledged what, and when? | The approval and acknowledgment records | Operation ID, proposal fingerprint, order revision |
| Execution evidence | What happened to the side effect? | The executor's claim, the provider reference, and a Succeeded, Failed, or Unknown outcome | Operation ID and idempotency key |
| Telemetry | How is the system behaving? | Metrics such as `agent_governance.tool_calls_blocked`, logs, and traces | Aggregates and samples |

Microsoft states plainly that the toolkit's audit trail records attempts, not outcomes. Execution evidence is therefore the application's job. Telemetry supports diagnosis, but it is sampled, aggregated, and retention-managed; it is not a reliable answer to "who approved refund 7f3c, under which policy, and did the money move?"

Correlate the records through the operation ID that the host assigns. The decision, workflow, and execution records all carry it, and the adapter passes it into the evaluated arguments as `operation_id`, so the governance event emitted for that evaluation carries the same key, inside its recorded arguments, instead of only the package's own identifiers. The package merges every argument into the policy evaluation context, so nothing stops a rule from reading it; keeping rules independent of it is a convention for policy authors, and the value is there only for correlation. Because the package is in preview, the adapter contract tests later in this article confirm that the event your pinned version emits still contains it. Also record which policy set was actually loaded, and identify it by everything that can change a decision. The same policy files can decide differently under a different conflict strategy, with execution rings or prompt-injection detection switched on, or with different thresholds; equal-priority ties can depend on load order; and a file that changes on disk after the kernel loaded it must not relabel decisions the kernel made from the old content.

Both problems have one fix: read every input exactly once into an immutable snapshot, derive the identifier from those bytes, and build the kernel from the same bytes. All decision-affecting options live in one versioned options file that is part of the snapshot, rather than being set in code where the digest cannot see them. The sample is scoped to the package's in-process policy engine. An external policy backend would have to be constructed and registered inside `CreateRuntime`, from configuration that is part of the snapshot, so that its configuration is both hashed and actually used; hashing a backend's configuration file without building the backend from it would label decisions with a configuration nothing evaluates:

```csharp
public sealed record PolicySetInfo(string Id);

public sealed class GovernanceSnapshot
{
    private static readonly JsonSerializerOptions StrictJson = new()
    {
        // A misspelled or unsupported option fails startup instead of being silently ignored.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly byte[] optionsJson;
    private readonly IReadOnlyList<string> policyYaml;

    private GovernanceSnapshot(PolicySetInfo policySet, byte[] optionsJson, IReadOnlyList<string> policyYaml)
    {
        PolicySet = policySet;
        this.optionsJson = optionsJson;
        this.policyYaml = policyYaml;
    }

    public PolicySetInfo PolicySet { get; }

    // optionsPath: every decision-affecting GovernanceOptions setting, including conflict strategy,
    // rings and thresholds, and prompt-injection settings. This sample covers the in-process
    // policy engine only; it does not configure an external policy backend.
    public static GovernanceSnapshot Load(string optionsPath, IReadOnlyList<string> policyPathsInLoadOrder) =>
        // Read every input exactly once. The ID and the kernel both come from these bytes.
        FromBytes(
            File.ReadAllBytes(optionsPath),
            policyPathsInLoadOrder.Select(path => (Path.GetFileName(path), File.ReadAllBytes(path))).ToList());

    public static GovernanceSnapshot FromBytes(
        byte[] options, IReadOnlyList<(string Name, byte[] Bytes)> policies)
    {
        // Copy before hashing: a caller that mutates its arrays afterward must not be able to
        // change what the kernel loads without changing the ID.
        options = options.ToArray();
        policies = policies.Select(p => (Name: p.Name, Bytes: p.Bytes.ToArray())).ToList();

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        void Field(byte[] value)
        {
            hash.AppendData(Encoding.UTF8.GetBytes($"{value.Length}:"));
            hash.AppendData(value);
        }

        void Text(string value) => Field(Encoding.UTF8.GetBytes(value));

        Text("governance-snapshot/v1");
        Text(typeof(GovernanceKernel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? throw new InvalidOperationException("Cannot identify the governance package version."));
        Field(options);
        foreach (var (name, bytes) in policies) // load order preserved, never sorted
        {
            Text(name);
            Field(bytes);
        }

        var policySet = new PolicySetInfo(
            "sha256:" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());

        var snapshot = new GovernanceSnapshot(
            policySet, options, policies.Select(p => StrictUtf8.GetString(p.Bytes)).ToList());

        _ = snapshot.CreateOptions(); // fail at load time, not at first use, if the options are invalid
        return snapshot;
    }

    // The only way to obtain a kernel: built from the snapshot's bytes, verified, and sealed.
    public GovernanceRuntime CreateRuntime()
    {
        var kernel = new GovernanceKernel(CreateOptions());
        try
        {
            foreach (string yaml in policyYaml)
            {
                kernel.LoadPolicyFromYaml(yaml);
            }

            GovernanceStartupChecks.Verify(kernel);
        }
        catch
        {
            // A failed load or verification must not leak the kernel's metrics resources,
            // especially when a file watcher retries a broken configuration repeatedly.
            kernel.Dispose();
            throw;
        }

        // Ownership transfers only after successful verification.
        return new GovernanceRuntime(kernel, PolicySet);
    }

    private GovernanceOptions CreateOptions()
    {
        GovernanceOptions options =
            JsonSerializer.Deserialize<GovernanceOptions>(optionsJson, StrictJson)
            ?? throw new InvalidOperationException("The governance options file is empty.");

        // The package loads PolicyPaths itself, from disk, in its constructor: unhashed content,
        // and possibly a second copy of a snapshotted policy. Policies enter only as snapshot bytes.
        if (options.PolicyPaths?.Any() == true)
        {
            throw new InvalidOperationException(
                "Governance options must not set PolicyPaths; load policies through the snapshot.");
        }

        return options;
    }
}

// Holds the kernel privately, so nothing can load, clear, or reconfigure policies after startup.
// Its lifetime is lease-counted: a retired runtime is disposed only after its last lease ends.
public sealed class GovernanceRuntime
{
    private readonly GovernanceKernel kernel;
    private int leases;   // in-flight evaluations
    private int retired;  // 1 once a replacement has been published
    private int disposed; // 1 once the kernel has been disposed

    internal GovernanceRuntime(GovernanceKernel kernel, PolicySetInfo policySet)
    {
        this.kernel = kernel;
        PolicySet = policySet;
    }

    public PolicySetInfo PolicySet { get; }

    public ToolCallResult EvaluateToolCall(string agentId, string toolName, Dictionary<string, object> args) =>
        kernel.EvaluateToolCall(agentId, toolName, args);

    // Internal: only the holder subscribes, so every runtime it publishes gets the same handlers.
    internal void OnAllEvents(Action<GovernanceEvent> handler) => kernel.OnAllEvents(handler);

    // Increment first, then check: paired with Retire, which marks first, then checks. Both
    // Interlocked operations are full fences, so one side always sees the other.
    internal bool TryAcquire()
    {
        Interlocked.Increment(ref leases);
        if (Volatile.Read(ref retired) == 1)
        {
            Release();
            return false;
        }

        return true;
    }

    internal void Release()
    {
        if (Interlocked.Decrement(ref leases) == 0 && Volatile.Read(ref retired) == 1)
        {
            DisposeOnce();
        }
    }

    internal void Retire()
    {
        Interlocked.Exchange(ref retired, 1);
        if (Volatile.Read(ref leases) == 0)
        {
            DisposeOnce();
        }
    }

    private void DisposeOnce()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            kernel.Dispose();
        }
    }
}

public sealed class RuntimeLease : IDisposable
{
    private GovernanceRuntime? runtime;

    internal RuntimeLease(GovernanceRuntime runtime) => this.runtime = runtime;

    public GovernanceRuntime Runtime =>
        Volatile.Read(ref runtime) ?? throw new ObjectDisposedException(nameof(RuntimeLease));

    public void Dispose() => Interlocked.Exchange(ref runtime, null)?.Release();
}

// A configuration change publishes a whole new runtime, so the kernel and its policy set ID change
// together, and the old runtime is retired rather than disposed out from under in-flight evaluations.
public sealed class GovernanceRuntimeHolder : IDisposable
{
    private readonly object gate = new(); // serializes Replace and Dispose
    private readonly Action<GovernanceEvent>[] eventHandlers;
    private GovernanceRuntime current;
    private int disposed;
    private long requestedGeneration; // assigned when a Replace call starts
    private long publishedGeneration; // generation of `current`; written only under the lock

    // The holder owns the event subscriptions, such as the evidence recorder's handler, and applies
    // them to the initial runtime and to every replacement before it is published. Evidence keeps
    // flowing across policy reloads instead of silently stopping at the first one.
    public GovernanceRuntimeHolder(GovernanceRuntime initial, IEnumerable<Action<GovernanceEvent>> eventHandlers)
    {
        this.eventHandlers = eventHandlers.ToArray();
        Subscribe(initial);
        current = initial;
    }

    private void Subscribe(GovernanceRuntime runtime)
    {
        foreach (Action<GovernanceEvent> handler in eventHandlers)
        {
            runtime.OnAllEvents(handler);
        }
    }

    // Reading the ID needs no lease; it never touches the kernel.
    public PolicySetInfo CurrentPolicySet => Volatile.Read(ref current).PolicySet;

    // A retired runtime is never handed out: if a replacement retires the runtime between the
    // read and the lease, the loop reads again and leases its successor. After Dispose there is
    // no successor, so the loop stops with ObjectDisposedException instead of spinning.
    public RuntimeLease Acquire()
    {
        while (true)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) == 1, this);

            GovernanceRuntime runtime = Volatile.Read(ref current);
            if (runtime.TryAcquire())
            {
                return new RuntimeLease(runtime);
            }
        }
    }

    // Returns false when nothing was published: either a newer Replace call already published
    // (the later request wins, however long each build takes) or the snapshot is identical to the
    // running one.
    public bool Replace(GovernanceSnapshot snapshot)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) == 1, this);

        // The generation records the order in which replacements were requested.
        long generation = Interlocked.Increment(ref requestedGeneration);

        // Build and verify outside the lock; it reads no shared state.
        GovernanceRuntime next = snapshot.CreateRuntime();
        try
        {
            Subscribe(next); // before publishing: no evaluation on `next` can go unrecorded
        }
        catch
        {
            next.Retire();
            throw;
        }

        lock (gate)
        {
            if (disposed == 1)
            {
                next.Retire(); // a replacement that lost the race with Dispose is retired, not leaked
                throw new ObjectDisposedException(nameof(GovernanceRuntimeHolder));
            }

            if (generation < publishedGeneration)
            {
                next.Retire(); // a slower, older build never overwrites a newer published snapshot
                return false;
            }

            if (StringComparer.Ordinal.Equals(next.PolicySet.Id, current.PolicySet.Id))
            {
                // Same configuration: keep the running kernel and its rate-limit windows and
                // circuit-breaker state, but advance the generation so an older in-flight build
                // still cannot publish afterward.
                publishedGeneration = generation;
                next.Retire();
                return false;
            }

            GovernanceRuntime previous = current;
            publishedGeneration = generation;
            Volatile.Write(ref current, next);
            previous.Retire(); // disposed when its last in-flight evaluation releases its lease
            return true;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed == 1)
            {
                return;
            }

            // Mark the holder first, then retire: an Acquire that finds the runtime retired
            // re-reads the flag and throws rather than looping forever.
            Volatile.Write(ref disposed, 1);
            current.Retire();
        }
    }
}
```

At startup, load the snapshot, call `CreateRuntime`, which builds the kernel from the snapshot's bytes and runs `GovernanceStartupChecks.Verify` on it, and register a `GovernanceRuntimeHolder` around the result, passing it the event handlers the application needs, such as the evidence recorder's. The holder owns those subscriptions and applies them to every replacement before publishing it, because a new kernel starts with none; without that, governance events would stop reaching the recorder after the first policy reload. Nothing else in the application ever receives the `GovernanceKernel`, so nothing can load or clear policies, add a backend, or change the conflict strategy behind the policy set ID's back. If loading or verification fails, `CreateRuntime` disposes the half-built kernel before rethrowing, so a broken configuration retried by a file watcher does not leak a kernel on every attempt. A configuration change is a new snapshot and a new runtime, published as a single reference swap: every evaluation leases one runtime, so a decision's policy set ID always describes the kernel that made it, and approvals and acknowledgments granted under the old ID stop counting. The replaced runtime is retired rather than disposed: new evaluations can no longer lease it, and its kernel, with its metrics resources, is disposed when the last in-flight evaluation releases its lease. Evaluation is synchronous here, so a lease lasts only as long as one `EvaluateToolCall`. Each `Replace` call takes a generation number when it starts, and a build that finishes after a newer one has been published is retired instead of published, so concurrent replacements take effect in the order they were requested, not the order their builds happened to finish. A snapshot identical to the running one is not published at all. The kernel holds rate-limit windows and circuit-breaker state in memory, so republishing an unchanged configuration, for example on every file-watcher event, would reset those controls and let repeated reloads bypass a `1/hour` limit. A genuine policy change does start the new kernel with fresh windows; if a limit must survive policy changes, enforce it in the host or in a store outside the kernel as well. `Replace` and `Dispose` are serialized, so a replacement can never be published after the holder is disposed; one that loses that race is retired immediately, and once the holder is disposed, `Acquire` and `Replace` throw `ObjectDisposedException` instead of spinning on a retired runtime. The options file must not list `PolicyPaths`, because the package would load those files itself, outside the snapshot; `CreateOptions` rejects them. Hashing raw bytes is deliberately conservative: reformatting a file changes the identifier and asks for fresh approvals and acknowledgments, but two configurations that can decide differently can never share one. Before relying on the options file, confirm in an adapter test that your pinned version's `GovernanceOptions` round-trips through `System.Text.Json` with every setting you use.

Keep secrets and raw model text out of the decision record, and never read the record back as permission. Evidence explains what happened; it does not authorize what happens next. [Your Audit Log Records the Story, Not the Decision](your-audit-log-is-not-evidence.md) covers the difference between a log line and evidence that can be trusted later.

## Responsibilities at a Glance

| Concern | Microsoft.AgentGovernance role | Host application role |
| --- | --- | --- |
| Agent action interception | Evaluate the proposed action before it runs | Ensure every path to the protected side effect actually passes through the boundary |
| Policy | Apply governance rules deterministically | Supply trusted context and business facts; own domain invariants |
| Identity and trust | Agent identity, trust scores, execution rings | User, tenant, resource, and application identity semantics |
| Human oversight | Express that approval is required; where an SDK implements ADR-0030, bind approvals to the exact action | Own workflow state, reviewer eligibility, revision binding, and expiry for the application's resources |
| Execution | Constrain governed action paths | Own the protected side effect and guarantee that non-allowed paths do not execute |
| Evidence | Governance events and audit records of attempts | Correlate policy, workflow, and execution evidence, including outcomes |
| Failure | Describe a fail-closed posture; some evaluation errors surface as exceptions rather than decisions | Map every exception or missing decision to a non-executing outcome; decide retries, degradation, and what users and the model are told |

The two columns are complementary. Neither replaces the other, and neither is complete alone: a governance library without host boundaries evaluates claims it cannot verify, and a host without runtime governance has no consistent, deterministic place to express which actions agents may take.

## Prove That Non-Allowed Paths Never Execute

Two kinds of tests keep this architecture honest.

Gateway tests use the application's own interface, so they need neither the package nor a model. They assert that no non-allowed outcome reaches the executor:

```csharp
public sealed class RefundGatewayTests
{
    [Theory]
    [InlineData(GovernedOutcome.Denied)]
    [InlineData(GovernedOutcome.ApprovalRequired)]
    [InlineData(GovernedOutcome.AcknowledgmentRequired)]
    [InlineData(GovernedOutcome.Escalated)]
    [InlineData(GovernedOutcome.Deferred)]
    [InlineData(GovernedOutcome.Unavailable)]
    public async Task Non_allowed_governance_outcomes_never_reach_the_executor(GovernedOutcome outcome)
    {
        var executor = new CountingRefundExecutor();
        RefundGateway gateway = TestGateway.Create(new FixedGovernance(outcome), executor);

        RefundResult result = await gateway.HandleAsync(
            TestData.Proposal(amount: 240.00m), TestData.Session(), Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task Unauthorized_support_agent_never_reaches_governance_or_the_executor()
    {
        var governance = new CountingGovernance(GovernedOutcome.Allowed);
        var executor = new CountingRefundExecutor();
        RefundGateway gateway = TestGateway.Create(governance, executor, supportAgentMayRefund: false);

        RefundResult result = await gateway.HandleAsync(
            TestData.Proposal(amount: 40.00m), TestData.Session(), Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(0, governance.Calls);
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task Fraud_hold_escalates_without_calling_governance_or_the_executor()
    {
        var governance = new CountingGovernance(GovernedOutcome.Allowed);
        var executor = new CountingRefundExecutor();
        RefundGateway gateway = TestGateway.Create(governance, executor, fraudHold: true);

        RefundResult result = await gateway.HandleAsync(
            TestData.Proposal(amount: 40.00m), TestData.Session(), Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(0, governance.Calls);
        Assert.Equal(0, executor.Calls);
    }
}
```

Adapter contract tests run against the real package with an inline policy. Because the package is in public preview, these are the tests to run on every upgrade:

```csharp
public sealed class AgentGovernanceRefundPolicyContractTests
{
    [Theory]
    [InlineData(15_000L, false, GovernedOutcome.Allowed)]
    [InlineData(24_000L, false, GovernedOutcome.ApprovalRequired)]
    [InlineData(24_000L, true, GovernedOutcome.Allowed)]
    public void Maps_documented_policy_actions_to_application_outcomes(
        long amountMinor, bool approved, GovernedOutcome expected)
    {
        // TestGovernance.Runtime builds a GovernanceSnapshot from inline options and policy text.
        var policy = new AgentGovernanceRefundPolicy(
            TestGovernance.Runtime(TestPolicies.SupportAssistantRefunds)); // the policy shown earlier
        RefundContext context = TestData.Context(amountMinor);

        GovernedDecision first = policy.Evaluate(context);
        GovernedDecision decision = approved
            ? policy.Evaluate(context, ApprovalRequirement.For(context, first))
            : first;

        Assert.Equal(expected, decision.Outcome);
    }

    [Fact]
    public void Approval_for_another_risk_tier_never_counts()
    {
        var policy = new AgentGovernanceRefundPolicy(
            TestGovernance.Runtime(TestPolicies.SupportAssistantRefunds));
        RefundContext lowRisk = TestData.Context(24_000L) with { RiskTier = RiskTier.Low };
        ApprovalRequirement approvedForLowRisk = ApprovalRequirement.For(lowRisk, policy.Evaluate(lowRisk));

        RefundContext highRisk = lowRisk with { RiskTier = RiskTier.High };

        Assert.Equal(GovernedOutcome.Unavailable, policy.Evaluate(highRisk, approvedForLowRisk).Outcome);
    }

    [Fact]
    public void Approval_requirement_carries_the_policy_declared_approvers()
    {
        var policy = new AgentGovernanceRefundPolicy(
            TestGovernance.Runtime(TestPolicies.SupportAssistantRefunds));
        RefundContext context = TestData.Context(24_000L);

        GovernedDecision decision = policy.Evaluate(context);
        ApprovalRequirement requirement = ApprovalRequirement.For(context, decision);

        Assert.Equal(new[] { "support-supervisors" }, decision.RequiredApprovers);
        Assert.Equal(new[] { "support-supervisors" }, requirement.ApproverGroupNames());
    }

    [Fact]
    public void Maps_both_rate_limit_states()
    {
        var policy = new AgentGovernanceRefundPolicy(
            TestGovernance.Runtime(TestPolicies.OneAssistantRefundPerHour)); // rate_limit rule, limit "1/hour"
        RefundContext context = TestData.Context(5_000L);

        Assert.Equal(GovernedOutcome.Allowed, policy.Evaluate(context).Outcome);  // within the window
        Assert.Equal(GovernedOutcome.Deferred, policy.Evaluate(context).Outcome); // limit exhausted
    }

    [Fact]
    public void Governance_event_carries_the_host_operation_id()
    {
        var events = new List<GovernanceEvent>();
        GovernanceRuntimeHolder runtimes = TestGovernance.Runtime(
            eventHandlers: [events.Add], TestPolicies.SupportAssistantRefunds);

        RefundContext context = TestData.Context(15_000L);
        new AgentGovernanceRefundPolicy(runtimes).Evaluate(context);

        Assert.Contains(events, e => CarriesOperationId(e, context.OperationId));
    }

    [Fact]
    public void Event_handlers_follow_the_runtime_across_a_replacement()
    {
        var events = new List<GovernanceEvent>();
        GovernanceRuntimeHolder runtimes = TestGovernance.Runtime(
            eventHandlers: [events.Add], TestPolicies.SupportAssistantRefunds);

        // A different policy set, so the replacement is actually published.
        Assert.True(runtimes.Replace(TestGovernance.Snapshot(TestPolicies.SupportAssistantRefundsRevised)));

        RefundContext context = TestData.Context(15_000L);
        new AgentGovernanceRefundPolicy(runtimes).Evaluate(context);

        Assert.Contains(events, e => CarriesOperationId(e, context.OperationId));
    }

    private static bool CarriesOperationId(GovernanceEvent e, Guid operationId) =>
        e.Data.TryGetValue("arguments", out object? args) &&
        args is IReadOnlyDictionary<string, object> arguments &&
        arguments.TryGetValue("operation_id", out object? id) &&
        Equals(id, operationId.ToString("N"));

    [Fact]
    public void Acknowledged_warning_completes_only_for_the_same_policy_set_policy_and_rule()
    {
        // Policy "assistant-refund-warnings" with warn rule "refund-warning".
        GovernanceRuntimeHolder runtimes = TestGovernance.Runtime(TestPolicies.WarnOnAssistantRefunds);
        var policy = new AgentGovernanceRefundPolicy(runtimes);
        string policySetId = runtimes.CurrentPolicySet.Id;
        string currentKey = WarningKeys.For(policySetId, "assistant-refund-warnings", "refund-warning");

        RefundContext unacknowledged = TestData.Context(5_000L);
        RefundContext acknowledged = unacknowledged with
        {
            AcknowledgedWarnings = new HashSet<string> { currentKey },
        };
        RefundContext acknowledgedUnderOldPolicy = unacknowledged with
        {
            AcknowledgedWarnings = new HashSet<string>
            {
                WarningKeys.For("sha256:previous", "assistant-refund-warnings", "refund-warning"),
            },
        };
        RefundContext acknowledgedForSameRuleNameInAnotherPolicy = unacknowledged with
        {
            AcknowledgedWarnings = new HashSet<string>
            {
                WarningKeys.For(policySetId, "another-policy", "refund-warning"),
            },
        };

        Assert.Equal(GovernedOutcome.AcknowledgmentRequired, policy.Evaluate(unacknowledged).Outcome);
        Assert.Equal(GovernedOutcome.Allowed, policy.Evaluate(acknowledged).Outcome);
        Assert.Equal(currentKey, policy.Evaluate(acknowledged).AcknowledgedWarning);
        Assert.Equal(GovernedOutcome.AcknowledgmentRequired, policy.Evaluate(acknowledgedUnderOldPolicy).Outcome);
        Assert.Equal(
            GovernedOutcome.AcknowledgmentRequired,
            policy.Evaluate(acknowledgedForSameRuleNameInAnotherPolicy).Outcome);
    }

    [Fact]
    public void Startup_rejects_a_permissive_default_even_when_the_probe_is_denied()
    {
        // default_action: allow, plus a rule that denies only the probe tool.
        Assert.Throws<InvalidOperationException>(
            () => TestGovernance.Runtime(TestPolicies.PermissiveDefaultThatDeniesTheProbe));
    }

    [Fact]
    public void Startup_rejects_blank_policy_rule_and_approver_names()
    {
        // A policy whose require_approval rule declares approvers: [" "].
        Assert.Throws<InvalidOperationException>(
            () => TestGovernance.Runtime(TestPolicies.ApprovalRuleWithBlankApprover));
    }

    [Fact]
    public void Startup_rejects_a_rule_name_reused_in_another_policy()
    {
        // A second policy, with a different policy name, that also declares a rule
        // named "refund-needs-supervisor-approval".
        Assert.Throws<InvalidOperationException>(
            () => TestGovernance.Runtime(
                TestPolicies.SupportAssistantRefunds,
                TestPolicies.OtherPolicyReusingTheApprovalRuleName));
    }
}
```

The first group proves the boundary: whatever governance decides, only `Allowed` reaches the executor. The second group proves the translation and the configuration checks: the package's actions, including both rate-limit states, still map to the outcomes the application depends on, an approval granted for one requirement never satisfies another, the policy-declared approver groups reach the stored requirement, a permissive default cannot hide behind a denied probe, blank identifiers and a rule name reused in another policy stop startup, and the governance event still carries the host's operation ID, including from a runtime published by a later reload. Add an executor test that publishes a new policy set between evaluation and reservation and expects `policy_set_changed`, a RolledBack operation that can be reserved again, and zero provider calls, a holder test showing that an older snapshot finishing its build last does not replace a newer one, approval-store tests showing that an approval is found only for its exact requirement, so one granted for a different amount, currency, reason, order, revision, rule, policy set, or risk tier is never returned, and executor tests for a changed order revision, a fraud hold placed after the decision, a terminal provider failure that releases the reservation, and an unknown outcome that keeps it. [How to Test That a Denied Operation Never Executes](test-denied-operation-never-executes.md) covers counting executors, composition-root tests, and the time-of-check-to-time-of-use window in more depth.

## Common Failure Modes

- **Treating the governance library as application authorization.** The verdict says what this agent may do under governance rules. It does not say whether this person may act on this tenant's order, and it does not enforce business invariants.
- **Trusting agent-supplied tenant, account, resource, or role values.** Valid structure is not authority. The host resolves those facts.
- **Evaluating correctly but executing elsewhere.** A retry job or administration endpoint that calls the executor directly makes the governance check decorative.
- **Revalidating only the row being written.** A fact held elsewhere, such as a fraud hold in a risk system, can revoke permission without changing the order's revision.
- **Persisting an allow decision.** An earlier allow, replayed after the policy or the resource changed, authorizes an operation nobody evaluated.
- **Confusing approval, acknowledgment, and authorization.** Each answers a different question, and none silently becomes permission to execute.
- **Assuming audit logging proves non-execution.** A recorded denial shows what governance decided, not what every other code path did.
- **Putting application workflow state or business side effects inside the policy evaluator.** The package keeps its own rate-limit and telemetry state; approvals, acknowledgments, and execution live in the application.
- **Failing open on governance errors.** Proceeding when evaluation fails, times out, or was never configured is a risk decision. Make it explicitly, per tool, or do not make it at all.
- **Duplicating one rule in several layers.** A balance check in the YAML, the handler, and the executor drifts. Give each rule one owner, and let the executor revalidate only the facts that can change before the write.
- **Wrapping every low-risk call in heavyweight machinery.** Approval workflows, grants, and remote decision points have costs. Use them where the lifecycle needs them.

## When Simpler Is Enough

Not every agent integration needs every layer described here. Microsoft's own limitations page notes that the full toolkit stack can be more than simple use cases need, and its packages can be adopted independently.

For an assistant that looks up order status, summarizes a ticket, or drafts a reply for a person to send, this is often enough:

```text
Framework registers only the tools this assistant may use
      ↓
Governance middleware or filter evaluates each call before it runs
      ↓
Handler reads the resource from the session, not from arguments
      ↓
Ordinary ASP.NET Core authorization for the person and the resource
      ↓
Immediate, same-process execution
```

That is sufficient when nothing else can reach the protected operation, the facts come from the session and the system of record, execution is immediate, there is no approval or delayed step, and ordinary logs are adequate evidence. [When ASP.NET Core Authorization Is Enough](../../architecture/when-aspnet-core-authorization-is-enough.md) describes that baseline in more detail.

Add structure when the lifecycle outgrows the request: approvals that pause execution, work that runs later or elsewhere, execution with less authority than the requester, or evidence that must outlive the logs.

## The Checklist

Before a governed agent action reaches a consequential side effect, confirm that:

1. **The policy evaluates host-resolved facts.** Order, tenant, actor, and approval state come from the host; the model contributes only validated proposal values.
2. **Each rule has one owner.** Domain invariants live in the application; agent-specific governance rules live in the policy.
3. **The verdict is translated, not collapsed.** Approval-required, deferred, escalated, acknowledgment-required, and unavailable remain distinct, and unrecognized decisions never become permission.
4. **Application authorization runs explicitly,** and only `Allowed` reaches the executor, which is reachable only through the gateway.
5. **Governance is verified at startup.** At least one policy is loaded, no policy name, rule name, or approver entry is blank, every rule name is unique across the policy set, every loaded policy declares `default_action: deny`, an unregistered tool is actually denied, and the kernel and its policy set ID both come from one immutable snapshot of the package version, options, and policy files.
6. **Approval and acknowledgment are host workflow state.** An approval is looked up only after evaluation names the requirement, and it is bound to that exact requirement (operation, proposal fingerprint, policy set, policy, rule, declared approver groups, assistant, and risk tier), checked again at consumption. Both have expiry, eligibility, and single use. An acknowledgment is bound to the proposal fingerprint, the specific warning, and the policy set, so an acknowledged warning completes instead of being requested again.
7. **Execution revalidates every mutable fact that could revoke permission,** reserves before calling out, and bounds execution authority only where delay or delegation requires it.
8. **Failure semantics are explicit per tool.** An ambiguous governance result never authorizes execution, and an ambiguous execution outcome is reconciled before any new execution attempt.
9. **Evidence binds policy, workflow, and execution records** through the operation ID; telemetry is not treated as evidence.
10. **Tests assert zero executor calls** for every non-allowed outcome, and adapter contract tests run on every package upgrade.

## Continue Deeper

- [Why an AI Tool Call Is a Proposal, Not Authority](why-ai-tool-call-is-only-a-proposal.md) makes the underlying argument: valid JSON and a known tool do not create authority.
- [Policy as Code in ASP.NET Core Without Overengineering](policy-as-code-aspnet-core-without-overengineering.md) follows a refund with no model in the loop and compares where a policy decision can live, from plain code to a remote decision service.
- [Governance Tool Selection and Composition](../../architecture/governance-tool-selection-and-composition.md) compares agent governance, policy engines, authorization, and application execution boundaries without treating them as substitutes.
- [Policy Versioning and Decision Provenance](../../governance/policy-versioning-and-decision-provenance.md) goes deeper on recording which policy produced a decision.

---

> **Read it. Run it. Question it. Improve it.**
