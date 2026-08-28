---
description: A practical AI tool-calling architecture article showing why model-generated tool calls remain untrusted proposals until a trusted host validates context, authorization, and execution.
title: Why an AI Tool Call Is a Proposal, Not Authority
author: Christopher D. Cavell
published: "2026-08-28"
summary: A model may propose a tool call, but valid JSON and a known tool do not create authority; the trusted host still owns context, authorization, credentials, and execution.
feed: true
---

# Why an AI Tool Call Is a Proposal, Not Authority

**Pattern classification:** General learning material

**Difficulty:** Advanced

**Prerequisites:** Familiarity with C# and basic AI tool/function calling is helpful, but no AI provider, agent framework, ASI Backbone package, or prior Learning material is required. The code uses C# 12 primary constructors; use a .NET 8 or later SDK as written, or adapt the constructors for an older language version.

**What this article covers:** the proposal-versus-authority boundary; host-owned tool, argument, resource, policy, credential, and execution controls; zero-execution and concurrency-conflict tests; safe model feedback versus internal diagnostics; and when ordinary framework controls are sufficient without a larger governance lifecycle.

AI tool calling can make a dangerous architectural shortcut look reasonable:

```text
Model output
     ↓
Valid JSON
     ↓
Known tool
     ↓
Real side effect
```

The problem is not that structured output is useless. Structured output is valuable because it makes model output easier to parse, validate, and route.

The problem is treating successful parsing as permission.

A tool call can be syntactically perfect and still target the wrong resource, contain an argument the caller may not control, conflict with current policy, rely on stale context, or ask the host to use credentials the model should never possess.

The safer rule is simple:

> **The model may propose. The host retains execution authority.**

This article makes that distinction with one deliberately narrow operation: `case.add-note`.

The example does not call a live model. A local `AiToolProposal` object stands in for model output so the trust boundary can be studied without an AI SDK, API key, network request, or external provider.

## The Core Pattern at a Glance

Do not build this:

```text
Model
  ↓
Tool call
  ↓
Execute
```

Prefer a host-owned path closer to this:

```text
Model proposal
      ↓
Host tool allowlist
      ↓
Schema validation
      ↓
Semantic validation
      ↓
Authoritative host context
      ↓
Authorization / policy
      ↓
Host-owned executor
```

Each stage answers a different question.

| Stage | Question | What success proves | What it still does not prove |
| --- | --- | --- | --- |
| Tool allowlist | Is this a tool the host exposes? | The operation name is known and reachable through this host path. | That the current actor may use it against this resource. |
| Schema validation | Does the proposal match the host-owned contract? | Required fields and basic shapes are acceptable. | That the values are authoritative or permitted. |
| Semantic validation | Do the accepted arguments make sense? | The proposal is meaningful enough to evaluate further. | That the actor/resource/operation combination is authorized. |
| Authoritative host context | What are the current trusted facts? | Resource identity, actor identity, tenant, state, and other policy inputs come from trusted sources. | That current policy allows the side effect. |
| Authorization / policy | May this operation proceed now? | The current actor/resource/operation combination is permitted under current rules. | That a later write is race-free, rate-safe, or idempotent. |
| Host-owned executor | Who can create the side effect? | Only the trusted component holding execution authority can perform the operation. | That model-generated content is safe to trust when stored or read later. |

The important equation is:

```text
Schema valid
    ≠
Authoritative
    ≠
Authorized
    ≠
Executable
```

## Valid JSON Is Not Valid Authority

Suppose a model produces:

```json
{
  "tool": "case.add-note",
  "arguments": {
    "note": "Customer confirmed the maintenance window."
  }
}
```

A structured-output API may guarantee that this JSON conforms to a declared schema.

That guarantee can be useful. It may establish that:

- `tool` is a string;
- `arguments.note` exists;
- `note` is below a declared length;
- unknown fields were rejected;
- the transport representation can be deserialized safely.

It does not establish that:

- the authenticated actor may modify the current case;
- the case belongs to the actor's tenant;
- the case is still open;
- an operational hold does not block writes;
- the model-selected resource identity is trustworthy;
- the model may use the application's database or SaaS credential;
- the operation is authorized under current policy.

Schema validation is an input-acceptance boundary.

Authorization is an authority boundary.

They can live in the same component, but they should not become the same concept.

## Keep the Tool Narrow

A narrow semantic tool is easier to reason about than a generic command surface.

Prefer `case.add-note` with one accepted model-controlled field, `note`, over something like `case.execute-command` with arbitrary operation names, resource identifiers, credential hints, destinations, or host-policy claims embedded in the arguments.

A narrow tool does not eliminate the need for authorization. It reduces the number of meanings the authorization boundary must defend.

## Separate Model-Controlled Arguments from Host Facts

A useful design decision is to decide explicitly which values the model may propose.

For `case.add-note`, let the model propose only the note text:

```csharp
public sealed class AiToolProposal
{
    private AiToolProposal(
        string toolName,
        IReadOnlyDictionary<string, string> arguments)
    {
        ToolName = toolName;
        Arguments = arguments;
    }

    public string ToolName { get; }

    public IReadOnlyDictionary<string, string> Arguments { get; }

    public static bool TryCreate(
        string toolName,
        IEnumerable<KeyValuePair<string, string>> arguments,
        out AiToolProposal? proposal,
        out string? rejectionReasonCode)
    {
        var normalizedArguments =
            new Dictionary<string, string>(StringComparer.Ordinal);

        foreach ((string key, string value) in arguments)
        {
            if (!normalizedArguments.TryAdd(key, value))
            {
                proposal = null;
                rejectionReasonCode = "tool.arguments.duplicate";
                return false;
            }
        }

        proposal = new AiToolProposal(
            toolName,
            normalizedArguments);
        rejectionReasonCode = null;
        return true;
    }
}
```

The host now owns argument-name comparison rather than inheriting whatever comparer happened to be used by the caller's dictionary. `StringComparer.Ordinal` makes `note` and `Note` different keys. Exact duplicates become the explicit rejection `tool.arguments.duplicate` instead of throwing an exception that might escape into the agent loop. Perform this normalization while the raw provider payload still preserves repeated properties; once a JSON deserializer has silently collapsed duplicate names into a dictionary, the host can no longer recover that evidence.

The type is intentionally a class rather than a record: dictionary-backed arguments do not provide the content-based equality semantics a reader might infer from a record. Do not use object equality or CLR `GetHashCode()` as audit/idempotency identity; assign a host-generated identifier or deliberately canonicalize the typed proposal before applying a cryptographic hash.

`IReadOnlyDictionary<string, string>` is still a teaching simplification. Real provider payloads often contain nested values, arrays, numbers, booleans, and versioned typed schemas. Preserve those types rather than flattening production tool arguments into strings merely to match this example; flattening nested data can also hide type distinctions or turn structured content into opaque stringified JSON that later validators can no longer reason about precisely.

The host supplies the resource identity separately. A dedicated type makes accidental raw-string plumbing harder:

```csharp
public readonly record struct AuthoritativeCaseId(string Value);

public readonly record struct ConcurrencyToken(string Value);

public sealed record AuthenticatedActor(
    string ActorId,
    string TenantId);

public sealed record CaseRecord(
    string CaseId,
    string TenantId,
    bool IsClosed,
    bool WritesSuspended,
    ConcurrencyToken ConcurrencyToken);
```

The wrapper type is not magic provenance. A caller can still construct it from the wrong string. In a stronger design, construct `AuthoritativeCaseId` only inside a host-owned context resolver or assembly boundary and pass the resulting typed value forward. The type then helps the compiler catch accidental calls that pass arbitrary model arguments directly.

That creates an important boundary: the model may propose the `case.add-note` operation and note text, while the host owns the authenticated actor, tenant identity, case identity, current case state, current policy, and executor credential.

If the case identifier came from a route, selected workspace, authenticated session, or another host-owned application context, do not copy a model-provided `caseId` back into the authoritative context merely because it looks plausible.

For this example, `caseId` is intentionally **not** part of the model schema. If a proposal includes it, schema validation rejects the unexpected argument rather than allowing the model to redirect the operation to another resource.

## A Minimal Host-Owned Tool Loop

The following loop is deliberately small. It uses ordinary C# types and a recording executor.

First define a result shape:

```csharp
public enum ToolResultKind
{
    Rejected,
    Unavailable,
    Denied,
    Executed
}

public sealed record ModelToolResponse(
    string Code,
    string? Field = null);

public sealed record ToolResult(
    ToolResultKind Kind,
    string InternalReasonCode)
{
    public bool Executed => Kind == ToolResultKind.Executed;

    public static ToolResult Reject(string internalReasonCode) =>
        new(ToolResultKind.Rejected, internalReasonCode);

    public static ToolResult Unavailable(string internalReasonCode) =>
        new(ToolResultKind.Unavailable, internalReasonCode);

    public static ToolResult Deny(string internalReasonCode) =>
        new(ToolResultKind.Denied, internalReasonCode);

    public static ToolResult Success() =>
        new(ToolResultKind.Executed, "case.note.executed");

    public ModelToolResponse ToModelResponse() =>
        (Kind, InternalReasonCode) switch
        {
            (ToolResultKind.Rejected,
                "tool.arguments.missing-required") =>
                new("argument.note.missing", "note"),
            (ToolResultKind.Rejected,
                "argument.note.empty") =>
                new("argument.note.empty", "note"),
            (ToolResultKind.Rejected,
                "argument.note.too-long") =>
                new("argument.note.too-long", "note"),
            (ToolResultKind.Executed,
                "case.note.executed") =>
                new("operation.executed"),
            (ToolResultKind.Rejected, _) =>
                new("proposal.invalid"),
            _ =>
                new("operation.unavailable")
        };
}
```

`ToolResult` is the host-facing result: it carries the precise outcome kind and internal diagnostic reason. `ModelToolResponse` is a separate projection type that physically has no place for `InternalReasonCode` or `ToolResultKind`. A production agent adapter should serialize only `result.ToModelResponse()`, not `ToolResult` itself. Better still, keep the host result type inside the trusted host module and expose only the model-facing DTO across the agent-loop boundary. Matching both `Kind` and `InternalReasonCode` also prevents an impossible pair such as `Rejected + case.note.executed` from being projected as a successful execution.

The mapping is intentionally asymmetric. Safe model-controlled corrections such as an empty or oversized `note` can return field-scoped codes. Host policy, tenant state, resource existence, concurrency conflicts, and infrastructure failures collapse to lower-detail responses such as `operation.unavailable`.

