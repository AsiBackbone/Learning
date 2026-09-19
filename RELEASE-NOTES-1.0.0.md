# AsiBackbone Learning 1.0.0

AsiBackbone Learning 1.0.0 is the first production documentation baseline for the AsiBackbone ecosystem. It is the educational companion to the AsiBackbone 6.0.0 implementation release.

## Highlights

- Aligns current terminology with the finalized AsiBackbone 6.0 vocabulary, including decision receipts, acknowledgment, capability grants, host-owned execution, and familiar outbox terminology.
- Adds a Learning 1.0 / AsiBackbone 6.0 compatibility guide for interpreting historical 5.x material.
- Adds an API boundary guide covering current 6.0 names, namespaces, evaluator construction, endpoint markers, renamed types, and removed compatibility members.
- Distinguishes Learning-owned framework-neutral teaching models from exact released package APIs.
- Refreshes Getting Started, navigation, tutorials, diagrams, samples, and cross-repository links around the production baseline.
- Validates all executable samples through locked restore, build, formatting, and 300 invariant tests.
- Adds durable release evidence: a samples SPDX SBOM, exact release notes, a SHA-256 evidence manifest, and GitHub provenance attestations.
- Strengthens documentation, link, workflow-security, dependency, code-scanning, publication, support, and maintainer gates.

## Compatibility

Learning 1.0 documents and teaches the AsiBackbone 6.0 production surface. Earlier Learning releases remain historical educational records and may reference APIs or terminology that were valid in earlier AsiBackbone release lines.

Use the [Learning 1.0 and AsiBackbone 6.0 Compatibility Guide](https://asibackbone.github.io/Learning/getting-started/learning-1-asibackbone-6-compatibility.html) to translate older material. Use the [AsiBackbone 6.0 API Boundary](https://asibackbone.github.io/Learning/getting-started/asibackbone-6-api-boundary.html) for current high-frequency names and examples. The AsiBackbone [5.x-to-6.0 migration guide](https://github.com/AsiBackbone/AsiBackbone/blob/release/6.0.0/docs/articles/upgrade-500-to-600.md) remains authoritative for complete implementation migration details.

## Stable Architectural Boundaries

The 6.0 vocabulary and API refinements do not change Learning's central architecture:

- policy decides before protected execution begins;
- acknowledgment does not silently become authorization;
- capability grants remain narrow, explicit, and independently validated;
- the host retains ownership of side effects and execution;
- decision, acknowledgment, authority, delivery, and execution evidence remain distinct.

## Known Limitations

- Learning is educational documentation, not a package or runtime support line, compliance certification, or security guarantee.
- Executable samples use framework-neutral teaching types and do not reference `AsiBackbone.*` packages.
- Experimental material remains explicitly labeled and is not presented as a standardized protocol or production-ready implementation.
- Exact package signatures, runtime behavior, configuration, compatibility, and security semantics remain authoritative in the AsiBackbone 6.0 implementation repository.

## Release Evidence

The GitHub Release includes an SPDX 2.3 inventory of sample source and locked dependencies, these exact release notes, and a SHA-256 evidence manifest. GitHub provenance attestations bind each evidence asset to the release workflow. See the [Stable Release Evidence Runbook](https://github.com/AsiBackbone/Learning/blob/release/1.0.0/RELEASE.md) for scope and verification commands.
