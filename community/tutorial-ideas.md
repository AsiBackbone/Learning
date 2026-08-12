# Tutorial Ideas

`AsiBackbone/Learning` is intended to grow through practical questions, architectural experiments, and community contributions.

This page collects tutorial concepts that may become future Learning material.

Unlike [requested-topics.md](requested-topics.md), which tracks subjects the community would like to see addressed, this page focuses on **possible tutorial shapes**: concrete lessons that could be written, demonstrated, tested, and connected to the working ASI Backbone repositories.

These ideas are not release commitments.

They are starting points.

> **Read it. Run it. Question it. Improve it.**

---

# What Makes a Good Tutorial Idea?

A strong tutorial idea usually has:

- A recognizable architectural problem.
- A clear learning objective.
- A small enough scope to teach well.
- A meaningful failure mode or tradeoff.
- A minimal example that can be understood independently.
- A connection to a real implementation, ADR, test, or pattern when one exists.
- A reason the learner should care beyond simply using a particular package.

A useful tutorial should answer more than:

> "How do I call this API?"

It should help the reader understand:

> "Why does this architectural boundary exist, and when should I use it?"

---

# Suggested Tutorial Structure

Where practical, tutorials should follow a problem-first progression:

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
Working repository example
   ↓
Questions for further exploration
```

Not every tutorial needs every section, but the learning objective should remain visible throughout.

---

# Foundation Tutorials

## 1. Decision Before Execution

### Working Title

**Decision Before Execution: Why Consequential Actions Need a Governance Boundary**

### Learning Objective

Understand why a request to perform an operation should not automatically become execution authority.

### Scenario

An administrator clicks a button that performs a consequential operation directly inside a controller or service method.

### Naive Flow

```text
HTTP Request
   ↓
Authorization Check
   ↓
Service Call
   ↓
Consequential Action
```

### Governed Flow

```text
Proposed Intent
   ↓
Policy Context
   ↓
Constraint Evaluation
   ↓
Decision
   ↓
Acknowledgment if required
   ↓
Scoped Authority
   ↓
Host-Owned Execution
   ↓
Audit Residue
```

### Teaching Opportunities

- Intent versus execution.
- Authorization versus governance.
- Explicit decision states.
- Why the host remains responsible for execution.
- When the pattern is unnecessary.

### Possible Lab

Refactor a controller that performs a sensitive operation directly into a decision-before-execution workflow.

**Status:** High priority

---

## 2. Beyond `bool`: Modeling Explicit Governance Outcomes

### Working Title

**Beyond Allow or Deny: Designing Explicit Decision Results**

### Learning Objective

Understand when a boolean authorization result is too limited for consequential workflows.

### Candidate Outcomes

```text
Allow
Deny
Defer
RequireAcknowledgment
Escalate
```

### Teaching Opportunities

- Why `true` and `false` lose important meaning.
- Reason codes versus human-readable messages.
- Decision metadata.
- Host behavior for each outcome.
- Testing decision matrices.

### Possible Exercise

Start with:

```csharp
bool CanExecute(Request request)
```

and evolve toward a structured decision result.

**Status:** High priority

---

## 3. Building a Policy Context

### Working Title

**Policy Context: Making Decision Inputs Explicit**

### Learning Objective

Understand how to represent the facts needed for policy evaluation without scattering them across services and middleware.

### Possible Context Dimensions

- Actor
- Operation
- Resource
- Tenant
- Region
- Environment
- Risk
- Request metadata
- Correlation information

### Teaching Opportunities

- Strong typing versus dictionaries.
- Context boundaries.
- Testability.
- Avoiding context-object sprawl.
- Separating facts from policy rules.

### Possible Lab

Given policy checks scattered across a controller, service, and helper, consolidate the relevant facts into a policy context.

**Status:** High priority

---

## 4. Reason Codes as an Architectural Contract

### Working Title

**Reason Codes: Making Governance Decisions Reviewable**

### Learning Objective

Understand why stable reason codes are often more useful than free-form decision messages.

### Teaching Opportunities

- Machine-readable versus human-readable explanation.
- Localization.
- Audit.
- Metrics.
- Testing.
- Backward compatibility.
- Avoiding sensitive information in reasons.

### Possible Example

Compare:

```text
"Request denied because something was wrong."
```

with:

```text
Decision: Deny
ReasonCode: Operation.NotPermittedInRegion
```

**Status:** Candidate

---

# Acknowledgment and Human Responsibility

## 5. Acknowledgment Is Not Authentication

### Working Title

**Authentication, Approval, and Acknowledgment Are Different Boundaries**

### Learning Objective

Separate identity verification, authorization, approval, and acknowledgment.

### Teaching Opportunities

- Authentication proves identity.
- Authorization grants access.
- Approval may be a workflow decision.
- Acknowledgment records conscious acceptance of a specific consequence or condition.

### Possible Diagram

```text
Identity
   ↓
