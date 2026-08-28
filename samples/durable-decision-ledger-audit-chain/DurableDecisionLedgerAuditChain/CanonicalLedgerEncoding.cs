using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DurableDecisionLedgerAuditChain;

public static class CanonicalLedgerEncoding
{
    public static byte[] Encode(LedgerRecordCore record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(record.Receipt);
        ValidateCore(record);

        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter writer = new(
            buffer,
            new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = false,
                SkipValidation = false
            });

        // canonical-json/v1 uses explicit ordinal property order. The outer names are
        // lexicographic to make comparison with RFC 8785-style ordering easier, but
        // this teaching format is still not presented as JCS.
        writer.WriteStartObject();
        writer.WriteString("artifactType", record.ArtifactType);
        writer.WriteString("canonicalizationVersion", record.CanonicalizationVersion);
        writer.WriteString("hashAlgorithm", record.HashAlgorithm);
        writer.WriteString("ledgerId", record.LedgerId);
        writer.WriteString("previousFingerprint", record.PreviousFingerprint);

        writer.WritePropertyName("receipt");
        writer.WriteStartObject();
        writer.WriteString("decisionId", record.Receipt.DecisionId);
        writer.WriteString(
            "occurredUtc",
            record.Receipt.OccurredUtc
                .ToUniversalTime()
                .ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture));
        writer.WriteString("operation", record.Receipt.Operation);
        writer.WriteString("outcome", record.Receipt.Outcome);
        writer.WriteString("policyId", record.Receipt.PolicyId);
        writer.WriteString("policyVersion", record.Receipt.PolicyVersion);
        writer.WriteString("resourceId", record.Receipt.ResourceId);
        writer.WriteEndObject();

        writer.WriteString("recordId", record.RecordId);
        writer.WriteString("recordSchemaVersion", record.RecordSchemaVersion);
        // Decimal text avoids losing a 64-bit sequence number in JSON consumers that
        // otherwise coerce numbers to IEEE-754 double precision.
        writer.WriteString("sequenceNumber", record.SequenceNumber.ToString(CultureInfo.InvariantCulture));
        writer.WriteEndObject();
        writer.Flush();

        return buffer.WrittenSpan.ToArray();
    }

    public static string ComputeFingerprint(LedgerRecordCore record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (!string.Equals(record.HashAlgorithm, LedgerFormat.HashAlgorithm, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"The teaching sample supports only {LedgerFormat.HashAlgorithm}; received {record.HashAlgorithm}.");
        }

        byte[] digest = SHA256.HashData(Encode(record));
        return Convert.ToHexStringLower(digest);
    }

    public static string EncodeAsUtf8Text(LedgerRecordCore record) =>
        Encoding.UTF8.GetString(Encode(record));

    private static void ValidateCore(LedgerRecordCore record)
    {
        // Maintenance invariant: validation added here becomes adversarial verifier input.
        // LedgerVerifier must map canonicalization exceptions to an evidence category
        // instead of allowing encoder validation failures to escape the verifier boundary.
        RequireText(record.ArtifactType, nameof(record.ArtifactType));
        RequireText(record.LedgerId, nameof(record.LedgerId));
        ArgumentOutOfRangeException.ThrowIfLessThan(record.SequenceNumber, 1);
        RequireText(record.RecordId, nameof(record.RecordId));
        RequireText(record.PreviousFingerprint, nameof(record.PreviousFingerprint));
        RequireText(record.RecordSchemaVersion, nameof(record.RecordSchemaVersion));
        RequireText(record.CanonicalizationVersion, nameof(record.CanonicalizationVersion));
        RequireText(record.HashAlgorithm, nameof(record.HashAlgorithm));

        RequireText(record.Receipt.DecisionId, nameof(record.Receipt.DecisionId));
        RequireText(record.Receipt.Operation, nameof(record.Receipt.Operation));
        RequireText(record.Receipt.ResourceId, nameof(record.Receipt.ResourceId));
        RequireText(record.Receipt.Outcome, nameof(record.Receipt.Outcome));
        RequireText(record.Receipt.PolicyId, nameof(record.Receipt.PolicyId));
        RequireText(record.Receipt.PolicyVersion, nameof(record.Receipt.PolicyVersion));
    }

    private static void RequireText(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        ValidateUnicodeScalarValues(value, parameterName);
    }

    private static void ValidateUnicodeScalarValues(string value, string parameterName)
    {
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (char.IsHighSurrogate(current))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                {
                    throw new ArgumentException(
                        "Canonical text must contain valid Unicode scalar values; unpaired surrogates are rejected.",
                        parameterName);
                }

                index++;
            }
            else if (char.IsLowSurrogate(current))
            {
                throw new ArgumentException(
                    "Canonical text must contain valid Unicode scalar values; unpaired surrogates are rejected.",
                    parameterName);
            }
        }
    }
}
