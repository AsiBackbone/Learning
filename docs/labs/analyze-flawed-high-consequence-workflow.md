---
description: Diagnose a deliberately flawed high-consequence account-disable workflow by tracing trust, authority, execution, replay, drift, failure, and evidence paths before redesigning it around explicit boundaries.
---

# Lab — Analyze a Deliberately Flawed High-Consequence Workflow

**Learning objective:** Inspect an unfamiliar high-consequence workflow, discover architectural defects without being given a checklist of answers, trace every path by which intent can become execution authority, and redesign the system so trust, governance, acknowledgment, scoped authority, freshness, execution, and evidence are explicit and testable.

**Difficulty:** Advanced

**Pattern classification:** General learning material

**Prerequisites:** Recommended — [Decision Before Execution](../tutorials/decision-before-execution.md), [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md), [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md), [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md), [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md), [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md), and [Threat Modeling as Architecture Reasoning](../security/threat-modeling-as-architecture-reasoning.md).

This lab is intentionally different from the earlier single-pattern exercises.

You are not told every defect in advance.

The starting design looks plausible, uses familiar framework features, returns structured results, calls policy and AI services, caches approvals, logs activity, and can pass ordinary happy-path tests.

It is also intentionally unsafe.

Your job is to discover **why**.

The central lesson is:

> **High-consequence architecture should be evaluated by the authority paths and failure modes it permits, not merely by whether the happy path works.**

---

# Scenario — Account Disablement

You maintain an internal administration API for a fictional SaaS platform.

The endpoint is:

```text
POST /accounts/{id}/disable
```

Disabling an account is operationally significant:

- interactive access stops;
- active sessions are revoked asynchronously;
- scheduled jobs owned by the account may stop;
- support intervention may be required to restore access;
- a duplicate or incorrect disablement is not considered harmless.

The application uses:

- ASP.NET Core authentication and authorization;
- an account repository;
- an external policy service;
- an AI reviewer that recommends whether the disablement appears reasonable;
- a cache intended to reduce repeated approval work;
- an administrative credential provider;
- an external account administration service;
- an audit sink.

The team reports:

> "The endpoint is protected by authorization, policy is checked, AI provides a second opinion, approvals are cached, and all successful disables are audited."

Do not accept or reject that claim yet.

Trace the architecture first.

---

# Part 1 — Read the Starting Workflow Without Fixing It

Assume the following code is a reduced teaching sketch of the production design.

The names are intentionally ordinary.

Some defects are visible in one method.

Others appear only when you compare components or reason about time, retries, failure, and alternate paths.

```csharp
public sealed record DisableAccountRequest(
    string RequestedRole,
    string Region,
    int RiskScore,
    bool AcceptedRisk,
    string Reason);

public sealed record AccountSnapshot(
    string AccountId,
    string TenantId,
    string Region,
    string Status,
    long StateVersion,
    string OwnerEmail,
    string SupportNotes);

public sealed record AiRecommendation(
    bool Approve,
    string Explanation,
    string SuggestedPolicyVersion);

public sealed record PolicyResponse(
    bool Allowed,
    bool Escalate,
    string ReasonCode,
    string PolicyVersion);

public sealed record CachedApproval(
    bool Approved,
    DateTimeOffset CreatedUtc);

public enum DisableOutcome
{
    Succeeded,
    Denied,
    AcknowledgmentRequired
}

public sealed record DisableAccountResult(
    DisableOutcome Outcome,
    string ReasonCode);

public interface IAccountRepository
{
    Task<AccountSnapshot?> FindAsync(
        string accountId,
        CancellationToken cancellationToken);
}

public interface IAiReviewer
{
    Task<AiRecommendation> ReviewAsync(
        object modelVisibleContext,
        string instruction,
        CancellationToken cancellationToken);
}

public interface IPolicyClient
{
    Task<PolicyResponse> EvaluateAsync(
        object policyContext,
        CancellationToken cancellationToken);
}

public interface IApprovalCache
{
    Task<CachedApproval?> GetAsync(
        string accountId,
        CancellationToken cancellationToken);

    Task SetAsync(
        string accountId,
        CachedApproval approval,
        CancellationToken cancellationToken);
}

public interface IAdminCredentialProvider
{
    Task<string> GetStandingAdministratorTokenAsync(
        CancellationToken cancellationToken);
}

public interface IAccountAdministrationClient
{
    Task DisableAsync(
        string accountId,
        string administratorToken,
        CancellationToken cancellationToken);
}

public interface IAuditSink
{
    Task WriteAsync(
        string eventName,
        object data,
        CancellationToken cancellationToken);
}
```

