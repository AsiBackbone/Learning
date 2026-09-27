---
description: A host-side checklist for AI tool calls, covering parsing, allowlisting, schema, semantics, host context, policy, approval, execution, and evidence.
title: What Should an AI Tool Gateway Validate Before Execution?
author: Christopher D. Cavell
published: "2026-09-27"
summary: Structured model output becomes eligible for execution only after the trusted host independently validates its shape, meaning, context, authority, and current permission. This is the ordered checklist, with code and tests that prove rejected proposals never reach the executor.
feed: true
---

# What Should an AI Tool Gateway Validate Before Execution?

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** Familiarity with C# and `System.Text.Json` is helpful. No AI provider, agent framework, AsiBackbone package, or prior Learning material is required. The code targets .NET 10. On older versions, replace two options: `AllowDuplicateProperties` (step 1) is new in .NET 10, and `RespectNullableAnnotations` (step 3) is new in .NET 9.

**What this article covers:** an ordered, host-owned acceptance sequence for model-generated tool calls, from raw JSON to the protected side effect; what each step validates and what failure looks like; which values the model may supply and which the host must own; tests that prove every rejected path makes zero executor calls; and when ordinary framework controls are enough.

Your application already accepts tool calls from a model. The model returns something like this, and your code is expected to act on it:

```json
{
  "name": "orders.refund",
  "arguments": { "amount": 25.50, "currency": "USD", "reason": "Damaged" }
}
```

The payload is valid JSON, and it names a real tool. The tempting next line of code deserializes the arguments and calls the refund service.

That line skips most of the decisions that matter. Valid JSON says nothing about whether this order may be refunded, whether $25.50 is refundable, whether the agent may issue refunds, or whether the order is on a fraud hold. If the model had also written `"orderId": "order-2002"`, a naive handler might have refunded a different customer.

[Why an AI Tool Call Is a Proposal, Not Authority](why-ai-tool-call-is-only-a-proposal.md) makes the case that a tool call is a request for the host to consider, not permission to act. This article starts one step later, when the structured output has arrived. It answers the practical question: **what, exactly, should the host check, in what order, before the handler runs?**

> Structured model output becomes eligible for execution only after the trusted host independently validates its shape, meaning, context, authority, and current permission.

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
| Payment credentials | Host-owned executor only | The model never sees or selects a credential. |

The model gets three fields. Everything that decides *whether* a refund may happen stays with the host.

## The Acceptance Sequence at a Glance

```text
Raw model output
      ↓
1. Parse structurally
      ↓
2. Resolve the tool through a host-owned allowlist
      ↓
3. Validate the argument schema
      ↓
4. Validate semantics that need no host data
      ↓
5. Resolve authoritative facts from host context
      ↓
6. Evaluate authorization and operational policy
      ↓
7. Require acknowledgment when the operation needs it
      ↓
8. Invoke the host-owned executor
      ↓
9. Record the outcome
```

Each step answers a different question and fails differently:

| Step | Question | On failure | Executor reached? |
| --- | --- | --- | --- |
| 1. Parse | Is this a well-formed tool-call envelope of reasonable size? | Rejected | No |
| 2. Allowlist | Is this a tool the host exposes on this path? | Rejected | No |
| 3. Schema | Are the arguments exactly the fields and types the host defined? | Rejected | No |
| 4. Semantics | Do the arguments make sense on their own? | Rejected | No |
| 5. Host context | Do the arguments fit the current, authoritative facts? | Rejected or unavailable | No |
| 6. Authorization and policy | May this agent do this to this order now? | Denied | No |
| 7. Acknowledgment | Does a person need to approve this first? | Pending | No |
| 8. Execute | Did the write succeed against the facts that were checked? | Unavailable | Yes, once |
| 9. Record | What happened, and why? | (always runs) | n/a |

The order matters. Cheap, context-free checks run first, so obviously bad proposals never cost a database read. Checks that need trusted data run only after the proposal is known to be well formed. The protected side effect is the last thing that can happen, not something that has to be undone.

