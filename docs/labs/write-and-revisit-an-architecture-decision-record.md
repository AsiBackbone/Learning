---
description: Practice comparing alternatives, writing an ADR, recording consequences and review conditions, and revisiting the decision when assumptions change.
---

# Lab — Write and Revisit an Architecture Decision Record

**Learning objective:** Evaluate a realistic architectural decision under competing constraints, compare credible alternatives, write a concise Architecture Decision Record that preserves the reasoning, and then revisit that decision when requirements and platform conditions change without rewriting architectural history.

**Difficulty:** Intermediate

**Pattern classification:** General learning material

**Prerequisites:** Read [Architecture Decision Records Preserve Architectural Reasoning](../aspnetcore/architecture-decision-records-preserve-architectural-reasoning.md) and [Architecture Decision Record Lifecycle, Review, Deprecation, and Supersession](../aspnetcore/architecture-decision-record-lifecycle-review-deprecation-and-supersession.md). [Structured Logging Without Sensitive-Data Sprawl](../aspnetcore/structured-logging-without-sensitive-data-sprawl.md) provides the technical context for the scenario. The [Working Repository ADR Case Study: NetCoreApplicationTemplate](../aspnetcore/netcoreapplicationtemplate-adr-case-study.md) is useful comparison material after you complete the exercise.

This lab tests architectural reasoning, not Markdown formatting.

Your ADR does not need to match a supplied template word for word.

It does need to preserve enough reasoning that a future maintainer can understand:

```text
What pressure required a decision?
        ↓
Which constraints mattered?
        ↓
Which alternatives were credible?
        ↓
What was selected?
        ↓
What did the team knowingly gain and give up?
        ↓
What evidence should cause the decision to be reviewed?
```

The lab has two stages.

In the first stage, you make and record a decision.

In the second stage, the environment changes and you decide what should happen to the existing ADR.

---

# Part 1 — Scenario: Operational Logging for Harbor API

You maintain a fictional ASP.NET Core service called **Harbor API**.

Harbor API accepts customer-facing requests, performs application work, calls downstream services, and queues some background operations.

The current application uses `ILogger<T>`, but most events are effectively human-readable console sentences. Operators can read them, but they are difficult to query consistently and correlation across request, background, and dependency boundaries is unreliable.

The team agrees that logging now requires an architectural decision rather than another local formatting change.

## Current Constraints

Treat the following as facts for the first stage:

1. Operations needs **queryable structured events** for production diagnostics.
2. Requests and background work must be **correlatable across application boundaries**.
3. Logs must not contain request bodies, access tokens, secrets, raw credentials, or unnecessary personal data.
4. Developers need useful local diagnostics without installing a production telemetry stack on every workstation.
5. Application code should continue to depend on the standard `ILogger<T>` abstraction rather than a provider-specific logging API.
6. The organization has **not** mandated one logging provider or telemetry backend.
7. The production platform can ingest structured JSON written to standard output.
8. Adding packages, sinks, exporters, or provider-specific configuration creates maintenance and patching cost.
9. The team is willing to accept an additional dependency if it provides a clear operational benefit.
10. Logging is operational telemetry. It is **not** the authoritative governance audit record.

Do not add unstated requirements simply to make your preferred option win.

If you believe another fact is necessary, record it as an assumption rather than silently treating it as part of the scenario.

---

# Part 2 — Identify the Architectural Decision

Before comparing products or packages, write one sentence that describes the decision the team actually needs to make.

A useful decision statement describes an architectural direction.

It should not collapse immediately into a configuration detail.

For example, this is too narrow:

> Should the JSON timestamp property be named `timestamp` or `time`?

That may matter to an implementation or schema, but it is not the architectural decision in this scenario.

A stronger decision scope will address questions such as:

- How should Harbor API produce structured operational events?
- Where should provider-specific behavior live?
- How should correlation be preserved?
- How much dependency and backend coupling should the application accept?

Write your decision question before moving on.

### Checkpoint

Your decision scope should be broad enough that at least two reasonable implementations could answer it, but narrow enough that one ADR can explain the choice coherently.

---

# Part 3 — Compare Credible Alternatives

Evaluate at least **two** reasonable alternatives.

