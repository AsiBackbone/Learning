using DistributedAcknowledgmentContinuation;
using Xunit;

namespace DistributedAcknowledgmentContinuation.Tests;

public sealed class DistributedAcknowledgmentContinuationTests
{
    [Fact]
    public async Task ValidAcknowledgmentReevaluatesAndExecutesExactlyOnce()
    {
        var executor = new RecordingContinuationExecutor();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Executed);
        Assert.Equal("execution.completed", result.InternalReasonCode);
        Assert.NotNull(result.ContinuationAuthorityId);
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task DifferentIntentDigestDoesNotContinue()
    {
        var executor = new RecordingContinuationExecutor();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(
                intentDigest: "sha256:different-intent"),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal("evidence.intent-mismatch", result.InternalReasonCode);
        Assert.Null(result.ContinuationAuthorityId);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task DifferentCanonicalizationVersionDoesNotContinue()
    {
        var executor = new RecordingContinuationExecutor();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(
                intentCanonicalizationVersion: "bulk-suspend-v2"),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal(
            "evidence.intent-version-mismatch",
            result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task DifferentPresentationVersionDoesNotContinue()
    {
        var executor = new RecordingContinuationExecutor();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(
                presentationVersion: "bulk-suspend-presentation-v2"),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal(
            "evidence.presentation-version-mismatch",
            result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task DifferentPresentationFingerprintDoesNotContinue()
    {
        var executor = new RecordingContinuationExecutor();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(
                presentationDigest: "sha256:different-presentation"),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal(
            "evidence.presentation-mismatch",
            result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task DifferentChallengeCannotSubstituteForContinuationState()
    {
        var executor = new RecordingContinuationExecutor();
        InMemoryAcknowledgmentChallengeStore challengeStore =
            SampleScenarios.CreateChallengeStore();
        challengeStore.Put(
            SampleScenarios.CreateChallenge(
                challengeId: "ack-2032-substitute"));
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                challengeStore: challengeStore);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(
                challengeId: "ack-2032-substitute"),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal(
            "evidence.challenge-mismatch",
            result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task MissingContinuationStateDoesNotContinue()
    {
        var executor = new RecordingContinuationExecutor();
        InMemoryContinuationStateStore continuationStateStore =
            SampleScenarios.CreateContinuationStateStore(
                includeState: false);
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                continuationStateStore: continuationStateStore);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal("continuation.not-found", result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task ContinuationCorrelationMismatchDoesNotContinue()
    {
        var executor = new RecordingContinuationExecutor();
        InMemoryContinuationStateStore continuationStateStore =
            SampleScenarios.CreateContinuationStateStore(
                correlationId: "corr-other");
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                continuationStateStore: continuationStateStore);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(
                correlationId: "corr-other"),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal(
            "continuation.correlation-mismatch",
            result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task EvidenceCorrelationMismatchDoesNotContinue()
    {
        var executor = new RecordingContinuationExecutor();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(
                correlationId: "corr-other"),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal(
            "evidence.correlation-mismatch",
            result.InternalReasonCode);
        Assert.Equal("request.not-continued", result.PublicReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task DeclinedEvidenceDoesNotContinue()
    {
        var executor = new RecordingContinuationExecutor();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(accepted: false),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal("evidence.declined", result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task ResponseOutsideChallengeWindowDoesNotContinue()
    {
        var executor = new RecordingContinuationExecutor();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(
                occurredAtUtc: SampleScenarios.IssuedAtUtc.AddMinutes(10)),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal(
            "evidence.response-outside-window",
            result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task ExpiredChallengeDoesNotExecute()
    {
        var executor = new RecordingContinuationExecutor();
        var contextProvider = new SimulatedCurrentContextProvider(
            SampleScenarios.CreateContext(
                nowUtc: SampleScenarios.IssuedAtUtc.AddMinutes(11)));
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                contextProvider: contextProvider);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal("challenge.expired", result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task CurrentContextUnavailableDoesNotContinue()
    {
        var executor = new RecordingContinuationExecutor();
        var contextProvider = new SimulatedCurrentContextProvider(
            SampleScenarios.CreateContext(),
            CurrentContextStatus.Unavailable);
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                contextProvider: contextProvider);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal("context.unavailable", result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task CurrentActorMismatchDoesNotContinue()
    {
        await AssertCurrentContextMismatchAsync(
            SampleScenarios.CreateContext(
                originatingActorId: "tenant-admin-99"),
            "context.actor-mismatch");
    }

    [Fact]
    public async Task CurrentIntentCanonicalizationVersionMismatchDoesNotContinue()
    {
        await AssertCurrentContextMismatchAsync(
            SampleScenarios.CreateContext(
                intentCanonicalizationVersion: "bulk-suspend-v2"),
            "context.intent-version-mismatch");
    }

    [Fact]
    public async Task CurrentIntentDigestMismatchDoesNotContinue()
    {
        await AssertCurrentContextMismatchAsync(
            SampleScenarios.CreateContext(
                intentDigest: "sha256:current-different-intent"),
            "context.intent-mismatch");
    }

    [Fact]
    public async Task CurrentOperationMismatchDoesNotContinue()
    {
        await AssertCurrentContextMismatchAsync(
            SampleScenarios.CreateContext(
                operation: "accounts.bulk-delete"),
            "context.operation-mismatch");
    }

    [Fact]
    public async Task CurrentResourceMismatchDoesNotContinue()
    {
        await AssertCurrentContextMismatchAsync(
            SampleScenarios.CreateContext(
                resourceId: "tenant-a:batch-99"),
            "context.resource-mismatch");
    }

    [Fact]
    public async Task CurrentCorrelationMismatchDoesNotContinue()
    {
        await AssertCurrentContextMismatchAsync(
            SampleScenarios.CreateContext(
                correlationId: "corr-current-other"),
            "context.correlation-mismatch");
    }

    [Fact]
    public async Task CurrentPolicyDenialAfterAcknowledgmentDoesNotExecute()
    {
        var executor = new RecordingContinuationExecutor();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                policyEvaluator: new SimulatedCurrentPolicyEvaluator(
                    allowed: false));

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal("policy.denied", result.InternalReasonCode);
        Assert.Null(result.ContinuationAuthorityId);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task ChangedAcknowledgmentRequirementNeedsNewChallenge()
    {
        var executor = new RecordingContinuationExecutor();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                policyEvaluator: new SimulatedCurrentPolicyEvaluator(
                    allowed: true,
                    requiredAcknowledgmentCode: "bulk-suspend-new-ack"));

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal(
            "policy.acknowledgment-requirement-changed",
            result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task WithdrawnAcknowledgmentRequirementUsesCurrentPolicyDecision()
    {
        var executor = new RecordingContinuationExecutor();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                policyEvaluator: new SimulatedCurrentPolicyEvaluator(
                    allowed: true,
                    requiredAcknowledgmentCode: null));

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Executed);
        ValidatedContinuationCommand command =
            Assert.IsType<ValidatedContinuationCommand>(executor.LastCommand);
        Assert.Equal(
            SampleScenarios.RequirementCode,
            command.AcknowledgmentRequirementCode);
        Assert.Null(command.CurrentRequiredAcknowledgmentCode);
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task PresentationMismatchStillBlocksWhenCurrentPolicyWithdrawsRequirement()
    {
        var executor = new RecordingContinuationExecutor();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                policyEvaluator: new SimulatedCurrentPolicyEvaluator(
                    allowed: true,
                    requiredAcknowledgmentCode: null));

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(
                presentationDigest: "sha256:different-presentation"),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal(
            "evidence.presentation-mismatch",
            result.InternalReasonCode);
        Assert.Null(result.ContinuationAuthorityId);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task ReplayedAcknowledgmentDoesNotDuplicateAuthorityOrExecution()
    {
        var executor = new RecordingContinuationExecutor();
        var claimStore = new InMemoryContinuationClaimStore();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                claimStore: claimStore);
        ContinuationRequest request =
            SampleScenarios.CreateContinuationRequest();
        AcknowledgmentEvidence evidence =
            SampleScenarios.CreateEvidence();

        GatewayResult first = await gateway.ExecuteAsync(
            request,
            evidence,
            TestContext.Current.CancellationToken);
        GatewayResult second = await gateway.ExecuteAsync(
            request,
            evidence,
            TestContext.Current.CancellationToken);

        Assert.True(first.Executed);
        Assert.False(second.Executed);
        Assert.Equal(
            "continuation.already-claimed",
            second.InternalReasonCode);
        Assert.Null(second.ContinuationAuthorityId);
        Assert.Equal(1, executor.InvocationCount);
        Assert.Equal(1, claimStore.GetClaimCount(SampleScenarios.ChallengeId));
    }

    [Fact]
    public async Task TwoActuallyConcurrentContinuationClaimsProduceOneExecution()
    {
        var executor = new RecordingContinuationExecutor();
        var claimStore = new InMemoryContinuationClaimStore();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                claimStore: claimStore);
        ContinuationRequest request =
            SampleScenarios.CreateContinuationRequest();
        AcknowledgmentEvidence evidence =
            SampleScenarios.CreateEvidence();
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using var ready = new CountdownEvent(2);
        using var start = new ManualResetEventSlim(false);

        Task<GatewayResult>[] tasks =
            Enumerable.Range(0, 2)
                .Select(_ => Task.Run(async () =>
                {
                    ready.Signal();
                    start.Wait(cancellationToken);

                    return await gateway.ExecuteAsync(
                        request,
                        evidence,
                        cancellationToken);
                }, cancellationToken))
                .ToArray();

        Assert.True(
            ready.Wait(
                TimeSpan.FromSeconds(5),
                cancellationToken));
        start.Set();

        GatewayResult[] results = await Task.WhenAll(tasks);

        Assert.Single(results, result => result.Executed);
        Assert.Single(
            results,
            result =>
                result.InternalReasonCode ==
                "continuation.already-claimed");
        Assert.Equal(1, executor.InvocationCount);
        Assert.Equal(1, claimStore.GetClaimCount(SampleScenarios.ChallengeId));
    }

    [Fact]
    public async Task EvidenceVerificationUnavailableDoesNotManufactureContinuationAuthority()
    {
        var executor = new RecordingContinuationExecutor();
        var verifier = new SimulatedAcknowledgmentEvidenceVerifier(
            SampleScenarios.TrustedEvidenceIssuer,
            EvidenceVerificationStatus.Unavailable);
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                verifier: verifier);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal(
            "evidence.verification-unavailable",
            result.InternalReasonCode);
        Assert.Null(result.ContinuationAuthorityId);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task WrongResponderDoesNotContinue()
    {
        var executor = new RecordingContinuationExecutor();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(
                responderId: "operator-99"),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal(
            "evidence.responder-mismatch",
            result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task DuplicateAcceptedResponsesDoNotCreateSecondContinuation()
    {
        var executor = new RecordingContinuationExecutor();
        var claimStore = new InMemoryContinuationClaimStore();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                claimStore: claimStore);
        ContinuationRequest request =
            SampleScenarios.CreateContinuationRequest();

        GatewayResult first = await gateway.ExecuteAsync(
            request,
            SampleScenarios.CreateEvidence(
                evidenceId: "evidence-first",
                responseId: "response-first"),
            TestContext.Current.CancellationToken);
        GatewayResult second = await gateway.ExecuteAsync(
            request,
            SampleScenarios.CreateEvidence(
                evidenceId: "evidence-second",
                responseId: "response-second"),
            TestContext.Current.CancellationToken);

        Assert.True(first.Executed);
        Assert.False(second.Executed);
        Assert.Equal(
            "continuation.already-claimed",
            second.InternalReasonCode);
        Assert.Equal(1, executor.InvocationCount);
        Assert.Equal(1, claimStore.GetClaimCount(SampleScenarios.ChallengeId));
    }

    [Fact]
    public async Task EvidenceBeforeChallengeCanBeRetriedAfterChallengeRecovery()
    {
        var executor = new RecordingContinuationExecutor();
        InMemoryAcknowledgmentChallengeStore challengeStore =
            SampleScenarios.CreateChallengeStore(
                includeChallenge: false);
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                challengeStore: challengeStore);
        ContinuationRequest request =
            SampleScenarios.CreateContinuationRequest();
        AcknowledgmentEvidence evidence =
            SampleScenarios.CreateEvidence();

        GatewayResult beforeChallenge = await gateway.ExecuteAsync(
            request,
            evidence,
            TestContext.Current.CancellationToken);

        challengeStore.Put(SampleScenarios.CreateChallenge());

        GatewayResult afterChallenge = await gateway.ExecuteAsync(
            request,
            evidence,
            TestContext.Current.CancellationToken);

        Assert.False(beforeChallenge.Executed);
        Assert.Equal(
            "challenge.not-found",
            beforeChallenge.InternalReasonCode);
        Assert.True(afterChallenge.Executed);
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task ResourceDriftDoesNotExecuteInExactSnapshotSample()
    {
        var executor = new RecordingContinuationExecutor();
        var contextProvider = new SimulatedCurrentContextProvider(
            SampleScenarios.CreateContext(
                resourceVersion: "snapshot-9"));
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                contextProvider: contextProvider);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal(
            "context.resource-version-drift",
            result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task UntrustedEvidenceDoesNotContinue()
    {
        var executor = new RecordingContinuationExecutor();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(
                evidenceIssuer: "untrusted-evidence-service"),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal("evidence.untrusted", result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task ExecutorRejectsUnexpectedContinuationAuthorityAudience()
    {
        var executor = new RecordingContinuationExecutor();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                executionAudience: "system-d:wrong-audience");

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal(
            "authority.audience-mismatch",
            result.InternalReasonCode);
        Assert.Equal("request.not-completed", result.PublicReasonCode);
        Assert.NotNull(result.ContinuationAuthorityId);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task ExecutorRejectsExpiredContinuationAuthorityAndClaimStaysConsumed()
    {
        var executor = new RecordingContinuationExecutor(
            executionNowUtc:
                SampleScenarios.IssuedAtUtc.AddMinutes(4));
        var claimStore = new InMemoryContinuationClaimStore();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                claimStore: claimStore);
        ContinuationRequest request =
            SampleScenarios.CreateContinuationRequest();
        AcknowledgmentEvidence evidence =
            SampleScenarios.CreateEvidence();

        GatewayResult first = await gateway.ExecuteAsync(
            request,
            evidence,
            TestContext.Current.CancellationToken);

        Assert.False(first.Executed);
        Assert.Equal("authority.expired", first.InternalReasonCode);
        Assert.Equal("request.not-completed", first.PublicReasonCode);
        Assert.NotNull(first.ContinuationAuthorityId);
        Assert.Equal(1, claimStore.GetClaimCount(SampleScenarios.ChallengeId));
        Assert.Equal(0, executor.InvocationCount);

        GatewayResult retry = await gateway.ExecuteAsync(
            request,
            evidence,
            TestContext.Current.CancellationToken);

        Assert.False(retry.Executed);
        Assert.Equal(
            "continuation.already-claimed",
            retry.InternalReasonCode);
        Assert.Null(retry.ContinuationAuthorityId);
        Assert.Equal(1, claimStore.GetClaimCount(SampleScenarios.ChallengeId));
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task ExecutorRejectsCommandThatDoesNotMatchContinuationAuthority()
    {
        var seedExecutor = new RecordingContinuationExecutor();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(seedExecutor);

        GatewayResult seedResult = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(),
            TestContext.Current.CancellationToken);

        Assert.True(seedResult.Executed);
        ScopedContinuationAuthority authority =
            Assert.IsType<ScopedContinuationAuthority>(
                seedExecutor.LastAuthority);
        ValidatedContinuationCommand command =
            Assert.IsType<ValidatedContinuationCommand>(
                seedExecutor.LastCommand);

        var executor = new RecordingContinuationExecutor();
        ContinuationExecutionResult execution =
            await executor.ExecuteAsync(
                authority,
                command with
                {
                    ResourceId = "tenant-a:batch-substituted"
                },
                TestContext.Current.CancellationToken);

        Assert.False(execution.Executed);
        Assert.Equal(
            "authority.binding-mismatch",
            execution.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task SuccessfulExecutionPreservesDistributedLineage()
    {
        var executor = new RecordingContinuationExecutor();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Executed);
        ScopedContinuationAuthority authority =
            Assert.IsType<ScopedContinuationAuthority>(executor.LastAuthority);
        ValidatedContinuationCommand command =
            Assert.IsType<ValidatedContinuationCommand>(executor.LastCommand);

        Assert.Equal(SampleScenarios.ExecutionAudience, authority.Audience);
        Assert.Equal(
            SampleScenarios.IssuedAtUtc.AddMinutes(2),
            authority.IssuedAtUtc);
        Assert.Equal(
            SampleScenarios.IssuedAtUtc.AddMinutes(3),
            authority.ExpiresAtUtc);
        Assert.Equal(command.ContinuationAuthorityId, authority.AuthorityId);
        Assert.Equal(command.ContinuationId, authority.ContinuationId);
        Assert.Equal(command.Operation, authority.Operation);
        Assert.Equal(command.ResourceId, authority.ResourceId);
        Assert.Equal(command.ExpectedResourceVersion, authority.ResourceVersion);
        Assert.Equal(command.ChallengeId, authority.ChallengeId);
        Assert.Equal(command.EvidenceId, authority.EvidenceId);
        Assert.Equal(command.CurrentPolicyId, authority.CurrentPolicyId);
        Assert.Equal(command.CurrentPolicyVersion, authority.CurrentPolicyVersion);

        Assert.Equal(SampleScenarios.ContinuationId, command.ContinuationId);
        Assert.Equal("decision-origin-42", command.OriginatingDecisionId);
        Assert.Equal(SampleScenarios.ChallengeId, command.ChallengeId);
        Assert.Equal("evidence-42", command.EvidenceId);
        Assert.Equal("tenant-admin-7", command.OriginatingActorId);
        Assert.Equal("operator-17", command.ResponderId);
        Assert.Equal(
            SampleScenarios.RequirementCode,
            command.AcknowledgmentRequirementCode);
        Assert.Equal(
            SampleScenarios.RequirementCode,
            command.CurrentRequiredAcknowledgmentCode);
        Assert.Equal(
            SampleScenarios.PresentationVersion,
            command.PresentationVersion);
        Assert.Equal(
            SampleScenarios.PresentationDigest,
            command.PresentationDigest);
        Assert.Equal("bulk-suspend-policy", command.OriginatingPolicyId);
        Assert.Equal("7.3", command.OriginatingPolicyVersion);
        Assert.Equal("decision-current-allow", command.CurrentDecisionId);
        Assert.Equal("bulk-suspend-policy", command.CurrentPolicyId);
        Assert.Equal("7.4", command.CurrentPolicyVersion);
        Assert.Equal(SampleScenarios.CorrelationId, command.CorrelationId);
        Assert.Equal(
            result.ContinuationAuthorityId,
            command.ContinuationAuthorityId);
        Assert.Equal(result.ExecutionId, command.ExecutionId);
    }

    private static async Task AssertCurrentContextMismatchAsync(
        CurrentContinuationContext context,
        string expectedReasonCode)
    {
        var executor = new RecordingContinuationExecutor();
        var contextProvider = new SimulatedCurrentContextProvider(context);
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                contextProvider: contextProvider);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            SampleScenarios.CreateEvidence(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Executed);
        Assert.Equal(expectedReasonCode, result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }
}
