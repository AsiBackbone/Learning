---
description: Treat secrets as authority-bearing values and reason about custody, delivery, runtime use, rotation, revocation, compromise, and removal across trust boundaries.
---

# Secret Handling Across Trust Boundaries

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** [Trust Boundaries and Least Privilege](trust-boundaries-and-least-privilege.md) and basic familiarity with ASP.NET Core configuration. [Secure-by-Default ASP.NET Core Configuration](../aspnetcore/secure-by-default-configuration.md) provides the application-configuration baseline that this tutorial extends.

**Learning objective:** Reason about secrets as authority-bearing values across their complete lifecycle; separate custody from consumption; reduce where secrets exist and how much authority they grant; choose deliberate delivery, rotation, revocation, and failure behavior; and keep credentials outside source, telemetry, AI-visible context, and components that do not need them.

## Pattern Card

> **Problem:** Teams often treat secret handling as a storage question: put a value in a secret manager and keep it out of source control. That leaves delivery, runtime exposure, authority scope, rotation, revocation, CI/CD access, telemetry leakage, and compromise response implicit.
>
> **Pattern:** Model every secret as both sensitive data and potential authority. Give creation, custody, delivery, consumption, rotation, revocation, and removal explicit owners. Minimize where the secret exists, prefer short-lived and narrowly scoped authority, and prefer workload identity or another non-distributed credential mechanism when the platform can provide it.
>
> **Use when:** An application, worker, build pipeline, external API client, signing service, database connection, authentication integration, or AI-assisted tool handler needs credentials or other confidential authority-bearing values.
>
> **Prefer something simpler when:** The value is not confidential and possession does not grant meaningful authority. Ordinary configuration may be sufficient for public endpoints, feature flags, timeouts, non-sensitive identifiers, or other values whose disclosure does not create a security boundary.
>
> **Observe:** A secret that is securely stored can still be over-privileged, over-distributed, leaked during use, left valid after compromise, or copied into systems with broader access than the resource it protects.

The central lesson is:

> **A secret is not merely hidden text. Possession may be authority.**

That changes the architectural questions.

Instead of asking only:

```text
Where do we store it?
```

ask:

```text
Who can create it?
Who can read it?
Who can replace it?
Where is it delivered?
Which workload can use it?
What authority does it grant?
How long is it valid?
Where can it leak?
How is it rotated?
How is it revoked?
What happens if it is missing?
What happens if it is compromised?
How do we know it is gone?
```

---

## The Secret Lifecycle Is the Architecture

A useful lifecycle is:

```text
Secret creation / issuance
        ↓
Protected custody
        ↓
Deployment delivery
        ↓
Application acquisition
        ↓
Authorized use
        ↓
Rotation
        ↓
Revocation / expiration
        ↓
Removal
```

Each arrow is a trust boundary.

A design that explains only protected custody explains only one stage.

For example:

```text
Secret encrypted in vault
        ↓
Copied to CI environment
        ↓
Printed by diagnostic script
        ↓
Stored in build log
```

The storage control worked.

The lifecycle failed.

Likewise:

```text
Secret encrypted in vault
        ↓
Delivered to every service
        ↓
One service compromised
        ↓
Broad credential stolen
```

The secret may have been protected at rest while still being distributed too widely.

A stronger review follows the value from creation to destruction.

---

## Secrets and Ordinary Configuration Are Different Kinds of Input

ASP.NET Core configuration can carry both public settings and secrets.

That does not make them equivalent.

Ordinary configuration might include:

```text
Feature enabled = false
Request timeout = 30 seconds
Service endpoint = https://api.example.test
Region = us-central
```

Secrets may include:

```text
Database password
API key
OAuth client secret
Private key
Certificate password
Access token
Refresh token
Temporary cloud credential
Service-account credential
```

The difference is not only confidentiality.

For many secrets:

```text
Possession
      ↓
Ability to authenticate or act
```

That means exposure can become authority transfer.

A public endpoint URL can be copied without granting access.

An API key may let the holder invoke the API.

Treating both as generic strings hides that distinction.

---

## Secret Types Carry Different Authority

The word `secret` covers several security models.

| Secret type | Typical authority or purpose | Important lifecycle concern |
| --- | --- | --- |
| Password | Authenticate a user or service | Human reuse, rotation, brute-force resistance, revocation |
| API key | Identify or authorize a caller | Scope, destination, rotation, leak response |
| OAuth/OIDC client secret | Authenticate a confidential client | Client identity, audience/provider boundary, rotation |
| Database credential | Connect with database privileges | Database role scope, per-service isolation, connection-string leakage |
| Private signing key | Create signatures accepted by verifiers | Signing authority, non-exportability, key versioning, compromise |
| Certificate password | Unlock protected certificate material | Often protects another authority-bearing artifact |
| Access token | Exercise delegated or direct authority | Audience, scopes, short lifetime, bearer-token exposure |
| Refresh token | Obtain new access tokens | Longer-lived authority, storage, revocation, theft impact |
| Temporary credential | Exercise bounded authority for a short period | Expiration, audience/scope, renewal, clock and failure behavior |

Do not apply one lifecycle mechanically to all of them.

The architecture should reflect what possession actually permits.

---

## Confidentiality and Authority Are Separate Properties

A value can be sensitive without granting authority.

A value can also grant authority even if it contains no human-readable sensitive data.

For example:

```text
Opaque API key
```

may look meaningless.

Possession may still authorize requests.

Therefore:

```text
Not personally identifying
        ≠
Safe to disclose
```

and:

```text
Encrypted at rest
        ≠
Narrowly authorized
```

The architecture should protect both:

```text
Confidentiality
```

and:

```text
Authority scope
```

---

## Secret Custody and Secret Consumption Are Different Responsibilities

One of the most useful separations is:

```text
Custody
=
Where the secret is protected and who may obtain/use it
```

versus:

```text
Consumption
=
Which workload uses the secret for which operation
```

A secret-management platform may own custody.

The application still owns consumption behavior.

For example:

```text
Protected secret store
       ↓
Authorized workload identity
       ↓
Application client
       ↓
Specific external API
```

The secret store can answer:

> Is this workload allowed to retrieve or use the credential?

