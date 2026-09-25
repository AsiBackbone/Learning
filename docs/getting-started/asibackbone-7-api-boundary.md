---
description: Use the current AsiBackbone 7.0 names and understand the security, serialization, and persistence changes from 6.0.
asibackbone_ref: main
asibackbone_status: current
---

# AsiBackbone 7.0 Compatibility and API Boundary

> **Release status:** AsiBackbone 7.0 is prepared on the implementation repository's `main` branch but is not yet a published package release. Use this page to review or prepare integrations; keep production package references on a released version until 7.0 is published.

Learning teaches governed-execution architecture. AsiBackbone owns exact package names, runtime behavior, wire contracts, and migrations. Learning samples remain framework-neutral teaching models and do not depend on `AsiBackbone.*` packages.

For the released Learning 1.0 / AsiBackbone 6.0 snapshot, use the [historical compatibility guide](learning-1-asibackbone-6-compatibility.md) and [6.0 API boundary](asibackbone-6-api-boundary.md). Their implementation links are pinned to `v6.0.0`.

## Current Vocabulary and Namespaces

AsiBackbone 7.0 completes the vocabulary transition begun in 6.0. The 6.0 compatibility names are removed; no obsolete forwarding aliases are provided.

| 6.0 name | 7.0 name |
| --- | --- |
| `AsiBackbone.Core.Handshakes` | `AsiBackbone.Core.Acknowledgments` |
| `AsiBackbone.AspNetCore.Handshakes` | `AsiBackbone.AspNetCore.Acknowledgments` |
| `AsiBackbone.Core.CapabilityTokens` | `AsiBackbone.Core.CapabilityGrants` |
| `AsiBackbone.Storage.InMemory.CapabilityTokens` | `AsiBackbone.Storage.InMemory.CapabilityGrants` |
| `LiabilityHandshakeRequest` | `AcknowledgmentRequest` |
| `LiabilityHandshakeAcknowledgment` | `AcknowledgmentResponse` |
| `LiabilityHandshakeRiskLevel` | `AcknowledgmentRiskLevel` |
| `RequireLiabilityHandshakeAttribute` | `RequireAcknowledgmentAttribute` |
| `IEndpointLiabilityHandshakeMetadata` | `IEndpointAcknowledgmentMetadata` |
| `CapabilityTokenGrant` | `CapabilityGrant` |
| `CapabilityTokenValidationCategory` | `CapabilityGrantValidationCategory` |
| `HandshakeRequestEntity` | `AcknowledgmentRequestEntity` |
| `HandshakeAcknowledgmentEntity` | `AcknowledgmentResponseEntity` |
| `AuditResidueId` | `DecisionReceiptId` |

The high-frequency current types include [`DecisionReceipt`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Audit/DecisionReceipt.cs), [`AcknowledgmentRequest`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Acknowledgments/AcknowledgmentRequest.cs), [`AcknowledgmentResponse`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Acknowledgments/AcknowledgmentResponse.cs), [`CapabilityGrant`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityGrants/CapabilityGrant.cs), and [`CapabilityGrantValidator`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityGrants/CapabilityGrantValidator.cs).

For execution-boundary validation, use `CapabilityGrantValidationOptions.CreateBoundExecutionBoundary(...)` with explicit `CapabilityGrantBindingExpectations`. The obsolete `CreateExecutionBoundary(...)` overload always fails closed and is retained only for binary compatibility. Metadata-only validation remains deliberately weaker and is not a substitute for execution-boundary checks.

## Security Changes That Require Review

### Acknowledgment responses are actor-bound

An acknowledgment response is accepted only when its actor identity matches the actor to whom the challenge was issued. Hosts must provide an authenticated, known actor with matching `ActorId` and `ActorType`; a response from another tenant, workflow, or actor must not continue the operation.

### DLP enum zero values are fail-closed

`DlpFailureBehavior` and `DlpIntentRiskLevel` now reserve zero for `Unspecified`. Every existing member therefore has a new numeric value. Source that uses enum names recompiles normally, but databases, messages, configuration, or telemetry that persisted numeric enum values require an explicit migration. Do not reinterpret stored 6.x numbers as 7.0 values.

## Serialization and Persistence Changes

Ordinary `System.Text.Json` output now uses `decisionReceiptId` instead of `auditResidueId` (or `DecisionReceiptId` instead of `AuditResidueId` with default naming). Translate stored documents and coordinate producers and consumers; otherwise old JSON may deserialize with a missing receipt identifier.

EF Core hosts must generate and review a migration that renames five columns without dropping data:

| Table | 6.x column | 7.0 column |
| --- | --- | --- |
| `AsiBackboneAuditLedgerRecords` | `AuditResidueId` | `DecisionReceiptId` |
| `AsiBackboneAuditResidueLifecycleEvents` | `AuditResidueId` | `DecisionReceiptId` |
| `AsiBackboneGovernanceOutboxEntries` | `EnvelopeAuditResidueId` | `EnvelopeDecisionReceiptId` |
| `AsiBackboneHandshakeRequestMetadata` | `HandshakeRequestId` | `AcknowledgmentRequestId` |
| `AsiBackboneHandshakeAcknowledgmentMetadata` | `HandshakeAcknowledgmentId` | `AcknowledgmentResponseId` |

Table names remain unchanged. Replace generated drop/add operations with column renames where necessary, then test the migration against production-like data.

## Stable Compatibility Contracts

The public CLR vocabulary changes, but signed and telemetry compatibility strings do not. Canonical artifact tags and payload bytes, OpenTelemetry event and attribute names, diagnostic IDs, EF Core table names, and `AddAsiBackbone*` registration methods keep their 6.x values. Artifacts signed by 6.x continue to verify under 7.0. Do not rename those protocol strings to match the new CLR names.

The authoritative [6.0 to 7.0 upgrade guide](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/upgrade-600-to-700.md) contains the complete rename inventory, constructor changes, migration operations, and stable-contract list.

## Copying Package Syntax

Evaluator construction and endpoint policy markers continue to use the supported builder and `MarkGovernancePolicy(...)` paths. Verify exact signatures in [`GovernancePolicyEvaluatorBuilder`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Evaluation/GovernancePolicyEvaluatorBuilder.cs) and [`EndpointGovernanceRouteBuilderExtensions`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.AspNetCore/Endpoints/EndpointGovernanceRouteBuilderExtensions.cs).

Before publishing package-facing Learning material:

- label Learning-owned code as illustrative;
- label exact package syntax as AsiBackbone 7.0 API;
- use acknowledgment, decision receipt, and capability grant names and namespaces;
- include actor, tenant, workflow correlation, and resource binding where the operation requires them;
- use `CreateBoundExecutionBoundary(...)` with explicit binding expectations at execution boundaries;
- treat DLP numeric enum migration, JSON key changes, and EF Core column renames as deployment work;
- preserve signed, telemetry, and persistence compatibility strings exactly;
- link current implementation behavior to `main` and historical version pages to immutable release tags.

Architecture terms in Learning explain responsibilities and flow; they are not automatically package type names or runtime guarantees. When the two differ, this page directs current readers to the implementation contract while the versioned 6.0 pages preserve the historical record.
