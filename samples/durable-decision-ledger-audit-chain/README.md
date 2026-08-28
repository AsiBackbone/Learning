# Durable Decision Ledger and Audit Chain Sample

This sample is the executable companion to [Durable Decision Ledgers and Cryptographic Audit Chains](../../docs/advanced/durable-decision-ledgers-and-cryptographic-audit-chains.md).

It demonstrates one boundary:

> **A locally consistent chain can expose mutation and ordering failures, while tail completeness requires an independently retained expectation such as a protected checkpoint.**

The sample stays deliberately smaller than a production ledger. It uses an in-memory append store so the code can isolate canonicalization, idempotency, chain construction, verification categories, and checkpoint semantics without implying that process memory is durable or append-only storage.

## Run It

The sample targets **.NET 10**, matching `samples/Directory.Build.props`.

From the repository root:

```bash
dotnet run --project samples/durable-decision-ledger-audit-chain/DurableDecisionLedgerAuditChain/DurableDecisionLedgerAuditChain.csproj
```

Run the focused tests:

```bash
dotnet test samples/durable-decision-ledger-audit-chain/DurableDecisionLedgerAuditChain.Tests/DurableDecisionLedgerAuditChain.Tests.csproj
```

Or validate the complete sample suite:

```bash
dotnet build samples/Samples.slnx
dotnet test samples/Samples.slnx
```

## What the Console Demonstrates

The console appends two governance receipts, retries the first logical append, captures a checkpoint at the current head, and then verifies several views:

```text
Governance receipts
        ↓
Deterministic canonical bytes
        ↓
SHA-256 record fingerprints
        ↓
Previous-fingerprint linkage
        ↓
In-memory compare/append boundary
        ↓
Protected-checkpoint teaching model
        ↓
Streaming verifier
```

Scenarios include:

- a valid chain with a matching checkpoint;
- verification resumed from a previously trusted checkpoint boundary;
- an unsupported hash-algorithm claim returned as an explicit verification category rather than an exception;
- a modified first record;
- a truncated prefix with no checkpoint, where integrity can verify but tail completeness remains unknown;
- the same truncated prefix with a later checkpoint, where the missing checkpointed tail is detectable;
- reordered records;
- an idempotent retry that reuses the original record rather than allocating a new sequence.

## Canonicalization Contract

`CanonicalLedgerEncoding` does not use a runtime's default object serializer. It writes a small versioned JSON object in an explicit property order and normalizes timestamps to a fixed UTC representation.

The test suite pins both the exact UTF-8 JSON text and its expected SHA-256 fingerprint for one fixed record. That gives the teaching sample a concrete test vector instead of merely asserting that two calls to the same serializer agree with each other.

This `canonical-json/v1` format is intentionally local to the sample. It is **not** presented as RFC 8785 JCS. Production systems should choose and document a canonicalization standard or protocol contract appropriate to their interoperability and retention requirements.

The local contract is explicit about details that often cause cross-implementation drift:

- outer and receipt properties are emitted in a fixed ordinal order; the outer order is lexicographic to make comparison with JCS-style ordering easier;
- `UnsafeRelaxedJsonEscaping` preserves ordinary non-ASCII characters as UTF-8 while still escaping JSON syntax/control characters; a non-ASCII vector is pinned in tests;
- no Unicode normalization is performed: NFC and NFD are distinct input strings by design, so any equivalence normalization belongs upstream of canonicalization;
- unpaired UTF-16 surrogates are rejected rather than silently replaced;
- `OccurredUtc` is normalized to UTC with a fixed seven-digit fractional-second representation (100-nanosecond precision);
- `SequenceNumber` is encoded as invariant decimal text rather than a JSON number so a 64-bit sequence is not silently rounded by consumers that parse numbers through IEEE-754 double precision;
- null/blank identifier and version fields are rejected rather than emitted as implicit JSON nulls.

The result is **JCS-like in property ordering but deliberately not JCS-compatible**: sequence numbers are strings, timestamps retain the sample's fixed seven-digit fractional form, and the complete canonicalization contract remains local to `canonical-json/v1`.

`GENESIS/v1` is deliberately a non-hex sentinel, so it cannot be mistaken for a real SHA-256 fingerprint. `RecordId` identity and all verifier comparisons use ordinal, case-sensitive semantics; `record-001` and `RECORD-001` are different identifiers.

## Idempotent Append Before Head Reconstruction

`InMemoryDecisionLedger.Append` checks stable `RecordId` state before assigning a sequence number or rebuilding the predecessor link.

```text
Retry with same RecordId + same receipt
        ↓
Return existing record
        ↓
No new sequence
No duplicate governance event
```

Reusing the same `RecordId` with different governance evidence is rejected.

The fingerprint is computed successfully **before** `_records` or `_recordsById` is mutated. A canonicalization rejection therefore cannot leave a half-appended record behind; a focused test pins that ordering so a future refactor does not move state mutation ahead of canonical validation.

The in-process lock serializes the teaching head transition. A production provider would need a real database/log transaction, optimistic concurrency token/CAS operation, serializable isolation, or another atomic provider mechanism appropriate to its deployment.

## Verification Separates Integrity From Completeness

`LedgerVerifier` reports two dimensions:

- `LedgerIntegrityStatus` — sequence, predecessor linkage, and canonical fingerprint verification.
- `LedgerCompletenessStatus` — what the verifier can say about the tail relative to an optional checkpoint.

A truncated prefix can therefore produce:

```text
IntegrityStatus = Verified
CompletenessStatus = UnknownWithoutCheckpoint
```

while the same prefix checked against a checkpoint from a later head produces:

```text
IntegrityStatus = Verified
CompletenessStatus = MissingCheckpointedTail
```

