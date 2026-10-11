# Requested Topics

`AsiBackbone/Learning` is intended to evolve in response to real questions from developers, architects, reviewers, students, security practitioners, and contributors.

This page tracks learning topics that the community would like to see explained, demonstrated, compared, or turned into hands-on labs.

If a topic repeatedly appears in Issues or Discussions, that is a strong signal that it may deserve dedicated Learning material.

`ROADMAP.md` remains the strategic source of truth for project direction and implementation status. This page is a lighter-weight community intake and history surface, so statuses are synchronized periodically at meaningful curriculum milestones rather than after every repository change.

The presence of a topic here does not, by itself, mean that it is highly community-requested. Issues, Discussions, concrete use cases, and contributor interest are stronger signals of demand.

This tracker is reconciled after meaningful curriculum milestones and before a Learning release that materially changes topic coverage. During that review, compare it with `ROADMAP.md` and the documentation, sample, lab, case-study, and diagram indexes. Mark a topic **Published** only when the requested learner outcome is available, link the most direct material, and describe any narrower unsatisfied follow-up explicitly instead of leaving the original request ambiguously open.

Standalone article candidates are curated separately in the [Problem-Oriented Standalone Article Backlog](article-backlog.md). That backlog starts from externally recognizable reader/search problems and synthesizes existing Learning material; it does not replace this curriculum-topic intake surface.

## How to Request a Topic

