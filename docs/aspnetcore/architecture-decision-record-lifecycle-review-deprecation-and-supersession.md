# Architecture Decision Record Lifecycle, Review, Deprecation, and Supersession

**Pattern classification:** General Learning Material

**Difficulty:** Intermediate

**Prerequisites:** Read [Architecture Decision Records Preserve Architectural Reasoning](architecture-decision-records-preserve-architectural-reasoning.md) first. Familiarity with version-controlled documentation and architectural tradeoffs is helpful.

**Learning objective:** Understand an ADR as a record of a decision made under particular assumptions rather than as permanent doctrine; recognize when a decision should be reviewed; distinguish proposed, accepted, deprecated, and superseded states; preserve historical reasoning while changing the active architecture; and detect drift between ADR state and implementation.

> **Working-repository follow-on:** Use [Working Repository ADR Case Study: NetCoreApplicationTemplate](netcoreapplicationtemplate-adr-case-study.md) to inspect how accepted ADRs map into current code and configuration, and how implementation drift can become evidence for review.

## Pattern Card

> **Problem:** Architectural decisions can outlive the assumptions that justified them. If teams treat accepted ADRs as immutable truth, silently edit historical records, delete obsolete decisions, or let implementation drift without updating the record, future maintainers lose both the current architecture and the reasoning that produced it.
>
> **Pattern:** Treat ADRs as durable historical records with an explicit lifecycle. Preserve the original decision and its context, revisit it when evidence or assumptions change, record the new decision separately when the architecture changes materially, and make relationships among current, deprecated, and superseded decisions visible.
>
> **Use when:** A previously recorded architectural choice is affected by changed requirements, trust boundaries, operational evidence, platform capabilities, major dependencies, deployment constraints, or another material assumption.
>
> **Prefer something simpler when:** The implementation change does not alter the architectural decision, the original reasoning remains valid, and a normal code change, documentation correction, or implementation-guide update is enough.
>
> **Observe:** A healthy ADR history lets a future maintainer answer both “What is the current decision?” and “Why did the previous decision make sense when it was made?”

## Accepted Does Not Mean Eternal

An accepted ADR records a decision that the team has adopted.

It does not convert the decision into a permanent law.

Architecture is chosen under conditions that can change:

```text
Requirements
Security assumptions
Operational evidence
Platform capabilities
Team constraints
Deployment model
Cost profile
        ↓
Architectural decision
```

If the conditions change, the old decision may deserve review.

That is not evidence that the original ADR failed.

A good ADR can remain historically correct even after the architecture changes.

For example:

```text
2025
Single application instance
Low background-job volume
No durability requirement
        ↓
Accepted decision:
Use an in-process queue
```

Later:

```text
2027
Multiple application instances
Higher job volume
Durability requirement added
        ↓
Original assumptions no longer hold
        ↓
Decision reviewed
```

The later architecture may be different without making the earlier record dishonest.

The ADR preserves what was reasonable under the earlier constraints.

## A Common ADR Lifecycle

Many teams use a small status vocabulary such as:

```text
Proposed
   ↓
Accepted
   ↓
Deprecated
or
Superseded
```

The exact words are conventions rather than a universal standard.

What matters is that the repository makes the meaning of each state clear.

A practical interpretation is:

| Status | Meaning |
| --- | --- |
| **Proposed** | The decision is under consideration and has not yet become the active architectural direction. |
| **Accepted** | The decision is currently adopted and should describe the architecture the team intends to follow. |
| **Deprecated** | The decision is no longer recommended or is being retired, but there may be no direct replacement ADR. |
| **Superseded** | A later ADR replaces the decision with a new architectural direction. |

Other teams may use terms such as `Rejected`, `Obsolete`, `Amended`, or `Cancelled`.

The vocabulary can vary.

The important property is that a reader can distinguish historical decisions from current ones without guessing.

## Proposed

A proposed ADR captures a decision that is still being evaluated.

This status can be useful when:

- A change is consequential enough that the reasoning should be reviewed before implementation.
- Multiple teams need to evaluate the same architectural choice.
- Security, operations, data, or platform owners need to comment before adoption.
- The implementation should not begin until a particular assumption is validated.
- The decision may be rejected without becoming part of the active architecture.

