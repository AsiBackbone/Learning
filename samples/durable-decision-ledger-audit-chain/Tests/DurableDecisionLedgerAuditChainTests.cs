using DurableDecisionLedgerAuditChain;
using Xunit;

namespace DurableDecisionLedgerAuditChain.Tests;

public sealed class DurableDecisionLedgerAuditChainTests
{
    [Fact]
    public void CanonicalEncodingHasStableTeachingVector()
    {
        LedgerRecordCore core = FirstCore();

        const string expected = "{\"artifactType\":\"accountable-systems-governance-ledger-record/v1\",\"canonicalizationVersion\":\"canonical-json/v1\",\"hashAlgorithm\":\"SHA-256\",\"ledgerId\":\"governance-east\",\"previousFingerprint\":\"GENESIS/v1\",\"receipt\":{\"decisionId\":\"dec-001\",\"occurredUtc\":\"2026-08-28T12:00:00.0000000Z\",\"operation\":\"account.disable\",\"outcome\":\"Allowed\",\"policyId\":\"account-admin\",\"policyVersion\":\"7.4\",\"resourceId\":\"account-123\"},\"recordId\":\"record-001\",\"recordSchemaVersion\":\"ledger-record/v1\",\"sequenceNumber\":\"1\"}";
        const string expectedFingerprint = "f4ac42655d93a38dae11a0eefd8627e02cd53a15944e376041a88d30eeef4984";

        Assert.Equal(expected, CanonicalLedgerEncoding.EncodeAsUtf8Text(core));
        Assert.Equal(expectedFingerprint, CanonicalLedgerEncoding.ComputeFingerprint(core));
    }

    [Fact]
    public void CanonicalEncodingPinsNonAsciiUtf8Behavior()
    {
        LedgerRecordCore core = FirstCore() with
        {
            Receipt = FirstReceipt() with { ResourceId = "café-☕" }
        };

        const string expected = "{\"artifactType\":\"accountable-systems-governance-ledger-record/v1\",\"canonicalizationVersion\":\"canonical-json/v1\",\"hashAlgorithm\":\"SHA-256\",\"ledgerId\":\"governance-east\",\"previousFingerprint\":\"GENESIS/v1\",\"receipt\":{\"decisionId\":\"dec-001\",\"occurredUtc\":\"2026-08-28T12:00:00.0000000Z\",\"operation\":\"account.disable\",\"outcome\":\"Allowed\",\"policyId\":\"account-admin\",\"policyVersion\":\"7.4\",\"resourceId\":\"café-☕\"},\"recordId\":\"record-001\",\"recordSchemaVersion\":\"ledger-record/v1\",\"sequenceNumber\":\"1\"}";
        // This value deliberately pins the NFC form of "café" as well as UTF-8 escaping.
        // Do not regenerate it merely because an NFD-equivalent string looks identical.
        const string expectedFingerprint = "79ef458652074c2e596c40c401cdcb68519635021e43341ac929283ba32d5335";

        Assert.Equal(expected, CanonicalLedgerEncoding.EncodeAsUtf8Text(core));
        Assert.Equal(expectedFingerprint, CanonicalLedgerEncoding.ComputeFingerprint(core));
    }

    [Fact]
    public void CanonicalEncodingRejectsUnpairedUnicodeSurrogate()
    {
        LedgerRecordCore core = FirstCore() with
        {
            Receipt = FirstReceipt() with { ResourceId = "account-\uD800" }
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => CanonicalLedgerEncoding.Encode(core));

        Assert.Equal("ResourceId", exception.ParamName);
    }

    [Fact]
    public void EquivalentTimestampOffsetsUseSameIdempotentRecord()
    {
        InMemoryDecisionLedger ledger = new("governance-east");
        GovernanceDecisionReceipt utc = FirstReceipt();
        GovernanceDecisionReceipt central = utc with
        {
            OccurredUtc = new DateTimeOffset(2026, 8, 28, 7, 0, 0, TimeSpan.FromHours(-5))
        };

        LedgerRecord first = ledger.Append("record-001", utc);
        LedgerRecord retry = ledger.Append("record-001", central);

        Assert.Same(first, retry);
        Assert.Equal(
            CanonicalLedgerEncoding.ComputeFingerprint(FirstCore()),
            retry.Fingerprint);
    }

