---
description: Review the dated scope, validation, and publication boundary for the Learning 1.7.0 release candidate.
---

# Learning 1.7.0 Release Readiness

**Review date:** 2026-11-21\
**Review outcome:** Approved locally as a release candidate, subject to required pull-request checks, merge to protected `main`, and tag-bound publication checks.\
**Selected implementation baselines:** AsiBackbone [`v7.0.0`](https://github.com/AsiBackbone/AsiBackbone/releases/tag/v7.0.0) and NetCoreApplicationTemplate [`v2.11.2`](https://github.com/AsiBackbone/NetCoreApplicationTemplate/releases/tag/v2.11.2)

This record prepares Learning 1.7.0 as an archival documentation-and-samples snapshot. The eventual protected-branch merge commit, not the branch state reviewed before merge, becomes the release candidate.

## Release Scope

- **Practitioner articles:** three new problem-first publications cover detached `Task.Run` work versus durable acceptance in ASP.NET Core, dependency-injection lifetimes as ownership decisions, and `HttpClient`, handler, and connection lifetimes.
- **Release and currency guidance:** entry pages link the exact published archive tag, distinguish the `main`-published site from the archive, and present the retained implementation pins as teaching snapshots. A release-marker validator checks those links in source, rendered HTML, and on the live site; the 2026-11-18 documentation currency review records the decisions.
- **Samples:** restore uses package source mapping to NuGet.org, central package-management choices are documented, and sample test dependencies are refreshed.
- **Repository controls:** release-evidence and X publishing runners are hardened, GitHub Actions workflows are included in CodeQL analysis, Zenodo DOIs are checked by a dedicated retry-bounded step, and the organization Code of Conduct is adopted.
- **Currentness evidence:** a dated review retains the selected AsiBackbone 7.0.0 and NetCoreApplicationTemplate 2.11.2 teaching boundaries after reviewing 7.5.4 and 2.11.11.

## Compatibility Statement

Learning 1.7 remains aligned with the reviewed AsiBackbone 7.0.0 and NetCoreApplicationTemplate 2.11.2 implementation boundaries. The [2026-11-21 implementation-currentness review](implementation-currentness-review-2026-11-21.md) checked AsiBackbone 7.5.4 and NetCoreApplicationTemplate 2.11.11 and retained the selected pins. This release does not change a package, binary, runtime contract, persisted schema, or support line. Earlier Learning release records remain historical snapshots.

Learning owns educational definitions, progressive explanations, tutorials, labs, and framework-neutral samples. AsiBackbone and NetCoreApplicationTemplate remain authoritative for released package and template names, signatures, runtime behavior, configuration, compatibility, migration requirements, security semantics, containers, and deployment guidance.

## Published-Archive Pointer

`eng/published-learning-baseline.json` selects the 2026-11-21 currentness review in this preparation change, because that review is complete. Its `publishedArchive` pointer, and the archive links on the home page, Getting Started, and README, remain at 1.6.0 because `v1.7.0` does not exist until publication. A follow-up change advances them to 1.7.0 after the GitHub Release is published, as the [2026-11-18 documentation currency review](documentation-currency-review-2026-11-18.md) requires.

## Local Validation Record

- [x] Repository diff contains no whitespace errors.
- [x] Implementation-currentness self-tests and the release-mode `--require-current` gate pass.
- [x] AsiBackbone API-reference validator self-tests and current-reference validation pass.
- [x] Release-marker self-tests and source and rendered-site validation pass.
- [x] Organization-link validator self-tests and repository-object validation pass.
- [x] DocFX template, landing-page, site, sitemap, feed, IndexNow, X-publisher, and metadata validation pass without documentation warnings or errors.
- [x] Samples restore in locked mode, build cleanly, pass formatting verification, and pass all tests.
- [x] Release-evidence generation, identity, hashing, and tamper-rejection tests pass.

The 2026-11-21 local run validated 451 instructional files for current AsiBackbone API references, 14 current implementation reviews with no behind or stale current pages, and 308 organization repository-object links. The release-marker check confirmed archive `v1.6.0` and currentness review 2026-11-21 in source and rendered HTML. The strict DocFX build processed 164 conceptual pages with zero warnings and zero errors. The generated sitemap contained 161 canonical indexable URLs, the RSS feed contained 43 items, and the sample suite passed all 414 tests with no failures or skips. General external-link validation remains a required hosted pull-request check because the pinned Lychee runner and authenticated network context are supplied by GitHub Actions.

## Release Evidence and Publication Boundary

The [Stable Release Evidence Runbook](https://github.com/AsiBackbone/Learning/blob/main/RELEASE.md) requires publication from a clean commit on protected `main`. The release-triggered workflow must generate, attest, upload, and anonymously verify:

- `learning-samples-1.7.0.spdx.json`;
- `learning-1.7.0-release-notes.md`;
- `release-evidence-manifest.json`;
- `release-evidence.sigstore.json`.

Those assets and their provenance cannot exist before publication. Tag identity, release-note identity, attestations, signatures, and downloads are post-merge gates and are not complete in this preparation pull request.

## Release Approval Checklist

- [x] Learning 1.7 release notes, changelog, citation metadata, and current-release messaging describe the intended scope.
- [x] The implementation-currentness review records why the selected AsiBackbone `v7.0.0` and NetCoreApplicationTemplate `v2.11.2` baselines remain deliberate.
- [x] Required local release validation passes and is recorded above.
- [ ] Required pull-request checks pass on the final release-preparation commit.
- [ ] The approved release candidate is merged to protected `main`.
- [ ] `v1.7.0` and its GitHub Release are created from the approved merge commit using the reviewed release notes.
- [ ] The release-triggered evidence workflow publishes, attests, and verifies all durable assets.
- [ ] The published-archive pointer and entry-page archive links advance to 1.7.0 after publication.

The unchecked controls are deliberately deferred to their pull-request, protected-branch, or release-triggered execution boundaries.
