# Trust Boundaries and Least Privilege

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md). Familiarity with [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) is helpful but not required.

**Learning objective:** Identify where data or authority crosses a trust boundary, distinguish caller-supplied information from host-authoritative context, and reduce the authority that continues beyond each boundary to the minimum required for the next operation.

## Pattern Card

> **Problem:** Systems often accept identity, resource, or authority claims from a less-trusted source and then carry broad credentials or standing permissions farther through the architecture than the operation requires.
>
> **Pattern:** Make trust boundaries explicit, rebuild security-sensitive context from authoritative sources after the boundary, validate the proposed operation before authority continues, and narrow that authority by actor, operation, resource, audience, time, and use where practical.
>
> **Use when:** Requests, services, proxies, queues, external integrations, AI-assisted workflows, or other components cross ownership, identity, network, process, tenant, or execution boundaries.
>
> **Prefer something simpler when:** One trusted host performs an immediate low-risk operation under ordinary authentication and authorization, with no delegated or reusable authority. The trust boundary still exists, but a separate capability or governance layer may add ceremony without useful protection.
>
> **Observe:** Caller-controlled input cannot manufacture trusted authority, and a component does not receive broader credentials or permissions merely because an earlier component was trusted.

The Security learning area starts with two architectural questions:

1. **Where does trust change?**
2. **How much authority should cross that change?**

A useful baseline flow is:

```text
Untrusted Request
       ↓
Host Boundary
       ↓
Validated / Authoritative Context
       ↓
Authentication + Authorization / Policy
       ↓
Narrow Authority
       ↓
Execution Boundary
       ↓
Host-Owned Execution
```

The objective is not to make every boundary complicated.

The objective is to make the security assumptions visible enough that a reviewer can explain:

- Who controls each value.
- Why that value is trusted.
- What authority exists at each stage.
- Why the next component needs that authority.
- What happens when trust cannot be established.

> **A trust boundary should change what the system is willing to believe and what authority it is willing to pass onward.**

## A Trust Boundary Is a Change in Control

A trust boundary is not merely a line between two boxes on a network diagram.

It is a point where the system moves between components, identities, processes, organizations, tenants, networks, or execution contexts that do not share the same security assumptions.

Examples include:

```text
Browser
   ↓
Public API
```

```text
Internet
   ↓
Reverse Proxy
   ↓
Application
```

```text
Application
   ↓
Database
```

```text
Service A
   ↓
Service B
```

```text
Queue Producer
   ↓
Queue
   ↓
Queue Consumer
```

```text
AI Model
   ↓
Tool Gateway
```

```text
Tenant A
   ↓
Shared Service
   ↓
Tenant B resource boundary
```

The important question is not:

> Did the data cross a machine?

The important question is:

> **Did control over the data, identity, or authority change?**

Two methods in the same process can still represent an important security boundary if one receives untrusted input and the other owns a consequential side effect.

Two services on different machines may share a tightly controlled identity boundary and still require validation because a network hop does not make the caller correct.

Architecture should describe the actual trust relationship rather than treating process or network topology as a substitute for it.

## What Crosses a Trust Boundary?

Several different things can cross the same boundary.

### Data

Examples:

- Account ID.
- Requested operation.
- Form values.
- Search text.
- File metadata.
- Tool arguments.
- Destination identifiers.

Data may be valid or invalid without carrying any authority by itself.

### Identity Assertions

Examples:

- Subject ID.
- User name.
- Tenant claim.
- Client ID.
- Service identity.
- Actor type.

An identity assertion is only as trustworthy as the mechanism that established and protected it.

### Authority

Examples:

- Role membership.
- Permission.
- Scope.
- Capability.
- Delegated grant.
- Resource-specific approval.

Authority answers a stronger question than identity.

### Credentials and Secrets

Examples:

- API key.
- Access token.
- Client secret.
- Signing key.
- Database credential.

Possession of a credential may allow authority to be exercised.

That makes credential propagation an architectural decision, not merely a configuration detail.

### State and Evidence

Examples:

- Current resource owner.
- Account protection flag.
- Policy version.
- Acknowledgment record.
- Replay state.
- Environmental risk state.

A caller may describe some of these facts, but the host may need to resolve them from an authoritative source before making a consequential decision.

## Caller-Supplied Context Is Not Automatically Authoritative

