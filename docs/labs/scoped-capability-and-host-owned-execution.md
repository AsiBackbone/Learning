---
description: Practice carrying narrow, short-lived authority from an allowed decision to host-owned execution while detecting expired, mismatched, or stale capabilities.
---

# Lab — Scoped Capability and Host-Owned Execution

**Learning objective:** Practice preserving narrow, short-lived execution authority across the transition from an allowed decision to a host-owned side effect, and detect when execution authority has become expired, mismatched, or stale.

**Difficulty:** Intermediate

**Prerequisites:** Complete the [Scoped Capability and Host-Owned Execution tutorial](../tutorials/scoped-capability-and-host-owned-execution.md) and run the [Scoped Capability and Host-Owned Execution sample](https://github.com/AsiBackbone/Learning/blob/main/samples/README.md#scoped-capability-and-host-owned-execution).

This lab builds directly on the fourth foundational tutorial and its executable companion sample.

The tutorial explains why approval should not silently become broad or permanent authority.

The sample demonstrates capability issuance after an allowed decision, execution-boundary validation, short expiration, actor/operation/resource/audience binding, relevant resource-state binding, and host-owned execution.

This lab asks you to **break those boundaries deliberately, repair them, and reason about stale authority and replay state**.

> **Validate where authority becomes action.**

---

## Starting Architecture

The companion sample uses this flow:

```text
Governance decision
   ↓
Capability issued
   ↓
Execution context constructed
   ↓
Capability validated
   ↓
Host-owned gateway
   ↓
Executor or stop
```

The important invariants include:

```text
Blocked decision
   ↓
No execution capability
```

```text
Expired capability
   ↓
Executor invocation count = 0
```

```text
Resource changed after approval
   ↓
Stale resource binding
   ↓
Executor invocation count = 0
```

The sample uses deterministic local data and a recording executor. No real account is disabled. The `CapabilityValidationRequest` stands for current host-built execution context; treat those facts as authoritative host inputs, not as values an untrusted caller may choose to make a capability validate.

## Prepare the Lab

Work on a temporary branch or disposable copy of the repository.

```bash
git switch -c lab/scoped-capability-execution
```

Run the sample and its focused tests before making changes:

```bash
dotnet run --project samples/scoped-capability-and-host-owned-execution/ScopedCapabilityAndHostOwnedExecution/ScopedCapabilityAndHostOwnedExecution.csproj

dotnet test samples/scoped-capability-and-host-owned-execution/ScopedCapabilityAndHostOwnedExecution.Tests/ScopedCapabilityAndHostOwnedExecution.Tests.csproj
```

Locate these types in `Program.cs`:

1. `GovernanceDecision`
2. `ExecutionCapability`
3. `ExecutionCapabilityFactory`
4. `CapabilityValidationRequest`
5. `ExecutionCapabilityValidator`
6. `DisableAccountGateway`
7. `RecordingDisableAccountExecutor`

You should be able to explain which component decides, which component describes narrow authority, which component validates authority, and which component owns the side effect.

---

# Part 1 — Turn a Narrow Decision into Standing Permission

Temporarily replace the capability handoff with a broad flag such as:

```csharp
bool approvedForAccountAdministration = decision.CanProceed;
```

Then allow the gateway to execute any account administration request while that flag is true.

Try changing:

```text
Operation: account.disable
Resource: user-100
```

into:

```text
Operation: account.delete
Resource: user-999
```

while keeping the broad approval flag.

## Explain the Failure

Answer:

1. Which original decision bindings disappeared?
2. How long does the broad permission remain valid?
3. Which gateway may use it?
4. Which resource may it target?
5. If the permission leaks, what is the maximum authority exposed?

Restore the explicit capability model before continuing.

The lesson is:

> **A narrow decision should not become a broad session-wide privilege.**

---

# Part 2 — Break Expiration Enforcement

Remove or bypass the validator's expiration check:

```text
NowUtc >= ExpiresUtc
   ↓
capability.expired
```

Run the focused tests.

`ExpiredCapabilityDoesNotReachExecutor` should fail if the expired capability now reaches execution.

Restore the expiration check.

Then test the exact boundary manually:

```text
NowUtc = ExpiresUtc - one tick
NowUtc = ExpiresUtc
NowUtc = ExpiresUtc + one tick
```

Document the semantics you observe.

The sample intentionally uses an exclusive expiration boundary:

```text
NowUtc < ExpiresUtc
```

## Reason About Clock Skew

Do not solve clock disagreement by silently extending the capability lifetime.

If you add clock-skew tolerance, make the tolerance explicit and answer:

- How much additional authority time does it create?
- Does the same tolerance apply to both not-before and expiration checks?
- Who owns clock synchronization in production?

---

# Part 3 — Remove Resource-State Binding

The sample binds both:

```text
ResourceId
ResourceVersion
```

`ResourceId` prevents target substitution.

`ResourceVersion` is a teaching stand-in for relevant resource-state freshness.

Temporarily remove the `ResourceVersion` comparison from `ExecutionCapabilityValidator`.

Run the tests.

`ResourceChangedAfterApprovalDoesNotReachExecutor` should expose the problem:

```text
Capability issued for user-100 version 7
   ↓
Current user-100 becomes version 8
   ↓
Same ResourceId still matches
   ↓
Without a state binding, stale authority may execute
```

Restore the comparison before continuing.

## Choose a Production-Oriented Binding

For a system you know, choose one possible replacement for the sample's integer version:

- Database row version
- ETag
- Resource revision
- Stable state hash or fingerprint
- Fresh authorization check
- Fresh policy re-evaluation

Explain what change the binding detects and what changes it does **not** detect.

The objective is not to conclude that every capability needs a version integer.

The objective is to recognize that resource identity and resource freshness are separate questions.

---

# Part 4 — Broaden the Operation or Scope

The sample capability is limited to:

```text
OperationName = account.disable
Scope = account.disable
```

Experiment with a broader scope such as:

```text
account.*
```

Then modify the validator so the broader scope authorizes several operations.

Try to execute:

```text
account.delete
account.reset-password
account.change-owner
```

## Evaluate the Authority Expansion

Answer:

1. Was the original policy decision about those operations?
2. Does the broader capability still represent the same approval?
3. Would a separate capability per operation be simpler to reason about?
4. Under what circumstances would a broader scope be justified?

Restore the narrow operation and scope before continuing.

The capability should grant only what the decision actually justified.

---

# Part 5 — Move Validation Too Far Upstream

Create an intentionally weak flow:

```text
Capability validated
   ↓
valid = true
   ↓
Time passes or resource changes
   ↓
Gateway trusts cached boolean
   ↓
Execute
```

For example:

1. Validate while the capability is unexpired and the resource is version 7.
2. Advance time past `ExpiresUtc` **or** change the resource version to 8.
3. Let the executor use the earlier `valid = true` result.

Explain why the earlier validation no longer proves current execution authority.

Restore the design in which `DisableAccountGateway` validates the current request immediately before calling the executor.

The lesson is:

> **Validation should happen as close as practical to the real side effect.**

---

# Part 6 — Add Another Binding Failure

Choose one capability binding not yet represented by a focused test and add coverage for it.

Good options include:

```text
Wrong resource identity
Missing required scope
Wrong policy version
Wrong acknowledgment reference
Wrong intended use
Not-yet-valid time
```

Your test should preserve this shape:

```text
Binding mismatch
   ↓
Stable validation reason code
   ↓
Executed = false
   ↓
Executor invocation count = 0
```

Do not test only the validator return value.

The architectural contract is that the mismatched authority cannot cross the host-owned execution boundary.

---

# Part 7 — Add Single-Use State

Before implementing the exercise, use [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) as the canonical explanation of durable replay state, atomic consumption, distributed races, and failure windows. This lab intentionally keeps the implementation local and in-memory.

The baseline sample deliberately omits replay enforcement.

Add a small in-memory use-store abstraction such as:

```csharp
public interface ICapabilityUseStore
{
    bool TryConsume(string capabilityId);
}
```

Implement it with a `HashSet<string>` for the exercise.

Integrate consumption at the execution boundary so the behavior becomes:

```text
First valid use
   ↓
Capability consumed
   ↓
Execute

Second use of same capability
   ↓
Already consumed
   ↓
No execution
```

Add a stable reason code such as:

```text
capability.already-consumed
```

## Identify the Limit

Now create a **new** in-memory use store and attempt the same capability again.

The new store does not remember prior consumption.

Explain why:

```text
In-memory single-use demonstration
≠
Durable distributed replay protection
```

A production guarantee depends on durable state, concurrency behavior, deployment topology, and atomic consumption semantics owned by the host.

---

# Part 8 — Preserve Separate Evidence Events

Add a minimal evidence timeline for:

```text
Decision allowed
Capability issued
Capability validation succeeded or failed
Execution attempted
Execution completed or blocked
```

Do not collapse them into one record.

A capability may be issued and never used.

A capability may fail validation.

Execution may fail after successful validation.

Your evidence should make those states distinguishable.

For the stale-resource scenario, the timeline should make it possible to observe:

```text
Decision = Allowed
Capability = Issued
Validation = ResourceVersionMismatch
Execution = NotAttempted
```

That is more informative than rewriting the original decision as denied.

---

# Final Validation

Run the sample and test project again.

Confirm:

- A blocked decision cannot mint a capability.
- A valid capability reaches the executor exactly once.
- An expired capability never reaches execution.
- Relevant resource-state drift invalidates the capability.
- Actor, operation, resource, audience, scope, policy, acknowledgment, and intended-use bindings remain explicit.
- Validation occurs at the host-owned execution boundary.
- Validation failure never falls back to execution.
- If you added single-use state, replay is blocked while that state exists.
- You can explain why an in-memory use store does not prove distributed replay protection.
- The capability object itself never performs the side effect.

The final architecture should still preserve:

```text
Decision
   ↓
Scoped capability
   ↓
Current execution context
   ↓
Validation
   ↓
Host-owned execution
```

not:

```text
Decision = Allowed
   ↓
Standing permission
```

---

# Completion Criteria

You have completed the lab when you can explain why each statement answers a different question:

```text
The policy allowed the operation.
A narrow capability was issued.
The capability is still valid for this current execution context.
The host chose to invoke the executor.
The executor completed the operation.
```

You should also be able to answer:

1. Which capability fields bound authority to the original decision?
2. Which checks detect stale authority?
3. Why is resource identity not always enough to detect resource-state drift?
4. Why should expiration normally be short?
5. Why does validation belong near execution?
6. What production guarantees are still host-owned?

## Optional Extension — Add Revocation

Introduce a host-owned revocation store keyed by `CapabilityId`.

Demonstrate:

```text
Capability cryptographically/structurally valid
   +
Not expired
   +
Correct bindings
   +
Revoked by host
   ↓
Execution blocked
```

This reinforces the distinction between artifact validity and current operational acceptability.

## Resetting the Sample

If you created a temporary branch only for the exercise, inspect your work before discarding it:

```bash
git status
git diff
```

To restore the baseline sample:

```bash
git restore samples/scoped-capability-and-host-owned-execution
```

Use `git status` first so you understand which local work will be affected.

---

## Related Content

- [Scoped Capability and Host-Owned Execution tutorial](../tutorials/scoped-capability-and-host-owned-execution.md) — review the architectural reasoning behind the lab.
- [Scoped Capability and Host-Owned Execution sample](https://github.com/AsiBackbone/Learning/blob/main/samples/README.md#scoped-capability-and-host-owned-execution) — return to the executable baseline used by the exercise.
- [Acknowledgment and Audit Residue lab](acknowledgment-and-audit-residue.md) — revisit acknowledgment, re-evaluation, and evidence before execution authority is issued.
- [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) — continue into the end-to-end composition where AI may propose and the host retains execution authority.
- [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) — connect this lab's in-memory single-use exercise to durable, atomic, multi-instance replay protection and idempotency boundaries.
- [Replay Protection and Bounded-Use Authority lab](replay-protection-and-bounded-use.md) — continue from the introductory single-use exercise into a dedicated concurrency race, atomic consume repair, bounded-use contention, and failure-window analysis.
- [`CapabilityTokenGrant`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityTokenGrant.cs) — compare the teaching capability with the working framework model.
- [`CapabilityGrantValidator`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityGrantValidator.cs) — inspect fuller execution-boundary validation.
- [`ICapabilityGrantUseStore`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/ICapabilityGrantUseStore.cs) — compare the lab's in-memory replay exercise with the provider-neutral production seam.
- [Capability Grant Hardening](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/capability-grant-hardening.md) — review proof, binding, replay, time, and failure-handling guidance.
- [Foundational Tutorial Index](../tutorials/index.md) — view the complete foundational learning path.

---

> **Read it. Run it. Question it. Improve it.**