Authorization
   ↓
Governance Decision
   ↓
Acknowledgment
   ↓
Execution Authority
```

### Possible Lab

Add acknowledgment to an already authenticated and authorized administrative workflow.

**Status:** High priority

---

## 6. Designing an Acknowledgment Handshake

### Working Title

**Building a Human Acknowledgment Boundary in ASP.NET Core**

### Learning Objective

Implement a workflow that pauses a consequential action until acknowledgment is explicitly captured.

### Teaching Opportunities

- Decision persistence.
- Expiration.
- Correlation.
- Changed context between decision and acknowledgment.
- Preventing stale acknowledgments.
- Recording what was acknowledged.

### Possible Scenario

A destructive administrative operation requires the operator to explicitly acknowledge the affected resource and consequence before proceeding.

**Status:** High priority

---

## 7. When Human Approval Becomes Security Theater

### Working Title

**Human in the Loop: Useful Control or Rubber Stamp?**

### Learning Objective

Explore when human review meaningfully reduces risk and when it simply adds friction.

### Teaching Opportunities

- Alert fatigue.
- Repeated approvals.
- Missing context.
- Approval quality.
- Escalation.
- Risk-based review.
- Automation bias.

### Possible Exercise

Review three approval dialogs and determine which one actually supports informed decision-making.

**Status:** Candidate

---

# Audit and Provenance Tutorials

## 8. Logging Is Not an Audit Receipt

### Working Title

**Operational Logs vs. Governance Evidence**

### Learning Objective

Understand why logs and audit residue serve different purposes.

### Comparison Areas

| Operational Logging | Governance Audit |
|---|---|
| Troubleshooting | Decision reconstruction |
| High volume | Deliberately structured |
| Often mutable/retained briefly | May require durable retention |
| Application-centric | Decision-centric |
| Free-form context common | Stable fields and reason codes preferred |

### Teaching Opportunities

- Correlation IDs.
- Decision IDs.
- Policy version.
- Actor/resource context.
- Data minimization.
- Retention.
- Privacy.

### Possible Lab

Given a set of application logs, design the smallest useful governance receipt.

**Status:** High priority

---

## 9. Designing an Audit Receipt

### Working Title

**What Should Survive a Governance Decision?**

### Learning Objective

Design a structured record that explains what was requested, how it was evaluated, and what authority followed.

### Possible Fields

- Decision ID
- Correlation ID
- Intent summary
- Actor reference
- Resource reference
- Outcome
- Reason codes
- Policy version/hash
- Timestamp
- Acknowledgment reference
- Capability reference
- Execution result reference

### Teaching Opportunities

- Evidence versus exhaustive logging.
- Privacy boundaries.
- Immutability versus tamper evidence.
- Storage ownership.

**Status:** High priority

---

## 10. Tamper-Evident Does Not Mean Immutable

### Working Title

**Tamper Evidence, Signing, and Durable Audit Storage**

### Learning Objective

Clarify commonly conflated security properties.

### Teaching Opportunities

- Durable storage.
- Append-only semantics.
- Hashing.
- Hash chains.
- Digital signatures.
- Verification.
- External anchoring.
- Key custody.
- What each mechanism actually proves.

### Important Boundary

The tutorial should avoid presenting a simplified demo as a production-grade tamper-proof ledger.

**Status:** Advanced candidate

---

# Capability and Execution Tutorials

## 11. Approval Is Not Permanent Authority

### Working Title

**From Approval to Scoped Capability**

### Learning Objective

Understand why a successful decision may grant narrow, short-lived authority rather than broad standing permission.

### Teaching Opportunities

- Operation binding.
- Resource binding.
- Expiration.
- Audience.
- Subject.
- Usage limits.
- Proof requirements.
- Replay protection.

### Possible Lab

Replace a broad "admin can execute" check with a grant scoped to one operation and resource.

**Status:** High priority

---

## 12. Validate Again at the Execution Boundary

### Working Title

**Why Capability Validation Belongs Next to Execution**

### Learning Objective

Understand why authority should be checked where the consequential operation actually occurs.

### Teaching Opportunities

- Time-of-check/time-of-use.
- Expiration.
- Resource mismatch.
- Operation mismatch.
- Replay.
- Stale decisions.
- Distributed components.

### Possible Sequence

```text
Decision Service
   ↓
