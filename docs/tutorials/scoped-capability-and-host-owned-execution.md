# Scoped Capability and Host-Owned Execution

**Learning objective:** Understand why an allowed or acknowledged operation should not automatically become broad execution authority, and how a short-lived, narrowly scoped capability can preserve a clear host-controlled execution boundary.

**Difficulty:** Intermediate  

**Prerequisites:** [Decision Before Execution](decision-before-execution.md), [Policy Context and Explicit Decision Outcomes](policy-context-and-explicit-decision-outcomes.md), and [Acknowledgment and Audit Residue](acknowledgment-and-audit-residue.md)

## Pattern Card

> **Problem:** An allowed decision can accidentally expand into broad, reusable, or stale execution authority that no longer matches the operation originally approved.
>
> **Pattern:** Mint narrowly scoped, short-lived authority only from an allowed decision, then validate its bindings against current host context immediately before host-owned execution.
>
> **Use when:** Execution authority crosses a layer, process, gateway, time window, or trust boundary and must remain bound to a specific actor, operation, resource, audience, policy state, or acknowledgment.
>
> **Prefer something simpler when:** The same trusted host immediately performs the approved operation under current authorization and no delegated, reusable, or time-separated execution authority exists.
>
> **Observe:** A blocked decision cannot mint execution authority, and expired or stale authority never reaches the executor.

This is the fourth foundational tutorial in ASI Backbone Learning.

It builds on:

1. [Decision Before Execution](decision-before-execution.md)
2. [Policy Context and Explicit Decision Outcomes](policy-context-and-explicit-decision-outcomes.md)
3. [Acknowledgment and Audit Residue](acknowledgment-and-audit-residue.md)

The earlier tutorials established that a consequential action should be proposed, evaluated, and—when necessary—acknowledged before execution.

This tutorial asks the next question:

> **Once an operation is allowed, what exact authority should exist to perform it?**

The core flow becomes:

```text
Intent
   ↓
Context
   ↓
Constraints
   ↓
Decision
   ↓
Acknowledgment when required
   ↓
Scoped capability
   ↓
Capability validation
   ↓
Host-owned execution
   ↓
Audit residue
```

The central principle is:

> **Approval should not silently become broad or permanent authority.**

## The Problem

Suppose a policy evaluates this operation:

```text
Disable account user-123
```

and returns:

```text
Allowed
```

What does that actually authorize?

A weak implementation may interpret the result as:

```text
The caller can disable accounts.
```

But that is much broader than the original decision.

The decision may have been about:

```text
Actor:
admin-42

Operation:
account.disable

Resource:
user-123

Policy:
3.2

Acknowledgment:
ack-77

Time:
now
```

Turning that narrow decision into a reusable administrative permission creates an authority expansion.

The problem is not merely whether the original decision was correct.

The problem is preserving its **scope** when the system crosses from decision into execution.

## Approval Is Not Authority

A useful distinction is:

```text
Decision
=
What should happen?

Capability
=
What narrow authority is available for follow-on execution?

Execution
=
The host actually performs the side effect.
```

These concepts may occur close together, but they should not be treated as identical.

For example:

```text
Decision = Allowed
```

does not necessarily mean:

```text
Any component may perform any related action indefinitely.
```

Likewise:

```text
Acknowledgment = Accepted
```

does not mean:

```text
The actor now has permanent permission.
```

An approval can justify issuing a narrow capability without becoming the capability itself.

## A Naive Authority Handoff

Consider:

```csharp
GovernanceDecision decision =
    policy.Evaluate(context);

if (decision.CanProceed)
{
    userSession.IsApprovedForAccountAdministration = true;
}
```

Later:

```csharp
if (userSession.IsApprovedForAccountAdministration)
{
    await accountService.DisableAsync(
        requestedAccountId,
        cancellationToken);
}
```

The original policy evaluated one operation.

The resulting state authorizes an entire category of operations.

Several bindings have disappeared:

- Original resource.
- Original operation.
- Policy version.
- Acknowledgment reference.
- Expiration.
- Intended execution host.
- Use count.

This is authority broadening.

## Represent Narrow Execution Authority

A framework-neutral capability can be modeled explicitly:

```csharp
public sealed record ExecutionCapability(
    string CapabilityId,
    string Issuer,
    string Audience,
    string SubjectId,
    string OperationName,
    string ResourceId,
    IReadOnlySet<string> Scopes,
    DateTimeOffset IssuedUtc,
    DateTimeOffset ExpiresUtc,
    string PolicyVersion,
    string? PolicyHash,
    string? AcknowledgmentId);
```

A capability might represent:

```text
CapabilityId:
cap-123

Issuer:
policy-engine

Audience:
account-admin-gateway

Subject:
admin-42

Operation:
account.disable

Resource:
user-123

Scope:
account.disable

Issued:
2026-08-12T19:00:00Z

Expires:
2026-08-12T19:05:00Z

PolicyVersion:
3.2

AcknowledgmentId:
ack-77
```

This is much narrower than:

```text
Administrator may manage accounts.
```

## Scope the Capability Along Multiple Dimensions

A capability can be narrow in several ways.

### Operation Scope

Bind authority to the action:

```text
account.disable
```

not:

```text
account.*
```

unless the broader scope is truly required.

### Resource Scope

Bind authority to:

```text
user-123
```

rather than:

```text
all users
```

### Subject Scope

Bind authority to the actor or subject for whom it was issued:

```text
admin-42
```

### Audience Scope

Bind authority to the intended execution boundary:

```text
account-admin-gateway
```

A grant intended for one gateway should not automatically be valid at another.

### Time Scope

Use a short validity window:

```text
IssuedUtc
ExpiresUtc
```

Optionally include:

```text
NotBeforeUtc
```

when execution should not begin immediately.

### Policy Scope

Bind the capability to:

```text
PolicyVersion
PolicyHash
```

when the execution should remain tied to the decision context that produced it.

### Acknowledgment Scope

If acknowledgment was required, bind the capability to:

```text
AcknowledgmentId
```

or the underlying handshake/challenge.

This prevents unrelated prior acknowledgment from satisfying a later execution request.

## Least Authority

The capability should carry only the authority required for the approved operation.

A useful design question is:

> **What is the smallest authority that allows this exact operation to succeed?**

Prefer:

```text
Scope:
account.disable

Resource:
user-123

Audience:
account-admin-gateway
```

over:

```text
Scope:
account.admin
```

and strongly prefer either over:

```text
Scope:
*
```

The narrower the capability, the smaller the damage if it is:

- Leaked.
- Replayed.
- Misrouted.
- Used by the wrong host.
- Used after the surrounding context changes.

## Capability Is Not Authentication

A capability does not automatically answer:

> Who is this caller?

That remains an authentication concern.

Likewise, a capability should not be assumed to replace the host's authorization model.

A production execution boundary may require all of these:

```text
Authenticated actor
   +
Host authorization
   +
Current resource authorization
   +
Validated capability
   +
Execution-specific validation
```

The capability represents one bounded piece of authority.

It is not a universal security credential.

## Issue the Capability After the Decision

A simple educational factory can make the transition explicit:

```csharp
public sealed class ExecutionCapabilityFactory
{
    public ExecutionCapability Create(
        DisableAccountPolicyContext context,
        GovernanceDecision decision,
        DateTimeOffset nowUtc,
        string? acknowledgmentId = null)
    {
        if (!decision.CanProceed)
        {
            throw new InvalidOperationException(
                "A blocked decision cannot produce an execution capability.");
        }

        return new ExecutionCapability(
            CapabilityId:
                Guid.NewGuid().ToString("N"),
            Issuer:
                "policy-engine",
            Audience:
                "account-admin-gateway",
            SubjectId:
                context.Actor.ActorId,
            OperationName:
                "account.disable",
            ResourceId:
                context.Account.AccountId,
            Scopes:
                new HashSet<string>(
                    ["account.disable"],
                    StringComparer.Ordinal),
            IssuedUtc:
                nowUtc,
            ExpiresUtc:
                nowUtc.AddMinutes(5),
            PolicyVersion:
                context.PolicyVersion,
            PolicyHash:
                null,
            AcknowledgmentId:
                acknowledgmentId);
    }
}
```

The important guard is:

```csharp
if (!decision.CanProceed)
{
    throw ...
}
```

A denied, deferred, acknowledgment-required, or escalation-required decision should not silently mint execution authority.

## Validate at the Execution Boundary

Issuing a capability is only half of the pattern.

