# Structured Logging Without Sensitive-Data Sprawl

**Pattern classification:** General Learning Material

**Difficulty:** Intermediate

**Prerequisites:** Basic familiarity with ASP.NET Core, dependency injection, and `ILogger`. The [ASP.NET Core learning area](index.md) provides the broader application-architecture context. [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) is useful when comparing operational telemetry with governance evidence.

**Learning objective:** Design structured operational events that answer specific diagnostic questions without turning logging into uncontrolled data collection. Choose stable event identity, useful low-risk properties, correlation context, exception boundaries, log levels, and retention intentionally; distinguish logs from metrics and traces; and preserve a hard boundary between operational logging and governance audit evidence.

## Pattern Card

> **Problem:** Logs become less useful when they are treated as formatted text, filled with entire objects, repeated at every method boundary, or used as a convenient destination for secrets and personal data. High-volume telemetry can become expensive, difficult to search, and unsafe to retain.
>
> **Pattern:** Record small, stable, structured operational events at meaningful architectural boundaries. Give events deliberate names or identifiers, attach only the context required to answer known diagnostic questions, preserve correlation, and keep sensitive values out by default.
>
> **Use when:** An ASP.NET Core application needs production diagnostics, request/operation correlation, dependency-failure visibility, exception context, or searchable operational history.
>
> **Prefer something simpler when:** A tiny local tool has no production observability requirement and ordinary console output is sufficient. Even then, secrets and credentials should not be printed.
>
> **Observe:** Better logging usually comes from recording **less, more deliberately**. The goal is not to preserve everything that happened. The goal is to preserve the smallest useful operational evidence needed to understand system behavior.

## Logging Is Event Design

A common mental model is:

```text
Something happened
      ↓
Write a sentence
```

That approach can work for a local console program.

It becomes limiting in a production service because operators rarely search only by exact rendered sentence.

They ask questions such as:

- Which operation failed?
- Which dependency was involved?
- Which requests were part of the same flow?
- Which resource was affected?
- Did the failure happen before or after a boundary?
- Did all failures share one reason or environment?

Structured logging begins with a different model:

```text
Something happened
      ↓
Name the event
      ↓
Record deliberate properties
      ↓
Render for humans + retain structure for tools
```

The rendered message still matters.

The difference is that the message is not the only useful artifact.

## Deliberately Unsafe Example: Interpolation Plus Object Dumping

Consider a fictional request handler:

```csharp
logger.LogInformation(
    $"User {user.Email} submitted request {request}");
```

This looks convenient.

It can create several problems at once:

1. String interpolation turns the event into already-formatted text before the logging pipeline can preserve individual fields.
2. `user.Email` introduces personal data even if the diagnostic question does not require it.
3. `request.ToString()` may expose fields that were never reviewed for logging.
4. A later property added to `request` can silently expand the data written to logs.
5. The event has no stable identity beyond its sentence.
6. Searching or aggregating by operation may require parsing text.

The design rule should not be:

> If a value might help one day, put it in the log now.

Prefer:

> **Identify the diagnostic question first, then record the minimum fields needed to answer it.**

## Structured and Minimized Event

Suppose the useful diagnostic questions are:

- Which operation was requested?
- Which internal resource did it target?
- Which flow did it belong to?

Then a smaller event is enough:

```csharp
logger.LogInformation(
    "Operation {OperationName} requested for resource {ResourceId} with correlation {CorrelationId}",
    operationName,
    resourceId,
    correlationId);
```

A structured logging provider can preserve properties such as:

```text
OperationName = catalog.rebuild
ResourceId = resource-123
CorrelationId = corr-7d91
```

The human-readable message remains useful:

```text
Operation catalog.rebuild requested for resource resource-123 with correlation corr-7d91
```

but tooling does not need to recover those fields by parsing the sentence.

The improvement is not merely syntax.

The improvement is **intentional event shape**.

## Events Versus Strings

Compare these approaches:

### Formatted text first

```csharp
logger.LogInformation(
    $"Operation {operationName} completed in {elapsedMilliseconds} ms");
```

