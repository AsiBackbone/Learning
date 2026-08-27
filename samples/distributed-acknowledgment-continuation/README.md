# Distributed Acknowledgment and Continuation

This sample is the executable companion to [Distributed Acknowledgment and Continuation Workflows](../../docs/advanced/distributed-acknowledgment-and-continuation-workflows.md).

It keeps the distributed workflow deliberately small and local so the trust and continuation boundaries remain visible.

> **Acknowledgment evidence is not portable execution authority.**

## Scenario

The sample models three fictional roles:

```text
System A
policy decision + bound challenge
        |
        v
System B
acknowledgment response evidence
        |
        v
System C
recipient trust validation
        |
        v
durable continuation-state binding
        |
        v
current-context reconstruction
        |
        v
current policy re-evaluation
        |
        v
single continuation claim
        |
        v
narrow local authority
        |
        v
host-owned dry-run executor
```

The fictional operation is `accounts.bulk-suspend`.

No real account system, identity provider, message broker, signing service, or external policy engine is contacted.

## What the Sample Makes Executable

The gateway keeps these artifacts separate:

- The original policy decision.
- The acknowledgment challenge.
- Acknowledgment evidence from the response system.
- Durable continuation state naming the expected challenge.
- Current System C context.
- Current System C policy decision.
- A single-use continuation claim.
- Narrow continuation authority.
- A validated local executor command.

The raw acknowledgment evidence never reaches the executor.

## Machine Intent and Human Presentation Binding

The challenge carries an `IntentCanonicalizationVersion` and `IntentDigest`. The evidence repeats both, so a digest produced under a different canonicalization contract cannot accidentally satisfy the challenge.

That only protects the machine path. It does not prove that the responder saw a faithful representation of the intent.

The sample therefore also carries a fictional `PresentationVersion` and `PresentationDigest` in both the challenge and evidence. The teaching assumption is that System A defined a deterministic presentation from the canonical intent and System B presented that exact versioned representation. A presentation-fingerprint mismatch blocks continuation.

A production design could instead preserve the exact rendered presentation or a trusted reference to it and let System C verify that record. The sample does not claim that a fingerprint proves the responder read or understood the presentation.

## Challenge and Continuation-State Binding

A `ContinuationRequest` names a durable `ContinuationId`. System C loads `ContinuationState` and obtains the challenge identity that continuation is actually waiting on.

Only then can evidence be accepted for that challenge:

```text
ContinuationId
        |
        v
ContinuationState.ExpectedChallengeId
        |
        +-- must match --> Evidence.ChallengeId
        |
        v
Authoritative challenge record
```

This prevents evidence for another valid challenge from being substituted merely because it has a related correlation identifier.

The in-memory continuation-state store is a teaching implementation only; production workflows need durable storage appropriate to their recovery model.

## Recipient-Owned Evidence Trust

`IAcknowledgmentEvidenceVerifier` is the recipient-side trust seam.

The teaching verifier can produce:

```text
Trusted
Untrusted
Unavailable
```

`Unavailable` does not mean `Denied`, but it also does not permit continuation. The gateway returns `evidence.verification-unavailable` and performs no execution.

No real cryptographic proof is used.

## Current Context, Policy, and Requirement Identity

`ICurrentContinuationContextProvider` rebuilds current System C facts instead of trusting the old challenge as current state.

`ICurrentPolicyEvaluator` then produces a new policy decision.

The original challenge preserves policy `7.3`; the sample's successful current evaluation uses policy `7.4`. Both survive into the validated command so history is not rewritten.

`RequirementCode` is also current-policy relevant:

- If current policy still requires the same code, the accepted evidence may satisfy that requirement after all other checks pass.
- If current policy requires a different code, the gateway returns `policy.acknowledgment-requirement-changed` and a new challenge is required.
- If current policy no longer requires acknowledgment, the accepted response remains historical lineage but is not the source of current authority.

The sample deliberately chooses **exact-snapshot continuation**: if the current resource version differs from the version recorded at challenge time, continuation is blocked. The article explains other legitimate drift policies.

## Executor-Side Continuation Authority Validation

After current validation and the single-use claim, the gateway mints a `ScopedContinuationAuthority` with a one-minute teaching lifetime. The executor receives that authority alongside the validated command and checks:

- Audience.
- Expiration.
- Continuation identity.
- Operation and resource bindings.
- Challenge and evidence identity.
- Current policy identity/version.

