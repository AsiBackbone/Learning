using System.Collections.Concurrent;

public sealed record ExecutionCapability(
    string CapabilityId,
    string SubjectId,
    string OperationName,
    string ResourceId,
    string Audience,
    DateTimeOffset IssuedUtc,
    DateTimeOffset ExpiresUtc,
    int MaximumUses);

public sealed record CapabilityValidationRequest(
    string SubjectId,
    string OperationName,
    string ResourceId,
    string Audience,
    DateTimeOffset NowUtc);

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
        if (capability.MaximumUses < 1)
        {
            return CapabilityValidationResult.Invalid(
                "capability.invalid-maximum-uses");
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

        if (!string.Equals(
                capability.Audience,
                request.Audience,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Invalid(
                "capability.audience-mismatch");
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

        return CapabilityValidationResult.Valid();
    }
}

public sealed record CapabilityUseResult(
    bool Accepted,
    int ObservedUseCount,
    int MaximumUses,
    string ReasonCode)
{
    public static CapabilityUseResult Consumed(
        int observedUseCount,
        int maximumUses) =>
        new(
            true,
            observedUseCount,
            maximumUses,
            "capability.use-consumed");

    public static CapabilityUseResult UseLimitExceeded(
        int observedUseCount,
        int maximumUses) =>
        new(
            false,
            observedUseCount,
            maximumUses,
            "capability.use-limit-exceeded");

    public static CapabilityUseResult MaximumUsesMismatch(
        int observedUseCount,
        int maximumUses) =>
        new(
            false,
            observedUseCount,
            maximumUses,
            "capability.maximum-uses-mismatch");
}

public interface ICapabilityUseStore
{
    ValueTask<CapabilityUseResult> TryConsumeAsync(
        string capabilityId,
        int maximumUses,
        DateTimeOffset usedUtc,
        CancellationToken cancellationToken);
}

public sealed class AtomicInMemoryCapabilityUseStore : ICapabilityUseStore
{
    private readonly ConcurrentDictionary<string, UseState> _states =
        new(StringComparer.Ordinal);

    public async ValueTask<CapabilityUseResult> TryConsumeAsync(
        string capabilityId,
        int maximumUses,
        DateTimeOffset usedUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId);

        if (maximumUses < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumUses),
                maximumUses,
                "Maximum uses must be at least one.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        UseState state = _states.GetOrAdd(
            capabilityId,
            static (_, configuredMaximumUses) =>
                new UseState(configuredMaximumUses),
            maximumUses);

        await state.Gate.WaitAsync(cancellationToken);

        try
        {
            if (state.MaximumUses != maximumUses)
            {
                return CapabilityUseResult.MaximumUsesMismatch(
                    state.UseCount,
                    state.MaximumUses);
            }

            if (state.UseCount >= state.MaximumUses)
            {
                return CapabilityUseResult.UseLimitExceeded(
                    state.UseCount,
                    state.MaximumUses);
            }

            state.UseCount++;

            return CapabilityUseResult.Consumed(
                state.UseCount,
                state.MaximumUses);
        }
        finally
        {
            state.Gate.Release();
        }
    }

    public int GetObservedUseCount(string capabilityId)
    {
        return _states.TryGetValue(capabilityId, out UseState? state)
            ? state.UseCount
            : 0;
    }

    private sealed class UseState(int maximumUses)
    {
        public int MaximumUses { get; } = maximumUses;

        public SemaphoreSlim Gate { get; } = new(1, 1);

        public int UseCount { get; set; }
    }
}

