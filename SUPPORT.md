# Support Policy

AsiBackbone Learning is an educational repository for practical .NET architecture, governed execution, policy-driven systems, secure application design, AI integration, and related patterns. Support is focused on the tutorials, executable samples, documentation site, repository automation, and release snapshots maintained here.

## Support Channels

Use [GitHub Discussions](https://github.com/orgs/AsiBackbone/discussions) for:

- Architecture, design, and tradeoff questions.
- Help understanding or applying a tutorial or sample.
- Ideas for new learning material that are not yet focused work items.
- Comparing canonical, alternative, or experimental patterns.
- Broader questions that span multiple ASI Backbone repositories.

Use [GitHub Issues](https://github.com/AsiBackbone/Learning/issues) for:

- Reproducible defects in tutorials, samples, labs, documentation, or repository tooling.
- Incorrect, unclear, outdated, or inaccessible learning material.
- Broken links, navigation, DocFX, citation, or publication problems.
- Focused requests for a tutorial, sample, lab, diagram, or repository improvement.
- Security-adjacent hardening work that is safe to discuss publicly.

Use the private vulnerability reporting process described in [SECURITY.md](SECURITY.md) for suspected vulnerabilities or reports containing sensitive details.

Questions about package APIs, runtime behavior, or package releases belong in [AsiBackbone](https://github.com/AsiBackbone/AsiBackbone). Questions about generated application behavior or template packages belong in [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate).

## Support Expectations

Support is provided on a best-effort basis by the project maintainers. The governance process in [GOVERNANCE.md](GOVERNANCE.md) guides project operations but is not a service-level agreement.

Users can expect:

- Public triage when a report contains enough information to evaluate.
- Reproducible sample and documentation defects to be prioritized over environment-specific customization questions.
- Corrections when material is inaccurate, misleading, or no longer aligned with its authoritative implementation source.
- Clear classification of canonical, alternative, experimental, and deprecated material when that distinction matters.
- Security reports and publication-blocking defects to receive higher priority than general content requests.

Users should not expect:

- Guaranteed response or resolution times.
- Private consulting, production incident response, legal or compliance advice, or environment-specific debugging.
- Learning material to serve as a runtime support contract, compliance certification, security guarantee, or universally correct architecture.
- Support here for defects that reproduce only in an implementation repository or a heavily modified downstream application.
- Every historical tutorial snapshot to be updated after a newer release is published.

## Release Support Lifecycle

Learning releases are archival and citation snapshots of educational material. They do not establish runtime compatibility or package support lines.

| Version line | Support expectation |
|:---|:---|
| `0.15.x` | Current stable learning snapshot. Supported for reproducible documentation, sample, security, citation, and publication corrections. |
| Earlier releases | Historical snapshots. Best effort when a report also affects current material or citation integrity. |
| Unreleased `main` branch | Active development material. Content may change before the next release. |

The release evidence process is defined in [RELEASE.md](RELEASE.md). The security-specific lifecycle and private reporting process are authoritative in [SECURITY.md](SECURITY.md).

## Issue Triage

Issues are generally reviewed for:

1. Reproducibility and the affected tutorial, sample, page, workflow, or release.
2. Learning value, technical accuracy, security impact, and publication impact.
3. Whether the material is canonical, alternative, experimental, or deprecated.
4. Whether the authoritative implementation belongs in Learning, AsiBackbone, or NetCoreApplicationTemplate.
5. Whether the report includes enough sanitized detail to validate safely.

Maintainers may close reports that are duplicated, stale, unreproducible, outside the educational scope, or specific to unsupported downstream customization. The detailed decision and contribution model is defined in [GOVERNANCE.md](GOVERNANCE.md).

## Pull Request Support

Pull requests should follow [CONTRIBUTING.md](CONTRIBUTING.md). Substantial architecture or editorial changes may require prior discussion, explicit tradeoff analysis, or links to authoritative implementation decisions even when automated checks pass.