The controller and workflow are:

```csharp
[ApiController]
public sealed class AccountsController(
    DisableAccountWorkflow workflow,
    IApprovalCache approvalCache,
    IAdminCredentialProvider credentials,
    IAccountAdministrationClient administrationClient)
    : ControllerBase
{
    [Authorize(Roles = "AccountAdministrator")]
    [HttpPost("/accounts/{id}/disable")]
    public async Task<IActionResult> DisableAsync(
        string id,
        DisableAccountRequest request,
        CancellationToken cancellationToken)
    {
        DisableAccountResult result =
            await workflow.ValidateAndExecuteAsync(
                User,
                id,
                request,
                cancellationToken);

        return result.Outcome switch
        {
            DisableOutcome.Succeeded => Ok(result),
            DisableOutcome.AcknowledgmentRequired => Conflict(result),
            _ => Forbid()
        };
    }

    // Operations asked for a retry route so support can recover quickly.
    [Authorize(Roles = "Support")]
    [HttpPost("/accounts/{id}/disable/retry")]
    public async Task<IActionResult> RetryDisableAsync(
        string id,
        CancellationToken cancellationToken)
    {
        CachedApproval? approval =
            await approvalCache.GetAsync(id, cancellationToken);

        if (approval?.Approved != true)
        {
            return Forbid();
        }

        string token =
            await credentials.GetStandingAdministratorTokenAsync(
                cancellationToken);

        await administrationClient.DisableAsync(
            id,
            token,
            cancellationToken);

        return Ok();
    }
}

public sealed class DisableAccountWorkflow(
    IAccountRepository accounts,
    IAiReviewer aiReviewer,
    IPolicyClient policyClient,
    IApprovalCache approvalCache,
    IAdminCredentialProvider credentials,
    IAccountAdministrationClient administrationClient,
    IAuditSink audit)
{
    public async Task<DisableAccountResult> ValidateAndExecuteAsync(
        ClaimsPrincipal caller,
        string accountId,
        DisableAccountRequest request,
        CancellationToken cancellationToken)
    {
        AccountSnapshot? account =
            await accounts.FindAsync(
                accountId,
                cancellationToken);

        if (account is null)
        {
            return new(
                DisableOutcome.Denied,
                "account.not-found");
        }

        AiRecommendation recommendation =
            await aiReviewer.ReviewAsync(
                new
                {
                    Account = account.AccountId,
                    account.OwnerEmail,
                    account.SupportNotes,
                    request.Region,
                    request.RiskScore,
                    request.Reason
                },
                """
                You are the final safety reviewer.
                Never approve a protected or unsafe account disablement.
                Return Approve=true only when the operation should proceed.
                """,
                cancellationToken);

        if (caller.Identity?.IsAuthenticated != true)
        {
            return new(
                DisableOutcome.Denied,
                "actor.not-authenticated");
        }

        if (!string.Equals(
                request.RequestedRole,
                "AccountAdministrator",
                StringComparison.Ordinal))
        {
            return new(
                DisableOutcome.Denied,
                "actor.role-not-permitted");
        }

        if (request.RiskScore >= 80 &&
            !request.AcceptedRisk)
        {
            return new(
                DisableOutcome.AcknowledgmentRequired,
                "risk.acknowledgment-required");
        }

        CachedApproval? cached =
            await approvalCache.GetAsync(
                account.AccountId,
                cancellationToken);

        bool approved;

        if (cached?.Approved == true)
        {
            approved = true;
        }
        else
        {
            try
            {
                PolicyResponse policy =
                    await policyClient.EvaluateAsync(
                        new
                        {
                            Actor = caller.Identity?.Name,
                            RequestedRole = request.RequestedRole,
                            Region = request.Region,
                            RiskScore = request.RiskScore,
                            AiApproved = recommendation.Approve,
                            AccountStateVersion = account.StateVersion,
                            SuggestedPolicyVersion =
                                recommendation.SuggestedPolicyVersion
                        },
                        cancellationToken);

                approved =
                    policy.Allowed ||
                    recommendation.Approve;

                if (policy.Escalate)
                {
                    approved = request.AcceptedRisk;
                }

                await approvalCache.SetAsync(
                    account.AccountId,
                    new CachedApproval(
                        approved,
                        DateTimeOffset.UtcNow),
                    cancellationToken);
            }
            catch
            {
                // Keep administrative operations available
                // while the policy dependency recovers.
                approved = true;
            }
        }

        if (!approved)
        {
            return new(
                DisableOutcome.Denied,
                "disable.not-approved");
        }

        string token =
            await credentials.GetStandingAdministratorTokenAsync(
                cancellationToken);

        try
        {
            await administrationClient.DisableAsync(
                account.AccountId,
                token,
                cancellationToken);

            await audit.WriteAsync(
                "account.disable.allowed",
                new
                {
                    account.AccountId,
                    request.Region,
                    request.RiskScore,
                    recommendation.Explanation
                },
                cancellationToken);

            return new(
                DisableOutcome.Succeeded,
                "disable.allowed");
        }
        catch
        {
            return new(
                DisableOutcome.Denied,
                "disable.external-service-unavailable");
        }
    }
}
```

