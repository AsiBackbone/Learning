---
description: Learn how ASP.NET Core middleware order changes request coverage, response behavior, exception handling, authentication, authorization, and security boundaries.
---

# Middleware Ordering Changes Behavior

**Pattern classification:** Canonical Pattern

**Difficulty:** Intermediate

**Prerequisites:** Basic familiarity with ASP.NET Core request handling and the [ASP.NET Core learning area](index.md)

**Learning objective:** Understand middleware as an ordered request/response pipeline, identify order-dependent behavior, predict which middleware can observe or control a request, and explain why a pipeline change can be a behavior or security change rather than a cosmetic refactor.

## Pattern Card

> **Problem:** The same middleware registrations can behave differently when their order changes because middleware can depend on earlier request state, wrap later behavior, short-circuit the request, or modify the response on the way back out.
>
> **Pattern:** Order middleware according to explicit dependencies and coverage goals. Place request-state producers before consumers, place wrappers before the behavior they must observe or catch, and place endpoint-aware middleware after routing has selected endpoint metadata.
>
> **Use when:** An ASP.NET Core application combines cross-cutting concerns such as proxy correction, logging, exception handling, security headers, routing, CORS, rate limiting, authentication, authorization, and endpoint execution.
>
> **Prefer something simpler when:** A very small application has only a few middleware components and no special ordering dependencies. The pipeline is still ordered, but it may not need a separate abstraction or ADR.
>
> **Observe:** Requests move through middleware in registration order, responses unwind through participating middleware in reverse order, and a short-circuit can prevent later middleware or endpoints from running at all.

## Start with the Two-Direction Model

ASP.NET Core middleware is not just a list of independent filters.

Each component can run code:

1. Before the next component.
2. Instead of the next component.
3. After the next component returns.

A simple pipeline looks like this:

```text
Request
  ↓
Middleware A — before
  ↓
Middleware B — before
  ↓
Endpoint
  ↑
Middleware B — after
  ↑
Middleware A — after
  ↑
Response
```

The request travels inward in registration order.

The response unwinds through the middleware that participated in the request, in reverse order.

That means placement controls both **what a middleware knows on the way in** and **what it can observe or change on the way out**.

## Short-Circuiting Changes the Reachable Pipeline

Middleware does not have to call the next component.

A middleware can decide that the request is complete:

```text
Request
  ↓
Middleware A
  ↓
Middleware B
  ↓
Short-circuit response
  ✕
Middleware C never runs
  ✕
Endpoint never runs
```

This is normal behavior for components such as:

- Static-file handling.
- Authorization failures.
- Rate-limit rejection.
- Authentication challenges.
- Custom maintenance or feature-gate middleware.
- Terminal middleware.

The architectural question is therefore not only:

> Which middleware is registered?

It is also:

> Which middleware is guaranteed to run before a possible short-circuit, and which middleware may never be reached?

## Treat Ordering as a Dependency Graph

A useful way to reason about middleware is to ask what each component **produces**, what it **consumes**, and what it must **wrap**.

Examples:

| Middleware concern | Depends on | Why placement matters |
| --- | --- | --- |
| Forwarded headers | Trusted proxy configuration | Downstream components should see the corrected scheme, host, and client address. |
| Request logging | Corrected request identity; desired coverage | Early placement sees more failures and short-circuits; later placement sees less. |
| Exception handling | Must precede code it is expected to catch | It can only catch exceptions thrown by later middleware or endpoints that execute inside its downstream call. |
| Security response headers | Must precede response producers it should cover | A response produced before the header middleware is reached may bypass those headers. |
| Routing | Request path and routing configuration | It selects endpoint metadata used by later endpoint-aware middleware. |
| CORS | Routing when endpoint metadata is used | It should run after routing and before authentication/authorization in the common endpoint-routing arrangement. |
| Endpoint-specific rate limiting | Routing metadata | Endpoint policies cannot be selected before routing has identified the endpoint. |
| Authentication | Authentication services and request credentials | It establishes the user principal used by later authorization. |
| Authorization | Authenticated principal and endpoint/resource metadata | It should evaluate the intended principal and the intended endpoint policy. |
| Endpoint execution | All required upstream controls | It is the point where the application actually handles the request. |