public sealed class DeliberatelyUnsafeCheckThenActCapabilityUseStore(
    int expectedConcurrentConsumers = 2)
    : ICapabilityUseStore
{
    private readonly ConcurrentDictionary<string, int> _useCounts =
        new(StringComparer.Ordinal);

    private readonly ConcurrentArrivalGate _raceGate =
        new(expectedConcurrentConsumers);

    public async ValueTask<CapabilityUseResult> TryConsumeAsync(
        string capabilityId,
        int maximumUses,
        DateTimeOffset usedUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId);

        if (maximumUses < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumUses),
                maximumUses,
                "Maximum uses must be at least one.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        int observedUseCount = _useCounts.TryGetValue(
            capabilityId,
            out int currentUseCount)
                ? currentUseCount
                : 0;

        if (observedUseCount >= maximumUses)
        {
            return CapabilityUseResult.UseLimitExceeded(
                observedUseCount,
                maximumUses);
        }

        // This gate deliberately makes two callers observe the same state before
        // either caller records consumption. It exists only to reproduce the
        // check-then-act race deterministically for the teaching sample.
        await _raceGate.WaitAsync(cancellationToken);

        _useCounts[capabilityId] = observedUseCount + 1;

        return CapabilityUseResult.Consumed(
            observedUseCount + 1,
            maximumUses);
    }

    public int GetObservedUseCount(string capabilityId)
    {
        return _useCounts.TryGetValue(capabilityId, out int useCount)
            ? useCount
            : 0;
    }

    private sealed class ConcurrentArrivalGate(int expectedConsumers)
    {
        private readonly int _expectedConsumers = expectedConsumers > 1
            ? expectedConsumers
            : throw new ArgumentOutOfRangeException(
                nameof(expectedConsumers),
                expectedConsumers,
                "At least two consumers are required to demonstrate the race.");

        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _arrivals;

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            int arrivals = Interlocked.Increment(ref _arrivals);

            if (arrivals >= _expectedConsumers)
            {
                _release.TrySetResult(true);
            }

            await _release.Task.WaitAsync(cancellationToken);
        }
    }
}

public sealed class CapabilityUseStoreUnavailableException : Exception
{
    public CapabilityUseStoreUnavailableException(string message)
        : base(message)
    {
    }
}

public sealed record ReplayEvidence(
    string Stage,
    string CapabilityId,
    string Outcome,
    string ReasonCode,
    int? ObservedUseCount,
    int MaximumUses,
    bool ExecutionAttempted,
    DateTimeOffset TimestampUtc);

public interface IReplayEvidenceSink
{
    void Record(ReplayEvidence evidence);
}

public sealed class InMemoryReplayEvidenceSink : IReplayEvidenceSink
{
    private readonly ConcurrentQueue<ReplayEvidence> _events = new();

    public void Record(ReplayEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        _events.Enqueue(evidence);
    }

    public IReadOnlyList<ReplayEvidence> Snapshot() =>
        _events.ToArray();
}

public interface IProtectedOperationExecutor
{
    Task ExecuteAsync(
        string resourceId,
        CancellationToken cancellationToken);
}

public sealed class RecordingProtectedOperationExecutor(
    bool writeToConsole = false)
    : IProtectedOperationExecutor
{
    private int _invocationCount;

    public int InvocationCount =>
        Volatile.Read(ref _invocationCount);

    public Task ExecuteAsync(
        string resourceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Interlocked.Increment(ref _invocationCount);

        if (writeToConsole)
        {
            Console.WriteLine(
                $"SIMULATED HOST EXECUTION: would mutate {resourceId}.");
        }

        return Task.CompletedTask;
    }
}

public sealed record CapabilityExecutionResult(
    bool ExecutionCompleted,
    string ReasonCode,
    CapabilityValidationResult Validation,
    CapabilityUseResult? Consumption);

