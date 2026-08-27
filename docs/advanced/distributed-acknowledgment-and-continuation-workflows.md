---
description: Learn how acknowledgment can cross process and system boundaries without becoming portable authorization or execution authority.
---

# Distributed Acknowledgment and Continuation Workflows

**Learning objective:** Understand how a bound acknowledgment challenge, a response in another system, durable continuation state, current-context reconstruction, policy re-evaluation, and host-owned execution can remain separate when one workflow spans processes or trust boundaries.

**Pattern classification:** General learning material

**Advanced area:** Distributed acknowledgment and continuation workflows

**Difficulty:** Advanced

**Required prerequisites:** [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) and [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md).

**Recommended background:** [Human-in-the-Loop Governance Workflows](../governance/human-in-the-loop-governance-workflows.md), [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md), [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md), and [Cross-System Capability Exchange and Delegated Authority](cross-system-capability-exchange-and-delegated-authority.md).

**Glossary:** [Acknowledgment](../architecture/glossary.md#acknowledgment), [audit residue](../architecture/glossary.md#audit-residue), [decision provenance](../architecture/glossary.md#decision-provenance), [scoped capability](../architecture/glossary.md#scoped-capability), [execution authority](../architecture/glossary.md#execution-authority), and [trust boundary](../architecture/glossary.md#trust-boundary).

> **Framework-neutral scope:** This article teaches lifecycle, trust, replay, recovery, and authority boundaries. It does not define a messaging protocol, workflow product, identity federation scheme, signature format, durable-store technology, or exactly-once execution mechanism.

## Why This Matters

A synchronous acknowledgment can look simple because one host still has the original request, the original policy result, the challenge, the response, and the current resource state in one place. A distributed workflow removes that convenience. The policy decision may happen in one system, the acknowledgment interaction in another, and the eventual continuation in a third. Messages can be delayed, duplicated, reordered, lost, or replayed. Policy and resource state can change while the workflow waits. The central lesson is:

> **Acknowledgment crossing a system boundary is evidence of a specific response to a specific challenge. It is not portable execution authority.**

A valid response can therefore be necessary for continuation without being sufficient for execution.

---

## 1. Assumptions and Non-Goals

This treatment assumes:

- Participating systems can authenticate service interactions where required.
- The continuation host can define which acknowledgment-evidence issuers it trusts.
- Current authoritative context can be reconstructed or explicitly reported unavailable.
- Current policy can be re-evaluated before consequential execution.
- Production continuation state can provide atomic transitions appropriate to the deployment model.
- The workflow can bind not only the machine intent but also the presentation the responder actually saw, either through deterministic rendering or recipient-verifiable presentation evidence.

This treatment does **not** define:

- A universal acknowledgment event schema.
- OAuth, JWT, mTLS, PKI, or signature profiles.
- A workflow-engine product architecture.
- A message broker or delivery guarantee.
- A distributed transaction protocol.
- Exactly-once execution.
- A legal or compliance definition of acknowledgment or approval.
- A production identity-proofing model for human responders.
- A universal presentation-rendering format.
- A guarantee that one challenge lifetime is correct for every operation.

Those choices remain host- and deployment-specific.

---

## 2. At a Glance

A representative distributed path is:

```text
System A
policy decision
     |
     v
AcknowledgmentRequired
     |
     v
Bound challenge
     |
     +---------------------------+
                                 |
                                 v
System B                    human/system response
                                 |
                                 v
                         acknowledgment evidence
                                 |
     +---------------------------+
     |
     v
System C
validate evidence trust
     |
     v
load durable continuation state + expected challenge
     |
     v
validate challenge + lineage
     |
     v
reconstruct current context
     |
     v
re-evaluate current policy
     |
     v
claim one continuation
     |
     v
mint narrow local authority
     |
     v
host-owned execution
```

The important boundary is not the network hop by itself. The important boundary is that System C must decide what it currently trusts and what it may currently execute. A response from System B cannot force System C to reuse System A's old decision.

---

## 3. Keep the Lifecycle Artifacts Distinct

A distributed acknowledgment workflow commonly contains at least six different artifacts or states:

| Artifact / state | Primary owner in this scenario | Meaning | What it does not mean |
| --- | --- | --- | --- |
| Policy decision | System A | System A's decision at a particular policy/context state | Permanent permission |
| Acknowledgment challenge | System A | A bound request for a specific response | Approval or execution authority |
| Acknowledgment response | System B | What an identified responder said about that challenge | Current authorization |
| Acknowledgment evidence | System B issues; System C validates | Recipient-verifiable evidence that the response occurred | A portable capability |
| Continuation state | System C | Durable workflow state connecting the lifecycle | Permission to execute by itself |
| Scoped continuation authority | System C | Narrow authority minted after current validation | Standing permission or reusable acknowledgment |

Ownership above is illustrative rather than universal. The important point is that each system may assert only the facts and state its trust contract actually gives it authority to own.

The foundational distinction still applies:

```text
Acknowledgment
!= approval
!= authorization
!= execution authority
```

An approval may express a separate reviewer's decision. Authorization answers whether an operation may proceed under current rules. Execution authority is what the final host accepts immediately before a protected side effect. Distributed transport does not collapse those meanings into one token.

---

## 4. The Running Scenario

This article uses one fictional operation:

```text
accounts.bulk-suspend
```

System A evaluates a tenant administrator's request to suspend a bounded set of fictional accounts. Current policy requires acknowledgment of the operational impact. System B presents the challenge to the required responder and produces evidence of the response. System C owns the eventual account executor. The systems are separate enough that System C cannot safely assume it still has System A's original in-memory context. The challenge therefore needs durable bindings that System C can validate later. A compact challenge model might contain:

```csharp
public sealed record AcknowledgmentChallenge(
    string ChallengeId,
    string OriginatingDecisionId,
    string RequesterActorId,
    string RequiredResponderId,
    string Operation,
    string ResourceId,
    string ResourceVersionAtChallenge,
    string IntentCanonicalizationVersion,
    string IntentDigest,
    string RequirementCode,
    string PresentationVersion,
    string PresentationDigest,
    string PolicyId,
    string PolicyVersion,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string CorrelationId);
```

The exact fields are application-specific. The invariant is not. The challenge must carry enough identity and binding information to answer:

> What exact thing was this responder asked to acknowledge, what presentation represented that thing to the responder, under what policy state, and for how long?

---

## 5. Challenge Identity Is a Security Boundary

A generic confirmation such as:

```text
confirmed = true
```

is insufficient once a workflow crosses time or process boundaries. At minimum, a consequential challenge often needs explicit bindings for:

- Challenge identity.
- Originating decision identity.
- Requesting actor.
- Required responder or responder eligibility.
- Operation.
- Resource or resource set.
- Exact intent digest and canonicalization version.
- Requirement code.
- Presentation version and presentation fingerprint when the human-visible rendering is security-relevant.
- Policy identity and version.
- Correlation identity.
- Issuance and expiration.

The challenge identifier should be unique to that challenge lifecycle. Do not use the correlation identifier as a substitute for challenge identity. One correlation can legitimately connect several decisions, responses, retries, and execution attempts.

The continuation host should also have an independent reason to know **which challenge this continuation belongs to**. Loading whatever challenge is named by incoming evidence is weaker than loading durable continuation state first and checking that the evidence names the expected challenge.

### Required invariant

```text
Continuation state expects Challenge Y
+ trusted evidence names Challenge X
     |
     v
No continuation
No execution
```

This is the distributed-acknowledgment analogue of audience binding: a valid artifact for one challenge must not satisfy another continuation merely because the correlation or surrounding request looks related.

---

## 6. Bind the Machine Intent and the Responder Presentation

A responder should acknowledge one defined proposal, not an editable description of a proposal. The machine path needs both the canonicalization version and the digest:

```text
host-defined intent
     |
     v
canonicalization version + canonical bytes
     |
     v
stable digest
     |
     v
challenge binding
     |
     v
response evidence repeats version + digest
     |
     v
continuation host verifies both
```

For example:

```json
{
  "challengeId": "ack-2032-0042",
  "operation": "accounts.bulk-suspend",
  "resourceId": "tenant-a:batch-42",
  "intentCanonicalizationVersion": "bulk-suspend-v1",
  "intentDigest": "sha256:fictional-intent-digest",
  "requirementCode": "bulk-suspend-impact-ack",
  "presentationVersion": "bulk-suspend-presentation-v1",
  "presentationDigest": "sha256:fictional-presentation-digest",
  "requiredResponderId": "operator-17",
  "policyId": "bulk-suspend-policy",
  "policyVersion": "7.3",
  "expiresAtUtc": "2032-04-05T16:10:00Z"
}
```

The intent digest is only meaningful if the canonicalization rule is defined and versioned. If System A and System C serialize the same logical intent differently, a hash does not repair that semantic mismatch.

### Bind what the responder actually saw

Intent binding protects the **machine interpretation** of the operation. It does not prove that System B displayed a faithful representation of that operation to the responder. A screen that says "suspend 12 test accounts" could otherwise be paired with an intent that actually covers 12,000 accounts while every machine-side intent check still passes.

Two legitimate design shapes are:

1. **Deterministic presentation.** Derive the human-visible presentation from the canonical intent through a versioned rendering rule. Carry a presentation version and fingerprint in the challenge, and require response evidence to repeat them.
2. **Recorded presentation evidence.** Let the presentation system persist the exact rendered artifact or a stable fingerprint/reference to it, then let the continuation host verify that record through a recipient-trusted path.

The companion sample uses the first shape. Its fictional challenge and evidence carry `PresentationVersion` and `PresentationDigest`, and a mismatch blocks continuation. That fingerprint says what presentation was bound to the response; it does not prove that the responder read, understood, or agreed with every consequence.

### Required invariants

```text
Valid acknowledgment evidence
+ different intent digest or canonicalization version
     |
     v
No continuation
No execution
```

```text
Valid acknowledgment evidence
+ different presentation fingerprint
     |
     v
No continuation
No execution
```

A new intent, or a materially different presentation of that intent, requires the workflow behavior defined by current policy. For consequential changes that alter what the responder was asked to accept, that normally means a new challenge rather than silently reusing old evidence.

---

## 7. Bind the Responder Without Confusing Roles

Several identities can participate in one distributed workflow:

- Original requesting actor.
- Required acknowledgment responder.
- Authenticated service presenting evidence.
- System that issued the evidence envelope.
- System C workload identity.
- Final protected executor identity.

These identities may be different. The challenge should state who may respond or how responder eligibility is determined. The response evidence should identify who actually responded. System C should independently decide whether it trusts the evidence issuer and whether the reported responder satisfies the challenge. Do not infer:

```text
trusted transport caller
therefore
trusted human responder
```

or:

```text
correct responder
therefore
authorized executor
```

Those are different claims.

---

## 8. Acknowledgment Evidence Is a Trust Input

System C needs a defined reason to believe the evidence it receives from System B. Possible designs include:

- A mutually authenticated service channel plus a recipient-owned trust policy.
- A signed response envelope validated against accepted trust anchors.
- An opaque evidence reference resolved through a trusted acknowledgment service.
- A durable event copied into a recipient-controlled evidence store through an authenticated integration path.

This article does not choose one transport. The recipient-side questions remain:

```text
Who issued this evidence?
Do I trust that issuer for this evidence type?
Is the evidence intact?
Does it name the expected challenge?
Does it identify an eligible responder?
Does it repeat the exact intent binding?
Does it bind the presentation the responder saw?
Was the response timely?
Has this lifecycle already continued?
```

A signature, authenticated channel, or trusted database record can help establish evidence authenticity. It still does not answer whether current policy permits execution.

---

## 9. Response Time and Challenge Lifetime Are Different Checks

Two time questions matter:

1. Did the response occur while the challenge was valid?
2. Is the challenge still eligible for continuation now?

A response may have occurred before expiration but arrive at System C after the continuation window closed. For a consequential operation, the host may choose:

```text
response occurred before expiry
+ continuation attempted after expiry
     |
     v
No continuation
```

That is the rule used by the companion sample. Other systems may define a separate accepted-response retention window, but that rule must be explicit. Do not let transport delay silently extend the challenge lifetime.

### Required invariants

The response timestamp and the continuation timestamp protect different windows:

```text
Acknowledgment evidence
+ response occurred outside the challenge window
     |
     v
No continuation
No execution
```

```text
Valid acknowledgment evidence
+ challenge no longer eligible at continuation time
     |
     v
No execution
```

The companion sample tests both directions separately. A sequential expiration test cannot substitute for checking whether the response itself occurred during the original challenge window.

---

## 10. Durable Continuation State Owns the Pause

When the workflow can outlive one process, the pending state cannot live only in:

- Browser memory.
- An HTTP session.
- A message-handler local variable.
- A process-local dictionary in a production multi-instance service.
- The presence of an acknowledgment response itself.

A durable continuation record can preserve lifecycle state such as:

```text
ContinuationId
ChallengeId
OriginatingDecisionId
CurrentStatus
StateVersion
AcceptedEvidenceId
ClaimedAtUtc
CompletedAtUtc
LastReasonCode
CorrelationId
```

A useful state progression is:

```text
Pending
  |
  +-- decline ----------> Declined
  +-- timeout ----------> Expired
  +-- cancel -----------> Cancelled
  +-- supersede --------> Superseded
  |
  +-- accepted evidence
          |
          v
AcceptedEvidenceRecorded
          |
          v
RevalidationPending
  |
  +-- blocked ----------> Blocked
  |
  v
ContinuationClaimed
  |
  +-- execution fails --> ExecutionFailed
  |
  v
Completed
```

The exact states are not universal. The important property is that the workflow does not infer permission from message arrival alone.

---

## 11. Out-of-Order Delivery Is Normal, Not Exceptional

Distributed events can arrive in an inconvenient order. For example:

```text
System B evidence arrives
     |
     v
System C has not yet received durable challenge state
```

Safe choices include:

- Defer and retry after challenge state arrives.
- Buffer the evidence in a bounded pending store.
- Resolve the challenge from an authoritative source.
- Reject and require the sender to retry.

What should not happen is:

```text
challenge missing
     |
     v
assume challenge was valid
     |
     v
continue
```

The companion sample chooses a simple teaching behavior: `challenge.not-found` performs no claim and no execution, so the same evidence can be retried after the trusted challenge record becomes available. That behavior demonstrates recovery without turning missing state into permission.

---

## 12. Duplicate Delivery and Replay Are Related but Not Identical

A messaging system may legitimately redeliver the same event. An attacker or faulty client may also intentionally replay accepted evidence. Both can look like:

```text
same challenge
same or equivalent accepted response
arrives again
```

The execution boundary still needs one continuation rule. For a single-use acknowledgment workflow:

```text
first valid continuation claim
     |
     v
claim stored atomically
     |
     v
later duplicate / replay
     |
     v
No second authority
No second side effect
```

The replay key may be based on challenge identity, response identity, continuation identity, or a defined combination. The key must match the lifecycle guarantee. If exactly one accepted response may continue a challenge, claiming only by `ResponseId` is insufficient because two different response records could target the same challenge. The companion sample therefore claims by `ChallengeId` and records which evidence won that claim.

---

## 13. Atomic Claiming Must Match the Deployment Scope

A process-local lock can demonstrate the invariant. It does not create a distributed guarantee across replicas. Production multi-instance continuation normally requires a recipient-trusted atomic state transition such as:

```text
Pending
  |
  | compare-and-set / transaction / equivalent atomic transition
  v
Claimed
```

Only one participant should be able to observe a successful transition when the contract says the challenge is single-use. Possible implementations include a transactional database update, durable workflow-state transition, strongly consistent key/value operation, or another mechanism whose consistency properties match the requirement. Do not describe an ordinary cache lookup followed by a separate write as atomic replay protection.

The companion sample includes `TwoActuallyConcurrentContinuationClaimsProduceOneExecution`, which releases two callers against the same process-local claim store at the same time. The sequential replay tests remain useful for lifecycle behavior, but a sequential test alone does not prove the atomic race property.

---

## 14. Reconstruct Current Context Before Consequential Continuation

System C should not treat System A's old context as the current truth merely because it appears in the challenge. Before consequential continuation, the host should rebuild execution-relevant facts from authoritative sources it currently trusts. Examples include:

```text
Originating actor still valid?
Resource still exists?
Resource version changed?
Tenant / jurisdiction changed?
Operation still applicable?
Current environment acceptable?
Current policy version available?
Current risk or maintenance state changed?
```

The challenge preserves historical evidence. Current context determines whether the workflow may continue now. That distinction lets the system truthfully say both:

> The acknowledgment was valid.

and:

> The operation is no longer allowed.

---

## 15. Re-Evaluation Is Not Optional Because the Response Was Accepted

A valid acknowledgment can satisfy an acknowledgment requirement. It does not freeze policy. A continuation host can model the transition as:

```text
Trusted acknowledgment evidence
     +
current authoritative context
     |
     v
current policy evaluation
     |
     +--> Denied / Deferred / Escalation
     |         |
     |         v
     |     No execution
     |
     v
Allowed
     |
     v
compare current acknowledgment requirement
     |
     v
narrow continuation authority when still valid
```

### Requirement identity must survive re-evaluation

`RequirementCode` is not decoration. It identifies the acknowledgment requirement the original challenge was created to satisfy. Current policy can now produce three materially different states:

| Current policy state | Meaning for the old acknowledgment |
| --- | --- |
| Still requires the same `RequirementCode` | The accepted evidence may satisfy that current requirement if every other binding remains valid. |
| Requires a different acknowledgment code | The old evidence is historical but insufficient; issue a new challenge and perform no execution. |
| No longer requires acknowledgment | The old evidence remains historical lineage but is no longer a current policy gate. Current policy, not the old acknowledgment, determines whether execution may proceed. |

The companion sample models all three. When the current requirement changes, it returns `policy.acknowledgment-requirement-changed` and performs zero executor calls. When current policy withdraws the acknowledgment requirement entirely, the existing response remains in lineage but does not become the reason execution is allowed.

A workflow that has **not yet received a response** and discovers that current policy no longer requires acknowledgment is a different entry path. It should perform a fresh current-policy transition rather than fabricating acknowledgment evidence merely to satisfy this continuation handler.

### Required invariant

```text
Acknowledgment accepted
+ current policy now denies
     |
     v
Executor calls = 0
```

The original decision remains provenance. It is not rewritten to pretend the original policy denied the request.

---

## 16. Policy Drift and Resource Drift Need Separate Evidence

Two changes can occur while the workflow waits.

### Policy drift

```text
Original policy = 7.3
Current policy = 7.4
```

The current evaluator may produce a different result even if the resource is unchanged. Preserve both policy identities.

### Resource drift

```text
Resource version at challenge = snapshot-8
Current resource version = snapshot-9
```

The host must define whether that change:

- Invalidates the challenge outright.
- Requires fresh policy evaluation against the changed state.
- Is acceptable because the acknowledged intent still has the same meaning.
- Requires a new acknowledgment because the consequence presented to the responder changed materially.

The companion sample chooses **exact-snapshot continuation** for clarity: resource-version drift blocks continuation. That is a teaching choice, not a universal rule. The important rule is that drift is detected and handled explicitly.

---

## 17. Mint Continuation Authority Only After Current Validation

A safe order is:

```text
verify evidence trust
     |
     v
load continuation state + expected challenge
     |
     v
validate challenge / intent / presentation / responder / time
     |
     v
reconstruct current context
     |
     v
re-evaluate current policy
     |
     v
claim one continuation
     |
     v
mint narrow authority
     |
     v
host-owned executor
```

Do not mint execution authority immediately when System B records `Accepted = true`. That would convert acknowledgment into portable authorization. The continuation authority, when one is needed, should be bound to the current execution boundary and current validated facts. Typical bindings may include:

- Operation.
- Resource.
- Current resource version.
- Audience.
- Originating actor.
- Continuation identity.
- Challenge identity.
- Evidence identity.
- Acknowledgment requirement code.
- Presentation fingerprint or trusted presentation reference when required.
- Current policy identity/version.
- Expiration.
- Single-use or bounded-use semantics.

For cross-system capability semantics, continue with [Cross-System Capability Exchange and Delegated Authority](cross-system-capability-exchange-and-delegated-authority.md).

---

## 18. Orphaned Continuation State Needs a Reconciliation Policy

Distributed workflows accumulate partial state. Examples include:

```text
challenge created
but presentation service never receives it
```

```text
response accepted
but continuation service is unavailable
```

```text
continuation claimed
but executor outcome cannot be confirmed
```

```text
workflow expired
but a delayed response arrives later
```

These are not merely cleanup concerns. They determine whether later recovery may cause a duplicate action or manufacture authority from stale state. A reconciliation process should classify records rather than silently deleting them. Useful states may include:

- Pending.
- Expired.
- Cancelled.
- Superseded.
- Orphaned awaiting reconciliation.
- Claimed with unknown execution outcome.
- Completed.

Retention and recovery rules should preserve enough lineage to explain why a later retry was accepted or refused.

---

## 19. Partial Failure Must Not Broaden Authority

A distributed acknowledgment path has several failure points.

| Failure | Safe default for a consequential operation | Why |
| --- | --- | --- |
| Challenge state unavailable | Defer / no execution | Exact lineage cannot be established. |
| Evidence verifier unavailable | Defer / no execution | Required trust cannot be established. |
| Evidence explicitly untrusted | Reject / no execution | Negative trust evidence exists. |
| Current context unavailable | Defer / no execution | Current facts cannot be established. |
| Current policy unavailable | Follow explicit degraded-mode policy; do not infer Allow | Missing evaluation is not permission. |
| Continuation claim store unavailable | Defer / no execution when single-use state is required | Replay eligibility cannot be established. |
| Executor unavailable after claim | Record ambiguous/failed execution state; do not silently make the claim reusable | The external effect may be uncertain. |

The companion sample makes the post-claim rule executable. An executor rejection after a successful single-use claim leaves the challenge claimed; retry stops at `continuation.already-claimed` rather than restoring the old acknowledgment as reusable continuation authority. A production system may need reconciliation or compensation, but it should not infer from a failed or unknown execution result that the protected side effect definitely did not occur.

The exact public status may differ from the internal reason. The architectural invariant is:

> **Missing continuation evidence never manufactures continuation authority.**

### Required invariant

```text
Acknowledgment evidence unavailable
     |
     v
No manufactured authority
No execution
```

---

## 20. Preserve End-to-End Lineage Without Conflating IDs

A useful evidence chain might include:

```text
CorrelationId
  |
  +--> OriginatingDecisionId
  |
  +--> ChallengeId
  |
  +--> ResponseId
  |
  +--> EvidenceId
  |
  +--> ContinuationClaimId / AuthorityId
  |
  +--> CurrentDecisionId
  |
  +--> ExecutionId
```

Each identifier answers a different question. Do not use one ID for all of them merely to simplify logging. Preserve both historical and current policy evidence:

```text
OriginatingPolicyId
OriginatingPolicyVersion
CurrentPolicyId
CurrentPolicyVersion
```

The final execution record should also preserve the originating actor and responder identities when appropriate to the application's privacy and audit requirements.

### Minimize cross-system evidence

Lineage does not require copying every local policy input across every system. Prefer stable identifiers, reason codes, version labels, and bounded hashes where they are sufficient. Redact or avoid sensitive local policy facts that another domain does not need. Audit usefulness and data minimization are compatible goals.

---

## 21. A Minimal Recipient Pipeline

A conceptual continuation pipeline can be written as:

```text
1. Authenticate the presenting service/workload when applicable.
2. Parse the evidence envelope safely.
3. Resolve recipient-owned trust for the evidence issuer.
4. Verify evidence integrity/authenticity.
5. Load durable continuation state by continuation identity.
6. Verify the evidence names the continuation state's expected challenge and correlation.
7. Load the authoritative challenge record by that expected challenge identity.
8. Verify exact intent canonicalization version + digest.
9. Verify presentation version + fingerprint when required.
10. Verify responder binding and response semantics.
11. Verify response time and current challenge freshness.
12. Reconstruct current actor/resource/environment context.
13. Detect policy/resource drift.
14. Re-evaluate current policy and current acknowledgment requirement.
15. Atomically claim the continuation when bounded use is required.
16. Mint narrow current execution authority.
17. Build a validated local command.
18. Invoke the host-owned executor.
19. Preserve result and lineage evidence.
```

Different implementations may combine steps operationally. They should not erase the trust and authority distinctions. In particular, the continuation identity, expected challenge identity, correlation identity, intent binding, and presentation binding solve different substitution problems and should not be collapsed into one convenient identifier.

---

## 22. The Executor Should Not Receive Raw Acknowledgment Evidence

Prefer an executor boundary such as:

```csharp
public sealed record ValidatedContinuationCommand(
    string ExecutionId,
    string ContinuationAuthorityId,
    string ContinuationId,
    string OriginatingDecisionId,
    string ChallengeId,
    string EvidenceId,
    string OriginatingActorId,
    string ResponderId,
    string Operation,
    string ResourceId,
    string ExpectedResourceVersion,
    string AcknowledgmentRequirementCode,
    string? CurrentRequiredAcknowledgmentCode,
    string PresentationVersion,
    string PresentationDigest,
    string CurrentPolicyId,
    string CurrentPolicyVersion,
    string CorrelationId);
```

The raw response envelope should stop at the validation boundary. That keeps the executor from accidentally becoming responsible for:

- Verifying remote evidence formats.
- Interpreting challenge semantics.
- Replaying historical policy decisions.
- Deciding whether an acknowledgment is stale.

The executor should still enforce the local authority it receives. In the companion sample, the gateway passes both the `ScopedContinuationAuthority` and the validated command to the executor. The executor checks the authority audience, expiration, and command bindings before recording the simulated side effect:

```csharp
Task<ContinuationExecutionResult> ExecuteAsync(
    ScopedContinuationAuthority authority,
    ValidatedContinuationCommand command,
    CancellationToken cancellationToken);
```

That makes `Audience` and `ExpiresAtUtc` real execution-boundary constraints rather than metadata that is minted and then discarded. Constraints that must be atomic with a real external side effect, such as a live expected resource version or destination allowlist, still belong at the final data/tool boundary. The companion [Cross-System Capability Exchange sample](https://github.com/AsiBackbone/Learning/blob/main/samples/cross-system-capability-exchange/README.md) demonstrates that richer executor-TOCTOU boundary in more detail.

---

## 23. Companion Sample Invariants

The runnable companion intentionally keeps transport, durable infrastructure, and cryptography simulated. It makes the lifecycle invariants observable in one process and names the test that backs each documented claim.

| Boundary | Invariant | Companion sample |
| --- | --- | --- |
| Valid path | A trusted, current, correctly bound acknowledgment is re-evaluated before one execution. | ✅ `ValidAcknowledgmentReevaluatesAndExecutesExactlyOnce` |
| Intent binding | Valid evidence with a different intent digest produces zero executor calls. | ✅ `DifferentIntentDigestDoesNotContinue` |
| Canonicalization version | A response interpreted under a different intent-canonicalization version cannot satisfy the challenge. | ✅ `DifferentCanonicalizationVersionDoesNotContinue` |
| Presentation version | A response tied to a different presentation contract cannot satisfy the challenge. | ✅ `DifferentPresentationVersionDoesNotContinue` |
| Presentation fingerprint | Evidence tied to a different human-visible presentation fingerprint cannot continue. | ✅ `DifferentPresentationFingerprintDoesNotContinue` |
| Challenge identity | Evidence naming a different valid challenge cannot substitute for the challenge expected by durable continuation state. | ✅ `DifferentChallengeCannotSubstituteForContinuationState` |
| Continuation state | Missing durable continuation state performs no execution. | ✅ `MissingContinuationStateDoesNotContinue` |
| Correlation lineage | Evidence and continuation/challenge correlation must remain consistent. | ✅ `EvidenceCorrelationMismatchDoesNotContinue`; ✅ `ContinuationCorrelationMismatchDoesNotContinue` |
| Response semantics | A declined response cannot continue. | ✅ `DeclinedEvidenceDoesNotContinue` |
| Response window | A response occurring outside the challenge window cannot continue even if the current continuation attempt is otherwise timely. | ✅ `ResponseOutsideChallengeWindowDoesNotContinue` |
| Current expiration | A valid response cannot continue a challenge whose continuation window has expired. | ✅ `ExpiredChallengeDoesNotExecute` |
| Current context availability | If current authoritative context cannot be rebuilt, no continuation occurs. | ✅ `CurrentContextUnavailableDoesNotContinue` |
| Current actor binding | Current actor mismatch blocks continuation. | ✅ `CurrentActorMismatchDoesNotContinue` |
| Current intent binding | Current canonicalization-version or intent-digest drift blocks continuation. | ✅ `CurrentIntentCanonicalizationVersionMismatchDoesNotContinue`; ✅ `CurrentIntentDigestMismatchDoesNotContinue` |
| Current operation/resource binding | Current operation or resource substitution blocks continuation. | ✅ `CurrentOperationMismatchDoesNotContinue`; ✅ `CurrentResourceMismatchDoesNotContinue` |
| Current correlation binding | Current context must remain on the same correlation lineage. | ✅ `CurrentCorrelationMismatchDoesNotContinue` |
| Resource drift | The sample's exact-snapshot policy blocks changed resource state. | ✅ `ResourceDriftDoesNotExecuteInExactSnapshotSample` |
| Current policy | Accepted acknowledgment plus current policy denial produces zero executor calls. | ✅ `CurrentPolicyDenialAfterAcknowledgmentDoesNotExecute` |
| Requirement change | A new current acknowledgment requirement cannot be satisfied by evidence for the old requirement. | ✅ `ChangedAcknowledgmentRequirementNeedsNewChallenge` |
| Requirement withdrawal | When current policy no longer requires acknowledgment, the old response remains lineage rather than current authority. | ✅ `WithdrawnAcknowledgmentRequirementUsesCurrentPolicyDecision` |
| Presentation binding before requirement withdrawal | Withdrawing the current acknowledgment requirement does not rescue evidence bound to a different presentation. | ✅ `PresentationMismatchStillBlocksWhenCurrentPolicyWithdrawsRequirement` |
| Evidence availability | Verification unavailability does not manufacture continuation authority. | ✅ `EvidenceVerificationUnavailableDoesNotManufactureContinuationAuthority` |
| Recipient trust | Evidence from an untrusted issuer cannot continue the workflow. | ✅ `UntrustedEvidenceDoesNotContinue` |
| Responder binding | An unexpected responder cannot satisfy the challenge. | ✅ `WrongResponderDoesNotContinue` |
| Sequential replay | Replaying the accepted evidence creates no second authority or side effect. | ✅ `ReplayedAcknowledgmentDoesNotDuplicateAuthorityOrExecution` |
| Concurrent replay | Two actually concurrent claims for one single-use challenge produce one execution. | ✅ `TwoActuallyConcurrentContinuationClaimsProduceOneExecution` |
| Duplicate responses | A second accepted response for the same single-use challenge cannot create a second continuation. | ✅ `DuplicateAcceptedResponsesDoNotCreateSecondContinuation` |
| Out-of-order recovery | Evidence arriving before trusted challenge state performs no execution and can be retried after challenge recovery. | ✅ `EvidenceBeforeChallengeCanBeRetriedAfterChallengeRecovery` |
| Authority audience | The executor rejects continuation authority minted for an unexpected audience. | ✅ `ExecutorRejectsUnexpectedContinuationAuthorityAudience` |
| Authority lifetime | The executor rejects expired continuation authority before the simulated side effect. | ✅ `ExecutorRejectsExpiredContinuationAuthorityAndClaimStaysConsumed` |
| Post-claim executor rejection | Executor rejection after a successful claim does not restore the single-use continuation; retry is rejected before another executor call. | ✅ `ExecutorRejectsExpiredContinuationAuthorityAndClaimStaysConsumed` |
| Authority-to-command binding | A validated command cannot be substituted after authority minting without executor rejection. | ✅ `ExecutorRejectsCommandThatDoesNotMatchContinuationAuthority` |
| Lineage + authority binding | Successful execution keeps continuation, policy, challenge, evidence, requirement, presentation, authority, execution, and correlation identities distinct while preserving the authority-to-command bindings. | ✅ `SuccessfulExecutionPreservesDistributedLineage` |

### Intentionally not modeled

| Area | Scope |
| --- | --- |
| Presenter/workload authentication | ◐ The sample begins after any service/workload authentication boundary. It models evidence-issuer trust, not presenter/channel authentication or cryptographic workload binding. |
| Real messaging | ◐ No broker, queue, webhook, or service-to-service network is required. |
| Production evidence cryptography | ◐ Trust verification is simulated; no real signing keys or credentials exist. |
| Distributed atomic storage | ◐ The continuation and claim stores are process-local and do not claim cross-replica guarantees. |
| Current-policy provider outage | ◐ The sample models current Allow/Deny and requirement changes, but not an unavailable policy evaluator. |
| Durable workflow recovery | ◐ Orphan/reconciliation states are explained but not backed by a durable workflow engine. |
| Superseded continuation lifecycle | ◐ The article names `Superseded` as a useful state, but the sample keeps only the active continuation record and does not implement supersession transitions. |
| Exactly-once external side effects | ◐ The executor is a dry-run recorder only. |
| Production executor TOCTOU | ◐ The sample validates local continuation-authority audience/lifetime/bindings, but no live external resource or destination is mutated. |

[Run the Distributed Acknowledgment and Continuation sample](https://github.com/AsiBackbone/Learning/blob/main/samples/distributed-acknowledgment-continuation/README.md).

---

## 24. Prefer Synchronous Confirmation When Distribution Does Not Earn Its Cost

A distributed acknowledgment workflow adds:

- Durable state.
- Cross-system trust.
- Expiration handling.
- Replay protection.
- Recovery logic.
- More evidence correlation.
- Partial-failure modes.
- Operational reconciliation.

Do not add those costs merely because an acknowledgment can technically be asynchronous. Prefer an ordinary synchronous confirmation when:

- One host owns the request and execution.
- The interaction completes within the request/session lifecycle.
- Current context can be reconstructed immediately.
- No durable resume is required.
- No separate system owns the response interaction.
- A conventional confirmation plus normal authorization adequately expresses the requirement.

A simple host-owned flow may be better:

```text
current request
     |
     v
current policy requires confirmation
     |
     v
bound confirmation response
     |
     v
current policy/context validation
     |
     v
host-owned execution
```

Distributed continuation is not a maturity upgrade. It is justified only when the workflow genuinely crosses time, process, ownership, or trust boundaries.

---

## 25. Related Learning

Continue with:

- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) for the foundational acknowledgment lifecycle.
- [Human-in-the-Loop Governance Workflows](../governance/human-in-the-loop-governance-workflows.md) when an independent reviewer disposition, rather than acknowledgment, is the requirement.
- [Human Acknowledgment Workflow](../case-studies/human-acknowledgment-workflow.md) for the detailed single-system persistence, race, evidence, and changed-state case study.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) for narrow continuation authority and executor ownership.
- [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) for preserving original and current policy identity without rewriting history.
- [Replay Protection and Bounded-Use Authority](../security/replay-protection-and-bounded-use.md) for atomic use-state and replay semantics.
- [Cross-System Capability Exchange and Delegated Authority](cross-system-capability-exchange-and-delegated-authority.md) for authority that crosses independently operated system boundaries after current governance permits continuation.
- [Safe Degraded Mode and Fail-Safe Governance](../labs/safe-degraded-mode-and-fail-safe-governance.md) for reasoning about unavailable trust and state dependencies.

---

## 26. Check Your Understanding

You should be able to explain why:

- A trusted acknowledgment response can still produce zero executor calls.
- A challenge identifier, an intent digest, and a presentation fingerprint solve different binding problems.
- Intent binding alone cannot prove that the responder saw a faithful representation of that intent.
- Duplicate delivery and malicious replay can require the same atomic continuation guard.
- An acknowledgment accepted under policy version 7.3 may remain historically valid while policy 7.4 blocks continuation, changes the requirement, or withdraws the requirement.
- Missing acknowledgment evidence is not equivalent to denial, but still must not become permission.
- A distributed acknowledgment workflow should be replaced with synchronous confirmation when no durable cross-boundary continuation requirement exists.

---

## 27. Closing Principle

```text
Acknowledgment evidence received
        |
        v
Trust + challenge + intent + presentation + freshness validated
        |
        v
Current context rebuilt
        |
        v
Current policy re-evaluated
        |
        v
One continuation claimed
        |
        v
Narrow host-owned execution authority
```

If any required step cannot establish the authority needed for consequential continuation:

```text
No valid current continuation authority
        |
        v
No protected execution
```

> **Read it. Run it. Question it. Improve it.**