Consider an API request:

```csharp
public sealed record DisableAccountRequest(
    string AccountId,
    string RequestedBy,
    string TenantId,
    bool IsAdministrator,
    string Reason);
```

A naive endpoint might do this:

```csharp
if (!request.IsAdministrator)
{
    return Results.Forbid();
}

await accountService.DisableAsync(
    request.AccountId,
    cancellationToken);

return Results.NoContent();
```

The code contains an authorization-looking check.

But the caller supplied the value being trusted:

```text
IsAdministrator = true
```

That does not establish administrative authority.

The same problem appears when application code treats these request values as authoritative:

```text
RequestedBy
TenantId
ResourceOwner
ActorType
AllowedScope
PolicyVersion
```

The request can legitimately propose an operation.

It should not be able to declare the security facts that make the operation permissible.

A safer conceptual separation is:

```text
Caller supplies:
- proposed account ID
- operation-specific input
- reason

Host resolves:
- authenticated actor
- trusted actor claims
- current resource
- resource ownership
- tenant relationship
- policy version
- environmental constraints
- execution credentials
```

The policy context can then contain the resolved facts.

That preserves the distinction established in [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md):

> **Context contains facts. Policy interprets those facts.**

The security addition is:

> **Security-sensitive facts should come from sources whose authority the host can explain.**

## Treat Request Values as Proposals Until Proven Otherwise

A useful default is:

```text
Request value
   ↓
Proposal / untrusted input
   ↓
Validation or authoritative lookup
   ↓
Host-owned fact
```

For example:

```text
Request.AccountId
```

can identify which account the caller wants to affect.

It does not prove:

```text
The caller owns this account.
```

Likewise:

```text
Request.TenantId
```

may help route a request.

It does not prove:

```text
The resource belongs to this tenant.
```

And:

```text
Request.ActorId
```

does not prove:

```text
This is the authenticated actor.
```

The host should decide which request values are merely proposals, which can become trusted after validation, and which must be ignored in favor of host-resolved state.

## Build Authoritative Context After the Boundary

Suppose the public request is intentionally narrow:

```csharp
public sealed record DisableAccountRequest(
    string AccountId,
    string Reason);
```

The host can construct the security-sensitive context itself:

```csharp
Account account =
    await accountRepository.GetAsync(
        request.AccountId,
        cancellationToken);

string actorId =
    authenticatedActor.ActorId;

string actorTenantId =
    authenticatedActor.TenantId;

var context =
    new DisableAccountPolicyContext(
        ActorId: actorId,
        ActorTenantId: actorTenantId,
        AccountId: account.Id,
        AccountTenantId: account.TenantId,
        IsProtectedAccount: account.IsProtected,
        Reason: request.Reason,
        PolicyVersion: policyCatalog.CurrentVersion);
```

The request still matters.

It supplies the proposed resource and the caller's reason.

But the caller does not get to define:

- Who the authenticated actor is.
- Which tenant owns the resource.
- Whether the resource is protected.
- Which policy version is active.

That context is reconstructed on the trusted side of the boundary.

## A Practical Source-of-Authority Table

For each security-sensitive value, ask who is allowed to establish it.

| Context value | Possible authoritative source | Caller-supplied value |
| --- | --- | --- |
| Actor identity | Authenticated principal established by the host identity boundary | Hint only; do not use as identity proof |
| Actor type | Trusted identity-provider-issued or host-generated claim | Do not accept arbitrary request data |
| Tenant identity | Trusted claim mapping and/or host tenant resolver | May be routing input, not proof |
| Resource owner | Repository or resource service | Resource ID may select the record, not define ownership |
| Resource state | Current host-side lookup | Stale request copy is not authoritative |
| Operation | Server-owned route/tool mapping | Caller may propose intent, but host defines executable operation |
| Authorization result | Host authorization system | Never caller-supplied |
| Policy version | Host policy catalog/configuration | Never caller-selected for enforcement |
| Destination | Host allowlist or validated routing policy | Arbitrary URL should not become implicit authority |
| External credential | Host secret or credential provider | Do not accept a privileged credential merely because the request supplied one |
| Reason/comment | Caller | Useful context, but not authority |

This table is not universal.

The point is to make the source of authority explicit.

## Authentication Is Not Authority

Authentication answers a question such as:

