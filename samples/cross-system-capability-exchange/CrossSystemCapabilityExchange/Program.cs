namespace CrossSystemCapabilityExchange;

public static class Program
{
    public static async Task Main()
    {
        Console.WriteLine("Cross-System Capability Exchange");
        Console.WriteLine("Simulated System A -> System B authority handoff");
        Console.WriteLine();

        await RunScenarioAsync(
            "valid direct grant",
            SampleScenarios.CreateArtifact(),
            SampleScenarios.CreateContext(),
            expectedExecuted: true);

        await RunScenarioAsync(
            "wrong audience",
            SampleScenarios.CreateArtifact(),
            SampleScenarios.CreateContext(
                audience: "system-b:account-admin"),
            expectedExecuted: false);

        await RunScenarioAsync(
            "resource drift",
            SampleScenarios.CreateArtifact(),
            SampleScenarios.CreateContext(
                resourceVersion: "snapshot-9"),
            expectedExecuted: false);

        await RunReplayScenarioAsync();

        Console.WriteLine();
        Console.WriteLine("Important limitations:");
        Console.WriteLine("- Proof verification is simulated; there are no real keys or credentials.");
        Console.WriteLine("- Replay state is atomic only inside this process.");
        Console.WriteLine("- Presenter binding is a policy check, not cryptographic proof of possession.");
        Console.WriteLine("- The executor performs a local dry-run only.");
    }

    private static async Task RunScenarioAsync(
        string name,
        ProtectedCapabilityArtifact artifact,
        RecipientExportContext context,
        bool expectedExecuted)
    {
        var executor = new RecordingExportExecutor();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(executor);

        GatewayResult result = await gateway.ExecuteAsync(
            artifact,
            context,
            CancellationToken.None);

        if (result.Executed != expectedExecuted)
        {
            throw new InvalidOperationException(
                $"Scenario '{name}' expected Executed={expectedExecuted} " +
                $"but received Executed={result.Executed}.");
        }

        Console.WriteLine($"Scenario: {name}");
        Console.WriteLine($"Recipient decision: {result.RecipientDecisionId}");
        Console.WriteLine($"Internal result: {result.InternalReasonCode}");
        Console.WriteLine($"Public result: {result.PublicReasonCode}");
        Console.WriteLine($"Execution identity: {result.ExecutionId ?? "n/a"}");
        Console.WriteLine($"Executor invocations: {executor.InvocationCount}");
        Console.WriteLine();
    }

    private static async Task RunReplayScenarioAsync()
    {
        var executor = new RecordingExportExecutor();
        var useStore = new InMemoryCapabilityUseStore();
        CrossSystemGateway gateway =
            SampleScenarios.CreateGateway(
                executor,
                useStore);
        ProtectedCapabilityArtifact artifact =
            SampleScenarios.CreateArtifact();
        RecipientExportContext context =
            SampleScenarios.CreateContext();

        GatewayResult first = await gateway.ExecuteAsync(
            artifact,
            context,
            CancellationToken.None);

        GatewayResult second = await gateway.ExecuteAsync(
            artifact,
            context,
            CancellationToken.None);

        if (!first.Executed ||
            second.Executed ||
            executor.InvocationCount != 1)
        {
            throw new InvalidOperationException(
                "Replay scenario did not preserve the single-use invariant.");
        }

        Console.WriteLine("Scenario: replayed second use");
        Console.WriteLine($"First: {first.InternalReasonCode}");
        Console.WriteLine($"Second: {second.InternalReasonCode}");
        Console.WriteLine($"Executor invocations: {executor.InvocationCount}");
    }
}
