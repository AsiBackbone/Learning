# Governed AI Tool Gateway Sample

This sample is the executable companion to the [Governed AI Tool Gateway tutorial](../../docs/tutorials/governed-ai-tool-gateway.md).

**Learning objective:** Run an end-to-end AI-assisted governance flow where a simulated model may propose a tool action, but the host owns the tool allowlist, authoritative context, policy decision, acknowledgment, scoped capability, dry-run execution, and audit evidence.

**Difficulty:** Advanced
**Prerequisites:** Complete the five foundational tutorials, especially [Scoped Capability and Host-Owned Execution](../../docs/tutorials/scoped-capability-and-host-owned-execution.md).

The central rule is:

> **The model may propose. The host retains execution authority.**

## What This Sample Demonstrates

The sample uses one narrow semantic operation:

```text
notification.send
```

A local `AiToolProposal` stands in for model output. There is no LLM SDK, network call, model credential, or external messaging provider.

The host then performs this flow:

```text
Simulated model proposal
   ↓
Host tool registry
   ↓
Proposal argument validation
   ↓
Host-built authoritative context
   ↓
Governance decision
   ↓
Acknowledgment when required
   ↓
Re-evaluation
   ↓
Short-lived single-use capability
   ↓
Execution-boundary validation
   ↓
Host-owned dry-run handler
   ↓
Audit residue
```

The sample deliberately keeps the proposer and executor separate.

## Project Structure

```text
governed-ai-tool-gateway/
│
├── GovernedAiToolGateway/
│   ├── GovernedAiToolGateway.csproj
│   └── Program.cs
│
├── GovernedAiToolGateway.Tests/
│   ├── GovernedAiToolGateway.Tests.csproj
│   └── GovernedGatewayTests.cs
│
└── README.md
```

The executable project contains the teaching implementation. The sibling xUnit project verifies the architectural invariants.

## Run the Sample

From the repository root:

```bash
dotnet run --project samples/governed-ai-tool-gateway/GovernedAiToolGateway/GovernedAiToolGateway.csproj
```

The console application runs representative scenarios including:

- An unknown model-proposed tool.
- An external recipient where the model incorrectly claims the destination is internal.
- The same external proposal after valid host-owned acknowledgment.
- A normal internal-recipient dry run.

No real notification is sent.

A successful execution path reports:

```text
WouldExecute = true
```

instead of performing an external side effect.

## Run the Tests

Run the focused tests:

```bash
dotnet test samples/governed-ai-tool-gateway/GovernedAiToolGateway.Tests/GovernedAiToolGateway.Tests.csproj
```

Or run the complete Learning sample suite:

```bash
dotnet test samples/Samples.slnx
```

## The Simulated Model Is Only a Proposer

The sample intentionally does **not** connect to an actual model provider.

An `AiToolProposal` contains:

```text
ProposalId
ModelId
ToolName
Arguments
ModelRationale
```

Creating a proposal has no side effect.

The proposal may suggest:

```text
notification.send
recipient = partner@example.net
template = case-update
classification = internal
```

The host does not treat the model-supplied classification as authoritative.

## Tool Allowlisting Is Host-Owned

The host registers only:

```text
notification.send
```

A proposal such as:

```text
finance.transfer_unlimited
```

returns:

```text
tool.unknown
```

before policy evaluation or execution.

The model cannot expand the executable surface by inventing a tool name.

## Proposal Arguments Cross a Validation Boundary

The registered tool requires:

```text
recipient
template
```

Missing or unexpected arguments are rejected before they become execution inputs.

For teaching purposes, `classification` is accepted as a proposal field so the trust boundary can be demonstrated. The host still ignores that claim when building authoritative policy context.

This distinction matters:

```text
Model may propose a fact
   ≠
Host accepts that fact as authoritative
```

## Authoritative Context Comes from the Host

The host reconstructs destination classification from `RecipientDirectory`.

For example:

```text
Proposal claims:
classification = internal

Recipient:
partner@example.net

Host classification:
External
```

