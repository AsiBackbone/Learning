---
description: Learn to classify rejected AI proposals, preserve model uncertainty, and apply bounded recovery without weakening host validation, policy, or execution authority.
---

# AI Proposal Rejection, Uncertainty, and Recovery Patterns

**Learning objective:** Understand how an AI-assisted host can distinguish invalid model output from valid-but-disallowed proposals, preserve uncertainty explicitly, return bounded corrective feedback, enforce retry and loop limits, and choose recovery actions without turning repeated model attempts into a path around governance.

**Pattern classification:** General learning material

**Difficulty:** Advanced

**Prerequisites:** [Typed AI Proposed Intent and Schema-Validation Boundaries](typed-ai-proposed-intent-and-schema-validation-boundaries.md), [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md), and [Deterministic and Probabilistic Inputs in Policy Evaluation](../governance/deterministic-and-probabilistic-inputs-in-policy-evaluation.md).

## At a Glance

> **Problem:** An AI proposal may fail parsing, schema validation, host resolution, governance, acknowledgment, escalation, or infrastructure checks. If every failure simply sends the request back to the model, retries can become unbounded, repetitive, or progressively shaped to discover and bypass hidden boundaries.
>
> **Core idea:** Classify the failed stage, preserve a stable machine-readable reason, and let a host-owned recovery policy choose among correction, replanning, missing-input collection, bounded retry, escalation, or termination.
>
> **Why it matters:** Rejection is part of the control boundary. A retry should not receive broader authority merely because an earlier proposal failed.
>
> **Prefer something simpler when:** Model output is advisory only, no consequential tool or workflow can execute, or an ordinary request validator and fixed application error flow already provide the necessary bounded recovery behavior.
>
> **Observe:** A rejected attempt produces no protected side effect, repeated equivalent failures consume a finite retry budget, and later proposals still pass the same host-owned validation and governance boundaries.

The central lesson is:

> **Rejection should constrain the next proposal; it should not create a retry loop that gradually erodes the original boundary.**

A useful recovery flow is:

```text
Proposal
   ↓
Host validation / governance
   ↓
Cannot proceed
   ↓
Stable failure category + bounded reason
   ↓
Host-owned recovery policy
   ├── Correct and retry
   ├── Re-plan
   ├── Request missing human input
   ├── Wait / back off
   ├── Escalate
   └── Terminate
```

Avoid:

```text
Rejected
   ↓
Ask model again
   ↓
Rejected
   ↓
Ask model again
   ↓
...
   ↓
Eventually something passes
```

That is not a governance strategy.

---

## Rejection Is Not One State

A host can fail to accept a proposal at several different boundaries.

Those failures should remain distinguishable because they imply different recovery actions.

| Stage | Example | What failed | Typical recovery direction |
| --- | --- | --- | --- |
| Parse | `proposal.parse.invalid-json` | The representation could not be read | Correct formatting if attempts remain |
| Schema | `proposal.schema.missing-argument` | The proposal contract was not satisfied | Correct the proposal using allowed contract information |
| Registry | `proposal.operation.unknown` | The host does not expose that operation | Re-plan using an exposed semantic tool or terminate |
| Semantic validation | `proposal.semantic.invalid-range` | Accepted fields conflict or are nonsensical | Correct values or request missing input |
| Host resolution | `proposal.resource.ambiguous` | The host cannot identify a unique resource | Request disambiguating input rather than guessing |
| Host context | `proposal.context.required-fact-unavailable` | A required authoritative fact is missing | Defer, retrieve the fact, escalate, or terminate |
| Probabilistic input | `proposal.signal.low-confidence` | An observed signal is too uncertain for the current policy path | Apply threshold policy, seek stronger evidence, or escalate |
| Governance | `policy.denied` | A valid proposal was evaluated and prohibited | Usually terminate this proposal; re-plan only if a materially different legitimate action exists |
| Governance | `policy.deferred` | Policy cannot conclude or proceed now | Wait for the stated condition or context change |
| Governance | `policy.acknowledgment-required` | A human acknowledgment boundary is required | Enter the host-owned acknowledgment workflow |
| Governance | `policy.escalation-recommended` | Another authority or review path is required | Enter the defined escalation workflow |
| Infrastructure | `dependency.unavailable` | A required host dependency is unavailable | Bounded retry/backoff, defer, degrade explicitly, or terminate |
| Execution boundary | `capability.invalid-or-expired` | Earlier authority is not valid for this execution attempt | Do not execute; re-evaluate from current state if the workflow permits |

