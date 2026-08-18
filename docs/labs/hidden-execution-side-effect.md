# Lab — Identify and Remove a Hidden Execution Side Effect

**Learning objective:** Recognize when code that appears to validate, authorize, or evaluate policy performs a consequential side effect, then refactor the workflow so blocked decisions cannot reach execution.

**Difficulty:** Beginner  

**Pattern classification:** Canonical pattern  

**Prerequisites:** Complete the [Decision Before Execution tutorial](../tutorials/decision-before-execution.md). Running the [Decision Before Execution sample](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-before-execution/README.md) first is recommended.

This lab starts with code that is deliberately wrong.

The method is named like a policy check. It returns a structured decision. Its denial result is correct.

It also starts an external deployment before the decision is complete.

The invariant for this exercise is:

```text
Denied Decision
      ↓
Consequential External Side Effects = 0
```

The goal is not merely to move one line until a test passes.

The goal is to make the architecture itself prevent evaluation from quietly becoming execution.

---

## What Counts as a Hidden Side Effect?

A hidden side effect is consequential behavior performed by code whose apparent responsibility is observation, validation, authorization, or policy evaluation.

Examples include:

- modifying application or database state;
- publishing an event or queue message;
- sending a notification;
- invoking an external command or administrative operation;
- starting a deployment or job;
- charging, refunding, deleting, disabling, provisioning, or otherwise changing a resource.

A method can return the correct `Denied` decision and still be architecturally broken if the requested action already happened.

This lab focuses on **consequential business or external side effects**. Operational logging, metrics, and read-only context gathering have different tradeoffs and are discussed later.

---

# Part 1 — Create the Deliberately Flawed Starting Point

Use a disposable scratch directory so that the intentionally failing exercise does not affect the repository's normal sample tests.

For example:

```bash
mkdir HiddenExecutionSideEffectLab
cd HiddenExecutionSideEffectLab
dotnet new xunit
```

Delete the generated test file and create `DeploymentPolicyTests.cs` with the following code.

```csharp
using Xunit;

public enum DecisionOutcome
{
    Allowed,
    Denied
}

public sealed record DeploymentDecision(
    DecisionOutcome Outcome,
    string ReasonCode)
{
    public bool CanExecute => Outcome == DecisionOutcome.Allowed;

    public static DeploymentDecision Allow() =>
        new(DecisionOutcome.Allowed, "deployment.allowed");

    public static DeploymentDecision Deny(string reasonCode) =>
        new(DecisionOutcome.Denied, reasonCode);
}

public sealed record DeploymentContext(
    bool RequesterIsOperator,
    bool ChangeApproved,
    string Environment,
    string Version);

public interface IDeploymentClient
{
    Task StartDeploymentAsync(
        string environment,
        string version,
        CancellationToken cancellationToken);
}

public sealed class RecordingDeploymentClient : IDeploymentClient
{
    public int StartCount { get; private set; }

    public Task StartDeploymentAsync(
        string environment,
        string version,
        CancellationToken cancellationToken)
    {
        StartCount++;
        return Task.CompletedTask;
    }
}

public sealed class DeploymentPolicy(IDeploymentClient deploymentClient)
{
    public async Task<DeploymentDecision> CheckAsync(
        DeploymentContext context,
        CancellationToken cancellationToken)
    {
        if (!context.RequesterIsOperator)
        {
            return DeploymentDecision.Deny(
                "deployment.requester-not-operator");
        }

        // Hidden execution side effect inside a method that appears to check policy.
        await deploymentClient.StartDeploymentAsync(
            context.Environment,
            context.Version,
            cancellationToken);

        if (string.Equals(
                context.Environment,
                "production",
                StringComparison.OrdinalIgnoreCase) &&
            !context.ChangeApproved)
        {
            return DeploymentDecision.Deny(
                "deployment.change-approval-required");
        }

        return DeploymentDecision.Allow();
    }
}

public sealed class DeploymentPolicyTests
{
    [Fact]
    public async Task Denied_decision_produces_no_deployment_side_effect()
    {
        var client = new RecordingDeploymentClient();
        var policy = new DeploymentPolicy(client);

        var context = new DeploymentContext(
            RequesterIsOperator: true,
            ChangeApproved: false,
            Environment: "production",
            Version: "2026.08.18");

        DeploymentDecision decision = await policy.CheckAsync(
            context,
            CancellationToken.None);

        Assert.Equal(DecisionOutcome.Denied, decision.Outcome);
        Assert.Equal(0, client.StartCount);
    }
}
```

Run the test:

```bash
dotnet test
```

The decision assertion should pass.

The side-effect assertion should fail because the deployment client was invoked once.

Conceptually, the failure is:

```text
Decision outcome = Denied
Deployment starts = 1
Expected deployment starts = 0
```

Do not repair the code yet.

The failing test is the evidence you need for the next step.

---

