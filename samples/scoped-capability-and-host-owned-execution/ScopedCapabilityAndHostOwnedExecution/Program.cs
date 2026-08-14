DateTimeOffset issuedUtc =
    new(2026, 8, 14, 14, 0, 0, TimeSpan.Zero);

var context = CreateContext(resourceVersion: 7);
var decision = GovernanceDecision.Allow();
var factory = new ExecutionCapabilityFactory();

ExecutionCapability capability = factory.Create(
    context,
    decision,
    issuedUtc,
    acknowledgmentId: "ack-77");

var validator = new ExecutionCapabilityValidator();
var executor = new RecordingDisableAccountExecutor();
var gateway = new DisableAccountGateway(validator, executor);

CapabilityScenario[] scenarios =
[
    new(
        Name: "Valid capability",
        Request: CreateRequest(
            resourceVersion: 7,
            nowUtc: issuedUtc.AddMinutes(1)),
        ExpectedExecuted: true,
        ExpectedReasonCode: "capability.valid"),
    new(
        Name: "Expired capability",
        Request: CreateRequest(
            resourceVersion: 7,
            nowUtc: capability.ExpiresUtc),
        ExpectedExecuted: false,
        ExpectedReasonCode: "capability.expired"),
    new(
        Name: "Resource changed after approval",
        Request: CreateRequest(
            resourceVersion: 8,
            nowUtc: issuedUtc.AddMinutes(1)),
        ExpectedExecuted: false,
        ExpectedReasonCode: "capability.resource-version-mismatch"),
    new(
        Name: "Wrong resource",
        Request: CreateRequest(
            resourceVersion: 7,
            nowUtc: issuedUtc.AddMinutes(1),
            resourceId: "user-999"),
        ExpectedExecuted: false,
        ExpectedReasonCode: "capability.resource-mismatch"),
    new(
        Name: "Wrong actor",
        Request: CreateRequest(
            resourceVersion: 7,
            nowUtc: issuedUtc.AddMinutes(1),
            subjectId: "operator-99"),
        ExpectedExecuted: false,
        ExpectedReasonCode: "capability.subject-mismatch"),
    new(
        Name: "Wrong operation",
        Request: CreateRequest(
            resourceVersion: 7,
            nowUtc: issuedUtc.AddMinutes(1),
            operationName: "account.delete"),
        ExpectedExecuted: false,
        ExpectedReasonCode: "capability.operation-mismatch"),
    new(
        Name: "Wrong audience",
        Request: CreateRequest(
            resourceVersion: 7,
            nowUtc: issuedUtc.AddMinutes(1),
            audience: "billing-gateway"),
        ExpectedExecuted: false,
        ExpectedReasonCode: "capability.audience-mismatch")
];

Console.WriteLine("Scoped Capability and Host-Owned Execution");
Console.WriteLine(new string('=', 42));
Console.WriteLine();

foreach (CapabilityScenario scenario in scenarios)
{
    executor.Reset();

    CapabilityExecutionResult result = await gateway.ExecuteAsync(
        capability,
        scenario.Request,
        CancellationToken.None);

    VerifyScenario(scenario, result, executor.InvocationCount);

    Console.WriteLine($"Scenario: {scenario.Name}");
    Console.WriteLine($"Validation: {result.Validation.ReasonCode}");
    Console.WriteLine($"Executed: {result.Executed}");
    Console.WriteLine($"Executor invocations: {executor.InvocationCount}");
    Console.WriteLine();
}

bool blockedDecisionCouldMintCapability = true;

try
{
    _ = factory.Create(
        context,
        GovernanceDecision.Deny(
            "account.disable.denied",
            "The policy denied the requested operation."),
        issuedUtc,
        acknowledgmentId: null);
}
catch (InvalidOperationException)
{
    blockedDecisionCouldMintCapability = false;
}

if (blockedDecisionCouldMintCapability)
{
    throw new InvalidOperationException(
        "A blocked decision unexpectedly produced execution authority.");
}

Console.WriteLine("Architectural invariants preserved:");
Console.WriteLine("- A blocked decision cannot mint execution authority.");
Console.WriteLine("- Expired authority never reaches the executor.");
Console.WriteLine("- Relevant resource-state drift invalidates the capability.");
Console.WriteLine("- Actor, operation, audience, and resource bindings are checked at execution.");
Console.WriteLine("- The host-owned gateway, not the capability, invokes the executor.");