Capability Issued
   ↓
Time Passes
   ↓
Executor Receives Request
   ↓
Capability Revalidated
   ↓
Operation Executes
```

**Status:** High priority

---

## 13. Host-Owned Execution

### Working Title

**The Governance Layer Decides; the Host Executes**

### Learning Objective

Clarify why a policy framework should not silently become an execution engine.

### Teaching Opportunities

- Separation of responsibility.
- Authentication and authorization remain host concerns.
- Infrastructure and safety controls remain host concerns.
- Governance outcome does not guarantee operational success.
- Execution failure should remain distinct from governance denial.

### Possible Example

A governance decision permits a deployment operation, but the deployment system still owns credentials, environment controls, retries, rollback, and execution.

**Status:** High priority

---

# AI Governance Tutorials

## 14. The Model May Propose; the Host Executes

### Working Title

**Building a Governed AI Tool Gateway in ASP.NET Core**

### Learning Objective

Demonstrate an end-to-end AI tool-call flow where model output is treated as proposed intent rather than execution authority.

### Flow

```text
User
   ↓
Model
   ↓
Proposed Tool Call
   ↓
Schema Validation
   ↓
Policy Context
   ↓
Governance Decision
   ↓
Acknowledgment if required
   ↓
Scoped Capability
   ↓
Host Tool Gateway
   ↓
Tool Execution
   ↓
Audit Residue
```

### Teaching Opportunities

- Model output is untrusted input.
- Tool allowlists.
- Argument validation.
- Explicit policy decisions.
- Human acknowledgment.
- Capability boundaries.
- Audit.
- Failure handling.

### Possible Deliverables

- Minimal ASP.NET Core application.
- Mock model interface.
- Mock tools.
- Mermaid sequence diagram.
- Tests.
- Threat-model notes.
- Lab.

**Status:** Highest-priority end-to-end tutorial

---

## 15. Treat AI Tool Arguments as Untrusted Input

### Working Title

**Schema Validation Before AI Tool Execution**

### Learning Objective

Show why a valid model response is not necessarily a valid application operation.

### Teaching Opportunities

- JSON/schema validation.
- Domain validation.
- Resource existence.
- Allowlisted values.
- Path traversal.
- Injection.
- Numeric bounds.
- Host-side normalization.

### Possible Lab

Provide several plausible-looking AI tool calls and require the learner to identify which must be rejected before policy evaluation or execution.

**Status:** Candidate

---

## 16. Model Explanation vs. Governance Explanation

### Working Title

**Who Explains the Decision? AI Output vs. Policy Evidence**

### Learning Objective

Distinguish generated model explanations from authoritative policy reasons.

### Teaching Opportunities

- Generated explanations can be useful UI.
- Policy reason codes should come from policy evaluation.
- Execution results should come from the host.
- Audit evidence should not depend on a model inventing its own rationale.

### Possible Diagram

```text
Model:
"I recommend deleting the resource."

Governance:
Outcome = RequireAcknowledgment
Reason = Resource.DestructiveOperation