You may use the following three, replace one, or add another option if you can explain it.

## Alternative A — `ILogger<T>` with Built-In Structured Console Output

Application code continues to use `ILogger<T>`.

The host configures structured console output, and the production platform ingests the resulting JSON from standard output.

Possible strengths:

- Small dependency surface.
- Provider-neutral application code.
- Works naturally in container-oriented environments.
- Local development can use a different console formatter without changing application call sites.

Possible costs:

- Enrichment, routing, filtering, or sink behavior may be less sophisticated than a specialized provider.
- The platform ingestion path becomes important to production usefulness.
- Teams may need conventions for event identity, scopes, correlation, and property naming.

## Alternative B — `ILogger<T>` with Serilog as the Host Logging Provider

Application code continues to use `ILogger<T>` while the host integrates Serilog and owns provider configuration.

Possible strengths:

- Mature structured logging ecosystem.
- Flexible enrichment, filtering, formatting, and sink configuration.
- Can keep provider-specific dependencies mostly at the composition boundary.

Possible costs:

- Additional packages and provider-specific configuration.
- Sink choices can increase operational coupling and patch surface.
- The team must avoid treating provider capabilities as permission to log more data than necessary.

## Alternative C — `ILogger<T>` with OpenTelemetry Log Export

Application code continues to use `ILogger<T>` while the host configures OpenTelemetry logging export to a collector or compatible backend.

Possible strengths:

- Can align logs with trace and metric correlation.
- Encourages one telemetry pipeline across services.
- Backend changes may be isolated behind the collector or host configuration.

Possible costs:

- Exporter and collector dependencies introduce operational assumptions.
- Local development can become less convenient if the design assumes external telemetry infrastructure.
- The team must still design stable, minimized events; OpenTelemetry does not solve event quality automatically.

## Build a Decision Matrix

Use a table like this in your notes:

| Criterion | Alternative A | Alternative B | Alternative C |
| --- | --- | --- | --- |
| Queryable structured events | ? | ? | ? |
| Correlation support | ? | ? | ? |
| Sensitive-data control | ? | ? | ? |
| Local-development usability | ? | ? | ? |
| Additional dependency cost | ? | ? | ? |
| Backend/provider coupling | ? | ? | ? |
| Operational fit today | ? | ? | ? |

You may use words such as `strong`, `acceptable`, `weak`, and `unknown` instead of numeric scores.

A numeric total should not decide the architecture automatically.

The purpose of the matrix is to make the tradeoffs visible.

### Required Reasoning

For each alternative you seriously consider, write:

1. Which constraints it satisfies well.
2. Which constraints it satisfies only with additional discipline or infrastructure.
3. Which new maintenance burden it introduces.
4. What assumption would make the option more or less attractive later.

Do not include an obviously unsuitable option only to make your preferred choice look inevitable.

---

# Part 4 — Select an Approach

Choose one alternative.

Your choice is not graded against a secret answer.

It is graded against the scenario and the reasoning you preserve.

A defensible choice should be explainable as:

```text
Given these constraints
        ↓
this option fits better than the alternatives
        ↓
because of these explicit tradeoffs
```

Avoid reasoning such as:

> Serilog is enterprise-ready.

> OpenTelemetry is the future.

> Built-in logging is simpler, therefore it is best.

Those statements hide the actual decision pressure.

Prefer reasoning such as:

> The platform already ingests structured JSON from standard output, so the built-in provider satisfies the current production queryability requirement without adding a provider dependency. We accept fewer provider-specific features in exchange for a smaller maintenance surface.

or:

> Operations needs enrichment and sink-routing behavior that the team already standardizes around Serilog, so the additional provider dependency has a concrete operational benefit. Application code remains on `ILogger<T>` to keep the dependency at the host boundary.

The evidence matters more than the brand name.

---

# Part 5 — Write the Initial ADR

Write a concise ADR for your decision.

You may use this structure or the convention of another repository:

```markdown
# ADR-NNNN: <decision title>

## Status

Accepted

## Context

Describe the problem, constraints, assumptions, and forces before defending the selected option.

## Alternatives Considered

Describe at least two credible alternatives and why each was or was not selected.

## Decision

State the chosen architectural direction clearly.

## Consequences

Positive:
- ...

Negative:
- ...

## Follow-up or Review Conditions

Review this decision if:
- ...

## Related References

Link to relevant implementation or learning material when useful.
```