public sealed class ProtectedOperationGateway(
    ExecutionCapabilityValidator validator,
    ICapabilityUseStore useStore,
    IProtectedOperationExecutor executor,
    IReplayEvidenceSink evidenceSink)
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
            evidenceSink.Record(
                CreateEvidence(
                    stage: "static-validation",
                    capability: capability,
                    outcome: "rejected",
                    reasonCode: validation.ReasonCode,
                    observedUseCount: null,
                    executionAttempted: false,
                    timestampUtc: request.NowUtc));

            return new CapabilityExecutionResult(
                ExecutionCompleted: false,
                ReasonCode: validation.ReasonCode,
                Validation: validation,
                Consumption: null);
        }

        CapabilityUseResult consumption;

        try
        {
            consumption = await useStore.TryConsumeAsync(
                capability.CapabilityId,
                capability.MaximumUses,
                request.NowUtc,
                cancellationToken);
        }
        catch (CapabilityUseStoreUnavailableException)
        {
            const string reasonCode = "capability.use-store-unavailable";

            evidenceSink.Record(
                CreateEvidence(
                    stage: "capability-consumption",
                    capability: capability,
                    outcome: "unavailable",
                    reasonCode: reasonCode,
                    observedUseCount: null,
                    executionAttempted: false,
                    timestampUtc: request.NowUtc));

            return new CapabilityExecutionResult(
                ExecutionCompleted: false,
                ReasonCode: reasonCode,
                Validation: validation,
                Consumption: null);
        }
        catch (OperationCanceledException)
        {
            evidenceSink.Record(
                CreateEvidence(
                    stage: "capability-consumption",
                    capability: capability,
                    outcome: "cancelled",
                    reasonCode: "capability.consumption-cancelled",
                    observedUseCount: null,
                    executionAttempted: false,
                    timestampUtc: request.NowUtc));

            throw;
        }

        if (!consumption.Accepted)
        {
            evidenceSink.Record(
                CreateEvidence(
                    stage: "capability-consumption",
                    capability: capability,
                    outcome: "rejected",
                    reasonCode: consumption.ReasonCode,
                    observedUseCount: consumption.ObservedUseCount,
                    executionAttempted: false,
                    timestampUtc: request.NowUtc));

            return new CapabilityExecutionResult(
                ExecutionCompleted: false,
                ReasonCode: consumption.ReasonCode,
                Validation: validation,
                Consumption: consumption);
        }

        evidenceSink.Record(
            CreateEvidence(
                stage: "capability-consumption",
                capability: capability,
                outcome: "accepted",
                reasonCode: consumption.ReasonCode,
                observedUseCount: consumption.ObservedUseCount,
                executionAttempted: false,
                timestampUtc: request.NowUtc));

        try
        {
            await executor.ExecuteAsync(
                request.ResourceId,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            evidenceSink.Record(
                CreateEvidence(
                    stage: "execution",
                    capability: capability,
                    outcome: "cancelled",
                    reasonCode: "execution.cancelled-after-consumption",
                    observedUseCount: consumption.ObservedUseCount,
                    executionAttempted: true,
                    timestampUtc: request.NowUtc));

            throw;
        }
        catch (Exception)
        {
            evidenceSink.Record(
                CreateEvidence(
                    stage: "execution",
                    capability: capability,
                    outcome: "failed",
                    reasonCode: "execution.failed-after-consumption",
                    observedUseCount: consumption.ObservedUseCount,
                    executionAttempted: true,
                    timestampUtc: request.NowUtc));

            throw;
        }

        evidenceSink.Record(
            CreateEvidence(
                stage: "execution",
                capability: capability,
                outcome: "completed",
                reasonCode: "execution.completed",
                observedUseCount: consumption.ObservedUseCount,
                executionAttempted: true,
                timestampUtc: request.NowUtc));

        return new CapabilityExecutionResult(
            ExecutionCompleted: true,
            ReasonCode: "execution.completed",
            Validation: validation,
            Consumption: consumption);
    }

    private static ReplayEvidence CreateEvidence(
        string stage,
        ExecutionCapability capability,
        string outcome,
        string reasonCode,
        int? observedUseCount,
        bool executionAttempted,
        DateTimeOffset timestampUtc)
    {
        return new ReplayEvidence(
            Stage: stage,
            CapabilityId: capability.CapabilityId,
            Outcome: outcome,
            ReasonCode: reasonCode,
            ObservedUseCount: observedUseCount,
            MaximumUses: capability.MaximumUses,
            ExecutionAttempted: executionAttempted,
            TimestampUtc: timestampUtc);
    }
}
