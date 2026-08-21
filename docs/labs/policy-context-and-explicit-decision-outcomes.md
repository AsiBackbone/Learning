---
description: Practice explicit policy context, structured decision outcomes, stable reason codes, and intentional precedence instead of scattered facts and booleans.
---

# Lab — Policy Context and Explicit Decision Outcomes

**Learning objective:** Practice replacing scattered authorization facts and boolean results with an explicit policy-context snapshot, structured decision outcomes, stable reason codes, and intentional rule precedence.

**Difficulty:** Beginner

**Prerequisites:** Complete the [Policy Context and Explicit Decision Outcomes tutorial](../tutorials/policy-context-and-explicit-decision-outcomes.md) and run the [Policy Context and Explicit Decision Outcomes sample](https://github.com/AsiBackbone/Learning/blob/main/samples/policy-context-and-explicit-decision-outcomes/README.md).

This lab builds directly on the second foundational tutorial and its executable companion sample.

The tutorial explains why decision inputs and outcomes should be visible.

The sample demonstrates that model with deterministic scenarios.

This lab asks you to **compress, extend, challenge, and make the policy contract more explicit**.

> **A governance decision should preserve enough information for the host to understand what happened and what should happen next.**

---

## Starting Architecture

The companion sample uses this flow:

```text
Host gathers facts
   ↓
Policy context snapshot
   |
   +-- Intent
   +-- Actor
   +-- Resource
   +-- Environment
   +-- Correlation
   +-- Policy identity
   ↓
Policy evaluation
   ↓
Structured decision outcome
   ↓
Host-controlled next step
```

The important invariant is:

```text
Same explicit context
+
Same policy behavior
   ↓
Same structured outcome and reason code
```

The sample intentionally performs no real account-disable operation. The exercise focuses on the information that reaches policy evaluation and the meaning of the decision returned from it.

## Prepare the Lab

Work on a temporary branch or disposable copy of the repository so that you can safely modify the sample.

For example:

```bash
git switch -c lab/policy-context-and-outcomes
```

From the repository root, run the companion sample before making changes:

```bash
dotnet run --project samples/policy-context-and-explicit-decision-outcomes/PolicyContextAndExplicitDecisionOutcomes/PolicyContextAndExplicitDecisionOutcomes.csproj
```

The baseline should finish with:

```text
Invariant preserved: every explicit context produced the expected structured outcome.
Scenarios verified: 7
```

Before continuing, locate these elements in `Program.cs`:

1. `DisableAccountIntent`
2. `ActorContext`
3. `AccountContext`
4. `EnvironmentContext`
5. `DisableAccountPolicyContext`
6. `GovernanceDecisionOutcome`
7. `DecisionReason`
8. `GovernanceDecision`
9. `DisableAccountPolicy`
10. `PolicyScenario`

You should be able to explain which types contain **facts**, which type contains **rules**, and which types describe the **decision result**.

---

# Part 1 — Collapse the Decision Back to a Boolean

The sample currently preserves several distinct outcomes:

```text
Allowed
Warning
Denied
Deferred
AcknowledgmentRequired
EscalationRecommended
```

Temporarily add a boolean-only view of the result after policy evaluation:

```csharp
GovernanceDecision decision = policy.Evaluate(scenario.Context);
bool allowed = decision.CanProceed;
```

Print only the scenario name and the boolean for one run.

## Observe the Information Loss

Answer these questions before restoring the original output:

1. Can the boolean distinguish `Allowed` from `Warning`?
2. Can it distinguish `Denied` from `Deferred`?
3. Can it distinguish `AcknowledgmentRequired` from `EscalationRecommended`?
4. Can the host determine why the result is `false` without reevaluating policy or inspecting other state?
5. Could two very different governance situations now look identical to downstream code?

Restore the structured output after the experiment.

A derived property such as `CanProceed` can be useful at a specific boundary. The problem appears when the boolean becomes the **entire decision model** and discards information required to route, explain, audit, or revisit the decision.

---

# Part 2 — Add an Explicit Context Fact

Extend the account context with a simple data-classification fact:

```csharp
public enum DataClassification
{
    Standard,
    Sensitive,
    Restricted
}
```

Add the value to `AccountContext`:

```csharp
public sealed record AccountContext(
    string AccountId,
    string TenantId,
    bool IsProtected,
    bool IsAlreadyDisabled,
    DataClassification Classification);
```

Update `CreateContext` so each scenario receives an explicit classification. Use `Standard` for the existing scenarios so the baseline behavior remains unchanged.

Create a new deterministic scenario for a restricted account:

```text
Requester is administrator
Actor and account are in the same tenant
Account is not protected
Account is not already disabled
Maintenance hold is false
Reason is supplied
Classification is Restricted
```

Do not add any policy rule yet.

Run the sample.

The new fact should be visible in the context, but the policy should still treat the scenario according to the existing rules.

This demonstrates:

```text
Context fact exists
≠
Policy interpretation exists
```

A context should describe the evaluated situation. It should not silently decide what the fact means.

---

# Part 3 — Interpret the New Fact with a Structured Outcome

Now introduce a policy rule for `Restricted` accounts.

For this lab, a reasonable design is:

```text
Outcome: EscalationRecommended
ReasonCode: account.disable.restricted-classification
```

Add a policy rule similar to:

```csharp
if (context.Account.Classification is DataClassification.Restricted)
{
    return GovernanceDecision.Escalate(
        "account.disable.restricted-classification",
        "Restricted accounts require higher-authority review.");
}
```

Update the new scenario so it expects:

```text
EscalationRecommended
account.disable.restricted-classification
```

Run the sample again and confirm that the scenario is verified.

## Explain the Outcome

Write a short explanation answering:

> Why is `EscalationRecommended` more informative than `false` for this case?

A denial means stop.

An escalation recommendation means the current path should stop **and another decision path should begin**.

Those states may both have `CanProceed == false`, but they are not operationally equivalent.

---

# Part 4 — Make Rule Precedence Observable

Create an overlapping scenario where more than one rule could apply:

```text
Requester is administrator
Actor and account are in the same tenant
Account is protected
Account classification is Restricted
Maintenance hold is active
Reason is supplied
```

At least three outcomes are plausible from the individual rules:

```text
Protected account
   ↓
EscalationRecommended

Restricted classification
   ↓
EscalationRecommended

Maintenance hold
   ↓
Deferred
```

Run the sample and observe which rule currently wins.

Do not assume that the current result is automatically correct merely because it appears first in the method.

Choose an intentional precedence rule and make it executable by adding the overlapping scenario to `PolicyScenario` with the expected outcome and reason code.

Reasonable choices include:

- Escalation outranks temporary deferral.
- A temporary operational hold short-circuits all account changes.

The expected scenario becomes a small contract documenting intended precedence.

---

# Part 5 — Preserve Stable Reason Codes

Change the human-readable message for one rule without changing its reason code.

For example, revise:

```text
Only administrators may disable accounts.
```

to:

```text
Account disable operations require an administrator actor.
```

Keep:

```text
account.disable.not-administrator
```

Run the sample again.

The existing scenario should continue to pass.

Now consider downstream code that depends on prose:

```csharp
if (decision.Reasons[0].Message.Contains("administrators"))
{
    ...
}
```

Answer:

1. Why is this fragile?
2. What happens when wording is localized?
3. What happens when a message is improved for clarity?
4. Which part of the decision should software depend on instead?

The reason message is for people.

The reason code is the stable machine-readable contract.

---

# Part 6 — Validate the Final Architecture

Run the modified sample again.

Confirm all of the following:

- Existing baseline scenarios still produce their intended outcomes.
- The new classification value is part of the explicit policy-context snapshot.
- Adding the context fact alone did not silently create policy behavior.
- The new `Restricted` rule returns a structured outcome rather than a boolean.
- The new rule has a stable reason code.
- The overlapping scenario makes precedence observable and testable.
- Changing human-readable reason text does not break scenario verification.
- `DisableAccountPolicyContext` still contains facts rather than service dependencies.
- `DisableAccountPolicy` remains the component that interprets those facts.
- `GovernanceDecision` describes the result but performs no side effect.

Your exact scenario count may now be greater than the original seven.

Do not preserve the original count artificially.

The useful invariant is that every declared scenario produces the expected structured result.

---

# Part 7 — Reason About a Scattered Alternative

Consider this alternative implementation:

```csharp
bool allowed =
    user.IsAdministrator &&
    user.TenantId == account.TenantId &&
    !account.IsProtected &&
    !maintenanceHold &&
    account.Classification != DataClassification.Restricted;
```

Answer these questions:

1. Where would `Deferred` be represented?
2. Where would `AcknowledgmentRequired` be represented?
3. How would the host distinguish a protected resource from a restricted-classification resource?
4. Where would stable reason codes live?
5. How would you reproduce exactly which facts were evaluated during an incident?
6. If the authorization expression grows across controllers and services, how easy is it to identify the actual policy contract?
7. Is a compact boolean expression always wrong, or does its suitability depend on whether the domain truly has only two meaningful states?

Some operations genuinely need only a small yes/no check.

The lesson is not to replace every boolean with a framework.

The lesson is to avoid compressing a richer governance lifecycle into a boolean when the system needs to preserve multiple states, reasons, routing choices, or evidence.

---

# Completion Criteria

You have completed the lab when you can demonstrate this progression:

```text
Scattered or compressed decision information
        ↓
Explicit context snapshot
        ↓
Facts remain separate from rules
        ↓
Policy interprets facts
        ↓
Structured outcome
        ↓
Stable reason code
        ↓
Intentional precedence
        ↓
Host can determine the next governed step
```

You should also be able to explain why these two statements are different:

```text
The operation may not proceed.
```

and:

```text
The operation may not proceed because it must be deferred,
acknowledged, denied, or escalated.
```

The second preserves information that the host can use.

## Optional Extension — Add Policy Identity to Verification

The sample already carries:

```text
CorrelationId
PolicyVersion
```

Extend `PolicyScenario` so it also declares the expected policy version, then verify that the context contains it.

Change one scenario to use a different policy-version string while leaving the policy rules unchanged.

Discuss:

- Why can policy identity matter even when it does not change the immediate outcome?
- Why is a version or hash useful for later audit interpretation?
- What additional evidence would a production system need before claiming that a decision is fully reproducible?

This prepares for the next tutorial, where acknowledgment and audit residue become first-class concerns.

## Resetting the Sample

If you created a temporary branch only for the exercise, you can compare your work with the original sample and then discard or keep the branch as desired.

To discard uncommitted changes to the sample:

```bash
git restore samples/policy-context-and-explicit-decision-outcomes/PolicyContextAndExplicitDecisionOutcomes/Program.cs
```

Use `git status` before restoring anything so that you understand which local changes will be affected.

---

## Related Content

- [Policy Context and Explicit Decision Outcomes tutorial](../tutorials/policy-context-and-explicit-decision-outcomes.md) — review the architectural reasoning behind the lab.
- [Policy Context and Explicit Decision Outcomes sample](https://github.com/AsiBackbone/Learning/blob/main/samples/policy-context-and-explicit-decision-outcomes/README.md) — return to the executable baseline used by this exercise.
- [Decision Before Execution lab](decision-before-execution.md) — practice the earlier boundary between decision and host-owned execution.
- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) — continue from structured outcomes into acknowledgment, lineage, and governance evidence.
- [Foundational Tutorial Index](../tutorials/index.md) — view the complete foundational learning path.
- [`GovernanceDecisionOutcome`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecisionOutcome.cs) — compare the teaching vocabulary with the working framework.
- [`GovernanceDecision`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecision.cs) — inspect the fuller decision model and reason metadata.
- [`IAsiBackboneConstraintEvaluationContext`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Constraints/IAsiBackboneConstraintEvaluationContext.cs) — compare the explicit teaching snapshot with the framework context surface.
- [`DefaultAsiBackbonePolicyEvaluator`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Evaluation/DefaultAsiBackbonePolicyEvaluator.cs) — inspect fuller constraint evaluation and decision composition.

---

> **Read it. Run it. Question it. Improve it.**
