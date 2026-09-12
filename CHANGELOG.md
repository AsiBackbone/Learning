# Changelog

Notable changes to AsiBackbone Learning are recorded here. This changelog begins with version 0.15.0; earlier history remains available through [GitHub Releases](https://github.com/AsiBackbone/Learning/releases) and the repository commit history.

Learning releases are archival and citation snapshots of educational material. They do not establish runtime compatibility or package support lines.

## [Unreleased]

### Added

- Durable stable-release evidence publishing release notes, a samples SPDX SBOM, a release-evidence manifest, hashes, and provenance attestations.
- Shared repository formatting, security-policy, workflow-validation, support, and maintainer baselines.

### Changed

- Aligned all sample tests on `Microsoft.Testing.Platform` and `xunit.v3`, matching the shared ASI Backbone repository posture.

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

[Unreleased]: https://github.com/AsiBackbone/Learning/compare/v0.15.0...HEAD
[0.15.0]: https://github.com/AsiBackbone/Learning/releases/tag/v0.15.0