This dependency view is more durable than memorizing one giant list.

## Deliberately Incorrect Pipeline: Authorization Before Routing

Consider an explicitly ordered endpoint-routing pipeline:

```csharp
app.UseAuthorization();

app.UseRouting();

app.MapControllers();
```

The problem is not formatting.

Authorization runs before routing has selected endpoint metadata.

For endpoint-specific authorization, that means authorization cannot evaluate the selected endpoint in the normal way. ASP.NET Core analyzer rule `ASP0001` specifically calls out this ordering problem.

The corrected relationship is:

```csharp
app.UseRouting();

app.UseAuthorization();

app.MapControllers();
```

The important invariant is:

```text
Route selection
   ↓
Endpoint metadata available
   ↓
Endpoint-aware authorization
```

not:

```text
Authorization guesses first
   ↓
Routing happens later
```

## Deliberately Incorrect Pipeline: Authorization Before Authentication

Another common ordering mistake is:

```csharp
app.UseAuthorization();
app.UseAuthentication();
```

Authorization may then evaluate before authentication has established the request's principal.

The usual relationship is:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

Conceptually:

```text
Credentials
   ↓
Authentication
   ↓
ClaimsPrincipal
   ↓
Authorization
```

Authentication answers who the request is operating as.

Authorization then evaluates what that principal may do.

Reversing those responsibilities can make valid authenticated users appear anonymous to authorization logic or otherwise change policy behavior.

## Exception Handling Must Wrap the Failures It Should Catch

Suppose an application has a middleware that may throw:

```text
Risky middleware
   ↓
Exception handler
   ↓
Endpoint
```

If the risky middleware throws **before it calls the next component**, the downstream exception handler is never entered.

The handler cannot travel backward in time and catch an exception that occurred before its own invocation.

A broader exception boundary looks like:

```text
Exception handler
   ↓
Risky middleware
   ↓
Endpoint
```

Now both the risky middleware and the endpoint execute inside the handler's downstream call.

The rule is:

> **Place an exception boundary before the behavior it is expected to normalize.**

This does not mean one handler can or should catch every failure in the process. Startup failures, host failures, and failures outside the HTTP pipeline require different handling.

## Request Logging Placement Defines What the Log Can See

Request logging has a similar coverage question.

Consider:

```text
Rate limiter
   ↓
Request logging
   ↓
Endpoint
```

A request rejected by the rate limiter may never reach request logging.

That might be intentional.

But if operators expect every rejected request to appear in the same request log, the placement does not satisfy that expectation.

Moving request logging earlier changes its coverage:

```text
Request logging
   ↓
Rate limiter
   ↓
Endpoint
```

Now request logging wraps the limiter and can observe both accepted and rejected requests, subject to the behavior of the logging implementation.

The correct placement depends on what you intend to observe.

## Response Middleware Must Be Reached Before the Response Is Produced

Security-header middleware illustrates the response side of ordering.

Suppose static-file middleware can produce a response and short-circuit:

```text
Static files
   ↓
Security headers
   ↓
Application endpoints
```

A static-file response may never reach the security-header middleware.

If the application's requirement is that those headers also apply to static-file responses, the security-header component needs to participate earlier:

```text
Security headers
   ↓
Static files
   ↓
Application endpoints
```

This is not a universal statement that every security header belongs before every response producer.

It is a reminder to define the coverage requirement first.

Ask:

> Which responses must carry this header?

Then place and test the middleware accordingly.

## Reverse Proxies Make Early Request Identity Important

Applications behind a reverse proxy or load balancer may receive forwarded information describing the original request scheme, host, and client address.

Any middleware that consumes those values should see the corrected request state.

A common dependency is:

```text
Trusted forwarded-header processing
   ↓
Request logging
   ↓
HTTPS redirect decisions
   ↓
Rate limiting / security decisions
```

If forwarded-header correction occurs too late, downstream behavior may be based on the proxy-facing connection rather than the externally observed request.

The host must still configure which proxies and networks are trusted. Middleware order does not make untrusted forwarded headers safe.

## Rate Limiting Shows Why There Is No Universal Order

