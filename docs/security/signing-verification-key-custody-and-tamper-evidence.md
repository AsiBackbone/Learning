# Signing, Verification, Key Custody, and Tamper Evidence

**Pattern classification:** General learning material

**Difficulty:** Advanced

**Prerequisites:** [Trust Boundaries and Least Privilege](trust-boundaries-and-least-privilege.md), [Replay Protection and Bounded-Use Authority](replay-protection-and-bounded-use.md), and [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md). Familiarity with [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) and [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) is helpful.

**Learning objective:** Distinguish content fingerprints from digital signatures, separate signing from verification and authorization, place signing and verification at explicit trust boundaries, reason about key ownership and rotation, and explain what signed or tamper-evident evidence can and cannot prove.

## Pattern Card

> **Problem:** A governance artifact may preserve useful provenance while still giving another component no cryptographic basis for deciding whether the artifact was altered, whether it came from an expected signing authority, or whether the signer was authorized for the artifact's purpose.
>
> **Pattern:** Canonicalize the artifact that matters, create a cryptographic proof through a narrowly owned signing authority, preserve enough metadata for later verification, verify at the consuming trust boundary against configured trust anchors and key-lifecycle policy, then perform semantic, policy, authority, freshness, and replay checks separately.
>
> **Use when:** Decisions, acknowledgments, capabilities, audit records, outbox records, policy artifacts, or other consequential evidence cross trust boundaries or must remain interpretable after time, deployment, or key rotation.
>
> **Prefer something simpler when:** The operation is low consequence, all processing stays inside one sufficiently trusted boundary, ordinary authorization and database controls meet the threat model, and no cryptographic authenticity or long-lived integrity claim is needed.
>
> **Observe:** Modified content fails verification; the wrong signing authority is rejected even when its signature is cryptographically valid; retired keys can remain available for historical verification when policy permits; revoked or unknown keys do not silently become trusted; verification failure does not become execution permission; and a valid signature never bypasses current semantic or execution checks.

The central boundary is:

```text
Hash
   ≠
Signature
   ≠
Trusted storage
   ≠
Authorization
   ≠
Safe execution
```

Those controls can reinforce one another.

They should not be described as interchangeable.

---

## Why This Topic Needs Its Own Trust Boundary

Earlier Learning material already preserves facts such as:

```text
DecisionId
CorrelationId
PolicyVersion
PolicyFingerprint
Actor
Operation
Resource
Outcome
ReasonCodes
OccurredUtc
```

Those fields improve provenance.

They do not automatically answer:

```text
Did these bytes change after the record was created?
```

or:

```text
Did an expected signing authority create this proof?
```

or:

```text
Was that signing authority authorized for this artifact type and purpose?
```

or:

```text
Is the artifact still acceptable now?
```

or:

```text
May the requested action execute?
```

A mature trust architecture keeps those questions separate so that each answer can be tested, logged, and governed explicitly.

## A Useful Evidence Ladder

Think of evidence as accumulating properties rather than jumping directly from "stored" to "trusted."

| Layer | What it can help establish | What it does not establish by itself |
| --- | --- | --- |
| Structured record | What fields the application chose to preserve | Integrity, authorship, authorization, immutability |
| Canonical representation | Which deterministic bytes represent the artifact | Who approved or signed the artifact |
| Hash / fingerprint | Whether recomputed canonical bytes match a recorded digest | Who created the digest; whether digest and artifact were both replaced |
| Digital signature | That a signature verifies for particular bytes under particular verification material | That the key holder was authorized for this purpose; that current policy permits use |
| Verification policy | Whether the signature, key, purpose, provider, algorithm, and lifecycle state are acceptable to this verifier | That the artifact's semantics are correct; that execution is safe |
| Durable append / integrity chain | Additional evidence that records were modified, reordered, inserted, or removed under a defined model | Perfect prevention, authorship, or detection of every truncation scenario |
| External checkpoint / protected anchor | Stronger resistance to rewriting an entire local history without detection | Semantic correctness, legal status, or automatic compliance |

The important design habit is:

> **State the property that each layer actually provides.**

Avoid using one strong word such as `trusted`, `immutable`, or `tamper-proof` to collapse several different guarantees into one claim.

---

## Start With the Threat Model

Cryptographic controls are useful only relative to a threat model.

Possible threats include:

- A caller modifies a capability after issuance.
- A queue consumer receives an artifact from an untrusted producer.
- A database administrator can edit an audit row.
- An application instance is compromised and attempts to forge decision evidence.
- A valid artifact is copied and replayed.
- A service signs an artifact with a key that is valid cryptographically but not authorized for that artifact type.
- A key is retired normally and old records still need verification.
- A key is compromised and historical artifacts require incident review.
- Canonical serialization changes after a deployment and old signatures stop verifying.
- An attacker changes both a stored payload and its stored hash.
- An attacker truncates the newest records from an otherwise valid hash chain.
- A verification provider becomes unavailable during a consequential operation.

Different threats require different controls.

For example:

```text
Threat: payload modified in transit
        ↓
Signature verification can help detect modification
```

but:

```text
Threat: valid signed capability replayed twice
        ↓
Signature verification alone does not help
        ↓
Replay/use-state control is still required
```

Likewise:

```text
Threat: authorized signer emits unsafe policy
        ↓
Signature can still be perfectly valid
        ↓
Semantic and governance review are still required
```

Cryptography narrows particular trust questions.

It does not erase the rest of the architecture.

---

## Hashing and Digital Signatures Answer Different Questions

A cryptographic hash function accepts bytes and produces a fixed-size digest.

Conceptually:

```text
Canonical bytes
      ↓
Hash function
      ↓
Digest
```

Anyone who has the bytes can normally recompute the digest.

That is useful for content identity.

Suppose a policy fingerprint is:

```text
sha256:2d4c...
```

A later component can canonicalize the policy again and compare the result.

If the digest differs, the bytes differ under that canonicalization rule.

If the digest matches, the component has evidence of content equality under that hash and representation.

The hash alone does not identify an author or authority.

### A Stored Hash Can Be Rewritten With the Record

Consider a mutable row:

```text
Payload = original bytes
Hash    = hash(original bytes)
```

An actor with sufficient write access may replace both:

```text
Payload = modified bytes
Hash    = hash(modified bytes)
```

The new pair is internally consistent.

That means:

> **A hash is not automatically a trust anchor.**

The verifier needs an independently trusted reference, signature, chain, checkpoint, protected store, or another integrity mechanism appropriate to the threat model.

