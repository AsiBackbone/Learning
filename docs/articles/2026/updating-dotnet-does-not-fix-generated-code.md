---
description: A runtime or SDK update cannot rewrite code your template already generated. Map each advisory to the component you own, then repair, rebuild, and prove it.
title: Updating .NET Did Not Fix the Code Your Template Generated
author: Christopher D. Cavell
published: "2026-11-24"
summary: A .NET runtime or SDK update repairs what Microsoft ships into that runtime or SDK. It does not rewrite source that a template or scaffolder copied into your application, it does not replace package versions your project pins, and it does not change a self-contained deployment that carries its own runtime. Read each security advisory for the component it names, map that component to the team that owns it, apply the remediation that component needs, and keep evidence that the deployed artifact contains the repair. The November 2026 ASP.NET Core advisories show all three remediation paths side by side.
feed: true
x_post: Patching .NET fixes the runtime, not code your template generated. That source is yours to repair and redeploy.
x_hashtags:
  - DotNet
  - DevSecOps
x_question: How does your team find code a template generated years ago?
---

# Updating .NET Did Not Fix the Code Your Template Generated

*Who owns the repair when a security advisory names generated source*

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** No formal prerequisites. Familiarity with how a .NET application is built and deployed, NuGet package references, and `dotnet new` or a scaffolding tool is helpful. No AsiBackbone package, NetCoreApplicationTemplate, or prior Learning material is required.

**Sample scope:** The team, its service, the Fabrikam template, and the Fabrikam advisory are fictional. The Microsoft advisories are real and are cited as the November 2026 case study; version numbers come only from those advisories and Microsoft's servicing post. The code example is a small, non-exploitable illustration of a generated handler and its repair, not a copy of Microsoft's templates. This article does not claim that every 2026 ASP.NET Core advisory affects generated source, that every `dotnet new` template or scaffolding release is affected, or that a runtime update is never sufficient.

