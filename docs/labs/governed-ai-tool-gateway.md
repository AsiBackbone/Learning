# Lab — Governed AI Tool Gateway

**Learning objective:** Practice preserving host-owned execution authority when an AI system proposes a consequential tool action, and threat-model the trust boundaries between model output, authoritative host context, governance, acknowledgment, scoped capability, and execution.

**Difficulty:** Advanced
**Prerequisites:** Complete the [Governed AI Tool Gateway tutorial](../tutorials/governed-ai-tool-gateway.md), run the [Governed AI Tool Gateway sample](https://github.com/AsiBackbone/Learning/blob/main/samples/governed-ai-tool-gateway/README.md), and be comfortable with the first four foundational patterns.

This is the capstone lab for the foundational ASI Backbone Learning path.

The baseline sample uses a simulated proposal generator and a dry-run `notification.send` handler.

No real model provider or external notification service is required.

The central rule is:

> **The model may propose. The host retains execution authority.**

This lab asks you to break that rule in controlled ways, observe what becomes possible, repair the boundary, and document the remaining risk.

---

## Starting Architecture

The companion sample begins with this flow:

```text
Simulated model proposal
   ↓
Host tool registry
   ↓
Proposal validation
   ↓
Host-built authoritative context
   ↓
Governance decision
   ↓
Acknowledgment when required
   ↓
Re-evaluation
   ↓
Scoped capability
   ↓
Execution-boundary validation
   ↓
Single-use consumption
   ↓
Host-owned dry-run handler
   ↓
Audit residue
```

Important baseline invariants include:

```text
Unknown tool
   ↓
Rejected
   ↓
Handler invocation count = 0
```

```text
Model claims external recipient is internal
   ↓
Host rebuilds classification
   ↓
AcknowledgmentRequired
```

```text
Expired or mismatched capability
   ↓
Execution blocked
```

```text
Same capability identity used twice
   ↓
Second use rejected
```

```text
Valid governed flow
   ↓
WouldExecute = true
   ↓
No real external side effect
```

## Prepare the Lab

Create a temporary branch or disposable copy:

```bash
git switch -c lab/governed-ai-tool-gateway
```

Run the sample and focused tests before making changes:

```bash
dotnet run --project samples/governed-ai-tool-gateway/GovernedAiToolGateway/GovernedAiToolGateway.csproj

dotnet test samples/governed-ai-tool-gateway/GovernedAiToolGateway.Tests/GovernedAiToolGateway.Tests.csproj
```

Locate these types in the sample:

1. `AiToolProposal`
2. `ToolRegistry`
3. `ProposalValidator`
4. `HostPolicyContextFactory`
5. `NotificationPolicy`
6. `AcknowledgmentService`
7. `ExecutionCapabilityIssuer`
8. `ExecutionCapabilityValidator`
9. `InMemoryCapabilityUseStore`
10. `RecordingNotificationHandler`
11. `InMemoryAuditSink`
12. `GovernedAiToolGateway`

Before modifying anything, explain which component owns:

```text
Proposal
Tool availability
Argument validation
Security-sensitive context
Policy decision
Acknowledgment
Execution authority
Credential boundary
Side effect
Evidence
```

---

# Part 1 — Let the Model Define the Tool Surface

The baseline host uses a fixed registry containing the narrow semantic operation:

```text
notification.send
```

Temporarily replace that boundary with logic that dynamically accepts any model-provided tool name.

For example, imagine a resolver that maps arbitrary strings to application methods:

```text
Model output
   ↓
Reflection / dynamic invocation
   ↓
Matching method executes
```

Then retry a proposal such as:

```text
finance.transfer_unlimited
```

or invent another operation that should not exist in the teaching host.

## Explain the Failure

Answer:

1. Who now defines the executable vocabulary?
2. Which review boundary disappeared?
3. Can a hallucinated tool name become authority?
4. Could a prompt injection expand the available action surface?
5. Why is a narrow host-owned registry easier to reason about than arbitrary reflection or command dispatch?

Restore the fixed tool registry.

The lesson is:

> **A model may select from a host-defined action surface. It should not silently create that surface.**

---

# Part 2 — Trust Model-Supplied Security Context

The sample deliberately allows a proposal to contain:

```text
classification = internal
```

but the host ignores that claim and reconstructs destination classification from `RecipientDirectory`.

Temporarily modify `HostPolicyContextFactory` to trust:

```csharp
proposal.Arguments["classification"]
```

instead of the host directory.

Run the existing test:

```text
ModelSuppliedClassificationDoesNotOverrideHostClassification
```

It should expose the boundary failure.

Now submit:

```text
recipient = partner@example.net
classification = internal
```

Observe whether the external-recipient acknowledgment requirement disappears.

## Explain the Trust Error

Answer:

1. Which participant benefits if the classification is lowered?
2. Which participant can manipulate the proposal?
3. Which source is authoritative for destination classification?
4. What other fields should normally be reconstructed by the host?
5. What changes if the model is highly reliable but still processes untrusted documents?

Restore host-built classification.

The lesson is:

> **Model output can contribute facts to a proposal without becoming the authority for security-sensitive facts.**

---

# Part 3 — Treat Prompt Text as Enforcement

Imagine the system prompt contains:

```text
Never send notifications to blocked.example.
```

Delete or bypass the host-side blocked-domain rule in `NotificationPolicy`.

Assume the model follows the prompt in normal conditions.

Then deliberately construct a proposal for:

```text
recipient@blocked.example
```

The sample proposal can represent:

- Model error.
- Prompt injection.
- Adversarial retrieval content.
- Tool-description confusion.
- A future model behavior change.

If the host no longer checks the destination, what prevents the operation from proceeding?

Restore the host-side rule.

## Compare the Two Controls

Document the difference between:

```text
Prompt instruction
```

and:

```text
Host execution control
```

A useful distinction is:

```text
Prompt
=
Influences model behavior

Host policy / gateway
=
Controls whether a side effect may occur
```

The two can complement each other.

They should not be confused.

---

# Part 4 — Let the Model Satisfy Acknowledgment

The baseline external flow requires a host-owned acknowledgment response associated with the actor.

Create an intentionally weak variant where the model can emit:

```json
{
  "acknowledged": true
}
```

and the gateway treats that as equivalent to the actor accepting the host challenge.

Try an external recipient again.

## Explain the Responsibility Collapse

Answer:

1. Who proposed the action?
2. Who satisfied the acknowledgment?
3. Is there still a distinct human or system responsibility boundary?
4. Which actor identity is preserved?
5. Could an injected instruction cause both proposal and acknowledgment?

Restore the separate host-owned acknowledgment response.

Then run:

```text
WrongAcknowledgmentActorDoesNotBecomeExecutionAuthority
```

The lesson is:

> **The component proposing an action should not automatically satisfy a separate responsibility boundary merely by generating affirmative text.**

---

# Part 5 — Turn Acknowledgment into a Policy Override

The baseline flow is:

```text
Acknowledgment accepted
   ↓
Context updated with acknowledgment identity
   ↓
Policy re-evaluated
```

Temporarily replace that sequence with:

```text
Acknowledgment accepted
   ↓
Execute
```

Now imagine the recipient becomes blocked after the challenge was issued but before execution.

Modify the sample or test harness so the second evaluation would return:

```text
Denied
```

Then bypass re-evaluation.

## Explain the Failure

An acknowledgment can satisfy a specific condition.

It should not automatically erase:

- New policy restrictions.
- Changed resource state.
- Revoked actor access.
- Changed destination trust.
- Policy-version changes.

Restore re-evaluation.

The lesson is:

> **Acknowledgment satisfies an acknowledgment requirement; it does not become universal permission.**

---

# Part 6 — Broaden the Capability

The baseline capability is bound to:

```text
Actor
Operation
Recipient
Audience
Scope
Policy version
Acknowledgment identity when applicable
Expiration
One use
```

Temporarily weaken one or more bindings.

Examples:

```text
ResourceId = *
Scope = notification.*
ExpiresUtc = +24 hours
MaximumUses = unlimited
Audience check removed
```

Then ask:

> If this capability leaked, what is the maximum authority it would expose?

Compare that answer with the original approved action.

Restore narrow bindings.

## Required Experiment — Recipient Substitution

Issue a capability for:

```text
partner@example.net
```

then validate it against current context containing:

```text
other@example.net
```

The baseline should return:

```text
capability.resource-mismatch
```

Remove the resource check and observe the difference.

The lesson is:

> **The follow-on authority should remain no broader than the decision that justified it.**

---

# Part 7 — Move Capability Validation Away from the Side Effect

Create a weak flow:

```text
Validate capability
   ↓
valid = true
   ↓
Wait
   ↓
Context changes or capability expires
   ↓
Handler trusts cached boolean
```

Test at least one of these cases:

```text
Capability expires after early validation
Recipient changes after early validation
Policy version changes after early validation
```

Explain why the old validation result does not prove authority at the later side-effect boundary.

Restore validation immediately before capability consumption and handler invocation.

The lesson is:

> **Validate as close as practical to where authority becomes action.**

---

# Part 8 — Break Single-Use Enforcement

The sample uses `InMemoryCapabilityUseStore` to demonstrate bounded use.

Remove this check:

```text
TryConsume(capabilityId)
```

Run the replay test.

The same capability identity should now be able to reach the handler more than once.

Restore the use store.

## Identify the Remaining Production Gap

The in-memory store is intentionally not a production replay guarantee.

Answer:

1. What happens after process restart?
2. What happens with two application instances?
3. What happens if both instances check before either writes?
4. What persistence and atomicity properties are required for true single-use semantics?
5. How would regional deployment affect the design?

The correct conclusion is not:

```text
HashSet = replay protection solved
```

It is:

```text
The sample demonstrates the state transition.
Production replay guarantees require host-owned durable atomic state.
```

---

# Part 9 — Move Credentials into the Proposal Path

The baseline model proposal contains no infrastructure secret.

The recording handler owns a placeholder credential reference to make the boundary visible.

Create a weak design where the proposal contains:

```text
apiKey
accessToken
connectionString
```

and the model is expected to return that value when selecting the tool.

Do not use a real credential.

Use a fictional placeholder only.

## Evaluate the Expansion

Answer:

1. Does the model need the secret to understand the desired action?
2. Does the model need the secret to propose the action?
3. Which logs or traces might now contain the secret?
4. Could conversation history retain it?
5. Could retrieved content cause it to be exposed?
6. Can the host handler own the credential instead?

Restore the host-owned credential boundary.

The lesson is:

> **Understanding an action does not require possessing the infrastructure authority used to perform it.**

---

# Part 10 — Create a Fail-Open Gateway

The teaching sample uses local deterministic components, so simulate an unavailable governance dependency.

Add a switch such as:

```text
PolicyAvailable = false
```

Then implement the dangerous fallback:

```text
Policy unavailable
   ↓
Execute anyway to preserve availability
```

Run a consequential external proposal.

## Compare Failure Policies

Consider these alternatives:

```text
Deny
Defer
Escalate
Queue for later evaluation
Execute
```

There is no universal failure policy for every operation.

For the sample's consequential notification path, explain why silent execution broadens authority.

Restore a non-executing failure path.

The lesson is:

> **Infrastructure failure should not accidentally become permission.**

---

# Part 11 — Threat-Model the Complete Gateway

Now model the gateway as a set of trust boundaries rather than as one method.

Use this surface list:

| Surface | Trust question |
| --- | --- |
| Model proposal | What can the proposer influence or fabricate? |
| Tool registry | Who defines the executable action vocabulary? |
| Argument validation | Which malformed or adversarial values may cross the boundary? |
| Host context factory | Which facts are authoritative and where do they come from? |
| Policy evaluator | What happens when rules are wrong, stale, or unavailable? |
| Acknowledgment workflow | Who may respond and what exactly is acknowledged? |
| Capability issuer | What exact authority is created after approval? |
| Capability validator | Which bindings are checked at execution time? |
| Replay/use store | Can the same authority be reused? |
| Tool handler | Where are credentials and real side effects located? |
| Audit sink | Can the governed path be reconstructed afterward? |

For each surface, document at least:

```text
Threat
Precondition
Potential consequence
Preventive control
Detective evidence
Residual risk
Failure behavior
```

## Minimum Threat Cases

Your threat model should include at least these cases.

### A. Prompt Injection Influences the Proposal

Example:

```text
Retrieved content instructs the model to send data externally.
```

Ask:

- Which host controls still execute?
- Can the injected content alter the tool registry?
- Can it alter authoritative destination classification?
- Can it satisfy acknowledgment?
- Can it access host credentials?

### B. Hallucinated Tool Name

Example:

```text
finance.transfer_unlimited
```

Expected baseline property:

```text
Unknown tool
   ↓
Rejected before execution
```

### C. Argument Substitution

Example:

```text
Policy evaluated recipient A
Execution attempts recipient B
```

Expected baseline property:

```text
Capability resource mismatch
   ↓
No handler invocation
```

### D. Stolen Capability

Assume the capability artifact leaks.

Ask:

- Which actor is it bound to?
- Which recipient?
- Which operation?
- Which audience?
- How long is it valid?
- How many uses?
- What production proof mechanism is missing from the teaching sample?

### E. Replay

Assume a valid request is captured and retried.

Distinguish:

```text
Capability replay protection
```

from:

```text
External operation idempotency
```

A single-use capability does not by itself prove that a remote provider will never perform a duplicate side effect after ambiguous failures.

### F. Audit Sink Failure

Suppose the execution decision is valid but audit persistence fails.

Decide whether the host should:

```text
Block
Defer
Execute with degraded evidence
Queue evidence durably
```

Your answer should depend on the consequence and audit requirement.

Do not silently assume that logging success is equivalent to governance success.

---

# Part 12 — Compare a Simpler Architecture

Not every tool needs this entire sequence.

Design a simpler path for a low-risk local transformation such as:

```text
format_markdown
summarize_local_text
sort_items
```

Compare it with `notification.send`.

Document which controls you would remove and why.

A useful comparison might be:

| Concern | Local formatting | External notification |
| --- | --- | --- |
| Tool allowlist | Useful | Important |
| Argument validation | Useful | Important |
| Host authoritative context | Minimal | Important |
| Policy decision | Maybe unnecessary | Appropriate |
| Human acknowledgment | Usually unnecessary | Context-dependent |
| Scoped capability | Usually unnecessary | Useful for consequential action |
| Replay state | Usually unnecessary | Potentially important |
| External credential | None | Host-owned |
| Audit residue | Lightweight | Potentially important |

The objective is to avoid turning governance into ceremony detached from consequence.

The lesson is:

> **Use the strongest boundary where the side effect justifies it.**

---

# Final Validation

Restore the baseline implementation and run:

```bash
dotnet test samples/governed-ai-tool-gateway/GovernedAiToolGateway.Tests/GovernedAiToolGateway.Tests.csproj
```

Then run the full sample suite:

```bash
dotnet test samples/Samples.slnx
```

Confirm that you can explain why all of these statements are different:

```text
The model proposed the tool.
The host recognized the tool.
The proposal arguments were structurally valid.
The host reconstructed authoritative context.
Policy allowed the current operation.
The required acknowledgment was satisfied.
A narrow capability was issued.
The capability remained valid at execution time.
The single-use authority was consumed.
The host dry-run handler would execute.
The evidence trail recorded the governed path.
```

Also confirm:

- Unknown tools never reach execution.
- Model-supplied classification cannot lower host classification.
- Blocked destinations remain blocked even if prompt behavior fails.
- The model cannot self-satisfy actor acknowledgment.
- Acknowledgment triggers re-evaluation instead of bypassing policy.
- Recipient substitution invalidates authority.
- Expired capability cannot execute.
- Replay does not invoke the handler twice while the sample use state exists.
- Infrastructure credentials remain outside the proposal.
- The sample produces `WouldExecute`, not a real external side effect.
- Audit stages retain the same correlation identifier.

---

# Completion Criteria

You have completed the lab when you can answer:

1. Why is a model-generated tool call a proposal rather than authority?
2. Why should the host own the tool registry?
3. Which facts in this sample are intentionally reconstructed by the host?
4. Why is prompt compliance not equivalent to execution enforcement?
5. Why should the proposer not automatically satisfy acknowledgment?
6. Why should acknowledgment be followed by re-evaluation when context can change?
7. Which capability bindings preserve the original decision scope?
8. Why should capability validation occur near the handler?
9. What does the in-memory use store demonstrate, and what does it not guarantee?
10. Why should credentials remain host-owned?
11. Which failure modes should block or defer execution in your environment?
12. What evidence would you need to reconstruct a disputed tool invocation?
13. Which threats remain outside the scope of this teaching sample?
14. When would a simpler architecture be preferable?

## Optional Extension — Add a Second Narrow Tool

Add a second semantic operation such as:

```text
case.note.create
```

Do not add a generic shell or arbitrary HTTP tool.

Give the second tool different:

- Required arguments.
- Policy rules.
- Audience.
- Capability scope.
- Risk level.

Then prove that a capability for:

```text
notification.send
```

cannot execute:

```text
case.note.create
```

This reinforces the relationship between tool design and least authority.

## Optional Extension — Simulate Policy Failure

Introduce a policy provider abstraction that can return:

```text
Available
Unavailable
```

Create tests for the host's explicit failure behavior.

Do not implement:

```text
Unavailable = Allowed
```

unless you are deliberately demonstrating a fail-open design and documenting the consequence.

## Optional Extension — Durable Replay Design

Do not implement a production database solely for this lab.

Instead, write a short design note describing how you would replace `InMemoryCapabilityUseStore` with a durable atomic store.

Address:

- Key choice.
- Atomic consume operation.
- Expiration cleanup.
- Multiple instances.
- Regional deployment.
- Failure behavior.
- Observability.

Then compare that design with the teaching `HashSet`.

## Resetting the Sample

Inspect your work before discarding lab changes:

```bash
git status
git diff
```

To restore the baseline sample:

```bash
git restore samples/governed-ai-tool-gateway
```

---

## Related Content

- [Governed AI Tool Gateway tutorial](../tutorials/governed-ai-tool-gateway.md) — review the complete architectural reasoning behind the capstone pattern.
- [Governed AI Tool Gateway sample](https://github.com/AsiBackbone/Learning/blob/main/samples/governed-ai-tool-gateway/README.md) — return to the executable baseline used by this lab.
- [Scoped Capability and Host-Owned Execution lab](scoped-capability-and-host-owned-execution.md) — revisit capability binding, expiration, stale authority, and replay concepts in isolation.
- [Acknowledgment and Audit Residue lab](acknowledgment-and-audit-residue.md) — revisit responsibility and evidence boundaries before they are composed into AI tool execution.
- [AI Agent Gateway Scenario](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/scenarios/ai-agent-gateway.md) — compare the teaching gateway with the working framework's scenario documentation.
- [Human Approval Before AI Tool Execution](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/scenarios/human-approval-before-ai-tool-execution.md) — compare acknowledgment handling with the implementation-oriented guidance.
- [Capability Grant Hardening](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/capability-grant-hardening.md) — examine production-oriented proof, replay, time, binding, and failure considerations.
- [Foundational Tutorial Index](../tutorials/index.md) — revisit the complete five-tutorial sequence.

---

> **Read it. Run it. Question it. Improve it.**
