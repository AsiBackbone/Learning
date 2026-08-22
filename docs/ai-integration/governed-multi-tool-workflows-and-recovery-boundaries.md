---
description: Learn how to coordinate multi-step AI-proposed workflows while keeping validation, policy, authority, recovery, and execution scoped to each host-controlled step.
---

# Governed Multi-Tool Workflows and Recovery Boundaries

**Learning objective:** Understand how a host can coordinate several AI-proposed operations without turning an accepted workflow plan into blanket execution authority, and how to recover safely when intermediate proposals are invalid, denied, stale, failed, cancelled, or replaced.

**Pattern classification:** General learning material

**Difficulty:** Advanced

**Prerequisites:** [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md), [Typed AI Proposed Intent and Schema-Validation Boundaries](typed-ai-proposed-intent-and-schema-validation-boundaries.md), [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md), and familiarity with [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md).

## At a Glance

> **Problem:** A model may propose a useful sequence of tool calls, but accepting the sequence as a plan can accidentally become blanket permission to execute every later step even after policy, resources, dependencies, or human decisions change.
>
> **Core idea:** Validate the proposed workflow as a plan, then treat every consequential step as a new execution boundary. Resolve current host-owned context, evaluate current policy, satisfy any step-specific acknowledgment, issue narrow authority, execute once, observe the result, and only then consider the next step.
>
> **Why it matters:** Multi-step workflows introduce time, intermediate state, partial success, retries, replanning, and recovery. Authority that was appropriate for one step or one moment should not silently survive those changes.
>
> **Prefer something simpler when:** One host-owned operation solves the task, a deterministic application workflow already owns the sequence safely, or AI contributes only advisory text and never proposes consequential actions.
>
> **Observe:** If Step 2 is rejected or fails before execution, Step 3 never reaches its protected executor. A revised Step 2 may be proposed, but it must cross the same validation and governance boundaries as any new proposal.

The central lesson is:

> **Authority should remain scoped to the step being executed; approval of a workflow plan should not silently become blanket authority for every later action.**

Two invariants follow immediately:

```text
Step N is allowed
      ≠
Authority for Step N+1
```

and:

```text
Rejected / failed step
      ↓
No protected execution for that step
```

A representative flow is:

```text
User goal
   ↓
Model proposes workflow
   ↓
Host validates plan shape and allowed tools
   ↓
Select next eligible step
   ↓
Resolve current authoritative context
   ↓
Validate step arguments and semantics
   ↓
Evaluate current policy
   ↓
Acknowledgment / escalation when required
   ↓
Issue step-specific scoped authority
   ↓
Validate authority at execution boundary
   ↓
Host executes one step
   ↓
Observe and record result
   ↓
Re-evaluate what may happen next
```

Avoid collapsing that into:

```text
Model emits five tool calls
   ↓
Host accepts the plan once
   ↓
Execute all five
```

The plan can organize work.

It should not become a standing execution credential.

---

## The Problem: A Workflow Outlives Its First Decision

A single governed tool call already has several boundaries:

```text
Proposal
   ↓
Validation
   ↓
Authoritative context
   ↓
Decision
   ↓
Scoped authority
   ↓
Execution
```

A multi-tool workflow repeats those boundaries over time.

Consider a support workflow using narrow semantic tools:

```text
1. case.lookup
2. account.disable
3. notification.send
```

The model may propose all three because they appear to satisfy a user goal.

But the state at Step 2 is not necessarily the state that existed when the model produced the plan.

Between steps:

- A resource may change.
- A policy version may change.
- A human may update the case.
- A capability may expire.
- A dependency may become unavailable.
- A previous result may contradict the model's assumptions.
- A tenant or regional overlay may change the applicable rules.
- A workflow may be cancelled.
- An external side effect may succeed while its response is lost.

The orchestration problem is therefore not only:

> Which step comes next?

It is also:

> **What current authority, if any, exists for the next step now?**

---

## Workflow Proposal and Execution Authority Are Different Artifacts

A workflow proposal can describe intended steps without authorizing them.

A small framework-neutral model might be:

```csharp
public sealed record ProposedWorkflow(
    string WorkflowId,
    string Goal,
    IReadOnlyList<ProposedWorkflowStep> Steps);

public sealed record ProposedWorkflowStep(
    string StepId,
    string ToolName,
    JsonElement Arguments,
    IReadOnlyList<string> DependsOn);
```

A proposal such as:

```text
Workflow: resolve-case-847

Step 1
  tool: case.lookup
  caseId: case-847

Step 2
  tool: account.disable
  accountId: account-42
  dependsOn: step-1

Step 3
  tool: notification.send
  template: security-notice
  dependsOn: step-2
```

answers:

> What sequence did the model suggest?

It does not answer:

> Which steps are currently authorized to execute?

Keep those questions separate.