The raw-provider adapter should also keep proposal-construction failures inside this result taxonomy instead of letting malformed input escape as an exception:

```csharp
if (!AiToolProposal.TryCreate(
        rawToolName, rawArgumentProperties,
        out AiToolProposal? proposal,
        out string? rejectionReasonCode))
{
    return ToolResult.Reject(rejectionReasonCode!);
}

return await gateway.HandleAsync(
    proposal!, authoritativeCaseId, actor, cancellationToken);
```

Here `rawArgumentProperties` is an enumerable that still preserves repeated argument names. Duplicate-name detection belongs before a provider-specific JSON layer silently chooses first-wins or last-wins behavior.

The host resolves the current case through an authoritative source:

```csharp
public interface ICaseStore
{
    Task<CaseRecord?> FindAsync(
        AuthoritativeCaseId caseId,
        CancellationToken cancellationToken);
}
```

Authorization is also host-owned:

```csharp
public interface ICaseNoteAuthorizer
{
    Task<bool> CanAddNoteAsync(
        AuthenticatedActor actor,
        CaseRecord caseRecord,
        CancellationToken cancellationToken);
}
```

The executor is the only abstraction that can create the protected side effect:

```csharp
public enum ExecutionOutcome
{
    Written,
    VersionConflict,
    TemporarilyUnavailable
}

public interface ICaseNoteExecutor
{
    Task<ExecutionOutcome> AddNoteAsync(
        AuthoritativeCaseId caseId,
        ConcurrencyToken expectedConcurrencyToken,
        string actorId,
        string note,
        CancellationToken cancellationToken);
}
```

The opaque concurrency token gives the executor enough information to make the final write conditional without pretending every store uses a numeric version. A production store can bind it to a row version, ETag, compare-and-swap token, transaction predicate, or equivalent mechanism. Just as important, the executor returns an explicit execution outcome so a failed precondition cannot be mistaken for a successful write.

Now the gateway can keep proposal acceptance and execution visibly separate:

```csharp
public sealed class CaseToolGateway(
    ICaseStore cases,
    ICaseNoteAuthorizer authorizer,
    ICaseNoteExecutor executor)
{
    private const string AddNoteTool = "case.add-note";

    public async Task<ToolResult> HandleAsync(
        AiToolProposal proposal,
        AuthoritativeCaseId authoritativeCaseId,
        AuthenticatedActor actor,
        CancellationToken cancellationToken)
    {
        // 1. Host-owned tool allowlist.
        if (!string.Equals(
                proposal.ToolName,
                AddNoteTool,
                StringComparison.Ordinal))
        {
            return ToolResult.Reject("tool.unknown");
        }

        // 2. Host-owned schema: one required field, no extras.
        if (!proposal.Arguments.TryGetValue(
                "note",
                out string? note))
        {
            return ToolResult.Reject(
                "tool.arguments.missing-required");
        }

        if (proposal.Arguments.Count != 1)
        {
            return ToolResult.Reject(
                "tool.arguments.unexpected");
        }

        // 3. Semantic validation and normalization.
        string normalizedNote = note.Trim();

        if (normalizedNote.Length == 0)
        {
            return ToolResult.Reject("argument.note.empty");
        }

        // Teaching choice: the downstream contract here is 500 UTF-8 bytes.
        if (Encoding.UTF8.GetByteCount(normalizedNote) > 500)
        {
            return ToolResult.Reject("argument.note.too-long");
        }

        // 4. Authoritative host context.
        CaseRecord? caseRecord = await cases.FindAsync(
            authoritativeCaseId,
            cancellationToken);

        if (caseRecord is null)
        {
            return ToolResult.Unavailable("case.not-found");
        }

        if (!string.Equals(
                actor.TenantId,
                caseRecord.TenantId,
                StringComparison.Ordinal))
        {
            return ToolResult.Deny("case.tenant-mismatch");
        }

        // 5. Current policy / authorization.
        if (caseRecord.IsClosed)
        {
            return ToolResult.Deny("case.closed");
        }

        if (caseRecord.WritesSuspended)
        {
            return ToolResult.Deny("case.writes-suspended");
        }

        if (!await authorizer.CanAddNoteAsync(
                actor,
                caseRecord,
                cancellationToken))
        {
            return ToolResult.Deny("case.note.not-authorized");
        }

        // 6. Host-owned side effect. The executor owns the final
        // concurrency precondition and reports whether a write occurred.
        ExecutionOutcome executionOutcome =
            await executor.AddNoteAsync(
                authoritativeCaseId,
                caseRecord.ConcurrencyToken,
                actor.ActorId,
                normalizedNote,
                cancellationToken);

        return executionOutcome switch
        {
            ExecutionOutcome.Written =>
                ToolResult.Success(),
            ExecutionOutcome.VersionConflict =>
                ToolResult.Unavailable("case.version-conflict"),
            ExecutionOutcome.TemporarilyUnavailable =>
                ToolResult.Unavailable("case.write-unavailable"),
            _ =>
                ToolResult.Unavailable("case.write-unknown")
        };
    }
}
```

The gateway does not need to know whether the proposal came from OpenAI, Anthropic, Azure AI, a local model, a rules engine, or a test fixture.

Its responsibility begins when untrusted proposed intent reaches the host.

## The Resource Identity Comes from the Host

