---
description: Learn when a simple application structure is enough, what signals justify Application or Domain boundaries, and how to add layers without turning architecture style into doctrine.
title: Growing Beyond a Simple Application Structure
author: Christopher D. Cavell
published: 2026-08-30
summary: Add application and domain boundaries only when they solve concrete complexity, dependency, testing, or reuse problems.
feed: true
---

# Growing Beyond a Simple Application Structure

**Pattern classification:** General learning material

A small application can be well designed without having many projects.

A structure such as:

```text
Web
 |
 v
Infrastructure
```

may be entirely sufficient for a large class of ASP.NET Core applications.

The important architectural question is not:

> How many layers should an application have?

It is:

> Which boundaries make the system easier to understand, test, change, and protect?

This page explains when a simple structure remains enough, what signals justify adding an Application or Domain boundary, how dependency direction should be reasoned about, and where CQRS, MediatR, and DDD fit without treating them as requirements.

The [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate) repository is one concrete example used in this discussion. It is a working reference, not a universal pattern.

---

## Start with the Smallest Structure That Works

A compact application structure is usually preferable while it keeps the important responsibilities visible.

A simple structure is often enough when:

- Request handling is easy to follow.
- Business rules are small and local.
- Data access is straightforward.
- Validation mostly protects input shape rather than deep business invariants.
- One host owns the use cases.
- The team can find orchestration, persistence, and tests without friction.
- Adding projects would create more ceremony than clarity.

A small solution can still have strong architecture.

It can still include:

- Explicit dependency injection.
- Centralized configuration.
- Authentication and authorization.
- Structured logging.
- Error handling.
- Transaction boundaries.
- Application services.
- Tests.
- Clear internal namespaces and folders.

Projects are only one way to represent boundaries.

A folder or namespace can be enough until compile-time separation becomes useful.

---

## What Additional Layers Are For

Application and Domain layers are useful when they make a real distinction visible.

A common larger structure is:

```text
Web
 |
 v
Application
 |
 v
Domain

Infrastructure
 |
 +----> Application
 |
 +----> Domain
```

This shape can help separate four different concerns.

### Web or Host

The host owns framework and transport concerns such as:

- HTTP endpoints.
- MVC or Razor Pages.
- Authentication integration.
- Authorization registration.
- Request/response mapping.
- Dependency injection.
- Startup and composition.
- Deployment-specific configuration.

### Application

The application layer coordinates use cases.

It may contain:

- Application services.
- Commands and queries.
- Use-case handlers.
- DTOs.
- Validators.
- Ports or interfaces for external dependencies.
- Transaction coordination.
- Workflow orchestration.

The application layer is useful when the same use case must be invoked from more than one entry point, or when orchestration is becoming too large for controllers or page models.

### Domain

The domain layer owns business meaning that should survive changes in transport or persistence.

It may contain:

- Entities.
- Value objects.
- Domain services.
- Business invariants.
- Domain events.
- State-transition rules.

A separate Domain project is justified when the business model itself has enough independent behavior to benefit from isolation.

### Infrastructure

Infrastructure contains implementation details such as:

- EF Core.
- Database provider code.
- File systems.
- Email.
- Queues.
- HTTP clients.
- External SDKs.
- Repository implementations.
- Messaging adapters.

Infrastructure should implement application- or domain-owned abstractions when inversion of control is useful.

---

## Signals That the Simple Structure Is Becoming Expensive

Add a boundary because pressure exists, not because a diagram says it should.

Useful signals include:

### Business rules are duplicated

The same rule appears in controllers, pages, background jobs, message consumers, or APIs.

That often means the rule needs a reusable application or domain home.

### Entry points are multiplying

A workflow is triggered by:

- HTTP.
- A background worker.
- A scheduled job.
- A message consumer.
- A CLI.
- Another service.

If each entry point contains its own orchestration, an application boundary can reduce duplication.

### Controllers or page models are becoming orchestration-heavy

When a request handler loads data, evaluates rules, coordinates several dependencies, sends notifications, and manages persistence, it may be doing application work that deserves a clearer boundary.