Host:
ExecutionResult = Completed
```

**Status:** Candidate

---

## 17. Tool Allowlists

### Working Title

**Giving an AI Fewer Things It Can Ask For**

### Learning Objective

Demonstrate how reducing the available tool surface can simplify governance.

### Teaching Opportunities

- Least capability.
- Static allowlists.
- Contextual allowlists.
- Per-user tool exposure.
- Per-operation restrictions.
- Difference between hiding a tool and authorizing a tool.

**Status:** Candidate

---

## 18. Agent-to-Agent Governance

### Working Title

**When One Automated System Requests Action From Another**

### Learning Objective

Explore how intent, authority, and provenance may cross automated-system boundaries.

### Questions

- Who originated the intent?
- Which system owns execution?
- Can authority be delegated?
- How is context preserved?
- How are chained decisions audited?

### Classification

Experimental.

**Status:** Experimental

---

# ASP.NET Core Architecture Tutorials

## 19. Middleware Order Is Architecture

### Working Title

**ASP.NET Core Middleware Ordering: Why Sequence Changes Security and Behavior**

### Learning Objective

Understand middleware ordering as an architectural decision rather than boilerplate.

### Teaching Opportunities

- Exception handling.
- Forwarded headers.
- Routing.
- Authentication.
- Authorization.
- Rate limiting.
- Static files.
- Security headers.
- Request logging.

### Possible Lab

Give the learner an intentionally misordered pipeline and require them to identify behavior and security problems.

**Status:** High-value NCAT tutorial

---

## 20. Secure Defaults as Active Constraints

### Working Title

**Secure by Default: Designing Configuration That Fails Safely**

### Learning Objective

Show how defaults constrain the set of behaviors an application can enter.

### Teaching Opportunities

- Opt-in versus opt-out.
- Configuration validation.
- Environment checks.
- Dangerous development defaults.
- Explicit production requirements.

### Possible Connection

Use `NetCoreApplicationTemplate` as the working implementation reference.

**Status:** Candidate

---

## 21. Structured Logging Without Logging Everything

### Working Title

**Useful Telemetry Without Sensitive-Data Sprawl**

### Learning Objective

Balance observability with privacy and maintainability.

### Teaching Opportunities

- Structured events.
- Correlation.
- Event IDs.
- Sensitive fields.
- Request bodies.
- Exception data.
- Log volume.
- Sampling.
- Audit separation.

**Status:** Candidate

---

## 22. Centralized Error Handling

### Working Title

**One Error Boundary, Many Failure Modes**

### Learning Objective

Show how centralized exception and status-code handling can improve consistency without hiding useful diagnostics.

### Teaching Opportunities

- Problem Details.
- Error mapping.
- Information disclosure.
- Correlation.
- Logging.
- Expected versus unexpected failures.

**Status:** Candidate

---

## 23. Architecture Decision Records

### Working Title

**Writing ADRs That Future Maintainers Can Actually Use**

### Learning Objective

Teach ADRs through real repository examples.

### Teaching Opportunities

- Decision context.
- Alternatives.
- Consequences.
- Status.
- Superseding decisions.
- ADRs versus comments.
- ADRs versus tutorials.

### Possible Exercise

Take an undocumented architectural choice and write a concise ADR for it.

**Status:** High-value learning tutorial

---

# Security and Supply-Chain Tutorials

## 24. Why Pin GitHub Actions by SHA?

### Working Title

**Immutable Workflow Dependencies: GitHub Actions SHA Pinning**

### Learning Objective

Understand the supply-chain value of immutable action references.

### Teaching Opportunities

- Tags are mutable references.
- Commit SHAs are immutable references.
- Version comments preserve readability.
- Renovation/dependency automation.
- Detecting mismatched version comments.
- Tradeoff between maintenance and integrity.

### Possible Working Reference

Use existing ASI Backbone workflow patterns as examples.

**Status:** Candidate

---

## 25. Locked Restore

### Working Title

**Why Reproducible Builds Need Locked Dependencies**

### Learning Objective

Explain what lock files contribute to build reproducibility and dependency review.

### Teaching Opportunities

- Dependency drift.
- Locked restore.
- Dependabot updates.
- Regenerating lock files.
- CI enforcement.
- Transitive dependencies.

**Status:** Candidate

---

## 26. SBOM and Provenance Are Different

### Working Title

**What Was Built vs. How It Was Built**

### Learning Objective

Distinguish a Software Bill of Materials from build provenance.

### Teaching Opportunities

- Package contents and dependencies.
- Build identity.
- Source commit.
- Artifact attestation.
- Verification.
- Limits of each mechanism.

**Status:** Candidate

---

## 27. Package Signing, Repository Signatures, and Provenance

### Working Title

**Three Different Questions About Package Trust**

### Learning Objective

Separate several mechanisms that are often discussed as if they were equivalent.

### Questions

- Who signed the package?
- Which repository distributed it?
- Which workflow produced it?
- What source commit was involved?
- What does each control fail to prove?

**Status:** Advanced candidate

---

# Policy Architecture Tutorials

## 28. Policy Composition

### Working Title

**When Multiple Policies Disagree**

### Learning Objective

Explore how constraints can combine into one decision.

### Possible Approaches

- Deny overrides.
- Priority ordering.
- Weighted risk.
- Escalation.
- Required acknowledgment.
- Defer when information is missing.

### Possible Lab

Given five policies with conflicting outcomes, design an explicit composition strategy.

**Status:** Candidate

---

## 29. Policy Versioning

### Working Title

**Which Policy Made This Decision?**

### Learning Objective

Show why policy identity/version can be important for audit and reproducibility.

### Teaching Opportunities

- Version IDs.
- Hashes.
- Deployment timestamps.
- Historical reconstruction.
- Rollback.
- Policy migration.

**Status:** Candidate

---

## 30. Fail Open, Fail Closed, or Defer?

### Working Title

**Governance During Dependency Failure**

### Learning Objective

Explore degraded-mode behavior when a policy dependency is unavailable.

### Scenario

A risk service times out during evaluation.

Possible responses:

```text
Allow
Deny
Defer
Escalate
Fallback Policy
```

### Teaching Opportunities

- Consequence-based failure policy.
- Availability versus safety.
- Audit.
- Retry behavior.
- User experience.

**Status:** High-value advanced tutorial

---

## 31. Regional and Tenant Policy Layers

### Working Title

**Composing Global, Regional, and Tenant Constraints**

### Learning Objective

Understand layered policy without assuming that one layer always overrides another.

### Possible Flow

```text
Global Constraints
   ↓