The application must still answer:

> Should this operation call that external API now?

Secret access is not a substitute for authorization, policy evaluation, destination validation, or execution governance.

---

## Assign Lifecycle Ownership Explicitly

A useful ownership table separates duties:

| Lifecycle stage | Questions to answer |
| --- | --- |
| Creation / issuance | Who creates the credential? What initial scope and lifetime does it receive? |
| Custody | Which system protects it? Who can read, use, export, replace, or administer it? |
| Delivery | How does an authorized workload receive access without unnecessary copies? |
| Acquisition | Which process or component obtains it, and when? |
| Use | Which operation, destination, audience, tenant, or resource may it authorize? |
| Rotation | Who creates the replacement and coordinates overlap? |
| Revocation | Who can disable the credential immediately? |
| Expiration | What happens when the credential naturally becomes invalid? |
| Removal | Where must old copies, caches, pipeline variables, or development artifacts be removed? |
| Compromise response | Who declares compromise, revokes authority, investigates exposure, and replaces affected credentials? |

If every row says:

```text
Application team
```

that may be appropriate for a small system.

It should still be an explicit choice rather than an accidental concentration of authority.

---

## Keep Secrets Out of Source Control

A checked-in secret crosses a large and persistent boundary:

```text
Developer workstation
      ↓
Git history
      ↓
Repository host
      ↓
Clones / forks / caches / backups
      ↓
Unknown future readers
```

Avoid storing real secrets in:

```text
appsettings.json
appsettings.Production.json
source constants
sample files
unit-test data
Dockerfiles
repository scripts
checked-in .env files
```

when those files are part of the repository artifact.

Use placeholders instead:

```json
{
  "ExternalApi": {
    "ApiKey": "__SUPPLIED_BY_DEPLOYMENT__"
  }
}
```

or omit the key entirely when the application can tolerate absence until deployment.

---

## `.gitignore` Prevents Some Future Adds; It Does Not Undo Disclosure

A common incident sequence is:

```text
Secret committed
      ↓
Secret noticed
      ↓
File added to .gitignore
      ↓
Team assumes secret is safe again
```

That is incorrect.

The secret may remain in:

- Git history.
- Existing clones.
- Pull-request diffs.
- CI logs.
- Repository caches.
- Mirrors or forks.
- Local backups.

The security response should begin with the credential's authority:

```text
Assume disclosed
      ↓
Revoke / disable old credential
      ↓
Issue replacement
      ↓
Remove unnecessary copies
      ↓
Investigate where it propagated
```

History rewriting may reduce future discoverability.

It is not a substitute for revocation.

> **Once an authority-bearing secret escapes, treat recovery as an authority problem, not only a Git-cleanup problem.**

---

## Development Secret Manager Has a Narrow Purpose

ASP.NET Core Secret Manager is useful for local development because it keeps values outside the project tree and source-controlled configuration.

That is a meaningful improvement over committing development secrets.

It should not be described as:

```text
Encrypted production vault
```

or:

```text
Enterprise key-management boundary
```

The important distinction is:

```text
Development convenience
        ≠
Production custody system
```

Local development still has risks:

- Developer-account access.
- Workstation compromise.
- Local debugging tools.
- Process inspection.
- Accidental logging.
- Copy/paste into tickets or chat.
- Reusing development credentials in shared environments.

Use local credentials with limited authority and short lifetimes where practical.

A development credential should not become a production fallback.

---

## Environment Variables Are Delivery Mechanisms, Not Automatic Vaults

Environment variables are common because many deployment platforms can inject them easily.

They can be useful.

They are not automatically private simply because they are not in a file.

Depending on the platform, environment values may be visible to:

- The process.
- Child processes.
- Container or orchestration tooling.
- Administrators.
- Diagnostic tooling.
- Crash reporting.
- Process inspection.
- Deployment metadata.
- CI job output when scripts echo the environment.

Therefore:

```text
Environment variable
        ≠
Secret vault
```

The right question is:

> Which trust boundaries can read this environment, and is that exposure acceptable for the credential's authority?

If an environment variable is the platform's supported delivery mechanism, use it deliberately and avoid copying it farther.

---

## Configuration Provider Precedence Can Change Which Secret Wins

ASP.NET Core composes configuration from multiple providers.

A simplified default sequence can include:

```text
appsettings.json
      ↓
appsettings.{Environment}.json
      ↓
User secrets in Development
      ↓
Environment variables
      ↓
Command-line arguments
```

Later providers can override earlier values.

That means secret review should ask:

```text
Which provider supplies this value in this environment?
Can another provider override it?
Can a deployment accidentally select an obsolete credential?
Can a command-line value replace the intended secret?
```

Do not validate only the checked-in configuration shape.

The application consumes the final composed value.

For broader provider and startup-invariant reasoning, see [Secure-by-Default ASP.NET Core Configuration](../aspnetcore/secure-by-default-configuration.md).

---

## Command-Line Arguments Are Poor Secret Carriers

A command such as:

```text
myapp --ApiKey=real-secret-value
```

may expose the secret through:

- Process listings.
- Shell history.
- Job logs.
- Deployment scripts.
- Diagnostic capture.

Command-line configuration is useful for many ordinary settings.

Treat it cautiously for secrets.

Prefer a delivery mechanism that does not intentionally place secret material into process invocation text.

---

## URLs and Query Strings Are Poor Secret Carriers

Avoid designs such as:

```text
https://api.example.test/resource?api_key=real-secret-value
```

URLs can be retained by:

- Reverse proxies.
- Access logs.
- Browser history.
- Monitoring systems.
- APM tools.
- Network intermediaries.
- Exception messages.
- Referrer handling in some contexts.

A credential protocol may define an appropriate header, token exchange, client certificate, or another mechanism.

Follow that protocol rather than inventing a query-string secret convention.

Even authorization headers must still be kept out of application logs and diagnostics.

---

## Prefer Deployment-Time Injection Over Source-Time Substitution

A build artifact is easier to trust when it does not contain environment-specific production secrets.

Prefer a relationship such as:

```text
Build artifact
      ↓
No production secret embedded
      ↓
Deployment selects environment
      ↓
Authorized deployment/runtime boundary supplies secret access
```

