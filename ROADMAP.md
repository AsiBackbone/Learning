# Roadmap

## Purpose

`AsiBackbone/Learning` is a community-maintained living tutorial for practical .NET architecture, governed execution, policy-driven systems, secure application design, AI integration, and related architectural patterns.

This roadmap describes the intended direction of the Learning repository.

It is not a fixed release contract. Priorities may change as the repository develops, contributors identify better learning paths, implementation repositories evolve, or community feedback reveals more useful areas to explore.

The project should grow deliberately:

> **Start with a small number of strong lessons, connect them to working implementations, then expand through use and contribution.**

## Guiding Goals

The Learning repository should become:

- A clear entry point into the ASI Backbone organization.
- A practical learning resource rather than a product manual.
- A bridge between architectural reasoning and working .NET implementations.
- A place where canonical and alternative architectural patterns can be compared.
- A source of minimal examples that expose architectural boundaries clearly.
- A home for hands-on labs and experiments.
- A community contribution surface that is easier to enter than the core implementation repositories.
- A durable record of lessons learned from the evolution of `AsiBackbone` and `NetCoreApplicationTemplate`.

## Relationship to the Other Repositories

The three primary organization repositories serve different roles:

```text
AsiBackbone/Learning
    |
    | teaches concepts, patterns, tradeoffs, and minimal examples
    |
    +------> AsiBackbone/AsiBackbone
    |        governance and policy-control implementation
    |
    +------> AsiBackbone/NetCoreApplicationTemplate
             ASP.NET Core reference architecture implementation
```

Learning should explain and connect.

It should not duplicate the complete technical documentation of the implementation repositories.

## Roadmap Principles

### Problem First

Tutorials should begin with the problem being solved rather than with a package API.

### Small Examples

Teaching examples should remain intentionally smaller than production implementations.

### Working References

Where possible, lessons should point to real implementation files, ADRs, tests, or documentation in the working repositories.

### Explicit Tradeoffs

Material should explain benefits, costs, alternatives, failure modes, and cases where a pattern may not be appropriate.

### Community Evolution

The roadmap should remain open to contribution, criticism, and alternative approaches.

### Clear Boundaries

The repository should preserve the distinction between:

```text
Intent
   ↓
Context
   ↓
Constraints
   ↓
Decision
   ↓
Acknowledgment when required
   ↓
Scoped authority
   ↓
Host-owned execution
   ↓
Audit residue
```

For AI-related material, the core teaching boundary remains:

> **The model may propose. The host retains execution authority.**

---

# Phase 1 — Foundation

## Goal

Establish the repository as a usable, navigable, contribution-ready learning project.

## Repository Foundation

- [x] Create root `README.md`.
- [x] Add MIT `LICENSE`.
- [x] Add `CODE_OF_CONDUCT.md`.
- [x] Add `CONTRIBUTING.md`.
- [x] Add `GOVERNANCE.md`.
- [x] Add `ROADMAP.md`.
- [ ] Add `SECURITY.md`.
- [ ] Add repository description and topics.
- [ ] Configure branch protection or repository rules as appropriate.
- [ ] Add Issue templates.
- [ ] Add pull request template.
- [ ] Add Discussions links and contribution guidance.
- [ ] Add initial contributor labels.

## Suggested Labels

Possible repository labels include:

- `good first tutorial`
- `documentation`
- `tutorial`
- `lab`
- `diagram`
- `example wanted`
- `architecture question`
- `alternative pattern`
- `canonical pattern`
- `experimental`
- `beginner`
- `intermediate`
- `advanced`
- `needs explanation`
- `community contribution`
- `help wanted`

## Initial Repository Structure

Target structure:

```text
Learning/
│
├── README.md
├── CONTRIBUTING.md
├── CODE_OF_CONDUCT.md
├── GOVERNANCE.md
├── ROADMAP.md
├── SECURITY.md
│
├── docs/
│   ├── getting-started/
│   ├── architecture/
│   ├── governance/
│   ├── aspnetcore/
│   ├── security/
│   ├── ai-integration/
│   └── advanced/
│
├── tutorials/
│   ├── decision-before-execution/
│   ├── policy-context/
│   ├── explicit-decision-results/
│   ├── acknowledgment-workflows/
│   ├── audit-residue/
│   ├── capability-tokens/
│   └── ai-tool-gateway/
│
├── labs/
│   ├── beginner/
│   ├── intermediate/
│   └── advanced/
│
├── diagrams/
│
└── community/
    ├── tutorial-ideas.md
    ├── requested-topics.md
    └── contributors.md
```

