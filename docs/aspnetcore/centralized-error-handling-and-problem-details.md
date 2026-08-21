---
description: Design centralized ASP.NET Core error handling with safe Problem Details responses, deliberate status mapping, correlation, and clear exception boundaries.
---

# Centralized Error Handling and Problem Details

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** Basic familiarity with ASP.NET Core middleware and `ILogger`. [Middleware Ordering Changes Behavior](middleware-ordering-changes-behavior.md) and [Structured Logging Without Sensitive-Data Sprawl](structured-logging-without-sensitive-data-sprawl.md) provide useful context. [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) is recommended when mapping governance outcomes to HTTP.

**Learning objective:** Design one application-level boundary for unexpected failures, return a stable and deliberately limited Problem Details contract, map known failures without hiding operational meaning, correlate public errors with internal diagnostics, and keep expected governance decisions distinct from exceptions.

## Pattern Card

> **Problem:** Error handling scattered across controllers, endpoint handlers, services, and repositories produces inconsistent status codes, duplicate exception logs, accidental information disclosure, and transport-specific logic that is difficult to review.
>
> **Pattern:** Let expected outcomes remain explicit data. Let unexpected exceptions cross to one centralized application boundary. At that boundary, map only failures the host understands, log once with deliberate diagnostic context, return a safe RFC 9457 Problem Details response, and use a generic `500` fallback for everything else.
>
> **Use when:** An ASP.NET Core application needs a consistent public error contract, centralized exception normalization, correlation with operational telemetry, or a clear distinction between expected domain/governance outcomes and unexpected execution failures.
>
> **Prefer something simpler when:** A small application has one or two endpoints and a local result-mapping function already provides a consistent contract without duplicated exception handling. Centralization should reduce ambiguity, not add ceremony for its own sake.
>
> **Observe:** An expected denial, deferral, acknowledgment requirement, or escalation recommendation can be translated directly by the host without throwing an exception merely to reach the error handler.

The central rule is:

> **Centralized error handling should normalize unexpected failures without erasing meaningful domain and governance outcomes.**

A denied decision is not an exception merely because execution does not proceed.

---

## Start by Separating Three Kinds of Outcomes

Many confusing error-handling designs begin by treating every non-success path as the same thing.

A more useful model separates three categories.

### 1. Expected application or governance outcomes

These are states the application deliberately models and expects to occur.

Examples include:

```text
Validation failed
Resource not found
Authorization denied
Governance decision = Denied
Governance decision = Deferred
Governance decision = AcknowledgmentRequired
Governance decision = EscalationRecommended
```

These outcomes should normally remain data.

The host can translate them to HTTP deliberately.

### 2. Known operational exceptions

An exception can represent a failure the host recognizes and can map safely.

For example:

```text
CatalogUnavailableException
   ↓
Known temporary dependency failure
   ↓
503 Service Unavailable
```

The public response should not expose the dependency host name, connection details, credentials, or raw exception message merely because the host recognizes the exception type.

### 3. Unexpected exceptions

An unmapped exception means the application did not complete the operation as expected and does not have a more specific public contract for the failure.

The safe fallback is usually:

```text
Unexpected exception
   ↓
Central handler
   ↓
Operational error log
   ↓
Generic 500 Problem Details
```

The client learns that the request failed and receives a correlation reference.

Operators keep the richer diagnostic evidence inside the application's observability boundary.

The three categories can be summarized as:

```text
Expected outcome
      ↓
Explicit result / decision
      ↓
Host HTTP mapping

Known exception
      ↓
Central exception handler
      ↓
Deliberate status + safe Problem Details

Unknown exception
      ↓
Central exception handler
      ↓
Generic 500 + safe Problem Details
```

This separation is more important than any particular exception class hierarchy.

---

## Why Scattered `try/catch` Becomes an Architecture Problem

Consider an application where several layers catch the same failure:

```text
Repository catches + logs
      ↓
Service catches + logs
      ↓
Endpoint catches + logs
      ↓
Endpoint invents HTTP response
```

A single failure may now produce:

- Several nearly identical log entries.
- Different status codes depending on which endpoint called the service.
- Different public response shapes.
- Repeated exception-to-HTTP translation logic.
- Inconsistent handling of correlation identifiers.
- A greater chance that one endpoint returns an exception message or stack trace.

