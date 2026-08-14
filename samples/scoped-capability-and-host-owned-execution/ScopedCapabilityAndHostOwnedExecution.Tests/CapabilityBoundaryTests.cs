using Xunit;

namespace ScopedCapabilityAndHostOwnedExecution.Tests;

public sealed class CapabilityBoundaryTests
{
    private static readonly DateTimeOffset IssuedUtc =
        new(2026, 8, 14, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidCapabilityReachesExecutorExactlyOnce()
    {
        var executor = new RecordingDisableAccountExecutor();
        var gateway = CreateGateway(executor);
        ExecutionCapability capability = CreateCapability();

        CapabilityExecutionResult result = await gateway.ExecuteAsync(
            capability,
            CreateRequest(nowUtc: IssuedUtc.AddMinutes(1)),
            CancellationToken.None);

        Assert.True(result.Executed);
        Assert.True(result.Validation.IsValid);
        Assert.Equal("capability.valid", result.Validation.ReasonCode);
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task ExpiredCapabilityDoesNotReachExecutor()
    {
        var executor = new RecordingDisableAccountExecutor();
        var gateway = CreateGateway(executor);
        ExecutionCapability capability = CreateCapability();

        CapabilityExecutionResult result = await gateway.ExecuteAsync(
            capability,
            CreateRequest(nowUtc: capability.ExpiresUtc),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal("capability.expired", result.Validation.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task ResourceChangedAfterApprovalDoesNotReachExecutor()
    {
        var executor = new RecordingDisableAccountExecutor();
        var gateway = CreateGateway(executor);
        ExecutionCapability capability = CreateCapability(resourceVersion: 7);

        CapabilityExecutionResult result = await gateway.ExecuteAsync(
            capability,
            CreateRequest(
                resourceVersion: 8,
                nowUtc: IssuedUtc.AddMinutes(1)),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(
            "capability.resource-version-mismatch",
            result.Validation.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task WrongResourceDoesNotReachExecutor()
    {
        var executor = new RecordingDisableAccountExecutor();
        var gateway = CreateGateway(executor);

        CapabilityExecutionResult result = await gateway.ExecuteAsync(
            CreateCapability(),
            CreateRequest(
                resourceId: "user-999",
                nowUtc: IssuedUtc.AddMinutes(1)),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(
            "capability.resource-mismatch",
            result.Validation.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task WrongActorDoesNotReachExecutor()
    {
        var executor = new RecordingDisableAccountExecutor();
        var gateway = CreateGateway(executor);

        CapabilityExecutionResult result = await gateway.ExecuteAsync(
            CreateCapability(),
            CreateRequest(
                subjectId: "operator-99",
                nowUtc: IssuedUtc.AddMinutes(1)),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(
            "capability.subject-mismatch",
            result.Validation.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task WrongOperationDoesNotReachExecutor()
    {
        var executor = new RecordingDisableAccountExecutor();
        var gateway = CreateGateway(executor);

        CapabilityExecutionResult result = await gateway.ExecuteAsync(
            CreateCapability(),
            CreateRequest(
                operationName: "account.delete",
                nowUtc: IssuedUtc.AddMinutes(1)),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(
            "capability.operation-mismatch",
            result.Validation.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task WrongAudienceDoesNotReachExecutor()
    {
        var executor = new RecordingDisableAccountExecutor();
        var gateway = CreateGateway(executor);

        CapabilityExecutionResult result = await gateway.ExecuteAsync(
            CreateCapability(),
            CreateRequest(
                audience: "billing-gateway",
                nowUtc: IssuedUtc.AddMinutes(1)),
            CancellationToken.None);

        Assert.False(result.Executed);
        Assert.Equal(
            "capability.audience-mismatch",
            result.Validation.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public void BlockedDecisionCannotMintExecutionCapability()
    {
        var factory = new ExecutionCapabilityFactory();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => factory.Create(
                CreateContext(),
                GovernanceDecision.Deny(
                    "account.disable.denied",
                    "The operation is not allowed."),
                IssuedUtc,
                acknowledgmentId: null));

        Assert.Contains(
            "blocked decision",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static DisableAccountGateway CreateGateway(
        RecordingDisableAccountExecutor executor)
    {
        return new DisableAccountGateway(
            new ExecutionCapabilityValidator(),
            executor);
    }

    private static ExecutionCapability CreateCapability(
        int resourceVersion = 7)
    {
        return new ExecutionCapabilityFactory().Create(
            CreateContext(resourceVersion),
            GovernanceDecision.Allow(),
            IssuedUtc,
            acknowledgmentId: "ack-77");
    }

    private static DisableAccountPolicyContext CreateContext(
        int resourceVersion = 7)
    {
        return new DisableAccountPolicyContext(
            ActorId: "operator-7",
            OperationName: "account.disable",
            ResourceId: "user-100",
            ResourceVersion: resourceVersion,
            Audience: "account-admin-gateway",
            RequiredScope: "account.disable",
            PolicyVersion: "4.0",
            CorrelationId: "test-user-100");
    }

    private static CapabilityValidationRequest CreateRequest(
        DateTimeOffset nowUtc,
        int resourceVersion = 7,
        string subjectId = "operator-7",
        string operationName = "account.disable",
        string audience = "account-admin-gateway",
        string resourceId = "user-100")
    {
        return new CapabilityValidationRequest(
            Audience: audience,
            SubjectId: subjectId,
            OperationName: operationName,
            ResourceId: resourceId,
            ResourceVersion: resourceVersion,
            RequiredScope: "account.disable",
            NowUtc: nowUtc,
            PolicyVersion: "4.0",
            AcknowledgmentId: "ack-77",
            IntendedUse: "disable-one-account");
    }
}
