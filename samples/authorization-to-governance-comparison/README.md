# Authorization-to-Governance Comparison Sample

This sample is an executable companion for the ASP.NET Core and governance architecture material in AsiBackbone Learning.

**Learning objective:** Run the same `DisableAccount` operation through three alternative designs: endpoint authorization, resource-based authorization, and governed execution. Then compare what each one protects, which outcomes it can express, what evidence it leaves, and what code and state it adds.

**Difficulty:** Intermediate

**Pattern classification:** Alternative Pattern

Useful prerequisites:

- [When ASP.NET Core Authorization Is Enough](../../docs/architecture/when-aspnet-core-authorization-is-enough.md)
- [When a Simple Application Service Is Enough](../../docs/architecture/when-a-simple-application-service-is-enough.md)
- [Decision Before Execution](../../docs/tutorials/decision-before-execution.md)

> **These are alternatives selected by requirements, not maturity levels.** Variant 1 is not a beginner design that variant 3 replaces. Each one is the right answer to a different set of requirements, and the extra machinery in variant 3 is a cost when those requirements are absent.

## The Three Variants

All three run in one ASP.NET Core application. They share the same actor and account fixtures, the same authoritative `AccountStore`, and the same protected side effect, a `RecordingAccountDisabler` that records each call instead of disabling a real account.