static DisableAccountPolicyContext CreateContext(int resourceVersion)
{
    return new DisableAccountPolicyContext(
        ActorId: "operator-7",
        OperationName: "account.disable",
        ResourceId: "user-100",
        ResourceVersion: resourceVersion,
        Audience: "account-admin-gateway",
        RequiredScope: "account.disable",
        PolicyVersion: "4.0",
        CorrelationId: "sample-user-100");
}

static CapabilityValidationRequest CreateRequest(
    int resourceVersion,
    DateTimeOffset nowUtc,
    string subjectId = "operator-7",
    string operationName = "account.disable",
    string audience = "account-admin-gateway",
    string resourceId = "user-100",
    string requiredScope = "account.disable",
    string policyVersion = "4.0",
    string? acknowledgmentId = "ack-77")
{
    return new CapabilityValidationRequest(
        Audience: audience,
        SubjectId: subjectId,
        OperationName: operationName,
        ResourceId: resourceId,
        ResourceVersion: resourceVersion,
        RequiredScope: requiredScope,
        NowUtc: nowUtc,
        PolicyVersion: policyVersion,
        AcknowledgmentId: acknowledgmentId,
        IntendedUse: "disable-one-account");
}

static void VerifyScenario(
    CapabilityScenario scenario,
    CapabilityExecutionResult result,
    int executorInvocations)
{
    if (result.Executed != scenario.ExpectedExecuted)
    {
        throw new InvalidOperationException(
            $"Scenario '{scenario.Name}' expected Executed={scenario.ExpectedExecuted} " +
            $"but received Executed={result.Executed}.");
    }

    if (!string.Equals(
            result.Validation.ReasonCode,
            scenario.ExpectedReasonCode,
            StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Scenario '{scenario.Name}' expected reason code " +
            $"'{scenario.ExpectedReasonCode}' but received " +
            $"'{result.Validation.ReasonCode}'.");
    }

    int expectedInvocations = scenario.ExpectedExecuted ? 1 : 0;

    if (executorInvocations != expectedInvocations)
    {
        throw new InvalidOperationException(
            $"Scenario '{scenario.Name}' expected {expectedInvocations} executor " +
            $"invocation(s) but observed {executorInvocations}.");
    }
}

public sealed record CapabilityScenario(
    string Name,
    CapabilityValidationRequest Request,
    bool ExpectedExecuted,
    string ExpectedReasonCode);

public enum GovernanceDecisionOutcome
{
    Allowed,
    Denied
}

public sealed record GovernanceDecision(
    GovernanceDecisionOutcome Outcome,
    string? ReasonCode,
    string? Reason)
{
    public bool CanProceed =>
        Outcome == GovernanceDecisionOutcome.Allowed;

    public static GovernanceDecision Allow() =>
        new(GovernanceDecisionOutcome.Allowed, null, null);

    public static GovernanceDecision Deny(
        string reasonCode,
        string reason) =>
        new(GovernanceDecisionOutcome.Denied, reasonCode, reason);
}

public sealed record DisableAccountPolicyContext(
    string ActorId,
    string OperationName,
    string ResourceId,
    int ResourceVersion,
    string Audience,
    string RequiredScope,
    string PolicyVersion,
    string CorrelationId);

public sealed record ExecutionCapability(
    string CapabilityId,
    string Issuer,
    string Audience,
    string SubjectId,
    string OperationName,
    string ResourceId,
    int ResourceVersion,
    IReadOnlySet<string> Scopes,
    DateTimeOffset IssuedUtc,
    DateTimeOffset ExpiresUtc,
    string PolicyVersion,
    string? AcknowledgmentId,
    string IntendedUse);

public sealed class ExecutionCapabilityFactory
{
    public ExecutionCapability Create(
        DisableAccountPolicyContext context,
        GovernanceDecision decision,
        DateTimeOffset nowUtc,
        string? acknowledgmentId)
    {
        if (!decision.CanProceed)
        {
            throw new InvalidOperationException(
                "A blocked decision cannot produce an execution capability.");
        }

        return new ExecutionCapability(
            CapabilityId: $"{context.CorrelationId}-capability",
            Issuer: "policy-engine",
            Audience: context.Audience,
            SubjectId: context.ActorId,
            OperationName: context.OperationName,
            ResourceId: context.ResourceId,
            ResourceVersion: context.ResourceVersion,
            Scopes: new HashSet<string>(
                [context.RequiredScope],
                StringComparer.Ordinal),
            IssuedUtc: nowUtc,
            ExpiresUtc: nowUtc.AddMinutes(5),
            PolicyVersion: context.PolicyVersion,
            AcknowledgmentId: acknowledgmentId,
            IntendedUse: "disable-one-account");
    }
}