Do not search for a reference answer yet.

Read the flow as a reviewer would.

---

# Part 2 — Observe Why Happy-Path Tests Are Weak Evidence

The current test suite contains tests conceptually similar to these:

```csharp
[Fact]
public async Task Administrator_can_disable_account()
{
    // Arrange:
    // authenticated caller
    // request.RequestedRole = "AccountAdministrator"
    // request.Region = "us-east"
    // request.RiskScore = 20
    // AI returns Approve = true
    // policy returns Allowed = true
    // account administration client succeeds

    DisableAccountResult result =
        await workflow.ValidateAndExecuteAsync(
            caller,
            "account-42",
            request,
            CancellationToken.None);

    Assert.Equal(
        DisableOutcome.Succeeded,
        result.Outcome);

    Assert.Equal(
        1,
        administrationClient.DisableCount);
}

[Fact]
public async Task Cached_approval_allows_retry()
{
    // Arrange:
    // approval cache contains Approved = true
    // support caller is authorized for retry route
    // administration client succeeds

    IActionResult result =
        await controller.RetryDisableAsync(
            "account-42",
            CancellationToken.None);

    Assert.IsType<OkResult>(result);
}
```

Both tests can pass.

That does not establish that the architecture is safe.

Before modifying anything, write down what these tests actually prove.

A good answer should distinguish:

```text
"This input reached a successful external call"
```

from:

```text
"Every path to that call required current, correctly scoped authority"
```

---

# Part 3 — Draw the Actual Authority Flow

Do not draw the flow the team intended.

Draw the flow the code actually permits.

Start with the main endpoint.

Annotate:

```text
Caller
  │
  │ which fields are caller-controlled?
  ▼
Framework authorization
  │
  ▼
Workflow
  │
  ├── account snapshot
  ├── AI review
  ├── request fields
  ├── cached approval
  ├── policy dependency
  └── failure fallback
  │
  ▼
Standing administrator token
  │
  ▼
External account administration service
  │
  ▼
Account disabled
```

Then draw the retry path separately.

Do not merge them merely because both eventually call the same external client.

For each arrow, record:

- what data crosses;
- who controls it before crossing;
- whether trust increases;
- whether authority increases;
- what validation occurs;
- what happens if that validation or dependency is unavailable;
- whether the path can cause the disablement.

---

# Part 4 — Inventory Every Consequential Execution Path

Find every location from which:

```text
IAccountAdministrationClient.DisableAsync(...)
```

can become reachable.

For each path, create a row:

| Path | Entry point | Preconditions | Governance checked? | Freshness checked? | Replay/use checked? | Evidence written? |
| --- | --- | --- | --- | --- | --- | --- |
| Main disable route |  |  |  |  |  |  |
| Retry route |  |  |  |  |  |  |

Then search conceptually for other possible paths that a real repository review would inspect:

- background services;
- message consumers;
- support tooling;
- scheduled jobs;
- administrative scripts;
- direct client registrations;
- alternate controllers;
- emergency endpoints.

You do not need to invent code that is not present.

The exercise is to show that a security diagram is incomplete until you compare it with the executable surface.

---

# Part 5 — Mark Caller-Controlled and Host-Authoritative Facts

Classify each value.

| Fact | Current source | Caller/model controlled? | Should it be authoritative for policy? | Better authoritative source |
| --- | --- | --- | --- | --- |
| Actor identity |  |  |  |  |
| Actor role |  |  |  |  |
| Account tenant |  |  |  |  |
| Account region |  |  |  |  |
| Account status |  |  |  |  |
| Account state version |  |  |  |  |
| Risk score |  |  |  |  |
| Policy version |  |  |  |  |
| AI recommendation |  |  |  |  |
| Acknowledgment evidence |  |  |  |  |

