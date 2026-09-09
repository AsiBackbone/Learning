using System.Diagnostics;

public sealed record GovernanceObservedEvent(
    string Name,
    IReadOnlyDictionary<string, string> Tags);

public sealed record GovernanceObservedActivity(
    string Name,
    string TraceId,
    string SpanId,
    string ParentSpanId,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyList<GovernanceObservedEvent> Events);

public static class GovernanceObservabilityInstrumentation
{
    public const string ActivitySourceName =
        "AsiBackbone.Learning.GovernedAiToolGateway";

    public const string CorrelationIdTagName =
        "governance.correlation_id";

    private static readonly ActivitySource Source =
        new(ActivitySourceName, "1.0.0");

    public static Activity? StartWorkflow(string correlationId)
    {
        Activity? activity = Source.StartActivity(
            "ai.governance.workflow",
            ActivityKind.Internal);

        activity?.SetTag(
            CorrelationIdTagName,
            correlationId);

        return activity;
    }

    public static Activity? StartStage(
        string name,
        string correlationId,
        string? proposalId = null)
    {
        Activity? activity = Source.StartActivity(
            name,
            ActivityKind.Internal);

        activity?.SetTag(
            CorrelationIdTagName,
            correlationId);

        if (!string.IsNullOrWhiteSpace(proposalId))
        {
            activity?.SetTag(
                "ai.proposal.id",
                proposalId);
        }

        return activity;
    }

    public static void RecordAuditEvent(AuditResidue residue)
    {
        var tags = new ActivityTagsCollection
        {
            { CorrelationIdTagName, residue.CorrelationId },
            { "governance.stage", residue.Stage },
            { "governance.outcome", residue.Outcome },
            { "governance.reason_code", residue.ReasonCode }
        };

        if (!string.IsNullOrWhiteSpace(residue.PolicyVersion))
        {
            tags.Add(
                "governance.policy.version",
                residue.PolicyVersion);
        }

        Activity.Current?.AddEvent(
            new ActivityEvent(
                $"governance.{residue.Stage}",
                tags: tags));
    }
}

public sealed class GovernanceTraceCollector : IDisposable
{
    private readonly object _sync = new();
    private readonly List<GovernanceObservedActivity> _activities = [];
    private readonly string _correlationId;
    private readonly ActivityListener _listener;

    public GovernanceTraceCollector(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException(
                "A correlation ID is required to scope collected activities.",
                nameof(correlationId));
        }

        _correlationId = correlationId;

        _listener = new ActivityListener
        {
            ShouldListenTo = source => string.Equals(
                source.Name,
                GovernanceObservabilityInstrumentation.ActivitySourceName,
                StringComparison.Ordinal),
            Sample = static (
                ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (
                ref ActivityCreationOptions<string> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (!string.Equals(
                        activity.GetTagItem(
                            GovernanceObservabilityInstrumentation
                                .CorrelationIdTagName)?.ToString(),
                        _correlationId,
                        StringComparison.Ordinal))
                {
                    return;
                }

                Dictionary<string, string> tags = activity.TagObjects
                    .ToDictionary(
                        item => item.Key,
                        item => item.Value?.ToString() ?? string.Empty,
                        StringComparer.Ordinal);

                GovernanceObservedEvent[] events = activity.Events
                    .Select(item =>
                        new GovernanceObservedEvent(
                            Name: item.Name,
                            Tags: item.Tags.ToDictionary(
                                tag => tag.Key,
                                tag => tag.Value?.ToString() ?? string.Empty,
                                StringComparer.Ordinal)))
                    .ToArray();

                lock (_sync)
                {
                    _activities.Add(
                        new GovernanceObservedActivity(
                            Name: activity.OperationName,
                            TraceId: activity.TraceId.ToString(),
                            SpanId: activity.SpanId.ToString(),
                            ParentSpanId: activity.ParentSpanId.ToString(),
                            Tags: tags,
                            Events: events));
                }
            }
        };

        ActivitySource.AddActivityListener(_listener);
    }

