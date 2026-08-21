---
description: Practice replaying identical representative policy contexts against baseline and candidate policy versions, comparing decision changes, and preserving a no-execution simulation boundary.
---

# Lab — Policy Simulation and Change-Impact Analysis

**Learning objective:** Practice evaluating a baseline and candidate policy against the same deterministic policy contexts, compare structured decision differences before rollout, preserve policy provenance, and prove that simulation never invokes protected execution.

**Difficulty:** Intermediate

**Pattern classification:** General learning material

**Prerequisites:** Complete [Practical Policy Testing and Decision-Table Strategies](../governance/practical-policy-testing-and-decision-table-strategies.md) and [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md). Familiarity with [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) is recommended.

Testing asks:

> Does this policy behave as specified?

Simulation asks an additional question:

> If this candidate policy became authoritative, which representative decisions would change?

The central lesson is:

> **Policy simulation asks what would change before the new policy becomes authoritative.**

The required invariant is:

```text
Simulation mode
      ↓
Policy evaluation occurs
      ↓
Decision evidence produced
      ↓
Protected executor invocation count = 0
```

---

## What You Will Build

You will create a small local harness:

```text
Representative contexts
        │
   ┌────┴────┐
   ▼         ▼
Policy v1  Policy v2 candidate
   │         │
   ▼         ▼
Decisions Decisions
   └────┬────┘
        ▼
 Difference report
```

The report will identify:

- Unchanged decisions.
- Newly denied operations.
- Newly allowed operations.
- New acknowledgment requirements.
- New escalation requirements.
- Other outcome changes.
- Reason-code-only changes.
- Baseline and candidate policy identities.

You will then add a tenant-specific overlay, boundary contexts, a no-execution assertion, and an expected-change plan before making your own rollout recommendation.

---

## Starting Scenario

Use a small account-disable policy with deterministic context:

```text
CaseId
ActorIsAdministrator
SameTenant
IsProtected
MaintenanceHold
HasReason
TenantId
Region
AffectedRecords
```

Baseline:

```text
Policy: account-disable / 1.0

Non-administrator
    → Denied

Cross-tenant
    → Denied

Maintenance hold
    → Deferred

Protected resource
    → AcknowledgmentRequired

Missing reason
    → AcknowledgmentRequired

Otherwise
    → Allowed
```

Candidate:

```text
Policy: account-disable / 2.0-candidate

Protected resource
    → EscalationRecommended
```

All other rules initially remain the same.

---

## Prepare a Disposable Workspace

Use a temporary branch or disposable copy:

```bash
git switch -c lab/policy-simulation
```

Create a scratch console project:

```bash
dotnet new console -o .lab-work/policy-simulation
```

The scratch project should not be wired to a real account service, queue, external API, deployment system, payment system, or other protected executor.

Run it once:

```bash
dotnet run --project .lab-work/policy-simulation
```

Then replace `Program.cs` as you work through the lab.

---

# Part 1 — Define the Policy Vocabulary

Start with explicit outcomes:

```csharp
public enum GovernanceOutcome
{
    Allowed,
    Denied,
    Deferred,
    AcknowledgmentRequired,
    EscalationRecommended
}
```

Add stable reasons:

```csharp
public sealed record DecisionReason(
    string Code,
    string Message);
```

Add policy identity:

```csharp
public sealed record PolicyIdentity(
    string PolicyId,
    string Version)
{
    public override string ToString() =>
        $"{PolicyId}/{Version}";
}
```

Add deterministic context:

```csharp
public sealed record AccountDisableContext(
    string CaseId,
    bool ActorIsAdministrator,
    bool SameTenant,
    bool IsProtected,
    bool MaintenanceHold,
    bool HasReason,
    string TenantId,
    string Region,
    int AffectedRecords);
```

Add a structured decision:

```csharp
public sealed record SimulationDecision(
    GovernanceOutcome Outcome,
    IReadOnlyList<DecisionReason> Reasons,
    IReadOnlyList<PolicyIdentity> Contributors)
{
    public bool CanProceed =>
        Outcome == GovernanceOutcome.Allowed;
}
```

