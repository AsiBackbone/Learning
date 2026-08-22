---
description: Learn to use threat modeling as architecture reasoning by tracing assets, authority, trust boundaries, abuse paths, mitigations, invariants, and residual risk before implementation choices harden.
---

# Threat Modeling as Architecture Reasoning

**Pattern classification:** General learning material

**Difficulty:** Intermediate

**Prerequisites:** [Trust Boundaries and Least Privilege](trust-boundaries-and-least-privilege.md) and the foundational [Decision Before Execution](../tutorials/decision-before-execution.md) tutorial. Familiarity with [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) is helpful for the worked example but is not required.

**Learning objective:** Use threat modeling as a repeatable architecture-reasoning technique: define scope, identify assets and authority, mark trust and execution boundaries, enumerate plausible abuse paths, map controls to concrete threats, expose unprotected assumptions, define testable invariants, record residual risk, and revisit the model when architecture changes.

## Pattern Card

> **Problem:** Teams can accumulate authentication, signatures, scanners, logs, policy checks, and other controls without being able to explain which threats those controls address, which assumptions remain unenforced, or which alternate paths still reach a consequential side effect.
>
> **Pattern:** Begin with the architecture and reason through it under adversarial and failure conditions. Trace assets, data, authority, trust changes, entry and egress points, dependencies, and side effects before selecting or evaluating mitigations.
>
> **Use when:** A feature crosses trust or execution boundaries, handles secrets or sensitive data, delegates authority, depends on external systems, performs consequential side effects, exposes administrative operations, or allows AI/automation to propose actions.
>
> **Prefer something simpler when:** A tiny local operation has one well-understood trust context, no meaningful delegated authority, no sensitive assets, and no consequential external side effect. Even then, the architecture should still make its assumptions explicit.
>
> **Observe:** Every meaningful mitigation can be traced to a concrete threat or assumption, and every consequential execution path has an explicit security or authority decision that can be tested.

Threat modeling is often introduced as a worksheet, a compliance artifact, or a prelude to penetration testing.

Those can be useful outcomes.

They are not the primary architectural value.

The core idea in this tutorial is:

> **Threat modeling is a way to reason about architecture under adversarial conditions.**

A useful starting flow is:

```text
Actor
   ↓
Input boundary
   ↓
Application / host
   ↓
Policy decision
   ↓
Authority boundary
   ↓
External dependency or executor
   ↓
Side effect
```

The threat-modeling questions are then architectural questions:

```text
What are we protecting?
Who controls each input?
Where does trust change?
Where does authority increase?
What can be replayed, forged, modified, disclosed, exhausted, or bypassed?
What happens when a dependency fails?
What evidence would reveal misuse?
Which assumptions are merely assumed rather than enforced?
```

The result is not a certificate that a system is secure.

It is a clearer explanation of how the system is expected to remain safe when callers, dependencies, data, credentials, administrators, model outputs, or operating conditions do not behave as hoped.

## Threat Modeling Is Not a Control Inventory

A control-first conversation often sounds like this:

```text
We use signatures.
      ↓
Therefore the system is secure.
```

or:

```text
We authenticate callers.
      ↓
Therefore requests are trustworthy.
```

or:

```text
The model was told not to call that tool.
      ↓
Therefore the tool cannot be called.
```

Each statement jumps from the existence of one control to a conclusion about the whole system.

Threat modeling reverses the direction:

```text
Architecture
   ↓
Assets and authority
   ↓
Trust boundaries
   ↓
Threat / abuse path
   ↓
Mitigation
   ↓
Verification
   ↓
Residual risk
```

The question becomes:

> **What failure or adversarial path does this control interrupt, and how do we know that interruption actually exists at the relevant boundary?**

A signature can help establish integrity and origin under a trusted-key model.

It does not decide whether the signed action is authorized.

Authentication can establish an identity under a trusted identity boundary.

It does not prove that every field supplied by that identity is authoritative.

A prompt can influence model behavior.

It does not remove host-side execution paths.

The architecture still has to enforce the boundary.

## Threat Models Complement Other Security Tools

Threat modeling, testing, scanning, and checklists are related but answer different questions:

| Tool | Primary question |
| --- | --- |
| Threat model | What can go wrong in this architecture, where can it happen, and which boundary should resist it? |
| Penetration test | Which weaknesses can be exercised in a concrete implementation under the defined scope? |
| Vulnerability scanner | Which known implementation or dependency weaknesses can be detected automatically? |
| Security checklist | Which recurring concerns should the team remember to inspect? |

None of these substitutes for the others. In particular, identifying a threat does not eliminate it; a mitigation may reduce likelihood or impact, increase detectability, transfer responsibility, or leave meaningful residual risk.

The architectural value of the threat model is that it connects those mitigations back to the specific assets, authority paths, and assumptions they are intended to protect.

## A Practical Ten-Step Method

This tutorial uses the following progression:

```text
1. Describe the architecture
        ↓
2. Identify assets and authority
        ↓
3. Mark trust boundaries
        ↓
4. Enumerate plausible abuse paths
        ↓
5. Map existing controls
        ↓
6. Find unprotected assumptions
        ↓
7. Change architecture or add mitigation
        ↓
8. Define an invariant/test
        ↓
9. Record residual risk
        ↓
10. Revisit when the architecture changes
```

The steps are deliberately iterative.

A newly discovered abuse path may reveal that the diagram is incomplete.

A proposed mitigation may create a new secret, dependency, administrator role, replay store, or availability requirement that must itself be modeled.

Threat modeling is therefore a loop, not a one-time form.

## 1. Describe the Architecture Before Listing Threats

Start by defining the system or feature under review.

A threat model that begins with an undefined scope quickly becomes either enormous or misleadingly narrow.

