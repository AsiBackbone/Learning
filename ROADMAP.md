# Roadmap

## Purpose

`AsiBackbone/Learning` is an open, community-oriented learning resource for practical .NET architecture, governed execution, policy-driven systems, secure application design, AI integration, and related architectural patterns.

This roadmap describes the intended direction of the Learning repository.

It is not a fixed release contract. Priorities may change as the repository develops, contributors identify better learning paths, implementation repositories evolve, or community feedback reveals more useful areas to explore.

The project should continue to grow deliberately:

> **Start with strong architectural lessons, make them runnable, turn them into exercises, connect them to working implementations, and improve them through use and contribution.**

---

# Current Project Status

**Active development — foundational tutorial/sample/test/lab path established**

The repository has moved beyond its initial scaffolding phase.

The initial five-tutorial learning path is now established, and the foundational
sequence is supported by runnable companion samples, focused invariant tests,
and tutorial-aligned learner exercises or labs.

1. [Decision Before Execution](docs/tutorials/decision-before-execution.md)
2. [Policy Context and Explicit Decision Outcomes](docs/tutorials/policy-context-and-explicit-decision-outcomes.md)
3. [Acknowledgment and Audit Residue](docs/tutorials/acknowledgment-and-audit-residue.md)
4. [Scoped Capability and Host-Owned Execution](docs/tutorials/scoped-capability-and-host-owned-execution.md)
5. [Governed AI Tool Gateway](docs/tutorials/governed-ai-tool-gateway.md)

Together, these tutorials establish the foundational governed-execution sequence:

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

The current development emphasis is now:

```text
Established Foundation
   ↓
Working Implementation References
   ↓
Deeper Diagnostic / Tradeoff Labs
   ↓
ASP.NET Core Architecture
   ↓
Security and Trust Architecture
   ↓
Governance and Advanced Material
```

The next major objective is therefore not to rebuild the foundation or simply add
more written material.

It is to deepen the established path, strengthen its connection to real
implementations, and begin the next architecture subjects without sacrificing
cohesion.

---

# Guiding Goals

The Learning repository should become:

* A clear educational entry point into the ASI Backbone organization.
* A practical learning resource rather than a product manual.
* A bridge between architectural reasoning and working .NET implementations.
* A source of intentionally small executable examples.
* A home for hands-on architectural labs and experiments.
* A place where canonical and alternative patterns can be compared.
* A community contribution surface that is easier to enter than the core implementation repositories.
* A durable record of lessons learned from the evolution of `AsiBackbone` and `NetCoreApplicationTemplate`.
* A resource that remains valuable even to developers who never adopt an ASI Backbone package.

---

# Relationship to the Other Repositories

The three primary organization repositories serve different roles:

```text
AsiBackbone/Learning
    |
    | teaches concepts, patterns, tradeoffs,
    | samples, labs, and architectural reasoning
    |
    +------> AsiBackbone/AsiBackbone
    |        governance and policy-control implementation
    |
    +------> AsiBackbone/NetCoreApplicationTemplate
             ASP.NET Core reference architecture implementation
```

Learning should **explain and connect**.

It should not duplicate the complete technical documentation of either implementation repository.

A Learning tutorial should expose the architectural idea clearly.

A sample should demonstrate the idea with minimal executable code.

A lab should require the learner to reason about the idea.

The working repositories should show how similar ideas appear in more complete software.

---

# Roadmap Principles

## Problem First

Learning material should begin with the problem being solved rather than with a package API.

The preferred progression remains:

```text
Problem
   ↓
Common or naive implementation
   ↓
Failure mode or limitation
   ↓
Architectural pattern
   ↓
Minimal teaching example
   ↓
Tradeoffs and alternatives
   ↓
Working implementation reference
```

## Small Examples

Teaching examples should remain intentionally smaller than production implementations.

Complexity should be introduced only when it contributes directly to the lesson.

## Runnable Where Practical

Conceptual material should increasingly be paired with executable examples.

Readers should be able to move from:

> “I understand the idea.”

to:

> “I can run the idea and observe the boundary.”

## Test Architectural Invariants

Tests should demonstrate architectural behavior rather than merely object construction.

For example:

```text
Denied decision
   ↓
Executor invocation count = 0
```

```text
Expired capability
   ↓
Execution blocked
```

```text
AI proposes an unknown tool
   ↓
Host rejects proposal
   ↓
No execution occurs
```

## Working References

Where useful, tutorials and samples should point to real implementation files, tests, ADRs, or documentation in:

* `AsiBackbone/AsiBackbone`
* `AsiBackbone/NetCoreApplicationTemplate`

## Explicit Tradeoffs

Material should explain:

* Benefits
* Costs
* Failure modes
* Alternatives
* Operational assumptions
* Cases where a simpler architecture may be preferable
* Cases where the demonstrated pattern should not be used

## Clear Boundaries

For AI-related material, the central teaching boundary remains:

> **The model may propose. The host retains execution authority.**

Prompt instructions, tool descriptions, and model behavior may influence proposals.

They do not replace host-side policy, validation, authorization, or execution controls.

## Canonical Does Not Mean Universal

A canonical pattern represents an approach aligned with one or more current ASI Backbone organization implementations.

It does not mean that the approach is universally correct.

Alternative architectures are welcome when they are technically grounded and their tradeoffs are explained.

## Community Evolution

Questions, corrections, experiments, disagreements, and alternative implementations should influence future material.

Repeated confusion is evidence that a lesson may need improvement.

## Licensing Clarity

The repository intentionally uses component-specific licensing:

```text
Documentation
Educational material
Community material
Diagrams and exercises
        ↓
CC BY 4.0

Executable sample code
under samples/
        ↓
MIT
```

Source-code snippets embedded in documentation are additionally available under the MIT License unless otherwise noted.

See [LICENSING.md](LICENSING.md) for the complete licensing policy.

---

# Milestone 1 — Repository and Publication Foundation

## Status

**Complete**

## Completed Foundation

* [x] Create root `README.md`.
* [x] Add `CODE_OF_CONDUCT.md`.
* [x] Add `CONTRIBUTING.md`.
* [x] Add `GOVERNANCE.md`.
* [x] Add `ROADMAP.md`.
* [x] Establish component-specific licensing.
* [x] Add CC BY 4.0 licensing for documentation and educational material.
* [x] Add MIT licensing for executable sample code.
* [x] Add `LICENSING.md`.
* [x] Add `LICENSES/` license references.
* [x] Add `CITATION.cff`.
* [x] Add Zenodo metadata.
* [x] Add repository description.
* [x] Establish `community/`.
* [x] Add `community/tutorial-ideas.md`.
* [x] Add `community/requested-topics.md`.
* [x] Add `community/contributors.md`.
* [x] Add `SECURITY.md`.
* [x] Add repository topics.
* [x] Review repository rules or branch protection.
* [x] Add Issue templates.
* [x] Add pull request template.
* [x] Add or refine contributor labels.
* [x] Improve direct linking to organization Discussions.
* [x] Add contribution pathways for tutorial, lab, sample, and alternative-pattern proposals.

