namespace AdaptiveRiskContext;

public static class Program
{
    public static async Task Main()
    {
        RiskPolicyEvaluator evaluator = new();
        RiskGovernancePolicy policy = SampleScenarios.CreatePolicy();

        Show(
            "Initial low-risk observation",
            evaluator.Evaluate(
                "decision-demo-low-risk",
                SampleScenarios.CreatePayment(),
                RiskSignalInput.Available(SampleScenarios.CreateObservation()),
                policy,
                SampleScenarios.BaselineUtc.AddMinutes(1)));

        Show(
            "Risk provider unavailable",
            evaluator.Evaluate(
                "decision-demo-provider-unavailable",
                SampleScenarios.CreatePayment(),
                RiskSignalInput.Unavailable("fraud-service"),
                policy,
                SampleScenarios.BaselineUtc.AddMinutes(1)));

        Show(
            "Stale stored observation",
            evaluator.Evaluate(
                "decision-demo-stale",
                SampleScenarios.CreatePayment(),
                RiskSignalInput.Available(SampleScenarios.CreateObservation()),
                policy,
                SampleScenarios.BaselineUtc.AddMinutes(10)));

        Show(
            "Current state after model/risk/environment drift",
            evaluator.Evaluate(
                "decision-demo-current-drift",
                SampleScenarios.CreatePayment(
                    resourceVersion: "pay-981:v2",
                    incidentPosture: IncidentPosture.Elevated,
                    environmentVersion: "env-elevated-v2"),
                RiskSignalInput.Available(
                    SampleScenarios.CreateObservation(
                        fraudProbability: 0.76m,
                        observationId: "risk-observation-2001",
                        modelVersion: "risk-v8",
                        observedAtUtc: SampleScenarios.BaselineUtc.AddMinutes(4),
                        providerValidUntilUtc: SampleScenarios.BaselineUtc.AddMinutes(14))),
                policy,
                SampleScenarios.BaselineUtc.AddMinutes(5)));

        await RunExecutionBoundaryDemoAsync(evaluator, policy);

        Console.WriteLine("Teaching boundary:");
        Console.WriteLine("- risk observations are inputs, not execution credentials");
        Console.WriteLine("- new model output creates new evidence rather than rewriting history");
        Console.WriteLine("- stale/unavailable/drifted context requires explicit policy behavior");
        Console.WriteLine("- current scoped authority is single-use before dry-run execution");
    }

    private static async Task RunExecutionBoundaryDemoAsync(
        RiskPolicyEvaluator evaluator,
        RiskGovernancePolicy policy)
    {
        Console.WriteLine("Full execution-boundary demonstration");

        PaymentContext payment = SampleScenarios.CreatePayment();
        RiskSignalInput risk =
            RiskSignalInput.Available(SampleScenarios.CreateObservation());
        DateTimeOffset decisionTime =
            SampleScenarios.BaselineUtc.AddMinutes(1);
        DateTimeOffset executionTime =
            SampleScenarios.BaselineUtc.AddMinutes(2);

        GovernanceDecision decision = evaluator.Evaluate(
            "decision-demo-execution",
            payment,
            risk,
            policy,
            decisionTime);

        AuthorityIssueResult issue = new ExecutionAuthorityIssuer().TryIssue(
            decision,
            policy,
            decisionTime);

        Console.WriteLine($"  Authority issuance: {issue.ReasonCode}");

        if (!issue.Issued || issue.Authority is not ExecutionAuthority authority)
        {
            Console.WriteLine("  Execution skipped because authority was not issued.");
            Console.WriteLine();
            return;
        }

        IExecutionAuthorityClaimStore claimStore =
            new InMemoryExecutionAuthorityClaimStore();
        RecordingPaymentExecutor executor = new();
        RiskExecutionGateway gateway = new(
            new ExecutionFreshnessEvaluator(),
            claimStore,
            executor);

        ExecutionResult first = await gateway.TryExecuteAsync(
            authority,
            payment,
            risk,
            policy,
            executionTime,
            CancellationToken.None);

        Console.WriteLine(
            $"  First presentation: {first.ReasonCode}; " +
            $"executed={first.Executed}; executorCalls={executor.InvocationCount}");
        Console.WriteLine(
            $"  Authority claims: {claimStore.GetClaimCount(authority.AuthorityId)}");

        ExecutionResult replay = await gateway.TryExecuteAsync(
            authority,
            payment,
            risk,
            policy,
            executionTime,
            CancellationToken.None);

        Console.WriteLine(
            $"  Replay presentation: {replay.ReasonCode}; " +
            $"executed={replay.Executed}; executorCalls={executor.InvocationCount}");
        Console.WriteLine(
            $"  Authority claims after replay: " +
            $"{claimStore.GetClaimCount(authority.AuthorityId)}");
        Console.WriteLine();
    }

    private static void Show(string title, GovernanceDecision decision)
    {
        Console.WriteLine(title);
        Console.WriteLine($"  Outcome: {decision.Outcome}");
        Console.WriteLine($"  Reason: {decision.ReasonCode}");
        Console.WriteLine($"  Policy: {decision.PolicyVersion}");
        Console.WriteLine($"  Threshold: {decision.ThresholdVersion}");
        Console.WriteLine(
            $"  Observation: {decision.RiskInput.Observation?.ObservationId ?? "unavailable"}");
        Console.WriteLine();
    }
}
