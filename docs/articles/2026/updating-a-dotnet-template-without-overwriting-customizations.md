---
description: Rerunning dotnet new --force over a customized app is not an upgrade. Diff two clean scaffolds, then reconcile that scaffold delta into your code deliberately.
title: How to Update a Scaffolded .NET Application Without Overwriting Your Code
author: Christopher D. Cavell
published: "2026-11-27"
summary: A newer dotnet new template does not update the applications it generated earlier, and regenerating over a customized application with --force can replace intentional changes while leaving files the template no longer emits. Record exactly how the application was generated and which template version it has adopted, regenerate the old and new template versions separately with identical options, review that clean scaffold delta, stage it through Git's own normalization, and reconcile it into the application with written conflict rules. Regenerate lock files rather than hand-merging them, follow EF Core's migration workflow instead of merging snapshots as text, and validate the raw baseline and the integrated application separately.
feed: true
x_post: Don't rerun dotnet new --force over your app. Diff two clean scaffolds, then reconcile that delta.
x_hashtags:
  - DotNet
  - SoftwareArchitecture
x_question: How do you adopt a newer project template?
---

# How to Update a Scaffolded .NET Application Without Overwriting Your Code

*Refreshing a template-generated application from two clean scaffolds*

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** Working familiarity with `dotnet new`, Git branches and conflict resolution, and NuGet package references. Git worktrees, `.gitattributes`, lock files, and EF Core migrations are introduced where they are needed. No AsiBackbone package, NetCoreApplicationTemplate, or prior Learning material is required.

**Sample scope:** The Larkspur template, its two releases, and the Orders application are fictional and deliberately small: one web project with an `.editorconfig`, central package versions, a lock file, and four relevant source and configuration files. Its `.gitignore` and `.editorconfig` are minimal illustrations, not files to copy into a real project. The commands are PowerShell examples for Windows; Git and .NET CLI commands are the same on other platforms, and the one Windows-specific tool (`robocopy`) is called out where it appears. Branch and tag names are this article's choices, not requirements. ReferenceApplication appears near the end as a bounded pointer to one complete runbook, not as proof that its arrangement suits every repository.

**Verification:** The outcomes reported in this article (the refusal and the overwrite from `dotnet new`, the option and reproducibility checks, the patch failure, the staged delta, the merge and cherry-pick conflicts, the `git apply --3way` refusal, the lock-file error, the build errors, and the passing endpoints) were observed by running the example with .NET SDK 10.0.404, Git 2.56.1 for Windows, and PowerShell 7.7.0 on November 27, 2026. The PowerShell commands were run as shown, phase 1 in one session and phase 2 in a new one, against a clone of a local bare repository standing in for the hosting service, with `$SourceArguments` pointing at a local folder feed and the placeholders filled in; the baseline pull request's squash merge was simulated on that bare repository. They are observations of this example in that environment, not guarantees about other templates or configurations. The [appendix](#appendix-reproducing-the-example) lists the template files and environment. Line-ending behavior depends on Git configuration, so the article states which setting produced each result. External sources were last verified on November 27, 2026.

