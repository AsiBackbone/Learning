# AsiBackbone Learning 1.5.0

AsiBackbone Learning 1.5.0 is an archival and citation snapshot of the educational material published since 1.4.0. It adds five problem-first practitioner articles about where authorization belongs, how authority survives delayed work and retries, how AI-assisted discovery differs from authorization to act, and why eligibility can change after a security check passes. It also gives first-time readers a five-minute preview and clearer routes for evaluating the wider AsiBackbone ecosystem.

## Highlights

- Publishes **Where Should Authorization Live in a .NET Application?**, a placement guide spanning endpoint policies, resource checks, application services, domain models, data boundaries, policy engines, and later executors.
- Publishes **A Background Job Should Not Inherit Everything the User Could Do**, separating requester identity, worker identity, accepted work, delivery, current authorization, and execution evidence.
- Publishes **Your Retry Logic May Be Replaying an Authorization Decision**, distinguishing retry eligibility from the continued validity of an earlier authorization decision.
- Publishes **Discovery Is Not Authorization: Using GitHub Issues to Govern AI-Assisted Development**, preserving issue scope and human authority when an agent discovers adjacent work.
- Publishes **Your Security Check Passed. Then the World Changed.**, covering time-of-check to time-of-use gaps, selective invalidation, revalidation, expiry, and conditional claims.
- Adds a five-minute Decision Before Execution preview while preserving the fuller ten-minute quick start.

## Discovery, Feedback, and Publication Surface

- Adds ReferenceApplication to ecosystem routing as the stable runnable integration specimen built from NetCoreApplicationTemplate and AsiBackbone.
- Routes first-time questions, tutorial proposals, and architecture-fit critiques to the relevant organization Discussion categories while preserving issues for concrete repository work.
- Adopts the open-book glyph from the AsiBackbone visual family across the documentation logo, favicon, feed image, and social cards.
- Aligns documentation search and navigation with the organization contract and adds OpenSSF Scorecard visibility.
- Refreshes sample dependencies, locked dependency graphs, validation tooling, and pinned workflow actions.

## Compatibility

Learning releases are citable documentation and sample snapshots, not runtime or package support lines. Learning 1.5 retains the reviewed AsiBackbone 7.0.0 and NetCoreApplicationTemplate 2.11.2 implementation boundaries. The 2026-11-07 currentness review confirms that the foundational material and production-host bridge remain accurate within those explicit pins after reviewing AsiBackbone 7.5.3 and NetCoreApplicationTemplate 2.11.8.

The newer implementation releases improve package and template discovery, evaluation guidance, terminology, documentation, visual identity, dependencies, containers, and release automation without invalidating Learning's selected teaching boundary. NetCoreApplicationTemplate 2.11.8 published its package and release evidence, but its signed container was blocked by a HIGH-severity vulnerability in the pinned official ASP.NET 10 runtime image; production consumers should follow the owning repository's issue and release guidance rather than infer container completion from this Learning snapshot.

## Known Limitations

- Learning is educational documentation, not a compliance certification, legal standard, security guarantee, or production architecture approval.
- Executable samples are teaching models and do not prove integration compatibility with released `AsiBackbone.*` packages.
- External links and upstream products remain independently operated and can change after this snapshot.
- Final tag identity, GitHub Release notes, evidence assets, and provenance are verified only after the approved release candidate is merged and `v1.5.0` is published.

## Release Evidence

The GitHub Release will include an SPDX 2.3 inventory of sample source and locked dependencies, these exact release notes, and a SHA-256 evidence manifest. GitHub provenance attestations bind each evidence asset to the release workflow. See the [Stable Release Evidence Runbook](RELEASE.md) for scope and verification commands.
