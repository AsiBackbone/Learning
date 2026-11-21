---
description: Review AsiBackbone 7.5.4 and NetCoreApplicationTemplate 2.11.11 against the implementation boundaries selected for Learning 1.7.
---

# Implementation Currentness Review: 2026-11-21

**Review date:** 2026-11-21\
**Review outcome:** Retain AsiBackbone `v7.0.0` and NetCoreApplicationTemplate `v2.11.2` as the reviewed Learning 1.7 implementation baselines.\
**Newer releases reviewed:** AsiBackbone [`v7.5.4`](https://github.com/AsiBackbone/AsiBackbone/releases/tag/v7.5.4), commit [`6891fe7b83e5a55b916c2e8eea7d20810c0414fe`](https://github.com/AsiBackbone/AsiBackbone/commit/6891fe7b83e5a55b916c2e8eea7d20810c0414fe), and NetCoreApplicationTemplate [`v2.11.11`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/releases/tag/v2.11.11), commit [`8e0030a73b2119e523d0fd99b6e3775107b6f7cd`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/commit/8e0030a73b2119e523d0fd99b6e3775107b6f7cd)

Learning releases are archival documentation-and-samples snapshots. A newer compatible implementation release does not silently redefine an already reviewed teaching boundary. This review checks whether the newer releases invalidate Learning's selected pins; it does not claim that `v7.0.0` or `v2.11.2` is the newest recommended dependency for a new production application.

## What Changed Since the Previous Review

AsiBackbone has published no release since 7.5.4, which the [2026-11-14 review](implementation-currentness-review-2026-11-14.md) already examined. Its conclusions for AsiBackbone carry forward unchanged.

NetCoreApplicationTemplate 2.11.11 is a backward-compatible patch release. It adds NuGet package source mapping to the repository and the generated scaffold, so every current package ID restores only from NuGet.org; refreshes the pinned .NET 10 SDK and ASP.NET Core runtime image digests without changing tags or Dockerfile structure; services EF Core, ASP.NET Core, and Microsoft.Extensions packages from 10.0.14 to 10.0.15, OpenTelemetry to 1.21.0, and other maintenance dependencies; adds scoped, expiring dependency-check suppressions for scanner fingerprint mismatches; and restores the SDK-default informational version format. The package ID, template identities, short name, supported options, public application surface, and `net10.0` target remain unchanged.

## Review Results

| Reviewed surface | Evidence | Decision |
| --- | --- | --- |
| Five foundational tutorials, samples, and labs | They use Learning-owned models and remain explicit about the decision-before-execution, receipt, acknowledgment, capability, and host-ownership boundaries. No AsiBackbone release followed 7.5.4, and NCAT 2.11.11 changes no runtime governance concept. | Retain the `v7.0.0` review metadata. |
| Production-host bridge | The mapped dependency-injection, middleware, persistence, audit, and application-service seams remain present. NCAT 2.11.11 preserves its template identity, supported options, target framework, and public application surface, and still does not generate an AsiBackbone integration. | Retain the version-pinned `v7.0.0` and `v2.11.2` bridge. Direct production adopters to the owning repositories for current package, template, container, and restore guidance. |
| Capability, evidence, and authorization guidance | AsiBackbone 7.5.4 remains the newest release and preserves the stable 7.x API and runtime behavior. | Existing Learning definitions and zero-execution invariants remain accurate. |
| Generated-host, restore, and container assumptions | NCAT 2.11.11 services dependencies and image digests and adds package source mapping to generated scaffolds. Learning's samples already restore through their own source mapping, and the bridge does not teach restore configuration or container images as part of the selected seams. | Keep the 2.11.2 teaching specimen. Applications that add a private feed to a 2.11.11 scaffold must add an explicit mapping for it, as the owning repository's release notes describe. |

## Why the Pins Remain Deliberate

Moving either selected baseline would require a separate adoption change: every implementation-dependent page and source link would need review, the bridge would need to describe the newer runtime and generated-host contracts, and executable coverage would need to support any newly taught integration. Learning 1.7 instead archives the educational work completed against the selected baselines while recording the current released implementations transparently.

Repeat the adoption decision when a later Learning archive is prepared, when a package-facing page begins teaching newer released APIs, when a generated-host seam used by Learning changes, or when a learner report shows that the retained pins cause incorrect guidance.

Use the [AsiBackbone 7.0 Compatibility and API Boundary](asibackbone-7-api-boundary.md) for the exact selected package surface and [From Learning Samples to a Production Host](from-learning-samples-to-production-host.md) for the selected host bridge. Use the owning repositories' latest release and migration documentation when building or upgrading a production application.
