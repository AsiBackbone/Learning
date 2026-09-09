namespace AdaptiveRiskContext;

public interface IPaymentExecutor
{
    Task<PaymentExecutionAttempt> ExecuteAsync(
        ExecutionAuthority authority,
        ValidatedPaymentCommand command,
        CancellationToken cancellationToken);
}

public sealed class RecordingPaymentExecutor : IPaymentExecutor
{
    private readonly string _acceptedAudience;
    private readonly string _acceptedOperation;
    private readonly DateTimeOffset? _executionNowUtc;
    private int _invocationCount;

    public RecordingPaymentExecutor(
        string acceptedAudience = PaymentExecutionContract.Audience,
        string acceptedOperation = PaymentExecutionContract.Operation,
        DateTimeOffset? executionNowUtc = null)
    {
        _acceptedAudience = acceptedAudience;
        _acceptedOperation = acceptedOperation;
        _executionNowUtc = executionNowUtc;
    }

    public int InvocationCount =>
        Volatile.Read(ref _invocationCount);

    public Task<PaymentExecutionAttempt> ExecuteAsync(
        ExecutionAuthority authority,
        ValidatedPaymentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset nowUtc =
            _executionNowUtc ?? command.ValidatedAtUtc;

        if (authority.IssuedAtUtc > nowUtc)
        {
            return Task.FromResult(
                PaymentExecutionAttempt.Reject(
                    "authority.not-yet-valid"));
        }

        if (authority.ExpiresAtUtc <= nowUtc)
        {
            return Task.FromResult(
                PaymentExecutionAttempt.Reject(
                    "authority.expired"));
        }

        if (!string.Equals(
                authority.Audience,
                _acceptedAudience,
                StringComparison.Ordinal))
        {
            return Task.FromResult(
                PaymentExecutionAttempt.Reject(
                    "authority.audience-mismatch"));
        }

        if (!string.Equals(
                authority.Operation,
                _acceptedOperation,
                StringComparison.Ordinal))
        {
            return Task.FromResult(
                PaymentExecutionAttempt.Reject(
                    "authority.operation-mismatch"));
        }

        if (!string.Equals(authority.PaymentId, command.PaymentId, StringComparison.Ordinal) ||
            !string.Equals(authority.ResourceVersion, command.ExpectedResourceVersion, StringComparison.Ordinal) ||
            authority.Amount != command.Amount ||
            authority.DestinationApproved != command.DestinationApproved ||
            authority.IncidentPosture != command.IncidentPosture ||
            !string.Equals(authority.EnvironmentVersion, command.EnvironmentVersion, StringComparison.Ordinal) ||
            !string.Equals(authority.DecisionId, command.DecisionId, StringComparison.Ordinal) ||
            !string.Equals(authority.AuthorityId, command.AuthorityId, StringComparison.Ordinal) ||
            !string.Equals(authority.Audience, command.Audience, StringComparison.Ordinal) ||
            !string.Equals(authority.Operation, command.Operation, StringComparison.Ordinal) ||
            !string.Equals(authority.RiskObservationId, command.RiskObservationId, StringComparison.Ordinal) ||
            !string.Equals(authority.PolicyId, command.PolicyId, StringComparison.Ordinal) ||
            !string.Equals(authority.PolicyVersion, command.PolicyVersion, StringComparison.Ordinal) ||
            !string.Equals(authority.ThresholdVersion, command.ThresholdVersion, StringComparison.Ordinal) ||
            !string.Equals(authority.FreshnessRuleVersion, command.FreshnessRuleVersion, StringComparison.Ordinal))
        {
            return Task.FromResult(
                PaymentExecutionAttempt.Reject(
                    "authority.binding-mismatch"));
        }

        Interlocked.Increment(ref _invocationCount);
        return Task.FromResult(PaymentExecutionAttempt.Success());
    }
}

public sealed class RiskExecutionGateway
{
    private readonly ExecutionFreshnessEvaluator _freshnessEvaluator;
    private readonly IExecutionAuthorityClaimStore _claimStore;
    private readonly IPaymentExecutor _executor;

    public RiskExecutionGateway(
        ExecutionFreshnessEvaluator freshnessEvaluator,
        IExecutionAuthorityClaimStore claimStore,
        IPaymentExecutor executor)
    {
        _freshnessEvaluator = freshnessEvaluator;
        _claimStore = claimStore;
        _executor = executor;
    }

    public async Task<ExecutionResult> TryExecuteAsync(
        ExecutionAuthority authority,
        PaymentContext currentContext,
        RiskSignalInput currentRisk,
        RiskGovernancePolicy currentPolicy,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        FreshnessAssessment freshness = _freshnessEvaluator.Evaluate(
            authority,
            currentContext,
            currentRisk,
            currentPolicy,
            nowUtc);

        if (freshness.Action != FreshnessAction.Proceed)
        {
            return new ExecutionResult(
                Executed: false,
                Action: freshness.Action,
                ReasonCode: freshness.ReasonCode);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!_claimStore.TryClaim(authority.AuthorityId))
        {
            return new ExecutionResult(
                Executed: false,
                Action: FreshnessAction.Reject,
                ReasonCode: "authority.already-claimed");
        }

        ValidatedPaymentCommand command = new(
            PaymentId: currentContext.PaymentId,
            ExpectedResourceVersion: currentContext.ResourceVersion,
            Amount: currentContext.Amount,
            DestinationApproved: currentContext.DestinationApproved,
            IncidentPosture: currentContext.IncidentPosture,
            EnvironmentVersion: currentContext.EnvironmentVersion,
            ValidatedAtUtc: nowUtc,
            DecisionId: authority.DecisionId,
            AuthorityId: authority.AuthorityId,
            Audience: authority.Audience,
            Operation: authority.Operation,
            RiskObservationId: authority.RiskObservationId,
            PolicyId: authority.PolicyId,
            PolicyVersion: authority.PolicyVersion,
            ThresholdVersion: authority.ThresholdVersion,
            FreshnessRuleVersion: authority.FreshnessRuleVersion);

        PaymentExecutionAttempt attempt = await _executor.ExecuteAsync(
            authority,
            command,
            cancellationToken);

        if (!attempt.Executed)
        {
            return new ExecutionResult(
                Executed: false,
                Action: FreshnessAction.Reject,
                ReasonCode: attempt.ReasonCode);
        }

        return new ExecutionResult(
            Executed: true,
            Action: FreshnessAction.Proceed,
            ReasonCode: attempt.ReasonCode);
    }
}