# Part 2 — Diagnose the Architectural Failure

Read `DeploymentPolicy.CheckAsync` as if you were reviewing unfamiliar application code.

Classify each operation as one of these categories:

```text
Observation
Decision
Execution
```

Then answer:

1. Which line first makes a consequential external action reachable?
2. Does the method name `CheckAsync` accurately describe everything the method does?
3. Why is the final `Denied` result insufficient protection?
4. Which dependency gives policy evaluation execution authority?
5. If a future outcome were `Deferred`, `AcknowledgmentRequired`, or `Escalated`, what would those outcomes mean after deployment had already started?
6. Could a caller safely evaluate the policy only to preview what the decision would be?

The central diagnosis should be:

> **The policy object is not merely deciding whether deployment is allowed. It also owns the authority to perform the deployment.**

That responsibility coupling is the defect.

---

# Part 3 — Reject the Superficial Fix

The easiest repair is tempting:

```csharp
if (productionRequiresApproval)
{
    return DeploymentDecision.Deny(
        "deployment.change-approval-required");
}

await deploymentClient.StartDeploymentAsync(...);

return DeploymentDecision.Allow();
```

This change would make the current denied test pass.

It does **not** establish the stronger architecture required by the lab.

The policy still owns `IDeploymentClient`. A future rule, refactor, exception path, or additional decision outcome can once again mix evaluation and execution.

Before moving on, write a short explanation of why this version is better in ordering but still weak in responsibility boundaries.

A useful answer should distinguish:

```text
"The side effect happens later"
```

from:

```text
"The policy cannot perform the side effect at all"
```

---

# Part 4 — Refactor to an Explicit Decision Boundary

Refactor the exercise under these constraints:

1. `DeploymentPolicy` must not depend on `IDeploymentClient`.
2. Policy evaluation must return a `DeploymentDecision` without starting a deployment.
3. The component that owns the deployment client must evaluate the decision before invoking it.
4. A denied decision must return before the external call becomes reachable.
5. The external side effect must remain visible in one explicit execution path.

The target flow is:

```text
Deployment request
       ↓
Build explicit context
       ↓
Policy evaluation
       ↓
DeploymentDecision
       ↓
CanExecute?
   ┌───┴────┐
   │        │
  No       Yes
   │        │
 Return     ↓
          External deployment
```

You may introduce a `DeploymentWorkflow`, `DeploymentHandler`, or similarly named host-owned component.

Prefer a policy API shaped like:

```csharp
DeploymentDecision Evaluate(DeploymentContext context)
```

rather than an asynchronous policy method that retains an execution-capable dependency.

Do not copy the reference solution yet.

Make the smallest refactor that enforces the responsibility boundary.

---

# Part 5 — Make the Invariant Observable in Tests

After refactoring, preserve the original denied test and add an allowed-path test.

Your tests should prove at least these two behaviors:

```text
Denied request
   ↓
Decision = Denied
   ↓
Deployment StartCount = 0
```

```text
Allowed request
   ↓
Decision = Allowed
   ↓
Deployment StartCount = 1
```

Add another denied scenario where the requester is not an operator.

That gives you two different denial paths and helps prevent a narrow fix that only handles one ordering case.

Recommended test names:

```text
Denied_production_request_produces_no_deployment_side_effect
Denied_non_operator_request_produces_no_deployment_side_effect
Allowed_request_executes_exactly_once
```

The important assertion is not only the decision value.

It is the combination:

```text
Blocked decision + zero consequential execution
```

---

# Part 6 — Inspect for Side-Effect Camouflage

Now imagine the original external call had been hidden behind a more innocent name:

```csharp
await deploymentValidator.ValidateTargetAsync(...);
```

Suppose `ValidateTargetAsync` internally created a deployment job as part of its "validation."

The caller would look observational even though the dependency was not.

For code review, ask these questions whenever you encounter validation or policy code:

- Does this dependency write state?
- Does it publish anything?
- Does it send anything?
- Does it enqueue or schedule work?
- Does it invoke a command endpoint?
- Does it create, delete, disable, charge, provision, deploy, or mutate a resource?
- Does a method named `Check`, `Validate`, `Authorize`, or `Evaluate` have a dependency capable of doing those things?

Names are clues.

They are not enforcement boundaries.

---

# Reference Solution — Read After Attempting the Refactor

One compact solution is to make the policy observational and let a workflow enforce the transition from decision to execution.

