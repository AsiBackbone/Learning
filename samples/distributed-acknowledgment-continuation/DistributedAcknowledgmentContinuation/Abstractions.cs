namespace DistributedAcknowledgmentContinuation;

public interface IAcknowledgmentChallengeStore
{
    AcknowledgmentChallenge? Find(string challengeId);

    void Put(AcknowledgmentChallenge challenge);
}

public interface IContinuationStateStore
{
    ContinuationState? Find(string continuationId);

    void Put(ContinuationState state);
}

public interface IAcknowledgmentEvidenceVerifier
{
    EvidenceVerificationResult Verify(AcknowledgmentEvidence evidence);
}

public interface ICurrentContinuationContextProvider
{
    CurrentContextResult Rebuild(AcknowledgmentChallenge challenge);
}

public interface ICurrentPolicyEvaluator
{
    CurrentPolicyDecision Evaluate(CurrentContinuationContext context);
}

public interface IContinuationClaimStore
{
    ContinuationClaimResult TryClaim(
        string challengeId,
        string evidenceId);

    int GetClaimCount(string challengeId);
}

public interface IContinuationExecutor
{
    Task<ContinuationExecutionResult> ExecuteAsync(
        ScopedContinuationAuthority authority,
        ValidatedContinuationCommand command,
        CancellationToken cancellationToken);
}