A useful distinction is:

| Artifact | Purpose | Creates execution authority? |
| --- | --- | --- |
| Workflow proposal | Describes a possible sequence | No |
| Step proposal | Describes one candidate operation | No |
| Governance decision | States current policy outcome | Not by itself |
| Acknowledgment | Records an actor response to a defined condition | No |
| Step capability | Represents narrow follow-on authority | Potentially, within validated scope |
| Execution receipt | Records what the host attempted or completed | No; it is evidence |

A completed Step 1 is history.

It is not a credential for Step 2.

---

## Validate the Whole Plan, but Do Not Authorize the Whole Plan

Whole-plan validation is useful because some problems are visible before any step runs.

The host can reject a workflow that violates structural orchestration rules such as:

- Too many steps.
- Duplicate step identifiers.
- Unknown tools.
- Unsupported tool versions.
- Invalid argument schemas.
- Missing dependency references.
- Cyclic dependencies.
- Unsupported combinations of tools.
- A tool outside the workflow's host-defined allowlist.
- A plan that exceeds a host-owned time, cost, or replan budget.

For example:

```text
Maximum planned steps = 8
Maximum replans = 3
Allowed tools =
  case.lookup
  case.add-note
  notification.send
  account.disable
```

These are host controls.

The model may propose within them.

A valid plan still does not mean:

```text
All future steps are authorized.
```

Whole-plan validation answers:

> Is this a supported plan shape worth considering?

Per-step governance answers:

> May this specific operation proceed under current facts and policy?

Those are different questions.

---

## Tool Allowlists Apply to Every Step

A host-owned tool registry remains the outer execution boundary.

A model should not gain broader tools merely because it is operating inside a workflow.

Prefer a narrow workflow surface such as:

```text
case.lookup
case.add-note
notification.send
account.disable
```

rather than:

```text
run_sql
execute_shell
invoke_arbitrary_http
```

A workflow engine that can sequence arbitrary primitives may expose more authority than the business task requires.

The allowlist should also remain current.

If `account.disable` is removed from the host registry after the plan is created but before Step 2 is considered, Step 2 should fail current validation rather than execute from a stale plan snapshot.

---

## Typed Arguments Still Matter at Step Time

A workflow may be structurally valid when first proposed and still contain a step that becomes semantically invalid later.

For each step, preserve the acceptance stages from [Typed AI Proposed Intent and Schema-Validation Boundaries](typed-ai-proposed-intent-and-schema-validation-boundaries.md):

```text
Model-proposed step
      ↓
Structural parsing
      ↓
Schema validation
      ↓
Typed proposed intent
      ↓
Semantic / host validation
      ↓
Current authoritative context
      ↓
Governance evaluation
```

Do not let a prior workflow parse replace current step validation.

For example, Step 2 may contain:

```text
accountId = account-42
```

but the host may discover that the case now points to a different account, the account no longer exists, or the requested operation no longer applies.

Schema validity does not freeze resource state.

---

## The Host Owns Workflow State

The model can propose a plan and revised steps.

The host should own the durable state that determines what has actually happened.

A conceptual state record might include:

```text
WorkflowId
CorrelationId
CurrentStatus
CompletedStepIds
RejectedStepIds
FailedStepIds
CurrentStepId
ReplanCount
CreatedAt
Deadline
CancellationState
```

Useful workflow states may include:

```text
Proposed
Ready
Running
WaitingForAcknowledgment
WaitingForReview
Blocked
Failed
Cancelled
Completed
```

The exact names are application-specific.

The architectural requirement is that the model does not get to declare:

```text
Step 2 succeeded
```

when the host has no successful execution receipt for Step 2.

Likewise, model text such as:

```text
The user already approved this.
```

should not replace host-owned acknowledgment evidence.

---

## Resolve Authoritative Context Again for Each Consequential Step

A workflow plan is a proposal snapshot.

Policy context should be based on current authoritative sources when a step is considered.

For Step 2, the host may need to resolve again:

- Authenticated actor.
- Tenant.
- Resource owner.
- Current resource status.
- Current classification.
- Current region.
- Current incident posture.
- Current policy version.
- Current acknowledgment state.
- Current workflow state.
- Results from prior steps that the host actually observed.

The model may summarize earlier results.

The host should still distinguish that summary from authoritative workflow state.

A useful rule is:

> **Prior model context can explain why a step was proposed; host-owned state determines whether the step is currently governable.**

---

## Every Consequential Step Gets Its Own Governance Decision

Suppose Step 1 is allowed:

```text
case.lookup
```

That decision does not imply that Step 2 is allowed:

```text
account.disable
```

A per-step flow is:

```text
Step proposal
   ↓
Current host context
   ↓
Current constraints
   ↓
Current governance decision
   ↓
Possible acknowledgment / escalation
   ↓
Step-specific authority
   ↓
Execution
```