Regional Constraints
   ↓
Tenant Constraints
   ↓
Operation Constraints
   ↓
Decision Composition
```

### Teaching Opportunities

- Precedence.
- Conflict.
- Policy provenance.
- Local autonomy.
- Default behavior.

**Status:** Advanced candidate

---

# Comparison Tutorials

## 32. Authentication vs. Authorization vs. Governance

### Working Title

**Three Questions That Look Similar but Are Not**

### Questions

```text
Authentication:
Who are you?

Authorization:
Are you permitted?

Governance:
Under these conditions, should this consequential action proceed, and what must happen before execution?
```

### Learning Objective

Prevent architectural responsibilities from being collapsed into a single authorization check.

**Status:** High-value comparison

---

## 33. RBAC, Claims, Policy, and Capability

### Working Title

**Choosing an Authority Model**

### Learning Objective

Compare common authority models without presenting one as universally superior.

### Possible Dimensions

- Identity coupling.
- Delegation.
- Scope.
- Duration.
- Revocation.
- Audit.
- Ease of administration.

**Status:** Candidate

---

## 34. Policy Engine vs. Governance Pipeline

### Working Title

**Evaluation Is Only One Stage of the Decision Lifecycle**

### Learning Objective

Clarify the difference between evaluating policy and governing a consequential operation end to end.

### Comparison

```text
Policy Engine:
Input → Rules → Result

Governance Pipeline:
Intent
  → Context
  → Constraints
  → Decision
  → Acknowledgment
  → Authority
  → Execution Boundary
  → Audit
