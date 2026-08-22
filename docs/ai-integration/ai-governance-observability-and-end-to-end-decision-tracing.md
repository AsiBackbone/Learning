---
description: Trace AI-generated proposals through validation, governance, acknowledgment, scoped authority, host-owned execution, and audit evidence without treating telemetry as authorization.
---

# AI Governance Observability and End-to-End Decision Tracing

**Learning objective:** Understand how to correlate an AI-generated proposal through host validation, policy context, governance decisions, acknowledgment, scoped authority, execution, and audit evidence while keeping telemetry observational rather than authoritative.

**Pattern classification:** General learning material

**Difficulty:** Advanced

**Prerequisites:** [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md), [Typed AI Proposed Intent and Schema-Validation Boundaries](typed-ai-proposed-intent-and-schema-validation-boundaries.md), [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md), [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md), and familiarity with [Structured Logging Without Sensitive-Data Sprawl](../aspnetcore/structured-logging-without-sensitive-data-sprawl.md).

## Pattern Card

> **Problem:** A governed AI system may preserve the execution boundary correctly while still be difficult to diagnose. After a model proposes an operation, developers and operators need to reconstruct what happened without treating telemetry as permission.
>
> **Pattern:** Carry stable proposal and correlation identifiers through host-owned validation, context construction, governance, acknowledgment, scoped authority, execution, and evidence. Use trace/span relationships, structured events, stable reason codes, and separate audit residue to make the path inspectable.
>
> **Use when:** AI-proposed tool execution, agent workflows, human acknowledgment, or scoped-capability execution need enough evidence to reconstruct decisions and verify important architectural invariants.
>
> **Prefer something simpler when:** The model is purely advisory, no consequential side effect exists, or normal request tracing already explains the complete operation without a separate governance lifecycle.
>
> **Observe:** Allowed proposals reach the executor through the expected stages; denied proposals show a decision and evidence but no capability or executor invocation; acknowledgment-required proposals show acknowledgment and re-evaluation before scoped authority is issued.

The central observability boundary is:

> **Telemetry records what happened. Telemetry does not authorize what may happen.**

That distinction is as important as the familiar AI boundary:

> **The model may propose. The host retains execution authority.**

## Why Observability Matters Here

The governed AI flow already separates proposal from execution:

```text
AI proposal
    ↓
Schema / argument validation
    ↓
Host-owned policy context
    ↓
Governance decision
    ↓
Acknowledgment when required
    ↓
Scoped capability
    ↓
Execution-boundary validation
    ↓
Host-owned executor
    ↓
Audit residue
```

Observability adds a parallel evidence path:

```text
Governed workflow
      │
      ├── trace/span relationships
      ├── structured operational events
      └── audit residue / receipt
```

The evidence path should let a learner inspect a statement such as:

```text
Decision = Denied
      ↓
Capability issuance = absent
      ↓
Executor invocation = absent
      ↓
Executor invocation count = 0
```

That is stronger than logging only:

```text
"Request denied."
```

The stronger observation shows where the workflow stopped and which downstream stages never became reachable.

## Keep Identity Layers Distinct

A production trace often contains several identifiers. They are related, but they solve different problems.

| Identifier | Purpose | Typical lifetime |
| --- | --- | --- |
| **Correlation ID** | Stable logical workflow identity across logs, receipts, retries, and asynchronous boundaries. | May outlive one technical trace. |
| **Proposal ID** | Identity of the specific model-generated proposal being evaluated. | Proposal lifecycle. |
| **Trace ID** | Groups related telemetry spans into one technical trace. | One technical execution path. |
| **Span ID** | Identifies one measured operation inside a trace. | One operation. |
| **Reason code** | Stable machine-readable explanation for a governance outcome. | Decision evidence lifecycle. |
| **Acknowledgment identity** | Binds a response to a particular acknowledgment requirement. | Acknowledgment lifecycle. |
| **Capability ID** | Identifies narrow follow-on execution authority. | Capability lifetime. |
| **Audit receipt / residue ID** | Identifies retained governance evidence. | Evidence-retention period. |

A useful mental model is:

```text
Correlation ID
    = logical workflow continuity

Trace ID
    = technical telemetry continuity
```

Do not overload one value merely because it is convenient.

A human acknowledgment may occur long after the original model proposal. The logical workflow may need to retain its correlation and proposal identity even if technical tracing resumes under a different process, host, or trace.

The companion sample demonstrates separate proposal and host correlation identifiers while keeping the trace tied to both.

## A Practical Trace Shape

A compact host-owned trace might look like:

```text
ai.governance.workflow
│
├── model.inference
├── host.governance-gateway
│   ├── event: proposal-validation
│   ├── event: context
│   ├── event: decision
│   ├── event: capability-issued
│   ├── event: capability-validation
│   ├── event: capability-consumption
│   ├── executor.invoke
│   └── event: execution
└── correlated audit residue
```