The decision should preserve step-specific reason codes.

For example:

```text
step-1
  outcome: Allowed
  reason: case.lookup.read-authorized

step-2
  outcome: Denied
  reason: account.disable.protected-account
```

Do not replace that history with a workflow-level summary such as:

```text
Workflow approved
```

when individual steps had different policy outcomes.

---

## Step-Specific Capability, Not Workflow-Wide Privilege

When a capability boundary is useful, issue authority for the step being executed.

A step capability may be bound to:

```text
WorkflowId
StepId
Actor
Tool / operation
Resource
Audience
Policy version
Acknowledgment reference when required
Expiration
Use count
```

For example:

```text
Capability for step-2
  workflow: resolve-case-847
  step: step-2
  operation: account.disable
  resource: account-42
  audience: account-service
  expires: 10:04:00Z
  uses: 1
```

It should not silently mean:

```text
May execute every remaining step in resolve-case-847.
```

A workflow may have an identity token, correlation token, or orchestration lease for coordination.

Do not confuse those with authority to perform protected side effects.

The core invariant remains:

```text
Valid capability for Step N
      ≠
Capability for Step N+1
```

---

## No Automatic Privilege Accumulation

A multi-step workflow can accidentally become more privileged over time if completed steps are treated as accumulated authority.

Avoid reasoning such as:

```text
Step 1 allowed read access
      +
Step 2 received acknowledgment
      +
Step 3 had a write capability
      ↓
Workflow now has broad read/write authority
```

Prefer:

```text
Each step receives only the authority required for that step.
Previous step authority expires or is consumed.
Workflow history remains evidence, not privilege.
```

The host should not union old scopes into a growing workflow credential unless that broader authority is an explicit, separately governed design requirement.

---

## Observe the Result Before Considering the Next Step

The next step should be based on what the host observed, not what the model predicted would happen.

For example:

```text
Step 1: case.lookup
Expected by model:
  account is active and unprotected

Observed by host:
  account is active
  protection = security-investigation
```

That result may change Step 2 completely.

The model can receive a bounded observation and propose a next action.

The host can also follow a deterministic workflow branch without asking the model again.

Both designs preserve the same execution boundary:

```text
Observed result
      ↓
Next proposal or deterministic branch
      ↓
Current validation + governance
      ↓
Possible execution
```

Observation is not automatic continuation.

---

## State Drift and Resource Drift Are Normal Workflow Conditions

Longer workflows create time for state to change.

Suppose the model proposed:

```text
Step 2: account.disable(account-42)
```

but before execution:

```text
Account already disabled by another operator
```

Possible host behaviors include:

- Treat the step as already satisfied if the operation is semantically idempotent.
- Return a no-op result with an explicit reason code.
- Reject the step because its precondition no longer holds.
- Rebuild context and re-evaluate policy.

Do not blindly execute merely because the plan is old but valid-looking.

Likewise, if the resource changes from:

```text
classification = Internal
```

to:

```text
classification = Restricted
```

before a later export or notification step, the newer authoritative classification should drive the current decision.

---

## Policy Drift Requires Re-Evaluation, Not Historical Permission

Policy can change during a workflow.

For example:

```text
10:00  Workflow proposed under policy v12
10:01  Step 1 allowed and executed under v12
10:03  Policy v13 deployed
10:04  Step 2 considered
```

Do not assume:

```text
Plan accepted under v12
      ↓
All later steps may execute under v12
```

unless the domain has intentionally designed and bounded a policy-snapshot model.

A safer default for consequential operations is:

```text
Step 2
   ↓
Resolve current policy identity
   ↓
Evaluate current context
   ↓
Record decision provenance
```

If a previously issued capability is bound to a policy version the execution boundary no longer accepts, validation should fail and the host can re-evaluate.

See [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) for the broader provenance model.

---

## Acknowledgment Scope Must Be Explicit

A human acknowledgment can apply to one step, one condition, or—if intentionally designed—a defined workflow scope.

Do not assume that acknowledging one consequential step means acknowledging every later action.

For example:

```text
Step 2:
account.disable(account-42)

Acknowledgment:
"I understand this disables account-42 and may interrupt access."
```

That acknowledgment should not silently cover:

```text
Step 3:
notification.send(external-recipient)
```

unless the acknowledgment was intentionally worded, bound, and governed to include that operation too.

A step-level acknowledgment should normally bind to the exact actor, operation, resource, policy context, and workflow/step identity that required it.

A workflow-wide acknowledgment can be appropriate only when the reviewed scope is explicit and stable enough to support it.

Even then:

> **Acknowledgment of the plan is not blanket execution authority.**

Current policy and step-specific execution checks still apply.

For longer human-review workflows, see [Human-in-the-Loop Governance Workflows](../governance/human-in-the-loop-governance-workflows.md).