Write down:

- The operation being modeled.
- The actors who can initiate it.
- The host or application components involved.
- External systems.
- Data stores.
- Queues or asynchronous boundaries.
- Trust relationships.
- Execution boundaries.
- Administrative paths.
- Entry points.
- Egress points.
- Important side effects.
- Deployment or runtime assumptions that materially affect security.

A simple scope statement might be:

> Model the path by which an authenticated user asks an AI-assisted application to invoke an external customer-notification tool. Include model proposal, host validation, policy evaluation, capability issuance/validation, execution, credential use, logging, and the external provider. Exclude model training infrastructure and the provider's internal implementation except where provider compromise or unavailability affects our boundary.

That statement gives the review somewhere to stop.

### Draw the Smallest Useful Flow

Do not begin with every class and method.

Begin with components that change control, trust, authority, persistence, or side effects.

For example:

```text
User
  ↓
Public API
  ↓
AI Model
  ↓
Host Tool Gateway
  ↓
Policy Evaluator
  ↓
Capability Boundary
  ↓
Tool Executor
  ↓
External Provider
  ↓
External Side Effect
```

Then add detail only where it changes the reasoning.

A diagram should be simple enough to discuss but complete enough that a consequential alternate path cannot disappear outside the drawing.

## 2. Identify Assets and Security Objectives

An asset is anything whose confidentiality, integrity, availability, authority, provenance, or correct use matters to the system.

Assets are not only database rows.

For governed systems, important assets often include:

- Sensitive data.
- External resources that can be changed.
- Execution authority.
- Administrator privileges.
- Policy configuration.
- Capability grants.
- Secrets and credentials.
- Signing keys.
- Replay/use state.
- Audit residue and decision provenance.
- Trusted identity or tenant mappings.
- Service availability.
- Build and release integrity.

For each asset, write the security objective in plain language.

| Asset | Example objective |
| --- | --- |
| Customer record | Only an authorized operation may change the intended record. |
| Execution authority | Authority must remain bound to the actor, operation, resource, audience, time, and use count required by the decision. |
| Policy configuration | A caller or model must not be able to choose the policy version that governs its own request. |
| External API credential | The credential must remain host-owned and should not enter prompts, client payloads, logs, or unrelated components. |
| Audit residue | Records should be useful for reconstruction without exposing secrets or unnecessary sensitive payloads. |
| Service capacity | One actor should not be able to exhaust shared execution resources without bounded controls. |

The objective is more useful than a generic label such as "protect the database."

It tells reviewers what failure would matter.

## 3. Identify Actors, Administrators, Systems, and Authority

List the participants that can influence the flow.

Do not limit the list to ordinary end users.

Typical participants include:

- Anonymous caller.
- Authenticated user.
- Tenant administrator.
- Platform administrator.
- Background worker.
- Service identity.
- AI model.
- Policy provider.
- Gateway.
- External API.
- Database.
- CI/CD pipeline.
- Package/dependency source.
- Human operator.
- Adversarial external actor.
- Compromised legitimate actor.

Administrators deserve explicit modeling.

"Administrator" is not the same as "outside the threat model."

An administrator may be trusted for some operations while still being unable to:

- Read production secrets directly.
- Modify audit history.
- bypass a two-person release path.
- execute cross-tenant operations.
- mint arbitrary capabilities.

Threat modeling asks which authority the administrator actually has and which controls still apply.

### Trace Authority Separately from Data

A data-flow diagram may show:

```text
Gateway
   ↓
Executor
```

but the security question may be:

```text
Who gave the executor authority to act?
```

Authority can travel through:

- Session state.
- Roles or claims.
- Access tokens.
- Capabilities.
- Queue messages.
- Signed artifacts.
- Database flags.
- Approval records.
- Acknowledgment state.
- Service credentials.

Make that flow visible.

A useful annotation is:

```text
Data flow: proposed operation
Authority flow: short-lived capability
Credential ownership: executor only
```

That one distinction can reveal broad authority that a normal request diagram hides.

## 4. Mark Trust and Execution Boundaries

A trust boundary is a change in control over data, identity, state, or authority.

An execution boundary is the point where information becomes a consequential side effect.

They often overlap, but they are not identical.

Consider:

```text
Caller-controlled request
        ↓
-----------------------------  Trust boundary
Host reconstructs context
        ↓
Policy decision
        ↓
Scoped capability
        ↓
-----------------------------  Authority / execution boundary
Executor validates capability
        ↓
External side effect
```

At every trust boundary, ask:

- What is crossing?
- Who controlled it before the boundary?
- What will the receiving side believe about it?
- Which validation occurs here?
- Which security-sensitive facts are reconstructed from authoritative sources?
- Does authority increase after crossing?
- What happens if validation cannot complete?

At every execution boundary, ask:

- What exact side effect becomes possible?
- Which actor, operation, resource, and destination are bound?
- Which current authorization or policy facts are rechecked?
- Can stale or replayed authority reach this point?
- Which credential is used?
- What evidence remains?

> **Every meaningful trust boundary should have an explicit validation or authority decision.**

That decision may be simple.

It still needs to exist.

## Diagram Completeness Is a Security Property

An incomplete diagram can produce a correct threat model for the wrong system.

Suppose the diagram shows:

```text
API
 ↓
Governance Gateway
 ↓
Executor
```

but a scheduled job also calls the executor directly:

```text
Scheduled Job
      ↓
Executor
```

The gateway threat model may be excellent while the architecture still contains a bypass.

Therefore ask:

- Are there alternate controllers?
- Background workers?
- Administrative endpoints?
- Retry processors?
- Migration tools?
- Support utilities?
- Direct database paths?
- Emergency operations?
- Legacy integrations?
- Test hooks enabled in production?

