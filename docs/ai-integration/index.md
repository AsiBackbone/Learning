---
description: Explore AI integration patterns that let models propose useful actions while the host retains context, governance, authorization, and execution control.
---

# AI Integration

The AI Integration section explores how AI-assisted systems can propose useful actions without automatically receiving authority to perform them.

The central principle is:

> **The model may propose. The host retains execution authority.**

This separation allows AI inference to participate in an application workflow without treating model output as authorization, policy, or execution authority.

> **Section status:** The foundational AI gateway and focused architecture pages are published, covering typed proposals, probabilistic inputs, governance observability and end-to-end decision tracing, multi-tool recovery, bounded rejection and uncertainty, persistent memory, and experimental multi-agent execution boundaries.

## Proposal Is Not Execution

An AI system may produce a tool call, function call, workflow request, or other proposed operation.

That proposal should not automatically become a real-world side effect.

A governed AI-assisted flow may look like:

```text
User Request
   ↓
AI Inference
   ↓
Tool Proposal
   ↓
Host Validation
   ↓
Authoritative Policy Context
   ↓
Governance Decision
   ↓
Acknowledgment when required
   ↓
Scoped Capability
   ↓
Execution-Boundary Validation
   ↓
Host-Owned Tool Execution
   ↓
Audit Residue
```

The model may help determine what operation should be proposed.

The host determines whether that proposal is valid, whether policy permits it, what authority should exist, and whether the operation ultimately executes.

## Start with the Foundational Sequence

The AI integration model builds on the earlier governed-execution tutorials.

### 1. Decision Before Execution

[Decision Before Execution](../tutorials/decision-before-execution.md)

Introduces the separation between proposed intent, governance evaluation, and real-world execution.

### 2. Policy Context and Explicit Decision Outcomes

[Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md)

Explores authoritative context, constraints, explicit governance outcomes, reason codes, and policy identity.

### 3. Acknowledgment and Audit Residue

[Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md)

Examines workflows that pause for acknowledgment and preserve evidence of the governed decision path.

### 4. Scoped Capability and Host-Owned Execution

[Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md)

Explores narrow, short-lived execution authority and validation at the execution boundary.

Then continue with the foundational AI composition tutorial.

### 5. Governed AI Tool Gateway

[Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md)

This tutorial composes the earlier patterns into an end-to-end AI-assisted execution gateway.

### Focused AI Boundary: Typed AI Proposed Intent and Schema-Validation Boundaries

[Typed AI Proposed Intent and Schema-Validation Boundaries](typed-ai-proposed-intent-and-schema-validation-boundaries.md)

This tutorial isolates the proposal-translation boundary: untrusted model output, parsing, schema validation, typed proposed intent, authoritative host facts, and the rule that successful parsing does not create authority.

### Governance / AI Bridge: Deterministic and Probabilistic Inputs in Policy Evaluation

[Deterministic and Probabilistic Inputs in Policy Evaluation](../governance/deterministic-and-probabilistic-inputs-in-policy-evaluation.md)

This advanced tutorial distinguishes host-authoritative deterministic facts from model-derived or statistical signals and shows how provenance, uncertainty, confidence, calibration, threshold policy, freshness, and host-owned execution remain explicit.

### Observability: AI Governance Observability and End-to-End Decision Tracing

[AI Governance Observability and End-to-End Decision Tracing](ai-governance-observability-and-end-to-end-decision-tracing.md)

This advanced tutorial shows how correlation IDs, proposal IDs, trace/span relationships, structured events, decision reason codes, policy provenance, acknowledgment, scoped authority, executor invocation, and audit residue can be connected without allowing telemetry to become authorization or execution authority. The companion Governed AI Tool Gateway sample includes deterministic allowed, denied, and acknowledgment-required traces that can be inspected locally without a real AI service or telemetry backend.

### Multi-Step AI Composition: Governed Multi-Tool Workflows and Recovery Boundaries

[Governed Multi-Tool Workflows and Recovery Boundaries](governed-multi-tool-workflows-and-recovery-boundaries.md)

This advanced tutorial extends the single-proposal gateway into multi-step execution: whole-plan validation without whole-plan authorization, current per-step policy evaluation, step-scoped authority, result observation, drift handling, bounded replanning, partial failure, idempotency, compensation, cancellation, and recovery.

### Rejection and Uncertainty: AI Proposal Rejection, Uncertainty, and Recovery Patterns