---

## Required Example: Step 1 Succeeds, Step 2 Is Rejected, Step 3 Never Executes

Consider this proposed workflow:

```text
User goal:
Resolve a suspected compromised account and notify the owner.

Step 1
  case.lookup(case-847)

Step 2
  account.disable(account-42)
  dependsOn: step-1

Step 3
  notification.send(owner-42, security-notice)
  dependsOn: step-2
```

### Step 1

The host validates `case.lookup`, resolves current context, evaluates policy, and executes the read operation.

Observed result:

```text
case = case-847
account = account-42
protection = security-investigation
```

Step 1 succeeds.

### Step 2

The host does not inherit Step 1 authority.

It resolves current context for:

```text
account.disable(account-42)
```

Current policy returns:

```text
Outcome: Denied
Reason: account.disable.protected-account
```

Therefore:

```text
No capability is issued
No account-disable handler is invoked
```

### Step 3

Step 3 depends on successful Step 2 completion.

Because Step 2 was denied:

```text
Step 3 eligibility = false
```

The notification executor is never invoked.

The workflow may become:

```text
Blocked
```

or:

```text
NeedsReplan
```

but not:

```text
Continue anyway
```

This is the observable invariant:

```text
Step 1 succeeds
Step 2 is rejected
Step 3 executor invocation count = 0
```

---

## The Model May Re-Plan Without Bypassing the Boundary

A denied step does not necessarily end all useful work.

The host may allow the model to propose a revised next step.

For the previous example, the model could propose:

```text
Revised Step 2
  case.add-note(
    case-847,
    "Automatic account disable was denied; security review required.")
```

That revised step is a **new proposal**.

It still passes through:

```text
Tool allowlist
   ↓
Schema validation
   ↓
Semantic validation
   ↓
Current host context
   ↓
Current policy
   ↓
Possible scoped authority
   ↓
Host-owned execution
```

The model should not be able to recover from denial by widening the request:

```text
account.disable(force = true)
```

when `force` is not part of the host-owned schema.

Nor should it be able to substitute a broader primitive:

```text
execute_shell("disable account-42 --force")
```

when that tool is outside the allowlist.

Replanning changes the proposal.

It does not change who owns authority.

---

## Recovery Is an Explicit Host Decision

Different failures should not collapse into one generic retry loop.

| Condition | Safe default question | Possible recovery |
| --- | --- | --- |
| Invalid model output | Can the proposal be accepted structurally and semantically? | Reject and request a new bounded proposal |
| Unknown tool | Is the tool host-supported? | Reject; do not dynamically expose a new executor |
| Policy denial | Is a different operation legitimately possible? | Stop, replan, or escalate; never reinterpret denial as permission |
| Acknowledgment rejected | Does current policy allow any alternative? | Stop or replan; do not reuse the rejected acknowledgment |
| Capability expired | Is the action still appropriate now? | Rebuild context and re-evaluate before issuing new authority |
| Dependency failure before side effect | Is retry safe and still allowed? | Retry under bounded policy or defer |
| Ambiguous external outcome | Did the side effect actually occur? | Reconcile using idempotency/receipt state before retrying |
| Resource drift | Does the proposed step still apply? | Revalidate, no-op, replan, or reject |
| Policy drift | Does current policy still permit the step? | Re-evaluate under current policy |
| Cancellation | Which future steps remain unexecuted? | Stop new execution and record cancellation |

Recovery logic belongs to the host or a host-controlled workflow component.

A model may suggest a recovery action.

The model should not decide that a failed boundary may be bypassed.

---

## Invalid Model Output Should Recover Through a New Proposal

Suppose the model produces:

```json
{
  "stepId": "step-2",
  "tool": "account.disable",
  "arguments": {
    "accountId": ["wrong", "shape"]
  }
}
```

If the schema requires a string identifier, reject the proposal before governance or execution.

The host may ask the model to regenerate the step within the supported schema.

The regenerated output should receive a new proposal identity or revision identity and pass validation again.

Do not mutate malformed model output silently into an executable request when the transformation changes meaning.

---

## Policy Denial Is Not a Retryable Technical Error

A policy denial means something different from a timeout.

Avoid:

```text
Denied
  ↓
Retry three times
  ↓
Maybe it will pass
```

A retry may be appropriate only when authoritative facts or policy have legitimately changed and the host intentionally re-evaluates.

More appropriate choices include:

- Stop the workflow.
- Ask for a different proposal.
- Route to human escalation when policy defines that path.
- Wait for a relevant state change.
- Record a denied terminal outcome.

For governed escalation, see [Escalation Patterns in Governed Systems](../governance/escalation-patterns-in-governed-systems.md).

---

## Dependency Failure Requires Outcome Awareness

A tool call can fail in several ways:

