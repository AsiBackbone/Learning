# Changelog

Notable changes to AsiBackbone Learning are recorded here. This changelog begins with version 0.15.0; earlier history remains available through [GitHub Releases](https://github.com/AsiBackbone/Learning/releases) and the repository commit history.

Learning releases are archival and citation snapshots of educational material. They do not establish runtime compatibility or package support lines.

## [Unreleased]

## [1.1.0] - 2026-09-26

### Changed

- Updated the repository-pinned DocFX tool and reviewed modern-template baseline from 2.78.5 to 2.81.0.
- Replaced chained conditional-return expressions in ordering-sensitive sample policies and validators with explicit guard clauses, preserving first-failure reason-code precedence and adding focused regression coverage (#358).
- Added a current AsiBackbone 7.0 compatibility and API boundary, separated it from the historical Learning 1.0 / AsiBackbone 6.0 contract, pinned historical implementation links to `v6.0.0`, and strengthened validation so versioned compatibility pages declare and honor their implementation ref (#355).
- Aligned current implementation correspondence tables, source links, test links, and sample references with the AsiBackbone 7.0 acknowledgment, decision-receipt, and capability-grant public surface while preserving Learning-owned teaching terminology and architectural boundaries (#354).

### Fixed

- Made GitHub link validation fail closed on persistent HTTP 429 responses, added authenticated retries and deterministic validation of `AsiBackbone/AsiBackbone` source links against a fetched Git tree, and added a nonexistent-path regression fixture (#357).
- Corrected the Governed AI Tool Gateway acknowledgment lifecycle so responses are validated against the exact issued challenge instead of a freshly recreated one. Challenge identifiers now use cryptographically unpredictable nonces; bind actor, tenant, workflow correlation, operation, and recipient; and are retained and atomically consumed in bounded host-owned state. Unknown, future-dated, expired, cross-context, resource-mismatched, and replayed challenges fail closed. The sample documentation now calls out the production requirement for durable or distributed challenge state across multiple instances (#356).

## [1.0.0] - 2026-09-19

### Added

- Learning 1.0 production compatibility, API-boundary, release-readiness, and release-note guidance aligned with AsiBackbone 6.0.
- Optional post-deployment X publication with source-frontmatter selection,
  durable receipt/checkpoint state, canonical-URL reconciliation, protected
  OAuth credentials, deterministic dry runs, and offline contract validation.
- Durable stable-release evidence publishing release notes, a samples SPDX SBOM, a release-evidence manifest, hashes, and provenance attestations.
- Shared repository formatting, security-policy, workflow-validation, support, and maintainer baselines.

### Changed

- Aligned current terminology, navigation, tutorials, diagrams, and sample guidance with the finalized AsiBackbone 6.0 vocabulary and public API surface.
- Aligned all sample tests on `Microsoft.Testing.Platform` and `xunit.v3`, matching the shared AsiBackbone repository posture.

## [0.15.0] - 2026-09-11

### Added

- The **Your Audit Log Is Not Evidence** article covering durable, verifiable decision evidence.
- Guidance and automation for repository-host security controls and protected `main` branch settings.
- CodeQL, OWASP Dependency-Check, OpenSSF Scorecard, actionlint, and zizmor security validation.
- Property-based replay-protection tests using FsCheck.

### Changed

- Centralized sample package versions and committed locked NuGet restore graphs.
- Pinned the .NET SDK and made the existing VSTest runner posture explicit.
- Added repository-wide C# formatting rules and CI formatting enforcement.
- Standardized sample project layouts around `Sample/` and `Tests/` directories.
- Refreshed the roadmap, security guidance, and ongoing maintenance priorities.

### Removed

- Obsolete dependency-check suppressions for packages not used by Learning.

[Unreleased]: https://github.com/AsiBackbone/Learning/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/AsiBackbone/Learning/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/AsiBackbone/Learning/compare/v0.15.0...v1.0.0
[0.15.0]: https://github.com/AsiBackbone/Learning/releases/tag/v0.15.0