Policy receives the host classification.

It does not receive the model's security-sensitive claim as truth.

The sample records a `context.host-authoritative` audit entry so this boundary is observable.

## Policy Outcomes Remain Explicit

The sample policy demonstrates:

```text
Internal destination
   ↓
Allowed

External destination
   ↓
AcknowledgmentRequired

Blocked domain
   ↓
Denied

Unknown destination classification
   ↓
Deferred
```

These states are not collapsed into one boolean.

## Acknowledgment Is Not an Override

For an external recipient, the initial decision is:

```text
AcknowledgmentRequired
```

The host creates a challenge bound to:

- Actor.
- Operation.
- Recipient.
- Reason code.
- Time window.

If the actor accepts the challenge, the host records the acknowledgment identity in policy context and **re-evaluates** the decision.

The sequence is:

```text
Decision = AcknowledgmentRequired
   ↓
Host challenge
   ↓
Actor accepts
   ↓
Satisfied acknowledgment recorded
   ↓
Policy re-evaluated
   ↓
Allowed or blocked
```

The acknowledgment does not directly invoke the tool.

A response from the wrong actor is rejected.

## Scoped Capability Preserves Narrow Authority

After an allowed decision, the host issues a capability bound to:

```text
Issuer
Audience
Actor
Operation
Recipient
Required scope
Policy version
Acknowledgment identity when applicable
Issued time
Two-minute expiration
Maximum uses = 1
```

The capability is metadata describing narrow authority.

It does not execute itself.

## Validation Happens at the Execution Boundary

Immediately before the handler, the gateway verifies the current context against the capability.

The tests prove rejection for important boundary failures including:

- Changed recipient.
- Expired capability.
- Replay of the same capability identity.

A valid capability is atomically consumed in the sample's in-memory use store before the dry-run handler is reached.

The sample's in-memory replay state demonstrates the concept only. It does **not** provide durable or distributed replay protection.

## The Host Owns the Handler and Credential Boundary

The simulated model has no provider credential.

The recording handler contains a placeholder host-owned credential reference to make ownership visible:

```text
Model proposal
   ↓
No infrastructure secret

Host handler
   ↓
Owns provider credential boundary
```

The sample never exposes a real secret.

A production system would keep actual credentials in an appropriate secret-management and execution environment rather than in source code.

## Dry-Run Execution

The final handler is a `RecordingNotificationHandler`.

It records an invocation and returns:

```text
WouldExecute = true
```

It does **not**:

- Send email.
- Call a messaging API.
- Make a network request.
- Modify external state.

This keeps the side-effect boundary visible without requiring production infrastructure.

## Audit Continuity

The sample records separate evidence stages such as:

```text
context
decision
acknowledgment
re-evaluation
capability-issued
capability-validation
capability-consumption
execution
```

All events for one proposal use the same correlation identifier.

This preserves the distinction between:

```text
Decision allowed
Capability issued
Capability validated
Capability consumed
Would execute
```

Those are related events, not one event.

## Governance Observability and Decision Tracing

The executable also runs three deterministic observability scenarios after the baseline gateway demonstration:

1. An allowed internal proposal that reaches the dry-run executor.
2. A denied blocklisted proposal that produces zero executor calls.
3. An external proposal that pauses for acknowledgment before re-evaluation and scoped authority.

The observability layer uses the .NET `ActivitySource` API and an in-process listener. No model provider, OpenTelemetry exporter, collector, or external backend is required.

The trace keeps these identities visible:

```text
Host correlation ID
Proposal ID
Trace ID
Span ID / parent span ID
Decision outcome + reason code
Policy version where recorded
Acknowledgment challenge identity
Capability identity
Executor invocation
Audit residue
```

The sample deliberately preserves this distinction:

> **Telemetry records what happened. Telemetry does not authorize what may happen.**

`InMemoryAuditSink` continues to represent the governance-evidence path. While a trace is active, each audit write is also projected as an `ActivityEvent` so the same lifecycle can be inspected through trace relationships without making telemetry the source of authority.