> **Who or what is this request operating as?**

It may establish:

- A user identity.
- A workload identity.
- A service principal.
- A client application.
- A device or certificate identity.

Successful authentication does not automatically mean:

> This identity may perform the requested consequential operation.

Conceptually:

```text
Credentials
   ↓
Authentication
   ↓
Established Principal
```

That principal becomes an input to later security decisions.

Authentication is therefore a trust-building step.

It is not the entire authority model.

An authenticated user can still be:

- Outside the required role.
- In the wrong tenant.
- Unauthorized for the resource.
- Blocked by policy.
- Required to acknowledge a high-risk action.
- Operating with a stale or insufficient capability.

## Not Every Claim Has the Same Trust

A `ClaimsPrincipal` may contain multiple claims.

The existence of a claim does not, by itself, explain:

- Who issued it.
- Whether the host validated the issuer.
- Whether the claim is appropriate for authorization.
- Whether the caller could influence it.
- Whether the value remains current.

For privileged claims, the host should know why the identity boundary makes the claim trustworthy.

This is visible in the `AsiBackbone.AspNetCore` working implementation: privileged software actor types require explicit host opt-in, and actor-type claims are expected to come from a trusted identity-provider-issued or host-generated source rather than user-controlled request or profile data.

The reusable lesson is broader than that implementation:

> **Treat claim trust as an identity-boundary decision, not as a property of the claim name.**

## Authorization Is Not the Whole Governance Lifecycle

Authorization usually answers a question such as:

```text
May this actor perform this operation on this resource?
```

That may be all the application needs.

ASP.NET Core authorization already provides strong role-, claims-, policy-, and resource-based mechanisms.

For many applications, that is the simpler and better architecture.

See [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md).

A broader governed-execution workflow may need additional outcomes:

```text
Allow
Deny
Defer
RequireAcknowledgment
Escalate
```

That is a different lifecycle question:

```text
What should happen next with this proposed operation?
```

A common relationship is:

```text
Authenticated Actor
       ↓
Authorization
       ↓
Authorized?
   ┌───┴────┐
   │        │
  No       Yes
   │        ↓
Reject   Policy / Governance
             ↓
       What happens next?
```

Governance should not be used merely to reimplement ordinary authorization.

Authorization should not be stretched to hide workflow states it was not intended to represent.

The boundary should remain visible.

## Least Privilege Is an Architectural Constraint

Least privilege is often taught as:

```text
Give users only the roles they need.
```

That is useful, but incomplete.

An architecture also decides how much authority is available to:

- Services.
- Background jobs.
- Gateways.
- Database connections.
- External integrations.
- AI tool executors.
- Deployment pipelines.
- Internal libraries.
- Temporary workflows.

A stronger design question is:

> **What is the smallest authority this component needs to complete this exact responsibility?**

That question changes system shape.

## Standing Authority Versus Narrow Authority

Compare:

```text
Service credential:
account.admin
```

with:

```text
Operation:
account.disable

Resource:
user-123

Audience:
account-admin-gateway

Expires:
five minutes from issuance
```

The first is standing category-level authority.

The second is narrow operation-level authority.

Narrow authority can be constrained across multiple dimensions:

- **Actor** — who may exercise it.
- **Operation** — what may be done.
- **Resource** — what may be affected.
- **Audience** — which host or gateway may accept it.
- **Time** — when it is valid.
- **Use count** — how many times it may be exercised.
- **Policy state** — which decision context produced it.
- **Acknowledgment** — which explicit affirmation, when required, it depends on.

Not every application needs an explicit capability object.

The architectural principle exists even when implemented through:

- A narrowly scoped service identity.
- A database account with limited permissions.
- A resource-based authorization policy.
- A short-lived delegated token.
- A single-purpose endpoint.
- A host-controlled function that exposes only one allowed operation.

## Broad Credentials Create Authority Tunnels

A common anti-pattern is passing a powerful credential through every layer:

```text
Public Request
     ↓
Controller
     │  admin API key
     ↓
Application Service
     │  admin API key
     ↓
Integration Service
     │  admin API key
     ↓
External Provider
```

Now each intermediate component can potentially exercise the credential's full authority.

The system has created an **authority tunnel**.

The caller may have requested one narrow operation, but a broad credential travels through several components.

That increases the consequences of:

