---
description: A host-side checklist for AI tool calls, covering parsing, allowlisting, schema, semantics, host context, policy, approval, execution, and evidence.
title: What Should an AI Tool Gateway Validate Before Execution?
author: Christopher D. Cavell
published: "2026-09-27"
summary: Structured model output becomes eligible for execution only after the trusted host independently validates its shape, meaning, context, authority, and current permission. This is the ordered checklist, covering approvals, retries, and uncertain outcomes, with tests that prove proposals blocked before execution never reach the executor.
feed: true
---

# What Should an AI Tool Gateway Validate Before Execution?

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** Familiarity with C# and `System.Text.Json` is helpful. No AI provider, agent framework, AsiBackbone package, or prior Learning material is required. The code targets .NET 10, which added `AllowDuplicateProperties` (step 1) and the `JsonSerializerOptions.Strict` preset (step 3). On older versions, set the equivalent options individually; `RespectNullableAnnotations` needs .NET 9.

**What this article covers:** an ordered, host-owned acceptance sequence for model-generated tool calls, from raw JSON to the protected side effect; what each step validates and what failure looks like; which values the model may supply and which the host must own; approvals, retries, and uncertain outcomes; tests that prove every path blocked before execution makes zero executor calls; and when ordinary framework controls are enough.

Your application already accepts tool calls from a model. The model returns something like this, and your code is expected to act on it:

```json
{
  "name": "orders.refund",
  "arguments": { "amount": 25.50, "currency": "USD", "reason": "Damaged" }
}
```

The payload is valid JSON, and it names a real tool. The tempting next line of code deserializes the arguments and calls the refund service.

That line skips most of the decisions that matter. Valid JSON says nothing about whether this order may be refunded, whether $25.50 is refundable, whether the agent may issue refunds, or whether the order is on a fraud hold. If the model had also written `"orderId": "order-2002"`, a naive handler might have refunded a different customer.

The short rule is: **the model may propose; the host retains execution authority.** [Why an AI Tool Call Is a Proposal, Not Authority](why-ai-tool-call-is-only-a-proposal.md) explains why. This article is the ordered checklist that implements it. It starts once the structured output has arrived and answers a practical question: **what, exactly, should the host check, in what order, before the side effect happens?**

> Structured model output becomes eligible for execution only after the trusted host independently validates its shape, meaning, context, authority, and current permission.

## The Threat Model in Four Sentences

The model is an untrusted proposer: its output can be wrong, manipulated by prompt injection, or simply invented. The host is the only authority: it owns identity, current facts, policy, approval, and credentials. The executor is the only path to the side effect, and nothing else can reach it. Logs and records are evidence of what happened, never permission for what happens next: operators and reconciliation may read them, but authorization never does.

## The Example: A Support Assistant That Can Issue Refunds

A support agent works a customer conversation that the application opened for one specific order. An assistant can propose a refund through the `orders.refund` tool.

The first design decision is which values the model may propose at all:

| Value | Who supplies it | Why |
| --- | --- | --- |
| Refund amount | Model proposes; host validates | The model can read the conversation and suggest an amount. |
| Currency | Model proposes; host checks against the order | A useful consistency check, never a conversion instruction. |
| Reason (`Damaged`, `NotDelivered`, `WrongItem`) | Model proposes from a fixed set | Classification is something a model does well. |
| Which order | Host, from the conversation | The conversation was opened for one order. The model must not redirect it. |
| Which agent, tenant, and permissions | Host, from authentication | Identity is never a model output. |
| Refundable balance, fraud hold, order version | Host, from the order store | Current facts come from the system of record. |
| Approval for a large refund | Host, from its approval workflow | Consent comes from a person, not from the proposal. |
| Payment credentials | Host-owned executor only | The model never sees or selects a credential. |

The design rule behind the table travels further than the example: **classifications and quantities may come from the model; identity, permission, current facts, approval, and credentials may not.**

This example also assumes the conversation belongs to the right customer. The host established that when it opened the conversation, which is why the order binding can be trusted.

## The Acceptance Sequence at a Glance

```text
Raw model output
      ↓
[gateway]       1. Parse structurally
[gateway]       2. Resolve the tool through a host-owned allowlist
      ↓
[tool handler]  3. Validate the argument schema
[tool handler]  4. Validate semantics that need no host data
[tool handler]  5. Resolve authoritative facts from host context
[tool handler]  6. Evaluate authorization and operational policy
[tool handler]  7. Require human approval when the operation needs it
[tool handler]  8. Invoke the host-owned executor
      ↓
[gateway]       9. Record the outcome
```

The **gateway** runs steps 1, 2, and 9, which are the same for every tool. Each **tool handler** runs steps 3 to 8 for its own operation. Each step answers a different question and fails differently:

| Step | Question | On failure | Executor reached? |
| --- | --- | --- | --- |
| 1. Parse | Is this a well-formed tool-call envelope of reasonable size? | Rejected | No |
| 2. Allowlist | Is this a tool the host exposes on this path? | Rejected | No |
| 3. Schema | Are the arguments exactly the fields and types the host defined? | Rejected | No |
| 4. Semantics | Do the arguments make sense on their own? | Rejected | No |
| 5. Host context | Do the arguments fit the current, authoritative facts? | Rejected or unavailable | No |
| 6. Authorization and policy | May this agent do this to this order now? | Denied | No |
| 7. Human approval | Has an authorized person approved exactly this refund, if required? | Pending | No |
| 8. Execute | Did the write succeed against the facts that were checked? | Unavailable | Yes, once |
| 9. Record | What happened, and why? | (runs for every outcome) | n/a |

The outcomes are deliberately distinct. `Rejected` means the proposal was not acceptable input. `Denied` means an acceptable proposal is not allowed. `PendingApproval` means it is allowed only with consent that does not exist yet. `Unavailable` means host facts or host systems do not permit execution right now: the order is not visible, it changed, a dependency failed, or the payment outcome is not yet known. None of these is `Executed`.

The order matters. Cheap, context-free checks run first, so obviously bad proposals never cost a database read. Checks that need trusted data run only after the proposal is known to be well formed. The protected side effect is the last thing that can happen, not something that has to be undone.