    [Fact]
    public void IdempotentRetryIsCheckedBeforeNewSequenceIsAllocated()
    {
        InMemoryDecisionLedger ledger = new("governance-east");
        GovernanceDecisionReceipt receipt = FirstReceipt();

        LedgerRecord first = ledger.Append("record-001", receipt);
        LedgerRecord retry = ledger.Append("record-001", receipt);

        Assert.Same(first, retry);
        Assert.Single(ledger.Snapshot());
        Assert.Equal(1L, retry.SequenceNumber);
    }

    [Fact]
    public void ReusingRecordIdWithDifferentEvidenceIsRejected()
    {
        InMemoryDecisionLedger ledger = new("governance-east");
        ledger.Append("record-001", FirstReceipt());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ledger.Append(
                "record-001",
                FirstReceipt() with { Outcome = "Denied" }));

        Assert.Contains("different governance evidence", exception.Message);
        Assert.Single(ledger.Snapshot());
    }

    [Fact]
    public void CanonicalizationFailureDoesNotPartiallyAppendRecord()
    {
        InMemoryDecisionLedger ledger = new("governance-east");

        Assert.Throws<ArgumentException>(
            () => ledger.Append(
                "record-001",
                FirstReceipt() with { Operation = "" }));

        Assert.Empty(ledger.Snapshot());
    }

    [Fact]
    public async Task ConcurrentAppendsProduceOneLinearHeadSequence()
    {
        const int writerCount = 16;
        InMemoryDecisionLedger ledger = new("governance-east");
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<LedgerRecord>[] writers = Enumerable.Range(0, writerCount)
            .Select(index => Task.Run(async () =>
            {
                await release.Task;
                return ledger.Append(
                    $"record-{index:D2}",
                    FirstReceipt() with
                    {
                        DecisionId = $"dec-{index:D2}",
                        ResourceId = $"account-{index:D2}",
                        OccurredUtc = DateTimeOffset.UnixEpoch.AddSeconds(index)
                    });
            }))
            .ToArray();

        release.SetResult(true);
        await Task.WhenAll(writers);

        IReadOnlyList<LedgerRecord> snapshot = ledger.Snapshot();
        Assert.Equal(writerCount, snapshot.Count);
        Assert.Equal(
            Enumerable.Range(1, writerCount).Select(value => (long)value).ToArray(),
            snapshot.Select(record => record.SequenceNumber).ToArray());

        for (int index = 1; index < snapshot.Count; index++)
        {
            Assert.Equal(snapshot[index - 1].Fingerprint, snapshot[index].PreviousFingerprint);
        }
    }

    [Fact]
    public void FirstRecordMustReferenceExplicitGenesisSentinel()
    {
        LedgerRecord original = CreateRecord(FirstCore());
        LedgerRecord broken = original with { PreviousFingerprint = "not-genesis" };

        LedgerVerificationResult result = LedgerVerifier.Verify([broken], original.LedgerId);

        Assert.Equal(LedgerIntegrityStatus.BrokenLink, result.IntegrityStatus);
        Assert.Equal(1L, result.FailureSequence);
    }

    [Fact]
    public void EmptyInputIsNotReportedAsVerifiedIntegrity()
    {
        LedgerVerificationResult result = LedgerVerifier.Verify([], "governance-east");

        Assert.Equal(LedgerIntegrityStatus.EmptyInput, result.IntegrityStatus);
        Assert.False(result.IntegrityVerified);
        Assert.Equal(0L, result.RecordsVerified);
        Assert.Null(result.VerifiedThroughSequence);
        Assert.Equal(LedgerCompletenessStatus.NotEvaluated, result.CompletenessStatus);
    }

    [Fact]
    public void EmptyInputWithLaterCheckpointStillReportsMissingTail()
    {
        var (ledger, _, checkpoint) = TwoRecordLedger();

        LedgerVerificationResult result = LedgerVerifier.Verify([], ledger.LedgerId, checkpoint);

        Assert.Equal(LedgerIntegrityStatus.EmptyInput, result.IntegrityStatus);
        Assert.False(result.IntegrityVerified);
        Assert.Equal(LedgerCompletenessStatus.MissingCheckpointedTail, result.CompletenessStatus);
        Assert.Equal(0L, result.RecordsVerified);
    }

    [Fact]
    public void EmptyInputCannotSelfConfirmItsResumeBoundaryCheckpoint()
    {
        InMemoryDecisionLedger ledger = new("governance-east");
        ledger.Append("record-001", FirstReceipt());
        LedgerCheckpoint checkpoint = ledger.CreateCheckpoint(
            new DateTimeOffset(2026, 8, 28, 12, 0, 30, TimeSpan.Zero),
            "resume-checkpoint");

        LedgerVerificationResult result = LedgerVerifier.Verify(
            [],
            ledger.LedgerId,
            checkpoint,
            LedgerVerificationStart.FromCheckpoint(checkpoint));

        Assert.Equal(LedgerIntegrityStatus.EmptyInput, result.IntegrityStatus);
        Assert.Equal(LedgerCompletenessStatus.NotEvaluated, result.CompletenessStatus);
        Assert.Equal(0L, result.RecordsVerified);
    }

    [Theory]
    [InlineData("artifact", LedgerIntegrityStatus.UnsupportedArtifactType)]
    [InlineData("schema", LedgerIntegrityStatus.UnsupportedRecordSchemaVersion)]
    [InlineData("canonicalization", LedgerIntegrityStatus.CanonicalizationUnavailable)]
    [InlineData("hash", LedgerIntegrityStatus.UnsupportedHashAlgorithm)]
    public void UnsupportedFormatClaimsReturnExplicitVerificationCategories(
        string mismatch,
        LedgerIntegrityStatus expectedStatus)
    {
        LedgerRecord record = CreateRecord(FirstCore());
        record = mismatch switch
        {
            "artifact" => record with { ArtifactType = "other-artifact/v1" },
            "schema" => record with { RecordSchemaVersion = "ledger-record/v2" },
            "canonicalization" => record with { CanonicalizationVersion = "canonical-json/v2" },
            "hash" => record with { HashAlgorithm = "SHA-512" },
            _ => throw new InvalidOperationException()
        };

        LedgerVerificationResult result = LedgerVerifier.Verify([record], record.LedgerId);

        Assert.Equal(expectedStatus, result.IntegrityStatus);
        Assert.False(result.IntegrityVerified);
        Assert.Equal(1L, result.FailureSequence);
    }

    [Theory]
    [InlineData("blank-operation")]
    [InlineData("unpaired-resource")]
    [InlineData("blank-record-id")]
    public void NonCanonicalizableRecordReturnsExplicitVerificationCategory(string invalidCase)
    {
        LedgerRecord record = CreateRecord(FirstCore());
        record = invalidCase switch
        {
            "blank-operation" => record with
            {
                Receipt = record.Receipt with { Operation = "" }
            },
            "unpaired-resource" => record with
            {
                Receipt = record.Receipt with { ResourceId = "account-\uD800" }
            },
            "blank-record-id" => record with { RecordId = "   " },
            _ => throw new InvalidOperationException()
        };

        LedgerVerificationResult result = LedgerVerifier.Verify([record], record.LedgerId);

        Assert.Equal(LedgerIntegrityStatus.RecordNotCanonicalizable, result.IntegrityStatus);
        Assert.False(result.IntegrityVerified);
        Assert.Equal(1L, result.FailureSequence);
        Assert.Equal(LedgerCompletenessStatus.NotEvaluated, result.CompletenessStatus);
    }

    [Fact]
    public void VerificationCanResumeImmediatelyAfterTrustedCheckpoint()
    {
        InMemoryDecisionLedger ledger = new("governance-east");
        LedgerRecord first = ledger.Append("record-001", FirstReceipt());
        LedgerCheckpoint firstCheckpoint = ledger.CreateCheckpoint(
            new DateTimeOffset(2026, 8, 28, 12, 0, 30, TimeSpan.Zero),
            "archive-segment-checkpoint");
        LedgerRecord second = ledger.Append(
            "record-002",
            FirstReceipt() with
            {
                DecisionId = "dec-002",
                ResourceId = "account-456",
                OccurredUtc = new DateTimeOffset(2026, 8, 28, 12, 1, 0, TimeSpan.Zero)
            });
        LedgerCheckpoint secondCheckpoint = ledger.CreateCheckpoint(
            new DateTimeOffset(2026, 8, 28, 12, 2, 0, TimeSpan.Zero),
            "independent-checkpoint-store");

        LedgerVerificationResult result = LedgerVerifier.Verify(
            [second],
            ledger.LedgerId,
            secondCheckpoint,
            LedgerVerificationStart.FromCheckpoint(firstCheckpoint));

        Assert.True(result.IntegrityVerified);
        Assert.Equal(1L, result.RecordsVerified);
        Assert.Equal(2L, result.VerifiedThroughSequence);
        Assert.Equal(LedgerCompletenessStatus.MatchesCheckpoint, result.CompletenessStatus);
        Assert.Contains("archive-segment-checkpoint", result.Detail);
        Assert.Equal(first.Fingerprint, second.PreviousFingerprint);
    }

    [Fact]
    public void ResumedFailurePreservesTrustedStartContext()
    {
        InMemoryDecisionLedger ledger = new("governance-east");
        ledger.Append("record-001", FirstReceipt());
        LedgerCheckpoint checkpoint = ledger.CreateCheckpoint(
            new DateTimeOffset(2026, 8, 28, 12, 0, 30, TimeSpan.Zero),
            "archive-segment-checkpoint");
        LedgerRecord second = ledger.Append(
            "record-002",
            FirstReceipt() with
            {
                DecisionId = "dec-002",
                ResourceId = "account-456",
                OccurredUtc = new DateTimeOffset(2026, 8, 28, 12, 1, 0, TimeSpan.Zero)
            });
        LedgerRecord broken = second with { PreviousFingerprint = "unexpected-predecessor" };

        LedgerVerificationResult result = LedgerVerifier.Verify(
            [broken],
            ledger.LedgerId,
            start: LedgerVerificationStart.FromCheckpoint(checkpoint));

        Assert.Equal(LedgerIntegrityStatus.BrokenLink, result.IntegrityStatus);
        Assert.Equal(2L, result.FailureSequence);
        Assert.Contains("archive-segment-checkpoint", result.Detail);
    }

    [Fact]
    public void CallerSuppliedTrustBoundaryIsSanitizedBeforeDiagnosticProjection()
    {
        LedgerRecord record = CreateRecord(FirstCore());
        string callerLabel = "trusted-boundary\r\nforged-line-" + new string('x', 160) + "-not-projected";
        LedgerVerificationStart start = new(
            1,
            LedgerFormat.GenesisFingerprint,
            callerLabel,
            record.LedgerId);

        LedgerVerificationResult result = LedgerVerifier.Verify(
            [record],
            record.LedgerId,
            start: start);

        Assert.True(result.IntegrityVerified);
        Assert.NotNull(result.Detail);
        Assert.DoesNotContain('\r', result.Detail);
        Assert.DoesNotContain('\n', result.Detail);
        Assert.Contains("trusted-boundary??forged-line", result.Detail);
        Assert.DoesNotContain("not-projected", result.Detail);
    }

    [Fact]
    public void OlderCheckpointOutsideResumedRangeIsNotReportedAsContradiction()
    {
        InMemoryDecisionLedger ledger = new("governance-east");
        ledger.Append("record-001", FirstReceipt());
        LedgerCheckpoint firstCheckpoint = ledger.CreateCheckpoint(
            new DateTimeOffset(2026, 8, 28, 12, 0, 30, TimeSpan.Zero),
            "older-checkpoint");
        ledger.Append(
            "record-002",
            FirstReceipt() with
            {
                DecisionId = "dec-002",
                ResourceId = "account-456",
                OccurredUtc = new DateTimeOffset(2026, 8, 28, 12, 1, 0, TimeSpan.Zero)
            });
        LedgerCheckpoint secondCheckpoint = ledger.CreateCheckpoint(
            new DateTimeOffset(2026, 8, 28, 12, 1, 30, TimeSpan.Zero),
            "resume-checkpoint");
        LedgerRecord third = ledger.Append(
            "record-003",
            FirstReceipt() with
            {
                DecisionId = "dec-003",
                ResourceId = "account-789",
                OccurredUtc = new DateTimeOffset(2026, 8, 28, 12, 2, 0, TimeSpan.Zero)
            });

        LedgerVerificationResult result = LedgerVerifier.Verify(
            [third],
            ledger.LedgerId,
            firstCheckpoint,
            LedgerVerificationStart.FromCheckpoint(secondCheckpoint));

        Assert.True(result.IntegrityVerified);
        Assert.Equal(3L, result.VerifiedThroughSequence);
        Assert.Equal(
            LedgerCompletenessStatus.CheckpointOutsideVerifiedRange,
            result.CompletenessStatus);
    }

    [Fact]
    public void ModifiedMiddleRecordIsDetected()
    {
        var (ledger, records, checkpoint) = TwoRecordLedger();
        LedgerRecord modified = records[0] with
        {
            Receipt = records[0].Receipt with { Outcome = "Denied" }
        };

        LedgerVerificationResult result = LedgerVerifier.Verify(
            [modified, records[1]],
            ledger.LedgerId,
            checkpoint);

        Assert.Equal(LedgerIntegrityStatus.FingerprintMismatch, result.IntegrityStatus);
        Assert.Equal(1L, result.FailureSequence);
        Assert.Equal(LedgerCompletenessStatus.NotEvaluated, result.CompletenessStatus);
    }

    [Fact]
    public void ReorderedRecordsFailSequenceVerification()
    {
        var (ledger, records, checkpoint) = TwoRecordLedger();

        LedgerVerificationResult result = LedgerVerifier.Verify(
            [records[1], records[0]],
            ledger.LedgerId,
            checkpoint);

        Assert.Equal(LedgerIntegrityStatus.SequenceMismatch, result.IntegrityStatus);
        Assert.Equal(2L, result.FailureSequence);
    }

    [Fact]
    public void TruncatedPrefixCanVerifyWhileTailCompletenessRemainsUnknownWithoutCheckpoint()
    {
        var (ledger, records, _) = TwoRecordLedger();

        LedgerVerificationResult result = LedgerVerifier.Verify([records[0]], ledger.LedgerId);

        Assert.True(result.IntegrityVerified);
        Assert.Equal(1L, result.VerifiedThroughSequence);
        Assert.Equal(LedgerCompletenessStatus.UnknownWithoutCheckpoint, result.CompletenessStatus);
    }

    [Fact]
    public void CheckpointCapturedFromLaterHeadDetectsMissingNewestRecord()
    {
        var (ledger, records, checkpoint) = TwoRecordLedger();

        LedgerVerificationResult result = LedgerVerifier.Verify(
            [records[0]],
            ledger.LedgerId,
            checkpoint);

        Assert.True(result.IntegrityVerified);
        Assert.Equal(LedgerCompletenessStatus.MissingCheckpointedTail, result.CompletenessStatus);
    }

    [Fact]
    public void KnownCheckpointDetectsWholeChainReplacementFromAlternateValidView()
    {
        var (original, _, checkpoint) = TwoRecordLedger();

        InMemoryDecisionLedger alternate = new(original.LedgerId);
        alternate.Append("record-001-alt", FirstReceipt() with { DecisionId = "dec-alt", Outcome = "Denied" });
        alternate.Append(
            "record-002-alt",
            FirstReceipt() with
            {
                DecisionId = "dec-alt-2",
                ResourceId = "account-999",
                OccurredUtc = new DateTimeOffset(2026, 8, 28, 12, 1, 0, TimeSpan.Zero)
            });

        LedgerVerificationResult result = LedgerVerifier.Verify(
            alternate.Snapshot(),
            alternate.LedgerId,
            checkpoint);

        Assert.True(result.IntegrityVerified);
        Assert.Equal(LedgerCompletenessStatus.CheckpointMismatch, result.CompletenessStatus);
    }

    [Fact]
    public void StreamingVerifierHandlesLargeSequenceWithoutRequiringRandomAccess()
    {
        LedgerVerificationResult result = LedgerVerifier.Verify(
            GenerateRecords(10_000),
            "streaming-ledger");

        Assert.True(result.IntegrityVerified);
        Assert.Equal(10_000L, result.RecordsVerified);
        Assert.Equal(10_000L, result.VerifiedThroughSequence);
        Assert.Equal(LedgerCompletenessStatus.UnknownWithoutCheckpoint, result.CompletenessStatus);
    }

    [Fact]
    public void CheckpointRejectsNonPositiveSequence()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LedgerCheckpoint(
                "governance-east",
                0,
                "head",
                DateTimeOffset.UnixEpoch,
                "test-boundary"));
    }

    private static IEnumerable<LedgerRecord> GenerateRecords(int count)
    {
        string previous = LedgerFormat.GenesisFingerprint;

        for (int index = 1; index <= count; index++)
        {
            LedgerRecordCore core = new(
                LedgerFormat.ArtifactType,
                "streaming-ledger",
                index,
                $"record-{index:D6}",
                previous,
                LedgerFormat.RecordSchemaVersion,
                LedgerFormat.CanonicalizationVersion,
                LedgerFormat.HashAlgorithm,
                new GovernanceDecisionReceipt(
                    $"decision-{index:D6}",
                    "document.review",
                    $"document-{index:D6}",
                    index % 2 == 0 ? "Allowed" : "Denied",
                    "document-policy",
                    "1.0",
                    DateTimeOffset.UnixEpoch.AddSeconds(index)));

            LedgerRecord record = CreateRecord(core);
            yield return record;
            previous = record.Fingerprint;
        }
    }

    private static (InMemoryDecisionLedger Ledger, IReadOnlyList<LedgerRecord> Records, LedgerCheckpoint Checkpoint)
        TwoRecordLedger()
    {
        InMemoryDecisionLedger ledger = new("governance-east");
        ledger.Append("record-001", FirstReceipt());
        ledger.Append(
            "record-002",
            FirstReceipt() with
            {
                DecisionId = "dec-002",
                ResourceId = "account-456",
                Outcome = "Denied",
                OccurredUtc = new DateTimeOffset(2026, 8, 28, 12, 1, 0, TimeSpan.Zero)
            });

        LedgerCheckpoint checkpoint = ledger.CreateCheckpoint(
            new DateTimeOffset(2026, 8, 28, 12, 2, 0, TimeSpan.Zero),
            "independent-checkpoint-store");

        return (ledger, ledger.Snapshot(), checkpoint);
    }

    private static LedgerRecord CreateRecord(LedgerRecordCore core) => new(
        core.ArtifactType,
        core.LedgerId,
        core.SequenceNumber,
        core.RecordId,
        core.PreviousFingerprint,
        core.RecordSchemaVersion,
        core.CanonicalizationVersion,
        core.HashAlgorithm,
        core.Receipt,
        CanonicalLedgerEncoding.ComputeFingerprint(core));

    private static LedgerRecordCore FirstCore() => new(
        LedgerFormat.ArtifactType,
        "governance-east",
        1,
        "record-001",
        LedgerFormat.GenesisFingerprint,
        LedgerFormat.RecordSchemaVersion,
        LedgerFormat.CanonicalizationVersion,
        LedgerFormat.HashAlgorithm,
        FirstReceipt());

    private static GovernanceDecisionReceipt FirstReceipt() => new(
        "dec-001",
        "account.disable",
        "account-123",
        "Allowed",
        "account-admin",
        "7.4",
        new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero));
}
