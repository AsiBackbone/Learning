---
description: Choose a Learning path based on whether you are a developer, system engineer, architect, platform engineer, AI integrator, or security and compliance reviewer.
---

# Adoption Personas and Entry Points

Learning is intended for several kinds of readers. The architecture is easier to evaluate when each reader starts with the question they actually own.

This page is an educational navigation guide, not a claim that every role should adopt the AsiBackbone package.

## Senior developer

**Typical question:** Can I add a stronger decision boundary without losing control of the application?

Start with:

1. [Decision Before Execution](../tutorials/decision-before-execution.md)
2. [When a Simple Application Service Is Enough](../architecture/when-a-simple-application-service-is-enough.md)
3. [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md)
4. [Executable Samples](../samples/index.md)

Look for clean seams, testability, and whether the extra lifecycle earns its complexity.

## System engineer

**Typical question:** Where does this decision boundary sit operationally, and what must be observable?

Start with:

1. [Intent to Execution: An Accountability Pattern](../architecture/intent-to-execution-accountability-pattern.md)
2. [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md)
3. [AI Governance Observability and End-to-End Decision Tracing](../ai-integration/ai-governance-observability-and-end-to-end-decision-tracing.md)
4. [Durable Decision Ledgers and Cryptographic Audit Chains](../advanced/durable-decision-ledgers-and-cryptographic-audit-chains.md)

Focus on correlation, failure recovery, replay, evidence, and execution reconciliation.

## Enterprise architect

**Typical question:** Which responsibilities should be standardized, and which should remain application-owned?

Start with:

1. [Accountable Systems Infrastructure and Governed Execution](../architecture/accountable-systems-infrastructure-and-governed-execution.md)
2. [Governance Tool Selection and Composition](../architecture/governance-tool-selection-and-composition.md)
3. [Policy Engines, Rules Engines, and Distributed Policy Enforcement](../architecture/policy-engines-rules-engines-and-distributed-policy-enforcement.md)
4. [Federated Governance and Independent Authority Coordination](../advanced/federated-governance-and-independent-authority-coordination.md)

Focus on responsibility boundaries rather than forcing one implementation across every workload.

## Platform engineering team

**Typical question:** Can the pattern become an internal paved road without becoming a monolithic framework?

Start with:

1. [Governance Spine and Capability Validation Diagrams](../architecture/governance-spine-and-capability-validation-diagrams.md)
2. [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md)
3. [Deployment Approval and Infrastructure Change Gates](../case-studies/deployment-approval-and-infrastructure-change-gates.md)
4. [Refactor Scattered Governance Checks](../labs/refactor-scattered-governance-checks.md)

Focus on reusable contracts, safe defaults, extension seams, and escape hatches.

## AI integration architect

**Typical question:** How do I keep model output as a proposal rather than execution authority?

Start with:

1. [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md)
2. [Agent and Tool Authorization Models and Host-Owned Execution](../architecture/agent-and-tool-authorization-models-and-host-owned-execution.md)
3. [Typed AI-Proposed Intent and Schema-Validation Boundaries](../ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md)
4. [AI-Assisted API and Governed Tool Gateway](../case-studies/ai-assisted-api-and-governed-tool-gateway.md)

Focus on authoritative host context, credential custody, bounded retry, and final executor control.

## Security or compliance reviewer

**Typical question:** What evidence exists around a consequential decision, and what does that evidence not prove?

Start with:

1. [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md)
2. [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md)
3. [Signing, Verification, Key Custody, and Tamper Evidence](../security/signing-verification-key-custody-and-tamper-evidence.md)
4. [Governed Execution in Regulated Systems](../advanced/governed-execution-in-regulated-systems.md)

Focus on the difference between evidence contribution and compliance, certification, non-repudiation, or complete security control coverage.

## Public-sector or regulated-system engineer

**Typical question:** How can regional, program, tenant, or regulatory constraints shape decisions without hard-coding every rule into the core application?

Start with:

1. [Regional and Tenant Policy Overlays](../advanced/regional-and-tenant-policy-overlays.md)
2. [Regional Policy and Operational Gateways](../advanced/regional-policy-and-operational-gateways.md)
3. [Governed Execution in Regulated Systems](../advanced/governed-execution-in-regulated-systems.md)
4. [Multi-Tenant and Regional Policy Overlay](../case-studies/multi-tenant-and-regional-policy-overlay.md)

Focus on policy ownership, versioning, local autonomy, evidence, and safe host-owned execution.

## Evaluating the implementation repositories

Learning owns the architecture teaching.

If you decide to evaluate the working implementations:

- [AsiBackbone/AsiBackbone](https://github.com/AsiBackbone/AsiBackbone) documents its own concrete packages, APIs, runtime behavior, compatibility, and security posture.
- [AsiBackbone/NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate) documents its own application-template implementation details.

Do not use a Learning example as a substitute for current product documentation.

---

> **Read it. Run it. Question it. Improve it.**