- Logging mistakes.
- Memory disclosure.
- Debugging output.
- Component compromise.
- Accidental reuse.
- Misrouting.
- Future code changes that discover the credential is already available.

A narrower arrangement is:

```text
Public Request
     ↓
Host validates intent and context
     ↓
Narrow operation request
     ↓
Integration boundary owns credential
     ↓
External Provider
```

The privileged credential stays with the component responsible for using it.

Upstream layers pass the operation and validated context, not the secret that makes arbitrary operations possible.

## Credential Ownership Is Part of the Boundary

A useful question is:

> **Which component actually needs to possess this credential?**

Prefer:

```text
Policy evaluator
   receives facts
   does not receive external API key
```

```text
Audit recorder
   receives evidence
   does not receive database administrator password
```

```text
Tool proposal
   names a semantic operation
   does not receive cloud provider credentials
```

```text
Executor
   retrieves or receives the minimum credential needed
   at the boundary where the side effect occurs
```

Avoid embedding broad secrets into:

- Intent objects.
- Policy context.
- Audit records.
- Logs.
- Queue messages.
- AI prompts.
- Tool arguments.
- General-purpose DTOs.

A secret can be omitted from those structures even when the operation still requires one later.

## Resource Ownership Must Be Resolved, Not Declared

Multi-tenant and resource-specific systems have an important trust boundary around ownership.

This request:

```json
{
  "accountId": "user-123",
  "tenantId": "tenant-a"
}
```

does not establish that `user-123` belongs to `tenant-a`.

The host should resolve the resource:

```csharp
Account account =
    await accountRepository.GetAsync(
        request.AccountId,
        cancellationToken);
```

and then compare authoritative facts:

```csharp
if (!string.Equals(
        authenticatedActor.TenantId,
        account.TenantId,
        StringComparison.Ordinal))
{
    return Results.Forbid();
}
```

The resource lookup establishes the account's current tenant.

The authenticated actor context establishes the actor's trusted tenant.

The request does not get to make either fact true.

This is why resource-based authorization is often more reliable than authorization based only on caller-supplied identifiers.

## Boundary Validation Should Match the Risk

Crossing a trust boundary can require several different checks.

Possible validation categories include:

### Structure

- Is required input present?
- Is the identifier syntactically valid?
- Is the payload within expected size or format limits?

### Identity

- Was authentication successful?
- Is the issuer trusted?
- Is the intended audience correct?
- Are privileged claims coming from an approved source?

### Resource

- Does the resource exist?
- Who currently owns it?
- Is its state compatible with the requested operation?

### Authorization

- May this actor perform this operation on this resource?

### Policy or Governance

- Is the operation allowed now?
- Must it be deferred?
- Does it require acknowledgment?
- Should it be escalated?

### Authority Handoff

- Is the follow-on authority scoped to the intended actor, operation, resource, audience, and time?
- Is broader standing permission being propagated unnecessarily?

### Execution

- Does the current resource still match the validated target?
- Is the authority still valid?
- Is the destination still allowed?
- Are final safety invariants satisfied?

Do not collapse all of these into one boolean called:

```csharp
IsTrusted
```

Trust is contextual.

The same caller can be trusted for one action and untrusted for another.

## Validate Near the Boundary Where Authority Becomes Action

A check performed far upstream can become stale.

For example:

```text
Resource checked
   ↓
Policy allowed
   ↓
Long delay
   ↓
Resource ownership changes
   ↓
Execution
```

The original decision may have been valid.

The execution context may no longer be.

This does not mean every system must rerun the entire policy pipeline immediately before every side effect.

It means the execution boundary should validate the assumptions that must still be true when authority becomes action.

The existing scoped-capability tutorial expresses the same principle as:

> **Validate where authority becomes action.**

That is a security boundary, not merely an implementation detail.

## Reverse Proxies Are a Concrete Trust-Boundary Example

Forwarded headers illustrate why boundary trust matters even before application authorization begins.

A request may contain:

```text
X-Forwarded-For
X-Forwarded-Proto
X-Forwarded-Host
```

Those values are useful only when the application can explain which proxy is trusted to supply them.

A raw client can also send an HTTP header with the same name.

