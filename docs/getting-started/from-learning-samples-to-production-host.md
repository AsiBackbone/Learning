---
description: Map five Learning samples to released AsiBackbone APIs and NetCoreApplicationTemplate host seams while preserving implementation boundaries.
learning_ref: v1.3.0
asibackbone_ref: v7.0.0
netcoreapplicationtemplate_ref: v2.11.2
---

# From Learning Samples to a Production Host

**Learning objective:** Translate the five foundational Learning models into version-pinned AsiBackbone implementation references and explicit ASP.NET Core host responsibilities.

**Pattern classification:** Canonical Pattern

**Difficulty:** Intermediate

**Prerequisites:** Complete the [five-part foundation](../tutorials/index.md#learning-path-at-a-glance), or use this page after the specific tutorial you are implementing.

Learning samples make architectural boundaries visible with small, local types. A production host needs more: authentication, authorization, authoritative state, dependency injection, persistence, transactions, failure handling, observability, and an executor that owns the real side effect.

This page connects those two views. It is a responsibility map, not a package-integration sample and not a claim that one host design is universally required.

## Reviewed Baselines

This bridge was reviewed against these released snapshots:

| Repository | Baseline | Role in this bridge |
| --- | --- | --- |
| Learning | [`v1.3.0`](https://github.com/AsiBackbone/Learning/releases/tag/v1.3.0) | Teaching models and five-part foundational sequence |
| AsiBackbone | [`v7.0.0`](https://github.com/AsiBackbone/AsiBackbone/releases/tag/v7.0.0) | Package names, public types, host adapters, persistence contracts, and runtime behavior |
| NetCoreApplicationTemplate | [`v2.11.2`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/releases/tag/v2.11.2) | ASP.NET Core composition, middleware, authentication, authorization, EF Core, and audit integration seams |

Learning 1.3 intentionally retains the reviewed AsiBackbone 7.0.0 boundary. AsiBackbone 7.1.0 is backward compatible, but its analyzer and deprecation additions are outside the Learning 1.3 validation claim. Use the [AsiBackbone 7.0 Compatibility and API Boundary](asibackbone-7-api-boundary.md) for the version rationale and migration-sensitive names.

NetCoreApplicationTemplate 2.11.2 does **not** include an `AsiBackbone.*` package reference or generated governance integration. It is an application-host specimen. A consuming application must deliberately select packages, registrations, policy, persistence, middleware placement, and execution behavior.

> [!IMPORTANT]
> Types defined under `samples/` are Learning-owned teaching models. Similar names indicate responsibility, not source, binary, serialization, or behavioral compatibility with an AsiBackbone package type.

## Foundation-to-Host Map

| Learning concern | Teaching model | AsiBackbone implementation reference | Host / DI or enforcement reference | Persistence / evidence reference | NetCoreApplicationTemplate integration seam |
| --- | --- | --- | --- | --- | --- |
| Decision Before Execution | [`DisableAccountIntent`, `GovernanceDecision`, `DisableAccountWorkflow`, `IDisableAccountExecutor`](https://github.com/AsiBackbone/Learning/blob/v1.3.0/samples/decision-before-execution/Sample/Program.cs) | [`GovernanceDecision`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.Core/Decisions/GovernanceDecision.cs) and [`IGovernancePolicyEvaluator`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.Core/Evaluation/IGovernancePolicyEvaluator.cs) are responsibility-level matches; there is no package `DisableAccountWorkflow` or executor | [`AddAsiBackboneAspNetCore`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.AspNetCore/DependencyInjection/AsiBackboneAspNetCoreServiceCollectionExtensions.cs), [`UseAsiBackboneEndpointGovernance`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.AspNetCore/Endpoints/EndpointGovernanceApplicationBuilderExtensions.cs), and host-owned endpoint/application service | [`DecisionReceipt`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.Core/Audit/DecisionReceipt.cs) plus a host-selected sink or ledger | Add registrations in [`Program.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/v2.11.2/src/ProjectTemplate.Web/Program.cs); place enforcement in the centralized [`PipelineExtensions`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/v2.11.2/src/ProjectTemplate.Web/Extensions/PipelineExtensions.cs); keep the real operation in host application code |
| Policy Context and Explicit Decision Outcomes | [`DisableAccountPolicyContext`, `GovernanceDecisionOutcome`, `DecisionReason`](https://github.com/AsiBackbone/Learning/blob/v1.3.0/samples/policy-context-and-explicit-decision-outcomes/Sample/Program.cs) | [`GovernanceEvaluationContext`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.Core/Constraints/GovernanceEvaluationContext.cs), [`GovernanceDecisionOutcome`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.Core/Decisions/GovernanceDecisionOutcome.cs), constraints, and a host-defined [`IGovernanceDecisionPolicy`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.Core/Evaluation/IGovernanceDecisionPolicy.cs) | The host authenticates the caller, loads current resource and tenant facts, chooses policy, and maps the result to transport behavior | Preserve correlation, policy identity, reason codes, and the facts safe to retain in the receipt; do not persist secrets merely because policy used them | Use NCAT authentication/authorization and its host-owned [`ICurrentActorAccessor`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/v2.11.2/src/ProjectTemplate.Infrastructure/Data/ICurrentActorAccessor.cs) as input seams; add operation-specific authoritative loaders rather than treating claims as current resource state |
| Decision Receipts and Acknowledgment | [`DecisionReceipt`, `AcknowledgmentChallenge`, `AcknowledgmentResponse`](https://github.com/AsiBackbone/Learning/blob/v1.3.0/samples/decision-receipts-and-acknowledgment/Sample/Program.cs) | [`DecisionReceipt`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.Core/Audit/DecisionReceipt.cs), [`AcknowledgmentRequest`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.Core/Acknowledgments/AcknowledgmentRequest.cs), [`AcknowledgmentResponse`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.Core/Acknowledgments/AcknowledgmentResponse.cs), and the ASP.NET Core [`IAcknowledgmentChallengeService`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.AspNetCore/Acknowledgments/IAcknowledgmentChallengeService.cs) | The host owns authenticated UI/API round trips, challenge retention, actor binding, re-evaluation, result mapping, and the rule that acknowledgment is not execution authority | Use host-owned implementations of [`IDecisionReceiptSink`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.Core/Audit/IDecisionReceiptSink.cs), lifecycle storage, ledger storage, and—when delivery is asynchronous—an outbox | NCAT's application mutation audit is a separate persistence contract, not a DecisionReceipt equivalent. Integrate through its [`ApplicationDbContext`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/v2.11.2/src/ProjectTemplate.Infrastructure/Data/ApplicationDbContext.cs) and reviewed audit/outbox boundary; use the optional [NCAT audit-completion adapter](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/docs/articles/ncat-audit-completion-adapter.md) only when its distinct handoff contract fits |
| Scoped Capability and Host-Owned Execution | [`ExecutionCapability`, `ExecutionCapabilityValidator`, `DisableAccountGateway`](https://github.com/AsiBackbone/Learning/blob/v1.3.0/samples/scoped-capability-and-host-owned-execution/Sample/Program.cs) | [`CapabilityGrant`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.Core/CapabilityGrants/CapabilityGrant.cs), [`CapabilityGrantValidationOptions.CreateBoundExecutionBoundary`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.Core/CapabilityGrants/CapabilityGrantValidationOptions.cs), [`CapabilityGrantBindingExpectations`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.Core/CapabilityGrants/CapabilityGrantBindingExpectations.cs), and [`CapabilityGrantValidator`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.Core/CapabilityGrants/CapabilityGrantValidator.cs) | Validate subject, operation, resource, audience, time, proof, and current state next to the protected executor. The framework validates authority; the host performs the effect | [`ICapabilityGrantUseStore`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.Core/CapabilityGrants/ICapabilityGrantUseStore.cs) is the bounded-use seam. The in-memory store is a local reference; durable distributed replay, revocation, and concurrency remain host work | NCAT has no capability-grant service. Register a host implementation in `Program.cs`; place durable use state in Infrastructure; keep external credentials and the protected executor outside policy code |
| Governed AI Tool Gateway | [`AiToolProposal`, `ToolRegistry`, `ProposalValidator`, `GovernedAiToolGateway`](https://github.com/AsiBackbone/Learning/blob/v1.3.0/samples/governed-ai-tool-gateway/Sample/Program.cs) | No one-to-one AI gateway package type exists. Compose decisions, receipts, acknowledgments, grants, and observability primitives; use the [AI Agent Gateway scenario](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/docs/articles/scenarios/ai-agent-gateway.md) as implementation guidance | The host owns tool allowlisting, schema validation, authoritative destination/context lookup, credentials, network controls, idempotency, retries, and final execution | Persist decision and execution evidence separately. Add durable acknowledgment/replay state and an outbox where the external effect and evidence cannot share one transaction | Add a host-owned application service and tool adapters; use NCAT authentication, authorization, Problem Details, logging, telemetry, data access, and background services as supporting seams. NCAT does not provide a model host or tool gateway |

## The Smallest Supported Host Sequence

The package-facing registration is intentionally explicit. In a plain ASP.NET Core host, the smallest endpoint-governance sequence begins like this:

```csharp
using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;

builder.Services.AddAsiBackboneAspNetCore();

// The host must also register the policy evaluator and every service
// requested by its endpoint metadata, such as a receipt sink or
// capability-grant validator. No production providers are selected here.

WebApplication app = builder.Build();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAsiBackboneEndpointGovernance();

app.MapControllers();
```

The exact registrations are determined by the metadata and features the host uses. [`AsiBackbone.AspNetCore`'s package README](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/src/AsiBackbone.AspNetCore/README.md) identifies the required evaluator, receipt sink, and capability validator contracts. The compile-ready [`PlainAspNetCoreHost`](https://github.com/AsiBackbone/AsiBackbone/tree/v7.0.0/samples/PlainAspNetCoreHost) is authoritative for a fuller package composition.

The order expresses four different decisions:

1. authentication establishes the caller identity;
2. ASP.NET Core authorization answers the application's access-control question;
3. endpoint governance evaluates the selected endpoint's host-defined governance metadata;
4. the endpoint or application service retains the final protected execution boundary.

`UseAsiBackboneEndpointGovernance()` must run after routing has selected an endpoint. Adding metadata with `MarkGovernancePolicy`, `RequireAcknowledgment`, `RequireCapabilityGrant`, or controller attributes does not by itself configure durable storage, policy, authorization, or execution.

## Applying the Sequence to NCAT

For a NetCoreApplicationTemplate 2.11.2-generated host:

1. Add selected package references and `AddAsiBackboneAspNetCore()` beside the existing registration groups in [`Program.cs`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/v2.11.2/src/ProjectTemplate.Web/Program.cs).
2. Preserve NCAT's existing authentication and authorization configuration. AsiBackbone does not replace either one.
3. Add endpoint-governance middleware to the authoritative [`UseApplicationPipeline`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/v2.11.2/src/ProjectTemplate.Web/Extensions/PipelineExtensions.cs) after routing and the identity/access-control middleware, but before controller and Razor Page endpoint execution.
4. Put host policy composition and application use cases in Web or in an added Application project when the application has enough complexity to justify it.
5. Put database-backed receipt, lifecycle, grant-use, or outbox implementations in Infrastructure. The host owns its `DbContext`, provider, migrations, transaction strategy, and deployment.
6. Keep the protected executor close to the system that owns the credential or side effect. Do not let a policy, acknowledgment handler, receipt writer, or model client become an alternate execution path.
7. Add integration tests that prove authentication failure, authorization failure, every blocked governance outcome, invalid continuation, and invalid or replayed authority all produce zero protected executions.

This is an integration plan, not a statement that NCAT-generated applications currently contain AsiBackbone wiring.

## Persistence and Evidence Choices

Learning samples use in-memory collections and invocation counters because those make the invariant easy to inspect. Replace them only when the production requirement is clear.

| Need | Small/local choice | Durable host choice |
| --- | --- | --- |
| Decision receipt capture | In-memory `IDecisionReceiptSink` for tests or local evaluation | Host-owned durable sink and, where required, `IGovernanceAuditLedgerStore` |
| Lifecycle history | In-memory lifecycle store | EF Core-backed lifecycle store with host-reviewed migrations and retention |
| Capability replay/use count | `InMemoryCapabilityGrantUseStore` for one-process tests | Durable, concurrency-safe host implementation of `ICapabilityGrantUseStore` |
| Provider delivery | Direct/no-op emission during local evaluation | Durable governance outbox, claim/lease semantics, idempotent provider, alerting, and reconciliation |
| Application mutation evidence | NCAT local audit record or completion outbox | NCAT's configured Local, Outbox, or ExternalSink posture plus reconciliation |

AsiBackbone's EF Core package contributes mappings and stores; it does not choose the database provider, connection string, migration policy, or transaction boundary. Review [EF Core Host Ownership and Migrations](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/docs/articles/ef-core-host-ownership-and-migrations.md) and [Durable Audit Outbox Persistence](https://github.com/AsiBackbone/AsiBackbone/blob/v7.0.0/docs/articles/durable-audit-outbox-persistence.md) before treating evidence as durable.

NCAT's `ApplicationMutationAuditReceipt` describes an application data mutation. An AsiBackbone `DecisionReceipt` describes a governance decision. Correlation can connect them, but neither should be renamed or treated as proof of the other.

## Host Responsibilities That Never Move into the Mapping

The ASP.NET Core host remains responsible for:

- authenticating callers and workloads;
- applying ordinary authorization;
- loading current resource, tenant, policy-input, and environmental state;
- deciding which unavailable facts cause denial, deferral, escalation, or another explicit outcome;
- selecting and configuring durable stores and migrations;
- mapping internal outcomes to safe HTTP or messaging responses;
- owning credentials, network access, transactions, retries, and idempotency;
- performing or refusing the protected side effect;
- recording operational execution success or failure separately from the earlier decision;
- threat modeling, deployment controls, incident response, and regulatory assessment.

An architectural responsibility is not necessarily a CLR type. A partial mapping or “no direct equivalent” result is often the accurate answer.

## Baseline Maintenance

When any selected baseline changes:

1. update the three front-matter refs together only after reviewing the released artifacts;
2. verify every linked type and source path against the selected tag;
3. re-check public names, registration methods, middleware behavior, persistence contracts, and security-sensitive defaults;
4. preserve older release-readiness and compatibility pages as historical records;
5. run the API-reference validator, organization-link validator, strict DocFX build, general link validation, sample suite, and formatting verification;
6. describe partial mappings and removed seams explicitly instead of forcing the old matrix onto a new release.

`tools/validate-asibackbone-api-references.cs` enforces the front-matter contract. Each of `learning_ref`, `asibackbone_ref`, and `netcoreapplicationtemplate_ref` must be a release tag or full commit SHA, `asibackbone_ref` must equal the current implementation boundary, and every link on this page to one of the three repositories must use that repository's declared baseline. Advancing the AsiBackbone boundary therefore fails validation until this page has been reviewed and updated with it.

Git tags are the repository's accepted reproducible link form, but a tag can be moved or deleted unless repository rules prevent it. Resolve a selected tag to its full commit SHA when an evidence record requires a reference whose object identity cannot change.

## Continue

- Use the [AsiBackbone 7.0 Compatibility and API Boundary](asibackbone-7-api-boundary.md) before copying package syntax.
- Use [Build a Governed API Operation](../labs/build-a-governed-api-operation.md) to practice the full sequence in a disposable ASP.NET Core application.
- Use [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md) before adding governance lifecycle machinery to an ordinary access-control problem.
- Use the [Working Repository ADR Case Study: NetCoreApplicationTemplate](../aspnetcore/netcoreapplicationtemplate-adr-case-study.md) to understand how NCAT records application-specific architectural choices.