Notice the method signature:

```csharp
HandleAsync(
    AiToolProposal proposal,
    AuthoritativeCaseId authoritativeCaseId,
    AuthenticatedActor actor,
    ...)
```

The proposal and the authoritative resource identity arrive through different paths.

That is intentional. A concrete attack makes the reason visible. Suppose untrusted content steers the model into proposing:

```json
{
  "tool": "case.add-note",
  "arguments": {
    "caseId": "case-from-another-tenant",
    "note": "Customer confirmed the maintenance window."
  }
}
```

In this article's contract, that proposal fails schema validation because `caseId` is an unexpected model-controlled field. The host route or selected application context still determines the actual case.

A web application might derive the case identity from a host-owned route or already-authorized application state:

```text
POST /cases/case-123/ai-note
             │
             └── host route identity

Model proposal:
{
  "tool": "case.add-note",
  "arguments": {
    "note": "Customer confirmed the maintenance window."
  }
}
```

The model helps propose **what to add**.

It does not get to redefine **which case the host is operating on** unless the application explicitly chooses to make resource selection part of the proposal and then validates that selection against authoritative host rules.

This distinction blocks a common category error: “the model said `case-999`” does not imply that `case-999` is the authorized resource.

## Prove Blocked Proposals and Execution Conflicts Precisely

A recording executor makes the protected boundary observable. It also returns an explicit execution outcome so the test can distinguish “executor was invoked” from “the write occurred”:

```csharp
public sealed class RecordingCaseNoteExecutor(
    ExecutionOutcome outcome = ExecutionOutcome.Written)
    : ICaseNoteExecutor
{
    private int invocationCount;

    public int InvocationCount =>
        Volatile.Read(ref invocationCount);

    public Task<ExecutionOutcome> AddNoteAsync(
        AuthoritativeCaseId caseId,
        ConcurrencyToken expectedConcurrencyToken,
        string actorId,
        string note,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref invocationCount);
        return Task.FromResult(outcome);
    }
}
```

The test snippets use a tiny helper so valid local fixtures still cross the same normalization boundary:

```csharp
private static AiToolProposal CreateProposal(
    string toolName,
    IEnumerable<KeyValuePair<string, string>> arguments)
{
    bool accepted = AiToolProposal.TryCreate(
        toolName,
        arguments,
        out AiToolProposal? proposal,
        out _);

    Assert.True(accepted);
    return proposal!;
}
```

A duplicate argument returns `tool.arguments.duplicate`; the provider adapter maps that reason to `ToolResult.Reject(...)` before the gateway or executor path is entered.

A rejected argument should produce zero executor calls:

```csharp
[Fact]
public async Task Empty_note_is_rejected_without_execution()
{
    var executor = new RecordingCaseNoteExecutor();
    var gateway = CreateGateway(
        executor,
        caseRecord: new(
            CaseId: "case-123",
            TenantId: "tenant-a",
            IsClosed: false,
            WritesSuspended: false,
            ConcurrencyToken: new("v7")),
        authorized: true);

    AiToolProposal proposal = CreateProposal(
        "case.add-note",
        new Dictionary<string, string>
        {
            ["note"] = "   "
        });

    ToolResult result = await gateway.HandleAsync(
        proposal,
        authoritativeCaseId: new("case-123"),
        actor: new("user-42", "tenant-a"),
        CancellationToken.None);

    ModelToolResponse modelResponse =
        result.ToModelResponse();

    Assert.Equal(ToolResultKind.Rejected, result.Kind);
    Assert.Equal("argument.note.empty", result.InternalReasonCode);
    Assert.Equal("argument.note.empty", modelResponse.Code);
    Assert.Equal("note", modelResponse.Field);
    Assert.Equal(0, executor.InvocationCount);
}
```

The last assertion is the architectural one:

```text
Rejected proposal
      ↓
Executor invocation count = 0
```

It proves more than the result enum alone. It proves that the protected side-effect boundary remained unreachable.

The proposal object also normalizes argument keys under host-owned ordinal comparison. A caller-supplied case-insensitive dictionary cannot silently make `Note` satisfy the required `note` field:

```csharp
var callerArguments =
    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Note"] = "Customer confirmed the maintenance window."
    };

AiToolProposal proposal = CreateProposal(
    "case.add-note",
    callerArguments);

ToolResult result = await gateway.HandleAsync(
    proposal,
    authoritativeCaseId: new("case-123"),
    actor: new("user-42", "tenant-a"),
    CancellationToken.None);

Assert.Equal(ToolResultKind.Rejected, result.Kind);
Assert.Equal(
    "tool.arguments.missing-required",
    result.InternalReasonCode);
Assert.Equal(0, executor.InvocationCount);
```

The same zero-invocation invariant should hold for policy denial:

```csharp
[Fact]
public async Task Closed_case_is_denied_without_execution()
{
    var executor = new RecordingCaseNoteExecutor();
    var gateway = CreateGateway(
        executor,
        caseRecord: new(
            CaseId: "case-123",
            TenantId: "tenant-a",
            IsClosed: true,
            WritesSuspended: false,
            ConcurrencyToken: new("v7")),
        authorized: true);

    AiToolProposal proposal = CreateProposal(
        "case.add-note",
        new Dictionary<string, string>
        {
            ["note"] = "Customer confirmed the maintenance window."
        });

    ToolResult result = await gateway.HandleAsync(
        proposal,
        authoritativeCaseId: new("case-123"),
        actor: new("user-42", "tenant-a"),
        CancellationToken.None);

    Assert.Equal(ToolResultKind.Denied, result.Kind);
    Assert.Equal("case.closed", result.InternalReasonCode);
    Assert.Equal(
        "operation.unavailable",
        result.ToModelResponse().Code);
    Assert.Equal(0, executor.InvocationCount);
}
```