The table is not a universal error taxonomy.

The important design choice is to keep the stage visible.

A parser error should not look like a policy denial.

A policy denial should not look like a transient dependency timeout.

An acknowledgment requirement should not look like model uncertainty.

---

## Preserve Two Important Distinctions

### Invalid proposal versus rejected valid proposal

These statements are different:

```text
Model could not produce a valid proposal
```

and:

```text
Host policy rejected a valid proposal
```

For example:

```text
operation = notification.send
recipient = missing
```

may fail schema validation before policy runs.

By contrast:

```text
operation = notification.send
recipient = external@example.test
schema = valid
host context = valid
policy = Denied
```

means the proposal reached governance successfully and the governance result blocked it.

Do not flatten both into:

```text
AI request failed
```

when the architecture needs to know whether model correction is even relevant.

### Low confidence versus no authority

These statements are also different:

```text
Low confidence
```

and:

```text
No authority
```

Low confidence describes uncertainty in an observation or inference.

No authority describes the absence of valid permission to perform a side effect.

They are orthogonal.

A system can have:

```text
High-confidence model classification
        +
No execution authority
```

or:

```text
Low-confidence model signal
        +
Actor is otherwise authorized
        +
Policy still requires escalation before execution
```

Do not use confidence as a substitute for authorization.

Do not describe a denied operation as though the model merely needs to become more confident.

---

## Recovery Policy Belongs to the Host

The model can propose a corrected or revised action.

The host should decide whether another attempt is permitted and what kind of recovery is appropriate.

A framework-neutral recovery disposition might be:

```csharp
public enum RecoveryDisposition
{
    CorrectAndRetry,
    Replan,
    RequestHumanInput,
    AwaitAcknowledgment,
    WaitAndRetry,
    Escalate,
    Terminate
}
```

A recovery directive can remain explicit:

```csharp
public sealed record RecoveryDirective(
    RecoveryDisposition Disposition,
    string ReasonCode,
    int AttemptNumber,
    int AttemptsRemaining,
    TimeSpan? RetryAfter,
    string? SafeModelMessage);
```

The host owns:

- Whether another attempt is allowed.
- Which failure categories are retryable.
- The maximum attempt count.
- Whether backoff is required.
- Whether the next attempt must be a correction or a re-plan.
- Whether a human must provide missing information.
- Whether escalation is allowed.
- Which terminal state is recorded when recovery ends.

The model does not choose its own retry budget.

---

## Use Stable Machine-Readable Reason Codes

Recovery is easier to test when failures use stable codes rather than relying only on prose.

Examples might include:

```text
proposal.parse.invalid-json
proposal.schema.missing-argument
proposal.schema.unsupported-version
proposal.operation.unknown
proposal.argument.unsupported
proposal.semantic.invalid-range
proposal.resource.ambiguous
proposal.context.required-fact-unavailable
proposal.signal.low-confidence
policy.denied
policy.deferred
policy.acknowledgment-required
policy.escalation-recommended
dependency.unavailable
recovery.attempt-budget-exhausted
recovery.repeated-equivalent-proposal
recovery.cancelled
```

The exact names are application-specific.

The useful properties are that reason codes are:

- Stable enough for tests and telemetry.
- Specific enough to identify the failed stage.
- Safe to record without embedding secrets.
- Separate from human-readable diagnostic text.
- Separate from model-visible corrective guidance.

A reason code is evidence about the host's observed outcome.

It is not itself a new authority grant.

---

## Return Only Safe Corrective Information to the Model

A model sometimes needs feedback to repair a malformed proposal.

That does not mean every internal validation or policy detail should be exposed.

A useful separation is:

| Internal evidence | Possible model-visible guidance |
| --- | --- |
| `proposal.schema.missing-argument` + internal validator detail | `Required argument "caseId" is missing.` |
| `proposal.operation.unknown` + registry metadata | `The requested operation is not available. Choose from the tool contract supplied by the host.` |
| `proposal.resource.ambiguous` + internal candidate set | `The resource identifier is ambiguous. Ask the user for a more specific identifier.` |
| `proposal.signal.low-confidence` + model/provenance evidence | `Available evidence is insufficient for this workflow. Do not guess; request another source or human input.` |
| `policy.denied` + sensitive policy reasons | `The proposed operation cannot proceed under current host policy.` |
| `dependency.unavailable` + infrastructure diagnostics | `The required host service is temporarily unavailable. Do not invent substitute facts.` |

Do not automatically return:

- Credentials.
- Secret values.
- Private keys or tokens.
- Hidden authorization thresholds.
- Sensitive tenant or resource details the model did not already have legitimate access to.
- Internal policy source code.
- Security-control bypass conditions.
- Full exception traces from privileged infrastructure.
- Candidate resource lists when disclosure would broaden access.

The host can be helpful without becoming an oracle for probing internal controls.

---

## Correction and Re-Planning Are Different

A **correction** keeps the same intended operation but repairs an invalid representation or argument.

For example:

```text
Attempt 1:
notification.send
missing templateId
        ↓
Schema rejection
        ↓
Attempt 2:
notification.send
templateId = case-update
```

A **re-plan** changes the proposed approach.

For example:

```text
Attempt 1:
account.disable
        ↓
Policy = Denied

Possible re-plan:
case.add-note
        ↓
New proposal
        ↓
Normal host validation and policy
```

The second operation is not allowed merely because the first one failed.

It starts a new proposal path.

A useful invariant is:

```text
Rejected operation A
      ≠
Authority for alternative operation B
```

Re-planning is useful when another legitimate path can satisfy the user's goal with less consequence or different semantics.

It should not be a search algorithm for discovering which restricted operation happens to pass policy.

---

## Do Not Let Policy Denial Become a Prompt-Optimization Loop

Suppose the model proposes:

```text
account.disable(account-123)
```

and the host returns:

```text
Decision = Denied
ReasonCode = account.disable.protected-account
```

A dangerous recovery pattern is:

```text
Denied
   ↓
Reveal exact hidden threshold
   ↓
Model tweaks arguments to fall just below threshold
   ↓
Retry until allowed
```

The safer question is:

> Does the workflow permit a materially different legitimate action, or is this proposal terminal?

Possible recovery may be:

```text
Denied account.disable
        ↓
No retry of same protected action
        ↓
Offer non-mutating case.lookup
or
Escalate through defined human path
or
Terminate
```

Policy denial is not malformed output.

The model should not be trained by the live host to reverse-engineer a bypass.

---

## Model Uncertainty Should Remain Typed Evidence

A model may produce:

```text
classification = suspicious
confidence = 0.54
```

or:

```text
classification unavailable
```

Those are different states.

A useful conceptual distinction is:

```text
Low confidence
=
A signal exists, but uncertainty is high
```

versus:

```text
Unavailable signal
=
No acceptable observation exists for this decision
```

For the broader treatment of probabilistic signals, provenance, calibration, freshness, and threshold policy, see [Deterministic and Probabilistic Inputs in Policy Evaluation](../governance/deterministic-and-probabilistic-inputs-in-policy-evaluation.md).

The recovery-specific rule is:

> **The model should not cure uncertainty by asserting a more confident value on the next attempt unless new evidence actually justifies it.**

Avoid:

```text
confidence = 0.54
        ↓
Host says confidence too low
        ↓
Model retries with confidence = 0.91
        ↓
Host accepts
```

when no new evidence was introduced.

Confidence belongs to the observation and its provenance.

It is not a negotiation field.

---

## Host-Authoritative Context Conflicts Are Not Model Errors to Repair

Suppose the model proposes:

```text
Resource classification = Public
```

while the host resolves:

```text
Resource classification = Restricted
```

The host should use the authoritative value.

The recovery path is not necessarily:

```text
Tell model to retry with Restricted
```

The more important boundary is:

```text
Model claim
      ↓
Host resolves authoritative fact
      ↓
Governance uses host fact
```

