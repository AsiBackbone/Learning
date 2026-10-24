# AsiBackbone Learning 1.4.0

AsiBackbone Learning 1.4.0 is an archival and citation snapshot of the educational material published since 1.3.0. It expands the curriculum with runnable comparison and failure-injection samples, production-oriented case studies, an ASP.NET Core retrofit playbook, and clearer implementation-currentness evidence. The selected implementation correspondence baselines remain AsiBackbone 7.0.0 and NetCoreApplicationTemplate 2.11.2 after a dated review of the newer 7.5.1 and 2.11.6 releases.

## Highlights

- Adds a runnable authorization-to-governance comparison that contrasts endpoint authorization, resource authorization, and governed execution without treating them as substitutes.
- Adds a governed failure-injection trace sample that makes evaluation, evidence, execution, and recovery boundaries observable under representative failures.
- Adds reference-architecture case studies for emergency production access, delayed financial payout, shared multi-tenant workers, and cross-system delegated execution.
- Adds a phased playbook for retrofitting governed execution into an existing ASP.NET Core application.
- Publishes practitioner articles separating decision truth from execution outcome, authority replay from idempotency, and isolated LTS integration from rollback.
- Connects Learning's teaching models to version-pinned AsiBackbone and NetCoreApplicationTemplate host seams and to the ReferenceApplication integration walkthrough.

## Validation and Publication Hardening

- Adds implementation-currentness metadata and a release gate that detects stale implementation reviews without rewriting framework-neutral teaching models.
- Aligns the documentation landing page and navigation with the organization standard and closes reduced-motion and focus-contrast gaps.
- Strengthens repository governance and required security validation, refreshes sample dependencies and workflow actions, and preserves strict documentation metadata, feed, sitemap, link, and release-evidence checks.

## Compatibility

Learning releases are citable documentation and sample snapshots, not runtime or package support lines. Learning 1.4 retains the reviewed AsiBackbone 7.0.0 and NetCoreApplicationTemplate 2.11.2 implementation boundaries. The dated currentness review confirms that the foundational material and production-host bridge remain accurate within those explicit pins after reviewing AsiBackbone 7.5.1 and NetCoreApplicationTemplate 2.11.6.

The newer implementation releases add important backward-compatible and opt-in behavior. Production consumers should use the owning repositories' current release, migration, security, and deployment guidance rather than treating Learning's archival pins as upgrade recommendations.

## Known Limitations

- Learning is educational documentation, not a compliance certification, legal standard, security guarantee, or production architecture approval.
- Executable samples are teaching models and do not prove integration compatibility with released `AsiBackbone.*` packages.
- External links and upstream products remain independently operated and can change after this snapshot.
- Final tag identity, GitHub Release notes, evidence assets, and provenance are verified only after the approved release candidate is merged and `v1.4.0` is published.

## Release Evidence

The GitHub Release will include an SPDX 2.3 inventory of sample source and locked dependencies, these exact release notes, and a SHA-256 evidence manifest. GitHub provenance attestations bind each evidence asset to the release workflow. See the [Stable Release Evidence Runbook](RELEASE.md) for scope and verification commands.
