using AdaptiveRiskContext;
using Xunit;

namespace AdaptiveRiskContext.Tests;

public sealed class AdaptiveRiskContextTests
{
    private readonly RiskPolicyEvaluator _policyEvaluator = new();
    private readonly ExecutionAuthorityIssuer _authorityIssuer = new();
    private int _decisionSequence;

    [Fact]
    public void LowRiskObservationProducesAllowedDecision()
    {
        GovernanceDecision decision = Evaluate(
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            SampleScenarios.CreatePolicy());

        Assert.Equal(DecisionOutcome.Allowed, decision.Outcome);
        Assert.Equal("risk.acceptable", decision.ReasonCode);
    }

    [Fact]
    public void LowRiskCannotOverrideDestinationDenial()
    {
        GovernanceDecision decision = Evaluate(
            SampleScenarios.CreatePayment(destinationApproved: false),
            RiskSignalInput.Available(
                SampleScenarios.CreateObservation(fraudProbability: 0.01m)),
            SampleScenarios.CreatePolicy());

        Assert.Equal(DecisionOutcome.Denied, decision.Outcome);
        Assert.Equal("payment.destination-not-approved", decision.ReasonCode);
    }

    [Fact]
    public void UnavailableProviderProducesDeferredDecision()
    {
        GovernanceDecision decision = Evaluate(
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Unavailable("fraud-service"),
            SampleScenarios.CreatePolicy());

        Assert.Equal(DecisionOutcome.Deferred, decision.Outcome);
        Assert.Equal("risk.provider-unavailable", decision.ReasonCode);
        Assert.Null(decision.RiskInput.Observation);
    }

    [Fact]
    public void MissingObservationProducesDeferredDecision()
    {
        RiskSignalInput missing = new(
            RiskSignalAvailability.Available,
            "fraud-service",
            Observation: null);

        GovernanceDecision decision = Evaluate(
            SampleScenarios.CreatePayment(),
            missing,
            SampleScenarios.CreatePolicy());

        Assert.Equal(DecisionOutcome.Deferred, decision.Outcome);
        Assert.Equal("risk.observation-missing", decision.ReasonCode);
    }

    [Fact]
    public void FutureObservationProducesDeferredDecision()
    {
        GovernanceDecision decision = _policyEvaluator.Evaluate(
            "decision-future-observation",
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(
                SampleScenarios.CreateObservation(
                    observedAtUtc: SampleScenarios.BaselineUtc.AddMinutes(2),
                    providerValidUntilUtc: SampleScenarios.BaselineUtc.AddMinutes(12))),
            SampleScenarios.CreatePolicy(),
            SampleScenarios.BaselineUtc.AddMinutes(1));

        Assert.Equal(DecisionOutcome.Deferred, decision.Outcome);
        Assert.Equal("risk.observation-not-yet-valid", decision.ReasonCode);
    }

    [Fact]
    public void UnapprovedSignalNameIsDeferred()
    {
        RiskObservation observation = SampleScenarios.CreateObservation() with
        {
            SignalName = "payment.unapproved-risk-signal"
        };

        GovernanceDecision decision = Evaluate(
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(observation),
            SampleScenarios.CreatePolicy());

        Assert.Equal(DecisionOutcome.Deferred, decision.Outcome);
        Assert.Equal("risk.signal-unapproved", decision.ReasonCode);
    }

    [Fact]
    public void UnapprovedProviderIsDeferred()
    {
        GovernanceDecision decision = Evaluate(
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(
                SampleScenarios.CreateObservation(providerId: "unapproved-risk-service")),
            SampleScenarios.CreatePolicy());

        Assert.Equal(DecisionOutcome.Deferred, decision.Outcome);
        Assert.Equal("risk.provider-unapproved", decision.ReasonCode);
    }

    [Fact]
    public void UnapprovedModelIsDeferred()
    {
        GovernanceDecision decision = Evaluate(
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(
                SampleScenarios.CreateObservation(modelVersion: "risk-v99")),
            SampleScenarios.CreatePolicy());

        Assert.Equal(DecisionOutcome.Deferred, decision.Outcome);
        Assert.Equal("risk.model-unapproved", decision.ReasonCode);
    }

    [Fact]
    public void StaleObservationProducesDeferredDecision()
    {
        GovernanceDecision decision = _policyEvaluator.Evaluate(
            "decision-stale-observation",
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            SampleScenarios.CreatePolicy(),
            SampleScenarios.BaselineUtc.AddMinutes(10));

        Assert.Equal(DecisionOutcome.Deferred, decision.Outcome);
        Assert.Equal("risk.signal-stale", decision.ReasonCode);
    }

