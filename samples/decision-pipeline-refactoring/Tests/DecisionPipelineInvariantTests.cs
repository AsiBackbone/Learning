using Xunit;

namespace DecisionPipelineRefactoring.Tests;

public sealed class DecisionPipelineInvariantTests
{
    [Fact]
    public void Scattered_service_exposes_mutation_before_a_denied_decision()
    {
        InMemoryAccountRepository repository = SampleData.CreateRepository();
        var notifications = new RecordingNotificationGateway();
        var events = new RecordingAccountEventPublisher();
        var service = new ScatteredAccountDisableService(
            repository,
            notifications,
            events);

        GovernanceDecision decision = service.Handle(new AccountDisableRequest(
            CorrelationId: "corr-starter",
            ActorId: "admin-17",
            AccountId: "acct-protected",
            RequesterIsAdministrator: true,
            AcknowledgmentSatisfied: true));

        Assert.Equal(DecisionOutcome.Denied, decision.Outcome);
        Assert.Equal(1, repository.DisableCount);
        Assert.Equal(1, notifications.SendCount);
        Assert.Equal(0, events.PublishCount);
    }

    [Fact]
    public void Denied_request_never_reaches_the_executor()
    {
        Fixture fixture = CreateFixture();

        GovernanceDecision decision = fixture.Pipeline.Handle(Request(
            accountId: "acct-protected",
            isAdministrator: true,
            acknowledgmentSatisfied: true));

        Assert.Equal(DecisionOutcome.Denied, decision.Outcome);
        Assert.Equal("account.disable.protected-account", decision.ReasonCode);
        AssertNoSideEffects(fixture);
        AssertDecisionEvidence(fixture, decision, "acct-protected", 7);
    }

    [Fact]
    public void Deferred_request_never_reaches_the_executor()
    {
        Fixture fixture = CreateFixture();

        GovernanceDecision decision = fixture.Pipeline.Handle(Request(
            accountId: "acct-pending",
            isAdministrator: true,
            acknowledgmentSatisfied: true));

        Assert.Equal(DecisionOutcome.Deferred, decision.Outcome);
        Assert.Equal("account.disable.investigation-pending", decision.ReasonCode);
        AssertNoSideEffects(fixture);
        AssertDecisionEvidence(fixture, decision, "acct-pending", 4);
    }

    [Fact]
    public void Acknowledgment_required_without_satisfied_continuation_never_reaches_the_executor()
    {
        Fixture fixture = CreateFixture();

        GovernanceDecision decision = fixture.Pipeline.Handle(Request(
            accountId: "acct-standard",
            isAdministrator: true,
            acknowledgmentSatisfied: false));

        Assert.Equal(DecisionOutcome.AcknowledgmentRequired, decision.Outcome);
        Assert.Equal("account.disable.acknowledgment-required", decision.ReasonCode);
        AssertNoSideEffects(fixture);
        AssertDecisionEvidence(fixture, decision, "acct-standard", 3);
    }

    [Fact]
    public void Escalation_recommended_never_reaches_the_executor()
    {
        Fixture fixture = CreateFixture();

        GovernanceDecision decision = fixture.Pipeline.Handle(Request(
            accountId: "acct-manual",
            isAdministrator: true,
            acknowledgmentSatisfied: true));

        Assert.Equal(DecisionOutcome.EscalationRecommended, decision.Outcome);
        Assert.Equal("account.disable.manual-review-required", decision.ReasonCode);
        AssertNoSideEffects(fixture);
        AssertDecisionEvidence(fixture, decision, "acct-manual", 11);
    }

    [Fact]
    public void Allowed_request_reaches_the_executor_exactly_once()
    {
        Fixture fixture = CreateFixture();

        GovernanceDecision decision = fixture.Pipeline.Handle(Request(
            accountId: "acct-standard",
            isAdministrator: true,
            acknowledgmentSatisfied: true));

        Assert.Equal(DecisionOutcome.Allowed, decision.Outcome);
        Assert.Equal("account.disable.allowed", decision.ReasonCode);
        Assert.Equal(1, fixture.Executor.InvocationCount);
        Assert.Equal(1, fixture.Repository.DisableCount);
        Assert.Equal(1, fixture.Notifications.SendCount);
        Assert.Equal(1, fixture.Events.PublishCount);
        Assert.Equal(2, fixture.Evidence.Records.Count);
        Assert.Equal("decision", fixture.Evidence.Records[0].Stage);
        Assert.Equal("execution", fixture.Evidence.Records[1].Stage);
        Assert.All(
            fixture.Evidence.Records,
            record =>
            {
                Assert.Equal("corr-acct-standard", record.CorrelationId);
                Assert.Equal("admin-17", record.ActorId);
                Assert.Equal("acct-standard", record.AccountId);
                Assert.Equal(3, record.ResourceVersion);
                Assert.Equal(DecisionOutcome.Allowed, record.Outcome);
                Assert.Equal("account.disable.allowed", record.ReasonCode);
            });
    }

