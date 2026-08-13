# Lab — Decision Before Execution

**Learning objective:** Practice preserving an explicit decision boundary so that blocked operations cannot reach a host-owned executor.

**Difficulty:** Beginner  
**Prerequisites:** Complete the [Decision Before Execution tutorial](../tutorials/decision-before-execution.md) and run the [Decision Before Execution sample](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-before-execution/README.md).

This lab builds directly on the first foundational tutorial and its executable companion sample.

The tutorial explains the boundary.

The sample demonstrates the boundary.

This lab asks you to **break, observe, repair, and extend** the boundary yourself.

> **A blocked decision should never reach execution.**

---

## Starting Architecture

The companion sample uses this flow:

```text
Intent
   ↓
Context
   ↓
Policy evaluation
   ↓
Decision
   ↓
Execution boundary
   ↓
Simulated host operation
```

The important invariant is:

```text
Blocked decision
   ↓
Executor invocation count = 0
```

The sample deliberately uses a simulated executor, so the lab can focus on architecture without performing a real administrative side effect.

## Prepare the Lab

Work on a temporary branch or disposable copy of the repository so that you can safely modify the sample.

For example:

```bash
git switch -c lab/decision-before-execution
```

From the repository root, run the companion sample before making changes:

```bash
dotnet run --project samples/decision-before-execution/DecisionBeforeExecution/DecisionBeforeExecution.csproj
```

The baseline should finish with:

```text
Invariant preserved: blocked decisions never reached the executor.
Total simulated executions: 1
```

Before continuing, locate these elements in `Program.cs`:

1. `DisableAccountIntent`
2. `DisableAccountContext`
3. `DisableAccountPolicy`
4. `GovernanceDecision`
5. `DisableAccountWorkflow`
6. `RecordingDisableAccountExecutor`

You should be able to point to the exact line where the workflow decides whether execution may occur.

---

# Part 1 — Break the Boundary Deliberately

The current workflow contains an execution guard similar to:

```csharp
if (!decision.CanExecute)
{
    return decision;
}

await executor.ExecuteAsync(
    context.Intent,
    cancellationToken);
```

Temporarily remove or bypass the guard so that the executor is invoked regardless of the decision outcome.

For example, reduce the workflow to behavior equivalent to:

```csharp
GovernanceDecision decision = policy.Evaluate(context);

await executor.ExecuteAsync(
    context.Intent,
    cancellationToken);

return decision;
```

Run the sample again.

## Observe

Do not focus only on whether the decision values are still correct.

Ask instead:

- Are `Denied` operations reaching the executor?
- Is `Deferred` still meaningful if execution already occurred?
- What does `AcknowledgmentRequired` mean if the side effect happens before acknowledgment?
- Can an `EscalationRecommended` result protect anything when the operation has already executed?
- Does a correct decision object matter if the host ignores it?

The sample's final invariant check should now fail because more than one scenario reached execution.

That failure is useful.

It demonstrates that:

> **A decision model governs execution only when the host actually conditions execution on the decision.**

---

# Part 2 — Repair the Boundary

Restore the explicit guard:

```csharp
if (!decision.CanExecute)
{
    return decision;
}

await executor.ExecuteAsync(
    context.Intent,
    cancellationToken);
```

Run the sample again:

```bash
dotnet run --project samples/decision-before-execution/DecisionBeforeExecution/DecisionBeforeExecution.csproj
```

The invariant should be restored:

```text
Invariant preserved: blocked decisions never reached the executor.
Total simulated executions: 1
```

## Explain the Repair

Before moving on, write a one- or two-sentence explanation in your own words answering:

> Why is `decision.CanExecute` an architectural boundary rather than merely another `if` statement?

A useful answer should address **when the side effect becomes reachable**, not merely what value the decision contains.

---

# Part 3 — Add a New Constraint Without Changing the Executor

Now extend the governance context with one additional fact:

```csharp
bool AccountAlreadyDisabled
```

Add the property to `DisableAccountContext` and update the sample scenarios accordingly.

Then add a policy rule that prevents an already-disabled account from reaching execution.

Choose an appropriate outcome and reason code.

One reasonable design is:

```text
Outcome: Denied
ReasonCode: account.disable.already-disabled
```

But you should decide whether `Denied` is the best semantic outcome for this sample and be able to explain your choice.