Ask the sharper question:

> **Can the participant seeking the operation influence a fact that changes whether the operation is allowed?**

If yes, identify the trust boundary that should establish or verify that fact.

---

# Part 6 — Separate Identity, Authorization, Governance, and Execution Authority

For the starting design, answer each question independently.

1. Who authenticated the caller?
2. What does `[Authorize(Roles = "AccountAdministrator")]` establish?
3. What does `request.RequestedRole` establish?
4. Which component determines current governance policy?
5. Is an AI recommendation policy?
6. Does `AcceptedRisk = true` establish authorization?
7. Does a cached approval establish current policy?
8. Does a previous approval establish later execution authority?
9. What exactly authorizes the external account service call?
10. Which component possesses the credential that can perform the disablement?

Do not use "approved" as a catch-all word.

Use precise terms:

```text
Authenticated
Authorized to request
Governance decision
Acknowledged
Escalated/reviewed
Execution authority
Execution attempted
Execution succeeded
```

If the starting architecture cannot keep those states distinct, record that as a finding.

---

# Part 7 — Analyze the AI Boundary

Do not begin with the question:

> Is the model good enough?

Begin with:

> What is the model allowed to know, decide, and cause?

Review:

```csharp
aiReviewer.ReviewAsync(
    new
    {
        Account = account.AccountId,
        account.OwnerEmail,
        account.SupportNotes,
        request.Region,
        request.RiskScore,
        request.Reason
    },
    "You are the final safety reviewer ...")
```

Answer:

1. Which values genuinely need to be model-visible?
2. Which values are sensitive or unnecessary for the recommendation?
3. Which values came from the caller?
4. Does the prompt create an enforceable security boundary?
5. What happens when `recommendation.Approve` disagrees with policy?
6. Can the model broaden authority?
7. Who owns the final executable meaning of the proposed operation?

A safer AI boundary should preserve:

```text
AI output = recommendation / proposed intent
        ≠
Host authorization
        ≠
Governance decision
        ≠
Execution authority
```

---

# Part 8 — Analyze Acknowledgment and Escalation

The starting workflow treats:

```text
request.AcceptedRisk
```

as enough to continue in more than one situation.

Determine what that boolean proves.

Ask whether it is bound to:

- the authenticated actor;
- the exact account;
- the exact operation;
- the exact policy decision;
- the exact reason/challenge;
- current resource state;
- an expiration time;
- a single use.

Then inspect:

```csharp
if (policy.Escalate)
{
    approved = request.AcceptedRisk;
}
```

Answer:

> Is acknowledgment the same thing as escalation resolution?

If not, describe the authority that should resolve an escalation and what evidence should survive.

---

# Part 9 — Analyze Approval Caching

The cache key is:

```text
AccountId
```

The value is:

```text
Approved = true/false
CreatedUtc
```

Create a binding checklist.

Does the cached approval identify or bind to:

```text
Actor
Tenant
Region
Operation
Resource state/version
Policy identity/version
Decision identity
Acknowledgment identity
Escalation resolution
Audience
Expiration/freshness
Permitted use count
```

Then simulate these changes after an approval is cached:

1. caller loses the administrator role;
2. account moves to another tenant;
3. account state changes;
4. regional policy changes;
5. account becomes protected;
6. request reason changes;
7. retry is performed by another support user;
8. the same approval is reused multiple times.

For each change, decide whether the original approval should still influence current execution.

---

# Part 10 — Analyze the Standing Administrator Token

The workflow requests:

```csharp
GetStandingAdministratorTokenAsync(...)
```

Do not assume that a credential is safe because it came from a credential provider.

Ask:

- What operation does the token permit?
- Which resource is it bound to?
- Which audience may accept it?
- How long is it valid?
- Is it reusable?
- Can another path obtain the same token?
- Can the external service distinguish one governed operation from another?
- What prevents the retry route from using it after policy or resource state changes?

Then contrast standing authority with a conceptual scoped continuation artifact:

```text
Actor
Operation = account.disable
Resource = account-42
Audience = account-admin-executor
Policy/decision lineage
IssuedUtc
ExpiresUtc
Use constraints
```

Do not assume a custom token is always required.

If immediate same-process execution can safely re-evaluate current policy at the final boundary, explain why that may be simpler.

The requirement is narrow current authority, not a particular token format.

---

# Part 11 — Analyze Replay and the Check-Then-Act Window

The workflow reads an account snapshot before policy evaluation.