If the resource cannot be resolved at all, that may produce a host-resolution failure.

If the resource is resolved and policy denies the operation, that is a governance result.

Do not send authoritative security-sensitive facts back to the model merely to make the next proposal appear consistent.

---

## Treat Governance Outcomes According to Their Meaning

A valid proposal can produce several non-executable governance outcomes.

They should not all trigger another model call.

### Denied

```text
Denied
   ↓
No capability
   ↓
No protected execution
```

Retry only if the next proposal represents a materially different, legitimate operation rather than a cosmetic mutation intended to reach `Allowed`.

### Deferred

A deferred decision may mean the host is waiting for:

- Fresh authoritative context.
- A dependency to recover.
- A scheduled condition.
- A required state transition.
- Another system-of-record update.

A model retry does not necessarily help.

The recovery policy may wait and re-evaluate the same host-owned intent after the relevant condition changes.

### Acknowledgment required

An acknowledgment-required outcome should enter the host-owned acknowledgment workflow.

The model should not satisfy the human boundary on the user's behalf.

After acknowledgment, refresh authoritative context and re-evaluate when the architecture requires it.

### Escalation recommended

An escalation outcome should enter the defined escalation path.

The model may help summarize the request if permitted.

It should not choose an unauthorized reviewer, grant the reviewer authority, or convert escalation into execution.

See [Escalation Patterns in Governed Systems](../governance/escalation-patterns-in-governed-systems.md) and [Human-in-the-Loop Governance Workflows](../governance/human-in-the-loop-governance-workflows.md) for the broader lifecycle.

---

## Infrastructure Failure Needs Its Own Recovery Policy

A dependency failure is not automatically a model failure.

Examples include:

```text
Resource catalog unavailable
Policy store unavailable
Replay store unavailable
Acknowledgment store unavailable
External API unavailable
Capability verifier unavailable
```

Avoid asking the model to compensate by inventing missing host facts.

For a transient dependency, a host-owned recovery policy might use:

```text
Failure
   ↓
Retryable category?
   ├── no  → terminate / defer / escalate
   └── yes
        ↓
Bounded backoff
        ↓
Refresh host state
        ↓
Retry host operation
```

The model does not necessarily need to be called again.

If the failed dependency owns authoritative state, retrying inference cannot replace it.

For consequential operations, infrastructure failure should not silently broaden authority.

---

## Use Retry Budgets

Every automatic recovery loop should have a stopping condition.

A simple budget can include:

```text
Maximum proposal attempts = 3
Maximum re-plans = 1
Maximum infrastructure retries = 2
Maximum workflow age = 5 minutes
```

Those numbers are examples, not defaults.

The important properties are:

- The limits are host-owned.
- Different failure categories can have different budgets.
- Budget consumption is recorded.
- A successful parse does not reset unrelated governance limits.
- Restarting a process does not silently grant a fresh infinite budget when durable state is required.
- Exhaustion produces an explicit terminal or escalation state.

A retry budget is part of the workflow state, not a suggestion to the model.

---

## Detect Equivalent Retry Loops

Attempt count alone may not reveal a loop quickly enough.

A host can also track a normalized proposal fingerprint such as:

```text
Canonical operation
+
Normalized arguments
+
Relevant schema version
+
Failure stage
+
Failure reason code
```

If the same effective proposal produces the same rejection repeatedly:

```text
Attempt 1 → same fingerprint → denied
Attempt 2 → same fingerprint → denied
Attempt 3 → same fingerprint → denied
```

stop earlier.

A model changing whitespace, rationale text, argument order, or a proposal identifier should not necessarily make the attempt semantically new.

Loop detection should focus on the governed meaning of the proposal.

---

## Backoff Is for Time-Dependent Recovery, Not Policy Shopping

Backoff is useful when time can plausibly change the condition.

Examples include:

- Temporary dependency unavailability.
- Rate limiting.
- Eventually consistent host state.
- Scheduled resource readiness.

Backoff is not useful when:

```text
Policy = Denied
```

and nothing relevant is expected to change.

Do not add delay to a policy-shopping loop and call it safe recovery.

The recovery policy should know why waiting could help.

---

## Request Missing Human Input Instead of Guessing

