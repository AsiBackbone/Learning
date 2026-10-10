using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GovernedFailureInjectionTrace;

/// <summary>
/// SIMULATED verification boundary. This is not production cryptography.
/// </summary>
/// <remarks>
/// The seal is an unkeyed SHA-256 digest of the receipt's canonical fields. It lets the
/// sample show where a verifier detects an altered receipt, but anyone who can read the
/// fields can recompute it, so it cannot detect a forged receipt. A production system
/// uses a keyed signature or MAC with managed, rotated keys, or relies on a store the
/// caller cannot write.
/// </remarks>
public static class SimulatedReceiptSeal
{
    public static string Compute(DecisionReceipt receipt)
    {
        string canonical = string.Join(
            '|',
            receipt.ReceiptId,
            receipt.IntentId,
            receipt.ActorId,
            receipt.ResourceId,
            receipt.Operation,
            receipt.Outcome.ToString(),
            receipt.ReasonCode,
            receipt.PolicyId,
            receipt.PolicyVersion,
            receipt.IssuedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            receipt.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static DecisionReceipt Apply(DecisionReceipt receipt)
    {
        return receipt with { Seal = Compute(receipt) };
    }

    public static bool Matches(DecisionReceipt receipt)
    {
        return string.Equals(receipt.Seal, Compute(receipt), StringComparison.Ordinal);
    }
}
