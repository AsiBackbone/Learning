---
description: Document the GitHub-host security controls that protect Learning source, executable samples, documentation workflows, and the main branch.
---

# Repository Host Security Controls

This page documents the GitHub-host controls used to protect the
`AsiBackbone/Learning` source, executable teaching samples, documentation
workflows, and default branch.

These settings are repository controls. They do not make the Learning examples a
production security product and they do not replace application-specific threat
modeling or review.

The repository currently has one active maintainer. The selected control set
therefore avoids treating self-review as independent review while still requiring
a pull-request trail, stale-review invalidation, blocking CI, review-thread
resolution, and an explicit emergency-bypass path.

## Canonical desired state

The machine-readable main-branch ruleset is committed at
`eng/repository-controls/main-branch-ruleset.json`. The live GitHub configuration
is expected to match it.

| Control | Selected posture | Rationale |
| --- | --- | --- |
| Dependabot security updates | **Enabled** | Automatically opens pull requests for supported vulnerable dependencies when a fix is available. The existing `.github/dependabot.yml` remains the source-controlled update configuration. |
| Secret scanning | **Enabled** | Detects supported credentials committed to repository history. |
| Secret-scanning push protection | **Required and enabled** | Blocks supported secrets before they land in the repository. A real secret should be removed and rotated or revoked rather than bypassed. |
| Non-provider patterns | Enable when the repository/plan exposes the control | Generic private keys and credential-bearing connection strings are relevant to a public repository containing executable .NET samples. Unsupported availability is a warning, not a reason to weaken provider push protection. |
| Validity checks | Enable when the repository/plan exposes the control | Validity information can improve alert triage for supported provider tokens. GitHub may contact the issuing service to determine validity, so the choice remains explicit. |
| Main-branch ruleset | **Active** | Makes the effective branch policy reviewable rather than relying only on legacy branch-protection defaults. |
| Pull request before merge | Required | Ordinary and emergency changes retain a PR and audit trail. |
| Dismiss stale approvals | Required | A review should not remain current after reviewable content changes. |
| Required approvals | `0` while one active maintainer remains | GitHub does not permit an author to provide independent approval of their own PR. Requiring one approval would create a permanent self-lock or routine bypass. |
| Code Owner approval | Not required while one active maintainer remains | `.github/CODEOWNERS` still records ownership and review routing. Re-enable required Code Owner review when another active maintainer can provide independent approval. |
| Last-push approval | Not required while one active maintainer remains | This also requires a second person. Revisit with the approval-count decision. |
| Review-thread resolution | Required | Blocking review conversations must be resolved before ordinary merge. |
| Merge method | Squash only | Keeps `main` linear and matches the repository's normal reviewed merge path. |
| Linear history | Required | Prevents merge commits from being pushed directly to `main`. |
| Force push and deletion | Blocked | Protects stable history from destructive updates. |
| Required signed commits | Deferred | Current local and automation commits are not yet guaranteed to be signed consistently. Enabling the rule before that workflow is proven would turn routine merges into bypass-only operations. |
| Emergency bypass | `@cdcavell` only, pull-request mode only | Replaces a broad implicit administrator exemption with one repository-specific, auditable bypass actor that still has to use a PR. |

## Required status checks

The ruleset carries forward the four required GitHub Actions checks already
enforced on `main`:

- `Build DocFX documentation`
- `Validate documentation links`
- `Restore, build, and test samples`
- `Analyze C# with CodeQL`

The checks are bound to the GitHub Actions integration and use strict status-check
policy so a pull request must be validated against the current target branch.

## Dependabot security updates

Learning already commits `.github/dependabot.yml` for NuGet and GitHub Actions
version-update scheduling. That file is useful, but it does not by itself prove
that repository-level Dependabot **security updates** are enabled.

The desired state therefore treats the repository setting as mandatory and the
committed file as configuration for how Dependabot operates once enabled.

