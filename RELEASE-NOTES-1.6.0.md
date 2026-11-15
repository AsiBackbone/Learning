# AsiBackbone Learning 1.6.0

AsiBackbone Learning 1.6.0 is an archival and citation snapshot of the educational material published since 1.5.0. It adds five problem-first practitioner articles, two complete ASP.NET Core implementation references, author-written X announcement copy, and durable Sigstore verification material for release evidence.

## Highlights

- Publishes **Passing Authentication Does Not Make Input Trustworthy**, separating authenticated identity from malformed, stale, unauthorized, cross-tenant, and adversarial request data.
- Publishes **A Correlation ID Is Not a Distributed Audit Trail**, distinguishing grouping, identity, causation, durable evidence, and external linkage across distributed work.
- Publishes **An In-Memory Grant Store Is Not Distributed Replay Protection**, defining the shared, atomic, durable admission contract required for one-time grants.
- Publishes **A Payment API Timeout Leaves the Outcome Unknown** and a companion ASP.NET Core implementation reference for durable attempts, notifications, reconciliation, outbox delivery, and escalation.
- Publishes **Canceling a Queued Job Can Race With Execution** and a companion ASP.NET Core implementation reference for atomic claims, cooperative stop requests, fenced leases, outcome recording, and recovery.

## Publication and Release Evidence

- Feed-enabled pages can provide an author-written `x_post` takeaway and optional `x_question`; validation uses X's weighted length rules and never truncates reviewed copy.
- Missing `x_post` copy creates a durable publication deferral rather than silently substituting generic text or blocking later articles.
- Release evidence now publishes the GitHub provenance attestation bundle as `release-evidence.sigstore.json` and verifies every subject against the exact repository, workflow, tag, commit, OIDC issuer, and GitHub-hosted runner identity.
- Consumer verification rejects altered files, missing or unexpected assets, incomplete bundles, and untrusted signer identities.
- Pinned GitHub Actions and repository validation dependencies are refreshed.

## Compatibility

Learning releases are citable documentation and sample snapshots, not runtime or package support lines. Learning 1.6 retains the reviewed AsiBackbone 7.0.0 and NetCoreApplicationTemplate 2.11.2 implementation boundaries. The 2026-11-14 currentness review confirms that the foundational material and production-host bridge remain accurate within those explicit pins after reviewing AsiBackbone 7.5.4 and NetCoreApplicationTemplate 2.11.10.

The newer implementation releases strengthen signed release-artifact verification, package compatibility validation, container security, and dependencies without invalidating Learning's selected teaching boundary. Production consumers should follow the owning repositories' current release and migration guidance rather than infer package or template compatibility from this Learning snapshot.

## Known Limitations

- Learning is educational documentation, not a compliance certification, legal standard, security guarantee, or production architecture approval.
- Executable samples and implementation references are teaching artifacts and do not prove integration compatibility with released `AsiBackbone.*` packages.
- External links and upstream products remain independently operated and can change after this snapshot.
- Final tag identity, GitHub Release notes, evidence assets, Sigstore bundle, and provenance are verified only after the approved release candidate is merged and `v1.6.0` is published.

## Release Evidence

The GitHub Release will include an SPDX 2.3 inventory of sample source and locked dependencies, these exact release notes, a SHA-256 evidence manifest, and the Sigstore bundle for GitHub provenance attestations over those three files. See the [Stable Release Evidence Runbook](RELEASE.md) for scope, trust boundaries, and verification commands.