Avoid:

```text
Build pipeline fetches all production secrets
      ↓
Writes them into generated appsettings.json
      ↓
Packages secret-bearing artifact
      ↓
Same artifact copied widely
```

Sometimes a deployment system must render secret-bearing configuration.

If so, treat the rendered artifact as sensitive and control its storage, lifetime, access, and cleanup accordingly.

The important goal is to avoid turning a broadly distributed application artifact into a credential container unnecessarily.

---

## Prefer Identity Over Distributed Secrets When the Platform Supports It

A workload sometimes needs to authenticate to a platform service.

One design is:

```text
Long-lived client secret
      ↓
Copied into deployment
      ↓
Application presents secret
```

Another design may be:

```text
Workload identity
      ↓
Platform establishes workload identity
      ↓
Short-lived token issued for allowed audience/scope
      ↓
Application uses token
```

Managed identity, workload identity, service identity, and federated identity systems use different platform mechanisms, but they share an architectural advantage when correctly configured:

> The application may not need a long-lived reusable secret distributed to each instance.

This does not remove identity risk.

The workload identity itself becomes a security boundary.

Review:

- Which workload can assert the identity.
- Which resource accepts it.
- Which roles/scopes it receives.
- Which audience the resulting token targets.
- How tokens expire and renew.
- What administrators can change identity bindings.
- What happens if federation or token issuance is unavailable.

The goal is not:

```text
No secrets anywhere in the platform
```

The goal is often:

```text
Fewer long-lived distributed secrets
      +
Shorter-lived bounded authority
```

---

## Secret Zero Is the Bootstrap Question

A secret manager does not eliminate bootstrap trust.

The application still needs some way to prove:

```text
I am the workload allowed to obtain or use this secret.
```

That bootstrap mechanism is sometimes called the `secret zero` problem.

Possible bootstrap mechanisms include:

- Platform workload identity.
- Node or pod identity.
- Instance identity.
- Client certificates.
- Hardware-backed identity.
- A narrowly scoped bootstrap credential.
- An operator-mediated provisioning step.

The correct mechanism is environment-specific.

The architectural question is universal:

> **What initial trust lets this workload cross into secret custody, and who can impersonate that trust?**

Moving one long-lived credential into a vault while placing another equally powerful bootstrap secret in source control has not solved the problem.

---

## Scope the Credential to the Real Operation

Secure storage does not imply least privilege.

Compare:

```text
One administrator API key
      ↓
Read, write, delete, administer all resources
```

with:

```text
Service-specific credential
      ↓
May call only required API
      ↓
May perform only required operations
      ↓
May access only intended resources/tenant
```

Reduce authority where the credential system supports it:

- Operation or permission scope.
- Resource scope.
- Tenant scope.
- Audience.
- Destination.
- Environment.
- Time.
- Maximum lifetime.
- Use count where applicable.

This mirrors the principle from [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md):

> **Approval should not silently become broad or permanent authority.**

The mechanism differs, but the least-authority reasoning is the same.

---

## Audience and Resource Binding Matter

Tokens and credentials are often valid only for a particular recipient or resource.

A token issued for:

```text
Audience = inventory-api
```

should not automatically be accepted by:

```text
billing-api
```

merely because the cryptographic proof is valid.

Similarly, a credential scoped to one tenant should not authorize another tenant.

A consuming boundary should validate the bindings that define authority.

This is especially important when credentials cross service boundaries.

---

## Prefer Short-Lived Authority When Practical

A long-lived secret creates a long-lived compromise window.

Compare:

```text
Static API key valid for one year
```

with:

```text
Access token valid for minutes
      ↓
Renewed through controlled identity boundary
```

Short lifetime does not prevent theft.

It can reduce how long stolen authority remains useful.

Short-lived credentials also introduce operational responsibilities:

- Renewal.
- Clock behavior.
- Provider availability.
- Retry policy.
- Token caching.
- Expiration handling.
- Revocation semantics.

Do not choose short lifetimes without designing renewal and failure behavior.

---

## Keep Secret Material Out of Broad Application Context

A secret that enters a process can spread through ordinary programming convenience.

Avoid patterns such as:

```csharp
public sealed record ApplicationContext(
    string ActorId,
    string TenantId,
    string DatabasePassword,
    string ExternalApiKey,
    string AccessToken);
```

This turns every consumer of `ApplicationContext` into a secret consumer.

Prefer narrow ownership:

```text
Business component
      ↓
Requests operation from host-owned client
      ↓
Client / credential boundary obtains needed authority
      ↓
External resource
```

The business component may need to know:

```text
Call customer export service
```

without knowing:

```text
Bearer token = ...
```

---

## Acquire Credentials as Late and Narrowly as Practical

A provider-neutral shape can make the boundary visible:

```csharp
public interface IExternalCustomerClient
{
    Task<CustomerUpdateResult> UpdateAsync(
        CustomerUpdate request,
        CancellationToken cancellationToken);
}
```

The caller receives no credential.

The implementation owns authentication internally:

```text
Application operation
      ↓
IExternalCustomerClient
      ↓
Credential provider / workload identity
      ↓
Authenticated outbound request
```

This is usually stronger than passing:

```text
string accessToken
```

through multiple services merely because the final HTTP client needs it.

The exact implementation may use a token provider, client certificate, API key handler, managed identity, or another mechanism.

The architectural point is narrow exposure.

---

## In-Memory Exposure Still Exists

Once a process can use a secret, some form of authority is present in the runtime boundary.

Possible exposure paths include:

- Debuggers.
- Memory dumps.
- Crash dumps.
- Diagnostic tooling.
- Reflection or instrumentation.
- Malicious code running in-process.
- Unsafe exception messages.
- Serialization of objects that contain secrets.

Do not make unrealistic claims such as:

```text
Secret is never in memory
```

when the application receives a plaintext API key string.

Instead, reduce unnecessary lifetime and copying:

- Do not retain secret-bearing objects longer than needed.
- Do not place secrets in singleton objects unless the mechanism requires it and the risk is accepted.
- Avoid serializing secret-bearing configuration.
- Avoid debugging output of configuration objects.
- Prefer provider APIs that perform cryptographic operations without exporting private key bytes when available.
- Treat process/admin access as part of the threat model.