### Business invariants need focused tests

If a rule should be testable without booting ASP.NET Core or EF Core, moving that rule into an application or domain type may improve both design and test speed.

### Infrastructure details are leaking inward

If use-case code depends directly on:

- EF Core query APIs.
- SMTP clients.
- Cloud SDKs.
- File-system APIs.
- Vendor-specific types.

an abstraction boundary may make the use case easier to test and change.

### State transitions are becoming meaningful

A data model may begin as CRUD and later acquire rules such as:

```text
Draft -> Submitted -> Approved
          |
          +-> Rejected
```

When transitions carry business meaning, a domain boundary may become valuable.

---

## Dependency Direction Is More Important Than Layer Names

Layer names do not create architecture by themselves.

Dependency direction does.

A useful rule is:

> Higher-level business policy should not be forced to depend on lower-level implementation detail unless that dependency is intentional and useful.

For example, this direction can preserve that separation:

```text
Web
 |
 v
Application
 |
 v
Domain

Infrastructure
 |
 v
Application abstractions
```

But even that is not mandatory.

A small application may reasonably use:

```text
Web -> Infrastructure
```

if the application does not yet benefit from another abstraction boundary.

The problem is not that one diagram is impure.

The problem is when dependency direction makes important code hard to test, reuse, replace, or understand.

---

## Do Not Add a Layer Before It Has a Job

Premature layering creates costs.

Every new project introduces some combination of:

- Project references.
- Build graph complexity.
- Package-management decisions.
- Naming decisions.
- More files and folders.
- More dependency-injection wiring.
- More tests to organize.
- More concepts for new maintainers.
- More places for abstractions to drift away from actual behavior.

A layer that has no distinct responsibility often becomes a pass-through boundary.

For example:

```text
Controller
   ↓
ApplicationService
   ↓
Repository
   ↓
DbContext
```

is not automatically better than:

```text
Controller
   ↓
DbContext
```

If the application service and repository contain no real policy, reuse, substitution, or testing value, they may only add ceremony.

The right question is:

> What problem becomes easier because this boundary exists?

If there is no strong answer, the boundary may be premature.

---

## CQRS Is Optional

CQRS is useful when separating commands and queries improves clarity or enables materially different models.

A simple form may be only organizational:

```text
CreateOrderCommand
GetOrderDetailsQuery
```

A more advanced form may separate:

- Write models.
- Read models.
- Persistence paths.
- Scaling behavior.
- Security rules.
- Consistency expectations.

But CQRS is not a requirement for having an Application layer.

An application service can remain a normal method:

```csharp
public Task<ArchiveCaseResult> ArchiveCaseAsync(...)
```

without introducing a command bus.

Use CQRS when command/query separation reduces complexity. Do not use it merely because an architecture style includes it.

See [CQRS, Command/Query Separation, and Governed Execution](cqrs-command-query-separation-and-governed-execution.md) for a broader comparison.

---

## MediatR Is a Tool, Not a Layer

MediatR can be useful when an application benefits from:

- A mediator abstraction.
- Handler discovery.
- Pipeline behaviors.
- Cross-cutting validation.
- Logging or telemetry around requests.
- Decoupled notifications.

It also adds:

- Another dependency.
- Indirection.
- Handler conventions.
- Pipeline ordering.
- More framework-specific structure.

A project does not need MediatR in order to have good application boundaries.

Plain services, interfaces, and methods are often enough.

Choose a mediator because its indirection solves a real coordination problem, not because it is associated with CQRS or Clean Architecture examples.

---

## DDD Is Not a Project Template Requirement

Domain-Driven Design is most valuable where software must model a complex business domain with meaningful language, invariants, boundaries, and behavior.

A separate Domain project does not automatically mean an application is doing DDD.

Likewise, an application can use DDD ideas without adopting every tactical pattern.

Useful DDD ideas may include:

- Ubiquitous language.
- Explicit invariants.
- Value objects.
- Aggregates.
- Domain events.
- Bounded contexts.

These ideas should be introduced when the domain benefits from them.

