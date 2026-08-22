---
description: Learn how multi-agent AI workflows can exchange proposals and delegated work while keeping validation, authority, and execution under host control.
---

# Governed Agent-to-Agent Requests and Multi-Agent Execution Boundaries

**Learning objective:** Understand how proposals, plans, recommendations, and delegated work can move among multiple AI agents without allowing agent-to-agent communication to become implicit execution authority.

**Pattern classification:** General learning material — **Experimental**

**Difficulty:** Advanced

**Prerequisites:** [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md), [Typed AI Proposed Intent and Schema-Validation Boundaries](../ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md), [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md), and [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md).

> **Experimental architecture note:** This article explores design boundaries for multi-agent systems. It does not define a standardized agent-to-agent protocol, autonomous-agent safety guarantee, AGI architecture, or production-ready agent platform.

## At a Glance

> **Problem:** Multi-agent workflows can make recommendations or delegated work appear more authoritative simply because several agents agree, pass requests among themselves, or operate across multiple services.
>
> **Core idea:** Treat agent-to-agent messages as typed proposals, plans, recommendations, or delegation requests; validate each trust-boundary crossing; resolve authoritative facts through the host; and establish execution authority explicitly through host-owned governance and scoped capabilities.
>
> **Why it matters:** Agent agreement, coordination, or delegation does not create authorization by itself, and authority must not silently widen as a request crosses agents, services, or execution boundaries.
>
> **Read this if:** An AI-assisted workflow has multiple agents, multi-hop requests, long-running tasks, cross-service coordination, or delegated steps that can ultimately reach consequential tools or external side effects.

The central learning question is:

> **Does one agent requesting an action from another create authority?**

The answer is:

> **No. Agent-to-agent communication may carry intent, proposals, plans, recommendations, or delegated evidence, but execution authority must still be established explicitly at the appropriate trust boundary.**

A useful conceptual flow is:

```text
Agent A
   ↓
Proposal

Agent B
   ↓
Planning / Refinement

Agent C
   ↓
Additional Recommendation

Host
   ↓
Authoritative Context
   ↓
Governance
   ↓
Scoped Authority
   ↓
Host-Owned Execution
```

Adding agents can increase reasoning depth, specialization, or workflow flexibility.

It should not automatically change:

```text
proposal → proposal
```

into:

```text
proposal → authority
```

---

## Agent, Model, and Host Are Different Roles

The terms are often used interchangeably even though the architectural responsibilities differ.

### Model

A model performs inference.

It may generate:

- Text.
- Structured output.
- Tool suggestions.
- Classifications.
- Plans.
- Arguments.
- Explanations.

A model output is not automatically an authoritative system fact.

### Agent

An agent is a software participant that uses one or more models, memory stores, tools, planners, workflows, or control loops to pursue a goal.

An agent may:

- Interpret a request.
- Ask another agent for help.
- Produce a plan.
- Refine arguments.
- Recommend an operation.
- Request a tool call.
- Carry a previously issued capability.

The word `agent` does not itself imply execution authority.

### Host

The host is the application or service boundary that owns the authoritative execution environment.

Depending on the architecture, the host may own:

- Authentication.
- Actor resolution.
- Tenant and resource lookup.
- Tool registries.
- Policy context.
- Governance evaluation.
- Capability issuance and validation.
- Credentials and secrets.
- External side effects.
- Durable audit evidence.
- Cancellation and recovery behavior.

A multi-agent system may have several hosts or services.

That makes trust boundaries more important, not less important.

---

## Recommendation, Planning, Delegation, and Authority Are Not Synonyms

A multi-agent workflow becomes easier to reason about when these states remain distinct.

| Concept | Meaning | Creates execution authority by itself? |
| --- | --- | --- |
| Recommendation | An agent suggests what should happen. | No |
| Plan | An agent or planner describes one or more intended steps. | No |
| Proposal | A typed request for the host to consider an operation. | No |
| Coordination | Agents exchange state, tasks, or recommendations. | No |
| Delegation request | A request to derive narrower authority for another participant. | No |
| Delegated capability | Explicit authority derived under host-defined rules. | Potentially, within its validated scope |
| Governance decision | A host-owned policy outcome about whether continuation is appropriate. | Not necessarily; capability/execution checks may remain |
| Execution authority | Authority accepted at the side-effect boundary for a specific action. | Yes, but only after validation |
| Execution | The host-owned side effect itself. | N/A |

This distinction prevents a common collapse:

```text
Agent A recommends
      ↓
Agent B agrees
      ↓
System treats agreement as authorization
```

Agreement may improve confidence in a recommendation.

It does not create a security or governance principal unless the host explicitly defines such a mechanism.

---

## Two Agents Agreeing Does Not Mean Authorized

Consider:

```text
Agent A:
"Ask Agent B to disable account 123."

Agent B:
"I agree."
```

Avoid interpreting this as:

```text
Two agents agreed
        ↓
Authorized
```

Prefer:

```text
Agent agreement
      ↓
Proposed intent
      ↓
Host validates proposal
      ↓
Host resolves actor + resource + current state
      ↓
Governance
      ↓
Possible scoped authority
      ↓
Host-owned execution
```

