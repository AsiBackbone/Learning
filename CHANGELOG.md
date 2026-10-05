# Changelog

Notable changes to AsiBackbone Learning are recorded here. This changelog begins with version 0.15.0; earlier history remains available through [GitHub Releases](https://github.com/AsiBackbone/Learning/releases) and the repository commit history.

Learning releases are archival and citation snapshots of educational material. They do not establish runtime compatibility or package support lines.

## [Unreleased]

### Added

- Published [When Should a Workflow Engine Own the Decision?](docs/articles/2026/when-workflow-engine-should-own-decision.md), a practitioner guide that follows one production deployment to separate workflow state, human approval, policy decisions, and execution permission, covering why reaching `Approved` does not prove current permission, how retries, timers, and compensation interact with policy freshness, evaluating independently owned policy at the protected execution boundary, recording the decision and policy version at execution, fail-closed behavior when policy evaluation is unavailable, and when a cohesive same-boundary workflow can own the decision without extra infrastructure (#394).
- Published [How Short-Lived Execution Authority Differs from User Authorization](docs/articles/2026/short-lived-execution-authority-vs-user-authorization.md), a practitioner guide that follows one scheduled vendor payout to separate the actor, the accepted operation, the later executor, the delegated grant, and the protected host, covering why forwarding a user's token or claims creates excessive or stale authority, the bindings that make delayed authority narrow, execution-time freshness checks, one-time use versus idempotency, and when immediate execution or ordinary workload identity is enough (#395).

## [1.2.0] - 2026-10-03

### Added

- Published [How Application Architecture Complements Microsoft.AgentGovernance](docs/articles/2026/application-architecture-complements-microsoft-agent-governance.md), a practitioner guide that follows one AI-proposed refund through a host-owned control flow around `Microsoft.AgentGovernance`, covering authoritative context, verdict translation without Boolean collapse, workflow state, execution-time revalidation, proportional execution authority, failure semantics, evidence, and tests proving that non-allowed paths never reach the executor (#391).
- Published [Policy as Code in ASP.NET Core Without Overengineering](docs/articles/2026/policy-as-code-aspnet-core-without-overengineering.md), a selection guide that follows one ASP.NET Core refund endpoint through ordinary code, framework authorization, an in-process policy component, an embedded engine, and a remote decision service, with the pressures that justify each boundary and the obligations remote evaluation adds (#377).
- Published [Authorization vs. Approval vs. Acknowledgment: Which Decision Do You Actually Have?](docs/articles/2026/authorization-vs-approval-vs-acknowledgment.md), a practitioner guide that separates authorization, approval, acknowledgment, and execution permission, with an execution-time check and tests showing that none of them silently becomes permission to run a delayed operation (#375).
- Published [What Should an AI Tool Gateway Validate Before Execution?](docs/articles/2026/validate-ai-tool-call-before-execution.md), an ordered host-side acceptance checklist for AI tool calls with code and tests that prove rejected proposals never reach the executor (#371).

### Changed

- Pinned every workflow job to `ubuntu-24.04` instead of `ubuntu-latest`, which GitHub migrates to Ubuntu 26 beginning October 19, 2026. Moving to a newer runner image is now an explicit, reviewable change.
- Dependabot commit messages use the prefix `chore` with the dependency scope, producing `chore(deps): ...` instead of `chore(deps)(deps): ...`.

### Fixed

- Pull request and push link validation now accepts HTTP 503, so a transient outage on a third-party host no longer fails an unrelated change. The weekly scheduled run keeps the strict accept list and still reports links that stay unavailable.
- Refreshed the landing page's stale copy. **Recently added** now lists the three newest published articles under their current titles, and the Executable Samples card no longer uses a hard-coded sample count.
- `tools/validate-doc-metadata.cs` now fails the documentation build when **Recently added** does not name the newest published articles, newest first, with matching titles, or when it contains any list item that is not a plain article link.
- Markdown link validation no longer fetches `AsiBackbone/AsiBackbone` blob and tree URLs live. `tools/validate-organization-links.cs` already resolves each one against a fetched clone, and the live check only added transient GitHub 503 failures.
- [Constraint Composition and Policy Precedence](docs/governance/constraint-composition-and-policy-precedence.md) no longer describes its fail-closed defaults as "the current `AsiBackbone` 3.x default". It now names the `GovernancePolicyOptions.DenyWhenNoConstraints` and `TreatConstraintExceptionAsDenial` options, which still default to `true` in 7.x.
- `tools/validate-asibackbone-api-references.cs` now fails the documentation build when a Markdown page describes an AsiBackbone major version other than the current implementation ref as current, for example "the current `AsiBackbone` 3.x default". Release notes, the changelog, version-transition pages, and pages marked `asibackbone_status: historical` are exempt.
- The AsiBackbone API boundary pages and the landing page no longer call release tags immutable. A Git tag can be moved or deleted unless repository rules prevent it, so the 7.0 boundary page now says to resolve the tag to its commit SHA when a reference must not change. Published release records are unchanged.

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

[Unreleased]: https://github.com/AsiBackbone/Learning/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/AsiBackbone/Learning/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/AsiBackbone/Learning/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/AsiBackbone/Learning/compare/v0.15.0...v1.0.0
[0.15.0]: https://github.com/AsiBackbone/Learning/releases/tag/v0.15.0