The host must validate it **where the side effect will occur**.

A minimal validator:

```csharp
public sealed record CapabilityValidationRequest(
    string Audience,
    string SubjectId,
    string OperationName,
    string ResourceId,
    string RequiredScope,
    DateTimeOffset NowUtc,
    string PolicyVersion,
    string? AcknowledgmentId);

public sealed class ExecutionCapabilityValidator
{
    public bool IsValid(
        ExecutionCapability capability,
        CapabilityValidationRequest request)
    {
        if (!string.Equals(
                capability.Audience,
                request.Audience,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(
                capability.SubjectId,
                request.SubjectId,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(
                capability.OperationName,
                request.OperationName,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(
                capability.ResourceId,
                request.ResourceId,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!capability.Scopes.Contains(
                request.RequiredScope))
        {
            return false;
        }

        if (!string.Equals(
                capability.PolicyVersion,
                request.PolicyVersion,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (request.NowUtc < capability.IssuedUtc ||
            request.NowUtc >= capability.ExpiresUtc)
        {
            return false;
        }

        if (!string.Equals(
                capability.AcknowledgmentId,
                request.AcknowledgmentId,
                StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}
```

The important architecture is:

```text
Capability received
   ↓
Execution context constructed
   ↓
Capability validated
   ↓
Only then may side effect occur
```

## Host-Owned Execution

The executor remains separate:

```csharp
public interface IDisableAccountExecutor
{
    Task ExecuteAsync(
        string accountId,
        CancellationToken cancellationToken);
}
```

The host controls the transition:

```csharp
public sealed class DisableAccountGateway(
    ExecutionCapabilityValidator validator,
    IDisableAccountExecutor executor)
{
    public async Task<bool> ExecuteAsync(
        ExecutionCapability capability,
        CapabilityValidationRequest request,
        CancellationToken cancellationToken)
    {
        if (!validator.IsValid(
                capability,
                request))
        {
            return false;
        }

        await executor.ExecuteAsync(
            request.ResourceId,
            cancellationToken);

        return true;
    }
}
```

The capability does not call the executor.

The policy does not call the executor.

The acknowledgment object does not call the executor.

The **host** owns the execution boundary.

## Why Validation Must Happen Near Execution

Avoid validating a capability far upstream and then trusting a boolean:

```csharp
bool valid =
    validator.IsValid(
        capability,
        request);

// many calls later...

await executor.ExecuteAsync(...);
```

Between validation and execution:

- The capability may expire.
- Resource state may change.
- Revocation state may change.
- Another process may consume a single-use grant.
- The operation target may be substituted.
- The execution path may change.

For consequential operations, capability validation should occur as close as practical to the real side effect.

The principle is:

> **Validate where authority becomes action.**

## Metadata Validation Is Not Execution Validation

A system may inspect a capability earlier in the flow to answer questions such as:

- Is the structure well formed?
- Is the expected scope present?
- Is the audience recognizable?
- Is the grant already expired?

That can be useful.

But a successful metadata check should not be treated as permission to execute.

A production execution boundary may additionally require:

- Proof or signature verification.
- Issuer trust validation.
- Audience validation.
- Subject binding.
- Resource binding.
- Policy binding.
- Acknowledgment binding.
- Not-before and expiration checks.
- Replay or use-count enforcement.
- Revocation or cancellation state.

Use names that make the difference visible:

```text
ValidateMetadata(...)
```

versus:

```text
ValidateForExecution(...)
```

The reduced path should not look stronger than it is.

## Short-Lived Authority

Capabilities should generally expire.

Why?

Because a decision reflects a context at a point in time.

Long-lived capability:

```text
Decision on Monday
   ↓
Capability still valid Friday
```

may survive:

- Policy changes.
- Role changes.
- Resource changes.
- Risk changes.
- Revocation.
- Organizational changes.

Short validity narrows this gap.

It does not eliminate the need for other checks.

A five-minute capability is not automatically safe merely because it expires quickly.

## Not-Before and Clock Skew

Distributed systems may disagree slightly about time.

A capability model may therefore contain:

```text
IssuedUtc
NotBeforeUtc
ExpiresUtc
```

A production validator may also permit a small explicit clock-skew tolerance.

That tolerance should remain bounded.

Avoid:

```text
Allow 15 minutes of clock skew
```