The second agent's agreement is still model-generated evidence.

It may be relevant input to a workflow.

It is not automatically authoritative policy.

---

## A Multi-Agent Planning System Is Not a Distributed Authorization System

A planner may decompose a goal:

```text
Goal:
Resolve support incident

Plan:
1. Inspect account state
2. Review recent events
3. Disable compromised account
4. Notify owner
```

Several specialist agents may contribute to that plan.

That makes the system a distributed **planning** or **coordination** system.

It does not automatically make every participant an authorization authority.

A useful boundary is:

```text
Planning plane
      ↓
Proposed steps
      ↓
Authority plane
      ↓
Host policy + scoped authority
      ↓
Execution plane
```

The planning plane can be probabilistic, exploratory, and model-driven.

The authority plane should remain explicit enough to test and review.

The execution plane should remain host-controlled.

---

## Preserve Originating Intent Across Agent Hops

A multi-hop request should not lose where it came from.

Suppose:

```text
User
  ↓
Agent A
  ↓
Agent B
  ↓
Agent C
  ↓
Tool proposal
```

By the time Agent C proposes an operation, the host may need to know:

```text
Who initiated the workflow?
What request did they make?
Which agent created the current proposal?
Which agents modified or refined it?
Which operation is now being proposed?
Which earlier approval or capability, if any, is being referenced?
```

A conceptual envelope might be:

```csharp
public sealed record AgentRequestEnvelope(
    string RequestId,
    string CorrelationId,
    string OriginatingActorId,
    string OriginatingRequestId,
    string SendingAgentId,
    string ReceivingAgentId,
    string MessageType,
    string SchemaVersion,
    object Payload,
    IReadOnlyList<string> PriorHopIds);
```

This is a teaching model, not a required protocol.

The important property is that later agents should not silently replace the originating identity with their own identity.

---

## Preserve Intent Provenance, Not Only Correlation

Correlation answers:

> Which events belong to the same workflow?

Intent provenance answers a different question:

> How did this particular proposed operation evolve?

A useful decision chain may preserve:

```text
Originating actor
Originating request ID
Workflow ID
Current proposal ID
Parent proposal ID
Agent hop ID
Agent identity
Message schema version
Operation
Resource
Argument digest or normalized argument identity
Referenced decision ID
Referenced capability ID
Correlation ID
```

Do not assume one correlation identifier answers every provenance question.

A single long-running workflow may contain several proposed operations and several independent decisions.

---

## Agent-Generated Context Is Advisory Until the Host Establishes Trust

Agent A may tell Agent B:

```text
actorRole = Administrator
resourceSensitivity = Low
tenant = tenant-a
region = US
```

Agent B may repeat those values.

The number of times the values are repeated does not make them authoritative.

Before governance, the host should resolve security-sensitive facts from appropriate authoritative sources:

```text
Authenticated actor
Current role or permission
Tenant membership
Resource owner
Resource classification
Current region
Current resource state
Policy identity
Destination trust
```

This is the same trust boundary taught in [Typed AI Proposed Intent and Schema-Validation Boundaries](../ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md), now repeated at each meaningful agent or service boundary.

---

## Agent-to-Agent Messages Need Schemas Too

An agent-to-agent protocol may transport JSON, typed RPC messages, queue events, or another structured format.

A valid transport message is still not authority.

A useful conceptual sequence is:

```text
Agent message received
      ↓
Parse
      ↓
Validate message schema
      ↓
Validate message type + version
      ↓
Validate sender / channel as appropriate
      ↓
Normalize proposal or delegation request
      ↓
Resolve authoritative host context
      ↓
Governance / authority validation
```

Validate at least the fields that influence downstream behavior:

- Message type.
- Schema version.
- Operation name.
- Resource identifiers.
- Destination identifiers.
- Required fields.
- Enum values.
- Size and length limits.
- Nested objects.
- Unsupported fields.
- Cross-field consistency.

Unknown or unsupported message types should fail explicitly.

---

## The Host Still Owns the Tool Registry

Agent B should not gain a new execution surface because Agent A names a tool.

Avoid:

```text
Agent A sends:
tool = shell.execute

Agent B dynamically resolves arbitrary method
        ↓
Execute
```

Prefer:

```text
Agent A proposes operation
        ↓
Agent B or receiving host parses proposal
        ↓
Host-owned registry lookup
        ↓
Unknown / unavailable operation rejected
        ↓
Executor invocation count = 0
```

The registry is an authority boundary.

Multi-agent communication should not expand it accidentally.

---

## Prohibited Pattern: Automatic Authority Inheritance

Avoid this conceptual design:

```text
Agent A has authority
        ↓
Agent A asks Agent B
        ↓
Agent B inherits all authority automatically
```

This erases:

- Audience binding.
- Subject binding.
- Operation binding.
- Resource binding.
- Expiration.
- Use limits.
- Delegation policy.
- Original decision context.
- Revocation behavior.

It also makes privilege amplification difficult to detect.

If Agent A can perform one operation, that does not imply Agent B should receive every permission Agent A possesses.

---

