DateTimeOffset issuedUtc =
    new(2026, 8, 19, 18, 0, 0, TimeSpan.Zero);

Console.WriteLine("Replay Protection and Bounded-Use Authority");
Console.WriteLine(new string('=', 43));
Console.WriteLine();

ExecutionCapability oneTimeCapability = CreateCapability(
    capabilityId: "cap-one-time",
    maximumUses: 1,
    issuedUtc);

CapabilityValidationRequest validRequest = CreateRequest(
    nowUtc: issuedUtc.AddMinutes(1));

var atomicStore = new AtomicInMemoryCapabilityUseStore();
var atomicExecutor = new RecordingProtectedOperationExecutor(
    writeToConsole: true);
var atomicEvidence = new InMemoryReplayEvidenceSink();
var atomicGateway = new ProtectedOperationGateway(
    new ExecutionCapabilityValidator(),
    atomicStore,
    atomicExecutor,
    atomicEvidence);

CapabilityExecutionResult[] atomicRace = await RunTwoConcurrentAsync(
    atomicGateway,
    oneTimeCapability,
    validRequest);

int acceptedAtomicConsumptions = atomicRace.Count(
    result => result.Consumption?.Accepted == true);
int rejectedAtomicConsumptions = atomicRace.Count(
    result => string.Equals(
        result.ReasonCode,
        "capability.use-limit-exceeded",
        StringComparison.Ordinal));

Require(
    acceptedAtomicConsumptions == 1,
    "Atomic one-time consumption should accept exactly one racing consumer.");
Require(
    rejectedAtomicConsumptions == 1,
    "Atomic one-time consumption should reject exactly one racing consumer.");
Require(
    atomicExecutor.InvocationCount == 1,
    "Exactly one racing consumer should reach protected execution.");

Console.WriteLine("Atomic one-time race");
Console.WriteLine($"MaximumUses: {oneTimeCapability.MaximumUses}");
Console.WriteLine("Concurrent consumers: 2");
Console.WriteLine($"Accepted consumptions: {acceptedAtomicConsumptions}");
Console.WriteLine($"Rejected consumptions: {rejectedAtomicConsumptions}");
Console.WriteLine($"Protected executions: {atomicExecutor.InvocationCount}");
Console.WriteLine();

CapabilityExecutionResult sequentialReplay = await atomicGateway.ExecuteAsync(
    oneTimeCapability,
    validRequest,
    CancellationToken.None);

Require(
    !sequentialReplay.ExecutionCompleted,
    "A later replay of the consumed capability should remain blocked.");
Require(
    atomicExecutor.InvocationCount == 1,
    "A rejected replay must not add another protected execution.");

Console.WriteLine("Sequential replay after the race");
Console.WriteLine($"Outcome: {sequentialReplay.ReasonCode}");
Console.WriteLine($"Protected executions remain: {atomicExecutor.InvocationCount}");
Console.WriteLine();

ExecutionCapability unsafeCapability = CreateCapability(
    capabilityId: "cap-unsafe-race",
    maximumUses: 1,
    issuedUtc);

var unsafeStore =
    new DeliberatelyUnsafeCheckThenActCapabilityUseStore();
var unsafeExecutor = new RecordingProtectedOperationExecutor();
var unsafeGateway = new ProtectedOperationGateway(
    new ExecutionCapabilityValidator(),
    unsafeStore,
    unsafeExecutor,
    new InMemoryReplayEvidenceSink());

CapabilityExecutionResult[] unsafeRace = await RunTwoConcurrentAsync(
    unsafeGateway,
    unsafeCapability,
    validRequest);

int unsafeExecutions = unsafeRace.Count(
    result => result.ExecutionCompleted);

Require(
    unsafeExecutions == 2,
    "The deliberately unsafe store should reproduce duplicate execution.");
Require(
    unsafeExecutor.InvocationCount == 2,
    "The deliberately unsafe store should let both racing consumers execute.");

Console.WriteLine("Deliberately unsafe check-then-act comparison");
Console.WriteLine("Both consumers read the pre-consumption state before either writes.");
Console.WriteLine($"Accepted consumptions: {unsafeRace.Count(result => result.Consumption?.Accepted == true)}");
Console.WriteLine($"Protected executions: {unsafeExecutor.InvocationCount}");
Console.WriteLine("This is the race the atomic store is designed to prevent.");
Console.WriteLine();

var invalidStore = new AtomicInMemoryCapabilityUseStore();
var invalidExecutor = new RecordingProtectedOperationExecutor();
var invalidGateway = new ProtectedOperationGateway(
    new ExecutionCapabilityValidator(),
    invalidStore,
    invalidExecutor,
    new InMemoryReplayEvidenceSink());

CapabilityExecutionResult expiredResult = await invalidGateway.ExecuteAsync(
    oneTimeCapability with { CapabilityId = "cap-expired" },
    CreateRequest(nowUtc: oneTimeCapability.ExpiresUtc),
    CancellationToken.None);

Require(
    !expiredResult.ExecutionCompleted,
    "An expired capability should be rejected before consumption.");
Require(
    invalidStore.GetObservedUseCount("cap-expired") == 0,
    "Static validation failure should not spend bounded-use authority.");
Require(
    invalidExecutor.InvocationCount == 0,
    "Static validation failure should not reach the executor.");

Console.WriteLine("Static validation before stateful consumption");
Console.WriteLine($"Expired capability outcome: {expiredResult.ReasonCode}");
Console.WriteLine("Consumed uses: 0");
Console.WriteLine("Protected executions: 0");
Console.WriteLine();

ReplayEvidence replayEvidence = atomicEvidence.Snapshot()
    .First(evidence =>
        evidence.Stage == "capability-consumption" &&
        evidence.Outcome == "rejected");

Console.WriteLine("Replay evidence");
Console.WriteLine($"Stage: {replayEvidence.Stage}");
Console.WriteLine($"Reason: {replayEvidence.ReasonCode}");
Console.WriteLine($"ObservedUseCount: {replayEvidence.ObservedUseCount}");
Console.WriteLine($"MaximumUses: {replayEvidence.MaximumUses}");
Console.WriteLine($"ExecutionAttempted: {replayEvidence.ExecutionAttempted}");
Console.WriteLine();

Console.WriteLine("Boundary notes:");
Console.WriteLine("- The in-memory atomic store coordinates only this process.");
Console.WriteLine("- Restart or another process would require shared durable replay state.");
Console.WriteLine("- Consumption is recorded before the protected executor is invoked.");
Console.WriteLine("- A consumed capability does not prove an external side effect completed exactly once.");
Console.WriteLine("- Request idempotency and downstream operation idempotency remain separate concerns.");

static ExecutionCapability CreateCapability(
    string capabilityId,
    int maximumUses,
    DateTimeOffset issuedUtc)
{
    return new ExecutionCapability(
        CapabilityId: capabilityId,
        SubjectId: "operator-7",
        OperationName: "account.disable",
        ResourceId: "user-100",
        Audience: "account-admin-gateway",
        IssuedUtc: issuedUtc,
        ExpiresUtc: issuedUtc.AddMinutes(5),
        MaximumUses: maximumUses);
}

static CapabilityValidationRequest CreateRequest(
    DateTimeOffset nowUtc,
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
        NowUtc: nowUtc);
}

static async Task<CapabilityExecutionResult[]> RunTwoConcurrentAsync(
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

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