The code may look locally defensive while the application-level contract becomes less predictable.

A centralized design instead aims for:

```text
Application code
   ↓
Expected outcomes returned explicitly
   ↓
Unexpected exceptions allowed to propagate
   ↓
One exception boundary
   ↓
One public normalization policy
```

This does **not** mean `try/catch` is forbidden below the boundary.

A lower layer may legitimately catch an exception when it can do something meaningful such as:

- Retry a transient operation.
- Translate a provider-specific exception into an application-specific exception.
- Compensate or roll back local work.
- Add context that changes the application's understanding of the failure.
- Consume an exception because the failure has actually been handled.

The problem is catching only to log and rethrow at every layer.

---

## The Central Exception Boundary Must Wrap the Work It Owns

ASP.NET Core exception handling is middleware behavior.

That means placement still matters.

Conceptually:

```text
Exception boundary
      ↓
Application middleware
      ↓
Endpoint
      ↓
Application service
```

An exception thrown by behavior that runs **before** the exception boundary is entered cannot be normalized by that boundary.

This is the same wrapping rule demonstrated in [Middleware Ordering Changes Behavior](middleware-ordering-changes-behavior.md):

```text
Boundary enters first
      ↓
Boundary awaits downstream work
      ↓
Downstream exception occurs
      ↓
Boundary can handle it
```

Do not memorize "exception handling goes first" as a universal pipeline slogan.

Ask instead:

> Which failures is this boundary expected to normalize, and does the middleware actually wrap those failure-producing components?

Some hosting, proxy, server, or very-early pipeline failures can occur outside the application's exception handler.

A centralized application boundary is strong, but it is not the only failure boundary in the system.

---

## ASP.NET Core Primitives

Modern ASP.NET Core provides complementary primitives for this design:

```text
AddProblemDetails()
      ↓
Registers Problem Details services

AddExceptionHandler<THandler>()
      ↓
Registers an IExceptionHandler implementation

UseExceptionHandler()
      ↓
Adds the application exception boundary

UseStatusCodePages()
      ↓
Can produce bodies for status codes that otherwise have no response body
```

A minimal registration can look like:

```csharp
WebApplicationBuilder builder =
    WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
```

The exact pipeline may contain other middleware.

The architectural responsibilities remain:

- `IExceptionHandler` handles exceptions that reach the boundary.
- `IProblemDetailsService` writes a standardized Problem Details representation.
- Status-code pages handle HTTP failures that did not require an exception.
- Endpoint or application code still returns expected outcomes explicitly.

---

## `IExceptionHandler` Makes the Boundary Explicit

A small handler can own three decisions:

1. Which exception types have an intentional public mapping?
2. What internal operational event should be recorded?
3. What safe public response should be returned?

For example:

```csharp
public sealed class ApplicationExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApplicationExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ExceptionProblem problem = exception switch
        {
            CatalogUnavailableException => new(
                StatusCodes.Status503ServiceUnavailable,
                "Service Unavailable",
                "/problems/catalog-unavailable",
                "dependency.catalog-unavailable",
                "The catalog is temporarily unavailable."),

            _ => new(
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "/problems/unexpected-failure",
                "unexpected.failure",
                "An unexpected error occurred.")
        };

        string traceId =
            Activity.Current?.TraceId.ToString()
            ?? httpContext.TraceIdentifier;

        logger.LogError(
            exception,
            "Application exception handled centrally. " +
            "StatusCode: {StatusCode}. ProblemCode: {ProblemCode}. " +
            "TraceId: {TraceId}.",
            problem.Status,
            problem.Code,
            traceId);

        var details = new ProblemDetails
        {
            Status = problem.Status,
            Title = problem.Title,
            Type = problem.Type,
            Detail = problem.PublicDetail,
            Instance = httpContext.Request.Path.Value
        };

        details.Extensions["code"] = problem.Code;
        details.Extensions["traceId"] = traceId;

        httpContext.Response.StatusCode = problem.Status;

        return await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = details
            });
    }
}
```

