# Security Policy

Thank you for taking the time to report security concerns responsibly.

ASI Backbone Learning is an educational repository for practical .NET architecture, governed execution, policy-driven systems, secure application design, AI integration, and related architectural patterns.

The repository contains documentation, diagrams, exercises, executable teaching samples, and documentation/build automation. It is not a production security product, compliance certification, AI model, AGI or ASI implementation, autonomous-agent runtime, or robotics controller.

## Supported Material

Learning is a living educational project that also preserves meaningful milestones as versioned educational releases. Those releases are archival and citation snapshots, not runtime package support lines. Security review therefore focuses primarily on the current repository state and the currently published documentation.

| Material                                            | Support posture                                                                                                                          |
| --------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| Current `main` branch                               | Primary review target for documentation, sample code, workflows, and repository configuration.                                           |
| Current published documentation                     | Primary review target when the issue affects rendered documentation or published guidance.                                               |
| Current executable samples under `samples/`         | Primary review target for sample behavior and demonstrated security boundaries.                                                          |
| Archived releases or historical snapshots           | Citable historical snapshots; best-effort review when an issue also affects current material or creates a meaningful migration concern. |
| Forks, copied examples, or downstream modifications | Outside repository support unless the issue is also present in the canonical Learning repository.                                        |

Security-sensitive corrections may result in documentation changes, sample-code changes, workflow hardening, dependency updates, or repository configuration changes depending on where the actual risk lives.

Versioned release records support provenance and reproducibility. They do not imply long-term security maintenance, backported corrections, runtime compatibility, or package-style support for historical Learning releases.

## Reporting a Vulnerability or Sensitive Concern

Please do **not** place exploit details, secrets, proof-of-concept payloads, private keys, tokens, personal data, or sensitive operational information in a public Issue, pull request, Discussion, commit message, screenshot, or comment.

Preferred reporting path:

1. Use [GitHub private vulnerability reporting](https://github.com/AsiBackbone/Learning/security/advisories/new) when it is available for this repository.
2. Include a concise title and identify the affected area, such as documentation, `samples/`, DocFX configuration, GitHub Actions, dependency/tooling configuration, or repository metadata.
3. Provide reproduction steps, expected behavior, actual behavior, and the practical security impact.
4. Use synthetic data and redact secrets or identifying information.
5. Allow reasonable time for review before public disclosure.

If private vulnerability reporting is unavailable, open a minimal public Issue stating only that you have a sensitive security report to share. Do not include technical details or sensitive material in that Issue.

For non-sensitive hardening suggestions, inaccurate security wording, broken examples, defense-in-depth improvements, or ordinary documentation corrections, a normal GitHub Issue or pull request is appropriate.

## Expected Response Posture

This project is community-oriented open-source educational material and does not promise a formal security-response SLA.

The expected best-effort process is:

1. A maintainer reviews the report and determines whether it is a vulnerability, unsafe teaching example, documentation defect, workflow concern, dependency issue, hardening opportunity, duplicate, or out-of-scope report.
2. The maintainer may request a reduced reproduction, affected commit information, sanitized logs, or clarification of the demonstrated impact.
3. Confirmed concerns are addressed through documentation correction, sample changes, workflow hardening, dependency updates, repository configuration changes, or a GitHub security advisory when appropriate.
4. Public wording will avoid overstating what the Learning repository, its samples, or the referenced ASI Backbone projects guarantee.

Please avoid repeated public disclosure while a sensitive report is being reviewed.

## Security Scope

Areas especially relevant to this repository include:

* executable teaching samples under `samples/`;
* examples that demonstrate policy evaluation, acknowledgment, audit residue, scoped capability, or host-owned execution;
* AI tool-gateway examples and host-side execution boundaries;
* sample handling of secrets, credentials, tokens, connection strings, or sensitive-looking data;
* documentation that could materially misstate a security boundary or encourage unsafe production behavior;
* DocFX configuration and documentation publishing behavior;
* GitHub Actions permissions and workflow behavior;
* dependency or build-tool integrity;
* supply-chain and repository automation concerns;
* accidental disclosure of credentials, personal data, or confidential information through repository content, logs, examples, or generated artifacts.

### Examples generally in scope

Examples include:

* a sample that executes a consequential operation after a documented deny decision;
* a capability example that can be trivially reused, widened, or bypassed contrary to the behavior the tutorial claims to demonstrate;
* an AI tool-gateway sample that allows an unvalidated model proposal to reach host-owned execution despite documentation stating otherwise;
* documentation that instructs readers to commit real credentials or materially unsafe secret-handling practices;
* a workflow configuration that exposes a repository credential or grants unnecessarily dangerous write permissions in a way that creates a practical exploit path;
* a documentation-build or publishing issue that allows untrusted content to escape the expected build or publishing boundary;
* repository-maintained sample code that exposes sensitive data or performs an unintended external side effect by default.

### Examples generally out of scope

Examples include:

* general disagreement with an architectural pattern when no concrete security defect is demonstrated;
* requests for a compliance, certification, legal, or security guarantee;
* vulnerabilities caused solely by a consuming application's custom implementation, infrastructure, deployment configuration, cloud policy, database security, or key-management choices;
* vulnerabilities in `AsiBackbone/AsiBackbone` or `AsiBackbone/NetCoreApplicationTemplate` that are not present in Learning material; report those to the affected repository instead;
* claims that an educational example should prevent all misuse of AI, agents, APIs, or robotics without a specific repository-level vulnerability;
* issues that exist only in a third-party fork or materially modified copy of a Learning example.

## Educational Security Boundaries

Learning examples are designed to expose architectural reasoning clearly. They intentionally omit some production complexity.

A successful sample or tutorial does **not** establish that a production system is secure, compliant, legally sufficient, tamper-proof, or appropriate for a particular risk level.

Production systems remain responsible for their own:

* authentication and authorization;
* policy ownership and policy correctness;
* persistence and transactional integrity;
* replay protection;
* cryptographic key custody;
* secret management;
* network and infrastructure security;
* dependency management;
* observability and incident response;
* regulatory and legal requirements;
* threat modeling and security review;
* operational execution and physical safety controls.

For governance and AI-related examples, the central teaching boundary remains:

> **The model may propose. The host retains execution authority.**

Prompt instructions, model behavior, and tool descriptions may influence what an AI system proposes. They are not substitutes for authoritative host-side validation, policy evaluation, authorization, or execution controls.

## Sample-Code Safety Expectations

Executable samples should remain safe teaching artifacts.

Repository-maintained samples should generally:

* use fictional or placeholder data;
* avoid real credentials, secrets, tokens, private keys, certificates, connection strings, or personal information;
* prefer deterministic local behavior;
* use mocks, fakes, simulation, or dry-run behavior for consequential external operations when practical;
* keep policy evaluation separate from side-effect execution;
* keep infrastructure credentials and execution authority host-owned;
* make important trust and execution boundaries visible;
* include tests for security-relevant architectural invariants when those invariants are central to the lesson.

A sample is not made production-ready merely because it compiles, passes tests, or demonstrates its intended architectural boundary.

## Secrets Incident Guidance

If a credential, token, key, certificate, connection string, or other secret is suspected of being committed, published, logged, or exposed through repository automation:

1. Treat the secret as compromised immediately.
2. Revoke or rotate it at the issuing system before relying on repository cleanup.
3. Review relevant GitHub Actions runs, repository events, generated artifacts, and publication activity for unexpected use.
4. Remove the exposed value from the current repository state.
5. Determine whether git-history cleanup is necessary based on the sensitivity and exposure of the secret.
6. Avoid copying the exposed value into Issues, pull requests, commit messages, screenshots, or additional logs.
7. Add or improve preventive controls where practical, such as secret scanning, push protection, ignored local artifact paths, safer examples, or tighter workflow permissions.

Repository cleanup does not invalidate a credential that has already been exposed. Rotation or revocation is the primary response.

## Sensitive Data Guidance for Reports

When submitting a report:

* redact passwords, secrets, tokens, private keys, certificates, connection strings, user identifiers, personal information, customer data, and regulated data;
* use synthetic examples whenever possible;
* share only the minimum information required to reproduce or understand the concern;
* clearly identify any material that remains sensitive.

## Reports for Related Repositories

Learning frequently links to fuller implementations in other ASI Backbone organization repositories.

Security concerns in those implementations should be reported to the repository that owns the affected code:

* [AsiBackbone security policy](https://github.com/AsiBackbone/AsiBackbone/security/policy)
* [NetCoreApplicationTemplate security policy](https://github.com/AsiBackbone/NetCoreApplicationTemplate/security/policy)

If a concern exists both in Learning material and in a referenced implementation, mention that relationship in the private report so maintainers can coordinate the correction.

## Safe Public Language

It is accurate to describe ASI Backbone Learning as an educational resource for studying architectural patterns, tradeoffs, examples, and governed-execution boundaries.

It is not accurate to describe this repository as providing:

* a production security guarantee;
* compliance certification;
* legal assurance;
* automatic protection against AI misuse;
* a production tamper-proof audit system;
* a production AI or robotics control system;
* a complete security architecture for consuming applications.

## Related Documents

* [README.md](README.md)
* [CONTRIBUTING.md](CONTRIBUTING.md)
* [GOVERNANCE.md](GOVERNANCE.md)
* [ROADMAP.md](ROADMAP.md)
* [LICENSING.md](LICENSING.md)
* [samples/README.md](samples/README.md)
* [Software Supply-Chain Integrity for .NET Repositories](docs/security/software-supply-chain-integrity-for-dotnet-repositories.md)