The management script audits the dedicated GitHub automated-security-fixes
endpoint. A disabled setting is a failure. A GitHub-paused security-update state
is reported as a warning so maintainers can determine whether inactivity or
another repository condition is suppressing security update pull requests.

## Signed-commit decision and compensating controls

Required commit signatures are intentionally deferred by this issue.

GitHub evaluates commits introduced by a pull request, not merely the final
GitHub-generated squash result. Requiring signatures before local Visual Studio
commits and automation identities are consistently signed could make every normal
pull request depend on bypass. That would weaken the intended control model.

Until signing is adopted and verified end to end, the compensating controls are:

- every ordinary change reaches `main` through a pull request;
- only squash merge is permitted by the ruleset;
- all four required checks must pass unless the documented emergency bypass is
  explicitly used;
- stale reviews are dismissed when reviewable content changes;
- review threads must be resolved;
- force pushes and branch deletion are blocked;
- the emergency bypass is limited to one user and to pull requests only;
- the PR, merge result, and ruleset-bypass event remain visible in repository
  history and Rule Insights.

Revisit `required_signatures` after local GPG or SSH signing and relevant
automation identities have been exercised successfully on a non-default branch.

## Applying or auditing the controls

Repository settings are not versioned by Git. This patch commits the desired
state and a tool used to compare or apply it; applying the Git patch does **not**
change the live GitHub settings.

Authenticate GitHub CLI with an account that has repository Administration
permission, then run the read-only audit:

```powershell
gh auth status
./scripts/Manage-RepositorySecurityControls.ps1
```

Preview mutations before applying them:

```powershell
./scripts/Manage-RepositorySecurityControls.ps1 -Apply -WhatIf
```

After reviewing the preview, apply the repository settings and create or update
the canonical ruleset:

```powershell
./scripts/Manage-RepositorySecurityControls.ps1 -Apply
```

The apply path enables Dependabot security updates, secret scanning, and push
protection first. It then attempts to enable non-provider patterns and validity
checks independently; if GitHub does not expose one of those optional controls
for the repository or plan, the script warns without weakening mandatory push
protection. Finally it creates or updates the named main-branch ruleset and
reruns the audit.

Legacy branch protection may remain in place as defense in depth. Its broad
administrator exemption is not the canonical bypass mechanism once the explicit
ruleset is active.

## Emergency bypass procedure

Bypass is for an urgent failure of the repository control plane, not a shortcut
around an inconvenient test.

1. Open or retain a pull request. Direct push to `main` is not the emergency path.
2. Record which rule or check is being bypassed and why waiting for repair is more
   dangerous than merging the narrow correction.
3. Keep the change as small as possible and use squash merge.
4. Never use ruleset bypass as a substitute for secret-scanning push-protection
   remediation. Remove the secret, revoke or rotate it when appropriate, and
   retry.
5. After the emergency merge, repair the failing control and rerun
   `Manage-RepositorySecurityControls.ps1`.
6. Preserve the PR and Rule Insights bypass event as the audit trail.

When a second active maintainer is appointed, the first repository-control
change should reevaluate required approvals, Code Owner review, last-push
approval, stale-review behavior, signed commits, and the bypass list.

## Workflow compatibility

The ruleset targets only `refs/heads/main`. It does not target tags or the GitHub
Pages deployment environment.

Normal pull requests continue to run the same documentation, link, sample, and
CodeQL checks already required by branch protection. Dependabot security-update
pull requests use the same review and CI path.

After first applying or materially changing the live controls:

1. run the read-only repository-control audit;
2. open a normal PR and confirm all four required checks are present;
3. confirm the PR can merge normally by squash after the checks pass;
4. confirm the resulting `main` push still starts the documentation publishing
   workflow as expected;
5. observe the next Dependabot security-update PR, when applicable, and confirm
   the same required checks run without a repository-rule bypass.

GitHub's platform documentation remains authoritative for ruleset and security
feature semantics. This page and the committed manifest are the
Learning-specific decision record.