Some failures are best resolved by asking the user or an authorized operator for a fact the model cannot safely infer.

For example:

```text
User says:
"Update the Acme case."

Host finds:
three cases named Acme
```

Avoid:

```text
Model guesses case-17
```

Prefer:

```text
proposal.resource.ambiguous
        ↓
Request disambiguating human input
        ↓
User selects case-42
        ↓
New typed proposal
        ↓
Normal host validation and governance
```

The human input resolves ambiguity.

It does not automatically authorize the resulting operation.

---

## Example: Correcting a Malformed Proposal

Assume the host exposes:

```text
case.add-note(caseId, note)
```

Attempt 1:

```json
{
  "operation": "case.add-note",
  "arguments": {
    "note": "Customer requested follow-up."
  }
}
```

Host result:

```text
Stage = Schema
ReasonCode = proposal.schema.missing-argument
SafeModelMessage = Required argument "caseId" is missing.
AttemptsRemaining = 1
```

The model may correct the proposal:

```json
{
  "operation": "case.add-note",
  "arguments": {
    "caseId": "case-42",
    "note": "Customer requested follow-up."
  }
}
```

That corrected proposal still proceeds through:

```text
Semantic validation
      ↓
Host resource resolution
      ↓
Governance
      ↓
Possible scoped authority
      ↓
Host-owned execution
```

Correction repaired shape.

It did not create permission.

---

## Example: Unknown Tool Re-Plans to a Narrow Tool

Attempt 1:

```text
Model proposes:
email.execute_arbitrary
```

Host result:

```text
Stage = Registry
ReasonCode = proposal.operation.unknown
Disposition = Replan
```

The model-visible tool contract already exposes:

```text
notification.send
case.add-note
```

The model re-plans:

```text
notification.send
recipient = customer@example.test
templateId = case-update
```

The new proposal does not inherit authority from the failed one.

It must still pass host validation, policy, acknowledgment if required, capability issuance, and execution-boundary validation.

---

## Example: Valid Proposal Is Denied and Terminates

Attempt 1:

```text
account.disable
accountId = protected-account-7
```

Validation:

```text
Parse = valid
Schema = valid
Resource = resolved
Host context = complete
```

Governance:

```text
Outcome = Denied
ReasonCode = account.disable.protected-account
```

Recovery:

```text
Disposition = Terminate
AttemptsRemaining = 0 for this operation
```

Execution:

```text
Protected executor invocation count = 0
```

The model is not invited to mutate the same request repeatedly until a differently shaped disable operation passes.

If policy defines an escalation path, the host may enter that path explicitly instead of terminating.

---

## Example: Low Confidence Requests More Evidence

Suppose a model-derived classification is one input to policy:

```text
Signal = PossibleFraud
Confidence = 0.48
Source = fraud-model-v3
ObservedAt = 10:14:05Z
```

The host policy may define:

```text
Confidence below accepted threshold
      ↓
Do not treat classification as sufficient evidence
      ↓
Request stronger evidence or human review
```

Recovery should not ask the model to repeat the same inference until it reports a larger number.

A legitimate next step might introduce new evidence:

```text
Additional transaction history
      ↓
New model observation with provenance
```

or:

```text
Human review
```

The key difference is that the evidence changed.

The authority boundary did not.

---

## Correlate Attempts Without Treating Them as One Decision

A recovery sequence may contain several proposal attempts.

Use one workflow or correlation identifier to connect them while preserving each attempt separately.

For example:

```text
WorkflowId = workflow-84
CorrelationId = corr-190

Attempt 1
  ProposalId = p-1
  Model = support-model-v4
  Stage = Schema
  Reason = proposal.schema.missing-argument
  Recovery = CorrectAndRetry

Attempt 2
  ProposalId = p-2
  Model = support-model-v4
  Stage = Governance
  Reason = policy.acknowledgment-required
  Recovery = AwaitAcknowledgment
```

Do not overwrite attempt 1 so the final record appears as though the first proposal was always valid.

Decision history matters when diagnosing model behavior, policy behavior, and workflow recovery.

---

## Preserve Model and Version Evidence Where Useful

When model behavior matters to later analysis, an attempt record may preserve:

```text
Model provider or family identifier
Model/deployment version when available
Prompt or policy template version identifier
Proposal schema version
Tool-registry version when relevant
Attempt number
Reason code
Recovery disposition
```

Do not assume every provider exposes an immutable model build identifier.

Record only evidence the host actually has.

Avoid copying full prompts or model responses into ordinary logs by default.

For operational data-minimization guidance, see [Secure Logging Across Trust Boundaries](../security/secure-logging-across-trust-boundaries.md).

---

## Audit Evidence Across Attempts

A minimized recovery record might include:

```csharp
public sealed record ProposalAttemptResidue(
    string WorkflowId,
    string CorrelationId,
    string ProposalId,
    int AttemptNumber,
    string? ModelId,
    string? ModelVersion,
    string? CanonicalOperation,
    string FailureStage,
    string ReasonCode,
    RecoveryDisposition Disposition,
    int AttemptsRemaining,
    DateTimeOffset OccurredAt);
```

If governance ran, preserve the relevant decision provenance separately:

```text
Policy identity
Policy version/hash when available
Outcome
Reason codes
Context fingerprint or resource version when useful
Acknowledgment/escalation references when applicable
```

The audit trail should let a reviewer answer:

```text
What did the model propose?
Which boundary rejected or paused it?
What reason was recorded?
Was another attempt permitted?
What changed before the next attempt?
Did policy run?
Did any protected execution occur?
```

---

## Terminal States Should Be Explicit

A recovery workflow needs a real end.

Possible terminal states include:

```text
Completed
Rejected
Denied
Cancelled
Expired
AttemptBudgetExhausted
Escalated
FailedDependency
InvalidProposal
```

The exact vocabulary is domain-specific.

The important property is that the host can stop asking for more model output.

A terminal state should answer:

- Why the workflow ended.
- Which proposal or decision caused the terminal state.
- Whether any earlier steps succeeded.
- Whether compensation remains pending.
- Whether human follow-up is required.

Termination is a valid governed outcome.

---

## Cancellation Must Stop Recovery Too

Cancellation should not only stop the current tool call.

It should also stop:

- Pending retries.
- Scheduled backoff.
- New model calls for the cancelled workflow.
- Re-planning attempts.
- Unneeded acknowledgments or escalation requests when they can be safely cancelled.

A useful invariant is:

```text
Workflow cancelled
      ↓
No new proposal attempts
      ↓
No new execution authority
```

Already completed side effects may still require explicit compensation or follow-up.

Cancellation does not imply rollback.

---

## Human Escalation After Bounded Failure

Repeated failure may justify human review when the domain supports it.

For example:

```text
Attempt 1 = ambiguous resource
Attempt 2 = ambiguous resource
Retry budget exhausted
        ↓
Escalate to operator
```

The operator may:

- Supply missing authoritative information.
- Reject the workflow.
- Select a supported resource.
- Choose a different allowed operation.
- Invoke a separately authorized override if the domain explicitly supports one.

Human escalation should not be a generic bypass that appears whenever the model cannot satisfy policy.

It is another governed workflow boundary.

---

## Recovery in Multi-Tool Workflows

A multi-step workflow adds another constraint:

```text
Recovery of Step N
      ≠
Authority for Step N+1
```

If a step fails and the model re-plans, the revised step must pass the normal per-step boundary.

If an earlier step already produced a side effect, recovery may need to consider:

- Whether the partial result is acceptable.
- Whether an idempotent retry is possible.
- Whether a compensating operation exists.
- Whether compensation itself needs governance.
- Whether the workflow should terminate before later steps.

See [Governed Multi-Tool Workflows and Recovery Boundaries](governed-multi-tool-workflows-and-recovery-boundaries.md) for the full multi-step treatment.

---

## Testing the Recovery Boundary

Tests should verify both stopping behavior and correction behavior.

### Parse failure

```text
Malformed proposal
      ↓
Parse rejection
      ↓
Policy invocation count = 0
      ↓
Executor invocation count = 0
```

### Schema correction

```text
Attempt 1 = missing argument
      ↓
CorrectAndRetry
      ↓
Attempt 2 = valid schema
      ↓
Still requires normal policy evaluation
```