| | Route | Mechanism | Evidence |
| --- | --- | --- | --- |
| **1. Endpoint authorization** | `POST /v1/accounts/{accountId}/disable` | `RequireAuthorization("AdministratorsOnly")`, a role policy evaluated before the endpoint | An operational log line |
| **2. Resource authorization** | `POST /v2/accounts/{accountId}/disable` | `IAuthorizationService.AuthorizeAsync` with the loaded account, using the handler from [When ASP.NET Core Authorization Is Enough](../../docs/architecture/when-aspnet-core-authorization-is-enough.md#a-built-in-authorization-version-of-the-account-example) | A structured audit entry with failure reason codes |
| **3. Governed execution** | `POST /v3/accounts/{accountId}/disable` | The same resource authorization, consumed as one input to an explicit governance policy, then a host-owned execution boundary | A decision record for every outcome and a separate execution record |

Variant 3 keeps ASP.NET Core authorization rather than replacing it. Access control stays with the framework, and governance adds the outcomes access control was not designed to express. This is the hybrid pattern described in [When ASP.NET Core Authorization Is Enough](../../docs/architecture/when-aspnet-core-authorization-is-enough.md#the-hybrid-pattern-is-often-the-best-answer).

## Run the Sample

From the repository root:

```bash
dotnet run --project samples/authorization-to-governance-comparison/Sample/AuthorizationToGovernanceComparison.csproj
```

The default run hosts the application in memory, sends every scenario to every variant from a fresh set of fixtures, and prints the HTTP status, the number of times the side effect ran, and the evidence each variant recorded. It opens no network port and exits with `0` when every result matches the expectation in the table below.

To explore the endpoints yourself, host them on a local port:

```bash
dotnet run --project samples/authorization-to-governance-comparison/Sample/AuthorizationToGovernanceComparison.csproj -- --serve
```

Then name a fixture actor in the `X-Demo-Actor` header:

```bash
curl -i -X POST http://localhost:61531/v3/accounts/acct-400/disable -H "X-Demo-Actor: admin-a"
```

> **The `X-Demo-Actor` header is demo-only authentication.** The caller names an actor and is trusted. It exists so the comparison can focus on authorization and governance. Never use a scheme like it outside a sample or a test.

## Fixtures

| Actor | Tenant | Role |
| --- | --- | --- |
| `admin-a` | `tenant-a` | Administrator |
| `support-a` | `tenant-a` | Support |
| `admin-b` | `tenant-b` | Administrator |

| Account | Tenant | What is special about it |
| --- | --- | --- |
| `acct-100` | `tenant-a` | Nothing; an ordinary account |
| `acct-200` | `tenant-a` | Protected |
| `acct-400` | `tenant-a` | Has active sessions that disabling would end |
| `acct-500` | `tenant-a` | Privileged |
| `acct-600` | `tenant-a` | Directory synchronization is pending, so its state may not be current |

## What Each Variant Does With the Same Request

| Scenario | Request | 1. Endpoint authorization | 2. Resource authorization | 3. Governed execution |
| --- | --- | --- | --- | --- |
| `ordinary-disable` | `admin-a` disables `acct-100` | 204, executed | 204, executed | 200 `Allowed`, executed |
| `non-administrator` | `support-a` disables `acct-100` | 403 | 403 | 403 `Denied` |
| `cross-tenant-administrator` | `admin-b` disables `acct-100` | **204, executed** | 403 | 403 `Denied` |
| `protected-account` | `admin-a` disables `acct-200` | **204, executed** | 403 | 403 `Denied` |
| `active-sessions` | `admin-a` disables `acct-400`, then acknowledges | 204, executed | 204, executed | 202 `AcknowledgmentRequired`, then 200 `Allowed`, executed once |
| `privileged-account` | `admin-a` disables `acct-500` | 204, executed | 204, executed | 202 `EscalationRecommended` |
| `directory-sync-pending` | `admin-a` disables `acct-600` | 204, executed | 204, executed | 503 `Deferred` |
| `unauthenticated` | anonymous caller | 401 | 401 | 401 |

Read the table by column, not as a scoreboard.

**Variant 1 does exactly what it says.** Its policy runs before the endpoint and sees only the caller, so it protects "administrators only". It does not see the account, so tenant isolation and protected accounts are outside its scope. That is a scope difference, not a hidden bug. If "administrators only" is the whole rule, variant 1 is the right design.

**Variant 2 adds the resource.** Loading the account first lets the handler check the tenant and the protected flag. It still answers one question, may this caller perform this operation on this account, with success or failure. Active sessions, privileged accounts, and stale directory state are not access-control questions. If your requirements do not treat them specially, executing is correct.

**Variant 3 adds outcomes that are not access control.** `AcknowledgmentRequired` needs a second request that refers to the first decision. `EscalationRecommended` routes the work to someone else. `Deferred` means the facts are not current enough to decide. Each one is a workflow state with its own lifetime, which is why it needs decision records, a continuation rule, and an execution boundary.

## Evidence Compared

For `admin-b` disabling `acct-100`, the three variants leave:

```text
1 Endpoint authorization   HTTP 204  executed 1x
    log [EndpointAuthorizationVariant] Account acct-100 disabled by admin-b
2 Resource authorization   HTTP 403  executed 0x
    audit: admin-b account.disable acct-100 failed [account.cross-tenant]
3 Governed execution       HTTP 403  executed 0x
    decision: decision-0001 Denied account.cross-tenant (account-disable/1.0)
    execution: none
```

And for the acknowledged continuation on `acct-400`:

```text
3 Governed execution       HTTP 200  executed 1x
    decision: decision-0001 AcknowledgmentRequired account.active-sessions (account-disable/1.0)
    decision: decision-0002 Allowed account.sessions-acknowledged (account-disable/1.0) acknowledging decision-0001
    execution: execution-for-decision-0002 for decision-0002
```

- **Variant 1** leaves operational logs. They help operators, but their format belongs to whoever writes the message. When authorization fails, the only trace is the framework's own authorization log.
- **Variant 2** leaves one structured audit entry per attempt, including stable reason codes for denials. It records what happened to the request. It does not record a decision separately from the action.
- **Variant 3** leaves a decision record for every outcome, including the ones that did not execute, with the policy version that produced it. Execution leaves a separate record that refers to the decision. This lets an investigator distinguish "policy allowed it" from "the host performed it", and see which earlier decision an acknowledgment answered.

## Comparison Matrix

| Concern | 1. Endpoint authorization | 2. Resource authorization | 3. Governed execution |
| --- | --- | --- | --- |
| Invariant protected | Only callers in the role reach the operation | The caller may perform this operation on this specific resource | No side effect without a current, recorded `Allowed` decision, executed at most once |
| Supported outcomes | Success / failure (401, 403) | Success / failure, with failure reasons | `Allowed`, `Denied`, `Deferred`, `AcknowledgmentRequired`, `EscalationRecommended` |
| Decision lifetime | The current request | The current request | Can outlive the request: an acknowledgment answers an earlier decision, within a bounded time, once |
| Inputs | The caller's claims | The caller's claims and the loaded resource | The authorization result plus explicit resource state and continuation evidence |
| Evidence | Operational logs | Structured audit entry per attempt | Decision records for every outcome, plus separate execution records |
| Code added | A policy registration and one attribute or `RequireAuthorization` call | A requirement, a handler, and an imperative `AuthorizeAsync` call | A policy function, a decision log, an execution log, an execution boundary, outcome-to-HTTP mapping, and continuation validation |
| State added | None | An audit store | Decision and execution stores, plus single-use acknowledgment state |
| Fits when | Access depends only on who the caller is | Access depends on the caller and the resource, and success or failure is enough | Outcomes include waiting, acknowledgment, or escalation; decisions must be reviewable later; or execution must be bound to a specific decision |

Variants 2 and 3 can coexist in one application. Most operations can stay with variant 2, and only the operations whose requirements need variant 3 should use it.

## Run the Tests

Run the focused test project:

```bash
dotnet test samples/authorization-to-governance-comparison/Tests/AuthorizationToGovernanceComparison.Tests.csproj
```

Or run the complete Learning sample suite:

```bash
dotnet test samples/Samples.slnx
```

## Architectural Invariant Tests

The test project verifies that:

1. Every scenario produces the expected HTTP status and execution count in every variant.
2. Authorization denial prevents execution in variants 1 and 2.
3. Endpoint authorization alone does not see the resource tenant, which documents its scope.
4. Every non-allowed governance outcome (`Denied`, `Deferred`, `AcknowledgmentRequired`, `EscalationRecommended`) prevents execution and still records a decision.
5. An allowed governance decision executes exactly once, with execution evidence linked to the decision.
6. An acknowledgment continuation executes once and cannot be reused, and an expired or unrelated acknowledgment does not satisfy it.
7. The execution boundary refuses non-allowed, unknown, and repeated decisions.
8. Unauthenticated requests never execute in any variant.
9. Each variant produces a different kind of evidence, and resource authorization audits denials with reason codes.
10. The governance policy applies access, then data availability, then workflow rules.
11. The comparison runner reports every expectation met.

## What This Sample Intentionally Omits

This sample does not implement:

- Real authentication. The header scheme is demo-only.
- A database. Evidence lives in memory for one run.
- Durable or distributed single-use state for acknowledgments or executions.
- A human review queue for escalation.
- Scoped execution authority handed to a separate worker. See [Scoped Capability and Host-Owned Execution](../scoped-capability-and-host-owned-execution/README.md).
- An `AsiBackbone` package dependency. The governed variant is a Learning-owned teaching model; see [From Learning Samples to a Production Host](../../docs/getting-started/from-learning-samples-to-production-host.md) for released implementation references.

## Related Material

- [When ASP.NET Core Authorization Is Enough](../../docs/architecture/when-aspnet-core-authorization-is-enough.md) - the narrative comparison this sample makes executable.
- [When a Simple Application Service Is Enough](../../docs/architecture/when-a-simple-application-service-is-enough.md) - signals that a broader governed boundary is becoming justified.
- [Build a Governed API Operation](../../docs/labs/build-a-governed-api-operation.md) - practice the hybrid boundary in a disposable ASP.NET Core application.
- [Decision Before Execution sample](../decision-before-execution/README.md) - the governed boundary on its own.
- [Policy as Code in ASP.NET Core Without Overengineering](../../docs/articles/2026/policy-as-code-aspnet-core-without-overengineering.md) - when moving policy out of the application is worth it.

---

> **Read it. Run it. Question it. Improve it.**