**License:** Original prose is licensed under [Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/), and original code examples are additionally offered under the [MIT License](https://opensource.org/license/mit), as described in the repository's [licensing terms](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md). Third-party material, where identified, remains under its original license. Quoted tool output, short documentation quotations, and product names are included for technical explanation and identification and remain subject to their respective owners' rights. The appendix's template package project is adapted from Microsoft's [Create a template package for dotnet new](https://learn.microsoft.com/dotnet/core/tutorials/cli-templates-create-template-package) tutorial and is not claimed as original code. It remains under the [MIT License](https://github.com/dotnet/docs/blob/main/LICENSE-CODE) of Microsoft's .NET documentation code, whose full notice appears in the [appendix](#third-party-notice).

**What this article covers:** what rerunning `dotnet new` over an existing application did in a small example, and why a green build afterward proves little; recording template provenance and the adopted template version before anything is regenerated; generating old and new scaffolds separately and checking options and reproducibility; reading the clean scaffold delta as added, changed, and deleted files; why a patch that passes `git apply --check` can still fail `git apply --index` under `.gitattributes` normalization, and staging by mirroring into a guarded, disposable worktree as one way around it; choosing between merge, cherry-pick, patch, and manual reconciliation; regenerating lock files and following EF Core's migration workflow; validating the raw baseline and the integrated application separately, with a stop boundary; and when the whole procedure costs more than it returns.

**In plain terms:** a builder gives you house plans, and you remodel the kitchen. Two years later the builder publishes revised plans that move a wall and remove a closet. Rebuilding from the revised plans would add the new wall but replace the kitchen you customized. Comparing the old plans with the new ones tells you exactly what the builder changed, so you can move that one wall around your kitchen and decide for yourself whether the closet you turned into a pantry goes.

Consider a team that generated its Orders service from an internal ASP.NET Core template, Larkspur.Templates 1.4.0. Over the following months the team added order endpoints, extended the generated `/status` endpoint to report the build version, and added an `Orders` section to `appsettings.json`. Larkspur 1.5.0 is now out with a newer OpenAPI package, health checks, and a new `.editorconfig`.

A developer installs 1.5.0 and runs the template again in the repository root. `dotnet new` refuses and lists eight files it would overwrite, so the developer adds `--force`. The command succeeds, the project builds without warnings, and the diff looks like an upgrade.

It is not. In this run, `--force` rewrote `Program.cs` from the new template, which silently removed `app.MapOrderEndpoints()` and the application's `/status` mapping. The project still compiles, because the endpoint classes are still there; nothing calls them. It reset `appsettings.json` to the template's version, deleting the `Orders` section, although Larkspur had not changed that file at all. Larkspur 1.5.0 also changed the default of its `EnableOpenApi` option from `true` to `false`, and the developer did not pass it, so OpenAPI disappeared from the project, the package version file, and the code. `StatusEndpoints.cs`, which 1.5.0 no longer generates, stayed on disk. The ordinary build that followed also rewrote `packages.lock.json` to match. The forced generation and the build that followed both succeeded. The `/orders/{id}` route was simply gone.

> **Installing a newer template changes what `dotnet new` generates next; it does not update applications generated earlier. Carry a template's changes into a customized application by comparing clean old and new scaffolds and reconciling that delta deliberately, not by regenerating over the application.**

---

## Why This Recurs

Templates are generators. Microsoft's [.NET templates for authors](https://learn.microsoft.com/dotnet/core/tools/custom-templates) documentation describes packaging, installing, and running them; once output is written, the files belong to the repository that received them. A template release can change files, defaults, options, and dependency versions, and it can stop generating a file. None of that reaches an existing application unless someone carries it there.

The obvious tool for carrying it, `dotnet new` itself, is designed to create content, not to merge it. Its [reference page](https://learn.microsoft.com/dotnet/core/tools/dotnet-new) on Microsoft Learn describes `--force` as forcing content to be generated "even if it would change existing files." That is a reasonable behavior for a generator and the wrong one for an upgrade. Long-lived applications encounter this repeatedly as organization templates evolve, security defaults change, and package baselines move.

### How this differs from the LTS migration article

[You Don't Need to Roll Back What You Never Merged](you-dont-need-to-roll-back-what-you-never-merged.md) covers the larger decision: keeping an upstream baseline and an isolated candidate so that a .NET LTS migration can be qualified, adopted, or discarded without touching `main`. It explains the three states (baseline A, the application delta, baseline B) and the adoption criteria. This article does not repeat that model. It covers the hands-on mechanics of one refresh at the file level: what to record, how to produce a trustworthy old-to-new delta, the line-ending and staging traps on the way, how to resolve the add, change, and delete cases, and what to regenerate rather than merge. Use the two together for a platform migration; use this one alone for an ordinary same-generation template refresh.

A template refresh is also not a .NET runtime upgrade and not a deployment rollback. A clean scaffold delta tells you what the template authors changed. It does not tell you whether your application is compatible with a new runtime, and it does nothing for a release that is already in production.

---

## 1. What In-Place Regeneration Did

Run against the Orders application with the 1.5.0 template installed, `dotnet new` without `--force` did not change anything. It listed every file it would overwrite and exited with code 73:

```text
Creating this template will make changes to existing files:
  Overwrite   ./.gitattributes
  Overwrite   ./.gitignore
  Overwrite   ./Directory.Build.props
  Overwrite   ./Directory.Packages.props
  Overwrite   ./Orders.Api.slnx
  Overwrite   ./Orders.Api/Orders.Api.csproj
  Overwrite   ./Orders.Api/Program.cs
  Overwrite   ./Orders.Api/appsettings.json
```

That refusal is useful, and it is the right place to stop. With `--force` added, the same command exited with code 0. Git reported this working-tree change immediately afterward, before any build:

```text
 M Directory.Packages.props
 M Orders.Api/Orders.Api.csproj
 M Orders.Api/Program.cs
 M Orders.Api/appsettings.json
?? .editorconfig
```

Read that list against what Larkspur actually changed (the clean delta appears in [section 4](#4-review-the-scaffold-delta)), and four distinct problems appear:

| Observed | Why it matters |
| --- | --- |
| `appsettings.json` was replaced although the template did not change it | Forced generation works from the new template's output, not from the old-to-new delta, so a customized file the template emits is at risk even when it did not change between releases. |
| `Program.cs` lost the application's endpoint mappings | The template's version replaced the file; regeneration has no notion of whose lines are whose. |
| OpenAPI was removed from three files | A template default changed between releases, and the command did not pass the option explicitly. |
| `StatusEndpoints.cs` remained | 1.5.0 no longer emits it, and in this example generation added and replaced files without removing it. No deletion appears anywhere in the diff. |

Identical files, such as `.gitattributes` here, were overwritten with the same bytes and show no diff, which makes the change look smaller than the command's reach. A subsequent ordinary build passed and also rewrote `packages.lock.json`, so a build-only CI gate would have been green.

This is one template on one SDK. Templates can contain conditional content and post-actions that run scripts or restore packages, so the exact effect of `--force` varies from template to template. What does not vary is that it is a generation option, not a reconciliation mechanism.

---

## 2. Record Provenance and the Adopted Version

A trustworthy automated comparison needs an old scaffold that can be reproduced under the recorded inputs and environment assumptions, and that requires knowing exactly how the application was generated. Record it at generation time, in the repository, where it remains available after the original maintainers are no longer involved.

| Record | Why |
| --- | --- |
| Template package ID, exact version, source feed, and a digest per release | `Larkspur.Templates@1.4.0` from a named feed is far more reproducible than "the Larkspur template", but an identifier and a feed do not by themselves guarantee that the package content is unchanged or still available. For a private or locally retained package, keep the exact `.nupkg` of each release in an artifact store with suitable immutability or retention controls, and record a named digest of its bytes (for example, its SHA-256 digest). |
| Template short name | Packages can contain several templates. |
| Every option that can affect the generated output, passed explicitly even when it equals the default | A default can change between releases, as `EnableOpenApi` did. |
| The project name (`--name`) and any output layout assumptions | `sourceName` substitution changes file names, namespaces, and paths. |
| SDK version used to generate (and `global.json`, if any) | The template engine ships with the SDK; generation behavior can differ across SDKs. |
| The template version the application has **adopted** | See below. |
| Relevant environment assumptions | Git line-ending configuration, operating system, and anything a post-action depends on. |

A small manifest committed beside the application is the core record; this article's examples read it from `template-manifest.json` at the repository root:

```json
{
  "templatePackage": "Larkspur.Templates",
  "templateSource": "<feed URL, or the folder path of a local feed>",
  "templatePackageDigests": {
    "1.4.0": "sha256:<digest of Larkspur.Templates.1.4.0.nupkg>",
    "1.5.0": "sha256:<digest of Larkspur.Templates.1.5.0.nupkg>"
  },
  "shortName": "larkspur-api",
  "arguments": [ "--name", "Orders.Api", "--EnableOpenApi", "true" ],
  "sdk": "10.0.404",
  "initialTemplateVersion": "1.4.0",
  "adoptedVersion": "1.4.0",
  "adoptedBaselineTag": "template-v1.4.0",
  "adoptedBaselineCommit": "<full commit ID of the template-v1.4.0 baseline commit>"
}
```

This is a schema example, and the angle-bracket values are placeholders; replace them with real values before relying on the checks in [section 6](#6-choose-how-to-integrate). The manifest is the reviewed, human-readable record. The commands in this article read only its `adopted*` fields and, in the higher-assurance check below, its digests; section 3 sets the other values in the script itself. Copying them is manual, so review that the script values match the manifest as part of the pull request. `initialTemplateVersion` records the release that first created the application and stays historical. `adoptedVersion` and the two fields after it record the latest template baseline whose delta the application has actually integrated, and they advance with each successful refresh. Add a digest for each release you refresh to.

The article's script installs releases by ID and version from a feed, because that is how the example was run, and it does not check digests. A higher-assurance runbook verifies the retained file first and installs from that file, so no fresh package lookup happens after verification:

```powershell
$Manifest = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'template-manifest.json') -Raw -ErrorAction Stop |
  ConvertFrom-Json
$Version = '1.5.0'
$Package = '<path to the retained Larkspur.Templates.1.5.0.nupkg>'
$Hive = Join-Path ([IO.Path]::GetTempPath()) "larkspur-verified-$([Guid]::NewGuid().ToString('N'))"

$RecordedDigest = [string] $Manifest.templatePackageDigests.$Version
if ($RecordedDigest -notmatch '^sha256:([0-9a-fA-F]{64})$') {
  throw "template-manifest.json records no valid SHA-256 digest for $Version."
}
$Actual = (Get-FileHash -LiteralPath $Package -Algorithm SHA256 -ErrorAction Stop).Hash
if (-not $Actual.Equals($Matches[1], [StringComparison]::OrdinalIgnoreCase)) {
  throw "$Package does not match its recorded digest."
}
dotnet new install $Package --debug:custom-hive $Hive
Assert-ExitCode "Installing the verified $Version package"
```

It uses `$RepositoryRoot` and `Assert-ExitCode` from section 3. In the example, reading the digest from the manifest, the check, and an install from the `.nupkg` file into a separate template store all succeeded.

Then keep the raw scaffold itself where it cannot drift: the example stores it on a `template-baseline` branch whose only commits are unmodified generator output, and marks each one with an annotated tag such as `template-v1.4.0`. Protect the baseline branch, require linear history on it, and, where the hosting service supports it, restrict creating, moving, and deleting the tags. Recording the full commit ID beside the tag keeps the reference verifiable even if a tag is moved. A generator that can always reproduce the output on demand works too; the [LTS article's mechanism table](you-dont-need-to-roll-back-what-you-never-merged.md#choosing-the-baseline-mechanism) compares the options. In the example, `main` was created from the baseline commit, so the two branches share real Git ancestry.

### The latest raw baseline is not necessarily the adopted one

Two states are easy to conflate. The newest commit on the baseline branch records what the template most recently generated. The adopted version records what the application has actually integrated. They differ whenever a baseline refresh has merged but its integration into the application has not, or was abandoned. Advance `adoptedVersion`, `adoptedBaselineTag`, and `adoptedBaselineCommit` in the same pull request that integrates the refresh into the application, never before it. The next integration must then bring in everything from the adopted tag to the new one, not only the newest baseline commit, or the application silently skips a release.

---

## 3. Regenerate Both Versions Independently

Generate the old and new scaffolds into a new, empty scratch folder outside the repository, with identical arguments. Because that folder is outside the repository, the repository's own `global.json` does not apply there, so pin the recorded SDK in the scratch folder too. `rollForward: disable` is deliberate: the procedure should stop if the recorded SDK is not installed rather than quietly test reproducibility with a different template engine. Relax it only as a recorded policy decision.

Start from a clean checkout of the application repository. The procedure runs in two phases. Phase 1 is the commands in this section and the first two stages of [section 5](#5-line-endings-patches-and-staging), up to pushing the baseline refresh for review; run them in one PowerShell session, because later blocks use `$RepositoryRoot`, `$Old`, `$New`, `$Scratch`, `$ScaffoldMarker`, and `Assert-ExitCode` from this one. Phase 2 runs after the baseline pull request merges, possibly in a new session, and section 5 shows how to re-create the values it needs.

`$Old` is the version at the tip of the baseline branch, which section 5 verifies. In this first refresh it is also the adopted version; when the two differ, use `$Old` for baseline validation and the adopted tag for integration ([section 6](#6-choose-how-to-integrate)).

> **Support boundary:** the isolated template stores below make the example easy to reproduce, but they depend on an undocumented diagnostic option. A durable team runbook should either pin and test that behavior deliberately or use the documented install-generate-install sequence described next.

The example installs each release into its own temporary template store with `--debug:custom-hive`, so the versions do not replace each other and the machine's installed templates are untouched. That option is a template-engine diagnostic switch that does not appear in the `dotnet new` reference; it worked as shown on SDK 10.0.404, but treat it as unsupported. The documented alternative is to run `dotnet new install` for each version in turn, using the [`<package>@<version>` syntax](https://learn.microsoft.com/dotnet/core/tools/dotnet-new-install), and to generate from each before installing the next. In the example, installing 1.5.0 replaced an installed 1.4.0 without a separate uninstall, and the machine was left with 1.5.0 installed.

```powershell
# The documented default is $false. Reset it in case a profile changed it, because this
# script interprets native exit codes itself. The assignment applies to the current scope.
$PreviousNativePreference = $PSNativeCommandUseErrorActionPreference
$PSNativeCommandUseErrorActionPreference = $false

$RepositoryRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) { throw 'Start from a checkout of the application repository.' }
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot -ErrorAction Stop).Path

$Old = '1.4.0'   # the version at the tip of template-baseline
$New = '1.5.0'
$Sdk = '10.0.404'
$Arguments = @('--name', 'Orders.Api', '--EnableOpenApi', 'true')
$ScaffoldMarker = 'Orders.Api.slnx'   # a file every clean scaffold of this application contains
$SourceArguments = @()                # or @('--add-source', '<feed>') if the feed is not configured
$Scratch = Join-Path ([IO.Path]::GetTempPath()) "larkspur-refresh-$New-$([Guid]::NewGuid().ToString('N'))"

New-Item -ItemType Directory -Path $Scratch -ErrorAction Stop | Out-Null
Set-Location -LiteralPath $Scratch -ErrorAction Stop
"Scratch folder: $Scratch"   # note it; phase 2 needs this path

@{ sdk = @{ version = $Sdk; rollForward = 'disable' } } |
  ConvertTo-Json -Depth 3 | Set-Content global.json -ErrorAction Stop
$ActualSdk = dotnet --version
if ($LASTEXITCODE -ne 0 -or $ActualSdk -ne $Sdk) { throw "Expected .NET SDK $Sdk, found '$ActualSdk'." }

function Assert-ExitCode([string] $Step) {
  if ($LASTEXITCODE -ne 0) { throw "$Step failed with exit code $LASTEXITCODE." }
}

foreach ($Version in $Old, $New) {
  $Hive = Join-Path $Scratch "hive-$Version"
  dotnet new install "Larkspur.Templates@$Version" @SourceArguments --debug:custom-hive $Hive
  Assert-ExitCode "Installing $Version"
  dotnet new larkspur-api --help --debug:custom-hive $Hive > "options-$Version.txt"
  Assert-ExitCode "Reading the $Version options"
}
```

Set `$SourceArguments` when the template comes from a feed that is not already configured. A unique scratch folder per run means a failed earlier attempt cannot leave stale template stores or scaffolds behind. Resetting `$PSNativeCommandUseErrorActionPreference` matters when a profile has enabled it, particularly with `$ErrorActionPreference` set to `Stop`; PowerShell would then turn an expected nonzero exit code, such as 1 from `git diff` when differences exist, into an error before the script could interpret it. The assignment stays in effect for the session, so each phase saves the previous value and restores it at its end; if a phase stops early, run `$PSNativeCommandUseErrorActionPreference = $PreviousNativePreference` yourself.

### Compare the options before trusting the defaults

```powershell
git -c core.autocrlf=false diff --no-index -- "options-$Old.txt" "options-$New.txt"
```

This is a review step, so its exit code of 1 when the lists differ is expected. For Larkspur it printed one change. (The `-E` alias is generated by the template engine; the template declares only `EnableOpenApi`.)

```text
 Template options:
   -E, --EnableOpenApi  Add an OpenAPI document endpoint in Development.
                        Type: bool
-                       Default: true
+                       Default: false
```

Had both scaffolds been generated with defaults, the delta would have "removed OpenAPI": an artifact of changed inputs, not a template change anyone decided to adopt. Decide the value the application uses, pass it explicitly to both generations, and record it in the manifest. A new option deserves the same decision.

The help comparison detects options that were added, removed, or given a different displayed default. It does not prove that an unchanged option still has the same effect, which is one more reason to review the scaffold delta itself.

### Generate each version twice

```powershell
# Each entry is (template version, output folder).
foreach ($Run in @($Old, 'old'), @($Old, 'old-repeat'), @($New, 'new'), @($New, 'new-repeat')) {
  dotnet new larkspur-api @Arguments --output $Run[1] --debug:custom-hive (Join-Path $Scratch "hive-$($Run[0])")
  Assert-ExitCode "Generating $($Run[1])"
}

foreach ($Pair in @('old', 'old-repeat'), @('new', 'new-repeat')) {
  git -c core.autocrlf=false diff --no-index --quiet -- $Pair[0] $Pair[1]
  $DiffExit = $LASTEXITCODE
  if ($DiffExit -eq 1) { throw "$($Pair[0]) differs between two generations." }
  if ($DiffExit -ne 0) { throw "git diff failed with exit code $DiffExit." }
}
```

`git diff --no-index` exits with 0 for no differences, 1 for differences, and anything else for a failure, so the script checks the code rather than whether anything was printed. `-c core.autocrlf=false` disables that configuration-based CRLF conversion for these comparisons, so a generator that varied its line endings would still be caught. Repository attributes do not govern the scratch paths, but other explicitly configured diff or text-conversion settings would need separate consideration. This remains a comparison of file paths and Git-compared content; a requirement for byte-for-byte identity would call for comparing per-file hashes instead. Both Larkspur versions produced identical output twice. That is evidence of reproducibility for these inputs in this environment, not proof that the generator is deterministic under all conditions. When two generations differ, the generator emits varying content such as GUIDs, ports, secrets, or timestamps. That noise would appear in every delta and could hide real changes. Handle it with a narrow, documented rule for those specific files or values, or by passing the value as an explicit option if the template allows it, never by ignoring files broadly.

### Tell nondeterminism from a corrupted baseline

Two different checks answer two different questions:

| Check | Failure means |
| --- | --- |
| Each version, generated twice, has the same file paths and Git-compared content | The *generator* did not reproduce its output for these inputs in this environment. |
| A fresh old scaffold matches the stored baseline | With reproducible output in this run: the stored baseline, the package content, the recorded generation inputs, the SDK or environment assumptions, the normalization rules, or the tag and branch references do not match the conditions that produced the fresh scaffold. A hand edit to the baseline is one possible cause; investigate rather than assume it. |

Run the first before the second, so a baseline mismatch is not blamed on the generator or the reverse. The second check happens inside Git in [section 5](#5-line-endings-patches-and-staging), because comparing raw bytes on disk would also report line-ending differences that Git does not store.

---

## 4. Review the Scaffold Delta

Diffing the two clean folders already shows what Larkspur changed:

```powershell
git -c core.autocrlf=false diff --no-index --stat -- old new
```

```text
 /dev/null => new/.editorconfig                 |  6 ++++++
 {old => new}/Directory.Packages.props          |  2 +-
 {old => new}/Orders.Api/Program.cs             |  5 ++---
 old/Orders.Api/StatusEndpoints.cs => /dev/null | 10 ----------
 4 files changed, 9 insertions(+), 14 deletions(-)
```

That is the complete observed file delta between the two clean scaffolds for these inputs (again a review step, where exit code 1 means differences were found), with the customized application nowhere in the comparison. Read each entry against the application before touching it:

| Template change | Application state | Defensible resolution |
| --- | --- | --- |
| **Added** `.editorconfig` | Absent | Adopt. Then check that the new rules do not mass-reformat application code in the same change; if they would, land formatting separately. |
| **Changed** `Directory.Packages.props`: OpenAPI 10.0.14 → 10.0.15 | Unchanged since generation | Review the version change, then adopt it and regenerate the lock file ([section 7](#7-dependencies-lock-files-and-ef-core-artifacts)). |
| **Changed** `Program.cs`: health checks added, `/status` mapping and its `using` removed | Customized: order endpoints mapped, `/status` kept | Reconcile by hand: keep the application's mappings and adopt health checks. |
| **Deleted** `StatusEndpoints.cs` | Customized: reports the build version | Decide ownership. The template has replaced `/status` with `/healthz`; the application has consumers of `/status`. |
| *Not in the delta:* `appsettings.json` and everything the application added | Customized | Leave untouched. A file the template did not change has nothing to reconcile. |

A file the newer template stops emitting is the case ordinary in-place regeneration may fail to surface: the previously generated file may remain in the application with no deletion recorded. It is also the case that needs a human decision. In the example, external monitors call `/status` and read its `build` field, so the team keeps `StatusEndpoints.cs` as application-owned code, adopts `/healthz` alongside it, and opens a follow-up to move monitors and retire `/status` later. Deleting the file because the template did would have been equally defensible if nothing depended on it. What is not defensible is not noticing.

If `.gitattributes` itself appears in the delta, review it first and separately: it can change how Git normalizes the files it matches.

---

## 5. Line Endings, Patches, and Staging

The `git diff --no-index` output is a review aid. Turning it into a commit on the baseline branch is where line endings can bite. If your repository does not enforce working-tree line endings through `.gitattributes`, or you never build a patch from scaffold folders on disk, you may not encounter the problem this section demonstrates.

Larkspur, like many templates, ships a `.gitattributes` that checks C#, project, and JSON files out with CRLF line endings while Git stores them with LF in the index:

```text
i/lf    w/crlf  attr/text eol=crlf      Orders.Api/Program.cs
```

The template writes the files with CRLF, and Git records them with LF. That is normal and correct. It also means the bytes on disk and the content in the index are different representations of the same file.

### Why a patch can pass `--check` and still fail `--index`

The tempting route is to save the folder diff as a patch and apply it to the baseline. The scratch folder is outside the repository, so the repository's `.gitattributes` rules do not govern this `git diff --no-index` comparison of two filesystem paths, although other Git configuration still can. In the recorded runs, `core.autocrlf` decided whether the patch carried carriage returns. With `core.autocrlf=false`, `git diff --no-index` wrote the generated CRLF bytes into the patch, 61 lines carrying a carriage return. Applied in a worktree of the baseline branch:

| Command | Result with the CRLF patch |
| --- | --- |
| `git apply --check -p2 old-to-new.patch` | Passed: the context matches the CRLF working tree. |
| `git apply --check --cached -p2 old-to-new.patch` | Failed: `patch does not apply`, because the index holds LF. |
| `git apply --index -p2 old-to-new.patch` | Failed for the same reason; nothing was applied. |
| `git apply --index --ignore-whitespace -p2 old-to-new.patch` | "Succeeded", but `git ls-files --eol` then reported `i/mixed` and `i/crlf` for three files: carriage returns were written into the index. |

[`git apply`](https://git-scm.com/docs/git-apply) is behaving as documented: `--index` applies to both the index and the working tree, so the context has to match both. `--ignore-whitespace` is a blunt instrument here; it hides the mismatch instead of resolving it. With `core.autocrlf=true`, the system-level setting on the test machine, the same `git diff --no-index` produced an LF-only patch, and every command above succeeded. The same recipe therefore works for one developer and fails for another. That is a reason to understand the mechanism, not a reason to distrust patches in general.

### Mirroring into a disposable worktree: one demonstrated solution

An approach that sidesteps patch context entirely is to make a separate worktree of the baseline branch match a scaffold folder exactly and let `git add` do the comparison. Git then normalizes every file through the repository's [`.gitattributes`](https://git-scm.com/docs/gitattributes) rules, detects additions and deletions itself, and needs no `-p` path handling. In the example it produced the same four-file delta with `core.autocrlf` set to `true` and to `false`. The procedure has three stages: create and verify the worktree, mirror and stage the scaffolds, then merge, tag, and clean up.

> **Destructive command:** `robocopy /MIR` (and `rsync --delete`) removes every destination file that is absent from the source, including ignored files such as `bin/` and `obj/`. Run it only in a newly created, verified, disposable [`git worktree`](https://git-scm.com/docs/git-worktree) of the baseline branch. Never point it at the application checkout or at any worktree that holds work you want to keep.

#### Create and verify the disposable worktree

The first block returns to the application checkout, refuses to continue if it has uncommitted changes, and brings the local baseline branch and tags up to date. It assumes the remote is named `origin`; check with `git remote -v`. In a repository without a remote, omit this fetch and adapt the push and post-merge fetch in the last stage. The refspec `template-baseline:template-baseline` deliberately updates the *local* branch, not only its `origin/template-baseline` remote-tracking reference: it creates or fast-forwards it, which a plain `git fetch origin template-baseline` would not do, and refuses an update that is not a fast-forward. It also requires that `template-baseline` is not checked out in any worktree; the disposable worktree checks out the refresh branch instead. If Git refuses the fetch, stop and inspect rather than forcing it. The block then checks that the baseline tip is the expected tag and that no refresh branch or worktree folder is left over from an earlier attempt, treating any `git show-ref` result other than "found" (0) or "not found" (1) as a failure. If `git worktree add` reports a stale registration for a folder that no longer exists, confirm the folder is gone and run `git worktree prune`.

```powershell
Set-Location -LiteralPath $RepositoryRoot -ErrorAction Stop
$CheckoutStatus = git status --porcelain=v1
Assert-ExitCode 'Reading the application checkout status'
if ($CheckoutStatus) { throw 'The application checkout has uncommitted changes.' }

git fetch origin template-baseline:template-baseline --tags
Assert-ExitCode 'Fetching the baseline branch and tags'

$BaselineCommit = git rev-parse --verify --quiet "template-v$Old^{commit}"
Assert-ExitCode "Resolving template-v$Old"
$BaselineTip = git rev-parse --verify --quiet template-baseline
Assert-ExitCode 'Resolving template-baseline'
if ($BaselineTip -ne $BaselineCommit) {
  throw "template-baseline is not at template-v$Old. Investigate before continuing."
}

$RefreshBranch = "template-refresh-$New"
$Worktree = Join-Path (Split-Path $RepositoryRoot) "orders-template-$New"
git show-ref --verify --quiet "refs/heads/$RefreshBranch"
$RefExit = $LASTEXITCODE
if ($RefExit -eq 0) { throw "Branch $RefreshBranch already exists from an earlier attempt." }
if ($RefExit -ne 1) { throw "git show-ref failed with exit code $RefExit." }
if (Test-Path -LiteralPath $Worktree) { throw "$Worktree already exists." }
git worktree add $Worktree -b $RefreshBranch template-baseline
Assert-ExitCode 'Creating the refresh worktree'
$Worktree = (Resolve-Path -LiteralPath $Worktree -ErrorAction Stop).Path
Set-Location -LiteralPath $Worktree -ErrorAction Stop
```

#### Mirror and stage the scaffolds

The function below enforces the destructive-command boundary rather than trusting the reader to. It refuses unless the source is a folder inside the scratch folder that contains the scaffold's marker file (`Orders.Api.slnx` here), the current directory is the root of the refresh worktree, the branch is the refresh branch, and `git status --porcelain=v1 --ignored` reports nothing, not even ignored files. It takes all of these as parameters, resolves every path itself, and compares paths case-insensitively, as ordinary Windows file systems do, so it does not depend on script variables. It excludes `.git`, which in a linked worktree is a file pointing at the repository, and junction points, and it checks the exit status of the Git and `robocopy` operations its safety depends on. The source checks reject an obviously wrong path, such as the application checkout, a template store, or a subfolder of a scaffold; they do not prove that a folder is the *expected* scaffold. That is what the reproducibility and old-baseline comparisons are for.

```powershell
function Update-IndexFromScaffold {
  param(
    [Parameter(Mandatory)] [string] $Source,
    [Parameter(Mandatory)] [string] $ScratchRoot,
    [Parameter(Mandatory)] [string] $ScaffoldMarker,
    [Parameter(Mandatory)] [string] $Worktree,
    [Parameter(Mandatory)] [string] $RefreshBranch)

  $Source = (Resolve-Path -LiteralPath $Source -ErrorAction Stop).Path
  $ScratchRoot = (Resolve-Path -LiteralPath $ScratchRoot -ErrorAction Stop).Path
  $Worktree = (Resolve-Path -LiteralPath $Worktree -ErrorAction Stop).Path
  $Prefix = $ScratchRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
  if (-not $Source.StartsWith($Prefix, [StringComparison]::OrdinalIgnoreCase) -or
      -not (Test-Path -LiteralPath (Join-Path $Source $ScaffoldMarker) -PathType Leaf)) {
    throw "Refusing to mirror: $Source is not a scaffold folder inside $ScratchRoot."
  }

  $Top = git rev-parse --show-toplevel
  Assert-ExitCode 'Locating the worktree root'
  $Top = (Resolve-Path -LiteralPath $Top -ErrorAction Stop).Path
  $Here = (Get-Location).Path
  $Branch = git branch --show-current
  Assert-ExitCode 'Reading the current branch'
  $Status = git status --porcelain=v1 --ignored
  Assert-ExitCode 'Reading worktree status'
  if (-not $Top.Equals($Worktree, [StringComparison]::OrdinalIgnoreCase) -or
      -not $Here.Equals($Worktree, [StringComparison]::OrdinalIgnoreCase) -or
      $Branch -ne $RefreshBranch -or $Status) {
    throw 'Refusing to mirror: not at the root of the clean refresh worktree.'
  }

  robocopy $Source $Worktree /MIR /XD .git /XF .git /XJ /R:2 /W:1 /NFL /NDL /NJH /NJS /NP *> $null
  $RobocopyExit = $LASTEXITCODE
  if ($RobocopyExit -ge 8) { throw "robocopy reported a failure (exit code $RobocopyExit)." }
  git add --all
  Assert-ExitCode 'Staging the scaffold'
  git add --renormalize .
  Assert-ExitCode 'Renormalizing tracked files'
}
```

`robocopy /MIR` is the Windows tool for mirroring a folder. Its exit codes below 8 mean that no copy failure was reported, while still describing which differences it found; mirroring the new scaffold returned 3 in the example, meaning files were copied and extra files removed. Its output is redirected rather than piped, and the exit code is captured on the next line, so nothing can replace `$LASTEXITCODE` before it is read. On Linux or macOS, `rsync --archive --delete --exclude=.git <source>/ ./` serves the same mirroring purpose, but the guards above are PowerShell and would need equivalent checks written around it; neither was run for this article. [`git add --renormalize`](https://git-scm.com/docs/git-add) reapplies Git's clean conversion to tracked files under the current attributes, so the staged comparison reflects the active `.gitattributes` policy. Git may print "LF will be replaced by CRLF" warnings; they are expected. In the example, the guard refused to run in a worktree with staged changes, in one holding an ignored `bin/` file, from a subdirectory, from the application checkout, and with a source that was missing, was the application checkout, was the scratch folder itself, was a template store, or was a subfolder of a scaffold.

This sample assumes a template that emits ordinary files and folders. A template that generates symbolic links, junctions, or nested repositories needs its own review before anything is mirrored.

First prove the baseline is what the old release generates, then stage the new release:

```powershell
$Mirror = @{
  ScratchRoot = $Scratch; ScaffoldMarker = $ScaffoldMarker
  Worktree = $Worktree; RefreshBranch = $RefreshBranch
}

Update-IndexFromScaffold "$Scratch\old" @Mirror
git diff --cached --quiet
$DiffExit = $LASTEXITCODE
if ($DiffExit -eq 1) {
  git diff --cached --stat
  throw "template-baseline does not match a fresh $Old scaffold."
}
if ($DiffExit -ne 0) { throw "git diff failed with exit code $DiffExit." }

Update-IndexFromScaffold "$Scratch\new" @Mirror
git diff --cached --check
Assert-ExitCode 'Checking whitespace'
git diff --cached --stat
Assert-ExitCode 'Summarizing the staged delta'
```

In the example, mirroring the old scaffold staged nothing and left the worktree clean, so the guard allowed the second call. After a deliberate hand edit to `appsettings.json` was committed on a copy of the baseline, the same check failed and named that file: drift in content is caught, while line-ending-only differences, which Git does not store, are not reported. Then the new scaffold staged exactly the four files from [section 4](#4-review-the-scaffold-delta).

Normalization is a feature here and also a limit. With the repository's current attributes and clean filters, "nothing staged" shows that the stored baseline and the fresh scaffold normalize to the same index content. It is not a complete filesystem comparison: `git add --all` does not stage ignored files, so a generated file matching `.gitignore` is outside the check, and it does not establish byte-for-byte identity on disk. If a template intentionally generates ignored files that matter, validate those separately. Reproducibility of the generator is what the repeated generations in [section 3](#3-regenerate-both-versions-independently) are for.

#### Merge, tag, and clean up

Commit the staged scaffold, push the refresh branch, and open a pull request into `template-baseline` that contains nothing else. That ends phase 1; restore the saved preference with `$PSNativeCommandUseErrorActionPreference = $PreviousNativePreference`. Merge the pull request without a merge commit (squash, rebase, or fast-forward) so the baseline history stays linear. With a squash merge, most hosting services take the resulting commit message from the pull request by default, so name the release in the pull request title.

> **Stop here** until the baseline pull request has merged. Phase 2 deletes the refresh branch and the scratch folder.

Phase 2 must run in the same clone as phase 1, while the local refresh branch and its worktree folder still exist, because it compares against that branch and removes that worktree. It may run in a new PowerShell session. If it does, re-create the values it needs first; the scratch folder path is the one phase 1 printed:

```powershell
$PreviousNativePreference = $PSNativeCommandUseErrorActionPreference
$PSNativeCommandUseErrorActionPreference = $false
function Assert-ExitCode([string] $Step) {
  if ($LASTEXITCODE -ne 0) { throw "$Step failed with exit code $LASTEXITCODE." }
}
$New = '1.5.0'
$RepositoryRoot = (Resolve-Path -LiteralPath '<path to the application checkout>' -ErrorAction Stop).Path
$RefreshBranch = "template-refresh-$New"
$Worktree = (Resolve-Path -LiteralPath (Join-Path (Split-Path $RepositoryRoot) "orders-template-$New") -ErrorAction Stop).Path
$Scratch = '<the scratch folder path that phase 1 printed>'
```

Then check again that the application checkout is clean, update the local baseline branch, and confirm that the merged commit has the same Git tree as the local refresh-branch head that was pushed for review, by comparing the two tree object IDs; a squash merge changes the commit ID but not the tree. This proves local tree equality only. The hosting service's rules must separately ensure that the pull request's head did not change after approval. Make sure the new tag exists neither locally nor on the remote, because a baseline tag must never be replaced automatically. Tag the merged commit and push the tag. Only then remove the disposable worktree through Git rather than deleting its folder, delete the local refresh branch, and delete the scratch folder after checking that its path and contents have the structure phase 1 created. In a repository without a remote, adapt the fetch, `ls-remote`, and push steps as in phase 1.

```powershell
# Phase 2: run only after the baseline pull request has merged.
Set-Location -LiteralPath $RepositoryRoot -ErrorAction Stop
$CheckoutStatus = git status --porcelain=v1
Assert-ExitCode 'Reading the application checkout status'
if ($CheckoutStatus) { throw 'The application checkout has uncommitted changes.' }
git fetch origin template-baseline:template-baseline
Assert-ExitCode 'Updating the local baseline branch'

$ReviewedTree = git rev-parse --verify --quiet "$RefreshBranch^{tree}"
Assert-ExitCode 'Resolving the reviewed tree'
$MergedTree = git rev-parse --verify --quiet 'template-baseline^{tree}'
Assert-ExitCode 'Resolving the merged baseline tree'
if ($ReviewedTree -ne $MergedTree) { throw 'template-baseline does not hold exactly the reviewed refresh tree.' }

$Tag = "template-v$New"
git show-ref --verify --quiet "refs/tags/$Tag"
$TagExit = $LASTEXITCODE
if ($TagExit -eq 0) { throw "Tag $Tag already exists locally. Verify it rather than replacing it." }
if ($TagExit -ne 1) { throw "git show-ref failed with exit code $TagExit." }
git ls-remote --exit-code --refs --tags origin "refs/tags/$Tag" *> $null
$RemoteTagExit = $LASTEXITCODE
if ($RemoteTagExit -eq 0) { throw "Tag $Tag already exists on origin. Verify it rather than replacing it." }
if ($RemoteTagExit -ne 2) { throw "git ls-remote failed with exit code $RemoteTagExit." }

git tag -a $Tag template-baseline -m "Raw Larkspur.Templates $New scaffold"
Assert-ExitCode 'Tagging the new baseline'
git push origin $Tag
Assert-ExitCode 'Pushing the tag'

git worktree remove $Worktree
Assert-ExitCode 'Removing the refresh worktree'
git branch -D $RefreshBranch   # -D: after a squash or rebase merge the branch is not an ancestor
Assert-ExitCode 'Deleting the local refresh branch'
$ScratchPrefix = Join-Path ([IO.Path]::GetTempPath()) 'larkspur-refresh-'
$ScratchLooksRight = $Scratch.StartsWith($ScratchPrefix, [StringComparison]::OrdinalIgnoreCase) -and
  (Test-Path -LiteralPath (Join-Path $Scratch 'global.json') -PathType Leaf) -and
  (Test-Path -LiteralPath (Join-Path $Scratch 'old') -PathType Container) -and
  (Test-Path -LiteralPath (Join-Path $Scratch 'new') -PathType Container)
if (-not $ScratchLooksRight) { throw "Refusing to delete $Scratch." }
Remove-Item -LiteralPath $Scratch -Recurse -Force -ErrorAction Stop
$PSNativeCommandUseErrorActionPreference = $PreviousNativePreference
```

With `--exit-code`, `git ls-remote` returns 0 when the tag is found and 2 when the remote answered but has no such tag, so any other nonzero result is treated as a communication or command failure. In the example, phase 2 ran in a new PowerShell process with the values re-created as shown. The pull request's squash merge was simulated on the bare repository standing in for the hosting service; the fetch fast-forwarded the local branch to the squash commit, the tree IDs matched, both tag checks passed, and the tag landed on that commit. A bare repository enforces no hosting rules, so the simulation verified the resulting trees and tags, not the enforcement of pull request or linear-history rules.

Mirroring is what this example and ReferenceApplication use because of their documented newline behavior, not a universal replacement for patches. A patch built from the committed baseline objects (`git diff template-v1.4.0 template-v1.5.0`) avoids this particular working-tree CRLF mismatch because it compares the blob content stored in those commits; in this repository, that content had already passed through the normalization rules applied during staging. The next section uses one.

---

## 6. Choose How to Integrate

The baseline branch now holds the old and new scaffolds as consecutive commits, and the application needs the difference between them. Do the reconciliation on a short-lived integration branch from `main`, never on `main` itself. Four techniques were tried against the same application, and for these inputs the first two reported the same conflicts:

| Technique | Command used | What happened in the example | Tradeoff |
| --- | --- | --- | --- |
| **Merge** the new baseline tag | `git merge template-v1.5.0` | A content conflict in `Program.cs` and a modify/delete conflict on `StatusEndpoints.cs` | Real ancestry: for this first refresh, `git merge-base main template-baseline` is the 1.4.0 commit, and later refreshes start from the last merged baseline. That only holds if the ancestry survives into `main`; a squash merge of the integration pull request discards it, and so does an earlier refresh adopted by cherry-pick, in which case use an explicit commit range or a patch instead. Merging the tag rather than the branch tip keeps the integration to the reviewed release. The baseline's history becomes part of `main`'s history. |
| **Cherry-pick** the refresh commit | `git cherry-pick -x template-v1.5.0` | The same two conflicts | Copies the refresh as one reviewable commit without merging the baseline branch's history into the application branch. Traceability comes from the baseline tags and the `-x` trailer. After resolving conflicts, finish with `git cherry-pick --continue`, inspect the final commit message, and add the source commit or baseline tag yourself if the trailer is absent. |
| **Apply a patch** of the baseline commits | `git diff --binary` from the adopted baseline into a patch file outside the checkout, then `git apply --3way` (commands after the pre-integration check below) | Failed with exit code 1; afterward the working tree and index were unchanged, with no unmerged entries | Needs no shared branch ancestry, but three-way fallback needs the blob identities recorded in the patch, which this diff carries, and those blobs must exist locally. `--3way` implies `--index`. Here the deletion of a file the application had modified could not be applied, and nothing was changed. Do not assume every failed three-way application ends that way: `git apply --3way` can also leave conflict markers and unmerged index entries, so check `git status` after any failure. |
| **Manual reconciliation** | Read the delta, edit the application | Not needed here | The honest choice when the template restructured enough that no diff applies; the delta still tells you what the template authors intended. |

All four rest on the same three-way comparison: old scaffold, new scaffold, and the application. A merge obtains the three states from shared history. A cherry-pick performs a three-way merge using the selected commit, its parent, and the current branch. `git apply --3way` attempts a three-way fallback when the patch records the required blob identities. These mechanisms preserve different kinds of ancestry and traceability, even where they expose the same conflicts.

If an earlier refresh's baseline merged but its integration was abandoned, the adopted version from [section 2](#the-latest-raw-baseline-is-not-necessarily-the-adopted-one) is older than the previous baseline tag, and a single-commit cherry-pick would skip a release. Integrate the cumulative baseline delta from the adopted tag through the new one instead.

Whichever path applies, first confirm that the adopted tag still points at the commit the manifest recorded, and inspect exactly which baseline commits the integration will bring in:

```powershell
$Manifest = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'template-manifest.json') -Raw -ErrorAction Stop |
  ConvertFrom-Json
$RecordedCommit = [string] $Manifest.adoptedBaselineCommit
if ($RecordedCommit -notmatch '^[0-9a-fA-F]{40}([0-9a-fA-F]{24})?$') {
  throw 'adoptedBaselineCommit in template-manifest.json is not a full commit ID.'
}
$ActualCommit = git rev-parse --verify --quiet "$($Manifest.adoptedBaselineTag)^{commit}"
Assert-ExitCode "Resolving $($Manifest.adoptedBaselineTag)"
if (-not $ActualCommit.Equals($RecordedCommit, [StringComparison]::OrdinalIgnoreCase)) {
  throw "$($Manifest.adoptedBaselineTag) no longer points at the recorded commit."
}

git merge-base --is-ancestor $Manifest.adoptedBaselineTag "template-v$New"
$AncestorExit = $LASTEXITCODE
if ($AncestorExit -eq 1) { throw "$($Manifest.adoptedBaselineTag) is not an ancestor of template-v$New." }
if ($AncestorExit -ne 0) { throw "git merge-base failed with exit code $AncestorExit." }
git log --oneline --reverse "$($Manifest.adoptedBaselineTag)..template-v$New"
Assert-ExitCode 'Listing the baseline commits to integrate'
```

The commit IDs are compared case-insensitively, and a SHA-256 repository's 64-character IDs are accepted as well as SHA-1's 40. In the example, the checks passed and the list held the single 1.5.0 refresh commit. On the required linear baseline, that list is the sequence to integrate; confirm it contains only the intended refreshes before running `git cherry-pick -x "$($Manifest.adoptedBaselineTag)..template-v$New"`. If the list contains merge commits or anything other than the intended template refreshes, stop and investigate; then select the commits explicitly, or merge the selected baseline tag if the application's preserved ancestry supports it and the complete delta has been reviewed. A multi-commit range was not exercised in the example.

The patch variant also starts from the verified adopted commit, never from `$Old`, so it carries the whole cumulative delta. It writes the patch to the temporary folder rather than the checkout, stops on a failed application, and keeps the patch for diagnosis in that case:

```powershell
$Patch = Join-Path ([IO.Path]::GetTempPath()) "baseline-$($Manifest.adoptedVersion)-to-$New-$([Guid]::NewGuid().ToString('N')).patch"
git diff --binary "--output=$Patch" $RecordedCommit "template-v$New"
Assert-ExitCode 'Writing the cumulative baseline patch'
git apply --3way $Patch
$ApplyExit = $LASTEXITCODE
git status --short
Assert-ExitCode 'Reading the status after git apply'
if ($ApplyExit -ne 0) {
  throw "git apply --3way exited with code $ApplyExit. Inspect the working tree and index; the patch is kept at $Patch."
}
Remove-Item -LiteralPath $Patch -ErrorAction Stop
```

This commit-to-commit `git diff` does not use `--exit-code`, so writing a nonempty patch still exits with 0. In the example, the application failed as the table describes, the script stopped, and the patch was kept.

### Resolve with written rules

Write the rules down before resolving, and put them in the pull request:

1. Apply template changes to customized files without discarding unrelated customizations, and leave customized files the delta did not change alone unless a compatibility change the refresh requires is separately justified.
2. Review the template's shared package versions as proposed baseline changes, and adopt them unless the application has a documented compatibility, servicing, or security reason to keep a different, tested version. Keep application-specific package entries.
3. Regenerate lock files and other generated state; never hand-merge them.
4. For a file the template deleted but the application changed, decide ownership explicitly and record the decision.
5. Review every automatically merged hunk in a customized file, even when Git reports no conflict, and accept it only after that review and passing builds and tests.

The fifth rule earned its place in the example. The cherry-pick merged one hunk of `Program.cs` cleanly: the template removed `using Orders.Api;` because its own code no longer needed it. The application's endpoint extensions live in that namespace. After keeping both sides of the conflicted block, the build of that intermediate file failed:

```text
Program.cs(14,5): error CS1061: 'WebApplication' does not contain a definition for 'MapStatusEndpoints' ...
Program.cs(15,5): error CS1061: 'WebApplication' does not contain a definition for 'MapOrderEndpoints' ...
```

The resolved file keeps the `using`, adopts health checks, and keeps the application's mappings:

```csharp
using Orders.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/healthz");
app.MapStatusEndpoints();
app.MapOrderEndpoints();

app.Run();
```

Here the compiler caught the clean-but-wrong hunk. A removed service registration or middleware call could compile and fail only at runtime, and a test suite may not cover it, which is why the rule asks for review as well as builds and tests.

---

## 7. Dependencies, Lock Files, and EF Core Artifacts

### Central package versions and lock files

Larkspur uses [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management), so the version bump arrives in one place, `Directory.Packages.props`. Larkspur does not ship a lock file; the application's `packages.lock.json` is application-owned state from its first restore, and the cherry-pick did not change it. Restoring in [locked mode](https://learn.microsoft.com/nuget/consume-packages/package-references-in-project-files#locking-dependencies) right after the cherry-pick failed, as it should. NuGet reported, verbatim (including the missing space after the first sentence):

```text
error NU1004: The package reference Microsoft.AspNetCore.OpenApi version has changed from [10.0.14, ) to [10.0.15, ).The packages lock file is inconsistent with the project dependencies so restore can't be run in locked mode. Disable the RestoreLockedMode MSBuild property or pass an explicit --force-evaluate option to run restore to update the lock file.
```

Resolve the package version files first, by the written rules, and only then regenerate the lock files from them, as two separate commands:

```powershell
dotnet restore Orders.Api.slnx --force-evaluate
Assert-ExitCode 'Regenerating the lock files'
dotnet restore Orders.Api.slnx --locked-mode
Assert-ExitCode 'Verifying a locked restore'
dotnet build Orders.Api.slnx --configuration Release --no-restore
Assert-ExitCode 'Building the integrated application'
```

The regenerated lock file changed the OpenAPI package and, transitively, `Microsoft.OpenApi` from 2.12.2 to 2.13.0, with new content hashes. That transitive change is part of what the refresh brings in and belongs in the review. If the lock files themselves conflict, which happens when a template ships them, take either side to clear the conflict and then regenerate; the contents of a hand-merged lock file describe a graph nobody restored.

The forced run in [section 1](#1-what-in-place-regeneration-did) shows the other side of this: an ordinary, non-locked build quietly rewrote `packages.lock.json` to match the regenerated project. NuGet's documentation describes this behavior: outside locked mode, restore updates the lock file when it detects a dependency change. Run restore with `--locked-mode` in CI, so a dependency change has to be made on purpose.

### EF Core model snapshots and migrations

Templates that include EF Core may generate a `DbContext`, configuration, and sometimes an initial migration. Once the application adds its own migrations, that ordered migration history belongs to the application, and the model snapshot reflects all of it. Treat a refresh that touches them as schema work, not file merging:

- **Do not resolve a snapshot conflict by combining whichever blocks look plausible, or by renaming and reordering migration files.** First decide whether the conflict represents two diverged migration histories. When it does, follow Microsoft's guidance for [migrations in team environments](https://learn.microsoft.com/ef/core/managing-schemas/migrations/teams): remove the unmerged migration while keeping its model changes, incorporate the accepted migration history, and re-create the migration so EF Core regenerates the migration and snapshot consistently. Review the migration, the snapshot, and the generated SQL together.
- **Do not regenerate, squash, or delete migrations that may already have been applied** to databases in one or more environments. That history is the application's contract with those databases.
- **Add a migration only for a change the refresh actually introduces**, whether a model change or a deliberate data or schema operation, review its SQL, and test it from an empty database and against a representative existing one.
- **Know what an empty migration proves.** It shows that EF Core detects no difference between the reconciled model and its current snapshot. [`dotnet ef migrations has-pending-model-changes`](https://learn.microsoft.com/ef/core/managing-schemas/migrations/managing) checks whether the model differs from the migrations snapshot without creating a migration, and can serve as a CI guard. Neither compares the model with a deployed database, so keep testing the migration chain against a new database and a representative existing one.

Version matters for one detail. Microsoft's team-environments page says the change arrived in EF Core 11 preview 3 and applies starting with EF Core 11, whose 11.0.0 packages are now published on NuGet: the model snapshot records the latest migration ID, so parallel migrations produce a visible snapshot conflict. EF Core 10 and earlier do not record it, so diverged migrations can merge without any conflict. The distinction depends on the EF Core package version the application references, not on its target framework or installed SDK; check that version before relying on conflict visibility. Larkspur itself contains no EF Core; this guidance applies when the procedure is extended to applications that do.

---

## 8. Validate Twice, and Know Where to Stop

Two different things need evidence, and passing one says little about the other.

**The raw baseline**, before any application code is involved:

1. Both versions reproduce across repeated generations, and the old scaffold matches the stored baseline.
2. The option comparison was reviewed, and every option that can affect the generated application was deliberately selected and recorded rather than left to a release-dependent default.
3. The staged delta contains only differences between the two raw scaffold outputs. Manually maintained application code or repository workflows, or any other file neither scaffold emitted, appearing in it mean the baseline, the source folders, or the generation inputs are wrong.
4. The raw scaffold restores, builds, and passes its own tests and format checks on the intended SDK, in locked mode when the template ships lock files. Larkspur ships neither tests nor lock files, so in the example this step was a restore and build, and the application's lock file was verified after integration.

**The integrated application**, on the integration branch:

1. Locked restore, Release build, format verification, and the full test suite, with nothing loosened to pass. The sample application has no test project, so the example relied on the build and the smoke test below; a real application should not.
2. Security-relevant defaults the template touched: authentication and authorization behavior, cookie and header settings, HTTPS and forwarded-header handling.
3. Data and provider compatibility: migrations from empty and against a representative database, as above.
4. Container and deployment behavior if the template generates or influences them: the image builds, starts, and passes its health checks, and configuration still binds.
5. A short smoke test of what the application promises. In the example, `/healthz`, `/status`, and `/orders/7` returned `Healthy`, `{"status":"ok","build":"1.0.0.0"}`, and `{"id":7}` with status 200; `1.0.0.0` is the default assembly version, because the sample sets none.

**Stop rules.** Stop and investigate, rather than pressing on, when the old scaffold does not match the baseline, the generator does not reproduce its output, the staged baseline delta contains anything neither scaffold generation produced, `.gitattributes` changed in a way you do not understand, or a conflict cannot be resolved without changing application behavior nobody has agreed to change.

**Recovery boundary.** Everything above happens away from `main`: the scaffolds are in a scratch folder, the baseline change is on a refresh branch in its own worktree, and the integration is on a short-lived branch. What stopping costs depends on how far the work got, but before either pull request merges it is limited to aborting any cherry-pick or merge still in progress, deleting whichever temporary branches were created, removing the disposable worktree with `git worktree remove`, and deleting the scratch folder. No revert of `main` is needed, because `main` changes only when the integration pull request is merged.

If integration is abandoned after the baseline pull request has merged, keep the validated raw baseline and its tag as a record of the template's history. The application stays on its previously adopted version, and its manifest still says so. Record why the integration was abandoned, and bring in the cumulative baseline delta from the adopted tag when the next attempt is made.

---

## When This Workflow Is Not Worth It

In the demonstrated arrangement, the procedure costs a protected baseline branch and tags, a recorded manifest, two pull requests per refresh, and continuing discipline. It is likely more than you need when:

- **The project is small or young.** If generating a fresh project from the new template and moving a handful of files into it takes an hour, do that.
- **The application has diverged so far that the template is a memory.** If little of the original structure remains, the delta will conflict everywhere, and reading the template's release notes and porting specific changes by hand may be cheaper and clearer.
- **The generator is unsuitable.** It does not reproduce its output in ways no narrow rule can handle, it is no longer available at the old version, or its post-actions reach outside the output folder.
- **The template rarely changes, or its changes rarely matter to you.** If its releases are mostly unrelated to what your application uses, maintaining the baseline may cost more than the occasional manual update.

In those cases a focused, hand-written change that names the template release it follows is a perfectly good refresh.

---

## Case Study Pointer: ReferenceApplication

ReferenceApplication, an AsiBackbone organization repository built from NetCoreApplicationTemplate, maintains a raw-scaffold branch protected by a repository ruleset that requires pull requests and linear history, and documents its complete refresh procedure in the [NCAT baseline refresh walkthrough](https://asibackbone.github.io/ReferenceApplication/walkthrough/refresh-ncat-baseline.html). That runbook covers the operational detail this article omits: its manifest, a scheduled reproducibility check, the exact pull request sequence, and its written conflict rules, including keeping its own data-access changes and regenerating its model snapshot. It is the source of the mirroring approach used here, which it adopted after the CRLF patch failure described in [section 5](#5-line-endings-patches-and-staging). Read it as one implementation, not as a requirement for this pattern.

---

## A Short Review Checklist

1. Did you avoid running `dotnet new` over the application checkout, with or without `--force`?
2. Is the template package, version, short name, every output-affecting option, SDK, and the application's adopted template version recorded, and were both scaffolds generated with exactly those inputs?
3. Did you compare the template's option lists, resolve any changed default explicitly, and still review the scaffold delta itself?
4. Does each version reproduce across repeated generations, and does a fresh old scaffold match the stored baseline?
5. Did you review the delta as added, changed, and deleted files, and decide ownership for every deleted file the application changed?
6. Was the baseline change staged in a guarded, disposable worktree with Git's normalization rules, and does it contain only differences between the two raw scaffold generations?
7. Are the conflict rules written down, including what is regenerated rather than merged, and does the integration cover the complete baseline delta from the adopted baseline tag through the new one?
8. Was every automatically merged hunk in a customized file reviewed, as well as built and tested?
9. Were lock files regenerated with `--force-evaluate` and verified with `--locked-mode`, and were EF Core migrations handled with EF Core's own workflow?
10. Were the raw baseline and the integrated application validated separately, and was the adopted version advanced only when the integration pull request merged?

---

## Continue Deeper

[You Don't Need to Roll Back What You Never Merged](you-dont-need-to-roll-back-what-you-never-merged.md) places a refresh like this inside a larger platform migration, with an isolated candidate, adoption criteria, and the reasons to keep `main` untouched until the evidence is in.

[Updating .NET Did Not Fix the Code Your Template Generated](updating-dotnet-does-not-fix-generated-code.md) covers the security side of the same ownership boundary: when an advisory names generated code, a focused, tested repair of the application's copy is usually safer than regenerating it.

[A Green CI Badge Does Not Prove Your .NET Package Is Trustworthy](ci-badge-does-not-prove-package-integrity.md) explains why a passing build, like the one after `--force`, answers a narrower question than it appears to.

### Related Work

- Microsoft's [`dotnet new`](https://learn.microsoft.com/dotnet/core/tools/dotnet-new) and [`dotnet new install`](https://learn.microsoft.com/dotnet/core/tools/dotnet-new-install) references describe `--force`, version-specific installation, and additional package sources.
- [.NET templates for authors](https://learn.microsoft.com/dotnet/core/tools/custom-templates) and [Create a template package for dotnet new](https://learn.microsoft.com/dotnet/core/tutorials/cli-templates-create-template-package) explain how templates are structured, packaged, and tested.
- NuGet's [locking dependencies](https://learn.microsoft.com/nuget/consume-packages/package-references-in-project-files#locking-dependencies) and [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management) pages define locked mode, forced re-evaluation, and centrally managed versions.
- EF Core's [migrations in team environments](https://learn.microsoft.com/ef/core/managing-schemas/migrations/teams) and [managing migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/managing) pages describe resolving diverged migrations and checking for pending model changes.
- Git's reference pages for [`gitattributes`](https://git-scm.com/docs/gitattributes), [`git apply`](https://git-scm.com/docs/git-apply), [`git add`](https://git-scm.com/docs/git-add), [`git worktree`](https://git-scm.com/docs/git-worktree), and [`git cherry-pick`](https://git-scm.com/docs/git-cherry-pick) define the behavior this article relies on.

---

## The Short Answer

Running `dotnet new --force` over a customized application can replace existing files that the selected template emits, without reconciling your changes, while files the newer template no longer emits may remain on disk. Instead, record how the application was generated and which template version it has adopted, regenerate the old and new versions separately with identical, explicit options, and check that both reproduce and that the stored baseline is untouched. Review the clean delta as added, changed, and deleted files. Stage it in a guarded, disposable worktree through Git's own normalization, or use a patch built from committed baselines, because a patch built from working-tree files can fail on line endings. Reconcile it on a short-lived branch with written rules, regenerate lock files instead of merging them, follow EF Core's migration workflow, and validate the raw baseline and the integrated application separately. For small or heavily diverged applications, a deliberate manual change is often the better refresh.

---

## Appendix: Reproducing the Example

### Environment used for verification

- Windows 11, PowerShell 7.7.0 and Git Bash
- .NET SDK 10.0.404
- Git 2.56.1 for Windows, with system-level `core.autocrlf=true` on the test machine; the CRLF patch result was reproduced by passing `-c core.autocrlf=false` to `git diff --no-index`
- Template packages built locally with `dotnet pack` and installed from a local folder feed with `--add-source`. Because that feed was not configured globally, each generation also printed a harmless "Failed to check update" notice from `dotnet new` to standard error and exited with code 0; it did not enter the captured option files.

### Template package

Each release is a template package project with this shape, adapted from Microsoft's template package tutorial (see the license note at the top); only `PackageVersion` differs:

```xml
<!-- Adapted from Microsoft's "Create a template package for dotnet new" tutorial.
     Copyright (c) Microsoft Corporation. Licensed under the MIT License. -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>Larkspur.Templates</PackageId>
    <PackageVersion>1.4.0</PackageVersion>
    <PackageType>Template</PackageType>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IncludeContentInPack>true</IncludeContentInPack>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <ContentTargetFolders>content</ContentTargetFolders>
    <NoDefaultExcludes>true</NoDefaultExcludes>
    <NoWarn>$(NoWarn);NU5128</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Content Include="content/**/*" />
    <Compile Remove="**/*" />
  </ItemGroup>
</Project>
```

`content/larkspur-api/.template.config/template.json` (1.5.0 changes `defaultValue` to `"false"`):

```json
{
  "$schema": "https://json.schemastore.org/template",
  "author": "Larkspur",
  "classifications": ["Web", "API"],
  "identity": "Larkspur.Templates.Api",
  "name": "Larkspur API",
  "shortName": "larkspur-api",
  "sourceName": "Larkspur.Api",
  "preferNameDirectory": false,
  "tags": { "language": "C#", "type": "project" },
  "symbols": {
    "EnableOpenApi": {
      "type": "parameter",
      "datatype": "bool",
      "defaultValue": "true",
      "description": "Add an OpenAPI document endpoint in Development."
    }
  }
}
```

The template declares no post-actions, so generation does not restore packages and the scaffold contains no lock file. Both releases generate, with CRLF line endings, a minimal `.gitignore` (`bin/` and `obj/` only), `Larkspur.Api.slnx`, `Larkspur.Api/appsettings.json` (the standard logging and `AllowedHosts` settings), and the following `.gitattributes`:

```text
* text=auto
*.cs text eol=crlf
*.csproj text eol=crlf
*.props text eol=crlf
*.slnx text eol=crlf
*.json text eol=crlf
```

`Directory.Build.props` sets `net10.0`, nullable and implicit usings, `TreatWarningsAsErrors`, and `RestorePackagesWithLockFile`. `Directory.Packages.props` enables central package management and, under `<!--#if (EnableOpenApi) -->`, pins `Microsoft.AspNetCore.OpenApi` to 10.0.14 in 1.4.0 and 10.0.15 in 1.5.0. The project file references that package under the same condition.

### What changes between the releases

1.4.0 `Larkspur.Api/Program.cs`:

```csharp
using Larkspur.Api;

var builder = WebApplication.CreateBuilder(args);

#if (EnableOpenApi)
builder.Services.AddOpenApi();

#endif
var app = builder.Build();

#if (EnableOpenApi)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

#endif
app.MapStatusEndpoints();

app.Run();
```

1.4.0 `Larkspur.Api/StatusEndpoints.cs`, which 1.5.0 no longer generates:

```csharp
namespace Larkspur.Api;

internal static class StatusEndpoints
{
    public static IEndpointRouteBuilder MapStatusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/status", () => Results.Ok(new { status = "ok" }));
        return endpoints;
    }
}
```

1.5.0 `Larkspur.Api/Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
#if (EnableOpenApi)
builder.Services.AddOpenApi();
#endif

var app = builder.Build();

#if (EnableOpenApi)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

#endif
app.MapHealthChecks("/healthz");

app.Run();
```

1.5.0 adds a minimal `.editorconfig`:

```ini
root = true

[*.cs]
indent_style = space
indent_size = 4
dotnet_sort_system_directives_first = true
```

### The application's customizations

After generating 1.4.0 with `--name Orders.Api --EnableOpenApi true` and committing the raw output as `template-v1.4.0`, the application's first commit on `main` added `app.MapOrderEndpoints();` after `app.MapStatusEndpoints();` in `Program.cs`, changed the `/status` response to `new { status = "ok", build = BuildInfo.Version }`, added an `"Orders": { "MaxPageSize": 50 }` section to `appsettings.json`, committed the restored `packages.lock.json`, and added `Orders.Api/OrderEndpoints.cs`:

```csharp
namespace Orders.Api;

internal static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/orders/{id:int}", (int id) => Results.Ok(new { id }));
        return endpoints;
    }
}

internal static class BuildInfo
{
    public static string Version { get; } =
        typeof(BuildInfo).Assembly.GetName().Version?.ToString() ?? "unknown";
}
```

### Third-party notice

The template package project in this appendix is adapted from Microsoft's documentation code, which is distributed under the following license:

```text
The MIT License (MIT)
Copyright (c) Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial
portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```
