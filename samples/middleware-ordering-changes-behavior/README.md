# Middleware Ordering Changes Behavior Sample

This executable ASP.NET Core companion demonstrates the request/response ordering and exception-boundary behavior discussed in [Middleware Ordering Changes Behavior](../../docs/aspnetcore/middleware-ordering-changes-behavior.md).

The sample intentionally contains both:

- a **corrected** pipeline, and
- a **deliberately incorrect** pipeline.

The difference is only where the fault-producing middleware sits relative to the custom exception boundary.

That small movement changes observable behavior.

## Learning Objective

Observe these two invariants directly:

```text
Request enters middleware in registration order
   ↓
Endpoint
   ↓
Response unwinds through participating middleware in reverse order
```

and:

```text
Exception boundary
   ↓
Fault-producing middleware
   ↓
Failure can be normalized
```

versus:

```text
Fault-producing middleware
   ↓
Exception boundary never entered
   ↓
Custom handler cannot catch that earlier failure
```

## Difficulty

Intermediate

## Prerequisites

- .NET 10 SDK
- Basic familiarity with ASP.NET Core middleware
- [ASP.NET Core Learning](../../docs/aspnetcore/index.md)

No `AsiBackbone` package is required.

## Project Structure

```text
middleware-ordering-changes-behavior/
├── MiddlewareOrderingChangesBehavior/
│   ├── MiddlewareOrderingChangesBehavior.csproj
│   ├── MiddlewareOrderDemo.cs
│   └── Program.cs
├── MiddlewareOrderingChangesBehavior.Tests/
│   ├── MiddlewareOrderingChangesBehavior.Tests.csproj
│   └── MiddlewareOrderTests.cs
└── README.md
```

`MiddlewareOrderDemo` exposes the same pipeline as an in-memory `RequestDelegate` so the architectural behavior can be tested without an external server.

## Run the Corrected Pipeline

From the repository root:

```bash
dotnet run --project samples/middleware-ordering-changes-behavior/MiddlewareOrderingChangesBehavior/MiddlewareOrderingChangesBehavior.csproj -- --PipelineMode=correct --urls http://127.0.0.1:5080
```

In another terminal:

```bash
curl http://127.0.0.1:5080/
```

Watch the application log.

The normal request should show the conceptual order:

```text
exception-boundary:request
outer:request
inner:request
endpoint
inner:response
outer:response
exception-boundary:response
```

The request travels inward through the registered middleware.

The response returns through the participating middleware in reverse order.

Now trigger the fault:

```bash
curl -i http://127.0.0.1:5080/fault
```

The corrected pipeline places the exception boundary first:

```text
Exception boundary
   ↓
Outer trace
   ↓
Fault probe
```

The custom boundary has already entered the request before the fault occurs, so it can normalize the demonstration exception into a controlled `500` response.

## Run the Deliberately Incorrect Pipeline

Stop the application and restart it:

```bash
dotnet run --project samples/middleware-ordering-changes-behavior/MiddlewareOrderingChangesBehavior/MiddlewareOrderingChangesBehavior.csproj -- --PipelineMode=incorrect --urls http://127.0.0.1:5080
```

The incorrect sequence is:

```text
Fault probe
   ↓
Exception boundary
   ↓
Outer trace
```

Request:

```bash
curl -i http://127.0.0.1:5080/fault
```

The fault probe throws before calling the next middleware.

The custom exception boundary has therefore never been entered.

The hosting server may still produce its own final `500` behavior, but that is **not** the same thing as the sample's custom exception boundary handling the failure.

The focused test makes that distinction deterministic.

## Run the Tests

From the repository root:

```bash
dotnet test samples/middleware-ordering-changes-behavior/MiddlewareOrderingChangesBehavior.Tests/MiddlewareOrderingChangesBehavior.Tests.csproj
```

Or run the complete sample suite:

```bash
dotnet test samples/Samples.slnx
```

The tests prove:

1. Request and response traversal occur in opposite directions.
2. The corrected exception boundary handles a fault produced downstream.
3. The deliberately incorrect order leaves the earlier fault outside that boundary.

## What This Sample Intentionally Omits

This is a teaching artifact, not a production ASP.NET Core baseline.

It intentionally does **not** reproduce:

- Authentication infrastructure.
- Authorization policies.
- Endpoint routing.
- CORS.
- Forwarded-header trust configuration.
- Rate limiting.
- Static files.
- Production Problem Details.
- Serilog request logging.
- Security-header policy.
- Reverse-proxy deployment.
- Health checks.
- The full `NetCoreApplicationTemplate` startup sequence.

Those concerns appear in the tutorial because their dependencies make middleware ordering important.

The sample isolates the mechanics so the learner can see the ordering invariant without a production application hiding it.

## Compare with the Working Reference

The fuller `NetCoreApplicationTemplate` specimen centralizes its order-sensitive middleware in:

- [`PipelineExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/PipelineExtensions.cs)
- [ADR-0002: Use Centralized Application Middleware Pipeline](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/adr/0002-use-centralized-application-middleware-pipeline.md)
- [Middleware Pipeline](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/articles/middleware.md)

The working template includes proxy correction, logging, error handling, security headers, HTTPS redirection, static files, routing, CORS, rate limiting, authentication, authorization, endpoint mapping, and separate health-check mapping.

Do not copy that sequence mechanically.

Use it to ask:

> Which ordering dependency is each position preserving?

## Try It

Useful experiments include:

1. Move the inner trace middleware before the outer trace and predict the event order before running the test.
2. Add a middleware that returns a `418` response without calling `next` and observe which later components disappear from the trace.
3. Add a response-header middleware after the short-circuit and confirm that the short-circuited response never reaches it.
4. Add a second exception-producing middleware in a different position and determine which exception boundary can catch it.
5. Add a test that proves a short-circuit endpoint is never invoked.
6. Compare this small demonstration with the full `NetCoreApplicationTemplate` pipeline and identify which components depend on routing, authentication, proxy correction, or response coverage.

## Continue with the Diagnostic Lab

After you can run both pipeline modes, continue with [Identify Middleware Ordering Problems](../../docs/labs/identify-middleware-ordering-problems.md).

The lab asks you to predict the defective behavior before running it, distinguish host fallback behavior from the custom exception boundary, encode the repaired expectation in a focused test, repair a disposable copy of the sample, and reason about authentication/authorization, logging, security-header, and rate-limiting placement.

## License

Executable sample code under `samples/` is licensed under the MIT License. See [LICENSING.md](../../LICENSING.md).

---

> **Read it. Run it. Question it. Improve it.**
