---
description: Why green CI is only one link in .NET package trust, and how to inspect artifact identity, provenance, signing, and release authority.
title: A Green CI Badge Does Not Prove Your .NET Package Is Trustworthy
author: Christopher D. Cavell
published: "2026-08-23"
summary: A practical guide to tracing a NuGet package from reviewed source through build, publication, and verifiable release evidence.
feed: true
---

# A Green CI Badge Does Not Prove Your .NET Package Is Trustworthy

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** Basic familiarity with .NET builds, NuGet packages, and CI terminology. No ASI Backbone knowledge is required.

A repository looks healthy: build passing, tests passing, dependency updates automated, release published.

That is useful evidence. It is not yet an answer to the question a package consumer ultimately cares about:

> **Can I explain how this reviewed source commit became the package I am about to install?**

A green CI run shows that configured jobs completed for a particular run, environment, and set of inputs. It does not automatically establish which package was published, whether release rebuilt it, which identity could publish it, or whether the available provenance and signatures describe the artifact consumers actually received.

The point is not to distrust CI. It is to give CI the right job inside a larger trust chain.

## Follow the Artifact, Not the Badge

For a package-producing .NET repository, trace the artifact through the boundaries where identity, authority, or evidence can change:

```text
Source commit
    ↓
Dependencies
    ↓
CI workflow
    ↓
Build environment
    ↓
Generated package
    ↓
Release workflow
    ↓
Package registry
    ↓
Consumer
```

At each transition, ask:

```text
Who controls this?
What may change?
What is verified?
What evidence survives?
```

No badge, lock file, checksum, signature, attestation, or scanner result substitutes for the rest of that path.

## What Green CI Actually Establishes

If a workflow runs `dotnet restore`, `dotnet build`, and `dotnet test` successfully, it can establish that restore, build, tests, and any other declared checks completed under the configured conditions.

It cannot prove checks it never performed, and success does not automatically extend across a later publication boundary. A green run does not by itself prove that all relevant tests exist, dependencies are safe, the runner was uncompromised, the `.nupkg` was inspected, release published the tested artifact, or the package is safe.

> **Green CI is evidence inside a release trust chain, not proof of the whole chain.**

The rest of the article expands this map:

| Signal or control | What it can help establish | What it does not establish by itself |
| --- | --- | --- |
| Green CI | Configured jobs completed successfully | Trusted release or complete security testing |
| Dependency lock state | Resolution stayed within reviewed lock state | Dependency safety |
| SHA-pinned action | External action identity stays fixed until deliberately changed | Action safety, runner integrity, or secure publication |
| Deterministic/CI build settings | More stable build/source-path behavior under defined inputs | Independent byte-for-byte reproducibility of every `.nupkg` |
| Package validation | Consumer-facing artifact passed configured checks | Authenticity or absence of vulnerabilities |
| SBOM | Component inventory evidence | Component safety or certification |
| Checksum | Bytes match an expected digest from a sufficiently trusted channel | Publisher identity |
| NuGet author signature | Package verifies under an author's signing certificate | Package safety or correct key custody |
| NuGet repository signature | Package integrity relative to the repository that signed it | Author identity or package safety |
| Provenance attestation | Origin/production evidence for the stated subject | Correctness, vulnerability absence, or uncompromised identities |

These controls answer different questions; none makes the others irrelevant.

## Is the Published Package the Validated Package?

A common gap appears after CI succeeds:

```text
Commit abc123
    ↓
Build + test + pack
    ↓
CI green
    ↓
Release starts later
    ↓
Rebuild package
    ↓
Publish rebuilt package
```

The release may be legitimate, but the published artifact now has a separate identity question. The same commit does not automatically imply identical package bytes; toolchain movement, generated inputs, timestamps, native dependencies, environment differences, or packaging behavior can change output.

A clearer release shape is to build, test, pack, validate, preserve the artifact, and publish that same artifact. If release intentionally rebuilds, document what connects the validated build to the published one.

### Deterministic Builds Narrow the Gap

.NET can narrow this gap. `ContinuousIntegrationBuild=true` enables CI-oriented source-path normalization; deterministic build settings improve compiler-output stability under defined inputs; and Source Link connects symbols and package metadata to repository source and commit information. Microsoft's [.NET Source Link guidance](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/sourcelink) recommends deterministic builds for traceability.