```text
Rejected before execution
Execution started and returned failure
Execution timed out before side effect
Execution timed out after side effect
Response lost after success
Dependency unavailable
```

Those outcomes are not equivalent.

Suppose:

```text
notification.send
```

returns a timeout.

The host should not immediately assume:

```text
No notification was sent.
```

If the provider supports idempotency keys or operation receipts, use them to reconcile the outcome.

A safe recovery may be:

```text
Ambiguous result
   ↓
Check host/provider receipt using idempotency key
   ↓
Known success? ── yes ──> record success, do not resend
   │
   no / unknown
   ↓
Apply bounded retry or escalation policy
```

The workflow should preserve the difference between:

```text
Not authorized
```

and:

```text
Authorized but operational outcome unknown
```

---

## Replay Protection and Idempotency Solve Different Problems

Multi-step workflows make both concerns visible.

Replay protection asks:

> Can the same execution authority be used again?

Idempotency asks:

> If the host repeats an operation request, can the side effect be duplicated?

A one-use capability can still be followed by an ambiguous network failure after the external system completed the action.

The host may then need an idempotency key such as:

```text
workflowId + stepId + executionAttemptId
```

or another application-specific key accepted by the destination.

Do not claim exactly-once behavior unless the complete storage, transaction, and external-system semantics actually support it.

See [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) for the broader distinction.

---

## Partial Success Is a First-Class Workflow Outcome

A multi-tool workflow may end after some real side effects have already occurred.

For example:

```text
Step 1: case.add-note       → succeeded
Step 2: notification.send  → succeeded
Step 3: account.disable    → denied
```

The correct final state is not necessarily:

```text
Failed, nothing happened
```

A more accurate result may be:

```text
PartiallyCompleted
```

with explicit receipts for the completed steps and a denial record for the blocked step.

The user or operator should be able to distinguish:

```text
what succeeded
what was rejected
what failed operationally
what remains unattempted
```

This distinction matters for both recovery and audit residue.

---

## Compensation Is Not Automatic Rollback

External side effects are often not transactional.

After:

```text
notification.send
```

the system cannot truly "unsend" the message.

After:

```text
account.disable
```

a later `account.enable` may be a compensating operation, but it is not automatically a safe rollback.

The resource may have changed again.

Policy may no longer permit re-enabling.

The compensation may require a different actor, acknowledgment, or authority.

Treat compensation as another governed operation:

```text
Failed workflow
   ↓
Host determines compensation is appropriate
   ↓
Compensation proposal
   ↓
Current context + policy
   ↓
Scoped authority
   ↓
Host-owned compensation
```

Use a local database transaction when the host truly controls an atomic transaction boundary.

Use explicit compensating semantics when the workflow crosses systems that cannot share one atomic transaction.

Do not label every compensating action "rollback" when the original world state cannot actually be restored.

---

## Limit Steps, Replans, and Loops

A model-driven workflow can otherwise continue indefinitely:

```text
proposal
  ↓
rejection
  ↓
replan
  ↓
rejection
  ↓
replan
  ↓
...
```

The host should own bounded workflow budgets such as:

- Maximum planned step count.
- Maximum executed step count.
- Maximum replan count.
- Maximum consecutive invalid proposals.
- Maximum elapsed workflow duration.
- Maximum repeated tool/argument fingerprints.
- Maximum external cost or resource budget where relevant.

For example:

```text
planned steps <= 8
executed steps <= 8
replans <= 3
workflow deadline <= 10 minutes
```

The values are domain-specific.

The architectural point is that loop termination is a host control, not a prompt suggestion.

A prompt may say:

```text
Do not retry more than three times.
```

The host should still enforce the actual retry or replan budget.

---

## Safe Cancellation Stops Future Authority

Cancellation should prevent new protected execution after the host accepts the cancellation state.

A useful invariant is:

```text
Workflow cancelled
      ↓
No new step capability issued
      ↓
No future step executor invoked
```

Cancellation does not erase side effects that already occurred.

If compensation is required, it should follow the governed compensation path rather than running as an unreviewed cleanup script.

For in-flight operations, define what cancellation can actually guarantee.

Some dependencies support cooperative cancellation before the side effect.

Others may complete despite the host abandoning the request.

Audit residue should record that distinction.

---

## Human Escalation Does Not Grant the Model More Power

A step may produce:

```text
EscalationRecommended
```

That outcome should block protected execution for the current step.

A host-controlled escalation workflow may then route the unresolved decision to an eligible reviewer or authority.

If a later human disposition permits continuation:

```text
Human disposition
   ↓
Refresh authoritative context
   ↓
Re-evaluate current policy
   ↓
Issue new step-specific authority if appropriate
   ↓
Execute through normal boundary
```

Do not resume using an old capability simply because a reviewer eventually responded.