The goal is not diagram beauty.

The goal is to ensure the model contains every path that can materially affect the asset or side effect under review.

## 5. Enumerate Plausible Abuse and Misuse Paths

An abuse path is a sequence in which a legitimate or illegitimate capability is used in a harmful way.

A misuse path may be accidental rather than adversarial.

Both matter because architecture often needs to resist the same unsafe transition.

For example:

```text
Caller controls TenantId
        ↓
Host trusts TenantId
        ↓
Policy evaluates wrong tenant
        ↓
Cross-tenant action becomes allowed
```

The threat is not "hackers."

The threat is a concrete authority error created by a trust assumption.

### Ask Path Questions

For each flow, ask whether an actor can:

- Spoof an identity or source.
- Modify input or state.
- Forge an artifact.
- Replay an otherwise valid artifact.
- Bypass a policy or gateway.
- Escalate privilege.
- Substitute a resource or tenant.
- Change the destination.
- Disclose a secret or sensitive value.
- Cause sensitive data to enter logs or prompts.
- Exhaust CPU, memory, requests, queue capacity, or external-provider quota.
- Exploit a fail-open or degraded path.
- Compromise an external dependency.
- Compromise a package, workflow, artifact, or release path.
- Suppress, modify, or flood evidence.
- Trigger the same side effect twice through retries or replay.

These questions are more useful when tied to a specific component or boundary.

## STRIDE Is Optional Vocabulary, Not the Architecture

STRIDE is one common threat-enumeration vocabulary:

| Category | Useful architecture question |
| --- | --- |
| Spoofing | Can an actor or service appear to be a different trusted identity? |
| Tampering | Can input, state, policy, capability, or evidence be modified without detection or validation? |
| Repudiation | Could a consequential action become difficult to attribute or reconstruct? |
| Information disclosure | Can secrets or sensitive data cross to a party, log, prompt, store, or provider that should not receive them? |
| Denial of service | Can an actor or dependency failure exhaust a resource required for safe operation? |
| Elevation of privilege | Can a less-privileged participant acquire broader authority than the architecture intended? |

The vocabulary can prompt useful questions.

It should not become a substitute for understanding the system.

A team that labels six boxes with six STRIDE letters but misses a direct executor bypass has not completed the important reasoning.

Other approaches can work equally well if they force the architecture to make threats and assumptions explicit.

## Threat, Vulnerability, Risk, Mitigation, and Residual Risk

These terms are related but different.

### Threat

A plausible harmful event or abuse path.

Example:

> A valid capability is captured and replayed to trigger the same consequential operation again.

### Vulnerability or Weakness

A condition that makes the threat easier to realize.

Example:

> The executor validates expiration and audience but stores no bounded-use state.

### Risk

The significance of the threat in context, including likelihood, impact, exposure, and the value of the affected asset.

Example:

> Replaying a read-only metadata request may be low impact; replaying a funds-transfer or destructive administrative capability may be high impact.

### Mitigation

A design, control, or operational measure intended to reduce likelihood or impact.

Example:

> Atomically consume a one-time capability identifier in durable state before execution.

### Residual Risk

What remains after mitigation.

Example:

> A crash after external execution but before durable completion recording may still require idempotency or reconciliation with the external provider.

Threat modeling becomes more useful when these are not collapsed into one vague "security issue" column.

## 6. Map Existing Controls Back to Threats

Now inventory the controls that already exist.

For each one, ask:

1. Which threat does it address?
2. At which boundary is it enforced?
3. What assumption does it depend on?
4. How is it verified?
5. What does it not protect against?

Example:

| Control | Threat addressed | Boundary | Important limit |
| --- | --- | --- | --- |
| Authentication | Identity spoofing | Public API / identity boundary | Does not make caller-supplied resource or tenant facts authoritative. |
| Authorization | Unauthorized actor/resource operation | Host authorization boundary | Does not necessarily model defer, acknowledgment, or other governance states. |
| Tool allowlist | Model proposes unknown executable tool | AI proposal / host gateway boundary | Does not validate the arguments of an allowed tool. |
| Signature verification | Artifact modification / untrusted origin under a key model | Artifact verification boundary | Does not prove the action is currently authorized. |
| Bounded-use replay state | Reuse of otherwise valid capability | Execution boundary | Must be atomic and match the deployment/concurrency model. |
| Structured, minimized logging | Sensitive-data leakage and weak observability | Observability egress boundary | Logging cannot replace prevention or durable governance evidence. |
| Rate limiting | Resource exhaustion | Public or expensive-operation boundary | Does not guarantee downstream dependencies remain available. |

This table prevents the existence of a control from becoming a security conclusion by itself.

## 7. Find Unprotected Assumptions

Threat models become especially valuable when they expose assumptions that no component actually enforces.

Examples:

```text
"The caller will send the correct tenant."
```

```text
"The capability will only be used once."
```

```text
"Only the gateway can reach the executor."
```

```text
"The external provider will always be available."
```

```text
"The model will follow the system prompt."
```

```text
"Developers will never log the secret."
```

```text
"The administrator would not change this setting."
```

Turn assumptions into explicit questions:

- Is this precondition validated?
- By which component?
- From which authoritative source?
- Is the validation current at execution time?
- Can a second path avoid it?
- Can failure of the validating dependency broaden authority?
- Can an operator disable the protection silently?

An **assumption register** can be useful:

| Assumption | Enforced by | Verification | Failure behavior |
| --- | --- | --- | --- |
| Resource tenant comes from current host state | Resource repository/context factory | Cross-tenant negative test | Deny if resource cannot be resolved |
| Only one capability use is allowed | Atomic replay store | Concurrent-use test | Block second use |
| Executor is reached only through gateway | Composition/module boundary + tests | Direct-path architecture test/review | No alternate production registration |
| Model cannot access provider credential | Host-owned secret acquisition | Prompt/context fixture test + logging test | Tool execution fails if host cannot acquire credential |

