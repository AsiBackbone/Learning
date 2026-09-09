namespace DurableDecisionLedgerAuditChain;

public static class Program
{
    public static void Main()
    {
        InMemoryDecisionLedger ledger = new("governance-east");

        GovernanceDecisionReceipt firstReceipt = Receipt(
            "dec-001",
            "account.disable",
            "account-123",
            "Allowed",
            "account-admin",
            "7.4",
            new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero));

        LedgerRecord first = ledger.Append("record-001", firstReceipt);
        LedgerRecord retry = ledger.Append("record-001", firstReceipt);

        LedgerCheckpoint firstCheckpoint = ledger.CreateCheckpoint(
            new DateTimeOffset(2026, 8, 28, 12, 0, 30, TimeSpan.Zero),
            "archive-segment-checkpoint");

        LedgerRecord second = ledger.Append(
            "record-002",
            Receipt(
                "dec-002",
                "account.disable",
                "account-456",
                "Denied",
                "account-admin",
                "7.4",
                new DateTimeOffset(2026, 8, 28, 12, 1, 0, TimeSpan.Zero)));

        LedgerCheckpoint checkpoint = ledger.CreateCheckpoint(
            new DateTimeOffset(2026, 8, 28, 12, 2, 0, TimeSpan.Zero),
            "independent-checkpoint-store");

        IReadOnlyList<LedgerRecord> snapshot = ledger.Snapshot();

        Console.WriteLine("Durable Decision Ledger and Audit Chain");
        Console.WriteLine($"Records: {snapshot.Count}");
        Console.WriteLine($"Idempotent retry reused sequence: {first.SequenceNumber == retry.SequenceNumber}");
        Console.WriteLine($"Head: {second.SequenceNumber} / {second.Fingerprint[..12]}...");
        Console.WriteLine();

        Show("Verified chain + matching checkpoint", LedgerVerifier.Verify(snapshot, ledger.LedgerId, checkpoint));

        LedgerRecord modifiedFirst = first with
        {
            Receipt = first.Receipt with { Outcome = "Denied" }
        };
        Show(
            "Middle record modified",
            LedgerVerifier.Verify([modifiedFirst, second], ledger.LedgerId, checkpoint));

        Show(
            "Newest record truncated, no checkpoint",
            LedgerVerifier.Verify([first], ledger.LedgerId));

        Show(
            "Newest record truncated, later checkpoint retained (teaching model)",
            LedgerVerifier.Verify([first], ledger.LedgerId, checkpoint));

        Show(
            "Records reordered",
            LedgerVerifier.Verify([second, first], ledger.LedgerId, checkpoint));

        Show(
            "Verification resumed after trusted checkpoint",
            LedgerVerifier.Verify(
                [second],
                ledger.LedgerId,
                checkpoint,
                LedgerVerificationStart.FromCheckpoint(firstCheckpoint)));

        LedgerRecord unsupportedHash = first with { HashAlgorithm = "SHA-512" };
        Show(
            "Unsupported hash algorithm is categorized, not thrown",
            LedgerVerifier.Verify([unsupportedHash], ledger.LedgerId));

        Console.WriteLine("Teaching boundary:");
        Console.WriteLine("- deterministic bytes and fingerprints are implemented");
        Console.WriteLine("- append idempotency is checked before head reconstruction");
        Console.WriteLine("- integrity and tail completeness are reported separately");
        Console.WriteLine("- checkpoint custody is modeled, not cryptographically implemented");
        Console.WriteLine("- a retained checkpoint can expose an alternate whole-chain view; cross-verifier equivocation detection still requires gossip/witnessing outside this sample");
        Console.WriteLine("- signatures, key rotation, and RFC 3161 timestamps are intentionally out of scope");
        Console.WriteLine("- a ledger record is historical evidence, never execution authority");
    }

    private static GovernanceDecisionReceipt Receipt(
        string decisionId,
        string operation,
        string resourceId,
        string outcome,
        string policyId,
        string policyVersion,
        DateTimeOffset occurredUtc) =>
        new(
            decisionId,
            operation,
            resourceId,
            outcome,
            policyId,
            policyVersion,
            occurredUtc);

    private static void Show(
        string title,
        LedgerVerificationResult result)
    {
        Console.WriteLine(title);
        Console.WriteLine($"  Integrity: {result.IntegrityStatus}");
        Console.WriteLine($"  Completeness: {result.CompletenessStatus}");
        Console.WriteLine($"  Records verified: {result.RecordsVerified}");

        if (result.VerifiedThroughSequence is not null)
        {
            Console.WriteLine($"  Verified through sequence: {result.VerifiedThroughSequence}");
        }

        if (result.FailureSequence is not null)
        {
            Console.WriteLine($"  Failure sequence: {result.FailureSequence}");
        }

        if (result.Detail is not null)
        {
            Console.WriteLine($"  Detail: {result.Detail}");
        }

        Console.WriteLine();
    }
}