[AI Proposal Rejection, Uncertainty, and Recovery Patterns](ai-proposal-rejection-uncertainty-and-recovery-patterns.md)

This advanced architecture explanation classifies proposal failures by stage, distinguishes invalid output from policy rejection, preserves low-confidence signals as uncertain evidence, and shows how host-owned retry budgets, loop detection, safe model feedback, replanning, escalation, and terminal states keep recovery from weakening the original boundary.

### Persistent Context: Agent Memory and Governance Boundaries

[Agent Memory and Governance Boundaries](agent-memory-and-governance-boundaries.md)

This advanced architecture explanation treats remembered information as scoped, provenance-carrying advisory context rather than current authority. It covers memory writes and reads, source identity, freshness, tenant and agent isolation, stale and conflicting values, persistent prompt-injection effects, retention and forgetting, and the rule that current host facts and current policy still control consequential execution.

## Host-Owned Tool Registry

For a direct comparison of model-visible tool lists, framework registration, per-agent permissions, host-side authorization, and capability-scoped execution, see [Agent and Tool Authorization Models and Host-Owned Execution](../architecture/agent-and-tool-authorization-models-and-host-owned-execution.md).

The comparison also explains when framework-native tool controls are already sufficient and when a separate execution-authority boundary protects a meaningful trust or delegation boundary.

A model should not define its own unrestricted execution surface.

The host can instead expose a deliberately constrained set of operations:

```text
Host Tool Registry
   │
   ├── notification.send
   ├── account.disable
   └── case.archive
```

The model may propose one of those operations.

The host remains responsible for determining whether:

* The requested tool exists.
* The model is permitted to propose it in the current workflow.
* The supplied arguments are valid.
* The destination or resource is acceptable.
* Governance policy permits the operation.
* Additional acknowledgment is required.
* Appropriate execution authority has been established.

## Prompt Guidance Is Not Enforcement

Prompt instructions can influence model behavior.

They should not be treated as the only enforcement boundary for consequential operations.

For example:

```text
"Do not delete protected records."
```

may provide useful guidance to a model.

A host-side rule that prevents protected records from reaching a deletion executor provides a stronger architectural boundary.

The distinction is:

```text
Prompt Instruction
   ↓
Influences Proposal

Host Policy
   ↓
Controls Execution
```

Both may be useful.

They do not serve the same purpose.

## Keep Context Authoritative

AI-generated information may contribute to a proposal, but consequential policy decisions should distinguish model-provided information from authoritative host-owned facts.

For example, a model may suggest:

```text
Resource sensitivity = Low
```

while the host's actual record indicates:

```text
Resource sensitivity = Restricted
```

The host-owned value should remain authoritative for the governance decision.

This protects the policy boundary from depending entirely on model interpretation or prompt behavior.

## Validate Arguments Before Governance and Execution

A recognized tool name is not sufficient.

Its arguments may still contain:

* Invalid identifiers
* Unexpected destinations
* Out-of-range values
* Unsupported options
* Protected resources
* Malformed data
* Attempts to broaden the operation

Argument validation should therefore remain an explicit host responsibility.

```text
Tool Proposal
   ↓
Tool Recognition
   ↓
Argument Validation
   ↓
Policy Evaluation
   ↓
Execution Consideration
```

Invalid proposals should fail before execution.

## Prefer Narrow Semantic Operations

Where practical, expose narrow operations that describe business intent.

Prefer interfaces such as:

```text
notification.send
account.disable
case.archive
```

over broadly privileged primitives such as:

```text
execute_shell
run_sql
invoke_arbitrary_method
```

unless the broader primitive is specifically the subject of the architecture being studied.

Narrow semantic operations make it easier to:

* Validate intent.
* Apply policy.
* Restrict authority.
* Audit outcomes.
* Test failure boundaries.
* Reason about consequences.

## Keep Secrets Host-Owned

An AI model does not need infrastructure credentials merely because it proposes an operation.

Where practical:

```text
Model
   ↓
Proposal

Host
   ↓
Validation
   ↓
Policy
   ↓
Credentials
   ↓
Execution
```

This preserves a clearer trust boundary and reduces unnecessary exposure of secrets, tokens, connection strings, or external-system authority.

## Scoped Execution Authority

An allowed governance decision does not necessarily imply broad standing permission.

Where the architecture benefits from a capability boundary, authority may be constrained by:

* Actor
* Operation
* Resource
* Audience
* Policy version
* Acknowledgment reference
* Expiration
* Intended use

The execution host can validate those bindings immediately before performing the operation.

See:

[Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md)

## Human Acknowledgment

Some AI-proposed operations may require an explicit human acknowledgment before proceeding.

Acknowledgment should not silently become a policy override.

A typical flow is:

```text
AI Proposal
   ↓
Governance Decision
   ↓
Require Acknowledgment
   ↓
Human Response
   ↓
Re-evaluation
   ↓
Scoped Authority
   ↓
Host-Owned Execution
```

This preserves the distinction among:

```text
Acknowledgment
≠
Authorization
≠
Execution Authority
```

See:

[Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md)

## Dry-Run First

AI-assisted execution is often easier to evaluate safely when the first implementation does not immediately perform external side effects.

A useful initial pattern is:

```text
AI Proposal
   ↓
Host Validation
   ↓
Governance Decision
   ↓
Capability Validation
   ↓
WouldExecute = true
```

This allows developers to observe:

* What the model proposes.
* Which proposals fail validation.
* Which policy decisions occur.
* Whether acknowledgment is triggered.
* Which execution authority would be issued.
* What audit residue would be preserved.

Real external execution can be introduced after the boundaries are understood and tested.

## AI Integration Topics

Current and future material in this section examines:

* Tool and function calling
* Tool registries and allowlists
* Argument validation
* Semantic versus broad execution primitives
* Prompt guidance versus enforcement
* Model-generated versus host-authoritative context
* Deterministic versus probabilistic policy inputs
* Confidence, calibration, and model-signal provenance
* Human acknowledgment
* Scoped execution authority
* Secret isolation
* Destination and egress control
* Replay and idempotency
* Dry-run execution
* Governance observability and end-to-end decision tracing
* Agent orchestration boundaries
* Multi-step workflows
* Agent memory provenance, scope, freshness, and retention
* Failure handling
* AI-specific threat modeling
* Alternative gateway architectures

## Working Implementation Reference

The primary governance implementation reference is:

[AsiBackbone/AsiBackbone](https://github.com/AsiBackbone/AsiBackbone)

Learning uses intentionally smaller examples to expose the architectural reasoning while the implementation repository demonstrates fuller governance and policy-control behavior.

For broader ASP.NET Core application architecture, see:

[AsiBackbone/NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate)

## Scope and Boundaries

The patterns described here are educational architectural guidance.

They do not establish:

* AI safety certification
* Security certification
* Regulatory compliance
* Legal conformity
* Correctness of model output
* Guaranteed prevention of harmful actions
* Suitability for every application

Production AI-assisted systems remain responsible for their own threat models, authentication, authorization, data protection, infrastructure, model selection, operational controls, and application-specific safety requirements.

## Current Status

The foundational AI integration tutorial and focused architecture material are published, including typed proposal boundaries, deterministic/probabilistic policy inputs, governance observability and end-to-end decision tracing, multi-tool workflow recovery, bounded rejection and uncertainty, agent-memory governance, and experimental multi-agent execution boundaries.

Future work can deepen this section through executable companion samples, hands-on labs, threat-model exercises, alternative gateway designs, and additional agent and tool-execution scenarios.

For the focused model-output acceptance boundary, continue with:

[Typed AI Proposed Intent and Schema-Validation Boundaries](typed-ai-proposed-intent-and-schema-validation-boundaries.md)

For the probabilistic model-signal boundary, continue with:

[Deterministic and Probabilistic Inputs in Policy Evaluation](../governance/deterministic-and-probabilistic-inputs-in-policy-evaluation.md)

For end-to-end proposal, governance, execution, and evidence tracing, continue with:

[AI Governance Observability and End-to-End Decision Tracing](ai-governance-observability-and-end-to-end-decision-tracing.md)

For multi-step execution, replanning, and recovery boundaries, continue with:

[Governed Multi-Tool Workflows and Recovery Boundaries](governed-multi-tool-workflows-and-recovery-boundaries.md)

For persistent context, memory provenance, scope, freshness, and current-authority reconstruction, continue with:

[Agent Memory and Governance Boundaries](agent-memory-and-governance-boundaries.md)

For the complete end-to-end execution path, continue with:

[Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md)

---

> **Read it. Run it. Question it. Improve it.**