### Message template with properties

```csharp
logger.LogInformation(
    "Operation {OperationName} completed in {ElapsedMilliseconds} ms",
    operationName,
    elapsedMilliseconds);
```

The second form communicates that `OperationName` and `ElapsedMilliseconds` are event properties rather than incidental text fragments.

That makes it easier for providers and backends to support:

- Filtering.
- Searching.
- Grouping.
- Alerting.
- Correlation.
- Aggregation where appropriate.

Do not turn every local variable into a property.

Structure should serve a diagnostic purpose.

## Stable Event Identity

A message template can change over time while the underlying event remains conceptually the same.

For important operational events, a stable `EventId` can make that identity explicit:

```csharp
public static class ApplicationEvents
{
    public static readonly EventId OperationRequested =
        new(2101, "OperationRequested");

    public static readonly EventId DependencyTimedOut =
        new(2401, "DependencyTimedOut");
}
```

Use it with the event:

```csharp
logger.LogInformation(
    ApplicationEvents.OperationRequested,
    "Operation {OperationName} requested for resource {ResourceId} with correlation {CorrelationId}",
    operationName,
    resourceId,
    correlationId);
```

A useful event identity should be stable enough that operators and tests can reason about it.

Avoid treating event numbers as global truth across unrelated applications unless the organization has deliberately designed such a convention.

A local range or category convention is often sufficient.

## Source-Generated Logging for Stable High-Volume Events

Modern .NET also supports source-generated logging with `LoggerMessageAttribute`.

A small example:

```csharp
internal static partial class ApplicationLog
{
    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Information,
        Message = "Operation {OperationName} requested for resource {ResourceId} with correlation {CorrelationId}")]
    public static partial void OperationRequested(
        ILogger logger,
        string operationName,
        string resourceId,
        string correlationId);
}
```

Call it with:

```csharp
ApplicationLog.OperationRequested(
    logger,
    operationName,
    resourceId,
    correlationId);
```

This is useful when an event is stable and frequent enough that consistency and logging overhead matter.

It is not required for every application log message.

The architectural lesson remains the same:

```text
Stable event
      +
Stable property names
      +
Minimal useful context
```

## Choose Properties by Diagnostic Question

A property belongs in an operational event because it helps answer an expected operational question.

For an outbound dependency call, useful properties might be:

```text
OperationName
DependencyName
Outcome
ElapsedMilliseconds
CorrelationId
TraceId
```

A property does **not** belong merely because it is available in memory.

Ask of every field:

1. Which operational question does this answer?
2. Will anyone search, group, or filter by it?
3. Is it sensitive or identifying?
4. Is its value bounded in size?
5. Is its cardinality appropriate for the destination?
6. Does retaining it create a new security or privacy obligation?
7. Could a stable non-sensitive identifier answer the same question?

If the answer to the first two questions is unclear, the field may not belong in the event.

## A Practical Data-Minimization Table

No universal table can classify every application's data.

The following is a conservative starting point:

| Value | Default logging stance | Why |
| --- | --- | --- |
| Passwords, API keys, access tokens, refresh tokens, client secrets | **Never log** | These are credentials or authority-bearing secrets. |
| `Authorization` and cookie values | **Never log** | They can expose authentication/session material. |
| Password-reset or one-time verification codes | **Never log** | They may grant temporary authority. |
| Full request or response bodies | **Off by default** | Payloads can contain secrets, personal data, documents, prompts, or large unbounded content. |
| Query strings | **Off by default** | Applications and upstream clients sometimes place sensitive values in queries. |
| User email or display name | **Avoid by default** | Personal data is often unnecessary for diagnostics; prefer a reviewed internal actor identifier when identity is needed. |
| Remote IP address | **Purpose-specific** | It can be operationally useful but may be identifying and creates retention/privacy considerations. |
| Internal resource identifier | **Usually acceptable after review** | Useful for targeted diagnostics if the identifier itself is not secret or highly sensitive. |
| Correlation/trace identifier | **Usually useful** | Links events without requiring full payload duplication; still validate externally supplied identifiers. |
| Operation name | **Recommended** | Stable, low-cardinality context makes event grouping and searching easier. |
| Status/outcome/reason category | **Recommended** | Helps explain behavior without requiring full object dumps. |