The host-owned handler creates the `executor.invoke` activity at the actual dry-run side-effect boundary. Therefore a denied trace can demonstrate all three negative observations together:

```text
Decision = Denied
Capability-issued audit stage = absent
executor.invoke activity = absent
Executor invocation count = 0
```

The companion tutorial explains correlation, structured events, policy provenance, sampling, sensitive-data minimization, and the boundary between operational telemetry and durable governance evidence:

[AI Governance Observability and End-to-End Decision Tracing](../../docs/ai-integration/ai-governance-observability-and-end-to-end-decision-tracing.md)

## Important Invariants

The focused test project verifies that:

1. An unknown tool is rejected and never reaches the handler.
2. Missing required arguments are rejected.
3. An internal recipient can reach the dry-run handler exactly once.
4. Model-supplied destination classification cannot override host classification.
5. A blocked domain is denied without acknowledgment or execution.
6. An unclassified destination is deferred.
7. Rejected acknowledgment does not become execution authority.
8. An acknowledgment from the wrong actor is rejected.
9. Changing the recipient after acknowledgment requires a new recipient-bound acknowledgment.
10. Valid external acknowledgment causes re-evaluation before execution authority is issued.
11. Changing the recipient after approval invalidates the capability.
12. An expired capability is rejected at the execution boundary.
13. Replaying the same capability identity cannot invoke the handler twice.
14. A successful flow preserves one correlation identifier across evidence stages.

These tests make the sample's architectural contract executable.

## Prompt Text Is Not the Execution Boundary

A model prompt could say:

```text
Only send notifications to approved destinations.
```

That may improve model behavior.

The sample does not rely on the prompt for enforcement.

The host still checks:

```text
Registered tool
Arguments
Destination classification
Policy
Acknowledgment
Capability
Replay state
```

A prompt may influence the proposal.

It does not grant execution authority.

## What the Sample Intentionally Omits

This teaching sample does not claim to provide production implementations of:

- Authentication.
- Authorization.
- Real model integration.
- Model sandboxing.
- Prompt-injection prevention.
- Cryptographic capability proof.
- Durable replay storage.
- Distributed atomic consumption.
- Capability revocation.
- Production secrets management.
- DLP enforcement.
- External network controls.
- Real message delivery.
- Idempotency across an external provider.
- Durable audit storage.
- Compliance controls.

Those remain host and environment responsibilities.

The sample exists to make the **ordering and ownership of authority** observable.

## Production-Oriented References

Compare the small teaching implementation with the fuller working `AsiBackbone` repository:

- [AI Agent Gateway Scenario](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/scenarios/ai-agent-gateway.md)
- [Human Approval Before AI Tool Execution](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/scenarios/human-approval-before-ai-tool-execution.md)
- [GovernanceDecision](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecision.cs)
- [AuditResidue](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Audit/AuditResidue.cs)
- [CapabilityTokenGrant](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityTokenGrant.cs)
- [Capability Grant Hardening](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/capability-grant-hardening.md)
- [`AsiBackbone.OpenTelemetry` README](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.OpenTelemetry/README.md)
- [`OpenTelemetryGovernanceInstrumentation`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.OpenTelemetry/OpenTelemetryGovernanceInstrumentation.cs)
- [`OpenTelemetryGovernanceAttributes`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.OpenTelemetry/OpenTelemetryGovernanceAttributes.cs)

The Learning sample remains framework-neutral so the architectural pattern can be studied independently of package adoption.

## Continue with the Lab

After the baseline behavior is clear, continue with the [Governed AI Tool Gateway advanced lab](../../docs/labs/governed-ai-tool-gateway.md).

The lab asks you to deliberately weaken proposal validation, authoritative context, acknowledgment, capability, replay, prompt, and credential boundaries, then threat-model the resulting gateway.

---

> **Read it. Run it. Question it. Improve it.**