### A Digital Signature Adds a Key-Based Claim

A digital signature normally introduces asymmetric key material:

```text
Private signing key
        ↓
Sign canonical artifact hash / bytes
        ↓
Signature

Public verification material
        ↓
Verify signature
```

Successful verification can provide evidence that:

1. The signed content matches the content presented for verification.
2. The signature was created using the private key corresponding to the trusted verification material, assuming the cryptographic primitive and key have not been compromised.

That is stronger than a bare hash.

It is still not the same as:

```text
The signer was authorized for this purpose.
```

Authorization of the signing authority comes from verifier configuration, key ownership, governance policy, certificate/key metadata, provider policy, and operational controls.

---

## Integrity and Authenticity Are Related but Distinct

The terms are often used loosely.

For this tutorial:

### Integrity

Integrity asks whether the artifact being evaluated is the same artifact that was protected by the integrity mechanism.

A hash comparison can provide one form of integrity evidence when the expected digest is itself trusted.

A valid digital signature also provides integrity evidence for the signed bytes because modification should invalidate the signature.

### Authenticity

Authenticity asks whether the artifact can be associated with an expected signing authority.

A valid signature supports authenticity only when the verifier trusts the corresponding key relationship for that purpose.

For example:

```text
Signature valid under key X
        +
Verifier policy says key X may sign capability grants
        ↓
Cryptographic authenticity evidence for this capability purpose
```

Without the second condition, the verifier has only learned that some trusted or known key signed the artifact.

It has not necessarily learned that the key was permitted to sign this kind of artifact.

### Authenticity Is Not Semantic Correctness

A perfectly authentic artifact can still be wrong.

Examples:

- A signed policy contains an unsafe rule.
- A signed capability grants the wrong resource.
- A signed decision used stale context.
- A signed acknowledgment refers to the wrong challenge.
- A signed audit record accurately records a bad execution.

Therefore:

```text
Cryptographically authentic
        ≠
Semantically correct
        ≠
Governance-approved
```

---

## Policy Fingerprints and Signed Policy Evidence

[Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) distinguishes a policy fingerprint from a trust proof.

That boundary can now be extended.

### Fingerprinted Policy Evidence

```text
PolicyId: customer-export
PolicyVersion: 4.2
PolicyFingerprint: sha256:...
```

This can answer:

> Which canonical policy content was associated with this decision?

### Signed Policy Evidence

A signature can add:

```text
SigningKeyId: policy-release-key
SigningKeyVersion: 2026-08
SignatureAlgorithm: provider-defined
Signature: ...
```

That can answer a stronger question after verification:

> Did an accepted signing authority create a valid signature over this canonical policy evidence?

It still does not automatically prove:

- That the policy was approved through the correct human process.
- That the policy was deployed to the environment.
- That the evaluator actually used that policy.
- That the policy was lawful or safe.
- That the decision used current context.

Signed policy evidence is one stronger provenance layer.

It is not a substitute for deployment evidence, runtime evidence, or governance review.

---

## Canonical Representation Comes Before Signing

Cryptographic functions operate on bytes.

Applications reason about structured meaning.

A signing design must define how meaning becomes stable bytes.

Consider these JSON objects:

```json
{"actor":"admin-42","operation":"account.disable"}
```

and:

```json
{"operation":"account.disable","actor":"admin-42"}
```

An application may treat them as logically equivalent.

A raw byte comparison does not.

Likewise, the following can change bytes:

- Property ordering.
- Whitespace.
- Text encoding.
- Line endings.
- Timestamp formatting.
- Unicode normalization.
- Numeric formatting.
- Null versus omitted properties.
- Collection ordering.
- Dictionary ordering.
- Default values.
- Diagnostic metadata.

A signing architecture therefore needs an explicit canonicalization contract.

A conceptual contract might define:

```text
Encoding = UTF-8
Object property order = ordinal
Timestamp format = UTC / stable round-trip format
Null handling = explicit
Unordered collections = sorted by defined rule
Metadata = explicit allow-list
CanonicalizationVersion = 1
```

The exact rules are application-specific.

The important requirement is stability.

### Canonicalization Version Is Evidence

If the canonicalization rules evolve, preserve the version used for the signature.

For example:

```text
CanonicalizationVersion: 1
PayloadSchemaVersion: 3
```

A verifier years later may need both values to reconstruct the exact signable representation.

Without them, historical verification can become ambiguous after serializers or schemas change.

### Sign the Artifact Type and Purpose Too

Do not sign only the visible business fields if the same bytes could be interpreted as another artifact type.

A canonical envelope can include domain-separation fields:

```text
ArtifactType
ArtifactId
PayloadSchemaVersion
CanonicalizationVersion
Purpose
Content
```

That reduces the chance that a signature valid for one artifact class is reused as proof for another class with a compatible byte shape.

For example:

```text
Signed as: acknowledgment-challenge
```

should not automatically be accepted as:

```text
capability-grant
```

merely because some content fields happen to match.

---

## Sign the Fields That Carry the Claim

A signature protects only what is included in the signed representation.

Suppose a capability contains:

```text
Actor
Operation
Resource
Audience
ExpiresUtc
PolicyVersion
```

but the canonical signed representation omits `Resource`.

A later actor might change the resource without invalidating the signature.

That is a signing-boundary failure.

The reverse problem also exists.

If every volatile diagnostic field is included, harmless logging metadata may break verification or force unnecessary re-signing.

A useful rule is:

> **Include fields that materially define the artifact's identity, authority, policy, scope, and evidence claim. Exclude unrelated volatile metadata by explicit rule.**

Do not discover the signing boundary accidentally from whatever serializer happened to be convenient.

---

## Signing Authority and Execution Authority Should Remain Separate

A signing key is authority.

Whoever can use that key can create artifacts that verifiers may accept as authentic under that key.

That authority should be scoped deliberately.

A useful separation is:

```text
Policy / decision component
        ↓
Creates signable artifact
        ↓
Authorized signing boundary
        ↓
Signed artifact
        ↓
Consumer / execution boundary
        ↓
Verification + current policy checks
        ↓
Possible execution
```

The signer does not need broad execution authority merely because it can sign.

The executor does not need the private signing key merely because it verifies.

### Why Separation Helps

Separation can reduce the impact of compromise.

If an execution host is compromised but cannot sign new policy evidence, it has fewer ways to forge upstream provenance.

