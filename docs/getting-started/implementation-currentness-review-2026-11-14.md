---
description: Review AsiBackbone 7.5.4 and NetCoreApplicationTemplate 2.11.10 against the implementation boundaries selected for Learning 1.6.
---

# Implementation Currentness Review: 2026-11-14

**Review date:** 2026-11-14\
**Review outcome:** Retain AsiBackbone `v7.0.0` and NetCoreApplicationTemplate `v2.11.2` as the reviewed Learning 1.6 implementation baselines.\
**Newer releases reviewed:** AsiBackbone [`v7.5.4`](https://github.com/AsiBackbone/AsiBackbone/releases/tag/v7.5.4), commit [`6891fe7b83e5a55b916c2e8eea7d20810c0414fe`](https://github.com/AsiBackbone/AsiBackbone/commit/6891fe7b83e5a55b916c2e8eea7d20810c0414fe), and NetCoreApplicationTemplate [`v2.11.10`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/releases/tag/v2.11.10), commit [`406030bf6df3e332a133c87343913aba858952b4`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/commit/406030bf6df3e332a133c87343913aba858952b4)

Learning releases are archival documentation-and-samples snapshots. A newer compatible implementation release does not silently redefine an already reviewed teaching boundary. This review checks whether the newer releases invalidate Learning's selected pins; it does not claim that `v7.0.0` or `v2.11.2` is the newest recommended dependency for a new production application.

## What Changed Since the Previous Review

AsiBackbone 7.5.4 adds Sigstore keyless signatures and pinned-identity verification for GitHub Release build packages, moves compatibility validation to the published 7.5.3 package baseline, refreshes pinned GitHub Actions, and advances sample-host tooling. Its release record explicitly preserves public APIs, source implementation, durable schemas, canonical wire values, package IDs, namespaces, target framework, documented defaults, runtime dependency minimums, runtime decision behavior, and binary assembly identity.

NetCoreApplicationTemplate 2.11.9 refreshes the pinned .NET 10 runtime and SDK images, resolving the CVE-2026-109518 publication blocker recorded for 2.11.8, and constrains automated image updates to the supported .NET major. Version 2.11.10 adds Sigstore signing for the exact template package, digest-bound verification for container signatures and provenance, corrects successful verification exit paths, and updates OpenTelemetry ASP.NET Core and HTTP instrumentation to 1.20.2. The package ID, template identities, short name, supported options, public application surface, and `net10.0` target remain unchanged.

## Review Results

| Reviewed surface | Evidence | Decision |
| --- | --- | --- |
| Five foundational tutorials, samples, and labs | They use Learning-owned models and remain explicit about the decision-before-execution, receipt, acknowledgment, capability, and host-ownership boundaries. AsiBackbone 7.5.4 changes release-artifact assurance and validation without changing those runtime concepts. | Retain the `v7.0.0` review metadata. The new package signatures and validation baseline are consumer and release-process guidance, not corrections to the framework-neutral teaching models. |
| Production-host bridge | The mapped dependency-injection, middleware, persistence, audit, and application-service seams remain present. NCAT 2.11.10 preserves its template identity, supported options, target framework, and public application surface, and still does not generate an AsiBackbone integration. | Retain the version-pinned `v7.0.0` and `v2.11.2` bridge. Direct production adopters to the owning repositories for current package, template, container, signature, and release guidance. |
| Capability, evidence, and authorization guidance | AsiBackbone 7.5.4 preserves the stable 7.x API and runtime behavior. Its Sigstore bundles protect released package bytes and do not change runtime governance-record signing or execution semantics. | Existing Learning definitions and zero-execution invariants remain accurate. Keep historical pins explicit and use the owning repository for current package syntax and artifact verification. |
| Generated-host and container assumptions | NCAT 2.11.9 and 2.11.10 service images, dependencies, signing, and release automation without changing the generated public application surface. The 2.11.8 container blocker is resolved in 2.11.9. | Keep the 2.11.2 teaching specimen. Do not carry the obsolete 2.11.8 publication limitation into the current release, and direct new applications to current NCAT release guidance. |

## Why the Pins Remain Deliberate

Moving either selected baseline would require a separate adoption change: every implementation-dependent page and source link would need review, the bridge would need to describe the newer runtime and generated-host contracts, and executable coverage would need to support any newly taught integration. Learning 1.6 instead archives the educational work completed against the selected baselines while recording the current released implementations transparently.

Repeat the adoption decision when a later Learning archive is prepared, when a package-facing page begins teaching newer released APIs, when a generated-host seam used by Learning changes, or when a learner report shows that the retained pins cause incorrect guidance.

Use the [AsiBackbone 7.0 Compatibility and API Boundary](asibackbone-7-api-boundary.md) for the exact selected package surface and [From Learning Samples to a Production Host](from-learning-samples-to-production-host.md) for the selected host bridge. Use the owning repositories' latest release and migration documentation when building or upgrading a production application.
