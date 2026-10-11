---
description: Review AsiBackbone 7.1.0 against the Learning 1.3 implementation baseline and record why Learning retains its reviewed 7.0.0 boundary.
---

# AsiBackbone 7.1.0 Currentness Review

**Review date:** 2026-10-11\
**Review outcome:** Retain AsiBackbone `v7.0.0` as the reviewed Learning 1.3 implementation baseline.\
**Newer implementation release reviewed:** AsiBackbone [`v7.1.0`](https://github.com/AsiBackbone/AsiBackbone/releases/tag/v7.1.0), commit [`2044c430d0f59f128028c96de5ec3f3238e40ca3`](https://github.com/AsiBackbone/AsiBackbone/commit/2044c430d0f59f128028c96de5ec3f3238e40ca3)

Learning 1.3 remains pinned to the implementation snapshot it reviewed and archived. AsiBackbone 7.1.0 is the owning repository's newer stable release, but its changes do not invalidate the five foundational teaching paths, the production-host bridge, or the 7.0 compatibility boundary. A future Learning archival release may adopt a newer implementation baseline after repeating the full baseline review; the existence of a newer compatible package alone does not redefine an already reviewed Learning snapshot.

## What Changed in AsiBackbone 7.1.0

AsiBackbone 7.1.0 is a backward-compatible minor release. The currentness-sensitive additions are:

- analyzer `ASIB004`, which warns when the package `SigningRequest`, `SignatureVerificationRequest`, or `ManagedKeySignRequest` is created without an explicit `SignatureInput`;
- deprecation warning `ASIB902` on `GovernanceSignatureInput.CreateLegacy(...)`, with `CreateV1(...)` as the path for new signing and verification;
- deprecation warning `ASIB903` on `GovernanceOutboxDrainWorkerOptions.RetryClock`, with a registered `TimeProvider` as the replacement; and
- dedicated migration and analyzer guidance for those warnings.

These are compiler and IDE warnings, not runtime breaking changes. The affected APIs remain callable in the 7.x line. The release does not change package IDs, namespaces, persisted schemas, canonical wire values, service-registration contracts, or documented runtime defaults. See the version-pinned [7.1.0 release notes](https://github.com/AsiBackbone/AsiBackbone/blob/v7.1.0/docs/articles/release-notes-710.md).

## Review Results

| Reviewed surface | Evidence | Decision |
| --- | --- | --- |
| Five foundational tutorial, sample, and lab paths | The paths use Learning-owned types and do not construct any of the three package request types covered by `ASIB004`. They do not call `GovernanceSignatureInput.CreateLegacy(...)` or configure `GovernanceOutboxDrainWorkerOptions.RetryClock`. | No baseline or sample-code change is required. Existing package links remain pinned to the reviewed `v7.0.0` baseline. |
| Production-host bridge | The mapped decision, acknowledgment, capability, persistence, middleware, and host-ownership surfaces remain available with the same package names and responsibilities in 7.1.0. | Keep `asibackbone_ref: v7.0.0`. The bridge continues to describe the Learning 1.3 review boundary rather than the newest compatible package. |
| Current API boundary | The 7.0 vocabulary, serialization, persistence, actor-binding, DLP, and capability-validation guidance remains accurate for the selected baseline. The 7.1 additions concern safer construction and deprecation guidance rather than replacements for those mappings. | Retain the 7.0 boundary and add a dated pointer to this review so “reviewed Learning baseline” is not confused with “newest AsiBackbone release.” |
| API-reference validator | `CurrentImplementationRef`, bridge fixtures, and pinned-link expectations consistently enforce `v7.0.0`. The validator already accepts full 40-character commit links for evidence such as this review. | Keep the constants and fixtures unchanged; changing them would falsely claim that the complete Learning 1.3 baseline was re-versioned to 7.1.0. |
| Signing and verification teaching | One provider-neutral Learning model defines a local `SigningRequest` without a package `SignatureInput`. The type teaches provider separation and does not construct or represent `AsiBackbone.Core.Signing.SigningRequest`. | Clarify the local type and direct package users to version 1 signature input. Do not mechanically copy the package request shape into the framework-neutral model. |
| Outbox and hosted-drain teaching | Learning discusses outbox responsibility and injected clocks generically, but no Learning page configures `RetryClock` or recommends a custom hosted-drain delegate. | No correction is required. Package users adopting 7.1.0 should register `TimeProvider` and follow `ASIB903`; generic Learning `TimeProvider` examples remain valid. |
| Direct implementation links and version claims | Current Learning implementation links remain pinned to `v7.0.0`; historical release/readiness records retain their original version meaning. Evidence links on this page use the exact 7.1.0 commit. | Preserve the existing pinned links and immutable historical records. |

## Signing-Input Boundary

The package analyzer applies to these released package request types, including the exact [`SigningRequest`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.1.0/src/AsiBackbone.Core/Signing/SigningRequest.cs) shape. New package signing and verification code should supply the version 1 bytes created by [`GovernanceSignatureInput.CreateV1(...)`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.1.0/src/AsiBackbone.Core/Signing/GovernanceSignatureInput.cs). Intentional verification of retained pre-6.0 artifacts remains a separate supported opt-in; `ASIB902` does not mean historical evidence should become unverifiable.

Learning's provider-neutral model instead makes a smaller architectural point: domain code can depend on a signing boundary without loading keys or binding itself to one cryptographic provider. Its local `SigningRequest` name describes that teaching role. It is not package-integration syntax, and adding a property with the same name would not by itself reproduce the package's canonicalization or verification contract.

## Outbox Clock Boundary

The 7.1.0 deprecation applies specifically to the package's hosted drain option. A custom [`RetryClock`](https://github.com/AsiBackbone/AsiBackbone/blob/v7.1.0/src/AsiBackbone.AspNetCore/Outbox/GovernanceOutboxDrainWorkerOptions.cs) can make the hosted drain cycle disagree with other services that use the registered `TimeProvider`. Learning does not recommend that package option. Its examples that inject `TimeProvider` into application or teaching code already follow the shared-clock direction and are not uses of the deprecated delegate.

## When to Revisit the Baseline

Repeat the adoption decision when one of these conditions is met:

- the next meaningful Learning archival release is prepared;
- a newer implementation release changes an API, default, serialized contract, or runtime behavior used by Learning's package-facing material;
- a foundational sample or production-host bridge begins constructing package signing requests or configuring the hosted outbox drain; or
- a validator or learner report demonstrates that the retained 7.0 boundary causes incorrect package guidance.

Until then, use the [AsiBackbone 7.0 Compatibility and API Boundary](asibackbone-7-api-boundary.md) for the exact Learning 1.3 implementation baseline, and use AsiBackbone's 7.1.0 release and migration documentation when upgrading a package-consuming application.