If a signer is compromised but cannot directly execute, downstream verification and current policy checks can still provide additional containment.

No separation is perfect.

But combining all authority into one process creates a larger trust domain:

```text
Evaluate
Sign
Verify
Execute
Store
Administer keys
```

A single compromise may then control the entire evidence story.

### Separation of Duties Is Not Only an Organizational Concept

It can be expressed technically through:

- Different service identities.
- Different key permissions.
- Signing-only provider permissions.
- Verification-only access to public material.
- Separate deployment roles.
- Separate administrative permissions.
- Narrow network paths.
- Audited key-use operations.

The correct amount of separation depends on consequence and operational complexity.

---

## Asymmetric Signing Is Often Useful Across Trust Boundaries

This tutorial does not prescribe one signature algorithm or key provider.

But asymmetric signing has a useful architectural property:

```text
Signer holds private capability
Verifier holds public verification material
```

A verifier can validate without receiving the signing secret.

That supports separation between:

```text
May verify
```

and:

```text
May forge a new valid signature
```

### Shared-Secret MACs Have Different Trust Semantics

A message authentication code such as an HMAC can provide integrity and authenticity within a shared-secret trust domain.

But every verifier that holds the shared secret may also be able to create valid MACs.

That means:

```text
Can verify
≈
Can sign/forge within the shared-secret domain
```

This can be entirely appropriate inside some systems.

It is less useful when the architecture needs strong separation between signing authority and verification authority.

Choose the primitive based on the trust model, not because one mechanism sounds more sophisticated.

### Do Not Hand-Roll Cryptographic Protocols

The examples in this tutorial stop at provider-neutral boundaries intentionally.

Production systems should use established cryptographic libraries and managed/provider implementations appropriate to the platform and threat model.

Avoid inventing custom signature encodings, key derivation, certificate validation, or cryptographic algorithms inside ordinary application code.

---

## Verification Requires a Trust Anchor

A signature is only meaningful relative to verification material that the verifier accepts.

The verifier needs a reason to trust something such as:

- A pinned public key.
- A certificate chain anchored in an accepted root.
- A managed-key identifier and provider relationship.
- A key registry maintained by the host.
- A provider-specific verification operation.
- A trusted key ID/version allow-list.

This configured trust relationship is the trust anchor.

A dangerous verifier says:

```text
Signature verifies under some key supplied with the artifact
        ↓
Accept
```

An attacker can create a new key pair, sign the artifact, and supply the matching public key.

The cryptography works.

The trust model fails.

A stronger verifier says:

```text
Artifact says key = capability-signing-key / v3
        ↓
Verifier resolves that reference through trusted configuration
        ↓
Verifier checks purpose/provider/algorithm/key policy
        ↓
Cryptographic verification
```

The artifact can carry a key identifier.

The artifact should not be able to define its own trust root without policy.

---

## Trust Pinning Narrows Otherwise Valid Signatures

A host may trust several signing authorities globally but only one for a specific path.

For example:

```text
Trusted organization keys:
- audit-key
- deployment-key
- capability-key
```

A robotics gateway should not accept a valid audit signature as capability authority merely because both keys are trusted somewhere in the organization.

The gateway can pin expectations:

```text
Expected artifact type = capability-grant
Expected key ID = capability-key
Expected provider = managed-key-provider
Expected algorithm = approved algorithm set
Expected policy version/hash = current accepted values
```

Then:

```text
Cryptographically valid under audit-key
        ↓
Trust policy mismatch
        ↓
Reject / escalate
```

This is an important boundary:

> **A valid signature from the wrong authority is still the wrong authority.**

---

## Key Ownership Is Part of the Security Model

A system should be able to answer:

1. Which component is allowed to request signatures?
2. Which identity is authorized to use the signing key?
3. Which artifact purposes may that identity sign?
4. Can the key material be exported?
5. Who can rotate or disable the key?
6. Who can change verifier trust configuration?
7. Who can read historical verification material?
8. How is key use audited?
9. What happens if the signing provider is unavailable?
10. What happens if the key is suspected of compromise?

If those answers are undefined, the cryptographic primitive may be strong while key custody remains weak.

### Avoid Keys in Source or Ordinary Configuration

Dangerous examples include:

```text
PrivateKey = "..." in appsettings.json
```

or:

```text
Private key committed to repository
```

or:

```text
Long-lived signing secret copied into every application instance
```

These designs increase exposure through:

- Source control.
- Configuration backups.
- Diagnostic dumps.
- Developer workstations.
- Build logs.
- Container images.
- Environment-variable inspection.
- Broad application compromise.

A stronger production model commonly keeps private signing operations behind a managed key store, HSM-backed service, platform key container, or another protected provider boundary.

The application requests a signing operation.

It does not necessarily retrieve the private key bytes.

### Managed Key Storage Is Not Magic

Using a managed key service does not automatically solve:

- Excessive IAM permissions.
- Weak service identity.
- Unreviewed key-administration roles.
- Missing rotation procedures.
- Missing revocation procedures.
- Poor logging.
- Incorrect trust anchors.
- Inadequate provider availability planning.
- Unsafe fail-open behavior.

Managed custody changes the key-exposure model.

It does not replace security architecture.

---

## Preserve Key and Algorithm Metadata

Historical verification requires enough information to resolve the original signing context.

A signed artifact commonly needs metadata such as:

```text
SigningHash
HashAlgorithm
SignatureValue
SignatureAlgorithm
KeyId
KeyVersion
ProviderDescriptor
SignedUtc
CanonicalizationVersion
PayloadSchemaVersion
ArtifactType
ArtifactId
```

Not every platform uses exactly these fields.

The important properties are:

- The verifier can identify the expected artifact.
- The verifier can recompute the correct canonical hash.
- The verifier can resolve the exact signing key version.
- The verifier can apply current algorithm policy.
- The verifier can distinguish signing-time metadata from later verification results.

### Key IDs Are References, Not Secrets

A key identifier such as:

```text
capability-signing-key
```

is normally safe metadata.

It should identify the logical signing authority without exposing private key material.

### Preserve the Exact Key Version

If a provider rotates:

```text
capability-signing-key / v3
        ↓
capability-signing-key / v4
```

an artifact signed under `v3` should keep:

```text
KeyId = capability-signing-key
KeyVersion = v3
```

Do not rewrite historical metadata to `v4` merely because `v4` is current.

That would destroy the evidence needed to verify the original signature.

### Algorithm Metadata Is Not Algorithm Permission

An artifact may state:

```text
SignatureAlgorithm = ...
HashAlgorithm = ...
```

The verifier should still apply an allow-list or provider policy.

Do not let an untrusted artifact downgrade the verifier merely by naming an algorithm the application can technically parse.

---

## Sign at Artifact Creation, Verify at the Consuming Boundary

A useful lifecycle is:

```text
Artifact created
      ↓
Canonical representation built
      ↓
Canonical hash computed
      ↓
Authorized signing boundary signs
      ↓
Artifact + signing metadata stored / transmitted
      ↓
Trust boundary crossed
      ↓
Consumer rebuilds canonical representation
      ↓
Consumer recomputes expected hash
      ↓
Consumer resolves trusted key / key lifecycle state
      ↓
Signature verification
      ↓
Verifier trust-policy decision
      ↓
Semantic / policy / authority / freshness / replay checks
      ↓
Possible continuation
```

This order avoids two common mistakes.

First:

```text
Trust a stored hash without recomputing from the artifact
```

Second:

```text
Signature valid
      ↓
Execute automatically
```

### The Consumer Should Recompute What It Can

If an artifact carries:

```text
SigningHash = abc...
```

verification should not merely ask whether the signature is valid over `abc...`.

The consumer should first compute:

```text
ExpectedHash = Hash(Canonicalize(received artifact))
```

then compare:

```text
ExpectedHash == SigningHash
```

before or as part of cryptographic verification.

Otherwise a valid signature may be detached from the actual artifact being consumed.

---

## Signing Is Not Verification

An artifact can be in several useful states.

```text
Unsigned
Signing-ready
Signed
Verification attempted
Verified
Policy accepted
```

Those states should not be collapsed.

### Signed

A signing provider returned signature metadata.

### Verified

A verifier checked the expected canonical hash and signature using trusted verification material.

### Policy Accepted

The host accepted the verification result for this purpose and context.

A system may legitimately store a signed artifact without verifying it immediately.

For example, verification may happen at:

- A downstream execution gateway.
- A governance emission boundary.
- An audit-review process.
- A cross-region consumer.
- An archival validation job.

The architecture should make that timing explicit.

Do not use `IsSigned` as a synonym for `IsTrusted`.

---

## A Valid Signature Is Not Complete Authorization

This is the most important execution boundary in the tutorial.

Preferred flow:

```text
Signature valid
      ↓
Expected signer / key / purpose valid
      ↓
Current actor and resource binding valid
      ↓
Current policy / freshness valid
      ↓
Expiration valid
      ↓
Replay / use state valid
      ↓
Current resource state valid
      ↓
Host may execute
```

Unsafe flow:

```text
Signature valid
      ↓
Execute
```

A signature does not answer:

- Is the actor still authorized?
- Is the resource still present?
- Is the capability expired?
- Has the capability already been consumed?
- Has current policy changed?
- Was the signer authorized for this artifact type?
- Is the action still within scope?
- Is the destination still allowed?
- Is the environment in a safe state?

This is why signing belongs inside a governed execution architecture rather than replacing it.

---

## Verification Results Should Be Explicit

A verifier should avoid returning only:

```text
true / false
```

Operational systems often need to distinguish failure categories.

Example categories include:

```text
Valid
MissingSignature
InvalidSignature
HashMismatch
UnknownKey
UnknownKeyVersion
RevokedKey
ProviderUnavailable
CanonicalizationMismatch
UnsupportedAlgorithm
PurposeMismatch
Failed
```

Those categories can map to host-owned outcomes such as:

```text
Allow
Deny
Defer
Retry
RequireAcknowledgment
Escalate
DeadLetter
```

The exact mapping depends on consequence.

For example:

| Verification condition | Possible high-consequence response |
| --- | --- |
| Valid under expected authority | Continue to semantic/policy checks |
| Invalid signature | Deny and preserve evidence |
| Hash mismatch | Deny and preserve forensic context |
| Revoked key | Deny or quarantine; incident handling |
| Unknown key version | Escalate or defer historical review |
| Provider unavailable | Defer or fail closed |
| Unsupported algorithm | Deny new trust decision; escalate archival review |
| Canonicalization mismatch | Escalate; do not guess which representation was signed |

The value of explicit categories is architectural.

They make failure behavior visible rather than hiding several trust failures behind one generic exception.

---

## Verification Provider Failure Must Not Become Permission

Suppose the verifier depends on a managed key service.

```text
Signed capability arrives
        ↓
Verification provider unavailable
```

Dangerous fallback:

```text
Cannot verify right now
        ↓
Assume signature is valid
        ↓
Execute
```

That converts loss of a trust dependency into additional authority.

Safer outcomes may include:

```text
Defer
Retry
Deny
Escalate
Dead-letter
Use a documented lower-assurance path for low-risk operations
```

The correct choice is application-specific.

The rule is not:

> Always fail closed in every possible system.

The rule is:

> **Define the failure posture explicitly and do not silently turn unavailable verification into successful verification.**

For high-consequence execution, conservative behavior is usually easier to defend.

---

## Key Rotation Is a Historical Verification Problem Too

Rotation changes the key used for new signatures.

It should not automatically destroy the ability to verify old evidence.

A normal rotation might look like:

```text
Key v3 active
      ↓
Prepare v4
      ↓
New signatures use v4
      ↓
v3 becomes retired for signing
      ↓
Historical v3 verification remains available during retention period
```

This requires the artifact to preserve:

```text
KeyId
KeyVersion
SignatureAlgorithm
HashAlgorithm
SignedUtc
CanonicalizationVersion
```

and the host/provider to retain or resolve the corresponding verification material.

### Retired Is Not Revoked

A useful distinction is:

```text
Retired key
=
Do not sign new artifacts
but historical verification may remain trusted
```

versus:

```text
Revoked key
=
Do not silently trust signatures under this key version
```

A retired key may simply have reached the end of its planned signing period.

A revoked key may indicate suspected compromise, misuse, invalid issuance, or another trust failure.

Those states should not share one ambiguous label such as `inactive` if the difference matters to verification policy.

### Historical Verification Requires Retention Planning

Ask:

1. How long must signed records remain verifiable?
2. Can retired public verification material remain available for that period?
3. Can the provider still resolve old key versions?
4. Are key-version backups or certificate chains retained?
5. Does archival storage preserve canonicalization and schema versions?
6. Does the organization know which records were signed by each version?

A successful rotation deployment does not prove that historical evidence remains verifiable.

Test it.

---

