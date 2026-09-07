---
description: Why writing a log line after an operation does not prove what a .NET system decided, when it decided it, or whether the record survived unchanged.
title: Your Audit Log Records the Story, Not the Decision
author: Christopher D. Cavell
published: "2026-09-07"
summary: An audit line written after execution describes an outcome; evidence requires a recorded decision, a binding to the operation it authorized, and integrity that outlives the process that wrote it.
feed: true
---

# Your Audit Log Records the Story, Not the Decision

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** Familiarity with structured logging in .NET is helpful. No AsiBackbone package, external audit product, or prior Learning material is required.

**What this article covers:** why an after-the-fact log line is narration rather than evidence; the four properties an auditable record needs; where ordinary `ILogger` output stops being sufficient; how to bind a record to the decision it describes; and when a simple log really is the right answer.

A reviewer asks a fair question after an incident:

> **Who approved this operation, on what basis, and can you show me?**

The team opens the logs and finds exactly what the code was written to produce:

```csharp
await _accountService.DisableAsync(accountId, ct);

_logger.LogInformation(
    "Account {AccountId} disabled by {UserId}",
    accountId,
    currentUser.Id);
```

That line is genuinely useful. It is not yet an answer.

It records that a code path reached the logging statement after calling `DisableAsync`. It does not record which rule permitted the operation, what the system believed at the moment it decided, whether the operation actually completed, or whether the text a reviewer is reading now is the text the process originally emitted.

The point is not that logging is wrong. The point is that logging and evidence are different jobs, and one statement is often asked to do both.

## Narration and Evidence Are Different Artifacts

A log line is a statement produced by a program about itself, for humans and tools that already trust the program.

An evidentiary record is a statement that must remain meaningful to a reader who does **not** already trust the process that produced it — a compliance reviewer, an incident responder, an opposing party, or the same team eighteen months later after four refactors.

That difference produces four requirements the ordinary log line does not satisfy by default:

| Property | Question it answers | Typical log line |
| --- | --- | --- |
| **Decision capture** | What was decided, and on what inputs? | Absent; only the outcome narration survives |
| **Binding** | Which specific operation does this record describe? | Correlated by convention at best |
| **Ordering** | Was this written before or after the effect? | Implicit in code position |
| **Integrity** | Is this the record that was originally written? | Whatever the log sink happens to provide |

Each property can be added independently, and each has a real cost. The mistake is assuming that adding a structured logging call satisfies all four.

## Failure 1 — The Record Describes the Outcome, Not the Decision

Return to the opening example. The interesting information — why the operation was allowed — is nowhere in the record.

Suppose the policy is: an administrator may disable an account unless the account is classified as protected, in which case a second approver is required. That rule lives somewhere in the call stack. The log line preserves none of it.

Six months later, a reviewer asks whether the account was protected at the time. The answer requires reconstructing state from a database whose rows have since changed, from a policy whose implementation has since been edited, in a codebase whose call graph has since moved.

A record that captures the decision looks different:

```csharp
public sealed record AccessDecision(
    string OperationId,
    string Operation,
    string SubjectId,
    string ResourceId,
    string Outcome,
    string ReasonCode,
    string PolicyVersion,
    IReadOnlyDictionary<string, string> Inputs,
    DateTimeOffset DecidedAt);
```

The important fields are the ones that are easy to omit:

- **`ReasonCode`** — a stable identifier for *why*, not a formatted English sentence that a later refactor will silently reword.
- **`PolicyVersion`** — the version of the rule that produced the outcome, so a reviewer is not forced to assume today's policy is the one that ran.
- **`Inputs`** — the facts the decision consumed, captured at decision time rather than re-read later. This is the field that answers "was the account protected *then*?"

`Inputs` is also where sensitive data leaks into permanent storage most easily. Capture the classification (`"protected": "true"`), not the record that produced it. [Secure Logging Across Trust Boundaries](../../security/secure-logging-across-trust-boundaries.md) covers what belongs on either side of that line.

### Reason Text Is Not a Reason Code

A frequent shortcut:

```csharp
_logger.LogWarning("Denied: account is protected and requires a second approver");
```

That string is a user-interface concern that has been promoted to an evidentiary field. It cannot be queried reliably, it cannot be compared across versions, and it will change the first time someone improves the wording. Emit a code and render the sentence separately:

```csharp
new AccessDecision(
    OperationId: operationId,
    Operation: "account.disable",
    SubjectId: currentUser.Id,
    ResourceId: accountId,
    Outcome: "Denied",
    ReasonCode: "protected_account.second_approver_required",
    PolicyVersion: "account-policy/3",
    Inputs: new Dictionary<string, string>
    {
        ["account.protected"] = "true",
        ["approvals.count"] = "1",
        ["approvals.required"] = "2",
    },
    DecidedAt: _clock.UtcNow);
```

Human-readable text can be produced from the code at display time and improved freely, because the evidentiary field is the code.