### Unknown tool

```text
Unknown tool
      ↓
Re-plan using host-exposed tool contract
      ↓
New proposal receives no inherited authority
```

### Policy denial

```text
Valid proposal
      ↓
Policy = Denied
      ↓
No automatic same-operation retry
      ↓
Executor invocation count = 0
```

### Low-confidence signal

```text
Signal confidence below policy threshold
      ↓
Model cannot self-increase confidence
      ↓
New evidence or human review required
```

### Equivalent-loop detection

```text
Same semantic proposal
+
Same rejection reason
+
Retry
      ↓
Loop detected
      ↓
Terminal or escalation state
```

### Retry budget exhaustion

```text
Maximum attempts reached
      ↓
No further model calls
      ↓
No new authority issued
```

### Infrastructure failure

```text
Authoritative dependency unavailable
      ↓
Bounded host retry/backoff
      ↓
Model is not asked to invent missing facts
```

### Correlation continuity

Verify every attempt has its own proposal identifier while the workflow correlation identifier remains stable.

---

## Core Invariants

A recovery design should make these statements testable:

```text
Rejected / failed proposal attempt
      ↓
Protected executor invocation count = 0
```

```text
Attempt N is rejected
      ≠
Broader validation or authority on Attempt N+1
```

```text
Low confidence
      ≠
No authority
```

```text
Retry budget exhausted
      ↓
No additional automatic proposal attempts
```

```text
Policy denial
      ≠
Instruction to search for a proposal that passes
```

```text
New proposal after re-plan
      ↓
Normal host validation + governance again
```

---

## Common Failure Modes

### 1. Retry Until Something Passes

Every rejection is sent back to the model with no stopping condition.

### 2. Validation Details Leak Hidden Policy

The host returns internal thresholds, sensitive classifications, or bypass conditions as corrective guidance.

### 3. Policy Denial Is Treated as Malformed Output

The model is encouraged to mutate a valid denied proposal rather than respect the governance result.

### 4. Confidence Is Negotiated

The model changes a confidence value without new evidence until it crosses a policy threshold.

### 5. Missing Host Facts Are Replaced by Model Guesses

An unavailable authoritative source causes the model to invent tenant, resource, identity, or classification facts.

### 6. Retry Resets Authority

A new proposal identifier is treated as though earlier replay, acknowledgment, expiry, or workflow restrictions no longer matter.

### 7. Re-Planning Inherits Permission

An alternate operation is executed because an earlier operation was considered, not because the alternate operation passed its own boundary.

### 8. Every Failure Invokes the Model Again

Transient infrastructure failures trigger more inference even when no proposal change can resolve the failure.

### 9. Equivalent Proposals Evade Attempt Limits

Whitespace, rationale, or argument-order changes make the same semantic proposal appear new.

### 10. Human Escalation Becomes a Bypass

Any exhausted model loop is routed to a person who can click through without a defined authority model.

### 11. Raw Rejected Output Is Logged Indefinitely

Recovery telemetry becomes a secondary store for prompts, sensitive context, or malformed payloads.

### 12. Terminal Failure Does Not Actually Stop the Workflow

A background scheduler or agent loop continues generating proposals after cancellation, denial, or budget exhaustion.

---

## Tradeoffs

### Benefits

- Failure categories remain observable and testable.
- Model correction can occur without confusing validation with authorization.
- Retry behavior becomes bounded and reviewable.
- Uncertainty remains evidence rather than authority.
- Policy denial is harder to turn into iterative boundary probing.
- Infrastructure failures do not automatically become model retries.
- Human escalation can be introduced deliberately.
- Audit evidence can reconstruct how the workflow changed across attempts.

### Costs

- Recovery state must be modeled explicitly.
- Reason-code taxonomies require maintenance.
- Retry budgets and loop fingerprints add orchestration state.
- Safe model-visible feedback requires careful design.
- Bounded recovery can terminate workflows that a more permissive loop might eventually complete.
- Long-running recovery may require durable state, expiry, and cancellation handling.
- Some domains need operation-specific recovery policies rather than one generic strategy.

The goal is not maximum completion rate.

The goal is predictable recovery that preserves the original authority boundaries.

