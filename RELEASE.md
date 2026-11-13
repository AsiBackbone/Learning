# Stable Release Evidence Runbook

Learning releases are archival and citation snapshots of educational material. They are not package or runtime support lines. This runbook defines the durable evidence required for every stable `vMAJOR.MINOR.PATCH` GitHub Release.

## Release gate

Publish a stable release only from a clean commit on protected `main`. Before publishing the GitHub Release, confirm the normal documentation and sample validation checks are green and that the release tag resolves to the intended commit.

Also confirm that no implementation-dependent page has a stale review against the selected Learning baseline in `eng/implementation-baseline.json`:

```powershell
dotnet run --file tools/validate-implementation-currentness.cs -- --require-current
```

Normal documentation validation only reports stale reviews; this release check fails on them. Re-review each stale page against the selected baseline and update its review metadata, or keep the previous baseline selected for the release. Record the resulting report in the release-readiness record. See [Implementation Currentness Metadata](CONTRIBUTING.md#implementation-currentness-metadata).

The `Publish Stable Release Evidence` workflow runs again when the GitHub Release is published. It checks out the exact tag and fails unless all of these operations succeed:

The repository-level `global.json` makes `Microsoft.Testing.Platform` the canonical test runner for these commands, matching the runner posture used across the AsiBackbone repositories.

```powershell
dotnet restore samples/Samples.slnx --locked-mode
dotnet build samples/Samples.slnx --no-restore
dotnet format samples/Samples.slnx --verify-no-changes --no-restore --verbosity minimal
dotnet test samples/Samples.slnx --no-build
dotnet run --file tools/validate-docfx-template-baseline.cs
dotnet tool restore
dotnet tool run docfx docs/docfx.json --warningsAsErrors
./scripts/Test-ReleaseEvidence.ps1
```

The workflow also fails if the stable tag is invalid, the tag and checked-out commit disagree, the published release or its notes cannot be resolved, evidence generation fails, provenance cannot be generated and verified against the pinned signer identity, the Sigstore bundle is missing, upload fails, an expected asset is absent, an asset is not anonymously downloadable, or a fresh public download of the release fails signature verification. Both verification passes also run negative tests: an altered subject and an untrusted signer identity must be rejected.

## Durable assets

Every stable release must expose these versioned or self-describing assets directly on the GitHub Release:

| Asset | Scope |
| --- | --- |
| `learning-samples-MAJOR.MINOR.PATCH.spdx.json` | SPDX 2.3 inventory of every tracked file under `samples/` and every distinct NuGet package/version resolved by the committed `packages.lock.json` files. |
| `learning-MAJOR.MINOR.PATCH-release-notes.md` | Exact Markdown body of the published GitHub Release. |
| `release-evidence-manifest.json` | Repository, tag, source commit, generation time, source/lock/dependency counts, component asset SHA-256 hashes, tool versions, commands, and trust-boundary statements. |
| `release-evidence.sigstore.json` | Sigstore bundle of the GitHub provenance attestation covering the three files above: the keyless signing certificate issued to the release workflow, the signature, and the transparency-log inclusion proof. Published from releases after 1.5.0. |

GitHub provenance attestations are generated for the three evidence files. GitHub Actions artifacts are retained briefly for diagnostics and workflow hand-off only; they are not the durable evidence record.

The SBOM describes source and resolved dependency identity. It is not a vulnerability report, does not prove the absence of vulnerabilities, and does not replace GitHub code-scanning or dependency-security surfaces.

## Verification

Download all three assets from the release page, then compare each component asset with the corresponding SHA-256 entry in `release-evidence-manifest.json`:

```powershell
(Get-FileHash -Algorithm SHA256 ./learning-samples-MAJOR.MINOR.PATCH.spdx.json).Hash.ToLowerInvariant()
(Get-FileHash -Algorithm SHA256 ./learning-MAJOR.MINOR.PATCH-release-notes.md).Hash.ToLowerInvariant()
```

Then verify the signed provenance of each file against the pinned signer identity. `--repo` alone accepts any workflow in the repository, so also pin the release-evidence workflow, the release tag, the tagged commit, the GitHub Actions OIDC issuer, and a GitHub-hosted runner:

```powershell
$tag = 'vMAJOR.MINOR.PATCH'
$commit = gh api "repos/AsiBackbone/Learning/commits/$tag" --jq .sha
gh attestation verify ./learning-samples-MAJOR.MINOR.PATCH.spdx.json `
  --bundle ./release-evidence.sigstore.json `
  --repo AsiBackbone/Learning `
  --signer-workflow AsiBackbone/Learning/.github/workflows/publish-release-evidence.yml `
  --source-ref "refs/tags/$tag" `
  --source-digest $commit `
  --cert-oidc-issuer https://token.actions.githubusercontent.com `
  --deny-self-hosted-runners
```

Repeat for the release-notes file and the manifest. For releases up to 1.5.0, which carry no bundle, omit `--bundle`; the attestation is then fetched from GitHub. `./scripts/Test-ReleaseSignatures.ps1 -TagName vMAJOR.MINOR.PATCH -IncludeNegativeTests` downloads the release fresh, applies the same pins to every subject, rejects missing subjects and unexpected assets, and confirms that an altered file and a wrong signer identity both fail.

Without the GitHub CLI, the bundle can be verified with Cosign:

```bash
cosign verify-blob-attestation --new-bundle-format \
  --bundle release-evidence.sigstore.json \
  --type https://slsa.dev/provenance/v1 \
  --certificate-identity "https://github.com/AsiBackbone/Learning/.github/workflows/publish-release-evidence.yml@refs/tags/vMAJOR.MINOR.PATCH" \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com \
  learning-MAJOR.MINOR.PATCH-release-notes.md
```

Trust assumptions: the Sigstore public-good root of trust (Fulcio certificate authority and Rekor transparency log), GitHub as OIDC issuer for the workflow identity, and the repository's branch and tag protection controlling what that workflow runs. A valid signature shows that this workflow, run for this tag and commit on a GitHub-hosted runner, produced these exact bytes. It does not show that the content is correct, safe, or free of vulnerabilities.

The manifest does not hash itself. Its provenance attestation binds that file to the release workflow; the component hashes inside it bind the SBOM and release-notes files.

### Zenodo archives

Zenodo archives each release by downloading GitHub's automatic source archive of the tag (for example `AsiBackbone/Learning-v1.5.0.zip`) and publishes only an MD5 checksum for it. That archive is not produced by the release workflow, is not signed, and does not contain the evidence files, so it cannot be matched to them by digest. GitHub does not promise byte-stable automatic archives, so do not compare a Zenodo download with a fresh GitHub archive by hash. Match it by source instead: the archive's ZIP comment and top-level folder name the commit (`AsiBackbone-Learning-51b3d4c/` for 1.5.0), which must equal the `sourceCommit` in the verified manifest; then compare the extracted tree with `git archive` of that commit. The Zenodo MD5 only checks that the Zenodo download is intact.

## Signing decision

Assessed for #480 against every GitHub Release from `v0.1.0` to `v1.5.0` and the Zenodo records.

| Artifact | Produced by | Decision |
| --- | --- | --- |
| SBOM, release notes, evidence manifest (`v0.15.0` and later) | Release-evidence workflow, uploaded to the release | **Signed.** These are the only immutable, workflow-produced bytes Learning distributes. They are signed by Sigstore keyless GitHub provenance attestations from the protected release workflow (`id-token: write`, SHA-pinned actions). Releases after 1.5.0 also publish the bundle as `release-evidence.sigstore.json`, and the workflow verifies a fresh public download against the pinned identity. |
| Releases `v0.1.0` to `v0.14.0` | No uploaded assets | Nothing to sign. Not backfilled: post-release evidence must never be presented as original. |
| GitHub automatic tag archives | GitHub, on request | Not signed. They are not workflow output, and their bytes are not guaranteed stable. The tag and commit are the source of record. |
| Zenodo archive | Zenodo, from GitHub's archive | Not signed. Matched to a release by commit as described above. |
| Documentation site, Markdown pages, samples source | Published from `main`, or source in the tag | Not signed individually. The SBOM inventories the samples source and the tag carries the rest. |

A separate `cosign sign-blob` signature was **not** added. The existing attestations are already Sigstore keyless signatures over the exact evidence bytes, with a pinned workflow identity and a transparency-log entry. A second signature over the same bytes would add maintenance cost without new assurance. Publishing their bundle covers the durable, offline-verifiable part.

These signals answer different questions and are not interchangeable:

| Signal | What it establishes | What it does not establish |
| --- | --- | --- |
| Zenodo DOI | A persistent citation identifier for an archived record | Integrity, origin, or which bytes were built |
| SHA-256 checksum | That a file matches a recorded digest | Who recorded the digest; it is only as trusted as where you read it |
| GitHub provenance attestation | That a specific workflow, ref, and commit on GitHub Actions produced the bytes, signed through Sigstore | That the content is correct or safe |
| Cosign keyless signature | The same kind of Sigstore identity binding, without the SLSA provenance statement | Build inputs or process, unless an attestation is also present |
| NuGet author signature | That a package was signed with a publisher's certificate, checked by NuGet clients | Anything here, because Learning publishes no NuGet package |

## Backfills

A backfill must be generated from a clean checkout of the exact historical tag after a locked restore, build, format check, test run, and documentation build succeed. Use `-GeneratedAfterRelease` and a `-GenerationNote` that records the backfill date, source tag and commit, tool context, and why provenance is unavailable or differs from current releases. Never imply that post-release evidence existed at the original publication time.

## Current boundary and re-evaluation

Learning currently releases documentation and executable sample source only. It does not publish a NuGet package, compiled binary, standalone source archive beyond GitHub's automatic tag archives, or container image. Package/container signing, Source Link validation, and image scanning therefore remain outside this workflow.

Re-evaluate the evidence and signing model before Learning publishes any package, binary, independently produced archive, container, installer, or other consumer-executed artifact. At that point, define artifact-specific SBOM, signing, provenance, vulnerability-reporting, retention, and verification requirements before publication. Any new release asset must be added to the attested evidence set, because `scripts/Test-ReleaseSignatures.ps1` fails on assets outside it. Also re-evaluate if Zenodo starts archiving workflow-produced assets, or if GitHub attestations stop being Sigstore-backed or verifiable from a published bundle.