Do not assume that an identifier is harmless merely because it is not a name.

A customer number, medical-record identifier, case number, or tenant key may still be sensitive in context.

## Correlation Connects Events Without Copying Everything

Correlation allows several small events to describe one flow:

```text
CorrelationId = corr-7d91

Event 1
OperationRequested

Event 2
DependencyCallStarted

Event 3
DependencyTimedOut

Event 4
OperationDeferred
```

This is preferable to copying the full request into every event.

A correlation identifier can be generated by the host or derived from an existing request/trace context.

If a client supplies a correlation header, treat the value as **untrusted input**:

- Bound its length.
- Validate or normalize its shape.
- Do not treat it as authentication or authorization evidence.
- Do not allow it to inject uncontrolled text into downstream systems.
- Generate a host value when the supplied value is unusable.

Correlation tells you:

> These events belong to the same operational flow.

It does not tell you:

> The caller is trusted.

## Request IDs, Correlation IDs, Trace IDs, and Span IDs

These identifiers can coexist, but they solve slightly different problems.

| Identifier | Typical purpose |
| --- | --- |
| Request ID | Identify one host request. |
| Correlation ID | Link a business or operational flow across several events and possibly several requests. |
| Trace ID | Identify a distributed-tracing trace across instrumented components. |
| Span ID | Identify one activity/span inside that trace. |

Do not create new identifiers merely because another name is available.

Prefer to reuse an existing trace or request context where it answers the question.

Add a separate application correlation ID only when the application needs a relationship that is not already represented adequately by the tracing context.

## Use Scopes for Repeated Context

When several related events need the same properties, a logging scope can reduce repetition:

```csharp
using IDisposable? scope = logger.BeginScope(
    new Dictionary<string, object?>
    {
        ["CorrelationId"] = correlationId,
        ["OperationName"] = operationName
    });

logger.LogInformation(
    "Operation accepted for processing");

logger.LogInformation(
    "Dependency {DependencyName} selected",
    dependencyName);
```

Whether scope state is rendered or exported depends on the configured provider.

The useful boundary is conceptual:

```text
Operation scope
      ↓
Shared correlation + operation context
      ↓
Several smaller events
```

Do not use a scope as permission to place sensitive actor, token, body, or claim data on every event inside the scope.

## Log at Architectural Boundaries

Logging every method entry and exit usually creates noise faster than understanding.

Prefer boundaries where state or responsibility meaningfully changes.

Useful examples include:

### Request boundary

```text
Request accepted
Request completed
Request rejected before application work
```

### Application-operation boundary

```text
Operation requested
Operation started
Operation completed
Operation deferred
Operation failed
```

### External dependency boundary

```text
Dependency call started
Dependency unavailable
Dependency timed out
Dependency returned invalid response
```

### Background-processing boundary

```text
Work item claimed
Work item completed
Work item abandoned/retried
```

### Governance boundary

Operational logs may summarize that a governance decision occurred:

```text
DecisionOutcome = Denied
ReasonCategory = policy
CorrelationId = corr-7d91
```

but the durable evidence-oriented governance record should remain a separate artifact when the system requires one.

The question is:

> Where would an operator need evidence that behavior crossed from one architectural responsibility to another?

That is usually a better logging boundary than every function call.

## Exception Logging Belongs Where the Exception Is Handled

A common failure pattern is logging the same exception repeatedly:

```text
Repository logs exception
      ↓
Service logs same exception
      ↓
Controller logs same exception
      ↓
Central handler logs same exception
```

One failure becomes four near-identical events.

Prefer logging where the exception is **handled, translated, or becomes operationally significant**.

For example:

```csharp
try
{
    await dependency.SendAsync(cancellationToken);
}
catch (TimeoutException exception)
{
    logger.LogWarning(
        exception,
        "Dependency {DependencyName} timed out during operation {OperationName} with correlation {CorrelationId}",
        dependencyName,
        operationName,
        correlationId);

    return OperationResult.Deferred("dependency.timeout");
}
```

The exception is supplied as the exception parameter.

Do not also add `exception.ToString()` as a separate property unless there is a specific reviewed reason.

Be aware that exception messages produced by dependencies can themselves contain sensitive values.

The fact that an exception object is useful diagnostically does not remove the need to review what upstream libraries include in exception text and data.

## Separate Public Error Responses from Internal Diagnostics

A production application may need rich internal diagnostics while exposing a much smaller public error response.

```text
Internal operational event
Exception type
Dependency name
Correlation/trace
Reason category
      ↓
Operator telemetry

Public response
Status
Stable problem type/code
Correlation reference when appropriate
      ↓
Caller
```

Do not return stack traces, secrets, connection strings, or internal dependency detail merely because they were useful in logs.

Logging and error-response design protect different boundaries.

The upcoming centralized error-handling material should build on this distinction.

## Request and Response Bodies Are High-Risk Logging Inputs

Full HTTP bodies are tempting because they appear to make every incident reproducible.

They also create one of the fastest paths to sensitive-data sprawl.

A body may contain:

- Credentials.
- Authentication assertions.
- Personal data.
- Payment or financial data.
- Uploaded documents.
- AI prompts or model outputs.
- Internal identifiers.
- Free-form text that nobody classified in advance.
- Large payloads that multiply storage cost.

A safer default is:

```text
Request/response body logging
      ↓
Disabled
```

Then explicitly add narrow, reviewed fields that answer known questions.

If body logging is ever enabled for a controlled diagnostic scenario:

- Scope it narrowly by endpoint/content type/environment.
- Apply size limits.
- Redact or omit sensitive fields.
- Define who may access the resulting telemetry.
- Define short retention.
- Remove the diagnostic override when the incident ends.

"Temporary debug logging" often becomes permanent unless removal is owned explicitly.

## Redaction Is a Backstop, Not a Collection Strategy

.NET provides data-classification and redaction facilities that can help sanitize known sensitive values before they are emitted.

That is useful.

It should not become this architecture:

```text
Collect everything
      ↓
Hope redaction catches it
```

Prefer:

```text
Do not collect unnecessary sensitive data
      ↓
Classify fields that are legitimately required
      ↓
Redact according to policy
      ↓
Review actual emitted output
```

Redaction reduces exposure.

Data minimization prevents the data from entering the telemetry path in the first place.

The second is usually the stronger default.

## Log Levels Express Operational Significance

Log levels should help operators decide what deserves attention.

A possible application convention is:

| Level | Typical use |
| --- | --- |
| `Trace` | Extremely detailed short-lived diagnostics, normally disabled in production. |
| `Debug` | Developer-oriented detail useful during diagnosis. |
| `Information` | Normal lifecycle events that explain meaningful application behavior. |
| `Warning` | Unexpected or degraded behavior from which the application can continue. |
| `Error` | An operation failed or could not satisfy its responsibility. |
| `Critical` | A severe failure threatens process/service viability or a major system invariant. |

Do not promote every expected client error to `Error` simply because the HTTP status is 4xx.

Likewise, do not downgrade a serious internal failure because the application managed to return a response.

The logging level should describe the operational significance to the application, not only the code path taken.

## Repeated-Event Noise Can Hide the Incident

Imagine a dependency is unavailable and every request writes five warnings.

At 1,000 requests per minute:

```text
5 warnings/request
      ×
1,000 requests/minute
      =
5,000 warnings/minute
```

The logs may become expensive and harder to investigate precisely when they are most needed.

Possible responses include:

- Reduce duplicate events so one boundary owns the failure event.
- Use metrics for aggregate rates.
- Keep one request-level event plus trace context rather than many method-level messages.
- Sample high-frequency success or debug events when the telemetry pipeline supports it.
- Preserve unsampled operational failures when the incident policy requires them.
- Add health signals for persistent dependency failure instead of repeating identical prose.

