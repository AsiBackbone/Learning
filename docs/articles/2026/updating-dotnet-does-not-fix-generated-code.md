---
description: A runtime or SDK update does not rewrite source a template already generated into your app. Map each advisory to its component, then repair and prove it.
title: Updating .NET Did Not Fix the Code Your Template Generated
author: Christopher D. Cavell
published: "2026-11-24"
summary: A .NET runtime or SDK update repairs what Microsoft ships in that runtime or SDK. It does not rewrite source that a template or scaffolder generated into your repository, it does not change package versions your project pins, and it does not replace the runtime inside an already published self-contained deployment. Read each security advisory for the components it names, map each component to the team that maintains it, apply the remediation that component needs, and keep evidence that the deployed artifact contains the repair. Three advisories from Microsoft's November 2026 release cycle show the runtime, standalone-package, and generated-source paths side by side.
feed: true
x_post: Patching .NET fixes the runtime, not code your template generated. That source is yours to repair and redeploy.
x_hashtags:
  - DotNet
  - DevSecOps
x_question: How does your team find code a template generated years ago?
---

# Updating .NET Did Not Fix the Code Your Template Generated

*Who repairs generated source when a security advisory names it*

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** No formal prerequisites. Familiarity with how a .NET application is built and deployed, NuGet package references, and `dotnet new` or a scaffolding tool is helpful. No AsiBackbone package, NetCoreApplicationTemplate, or prior Learning material is required.

**Sample scope:** The patch-day scenario, its team and service, the Cedarline template, and the Cedarline advisory are fictional. The Microsoft advisories are real and are cited as the November 2026 case study; Microsoft version numbers come only from those advisories and Microsoft's servicing post. The code example is a small, non-exploitable illustration of a generated handler and its repair, not a copy of Microsoft's templates. This article does not claim that every 2026 ASP.NET Core advisory affects generated source, that every `dotnet new` template or scaffolding release is affected, or that a runtime update is never sufficient. In this article, the **owner** of a component is the team or role responsible for maintaining and remediating it, not necessarily the holder of any copyright in it.