Many model APIs return several tool calls at once. Treat each call as its own pass through steps 1 to 9. A valid call must not carry an invalid sibling through, and one call's approval or success grants nothing to the next. If the calls are independent, they can be evaluated in any order. If a later call depends on an earlier one, run them in sequence and stop at the first outcome that is not `Executed`.

## What the Sequence Stops

| Attempt | Stopped at |
| --- | --- |
| Prompt injection adds `"orderId": "order-2002"` | Step 3 (`argument.host-owned`); step 5 uses the host-bound order regardless |
| The model writes `"approved": true` | Step 3 (`argument.host-owned`); step 7 reads only host-held approvals |
| Repeated `amount` properties with different values | Step 1 (`parse.malformed`) |
| The model invents a tool name | Step 2 (`tool.unknown`) |
| An integer where an enum name was expected | Step 4 (`reason.invalid`) |
| A hallucinated amount above what was paid | Step 5 (`amount.exceeds-refundable`) |
| An agent without refund permission | Step 6 (`agent.not-authorized`) |
| A large refund without a supervisor | Step 7 (`refund.approval-required`) |
| Another agent refunds the order first | Step 8 (`order.changed`) |
| A provider retry of the same reservation | Step 8's stored idempotency key |
| A new attempt while an earlier refund's outcome is unknown | Step 5 (`refund.in-progress`) |

## Step 1: Parse Structurally

Parsing turns bytes into a JSON document. It is a small claim: the payload is well formed, not that it is meaningful or safe. Still, a few structural rules belong here, because later steps cannot recover what the parser discards:

```csharp
public sealed record RawToolCall(string ToolName, JsonElement Arguments);

public static class ToolCallParser
{
    private const int MaxPayloadBytes = 16 * 1024;

    private static readonly JsonDocumentOptions Options = new()
    {
        MaxDepth = 8,
        AllowDuplicateProperties = false
    };

    public static bool TryParse(
        string rawPayload,
        out RawToolCall? call,
        out string? reasonCode)
    {
        call = null;

        if (Encoding.UTF8.GetByteCount(rawPayload) > MaxPayloadBytes)
        {
            reasonCode = "parse.too-large";
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(rawPayload, Options);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("name", out JsonElement name) ||
                name.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("arguments", out JsonElement arguments) ||
                arguments.ValueKind != JsonValueKind.Object ||
                root.EnumerateObject().Count() != 2)
            {
                reasonCode = "parse.envelope-invalid";
                return false;
            }

            call = new RawToolCall(name.GetString()!, arguments.Clone());
            reasonCode = null;
            return true;
        }
        catch (JsonException)
        {
            reasonCode = "parse.malformed";
            return false;
        }
    }
}
```

What this step validates:

- **Size before parsing.** A bounded payload keeps a model that loops or repeats itself from becoming a memory problem.
- **Depth.** Tool arguments are shallow. A deeply nested document is not a legitimate proposal.
- **Duplicate properties.** `{"amount": 25, "amount": 2500}` is valid JSON to many parsers, which silently keep one value. A person reviewing the payload and the code acting on it could then see different numbers. Reject the ambiguity while the raw document still shows it. Before .NET 10, `AllowDuplicateProperties` is not available, so check for repeated names while enumerating the raw document instead.
- **The envelope's exact shape.** One tool name, one arguments object, nothing else. An extra top-level field such as `"approved": true` cannot smuggle meaning in beside the arguments. Provider-specific envelopes (OpenAI, Anthropic, Gemini, and others each shape tool calls differently) are normalized to this shape in an adapter, so vendor metadata never reaches the gateway as arguments.
- **Failures as results, not exceptions.** A malformed payload is an expected input, not an application error. It becomes a rejection with a reason code, and nothing escapes into the agent loop.

## Step 2: Resolve the Tool Through a Host-Owned Allowlist

The host, not the model, defines which operations exist on this path. The gateway resolves the proposed name through a registry and runs the shared steps around the tool-specific ones:

```csharp
public interface IToolHandler
{
    string Name { get; }

    Task<ToolOutcome> HandleAsync(
        JsonElement arguments,
        ConversationContext context,
        CancellationToken cancellationToken);
}

public sealed class ToolGateway(
    IEnumerable<IToolHandler> handlers,
    IToolDecisionLog decisions)
{
    private readonly Dictionary<string, IToolHandler> registry =
        handlers.ToDictionary(handler => handler.Name, StringComparer.Ordinal);

    public async Task<ToolOutcome> HandleAsync(
        string rawPayload,
        ConversationContext context,
        CancellationToken cancellationToken)
    {
        ToolOutcome outcome;
        string toolName = "(unparsed)";

        if (!ToolCallParser.TryParse(rawPayload, out RawToolCall? call, out string? parseReason))
        {
            outcome = ToolOutcome.Reject("parse", parseReason!);
        }
        else if (!registry.TryGetValue(call!.ToolName, out IToolHandler? handler))
        {
            toolName = "(unknown)";
            outcome = ToolOutcome.Reject("allowlist", "tool.unknown");
        }
        else
        {
            toolName = handler.Name;

            try
            {
                outcome = await handler.HandleAsync(call.Arguments, context, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A failing host dependency is an outcome, not a crash in the agent loop.
                // Log the exception for operators: it may be an outage or a defect.
                outcome = ToolOutcome.Unavailable("host", "host.error");
            }
        }

        decisions.Record(new ToolDecisionRecord(
            toolName,
            context.BoundOrder.Value,
            outcome.Kind,
            outcome.Stage,
            outcome.ReasonCode));

        return outcome;
    }
}
```

What this step validates:

- **Membership, with an exact comparison.** The registry uses `StringComparer.Ordinal`, so `Orders.Refund` is not `orders.refund`. Tool names are identifiers, not prose.
- **Nothing tool-specific runs for an unknown tool.** There is no fallback to a "closest" tool, no reflection, and no `Type.GetType(name)`. An unknown name stops here, before any argument is interpreted.
- **The registry is the only route to a handler.** If the same handler is also reachable from another endpoint, background job, or auto-dispatching SDK, that path needs the same checks, or this allowlist is only a suggestion.
- **Host failures stay inside the outcome model.** If the order store times out, the gateway records `host.error` as `Unavailable` rather than letting an exception escape into the agent loop, where a retry policy might treat it as permission to try something else. Every host failure collapses to the same `host.error` on purpose: the agent loop must not infer retry behavior from internal exceptions. Operators still need the detail, so log the exception itself. A timeout is an outage, but a `NullReferenceException` is a defect, and retry tuning will not fix it.