The structure may evolve when actual content reveals a better organization.

---

# Phase 2 — DocFX Documentation Foundation

## Goal

Create a consistent published documentation experience similar to the other ASI Backbone organization repositories.

## Planned Work

- [ ] Add DocFX tooling configuration.
- [ ] Add `docs/docfx.json`.
- [ ] Add documentation index.
- [ ] Add table-of-contents files.
- [ ] Add navigation for tutorials and labs.
- [ ] Add organization and repository links.
- [ ] Add local documentation build instructions.
- [ ] Add GitHub Actions documentation build validation.
- [ ] Add GitHub Pages publishing workflow.
- [ ] Add link validation where practical.
- [ ] Add documentation build status to the repository README.

## Documentation Goal

The published site should provide a guided learning path while GitHub remains the primary source-control and collaboration surface.

The Markdown files in the repository should remain readable and useful without requiring the published site.

---

# Phase 3 — Core Architecture Tutorials

## Goal

Publish a small, coherent set of foundational tutorials before expanding broadly.

Quality is more important than tutorial count.

## Tutorial 1 — Decision Before Execution

### Learning Objective

Understand why consequential execution should be separated from the request that proposes it.

### Planned Topics

- Intent versus execution.
- Why authorization alone may not describe the entire decision.
- Policy and contextual evaluation.
- Explicit decision results.
- Host-owned execution.
- Audit evidence.

### Minimal Flow

```text
Request
   ↓
Intent
   ↓
Governance Decision
   ↓
Execution Boundary
   ↓
Host Operation
```

- [ ] Problem statement.
- [ ] Naive implementation.
- [ ] Failure modes.
- [ ] Minimal C# example.
- [ ] Sequence diagram.
- [ ] Tradeoff analysis.
- [ ] Link to working `AsiBackbone` implementation.
- [ ] Beginner lab.

## Tutorial 2 — Policy Context and Explicit Decision Outcomes

### Learning Objective

Understand why the facts used to make a governance decision should be represented explicitly.

### Planned Topics

- Policy context.
- Actor and resource information.
- Operation metadata.
- Environmental context.
- Structured reason codes.
- Explicit outcomes.

Expected outcomes include:

```text
Allow
Deny
Defer
RequireAcknowledgment
Escalate
```

- [ ] Context model example.
- [ ] Decision result example.
- [ ] Policy evaluation example.
- [ ] Diagram.
- [ ] Tests.
- [ ] Link to working implementation.

## Tutorial 3 — Acknowledgment and Audit Residue

### Learning Objective

Understand how a consequential operation can pause for acknowledgment while preserving evidence of what occurred.

### Planned Topics

- Acknowledgment as a governance boundary.
- Human responsibility.
- Decision lineage.
- Reason codes.
- Correlation identifiers.
- Policy versioning.
- Audit residue.
- Difference between logging and governance evidence.

- [ ] Minimal acknowledgment workflow.
- [ ] Audit receipt model.
- [ ] Sequence diagram.
- [ ] Failure-mode examples.
- [ ] Link to working implementation.
- [ ] Intermediate lab.

## Tutorial 4 — Scoped Capability and Host-Owned Execution

### Learning Objective

Understand why approval does not necessarily imply broad or permanent execution authority.

### Planned Topics

- Scoped capability.
- Short-lived authority.
- Operation binding.
- Resource binding.
- Replay considerations.
- Validation boundaries.
- Host responsibility.

- [ ] Capability model example.
- [ ] Validation example.
- [ ] Execution-boundary example.
- [ ] Threat considerations.
- [ ] Link to working implementation.

---

# Phase 4 — Governed AI Tool Gateway

## Goal

Create the first end-to-end tutorial showing how governance patterns can be applied to AI-assisted execution.

## Tutorial — Building a Governed AI Tool Gateway in ASP.NET Core

### Core Principle

> **The model may propose. The host retains execution authority.**

### Planned Flow