## Safer Pattern: Explicit Narrow Delegation

A more deliberate design is:

```text
Agent A holds scoped authority
        ↓
Delegation requested
        ↓
Host validates delegation rules
        ↓
Narrower authority issued to Agent B
        ↓
Agent B may act only within delegated scope
```

The central delegation invariant is:

```text
Derived authority
    must not silently become broader than
Source authority
```

A host-defined delegated capability might be checked against:

```text
Source capability
Delegating subject
Receiving subject
Operation
Resource
Audience
Scopes
Expiration
Use limit
Policy identity
Delegation depth
Revocation state
```

The exact representation is application-specific.

The architectural property is monotonic authority narrowing unless a separately authorized elevation path exists.

---

## Delegation Should Be a Host Decision, Not an Agent Statement

Agent A might output:

```text
I delegate my permissions to Agent B.
```

That sentence is not a capability.

A host should decide whether delegation is permitted.

A conceptual flow might be:

```text
Source authority presented
      ↓
Delegation request parsed
      ↓
Host verifies source authority
      ↓
Host verifies delegation is permitted
      ↓
Host computes allowable derived scope
      ↓
Host issues narrower authority
```

The receiving agent should not decide for itself that the delegation is valid.

---

## Compare Source and Derived Authority Explicitly

Suppose Agent A holds:

```text
Subject: agent-a
Operation: account.disable
Resource: account-123
Audience: account-gateway
Scopes: account.disable
Expires: 10:05
MaxUses: 1
```

A valid derived authority for Agent B might be:

```text
Subject: agent-b
Operation: account.disable
Resource: account-123
Audience: account-gateway
Scopes: account.disable
Expires: 10:03
MaxUses: 1
ParentCapability: cap-a
```

A suspicious derived request would be:

```text
Subject: agent-b
Operation: account.*
Resource: *
Audience: any
Scopes: admin
Expires: tomorrow
MaxUses: unlimited
```

That second form expands authority across operation, resource, audience, scope, time, and use count.

A delegation validator should reject it.

---

## Audience Binding Matters More Across Services

Multi-agent systems often span services:

```text
Planner service
      ↓
Specialist service
      ↓
Tool gateway
```

A capability accepted by the specialist should not automatically be accepted by every other service.

Audience binding can express:

```text
This authority is intended for account-gateway only.
```

Without audience checks, a capability may be replayed in a service that interprets the same scope more broadly.

Each receiving service remains responsible for validating authority at its own boundary.

---

## Resource and Operation Bindings Prevent Argument Substitution

Suppose Agent A proposes:

```text
account.disable
accountId = 123
```

The host evaluates that request and establishes authority for `account-123`.

Then Agent B changes the arguments:

```text
accountId = 999
```

Avoid:

```text
Original operation was approved
        ↓
Execute changed arguments
```

Prefer:

```text
Agent A proposes operation
        ↓
Host evaluates account-123
        ↓
Agent B changes arguments to account-999
        ↓
Original approval no longer matches
        ↓
Re-evaluation required
```

The same rule applies if the operation, destination, tenant, region, amount, recipient, or other policy-relevant argument changes.

Approval belongs to the evaluated intent, not to a vague workflow label.

---

## Normalize Agent Changes Into New Proposal Identity

When a downstream agent materially changes a proposal, create a new proposal identity rather than mutating the earlier proposal invisibly.

For example:

```text
proposal-17
operation = account.disable
resource = account-123
        ↓
Agent B changes resource
        ↓
proposal-18
parent = proposal-17
operation = account.disable
resource = account-999
```

This makes provenance easier to reconstruct.

It also prevents an earlier decision from appearing to authorize a later changed request.

---

## Multi-Step Workflows Need Step-Specific Authority

A long-running plan might be:

```text
1. Read customer record
2. Export selected fields
3. Upload analytics package
4. Notify customer
```

Avoid issuing one capability such as:

```text
scope = workflow.execute_all
expires = 24 hours
```

when the individual steps have different consequences and trust boundaries.

A safer architecture may establish authority step by step:

```text
Step 1
Read capability
      ↓
Step result
      ↓
Re-evaluate current state
      ↓
Step 2
Export capability
      ↓
Step result
      ↓
Re-evaluate destination / policy
      ↓
Step 3
Upload capability
```

Step-specific authority limits how much a compromised or confused agent can do with one artifact.

---

## A Plan Is Not a Pre-Authorization for Every Step

Planning may occur before all authoritative facts are available.

For example:

```text
Planner proposes:
1. archive case
2. delete temporary attachment
3. notify external contact
```

The plan itself should not pre-authorize those future actions.

Each consequential step may require fresh host context and governance.

This is especially important when:

- Time passes.
- Resource state changes.
- Policy changes.
- Tenant or region changes.
- Acknowledgment becomes stale.
- A destination changes.
- Another agent modifies the plan.

---

## Re-Evaluate Between Steps When Assumptions Change

Suppose a workflow was approved under policy version `4.2`.

Before step 3, policy changes to `4.3`.

A host-defined freshness rule might require:

```text
Step 2 completes
      ↓
Policy changed
      ↓
Old decision evidence preserved
      ↓
Current policy resolved
      ↓
Step 3 re-evaluated
```

Do not rewrite the earlier decision to pretend it was created under the new policy.

Preserve historical provenance and create new decision evidence when re-evaluation occurs.

See [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md).

---

## Resource Change Can Invalidate Derived Authority

A capability may still be unexpired while the resource no longer matches the assumptions that justified it.

Examples:

```text
Account becomes protected
Resource changes tenant
Document classification becomes restricted
Case is closed
Destination becomes blocklisted
```

The execution boundary may need to resolve current host state before accepting the capability.

Time validity is not the same as current semantic validity.

---

## Bounded Use and Replay Still Apply

Delegated authority can be replayed like any other execution authority.

A derived capability may need:

- Maximum-use count.
- Atomic consumption.
- Nonce or unique capability ID.
- Revocation state.
- Expiration.
- Durable shared use state when multiple hosts can accept it.

A local in-memory check does not prove distributed single-use behavior.

For the broader boundary, see [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md).

---

## Revocation Must Reach the Execution Boundary

If the source authority is revoked, what happens to derived authority?

Possible models include:

```text
Source revocation automatically invalidates descendants
```

or:

```text
Each derived capability has independent revocation state
```

or:

```text
Execution validates both parent lineage and current derived grant
```

There is no universal answer.

But the choice should be explicit.

If descendant authority can outlive a revoked parent, that is a significant security property and should not be accidental.

---

## Delegation Depth Should Be Bounded Deliberately

Unbounded chains can become difficult to reason about:

```text
Agent A
  ↓ delegates
Agent B
  ↓ delegates
Agent C
  ↓ delegates
Agent D
  ↓ ...
```

A host may choose to constrain:

```text
Maximum delegation depth
Allowed delegate identities
Allowed services
Allowed operations
Maximum expiration after each hop
Maximum remaining use count
```

A simple policy might allow only one delegation hop.

Another system might permit deeper chains while preserving parent references and strict scope narrowing.

The design should match the risk and operational need.

---

## Detect Cyclic Delegation

Agent graphs can contain cycles:

```text
Agent A delegates to Agent B
Agent B delegates to Agent C
Agent C delegates back to Agent A
```

Without cycle detection, a workflow may:

- Loop indefinitely.
- Repeatedly consume resources.
- Generate duplicate proposals.
- Produce confusing provenance.
- Create accidental replay opportunities.
- Hide whether any participant still has valid authority.

A host may preserve visited hop identities or delegation lineage and reject cycles according to its workflow rules.

Cycle detection is an orchestration safeguard.

It does not replace authority validation.

---

## Agent Loops Need Operational Bounds

Even without delegation, agents may repeatedly ask one another for refinement:

```text
Agent A → Agent B → Agent A → Agent B ...
```

Useful bounds may include:

- Maximum steps.
- Maximum elapsed time.
- Maximum model calls.
- Maximum cost or token budget.
- Maximum tool proposals.
- Cancellation token.
- Workflow deadline.

These are operational controls.

A governance system should not be expected to solve every orchestration loop problem.

---

## Agent Self-Approval Is Not a Human Responsibility Boundary

Suppose a policy returns:

```text
AcknowledgmentRequired
```

Avoid allowing the proposing agent to answer:

```text
I acknowledge.
```

and treating that as human acknowledgment.

The same applies to an agent approving its own request under a workflow that was intended to require an independent actor.

A host should resolve who is authorized to satisfy the acknowledgment or approval boundary.

The identity and meaning of that response should be explicit.

---

## Mutual-Agent Approval Does Not Automatically Solve Self-Approval

A system might attempt:

```text
Agent A proposes
Agent B approves
```

That can be useful as a recommendation workflow.

But if both agents operate under the same trust assumptions, same credentials, same compromised prompt context, or same authority source, the second approval may not provide meaningful independence.

Do not assume:

```text
Different model instance
=
Independent authorization authority
```

Independence is a property of the trust and authority design, not merely the number of models involved.

---

## Quorum Recommendations Are Not the Same as Authorization

Some systems may ask several agents to vote:

```text
Agent A = approve
Agent B = approve
Agent C = reject
```

The result might be:

```text
2 / 3 recommend proceed
```

That can be useful evidence for planning or review.

It should remain distinct from:

```text
Authorized = true
```

unless the host has explicitly defined a policy in which a particular authenticated quorum is itself an authorization authority.

Model confidence, voting, consensus, and explanation quality are not substitutes for host-owned policy by default.

---

## Credentials and Secrets Should Remain Host-Owned

Agent B should not receive a database password merely because Agent A wants it to perform a database-backed action.

Prefer:

```text
Agent proposes semantic operation
      ↓
Host validates authority
      ↓
Host-owned tool handler uses credential
```

rather than:

```text
Agent delegates request
      ↓
Copy infrastructure credential to next agent
```

This reduces privilege propagation and limits what is exposed in prompts, memory, traces, or agent messages.

A delegated capability can represent narrow authority without exposing the underlying infrastructure secret.

---

## Cross-Service Agent Hops Are Trust Boundaries