Do not allow the model to "escalate" by selecting a more privileged tool, changing actor identity, or routing around the original policy boundary.

Human review changes the decision path only through explicit host-owned rules.

---

## Correlation and Audit Residue Should Preserve the Step History

A useful audit chain can reconstruct the workflow without pretending that every event had the same outcome.

For each workflow, preserve identifiers such as:

```text
WorkflowId
CorrelationId
Proposal revision
StepId
Tool / operation
Decision outcome
Reason codes
Policy identity
Acknowledgment reference when applicable
Capability identity / validation result
Execution attempt
Execution receipt
Observed result
Recovery action
```

A conceptual event sequence might be:

```text
workflow.proposed
step-1.validated
step-1.allowed
step-1.executed
step-1.observed
step-2.validated
step-2.denied
workflow.replan-requested
step-2r1.proposed
step-2r1.allowed
step-2r1.executed
workflow.completed
```

Reason codes should remain attached to the step that produced them.

Do not compress this into:

```text
AI workflow succeeded
```

when intermediate denials, retries, or replans are material to understanding what happened.

Operational logs and governance evidence may overlap in identifiers while serving different retention and integrity purposes.

---

## A Small Host-Controlled Orchestration Sketch

The exact implementation may use an application service, durable workflow engine, queue, state machine, or background worker.

The architectural ordering can remain the same:

```csharp
foreach (ProposedWorkflowStep step in workflow.Steps)
{
    if (!hostState.IsEligible(step))
    {
        break;
    }

    StepValidation validation =
        validator.Validate(step, hostToolRegistry);

    if (!validation.Accepted)
    {
        hostState.Reject(step, validation.ReasonCode);
        break;
    }

    PolicyContext context =
        await contextFactory.BuildCurrentAsync(
            workflow,
            step,
            cancellationToken);

    GovernanceDecision decision =
        policy.Evaluate(context);

    audit.WriteDecision(
        workflow.WorkflowId,
        step.StepId,
        decision);

    if (!decision.CanProceed)
    {
        hostState.Block(step, decision.ReasonCodes);
        break;
    }

    ExecutionCapability capability =
        capabilityIssuer.IssueForStep(
            workflow.WorkflowId,
            step.StepId,
            context,
            decision);

    CapabilityValidationResult capabilityResult =
        await capabilityValidator.ValidateAsync(
            capability,
            context,
            cancellationToken);

    if (!capabilityResult.Allowed)
    {
        hostState.Reject(
            step,
            capabilityResult.ReasonCode);
        break;
    }

    ToolExecutionResult result =
        await executor.ExecuteAsync(
            step,
            capability,
            cancellationToken);

    hostState.RecordResult(step, result);
    audit.WriteExecution(
        workflow.WorkflowId,
        step.StepId,
        result);

    if (!result.Succeeded)
    {
        break;
    }
}
```

This sketch intentionally leaves out acknowledgment, escalation, retries, durable persistence, and distributed coordination details.

Those concerns can be inserted at the visible boundaries rather than hidden inside one `ExecutePlanAsync` call.

The important property is that the loop does **not** pre-authorize every step when it begins.

---

## Replanning Should Produce a New Revision

When the model revises a plan, preserve the relationship between the old and new proposals.

For example:

```text
workflowId = wf-847
revision = 1
  step-2 account.disable → denied

workflowId = wf-847
revision = 2
  step-2r1 case.add-note → proposed
```

The new revision should not rewrite history so that the denied step disappears.

Preserve:

```text
Original proposal
Original denial
Reason code
Replan request
Revised proposal
New decision
```

This makes it possible to review whether the model found a legitimate alternative or merely attempted to route around a denied boundary.

---

## Model Uncertainty May Inform a Step but Does Not Authorize It

A workflow may include model-derived confidence or classification signals.

For example:

```text
compromiseProbability = 0.82
```

That signal may influence whether the model proposes `account.disable` or whether policy requires review.

It should remain a probabilistic observation with provenance rather than a hidden authority claim.

The host still resolves deterministic facts and applies explicit threshold policy.

See [Deterministic and Probabilistic Inputs in Policy Evaluation](../governance/deterministic-and-probabilistic-inputs-in-policy-evaluation.md) for the focused treatment of this boundary.

---

## Common Failure Modes

### 1. Plan Approval Becomes Blanket Execution Authority

```text
Plan accepted
   ↓
All later tools execute without current policy
```

This is the primary failure this tutorial avoids.

### 2. Step 1 Authority Is Reused for Step 2

A capability for one resource or operation is treated as a workflow-wide credential.

### 3. Old Context Is Reused After State Changes

The host executes a later step using resource facts captured before earlier side effects or external changes occurred.

### 4. Policy Version Is Frozen Accidentally

A plan created under an old policy continues to execute after current policy changed, without an intentional snapshot rule.

