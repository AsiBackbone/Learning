# Architecture Decision Records Preserve Architectural Reasoning

**Pattern classification:** General Learning Material

**Difficulty:** Beginner

**Prerequisites:** Basic familiarity with software architecture and the idea that meaningful design choices involve constraints and tradeoffs. The [ASP.NET Core learning area](index.md) is useful context but is not required.

**Learning objective:** Recognize when a decision deserves an Architecture Decision Record, distinguish ADRs from routine documentation, write a concise record that preserves context and tradeoffs, and explain why alternatives and consequences matter as much as the selected decision.

## Pattern Card

> **Problem:** Code preserves what a system currently does, but it often cannot explain why a consequential choice was made, which alternatives were rejected, which constraints shaped the decision, or which costs were knowingly accepted.
>
> **Pattern:** Record consequential architectural choices as short, version-controlled Architecture Decision Records that preserve the decision context, meaningful alternatives, selected direction, consequences, and conditions that may justify future review.
>
> **Use when:** A choice has long-lived effects on system structure, trust boundaries, data ownership, deployment, operations, major dependencies, cross-cutting behavior, or another concern that future maintainers may reasonably question.
>
> **Prefer something simpler when:** The change is a routine implementation detail, local refactor, temporary fix, ordinary code cleanup, or another choice whose reasoning is already obvious from the code and carries little architectural consequence.
>
> **Observe:** A useful ADR lets a future maintainer reconstruct the pressure that existed when the choice was made instead of reverse-engineering intent from the surviving implementation.

## Architectural Reasoning Is Easy to Lose

A repository is very good at preserving implementation history.

It is much less reliable at preserving the reasoning that produced that implementation.

A future maintainer may be able to see that an application uses a centralized middleware pipeline, a particular persistence boundary, a dedicated background worker, or a specific authentication strategy.

The code may not reveal:

- Which competing approaches were considered.
- Which operational or security constraints mattered at the time.
- Which tradeoffs were accepted deliberately.
- Which option was rejected and why.
- Which assumption would cause the team to reconsider the decision later.

Over time, commit messages become hard to search, pull-request conversations become detached from the relevant code, team members leave, and once-obvious constraints stop being obvious.

The architectural reasoning can disappear even while the implementation remains.

An ADR exists to preserve that missing layer.

```text
Architectural pressure
        ↓
Decision required
        ↓
Alternatives considered
        ↓
Decision
        ↓
Consequences
        ↓
Recorded reasoning
```

The record is not valuable because it is Markdown.

It is valuable because it preserves the reasoning chain.

## What Is an Architecture Decision Record?

An **Architecture Decision Record**, usually shortened to **ADR**, is a small document that captures one meaningful architectural decision and enough surrounding evidence to understand it later.

A useful ADR answers questions such as:

1. What problem or pressure required a decision?
2. What constraints shaped the available options?
3. Which alternatives were seriously considered?
4. What did the team decide?
5. What benefits, costs, risks, or maintenance obligations follow from that choice?
6. What evidence or changed assumptions might justify reviewing the decision later?

An ADR is therefore closer to a preserved architectural argument than to a generic project note.

It should be understandable even after the original participants are no longer available to explain the decision verbally.

## ADRs Are Not a Universal Standard

There is no single universal ADR file format, numbering scheme, status vocabulary, or required section order.

Teams commonly use conventions such as:

```text
0001-use-structured-logging.md
0002-centralize-http-pipeline-composition.md
```

and status values such as:

```text
Proposed
Accepted
Deprecated
Superseded
```

Those conventions are useful because consistency makes records easier to scan.

They are still conventions.

A team can use different names, omit numbering, add a decision owner, add review conditions, or organize sections differently if the resulting record remains clear.

Do not confuse the template with the architectural practice.

The practice is preserving consequential reasoning.

## When Does a Decision Merit an ADR?

A practical test is to ask whether the choice changes the system in a way that a future maintainer may reasonably need to understand before changing it again.

A decision is a strong ADR candidate when several of these are true:

- It shapes the long-term structure of the application.
- It crosses a security, trust, process, deployment, or data boundary.
- More than one reasonable alternative exists.
- The selected approach introduces a meaningful operational or maintenance cost.
- The choice is expensive or risky to reverse.
- The reasoning depends on constraints that are not obvious from the code.
- The decision affects multiple teams, components, or deployment units.
- A future maintainer is likely to ask, "Why is it built this way?"

Examples can include:

| Decision | Why it may deserve an ADR |
| --- | --- |
| Run long-running work in a separate worker service | Changes deployment shape, failure handling, scaling, and operational ownership. |
| Centralize middleware composition | Makes order-sensitive behavior and application startup structure a deliberate long-term choice. |
| Adopt a particular persistence strategy | Affects data ownership, transactions, provider behavior, migrations, and portability. |
| Use an outbox for database-to-message-broker coordination | Adds durable state and recovery behavior to avoid unsafe cross-system assumptions. |
| Establish a host-owned execution boundary for AI-proposed actions | Defines where authority, validation, credentials, and side effects are controlled. |
| Standardize application logging around structured events | Creates operational dependencies, event-shape expectations, and data-handling obligations. |

The deciding factor is not how many files the change touches.

A one-line configuration choice can be architectural if it changes a trust boundary or deployment assumption.

A large refactor can be non-architectural if it preserves the same boundaries and behavior.

## What Usually Does Not Need an ADR?

Most repository changes should not require an ADR.

Examples that normally do not need one include:

- Renaming a local variable or private method.
- Extracting a helper without changing architectural responsibilities.
- Formatting or analyzer cleanup.
- Routine dependency patch updates that do not change the architecture.
- Fixing a straightforward defect while preserving the existing design.
- Adding a unit test for already-established behavior.
- Updating API reference documentation after a method signature changes.
- Temporary diagnostic code that will be removed.

The word **normally** matters.

Context can change the answer.

A dependency upgrade that forces a new authentication model or a "small" configuration change that moves secret ownership across a trust boundary may deserve a record because the architectural consequences are no longer small.

A useful question is:

> Will a future maintainer need more than the diff to understand why this choice exists?

If the answer is no, an ADR is probably unnecessary.

## ADRs Are Different from Other Documentation

ADRs complement other documentation rather than replace it.

| Artifact | Primary question it answers | Typical scope |
| --- | --- | --- |
| **ADR** | Why did we choose this architectural direction? | One consequential architectural decision and its reasoning. |
| **Code comment** | Why does this local implementation behave this way? | A nearby line, method, algorithm, workaround, or invariant. |
| **API documentation** | How do callers use this type, endpoint, or contract? | Public behavior, parameters, results, errors, and usage. |
| **Implementation guide** | How is this subsystem configured, operated, or extended? | Current mechanics and procedures. |
| **Changelog entry** | What changed in this release or version? | User-visible or repository-visible change history. |

These artifacts can point to one another.

For example:

```text
ADR
  ↓ explains why
Implementation guide
  ↓ explains how
Code
  ↓ performs the behavior
Changelog
  ↓ records when the behavior changed
```

Trying to make one artifact perform all four jobs usually makes the documentation less useful.

## A Practical ADR Structure

A concise ADR can use the following structure:

```markdown
# ADR-NNNN: Decision title

## Status

Proposed

## Context

What problem, constraints, assumptions, and forces require a decision?

## Alternatives Considered

What reasonable options were considered, including keeping the current approach?

## Decision

What was selected, stated clearly and directly?

## Consequences

What benefits, costs, risks, and maintenance obligations follow?

## Follow-up or Review Conditions

What evidence or changed assumption should cause the team to revisit this decision?

## Related References

Which issues, pull requests, code, diagrams, or operational evidence help a future reader?
```

The exact headings and order are not important.

The information is.

Some teams place **Decision** before **Alternatives Considered**. Others place **Consequences** before alternatives. Some omit **Related References**. Some keep review conditions in the context or consequences section.