A multi-agent workflow may cross:

```text
Service A
      ↓
Message bus
      ↓
Service B
      ↓
External tool gateway
```

Each hop may introduce questions such as:

```text
Who authenticated the sender?
Was the message altered?
Which schema version is supported?
Is this sender allowed to request this operation?
Which actor originally initiated the workflow?
Is the referenced capability intended for this audience?
Is the resource still current?
Is policy still current?
```

A network location or service name should not automatically substitute for those checks.

See [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md).

---

## Message Authenticity and Execution Authority Are Separate

A receiving service may cryptographically verify that a message came from Agent Service A.

That proves a narrower fact:

```text
This message was authenticated as coming from Service A.
```

It does not automatically prove:

```text
Service A was authorized to cause this operation.
```

Authentication, authorization, governance, and execution authority remain separate concerns.

A signed agent message can still contain an operation that policy denies.

---

## Durable Decision-Chain Evidence

Long-running multi-agent workflows need more than one final success log.

A useful evidence chain may include:

```text
Originating request received
Agent A proposal created
Agent B refinement created
Host normalized proposed intent
Governance decision created
Delegation requested
Derived capability issued
Capability validated
Step execution started
Step execution completed
Policy re-evaluated
Workflow completed / cancelled / failed
```

Useful identifiers may include:

```text
WorkflowId
CorrelationId
OriginatingRequestId
ProposalId
ParentProposalId
AgentHopId
DecisionId
AcknowledgmentId
CapabilityId
ParentCapabilityId
ExecutionAttemptId
```

The exact fields depend on the application.

The goal is to reconstruct the authority path without storing unnecessary sensitive content.

---

## Observability Is Not the Same as Governance Provenance

Distributed tracing may tell you:

```text
Service A called Service B
Service B called Service C
Latency = 420 ms
```

Governance provenance should answer different questions:

```text
Which actor initiated the request?
Which proposal was evaluated?
Which policy version produced the decision?
Which capability justified the execution attempt?
Which agent modified the proposed arguments?
Was the authority delegated?
Did the resource or policy change before execution?
```

Trace identifiers are useful for correlation.

They do not replace governance-specific evidence.

Likewise, governance evidence should not be overloaded with every diagnostic field in the tracing system.

---

## Minimize Raw Prompt and Response Retention

Multi-agent systems can produce large amounts of conversational content.

Do not assume governance provenance requires retaining every raw prompt and response indefinitely.

Prefer durable structured evidence such as:

```text
Proposal identity
Normalized operation
Normalized resource
Policy identity
Reason codes
Capability identity
Agent hop identity
Outcome
Timestamp
Correlation
```

when that is sufficient.

Raw prompts or responses may contain:

- Secrets.
- Personal data.
- Untrusted content.
- Proprietary data.
- Large payloads.

Retention should follow the application's actual operational, privacy, security, and legal requirements.

---

## Partial Failure Does Not Undo Earlier Side Effects

Consider a multi-step workflow:

```text
Step 1 = completed
Step 2 = completed
Step 3 = failed
```

A governance system cannot automatically pretend the first two side effects never happened.

The host needs an explicit recovery model:

```text
Retry
Compensate
Pause
Escalate
Cancel remaining steps
Create a new decision
```

Which response is correct depends on the operation.

The evidence chain should distinguish:

```text
Planned
Authorized
Attempted
Completed
Failed
Compensated
Cancelled
```

---

## Cancellation Should Stop Future Authority Use

A user or supervisor may cancel a long-running workflow.

The architecture should define what cancellation means for:

- Pending proposals.
- Outstanding acknowledgments.
- Issued capabilities.
- Derived capabilities.
- In-flight tool execution.
- Retry queues.
- Recovery work.

Where practical, cancellation should prevent future use of authority that was issued only for the cancelled workflow.

That may require revocation or workflow-state checks at execution.

---

## Recovery May Require Fresh Governance

After failure, avoid assuming the original authority remains suitable for every recovery action.

For example:

```text
Original operation:
archive.case

Failure:
archive completed, notification failed

Recovery proposal:
restore.case
```

`restore.case` is a different operation.

It may require its own policy evaluation and authority.

Compensation is not automatically authorized merely because the original action was authorized.

---

## Policy Change During a Workflow Is a First-Class Event

A long-running workflow may span policy deployments.

Suppose:

```text
T1 Agent A proposes export
T2 Decision = Allowed under policy 7.1
T3 Agent B performs preparation
T4 Policy changes to 7.2
T5 Upload step requested
```

The host should have an explicit freshness strategy:

```text
Use original policy for all already-approved steps
Re-evaluate every consequential step
Re-evaluate only when selected policy inputs changed
Require exact policy-version compatibility
```

There is no universal answer.

What matters is that policy drift does not disappear because the workflow has several agents.

---

## Resource Change During a Workflow Is Also First-Class

The resource may change while agents are planning.

For example:

```text
Account was active
      ↓
Agent A proposes disable
      ↓
Another process marks account protected
      ↓
Agent B asks tool agent to disable
```

Current host state should win over stale agent memory.

A re-evaluation may now produce:

```text
EscalationRecommended
```

The earlier proposal does not freeze resource state.

---

## Human Acknowledgment Can Exist Inside a Multi-Agent Workflow

A multi-agent workflow may pause for a human boundary:

```text
Agent A proposes
      ↓
Agent B refines
      ↓
Governance = AcknowledgmentRequired
      ↓
Human acknowledgment
      ↓
Host refreshes context
      ↓
Re-evaluation
      ↓
Possible capability
```

The acknowledgment should remain bound to the relevant:

- Actor.
- Operation.
- Resource.
- Policy state.
- Challenge.
- Proposal identity where useful.

If a downstream agent materially changes the proposal after acknowledgment, the host may need a new acknowledgment or re-evaluation.

Acknowledgment is not a blank check for the remainder of the workflow.

---

## Use Narrow Semantic Operations Between Agents

Prefer agent requests such as:

```text
case.archive
account.disable
notification.send
customer.export
```

rather than broad primitives such as:

```text
shell.execute
sql.execute
invoke_arbitrary_method
filesystem.write_anywhere
```

Narrow operations make it easier to bind:

- Operation.
- Resource.
- Audience.
- Policy.
- Capability scope.
- Audit evidence.

A multi-agent architecture cannot fully compensate for an unnecessarily broad tool surface.

---

## Do Not Let Agent Names Become Security Roles Accidentally

A service may call a model:

```text
SecuritySupervisorAgent
```

That name is not a permission.

Avoid authorization logic such as:

```text
if (agent.Name.Contains("Supervisor"))
{
    allow = true;
}
```

Authority should come from host-owned identity and policy mechanisms.

Descriptive agent roles can help orchestration.

They should not silently become security principals.

---

## Do Not Use Model Confidence as Authority

Agent B may report:

```text
confidence = 0.99
```

That can influence review or planning.

It should not automatically become:

```text
CanExecute = true
```

A high-confidence incorrect proposal is still incorrect.

Likewise, low confidence does not necessarily mean policy must deny.

Keep inference confidence and governance authority conceptually separate.

---

## Determinism Belongs at the Governance Boundary

Agent reasoning may be nondeterministic.

Governance can still aim for a useful deterministic property:

```text
Same normalized proposed intent
+
Same authoritative context
+
Same policy version
+
Same validated authority inputs
      ↓
Same governance result
```

This does not require every agent to generate the same plan.

It requires that once a particular proposal reaches the governance boundary, policy behavior is testable rather than dependent on hidden conversational state.

---

## Test Multi-Hop Intent Preservation

A useful test can simulate deterministic fake agents:

```text
Fake Agent A
      ↓ proposal-1
Fake Agent B
      ↓ proposal-2 derived from proposal-1
Host
      ↓ governance
```

Assert that the final normalized proposal preserves:

```text
Originating actor
Originating request
Parent proposal
Current proposal
Correlation ID
Operation
Resource
```

A later executable sample should prefer deterministic fake agents over real external AI services so the architectural invariants remain repeatable.

---

## Test That Agreement Does Not Create Authority

Example:

```text
Agent A recommends account.disable
Agent B recommends account.disable
Agent C recommends account.disable
        ↓
No host governance decision
        ↓
Executor invocation count = 0
```

This test makes the learning point executable:

```text
Agent consensus
    ≠
Execution authority
```

---

## Test Delegation Narrowing

Start with source authority:

```text
Operation = account.disable
Resource = account-123
Audience = account-gateway
Expires = 10:05
Uses = 1
```

Attempt to derive:

```text
Operation = account.*
Resource = *
Audience = any
Expires = 11:05
Uses = unlimited
```

Expected:

```text
Delegation rejected
Executor invocation count = 0
```

Then derive a genuinely narrower capability and verify that only its exact operation and resource can reach execution.

---

## Test Argument Mutation

Example:

```text
proposal-1:
account.disable(account-123)
      ↓
Decision = Allowed
      ↓
Agent B mutates target
account.disable(account-999)
      ↓
Old decision does not match
      ↓
Re-evaluation required
      ↓
No execution under old authority
```

This protects the boundary between approved intent and later modified execution parameters.

---

## Test Audience and Cross-Service Boundaries

A capability issued for:

```text
Audience = account-gateway
```

should fail at:

```text
Audience = infrastructure-admin-gateway
```

Even if both services understand the same capability format.

Format compatibility does not imply authority compatibility.

---

## Test Replay and Descendant Revocation

Useful cases include:

```text
Derived capability used once
      ↓
Second use rejected
```

and, if the host's model requires lineage revocation:

```text
Parent capability revoked
      ↓
Unused child capability presented
      ↓
Rejected
```

The exact expected behavior depends on the chosen revocation model.

Document it before relying on it.

---

## Test Policy and Resource Drift Between Steps

Example:

```text
Step 1 decision under policy 4.2
      ↓
Policy changes to 4.3
      ↓
Step 2 request
      ↓
Configured freshness rule requires re-evaluation
```

Also test:

```text
Resource becomes protected between steps
      ↓
Current host lookup sees protected state
      ↓
Governance = EscalationRecommended
      ↓
Executor invocation count = 0
```