The policy identity is evidence. It is not execution authority.

---

## Add a Policy Contract

```csharp
public interface IAccountDisablePolicy
{
    PolicyIdentity Identity { get; }

    SimulationDecision Evaluate(
        AccountDisableContext context);
}
```

The interface contains no executor.

A simulator needs evaluation capability. It does not need side-effect capability.

---

# Part 2 — Implement the Baseline Policy

```csharp
public sealed class BaselinePolicy
    : IAccountDisablePolicy
{
    public PolicyIdentity Identity { get; } =
        new("account-disable", "1.0");

    public SimulationDecision Evaluate(
        AccountDisableContext context)
    {
        if (!context.ActorIsAdministrator)
        {
            return Decision(
                GovernanceOutcome.Denied,
                "account.disable.not-administrator",
                "Only administrators may disable accounts.");
        }

        if (!context.SameTenant)
        {
            return Decision(
                GovernanceOutcome.Denied,
                "account.disable.cross-tenant",
                "Cross-tenant account disable is not permitted.");
        }

        if (context.MaintenanceHold)
        {
            return Decision(
                GovernanceOutcome.Deferred,
                "account.disable.maintenance-hold",
                "Account changes are deferred during maintenance.");
        }

        if (context.IsProtected)
        {
            return Decision(
                GovernanceOutcome.AcknowledgmentRequired,
                "account.disable.protected.acknowledgment",
                "Protected accounts require acknowledgment.");
        }

        if (!context.HasReason)
        {
            return Decision(
                GovernanceOutcome.AcknowledgmentRequired,
                "account.disable.reason-required",
                "A reason must be acknowledged.");
        }

        return Decision(
            GovernanceOutcome.Allowed,
            "account.disable.allowed",
            "The operation may proceed.");
    }

    private SimulationDecision Decision(
        GovernanceOutcome outcome,
        string code,
        string message) =>
        new(
            outcome,
            [new DecisionReason(code, message)],
            [Identity]);
}
```

The rule order is policy:

```text
Administrator
      ↓
Tenant
      ↓
Maintenance
      ↓
Protected
      ↓
Reason
      ↓
Allowed
```

---

# Part 3 — Implement the Candidate Policy

Create a candidate that changes only the protected-resource rule:

```csharp
public sealed class CandidatePolicy
    : IAccountDisablePolicy
{
    public PolicyIdentity Identity { get; } =
        new("account-disable", "2.0-candidate");

    public SimulationDecision Evaluate(
        AccountDisableContext context)
    {
        if (!context.ActorIsAdministrator)
        {
            return Decision(
                GovernanceOutcome.Denied,
                "account.disable.not-administrator",
                "Only administrators may disable accounts.");
        }

        if (!context.SameTenant)
        {
            return Decision(
                GovernanceOutcome.Denied,
                "account.disable.cross-tenant",
                "Cross-tenant account disable is not permitted.");
        }

        if (context.MaintenanceHold)
        {
            return Decision(
                GovernanceOutcome.Deferred,
                "account.disable.maintenance-hold",
                "Account changes are deferred during maintenance.");
        }

        if (context.IsProtected)
        {
            return Decision(
                GovernanceOutcome.EscalationRecommended,
                "account.disable.protected.escalation",
                "Protected accounts require escalation.");
        }

        if (!context.HasReason)
        {
            return Decision(
                GovernanceOutcome.AcknowledgmentRequired,
                "account.disable.reason-required",
                "A reason must be acknowledged.");
        }

        return Decision(
            GovernanceOutcome.Allowed,
            "account.disable.allowed",
            "The operation may proceed.");
    }

    private SimulationDecision Decision(
        GovernanceOutcome outcome,
        string code,
        string message) =>
        new(
            outcome,
            [new DecisionReason(code, message)],
            [Identity]);
}
```

The candidate is not authoritative merely because the class exists.

Use a candidate label such as `2.0-candidate` rather than a production-looking version if the version has not been approved or deployed.

---

# Part 4 — Build Representative Contexts

Use synthetic contexts first:

```csharp
AccountDisableContext[] contexts =
[
    new("normal-admin", true, true, false, false, true,
        "tenant-a", "us-east", 1),

    new("non-admin", false, true, false, false, true,
        "tenant-a", "us-east", 1),

    new("cross-tenant", true, false, false, false, true,
        "tenant-a", "us-east", 1),

    new("maintenance", true, true, false, true, true,
        "tenant-a", "us-east", 1),

    new("missing-reason", true, true, false, false, false,
        "tenant-a", "us-east", 1),

    new("protected-a", true, true, true, false, true,
        "tenant-a", "us-east", 1),

    new("protected-b", true, true, true, false, true,
        "tenant-b", "us-east", 1),

    new("protected-missing-reason", true, true, true, false, false,
        "tenant-a", "eu-west", 1),

    new("protected-maintenance", true, true, true, true, true,
        "tenant-a", "us-east", 1),

    new("normal-tenant-b", true, true, false, false, true,
        "tenant-b", "us-east", 10)
];
```

Before running, predict the decisions:

| Case | Baseline | Candidate | Expected change? |
| --- | --- | --- | --- |
| normal-admin | Allowed | Allowed | No |
| non-admin | Denied | Denied | No |
| cross-tenant | Denied | Denied | No |
| maintenance | Deferred | Deferred | No |
| missing-reason | AcknowledgmentRequired | AcknowledgmentRequired | No |
| protected-a | AcknowledgmentRequired | EscalationRecommended | Yes |
| protected-b | AcknowledgmentRequired | EscalationRecommended | Yes |
| protected-missing-reason | AcknowledgmentRequired | EscalationRecommended | Yes |
| protected-maintenance | Deferred | Deferred | No |
| normal-tenant-b | Allowed | Allowed | No |

The `protected-maintenance` row is intentionally useful.

A learner may expect all protected resources to escalate under the candidate, but maintenance is evaluated first, so the row remains `Deferred`.

That is an example of simulation exposing precedence impact rather than only changed source code.

---

# Part 5 — Build the Simulation Runner

```csharp
public sealed record SimulationComparison(
    string CaseId,
    AccountDisableContext Context,
    SimulationDecision Baseline,
    SimulationDecision Candidate);

public sealed class PolicySimulationRunner
{
    public IReadOnlyList<SimulationComparison> Run(
        IEnumerable<AccountDisableContext> contexts,
        IAccountDisablePolicy baseline,
        IAccountDisablePolicy candidate)
    {
        List<SimulationComparison> results = [];

        foreach (AccountDisableContext context in contexts)
        {
            results.Add(
                new SimulationComparison(
                    context.CaseId,
                    context,
                    baseline.Evaluate(context),
                    candidate.Evaluate(context)));
        }

        return results;
    }
}
```

The runner does not receive an account executor, HTTP client, queue publisher, deployment client, or other side-effect dependency.

---

## Replay Identical Contexts

The comparison must isolate the policy variable:

```text
One context snapshot
        │
   ┌────┴────┐
   ▼         ▼
Baseline  Candidate
```

Avoid recapturing live context separately for each policy if the purpose is policy-change analysis.

Otherwise input drift can be mistaken for policy impact.

---

# Part 6 — Classify Differences

```csharp
public enum ChangeKind
{
    Unchanged,
    NewlyDenied,
    NewlyAllowed,
    NewAcknowledgmentRequirement,
    NewEscalationRequirement,
    OutcomeChanged,
    ReasonChangedOnly
}
```

Write a classifier. A useful shape is:

```csharp
public static ChangeKind Classify(
    SimulationComparison comparison)
{
    GovernanceOutcome before =
        comparison.Baseline.Outcome;

    GovernanceOutcome after =
        comparison.Candidate.Outcome;

    if (before == after)
    {
        string beforeReasons = string.Join(
            "|",
            comparison.Baseline.Reasons.Select(r => r.Code));

        string afterReasons = string.Join(
            "|",
            comparison.Candidate.Reasons.Select(r => r.Code));

        return beforeReasons == afterReasons
            ? ChangeKind.Unchanged
            : ChangeKind.ReasonChangedOnly;
    }

    if (after == GovernanceOutcome.Denied)
    {
        return ChangeKind.NewlyDenied;
    }

    if (before == GovernanceOutcome.Denied &&
        after == GovernanceOutcome.Allowed)
    {
        return ChangeKind.NewlyAllowed;
    }

    if (after == GovernanceOutcome.AcknowledgmentRequired)
    {
        return ChangeKind.NewAcknowledgmentRequirement;
    }

    if (after == GovernanceOutcome.EscalationRecommended)
    {
        return ChangeKind.NewEscalationRequirement;
    }

    return ChangeKind.OutcomeChanged;
}
```

Do not classify every row as changed merely because baseline and candidate policy identities differ. Policy identity belongs in the evidence; behavioral change is about outcome and reason semantics.

---

# Part 7 — Produce the Difference Report

```csharp
IAccountDisablePolicy baseline =
    new BaselinePolicy();

IAccountDisablePolicy candidate =
    new CandidatePolicy();

PolicySimulationRunner runner =
    new();

IReadOnlyList<SimulationComparison> results =
    runner.Run(contexts, baseline, candidate);

foreach (SimulationComparison result in results)
{
    Console.WriteLine(
        $"{result.CaseId,-28} " +
        $"{result.Baseline.Outcome,-26} -> " +
        $"{result.Candidate.Outcome,-26} " +
        $"{Classify(result)}");
}
```

The expected behavioral summary is:

```text
Contexts evaluated: 10

Unchanged:                   7
New escalation requirement: 3
Newly denied:                0
Newly allowed:               0
New acknowledgment:          0
```

Three of the ten synthetic cases change from:

```text
AcknowledgmentRequired
```

to:

```text
EscalationRecommended
```

That is 30% of this small corpus.

Do not translate that into a claim that 30% of future production traffic will change.

---

## Preserve Reasons and Policy Identities

For changed rows, print:

```text
CaseId
Baseline outcome
Baseline reason codes
Baseline contributors
Candidate outcome
Candidate reason codes
Candidate contributors
```

A useful row resembles:

```text
Case:
protected-a

Baseline:
Outcome = AcknowledgmentRequired
Reason = account.disable.protected.acknowledgment
Policy = account-disable/1.0

Candidate:
Outcome = EscalationRecommended
Reason = account.disable.protected.escalation
Policy = account-disable/2.0-candidate
```

This is comparison evidence. It does not make the candidate authoritative.

---

# Part 8 — Distinguish Expected Changes from Regressions

Create an expected-change plan before judging the candidate:

```text
protected-a
    AcknowledgmentRequired
        →
    EscalationRecommended

protected-b
    AcknowledgmentRequired
        →
    EscalationRecommended

protected-missing-reason
    AcknowledgmentRequired
        →
    EscalationRecommended
```

Classify observed results as:

```text
Expected change
Unexpected change
Expected unchanged
Unexpected unchanged
```

`Unexpected unchanged` matters. If a policy change was intended to affect a case but simulation shows no difference, the candidate or change plan may be wrong.

---

## Examine the Surprising Unchanged Case

`protected-maintenance` remains `Deferred` because maintenance precedence is unchanged.

Ask:

1. Is this precedence intentional?
2. Should maintenance deferment outrank protected-resource escalation?
3. Did the change request mean all protected resources, or only those not already deferred?
4. Does the reason evidence make the result understandable?
5. Should the candidate be revised?

The simulator exposes the behavior. It does not decide which policy is correct.

---

# Part 9 — Prove Newly Allowed Regression Detection

Temporarily introduce a candidate bug:

```csharp
if (!context.SameTenant)
{
    return Decision(
        GovernanceOutcome.Allowed,
        "account.disable.cross-tenant.candidate-bug",
        "Candidate bug for lab.");
}
```

Re-run the simulation.

The `cross-tenant` row should become:

```text
Denied
   →
Allowed

ChangeKind = NewlyAllowed
```

Treat unexpected authority broadening as a high-signal regression.

Then repair the candidate.

