# Centralized Error Handling and Problem Details Sample

This sample is the executable companion to [Centralized Error Handling and Problem Details](../../docs/aspnetcore/centralized-error-handling-and-problem-details.md).

It demonstrates a deliberately small ASP.NET Core error boundary built around:

- `IExceptionHandler`.
- `AddProblemDetails()` / `IProblemDetailsService`.
- `UseExceptionHandler()`.
- `UseStatusCodePages()`.
- Stable public problem codes.
- Trace correlation between public responses and internal structured logs.
- Explicit host mapping for expected governance outcomes.

The sample is a teaching artifact, not a production starter application.

## Central Boundary

The main distinction is:

```text
Unexpected application failure
        ↓
Central IExceptionHandler
        ↓
Structured operational log
        ↓
Safe Problem Details
```

versus:

```text
Expected governance decision
Denied / Deferred / AcknowledgmentRequired / EscalationRecommended
        ↓
Explicit host HTTP mapping
        ↓
HTTP result / Problem Details
```

> **A denied decision is not an exception merely because execution does not proceed.**

## Project Layout

```text
centralized-error-handling-and-problem-details/
├── CentralizedErrorHandlingAndProblemDetails/
│   ├── ApplicationExceptionHandler.cs
│   ├── CentralizedErrorHandlingAndProblemDetails.csproj
│   ├── GovernanceDecision.cs
│   ├── Program.cs
│   └── SampleApplication.cs
├── CentralizedErrorHandlingAndProblemDetails.Tests/
│   ├── CentralizedErrorHandlingAndProblemDetails.Tests.csproj
│   └── ErrorHandlingIntegrationTests.cs
└── README.md
```

## Run the Sample

From the repository root:

```bash
dotnet run --project samples/centralized-error-handling-and-problem-details/CentralizedErrorHandlingAndProblemDetails/CentralizedErrorHandlingAndProblemDetails.csproj --urls http://127.0.0.1:5082
```

Then try the paths below.

### Unexpected failure

```bash
curl -i http://127.0.0.1:5082/failure/unexpected
```

Expected behavior:

```text
InvalidOperationException
        ↓
Central exception handler
        ↓
500 Internal Server Error
        ↓
code = unexpected.failure
        ↓
Safe public detail
```

The thrown exception deliberately contains fictional internal connection information. The response must not expose it.

### Known dependency failure

```bash
curl -i http://127.0.0.1:5082/failure/catalog
```

Expected behavior:

```text
CatalogUnavailableException
        ↓
Known exception mapping
        ↓
503 Service Unavailable
        ↓
code = dependency.catalog-unavailable
```

The public response describes temporary unavailability without returning the fictional internal database host or fake secret embedded in the exception message.

### Expected governance denial

```bash
curl -i http://127.0.0.1:5082/governance/denied
```

Expected behavior:

```text
GovernanceDecisionOutcome.Denied
        ↓
GovernanceHttpMapper
        ↓
403 Forbidden Problem Details
        ↓
No ApplicationExceptionHandler event
```

### Expected governance deferral

```bash
curl -i http://127.0.0.1:5082/governance/deferred
```

Expected behavior:

```text
GovernanceDecisionOutcome.Deferred
        ↓
GovernanceHttpMapper
        ↓
503 Service Unavailable Problem Details
        ↓
No ApplicationExceptionHandler event
```

The sample also supports:

```text
/governance/allowed
/governance/acknowledgment-required
/governance/escalation-recommended
```

The exact HTTP mapping is a host/API choice. The sample uses `409 Conflict` for the acknowledgment and escalation workflow examples so the transport can communicate an explicit next-step state without converting it into an exception.

### Missing route

```bash
curl -i http://127.0.0.1:5082/not-mapped
```

Expected behavior:

```text
404 produced without exception
        ↓
Status-code pages
        ↓
Problem Details body
```

This shows that exception normalization and ordinary HTTP status handling are different responsibilities.

## Run the Integration Tests

From the repository root:

```bash
dotnet test samples/centralized-error-handling-and-problem-details/CentralizedErrorHandlingAndProblemDetails.Tests/CentralizedErrorHandlingAndProblemDetails.Tests.csproj
```

The focused tests prove these invariants:

```text
Denied governance decision
        ↓
403 Problem Details
        ↓
Exception-handler log count = 0
```

```text
Deferred governance decision
        ↓
503 Problem Details
        ↓
Exception-handler log count = 0
```

```text
Unexpected exception
        ↓
500 Problem Details
        ↓
Fake secret/internal host absent from public body
```

```text
Known catalog exception
        ↓
503 Problem Details
        ↓
Stable problem code
        ↓
Fake secret/internal host absent from public body
```

```text
Problem Details traceId
        =
Structured handler-log TraceId
```

```text
Missing route
        ↓
404 Problem Details
        ↓
No exception-handler log
```

## Code Map

### `Program.cs` and `SampleApplication.cs`

`Program.cs` keeps the executable entry point small. `SampleApplication.cs` registers Problem Details and the centralized exception handler, places the exception boundary in the ASP.NET Core pipeline, enables status-code pages, and exposes deterministic demonstration endpoints. The separate builder/configuration seam also lets the integration tests run the same application through `TestServer`.

### `ApplicationExceptionHandler.cs`

Maps one known exception to a safe `503`, maps everything else to a generic `500`, logs the handled exception once with a stable event ID and trace ID, checks whether the response has already started, and writes the public Problem Details response through `IProblemDetailsService`.

### `GovernanceDecision.cs`

Keeps governance outcomes independent of HTTP, then translates those expected outcomes through a host-owned `GovernanceHttpMapper`.

The mapper demonstrates:

```text
Denied                  → 403
Deferred                → 503
AcknowledgmentRequired  → 409
EscalationRecommended   → 409
```

These are teaching choices, not universal status-code requirements.

### `ErrorHandlingIntegrationTests.cs`

Starts the sample with ASP.NET Core `TestServer`, captures structured logging state, and verifies the public response contract and correlation behavior.

## Why the Sample Does Not Return Development Exception Detail

The sample deliberately uses the same safe public exception detail in every environment.

That keeps the disclosure invariant easy to test:

```text
Internal exception detail
        ≠
Public Problem Details detail
```

A production application may use local developer tooling or additional Development-only diagnostics, but Production should still have an explicit disclosure policy.

## What the Sample Does Not Claim

This sample does not claim that:

- Every application should use the same exception classes.
- Every governance denial should be `403`.
- Every deferral should be `503`.
- Every acknowledgment or escalation workflow should use `409`.
- Every exception should be logged at `Error`.
- Problem Details is an audit ledger.
- A trace ID is authentication or authorization evidence.
- A centralized handler can normalize a response after the response body has already started.
- A small sample replaces application-specific threat modeling or production error-contract design.

Use the smallest mapping that accurately represents the host's actual contract.

## Working Reference

For a fuller production-oriented ASP.NET Core specimen, compare the sample with:

- [`ProblemDetailsExceptionHandler.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/ErrorHandling/ProblemDetailsExceptionHandler.cs)
- [`ProblemDetailsExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/ErrorHandling/ProblemDetailsExtensions.cs)
- [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

The Learning sample stays smaller so the exception boundary, public response, governance mapping, and correlation invariant remain visible.

## License

Executable sample code under `samples/` is licensed under the MIT License. See [LICENSING.md](../../LICENSING.md).
