# Practical Policy Testing and Decision-Table Strategies

**Learning objective:** Learn how to translate policy requirements into explicit decision tables, equivalence classes, boundary cases, layered automated tests, and execution-boundary invariants so governance behavior remains observable and regression-testable as policy evolves.

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md), [Constraint Composition and Policy Precedence](constraint-composition-and-policy-precedence.md), and [Policy Versioning and Decision Provenance](policy-versioning-and-decision-provenance.md). Familiarity with [Decision Before Execution](../tutorials/decision-before-execution.md) is helpful for the execution-boundary examples.

## At a Glance

> **Problem:** Policy tests can cover implementation branches while still leaving important combinations of context, outcomes, precedence, continuation state, and execution authority under-specified.
>
> **Core idea:** Translate policy requirements into explicit decision tables, equivalence classes, boundary cases, structured outcome assertions, and execution-boundary invariants.
>
> **Why it matters:** High code coverage does not prove that meaningful policy behavior is complete, deterministic, or that non-executable decisions are prevented from reaching protected execution.
>
> **Read this if:** Your system has multiple governance outcomes, composed constraints, policy versions, acknowledgment, capability issuance, delayed continuation, or a protected execution boundary that must remain regression-testable.

The central lesson is:

> **Policy behavior should be observable and regression-testable at the decision boundary.**

This is not the same as saying every possible system state must be enumerated.

The goal is to make the **meaningful policy space** visible enough that important rules, boundaries, precedence decisions, failure states, and execution invariants cannot disappear inside implementation details.

---

## Start from the Policy Statement, Not the Class Under Test

Consider this requirement:

```text
Administrators may disable an active account
unless the account is protected,
and protected accounts require escalation.
```

A weak testing approach may begin by opening the implementation and writing one test for each `if` statement.

That can produce useful branch tests, but it starts from the code rather than the policy.

A stronger first step is to ask:

```text
Which facts matter?
Which combinations are meaningfully different?
What explicit outcome should each important combination produce?
What must never happen after a non-executable outcome?
```

The policy statement can then become a small decision table.

For example:

| Actor role | Account state | Protected | Expected outcome |
| --- | --- | --- | --- |
| Administrator | Active | No | `Allowed` |
| Administrator | Active | Yes | `EscalationRecommended` |
| User | Active | No | `Denied` |

A different application could choose a different result for an already-disabled account:

```text
Already disabled
    ↓
Denied
```

or:

```text
Already disabled
    ↓
Warning
```

The current Learning sample intentionally uses `Warning` for that case.

The important testing principle is not which outcome is universally correct.

It is that the chosen behavior appears **explicitly in the policy table and in the automated tests**.

---

## Use the Existing Disable-Account Sample as a Concrete Model