## Ongoing Maintenance

* [ ] Review repository metadata periodically as the project matures.

## Contributor Label Taxonomy

Repository labels should remain composable rather than encoding every possible combination as a separate label.

For example:

```
good first issue + tutorial
good first issue + sample
help wanted + lab
question + tutorial
alternative pattern + advanced
```

Learning-specific labels include:

* `tutorial`
* `sample`
* `lab`
* `diagram`
* `canonical pattern`
* `alternative pattern`
* `experimental`
* `example wanted`
* `needs explanation`
* `beginner`
* `intermediate`
* `advanced`

These complement GitHub's standard workflow labels such as `bug`, `documentation`, `enhancement`, `good first issue`, `help wanted`, and `question`.

The intent is to make Issues discoverable by **content area, architectural classification, difficulty, and contribution opportunity** without creating redundant compound labels.

---

# Milestone 2 — Documentation Platform ([#5](https://github.com/AsiBackbone/Learning/issues/5))

## Status

**Complete — baseline documentation platform established**

## Completed

* [x] Add DocFX tooling configuration.
* [x] Add `docs/docfx.json`.
* [x] Add documentation home page.
* [x] Add Getting Started content.
* [x] Add table-of-contents files.
* [x] Add tutorial navigation.
* [x] Add lab navigation foundation.
* [x] Add documentation build validation.
* [x] Treat DocFX warnings as build failures.
* [x] Add GitHub Pages publishing workflow.
* [x] Publish the documentation through GitHub Pages.
* [x] Keep Markdown files readable independently of the generated site.
* [x] Add or improve local documentation build instructions.
* [x] Add automated link validation where practical.
* [x] Add documentation build status to the repository README if useful.
* [x] Improve cross-navigation between tutorials, samples, labs, and working repositories.
* [x] Add related-content links to tutorial pages.
* [x] Add difficulty and prerequisite metadata where useful.
* [x] Review accessibility as diagrams and richer content are added.

## Ongoing Maintenance

* [ ] Re-review accessibility when substantial new diagrams, media, interactive content, or theme customizations are introduced.

## Documentation Goal

The documentation site should provide a guided learning experience while GitHub remains the primary source-control and collaboration surface.

The repository Markdown remains canonical.

---

# Milestone 3 — Foundational Architecture Learning Path

## Status

**Complete — foundational five-tutorial sequence established**

Quality and refinement now matter more than adding another foundational tutorial merely to increase tutorial count.

## Tutorial 1 — Decision Before Execution

* [x] Publish foundational tutorial.
* [x] Explain intent versus execution.
* [x] Show policy/context evaluation.
* [x] Model explicit decision outcomes.
* [x] Preserve host-owned execution.
* [x] Discuss failure modes and tradeoffs.
* [x] Pair with executable companion sample.
* [x] Pair with beginner lab.
* [x] Strengthen links to working implementation references.

## Tutorial 2 — Policy Context and Explicit Decision Outcomes

* [x] Publish foundational tutorial.
* [x] Explain explicit policy context.
* [x] Model actor, resource, operation, and environment information.
* [x] Demonstrate structured decision results.
* [x] Cover meaningful outcomes:

```text
Allow
Deny
Defer
RequireAcknowledgment
Escalate
```

* [x] Pair with executable companion sample.
* [x] Pair with learner exercise.
* [x] Strengthen links to working implementation references.

## Tutorial 3 — Acknowledgment and Audit Residue

* [x] Publish foundational tutorial.
* [x] Explain acknowledgment as a governance boundary.
* [x] Preserve distinction between acknowledgment and authorization.
* [x] Explain decision lineage and audit residue.
* [x] Address reason codes, correlation, and policy identity.
* [x] Distinguish operational logging from governance evidence.
* [x] Pair with executable companion sample.
* [x] Pair with intermediate lab.
* [x] Strengthen links to working implementation references.

## Tutorial 4 — Scoped Capability and Host-Owned Execution