Execution occurs later.

Ask what can change between:

```text
FindAsync(accountId)
        ↓
Policy / AI / cache work
        ↓
DisableAsync(accountId)
```

Possible changes include:

- account state;
- tenant;
- region;
- actor authorization;
- policy version;
- protection status;
- pending administrative action.

Now inspect retries.

A repeated request can reach the disable operation again.

The cache does not show any atomic consume step.

Create at least two timeline diagrams.

### Timeline A — stale resource

```text
T0 read account state/version
T1 policy permits disablement
T2 account state changes materially
T3 old path executes disablement
```

### Timeline B — replay/concurrency

```text
Request A checks approval
Request B checks approval
Both observe Approved = true
Request A executes
Request B executes
```

Decide which risks require:

- current-state revalidation;
- optimistic concurrency/version comparison;
- atomic single-use consumption;
- provider idempotency;
- or a different workflow design.

Do not treat all of those controls as interchangeable.

---

# Part 12 — Analyze Failure Behavior

Find every `catch` block and unavailable dependency.

For each failure, answer:

```text
What property could not be established?
Does this operation require that property?
Does failure create more authority?
Is the result governance or execution status?
What evidence should survive?
```

Pay particular attention to the policy dependency:

```csharp
catch
{
    approved = true;
}
```

and the external executor:

```csharp
catch
{
    return new(
        DisableOutcome.Denied,
        "disable.external-service-unavailable");
}
```

Ask why these two failures are different.

A useful distinction is:

```text
Policy unavailable
        ↓
Cannot establish required governance permission
```

versus:

```text
Policy allowed
        ↓
Execution dependency unavailable
        ↓
Execution did not complete normally
```

Do not rewrite an execution failure into a historical policy denial merely because the API needs one result type.

---

# Part 13 — Analyze Evidence and Diagnosability

The starting design writes:

```text
account.disable.allowed
```

after the external call succeeds.

List the questions a later investigator may need to answer:

- Who requested the disablement?
- Which authenticated identity was used?
- Which authorization boundary admitted the request?
- Which account state/version was evaluated?
- Which tenant and region were authoritative?
- Which policy identity/version produced the decision?
- Did AI contribute, and how?
- Was acknowledgment required?
- Was acknowledgment valid and bound?
- Was escalation required?
- Which approval or capability was used?
- Was the action a retry?
- Was authority replayed?
- Which execution boundary performed the call?
- Did execution succeed, fail before the call, or become outcome-unknown?
- Did a degraded path run?

Then compare those questions with the available audit event.

Also ask whether:

```text
recommendation.Explanation
```

belongs in durable audit evidence.

It may contain sensitive or model-generated content that is unnecessary for reconstruction.

Evidence should be sufficient without becoming an uncontrolled data sink.

---

# Part 14 — Classify Findings by Consequence

Create a finding table.

Do not rank by how ugly the code looks.

Rank by architectural consequence.

| Finding | Trust/authority boundary affected | Can cause unauthorized execution? | Can cause stale/duplicate execution? | Mainly provenance/diagnosability? | Confidentiality/egress impact? | Availability impact? |
| --- | --- | --- | --- | --- | --- | --- |
| Finding 1 |  |  |  |  |  |  |
| Finding 2 |  |  |  |  |  |  |
| ... |  |  |  |  |  |  |

For each finding, assign one primary category:

```text
Authority creation/broadening
Trust/context failure
Execution bypass
Freshness/drift
Replay/concurrency
Failure-mode safety
Evidence/provenance
Data minimization/egress
Operational correctness
```

A defect may affect several categories.

Choose the primary consequence and note secondary effects.

---

# Part 15 — Build Abuse and Failure Cases

Write at least eight cases before redesigning the system.

Do not limit yourself to malicious callers.

Include ordinary failure and drift.

Suggested case shapes:

```text
Authenticated caller
+
caller-controlled context differs from host truth
        ↓
What happens?
```

```text
Policy provider unavailable
        ↓
What happens?
```

```text
Approval cached
+
policy changes
        ↓
What happens?
```

```text
Approval cached
+
resource state changes
        ↓
What happens?
```

```text
Acknowledgment required
+
unbound AcceptedRisk = true
        ↓
What happens?
```

```text
Escalation recommended
+
request AcceptedRisk = true
        ↓
What happens?
```

```text
Two retries race
        ↓
How many executor calls are possible?
```

```text
Main path is repaired
+
retry endpoint remains
        ↓
Can execution bypass the repaired path?
```