Only then does the recording executor increment its invocation count. This keeps `Audience` and `ExpiresAtUtc` from becoming decorative fields. Because the continuation claim is taken before execution, an executor rejection leaves that single-use claim consumed; retry returns `continuation.already-claimed` rather than silently restoring reusable authority. The object is still a local teaching model, not a portable token or production cryptographic grant.

## Duplicate Delivery and Replay

`IContinuationClaimStore` is keyed at the challenge lifecycle, not only by response ID.

That matters because two different accepted response records could otherwise continue the same single-use challenge.

The in-memory implementation performs the claim under one process-local lock:

```text
challenge unclaimed
        |
        v
TryClaim(challengeId, evidenceId)
        |
        +--> first claimant succeeds
        |
        +--> replay / duplicate fails
```

This is a teaching boundary only. Production multi-instance systems need atomic state whose consistency scope matches the required replay guarantee. The test suite includes an actually concurrent two-caller race; the sequential replay tests do not stand in for that proof.

## Out-of-Order Recovery

When continuation state exists but the trusted challenge record has not arrived yet, the sample returns:

```text
challenge.not-found
```

No continuation is claimed and no executor runs.

After the challenge record becomes available, the same evidence can be retried. This is safe because the gateway looks up authoritative challenge state and never creates challenge state from acknowledgment evidence. A missing challenge therefore fails before the single-use continuation claim.

## Run the Sample

From the repository root:

```bash
dotnet run --project samples/distributed-acknowledgment-continuation/DistributedAcknowledgmentContinuation/DistributedAcknowledgmentContinuation.csproj
```

## Run the Tests

```bash
dotnet test samples/distributed-acknowledgment-continuation/DistributedAcknowledgmentContinuation.Tests/DistributedAcknowledgmentContinuation.Tests.csproj
```

The focused tests prove:

- A valid acknowledgment is re-evaluated before exactly one execution.
- Intent digest and intent-canonicalization version are independently bound.
- Presentation version and presentation fingerprint are independently bound.
- A valid challenge cannot substitute for the challenge expected by continuation state.
- Missing continuation state and inconsistent continuation/evidence correlation perform no execution.
- Declined evidence performs no continuation.
- A response outside the original challenge window is rejected independently of current challenge expiration.
- An expired challenge produces zero executor calls.
- Current-context unavailability performs no continuation.
- Current actor, intent version, intent digest, operation, resource, and correlation mismatches are rejected.
- Resource drift is rejected by the sample's exact-snapshot rule.
- Current policy denial after accepted acknowledgment produces zero executor calls.
- A changed current acknowledgment requirement requires a new challenge.
- Withdrawal of the acknowledgment requirement leaves the old response as lineage while current policy owns the allow decision.
- A presentation mismatch still blocks continuation even when current policy has withdrawn the acknowledgment requirement.
- Evidence-verification unavailability does not manufacture authority, while explicitly untrusted evidence is rejected.
- A wrong responder cannot satisfy the challenge.
- Sequential replay produces no second authority or side effect.
- Two actually concurrent claims for one single-use challenge produce exactly one execution.
- Two different accepted response records cannot continue one single-use challenge twice.
- Evidence arriving before authoritative challenge state can be retried after challenge recovery without an earlier claim or side effect.
- The public rejection result stays coarse even when the internal reason is specific.
- The executor rejects an unexpected continuation-authority audience.
- The executor rejects expired continuation authority before the simulated side effect, leaves the single-use claim consumed, and rejects retry before another executor call.
- The executor rejects a validated command that no longer matches the minted continuation authority.
- Successful execution preserves distinct continuation, original/current policy, challenge, evidence, requirement, presentation, actor, responder, authority, execution, and correlation lineage.

## What This Sample Does Not Prove

The sample does not provide:

- Presenter/workload authentication or channel binding.
- Real service-to-service authentication.
- Production evidence signing or verification.
- A message broker or delivery guarantee.
- Durable cross-process challenge or continuation-state storage.
- Distributed atomic continuation claims.
- Production supersession, workflow recovery, or reconciliation state transitions.
- Exactly-once external side effects.
- A production human-presentation attestation mechanism.
- A legal or compliance definition of acknowledgment.
- A production bulk-account operation.

It exists to make the distributed continuation boundary observable without hiding it beneath infrastructure.

---

> **Read it. Run it. Question it. Improve it.**
