using FsCheck;
using FsCheck.Fluent;
using Xunit;

namespace ReplayProtectionAndBoundedUse.Tests;

public sealed class ReplayProtectionPropertyTests
{
    private static readonly Config FuzzConfig =
        Config.QuickThrowOnFailure.WithMaxTest(250);

    [Fact]
    public void ArbitraryMismatchedSubjectsAreRejected()
    {
        Check.One(
            FuzzConfig,
            Prop.ForAll<string?>(untrustedInput =>
            {
                const string expectedSubject = "operator-7";
                string mismatchedSubject = string.Concat(
                    "untrusted-subject:",
                    untrustedInput);

                var capability = new ExecutionCapability(
                    CapabilityId: "cap-fuzz-subject",
                    SubjectId: expectedSubject,
                    OperationName: "account.disable",
                    ResourceId: "user-100",
                    Audience: "account-admin-gateway",
                    IssuedUtc: DateTimeOffset.UnixEpoch,
                    ExpiresUtc: DateTimeOffset.UnixEpoch.AddMinutes(5),
                    MaximumUses: 1);

                var request = new CapabilityValidationRequest(
                    SubjectId: mismatchedSubject,
                    OperationName: capability.OperationName,
                    ResourceId: capability.ResourceId,
                    Audience: capability.Audience,
                    NowUtc: capability.IssuedUtc.AddMinutes(1));

                CapabilityValidationResult result =
                    new ExecutionCapabilityValidator().Validate(
                        capability,
                        request);

                return !result.IsValid &&
                    result.ReasonCode == "capability.subject-mismatch";
            }));
    }

    [Fact]
    public void ArbitraryBoundedUseLimitsAreNeverExceeded()
    {
        Check.One(
            FuzzConfig,
            Prop.ForAll<int>(generatedValue =>
            {
                int maximumUses = Math.Abs(generatedValue % 16) + 1;
                string capabilityId = $"cap-fuzz-{generatedValue:X8}";
                var store = new AtomicInMemoryCapabilityUseStore();

                CapabilityUseResult[] results = Enumerable
                    .Range(0, maximumUses + 2)
                    .Select(_ => store.TryConsumeAsync(
                        capabilityId,
                        maximumUses,
                        DateTimeOffset.UnixEpoch,
                        CancellationToken.None)
                        .AsTask()
                        .GetAwaiter()
                        .GetResult())
                    .ToArray();

                return results.Count(result => result.Accepted) ==
                        maximumUses &&
                    results.Skip(maximumUses).All(result =>
                        !result.Accepted &&
                        result.ReasonCode ==
                            "capability.use-limit-exceeded") &&
                    store.GetObservedUseCount(capabilityId) == maximumUses;
            }));
    }
}
