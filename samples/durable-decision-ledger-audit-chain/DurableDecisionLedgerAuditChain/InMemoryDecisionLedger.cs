namespace DurableDecisionLedgerAuditChain;

public sealed class InMemoryDecisionLedger
{
    private readonly object _gate = new();
    private readonly List<LedgerRecord> _records = [];
    private readonly Dictionary<string, LedgerRecord> _recordsById = new(StringComparer.Ordinal);

    public InMemoryDecisionLedger(string ledgerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ledgerId);
        LedgerId = ledgerId;
    }

    public string LedgerId { get; }

    public LedgerRecord Append(
        string recordId,
        GovernanceDecisionReceipt receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        ArgumentNullException.ThrowIfNull(receipt);

        lock (_gate)
        {
            // Idempotency is checked before sequence/head reconstruction.
            if (_recordsById.TryGetValue(recordId, out LedgerRecord? existing))
            {
                if (existing.Receipt != receipt)
                {
                    throw new InvalidOperationException(
                        $"RecordId '{recordId}' already exists with different governance evidence.");
                }

                return existing;
            }

            long sequenceNumber = _records.Count == 0
                ? 1
                : _records[^1].SequenceNumber + 1;

            string previousFingerprint = _records.Count == 0
                ? LedgerFormat.GenesisFingerprint
                : _records[^1].Fingerprint;

            LedgerRecordCore core = new(
                LedgerFormat.ArtifactType,
                LedgerId,
                sequenceNumber,
                recordId,
                previousFingerprint,
                LedgerFormat.RecordSchemaVersion,
                LedgerFormat.CanonicalizationVersion,
                LedgerFormat.HashAlgorithm,
                receipt);

            LedgerRecord record = new(
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

            _records.Add(record);
            _recordsById.Add(recordId, record);
            return record;
        }
    }

    public IReadOnlyList<LedgerRecord> Snapshot()
    {
        lock (_gate)
        {
            return _records.ToArray();
        }
    }

    public LedgerCheckpoint CreateCheckpoint(
        DateTimeOffset observedUtc,
        string custodyBoundary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(custodyBoundary);

        lock (_gate)
        {
            if (_records.Count == 0)
            {
                throw new InvalidOperationException("Cannot checkpoint an empty ledger.");
            }

            LedgerRecord head = _records[^1];
            return new LedgerCheckpoint(
                LedgerId,
                head.SequenceNumber,
                head.Fingerprint,
                observedUtc.ToUniversalTime(),
                custodyBoundary);
        }
    }
}