For an acknowledgment-required path, the same logical workflow may contain two gateway passes:

```text
ai.governance.workflow
│
├── model.inference
├── host.governance-gateway
│   ├── event: proposal-validation
│   ├── event: context
│   ├── event: decision = AcknowledgmentRequired
│   └── event: acknowledgment = required
├── acknowledgment.respond
└── host.governance-gateway
    ├── event: proposal-validation
    ├── event: context
    ├── event: decision = AcknowledgmentRequired
    ├── event: acknowledgment = accepted
    ├── event: re-evaluation = Allowed
    ├── event: capability-issued
    ├── event: capability-validation
    ├── event: capability-consumption
    ├── executor.invoke
    └── event: execution
```

The exact activity and event names are teaching vocabulary, not a proposed standard.

The important properties are:

- Model inference produces a proposal, not authority.
- Validation happens before governance or execution.
- Authoritative context comes from the host.
- The decision remains explicit.
- A denied path stops before capability issuance and execution.
- Acknowledgment does not bypass re-evaluation.
- Scoped authority appears only after an executable decision.
- Execution-boundary checks still precede the host-owned executor.
- Audit evidence remains separate from transient telemetry.

## Trace Status Is Not Governance Status

A governance denial is not automatically a technical trace failure.

Consider:

```text
Policy evaluated successfully
Decision = Denied
Executor calls = 0
```

The system may have behaved exactly as designed.

Do not mechanically map:

```text
Denied
    ↓
ActivityStatusCode.Error
```

unless denial represents a technical failure in that particular host.

A better separation is:

```text
Technical status
    = did the software operation complete as designed?

Governance outcome
    = what decision did policy produce?
```

Store the governance outcome as structured metadata such as:

```text
governance.decision.outcome = Denied
governance.reason_code = notification.destination-blocked
```

Then reserve technical error status for exceptions, unavailable dependencies, malformed instrumentation, or other genuine operational failures.

## What Belongs in Telemetry

Useful low-risk fields may include:

```text
governance.correlation_id
ai.proposal.id
ai.model.id
tool.name
governance.policy.version
governance.stage
governance.outcome
governance.reason_code
acknowledgment.challenge_id
capability.id
execution.invocation_count
```

Avoid automatically attaching:

- Full prompts.
- Full model completions.
- Tool argument payloads.
- Credentials or tokens.
- Connection strings.
- Raw personal information.
- Protected-resource contents.
- Human-readable decision text containing sensitive data.

Prefer stable reason codes such as:

```text
notification.destination-blocked
```

over telemetry such as:

```text
Denied because jane.doe@example.com belongs to investigation case 1234.
```

The first form remains useful for grouping and diagnostics without unnecessarily widening the telemetry data boundary.

## Structured Logging and Activity Events

Structured logs and trace activity events can carry similar fields:

```text
EventName = GovernanceDecision
CorrelationId = corr-123
ProposalId = proposal-456
Outcome = Denied
ReasonCode = notification.destination-blocked
PolicyVersion = 5.0
```

The representation is less important than preserving stable field meaning.

A host may choose to emit the same decision boundary through:

- `ILogger` structured events,
- `Activity` tags and events,
- metrics,
- durable audit records,
- or a combination of these.

Do not assume that because the same fields appear in two destinations, the destinations have the same trust, retention, integrity, or authorization role.

Operational telemetry is usually optimized for diagnosis and aggregation.

Governance evidence may require stronger retention, durability, access control, lineage, or integrity guarantees.

## OpenTelemetry Concepts Without an Exporter Dependency

The companion sample uses `System.Diagnostics.ActivitySource` directly.

That gives the learner local visibility into:

- trace IDs,
- span IDs,
- parent/child relationships,
- activity tags,
- activity events.

No OpenTelemetry exporter or external backend is required.

This is useful because `.NET ActivitySource` is the instrumentation primitive commonly consumed by OpenTelemetry pipelines, but the architectural lesson remains visible without adding infrastructure to the teaching sample.

A production host could later subscribe through OpenTelemetry and export to its chosen backend.

The instrumentation still does not become a governance engine.

## Decision Provenance

A trace that says only:

```text
Decision = Denied
```

is incomplete when policy versions can change.

Useful decision provenance may include:

```text
PolicyVersion = 5.0
ReasonCode = notification.destination-blocked
CorrelationId = corr-123
ProposalId = proposal-456
```

A fuller system may additionally preserve policy hashes, constraint identities, overlay versions, model-signal provenance, or other source evidence.

The amount of provenance should match the consequence and operational requirements.

The important boundary is that provenance describes **why the decision happened**. It does not itself authorize the next action.

## Three Traces to Inspect

### 1. Allowed Proposal

The deterministic fake model proposes an internal notification.

