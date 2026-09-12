# Stable Release Evidence Runbook

Learning releases are archival and citation snapshots of educational material. They are not package or runtime support lines. This runbook defines the durable evidence required for every stable `vMAJOR.MINOR.PATCH` GitHub Release.

## Release gate

Publish a stable release only from a clean commit on protected `main`. Before publishing the GitHub Release, confirm the normal documentation and sample validation checks are green and that the release tag resolves to the intended commit.

The `Publish Stable Release Evidence` workflow runs again when the GitHub Release is published. It checks out the exact tag and fails unless all of these operations succeed:

```powershell
dotnet restore samples/Samples.slnx --locked-mode
dotnet build samples/Samples.slnx --no-restore
dotnet format samples/Samples.slnx --verify-no-changes --no-restore --verbosity minimal
dotnet test samples/Samples.slnx --no-build
dotnet run --file tools/validate-docfx-template-baseline.cs
dotnet tool restore
dotnet tool run docfx docs/docfx.json --warningsAsErrors
./scripts/Test-LearningReleaseEvidence.ps1
```

The workflow also fails if the stable tag is invalid, the tag and checked-out commit disagree, the published release or its notes cannot be resolved, evidence generation fails, provenance cannot be generated and verified, upload fails, an expected asset is absent, or an asset is not anonymously downloadable.

## Durable assets

Every stable release must expose these three versioned or self-describing assets directly on the GitHub Release:

| Asset | Scope |
| --- | --- |
| `learning-samples-MAJOR.MINOR.PATCH.spdx.json` | SPDX 2.3 inventory of every tracked file under `samples/` and every distinct NuGet package/version resolved by the committed `packages.lock.json` files. |
| `learning-MAJOR.MINOR.PATCH-release-notes.md` | Exact Markdown body of the published GitHub Release. |
| `release-evidence-manifest.json` | Repository, tag, source commit, generation time, source/lock/dependency counts, component asset SHA-256 hashes, tool versions, commands, and trust-boundary statements. |

GitHub provenance attestations are generated for all three files. GitHub Actions artifacts are retained briefly for diagnostics and workflow hand-off only; they are not the durable evidence record.

The SBOM describes source and resolved dependency identity. It is not a vulnerability report, does not prove the absence of vulnerabilities, and does not replace GitHub code-scanning or dependency-security surfaces.

## Verification

Download all three assets from the release page, then compare each component asset with the corresponding SHA-256 entry in `release-evidence-manifest.json`:

```powershell
(Get-FileHash -Algorithm SHA256 ./learning-samples-MAJOR.MINOR.PATCH.spdx.json).Hash.ToLowerInvariant()
(Get-FileHash -Algorithm SHA256 ./learning-MAJOR.MINOR.PATCH-release-notes.md).Hash.ToLowerInvariant()
```

For releases produced by the current workflow, verify provenance independently:

```powershell
gh attestation verify ./learning-samples-MAJOR.MINOR.PATCH.spdx.json --repo AsiBackbone/Learning
gh attestation verify ./learning-MAJOR.MINOR.PATCH-release-notes.md --repo AsiBackbone/Learning
gh attestation verify ./release-evidence-manifest.json --repo AsiBackbone/Learning
```

The manifest does not hash itself. Its provenance attestation binds that file to the release workflow; the component hashes inside it bind the SBOM and release-notes files.

## Backfills

A backfill must be generated from a clean checkout of the exact historical tag after a locked restore, build, format check, test run, and documentation build succeed. Use `-GeneratedAfterRelease` and a `-GenerationNote` that records the backfill date, source tag and commit, tool context, and why provenance is unavailable or differs from current releases. Never imply that post-release evidence existed at the original publication time.

## Current boundary and re-evaluation

Learning currently releases documentation and executable sample source only. It does not publish a NuGet package, compiled binary, standalone source archive beyond GitHub's automatic tag archives, or container image. Package/container signing, Source Link validation, and image scanning therefore remain outside this workflow.

Re-evaluate the evidence and signing model before Learning publishes any package, binary, independently produced archive, container, installer, or other consumer-executed artifact. At that point, define artifact-specific SBOM, signing, provenance, vulnerability-reporting, retention, and verification requirements before publication.