The unknown name is not written to the record verbatim. Model-chosen text is untrusted, and a log is a sink like any other.

Rate limits belong in front of the gateway, not inside it. A runaway agent loop can issue hundreds of rejected calls a minute. A per-conversation or per-agent limit that runs before parsing protects the host's CPU as well as its database, and it is a separate control from any of the validation steps.

The results use one small type throughout:

```csharp
public enum ToolOutcomeKind
{
    Rejected,
    Denied,
    PendingApproval,
    Unavailable,
    Executed
}

public sealed record ToolOutcome(
    ToolOutcomeKind Kind,
    string Stage,
    string ReasonCode)
{
    public static ToolOutcome Reject(string stage, string reason) =>
        new(ToolOutcomeKind.Rejected, stage, reason);

    public static ToolOutcome Deny(string stage, string reason) =>
        new(ToolOutcomeKind.Denied, stage, reason);

    public static ToolOutcome Unavailable(string stage, string reason) =>
        new(ToolOutcomeKind.Unavailable, stage, reason);
}
```

## Step 3: Validate the Argument Schema

The refund tool accepts exactly three fields, and the host defines their types:

```csharp
[JsonConverter(typeof(JsonStringEnumConverter<RefundReason>))]
public enum RefundReason
{
    Damaged,
    NotDelivered,
    WrongItem
}

public sealed record RefundArguments(
    [property: JsonRequired] decimal Amount,
    [property: JsonRequired] string Currency,
    [property: JsonRequired] RefundReason Reason);
```

The refund handler owns steps 3 to 8. It receives the order store, the approval store, the executor, and a clock from the host:

```csharp
public sealed class RefundToolHandler(
    IOrderStore orders,
    IRefundApprovals approvals,
    IRefundExecutor executor,
    TimeProvider clock) : IToolHandler
{
    private const decimal ApprovalThreshold = 250m;

    public string Name => "orders.refund";

    // HandleAsync runs the step 3 to 8 fragments below, in order.
}
```

It names the fields the model must never supply, and deserializes with options that fail closed:

```csharp
private static readonly string[] HostOwnedFields =
    ["orderId", "customerId", "tenantId", "agentId", "approved", "approvalId", "idempotencyKey", "version"];

// .NET 10's Strict preset rejects duplicate and unmapped properties,
// enforces required and nullable members, and refuses numbers in strings.
private static readonly JsonSerializerOptions SchemaOptions =
    new(JsonSerializerOptions.Strict)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
```

```csharp
// 3. Schema: host-owned fields are named explicitly, then the typed
//    contract rejects anything missing, mistyped, or unexpected.
foreach (JsonProperty property in arguments.EnumerateObject())
{
    if (HostOwnedFields.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
    {
        return ToolOutcome.Reject("schema", "argument.host-owned");
    }
}

RefundArguments? args;
try
{
    args = arguments.Deserialize<RefundArguments>(SchemaOptions);
}
catch (JsonException)
{
    return ToolOutcome.Reject("schema", "schema.invalid");
}

if (args is null)
{
    return ToolOutcome.Reject("schema", "schema.invalid");
}
```

What this step validates:

- **Required fields.** `[JsonRequired]` makes a missing `reason` a failure, not a default value.
- **No extra fields.** The `Strict` preset sets `UnmappedMemberHandling.Disallow`, so a model that adds `"note"` or `"priority"` is rejected rather than quietly ignored. That includes a nested object shaped like another tool call: the gateway never interprets structures inside arguments as operations.
- **Types, strictly.** The preset's strict number handling rejects `"amount": "25"`. A number that arrives as a string is a different input, not a formatting detail.
- **Exact property names.** Property matching is case-sensitive, so `Amount` is not `amount`.
- **Host-owned fields get their own reason.** `orderId` would already fail as an unmapped field. Naming it explicitly records `argument.host-owned`, which tells an operator that the model tried to supply identity, approval, a version, or an idempotency key rather than merely a stray field. This check deliberately ignores case, so `OrderID` and `TenantId` are caught too.

The `Strict` preset configures the serializer. It does not replace step 1: `JsonDocument` has its own `JsonDocumentOptions`, which is why the parser sets `AllowDuplicateProperties` separately.

Schema validation has limits worth seeing. The string-enum converter matches names case-insensitively, so `"damaged"` becomes `Damaged`. That is harmless here, because the meaning is identical. It also accepts integers by default, so `"reason": 7` deserializes into an undefined `RefundReason` and passes the schema. The next step has to catch that one. Every schema library has leniencies like these, which is one reason schema validity is necessary but never sufficient.

The refund tool has no free-text field, on purpose. If a tool needs prose, such as a note to the customer, treat it as content to store and escape when it is displayed. Never treat it as input that decides what happens.

## Step 4: Validate Semantics That Need No Host Data

Some arguments can be judged on their own. Check those next, before spending a database read:

```csharp
// 4. Semantics that need no host data.
if (args.Amount <= 0m || decimal.Round(args.Amount, 2) != args.Amount)
{
    return ToolOutcome.Reject("semantic", "amount.invalid");
}

if (args.Currency.Length != 3 || !args.Currency.All(char.IsAsciiLetterUpper))
{
    return ToolOutcome.Reject("semantic", "currency.invalid");
}

if (!Enum.IsDefined(args.Reason))
{
    return ToolOutcome.Reject("semantic", "reason.invalid");
}
```

What this step validates:

- **Meaningful values.** A refund of `-25` or `25.001` is well typed and meaningless.
- **Shape of codes.** A currency code is three uppercase letters. `"ZZZ"` passes this step. Whether a code is *this order's* currency is a question for the next step, where it fails.
- **Defined enum values.** This is where `"reason": 7` stops.

