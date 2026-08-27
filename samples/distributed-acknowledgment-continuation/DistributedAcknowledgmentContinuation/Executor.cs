namespace DistributedAcknowledgmentContinuation;

public sealed class RecordingContinuationExecutor
    : IContinuationExecutor
{
    public const string DefaultAcceptedAudience =
        "system-c:accounts-bulk-suspend";

    private readonly string _acceptedAudience;
    private readonly DateTimeOffset? _executionNowUtc;
    private readonly object _lastExecutionSync = new();
    private int _invocationCount;
    private ScopedContinuationAuthority? _lastAuthority;
    private ValidatedContinuationCommand? _lastCommand;

    public RecordingContinuationExecutor(
        string acceptedAudience = DefaultAcceptedAudience,
        DateTimeOffset? executionNowUtc = null)
    {
        _acceptedAudience = acceptedAudience;
        _executionNowUtc = executionNowUtc;
    }

    public int InvocationCount =>
        Volatile.Read(ref _invocationCount);

    public ScopedContinuationAuthority? LastAuthority
    {
        get
        {
            lock (_lastExecutionSync)
            {
                return _lastAuthority;
            }
        }
    }

    public ValidatedContinuationCommand? LastCommand
    {
        get
        {
            lock (_lastExecutionSync)
            {
                return _lastCommand;
            }
        }
    }

    public Task<ContinuationExecutionResult> ExecuteAsync(
        ScopedContinuationAuthority authority,
        ValidatedContinuationCommand command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset nowUtc =
            _executionNowUtc ?? authority.IssuedAtUtc;

        if (!string.Equals(
                authority.Audience,
                _acceptedAudience,
                StringComparison.Ordinal))
        {
            return Task.FromResult(
                ContinuationExecutionResult.Reject(
                    "authority.audience-mismatch"));
        }

        if (nowUtc >= authority.ExpiresAtUtc)
        {
            return Task.FromResult(
                ContinuationExecutionResult.Reject(
                    "authority.expired"));
        }

        if (!string.Equals(
                authority.AuthorityId,
                command.ContinuationAuthorityId,
                StringComparison.Ordinal) ||
            !string.Equals(
                authority.ContinuationId,
                command.ContinuationId,
                StringComparison.Ordinal) ||
            !string.Equals(
                authority.Operation,
                command.Operation,
                StringComparison.Ordinal) ||
            !string.Equals(
                authority.ResourceId,
                command.ResourceId,
                StringComparison.Ordinal) ||
            !string.Equals(
                authority.ResourceVersion,
                command.ExpectedResourceVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                authority.ChallengeId,
                command.ChallengeId,
                StringComparison.Ordinal) ||
            !string.Equals(
                authority.EvidenceId,
                command.EvidenceId,
                StringComparison.Ordinal) ||
            !string.Equals(
                authority.CurrentPolicyId,
                command.CurrentPolicyId,
                StringComparison.Ordinal) ||
            !string.Equals(
                authority.CurrentPolicyVersion,
                command.CurrentPolicyVersion,
                StringComparison.Ordinal))
        {
            return Task.FromResult(
                ContinuationExecutionResult.Reject(
                    "authority.binding-mismatch"));
        }

        Interlocked.Increment(ref _invocationCount);

        lock (_lastExecutionSync)
        {
            _lastAuthority = authority;
            _lastCommand = command;
        }

        Console.WriteLine(
            $"SIMULATED HOST EXECUTION: would run {command.Operation} " +
            $"for {command.ResourceId} at {command.ExpectedResourceVersion}.");

        return Task.FromResult(
            ContinuationExecutionResult.Success());
    }
}