Managed custody can reduce exposure.

It does not make a compromised authorized process harmless.

---

## Administrator and Process Boundaries Matter

A secret may be inaccessible to ordinary users but visible to:

- Host administrators.
- Cluster administrators.
- Cloud subscription administrators.
- CI administrators.
- Secret-store administrators.
- Debugging operators.

That may be operationally necessary.

It should be recognized explicitly.

If one administrative role can:

```text
Change workload identity
Read secrets
Change application deployment
Read logs
Disable monitoring
```

then that role is a powerful trust domain.

A secret manager cannot create separation of duties that the surrounding IAM model removes.

---

## Background Jobs Need Their Own Secret Boundaries

A web application and its background worker may run in the same repository while having different responsibilities.

Avoid assuming:

```text
Same codebase
      ↓
Same secrets
```

If a worker only needs to send notifications, it may not need:

- Database administrator credentials.
- Deployment credentials.
- Signing authority.
- Billing API credentials.

Likewise, a web process may not need secrets used only by a maintenance worker.

Prefer per-process or per-service secret access where the deployment model allows it.

This reduces the impact of one workload compromise.

---

## Per-Service Isolation Is Stronger Than Shared Credential Pools

A common convenience is:

```text
Shared integration account
      ↓
Service A
Service B
Service C
Service D
```

That creates several problems:

- Compromise attribution becomes weaker.
- Rotation affects every service.
- Scope must satisfy the broadest consumer.
- Revoking one consumer may disrupt all consumers.
- One service compromise exposes shared authority.

Where practical, prefer:

```text
Service A identity / credential
Service B identity / credential
Service C identity / credential
```

with each receiving only the authority it needs.

Operational complexity increases.

Blast radius can decrease.

---

## Tenant Boundaries Apply to Secrets Too

Multi-tenant systems should ask whether credentials are:

```text
Shared across all tenants
```

or:

```text
Tenant-specific
```

A shared credential can be reasonable when the external resource is application-wide and tenant separation is enforced elsewhere.

A tenant-specific credential can be necessary when:

- Each tenant owns an external account.
- Data must be routed through tenant-specific destinations.
- Tenant revocation must be independent.
- Contractual or regional boundaries require isolated authority.

Do not store tenant secrets in a shared metadata dictionary merely because the dictionary already contains tenant configuration.

Define custody, lookup authorization, and isolation intentionally.

---

## External API Clients Should Own Their Authentication Boundary

A useful flow is:

```text
Business intent
      ↓
Host authorization / policy
      ↓
External API client
      ↓
Client obtains narrowly scoped credential
      ↓
Validated destination
      ↓
External API
```

Avoid:

```text
Controller receives production API key
      ↓
Passes key to service
      ↓
Passes key to repository
      ↓
Passes key to helper
      ↓
HTTP client uses key
```

Every hop becomes a new disclosure surface.

The calling code should pass operation data.

The infrastructure boundary should own infrastructure authentication.

---

## Keep Secrets Out of Logs, Traces, Metrics, and Errors

Secret leakage frequently occurs through observability rather than source control.

Do not intentionally emit:

- Passwords.
- API keys.
- Access tokens.
- Refresh tokens.
- Client secrets.
- Authorization headers.
- Session cookies.
- Database passwords.
- Connection strings containing credentials.
- Private keys.

Watch indirect paths too:

```text
Exception message
      ↓
Includes request URL with token
      ↓
Central exception handler logs message
```

or:

```text
Configuration object
      ↓
Serialized for debug logging
      ↓
Contains client secret
```

or:

```text
Telemetry scope
      ↓
Adds Authorization header
      ↓
Every child span receives it
```

For the dedicated observability treatment, see [Secure Logging Across Trust Boundaries](secure-logging-across-trust-boundaries.md).

A secret should not enter telemetry merely because downstream redaction exists.

---

## Public Error Responses Must Not Echo Secrets

An internal failure may involve a credential.

The public response should not contain it.

Avoid returning:

```text
Failed to connect using Server=db;User Id=app;Password=...
```

or:

```text
External API rejected token eyJ...
```

Prefer a stable public problem code and correlation reference while preserving safe internal diagnostics.

This is the same boundary described in [Centralized Error Handling and Problem Details](../aspnetcore/centralized-error-handling-and-problem-details.md): public error disclosure and internal diagnosis are separate concerns.

---

## AI Models Should Not Receive Infrastructure Secrets Unnecessarily

AI-assisted execution makes secret ownership especially visible.

A weak pattern is:

```text
Host receives user request
      ↓
Host inserts production API key into model prompt
      ↓
Model emits tool call containing key
      ↓
Tool executes
```

The model now participates in credential custody.

The prompt, model context, provider telemetry, traces, and tool arguments may all become additional exposure surfaces.

Prefer:

```text
User request
      ↓
Model proposes operation
      ↓
Host validates proposal
      ↓
Host builds authoritative context
      ↓
Host authorizes / governs operation
      ↓
Host-owned tool handler obtains credential
      ↓
External resource
```

The model can know:

```text
Tool = customer.update
```

without knowing:

```text
API key = ...
```

This preserves the core [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) boundary:

> **The model may propose. The host retains execution authority.**

Credential custody belongs with the host or its credential provider unless the use case explicitly requires otherwise.

---

## Model-Visible Tool Arguments Should Not Become Secret Carriers

Suppose a tool schema is:

```json
{
  "tool": "customer.update",
  "arguments": {
    "customerId": "981",
    "apiKey": "..."
  }
}
```

The secret is now part of model-visible structured data.

Prefer a schema such as:

```json
{
  "tool": "customer.update",
  "arguments": {
    "customerId": "981"
  }
}
```

The host resolves authentication after proposal validation.

If a secret genuinely must be model-visible, document that exception explicitly and account for:

- Provider retention.
- Prompt logging.
- Conversation history.
- Tool-call logging.
- Evaluation datasets.
- Human review access.
- Exported traces.
- Incident response.

Do not make secret exposure the default tool contract.

---

## Signing-Key Custody Is a Specialized Secret-Custody Problem