If the "Enforced by" column is empty, the assumption deserves attention.

## 8. Change Architecture or Add a Mitigation

Not every threat should be answered by adding another product or check.

Sometimes the strongest correction is architectural simplification.

Consider:

```text
Caller controls field
      ↓
Host trusts field
      ↓
Policy decision changes
```

One response might be to add a complicated signature scheme around the caller's field.

A simpler architectural correction may be:

```text
Caller proposal
      ↓
Host reconstructs authoritative context
      ↓
Policy evaluates trusted context
```

The second design removes the need to trust the caller for that fact.

Similarly:

```text
Model receives broad provider credential
      ↓
Model proposes action
      ↓
Provider called
```

can often become:

```text
Model proposes semantic action
      ↓
Host validates and governs
      ↓
Host-owned executor acquires narrow credential
      ↓
Provider called
```

The architecture reduces where authority exists instead of merely attempting to control a broad authority after distribution.

### Prefer Risk Removal When Practical

Ask:

> Can the system remove the dangerous path rather than protect it with another layer?

Examples:

- Remove direct executor registration from public request handlers.
- Do not send a secret into model context at all.
- Replace caller-supplied authorization facts with host-resolved facts.
- Use a single-purpose service identity instead of a broad administrator credential.
- Make a destructive endpoint unavailable rather than hidden behind an undocumented flag.
- Use an allowlisted semantic operation instead of accepting arbitrary URLs or commands.

Security controls remain necessary.

But a smaller attack and authority surface often removes more risk than stacking controls around an unnecessary path.

## Mitigations Introduce Their Own Assumptions and Costs

A mitigation is part of the architecture too.

Adding a replay store introduces:

- Durable state.
- Concurrency behavior.
- Availability requirements.
- Cleanup/retention behavior.
- Failure windows.

Adding signature verification introduces:

- Key custody.
- Key distribution.
- Algorithm choices.
- Rotation.
- Revocation or compromise handling.
- Trust in the issuer identity.

Adding a human approval step introduces:

- Reviewer identity and authorization.
- UI integrity.
- fatigue/rubber-stamping risk.
- latency.
- escalation and timeout behavior.

Adding an external risk service introduces:

- A new dependency.
- New data egress.
- New failure behavior.
- New trust in returned classifications.

A threat model should therefore ask:

> **What new assumptions did the mitigation create?**

## 9. Define an Architectural Invariant and a Verification Path

A mitigation is easier to trust when it produces an observable invariant.

Learning repeatedly uses tests such as:

```text
Denied decision
      ↓
Executor invocation count = 0
```

Threat modeling can extend that discipline.

Examples:

```text
Unknown tool proposal
      ↓
Host rejects proposal
      ↓
Executor invocation count = 0
```

```text
Expired capability
      ↓
Execution blocked
```

```text
Capability audience mismatch
      ↓
Execution blocked
```

```text
Second use of one-time capability
      ↓
Atomic consumption fails
      ↓
Second execution count = 0
```

```text
Caller supplies false tenant
      ↓
Host reconstructs tenant from authoritative state
      ↓
Policy sees host-owned tenant
```

```text
Policy dependency unavailable
      ↓
High-consequence operation does not silently become Allowed
```

Verification can include:

- Unit tests.
- Integration tests.
- Concurrency tests.
- Architecture tests.
- Configuration validation.
- Deployment checks.
- Log-content tests.
- Failure injection.
- Manual review for boundaries that are not easily automated.
- Penetration testing after implementation exists.

The key is to connect verification to the threat.

"Security tests passed" is less informative than:

> The gateway rejects an unknown model-proposed tool before a handler is resolved, and the invariant test proves handler invocation remains zero.

## 10. Record Residual Risk and Revisit the Model

No useful threat model ends with:

```text
All threats fixed.
```

Some threats will be:

- Mitigated.
- Partially mitigated.
- Accepted.
- Transferred to an external boundary.
- Deferred.
- Removed by architecture change.
- Out of scope for this model.

Record the result.

A compact residual-risk entry might contain:

```text
Threat:
External provider account is compromised.

Mitigation:
Use narrow provider credentials, destination restrictions,
provider-side controls, and alerting.

Residual risk:
A trusted provider with valid credentials can still perform
operations within the provider-side authority granted to it.

Owner:
Integration/platform team.

Review trigger:
Provider, credential scope, or destination model changes.
```

This is not legal or compliance risk acceptance by itself.

It is an architecture record that makes the remaining assumption visible.

## Revisit on Architecture Change

A threat model describes a particular architecture and trust relationship.

Revisit it when changes include:

- New public entry point.
- New administrative path.
- New external dependency.
- New queue or asynchronous worker.
- New tenant or regional boundary.
- New data classification.
- New credential type or secret store.
- New signing or verification model.
- New capability audience.
- New retry/replay behavior.
- New degraded-mode path.
- New tool or executor.
- New AI model or model-visible context.
- New release/deployment path.
- New package source.
- New trust relationship between services.

A useful maintenance habit is **diff-driven threat modeling**:

> What trust, authority, data, dependency, or execution path changed in this architecture change, and which threat-model assumptions depended on the old shape?

## Worked Example: Governed AI Tool Gateway

The [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) is a useful synthesis example because it already separates proposal from execution authority.

Start with this flow:

```text
User input
    ↓
AI model
    ↓
AI tool proposal
    ↓
Host validation
    ↓
Authoritative policy context
    ↓
Policy decision
    ↓
Scoped capability
    ↓
Execution-boundary validation
    ↓
Tool executor
    ↓
External dependency
    ↓
Side effect
```

