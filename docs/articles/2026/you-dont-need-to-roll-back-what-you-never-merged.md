---
description: Evaluate a new .NET LTS on an isolated candidate built from a preserved upstream baseline, so a failed migration never has to be backed out of main.
title: You Don't Need to Roll Back What You Never Merged
author: Christopher D. Cavell
published: "2026-10-23"
summary: A platform migration does not have to begin by changing the application you maintain. Preserve a reproducible upstream baseline, qualify the new generation on its own, reconcile your application on an isolated candidate, and merge only on explicit evidence. A failed candidate leaves nothing to back out of main.
feed: true
x_hashtags:
  - DotNet
  - SoftwareArchitecture
---

# You Don't Need to Roll Back What You Never Merged

*Using a clean baseline to de-risk .NET LTS migrations*

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** No formal prerequisites. Familiarity with Git branches, pull requests, and a CI pipeline for a .NET application is helpful. No AsiBackbone package, NetCoreApplicationTemplate, or prior Learning material is required.

**Sample scope:** The commands are illustrative Git and .NET CLI fragments, not a complete migration script. Branch names, tag names, and the order of checks are this article's examples, not requirements. ReferenceApplication appears near the end as a bounded case study of one implementation, not as proof that its arrangement suits every team.

**What this article covers:** why "we can always roll it back" is the wrong first safety mechanism for a platform migration; the three states worth preserving (an upstream baseline, your application's delta from it, and the new baseline) and the isolated candidate that reconciles them; why that is a three-way integration model rather than necessarily one `git merge`; qualifying the raw new baseline before qualifying the integrated candidate; keeping fixes and security updates flowing on `main` while the candidate is unmerged; objective adoption evidence; discarding a failed candidate; what the pattern does not solve; choosing a branch, tag, generator, or vendor snapshot as the baseline; and when the whole arrangement is more than you need.

A team maintains an internal ordering service. Three years ago it was generated from the organization's ASP.NET Core template: authentication, EF Core persistence, health checks, container build, and CI were all scaffolded. Since then the team has added a domain model, two integrations, a reporting module, and many small fixes. The template team has now published a new generation targeting the next .NET LTS release, with a new SDK, new ASP.NET Core and EF Core majors, updated packages, and a few structural changes to the generated host.

The plan is familiar: change the target framework, bump the packages, repair the build, merge as soon as CI is green, and clean up the remaining warnings and a flaky container test "on `main`." Three weeks later the cleanup is still going when a security advisory arrives that has to ship today. It must be built and deployed from a half-migrated `main`, where nobody is sure whether a failing integration test comes from the advisory fix, the new runtime, the new EF Core provider, or a template change. Someone asks how hard it would be to roll the migration back.

That question arrives too late. The uncertainty was allowed into the line that the team uses to support production, and rollback is now being asked to remove it.

> **A platform migration does not have to begin by changing the application being maintained. Preserve a known upstream baseline, validate the new generation independently, reconcile the application delta on an isolated candidate, and merge only after the candidate earns adoption through explicit evidence. If the experiment fails before merge, there is nothing to back out of `main`.**

This changes where uncertainty is allowed to live. It does not make upgrades automatic, it does not prevent breaking changes, and it does not remove the need for rollback after a release is deployed.

---

## Why This Recurs

The migration in the opening is not a one-off. As of October 2026, Microsoft's [.NET and .NET Core support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core) states that a new major .NET release is published every year in November, that Long Term Support (LTS) releases receive three years of free support and patches, and that Standard Term Support (STS) releases receive two years. Patch updates follow Microsoft's monthly Patch Tuesday schedule, and systems must remain current on released patches to stay supported. Check the policy page for the current versions and end-of-support dates rather than relying on any date copied into a document, including this one.

Microsoft's guidance on [breaking changes when porting](https://learn.microsoft.com/dotnet/core/porting/breaking-changes) says that changes affecting compatibility do occur between versions, describes behavioral, binary, source, and design-time compatibility among the ways a change can break an application, and advises checking the breaking-changes documentation before upgrading major versions. The [.NET breaking-changes reference](https://learn.microsoft.com/dotnet/core/compatibility/breaking-changes) is organized by release and points to separate tracking for ASP.NET Core and Entity Framework Core.

Microsoft defines these lifecycles; it does not set a schedule for any particular application. But for a long-lived application that stays on supported .NET releases, the cadence makes periodic platform-generation upgrades a recurring piece of work rather than a one-time event. Each upgrade may affect the runtime, the SDK, ASP.NET Core, EF Core, and third-party dependencies at the same time. If the application was generated from a template or reference architecture, the upstream structure may change too. That category of work is predictable, even when its scope is not. Mixing it with ordinary maintenance is a choice, not a requirement.

---

## 1. The Rollback Framing Is Too Late

Rollback is a remedy for a change that has already been accepted. When the migration starts as edits to `main`, every later commit is built on top of it. Features, fixes, and dependency updates merged during the migration interleave with migration commits. A `git revert` of the migration is then not one operation but a negotiation: which later commits depend on the new runtime, which fixes were written against the new APIs, and which tests were changed to accommodate both.

The deeper problem is attribution. When the half-migrated `main` fails, the cause could be any of:

- the .NET runtime or SDK;
- ASP.NET Core or EF Core behavior;
- a third-party package that moved with them;
- a structural change in the upstream template;
- the application's own code; or
- an interaction between two of these.

A conventional upgrade branch cut from `main` isolates the work from the supported line, but by itself it provides a weaker attribution boundary. The branch starts from the fully integrated application, so every result on it is produced by the customized application running on the new platform. Package-by-package commits, targeted tests, and bisecting can recover some attribution, at extra cost, but there is still no clean reference point for what the upstream alone would have done.

The alternative is to keep migration uncertainty outside the supported line until it has been resolved, and to keep a reference point that makes failures attributable.

---

## 2. Preserve Three States

The pattern depends on three states that the team can reproduce on demand, plus one place where they are reconciled.

| State | What it is | Question it answers |
| --- | --- | --- |
| **Baseline A** | The qualified, unmodified upstream output currently underlying the supported line: the generated template, reference architecture, or framework skeleton, with the exact options used. Initially it is the output the application was created from; later it may be a same-generation upstream refresh that has already been adopted into `main` | What did the upstream provide, separate from anything we changed? |
| **Application delta (A → `main`)** | Everything the team changed after generation: domain code, integrations, configuration, fixes, tests | What have *we* decided? |
| **Baseline B** | The unmodified upstream output for the new generation, produced with the same inputs as A | What did the upstream change on its own? |
| **Integration candidate** | An isolated line where the application delta is reconciled onto B | Can our decisions coexist with the new generation, and is the result ready to adopt? |

In plain terms: A is the upstream you are built on, `main` is where you are, B is where the upstream went, and the candidate is your attempt to get to B without losing what you built.

The value of A is easy to overlook. Without it, the difference between "the template changed this" and "we changed this" is a matter of memory and archaeology. With it, `A → B` is the upstream evolution and `A → main` is the application's evolution, and each can be reviewed by the people who own it.

```text
baseline A ───────────────► baseline B / new .NET generation
    │                                │
    │ application delta              │ isolated integration
    ▼                                ▼
current main ─────────────► candidate + full CI
    │                                │
    │ normal maintenance       pass / fail
    │                           │       │
    └───────────────────────────┘       └─ discard or continue;
                    adopt               main was never changed
```

The `main` branch keeps moving the whole time. Only an adoption decision connects the candidate back to it.

---

## 3. A Three-Way Integration Model, Not Necessarily One Git Command

The reasoning is the same as a three-way merge. There is a common ancestor (A), one side that evolved for upstream reasons (B), and one side that evolved for application reasons (`main`). Reconciliation keeps both sets of intended changes and resolves the places where they touch the same thing.

That is a mental model, not a prescription to run `git merge`. Depending on how the baseline is stored and how much the upstream changed, the reconciliation may use:

- **a Git merge** of a baseline branch into the candidate, which gives Git a real merge base and full three-way context for later refreshes;
- **a cherry-pick** of the single commit that moved the baseline from A to B, which keeps history linear and makes each refresh one reviewable change;
- **a patch** produced from `A → B` and applied to the candidate;
- **regeneration** of B with the same inputs, followed by reapplying the application delta file by file; or
- **manual integration**, when the upstream restructured enough that neither side's diff applies cleanly and the team ports its changes onto the new structure deliberately.

Each choice has trade-offs. A merge preserves ancestry that tools can use later, but it couples the baseline's history to the application's history. A cherry-pick or patch keeps history simple, but traceability then depends on tags, commit trailers, or recorded diffs rather than on Git's merge base. Regeneration and manual integration are the honest options when the upstream changed its shape. The model is still useful in that case, because `A → B` tells the team what the upstream intended while they port.

Whichever mechanism is used, the conflict-resolution rules belong in writing before the work starts. Examples: keep application-specific package references, adopt the upstream's shared dependency versions, regenerate lock files and EF Core model snapshots instead of hand-merging them, and do not replace a customized file unless the upstream delta actually changed it.

---

## 4. Qualify the Raw Baseline First

Before any application code is involved, establish that B itself is sound. Problems found here arise before application integration, which makes them much cheaper to attribute before the application delta obscures their source.

Check, at least:

1. **Deterministic generation.** Generate B twice with identical inputs and confirm the outputs are identical. Random ports, GUIDs, or timestamps in generated files would otherwise appear as noise in every comparison.
2. **Option stability.** Compare the generator's available options and defaults between A and B. If a default changed, decide which value the application uses and generate both A and B with that value passed explicitly, so `A → B` contains only the release change.
3. **Baseline integrity.** Regenerate A and confirm it still matches the stored baseline exactly. A mismatch means the baseline was edited after generation, or was generated with different inputs. Stop here, because the `A → B` delta would include that contamination.
4. **Restore, build, test, and format** on the new SDK, using locked dependency restore where the project supports it, with warnings treated as errors if that is the application's policy.
5. **Security analysis**: dependency vulnerability review and static analysis on the raw output.
6. **Container behavior**, if the upstream generates container assets: the image builds, starts, and passes its health checks.
7. **Documented breaking changes** for the runtime, ASP.NET Core, and EF Core versions involved, reviewed against what the generated host actually uses.
8. **Delta review.** Read `A → B` as an upstream change. It should contain upstream files only. Application, documentation, or workflow files appearing in it mean something is wrong with the inputs.

These checks can run in CI on a branch or tag that holds B, or as a scripted local procedure whose output is attached to the baseline review. Either way, record the result as evidence for B alone. A raw baseline that does not restore on the new SDK has failed before application integration begins. Treat that first as a baseline, platform, dependency, or environment finding, to fix locally, report upstream, or wait out, rather than attributing it to the application's migration.

---

## 5. Qualify the Integrated Candidate Second

Only after B is qualified does the candidate reconcile the application delta onto it. The candidate then runs everything `main` runs, and usually more.

- **Full regression suite**: unit, integration, and end-to-end tests, without tests disabled or loosened to make the candidate pass. A changed test needs the same justification it would need on `main`.
- **Persistence and migrations**: apply the full migration history to an empty database; apply pending migrations to a representative, production-shaped dataset that has been masked, synthesized, or otherwise handled under the organization's data-governance requirements; rerun the deployment's chosen migration mechanism against the already-current database and confirm that it applies nothing and changes nothing; and verify that durable records written by the current version can still be read by the candidate. Pay particular attention to provider behavior changes in a new EF Core major, and to which migration-deployment features (such as idempotent SQL scripts) your database provider supports.
- **Security**: dependency review, static analysis, authentication and authorization behavior, and any security headers or cookie settings that the new ASP.NET Core version may default differently.
- **Compatibility**: public API contracts, serialization formats, and message schemas that other systems depend on.
- **Operational behavior**: container build and runtime image, health checks, configuration binding, logging and telemetry, startup time, and resource use under representative load.
- **Documentation**: build instructions, the recorded SDK and framework versions, and any operator runbooks that name versions or commands.
- **Deployment rehearsal**: deploy the candidate to a non-production environment with the same pipeline that will deploy it after adoption.

### Running on the New LTS Is Not the Same as Being Ready to Adopt It

Keep these validation layers separate, because passing one says little about the next:

| Layer | Question |
| --- | --- |
| Upstream generation | Can the new template or baseline be reproduced deterministically with the intended options? |
| Platform compatibility | Does the raw baseline restore, build, test, and run on the new .NET generation? |
| Integration compatibility | Can the application's delta coexist with the new upstream structure and dependencies? |
| Persistence compatibility | Can supported databases and durable evidence be upgraded without loss? |
| Operational compatibility | Do containers, health checks, security controls, authentication, observability, and deployment assumptions still hold? |
| Adoption decision | Is the candidate ready to replace the supported line, rather than merely capable of compiling? |

All five validation layers can pass while the adoption decision is still "no." A candidate can compile, pass every test, and run in a container, and still be unready because a dependent system has not been checked, a migration has not been rehearsed at representative data volumes, or the team has not yet had time to observe it under load. The candidate makes that distinction possible: "it runs" can be true for weeks while "we adopt it" is deliberately left undecided, and nobody else is affected by the wait.

---

## 6. Keep Normal Maintenance Moving

While the candidate is evaluated, `main` remains the supported line. Features, fixes, and security updates continue there, through the same reviews and the same CI as always. That is the point of the arrangement, but it creates drift: every change to `main` is a change the candidate does not yet have.

Manage drift deliberately:

- **Land shared fixes on `main` first.** A fix that applies to both lines is made once on `main` and carried to the candidate when it is next reconciled. Do not make the same fix independently on both lines.
- **Keep generation-specific changes on the candidate.** Code that only compiles or only makes sense on the new platform stays there until adoption.
- **Move forward-compatible preparation to `main` early.** Replacing an API that is obsolete in the new version, when the replacement also works on the current version, shrinks the candidate's delta and ships value immediately. Keep these changes small and separately reviewed so they do not become a migration by stealth.
- **Reconcile the candidate on a cadence.** Bring `main` into the candidate regularly (for example after each release, or weekly), not only at the end. Small, frequent reconciliations are easier than one large one, and each is another chance to rerun the full suite. When it is enabled, Git's [reuse of recorded resolutions (`git rerere`)](https://git-scm.com/docs/git-rerere) can reduce repeated work when the same conflicts recur. Review what it replays like any other resolution.
- **Treat security updates as time-critical on both lines.** An urgent fix ships from `main` on the current platform today. Carry it to the candidate promptly, because an unpatched candidate that is later adopted would reintroduce the vulnerability.
- **Advance baseline A only after a same-generation upstream refresh has been qualified and adopted into `main`**, and advance it to that exact upstream output. A stale A makes `A → B` include changes that `main` already absorbed; an A advanced before `main` contains the refresh describes an upstream the application is not built on.

If the candidate's reconciliations keep producing large, repeated conflicts in the same files, treat that as information. The application may be fighting the upstream structure, and that is a design conversation, not just a merge problem.

---

## 7. Adopt or Discard

Write the adoption criteria before evaluation starts, so that "ready" means the same thing at the end as it did at the beginning. A reasonable set of objective evidence:

1. The raw baseline B was qualified and its evidence was recorded.
2. The candidate was reconciled with the current `main` within an agreed window before adoption, with no unreviewed conflict resolutions.
3. Restore, build, formatting, and the full test suite pass on the candidate with the same strictness as `main`.
4. Dependency and static security analysis report no new findings above the agreed threshold.
5. Database migrations were applied from scratch and to a governed, production-shaped dataset, rerunning the migration mechanism changed nothing, and existing durable records remain readable.
6. Container and deployment rehearsal in a non-production environment passed, including health checks, configuration, and telemetry.
7. Documented breaking changes for every major version involved were reviewed, and each relevant one is addressed or explicitly accepted.
8. Documentation and runbooks name the new versions and commands.
9. An owner has signed off that the candidate is ready to replace the supported line, and a post-adoption rollback plan exists (see the next section).

When the criteria pass, adoption is one reviewable pull request to `main` that links the evidence. Reviewers can read it as "the upstream change, already qualified in B, plus these reconciliation decisions."

When they do not pass, the candidate can be paused, continued, restarted from a fresh B, or discarded. Discarding means deleting a branch and keeping the findings: what failed, which layer it belonged to, and what would have to change before the next attempt. An issue, a short report, or an [architecture decision record](../../aspnetcore/architecture-decision-records-preserve-architectural-reasoning.md#add-review-conditions-without-predicting-the-future) with explicit review conditions preserves that learning. `main` needs no revert, because it never received the experiment.

---

## 8. What the Pattern Does Not Solve

The pattern moves uncertainty out of the supported line before adoption. It does nothing for uncertainty after adoption, and it has costs of its own.

**Post-merge and post-deployment rollback still matter.** Once the candidate is adopted and deployed, problems discovered in production need the usual tools: deployment rollback, feature flags, canary or staged releases, backups, and restore procedures. A clean pre-merge evaluation lowers the probability of needing them. It does not remove the need to have them and to have rehearsed them.

**Data migrations may be one-way.** If the adopted version applies schema or data migrations that the previous version cannot read, redeploying the old binaries is not a rollback. Plan expand-and-contract migrations, backups, or explicit forward-fix procedures for the adoption release, independently of how the candidate was evaluated.

**External side effects are not undone by reverting code.** Messages sent, records written to other systems, and notifications delivered by the new version stay sent. Recovery after side effects needs compensating operations, not version control.

**Long-lived branches conflict.** The longer the candidate lives, the more `main` drifts and the more often the same files conflict. Regular reconciliation reduces the cost but does not remove it.

**Work can be duplicated.** Without the "shared fixes land on `main` first" discipline, fixes get made twice, differently, and the reconciliation has to choose between them.

**Baselines can be contaminated.** If anyone edits the stored baseline to make something work, A stops being the upstream output and every later `A → B` comparison is misleading. Protect the baseline (branch protection, an immutable tag, or a generated artifact) and verify it against a fresh generation before each refresh.

**CI capacity is real.** Qualifying B, running the candidate's full suite, and continuing to run `main`'s suite multiplies pipeline load during the evaluation.

**Unsupported upstreams break the model.** If the template or reference architecture stops publishing new generations, there is no B. At that point the team owns the structure outright, and the migration is an ordinary in-application upgrade, which can still be done on an isolated candidate but without the attribution benefit.

---

## Choosing the Baseline Mechanism

Nothing about the pattern requires a permanent branch, or any particular name. What matters is that A and B can be reproduced exactly and compared. Choose the cheapest mechanism that provides that evidence.

| Mechanism | Fits when | Watch for |
| --- | --- | --- |
| **Protected baseline branch** | The upstream is generated source the team owns after generation, refreshes happen repeatedly, and reviewers want each `A → B` delta as an ordinary pull request | Branch protection and a rule that only raw generated output lands there; contamination checks before each refresh |
| **Immutable tag per baseline** | A branch would add little beyond marking points in history; for example, each generation is committed once and never edited | Tags must be protected from deletion or force-moving; record the generator version and options with the tag |
| **Reproducible generator plus recorded inputs** | The generator is versioned, available, and deterministic, so A and B can be regenerated on demand into a scratch folder | Generator availability years later; nondeterministic output; options or defaults that change between versions |
| **Vendor snapshot or archived artifact** | The upstream is not a template but a vendored framework, SDK sample, or reference implementation delivered as source or archive | Licensing of the stored copy; recording exactly which release it is; storing it where it cannot be edited |

Many teams combine two: a generator with recorded inputs to produce A and B, and tags to mark the exact commits that held them.

## When This Is More Than You Need

The pattern earns its keep when the upstream structure is substantial and the application is long-lived. It is probably excessive when:

- the application has no meaningful upstream-generated architecture, so there is no A worth preserving;
- the application is small enough that a rewrite on the new platform is cheaper than maintaining a baseline;
- the migration is already trivial and well covered: no documented breaking changes that affect the application, and a test suite the team trusts; or
- the team cannot afford the CI capacity or the reconciliation discipline, in which case a short-lived upgrade branch merged quickly may carry less risk than a long-lived candidate that drifts.

An isolated candidate is still useful in most of these cases, even without a preserved baseline. What you give up is the clean attribution between upstream and application changes.

---

## Case Study: ReferenceApplication and NetCoreApplicationTemplate

[ReferenceApplication](https://github.com/AsiBackbone/ReferenceApplication) is an AsiBackbone organization repository that combines a host generated from NetCoreApplicationTemplate (NCAT) with the AsiBackbone governance library. It is one implementation of this pattern. The details below come from its [README at `v0.1.0-alpha.11`](https://github.com/AsiBackbone/ReferenceApplication/blob/v0.1.0-alpha.11/README.md) and its [baseline refresh procedure](https://github.com/AsiBackbone/ReferenceApplication/blob/v0.1.0-alpha.11/docs/walkthrough/refresh-ncat-baseline.md) at the same tag.

**The three states.** `main` is the integrated application and the default development branch. `ncat-baseline` is a protected branch holding the unmodified NCAT scaffold, generated with recorded options. Each raw scaffold commit is tagged `ncat-v<version>`: the `ncat-v2.11.4` tag marks the original generation, and the [`ncat-v2.11.5`](https://github.com/AsiBackbone/ReferenceApplication/tree/ncat-v2.11.5) tag marks the current baseline. `git diff ncat-baseline main` shows everything the application added on top of NCAT.

**Two separate changes.** Refreshing the baseline and integrating it are separate pull requests. The 2.11.4 to 2.11.5 refresh landed first as a pull request into `ncat-baseline` containing only raw generated output, then as a second pull request into `main` that reconciled it with the application.

**Raw baseline qualification.** The refresh procedure generates the old and new scaffolds into a scratch folder, compares the template's option lists, generates the new scaffold twice to prove deterministic output, and verifies that `ncat-baseline` still matches a fresh old scaffold exactly before staging the new one. It then runs locked restore, Release build, format verification, and tests against the raw scaffold. The expected 2.11.5 delta was narrow, limited to repository configuration, central package versions, and lock files, and the procedure says to stop if anything else appears. In this repository the raw baseline is qualified by that scripted procedure, whose results accompany the baseline pull request. The repository's CI workflow runs for `main`, not for `ncat-baseline`. A team adopting the pattern could equally run baseline qualification in CI.

**Integrated candidate qualification.** The integration branch is created from `main` and receives the tagged baseline commit through `git cherry-pick -x`, with written conflict rules: keep application-specific package entries and data-access changes, adopt NCAT's shared dependency versions, regenerate lock files and the EF Core model snapshot instead of hand-merging them, and do not replace customized files the upstream delta did not touch. The integration pull request then runs the repository's full CI: dependency review, build, format verification, tests with coverage, a container smoke test that applies migrations, runs the migration step a second time against the now-current database, and serves the application, and CodeQL analysis.

**Why cherry-pick.** The procedure explains its choice: a merge would give Git a true merge base and three-way context for later refreshes, while a cherry-pick keeps `main` linear and makes each refresh one reviewable change, with the `-x` trailer and the `ncat-v<version>` tags providing traceability. That is the three-way integration model carried out without a literal three-way merge.

**What it does and does not show.** The 2.11.4 to 2.11.5 refresh was a small rehearsal on the same .NET generation. It shows that the mechanics work and that the contamination, determinism, and option checks catch the problems they are designed to catch. It does not show that a future LTS transition will be easy. A new NCAT generation on a new .NET major may change the generated host substantially: a different startup shape, new ASP.NET Core authentication or hosting defaults, EF Core provider changes that require regenerating migrations or the model snapshot, and a much larger `A → B` delta. Then the cherry-pick may conflict widely, and ReferenceApplication's integration may become the regeneration or manual-porting case described earlier. What the arrangement offers is that the work can be done on an isolated candidate, attributed to the right owner, and adopted only when the evidence is in, while fixes continue to ship from `main`.

The pattern does not depend on NCAT, AsiBackbone, or this repository. Any application derived from a template, a reference architecture, or a vendored framework baseline can apply it with whichever baseline mechanism fits.

---

## A Short Review Checklist

1. Can you reproduce baseline A exactly, and have you verified that the stored copy still matches a fresh generation?
2. Is baseline B generated with the same inputs as A, deterministically, with any changed option or default resolved explicitly?
3. Is B qualified on its own (restore, build, test, format, security, container, and breaking-change review) before any application code is reconciled onto it?
4. Is the candidate isolated from `main`, and does it run the same suite as `main` plus persistence, operational, and deployment rehearsal?
5. Are the conflict-resolution rules written down, including which files are regenerated rather than hand-merged?
6. Do shared fixes and security updates land on `main` first and reach the candidate on a regular reconciliation cadence?
7. Are the adoption criteria written down before evaluation, and do they distinguish "runs on the new LTS" from "ready to adopt it"?
8. If the candidate is discarded, are the findings recorded somewhere that will inform the next attempt?
9. After adoption, do deployment rollback, data-migration recovery, and compensation plans still exist and still work?
10. Have you deliberately decided that the maintenance cost of the baseline is worth it for this application?

---

## Continue Deeper

[Retrofitting Governed Execution into an Existing ASP.NET Core Application](../../aspnetcore/retrofit-governed-execution-into-an-existing-application.md#the-stopping-rule) applies related phased-change thinking to a different problem: changing an application's behavior through phases with entry criteria, validation, and rollback conditions, rather than isolating a platform-generation experiment. Its stopping rule is a useful companion to the "when this is more than you need" question here.

[Architecture Decision Records Preserve Architectural Reasoning](../../aspnetcore/architecture-decision-records-preserve-architectural-reasoning.md) explains how to record why a baseline exists, which alternatives were considered, and which conditions would cause the strategy to change, so the reason for keeping it survives longer than the people who set it up.

[A Green CI Badge Does Not Prove Your .NET Package Is Trustworthy](ci-badge-does-not-prove-package-integrity.md) makes a parallel point about evidence: a passing pipeline answers specific questions, and the adoption decision needs to know which ones.

### Related Work

- Microsoft's [.NET and .NET Core support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core) defines the release cadence, LTS and STS support durations, patch requirements, and current end-of-support dates.
- [Breaking changes can affect porting your app](https://learn.microsoft.com/dotnet/core/porting/breaking-changes) describes the categories of compatibility change and advises reviewing breaking changes before a major-version upgrade.
- The [.NET breaking-changes reference](https://learn.microsoft.com/dotnet/core/compatibility/breaking-changes) lists breaking changes by release and links to ASP.NET Core and EF Core tracking.

---

## The Short Answer

Do not start a .NET LTS migration by editing the line you use to support production. Keep a reproducible copy of the upstream baseline you started from, generate the new baseline with the same inputs, and qualify it alone. Reconcile your application's changes onto it in an isolated candidate, run everything `main` runs and more, and keep shipping fixes from `main` while you do. Adopt the candidate only when written criteria pass, and treat "it runs on the new LTS" and "we are ready to adopt it" as separate decisions. If the candidate fails, discard it and keep what you learned: there is nothing to back out of `main`. After adoption, deployment rollback and data recovery still matter as much as ever.
