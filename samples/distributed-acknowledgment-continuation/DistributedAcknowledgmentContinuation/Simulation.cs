namespace DistributedAcknowledgmentContinuation;

public sealed class SimulatedAcknowledgmentEvidenceVerifier(
    string trustedEvidenceIssuer,
    EvidenceVerificationStatus forcedStatus =
        EvidenceVerificationStatus.Trusted)
    : IAcknowledgmentEvidenceVerifier
{
    public EvidenceVerificationResult Verify(
        AcknowledgmentEvidence evidence)
    {
        if (forcedStatus == EvidenceVerificationStatus.Unavailable)
        {
            return new EvidenceVerificationResult(
                EvidenceVerificationStatus.Unavailable,
                "evidence.verification-unavailable");
        }

        if (forcedStatus == EvidenceVerificationStatus.Untrusted ||
            !string.Equals(
                evidence.EvidenceIssuer,
                trustedEvidenceIssuer,
                StringComparison.Ordinal))
        {
            return new EvidenceVerificationResult(
                EvidenceVerificationStatus.Untrusted,
                "evidence.untrusted");
        }

        return new EvidenceVerificationResult(
            EvidenceVerificationStatus.Trusted,
            "evidence.trusted");
    }
}

public sealed class SimulatedCurrentContextProvider(
    CurrentContinuationContext context,
    CurrentContextStatus status = CurrentContextStatus.Available)
    : ICurrentContinuationContextProvider
{
    public CurrentContextResult Rebuild(
        AcknowledgmentChallenge challenge)
    {
        _ = challenge;

        if (status == CurrentContextStatus.Unavailable)
        {
            return new CurrentContextResult(
                CurrentContextStatus.Unavailable,
                null,
                "context.unavailable");
        }

        return new CurrentContextResult(
            CurrentContextStatus.Available,
            context,
            "context.available");
    }
}

public sealed class SimulatedCurrentPolicyEvaluator(
    bool allowed,
    string? requiredAcknowledgmentCode = "bulk-suspend-impact-ack",
    string policyId = "bulk-suspend-policy",
    string policyVersion = "7.4")
    : ICurrentPolicyEvaluator
{
    public CurrentPolicyDecision Evaluate(
        CurrentContinuationContext context)
    {
        _ = context;

        return new CurrentPolicyDecision(
            allowed,
            DecisionId: allowed
                ? "decision-current-allow"
                : "decision-current-deny",
            PolicyId: policyId,
            PolicyVersion: policyVersion,
            RequiredAcknowledgmentCode: requiredAcknowledgmentCode,
            ReasonCode: allowed
                ? "policy.allowed"
                : "policy.denied");
    }
}
