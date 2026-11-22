---
description: Moving one ASP.NET Core middleware line can let exceptions escape or reject valid users. Trace the pipeline, find the dependency, and test it.
title: Why Moving ASP.NET Core Middleware Breaks Your Endpoint
author: Christopher D. Cavell
published: "2026-11-22"
summary: ASP.NET Core middleware is not a list of independent filters. Each component that calls the rest of the pipeline wraps it, running before that work on the way in and after it on the way out, and a component can also answer a request without calling the rest at all. Moving one registration therefore changes what a component can see and what it can catch. Trace a small endpoint to see middleware wrapping, reverse response order, and short-circuiting. Then break and repair an exception boundary and an authentication and authorization dependency, account for the middleware WebApplication supplies automatically, and pin the order you depend on with focused integration tests.
feed: true
x_post: Middleware order is behavior. Each component wraps what follows it, so moving one line changes what it can see and catch.
x_hashtags:
  - DotNet
  - AspNetCore
x_question: "Which middleware move surprised you the most?"
---

# Why Moving ASP.NET Core Middleware Breaks Your Endpoint

**Pattern classification:** General learning material

**Difficulty:** Beginner to intermediate

**Prerequisites:** Comfortable reading basic C# and ASP.NET Core minimal API code, and registering services with `builder.Services`. Middleware, endpoint routing, authentication, and integration tests with `TestServer` are introduced in the article. No AsiBackbone package or prior Learning material is required.

**Sample scope:** The middleware concepts apply to ASP.NET Core 8 and later with the `WebApplication` hosting model and minimal APIs. The complete sample targets .NET 10 and uses the .NET 10 SDK's built-in Microsoft.Testing.Platform support; running it on an earlier SDK requires changing the target framework, package versions, and test-runner configuration. The order API and its incident are hypothetical.