A concurrency conflict is different: the executor is reached because the proposal and policy decision were valid, but the conditional write refuses stale state. The gateway must not report success:

```csharp
[Fact]
public async Task Version_conflict_is_not_reported_as_executed()
{
    var executor = new RecordingCaseNoteExecutor(
        ExecutionOutcome.VersionConflict);
    var gateway = CreateGateway(
        executor,
        caseRecord: new(
            CaseId: "case-123",
            TenantId: "tenant-a",
            IsClosed: false,
            WritesSuspended: false,
            ConcurrencyToken: new("v7")),
        authorized: true);

    AiToolProposal proposal = CreateProposal(
        "case.add-note",
        new Dictionary<string, string>
        {
            ["note"] = "Customer confirmed the maintenance window."
        });

    ToolResult result = await gateway.HandleAsync(
        proposal,
        authoritativeCaseId: new("case-123"),
        actor: new("user-42", "tenant-a"),
        CancellationToken.None);

    Assert.Equal(ToolResultKind.Unavailable, result.Kind);
    Assert.Equal("case.version-conflict", result.InternalReasonCode);
    Assert.Equal(
        "operation.unavailable",
        result.ToModelResponse().Code);
    Assert.False(result.Executed);
    Assert.Equal(1, executor.InvocationCount);
}
```

That distinction matters:

```text
Rejected / denied
      ↓
Executor invocation count = 0

Version conflict
      ↓
Executor invocation count = 1
      ↓
Conditional write = not performed
      ↓
Result = Unavailable, not Executed
```

A complete local test fixture can use an in-memory `ICaseStore` and a fixed `ICaseNoteAuthorizer`. Nothing about these tests requires model inference.

## Unknown Tools Should Fail Before Policy or Execution

A model can invent a convincing operation name such as `case.export-all-notes`. That does not expand the host's executable surface.

The gateway rejects it at the allowlist boundary with internal diagnostic `tool.unknown`, exposes only the lower-detail `proposal.invalid` model-safe code, and leaves executor calls at zero. No context-dependent policy work is required.

This is one reason the host should own the registry rather than deriving executable functions dynamically from model output.

## Rejection and Denial Are Different Boundaries

### Proposal Rejection Should Not Become Policy Input

Schema and semantic validation should run before expensive or security-sensitive downstream work where practical.

Examples of proposal rejection include:

| Proposal problem | Internal diagnostic | Executor calls |
| --- | --- | ---: |
| Unknown tool | `tool.unknown` | 0 |
| Duplicate argument name during raw normalization | `tool.arguments.duplicate` | 0 |
| Missing `note` | `tool.arguments.missing-required` | 0 |
| Extra model-supplied `caseId` | `tool.arguments.unexpected` | 0 |
| Empty note | `argument.note.empty` | 0 |
| Oversized note | `argument.note.too-long` | 0 |

These are proposal-acceptance failures, not authorization denials.

Keeping the meanings separate improves diagnostics and prevents malformed model output from being mislabeled as a policy decision.

A missing authoritative resource is different again. The proposal may be well formed, but the host cannot resolve the required context. This article classifies that as `Unavailable` rather than pretending it was a schema rejection or authorization decision. A production HTTP/API surface may intentionally collapse `not found` and `not authorized` into the same external response when disclosing resource existence would create an enumeration oracle.

`Unavailable` intentionally groups several host-visible conditions at the coarse result level: unresolved authoritative context, a stale concurrency precondition, and transient downstream failure. They are not operationally equivalent. The internal reason code preserves that distinction so the host can decide whether to retry, re-resolve context, suppress retry, or alert; callers should not drive those decisions from `Kind` alone.

### Policy Denial Happens After Proposal Acceptance

After the proposal is structurally and semantically acceptable, the host may still deny it:

| Current host fact | Internal diagnostic | Executor calls |
| --- | --- | ---: |
| Actor and case tenant differ | `case.tenant-mismatch` | 0 |
| Case is closed | `case.closed` | 0 |
| Writes are suspended | `case.writes-suspended` | 0 |
| Actor lacks permission | `case.note.not-authorized` | 0 |

That distinction matters because a schema-valid proposal can still be unauthorized.

A provider may have done its job perfectly while the correct host decision remains “do not execute.”

## Authorization Is Not the End of Execution Safety

Even the simple same-process path still has three execution concerns that are separate from authorization.

**State drift / TOCTOU.** The gateway reads `IsClosed` and `WritesSuspended`, then calls the executor. Another writer can change the case between those steps. That window may be small, but it is not zero. The sample therefore passes the opaque `CaseRecord.ConcurrencyToken` to the executor, and `ExecutionOutcome.VersionConflict` means the conditional write did not happen. The gateway maps that conflict to `Unavailable` instead of claiming `Executed`. If independently managed policy can change in the same window, resource concurrency alone is not enough—the executor or trusted host must also revalidate the policy facts that remain decisive. If freshness fails, do not write first and compensate later; reload current facts and decide again.