The exact heading order is not important.

The reasoning is.

## Context Requirements

Your context should preserve at least these categories:

- operational need;
- correlation need;
- sensitive-data boundary;
- local-development need;
- dependency or maintenance pressure;
- current platform capability;
- provider-neutral application-code goal.

Do not write the context as a disguised decision.

Weak:

> We need Serilog because structured logging is important.

Stronger:

> Production support needs queryable structured events and reliable correlation. The platform can already ingest structured JSON from standard output, application code currently uses `ILogger<T>`, and the team wants to minimize new provider dependencies unless they provide a specific operational capability.

The stronger version leaves room to evaluate multiple answers.

## Consequence Requirements

Record both positive and negative consequences.

At least one negative consequence should describe an obligation the team must actually maintain, operate, test, patch, or explain.

Examples include:

- provider package maintenance;
- collector availability;
- platform ingestion dependency;
- event-schema conventions;
- correlation discipline;
- sink configuration;
- migration cost;
- local-development divergence.

## Review-Condition Requirements

Write at least **three** review conditions.

Review conditions should point to changed evidence or assumptions, not calendar age alone.

Good examples include:

- the organization standardizes on one telemetry transport;
- the platform gains or removes a capability that changes the dependency tradeoff;
- correlation requirements expand across multiple services;
- a provider becomes a material security or maintenance burden;
- operational evidence shows the current logging path loses required events;
- local-development and production behavior diverge enough to create repeated defects.

Avoid:

> Review this ADR in one year.

A date can prompt a review, but it does not explain what architectural assumption might have changed.

---

# Part 6 — Validate the Initial ADR

Before reading the second-stage change, review your ADR using this rubric.

| Question | Yes / No |
| --- | --- |
| Does the context describe the problem before defending the solution? | ? |
| Are at least two alternatives genuinely credible? | ? |
| Can a future maintainer state exactly what was chosen? | ? |
| Are positive and negative consequences both visible? | ? |
| Is provider-specific coupling placed deliberately? | ? |
| Does the ADR preserve the sensitive-data boundary? | ? |
| Are review conditions tied to assumptions or evidence? | ? |
| Could a future maintainer tell what would justify reopening the decision? | ? |

If several answers are `No`, improve the reasoning before continuing.

Formatting, heading names, sentence length, and ADR numbering are not part of the score unless they prevent the reasoning from being understood.

---

# Part 7 — Stage Two: Conditions Change

Six months later, the organization changes its telemetry platform.

Treat the following as new facts:

1. The organization now operates a centrally managed **OpenTelemetry Collector**.
2. Platform teams want logs, traces, and metrics to share consistent trace and span correlation across services.
3. OTLP export is now a supported production path with managed routing and backend configuration.
4. Structured JSON on standard output is still supported during migration; it has not suddenly become invalid.
5. Harbor API has been split into the original HTTP service plus a background worker, increasing cross-process correlation needs.
6. Local file log retention is no longer permitted in production containers.
7. Developers still need a simple local console experience.
8. Application code still uses `ILogger<T>` and should remain provider-neutral.
9. No incident has shown that the existing event shapes contain too little or too much data.
10. The original ADR remains in the repository as `Accepted`.

Do not assume these facts automatically require a new ADR.

The answer depends partly on what your original ADR actually decided.

---

# Part 8 — Identify What Actually Changed

Re-read your initial ADR before choosing a lifecycle action.

Create a table like this:

| Original assumption or consequence | Still true? | New evidence | Architectural effect |
| --- | --- | --- | --- |
| Production platform capability | ? | Managed OTLP path now exists | ? |
| Correlation scope | ? | HTTP service + worker | ? |
| Provider dependency cost | ? | Platform support may change the tradeoff | ? |
| Local development | ? | Console still required | ? |
| Sensitive-data boundary | ? | No change | ? |
| Provider-neutral application code | ? | No change | ? |

Then answer:

1. Which original assumptions changed materially?
2. Which constraints did **not** change?
3. Did the architectural direction change, or only the best implementation of that direction?
4. Did one of your written review conditions fire?
5. Would silently editing the old ADR change what a future reader believes the team originally decided?