## Failure 2 — Nothing Binds the Record to the Operation

Two records exist:

```text
14:22:07  Decision: account.disable Allowed  subject=u-4471  resource=a-9920
14:22:09  Account a-9920 disabled
```

A reader connects them because the identifiers match and the timestamps are close. That connection is an inference, not a recorded fact.

It breaks in ordinary conditions: a retry produces a second execution against one decision, concurrent requests interleave, a partial failure leaves the decision recorded and the effect never applied, or a background worker executes a decision made minutes earlier. In each case the log file still reads plausibly.

Binding means the executing code carries the decision's identity and stamps it on the effect:

```csharp
var decision = await _decisions.EvaluateAsync(request, ct);
await _decisionLog.RecordAsync(decision, ct);

if (decision.Outcome is not "Allowed")
{
    return Result.Denied(decision.ReasonCode);
}

await _accountService.DisableAsync(
    accountId,
    decisionId: decision.OperationId,
    ct);
```

Now the effect and the record share an identifier that neither side invented on its own. A reviewer can ask which decision authorized a given change and get an answer rather than a correlation.

This also makes a specific bug detectable: a decision recorded with no corresponding execution, or an execution stamped with a decision identifier that was never recorded as allowed. Both are silent under time-proximity correlation.

## Failure 3 — The Record Is Written After the Effect

Ordering is the property most often lost to convenience. Writing the record after the operation is easier — the outcome is known, the identifiers are all in scope, and there is nothing to reconcile.

It also means every failure mode between the effect and the log call produces an unrecorded effect. The process is terminated, the log sink is unreachable, an exception unwinds past the logging statement, the container is evicted mid-request. The account is disabled; the record does not exist.

The inverse ordering has the opposite failure: a recorded decision whose effect never happened. That failure is preferable, because it is visible. An extra record with no matching effect is a reconcilable discrepancy. A missing record is indistinguishable from nothing having happened.

So the ordering rule is:

> **Record the decision before the protected effect begins; record completion separately.**

Two records, not one:

```text
decision   op=7f3c  account.disable  Allowed   policy=account-policy/3
completion op=7f3c  account.disable  Succeeded
```

The gap between them is not a defect in the design. It is the observable window where a system can be reconciled, and it is why an outbox or a durable queue is a common companion to this pattern — the completion record is written through the same transactional boundary as the effect, rather than hoped for afterwards.

If you are already familiar with the argument that a decision must resolve before protected work begins, this is the recording half of the same boundary. [Your Authorization Check Runs Too Late](authorization-check-runs-too-late.md) covers the decision half.

## Failure 4 — Integrity Ends at the Log Sink

The remaining question is the one that separates a record from evidence:

> **Is this the record that was originally written?**

For most application logging the honest answer is: as trustworthy as whoever can write to the sink. Operators can usually delete log streams, retention policies discard older entries on a schedule, and an attacker with the access needed to perform an unauthorized operation frequently has the access needed to remove the line describing it.

That is not a scandal — it is a design choice, and often the correct one. It becomes a problem only when a record with ordinary log-sink integrity is presented as tamper-evident.

The controls that change the answer, in increasing cost order:

- **Separate the store.** Write decision records to a destination the operational application cannot rewrite. Append-only tables with revoked `UPDATE`/`DELETE` permissions, or a separate account with a distinct trust boundary, remove the easiest class of silent edit.
- **Chain the records.** Include the hash of the previous record in each new one. A removed or altered record breaks the chain at a detectable point. This proves *tampering occurred*; it does not prove *what the original said*.
- **Sign the records.** A signature binds a record to a signing identity, which shifts the question to key custody: who can sign, where the key lives, and what happens after a compromise. A signature produced by an over-exposed signing path proves only that the exposed path signed it.
- **Anchor externally.** Periodically publish a chain digest to a store outside the system's control, so an attacker who can rewrite both records and chain cannot also rewrite history that has already left the building.

Each step answers a narrower question than teams usually assume. "Signed" is not one claim; [Signing, Verification, Key Custody, and Tamper Evidence](../../security/signing-verification-key-custody-and-tamper-evidence.md) separates integrity, authenticity, trust anchors, and verification policy. For the chained and durable end of this spectrum, [Durable Decision Ledgers and Cryptographic Audit Chains](../../advanced/durable-decision-ledgers-and-cryptographic-audit-chains.md) goes further.

A control earns its place when someone can state which of those questions it answers. Hash-chaining records that anyone can delete wholesale adds ceremony without adding evidence.

## Test the Record, Not Just the Outcome

Audit records are unusually prone to silent decay. They are rarely asserted on, so a refactor that drops a field, reorders the write, or moves it past an early return produces green tests and an empty audit trail. The absence is discovered during the incident that needed it.

Assert on the record the same way you assert on behavior:

```csharp
[Fact]
public async Task Denied_operation_records_decision_and_does_not_execute()
{
    var records = new RecordingDecisionLog();
    var executor = new RecordingExecutor();
    var sut = new AccountWorkflow(records, executor, ProtectedAccountPolicy);

    var result = await sut.DisableAsync("a-9920", Administrator, CancellationToken.None);

    Assert.Equal(Outcome.Denied, result.Outcome);
    Assert.Empty(executor.Calls);

    var decision = Assert.Single(records.Written);
    Assert.Equal("Denied", decision.Outcome);
    Assert.Equal("protected_account.second_approver_required", decision.ReasonCode);
    Assert.Equal("account-policy/3", decision.PolicyVersion);
}
```

Three assertions matter here and are easy to skip. `Assert.Empty(executor.Calls)` proves the effect did not occur — a denied result alone does not, as [How to Test That a Denied Operation Never Executes](test-denied-operation-never-executes.md) works through in detail. `ReasonCode` pins the stable identifier so a reworded message cannot quietly change the evidentiary field. `PolicyVersion` fails the test when a policy changes without anyone considering the recorded provenance.

Ordering deserves its own test when the ordering is load-bearing:

```csharp
[Fact]
public async Task Decision_is_recorded_before_the_protected_effect_runs()
{
    var timeline = new List<string>();
    var records = new RecordingDecisionLog(onWrite: () => timeline.Add("decision"));
    var executor = new RecordingExecutor(onExecute: () => timeline.Add("effect"));

    await new AccountWorkflow(records, executor, PermissivePolicy)
        .DisableAsync("a-1001", Administrator, CancellationToken.None);

    Assert.Equal(new[] { "decision", "effect" }, timeline);
}
```

That test exists to fail during a future refactor. It is the only thing standing between the intended ordering and the convenient one.

## When an Ordinary Log Really Is Enough

Most operations in most systems do not need any of this.

A structured log line is the right answer when the record's job is operational — diagnosing behavior, tracing a request, understanding load — and when no one will later be asked to prove what happened to a reader who does not trust the process.

Reach for a decision record when at least one of these is true:

- An external reviewer, regulator, or counterparty may need to reconstruct the decision.
- The operation is irreversible or expensive enough that "did we actually authorize this?" is a question with consequences.
- The decision depends on state that will have changed by the time anyone looks.
- Execution is deferred, delegated, or crosses a process boundary, so the decision and the effect are separated in time.
- The person able to perform the operation is also able to edit the log.

Reach for chaining, signing, or external anchoring only when the threat model includes someone who can modify the record store — and when you can name who that is.

Adding an audit subsystem to a workflow that needed a log line is its own failure. It produces storage cost, retention obligations, privacy exposure, and a false impression of rigor.

## A Short Review Checklist

1. **Does the record contain the decision, or only the outcome?** If a reviewer cannot see the rule and the inputs, the record narrates rather than evidences.
2. **Is the reason a stable code?** Prose fields drift; codes survive rewording.
3. **Is the policy version recorded?** Without it, every reconstruction silently assumes today's rules.
4. **Are the inputs captured at decision time?** State re-read later is a different fact.
5. **Is the record bound to the operation by an identifier, not by timestamp proximity?**
6. **Is the decision written before the protected effect?** If not, every crash in between erases the record and keeps the effect.
7. **Is completion recorded separately from the decision?** One combined record cannot distinguish "decided" from "done".
8. **Who can modify or delete the store?** If the answer includes the application performing the operation, the record has operational integrity, not tamper evidence.
9. **Does a test assert on the record's fields and ordering?** Unasserted audit fields decay silently.
10. **Can you state, in one sentence, which question each integrity control answers?** If not, remove the control or learn what it proves.

## Continue Deeper

To follow the decision half of this boundary — resolving the full decision before any protected side effect begins — continue with [Your Authorization Check Runs Too Late](authorization-check-runs-too-late.md).

When the record must survive an adversary rather than an accident, [Signing, Verification, Key Custody, and Tamper Evidence](../../security/signing-verification-key-custody-and-tamper-evidence.md) separates the claims that "signed" collapses together, and [Durable Decision Ledgers and Cryptographic Audit Chains](../../advanced/durable-decision-ledgers-and-cryptographic-audit-chains.md) covers chained and durable ledger construction.

If the open question is which fields may safely be written at all, [Secure Logging Across Trust Boundaries](../../security/secure-logging-across-trust-boundaries.md) addresses sensitive data in records that outlive the request.

For policy version and provenance specifically — how a recorded decision stays interpretable after the policy changes — see [Policy Versioning and Decision Provenance](../../governance/policy-versioning-and-decision-provenance.md).

The rule to keep is narrow: **a log line proves your code reached a logging statement.** Everything a reviewer wants beyond that — what was decided, on what basis, in what order, and whether the text is unchanged — is a property you have to design in deliberately, and only where it is worth its cost.