This is not a formal threat model for the `AsiBackbone` implementation repository.

It is an educational model of the architecture pattern.

### Step A: Scope

In scope:

- User request entering the host.
- Model-produced tool proposal.
- Host-owned tool registry.
- Argument validation.
- Authoritative context construction.
- Policy evaluation.
- Capability issuance and validation.
- Replay/use state.
- Tool handler/executor.
- External provider credential use.
- Audit/logging paths.
- External provider interaction.
- Degraded behavior when required dependencies are unavailable.

Out of scope for this example:

- Training the model.
- Internal security of the model provider.
- Complete security of the external provider.
- Production key-management implementation details.
- Organization-wide incident response.

Out-of-scope components can still appear as dependencies whose compromise or failure affects the modeled system.

### Step B: Assets

The most important assets include:

1. **Real-world side effect** — only the intended operation should occur.
2. **Host execution authority** — the model must not own direct authority to invoke arbitrary tools.
3. **Authoritative actor/tenant/resource context** — security-sensitive facts must not be replaced by caller/model claims.
4. **Capability authority** — a valid grant must remain narrow, current, correctly addressed, and bounded in use.
5. **External credential** — the model, caller, logs, and unrelated components should not receive it.
6. **Policy integrity** — a caller or model should not bypass or choose the policy that governs its own request.
7. **Audit/operational evidence** — enough data should remain to explain decisions and failures without leaking sensitive values.
8. **Availability** — one request or dependency failure should not force the system into a broader-authority mode.

### Step C: Trust and Authority Boundaries

Annotate the flow:

```text
User
  │ caller-controlled input
  ▼
==============================  Boundary 1
Host / model interaction
  │ model output is untrusted proposed intent
  ▼
==============================  Boundary 2
Host validation + authoritative context
  │ host-owned facts
  ▼
Policy
  │ decision only; no side effect
  ▼
Capability issuer
  │ narrow execution authority
  ▼
==============================  Boundary 3
Execution gateway
  │ validates current authority
  ▼
Tool executor
  │ host-owned provider credential
  ▼
==============================  Boundary 4
External provider
  │ side effect
  ▼
External state
```

Boundary 2 is important even if the model runs inside the same application process.

The model controls the proposal.

The host controls executable meaning and authority.

Security boundaries are about control and trust, not only machines or network hops.

### Step D: Threat Table

| Threat / abuse path | Architectural question | Mitigation direction | Example invariant | Residual concern |
| --- | --- | --- | --- | --- |
| Model proposes an unknown tool | Can model output expand the executable surface? | Host-owned registry/allowlist rejects unknown names before handler resolution. | Unknown tool → handler invocation count = 0. | A registered tool may still be too broad. |
| Model proposes a valid tool with malicious arguments | Does an allowed name imply safe parameters? | Typed/schema validation plus semantic constraints, destination/resource allowlists, size limits. | Invalid destination → executor invocation count = 0. | Validation rules may be incomplete or stale. |
| Caller/model supplies false authoritative context | Who is allowed to establish actor, tenant, ownership, classification, or policy version? | Host reconstructs security-sensitive context from authenticated/current authoritative sources. | Caller tenant claim cannot alter host-resolved tenant. | Authoritative source itself can be stale or compromised. |
| Policy decision is bypassed by an alternate path | Can any controller, job, support tool, or handler reach execution directly? | Single host-owned execution boundary; remove or constrain alternate production paths. | Every production execution carries a valid decision/capability lineage. | Emergency/admin paths may need separate modeling. |
| Valid capability is replayed | Is valid authority reusable when it should not be? | Bounded-use grants plus atomic durable consumption matched to deployment topology. | Second use → second execution count = 0. | External side-effect/recovery windows may still need idempotency. |
| Capability audience is wrong | Can authority minted for one executor be redirected to another? | Bind and validate intended audience at execution. | Audience mismatch → blocked. | Audience identity must itself be trustworthy. |
| Secret appears in model-visible context | Does proposing an action require possession of infrastructure authority? | Keep credentials host-owned; executor acquires only what it needs at execution. | Test prompt/context fixture contains no provider credential. | Process-memory and privileged-host compromise still matter. |
| External dependency is compromised | What does a trusted provider get to do if it misbehaves? | Narrow credentials, destination restrictions, response validation, provider-side controls, monitoring. | Provider call cannot exceed configured semantic operation/resource scope where enforceable. | Third-party compromise cannot be eliminated locally. |
| Logging/audit path leaks sensitive values | Does evidence creation become a data-egress vulnerability? | Data minimization before emission, structured allowlists, secret filtering, separate governance evidence. | Known test secret never appears in captured logs. | Novel sensitive fields may still be added later. |
| Policy/risk dependency fails | Does unavailable governance broaden authority? | Explicit fail-closed, defer, or narrowly defined degraded behavior by operation class. | Consequential operation + unavailable policy → no execution. | Availability may be reduced intentionally. |
| Request floods exhaust model/tool resources | Can one actor monopolize expensive inference or execution capacity? | Rate limits, quotas, timeouts, bounded queues, concurrency limits, cost budgets. | Requests above bound are rejected/queued without unbounded resource growth. | Distributed or provider-level exhaustion can remain. |
| Dependency/build pipeline is compromised | Can a trusted tool handler be replaced before runtime controls see it? | Dependency integrity, locked restore where appropriate, pinned CI actions, artifact provenance, controlled publication. | Build/release policy rejects unauthorized dependency/workflow changes where tooling supports it. | Supply-chain controls reduce but do not eliminate upstream compromise. |

The table is intentionally not a list of products.

Every row begins with an architecture question.

### Deep Dive 1: Caller-Controlled Context