---

## Test Cyclic Agent Requests

A deterministic fake-agent graph can model:

```text
A → B → C → A
```

Expected behavior may be:

```text
Cycle detected
Workflow stopped or escalated
No new execution authority issued
```

This is an orchestration test, but it protects the authority pipeline from endless delegated work.

---

## Test Self-Approval and Mutual Approval Boundaries

If policy requires a human acknowledgment:

```text
Agent response = "approved"
```

should not satisfy the requirement unless the host explicitly defines that agent as an authorized responder for that particular workflow.

Likewise:

```text
Agent A proposes
Agent B approves
```

should remain a recommendation chain unless the host has established an actual authorization authority behind Agent B.

---

## A Useful Layered Test Model

Multi-agent architecture benefits from layered tests:

```text
Agent Message Schema Tests
        ↓
Proposal Provenance Tests
        ↓
Host Context Resolution Tests
        ↓
Governance Decision Tests
        ↓
Delegation / Capability Narrowing Tests
        ↓
Cross-Service Validation Tests
        ↓
Execution-Boundary Invariant Tests
        ↓
Workflow Failure / Recovery Tests
```

This prevents one large integration test from hiding which boundary failed.

---

## Common Failure Modes

### 1. Agent Agreement Becomes Authorization

Several agents recommend the same action and the system skips host policy.

### 2. Authority Is Copied Instead of Delegated

Agent B receives every permission Agent A has rather than a narrower derived scope.

### 3. Originating Actor Identity Is Lost

A downstream request appears to originate from the latest agent rather than the human or service that initiated the workflow.

### 4. Agent Context Becomes Authoritative Context

Tenant, role, region, classification, or risk values are copied through agent messages without host verification.

### 5. Argument Changes Reuse Old Approval

A downstream agent changes the resource or destination while retaining an earlier decision or capability.

### 6. One Capability Authorizes an Entire Plan

A long-running workflow receives broad standing authority instead of step-specific bounded authority.

### 7. Delegation Expands Time or Scope

A child capability lasts longer or grants more operations than its parent.

### 8. Audience Is Not Bound

Authority intended for one service is accepted by another.

### 9. Cyclic Delegation Is Unbounded

Agents repeatedly delegate work back to earlier agents without an explicit loop limit.

### 10. Agent Self-Approval Satisfies Human Acknowledgment

The same automated system proposing the action also satisfies a responsibility boundary intended for a human actor.

### 11. Agent Voting Is Treated as Policy

A quorum of model recommendations substitutes for authoritative authorization rules.

### 12. Credentials Propagate with the Task

Secrets move from agent to agent instead of staying behind host-owned handlers.

### 13. Trace Logs Are Mistaken for Decision Provenance

Distributed traces show call order but cannot reconstruct why authority existed.

### 14. Policy Drift Is Ignored During Long Workflows

A workflow continues under stale policy assumptions because an earlier step was allowed.

### 15. Cancellation Does Not Revoke Future Use

Outstanding capabilities remain usable after the workflow is cancelled.

### 16. Recovery Reuses Unrelated Authority

A failure compensation operation executes under authority that was issued for the original action.

### 17. Framework Primitives Are Presented as a Multi-Agent Protocol

Capability or audit types are mistaken for a complete agent orchestration or delegation standard.

---

## Tradeoffs

### Benefits

- Agent communication remains separate from execution authority.
- Originating intent can survive multi-hop workflows.
- Delegation can be explicitly narrowed and audited.
- Privilege amplification becomes easier to detect.
- Step-specific authority limits long-running workflow exposure.
- Cross-service audience and resource bindings remain visible.
- Policy and resource drift can trigger deliberate re-evaluation.
- Human acknowledgment can remain independent from agent planning.
- Durable evidence can reconstruct how a proposal evolved.

### Costs

- More identifiers and provenance must be carried across hops.
- Delegation validation adds state and policy.
- Step-specific capabilities increase orchestration complexity.
- Revocation and replay become harder across distributed services.
- Long-running workflows need cancellation and recovery semantics.
- Re-evaluation can increase latency and reduce availability.
- Excessive evidence retention can increase privacy and storage risk.
- Over-governing low-consequence agent coordination can add unnecessary ceremony.

Use the smallest architecture that preserves the authority boundaries the actual operation needs.

---

## What This Experimental Pattern Does Not Guarantee

This architecture does not automatically provide:

- A standardized agent-to-agent protocol.
- Correct model reasoning.
- Correct planning.
- Safe autonomous behavior.
- Secure prompt handling.
- Prompt-injection immunity.
- Correct authentication or authorization.
- Distributed single-use guarantees.
- Correct revocation.
- Exactly-once execution.
- Successful recovery from partial failure.
- Regulatory compliance.
- AGI or ASI architecture.
- Production readiness.

It provides a way to reason about where authority should and should not appear in a multi-agent workflow.

---

## Relationship to AsiBackbone

The working `AsiBackbone` repository provides useful governance primitives and examples, but it should not be interpreted as a multi-agent runtime or standardized delegation protocol.

