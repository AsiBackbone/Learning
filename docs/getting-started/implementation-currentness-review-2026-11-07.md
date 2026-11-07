---
description: Review AsiBackbone 7.5.3 and NetCoreApplicationTemplate 2.11.8 against the implementation boundaries selected for Learning 1.5.
---

# Implementation Currentness Review: 2026-11-07

**Review date:** 2026-11-07\
**Review outcome:** Retain AsiBackbone `v7.0.0` and NetCoreApplicationTemplate `v2.11.2` as the reviewed Learning 1.5 implementation baselines.\
**Newer releases reviewed:** AsiBackbone [`v7.5.3`](https://github.com/AsiBackbone/AsiBackbone/releases/tag/v7.5.3), commit [`6a58c44f7e919ea94fe56dac3f085b6b768ae7f9`](https://github.com/AsiBackbone/AsiBackbone/commit/6a58c44f7e919ea94fe56dac3f085b6b768ae7f9), and NetCoreApplicationTemplate [`v2.11.8`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/releases/tag/v2.11.8), commit [`833af17f2a8965ce45e0413c43481e0b8e39d13a`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/commit/833af17f2a8965ce45e0413c43481e0b8e39d13a)

Learning releases are archival documentation-and-samples snapshots. A newer compatible implementation release does not silently redefine an already reviewed teaching boundary. This review checks whether the newer releases invalidate Learning's selected pins; it does not claim that `v7.0.0` or `v2.11.2` is the newest recommended dependency for a new production application.

## What Changed Since the Previous Review

AsiBackbone 7.5.2 and 7.5.3 improve dependency maintenance, package discovery metadata, current-facing terminology, a five-minute package evaluator, project routing, citation metadata, visual identity, documentation search, release-document continuity validation, and repository tooling. The 7.5.3 release explicitly preserves stable public signatures, source implementation, durable schemas, canonical wire values, package IDs, namespaces, target frameworks, documented defaults, and runtime decision behavior.

NetCoreApplicationTemplate 2.11.7 and 2.11.8 refresh dependencies and pinned .NET 10 container images, add reviewed package-discovery metadata and a five-minute scaffold evaluation path, adopt the organization visual family and navigation contract, strengthen release-marker automation, and service repository tooling. They preserve the package ID, template identity, short name, supported options, public application surface, and `net10.0` target. Version 2.11.8 published its package and release evidence, but the signed container and automated release-marker follow-up were blocked when the required Trivy gate found HIGH-severity CVE-2026-109518 in the pinned official ASP.NET 10 runtime image. That publication limitation is tracked by the owning repository in [issue #688](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues/688).

## Review Results

| Reviewed surface | Evidence | Decision |
| --- | --- | --- |
| Five foundational tutorials, samples, and labs | They use Learning-owned models and remain explicit about the decision-before-execution, receipt, acknowledgment, capability, and host-ownership boundaries. AsiBackbone 7.5.3 changes discovery and evaluation guidance without changing those runtime concepts. | Retain the `v7.0.0` review metadata. The newer evaluator is additional package-consumer guidance, not a correction to the framework-neutral teaching models. |
| Production-host bridge | The mapped dependency-injection, middleware, persistence, audit, and application-service seams remain present. NCAT 2.11.8 preserves its template identity, supported options, target framework, and public application surface, and still does not generate an AsiBackbone integration. | Retain the version-pinned `v7.0.0` and `v2.11.2` bridge. Direct production adopters to the owning repositories for current package, template, container, and release guidance. |
| Capability, evidence, and authorization guidance | AsiBackbone 7.5.3 preserves the stable 7.x API and runtime behavior while clarifying capability-grant and acknowledgment terminology. | Existing Learning definitions and zero-execution invariants remain accurate. Keep historical pins explicit and use the owning repository for current package syntax. |
| Generated-host and container assumptions | NCAT 2.11.7 and 2.11.8 service dependencies, images, metadata, documentation, and release automation without changing the generated public application surface. The incomplete 2.11.8 container publication is an owning-repository release-state fact, not a change to Learning's pinned host-seam mapping. | Keep the 2.11.2 teaching specimen. Do not describe the 2.11.8 signed container as published, and direct new applications to the current NCAT release guidance and issue #688. |

## Why the Pins Remain Deliberate

Moving either selected baseline would require a separate adoption change: every implementation-dependent page and source link would need review, the bridge would need to describe the newer runtime and generated-host contracts, and executable coverage would need to support any newly taught integration. Learning 1.5 instead archives the educational work completed against the selected baselines while recording the newer releases and the NCAT container-publication limitation transparently.

Repeat the adoption decision when a later Learning archive is prepared, when a package-facing page begins teaching newer released APIs, when a generated-host seam used by Learning changes, or when a learner report shows that the retained pins cause incorrect guidance.

Use the [AsiBackbone 7.0 Compatibility and API Boundary](asibackbone-7-api-boundary.md) for the exact selected package surface and [From Learning Samples to a Production Host](from-learning-samples-to-production-host.md) for the selected host bridge. Use the owning repositories' latest release and migration documentation when building or upgrading a production application.