The format should make the reasoning easy to recover, not satisfy a universal schema that does not exist.

## Write the Context Before Defending the Decision

A weak ADR often begins with the preferred solution and then works backward to justify it.

That produces records such as:

> We will use a worker service because worker services are scalable.

The statement does not explain the actual architectural pressure.

A stronger context describes the problem before selecting the answer:

```text
Export jobs can run for several minutes.
HTTP requests should not remain open for the duration.
Jobs must survive an application restart.
The team already operates a durable queue.
Export workers may need to scale independently from the API.
```

Now a reader can evaluate the decision against the constraints that existed.

Context should describe relevant forces, not every fact about the project.

## Alternatives Matter Because the Decision Was Not Inevitable

Recording only the selected option can make an architectural choice look obvious in hindsight.

It may not have been obvious when the decision was made.

Meaningful alternatives show the boundary of the decision.

For example:

```text
Alternative A — Keep export work inside the HTTP request.
Benefit: smallest deployment surface.
Cost: long request duration and poor restart behavior.

Alternative B — Use an in-process background queue.
Benefit: no separate worker deployment.
Cost: queued work is coupled to the API process lifecycle.

Alternative C — Use a durable queue and separate worker.
Benefit: independent lifecycle and scaling.
Cost: additional infrastructure and operational complexity.
```

The alternatives section should not contain obviously bad options added only to make the selected choice look better.

It should preserve the serious choices the team actually had.

## Consequences Matter Because Every Architecture Has a Cost

A useful ADR does not end with "Decision: use option C."

The decision changes what the team must now maintain, operate, test, secure, or explain.

Consequences can be positive:

- Better failure isolation.
- Clearer ownership.
- Independent scaling.
- Easier testing of a boundary.

They can also be negative:

- Additional infrastructure.
- New deployment units.
- More operational monitoring.
- Eventual consistency.
- Migration cost.
- Vendor or framework coupling.

A mature ADR records both.

If the consequences section reads like a product advertisement, the record is probably hiding the most useful information.

## Add Review Conditions Without Predicting the Future

A decision is made under current assumptions.

Those assumptions can change.

A lightweight review-condition section can preserve what would make reconsideration reasonable:

```text
Review this decision if:

- export jobs become short enough to complete reliably in-request;
- the organization retires the durable queue;
- the worker becomes the dominant operational cost; or
- a platform capability removes the current restart/retry limitation.
```

Review conditions do not mean the decision expires automatically.

They identify the evidence that would justify opening the reasoning again.

Continue with [Architecture Decision Record Lifecycle, Review, Deprecation, and Supersession](architecture-decision-record-lifecycle-review-deprecation-and-supersession.md) for the detailed lifecycle; the important point here is that an accepted decision should not be mistaken for an eternal law.

## Example: A Small but Complete ADR

The following fictional example shows the amount of detail needed to preserve the decision without turning the ADR into a design document.

```markdown
# ADR-0007: Run invoice export jobs in a separate worker

## Status

Accepted

## Context

Invoice exports can take several minutes and must survive API restarts.
Keeping HTTP requests open until an export finishes creates timeout and retry
problems. The organization already operates a durable queue, and export load
may need to scale independently from normal API traffic.

## Alternatives Considered

1. Run the export synchronously in the HTTP request.
2. Queue the export to an in-process background service.
3. Publish the job to the existing durable queue and process it in a separate worker.

## Decision

The API will validate and enqueue export requests. A separate worker will own
export execution and durable retry behavior.

## Consequences

Positive:
- API request duration no longer depends on export duration.
- Export workers can scale and restart independently.
- Durable queueing provides a recoverable handoff boundary.

Negative:
- The system gains another deployable component.
- Completion becomes asynchronous.
- The API and worker need correlation, idempotency, monitoring, and failure handling.

## Follow-up or Review Conditions

Review this decision if export duration and volume become consistently small,
if the queue platform is retired, or if operating the worker becomes more costly
than the failure isolation it provides.
```

Notice what the example does not contain.

