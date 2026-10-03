# AsiBackbone Learning 1.2.0

AsiBackbone Learning 1.2.0 is an archival and citation snapshot of the educational material published since 1.1.0. It adds four practitioner-focused articles about AI tool execution, decision boundaries, policy-as-code architecture, and the relationship between application controls and `Microsoft.AgentGovernance`. The implementation correspondence baseline remains the released AsiBackbone 7.0.0 surface.

## Highlights

- Adds **What Should an AI Tool Gateway Validate Before Execution?**, an ordered host-side acceptance checklist with tests proving rejected proposals never reach the executor.
- Adds **Authorization vs. Approval vs. Acknowledgment: Which Decision Do You Actually Have?**, separating four commonly collapsed decision concepts and carrying them through execution-time validation.
- Adds **Policy as Code in ASP.NET Core Without Overengineering**, comparing ordinary code, framework authorization, in-process policy components, embedded engines, and remote decision services.
- Adds **How Application Architecture Complements Microsoft.AgentGovernance**, following an AI-proposed refund through authoritative context, verdict translation, workflow state, revalidation, execution authority, failure handling, and evidence.
- Clarifies that pipeline behaviors may enforce transport or request-boundary rules but must not silently become the authority that decides or executes a consequential operation.

## Documentation and Validation Hardening

- Current-version wording is validated so instructional pages cannot describe an older AsiBackbone major version as current while historical records remain exempt.
- Current implementation links no longer describe Git tags as inherently immutable; guidance now recommends resolving a tag to its commit SHA when a reference must not change.
- Landing-page validation ensures **Recently added** lists the newest publications in order and uses their exact titles.
- Organization source links are validated against fetched Git objects instead of live blob and tree requests, avoiding transient GitHub failures without reducing path validation.
- Pull-request and push link validation tolerates transient HTTP 503 responses while the scheduled validation run remains strict.

## Repository Operations

- Pins GitHub-hosted workflow jobs to Ubuntu 24.04 so runner-image changes remain explicit and reviewable.
- Corrects Dependabot's commit-message configuration to produce the intended `chore(deps):` prefix.
- Records the protected X-publisher state branch and its repository ruleset boundary.
- Adds the repository-local Learning documentation-review skill.

## Compatibility

Learning releases are citable documentation and sample snapshots, not runtime or package support lines. Learning 1.2 continues to teach the released AsiBackbone 7.0.0 API boundary. Framework-neutral executable samples do not reference `AsiBackbone.*` packages and are not package-integration tests.

The Learning 1.1.0 and Learning 1.0.0 records remain historical snapshots of their reviewed boundaries. Exact package signatures, runtime behavior, configuration, migrations, and security semantics remain authoritative in the corresponding AsiBackbone release documentation.

## Known Limitations

- Learning is educational documentation, not a compliance certification, legal standard, security guarantee, or production architecture approval.
- Executable samples are teaching models and do not prove integration compatibility with released `AsiBackbone.*` packages.
- External links and upstream products remain independently operated and can change after this snapshot.
- Final tag identity, GitHub Release notes, evidence assets, and provenance are verified only after the approved release candidate is merged and `v1.2.0` is published.

## Release Evidence

The GitHub Release will include an SPDX 2.3 inventory of sample source and locked dependencies, these exact release notes, and a SHA-256 evidence manifest. GitHub provenance attestations bind each evidence asset to the release workflow. See the [Stable Release Evidence Runbook](RELEASE.md) for scope and verification commands.