    public IReadOnlyList<GovernanceObservedActivity> Snapshot()
    {
        lock (_sync)
        {
            return _activities.ToArray();
        }
    }

    public void Dispose()
    {
        _listener.Dispose();
    }
}

public enum GovernanceObservabilityScenario
{
    Allowed,
    Denied,
    AcknowledgmentRequired
}

public sealed class GovernanceObservabilityFakeModel
{
    private const string ModelId =
        "deterministic-observability-model-v1";

    public AiToolProposal Propose(
        string correlationId,
        GovernanceObservabilityScenario scenario)
    {
        string proposalId =
            $"proposal-observe-{scenario.ToString().ToLowerInvariant()}";

        using Activity? inference =
            GovernanceObservabilityInstrumentation.StartStage(
                "model.inference",
                correlationId,
                proposalId);

        inference?.SetTag("ai.model.id", ModelId);
        inference?.SetTag("tool.name", "notification.send");

        string recipient = scenario switch
        {
            GovernanceObservabilityScenario.Allowed =>
                "employee@example.internal",
            GovernanceObservabilityScenario.Denied =>
                "recipient@blocked.example",
            GovernanceObservabilityScenario.AcknowledgmentRequired =>
                "partner@example.net",
            _ => throw new ArgumentOutOfRangeException(
                nameof(scenario),
                scenario,
                "Unknown observability scenario.")
        };

        return new AiToolProposal(
            ProposalId: proposalId,
            ModelId: ModelId,
            ToolName: "notification.send",
            Arguments: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["recipient"] = recipient,
                ["template"] = "case-update"
            },
            ModelRationale:
                "Deterministic fake model output for governance tracing.");
    }
}

public sealed record GovernanceObservabilityRun(
    string CorrelationId,
    AiToolProposal Proposal,
    GatewayResult Result,
    int ExecutorInvocationCount,
    IReadOnlyList<GovernanceObservedActivity> Activities,
    IReadOnlyList<AuditResidue> AuditEntries);

public static class GovernanceObservabilityRunner
{
    private static readonly DateTimeOffset NowUtc =
        new(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);

    public static async Task<GovernanceObservabilityRun> RunAsync(
        GovernanceObservabilityScenario scenario,
        string correlationId)
    {
        using var collector =
            new GovernanceTraceCollector(correlationId);
        SampleHost host = SampleComposition.Create();
        var model = new GovernanceObservabilityFakeModel();
        GatewayResult result;
        AiToolProposal proposal;

        using (Activity? workflow =
               GovernanceObservabilityInstrumentation.StartWorkflow(
                   correlationId))
        {
            proposal = model.Propose(correlationId, scenario);

            workflow?.SetTag(
                "ai.proposal.id",
                proposal.ProposalId);
            workflow?.SetTag(
                "ai.model.id",
                proposal.ModelId);
            workflow?.SetTag(
                "tool.name",
                proposal.ToolName);

            result = await InvokeGatewayAsync(
                host,
                proposal,
                correlationId,
                NowUtc,
                acknowledgmentResponse: null);

            if (scenario ==
                GovernanceObservabilityScenario.AcknowledgmentRequired)
            {
                AcknowledgmentChallenge challenge =
                    result.AcknowledgmentChallenge ??
                    throw new InvalidOperationException(
                        "The observability acknowledgment scenario should pause for acknowledgment.");

                using (Activity? responseActivity =
                       GovernanceObservabilityInstrumentation.StartStage(
                           "acknowledgment.respond",
                           correlationId,
                           proposal.ProposalId))
                {
                    responseActivity?.SetTag(
                        "acknowledgment.challenge_id",
                        challenge.ChallengeId);
                    responseActivity?.SetTag(
                        "acknowledgment.accepted",
                        true);
                }

                result = await InvokeGatewayAsync(
                    host,
                    proposal,
                    correlationId,
                    NowUtc.AddSeconds(5),
                    new AcknowledgmentResponse(
                        ChallengeId: challenge.ChallengeId,
                        ActorId: "operator-7",
                        Accepted: true,
                        RespondedUtc: NowUtc.AddSeconds(5)));
            }

            workflow?.SetTag(
                "governance.final_status",
                result.Status.ToString());
            workflow?.SetTag(
                "governance.final_reason_code",
                result.ReasonCode);
            workflow?.SetTag(
                "execution.invocation_count",
                host.Handler.InvocationCount);
        }

        AuditResidue[] auditEntries = host.AuditSink.Entries
            .Where(entry => string.Equals(
                entry.CorrelationId,
                correlationId,
                StringComparison.Ordinal))
            .ToArray();

        return new GovernanceObservabilityRun(
            CorrelationId: correlationId,
            Proposal: proposal,
            Result: result,
            ExecutorInvocationCount: host.Handler.InvocationCount,
            Activities: collector.Snapshot(),
            AuditEntries: auditEntries);
    }