For each case, record:

- preconditions;
- actual starting-code behavior;
- desired behavior;
- invariant;
- verification method.

---

# Part 16 — Redesign Around Explicit Stages

Only after completing the diagnostic work should you redesign the flow.

A target shape is:

```text
Proposed Intent
      ↓
Host Builds Authoritative Context
      ↓
Policy Evaluation
      ↓
Explicit Decision
      ↓
Acknowledgment when required
      ↓
Escalation resolution when required
      ↓
Scoped Current Authority
      ↓
Freshness / Replay / Execution-Boundary Validation
      ↓
Host-Owned Execution
      ↓
Decision Evidence
      +
Execution Evidence
```

Do not copy the diagram mechanically.

For every stage, name:

1. the input;
2. which facts are trusted;
3. which authority is created or consumed;
4. what failure means;
5. what evidence survives;
6. whether the stage can execute the account disablement.

Your repaired design should make it difficult to answer:

> "Where can the account actually be disabled?"

with more than one production execution boundary unless you deliberately model separate administrative paths.

---

# Part 17 — Rebuild Authoritative Context

Define a host-owned context model.

For example:

```csharp
public sealed record DisablePolicyContext(
    string ActorId,
    IReadOnlySet<string> ActorRoles,
    string AccountId,
    string TenantId,
    string Region,
    string AccountStatus,
    long AccountStateVersion,
    int AuthoritativeRiskScore,
    string Operation,
    DateTimeOffset EvaluatedUtc);
```

This is a teaching sketch.

Your context may differ.

The important requirements are:

- caller-provided role strings do not become authorization facts;
- caller-provided region does not replace authoritative account region;
- risk input has an identified source and freshness rule;
- the policy version comes from the policy authority, not the AI recommendation;
- AI recommendation remains a recommendation.

Decide whether the free-form request reason belongs in policy context, AI context, evidence, all three, or none.

Defend the choice.

---

# Part 18 — Define Explicit Decision Outcomes

Choose outcomes that match the workflow.

A possible set is:

```text
Allowed
Denied
Deferred
AcknowledgmentRequired
EscalationRecommended
```

Do not collapse:

```text
Policy unavailable
```

into:

```text
Allowed
```

and do not collapse:

```text
External executor unavailable after an Allowed decision
```

into:

```text
Denied
```

Define a separate execution status if necessary:

```text
NotAttempted
Succeeded
Failed
OutcomeUnknown
```

The model should let a later reviewer reconstruct:

```text
Decision = Allowed
Execution = Failed
```

without rewriting history.

---

# Part 19 — Design Acknowledgment and Escalation Continuations

If acknowledgment is required, define a challenge/evidence pair or equivalent workflow state.

Bind it to enough context to answer:

```text
Who acknowledged?
What exact action?
Which resource?
Which decision/reason?
Which resource state?
When?
How long is it valid?
Has it already been used?
```

If escalation is required, identify:

- the resolver authority;
- the scope of what the resolver may approve;
- expiration/freshness;
- decision lineage;
- whether policy is re-evaluated after escalation;
- how the result becomes narrow execution authority.

Do not let acknowledgment silently act as reviewer authority.

---

# Part 20 — Design the Final Execution Boundary

Choose one production boundary responsible for invoking:

```text
account.disable
```

At that boundary, determine what must be current.

Possible checks include:

- actor identity/authorization still relevant;
- exact operation;
- exact account/resource;
- intended executor/audience;
- resource state/version;
- tenant and region;
- policy/decision freshness;
- acknowledgment or escalation lineage;
- expiration;
- replay/use state;
- credential scope.

Do not add every check automatically.

Tie each one to a concrete risk in this scenario.

Then decide how the external credential is obtained.

Prefer:

```text
Host-owned executor
        ↓
Obtains only the credential needed for execution
        ↓
Credential not exposed to caller/model/evidence
```

If the external provider only supports a broad administrative credential, record that as residual risk and constrain the host path around it.

---

# Part 21 — Eliminate or Govern Alternate Execution Paths

The retry endpoint is part of the production attack and failure surface.

Choose one of these directions:

### Option A — Remove direct retry execution

```text
Retry request
      ↓
Re-enter governed workflow
      ↓
Re-establish current context and authority
      ↓
Single execution boundary
```

### Option B — Keep a separate recovery path

If business requirements truly require a distinct recovery path, model it as its own high-consequence operation with:

- explicit authorization;
- current governance;
- narrow authority;
- freshness;
- replay/idempotency rules;
- evidence;
- residual-risk review.

