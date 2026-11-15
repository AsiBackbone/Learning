---
description: Review the dated scope, validation, and publication boundary for the Learning 1.6.0 release candidate.
---

# Learning 1.6.0 Release Readiness

**Review date:** 2026-11-14\
**Review outcome:** Approved locally as a release candidate, subject to required pull-request checks, merge to protected `main`, and tag-bound publication checks.\
**Selected implementation baselines:** AsiBackbone [`v7.0.0`](https://github.com/AsiBackbone/AsiBackbone/releases/tag/v7.0.0) and NetCoreApplicationTemplate [`v2.11.2`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/releases/tag/v2.11.2)

This record prepares Learning 1.6.0 as an archival documentation-and-samples snapshot. The eventual protected-branch merge commit, not the branch state reviewed before merge, becomes the release candidate.

## Release Scope

- **Practitioner articles:** five new problem-first publications cover authenticated-but-untrusted input, correlation IDs versus durable audit evidence, distributed replay protection, ambiguous payment outcomes, and cancellation races in queued work.
- **Implementation references:** complete ASP.NET Core references accompany the payment-recovery and queued-job-cancellation articles with schemas, conditional writes, recovery workers, outbox delivery, escalation, tests, and review checklists.
- **X publication:** feed pages can provide reviewed `x_post` takeaways and `x_question` prompts, with weighted-length validation and durable deferral when required copy is missing.
- **Release evidence:** the release workflow publishes the Sigstore attestation bundle, pins verification to the exact workflow identity and source, verifies fresh public downloads, and rejects altered subjects and wrong identities.
- **Currentness evidence:** a dated review retains the selected AsiBackbone 7.0.0 and NetCoreApplicationTemplate 2.11.2 teaching boundaries after reviewing 7.5.4 and 2.11.10.

## Compatibility Statement

Learning 1.6 remains aligned with the reviewed AsiBackbone 7.0.0 and NetCoreApplicationTemplate 2.11.2 implementation boundaries. The [2026-11-14 implementation-currentness review](implementation-currentness-review-2026-11-14.md) checked AsiBackbone 7.5.4 and NetCoreApplicationTemplate 2.11.10 and retained the selected pins. This release does not change a package, binary, runtime contract, persisted schema, or support line. Earlier Learning release records remain historical snapshots.

Learning owns educational definitions, progressive explanations, tutorials, labs, and framework-neutral samples. AsiBackbone and NetCoreApplicationTemplate remain authoritative for released package and template names, signatures, runtime behavior, configuration, compatibility, migration requirements, security semantics, containers, and deployment guidance.

## Local Validation Record

- [x] Repository diff contains no whitespace errors.
- [x] Implementation-currentness self-tests and the release-mode `--require-current` gate pass.
- [x] AsiBackbone API-reference validator self-tests and current-reference validation pass.
- [x] Organization-link validator self-tests and repository-object validation pass.
- [x] DocFX template, landing-page, site, sitemap, feed, IndexNow, X-publisher, and metadata validation pass without documentation warnings or errors.
- [x] Samples restore in locked mode, build cleanly, pass formatting verification, and pass all tests.
- [x] Release-evidence generation, identity, hashing, and tamper-rejection tests pass.

The 2026-11-14 local run validated 444 instructional files for current AsiBackbone API references, 14 current implementation reviews with no behind or stale current pages, and 308 organization repository-object links. The strict DocFX build processed 158 conceptual pages with zero warnings and zero errors. The generated sitemap contained 155 canonical indexable URLs, the RSS feed contained 40 items, and the sample suite passed all 414 tests with no failures or skips. General external-link validation remains a required hosted pull-request check because the pinned Lychee runner and authenticated network context are supplied by GitHub Actions.

## Release Evidence and Publication Boundary

The [Stable Release Evidence Runbook](https://github.com/AsiBackbone/Learning/blob/main/RELEASE.md) requires publication from a clean commit on protected `main`. The release-triggered workflow must generate, attest, upload, and anonymously verify:

- `learning-samples-1.6.0.spdx.json`;
- `learning-1.6.0-release-notes.md`;
- `release-evidence-manifest.json`;
- `release-evidence.sigstore.json`.

Those assets and their provenance cannot exist before publication. Tag identity, release-note identity, attestations, signatures, and downloads are post-merge gates and are not complete in this preparation pull request.

## Release Approval Checklist

- [x] Learning 1.6 release notes, changelog, citation metadata, and current-release messaging describe the intended scope.
- [x] The implementation-currentness review records why the selected AsiBackbone `v7.0.0` and NetCoreApplicationTemplate `v2.11.2` baselines remain deliberate.
- [x] Required local release validation passes and is recorded above.
- [ ] Required pull-request checks pass on the final release-preparation commit.
- [ ] The approved release candidate is merged to protected `main`.
- [ ] `v1.6.0` and its GitHub Release are created from the approved merge commit using the reviewed release notes.
- [ ] The release-triggered evidence workflow publishes, attests, and verifies all durable assets.

The unchecked controls are deliberately deferred to their pull-request, protected-branch, or release-triggered execution boundaries.