Sampling is an operational policy, not an excuse to make important events impossible to reconstruct.

And governance audit evidence should not be silently subjected to the same sampling policy as ordinary operational logs.

## Cardinality Is an Architectural Cost

A property can be perfectly structured and still be expensive.

Consider:

```text
OperationName = catalog.rebuild
```

This has low cardinality.

Now consider:

```text
ResourceId = resource-123
ResourceId = resource-124
ResourceId = resource-125
...
```

This can have high cardinality.

High-cardinality properties can be useful in logs because an operator may need to find one resource.

But indexing every high-cardinality field can increase backend cost.

The same value may be especially inappropriate as a **metric label**, where unbounded cardinality can create a large number of time series.

Therefore:

> **Choose the property for the telemetry type and query you actually need.**

Do not assume that because a field belongs in a log event it also belongs on every metric or trace attribute.

## Logging, Metrics, and Tracing Are Complementary

Observability usually combines several telemetry types.

### Logs

Best at answering:

> What discrete event occurred, with which local context?

Examples:

```text
DependencyTimedOut
OperationDeferred
ConfigurationReloadFailed
```

### Metrics

Best at answering:

> How often or how much is this happening over time?

Examples:

```text
request_count
operation_duration
active_work_items
dependency_failure_count
```

### Distributed tracing

Best at answering:

> How did this request or operation move across components, and where was time spent?

Examples:

```text
HTTP request span
      ↓
application operation span
      ↓
outbound dependency span
```

A single incident investigation may use all three:

```text
Metric alert
      ↓
Trace identifies slow dependency
      ↓
Structured log explains local failure outcome
```

Do not force one telemetry type to do all jobs.

## Environment-Specific Logging Should Change Volume, Not Safety Principles

Development legitimately needs more detail than production.

A typical relationship might be:

```text
Development
More Debug detail
Local console output
Short retention

Production
Higher minimum levels
Central collection
Explicit retention
Alerting/export policy
```

But avoid:

```text
Development = safe logging
Production = safe logging
Debug incident = log secrets temporarily
```

Secrets remain secrets in every environment.

Personal data remains governed data even when a developer is troubleshooting.

Environment-specific configuration should change **how much reviewed telemetry is emitted**, not suspend data-handling discipline.

## Retention Is Part of the Logging Design

Writing an event is only the first half of its lifecycle.

Ask:

- Where is the event stored?
- How long is it retained?
- Who can query it?
- Is it copied into multiple backends?
- Is it exported outside the application's region or trust boundary?
- Can operators delete it when required?
- Is local file rotation configured?
- Does a debug override increase retention unexpectedly?

A useful principle is:

```text
Operational usefulness window
      ↓
Retention requirement
      ↓
Storage/access policy
```

Do not retain operational telemetry indefinitely merely because storage is available.

Long retention increases cost and the impact of accidental sensitive-data collection.

## Operational Logs Are Not Governance Audit Evidence

This is the central boundary for ASI Backbone Learning.

Operational logging answers questions such as:

```text
What is the application doing?
Why is this request slow?
Which dependency failed?
Which exception occurred?
```

Governance audit evidence answers different questions:

```text
What consequential operation was proposed?
What policy decision was made?
Which reason codes applied?
Was acknowledgment required?
Which policy version produced the decision?
What execution state followed?
```

A useful separation is:

```text
Operational Event
      ↓
Troubleshooting / Observability
      ↓
May be filtered, sampled, rotated, or short-retained
```

versus:

```text
Governance Receipt
      ↓
Decision Reconstruction / Evidence
      ↓
Purpose-built schema + evidence retention/integrity requirements
```

A JSON log line is still an operational log line unless the surrounding architecture gives it the durability, custody, schema, integrity, and lifecycle properties required of governance evidence.

Likewise, an audit ledger should not become a dumping ground for verbose request diagnostics.

## The Same Correlation ID Can Link Separate Evidence Systems