Endpoint-specific rate limiting needs routing metadata:

```text
Routing
   ↓
Endpoint-specific rate limiter
   ↓
Endpoint
```

But a purely global limiter can be applied without endpoint metadata and may legitimately run earlier.

That means this question:

> Where does rate limiting go?

has at least two different answers depending on the policy model.

The better question is:

> Does this limiter require endpoint metadata, authenticated identity, corrected proxy information, or another value produced earlier in the pipeline?

The answer determines the useful placement.

## A Representative Ordered Pipeline

A production pipeline might need relationships like these:

```text
Forwarded headers
   ↓
Request logging
   ↓
Centralized exception handling
   ↓
Security response headers
   ↓
HTTPS redirection
   ↓
Static files
   ↓
Routing
   ↓
CORS
   ↓
Endpoint-aware rate limiting
   ↓
Authentication
   ↓
Authorization
   ↓
Endpoints
```

This sequence is intentionally similar to the current `NetCoreApplicationTemplate` working specimen.

It is **not** a universal ASP.NET Core law.

For example:

- An application without a reverse proxy may not need forwarded-header middleware.
- A static site may intentionally leave static assets outside authentication.
- A site that must protect static assets may choose a different static-file arrangement.
- A global limiter may run before routing.
- Caching and compression can have multiple valid orders with different tradeoffs.
- Some diagnostics may intentionally run outside the normal application error boundary.
- Minimal API applications can receive framework-added routing, authentication, authorization, and endpoint middleware when the application does not register them explicitly.

The architectural skill is recognizing the dependency, not memorizing the specimen.

## Minimal APIs Can Add Some Middleware Automatically

Modern `WebApplication` hosting can automatically add routing and, when the corresponding services are present, authentication and authorization middleware.

That convenience does not eliminate order as an architectural concern.

When an application needs explicit control—for example, to place CORS before authentication and authorization—it can register those middleware calls explicitly.

This is a useful distinction:

```text
Framework default placement
```

can be sufficient when the defaults satisfy the application.

Explicit placement becomes useful when the application has an ordering requirement that must remain visible and reviewable.

## Observable Failure Is Better Than a Comment

Do not rely only on comments such as:

```csharp
// Authentication must come before authorization.
```

A stronger architecture has tests or executable scenarios that prove the behavior being protected.

Useful pipeline invariants include:

```text
Failure inside exception boundary
   ↓
Normalized response
```

```text
Failure before exception boundary
   ↓
Custom handler is not reached
```

```text
Request enters Outer then Inner
   ↓
Endpoint
   ↓
Response leaves Inner then Outer
```

```text
Endpoint-specific authorization
   ↓
Routing metadata already exists
```

These tests convert an ordering convention into observable behavior.

## Run the Companion Sample

The companion sample intentionally isolates two ideas:

1. Request/response traversal order.
2. Exception-boundary placement.

From the repository root:

```bash
dotnet run --project samples/middleware-ordering-changes-behavior/MiddlewareOrderingChangesBehavior/MiddlewareOrderingChangesBehavior.csproj -- --PipelineMode=correct --urls http://127.0.0.1:5080
```

Request the normal path and watch the console:

```bash
curl http://127.0.0.1:5080/
```

Then trigger the demonstration failure:

```bash
curl -i http://127.0.0.1:5080/fault
```

In `correct` mode, the demo exception boundary is placed before the fault-producing middleware and returns the sample's controlled 500 response.

Restart in deliberately incorrect mode:

```bash
dotnet run --project samples/middleware-ordering-changes-behavior/MiddlewareOrderingChangesBehavior/MiddlewareOrderingChangesBehavior.csproj -- --PipelineMode=incorrect --urls http://127.0.0.1:5080
```

The `/fault` request now throws before the custom exception boundary is entered.

The companion tests exercise the same invariant without relying on the hosting server's final fallback response.

