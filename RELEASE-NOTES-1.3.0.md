# AsiBackbone Learning 1.3.0

AsiBackbone Learning 1.3.0 is an archival and citation snapshot of the educational material published since 1.2.0. It adds four practitioner-focused articles about delayed execution authority, workflow ownership, multi-tenant worker isolation, and authorization behavior during policy outages. The implementation correspondence baseline remains the released AsiBackbone 7.0.0 surface.

## Highlights

- Adds **How Short-Lived Execution Authority Differs from User Authorization**, following a scheduled payout to separate the original actor and approval from the narrow authority a later executor needs.
- Adds **When Should a Workflow Engine Own the Decision?**, distinguishing workflow state from current authorization and showing when policy should remain independently evaluated at the protected boundary.
- Adds **Can One Worker Safely Execute Delayed Operations for Many Tenants?**, separating tenant-substitution checks, workload authentication, and compromise containment for shared workers.
- Adds **Should Authorization Fail Open, Fail Closed, or Defer?**, distinguishing explicit denial from an unavailable decision and designing rejection, bounded deferral, local evaluation, escalation, and reconciliation per operation.

## Publication and Workflow Hardening

- Makes the standalone-article publication contract explicit about search and social-preview descriptions, quoted publication dates, and optional curated X hashtags.
- Stops the documentation workflow from inheriting every caller secret into the reusable X publisher and passes only the four required OAuth credentials explicitly.
- Keeps the X publisher's protected environment as the credential boundary while preserving offline self-tests and post-deployment publication behavior.

## Compatibility

Learning releases are citable documentation and sample snapshots, not runtime or package support lines. Learning 1.3 continues to teach the reviewed AsiBackbone 7.0.0 API boundary. The four new articles are framework-neutral, and the executable samples do not reference `AsiBackbone.*` packages or claim integration coverage for the newer 7.1 analyzer and deprecation surface.

The Learning 1.2.0, Learning 1.1.0, and Learning 1.0.0 records remain historical snapshots of their reviewed boundaries. Exact package signatures, runtime behavior, configuration, migrations, and security semantics remain authoritative in the corresponding AsiBackbone release documentation.

## Known Limitations

- Learning is educational documentation, not a compliance certification, legal standard, security guarantee, or production architecture approval.
- Executable samples are teaching models and do not prove integration compatibility with released `AsiBackbone.*` packages.
- External links and upstream products remain independently operated and can change after this snapshot.
- Final tag identity, GitHub Release notes, evidence assets, and provenance are verified only after the approved release candidate is merged and `v1.3.0` is published.

## Release Evidence

The GitHub Release will include an SPDX 2.3 inventory of sample source and locked dependencies, these exact release notes, and a SHA-256 evidence manifest. GitHub provenance attestations bind each evidence asset to the release workflow. See the [Stable Release Evidence Runbook](RELEASE.md) for scope and verification commands.
