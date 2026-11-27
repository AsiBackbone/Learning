---
description: Rerunning dotnet new --force over a customized app is not an upgrade. Diff two clean scaffolds, then reconcile that upstream delta into your code deliberately.
title: How to Update a Scaffolded .NET Application Without Overwriting Your Code
author: Christopher D. Cavell
published: "2026-11-27"
summary: A newer dotnet new template does not update the applications it generated earlier, and regenerating over a customized application with --force can replace intentional changes while leaving files the template has since deleted. Record exactly how the application was generated, regenerate the old and new template versions separately with identical options, review that clean upstream delta, stage it through Git's own line-ending normalization, and reconcile it into the application with written conflict rules. Regenerate lock files rather than hand-merging them, treat EF Core snapshots and migrations as schema decisions, and validate the raw baseline and the integrated application separately.
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

**Prerequisites:** No formal prerequisites. Familiarity with `dotnet new`, Git branches and conflicts, and NuGet package references is helpful. No AsiBackbone package, NetCoreApplicationTemplate, or prior Learning material is required.

**Sample scope:** The Larkspur template, its two releases, and the Orders application are fictional and deliberately small: one web project with an `.editorconfig`, central package versions, a lock file, and four relevant source and configuration files. The commands are PowerShell examples for Windows; Git and .NET CLI commands are the same on other platforms, and the one Windows-specific tool (`robocopy`) is called out where it appears. Branch and tag names are this article's choices, not requirements. ReferenceApplication appears near the end as a bounded pointer to one complete runbook, not as proof that its arrangement suits every repository.

