# ASI Backbone Learning

[![Documentation Validation](https://github.com/AsiBackbone/Learning/actions/workflows/docs-validation.yml/badge.svg?branch=main)](https://github.com/AsiBackbone/Learning/actions/workflows/docs-validation.yml)
[![Samples Validation](https://github.com/AsiBackbone/Learning/actions/workflows/samples-validation.yml/badge.svg?branch=main)](https://github.com/AsiBackbone/Learning/actions/workflows/samples-validation.yml)
[![Docs](https://img.shields.io/badge/docs-GitHub%20Pages-blue)](https://asibackbone.github.io/Learning/)
[![Zenodo DOI](https://img.shields.io/badge/DOI-10.5281%2Fzenodo.21938556-blue)](https://doi.org/10.5281/zenodo.21938556)

**Practical .NET architecture education for governed execution, secure applications, AI integration, and policy-driven systems.**

`AsiBackbone/Learning` teaches how proposed operations can move through explicit policy decisions, acknowledgments, scoped authority, host-owned execution, and durable audit evidence.

**You can use this material without installing the `AsiBackbone` framework.** Tutorials, samples, comparisons, and labs are intended to remain useful as independent architecture education.

In this project, **ASI** means **Accountable Systems Infrastructure**. Learning is the educational layer of the ASI Backbone organization; it is not an artificial general intelligence or artificial superintelligence implementation.

## Quick Start — Run It in 10 Minutes

Prefer to see the architecture run before reading the deeper explanation? The foundational **Decision Before Execution** sample provides the shortest path from clone to observable behavior.

**Prerequisite:** .NET 10 SDK

From a terminal:

```bash
git clone https://github.com/AsiBackbone/Learning.git
cd Learning

dotnet run --project samples/decision-before-execution/DecisionBeforeExecution/DecisionBeforeExecution.csproj

dotnet test samples/decision-before-execution/DecisionBeforeExecution.Tests/DecisionBeforeExecution.Tests.csproj
```

### What to Observe

The sample makes one architectural invariant visible:

> **A blocked decision never reaches the executor.**

```text
Allowed decision
   ↓
Host-owned executor invoked

Denied / deferred / escalation-recommended / acknowledgment-required decision
   ↓
Executor not invoked
```

The console sample evaluates five deterministic scenarios and verifies that exactly one allowed operation crosses the execution boundary. The focused xUnit tests make the same contract repeatable for local development and CI.

Want to understand why this boundary exists or experiment with it?

- [Decision Before Execution sample README](samples/decision-before-execution/README.md)
- [Decision Before Execution tutorial](docs/tutorials/decision-before-execution.md)
- [Getting Started](docs/getting-started/index.md)

## Choose Your Next Step

| If you want to... | Start here |
| --- | --- |
| Learn the foundational governed-execution boundary | [Decision Before Execution](docs/tutorials/decision-before-execution.md) |
| Route from a problem you already recognize | [Find Your Path](docs/getting-started/find-your-path.md) |
| See the curriculum and prerequisites at a glance | [Learning Path Map](docs/getting-started/learning-path-map.md) |
| Decide whether ASP.NET Core authorization is already enough | [When ASP.NET Core Authorization Is Enough](docs/architecture/when-aspnet-core-authorization-is-enough.md) |

## What This Architecture Looks Like in Practice

A governed system makes the proposed operation, relevant context, active constraint, decision, and execution boundary visible.

```text
Proposed operation:
account.disable

Context:
Account is protected

Constraint:
Protected accounts require escalation

Decision:
EscalationRecommended

Execution:
Not invoked
```

Learning explores the architectural boundaries that make behavior like this explicit, testable, auditable, and separable from execution. The same reasoning can be applied to administrative workflows, infrastructure changes, sensitive API operations, AI tool calls, and other consequential actions.

## Foundational Learning Path

The established five-part sequence moves from proposed intent to governed AI-assisted execution:

1. [Decision Before Execution](docs/tutorials/decision-before-execution.md)
2. [Policy Context and Explicit Decision Outcomes](docs/tutorials/policy-context-and-explicit-decision-outcomes.md)
3. [Acknowledgment and Audit Residue](docs/tutorials/acknowledgment-and-audit-residue.md)
4. [Scoped Capability and Host-Owned Execution](docs/tutorials/scoped-capability-and-host-owned-execution.md)
5. [Governed AI Tool Gateway](docs/tutorials/governed-ai-tool-gateway.md)

Each foundational topic is reinforced by runnable samples, focused architectural-invariant tests, and hands-on labs.

Want to understand why Learning uses a problem-first tutorial model, how tutorials differ from labs, or how canonical and alternative patterns are handled? See the [Learning Model](docs/getting-started/learning-model.md).

## ASI Backbone Ecosystem

The organization contains complementary projects with different responsibilities:

| Project | Primary role |
| --- | --- |
| [Learning](https://asibackbone.github.io/Learning/) | Teaches concepts, patterns, tradeoffs, samples, and labs |
| [AsiBackbone](https://github.com/AsiBackbone/AsiBackbone) | Working governance and policy-control implementation |
| [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate) | ASP.NET Core reference application and architecture specimen |

Learning connects to the implementation repositories when fuller examples are useful, but adoption of either implementation repository is not required to benefit from the educational material.

## Scope and Boundaries

ASI Backbone Learning is an educational and architectural resource.

- It teaches architectural patterns; it does not certify compliance or guarantee security.
- Examples do not replace application-specific security, legal, regulatory, safety, or operational review.
- Learning is not an AI model, an artificial general intelligence or artificial superintelligence implementation, or a robotics controller.
- No `AsiBackbone` package is required, and no pattern is presented as universally correct.

Production systems remain responsible for their own authentication, authorization, persistence, infrastructure, threat modeling, safety controls, regulatory requirements, and operational execution.

## Project and Community

Use the canonical project surfaces for deeper information rather than treating the root README as the full reference manual:

**Questions and ideas → [Discussions](https://github.com/AsiBackbone/Learning/discussions) · Concrete work → [Issues](https://github.com/AsiBackbone/Learning/issues) · Changes → [Pull Requests](https://github.com/AsiBackbone/Learning/pulls)**

- **Published documentation:** [asibackbone.github.io/Learning](https://asibackbone.github.io/Learning/)
- **Contribution guidance:** [CONTRIBUTING.md](CONTRIBUTING.md)
- **Project status and planned work:** [ROADMAP.md](ROADMAP.md)
- **Learning discussions:** [AsiBackbone/Learning Discussions](https://github.com/AsiBackbone/Learning/discussions)
- **Organization-wide discussion:** [ASI Backbone Organization Discussions](https://github.com/orgs/AsiBackbone/discussions)
- **Governance:** [GOVERNANCE.md](GOVERNANCE.md)
- **Security policy:** [SECURITY.md](SECURITY.md)
- **Citation metadata:** [CITATION.cff](CITATION.cff)
- **Licensing details:** [LICENSING.md](LICENSING.md)

Issues are best used for concrete repository work; Learning Discussions are better suited to exploratory architecture questions, tutorial proposals, alternatives, design debates, and community examples.

## Project Status

**Active development — foundational tutorial, sample, test, and lab path established.**

Current development is focused on stronger implementation references, deeper labs, ASP.NET Core architecture, security and trust architecture, governance material, architecture comparisons, and improved discoverability. See [ROADMAP.md](ROADMAP.md) for the maintained direction.

## Citing a Release

Use the [Zenodo concept DOI](https://doi.org/10.5281/zenodo.21938556) when citing the evolving Learning work as a whole.

When reproducibility depends on the exact material reviewed, cite the version-specific DOI shown on that release's Zenodo record and include the corresponding Learning version or GitHub tag. [GitHub Releases](https://github.com/AsiBackbone/Learning/releases) provides the versioned repository trail.

## License

ASI Backbone Learning uses component-specific licensing:

- Documentation, educational material, and diagrams: **CC BY 4.0**
- Executable sample code under `samples/`: **MIT License**
- Source-code snippets embedded in documentation: **MIT License** unless otherwise noted

See [LICENSING.md](LICENSING.md) for the complete licensing policy.

---

**ASI Backbone Learning is not intended to provide doctrine. It is intended to provide patterns worth examining.**

Read them. Test them. Challenge them. Adapt them. Improve them.