A proposed ADR should still contain real reasoning.

It should not be an empty placeholder whose only purpose is to reserve a number.

If the proposal is rejected, the team can preserve that outcome according to its repository convention.

Some teams keep rejected ADRs because the rejected reasoning may prevent the same debate from being repeated later.

Others record the rejected option in the accepted ADR that followed.

Either approach can work if the historical reasoning remains understandable.

## Accepted

An accepted ADR represents the active architectural decision.

That does not mean every implementation detail belongs in the ADR.

It means the chosen direction is now part of the architecture.

An accepted ADR should answer:

- What problem required a decision?
- Which constraints mattered?
- Which alternatives were credible?
- What was selected?
- What consequences were knowingly accepted?
- Which assumptions or evidence might justify later review?

The implementation, tests, deployment configuration, and operational documentation should then reflect that direction.

This relationship matters:

```text
Accepted ADR
     ↓
Expected architectural direction
     ↓
Implementation and operations
```

If implementation and ADR state diverge, the team has a documentation or architecture problem to investigate.

## Deprecated Is Not the Same as Superseded

These states are related but not identical.

### Deprecated

A deprecated ADR describes a decision that is no longer recommended or is intentionally being retired, but there may not be a replacement architectural decision.

For example:

```text
ADR-0012
Support legacy protocol bridge
Status: Accepted
```

Later, the organization decides that the legacy protocol must be removed entirely.

The capability is going away rather than being replaced by a new protocol architecture.

The record might become:

```text
ADR-0012
Support legacy protocol bridge
Status: Deprecated
```

The useful history remains:

- Why the bridge existed.
- Which constraints once justified it.
- Why the decision is no longer recommended.
- What retirement work may still remain.

### Superseded

A superseded ADR has been replaced by another ADR.

For example:

```text
ADR-0004
Use an in-process queue
Status: Accepted
```

Later:

```text
ADR-0009
Use a durable external queue
Status: Accepted
```

The original record becomes:

```text
ADR-0004
Status: Superseded by ADR-0009
```

Conceptually:

```text
ADR-0004
Accepted
   ↓
Assumptions change
   ↓
Decision reviewed
   ↓
ADR-0009
Accepted
   ↓
ADR-0004
Superseded by ADR-0009
```

A replacement relationship exists.

That is what makes supersession different from simple deprecation.

## Preserve the Old Record

When a decision is superseded, deleting the old ADR usually destroys useful evidence.

The original record may explain:

- Why the earlier architecture existed.
- Which constraints were important at the time.
- Which alternatives had already been evaluated.
- Which operational or security tradeoffs were accepted.
- Why the implementation looked the way it did before migration.
- Which assumption later stopped being true.

A future maintainer investigating an old commit, incident, deployment, or migration may need that information.

Preservation therefore matters even when the decision is no longer current.

This distinction is useful:

```text
Preserved
does not mean
Current
```

A superseded ADR can be historically important and operationally obsolete at the same time.

## Do Not Rewrite History to Match the Present

Suppose an accepted ADR originally said:

```text
Decision:
Use an in-process queue because the application runs as one instance and
queued work does not need to survive process restarts.
```

Two years later, the system uses a durable broker.

Silently editing the original ADR to say:

```text
Decision:
Use a durable broker because jobs must survive restarts.
```

makes the repository look cleaner.

It also destroys the historical reasoning.

A reader can no longer tell:

- Why the in-process design existed.
- When the requirement changed.
- What evidence triggered the new direction.
- Which implementation era the original ADR described.
- Whether the architecture evolved deliberately or accidentally.

The better pattern is:

```text
ADR-0004
Original decision preserved
Status changed to Superseded
Link to ADR-0009

ADR-0009
New context
New alternatives
New decision
New consequences
```

The historical chain remains visible.

## Corrections Are Different from Rewriting a Decision

Preserving history does not mean an accepted ADR can never be edited.

Small maintenance changes can be appropriate, such as:

- Fixing a spelling error.
- Repairing a broken link.
- Clarifying an ambiguous sentence without changing the decision.
- Adding a link to the implementing pull request.
- Adding the explicit supersession link after a replacement is accepted.

The key question is:

> Does this edit change what a future reader would believe the team decided or why it decided it?

