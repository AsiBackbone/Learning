namespace DistributedAcknowledgmentContinuation;

public static class SampleScenarios
{
    public static readonly DateTimeOffset IssuedAtUtc =
        new(2032, 4, 5, 16, 0, 0, TimeSpan.Zero);

    public const string ChallengeId = "ack-2032-0042";
    public const string ContinuationId = "continuation-42";
    public const string CorrelationId = "corr-bulk-suspend-42";
    public const string IntentDigest = "sha256:fictional-intent-digest";
    public const string PresentationVersion = "bulk-suspend-presentation-v1";
    public const string PresentationDigest = "sha256:fictional-presentation-digest";
    public const string RequirementCode = "bulk-suspend-impact-ack";
    public const string TrustedEvidenceIssuer = "system-b-ack-service";
    public const string ExecutionAudience =
        RecordingContinuationExecutor.DefaultAcceptedAudience;

    public static AcknowledgmentChallenge CreateChallenge(
        string challengeId = ChallengeId,
        string correlationId = CorrelationId)
    {
        return new AcknowledgmentChallenge(
            ChallengeId: challengeId,
            OriginatingDecisionId: "decision-origin-42",
            RequesterActorId: "tenant-admin-7",
            RequiredResponderId: "operator-17",
            Operation: "accounts.bulk-suspend",
            ResourceId: "tenant-a:batch-42",
            ResourceVersionAtChallenge: "snapshot-8",
            IntentCanonicalizationVersion: "bulk-suspend-v1",
            IntentDigest: IntentDigest,
            RequirementCode: RequirementCode,
            PresentationVersion: PresentationVersion,
            PresentationDigest: PresentationDigest,
            PolicyId: "bulk-suspend-policy",
            PolicyVersion: "7.3",
            IssuedAtUtc,
            ExpiresAtUtc: IssuedAtUtc.AddMinutes(10),
            CorrelationId: correlationId);
    }

    public static AcknowledgmentEvidence CreateEvidence(
        string evidenceId = "evidence-42",
        string responseId = "response-42",
        string challengeId = ChallengeId,
        string responderId = "operator-17",
        string intentCanonicalizationVersion = "bulk-suspend-v1",
        string intentDigest = IntentDigest,
        string presentationVersion = PresentationVersion,
        string presentationDigest = PresentationDigest,
        string evidenceIssuer = TrustedEvidenceIssuer,
        bool accepted = true,
        DateTimeOffset? occurredAtUtc = null,
        string correlationId = CorrelationId)
    {
        return new AcknowledgmentEvidence(
            evidenceId,
            evidenceIssuer,
            challengeId,
            responseId,
            responderId,
            intentCanonicalizationVersion,
            intentDigest,
            presentationVersion,
            presentationDigest,
            accepted,
            occurredAtUtc ?? IssuedAtUtc.AddMinutes(1),
            correlationId);
    }

    public static ContinuationRequest CreateContinuationRequest(
        string continuationId = ContinuationId)
    {
        return new ContinuationRequest(continuationId);
    }

    public static CurrentContinuationContext CreateContext(
        string originatingActorId = "tenant-admin-7",
        string operation = "accounts.bulk-suspend",
        string resourceId = "tenant-a:batch-42",
        string resourceVersion = "snapshot-8",
        string intentCanonicalizationVersion = "bulk-suspend-v1",
        string intentDigest = IntentDigest,
        string correlationId = CorrelationId,
        DateTimeOffset? nowUtc = null)
    {
        return new CurrentContinuationContext(
            OriginatingActorId: originatingActorId,
            Operation: operation,
            ResourceId: resourceId,
            ResourceVersion: resourceVersion,
            IntentCanonicalizationVersion: intentCanonicalizationVersion,
            IntentDigest: intentDigest,
            CorrelationId: correlationId,
            NowUtc: nowUtc ?? IssuedAtUtc.AddMinutes(2));
    }

    public static InMemoryAcknowledgmentChallengeStore CreateChallengeStore(
        bool includeChallenge = true)
    {
        var store = new InMemoryAcknowledgmentChallengeStore();

        if (includeChallenge)
        {
            store.Put(CreateChallenge());
        }

        return store;
    }

    public static InMemoryContinuationStateStore CreateContinuationStateStore(
        bool includeState = true,
        string challengeId = ChallengeId,
        string correlationId = CorrelationId)
    {
        var store = new InMemoryContinuationStateStore();

        if (includeState)
        {
            store.Put(new ContinuationState(
                ContinuationId,
                challengeId,
                correlationId));
        }

        return store;
    }

    public static DistributedAcknowledgmentGateway CreateGateway(
        RecordingContinuationExecutor executor,
        IAcknowledgmentChallengeStore? challengeStore = null,
        IContinuationStateStore? continuationStateStore = null,
        IAcknowledgmentEvidenceVerifier? verifier = null,
        ICurrentContinuationContextProvider? contextProvider = null,
        ICurrentPolicyEvaluator? policyEvaluator = null,
        IContinuationClaimStore? claimStore = null,
        string executionAudience = ExecutionAudience)
    {
        return new DistributedAcknowledgmentGateway(
            challengeStore ?? CreateChallengeStore(),
            continuationStateStore ?? CreateContinuationStateStore(),
            verifier ?? new SimulatedAcknowledgmentEvidenceVerifier(
                TrustedEvidenceIssuer),
            contextProvider ?? new SimulatedCurrentContextProvider(
                CreateContext()),
            policyEvaluator ?? new SimulatedCurrentPolicyEvaluator(
                allowed: true),
            claimStore ?? new InMemoryContinuationClaimStore(),
            executor,
            executionAudience);
    }
}