    [Fact]
    public void Non_administrator_is_denied_after_authoritative_context_is_loaded_but_before_execution()
    {
        Fixture fixture = CreateFixture();

        GovernanceDecision decision = fixture.Pipeline.Handle(Request(
            accountId: "acct-standard",
            isAdministrator: false,
            acknowledgmentSatisfied: true));

        Assert.Equal(DecisionOutcome.Denied, decision.Outcome);
        Assert.Equal("account.disable.requester-not-administrator", decision.ReasonCode);
        Assert.Equal(0, fixture.Executor.InvocationCount);
        Assert.Single(fixture.Evidence.Records);
        Assert.Equal("acct-standard", fixture.Evidence.Records[0].AccountId);
        Assert.Equal(3, fixture.Evidence.Records[0].ResourceVersion);
    }

    [Fact]
    public void Whitespace_account_identifier_is_rejected_before_decision_or_execution()
    {
        Fixture fixture = CreateFixture();

        Assert.Throws<ArgumentException>(
            () => fixture.Pipeline.Handle(Request(
                accountId: "   ",
                isAdministrator: true,
                acknowledgmentSatisfied: true)));

        AssertNoSideEffects(fixture);
        Assert.Empty(fixture.Evidence.Records);
    }

    [Fact]
    public void Unknown_account_is_rejected_before_decision_or_execution()
    {
        Fixture fixture = CreateFixture();

        Assert.Throws<KeyNotFoundException>(
            () => fixture.Pipeline.Handle(Request(
                accountId: "acct-missing",
                isAdministrator: true,
                acknowledgmentSatisfied: true)));

        AssertNoSideEffects(fixture);
        Assert.Empty(fixture.Evidence.Records);
    }

    private static void AssertNoSideEffects(Fixture fixture)
    {
        Assert.Equal(0, fixture.Executor.InvocationCount);
        Assert.Equal(0, fixture.Repository.DisableCount);
        Assert.Equal(0, fixture.Notifications.SendCount);
        Assert.Equal(0, fixture.Events.PublishCount);
    }

    private static void AssertDecisionEvidence(
        Fixture fixture,
        GovernanceDecision decision,
        string accountId,
        int resourceVersion)
    {
        DecisionEvidenceRecord record = Assert.Single(fixture.Evidence.Records);

        Assert.Equal("decision", record.Stage);
        Assert.Equal($"corr-{accountId}", record.CorrelationId);
        Assert.Equal("admin-17", record.ActorId);
        Assert.Equal(accountId, record.AccountId);
        Assert.Equal(resourceVersion, record.ResourceVersion);
        Assert.Equal(decision.Outcome, record.Outcome);
        Assert.Equal(decision.ReasonCode, record.ReasonCode);
    }

    private static AccountDisableRequest Request(
        string accountId,
        bool isAdministrator,
        bool acknowledgmentSatisfied)
    {
        return new(
            CorrelationId: $"corr-{accountId}",
            ActorId: "admin-17",
            AccountId: accountId,
            RequesterIsAdministrator: isAdministrator,
            AcknowledgmentSatisfied: acknowledgmentSatisfied);
    }

    private static Fixture CreateFixture()
    {
        InMemoryAccountRepository repository = SampleData.CreateRepository();
        var notifications = new RecordingNotificationGateway();
        var events = new RecordingAccountEventPublisher();
        var executor = new RecordingAccountDisableExecutor(
            repository,
            notifications,
            events);
        var evidence = new RecordingDecisionEvidenceSink();
        var pipeline = new AccountDisableDecisionPipeline(
            new AccountDisableContextBuilder(repository),
            executor,
            evidence);

        return new Fixture(
            repository,
            notifications,
            events,
            executor,
            evidence,
            pipeline);
    }

    private sealed record Fixture(
        InMemoryAccountRepository Repository,
        RecordingNotificationGateway Notifications,
        RecordingAccountEventPublisher Events,
        RecordingAccountDisableExecutor Executor,
        RecordingDecisionEvidenceSink Evidence,
        AccountDisableDecisionPipeline Pipeline);
}