If yes, treat the change as architectural history rather than a documentation cleanup.

A new ADR, explicit amendment, or another traceable lifecycle mechanism is usually more honest than silent revision.

## What Should Trigger a Review?

An ADR should not be revisited merely because it is old.

Age alone is weak evidence.

Review becomes useful when something material has changed.

### Changed Requirements

A decision may have been correct for the original product requirements and wrong for the current ones.

Examples:

- A local-only tool becomes a multi-tenant service.
- A best-effort process becomes subject to a durability requirement.
- A low-volume workload becomes latency-sensitive at scale.
- Data that was previously non-sensitive becomes regulated or contractually restricted.

The review question is:

> Does the old decision still satisfy the system we are actually building?

### Changed Security or Trust Assumptions

Security boundaries are architectural inputs.

A review may be justified when:

- A component now receives untrusted input that was previously internal.
- Credentials move across a new process or network boundary.
- A service becomes internet-facing.
- A new tenant or regional isolation requirement appears.
- An attacker model changes.
- A dependency or integration no longer deserves the same level of trust.

The old ADR may remain historically sound.

Its trust assumptions may simply no longer match reality.

### Operational Evidence

Production behavior can invalidate an architectural assumption.

Examples:

- A retry strategy causes duplicate side effects.
- An in-memory queue loses unacceptable work during restarts.
- A database abstraction creates measurable contention.
- A middleware arrangement produces observability gaps.
- A dependency becomes a dominant source of incidents.
- A supposedly rare failure mode becomes common.

Operational evidence is especially useful because it replaces speculation with observed behavior.

A revisit should preserve that evidence rather than summarize it as:

> The old design did not work.

A stronger record says what failed, under which conditions, and why the evidence matters to the architectural decision.

### New Framework or Platform Capabilities

A tradeoff may become obsolete when the platform changes.

For example:

- A framework adds a built-in capability that previously required custom infrastructure.
- A managed service removes a reliability burden the team once had to own.
- A runtime adds a security feature that changes an earlier threat model.
- A deployment platform gains a primitive that makes a custom workaround unnecessary.

The review question is not:

> Is the new feature newer?

It is:

> Does the new capability change the costs, risks, or constraints that justified the earlier decision?

### Changed Organizational Constraints

Architecture is also shaped by who can operate it.

A decision may deserve review when:

- Ownership moves to another team.
- A specialist capability is no longer available.
- A platform becomes organization-standard.
- Support or licensing cost changes materially.
- Deployment or compliance responsibilities move across organizational boundaries.

These are real architectural forces.

They should be recorded as such rather than hidden behind vague language about “modernizing.”

## Record the Trigger, Not Just the New Preference

When reopening a decision, preserve the evidence that caused the review.

A useful review note or replacement ADR can capture:

```text
Trigger
   ↓
Changed assumption or new evidence
   ↓
Impact on old decision
   ↓
Alternatives reconsidered
   ↓
New decision or retained decision
```

For example:

```text
Trigger:
Three production restarts in the last quarter discarded queued notification work.

Changed assumption:
Background work can no longer be treated as disposable.

Impact:
The accepted in-process queue decision no longer satisfies the durability requirement.

Review outcome:
Evaluate durable external queueing versus database-backed scheduling.
```

That is much more useful than:

> We decided to use a message broker.

The trigger explains why the architecture moved.

## Review Can Confirm the Existing Decision

Revisiting an ADR does not require changing it.

A review can conclude:

```text
Assumptions checked
      ↓
Alternatives reconsidered
      ↓
Current decision still fits
      ↓
ADR remains Accepted
```

That outcome is valuable when the review was triggered by real evidence or changed conditions.

For example, a platform may introduce a new built-in feature, but evaluation may show that it does not satisfy the application's isolation or operational requirements.

The accepted ADR can remain current.

The team may record a dated review note, issue, pull request, or related reference depending on its convention.

Do not create a replacement ADR merely to prove that a review happened.

## Implementation and ADR State Can Drift

An ADR repository can become misleading when the recorded decision and the running system disagree.

Common drift patterns include:

| ADR state | Implementation state | Possible interpretation |
| --- | --- | --- |
| Accepted | Matches ADR | Expected alignment. |
| Accepted | Has materially diverged | Unrecorded architecture change, stale ADR, or implementation defect. |
| Superseded | Still fully implements old decision | Migration may be incomplete or the supersession may have been premature. |
| Deprecated | New use continues to expand | Deprecation is not being enforced or understood. |
| Proposed | Implementation already depends on it | The process may be documenting an already-made decision after the fact. |

The table does not determine the answer automatically.

It identifies where investigation is needed.

A healthy review asks:

1. Is the implementation wrong?
2. Is the ADR state wrong?
3. Did the architecture change without a recorded decision?
4. Is the system in an intentional migration period?
5. Does the repository need an explicit transition note?

The goal is not documentation perfection.

The goal is avoiding a false architectural history.

## Migration Periods Need Honest States

Architectural replacement is often gradual.

A system can temporarily contain both the old and new approaches:

```text
Old architecture
      ↓
Migration begins
      ↓
Old + new coexist
      ↓
Migration completes
      ↓
New architecture only
```

The ADR history should not pretend that the new architecture is fully deployed if it is not.

Useful approaches include:

- Accepting the replacement ADR while documenting migration scope.
- Marking the old ADR as superseded but noting that legacy implementation remains during transition.
- Linking to a migration issue or plan.
- Defining the condition that marks transition complete.

The exact convention is less important than making the temporary state visible.

## Periodic Review Without ADR Ceremony

Teams sometimes respond to lifecycle concerns by scheduling a formal review of every ADR every quarter.

That can create more process than value.

A lighter approach is usually better.

### Event-Driven Review

Review when a meaningful trigger appears:

- A requirement changes.
- A trust boundary changes.
- Operational evidence contradicts an assumption.
- A major platform capability appears.
- A critical dependency changes.
- A migration exposes unresolved architectural questions.

This keeps review connected to evidence.

### Periodic Review

Periodic review can still be useful for decisions that are:

- High consequence.
- Security-sensitive.
- Expensive to reverse.
- Dependent on rapidly changing external platforms.
- Subject to contractual or regulatory change.
- Easy to forget because the implementation rarely changes.

Even then, the review should ask a focused question:

> Are the assumptions and consequences that justified this decision still true enough?

It should not become a ritual that rewrites dates and status labels without new information.

## A Worked Lifecycle Example

Consider a fictional application that sends customer notifications.

### ADR-0004 — Initial Decision

```markdown
# ADR-0004: Use an in-process notification queue

## Status

Accepted

## Context

The application runs as one instance.
Notification volume is low.
Notifications are best effort and may be lost during a process restart.
The team wants to avoid operating an external broker.

## Decision

Queue notifications in memory and process them with a hosted background service.

## Consequences

Positive:
- Minimal infrastructure.
- Simple local development.
- No external queue dependency.

Negative:
- Queued work is coupled to the application process.
- Restarting the process can discard pending notifications.

## Review Conditions

Review if notification loss becomes unacceptable, the application scales to
multiple instances, or the workload requires independent retry and monitoring.
```

At the time, the decision is coherent.

### Conditions Change

A year later:

```text
Application now runs on four instances.
Customers rely on delivery confirmation.
Operations reports lost notifications during rolling restarts.
A managed durable queue is now available on the standard platform.
```

Every important assumption in ADR-0004 deserves review.

### Review

The team evaluates:

1. Keep the in-process queue and accept loss.
2. Persist queued work in the application database.
3. Use the managed durable queue.

The review is driven by changed requirements, operational evidence, and a new platform capability.

### ADR-0009 — Replacement Decision

```markdown
# ADR-0009: Use the managed durable queue for customer notifications

## Status

Accepted

## Context

Notification loss is no longer acceptable.
The application runs across multiple instances.
Rolling restarts have caused lost in-memory work.
The standard platform now provides a managed durable queue.

## Decision

Publish notification work to the managed durable queue and process it through
independently scalable workers.

## Consequences

Positive:
- Work survives application restarts.
- Multiple API instances share one durable work source.
- Retry and queue depth become operationally visible.

Negative:
- The system gains an external dependency.
- Delivery semantics, idempotency, poison-message handling, and queue access
  controls must be managed.
```

