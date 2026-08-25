---
description: Study realistic, simulated reference architectures that compose multiple ASI Backbone Learning boundaries without prescribing a production framework or package design.
---

# Reference Architecture Case Studies

Reference Architecture Case Studies show how several Learning patterns can coexist inside one realistic scenario.

They are **educational architecture specimens**, not production blueprints. Each study keeps the architectural responsibilities visible while deliberately leaving product selection, deployment topology, persistence technology, credential management, and operational ownership open to the host application.

Use a case study when the individual tutorials make sense on their own but you want to answer the harder question:

> **What does this architecture look like when several of these responsibilities exist in one application?**

## How to Read a Case Study

Each case study separates six concerns that are easy to collapse when a diagram becomes implementation code:

| Concern | Question |
| --- | --- |
| Architecture | Which components and trust or lifecycle boundaries exist? |
| Implementation | How might the example be represented in .NET without prescribing one framework? |
| Operations | Who deploys, monitors, retries, and supports the application? |
| Security | Who authenticates actors, protects credentials, and enforces trust boundaries? |
| Governance | Who establishes policy and produces the decision? |
| Execution | Which component actually performs the protected side effect? |

The studies prefer fictional, simulated, or dry-run consequential operations unless a real integration materially improves the lesson.

## Available Case Studies

### [Governed Administrative Operation](governed-administrative-operation.md)

Follow a fictional `account.disable` request from authenticated administrative access through authoritative context, policy evaluation, explicit outcomes, acknowledgment or escalation, scoped execution authority, host-owned execution, and correlated evidence.

The case includes both an allowed path and an escalated path, with explicit decision reason codes, policy identity, and executor-invocation evidence.

### [Sensitive-Data Access Decision](sensitive-data-access-decision.md)

Compare ordinary low-sensitivity access with a fictional `records.export` operation that requires current resource classification, tenant and purpose context, destination approval, policy evaluation, narrow resource-specific authority, and sensitive-data-safe evidence.

The case includes acknowledgment, escalation, denial, short-lived export authority, zero-executor blocked paths, data minimization, safe logging, provenance, and partial-failure considerations without exposing real protected information.

### [Deployment Approval and Infrastructure Change Gates](deployment-approval-and-infrastructure-change-gates.md)

Compare application deployment and infrastructure-change variants where build or plan evidence, environment policy, human approval, short-lived authority, credential custody, and execution remain separate responsibilities.

The case demonstrates artifact- and plan-bound approvals, separation of duties, expiry and freshness checks, plan-versus-apply, synthetic executors, rollback responsibility, and zero-executor blocked paths without connecting to a real deployment target or cloud provider.

### [AI-Assisted API and Governed Tool Gateway](ai-assisted-api-and-governed-tool-gateway.md)

Compare a conventional human API request with a deterministic fake-model proposal when both ultimately target the same fictional `case.add-note` operation and the same host-owned governance and execution boundary.

The case demonstrates typed proposal validation, unknown-tool and invalid-argument rejection, authoritative host context overriding model hints, acknowledgment, scoped authority, credential custody, end-to-end tracing, and zero executor calls after rejected or denied proposals.

### [Multi-Tenant and Regional Policy Overlay](multi-tenant-and-regional-policy-overlay.md)

Follow a fictional `document.export` operation through authoritative tenant and region resolution, base/regional/tenant/operation policy contributions, explicit composition, policy-set provenance, scoped authority, and host-owned synthetic execution.

The case compares the same operation under two tenants and two regions, surfaces conflicting constraints, preserves policy identity/version/fingerprint evidence, prevents clients from self-selecting a more permissive policy scope, and demonstrates side-effect-free candidate policy simulation.

### [Human Acknowledgment Workflow](human-acknowledgment-workflow.md)

Follow a fictional `accounts.bulk-suspend` operation from an acknowledgment-required policy decision through a durable challenge, explicit human acceptance or decline, expiry and cancellation, current-state re-evaluation, scoped execution authority, and host-owned synthetic execution.

The case demonstrates that acknowledgment is neither approval nor execution authority, preserves decision/challenge/response/grant/execution evidence across time, and proves that an accepted acknowledgment without valid current authority still produces zero protected executor calls.

### [Capability-Scoped Background Operation](capability-scoped-background-operation.md)

Follow a fictional `report.generate` request from an allowed request-time decision through narrow capability issuance, durable job delivery, background-worker validation, replay/expiry/revocation controls, current policy and resource re-evaluation, and host-owned execution.

The case demonstrates that a queue message is not authority, a worker identity is not broad operation authority, altered scope is rejected, delayed execution remains freshness-bound, and operational retry stays distinct from fresh governance.

### [Simulated Robotics-Command Governance Boundary](simulated-robotics-command-governance-boundary.md)

Follow a deterministic fake robot command from planner or AI proposal through semantic command validation, authoritative device and regional context, governance, narrow one-use command authority, simulated gateway enforcement, current local safety checks, and fake physical execution.

The experimental case demonstrates that governance permission is not a robotics safety verdict: an out-of-scope, expired, replayed, or locally unsafe command produces zero simulated movement, and no model output is connected directly to an actuator or production robotics stack.

## What Case Studies Do Not Replace

Case studies do not replace the focused material that explains each boundary independently. Use them as composition maps, then follow the contextual links when one responsibility needs deeper treatment.

For foundational reasoning, begin with [Decision Before Execution](../tutorials/decision-before-execution.md). For hands-on composition, use [Build a Governed API Operation](../labs/build-a-governed-api-operation.md).
