namespace CrossSystemCapabilityExchange;

public sealed record DelegationHop(
    string Issuer,
    string DelegatedTo,
    int HopPosition,
    int RemainingDelegationDepth);

public sealed record CrossSystemCapability(
    string CapabilityId,
    string Issuer,
    string Audience,
    string OriginatingSubject,
    string PresenterBinding,
    string Operation,
    string ResourceId,
    string ResourceVersion,
    string Purpose,
    string RequestDigest,
    string IssuerDecisionId,
    string IssuerPolicyVersion,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    int MaxUses,
    int RemainingDelegationDepth,
    IReadOnlyList<DelegationHop> DelegationChain);

public sealed record SimulatedProof(bool IsValid);

public sealed record ProtectedCapabilityArtifact(
    CrossSystemCapability Capability,
    string KeyId,
    SimulatedProof Proof);

public sealed record RecipientExportContext(
    string CorrelationId,
    string Audience,
    string AuthenticatedPresenter,
    string Operation,
    string ResourceId,
    string ResourceVersion,
    string Purpose,
    string RequestDigest,
    bool LocalPolicyAllows,
    DateTimeOffset NowUtc);

public sealed record RecipientIssuerPolicy(
    string Issuer,
    string RecipientSystemId,
    IReadOnlySet<string> AcceptedKeyIds,
    string Audience,
    string Operation,
    TimeSpan MaxLifetime,
    TimeSpan MaxClockSkew,
    int MaximumRemainingDelegationDepth,
    bool AllowChainedDelegation);

public sealed record CapabilityValidationResult(
    bool Accepted,
    string ReasonCode)
{
    public static CapabilityValidationResult Accept() =>
        new(true, "capability.accepted");

    public static CapabilityValidationResult Reject(string reasonCode) =>
        new(false, reasonCode);
}

public sealed record CapabilityClaimResult(
    bool Accepted,
    string ReasonCode);

public sealed record ValidatedExportCommand(
    string ExecutionId,
    string RecipientDecisionId,
    string OriginatingSubject,
    string IssuerDecisionId,
    string ResourceId,
    string ResourceVersion,
    string Destination,
    string Purpose,
    string CapabilityId,
    string CorrelationId);

public sealed record ExportExecutionResult(
    bool Succeeded,
    string ReasonCode)
{
    public static ExportExecutionResult Success() =>
        new(true, "executor.completed");

    public static ExportExecutionResult Reject(string reasonCode) =>
        new(false, reasonCode);
}

public sealed record GatewayResult(
    bool Executed,
    string InternalReasonCode,
    string PublicReasonCode,
    string RecipientDecisionId,
    string? ExecutionId)
{
    public static GatewayResult Rejected(
        string recipientDecisionId,
        string internalReasonCode) =>
        new(
            false,
            internalReasonCode,
            "request.not-accepted",
            recipientDecisionId,
            null);

    public static GatewayResult ExecutionFailed(
        string recipientDecisionId,
        string executionId,
        string internalReasonCode) =>
        new(
            false,
            internalReasonCode,
            "request.not-completed",
            recipientDecisionId,
            executionId);

    public static GatewayResult ExecutedSuccessfully(
        string recipientDecisionId,
        string executionId) =>
        new(
            true,
            "execution.completed",
            "request.completed",
            recipientDecisionId,
            executionId);
}