```

**Status:** High-value comparison

---

## 35. API Gateway vs. Governance Gateway

### Working Title

**Routing Traffic Is Not the Same as Governing Action**

### Learning Objective

Compare network/API gateway responsibilities with decision/execution governance.

**Status:** Candidate

---

## 36. Workflow Engine vs. Governance Pipeline

### Working Title

**Orchestration and Governance Solve Different Problems**

### Learning Objective

Explain where workflow orchestration overlaps with governance and where they should remain separate.

**Status:** Candidate

---

# Failure-Mode Tutorials

## 37. The Boolean Authorization Trap

### Scenario

A system treats every consequential operation as:

```csharp
if (user.CanDoThing)
{
    DoThing();
}
```

### Learning Objective

Identify information lost by collapsing policy, acknowledgment, authority, and execution into one boolean.

**Status:** Candidate

---

## 38. The Permanent Admin Token

### Scenario

A broad credential exists because creating scoped authority seemed inconvenient.

### Learning Objective

Explore the operational and security consequences of standing authority.

**Status:** Candidate

---

## 39. The Audit Log That Cannot Explain the Decision

### Scenario

The application records requests and exceptions but cannot reconstruct why a sensitive operation was allowed.

### Learning Objective

Identify missing governance evidence.

**Status:** Candidate

---

## 40. The Approval Button Everyone Clicks

### Scenario

A human approval step exists, but operators receive insufficient information and approve nearly every request.

### Learning Objective

Explore acknowledgment quality and automation bias.

**Status:** Candidate

---

## 41. The AI Agent With Direct Database Access

### Scenario

A model can translate user language directly into database-changing operations.

### Learning Objective

Identify missing boundaries:

- Schema validation.
- Policy context.
- Explicit decision.
- Scoped authority.
- Host-owned execution.
- Audit residue.

**Status:** High-value AI failure-mode tutorial

---

# Tutorial Series Ideas

## Series A — Governed Execution Fundamentals

1. Decision Before Execution
2. Policy Context
3. Explicit Decision Outcomes
4. Acknowledgment
5. Audit Residue
6. Scoped Capability
7. Host-Owned Execution

### Final Project

Build a governed administrative operation from end to end.

---

## Series B — Governed AI Tool Use

1. AI Output as Proposed Intent
2. Tool Schema Validation
3. Tool Allowlists
4. Policy Context for AI Actions
5. Human Acknowledgment
6. Capability-Scoped Execution
7. Audit and Execution Results

### Final Project

Build a governed AI tool gateway in ASP.NET Core.

---

## Series C — Secure ASP.NET Core Architecture

1. Middleware Ordering
2. Secure Defaults
3. Structured Logging
4. Error Handling
5. Rate Limiting
6. Authentication Boundaries
7. Data Access
8. Architecture Decision Records

### Working Reference

`AsiBackbone/NetCoreApplicationTemplate`

---

## Series D — Software Supply-Chain Reasoning

1. Dependency Locking
2. SHA-Pinned GitHub Actions
3. Build Reproducibility
4. SBOM
5. Provenance
6. Package Metadata
7. Source Link
8. Signing and Repository Trust

---

# Tutorial Formats to Encourage

Not every contribution needs to be a long-form tutorial.

Useful formats include:

### Concept Tutorial

Explains one architectural idea.

### Walkthrough

Steps through a small working implementation.

### Failure Analysis

Starts with a flawed design and improves it.

### Comparison

Examines two or more approaches and their tradeoffs.

### Repository Tour

Uses a working repository as a specimen and explains why it is structured a certain way.

### Lab

Provides a problem for the learner to solve.

### Architecture Review

Reviews a small example system and identifies boundaries, assumptions, and risks.

### Diagram-First Tutorial

Uses one strong diagram as the central teaching artifact.

### Test-Driven Tutorial

Uses failing tests to reveal an architectural requirement.

---

# Ideas for Beginner-Friendly Tutorials

Potential beginner contributions should avoid requiring deep familiarity with the complete ASI Backbone architecture.

Examples:

- Why `bool` is sometimes not enough.
- What is a correlation ID?
- Why middleware order matters.
- What is a reason code?
- Authentication versus authorization.
- What is an audit receipt?
- Why validate configuration at startup?
- Why avoid logging secrets?
- What does a lock file do?
- Why pin a GitHub Action?
- What is an ADR?

These can provide easier entry points for contributors and readers.

---

# Ideas for Advanced Tutorials

Advanced material may explore:

- Distributed policy evaluation.
- Durable replay protection.
- Multi-node capability validation.
- Policy provenance.
- Cryptographic audit chains.
- Regional governance overlays.
- AI agent delegation.
- External policy providers.
- High-consequence workflow design.
- Governed robotics command gateways.

Advanced tutorials should clearly identify assumptions and avoid presenting experimental patterns as settled guidance.

---

# Ideas That Cross Both Working Repositories

Some of the strongest tutorials may connect concepts from both `AsiBackbone` and `NetCoreApplicationTemplate`.

Examples:

## Governed Administrative Endpoint

Use NCAT for:

- ASP.NET Core host
- Middleware
- Authentication
- Logging
- Configuration
- Persistence

Use AsiBackbone for:

- Intent
- Policy context
- Decision
- Acknowledgment
- Capability
- Audit residue

### Learning Goal

Show how application architecture and governance architecture complement each other without collapsing into one framework.

---

## Governed Deployment Request

Use the application host to receive and authenticate the request.

Use governance patterns to decide whether deployment should proceed.

Leave actual deployment execution with a separate host-owned executor.

### Learning Goal

Teach the distinction among:

```text
Request
Decision
Authority
Execution
Evidence
```

---

## Governed AI Administrative Assistant

Use an ASP.NET Core host with a simulated AI assistant proposing administrative operations.

Apply governance before tool execution.

### Learning Goal

Provide a realistic end-to-end reference example without requiring a specific external AI provider.

---

# Community-Sourced Tutorial Ideas

Contributors are encouraged to add ideas here or propose them through Discussions.

A useful proposal format is:

```markdown
## Tutorial Idea: Title