**Admission control.** A fully authorized model can still submit an abusive volume of valid proposals. Rate limits, quotas, concurrency caps, budget controls, and per-actor/tool admission rules answer a different question from authorization: not only “may this actor do this?” but “how much may this path do now?” A retry loop that passes every policy check can still become a denial-of-service or cost-amplification path without these controls.

**Retry/idempotency.** A timeout after the external system accepted the note can cause the host to retry and create a duplicate. When the downstream effect is not naturally idempotent, bind a host-owned idempotency key or operation identity to the proposed action and let the executor recognize a retry of the same intended side effect. The minimal executor signature above omits that key because the article is not modeling transport retries; in production, derive it from a stable host-generated request/operation identity or correlation record—not from a model-provided `idempotencyKey`—and pass it into the executor or downstream write contract.

None of these concerns automatically requires a capability token or workflow engine. They are ordinary host/executor responsibilities that belong at the boundary where the side effect becomes real.

## Reason Codes Are Host Diagnostics, Not Automatically Model Output

A retry-capable model creates a reverse information channel. If the host feeds detailed internal reason codes such as `case.closed`, `case.writes-suspended`, or `case.note.not-authorized` directly back into model context, the model can probe the system by varying proposals and observing policy details. Distinct `case.not-found` and `case.tenant-mismatch` responses can also become an existence oracle when resource identifiers are model-controlled.

That is why the sample uses two different types:

- `ToolResult` — precise host/operator/audit outcome and `InternalReasonCode`; and
- `ModelToolResponse` — the intentionally narrow DTO permitted to cross back into model context.

The host-facing result is not a model protocol object. A model adapter should serialize only `result.ToModelResponse()`. That projection physically cannot carry `InternalReasonCode` or `ToolResultKind`, which reduces the chance that one careless `JsonSerializer.Serialize(result)` call turns internal policy state into agent feedback. In a production codebase, keep the host result type behind the trusted module boundary so the model-facing layer receives only the projection.

Self-correction can still be useful. The sample deliberately exposes safe, model-controlled validation details:

| Internal diagnostic | Model-facing response | Why it is safe enough here |
| --- | --- | --- |
| `tool.arguments.duplicate` | `proposal.invalid` | Keeps malformed transport/schema detail inside the host. |
| `tool.arguments.missing-required` | `argument.note.missing`, field `note` | Tells the model which declared input it omitted. |
| `argument.note.empty` | `argument.note.empty`, field `note` | Describes only model-controlled content. |
| `argument.note.too-long` | `argument.note.too-long`, field `note` | Describes only the declared note-size contract. |
| `tool.arguments.unexpected` | `proposal.invalid` | Does not enumerate which extra field triggered rejection. |
| `case.closed` / `case.not-found` / `case.version-conflict` | `operation.unavailable` | Avoids exposing host state, existence, or freshness details. |

This gives a retrying model enough information to repair an empty or oversized note without turning policy state, tenant facts, resource existence, concurrency state, raw exception messages, SQL errors, secret identifiers, or authorization internals into a probing surface.

The disclosure policy is part of the tool protocol. It should be reviewed just like the input schema.

## Record the Decision Without Making the Record Authority

Accountability does not require a full event-sourcing system, but consequential tool paths should leave enough host-owned evidence to explain what happened. Record rejected, unavailable, denied, and executed outcomes with the explicit/canonical proposal identity described above, authoritative actor/resource, outcome, internal reason code, relevant concurrency/policy token or version, whether execution was attempted, and the final execution outcome when the executor was reached. Avoid storing the raw note body unless the evidence use case actually needs it; a content hash, correlation identifier, or reference is often enough for decision tracing.

If regulated or investigative requirements do require the raw body in an evidence store, that copy remains untrusted content too. Apply the same provenance, access, rendering, and future-model-ingestion rules that protect the case store itself.

The minimal code above omits recorder plumbing so the authority boundary stays visible, not because denial should disappear without a trace. Evidence is observational: it does not authorize execution, and a later executor should not treat an earlier log entry as permission.

## Keep Credentials with the Host-Owned Executor

The model does not need a case-system API token merely because it proposed `case.add-note`.

A production executor might look conceptually like this:

```csharp
public sealed class CaseNoteExecutor(
    ICaseSystemClient caseSystemClient)
    : ICaseNoteExecutor
{
    public Task<ExecutionOutcome> AddNoteAsync(
        AuthoritativeCaseId caseId,
        ConcurrencyToken expectedConcurrencyToken,
        string actorId,
        string note,
        CancellationToken cancellationToken) =>
        caseSystemClient.AddNoteIfMatchAsync(
            caseId.Value,
            expectedConcurrencyToken.Value,
            actorId,
            note,
            cancellationToken);
}
```

In this conceptual adapter, `ICaseSystemClient.AddNoteIfMatchAsync` returns `ExecutionOutcome.Written`, `VersionConflict`, or `TemporarilyUnavailable`. The important contract is that an expected concurrency failure or transient downstream failure is represented explicitly rather than discarded. A provider-specific adapter may translate an HTTP precondition failure, row-version mismatch, or known transient exception into those outcomes. Unexpected faults should still be handled by the trusted host's normal exception boundary and must not be serialized back to the model as raw exception detail.