### 5. Acknowledgment Silently Expands

A human acknowledgment for one operation is treated as permission for all remaining steps.

### 6. Replanning Becomes Policy Evasion

The model responds to denial by trying broader tools, hidden arguments, or alternate routes that bypass the same protected boundary.

### 7. Retry Treats Denial Like a Timeout

A policy denial is retried mechanically instead of being handled as a governance outcome.

### 8. Timeout Is Assumed to Mean No Side Effect

The host repeats an external operation without reconciling an ambiguous prior result.

### 9. Automatic Rollback Performs New Side Effects Without Governance

Compensating actions receive more authority than the original actions they are supposed to repair.

### 10. Workflow History Becomes a Privilege Cache

Completed steps accumulate into broader standing authority.

### 11. The Model Owns Loop Termination

A prompt asks the model to stop eventually, but the host enforces no step, replan, time, or cost limit.

### 12. Cancellation Only Hides the UI

The workflow is marked cancelled while background workers continue issuing capabilities and executing later steps.

---

## Test the Architectural Invariants

A useful test suite should make failure behavior observable.

### Allowed Step Does Not Authorize the Next Step

```text
Step 1 = Allowed
Step 1 executes once
Step 2 = Denied

Expected:
Step 2 capability count = 0
Step 2 executor count = 0
```

### Denied Intermediate Step Blocks Dependents

```text
Step 1 = succeeded
Step 2 = denied
Step 3 depends on Step 2

Expected:
Step 3 executor count = 0
```

### Revised Step Must Re-enter Governance

```text
Step 2 = denied
Model proposes Step 2 revision

Expected:
new schema validation
new authoritative context
new governance decision
new capability only if allowed
```

### Resource Drift Invalidates Old Assumptions

```text
Plan says resource classification = Internal
Host now says = Restricted

Expected:
current host classification reaches policy
```

### Policy Drift Is Preserved in Provenance

```text
Step 1 decision = policy v12
Step 2 decision = policy v13

Expected:
both policy identities remain visible
```

### Expired or Replayed Authority Fails

```text
Step capability expired or consumed
      ↓
No execution
```

### Ambiguous External Outcome Does Not Blindly Duplicate

```text
First attempt times out after possible side effect
      ↓
Reconcile idempotency / receipt state
      ↓
No duplicate execution when success is already known
```

### Cancellation Stops Future Steps

```text
Step 1 complete
Workflow cancelled

Expected:
Step 2 capability count = 0
Step 2 executor count = 0
```

### Replan Budget Terminates Loops

```text
Maximum replans = 3
Fourth replan requested

Expected:
workflow blocked / escalated / failed
no additional tool execution
```

---

## Tradeoffs

### Benefits

- Model planning remains useful without becoming execution authority.
- Current policy and resource state can be reconsidered between steps.
- Each consequential operation receives narrow authority.
- Partial success and failure remain observable.
- Replanning can recover useful work without bypassing denied boundaries.
- Replay, idempotency, cancellation, and compensation become explicit design concerns.
- Audit evidence can explain which step was proposed, allowed, denied, executed, retried, or replaced.

### Costs

- Per-step evaluation adds orchestration and latency.
- Durable workflow state may be required.
- Revalidation can increase dependency calls.
- Long-running workflows need expiry, cancellation, and recovery rules.
- Idempotency and ambiguous-outcome handling can require destination-specific support.
- Compensation logic may be domain-specific and operationally expensive.
- Human review can pause workflows for long periods.
- Excessive governance around low-consequence steps can make a simple workflow harder to maintain.

The goal is not to govern every internal computation as if it were a consequential side effect.

The goal is to preserve explicit boundaries where authority, trust, or external state changes.

---

## Prefer Simpler Alternatives When They Fit

A multi-tool governed workflow is not automatically the right architecture.

Prefer a simpler design when:

- One host-owned operation can encapsulate the transaction safely.
- A conventional application service already owns a deterministic sequence.
- A workflow engine can coordinate known steps without AI-driven replanning.
- All model output is advisory or read-only.
- The operations are low consequence and ordinary authorization plus validation is sufficient.

For example, if a host operation already implements:

```text
CloseCaseAndNotifyOwner(caseId)
```

with clear authorization, transaction, and failure behavior, decomposing it into three model-selected tools may add unnecessary failure modes.

Use multi-tool orchestration when step-by-step proposal, observation, conditional branching, or recovery materially contributes to the task.

---

## Relationship to Multi-Agent Workflows

Multi-tool and multi-agent systems are related but not identical.

A single agent can propose many tool steps.

Several agents can also cooperate on one step.

The same authority rule applies:

```text
More planning participants
      ≠
More execution authority
```