**Verification:** The handler and its three tests in the [appendix](#appendix-complete-code) were compiled and run on .NET 10; the appendix records the [environment and command](#environment-used-for-verification). The unrepaired handler shown in the article fails two of those tests. Advisory statements are paraphrased from the cited sources rather than quoted, and each advisory remains the authority for its own affected versions and remediation. Original prose is licensed under [Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/), and original code examples are additionally offered under the [MIT License](https://opensource.org/license/mit), as described in the repository's [licensing terms](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md). Third-party material, where identified, remains under its original license. External sources were last verified on November 24, 2026.

**What this article covers:** a servicing update that passed every runtime check while an application-owned generated file stayed unchanged; inventorying what an application actually owns; reading an advisory by component rather than by one version number; how the November 2026 ASP.NET Core advisories separate runtime fixes, standalone packages, and previously generated code; a [component, owner, remediation, and evidence table](#4-map-each-component-to-an-owner-and-a-remediation); a small generated-file repair with a test that proves it; why package scanners, lock files, and SBOMs can miss a defect that lives in first-party source; what a clean template baseline is good for, and when a focused fix is safer than regenerating; and why verified remediation is not a promise that an application is secure.

**In plain terms:** a car manufacturer issues a recall for a brake part. Your dealer replaces that part during the next service. If, years earlier, you had a local mechanic fabricate a copy of the old part for a trailer you built yourself, the dealer never sees the trailer and never touches it. The recall notice may even say so. The trailer is yours to inspect and repair.

A hypothetical team runs a customer-facing order service on ASP.NET Core 10. On patch day, its platform engineer updates the base container image to the current ASP.NET Core runtime, rebuilds the image, and redeploys. The runtime version check in the deployment pipeline reports the new patch version on every node. The vulnerability scanner reports no vulnerable packages. The change ticket is closed as "remediated: runtime updated."

Two days later a reviewer reads one of the month's advisories more carefully. It concerns code that a scaffolding tool *generates into applications*, and it says that updating the tool does not repair code that was already generated. The order service's account-recovery pages were scaffolded three years ago and later customized. They live in the team's repository, compile into the team's assembly, and have not changed in eighteen months. Nothing in the patch-day work touched them, and nothing in the pipeline was designed to notice.

> **A runtime or SDK update repairs what ships inside that runtime or SDK. Source that a template or scaffolder generated into your application is application-owned code. Updating the generator does not change it, and only a source change, a rebuild, and a redeployment do.**

That does not mean runtime updates are insufficient in general. For many advisories, installing the corrected runtime and restarting a framework-dependent application is the complete remediation. The point is that the remediation depends on which component the advisory names and on who owns that component in your application, not on the version number printed by `dotnet --info`.

---

## Why This Recurs

Servicing is usually discussed in terms of versions: the runtime moved from one patch to the next, the SDK moved to a new feature band, the container image was rebuilt. Those numbers are easy to collect and easy to compare against an advisory's "patched version" column, so they become the evidence of remediation.

A .NET application, though, is assembled from parts that are serviced in different ways:

- some code arrives from a **shared runtime** that the host or container supplies when the application starts;
- some arrives from **NuGet packages** that the project references and the build copies into the output;
- some is **embedded in the deployment** itself, as when a self-contained application carries its own copy of the runtime;
- and some was **generated once** by `dotnet new`, a scaffolder, or an internal template, then committed and edited like any other source.

A runtime installation changes the first category. It changes nothing in the others. Templates make the last category easy to forget, because the code did not feel like something the team wrote. It arrived complete, it worked, and over time it became indistinguishable from the rest of the repository.

---

## 1. A Patch That Passed Every Runtime Check

Return to the order service. Here is what the patch-day evidence actually showed:

| Check | Result | What it actually proves |
| --- | --- | --- |
| Base image updated and rebuilt | New image digest | The container now includes the new shared runtime |
| Runtime version on every node | Current patch version | Framework-dependent code resolves the corrected shared framework |
| Package vulnerability scan | No findings | No *referenced package version* matched a known advisory at scan time |
| Smoke tests | Passing | The service starts and its tested paths behave as before |
| `git log` for the account-recovery pages | No change in 18 months | The generated source is exactly what it was before the patch |

Every row is true. The first four rows are correct evidence for the components they cover, and none of them says anything about the last row. The application's account-recovery handlers are not part of the runtime, are not a package, and are not something the scanner enumerates. They are part of the application's own assembly, generated from a template version that predates the fix.

---

## 2. Inventory What the Application Actually Owns

Before reading any advisory, list the components that end up in the deployed artifact and who is responsible for each. A practical inventory for an ASP.NET Core service has at least these entries:

| Component | Where it comes from | Who changes it |
| --- | --- | --- |
| Shared runtime (`Microsoft.NETCore.App`, `Microsoft.AspNetCore.App`) | Host machine or base container image, for framework-dependent apps | Whoever patches the host or rebuilds the image |
| Embedded runtime | Copied into a self-contained or single-file publish | The application team, by rebuilding and republishing |
| App-local NuGet packages | `PackageReference` and transitive dependencies, restored at build time | The application team, by updating references and rebuilding |
| Generated or scaffolded source | `dotnet new`, a scaffolding tool, or an internal template, committed to the repository | The application team, by editing source |
| Copied templates and custom forks | Files copied from samples, an internal starter kit, or a vendored framework | The team that took the copy |
| SDK and build tooling | Developer machines and CI agents; selected by `global.json` | Whoever maintains build environments |
| Container and deployment artifacts | Image, package, or bundle that actually runs | The release process, which must rebuild from the corrected inputs |

Two details matter in that table.

First, a generated file has a different provenance from the rest of the source, but the same owner. Once it is committed, the generator has no further relationship with it. Record its provenance anyway: which generator, which version, and when. Without that record, the team cannot answer the first question an advisory asks.

Second, the SDK appears in the inventory even though it does not run in production. It matters because the SDK can bundle the templates that generate code, and `global.json` can keep an older SDK selected after a newer one is installed. That affects *future* generated code; it never reaches back into code generated earlier.

---

## 3. Read the Advisory by Component, Not by Version Number

A security advisory for .NET usually lists affected **packages**, not just one product version. Read each row as a statement about one component:

- **What component is affected?** A runtime pack, a standalone NuGet package, a scaffolding package, a template package, or a tool.
- **What is the fixed version of that component?** Not the runtime version, unless the row is a runtime pack.
- **What does the remediation section say to do?** Install, update a reference, rebuild, redeploy, restart, or change source.
- **Does it mention code that already exists in applications?** That is the signal that the fix cannot be delivered by updating anything you download.

A single "patched version" is rarely enough. One month's advisories can name a runtime fix for one CVE, a package that ships independently of the runtime for another, and a manual source repair for a third.

### The November 2026 case: three remediation paths in one month

On November 10, 2026, Microsoft published thirteen ASP.NET Core security advisories and [an announcement that collects them](https://github.com/dotnet/announcements/issues/504). The announcement states that each advisory remains the authority for its own affected versions and remediation, that the required actions differ by CVE, and that a runtime or SDK update does not replace every standalone package or repair every application. [Microsoft's November servicing post](https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-november-2026-servicing-updates/) lists the same month's runtime releases (.NET 10.0.15, 9.0.23, 8.0.34, and 11.0.0) alongside the CVEs. Three of those advisories illustrate the three paths.

**A runtime fix, with a standalone package exception: CVE-2026-114146.** This [SignalR advisory](https://github.com/dotnet/aspnetcore/security/advisories/GHSA-67wp-ghc2-fw2g) lists ASP.NET Core runtime packs for .NET 8, 9, 10, and 11 with their patched runtime versions, and separately lists one standalone ASP.NET Core 2.3 package, `Microsoft.AspNetCore.Http.Connections`, with its own fixed version. Its remediation section says to install the applicable runtime or update each independently referenced package, to rebuild and redeploy after updating standalone packages because a restart alone does not replace app-local packages, and to rebuild and redeploy self-contained applications because a machine-wide runtime update does not replace their embedded runtime. It also says that an SDK update alone does not change an application's standalone package references. For a framework-dependent .NET 10 application with no standalone reference to the affected package, the corrected runtime and a restart are what this advisory asks for.

**A standalone package fixed outside the runtime release.** The servicing post notes that CVE-2026-118747 requires an update to the separate `System.Formats.Cbor` NuGet package. For the .NET 9 package line the fix is in package version 9.0.24, not in the 9.0.23 runtime, and installing the 9.0.23 runtime alone does not address it. The post says to update the package, then rebuild and redeploy. A team that compares only its runtime version with the month's runtime release would record this one as fixed when it is not.

**Generated application code: CVE-2026-118741.** This [scaffolding advisory](https://github.com/dotnet/announcements/issues/525) concerns ASP.NET Core Identity scaffolding templates that, in password-reset and email-confirmation flows, send the message to the email address supplied in the request instead of the address stored on the located account. It lists affected versions of the scaffolding packages (`Microsoft.VisualStudio.Web.CodeGenerators.Mvc` and its parent `Microsoft.VisualStudio.Web.CodeGeneration.Design`), the standalone `Microsoft.DotNet.Web.ProjectTemplates` packages, and the `dotnet-scaffold` tools. The advisory then makes three distinctions that this article depends on:

- Updating scaffolding packages or tools does not repair previously generated application code, and an application generated with an affected template remains affected after the tool is updated unless its handlers are corrected.
- Determining exposure requires checking both the scaffolding package or tool version *and the code it generated*. The `dotnet-aspnet-codegenerator` command loads generators from the project's own dependencies, so updating only the command-line tool does not replace those references. For `dotnet new`, the selected SDK and any separately installed template packs matter, and a package reference alone does not identify which template generated an application.
- The fix for existing applications is a described manual repair. In the generated `ForgotPassword` and `ResendEmailConfirmation` handlers, including Blazor equivalents, keep the existing user lookup and checks, obtain the stored email for the located user, reject a missing stored address, send only to that stored address, then rebuild and redeploy. The repair must be reapplied if an affected template generates the handlers again.

The same announcement also notes that an earlier advisory, [CVE-2026-70175](https://github.com/dotnet/aspnetcore/security/advisories/GHSA-x3vp-85cm-q97r), received updated guidance in November. That advisory originally shipped runtime and `Microsoft.AspNetCore.Identity.UI` package fixes in October; the November update adds corrected SDK templates for the Blazor account-recovery handlers, by SDK feature band. Its remediation lists three separate steps: install the corrected runtime or SDK and update package references; independently repair existing generated handlers; and rebuild and redeploy. It states explicitly that updating a runtime, SDK, Identity UI package, or template package does not perform the source repair for application-owned generated code.

Read together, these advisories show why "we installed the November runtime" can be complete evidence for one CVE, incomplete for another, and irrelevant to a third.

---

## 4. Map Each Component to an Owner and a Remediation

Once the inventory and the advisory are side by side, each affected row maps to an owner, an action, and the evidence that the action reached production.

| Component named by the advisory | Owner | Remediation | When patching alone is enough | Evidence of remediation |
| --- | --- | --- | --- | --- |
| Shared runtime pack (framework-dependent app) | Host or image maintainer | Install the corrected runtime or rebuild the base image; restart the application | Often, when the advisory names only runtime packs and the app references no affected standalone package | Runtime version reported by the running process, and image digest or host patch record |
| Embedded runtime (self-contained or single-file app) | Application team | Rebuild and republish with an SDK that targets the corrected runtime; redeploy | Never by patching the host: the runtime is inside the deployment | Build log showing the runtime pack version, and the deployed artifact's identifier |
| App-local NuGet package | Application team | Update the reference to the fixed package version, restore, rebuild, redeploy | Never by patching the runtime, unless the advisory says the runtime supplies that code | Resolved version in `packages.lock.json` or the build's dependency output, and the deployed artifact's identifier |
| Scaffolding package, template pack, or tool | Developers and build-environment maintainers | Update the tool, project dependency, template pack, or SDK before generating more code; check `global.json` selection | Only for code generated *after* the update | Tool or template version on developer machines and CI agents |
| Previously generated or copied source | Application team | Locate every affected file, apply the advisory's source repair, add a test, rebuild, redeploy | Never: no update you install changes committed source | Commit that changes the file, a test that fails before and passes after, and the deployed artifact built from that commit |

Two cautions apply to the "enough" column. It is a reading aid, not a substitute for the advisory, and it depends on deployment mode: a framework-dependent application and a self-contained one can need different actions for the same CVE. And "rebuild and redeploy" means the artifact that runs in production is replaced by one built from the corrected inputs. A rebuild that is never deployed changes nothing that users reach.

---

## 5. A Small Generated File, Its Repair, and Its Evidence

To keep the example small and independent of Microsoft's templates, this section does not reproduce the Identity handlers. It uses a fictional internal template, *Fabrikam.Templates.Web*, which generates a team-invitation feature into new services. Fabrikam's template team later publishes a fictional advisory: version 3.2.0 of the template generated a handler that resends an invitation to the address supplied in the request, rather than to the invitee address stored on the invitation. Template version 3.2.1 fixes the template. The advisory says, as Microsoft's did, that updating the template does not change services already generated from it.

The order service was generated from version 3.2.0. Its handler, as generated:

```csharp
namespace Fabrikam.Orders.TeamInvitations;

public sealed class ResendInvitationHandler(IInvitationStore store, IMessageSender sender)
{
    public ResendInvitationResult Handle(ResendInvitationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invitation = store.Find(request.InvitationCode);
        if (invitation is null)
        {
            return ResendInvitationResult.NotFound;
        }

        sender.Send(request.Email, "Your team invitation", $"Invitation code: {invitation.Code}");
        return ResendInvitationResult.Sent;
    }
}
```

The defect is one argument: the recipient comes from the request rather than from the record that was located. Updating the service's runtime, SDK, or template pack leaves this file byte-for-byte unchanged.

### Make the focused repair

The repair changes only the recipient and adds the check that the stored address exists. The lookup and the not-found behavior stay as they were:

```csharp
// Generated by Fabrikam.Templates.Web 3.2.0 (fictional). This file is application-owned source.
// Security repair: send only to the stored invitee address (Fabrikam template advisory FAB-2026-07, fictional).
namespace Fabrikam.Orders.TeamInvitations;

public sealed class ResendInvitationHandler(IInvitationStore store, IMessageSender sender)
{
    public ResendInvitationResult Handle(ResendInvitationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var invitation = store.Find(request.InvitationCode);
        if (invitation is null)
        {
            return ResendInvitationResult.NotFound;
        }

        var storedEmail = invitation.InviteeEmail;
        if (string.IsNullOrEmpty(storedEmail))
        {
            throw new InvalidOperationException("Invitations must have a stored invitee address.");
        }

        sender.Send(storedEmail, "Your team invitation", $"Invitation code: {invitation.Code}");
        return ResendInvitationResult.Sent;
    }
}
```

The header comment is deliberate. It records where the file came from and why it now differs from what its generator produced. The next person to compare the service with a template baseline will see a difference and know it is intended.

### Prove the repair with a test

A remediation claim should rest on something that would fail if the repair were missing. The test below fails against the generated handler and passes against the repaired one:

```csharp
[Fact]
public void SendsOnlyToTheStoredInviteeAddress()
{
    var store = new InMemoryInvitationStore(new Invitation("INV-1", "invitee@example.test"));
    var sender = new RecordingSender();
    var handler = new ResendInvitationHandler(store, sender);

    var result = handler.Handle(new ResendInvitationRequest("INV-1", "someone-else@example.test"));

    Assert.Equal(ResendInvitationResult.Sent, result);
    var message = Assert.Single(sender.Messages);
    Assert.Equal("invitee@example.test", message.Recipient);
}
```

The appendix adds a test for an invitation with no stored address, which must send nothing, and a test that an unknown code still sends nothing. Against the generated handler, the first two tests fail; the third passes, which confirms the repair did not change the not-found path.

The test asserts the property the advisory cares about: who receives the message. It does not try to reproduce an attack, and it does not need to.

### Record the evidence

For each affected component, the remediation record should let someone else confirm the repair without trusting anyone's memory:

1. **Advisory and scope.** The advisory identifier, the components it names, and the inventory rows that matched. Record the rows that did *not* match and why.
2. **Provenance.** For generated code: the generator and version that produced it, and how that was determined (a provenance header, a commit message, a template manifest, or a comparison with a known baseline).
3. **Files changed.** Each repaired file, with the commit that changed it. Search for every copy: a handler generated once may have been copied into a second service.
4. **Test.** The test that fails without the repair and passes with it, and the CI run where it passed.
5. **Component versions.** The runtime version the process reports, the resolved versions of affected packages, and, for self-contained applications, the runtime pack version used at publish.
6. **Deployed artifact.** The image digest, package version, or build number now running in each environment, and the commit it was built from.

Item 6 closes the loop. Items 1–5 show the repair exists; item 6 shows that the repaired artifact is the one users reach.

---

## 6. Why Scanners May Not See This

Dependency tooling is valuable, and every remediation workflow should use it. It answers a specific question: *which identified third-party components, at which versions, does this application depend on?* A defect generated into first-party source falls outside that question.

- **NuGet vulnerability auditing** (`dotnet list package --vulnerable`, or restore-time auditing) compares package identifiers and versions with advisory data. Generated source is not a package. A project may not even reference the scaffolding package anymore; many teams remove it after generation.
- **Lock files** record the resolved versions of packages. They say nothing about what source a template wrote into the repository.
- **An SBOM** inventories components by identity and version. Generated code is reported, at most, as part of the application's own component, with no link to the template that produced it.
- **Container image scanners** inspect operating-system packages and, often, the runtime and libraries in the image. They can confirm the shared runtime was patched; they cannot tell whether a handler in the application's own assembly sends mail to the right address.

Some static-analysis tools may catch some patterns in source, and a code search for the handler names an advisory lists is often the fastest first pass. Neither is a guarantee. That is why the advisory's own instruction, to check the generated code itself, is part of the remediation rather than a formality, and why the provenance in your inventory matters: you can search only for code you know you have.

---

## 7. Use a Clean Baseline for Comparison, Not for Overwriting

When a template ships a fix, an obvious idea is to regenerate the application from the corrected template. A clean baseline, which is a reproducible copy of what a template version generates with your options, is genuinely useful here, but mostly as a reference:

- **It locates the change.** Generating the old and new template versions with the same options and comparing them shows exactly which files and lines the template fix changed.
- **It separates intent from drift.** Comparing your application with the old baseline shows which generated files you customized. An unmodified generated file can often take the template's corrected version directly; a customized one needs a deliberate merge.
- **It supports future reviews.** A recorded baseline makes the next advisory's "which of our files came from this template?" question cheaper to answer.

Regenerating the whole application in place, or forcing new template output over existing files, is a different matter. It can silently discard customizations, reintroduce defaults the team intentionally changed, and produce a diff so large that the security-relevant change is hard to review. For a security repair, a focused change to the named files, with a test, is usually safer and easier to verify than a broad regeneration. Reconciling a whole application with a newer template generation is a separate, planned piece of work. [Issue #530](https://github.com/AsiBackbone/Learning/issues/530) tracks guidance on comparing old and new generated baselines file by file without overwriting customizations, and [You Don't Need to Roll Back What You Never Merged](you-dont-need-to-roll-back-what-you-never-merged.md) covers isolating a larger platform-generation change on a candidate branch.

As one bounded implementation example, ReferenceApplication's [baseline refresh walkthrough](https://asibackbone.github.io/ReferenceApplication/walkthrough/refresh-ncat-baseline.html) shows how a single application keeps a reproducible template baseline and refreshes it. It illustrates the mechanics; it is not a requirement for applying this article.

---

## 8. Verified Remediation Is Not a Security Guarantee

The evidence above supports a narrow, useful claim: *for this advisory, the named components in this deployed artifact now contain the described fix, and a test confirms the property the fix restores.* That is what a remediation record should say.

It does not say:

- that the application has no other vulnerabilities;
- that every copy of the generated code everywhere in the organization was found, unless the search is recorded and its scope stated;
- that the advisory's description of the defect is complete, or that a later revision of the advisory will not add components or steps (CVE-2026-70175's November update is an example of guidance changing after initial publication);
- or that a passing CI pipeline amounts to a security assessment.

Keeping the claim narrow is what makes it trustworthy. A record that says "remediated" without saying *what* was remediated, *where*, and *how it was verified* invites exactly the mistake that opened this article.

---

## A Short Review Checklist

1. Does your inventory distinguish the shared runtime, any embedded runtime, app-local packages, generated or copied source, build tooling, and deployment artifacts?
2. Do you record which template or scaffolder generated each part of an application, and at which version?
3. For each advisory, did you read the affected-component tables and the remediation section rather than only the patched runtime version?
4. For each affected component, is there a named owner and a remediation that matches the component type and the deployment mode?
5. If the advisory names generated code, did you search for every affected file, including copies in other services?
6. Is each source repair focused on the named change, marked with its provenance, and covered by a test that fails without it?
7. Were updated scaffolding tools, template packs, and SDK selections in place before anyone generated more code?
8. Does the record identify the deployed artifact that contains the repair, not just the commit or the build?
9. Does the remediation record state its scope, and avoid claiming that the application is secure?

---

## Continue Deeper

[A Green CI Badge Does Not Prove Your .NET Package Is Trustworthy](ci-badge-does-not-prove-package-integrity.md) makes the parallel point about evidence: each check answers a specific question, and a release decision needs to know which ones were answered.

[Software Supply-Chain Integrity for .NET Repositories](../../security/software-supply-chain-integrity-for-dotnet-repositories.md) covers dependency auditing, lock files, provenance, and SBOMs in more depth, including what each can and cannot establish.

[You Don't Need to Roll Back What You Never Merged](you-dont-need-to-roll-back-what-you-never-merged.md) explains how to keep a reproducible template baseline and evaluate a new platform generation on an isolated candidate, which is the larger, planned counterpart to a focused security repair.

### Related Work

- Microsoft's [November 2026 ASP.NET Core security updates announcement](https://github.com/dotnet/announcements/issues/504) lists the month's thirteen ASP.NET Core advisories and the CVE-2026-70175 follow-up.
- The [CVE-2026-118741 announcement](https://github.com/dotnet/announcements/issues/525) and its [published advisory](https://github.com/dotnet/Scaffolding/security/advisories/GHSA-jxmf-523q-f7r7) describe the affected scaffolding packages, templates, and tools, and the manual repair for previously generated Identity handlers. Questions go to its [discussion issue](https://github.com/dotnet/Scaffolding/issues/3959).
- The [CVE-2026-70175 advisory](https://github.com/dotnet/aspnetcore/security/advisories/GHSA-x3vp-85cm-q97r) separates the runtime, Identity UI package, SDK template, and generated-code remediation steps.
- [.NET and .NET Framework November 2026 servicing updates](https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-november-2026-servicing-updates/) lists the month's runtime releases and CVEs, including the standalone `System.Formats.Cbor` package fix.

---

## The Short Answer

A .NET runtime or SDK update fixes code that ships in that runtime or SDK. It does not change package versions your project pins, it does not replace the runtime inside a self-contained deployment, and it never rewrites source that a template or scaffolder generated into your application. Read each advisory by the components it names, map each component to the people who own it, and apply the remediation that component needs: patch the runtime or image, update the package, or repair the generated source with a focused, tested change. Then rebuild and redeploy where the advisory requires it, and record which deployed artifact contains the fix. For many advisories the runtime update really is the whole job; the November 2026 ASP.NET Core advisories show that you have to read each one to know.

---

## Appendix: Complete Code

The sample has two projects: a class library containing the repaired handler and a test project. Every file below was compiled and run as shown.

### Environment used for verification

- .NET SDK 10.0.404, target framework `net10.0`, on Windows x64.
- xUnit v3 4.0.3, run through Microsoft.Testing.Platform, which `global.json` selects for `dotnet test` on the .NET 10 SDK.
- Warnings treated as errors, with the `latest-recommended` .NET analyzers.
- The command `dotnet test --project Tests/Invitations.Tests.csproj`, run from the folder that contains `global.json`, built both projects and reported 3 passing tests. With the generated handler from [section 5](#5-a-small-generated-file-its-repair-and-its-evidence) substituted for the repaired one, the same command reported 2 failing tests and 1 passing test.

### Project files

```text
global.json
App/Invitations.csproj
App/TeamInvitations/InvitationContracts.cs
App/TeamInvitations/ResendInvitationHandler.cs
Tests/Invitations.Tests.csproj
Tests/ResendInvitationHandlerTests.cs
```

`global.json`:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

`App/Invitations.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

</Project>
```

`Tests/Invitations.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="4.0.3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../App/Invitations.csproj" />
  </ItemGroup>

</Project>
```

### Application code

`App/TeamInvitations/InvitationContracts.cs`:

```csharp
namespace Fabrikam.Orders.TeamInvitations;

public sealed record Invitation(string Code, string? InviteeEmail);

public sealed record ResendInvitationRequest(string InvitationCode, string Email);

public enum ResendInvitationResult
{
    Sent,
    NotFound,
}

public interface IInvitationStore
{
    Invitation? Find(string code);
}

public interface IMessageSender
{
    void Send(string recipient, string subject, string body);
}
```

`App/TeamInvitations/ResendInvitationHandler.cs` is the repaired handler shown in [section 5](#make-the-focused-repair).

### Tests

`Tests/ResendInvitationHandlerTests.cs`:

```csharp
using Fabrikam.Orders.TeamInvitations;
using Xunit;

namespace Fabrikam.Orders.Tests;

public sealed class ResendInvitationHandlerTests
{
    [Fact]
    public void SendsOnlyToTheStoredInviteeAddress()
    {
        var store = new InMemoryInvitationStore(new Invitation("INV-1", "invitee@example.test"));
        var sender = new RecordingSender();
        var handler = new ResendInvitationHandler(store, sender);

        var result = handler.Handle(new ResendInvitationRequest("INV-1", "someone-else@example.test"));

        Assert.Equal(ResendInvitationResult.Sent, result);
        var message = Assert.Single(sender.Messages);
        Assert.Equal("invitee@example.test", message.Recipient);
    }

    [Fact]
    public void RejectsAnInvitationWithoutAStoredAddress()
    {
        var store = new InMemoryInvitationStore(new Invitation("INV-2", InviteeEmail: null));
        var sender = new RecordingSender();
        var handler = new ResendInvitationHandler(store, sender);

        Assert.Throws<InvalidOperationException>(
            () => handler.Handle(new ResendInvitationRequest("INV-2", "someone@example.test")));
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public void SendsNothingForAnUnknownInvitation()
    {
        var store = new InMemoryInvitationStore();
        var sender = new RecordingSender();
        var handler = new ResendInvitationHandler(store, sender);

        var result = handler.Handle(new ResendInvitationRequest("INV-404", "someone@example.test"));

        Assert.Equal(ResendInvitationResult.NotFound, result);
        Assert.Empty(sender.Messages);
    }

    private sealed class InMemoryInvitationStore(params Invitation[] invitations) : IInvitationStore
    {
        public Invitation? Find(string code) =>
            invitations.SingleOrDefault(invitation => invitation.Code == code);
    }

    private sealed record SentMessage(string Recipient, string Subject, string Body);

    private sealed class RecordingSender : IMessageSender
    {
        public List<SentMessage> Messages { get; } = [];

        public void Send(string recipient, string subject, string body) =>
            Messages.Add(new SentMessage(recipient, subject, body));
    }
}
```