```text
User request
   ↓
AI proposes tool action
   ↓
Host constructs policy context
   ↓
Governance evaluation
   ↓
Allow / Deny / Defer / Acknowledge / Escalate
   ↓
Optional scoped capability
   ↓
Host validates authority
   ↓
Host invokes tool
   ↓
Audit residue
```

### Planned Topics

- Distinguishing AI inference from execution authority.
- Tool proposal modeling.
- Policy context.
- Explicit decision outcomes.
- Human acknowledgment.
- Capability-scoped execution.
- Tool allowlists.
- Argument validation.
- Logging versus audit residue.
- Failure handling.
- Least authority.
- Host-side safety boundaries.

### Deliverables

- [ ] Architecture overview.
- [ ] Mermaid sequence diagram.
- [ ] Minimal ASP.NET Core sample.
- [ ] Mock AI/tool interface.
- [ ] Policy evaluation example.
- [ ] Acknowledgment flow.
- [ ] Capability validation example.
- [ ] Audit example.
- [ ] Tests.
- [ ] Threat-model notes.
- [ ] Lab exercise.
- [ ] Links to relevant `AsiBackbone` implementation.

---

# Phase 5 — ASP.NET Core Architecture Learning

## Goal

Use `NetCoreApplicationTemplate` as a working reference specimen for broader application architecture lessons.

## Planned Learning Areas

### Middleware Ordering

- [ ] Why middleware order changes behavior.
- [ ] Exception handling boundaries.
- [ ] Authentication and authorization placement.
- [ ] Security header placement.
- [ ] Request logging.
- [ ] Rate limiting.
- [ ] Reverse proxy considerations.

### Secure Defaults

- [ ] Secure-by-default configuration.
- [ ] Explicit opt-in versus implicit exposure.
- [ ] Configuration validation.
- [ ] Environment-specific behavior.
- [ ] Secrets handling.

### Structured Logging

- [ ] Events versus strings.
- [ ] Correlation.
- [ ] Operational diagnostics.
- [ ] Avoiding sensitive-data leakage.
- [ ] Distinguishing operational logs from audit records.

### Error Handling

- [ ] Centralized exception handling.
- [ ] Problem Details.
- [ ] Information disclosure.
- [ ] Status-code handling.
- [ ] Observability considerations.

### Data Access

- [ ] EF Core boundaries.
- [ ] Persistence abstractions.
- [ ] Transaction reasoning.
- [ ] Interceptors and cross-cutting behavior.
- [ ] Local versus production storage choices.

### Architecture Decision Records

- [ ] Why ADRs matter.
- [ ] How to write an ADR.
- [ ] How to revisit a decision.
- [ ] How ADRs connect implementation to learning material.

---

# Phase 6 — Hands-On Labs

## Goal

Move beyond reading and require learners to make architectural decisions.

## Beginner Labs

- [ ] Separate intent from execution.
- [ ] Replace boolean authorization with explicit decision outcomes.
- [ ] Build a small policy-context model.
- [ ] Add structured reason codes.
- [ ] Identify middleware ordering problems.

## Intermediate Labs

- [ ] Add acknowledgment to a consequential workflow.
- [ ] Preserve an audit receipt.
- [ ] Introduce capability-scoped execution.
- [ ] Build a simple governed API operation.
- [ ] Refactor scattered governance logic into a decision pipeline.

## Advanced Labs

- [ ] Govern an AI tool call.
- [ ] Design a replay-resistant capability workflow.
- [ ] Compare two competing policy architectures.
- [ ] Threat-model a governed execution gateway.
- [ ] Design a regional or tenant-specific policy layer.
- [ ] Analyze a deliberately flawed high-consequence workflow.

## Lab Design Standard

Each lab should identify:

```text
Learning Objective
   ↓
Starting Architecture
   ↓
Constraints
   ↓
Learner Task
   ↓
Validation
   ↓
Discussion / Solution
```

---

# Phase 7 — Security and Trust Architecture

## Goal

Teach security as an architectural property rather than a collection of isolated controls.

## Planned Topics

- [ ] Trust boundaries.
- [ ] Least privilege.
- [ ] Capability-based authority.
- [ ] Authentication versus governance.
- [ ] Authorization versus policy evaluation.
- [ ] Replay protection.
- [ ] Signing and verification concepts.
- [ ] Key custody boundaries.
- [ ] Tamper-evident records.
- [ ] Secure logging.
- [ ] Secret handling.
- [ ] Dependency integrity.
- [ ] Supply-chain provenance.
- [ ] GitHub Actions SHA pinning.
- [ ] SBOM concepts.
- [ ] Security failure modes.

