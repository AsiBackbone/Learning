namespace DistributedAcknowledgmentContinuation;

public static class Program
{
    public static async Task Main()
    {
        Console.WriteLine("Distributed Acknowledgment and Continuation");
        Console.WriteLine("Simulated System A -> System B -> System C workflow");
        Console.WriteLine();

        await RunScenarioAsync(
            "valid acknowledgment",
            SampleScenarios.CreateEvidence(),
            currentPolicyAllows: true);

        await RunScenarioAsync(
            "current policy denies after acknowledgment",
            SampleScenarios.CreateEvidence(),
            currentPolicyAllows: false);

        await RunReplayScenarioAsync();

        Console.WriteLine();
        Console.WriteLine("Important limitations:");
        Console.WriteLine("- Evidence trust is simulated; there are no real keys or credentials.");
        Console.WriteLine("- Challenge, continuation-state, and claim stores are process-local teaching implementations.");
        Console.WriteLine("- The sample chooses exact-snapshot resource continuation.");
        Console.WriteLine("- Presentation binding uses a fictional deterministic presentation fingerprint.");
        Console.WriteLine("- The executor performs a local dry-run only.");
    }

    private static async Task RunScenarioAsync(
        string name,
        AcknowledgmentEvidence evidence,
        bool currentPolicyAllows)
    {
        var executor = new RecordingContinuationExecutor();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                policyEvaluator: new SimulatedCurrentPolicyEvaluator(
                    currentPolicyAllows));

        GatewayResult result = await gateway.ExecuteAsync(
            SampleScenarios.CreateContinuationRequest(),
            evidence,
            CancellationToken.None);

        Console.WriteLine($"Scenario: {name}");
        Console.WriteLine($"Internal result: {result.InternalReasonCode}");
        Console.WriteLine($"Executed: {result.Executed}");
        Console.WriteLine($"Executor invocations: {executor.InvocationCount}");
        Console.WriteLine();
    }

    private static async Task RunReplayScenarioAsync()
    {
        var executor = new RecordingContinuationExecutor();
        var claimStore = new InMemoryContinuationClaimStore();
        DistributedAcknowledgmentGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                claimStore: claimStore);
        ContinuationRequest request =
            SampleScenarios.CreateContinuationRequest();
        AcknowledgmentEvidence evidence =
            SampleScenarios.CreateEvidence();

        GatewayResult first = await gateway.ExecuteAsync(
            request,
            evidence,
            CancellationToken.None);

        GatewayResult second = await gateway.ExecuteAsync(
            request,
            evidence,
            CancellationToken.None);

        Console.WriteLine("Scenario: replayed acknowledgment");
        Console.WriteLine($"First: {first.InternalReasonCode}");
        Console.WriteLine($"Second: {second.InternalReasonCode}");
        Console.WriteLine($"Executor invocations: {executor.InvocationCount}");
    }
}