---

## When a Simpler Recovery Model Is Enough

A dedicated recovery state machine may be unnecessary when:

- The model produces text only and a person decides what to do.
- One non-consequential read-only tool has ordinary validation and one retry.
- A fixed application form already collects missing fields deterministically.
- The host can terminate immediately on invalid output with no user-value loss.

In those cases, a small rule may be enough:

```text
Invalid proposal
      ↓
Return bounded error
      ↓
One correction attempt
      ↓
Terminate if still invalid
```

Use the smallest recovery architecture that keeps failure and authority visible.

---

## Review Questions

When reviewing AI proposal recovery, ask:

1. Can the host distinguish parse, schema, registry, semantic, host-context, governance, infrastructure, and execution-boundary failures?
2. Are stable reason codes preserved independently of human-readable text?
3. Can the model receive enough information to repair malformed output without receiving secrets or hidden policy internals?
4. Does a valid policy denial terminate or constrain retry rather than start policy shopping?
5. Are low confidence and unavailable evidence represented differently?
6. Can the model increase a confidence value without new evidence?
7. Does the host own retry budgets and maximum attempts?
8. Are equivalent semantic retries detected?
9. Is backoff used only when time or dependency recovery can plausibly change the result?
10. Can ambiguous resources be resolved through human input rather than model guessing?
11. Are acknowledgment and escalation entered as explicit host workflows?
12. Can infrastructure failures recover without unnecessarily calling the model again?
13. Does re-planning create a new proposal that must pass normal validation and policy?
14. Can cancellation stop future retries and model calls?
15. Does the audit trail preserve each attempt while maintaining one workflow correlation?
16. Are model/version identifiers recorded only when the host actually has reliable values?
17. Does attempt-budget exhaustion create an explicit terminal or escalation state?
18. Can every rejected or failed attempt prove that no protected execution occurred for that attempt?

If those answers are unclear, recovery may be functioning as an uncontrolled agent loop rather than a governed workflow.

## Related Content

- [AI Integration](index.md) — place recovery within the broader host-owned AI execution boundary.
- [Typed AI Proposed Intent and Schema-Validation Boundaries](typed-ai-proposed-intent-and-schema-validation-boundaries.md) — classify parse, schema, registry, and semantic proposal failures before governance.
- [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) — follow accepted proposals through authoritative context, decision, acknowledgment, capability validation, and host-owned execution.
- [Governed Multi-Tool Workflows and Recovery Boundaries](governed-multi-tool-workflows-and-recovery-boundaries.md) — apply bounded recovery per step without granting blanket workflow authority.
- [Deterministic and Probabilistic Inputs in Policy Evaluation](../governance/deterministic-and-probabilistic-inputs-in-policy-evaluation.md) — preserve provenance, confidence, calibration, freshness, and uncertainty for model-derived signals.
- [Escalation Patterns in Governed Systems](../governance/escalation-patterns-in-governed-systems.md) — route unresolved decisions to another defined authority without silently broadening execution rights.
- [Human-in-the-Loop Governance Workflows](../governance/human-in-the-loop-governance-workflows.md) — model durable human review, revalidation, and scoped follow-on authority.
- [Secret Handling Across Trust Boundaries](../security/secret-handling-across-trust-boundaries.md) — keep credentials and other authority-bearing values out of model-visible recovery data.
- [Secure Logging Across Trust Boundaries](../security/secure-logging-across-trust-boundaries.md) — minimize operational evidence across rejected proposal attempts.
- [Threat Modeling as Architecture Reasoning](../security/threat-modeling-as-architecture-reasoning.md) — test how malformed proposals, retries, policy probing, dependencies, and escalation interact with trust boundaries.
- [Governed AI Tool Gateway sample](https://github.com/AsiBackbone/Learning/blob/main/samples/governed-ai-tool-gateway/README.md) — observe the existing executable proposal, decision, acknowledgment, capability, and execution invariants.
- [Governed AI Tool Gateway advanced lab](../labs/governed-ai-tool-gateway.md) — deliberately weaken and repair the broader gateway boundary.

---

> **Reject clearly. Recover deliberately. Re-evaluate authority every time.**
