# ASP.NET Core

The ASP.NET Core section connects the architectural ideas in ASI Backbone Learning to practical application structure in modern .NET web applications.

The focus is not on teaching every ASP.NET Core feature.

Instead, this section examines how application structure can make security, governance, execution boundaries, and operational behavior easier to understand and maintain.

> **Section status:** Focused ASP.NET Core learning is expanding. Start with [Middleware Ordering Changes Behavior](middleware-ordering-changes-behavior.md), continue with [Secure-by-Default ASP.NET Core Configuration](secure-by-default-configuration.md), then use the [Foundational Tutorials](../tutorials/index.md) when you want to connect application structure to governed execution.

## Start Here

[Middleware Ordering Changes Behavior](middleware-ordering-changes-behavior.md) is the first focused ASP.NET Core tutorial. It explains the two-direction request/response pipeline, short-circuiting, exception-handler coverage, authentication/authorization order, endpoint-routing boundaries, request logging, security headers, proxy correction, and rate-limiting placement.

Its [companion sample](https://github.com/AsiBackbone/Learning/blob/main/samples/middleware-ordering-changes-behavior/README.md) includes both a corrected pipeline and a deliberately incorrect pipeline so the order-sensitive behavior is observable.

Continue with the [Identify Middleware Ordering Problems lab](../labs/identify-middleware-ordering-problems.md) to diagnose the incorrect sequence, repair a disposable copy, validate the changed behavior, and explain the ordering dependency.

[Secure-by-Default ASP.NET Core Configuration](secure-by-default-configuration.md) then treats configuration as part of the application architecture and trust boundary. It covers explicit opt-in, startup validation, environment-specific behavior, secrets, safer failure choices, and configuration ownership boundaries.

## Architectural Areas

This section will continue to expand into topics such as:

- [Middleware ordering](middleware-ordering-changes-behavior.md)
- [Secure defaults and configuration](secure-by-default-configuration.md)
- Dependency injection boundaries
- Request validation
- Centralized exception handling
- Status-code handling
- Structured logging
- Forwarded headers
- Reverse-proxy deployment
- Rate limiting
- Authentication-ready architecture
- Data-access boundaries
- Configuration ownership
- Background processing
- Health checks
- Operational diagnostics
- Governance integration

## Governed Execution in ASP.NET Core

A common application path is:

```text
HTTP Request
   ↓
Authentication
   ↓
Authorization
   ↓
Application Logic
   ↓
Side Effect
```

For consequential operations, the application may benefit from a more explicit boundary:

```text
HTTP Request
   ↓
Authenticated Actor
   ↓
Proposed Intent
   ↓
Policy Context
   ↓
Governance Decision
   ↓
Scoped Authority
   ↓
Host-Owned Execution
   ↓
Response + Audit Residue
```

The appropriate amount of structure depends on the application.

Not every endpoint requires a governance pipeline.

### Recommended Learning Path

Before applying these ideas to an ASP.NET Core application, review:

* [Decision Before Execution](../tutorials/decision-before-execution.md)
* [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md)
* [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md)

For AI-assisted application scenarios, continue with:

* [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md)

## Working ASP.NET Core Reference

The primary implementation reference is:

[AsiBackbone/NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

That repository provides a fuller ASP.NET Core reference architecture demonstrating production-oriented concerns such as:

* Middleware organization
* Structured logging
* Security defaults
* Configuration validation and environment handling
* Error handling
* Rate limiting
* Authentication-ready design
* Data-access patterns
* Architecture Decision Records

Learning should explain the architectural lesson without reproducing the full application.

## Current Status

The ASP.NET Core learning area now covers middleware ordering and secure-by-default configuration. The middleware lesson is paired with an executable companion sample, focused invariant tests, and a beginner diagnostic lab; the configuration lesson establishes the next architecture foundation around explicit opt-in, startup validation, environment behavior, secrets, safer failure, and configuration ownership. Additional material will expand into structured logging, centralized error handling, data access, and related application-architecture concerns.

Use the [Foundational Tutorials](../tutorials/index.md) for the governance model and `NetCoreApplicationTemplate` for a fuller application specimen.

---

> **Read it. Run it. Question it. Improve it.**