Those controls do not prove arbitrary independent builds will produce the same `.nupkg`, and they do not prove a later publication job uploaded the package CI validated. Artifact handoff, digest comparison, reproducible-build checks, or provenance may still be needed when the threat model requires a stronger join.

## Pinned Dependencies and Actions Protect Narrow Properties

Central package management improves version visibility. `packages.lock.json` and locked restore can reduce unexpected dependency-resolution drift. Explicit package sources make resolution easier to review. None of those facts proves the selected dependency is safe.

Dependabot or similar automation is also an intake mechanism, not an approval authority. It makes dependency drift reviewable; review and CI still decide whether the change should merge.

GitHub Actions have the same distinction. A full commit reference such as:

```yaml
uses: vendor/action@<full-commit-sha>
```

prevents a moving tag from silently changing the action identity. It does not prove the pinned code, its dependencies, runner, permissions, or release path are safe.

The March 2025 compromise of `tj-actions/changed-files` is a concrete example. Existing version tags were repointed to malicious code. Wiz reported that hash-pinned users were not impacted unless they updated to an impacted hash during the exploitation window, while also noting residual risk from cached actions after the malicious commits were reverted.

Wiz later updated its analysis to say maintainers reported a compromised personal access token used by a privileged bot, and separately identified `reviewdog/action-setup` as a possible contributor to the compromise. SHA pinning did one useful job: it prevented tag movement from changing the executed revision. It did not solve credential custody or transitive executable-dependency risk. See [Wiz's incident analysis](https://www.wiz.io/blog/github-action-tj-actions-changed-files-supply-chain-attack-cve-2025-30066) and [StepSecurity's report](https://www.stepsecurity.io/blog/harden-runner-detection-tj-actions-changed-files-action-is-compromised).

## Build Validation and Publication Authority Are Different Boundaries

A validation job usually needs to read source and execute build commands. A publication job may need authority to push packages, obtain deployment identity, or use signing material. Those are different privilege sets.

Review the publication boundary explicitly:

- Which job can publish?
- Which events can reach it?
- When do credentials or identities become available?
- Can pull-request code reach them?
- Can unrelated build steps reuse them?

A masked secret can still be over-privileged; redaction does not answer why the credential was present.

For NuGet.org, publication authority also includes package ownership. [Reserved package ID prefixes](https://learn.microsoft.com/en-us/nuget/nuget-org/id-prefix-reservation) can bind a namespace prefix to approved owners and give consumers an identity signal. That helps reduce package-identity ambiguity; it does not prove a package version is safe or correctly built.

## Validate the Package, Not Only the Project

`dotnet build` validates a project build. Consumers receive a `.nupkg`.

Packaging creates another artifact boundary because the package can contain or omit assemblies, target-framework assets, symbols, content files, dependency metadata, repository metadata, version information, and other generated content. Where consequences justify it, validate the package itself through metadata inspection, installation smoke tests, or a minimal external-consumer test.

The claim remains narrow: the generated package passed the checks you configured. That is not a universal trust verdict.

## Checksums and NuGet Signatures Answer Different Questions

A checksum helps answer whether downloaded bytes match an expected digest. Its value depends on how the expected digest is obtained; if an attacker can replace both artifact and checksum in the same channel, the pair can still match.

NuGet signatures add a different claim. NuGet defines two signature roles:

- **Author signature:** created by the package author with an X.509 signing certificate.
- **Repository signature:** added by a package repository to provide integrity evidence relative to that repository.

On NuGet.org, an unsigned submission receives a repository signature; an author-signed package is repository-countersigned. So **"is this package signed?"** is incomplete unless the reviewer also asks **"signed by whom, in which role?"** A NuGet.org repository signature is not the same claim as an author signature.

Microsoft documents this distinction in the [signed packages reference](https://learn.microsoft.com/en-us/nuget/reference/signed-packages-reference). Consumers can inspect signature type and certificate information with:

```bash
dotnet nuget verify package.nupkg
```

See [`dotnet nuget verify`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-verify).

A valid signature still does not mean the package is safe. A verifier needs a trust policy explaining why that signing identity is acceptable for this package and purpose.

## Signing Requires a Key-Custody Story

For an author signature, access to the private signing key or signing service is authority. Review who can use it, where it is held, which workflow can reach it, and how rotation, revocation, and compromise response work.

A cryptographically valid signature produced through an over-broadly exposed signing path may only prove that the compromised path signed the package.

This is also where repository examples need reflexive claim discipline. The current `AsiBackbone/AsiBackbone` package workflow demonstrates locked restore, CI/deterministic build settings, package validation, SBOM generation, build-provenance attestations, artifact handoff, and separated publication authority. It does **not** currently author-sign its `.nupkg` files before publication. Packages published to NuGet.org receive NuGet.org's repository signature, which is a different property.

## Provenance Is Origin Evidence, Not a Safety Verdict

Provenance can identify the workflow, repository/ref, artifact digest, and build identity associated with a release. It is most useful when bound to the exact package ultimately published.

Verified provenance can still describe vulnerable source, a malicious dependency, unsafe configuration, or a compromised trusted identity. It improves the answer to **where did this come from and how was it produced?** It does not automatically answer **should I trust the behavior of this software?**

Build provenance and package signing should remain distinct as well: an attestation about production history is not automatically a NuGet author signature.

## Consumer Verification Completes the Story

Producer-side evidence has limited value if nobody can retrieve or verify it later. Ask:

- Where do the package, SBOM, checksums, provenance statements, and signatures live?
- How long does each form of evidence survive?
- Which tool and trust policy verifies each claim?

For GitHub build provenance attestations, `gh attestation verify package.nupkg --repo <owner>/<repo>` verifies the artifact against associated signed attestations scoped to the repository identity you expect. GitHub also supports tighter signer and source constraints when the threat model requires them; see [Verifying artifact attestations](https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations/use-artifact-attestations).

Temporary CI artifacts are not automatically durable release evidence. A signature or attestation that nobody verifies can become decorative metadata rather than an active trust control.

## Use Controls Proportionally

Not every repository needs SBOMs, provenance, author signing, protected publication environments, or independent reproducible builds. A small repository may reasonably stop at explicit restore/build/test, read-only CI, deliberate dependency updates, and minimal credentials. A widely consumed package may justify much more.

More YAML is not automatically more security. A control earns its place when a maintainer can explain which trust question it answers.

## A Practical Reviewer Pass

1. **Identify the source commit.** Which reviewed commit, tag, or release input should correspond to the package?
2. **Inspect dependency and workflow inputs.** Are package versions, sources, SDK/tool versions, and external actions controlled under an intentional mutability policy?
3. **Inspect workflow permissions.** Which jobs can write, publish, request identities, or access secrets?
4. **Identify who can trigger release.** Can ordinary pull-request code or unrelated automation reach publication?
5. **Locate publication authority.** Where do registry credentials, federated identities, package ownership, or signing permissions become available?
6. **Follow the artifact handoff.** Is the published package the one built, tested, packed, and validated, or does release rebuild it?
7. **Inspect provenance and signatures.** What exact artifact is each digest, attestation, author signature, or repository signature about? Where those evidence types exist, use `dotnet nuget verify package.nupkg` for NuGet signatures and `gh attestation verify package.nupkg --repo <owner>/<repo>` for GitHub attestations.
8. **Check the consumer view.** Can a downstream consumer connect the published package to source, release process, package identity, and retained evidence?

If those questions have clear answers, the green badge has been placed in context instead of being asked to carry a claim it was never designed to prove.

## Further Reading

- [Software Supply-Chain Integrity for .NET Repositories](https://asibackbone.github.io/Learning/security/software-supply-chain-integrity-for-dotnet-repositories.html) — the deeper Learning walkthrough of source, dependency, workflow, build, package, provenance, and publication boundaries.
- [Signing, Verification, Key Custody, and Tamper Evidence](https://asibackbone.github.io/Learning/security/signing-verification-key-custody-and-tamper-evidence.html) — deeper treatment of hashes, signatures, trust anchors, key lifecycle, and verification policy.
- [Secret Handling Across Trust Boundaries](https://asibackbone.github.io/Learning/security/secret-handling-across-trust-boundaries.html) — broader guidance for CI/CD secrets, short-lived identities, rotation, revocation, and compromise response.
- [AsiBackbone package repository workflows](https://github.com/AsiBackbone/AsiBackbone/tree/main/.github/workflows) — an optional specimen for package validation, artifact handoff, SBOM, provenance, and publication-boundary patterns. It is not a specimen for NuGet author signing.

The point is not to copy one repository's release stack. The point is to explain, narrowly and verifiably, how reviewed source became the artifact a consumer received.