## Compromised Keys Change the Meaning of a Valid Signature

Suppose `key-v3` is later suspected of compromise.

A historical artifact may still pass the raw cryptographic operation:

```text
Signature verifies mathematically under v3
```

but the trust decision has changed:

```text
v3 is revoked / quarantined
        ↓
Do not silently accept as trustworthy evidence
```

This shows why verification includes key-lifecycle policy, not only mathematics.

A compromise response may need to:

- Stop new signing with the affected version.
- Activate a replacement version.
- Mark the affected version revoked or quarantined.
- Identify artifacts signed during the exposure window.
- Re-verify or quarantine those artifacts under incident policy.
- Preserve forensic context.
- Record later verification outcomes without rewriting original signing metadata.

Do not "repair" history by replacing the original key version on old artifacts.

If an archival attestation or re-signing step is required, store it as a new layer of evidence rather than pretending it was the original signature.

---

## Signed Timestamps Need Their Own Trust Model

A field such as:

```text
SignedUtc: 2026-08-19T17:00:00Z
```

is useful metadata.

Its strength depends on who supplied the time.

If ordinary application code supplied the timestamp before signing, the signature can protect the timestamp from later modification.

That does not prove the timestamp was objectively correct.

Stronger time claims may require:

- Provider-generated signing timestamps.
- Trusted timestamping services.
- External transparency logs.
- Protected append checkpoints.
- Other environment-specific time authorities.

Do not call an application-supplied signed timestamp "proof of time" unless the deployed system actually supports that claim.

---

## Tamper Evidence Is Not Tamper Prevention

A tamper-evident design aims to make unauthorized or unexpected modification detectable under a defined verification process.

A tamper-prevention design aims to make modification difficult or impossible for particular actors.

They are related but different.

Examples of preventive controls include:

- Database permissions.
- Object-lock / WORM storage.
- Append-only APIs.
- Separation of write and administrative roles.
- Storage retention locks.

Examples of evidence controls include:

- Signatures over records.
- Hash chains.
- Merkle-tree commitments.
- External checkpoints.
- Transparency logs.

A system can have one without the other.

For example:

```text
Append-only database table
```

may prevent ordinary updates through the application API.

It does not automatically prove who authored a row.

Likewise:

```text
Signed record in a mutable database
```

may reveal that the row was changed if verification fails.

It does not necessarily prevent deletion or replacement attempts.

The strongest designs often combine prevention and evidence according to the threat model.

---

## A Minimal Tamper-Evident Chain

A simple chain can link each record to the previous record.

Conceptually:

```text
Record 1
Hash = H(canonical record 1)

Record 2
PreviousHash = Hash(record 1)
Hash = H(canonical record 2 + PreviousHash)

Record 3
PreviousHash = Hash(record 2)
Hash = H(canonical record 3 + PreviousHash)
```

Then modification to `Record 1` can break later links.

A conceptual record might contain:

```csharp
public sealed record ChainedEvidenceRecord(
    long Sequence,
    string ArtifactId,
    string PreviousRecordHash,
    string CurrentRecordHash,
    DateTimeOffset RecordedUtc);
```

That illustrates the structure.

It does **not** provide production tamper evidence by itself.

### The Entire Chain Can Be Rewritten

If one actor can rewrite every record and recompute every hash, the chain can be reconstructed consistently.

Therefore a chain needs a trust anchor appropriate to the threat model.

Possible anchors include:

- Periodically signed chain heads.
- Protected checkpoints in another system.
- Object-lock storage.
- External transparency services.
- Independent replication under separate administration.
- Hardware-backed signing of checkpoints.

The specific choice depends on consequence and operational environment.

### Tail Truncation Is a Special Case

A hash chain can detect many modifications to retained records.

If an attacker deletes the newest records and no trusted external party knows the expected latest chain head, the remaining prefix may still verify internally.

This is why strong claims about deletion detection often require:

```text
Expected sequence / head
        +
Protected or external checkpoint
```

A chain is a mechanism.

The guarantee comes from the complete verification and anchoring model.

---

## Append-Only and Immutable Storage Do Not Prove Authorship

An append-only store can make historical mutation harder.

An immutable object can prevent later replacement under configured retention controls.

Neither tells the verifier whether the producer was authorized.

For example:

```text
Malicious producer
      ↓
Writes false record
      ↓
Storage correctly preserves it forever
```

Immutability faithfully preserves bad data too.

Likewise, a signature can show that an authorized key signed a false record.

This is why evidence systems need both:

```text
Who / what was authorized to produce evidence?
```

and:

```text
Was the preserved evidence altered later?
```

Those are separate questions.

---

## Decision Records as Signed Artifacts

A governance decision can be a useful signing boundary when another component must trust its origin.

For example:

```text
DecisionId
CorrelationId
Outcome
Actor
Operation
Resource
PolicyVersion
PolicyFingerprint
ReasonCodes
OccurredUtc
```

can become a canonical decision artifact.

Possible flow:

```text
Policy evaluator creates decision
        ↓
Decision canonicalized
        ↓
Decision signer signs
        ↓
Decision crosses process boundary
        ↓
Consumer verifies expected decision authority
        ↓
Consumer still checks current policy/freshness before execution
```

The signature can protect the decision artifact.

It does not make the old decision current forever.

---

## Acknowledgments as Signed Evidence

An acknowledgment workflow may preserve:

```text
ChallengeId
DecisionId
ActorId
AcknowledgmentCode
PolicyVersion
OccurredUtc
```

A host may sign the resulting acknowledgment record so later systems can verify that the configured acknowledgment service produced it.

That still does not automatically prove:

- The human understood the text.
- The human was legally competent.
- The user interface displayed exactly what the business intended.
- The authentication process met a legal signature standard.
- The acknowledgment remains sufficient under current policy.

Use language proportional to the mechanism:

> The system preserved a signed acknowledgment record produced by the configured service.

Avoid:

> The signature proves legal consent.

unless the full legal and identity process actually supports that claim.

---

## Capabilities as Signed Artifacts

Capabilities are especially sensitive because they may carry execution authority.

A signed capability can bind:

```text
Issuer
Subject / actor
Audience
Operation
Resource
Scope
PolicyVersion
ExpiresUtc
CapabilityId
```

The execution boundary should verify both cryptographic proof and semantic bindings.

For example:

```text
Proof valid
        +
Expected issuer
        +
Expected audience
        +
Expected operation
        +
Expected resource
        +
Expected policy evidence
        +
Not expired
        +
Not revoked
        +
Replay/use state available
        ↓
Possible execution
```