Suppose the proposal contains:

```json
{
  "tool": "customer.export",
  "tenant": "tenant-a",
  "classification": "public"
}
```

A weak flow is:

```text
Model/caller supplies tenant + classification
        ↓
Host places them in policy context
        ↓
Policy treats them as facts
        ↓
Decision changes
```

Threat-model finding:

> The participant seeking the operation can influence the facts that determine whether the operation is allowed.

The architectural correction is not "add security" generically.

It is:

```text
Model/caller proposes resource/action
        ↓
Host authenticates actor
        ↓
Host resolves current resource
        ↓
Host resolves tenant + classification
        ↓
Policy evaluates host-owned facts
```

The invariant becomes:

```text
Caller-controlled classification
      ≠
Policy classification
```

unless the host has explicitly validated and promoted the value into an authoritative fact.

This connects directly to [Trust Boundaries and Least Privilege](trust-boundaries-and-least-privilege.md).

### Deep Dive 2: Replay

Suppose the capability is valid:

```text
Actor: user-42
Operation: customer.export
Resource: customer-981
Audience: export-gateway
Expires: +2 minutes
Maximum uses: 1
```

A stateless validator may correctly verify every field twice:

```text
Use 1: valid
Use 2: valid
```

The threat path is:

```text
Capability valid
      ↓
Capability captured or retried
      ↓
Capability replayed
      ↓
Second execution occurs
```

Expiration alone does not solve the problem.

The architecture needs bounded-use state when the operation requires it:

```text
Validate capability
      ↓
Atomically consume use
      ↓
Only winner may execute
```

The invariant is:

```text
Two concurrent attempts
      ↓
At most one successful capability consumption
      ↓
At most one execution through this authority path
```

This still does not create an exactly-once guarantee across arbitrary external side effects.

The deeper failure windows are covered in [Replay Protection and Bounded-Use Authority](replay-protection-and-bounded-use.md).

### Deep Dive 3: Prompt Instruction Is Not an Execution Boundary

Suppose the system prompt says:

```text
Never call administrative tools unless the user is an administrator.
```

That instruction may improve model behavior.

It is not the final enforcement boundary.

Threat path:

```text
Prompt injection / model error / stale context
      ↓
Model proposes administrative tool
      ↓
Host trusts model decision
      ↓
Side effect
```

Architectural correction:

```text
Model proposes administrative tool
      ↓
Host registry recognizes tool
      ↓
Host resolves authenticated actor + resource
      ↓
Authorization / policy evaluates current facts
      ↓
Host-owned execution boundary
```

Invariant:

```text
Unauthorized actor
      ↓
Model may still propose
      ↓
Host blocks
      ↓
Executor invocation count = 0
```

This is why the Learning boundary remains:

> **The model may propose. The host retains execution authority.**

## Authentication, Authorization, Governance, and Threat Modeling

Threat modeling should keep these layers distinct.

### Authentication

Question:

> Who or what is this request operating as?

Threat-model concerns include:

- Credential theft.
- Session theft.
- Token validation.
- Issuer/audience trust.
- Privileged claim sources.

### Authorization

Question:

> May this actor perform this operation on this resource?

Threat-model concerns include:

- Missing resource binding.
- Cross-tenant access.
- privilege escalation.
- stale role/permission state.
- overly broad service identities.

### Governance / Policy

Question:

> Given current facts and constraints, what should happen next?

Threat-model concerns include:

- Caller-controlled policy facts.
- policy bypass.
- unsafe precedence.
- degraded-mode broadening.
- stale policy version.
- acknowledgment or escalation bypass.

### Threat Modeling

Question:

> How can any of those assumptions, boundaries, or paths fail under adversarial or abnormal conditions?

Threat modeling does not replace the other layers.

It examines whether they are positioned and enforced where the architecture assumes they are.

## Secrets and Credentials Are Authority-Bearing Assets

A secret is not only sensitive text.

Possession may allow an operation to occur.

Therefore threat modeling should trace:

```text
Creation
   ↓
Custody
   ↓
Delivery
   ↓
Runtime acquisition
   ↓
Use
   ↓
Rotation / revocation
   ↓
Removal
```

Ask:

- Which components can read the secret?
- Which components only need a semantic operation rather than the credential?
- Can the value enter logs, exceptions, prompts, traces, command lines, URLs, or crash dumps?
- What authority does possession grant?
- Can workload identity remove the need to distribute a static secret?
- What happens when the secret is unavailable or revoked?

See [Secret Handling Across Trust Boundaries](secret-handling-across-trust-boundaries.md) for the dedicated lifecycle treatment.

## Logging and Evidence Are Egress Boundaries

Threat models often focus on inbound requests and forget outbound observability.

But logging can create a second data path:

```text
Sensitive request / context
      ↓
Application
      ↓
Logger / tracer
      ↓
Collector
      ↓
Storage
      ↓
Operators / vendor / retention system
```

Ask:

- Which values are emitted?
- Which provider or collector receives them?
- Are tenants separated?
- How long are values retained?
- Can credentials or personal data leak through exceptions or structured properties?
- Is governance evidence separate from ordinary diagnostics?
- Does degraded logging change execution authority?

See [Secure Logging Across Trust Boundaries](secure-logging-across-trust-boundaries.md).

## External Dependencies Are Part of the Model

A dependency can fail without being malicious.

It can also become compromised.

Threat modeling should distinguish those cases.

### Unavailable Dependency

Questions:

- Does the operation fail closed, defer, or enter a deliberate degraded mode?
- Which operations may continue?
- Is authority ever broadened to preserve availability?
- Can retries amplify load or duplicate side effects?

### Compromised Dependency

Questions:

- What data does the dependency receive?
- What credentials or authority does it hold?
- Which responses does the host trust?
- Can the host validate response shape, source, destination, or provenance?
- Is the provider able to redirect operations?
- Can the integration be disabled or isolated?

The safer architecture often minimizes what a dependency needs to know and what it can cause.

## Supply Chain Is an Upstream Trust Boundary

Runtime security assumes that the deployed code is the code the organization intended to run.

That assumption depends on:

```text
Source
  ↓
Dependencies
  ↓
Build workflow
  ↓
Build environment
  ↓
Artifact
  ↓
Publication
  ↓
Deployment
```

A threat model for a consequential system should ask whether an attacker can change the component before runtime validation ever sees it.

Relevant controls may include:

- Dependency review/update practices.
- Locked restore where appropriate.
- GitHub Actions SHA pinning.
- Minimal workflow permissions.
- SBOM generation.
- Artifact attestations/provenance where supported.
- Controlled publication authority.
- Reproducible or verifiable build practices where practical.

See [Software Supply-Chain Integrity for .NET Repositories](software-supply-chain-integrity-for-dotnet-repositories.md).

These controls still need threat-specific interpretation.

An SBOM is useful inventory and evidence.

It does not, by itself, prove that every dependency is safe.

## Safe Degraded Operation Belongs in the Threat Model

A system can be secure during normal operation and unsafe when a dependency disappears.

Model the failure paths explicitly:

```text
Policy provider unavailable
        ↓
What happens?
```

```text
Replay store unavailable
        ↓
What happens?
```

```text
Secret provider unavailable
        ↓
What happens?
```

```text
Audit sink unavailable
        ↓
What happens?
```

The right answer may differ by operation.

A read-only, low-risk feature may continue with a constrained local policy.

A high-consequence external side effect may need to deny or defer.

The important architecture property is:

> **Failure behavior should be deliberate and should not silently broaden authority.**

The [Safe Degraded Mode and Fail-Safe Governance lab](../labs/safe-degraded-mode-and-fail-safe-governance.md) explores this reasoning in more depth.

## Denial of Service Is Architecture Reasoning Too

Threat modeling should not treat availability as an afterthought.

Ask where expensive work occurs:

```text
Request
  ↓
Model inference
  ↓
Policy lookup
  ↓
External provider
```

Potential controls include:

- Authentication before expensive work where appropriate.
- Rate limiting.
- Per-actor or per-tenant quotas.
- Request-size limits.
- Timeouts.
- Cancellation.
- Bounded queues.
- Concurrency limits.
- Circuit breakers.
- External-provider budgets.
- Backpressure.

The model should also consider whether protective limits create starvation or priority inversion for legitimate users.

Availability mitigations have tradeoffs like every other control.

## Use Abuse Cases, Not Only Happy-Path Requirements

A normal requirement may say:

> An administrator can disable an account.

An abuse-oriented companion question is:

> Can an administrator disable an account in another tenant?

Then:

> Can a caller cause the host to misidentify the tenant?

Then:

> Can a background job bypass the same resource check?

Then:

> Can a stale capability still disable the account after the administrator loses access?

Then:

> Can the operation execute twice if a retry races with the original request?

This progression turns a vague "secure admin endpoint" requirement into testable architecture boundaries.

## A Compact Threat-Model Worksheet

The following template is intentionally lightweight.

It can be used in an ADR, design review, pull request, or feature document without requiring a specialized tool.

```markdown
# Threat Model — <Feature / Flow>

## Scope
- In scope:
- Out of scope:
- Main side effect:

## Architecture Flow
<diagram or text flow>

## Assets / Objectives
- Asset:
  - Objective:

## Actors and Authority
- Actor:
  - Trusted for:
  - Not trusted for:
  - Authority held:

## Trust / Execution Boundaries
- Boundary:
  - What crosses:
  - Who controls it before crossing:
  - Validation / authority decision:
  - Failure behavior:

## Threats / Abuse Cases
- Threat:
  - Preconditions:
  - Path:
  - Asset affected:
  - Existing control:
  - Gap:

## Mitigation
- Architecture change or control:
- New assumptions introduced:
- Operational cost:

## Verification
- Invariant:
- Test / review / monitoring path:

## Residual Risk
- Remaining risk:
- Owner:
- Accepted / deferred / transferred / mitigated:
- Review trigger:
```

The value of the worksheet is the reasoning it preserves, not the number of rows completed.

## Common Failure Modes

### 1. Start with Products Instead of Architecture

Avoid:

```text
WAF
SIEM
Scanner
Signatures
MFA
```

as the threat model itself.

Those may be controls.

First explain the threat and boundary they address.

### 2. Treat Authentication as Trust in All Caller Data

An authenticated caller can still submit false:

- Tenant identifiers.
- Resource ownership claims.
- Classification values.
- Policy versions.
- destination URLs.
- role-like booleans.

Authentication establishes identity under an identity boundary.

It does not promote every request field into an authoritative fact.

### 3. Treat a Signature as Authorization

A valid signature may establish that an artifact was produced by a holder of a trusted key and has not changed since signing.

It does not prove:

- The signer was authorized for this resource.
- The action is still permitted.
- The artifact is not stale.
- The artifact has not been replayed.
- The signing key was not compromised.

See [Signing, Verification, Key Custody, and Tamper Evidence](signing-verification-key-custody-and-tamper-evidence.md).

### 4. Treat STRIDE Categories as Completion Criteria

Checking one item for each category can still miss:

- Alternate execution paths.
- Resource substitution.
- Tenant confusion.
- Unsafe failover.
- excessive credential distribution.
- model-context leakage.
- concurrency/replay races.

Use a taxonomy to expand questions, not to end them.

### 5. Ignore Administrators and Internal Tools

Internal does not mean consequence-free.

