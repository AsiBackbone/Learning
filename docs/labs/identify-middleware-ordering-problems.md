# Lab â€” Identify Middleware Ordering Problems

**Learning objective:** Inspect a deliberately misordered ASP.NET Core middleware pipeline, predict its request/response behavior, identify the architectural boundary that is broken, repair the order, validate the changed behavior, and explain why the correction works.

**Difficulty:** Beginner  

**Pattern classification:** Canonical pattern  

**Prerequisites:** Complete [Middleware Ordering Changes Behavior](../aspnetcore/middleware-ordering-changes-behavior.md). Run the [Middleware Ordering Changes Behavior sample](https://github.com/AsiBackbone/Learning/blob/main/samples/middleware-ordering-changes-behavior/README.md) before starting the repair exercise.

Middleware order is executable architecture.

The same components can produce different behavior when their registration order changes because earlier middleware can wrap later behavior, short-circuit requests, establish request state, or modify responses on the way back out.

The diagnostic flow for this lab is:

```text
Incorrect Order
      â†“
Predict Behavior
      â†“
Observe Request / Response
      â†“
Identify Broken Boundary
      â†“
Encode the Intended Behavior in a Test
      â†“
Reorder Middleware
      â†“
Validate
      â†“
Explain Why Behavior Changed
```

The goal is not to memorize a universal middleware list.

The goal is to learn how to prove what a particular position means.

---

# Part 1 â€” Establish the Baseline

From the repository root, run the focused sample tests:

```bash
dotnet test samples/middleware-ordering-changes-behavior/MiddlewareOrderingChangesBehavior.Tests/MiddlewareOrderingChangesBehavior.Tests.csproj
```

The existing tests establish three observable facts:

1. Request traversal and response unwinding occur in opposite directions.
2. A correctly placed exception boundary can normalize a downstream demonstration failure.
3. A fault that occurs before the exception boundary is entered remains outside that custom boundary.

Now inspect these files:

```text
samples/middleware-ordering-changes-behavior/
â”œâ”€â”€ MiddlewareOrderingChangesBehavior/
â”‚   â”œâ”€â”€ MiddlewareOrderDemo.cs
â”‚   â””â”€â”€ Program.cs
â””â”€â”€ MiddlewareOrderingChangesBehavior.Tests/
    â””â”€â”€ MiddlewareOrderTests.cs
```

Focus first on the deliberately incorrect branch in `MiddlewareOrderDemo.Configure`:

```csharp
UseFaultProbe(app, observe);
UseExceptionBoundary(app, observe);
UseTrace(app, "outer", observe);
```

Do not repair it yet.

---

# Part 2 â€” Predict Before You Run

For the incorrect pipeline, predict what will happen for each request before executing it.

Fill in a table like this in your notes:

| Request | Expected status/result | First observable event | Will the custom exception boundary handle it? | Will the endpoint run? |
| --- | --- | --- | --- | --- |
| `/` | ? | ? | ? | ? |
| `/fault` | ? | ? | ? | ? |

Use the two-direction model:

```text
Request
  â†“ registration order
Middleware
  â†“
Endpoint
  â†‘
Middleware
  â†‘ reverse unwind
Response
```

Remember that middleware which throws or short-circuits before calling `next` prevents later middleware from being entered.

Before running anything, answer:

1. Which middleware is outermost in the incorrect pipeline?
2. Which middleware is never entered when `/fault` throws immediately?
3. Can a downstream exception handler catch an exception thrown before that handler is entered?
4. For `/`, why can the pipeline still appear healthy even though the ordering defect exists?

---

# Part 3 â€” Observe the Incorrect Behavior

Run the deliberately incorrect pipeline:

```bash
dotnet run --project samples/middleware-ordering-changes-behavior/MiddlewareOrderingChangesBehavior/MiddlewareOrderingChangesBehavior.csproj -- --PipelineMode=incorrect --urls http://127.0.0.1:5080
```

In another terminal, make a normal request:

```bash
curl -i http://127.0.0.1:5080/
```

Compare the console trace with your prediction.

The normal request can still reach the endpoint because the fault probe calls the next middleware when the path is not `/fault`.

Now trigger the demonstration failure:

```bash
curl -i http://127.0.0.1:5080/fault
```

The hosting server may ultimately produce a `500` response.

That alone does **not** prove the sample's custom exception boundary handled the failure.

Use the event trace to answer the more important question:

> Did `exception-boundary:handled` occur?

For the deliberately incorrect order, the important causal sequence is:

```text
/fault request
   â†“
Fault probe entered first
   â†“
Fault probe throws before calling next
   â†“
Exception boundary is never entered
   â†“
Custom handler cannot normalize that failure
```

The architectural defect is therefore not "the application returned 500."

The defect is:

> **The failure-producing middleware sits outside the custom exception boundary that is expected to normalize it.**

---

# Part 4 â€” Make a Disposable Repair Copy

Do not change the canonical teaching sample in your working tree for the exercise.

Copy the sample to a disposable directory beside the repository.

### PowerShell

```powershell
Copy-Item -Recurse `
  samples/middleware-ordering-changes-behavior `
  ../MiddlewareOrderingLab
```

### Bash

```bash
cp -R samples/middleware-ordering-changes-behavior ../MiddlewareOrderingLab
```

The copied test project keeps its relative project reference, so the pair remains runnable together.

Run the copied tests before changing anything:

```bash
dotnet test ../MiddlewareOrderingLab/MiddlewareOrderingChangesBehavior.Tests/MiddlewareOrderingChangesBehavior.Tests.csproj
```

They should pass in the copied baseline.

---

# Part 5 â€” Encode the Target Behavior Before Repairing the Order

In the copied `MiddlewareOrderTests.cs`, replace the test named:

```text
IncorrectOrder_LeavesEarlierFaultOutsideExceptionBoundary
```

with this repair-target test:

```csharp
[Fact]
public async Task RepairedOrder_CatchesFaultInsideExceptionBoundary()
{
    List<string> events = [];

    RequestDelegate pipeline =
        MiddlewareOrderDemo.Build(
            correctOrder: false,
            events.Add);

    DefaultHttpContext context =
        CreateContext("/fault");

    await pipeline(context);

    Assert.Equal(
        StatusCodes.Status500InternalServerError,
        context.Response.StatusCode);

    Assert.Contains(
        "exception-boundary:handled",
        events);
}
```

Run the copied tests again:

```bash
dotnet test ../MiddlewareOrderingLab/MiddlewareOrderingChangesBehavior.Tests/MiddlewareOrderingChangesBehavior.Tests.csproj
```

The new repair-target test should fail because `correctOrder: false` still builds the defective sequence.

That failure is useful.

You have changed the test from:

```text
Prove the defect exists
```

to:

```text
Prove the repaired boundary works
```

Now the code must earn the new expectation.

---

# Part 6 â€” Repair the Middleware Boundary

Open the copied `MiddlewareOrderDemo.cs`.

Change only the deliberately incorrect branch.

Your constraint is:

```text
The exception boundary must be entered
before the fault-producing middleware can throw.
```

Do not copy the corrected branch mechanically.

Reason from the dependency:

```text
Exception boundary
      â†“ must wrap
Fault-producing middleware
```

Reorder the calls until the custom exception boundary encloses the fault probe.

Leave the trace middleware in a position you can explain.

Run the copied tests:

```bash
dotnet test ../MiddlewareOrderingLab/MiddlewareOrderingChangesBehavior.Tests/MiddlewareOrderingChangesBehavior.Tests.csproj
```

The repair-target test should now pass.

If it does not, inspect the event list rather than moving middleware at random.

A successful repair should make this sequence reachable:

```text
exception-boundary:request
   â†“
fault-probe:throw
   â†“
exception-boundary:handled
   â†“
exception-boundary:response
```

The endpoint should still not execute for `/fault`.

The difference is that the failure now occurs **inside** the boundary that owns its normalization behavior.

---

# Part 7 â€” Explain Why the Behavior Changed

Write a short explanation using these terms:

- registration order;
- request traversal;
- `next`;
- wrapping;
- short-circuit or early failure;
- response unwinding;
- exception boundary.

A strong explanation should be able to complete this statement:

> Moving the exception handler earlier changed behavior because ...

Avoid explanations such as:

> "ASP.NET Core wants the exception handler first."

That describes a remembered rule, not the architecture.

The useful explanation is causal:

```text
Earlier registration
   â†“
Boundary enters request first
   â†“
Boundary awaits downstream next
   â†“
Downstream fault occurs inside try/catch
   â†“
Boundary can normalize the failure
```

---

# Part 8 â€” Diagnose Other Ordering Boundaries

Exception handling is only one form of order-dependent behavior.

For each scenario below, identify the broken dependency or coverage requirement before proposing a reorder.

## Scenario A â€” Authentication and Authorization

```csharp
app.UseAuthorization();
app.UseAuthentication();
```

Question:

> If authorization expects an authenticated `ClaimsPrincipal`, which middleware produces that state and which middleware consumes it?

The dependency is:

```text
Authentication
   â†“ produces principal
Authorization
   â†“ consumes principal
```

If authorization is endpoint-specific, routing metadata is another dependency that must already be available.

## Scenario B â€” Request Logging and a Short-Circuiting Limiter

```text
Rate limiter
   â†“
Request logging
   â†“
Endpoint
```

Suppose the operational requirement is:

> Every rejected request must appear in the same request log.

A request rejected before logging is reached cannot satisfy that requirement.

Moving request logging earlier increases coverage:

```text
Request logging
   â†“
Rate limiter
   â†“
Endpoint
```

That does not make earlier logging universally correct.

If the requirement intentionally excludes noisy rejected traffic from that log, later placement may be reasonable.

## Scenario C â€” Security Headers and an Earlier Response Producer

```text
Static files
   â†“
Security headers
   â†“
Application endpoints
```

Suppose the requirement is:

> Static-file responses must receive the same selected security headers.

A static-file response can short-circuit before the security-header middleware is reached.

The coverage dependency then points toward:

```text
Security headers
   â†“
Static files
   â†“
Application endpoints
```

Again, define the response-coverage requirement first rather than memorizing the position.

## Scenario D â€” Endpoint-Specific Rate Limiting

An endpoint-specific limiter needs routing metadata.

That gives the dependency:

```text
Routing
   â†“
Endpoint metadata available
   â†“
Endpoint-specific rate limiting
```

A purely global limiter may have no such dependency and can legitimately run earlier.

This is why "Where does rate limiting go?" does not have one universal answer.

---

# Part 9 â€” Build a Dependency Table

Choose a real or sample ASP.NET Core pipeline and classify at least five middleware concerns using this shape:

| Concern | Produces | Consumes | Must wrap | Coverage requirement |
| --- | --- | --- | --- | --- |
| Example: Authentication | `ClaimsPrincipal` | Credentials/services | â€” | Requests requiring authenticated identity |
| ? | ? | ? | ? | ? |
| ? | ? | ? | ? | ? |
| ? | ? | ? | ? | ? |
| ? | ? | ? | ? | ? |

Good candidates include:

- forwarded headers;
- request logging;
- exception handling;
- security headers;
- routing;
- CORS;
- rate limiting;
- authentication;
- authorization;
- endpoint execution.

For each row, answer:

> What observable behavior would change if this component moved across the thing it depends on or is expected to wrap?

That question is more durable than memorizing a framework-specific list.

---

# Part 10 â€” Compare with the Working Reference

After completing the repair, inspect the fuller `NetCoreApplicationTemplate` pipeline:

- [`PipelineExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/PipelineExtensions.cs)
- [ADR-0002: Use Centralized Application Middleware Pipeline](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/adr/0002-use-centralized-application-middleware-pipeline.md)
- [Middleware Pipeline](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/articles/middleware.md)

Do not treat the working template's sequence as a universal answer key.

Instead, choose three neighboring components and explain the dependency or coverage goal that justifies their relative order.

If you cannot explain why two components have to be ordered relative to each other, determine whether that relationship is actually required or merely conventional.

---

# Completion Criteria

You have completed the lab when you can demonstrate all of the following:

- You predicted the incorrect pipeline's behavior before running it.
- You distinguished a host-generated `500` from the sample's custom exception boundary handling the failure.
- You identified why the fault probe sits outside the intended exception boundary.
- You changed a test so the desired repaired behavior failed before the code was changed.
- You reordered the copied pipeline so the custom boundary handles the downstream failure.
- The copied focused tests pass after the repair.
- You can explain request traversal and response unwinding without relying on a memorized middleware list.
- You can identify producer/consumer dependencies such as authentication before authorization.
- You can explain coverage tradeoffs for request logging and security headers.
- You can explain why endpoint-specific rate limiting may require a different position from global rate limiting.
- You can justify a middleware reorder in terms of observable behavior, dependency, wrapping, or coverage.

The architectural invariant should now be visible:

```text
Middleware position
   â†“
What state is available?
What behavior is wrapped?
What can short-circuit first?
Which responses unwind through this component?
   â†“
Observable application behavior
```

---

## Optional Extension â€” Add a Short-Circuit Probe

In the disposable copy, add middleware that returns `418 I'm a teapot` without calling `next` for `/short-circuit`.

Then write a test that proves:

```text
Short-circuit middleware reached
   â†“
Endpoint event absent
   â†“
Only middleware that already entered the request can participate in response unwinding
```

Move the short-circuit middleware one position at a time and record which events disappear.

This makes pipeline reachability observable without introducing authentication, routing, or external infrastructure.

---

## Related Content

- [Middleware Ordering Changes Behavior](../aspnetcore/middleware-ordering-changes-behavior.md) â€” architecture explanation and ordering dependency model.
- [Middleware Ordering Changes Behavior sample](https://github.com/AsiBackbone/Learning/blob/main/samples/middleware-ordering-changes-behavior/README.md) â€” runnable corrected and deliberately incorrect pipelines.
- [ASP.NET Core learning area](../aspnetcore/index.md) â€” broader application-architecture learning path.
- [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate) â€” fuller working ASP.NET Core reference specimen.

---

> **Read it. Run it. Question it. Improve it.**