## Constraint

**Do not modify `RecordingDisableAccountExecutor` to implement the rule.**

The executor should remain responsible only for performing the host-owned operation after the decision boundary has been crossed.

If you feel forced to add policy logic inside the executor, reconsider where the responsibility belongs.

Add at least one deterministic scenario where:

```text
Requester is administrator
Account is not protected
Maintenance hold is false
Reason is supplied
Account is already disabled
```

The new scenario must not invoke the executor.

---

# Part 4 — Validate the Architecture

Run the sample again.

Your exact total execution count may depend on how you changed the scenarios, so validate the architectural behavior rather than copying a number blindly.

Confirm all of the following:

- An allowed operation reaches the executor.
- A denied operation does not reach the executor.
- A deferred operation does not reach the executor.
- An acknowledgment-required operation does not reach the executor.
- An escalation-recommended operation does not reach the executor.
- The already-disabled-account scenario does not reach the executor.
- `DisableAccountPolicy` evaluates the constraint but performs no side effect.
- `RecordingDisableAccountExecutor` performs no policy evaluation.
- `DisableAccountWorkflow` remains the component that enforces the transition from decision to execution.

If your final invariant check assumes exactly one execution, adjust scenarios carefully rather than weakening the invariant merely to make the program pass.

---

# Part 5 — Reason About an Alternative

Consider this alternative design:

```csharp
public sealed class DisableAccountExecutor
{
    public async Task ExecuteAsync(
        DisableAccountContext context,
        CancellationToken cancellationToken)
    {
        if (!context.RequesterIsAdministrator)
        {
            return;
        }

        if (context.IsProtectedAccount)
        {
            return;
        }

        await DisableAccountAsync(
            context.Intent.AccountId,
            cancellationToken);
    }
}
```

This implementation contains defensive checks, but it moves decision logic into the executor.

Answer these questions:

1. What becomes harder to test independently?
2. Where would `Deferred`, `AcknowledgmentRequired`, or `EscalationRecommended` belong?
3. Can the application produce a meaningful decision before execution?
4. What happens when several executors duplicate similar checks?
5. Are defensive checks at an execution boundary always wrong, or is the problem that they have become the primary policy model?

The final question matters.

A production executor may still validate assumptions, capabilities, resource state, or safety invariants at the execution boundary.

The lesson is not that executors must trust everything blindly.

The lesson is that **the primary governance decision should remain explicit and should exist before the consequential side effect becomes reachable**.

---

# Completion Criteria

You have completed the lab when you can demonstrate all of the following:

```text
Proposal exists before execution
        ↓
Context is explicit
        ↓
Policy returns a decision
        ↓
Blocked decisions stop
        ↓
Only allowed decisions cross the execution boundary
        ↓
Executor remains host-owned
```

You should also be able to explain why a system can return the correct decision values and still have a broken architecture if the host does not enforce those decisions before execution.

## Optional Extension

Add a second new constraint of your choice, such as:

- A reason must contain a minimum amount of useful information.
- Certain account classes require escalation.
- A temporary operational freeze causes deferral.

Preserve this rule:

> Adding the new governance constraint should not require changing the executor's responsibility.

If the executor must change merely because a new decision rule was introduced, examine whether policy and execution have become too tightly coupled.

## Resetting the Sample

If you created a temporary branch only for the exercise, you can compare your work with the original sample and then discard or keep the branch as desired.

To discard uncommitted changes to the sample:

```bash
git restore samples/decision-before-execution/DecisionBeforeExecution/Program.cs
```

Use `git status` before restoring anything so that you understand which local changes will be affected.

---

## Related Content

- [Decision Before Execution tutorial](../tutorials/decision-before-execution.md) — review the architectural reasoning behind the lab.
- [Decision Before Execution sample](https://github.com/AsiBackbone/Learning/blob/main/samples/decision-before-execution/README.md) — return to the known executable baseline.
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) — continue into richer policy facts and structured outcomes.
- [Foundational Tutorial Index](../tutorials/index.md) — view the complete foundational learning path.
- [AsiBackbone working implementation](https://github.com/AsiBackbone/AsiBackbone) — inspect fuller governance and decision-flow implementations.

---

> **Read it. Run it. Question it. Improve it.**