Steps 1, 2, and 9 are the same for every tool. Steps 3 to 8 belong to the tool. The code mirrors that split.

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
- **Duplicate properties.** `{"amount": 25, "amount": 2500}` is valid JSON to many parsers, which silently keep one value. A reviewer reading the payload and the handler that acts on it can see different numbers. Reject the ambiguity while the raw document still shows it. On .NET versions before 10, `AllowDuplicateProperties` is not available, so check for repeated names while enumerating the raw document instead.
- **The envelope's exact shape.** One tool name, one arguments object, nothing else. A provider-specific envelope is normalized to this shape in an adapter before it reaches the gateway.
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
            outcome = await handler.HandleAsync(call.Arguments, context, cancellationToken);
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

The unknown name is not written to the record verbatim. Model-chosen text is untrusted, and a log is a sink like any other.

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

`Rejected` means the proposal was not acceptable input. `Denied` means an acceptable proposal is not allowed. Keeping them apart stops a malformed proposal from being treated as a policy question, and gives operators two different things to investigate.

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

The refund handler owns steps 3 to 8. It receives the order store and the executor from the host, names the fields the model must never supply, and deserializes with options that fail closed:

```csharp
public sealed class RefundToolHandler(
    IOrderStore orders,
    IRefundExecutor executor) : IToolHandler
{
    private const decimal ApprovalThreshold = 250m;

    public string Name => "orders.refund";

    // HandleAsync runs the step 3 to 8 fragments below, in order.
}
```

```csharp
private static readonly string[] HostOwnedFields =
    ["orderId", "customerId", "tenantId", "agentId", "approved"];

private static readonly JsonSerializerOptions SchemaOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    NumberHandling = JsonNumberHandling.Strict
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
- **No extra fields.** `JsonUnmappedMemberHandling.Disallow` rejects anything the contract does not define. A model that adds `"note"` or `"priority"` is not quietly ignored.
- **Host-owned fields get their own reason.** `orderId` would already fail as an unmapped field. Naming it explicitly records `argument.host-owned`, which tells an operator that the model tried to supply identity rather than merely a stray field. The check ignores case, so `OrderID` is caught too.
- **Types, strictly.** `JsonNumberHandling.Strict` rejects `"amount": "25"`. A number that arrives as a string is a different input, not a formatting detail.
- **Exact property names.** `PropertyNameCaseInsensitive = false` means `Amount` is not `amount`.

Schema validation has limits worth seeing. `JsonStringEnumConverter` accepts integers by default, so `"reason": 7` deserializes into an undefined `RefundReason`. The payload passes the schema, and the next step has to catch it. Schema libraries each have their own leniencies. That is one reason schema validity is necessary but never sufficient.

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
- **Shape of codes.** A currency code is three uppercase letters. Whether it is *this order's* currency is a question for the next step.
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
- **The resource is visible to this actor.** An order in another tenant and an order that does not exist return the same `order.unavailable`. The caller learns nothing about which orders exist elsewhere.
- **The proposal fits current facts.** A refund in the wrong currency, or above the refundable balance, is rejected here. These are semantic checks that need trusted data, so they live after it is loaded.
- **Freshness is captured.** `order.Version` records exactly which state these checks saw. Step 8 uses it.

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

## Step 7: Require Acknowledgment When the Operation Needs It

Some operations are allowed, but not without a person agreeing first. Here, refunds above $250 need a supervisor:

```csharp
// 7. Acknowledgment: large refunds wait for a person.
if (args.Amount > ApprovalThreshold)
{
    return new ToolOutcome(
        ToolOutcomeKind.PendingApproval, "acknowledgment", "refund.approval-required");
}
```

What this step validates:

- **Pending is not executed.** The handler stops before the executor. Nothing is written, reserved, or partially applied while approval is outstanding.
- **Approval comes from outside the proposal.** The model cannot satisfy this step by writing `"approved": true`. Step 3 rejects that field, and nothing here reads it.

When the supervisor approves, treat the continuation as a new attempt: resolve the order again, re-run steps 5 to 7 against current facts, and execute only if they still pass. The order may have been refunded, or placed on hold, while the request waited.

This is also where more infrastructure can start to earn its place. If the approved work runs later, in another process, or with less authority than the requester, a scoped, expiring grant for exactly this refund is useful. If the supervisor approves in the same application and the handler simply runs again, it is not needed.

## Step 8: Invoke the Host-Owned Executor

Only now does the protected side effect become reachable:

```csharp
public enum RefundWriteResult
{
    Issued,
    VersionConflict
}