The distinction does not require isolation.

The same correlation identifier can connect telemetry and governance evidence:

```text
CorrelationId = corr-7d91

Operational log
"Policy evaluation completed"
Outcome = Denied
ElapsedMilliseconds = 12
      ↓
Troubleshooting

Governance receipt
Outcome = Denied
ReasonCodes = ["resource.protected"]
PolicyVersion = 4.1
DecisionStage = decision
      ↓
Decision reconstruction
```

The shared identifier allows an investigator to move between systems.

It does **not** make the two artifacts equivalent.

This is often the right relationship:

```text
Same operation
      ↓
Same correlation
      ↓
Different purpose
Different schema
Different retention
Different integrity expectations
```

## Do Not Put Governance Evidence Only in Console Logs

If a governance event must survive process restarts, log rotation, collector outages, or later dispute, best-effort application logging is not enough by itself.

The evidence path may require:

- Durable local persistence.
- Append-style records.
- Policy identity/version.
- Reason codes.
- Explicit lifecycle stages.
- Signing or hash metadata when implemented.
- Outbox delivery for external destinations.
- Access and retention controls.

Those are governance-storage concerns.

The operational log may still emit a small correlated summary so operators can find the flow quickly.

## A Minimal Operational Logging Policy

A team can make logging easier to review by defining a small policy before individual developers add events.

For example:

```text
1. Every application event has a diagnostic purpose.
2. Stable operations use stable property names.
3. Credentials and authority-bearing tokens are never logged.
4. Request/response bodies are disabled by default.
5. Correlation/trace context is preferred over payload duplication.
6. Exceptions are logged at the handling/translation boundary, not every catch.
7. High-frequency repeated events are reviewed for noise and cost.
8. Production retention is explicit.
9. Operational logs are not the governance audit ledger.
10. Sensitive-data exceptions require explicit review, not developer convenience.
```

This is intentionally simple.

A production organization may add data classification, region-specific retention, access controls, redaction, and incident logging procedures.

## Working Implementation References

Learning keeps the examples provider-neutral and intentionally small.

`NetCoreApplicationTemplate` provides a fuller working ASP.NET Core specimen where structured logging, request correlation, trace context, filtering, local retention, and OpenTelemetry are implemented as separate concerns.

| Learning concept | Working reference | What to inspect |
| --- | --- | --- |
| Structured request logging and correlation | [`RequestLoggingExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/RequestLoggingExtensions.cs) | Correlation IDs, request/trace enrichment, excluded paths, status-based event levels, and the explicit comment warning against bodies, cookies, authorization headers, tokens, identity payloads, password/form fields, and query strings without review. |
| Logging levels, enrichment, and bounded local retention | [`appsettings.json`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/appsettings.json) | Provider-level minimums, correlation/trace properties, file rolling, retention limits, file-size limits, and request-logging options. Treat the concrete values as one implementation choice, not universal defaults. |
| Tracing and metrics as separate observability concerns | [`OpenTelemetryServiceExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/OpenTelemetryServiceExtensions.cs) | Independent tracing/metrics enablement, ASP.NET Core and `HttpClient` instrumentation, service resource identity, and optional OTLP export. |
| Governance evidence rather than ordinary telemetry | [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) | The Learning boundary between operational logs and structured decision/acknowledgment/execution evidence. |
| Production-oriented audit/telemetry hygiene | [Safe Audit and Telemetry Data Guidance](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/safe-audit-telemetry-data.md) | How the governance implementation discusses safe metadata handling across audit and telemetry surfaces without collapsing them into one store. |

Use these repositories as working specimens rather than as package requirements for the tutorial.

The reusable pattern is:

> **Record small operational events for observability; preserve separate purpose-built evidence when governance reconstruction matters.**

## Structured Logging Review Checklist

Before adding a new event, ask:

1. What exact operational question will this event answer?
2. Is there already another event at the correct architectural boundary?
3. Should this be a log, metric, trace attribute/span, health signal, or governance receipt instead?
4. Does the event have a stable operation/event identity?
5. Are the property names stable and meaningful?
6. Can any property contain a password, key, token, cookie, authorization header, verification code, or secret?
7. Can any property contain personal or regulated data?
8. Is a full object being logged when only one identifier or outcome is needed?
9. Are request/response bodies or query strings being captured unnecessarily?
10. Is the correlation identifier generated or validated safely?
11. Will the event be repeated at several layers for the same failure?
12. Is the chosen log level operationally meaningful?
13. Could the event create high volume during an outage?
14. Which properties are high-cardinality, and does the backend need to index them?
15. What is the production retention period?
16. Who can access the event after it leaves the application?
17. If this is governance evidence, why is it being sent to the ordinary logging pipeline instead of an evidence-oriented store?
18. Can a shared correlation identifier link the systems without duplicating sensitive data?

If the answers are unclear, the event is not yet fully designed.

## Tradeoffs

### Benefits

- Structured properties make diagnostics easier to search and filter.
- Stable event identity reduces dependence on message wording.
- Correlation connects a flow without copying whole payloads.
- Boundary-oriented events reduce duplicate noise.
- Data minimization reduces breach impact and retention burden.
- Explicit log/metric/trace roles make observability easier to reason about.
- Separating operational telemetry from governance evidence preserves clearer custody and lifecycle expectations.

### Costs

- Event schemas and naming conventions require maintenance.
- Teams must review what data is safe to emit.
- Restricting payload logging can make some incidents require targeted temporary instrumentation.
- Correlation and tracing configuration add implementation complexity.
- Sampling and filtering require operational ownership.
- Structured backends may charge more for indexed high-cardinality properties.
- Separate audit/evidence storage adds infrastructure when governance reconstruction is required.

The goal is not zero telemetry.

The goal is telemetry whose usefulness justifies its data, volume, retention, and operational cost.

## Official .NET and ASP.NET Core References

- [Logging in C# and .NET](https://learn.microsoft.com/dotnet/core/extensions/logging/overview)
- [High-performance logging in .NET](https://learn.microsoft.com/dotnet/core/extensions/logging/high-performance-logging)
- [Compile-time logging source generation](https://learn.microsoft.com/dotnet/core/extensions/logging/source-generation)
- [HTTP logging in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/http-logging/?view=aspnetcore-10.0)
- [.NET observability with OpenTelemetry](https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel)
- [Distributed tracing in .NET](https://learn.microsoft.com/dotnet/core/diagnostics/distributed-tracing)
- [Data redaction in .NET](https://learn.microsoft.com/dotnet/core/extensions/data-redaction)

## Review Questions

Before moving on, you should be able to answer:

1. Why is a structured logging event different from a formatted sentence?
2. Why can interpolating an entire request object create hidden logging risk?
3. What makes an event name or `EventId` useful?
4. How should you decide which properties belong in an event?
5. Why is correlation usually better than copying a full payload into several logs?
6. Why should client-supplied correlation values be treated as untrusted input?
7. When is a logging scope useful?
8. Why is logging at architectural boundaries usually better than logging every method entry/exit?
9. Where should an exception normally be logged?
10. Why are request and response bodies dangerous default telemetry?
11. Why is redaction a backstop rather than a reason to collect everything?
12. What is the difference between logs, metrics, and distributed traces?
13. How can high-cardinality properties affect observability cost?
14. Why should environment-specific logging change verbosity without relaxing secret-handling rules?
15. Why is retention part of logging architecture?
16. Why is a structured operational log not automatically a governance audit receipt?
17. How can one correlation ID connect operational telemetry and governance evidence while preserving separate purposes?

## Related Content

- [ASP.NET Core learning area](index.md)
- [Middleware Ordering Changes Behavior](middleware-ordering-changes-behavior.md)
- [Secure-by-Default ASP.NET Core Configuration](secure-by-default-configuration.md)
- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md)
- [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md)
- [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)
- [AsiBackbone](https://github.com/AsiBackbone/AsiBackbone)

---

> **Read it. Run it. Question it. Improve it.**
