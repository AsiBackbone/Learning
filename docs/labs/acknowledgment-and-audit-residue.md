# Lab — Acknowledgment and Audit Residue

**Learning objective:** Practice treating acknowledgment as a narrowly bound governance event rather than permission, preserving re-evaluation after acknowledgment, and maintaining a correlated audit timeline that distinguishes decisions, acknowledgments, and execution outcomes.

**Difficulty:** Intermediate
**Prerequisites:** Complete the [Acknowledgment and Audit Residue tutorial](../tutorials/acknowledgment-and-audit-residue.md) and run the [Acknowledgment and Audit Residue sample](https://github.com/AsiBackbone/Learning/blob/main/samples/acknowledgment-and-audit-residue/README.md).

This lab builds directly on the third foundational tutorial and its executable companion sample.

The tutorial explains the acknowledgment boundary and the purpose of structured audit residue.

The sample demonstrates five deterministic workflows, including rejected, mismatched, expired, successful, and context-drift paths.

This lab asks you to **break the boundary, strengthen the binding, preserve evidence beyond a local list, and reason about stale state and execution failure**.

> **Acknowledgment should satisfy a specific requirement without becoming a policy override or standing execution authority.**

---

## Starting Architecture

The companion sample uses this flow:

```text
Intent
   ↓
Policy evaluation
   ↓
AcknowledgmentRequired
   ↓
Challenge issued
   ↓
Actor response
   ↓
Response validation
   ↓
Current context reconstructed
   ↓
Policy re-evaluated
   ↓
Host-owned execution or stop
   ↓
Audit residue
```

The important invariants are:

```text
Rejected / mismatched / expired acknowledgment
   ↓
Executor invocation count = 0
```

and:

```text
Valid acknowledgment
   ↓
Re-evaluate current context
   ↓
Unrelated constraint still applies
```

and:

```text
Decision
≠
Acknowledgment
≠
Execution result
```

The sample intentionally keeps all state in memory and uses a recording executor. No real account is disabled.

## Prepare the Lab

Work on a temporary branch or disposable copy of the repository so you can modify the sample safely.

For example:

```bash
git switch -c lab/acknowledgment-audit-residue
```

From the repository root, run the companion sample before making changes:

```bash
dotnet run --project samples/acknowledgment-and-audit-residue/AcknowledgmentAndAuditResidue/AcknowledgmentAndAuditResidue.csproj
```

Before continuing, locate these elements in `Program.cs`:

1. `RunScenario`
2. `AcknowledgmentChallenge`
3. `AcknowledgmentResponse`
4. `AcknowledgmentValidator`
5. `DisableAccountPolicyContext`
6. `DisableAccountPolicy`
7. `AuditResidue`
8. `AddDecisionResidue`
9. `AddResidue`
10. `RecordingExecutor`
11. `WorkflowScenario`
12. `VerifyScenario`

You should be able to explain which parts represent **policy**, which represent **acknowledgment state**, which represent **evidence**, and which component owns the side effect.

---

# Part 1 — Turn Acknowledgment into a Policy Bypass

The current sample validates a successful acknowledgment and then reconstructs current context before evaluating policy again.

Temporarily break that boundary.

After a valid acknowledgment is accepted, change the flow so the executor runs immediately instead of performing the re-evaluation step.

Conceptually, create the unsafe path:

```text
Acknowledgment accepted
   ↓
Execute immediately
```

instead of:

```text
Acknowledgment accepted
   ↓
Reconstruct current context
   ↓
Re-evaluate policy
   ↓
Execute only if current decision permits
```

Run the sample.

The `Context drift after acknowledgment` scenario should now expose the problem. That scenario validly acknowledges the original requirement, but the resource becomes protected before continuation.

If the host treats acknowledgment as permission, the executor can be reached even though the current policy would recommend escalation.

The sample's invariant verification should fail rather than silently accepting that behavior.

## Explain the Failure

Answer:

1. Was the acknowledgment itself invalid?
2. Which fact changed after the acknowledgment?
3. Which policy rule should still apply?
4. Why is a valid acknowledgment insufficient to prove that execution remains appropriate?
5. What is the difference between satisfying the original acknowledgment requirement and satisfying all current policy constraints?

Restore the original re-evaluation path before continuing.

The lesson is:

> **Acknowledgment can satisfy one condition without freezing the rest of the policy context.**

---

# Part 2 — Add Another Binding Failure

The sample already rejects a response from the wrong actor and an expired challenge.

Add a deterministic scenario that changes the response correlation identifier.

Extend `ResponseMode` with a value such as:

```csharp
WrongCorrelation
```

Update `CreateResponse` so that mode produces a response with a correlation identifier different from the challenge.

Add a scenario expecting:

```text
Final state: AcknowledgmentInvalid
Executor invocations: 0
Final audit stage: acknowledgment-invalid
Reason code: acknowledgment.correlation-mismatch
```

Run the sample and verify that the executor remains untouched.

## Why Correlation Is Not Just Logging Metadata

Discuss:

- Why should the response remain connected to the same governed workflow?
- What could happen if a valid response from one workflow were accepted for another?
- Why is `ChallengeId` still important even when correlation is also checked?
- Is correlation by itself sufficient as a security boundary?

Correlation helps connect the story.

It does not replace actor binding, challenge identity, acknowledgment code, expiration, or policy validation.

---

# Part 3 — Expose Replay as a State Problem

The sample validates a response as data, but it intentionally omits durable challenge-consumption state.

Use the successful scenario to ask a new question:

> What happens if the same accepted acknowledgment response is submitted twice?

Call the validator twice with the same challenge and response.

With the current stateless validator, both validations can succeed because the validator has no knowledge that the challenge was already consumed.

That does not mean the validator is incorrect for the current teaching scope.

It demonstrates that replay resistance requires state outside the pure field comparisons.

## Add One-Time Consumption

Introduce a small in-memory abstraction for challenge consumption.

One possible shape is:

```csharp
public interface IAcknowledgmentConsumptionStore
{
    bool TryConsume(string challengeId);
}
```

A simple implementation may use a `HashSet<string>`.

Your design should make this invariant executable:

```text
First valid acknowledgment
   ↓
Challenge consumed
   ↓
Continuation may proceed

Same response submitted again
   ↓
Challenge already consumed
   ↓
Continuation blocked
```

Use a stable reason code such as:

```text
acknowledgment.already-consumed
```

Do not turn the store into execution authority.

The store answers whether the acknowledgment challenge may be consumed.

Policy and the host still determine whether execution may occur.

## Reason About Process Restart

Now create a **new** in-memory consumption store and ask:

> Does it remember that the challenge was consumed?

It should not.

That reveals the next architectural boundary:

```text
In-memory replay protection
≠
Durable replay protection
```

You do not need a database for this lab. The objective is to identify where persistence becomes necessary.

---

# Part 4 — Preserve Audit Residue Behind a Store Boundary

The sample currently builds a local `List<AuditResidue>` inside each workflow.

That makes the lifecycle easy to observe, but the list disappears with the process.

Introduce a small evidence-store abstraction.

For example:

```csharp
public interface IAuditResidueStore
{
    void Append(AuditResidue residue);

    IReadOnlyList<AuditResidue> ReadByCorrelationId(
        string correlationId);
}
```

Implement it in memory for the lab.

Refactor the sample so every residue is appended through the store rather than existing only as a local implementation detail.

Preserve these properties:

```text
Append-only use by the workflow
Stable EventId
Stable CorrelationId
Lifecycle Stage
Outcome
Reason codes
Policy version
Occurrence time
```

You may still return a timeline from `RunScenario` for easy display.

The important change is that the evidence has an explicit storage boundary.

## Validate the Evidence

For the valid acknowledgment scenario, read the stored records by correlation identifier and confirm the expected order:

```text
decision
challenge-issued
acknowledgment-accepted
re-evaluation
execution-completed
```

For the context-drift scenario, confirm the timeline ends at:

```text
re-evaluation
```

and contains no `execution-completed` residue.

## Do Not Overclaim the Store

Answer:

1. Is the in-memory store durable?
2. Is it immutable?
3. Is it tamper-evident?
4. Does append-only usage in application code prove that an administrator cannot alter memory or storage?
5. What would need to be added before stronger claims such as durable, signed, or tamper-evident evidence would be justified?

A storage abstraction creates a deliberate persistence boundary.

It does not magically create persistence guarantees.

---

# Part 5 — Separate an Allowed Decision from Execution Failure

The sample currently records:

```text
re-evaluation = Allowed
```

and then:

```text
execution-completed
```

Now introduce an execution failure.

Modify `RecordingExecutor` so one scenario can simulate a failure. You may throw an exception or return an explicit execution result.

Add a workflow scenario where:

```text
Acknowledgment is valid
Policy re-evaluation = Allowed
Execution is attempted
Execution fails
```

The expected evidence should preserve both facts:

```text
Policy decision = Allowed
```

and:

```text
Execution outcome = Failed
```

Add a final residue such as:

```text
Stage: execution-failed
Outcome: ExecutionFailed
ReasonCode: account.disable.execution-failed
```

Do **not** rewrite the prior `Allowed` decision as `Denied` merely because the executor failed.

Those events answer different questions.

## Explain the Distinction

Answer:

- What did policy decide?
- What did the executor attempt?
- What actually happened?
- Why would combining those facts into one record make later investigation harder?
- Should an execution exception erase the acknowledgment evidence that came before it?

The governed path is a timeline, not a single final sentence.

---

# Part 6 — Reason About Policy Identity Drift

The challenge currently carries:

```text
PolicyVersion = 3.2
```

and the reconstructed context also carries policy identity into re-evaluation.

Create an experiment where the policy version changes after challenge issuance but before continuation.

For example:

```text
Challenge issued under policy 3.2
   ↓
Actor acknowledges
   ↓
Current policy identity becomes 3.3
   ↓
Re-evaluation occurs
```

Do not assume there is one universally correct response.

Choose and defend one of these designs:

### Option A — Reject Stale Acknowledgment

Require the challenge policy identity to match the current policy identity before acknowledgment can satisfy the requirement.

This favors tight binding but may create more user friction during policy deployment.

### Option B — Accept the Acknowledgment, Re-evaluate Under Current Policy

Treat the acknowledgment as evidence that the actor accepted the condition presented under 3.2, then evaluate the operation under 3.3 before execution.

This preserves the acknowledgment history while allowing current policy to remain authoritative.

### Option C — Risk-Based Choice

Allow the policy to determine whether a version change invalidates the challenge based on operation risk or the nature of the policy change.

Whichever design you choose, preserve enough evidence to distinguish:

```text
Policy identity that produced the challenge
```

from:

```text
Policy identity used for current re-evaluation
```

A single mutable `PolicyVersion` field may no longer be enough if those identities can legitimately differ.

---

# Part 7 — Review the Evidence Surface

Inspect the final `AuditResidue` model and your in-memory store.

For each field, classify it as one of:

```text
Identity
Decision explanation
Correlation
Lifecycle state
Policy provenance
Operational detail
```

Then identify information that should **not** be copied into residue merely because it is available.

Examples include:

- Full authentication tokens
- Secrets
- Entire request bodies
- Complete account objects
- Sensitive personal data unrelated to the decision
- Unredacted model prompts

Answer:

1. What is the minimum evidence needed to explain this workflow?
2. Which identifiers are sufficient to retrieve fuller context from an appropriately protected system later?
3. Which data would unnecessarily increase breach impact if copied into every audit event?
4. What retention policy questions appear once the evidence becomes durable?

Governance evidence should be useful and intentional, not exhaustive by default.

---

# Final Validation

Run the modified sample and confirm all of the following:

- The baseline acknowledgment scenarios still behave as intended.
- Context drift after acknowledgment still cannot reach execution.
- Wrong correlation is rejected with a stable reason code.
- Replaying an already consumed acknowledgment is blocked while consumption state exists.
- Recreating the in-memory consumption store demonstrates why durable replay protection is a separate concern.
- Audit residue is appended through an explicit store boundary.
- Stored records can be read back by correlation identifier in lifecycle order.
- A failed executor produces `execution-failed` evidence without changing the earlier policy decision into a denial.
- Policy identity drift is handled according to a documented rule.
- Acknowledgment never becomes standing permission or direct execution authority.

The final architecture should still preserve:

```text
Acknowledgment
   ↓
Validation
   ↓
Current policy evaluation
   ↓
Host-controlled execution decision
```

not:

```text
Acknowledgment
   ↓
Automatic execution
```

---

# Completion Criteria

You have completed the lab when you can demonstrate and explain this progression:

```text
AcknowledgmentRequired decision
        ↓
Narrow challenge
        ↓
Bound response
        ↓
Validation
        ↓
One-time consumption
        ↓
Current context reconstruction
        ↓
Policy re-evaluation
        ↓
Host-owned execution attempt
        ↓
Distinct execution result
        ↓
Correlated audit residue
```

You should also be able to explain why each of these statements is different:

```text
The actor acknowledged the condition.
The current policy allows the operation.
The host attempted execution.
The operation completed successfully.
```

A trustworthy governance timeline preserves those distinctions rather than collapsing them into one boolean or one log message.

## Optional Extension — Simulate Restart Boundaries

Create three separate in-memory stores:

```text
Challenge store
Consumption store
Audit residue store
```

Run a workflow through challenge issuance, then construct new workflow objects while selectively preserving or replacing each store.

Ask:

- If challenge state disappears, can the response still be validated?
- If consumption state disappears, can a response be replayed?
- If audit state disappears, can the governed history be reconstructed?
- Which states must be durable in your chosen risk model?
- Which states should have different retention periods?

This exercise prepares for production-oriented persistence patterns without requiring a database or external service.

## Resetting the Sample

If you created a temporary branch only for the exercise, inspect your changes before discarding them:

```bash
git status
git diff
```

To restore the companion sample:

```bash
git restore samples/acknowledgment-and-audit-residue/AcknowledgmentAndAuditResidue/Program.cs
```

Use `git status` first so you understand which local work will be affected.

---

## Related Content

- [Acknowledgment and Audit Residue tutorial](../tutorials/acknowledgment-and-audit-residue.md) — review the architectural reasoning behind the lab.
- [Acknowledgment and Audit Residue sample](https://github.com/AsiBackbone/Learning/blob/main/samples/acknowledgment-and-audit-residue/README.md) — return to the executable baseline used by this exercise.
- [Policy Context and Explicit Decision Outcomes lab](policy-context-and-explicit-decision-outcomes.md) — revisit explicit decision inputs, reason codes, and precedence.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — continue from acknowledged governance requirements into narrow execution authority.
- [Foundational Tutorial Index](../tutorials/index.md) — view the complete foundational learning path.
- [`LiabilityHandshakeRequest`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Handshakes/LiabilityHandshakeRequest.cs) — compare the teaching challenge with the fuller framework handshake request.
- [`LiabilityHandshakeAcknowledgment`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Handshakes/LiabilityHandshakeAcknowledgment.cs) — inspect the working accepted/rejected acknowledgment model.
- [`AuditResidue`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Audit/AuditResidue.cs) — compare the lab's small evidence model with the framework's richer governance residue.
- [`Dynamic Liability Handshake`](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/dynamic-liability-handshake.md) — review the fuller handshake lifecycle.
- [`Durable Audit Outbox Persistence`](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/durable-audit-outbox-persistence.md) — study production-oriented durability and delivery concerns after completing the in-memory exercise.

---

> **Read it. Run it. Question it. Improve it.**