If the application is mostly data entry, CRUD, workflow screens, and straightforward integration, heavy DDD terminology can obscure rather than clarify the model.

---

## Application Services Are a Useful Middle Ground

Many applications do not need either extreme:

```text
Controller directly owns everything
```

or:

```text
Full mediator + CQRS + DDD + event-driven architecture
```

A simple application-service boundary often provides enough separation:

```text
HTTP endpoint
   ↓
Application service
   ↓
Domain rule or persistence boundary
   ↓
Result
```

This gives use cases a home without forcing a large architecture vocabulary.

See [When a Simple Application Service Is Enough](when-a-simple-application-service-is-enough.md) for a detailed comparison.

---

## A Safe Incremental Migration Strategy

If a codebase has outgrown its original structure, do not rewrite the entire solution only to match a target diagram.

A safer path is incremental.

### 1. Identify duplicated or overloaded use-case logic

Start with the code that is already causing pain.

### 2. Extract application services

Move orchestration out of transport-specific handlers when reuse or focused testing becomes valuable.

### 3. Introduce an Application project only when compile-time separation helps

A folder may be enough first.

Create a project when project-level references improve the boundary.

### 4. Move business invariants toward domain types

Do this when rules represent business meaning independent of HTTP, EF Core, or provider SDKs.

### 5. Introduce a Domain project only when the domain deserves independent isolation

Do not create it merely to hold data classes.

### 6. Move infrastructure behind abstractions where substitution matters

Examples include:

- External services.
- Messaging.
- File storage.
- Provider SDKs.
- Persistence behaviors that should not leak into use cases.

### 7. Add focused tests at the new boundary

The new layer should make some tests easier.

If testing becomes harder, reassess the design.

### 8. Preserve integration tests

Layering should not remove tests that prove the application still composes correctly.

---

## Reference Example: NetCoreApplicationTemplate

[AsiBackbone/NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate) intentionally generates a small baseline:

```text
ProjectTemplate.Web
        |
        v
ProjectTemplate.Infrastructure

ProjectTemplate.Web.Tests
        |
        v
ProjectTemplate.Web
```

This is a concrete working reference showing that a production-oriented ASP.NET Core baseline does not need to prescribe Application, Domain, CQRS, MediatR, or DDD projects.

Consumers may add those boundaries later if their application complexity justifies them.

NCAT's own implementation-specific extension rules are documented in:

[Application and Domain Extension Boundaries](https://asibackbone.github.io/NetCoreApplicationTemplate/articles/optional-application-domain-layers.html)

That page explains how NCAT's generated solution, template metadata, scaffold manifest, package inputs, and tests must be considered when extending the actual template.

The distinction matters:

- **Learning** explains when and why additional architecture boundaries may help.
- **NCAT** explains how its concrete generated template is structured and what must remain consistent when that structure changes.

---

## Decision Checklist

Before adding a new layer, ask:

| Question | If yes |
| --- | --- |
| Is use-case logic duplicated across entry points? | An Application boundary may help. |
| Are transport details mixed with reusable orchestration? | Extract an application service. |
| Do business invariants deserve framework-independent tests? | A Domain boundary may help. |
| Are provider SDKs leaking into use cases? | Introduce abstractions and move implementations outward. |
| Do commands and queries have materially different models or concerns? | CQRS may help. |
| Would a mediator pipeline solve a real cross-cutting coordination problem? | MediatR or another mediator may help. |
| Is the domain complex enough to benefit from explicit modeling language and invariants? | DDD techniques may help. |
| Would the new project mostly pass calls through unchanged? | Do not add it yet. |
| Can the existing structure still be understood and tested easily? | Keep it simple. |

---

## Summary

Good architecture does not maximize the number of layers.

It creates boundaries that correspond to real differences in responsibility, dependency, trust, lifecycle, or business meaning.

Start small.

Add Application or Domain boundaries when they make the system easier to reason about.

Use CQRS, MediatR, or DDD when they solve specific problems.

Keep simpler structures when they remain clear.

And treat working repositories such as NetCoreApplicationTemplate as references to study, not templates that every application must copy exactly.
