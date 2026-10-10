using System.Collections.Concurrent;

namespace AuthorizationToGovernanceComparison;

public enum Variant
{
    EndpointAuthorization = 1,
    ResourceAuthorization = 2,
    GovernedExecution = 3
}

/// <summary>
/// The protected side effect, shared by every variant. It records each invocation
/// instead of disabling a real account.
/// </summary>
public interface IAccountDisabler
{
    void Disable(Variant variant, string accountId);
}

public sealed record DisableInvocation(Variant Variant, string AccountId);

public sealed class RecordingAccountDisabler : IAccountDisabler
{
    private readonly ConcurrentQueue<DisableInvocation> _invocations = new();

    public IReadOnlyList<DisableInvocation> Invocations => [.. _invocations];

    public void Disable(Variant variant, string accountId)
    {
        _invocations.Enqueue(new(variant, accountId));
    }
}

/// <summary>
/// Variant 1 evidence: ordinary operational log lines, as a logging provider sees them.
/// </summary>
public sealed record OperationalLogEntry(string Category, LogLevel Level, string Message);

public sealed class OperationalLogSink : ILoggerProvider
{
    private readonly ConcurrentQueue<OperationalLogEntry> _entries = new();

    public IReadOnlyList<OperationalLogEntry> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName)
    {
        return new SinkLogger(categoryName, _entries);
    }

    public void Dispose()
    {
    }

    private sealed class SinkLogger(string category, ConcurrentQueue<OperationalLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Information &&
                   (category.StartsWith(nameof(AuthorizationToGovernanceComparison), StringComparison.Ordinal) ||
                    category.StartsWith("Microsoft.AspNetCore.Authorization", StringComparison.Ordinal));
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                entries.Enqueue(new(category, logLevel, formatter(state, exception)));
            }
        }
    }
}

/// <summary>
/// Variant 2 evidence: a structured application audit entry for each attempt.
/// </summary>
public sealed record AuditEntry(
    string ActorId,
    string AccountId,
    string Action,
    bool Succeeded,
    IReadOnlyList<string> FailureReasons,
    DateTimeOffset AtUtc);

public sealed class ApplicationAuditLog
{
    private readonly ConcurrentQueue<AuditEntry> _entries = new();

    public IReadOnlyList<AuditEntry> Entries => [.. _entries];

    public void Append(AuditEntry entry)
    {
        _entries.Enqueue(entry);
    }
}

/// <summary>
/// Variant 3 decision evidence: what policy concluded, why, under which version, and
/// whether it was later executed. It exists for every outcome, not only for success.
/// </summary>
public sealed record DecisionRecord(
    string DecisionId,
    string ActorId,
    string AccountId,
    string Operation,
    GovernanceOutcome Outcome,
    string ReasonCode,
    string PolicyVersion,
    DateTimeOffset DecidedAtUtc,
    string? AcknowledgedDecisionId);

public sealed class DecisionLog
{
    private readonly ConcurrentDictionary<string, DecisionRecord> _decisions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, bool> _consumedAcknowledgments = new(StringComparer.Ordinal);
    private int _sequence;

    public IReadOnlyList<DecisionRecord> Entries =>
        [.. _decisions.Values.OrderBy(record => record.DecisionId, StringComparer.Ordinal)];

    public string NextDecisionId()
    {
        return $"decision-{Interlocked.Increment(ref _sequence):D4}";
    }

    public void Record(DecisionRecord decision)
    {
        _decisions[decision.DecisionId] = decision;
    }

    public DecisionRecord? Find(string decisionId)
    {
        return _decisions.TryGetValue(decisionId, out DecisionRecord? record) ? record : null;
    }

    /// <summary>
    /// An acknowledgment satisfies one later decision only.
    /// </summary>
    public bool TryConsumeAcknowledgment(string decisionId)
    {
        return _consumedAcknowledgments.TryAdd(decisionId, true);
    }
}

/// <summary>
/// Variant 3 execution evidence: what the host actually did, linked to the decision.
/// </summary>
public sealed record ExecutionRecord(
    string ExecutionId,
    string DecisionId,
    string AccountId,
    DateTimeOffset ExecutedAtUtc);

public sealed class ExecutionLog
{
    private readonly ConcurrentDictionary<string, ExecutionRecord> _byDecision = new(StringComparer.Ordinal);

    public IReadOnlyList<ExecutionRecord> Entries =>
        [.. _byDecision.Values.OrderBy(record => record.ExecutionId, StringComparer.Ordinal)];

    public bool TryRecord(ExecutionRecord record)
    {
        return _byDecision.TryAdd(record.DecisionId, record);
    }
}