The [companion sample](https://github.com/AsiBackbone/Learning/blob/main/samples/centralized-error-handling-and-problem-details/README.md) uses the same shape with source-generated logging and focused integration tests.

The example is intentionally small.

A production handler may need additional mappings, metrics, localization policy, cancellation handling, content negotiation, or host-specific operational behavior.

The important constraint is that the handler should not become a place where every domain rule is recreated as an exception mapping.

---

## Problem Details Is a Public Error Contract

RFC 9457 defines the Problem Details format for machine-readable HTTP API errors.

A typical response can contain fields such as:

```json
{
  "type": "/problems/catalog-unavailable",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "The catalog is temporarily unavailable.",
  "instance": "/failure/catalog",
  "code": "dependency.catalog-unavailable",
  "traceId": "00-..."
}
```

The standard fields have different purposes:

| Field | Purpose |
| --- | --- |
| `type` | Identifies the problem type or category. |
| `title` | Short human-readable summary. |
| `status` | HTTP status associated with the occurrence. |
| `detail` | Human-readable detail safe for the caller. |
| `instance` | Identifies the specific request/problem occurrence, often with a request path or URI reference. |

Extensions can add application-specific fields such as:

```text
code
traceId
```

Do not make clients parse the human-readable `detail` text to determine program behavior.

Prefer a stable code such as:

```text
dependency.catalog-unavailable
```

The message can then change for wording, localization, or usability without breaking the machine contract.

### Stable code does not mean internal implementation detail

A public problem code should identify a supported client-facing category.

Avoid exposing identifiers such as:

```text
SqlException-18456
ClusterNode-east-prod-07
InternalPolicyClass42
```

unless those identifiers are deliberately part of the public API contract.

A stable code should help the client react without publishing unnecessary internal topology.

---

## Public Error Information and Internal Diagnostics Serve Different Boundaries

The same failure may need two representations.

### Internal operational event

Operators may need:

```text
Exception type
Stack trace
Dependency name
Failure category
Status mapping
Trace ID
Request path
Timing
```

### Public Problem Details

The caller may need only:

```text
Status
Stable problem type/code
Safe explanatory text
Trace/correlation reference
```

The relationship is:

```text
Rich internal diagnostic evidence
      ↓
Operator observability boundary

Safe public problem contract
      ↓
Caller boundary
```

Do not copy the internal event into the public response.

Do not make the public response so empty that support cannot correlate it with internal telemetry.

A trace or correlation identifier often provides the bridge.

---

## Information Disclosure: What Not to Return

A production error response should not expose values merely because they exist on an exception.

Avoid returning:

- Stack traces.
- Raw exception messages by default.
- Connection strings.
- Database server names unless intentionally public.
- API keys, tokens, cookies, or credentials.
- File-system paths.
- Internal service topology.
- Raw request or response bodies.
- Provider-specific query text.
- Internal policy objects or unreviewed identifiers.
- Personal or regulated data not required by the caller.

For example, suppose an internal exception says:

```text
Connection to sql.internal.example failed.
Password=demo-secret
```

The public response should not repeat that text.

It can instead say:

```text
The catalog is temporarily unavailable.
```

with a stable code and trace ID.

This is not about hiding every fact from a caller.

It is about making disclosure a reviewed API decision rather than an accidental consequence of `exception.Message`.

---

## Development Versus Production Behavior

Development environments often need more diagnostics.

ASP.NET Core can provide developer-oriented exception information during local development.

That does not require the production API contract to become verbose.

A useful separation is:

```text
Development diagnostics
   ↓
Developer tooling / local logs / debugger

Public API contract
   ↓
Safe Problem Details shape
```

If an application chooses to include additional details in Development responses, make the environment boundary explicit and test that Production does not expose them.

Do not rely only on a developer remembering not to deploy a verbose flag.

The companion sample deliberately returns the same safe public exception details in every environment so its disclosure tests remain deterministic.

A production application can layer development tooling around that boundary without changing the lesson.

---

## Map Only Exceptions You Understand

A mapping table should describe application meaning, not merely CLR type names.

For example:

| Exception or failure | Possible HTTP mapping | Reasoning |
| --- | --- | --- |
| `CatalogUnavailableException` | `503 Service Unavailable` | Host has classified the condition as temporary dependency unavailability. |
| Explicit request-parse failure owned by the HTTP boundary | `400 Bad Request` | The request is invalid because of caller-controlled input. |
| Unknown/unmapped exception | `500 Internal Server Error` | Host has no narrower supported public contract. |

Be cautious with broad mappings such as:

```text
ArgumentException = 400
```

An `ArgumentException` can be caused by a server-side programming bug just as easily as by bad caller input.

If all argument exceptions automatically become `400`, an internal defect can be mislabeled as a client error.

Prefer a specific request-validation result or a narrow application exception when the caller truly owns the invalid input.

The working `NetCoreApplicationTemplate` reference follows this conservative distinction in its centralized handler.

---

## Expected Governance Outcomes Are Not Exceptions

The foundational governance material uses explicit outcomes such as:

```text
Allowed
Denied
Deferred
AcknowledgmentRequired
EscalationRecommended
```

These values describe what the governed workflow should do next.

They should remain transport-independent.

The HTTP host can translate them.

For example:

| Governance outcome | Possible HTTP representation | Important note |
| --- | --- | --- |
| `Allowed` | Normal success result | The endpoint continues under the host's normal execution contract. |
| `Denied` | Often `403 Forbidden` | A host may choose another representation for concealment or domain-specific reasons; do not make `403` part of the governance enum. |
| `Deferred` | Often `503 Service Unavailable` or another retriable host result | Use only when the transport meaning matches the actual deferral condition. Retry metadata may be appropriate. |
| `AcknowledgmentRequired` | Often `409 Conflict` with an explicit problem code | The client needs a workflow step, not an exception stack trace. |
| `EscalationRecommended` | Often `409`, `422`, or a workflow-specific result | The exact transport mapping belongs to the host/API contract. |

The important architecture is:

```text
GovernanceDecisionOutcome
      ↓
Host mapping
      ↓
HTTP status + Problem Details when useful
```

not:

```text
GovernanceDecisionOutcome
      ↓
Throw exception
      ↓
Central handler guesses governance meaning
```

### A host mapper can remain explicit

For example:

```csharp
static IResult ToHttpResult(
    GovernanceDecision decision,
    HttpContext httpContext)
{
    return decision.Outcome switch
    {
        GovernanceDecisionOutcome.Allowed =>
            Results.NoContent(),

        GovernanceDecisionOutcome.Denied =>
            Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                type: "/problems/governance-denied",
                detail: decision.PublicDetail,
                instance: httpContext.Request.Path,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = decision.Code
                }),

        GovernanceDecisionOutcome.Deferred =>
            Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Service Unavailable",
                type: "/problems/governance-deferred",
                detail: decision.PublicDetail,
                instance: httpContext.Request.Path,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = decision.Code
                }),

        _ =>
            MapOtherExpectedWorkflowOutcome(decision, httpContext)
    };
}
```

The mapper is transport code.

The policy evaluator does not need to know what `403`, `409`, or `503` means.

That preserves the boundary established in [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md).

---

## HTTP Status Is a Translation, Not the Domain Model

It is tempting to encode HTTP directly into application decisions:

```csharp
public sealed record GovernanceDecision(
    int HttpStatusCode,
    string Message);
```

That makes the decision harder to reuse in:

- Background jobs.
- Message consumers.
- CLI applications.
- Desktop hosts.
- Tests that should reason about policy independently of HTTP.
- Future transports with different response semantics.

Prefer:

```text
Domain/governance outcome
      ↓
Host-specific translation
```

The HTTP status communicates the transport-level consequence.

The stable decision/problem code preserves the application meaning.

---

## Log the Exception Where It Is Handled

A centralized exception handler is a natural owner for the operational exception event because that is where the application decides:

```text
This exception crossed the application boundary
      ↓
This is how it is classified
      ↓
This is the public response
```

Prefer:

```text
Repository throws
      ↓
Service cannot recover → lets it propagate
      ↓
Endpoint does not catch only to rethrow
      ↓
Central handler logs once
```

rather than:

```text
Repository logs
Service logs
Endpoint logs
Central handler logs
```

A lower layer should still log if it genuinely handles, retries, translates, or consumes a failure and that event has independent operational value.

The rule is not "only one log line may ever mention an incident."

The rule is:

> **Do not repeatedly log the same exception merely because it crosses another method boundary.**

In .NET 10, handled-exception diagnostic emission by the exception-handler middleware is suppressed by default when an `IExceptionHandler` successfully handles the exception. If a host changes that diagnostic behavior, review whether the custom handler and framework diagnostics now duplicate the same event.

---

## Correlate the Public Problem with Internal Logs

A useful incident path looks like:

```text
Caller receives:
traceId = 3a4f...
      ↓
Support searches operational telemetry
      ↓
Handler log contains:
TraceId = 3a4f...
      ↓
Internal exception evidence found
```

The public response does not need the stack trace to make the incident diagnosable.

For example:

```csharp
string traceId =
    Activity.Current?.TraceId.ToString()
    ?? httpContext.TraceIdentifier;

problemDetails.Extensions["traceId"] = traceId;
```

and the structured event can use the same value:

```csharp
logger.LogError(
    exception,
    "Application exception handled centrally. TraceId: {TraceId}.",
    traceId);
```

Treat externally supplied correlation identifiers as untrusted input if you use them in addition to the host trace identifier.

Bound and validate them as described in [Structured Logging Without Sensitive-Data Sprawl](structured-logging-without-sensitive-data-sprawl.md).

A correlation field links evidence.

It does not create authentication, authorization, or proof of causality by itself.

---

## Status-Code Pages Handle Non-Exceptional HTTP Failures

Not every HTTP error should require an exception.

Examples include:

```text
404 Not Found
405 Method Not Allowed
401 Unauthorized challenge
403 Forbidden result
```

Some of these responses may be created by routing, authorization, or explicit endpoint results rather than by thrown exceptions.

`UseStatusCodePages()` can fill a response body when an error status would otherwise have no body, and `AddProblemDetails()` lets ASP.NET Core use Problem Details for that representation when a writer is available.

This gives two distinct paths:

```text
Exception thrown
      ↓
UseExceptionHandler / IExceptionHandler
      ↓
Problem Details
```

and:

```text
HTTP status produced without exception
      ↓
Status-code pages when no body exists
      ↓
Problem Details
```

Do not throw an exception simply to manufacture a `404` or `403` body when the host already knows the expected HTTP outcome.

The companion sample includes a missing-route integration test to make this distinction observable.

---

## Do Not Rewrite a Response That Has Already Started

HTTP has another practical boundary:

```text
Headers/body begin streaming
      ↓
Later exception occurs
```

Once the response has started, the application may no longer be able to replace it cleanly with a new Problem Details document and status code.

A robust handler should therefore treat:

```csharp
httpContext.Response.HasStarted
```

as a meaningful boundary.

If the response has already started, returning `false` from a custom handler can allow the framework/host to continue its fallback behavior rather than pretending a complete normalized response was written.

For streaming endpoints, file transfers, server-sent events, or long-running responses, failure behavior needs additional design beyond a normal request/response Problem Details contract.

---

## Safe Fallback Behavior for Unmapped Exceptions

A common mistake is trying to infer a specific status from every exception.

That encourages fragile rules such as:

```text
NullReferenceException → 404
ArgumentException → 400
InvalidOperationException → 409
```

Those mappings may hide programming defects behind client-looking responses.

A safer default is:

```text
Known, deliberately classified exception
      ↓
Specific safe mapping

Everything else
      ↓
500 Internal Server Error
      ↓
Generic public detail
      ↓
Rich internal diagnostic event
```

The fallback should not fail open.

An error in exception classification should not result in the application continuing a consequential side effect as though the request succeeded.

---

## Cancellation Needs Its Own Policy

Cancellation can represent several different situations:

- The caller disconnected.
- A request timeout expired.
- The application is shutting down.
- A dependency operation was canceled.
- Application code intentionally canceled work.

Do not automatically turn every `OperationCanceledException` into a generic `500` without considering which cancellation token triggered it and what the host should report.

Likewise, do not swallow cancellation indiscriminately just to make logs look quieter.

The important rule is:

> **Classify cancellation according to the operation and host lifecycle rather than treating it as ordinary policy denial or arbitrary server failure.**

The companion sample keeps cancellation outside its intentionally small scope.

---

## Problem Details Is Not Governance Audit Evidence

A Problem Details response is a client-facing transport artifact.

It may contain:

```text
status
problem type
stable code
trace ID
```

That does not make it the durable record of why a consequential governance decision occurred.

A governance receipt may need different evidence such as:

```text
Decision ID
Policy identity/version
Reason codes
Acknowledgment state
Capability reference
Execution state
Correlation ID
```

The relationship can be:

```text
Governance receipt
      ↓
Purpose-built evidence store

HTTP Problem Details
      ↓
Client-facing representation

Shared correlation / decision reference
      ↓
Links the two when appropriate
```

Do not copy the entire governance receipt into the public response merely because both are structured JSON.

See [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) for the evidence boundary.

---

## Test the Public Error Contract as an Invariant

A centralized handler is valuable only if its behavior remains consistent during refactoring.

Useful integration tests include the following.

### Denied governance decision does not become an unhandled exception

```text
Governance outcome = Denied
      ↓
Host maps to 403 Problem Details
      ↓
Central exception handler invocation count = 0
```

This proves the expected-outcome boundary.

### Unexpected exception becomes a safe `500`

```text
Endpoint throws unmapped exception
      ↓
500 Problem Details
      ↓
code = unexpected.failure
```

### Sensitive exception detail is absent from the public response

Create a fictional exception message containing a recognizable fake secret or internal host name.

Then prove the response body does not contain it.

The point is not the fake value.

The point is preventing a future refactor from replacing reviewed public detail with `exception.Message`.

### Known application exception maps consistently

```text
CatalogUnavailableException
      ↓
503 Problem Details
      ↓
code = dependency.catalog-unavailable
```

### Public trace ID matches the handler log

```text
Problem Details.traceId
      =
Structured exception log.TraceId
```

This proves the observability bridge without exposing the diagnostic payload.

### Missing route gets a Problem Details body without throwing

```text
Unknown route
      ↓
404 status
      ↓
Status-code pages
      ↓
Problem Details
```

This proves that exception handling and status-code handling are separate responsibilities.

The [companion sample](https://github.com/AsiBackbone/Learning/blob/main/samples/centralized-error-handling-and-problem-details/README.md) implements these tests against a small in-memory ASP.NET Core host.

---

## Review the Contract from Both Sides

An error-handling review should ask both client and operator questions.

### Client questions

1. Is the HTTP status appropriate to the supported API contract?
2. Is the Problem Details `type` or stable code documented enough to react to?
3. Is the `detail` text safe to disclose?
4. Is there a trace/correlation reference when support may need one?
5. Can clients distinguish a retryable deferral from a permanent denial?
6. Is an acknowledgment or escalation step represented explicitly rather than disguised as `500`?

### Operator questions

1. Is the actual exception logged where it is handled?
2. Does the event contain a stable problem category and trace ID?
3. Are duplicate exception logs being emitted at several layers?
4. Can a known mapped exception still trigger the alerting needed for an outage?
5. Can internal exception text contain secrets or regulated data that should be minimized or redacted?
6. What retention and access policy applies to the diagnostic event?

Good error handling needs both views.

A safe public response without internal visibility is hard to operate.

Rich internal diagnostics copied to the client are unsafe.

---

## Working Implementation References

Learning keeps the tutorial intentionally smaller than a production application.

`NetCoreApplicationTemplate` provides a fuller working ASP.NET Core specimen for centralized exception handling and Problem Details.

| Learning concept | Working reference | What to inspect |
| --- | --- | --- |
| Central `IExceptionHandler` mapping | [`ProblemDetailsExceptionHandler.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/ErrorHandling/ProblemDetailsExceptionHandler.cs) | Known status mapping, generic `500` fallback, response-started checks, Problem Details writing, trace/request identifiers, and logging at the handler boundary. |
| Problem Details registration and status-code behavior | [`ProblemDetailsExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/ErrorHandling/ProblemDetailsExtensions.cs) | `AddProblemDetails`, custom response enrichment, environment behavior, exception-handler registration, and status-code pages. |
| Error-contract tests | [`ProjectTemplate.Web.Tests`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/tree/main/tests/ProjectTemplate.Web.Tests) | Integration and customization tests around the working application's Problem Details behavior. |
| Operational telemetry boundary | [Structured Logging Without Sensitive-Data Sprawl](structured-logging-without-sensitive-data-sprawl.md) | Why exception diagnostics and public error responses should carry different information even when they share correlation. |

Use the implementation repository as a specimen, not as a requirement to reproduce every mapping or middleware choice.

One especially useful detail in the working handler is its conservative treatment of broad `ArgumentException` failures: a generic argument exception is not automatically labeled `400 Bad Request` because it may represent a server-side developer defect.

That is the kind of boundary this tutorial is intended to make reviewable.

---

## When a Simpler Pattern Is Better

A custom `IExceptionHandler` is not mandatory merely because ASP.NET Core supports it.

A smaller application may already have:

- One endpoint group.
- A small, consistent result type.
- Framework validation for caller errors.
- No special exception mapping beyond generic `500` behavior.
- Built-in authorization for access control.

In that case, the framework defaults plus a small amount of host result mapping may be sufficient.

Add a custom handler when it gives the application a clearer contract for:

- Known exception classes.
- Public Problem Details shape.
- Correlation.
- Central exception logging.
- Safe fallback behavior.
- Cross-endpoint consistency.

Do not build a deep exception taxonomy simply to demonstrate architectural sophistication.

The smallest clear boundary is usually the better one.

---

## Tradeoffs

### Benefits

- One application boundary creates more consistent public failure behavior.
- Expected governance outcomes remain explicit rather than exception-driven.
- Problem Details provides a standard machine-readable response shape.
- Stable problem codes reduce dependence on human-readable wording.
- Central logging reduces duplicate exception events.
- Correlation lets public incidents be matched with private diagnostics.
- A generic `500` fallback prevents arbitrary exception details from becoming public API behavior.
- Integration tests can protect disclosure and mapping invariants.

### Costs

- Exception classification becomes an application-owned policy that must be maintained.
- A centralized handler can become an oversized catch-all if domain rules are pushed into it.
- Incorrect status mappings can hide server defects or mislead clients.
- Correlation and structured logging require observability conventions.
- Streaming or already-started responses need additional failure design.
- Development diagnostics and production disclosure rules must remain clearly separated.
- A uniform Problem Details shape does not remove the need to document individual problem codes and retry/workflow semantics.

The goal is not to make every failure look identical.

The goal is to make each category predictable without erasing what the failure actually means.

---

## Official References

- [Handle errors in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0)
- [`IExceptionHandler`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.diagnostics.iexceptionhandler?view=aspnetcore-10.0)
- [`IProblemDetailsService`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.iproblemdetailsservice?view=aspnetcore-10.0)
- [RFC 9457 — Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc9457.html)

---

## Review Questions

Before moving on, you should be able to answer:

1. Why is an expected governance denial different from an unexpected exception?
2. What problem does one application-level exception boundary solve that scattered `try/catch` blocks do not?
3. Why must exception-handler middleware wrap the failures it is expected to normalize?
4. What responsibilities belong to `IExceptionHandler`?
5. What does Problem Details standardize, and what remains application-specific?
6. Why should clients use a stable problem code rather than parse `detail` text?
7. Why is returning `exception.Message` unsafe as a production default?
8. Why can broad `ArgumentException → 400` mapping hide server bugs?
9. Why should `GovernanceDecisionOutcome` remain independent of HTTP status codes?
10. How can `Denied`, `Deferred`, `AcknowledgmentRequired`, and `EscalationRecommended` be mapped by the HTTP host without throwing?
11. Why is logging the same exception at several layers usually harmful?
12. How can one trace ID connect the public response to internal structured logs?
13. What is the difference between exception handling and status-code pages?
14. Why can an already-started response prevent normal Problem Details replacement?
15. What should happen to an exception that has no deliberate mapping?
16. Why does cancellation need its own host policy rather than automatic `500` mapping?
17. Why is a Problem Details response not a governance audit receipt?
18. Which integration tests protect the public disclosure and mapping contract?
19. When would framework defaults or a smaller result-mapping function be enough?

If these answers are unclear, the application may have error responses, but it does not yet have a deliberate error-handling architecture.

---

## Related Content

- [ASP.NET Core learning area](index.md)
- [Middleware Ordering Changes Behavior](middleware-ordering-changes-behavior.md)
- [Secure-by-Default ASP.NET Core Configuration](secure-by-default-configuration.md)
- [Structured Logging Without Sensitive-Data Sprawl](structured-logging-without-sensitive-data-sprawl.md)
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md)
- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md)
- [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md)
- [Centralized Error Handling and Problem Details sample](https://github.com/AsiBackbone/Learning/blob/main/samples/centralized-error-handling-and-problem-details/README.md)
- [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

---

> **Read it. Run it. Question it. Improve it.**
