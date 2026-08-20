# Working Repository ADR Case Study: NetCoreApplicationTemplate

**Pattern classification:** Working Repository Walkthrough

**Difficulty:** Intermediate

**Prerequisites:** Read [Architecture Decision Records Preserve Architectural Reasoning](architecture-decision-records-preserve-architectural-reasoning.md) and [Architecture Decision Record Lifecycle, Review, Deprecation, and Supersession](architecture-decision-record-lifecycle-review-deprecation-and-supersession.md) first. [Middleware Ordering Changes Behavior](middleware-ordering-changes-behavior.md) and [Structured Logging Without Sensitive-Data Sprawl](structured-logging-without-sensitive-data-sprawl.md) provide the general architectural lessons used in the two walkthroughs below.

**Learning objective:** Trace a general architectural problem into repository-specific constraints, an accepted Architecture Decision Record, concrete code and configuration, consequences, alternatives, and review triggers; distinguish reusable architectural reasoning from a particular repository's implementation choice; and recognize when implementation drift should lead to a code correction, documentation update, ADR review, or superseding decision.

## Pattern Card

> **Problem:** ADRs can remain abstract if learners see only the document. A decision becomes easier to understand when the architectural problem, repository constraints, recorded rationale, implementation, and maintenance consequences can be inspected together.
>
> **Pattern:** Read an ADR as one link in a decision chain. Start with the general problem, identify the constraints of the working repository, inspect the ADR that records the chosen direction, trace that direction into code and configuration, and then ask which parts are reusable principles versus local choices.
>
> **Use when:** A repository contains both architectural decision records and enough implementation evidence to show how an accepted decision shapes real code, configuration, tests, or operational documentation.
>
> **Prefer something simpler when:** The change is a local implementation detail with no meaningful architectural alternative, consequence, or future review condition. Not every code choice deserves an ADR-backed walkthrough.
>
> **Observe:** A useful repository walkthrough should let a learner move in both directions: from architectural reasoning into implementation and from implementation back to the decision that explains why the structure exists.

## Read the Decision Chain, Not Just the ADR

An ADR is not a replacement for architectural teaching, and it is not a replacement for code.

It connects them.

A useful learning path is:

```text
General architectural problem
        ↓
Learning explanation
        ↓
Repository-specific constraints
        ↓
Architecture Decision Record
        ↓
Concrete implementation + configuration
        ↓
Consequences, alternatives, and maintenance evidence
```

The same chain can be followed in reverse when maintaining a system:

```text
Unexpected implementation behavior
        ↓
Locate the architectural boundary
        ↓
Find the ADR that explains the intended direction
        ↓
Compare current code with recorded assumptions
        ↓
Correct drift, update documentation, or revisit the decision
```

