---
description: Documentation currency review inventory for Learning issue 507, distinguishing the published archive, live site, implementation reviews and teaching pins.
---

# Documentation Currency Review — 2026-11-18

Tracking issue: [AsiBackbone/Learning #507](https://github.com/AsiBackbone/Learning/issues/507).

## Reviewed reference points

- Published Learning archival release: [v1.6.0](https://github.com/AsiBackbone/Learning/releases/tag/v1.6.0), with matching [`RELEASE-NOTES-1.6.0.md`](https://github.com/AsiBackbone/Learning/blob/v1.6.0/RELEASE-NOTES-1.6.0.md).
- [Currentness review 2026-11-14](implementation-currentness-review-2026-11-14.md): retains AsiBackbone `v7.0.0` and NCAT `v2.11.2` as deliberately selected implementation teaching baselines after reviewing AsiBackbone 7.5.4 and NCAT 2.11.10.
- Live DocFX site: published from `main`, so new articles can appear after an archival tag without changing that immutable archived snapshot.
- Source-of-truth selection: `eng/published-learning-baseline.json` records the archive and latest dated review used by the entry-page guard. Move its pointers only after the relevant release/review has actually been completed.

## Documentation tree scope

The inspected `main` tree (commit `f311ad7`) contained 190 files under `docs/` before this review record was added (Markdown, navigation, site templates, metadata, assets, and other resources). Review treatment by area:

| Area | Classification / decision |
| --- | --- |
| `docs/index.md`, `docs/getting-started/index.md`, `README.md` | Current discovery; show exact archive tag, selected review, and main/live distinction |
| `docs/getting-started/implementation-currentness-review-*.md` | Dated review evidence; retain previous reviews and link the selected review |
| `docs/getting-started/asibackbone-7-api-boundary.md` and `from-learning-samples-to-production-host.md` | Version-pinned reviewed teaching/integration surfaces; **not** latest-package installation recommendations |
| `docs/getting-started/learning-*-release-readiness.md` | Historical release-preparation records; do not rewrite unsigned/unchecked steps after publication |
| `docs/tutorials/`, `docs/labs/`, `docs/samples/` | Conceptual and runnable teaching; sample-local types are not framework binary contracts |
| `docs/architecture/`, `docs/governance/`, `docs/security/`, `docs/aspnetcore/`, `docs/ai-integration/` | Evergreen conceptual guidance: prefer behavior-focused, version-neutral explanations |
| `docs/articles/`, `docs/case-studies/`, `docs/advanced/` | Published practitioner articles, reference designs, dated examples; keep meaningful historical/contextual pins |
| DocFX TOCs, generated API/source references, `docs/templates/`, `docs/public/`, assets | Navigation, source-link and rendering surfaces, checked by existing metadata/DocFX/link tooling |

Version-related claims were cross-checked against `eng/implementation-baseline.json`, the dated currentness decision, release notes, current source entry points, and available validation/publication workflows. This inventory documents reviewed categories and concrete drift decisions; it does **not** assert an independent line-by-line security audit of every article.

## Findings and controls

1. Source current-release pointers were correct in broad terms but lacked a clear exact archival link and distinction from `main` publication. Corrected on entry pages.
2. The bridge's AsiBackbone `v7.0.0` and NCAT `v2.11.2` pins are intentional reproducible teaching snapshots, preserved with prominent non-production-pinning guidance.
3. `scripts/Validate-LearningReleaseMarkers.ps1` checks that the home page, Getting Started, and README link the selected archive tag and dated review and mark the site as published from `main`, asserts that the selected review and release-notes files exist, and guards the bridge pins. It does **not** reject older releases cited inside historical material.
4. Documentation CI runs the validator's self-test, then checks the source and the rendered DocFX output; publishing checks the generated HTML again before upload. After deployment, a separate `verify-live-docs` job polls the live site for up to about ten minutes. It reports a Pages/cache delay (the deployed revision has not appeared yet) separately from a deployed page that lacks the expected links, and its failure does not block the IndexNow or X jobs.
5. Do not treat search-engine snippets as proof of what the deployed site currently serves; direct HTTP fetched page content and the deployment revision are the post-publication evidence.

## Outstanding validation

Run the validator self-test and source check, DocFX build, metadata/currentness validators, and link checks in hosted CI. After deployment, check the live home and Getting Started HTML, and confirm the deployed commit is the one published. Release or currentness-review updates must advance `eng/published-learning-baseline.json` and the corresponding entry-page links together.

Any newly discovered obsolete API or behavior claim requiring source-code changes belongs in a separately scoped issue owned by the appropriate implementation repository.