**Verification:** The handler and its three tests in the [appendix](#appendix-complete-code) were compiled and run on .NET 10; the appendix records the [environment and command](#environment-used-for-verification). The unrepaired handler shown in the article fails two of those tests. Advisory content is summarized in the article's own words, with links, and each advisory remains the authority for its own affected versions and remediation. External sources were last verified on November 24, 2026.

**License:** Original prose is licensed under [Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/), and original code examples are additionally offered under the [MIT License](https://opensource.org/license/mit), as described in the repository's [licensing terms](https://github.com/AsiBackbone/Learning/blob/main/LICENSING.md). Third-party material, where identified, remains under its original license.

**What this article covers:** a servicing update that passed the team's configured runtime checks while a generated file in the application's repository stayed unchanged; inventorying what an application actually contains and who maintains each part; reading an advisory by component rather than by one version number; how three November 2026 advisories separate runtime fixes, standalone packages, and previously generated code; a [component, owner, remediation, and evidence table](#4-map-each-component-to-an-owner-and-a-remediation); a small generated-file repair with a test that proves it; why conventional package scans, lock files, and SBOMs can miss a defect that lives in first-party source; what a clean template baseline is good for, and when a focused fix is safer than regenerating; and why verified remediation is not a promise that an application is secure.

**In plain terms:** a car manufacturer issues a recall for a brake part. Your dealer replaces that part during the next service. If, years earlier, you had a local mechanic fabricate a copy of the old part for a trailer you built yourself, the dealer never sees the trailer and never touches it. The recall notice may even say so. The trailer is yours to inspect and repair.

Consider a fictional patch-day scenario. A team runs a customer-facing order service on ASP.NET Core 10. Its platform engineer updates the base container image to one containing the corrected ASP.NET Core runtime, rebuilds the image, and redeploys. The pipeline's runtime inventory reports the corrected shared framework present in the environment of every deployed application instance, although it does not check which version each running process loaded. The package scan reports no findings among the resolved packages and advisory sources it examined. The change ticket is closed as "remediated: runtime updated."

Two days later a reviewer reads one of the month's advisories more carefully. It concerns code that a scaffolding tool *generates into applications*, and it says that a tool update leaves already generated code as it was. The order service's account-recovery pages were scaffolded two years ago and later customized. The project has since moved to .NET 10, but that upgrade changed project files and package references, not those pages. They live in the team's repository, compile into the team's assembly, and have not changed in eighteen months. Nothing in the patch-day work touched them, and nothing in the pipeline was designed to notice.

> **A runtime or SDK update repairs what ships inside that runtime or SDK. Source that a template or scaffolder generated once and committed into your repository becomes application-maintained source. Updating the generator does not change that committed copy. Repairing the deployed application requires correcting that source, rebuilding, and deploying the corrected artifact.**

For many advisories, installing the corrected runtime and restarting a framework-dependent application is the complete remediation. What decides it is the component the advisory names and how the application is built and deployed, not an SDK or runtime version that `dotnet --info` reports outside the deployed process.

---

## Why This Recurs

Servicing is usually discussed in terms of versions: the runtime moved from one patch to the next, the SDK moved to a new feature band, the container image was rebuilt. Those numbers are easy to collect and easy to compare against an advisory's "patched version" column, so they become the evidence of remediation.

A .NET application, though, is assembled from parts that are serviced in different ways:

- some code arrives from a **shared runtime** that the host or container supplies when the application starts;
- some arrives from **NuGet packages** that the project references, whose applicable assets the build includes in its output;
- some is **embedded in the deployment** itself, as when a self-contained application carries its own copy of the runtime;
- and some was **generated once** by `dotnet new`, a scaffolder, or an internal template, then committed and edited like any other source.

Installing a shared runtime directly changes only the first category. It does not rewrite committed source, update package references, or replace the runtime embedded in an already published self-contained artifact. Those components need their own update and, where the advisory says so, a rebuild and redeployment. Templates make the last category easy to forget, because the code did not feel like something the team wrote. It arrived complete, it worked, and over time it became indistinguishable from the rest of the repository.

---

## 1. A Patch That Passed Its Runtime Checks

Return to the order service. Here is what the patch-day evidence actually showed:

| Check | Result | What it actually proves |
| --- | --- | --- |
| Base image updated and rebuilt | New image digest | A different image was built. That is not evidence of production remediation until the corrected component is verified in that image and the same digest is confirmed as deployed and running |
| Runtime inventory in each deployed instance | Corrected shared framework present | The corrected shared framework is *available* in the container or host environment; separate process-level evidence shows which version the application loaded |
| Package vulnerability scan | No findings | Within the scanner's configured sources and advisory data at scan time, no resolved package version it examined matched a reported vulnerability |
| Smoke tests | Passing | The service starts and its tested paths behave as before |
| Account-recovery pages compared at the source commits of the old and new deployed images | Identical | The source behind both deployments contains the same generated handlers; together with verified build provenance (each deployed image digest maps to a build from the stated commit), this shows the deployed application kept the generated code it had before the patch |

Every row is true, and each is evidence for the components it covers. A clean package scan, for example, is evidence about the package identifiers and versions the scanner recognized; it is not evidence that application-maintained source is unaffected. None of the first four rows says anything about the last one. The account-recovery handlers are not part of the runtime, are not a package, and are not something the scanner enumerates. In this service they compile into the application assembly, generated from a template version that predates the fix.

---

## 2. Inventory What the Application Actually Contains

Before reading any advisory, list the components that end up in the deployed artifact and who maintains each. A practical inventory for an ASP.NET Core service has at least these entries:

| Component | Where it comes from | Who changes it |
| --- | --- | --- |
| Shared runtime (`Microsoft.NETCore.App`, `Microsoft.AspNetCore.App`) | Host machine or base container image, for framework-dependent apps | Whoever patches the host or rebuilds the image |
| Embedded runtime | Copied into a self-contained deployment, including a self-contained single-file publish | The application team, by republishing and redeploying |
| App-local NuGet packages | `PackageReference` and transitive dependencies, resolved at restore and build time; applicable runtime assets are included in the output | The application team, by updating references and rebuilding |
| Generated or scaffolded source | `dotnet new`, a scaffolding tool, or an internal template, committed to the repository | The application team, by editing source |
| Copied source | Files copied from samples, an internal starter kit, or a vendored framework | The team that took the copy |
| Custom template or generator forks | An internal fork of a template or scaffolder | The fork's maintainers, for future output; teams that already generated code from it repair their own copies |
| SDK and build tooling | Developer machines and CI agents; selected by `global.json` | Whoever maintains build environments |
| Container and deployment artifacts | Image, package, or bundle deployed to the running environment | The release process, which must rebuild from the corrected inputs |

Three details matter in that table.

First, single-file and self-contained are different choices. A single-file application can be framework-dependent, in which case it uses the shared runtime like any other framework-dependent app. Only a self-contained publish carries its own runtime.

Second, a generated file has different provenance from handwritten application code, but once it is committed it is maintained by the application team. The generator no longer controls or updates that committed copy. Record its provenance anyway: which generator, which version, and when. Without that record, the team may have to reconstruct provenance from commit history, manifests, dependency records, or baseline comparisons before it can reliably answer an advisory's first scoping question.

Third, the SDK appears in the inventory even though it does not run in production. It can bundle the templates that generate code, and `global.json` can keep an older SDK selected after a newer one is installed. For a self-contained publish, the selected SDK and the project's publish inputs together resolve the runtime pack that is embedded; an explicit `RuntimeFrameworkVersion` can control that version. None of this reaches back into code generated earlier.

---

## 3. Read the Advisory by Component, Not by Version Number

A .NET security advisory may identify several affected **components** and versions, including runtime packs, standalone NuGet packages, SDK templates, tools, or generated application code, rather than one product-wide version. Read each row as a statement about one component:

- **What component is affected?** A runtime pack, a standalone NuGet package, a scaffolding package, a template package, or a tool.
- **What is the fixed version of that component?** Not the runtime version, unless the row is a runtime pack.
- **What does the remediation section say to do?** Install, update a reference, rebuild, redeploy, restart, or change source.
- **Does it mention code that already exists in applications?** That is the signal that the fix cannot be delivered by updating anything you download.

A single patched-version value is not always enough to establish remediation across every component and deployment mode an advisory names. One release cycle can contain a runtime fix for one CVE, a package that ships independently of the runtime for another, and a manual source repair for a third.

### The November 2026 case: three remediation paths in one release cycle

On November 10, 2026, Microsoft released .NET servicing updates. [Microsoft's November servicing post](https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-november-2026-servicing-updates/) lists that day's runtime releases (.NET 10.0.15, 9.0.23, and 8.0.34, together with the initial .NET 11.0.0 release) and the CVEs addressed across .NET, ASP.NET Core, and Visual Studio. Separately, a [consolidated ASP.NET Core announcement](https://github.com/dotnet/announcements/issues/504) collects thirteen new ASP.NET Core advisories. It says that each advisory remains the authority for its own affected versions and remediation, that required actions differ by CVE, and that a runtime or SDK update neither replaces every standalone package nor repairs every application. Three advisories from the same release cycle illustrate the three paths. Only the first is one of the thirteen in that announcement. The other two have their own announcements.

**A runtime fix, with a standalone package exception: CVE-2026-114146.** This [SignalR advisory](https://github.com/dotnet/aspnetcore/security/advisories/GHSA-67wp-ghc2-fw2g), one of the thirteen, lists ASP.NET Core runtime packs for .NET 8, 9, 10, and 11 with their patched runtime versions. It separately lists one standalone ASP.NET Core 2.3 package, `Microsoft.AspNetCore.Http.Connections`, fixed in version 1.2.14. Its remediation section directs teams to install the applicable runtime or update each independently referenced package. After a standalone package update, the application must be rebuilt and redeployed, because restarting does not replace app-local packages. A self-contained application must also be rebuilt and redeployed, because a machine-wide runtime update leaves its embedded runtime as it was. An SDK update by itself does not change an application's standalone package references. For a framework-dependent .NET 10 application that does not resolve the affected standalone package, the corrected runtime and a restart are what this advisory asks for.

**A standalone package that needs its own update: CVE-2026-118747.** This [.NET advisory](https://github.com/dotnet/runtime/security/advisories/GHSA-vmv3-wvjp-wqpj) names [its own announcement](https://github.com/dotnet/announcements/issues/501), which was later closed as a duplicate of the consolidated [November 2026 .NET Runtime security updates announcement](https://github.com/dotnet/announcements/issues/498) that also lists it. It concerns the independently versioned `System.Formats.Cbor` NuGet package. For the .NET 9 package line the fix is package version 9.0.24, which is not delivered by the 9.0.23 runtime. The advisory also lists a fixed 10.0.15 package, which includes a `net9.0` target, as an alternative; follow the advisory and your own compatibility policy when choosing a package line. The servicing post likewise says that installing the 9.0.23 runtime does not address this CVE, and directs teams to update the package, rebuild, and redeploy. A team that compares only its runtime version with the month's runtime release would record this one as fixed when it is not.

**Generated application code: CVE-2026-118741.** This [scaffolding advisory](https://github.com/dotnet/Scaffolding/security/advisories/GHSA-jxmf-523q-f7r7) has its own [announcement](https://github.com/dotnet/announcements/issues/525). It concerns ASP.NET Core Identity scaffolding templates that, in password-reset and email-confirmation flows, address the message to the email supplied in the request rather than the address stored on the located account. For the stable .NET 8, 9, and 10 lines, the affected components include:

- the scaffolding package `Microsoft.VisualStudio.Web.CodeGenerators.Mvc`, which contains the affected templates, and `Microsoft.VisualStudio.Web.CodeGeneration.Design`, which brings it in through a minimum-version dependency (listed as affected for versions >= 8.0.0, <= 8.0.23; >= 9.0.0, <= 9.0.12; and >= 10.0.0, <= 10.0.2, and fixed in 8.0.24, 9.0.13, and 10.0.3). The advisory notes that a project whose resolved MVC generator is already fixed is not vulnerable merely because it keeps an older Design reference, so assess the resolved `CodeGenerators.Mvc` version to decide whether future scaffolding uses a corrected template, then inspect the committed handlers separately to decide whether existing source needs repair;
- the standalone `Microsoft.DotNet.Web.ProjectTemplates` packages (listed as affected for versions >= 8.0.0, <= 8.0.33; >= 9.0.0, <= 9.0.22; and >= 10.0.0, <= 10.0.14, and fixed in 8.0.34, 9.0.23, and 10.0.15);
- and the `dotnet-scaffold` tools. The advisory directs `Microsoft.dotnet-scaffold` installations to update to 20.3.0, and installations of the older `Microsoft.dotnet-scaffold-aspnet` to be uninstalled in favor of `Microsoft.dotnet-scaffold` 20.3.0. That tool is installed as a .NET tool, not a project package reference, and 20.3.0 is not a version of the MVC or Design packages.

The servicing post lists this CVE against .NET 8, 9, and 10. The advisory also covers the 20.x scaffolding tool and affected prerelease versions, including .NET 11 previews, whose generated code needs the same repair, and gives tool-specific and Visual Studio remediation. Consult its current affected-packages and remediation sections for those environments.

The advisory's FAQ and remediation sections draw three distinctions that this article depends on:

- Updating a scaffolding package or tool changes later scaffolding operations; it does not modify affected source already generated into an application. An application generated with an affected template stays affected until its relevant handlers are corrected.
- Exposure depends on two facts: the scaffolding package or tool version, *and the code that version generated*. The `dotnet-aspnet-codegenerator` command loads generators from the project's own dependencies, so a newer command-line tool alone leaves those references unchanged. For `dotnet new`, the selected SDK and any separately installed template packs matter, and a package reference alone does not reveal which template produced an application.
- Existing applications need a described manual repair. In the generated `ForgotPassword` and `ResendEmailConfirmation` handlers, including Blazor equivalents, keep the existing user lookup and checks, obtain the stored email for the located user, reject a missing stored address, send only to that stored address, then rebuild and redeploy. If an affected template generates the handlers again, the repair has to be applied again.

The advisory's ranges start with the .NET 8 scaffolding and template lines. Code generated by older templates is outside its stated affected ranges. That does not establish that such code is safe; if comparable handlers exist, reviewing them is a prudent separate assessment, not a step the advisory requires.

The consolidated announcement also notes that an earlier advisory, [CVE-2026-70175](https://github.com/dotnet/aspnetcore/security/advisories/GHSA-x3vp-85cm-q97r), received updated guidance in November and is separate from the thirteen. That advisory was first published on October 13 with runtime and `Microsoft.AspNetCore.Identity.UI` package fixes. Its November 10 revision (V1.1, still the latest revision when this article was verified on November 24) lists corrected SDK templates for the Blazor account-recovery handlers, with a separate affected range and fixed SDK version for each SDK feature band (for example, 10.0.115 for the 10.0.1xx band and 10.0.404 for the 10.0.4xx band), and adds guidance for repairing previously generated code. Its remediation has three separate steps: install the corrected runtime or SDK and update package references; independently repair existing generated handlers; and rebuild and redeploy. It states that installing a runtime, SDK, Identity UI package, or template package does not perform the second step.

Read together, these advisories show why installing the November runtime can be the complete remediation for one CVE, only part of the remediation for another, and irrelevant to a third. Even where it is complete, as for a framework-dependent app affected only through its shared framework, verify that the deployed process loaded the corrected version before closing the advisory.

---

## 4. Map Each Component to an Owner and a Remediation

Once the inventory and the advisory are side by side, each affected row maps to an owner, an action, and the evidence that the action reached production.

| Component named by the advisory | Owner | Remediation | When patching alone is enough | Evidence of remediation |
| --- | --- | --- | --- | --- |
| Shared runtime pack (framework-dependent app) | Host or image maintainer | Install the corrected runtime or rebuild the base image; restart the application | Often, when the advisory names only runtime packs and the application resolves no affected standalone package | Exact shared-framework version resolved by the deployed process, collected from the running environment or deployment diagnostics, with the image digest or host patch record |
| Embedded runtime (self-contained app, including self-contained single-file) | Application team | Republish so that the corrected runtime pack is resolved, updating `RuntimeFrameworkVersion` if the project pins it; verify the resolved version; redeploy | Never by patching the host: the runtime is inside the deployment | Resolved runtime pack version from the build or publish record, tied to the deployed artifact's identifier. For a folder publish, `includedFrameworks` in the published `*.runtimeconfig.json` is another place to check |
| App-local NuGet package | Application team | Change the direct reference, centrally managed version, or dependency that controls resolution so that restore selects a fixed version; then restore, rebuild, and redeploy | Never by installing a runtime: the package is resolved through the application's dependency graph, and its applicable assets are included in the build or publish output | Resolved version in `packages.lock.json` when lock files are enabled, or in versioned restore or build dependency output, and the deployed artifact's identifier |
| Scaffolding package, template pack, or tool | Developers and build-environment maintainers | Update the tool, project dependency, template pack, or SDK before generating more code; check which SDK `global.json` selects | The update corrects this advisory's defect for subsequent generation only; it does not remediate source generated earlier | Resolved tool, project dependency, template pack, and selected SDK versions in the environment that performs generation, with a clean-generation check where practical |
| Previously generated or copied source | Application team | Locate every affected file, apply the advisory's source repair, add a test, rebuild, redeploy | Never through a runtime, SDK, tool, or package installation alone; the committed source must be corrected or replaced | Commit that changes the file, a test that fails before and passes after, and the deployed artifact built from that commit |

Each row's evidence should also name the advisory identifier and revision that was evaluated, because guidance can change after publication. The "enough" column is a reading aid, not a substitute for the advisory, and it depends on deployment mode: a framework-dependent application and a self-contained one can need different actions for the same CVE. "Rebuild and redeploy" means the artifact that runs in production is replaced by one built from the corrected inputs. A rebuild that is never deployed changes nothing that users reach.

---

## 5. A Small Generated File, Its Repair, and Its Evidence

To keep the example small and independent of Microsoft's templates, this section does not reproduce the Identity handlers. It uses a fictional internal template, *Cedarline.Templates.Web*, which generates a team-invitation feature into new services. Cedarline's template team later publishes a fictional advisory: version 3.2.0 of the template generated a handler that resends an invitation to the address supplied in the request, rather than to the invitee address stored on the invitation. Template version 3.2.1 fixes the template. The advisory says that services already generated from 3.2.0 keep the old handler until someone changes it.

Suppose the same order service also contains a team-invitation feature generated from Cedarline 3.2.0. This is a separate, smaller illustration of the same ownership problem, not a model of the account-recovery pages. The handler, as generated:

```csharp
namespace Cedarline.Orders.TeamInvitations;

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

The central defect is one argument: the recipient comes from the request rather than from the record that was located. The repair also has to define what happens when that stored address is absent. Installing a newer runtime, SDK, or template pack leaves this file byte-for-byte unchanged.

### Make the focused repair

The repair changes only the recipient and adds the check that the stored address exists. The lookup and the not-found behavior stay as they were:

```csharp
// Generated by Cedarline.Templates.Web 3.2.0 (fictional). This file is application-maintained source.
// Security repair: send only to the stored invitee address (Cedarline template advisory CDL-2026-07, fictional).
namespace Cedarline.Orders.TeamInvitations;

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

The sample treats a missing stored address as corrupt application state and throws, having sent nothing; the repair snippet in the CVE-2026-118741 advisory also throws `InvalidOperationException` in that case. That is not the only valid policy. A public-facing endpoint may instead keep its outward response uniform, record the invalid state internally, and send nothing. Whichever policy applies, test it explicitly and make sure the branch does not reveal whether an account or stored address exists.

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

For each affected component, the remediation record should let someone else confirm the repair without trusting anyone's memory. The table in section 4 names the evidence per component; for a source repair, the record should also cover:

1. **Advisory and scope.** The advisory identifier and revision, the components it names, and the inventory rows that matched. Record the rows that did *not* match and why.
2. **Provenance.** The generator and version that produced the code, and how that was determined (a provenance header, a commit message, a template manifest, or a comparison with a known baseline).
3. **Files changed.** Each repaired file, with the commit that changed it. Search for every copy: a handler generated once may have been copied into a second service.
4. **Test.** The test that fails without the repair and passes with it, and the CI run where it passed.
5. **Deployed artifact.** The image digest, package version, or build number now running in each environment, and the commit it was built from.

Item 5 closes the loop. Items 1–4 show the repair exists; item 5 shows that the repaired artifact is the one users reach.

---

## 6. Why Scanners May Not See This

Dependency tooling remains valuable wherever packages, runtimes, or other identifiable components are within an advisory's scope, but it answers a narrower question than source review: *which identified third-party components, at which versions, does this application depend on?* A defect generated into first-party source falls outside that question.

- **NuGet vulnerability auditing** (restore-time auditing, or commands such as `dotnet package list --vulnerable --include-transitive` and the older verb-first `dotnet list package` form) compares package identifiers and versions with advisory data. Generated source is not a package. A project may not even reference the scaffolding package anymore; many teams remove it after generation.
- **Lock files** record the resolved versions of packages. They say nothing about what source a template wrote into the repository.
- **A typical automatically generated SBOM** inventories the application and its identified dependencies by component identity and version. Generated source ordinarily appears only as part of the application's own component. The SBOM can record which template produced it only if the organization adds generator or source-provenance metadata; package resolution alone does not discover that relationship.
- **Conventional container image scanning** inspects operating-system packages, runtimes, and identifiable libraries in an image. It may confirm that a corrected runtime is present, but on its own it does not establish an application-level behavior such as a handler sending mail only to the stored address. A product that also performs source or binary analysis may detect some such patterns, but that is a different kind of evidence.

A code search for the handler names an advisory lists is often the fastest first pass. It is not a guarantee. That is why the advisory's own instruction, to check the generated code itself, is part of the remediation rather than a formality, and why the provenance in your inventory matters: it makes a complete search far easier to design, and without it a team may not know every generated pattern or location it needs to examine.

---

## 7. Use a Clean Baseline for Comparison, Not for Overwriting

When a template ships a fix, an obvious idea is to regenerate the application from the corrected template. A clean baseline, which is a reproducible copy of what a template version generates with your options, is genuinely useful here, but mostly as a reference:

- **It locates the change.** Generating the old and new template versions with the same options and comparing them shows which files and lines the template fix changed, once generated GUIDs, timestamps, and other nondeterministic output are normalized.
- **It separates intent from drift.** Comparing your application with the old baseline shows which generated files you customized. An unmodified generated file can often take the template's corrected version directly; a customized one needs a deliberate merge.
- **It supports future reviews.** A recorded baseline makes the next advisory's "which of our files came from this template?" question cheaper to answer.

Regenerating the whole application in place, or forcing new template output over existing files, is a different matter. It can silently discard customizations, reintroduce defaults the team intentionally changed, and produce a diff so large that the security-relevant change is hard to review. When the advisory identifies a bounded source correction, a focused change to the named files, supported by a regression test, is generally easier to review and verify than overwriting a customized application with newly generated output. Reconciling a whole application with a newer template generation is a separate, planned piece of work. [Issue #530](https://github.com/AsiBackbone/Learning/issues/530) tracks guidance on comparing old and new generated baselines file by file without overwriting customizations, and [You Don't Need to Roll Back What You Never Merged](you-dont-need-to-roll-back-what-you-never-merged.md) covers isolating a larger platform-generation change on a candidate branch.

As one bounded implementation example, ReferenceApplication's [baseline refresh walkthrough](https://asibackbone.github.io/ReferenceApplication/walkthrough/refresh-ncat-baseline.html) shows how a single application keeps a reproducible template baseline and refreshes it. It illustrates the mechanics; it is not a requirement for applying this article.

---

## 8. Verified Remediation Is Not a Security Guarantee

The evidence above supports a narrow, useful claim: *for this advisory, the named components in this deployed artifact now contain the described fix, and a test confirms the property the fix restores.* That is what a remediation record should say.

It does not say:

- that the application has no other vulnerabilities;
- that every copy of the generated code everywhere in the organization was found, unless the search is recorded and its scope stated;
- that the advisory's description of the defect is complete, or that a later revision will not add components or steps (CVE-2026-70175's November 10 revision is an example of guidance changing after initial publication);
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
8. Does the record identify the deployed artifact containing the repair and, when the runtime is affected, the runtime version the deployed process actually loaded?
9. Does the remediation record state its scope, and avoid claiming that the application is secure?

---

## Continue Deeper

[A Green CI Badge Does Not Prove Your .NET Package Is Trustworthy](ci-badge-does-not-prove-package-integrity.md) makes the parallel point about evidence: each check answers a specific question, and a release decision needs to know which ones were answered.

[Software Supply-Chain Integrity for .NET Repositories](../../security/software-supply-chain-integrity-for-dotnet-repositories.md) covers dependency auditing, lock files, provenance, and SBOMs in more depth, including what each can and cannot establish.

[You Don't Need to Roll Back What You Never Merged](you-dont-need-to-roll-back-what-you-never-merged.md) explains how to keep a reproducible template baseline and evaluate a new platform generation on an isolated candidate, which is the larger, planned counterpart to a focused security repair.

### Related Work

- Microsoft's [November 2026 ASP.NET Core security updates announcement](https://github.com/dotnet/announcements/issues/504) lists the thirteen new ASP.NET Core advisories and the CVE-2026-70175 follow-up.
- The [CVE-2026-118741 announcement](https://github.com/dotnet/announcements/issues/525) and its [published advisory](https://github.com/dotnet/Scaffolding/security/advisories/GHSA-jxmf-523q-f7r7) describe the affected scaffolding packages, templates, and tools, and the manual repair for previously generated Identity handlers. Questions go to its [discussion issue](https://github.com/dotnet/Scaffolding/issues/3959).
- The [CVE-2026-70175 advisory](https://github.com/dotnet/aspnetcore/security/advisories/GHSA-x3vp-85cm-q97r) separates the runtime, Identity UI package, SDK template, and generated-code remediation steps.
- The [CVE-2026-118747 advisory](https://github.com/dotnet/runtime/security/advisories/GHSA-vmv3-wvjp-wqpj) lists the fixed `System.Formats.Cbor` package versions.
- [.NET and .NET Framework November 2026 servicing updates](https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-november-2026-servicing-updates/) lists the release cycle's runtime releases and CVEs.

---

## The Short Answer

A .NET runtime or SDK update fixes code that ships in that runtime or SDK. It does not change package versions your project pins, replace the runtime inside an already published self-contained deployment, or rewrite source that a template or scaffolder generated into your repository. Read each advisory by the components it names, map each component to the team or role accountable for it, and apply what that component needs: patch the runtime or image, update the package, or repair the generated source with a focused, tested change. Rebuild and redeploy where the advisory requires it, and record which deployed artifact contains the fix. For many advisories the runtime update really is the whole job; the November 2026 advisories show that you have to read each one to know.

---

## Appendix: Complete Code

The sample has two projects: a class library containing the repaired handler and a test project. Every file below was compiled and run as shown.

### Environment used for verification

- .NET SDK 10.0.404, target framework `net10.0`, on Windows x64.
- xUnit v3 4.0.3, run through Microsoft.Testing.Platform, which `global.json` selects for `dotnet test` on the .NET 10 SDK. The recorded restore resolved Microsoft.Testing.Platform 2.5.2 through `xunit.v3.mtp-v2`.
- `global.json` requests SDK 10.0.100 with `rollForward: latestFeature`, which accepts the highest installed .NET 10 SDK feature band and patch, including the 10.0.404 used for the recorded run.
- Warnings treated as errors, with `AnalysisLevel` set to `latest-recommended` under SDK 10.0.404.
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
namespace Cedarline.Orders.TeamInvitations;

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

`App/TeamInvitations/ResendInvitationHandler.cs` is the repaired handler shown in [section 5](#make-the-focused-repair), including its two header comment lines.

### Tests

`Tests/ResendInvitationHandlerTests.cs`:

```csharp
using Cedarline.Orders.TeamInvitations;
using Xunit;

namespace Cedarline.Orders.Tests;

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