* [x] Publish foundational tutorial.
* [x] Explain narrow execution authority.
* [x] Address short-lived capabilities.
* [x] Cover actor, operation, resource, audience, and expiration bindings.
* [x] Preserve host-owned execution.
* [x] Discuss replay and validation boundaries.
* [x] Pair with executable companion sample. ([#3](https://github.com/AsiBackbone/Learning/issues/3))
* [x] Add capability-focused lab. ([#3](https://github.com/AsiBackbone/Learning/issues/3))
* [x] Strengthen links to working implementation references. ([#18](https://github.com/AsiBackbone/Learning/issues/18))

## Tutorial 5 — Governed AI Tool Gateway

* [x] Publish end-to-end tutorial.
* [x] Separate AI inference from execution authority.
* [x] Model tool proposals.
* [x] Build authoritative host-side context.
* [x] Apply explicit governance decisions.
* [x] Include acknowledgment boundaries.
* [x] Include scoped authority.
* [x] Preserve host-owned execution.
* [x] Address tool allowlists and argument validation.
* [x] Discuss audit residue and failure handling.
* [x] Pair with executable companion sample. ([#4](https://github.com/AsiBackbone/Learning/issues/4))
* [x] Add end-to-end lab. ([#4](https://github.com/AsiBackbone/Learning/issues/4))
* [x] Expand threat-model exercises. ([#4](https://github.com/AsiBackbone/Learning/issues/4))

---

# Milestone 4 — Executable Companion Samples

## Status

**Foundational sample set complete — ongoing refinement remains**

The `samples/` area now contains executable companions for all five foundational tutorials, including focused invariant tests and the Governed AI Tool Gateway capstone.

Future work should refine coverage, improve cross-links, and add samples for new learning areas only where runnable code materially improves understanding.

## Goal

Each foundational tutorial should eventually have a companion sample where executable code materially improves understanding.

The intended relationship is:

```text
Tutorial
   ↓
Minimal embedded example
   ↓
Runnable sample
   ↓
Tests
   ↓
Working repository implementation
```

Samples are teaching artifacts.

They should not attempt to reproduce the full `AsiBackbone` or `NetCoreApplicationTemplate` implementations.

## Foundational Sample Set

* [x] Decision Before Execution sample.
* [x] Policy Context and Explicit Decision Outcomes sample.
* [x] Acknowledgment and Audit Residue sample.
* [x] Scoped Capability and Host-Owned Execution sample. ([#3](https://github.com/AsiBackbone/Learning/issues/3))
* [x] Governed AI Tool Gateway sample. ([#4](https://github.com/AsiBackbone/Learning/issues/4))

## Sample Infrastructure

* [x] Establish buildable sample solution structure. ([#1](https://github.com/AsiBackbone/Learning/issues/1))
* [x] Ensure repository-level `dotnet restore` works for samples. ([#1](https://github.com/AsiBackbone/Learning/issues/1))
* [x] Ensure repository-level `dotnet build` works for samples. ([#1](https://github.com/AsiBackbone/Learning/issues/1))
* [x] Ensure repository-level `dotnet test` works for samples. ([#1](https://github.com/AsiBackbone/Learning/issues/1))
* [x] Add sample build validation to CI. ([#1](https://github.com/AsiBackbone/Learning/issues/1))
* [x] Add tests for architectural invariants. ([#2](https://github.com/AsiBackbone/Learning/issues/2))
* [x] Add per-sample README files where setup or explanation is needed. ([#6](https://github.com/AsiBackbone/Learning/issues/6))
* [x] Cross-link each sample to its tutorial. ([#6](https://github.com/AsiBackbone/Learning/issues/6))
* [x] Cross-link each tutorial to its sample. ([#6](https://github.com/AsiBackbone/Learning/issues/6))
* [x] Link samples to fuller working repository implementations where useful. ([#18](https://github.com/AsiBackbone/Learning/issues/18))

## Sample Design Standard

Samples should:

* Remain intentionally small.
* Keep side effects visible.
* Separate policy evaluation from execution.
* Prefer framework-neutral concepts where practical.
* Use fictional or placeholder data.
* Avoid real credentials.
* Prefer deterministic local behavior.
* Use mocks, fakes, simulation, or dry-run behavior for external operations.
* Test important architectural boundaries.
* Avoid broad execution primitives unless they are specifically the lesson.
* Keep external secrets and infrastructure authority host-owned.

## Dry-Run Principle

Consequential examples should generally begin with:

```text
Governance Decision
   ↓
Capability Validation
   ↓
WouldExecute = true
```

rather than immediately invoking real external systems.

---

# Milestone 5 — Hands-On Labs

## Status

**Foundational tutorial-aligned lab path established — advanced lab coverage expanding**

Each foundational tutorial now has a learner exercise or lab. Replay-resistance,
safe degraded-mode, and policy-simulation/change-impact exercises are established.
Remaining dedicated lab work should deepen decision-pipeline refactoring,
policy-architecture comparisons, regional/tenant policy exercises, deliberately
flawed high-consequence workflows, and broader ASP.NET Core and security scenarios.

Tutorials explain.

Samples demonstrate.

Labs should require the learner to decide.

## Goal

Move learners from architectural recognition to architectural reasoning.

A lab should generally follow:

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

## Beginner Labs

Potential initial labs:

* [x] Separate intent from execution.
* [x] Replace boolean authorization with explicit decision outcomes.
* [x] Build a small policy-context model.
* [x] Add structured reason codes.
* [x] Identify a hidden execution side effect. ([#19](https://github.com/AsiBackbone/Learning/issues/19))
* [x] Identify middleware ordering problems. ([#34](https://github.com/AsiBackbone/Learning/issues/34))

## Intermediate Labs

* [x] Add acknowledgment to a consequential workflow.
* [x] Preserve an audit receipt.
* [x] Introduce capability-scoped execution. ([#3](https://github.com/AsiBackbone/Learning/issues/3))
* [x] Build a governed API operation. ([#54](https://github.com/AsiBackbone/Learning/issues/54))
* [ ] Refactor scattered governance logic into a decision pipeline. — **Still needed.**
  Dedicated lab work is tracked by [#221](https://github.com/AsiBackbone/Learning/issues/221).
* [x] Detect stale or mismatched execution authority. ([#3](https://github.com/AsiBackbone/Learning/issues/3))
* [x] Add policy-version evidence to a decision path. ([#37](https://github.com/AsiBackbone/Learning/issues/37))

## Advanced Labs

* [x] Govern an AI tool call. ([#4](https://github.com/AsiBackbone/Learning/issues/4))
* [x] Design a replay-resistant capability workflow. ([#65](https://github.com/AsiBackbone/Learning/issues/65))
* [ ] Compare competing policy architectures. — **Still needed as a dedicated lab.**
  Architecture comparisons exist, but they do not replace a learner exercise.
* [x] Threat-model a governed execution gateway. ([#4](https://github.com/AsiBackbone/Learning/issues/4))
* [ ] Design a regional or tenant-specific policy layer. — **Partially covered.**
  Advanced and case-study material exists; a dedicated lab remains useful.
* [ ] Analyze a deliberately flawed high-consequence workflow. — **Still needed.**
* [x] Design safe degraded-mode behavior. ([#66](https://github.com/AsiBackbone/Learning/issues/66))
* [ ] Critique an architecture where an AI agent owns both proposal and execution authority.
  — **Partially covered.** Existing governed-AI material teaches the boundary; a
  focused critique lab remains unfinished.

## Lab Quality Standard

Each lab should provide enough information to solve the problem without dictating a single implementation unnecessarily.

Where multiple valid solutions exist, the discussion should explain the tradeoffs rather than present one answer as universally correct.

---

# Milestone 6 — ASP.NET Core Architecture Learning ([#5](https://github.com/AsiBackbone/Learning/issues/5))

## Status

**Complete — ASP.NET Core runtime foundations and the ADR reasoning, case-study, and hands-on learning path are established**

## Goal

Use `NetCoreApplicationTemplate` as a working reference specimen for broader application-architecture lessons.

These materials should remain useful independently of that repository.

## Middleware Ordering ([#20](https://github.com/AsiBackbone/Learning/issues/20))

* [x] Why middleware order changes behavior.
* [x] Exception handling boundaries.
* [x] Authentication placement.
* [x] Authorization placement.
* [x] Security-header placement.
* [x] Request logging.
* [x] Rate limiting.
* [x] Reverse-proxy considerations.
* [x] Failure modes caused by incorrect ordering.
* [x] Pair with a runnable corrected/incorrect middleware-ordering sample.
* [x] Add focused tests for request/response traversal and exception-boundary placement.

## Secure Defaults ([#36](https://github.com/AsiBackbone/Learning/issues/36))

* [x] Secure-by-default configuration.
* [x] Explicit opt-in versus implicit exposure.
* [x] Configuration validation.
* [x] Environment-specific behavior.
* [x] Secrets handling.
* [x] Safer failure defaults.
* [x] Configuration ownership boundaries.

## Structured Logging ([#38](https://github.com/AsiBackbone/Learning/issues/38))

* [x] Events versus strings.
* [x] Correlation.
* [x] Operational diagnostics.
* [x] Avoiding sensitive-data leakage.
* [x] Logging boundaries.
* [x] Distinguishing operational logs from audit records.

## Error Handling ([#51](https://github.com/AsiBackbone/Learning/issues/51))

* [x] Centralized exception handling.
* [x] Problem Details.
* [x] Information disclosure.
* [x] Status-code handling.
* [x] Observability considerations.
* [x] Safe failure behavior.
* [x] Pair with a runnable sample and focused integration tests. ([#51](https://github.com/AsiBackbone/Learning/issues/51))

## Data Access ([#55](https://github.com/AsiBackbone/Learning/issues/55))

* [x] EF Core boundaries.
* [x] Persistence abstractions.
* [x] Transaction reasoning.
* [x] Interceptors and cross-cutting behavior.
* [x] Local versus production storage choices.
* [x] Data-access failure boundaries.

## Architecture Decision Records

* [x] Why ADRs matter. ([#87](https://github.com/AsiBackbone/Learning/issues/87))
* [x] How to write an ADR. ([#87](https://github.com/AsiBackbone/Learning/issues/87), [#90](https://github.com/AsiBackbone/Learning/issues/90))
* [x] How to revisit a decision. ([#88](https://github.com/AsiBackbone/Learning/issues/88), [#90](https://github.com/AsiBackbone/Learning/issues/90))
* [x] How ADRs preserve architectural reasoning. ([#87](https://github.com/AsiBackbone/Learning/issues/87))
* [x] How ADRs connect implementation repositories to Learning material. ([#89](https://github.com/AsiBackbone/Learning/issues/89))
* [x] Pair ADR writing and lifecycle review with a hands-on reasoning lab. ([#90](https://github.com/AsiBackbone/Learning/issues/90))

The ADR learning path now covers why ADRs matter, when a decision merits a record, a practical writing structure, preservation of alternatives and consequences, lifecycle review through retained, deprecated, and superseded decisions, a working-repository case study connecting `NetCoreApplicationTemplate` ADRs to concrete implementation evidence, and a two-stage hands-on lab that requires learners to write and revisit a decision under changed constraints.

---

# Milestone 7 — Security and Trust Architecture ([#5](https://github.com/AsiBackbone/Learning/issues/5))

## Status

**Complete — dedicated Security and Trust Architecture foundation established**

## Goal

Teach security as an architectural property rather than a collection of isolated controls.

## Planned Topics

* [x] Trust boundaries. ([#21](https://github.com/AsiBackbone/Learning/issues/21))
* [x] Least privilege. ([#21](https://github.com/AsiBackbone/Learning/issues/21))
* [x] Capability-based authority — [Scoped Capability and Host-Owned Execution](docs/tutorials/scoped-capability-and-host-owned-execution.md).
* [x] Authentication versus governance. ([#21](https://github.com/AsiBackbone/Learning/issues/21))
* [x] Authorization versus policy evaluation. ([#21](https://github.com/AsiBackbone/Learning/issues/21))
* [x] Replay protection. ([#52](https://github.com/AsiBackbone/Learning/issues/52))
* [x] Signing and verification concepts. ([#64](https://github.com/AsiBackbone/Learning/issues/64))
* [x] Key custody boundaries. ([#64](https://github.com/AsiBackbone/Learning/issues/64))
* [x] Tamper-evident records. ([#64](https://github.com/AsiBackbone/Learning/issues/64))
* [x] Secure logging — [Secure Logging Across Trust Boundaries](docs/security/secure-logging-across-trust-boundaries.md). ([#100](https://github.com/AsiBackbone/Learning/issues/100))
* [x] Secret handling — [Secret Handling Across Trust Boundaries](docs/security/secret-handling-across-trust-boundaries.md). ([#101](https://github.com/AsiBackbone/Learning/issues/101))
* [x] Dependency integrity. ([#76](https://github.com/AsiBackbone/Learning/issues/76))
* [x] Supply-chain provenance. ([#76](https://github.com/AsiBackbone/Learning/issues/76))
* [x] GitHub Actions SHA pinning. ([#76](https://github.com/AsiBackbone/Learning/issues/76))
* [x] SBOM concepts. ([#76](https://github.com/AsiBackbone/Learning/issues/76))
* [x] Fail-open versus fail-closed behavior. ([#66](https://github.com/AsiBackbone/Learning/issues/66))
* [x] Security failure modes. ([#66](https://github.com/AsiBackbone/Learning/issues/66))
* [x] Threat modeling as architecture reasoning — [Threat Modeling as Architecture Reasoning](docs/security/threat-modeling-as-architecture-reasoning.md). ([#102](https://github.com/AsiBackbone/Learning/issues/102))

The dedicated Milestone 7 foundation now connects individual security controls
to a complete architecture-reasoning practice: define scope, identify assets and
authority, mark trust and execution boundaries, enumerate abuse paths, map
mitigations to threats, verify architectural invariants, record residual risk, and
revisit the model as trust relationships change.

Educational security examples should clearly distinguish demonstrated patterns from production security guarantees.

---

# Milestone 8 — Governance and Policy Architecture 

## Status

**Complete — governance and policy architecture learning path established through composition, provenance, overlays, risk, deterministic/probabilistic inputs, human review, escalation, degraded-mode behavior, testing, simulation, and rollout/rollback reasoning**

## Goal

Expand the conceptual vocabulary around policy-governed systems after the foundational learning path is well supported by samples and labs.

## Completed Topics

* [x] Policy pipeline design. ([#44](https://github.com/AsiBackbone/Learning/issues/44))
* [x] Constraint composition. ([#44](https://github.com/AsiBackbone/Learning/issues/44))
* [x] Policy precedence. ([#44](https://github.com/AsiBackbone/Learning/issues/44))
* [x] Policy versioning. ([#53](https://github.com/AsiBackbone/Learning/issues/53))
* [x] Policy identity and hashing. ([#53](https://github.com/AsiBackbone/Learning/issues/53))
* [x] Regional policy overlays. ([#74](https://github.com/AsiBackbone/Learning/issues/74))
* [x] Tenant-specific policy. ([#74](https://github.com/AsiBackbone/Learning/issues/74))
* [x] Risk-based decisions. ([#112](https://github.com/AsiBackbone/Learning/issues/112))
* [x] Human-in-the-loop workflows. ([#113](https://github.com/AsiBackbone/Learning/issues/113))
* [x] Escalation patterns. ([#114](https://github.com/AsiBackbone/Learning/issues/114))
* [x] Decision provenance. ([#53](https://github.com/AsiBackbone/Learning/issues/53))
* [x] Policy failure behavior. ([#66](https://github.com/AsiBackbone/Learning/issues/66))
* [x] Degraded-mode decisions. ([#66](https://github.com/AsiBackbone/Learning/issues/66))
* [x] Deterministic versus probabilistic policy inputs. ([#115](https://github.com/AsiBackbone/Learning/issues/115))
* [x] Policy testing. ([#72](https://github.com/AsiBackbone/Learning/issues/72))
* [x] Policy simulation. ([#116](https://github.com/AsiBackbone/Learning/issues/116))
* [x] Policy rollout and rollback reasoning. ([#53](https://github.com/AsiBackbone/Learning/issues/53))
* [x] Separation between policy decision and operational execution. ([#44](https://github.com/AsiBackbone/Learning/issues/44))

Milestone 8 now includes a hands-on policy-simulation exercise that compares baseline and
candidate decisions before rollout while preserving a strict no-execution boundary.

---

# Milestone 9 — Expanded AI Integration ([#5](https://github.com/AsiBackbone/Learning/issues/5))

## Status

**Complete — expanded AI integration boundaries established across typed proposals, authoritative context, scoped execution, multi-tool recovery, bounded rejection and uncertainty, agent memory, and experimental multi-agent execution**

## Goal

Build beyond the initial Governed AI Tool Gateway without turning Learning into an autonomous-agent framework.

## Planned Topics

* [x] Tool proposal schemas. ([#73](https://github.com/AsiBackbone/Learning/issues/73))
* [x] Tool allowlists. ([#73](https://github.com/AsiBackbone/Learning/issues/73))
* [x] Argument validation. ([#73](https://github.com/AsiBackbone/Learning/issues/73))
* [x] Host-side context reconstruction. ([#73](https://github.com/AsiBackbone/Learning/issues/73))
* [x] Model-provided context versus authoritative context. ([#73](https://github.com/AsiBackbone/Learning/issues/73))
* [x] Capability-scoped tool execution — [Governed AI Tool Gateway](docs/tutorials/governed-ai-tool-gateway.md).
* [x] Multi-tool workflows — [Governed Multi-Tool Workflows and Recovery Boundaries](docs/ai-integration/governed-multi-tool-workflows-and-recovery-boundaries.md). ([#125](https://github.com/AsiBackbone/Learning/issues/125))
* [x] Human acknowledgment for consequential actions — [Governed AI Tool Gateway](docs/tutorials/governed-ai-tool-gateway.md).
* [x] Handling model uncertainty — [AI Proposal Rejection, Uncertainty, and Recovery Patterns](docs/ai-integration/ai-proposal-rejection-uncertainty-and-recovery-patterns.md). ([#126](https://github.com/AsiBackbone/Learning/issues/126))
* [x] AI proposal rejection and recovery — [AI Proposal Rejection, Uncertainty, and Recovery Patterns](docs/ai-integration/ai-proposal-rejection-uncertainty-and-recovery-patterns.md). ([#125](https://github.com/AsiBackbone/Learning/issues/125), [#126](https://github.com/AsiBackbone/Learning/issues/126))
* [x] Tool execution receipts — [Governed AI Tool Gateway](docs/tutorials/governed-ai-tool-gateway.md).
* [x] Prompt injection versus execution controls. ([#73](https://github.com/AsiBackbone/Learning/issues/73))
* [x] Credential ownership — [AI Integration](docs/ai-integration/index.md).
* [x] Agent memory and governance boundaries — [Agent Memory and Governance Boundaries](docs/ai-integration/agent-memory-and-governance-boundaries.md). ([#128](https://github.com/AsiBackbone/Learning/issues/128))
* [x] Multi-agent proposal flows. ([#75](https://github.com/AsiBackbone/Learning/issues/75))
* [x] Agent-to-agent governed execution as experimental material. ([#75](https://github.com/AsiBackbone/Learning/issues/75))

Milestone 9 now covers the planned AI integration boundaries through dedicated
learning material: typed proposal validation, authoritative context reconstruction,
scoped execution, multi-tool recovery, bounded rejection and uncertainty handling,
agent-memory governance, and experimental multi-agent execution. Future work can
refine these areas through executable companions and labs without expanding the
repository into an autonomous-agent framework.

The central rule remains:

> **The model may propose. The host retains execution authority.**

---

# Milestone 10 — Architecture Comparisons and Alternative Patterns

## Status

**Complete — nine alternative-pattern comparisons published** ([#7](https://github.com/AsiBackbone/Learning/issues/7), [#67](https://github.com/AsiBackbone/Learning/issues/67), [#174](https://github.com/AsiBackbone/Learning/issues/174), [#175](https://github.com/AsiBackbone/Learning/issues/175), [#176](https://github.com/AsiBackbone/Learning/issues/176), [#177](https://github.com/AsiBackbone/Learning/issues/177), [#178](https://github.com/AsiBackbone/Learning/issues/178), [#179](https://github.com/AsiBackbone/Learning/issues/179), [#180](https://github.com/AsiBackbone/Learning/issues/180))

## Goal

Help readers understand where demonstrated patterns fit relative to established and adjacent architectural approaches.

Potential comparison areas include:

* [x] Traditional role-based authorization — [Role-Based, Claims-Based, and Capability-Based Authorization](docs/architecture/role-based-claims-based-and-capability-based-authorization.md). ([#7](https://github.com/AsiBackbone/Learning/issues/7), [#174](https://github.com/AsiBackbone/Learning/issues/174))
* [x] Claims-based authorization — [Role-Based, Claims-Based, and Capability-Based Authorization](docs/architecture/role-based-claims-based-and-capability-based-authorization.md). ([#7](https://github.com/AsiBackbone/Learning/issues/7), [#174](https://github.com/AsiBackbone/Learning/issues/174))
* [x] Policy-based authorization — [When ASP.NET Core Authorization Is Enough](docs/architecture/when-aspnet-core-authorization-is-enough.md). ([#7](https://github.com/AsiBackbone/Learning/issues/7))
* [x] Simple application-service boundary — [When a Simple Application Service Is Enough](docs/architecture/when-a-simple-application-service-is-enough.md). ([#67](https://github.com/AsiBackbone/Learning/issues/67))
* [x] Capability-based security — [Role-Based, Claims-Based, and Capability-Based Authorization](docs/architecture/role-based-claims-based-and-capability-based-authorization.md). ([#7](https://github.com/AsiBackbone/Learning/issues/7), [#174](https://github.com/AsiBackbone/Learning/issues/174))
* [x] API gateways — [API Gateways, Service Meshes, Zero Trust, and Governed Execution](docs/architecture/api-gateways-service-meshes-zero-trust-and-governed-execution.md). ([#175](https://github.com/AsiBackbone/Learning/issues/175))
* [x] Service meshes — [API Gateways, Service Meshes, Zero Trust, and Governed Execution](docs/architecture/api-gateways-service-meshes-zero-trust-and-governed-execution.md). ([#175](https://github.com/AsiBackbone/Learning/issues/175))
* [x] Workflow engines — [Workflow Engines, Human Approval Systems, and Governed Execution](docs/architecture/workflow-engines-human-approval-and-governed-execution.md). ([#176](https://github.com/AsiBackbone/Learning/issues/176))
* [x] Policy engines — [Policy Engines, Rules Engines, and Distributed Policy Enforcement](docs/architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md). ([#7](https://github.com/AsiBackbone/Learning/issues/7), [#177](https://github.com/AsiBackbone/Learning/issues/177))
* [x] Agent/tool authorization models — [Agent and Tool Authorization Models and Host-Owned Execution](docs/architecture/agent-and-tool-authorization-models-and-host-owned-execution.md). ([#178](https://github.com/AsiBackbone/Learning/issues/178))
* [x] Human approval systems — [Workflow Engines, Human Approval Systems, and Governed Execution](docs/architecture/workflow-engines-human-approval-and-governed-execution.md). ([#176](https://github.com/AsiBackbone/Learning/issues/176))
* [x] Event-sourced audit approaches — [Event Sourcing, Audit Trails, and Governance Decision Provenance](docs/architecture/event-sourcing-audit-trails-and-governance-decision-provenance.md). ([#179](https://github.com/AsiBackbone/Learning/issues/179))
* [x] Command/query separation — [CQRS, Command/Query Separation, and Governed Execution](docs/architecture/cqrs-command-query-separation-and-governed-execution.md). ([#180](https://github.com/AsiBackbone/Learning/issues/180))
* [x] Zero-trust architecture — [API Gateways, Service Meshes, Zero Trust, and Governed Execution](docs/architecture/api-gateways-service-meshes-zero-trust-and-governed-execution.md). ([#175](https://github.com/AsiBackbone/Learning/issues/175))
* [x] Rules engines — [Policy Engines, Rules Engines, and Distributed Policy Enforcement](docs/architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md). ([#7](https://github.com/AsiBackbone/Learning/issues/7), [#177](https://github.com/AsiBackbone/Learning/issues/177))
* [x] Distributed policy enforcement — [Policy Engines, Rules Engines, and Distributed Policy Enforcement](docs/architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md). ([#7](https://github.com/AsiBackbone/Learning/issues/7), [#177](https://github.com/AsiBackbone/Learning/issues/177))

Comparisons should not frame adjacent architectures as competitors simply because they solve related problems.

The objective is to clarify:

```text
What problem does this pattern solve?

What does it not solve?

Where do responsibilities overlap?

Where are the trust boundaries different?

What are the operational tradeoffs?
```

---

# Milestone 11 — Reference Architecture Case Studies

## Status

**Complete — all eight planned reference architecture case studies published** ([#189](https://github.com/AsiBackbone/Learning/issues/189), [#190](https://github.com/AsiBackbone/Learning/issues/190), [#191](https://github.com/AsiBackbone/Learning/issues/191), [#192](https://github.com/AsiBackbone/Learning/issues/192), [#193](https://github.com/AsiBackbone/Learning/issues/193), [#194](https://github.com/AsiBackbone/Learning/issues/194), [#195](https://github.com/AsiBackbone/Learning/issues/195), [#196](https://github.com/AsiBackbone/Learning/issues/196))

## Goal

Show how multiple patterns interact in realistic scenarios without turning Learning into another production framework.

Potential case studies include:

* [x] [Governed administrative operation](docs/case-studies/governed-administrative-operation.md). ([#189](https://github.com/AsiBackbone/Learning/issues/189))
* [x] [Sensitive-data access decision](docs/case-studies/sensitive-data-access-decision.md). ([#190](https://github.com/AsiBackbone/Learning/issues/190))
* [x] [Deployment approval and infrastructure change gates](docs/case-studies/deployment-approval-and-infrastructure-change-gates.md). ([#191](https://github.com/AsiBackbone/Learning/issues/191))
* [x] [AI-assisted API operation and governed AI tool gateway](docs/case-studies/ai-assisted-api-and-governed-tool-gateway.md). ([#192](https://github.com/AsiBackbone/Learning/issues/192))
* [x] [Multi-tenant and regional policy overlay](docs/case-studies/multi-tenant-and-regional-policy-overlay.md). ([#193](https://github.com/AsiBackbone/Learning/issues/193))
* [x] [Human acknowledgment workflow](docs/case-studies/human-acknowledgment-workflow.md). ([#194](https://github.com/AsiBackbone/Learning/issues/194))
* [x] [Capability-scoped background operation](docs/case-studies/capability-scoped-background-operation.md). ([#195](https://github.com/AsiBackbone/Learning/issues/195))
* [x] [Simulated robotics-command governance boundary](docs/case-studies/simulated-robotics-command-governance-boundary.md). ([#196](https://github.com/AsiBackbone/Learning/issues/196))

Each case study should separate:

```text
Architecture
Implementation
Operational responsibility
Security responsibility
Governance responsibility
Execution responsibility
```

Case studies should prefer simulated or dry-run consequential operations unless real integration materially contributes to the lesson.

---

# Milestone 12 — Community Learning Loop

## Status

**Established intake and contribution foundation — participation depth remains ongoing**

## Existing Community Artifacts

* [x] `community/tutorial-ideas.md`
* [x] `community/requested-topics.md`
* [x] `community/contributors.md`
* [x] Contribution guidance.
* [x] Governance guidance.
* [x] Quick-start contribution path in `CONTRIBUTING.md`. ([#8](https://github.com/AsiBackbone/Learning/issues/8))

## Reconciled Community Work

The earlier future-work list mixed missing intake surfaces with participation goals.
The current repository already has dedicated Discussion templates for Q&A,
Architecture & Patterns, Tutorial Ideas, and Show and tell, plus bug,
learning-content, and improvement Issue forms. The remaining work is therefore
classified by what is actually missing rather than by creating more overlapping
templates.

* **Architecture-question template — Obsolete / superseded.** Q&A and Architecture
  & Patterns Discussions now provide the intended intake paths.
* **Tutorial-proposal template — Complete.** The Tutorial Ideas Discussion template
  provides a dedicated proposal surface.
* **Sample-proposal guidance — Complete.** `CONTRIBUTING.md` covers executable-sample
  contributions, and Tutorial Ideas accepts example proposals.
* **Lab-proposal template — Obsolete / superseded.** Tutorial Ideas and the
  learning-content Issue form cover intake, while
  `docs/templates/lab-acceptance-criteria-template.md` covers lab authoring.
* **Alternative-pattern proposal guidance — Complete.** Architecture & Patterns
  Discussions and the Alternatives Are Welcome guidance in `CONTRIBUTING.md`
  cover this path.
* **`good first tutorial` issues — Still needed when concrete starter work exists.**
  Do not maintain a quota merely to satisfy a roadmap category.
* **`good first sample` issues — Still needed when concrete starter work exists.**
* **Documentation-only starter issues — Still needed when concrete starter work
  exists.**
* **Diagram contribution opportunities — Still needed when concrete starter work
  exists.**
* **Lab review contributors — Ongoing maintenance.** Grow reviewer participation as
  contribution volume justifies it.
* **Topic-specific reviewers — Ongoing maintenance.** Recruit by subject and need
  rather than by roadmap checkbox.
* **Contributor recognition — Ongoing maintenance.** `community/contributors.md`
  provides the foundation; recognition should evolve with real participation.
* **Community-authored alternative patterns — Still needed.** Existing
  alternative-pattern material satisfies the learning need, but not the distinct
  community-authorship goal.
* **Additional maintainers if sustained contribution creates a practical need —
  Ongoing maintenance / demand-driven.**
* **Translation support if demand emerges — Ongoing maintenance / demand-driven.**

The active community priority is to keep a small set of genuine starter
opportunities visible when suitable work exists and to deepen reviewer and
community-authored participation without manufacturing contribution work.

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
Sample
   ↓
Lab
   ↓
Working implementation reference
   ↓
Feedback
   ↺
```

Useful community questions should become candidates for future documentation.

Repeated misunderstandings should trigger refinement of existing material before automatically creating new material.

---

# Milestone 13 — Advanced and Experimental Material ([#5](https://github.com/AsiBackbone/Learning/issues/5))

## Status

**Reconciled — several proposed subjects now have substantial coverage; remaining work is concentrated in distributed/federated governance, cross-system authority, and explanation depth**

## Goal

Provide a clearly labeled area for ideas that are worth exploring but should not yet be presented as established guidance.

The original subject list is reconciled against dedicated curriculum, labs,
samples, case studies, and architecture comparisons as follows:

* **Distributed governance — Partially covered.** Distributed policy enforcement is
  documented, but broader coordination across independent governance authorities
  remains open.
* **Multi-region policy coordination — Partially covered.** Regional and tenant
  overlays are established; cross-region coordination and authority conflicts still
  deserve focused treatment.
* **Durable decision ledgers — Partially covered.** Decision provenance and
  event-sourcing material cover durable evidence concepts, but a dedicated
  decision-ledger lifecycle remains unfinished.
* **Cryptographic audit chains — Partially covered.** Signing, verification, key
  custody, and tamper-evidence are covered; chained append-only audit structures are
  not yet a dedicated learning subject.
* **External policy providers — Complete at the architecture-learning level.** The
  policy-engine comparison covers externalized PDPs, local/sidecar/remote placement,
  policy-as-code, and representative OPA/Rego, Cedar, and XACML anchors.
  Provider-specific integration samples remain optional.
* **Policy simulation — Complete.** A dedicated change-impact lab and executable
  policy-simulation harness with tests now exist.
* **Governance telemetry — Complete for the current learning intent.** Dedicated
  decision-tracing/observability material and executable governed-gateway
  instrumentation now cover the subject; broader telemetry refinements are ongoing.
* **Decision explainability — Partially covered.** Reason codes, provenance, and
  tracing exist, but explanation for human operators remains distinct from audit
  evidence and deserves focused treatment.
* **Adaptive risk context — Partially covered.** Risk-based decisions and
  deterministic/probabilistic inputs are established; adaptive feedback, drift, and
  changing risk context remain open.
* **Robotics gateway patterns — Complete for the Learning scope.** The simulated
  robotics command-governance case study teaches the gateway and host-owned
  execution boundary without implying a production robotics controller.
* **Agent-to-agent governed execution — Complete.**
  ([#75](https://github.com/AsiBackbone/Learning/issues/75))
* **Cross-system capability exchange — Still needed.** Delegation, audience binding,
  revocation, replay, and authority transfer across independently operated systems
  remain worthwhile advanced work.
* **Federated governance models — Partially covered.** Distributed enforcement and
  regional overlays provide foundations, but federation across independently
  governed authorities remains unfinished.
* **Distributed acknowledgment workflows — Still needed.** Existing acknowledgment
  material does not yet cover continuation state and responsibility across
  distributed boundaries.
* **Governance under partial system failure — Complete.**
  ([#66](https://github.com/AsiBackbone/Learning/issues/66))

Experimental material should clearly state:

* Assumptions
* Unknowns
* Failure modes
* Security concerns
* Operational constraints
* What has and has not been implemented
* What evidence would strengthen or weaken the proposed approach

---

# Documentation Quality Goals

As the repository grows, learning material should increasingly provide:

* [x] Clear learning objectives in foundational tutorials.
* [x] Architectural diagrams or flows where useful.
* [x] Tradeoff discussion in foundational material.
* [x] Scope and boundary language.
* [x] Consistent tutorial metadata.
* [x] Difficulty indicators.
* [x] Prerequisites.
* [ ] Estimated scope rather than artificial completion times.
* [x] Runnable companion samples.
* [x] Executable tests. ([#2](https://github.com/AsiBackbone/Learning/issues/2))
* [x] Consistent "When not to use this" guidance where it materially improves the lesson. ([#140](https://github.com/AsiBackbone/Learning/issues/140))
* [ ] **Ongoing maintenance — implementation links:** Continue strengthening links to
  working implementation repositories where they materially deepen the lesson.
* [x] Links to relevant ADRs when they directly explain a working implementation decision. ([#140](https://github.com/AsiBackbone/Learning/issues/140))
* [x] Related-tutorial links.
* [x] Suggested labs.
* [x] Clear canonical, alternative, or experimental status where relevant. ([#139](https://github.com/AsiBackbone/Learning/issues/139))
* [ ] **Ongoing maintenance — accessibility:** Re-review diagrams and visual material
  when substantial new visuals, media, interactive content, or theme changes are
  introduced.
* [ ] **Ongoing maintenance — technical currency:** Periodically review technical
  guidance and implementation-repository cross-links as .NET and the implementation
  repositories evolve.

Issue #140 completed a representative cross-cutting pass rather than adding formulaic sections to every page. Usage boundaries remain explicit where over-application is a meaningful risk, and ADR links are added only when a working repository contains a directly relevant decision record.

---

# Repository Automation Goals

Automation should support quality without making contribution unnecessarily difficult.

## Established

* [x] DocFX build validation.
* [x] Treat documentation warnings as errors.
* [x] GitHub Pages deployment.
* [x] SHA-pinned GitHub Actions in documentation workflows.
* [x] Automated link checking.
* [x] Sample restore validation. ([#1](https://github.com/AsiBackbone/Learning/issues/1))
* [x] Sample build validation. ([#1](https://github.com/AsiBackbone/Learning/issues/1))
* [x] Sample test execution. ([#1](https://github.com/AsiBackbone/Learning/issues/1), [#2](https://github.com/AsiBackbone/Learning/issues/2))
* [x] Grouped Dependabot version-update automation for NuGet and GitHub Actions dependencies. ([#137](https://github.com/AsiBackbone/Learning/issues/137))
* [x] Repository-maintained CodeQL scanning for the C# executable surface on relevant pull requests, pushes to `main`, and a weekly schedule. ([#137](https://github.com/AsiBackbone/Learning/issues/137))

## Reconciled Follow-up

* **Obsolete / superseded — standalone Markdown validation as a roadmap objective.**
  DocFX warnings-as-errors, link validation, and metadata validation form the
  current baseline. Add a separate Markdown linter only if it catches a
  demonstrated gap.
* **Ongoing maintenance — repository-setting controls.** Periodically review settings
  such as secret scanning and push protection as repository capabilities and risk
  change.
* **Ongoing maintenance — pull request validation.** Extend the established targeted
  checks only when a new failure mode justifies another required signal.
* **Complete — documentation artifact validation.** Documentation validation now
  covers the DocFX/template baseline, generated sitemap, RSS feed, IndexNow
  publication contract, and generated metadata; the publish workflow produces and
  uploads the GitHub Pages artifact.
* **Still needed where practical — license-boundary validation.** Add automation only
  when it can verify the documentation/sample licensing split without creating
  noisy or burdensome checks.

Source-controlled automation and repository-host controls should be reviewed separately. A committed workflow does not prove that branch rules, secret scanning, push protection, Dependabot alerts, or Dependabot security updates are enabled in GitHub settings.

Automation should be introduced when it provides meaningful protection, prevents documentation drift, or reduces maintenance burden.

---

# Citation and Archival Goals

The Learning repository uses a versioned educational-release model so that meaningful milestones can be archived and cited without treating the project as a versioned runtime package.

## Established

* [x] Add `CITATION.cff`.
* [x] Add Zenodo metadata.
* [x] Establish component-specific licensing metadata.
* [x] Publish meaningful educational milestones as versioned GitHub and Zenodo snapshots. ([#136](https://github.com/AsiBackbone/Learning/issues/136))
* [x] Synchronize release identity across `CITATION.cff` and `.zenodo.json`. ([#136](https://github.com/AsiBackbone/Learning/issues/136))
* [x] Use a Zenodo concept DOI for the evolving Learning work and version-specific DOIs for exact archived releases. ([#136](https://github.com/AsiBackbone/Learning/issues/136))
* [x] Document how readers should cite the evolving project versus a particular archived release. ([#136](https://github.com/AsiBackbone/Learning/issues/136))
* [x] Keep archival cadence milestone-based rather than treating every documentation change as a release event. ([#136](https://github.com/AsiBackbone/Learning/issues/136))

## Ongoing Maintenance

* [ ] Update the `CITATION.cff` release version/date and `.zenodo.json` version together for each future archived release.
* [ ] Keep Zenodo project identity, licensing, and related identifiers current.
* [ ] Preserve new versioned snapshots when a meaningful educational milestone warrants an archival release.
* [ ] Re-check citation guidance, archival links, and the related-identifier graph when the publication workflow or related works change.

Archival infrastructure should support the learning resource.

It should not force the project into unnecessary release ceremony.

Versioned Learning releases are citable educational snapshots. They do not establish runtime compatibility lines, historical package support, or long-term security maintenance for archived material.

---

# Near-Term Priorities

The highest-priority work now is:

1. Complete the decision-pipeline refactoring lab tracked by
   [#221](https://github.com/AsiBackbone/Learning/issues/221), then select the next
   lab only where a dedicated learner exercise adds value beyond existing material.
2. Develop cross-system capability exchange, including delegation, audience
   binding, revocation, replay resistance, and authority transfer across
   independently operated execution boundaries.
3. Deepen federated and distributed governance beyond policy distribution:
   multi-region authority coordination, independent governance domains, and
   distributed acknowledgment/continuation workflows.
4. Add focused decision-explainability material that distinguishes human/operator
   explanation from reason codes, provenance, telemetry, and audit evidence.
5. Keep concrete good-first contribution opportunities visible when genuine starter
   work exists, and grow lab/topic reviewer participation as community activity
   justifies it.

These are strategic curriculum and community priorities. Editorial publication
planning remains in `community/article-backlog.md`; article candidates and their
dedicated implementation issues should not be duplicated here as roadmap work.

The short-term emphasis should remain:

> **Depth before breadth.**

A strong tutorial with a runnable sample, meaningful tests, a useful lab, and clear implementation references is more valuable than several disconnected pages of new material.

---

# Measuring Progress

Progress should not be measured only by repository size, package adoption, stars, or raw page count.

Useful signals include:

* Foundational tutorials remaining technically current.
* Companion samples that compile.
* Tests that preserve architectural invariants.
* Labs that can be completed without hidden assumptions.
* Documentation build health.
* Working links between Learning and implementation repositories.
* Corrections prompted by readers.
* Questions converted into improved explanations.
* Architectural discussions converted into tutorials or labs.
* Alternative patterns contributed and reviewed.
* Patterns reused outside ASI Backbone repositories.
* Community Issues and Discussions.
* External pull requests.
* Contributors who begin with documentation or samples and later participate elsewhere in the organization.

The Learning repository succeeds when it helps people reason more clearly about software architecture.

---

# What Is Not Currently a Roadmap Goal

The Learning repository is not intended to become:

* A replacement for `AsiBackbone`.
* A replacement for `NetCoreApplicationTemplate`.
* A general-purpose .NET framework.
* A competing governance runtime.
* An AI model host.
* An autonomous-agent platform.
* An AGI or ASI implementation.
* A compliance product.
* A certification program.
* A security guarantee.
* A production robotics controller.
* A repository that attempts to prove the broader theoretical ASI Backbone or Eden Hypothesis framework.

Its purpose is narrower:

> **Teach architectural reasoning through patterns, examples, samples, labs, working references, experiments, and community discussion.**

---

# Long-Term Direction

Over time, Learning may become the primary educational entry point into the ASI Backbone organization.

A mature learning ecosystem could look like:

```text
                    ASI Backbone Organization
                             |
              +--------------+--------------+
              |                             |
              v                             v
        Learning Repository             Discussions
      education + examples          questions + debate
              |
              v
          Tutorials
              |
              v
           Samples
              |
              v
             Labs
              |
              v
     Architectural Patterns
              |
       +------+------+
       |             |
       v             v
 AsiBackbone   NetCoreApplicationTemplate
 governance       application
 implementation   architecture
       |             |
       +------+------+
              |
              v
      Practical Experience
              |
              v
      Community Feedback
              |
              +-----------> Learning
```

The intended cycle is:

> **Theory becomes pattern. Pattern becomes tutorial. Tutorial becomes sample. Sample becomes exercise. Exercise meets implementation. Implementation produces experience. Experience improves the lesson.**

---

## Roadmap Status

This roadmap is intentionally living.

Items may be added, reordered, split, merged, deferred, completed, or removed as the project develops.

Completion of a roadmap item does not mean the subject is permanently finished. Tutorials, samples, labs, and guidance may require revision as .NET, security practices, AI integration patterns, and the implementation repositories evolve.

Changes should continue to serve the central principle:

> **Read it. Run it. Question it. Improve it.**