`ICaseSystemClient` can be configured by the trusted host with a managed identity, workload identity, server-side secret store, or another credential mechanism appropriate to the environment.

The credential should not appear in:

- the prompt;
- model memory;
- the model-visible tool schema;
- model-generated arguments;
- an `AiToolProposal` field;
- logs that the model can read back.

The trust shape is simple: the model contributes a proposal; the trusted host or executor performs authorization, acquires or uses infrastructure credentials, and owns the external side effect.

A narrow tool backed by a broad administrator credential can still have a large blast radius, so infrastructure credentials should be scoped as narrowly as practical to the operation and resource boundary the host intends to expose.

## Authorized Content Is Still Untrusted Content

The gateway can correctly authorize `case.add-note` and still store model-generated prose that is unsafe to trust later. Authorization answers whether the side effect may occur; it does not certify the note text as harmless, factual, or instruction-free.

That matters when the note is later rendered in a UI, exported, indexed, copied into another channel, or read back into a future model context. The last case creates a second-order prompt-injection path: a model-generated note can survive the gateway, enter the system of record, and later influence another model that reads the case. Treat stored model/user content as untrusted data again at every new sink. Apply output encoding for the rendering context, preserve provenance, use content/DLP controls where the domain requires them, and make future model prompts distinguish quoted case data from host instructions.

Passing the execution boundary means **permitted to store**, not **trusted forever**.

## Tool Visibility and Framework Controls: Surface Reduction vs. Authority

It is good practice to expose only the tools a model needs.

A support workflow might reveal only `case.open`, `case.add-note`, and `case.search` while hiding unrelated operations such as `case.delete`, `case.export`, and `admin.rotate-keys`.

That reduces proposal surface, tool-selection confusion, and opportunities for prompt injection to steer the model toward unrelated operations.

But “tool hidden from the model” still does not imply “operation unauthorized everywhere.”

The tool may remain reachable through another endpoint, another agent, a direct service call, a misconfigured runtime path, or the underlying executor.

Model visibility answers:

> What may this model-driven path easily propose?

Authorization answers:

> May this actor perform this operation on this resource with these arguments now?

Those questions can overlap in a well-designed runtime, but they are not automatically identical.

Some SDKs and agent runtimes can automatically dispatch a model-selected tool. Auto-dispatch is not inherently unsafe, but the dispatcher becomes part of the trusted execution boundary only if it performs or invokes the same host-owned validation, authoritative context resolution, authorization, and credential controls before the handler can create a side effect.

The same reasoning applies to MCP-style tool discovery and invocation: exposing a tool through a server or registry describes a reachable operation; resource- and argument-level authorization still belongs inside the trusted server/handler path or another non-bypassable host boundary.

In multi-agent or multi-model systems, one agent's successful tool call should not create ambient authority for the next agent. Treat each new proposal as a new request for host evaluation unless the system deliberately carries a narrower, explicit continuation authority.

### When the Trusted Runtime Is Enough

The rule “a tool call is a proposal” does **not** imply that every application needs a custom policy engine, capability issuer, approval workflow, or separate gateway service.

A framework-native tool allowlist can be a sufficient enforcement boundary when the threat model is simple and the runtime is deliberately trusted. For example:

```text
Authenticated user
      ↓
Trusted agent runtime
      ↓
Registered case.add-note tool only
      ↓
Host validates note
      ↓
Ordinary ASP.NET Core / domain authorization
      ↓
Immediate same-process execution
```

That can be strong enough when:

- the runtime is trusted and non-bypassable for this tool path;
- unregistered tools cannot be invoked through another path;
- the tool handler uses authoritative host identity and resource state;
- argument and resource authorization are enforced before the side effect;
- credentials remain inside the trusted runtime or executor;
- execution is immediate rather than delegated to a later worker;
- no separate acknowledgment, escalation, or cross-process authority handoff is required.

In that design, the framework runtime **is** the trusted host boundary.

No extra ceremony is required merely to use AI.

The question is not whether the application has a class named `ToolGateway`.

The question is whether a trusted, non-bypassable component actually owns the authority checks and side effect.

## When an Additional Execution Boundary Becomes Useful

More machinery earns its keep when the lifecycle becomes more complex. Examples include:

- execution is delayed or queued;
- a later worker should receive less authority than the requester;
- human acknowledgment or approval interrupts the flow;
- policy is independently versioned or remotely evaluated;
- execution is delayed enough that resource or policy freshness needs an explicit continuation rule;
- the operation is high consequence;
- replay or one-time execution must be controlled explicitly;
- the executor runs in another process, service, tenant, or trust boundary;
- durable evidence must connect proposal, decision, continuation authority, and execution.

At that point the flow may grow into:

```text
Model proposal
      ↓
Validation
      ↓
Authoritative context
      ↓
Governance decision
      ↓
Acknowledgment when required
      ↓
Scoped continuation authority
      ↓
Execution-boundary validation
      ↓
Host-owned executor
```

That is the broader lifecycle taught by the Governed AI Tool Gateway material.

The narrow principle does not change: the model still proposes; the trusted system still decides what may become a side effect.

## No Live Model Is Required to Test the Boundary

An AI provider is unnecessary for testing the architectural invariant.

This local typed proposal (using the test helper above):

