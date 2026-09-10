namespace DistributedAcknowledgmentContinuation;

public sealed class DistributedAcknowledgmentGateway(
    IAcknowledgmentChallengeStore challengeStore,
    IContinuationStateStore continuationStateStore,
    IAcknowledgmentEvidenceVerifier evidenceVerifier,
    ICurrentContinuationContextProvider contextProvider,
    ICurrentPolicyEvaluator policyEvaluator,
    IContinuationClaimStore claimStore,
    IContinuationExecutor executor,
    string executionAudience = "system-c:accounts-bulk-suspend")
{
    public async Task<GatewayResult> ExecuteAsync(
        ContinuationRequest request,
        AcknowledgmentEvidence evidence,
        CancellationToken cancellationToken)
    {
        EvidenceVerificationResult verification =
            evidenceVerifier.Verify(evidence);

        if (verification.Status != EvidenceVerificationStatus.Trusted)
        {
            return GatewayResult.Rejected(verification.ReasonCode);
        }

        ContinuationState? continuation =
            continuationStateStore.Find(request.ContinuationId);

        if (continuation is null)
        {
            return GatewayResult.Rejected("continuation.not-found");
        }

        if (!string.Equals(
                evidence.ChallengeId,
                continuation.ChallengeId,
                StringComparison.Ordinal))
        {
            return GatewayResult.Rejected("evidence.challenge-mismatch");
        }

        if (!string.Equals(
                evidence.CorrelationId,
                continuation.CorrelationId,
                StringComparison.Ordinal))
        {
            return GatewayResult.Rejected("evidence.correlation-mismatch");
        }

        // Evidence never creates authoritative challenge state. If trusted challenge
        // state has not arrived yet, this attempt fails before the continuation claim,
        // so the same evidence can be retried after trusted-state recovery.
        AcknowledgmentChallenge? challenge =
            challengeStore.Find(continuation.ChallengeId);

        if (challenge is null)
        {
            return GatewayResult.Rejected("challenge.not-found");
        }

        if (!string.Equals(
                challenge.CorrelationId,
                continuation.CorrelationId,
                StringComparison.Ordinal))
        {
            return GatewayResult.Rejected("continuation.correlation-mismatch");
        }

        if (!evidence.Accepted)
        {
            return GatewayResult.Rejected("evidence.declined");
        }

        if (!string.Equals(
                evidence.IntentCanonicalizationVersion,
                challenge.IntentCanonicalizationVersion,
                StringComparison.Ordinal))
        {
            return GatewayResult.Rejected("evidence.intent-version-mismatch");
        }

        if (!string.Equals(
                evidence.IntentDigest,
                challenge.IntentDigest,
                StringComparison.Ordinal))
        {
            return GatewayResult.Rejected("evidence.intent-mismatch");
        }

        if (!string.Equals(
                evidence.PresentationVersion,
                challenge.PresentationVersion,
                StringComparison.Ordinal))
        {
            return GatewayResult.Rejected("evidence.presentation-version-mismatch");
        }

        if (!string.Equals(
                evidence.PresentationDigest,
                challenge.PresentationDigest,
                StringComparison.Ordinal))
        {
            return GatewayResult.Rejected("evidence.presentation-mismatch");
        }

        if (!string.Equals(
                evidence.ResponderId,
                challenge.RequiredResponderId,
                StringComparison.Ordinal))
        {
            return GatewayResult.Rejected("evidence.responder-mismatch");
        }

        if (evidence.OccurredAtUtc < challenge.IssuedAtUtc ||
            evidence.OccurredAtUtc >= challenge.ExpiresAtUtc)
        {
            return GatewayResult.Rejected("evidence.response-outside-window");
        }

        CurrentContextResult contextResult =
            contextProvider.Rebuild(challenge);
        CurrentContinuationContext? context = contextResult.Context;

        if (contextResult.Status != CurrentContextStatus.Available ||
            context is null)
        {
            return GatewayResult.Rejected(contextResult.ReasonCode);
        }

        if (context.NowUtc >= challenge.ExpiresAtUtc)
        {
            return GatewayResult.Rejected("challenge.expired");
        }

        if (!string.Equals(
                context.OriginatingActorId,
                challenge.RequesterActorId,
                StringComparison.Ordinal))
        {
            return GatewayResult.Rejected("context.actor-mismatch");
        }

        if (!string.Equals(
                context.IntentCanonicalizationVersion,
                challenge.IntentCanonicalizationVersion,
                StringComparison.Ordinal))
        {
            return GatewayResult.Rejected("context.intent-version-mismatch");
        }

        if (!string.Equals(
                context.IntentDigest,
                challenge.IntentDigest,
                StringComparison.Ordinal))
        {
            return GatewayResult.Rejected("context.intent-mismatch");
        }

        if (!string.Equals(
                context.Operation,
                challenge.Operation,
                StringComparison.Ordinal))
        {
            return GatewayResult.Rejected("context.operation-mismatch");
        }

        if (!string.Equals(
                context.ResourceId,
                challenge.ResourceId,
                StringComparison.Ordinal))
        {
            return GatewayResult.Rejected("context.resource-mismatch");
        }

        if (!string.Equals(
                context.CorrelationId,
                challenge.CorrelationId,
                StringComparison.Ordinal))
        {
            return GatewayResult.Rejected("context.correlation-mismatch");
        }

        if (!string.Equals(
                context.ResourceVersion,
                challenge.ResourceVersionAtChallenge,
                StringComparison.Ordinal))
        {
            return GatewayResult.Rejected("context.resource-version-drift");
        }

        CurrentPolicyDecision currentDecision =
            policyEvaluator.Evaluate(context);

        if (!currentDecision.Allowed)
        {
            return GatewayResult.Rejected(currentDecision.ReasonCode);
        }

        if (currentDecision.RequiredAcknowledgmentCode is not null &&
            !string.Equals(
                currentDecision.RequiredAcknowledgmentCode,
                challenge.RequirementCode,
                StringComparison.Ordinal))
        {
            return GatewayResult.Rejected(
                "policy.acknowledgment-requirement-changed");
        }

        ContinuationClaimResult claim = claimStore.TryClaim(
            challenge.ChallengeId,
            evidence.EvidenceId);

        if (!claim.Claimed)
        {
            return GatewayResult.Rejected(claim.ReasonCode);
        }

        DateTimeOffset authorityIssuedAt = context.NowUtc;
        var authority = new ScopedContinuationAuthority(
            AuthorityId: $"authority-{challenge.ChallengeId}",
            ContinuationId: continuation.ContinuationId,
            Audience: executionAudience,
            Operation: challenge.Operation,
            ResourceId: challenge.ResourceId,
            ResourceVersion: context.ResourceVersion,
            ChallengeId: challenge.ChallengeId,
            EvidenceId: evidence.EvidenceId,
            CurrentPolicyId: currentDecision.PolicyId,
            CurrentPolicyVersion: currentDecision.PolicyVersion,
            IssuedAtUtc: authorityIssuedAt,
            ExpiresAtUtc: authorityIssuedAt.AddMinutes(1));

        var command = new ValidatedContinuationCommand(
            ExecutionId: $"exec-{challenge.ChallengeId}",
            ContinuationAuthorityId: authority.AuthorityId,
            ContinuationId: authority.ContinuationId,
            OriginatingDecisionId: challenge.OriginatingDecisionId,
            ChallengeId: challenge.ChallengeId,
            EvidenceId: evidence.EvidenceId,
            OriginatingActorId: challenge.RequesterActorId,
            ResponderId: evidence.ResponderId,
            Operation: authority.Operation,
            ResourceId: authority.ResourceId,
            ExpectedResourceVersion: authority.ResourceVersion,
            AcknowledgmentRequirementCode: challenge.RequirementCode,
            CurrentRequiredAcknowledgmentCode:
                currentDecision.RequiredAcknowledgmentCode,
            PresentationVersion: challenge.PresentationVersion,
            PresentationDigest: challenge.PresentationDigest,
            OriginatingPolicyId: challenge.PolicyId,
            OriginatingPolicyVersion: challenge.PolicyVersion,
            CurrentDecisionId: currentDecision.DecisionId,
            CurrentPolicyId: currentDecision.PolicyId,
            CurrentPolicyVersion: currentDecision.PolicyVersion,
            CorrelationId: challenge.CorrelationId);

        ContinuationExecutionResult execution =
            await executor.ExecuteAsync(
                authority,
                command,
                cancellationToken);

        if (!execution.Executed)
        {
            return GatewayResult.ExecutionRejected(
                authority.AuthorityId,
                command.ExecutionId,
                execution.ReasonCode);
        }

        return GatewayResult.ExecutedSuccessfully(
            authority.AuthorityId,
            command.ExecutionId);
    }
}