Private signing keys deserve special treatment because possession can create artifacts that verifiers may accept as authentic.

The existing [Signing, Verification, Key Custody, and Tamper Evidence](signing-verification-key-custody-and-tamper-evidence.md) tutorial goes deeper into:

- Signing authority.
- Verification authority.
- Non-exportable key operations.
- Key IDs and versions.
- Rotation and compromise.
- Trust anchors.
- Evidence claims.

This tutorial has a broader scope.

It also covers:

- Passwords.
- Database credentials.
- API keys.
- Client secrets.
- Tokens.
- CI/CD credentials.
- Runtime secret delivery.
- AI-visible secret boundaries.

Therefore:

```text
Signing-key custody
        ⊂
Broader secret lifecycle architecture
```

The same least-authority principles apply, but signing keys add cryptographic verification and historical-evidence concerns that ordinary API keys may not have.

---

## Rotation Is a Coordinated State Transition

Rotation is often described as:

```text
Replace old secret with new secret
```

Real systems may require overlap:

```text
Issuer creates v2
      ↓
Consumers learn v2
      ↓
Both v1 and v2 accepted briefly
      ↓
Consumers move to v2
      ↓
v1 disabled
```

This grace window can reduce outages.

It also temporarily expands valid authority because two credentials may work.

A rotation plan should define:

- Who creates the new credential.
- How consumers discover or receive it.
- Whether both versions are valid concurrently.
- Maximum overlap duration.
- How success is observed.
- Which caches must refresh.
- When the old credential stops working.
- What rollback means.
- How stale instances are detected.

Rotation is not merely a scheduler job.

It is a distributed state transition.

---

## Rotation Does Not Automatically Revoke a Compromised Credential

Consider:

```text
Credential v1 stolen
      ↓
Credential v2 issued
      ↓
Application starts using v2
```

If v1 remains valid:

```text
Attacker still has authority
```

Therefore:

```text
Rotated
      ≠
Revoked
```

A compromise response should explicitly disable or invalidate the exposed authority as quickly as the system permits.

Planned rotation and emergency revocation are related but different operations.

---

## Revocation Needs a Real Enforcement Point

Revocation is meaningful only if consumers or providers enforce it.

Possible models include:

- Disable an API key at the provider.
- Remove a client secret from an identity provider.
- Revoke or disable a certificate/key version.
- Disable a service account.
- Remove a workload role binding.
- Revoke a refresh token.
- Wait for a short-lived access token to expire while preventing renewal.

A local configuration change that stops your application from using the secret does not necessarily revoke the secret for an attacker who already has it.

Ask:

> **Where is the authority actually invalidated?**

---

## Expiration Is Not the Same as Revocation

Expiration says:

```text
Authority becomes invalid after time T
```

Revocation says:

```text
Authority is invalidated before its natural expiry
```

Short-lived credentials can reduce reliance on complex revocation for some threat models.

Long-lived refresh or bootstrap authority may still require explicit revocation.

Design both mechanisms according to the credential type.

---

## Compromise Response Should Be Designed Before Compromise

A secret incident is easier to handle when the team already knows:

1. Which resource the credential can access.
2. Which workloads use it.
3. Where it is stored or delivered.
4. Who can revoke it.
5. How to issue a replacement.
6. Whether overlap is allowed.
7. Which logs can show credential use without containing the secret itself.
8. Which downstream systems may have copied it.
9. Which code, pipeline, ticket, prompt, or diagnostic surface exposed it.
10. Whether related credentials must also be rotated.

A useful response flow is:

```text
Suspected disclosure
      ↓
Contain authority
      ↓
Revoke / disable
      ↓
Issue replacement
      ↓
Deploy / renew authorized consumers
      ↓
Investigate propagation and use
      ↓
Remove stale copies
      ↓
Review why the lifecycle allowed exposure
```

Do not wait for the incident to discover who owns revocation.

---

## Missing Secrets Need Deliberate Failure Behavior

A required secret may be unavailable because:

- Deployment injection failed.
- Secret store is down.
- Workload identity is misconfigured.
- Credential expired.
- Rotation left a stale reference.
- Network policy blocks the provider.
- The secret was intentionally revoked.

The application needs a defined response.

Three patterns are useful.

### Fail Fast

Use when the application cannot perform its core responsibility safely without the credential.

```text
Required database credential unavailable
      ↓
Primary function impossible
      ↓
Startup fails
```

### Fail Closed for the Affected Capability

Use when one consequential integration is optional.

```text
Payment credential unavailable
      ↓
Payment operation unavailable
      ↓
No anonymous or fallback payment path
      ↓
Other safe features continue
```

### Deliberate Degraded Mode

Use only when the reduced state is understood and safe.

```text
Optional telemetry credential unavailable
      ↓
Remote export disabled
      ↓
Local bounded diagnostics continue
      ↓
Operator-visible degraded state
```

Avoid:

```text
Credential unavailable
      ↓
Use broader administrator credential
```

or:

```text
Credential verification unavailable
      ↓
Skip authentication
```

Failure must not silently expand authority.

---

## Rotation Failure Is Its Own Operational State

A system can fail during rotation even when both old and new credentials are valid individually.

Examples:

- Half the instances have v1 and half have v2.
- Provider accepts only v2 before all clients refresh.
- Secret cache ignores the new version.
- Rollback restores code but not the old credential.
- A background worker keeps a credential for days.

Test rotation as a lifecycle, not only as a value replacement.

Useful observability can include safe metadata such as:

```text
Credential logical name
Credential version identifier
Acquisition status
Expiration time bucket
Rotation phase
```

without logging the secret itself.

---

## Background Services Must Refresh Deliberately

Long-running hosted services may acquire a credential at startup and keep it indefinitely.

That can defeat rotation.

Ask:

```text
When is the credential resolved?
Is it cached?
How long?
How is expiration detected?
Does the client library renew automatically?
What happens to in-flight work during rotation?
```

A short-lived token provider may make renewal automatic.

A static API key may require explicit client refresh or process restart.

The architecture should match the credential mechanism.

---

## CI/CD Secrets and Runtime Secrets Are Different Trust Domains

A repository workflow may need authority to:

- Read packages.
- Publish a package.
- Deploy an environment.
- Request a cloud token.
- Upload an artifact.

The running application may need authority to:

- Access a database.
- Call a production API.
- Read a runtime secret.

These should not automatically be the same credentials.

Avoid:

```text
One production credential
      ↓
CI validation
Release job
Deployment job
Runtime application
Developer debugging
```

Prefer separation such as:

```text
Validation job
    → read-only repository/package authority

Publish job
    → package publication authority

Deploy job
    → deployment authority

Runtime workload
    → application resource authority
```

This limits what one compromised stage can do.

---

## Repository Actions Secrets Are Not Runtime Secret Architecture

A GitHub Actions secret, environment secret, or equivalent CI secret facility can protect values used by workflow jobs.

That does not automatically make it the right runtime secret store.

Ask separately:

```text
Does the build/publish job need this secret?
```

and:

```text
Does the deployed workload need this secret?
```

If only the runtime needs it, avoid routing the value through CI merely because CI has a secret feature.

A deployment can sometimes configure workload identity or a runtime secret reference without exposing the underlying production credential to the build job.

For broader workflow-permission and supply-chain reasoning, see [Software Supply-Chain Integrity for .NET Repositories](software-supply-chain-integrity-for-dotnet-repositories.md).

---

## Federated/OIDC Credentials Can Reduce Stored CI Secrets

Modern CI systems can sometimes exchange a workflow identity for a short-lived cloud or service credential.

Conceptually:

```text
Workflow identity
      ↓
Federated trust policy
      ↓
Short-lived token
      ↓
Deployment / publication action
```

This can reduce reliance on:

```text
Long-lived cloud secret stored in repository settings
```

The trust does not disappear.

It moves into:

- Repository/workflow identity.
- Branch or environment restrictions.
- Token audience.
- Federated trust configuration.
- Job permissions.
- Deployment environment policy.

Review those claims and bindings carefully.

A broadly trusted repository identity can be as dangerous as a broadly scoped stored secret.

---

## Forks and Pull Requests Need Special CI Secret Boundaries

Untrusted or less-trusted contribution code should not automatically receive privileged secrets.

A workflow triggered from a pull request can execute repository-controlled or contributor-controlled code depending on the design.

Ask:

- Can this event access repository secrets?
- Does it execute code from the proposed change?
- Can that code print or exfiltrate a credential?
- Is a privileged environment approval required?
- Are publication/deployment jobs separated from validation jobs?

The safest design usually keeps high-authority secrets away from code that has not crossed the intended review boundary.

---

## Secret Scanning Is Detection, Not Prevention

Secret scanning can identify values that match known token formats or detection patterns.

That is useful.

It does not prove:

```text
No secrets exist in the repository
```

because:

- Custom formats may not match detectors.
- Encoded or transformed values may evade detection.
- The scanner may not inspect every external system where the secret propagated.
- A secret may leak through logs, prompts, tickets, or artifacts instead of source.

Likewise, finding a secret does not revoke it.

Use scanning as:

```text
Detection signal
      ↓
Incident / remediation workflow
```

not:

```text
Scanner enabled
      ↓
Secret handling solved
```

---

## Test with Fictional or Placeholder Secrets

Tests and examples should not need real production authority.

Prefer values such as:

```text
test-api-key-not-valid
example-client-secret
fake-token-for-parser-test
```

when no provider validates the credential.

For integration tests that genuinely need provider authentication, use:

- Dedicated test identities.
- Test-only scopes.
- Isolated non-production resources.
- Short lifetimes.
- CI secret/federation boundaries appropriate to the test.

Do not copy production credentials into test fixtures to make tests convenient.

Also avoid realistic-looking fake values that trigger secret scanners unless the test specifically validates scanning behavior.

---

## Test the Boundary, Not the Secret Value

Useful secret-handling tests prove architecture invariants.

Examples:

```text
Required credential missing
      ↓
Protected operation unavailable
```

```text
Model proposes external API action
      ↓
Model-visible arguments contain no infrastructure credential
      ↓
Host handler performs authentication
```

```text
Credential for tenant A
      ↓
Tenant B operation
      ↓
Rejected / separate lookup required
```

```text
Old credential revoked
      ↓
New operation cannot use old authority
```

```text
Secret-like value enters exception path
      ↓
Public error and captured telemetry do not contain it
```

```text
Validation CI job
      ↓
No publication credential available
```

These tests are more valuable than asserting:

```text
ApiKey != null
```

The goal is to protect the trust boundary.

---

## A Provider-Neutral Runtime Boundary

A teaching abstraction can avoid committing the architecture to one secret product:

```csharp
public sealed record CredentialRequest(
    string Purpose,
    string Audience,
    string? TenantId = null);

public interface ICredentialUseBoundary
{
    Task<T> ExecuteAsync<T>(
        CredentialRequest request,
        Func<CancellationToken, Task<T>> protectedOperation,
        CancellationToken cancellationToken);
}
```

This intentionally does **not** expose:

```text
GetSecretStringAsync()
```

to every caller.

A real implementation might instead provide a configured `HttpClient`, database connection factory, signing service, token credential, SDK client, or provider-specific handle.

The important design question is:

> Can the application express the operation without distributing raw credential material to unrelated code?

Do not create an abstraction merely to hide a string getter behind an interface.

The abstraction should narrow responsibility.

---

## Secret References Are Safer Than Secret Values Only When Resolution Is Controlled

A configuration value such as:

```text
ExternalApi:CredentialRef = customer-api-prod
```

can be safer to retain than the secret itself.

But the reference is not magic.

Ask:

- Who can resolve it?
- Can the caller choose an arbitrary reference?
- Is the reference tenant-bound?
- Does resolving one name grant broad secret-store read access?
- Can an attacker substitute a different credential reference?

A secret reference should participate in authorization and configuration validation where appropriate.

---

## Provider-Specific Secret Services Solve Only Part of the Lifecycle

Azure Key Vault, AWS Secrets Manager, HashiCorp Vault, Kubernetes secret mechanisms, platform key stores, HSMs, and other providers expose different capabilities.

A provider may help with:

- Protected storage.
- Access control.
- Versioning.
- Rotation hooks.
- Audit events.
- Non-exportable key operations.
- Workload identity integration.

The provider does not automatically define:

- Which application component should receive the secret.
- Which operation the credential should authorize.
- Whether the credential is over-privileged.
- Whether logs contain the secret.
- Whether AI prompts contain the secret.
- Whether CI should access the runtime credential.
- Whether old credentials are revoked during compromise.
- Whether tenant isolation is correct.
- Whether failure behavior is safe.

Therefore:

> **A secret service is a custody mechanism inside a larger authority lifecycle.**

Keep provider claims proportional to what is actually configured and enforced.

---

## Working Implementation References

Learning keeps this tutorial provider-neutral.

The organization repositories provide useful specimens for specific boundaries without defining one required secret platform.

| Learning concept | Working reference | What to inspect |
| --- | --- | --- |
| Configuration layering and development/production secret separation | [NetCoreApplicationTemplate Configuration](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/docs/articles/configuration.md) | Provider precedence, environment overrides, Secret Manager guidance, environment variables, production review, and the boundary between application invariants and deployment values. |
| Secure configuration reasoning | [Secure-by-Default ASP.NET Core Configuration](../aspnetcore/secure-by-default-configuration.md) | Why secret values have different custody requirements from ordinary configuration and why missing critical values need deliberate startup behavior. |
| Logging and telemetry exclusion | [Secure Logging Across Trust Boundaries](secure-logging-across-trust-boundaries.md) | Why tokens, API keys, authorization headers, cookies, connection strings, and other authority-bearing values should be minimized before observability emission. |
| Signing-key custody | [Signing, Verification, Key Custody, and Tamper Evidence](signing-verification-key-custody-and-tamper-evidence.md) | The narrower cryptographic case: signing authority, trust anchors, key versions, provider boundaries, rotation, and compromise. |
| CI/workflow authority | [Software Supply-Chain Integrity for .NET Repositories](software-supply-chain-integrity-for-dotnet-repositories.md) | Workflow permissions, checkout credentials, OIDC identity, environment secrets, package credentials, cloud credentials, and the separation between validation and publication authority. |
| AI host-owned credential boundary | [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) | Why a model proposes an action while the host-owned tool handler keeps infrastructure credentials outside model-visible context. |
| Narrow follow-on authority | [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) | Actor, operation, resource, audience, time, and use bindings that provide an architectural analogue for reducing credential authority. |
| Audit/telemetry hygiene | [Safe Audit and Telemetry Data Guidance](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/safe-audit-telemetry-data.md) | Host responsibility for keeping credentials, tokens, connection strings, prompts, and uncontrolled payloads out of durable governance and telemetry paths. |

Use these as working specimens rather than as a claim that every deployment needs the same secret manager, identity provider, or credential type.

---

## Secret Boundary Review Worksheet

For each credential or secret, answer:

1. **What authority does possession grant?**
2. **Who creates or issues it?**
3. **Where is it stored between uses?**
4. **Can the underlying secret be exported, or can the provider perform the operation without exposing key material?**
5. **Which identity may obtain or use it?**
6. **How is that identity established?**
7. **What is the bootstrap or secret-zero boundary?**
8. **How is the value delivered to the workload?**
9. **Which process, service, job, or tenant actually needs it?**
10. **Can access be narrowed by service, tenant, operation, resource, scope, or audience?**
11. **How long is the authority valid?**
12. **Can a short-lived token replace a long-lived distributed secret?**
13. **Can workload identity remove the need to distribute the secret at all?**
14. **Can command-line, URL, environment, debugging, or process inspection reveal it?**
15. **Can it enter logs, traces, metrics, exceptions, audit residue, or public errors?**
16. **Can it enter an AI prompt, conversation, tool argument, evaluation set, or provider trace?**
17. **Which CI jobs can access it?**
18. **Does a validation job receive publication or deployment authority unnecessarily?**
19. **How is the credential rotated?**
20. **Is there an overlap/grace window, and how long?**
21. **How is the old credential revoked?**
22. **What is the natural expiration behavior?**
23. **What happens if the secret store or token issuer is unavailable?**
24. **Does failure preserve or expand authority?**
25. **Who can declare compromise and revoke the credential?**
26. **Which systems may contain stale copies after rotation or compromise?**
27. **Which test proves the protected operation cannot proceed with missing, expired, wrong-tenant, or revoked authority?**
28. **Which operational signal shows secret-acquisition failure without exposing the secret?**

If these questions cannot be answered, the secret lifecycle is probably implicit.

---

## Common Failure Modes

### 1. "It Is in a Vault, So It Is Solved"

Custody is protected, but the credential is long-lived, globally scoped, and copied into every service.

### 2. `.gitignore` Is Treated as Incident Response

A committed credential is ignored in future commits but remains valid and present in history.

### 3. Environment Variables Are Called a Vault

The deployment mechanism is confused with the complete access, lifecycle, and authority model.

### 4. Secret Manager Is Used as Production Key Management

A development convenience is treated as an encrypted enterprise custody system.

### 5. One Credential Is Shared Everywhere

Development, CI, deployment, and runtime all receive the same standing authority.

### 6. Rotation Leaves the Old Secret Valid Forever

The application moves to the replacement, but the exposed old credential remains usable.

### 7. Every Service Receives Every Secret

One compromised workload gains unrelated database, API, signing, or deployment authority.

### 8. Secrets Travel Through Generic Context Objects

Convenient dependency injection turns unrelated business code into secret consumers.

### 9. Secret Values Appear in Logs or Exceptions

A protected runtime value is copied into a much broader observability system.

### 10. Credentials Appear in URLs

Access logs, proxies, traces, or exception messages preserve the secret.

### 11. AI Context Contains Production Credentials

The model receives authority it does not need merely to propose an operation.

### 12. CI Receives Runtime Secrets

A build or validation job can exfiltrate a production credential even though the job only needs to compile and test.

### 13. Short-Lived Tokens Have Broad Scope

Short lifetime is treated as permission to grant excessive audience or resource authority.

### 14. Workload Identity Is Assumed Safe by Name

Federation or role bindings are broad enough that the wrong workload can obtain powerful tokens.

