# AsiBackbone Learning 1.1.0

AsiBackbone Learning 1.1.0 aligns the current educational baseline with the released AsiBackbone 7.0.0 implementation. Current implementation links are pinned to the immutable `v7.0.0` tag, while the Learning 1.0 / AsiBackbone 6.0 material remains an unchanged historical record.

## Highlights

- Documents the AsiBackbone 7.0 namespace, acknowledgment, decision-receipt, capability-grant, serialization, persistence, and security boundaries.
- Pins current implementation and migration references to the published AsiBackbone `v7.0.0` tag and validates that versioned compatibility pages honor their declared tag.
- Corrects the Governed AI Tool Gateway acknowledgment lifecycle so a response is checked against the exact issued challenge, with unpredictable identifiers and actor, tenant, workflow-correlation, operation, and recipient binding.
- Bounds issued and consumed challenge state, rejects unknown, future-dated, expired, mismatched, and replayed responses, and documents the need for durable or distributed production state.
- Makes organization-link validation fail closed after persistent rate limiting, supports authenticated retries, validates paths against the fetched Git tree, and correctly resolves refs containing slashes.
- Replaces ordering-sensitive conditional chains in samples with explicit guard clauses and adds regression tests for first-failure reason-code precedence.
- Updates the repository-pinned DocFX tool and reviewed template baseline to 2.81.0.

## Compatibility

Learning 1.1 documents and teaches the AsiBackbone 7.0.0 production surface. The [AsiBackbone 7.0 Compatibility and API Boundary](docs/getting-started/asibackbone-7-api-boundary.md) summarizes the package-facing changes and links to immutable implementation evidence.

Learning-owned executable samples remain framework-neutral teaching models; they do not reference `AsiBackbone.*` packages and are not package-integration tests. Exact package signatures, runtime behavior, configuration, compatibility, migrations, and security semantics remain authoritative in the [AsiBackbone 7.0.0 release](https://github.com/AsiBackbone/AsiBackbone/releases/tag/v7.0.0).

The [Learning 1.0 / AsiBackbone 6.0 compatibility guide](https://github.com/AsiBackbone/Learning/blob/v1.0.0/docs/getting-started/learning-1-asibackbone-6-compatibility.md), [6.0 API boundary](https://github.com/AsiBackbone/Learning/blob/v1.0.0/docs/getting-started/asibackbone-6-api-boundary.md), and [Learning 1.0.0 release-readiness record](https://github.com/AsiBackbone/Learning/blob/v1.0.0/docs/getting-started/learning-1-release-readiness.md) remain the historical 6.0 snapshot.

## Security and State Boundaries

The Governed AI Tool Gateway sample now demonstrates that acknowledgment is a challenge-response protocol, not a freshly reconstructed comparison. Its in-memory challenge store intentionally models bounded single-process state. Multi-instance production hosts must use a durable or distributed store with atomic consume semantics so expiration and replay rejection hold across restarts and replicas.

The link validator's authenticated retry and Git-tree inspection improve documentation integrity, but do not replace review of the linked implementation, packages, or release provenance.

## Known Limitations

- Learning is educational documentation, not a package or runtime support line, compliance certification, or security guarantee.
- Executable samples use local teaching types and do not prove integration compatibility with released `AsiBackbone.*` packages.
- Experimental material remains explicitly labeled and is not presented as a standardized protocol or production-ready implementation.
- Final tag identity, GitHub Release notes, evidence assets, and provenance are verified only after the approved release candidate is merged and `v1.1.0` is published.

## Release Evidence

The GitHub Release will include an SPDX 2.3 inventory of sample source and locked dependencies, these exact release notes, and a SHA-256 evidence manifest. GitHub provenance attestations bind each evidence asset to the release workflow. See the [Stable Release Evidence Runbook](RELEASE.md) for scope and verification commands.
