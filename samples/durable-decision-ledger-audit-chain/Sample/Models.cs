namespace DurableDecisionLedgerAuditChain;

public static class LedgerFormat
{
    public const string ArtifactType = "accountable-systems-governance-ledger-record/v1";
    public const string RecordSchemaVersion = "ledger-record/v1";
    public const string CanonicalizationVersion = "canonical-json/v1";
    public const string HashAlgorithm = "SHA-256";
    public const string GenesisFingerprint = "GENESIS/v1";
}

public sealed record GovernanceDecisionReceipt(
    string DecisionId,
    string Operation,
    string ResourceId,
    string Outcome,
    string PolicyId,
    string PolicyVersion,
    DateTimeOffset OccurredUtc);

public sealed record LedgerRecordCore(
    string ArtifactType,
    string LedgerId,
    long SequenceNumber,
    string RecordId,
    string PreviousFingerprint,
    string RecordSchemaVersion,
    string CanonicalizationVersion,
    string HashAlgorithm,
    GovernanceDecisionReceipt Receipt);

public sealed record LedgerRecord(
    string ArtifactType,
    string LedgerId,
    long SequenceNumber,
    string RecordId,
    string PreviousFingerprint,
    string RecordSchemaVersion,
    string CanonicalizationVersion,
    string HashAlgorithm,
    GovernanceDecisionReceipt Receipt,
    string Fingerprint)
{
    public LedgerRecordCore ToCore() => new(
        ArtifactType,
        LedgerId,
        SequenceNumber,
        RecordId,
        PreviousFingerprint,
        RecordSchemaVersion,
        CanonicalizationVersion,
        HashAlgorithm,
        Receipt);
}

public sealed record LedgerCheckpoint
{
    public LedgerCheckpoint(
        string ledgerId,
        long sequenceNumber,
        string headFingerprint,
        DateTimeOffset observedUtc,
        string custodyBoundary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ledgerId);
        ArgumentOutOfRangeException.ThrowIfLessThan(sequenceNumber, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(headFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(custodyBoundary);

        LedgerId = ledgerId;
        SequenceNumber = sequenceNumber;
        HeadFingerprint = headFingerprint;
        ObservedUtc = observedUtc.ToUniversalTime();
        CustodyBoundary = custodyBoundary;
    }

    public string LedgerId { get; }
    public long SequenceNumber { get; }
    public string HeadFingerprint { get; }
    public DateTimeOffset ObservedUtc { get; }
    public string CustodyBoundary { get; }
}

public sealed record LedgerVerificationStart
{
    public LedgerVerificationStart(
        long expectedSequenceNumber,
        string expectedPreviousFingerprint,
        string trustBoundary,
        string? ledgerId = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedSequenceNumber, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedPreviousFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(trustBoundary);

        if (ledgerId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ledgerId);
        }

        ExpectedSequenceNumber = expectedSequenceNumber;
        ExpectedPreviousFingerprint = expectedPreviousFingerprint;
        TrustBoundary = trustBoundary;
        LedgerId = ledgerId;
    }

    public long ExpectedSequenceNumber { get; }
    public string ExpectedPreviousFingerprint { get; }
    public string TrustBoundary { get; }
    public string? LedgerId { get; }

    public static LedgerVerificationStart Genesis { get; } = new(
        1,
        LedgerFormat.GenesisFingerprint,
        "explicit-genesis");

    public static LedgerVerificationStart FromCheckpoint(LedgerCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        return new LedgerVerificationStart(
            checked(checkpoint.SequenceNumber + 1),
            checkpoint.HeadFingerprint,
            $"checkpoint:{checkpoint.CustodyBoundary}",
            checkpoint.LedgerId);
    }
}

public enum LedgerIntegrityStatus
{
    Verified,
    EmptyInput,
    LedgerMismatch,
    SequenceMismatch,
    BrokenLink,
    UnsupportedArtifactType,
    UnsupportedRecordSchemaVersion,
    CanonicalizationUnavailable,
    UnsupportedHashAlgorithm,
    RecordNotCanonicalizable,
    FingerprintMismatch
}

public enum LedgerCompletenessStatus
{
    NotEvaluated,
    UnknownWithoutCheckpoint,
    MatchesCheckpoint,
    VerifiedPastCheckpoint,
    MissingCheckpointedTail,
    CheckpointOutsideVerifiedRange,
    CheckpointMismatch
}

public sealed record LedgerVerificationResult(
    LedgerIntegrityStatus IntegrityStatus,
    LedgerCompletenessStatus CompletenessStatus,
    long RecordsVerified,
    long? VerifiedThroughSequence = null,
    long? FailureSequence = null,
    string? Detail = null)
{
    public bool IntegrityVerified => IntegrityStatus == LedgerIntegrityStatus.Verified;
}