---

# Part 10 — Add a Tenant-Specific Candidate Overlay

Add a candidate tenant rule:

```text
Tenant-b protected resources
    → Denied
```

One teaching implementation is:

```csharp
public sealed class TenantOverlayCandidatePolicy(
    IAccountDisablePolicy basePolicy)
    : IAccountDisablePolicy
{
    public PolicyIdentity Identity { get; } =
        new(
            "tenant-b-account-disable",
            "1.0-candidate");

    public SimulationDecision Evaluate(
        AccountDisableContext context)
    {
        SimulationDecision baseDecision =
            basePolicy.Evaluate(context);

        if (context.TenantId == "tenant-b" &&
            context.IsProtected)
        {
            return new(
                GovernanceOutcome.Denied,
                [
                    new DecisionReason(
                        "tenant-b.protected.denied",
                        "Tenant-b prohibits disabling protected accounts.")
                ],
                [
                    ..baseDecision.Contributors,
                    Identity
                ]);
        }

        return baseDecision;
    }
}
```

Wrap the candidate:

```csharp
IAccountDisablePolicy tenantCandidate =
    new TenantOverlayCandidatePolicy(candidate);
```

The `protected-b` row should now become:

```text
Baseline:
AcknowledgmentRequired

Candidate + tenant overlay:
Denied
```

and candidate contributors should include:

```text
account-disable/2.0-candidate
tenant-b-account-disable/1.0-candidate
```

Keep the input fact `IsProtected = true` unchanged. Let the overlay make its own policy contribution explicit rather than mutating the fact to encode stricter policy.

---

# Part 11 — Add Boundary Contexts

Add a candidate threshold:

```text
AffectedRecords >= 5000
    → EscalationRecommended
```

Then add otherwise-identical contexts with:

```text
AffectedRecords = 4999
AffectedRecords = 5000
AffectedRecords = 5001
```

The candidate should make the threshold transition visible.

Also try:

```text
AffectedRecords = 5000
MaintenanceHold = true
```

Which rule wins depends on candidate precedence. Simulation should reveal the behavior; your policy specification should decide whether it is intentional.

---

# Part 12 — Prove Simulation Never Executes

Define a protected executor only as a guard:

```csharp
public interface IProtectedAccountExecutor
{
    Task ExecuteAsync(
        AccountDisableContext context,
        CancellationToken cancellationToken);
}

public sealed class RecordingExecutor
    : IProtectedAccountExecutor
{
    public int InvocationCount { get; private set; }

    public Task ExecuteAsync(
        AccountDisableContext context,
        CancellationToken cancellationToken)
    {
        InvocationCount++;
        return Task.CompletedTask;
    }
}
```

Run the simulator beside the fake:

```csharp
var executor =
    new RecordingExecutor();

PolicySimulationRunner simulator =
    new();

_ = simulator.Run(
    contexts,
    baseline,
    candidate);

if (executor.InvocationCount != 0)
{
    throw new InvalidOperationException(
        "Simulation invoked the protected executor.");
}
```

The executor is intentionally not passed to the simulator.

This is stronger than depending on a `dryRun` boolean inside an execution-capable workflow.

Even when both policies return `Allowed`, simulation must still leave:

```text
Executor invocation count = 0
```

---

## Prefer Architectural Non-Reachability

Weaker:

```text
Simulator
   ↓
Executor available
   ↓
if (!dryRun)
    execute
```

Stronger:

```text
Simulator
   ↓
Policies only
   ↓
Decision evidence
   ↓
No executor dependency exists
```

If a production design reuses shared orchestration, add explicit tests proving simulation mode cannot reach real handlers, queues, webhooks, deployment APIs, payment rails, robotics gateways, or other protected side effects.

---

# Part 13 — Preserve Deterministic Simulation Inputs

A minimal replay record can be:

```csharp
public sealed record SimulationCaseRecord(
    string CaseId,
    AccountDisableContext Context,
    DateTimeOffset CapturedAt,
    string SourceKind);
```

Examples:

```text
SourceKind = synthetic
SourceKind = historical-snapshot
```

