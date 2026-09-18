---
description: Distinguish Learning teaching models from the finalized AsiBackbone 6.0 API, including supported construction, endpoint markers, and removed members.
---

# AsiBackbone 6.0 API Boundary

AsiBackbone Learning teaches architecture with two different kinds of code:

1. **Learning-owned teaching models** are small, framework-neutral types compiled from this repository. They make an architectural boundary easy to observe, but they are not package API signatures.
2. **AsiBackbone 6.0 API examples** use the finalized public names and namespaces from the implementation repository's `release/6.0.0` branch.

Keep that distinction visible when copying an example. A local teaching type named for a concept may be intentionally smaller than the similarly named framework type.

> **API status:** Unless a section is labeled **AsiBackbone 6.0 API**, code in Learning is illustrative or belongs to a Learning sample. Follow the linked implementation source or the [5.x to 6.0 migration guide](https://github.com/AsiBackbone/AsiBackbone/blob/release/6.0.0/docs/articles/upgrade-500-to-600.md) for exact package syntax.

## Executable Sample Package Policy

The projects under `samples/` are executable Learning companions, not package-integration samples. They currently contain no `PackageReference` to an `AsiBackbone.*` package. Their local records, policies, evaluators, and gateways are teaching models owned by this repository.

That is deliberate:

- the samples remain runnable while the 6.0 package line is prepared;
- architectural invariants can be studied without adopting a framework;
- local types do not claim to reproduce every constructor, member, namespace, persistence seam, or security control in the implementation.

If a future sample adds an `AsiBackbone.*` package reference, it must pin a released 6.x version through the sample package-management files, update the lock file, compile against that package, and identify itself as a package-integration sample.

## Finalized Core Names

The complete review covers 232 public type entries across ten managed packages. The authoritative inventory is the [6.0 public API naming convention](https://github.com/AsiBackbone/AsiBackbone/blob/release/6.0.0/docs/articles/public-api-naming-600.md).

The names most often used by Learning material are:

| Purpose | AsiBackbone 6.0 public type |
| --- | --- |
| Evaluation context contract | `AsiBackbone.Core.Constraints.IGovernanceEvaluationContext` |
| Default context | `AsiBackbone.Core.Constraints.GovernanceEvaluationContext` |
| Constraint | `AsiBackbone.Core.Constraints.IGovernanceConstraint<TContext>` |
| Policy evaluator | `AsiBackbone.Core.Evaluation.IGovernancePolicyEvaluator<TContext>` |
| Default evaluator | `AsiBackbone.Core.Evaluation.DefaultGovernancePolicyEvaluator<TContext>` |
| Evaluator builder | `AsiBackbone.Core.Evaluation.GovernancePolicyEvaluatorBuilder<TContext>` |
| Evaluator options | `AsiBackbone.Core.Evaluation.GovernancePolicyOptions` |
| Decision policy | `AsiBackbone.Core.Evaluation.IGovernanceDecisionPolicy<TContext>` |
| Decision | `AsiBackbone.Core.Decisions.GovernanceDecision` |
| Decision receipt | `AsiBackbone.Core.Audit.DecisionReceipt` |
| Receipt sink | `AsiBackbone.Core.Audit.IDecisionReceiptSink` |
| Durable audit ledger | `AsiBackbone.Core.Audit.IGovernanceAuditLedgerStore` |
| Actor context | `AsiBackbone.Core.Actors.GovernanceActorContext` |
| ASP.NET Core endpoint service | `AsiBackbone.AspNetCore.Endpoints.IEndpointGovernanceService` |
| Acknowledgment challenge service | `AsiBackbone.AspNetCore.Handshakes.IAcknowledgmentChallengeService` |

These are implementation types. A Learning snippet that defines its own decision, receipt, context, or acknowledgment record is a teaching model unless the section explicitly says otherwise.

## Construct an Evaluator

The five partial evaluator constructors retained during 5.x do not exist in 6.0. Use the builder or the full-dependency constructor.

**AsiBackbone 6.0 API:**

```csharp
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Evaluation;

IGovernancePolicyEvaluator<GovernanceEvaluationContext> evaluator =
    DefaultGovernancePolicyEvaluator
        .CreateBuilder<GovernanceEvaluationContext>()
        .AddConstraints(constraints)
        .AddThreatModelContributors(threatModelContributors)
        .WithDecisionPolicy(decisionPolicy)
        .WithOptions(options)
        .WithLogger(logger)
        .Build();
```

The supported direct constructor accepts the complete dependency set:

**AsiBackbone 6.0 API:**

```csharp
var evaluator = new DefaultGovernancePolicyEvaluator<MyPolicyContext>(
    constraints,
    threatModelContributors: null,
    decisionPolicy: null,
    options: null,
    logger: null);
```

For dependency injection, prefer a factory that resolves the host's configured constraints, contributors, decision policy, `IOptions<GovernancePolicyOptions>.Value`, and logger before calling the builder. Registering only the concrete evaluator type requires the container to resolve all five constructor dependencies.

Inspect the exact implementation in [`DefaultGovernancePolicyEvaluator`](https://github.com/AsiBackbone/AsiBackbone/blob/release/6.0.0/src/AsiBackbone.Core/Evaluation/DefaultGovernancePolicyEvaluator.cs), its [`CreateBuilder` factory](https://github.com/AsiBackbone/AsiBackbone/blob/release/6.0.0/src/AsiBackbone.Core/Evaluation/DefaultGovernancePolicyEvaluator.Factory.cs), and [`GovernancePolicyEvaluatorBuilder`](https://github.com/AsiBackbone/AsiBackbone/blob/release/6.0.0/src/AsiBackbone.Core/Evaluation/GovernancePolicyEvaluatorBuilder.cs).

## Mark Endpoint Policy Metadata

The 5.x `RequireGovernancePolicy` route-builder methods were removed. Use `MarkGovernancePolicy`.

**AsiBackbone 6.0 API:**

```csharp
using AsiBackbone.AspNetCore.Endpoints;

app.MapPost("/operations", HandleOperation)
    .MarkGovernancePolicy<ConsequentialOperationPolicy>();

app.MapPost("/exports", HandleExport)
    .MarkGovernancePolicy(typeof(ExportPolicyMarker));
```

The marker records policy metadata. It does not, by itself, resolve a policy, select constraints, or enforce execution. See the exact [`EndpointGovernanceRouteBuilderExtensions`](https://github.com/AsiBackbone/AsiBackbone/blob/release/6.0.0/src/AsiBackbone.AspNetCore/Endpoints/EndpointGovernanceRouteBuilderExtensions.cs) behavior.

## Removed Compatibility Surface

The upstream obsolete-member inventory found exactly seven public members whose compatibility window ended at 6.0:

| Removed after 5.x | Supported 6.0 path |
| --- | --- |
| `DefaultAsiBackbonePolicyEvaluator<TContext>(constraints, decisionPolicy = null)` | Builder with `AddConstraints` and `WithDecisionPolicy` |
| `DefaultAsiBackbonePolicyEvaluator<TContext>(constraints, decisionPolicy, options)` | Builder plus `WithOptions` |
| `DefaultAsiBackbonePolicyEvaluator<TContext>(constraints, decisionPolicy, options, logger)` | Builder plus `WithOptions` and `WithLogger` |
| `DefaultAsiBackbonePolicyEvaluator<TContext>(constraints, threatModelContributors, decisionPolicy = null)` | Builder plus `AddThreatModelContributors` and `WithDecisionPolicy` |
| `DefaultAsiBackbonePolicyEvaluator<TContext>(constraints, threatModelContributors, decisionPolicy, options)` | Builder plus contributors, policy, and options |
| `RequireGovernancePolicy<TPolicy>(RouteHandlerBuilder)` | `MarkGovernancePolicy<TPolicy>()`, or `MarkGovernancePolicy(typeof(TPolicy))` for a plain marker |
| `RequireGovernancePolicy<TBuilder>(TBuilder, Type)` | `MarkGovernancePolicy(builder, policyType)` |

The non-obsolete `RequireGovernancePolicyAttribute` remains part of the implementation. Historical 4.x and 5.x migration material may retain old names when it is clearly presented as history; current examples must not present the removed members as callable 6.0 APIs.

## Decision Receipt Is Not Execution Proof

`DecisionReceipt` records the policy decision outcome and reasons. Later acknowledgment, capability, gateway, emission, and execution lifecycle evidence can correlate with it, but the decision receipt does not prove that the host executed the protected operation.

Learning samples may use smaller local receipt or lifecycle records to make that distinction visible. Compare them with the exact 6.0 [`DecisionReceipt`](https://github.com/AsiBackbone/AsiBackbone/blob/release/6.0.0/src/AsiBackbone.Core/Audit/DecisionReceipt.cs) and [`DecisionReceiptLifecycleEvent`](https://github.com/AsiBackbone/AsiBackbone/blob/release/6.0.0/src/AsiBackbone.Core/Audit/DecisionReceiptLifecycleEvent.cs) source before adopting a package integration.

## Review Checklist

Before publishing an API-facing Learning change:

- label teaching code as illustrative or Learning-owned;
- label exact framework syntax as AsiBackbone 6.0 API;
- use finalized 6.0 names and namespaces;
- do not call removed evaluator constructors or route-builder methods;
- link implementation source to `release/6.0.0`, not `main` or a 5.x branch;
- pin any future `AsiBackbone.*` sample package reference to a released 6.x version and commit its lock-file update;
- preserve old names only in clearly historical migration or release material.

The repository's API-reference validator enforces the machine-checkable parts of this boundary during documentation validation.
