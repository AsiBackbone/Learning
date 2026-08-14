using Xunit;

namespace GovernedAiToolGateway.Tests;

public sealed class GovernedGatewayTests
{
    private static readonly DateTimeOffset NowUtc =
        new(2026, 8, 14, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UnknownToolIsRejectedWithoutExecution()
    {
        SampleHost host = SampleComposition.Create();

        GatewayResult result = await host.Gateway.ExecuteAsync(
            CreateProposal(
                toolName: "finance.transfer_unlimited",
                recipient: null,
                template: null),
            CreateActor(),
            NowUtc,
            acknowledgmentResponse: null,
            CancellationToken.None);

        Assert.Equal(GatewayStatus.Rejected, result.Status);
        Assert.Equal("tool.unknown", result.ReasonCode);
        Assert.False(result.WouldExecute);
        Assert.Equal(0, host.Handler.InvocationCount);
    }

    [Fact]
    public async Task MissingRequiredArgumentIsRejectedWithoutExecution()
    {
        SampleHost host = SampleComposition.Create();

        GatewayResult result = await host.Gateway.ExecuteAsync(
            CreateProposal(
                recipient: "employee@example.internal",
                template: null),
            CreateActor(),
            NowUtc,
            acknowledgmentResponse: null,
            CancellationToken.None);

        Assert.Equal(GatewayStatus.Rejected, result.Status);
        Assert.Equal("proposal.arguments-invalid", result.ReasonCode);
        Assert.Equal(0, host.Handler.InvocationCount);
    }

    [Fact]
    public async Task InternalRecipientWouldExecuteExactlyOnce()
    {
        SampleHost host = SampleComposition.Create();

        GatewayResult result = await host.Gateway.ExecuteAsync(
            CreateProposal(
                proposalId: "proposal-internal",
                recipient: "employee@example.internal",
                template: "case-update"),
            CreateActor(),
            NowUtc,
            acknowledgmentResponse: null,
            CancellationToken.None);

        Assert.Equal(GatewayStatus.WouldExecute, result.Status);
        Assert.True(result.WouldExecute);
        Assert.Equal(1, host.Handler.InvocationCount);
        Assert.Equal("employee@example.internal", host.Handler.LastRecipient);
        Assert.Equal("host", host.Handler.CredentialOwner);
    }

    [Fact]
    public async Task ModelSuppliedClassificationDoesNotOverrideHostClassification()
    {
        SampleHost host = SampleComposition.Create();

        GatewayResult result = await host.Gateway.ExecuteAsync(
            CreateProposal(
                proposalId: "proposal-external-claims-internal",
                recipient: "partner@example.net",
                template: "case-update",
                claimedClassification: "internal"),
            CreateActor(),
            NowUtc,
            acknowledgmentResponse: null,
            CancellationToken.None);

        Assert.Equal(GatewayStatus.AwaitingAcknowledgment, result.Status);
        Assert.Equal(
            GovernanceDecisionOutcome.AcknowledgmentRequired,
            result.DecisionOutcome);
        Assert.Equal(0, host.Handler.InvocationCount);

        AuditResidue contextEntry = Assert.Single(
            host.AuditSink.Entries,
            entry => entry.Stage == "context");

        Assert.Equal("External", contextEntry.Outcome);
        Assert.Equal("context.host-authoritative", contextEntry.ReasonCode);
    }

    [Fact]
    public async Task BlocklistedDomainIsDeniedWithoutAcknowledgmentOrExecution()
    {
        SampleHost host = SampleComposition.Create();

        GatewayResult result = await host.Gateway.ExecuteAsync(
            CreateProposal(
                proposalId: "proposal-blocked",
                recipient: "recipient@blocked.example",
                template: "case-update"),
            CreateActor(),
            NowUtc,
            acknowledgmentResponse: null,
            CancellationToken.None);

        Assert.Equal(GatewayStatus.Blocked, result.Status);
        Assert.Equal("notification.destination-blocked", result.ReasonCode);
        Assert.Null(result.AcknowledgmentChallenge);
        Assert.Equal(0, host.Handler.InvocationCount);
    }

    [Fact]
    public async Task UnknownDestinationClassificationDefersWithoutExecution()
    {
        SampleHost host = SampleComposition.Create();

        GatewayResult result = await host.Gateway.ExecuteAsync(
            CreateProposal(
                proposalId: "proposal-unclassified",
                recipient: "recipient@unclassified.test",
                template: "case-update"),
            CreateActor(),
            NowUtc,
            acknowledgmentResponse: null,
            CancellationToken.None);

        Assert.Equal(GatewayStatus.Blocked, result.Status);
        Assert.Equal("notification.destination-unknown", result.ReasonCode);
        Assert.Equal(
            GovernanceDecisionOutcome.Deferred,
            result.DecisionOutcome);
        Assert.Equal(0, host.Handler.InvocationCount);
    }

    [Fact]
    public async Task RejectedAcknowledgmentDoesNotBecomeExecutionAuthority()
    {
        SampleHost host = SampleComposition.Create();
        AiToolProposal proposal = CreateProposal(
            proposalId: "proposal-rejected-ack",
            recipient: "partner@example.net",
            template: "case-update");

        GatewayResult first = await host.Gateway.ExecuteAsync(
            proposal,
            CreateActor(),
            NowUtc,
            acknowledgmentResponse: null,
            CancellationToken.None);

        AcknowledgmentChallenge challenge =
            Assert.IsType<AcknowledgmentChallenge>(
                first.AcknowledgmentChallenge);

        GatewayResult result = await host.Gateway.ExecuteAsync(
            proposal,
            CreateActor(),
            NowUtc.AddSeconds(5),
            new AcknowledgmentResponse(
                challenge.ChallengeId,
                "operator-7",
                Accepted: false,
                RespondedUtc: NowUtc.AddSeconds(5)),
            CancellationToken.None);

        Assert.Equal(GatewayStatus.Rejected, result.Status);
        Assert.Equal("acknowledgment.rejected", result.ReasonCode);
        Assert.Equal(0, host.Handler.InvocationCount);
    }

    [Fact]
    public async Task WrongAcknowledgmentActorDoesNotBecomeExecutionAuthority()
    {
        SampleHost host = SampleComposition.Create();
        AiToolProposal proposal = CreateProposal(
            proposalId: "proposal-wrong-actor",
            recipient: "partner@example.net",
            template: "case-update");

        GatewayResult first = await host.Gateway.ExecuteAsync(
            proposal,
            CreateActor(),
            NowUtc,
            acknowledgmentResponse: null,
            CancellationToken.None);

        AcknowledgmentChallenge challenge =
            Assert.IsType<AcknowledgmentChallenge>(
                first.AcknowledgmentChallenge);

        GatewayResult result = await host.Gateway.ExecuteAsync(
            proposal,
            CreateActor(),
            NowUtc.AddSeconds(5),
            new AcknowledgmentResponse(
                challenge.ChallengeId,
                "different-actor",
                Accepted: true,
                RespondedUtc: NowUtc.AddSeconds(5)),
            CancellationToken.None);

        Assert.Equal(GatewayStatus.Rejected, result.Status);
        Assert.Equal("acknowledgment.actor-mismatch", result.ReasonCode);
        Assert.Equal(0, host.Handler.InvocationCount);
    }

    [Fact]
    public async Task ValidExternalAcknowledgmentReevaluatesAndWouldExecute()
    {
        SampleHost host = SampleComposition.Create();
        AiToolProposal proposal = CreateProposal(
            proposalId: "proposal-valid-external",
            recipient: "partner@example.net",
            template: "case-update");

        GatewayResult first = await host.Gateway.ExecuteAsync(
            proposal,
            CreateActor(),
            NowUtc,
            acknowledgmentResponse: null,
            CancellationToken.None);

        AcknowledgmentChallenge challenge =
            Assert.IsType<AcknowledgmentChallenge>(
                first.AcknowledgmentChallenge);

        GatewayResult result = await host.Gateway.ExecuteAsync(
            proposal,
            CreateActor(),
            NowUtc.AddSeconds(5),
            new AcknowledgmentResponse(
                challenge.ChallengeId,
                "operator-7",
                Accepted: true,
                RespondedUtc: NowUtc.AddSeconds(5)),
            CancellationToken.None);

        Assert.Equal(GatewayStatus.WouldExecute, result.Status);
        Assert.True(result.WouldExecute);
        Assert.NotNull(result.CapabilityId);
        Assert.Equal(1, host.Handler.InvocationCount);

        Assert.Contains(
            host.AuditSink.Entries,
            entry =>
                entry.CorrelationId == proposal.ProposalId &&
                entry.Stage == "re-evaluation" &&
                entry.Outcome == "Allowed");
    }

    [Fact]
    public async Task ChangedRecipientAfterAcknowledgmentRequiresNewAcknowledgment()
    {
        SampleHost host = SampleComposition.Create();
        AiToolProposal originalProposal = CreateProposal(
            proposalId: "proposal-ack-resource-binding",
            recipient: "partner@example.net",
            template: "case-update");

        GatewayResult first = await host.Gateway.ExecuteAsync(
            originalProposal,
            CreateActor(),
            NowUtc,
            acknowledgmentResponse: null,
            CancellationToken.None);

        AcknowledgmentChallenge originalChallenge =
            Assert.IsType<AcknowledgmentChallenge>(
                first.AcknowledgmentChallenge);

        AiToolProposal changedProposal = CreateProposal(
            proposalId: originalProposal.ProposalId,
            recipient: "other@example.net",
            template: "case-update");

        GatewayResult result = await host.Gateway.ExecuteAsync(
            changedProposal,
            CreateActor(),
            NowUtc.AddSeconds(5),
            new AcknowledgmentResponse(
                originalChallenge.ChallengeId,
                "operator-7",
                Accepted: true,
                RespondedUtc: NowUtc.AddSeconds(5)),
            CancellationToken.None);

        Assert.Equal(GatewayStatus.Rejected, result.Status);
        Assert.Equal("acknowledgment.challenge-mismatch", result.ReasonCode);
        Assert.Equal(0, host.Handler.InvocationCount);
    }

    [Fact]
    public void ChangedRecipientAfterApprovalInvalidatesCapability()
    {
        SampleHost host = SampleComposition.Create();
        AiToolProposal proposal = CreateProposal(
            proposalId: "proposal-resource-binding",
            recipient: "partner@example.net",
            template: "case-update");
        ToolDescriptor descriptor =
            Assert.IsType<ToolDescriptor>(
                host.ToolRegistry.Find("notification.send"));
        AiToolPolicyContext originalContext = host.ContextFactory.Create(
            proposal,
            descriptor,
            CreateActor()) with
        {
            SatisfiedAcknowledgmentId = "ack-1"
        };
        GovernanceDecision decision = host.Policy.Evaluate(originalContext);
        ExecutionCapability capability = host.CapabilityIssuer.Issue(
            originalContext,
            decision,
            descriptor,
            NowUtc);
        AiToolPolicyContext changedContext = originalContext with
        {
            Recipient = "other@example.net"
        };

        CapabilityValidationResult validation =
            host.CapabilityValidator.Validate(
                capability,
                changedContext,
                descriptor,
                NowUtc.AddSeconds(10));

        Assert.False(validation.IsValid);
        Assert.Equal("capability.resource-mismatch", validation.ReasonCode);
    }

    [Fact]
    public void ExpiredCapabilityIsRejectedAtExecutionBoundary()
    {
        SampleHost host = SampleComposition.Create();
        ToolDescriptor descriptor =
            Assert.IsType<ToolDescriptor>(
                host.ToolRegistry.Find("notification.send"));
        AiToolPolicyContext context = host.ContextFactory.Create(
            CreateProposal(
                proposalId: "proposal-expiration",
                recipient: "employee@example.internal",
                template: "case-update"),
            descriptor,
            CreateActor());
        GovernanceDecision decision = host.Policy.Evaluate(context);
        ExecutionCapability capability = host.CapabilityIssuer.Issue(
            context,
            decision,
            descriptor,
            NowUtc);

        CapabilityValidationResult validation =
            host.CapabilityValidator.Validate(
                capability,
                context,
                descriptor,
                capability.ExpiresUtc);

        Assert.False(validation.IsValid);
        Assert.Equal("capability.expired", validation.ReasonCode);
    }

    [Fact]
    public async Task ReplayingSameCapabilityIdentityDoesNotReachHandlerTwice()
    {
        SampleHost host = SampleComposition.Create();
        AiToolProposal proposal = CreateProposal(
            proposalId: "proposal-single-use",
            recipient: "employee@example.internal",
            template: "case-update");

        GatewayResult first = await host.Gateway.ExecuteAsync(
            proposal,
            CreateActor(),
            NowUtc,
            acknowledgmentResponse: null,
            CancellationToken.None);

        GatewayResult second = await host.Gateway.ExecuteAsync(
            proposal,
            CreateActor(),
            NowUtc.AddSeconds(10),
            acknowledgmentResponse: null,
            CancellationToken.None);

        Assert.Equal(GatewayStatus.WouldExecute, first.Status);
        Assert.Equal(GatewayStatus.Rejected, second.Status);
        Assert.Equal("capability.already-consumed", second.ReasonCode);
        Assert.Equal(1, host.Handler.InvocationCount);
    }

    [Fact]
    public async Task SuccessfulFlowPreservesCorrelationAcrossEvidenceStages()
    {
        SampleHost host = SampleComposition.Create();
        const string proposalId = "proposal-correlation";

        GatewayResult result = await host.Gateway.ExecuteAsync(
            CreateProposal(
                proposalId: proposalId,
                recipient: "employee@example.internal",
                template: "case-update"),
            CreateActor(),
            NowUtc,
            acknowledgmentResponse: null,
            CancellationToken.None);

        Assert.Equal(GatewayStatus.WouldExecute, result.Status);

        AuditResidue[] entries = host.AuditSink.Entries
            .Where(entry => entry.CorrelationId == proposalId)
            .ToArray();

        Assert.NotEmpty(entries);
        Assert.All(
            entries,
            entry => Assert.Equal(proposalId, entry.CorrelationId));
        Assert.Contains(entries, entry => entry.Stage == "context");
        Assert.Contains(entries, entry => entry.Stage == "decision");
        Assert.Contains(entries, entry => entry.Stage == "capability-issued");
        Assert.Contains(entries, entry => entry.Stage == "capability-validation");
        Assert.Contains(entries, entry => entry.Stage == "execution");
    }

    private static HostActor CreateActor() =>
        new("operator-7", "tenant-a");

    private static AiToolProposal CreateProposal(
        string proposalId = "proposal-test",
        string toolName = "notification.send",
        string? recipient = "employee@example.internal",
        string? template = "case-update",
        string? claimedClassification = null)
    {
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);

        if (recipient is not null)
        {
            arguments["recipient"] = recipient;
        }

        if (template is not null)
        {
            arguments["template"] = template;
        }

        if (claimedClassification is not null)
        {
            arguments["classification"] = claimedClassification;
        }

        return new AiToolProposal(
            ProposalId: proposalId,
            ModelId: "simulated-model-v1",
            ToolName: toolName,
            Arguments: arguments,
            ModelRationale: "Simulated proposal for invariant testing.");
    }
}
