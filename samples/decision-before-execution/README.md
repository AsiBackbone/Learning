# Decision Before Execution Sample

This executable companion sample demonstrates the architectural boundary taught in the [Decision Before Execution](../../docs/tutorials/decision-before-execution.md) tutorial.

The sample keeps a proposed operation separate from the host-owned side effect:

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

The key invariant is:

> **A blocked decision never reaches the executor.**

## Learning Objective

Observe how an explicit governance decision controls whether a host-owned executor is invoked.

## Difficulty

Beginner

## Prerequisites

- .NET 10 SDK

## Run the Sample

From the repository root:

```bash
dotnet run --project samples/decision-before-execution/DecisionBeforeExecution/DecisionBeforeExecution.csproj
```

The sample evaluates five deterministic scenarios:

- Allowed
- Denied
- Escalation recommended
- Deferred
- Acknowledgment required

Only the allowed scenario crosses the execution boundary. The executor performs no real account operation; it records and prints a simulated host action.

At the end of the run, the sample verifies that exactly one scenario reached execution. If a blocked decision accidentally reaches the executor, the program fails instead of silently accepting the boundary violation.

## What to Observe

The evaluator returns a decision but never performs the side effect.

The workflow checks `decision.CanExecute` before invoking the executor.

The executor is host-owned and receives only operations that have already crossed the explicit decision boundary.

## What This Sample Intentionally Omits

This is a teaching artifact, not a production application. It intentionally omits:

- Authentication infrastructure
- Persistent storage
- Distributed policy sources
- Durable audit storage
- External services
- Real administrative side effects
- The fuller `AsiBackbone` package abstractions

## Try It

After running the sample, modify one scenario or policy rule and observe whether the executor invocation count changes.

Useful experiments include:

1. Make the allowed scenario non-administrative and confirm that total execution drops to zero.
2. Remove the `if (!decision.CanExecute)` guard and observe why the host boundary matters.
3. Add another decision outcome without changing the executor.

## Related Material

- [Decision Before Execution tutorial](../../docs/tutorials/decision-before-execution.md)
- [Decision Before Execution beginner lab](../../docs/labs/decision-before-execution.md)
- [Policy Context and Explicit Decision Outcomes](../../docs/tutorials/policy-context-and-explicit-decision-outcomes.md)
- [`GovernanceDecision`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecision.cs) - compare the teaching decision model with the fuller framework decision type.
- [`DefaultAsiBackbonePolicyEvaluator`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Evaluation/DefaultAsiBackbonePolicyEvaluator.cs) - inspect fuller policy and constraint evaluation.
- [Host-Owned Execution Enforcement](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/host-owned-execution-enforcement.md) - follow the production-oriented execution-boundary guidance.
- [Plain ASP.NET Core Host](https://github.com/AsiBackbone/AsiBackbone/tree/main/samples/PlainAspNetCoreHost) - inspect a concrete host integration.

## License

Executable sample code under `samples/` is licensed under the MIT License. See [LICENSING.md](../../LICENSING.md).