That is the central teaching distinction: a valid prefix is not proof that the newest evidence is present.

The verifier also preserves format and canonicalization failures as evidence categories. Unsupported `ArtifactType`, `RecordSchemaVersion`, `CanonicalizationVersion`, or `HashAlgorithm` values are reported explicitly before fingerprint recomputation. Records whose supported-format fields cannot be reproduced under the canonicalization contract—for example blank required text or invalid Unicode scalar values—return `RecordNotCanonicalizable` rather than escaping as `ArgumentException` or masquerading as tampering. Empty input is likewise `EmptyInput`, not `Verified`; when a later checkpoint exists, completeness can still report `MissingCheckpointedTail`.

### Resume from a trusted boundary

A full verification starts from the explicit `GENESIS/v1` predecessor. For segmented archives or checkpoint-based continuation, the caller can instead pass `LedgerVerificationStart.FromCheckpoint(...)`. The first record in that segment must have the expected next sequence and must reference the retained checkpoint fingerprint.

The start object records a descriptive trust-boundary source because beginning at sequence 501 is only meaningful if the verifier has an independently justified expectation for sequence 500. The verifier carries that context into both success and failure detail for audit/debug visibility. Before projection, control/line-separator characters are replaced and the label is length-bounded so caller text cannot forge diagnostic line structure. The underlying label is still descriptive caller-owned metadata; the verifier does not authenticate it. Resume verifies **forward from that accepted boundary**; it does not retroactively prove earlier records.

An empty resumed segment cannot use the same checkpoint as both its start expectation and affirmative completeness evidence. With zero supplied records, a checkpoint exactly at the accepted boundary is `NotEvaluated`; a later independently retained checkpoint can still produce `MissingCheckpointedTail`.

A checkpoint older than the accepted resume boundary is `CheckpointOutsideVerifiedRange`, not `CheckpointMismatch`: the verifier has not demonstrated a contradiction, only that the supplied checkpoint cannot evaluate the segment it was asked to verify.

## Split-View Boundary

The sample includes a test where a second ledger with the same logical `LedgerId` builds a different but internally valid history. Each view verifies locally.

Only when the alternate view is compared with the checkpoint captured from the original history does the mismatch become visible. That test proves **whole-chain replacement contradicts a known checkpoint**; it does not by itself prove detection of equivocation between two isolated verifiers.

The sample does **not** implement gossip, witness distribution, Certificate Transparency consistency proofs, or a network protocol. Those are deliberately left as external mechanisms because a single in-process verifier cannot prove that every other verifier received the same head.

## Streaming Verification

`LedgerVerifier.Verify` consumes `IEnumerable<LedgerRecord>` sequentially and does not require random access to the full ledger. The focused tests feed it a generated 10,000-record sequence to preserve the single-pass/no-random-access shape without pretending that an ordinary unit test proves a production memory bound. Canonical validation also walks the required text fields during each fingerprint recomputation, so large-ledger benchmarks should account for work proportional to total canonical text as well as record count.

Large production ledgers still need explicit throughput, storage, checkpoint, historical-key lookup, and recovery benchmarking.

## Focused Invariants

Tests prove that:

- the canonical test vector remains byte-for-byte stable;
- the expected SHA-256 fingerprint remains stable;
- a retry of the same logical record does not allocate another sequence;
- the same `RecordId` cannot silently represent different evidence;
- sixteen concurrently released in-process writers still produce one linear sequence;
- modifying a record without updating its fingerprint is detected;
- reordering records is detected;
- an internally valid truncated prefix has unknown tail completeness without a checkpoint;
- a protected checkpoint detects a missing checkpointed tail;
- an alternate internally valid whole-chain replacement conflicts with the known checkpoint;
- verification can resume immediately after an accepted checkpoint boundary;
- unsupported artifact/schema/canonicalization/hash claims return explicit verification categories;
- supported-format records that cannot be canonicalized return `RecordNotCanonicalizable` without throwing from the verifier;
- empty input is not reported as verified; no-checkpoint and self-confirming boundary-checkpoint cases stay `NotEvaluated`, while an independent later checkpoint can still expose a missing tail;
- a checkpoint older than a resumed verification range is reported as outside that range rather than as contradictory evidence;
- resumed verification failures preserve the caller-supplied start-boundary context in sanitized diagnostic detail;
- the first record must reference the explicit genesis sentinel;
- equivalent timestamp instants expressed with different offsets follow the same idempotent/canonical path;
- non-ASCII encoding behavior and invalid-surrogate rejection are pinned;
- long sequences can be verified through an enumerable stream rather than a random-access collection.

## What the Sample Does Not Model

This sample does **not** provide:

- durable database or object-store persistence;
- administrator/IAM separation;
- append-only storage enforcement;
- digital signatures;
- key custody, rotation, revocation, or historical public-key resolution;
- hash/signature algorithm migration;
- RFC 3161 trusted timestamping;
- Certificate Transparency-style Merkle inclusion/consistency proofs;
- cross-verifier gossip or independent witnesses;
- encryption or crypto-shredding;
- privacy/compliance policy;
- production backup, archive, restore, or migration procedures;
- current execution authority.

`LedgerCheckpoint.CustodyBoundary` is descriptive teaching metadata. It does not cryptographically protect the checkpoint. The article explains what independent custody, authenticated publication, timestamps, monitors, or witnesses would need to add.

## Architectural Boundaries

Keep these statements separate:

```text
Record fingerprints verify
        !=
Storage cannot be rewritten
        !=
The tail is complete
        !=
A signer is currently trusted
        !=
The historical decision is authorized to execute now
```

The sample implements the first statement and a small checkpoint comparison for the third. It intentionally does not claim the others.

---

> **Read it. Run it. Question it. Improve it.**