**Verification:** Every outcome reported in this article (the refusal and the overwrite from `dotnet new`, the option and determinism checks, the patch failure, the staged delta, the cherry-pick and merge conflicts, the `git apply --3way` refusal, the lock-file error, the build error, and the passing endpoints) was observed by running the example with .NET SDK 10.0.404 and Git 2.56.1 for Windows on November 27, 2026. The [appendix](#appendix-reproducing-the-example) lists the template files and environment. Line-ending behavior depends on Git configuration, so the article states which setting produced each result. External sources were last verified on November 27, 2026.

**License:** Original prose is licensed under [Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/), and original code examples are additionally offered under the [MIT License](https://opensource.org/license/mit), as described in the repository's [licensing terms](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md). Third-party material, where identified, remains under its original license.

**What this article covers:** what rerunning `dotnet new` over an existing application actually does, and why a green build afterward proves little; recording template provenance before anything is regenerated; generating old and new scaffolds separately and checking options and determinism; reading the clean upstream delta as added, changed, and deleted files; why a patch that passes `git apply --check` can still fail `git apply --index` under `.gitattributes` normalization, and staging by mirroring into a disposable worktree as one way around it; choosing between merge, cherry-pick, patch, and manual reconciliation; regenerating lock files and treating EF Core migrations as schema work; validating the raw baseline and the integrated application separately, with a stop boundary; and when the whole procedure costs more than it returns.

**In plain terms:** a builder gives you house plans, and you remodel the kitchen. Two years later the builder publishes revised plans that move a wall and remove a closet. Rebuilding your house from the new plans would give you the new wall and erase your kitchen. Comparing the old plans with the new ones tells you exactly what the builder changed, so you can move that one wall around your kitchen and decide for yourself whether the closet you turned into a pantry goes.

Consider a team that generated its Orders service from an internal ASP.NET Core template, Larkspur.Templates 1.4.0. Over the following months the team added order endpoints, extended the generated `/status` endpoint to report the build version, and added an `Orders` section to `appsettings.json`. Larkspur 1.5.0 is now out with a newer OpenAPI package, health checks, and a new `.editorconfig`.

A developer installs 1.5.0 and runs the template again in the repository root. `dotnet new` refuses and lists eight files it would overwrite, so the developer adds `--force`. The command succeeds, the project builds without warnings, and the diff looks like an upgrade.

It is not. In this run, `--force` rewrote `Program.cs` from the new template, which silently removed `app.MapOrderEndpoints()` and the application's `/status` mapping. The project still compiles, because the endpoint classes are still there; nothing calls them. It reset `appsettings.json` to the template's version, deleting the `Orders` section, although Larkspur had not changed that file at all. Larkspur 1.5.0 also changed the default of its `EnableOpenApi` option from `true` to `false`, and the developer did not pass it, so OpenAPI disappeared from the project, the package version file, and the code. `StatusEndpoints.cs`, which 1.5.0 no longer generates, stayed on disk, because regenerating writes files and does not delete them. The ordinary build that followed rewrote `packages.lock.json` to match. Nothing failed. The /orders route was simply gone.

> **Installing a newer template changes what `dotnet new` will generate next. It does not update applications generated earlier. To bring a template's changes into a customized application, compare a clean scaffold of the old version with a clean scaffold of the new version, then reconcile that upstream delta into the application deliberately, never by regenerating over it.**

---

## Why This Recurs

Templates are generators. Microsoft's [.NET templates for authors](https://learn.microsoft.com/dotnet/core/tools/custom-templates) documentation describes packaging, installing, and running them; once output is written, the files belong to the repository that received them. A template release can change files, defaults, options, and dependency versions, and it can stop generating a file. None of that reaches an existing application unless someone carries it there.

The obvious tool for carrying it, `dotnet new` itself, is designed to create content, not to merge it. Its [reference page](https://learn.microsoft.com/dotnet/core/tools/dotnet-new) describes `--force` as forcing content to be generated "even if it would change existing files." That is exactly what the opening scenario did. It is a reasonable behavior for a generator and the wrong one for an upgrade.

Long-lived applications meet this repeatedly: organization templates evolve, security defaults change, and package baselines move every month. A procedure that is safe, reviewable, and repeatable is worth having before the first refresh, not after the first lost customization.

### How this differs from the LTS migration article

[You Don't Need to Roll Back What You Never Merged](you-dont-need-to-roll-back-what-you-never-merged.md) covers the larger decision: keeping an upstream baseline and an isolated candidate so that a .NET LTS migration can be qualified, adopted, or discarded without touching `main`. It explains the three states (baseline A, the application delta, baseline B) and the adoption criteria. This article does not repeat that model. It covers the hands-on mechanics of one refresh at the file level: what to record, how to produce a trustworthy old-to-new delta, the line-ending and staging traps on the way, how to resolve the add, change, and delete cases, and what to regenerate rather than merge. Use the two together for a platform migration; use this one alone for an ordinary same-generation template refresh.

A template refresh is also not a .NET runtime upgrade and not a deployment rollback. A clean scaffold delta tells you what the template authors changed. It does not tell you whether your application is compatible with a new runtime, and it does nothing for a release that is already in production.

---

## 1. What In-Place Regeneration Actually Does

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

That refusal is useful, and it is the right place to stop. With `--force` added, the same command exited with code 0 and produced this working-tree change:

```text
 M Directory.Packages.props
 M Orders.Api/Orders.Api.csproj
 M Orders.Api/Program.cs
 M Orders.Api/appsettings.json
?? .editorconfig
```

Read that list against what Larkspur actually changed (the clean delta appears in [section 4](#4-review-the-upstream-delta)), and four distinct problems appear:

| Observed | Why it matters |
| --- | --- |
| `appsettings.json` was replaced although the template did not change it | Overwriting is per generated file, not per upstream change. Every customized file the template generates is at risk. |
| `Program.cs` lost the application's endpoint mappings | The template's version replaced the file; regeneration has no notion of whose lines are whose. |
| OpenAPI was removed from three files | A template default changed between releases, and the command did not pass the option explicitly. |
| `StatusEndpoints.cs` remained | 1.5.0 no longer generates it, but generation adds and overwrites; it does not delete. The deletion is not recorded anywhere. |

Identical files, such as `.gitattributes` here, were overwritten with the same bytes and show no diff, which makes the change look smaller than the command's reach. And the subsequent build passed, so CI would have been green.

Two qualifications keep this honest. First, this is one template on one SDK. Templates can contain conditional content, post-actions that run scripts or restore packages, and renamed or relocated files, so the exact effect of `--force` varies; the point is that it is not designed to preserve anything. Second, `--force` into an *empty* directory is harmless and is exactly what the clean-scaffold procedure below does. The problem is the target, not the flag.

---

## 2. Record Provenance Before Regenerating Anything

A clean old scaffold is only useful if it can be reproduced exactly. That requires knowing exactly how the application was generated. Record it at generation time, in the repository, where it survives the people who ran the command.

| Record | Why |
| --- | --- |
| Template package ID and exact version, and its source feed | `Larkspur.Templates@1.4.0` is reproducible; "the Larkspur template" is not. |
| Template short name | Packages can contain several templates. |
| Every meaningful option, passed explicitly, including those left at their defaults | A default can change between releases, as `EnableOpenApi` did. |
| The project name (`--name`) and any output layout assumptions | `sourceName` substitution changes file names, namespaces, and paths. |
| SDK version used to generate (and `global.json`, if any) | The template engine ships with the SDK; generation behavior can differ across SDKs. |
| Relevant environment assumptions | Git line-ending configuration, operating system, and anything a post-action depends on. |

A small manifest committed beside the application is enough:

```json
{
  "templatePackage": "Larkspur.Templates",
  "templateVersion": "1.4.0",
  "shortName": "larkspur-api",
  "arguments": [ "--name", "Orders.Api", "--EnableOpenApi", "true" ],
  "sdk": "10.0.404"
}
```

Then keep the raw scaffold itself where it cannot drift: the example stores it on a `template-baseline` branch whose only commits are unmodified generator output, and marks each one with an annotated tag such as `template-v1.4.0`. A tag alone, or a generator that can always reproduce the output on demand, works too; the [LTS article's mechanism table](you-dont-need-to-roll-back-what-you-never-merged.md#choosing-the-baseline-mechanism) compares the options. In the example, `main` was created from the baseline commit, so the two branches share real Git ancestry.

---

## 3. Regenerate Both Versions Independently

Generate the old and new scaffolds into an empty scratch folder outside the repository, with identical arguments. The example installs each release into its own temporary template store with `--debug:custom-hive`, so the versions do not replace each other and the machine's installed templates are untouched. That option is a template-engine diagnostic switch that does not appear in the `dotnet new` reference; it worked as shown on SDK 10.0.404, but treat it as unsupported. The documented alternative is to `dotnet new uninstall` and `dotnet new install` each version in turn, using the [`<package>@<version>` syntax](https://learn.microsoft.com/dotnet/core/tools/dotnet-new-install), and to accept that the machine is left with the newer version installed.

```powershell
$Old = '1.4.0'
$New = '1.5.0'
$Arguments = @('--name', 'Orders.Api', '--EnableOpenApi', 'true')
$Scratch = Join-Path ([IO.Path]::GetTempPath()) "larkspur-refresh-$New"

New-Item -ItemType Directory -Path $Scratch | Out-Null
Set-Location $Scratch

foreach ($Version in $Old, $New) {
  $Hive = Join-Path $Scratch "hive-$Version"
  dotnet new install "Larkspur.Templates@$Version" --debug:custom-hive $Hive
  dotnet new larkspur-api --help --debug:custom-hive $Hive > "options-$Version.txt"
}
```

Add `--add-source <feed>` to `dotnet new install` when the template comes from a private feed.

### Compare the options before trusting the defaults

```powershell
git diff --no-index -- "options-$Old.txt" "options-$New.txt"
```

For Larkspur this printed one change:

```text
 Template options:
   -E, --EnableOpenApi  Add an OpenAPI document endpoint in Development.
                        Type: bool
-                       Default: true
+                       Default: false
```

Had the old and new scaffolds been generated with defaults, the upstream delta would have "removed OpenAPI", which is not a template change the application should adopt; it is an artifact of changed inputs. Decide the value the application uses, pass it explicitly to both generations, and record it in the manifest. A new option deserves the same decision.

### Generate, twice for the new version

```powershell
dotnet new larkspur-api @Arguments --output old --debug:custom-hive "$Scratch\hive-$Old"
dotnet new larkspur-api @Arguments --output new --debug:custom-hive "$Scratch\hive-$New"
dotnet new larkspur-api @Arguments --output new-repeat --debug:custom-hive "$Scratch\hive-$New"

git diff --no-index --stat -- new new-repeat
```

The final command must print nothing. Two generations that differ mean the generator emits nondeterministic content, such as GUIDs, ports, secrets, or timestamps. That noise would appear in every delta and could hide real changes. Handle it with a narrow, documented rule for those specific files or values, or by passing the value as an explicit option if the template allows it, never by ignoring files broadly.

### Tell nondeterminism from a corrupted baseline

Two different checks answer two different questions:

| Check | Failure means |
| --- | --- |
| New scaffold generated twice is identical | Failure: the *generator* is nondeterministic. |
| Fresh old scaffold matches the stored baseline | Failure with a deterministic generator: the *baseline* was edited after generation, was generated with different inputs, or the tag points at the wrong commit. |

Run the first before the second, so a baseline mismatch is not blamed on the generator or the reverse. The second check happens inside Git in the next section, because comparing raw bytes on disk would also report line-ending differences that Git does not store.

---

## 4. Review the Upstream Delta

Diffing the two clean folders already shows what Larkspur changed:

```powershell
git diff --no-index --stat -- old new
```

```text
 /dev/null => new/.editorconfig                 |  6 ++++++
 {old => new}/Directory.Packages.props          |  2 +-
 {old => new}/Orders.Api/Program.cs             |  5 ++---
 old/Orders.Api/StatusEndpoints.cs => /dev/null | 10 ----------
 4 files changed, 9 insertions(+), 14 deletions(-)
```

That is the whole upstream change, with the application nowhere in it. Read each entry against the application before touching it:

| Upstream change | Application state | Defensible resolution |
| --- | --- | --- |
| **Added** `.editorconfig` | Absent | Adopt. Then check that the new rules do not mass-reformat application code in the same change; if they would, land formatting separately. |
| **Changed** `Directory.Packages.props`: OpenAPI 10.0.14 → 10.0.15 | Unchanged since generation | Adopt the shared version, then regenerate the lock file ([section 7](#7-dependencies-lock-files-and-ef-core-artifacts)). |
| **Changed** `Program.cs`: health checks added, `/status` mapping and its `using` removed | Customized: order endpoints mapped, `/status` kept | Reconcile by hand: keep the application's mappings and adopt health checks. |
| **Deleted** `StatusEndpoints.cs` | Customized: reports the build version | Decide ownership. The template has replaced `/status` with `/healthz`; the application has consumers of `/status`. |
| *Not in the delta:* `appsettings.json` and everything the application added | Customized | Leave untouched. A file the template did not change has nothing to reconcile. |

The deleted file is the case in-place regeneration loses entirely, and it is also the one that needs a human decision. In the example, external monitors call `/status` and read its `build` field, so the team keeps `StatusEndpoints.cs` as application-owned code, adopts `/healthz` alongside it, and opens a follow-up to move monitors and retire `/status` later. Deleting the file because upstream did would have been equally defensible if nothing depended on it. What is not defensible is not noticing.

If `.gitattributes` itself appears in the delta, review it first and separately: it changes how Git normalizes every other file.

---

## 5. Line Endings, Patches, and Staging

The `git diff --no-index` output is a review aid. Turning it into a commit on the baseline branch is where line endings can bite.

Larkspur, like many templates, ships a `.gitattributes` that checks C#, project, and JSON files out with CRLF line endings while Git stores them with LF in the index:

```text
i/lf    w/crlf  attr/text eol=crlf      Orders.Api/Program.cs
```

The template writes the files with CRLF, and Git records them with LF. That is normal and correct. It also means the bytes on disk and the content in the index are different representations of the same file.

### Why a patch can pass `--check` and still fail `--index`

The tempting route is to save the folder diff as a patch and apply it to the baseline. What happens depends on Git configuration. With `core.autocrlf=false`, `git diff --no-index` wrote the generated CRLF bytes into the patch, 61 lines carrying a carriage return. Applied in a worktree of the baseline branch:

| Command | Result with the CRLF patch |
| --- | --- |
| `git apply --check -p2 old-to-new.patch` | Passed: the context matches the CRLF working tree. |
| `git apply --check --cached -p2 old-to-new.patch` | Failed: `patch does not apply`, because the index holds LF. |
| `git apply --index -p2 old-to-new.patch` | Failed for the same reason; nothing was applied. |
| `git apply --index --ignore-whitespace -p2 old-to-new.patch` | "Succeeded", but `git ls-files --eol` then reported `i/mixed` and `i/crlf` for three files: carriage returns were written into the index. |

[`git apply`](https://git-scm.com/docs/git-apply) is behaving as documented: `--index` applies to both the index and the working tree, so the context has to match both. With the machine's `core.autocrlf=true` (the Git for Windows installer default used here), the same `git diff --no-index` produced an LF-only patch, and every command above succeeded. The same recipe therefore works for one developer and fails for another. That is a reason to understand the mechanism, not a reason to distrust patches in general.

### Mirroring into a disposable worktree: one demonstrated solution

An approach that sidesteps patch context entirely is to make a separate worktree of the baseline branch match a scaffold folder exactly and let `git add` do the comparison. Git then normalizes every file through the [`.gitattributes`](https://git-scm.com/docs/gitattributes) rules, detects additions and deletions itself, and needs no `-p` path handling. In the example it produced the same four-file delta with `core.autocrlf` set to `true` and to `false`.

Mirroring deletes files, so the safety rules matter more than the command:

- Run it only in a **dedicated [`git worktree`](https://git-scm.com/docs/git-worktree)** of the baseline branch, never in the application's checkout or any worktree with uncommitted work.
- Exclude `.git`: in a linked worktree it is a file that points at the repository, and deleting it detaches the worktree.
- Refuse to run if the worktree is not clean or is not on the expected branch.

```powershell
$RepositoryRoot = (Get-Location).Path   # a clean checkout of the application repository
$BaselineCommit = git rev-parse "template-v$Old^{commit}"
if ((git rev-parse template-baseline) -ne $BaselineCommit) {
  throw "template-baseline has moved since template-v$Old. Investigate before continuing."
}

$RefreshBranch = "template-refresh-$New"
$Worktree = Join-Path (Split-Path $RepositoryRoot) "orders-template-$New"
git worktree add $Worktree -b $RefreshBranch template-baseline
Set-Location $Worktree

function Update-IndexFromScaffold {
  param([Parameter(Mandatory)] [string] $Source)

  if ((git branch --show-current) -ne $RefreshBranch -or (git status --porcelain)) {
    throw 'Refusing to mirror: not the clean refresh worktree.'
  }
  robocopy $Source . /MIR /XD .git /XF .git /NFL /NDL /NJH /NJS /NP | Out-Null
  if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE." }
  git add --all
  git add --renormalize .
}
```

`robocopy /MIR` is the Windows tool for mirroring a folder, and exit codes below 8 mean success. On Linux or macOS, `rsync --archive --delete --exclude=.git <source>/ ./` serves the same purpose; it was not run for this article. [`git add --renormalize`](https://git-scm.com/docs/git-add) applies the clean process freshly to all tracked files, so a change that cached file metadata might hide is still detected. Git may print "LF will be replaced by CRLF" warnings; they are expected.

First prove the baseline is what the old release generates, then stage the new release:

```powershell
Update-IndexFromScaffold "$Scratch\old"
git diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
  git diff --cached --stat
  throw "template-baseline does not match a fresh $Old scaffold."
}

git reset --hard --quiet   # the worktree check requires a clean tree
Update-IndexFromScaffold "$Scratch\new"
git diff --cached --check
git diff --cached --stat
```

In the example, the first check staged nothing. After a deliberate hand edit to `appsettings.json` was committed on a copy of the baseline, the same check failed and named that file: drift in content is caught, while line-ending-only differences, which Git does not store, are not reported. Then the new scaffold staged exactly the four files from [section 4](#4-review-the-upstream-delta). Commit it with a message naming the release, open a pull request into `template-baseline` containing nothing else, and tag the merged commit `template-v1.5.0`.

Normalization is a feature here and also a limit. "Nothing staged" proves the stored baseline matches the generator's output *as Git stores it*. It does not prove that the generator is byte-for-byte deterministic; that is what the `new`/`new-repeat` comparison is for.

Mirroring is what this example and ReferenceApplication use because of their documented newline behavior, not a universal replacement for patches. A patch built from the baseline commits themselves (`git diff template-v1.4.0 template-v1.5.0`) is generated from normalized index content and does not carry the working-tree CRLF problem at all; the next section uses one.

---

## 6. Choose How to Integrate

The baseline branch now holds the old and new scaffolds as consecutive commits, and the application needs the difference between them. Do the reconciliation on a short-lived integration branch from `main`, never on `main` itself. Four techniques were tried against the same application:

| Technique | Command used | What happened in the example | Tradeoff |
| --- | --- | --- | --- |
| **Merge** the baseline branch | `git merge template-baseline` | A content conflict in `Program.cs` and a modify/delete conflict on `StatusEndpoints.cs` | Real ancestry: `git merge-base main template-baseline` is the 1.4.0 commit, so later refreshes start from the right base automatically. The baseline's history becomes part of `main`'s history. |
| **Cherry-pick** the refresh commit | `git cherry-pick -x template-v1.5.0` | The same two conflicts | Keeps `main` linear; each refresh is one reviewable commit. Ancestry is not recorded; traceability comes from the tags and the `-x` trailer, which was present in the final message here. |
| **Apply a patch** of the baseline commits | `git diff --binary template-v1.4.0 template-v1.5.0` then `git apply --3way` | Refused the whole patch, because the deleted file had been modified; nothing was applied | Portable between repositories without shared history. `--3way` uses blob identities recorded in the patch, but a delete of a changed file still needs manual work. |
| **Manual reconciliation** | Read the delta, edit the application | Not needed here | The honest choice when the template restructured enough that no diff applies; the delta still tells you what upstream intended. |

Whichever is used, the mental model is a three-way comparison: old scaffold, new scaffold, and the application. A merge gives Git that comparison through real history. Cherry-pick and patch emulate it per file from the commits they are given, which is why both reported the same conflicts. The model matters even when the command is manual.

### Resolve with written rules

Write the rules down before resolving, and put them in the pull request:

1. Do not replace a customized file unless the upstream delta changed it.
2. Adopt the template's shared package versions; keep application-specific package entries.
3. Regenerate lock files and other generated state; never hand-merge them.
4. For a file the template deleted but the application changed, decide ownership explicitly and record the decision.
5. Treat every clean, automatically merged hunk in a file the application customized as unreviewed until the build and tests pass.

The fifth rule earned its place in the example. The cherry-pick merged one hunk of `Program.cs` cleanly: the template removed `using Orders.Api;` because its own code no longer needed it. The application's endpoint extensions live in that namespace. After keeping both sides of the conflicted block, the build failed:

```text
Program.cs(14,5): error CS1061: 'WebApplication' does not contain a definition for 'MapStatusEndpoints' ...
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

Here the compiler caught the clean-but-wrong hunk. A removed service registration or middleware call would compile and fail only at runtime, which is why the rule says *build and tests*, not just build.

---

## 7. Dependencies, Lock Files, and EF Core Artifacts

### Central package versions and lock files

Larkspur uses [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management), so the version bump arrives in one place, `Directory.Packages.props`, and the application's `packages.lock.json` does not change with it. Restoring in [locked mode](https://learn.microsoft.com/nuget/consume-packages/package-references-in-project-files#locking-dependencies) right after the cherry-pick failed, as it should:

```text
error NU1004: The package reference Microsoft.AspNetCore.OpenApi version has changed from [10.0.14, ) to [10.0.15, ).
The packages lock file is inconsistent with the project dependencies so restore can't be run in locked mode.
```

Resolve the package version files first, by the written rules, and only then regenerate the lock files from them:

```powershell
dotnet restore Orders.Api.slnx --force-evaluate
dotnet restore Orders.Api.slnx --locked-mode
dotnet build Orders.Api.slnx --configuration Release --no-restore
```

The regenerated lock file changed the OpenAPI package and, transitively, `Microsoft.OpenApi` from 2.12.2 to 2.13.0, with new content hashes. That transitive change is part of what the refresh brings in and belongs in the review. If the lock files themselves conflict, which happens when the template ships them, take either side to clear the conflict and then regenerate; the contents of a hand-merged lock file describe a graph nobody restored.

One observation from the forced run in [section 1](#1-what-in-place-regeneration-actually-does) belongs here: an ordinary, non-locked build quietly rewrote `packages.lock.json` to match the regenerated project. NuGet's documentation describes this behavior: outside locked mode, restore updates the lock file when it detects a dependency change. Run restore with `--locked-mode` in CI, so a dependency change has to be made on purpose.

### EF Core model snapshots and migrations

Templates that include EF Core often generate a `DbContext`, configuration, and sometimes an initial migration. The application then adds its own migrations, and the model snapshot reflects all of them. Treat this as schema work, not file merging:

- **Do not hand-merge the model snapshot as text.** After reconciling the model code, regenerate what the tools generate, and compare the result with the migrations the application has already applied.
- **Do not regenerate, squash, or delete historical migrations** to make a refresh tidy. Databases in every environment have applied them, and their history is the application's contract with those databases.
- **Generate a new migration only for a model change the refresh actually introduces**, review its SQL, and test it from an empty database and against a representative existing one. An empty migration is a useful sign that the reconciled model matches the schema.
- Follow Microsoft's guidance for [migrations in team environments](https://learn.microsoft.com/ef/core/managing-schemas/migrations/teams) when the snapshot conflicts. That page notes that EF Core 11 and later record the latest migration ID in the snapshot so parallel migrations conflict visibly, while EF Core 10 and earlier do not, and it advises re-creating your migration rather than renaming or reordering files.

---

## 8. Validate Twice, and Know Where to Stop

Two different things need evidence, and passing one says little about the other.

**The raw baseline**, before any application code is involved:

1. The new scaffold is deterministic, and the old scaffold matches the stored baseline.
2. The option comparison was reviewed, and every option is passed explicitly.
3. The staged delta contains only files the template generates; application, documentation, or workflow files in it mean the inputs are wrong.
4. The raw scaffold restores in locked mode where it ships lock files, builds, and passes its own tests and format checks on the intended SDK.

**The integrated application**, on the integration branch:

1. Locked restore, Release build, format verification, and the full test suite, with nothing loosened to pass.
2. Security-relevant defaults the template touched: authentication and authorization behavior, cookie and header settings, HTTPS and forwarded-header handling.
3. Data and provider compatibility: migrations from empty and against a representative database, as above.
4. Container and deployment behavior if the template generates or influences them: the image builds, starts, and passes its health checks, and configuration still binds.
5. A short smoke test of what the application promises. In the example, `/healthz`, `/status`, and `/orders/7` returned `Healthy`, `{"status":"ok","build":"1.0.0.0"}`, and `{"id":7}` with status 200.

**Stop rules.** Stop and investigate, rather than pressing on, when the old scaffold does not match the baseline, the generator is nondeterministic, the delta contains files the template does not generate, `.gitattributes` changed in a way you do not understand, or a conflict cannot be resolved without changing application behavior nobody has agreed to change.

**Recovery boundary.** Everything above happens away from `main`: the scaffolds are in a scratch folder, the baseline change is on a refresh branch in its own worktree, and the integration is on a short-lived branch. Stopping costs `git cherry-pick --abort` or `git merge --abort`, deleting two branches, and removing a worktree and a folder. No revert of `main` is needed, because `main` changes only when the integration pull request is merged. Advance the baseline tag only after the baseline pull request merges, and record why a refresh was abandoned so the next attempt starts from that knowledge.

---

## When This Workflow Is Not Worth It

The procedure costs a protected baseline, a recorded manifest, a two-pull-request ritual, and some discipline. It is likely more than you need when:

- **The project is small or young.** If generating a fresh project from the new template and moving a handful of files into it takes an hour, do that.
- **The application has diverged so far that the template is a memory.** If little of the original structure remains, the delta will conflict everywhere, and reading the template's release notes and porting specific changes by hand may be cheaper and clearer.
- **The generator is unsuitable.** It is nondeterministic in ways no narrow rule can handle, it is no longer available at the old version, or its post-actions reach outside the output folder.
- **The template rarely changes, or its changes rarely matter to you.** If the template's releases are mostly unrelated to what your application uses, maintaining the baseline may cost more than the occasional manual update.

In those cases, still avoid regenerating over the application. A focused, hand-written change that names the template release it follows is a perfectly good refresh.

---

## Case Study Pointer: ReferenceApplication

ReferenceApplication, an AsiBackbone organization repository built from NetCoreApplicationTemplate, maintains a protected raw-scaffold branch and documents its complete refresh procedure in the [NCAT baseline refresh walkthrough](https://asibackbone.github.io/ReferenceApplication/walkthrough/refresh-ncat-baseline.html). That runbook covers the operational detail this article omits: its manifest, a scheduled reproducibility check, the exact pull request sequence, and its written conflict rules, including keeping its own data-access changes and regenerating its model snapshot. It is the source of the mirroring approach used here, which it adopted after the CRLF patch failure described in [section 5](#5-line-endings-patches-and-staging). Read it as one implementation, not as a requirement for this pattern.

---

## A Short Review Checklist

1. Did you avoid running `dotnet new` over the application checkout, with or without `--force`?
2. Is the template package, version, short name, every option, and SDK recorded, and were both scaffolds generated with exactly those inputs?
3. Did you compare the template's option lists and resolve any changed default explicitly?
4. Is the new scaffold deterministic, and does a fresh old scaffold match the stored baseline?
5. Did you review the delta as added, changed, and deleted files, and decide ownership for every deleted file the application changed?
6. Was the baseline change staged in a disposable worktree, with Git's normalization rules, and does it contain only generated files?
7. Are the conflict rules written down, including what is regenerated rather than merged?
8. Were automatically merged hunks in customized files treated as unreviewed until the build and tests passed?
9. Were lock files regenerated with `--force-evaluate` and verified with `--locked-mode`, and EF Core migrations treated as schema work?
10. Were the raw baseline and the integrated application validated separately, and was the baseline tag advanced only after its pull request merged?

---

## Continue Deeper

[You Don't Need to Roll Back What You Never Merged](you-dont-need-to-roll-back-what-you-never-merged.md) places a refresh like this inside a larger platform migration, with an isolated candidate, adoption criteria, and the reasons to keep `main` untouched until the evidence is in.

[Updating .NET Did Not Fix the Code Your Template Generated](updating-dotnet-does-not-fix-generated-code.md) covers the security side of the same ownership boundary: when an advisory names generated code, a focused, tested repair of the application's copy is usually safer than regenerating it.

[A Green CI Badge Does Not Prove Your .NET Package Is Trustworthy](ci-badge-does-not-prove-package-integrity.md) explains why a passing build, like the one after `--force`, answers a narrower question than it appears to.

### Related Work

- Microsoft's [`dotnet new`](https://learn.microsoft.com/dotnet/core/tools/dotnet-new) and [`dotnet new install`](https://learn.microsoft.com/dotnet/core/tools/dotnet-new-install) references describe `--force`, version-specific installation, and additional package sources.
- [.NET templates for authors](https://learn.microsoft.com/dotnet/core/tools/custom-templates) explains how templates are structured, packaged, and tested.
- NuGet's [locking dependencies](https://learn.microsoft.com/nuget/consume-packages/package-references-in-project-files#locking-dependencies) and [Central Package Management](https://learn.microsoft.com/nuget/consume-packages/central-package-management) pages define locked mode, forced re-evaluation, and centrally managed versions.
- EF Core's [migrations in team environments](https://learn.microsoft.com/ef/core/managing-schemas/migrations/teams) describes resolving model snapshot conflicts.
- Git's reference pages for [`gitattributes`](https://git-scm.com/docs/gitattributes), [`git apply`](https://git-scm.com/docs/git-apply), [`git add`](https://git-scm.com/docs/git-add), [`git worktree`](https://git-scm.com/docs/git-worktree), and [`git cherry-pick`](https://git-scm.com/docs/git-cherry-pick) define the behavior this article relies on.

---

## The Short Answer

Installing a newer template does not update the applications it generated, and running `dotnet new --force` over a customized application overwrites every file the template generates while leaving the files it stopped generating. Instead, record exactly how the application was generated, regenerate the old and new template versions separately with identical, explicit options, and check that the generator is deterministic and the stored baseline is untouched. Review the clean delta as added, changed, and deleted files. Stage it in a disposable worktree through Git's own normalization, or use a patch built from committed baselines, because a patch built from working-tree files can fail on line endings. Reconcile it on a short-lived branch with written rules, regenerate lock files instead of merging them, treat EF Core migrations as schema decisions, and validate the raw baseline and the integrated application separately. For small or heavily diverged applications, a deliberate manual change is often the better refresh.

---

## Appendix: Reproducing the Example

### Environment used for verification

- Windows 11, PowerShell 7.7.0 and Git Bash
- .NET SDK 10.0.404
- Git 2.56.1 for Windows, with the installer's system-level `core.autocrlf=true`; the CRLF patch result was reproduced by passing `-c core.autocrlf=false` to `git diff --no-index`
- Template packages built locally with `dotnet pack` and installed from a local folder feed with `--add-source`; because that feed was not configured globally, each generation also printed a harmless "Failed to check update" notice and exited with code 0

### Template package

Each release is a template package project with this shape; only `PackageVersion` differs:

```xml
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
    <NoWarn>NU5128</NoWarn>
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
  "$schema": "http://json.schemastore.org/template",
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

Both releases generate, with CRLF line endings, `.gitignore` (`bin/` and `obj/`), `Larkspur.Api.slnx`, `Larkspur.Api/appsettings.json` (the standard logging and `AllowedHosts` settings), and the following `.gitattributes`:

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

1.5.0 adds `.editorconfig`:

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
