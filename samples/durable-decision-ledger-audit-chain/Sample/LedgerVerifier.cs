namespace DurableDecisionLedgerAuditChain;

public static class LedgerVerifier
{
    public static LedgerVerificationResult Verify(
        IEnumerable<LedgerRecord> records,
        string expectedLedgerId,
        LedgerCheckpoint? checkpoint = null,
        LedgerVerificationStart? start = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedLedgerId);

        bool callerSuppliedStart = start is not null;
        LedgerVerificationStart verificationStart = start ?? LedgerVerificationStart.Genesis;
        string? startDetail = callerSuppliedStart
            ? $"Verification began from caller-supplied boundary metadata '{SanitizeDetailValue(verificationStart.TrustBoundary)}'."
            : null;

        if (verificationStart.LedgerId is not null &&
            !string.Equals(verificationStart.LedgerId, expectedLedgerId, StringComparison.Ordinal))
        {
            return new LedgerVerificationResult(
                LedgerIntegrityStatus.LedgerMismatch,
                LedgerCompletenessStatus.NotEvaluated,
                0,
                Detail: CombineDetail("Verification start boundary belongs to another ledger.", startDetail));
        }

        long expectedSequence = verificationStart.ExpectedSequenceNumber;
        long recordsVerified = 0;
        long? verifiedThroughSequence = null;
        string expectedPrevious = verificationStart.ExpectedPreviousFingerprint;
        string? checkpointObservedFingerprint = null;

        foreach (LedgerRecord record in records)
        {
            if (!string.Equals(record.LedgerId, expectedLedgerId, StringComparison.Ordinal))
            {
                return Failed(
                    LedgerIntegrityStatus.LedgerMismatch,
                    recordsVerified,
                    verifiedThroughSequence,
                    record.SequenceNumber,
                    "Record belongs to another ledger.",
                    startDetail);
            }

            if (record.SequenceNumber != expectedSequence)
            {
                return Failed(
                    LedgerIntegrityStatus.SequenceMismatch,
                    recordsVerified,
                    verifiedThroughSequence,
                    record.SequenceNumber,
                    $"Expected sequence {expectedSequence}; observed {record.SequenceNumber}.",
                    startDetail);
            }

            LedgerIntegrityStatus? formatFailure = ValidateSupportedFormat(record);
            if (formatFailure is not null)
            {
                return Failed(
                    formatFailure.Value,
                    recordsVerified,
                    verifiedThroughSequence,
                    record.SequenceNumber,
                    "Record format is not supported by this teaching verifier.",
                    startDetail);
            }

            if (!string.Equals(record.PreviousFingerprint, expectedPrevious, StringComparison.Ordinal))
            {
                return Failed(
                    LedgerIntegrityStatus.BrokenLink,
                    recordsVerified,
                    verifiedThroughSequence,
                    record.SequenceNumber,
                    "Previous fingerprint does not match the trusted verification boundary.",
                    startDetail);
            }

            string recomputed;
            try
            {
                recomputed = CanonicalLedgerEncoding.ComputeFingerprint(record.ToCore());
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                return Failed(
                    LedgerIntegrityStatus.RecordNotCanonicalizable,
                    recordsVerified,
                    verifiedThroughSequence,
                    record.SequenceNumber,
                    "Record cannot be reproduced under the supported canonicalization contract.",
                    startDetail);
            }

            if (!string.Equals(record.Fingerprint, recomputed, StringComparison.Ordinal))
            {
                return Failed(
                    LedgerIntegrityStatus.FingerprintMismatch,
                    recordsVerified,
                    verifiedThroughSequence,
                    record.SequenceNumber,
                    "Stored fingerprint does not match canonical record bytes.",
                    startDetail);
            }

            recordsVerified++;
            verifiedThroughSequence = record.SequenceNumber;
            expectedSequence = checked(record.SequenceNumber + 1);
            expectedPrevious = record.Fingerprint;

            if (checkpoint is not null && record.SequenceNumber == checkpoint.SequenceNumber)
            {
                checkpointObservedFingerprint = record.Fingerprint;
            }
        }

        if (recordsVerified == 0)
        {
            LedgerCompletenessStatus emptyCompleteness = checkpoint is null
                ? LedgerCompletenessStatus.NotEvaluated
                : EvaluateCheckpoint(
                    expectedLedgerId,
                    verificationStart,
                    verifiedThroughSequence,
                    checkpointObservedFingerprint,
                    checkpoint);

            return new LedgerVerificationResult(
                LedgerIntegrityStatus.EmptyInput,
                emptyCompleteness,
                0,
                Detail: startDetail is null
                    ? "No ledger records were supplied for verification."
                    : $"No ledger records were supplied for verification. {startDetail}");
        }