```csharp
AiToolProposal proposal = CreateProposal(
    "case.add-note",
    new Dictionary<string, string>
    {
        ["note"] = "Customer confirmed the maintenance window."
    });
```

is enough to exercise:

- tool allowlisting;
- schema validation;
- semantic validation;
- host-owned resource identity;
- current policy;
- executor reachability;
- host-internal reason codes and the separate model-facing response projection;
- executor invocation count and explicit execution outcome.

Provider integration tests can be added separately to verify that a chosen model or SDK produces the expected proposal shape.

Do not make the correctness of the authority boundary depend on whether a live model happens to behave well during a test run.

## A Practical Review Checklist

Before allowing a model-generated tool proposal to reach a consequential handler, ask:

1. **Is the tool host-owned?** Can the model only choose from a registry the host defines?
2. **Is the proposal schema explicit and normalized by the host?** Are required, optional, forbidden, duplicate, and typed arguments clear, are argument-name comparison rules host-owned before a serializer can collapse collisions, and is the downstream unit for size limits explicit?
3. **Are semantic checks and normalization separate from parsing?** Can a well-formed but nonsensical proposal be rejected before policy work?
4. **Which values are model-controlled?** Is that list intentionally small, and are authoritative identifiers carried through a distinct host-owned path/type?
5. **Where do actor and resource facts come from?** Are identity, tenant, resource state, policy inputs, and freshness reconstructed from trusted sources rather than copied from model text?
6. **Where is authorization enforced?** Does a trusted component evaluate actor + operation + resource + arguments before execution?
7. **Who owns credentials?** Are secrets or workload identities kept outside model-visible state and scoped to the executor's real responsibility?
8. **What happens to unknown tools, rejected arguments, unavailable context, policy denial, and execution conflicts?** Proposal/context/policy blocks should leave executor calls at zero; a final concurrency conflict may reach the executor, but it must report non-success and perform no protected write.
9. **What may be disclosed back to the model?** Keep internal reason codes, result kinds, policy state, resource existence, concurrency details, and exception text behind a separate model-facing DTO or serializer contract.
10. **Does successful execution preserve untrusted-data handling?** Stored model text may later reach a UI, export, search index, or another model and must be treated as untrusted at that new sink.
11. **Can the tool path be bypassed?** Model visibility, framework registration, or auto-dispatch is weaker if another reachable path skips the same checks.
12. **Is the final write protected from drift and retries?** Use opaque concurrency tokens or equivalent transactional preconditions, return explicit write outcomes, and add host-owned idempotency where the downstream effect requires it.
13. **Can authorized volume still become abuse?** Apply rate, quota, concurrency, and cost controls separately from permission.
14. **Is simple enough actually enough?** Do not add capabilities or workflow state when a trusted same-process runtime plus ordinary authorization already protects the operation.
15. **Can tests and evidence observe the side-effect boundary?** Record or mock the executor for blocked-path assertions and retain enough decision evidence to explain proposal → host facts → outcome → execution without turning that evidence into authority.

The checklist is intentionally framework-neutral.

The trusted boundary may live in an ASP.NET Core application, an agent runtime, a background worker, an MCP server, an internal service, or another host component. What matters is the authority it actually owns and the paths it can actually block. The C# examples are only one implementation language; the trust and execution boundaries apply equally to TypeScript, Python, Java, Go, or any other host stack.

## The Architectural Test

A strong design should be able to answer this question without referring to model confidence:

> If the model produces perfectly valid output for a real tool, what independent host fact still determines whether the side effect occurs?

For the example in this article, the answer is visible:

```text
Valid case.add-note proposal
        ↓
Host resolves current case
        ↓
Host binds authenticated actor
        ↓
Host evaluates current policy
        ↓
Denied? → executor calls = 0
Allowed? → executor checks final write precondition
              ├── conflict/unavailable → no write, not Executed
              └── written → Executed
```

That is the difference between **proposal** and **authority**.

A model may help decide what to ask for.

It should not acquire permission merely by asking in the right JSON shape.

## Continue Deeper

- [Governed AI Tool Gateway](../../tutorials/governed-ai-tool-gateway.md) develops the complete proposal → context → decision → acknowledgment → scoped authority → host-owned execution lifecycle.
- [Governed AI Tool Gateway runnable sample](https://github.com/AsiBackbone/Learning/blob/main/samples/governed-ai-tool-gateway/README.md) demonstrates unknown-tool rejection, authoritative host context, acknowledgment, scoped authority, dry-run execution, observability, and the invariant that denied paths produce zero executor calls without requiring a live model.
- [Agent and Tool Authorization Models and Host-Owned Execution](../../architecture/agent-and-tool-authorization-models-and-host-owned-execution.md) is the deeper architecture comparison for model-visible tool lists, framework registration, per-agent permissions, host authorization, capability-scoped execution, credential custody, and when simple framework-native controls are sufficient.
- [Typed AI Proposed Intent and Schema-Validation Boundaries](../../ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md) goes deeper on parsing, schema versions, unknown fields, typed proposed intent, and the difference between model-provided values and authoritative host facts.
- [Governed AI Tool Gateway advanced lab](../../labs/governed-ai-tool-gateway.md) lets you deliberately weaken these boundaries and observe the failure modes.

---

> **Read it. Run it. Question it. Improve it.**