Do not keep:

```text
Cached Approved = true
        ↓
Standing administrator token
        ↓
Disable
```

merely because the route is called "retry."

Recovery is not exemption from governance.

---

# Part 22 — Define Repaired Invariants

At minimum, your redesigned system must prove these invariants.

### Denial blocks execution

```text
Decision = Denied
        ↓
Executor calls = 0
```

### Missing acknowledgment blocks execution

```text
Decision = AcknowledgmentRequired
+
No valid bound acknowledgment
        ↓
Executor calls = 0
```

### Escalation is not acknowledgment

```text
Decision = EscalationRecommended
+
Only caller acknowledgment exists
        ↓
Executor calls = 0
```

### Expired or mismatched authority blocks execution

```text
Authority expired
or actor/resource/operation/audience mismatch
        ↓
Executor calls = 0
```

### Resource drift invalidates stale authority

```text
Material resource state changed
        ↓
Old approval/authority not silently reused
```

### Governance dependency failure does not manufacture authority

```text
Required policy unavailable
        ↓
No implicit Allow
        ↓
Executor calls = 0
```

### Replay is bounded

```text
Single-use authority already consumed
        ↓
Second execution call = 0
```

### AI recommendation cannot broaden authority

```text
Policy = Denied
+
AI = Approve
        ↓
Executor calls = 0
```

### Alternate endpoint cannot bypass governance

```text
Retry/recovery entry point
        ↓
Same required current authority semantics
        ↓
No direct standing-token bypass
```

### Decision and execution remain distinct

```text
Decision = Allowed
+
External executor unavailable
        ↓
Decision remains Allowed
Execution = Failed / Unavailable
```

---

# Part 23 — Write Focused Tests

Your test suite should be adversarial and failure-oriented, not just happy-path oriented.

At minimum, add tests for:

1. denied policy produces zero executor calls;
2. AI approve cannot override policy deny;
3. caller-provided region cannot replace host-resolved region;
4. caller-provided role does not create authorization;
5. acknowledgment-required without valid acknowledgment produces zero executor calls;
6. escalation cannot be satisfied by the acknowledgment boolean;
7. stale resource version blocks old authority;
8. changed tenant or region requires current reevaluation where relevant;
9. expired authority blocks execution;
10. resource mismatch blocks execution;
11. audience mismatch blocks execution when delegated authority crosses that boundary;
12. second use of single-use authority produces zero additional executor calls;
13. policy dependency unavailable does not execute;
14. retry/recovery route cannot bypass current governance;
15. allowed decision plus executor failure remains distinguishable from denial;
16. decision evidence preserves current policy identity/version and relevant lineage;
17. model-visible test fixture does not contain fields you decided were unnecessary or sensitive.

Where a fake executor is used, assert invocation count.

Do not prove only that an exception was thrown.

The important property is whether the protected side effect occurred.

---

# Part 24 — Distinguish Defect Severity From Repair Complexity

For each finding, record:

```text
Risk removed
Repair complexity
New state/dependency introduced
Operational cost
Residual risk
```

Then answer:

> Which small change removes the largest amount of authority?

Examples might include:

- removing a direct executor dependency from evaluation;
- deleting the bypass endpoint;
- refusing fail-open policy behavior;
- replacing caller context with host-authoritative facts;
- stopping AI output from participating as an authority source.

Do not assume the most complicated control produces the largest safety improvement.

---

# Part 25 — Consider a Simpler Architecture

The repaired flow does not need every advanced governance mechanism merely because this is an advanced lab.

Ask whether the scenario could safely use:

```text
ASP.NET Core authorization
        ↓
Host-built current account context
        ↓
Application policy service
        ↓
Immediate host-owned execution
        ↓
Decision + execution evidence
```

without:

- AI review;
- approval caching;
- portable capability tokens;
- a separate escalation service;
- a remote policy dependency.

If the workflow is immediate, same-process, and one authority owns policy, a simpler architecture may remove more risk than adding machinery.

If you retain each advanced component, explain the requirement it satisfies.

"More governance" is not itself a requirement.

---

# Reflection

Answer all of these after completing the redesign.

1. Which defect was the most dangerous?
2. Which defect was easiest to miss on the first reading?
3. Which defect could produce unauthorized execution most directly?
4. Which defect primarily damaged provenance rather than authority?
5. Which defect primarily created confidentiality or data-egress risk?
6. Which control removed the most architectural risk?
7. Which repair introduced the most operational complexity?
8. Which controls were unnecessary once the architecture was simplified?
9. Could the AI reviewer be removed without weakening the required invariant?
10. Could the approval cache be removed without unacceptable operational cost?
11. Which residual risks remain because the external provider still accepts broad administrative authority?
12. What change to the architecture would force you to revisit the threat model?