This connects directly to [Replay Protection and Bounded-Use Authority](replay-protection-and-bounded-use.md):

> **A valid signature says nothing about whether the capability has already been consumed.**

Cryptographic authenticity and replay state are complementary controls.

---

## Audit Residue as Signed Evidence

Audit residue can preserve:

```text
What decision occurred?
What acknowledgment occurred?
What capability was issued or consumed?
What execution occurred?
What failed?
Which policy was involved?
```

Signing selected audit artifacts can help downstream reviewers detect modification and identify the expected signing authority.

But be precise about the claim.

Safe wording:

> The audit record is signed and can be verified against the configured trust policy.

Stronger wording:

> The audit trail is tamper-evident.

requires more than a signature on individual rows.

The deployed design may also need:

- Durable write controls.
- Chain or sequence integrity.
- Detection of deletion/truncation.
- Protected checkpoints.
- Retention policy.
- Monitoring.
- Verification procedures.
- Incident response.

The evidence claim belongs to the whole system, not one field named `Signature`.

---

## A Provider-Neutral Teaching Model

The cryptographic provider can remain outside application-domain code.

A small teaching boundary might look like:

```csharp
public sealed record SigningRequest(
    string ArtifactType,
    string ArtifactId,
    string HashAlgorithm,
    string SigningHash,
    string KeyId,
    string? KeyVersion = null);

public sealed record SigningEvidence(
    string HashAlgorithm,
    string SigningHash,
    string SignatureAlgorithm,
    string KeyId,
    string? KeyVersion,
    string SignatureValue,
    DateTimeOffset SignedUtc);

public interface IArtifactSigningService
{
    ValueTask<SigningEvidence> SignAsync(
        SigningRequest request,
        CancellationToken cancellationToken);
}
```

The verifier can use a separate interface:

```csharp
public enum VerificationCategory
{
    Valid,
    InvalidSignature,
    HashMismatch,
    UnknownKey,
    RevokedKey,
    ProviderUnavailable,
    UnsupportedAlgorithm,
    Failed
}

public sealed record VerificationResult(
    VerificationCategory Category,
    string? FailureCode = null);

public interface IArtifactVerificationService
{
    ValueTask<VerificationResult> VerifyAsync(
        string artifactType,
        string expectedHash,
        SigningEvidence evidence,
        CancellationToken cancellationToken);
}
```

These types deliberately omit:

- Private key bytes.
- Certificate-loading code.
- Cloud credentials.
- HSM sessions.
- Provider-specific algorithm implementation.

The teaching goal is to make responsibility visible:

```text
Domain code builds artifact
Provider signs
Consumer verifies
Host policy decides what verification means
```

not to teach custom cryptographic implementation.

---

## Verification Policy Still Needs Semantic Context

A verifier may need context beyond the signature itself.

For example:

```csharp
public sealed record VerificationPolicyContext(
    string ExpectedArtifactType,
    string ExpectedKeyId,
    string? ExpectedKeyVersion,
    string? ExpectedProvider,
    string? RequiredHashAlgorithm,
    string? ExpectedPolicyVersion,
    string? ExpectedPolicyFingerprint);
```

This lets the host distinguish:

```text
Signature mathematically valid
```

from:

```text
Signature valid for the authority and purpose expected here
```

A verifier that accepts any key it knows can create accidental cross-purpose authority.

Purpose-specific trust configuration is usually easier to reason about.

---

## Threat Scenarios

### Scenario 1 — Modified Decision Record

```text
Signed decision stored
        ↓
Outcome changed from Deny to Allow
        ↓
Consumer canonicalizes modified record
        ↓
Expected hash differs / signature fails
        ↓
Reject and preserve forensic evidence
```

Signing can help here when the consumer trusts the signing authority and reconstructs the correct canonical bytes.

### Scenario 2 — Attacker Replaces Record and Hash

```text
Payload changed
Hash changed to match new payload
```

A bare fingerprint passes its own internal comparison.

A signature anchored to a protected signing authority should fail unless the attacker can also create a valid signature.

### Scenario 3 — Valid Signature From Wrong Authority

```text
Audit key signs a capability
        ↓
Signature verifies cryptographically
        ↓
Capability verifier expects capability-signing-key
        ↓
Trust-policy mismatch
        ↓
Reject
```

This is why key purpose matters.

### Scenario 4 — Replayed Signed Capability

```text
Capability signature valid
Capability unexpired
        ↓
Capability was already consumed
        ↓
Replay store rejects second use
```

The signature remains valid.

The authority is no longer consumable.

### Scenario 5 — Verification Provider Outage

```text
Consequential request
        ↓
Verification provider unavailable
        ↓
Configured Defer / Deny / Escalate
        ↓
No silent execution
```

Availability policy should be explicit.

### Scenario 6 — Normal Key Rotation

```text
Artifact signed by v3
        ↓
v4 becomes active
        ↓
v3 retired, verification material retained
        ↓
Historical artifact still verifies under v3
```

No historical metadata is rewritten.

### Scenario 7 — Compromised Key

```text
Artifact signature mathematically valid under v3
        ↓
v3 is revoked after compromise
        ↓
Verification policy returns revoked/quarantined state
        ↓
Do not silently trust
```

Key lifecycle can override the naive "valid signature" interpretation.

### Scenario 8 — Hash Chain Rewritten Locally

```text
Attacker can modify every row
        ↓
Attacker rewrites payloads and recomputes entire chain
        ↓
Local chain remains internally consistent
```

Without a protected checkpoint or independently trusted signature/anchor, the chain may not reveal the rewrite.

---

## Common Failure Modes

### 1. Call a Hash a Signature

A SHA-256 digest is presented as proof of authorship.

It is content identity evidence, not signing authority evidence.

### 2. Trust Any Cryptographically Valid Signature

The verifier does not check key purpose, key ID, provider, policy context, or trust anchor.

A valid signature from an unintended authority becomes accidental permission.

### 3. Store Private Keys in Application Configuration

Private signing material is distributed with ordinary application configuration or source.

Compromise of the app or configuration becomes compromise of signing authority.

### 4. Give Every Service the Signing Secret

Every verifier can also forge signatures.

Separation of duties disappears.

### 5. Sign an Unstable Serialization

Property ordering, timestamps, or metadata change unpredictably.

Legitimate artifacts fail verification after harmless serialization differences.