- [Open the companion sample README](https://github.com/AsiBackbone/Learning/blob/main/samples/middleware-ordering-changes-behavior/README.md)
- [Browse the published executable-samples guide](../samples/index.md)

## Working Implementation References

Learning keeps the demonstration small.

The current `NetCoreApplicationTemplate` repository provides a fuller ASP.NET Core specimen where middleware ordering is centralized and documented as an architectural decision.

| Learning concept | Working reference | What to inspect |
| --- | --- | --- |
| Startup delegates pipeline construction | [`Program.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Program.cs) | `Program.cs` keeps startup readable and delegates the template-owned HTTP sequence to `UseApplicationPipeline()`. |
| Centralized order-sensitive pipeline | [`PipelineExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/PipelineExtensions.cs) | Inspect the current order for forwarded headers, request logging, error handling, security headers, HTTPS, static files, routing, CORS, rate limiting, authentication, authorization, and endpoint mapping. |
| Architectural rationale | [ADR-0002: Use Centralized Application Middleware Pipeline](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/adr/0002-use-centralized-application-middleware-pipeline.md) | Review the order-sensitive invariants and the questions maintainers should ask before moving middleware. |
| Implementation documentation | [Middleware Pipeline](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/articles/middleware.md) | Compare the Learning explanation with the template's current baseline order and custom-middleware guidance. |

Use the working repository as a specimen, not as proof that every application needs the same sequence.

The reusable idea is:

> **Make order-sensitive dependencies explicit enough that a reviewer can explain what changes when one component moves.**

## Review a Pipeline Before Moving Middleware

Before reordering middleware, ask:

1. Does this component require endpoint metadata?
2. Does it require an authenticated principal?
3. Does it depend on corrected proxy request information?
4. Should it observe requests that later short-circuit?
5. Should it catch exceptions from the component being moved?
6. Should it modify responses produced by static files, redirects, failures, or endpoints?
7. Does moving it change which requests are rate limited?
8. Does moving it change CORS preflight behavior?
9. Does moving it change which authorization policy is selected?
10. Do existing integration tests prove the intended behavior?

If any answer changes, the reorder is a behavior change and should be reviewed as one.

## Tradeoffs

### Benefits of Explicit Pipeline Structure

- Security-sensitive order becomes reviewable.
- Failure coverage is easier to explain.
- Endpoint-aware dependencies are visible.
- Logging expectations become testable.
- Custom middleware has a documented insertion point.
- Future maintainers are less likely to treat reordering as harmless cleanup.

### Costs

- Centralizing a large pipeline can become its own abstraction layer.
- Comments and ADRs can drift from code.
- Overly rigid ordering can make application-specific customization harder.
- A long canonical list can tempt readers to copy it without understanding dependencies.
- Integration tests are needed to keep important ordering claims honest.

The goal is not the longest possible pipeline.

The goal is a pipeline whose behavior can be explained.

## Official ASP.NET Core References

- [ASP.NET Core middleware](https://learn.microsoft.com/aspnet/core/fundamentals/middleware/?view=aspnetcore-10.0)
- [ASP0001: Authorization middleware is incorrectly configured](https://learn.microsoft.com/aspnet/core/diagnostics/asp0001?view=aspnetcore-10.0)
- [Rate limiting middleware in ASP.NET Core](https://learn.microsoft.com/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
- [Authentication and authorization in Minimal APIs](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/security?view=aspnetcore-10.0)

## Review Questions

Before moving on, you should be able to answer:

1. Why does response processing make middleware order two-directional?
2. What does it mean for middleware to short-circuit?
3. Why should endpoint-aware authorization run after routing?
4. Why should authentication normally precede authorization?
5. Why can an exception handler only normalize failures produced downstream from it?
6. How does request-logging placement change observable coverage?
7. Why can security-header placement affect which responses receive headers?
8. Why should forwarded-header processing precede middleware that consumes request identity?
9. Why can endpoint-specific and global rate limiting justify different placements?
10. Why are automatic Minimal API middleware defaults compatible with the idea that explicit order still matters?
11. Why should a middleware reorder be treated as a behavior change when an ordering invariant changes?

## Related Content

- [ASP.NET Core learning area](index.md)
- [Identify Middleware Ordering Problems lab](../labs/identify-middleware-ordering-problems.md)
- [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md)
- [Decision Before Execution](../tutorials/decision-before-execution.md)
- [Executable Samples](../samples/index.md)
- [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

---

> **Read it. Run it. Question it. Improve it.**
