# Requested Topics

`AsiBackbone/Learning` is intended to evolve in response to real questions from developers, architects, reviewers, students, security practitioners, and contributors.

This page tracks learning topics that the community would like to see explained, demonstrated, compared, or turned into hands-on labs.

If a topic repeatedly appears in Issues or Discussions, that is a strong signal that it may deserve dedicated Learning material.

## How to Request a Topic

For a new topic request, prefer opening a GitHub Discussion when the subject is exploratory, architectural, or likely to benefit from community input.

Use an Issue when the requested work is already concrete and well scoped.

A useful request includes:

- The problem you are trying to understand.
- Why the topic matters in practice.
- What level of detail would be most useful.
- Whether you would prefer a tutorial, lab, diagram, comparison, or worked example.
- Any existing ASI Backbone or NetCoreApplicationTemplate implementation that appears relevant.
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

---

# Current Priority Requests

These topics align with the initial Learning roadmap and are strong candidates for early tutorials and labs.

## Decision Before Execution

**Status:** Planned

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

## Policy Context

**Status:** Planned

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

## Explicit Decision Outcomes

**Status:** Planned

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

## Acknowledgment Workflows

**Status:** Planned

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

## Audit Residue and Provenance

**Status:** Planned

Explain the difference between normal application logs and durable governance evidence.

Questions to address:

- What should survive a decision?
- What is a useful audit receipt?
- How should reason codes, policy versions, hashes, correlation IDs, and timestamps be used?
- What should not be placed in an audit record?
- How should privacy and sensitive data affect audit design?

Suggested format:

- Tutorial
- Record model
- Logging versus audit comparison
- Failure-mode examples

---

## Scoped Capability

**Status:** Planned

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

## Host-Owned Execution

**Status:** Planned

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

## Governed AI Tool Gateway

**Status:** Planned

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

# ASP.NET Core Architecture Requests

## Middleware Ordering

**Status:** Requested

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

## Secure-by-Default Configuration

**Status:** Requested

Explain how application defaults can reduce accidental exposure.

Possible areas:

- Explicit opt-in
- Environment validation
- Feature toggles
- Configuration validation
- Secrets
- Production versus development behavior

---

## Structured Logging

**Status:** Requested

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

## Centralized Error Handling

**Status:** Requested

Explore centralized exception handling and consistent client-facing error behavior.

Possible areas:

- Problem Details
- Information disclosure
- Exception mapping
- Status codes
- Correlation identifiers
- Logging boundaries

---

## EF Core Cross-Cutting Behavior

**Status:** Requested

Explain when interceptors, repositories, unit-of-work patterns, or direct DbContext usage make sense.

Possible areas:

- SaveChanges interceptors
- Auditing
- Transactions
- Persistence boundaries
- Testing
- Tradeoffs between abstraction and transparency

---

## Architecture Decision Records

**Status:** Requested

Teach how to write and maintain ADRs using real decisions from the organization repositories as references.

Questions to address:

- What deserves an ADR?
- What belongs in an ADR?
- How should superseded decisions be handled?
- How do ADRs differ from code comments?
- How can ADRs feed Learning tutorials?

---

# Security and Trust Architecture Requests

## Authentication vs Authorization vs Governance

**Status:** Requested

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

## Capability-Based Security

**Status:** Requested

Compare capability-scoped authority with traditional role- and claims-based authorization.

Suggested areas:

- Least authority
- Resource binding
- Operation binding
- Expiration
- Delegation
- Replay

---

## Replay Protection

**Status:** Requested

Explain replay as a system-level concern rather than only a token-validation detail.

Possible examples:

- One-time operations
- Durable nonce storage
- Bounded-use grants
- Distributed execution nodes
- Failure recovery

---

## Signing and Verification

**Status:** Requested

Introduce signing concepts without implying that signing alone creates trust.

Questions to address:

- What does a signature prove?
- What does it not prove?
- Who owns key custody?
- How should key rotation be handled?
- How do signing and provenance differ?

---

## Tamper-Evident Audit Records

**Status:** Requested

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

## Software Supply-Chain Integrity

**Status:** Requested

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

# AI and Agent Governance Requests

## AI Proposed Intent

**Status:** Requested

Explain how to translate model output into structured proposed intent before policy evaluation.

Possible areas:

- Typed tool requests
- Schema validation
- Argument normalization
- Untrusted model output
- Intent metadata

---

## Human-in-the-Loop Governance

**Status:** Requested

Explore when human review is useful and when it becomes security theater.

Questions to address:

- What should a human actually review?
- How should context be presented?
- How should acknowledgment be recorded?
- What happens when humans routinely approve everything?

---

## Tool Allowlists and Argument Constraints

**Status:** Requested

Show how tool-level authorization can remain narrow even when a model has access to many capabilities.

Suggested format:

- Tutorial
- Example policy
- Negative tests

---

## Agent-to-Agent Requests

**Status:** Experimental

Explore governance when one automated system proposes an operation to another automated system.

Potential questions:

- How is originating intent preserved?
- What context crosses boundaries?
- How is authority delegated?
- Who owns final execution?
- How are decision chains audited?