If several agents exchange recommendations, delegation requests, or plans, continue with [Governed Agent-to-Agent Requests and Multi-Agent Execution Boundaries](../advanced/governed-agent-to-agent-requests-and-multi-agent-execution-boundaries.md).

That material focuses on trust and delegation across participants.

This tutorial focuses on execution and recovery across steps.

---

## Relationship to AsiBackbone

This tutorial is framework-neutral.

The working `AsiBackbone` repository provides governance artifacts that can participate in a per-step design, including structured governance decisions, acknowledgment/handshake requests, audit residue, and capability-token grants.

Useful references include:

- [`GovernanceDecision`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecision.cs)
- [`AuditResidue`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Audit/AuditResidue.cs)
- [`LiabilityHandshakeRequest`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Handshakes/LiabilityHandshakeRequest.cs)
- [`CapabilityTokenGrant`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityTokenGrant.cs)

Those abstractions do not make `AsiBackbone` a model runtime or workflow engine.

A host application still owns:

```text
workflow state
model invocation
step selection
current context construction
retry / replan policy
cancellation
idempotency
external tool execution
operational recovery
```

The governance layer can remain focused on explicit decision and authority artifacts while the host owns orchestration.

---

## Design Exercise

Model this user goal without executing real side effects:

```text
Review case-847,
record the appropriate case note,
disable account-42 only if current policy permits,
and notify the owner only after the disable step succeeds.
```

Use only:

```text
case.lookup
case.add-note
account.disable
notification.send
```

Require the design to demonstrate:

1. Whole-plan validation without whole-plan authorization.
2. Current host context for every consequential step.
3. A step-specific decision and reason code.
4. A step-specific capability for mutating operations.
5. Step 1 success followed by Step 2 denial.
6. Zero Step 3 executor calls after Step 2 denial.
7. A revised `case.add-note` proposal after the denial.
8. Full validation and policy evaluation for the revised proposal.
9. Maximum step and replan limits.
10. Correlation across original proposal, denial, replan, and final outcome.

Then change the scenario so Step 2 is allowed but the dependency returns an ambiguous timeout.

Explain how idempotency, receipt reconciliation, and safe cancellation change the recovery path.

Do not add a real external integration until the failure boundaries are observable in simulation.

---

## Review Questions

You should now be able to answer:

1. Why does accepting a workflow plan not authorize every step?
2. Which checks belong to whole-plan validation, and which must be repeated per step?
3. Why should current host-owned context be rebuilt between consequential steps?
4. How can policy or resource drift invalidate a later step without invalidating the history of earlier steps?
5. Why should acknowledgment scope be explicit?
6. Why should a capability be bound to a workflow step rather than the whole plan by default?
7. What should happen to dependent steps after an intermediate denial?
8. How may a model replan without receiving authority to bypass the denied boundary?
9. Why is policy denial different from dependency failure?
10. How do replay protection and idempotency differ?
11. Why can partial success be more accurate than a generic failed workflow state?
12. Why is compensation a new governed operation rather than automatic rollback?
13. Which host-owned limits prevent endless replanning loops?
14. What does safe cancellation guarantee, and what does it not undo?
15. Why should workflow history remain evidence rather than accumulated privilege?

## Related Content

- [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) — establish the single-proposal host execution boundary this tutorial repeats per step.
- [Typed AI Proposed Intent and Schema-Validation Boundaries](typed-ai-proposed-intent-and-schema-validation-boundaries.md) — validate model-generated workflow and step proposals before governance.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — bind narrow authority to the exact step, resource, audience, and validity window.
- [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) — separate capability replay control from request idempotency and exactly-once claims.
- [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) — preserve which policy version decided each step as policy changes over time.
- [Human-in-the-Loop Governance Workflows](../governance/human-in-the-loop-governance-workflows.md) — model durable review when a step cannot continue synchronously.
- [Escalation Patterns in Governed Systems](../governance/escalation-patterns-in-governed-systems.md) — route unresolved decisions without converting escalation into execution authority.
- [Deterministic and Probabilistic Inputs in Policy Evaluation](../governance/deterministic-and-probabilistic-inputs-in-policy-evaluation.md) — preserve uncertainty when model-derived signals influence a step decision.
- [Governed Agent-to-Agent Requests and Multi-Agent Execution Boundaries](../advanced/governed-agent-to-agent-requests-and-multi-agent-execution-boundaries.md) — extend the trust model when multiple agents exchange proposals or delegated work.
- [Governed AI Tool Gateway sample](https://github.com/AsiBackbone/Learning/tree/main/samples/governed-ai-tool-gateway) — observe the existing single-tool gateway and invariant tests before designing a multi-step companion.
- [Governed AI Tool Gateway advanced lab](../labs/governed-ai-tool-gateway.md) — practice breaking and repairing the execution boundary that remains foundational here.

---

> **Read it. Run it. Question it. Improve it.**