        LedgerCompletenessStatus completeness = EvaluateCheckpoint(
            expectedLedgerId,
            verificationStart,
            verifiedThroughSequence,
            checkpointObservedFingerprint,
            checkpoint);

        return new LedgerVerificationResult(
            LedgerIntegrityStatus.Verified,
            completeness,
            recordsVerified,
            verifiedThroughSequence,
            Detail: startDetail);
    }

    private static LedgerIntegrityStatus? ValidateSupportedFormat(LedgerRecord record)
    {
        if (!string.Equals(record.ArtifactType, LedgerFormat.ArtifactType, StringComparison.Ordinal))
        {
            return LedgerIntegrityStatus.UnsupportedArtifactType;
        }

        if (!string.Equals(record.RecordSchemaVersion, LedgerFormat.RecordSchemaVersion, StringComparison.Ordinal))
        {
            return LedgerIntegrityStatus.UnsupportedRecordSchemaVersion;
        }

        if (!string.Equals(record.CanonicalizationVersion, LedgerFormat.CanonicalizationVersion, StringComparison.Ordinal))
        {
            return LedgerIntegrityStatus.CanonicalizationUnavailable;
        }

        if (!string.Equals(record.HashAlgorithm, LedgerFormat.HashAlgorithm, StringComparison.Ordinal))
        {
            return LedgerIntegrityStatus.UnsupportedHashAlgorithm;
        }

        return null;
    }

    private static LedgerCompletenessStatus EvaluateCheckpoint(
        string expectedLedgerId,
        LedgerVerificationStart start,
        long? verifiedThroughSequence,
        string? checkpointObservedFingerprint,
        LedgerCheckpoint? checkpoint)
    {
        if (checkpoint is null)
        {
            return LedgerCompletenessStatus.UnknownWithoutCheckpoint;
        }

        if (!string.Equals(checkpoint.LedgerId, expectedLedgerId, StringComparison.Ordinal))
        {
            return LedgerCompletenessStatus.CheckpointMismatch;
        }

        long boundarySequence = start.ExpectedSequenceNumber - 1;
        long observedHeadSequence = verifiedThroughSequence ?? boundarySequence;

        if (checkpoint.SequenceNumber < boundarySequence)
        {
            return LedgerCompletenessStatus.CheckpointOutsideVerifiedRange;
        }

        // A verifier may resume immediately after a previously trusted checkpoint.
        if (checkpoint.SequenceNumber == boundarySequence)
        {
            if (!string.Equals(
                    checkpoint.HeadFingerprint,
                    start.ExpectedPreviousFingerprint,
                    StringComparison.Ordinal))
            {
                return LedgerCompletenessStatus.CheckpointMismatch;
            }

            // With no supplied records, a checkpoint copied into the start boundary
            // only confirms itself. No segment bytes were verified against that head.
            if (verifiedThroughSequence is null)
            {
                return LedgerCompletenessStatus.NotEvaluated;
            }

            return LedgerCompletenessStatus.VerifiedPastCheckpoint;
        }

        if (checkpoint.SequenceNumber > observedHeadSequence)
        {
            return LedgerCompletenessStatus.MissingCheckpointedTail;
        }

        if (!string.Equals(
                checkpointObservedFingerprint,
                checkpoint.HeadFingerprint,
                StringComparison.Ordinal))
        {
            return LedgerCompletenessStatus.CheckpointMismatch;
        }

        return checkpoint.SequenceNumber == observedHeadSequence
            ? LedgerCompletenessStatus.MatchesCheckpoint
            : LedgerCompletenessStatus.VerifiedPastCheckpoint;
    }

    private static LedgerVerificationResult Failed(
        LedgerIntegrityStatus integrityStatus,
        long recordsVerified,
        long? verifiedThroughSequence,
        long sequenceNumber,
        string detail,
        string? startDetail) =>
        new(
            integrityStatus,
            LedgerCompletenessStatus.NotEvaluated,
            recordsVerified,
            verifiedThroughSequence,
            sequenceNumber,
            CombineDetail(detail, startDetail));

    private static string CombineDetail(string detail, string? startDetail) =>
        startDetail is null ? detail : $"{detail} {startDetail}";

    private static string SanitizeDetailValue(string value)
    {
        const int maxLength = 120;
        Span<char> buffer = stackalloc char[Math.Min(value.Length, maxLength)];
        int length = 0;

        foreach (char character in value)
        {
            if (length == buffer.Length)
            {
                break;
            }

            buffer[length++] = char.IsControl(character) || character is '\u2028' or '\u2029'
                ? '?'
                : character;
        }

        return new string(buffer[..length]);
    }
}