simply because clock synchronization is unreliable.

A broad tolerance enlarges the effective authority window.

Fix time synchronization rather than using tolerance as a substitute for reliable clocks.

## Replay and Bounded Use

This section introduces the capability-specific replay boundary. For the deeper treatment of durable use state, atomic consumption, multi-instance and restart behavior, request idempotency, and failure windows, continue with [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md).

A capability may be correctly scoped and still be dangerous if it can be replayed indefinitely.

Suppose:

```text
Capability:
disable user-123

Use 1:
success

Use 2:
success

Use 3:
success
```

Was that intended?

For many consequential operations, the answer is no.

A bounded-use model might allow:

```text
Maximum uses: 1
```

Conceptually:

```text
Validate capability
   ↓
Check use state
   ↓
Atomically consume use
   ↓
Execute
```

The word **atomically** matters.

This is unsafe:

```text
Check count = 0
   ↓
two hosts both see 0
   ↓
both execute
   ↓
both record use
```

A production replay/use store must match the host's concurrency and deployment model.

## In-Memory Replay Checks Have Limits

An in-memory use store can be excellent for:

- Tests.
- Samples.
- Local development.
- Single-process demonstrations.

It does not automatically provide:

- Durability.
- Cross-node coordination.
- Distributed locking.
- Restart survival.
- Multi-region consistency.

Do not describe an in-memory single-use check as production replay protection across a distributed system.

The host owns that guarantee.

## Revocation and Cancellation

Expiration answers:

> When does authority naturally end?

Revocation or cancellation answers:

> Should this authority end before its scheduled expiration?

A production host may need to track:

```text
Active
Consumed
Revoked
Cancelled
Expired
```

A grant that is cryptographically valid and temporally valid may still be unacceptable because the host has revoked it.

This is another reason execution-boundary validation must include current host state when required.

## Bind to the Resource

Resource binding protects against target substitution.

Suppose a capability was issued for:

```text
user-123
```

but the later request asks for:

```text
user-999
```

If the host validates only:

```text
scope = account.disable
```

the original approval may be redirected to a different target.

Validate both:

```text
Operation
+
Resource
```

when the decision was resource-specific.

## Bind to the Audience or Gateway

A capability may be intended for:

```text
account-admin-gateway
```

Do not assume it should also work at:

```text
billing-gateway
robotics-gateway
deployment-gateway
```

Audience and gateway binding narrow where the authority can become action.

This is particularly important when several services understand similar capability formats.

## Bind to Policy Identity

Suppose a capability was created under:

```text
PolicyVersion = 3.2
```

but the execution gateway now requires:

```text
PolicyVersion = 3.3
```

There are several possible designs:

- Reject and require a new decision.
- Re-evaluate policy.
- Accept older policy within an explicit compatibility window.
- Validate a policy hash rather than only a version.

There is no universal answer.

The important point is that the choice should be explicit.

Do not accidentally allow stale grants because policy identity was discarded.

## Bind to Acknowledgment When Required

From the previous tutorial:

```text
Decision = AcknowledgmentRequired
   ↓
Actor accepts challenge
```

A capability issued afterward may carry:

```text
AcknowledgmentId = ack-77
```

The execution boundary can then require:

```text
ExpectedAcknowledgmentId = ack-77
```

This helps preserve lineage:

```text
Decision
   ↓
Acknowledgment
   ↓
Capability
   ↓
Execution
```

Without this binding, an unrelated acknowledgment may be reused as justification for another operation.

## Signing and Proof

A plain capability object is data.

If an untrusted party can modify:

```text
ResourceId
Scopes
ExpiresUtc
Audience
```

then validation of those fields is meaningless unless the host also trusts their integrity.

Production systems may therefore:

- Store capabilities server-side and issue opaque references.
- Protect them with authenticated encryption.
- Sign them.
- Wrap them in another trusted provider format.
- Bind them to a session or secure channel.

The exact mechanism is host-specific.

Do not confuse:

```text
Capability metadata
```

with:

```text
Cryptographically protected capability artifact
```

Those are related but distinct layers.

## Capability Is Not a Bearer Token Requirement

The word "token" can imply:

```text
String passed in Authorization header
```

but the capability pattern does not require that representation.

A host may keep the capability:

- Entirely server-side.
- In a database.
- In a workflow store.
- Inside a signed envelope.
- Behind an opaque identifier.
- In another provider's authorization artifact.

What matters is the narrow authority model and execution-boundary validation.

The transport format is a separate decision.

## Failure Should Not Expand Authority

A dangerous fallback is:

```csharp
if (!capabilityValidator.IsAvailable)
{
    // Keep service running.
    await executor.ExecuteAsync(...);
}
```

For high-consequence operations, validation failure should not silently become permission.

Depending on the failure, the host may:

```text
Deny
Defer
Require acknowledgment
Escalate
```

For example:

```text
Invalid proof
   ↓
Deny

Expired grant
   ↓
Deny

Not yet valid
   ↓
Defer

Replay store unavailable
   ↓
Defer
```

The exact failure policy belongs to the host.

The important rule is:

> **Failure should not broaden authority.**

## Decision, Capability, and Execution Evidence

The audit timeline can now distinguish:

```text
T1 Decision = Allowed
T2 Capability issued
T3 Capability validated
T4 Capability use consumed
T5 Execution started
T6 Execution completed
```

These are not the same event.

A capability may be issued but never used.

A capability may fail validation.

Execution may fail after successful capability validation.

Keeping these states separate improves investigation and system reasoning.

## Test the Capability Boundary

A useful test should prove that the executor is unreachable when a binding fails.

Example:

```csharp
[Fact]
public async Task WrongResource_DoesNotReachExecutor()
{
    var capability =
        CreateCapability(
            resourceId: "user-123");

    var request =
        CreateValidationRequest(
            resourceId: "user-999");

    var executor =
        new RecordingDisableAccountExecutor();

    var gateway =
        new DisableAccountGateway(
            new ExecutionCapabilityValidator(),
            executor);

    bool executed =
        await gateway.ExecuteAsync(
            capability,
            request,
            CancellationToken.None);

    Assert.False(executed);
    Assert.Equal(
        0,
        executor.InvocationCount);
}
```

Also test:

- Wrong audience.
- Wrong subject.
- Wrong operation.
- Missing scope.
- Wrong resource.
- Expired capability.
- Not-yet-valid capability.
- Wrong policy version.
- Wrong acknowledgment reference.
- Replay/use limit exceeded.
- Revoked capability.
- Valid capability reaches the executor exactly once.

## Test Scope Narrowing

Suppose a capability contains:

```text
account.disable
```

Verify it cannot perform:

```text
account.delete
account.reset-password
account.change-owner
```

This tests the architectural promise:

> The capability grants only what it says.

## Test Time Boundaries

Time bugs often appear at exact boundaries.

Test:

```text
Now < NotBefore
Now = NotBefore
Now just before Expires
Now = Expires
Now > Expires
```

Define the semantics clearly.

For example:

```text
NotBefore is inclusive.
Expiration is exclusive.
```

The exact policy can differ, but ambiguity should not.

## Test Replay Behavior

If the capability is single-use:

```text
First valid use
   ↓
Allowed

Second use
   ↓
Denied
```

Then test concurrent attempts as well.

A single-threaded unit test does not prove distributed single-use behavior.

That guarantee depends on the production use-store architecture.

## Common Failure Modes

### 1. Decision Becomes a Session-Wide Permission

A narrow decision creates a broad flag such as:

```text
ApprovedForAdminActions = true
```

The original operation boundaries disappear.

### 2. Capability Contains Wildcard Scope

```text
scope = *
```

The capability exists, but least authority has been defeated.

### 3. Resource Is Not Bound

Approval for `user-123` is reused for `user-999`.

### 4. Audience Is Not Checked

A capability issued for one gateway is accepted by another.

### 5. Expiration Is Long or Missing

Authority outlives the context that justified it.

### 6. Metadata Validation Is Treated as Execution Permission

Structural checks pass, but proof, replay, binding, or authorization checks never occur.

### 7. Capability Validation Happens Too Early

The host validates upstream and assumes authority is still valid much later at the side effect.

### 8. Replay Store Is Best-Effort

Concurrent hosts can execute the same supposedly single-use capability.

### 9. Acknowledgment Binding Is Lost

A capability cannot prove which acknowledgment satisfied the original requirement.

### 10. Capability Object Executes Itself

```csharp
await capability.ExecuteAsync();
```