That last question is especially important.

---

# Part 9 — Choose the Lifecycle Outcome

Choose one of these outcomes and defend it:

```text
Retain the existing ADR

Deprecate the existing ADR

Supersede the existing ADR with a new ADR

Make an implementation/configuration change without changing the ADR
```

The labels are not interchangeable.

## Retain

Retain the ADR when the decision still fits the changed conditions.

A review can conclude that the original direction remains appropriate.

You may record a review note or related pull request without creating a replacement ADR.

## Deprecate

Deprecate when a recorded decision is being retired and there is no direct replacement architectural decision.

For example, a separate ADR that authorized **production rolling-file logs** might be deprecated if the organization simply removes that capability and the broader structured-logging architecture remains unchanged.

Do not use `Deprecated` merely as a softer word for `Superseded`.

## Supersede

Supersede when a later ADR replaces the architectural direction.

If your original ADR explicitly said:

> Harbor API standardizes on provider X and export strategy Y because those are the selected architectural dependencies.

and the team now adopts a materially different provider or transport strategy for architectural reasons, a replacement ADR may be the clearest history.

Preserve the old context and link the records.

## Implementation or Configuration Change Only

A provider or exporter change does not automatically require a new ADR.

Suppose your original decision was deliberately scoped as:

> Application code emits structured events through `ILogger<T>`, provider-specific configuration remains at the host boundary, and production export may change without changing application logging semantics.

If the new collector can be adopted entirely within that existing boundary, the implementation may change while the ADR remains correct.

That is not evading architectural documentation.

It is respecting the scope of the original decision.

---

# Part 10 — Produce the Stage-Two Artifact

Create the artifact appropriate to your chosen lifecycle outcome.

## If You Retain the ADR

Write a short review note containing:

```text
Review trigger
Changed evidence
Why the original decision still fits
Any implementation follow-up
Date or related issue/PR reference if your repository uses one
```

Do not rewrite the original context as if the collector existed when the ADR was first accepted.

## If You Deprecate the ADR

Record:

```text
Status: Deprecated
Reason the decision is being retired
Any remaining migration or cleanup work
Related issue or implementation reference
```

Preserve the original decision and context.

## If You Supersede the ADR

Write a short replacement ADR containing:

```text
New context
Changed assumptions or evidence
Alternatives reconsidered
New decision
Positive and negative consequences
Review conditions
Link back to the original ADR
```

Then update the old ADR only enough to make the lifecycle visible:

```text
Status: Superseded by ADR-NNNN
```

Do not replace the old context with the new context.

## If the Change Is Implementation-Only

Write a short implementation note or pull-request rationale explaining:

```text
What changed
Why the accepted ADR still describes the architecture
Which implementation/configuration details moved
How the change was validated
```

A future reader should be able to distinguish:

```text
Architecture remained stable
        ↓
implementation evolved inside the boundary
```

from:

```text
Architecture changed
        ↓
record was never updated
```

---

# Part 11 — Discussion: Several Answers Can Be Correct

The second-stage facts do not create one universal answer because your original ADR may have been scoped differently.

The following examples illustrate how scope changes the lifecycle outcome.

| Initial ADR scope | Changed conditions | Defensible outcome | Why |
| --- | --- | --- | --- |
| "Use `ILogger<T>` for structured events; provider/export configuration is host-owned and replaceable." | Host switches export to managed OpenTelemetry. | Retain ADR + implementation change | The original architecture anticipated provider replacement behind the host boundary. |
| "Standardize on Serilog as the application logging provider because its enrichment and sink model are required operational capabilities." | Organization standardizes a different telemetry path and those Serilog-specific capabilities are no longer required. | Review, often supersede if the provider decision changes | The provider itself was part of the recorded architectural choice. |
| "Write production logs to rolling local files for retention." | Production containers prohibit local file retention. | Deprecate if the file-retention decision is simply removed; supersede if a new retention architecture replaces it | The correct lifecycle depends on whether a replacement architectural decision exists. |
| "Export logs through OpenTelemetry to a collector while keeping application code on `ILogger<T>`." | Organization introduces a managed collector endpoint. | Retain ADR, likely configuration change | The new platform strengthens rather than contradicts the existing decision. |