### 15. Secret Scanning Is Treated as Prevention

The repository scanner is green, but secrets leak through logs, prompts, artifacts, or custom formats.

### 16. Missing Credential Falls Back to Administrator Authority

A failure path expands privilege exactly when the trust dependency is unavailable.

### 17. Rotation Is Untested

The first real rotation reveals stale caches, partial deployments, or incompatible grace-window behavior.

### 18. Secret References Are Caller-Controlled

An untrusted request selects which credential alias the application resolves.

### 19. Compromise Ownership Is Undefined

Everyone can use the credential, but nobody knows who can revoke it quickly.

### 20. Provider Choice Is Mistaken for Architecture

A team names a secret-management product but cannot explain scope, runtime identity, leakage paths, revocation, or degraded behavior.

---

## Tradeoffs

### Benefits

- Explicit lifecycle ownership makes compromise and rotation easier to reason about.
- Treating secrets as authority discourages casual propagation as strings.
- Per-service and per-tenant isolation can reduce blast radius.
- Short-lived credentials can reduce the useful lifetime of stolen authority.
- Workload identity can reduce distribution of reusable long-lived secrets.
- Host-owned clients can keep credentials out of business and AI-visible context.
- Deliberate CI/runtime separation reduces supply-chain authority concentration.
- Defined failure behavior prevents missing credentials from becoming privilege expansion.
- Testing rotation and revocation protects lifecycle behavior rather than only startup configuration.

### Costs

- Secret managers, identity providers, and workload federation add operational dependencies.
- Per-service credentials increase provisioning and rotation work.
- Short-lived credentials require reliable renewal and time handling.
- Rotation overlap creates temporary dual-authority windows.
- Tenant-specific custody can increase secret count and operational complexity.
- Provider-neutral abstractions can hide useful platform capabilities if designed too generically.
- Fail-fast behavior can reduce availability when a safely degraded mode would be acceptable.
- Emergency revocation can disrupt legitimate workloads while containing compromise.

The objective is not zero secret-management complexity.

The objective is to make authority proportional, visible, and recoverable.

---

## Secret Handling Is Not a Production Security Guarantee

This tutorial describes architectural reasoning.

It does not prove that a production deployment is secure, compliant, or resistant to credential theft.

A real deployment may additionally require:

- Organization-specific identity and access management.
- Hardware-backed or non-exportable key custody.
- Network isolation.
- Platform-specific secret-store hardening.
- Rotation automation.
- Emergency revocation procedures.
- Administrative separation of duties.
- Secret-use monitoring.
- Data-loss prevention.
- Threat modeling.
- Incident response.
- Backup and recovery review.
- Legal or compliance controls.
- Penetration testing.

A vault does not create least privilege.

Encryption at rest does not prevent runtime leakage.

A short lifetime does not make broad authority narrow.

Rotation does not automatically revoke compromise.

A workload identity does not prove the role binding is correct.

Secret scanning does not prove no secret escaped.

> **Secret handling is the architecture of authority across a credential's lifecycle, not the name of the storage product that holds it.**

---

## Review Questions

Before moving on, you should be able to answer:

1. Why should many secrets be treated as authority-bearing values rather than only confidential strings?
2. Why is protected custody only one stage of the secret lifecycle?
3. What is the difference between secret custody and secret consumption?
4. Why does `.gitignore` not recover a secret that was already committed?
5. Why should an accidentally disclosed credential normally be revoked even if Git history is rewritten?
6. Why is ASP.NET Core Secret Manager a development convenience rather than a production vault?
7. Why are environment variables not automatically a secret-management system?
8. How can configuration-provider precedence affect secret selection?
9. Why are command-line arguments and query strings poor secret carriers?
10. What is the architectural advantage of workload identity or federation when it replaces a distributed long-lived secret?
11. What is the secret-zero/bootstrap problem?
12. Why should credential authority be narrowed by scope, audience, resource, tenant, or lifetime where possible?
13. Why can short-lived credentials still be dangerous when over-scoped?
14. Why should background jobs and separate services not automatically share the same secret set?
15. Why should infrastructure credentials remain outside AI prompts and model-visible tool arguments when possible?
16. How does signing-key custody differ from the broader secret-lifecycle problem?
17. Why does planned rotation differ from revocation?
18. Why can a rotation grace window temporarily expand valid authority?
19. What should a compromise-response plan know before an incident occurs?
20. How should an application distinguish fail-fast, fail-closed, and deliberate degraded behavior when credentials are unavailable?
21. Why should CI validation, publication, deployment, and runtime credentials be separated?
22. How can federated/OIDC workflow identity reduce stored CI secrets while still creating important trust-policy boundaries?
23. Why is secret scanning detection rather than prevention?
24. What kinds of tests prove secret-handling invariants without using production credentials?
25. Why does choosing a respected secret-management provider not complete the secret lifecycle by itself?

---

## Related Content

- [Security](index.md) — return to the Security learning-area overview.
- [Trust Boundaries and Least Privilege](trust-boundaries-and-least-privilege.md) — identify where control changes and reduce the authority that crosses each boundary.
- [Secure-by-Default ASP.NET Core Configuration](../aspnetcore/secure-by-default-configuration.md) — review configuration composition, startup validation, Secret Manager boundaries, environment variables, and deployment-owned values.
- [Secure Logging Across Trust Boundaries](secure-logging-across-trust-boundaries.md) — keep credentials and authority-bearing values out of operational telemetry and downstream collectors.
- [Signing, Verification, Key Custody, and Tamper Evidence](signing-verification-key-custody-and-tamper-evidence.md) — study the specialized cryptographic key-custody case and verification trust model.
- [Software Supply-Chain Integrity for .NET Repositories](software-supply-chain-integrity-for-dotnet-repositories.md) — apply least-authority reasoning to workflow permissions, CI credentials, publication, and provenance.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — compare credential scope with narrow follow-on execution authority.
- [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) — keep model proposals separate from host-owned infrastructure credentials and execution authority.
- [Centralized Error Handling and Problem Details](../aspnetcore/centralized-error-handling-and-problem-details.md) — keep internal credential-related failure detail out of public responses.

---

> **Read it. Run it. Question it. Improve it.**
