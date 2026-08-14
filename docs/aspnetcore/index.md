# ASP.NET Core

The ASP.NET Core section connects the architectural ideas in ASI Backbone Learning to practical application structure in modern .NET web applications.

The focus is not on teaching every ASP.NET Core feature.

Instead, this section examines how application structure can make security, governance, execution boundaries, and operational behavior easier to understand and maintain.

> **Section status:** This page is currently an overview. Focused ASP.NET Core tutorials and examples are planned; start with the [Foundational Tutorials](../tutorials/index.md) for the current learning path.

## Architectural Areas

Future material in this section may examine topics such as:

- Middleware ordering
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
- Configuration
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
* Error handling
* Rate limiting
* Authentication-ready design
* Data-access patterns
* Architecture Decision Records

Learning should explain the architectural lesson without reproducing the full application.

## Current Status

The ASP.NET Core learning area is established and will expand as focused tutorials, architectural comparisons, and small implementation examples are added.

Until then, use the [Foundational Tutorials](../tutorials/index.md) for the governance model and `NetCoreApplicationTemplate` for a fuller application specimen.

---

> **Read it. Run it. Question it. Improve it.**