    [Fact]
    public void HostMaximumAgeCanExpireProviderStillValidObservation()
    {
        RiskGovernancePolicy policy = SampleScenarios.CreatePolicy(
            maximumSignalAge: TimeSpan.FromMinutes(5));
        RiskObservation observation = SampleScenarios.CreateObservation(
            providerValidUntilUtc: SampleScenarios.BaselineUtc.AddMinutes(30));

        GovernanceDecision decision = _policyEvaluator.Evaluate(
            "decision-host-max-age",
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(observation),
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(6));

        Assert.Equal(DecisionOutcome.Deferred, decision.Outcome);
        Assert.Equal("risk.signal-stale", decision.ReasonCode);
    }

    [Fact]
    public void DegradedModelHealthProducesEscalationRecommended()
    {
        GovernanceDecision decision = Evaluate(
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(
                SampleScenarios.CreateObservation(modelHealth: ModelHealth.Degraded)),
            SampleScenarios.CreatePolicy());

        Assert.Equal(DecisionOutcome.EscalationRecommended, decision.Outcome);
        Assert.Equal("risk.model-health-degraded", decision.ReasonCode);
    }

    [Fact]
    public void HighRiskProducesDeniedDecision()
    {
        GovernanceDecision decision = Evaluate(
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(
                SampleScenarios.CreateObservation(fraudProbability: 0.95m)),
            SampleScenarios.CreatePolicy());

        Assert.Equal(DecisionOutcome.Denied, decision.Outcome);
        Assert.Equal("risk.probability-denied", decision.ReasonCode);
    }

    [Fact]
    public void ElevatedRiskProducesEscalationRecommended()
    {
        GovernanceDecision decision = Evaluate(
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(
                SampleScenarios.CreateObservation(fraudProbability: 0.76m)),
            SampleScenarios.CreatePolicy());

        Assert.Equal(DecisionOutcome.EscalationRecommended, decision.Outcome);
        Assert.Equal("risk.probability-escalated", decision.ReasonCode);
    }

    [Fact]
    public void ElevatedIncidentPostureCanEscalateLowScore()
    {
        GovernanceDecision decision = Evaluate(
            SampleScenarios.CreatePayment(incidentPosture: IncidentPosture.Elevated),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            SampleScenarios.CreatePolicy());

        Assert.Equal(DecisionOutcome.EscalationRecommended, decision.Outcome);
        Assert.Equal("risk.incident-posture-escalated", decision.ReasonCode);
    }

    [Fact]
    public void ThresholdChangeCanChangeOutcomeWithoutChangingObservation()
    {
        RiskObservation observation = SampleScenarios.CreateObservation(
            fraudProbability: 0.76m);

        GovernanceDecision oldDecision = Evaluate(
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(observation),
            SampleScenarios.CreatePolicy(
                thresholdVersion: "threshold-v12",
                escalationThreshold: 0.80m));

        GovernanceDecision newDecision = Evaluate(
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(observation),
            SampleScenarios.CreatePolicy(
                policyVersion: "payment-policy-v13",
                thresholdVersion: "threshold-v13",
                escalationThreshold: 0.75m));

        Assert.Equal(DecisionOutcome.Allowed, oldDecision.Outcome);
        Assert.Equal(DecisionOutcome.EscalationRecommended, newDecision.Outcome);
        Assert.Equal(oldDecision.RiskInput.Observation, newDecision.RiskInput.Observation);
        Assert.NotEqual(oldDecision.DecisionId, newDecision.DecisionId);
        Assert.NotEqual(oldDecision.ThresholdVersion, newDecision.ThresholdVersion);
    }

    [Fact]
    public void ModelChangePreservesHistoricalObservation()
    {
        GovernanceDecision historical = Evaluate(
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(
                SampleScenarios.CreateObservation(
                    fraudProbability: 0.21m,
                    observationId: "risk-observation-1001",
                    modelVersion: "risk-v7")),
            SampleScenarios.CreatePolicy());

        GovernanceDecision current = Evaluate(
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(
                SampleScenarios.CreateObservation(
                    fraudProbability: 0.76m,
                    observationId: "risk-observation-2001",
                    modelVersion: "risk-v8")),
            SampleScenarios.CreatePolicy());

        Assert.Equal(0.21m, historical.RiskInput.Observation?.FraudProbability);
        Assert.Equal("risk-v7", historical.RiskInput.Observation?.ModelVersion);
        Assert.Equal("risk-observation-1001", historical.RiskInput.Observation?.ObservationId);
        Assert.NotEqual(historical.DecisionId, current.DecisionId);
        Assert.Equal(0.76m, current.RiskInput.Observation?.FraudProbability);
        Assert.Equal("risk-v8", current.RiskInput.Observation?.ModelVersion);
        Assert.Equal("risk-observation-2001", current.RiskInput.Observation?.ObservationId);
    }