---

## AI Decision Explainability

**Status:** Requested

Distinguish:

- Model explanation
- Policy reason
- Governance decision reason
- Host execution result

The tutorial should avoid treating generated explanations as authoritative evidence of model internals.

---

# Policy Architecture Requests

## Policy Composition

**Status:** Requested

Explore how multiple constraints combine into one decision.

Possible areas:

- Precedence
- Short-circuiting
- Aggregation
- Conflicting rules
- Required acknowledgments
- Escalation

---

## Policy Versioning

**Status:** Requested

Explain why a decision should often record which policy version produced it.

Possible areas:

- Reproducibility
- Audit
- Rollback
- Historical interpretation
- Policy hashes

---

## Regional and Tenant Policy Overlays

**Status:** Requested

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

## Degraded-Mode Governance

**Status:** Requested

Explore what should happen when a policy provider, storage dependency, risk service, or external governance component is unavailable.

Questions to address:

- Fail open or fail closed?
- Which operations may defer?
- Which may continue?
- How should degraded decisions be recorded?

---

## Policy Testing

**Status:** Requested

Show practical strategies for testing governance rules.

Potential areas:

- Unit tests
- Decision tables
- Boundary cases
- Property-based testing
- Regression cases
- Policy snapshots

---

# Architecture Comparison Requests

## Boolean Authorization vs Explicit Decision Models

**Status:** Requested

Compare simple authorization checks with structured governance decision results.

---

## RBAC vs Claims vs Policy vs Capability

**Status:** Requested

Explain where each model is useful and where the concepts overlap.

The goal should be comparison, not declaring one approach universally superior.

---

## Policy Engines and Governance Pipelines

**Status:** Requested

Compare policy evaluation engines with the broader lifecycle around a consequential decision.

Potential distinction:

```text
Policy evaluation
        vs.
Intent → Context → Decision → Acknowledgment → Authority → Execution → Audit
```

---

## API Gateway vs Governance Gateway

**Status:** Requested

Explore how network/API routing concerns differ from consequential-operation governance.

---

## Workflow Engine vs Governance Pipeline

**Status:** Requested

Clarify when orchestration and governance overlap and when they should remain separate.

---

# Lab Requests

## Beginner

- [ ] Convert direct execution into decision-before-execution.
- [ ] Replace boolean policy results with explicit outcomes.
- [ ] Build a typed policy context.
- [ ] Identify missing reason codes in a sample system.
- [ ] Correct an unsafe middleware order.

## Intermediate

- [ ] Add acknowledgment to a sensitive operation.
- [ ] Generate an audit receipt.
- [ ] Add a scoped capability.
- [ ] Refactor scattered policy checks into a governance pipeline.
- [ ] Add tests for policy edge cases.

## Advanced

- [ ] Build a governed AI tool gateway.
- [ ] Threat-model a capability-based workflow.
- [ ] Design replay protection for a distributed executor.
- [ ] Compare two competing policy-composition strategies.
- [ ] Design a multi-region policy overlay.
- [ ] Review a deliberately flawed governance architecture.

---

# Diagram Requests

Diagrams are especially useful for concepts where boundaries matter.

Requested diagrams include:

- [ ] Intent-to-execution lifecycle.
- [ ] Decision pipeline.
- [ ] Acknowledgment sequence.
- [ ] Capability issuance and validation.
- [ ] Host-owned execution boundary.
- [ ] AI tool gateway.
- [ ] Logging versus audit residue.
- [ ] Authentication/authorization/governance comparison.
- [ ] Policy composition.
- [ ] Regional policy overlay.
- [ ] Supply-chain validation flow.
- [ ] ASP.NET Core middleware pipeline.

Mermaid is preferred when it can express the concept clearly because text-based diagrams are easier to review and maintain.

---

# Topics That Need Real-World Examples

Some concepts are easier to understand with realistic but non-domain-sensitive scenarios.

Useful example domains include:

- Administrative configuration changes
- Deployment approvals
- Infrastructure changes
- Sensitive data access
- API tool invocation
- Background operations
- Multi-tenant applications
- Human acknowledgment
- Capability-scoped jobs

Contributors are welcome to propose examples that demonstrate consequential decisions without introducing unnecessary legal, medical, financial, or regulatory complexity.

---

# Experimental Topic Candidates

These topics may be valuable but should remain clearly labeled until the project has enough implementation experience to teach them responsibly.

- Distributed governance coordination
- Cross-system capability exchange
- Cryptographic decision ledgers
- External policy providers
- Adaptive risk context
- Governance telemetry
- Policy simulation
- Agent-to-agent authority delegation
- Regional AI governance layers
- Robotics command gateways
- Multi-node replay protection
- Governance evidence anchoring

Experimental status is not a rejection.

It is a signal that assumptions and unresolved questions should remain visible.

---

# Suggesting Priorities

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

# From Request to Published Material

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

# Submit a Topic

If there is something you would like to understand better, request it.

The most useful questions are often the ones that expose an architectural assumption that experienced developers have stopped noticing.

> **Read it. Run it. Question it. Improve it.**