This case study uses [`AsiBackbone/NetCoreApplicationTemplate`](https://github.com/AsiBackbone/NetCoreApplicationTemplate) as a working specimen because it contains ADRs alongside the ASP.NET Core implementation they describe.

The repository is not a universal standard.

Its value here is that the reasoning and the implementation can be inspected together.

## Case Study 1: Centralize an Order-Sensitive Middleware Pipeline

### Start with the General Problem

The Learning article [Middleware Ordering Changes Behavior](middleware-ordering-changes-behavior.md) establishes the reusable problem:

- Middleware runs in an ordered request/response pipeline.
- Some components produce state that later components consume.
- Some middleware must wrap later behavior to observe responses or handle failures.
- Short-circuiting can prevent later middleware or endpoints from running.
- Endpoint-aware behavior may depend on routing having already selected endpoint metadata.

The general architectural principle is therefore not:

> Use one exact middleware sequence in every ASP.NET Core application.

It is:

> **Make order-sensitive dependencies explicit enough that maintainers can explain what changes when a component moves.**

### Add the Repository-Specific Constraints

`NetCoreApplicationTemplate` is a reusable application template rather than one narrowly tailored application.

That changes the design pressure.

The template combines several cross-cutting concerns, including forwarded-header processing, request logging, centralized error handling, security headers, HTTPS redirection, static files, routing, CORS, rate limiting, authentication, authorization, and endpoint mapping.

For this repository, maintainers also need:

- A visible baseline order that template consumers can inspect.
- A readable `Program.cs` that does not bury the startup path in a long sequence of middleware calls.
- One obvious place to review order-sensitive changes during framework upgrades.
- A documented place for future maintainers to compare implementation with architectural intent.

Those are local constraints.

Another application may prefer a shorter inline pipeline because it has only a few components and no reusable-template requirement.

### Read the ADR

Now inspect [ADR-0002: Use Centralized Application Middleware Pipeline](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/adr/0002-use-centralized-application-middleware-pipeline.md).

The ADR records the repository-specific decision to centralize the template-owned HTTP pipeline in `UseApplicationPipeline()` rather than scattering the ordering across startup code or relying primarily on implicit framework placement.

It also records alternatives rather than pretending the chosen structure was inevitable:

1. **Register the sequence directly in `Program.cs`.** Simpler for a small application, but increasingly noisy and easier to reorder casually in a reusable template.
2. **Split the pipeline across several feature-owned helpers.** More modular locally, but the complete order becomes harder to review as one architectural unit.
3. **Rely more heavily on framework-added or implicit placement.** Valid in applications where framework defaults satisfy the required relationships, but less explicit for a template that wants a stable, reviewable baseline.

The important lesson is not that those alternatives are wrong.

The ADR explains why they were not selected **under this repository's constraints**.

### Trace the Decision into the Implementation

Start with [`Program.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Program.cs).

The startup path delegates the template-owned pipeline to one call:

```csharp
app.UseApplicationPipeline();
```

Then inspect [`PipelineExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/PipelineExtensions.cs).

That file is where the ADR becomes concrete. The current implementation keeps the order together and explicitly warns maintainers to keep the sequence aligned with ADR-0002 and the middleware documentation.

The current sequence makes relationships such as these visible:

```text
Forwarded headers
        ↓
Request logging
        ↓
Error handling
        ↓
Security headers
        ↓
HTTPS / static files
        ↓
Routing
        ↓
CORS / rate limiting
        ↓
Authentication
        ↓
Authorization
        ↓
Endpoints
```

The repository's [Middleware Pipeline documentation](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/articles/middleware.md) provides the implementation-facing explanation beside the code.

Notice the division of responsibility:

| Artifact | Responsibility |
| --- | --- |
| Learning tutorial | Explains why middleware order can change behavior. |
| ADR-0002 | Records why this repository chose one centralized baseline. |
| `Program.cs` | Delegates pipeline construction to the chosen boundary. |
| `PipelineExtensions.cs` | Enacts the current template-owned order. |
| Implementation documentation and tests | Explain and exercise the behavior that the repository intends to preserve. |

No single artifact has to carry the entire explanation.

### Separate the Reusable Principle from the Local Choice

| Reusable architectural reasoning | `NetCoreApplicationTemplate` choice |
| --- | --- |
| Middleware ordering is behavior, not formatting. | Centralize the template-owned sequence in `UseApplicationPipeline()`. |
| Producers should precede consumers of request state. | Process trusted forwarded headers before downstream request logging and request-dependent behavior. |
| Wrappers must precede behavior they are expected to observe or catch. | Place the centralized error boundary before later application pipeline behavior. |
| Endpoint-aware policies need endpoint metadata first. | Route before endpoint-aware CORS/rate-limit/auth behavior in the current baseline. |
| Important ordering assumptions should be reviewable. | Preserve the baseline in one extension method and ADR. |

A project can accept every item in the left column without copying every item in the right column.

### Ask What Would Trigger Review

Suppose a maintainer wants to move rate limiting before routing.

The first question is not:

> Does ADR-0002 forbid this?

The useful questions are:

1. Is the limiter global, or does it require endpoint metadata?
2. Does the new location still satisfy the behavior the template intends to preserve?
3. Is this a local implementation correction, or does it change the architectural baseline?
4. Do existing tests and documentation still describe the resulting behavior?

If the code accidentally drifted away from an accepted ADR and the original reasoning still holds, fixing the implementation may be enough.

If the desired architecture has materially changed, use the lifecycle guidance in [Architecture Decision Record Lifecycle, Review, Deprecation, and Supersession](architecture-decision-record-lifecycle-review-deprecation-and-supersession.md) rather than silently rewriting the old rationale.

## Case Study 2: Choose Structured Logging Without Coupling Application Code to the Provider

### Start with the General Problem

[Structured Logging Without Sensitive-Data Sprawl](structured-logging-without-sensitive-data-sprawl.md) teaches a provider-neutral lesson:

- Operational logs should be designed as deliberate events rather than uncontrolled object dumps.
- Stable event identity and property names make telemetry easier to search and reason about.
- Correlation can connect small events without duplicating entire requests or payloads.
- Sensitive values should stay out by default.
- Logging should occur at meaningful architectural boundaries.

Those principles do not require Serilog.

They describe what useful operational logging should accomplish.

### Add the Repository-Specific Constraints

The template needs a consistent logging baseline that can support:

- Structured message templates.
- Context enrichment.
- Console output for local and hosted execution.
- Rolling file output in the current template baseline.
- Configuration-driven sink and level changes.
- Application code that still depends on the standard `ILogger<T>` abstraction rather than a provider-specific logger API.

Those requirements narrow the local choice without turning the selected provider into a universal recommendation.

### Read the ADR

Inspect [ADR-0001: Use Structured Serilog Logging](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/adr/0001-use-structured-serilog-logging.md).

The ADR chooses Serilog through the Microsoft logging integration while preserving `ILogger<T>` as the application-facing abstraction.

It also records credible alternatives:

1. **Built-in console logging only.** Sufficient for simpler needs, but not the desired sink/enrichment/configuration baseline for this template.
2. **NLog.** A viable structured logging provider, but not the provider selected for this repository.
3. **OpenTelemetry logs only.** A possible future direction, but the ADR treats OpenTelemetry tracing and metrics as complementary rather than a reason to remove the current logging provider immediately.

Again, the value of the alternatives section is not to label other approaches as bad.

It preserves why one choice fit the repository at the time of the decision.

### Trace the Decision into Startup, Extension Code, and Configuration

Three implementation surfaces show the decision at different levels.

First, [`Program.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Program.cs) creates a bootstrap logger for startup diagnostics and then calls:

```csharp
builder.AddApplicationSerilog();
```

Second, [`SerilogExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/SerilogExtensions.cs) configures Serilog from host configuration and registered services while enriching from the log context.

Third, [`appsettings.json`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/appsettings.json) contains the current provider configuration: level overrides, enrichers, console output, rolling file output, and the fields rendered for request, correlation, trace, and span context.

The repository's [Logging documentation](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/articles/logging.md) explains the implementation-facing logging model.

The chain is therefore visible:

```text
Learning principle:
structured, minimized operational events
        ↓
Repository constraint:
consistent configurable template baseline
        ↓
ADR-0001:
Serilog behind Microsoft logging abstractions
        ↓
Program.cs:
bootstrap + registration boundary
        ↓
SerilogExtensions.cs:
provider integration
        ↓
appsettings.json:
current sinks, levels, enrichment, rendering
```

### Distinguish Decision from Configuration Detail

An ADR should preserve the architectural choice without forcing every operational setting to become permanent architecture.

For example, these changes may fit inside the accepted provider decision:

- Adjust a minimum log level.
- Change a file-retention count.
- Add or remove a reviewed low-risk enrichment field.
- Replace a local file sink with a centralized sink while retaining the provider strategy.

Those changes may need normal configuration review, operational documentation, tests, or security review without requiring a new ADR.

A materially different change is different:

```text
Current decision
Serilog as configured provider behind ILogger<T>
        ↓
New organizational or platform constraint
        ↓
Replace provider strategy entirely
        ↓
Review ADR-0001
        ↓
Retain, amend through the repository's convention, or supersede
```

The line is not whether a file changed.

The line is whether the architectural decision changed.

### Separate the Reusable Principle from the Local Choice

| Reusable architectural reasoning | `NetCoreApplicationTemplate` choice |
| --- | --- |
| Prefer structured events over formatted-text-only diagnostics. | Use Serilog as the configured provider. |
| Keep application code behind a logging abstraction when practical. | Keep application code on Microsoft `ILogger<T>`. |
| Preserve correlation and useful low-risk context. | Render the repository's current correlation, trace, span, request, and path fields. |
| Minimize sensitive data and uncontrolled payload logging. | Configure reviewed sinks and templates through `appsettings.json`. |
| Treat observability configuration as maintainable operational architecture. | Use console and rolling file sinks in the current baseline. |

A project may choose another provider, another sink strategy, or no file sink at all while preserving the left-column principles.

## Compare the Two Decision Chains

The two ADRs look different, but the reasoning pattern is the same.

| Question | Middleware pipeline | Structured logging |
| --- | --- | --- |
| What is the general problem? | Order changes request/response behavior. | Diagnostics need structured, bounded, correlatable events. |
| What makes the repository different? | A reusable template needs one reviewable baseline across many cross-cutting components. | A reusable template needs a configurable provider baseline while application code remains provider-agnostic. |
| Where is the decision recorded? | ADR-0002. | ADR-0001. |
| Where is it enacted? | `Program.cs` and `PipelineExtensions.cs`. | `Program.cs`, `SerilogExtensions.cs`, and `appsettings.json`. |
| What alternatives were preserved? | Inline startup, split helpers, more implicit placement. | Built-in console only, NLog, OpenTelemetry-only logging. |
| What might trigger review? | A new hosting/pipeline model, changed ordering requirements, or centralization becoming a customization barrier. | A mandated provider, a changed observability platform, or Serilog becoming unnecessary for the required baseline. |

The reusable skill is learning to trace that relationship yourself.

## Repository Walkthrough Checklist

When a Learning article points to a working implementation, use this sequence:

1. **State the general problem first.** Explain the architectural behavior without assuming the working repository's answer.
2. **Identify local constraints.** Ask what scale, trust boundary, deployment model, reuse goal, operational requirement, or maintenance concern made a decision necessary.
3. **Read the ADR completely.** Inspect context, decision, alternatives, consequences, and review conditions rather than reading only the title.
4. **Trace the implementation.** Follow the decision into startup code, implementation files, configuration, tests, and operational documentation where relevant.
5. **Separate principle from specimen.** Write down what would remain true if the application used a different framework feature, provider, or repository structure.
6. **Look for drift.** Compare the accepted ADR with current implementation and documentation. A mismatch is evidence to investigate, not automatic proof that either side is wrong.
7. **Choose the right maintenance action.** Decide whether the change needs a code fix, documentation correction, configuration update, ADR review, or a new superseding ADR.

This turns a repository link into a learning exercise rather than a copy-and-paste instruction.

## What Not to Copy Blindly

The working repository is intentionally concrete.

That makes it useful, but it also creates a copying risk.

Do not infer that:

- Every ASP.NET Core application needs the same middleware sequence.
- Every application should centralize the entire pipeline in one extension method.
- Serilog is required for structured logging.
- Every application needs console plus rolling file sinks.
- The template's current correlation fields, log levels, retention settings, or middleware set are universal defaults.
- Every implementation detail deserves its own ADR.
- An accepted ADR should prevent a project from evolving when its assumptions change.

Instead, reuse the reasoning process:

```text
Problem
  ↓
Constraints
  ↓
Alternatives
  ↓
Decision
  ↓
Implementation evidence
  ↓
Consequences
  ↓
Review when assumptions change
```

That is the durable connection between Learning material and an implementation repository.

## Related Learning Material

- [Architecture Decision Records Preserve Architectural Reasoning](architecture-decision-records-preserve-architectural-reasoning.md)
- [Architecture Decision Record Lifecycle, Review, Deprecation, and Supersession](architecture-decision-record-lifecycle-review-deprecation-and-supersession.md)
- [Middleware Ordering Changes Behavior](middleware-ordering-changes-behavior.md)
- [Structured Logging Without Sensitive-Data Sprawl](structured-logging-without-sensitive-data-sprawl.md)

## Working Repository References

- [`NetCoreApplicationTemplate` ADR index](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/adr/index.md)
- [ADR-0001: Use Structured Serilog Logging](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/adr/0001-use-structured-serilog-logging.md)
- [ADR-0002: Use Centralized Application Middleware Pipeline](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/adr/0002-use-centralized-application-middleware-pipeline.md)
- [`Program.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Program.cs)
- [`PipelineExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/PipelineExtensions.cs)
- [`SerilogExtensions.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/Extensions/SerilogExtensions.cs)
- [`appsettings.json`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/src/ProjectTemplate.Web/appsettings.json)
- [Middleware Pipeline documentation](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/articles/middleware.md)
- [Logging documentation](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/articles/logging.md)

The working repository will continue to evolve.

When a linked implementation changes, compare the new code with the accepted ADR instead of assuming the article or the implementation must automatically win.

That comparison is part of architectural maintenance.

---

> **Learning principle → local constraints → recorded decision → implementation evidence → reviewable consequences.**