These tutorials should clearly distinguish educational examples from production security guarantees.

---

# Phase 8 — Governance and Policy Architecture

## Goal

Expand the conceptual vocabulary around policy-governed systems.

## Planned Topics

- [ ] Policy pipeline design.
- [ ] Constraint composition.
- [ ] Policy precedence.
- [ ] Policy versioning.
- [ ] Regional policy overlays.
- [ ] Tenant-specific policy.
- [ ] Risk-based decisions.
- [ ] Human-in-the-loop workflows.
- [ ] Escalation patterns.
- [ ] Decision provenance.
- [ ] Policy failure behavior.
- [ ] Degraded-mode decisions.
- [ ] Deterministic versus probabilistic policy inputs.
- [ ] Policy testing.

---

# Phase 9 — Architecture Comparisons

## Goal

Help readers understand where demonstrated patterns fit relative to other approaches.

Potential comparison areas may include:

- [ ] Traditional role-based authorization.
- [ ] Claims-based authorization.
- [ ] Policy-based authorization.
- [ ] Capability-based security.
- [ ] API gateways.
- [ ] Service meshes.
- [ ] Workflow engines.
- [ ] Policy engines.
- [ ] Agent/tool authorization models.
- [ ] Human approval systems.
- [ ] Event-sourced audit approaches.

Comparisons should avoid framing other architectures as competitors merely because they solve adjacent problems.

The objective is to clarify boundaries and tradeoffs.

---

# Phase 10 — Community Learning Loop

## Goal

Allow community questions and contributions to influence what is taught next.

## Planned Community Artifacts

- [ ] `community/tutorial-ideas.md`
- [ ] `community/requested-topics.md`
- [ ] `community/contributors.md`
- [ ] Architecture question template.
- [ ] Tutorial proposal template.
- [ ] Lab proposal template.
- [ ] Alternative-pattern proposal guidance.

## Learning Feedback Cycle

```text
Question
   ↓
Discussion
   ↓
Experiment or competing approaches
   ↓
Tutorial
   ↓
Lab
   ↓
Working implementation reference
   ↓
Feedback
   ↺
```

Useful community questions should be candidates for future documentation.

Repeated misunderstandings are signals that existing explanations should improve.

---

# Phase 11 — Reference Architecture Case Studies

## Goal

Show how multiple patterns interact in realistic systems without turning the Learning repository into another production framework.

Potential case studies include:

- [ ] Governed administrative operation.
- [ ] Sensitive-data access decision.
- [ ] Deployment approval gateway.
- [ ] Infrastructure change gate.
- [ ] AI-assisted API operation.
- [ ] AI tool gateway.
- [ ] Multi-tenant policy evaluation.
- [ ] Human acknowledgment workflow.
- [ ] Capability-scoped background operation.
- [ ] Simulated robotics-command governance boundary.

Each case study should separate:

```text
Architecture
Implementation
Operational responsibility
Security responsibility
Governance responsibility
```

---

# Phase 12 — Advanced and Experimental Material

## Goal

Provide a clearly labeled area for ideas that are useful to explore but should not yet be presented as established guidance.

Possible subjects include:

- [ ] Distributed governance.
- [ ] Multi-region policy coordination.
- [ ] Durable decision ledgers.
- [ ] Cryptographic audit chains.
- [ ] External policy providers.
- [ ] Policy simulation.
- [ ] Governance telemetry.
- [ ] Decision explainability.
- [ ] Adaptive risk context.
- [ ] Robotics gateway patterns.
- [ ] Agent-to-agent governed execution.
- [ ] Cross-system capability exchange.

Experimental material should clearly state assumptions, uncertainties, and unresolved questions.

---

# Documentation Quality Goals

As the repository grows, documentation should increasingly provide:

- [ ] Consistent tutorial structure.
- [ ] Learning objectives.
- [ ] Difficulty indicators.
- [ ] Prerequisites.
- [ ] Estimated scope rather than artificial completion times.
- [ ] Diagrams.
- [ ] Runnable examples.
- [ ] Tests.
- [ ] Tradeoff sections.
- [ ] "When not to use this" guidance.
- [ ] Links to implementation repositories.
- [ ] Links to relevant ADRs.
- [ ] Related tutorials.
- [ ] Suggested labs.
- [ ] Clear canonical/alternative/experimental status where relevant.

---

# Repository Automation Goals

Automation should support quality without making contribution unnecessarily difficult.

Potential automation includes:

- [ ] Markdown validation.
- [ ] Link checking.
- [ ] DocFX build validation.
- [ ] Example build validation.
- [ ] Test execution.
- [ ] Dependency updates.
- [ ] Secret scanning.
- [ ] Code scanning where applicable.
- [ ] GitHub Actions dependency pinning.
- [ ] Pull request validation.
- [ ] GitHub Pages deployment.
- [ ] Documentation artifact generation.

Automation should be introduced when it provides meaningful protection or reduces maintenance burden.

---

# Contribution Growth Goals

As participation develops, the project may introduce:

- [ ] `good first tutorial` issues.
- [ ] Documentation-only starter issues.
- [ ] Diagram contribution opportunities.
- [ ] Lab review contributors.
- [ ] Topic-specific reviewers.
- [ ] Additional maintainers.
- [ ] Contributor recognition.
- [ ] Community-authored alternative patterns.
- [ ] Translation support if demand emerges.

Governance should evolve only when sustained participation creates a practical need.

---

# What Is Not Currently a Roadmap Goal

The Learning repository is not currently intended to become:

- A replacement for `AsiBackbone`.
- A replacement for `NetCoreApplicationTemplate`.
- A general-purpose .NET framework.
- A package distribution repository.
- An AI model host.
- An autonomous-agent runtime.
- A compliance product.
- A certification program.
- A production robotics controller.
- A repository that attempts to prove the broader theoretical ASI Backbone or Eden Hypothesis framework.

Its purpose is narrower:

> **Teach architectural reasoning through patterns, examples, working references, experiments, and community discussion.**

---

# Near-Term Priorities

The highest-priority work after repository setup is:

1. Establish the DocFX documentation structure.
2. Publish **Decision Before Execution**.
3. Publish **Policy Context and Explicit Decision Outcomes**.
4. Publish **Acknowledgment and Audit Residue**.
5. Publish **Scoped Capability and Host-Owned Execution**.
6. Build the first end-to-end **Governed AI Tool Gateway** tutorial.
7. Add at least one beginner and one intermediate lab.
8. Connect tutorials directly to relevant files and ADRs in the working repositories.
9. Establish Issue, Discussion, and pull request contribution pathways.
10. Use early community feedback to determine the next tutorial areas.

---

# Measuring Progress

Progress should not be measured only by repository size or package adoption.

Useful signals include:

- Tutorials completed.
- Labs completed.
- Examples that compile and remain current.
- Documentation build health.
- Community Issues and Discussions.
- Pull requests from outside maintainers.
- Corrections prompted by readers.
- Architectural questions converted into tutorials.
- Patterns reused outside the ASI Backbone repositories.
- Links or references from other projects.
- Contributors who begin with documentation and later participate in implementation repositories.

The Learning repository succeeds when it helps people reason more clearly about architecture.

---

# Long-Term Direction

Over time, the Learning repository may become the primary educational entry point into the ASI Backbone organization.

A mature ecosystem could look like:

```text
                    ASI Backbone Organization
                             |
             +---------------+---------------+
             |                               |
             v                               v
      Learning Repository              Discussions
     education + examples          questions + debate
             |                               |
             +---------------+---------------+
                             |
                             v
                  Architectural Patterns
                             |
                +------------+------------+
                |                         |
                v                         v
        AsiBackbone              NetCoreApplicationTemplate
   governance implementation      application architecture
                |                         |
                +------------+------------+
                             |
                             v
                    Community Feedback
                             |
                             +-------> Learning
```

The intended cycle is:

> **Theory becomes pattern. Pattern becomes example. Example meets implementation. Implementation produces experience. Experience improves the lesson.**

---

## Roadmap Status

This roadmap is intentionally living.

Items may be added, reordered, split, merged, deferred, or removed as the project develops.

Changes should continue to serve the central principle:

> **Read it. Run it. Question it. Improve it.**