```csharp
public sealed class DeploymentPolicy
{
    public DeploymentDecision Evaluate(DeploymentContext context)
    {
        if (!context.RequesterIsOperator)
        {
            return DeploymentDecision.Deny(
                "deployment.requester-not-operator");
        }

        if (string.Equals(
                context.Environment,
                "production",
                StringComparison.OrdinalIgnoreCase) &&
            !context.ChangeApproved)
        {
            return DeploymentDecision.Deny(
                "deployment.change-approval-required");
        }

        return DeploymentDecision.Allow();
    }
}

public sealed class DeploymentWorkflow(
    DeploymentPolicy policy,
    IDeploymentClient deploymentClient)
{
    public async Task<DeploymentDecision> RunAsync(
        DeploymentContext context,
        CancellationToken cancellationToken)
    {
        DeploymentDecision decision = policy.Evaluate(context);

        if (!decision.CanExecute)
        {
            return decision;
        }

        await deploymentClient.StartDeploymentAsync(
            context.Environment,
            context.Version,
            cancellationToken);

        return decision;
    }
}
```

The policy can now be evaluated without an execution-capable dependency.

The execution line exists only after the explicit guard:

```csharp
if (!decision.CanExecute)
{
    return decision;
}
```

That is the boundary the tests should protect.

A corresponding allowed-path test can look like:

```csharp
[Fact]
public async Task Allowed_request_executes_exactly_once()
{
    var client = new RecordingDeploymentClient();
    var workflow = new DeploymentWorkflow(
        new DeploymentPolicy(),
        client);

    var context = new DeploymentContext(
        RequesterIsOperator: true,
        ChangeApproved: true,
        Environment: "production",
        Version: "2026.08.18");

    DeploymentDecision decision = await workflow.RunAsync(
        context,
        CancellationToken.None);

    Assert.Equal(DecisionOutcome.Allowed, decision.Outcome);
    Assert.Equal(1, client.StartCount);
}
```

Rewrite the denied tests to invoke `DeploymentWorkflow.RunAsync` as well.

All blocked paths should leave `StartCount` at zero.

---

# Discussion — Does Evaluation Need to Be Mathematically Pure?

Not necessarily.

Real systems may need to gather authoritative facts from databases, directories, caches, or external services before a decision can be made.

The stronger practical rule is:

> **Evaluation must not perform the consequential operation it is supposed to govern, and blocked decisions must not produce consequential external side effects.**

A useful separation is:

```text
Context construction
   ↓
May gather authoritative facts
   ↓
Policy evaluation
   ↓
Produces decision
   ↓
Execution boundary
   ↓
Performs consequential operation
```

Operational logging and metrics are also technically side effects, but they are not the same thing as executing the governed business operation. They should still be designed deliberately, especially when they can leak sensitive data or trigger downstream automation.

Audit residue is another distinct concern. Recording that a decision occurred should not silently become the requested external operation itself.

The invariant in this beginner lab is intentionally narrower and easier to observe:

```text
Denied Decision
      ↓
Governed External Operation Invocations = 0
```

---

# Completion Criteria

You have completed the lab when you can demonstrate all of the following:

- You can identify the hidden side effect in the deliberately flawed starting code.
- `DeploymentPolicy` no longer owns an execution-capable deployment dependency.
- The policy returns a decision before the external operation becomes reachable.
- Two different denied scenarios produce zero deployment invocations.
- An allowed scenario produces exactly one deployment invocation.
- The tests fail if the execution guard is removed or bypassed.
- You can explain why moving the side effect lower inside the policy is weaker than removing execution authority from the policy entirely.
- You can distinguish context gathering, policy evaluation, and host-owned execution.

The architectural invariant should now be visible rather than assumed:

```text
Evaluation
   ↓
Decision
   ↓
Blocked? ── Yes ──> Return with zero governed side effects
   │
   No
   ↓
Explicit execution boundary
   ↓
Host-owned external operation
```

## Optional Extension

Add a third decision outcome such as `Deferred`.

For example, a deployment may be deferred during a maintenance freeze.

Preserve this invariant:

```text
Deferred Decision
      ↓
Deployment StartCount = 0
```

Then answer:

> Why is adding `Deferred` straightforward after evaluation and execution have been separated, but awkward when a policy method performs the deployment itself?

---

## Related Content

- [Decision Before Execution tutorial](../tutorials/decision-before-execution.md) — review the architectural boundary this lab diagnoses from a different direction.
- [Decision Before Execution sample](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-before-execution/README.md) — compare the corrected sample flow with the flawed starter code in this exercise.
- [Decision Before Execution lab](decision-before-execution.md) — practice deliberately breaking and repairing the host execution guard.
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) — continue into richer context and non-boolean outcomes.
- [Intent-to-Execution Pattern](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/intent-to-execution-pattern.md) — inspect the fuller lifecycle from proposal through execution.
- [Host-Owned Execution Enforcement](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/host-owned-execution-enforcement.md) — compare the teaching boundary with the fuller implementation guidance.
- [`PolicyEvaluatorEndToEndTests`](https://github.com/AsiBackbone/AsiBackbone/blob/main/tests/AsiBackbone.Core.Tests/Evaluation/PolicyEvaluatorEndToEndTests.cs) — inspect tests that make policy/execution behavior observable in the implementation repository.

---

> **Read it. Run it. Question it. Improve it.**