Avoid rebuilding historical contexts from current state if the original decision-time facts no longer exist. That answers a different question.

---

# Part 14 — Compare Historical and Synthetic Data

| Source | Strengths | Limitations |
| --- | --- | --- |
| Synthetic contexts | Deterministic, reviewable, privacy-friendly, easy to target boundaries | May omit real combinations and frequency |
| Historical snapshots | Reflect observed production combinations and approximate frequency | Can contain sensitive data, encode past blind spots, and fail to represent future traffic |
| Incident cases | High-value regressions | Biased toward previously observed failures |
| Generated combinations | Broad structural coverage | Can create unrealistic combinations or overwhelming volume |

A strong simulation corpus may combine several sources.

Historical replay answers:

> How would the candidate have classified these historical contexts?

It does not prove:

> This is how all future requests will behave.

---

# Part 15 — Treat Simulation Data as Potentially Sensitive

Historical policy context may contain user, tenant, resource, location, transaction, classification, risk, or security information.

Do not copy full production requests when the policy uses only a small subset of fields.

Define:

- Which fields are required.
- How identifiers are minimized or pseudonymized.
- Who can access the corpus.
- How long it is retained.
- Whether equivalence-class labels can replace raw values.
- Whether the corpus may leave its original tenant or region boundary.

A simulation environment can become a secondary sensitive-data store if this is ignored.

---

# Part 16 — Compare Reason-Code Changes

Temporarily keep the same outcome but change:

```text
account.disable.reason-required
```

to:

```text
account.disable.reason-missing-v2
```

The missing-reason row should be classified as:

```text
ReasonChangedOnly
```

Ask whether downstream reporting, tests, dashboards, or integrations treat the reason code as a stable contract.

---

# Part 17 — Preserve Contributor Identity

A useful report row can contain:

```csharp
public sealed record SimulationReportRow(
    string CaseId,
    ChangeKind ChangeKind,
    GovernanceOutcome BaselineOutcome,
    IReadOnlyList<string> BaselineReasons,
    IReadOnlyList<string> BaselinePolicies,
    GovernanceOutcome CandidateOutcome,
    IReadOnlyList<string> CandidateReasons,
    IReadOnlyList<string> CandidatePolicies);
```

This allows the report to answer:

```text
What changed?
Why?
Which baseline policy produced the old result?
Which candidate policy produced the proposed result?
```

That creates a bridge between simulation and decision provenance.

---

# Part 18 — Add an Expected-Impact Check

```csharp
HashSet<string> expectedChangedCases =
[
    "protected-a",
    "protected-b",
    "protected-missing-reason"
];
```

Compare the expected set with actual changed case IDs.

If they differ, print a clear warning.

Do not turn this teaching check into an automatic production deployment decision without an explicit change-governance design.

Expected behavior for the corpus is useful evidence. It is not proof of production safety.

---

# Part 19 — Connect Simulation to Rollout and Rollback

Produce a compact summary:

```text
Baseline:
account-disable/1.0

Candidate:
account-disable/2.0-candidate

Representative cases:
10

Behaviorally changed:
3

New escalation requirements:
3

Newly allowed:
0

Newly denied:
0

Unexpected changes:
0

No-execution invariant:
Pass
```

Then choose one:

```text
Roll forward
Revise candidate policy
Run additional simulation
Stage rollout
Abandon change
```

The simulator should not make that choice for you.

Rollback is also a policy change. You can use the same harness to compare a current policy with a rollback target before restoring an older version.

---

# Part 20 — Optional Probabilistic Extension

After [Deterministic and Probabilistic Inputs in Policy Evaluation](../governance/deterministic-and-probabilistic-inputs-in-policy-evaluation.md), add a captured signal:

```text
FraudProbability = 0.81
ModelVersion = fraud-v8
ObservedAt = fixed timestamp
```

Replay the same captured observation through baseline and candidate policy.

Do not call a live model separately for each policy if the objective is to isolate policy change. Otherwise model-output variation and policy variation become mixed together.

---

# Part 21 — Write Focused Tests

At minimum, verify:

### Same Context Reaches Both Policies

```text
Same CaseId
Same context values
```

### Protected Cases Change as Intended

```text
Baseline = AcknowledgmentRequired
Candidate = EscalationRecommended
```

### Maintenance Precedence Is Visible

```text
Protected + Maintenance
Baseline = Deferred
Candidate = Deferred
```

### Cross-Tenant Does Not Become Allowed

```text
Baseline = Denied
Candidate = Denied
```

### Newly Allowed Regression Is Detectable

The temporary candidate bug returns:

```text
NewlyAllowed
```

### Reason-Only Change Is Detectable

Same outcome, changed reason code:

```text
ReasonChangedOnly
```

### Tenant Overlay Contributor Is Preserved

Candidate report contains both candidate policy identities.

### Simulation Never Executes

```text
Allowed case exists
+
Simulation runs
      ↓
Executor InvocationCount = 0
```

Use named expected cases rather than reimplementing policy logic inside the test oracle.

---

# Part 22 — Review False Confidence

Simulation can look rigorous while still be weak.

Examples:

### Only Happy Paths

No protected, cross-tenant, maintenance, missing-reason, or boundary cases are included.

### Historical Corpus Has a Blind Spot

No tenant-b traffic existed in the replay period, so the tenant overlay is effectively untested.

### Baseline and Candidate Call Different Live Dependencies

Input drift is mistaken for policy impact.

### Historical Context Is Rebuilt from Current Facts

The replay no longer represents the historical decision-time state.

### Exact Match Is Treated as Approval

The report matches expectations, so normal review is skipped.

Simulation is evidence for a decision. It is not the decision authority.

---

# Part 23 — Produce the Change-Impact Summary

Your final summary should include:

```text
Baseline policy identity
Candidate policy identity
Corpus type
Context count
Changed outcome count
Reason-only change count
Newly allowed count
Newly denied count
New acknowledgment count
New escalation count
Unexpected change count
Boundary cases included
Regional / tenant overlays included
Protected executor invocation count
Known corpus limitations
Recommended next step
```

For the initial corpus, note the intentionally surprising unchanged case:

```text
Protected + maintenance remains Deferred
because maintenance precedence is unchanged.
```

---

# Part 24 — Make the Rollout Recommendation

Choose one:

```text
Roll forward
Revise candidate policy
Run additional simulation
Stage rollout
Abandon change
```

Write one paragraph explaining your recommendation using the simulation evidence and its limitations.

Do not write:

```text
The simulator says to deploy.
```

The simulator did not make that decision.

A candidate that increases escalation may also change reviewer load, queue depth, timeouts, and degraded-mode behavior. Decision impact is only one part of rollout impact.

---

## Historical Replay Checklist

If you repeat the lab with historical data, verify:

1. Are contexts captured from the correct decision-time state?
2. Are identifiers minimized or pseudonymized where practical?
3. Are sensitive fields excluded unless they affect policy?
4. Is the corpus allowed in the simulation environment?
5. Does every row identify its source period?
6. Are old inputs semantically compatible with the candidate?
7. Are missing fields represented explicitly rather than invented?
8. Are historical scores replayed as captured observations when practical?
9. Are new candidate fields given a documented default or marked unavailable?
10. Is the period representative enough for the question being asked?
11. Are incident-derived edge cases included separately?
12. Are future-facing synthetic boundary cases also included?

---

## Candidate Policy Review Checklist

Before recommending rollout, ask:

1. Which behavior changes were intended?
2. Which observed changes were unexpected?
3. Did anything become newly allowed?
4. Did anything become newly denied?
5. Did acknowledgment volume increase?
6. Did escalation volume increase?
7. Did reason codes change?
8. Did contributing policy identities change as expected?
9. Were tenant and regional overlays represented?
10. Were threshold boundaries included?
11. Did precedence create surprising unchanged cases?
12. Did the candidate rely on new unavailable data?
13. Did both policies receive identical inputs?
14. Did any live probabilistic service introduce nondeterminism?
15. Did protected executor invocation remain zero?
16. Does the corpus contain sensitive data?
17. What production scenarios are missing?
18. What staged-rollout telemetry would detect problems?
19. Is rollback behavior understood?
20. Who owns the final deployment decision?