**Verification:** The application code, project files, and sixteen in-process integration tests in the [appendix](#appendix-complete-code) were compiled and run on .NET 10; the appendix records the [environment and command](#environment-used-for-verification). Each result the article names as observed on .NET 10 is pinned by one of those tests. One note about ASP.NET Core 11 cites the framework's source code and was not tested. Other framework behavior is described from Microsoft's documentation on the verification date below; check it for your version. Original prose is licensed under [Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/), and original code examples are additionally offered under the [MIT License](https://opensource.org/license/mit), as described in the repository's [licensing terms](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md). Third-party material, where identified, remains under its original license. External sources were last verified on November 22, 2026.

**What this article covers:** an order endpoint whose error responses changed after one line moved; how middleware wraps the components registered after it, why responses unwind in reverse order, and what a short-circuit removes from the trace; an exception handler that stopped covering the middleware that throws, and the repair; why authorization depends on routing and authentication having run first, and what happens when it does not; the middleware `WebApplication` inserts automatically and how it can hide an ordering mistake or supply a missing component; focused integration tests that pin the order you depend on; and a [four-question diagnostic](#four-questions-before-you-move-a-registration) for the next time a registration moves.

**In plain terms:** think of a set of nesting boxes. Each box you add goes inside the previous one. A request is a marble dropped into the outermost box: it passes through every lid on the way down to the endpoint at the bottom, and back out through every lid in reverse on the way up. A box can catch something only if that something happens inside it, and a box can decide to send the marble straight back up without opening the next lid. Moving a line in `Program.cs` moves a box, and with it everything the box contains.

Consider a hypothetical order API. Every request carries an `X-Store` header, and a small middleware looks the store up in a directory service before the endpoint runs. The pipeline looked like this, and when an unhandled exception occurred, clients that accepted JSON received a [problem details](https://www.rfc-editor.org/rfc/rfc9457) response with status `500`:

```csharp
app.UseRequestLog();
app.UseExceptionHandler();   // wraps the store lookup and the endpoint
app.UseStoreLookup();
app.UseAuthentication();
app.UseAuthorization();
```

During a cleanup, a developer moved `UseExceptionHandler()` down a line, so that it sat closer to the endpoints "where error handling belongs":

```csharp
app.UseRequestLog();
app.UseStoreLookup();
app.UseExceptionHandler();   // moved: now inside the store lookup
app.UseAuthentication();
app.UseAuthorization();
```

They checked the change by calling an order that makes the endpoint throw, and still got the same problem details response. The change shipped.

Two weeks later, the store directory timed out for a few minutes. In this deployment, clients that had previously received a problem details body received a `500` with no body instead, and the client library's error parser failed on it. The request log, which normally records every request's arrival and final status code, recorded the arrival of those requests but no completion entry or status.

Nothing in the exception handler changed. Nothing in the store lookup changed. One line moved, and the handler stopped wrapping the code that threw. To see why, you need one idea: middleware wraps.

## Middleware Wraps Everything Registered After It

An ASP.NET Core middleware component is a function that receives the current `HttpContext` and a delegate, usually called `next`, that runs the rest of the pipeline. Microsoft's [middleware documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/#middleware-order) describes the consequence: components run in registration order on the way in, and in reverse order on the way out.

Two tiny components make that visible. Each writes to a shared `RequestTrace`, a thread-safe queue of strings registered as a singleton. A single shared trace is a teaching aid for tests that send one request at a time; its entries carry no request identifier, so concurrent requests would interleave.

```csharp
// Tracer A: records the request on the way in, and the status code on the way out.
public static IApplicationBuilder UseRequestLog(this IApplicationBuilder app) =>
    app.Use(async (context, next) =>
    {
        var trace = context.RequestServices.GetRequiredService<RequestTrace>();
        trace.Add("RequestLog: in");
        await next(context);
        trace.Add($"RequestLog: out {context.Response.StatusCode}");
    });

// Tracer B: answers 503 itself, without calling next, while maintenance mode is on.
public static IApplicationBuilder UseMaintenanceGate(this IApplicationBuilder app) =>
    app.Use(async (context, next) =>
    {
        var trace = context.RequestServices.GetRequiredService<RequestTrace>();
        trace.Add("Maintenance: in");

        if (context.RequestServices.GetRequiredService<MaintenanceMode>().IsOn)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            trace.Add("Maintenance: answered 503");
        }
        else
        {
            await next(context);
        }

        trace.Add("Maintenance: out");
    });
```

The endpoint adds `"endpoint"` to the same trace. Register the two components first, followed by authentication and authorization:

```csharp
app.UseRequestLog();
app.UseMaintenanceGate();
app.UseAuthentication();
app.UseAuthorization();
```

A successful request to `GET /orders/42` produces this trace:

```text
RequestLog: in
Maintenance: in
endpoint
Maintenance: out
RequestLog: out 200
```

Everything a component does before `await next(context)` happens on the way in. Everything after it happens on the way out, once all the inner components and the endpoint have finished, provided that `next` completes without throwing. The request log was registered first, so it is the outermost of the components shown: it is the first of them to see the request and the last to see the response, which is why it can record the final status code. (`WebApplication` also adds routing in front of all of them; [What WebApplication Adds for You](#what-webapplication-adds-for-you) explains why that matters.) The `endpoint` line appears in the middle because, in this illustrated pipeline, endpoint execution is the innermost work reached through these components.

```text
RequestLog ──────────────────────────────────────────┐
│  in                                                │
│   Maintenance gate ─────────────────────────────┐  │
│   │  in                                         │  │
│   │      authentication, authorization          │  │
│   │      endpoint                               │  │
│   │  out                                        │  │
│   └─────────────────────────────────────────────┘  │
│  out 200                                           │
└────────────────────────────────────────────────────┘
```

The response does not travel back through a separate "response pipeline". It is the same chain of method calls returning. A component's position decides what earlier components have already established when it runs, which later components it can stop, and which exceptions it can catch: only those thrown while the work inside its call to `next` is running.

### What a short-circuit changes

Middleware is not obliged to call `next`. When maintenance mode is on, the gate writes `503` and returns without calling it. The documentation calls such a component terminal for that request, because it prevents the components after it from running. The trace shows exactly what was skipped:

```text
RequestLog: in
Maintenance: in
Maintenance: answered 503
Maintenance: out
RequestLog: out 503
```

The endpoint never ran. The request log still saw the request and its `503`, because the gate is inside the log.

Now move the gate above the log, the kind of change that looks harmless in review:

```csharp
app.UseMaintenanceGate();
app.UseRequestLog();
app.UseAuthentication();
app.UseAuthorization();
```

With maintenance mode on, the trace becomes:

```text
Maintenance: in
Maintenance: answered 503
Maintenance: out
```

The request log never ran for these requests. Nothing failed and nothing was reported; the log simply has no record of any request the gate answered. This is the "request logging disappeared" version of the same problem. Short-circuits are common and legitimate: static-file middleware, health-check handling, CORS preflight handling, authorization challenges and forbids, rate limiting, response caching, and application-specific gates like this one can all produce a response without running the rest of the pipeline. A component placed after one of them only ever sees the requests that component lets through.

Two practical consequences follow:

- A component that must see every request, such as request logging or a correlation identifier, has to be outside every short-circuit it must observe.
- A component that adds a response header must do so before the response starts. The response starts when its headers are sent: writing or flushing the body normally does that, and `Response.StartAsync()` can do it without writing any body. After that, `context.Response.HasStarted` is `true`, and attempts to change the status code or headers are invalid and ordinarily throw. Being outside the component that wrote the body does not help, because by the time `await next(context)` returns, the response may already have started. Set the header before calling `next`, or register a callback with `context.Response.OnStarting` before calling `next`; the callback runs just before the headers are sent.

## Breaking and Repairing the Exception Boundary

Return to the order API. The store lookup is ordinary middleware that can throw:

```csharp
// Looks up the caller's store before the endpoint runs. The lookup can throw.
public static IApplicationBuilder UseStoreLookup(this IApplicationBuilder app) =>
    app.Use(async (context, next) =>
    {
        var stores = context.RequestServices.GetRequiredService<IStoreDirectory>();
        string storeCode = context.Request.Headers["X-Store"].ToString();
        context.Items["Store"] = await stores.FindAsync(storeCode, context.RequestAborted);
        await next(context);
    });
```

The application registers `builder.Services.AddProblemDetails()`. With that service available, the parameterless `app.UseExceptionHandler()` can handle an unhandled exception from the components inside it and use the registered problem details service to produce the response, provided the response has not started and content negotiation selects a representation a registered problem details writer supports. The ordinary test requests in this article send no `Accept` header, so the default writer responds with `application/problem+json`; one test sends `Accept: text/html` explicitly. Microsoft's [error-handling documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling#problem-details) describes this combination. Two limits are worth knowing, and a test in the appendix pins each one on .NET 10:

- The parameterless overload needs a fallback that can produce the response. In the tested .NET 10 configuration, it throws during startup unless an error-handling path, a handler lambda, or a problem details service is configured. Registering `PassThroughExceptionHandler`, an `IExceptionHandler` that declines every exception, without also calling `AddProblemDetails()` did not provide that fallback, and startup failed (`ParameterlessUseExceptionHandlerWithoutProblemDetailsFailsAtStartup`). The startup check looks for the fallback itself; what the registered handler would return never comes into it.
- Whether this configuration writes a problem details body depends on content negotiation and the registered writers. A request whose `Accept` header allows only `text/html` received a `500` with an empty body from the repaired pipeline (`RepairedOrderWritesNoProblemDetailsForAnHtmlOnlyClient`). The exception was still handled; only the body was missing.

For reasoning about pipeline boundaries, you can model the exception handler middleware as a `try`/`catch` around its call to `next`. The real middleware does more: it logs the exception, checks whether the response has started, runs any registered `IExceptionHandler` services, and, when configured with an error path, re-executes the request along that path. The model is still right about the boundary: it can catch only exceptions thrown inside its call to `next`. Draw both orders as boxes (authentication and authorization sit inside the store lookup in both, next to the endpoint, and are omitted here):

```text
Broken order                                  Repaired order

RequestLog                                    RequestLog
└─ StoreLookup          ← throws here         └─ ExceptionHandler
   └─ ExceptionHandler                           └─ StoreLookup      ← throws here
      └─ endpoint                                   └─ endpoint
```

In the broken order, the endpoint is still inside the handler, so when the endpoint throws, the handler catches the exception and writes problem details in its place. That is why the developer's manual check passed. The store lookup, however, is outside the handler. When it throws, the exception passes straight through the request log, which never reaches its "out" line, and leaves the application.

Microsoft's [server exception handling documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling#server-exception-handling) states that when the server catches an exception before the response has started, it sends a `500` response without a body. With Kestrel, the exception is also logged. A reverse proxy or gateway in front of the application can replace that response with its own. In this incident, that bodiless `500` is the response the clients' error parser rejected.

The tests make each path observable. The helper `TestApp.StartAsync` builds the application with whatever pipeline a test passes in; it is shown in the [testing section](#a-test-helper-that-builds-a-real-webapplication-pipeline). The test client sends a store header and a test user, and the fake store directory can be switched to "down".

```csharp
private static void BrokenOrder(WebApplication app)
{
    app.UseRequestLog();
    app.UseStoreLookup();
    app.UseExceptionHandler();   // moved: now inside the store lookup
    app.UseAuthentication();
    app.UseAuthorization();
}

private static void RepairedOrder(WebApplication app)
{
    app.UseRequestLog();
    app.UseExceptionHandler();   // wraps the store lookup and the endpoint
    app.UseStoreLookup();
    app.UseAuthentication();
    app.UseAuthorization();
}

[Fact]
public async Task BrokenOrderStillHandlesAnEndpointFailure()
{
    await using WebApplication app = await TestApp.StartAsync(BrokenOrder);

    HttpResponseMessage response = await app.CreateClient().GetAsync("/orders/0", TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    Assert.Equal(["RequestLog: in", "endpoint", "RequestLog: out 500"], app.Trace());
}

[Fact]
public async Task BrokenOrderLetsAStoreLookupFailureEscape()
{
    await using WebApplication app = await TestApp.StartAsync(BrokenOrder);
    app.Services.GetRequiredService<FakeStoreDirectory>().IsDown = true;

    await Assert.ThrowsAsync<TimeoutException>(
        () => app.CreateClient().GetAsync("/orders/42", TestContext.Current.CancellationToken));

    Assert.Equal(["RequestLog: in"], app.Trace());
}

[Fact]
public async Task RepairedOrderTurnsAStoreLookupFailureIntoProblemDetails()
{
    await using WebApplication app = await TestApp.StartAsync(RepairedOrder);
    app.Services.GetRequiredService<FakeStoreDirectory>().IsDown = true;

    HttpResponseMessage response = await app.CreateClient().GetAsync("/orders/42", TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    Assert.DoesNotContain("store directory", body, StringComparison.OrdinalIgnoreCase);
    Assert.Equal(["RequestLog: in", "RequestLog: out 500"], app.Trace());
}
```

Read the three traces together; they say which handler was reached. The store lookup writes no trace entries of its own, so it does not appear in them.

| Case | Observed through `TestServer` | Trace | Who handled the exception |
| --- | --- | --- | --- |
| Broken order, endpoint throws | `500`, `application/problem+json` | `in`, `endpoint`, `out 500` | The exception handler |
| Broken order, store lookup throws | The client call throws `TimeoutException`; no response is returned | `in` only | Nothing in the application |
| Repaired order, store lookup throws | `500`, `application/problem+json` | `in`, `out 500` | The exception handler |

`TestServer` rethrows an exception that escapes the application into the test, which is what `Assert.ThrowsAsync<TimeoutException>` observes. That is a feature for this test: it proves the exception left the pipeline unhandled, instead of inferring it from an empty response. The repaired test also checks that the exception's message does not appear in the body, because the default problem details response describes the failure generically rather than exposing it.

Notice where the request log sits in the repaired order. It is outside the handler, so when the handler turns the exception into a response, the log records the final `500`. If the log were inside the handler, the exception would pass through it on the way to the handler, and the log would never reach its "out" line. Moving the "out" entry into a `finally` block would make it run, but would not guarantee an accurate final status: while the exception is still unwinding, the outer exception handler has not yet turned it into its final response, so the status the inner log observes may not be the eventual `500`. A log that must record failures should record the exception itself when one passes through it, and should sit outside the exception handler when it must see the handler's final response.

The rule is not "the exception handler goes first". It is: **the exception handler must wrap every component whose failures it is expected to turn into a response.** In most applications that means near the start of the pipeline, which is where Microsoft's documentation places it. A component deliberately placed outside it, like this request log, should be simple enough not to throw.

## Authorization Depends on Endpoint Metadata and Caller Identity

The order endpoint requires an authenticated user:

```csharp
endpoints.MapGet("/orders/{id:int}", (int id, HttpContext context, RequestTrace trace) =>
{
    // ...
})
.RequireAuthorization();
```

Three middleware components cooperate to enforce that, and each has one job:

| Component | Responsibility | What it produces |
| --- | --- | --- |
| Routing (`UseRouting`) | Matches the URL to an endpoint | The selected endpoint and its metadata, including `.RequireAuthorization()` |
| Authentication (`UseAuthentication`) | Reads credentials, such as a cookie or bearer token | `HttpContext.User`, the identity of the caller |
| Authorization (`UseAuthorization`) | Decides whether this caller may use this endpoint | Lets the request continue, or triggers a challenge or forbid through the configured authentication service and scheme. The tests here produce only challenges, which this test scheme answers with `401`; a forbid normally produces `403`, and a cookie scheme, for example, can redirect instead |

In the conventional pipeline used here, authorization consumes what routing and authentication have already established: it needs the selected endpoint's metadata to know which policy applies, and it needs the caller on `HttpContext.User` to evaluate that policy. If it runs before either one, it is deciding without the information the decision depends on.

### Authorization before authentication

Here is the broken order. Every required call is present, but the order is wrong:

```csharp
app.UseAuthorization();
app.UseAuthentication();
```

In this sample, a request carrying the otherwise valid test user `alice` now receives `401 Unauthorized`. Authorization runs first and finds the endpoint's requirement for an authenticated user. The default policy names no authentication scheme, so authorization evaluates whatever identity is already on `HttpContext.User`, and that identity is still anonymous because authentication has not run. Authorization challenges, and the request never reaches the endpoint. Authentication would have recognized `alice`, but it is downstream of a component that has already answered. (A policy that names its authentication schemes asks those schemes to authenticate during authorization, so with such a policy this particular mistake can be masked.)

Swapping the two lines repairs it. The tests pin both outcomes, and a third confirms that the repaired order still rejects a caller with no credentials, so the fix does not simply turn authorization off:

```csharp
[Fact]
public async Task AuthorizationBeforeAuthenticationRejectsAValidUser()
{
    await using WebApplication app = await TestApp.StartAsync(app =>
    {
        app.UseAuthorization();
        app.UseAuthentication();
    });

    HttpResponseMessage response = await app.CreateClient(user: "alice").GetAsync("/orders/42", TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.Empty(app.Trace());
}

[Fact]
public async Task AuthenticationBeforeAuthorizationAdmitsTheUser()
{
    await using WebApplication app = await TestApp.StartAsync(app =>
    {
        app.UseAuthentication();
        app.UseAuthorization();
    });

    HttpResponseMessage response = await app.CreateClient(user: "alice").GetAsync("/orders/42", TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    OrderSummary? order = await response.Content.ReadFromJsonAsync<OrderSummary>(TestContext.Current.CancellationToken);
    Assert.Equal("alice", order?.ViewedBy);
}

[Fact]
public async Task AuthenticationBeforeAuthorizationStillRejectsAnonymousCallers()
{
    await using WebApplication app = await TestApp.StartAsync(app =>
    {
        app.UseAuthentication();
        app.UseAuthorization();
    });

    HttpResponseMessage response = await app.CreateClient(user: null).GetAsync("/orders/42", TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.Empty(app.Trace());
}
```

`Assert.Empty(app.Trace())` checks the other half of the claim: a rejected request never reached the endpoint.

### Authorization before routing

The other dependency, endpoint metadata, fails differently. If authorization runs before routing has selected an endpoint, it cannot see the endpoint's authorization metadata, so it has no endpoint-specific policy to apply:

```csharp
app.UseAuthentication();
app.UseAuthorization();   // ASP0001: runs before an endpoint is selected
app.UseRouting();
```

Two different mechanisms report this mistake:

- **At build time,** the ASP.NET Core analyzer warns with [ASP0001](https://learn.microsoft.com/en-us/aspnet/core/diagnostics/asp0001) because `UseAuthorization` is not between routing and endpoint execution. The companion test has to suppress the warning to build at all under warnings-as-errors.
- **At run time,** observed on .NET 10, the endpoint middleware notices that the selected endpoint carries authorization metadata that no authorization middleware evaluated, and throws an `InvalidOperationException` instead of running the endpoint.

In this tested configuration the mistake fails closed: the order is not returned. Do not rely on that as a safety net. ASP0001's documentation notes that when authorization is out of place, a configured fallback policy is used to authorize all requests, and resources that are not endpoints can behave differently. Fix the analyzer warning rather than suppressing it.

## What WebApplication Adds for You

The cases so far are about the basic wrapping model. The next four are about the hosting model that builds the final pipeline around your registrations. Some test pipelines in this article are only a line or two long, yet authorization still ran. That is because the `WebApplication` hosting model adds some middleware on its own. Microsoft's [middleware documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/#middleware-added-automatically-by-webapplication) lists it. The table describes the .NET 10 behavior this sample was tested with; where authentication and authorization go when you call `UseRouting()` yourself depends on the version, as described below.

| Middleware | Added automatically when | Where |
| --- | --- | --- |
| Developer exception page | The environment is `Development` | First |
| `UseRouting` | Endpoints are mapped and your code did not call `UseRouting` | Next |
| `UseAuthentication` | `AddAuthentication` registered its services and your code did not call `UseAuthentication` | After the automatic routing, before your middleware (see the `UseRouting` case below for .NET 10) |
| `UseAuthorization` | `AddAuthorization` registered its services and your code did not call `UseAuthorization` | After the automatic authentication, before your middleware |
| Your middleware | Always | In the order you register it |
| Endpoint execution (`UseEndpoints`) | Endpoints are mapped | After your middleware; this is where a matched endpoint runs |

In the .NET 10 pipelines shown here, when your code does not call `UseRouting()` itself, the automatically supplied developer exception page, routing, authentication, and authorization run before the middleware you register, and automatic endpoint execution comes after it. That placement has four consequences for the pipelines in this article.

**An omitted call can be supplied automatically.** If you call only `app.UseAuthorization()` and never call `app.UseAuthentication()`, `WebApplication` adds authentication itself, because the test helper registered authentication services. It places authentication after the automatic routing, ahead of your explicitly registered authorization call. The request from `alice` succeeds. A test in the appendix pins that behavior, `OmittedUseAuthenticationIsSuppliedByWebApplication`. The framework is not detecting your mistake; it is filling in a component you did not place, in its conventional position. The broken example above reproduces only because both calls are explicit. Each explicit call fixes the position of that one component; a component whose call you omit can still be added automatically.

**Automatic authorization runs before your own middleware.** In the earlier sections, every pipeline that should observe rejections calls `UseAuthentication()` and `UseAuthorization()` explicitly after the request log. Leave them out and the automatic versions run first, outside the log, so a rejected request never reaches the log at all:

```csharp
[Fact]
public async Task AutomaticAuthorizationRunsBeforeTheRequestLog()
{
    await using WebApplication app = await TestApp.StartAsync(app => app.UseRequestLog());

    HttpResponseMessage response = await app.CreateClient(user: null).GetAsync("/orders/42", TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.Empty(app.Trace());
}

[Fact]
public async Task ExplicitAuthorizationAfterTheLogLetsTheLogRecordRejections()
{
    await using WebApplication app = await TestApp.StartAsync(app =>
    {
        app.UseRequestLog();
        app.UseAuthentication();
        app.UseAuthorization();
    });

    HttpResponseMessage response = await app.CreateClient(user: null).GetAsync("/orders/42", TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.Equal(["RequestLog: in", "RequestLog: out 401"], app.Trace());
}
```

By the same placement rule, automatically added authentication and authorization also run outside an exception handler you register; no test in this article checks that case separately.

**On .NET 10, calling `UseRouting` yourself moves routing, but not the automatic authorization.** Unless you call `UseRouting`, route matching happens before any middleware you register, so even your first component can read the selected endpoint with `context.GetEndpoint()`. If you call `UseRouting` explicitly, routing runs at that position instead, which is useful when middleware must run before route matching. On .NET 10, `WebApplication` places the automatic authentication and authorization while it assembles the outer pipeline, ahead of all of your registrations, and your explicit routing call does not move them. In this article's .NET 10 test, they stayed ahead of the explicit `UseRouting()` call, which produced the authorization-before-routing failure from the previous section even though the application contained no authorization call:

```csharp
[Fact]
public async Task ExplicitUseRoutingAloneLeavesAutomaticAuthorizationBeforeRouting()
{
    await using WebApplication app = await TestApp.StartAsync(app => app.UseRouting());

    InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
        () => app.CreateClient(user: "alice").GetAsync("/orders/42", TestContext.Current.CancellationToken));

    Assert.Contains("authorization metadata", exception.Message, StringComparison.Ordinal);
}
```

If you call `UseRouting` explicitly, call `UseAuthentication` and `UseAuthorization` explicitly after it as well. Automatic placement is version-sensitive. In the ASP.NET Core 11.0.0 [`WebApplicationBuilder` source](https://github.com/dotnet/aspnetcore/blob/1def4cdc07dc10fc267136060e3ef5c1a96d4a99/src/DefaultBuilder/src/WebApplicationBuilder.cs#L516-L524), the code that assembles the pipeline defers the automatic authentication and authorization until after an explicit `UseRouting()` call; a source comment there explains that they must observe the matched endpoint. That version was not run as part of this article's tests. Keep a test like this one when you upgrade, so a placement change shows up as a failing test rather than a production surprise.

**The development environment can mask a missing application exception boundary.** In a `Development` `WebApplication` with the default hosting configuration, the developer exception page is added at the start of the application's middleware pipeline. The store-lookup failure from the opening would escape the application's own exception handler and be caught by the developer exception page instead, which turns it into a detailed error page on a developer's machine. Locally the failure looks covered; in production the same request produces a bodiless `500`. The tests in this article set the environment to `Production` explicitly for that reason.

Whether to rely on the automatic placement or write every call yourself is a judgment call. The automatic defaults are reasonable for many minimal APIs. Writing the calls out makes the order visible in review, and it is required when you need something between them, such as CORS, which the documentation says must run before authentication and authorization. Either way, test the behavior you depend on rather than the lines you wrote.

## Testing the Order You Depend On

Comments such as `// must stay before UseAuthorization` are easy to miss in a diff. An integration test that sends a request through a real pipeline and asserts what happened is much harder to break by accident.

### A test helper that builds a real WebApplication pipeline

The tests build a real `WebApplication`, including its automatic middleware, and host it with an in-memory `TestServer` rather than Kestrel:

```csharp
public static async Task<WebApplication> StartAsync(Action<WebApplication> configurePipeline)
{
    var builder = WebApplication.CreateBuilder(
        new WebApplicationOptions { EnvironmentName = Environments.Production });

    builder.WebHost.UseTestServer();
    builder.Logging.ClearProviders();

    builder.Services.AddSingleton<RequestTrace>();
    builder.Services.AddSingleton<MaintenanceMode>();
    builder.Services.AddSingleton<FakeStoreDirectory>();
    builder.Services.AddSingleton<IStoreDirectory>(sp => sp.GetRequiredService<FakeStoreDirectory>());
    builder.Services.AddProblemDetails();
    builder.Services.AddAuthentication(TestAuthHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
    builder.Services.AddAuthorization();

    WebApplication app = builder.Build();
    configurePipeline(app);
    app.MapOrders();

    await app.StartAsync();
    return app;
}
```

Three choices keep the evidence honest:

- **The environment is `Production`,** so the developer exception page cannot catch what your own handler misses.
- **Authentication is deterministic.** `TestAuthHandler` treats the `X-Test-User` header as the caller's name and returns "no result" when it is absent. No identity provider, network, or token signing takes part, so a `401` comes from the local authentication and authorization configuration, never from an unavailable token server. With the valid `alice` header, a `401` exposes an ordering defect; without a header, it is the expected rejection. Never register a handler like this in a real application; it trusts a header any client can send.
- **Each test builds its own application,** so one test's trace or maintenance switch cannot leak into another.

This helper takes the pipeline as a parameter so that one test class can compare orders. For your own application, test the pipeline you ship: start it with `WebApplicationFactory<Program>` from `Microsoft.AspNetCore.Mvc.Testing` and replace only the authentication scheme and the external dependencies. With top-level statements, the compiler's `Program` class is internal. ASP.NET Core 10 generates a public declaration for it, as the [ASP0027 analyzer documentation](https://learn.microsoft.com/en-us/aspnet/core/diagnostics/asp0027) explains; on earlier versions, add `public partial class Program { }` to the application, or make its internals visible to the test project.

### Asserting the trace, not the timing

The trace tests compare the exact sequence of events for a completed request and a short-circuited one:

```csharp
[Fact]
public async Task CompletedRequestEntersInOrderAndLeavesInReverse()
{
    await using WebApplication app = await TestApp.StartAsync(app =>
    {
        app.UseRequestLog();
        app.UseMaintenanceGate();
        app.UseAuthentication();
        app.UseAuthorization();
    });

    HttpResponseMessage response = await app.CreateClient().GetAsync("/orders/42", TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(
        ["RequestLog: in", "Maintenance: in", "endpoint", "Maintenance: out", "RequestLog: out 200"],
        app.Trace());
}

[Fact]
public async Task ShortCircuitedRequestNeverReachesTheEndpoint()
{
    await using WebApplication app = await TestApp.StartAsync(app =>
    {
        app.UseRequestLog();
        app.UseMaintenanceGate();
        app.UseAuthentication();
        app.UseAuthorization();
    });
    app.Services.GetRequiredService<MaintenanceMode>().IsOn = true;

    HttpResponseMessage response = await app.CreateClient().GetAsync("/orders/42", TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    Assert.Equal(
        ["RequestLog: in", "Maintenance: in", "Maintenance: answered 503", "Maintenance: out", "RequestLog: out 503"],
        app.Trace());
}
```

These assertions are about order, not about time. They contain no delays, stopwatches, or elapsed-time thresholds, so differences in build-agent speed do not change the expected sequence. These tests use `GetAsync` with its default completion option, which buffers the whole response body before returning. With `TestServer`, the body completed only after the pipeline finished, so every "out" entry had been recorded before the test read the trace. The suite produced the same traces on each of three consecutive runs.

### What these tests deliberately do not assert

- They do not assert the full list of middleware in the pipeline. A test that fails whenever anyone adds unrelated middleware teaches people to update the test without reading it. Assert the behavior that depends on order: which component saw the request, which handler produced the response, and whether the endpoint ran.
- They do not assert the exact text of the problem details body beyond what clients depend on, here the status code and media type, plus the absence of the exception's message.
- They do not test Kestrel's answer to an escaped exception. `TestServer` surfaces the exception instead, which is the more precise evidence that nothing in the application handled it.

## Four Questions Before You Move a Registration

When a registration moves, or when an endpoint starts misbehaving after a pipeline change, ask these four questions about the component that moved and about its neighbors:

1. **What does it need from earlier components?** Authorization needs the selected endpoint and the caller's identity. A component that reads `context.GetEndpoint()` needs routing. A component that reads `context.User` needs authentication. A component that reads the store from `context.Items` needs the store lookup. If any of those producers would now run later, the component is reading state that does not exist yet.
2. **What does it wrap?** Everything registered after it runs inside its call to `next`, if it makes that call. An exception handler catches only what is inside it. A request log can record only the requests and responses that pass through its boundary. Moving a component down shrinks what it wraps; moving it up enlarges it. Account for automatic middleware too: the developer exception page, routing, authentication, and authorization can run before your first registration, while automatic endpoint execution runs after your last.
3. **Does it always call `next`?** Many components do not: static files, health-check handling, CORS preflight handling, authorization challenges and forbids, rate limiters, response caches, and gates like the maintenance check can all answer the request themselves. Anything registered after a short-circuiting component never sees the requests it answers.
4. **What response state can it still change?** A component can change the status code and headers only until the response starts, that is, until the headers are sent and `Response.HasStarted` becomes `true`. A component that sets a header after the endpoint has written its body is too late, and a component that catches an exception after the response has started can no longer replace it with an error response.

These questions do not produce one universal pipeline. Different applications reasonably order logging, CORS, rate limiting, and forwarded headers differently, depending on what each component needs and what it must see. They do tell you, for one specific move, what the move changes.

## Continue Deeper

This article follows one endpoint through a few moves. [Middleware Ordering Changes Behavior](../../aspnetcore/middleware-ordering-changes-behavior.md) treats the whole pipeline as a dependency graph and covers the categories this article leaves out, including forwarded headers behind a reverse proxy, CORS, rate limiting, and response middleware, together with a representative ordered pipeline and a companion sample.

Placing the exception handler correctly decides whether failures become a consistent error response. [Centralized Error Handling and Problem Details](../../aspnetcore/centralized-error-handling-and-problem-details.md) covers what that response should contain, how to map known failures to status codes, and how to keep internal details out of the public error boundary.

### Related Work

- [ASP.NET Core Middleware](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/), in the ASP.NET Core documentation, describes request and reverse response order, short-circuiting and terminal middleware, the recommended order of built-in middleware, and the middleware `WebApplication` adds automatically.
- [Routing in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/routing), in the ASP.NET Core documentation, explains how `WebApplication` wraps your middleware with `UseRouting` and `UseEndpoints`, why authentication and authorization run between them, and how calling `UseRouting` explicitly changes what runs before route matching.
- [Handle errors in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling), in the ASP.NET Core documentation, covers the exception handler middleware, problem details and their content negotiation, server exception handling, and the developer exception page that `WebApplication` enables in `Development`.
- [ASP0001: Authorization middleware is incorrectly configured](https://learn.microsoft.com/en-us/aspnet/core/diagnostics/asp0001), in the ASP.NET Core analyzer documentation, explains the warning for `UseAuthorization` outside the routing and endpoint middleware and its effect on a fallback policy.
- [Integration tests in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests), in the ASP.NET Core documentation, introduces `TestServer`, `WebApplicationFactory`, and test authentication handlers.
- [RFC 9457: Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc9457) defines the problem details representation for HTTP APIs and the `application/problem+json` media type the exception handler produces.

---

## The Short Answer

Middleware is not a list of filters that each look at a request independently. A component that calls `next` wraps everything registered after it: it runs before that work on the way in and after it on the way out. A component can also answer the request without calling `next` at all. Moving a registration changes what the component can see and what it can catch.

An exception handler catches only what runs inside it, so it must wrap every component whose failures should become an error response. Endpoint-specific authorization needs the endpoint that routing selected, so routing must run first. Place authentication before authorization so that middleware depending on the caller sees a consistently established identity. Policies that explicitly name authentication schemes can also authenticate during authorization, so the reversed-order failure shown here is not universal. `WebApplication` can add routing, authentication, authorization, and, in `Development`, the developer exception page ahead of your own middleware, which can supply a call you omitted or hide a mistake you made. Ask the four questions before moving a line, and pin the behavior you depend on with integration tests that run in `Production`, use deterministic authentication, and assert the sequence of events rather than their timing.

---

## Appendix: Complete Code

The sample has two projects: an ASP.NET Core application project and a test project. Every file below was compiled and run as shown.

### Environment used for verification

- .NET SDK 10.0.404, target framework `net10.0`, on Windows x64.
- xUnit v3 4.0.3, `Microsoft.AspNetCore.TestHost` 10.0.15, `Microsoft.NET.Test.Sdk` 18.12.0, and `xunit.runner.visualstudio` 4.0.1. `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` support VSTest-based runners such as Visual Studio's Test Explorer; the recorded run used Microsoft.Testing.Platform, which `global.json` selects for `dotnet test` on the .NET 10 SDK.
- The recorded run used SDK 10.0.404. `global.json` permits any .NET 10 SDK from feature band 10.0.100 onward rather than requiring that exact SDK.
- Warnings treated as errors, with the `latest-recommended` .NET analyzers.
- The command `dotnet test --project Tests/OrderApi.Tests.csproj`, run from the folder that contains `global.json`, built both projects and reported 16 passing tests, with the same result on three consecutive runs.

### Project files

```text
global.json
App/OrderApi.csproj
App/OrderApi.cs
Tests/OrderApi.Tests.csproj
Tests/Usings.cs
Tests/TestSupport.cs
Tests/PipelineOrderTests.cs
```

`global.json`:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

`App/OrderApi.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Library</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

</Project>
```

`Tests/OrderApi.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.TestHost" Version="10.0.15" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.12.0" />
    <PackageReference Include="xunit.v3" Version="4.0.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="4.0.1">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <ProjectReference Include="../App/OrderApi.csproj" />
  </ItemGroup>

</Project>
```

`Tests/Usings.cs`:

```csharp
global using Xunit;
```

The application project is a library here because the tests start it themselves. In a real application, `Program.cs` would register `AddProblemDetails()`, `AddAuthorization()`, a real `IStoreDirectory` implementation, and a production authentication scheme, never `TestAuthHandler`, and then build one pipeline, for example `UseRequestLog`, `UseExceptionHandler`, `UseStoreLookup`, `UseAuthentication`, and `UseAuthorization`, followed by `MapOrders`. That order is illustrative: where CORS, forwarded headers, HTTPS redirection, static files, or rate limiting belong depends on your application.

### Application code

`App/OrderApi.cs`:

```csharp
using System.Collections.Concurrent;

namespace OrderApi;

public sealed class RequestTrace
{
    private readonly ConcurrentQueue<string> _events = new();

    public void Add(string message) => _events.Enqueue(message);

    public IReadOnlyList<string> Events => [.. _events];
}

public sealed class MaintenanceMode
{
    public bool IsOn { get; set; }
}

public interface IStoreDirectory
{
    Task<string> FindAsync(string storeCode, CancellationToken cancellationToken);
}

public sealed record OrderSummary(int Id, string? Store, string ViewedBy);

public static class OrderPipeline
{
    // Tracer A: records the request on the way in, and the status code on the way out.
    public static IApplicationBuilder UseRequestLog(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var trace = context.RequestServices.GetRequiredService<RequestTrace>();
            trace.Add("RequestLog: in");
            await next(context);
            trace.Add($"RequestLog: out {context.Response.StatusCode}");
        });

    // Tracer B: answers 503 itself, without calling next, while maintenance mode is on.
    public static IApplicationBuilder UseMaintenanceGate(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var trace = context.RequestServices.GetRequiredService<RequestTrace>();
            trace.Add("Maintenance: in");

            if (context.RequestServices.GetRequiredService<MaintenanceMode>().IsOn)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                trace.Add("Maintenance: answered 503");
            }
            else
            {
                await next(context);
            }

            trace.Add("Maintenance: out");
        });

    // Looks up the caller's store before the endpoint runs. The lookup can throw.
    public static IApplicationBuilder UseStoreLookup(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var stores = context.RequestServices.GetRequiredService<IStoreDirectory>();
            string storeCode = context.Request.Headers["X-Store"].ToString();
            context.Items["Store"] = await stores.FindAsync(storeCode, context.RequestAborted);
            await next(context);
        });

    public static IEndpointRouteBuilder MapOrders(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/orders/{id:int}", (int id, HttpContext context, RequestTrace trace) =>
        {
            trace.Add("endpoint");

            if (id == 0)
            {
                throw new InvalidOperationException("Order 0 cannot be loaded.");
            }

            string viewedBy = context.User.Identity?.Name ?? "(unnamed)";
            return TypedResults.Ok(new OrderSummary(id, context.Items["Store"] as string, viewedBy));
        })
        .RequireAuthorization();

        return endpoints;
    }
}
```

### Test support

`Tests/TestSupport.cs`:

```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderApi;

namespace OrderApi.Tests;

// Test-only: trusts a header that any client can send. Never register this in an application.
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string user = Request.Headers[UserHeader].ToString();

        if (user.Length == 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, user)], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public sealed class FakeStoreDirectory : IStoreDirectory
{
    public bool IsDown { get; set; }

    public Task<string> FindAsync(string storeCode, CancellationToken cancellationToken) =>
        IsDown
            ? throw new TimeoutException("The store directory did not answer.")
            : Task.FromResult(storeCode);
}

// Declines every exception, so it never produces a response itself.
public sealed class PassThroughExceptionHandler : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(false);
}

public static class TestApp
{
    public static async Task<WebApplication> StartAsync(Action<WebApplication> configurePipeline)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = Environments.Production });

        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<RequestTrace>();
        builder.Services.AddSingleton<MaintenanceMode>();
        builder.Services.AddSingleton<FakeStoreDirectory>();
        builder.Services.AddSingleton<IStoreDirectory>(sp => sp.GetRequiredService<FakeStoreDirectory>());
        builder.Services.AddProblemDetails();
        builder.Services.AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
        builder.Services.AddAuthorization();

        WebApplication app = builder.Build();
        configurePipeline(app);
        app.MapOrders();

        await app.StartAsync();
        return app;
    }

    public static HttpClient CreateClient(this WebApplication app, string? user = "alice")
    {
        HttpClient client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Store", "north");

        if (user is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, user);
        }

        return client;
    }

    public static IReadOnlyList<string> Trace(this WebApplication app) =>
        app.Services.GetRequiredService<RequestTrace>().Events;
}
```

### Tests

`Tests/PipelineOrderTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderApi;

namespace OrderApi.Tests;

public sealed class TraceTests
{
    [Fact]
    public async Task CompletedRequestEntersInOrderAndLeavesInReverse()
    {
        await using WebApplication app = await TestApp.StartAsync(app =>
        {
            app.UseRequestLog();
            app.UseMaintenanceGate();
            app.UseAuthentication();
            app.UseAuthorization();
        });

        HttpResponseMessage response = await app.CreateClient().GetAsync("/orders/42", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            ["RequestLog: in", "Maintenance: in", "endpoint", "Maintenance: out", "RequestLog: out 200"],
            app.Trace());
    }

    [Fact]
    public async Task ShortCircuitedRequestNeverReachesTheEndpoint()
    {
        await using WebApplication app = await TestApp.StartAsync(app =>
        {
            app.UseRequestLog();
            app.UseMaintenanceGate();
            app.UseAuthentication();
            app.UseAuthorization();
        });
        app.Services.GetRequiredService<MaintenanceMode>().IsOn = true;

        HttpResponseMessage response = await app.CreateClient().GetAsync("/orders/42", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(
            ["RequestLog: in", "Maintenance: in", "Maintenance: answered 503", "Maintenance: out", "RequestLog: out 503"],
            app.Trace());
    }

    [Fact]
    public async Task GateMovedAboveLogHidesShortCircuitedRequestsFromTheLog()
    {
        await using WebApplication app = await TestApp.StartAsync(app =>
        {
            app.UseMaintenanceGate();
            app.UseRequestLog();
            app.UseAuthentication();
            app.UseAuthorization();
        });
        app.Services.GetRequiredService<MaintenanceMode>().IsOn = true;

        HttpResponseMessage response = await app.CreateClient().GetAsync("/orders/42", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(["Maintenance: in", "Maintenance: answered 503", "Maintenance: out"], app.Trace());
    }
}

public sealed class ExceptionHandlerPlacementTests
{
    private static void BrokenOrder(WebApplication app)
    {
        app.UseRequestLog();
        app.UseStoreLookup();
        app.UseExceptionHandler();   // moved: now inside the store lookup
        app.UseAuthentication();
        app.UseAuthorization();
    }

    private static void RepairedOrder(WebApplication app)
    {
        app.UseRequestLog();
        app.UseExceptionHandler();   // wraps the store lookup and the endpoint
        app.UseStoreLookup();
        app.UseAuthentication();
        app.UseAuthorization();
    }

    [Fact]
    public async Task BrokenOrderStillHandlesAnEndpointFailure()
    {
        await using WebApplication app = await TestApp.StartAsync(BrokenOrder);

        HttpResponseMessage response = await app.CreateClient().GetAsync("/orders/0", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(["RequestLog: in", "endpoint", "RequestLog: out 500"], app.Trace());
    }

    [Fact]
    public async Task BrokenOrderLetsAStoreLookupFailureEscape()
    {
        await using WebApplication app = await TestApp.StartAsync(BrokenOrder);
        app.Services.GetRequiredService<FakeStoreDirectory>().IsDown = true;

        await Assert.ThrowsAsync<TimeoutException>(
            () => app.CreateClient().GetAsync("/orders/42", TestContext.Current.CancellationToken));

        Assert.Equal(["RequestLog: in"], app.Trace());
    }

    [Fact]
    public async Task RepairedOrderTurnsAStoreLookupFailureIntoProblemDetails()
    {
        await using WebApplication app = await TestApp.StartAsync(RepairedOrder);
        app.Services.GetRequiredService<FakeStoreDirectory>().IsDown = true;

        HttpResponseMessage response = await app.CreateClient().GetAsync("/orders/42", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("store directory", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["RequestLog: in", "RequestLog: out 500"], app.Trace());
    }

    [Fact]
    public async Task RepairedOrderWritesNoProblemDetailsForAnHtmlOnlyClient()
    {
        await using WebApplication app = await TestApp.StartAsync(RepairedOrder);
        app.Services.GetRequiredService<FakeStoreDirectory>().IsDown = true;
        HttpClient client = app.CreateClient();
        client.DefaultRequestHeaders.Add("Accept", "text/html");

        HttpResponseMessage response = await client.GetAsync("/orders/42", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["RequestLog: in", "RequestLog: out 500"], app.Trace());
    }

    [Fact]
    public async Task ParameterlessUseExceptionHandlerWithoutProblemDetailsFailsAtStartup()
    {
        // The startup check requires a fallback response (an error path, a handler lambda, or problem details).
        // A registered IExceptionHandler is not that fallback, whatever it returns.
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddExceptionHandler<PassThroughExceptionHandler>();   // no AddProblemDetails()

        await using WebApplication app = builder.Build();
        app.UseExceptionHandler();
        app.MapGet("/", () => "ok");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => app.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("ExceptionHandlingPath", exception.Message, StringComparison.Ordinal);
    }
}

public sealed class AuthorizationOrderTests
{
    [Fact]
    public async Task AuthorizationBeforeAuthenticationRejectsAValidUser()
    {
        await using WebApplication app = await TestApp.StartAsync(app =>
        {
            app.UseAuthorization();
            app.UseAuthentication();
        });

        HttpResponseMessage response = await app.CreateClient(user: "alice").GetAsync("/orders/42", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(app.Trace());
    }

    [Fact]
    public async Task AuthenticationBeforeAuthorizationAdmitsTheUser()
    {
        await using WebApplication app = await TestApp.StartAsync(app =>
        {
            app.UseAuthentication();
            app.UseAuthorization();
        });

        HttpResponseMessage response = await app.CreateClient(user: "alice").GetAsync("/orders/42", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        OrderSummary? order = await response.Content.ReadFromJsonAsync<OrderSummary>(TestContext.Current.CancellationToken);
        Assert.Equal("alice", order?.ViewedBy);
    }

    [Fact]
    public async Task AuthenticationBeforeAuthorizationStillRejectsAnonymousCallers()
    {
        await using WebApplication app = await TestApp.StartAsync(app =>
        {
            app.UseAuthentication();
            app.UseAuthorization();
        });

        HttpResponseMessage response = await app.CreateClient(user: null).GetAsync("/orders/42", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(app.Trace());
    }

    [Fact]
    public async Task AuthorizationBeforeRoutingFailsClosedWithAnException()
    {
        await using WebApplication app = await TestApp.StartAsync(app =>
        {
            app.UseAuthentication();
#pragma warning disable ASP0001 // Deliberately broken: authorization runs before an endpoint is selected.
            app.UseAuthorization();
#pragma warning restore ASP0001
            app.UseRouting();
        });

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => app.CreateClient(user: "alice").GetAsync("/orders/42", TestContext.Current.CancellationToken));

        Assert.Contains("authorization metadata", exception.Message, StringComparison.Ordinal);
        Assert.Empty(app.Trace());
    }
}

public sealed class AutomaticMiddlewareTests
{
    [Fact]
    public async Task OmittedUseAuthenticationIsSuppliedByWebApplication()
    {
        await using WebApplication app = await TestApp.StartAsync(app => app.UseAuthorization());

        HttpResponseMessage response = await app.CreateClient(user: "alice").GetAsync("/orders/42", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AutomaticAuthorizationRunsBeforeTheRequestLog()
    {
        await using WebApplication app = await TestApp.StartAsync(app => app.UseRequestLog());

        HttpResponseMessage response = await app.CreateClient(user: null).GetAsync("/orders/42", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(app.Trace());
    }

    [Fact]
    public async Task ExplicitAuthorizationAfterTheLogLetsTheLogRecordRejections()
    {
        await using WebApplication app = await TestApp.StartAsync(app =>
        {
            app.UseRequestLog();
            app.UseAuthentication();
            app.UseAuthorization();
        });

        HttpResponseMessage response = await app.CreateClient(user: null).GetAsync("/orders/42", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(["RequestLog: in", "RequestLog: out 401"], app.Trace());
    }

    [Fact]
    public async Task ExplicitUseRoutingAloneLeavesAutomaticAuthorizationBeforeRouting()
    {
        await using WebApplication app = await TestApp.StartAsync(app => app.UseRouting());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => app.CreateClient(user: "alice").GetAsync("/orders/42", TestContext.Current.CancellationToken));

        Assert.Contains("authorization metadata", exception.Message, StringComparison.Ordinal);
    }
}
```