The existing [Policy Context and Explicit Decision Outcomes sample](https://github.com/AsiBackbone/Learning/tree/main/samples/policy-context-and-explicit-decision-outcomes) already exposes a useful policy vocabulary:

```text
Allowed
Warning
Denied
Deferred
AcknowledgmentRequired
EscalationRecommended
```

Its decision context includes facts such as:

```text
Actor administrator status
Actor tenant
Account tenant
Account protected status
Account already-disabled status
Maintenance hold
Reason text
Policy version
```

The sample policy currently evaluates them with behavior equivalent to:

| Scenario | Expected outcome | Expected reason |
| --- | --- | --- |
| Administrator, same tenant, active, unprotected, no hold, reason present | `Allowed` | none |
| Non-administrator | `Denied` | `account.disable.not-administrator` |
| Cross-tenant account | `Denied` | `account.disable.cross-tenant` |
| Already-disabled account | `Warning` | `account.disable.already-disabled` |
| Protected account | `EscalationRecommended` | `account.disable.protected-account` |
| Maintenance hold | `Deferred` | `account.disable.maintenance-hold` |
| Missing reason | `AcknowledgmentRequired` | `account.disable.reason-required` |

That table is already more informative than a statement such as:

```text
DisableAccountPolicy has seven tests.
```

The table tells a reviewer **which policy distinctions those tests protect**.

---

## A Decision Table Is a Policy Artifact

A decision table is not merely a compact way to write tests.

It is a reviewable policy artifact.

A useful table answers:

- What facts influence the decision?
- Which fact combinations are intentionally equivalent?
- Which combinations produce different governance outcomes?
- Which reason codes are expected?
- Which outcomes may proceed?
- Which policy version defines the expectation?
- Which downstream actions are forbidden?

A more complete table can therefore include more than the final outcome:

| Case | Policy version | Expected outcome | Expected reason | Can proceed | Executor calls |
| --- | --- | --- | --- | --- | --- |
| normal-admin | `2.0` | `Allowed` | none | Yes | 1 after host continuation |
| non-admin | `2.0` | `Denied` | `account.disable.not-administrator` | No | 0 |
| protected | `2.0` | `EscalationRecommended` | `account.disable.protected-account` | No | 0 |
| maintenance-hold | `2.0` | `Deferred` | `account.disable.maintenance-hold` | No | 0 |
| missing-reason | `2.0` | `AcknowledgmentRequired` | `account.disable.reason-required` | No | 0 until valid continuation |

The final column protects an architectural boundary that a policy-only unit test cannot prove.

---

## The Worked Failure: An Incomplete Suite Misses the Protected-Account Boundary

Suppose the first test suite contains only:

```text
Admin + Active account
    ↓
Allowed

User + Active account
    ↓
Denied
```

Those two tests may look reasonable.

They prove a positive case and a negative case.

But the policy requirement later becomes explicit:

```text
Protected accounts require escalation.
```

The missing case is now visible:

```text
Admin + Active + Protected
    ↓
EscalationRecommended
```

Without a decision table, the omission can remain hidden because the developer may think in terms of:

```text
administrator branch covered
non-administrator branch covered
```

rather than:

```text
policy state-space reviewed
```

A decision table makes the gap visible before production behavior depends on it.

### Incomplete Table

| Actor role | Active | Protected | Expected |
| --- | --- | --- | --- |
| Administrator | Yes | No | `Allowed` |
| User | Yes | No | `Denied` |

### Requirement Review

```text
Protected accounts require escalation
```

### Missing Row

| Actor role | Active | Protected | Expected |
| --- | --- | --- | --- |
| Administrator | Yes | Yes | `EscalationRecommended` |

The important change is not merely one more test.

The important change is that the policy requirement now has a visible row that can be reviewed, automated, and preserved through future refactoring.

---

## Turn Table Rows into Named Automated Cases

A table-driven test should keep the expected outcomes explicit rather than recomputing them with the same branching logic as the production policy.

A small xUnit teaching model could look like this:

```csharp
public sealed record DisableAccountCase(
    string Name,
    bool IsAdministrator,
    bool IsProtected,
    bool IsAlreadyDisabled,
    bool MaintenanceHoldActive,
    string Reason,
    GovernanceDecisionOutcome ExpectedOutcome,
    string? ExpectedReasonCode);

public static TheoryData<DisableAccountCase> Cases =>
    new()
    {
        new(
            "normal administrator",
            true,
            false,
            false,
            false,
            "Security investigation",
            GovernanceDecisionOutcome.Allowed,
            null),
        new(
            "non administrator",
            false,
            false,
            false,
            false,
            "Security investigation",
            GovernanceDecisionOutcome.Denied,
            "account.disable.not-administrator"),
        new(
            "protected account",
            true,
            true,
            false,
            false,
            "Rotation request",
            GovernanceDecisionOutcome.EscalationRecommended,
            "account.disable.protected-account"),
        new(
            "maintenance hold",
            true,
            false,
            false,
            true,
            "Administrative cleanup",
            GovernanceDecisionOutcome.Deferred,
            "account.disable.maintenance-hold"),
        new(
            "missing reason",
            true,
            false,
            false,
            false,
            string.Empty,
            GovernanceDecisionOutcome.AcknowledgmentRequired,
            "account.disable.reason-required")
    };
```

The corresponding test can stay small:

```csharp
[Theory]
[MemberData(nameof(Cases))]
public void DecisionTableRowsProduceExpectedOutcomes(
    DisableAccountCase testCase)
{
    DisableAccountPolicyContext context =
        CreateContext(testCase);

    GovernanceDecision decision =
        new DisableAccountPolicy().Evaluate(context);

    Assert.Equal(
        testCase.ExpectedOutcome,
        decision.Outcome);

    if (testCase.ExpectedReasonCode is null)
    {
        Assert.Empty(decision.Reasons);
    }
    else
    {
        Assert.Contains(
            decision.Reasons,
            reason => reason.Code == testCase.ExpectedReasonCode);
    }
}
```

This is a teaching sketch.

The useful property is that the **expected result is data**.

Avoid a test oracle like this:

```csharp
GovernanceDecisionOutcome expected =
    !context.Actor.IsAdministrator
        ? GovernanceDecisionOutcome.Denied
        : context.Account.IsProtected
            ? GovernanceDecisionOutcome.EscalationRecommended
            : GovernanceDecisionOutcome.Allowed;
```

That test duplicates the production decision logic.

A defect copied into both implementation and test can then pass perfectly.

---

## Policy Tests Should Assert Meaning, Not Object Construction

This test is weak:

```csharp
[Fact]
public void ConstraintReturnsDenied()
{
    ConstraintResult result =
        new ProtectedAccountConstraint().Evaluate(context);

    Assert.True(result.IsDenied);
}
```

It may be a valid **constraint-level test**, but it does not prove the full governance behavior.

The more important questions may be:

```text
Did the final composed decision remain non-executable?
Did the expected reason survive composition?
Did a decision policy change the outcome?
Was the correct policy version attached?
Did the host stop before the protected executor?
```

A stronger end-to-end invariant looks like:

```text
Protected account
        ↓
Policy outcome = EscalationRecommended
        ↓
CanProceed = false
        ↓
Protected executor invocation count = 0
```

This is why policy testing needs layers.

---

## Use a Layered Testing Model

A useful governance testing model is:

```text
Individual Constraint Tests
            ↓
Constraint Composition Tests
            ↓
Decision-Policy Tests
            ↓
Governance Pipeline Tests
            ↓
Execution-Boundary Invariant Tests
```

Each layer proves a different responsibility.

### 1. Individual Constraint Tests

Test the narrow semantics of one rule.

Examples:

```text
Same-tenant constraint
cross-tenant context
    ↓
Deny
reason = account.disable.cross-tenant
```

or:

```text
Contractor-hours constraint
employee context
    ↓
NotApplicable
```

These tests should not need to understand global precedence.

### 2. Constraint Composition Tests

Test how several local findings become one base decision.

Examples:

```text
Allow + Deny
    ↓
Denied
```

```text
Allow + Warning + NotApplicable
    ↓
Warning
```

```text
Deny(reason-a) + Deny(reason-b)
    ↓
Denied
blocking reasons contain reason-a and reason-b
```

These tests protect the composition policy rather than any one domain rule.

### 3. Decision-Policy Tests

Test post-composition host or domain rules.

Examples:

```text
Base decision = Warning
Regional rule = acknowledgment required
    ↓
Final decision = AcknowledgmentRequired
```

or:

```text
Base decision = Denied
Ordinary decision policy runs
    ↓
Final decision remains non-executable
```

If a specialized override authority can weaken a denial, test that path separately and make the authority explicit.

### 4. Governance Pipeline Tests

Test representative context snapshots through the real evaluation pipeline.

These tests answer:

```text
Given this authoritative context
and this active policy version
what final governance decision is produced?
```

This is where decision tables are especially useful.

### 5. Execution-Boundary Invariant Tests

Test the architectural boundary after the decision exists.

The central invariant is:

```text
Expected outcome is non-executable
        ↓
Protected executor invocation count = 0
```

This final layer catches defects that policy-only tests cannot.

A perfect `Denied` object is not useful if the host executes anyway.

---

## Explicitly Test Every Governance Outcome Your Policy Uses

A policy with six possible outcomes should not be tested as though it were Boolean.

The Learning vocabulary distinguishes:

```text
Allowed
Warning
Denied
Deferred
AcknowledgmentRequired
EscalationRecommended
```

Those outcomes have different operational meanings.

### Allowed

Test that the operation is proceedable **subject to later execution checks**.

```text
Outcome = Allowed
CanProceed = true
```

Do not treat `Allowed` as proof that every later authority, replay, freshness, or resource-state check can be skipped.

### Warning

If the host treats warnings as proceedable, test both facts:

```text
Outcome = Warning
CanProceed = true
Reason is preserved
```

A warning should not disappear merely because execution may continue.

### Denied

Test:

```text
Outcome = Denied
CanProceed = false
Expected blocking reason exists
Protected executor calls = 0
```

Expected policy denial should normally be represented as a governance result, not as an exception.

### Deferred

Test that temporary inability to continue remains distinguishable from explicit prohibition:

```text
Outcome = Deferred
CanProceed = false
Reason = account.disable.maintenance-hold
```

The caller may retry later, but the current decision is still non-executable.

### AcknowledgmentRequired

Test that the decision pauses the workflow:

```text
Outcome = AcknowledgmentRequired
CanProceed = false
Challenge may be issued
Executor calls = 0
```

Then test acknowledgment as a separate continuation event.

### EscalationRecommended

Test that escalation is not silently treated as approval:

```text
Outcome = EscalationRecommended
CanProceed = false
Escalation route may be created
Executor calls = 0
```

The exact downstream process is host-defined.

The non-execution boundary should remain explicit.

---

## Positive and Negative Cases Are Necessary but Not Sufficient

A common test plan contains:

```text
one happy path
one denied path
```

That is better than no tests.

But governance systems often need more structure because different non-happy paths mean different things.

For the disable-account policy:

```text
Non-administrator
    = Denied

Maintenance hold
    = Deferred

Missing reason
    = AcknowledgmentRequired

Protected account
    = EscalationRecommended
```

Treating all four as merely "negative tests" loses policy meaning.

A useful decision table preserves the distinction.

---

## Use Equivalence Classes to Avoid Exhaustive Enumeration

Decision tables do not require a row for every raw system state.

Instead, group states that should be equivalent under the policy.

For example, if administrator identity is the only role distinction used by a rule:

```text
Administrator
Non-administrator
```

may be useful equivalence classes even if the application has dozens of concrete job titles.

Likewise, if the policy cares only whether tenant IDs match:

```text
Same tenant
Different tenant
```

is usually more useful than enumerating every tenant pair.

A good equivalence class has this property:

> Any representative member should produce the same policy meaning under the rule being tested.

Examples:

| Raw input space | Useful policy equivalence classes |
| --- | --- |
| Many actor roles | administrator / non-administrator |
| Many tenants | same tenant / different tenant |
| Reason text | present / missing-or-whitespace |
| Account status | active / already disabled |
| Policy freshness | same version / explicitly compatible / stale / unknown |
| Capability use state | unused / already consumed / revoked |

Equivalence classes reduce the table while preserving meaningful distinctions.

---

## Boundary-Value Testing Matters When Policy Has Thresholds

Boolean policy facts do not need numeric boundary-value analysis.

Threshold policies do.

Suppose an export rule says:

```text
Exports up to 10,000 records are allowed.
Exports above 10,000 records require acknowledgment.
```

Do not test only:

```text
100 records
50,000 records
```

Test the boundary:

| Record count | Expected outcome |
| ---: | --- |
| 9,999 | `Allowed` |
| 10,000 | `Allowed` |
| 10,001 | `AcknowledgmentRequired` |

The most useful tests often sit immediately around the policy transition.

Other common boundaries include:

- Time windows.
- Risk-score thresholds.
- Retry counts.
- Monetary limits.
- Capability expiration.
- Maximum batch size.
- Geographic or tenant scope transitions.

Boundary tests protect the policy definition itself, not merely the arithmetic implementation.

---

## Test Conflicting Constraint Results Explicitly

Composition policy is easiest to understand through conflicting results.

If the documented precedence is:

```text
Any denial wins
```

then test combinations such as:

| Constraint A | Constraint B | Constraint C | Expected base decision |
| --- | --- | --- | --- |
| Allow | Allow | NotApplicable | Allowed |
| Allow | Warning | NotApplicable | Warning |
| Warning | Deny | Allow | Denied |
| Deny | Deny | Warning | Denied |

These tests protect the composition boundary against accidental changes in registration order or refactoring.

Do not infer correctness merely because each individual constraint has its own unit tests.

Two locally correct rules can still be composed incorrectly.

---

## Test NotApplicable Separately from Allow

`NotApplicable` and `Allow` are both non-blocking in many composition models, but they do not mean the same thing.

Test them separately when diagnostics or policy review depend on the distinction.

```text
Allow
    = this rule applied and passed

NotApplicable
    = this rule had nothing to decide
```

Useful cases include:

```text
Allow + NotApplicable
    ↓
Allowed
```

and:

```text
NotApplicable + NotApplicable
    ↓
documented all-not-applicable behavior
```

Do not assume the all-not-applicable case is identical to an empty policy.

The current deeper governance material intentionally distinguishes:

```text
Non-empty policy where every rule is NotApplicable
```

from:

```text
Zero active constraints
```

Those states deserve separate tests.

---

## Full Evaluation and Short-Circuiting Need Different Test Expectations

A full-evaluation policy and a first-denial short-circuit policy can produce the same final outcome while producing different evidence.

### Full Evaluation

```text
Constraint A = Warning
Constraint B = Deny(reason-b)
Constraint C = Deny(reason-c)
        ↓
All three run
Final = Denied
Blocking reasons may include reason-b and reason-c
```

A test should assert the complete evidence the composition contract promises.

### Short-Circuit

```text
Constraint A = Warning
Constraint B = Deny(reason-b)
Constraint C = never evaluated
        ↓
Final = Denied
Constraint C invocation count = 0
```

A short-circuit test should not expect evidence from a constraint that never ran.

This is an example of an architectural behavior that code coverage alone does not explain.

The same branch may be correct under one evaluation mode and incorrect under another.

---

## Test Reason Codes as Stable Decision Evidence

Reason codes are part of the observable contract when callers, logs, audit records, dashboards, or remediation workflows depend on them.

Prefer assertions such as:

```csharp
Assert.Equal(
    GovernanceDecisionOutcome.EscalationRecommended,
    decision.Outcome);

Assert.Equal(
    "account.disable.protected-account",
    Assert.Single(decision.Reasons).Code);
```

Avoid asserting only message text when the message is intended for humans and may legitimately change.

A stable reason code can support:

- Regression testing.
- Localization-independent logic.
- Audit correlation.
- Metrics.
- Remediation routing.
- Policy review.

Do not use a reason code as a substitute for policy identity or version.

Those are different facts.

---

## Assert Policy Identity and Version Where the Decision Outlives Evaluation

When a decision carries policy evidence, test that evidence as part of the decision contract.

For example:

```text
Context.PolicyVersion = account-disable/2.0
        ↓
Decision.PolicyVersion = account-disable/2.0
```

If a policy hash or fingerprint is part of the model:

```text
Context.PolicyHash = sha256:abc...
        ↓
Decision.PolicyHash = sha256:abc...
```

The test should protect historical attribution.

A later deployment must not make an old decision appear to have been produced by a new policy.

---

## Determinism Is an Architectural Invariant for Deterministic Inputs

For deterministic policy logic, a useful invariant is:

```text
Same authoritative context
+
Same active policy version
+
Same deterministic dependencies
        ↓
Same governance decision
```

A test can evaluate the same immutable context more than once and compare the policy-significant result:

```csharp
GovernanceDecision first =
    await evaluator.EvaluateAsync(context, cancellationToken);

GovernanceDecision second =
    await evaluator.EvaluateAsync(context, cancellationToken);

Assert.Equal(first.Outcome, second.Outcome);
Assert.Equal(first.ReasonCodes, second.ReasonCodes);
Assert.Equal(first.PolicyVersion, second.PolicyVersion);
```

Do not compare fields that are intentionally unique per evaluation, such as newly generated decision IDs or timestamps, unless the contract says they must match.

If a rule consults live dependencies, then those dependency results are part of the effective input.

The better question becomes:

```text
Same captured authoritative facts
        ↓
Same deterministic decision
```

---

## Historical Policy Versions Need Regression Cases of Their Own

Policy versioning changes the testing problem.

Suppose policy `2.0` says:

```text
Protected account
    ↓
EscalationRecommended
```

and policy `2.1` intentionally changes the rule to:

```text
Protected account
    ↓
Denied
```

A useful regression suite can preserve both expectations when historical interpretation matters:

| Policy version | Protected | Expected outcome |
| --- | --- | --- |
| `2.0` | Yes | `EscalationRecommended` |
| `2.1` | Yes | `Denied` |

That does not mean old executable code must remain forever.

It means the test model should make the semantic change explicit.

When a new version changes expected rows, reviewers can ask:

```text
Was this an intentional policy change?
Or did a refactor accidentally change behavior?
```

That is a much stronger review signal than a generic snapshot diff.

---

## Test Policy Drift Separately from Historical Provenance

A historical decision can remain correct evidence while being stale for execution.

Test the two questions separately.

### Historical Attribution

```text
Decision created under 4.2
Current policy becomes 4.3
        ↓
Stored decision still says 4.2
```

### Execution Freshness

```text
Decision policy = 4.2
Current policy = 4.3
        ↓
Freshness policy returns Reevaluate
or Defer
or another documented non-executable result
```

Do not test drift by rewriting the historical decision to `4.3`.

That would destroy the evidence the test is supposed to protect.

---

## Test Stale Decision Evidence at the Continuation Boundary

A delayed workflow can create this timeline:

```text
Policy 4.2 evaluates request
        ↓
Decision stored
        ↓
Policy 4.3 deployed
        ↓
Continuation requested
```

A strong continuation test asserts:

```text
Old decision detected as stale
        ↓
No protected execution
        ↓
Explicit re-evaluation / defer / escalation path
```

The exact freshness rule is application-specific.

The non-execution invariant should not be.

---

## Acknowledgment Must Not Silently Become Authorization

An acknowledgment is evidence that an actor saw and accepted a required condition.

It is not universal execution authority.

Test this sequence:

```text
Decision = AcknowledgmentRequired
        ↓
Challenge issued
        ↓
Actor acknowledges
        ↓
Current policy / resource / capability checks still occur
```

A dangerous test gap is:

```text
Acknowledgment exists
        ↓
Execute
```

without checking whether the decision is still valid.

A useful regression case is:

```text
Policy 4.2 requires acknowledgment
        ↓
Actor acknowledges
        ↓
Policy 4.3 now denies the operation
        ↓
Executor invocation count = 0
```

This proves that acknowledgment does not silently override later policy.

---

## Capability Issuance Needs Its Own Policy Tests

If the system issues narrow execution authority after approval, test the issuance boundary separately.

Useful cases include:

```text
Allowed decision
        ↓
Capability may be issued
```

```text
Denied decision
        ↓
Capability issuance count = 0
```

```text
Deferred decision
        ↓
Capability issuance count = 0
```

```text
AcknowledgmentRequired without valid acknowledgment
        ↓
Capability issuance count = 0
```

Then test capability validation at execution:

- Correct actor.
- Correct resource.
- Correct audience.
- Correct operation.
- Not expired.
- Not revoked.
- Not replayed when single-use.
- Acceptable policy freshness.

A capability is a separate authority artifact.

Do not infer its correctness merely because the earlier policy decision was correct.

---

## Test the Execution Boundary, Not Only the Decision Object

The highest-value governance invariant often sits one step beyond the policy evaluator.

A small fake executor can make the boundary observable:

```csharp
public sealed class RecordingAccountExecutor
{
    public int InvocationCount { get; private set; }

    public Task DisableAsync(
        string accountId,
        CancellationToken cancellationToken)
    {
        InvocationCount++;
        return Task.CompletedTask;
    }
}
```

The host orchestration test can then assert:

```csharp
GovernanceDecision decision =
    await policyPipeline.EvaluateAsync(
        context,
        cancellationToken);

if (decision.CanProceed)
{
    await executor.DisableAsync(
        context.Account.AccountId,
        cancellationToken);
}

Assert.Equal(
    GovernanceDecisionOutcome.EscalationRecommended,
    decision.Outcome);

Assert.Equal(0, executor.InvocationCount);
```

The exact host code will differ.

The invariant remains:

```text
Non-executable governance result
        ↓
Protected side effect does not occur
```

---

## Test Unavailable Dependencies Separately from Explicit Policy Denial

A policy rule can intentionally deny:

```text
Cross-tenant operation
        ↓
Denied
Reason = account.disable.cross-tenant
```

A dependency can also fail:

```text
Policy registry unavailable
```

Those are not the same event.

A fail-closed host may map the dependency failure into a non-executable decision, but the test should preserve the distinction.

For example:

```text
Policy denial
    Outcome = Denied
    Reason = account.disable.cross-tenant

Dependency failure mapped fail-closed
    Outcome = Denied or Deferred according to host policy
    Reason = stable infrastructure/failure code
    Operational error is logged separately
```

Do not write a test that proves only:

```text
Both returned Denied
```

if the architecture promises different reason evidence and operational handling.

---

## Governance Outcomes and Exceptions Are Different Test Categories

Expected policy behavior belongs in structured outcomes.

Examples:

```text
Actor lacks required role
Resource is protected
Maintenance hold active
Acknowledgment missing
```

Those conditions should normally produce explicit governance decisions.

Unexpected infrastructure or programming failures may instead:

- Propagate.
- Be converted to a fail-closed decision.
- Be deferred.
- Be escalated.

The chosen failure posture should be tested explicitly.

Do not use `Assert.Throws` as the normal way to prove an expected policy denial unless the application deliberately models policy that way.

---

## Code Coverage Is Not Policy Coverage

A high line-coverage percentage can coexist with a weak policy test suite.

Imagine a method with many branches where tests execute most lines through incidental setup but never assert:

```text
Protected + administrator
        ↓
EscalationRecommended
```

or never prove:

```text
EscalationRecommended
        ↓
Executor calls = 0
```

Code coverage answers questions such as:

```text
Was this line executed?
Was this branch traversed?
```

Policy coverage asks different questions:

```text
Which policy distinctions are represented?
Which outcome classes are tested?
Which precedence conflicts are tested?
Which boundaries are tested?
Which historical versions are protected?
Which execution invariants are proved?
```

Both forms of coverage can be useful.

They are not interchangeable.

A practical review artifact is a policy-coverage matrix:

| Policy concern | Representative cases | Covered? |
| --- | --- | --- |
| Administrator requirement | admin / non-admin | Yes |
| Tenant boundary | same / different | Yes |
| Protected-account escalation | protected / unprotected | Yes |
| Maintenance deferment | hold / no hold | Yes |
| Missing-reason acknowledgment | present / empty / whitespace | Yes |
| Precedence conflict | warning + deny | Yes |
| Policy drift | same version / stale version | Yes |
| Non-execution | every non-proceedable outcome | Yes |

This table communicates more policy meaning than a single percentage.

---

## Property-Based Testing Can Strengthen Invariants

Property-based testing is useful when the invariant spans a broad input space and hand-written rows would be repetitive.

For example, if the policy contract says:

> A non-administrator must never receive a proceedable disable-account decision.

then a property can generate many combinations of:

- Tenant IDs.
- Protected status.
- Already-disabled status.
- Maintenance state.
- Reason text.

while holding:

```text
IsAdministrator = false
```

The property is:

```text
For every generated non-administrator context:
    decision.CanProceed == false
```

Property-based testing is especially useful for invariants such as:

- Denied decisions are never proceedable.
- A consumed single-use capability cannot become usable again.
- Cross-tenant contexts never produce executable authority.
- Reordering constraints does not change the final outcome when the documented composition policy is order-independent.
- Equivalent canonical policy representations produce the same fingerprint.

Use properties to complement explicit decision tables, not replace them.

Named rows remain valuable because they communicate policy intent to human reviewers.

Also avoid generators that merely recreate the production branching rules in test form.

---

## Snapshot and Golden-Master Tests Have Tradeoffs

Snapshot tests can be useful when a decision receipt contains structured evidence such as:

```text
Outcome
Reason codes
Policy version
Policy hash
Constraint evidence
Audit metadata
```

A golden master can reveal an unexpected shape change.

But it is a weak primary oracle for policy semantics.

Risks include:

- Large snapshots become difficult to review.
- Legitimate timestamps or IDs create noise.
- Developers may approve changed snapshots without understanding the policy change.
- A snapshot can preserve an incorrect result just as faithfully as a correct one.
- Formatting changes can obscure semantic changes.

A better pattern is:

```text
Explicit semantic assertions
        +
Optional snapshot for broader evidence shape
```

For example:

```text
Assert expected outcome
Assert expected reason codes
Assert policy version
Assert non-execution invariant
Then snapshot remaining stable receipt fields if useful
```

Never let "update the snapshot" become the entire policy-review process.

---

## Keep Tests from Becoming a Second Policy Engine

The test suite should describe policy expectations.

It should not independently implement the same policy algorithm.

### Fragile Approach

```csharp
expected =
    actor.IsAdmin && !account.IsProtected
        ? Allowed
        : Denied;
```

The test has become another policy evaluator.

### Better Approach

```text
Case: normal administrator
Expected: Allowed

Case: protected administrator
Expected: EscalationRecommended

Case: non-administrator
Expected: Denied
```

The expected result is stated directly.

This makes policy changes reviewable as changed cases rather than as synchronized edits to two algorithms.

---

## Treat the Decision Table as Versioned Review Material

When policy behavior is consequential, the table can be reviewed with the same seriousness as code.

Useful practices include:

- Give every row a meaningful scenario name.
- Include the policy version when version-specific behavior matters.
- Keep reason codes explicit.
- Record whether the result is proceedable.
- Add a row when a production incident exposes a missing class of behavior.
- Change expected rows only when the policy change is intentional.
- Preserve historical cases when they are needed to interpret old decisions or migrations.

A pull request that changes:

```text
protected account: EscalationRecommended
```

to:

```text
protected account: Denied
```

should be visibly different from a refactor that changes no policy semantics.

Decision-table tests make that distinction easier to review.

---

## A Practical Test-Selection Strategy

When the policy has many dimensions, use a deliberate selection process instead of attempting a Cartesian product of every possible value.

A practical sequence is:

1. Identify the policy facts.
2. Define equivalence classes for each fact.
3. Identify threshold boundaries.
4. Add one clear allowed case.
5. Add one case for every distinct non-allowed outcome used by the policy.
6. Add precedence-conflict cases.
7. Add not-applicable and empty-policy cases when composition uses them.
8. Add dependency-failure cases separately from business denial.
9. Add policy-version and drift cases where decisions can outlive evaluation.
10. Add execution-boundary tests for every non-executable outcome.
11. Add property-based tests for broad invariants where they reduce repetitive examples.
12. Add incident-derived regression cases when real failures expose missing distinctions.

If the number of combinations is still large, techniques such as pairwise or risk-based selection can help.

The goal remains **meaningful policy coverage**, not brute-force enumeration.

---

## A Compact Review Checklist

When reviewing a policy test suite, ask:

1. Can the policy requirements be read without opening the implementation?
2. Is there an explicit decision table or equivalent case model?
3. Does every governance outcome used by the policy have at least one representative test?
4. Are equivalence classes documented or obvious from the cases?
5. Are threshold boundaries tested immediately around the transition?
6. Are positive and negative cases distinguished from deferment, acknowledgment, and escalation?
7. Are individual constraint tests separated from composition tests?
8. Is precedence tested with conflicting results?
9. Is `NotApplicable` tested separately from `Allow` where the distinction matters?
10. Are full-evaluation and short-circuit expectations tested separately?
11. Are reason codes asserted where they are part of the observable contract?
12. Is policy identity/version asserted when decisions survive evaluation?
13. Do deterministic inputs produce deterministic policy-significant outputs?
14. Are historical policy changes represented intentionally rather than hidden in rewritten expectations?
15. Is stale decision evidence rejected or re-evaluated according to an explicit freshness rule?
16. Does acknowledgment remain separate from authorization?
17. Is capability issuance blocked for inappropriate decisions?
18. Does the execution boundary reject stale, invalid, replayed, or out-of-scope authority?
19. Are dependency failures distinguishable from ordinary policy denials?
20. Do non-executable outcomes prove protected executor invocation count remains zero?
21. Are tests asserting policy meaning rather than reimplementing the production algorithm?
22. Is code coverage being interpreted separately from policy coverage?

If several answers are unclear, the test suite may exercise policy code without actually protecting the policy architecture.

---

## Working References

The Learning repository and the `AsiBackbone` implementation repository contain useful examples at different layers.

| Testing concern | Reference | What to inspect |
| --- | --- | --- |
| Explicit outcome assertions | [`DecisionOutcomeTests`](https://github.com/AsiBackbone/Learning/blob/main/samples/policy-context-and-explicit-decision-outcomes/PolicyContextAndExplicitDecisionOutcomes.Tests/DecisionOutcomeTests.cs) | Direct assertions for `Denied`, `Deferred`, `AcknowledgmentRequired`, and `Allowed` |
| Table-like scenario coverage | [Policy Context sample program](https://github.com/AsiBackbone/Learning/blob/main/samples/policy-context-and-explicit-decision-outcomes/PolicyContextAndExplicitDecisionOutcomes/Program.cs) | Named policy scenarios covering all major disable-account outcomes |
| Composition invariants | [`DefaultAsiBackbonePolicyEvaluatorTests`](https://github.com/AsiBackbone/AsiBackbone/blob/main/tests/AsiBackbone.Core.Tests/Evaluation/DefaultAsiBackbonePolicyEvaluatorTests.cs) | Empty-policy behavior, warning/denial composition, exception posture, short-circuiting, and decision-policy interaction |
| End-to-end evaluator behavior | [`PolicyEvaluatorEndToEndTests`](https://github.com/AsiBackbone/AsiBackbone/blob/main/tests/AsiBackbone.Core.Tests/Evaluation/PolicyEvaluatorEndToEndTests.cs) | Policy-evaluator invariants across the real implementation pipeline |
| Policy provenance | [Policy Versioning and Decision Provenance](policy-versioning-and-decision-provenance.md) | Historical identity, drift, freshness, and version/hash boundaries |
| Bounded execution authority | [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) | Replay/use-state validation at the execution boundary |

These references demonstrate working techniques.

They do not imply that every application needs the same test framework, policy vocabulary, or exact case count.

---

## Relationship to the Governance Learning Path

The governance material can now be read as a progression:

```text
Policy Context and Explicit Decision Outcomes
        ↓
Constraint Composition and Policy Precedence
        ↓
Policy Versioning and Decision Provenance
        ↓
Practical Policy Testing and Decision-Table Strategies
        ↓
Hands-on labs and working implementation tests
```

The testing tutorial does not replace the earlier material.

It makes those architectural claims executable as regression expectations.

The recurring idea is:

```text
Policy statement
        ↓
Explicit context and constraints
        ↓
Documented composition and version
        ↓
Expected governance decision
        ↓
Observable continuation boundary
        ↓
Repeatable automated test
```

---

## Related Content

- [Governance Index](index.md) — view the governance learning path.
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) — start with explicit facts, reason codes, and structured outcomes.
- [Constraint Composition and Policy Precedence](constraint-composition-and-policy-precedence.md) — understand the composition rules that the decision-table tests should protect.
- [Policy Versioning and Decision Provenance](policy-versioning-and-decision-provenance.md) — extend regression cases across historical policy identity and drift.
- [Decision Before Execution](../tutorials/decision-before-execution.md) — revisit the boundary between a decision and a protected side effect.
- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) — test delayed continuation without turning acknowledgment into authorization.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — test capability issuance and host-owned execution boundaries.
- [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) — test replay, expiry, revocation, and bounded-use authority separately from policy approval.
- [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md) — compare the richer governance test surface with ordinary authorization requirements.

---

> **Test the policy boundary, not merely the branches that happen to implement it.**