### Problem

What problem does the tutorial explain?

### Learning Objective

What should the reader understand or be able to do afterward?

### Example Scenario

What small scenario can demonstrate the idea?

### Failure Mode

What goes wrong in the naive or common design?

### Pattern

What architectural pattern addresses the problem?

### Working Reference

Is there relevant code, documentation, an ADR, or a test in another ASI Backbone repository?

### Suggested Format

Tutorial / Lab / Diagram / Comparison / Repository Tour

### Difficulty

Beginner / Intermediate / Advanced

### Status

Idea / Discussing / Planned / In Progress
```

---

# Tutorial Selection Criteria

When choosing what to build next, consider:

1. Does the topic address a real architectural question?
2. Can the lesson be demonstrated clearly?
3. Is the tutorial useful without requiring package adoption?
4. Is there a meaningful tradeoff to explain?
5. Can the example remain reasonably small?
6. Is there a working implementation or ADR to connect to?
7. Has the topic appeared repeatedly in Issues or Discussions?
8. Does the tutorial strengthen an existing learning path?
9. Can contributors realistically maintain the example?
10. Does the topic fit the boundaries of the Learning repository?

---

# Ideas That Should Usually Stay Elsewhere

Some material may be useful but belong in another repository.

### AsiBackbone Implementation Documentation

Detailed API reference and package-specific implementation guidance should normally remain in:

`AsiBackbone/AsiBackbone`

### NetCoreApplicationTemplate Implementation Documentation

Detailed template configuration and application-specific operational guidance should normally remain in:

`AsiBackbone/NetCoreApplicationTemplate`

### Organization Identity and Default Community Files

Organization-level profile and shared GitHub community configuration should normally remain in:

`AsiBackbone/.github`

Learning should link to those sources rather than duplicating them.

---

# From Idea to Tutorial

A tutorial idea may progress through:

```text
Idea
  ↓
Discussion
  ↓
Learning Objective
  ↓
Scope
  ↓
Minimal Example
  ↓
Draft
  ↓
Technical Review
  ↓
Editorial Review
  ↓
Published Tutorial
  ↓
Lab or Follow-Up
  ↓
Reader Feedback
  ↺
```

Not every idea needs to reach publication.

An idea may be merged with another topic, reduced to a diagram, deferred, moved to another repository, or rejected if it does not fit the Learning project's scope.

---

# Current Recommended Starting Set

If the community were to build only a small first collection, the recommended starting tutorials are:

1. **Decision Before Execution**
2. **Policy Context**
3. **Beyond `bool`: Explicit Decision Outcomes**
4. **Acknowledgment Is Not Authentication**
5. **Logging Is Not an Audit Receipt**
6. **Approval Is Not Permanent Authority**
7. **Host-Owned Execution**
8. **Building a Governed AI Tool Gateway in ASP.NET Core**
9. **ASP.NET Core Middleware Ordering**
10. **Writing ADRs That Future Maintainers Can Actually Use**

Together, these establish the core teaching language for the repository while connecting naturally to both working ASI Backbone projects.

---

## Add an Idea

If you have a question that made you stop and think, it may be a tutorial.

If an architecture works but its reasoning is difficult to explain, it may be a tutorial.

If two reasonable approaches disagree, that may be an especially useful tutorial.

> **Read it. Run it. Question it. Improve it.**