public sealed record CapabilityValidationRequest(
    string Audience,
    string SubjectId,
    string OperationName,
    string ResourceId,
    int ResourceVersion,
    string RequiredScope,
    DateTimeOffset NowUtc,
    string PolicyVersion,
    string? AcknowledgmentId,
    string IntendedUse);

public sealed record CapabilityValidationResult(
    bool IsValid,
    string ReasonCode)
{
    public static CapabilityValidationResult Valid() =>
        new(true, "capability.valid");

    public static CapabilityValidationResult Invalid(string reasonCode) =>
        new(false, reasonCode);
}

public sealed class ExecutionCapabilityValidator
{
    public CapabilityValidationResult Validate(
        ExecutionCapability capability,
        CapabilityValidationRequest request)
    {
        if (!string.Equals(
                capability.Audience,
                request.Audience,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Invalid(
                "capability.audience-mismatch");
        }

        if (!string.Equals(
                capability.SubjectId,
                request.SubjectId,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Invalid(
                "capability.subject-mismatch");
        }

        if (!string.Equals(
                capability.OperationName,
                request.OperationName,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Invalid(
                "capability.operation-mismatch");
        }

        if (!string.Equals(
                capability.ResourceId,
                request.ResourceId,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Invalid(
                "capability.resource-mismatch");
        }

        if (capability.ResourceVersion != request.ResourceVersion)
        {
            return CapabilityValidationResult.Invalid(
                "capability.resource-version-mismatch");
        }

        if (!capability.Scopes.Contains(request.RequiredScope))
        {
            return CapabilityValidationResult.Invalid(
                "capability.scope-missing");
        }

        if (!string.Equals(
                capability.PolicyVersion,
                request.PolicyVersion,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Invalid(
                "capability.policy-mismatch");
        }

        if (request.NowUtc < capability.IssuedUtc)
        {
            return CapabilityValidationResult.Invalid(
                "capability.not-yet-valid");
        }

        if (request.NowUtc >= capability.ExpiresUtc)
        {
            return CapabilityValidationResult.Invalid(
                "capability.expired");
        }

        if (!string.Equals(
                capability.AcknowledgmentId,
                request.AcknowledgmentId,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Invalid(
                "capability.acknowledgment-mismatch");
        }

        if (!string.Equals(
                capability.IntendedUse,
                request.IntendedUse,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Invalid(
                "capability.intended-use-mismatch");
        }

        return CapabilityValidationResult.Valid();
    }
}

public sealed record CapabilityExecutionResult(
    bool Executed,
    CapabilityValidationResult Validation);

public interface IDisableAccountExecutor
{
    Task ExecuteAsync(
        string accountId,
        CancellationToken cancellationToken);
}

public sealed class RecordingDisableAccountExecutor
    : IDisableAccountExecutor
{
    public int InvocationCount { get; private set; }

    public Task ExecuteAsync(
        string accountId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InvocationCount++;

        Console.WriteLine(
            $"SIMULATED HOST EXECUTION: would disable {accountId}.");

        return Task.CompletedTask;
    }

    public void Reset()
    {
        InvocationCount = 0;
    }
}

public sealed class DisableAccountGateway(
    ExecutionCapabilityValidator validator,
    IDisableAccountExecutor executor)
{
    public async Task<CapabilityExecutionResult> ExecuteAsync(
        ExecutionCapability capability,
        CapabilityValidationRequest request,
        CancellationToken cancellationToken)
    {
        CapabilityValidationResult validation =
            validator.Validate(capability, request);

        if (!validation.IsValid)
        {
            return new CapabilityExecutionResult(
                Executed: false,
                Validation: validation);
        }

        await executor.ExecuteAsync(
            request.ResourceId,
            cancellationToken);

        return new CapabilityExecutionResult(
            Executed: true,
            Validation: validation);
    }
}