If several answers are unclear, run additional simulation or revise the candidate before treating the report as strong rollout evidence.

---

## What Simulation Does Not Prove

Policy simulation does not automatically prove:

- Production safety.
- Correctness for unseen contexts.
- Regulatory compliance.
- Absence of security defects.
- Correct external dependency behavior.
- Performance under production load.
- Human reviewer capacity.
- Availability of escalation authorities.
- Correctness of model-derived inputs.
- Correctness of the candidate policy itself.
- Successful execution.
- Safe rollback.
- That historical traffic predicts future traffic.

The lab makes a narrower claim:

> The simulator can make decision differences observable for a defined corpus before a candidate policy becomes authoritative.

---

## Working Implementation References

This lab is framework-neutral.

| Learning concern | Reference | What to inspect |
| --- | --- | --- |
| Policy evaluation | [`DefaultAsiBackbonePolicyEvaluator`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Evaluation/DefaultAsiBackbonePolicyEvaluator.cs) | A concrete evaluation pipeline that returns governance decisions without performing host side effects. |
| Structured decisions | [`GovernanceDecision`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Decisions/GovernanceDecision.cs) | Outcome, reasons, correlation, and policy identity that can participate in comparison evidence. |
| Host-specific decision policy | [`IAsiBackboneDecisionPolicy`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Evaluation/IAsiBackboneDecisionPolicy.cs) | A boundary where candidate host/domain decision behavior can be evaluated separately from execution. |
| Policy pipeline explanation | [Policy Evaluator Pipeline](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/policy-evaluator-pipeline.md) | Evaluation and composition boundaries useful when designing replayable policy inputs. |
| Decision-policy examples | [Custom Decision Policy Examples](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/custom-decision-policy-examples.md) | Examples of host policy variations that could be compared in a simulation corpus. |
| Host-owned execution | [Host-Owned Execution Enforcement](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/host-owned-execution-enforcement.md) | Why evaluating an allowed result does not require or imply performing the protected action. |

Learning does not require a particular simulation service, policy-management product, event store, data warehouse, or deployment controller.

---

## Related Content

- [Practical Policy Testing and Decision-Table Strategies](../governance/practical-policy-testing-and-decision-table-strategies.md) — establish current-policy expectations, equivalence classes, boundary cases, and execution invariants before comparing policy versions.
- [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) — preserve baseline, candidate, and contributing policy identities in comparison evidence.
- [Constraint Composition and Policy Precedence](../governance/constraint-composition-and-policy-precedence.md) — understand why a changed rule may not affect a context when an earlier constraint still determines the outcome.
- [Regional and Tenant Policy Overlays](../advanced/regional-and-tenant-policy-overlays.md) — extend simulation across multiple policy authorities and contributor identities.
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) — use explicit deterministic policy contexts and structured outcomes as replay inputs and comparison results.
- [Deterministic and Probabilistic Inputs in Policy Evaluation](../governance/deterministic-and-probabilistic-inputs-in-policy-evaluation.md) — capture probabilistic observations once when the objective is to isolate policy changes from signal-generation changes.
- [Human-in-the-Loop Governance Workflows](../governance/human-in-the-loop-governance-workflows.md) — consider reviewer workload when a candidate increases human review.
- [Escalation Patterns in Governed Systems](../governance/escalation-patterns-in-governed-systems.md) — consider routing capacity, timeout, loop, and authority effects when a candidate increases escalation.
- [Safe Degraded Mode and Fail-Safe Governance](safe-degraded-mode-and-fail-safe-governance.md) — examine unavailable dependencies or escalation paths introduced by a candidate.
- [Policy-Version Evidence in Governance Decisions](policy-version-evidence-in-governance-decisions.md) — practice decision-time policy identity and drift before comparing whole policy versions.
- [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) — keep simulation replay separate from execution-authority replay.

---

> **Simulate the decision change. Keep the side effect out of the simulator.**