Keep this step free of host data. If a check needs the order, it belongs in step 5, where the source of truth is explicit.

## Step 5: Resolve Authoritative Facts from Host Context

Now the host brings in facts the model cannot supply. The conversation context comes from the authenticated session and the order the conversation was opened for:

```csharp
public readonly record struct OrderId(string Value);

public sealed record AuthenticatedAgent(
    string AgentId,
    string TenantId,
    bool CanIssueRefunds);

// Supplied by the host from the authenticated session and the order the
// support conversation was opened for. Never built from model output.
public sealed record ConversationContext(
    AuthenticatedAgent Agent,
    OrderId BoundOrder);

public sealed record OrderRecord(
    OrderId Id,
    string TenantId,
    string Currency,
    decimal RefundableAmount,
    bool OnFraudHold,
    bool HasUnsettledRefund,
    string Version);

public interface IOrderStore
{
    Task<OrderRecord?> FindAsync(OrderId id, CancellationToken cancellationToken);
}
```

```csharp
// 5. Authoritative context: the order comes from the conversation, not the model.
OrderRecord? order = await orders.FindAsync(context.BoundOrder, cancellationToken);

if (order is null ||
    !string.Equals(order.TenantId, context.Agent.TenantId, StringComparison.Ordinal))
{
    return ToolOutcome.Unavailable("context", "order.unavailable");
}

// An earlier refund whose provider outcome is not yet settled blocks new ones
// until reconciliation finishes, so a lost response cannot become a second refund.
if (order.HasUnsettledRefund)
{
    return ToolOutcome.Unavailable("context", "refund.in-progress");
}

if (!string.Equals(order.Currency, args.Currency, StringComparison.Ordinal))
{
    return ToolOutcome.Reject("context", "currency.mismatch");
}

if (args.Amount > order.RefundableAmount)
{
    return ToolOutcome.Reject("context", "amount.exceeds-refundable");
}
```

What this step validates:

- **Resource identity comes from the host.** The order is `context.BoundOrder`, never a value from the arguments. Step 3 already refused a model-supplied `orderId`. This step is where that refusal pays off: there is no code path that could have used it.
- **Visibility without leaking existence.** An order in another tenant and an order that does not exist return the same `order.unavailable`. The model never names an order at all, so it cannot probe for other order IDs either.
- **No new refund while an earlier one is unsettled.** If an earlier refund was reserved but the provider's answer was lost, a fresh attempt must not start a second one. Step 8 explains how that state arises and how it clears.
- **The proposal fits current facts.** A refund in the wrong currency, or above the refundable balance, is rejected here. These are semantic checks that need trusted data, so they live after it is loaded.
- **Freshness is captured.** `order.Version` records exactly which state these checks saw. Step 8 uses it.

If other tools also act on orders, such as `orders.cancel` or `orders.reship`, each handler resolves and checks the facts it depends on. Do not rely on another tool's earlier check, or on a version it saw. Each call is judged against current facts.

## Step 6: Evaluate Authorization and Operational Policy

A well-formed proposal that fits the facts can still be forbidden:

```csharp
// 6. Authorization and operational policy.
if (!context.Agent.CanIssueRefunds)
{
    return ToolOutcome.Deny("policy", "agent.not-authorized");
}

if (order.OnFraudHold)
{
    return ToolOutcome.Deny("policy", "order.fraud-hold");
}
```

What this step validates:

- **Caller authorization.** The authenticated agent must hold the refund permission. The model's confidence plays no part.
- **Operational policy.** A fraud hold blocks refunds regardless of who asks. Real systems add refund windows, daily limits, or regional rules here.
- **Denial is a decision, not an error.** It is recorded as `Denied` with a reason, so an operator can distinguish "the assistant asked for something malformed" from "the assistant asked for something the business does not allow".

In an ASP.NET Core application, this step is often an `IAuthorizationService` call with a resource-based requirement. That is fine. What matters is that it runs against the authoritative order from step 5, not against the arguments.

## Step 7: Require Human Approval When the Operation Needs It

Some operations are allowed, but not without an authorized person deciding first. Here, refunds above $250 need a supervisor's approval.