Useful implementation specimens include:

- [AI Agent Gateway Scenario](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/scenarios/ai-agent-gateway.md) — establishes the single-agent boundary where the agent proposes and the host owns policy context, execution, and operational safeguards.
- [`CapabilityTokenGrant`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityTokenGrant.cs) — provides provider-neutral capability metadata that can be studied for subject, operation, resource, audience, scope, policy, and time bindings.
- [Capability Grant Hardening](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/capability-grant-hardening.md) — covers proof, binding, failure, time, and bounded-use concerns at the execution boundary.
- [`AuditResidue`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Audit/AuditResidue.cs) — provides structured governance evidence that can participate in a larger host-owned decision chain.
- [Intent to Execution Pattern](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/intent-to-execution-pattern.md) — reinforces that governance artifacts do not themselves perform the protected side effect.

These references provide building blocks to inspect.

They do **not** mean that the framework currently implements:

```text
Agent discovery
Agent-to-agent transport
Planner orchestration
Delegation graphs
Agent voting
Multi-agent scheduling
Autonomous recovery
A complete delegated-capability protocol
```

Those concerns remain host or platform architecture unless explicitly implemented elsewhere.

---

## A Deterministic Future Simulation

A later executable companion can demonstrate this article without external AI services.

Use deterministic fake agents:

```text
FakeAgentA
  always proposes account.disable(account-123)

FakeAgentB
  can either preserve or mutate the proposal

FakeAgentC
  produces a recommendation only
```

Then connect them to fake host services:

```text
Fake authoritative account store
Fake policy evaluator
Fake delegation validator
Fake capability issuer
Recording executor
In-memory evidence sink
```

Useful scenarios would include:

1. Three agents agree, but no governance decision exists — no execution.
2. Agent B mutates the resource — re-evaluation required.
3. Agent A requests delegation — Agent B receives narrower authority.
4. Agent B requests broader delegated scope — rejected.
5. Policy changes between steps — re-evaluation occurs.
6. Resource becomes protected — execution stops.
7. Derived capability is replayed — second use fails in the teaching model.
8. Cyclic delegation is detected.
9. Human acknowledgment cannot be supplied by the proposing agent.
10. Successful final execution reaches the recording executor exactly once.

Keeping the agents deterministic lets the learner test architecture rather than model variability.

---

## Review Questions

When reviewing a multi-agent execution architecture, ask:

1. Which participants are models, agents, hosts, policy authorities, and executors?
2. Which agent outputs are recommendations, plans, proposals, or delegation requests?
3. Where is execution authority actually established?
4. Is the originating actor identity preserved across every hop?
5. Is the originating request identity preserved?
6. Can each material proposal change be reconstructed?
7. Are agent-generated role, tenant, region, or risk claims re-resolved through authoritative host sources?
8. Are agent-to-agent messages schema-validated?
9. Can an agent invent a new executable tool name?
10. Can Agent B automatically inherit Agent A's authority?
11. If delegation is allowed, how is derived authority proven no broader than source authority?
12. Are subject, operation, resource, audience, time, and use bounds explicit?
13. What happens when Agent B changes an already-approved argument?
14. Does each consequential workflow step need fresh or step-specific authority?
15. What happens when policy changes between steps?
16. What happens when resource state changes between steps?
17. How are replay and revocation handled for derived authority?
18. Can a revoked parent leave valid descendants, and is that behavior intentional?
19. Is delegation depth bounded?
20. Are cycles detected?
21. Can an agent satisfy its own human acknowledgment requirement?
22. Is a multi-agent vote only a recommendation, or has the host explicitly made it an authorization mechanism?
23. Do credentials remain behind host-owned handlers?
24. Can tracing and governance provenance be distinguished?
25. What happens during partial failure, cancellation, and recovery?
26. Does every non-executable path prove `protected executor invocation count = 0`?
27. Would the simpler single-agent governed gateway preserve the same required boundaries with less complexity?

If those answers are unclear, adding more agents may have increased orchestration without clarifying authority.

---

## Related Content

- [Advanced Overview](index.md) — place multi-agent execution boundaries in the broader advanced learning path.
- [Typed AI Proposed Intent and Schema-Validation Boundaries](../ai-integration/typed-ai-proposed-intent-and-schema-validation-boundaries.md) — treat each agent-generated operation as untrusted proposed intent before authoritative host context is constructed.
- [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) — begin with the simpler single-agent proposal-to-host execution boundary.
- [Agent Memory and Governance Boundaries](../ai-integration/agent-memory-and-governance-boundaries.md) — share remembered information across agents while preserving provenance, scope, current host facts, and the rule that shared memory does not share authority.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — review narrow execution authority, audience/resource/operation bindings, expiration, and host-owned validation.
- [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md) — identify where agent and service hops change what the system is willing to trust.
- [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) — examine replay, atomic consumption, distributed use state, and idempotency boundaries.
- [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) — preserve policy identity and reason about stale decisions during long-running workflows.
- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) — keep human responsibility and durable evidence distinct from agent-generated approval language.

---

> **More agents can add reasoning paths. They do not add authority unless the host explicitly creates it.**
