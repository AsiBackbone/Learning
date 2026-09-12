# Maintainers

AsiBackbone Learning currently operates under the bootstrap solo-maintainer model defined in [GOVERNANCE.md](GOVERNANCE.md). This file records operational ownership; `GOVERNANCE.md` remains authoritative for project roles, decisions, disagreement handling, and succession.

## Current Maintainer

| Maintainer | Role |
|:---|:---|
| [@cdcavell](https://github.com/cdcavell) | Core Maintainer; educational architecture, samples, releases, security, CI/CD, documentation, and community owner |

## Maintainer Responsibilities

The maintainer is responsible for:

- Reviewing and merging pull requests while preserving learning value, accuracy, clarity, and repository scope.
- Classifying material as canonical, alternative, experimental, or deprecated when needed.
- Maintaining executable samples, tests, package locks, formatting rules, and documentation builds.
- Keeping links to authoritative AsiBackbone and NetCoreApplicationTemplate implementations accurate.
- Reviewing workflow, dependency, security, citation, archival, and release-process changes.
- Coordinating private vulnerability reports and security-related corrections.
- Maintaining release-blocking checks, branch protections, repository rulesets, and CODEOWNERS coverage.
- Publishing the documentation site, GitHub Releases, durable release evidence, and Zenodo archival metadata.
- Keeping governance, support, contribution, security, release, licensing, and maintainer documentation current.

## Release Cadence

Learning does not promise a fixed release calendar. Releases are archival and citation snapshots rather than runtime compatibility or package support lines. The required evidence and validation process is documented in [RELEASE.md](RELEASE.md).

Expected release behavior:

- Patch releases may correct documentation, samples, citations, security guidance, or publication evidence without materially changing the learning scope.
- Minor releases may add tutorials, samples, labs, or substantial compatible expansions to existing material.
- Major releases may reflect a fundamental change to repository scope, organization, licensing, or the meaning of a stable learning snapshot.
- Release timing depends on issue readiness, sample and documentation validation, citation metadata, security checks, and maintainer availability.

## Publishing Ownership

Official release and publication artifacts are produced only through maintainer-controlled workflows. Publishing ownership includes:

- The Learning documentation site.
- GitHub Releases, stable tags, and release notes.
- Durable release evidence, the samples SBOM, and provenance attestations produced by repository workflows.
- Citation metadata and Zenodo archival sequencing.
- Post-publication verification of release assets, hashes, attestations, links, sitemap, feed, and citation metadata.

Learning does not currently publish a NuGet package, compiled binary, container, installer, or other independently distributed executable artifact. Artifact-specific signing remains outside the current scope as documented in [RELEASE.md](RELEASE.md).

## Branch Protection Expectations

The `main` branch is the stable integration branch. Its expected controls are documented in [Repository Host Security Controls](docs/security/repository-host-security-controls.md) and represented by `eng/repository-controls/main-branch-ruleset.json`.

Under the current bootstrap solo-maintainer model:

- Changes reach `main` through pull requests.
- Required documentation, link, sample, CodeQL, workflow-security, and dependency checks pass before ordinary merge.
- Review threads must be resolved, and force pushes and branch deletion are blocked.
- The required approving-review count remains zero while fewer than two active maintainers are available.
- Code Owner approval and last-push approval remain disabled because both require an independent reviewer.
- Required signed commits remain deferred until local and automation identities can satisfy the policy consistently.
- Emergency bypass is repository-specific, pull-request-only, and reserved for documented emergencies rather than routine direct pushes.

## Adding or Removing Maintainers

Maintainer changes follow [GOVERNANCE.md](GOVERNANCE.md). Before expanding maintainership, review repository permissions, independent approval requirements, the emergency bypass actor, publication environments, security advisory access, Zenodo ownership, and CODEOWNERS coverage.

Update this file, `GOVERNANCE.md`, `.github/CODEOWNERS`, and the repository-host security controls together whenever maintainer ownership changes.
