namespace DistributedAcknowledgmentContinuation;

public sealed record AcknowledgmentChallenge(
    string ChallengeId,
    string OriginatingDecisionId,
    string RequesterActorId,
    string RequiredResponderId,
    string Operation,
    string ResourceId,
    string ResourceVersionAtChallenge,
    string IntentCanonicalizationVersion,
    string IntentDigest,
    string RequirementCode,
    string PresentationVersion,
    string PresentationDigest,
    string PolicyId,
    string PolicyVersion,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string CorrelationId);

public sealed record AcknowledgmentEvidence(
    string EvidenceId,
    string EvidenceIssuer,
    string ChallengeId,
    string ResponseId,
    string ResponderId,
    string IntentCanonicalizationVersion,
    string IntentDigest,
    string PresentationVersion,
    string PresentationDigest,
    bool Accepted,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId);

public sealed record ContinuationRequest(string ContinuationId);

public sealed record ContinuationState(
    string ContinuationId,
    string ChallengeId,
    string CorrelationId);

public enum EvidenceVerificationStatus
{
    Trusted,
    Untrusted,
    Unavailable
}

public sealed record EvidenceVerificationResult(
    EvidenceVerificationStatus Status,
    string ReasonCode);

public sealed record CurrentContinuationContext(
    string OriginatingActorId,
    string Operation,
    string ResourceId,
    string ResourceVersion,
    string IntentCanonicalizationVersion,
    string IntentDigest,
    string CorrelationId,
    DateTimeOffset NowUtc);

public enum CurrentContextStatus
{
    Available,
    Unavailable
}

public sealed record CurrentContextResult(
    CurrentContextStatus Status,
    CurrentContinuationContext? Context,
    string ReasonCode);

public sealed record CurrentPolicyDecision(
    bool Allowed,
    string DecisionId,
    string PolicyId,
    string PolicyVersion,
    string? RequiredAcknowledgmentCode,
    string ReasonCode);

public sealed record ContinuationClaimResult(
    bool Claimed,
    string ReasonCode,
    string? ClaimId);

public sealed record ScopedContinuationAuthority(
    string AuthorityId,
    string ContinuationId,
    string Audience,
    string Operation,
    string ResourceId,
    string ResourceVersion,
    string ChallengeId,
    string EvidenceId,
    string CurrentPolicyId,
    string CurrentPolicyVersion,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record ContinuationExecutionResult(
    bool Executed,
    string ReasonCode)
{
    public static ContinuationExecutionResult Success() =>
        new(true, "executor.completed");

    public static ContinuationExecutionResult Reject(string reasonCode) =>
        new(false, reasonCode);
}

public sealed record ValidatedContinuationCommand(
    string ExecutionId,
    string ContinuationAuthorityId,
    string ContinuationId,
    string OriginatingDecisionId,
    string ChallengeId,
    string EvidenceId,
    string OriginatingActorId,
    string ResponderId,
    string Operation,
    string ResourceId,
    string ExpectedResourceVersion,
    string AcknowledgmentRequirementCode,
    string? CurrentRequiredAcknowledgmentCode,
    string PresentationVersion,
    string PresentationDigest,
    string OriginatingPolicyId,
    string OriginatingPolicyVersion,
    string CurrentDecisionId,
    string CurrentPolicyId,
    string CurrentPolicyVersion,
    string CorrelationId);

public sealed record GatewayResult(
    bool Executed,
    string InternalReasonCode,
    string PublicReasonCode,
    string? ContinuationAuthorityId,
    string? ExecutionId)
{
    public static GatewayResult Rejected(string reasonCode) =>
        new(
            false,
            reasonCode,
            "request.not-continued",
            null,
            null);

    public static GatewayResult ExecutionRejected(
        string authorityId,
        string executionId,
        string reasonCode) =>
        new(
            false,
            reasonCode,
            "request.not-completed",
            authorityId,
            executionId);

    public static GatewayResult ExecutedSuccessfully(
        string authorityId,
        string executionId) =>
        new(
            true,
            "execution.completed",
            "request.completed",
            authorityId,
            executionId);
}