Admin and support paths often carry the broadest authority.

Model them explicitly.

### 6. Ignore Egress

Data can leak through:

- Logs.
- Metrics.
- Traces.
- Error responses.
- AI prompts.
- External providers.
- Exports.
- Analytics.

Threat models should trace data leaving the host as carefully as data entering it.

### 7. Stop After Identifying a Threat

A finding such as:

```text
Replay is possible.
```

is incomplete.

Continue to:

```text
Threat
  ↓
Mitigation
  ↓
Invariant
  ↓
Verification
  ↓
Residual risk
```

### 8. Add a Mitigation Without Modeling Its Failure

A replay store that fails open may defeat the intended protection.

A risk service that times out may create an unsafe fallback.

A logging system may leak the secret it was supposed to help investigate.

Model the mitigation as part of the architecture.

### 9. Assume One Diagram Shows Every Path

Compare the model with:

- Dependency injection registrations.
- endpoint maps.
- message consumers.
- background services.
- admin/support tools.
- deployment scripts.
- direct database operations.

A bypass path outside the drawing can invalidate the intended control flow.

### 10. Never Revisit the Model

Threat models age when:

- Trust relationships change.
- Components move.
- New tools are added.
- credentials broaden.
- AI-visible context expands.
- data classifications change.
- degraded modes evolve.

Treat the model as architecture documentation, not a completed ceremony.

## Tradeoffs

Threat modeling improves architecture reasoning, but it has costs.

### Benefits

- Makes hidden trust assumptions visible.
- Connects individual controls into one system-level explanation.
- Exposes bypass and alternate execution paths.
- Helps distinguish identity, authorization, policy, and execution authority.
- Gives security tests concrete invariants.
- Reveals when a simpler architecture removes risk.
- Documents residual risk and review triggers.
- Creates a shared language for developers, security reviewers, operators, and architects.
- Encourages failure-mode reasoning before production incidents reveal the boundary.

### Costs

- Requires current architecture diagrams and knowledgeable participants.
- Can become stale if not maintained.
- Can become ceremonial if teams optimize for completing a template rather than challenging assumptions.
- Detailed models can consume substantial review time.
- Risk ranking can create false precision when evidence is weak.
- Mitigations may add latency, state, dependencies, operational burden, or user friction.

The objective is not maximum documentation.

The objective is enough explicit reasoning to make consequential trust and authority paths understandable and testable.

## When a Simpler Architecture Is Better

Threat modeling should be allowed to conclude:

> Do not build this path.

Examples:

- Remove a direct administrative executor endpoint instead of securing two parallel paths.
- Keep a secret inside one executor instead of distributing it through three services.
- Replace arbitrary command execution with a small semantic command set.
- Avoid an AI tool entirely when a read-only recommendation is sufficient.
- Use built-in ASP.NET Core authorization when no richer governance lifecycle is needed.
- Keep a low-risk operation local instead of introducing delegated capability infrastructure.

Security architecture is not improved merely by adding components.

Sometimes fewer trust boundaries and less authority are the stronger design.

## Working Repository References

The Learning repository uses implementation repositories as architectural specimens rather than as universal templates.

Useful places to study include:

- [AsiBackbone](https://github.com/AsiBackbone/AsiBackbone) for governed decision and execution boundaries.
- [NetCoreApplicationTemplate](https://github.com/AsiBackbone/NetCoreApplicationTemplate) for ASP.NET Core trust, middleware, configuration, logging, and operational architecture.

When reviewing either repository, ask the same threat-model questions:

- Which component owns the input?
- Which component establishes authoritative context?
- Where does authority change?
- Where is execution possible?
- Which failure modes broaden or restrict behavior?
- Which evidence remains?
- Which assumptions are enforced versus documented?

This tutorial does **not** publish a formal threat model for either repository.

A repository-specific threat model would require a defined deployment, configuration, trust environment, operational model, and review scope.

## Scope and Boundaries

This material is educational.

It does not constitute:

- A security certification.
- A penetration test.
- A vulnerability assessment.
- A compliance assessment.
- A production threat model for any ASI Backbone organization repository.
- A guarantee that the listed mitigations are sufficient for a particular application.

Threat modeling identifies and structures reasoning.

It does not prove that all threats have been found, that every mitigation is correctly implemented, or that accepted residual risk is appropriate for a particular organization.

Application-specific review remains necessary.

## Related Content

- [Security](index.md) — view the complete Security learning path.
- [Trust Boundaries and Least Privilege](trust-boundaries-and-least-privilege.md) — identify changes in control and keep authority narrow across them.
- [Secret Handling Across Trust Boundaries](secret-handling-across-trust-boundaries.md) — trace authority-bearing credentials through custody, delivery, use, rotation, and revocation.
- [Secure Logging Across Trust Boundaries](secure-logging-across-trust-boundaries.md) — model observability as an outbound data and trust boundary.
- [Replay Protection and Bounded-Use Authority](replay-protection-and-bounded-use.md) — go deeper on replay state, atomic consumption, failure windows, and idempotency.
- [Signing, Verification, Key Custody, and Tamper Evidence](signing-verification-key-custody-and-tamper-evidence.md) — separate cryptographic evidence from authorization and current trust.
- [Software Supply-Chain Integrity for .NET Repositories](software-supply-chain-integrity-for-dotnet-repositories.md) — extend trust reasoning upstream into source, dependencies, workflows, artifacts, and publication.
- [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) — review the full proposal-to-execution composition used by the worked example.
- [Safe Degraded Mode and Fail-Safe Governance lab](../labs/safe-degraded-mode-and-fail-safe-governance.md) — practice reasoning about dependency failure without silently broadening authority.

---

> **Read it. Run it. Question it. Improve it.**
