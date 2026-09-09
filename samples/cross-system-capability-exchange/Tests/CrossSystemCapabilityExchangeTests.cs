using CrossSystemCapabilityExchange;
using Xunit;

namespace CrossSystemCapabilityExchange.Tests;

public sealed class CrossSystemCapabilityExchangeTests
{
    private static readonly DateTimeOffset IssuedUtc =
        SampleScenarios.IssuedUtc;

    [Fact]
    public async Task ValidDirectGrantReachesExecutorExactlyOnce()
    {
        var executor = new RecordingExportExecutor();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(),
            SampleScenarios.CreateContext(),
            CancellationToken.None);

        Assert.True(result.Executed);
        Assert.Equal("execution.completed", result.InternalReasonCode);
        Assert.Equal("request.completed", result.PublicReasonCode);
        Assert.NotEmpty(result.RecipientDecisionId);
        Assert.Equal("exec-cap-a-784", result.ExecutionId);
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task CorrectProofWithWrongAudienceDoesNotExecute()
    {
        var executor = new RecordingExportExecutor();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(),
            SampleScenarios.CreateContext(
                audience: "system-b:account-admin"),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal("audience.mismatch", result.InternalReasonCode);
        Assert.Equal("request.not-accepted", result.PublicReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task TrustedIssuerWithExpiredAuthorityDoesNotExecute()
    {
        var executor = new RecordingExportExecutor();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(),
            SampleScenarios.CreateContext(
                nowUtc: IssuedUtc.AddMinutes(6)),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal("lifetime.expired", result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task ExcessivelyLongAuthorityDoesNotExecute()
    {
        var executor = new RecordingExportExecutor();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(
                expiresAtUtc: IssuedUtc.AddMinutes(6)),
            SampleScenarios.CreateContext(),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(
            "lifetime.not-accepted",
            result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }


    [Fact]
    public async Task AuthorityTooFarInTheFutureDoesNotExecute()
    {
        var executor = new RecordingExportExecutor();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(executor);

        DateTimeOffset futureIssued = IssuedUtc.AddMinutes(2);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(
                issuedAtUtc: futureIssued,
                expiresAtUtc: futureIssued.AddMinutes(5)),
            SampleScenarios.CreateContext(
                nowUtc: IssuedUtc),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(
            "lifetime.not-yet-valid",
            result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task ResourceDriftRequiresRevalidationAndDoesNotExecute()
    {
        var executor = new RecordingExportExecutor();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(),
            SampleScenarios.CreateContext(
                resourceVersion: "snapshot-9"),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(
            "resource.version-drift",
            result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task ReplayedSecondUseDoesNotDuplicateProtectedExecution()
    {
        var executor = new RecordingExportExecutor();
        var useStore = new InMemoryCapabilityUseStore();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                useStore);

        ProtectedCapabilityArtifact artifact =
            SampleScenarios.CreateArtifact();
        RecipientExportContext context =
            SampleScenarios.CreateContext();

        GatewayResult first = await gateway.ExecuteAsync(
            artifact,
            context,
            CancellationToken.None);

        GatewayResult second = await gateway.ExecuteAsync(
            artifact,
            context,
            CancellationToken.None);

        Assert.True(first.Executed);
        Assert.False(second.Executed);
        Assert.Equal("claim.replayed", second.InternalReasonCode);
        Assert.Equal(1, executor.InvocationCount);
        Assert.Equal(1, useStore.GetUseCount("cap-a-784"));
    }

    [Fact]
    public async Task UntrustedDelegationChainDoesNotExpandTrust()
    {
        var executor = new RecordingExportExecutor();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(executor);

        DelegationHop[] unexpectedChain =
        {
            new(
                "system-a",
                "system-c",
                HopPosition: 0,
                RemainingDelegationDepth: 1),
            new(
                "system-c",
                "system-b",
                HopPosition: 1,
                RemainingDelegationDepth: 0)
        };

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(
                delegationChain: unexpectedChain),
            SampleScenarios.CreateContext(),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(
            "delegation.chain-not-accepted",
            result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task RecipientIdentityComesFromRecipientPolicy()
    {
        var executor = new RecordingExportExecutor();
        RecipientIssuerPolicy issuerPolicy =
            SampleScenarios.CreateIssuerPolicy() with
            {
                RecipientSystemId = "system-b-alt"
            };

        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                issuerPolicy: issuerPolicy);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(),
            SampleScenarios.CreateContext(),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(
            "delegation.endpoint-mismatch",
            result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task RecipientLocalPolicyCanDenyOtherwiseValidArtifact()
    {
        var executor = new RecordingExportExecutor();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(),
            SampleScenarios.CreateContext(
                localPolicyAllows: false),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(
            "recipient-policy.denied",
            result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task SameAudienceWithDifferentRequestBindingDoesNotExecute()
    {
        var executor = new RecordingExportExecutor();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(),
            SampleScenarios.CreateContext(
                requestDigest: "sha256:substituted-request"),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(
            "request.binding-mismatch",
            result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task AuthenticatedWrongPresenterDoesNotExecute()
    {
        var executor = new RecordingExportExecutor();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(),
            SampleScenarios.CreateContext(
                authenticatedPresenter: "system-b-other-worker"),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal("presenter.mismatch", result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }


    [Fact]
    public async Task UnknownIssuerDoesNotExecute()
    {
        var executor = new RecordingExportExecutor();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(
                issuer: "system-untrusted"),
            SampleScenarios.CreateContext(),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal("issuer.not-trusted", result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task UnknownTrustAnchorDoesNotAttemptProofOrExecute()
    {
        var executor = new RecordingExportExecutor();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(
                keyId: "a-unknown",
                proofValid: false),
            SampleScenarios.CreateContext(),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal("key.not-accepted", result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task InvalidProofUnderAcceptedKeyDoesNotExecute()
    {
        var executor = new RecordingExportExecutor();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(
                proofValid: false),
            SampleScenarios.CreateContext(),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal("proof.invalid", result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task RevokedAuthorityDoesNotExecute()
    {
        var executor = new RecordingExportExecutor();
        var revocationStore = new InMemoryRevocationStore();
        revocationStore.Revoke("cap-a-784");

        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                revocationStore: revocationStore);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(),
            SampleScenarios.CreateContext(),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal("capability.revoked", result.InternalReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task ReplayStoreUnavailableFailsClosed()
    {
        var executor = new RecordingExportExecutor();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                new UnavailableCapabilityUseStore());

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(),
            SampleScenarios.CreateContext(),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(
            "claim.store-unavailable",
            result.InternalReasonCode);
        Assert.Equal("request.not-accepted", result.PublicReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task TwoActuallyConcurrentClaimsProduceOneExecution()
    {
        var executor = new RecordingExportExecutor();
        var useStore = new InMemoryCapabilityUseStore();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                useStore);

        ProtectedCapabilityArtifact artifact =
            SampleScenarios.CreateArtifact();
        RecipientExportContext context =
            SampleScenarios.CreateContext();

        using var ready = new CountdownEvent(2);
        using var start = new ManualResetEventSlim(false);

        Task<GatewayResult>[] tasks =
            Enumerable.Range(0, 2)
                .Select(_ => Task.Run(async () =>
                {
                    ready.Signal();
                    start.Wait();

                    return await gateway.ExecuteAsync(
                        artifact,
                        context,
                        CancellationToken.None);
                }))
                .ToArray();

        Assert.True(
            ready.Wait(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken));
        start.Set();

        GatewayResult[] results =
            await Task.WhenAll(tasks);

        Assert.Single(results, result => result.Executed);
        Assert.Single(
            results,
            result => result.InternalReasonCode == "claim.replayed");
        Assert.Equal(1, executor.InvocationCount);
        Assert.Equal(1, useStore.GetUseCount("cap-a-784"));
    }

    [Fact]
    public async Task ExecutorFailureReturnsStructuredResultAndLeavesGrantConsumed()
    {
        var useStore = new InMemoryCapabilityUseStore();
        CrossSystemGateway failingGateway =
            SampleScenarios.CreateGateway(
                new ThrowingExportExecutor(),
                useStore);

        ProtectedCapabilityArtifact artifact =
            SampleScenarios.CreateArtifact();
        RecipientExportContext context =
            SampleScenarios.CreateContext();

        GatewayResult failed = await failingGateway.ExecuteAsync(
            artifact,
            context,
            CancellationToken.None);

        Assert.False(failed.Executed);
        Assert.Equal("execution.failed", failed.InternalReasonCode);
        Assert.Equal("request.not-completed", failed.PublicReasonCode);
        Assert.Equal("exec-cap-a-784", failed.ExecutionId);
        Assert.Equal(1, useStore.GetUseCount("cap-a-784"));

        var retryExecutor = new RecordingExportExecutor();
        CrossSystemGateway retryGateway =
            SampleScenarios.CreateGateway(
                retryExecutor,
                useStore);

        GatewayResult retry = await retryGateway.ExecuteAsync(
            artifact,
            context,
            CancellationToken.None);

        Assert.False(retry.Executed);
        Assert.Equal("claim.replayed", retry.InternalReasonCode);
        Assert.Equal(0, retryExecutor.InvocationCount);
        Assert.Equal(1, useStore.GetUseCount("cap-a-784"));
    }

    [Fact]
    public async Task ExecutorRechecksResourceVersionAtSideEffectBoundary()
    {
        var executor = new RecordingExportExecutor(
            currentResourceVersion: "snapshot-9");
        var useStore = new InMemoryCapabilityUseStore();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                useStore);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(
                resourceVersion: "snapshot-8"),
            SampleScenarios.CreateContext(
                resourceVersion: "snapshot-8"),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(
            "executor.resource-version-mismatch",
            result.InternalReasonCode);
        Assert.Equal("request.not-completed", result.PublicReasonCode);
        Assert.Equal(0, executor.InvocationCount);
        Assert.Equal(1, useStore.GetUseCount("cap-a-784"));
    }

    [Fact]
    public async Task ExecutorRejectsDestinationOutsideItsOwnAllowlist()
    {
        var executor = new RecordingExportExecutor();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                executionDestination: "system-b-unapproved-store");

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(),
            SampleScenarios.CreateContext(),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(
            "executor.destination-not-allowed",
            result.InternalReasonCode);
        Assert.Equal("request.not-completed", result.PublicReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task ExecutionCommandPreservesCrossSystemProvenanceWithoutConflatingIds()
    {
        var executor = new RecordingExportExecutor();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateArtifact(),
            SampleScenarios.CreateContext(),
            CancellationToken.None);

        ValidatedExportCommand command =
            Assert.IsType<ValidatedExportCommand>(
                executor.LastCommand);

        Assert.True(result.Executed);
        Assert.Equal("analyst-17", command.OriginatingSubject);
        Assert.Equal("dec-a-551", command.IssuerDecisionId);
        Assert.Equal(
            result.RecipientDecisionId,
            command.RecipientDecisionId);
        Assert.Equal(result.ExecutionId, command.ExecutionId);
        Assert.Equal("cap-a-784", command.CapabilityId);
        Assert.Equal("corr-1001", command.CorrelationId);
        Assert.NotEqual(
            command.IssuerDecisionId,
            command.RecipientDecisionId);
        Assert.NotEqual(
            command.RecipientDecisionId,
            command.ExecutionId);
    }
}