ADR-0004 is then updated only enough to make its lifecycle visible:

```text
Status: Superseded by ADR-0009
```

The old context remains.

The repository now tells the full story:

```text
ADR-0004
Why in-process queueing once made sense
        ↓
Changed requirements + observed loss + new platform capability
        ↓
ADR-0009
Why durable queueing became the new decision
```

That history is more useful than one continuously edited file.

## Deprecation Example Without a Replacement

Supersession is not always the right outcome.

Imagine an ADR that established a temporary compatibility bridge for a legacy protocol.

Later, the organization stops accepting that protocol entirely.

There is no replacement bridge.

The capability is being removed.

The original ADR can become:

```text
Status: Deprecated
Reason: Legacy protocol support is being retired.
Retirement tracking: <issue or migration reference>
```

The distinction is:

```text
Deprecated
   ↓
Decision no longer recommended / being retired
   ↓
No direct replacement required
```

versus:

```text
Superseded
   ↓
Decision replaced
   ↓
New ADR becomes the active direction
```

## Link Superseded ADRs in Both Directions When Practical

A reader who opens the old ADR should be able to find the replacement.

A reader who opens the replacement should be able to discover the history it replaced.

Useful links are:

```text
ADR-0004
Superseded by ADR-0009
        ↕
ADR-0009
Supersedes ADR-0004
```

Bidirectional linkage is especially helpful in large repositories where file numbering alone does not explain relationships.

The links can be placed in status text, related references, or another repository convention.

Consistency matters more than the exact location.

## Keep the Lifecycle Separate from Implementation Documentation

An ADR lifecycle answers questions such as:

- Is this decision current?
- Why was it reconsidered?
- Which decision replaced it?
- What evidence caused the change?

An implementation guide answers different questions:

- Which package is configured?
- Which service owns the connection?
- How is retry configured?
- Which deployment setting must be present?
- How is migration executed?

The lifecycle record should not become a changelog of every implementation step.

A concise relationship is:

```text
ADR history
   ↓ why the architecture changed
Migration plan
   ↓ how the transition will occur
Implementation docs
   ↓ how the current system works
Code and configuration
   ↓ enact the architecture
```

Keeping those roles separate makes each artifact easier to maintain.

## Working Reference: NetCoreApplicationTemplate

`AsiBackbone/NetCoreApplicationTemplate` provides a working repository convention for ADR lifecycle state.

Its [ADR index](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/adr/index.md) defines:

```text
Proposed
Accepted
Deprecated
Superseded
```

and states that a superseded ADR should normally be preserved and linked to its replacement rather than deleted.

That is a useful specimen because it makes historical and current decisions distinguishable.

It is not a universal lifecycle standard.

A different repository may use different status words or a different organization as long as the current decision and historical reasoning remain recoverable.

The later Learning case-study material can examine individual implementation ADRs in more detail; this article uses NCAT only as a lifecycle convention specimen.

## Review Checklist

When reconsidering an ADR, ask:

1. What assumption, requirement, trust boundary, evidence, or platform capability changed?
2. Does the old decision still satisfy the current system?
3. Is the problem architectural, or is this only an implementation change?
4. Which alternatives are now credible?
5. Has the cost or risk profile changed?
6. Should the current ADR remain accepted?
7. If the decision is no longer recommended, is it deprecated or actually replaced?
8. If replaced, does the new ADR link to the old one?
9. Does the old ADR link forward to the replacement?
10. Does implementation currently match the recorded lifecycle state?
11. Is there a migration period that should be documented explicitly?
12. Can the old reasoning remain intact rather than being rewritten to match the present?

If the team can answer those questions, the lifecycle is probably preserving the right evidence.

## Summary

Architecture Decision Records are not permanent commands.

They are historical records of choices made under particular constraints.

A useful lifecycle keeps both current direction and historical reasoning visible:

```text
Proposed
   ↓
Accepted
   ↓
Assumptions or evidence change
   ↓
Review
   ↓
Retain
or
Deprecated
or
Superseded
   ↓
History preserved
```

The key rule is simple:

> **Change the architecture when the evidence requires it, but do not erase the reasoning that explains how the system arrived there.**

---

> **Preserve the old decision as history; make the new decision explicit as architecture.**
