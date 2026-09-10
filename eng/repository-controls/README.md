# Repository controls

This directory records the repository-specific desired state for GitHub-host
security controls that cannot be enforced by the documentation build or sample
test suite.

`main-branch-ruleset.json` is the canonical repository ruleset payload for the
`main` branch. Apply or audit it with:

```powershell
./scripts/Manage-RepositorySecurityControls.ps1
./scripts/Manage-RepositorySecurityControls.ps1 -Apply -WhatIf
./scripts/Manage-RepositorySecurityControls.ps1 -Apply
```

The script requires an authenticated `gh` session. Mutation requires repository
Administration permission.

## Selected posture

- Dependabot security updates: enabled;
- secret scanning: enabled;
- secret-scanning push protection: enabled and mandatory;
- non-provider secret patterns: enabled when GitHub exposes the feature for this
  repository/plan;
- secret validity checks: enabled when GitHub exposes the feature for this
  repository/plan;
- main changes: pull request required;
- merge method: squash only;
- linear history: required;
- force pushes and deletion: blocked on `main`;
- automatic deletion of merged pull-request head branches: enabled;
- stale approvals: dismissed on new reviewable pushes;
- four existing documentation/sample/link/CodeQL GitHub Actions checks: required
  and strict;
- review-thread resolution: required;
- required approvals / Code Owner approval / last-push approval: disabled while
  the repository has only one active maintainer;
- required signed commits: deferred until normal local and automation commits are
  consistently signed and can pass GitHub's PR signature evaluation without
  turning routine merges into bypasses;
- bypass: only GitHub user `@cdcavell` (user id `28095137`), and only through a
  pull request.

The explicit user bypass is intentionally narrower than a repository-role or
organization-administrator exemption. `pull_request` bypass mode preserves the
PR and ruleset-bypass audit trail and does not grant an emergency direct-push
path to `main`.

## Required checks captured by the manifest

The current protected `main` branch requires these GitHub Actions checks, and the
ruleset deliberately carries the same set forward:

- `Build DocFX documentation`
- `Validate documentation links`
- `Restore, build, and test samples`
- `Analyze C# with CodeQL`

The GitHub Actions integration id currently associated with those checks is
`15368`. If a check is renamed, added, removed, or moves to a different trusted
integration, update the manifest in the same PR as the workflow/protection
change.

## Review rule changes like code

Changing this manifest changes the expected repository trust boundary. Review
the diff for bypass actors, target refs, required checks, merge methods, and PR
requirements before applying it. Do not use `-Apply` merely to overwrite an
unexpected live configuration without first determining why it drifted.

The rationale, optional-feature decision, signed-commit deferral, and emergency
bypass procedure are documented in
`docs/security/repository-host-security-controls.md`.

## Branch retention and archival releases

Branches are working refs, not the archival identity of a Learning release. A
citable release is retained through its version tag, GitHub Release, synchronized
citation/Zenodo metadata, and published archival record. Deleting a merged
`release/*` branch must not delete any of those identities.

The retention baseline is:

- `main` is permanent and protected.
- `issue_work` may remain while it carries active or unresolved integration work;
  it is not a release archive.
- `release/*` branches are temporary. Remove them after the release is merged and
  the release-verification checklist below succeeds.
- ordinary pull-request head branches are temporary and should be removed after
  merge. The repository setting `delete_branch_on_merge` is part of the audited
  desired state and is enabled by `Manage-RepositorySecurityControls.ps1`.
- automation-owned branches such as Dependabot branches are transient and are not
  archival refs.

Before deleting any historical or release branch:

1. Fetch current refs and tags.

   ```powershell
   git fetch --prune --tags origin
   ```

2. Verify the branch contains no commits that are absent from `main`.

   ```powershell
   git log --oneline origin/main..origin/<branch>
   ```

   Any output means the branch still contains unique commits. Do not delete it
   until those commits are intentionally merged, preserved under another ref, or
   explicitly dispositioned.

3. For a release branch, verify the expected version tag and GitHub Release exist.

   ```powershell
   git tag --list v<version>
   gh release view v<version> --repo AsiBackbone/Learning
   ```

4. Verify the release identity recorded in `CITATION.cff` and `.zenodo.json`
   matches the published archival record, including the version-specific DOI when
   one has been issued. The concept DOI identifies the evolving work; it is not a
   substitute for the version-specific release record.

5. Only after those checks succeed, delete the redundant remote branch.

   ```powershell
   git push origin --delete <branch>
   ```

For `issue_work`, use the same unique-commit check before deleting or recreating
the branch. If `git log origin/main..origin/issue_work --oneline` is empty and
there is no unresolved pull request or intentionally retained work associated
with it, the branch can be pruned and recreated later from `main`.

Automatic deletion handles normal merged pull-request branches, but release
branches still require the explicit archival verification above. This preserves
traceability without treating every historical branch name as a permanent
citation surface.