This is approval by another authority, not acknowledgment. Acknowledgment records that someone was shown a condition and accepted it, such as an agent confirming "this refund cannot be reversed". Approval is a separate decision by someone with the authority to make it. A system can need either one, both, or neither. [Terminology and Established Concepts](../../architecture/terminology-and-established-concepts.md#approval-acknowledgment-authorization-and-authority) explains why they should not be collapsed.

Approval has to be something the host holds, bound to exactly one refund. When the first attempt returns `PendingApproval`, the host's approval workflow records the proposed refund and asks a supervisor. If the supervisor approves, the workflow stores an approval:

```csharp
// Created by the host's approval workflow when a supervisor approves a
// specific pending refund. The model never sees or supplies it.
public sealed record RefundApproval(
    string ApprovalId,
    OrderId OrderId,
    decimal Amount,
    string Currency,
    RefundReason Reason,
    string RequestedBy,
    string ApprovedBy,
    DateTimeOffset ExpiresAt);

public interface IRefundApprovals
{
    // Looks up by the full identity of one refund, not just the order.
    // The handler still re-checks every field it relies on.
    Task<RefundApproval?> FindUnusedAsync(
        OrderId orderId,
        decimal amount,
        string currency,
        RefundReason reason,
        string requestedBy,
        CancellationToken cancellationToken);
}
```

The handler then accepts a large refund only if an unused approval covers exactly this one:

```csharp
// 7. Human approval: large refunds need a host-held approval for exactly this refund.
string? approvalId = null;

if (args.Amount > ApprovalThreshold)
{
    RefundApproval? approval = await approvals.FindUnusedAsync(
        order.Id, args.Amount, args.Currency, args.Reason, context.Agent.AgentId, cancellationToken);

    if (approval is null ||
        approval.Amount != args.Amount ||
        !string.Equals(approval.Currency, args.Currency, StringComparison.Ordinal) ||
        approval.Reason != args.Reason ||
        !string.Equals(approval.RequestedBy, context.Agent.AgentId, StringComparison.Ordinal) ||
        string.Equals(approval.ApprovedBy, context.Agent.AgentId, StringComparison.Ordinal) ||
        approval.ExpiresAt <= clock.GetUtcNow())
    {
        return new ToolOutcome(
            ToolOutcomeKind.PendingApproval, "approval", "refund.approval-required");
    }

    approvalId = approval.ApprovalId;
}
```

What this step validates:

- **Pending is not executed.** Without a covering approval, the handler stops before the executor. Nothing is written, reserved, or partially applied while approval is outstanding.
- **Approval comes from outside the proposal.** It lives in host storage, not in the model-visible arguments. A model that writes `"approved": true` or `"approvalId": "..."` is rejected at step 3, and nothing here reads the arguments for consent.
- **Approval covers one exact refund.** A supervisor who approved $280 has not approved $300. The store looks up by the refund's full identity, so an unused $280 approval on the same order cannot hide the $300 one. The handler still re-checks every field it relies on, so a store that returns the wrong row cannot widen an approval.
- **Approval comes from someone else.** The agent who asked cannot approve their own request.
- **Every approval expires, soon, on the host's terms.** The approval workflow sets a short expiry; nothing in the proposal can extend it. An approval with no expiry, or a very long one, is permanent authority waiting to be used. If the approval workflow and the gateway run on different machines, compare expiry against one trusted clock, or set expiries with enough margin that small clock differences cannot extend them.
- **Approval belongs to one tool.** A `RefundApproval` authorizes one refund. Nothing about it can satisfy an approval requirement in `orders.cancel` or any other operation.
- **Continuation is a new attempt.** Because steps 5 and 6 run again before this check, an approved refund still fails if the order was refunded or placed on hold while it waited.

The approval ID travels to the executor. In the same local transaction that reserves the refund, the executor checks again that the approval is still unused and unexpired, then marks it used. The handler's check alone cannot cover the moment between steps 7 and 8, when the approval could expire or be used by a concurrent attempt. The transaction makes the approval single-use: a second attempt finds no unused approval and returns to pending.

This design keeps approval inside one host. If approved work crosses a process or service boundary, or runs later with less authority than the requester, the approval should travel as a signed, expiring grant bound to the same details. That is the point where capability-style tokens start to earn their place. For a supervisor approving in the same application, a database row is enough.

## Step 8: Invoke the Host-Owned Executor

Only now does the protected side effect become reachable. The handler builds one command from host facts and validated arguments:

```csharp
public enum RefundWriteResult
{
    Issued,
    VersionConflict,
    OutcomeUnknown
}

public sealed record RefundCommand(
    OrderId OrderId,
    string ExpectedVersion,
    decimal Amount,
    string Currency,
    RefundReason Reason,
    string AgentId,
    string IdempotencyKey,
    string? ApprovalId);

public interface IRefundExecutor
{
    Task<RefundWriteResult> IssueAsync(RefundCommand command, CancellationToken cancellationToken);
}
```

```csharp
// 8. Execute through the host-owned executor, conditional on the
//    order version that every earlier check was made against.
var command = new RefundCommand(
    order.Id,
    order.Version,
    args.Amount,
    order.Currency,
    args.Reason,
    context.Agent.AgentId,
    IdempotencyKeyFor(order, args),
    approvalId);

RefundWriteResult write = await executor.IssueAsync(command, cancellationToken);

return write switch
{
    RefundWriteResult.Issued =>
        new ToolOutcome(ToolOutcomeKind.Executed, "execute", "refund.issued"),
    RefundWriteResult.VersionConflict =>
        ToolOutcome.Unavailable("execute", "order.changed"),
    _ =>
        ToolOutcome.Unavailable("execute", "refund.outcome-unknown")
};
```

The idempotency key is derived by the host, never proposed by the model:

```csharp
// Identifies one refund decision: the same tenant, order, version, amount,
// currency, and reason always produce the same key. The executor stores it with
// the reservation and reuses it for every provider retry of that reservation.
private static string IdempotencyKeyFor(OrderRecord order, RefundArguments args)
{
    string material = string.Join(
        '\n',
        order.TenantId,
        order.Id.Value,
        order.Version,
        args.Amount.ToString("0.00", CultureInfo.InvariantCulture),
        order.Currency,
        args.Reason.ToString());

    return "refund-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
}
```

What this step validates:

- **Every value is host-sourced or already validated.** The order, version, currency, agent, key, and approval come from the host. The model contributes only a validated amount and reason.
- **The local write is conditional, and it is recorded before anything leaves the host.** Inside the executor, one local transaction checks that the order is still at `ExpectedVersion`. It then records a refund operation with its idempotency key, reserves the amount against the balance, marks the order as having an unsettled refund, and rechecks and consumes the approval, all atomically. If another agent refunded the order after step 5, the version no longer matches and the result is `VersionConflict`.
- **The provider call reuses the stored key.** After the reservation commits, the executor calls the payment provider with the key it stored. If the network drops, the executor's own retries of that call send the same key, and the provider does not refund twice.
- **Uncertainty is reported as uncertainty.** A remote call can time out after the provider has already acted. The executor cannot know which, so it returns `OutcomeUnknown`, which the handler reports as `Unavailable` with `refund.outcome-unknown`, never as `Executed`.
- **A new attempt cannot start a second refund.** The reservation changed the order's version, so a fresh proposal would derive a new key. That is why step 5 refuses new refunds (`refund.in-progress`) while the order has an unsettled refund.
- **The flag clears as soon as the outcome is known.** When the provider confirms the refund, the executor records the operation as issued and clears the flag in the same step, so a successful refund does not block the next one. Only an unknown outcome leaves the flag set. A reconciliation job then asks the provider about the stored key, settles the operation as issued or not issued, and clears the flag.
- **An unresolved operation stays blocked.** Providers keep idempotency keys for a limited time, so reconciliation should finish well within that window. If it cannot, because the provider has no record of the key or the window has passed, the order stays blocked and the operation goes to a person to investigate. Retrying with a new key could refund twice. The key alone cannot protect retries indefinitely.
- **Credentials stay here.** The payment-provider credential lives inside the executor's implementation. It is not in the context, the arguments, or anything the model can see.

The key's ingredients are deliberate. The version makes it identify one decision: a refund decided against `v12` is a different operation from the same amount decided after the order changed. The tenant keeps keys from colliding if several tenants share one provider account. Joining the parts with a newline, and formatting the amount with two fixed decimals, keeps two different inputs from producing the same material. The hash is a fingerprint, not a signature: it is safe only because the host derives it and the model never supplies one.

The version check and the idempotency key protect against different things. The version check stops a stale decision: once the order changes, an old proposal cannot be replayed against it. The stored key stops a duplicate side effect: the same reservation retried cannot refund twice. The unsettled-refund check stops a new decision from racing an old one whose outcome is unknown. None of these makes the operation exactly-once on its own. Together with reconciliation, they make it safe to retry.

## Step 9: Record the Outcome

The gateway records the outcome it returns, including rejections and host failures, in one place:

```csharp
public sealed record ToolDecisionRecord(
    string ToolName,
    string? OrderId,
    ToolOutcomeKind Outcome,
    string Stage,
    string ReasonCode);

public interface IToolDecisionLog
{
    void Record(ToolDecisionRecord record);
}
```

What this step must do, and must not do:

- **Record the decision, not the conversation.** Tool, host-resolved order, outcome, stage, and reason code are enough to explain what happened. Raw prompts, model text, full order records, and payment details stay out.
- **Record rejections too.** A spike in `argument.host-owned` is a signal that someone is steering the assistant toward other customers' orders.
- **Keep the record append-only, and never read it as authority.** Nothing consults this log to decide whether to execute. A later process must not treat "a log line exists" as proof that a refund was approved.
- **Treat a failed log write as an operational problem.** If the decision store is down, alert on it. A failed write cannot undo a refund, and it must not turn a rejection into an execution. If your audit requirements need the record and the refund to succeed or fail together, write the record inside the executor's local transaction instead.

What the model sees next is a separate question. Internal reason codes such as `order.fraud-hold` can tell a manipulated model which lever to try next. Send it a smaller, safer projection, such as "invalid amount" for something it can correct and "unavailable" for everything else. [Why an AI Tool Call Is a Proposal, Not Authority](why-ai-tool-call-is-only-a-proposal.md) shows that separation in detail.

## Prove That Rejected Proposals Never Execute

The architectural promise of this sequence is simple: **every path blocked before execution makes zero executor calls.** Only outcomes the executor itself reports, such as a version conflict or an unknown provider result, may involve a call, and those must never be reported as executed. The code in this article is an excerpt from a small test project; all of it was compiled and run with .NET 10 and xUnit. A recording executor makes the promise observable:

```csharp
public sealed class RecordingRefundExecutor(
    RefundWriteResult result = RefundWriteResult.Issued) : IRefundExecutor
{
    private readonly List<RefundCommand> commands = [];

    public int Calls => commands.Count;

    public IReadOnlyList<RefundCommand> Commands => commands;

    public Task<RefundWriteResult> IssueAsync(RefundCommand command, CancellationToken cancellationToken)
    {
        lock (commands)
        {
            commands.Add(command);
        }

        return Task.FromResult(result);
    }
}
```

The test helper `Create` builds the gateway around one order, with the recording executor, in-memory order and approval stores, a fixed clock, and a list-backed decision log. The fixture order has a $400 refundable balance, and the conversation is bound to it for an agent who may issue refunds:

```csharp
private static readonly ConversationContext Conversation = new(
    new AuthenticatedAgent("agent-7", "tenant-a", CanIssueRefunds: true),
    new OrderId("order-1001"));

private const string ValidRefund =
    """{"name":"orders.refund","arguments":{"amount":25.50,"currency":"USD","reason":"Damaged"}}""";

private const string LargeRefund =
    """{"name":"orders.refund","arguments":{"amount":300,"currency":"USD","reason":"Damaged"}}""";

private static OrderRecord Order(
    bool fraudHold = false,
    string tenantId = "tenant-a",
    bool unsettledRefund = false) => new(
    new OrderId("order-1001"),
    TenantId: tenantId,
    Currency: "USD",
    RefundableAmount: 400m,
    OnFraudHold: fraudHold,
    HasUnsettledRefund: unsettledRefund,
    Version: "v12");
```

`Approval(...)` builds a supervisor's approval for the $300 `LargeRefund`, valid for an hour after the fixed clock's `Now`:

```csharp
private static RefundApproval Approval(
    decimal amount = 300m,
    string approvedBy = "supervisor-2",
    DateTimeOffset? expiresAt = null) => new(
    "approval-55",
    new OrderId("order-1001"),
    amount,
    "USD",
    RefundReason.Damaged,
    RequestedBy: "agent-7",
    ApprovedBy: approvedBy,
    ExpiresAt: expiresAt ?? Now.AddHours(1));
```

One theory covers the rejected and pending paths, using raw payloads exactly as a model would produce them. No live model is involved. These are some of its rows:

```csharp
public static TheoryData<string, string, ToolOutcomeKind, string> BlockedProposals => new()
{
    { "malformed JSON", """{"name":"orders.refund","arguments":{""", ToolOutcomeKind.Rejected, "parse.malformed" },
    { "extra envelope field", """{"name":"orders.refund","arguments":{"amount":25,"currency":"USD","reason":"Damaged"},"approved":true}""", ToolOutcomeKind.Rejected, "parse.envelope-invalid" },
    { "duplicate property", """{"name":"orders.refund","arguments":{"amount":25,"amount":2500,"currency":"USD","reason":"Damaged"}}""", ToolOutcomeKind.Rejected, "parse.malformed" },
    { "unknown tool", """{"name":"orders.cancel","arguments":{}}""", ToolOutcomeKind.Rejected, "tool.unknown" },
    { "amount as string", """{"name":"orders.refund","arguments":{"amount":"25","currency":"USD","reason":"Damaged"}}""", ToolOutcomeKind.Rejected, "schema.invalid" },
    { "model-supplied order", """{"name":"orders.refund","arguments":{"orderId":"order-2002","amount":25,"currency":"USD","reason":"Damaged"}}""", ToolOutcomeKind.Rejected, "argument.host-owned" },
    { "host field casing", """{"name":"orders.refund","arguments":{"OrderID":"order-2002","amount":25,"currency":"USD","reason":"Damaged"}}""", ToolOutcomeKind.Rejected, "argument.host-owned" },
    { "numeric enum", """{"name":"orders.refund","arguments":{"amount":25,"currency":"USD","reason":7}}""", ToolOutcomeKind.Rejected, "reason.invalid" },
    { "well-formed unknown currency", """{"name":"orders.refund","arguments":{"amount":25,"currency":"ZZZ","reason":"Damaged"}}""", ToolOutcomeKind.Rejected, "currency.mismatch" },
    { "above refundable", """{"name":"orders.refund","arguments":{"amount":900,"currency":"USD","reason":"Damaged"}}""", ToolOutcomeKind.Rejected, "amount.exceeds-refundable" },
    { "needs approval", LargeRefund, ToolOutcomeKind.PendingApproval, "refund.approval-required" },
};

[Theory]
[MemberData(nameof(BlockedProposals))]
public async Task Blocked_proposal_never_reaches_the_executor(
    string scenario, string rawPayload, ToolOutcomeKind expectedKind, string expectedReason)
{
    var (gateway, executor, log) = Create(Order());

    ToolOutcome outcome = await gateway.HandleAsync(rawPayload, Conversation, CancellationToken.None);

    Assert.True(expectedKind == outcome.Kind, scenario);
    Assert.Equal(expectedReason, outcome.ReasonCode);
    Assert.Equal(0, executor.Calls);
    Assert.Single(log.Records);
}
```

The full theory also covers an oversized payload, excessive depth, a wrong-case tool name, a missing field, an unexpected field, a model-supplied `"approved": true`, negative and sub-cent amounts, and the wrong currency. Every row asserts zero executor calls.

Authorization denial needs different host facts rather than a different payload, so it gets its own test. It proves that `Denied` is distinct from `Rejected`:

```csharp
[Fact]
public async Task Agent_without_refund_permission_is_denied_without_execution()
{
    var (gateway, executor, _) = Create(Order());
    ConversationContext readOnlyAgent = Conversation with
    {
        Agent = Conversation.Agent with { CanIssueRefunds = false }
    };

    ToolOutcome outcome = await gateway.HandleAsync(ValidRefund, readOnlyAgent, CancellationToken.None);

    Assert.Equal(ToolOutcomeKind.Denied, outcome.Kind);
    Assert.Equal("agent.not-authorized", outcome.ReasonCode);
    Assert.Equal(0, executor.Calls);
}
```

The approval path is tested in both directions. A covering approval lets the refund execute exactly once and carries the approval to the executor. Approvals that do not cover this refund leave it pending:

```csharp
[Fact]
public async Task Approved_large_refund_executes_once_with_its_approval()
{
    var (gateway, executor, _) = Create(Order(), [Approval()]);

    ToolOutcome outcome = await gateway.HandleAsync(LargeRefund, Conversation, CancellationToken.None);

    Assert.Equal(ToolOutcomeKind.Executed, outcome.Kind);
    Assert.Equal("approval-55", Assert.Single(executor.Commands).ApprovalId);
}

public static TheoryData<string, RefundApproval> UnusableApprovals => new()
{
    { "different amount", Approval(amount: 280m) },
    { "expired", Approval(expiresAt: Now.AddMinutes(-1)) },
    { "self-approved", Approval(approvedBy: "agent-7") },
};

[Theory]
[MemberData(nameof(UnusableApprovals))]
public async Task Approval_that_does_not_cover_this_refund_leaves_it_pending(
    string scenario, RefundApproval approval)
{
    var (gateway, executor, _) = Create(Order(), [approval]);

    ToolOutcome outcome = await gateway.HandleAsync(LargeRefund, Conversation, CancellationToken.None);

    Assert.True(outcome.Kind == ToolOutcomeKind.PendingApproval, scenario);
    Assert.Equal(0, executor.Calls);
}
```

An earlier refund with an unknown outcome must block new ones before they reach the executor:

```csharp
[Fact]
public async Task Unsettled_earlier_refund_blocks_a_new_one_without_execution()
{
    var (gateway, executor, _) = Create(Order(unsettledRefund: true));

    ToolOutcome outcome = await gateway.HandleAsync(ValidRefund, Conversation, CancellationToken.None);

    Assert.Equal(ToolOutcomeKind.Unavailable, outcome.Kind);
    Assert.Equal("refund.in-progress", outcome.ReasonCode);
    Assert.Equal(0, executor.Calls);
}
```

The executor is the one place a blocked outcome may still arrive, because only the executor can see the final state. Those outcomes must never be reported as executed:

```csharp
[Theory]
[InlineData(RefundWriteResult.VersionConflict, "order.changed")]
[InlineData(RefundWriteResult.OutcomeUnknown, "refund.outcome-unknown")]
public async Task Executor_non_success_is_never_reported_as_executed(
    RefundWriteResult writeResult, string expectedReason)
{
    var (gateway, executor, _) = Create(Order(), writeResult: writeResult);

    ToolOutcome outcome = await gateway.HandleAsync(ValidRefund, Conversation, CancellationToken.None);

    Assert.Equal(ToolOutcomeKind.Unavailable, outcome.Kind);
    Assert.Equal(expectedReason, outcome.ReasonCode);
    Assert.Equal(1, executor.Calls);
}
```

And the happy path proves the sequence is not simply blocking everything, and that the executor receives host-sourced values rather than model-supplied ones:

```csharp
[Fact]
public async Task Valid_proposal_executes_once_with_host_sourced_values()
{
    var (gateway, executor, log) = Create(Order());

    ToolOutcome outcome = await gateway.HandleAsync(ValidRefund, Conversation, CancellationToken.None);

    Assert.Equal(ToolOutcomeKind.Executed, outcome.Kind);
    RefundCommand command = Assert.Single(executor.Commands);
    Assert.Equal(new OrderId("order-1001"), command.OrderId);
    Assert.Equal("v12", command.ExpectedVersion);
    Assert.Equal(25.50m, command.Amount);
    Assert.Equal("USD", command.Currency);
    Assert.Equal("agent-7", command.AgentId);
    Assert.Null(command.ApprovalId);
    Assert.Equal("refund.issued", Assert.Single(log.Records).ReasonCode);
}
```

The same suite also shows that:

- a fraud hold is denied;
- an order in another tenant looks the same as a missing one;
- a failing order store is recorded as `host.error` without execution;
- with unused $280 and $300 approvals on one order, a $300 refund finds the $300 approval;
- an identical proposal against an unchanged order derives the same idempotency key.

`Assert.Equal(0, executor.Calls)` is the assertion that matters. A result enum proves what the gateway *reported*. The call count proves what it *did*. A gateway that returns `Rejected` after already calling the payment provider would pass a result-only test and fail this one.

## Schema Validity Is Necessary, but It Does Not Create Authority

It is tempting to read a strict schema as the security boundary. The walkthrough shows why it is not:

- A payload with `"amount": 900` passed the schema and the semantic checks, and would have passed a naive handler. Only the authoritative refundable balance stopped it.
- A payload with a valid amount passed everything up to policy. Only the fraud hold stopped it.
- A payload asking for $300 passed policy. Only a missing approval stopped it.
- `"reason": 7` passed the schema. Only semantic validation stopped it.

Schema validation decides whether a proposal is acceptable *input*. Authority comes from host facts, authorization, policy, and, where required, a person. Structured output from a provider that guarantees schema conformance mostly removes step 3 failures from normal traffic. It does not remove steps 4 to 8.

## When Simpler Is Enough

None of this requires a separate gateway service, a policy engine, or capability tokens. For many applications, the sequence already exists once you look for it:

```text
Framework registers only orders.refund for this assistant
      ↓
Typed tool parameters with strict deserialization
      ↓
Handler reads the order from the session, not from arguments
      ↓
Ordinary ASP.NET Core resource-based authorization
      ↓
Same-process call to the refund service with a version check
```

That is sufficient when:

- nothing else can reach the refund executor without the same checks, whether another endpoint, a background job, or a second agent;
- the agent runtime is trusted, and the framework's tool registration is the allowlist, so nothing dispatches by arbitrary name;
- identity and resource facts come from the authenticated session and the system of record;
- authorization runs inside the handler, before the side effect;
- execution happens immediately, in the same process, with the application's own credentials;
- no approval step, delayed worker, or cross-service handoff is involved.

The first condition matters most. If a background job or a second endpoint can call the refund executor directly, the gateway is theater, however careful its checks are.

Add more structure only when the lifecycle outgrows that. Examples are approvals that pause execution, work that runs later or elsewhere, execution with less authority than the requester, or evidence that must connect proposal, decision, and execution durably. The validation steps do not change when you add that structure. They just gain more places they must be enforced.

## The Checklist

Before a model-generated tool call reaches a consequential handler, confirm that:

1. **Parsing is bounded and strict.** Size and depth are limited, duplicate properties are rejected, the envelope shape is exact, and failures become results rather than exceptions.
2. **Tools resolve through a host-owned registry** with exact name comparison and no fallback, reflection, or alternate unchecked path. Each call in a batch is judged on its own.
3. **The argument schema is explicit.** Fields are required, unknown fields are rejected, types are strict, and host-owned fields such as identity, tenant, or approval are refused by name, whatever their casing.
4. **Context-free semantics are checked** before any host data is loaded, including values the schema library accepted leniently.
5. **Authoritative facts come from the host.** The resource comes from session or route context, visibility is checked without revealing existence, proposal values are compared with current facts, the version that was checked is captured, and no new side effect starts while an earlier one is unsettled.
6. **Authorization and operational policy** run against those facts, and denial is recorded separately from rejection.
7. **Human approval is host-held and exact.** It is looked up by and covers one specific operation, comes from someone other than the requester, always expires, is used once, belongs to one tool, and never comes from the arguments.
8. **The executor is the only path to the side effect.** It holds the credentials, records the operation and its host-derived idempotency key before calling out, writes conditionally, reuses the stored key on retry, reports an unknown outcome as unknown, and leaves reconciliation to settle it within the provider's key-retention window.
9. **Every outcome is recorded** as an append-only decision, without secrets or raw model text, and the record is never read as permission. What the model is told is a separate, smaller projection.
10. **Tests assert zero executor calls on every path blocked before execution**, not just the returned result, and assert that executor-reported failures are never reported as executed.

## How This Maps to the Learning Vocabulary

If you are following the Learning tutorials, this article uses plainer names for the same boundaries:

| This article | Learning term |
| --- | --- |
| Tool call, parsed and schema-checked (steps 1 to 4) | Intent (typed proposed intent) |
| Host-bound order, agent, and current facts (step 5) | Context |
| Authorization and fraud-hold policy (step 6) | Constraints and decision |
| Host-held supervisor approval (step 7) | Approval by another authority (distinct from acknowledgment) |
| Refund command with version, key, and approval (step 8) | Scoped authority and host-owned execution |
| Decision record (step 9) | Evidence |

## Continue Deeper

- [Authorization vs. Approval vs. Acknowledgment: Which Decision Do You Actually Have?](authorization-vs-approval-vs-acknowledgment.md) is the same distinction outside AI: authorization, approval, and acknowledgment for a delayed data export, each kept separate from permission to execute.
- [Why an AI Tool Call Is a Proposal, Not Authority](why-ai-tool-call-is-only-a-proposal.md) makes the argument this checklist implements, and shows how to keep internal reason codes out of what the model sees.
- [Governed AI Tool Gateway](../../tutorials/governed-ai-tool-gateway.md) and its [runnable sample](https://github.com/AsiBackbone/Learning/blob/main/samples/governed-ai-tool-gateway/README.md) extend steps 6 to 9 into a full lifecycle with decision receipts, acknowledgment, and scoped execution authority, still without a live model.

Already following the Learning path? [Typed AI Proposed Intent and Schema-Validation Boundaries](../../ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md) goes deeper on steps 1 to 4, and [Trust Boundaries and Least Privilege](../../security/trust-boundaries-and-least-privilege.md) on where the host boundary sits.

---

> **Read it. Run it. Question it. Improve it.**