Authority description and execution become coupled.

### 11. Validation Failure Falls Back to Execution

An unavailable validator becomes an accidental allow path.

### 12. Capability Replaces Host Security

The system stops performing authentication, authorization, or resource checks because "the capability already allowed it."

That is a category error.

## Tradeoffs

### Benefits

- Approval remains narrowly scoped.
- Execution authority can be time-limited.
- Resource substitution becomes detectable.
- Audience and gateway boundaries become explicit.
- Policy and acknowledgment lineage can be retained.
- Replay/use behavior can be modeled.
- Host-owned execution remains visible.
- AI systems can receive bounded follow-on authority without receiving broad platform permissions.

### Costs

- Capability issuance and validation add state and code.
- Proof/signing introduces key-management concerns.
- Single-use enforcement requires reliable storage.
- Distributed replay prevention can be operationally difficult.
- Revocation may require centralized or shared state.
- Time-based validation depends on reliable clocks.
- Overly complex scope models can become difficult to administer.
- Poorly designed capabilities can create the appearance of least privilege without actually reducing authority.

The pattern is valuable when the narrowed authority is meaningful.

It should not become ceremony around trivial operations.

## Working Implementation References

This tutorial is deliberately framework-neutral. The working `AsiBackbone` repository implements fuller versions of the same capability and execution-boundary concepts with signing, explicit validation profiles, bounded-use state, richer failure outcomes, and host integration seams.

Use these references as an implementation map rather than as required dependencies for understanding the pattern.

### Concept-to-Implementation Map

