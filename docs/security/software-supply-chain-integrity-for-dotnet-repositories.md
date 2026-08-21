---
description: Trace trust from source commit to .NET artifact and learn how dependency, CI, build, signing, provenance, and release controls reduce supply-chain risk.
---

# Software Supply-Chain Integrity for .NET Repositories

**Learning objective:** Trace how trust changes from source commit to published .NET artifact, connect common repository controls to the threats they are intended to reduce, and distinguish useful integrity evidence from guarantees those controls do not provide.

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** [Trust Boundaries and Least Privilege](trust-boundaries-and-least-privilege.md). [Signing, Verification, Key Custody, and Tamper Evidence](signing-verification-key-custody-and-tamper-evidence.md) is useful when evaluating package signing, attestations, checksums, and provenance claims.

## Pattern Card

> **Problem:** A well-designed application can still be delivered through a compromised or ambiguous build and release path. Dependencies, CI actions, workflow permissions, build credentials, package metadata, generated artifacts, and publication steps all introduce trust decisions outside the runtime request pipeline.
>
> **Pattern:** Treat the software supply chain as a sequence of explicit trust boundaries. At each stage, identify what is trusted, who controls it, what may change, what is verified, and what evidence remains. Apply controls that are proportional to the artifact's consumers and consequences, then describe their guarantees narrowly.
>
> **Use when:** A repository builds executable software, publishes packages or images, distributes release artifacts, runs privileged automation, or serves as a reference that others are likely to copy.
>
> **Prefer something simpler when:** A small local teaching repository produces no distributable artifact and has low-impact automation. Even then, keep obvious trust boundaries visible rather than adopting heavyweight release infrastructure without a real threat model.
>
> **Observe:** A reviewer can explain how source becomes a release, identify mutable or privileged inputs, distinguish build validation from release provenance, and verify that the repository does not claim more than its controls establish.

The lifecycle in this walkthrough is:

```text
Source
   ↓
Dependencies
   ↓
Restore
   ↓
Build
   ↓
Test
   ↓
Artifact
   ↓
Package / Release
   ↓
Published Provenance
```

A second view emphasizes changes in control:

```text
Developer Commit
      ↓
Repository Controls
      ↓
CI Workflow
      ↓
External Actions / Dependencies
      ↓
Build Environment
      ↓
Generated Artifact
      ↓
Release Process
      ↓
Consumer
```

At every transition, ask:

```text
What is trusted?
Who controls it?
What can change?
What is verified?
What evidence remains?
```

The central lesson is:

> **Supply-chain integrity is a trust-chain property, not a collection of CI badges.**

---

## What This Walkthrough Uses as Working Specimens

This article uses three ASI Backbone organization repositories selectively:

- [`AsiBackbone/Learning`](https://github.com/AsiBackbone/Learning) — an educational repository with documentation and sample-validation workflows.
- [`AsiBackbone/AsiBackbone`](https://github.com/AsiBackbone/AsiBackbone) — a package-producing .NET repository with central dependency management, locked restore, release validation, SBOM generation, provenance attestations, and package publication automation.
- [`AsiBackbone/NetCoreApplicationTemplate`](https://github.com/AsiBackbone/NetCoreApplicationTemplate) — an ASP.NET Core reference repository with dependency-update automation and broader application CI/security configuration.

The point is not:

> Copy these repositories exactly.

The point is:

> Inspect why a control exists, identify the trust boundary it strengthens, and decide whether the same concern exists in your repository.

Repository examples are snapshots of current practice, not universal security standards.

---

## Stage 1: Source and the Repository Trust Boundary

The first supply-chain question is not about NuGet.

It is:

> **Which source is allowed to become release input?**

A repository may contain:

- Application source.
- Tests.
- Build scripts.
- Workflow YAML.
- Package metadata.
- Tool manifests.
- Dependency configuration.
- Release scripts.
- Documentation generators.

All of these can affect what consumers eventually receive.

That means a change to:

```text
.github/workflows/publish.yml
```

can be as security-sensitive as a change to:

```text
src/Payments/PaymentService.cs
```

if the workflow can alter, replace, sign, or publish the resulting artifact.

### Review Workflow Changes as Security-Sensitive Code

A workflow change can modify:

```text
Permissions
Credentials
Build commands
Dependencies
Artifact paths
Publication destinations
Attestation subjects
Release triggers
```

A code review process that carefully reviews application source but casually accepts workflow changes has an incomplete repository trust model.

Useful review questions include:

1. Did the workflow gain a write permission?
2. Did it start using a new third-party action?
3. Did an action reference move from a commit SHA to a mutable tag?
4. Did checkout begin persisting credentials?
5. Did the release trigger become broader?
6. Did a secret become available to a less-trusted job?
7. Did the artifact path change?
8. Did restore stop using the intended dependency controls?
9. Did package or provenance validation move after publication?

### Protected Branches and Required Checks

Repository rules can strengthen the source boundary by requiring review, required status checks, or other merge conditions before protected branches change.

But workflow YAML does not, by itself, prove that branch protection is configured.

The current Learning `docs-validation.yml` includes a repository-maintained comment that its documentation job is intended to be a required branch-protection check and therefore runs for every pull request targeting `main`.

That is useful design evidence.

The actual repository-host rule remains a separate control that must be inspected where it is configured.

This distinction matters:

```text
Workflow exists
    ≠
Workflow is required before merge
```

---

## Stage 2: GitHub Actions as Executable Dependencies

A GitHub Action is executable supply-chain input.

Compare:

```yaml
uses: actions/checkout@v4
```

with:

```yaml
uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # reviewed release
```

The first reference follows a tag.

The second identifies one commit.

The security property is **reference mutability**.

A SHA pin reduces the chance that the action code changes underneath an unchanged workflow file.

It does not prove that the pinned action is safe.

Keep the claim narrow:

```text
SHA-pinned action
        ↓
Reduces reliance on a mutable action tag
```

but:

```text
SHA-pinned action
        ≠
Proof that pinned code is trustworthy
```

The pinned commit may still contain a defect, malicious behavior, compromised dependency, or behavior inappropriate for your threat model.

### Learning Repository Specimen

The current Learning workflows use full commit references for actions such as checkout and .NET SDK setup.

They also retain a human-readable release comment next to the pin.

That combination is useful because it separates:

```text
Execution identity = commit SHA
Human maintenance hint = release label comment
```

A maintainer can update the pin deliberately while still seeing which upstream release the commit is expected to represent.

### Third-Party Build Tooling Extends the Chain

Actions are not the only build dependencies.

Examples include:

- .NET global or local tools.
- PowerShell modules.
- npm-based documentation tooling.
- Container images.
- Shell utilities installed during CI.
- Package-signing tools.
- SBOM generators.
- Release CLIs.

Every downloaded executable changes the set of parties and artifacts trusted by the build.

The current Learning repository uses a checked-in .NET tool manifest with a specific DocFX version.

That improves version visibility compared with installing an unspecified latest tool during every build.

It still does not prove the tool is safe.

---

## Stage 3: Workflow Permissions and CI Runner Trust

A build runner executes repository-controlled commands with whatever authority the workflow grants it.

The questions are therefore:

```text
What can this job read?
What can this job write?
Which credentials can it obtain?
Which environment can it deploy to?
```

### Deny Broad Permissions by Default

The current Learning validation workflows use:

```yaml
permissions: {}
```

at the workflow level and then grant the validation job only:

```yaml
permissions:
  contents: read
```

That expresses a useful least-privilege property:

```text
Validation job
      ↓
Can read source needed to validate
      ↓
Does not receive unrelated repository write authority by default
```

This mirrors runtime least privilege.

The unit is different—the CI job rather than the application service—but the architectural question is the same:

> **What is the minimum authority required for this operation?**

### Publishing Needs Different Authority

The Learning documentation publication workflow separates build and deploy jobs.

The build job uses read-only repository access.

The deployment job receives the permissions required for GitHub Pages publication:

```text
pages: write
id-token: write
```

and uses the `github-pages` environment.

This separation is useful because:

```text
Build authority
    ≠
Deployment authority
```

A release-oriented repository should ask the same question about package publication.

### Least Privilege Is Context-Specific

Do not mechanically copy:

```yaml
permissions: {}
```

without understanding which permissions later jobs need.

The objective is not zero permissions.

It is:

```text
Only required authority
at the job that requires it
for the duration that requires it
```

---

## Checkout Credentials and `persist-credentials`

Repository checkout is another credential boundary.

The current Learning workflows configure:

```yaml
with:
  persist-credentials: false
```

for checkout.

That choice is useful when later steps do not need the checkout credential persisted into local Git configuration.

The architectural question is:

> **Does this job need a reusable repository credential after source retrieval?**

If the answer is no, leaving one available increases unnecessary authority.

But keep the claim narrow:

```text
persist-credentials: false
        ↓
Reduces persistence of checkout credentials for later git operations
```

It does not mean:

```text
The job has no credentials
```

A workflow may still receive `GITHUB_TOKEN`, OIDC identity, environment secrets, package credentials, cloud credentials, or other authority through different mechanisms.

---

## Stage 4: Dependency Trust Is More Than Version Selection

A .NET restore answers:

```text
Which packages should be resolved?
```

It does not automatically answer:

```text
Are those packages trustworthy?
```

Dependency trust involves several separate concerns:

- Package identity.
- Package version.
- Package source.
- Transitive dependencies.
- Update review.
- Vulnerability information.
- Publisher trust.
- Package integrity mechanisms.
- Restore determinism.

A version can be exactly pinned and still be malicious or vulnerable.

### Central Package Management

The current `AsiBackbone/AsiBackbone` repository uses `Directory.Packages.props` with:

```xml
<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
```

and keeps package versions in one central file.

This strengthens **version visibility and consistency**.

It can make review easier because dependency version changes are concentrated rather than duplicated across many project files.

It does not establish dependency safety.

```text
Central package management
        ≠
Trusted dependencies
```

### Lock Files and Locked Restore

The same repository configures package lock-file generation in `Directory.Build.props` and release workflows restore using:

```text
dotnet restore ... --locked-mode
```

The property being protected is restore drift.

If the dependency graph no longer matches the checked-in lock state, locked restore should fail instead of silently accepting a newly resolved graph.

That is stronger than assuming:

```text
Same project file
        ↓
Same dependency graph forever
```

But locked restore still depends on the package sources and artifacts being appropriate to trust.

```text
Locked restore
        ≠
Proof packages are safe
```

### When Lock Files Are Worth the Cost

Lock files can add maintenance overhead, especially across many projects or frameworks.

They become more valuable when:

- Release reproducibility matters.
- Dependency graphs are nontrivial.
- Packages are distributed to external consumers.
- Restore drift would materially change a release.
- Automated update tooling can keep lock state synchronized.

A small teaching project may reasonably choose a simpler model if the learning objective does not depend on restore reproducibility.

The choice should still be deliberate.

---

## Package Sources Are Part of the Trust Model

NuGet version configuration is only part of dependency resolution.

Ask:

```text
Which feeds are configured?
Can package-source order change resolution?
Are private and public package identities separated safely?
Can an unexpected feed satisfy a package ID?
Who controls feed credentials?
```

A trusted package feed is not merely one that responds over HTTPS.

Trust may depend on:

- Who can publish to it.
- Which package identities are expected there.
- How credentials are protected.
- Whether repository policy constrains sources.
- Whether source mapping or other package-source controls are appropriate.

Do not treat the network location alone as a complete publisher trust decision.

---

## Dependency Update Automation Is an Intake Mechanism

The current `AsiBackbone/AsiBackbone` and `NetCoreApplicationTemplate` repositories use Dependabot configuration for both NuGet and GitHub Actions updates.

The configuration groups related updates, limits open pull requests, applies a cooldown, and schedules update checks.

That improves maintenance flow.

It does not authorize the updates automatically.

A healthy mental model is:

```text
Dependency bot
      ↓
Proposes repository change
      ↓
Review + CI + policy
      ↓
Possible merge
```

not:

```text
Dependency bot opened PR
      ↓
Dependency is trusted
```

Automated updates should still be reviewed for:

- Unexpected major-version changes.
- New transitive dependencies.
- Changed package ownership or provenance.
- Action permission changes.
- Build-script changes.
- Lock-file changes.
- Behavioral regressions.
- Security advisories and release context.

### Dependency Scanning Has a Narrow Claim

A dependency scanner can identify known conditions represented in the data sources it uses.

That does not prove the absence of unknown vulnerabilities or malicious behavior.

```text
Dependency scan clean
        ≠
Dependency safe
```

Likewise:

```text
Known vulnerability detected
        ≠
Automatic proof of exploitability in this application
```

The signal is useful.

Its meaning should remain proportional to the evidence.

---

## Stage 5: Restore Determinism and Build Determinism Are Different

A locked dependency graph does not make the complete build reproducible.

Build output may also depend on:

- SDK version.
- Compiler version.
- Environment variables.
- Operating system and architecture.
- Timestamps.
- Generated source.
- Network-fetched data.
- Tool versions.
- Native dependencies.
- Build scripts.
- Repository state.

The `AsiBackbone/AsiBackbone` repository sets deterministic build properties and enables `ContinuousIntegrationBuild` in GitHub Actions.

It also enables Source Link-related build metadata in CI.

Those settings improve artifact traceability and deterministic-build behavior.

They should not be over-described as proof of perfectly reproducible builds across arbitrary machines.

### Deterministic Build

A deterministic build generally seeks to make equivalent build inputs produce equivalent compiler outputs under the defined build model.

### Reproducible Build

A stronger reproducibility claim may require independent rebuilds from a precisely defined environment and comparison of resulting artifacts.

That can involve:

```text
Pinned SDK
Pinned toolchain
Pinned dependencies
Canonical build inputs
Controlled environment
Independent rebuild
Artifact comparison
```

Therefore:

```text
<Deterministic>true</Deterministic>
        ≠
Independent reproducible-build proof
```

---

## Pin the SDK When Release Repeatability Matters

A workflow such as:

```yaml
dotnet-version: '10.0.x'
```

allows movement within the matching SDK band.

That may be entirely appropriate for a teaching repository that wants routine SDK servicing.

A package-producing repository may choose a checked-in `global.json` to make the SDK selection more explicit.

The current `AsiBackbone/AsiBackbone` release workflows use `global-json-file: global.json` when setting up .NET.

Neither approach is universally correct.

The decision is a tradeoff between:

```text
Automatic servicing movement
        vs.
Stricter toolchain repeatability
```

Document the intended behavior.

---

## Stage 6: Build Validation Should Match What You Ship

CI becomes meaningful when it validates the actual artifact path.

The current Learning sample-validation workflow performs:

```text
dotnet restore
      ↓
dotnet build --no-restore
      ↓
dotnet test --no-build
```

against the sample solution.

That separation catches a useful class of drift:

- Restore must succeed.
- Build must consume restored state.
- Tests must run against the built state.

The documentation workflow separately builds DocFX with warnings treated as errors.

This reflects a repository-specific truth:

> Documentation is a shipped artifact of Learning, so documentation validation belongs in its supply chain.

For another repository, the required checks may instead include:

- Unit tests.
- Integration tests.
- Formatting.
- Static analysis.
- Package smoke tests.
- Container build tests.
- Template instantiation tests.
- Documentation generation.

### Green CI Has a Narrow Meaning

A successful workflow proves only that the configured jobs completed successfully for that run under that environment.

It does not establish:

```text
All relevant tests exist
All dependencies are safe
Runner was uncompromised
Release artifact matches CI artifact
Publication used the expected credential
Provenance was produced
Consumer received the same artifact
```

Therefore:

```text
CI green
        ≠
Trusted release
```

Green CI is useful evidence inside a larger trust chain.

---

## Stage 7: Validate the Package, Not Only the Project

A common release gap is:

```text
Project builds
Tests pass
      ↓
dotnet pack
      ↓
Publish without inspecting the package
```

The generated package is a new artifact boundary.

Packaging can introduce or omit:

- Assemblies.
- Symbols.
- Content files.
- README/license/icon metadata.
- Repository metadata.
- Dependency metadata.
- Target-framework assets.
- Version metadata.
- Build outputs not exercised as packaged.

The current `AsiBackbone/AsiBackbone` release validation includes explicit package creation, generated-version checks, NuGet metadata validation, template smoke tests where relevant, external-consumer smoke tests, and package integration smoke tests.

That is a useful teaching specimen because validation continues **after** compilation.

The architectural property is:

> **Validate the form consumers will receive, not only the source form maintainers build.**

---

## NuGet Metadata Is Part of Consumer Trust

Package metadata can help a consumer answer:

```text
What package is this?
Which version is this?
Where is its repository?
Where is project documentation?
What license information is declared?
```

Repository metadata does not prove the package is safe.

But incorrect or misleading metadata weakens traceability.

The `AsiBackbone/AsiBackbone` shared build properties include repository and project URL metadata and configure repository publication metadata for packages.

Release validation then checks generated package metadata before publication.

That gives two layers:

```text
Build configuration declares metadata
        ↓
Generated package is validated
```

The second step matters because configuration intent and packaged result can diverge.

---

## Source Link Strengthens Traceability, Not Trustworthiness

Source Link can associate built binaries with repository source information for debugging and source navigation.

That is useful traceability evidence.

It does not prove:

- The repository was uncompromised.
- The source was reviewed.
- The build runner was trustworthy.
- The package was published by an authorized party.
- The source itself is secure.

The current `AsiBackbone/AsiBackbone` build configuration enables Source Link behavior in CI and publishes repository metadata.

A precise claim is:

> The package build includes source/repository traceability metadata configured by the repository.

Avoid turning that into:

> Source Link proves this binary is trustworthy.

---

## Stage 8: SBOMs Describe Components; They Do Not Certify Them

A Software Bill of Materials can record components associated with a build or package.

That can improve:

- Inventory.
- Incident response.
- Dependency review.
- Vulnerability triage.
- Consumer visibility.
- Historical investigation.

The current `AsiBackbone/AsiBackbone` release workflows generate package SBOMs and upload them as artifacts.

The publish workflow also creates provenance attestations for both package artifacts and generated SBOM files.

That is meaningful evidence.

But:

```text
SBOM generated
        ≠
Dependencies verified trustworthy
```

and:

```text
SBOM complete according to generator
        ≠
Security certification
```

An SBOM can accurately list a vulnerable or malicious component.

Its existence is inventory evidence, not a safety verdict.

### Ask What the SBOM Actually Covers

A reviewer should ask:

```text
Does it describe the package or the build environment?
Does it include transitive dependencies?
Does it include build-only tooling?
Which format and schema version is used?
How is the SBOM tied to the artifact?
Where is it retained?
How is it verified later?
```

Do not assume that one file named `sbom.json` answers every supply-chain inventory question.

---

## Stage 9: Provenance, Authenticity, and Trustworthiness Are Different

These words are often collapsed.

Keep them separate.

### Provenance

Provenance answers questions about origin and production history, such as:

```text
Which workflow produced this artifact?
Which repository/ref was involved?
Which subject artifact is the statement about?
```

### Authenticity

Authenticity asks whether evidence can be verified as coming from an expected identity or signing authority under an explicit trust policy.

### Trustworthiness

Trustworthiness is broader.

Even authentic provenance may describe a build that used vulnerable code, incorrect policy, malicious dependencies, or a compromised trusted identity.

Therefore:

```text
Provenance verified
        ≠
Artifact safe
```

and:

```text
Artifact signed
        ≠
Publisher authorized for every purpose
```

For the deeper cryptographic boundary, continue with [Signing, Verification, Key Custody, and Tamper Evidence](signing-verification-key-custody-and-tamper-evidence.md).

---

## Build Attestations Are Not Package Signing

The current `AsiBackbone/AsiBackbone` package workflow uses GitHub build-provenance attestation for generated `.nupkg` files and SBOM files.

That demonstrates one provenance mechanism.

Do not silently relabel it as NuGet package signing.

These are different concepts:

```text
Build provenance attestation
        =
Evidence about artifact production/origin
```

versus:

```text
NuGet package signature
        =
A signature embedded/applied according to NuGet package-signing semantics
```

A repository may use one, both, or neither depending on its distribution and trust model.

The same distinction applies to generic artifact signing.

Ask what is signed or attested, by whom, how the consumer verifies it, and what claim verification supports.

---

## Release Hashes and Checksums

A release process may publish a digest such as SHA-256 for an artifact.

A checksum can help detect accidental or malicious content changes **if the expected checksum is obtained through a trustworthy channel**.

For example:

```text
Artifact A
      ↓
SHA-256 = abc...
```

Later:

```text
Downloaded artifact
      ↓
SHA-256 = abc...
      ↓
Bytes match the expected digest
```

But a checksum published beside a compromised artifact in the same mutable channel can be replaced with it.

Therefore:

```text
Checksum match
        ≠
Independent proof of publisher identity
```

A hash is content identity evidence.

Its trust value depends on how the expected digest is distributed and protected.

---

## Stage 10: Release Credentials Are High-Impact Authority

A build job that can publish a package has materially different authority from a test job.

Release credentials may include:

- Package registry API keys.
- OIDC-derived publication credentials.
- Repository release tokens.
- Signing authority.
- Cloud deployment identities.
- Container-registry credentials.

Ask:

```text
Which job receives publication authority?
When is it available?
Which trigger can reach that job?
Which environment gates it?
Can pull-request code access it?
Can an unrelated build step reuse it?
```

### Separate Pack from Publish

The current `AsiBackbone/AsiBackbone` package workflow separates:

```text
Validate + build + test + pack + attest
```

from:

```text
Publish package
```

The publication job uses a named `package-publish` environment.

That separation creates a clearer boundary between producing an artifact and granting authority to distribute it.

It also passes the package between jobs as an artifact rather than rebuilding it in the publish job.

That helps preserve the question:

> **Is the thing being published the thing that was validated?**

### Secrets Should Not Become Build Output

Workflow secret handling should avoid:

- Printing secrets.
- Writing credentials into artifacts.
- Passing long-lived secrets to jobs that do not need them.
- Persisting secrets into caches.
- Embedding tokens into package metadata.
- Making secrets available to untrusted pull-request code.

A secret can be masked in logs and still be over-privileged.

Masking is not least privilege.

---

## Environments Add a Deployment Boundary

GitHub environments can be used to separate deployment or publication from ordinary CI and may support environment-specific secrets and protection rules depending on repository configuration.

The Learning Pages workflow uses a `github-pages` environment.

The `AsiBackbone/AsiBackbone` package workflow uses a `package-publish` environment.

The YAML establishes the environment boundary in the workflow.

Whether that environment has required reviewers, wait timers, branch restrictions, or other protections is repository-host configuration and should be verified separately.

Again:

```text
Environment named in YAML
        ≠
Proof that environment protections are configured
```

---

## Artifact Retention Is Part of Evidence Design

CI artifacts are often temporary.

If a release investigation later depends on:

```text
Package
SBOM
Test report
Provenance statement
Checksum
Build log
```

then retention policy affects whether that evidence remains available.

Ask:

- Which evidence is temporary CI output?
- Which evidence is attached to a durable release?
- Which evidence is published to a package registry?
- Which evidence is retained externally?
- How long must consumers or maintainers be able to verify it?

Artifact upload is not the same as durable archival.

Retention should match the claim the repository intends to support.

---

## Versioning Connects Source, Artifact, and Release

Version metadata is a provenance join key.

A reviewer often needs to connect:

```text
Git tag
      ↓
Package version
      ↓
Assembly informational version
      ↓
Release notes / changelog
      ↓
Published artifact
```

The current `AsiBackbone/AsiBackbone` release workflows validate version consistency before and after package creation.

This reduces a class of mistakes where:

```text
Tag says v3.2.1
Package says 3.2.0
Release notes describe another version
```

Version consistency does not prove artifact authenticity.

It makes the release easier to identify and investigate.

---

## Changelogs and Release Notes Are Human Provenance

Machine-readable provenance answers technical origin questions.

Changelogs and release notes answer a different question:

> **What did maintainers intend to change in this release?**

That human context helps reviewers evaluate:

- Dependency updates.
- Security fixes.
- Breaking changes.
- Packaging changes.
- New build tooling.
- Release process changes.

A changelog can be inaccurate.

It is not cryptographic evidence.

But a release process with no coherent human change history is harder to review and investigate.

---

## Package Signing Concepts

Package signing can strengthen authenticity claims when consumers verify signatures under an appropriate trust policy.

But signing introduces its own supply chain:

```text
Signing key
Certificate / identity
Signing service
Key custody
Rotation
Revocation
Verification policy
Timestamping where used
```

The important question is not merely:

> Is the package signed?

Ask:

```text
Who is expected to sign it?
How is that signer authorized?
How does the consumer verify it?
How are expired or revoked credentials handled?
What happens after key compromise?
```

A correctly signed malicious package remains malicious.

```text
Valid signature
        ≠
Safe package
```

---

## Vulnerability Scanning and Dependency Review

Vulnerability scanning can strengthen the dependency boundary by comparing known component identities against vulnerability intelligence.

Dependency review can help show how a pull request changes the dependency graph.

These are valuable controls when distribution risk justifies them.

They still operate on available information.

Do not claim:

```text
No scanner findings
        ↓
No vulnerabilities exist
```

Prefer:

> The configured scan found no matching known issues under its current data and policy.

That wording reflects the actual evidence.

---

## A Trust-Boundary Walkthrough of the Learning Repository

The Learning repository is intentionally lighter than a package publisher, but it still contains useful supply-chain boundaries.

### Source to CI

Current validation workflows:

- Run on pull requests to `main`.
- Default token permissions to none.
- Grant validation jobs `contents: read`.
- Use SHA-pinned actions.
- Disable persisted checkout credentials.

The property being strengthened is **controlled, least-privilege validation of repository content**.

### Tool Restore

The repository checks in a .NET tool manifest with an explicit DocFX version.

The property being strengthened is **tool-version visibility and repeatability**.

### Documentation Build

DocFX runs with warnings treated as errors in documentation validation.

The property being strengthened is **published documentation consistency**.

### Sample Build

Sample CI restores, builds, and tests the sample solution.

The property being strengthened is **executable teaching-sample integrity relative to the checked-in source and configured dependencies**.

### Documentation Publication

The Pages workflow separates build from deployment and gives the deploy job the write/OIDC permissions it needs.

The property being strengthened is **separation of validation/build authority from publication authority**.

### What Learning Does Not Demonstrate

Learning is not a NuGet package-release specimen.

Its workflows do not establish the complete package publication, SBOM, package attestation, or package-signing lifecycle taught later in this article.

That is why the package-producing `AsiBackbone/AsiBackbone` repository is used as the richer working specimen for those stages.

---

## A Trust-Boundary Walkthrough of the AsiBackbone Package Repository

The package repository demonstrates a longer chain.

### Dependency Definition

`Directory.Packages.props` centralizes package versions.

`Directory.Build.props` requests package lock files and includes deterministic/repository metadata settings.

**Property:** dependency/version visibility and build metadata consistency.

### Restore

Release workflows use locked restore.

**Property:** prevent unreviewed dependency-resolution drift from silently entering a release build.

### Build and Test

Release validation builds, checks formatting, runs tests, builds documentation, and performs smoke tests.

**Property:** validate multiple representations of the repository before publication.

### Pack

Package projects are discovered and packed into a dedicated artifact directory.

Generated package versions and NuGet metadata are validated.

**Property:** inspect the consumer-facing package artifact rather than assuming project configuration produced the intended package.

### SBOM

Package SBOMs are generated into a separate artifact directory.

**Property:** retain component/inventory evidence associated with the release process.

### Attestation

Build-provenance attestations are produced for package and SBOM subjects.

**Property:** attach verifiable provenance evidence to specific generated subjects.

### Publish

A later job downloads the previously packed artifacts and publishes them using package-publication authority.

**Property:** separate artifact production/validation from registry publication.

The chain is useful because each step answers a different trust question.

None should be treated as replacing all the others.

---

## Dependabot Walkthrough

The organization package and application-template repositories provide a useful dependency-update specimen.

Their current configuration includes scheduled update checks for:

```text
NuGet
GitHub Actions
```

and groups related dependencies.

The architectural value is not the schedule itself.

It is that dependency drift becomes a **reviewable repository change** rather than an invisible manual process.

A reviewer can inspect:

```text
Old version
New version
Lock-file or manifest change
CI results
Release notes / advisory context
```

before merging.

The bot is an intake mechanism, not an approval authority.

---

## Contrasting Weak and Stronger Workflow Shapes

Consider a deliberately simplified weak workflow:

```yaml
permissions: write-all

steps:
  - uses: actions/checkout@v4
  - run: dotnet restore
  - run: dotnet test
  - run: dotnet nuget push ./bin/*.nupkg --api-key "$KEY"
```

Several concerns are collapsed:

```text
Broad token authority
Mutable action reference
Validation and publication in one job
Unclear artifact identity
Release credential available beside arbitrary build commands
No explicit package validation
No provenance evidence
```

A stronger shape might separate concerns:

```text
Read-only validation job
      ↓
Pinned external actions
      ↓
Controlled restore
      ↓
Build + test
      ↓
Pack
      ↓
Validate generated package
      ↓
Record SBOM / provenance where required
      ↓
Publication job with narrow release authority
```

This diagram is intentionally architectural.

Do not copy it as a claim that every repository needs every stage.

---

## Threat-to-Control Matrix

| Threat or ambiguity | Example control | Property strengthened | What it does not guarantee |
| --- | --- | --- | --- |
| Action tag changes underneath workflow | Pin action to commit SHA | External action identity is stable until reviewed update | Pinned code is safe |
| Validation job has unnecessary repository authority | Job-level least-privilege permissions | Limits blast radius of validation job | Runner or dependency is uncompromised |
| Checkout credential remains available unnecessarily | `persist-credentials: false` | Reduces reusable checkout credential persistence | Job has no other credentials |
| Dependency graph drifts during restore | Lock files + locked restore | Restore graph must match reviewed lock state | Packages are trustworthy |
| Package versions diverge across projects | Central package management | Improves version consistency and reviewability | Selected versions are secure |
| Dependencies age silently | Dependabot/equivalent | Produces reviewable update proposals | Updates should be auto-merged |
| Build passes but package is malformed | Package metadata/smoke validation | Tests consumer-facing artifact | Package is vulnerability-free |
| Consumer cannot inventory components | SBOM | Component inventory evidence | Components are safe or SBOM is a certification |
| Artifact origin is ambiguous | Provenance attestation | Production/origin evidence for artifact subject | Artifact logic is correct or safe |
| Artifact bytes may be altered | Checksum/signature depending threat model | Content identity / authenticity evidence | Publisher intent or safety by itself |
| Release job has broad long-lived credentials | Environment-scoped or identity-based release authority | Narrows publication authority and exposure | Release process is fully trustworthy |
| Source and package versions diverge | Version consistency validation | Traceability across tag/build/package | Artifact authenticity |
| Workflow changes bypass scrutiny | Review + protected branch/required checks | Strengthens repository change control | Reviewers catch every malicious change |

This table is deliberately phrased in terms of **properties strengthened**, not guarantees achieved.

---

## Supply-Chain Evidence Layers

A mature release may accumulate several types of evidence:

```text
Source commit
CI run
Test results
Dependency lock state
Package metadata
SBOM
Artifact digest
Build attestation
Package signature
Release notes
Registry publication record
```

These artifacts overlap but are not interchangeable.

For example:

```text
SBOM
=
What components are described?
```

```text
Attestation
=
What process/identity claims to have produced this subject?
```

```text
Signature
=
Can cryptographic verification establish a signer under a trust policy?
```

```text
Checksum
=
Do these bytes match an expected digest?
```

```text
Tests
=
Did configured test assertions pass for this build?
```

A good investigation may need several layers.

---

## Provenance Should Bind to the Artifact You Actually Publish

A subtle failure mode is:

```text
Build artifact A
      ↓
Attest artifact A
      ↓
Rebuild artifact B
      ↓
Publish artifact B
```

The provenance statement may be valid for A while consumers receive B.

A stronger release path passes the validated artifact forward rather than rebuilding after validation without a reason.

The current `AsiBackbone/AsiBackbone` package workflow packs artifacts in one job, uploads them, and downloads those artifacts in the publication job.

That preserves a clearer artifact handoff.

A production design may go further by verifying digests across boundaries or attaching registry-native provenance.

The key question remains:

> **Is the published subject the same subject the evidence describes?**

---

## Release Rebuilds Need an Explicit Reason

Sometimes rebuilding in the publish job is unavoidable or intentional.

If so, say what evidence connects the two builds.

Possible strategies may include:

- Deterministic/reproducible build comparison.
- Digest comparison.
- Independent attestation of the publication build.
- Release-specific environment controls.

Avoid assuming:

```text
Same git commit
        ↓
Byte-identical artifact
```

unless the build model actually supports that claim.

---

## CI Runner Trust

A hosted or self-hosted runner is part of the build trusted computing base.

A repository can pin every action and still be vulnerable if the runner environment is compromised.

Ask:

```text
Who controls the runner image?
Is it ephemeral or persistent?
Can one job leave state for another?
Which network destinations can it reach?
Which credentials are available?
Which caches are shared?
How are self-hosted runners patched?
```

This is one reason supply-chain security cannot be reduced to action pinning.

```text
Pinned workflow inputs
        ≠
Trusted execution environment
```

---

## Caches Are Another Mutable Input

Build caches improve performance but can also influence the build.

Threat-model them as inputs:

- Who can populate the cache?
- How is the key constructed?
- Can untrusted branches poison a cache used by release jobs?
- Does the build verify restored artifacts independently?
- Is a cache performance-only, or can it change output?

A repository without caches avoids this particular complexity.

A repository with caches should make cache trust explicit.

---

## Secret Handling Is Not Only About Redaction

Secret scanning, masking, and redaction are useful.

The stronger question is:

> **Why was this secret available to this step at all?**

Prefer designs where:

```text
Validation
      ↓
No publication secret
```

and:

```text
Publication
      ↓
Only required release authority
```

Short-lived identity-based credentials can reduce long-lived secret exposure when supported by the package registry or deployment target.

They still require a correct trust policy between issuer, repository/workflow identity, and destination.

---

## `GITHUB_TOKEN` Is Still Authority

`GITHUB_TOKEN` is convenient because it is scoped to a workflow run, but it should still be treated as an authorization artifact.

Review:

- Workflow-level permissions.
- Job-level permissions.
- Which events can trigger the job.
- Whether pull requests from forks can reach privileged paths.
- Whether untrusted generated content is passed into shell commands.

Least privilege applies even to automatically issued credentials.

---

## Package and Artifact Signing: Ask the Verification Question

Signing is only useful when someone verifies.

A release design should identify:

```text
Producer signs
      ↓
Signature travels with artifact or registry metadata
      ↓
Consumer/verifier resolves trusted identity/key
      ↓
Cryptographic verification
      ↓
Trust policy decides whether signer is acceptable
```

Without a verifier and trust policy, a signature may become decorative metadata.

For key custody, rotation, revocation, and trust-anchor details, use [Signing, Verification, Key Custody, and Tamper Evidence](signing-verification-key-custody-and-tamper-evidence.md).

---

## Supply-Chain Controls Can Become Disproportionate

A five-file teaching repository and a widely consumed package registry release do not necessarily need the same controls.

Consider four dimensions.

### Distribution Model

```text
Local demo
Internal service
Public source repository
Published NuGet package
Container image
Signed enterprise release
```

Wider distribution generally increases the number of consumers relying on the artifact.

### Consequence

A formatting library and a package that mediates administrative execution may justify different release assurance.

### Maintainer Capacity

A complex signing or attestation system that no maintainer understands can create its own operational risk.

### Consumer Expectations

Enterprise consumers may require SBOMs, attestations, signed artifacts, retention, or reproducibility evidence that a small educational audience does not.

Use controls where they answer a real trust question.

Do not add ceremony merely to maximize the number of security features in YAML.

---

## A Proportional Adoption Path

A small .NET repository can improve incrementally.

### Level 1: Make the Build Explicit

```text
Restore
Build
Test
```

Keep the commands runnable locally and in CI.

### Level 2: Harden Repository Automation

Consider:

- Least-privilege workflow permissions.
- Reviewed workflow changes.
- Stable action references.
- Reduced credential persistence.
- Explicit SDK/tool versions where useful.

### Level 3: Make Dependencies Reviewable

Consider:

- Central package management.
- Dependency-update automation.
- Lock files / locked restore where the value justifies maintenance.
- Package-source policy.
- Vulnerability/dependency review.

### Level 4: Validate the Distributed Artifact

For packages or images:

- Pack/build the release form.
- Validate metadata.
- Smoke-test consumer installation/use.
- Preserve version consistency.

### Level 5: Add Release Evidence

When consumers or risk justify it:

- SBOM.
- Checksums.
- Provenance attestations.
- Package/artifact signing.
- Durable release evidence.

### Level 6: Strengthen the Publication Boundary

Consider:

- Protected release environments.
- Narrow publication identity.
- Short-lived credentials.
- Explicit release approvals where appropriate.
- Artifact handoff rather than rebuild.

The levels are not a maturity certification.

They are a way to reason about increasing supply-chain complexity.

---

## Review Workflow Changes Before Auto-Merging Dependency PRs

Automated dependency PRs can change workflow files too.

A GitHub Actions update may change executable code that runs with repository permissions.

That deserves review proportional to the job's authority.

A low-privilege formatting action and a release action with access to publication credentials present different risks.

Do not let the label:

```text
dependabot
```

replace review of:

```text
What executable dependency changed?
What permissions does it run with?
What release notes explain the change?
Does the new commit still match the intended upstream release?
```

Automation reduces maintenance effort.

It does not eliminate human or policy judgment.

---

## Common Failure Modes

### 1. Treating CI as a Security Boundary by Itself

```text
CI passed
```

becomes a proxy for:

```text
Release is trustworthy
```

Those claims are not equivalent.

### 2. Mutable Third-Party Actions in Privileged Jobs

A release workflow relies on moving tags without a documented update/review policy.

### 3. SHA Pinning Without Update Ownership

Actions are pinned once and never reviewed or updated, leaving known defects indefinitely.

### 4. Broad Workflow Permissions

Every job receives write authority even when most jobs only need source read access.

### 5. Persisted Credentials by Habit

Checkout leaves a credential available even though later steps never need git write operations.

### 6. Bot PR Equals Trusted Update

Automated dependency changes merge without understanding the package/action change.

### 7. Floating Dependency Resolution in Release Builds

A release unexpectedly resolves a dependency graph that was not the one reviewed earlier.

### 8. Package Source Ambiguity

The same package ID can be satisfied unexpectedly from a source the maintainer did not intend.

### 9. Build Project, Never Inspect Package

Compilation succeeds while the generated `.nupkg` contains wrong metadata or missing assets.

### 10. SBOM as Certification

An inventory file is described as proving dependency safety or regulatory compliance.

### 11. Attestation as Safety Proof

Verified provenance is treated as proof that source, dependencies, or runtime behavior are safe.

### 12. Signature as Trustworthiness

A package is correctly signed by an expected key but contains vulnerable or malicious code.

### 13. Rebuild Between Validation and Publish

The artifact consumers receive is not necessarily the artifact that passed validation.

### 14. Release Secret Available to Validation Jobs

Compromise of ordinary build steps gains package-publication authority unnecessarily.

### 15. Environment Name Mistaken for Environment Protection

YAML names an environment, but nobody verifies whether protection rules are configured.

### 16. Artifact Upload Mistaken for Archival

Temporary CI artifacts expire before the retention period implied by the project's provenance claims.

### 17. Deterministic Flag Called Reproducible Proof

One build setting is described as establishing independent reproducibility.

### 18. Vulnerability Scanner Called Complete

No known findings is reported as proof that no vulnerabilities exist.

### 19. Workflow Changes Receive Less Review Than Runtime Code

A small YAML modification silently broadens credentials or publication behavior.

---

## Test Supply-Chain Invariants

Repository security controls become more useful when important assumptions are machine-checkable.

Possible checks include:

### Workflow Actions Are Pinned According to Repository Policy

```text
Workflow changed
      ↓
Unapproved mutable action reference found
      ↓
Validation fails
```

### Validation Jobs Stay Read-Only

```text
Pull-request validation job
      ↓
Unexpected write permission added
      ↓
Security validation fails
```

### Restore Uses Reviewed Lock State

```text
Package graph changes
      ↓
Lock file not updated intentionally
      ↓
Locked restore fails
```

### Package Version Matches Release Version

```text
Tag / release version
      ↓
Generated package version
      ↓
Mismatch = fail
```

### Package Metadata Is Present

```text
Generated .nupkg
      ↓
Repository/license/project metadata validation
      ↓
Missing or inconsistent field = fail
```

### Publication Uses the Validated Artifact

```text
Artifact produced by pack job
      ↓
Digest / artifact identity
      ↓
Same artifact consumed by publish job
```

### No Protected Publication on Non-Release Trigger

```text
Pull-request event
      ↓
Package publication path unreachable
```

The exact implementation depends on the repository.

The invariant is more important than the scripting language used to enforce it.

---

## Manual Review Still Matters

Automation is strongest when it turns hidden drift into reviewable evidence.

Some questions remain contextual:

```text
Is this new dependency necessary?
Is this action maintained by the expected project?
Does this major-version update change permissions or behavior?
Does the release process still match consumer expectations?
Is this SBOM format sufficient for downstream users?
Is signing complexity justified by the distribution model?
```

A fully automated pipeline can still automate the wrong policy perfectly.

---

## Working Repository Map

Use these repositories as specimens rather than templates to copy verbatim.

### Learning

- [Learning repository](https://github.com/AsiBackbone/Learning)
- [Learning workflow directory](https://github.com/AsiBackbone/Learning/tree/main/.github/workflows)
- [Learning Security Policy](https://github.com/AsiBackbone/Learning/blob/main/SECURITY.md)

Inspect it for:

```text
SHA-pinned actions
Least-privilege validation permissions
persist-credentials: false
Separate docs build/deploy authority
Sample restore/build/test validation
Pinned DocFX tool manifest
```

### AsiBackbone

- [AsiBackbone repository](https://github.com/AsiBackbone/AsiBackbone)
- [AsiBackbone workflow directory](https://github.com/AsiBackbone/AsiBackbone/tree/main/.github/workflows)
- [Directory.Build.props](https://github.com/AsiBackbone/AsiBackbone/blob/main/Directory.Build.props)
- [Directory.Packages.props](https://github.com/AsiBackbone/AsiBackbone/blob/main/Directory.Packages.props)
- [Dependabot configuration](https://github.com/AsiBackbone/AsiBackbone/blob/main/.github/dependabot.yml)

Inspect it for:

```text
Central package versions
Lock-file generation
Locked restore in release workflows
Deterministic/CI build settings
Source/repository metadata
Package validation
SBOM generation
Build-provenance attestations
Artifact handoff into publication
Narrower release-job authority
```

### NetCoreApplicationTemplate

- [NetCoreApplicationTemplate repository](https://github.com/AsiBackbone/NetCoreApplicationTemplate)
- [NetCoreApplicationTemplate workflow directory](https://github.com/AsiBackbone/NetCoreApplicationTemplate/tree/main/.github/workflows)
- [Dependabot configuration](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/.github/dependabot.yml)

Use it as a second specimen for application-oriented CI and dependency-update practices rather than assuming a package-producing repository and an ASP.NET Core application should have identical release controls.

---

## Review Checklist by Boundary

### Repository

- Are workflow changes reviewed as security-sensitive code?
- Are protected branches and required checks configured where the threat model requires them?
- Can untrusted branches reach privileged workflows?
- Are release triggers narrow and understandable?

### Actions and Tools

- Are external actions referenced according to a deliberate mutability policy?
- Are pins maintained through a reviewable update process?
- Are tool versions explicit enough for the repository's repeatability needs?
- Does every downloaded build tool justify its place in the trusted computing base?

### Workflow Authority

- Are permissions denied or minimized by default?
- Are job-level writes granted only where needed?
- Are checkout credentials persisted only when required?
- Are release identities/secrets isolated from ordinary validation jobs?

### Dependencies

- Are package versions visible and reviewable?
- Is central package management useful for this repository?
- Are lock files/locked restore appropriate to release risk?
- Are package sources explicit enough to avoid surprising resolution?
- Are automated dependency updates reviewed rather than blindly trusted?

### Build

- Is the SDK/toolchain selection deliberate?
- Does CI use the same logical build path contributors can reproduce?
- Are deterministic-build settings described accurately?
- Are caches treated as mutable build inputs?

### Validation

- Are tests run against the built artifact state?
- Is documentation validated if documentation is a shipped artifact?
- Is the packaged/distributed form validated directly?
- Are version and package metadata checked before publication?

### Artifact and Release

- Is the artifact that was validated the artifact that is published?
- Are release credentials scoped to the publication boundary?
- Are publication environments/protection rules verified rather than assumed?
- Is artifact retention sufficient for the evidence claims being made?

### Provenance and Evidence

- Is an SBOM described as inventory rather than certification?
- Are attestations distinguished from package signatures?
- Are checksums distinguished from authenticity?
- Are signatures distinguished from trustworthiness?
- Can consumers verify the provenance or signatures the project claims to provide?
- Is evidence retained long enough to support later investigation?

---

## When a Simpler Repository Is Better

Supply-chain controls can become self-defeating when maintainers cannot explain or operate them.

A small educational repository might reasonably choose:

```text
Read-only CI
Pinned or deliberately managed action references
Explicit restore/build/test
Minimal secrets
No publication credentials
```

without adding:

```text
SBOM generation
Artifact signing
Attestations
Complex environment approvals
Multi-stage release orchestration
```

if it publishes no consequential artifact.

By contrast, a widely consumed package may justify much more evidence and publication hardening.

The decision should follow:

```text
Threat model
Distribution model
Consumer expectations
Release consequence
Maintainer capacity
```

not:

```text
Number of security checkboxes available
```

---

## What These Controls Do Not Guarantee

Even a repository that uses SHA-pinned actions, least-privilege permissions, locked restore, central package management, SBOMs, attestations, package validation, and protected publication still does not automatically guarantee:

- Safe dependencies.
- Vulnerability-free code.
- Uncompromised CI infrastructure.
- Correct build scripts.
- Correct package metadata.
- Complete SBOM coverage.
- Reproducible builds.
- Authorized maintainers.
- Correct signing policy.
- Safe runtime behavior.
- Regulatory compliance.
- Consumer-side verification.
- Absence of insider compromise.

The controls narrow specific risks and improve evidence.

They do not collapse the entire supply chain into one proof of trustworthiness.

---

## Review Questions

You should now be able to answer:

1. Why is software supply-chain integrity a trust-chain problem rather than a CI feature list?
2. Why should workflow YAML receive security-sensitive code review?
3. What security property does SHA pinning strengthen?
4. Why does a SHA pin not prove that action code is safe?
5. Why can `persist-credentials: false` be useful?
6. Why does it not mean a job has no credentials?
7. How do workflow-level and job-level permissions support least privilege?
8. Why should build and publication authority often be separated?
9. What does central package management improve?
10. Why does it not prove dependency safety?
11. What does locked restore protect against?
12. Why is a locked dependency graph not a complete reproducible-build guarantee?
13. What package-source questions belong in dependency trust?
14. Why is a Dependabot PR a proposal rather than an authorization to update?
15. Why should GitHub Actions updates receive review proportional to workflow authority?
16. Why should CI validate the generated package rather than only the project?
17. What does Source Link improve, and what does it not prove?
18. What useful evidence does an SBOM provide?
19. Why is an SBOM not a security certification?
20. What is the difference between provenance, authenticity, and trustworthiness?
21. Why is a build attestation not the same thing as NuGet package signing?
22. What does a checksum establish when the expected digest is trustworthy?
23. Why should release credentials be isolated from ordinary validation jobs?
24. Why does naming a GitHub environment not prove its protection rules are configured?
25. Why should artifact retention match the project's evidence claims?
26. Why is deterministic compilation weaker than independent reproducible-build verification?
27. What does green CI actually prove?
28. Why can vulnerability scanning never prove the absence of all vulnerabilities?
29. How can rebuilding between validation and publication break provenance continuity?
30. When do supply-chain controls become disproportionate for a small teaching repository?

---

## Related Content

- [Security](index.md) — return to the Security learning-area overview.
- [Trust Boundaries and Least Privilege](trust-boundaries-and-least-privilege.md) — apply the same trust-boundary questions to source, build, dependency, and release stages.
- [Signing, Verification, Key Custody, and Tamper Evidence](signing-verification-key-custody-and-tamper-evidence.md) — go deeper on signatures, verification policy, key ownership, hashes, and evidence claims.
- [Replay Protection and Bounded-Use Authority](replay-protection-and-bounded-use.md) — compare release credentials and bounded authority with stateful execution-boundary controls.
- [Contributing](https://github.com/AsiBackbone/Learning/blob/main/CONTRIBUTING.md) — review repository contribution expectations alongside workflow and validation boundaries.
- [Security Policy](https://github.com/AsiBackbone/Learning/blob/main/SECURITY.md) — review the repository's vulnerability-reporting and automation-security scope.

---

## Scope

This walkthrough is educational architecture guidance.

It does not provide:

- A supply-chain security certification.
- A SLSA level or equivalent assurance claim.
- A guarantee that the referenced repositories are free from compromise.
- A complete CI/CD threat model.
- A package-signing implementation.
- A reproducible-build certification.
- A guarantee that an SBOM is complete.
- A guarantee that provenance evidence proves artifact safety.
- A substitute for GitHub, NuGet, cloud, runner, or organization-specific security configuration.

Repository owners remain responsible for their own threat model, branch/rules configuration, dependency sources, credentials, build infrastructure, package-signing decisions, artifact retention, publication policy, and consumer-verification requirements.

---

> **Trust the chain only as strongly as you can explain and verify each boundary.**