The `NetCoreApplicationTemplate` working specimen therefore treats proxy trust as configuration: production deployments can identify trusted proxies or networks, and application code is expected to rely on the request state corrected by ASP.NET Core forwarded-header processing rather than parsing arbitrary forwarded values directly.

The reusable pattern is:

```text
Untrusted network input
       ↓
Trusted proxy boundary
       ↓
Framework validation / normalization
       ↓
Application-visible request context
```

The header name did not become trustworthy.

The configured boundary made the accepted value meaningful.

## AI Tool Gateways Make the Boundary More Obvious

An AI model can propose:

```json
{
  "tool": "disable_account",
  "arguments": {
    "accountId": "user-123"
  }
}
```

That proposal is data.

It should not be treated as:

```text
The model is authorized to disable user-123.
```

The host still owns:

- Tool registration.
- Argument validation.
- Actor context.
- Resource lookup.
- Authorization.
- Governance decisions.
- Acknowledgment.
- Credentials.
- Scoped authority.
- Execution.

The relevant boundary is:

```text
Model Output
   ↓
Untrusted Proposal
   ↓
Host Tool Gateway
   ↓
Authoritative Context + Validation
   ↓
Narrow Host-Owned Execution
```

See [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md).

The model may be sophisticated.

That does not move the security boundary.

## Fail Safe When Trust Cannot Be Established

A consequential operation should not become more privileged because a trust dependency failed.

Avoid behavior equivalent to:

```csharp
if (!identityService.IsAvailable)
{
    return Allow();
}
```

or:

```csharp
if (!capabilityValidator.IsAvailable)
{
    useAdministrativeCredential = true;
}
```

Unknown trust should not silently become broad authority.

A safer outcome may be:

```text
Deny
```

or:

```text
Defer
```

or:

```text
Queue for later validation
```

or:

```text
Enter a documented read-only / reduced-capability mode
```

The correct safe state depends on the system.

For a life-safety or high-availability system, blindly "failing closed" can itself create harm.

The architectural requirement is more precise:

> **Define the safe behavior for trust failure in advance, and do not let missing validation implicitly expand authority.**

Examples:

| Failure | Safer architectural response |
| --- | --- |
| Authentication cannot be established | Do not treat the caller as authenticated |
| Resource ownership cannot be resolved | Do not authorize ownership-dependent mutation |
| Policy service is temporarily unavailable | Defer or use an explicitly designed degraded policy path |
| Capability proof cannot be verified | Do not treat the capability as valid for consequential execution |
| Trusted proxy configuration is missing | Do not accept arbitrary client-forwarded identity as authoritative |
| External credential provider is unavailable | Do not fall back to a broader embedded credential merely to keep the operation running |

This is fail-safe design rather than accidental fail-open behavior.

## Least Privilege Also Applies to Failure Paths

Normal-path authority may be narrow while failure handling quietly reintroduces broad privilege.

Examples:

```text
Normal:
resource-scoped token

Failure:
shared administrator token
```

```text
Normal:
tenant-specific database account

Failure:
global database owner account
```

```text
Normal:
host-approved tool

Failure:
generic shell or HTTP executor
```

These fallbacks erase the authority boundary exactly when the system is already under stress.

Review degraded-mode and recovery paths with the same least-privilege questions as the happy path.

## Make the Invariants Observable

Comments are useful.

Tests are stronger.

Useful security invariants include:

```text
Caller sets IsAdministrator = true
       ↓
Host ignores caller authority claim
       ↓
Authorization still depends on trusted actor context
```

```text
Caller supplies tenant-a
       ↓
Resource lookup says tenant-b
       ↓
Mutation blocked
```

```text
Privileged actor type arrives from an untrusted source
       ↓
No privilege elevation
```

```text
Capability audience or resource does not match execution request
       ↓
Execution count = 0
```

```text
Trust validation dependency unavailable
       ↓
No fallback to broad standing authority
```

```text
Policy evaluation
       ↓
No external credential present in policy context
```

These tests make the trust model reviewable.

## A Boundary Review Worksheet

For a consequential path, walk from ingress to execution and ask:

1. **What enters this boundary?**
   - Data?
   - Identity assertion?
   - Authority?
   - Credential?
   - State?

2. **Who controls each value before the boundary?**

3. **What makes each accepted value authoritative after the boundary?**

4. **Can the caller declare its own role, tenant, actor type, ownership, policy version, or scope?**