The important question is not:

> Which technology is newest?

It is:

> **What did the accepted ADR actually commit the architecture to, and did the changed evidence invalidate that commitment?**

---

# Part 12 — Example Initial ADR

The following is one defensible answer for the first-stage scenario.

It is not the required answer.

```markdown
# ADR-0012: Use provider-neutral structured console logging for Harbor API

## Status

Accepted

## Context

Production support needs queryable structured operational events and correlation
across HTTP requests and background work. Logs must minimize sensitive data and
must not become the governance audit record. Application code already depends on
`ILogger<T>`. The production platform can ingest structured JSON from standard
output, and no organization-wide logging provider is currently mandated. The team
wants to avoid additional provider dependencies unless they solve a requirement
that the platform and built-in logging path cannot satisfy.

## Alternatives Considered

1. Use `ILogger<T>` with built-in structured console output.
2. Use `ILogger<T>` with Serilog configured at the host boundary.
3. Use `ILogger<T>` with OpenTelemetry log export to a collector.

Serilog provides mature enrichment and sink capabilities, but the current scenario
does not require provider-specific routing or sinks strongly enough to justify the
additional dependency. OpenTelemetry would improve telemetry-pipeline alignment,
but the organization does not yet operate a standard collector and requiring one
would add infrastructure to both production and local development.

## Decision

Application code will emit minimized structured events through `ILogger<T>`. The
host will configure structured console output for production, where the platform
will ingest JSON from standard output. Correlation will use established request,
activity, and logging-scope context. Provider-specific APIs will not be used from
application services.

## Consequences

Positive:
- Application code remains provider-neutral.
- Production receives queryable structured events without a new logging provider.
- Local development can retain a straightforward console experience.
- Backend changes can often remain at the host or platform boundary.

Negative:
- The team must define and maintain event naming, property, correlation, and
  sensitive-data conventions.
- Production usefulness depends on reliable platform ingestion of standard output.
- Specialized enrichment, routing, and sink capabilities may require later work.

## Follow-up or Review Conditions

Review this decision if:
- the organization standardizes a telemetry transport or collector;
- cross-service correlation requirements outgrow the current path;
- platform ingestion loses required events or becomes operationally unreliable;
- a specialized provider offers a required capability that cannot be achieved
  reasonably through the current host boundary; or
- provider-neutrality is being eroded by application-level logging dependencies.
```

This ADR is useful because the selected technology is not the only thing preserved.

It records why the smaller dependency surface won **under the original conditions**.

---

# Part 13 — Example Stage-Two Review

For the example ADR above, the managed OpenTelemetry platform fires one of the explicit review conditions.

That means the team should review the decision.

It does **not** mean a replacement ADR is automatically required.

A defensible review could conclude:

```text
Review trigger:
The organization now operates a managed OpenTelemetry Collector and Harbor API
has added a separate background worker.

Changed evidence:
OTLP export is operationally supported and cross-process trace correlation matters
more than it did when ADR-0012 was accepted.

Decision impact:
ADR-0012 requires provider-neutral application code and host-owned export behavior.
Those architectural boundaries remain useful. The specific production export path
was intentionally placed at the host/platform boundary.

Outcome:
Retain ADR-0012 as Accepted. Change host telemetry configuration to evaluate or
adopt OpenTelemetry export. Record the implementation change and validate that
structured event identity, correlation, local console diagnostics, and data
minimization remain intact.
```

That answer is reasonable because the original ADR deliberately made the provider/export path replaceable.

A different initial ADR might require a different lifecycle action.

For example, if the original decision explicitly established Serilog and provider-specific sinks as part of the architecture, replacing that direction with OpenTelemetry could justify a new ADR and supersession relationship.

The lab therefore validates lifecycle reasoning against **your actual original decision**, not against one preferred product choice.

---

# Part 14 — Architecture-Reasoning Rubric

Use this rubric to evaluate your completed work.

A strong submission should satisfy all of these properties:

- The architectural decision is stated clearly enough that another person can tell what is in scope.
- The context describes constraints before defending the selected solution.
- At least two alternatives are genuinely reasonable under the initial scenario.
- The decision is supported by scenario evidence rather than slogans or technology popularity.
- Positive consequences are visible.
- Negative consequences and maintenance obligations are visible.
- Sensitive-data minimization remains an explicit constraint.
- Operational logging remains distinct from authoritative governance audit evidence.
- Review conditions identify evidence or assumptions that could change.
- Stage two identifies which original assumptions changed and which remained stable.
- The lifecycle outcome matches the scope of the original ADR.
- A superseded decision is preserved rather than rewritten to match the present.
- A retained ADR is not replaced merely to demonstrate activity.
- An implementation-only change is not mislabeled as a new architectural decision.
- The reasoning would still be understandable if the original author were unavailable.

A submission can satisfy all of these conditions with different wording, a different heading order, and a different technology choice.

That is intentional.

---

# Part 15 — Compare with a Working Repository

After completing your own ADR and lifecycle review, inspect the ADR material in `AsiBackbone/NetCoreApplicationTemplate`:

- [`docs/adr/index.md`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/adr/index.md)
- [`docs/adr/template.md`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/adr/template.md)
- [ADR-0001: Use Structured Serilog Logging](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/adr/0001-use-structured-serilog-logging.md)

Then compare your work with the [Working Repository ADR Case Study: NetCoreApplicationTemplate](../aspnetcore/netcoreapplicationtemplate-adr-case-study.md).

Do not ask whether your ADR copied the NCAT format exactly.

Ask:

1. Does the NCAT ADR preserve constraints that are different from Harbor API's constraints?
2. Which consequences are repository-specific?
3. Which principles are broadly reusable?
4. If NCAT later changes logging providers, which parts of ADR-0001 would determine whether that change is implementation-only or architectural?
5. What evidence would justify review without automatically forcing replacement?

The working repository is a specimen, not the answer key.

---

# Completion Criteria

You have completed the lab when you can demonstrate all of the following:

- You identified one architectural decision rather than jumping directly to a package choice.
- You compared at least two credible alternatives.
- You wrote context that preserves the actual constraints.
- You selected an approach and connected it to explicit tradeoffs.
- You recorded both positive and negative consequences.
- You defined at least three evidence-based review conditions.
- You revisited the ADR after the scenario changed.
- You distinguished changed assumptions from unchanged constraints.
- You chose and defended `Retain`, `Deprecated`, `Superseded`, or implementation-only change correctly for the scope of your original ADR.
- You preserved historical reasoning rather than editing the old decision to make it look as though the new conditions always existed.
- You can explain why another learner could choose a different technology or lifecycle outcome and still produce a sound architectural record.

The architectural invariant should now be visible:

```text
Decision quality
   ≠
template conformity

Decision quality
   =
recoverable context
+ credible alternatives
+ explicit tradeoffs
+ clear consequences
+ evidence-based review
+ honest lifecycle history
```

---

## Optional Extension — Write the Superseding ADR

If your stage-two answer did not require supersession, create a hypothetical third-stage condition that **would** invalidate the architectural direction you selected.

Examples:

- the provider becomes unsupported;
- a contractual requirement mandates a different telemetry transport;
- the application moves to an environment where standard-output collection is unavailable;
- a new data-boundary requirement prohibits the current export path;
- an operational incident proves the current design cannot preserve required correlation.

Then write:

1. the review trigger;
2. the replacement ADR;
3. the old ADR's supersession status line; and
4. the bidirectional references between the records.

The exercise is complete only if the old ADR still explains why the earlier design once made sense.

---

## Related Content

- [Architecture Decision Records Preserve Architectural Reasoning](../aspnetcore/architecture-decision-records-preserve-architectural-reasoning.md)
- [Architecture Decision Record Lifecycle, Review, Deprecation, and Supersession](../aspnetcore/architecture-decision-record-lifecycle-review-deprecation-and-supersession.md)
- [Structured Logging Without Sensitive-Data Sprawl](../aspnetcore/structured-logging-without-sensitive-data-sprawl.md)
- [Working Repository ADR Case Study: NetCoreApplicationTemplate](../aspnetcore/netcoreapplicationtemplate-adr-case-study.md)
- [Labs](index.md)

---

> **Preserve the reasoning first; preserve the history when the reasoning changes.**