| Tutorial concept | Working reference | What to inspect |
| --- | --- | --- |
| Narrow, short-lived execution authority | [`CapabilityTokenGrant`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityTokenGrant.cs) | Compare the tutorial's compact `ExecutionCapability` with the provider-neutral grant metadata for issuer, audience, scopes, time bounds, subject, operation, policy identity, acknowledgment/handshake references, gateway binding, and resource binding. |
| Execution-boundary versus metadata-only validation | [`CapabilityGrantValidationOptions`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityGrantValidationOptions.cs) | Compare `CreateExecutionBoundary(...)`, which requires proof and bounded-use validation by default, with the deliberately weaker `CreateMetadataValidation(...)` profile. |
| Capability validation pipeline | [`CapabilityGrantValidator`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityGrantValidator.cs) | Follow proof verification, issuer/audience checks, time bounds, scopes, policy identity, acknowledgment/handshake references, gateway/resource bindings, and optional bounded-use state before a result can allow continuation. |
| Execution-profile behavior under tests | [`CapabilityGrantValidationProfileTests`](https://github.com/AsiBackbone/AsiBackbone/blob/main/tests/AsiBackbone.Core.Tests/CapabilityTokens/CapabilityGrantValidationProfileTests.cs) and [`CapabilityGrantValidatorTests`](https://github.com/AsiBackbone/AsiBackbone/blob/main/tests/AsiBackbone.Core.Tests/CapabilityTokens/CapabilityGrantValidatorTests.cs) | Inspect executable cases for strict execution-boundary defaults, metadata-only behavior, proof failure, unavailable use-state, binding mismatches, expiration, policy evidence, and validation outcomes. |
| Bounded-use and replay-state seam | [`ICapabilityGrantUseStore`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/ICapabilityGrantUseStore.cs) and [`InMemoryCapabilityGrantUseStore`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Storage.InMemory/CapabilityTokens/InMemoryCapabilityGrantUseStore.cs) | Compare the provider-neutral host contract with the explicitly local in-memory reference implementation. Durable, distributed, atomic replay guarantees remain host-owned. |
| Bounded-use behavior under tests | [`InMemoryCapabilityGrantUseStoreTests`](https://github.com/AsiBackbone/AsiBackbone/blob/main/tests/AsiBackbone.Core.Tests/CapabilityTokens/InMemoryCapabilityGrantUseStoreTests.cs) | Follow first-use, reuse-limit, stopped/cancelled, and local-state behavior without mistaking the in-memory store for distributed replay protection. |
| Production-oriented capability hardening | [Capability Grant Hardening](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/capability-grant-hardening.md) | Review execution-boundary profiles, proof handling, binding checks, clock skew, failure behavior, bounded use, and the explicit boundary between capability validation and host authorization/execution. |
| Proof trust narrowing | [Capability Proof Trust Pinning](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/capability-proof-trust-pinning.md) | See how a host can narrow which otherwise valid signing authority is acceptable for a particular capability-validation context. |
| Host-owned execution lifecycle | [Intent to Execution Pattern](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/intent-to-execution-pattern.md) | Follow the broader governed lifecycle and observe that execution remains deliberately outside the governance spine and under host control. |

### Follow the Capability Path

For a code-first inspection, follow these references in order:

1. [`CapabilityTokenGrant`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityTokenGrant.cs) — begin with the provider-neutral description of narrow follow-on authority.
2. [`CapabilityGrantValidationOptions`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityGrantValidationOptions.cs) — inspect how the host declares whether it is performing strict execution-boundary validation or intentionally weaker metadata validation.
3. [`CapabilityGrantValidator`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/CapabilityGrantValidator.cs) — follow the configured proof, time, scope, policy, acknowledgment, gateway, resource, and use-state checks.
4. [`CapabilityGrantValidationProfileTests`](https://github.com/AsiBackbone/AsiBackbone/blob/main/tests/AsiBackbone.Core.Tests/CapabilityTokens/CapabilityGrantValidationProfileTests.cs) and [`CapabilityGrantValidatorTests`](https://github.com/AsiBackbone/AsiBackbone/blob/main/tests/AsiBackbone.Core.Tests/CapabilityTokens/CapabilityGrantValidatorTests.cs) — compare the API surface with executable allow, deny, and defer behavior.
5. [`ICapabilityGrantUseStore`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/CapabilityTokens/ICapabilityGrantUseStore.cs) and [`InMemoryCapabilityGrantUseStore`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Storage.InMemory/CapabilityTokens/InMemoryCapabilityGrantUseStore.cs) — continue into bounded-use state while keeping production persistence and concurrency guarantees host-owned.
6. [Capability Grant Hardening](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/capability-grant-hardening.md) — put those source types back into their production-oriented security and failure-handling context.
7. [Intent to Execution Pattern](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/intent-to-execution-pattern.md) — finish at the broader lifecycle and the boundary where the host performs the real side effect.

### Teaching Model Versus Working Framework

The Learning sample is intentionally smaller and more explicit than the working framework.

For example:

- The teaching sample uses an in-process capability object so actor, operation, resource, audience, policy, acknowledgment, time, and intended-use bindings remain easy to see.
- The sample's integer `ResourceVersion` is a teaching-specific freshness mechanism. The working framework exposes a host-defined `ResourceBinding`; it does not prescribe a row version, ETag, revision number, or database concurrency model.
- The sample directly validates deterministic current-context facts immediately before its simulated executor. The framework separates grant metadata, validation policy, proof verification, bounded-use state, and host integration so applications can add their own authentication, authorization, resource-freshness, and execution checks.
- The framework's execution-boundary profile can require signed-proof verification and bounded-use enforcement. The teaching sample intentionally omits cryptographic proof and durable replay infrastructure.
- The in-memory framework use store is a reference implementation for tests and local validation, not a claim of durable or distributed single-use enforcement.

The working framework explicitly describes `CapabilityTokenGrant` as a **metadata model, not a bearer-token format**.

That distinction preserves the same architectural boundary taught here:

```text
Governance decision
   ↓
Narrow follow-on authority
   ↓
Current host context
   ↓
Execution-boundary validation
   ↓
Host decides whether to execute
   ↓
Host-owned side effect
```

The host still decides how the grant is serialized, transported, protected, bound to authentication and authorization, checked against current resource state, protected against replay, and translated into a real external action.

The implementation links are therefore examples of how the pattern becomes richer in working software, not instructions to copy the teaching classes one-for-one.

## Apply the Pattern to AI

This pattern becomes especially useful for AI tool execution.

Avoid:

```text
Model decides tool is appropriate
   ↓
Model receives permanent tool credential
   ↓
Model can invoke tool repeatedly
```

Prefer:

```text
User request
   ↓
Model proposes tool action
   ↓
Host builds authoritative context
   ↓
Governance decision
   ↓
Acknowledgment when required
   ↓
Host issues narrow capability
   |
   +-- tool scope
   +-- resource binding
   +-- subject binding
   +-- gateway binding
   +-- short expiration
   +-- acknowledgment reference
   +-- bounded use
   ↓
Execution gateway validates capability
   ↓
Host invokes tool
   ↓
Audit residue
```

Example:

```text
Scope:
customer-record.delete

Resource:
customer-981

Audience:
customer-data-gateway

Expires:
+2 minutes

Max uses:
1
```

The model may carry or reference the proposed action.

It does not receive broad database credentials.

The execution gateway still validates the host-defined capability before performing the action.

> **The model may propose. The host retains execution authority.**

## Exercise

Extend the workflow from the first three tutorials.

After an allowed decision or successfully satisfied acknowledgment, create an `ExecutionCapability` containing:

```text
CapabilityId
Issuer
Audience
SubjectId
OperationName
ResourceId
Scopes
IssuedUtc
ExpiresUtc
PolicyVersion
AcknowledgmentId
```

Then create an execution gateway that validates the capability immediately before calling the executor.

Write tests proving:

1. A valid capability reaches the executor.
2. The wrong subject does not.
3. The wrong operation does not.
4. The wrong resource does not.
5. The wrong audience does not.
6. A missing scope does not.
7. An expired capability does not.
8. The wrong acknowledgment reference does not.
9. A single-use capability cannot be replayed.
10. Validation failure never falls back to execution.

For additional practice:

- Add a `NotBeforeUtc` field.
- Add a revocation store.
- Add a bounded clock-skew policy.
- Record capability issuance and validation as separate audit stages.

Then ask:

> If this capability leaked, what is the maximum authority it would expose?

If the answer is much broader than the original approved operation, narrow the design.

## Review Questions

Before moving on, you should be able to answer:

1. Why is an allowed decision different from execution authority?
2. What makes a capability "scoped"?
3. Why should operation and resource both be bound when the decision was resource-specific?
4. Why does audience binding matter?
5. Why should capabilities usually be short-lived?
6. Why is capability metadata not automatically a secure bearer token?
7. Why should validation happen near the real side effect?
8. Why is metadata-only validation weaker than execution-boundary validation?
9. What problem does bounded-use or replay enforcement solve?
10. Why can an in-memory use store not prove distributed single-use behavior?
11. Why should validation failure not broaden authority?
12. Why does the host still need authentication and authorization?
13. How can acknowledgment identity be carried forward into capability validation?
14. How does the capability pattern reduce risk for AI-proposed tool execution?

## Next

The next foundational topic is **Governed AI Tool Gateway**.

That tutorial will compose the first four lessons into one end-to-end workflow:

```text
User request
   ↓
AI proposes tool action
   ↓
Intent
   ↓
Host policy context
   ↓
Governance decision
   ↓
Acknowledgment when required
   ↓
Scoped capability
   ↓
Execution-boundary validation
   ↓
Host invokes tool
   ↓
Audit residue
```

The fifth tutorial is therefore not a new architectural primitive.

It is the first full composition of the primitives established so far.

## Related Content

- [Foundational Tutorial Index](index.md) — view the complete five-tutorial governed-execution learning path.
- [Decision Before Execution](decision-before-execution.md) — revisit the foundational boundary between a proposed operation, a governance decision, and the host-owned side effect.
- [Acknowledgment and Audit Residue](acknowledgment-and-audit-residue.md) — review the responsibility and evidence boundaries that may precede issuance of execution authority.
- [Policy Context and Explicit Decision Outcomes](policy-context-and-explicit-decision-outcomes.md) — revisit the policy facts, outcome semantics, and policy identity that justify a scoped capability.
- [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) — go deeper on durable replay state, atomic consumption, distributed races, idempotency, and execution failure windows.
- [Governed AI Tool Gateway](governed-ai-tool-gateway.md) — see scoped capabilities used as part of a complete AI-proposed, host-governed execution path.
- [Scoped Capability and Host-Owned Execution sample](https://github.com/AsiBackbone/Learning/blob/main/samples/scoped-capability-and-host-owned-execution/README.md) — run the capability issuance and execution-boundary validation pattern.
- [Scoped Capability and Host-Owned Execution lab](../labs/scoped-capability-and-host-owned-execution.md) — break expiration, resource-freshness, and scope boundaries, then repair them.

---

> **Read it. Run it. Question it. Improve it.**
