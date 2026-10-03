---
description: Review the dated scope, validation, and publication boundary for the Learning 1.2.0 release candidate.
---

# Learning 1.2.0 Release Readiness

**Review date:** 2026-10-03\
**Review outcome:** Approved locally as a release candidate, subject to required pull-request checks, merge to protected `main`, and tag-bound publication checks.\
**Aligned implementation baseline:** AsiBackbone [`v7.0.0`](https://github.com/AsiBackbone/AsiBackbone/releases/tag/v7.0.0), commit [`bd26aaf5a2032febcc399c02de2517cb0ba3a6dc`](https://github.com/AsiBackbone/AsiBackbone/commit/bd26aaf5a2032febcc399c02de2517cb0ba3a6dc)

This record prepares Learning 1.2.0 as an archival documentation-and-samples snapshot. The eventual protected-branch merge commit, not the branch state reviewed before merge, becomes the release candidate.

## Release Scope

- **AI tool execution:** an ordered validation guide demonstrates how host-owned checks prevent rejected proposals from reaching an executor.
- **Decision boundaries:** authorization, approval, acknowledgment, and execution permission are separated and carried through execution-time checks.
- **Policy-as-code selection:** a practitioner guide compares progressively stronger policy boundaries and the obligations introduced by remote evaluation.
- **Agent governance integration:** a worked refund flow shows how application architecture surrounds `Microsoft.AgentGovernance` with authoritative context, workflow state, revalidation, narrow execution authority, failure semantics, and evidence.
- **Documentation integrity:** current-version wording, current-release pointers, landing-page recency, organization links, and transient link behavior receive stronger automated validation.
- **Repository operations:** workflow runner images, Dependabot prefixes, publication-state protection, and repository-local documentation review are made explicit.

## Compatibility Statement

Learning 1.2 remains aligned with the released AsiBackbone 7.0.0 implementation boundary. It does not change a package, binary, runtime contract, persisted schema, or support line. The Learning 1.1.0 and Learning 1.0.0 records remain historical snapshots.

Learning owns educational definitions, progressive explanations, tutorials, labs, and framework-neutral samples. AsiBackbone remains authoritative for released package names, namespaces, signatures, runtime behavior, configuration, compatibility, migration requirements, and security semantics.

## Local Validation Record

- [x] Repository diff contains no whitespace errors.
- [x] AsiBackbone API-reference validator self-tests and current-reference validation pass.
- [x] Organization-link validator self-tests and repository-object validation pass.
- [x] DocFX template, site, sitemap, feed, IndexNow, X-publisher, and metadata validation pass without documentation warnings or errors.
- [x] Samples restore in locked mode, build cleanly, pass formatting verification, and pass all tests.
- [x] Release-evidence generation, identity, hashing, and tamper-rejection tests pass.

All local gates passed on 2026-10-03. The API validator covered 371 instructional files, organization-link validation resolved 280 AsiBackbone source links against fetched Git objects, DocFX built 125 conceptual pages with zero warnings or errors, the sitemap validated 122 canonical pages, and all 312 sample tests passed with zero failures or skips. General external-link validation remains a required hosted pull-request check because the pinned Lychee runner is supplied by GitHub Actions.

## Release Evidence and Publication Boundary

The [Stable Release Evidence Runbook](https://github.com/AsiBackbone/Learning/blob/main/RELEASE.md) requires publication from a clean commit on protected `main`. The release-triggered workflow must generate, attest, upload, and anonymously verify:

- `learning-samples-1.2.0.spdx.json`;
- `learning-1.2.0-release-notes.md`;
- `release-evidence-manifest.json`.

Those assets and their provenance cannot exist before publication. Tag identity, release-note identity, attestations, and downloads are post-merge gates and are not complete in this preparation pull request.

## Release Approval Checklist

- [x] Learning 1.2 release notes, changelog, citation metadata, and current-release messaging describe the intended scope.
- [x] Current implementation references remain aligned with released AsiBackbone `v7.0.0`; earlier Learning records remain historical.
- [ ] Required pull-request checks pass on the final release-preparation commit.
- [ ] The approved release candidate is merged to protected `main`.
- [ ] `v1.2.0` and its GitHub Release are created from the approved merge commit using the reviewed release notes.
- [ ] The release-triggered evidence workflow publishes, attests, and verifies all durable assets.

The unchecked controls are deliberately deferred to their local-validation, pull-request, protected-branch, or release-triggered execution boundaries.
