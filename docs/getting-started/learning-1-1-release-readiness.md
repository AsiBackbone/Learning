---
description: Review the dated compatibility, validation, and publication boundary for the Learning 1.1.0 release candidate aligned with AsiBackbone 7.0.0.
---

# Learning 1.1.0 Release Readiness

**Review date:** 2026-09-26\
**Review outcome:** Approved as a release candidate, subject to required pull-request checks, merge to protected `main`, and tag-bound publication checks.\
**Aligned implementation baseline:** AsiBackbone [`v7.0.0`](https://github.com/AsiBackbone/AsiBackbone/releases/tag/v7.0.0), commit [`bd26aaf5a2032febcc399c02de2517cb0ba3a6dc`](https://github.com/AsiBackbone/AsiBackbone/commit/bd26aaf5a2032febcc399c02de2517cb0ba3a6dc)

This record prepares Learning 1.1.0 from the pull request that resolves the pre-release portion of [issue #367](https://github.com/AsiBackbone/Learning/issues/367). The eventual protected-branch merge commit, not the branch state reviewed before merge, becomes the release candidate.

## Compatibility Statement

Learning 1.1 documents and teaches the released AsiBackbone 7.0.0 surface. Current source, test, migration, and API references are pinned to `v7.0.0`. The Learning 1.0 / AsiBackbone 6.0 compatibility pages and release records remain immutable history.

Learning owns educational definitions, progressive explanations, tutorials, labs, and framework-neutral samples. AsiBackbone remains authoritative for released package names, namespaces, signatures, runtime behavior, configuration, compatibility, migration requirements, and security semantics.

## Release Scope

- **7.0 alignment:** current implementation correspondence tables and guidance use the released acknowledgment, decision-receipt, and capability-grant vocabulary and immutable `v7.0.0` links.
- **Acknowledgment lifecycle:** the Governed AI Tool Gateway validates the exact issued challenge, uses unpredictable identifiers, binds relevant identity and workflow context, bounds retained state, and rejects expiration and replay failures closed.
- **Link hardening:** organization-link validation retries authenticated requests, fails closed on persistent rate limits, verifies paths against a fetched Git tree, and handles branch or tag names that contain slashes.
- **Decision ordering:** explicit sample guard clauses preserve first-failure reason-code precedence and focused tests protect that behavior.
- **Documentation tooling:** DocFX and the reviewed template baseline are pinned to 2.81.0.

## Validation Record

The following local gates are required on the release-preparation branch:

| Gate | Command | Required result |
| --- | --- | --- |
| Repository diff | `git diff --check` | No whitespace errors. |
| API references | `dotnet run --file tools/validate-asibackbone-api-references.cs` | Current links use `v7.0.0`; historical 6.0 pages remain pinned. |
| Organization links | `dotnet run --file tools/validate-organization-links.cs -- --source-repository ../AsiBackbone --self-test` | Self-tests and all organization links pass against fetched Git objects. |
| Documentation | DocFX template, metadata, sitemap, feed, IndexNow, and X-publisher validation commands from `docs-validation.yml` | All checks pass; DocFX emits no warnings or errors. |
| Samples | Locked restore, clean build, formatting verification, and tests from `samples-validation.yml` | All sample projects build and all tests pass. |
| Release evidence | `./scripts/Test-ReleaseEvidence.ps1` | SBOM, notes, manifest, hash, and tamper-rejection contracts pass. |

All local gates passed on 2026-09-26. The API validator covered 362 instructional files, organization-link validation resolved 280 AsiBackbone source links against fetched Git objects, DocFX built 119 conceptual pages with zero warnings or errors, and all 312 sample tests passed with zero failures or skips.

The pull request must also pass hosted documentation, link, sample, CodeQL, dependency, workflow-security, and supply-chain checks before merge.

## Release Evidence and Publication Boundary

The [Stable Release Evidence Runbook](https://github.com/AsiBackbone/Learning/blob/main/RELEASE.md) requires publication from a clean commit on protected `main`. The release-triggered workflow must generate, attest, upload, and anonymously verify:

- `learning-samples-1.1.0.spdx.json`;
- `learning-1.1.0-release-notes.md`;
- `release-evidence-manifest.json`.

Those assets and their provenance cannot exist before publication. Tag identity, release-note identity, attestations, and downloads are therefore post-merge gates and must not be reported as complete by this preparation pull request.

## Known Limitations

- Learning releases are archival, citable educational snapshots; they do not create package or runtime support lines.
- Executable samples are framework-neutral teaching models, not integration tests for `AsiBackbone.*` packages.
- The in-memory acknowledgment-challenge store demonstrates lifecycle rules only. Multi-instance production systems require durable or distributed state with atomic consume semantics.
- External links and upstream hosting remain independently operated and require continued scheduled validation.
- Experimental pages remain explicitly labeled and are not standardized protocols or production guarantees.

## Release Approval Checklist

- [x] AsiBackbone `v7.0.0` is published and its API, migration, persistence, and security guidance has been reviewed.
- [x] Current implementation links are pinned to `v7.0.0`; Learning 1.0 / AsiBackbone 6.0 records remain historical.
- [x] Learning 1.1 release notes, changelog, citation metadata, and current-release messaging describe the complete scope.
- [ ] Required pull-request checks pass on the final release-preparation commit.
- [ ] The approved release candidate is merged to protected `main`.
- [ ] `v1.1.0` and its GitHub Release are created from the approved merge commit using the reviewed release notes.
- [ ] The release-triggered evidence workflow publishes, attests, and verifies all durable assets.

The unchecked controls are deliberately deferred to their pull-request, protected-branch, or release-triggered execution boundaries.