    private static async Task<GatewayResult> InvokeGatewayAsync(
        SampleHost host,
        AiToolProposal proposal,
        string correlationId,
        DateTimeOffset nowUtc,
        AcknowledgmentResponse? acknowledgmentResponse)
    {
        using Activity? gateway =
            GovernanceObservabilityInstrumentation.StartStage(
                "host.governance-gateway",
                correlationId,
                proposal.ProposalId);

        GatewayResult result = await host.Gateway.ExecuteAsync(
            proposal,
            new HostActor("operator-7", "tenant-a"),
            nowUtc,
            acknowledgmentResponse,
            CancellationToken.None,
            hostCorrelationId: correlationId);

        gateway?.SetTag(
            "governance.gateway.status",
            result.Status.ToString());
        gateway?.SetTag(
            "governance.reason_code",
            result.ReasonCode);
        gateway?.SetTag(
            "capability.id",
            result.CapabilityId);
        gateway?.SetTag(
            "acknowledgment.challenge_id",
            result.AcknowledgmentChallenge?.ChallengeId);

        return result;
    }
}

public static class GovernanceObservabilityDemo
{
    public static async Task RunAsync()
    {
        Console.WriteLine();
        Console.WriteLine("AI governance observability traces");
        Console.WriteLine(new string('=', 35));
        Console.WriteLine();
        Console.WriteLine(
            "Telemetry records what happened. Telemetry does not authorize what may happen.");
        Console.WriteLine();

        await PrintScenarioAsync(
            "Allowed proposal",
            GovernanceObservabilityScenario.Allowed,
            "corr-observe-allowed");

        await PrintScenarioAsync(
            "Denied proposal",
            GovernanceObservabilityScenario.Denied,
            "corr-observe-denied");

        await PrintScenarioAsync(
            "Acknowledgment before scoped authority",
            GovernanceObservabilityScenario.AcknowledgmentRequired,
            "corr-observe-ack");
    }

    private static async Task PrintScenarioAsync(
        string title,
        GovernanceObservabilityScenario scenario,
        string correlationId)
    {
        GovernanceObservabilityRun run =
            await GovernanceObservabilityRunner.RunAsync(
                scenario,
                correlationId);

        Console.WriteLine($"Scenario: {title}");
        Console.WriteLine($"Correlation ID: {run.CorrelationId}");
        Console.WriteLine($"Proposal ID: {run.Proposal.ProposalId}");
        Console.WriteLine($"Final status: {run.Result.Status}");
        Console.WriteLine($"Reason: {run.Result.ReasonCode}");
        Console.WriteLine(
            $"Executor invocations: {run.ExecutorInvocationCount}");

        Console.WriteLine("Trace activities:");

        foreach (GovernanceObservedActivity activity in run.Activities)
        {
            Console.WriteLine(
                $"- {activity.Name} " +
                $"trace={activity.TraceId} " +
                $"span={activity.SpanId} " +
                $"parent={activity.ParentSpanId}");
        }

        Console.WriteLine("Audit evidence:");

        foreach (AuditResidue residue in run.AuditEntries)
        {
            Console.WriteLine(
                $"- {residue.Stage}: {residue.Outcome} " +
                $"({residue.ReasonCode}) " +
                $"policy={residue.PolicyVersion ?? "-"}");
        }

        Console.WriteLine();
    }
}