For a new topic request, prefer opening an [AsiBackbone Organization Discussion](https://github.com/orgs/AsiBackbone/discussions) when the subject is exploratory, architectural, or likely to benefit from community input.

Use an Issue when the requested work is already concrete and well scoped.

A useful request includes:

- The problem you are trying to understand.
- Why the topic matters in practice.
- What level of detail would be most useful.
- Whether you would prefer a tutorial, lab, diagram, comparison, or worked example.
- Any existing AsiBackbone or NetCoreApplicationTemplate implementation that appears relevant.
- Any specific tradeoff or failure mode you want examined.

You do not need to know the solution before requesting a topic.

A good learning request may simply begin with:

> "I understand the code works, but I do not understand why the architecture is structured this way."

## Topic Status

Requested topics may use the following informal status labels:

- **Requested** — identified as useful but not yet planned.
- **Discussing** — active community discussion is shaping the topic.
- **Planned** — accepted for future Learning work.
- **In Progress** — tutorial, lab, diagram, or example is being developed.
- **Published** — learning material is available.
- **Deferred** — useful, but not currently prioritized.
- **Needs Example** — concept is understood but a good teaching example is still needed.
- **Needs Contributor** — suitable topic, but no one is currently working on it.
- **Experimental** — topic is useful to explore but should not yet be presented as established guidance.

These labels are descriptive rather than a formal release commitment.

A topic may be **Published** here while a narrower follow-up, alternative treatment, or deeper lab remains a valid future idea elsewhere in the repository.

---

## Foundational Requests — Published

These topics shaped the initial Learning roadmap and now have published coverage in the foundational tutorial/sample/test/lab path. Their original questions and suggested formats are retained below as historical planning context.

The published foundation consists of [Decision Before Execution](../docs/tutorials/decision-before-execution.md), [Policy Context and Explicit Decision Outcomes](../docs/tutorials/policy-context-and-explicit-decision-outcomes.md), [Decision Receipts and Acknowledgment](../docs/tutorials/decision-receipts-and-acknowledgment.md), [Scoped Capability and Host-Owned Execution](../docs/tutorials/scoped-capability-and-host-owned-execution.md), and [Governed AI Tool Gateway](../docs/tutorials/governed-ai-tool-gateway.md), with corresponding runnable samples under [`samples/`](../samples/README.md) and learner labs under [`docs/labs/`](../docs/labs/index.md).

### Decision Before Execution

**Status:** Published

Explain why a consequential operation should be represented as proposed intent before the host performs the operation.

Questions to address:

- Why is direct request-to-execution coupling risky?
- How does a decision boundary differ from ordinary authorization?
- What information should exist before execution?
- What should remain after execution?
- When is this pattern unnecessary?

Suggested format:

- Tutorial
- Sequence diagram
- Minimal C# example
- Beginner lab

---

### Policy Context

**Status:** Published

Explain how to gather the facts required for a governance decision into an explicit context model.

Questions to address:

- What belongs in policy context?
- What should remain outside the policy context?
- How should actor, resource, operation, tenant, region, and risk information be represented?
- How can context remain testable?
- How do we avoid turning context into an unbounded object bag?

Suggested format:

- Tutorial
- Context-model example
- Unit tests
- Comparison with scattered policy inputs

---

### Explicit Decision Outcomes

**Status:** Published

Explain why a governance result may need more expressive outcomes than a boolean allow/deny response.

Candidate outcomes:

```text
Allow
Deny
Defer
RequireAcknowledgment
Escalate
```

Questions to address:

- When is `bool` insufficient?
- What should a decision result contain besides the outcome?
- How should reason codes differ from display messages?
- How should hosts respond to `Defer` or `Escalate`?

Suggested format:

- Tutorial
- Minimal result model
- Decision matrix
- Lab

---

### Acknowledgment Workflows

**Status:** Published

Explain how a workflow can pause for explicit acknowledgment before a consequential operation proceeds.

Questions to address:

- What is acknowledgment?
- How is it different from authentication or approval?
- What information should the user acknowledge?
- What should be recorded?
- What happens when the underlying decision changes before execution?

Suggested format:

- Tutorial
- Sequence diagram
- ASP.NET Core example
- Intermediate lab

---

### Decision Receipt and Provenance

**Status:** Published

Explain the difference between normal application logs and durable governance evidence.

Questions to address:

- What should survive a decision?
- What is a useful decision receipt?
- How should reason codes, policy versions, hashes, correlation IDs, and timestamps be used?
- What should not be placed in an audit record?
- How should privacy and sensitive data affect audit design?

Suggested format:

- Tutorial
- Record model
- Logging versus audit comparison
- Failure-mode examples

---

### Scoped Capability

**Status:** Published

Explain how a decision may produce narrow, temporary authority for a specific follow-on operation.

Questions to address:

- Why not rely solely on broad standing authorization?
- What should a capability be bound to?
- How long should it remain valid?
- What validation belongs at the execution boundary?
- What replay risks exist?

Suggested format:

- Tutorial
- Threat diagram
- Minimal token/grant example
- Intermediate lab

---

### Host-Owned Execution

**Status:** Published

Explain why the governance layer should not automatically become the component that performs the real-world action.

Questions to address:

- What does host-owned execution mean?
- Why is governance separate from execution?
- Which responsibilities remain with the host?
- How should failure after approval be handled?
- What should be audited when execution never occurs?

Suggested format:

- Tutorial
- Boundary diagram
- Comparison with tightly coupled designs

---

### Governed AI Tool Gateway

**Status:** Published

Build an end-to-end example in which an AI system proposes a tool action but the host retains authority to evaluate and execute it.

Core principle:

> **The model may propose. The host retains execution authority.**

Questions to address:

- How should proposed tool calls be represented?
- How should tool arguments be validated?
- Where should policy evaluation occur?
- When should acknowledgment be required?
- How can capability-scoped execution be applied?
- What evidence should remain after the tool call?

Suggested format:

- End-to-end tutorial
- ASP.NET Core sample
- Mermaid sequence diagram
- Tests
- Threat-model notes
- Lab

---

## ASP.NET Core Architecture Requests

### Middleware Ordering

**Status:** Published

Middleware ordering has moved beyond the original request stage. The repository now includes an architecture article, runnable sample, focused tests, and a learner lab showing how ordering changes application behavior and trust boundaries.

Published material:

- [Middleware Ordering Changes Behavior](../docs/aspnetcore/middleware-ordering-changes-behavior.md)
- [Runnable sample and focused tests](../samples/middleware-ordering-changes-behavior/README.md)
- [Identify Middleware Ordering Problems lab](../docs/labs/identify-middleware-ordering-problems.md)

Explore how middleware order changes application behavior and trust boundaries.

Possible areas:

- Exception handling
- Forwarded headers
- HTTPS
- Static files
- Routing
- Authentication
- Authorization
- Rate limiting
- Request logging
- Security headers

---

### Secure-by-Default Configuration

**Status:** Published

Published material: [Secure-by-Default ASP.NET Core Configuration](../docs/aspnetcore/secure-by-default-configuration.md)

Explain how application defaults can reduce accidental exposure.

Possible areas:

- Explicit opt-in
- Environment validation
- Feature toggles
- Configuration validation
- Secrets
- Production versus development behavior

---

### Structured Logging

**Status:** Published

Published material:

- [Structured Logging Without Sensitive-Data Sprawl](../docs/aspnetcore/structured-logging-without-sensitive-data-sprawl.md)
- [Secure Logging Across Trust Boundaries](../docs/security/secure-logging-across-trust-boundaries.md)

Explain how to design useful structured events rather than treating logs as formatted strings.

Possible areas:

- Event IDs
- Correlation
- Context enrichment
- Sensitive data
- Operational diagnostics
- Log volume
- Audit versus logging

---

### Centralized Error Handling

**Status:** Published

Published material: [Centralized Error Handling and Problem Details](../docs/aspnetcore/centralized-error-handling-and-problem-details.md)

Explore centralized exception handling and consistent client-facing error behavior.

Possible areas:

- Problem Details
- Information disclosure
- Exception mapping
- Status codes
- Correlation identifiers
- Logging boundaries

---

### EF Core Cross-Cutting Behavior

**Status:** Published

Published material: [Data-Access Boundaries and Transaction Reasoning with EF Core](../docs/aspnetcore/data-access-boundaries-and-transaction-reasoning.md)

Explain when interceptors, repositories, unit-of-work patterns, or direct DbContext usage make sense.

Possible areas:

- SaveChanges interceptors
- Auditing
- Transactions
- Persistence boundaries
- Testing
- Tradeoffs between abstraction and transparency

---

### Architecture Decision Records

**Status:** Published

Published material:

- [Architecture Decision Records Preserve Architectural Reasoning](../docs/aspnetcore/architecture-decision-records-preserve-architectural-reasoning.md)
- [ADR Lifecycle: Review, Deprecation, and Supersession](../docs/aspnetcore/architecture-decision-record-lifecycle-review-deprecation-and-supersession.md)
- [NetCoreApplicationTemplate ADR Case Study](../docs/aspnetcore/netcoreapplicationtemplate-adr-case-study.md)
- [Write and Revisit an Architecture Decision Record lab](../docs/labs/write-and-revisit-an-architecture-decision-record.md)

Teach how to write and maintain ADRs using real decisions from the organization repositories as references.

Questions to address:

- What deserves an ADR?
- What belongs in an ADR?
- How should superseded decisions be handled?
- How do ADRs differ from code comments?
- How can ADRs feed Learning tutorials?

---

## Security and Trust Architecture Requests

### Authentication vs Authorization vs Governance

**Status:** Published

Published material:

- [Terminology and Established Concepts](../docs/architecture/terminology-and-established-concepts.md)
- [When ASP.NET Core Authorization Is Enough](../docs/architecture/when-aspnet-core-authorization-is-enough.md)

Clarify the boundaries among:

- Identity
- Authentication
- Authorization
- Policy evaluation
- Governance
- Execution authority

Suggested format:

- Comparison tutorial
- Decision-flow diagram

---

### Capability-Based Security

**Status:** Published

Published material:

- [Role-Based, Claims-Based, and Capability-Based Authorization](../docs/architecture/role-based-claims-based-and-capability-based-authorization.md)
- [Scoped Capability and Host-Owned Execution](../docs/tutorials/scoped-capability-and-host-owned-execution.md)

Compare capability-scoped authority with traditional role- and claims-based authorization.

Suggested areas:

- Least authority
- Resource binding
- Operation binding
- Expiration
- Delegation
- Replay

---

### Replay Protection

**Status:** Published

Published material: [Replay Protection and Bounded-Use Authority](../docs/security/replay-protection-and-bounded-use.md)

Explain replay as a system-level concern rather than only a token-validation detail.

Possible examples:

- One-time operations
- Durable nonce storage
- Bounded-use grants
- Distributed execution nodes
- Failure recovery

---

### Signing and Verification

**Status:** Published

Published material: [Signing, Verification, Key Custody, and Tamper Evidence](../docs/security/signing-verification-key-custody-and-tamper-evidence.md)

Introduce signing concepts without implying that signing alone creates trust.

Questions to address:

- What does a signature prove?
- What does it not prove?
- Who owns key custody?
- How should key rotation be handled?
- How do signing and provenance differ?

---

### Tamper-Evident Audit Records

**Status:** Published

Published material:

- [Signing, Verification, Key Custody, and Tamper Evidence](../docs/security/signing-verification-key-custody-and-tamper-evidence.md)
- [Durable Decision Ledgers and Cryptographic Audit Chains](../docs/advanced/durable-decision-ledgers-and-cryptographic-audit-chains.md)

Explore the difference between ordinary durable storage and tamper-evident evidence.

Potential subjects:

- Hash chaining
- Signing
- Append-only storage
- External anchoring
- Verification
- Operational complexity

This topic should remain explicit about the difference between a conceptual pattern and a production-grade implementation.

---

### Software Supply-Chain Integrity

**Status:** Published

Published material: [Software Supply-Chain Integrity for .NET Repositories](../docs/security/software-supply-chain-integrity-for-dotnet-repositories.md)

Use the organization repositories as practical examples for:

- SHA-pinned GitHub Actions
- Dependency update automation
- SBOM generation
- Build provenance
- Locked restores
- Package metadata validation
- Source Link
- Release validation

Suggested format:

- Tutorial
- Repository walkthrough
- Security checklist

---

### Threat Modeling as Architecture Reasoning

**Status:** Published

Published material: [Threat Modeling as Architecture Reasoning](../docs/security/threat-modeling-as-architecture-reasoning.md)

Teach threat modeling as a way to reason about architecture under adversarial and failure conditions rather than as a compliance worksheet or substitute for testing.

Questions to address:

- What assets and security objectives matter inside the selected scope?
- Where do data, trust, and authority cross boundaries?
- Which abuse paths can replay, forge, modify, disclose, exhaust, or bypass the intended flow?
- Which controls mitigate concrete threats, and which assumptions remain unenforced?
- What architectural invariants can verify that blocked paths never reach consequential execution?
- What residual risk remains, and what architecture changes should trigger re-evaluation?

Suggested format:

- Tutorial
- Worked architecture flow
- Threat-to-mitigation and invariant table

---

## AI and Agent Governance Requests

### AI Proposed Intent

**Status:** Published

Published material: [Typed AI Proposed Intent and Schema-Validation Boundaries](../docs/ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md)

Explain how to translate model output into structured proposed intent before policy evaluation.

Possible areas:

- Typed tool requests
- Schema validation
- Argument normalization
- Untrusted model output
- Intent metadata

---

### Human-in-the-Loop Governance

**Status:** Published

Published material: [Human-in-the-Loop Governance Workflows](../docs/governance/human-in-the-loop-governance-workflows.md)

Explore when human review is useful and when it becomes security theater.

Questions to address:

- What should a human actually review?
- How should context be presented?
- How should acknowledgment be recorded?
- What happens when humans routinely approve everything?

---

### Tool Allowlists and Argument Constraints

**Status:** Published

Published material:

- [Governed AI Tool Gateway](../docs/tutorials/governed-ai-tool-gateway.md)
- [What Should an AI Tool Gateway Validate Before Execution?](../docs/articles/2026/validate-ai-tool-call-before-execution.md)

Show how tool-level authorization can remain narrow even when a model has access to many capabilities.

Suggested format:

- Tutorial
- Example policy
- Negative tests

---

### Agent-to-Agent Requests

**Status:** Published

Published material: [Governed Agent-to-Agent Requests and Multi-Agent Execution Boundaries](../docs/advanced/governed-agent-to-agent-requests-and-multi-agent-execution-boundaries.md)

The material remains explicitly experimental: it exposes assumptions, trust boundaries, and unresolved operational questions rather than presenting agent-to-agent delegation as settled production guidance.

Explore governance when one automated system proposes an operation to another automated system.

Potential questions:

- How is originating intent preserved?
- What context crosses boundaries?
- How is authority delegated?
- Who owns final execution?
- How are decision chains audited?

---

### AI Decision Explainability

**Status:** Published

Published material: [Decision Explainability for Human Operators](../docs/advanced/decision-explainability-for-human-operators.md)

Distinguish:

- Model explanation
- Policy reason
- Governance decision reason
- Host execution result

The tutorial should avoid treating generated explanations as authoritative evidence of model internals.

---

## Policy Architecture Requests

### Policy Composition

**Status:** Published

Published material: [Constraint Composition and Policy Precedence](../docs/governance/constraint-composition-and-policy-precedence.md)

Explore how multiple constraints combine into one decision.

Possible areas:

- Precedence
- Short-circuiting
- Aggregation
- Conflicting rules
- Required acknowledgments
- Escalation

---

### Policy Versioning

**Status:** Published

Published material: [Policy Versioning and Decision Provenance](../docs/governance/policy-versioning-and-decision-provenance.md)

Explain why a decision should often record which policy version produced it.

Possible areas:

- Reproducibility
- Audit
- Rollback
- Historical interpretation
- Policy hashes

---

### Regional and Tenant Policy Overlays

**Status:** Published

Published material:

- [Regional and Tenant Policy Overlays](../docs/advanced/regional-and-tenant-policy-overlays.md)
- [Multi-Tenant and Regional Policy Overlay case study](../docs/case-studies/multi-tenant-and-regional-policy-overlay.md)

Explore how global policy can coexist with regional, tenant, or organizational constraints.

Potential flow:

```text
Global Policy
   ↓
Regional Policy
   ↓
Tenant Policy
   ↓
Operation-Specific Constraints
   ↓
Decision
```

---

### Degraded-Mode Governance

**Status:** Published

Published material:

- [Should Authorization Fail Open, Fail Closed, or Defer?](../docs/articles/2026/fail-open-fail-closed-or-defer.md)
- [Safe Degraded Mode and Fail-Safe Governance lab](../docs/labs/safe-degraded-mode-and-fail-safe-governance.md)

Explore what should happen when a policy provider, storage dependency, risk service, or external governance component is unavailable.

Questions to address:

- Fail open or fail closed?
- Which operations may defer?
- Which may continue?
- How should degraded decisions be recorded?

---

### Policy Testing

**Status:** Published

Published material:

- [Practical Policy Testing and Decision-Table Strategies](../docs/governance/practical-policy-testing-and-decision-table-strategies.md)
- [Policy Simulation and Change-Impact Analysis lab](../docs/labs/policy-simulation-and-change-impact-analysis.md)

Show practical strategies for testing governance rules.

Potential areas:

- Unit tests
- Decision tables
- Boundary cases
- Property-based testing
- Regression cases
- Policy snapshots

---

## Architecture Comparison Requests

### Boolean Authorization vs Explicit Decision Models

**Status:** Published

Published material: [When ASP.NET Core Authorization Is Enough](../docs/architecture/when-aspnet-core-authorization-is-enough.md)

Compare simple authorization checks with structured governance decision results.

---

### RBAC vs Claims vs Policy vs Capability

**Status:** Published

Published material: [Role-Based, Claims-Based, and Capability-Based Authorization](../docs/architecture/role-based-claims-based-and-capability-based-authorization.md)

Explain where each model is useful and where the concepts overlap.

The goal should be comparison, not declaring one approach universally superior.

---

### Policy Engines and Governance Pipelines

**Status:** Published

Published material: [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../docs/architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md)

Compare policy evaluation engines with the broader lifecycle around a consequential decision.

Potential distinction:

```text
Policy evaluation
        vs.
Intent → Context → Decision → Acknowledgment → Authority → Execution → Audit
```

---

### API Gateway vs Governance Gateway

**Status:** Published

Published material: [API Gateways, Service Meshes, Zero Trust, and Governed Execution](../docs/architecture/api-gateways-service-meshes-zero-trust-and-governed-execution.md)

Explore how network/API routing concerns differ from consequential-operation governance.

---

### Workflow Engine vs Governance Pipeline

**Status:** Published

Published material: [Workflow Engines, Human Approval Systems, and Governed Execution](../docs/architecture/workflow-engines-human-approval-and-governed-execution.md)

Clarify when orchestration and governance overlap and when they should remain separate.

---

## Lab Requests

All originally requested lab outcomes are now represented in the published lab path. The links below identify the most direct exercise; several labs satisfy more than one original request.

### Beginner

- [x] Convert direct execution into decision-before-execution — [Decision Before Execution](../docs/labs/decision-before-execution.md).
- [x] Replace boolean policy results with explicit outcomes — [Policy Context and Explicit Decision Outcomes](../docs/labs/policy-context-and-explicit-decision-outcomes.md).
- [x] Build a typed policy context — [Policy Context and Explicit Decision Outcomes](../docs/labs/policy-context-and-explicit-decision-outcomes.md).
- [x] Identify missing reason codes in a sample system — [Policy Context and Explicit Decision Outcomes](../docs/labs/policy-context-and-explicit-decision-outcomes.md).
- [x] Correct an unsafe middleware order — [Identify Middleware Ordering Problems](../docs/labs/identify-middleware-ordering-problems.md).

### Intermediate

- [x] Add acknowledgment to a sensitive operation — [Decision Receipts and Acknowledgment](../docs/labs/decision-receipts-and-acknowledgment.md).
- [x] Generate a decision receipt — [Acknowledgment and Audit Residue](../docs/labs/acknowledgment-and-audit-residue.md).
- [x] Add a scoped capability — [Scoped Capability and Host-Owned Execution](../docs/labs/scoped-capability-and-host-owned-execution.md).
- [x] Refactor scattered policy checks into a governance pipeline — [Refactor Scattered Governance Checks](../docs/labs/refactor-scattered-governance-checks.md).
- [x] Add tests for policy edge cases — [Policy Simulation and Change-Impact Analysis](../docs/labs/policy-simulation-and-change-impact-analysis.md).

### Advanced

- [x] Build a governed AI tool gateway — [Governed AI Tool Gateway](../docs/labs/governed-ai-tool-gateway.md).
- [x] Threat-model a capability-based workflow — [Replay Protection and Bounded Use](../docs/labs/replay-protection-and-bounded-use.md).
- [x] Design replay protection for a distributed executor — [Replay Protection and Bounded Use](../docs/labs/replay-protection-and-bounded-use.md).
- [x] Compare two competing policy-composition strategies — [Compare Competing Policy Architectures](../docs/labs/compare-competing-policy-architectures.md).
- [x] Design a multi-region policy overlay — [Design a Regional and Tenant Policy Layer](../docs/labs/design-regional-and-tenant-policy-layer.md).
- [x] Review a deliberately flawed governance architecture — [Analyze a Flawed High-Consequence Workflow](../docs/labs/analyze-flawed-high-consequence-workflow.md).

---

## Diagram Requests

The original diagram requests are represented by the published architecture diagrams or by focused visual explanations embedded in the linked material.

- [x] Intent-to-execution lifecycle — [Governance Spine](../docs/architecture/governance-spine-and-capability-validation-diagrams.md).
- [x] Decision pipeline — [Governance Spine](../docs/architecture/governance-spine-and-capability-validation-diagrams.md).
- [x] Acknowledgment sequence — [Decision Receipts and Acknowledgment](../docs/tutorials/decision-receipts-and-acknowledgment.md).
- [x] Capability issuance and validation — [Capability Validation Profiles](../docs/architecture/governance-spine-and-capability-validation-diagrams.md).
- [x] Host-owned execution boundary — [Governance Spine](../docs/architecture/governance-spine-and-capability-validation-diagrams.md).
- [x] AI tool gateway — [Governed AI Tool Execution](../docs/architecture/governance-spine-and-capability-validation-diagrams.md).
- [x] Logging versus decision receipt — [Decision Receipts and Acknowledgment](../docs/tutorials/decision-receipts-and-acknowledgment.md).
- [x] Authentication/authorization/governance comparison — [Terminology and Established Concepts](../docs/architecture/terminology-and-established-concepts.md).
- [x] Policy composition — [Constraint Composition and Policy Precedence](../docs/governance/constraint-composition-and-policy-precedence.md).
- [x] Regional policy overlay — [Regional and Tenant Policy Overlays](../docs/advanced/regional-and-tenant-policy-overlays.md).
- [x] Supply-chain validation flow — [Software Supply-Chain Integrity for .NET Repositories](../docs/security/software-supply-chain-integrity-for-dotnet-repositories.md).
- [x] ASP.NET Core middleware pipeline — [Middleware Ordering Changes Behavior](../docs/aspnetcore/middleware-ordering-changes-behavior.md).

Mermaid is preferred when it can express the concept clearly because text-based diagrams are easier to review and maintain.

---

## Published Real-World Examples

The original example domains now have realistic but non-domain-sensitive case studies:

- Administrative configuration changes — [Governed Administrative Operation](../docs/case-studies/governed-administrative-operation.md).
- Deployment approvals and infrastructure changes — [Deployment Approval and Infrastructure Change Gates](../docs/case-studies/deployment-approval-and-infrastructure-change-gates.md).
- Sensitive data access — [Sensitive-Data Access Decision](../docs/case-studies/sensitive-data-access-decision.md).
- API tool invocation — [AI-Assisted API and Governed Tool Gateway](../docs/case-studies/ai-assisted-api-and-governed-tool-gateway.md).
- Background operations and capability-scoped jobs — [Capability-Scoped Background Operation](../docs/case-studies/capability-scoped-background-operation.md).
- Multi-tenant applications — [Multi-Tenant and Regional Policy Overlay](../docs/case-studies/multi-tenant-and-regional-policy-overlay.md).
- Human acknowledgment — [Human Acknowledgment Workflow](../docs/case-studies/human-acknowledgment-workflow.md).

Future examples should demonstrate a distinct learner outcome rather than repeat these domains only to add volume. They should continue to avoid unnecessary legal, medical, financial, or regulatory complexity.

---

## Experimental and Advanced Topic Status

Most original experimental candidates now have published treatments. A published page may remain classified **Experimental** where implementation experience or unresolved questions do not justify presenting it as established guidance.

| Topic | Status | Published material or remaining gap |
| --- | --- | --- |
| Distributed governance coordination | Published | [Federated Governance and Independent-Authority Coordination](../docs/advanced/federated-governance-and-independent-authority-coordination.md) |
| Cross-system capability exchange | Published | [Cross-System Capability Exchange and Delegated Authority](../docs/advanced/cross-system-capability-exchange-and-delegated-authority.md) |
| Cryptographic decision ledgers | Published | [Durable Decision Ledgers and Cryptographic Audit Chains](../docs/advanced/durable-decision-ledgers-and-cryptographic-audit-chains.md) |
| External policy providers | Published | [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../docs/architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md) teaches remote and distributed provider boundaries without prescribing a product. |
| Adaptive risk context | Published | [Adaptive Risk Context, Freshness, and Drift](../docs/advanced/adaptive-risk-context-freshness-and-drift.md) |
| Governance telemetry | Published | [AI Governance Observability and End-to-End Decision Tracing](../docs/ai-integration/ai-governance-observability-and-end-to-end-decision-tracing.md) |
| Policy simulation | Published | [Policy Simulation and Change-Impact Analysis lab](../docs/labs/policy-simulation-and-change-impact-analysis.md) and its runnable sample. |
| Agent-to-agent authority delegation | Published — experimental classification retained | [Governed Agent-to-Agent Requests and Multi-Agent Execution Boundaries](../docs/advanced/governed-agent-to-agent-requests-and-multi-agent-execution-boundaries.md) |
| Regional AI governance layers | Experimental — narrower gap remains | A future treatment would need to show how region-specific model and tool constraints compose with authoritative tenant/region context without presenting jurisdictional interpretation as application policy truth. Existing [Regional and Tenant Policy Overlays](../docs/advanced/regional-and-tenant-policy-overlays.md) covers the general composition model. |
| Robotics command gateways | Published — simulated scope | [Simulated Robotics-Command Governance Boundary](../docs/case-studies/simulated-robotics-command-governance-boundary.md) |
| Multi-node replay protection | Published | [Replay Protection and Bounded-Use Authority](../docs/security/replay-protection-and-bounded-use.md) |
| Governance evidence anchoring | Published | [Durable Decision Ledgers and Cryptographic Audit Chains](../docs/advanced/durable-decision-ledgers-and-cryptographic-audit-chains.md) covers anchoring boundaries without claiming immutable storage. |

Experimental status is not a rejection. It signals that assumptions and unresolved questions should remain visible even after educational material is published.

---

## Suggesting Priorities

Community members can help prioritize topics by:

- Opening Discussions.
- Commenting on existing topic requests.
- Providing concrete use cases.
- Sharing failure modes they have encountered.
- Offering to contribute a tutorial, example, lab, or diagram.
- Linking to relevant implementation code or ADRs.
- Explaining where current documentation is unclear.

Priority should generally reflect:

1. Frequency of real questions.
2. Learning value.
3. Relevance to working repository architecture.
4. Availability of a clear teaching example.
5. Community willingness to contribute.
6. Ability to explain the topic without overclaiming.

---

## From Request to Published Material

A requested topic may evolve through the following path:

```text
Requested Topic
      ↓
Discussion
      ↓
Scope and Learning Objective
      ↓
Tutorial / Diagram / Lab Proposal
      ↓
Draft
      ↓
Technical and Editorial Review
      ↓
Published Learning Material
      ↓
Reader Feedback
      ↺
```

A request does not need to become a full tutorial.

Sometimes the best result may be:

- A short explanation.
- A diagram.
- A comparison table.
- A lab.
- A link to an existing ADR.
- A correction to another tutorial.
- A decision not to teach the pattern because it falls outside project scope.

---

## Submit a Topic

If there is something you would like to understand better, request it.

The most useful questions are often the ones that expose an architectural assumption that experienced developers have stopped noticing.

> **Read it. Run it. Question it. Improve it.**