    [Fact]
    public void SeparateEvaluationsKeepDistinctDecisionIdentityWhenOutcomeChanges()
    {
        RiskGovernancePolicy policy = SampleScenarios.CreatePolicy();
        RiskObservation observation = SampleScenarios.CreateObservation();

        GovernanceDecision current = _policyEvaluator.Evaluate(
            "decision-current-risk",
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(observation),
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(1));
        GovernanceDecision stale = _policyEvaluator.Evaluate(
            "decision-stale-risk",
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(observation),
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(10));

        Assert.Equal(DecisionOutcome.Allowed, current.Outcome);
        Assert.Equal(DecisionOutcome.Deferred, stale.Outcome);
        Assert.Equal(current.RiskInput.Observation, stale.RiskInput.Observation);
        Assert.NotEqual(current.DecisionId, stale.DecisionId);
        Assert.NotEqual(current.DecidedAtUtc, stale.DecidedAtUtc);
    }

    [Fact]
    public void NonAllowedDecisionCannotMintAuthority()
    {
        GovernanceDecision decision = Evaluate(
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Unavailable("fraud-service"),
            SampleScenarios.CreatePolicy());

        AuthorityIssueResult issue = _authorityIssuer.TryIssue(
            decision,
            SampleScenarios.CreatePolicy(),
            SampleScenarios.BaselineUtc.AddMinutes(1));

        Assert.False(issue.Issued);
        Assert.Equal("authority.decision-not-allowed", issue.ReasonCode);
        Assert.Null(issue.Authority);
    }

    [Fact]
    public void MissingRiskEvidenceCannotMintAuthority()
    {
        GovernanceDecision allowed = Evaluate(
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            SampleScenarios.CreatePolicy());
        GovernanceDecision missingEvidence = allowed with
        {
            RiskInput = RiskSignalInput.Unavailable("fraud-service")
        };

        AuthorityIssueResult issue = _authorityIssuer.TryIssue(
            missingEvidence,
            SampleScenarios.CreatePolicy(),
            SampleScenarios.BaselineUtc.AddMinutes(1));

        Assert.False(issue.Issued);
        Assert.Equal("authority.risk-evidence-missing", issue.ReasonCode);
        Assert.Null(issue.Authority);
    }

    [Fact]
    public void PolicyMismatchCannotMintAuthority()
    {
        GovernanceDecision decision = Evaluate(
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            SampleScenarios.CreatePolicy());

        AuthorityIssueResult issue = _authorityIssuer.TryIssue(
            decision,
            SampleScenarios.CreatePolicy(policyVersion: "payment-policy-v13"),
            SampleScenarios.BaselineUtc.AddMinutes(1));

        Assert.False(issue.Issued);
        Assert.Equal("authority.policy-mismatch", issue.ReasonCode);
        Assert.Null(issue.Authority);
    }

    [Fact]
    public void AuthorityExpiryIsBoundedByRiskFreshness()
    {
        RiskGovernancePolicy policy = SampleScenarios.CreatePolicy(
            maximumSignalAge: TimeSpan.FromMinutes(5));
        RiskObservation observation = SampleScenarios.CreateObservation(
            providerValidUntilUtc: SampleScenarios.BaselineUtc.AddMinutes(30));
        DateTimeOffset decisionTime = SampleScenarios.BaselineUtc.AddMinutes(1);
        GovernanceDecision decision = _policyEvaluator.Evaluate(
            "decision-authority-expiry",
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(observation),
            policy,
            decisionTime);

        AuthorityIssueResult issue = _authorityIssuer.TryIssue(
            decision,
            policy,
            decisionTime,
            TimeSpan.FromMinutes(20));
        ExecutionAuthority authority = Assert.IsType<ExecutionAuthority>(issue.Authority);

        Assert.True(issue.Issued);
        Assert.Equal("authority.issued", issue.ReasonCode);
        Assert.Equal(SampleScenarios.BaselineUtc.AddMinutes(5), authority.ExpiresAtUtc);
    }

    [Fact]
    public void StaleRiskEvidenceCannotMintNewAuthority()
    {
        RiskGovernancePolicy policy = SampleScenarios.CreatePolicy();
        GovernanceDecision decision = Evaluate(
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            policy);

        AuthorityIssueResult issue = _authorityIssuer.TryIssue(
            decision,
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(11));

        Assert.False(issue.Issued);
        Assert.Equal("authority.risk-evidence-stale", issue.ReasonCode);
        Assert.Null(issue.Authority);
    }

    [Fact]
    public void FreshnessEvaluatorReturnsCurrentForUnchangedAuthority()
    {
        RiskGovernancePolicy policy = SampleScenarios.CreatePolicy();
        ExecutionAuthority authority = SampleScenarios.CreateAuthority(policy);

        FreshnessAssessment assessment = new ExecutionFreshnessEvaluator().Evaluate(
            authority,
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(2));

        Assert.Equal(FreshnessAction.Proceed, assessment.Action);
        Assert.Equal("freshness.current", assessment.ReasonCode);
    }