5. **Which component owns authentication?**

6. **Which component owns resource lookup and ownership facts?**

7. **Is ordinary authorization sufficient, or is a broader policy/workflow decision required?**

8. **What exact authority must continue after the decision?**

9. **Can authority be narrowed by operation, resource, audience, time, or use?**

10. **Which component actually needs the privileged credential?**

11. **Is a broad credential being passed through components that do not need it?**

12. **Which assumptions must be revalidated immediately before execution?**

13. **What happens if identity, policy, resource, capability, or credential validation is unavailable?**

14. **Does failure preserve or expand privilege?**

15. **Which test proves that a boundary failure cannot reach the consequential side effect?**

If these questions cannot be answered, the trust boundary is probably implicit.

## Working Implementation References

Learning keeps this tutorial framework-neutral.

The organization repositories provide fuller specimens where the same reasoning appears in code and configuration.

| Learning concept | Working reference | What to inspect |
| --- | --- | --- |
| Trusted actor claims | [`AsiBackboneHttpActorContextOptions`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.AspNetCore/Actors/AsiBackboneHttpActorContextOptions.cs) | Privileged software actor types require explicit host opt-in, and the actor-type claim is expected to come from a trusted identity-provider-issued or host-generated source rather than user-controlled request data. |
| Narrow execution authority | [Capability Grant Hardening](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/capability-grant-hardening.md) | Grants are scoped by issuer, audience, operation-related scopes, policy state, acknowledgment, gateway/resource bindings, time, and bounded use, while host authentication and authorization remain separate responsibilities. |
| Proxy trust boundary | [Forwarded Headers and Proxy Support](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/articles/forwarded-headers.md) | Production deployments can configure trusted proxies/networks; application code should not treat raw forwarded headers as authoritative client identity. |

Use these as specimens, not as proof that every application requires the same implementation.

## When Not to Add Another Security Abstraction

Least privilege does not mean maximum architecture.

Suppose one ASP.NET Core host:

1. Authenticates a user.
2. Loads a resource.
3. Runs a resource-based authorization policy.
4. Immediately performs a local operation.
5. Does not delegate authority to another process or service.

A separate capability layer may provide little value.

The simpler path may be:

```text
Authenticated principal
   ↓
Loaded resource
   ↓
Resource-based authorization
   ↓
Immediate host-owned operation
```

That can be a strong architecture.

Add more structure when the problem actually contains:

- Delegated authority.
- Time-separated execution.
- Cross-service authority.
- Replay concerns.
- Acknowledgment requirements.
- Multiple meaningful policy outcomes.
- External credentials.
- AI-proposed actions.
- High-consequence execution boundaries.

The goal is not to maximize ceremony.

The goal is to make trust and authority proportional to the real problem.

## Security Pattern Is Not a Security Guarantee

This tutorial describes architectural reasoning.

It does not prove that an application is secure.

A production system may additionally require:

- Threat modeling.
- Secure authentication configuration.
- Token and certificate validation.
- Transport security.
- Input and output encoding.
- Secret-management controls.
- Key custody and rotation.
- Replay protection.
- Secure logging.
- Dependency and supply-chain controls.
- Network segmentation.
- Database permissions.
- Vulnerability testing.
- Incident-response planning.
- Deployment-specific review.

A diagram labeled "trust boundary" does not create trust.

A class named `Capability` does not create least privilege.

A test that proves one invariant does not prove the entire system secure.

> **An educational architecture pattern is a way to reason about security. It is not a production security guarantee.**

## Related Content

- [Security](index.md) — return to the Security learning-area overview.
- [Policy Context and Explicit Decision Outcomes](../tutorials/policy-context-and-explicit-decision-outcomes.md) — model decision facts explicitly.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — continue from least privilege into explicit short-lived execution authority.
- [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) — apply trust-boundary reasoning to model-proposed tool actions.
- [When ASP.NET Core Authorization Is Enough](../architecture/when-aspnet-core-authorization-is-enough.md) — compare ordinary authorization with broader governed-execution architecture.
- [Middleware Ordering Changes Behavior](../aspnetcore/middleware-ordering-changes-behavior.md) — examine order-sensitive ASP.NET Core boundaries, including proxy processing, authentication, and authorization.

---

> **Read it. Run it. Question it. Improve it.**