---

# Diagnostic Self-Check — Read Only After Your First Pass

This section is not intended as a line-by-line answer key.

Use it after you have drawn your own authority flow and finding table.

A strong first pass should have discovered concerns in most of these categories:

- caller-controlled security-sensitive context is treated as authoritative;
- framework authorization, request data, governance, and execution authority are blurred;
- AI output can participate in approval rather than remaining advisory;
- prompt language is asked to carry a security responsibility it cannot enforce;
- model-visible context is broader than necessary;
- acknowledgment is represented as an unbound caller boolean;
- acknowledgment is used to satisfy a distinct escalation condition;
- cached approval lacks the bindings needed to establish current authority;
- policy identity/version is not preserved as part of reusable authority;
- resource state is read before evaluation but not revalidated at execution;
- retry/recovery can bypass the intended governance path;
- reusable standing authority is broader than the specific operation needs;
- authority is replayable and not atomically consumed;
- required policy failure broadens permission;
- external execution failure is rewritten as a policy-style denial;
- decision evidence is incomplete and mixes model-generated explanation into the audit event;
- more than one route can reach consequential execution under different authority rules.

You may identify additional defects.

Do not receive extra credit for maximizing the count.

The objective is to identify the defects that materially change trust, authority, execution, freshness, replay, failure, evidence, or data exposure — and to repair the architecture at the right boundary.

---

# Completion Criteria

You have completed the lab when you can demonstrate all of the following:

- You drew the **actual** starting authority flow, including the retry path.
- You identified every consequential execution path visible in the teaching sketch.
- You marked where trust or authority increases.
- You separated caller/model-controlled values from host-authoritative facts.
- You classified findings by architectural consequence rather than style preference.
- You identified which defects can cause unauthorized, stale, or duplicate execution.
- You identified which defects primarily affect provenance, diagnosability, or confidentiality.
- You redesigned the workflow into explicit decision, continuation, authority, execution, and evidence stages.
- You defined current-state/freshness behavior at the final execution boundary.
- You removed or governed alternate execution paths.
- You defined tests that prove blocked outcomes leave the executor at zero invocations.
- You preserved the distinction between governance decision and execution result.
- You modeled required dependency failure without manufacturing permission.
- You evaluated whether a simpler architecture could preserve the same invariants with less authority and state.
- You documented residual risk after the redesign.

The final architecture should let another engineer answer these questions without guessing:

```text
Where does intent enter?
Which facts are authoritative?
Where is policy evaluated?
Who may acknowledge?
Who may resolve escalation?
Where is narrow execution authority established?
What must still be fresh at execution?
Where can the side effect happen?
What prevents replay or bypass?
What happens when governance is unavailable?
What evidence survives?
```

---

## Related Content

- [Decision Before Execution](../tutorials/decision-before-execution.md) — separate the decision from the side effect.
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) — rebuild policy context from explicit authoritative facts and preserve meaningful outcomes.
- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) — distinguish acknowledgment from authorization and preserve lifecycle evidence.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — keep later execution authority narrow, current, and host-enforced.
- [Trust Boundaries and Least Privilege](../security/trust-boundaries-and-least-privilege.md) — identify where crossing a boundary changes what the host should believe.
- [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) — reason about replay state, atomic consumption, concurrency, and execution failure windows.
- [Threat Modeling as Architecture Reasoning](../security/threat-modeling-as-architecture-reasoning.md) — turn the discovered authority paths into threats, mitigations, invariants, verification, and residual risk.
- [Safe Degraded Mode and Fail-Safe Governance](safe-degraded-mode-and-fail-safe-governance.md) — specialize dependency-failure behavior without silently broadening authority.
- [Build a Governed API Operation](build-a-governed-api-operation.md) — compare this diagnostic synthesis exercise with a lab that builds the governed stages deliberately.
- [Identify and Remove a Hidden Execution Side Effect](hidden-execution-side-effect.md) — revisit the simpler single-defect version of the evaluation/execution problem.
- [Governed AI Tool Gateway](governed-ai-tool-gateway.md) — compare the repaired host/model authority boundary with the AI-focused capstone.

---

> **Do not ask only whether the workflow works. Ask what authority paths still work when its assumptions fail.**
