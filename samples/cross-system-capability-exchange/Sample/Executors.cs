namespace CrossSystemCapabilityExchange;

public interface IExportExecutor
{
    Task<ExportExecutionResult> ExportAsync(
        ValidatedExportCommand command,
        CancellationToken cancellationToken);
}

public sealed class RecordingExportExecutor : IExportExecutor
{
    private readonly string _currentResourceVersion;
    private readonly IReadOnlySet<string> _allowedDestinations;
    private int _invocationCount;

    public RecordingExportExecutor(
        string currentResourceVersion = "snapshot-8",
        IReadOnlySet<string>? allowedDestinations = null)
    {
        _currentResourceVersion = currentResourceVersion;
        _allowedDestinations = allowedDestinations ??
            new HashSet<string>(
                new[] { SampleScenarios.DefaultDestination },
                StringComparer.Ordinal);
    }

    public int InvocationCount =>
        Volatile.Read(ref _invocationCount);

    public ValidatedExportCommand? LastCommand { get; private set; }

    public Task<ExportExecutionResult> ExportAsync(
        ValidatedExportCommand command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // These checks intentionally live at the executor boundary. Validation
        // earlier in the gateway cannot guarantee that resource or destination
        // state remained unchanged up to the actual side effect.
        if (!string.Equals(
                command.ResourceVersion,
                _currentResourceVersion,
                StringComparison.Ordinal))
        {
            return Task.FromResult(
                ExportExecutionResult.Reject(
                    "executor.resource-version-mismatch"));
        }

        if (!_allowedDestinations.Contains(command.Destination))
        {
            return Task.FromResult(
                ExportExecutionResult.Reject(
                    "executor.destination-not-allowed"));
        }

        Interlocked.Increment(ref _invocationCount);
        LastCommand = command;

        Console.WriteLine(
            $"SIMULATED EXPORT: {command.ResourceId}@{command.ResourceVersion} " +
            $"-> {command.Destination} ({command.Purpose})");

        return Task.FromResult(
            ExportExecutionResult.Success());
    }
}

public sealed class ThrowingExportExecutor : IExportExecutor
{
    public Task<ExportExecutionResult> ExportAsync(
        ValidatedExportCommand command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        throw new InvalidOperationException(
            "Simulated executor failure after authority was claimed.");
    }
}
