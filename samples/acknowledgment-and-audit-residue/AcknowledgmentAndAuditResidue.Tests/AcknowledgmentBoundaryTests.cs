using Xunit;

namespace AcknowledgmentAndAuditResidue.Tests;

public sealed class AcknowledgmentBoundaryTests
{
    private static readonly DateTimeOffset NowUtc =
        new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AcknowledgmentDoesNotGrantExecutionAuthority()
    {
        var policy = new DisableAccountPolicy();
        var validator = new AcknowledgmentValidator();
        var executor = new RecordingExecutor();
        DisableAccountPolicyContext context = CreateContext();

        GovernanceDecision initialDecision = policy.Evaluate(context);

        Assert.Equal(
            GovernanceDecisionOutcome.AcknowledgmentRequired,
            initialDecision.Outcome);
        Assert.False(initialDecision.CanProceed);
        Assert.Equal(0, executor.InvocationCount);

        AcknowledgmentChallenge challenge = CreateChallenge(
            context,
            initialDecision,
            NowUtc);
        AcknowledgmentResponse response = CreateAcceptedResponse(
            challenge,
            NowUtc.AddSeconds(1));
        AcknowledgmentValidation validation = validator.Validate(
            challenge,
            response,
            response.OccurredUtc);

        Assert.True(validation.IsValid);
        Assert.Equal(0, executor.InvocationCount);

        DisableAccountPolicyContext acknowledgedContext = context with
        {
            RequiredAcknowledgmentSatisfied = true
        };
        GovernanceDecision reevaluatedDecision =
            policy.Evaluate(acknowledgedContext);

        Assert.Equal(
            GovernanceDecisionOutcome.Allowed,
            reevaluatedDecision.Outcome);
        Assert.Equal(0, executor.InvocationCount);

        executor.Execute(acknowledgedContext.Intent);

        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public void ResourceChangedAfterAcknowledgmentBlocksExecution()
    {
        var policy = new DisableAccountPolicy();
        var validator = new AcknowledgmentValidator();
        var executor = new RecordingExecutor();
        DisableAccountPolicyContext context = CreateContext();
        GovernanceDecision initialDecision = policy.Evaluate(context);
        AcknowledgmentChallenge challenge = CreateChallenge(
            context,
            initialDecision,
            NowUtc);
        AcknowledgmentResponse response = CreateAcceptedResponse(
            challenge,
            NowUtc.AddSeconds(1));

        AcknowledgmentValidation validation = validator.Validate(
            challenge,
            response,
            response.OccurredUtc);

        Assert.True(validation.IsValid);

        DisableAccountPolicyContext changedContext = context with
        {
            RequiredAcknowledgmentSatisfied = true,
            Account = context.Account with { IsProtected = true }
        };
        GovernanceDecision reevaluatedDecision =
            policy.Evaluate(changedContext);

        Assert.Equal(
            GovernanceDecisionOutcome.EscalationRecommended,
            reevaluatedDecision.Outcome);
        Assert.False(reevaluatedDecision.CanProceed);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public void ExpiredAcknowledgmentDoesNotReachExecution()
    {
        var policy = new DisableAccountPolicy();
        var validator = new AcknowledgmentValidator();
        var executor = new RecordingExecutor();
        DisableAccountPolicyContext context = CreateContext();
        GovernanceDecision initialDecision = policy.Evaluate(context);
        AcknowledgmentChallenge challenge = CreateChallenge(
            context,
            initialDecision,
            NowUtc);
        DateTimeOffset expiredAt = challenge.ExpiresUtc.AddSeconds(1);
        AcknowledgmentResponse response = CreateAcceptedResponse(
            challenge,
            expiredAt);

        AcknowledgmentValidation validation = validator.Validate(
            challenge,
            response,
            expiredAt);

        Assert.False(validation.IsValid);
        Assert.Equal("acknowledgment.expired", validation.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    private static DisableAccountPolicyContext CreateContext()
    {
        return new DisableAccountPolicyContext(
            Intent: new DisableAccountIntent(
                AccountId: "user-100",
                RequestedBy: "operator-7",
                Reason: string.Empty),
            Actor: new ActorContext(
                ActorId: "operator-7",
                TenantId: "tenant-a",
                IsAdministrator: true),
            Account: new AccountContext(
                AccountId: "user-100",
                TenantId: "tenant-a",
                IsProtected: false),
            RequiredAcknowledgmentSatisfied: false,
            CorrelationId: "test-user-100",
            PolicyVersion: "3.2");
    }

    private static AcknowledgmentChallenge CreateChallenge(
        DisableAccountPolicyContext context,
        GovernanceDecision decision,
        DateTimeOffset issuedUtc)
    {
        DecisionReason reason = Assert.Single(decision.Reasons);

        return new AcknowledgmentChallenge(
            ChallengeId: $"{context.CorrelationId}-challenge",
            ActorId: context.Actor.ActorId,
            OperationName: "account.disable",
            ResourceId: context.Account.AccountId,
            ReasonCode: reason.Code,
            RequiredAcknowledgmentCode:
                "account.disable.accept-responsibility",
            CorrelationId: context.CorrelationId,
            PolicyVersion: context.PolicyVersion,
            ExpiresUtc: issuedUtc.AddMinutes(5));
    }

    private static AcknowledgmentResponse CreateAcceptedResponse(
        AcknowledgmentChallenge challenge,
        DateTimeOffset occurredUtc)
    {
        return new AcknowledgmentResponse(
            AcknowledgmentId:
                $"{challenge.CorrelationId}-acknowledgment",
            ChallengeId: challenge.ChallengeId,
            ActorId: challenge.ActorId,
            AcknowledgmentCode:
                challenge.RequiredAcknowledgmentCode,
            Accepted: true,
            OccurredUtc: occurredUtc,
            CorrelationId: challenge.CorrelationId);
    }
}