Expected path:

```text
model.inference
    ↓
proposal-validation = valid
    ↓
context = host-authoritative
    ↓
decision = Allowed
    ↓
capability-issued
    ↓
capability-validation = valid
    ↓
capability-consumption = consumed
    ↓
executor.invoke
    ↓
execution = would-execute
```

Observable invariant:

```text
Executor invocation count = 1
```

### 2. Denied Proposal

The model proposes a destination on the host blocklist.

Expected path:

```text
model.inference
    ↓
proposal-validation = valid
    ↓
context = host-authoritative
    ↓
decision = Denied
    ↓
stop
```

Observable invariant:

```text
Capability-issued event = absent
Executor span = absent
Execution audit stage = absent
Executor invocation count = 0
```

This is the most important negative trace in the tutorial.

A system that logs `Denied` but still invokes the executor has not preserved the architecture.

### 3. Acknowledgment Before Scoped Authority

The model proposes an external destination.

Expected path:

```text
Initial decision = AcknowledgmentRequired
    ↓
acknowledgment required
    ↓
actor responds
    ↓
acknowledgment accepted
    ↓
policy re-evaluated
    ↓
Allowed
    ↓
capability issued
    ↓
capability validated
    ↓
executor.invoke
```

The ordering matters.

The trace should not show:

```text
capability issued
    ↓
acknowledgment accepted
```

because that would mean execution authority existed before the required responsibility boundary had been satisfied.

## Why the Companion Sample Uses a Fake Model

The sample intentionally uses deterministic fake model output.

That keeps the lesson focused on:

```text
proposal identity
    ↓
validation
    ↓
governance
    ↓
execution boundary
    ↓
observability
```

rather than:

- provider credentials,
- network variability,
- model rate limits,
- prompt tuning,
- stochastic output,
- model-provider SDK behavior.

The fake model is only a proposer.

Its output still crosses the same host-owned validation and governance boundaries.

## Telemetry Must Not Become Authority

Avoid designs such as:

```text
Telemetry says Allowed
    ↓
Executor runs
```

or:

```text
SIEM alert cleared
    ↓
Operation authorized
```

unless a deliberately designed, authenticated, authoritative control-plane workflow exists and is independently governed.

Ordinary observability data should remain observational.

A telemetry backend is typically not the right place to reconstruct authorization state or mint capability authority.

The safer default is:

```text
Governance / authorization state
    ↓
controls execution

Telemetry
    ↓
records the result
```

## Sampling and the Evidence Boundary

Distributed tracing systems may sample.

A trace can therefore disappear even when a governed operation occurred.

That creates an important distinction:

```text
Trace missing
    ≠
Operation did not happen
```

Do not rely on sampled telemetry as the only audit mechanism for consequential actions.

Likewise:

```text
Trace present
    ≠
Audit record is durable or tamper-evident
```

The companion sample keeps `InMemoryAuditSink` separate from trace collection for exactly this reason.

Both are in-memory teaching artifacts, but they represent different responsibilities.

## Failure and Unavailable Telemetry

An observability exporter may be unavailable while governance and execution controls remain healthy.

Possible host policies include:

- Continue execution but retain local durable evidence.
- Buffer telemetry for later export.
- Reduce diagnostic detail while preserving required audit evidence.
- Fail closed only when an explicit requirement says telemetry delivery itself is a prerequisite.

Do not accidentally create:

```text
Exporter unavailable
    ↓
Skip governance
```

or:

```text
Exporter unavailable
    ↓
Execute without evidence even though evidence is mandatory
```

The required behavior belongs in host policy and operational design.

## Working `AsiBackbone.OpenTelemetry` Reference

The working `AsiBackbone` repository contains an `AsiBackbone.OpenTelemetry` package that adapts provider-neutral governance emission envelopes into OpenTelemetry-friendly .NET diagnostics primitives.

Useful implementation references include:

- [`AsiBackbone.OpenTelemetry` README](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.OpenTelemetry/README.md)
- [`OpenTelemetryGovernanceInstrumentation`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.OpenTelemetry/OpenTelemetryGovernanceInstrumentation.cs)
- [`OpenTelemetryGovernanceAttributes`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.OpenTelemetry/OpenTelemetryGovernanceAttributes.cs)

The working package exposes activity events/tags, metrics, stable governance attributes, trace/span identifiers, decision metadata, lifecycle information, and emission outcomes.

Its production-oriented boundary is important:

> OpenTelemetry is a projection path, not the authoritative ledger.

The working implementation describes a flow in which durable governance evidence and outbox state precede optional downstream OpenTelemetry export.

The Learning sample is intentionally smaller. It uses `ActivitySource` and an in-memory collector so the trace can be inspected locally without importing the production package or configuring exporters.

## Run the Companion Sample

From the repository root:

```bash
dotnet run --project samples/governed-ai-tool-gateway/GovernedAiToolGateway/GovernedAiToolGateway.csproj
```

The existing gateway scenarios run first.

The observability demonstration then prints the allowed, denied, and acknowledgment-required traces with:

- correlation ID,
- proposal ID,
- final gateway status,
- executor invocation count,
- trace/span relationships,
- audit stages and policy version where recorded.

Run the focused tests with:

```bash
dotnet test samples/governed-ai-tool-gateway/GovernedAiToolGateway.Tests/GovernedAiToolGateway.Tests.csproj
```

The observability tests verify the architectural outcomes rather than a particular exporter.

## Common Failure Modes

### 1. One Identifier for Everything

The proposal ID, workflow correlation ID, trace ID, and audit receipt are treated as interchangeable.

That becomes fragile when retries, asynchronous work, or delayed acknowledgment appear.

### 2. Logs Are Used as Authorization State

A later component reads a log entry saying `Allowed` and treats it as permission to execute.

The execution boundary has moved into the observability system.

### 3. Denied Decisions Still Produce Executor Spans

If a denied proposal shows `executor.invoke`, investigate immediately.

Either the architecture is broken or the instrumentation is attached to the wrong boundary.

### 4. Policy Version Is Missing

An operator can see the outcome but cannot determine which policy semantics produced it.

### 5. Full Prompts and Payloads Are Logged

Telemetry becomes a secondary sensitive-data store.

### 6. Sampling Is Mistaken for Durable Evidence

A missing trace is interpreted as proof that an operation never happened.

### 7. Acknowledgment and Capability Ordering Is Ambiguous

The trace cannot demonstrate that the required acknowledgment occurred before scoped authority was issued.

### 8. Telemetry Failure Broadens Authority

An unavailable exporter causes the host to skip governance or execution checks.

Observability failure should not silently weaken authorization or governance boundaries.

## Tradeoffs

### Benefits

- Decision paths become easier to reconstruct.
- Negative invariants such as zero executor calls become inspectable.
- Correlation between model proposal, governance, acknowledgment, authority, and execution becomes clearer.
- Stable reason codes improve diagnostics and aggregation.
- Policy-version evidence improves provenance.
- Trace relationships make multi-stage and distributed workflows easier to reason about.
- The distinction between operational telemetry and durable governance evidence becomes explicit.

### Costs

- More identifiers and propagation rules.
- Additional instrumentation code.
- Telemetry schemas require versioning discipline.
- Sensitive-data minimization requires design work.
- High-cardinality fields can increase telemetry cost.
- Sampling complicates evidence interpretation.
- Distributed correlation requires careful propagation.
- Over-instrumentation can bury the important boundary in noise.

Use enough telemetry to make the architecture observable, not so much that telemetry becomes the architecture.

## Check Your Understanding

After completing this tutorial and running the sample, you should be able to:

- [ ] Explain why correlation ID, proposal ID, trace ID, and span ID have different responsibilities.
- [ ] Trace an allowed proposal from deterministic model output through validation, policy context, decision, capability, execution, and audit evidence.
- [ ] Demonstrate that a denied proposal produces decision evidence while capability issuance and executor invocation remain absent.
- [ ] Show that acknowledgment acceptance and re-evaluation occur before scoped authority is issued.
- [ ] Explain why a governance denial is not automatically a technical tracing error.
- [ ] Explain why sampled telemetry cannot replace durable governance evidence.
- [ ] Identify fields that are useful for structured observability without copying sensitive prompts, payloads, or credentials into telemetry.
- [ ] Explain why telemetry must remain observational unless a separate authoritative control-plane design explicitly says otherwise.

## Related Content

- [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) — the foundational end-to-end host-owned execution path.
- [Typed AI Proposed Intent and Schema-Validation Boundaries](typed-ai-proposed-intent-and-schema-validation-boundaries.md) — the model-output acceptance boundary.
- [Governed Multi-Tool Workflows and Recovery Boundaries](governed-multi-tool-workflows-and-recovery-boundaries.md) — extends the tracing problem into multiple governed steps, partial failure, replanning, and recovery.
- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) — deeper treatment of responsibility and evidence stages.
- [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) — policy identity, drift, provenance, and freshness.
- [Structured Logging Without Sensitive-Data Sprawl](../aspnetcore/structured-logging-without-sensitive-data-sprawl.md) — operational event design and data minimization.
- [Secure Logging Across Trust Boundaries](../security/secure-logging-across-trust-boundaries.md) — provider, collector, storage, access, retention, and tenant boundaries.
- [Governed AI Tool Gateway sample](https://github.com/AsiBackbone/Learning/blob/main/samples/governed-ai-tool-gateway/README.md) — deterministic local trace and audit demonstration.

---

> **Observe the path. Verify the boundary. Keep authority with the host.**