    [Fact]
    public async Task CurrentAuthorityReachesExecutorAfterValidation()
    {
        RecordingPaymentExecutor executor = new();
        RiskExecutionGateway gateway = CreateGateway(executor);
        RiskGovernancePolicy policy = SampleScenarios.CreatePolicy();

        ExecutionResult result = await gateway.TryExecuteAsync(
            SampleScenarios.CreateAuthority(policy),
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(2),
            CancellationToken.None);

        Assert.True(result.Executed);
        Assert.Equal(FreshnessAction.Proceed, result.Action);
        Assert.Equal("execution.completed", result.ReasonCode);
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task ReplayedAuthorityDoesNotExecuteTwice()
    {
        RiskGovernancePolicy policy = SampleScenarios.CreatePolicy();
        ExecutionAuthority authority = SampleScenarios.CreateAuthority(policy);
        InMemoryExecutionAuthorityClaimStore claimStore = new();
        RecordingPaymentExecutor executor = new();
        RiskExecutionGateway gateway = CreateGateway(executor, claimStore);

        ExecutionResult first = await gateway.TryExecuteAsync(
            authority,
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(2),
            CancellationToken.None);
        ExecutionResult second = await gateway.TryExecuteAsync(
            authority,
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(2),
            CancellationToken.None);

        Assert.True(first.Executed);
        Assert.False(second.Executed);
        Assert.Equal(FreshnessAction.Reject, second.Action);
        Assert.Equal("authority.already-claimed", second.ReasonCode);
        Assert.Equal(1, claimStore.GetClaimCount(authority.AuthorityId));
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task TwoActuallyConcurrentAuthorityClaimsProduceOneExecution()
    {
        RiskGovernancePolicy policy = SampleScenarios.CreatePolicy();
        ExecutionAuthority authority = SampleScenarios.CreateAuthority(policy);
        InMemoryExecutionAuthorityClaimStore claimStore = new();
        CoordinatedPaymentExecutor executor = new();
        RiskExecutionGateway gateway = new(
            new ExecutionFreshnessEvaluator(),
            claimStore,
            executor);

        Task<ExecutionResult> firstTask = gateway.TryExecuteAsync(
            authority,
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(2),
            CancellationToken.None);

        await executor.Entered;

        Task<ExecutionResult> secondTask = gateway.TryExecuteAsync(
            authority,
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(2),
            CancellationToken.None);

        ExecutionResult second = await secondTask;
        executor.Release();
        ExecutionResult first = await firstTask;

        Assert.True(first.Executed);
        Assert.False(second.Executed);
        Assert.Equal("authority.already-claimed", second.ReasonCode);
        Assert.Equal(1, claimStore.GetClaimCount(authority.AuthorityId));
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task ExecutorRejectsCommandResourceSubstitution()
    {
        ExecutionAuthority authority = SampleScenarios.CreateAuthority();
        RecordingPaymentExecutor executor = new(
            executionNowUtc: SampleScenarios.BaselineUtc.AddMinutes(2));
        ValidatedPaymentCommand command = CreateCommand(authority) with
        {
            ExpectedResourceVersion = "pay-981:v999"
        };

        PaymentExecutionAttempt attempt = await executor.ExecuteAsync(
            authority,
            command,
            CancellationToken.None);

        Assert.False(attempt.Executed);
        Assert.Equal("authority.binding-mismatch", attempt.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task ExecutorRejectionAfterClaimLeavesAuthorityConsumed()
    {
        RiskGovernancePolicy policy = SampleScenarios.CreatePolicy();
        ExecutionAuthority authority = SampleScenarios.CreateAuthority(policy);
        InMemoryExecutionAuthorityClaimStore claimStore = new();
        RecordingPaymentExecutor executor = new(
            executionNowUtc: authority.ExpiresAtUtc);
        RiskExecutionGateway gateway = CreateGateway(executor, claimStore);

        ExecutionResult first = await gateway.TryExecuteAsync(
            authority,
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(2),
            CancellationToken.None);
        ExecutionResult retry = await gateway.TryExecuteAsync(
            authority,
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(2),
            CancellationToken.None);

        Assert.False(first.Executed);
        Assert.Equal("authority.expired", first.ReasonCode);
        Assert.Equal(1, claimStore.GetClaimCount(authority.AuthorityId));
        Assert.False(retry.Executed);
        Assert.Equal("authority.already-claimed", retry.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task NotYetValidAuthorityIsRejectedBeforeExecution()
    {
        RecordingPaymentExecutor executor = new();
        RiskExecutionGateway gateway = CreateGateway(executor);
        RiskGovernancePolicy policy = SampleScenarios.CreatePolicy();
        ExecutionAuthority authority = SampleScenarios.CreateAuthority(policy) with
        {
            IssuedAtUtc = SampleScenarios.BaselineUtc.AddMinutes(2).AddSeconds(30)
        };

        ExecutionResult result = await gateway.TryExecuteAsync(
            authority,
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(2),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(FreshnessAction.Reject, result.Action);
        Assert.Equal("authority.not-yet-valid", result.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Theory]
    [InlineData("audience", "authority.audience-mismatch")]
    [InlineData("operation", "authority.operation-mismatch")]
    public async Task AuthorityExecutionBoundaryBindingsAreValidated(
        string binding,
        string expectedReason)
    {
        RecordingPaymentExecutor executor = new();
        RiskExecutionGateway gateway = CreateGateway(executor);
        RiskGovernancePolicy policy = SampleScenarios.CreatePolicy();
        ExecutionAuthority authority = SampleScenarios.CreateAuthority(policy);
        ExecutionAuthority mismatched = binding switch
        {
            "audience" => authority with { Audience = "other-executor" },
            "operation" => authority with { Operation = "payment.cancel" },
            _ => throw new ArgumentOutOfRangeException(nameof(binding))
        };

        ExecutionResult result = await gateway.TryExecuteAsync(
            mismatched,
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(2),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(FreshnessAction.Reject, result.Action);
        Assert.Equal(expectedReason, result.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task ExpiredAuthorityIsRejectedBeforeExecution()
    {
        RecordingPaymentExecutor executor = new();
        RiskExecutionGateway gateway = CreateGateway(executor);
        RiskGovernancePolicy policy = SampleScenarios.CreatePolicy();

        ExecutionResult result = await gateway.TryExecuteAsync(
            SampleScenarios.CreateAuthority(policy),
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(4),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(FreshnessAction.Reject, result.Action);
        Assert.Equal("authority.expired", result.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Theory]
    [InlineData("policy", "risk.policy-version-drift")]
    [InlineData("threshold", "risk.threshold-policy-drift")]
    [InlineData("freshness", "risk.freshness-policy-drift")]
    public async Task PolicyIdentityDriftRequiresReevaluation(
        string drift,
        string expectedReason)
    {
        RiskGovernancePolicy current = drift switch
        {
            "policy" => SampleScenarios.CreatePolicy(policyVersion: "payment-policy-v13"),
            "threshold" => SampleScenarios.CreatePolicy(thresholdVersion: "threshold-v13"),
            "freshness" => SampleScenarios.CreatePolicy(freshnessRuleVersion: "freshness-v2"),
            _ => throw new ArgumentOutOfRangeException(nameof(drift))
        };

        ExecutionResult result = await ExecuteBlocked(currentPolicy: current);

        AssertReevaluation(result, expectedReason);
    }

    [Fact]
    public async Task ResourceIdentitySubstitutionIsRejected()
    {
        ExecutionResult result = await ExecuteBlocked(
            currentContext: SampleScenarios.CreatePayment(paymentId: "pay-999"));

        Assert.False(result.Executed);
        Assert.Equal(FreshnessAction.Reject, result.Action);
        Assert.Equal("authority.resource-mismatch", result.ReasonCode);
    }

    [Fact]
    public async Task ResourceIdentitySubstitutionTakesPrecedenceOverPolicyDrift()
    {
        ExecutionResult result = await ExecuteBlocked(
            currentContext: SampleScenarios.CreatePayment(paymentId: "pay-999"),
            currentPolicy: SampleScenarios.CreatePolicy(
                policyVersion: "payment-policy-v13"));

        Assert.False(result.Executed);
        Assert.Equal(FreshnessAction.Reject, result.Action);
        Assert.Equal("authority.resource-mismatch", result.ReasonCode);
    }

    [Theory]
    [InlineData("resource", "context.resource-drift")]
    [InlineData("amount", "context.amount-drift")]
    [InlineData("destination", "context.destination-drift")]
    [InlineData("incident", "context.incident-posture-drift")]
    [InlineData("environment", "context.environment-drift")]
    public async Task MaterialContextDriftRequiresReevaluation(
        string drift,
        string expectedReason)
    {
        PaymentContext current = drift switch
        {
            "resource" => SampleScenarios.CreatePayment(resourceVersion: "pay-981:v2"),
            "amount" => SampleScenarios.CreatePayment(amount: 300_000m),
            "destination" => SampleScenarios.CreatePayment(destinationApproved: false),
            "incident" => SampleScenarios.CreatePayment(incidentPosture: IncidentPosture.Elevated),
            "environment" => SampleScenarios.CreatePayment(environmentVersion: "env-elevated-v2"),
            _ => throw new ArgumentOutOfRangeException(nameof(drift))
        };

        ExecutionResult result = await ExecuteBlocked(currentContext: current);

        AssertReevaluation(result, expectedReason);
    }

    [Fact]
    public async Task CurrentProviderUnavailableDefersExecution()
    {
        ExecutionResult result = await ExecuteBlocked(
            currentRisk: RiskSignalInput.Unavailable("fraud-service"));

        Assert.False(result.Executed);
        Assert.Equal(FreshnessAction.Defer, result.Action);
        Assert.Equal("risk.provider-unavailable", result.ReasonCode);
    }

    [Fact]
    public async Task CurrentObservationMissingDefersExecution()
    {
        RiskSignalInput missing = new(
            RiskSignalAvailability.Available,
            "fraud-service",
            Observation: null);

        ExecutionResult result = await ExecuteBlocked(currentRisk: missing);

        Assert.False(result.Executed);
        Assert.Equal(FreshnessAction.Defer, result.Action);
        Assert.Equal("risk.observation-missing", result.ReasonCode);
    }

    [Fact]
    public async Task CurrentUnapprovedSignalDefersInsteadOfReportingDrift()
    {
        RiskObservation observation = SampleScenarios.CreateObservation(
            observationId: "risk-observation-2001") with
        {
            SignalName = "payment.unapproved-risk-signal"
        };

        ExecutionResult result = await ExecuteBlocked(
            currentRisk: RiskSignalInput.Available(observation));

        Assert.False(result.Executed);
        Assert.Equal(FreshnessAction.Defer, result.Action);
        Assert.Equal("risk.signal-unapproved", result.ReasonCode);
    }

    [Fact]
    public async Task CurrentUnapprovedProviderDefersInsteadOfReportingDrift()
    {
        ExecutionResult result = await ExecuteBlocked(
            currentRisk: RiskSignalInput.Available(
                SampleScenarios.CreateObservation(
                    observationId: "risk-observation-2001",
                    providerId: "unapproved-risk-service")));

        Assert.False(result.Executed);
        Assert.Equal(FreshnessAction.Defer, result.Action);
        Assert.Equal("risk.provider-unapproved", result.ReasonCode);
    }

    [Fact]
    public async Task CurrentUnapprovedModelDefersInsteadOfReportingDrift()
    {
        ExecutionResult result = await ExecuteBlocked(
            currentRisk: RiskSignalInput.Available(
                SampleScenarios.CreateObservation(
                    observationId: "risk-observation-2001",
                    modelVersion: "risk-v99")));

        Assert.False(result.Executed);
        Assert.Equal(FreshnessAction.Defer, result.Action);
        Assert.Equal("risk.model-unapproved", result.ReasonCode);
    }

    [Fact]
    public async Task FutureCurrentObservationDefersExecution()
    {
        RiskObservation future = SampleScenarios.CreateObservation(
            observedAtUtc: SampleScenarios.BaselineUtc.AddMinutes(3),
            providerValidUntilUtc: SampleScenarios.BaselineUtc.AddMinutes(13));

        ExecutionResult result = await ExecuteBlocked(
            currentRisk: RiskSignalInput.Available(future));

        Assert.False(result.Executed);
        Assert.Equal(FreshnessAction.Defer, result.Action);
        Assert.Equal("risk.observation-not-yet-valid", result.ReasonCode);
    }

    [Fact]
    public async Task StaleSignalCanRequireReevaluation()
    {
        RiskGovernancePolicy policy = SampleScenarios.CreatePolicy();
        ExecutionAuthority authority = SampleScenarios.CreateAuthority(policy) with
        {
            // The normal issuer would cap this authority at the risk-freshness
            // boundary. Extending it here isolates the evaluator's stale branch.
            ExpiresAtUtc = SampleScenarios.BaselineUtc.AddMinutes(30)
        };
        RecordingPaymentExecutor executor = new();
        RiskExecutionGateway gateway = CreateGateway(executor);

        ExecutionResult result = await gateway.TryExecuteAsync(
            authority,
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(11),
            CancellationToken.None);

        AssertReevaluation(result, "risk.signal-stale");
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task StaleSignalCanDefer()
    {
        RiskGovernancePolicy policy = SampleScenarios.CreatePolicy(
            staleSignalDisposition: StaleSignalDisposition.Defer);
        ExecutionAuthority authority = SampleScenarios.CreateAuthority(policy) with
        {
            // See StaleSignalCanRequireReevaluation: this isolates stale-policy
            // behavior from the issuer's stricter freshness-bounded expiration.
            ExpiresAtUtc = SampleScenarios.BaselineUtc.AddMinutes(30)
        };
        RecordingPaymentExecutor executor = new();
        RiskExecutionGateway gateway = CreateGateway(executor);

        ExecutionResult result = await gateway.TryExecuteAsync(
            authority,
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(11),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(FreshnessAction.Defer, result.Action);
        Assert.Equal("risk.signal-stale", result.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task ProvenanceDriftTakesPrecedenceOverStaleDisposition()
    {
        RiskGovernancePolicy policy = SampleScenarios.CreatePolicy(
            staleSignalDisposition: StaleSignalDisposition.Defer);
        RiskObservation staleNewModel = SampleScenarios.CreateObservation(
            observationId: "risk-observation-2001",
            modelVersion: "risk-v8",
            observedAtUtc: SampleScenarios.BaselineUtc.AddMinutes(-20),
            providerValidUntilUtc: SampleScenarios.BaselineUtc.AddMinutes(20));

        ExecutionResult result = await ExecuteBlocked(
            currentRisk: RiskSignalInput.Available(staleNewModel),
            currentPolicy: policy);

        AssertReevaluation(result, "risk.model-drift");
    }

    [Theory]
    [InlineData("signal", "risk.signal-drift")]
    [InlineData("provider", "risk.provider-drift")]
    [InlineData("model", "risk.model-drift")]
    [InlineData("scoring", "risk.scoring-method-drift")]
    [InlineData("calibration", "risk.calibration-drift")]
    [InlineData("health", "risk.model-health-drift")]
    [InlineData("observation", "risk.observation-drift")]
    public async Task RiskProvenanceDriftRequiresReevaluation(
        string drift,
        string expectedReason)
    {
        RiskObservation current = drift switch
        {
            "signal" => SampleScenarios.CreateObservation(
                observationId: "risk-observation-2001") with
            {
                SignalName = "payment.fraud-probability-v2"
            },
            "provider" => SampleScenarios.CreateObservation(
                observationId: "risk-observation-2001",
                providerId: "backup-fraud-service"),
            "model" => SampleScenarios.CreateObservation(
                observationId: "risk-observation-2001",
                modelVersion: "risk-v8"),
            "scoring" => SampleScenarios.CreateObservation(
                observationId: "risk-observation-2001",
                scoringMethodVersion: "fraud-score-v4"),
            "calibration" => SampleScenarios.CreateObservation(
                observationId: "risk-observation-2001",
                calibrationVersion: "fraud-cal-2026-09"),
            "health" => SampleScenarios.CreateObservation(
                observationId: "risk-observation-2001",
                modelHealth: ModelHealth.Degraded),
            "observation" => SampleScenarios.CreateObservation(
                observationId: "risk-observation-2001"),
            _ => throw new ArgumentOutOfRangeException(nameof(drift))
        };
        ExecutionResult result = await ExecuteBlocked(
            currentRisk: RiskSignalInput.Available(current));

        AssertReevaluation(result, expectedReason);
    }

    [Fact]
    public async Task MutatedObservationWithSameIdentityIsRejected()
    {
        ExecutionResult result = await ExecuteBlocked(
            currentRisk: RiskSignalInput.Available(
                SampleScenarios.CreateObservation(fraudProbability: 0.22m)));

        Assert.False(result.Executed);
        Assert.Equal(FreshnessAction.Reject, result.Action);
        Assert.Equal("risk.observation-integrity-mismatch", result.ReasonCode);
    }

    [Fact]
    public async Task SameObservationIdentityWithModelMutationIsRejected()
    {
        ExecutionResult result = await ExecuteBlocked(
            currentRisk: RiskSignalInput.Available(
                SampleScenarios.CreateObservation(modelVersion: "risk-v8")));

        Assert.False(result.Executed);
        Assert.Equal(FreshnessAction.Reject, result.Action);
        Assert.Equal("risk.observation-integrity-mismatch", result.ReasonCode);
    }

    [Fact]
    public async Task MaterialDriftBlocksOldAuthorityAndCurrentReevaluationEscalates()
    {
        RiskGovernancePolicy policy = SampleScenarios.CreatePolicy();
        GovernanceDecision historical = Evaluate(
            SampleScenarios.CreatePayment(),
            RiskSignalInput.Available(
                SampleScenarios.CreateObservation(
                    fraudProbability: 0.21m,
                    observationId: "risk-observation-1001",
                    modelVersion: "risk-v7")),
            policy);
        ExecutionAuthority oldAuthority = RequireIssued(
            _authorityIssuer.TryIssue(
                historical,
                policy,
                SampleScenarios.BaselineUtc.AddMinutes(1),
                TimeSpan.FromMinutes(8)));

        PaymentContext currentContext = SampleScenarios.CreatePayment(
            resourceVersion: "pay-981:v2",
            incidentPosture: IncidentPosture.Elevated,
            environmentVersion: "env-elevated-v2");
        RiskObservation currentObservation = SampleScenarios.CreateObservation(
            fraudProbability: 0.76m,
            observationId: "risk-observation-2001",
            modelVersion: "risk-v8",
            observedAtUtc: SampleScenarios.BaselineUtc.AddMinutes(4),
            providerValidUntilUtc: SampleScenarios.BaselineUtc.AddMinutes(14));

        RecordingPaymentExecutor executor = new();
        ExecutionResult oldAuthorityResult = await CreateGateway(executor).TryExecuteAsync(
            oldAuthority,
            currentContext,
            RiskSignalInput.Available(currentObservation),
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(5),
            CancellationToken.None);
        GovernanceDecision current = _policyEvaluator.Evaluate(
            "decision-current-drift-state",
            currentContext,
            RiskSignalInput.Available(currentObservation),
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(5));

        AssertReevaluation(oldAuthorityResult, "context.resource-drift");
        Assert.Equal(0, executor.InvocationCount);
        Assert.Equal(DecisionOutcome.Allowed, historical.Outcome);
        Assert.Equal(0.21m, historical.RiskInput.Observation?.FraudProbability);
        Assert.Equal("risk-v7", historical.RiskInput.Observation?.ModelVersion);
        Assert.Equal(DecisionOutcome.EscalationRecommended, current.Outcome);
        Assert.Equal(0.76m, current.RiskInput.Observation?.FraudProbability);
        Assert.Equal("risk-v8", current.RiskInput.Observation?.ModelVersion);
    }

    private GovernanceDecision Evaluate(
        PaymentContext context,
        RiskSignalInput riskInput,
        RiskGovernancePolicy policy,
        string? decisionId = null) =>
        _policyEvaluator.Evaluate(
            decisionId ?? $"decision-test-{Interlocked.Increment(ref _decisionSequence)}",
            context,
            riskInput,
            policy,
            SampleScenarios.BaselineUtc.AddMinutes(1));

    private static RiskExecutionGateway CreateGateway(
        RecordingPaymentExecutor executor,
        InMemoryExecutionAuthorityClaimStore? claimStore = null) =>
        new(
            new ExecutionFreshnessEvaluator(),
            claimStore ?? new InMemoryExecutionAuthorityClaimStore(),
            executor);

    private static async Task<ExecutionResult> ExecuteBlocked(
        PaymentContext? currentContext = null,
        RiskSignalInput? currentRisk = null,
        RiskGovernancePolicy? currentPolicy = null)
    {
        RiskGovernancePolicy initialPolicy = SampleScenarios.CreatePolicy();
        RecordingPaymentExecutor executor = new();
        RiskExecutionGateway gateway = CreateGateway(executor);

        ExecutionResult result = await gateway.TryExecuteAsync(
            SampleScenarios.CreateAuthority(initialPolicy),
            currentContext ?? SampleScenarios.CreatePayment(),
            currentRisk ?? RiskSignalInput.Available(SampleScenarios.CreateObservation()),
            currentPolicy ?? initialPolicy,
            SampleScenarios.BaselineUtc.AddMinutes(2),
            CancellationToken.None);

        Assert.Equal(0, executor.InvocationCount);
        return result;
    }

    private static ValidatedPaymentCommand CreateCommand(
        ExecutionAuthority authority) =>
        new(
            PaymentId: authority.PaymentId,
            ExpectedResourceVersion: authority.ResourceVersion,
            Amount: authority.Amount,
            DestinationApproved: authority.DestinationApproved,
            IncidentPosture: authority.IncidentPosture,
            EnvironmentVersion: authority.EnvironmentVersion,
            ValidatedAtUtc: authority.IssuedAtUtc,
            DecisionId: authority.DecisionId,
            AuthorityId: authority.AuthorityId,
            Audience: authority.Audience,
            Operation: authority.Operation,
            RiskObservationId: authority.RiskObservationId,
            PolicyId: authority.PolicyId,
            PolicyVersion: authority.PolicyVersion,
            ThresholdVersion: authority.ThresholdVersion,
            FreshnessRuleVersion: authority.FreshnessRuleVersion);

    private sealed class CoordinatedPaymentExecutor : IPaymentExecutor
    {
        private readonly TaskCompletionSource<bool> _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _invocationCount;

        public Task Entered => _entered.Task;

        public int InvocationCount =>
            Volatile.Read(ref _invocationCount);

        public void Release() =>
            _release.TrySetResult(true);

        public async Task<PaymentExecutionAttempt> ExecuteAsync(
            ExecutionAuthority authority,
            ValidatedPaymentCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(authority);
            ArgumentNullException.ThrowIfNull(command);

            Interlocked.Increment(ref _invocationCount);
            _entered.TrySetResult(true);
            await _release.Task.WaitAsync(cancellationToken);
            return PaymentExecutionAttempt.Success();
        }
    }

    private static ExecutionAuthority RequireIssued(AuthorityIssueResult result)
    {
        Assert.True(result.Issued);
        return Assert.IsType<ExecutionAuthority>(result.Authority);
    }

    private static void AssertReevaluation(
        ExecutionResult result,
        string expectedReasonCode)
    {
        Assert.False(result.Executed);
        Assert.Equal(FreshnessAction.Reevaluate, result.Action);
        Assert.Equal(expectedReasonCode, result.ReasonCode);
    }
}
