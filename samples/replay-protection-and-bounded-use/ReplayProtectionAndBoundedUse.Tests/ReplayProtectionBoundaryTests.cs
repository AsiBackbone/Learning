using Xunit;

namespace ReplayProtectionAndBoundedUse.Tests;

public sealed class ReplayProtectionBoundaryTests
{
    private static readonly DateTimeOffset IssuedUtc =
        new(2026, 8, 19, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidUnusedCapabilityExecutesOnce()
    {
        var store = new AtomicInMemoryCapabilityUseStore();
        var executor = new RecordingProtectedOperationExecutor();
        var gateway = CreateGateway(store, executor);
        ExecutionCapability capability = CreateCapability();

        CapabilityExecutionResult result = await gateway.ExecuteAsync(
            capability,
            CreateRequest(),
            CancellationToken.None);

        Assert.True(result.ExecutionCompleted);
        Assert.Equal("execution.completed", result.ReasonCode);
        Assert.NotNull(result.Consumption);
        Assert.True(result.Consumption!.Accepted);
        Assert.Equal(1, store.GetObservedUseCount(capability.CapabilityId));
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task SecondSequentialUseIsRejectedAndExecutionCountRemainsOne()
    {
        var store = new AtomicInMemoryCapabilityUseStore();
        var executor = new RecordingProtectedOperationExecutor();
        var gateway = CreateGateway(store, executor);
        ExecutionCapability capability = CreateCapability();

        CapabilityExecutionResult first = await gateway.ExecuteAsync(
            capability,
            CreateRequest(),
            CancellationToken.None);

        CapabilityExecutionResult second = await gateway.ExecuteAsync(
            capability,
            CreateRequest(),
            CancellationToken.None);

        Assert.True(first.ExecutionCompleted);
        Assert.False(second.ExecutionCompleted);
        Assert.Equal("capability.use-limit-exceeded", second.ReasonCode);
        Assert.Equal(1, store.GetObservedUseCount(capability.CapabilityId));
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task TwoConcurrentConsumersOfOneTimeCapabilityExactlyOneExecutes()
    {
        var store = new AtomicInMemoryCapabilityUseStore();
        var executor = new RecordingProtectedOperationExecutor();
        var gateway = CreateGateway(store, executor);
        ExecutionCapability capability = CreateCapability(maximumUses: 1);

        CapabilityExecutionResult[] results = await RunTwoConcurrentAsync(
            gateway,
            capability,
            CreateRequest());

        Assert.Equal(
            1,
            results.Count(result => result.Consumption?.Accepted == true));
        Assert.Equal(
            1,
            results.Count(result =>
                result.ReasonCode == "capability.use-limit-exceeded"));
        Assert.Equal(
            1,
            results.Count(result => result.ExecutionCompleted));
        Assert.Equal(1, store.GetObservedUseCount(capability.CapabilityId));
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task DeliberatelyUnsafeCheckThenActAllowsBothConcurrentConsumers()
    {
        var store =
            new DeliberatelyUnsafeCheckThenActCapabilityUseStore();
        var executor = new RecordingProtectedOperationExecutor();
        var gateway = CreateGateway(store, executor);
        ExecutionCapability capability = CreateCapability(
            capabilityId: "cap-unsafe-race",
            maximumUses: 1);

        CapabilityExecutionResult[] results = await RunTwoConcurrentAsync(
            gateway,
            capability,
            CreateRequest());

        Assert.Equal(
            2,
            results.Count(result => result.Consumption?.Accepted == true));
        Assert.Equal(2, results.Count(result => result.ExecutionCompleted));
        Assert.Equal(2, executor.InvocationCount);

        // Both callers wrote the same stale value, so the unsafe store can even
        // under-report how many accepted executions actually occurred.
        Assert.Equal(1, store.GetObservedUseCount(capability.CapabilityId));
    }

    [Fact]
    public async Task BoundedUseCapabilityStopsAtConfiguredMaximum()
    {
        var store = new AtomicInMemoryCapabilityUseStore();
        var executor = new RecordingProtectedOperationExecutor();
        var gateway = CreateGateway(store, executor);
        ExecutionCapability capability = CreateCapability(maximumUses: 2);

        CapabilityExecutionResult first = await gateway.ExecuteAsync(
            capability,
            CreateRequest(),
            CancellationToken.None);
        CapabilityExecutionResult second = await gateway.ExecuteAsync(
            capability,
            CreateRequest(),
            CancellationToken.None);
        CapabilityExecutionResult third = await gateway.ExecuteAsync(
            capability,
            CreateRequest(),
            CancellationToken.None);

        Assert.True(first.ExecutionCompleted);
        Assert.True(second.ExecutionCompleted);
        Assert.False(third.ExecutionCompleted);
        Assert.Equal("capability.use-limit-exceeded", third.ReasonCode);
        Assert.Equal(2, store.GetObservedUseCount(capability.CapabilityId));
        Assert.Equal(2, executor.InvocationCount);
    }

    [Fact]
    public async Task TwoConcurrentConsumersCannotBothClaimFinalBoundedUse()
    {
        var store = new AtomicInMemoryCapabilityUseStore();
        var executor = new RecordingProtectedOperationExecutor();
        var gateway = CreateGateway(store, executor);
        ExecutionCapability capability = CreateCapability(
            capabilityId: "cap-bounded-final-use",
            maximumUses: 2);

        CapabilityExecutionResult first = await gateway.ExecuteAsync(
            capability,
            CreateRequest(),
            CancellationToken.None);

        Assert.True(first.ExecutionCompleted);
        Assert.Equal(1, store.GetObservedUseCount(capability.CapabilityId));

        CapabilityExecutionResult[] finalUseRace = await RunTwoConcurrentAsync(
            gateway,
            capability,
            CreateRequest());

        Assert.Equal(
            1,
            finalUseRace.Count(result => result.ExecutionCompleted));
        Assert.Equal(
            1,
            finalUseRace.Count(result =>
                result.ReasonCode == "capability.use-limit-exceeded"));
        Assert.Equal(2, store.GetObservedUseCount(capability.CapabilityId));
        Assert.Equal(2, executor.InvocationCount);
    }

    [Fact]
    public async Task ExpiredCapabilityIsRejectedBeforeConsumption()
    {
        var store = new AtomicInMemoryCapabilityUseStore();
        var executor = new RecordingProtectedOperationExecutor();
        var gateway = CreateGateway(store, executor);
        ExecutionCapability capability = CreateCapability();

        CapabilityExecutionResult result = await gateway.ExecuteAsync(
            capability,
            CreateRequest(nowUtc: capability.ExpiresUtc),
            CancellationToken.None);

        Assert.False(result.ExecutionCompleted);
        Assert.Equal("capability.expired", result.ReasonCode);
        Assert.Null(result.Consumption);
        Assert.Equal(0, store.GetObservedUseCount(capability.CapabilityId));
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task MismatchedCapabilityIsRejectedBeforeConsumption()
    {
        var store = new AtomicInMemoryCapabilityUseStore();
        var executor = new RecordingProtectedOperationExecutor();
        var gateway = CreateGateway(store, executor);
        ExecutionCapability capability = CreateCapability();

        CapabilityExecutionResult result = await gateway.ExecuteAsync(
            capability,
            CreateRequest(resourceId: "user-999"),
            CancellationToken.None);

        Assert.False(result.ExecutionCompleted);
        Assert.Equal("capability.resource-mismatch", result.ReasonCode);
        Assert.Null(result.Consumption);
        Assert.Equal(0, store.GetObservedUseCount(capability.CapabilityId));
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task RejectedReplayLeavesConsumptionEvidenceWithoutExecutionAttempt()
    {
        var store = new AtomicInMemoryCapabilityUseStore();
        var executor = new RecordingProtectedOperationExecutor();
        var evidence = new InMemoryReplayEvidenceSink();
        var gateway = CreateGateway(store, executor, evidence);
        ExecutionCapability capability = CreateCapability();

        _ = await gateway.ExecuteAsync(
            capability,
            CreateRequest(),
            CancellationToken.None);

        _ = await gateway.ExecuteAsync(
            capability,
            CreateRequest(),
            CancellationToken.None);

        ReplayEvidence rejected = evidence.Snapshot().Single(item =>
            item.Stage == "capability-consumption" &&
            item.Outcome == "rejected");

        Assert.Equal(capability.CapabilityId, rejected.CapabilityId);
        Assert.Equal("capability.use-limit-exceeded", rejected.ReasonCode);
        Assert.Equal(1, rejected.ObservedUseCount);
        Assert.Equal(1, rejected.MaximumUses);
        Assert.False(rejected.ExecutionAttempted);
    }

    [Fact]
    public async Task CancellationBeforeConsumptionDoesNotSpendAuthority()
    {
        var store = new AtomicInMemoryCapabilityUseStore();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.TryConsumeAsync(
                    "cap-cancelled",
                    maximumUses: 1,
                    usedUtc: IssuedUtc,
                    cancellation.Token)
                .AsTask());

        Assert.Equal(0, store.GetObservedUseCount("cap-cancelled"));
    }

    [Fact]
    public async Task UnavailableUseStoreDoesNotBecomePermission()
    {
        var executor = new RecordingProtectedOperationExecutor();
        var evidence = new InMemoryReplayEvidenceSink();
        var gateway = CreateGateway(
            new UnavailableCapabilityUseStore(),
            executor,
            evidence);

        CapabilityExecutionResult result = await gateway.ExecuteAsync(
            CreateCapability(),
            CreateRequest(),
            CancellationToken.None);

        Assert.False(result.ExecutionCompleted);
        Assert.Equal("capability.use-store-unavailable", result.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
        Assert.Contains(
            evidence.Snapshot(),
            item =>
                item.Stage == "capability-consumption" &&
                item.Outcome == "unavailable" &&
                !item.ExecutionAttempted);
    }

    [Fact]
    public async Task ExecutorFailureAfterConsumptionDoesNotRestoreAuthority()
    {
        var store = new AtomicInMemoryCapabilityUseStore();
        var executor = new ThrowingProtectedOperationExecutor();
        var evidence = new InMemoryReplayEvidenceSink();
        var gateway = CreateGateway(store, executor, evidence);
        ExecutionCapability capability = CreateCapability();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => gateway.ExecuteAsync(
                capability,
                CreateRequest(),
                CancellationToken.None));

        Assert.Equal(1, store.GetObservedUseCount(capability.CapabilityId));
        Assert.Equal(1, executor.InvocationCount);
        Assert.Contains(
            evidence.Snapshot(),
            item =>
                item.Stage == "execution" &&
                item.Outcome == "failed" &&
                item.ReasonCode == "execution.failed-after-consumption");

        CapabilityExecutionResult replay = await gateway.ExecuteAsync(
            capability,
            CreateRequest(),
            CancellationToken.None);

        Assert.False(replay.ExecutionCompleted);
        Assert.Equal("capability.use-limit-exceeded", replay.ReasonCode);
        Assert.Equal(1, executor.InvocationCount);
    }

    private static ProtectedOperationGateway CreateGateway(
        ICapabilityUseStore store,
        IProtectedOperationExecutor executor,
        IReplayEvidenceSink? evidence = null)
    {
        return new ProtectedOperationGateway(
            new ExecutionCapabilityValidator(),
            store,
            executor,
            evidence ?? new InMemoryReplayEvidenceSink());
    }

    private static ExecutionCapability CreateCapability(
        string capabilityId = "cap-one-time",
        int maximumUses = 1)
    {
        return new ExecutionCapability(
            CapabilityId: capabilityId,
            SubjectId: "operator-7",
            OperationName: "account.disable",
            ResourceId: "user-100",
            Audience: "account-admin-gateway",
            IssuedUtc: IssuedUtc,
            ExpiresUtc: IssuedUtc.AddMinutes(5),
            MaximumUses: maximumUses);
    }

    private static CapabilityValidationRequest CreateRequest(
        DateTimeOffset? nowUtc = null,
        string subjectId = "operator-7",
        string operationName = "account.disable",
        string resourceId = "user-100",
        string audience = "account-admin-gateway")
    {
        return new CapabilityValidationRequest(
            SubjectId: subjectId,
            OperationName: operationName,
            ResourceId: resourceId,
            Audience: audience,
            NowUtc: nowUtc ?? IssuedUtc.AddMinutes(1));
    }

    private static async Task<CapabilityExecutionResult[]> RunTwoConcurrentAsync(
        ProtectedOperationGateway gateway,
        ExecutionCapability capability,
        CapabilityValidationRequest request)
    {
        var start = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<CapabilityExecutionResult> AttemptAsync()
        {
            await start.Task;

            return await gateway.ExecuteAsync(
                capability,
                request,
                CancellationToken.None);
        }

        Task<CapabilityExecutionResult> first = AttemptAsync();
        Task<CapabilityExecutionResult> second = AttemptAsync();

        start.SetResult(true);

        return await Task.WhenAll(first, second);
    }

    private sealed class UnavailableCapabilityUseStore : ICapabilityUseStore
    {
        public ValueTask<CapabilityUseResult> TryConsumeAsync(
            string capabilityId,
            int maximumUses,
            DateTimeOffset usedUtc,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromException<CapabilityUseResult>(
                new CapabilityUseStoreUnavailableException(
                    "The teaching use store is unavailable."));
        }
    }

    private sealed class ThrowingProtectedOperationExecutor
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

            throw new InvalidOperationException(
                "Simulated protected execution failure.");
        }
    }
}
