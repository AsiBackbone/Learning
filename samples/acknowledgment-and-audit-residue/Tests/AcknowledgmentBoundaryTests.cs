using Xunit;

namespace AcknowledgmentAndAuditResidue.Tests;

public sealed class AcknowledgmentBoundaryTests
{
    private static readonly DateTimeOffset _nowUtc =
        new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AcknowledgmentDoesNotGrantExecutionAuthority()
    {
        _ = new DisableAccountPolicy();

        _ = new AcknowledgmentValidator();
        var executor = new RecordingExecutor();
        DisableAccountPolicyContext context = CreateContext();

        GovernanceDecision initialDecision = DisableAccountPolicy.Evaluate(context);

        Assert.Equal(
            GovernanceDecisionOutcome.AcknowledgmentRequired,
            initialDecision.Outcome);
        Assert.False(initialDecision.CanProceed);
        Assert.Equal(
            "account.disable.reason-required",
            Assert.Single(initialDecision.Reasons).Code);
        Assert.Equal(0, executor.InvocationCount);

        AcknowledgmentChallenge challenge = CreateChallenge(
            context,
            initialDecision,
            _nowUtc);
        AcknowledgmentResponse response = CreateAcceptedResponse(
            challenge,
            _nowUtc.AddSeconds(1));
        AcknowledgmentValidation validation = AcknowledgmentValidator.Validate(
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
            DisableAccountPolicy.Evaluate(acknowledgedContext);

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
        _ = new DisableAccountPolicy();

        _ = new AcknowledgmentValidator();
        var executor = new RecordingExecutor();
        DisableAccountPolicyContext context = CreateContext();
        GovernanceDecision initialDecision = DisableAccountPolicy.Evaluate(context);
        AcknowledgmentChallenge challenge = CreateChallenge(
            context,
            initialDecision,
            _nowUtc);
        AcknowledgmentResponse response = CreateAcceptedResponse(
            challenge,
            _nowUtc.AddSeconds(1));

        AcknowledgmentValidation validation = AcknowledgmentValidator.Validate(
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
            DisableAccountPolicy.Evaluate(changedContext);

        Assert.Equal(
            GovernanceDecisionOutcome.EscalationRecommended,
            reevaluatedDecision.Outcome);
        Assert.False(reevaluatedDecision.CanProceed);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public void ExpiredAcknowledgmentDoesNotReachExecution()
    {
        _ = new DisableAccountPolicy();

        _ = new AcknowledgmentValidator();
        var executor = new RecordingExecutor();
        DisableAccountPolicyContext context = CreateContext();
        GovernanceDecision initialDecision = DisableAccountPolicy.Evaluate(context);
        AcknowledgmentChallenge challenge = CreateChallenge(
            context,
            initialDecision,
            _nowUtc);
        DateTimeOffset expiredAt = challenge.ExpiresUtc.AddSeconds(1);
        AcknowledgmentResponse response = CreateAcceptedResponse(
            challenge,
            expiredAt);

        AcknowledgmentValidation validation = AcknowledgmentValidator.Validate(
            challenge,
            response,
            expiredAt);

        Assert.False(validation.IsValid);
        Assert.Equal("acknowledgment.expired", validation.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public void RejectedAcknowledgmentIsInvalid()
    {
        (AcknowledgmentChallenge challenge, AcknowledgmentResponse response) =
            CreateValidExchange();

        AcknowledgmentValidation validation = AcknowledgmentValidator.Validate(
            challenge,
            response with { Accepted = false },
            response.OccurredUtc);

        Assert.False(validation.IsValid);
        Assert.Equal("acknowledgment.rejected", validation.ReasonCode);
    }

    [Fact]
    public void WrongChallengeIdentifierIsInvalid()
    {
        (AcknowledgmentChallenge challenge, AcknowledgmentResponse response) =
            CreateValidExchange();

        AcknowledgmentValidation validation = AcknowledgmentValidator.Validate(
            challenge,
            response with { ChallengeId = "different-challenge" },
            response.OccurredUtc);

        Assert.False(validation.IsValid);
        Assert.Equal("acknowledgment.challenge-mismatch", validation.ReasonCode);
    }

    [Fact]
    public void WrongActorIsInvalid()
    {
        (AcknowledgmentChallenge challenge, AcknowledgmentResponse response) =
            CreateValidExchange();

        AcknowledgmentValidation validation = AcknowledgmentValidator.Validate(
            challenge,
            response with { ActorId = "operator-99" },
            response.OccurredUtc);

        Assert.False(validation.IsValid);
        Assert.Equal("acknowledgment.actor-mismatch", validation.ReasonCode);
    }

    [Fact]
    public void WrongAcknowledgmentCodeIsInvalid()
    {
        (AcknowledgmentChallenge challenge, AcknowledgmentResponse response) =
            CreateValidExchange();

        AcknowledgmentValidation validation = AcknowledgmentValidator.Validate(
            challenge,
            response with { AcknowledgmentCode = "account.disable.wrong-code" },
            response.OccurredUtc);

        Assert.False(validation.IsValid);
        Assert.Equal("acknowledgment.code-mismatch", validation.ReasonCode);
    }

    [Fact]
    public void WrongCorrelationIdentifierIsInvalid()
    {
        (AcknowledgmentChallenge challenge, AcknowledgmentResponse response) =
            CreateValidExchange();

        AcknowledgmentValidation validation = AcknowledgmentValidator.Validate(
            challenge,
            response with { CorrelationId = "different-correlation" },
            response.OccurredUtc);

        Assert.False(validation.IsValid);
        Assert.Equal("acknowledgment.correlation-mismatch", validation.ReasonCode);
    }

    [Fact]
    public void ResponseAtExpirationBoundaryIsValid()
    {
        (AcknowledgmentChallenge challenge, AcknowledgmentResponse response) =
            CreateValidExchange();
        AcknowledgmentResponse boundaryResponse = response with
        {
            OccurredUtc = challenge.ExpiresUtc
        };

        AcknowledgmentValidation validation = AcknowledgmentValidator.Validate(
            challenge,
            boundaryResponse,
            boundaryResponse.OccurredUtc);

        Assert.True(validation.IsValid);
        Assert.Equal("acknowledgment.accepted", validation.ReasonCode);
    }

    [Fact]
    public void NonAdministratorIsDeniedWithStableReasonCode()
    {
        DisableAccountPolicyContext original = CreateContext();
        DisableAccountPolicyContext context = original with
        {
            Actor = original.Actor with { IsAdministrator = false }
        };

        GovernanceDecision decision = DisableAccountPolicy.Evaluate(context);

        Assert.Equal(GovernanceDecisionOutcome.Denied, decision.Outcome);
        Assert.Equal(
            "account.disable.not-administrator",
            Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void CrossTenantRequestIsDeniedWithStableReasonCode()
    {
        DisableAccountPolicyContext original = CreateContext();
        DisableAccountPolicyContext context = original with
        {
            Account = original.Account with { TenantId = "tenant-b" }
        };

        GovernanceDecision decision = DisableAccountPolicy.Evaluate(context);

        Assert.Equal(GovernanceDecisionOutcome.Denied, decision.Outcome);
        Assert.Equal(
            "account.disable.cross-tenant",
            Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void ProtectedAccountRecommendsEscalationWithStableReasonCode()
    {
        DisableAccountPolicyContext original = CreateContext();
        DisableAccountPolicyContext context = original with
        {
            Account = original.Account with { IsProtected = true }
        };

        GovernanceDecision decision = DisableAccountPolicy.Evaluate(context);

        Assert.Equal(
            GovernanceDecisionOutcome.EscalationRecommended,
            decision.Outcome);
        Assert.Equal(
            "account.disable.protected-account",
            Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void SuppliedReasonAllowsRequestWithoutReasonCodes()
    {
        DisableAccountPolicyContext original = CreateContext();
        DisableAccountPolicyContext context = original with
        {
            Intent = original.Intent with { Reason = "Security investigation" }
        };

        GovernanceDecision decision = DisableAccountPolicy.Evaluate(context);

        Assert.Equal(GovernanceDecisionOutcome.Allowed, decision.Outcome);
        Assert.True(decision.CanProceed);
        Assert.Empty(decision.Reasons);
    }

    [Fact]
    public void AuditResidueKeepsLifecycleIdentityExplicit()
    {
        var residue = new AuditResidue(
            Sequence: 2,
            EventId: "test-user-100-event-02",
            OccurredUtc: _nowUtc,
            ActorId: "operator-7",
            OperationName: "account.disable",
            Outcome: "AcknowledgmentAccepted",
            ReasonCodes: ["account.disable.reason-required", "acknowledgment.accepted"],
            CorrelationId: "test-user-100",
            PolicyVersion: "3.2",
            Stage: "acknowledgment-accepted");

        Assert.Equal("test-user-100", residue.CorrelationId);
        Assert.Equal("3.2", residue.PolicyVersion);
        Assert.Equal("acknowledgment-accepted", residue.Stage);
        Assert.Equal(2, residue.ReasonCodes.Count);
    }

    [Fact]
    public void ExecutableScenariosPreserveExpectedAuditTimelines()
    {
        System.Reflection.MethodInfo entryPoint =
            Assert.IsAssignableFrom<System.Reflection.MethodInfo>(
                typeof(DisableAccountPolicy).Assembly.EntryPoint);

        entryPoint.Invoke(null, [Array.Empty<string>()]);
    }

    private static (AcknowledgmentChallenge Challenge, AcknowledgmentResponse Response)
        CreateValidExchange()
    {
        DisableAccountPolicyContext context = CreateContext();
        GovernanceDecision decision = DisableAccountPolicy.Evaluate(context);
        AcknowledgmentChallenge challenge = CreateChallenge(
            context,
            decision,
            _nowUtc);

        return (
            challenge,
            CreateAcceptedResponse(challenge, _nowUtc.AddSeconds(1)));
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