It does not document queue client APIs, deployment commands, class names, dashboard locations, or retry configuration details.

Those belong in implementation and operations documentation.

The ADR preserves the architectural choice that gives those implementation details a reason to exist.

## Write for the Future Maintainer

A useful ADR should allow a future maintainer to answer:

- What pressure existed?
- Which constraints mattered?
- Which alternatives were credible?
- What did the team choose?
- What did the team knowingly give up?
- Which assumptions may no longer be true?

Avoid relying on phrases such as:

> This is best practice.

or:

> This is more enterprise-ready.

Those statements hide reasoning rather than preserve it.

Replace them with concrete constraints and consequences.

For example:

> The application must preserve queued work across process restarts, so an in-memory queue does not satisfy the recovery requirement.

That is reviewable evidence.

## Keep ADRs Lightweight

ADRs lose value when teams turn them into a ceremony for every meaningful pull request.

Warning signs include:

- Every code change requires an ADR regardless of consequence.
- The template is longer than the decision being recorded.
- Teams delay small reversible decisions while waiting for formal architecture approval.
- ADRs duplicate implementation guides instead of recording reasoning.
- Alternatives are invented after the fact only to complete a template.
- Records become so long that maintainers stop reading them.

A useful ADR is often short.

Its quality comes from the specificity of its reasoning, not its length.

The goal is **concise architectural evidence**.

## Working Reference: NetCoreApplicationTemplate

`AsiBackbone/NetCoreApplicationTemplate` provides a real repository specimen for ADR organization.

Its [ADR index](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/adr/index.md) describes ADRs as records of long-term architectural decisions, identifies examples of decisions worth recording, and distinguishes ADRs from code comments, API documentation, and implementation guides.

Its [ADR template](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/adr/template.md) uses a compact structure built around:

```text
Status
Context
Decision
Consequences
Alternatives Considered
Related References
```

Learning adds **follow-up or review conditions** as a useful optional prompt because future reconsideration is easier when the original assumptions are visible.

The NCAT structure is a working convention, not a required Learning format.

Use it as a specimen of a repository preserving architectural reasoning, not as proof that every project needs the same numbering scheme, headings, or status vocabulary.

## Related Learning Material

Several existing ASP.NET Core lessons contain architectural choices that a real project might decide to preserve in ADRs:

- [Middleware Ordering Changes Behavior](middleware-ordering-changes-behavior.md) — order-sensitive pipeline structure and coverage goals.
- [Secure-by-Default ASP.NET Core Configuration](secure-by-default-configuration.md) — configuration ownership, explicit opt-in, and failure behavior.
- [Structured Logging Without Sensitive-Data Sprawl](structured-logging-without-sensitive-data-sprawl.md) — logging boundaries, event identity, and sensitive-data constraints.
- [Data-Access Boundaries and Transaction Reasoning with EF Core](data-access-boundaries-and-transaction-reasoning.md) — persistence abstractions, transaction boundaries, and external-side-effect failure windows.

Those lessons explain architectural problems in general.

An ADR records how one implementation team resolved one of those problems under its own constraints.

## Quick Decision Test

Before creating an ADR, ask:

1. Is there a real architectural decision, or only an implementation task?
2. Are there multiple reasonable choices or non-obvious constraints?
3. Will the choice shape boundaries, operations, security, data, deployment, or long-term maintainability?
4. Could a future maintainer reasonably question why the system has this shape?
5. Can the reasoning be captured concisely without duplicating implementation documentation?

If most answers are yes, an ADR is probably useful.

If most are no, use the lighter documentation artifact that fits the change.

## Summary

Architecture Decision Records are valuable because software preserves implementation more reliably than it preserves intent.

A useful ADR keeps enough evidence for a future reader to recover:

```text
Pressure
   ↓
Constraints
   ↓
Alternatives
   ↓
Decision
   ↓
Consequences
   ↓
Review conditions
```

The format can vary.

The durable requirement is that the reasoning survives.

---

> **Record the decision because the code will remember what you built, not why you built it.**