### 6. Omit Material Fields From the Signed Representation

A resource, audience, scope, or policy binding can change without invalidating the signature.

### 7. Let the Artifact Choose Its Own Trust Root

The artifact supplies a public key and the verifier accepts it without independent trust configuration.

An attacker can self-sign anything.

### 8. Treat Signed as Verified

The system sees a non-empty signature field and assumes the artifact is trusted.

No cryptographic verification occurs.

### 9. Treat Verified as Authorized

Signature verification succeeds, so current actor, resource, policy, expiry, replay, or revocation checks are skipped.

### 10. Rotate Keys and Delete Historical Verification Material

New signing works, but older audit evidence becomes unverifiable.

### 11. Treat Retired and Revoked as the Same State

Normal rotation and suspected compromise become indistinguishable.

Historical trust decisions become ambiguous.

### 12. Fail Open When Verification Is Unavailable

Provider outage becomes permission to execute.

### 13. Call a Mutable Database "Tamper-Proof"

Ordinary rows are described as immutable or tamper-proof without supporting controls.

### 14. Call Append-Only Storage Proof of Authorship

The store prevents updates but accepts false records from an unauthorized producer.

### 15. Assume a Hash Chain Detects Every Deletion

The newest records are truncated and no protected external checkpoint exists.

The retained prefix still verifies.

### 16. Re-Sign Old Records and Overwrite Original Evidence

Rotation or archival work replaces original key metadata.

Historical provenance is lost.

### 17. Log Private Keys or Raw Signing Secrets

Diagnostic telemetry turns security material into incident material.

### 18. Describe a Teaching Signer as Production Key Custody

An in-memory or local-development signer is useful for demonstrating the seam.

It does not establish production-grade key protection.

---

## Test Architectural Invariants

Tests should prove trust-boundary behavior rather than merely confirm that signature fields exist.

### Content Modification Fails

```text
Sign canonical artifact A
        ↓
Change material field
        ↓
Recompute canonical hash
        ↓
Verification does not return Valid
```

### Canonicalization Is Stable

Representations intentionally defined as semantically equivalent should produce the same canonical bytes.

Fields intentionally defined as meaningful should change the canonical bytes when modified.

### Wrong Artifact Type Fails

A signature for:

```text
artifactType = acknowledgment
```

must not automatically verify as:

```text
artifactType = capability
```

when artifact type is part of the signed domain.

### Wrong Authority Fails

```text
Signature valid under audit-key
Expected key = capability-key
        ↓
Trust-policy failure
```

### Verification Is Required

A signed artifact that has not been verified should not satisfy a code path requiring verified proof.

### Revoked Key Does Not Become Valid

A signature that mathematically verifies under a revoked key should map to the configured revoked-key outcome.

### Retired Key Remains Historically Verifiable When Policy Allows

After normal rotation, artifacts signed under the retired version should still verify during the intended retention window.

### Provider Outage Does Not Execute Silently

```text
Verifier unavailable
        ↓
Configured deny / defer / escalation
        ↓
Protected executor invocation count = 0
```

for paths where verification is required before execution.

### Signature Does Not Replace Replay Protection

```text
Signed one-time capability
        ↓
First use accepted
        ↓
Second use signature still valid
        ↓
Replay/use check rejects
```

### Policy Drift Remains Visible

A verified decision signed under policy `4.2` should not be silently relabeled or treated as current when execution policy is `4.3` and the freshness rule requires re-evaluation.

### Chain Modification Breaks Verification

Changing a retained record should break the expected record hash or later chain links.

### Chain Truncation Test Matches the Claimed Model

If the system claims tail-deletion detection, the test should prove that the verifier has an independently trusted expected sequence/head/checkpoint.

Do not claim truncation detection from internal chain consistency alone.

---

## When a Simpler Pattern Is Better

Not every record needs a signature.

A simpler design may be appropriate when:

- The operation is low consequence.
- Producer and consumer run inside one trusted process.
- No cross-service authenticity question exists.
- Ordinary database access control is sufficient for the threat model.
- Historical evidence does not need independent cryptographic verification.
- Existing platform authentication and authorization already provide the required boundary.
- Adding key infrastructure would create more operational risk than security value.

For example:

```text
In-process UI preference validation
```

usually does not need a signing service.

Likewise, an ordinary audit log used only for short-term local debugging may not justify a complex cryptographic evidence system.

The objective is not:

> Sign everything.

The objective is:

> **Use cryptographic evidence where a real trust-boundary question requires it, and make the resulting claim precise.**

---

## Tradeoffs

### Benefits

- Detects modification of signed material when verification is performed correctly.
- Provides a stronger origin claim than a bare fingerprint.
- Allows verification authority to be separated from signing authority with asymmetric designs.
- Makes key ownership and purpose explicit.
- Supports cross-process and long-lived artifact verification.
- Preserves a path for historical verification across normal key rotation.
- Creates explicit failure states for unknown, revoked, unsupported, or unavailable trust dependencies.
- Can strengthen audit and capability evidence when combined with policy and storage controls.

### Costs

- Key custody becomes an operational dependency.
- Rotation and historical verification require lifecycle planning.
- Verification can add latency and provider availability concerns.
- Canonicalization becomes a versioned contract.
- Incorrect trust anchors can invalidate otherwise sound cryptography.
- More metadata must be retained for long-lived evidence.
- Incident response becomes more complex after key compromise.
- Tamper-evident storage requires more than adding a signature column.
- Cross-region verification may depend on key/provider availability and replication design.
- Stronger evidence claims require stronger monitoring, retention, and operational discipline.

Cryptographic evidence is therefore not a free property attached by a library call.

It is an end-to-end trust contract.

---

## Review Checklist

Before claiming cryptographic integrity or authenticity for a consequential artifact, ask:

1. What exact artifact is being protected?
2. What threat requires a signature rather than only ordinary authorization or storage controls?
3. Is the canonical representation defined and versioned?
4. Are all materially relevant fields included in the signed representation?
5. Are unrelated volatile fields excluded deliberately?
6. Is artifact type/purpose bound into the signed representation?
7. Is the recorded hash recomputed from the received artifact during verification?
8. Which component owns signing authority?
9. Does that component also hold execution authority unnecessarily?
10. Where is private signing material stored?
11. Can the application retrieve raw private key bytes, and does it need to?
12. Which service identity may request signing operations?
13. Which key ID and key version are recorded on the artifact?
14. Which signature and hash algorithms are allowed by verifier policy?
15. What is the verifier's trust anchor?
16. Can an artifact supply an arbitrary verification key and become self-trusted?
17. Is the signing key authorized for this artifact type and purpose?
18. Where is signature verification performed?
19. Is "signed" kept separate from "verified" in code and documentation?
20. What happens after verification succeeds—what semantic and policy checks still remain?
21. What happens when verification fails?
22. What happens when the verifier or key provider is unavailable?
23. How are unknown, retired, revoked, expired, disabled, and compromised keys distinguished?
24. How are historical key versions retained or resolved?
25. How long must historical signatures remain verifiable?
26. What is the emergency rotation and compromise-response process?
27. Are verification results retained separately from original signing metadata?
28. Is a signed timestamp being described more strongly than its time source supports?
29. If the artifact is replay-sensitive, where is replay/use state enforced?
30. If the storage is called tamper-evident, what detects record modification?
31. What detects record deletion or tail truncation?
32. Is there a protected or external chain checkpoint when the threat model requires one?
33. What prevents an unauthorized producer from writing false but immutable evidence?
34. Are key identifiers and safe verification metadata logged without exposing signatures or secrets unnecessarily?
35. Can the team state the evidence guarantee without using "tamper-proof," "immutable," "non-repudiable," or "trusted" more strongly than the deployed controls support?

If those answers are unclear, the cryptographic primitive may exist while the trust architecture remains implicit.

---

## Review Questions

Before moving on, you should be able to answer:

1. Why is a policy fingerprint not a digital signature?
2. Why can a hash stored beside a mutable record be replaced with that record?
3. What additional claim can a digital signature support after successful verification?
4. Why does signature verification still depend on a trust anchor?
5. Why is a valid signature from the wrong key or purpose not sufficient?
6. Why should a consumer recompute the canonical hash instead of trusting the stored hash value?
7. Why does canonicalization need a version?
8. Why should artifact type or purpose be bound into the signed representation?
9. Why is signing authority different from execution authority?
10. Why can asymmetric signing improve separation between signer and verifier?
11. Why does a shared-secret MAC have different verifier-authority semantics?
12. Why is a managed key service not a complete key-management strategy by itself?
13. Why should exact key version metadata survive rotation?
14. What is the difference between a retired key and a revoked key?
15. Why can a mathematically valid signature under a compromised key still be rejected by policy?
16. Why is `SignedUtc` not automatically a trusted timestamp?
17. Why is `signed` different from `verified`?
18. Why is `verified` different from `authorized to execute`?
19. What should happen when required verification cannot be completed?
20. Why does a signed capability still need expiration, binding, revocation, and replay checks?
21. Why does an append-only store not prove authorship?
22. Why is tamper evidence different from tamper prevention?
23. How can a hash chain reveal modification?
24. Why can an attacker who rewrites the entire local chain defeat an unanchored chain?
25. Why can tail truncation remain undetected without an expected external or protected chain head?
26. What additional controls are needed before describing an audit trail as tamper-evident?

---

## Working Implementation References

Learning keeps this tutorial provider-neutral and does not require the `AsiBackbone` package.

The current `AsiBackbone/AsiBackbone` repository provides useful working references for the same boundaries.

| Learning concept | Working reference | What to inspect |
| --- | --- | --- |
| Canonical payloads, hashes, signing metadata, and provider-neutral interfaces | [Signing-Ready Receipts and Key Handling](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/signing-ready-receipts-and-key-handling.md) | Deterministic canonical payloads, key ID/version metadata, signing seams, and explicit wording limits. |
| Signed is not verified | [Verification Policy and Result Handling](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/verification-policy-and-result-handling.md) | Verification categories, host policy actions, trust-context checks, and failure handling. |
| Rotation and historical verification | [Key Rotation and Retired-Key Verification](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/key-rotation-and-retired-key-verification.md) | Active, retired, revoked, expired, disabled, and unknown key states plus historical verification guidance. |
| Signed governance artifacts | [Signed Audit and Outbox Records](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/signed-audit-and-outbox-records.md) | Signing points for audit and outbox artifacts and the boundary between signed records and tamper-evident trails. |
| Narrow proof authority | [Capability Proof Trust Pinning](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/capability-proof-trust-pinning.md) | Why a cryptographically valid proof can still fail when key, version, provider, algorithm, or policy expectations do not match. |
| Production security wording and non-goals | [Cryptographic Security Posture and Production Guidance](https://github.com/AsiBackbone/AsiBackbone/blob/main/docs/articles/cryptographic-security-posture.md) | Host responsibilities, provider boundaries, security non-goals, and safe production claims. |

These references are implementation specimens rather than universal prescriptions.

The Learning boundary remains:

> **Cryptographic proof strengthens a defined trust claim. It does not make policy, storage, or execution trustworthy by itself.**

---

## Related Content

- [Security](index.md) — return to the Security learning-area overview.
- [Trust Boundaries and Least Privilege](trust-boundaries-and-least-privilege.md) — identify where authority and data change control before deciding where to sign or verify.
- [Replay Protection and Bounded-Use Authority](replay-protection-and-bounded-use.md) — distinguish cryptographic authenticity from stateful one-time or bounded-use authority.
- [Policy Versioning and Decision Provenance](../governance/policy-versioning-and-decision-provenance.md) — distinguish policy identity and fingerprints from authenticity and tamper-evidence claims.
- [Acknowledgment and Audit Residue](../tutorials/acknowledgment-and-audit-residue.md) — connect signatures to durable governance evidence without treating acknowledgment as an execution override.
- [Scoped Capability and Host-Owned Execution](../tutorials/scoped-capability-and-host-owned-execution.md) — apply verification as one execution-boundary check around narrow authority.
- [Governed AI Tool Gateway](../tutorials/governed-ai-tool-gateway.md) — preserve the rule that AI may propose while host-owned code retains verification, policy, and execution authority.

---

## Scope

This tutorial is educational architecture guidance.

It does not provide:

- A production cryptographic implementation.
- A key-management product.
- A certificate-policy design.
- A legal signature standard.
- Legal non-repudiation.
- A compliance certification.
- An immutable ledger.
- A guarantee that a hash chain is sufficient for a particular threat model.
- A claim that signing every governance artifact is desirable.

Production systems should use established cryptographic libraries and provider services, define explicit key-management and verification policy, test historical verification and failure behavior, and obtain application-specific security review appropriate to the consequences involved.

---

> **Read it. Run it. Question it. Improve it.**