public interface IRefundExecutor
{
    Task<RefundWriteResult> IssueAsync(
        OrderId orderId,
        string expectedVersion,
        decimal amount,
        string currency,
        RefundReason reason,
        string agentId,
        CancellationToken cancellationToken);
}
```

```csharp
// 8. Execute through the host-owned executor, conditional on the
//    order version that every earlier check was made against.
RefundWriteResult write = await executor.IssueAsync(
    order.Id,
    order.Version,
    args.Amount,
    order.Currency,
    args.Reason,
    context.Agent.AgentId,
    cancellationToken);

return write == RefundWriteResult.Issued
    ? new ToolOutcome(ToolOutcomeKind.Executed, "execute", "refund.issued")
    : ToolOutcome.Unavailable("execute", "order.changed");
```

What this step validates:

- **Every argument is host-sourced or already validated.** The order ID, currency, and agent come from host facts. The model contributes only a validated amount and reason.
- **The write is conditional.** `expectedVersion` makes the refund fail if the order changed after step 5, for example if another agent refunded it a moment earlier. A version conflict reports `Unavailable`, never `Executed`.
- **Credentials stay here.** The payment-provider credential lives inside the executor's implementation. It is not in the context, the arguments, or anything the model can see.

## Step 9: Record the Outcome

The gateway records every outcome, including rejections, in one place:

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

What this step validates, or rather what it must not do:

- **Record the decision, not the conversation.** Tool, host-resolved order, outcome, stage, and reason code are enough to explain what happened. Raw prompts, model text, and payment details stay out.
- **Record rejections too.** A spike in `argument.host-owned` is a signal that someone is steering the assistant toward other customers' orders.
- **The record is evidence, not authority.** Nothing reads this log to decide whether to execute. If the log write fails, the refund's legitimacy does not change, and a later process must not treat "a log line exists" as proof that a refund was approved.

## Prove That Rejected Proposals Never Execute

The architectural promise of this sequence is simple: **every blocked path makes zero executor calls.** A recording executor makes that observable:

```csharp
public sealed class RecordingRefundExecutor(
    RefundWriteResult result = RefundWriteResult.Issued) : IRefundExecutor
{
    private int calls;

    public int Calls => Volatile.Read(ref calls);

    public Task<RefundWriteResult> IssueAsync(
        OrderId orderId,
        string expectedVersion,
        decimal amount,
        string currency,
        RefundReason reason,
        string agentId,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        return Task.FromResult(result);
    }
}
```

One theory then covers the rejected and pending paths, using raw payloads exactly as a model would produce them. No live model is involved:

```csharp
public static TheoryData<string, string, ToolOutcomeKind, string> BlockedProposals => new()
{
    { "malformed JSON", """{"name":"orders.refund","arguments":{""", ToolOutcomeKind.Rejected, "parse.malformed" },
    { "duplicate property", """{"name":"orders.refund","arguments":{"amount":25,"amount":2500,"currency":"USD","reason":"Damaged"}}""", ToolOutcomeKind.Rejected, "parse.malformed" },
    { "unknown tool", """{"name":"orders.cancel","arguments":{}}""", ToolOutcomeKind.Rejected, "tool.unknown" },
    { "tool name case", """{"name":"Orders.Refund","arguments":{"amount":25,"currency":"USD","reason":"Damaged"}}""", ToolOutcomeKind.Rejected, "tool.unknown" },
    { "missing field", """{"name":"orders.refund","arguments":{"amount":25,"currency":"USD"}}""", ToolOutcomeKind.Rejected, "schema.invalid" },
    { "amount as string", """{"name":"orders.refund","arguments":{"amount":"25","currency":"USD","reason":"Damaged"}}""", ToolOutcomeKind.Rejected, "schema.invalid" },
    { "unexpected field", """{"name":"orders.refund","arguments":{"amount":25,"currency":"USD","reason":"Damaged","note":"x"}}""", ToolOutcomeKind.Rejected, "schema.invalid" },
    { "model-supplied order", """{"name":"orders.refund","arguments":{"orderId":"order-2002","amount":25,"currency":"USD","reason":"Damaged"}}""", ToolOutcomeKind.Rejected, "argument.host-owned" },
    { "negative amount", """{"name":"orders.refund","arguments":{"amount":-25,"currency":"USD","reason":"Damaged"}}""", ToolOutcomeKind.Rejected, "amount.invalid" },
    { "sub-cent amount", """{"name":"orders.refund","arguments":{"amount":25.001,"currency":"USD","reason":"Damaged"}}""", ToolOutcomeKind.Rejected, "amount.invalid" },
    { "numeric enum", """{"name":"orders.refund","arguments":{"amount":25,"currency":"USD","reason":7}}""", ToolOutcomeKind.Rejected, "reason.invalid" },
    { "wrong currency", """{"name":"orders.refund","arguments":{"amount":25,"currency":"EUR","reason":"Damaged"}}""", ToolOutcomeKind.Rejected, "currency.mismatch" },
    { "above refundable", """{"name":"orders.refund","arguments":{"amount":900,"currency":"USD","reason":"Damaged"}}""", ToolOutcomeKind.Rejected, "amount.exceeds-refundable" },
    { "needs approval", """{"name":"orders.refund","arguments":{"amount":300,"currency":"USD","reason":"Damaged"}}""", ToolOutcomeKind.PendingApproval, "refund.approval-required" },
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

`Create` builds the gateway around that order with the recording executor, an in-memory order store, and a list-backed decision log. The fixture uses an order with a $400 refundable balance, and a conversation bound to that order for an agent who may issue refunds:

```csharp
private static readonly ConversationContext Conversation = new(
    new AuthenticatedAgent("agent-7", "tenant-a", CanIssueRefunds: true),
    new OrderId("order-1001"));

private static OrderRecord Order(bool fraudHold = false) => new(
    new OrderId("order-1001"),
    TenantId: "tenant-a",
    Currency: "USD",
    RefundableAmount: 400m,
    OnFraudHold: fraudHold,
    Version: "v12");
```

Policy denial gets its own test, because it needs different host facts rather than a different payload:

```csharp
[Fact]
public async Task Fraud_hold_is_denied_without_execution()
{
    var (gateway, executor, _) = Create(Order(fraudHold: true));

    ToolOutcome outcome = await gateway.HandleAsync(
        """{"name":"orders.refund","arguments":{"amount":25,"currency":"USD","reason":"Damaged"}}""",
        Conversation,
        CancellationToken.None);

    Assert.Equal(ToolOutcomeKind.Denied, outcome.Kind);
    Assert.Equal("order.fraud-hold", outcome.ReasonCode);
    Assert.Equal(0, executor.Calls);
}
```

And the happy path proves the sequence is not simply blocking everything:

```csharp
[Fact]
public async Task Valid_proposal_executes_once_against_the_host_resolved_order()
{
    var (gateway, executor, log) = Create(Order());

    ToolOutcome outcome = await gateway.HandleAsync(
        """{"name":"orders.refund","arguments":{"amount":25.50,"currency":"USD","reason":"Damaged"}}""",
        Conversation,
        CancellationToken.None);

    Assert.Equal(ToolOutcomeKind.Executed, outcome.Kind);
    Assert.Equal(1, executor.Calls);
    Assert.Equal("refund.issued", Assert.Single(log.Records).ReasonCode);
}
```

`Assert.Equal(0, executor.Calls)` is the assertion that matters. A result enum proves what the gateway *reported*. The call count proves what it *did*. A gateway that returns `Rejected` after already calling the payment provider would pass a result-only test and fail this one.

A version conflict is the one blocked outcome that does reach the executor, by design, because only the executor can see the final state. It must still report `Unavailable`, not `Executed`, and the recording executor makes that testable too.

## Schema Validity Is Necessary, but It Does Not Create Authority

It is tempting to read a strict schema as the security boundary. The walkthrough shows why it is not:

- A payload with `"amount": 900` passed the schema, the semantics, and would have passed a naive handler. Only the authoritative refundable balance stopped it.
- A payload with a valid amount passed everything up to policy. Only the fraud hold stopped it.
- A payload asking for $300 passed policy. Only the approval rule stopped it.
- `"reason": 7` passed the schema. Only semantic validation stopped it.

Schema validation decides whether a proposal is acceptable *input*. Authority comes from host facts, authorization, policy, and, where required, a person. Structured output from a provider that guarantees schema conformance removes step 3 failures from normal traffic. It does not remove steps 4 to 8.

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

- the agent runtime is trusted, and the tool handler cannot be reached through an unchecked path;
- the framework's tool registration is the allowlist, and nothing dispatches by arbitrary name;
- identity and resource facts come from the authenticated session and the system of record;
- authorization runs inside the handler, before the side effect;
- execution happens immediately, in the same process, with the application's own credentials;
- no approval step, delayed worker, or cross-service handoff is involved.

Add more structure only when the lifecycle outgrows that. Examples are approvals that pause execution, work that runs later or elsewhere, execution with less authority than the requester, or evidence that must connect proposal, decision, and execution durably. The validation steps do not change when you add that structure. They just gain more places they must be enforced.

## The Checklist

Before a model-generated tool call reaches a consequential handler, confirm that:

1. **Parsing is bounded and strict.** Size and depth are limited, duplicate properties are rejected, the envelope shape is exact, and failures become results rather than exceptions.
2. **Tools resolve through a host-owned registry** with exact name comparison and no fallback, reflection, or alternate unchecked path.
3. **The argument schema is explicit.** Fields are required, unknown fields are rejected, types are strict, and host-owned fields such as identity, tenant, or approval are refused by name.
4. **Context-free semantics are checked** before any host data is loaded, including values the schema library accepted leniently.
5. **Authoritative facts come from the host.** The resource comes from session or route context, visibility is checked without revealing existence, proposal values are compared with current facts, and the version that was checked is captured.
6. **Authorization and operational policy** run against those facts, and denial is recorded separately from rejection.
7. **Operations that need approval stop before execution**, and approval re-runs the checks against current facts.
8. **The executor is the only path to the side effect.** It holds the credentials, writes conditionally, and reports explicit results.
9. **Every outcome is recorded** as a decision, without secrets or raw model text, and the record is never read as permission.
10. **Tests assert zero executor calls** on every blocked path, not just the returned result.

## Continue Deeper

- [Why an AI Tool Call Is a Proposal, Not Authority](why-ai-tool-call-is-only-a-proposal.md) makes the underlying argument this checklist implements, including how to keep internal reason codes out of what the model sees.
- [Typed AI Proposed Intent and Schema-Validation Boundaries](../../ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md) goes deeper on steps 1 to 4: envelope validation, schema versions, unknown-field policy, and turning raw output into typed proposed intent.
- [Trust Boundaries and Least Privilege](../../security/trust-boundaries-and-least-privilege.md) explains where the host boundary sits and why credentials and authoritative identity stay on the trusted side.
- [Governed AI Tool Gateway](../../tutorials/governed-ai-tool-gateway.md) extends steps 6 to 9 into a full lifecycle with decision receipts, acknowledgment, and scoped execution authority.
- The [Governed AI Tool Gateway runnable sample](https://github.com/AsiBackbone/Learning/blob/main/samples/governed-ai-tool-gateway/README.md) runs that lifecycle end to end, with tests showing that denied paths make zero executor calls and no live model.

---

> **Read it. Run it. Question it. Improve it.**
