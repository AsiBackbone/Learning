DateTimeOffset simulatedUtc =
    new(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);

PolicySimulationHarness harness =
    new(PolicyCatalog.CreateDefault());

SimulationScenario[] scenarios =
[
    CreateScenario(
        "baseline-us-low-risk",
        region: "US",
        tenantId: "tenant-a",
        risk: RiskLevel.Low,
        environment: EnvironmentState.Normal,
        policyVersion: "1.0"),
    CreateScenario(
        "same-intent-eu-region",
        region: "EU",
        tenantId: "tenant-a",
        risk: RiskLevel.Low,
        environment: EnvironmentState.Normal,
        policyVersion: "1.0"),
    CreateScenario(
        "same-intent-regulated-tenant",
        region: "US",
        tenantId: "tenant-regulated",
        risk: RiskLevel.Low,
        environment: EnvironmentState.Normal,
        policyVersion: "1.0"),
    CreateScenario(
        "same-intent-high-risk",
        region: "US",
        tenantId: "tenant-a",
        risk: RiskLevel.High,
        environment: EnvironmentState.Normal,
        policyVersion: "1.0"),
    CreateScenario(
        "same-intent-degraded-environment",
        region: "US",
        tenantId: "tenant-a",
        risk: RiskLevel.Low,
        environment: EnvironmentState.Degraded,
        policyVersion: "1.0"),
    CreateScenario(
        "same-intent-medium-risk-v1",
        region: "US",
        tenantId: "tenant-a",
        risk: RiskLevel.Medium,
        environment: EnvironmentState.Normal,
        policyVersion: "1.0"),
    CreateScenario(
        "same-intent-medium-risk-v2",
        region: "US",
        tenantId: "tenant-a",
        risk: RiskLevel.Medium,
        environment: EnvironmentState.Normal,
        policyVersion: "2.0")
];

SimulationReport report = harness.Simulate(scenarios);

Console.WriteLine("Minimal Policy Simulation Harness");
Console.WriteLine(new string('=', 33));
Console.WriteLine();
Console.WriteLine(
    "Simulation evaluates decisions only. It does not own or invoke an executor.");
Console.WriteLine($"Synthetic evaluation time: {simulatedUtc:O}");
Console.WriteLine();

foreach (SimulationResult result in report.Results)
{
    Console.WriteLine($"Scenario: {result.ScenarioId}");
    Console.WriteLine($"Decision: {result.Decision}");
    Console.WriteLine($"Reason: {result.ReasonCode}");
    Console.WriteLine(
        $"Policy: {result.PolicyId}@{result.PolicyVersion}");
    Console.WriteLine(
        $"Matched constraints: {string.Join(", ", result.ConstraintEvidence.Select(
            item => item.ConstraintId))}");
    Console.WriteLine(
        $"Execution attempted: {result.ExecutionAttempted}");
    Console.WriteLine();
}

Console.WriteLine("Comparison highlights:");
PrintComparison(
    report,
    "baseline-us-low-risk",
    "same-intent-eu-region",
    "same intent + different region");
PrintComparison(
    report,
    "baseline-us-low-risk",
    "same-intent-regulated-tenant",
    "same intent + different tenant");
PrintComparison(
    report,
    "baseline-us-low-risk",
    "same-intent-high-risk",
    "same intent + different risk");
PrintComparison(
    report,
    "same-intent-medium-risk-v1",
    "same-intent-medium-risk-v2",
    "same intent + different policy version");

static SimulationScenario CreateScenario(
    string scenarioId,
    string region,
    string tenantId,
    RiskLevel risk,
    EnvironmentState environment,
    string policyVersion) =>
    new(
        ScenarioId: scenarioId,
        ActorId: "analyst-7",
        ResourceId: "customer-batch-42",
        OperationName: "customer.export",
        Region: region,
        TenantId: tenantId,
        Risk: risk,
        Environment: environment,
        PolicyVersion: policyVersion);

static void PrintComparison(
    SimulationReport report,
    string leftScenarioId,
    string rightScenarioId,
    string label)
{
    SimulationResult left = report.Get(leftScenarioId);
    SimulationResult right = report.Get(rightScenarioId);

    Console.WriteLine(
        $"- {label}: {left.Decision} -> {right.Decision}");
}

public enum RiskLevel
{
    Low,
    Medium,
    High
}

public enum EnvironmentState
{
    Normal,
    Degraded
}

public enum DecisionOutcome
{
    Allowed,
    Denied,
    Deferred,
    AcknowledgmentRequired,
    EscalationRecommended
}

public sealed record SimulationScenario(
    string ScenarioId,
    string ActorId,
    string ResourceId,
    string OperationName,
    string Region,
    string TenantId,
    RiskLevel Risk,
    EnvironmentState Environment,
    string PolicyVersion);

public sealed record PolicyDefinition(
    string PolicyId,
    string PolicyVersion,
    IReadOnlySet<string> AcknowledgmentRegions,
    IReadOnlySet<string> EscalationTenants,
    bool RequireAcknowledgmentForMediumRisk);

public sealed class PolicyCatalog
{
    public const string DefaultPolicyId = "customer-export";

    private readonly IReadOnlyDictionary<string, PolicyDefinition> _definitions;

    public PolicyCatalog(IEnumerable<PolicyDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(
            definition => definition.PolicyVersion,
            StringComparer.Ordinal);
    }

    public PolicyDefinition? Resolve(string policyVersion)
    {
        return _definitions.TryGetValue(
            policyVersion,
            out PolicyDefinition? definition)
            ? definition
            : null;
    }

    public static PolicyCatalog CreateDefault() =>
        new(
        [
            new PolicyDefinition(
                PolicyId: DefaultPolicyId,
                PolicyVersion: "1.0",
                AcknowledgmentRegions: new HashSet<string>(
                    ["EU"],
                    StringComparer.Ordinal),
                EscalationTenants: new HashSet<string>(
                    ["tenant-regulated"],
                    StringComparer.Ordinal),
                RequireAcknowledgmentForMediumRisk: false),
            new PolicyDefinition(
                PolicyId: DefaultPolicyId,
                PolicyVersion: "2.0",
                AcknowledgmentRegions: new HashSet<string>(
                    ["EU"],
                    StringComparer.Ordinal),
                EscalationTenants: new HashSet<string>(
                    ["tenant-regulated"],
                    StringComparer.Ordinal),
                RequireAcknowledgmentForMediumRisk: true)
        ]);
}

public sealed record ConstraintObservation(
    string ConstraintId,
    DecisionOutcome Outcome,
    string ReasonCode);

public sealed record SimulationResult(
    string ScenarioId,
    DecisionOutcome Decision,
    string ReasonCode,
    string PolicyId,
    string PolicyVersion,
    IReadOnlyList<ConstraintObservation> ConstraintEvidence,
    bool ExecutionAttempted);

public sealed record SimulationReport(
    IReadOnlyList<SimulationResult> Results)
{
    public SimulationResult Get(string scenarioId) =>
        Results.Single(
            result => string.Equals(
                result.ScenarioId,
                scenarioId,
                StringComparison.Ordinal));
}

public sealed class PolicySimulationHarness
{
    private readonly PolicyCatalog _catalog;
    private readonly PolicyConstraintEvaluator _evaluator = new();
    private readonly PolicyDecisionComposer _composer = new();

    public PolicySimulationHarness(PolicyCatalog catalog)
    {
        _catalog = catalog;
    }

    public SimulationReport Simulate(
        IEnumerable<SimulationScenario> scenarios)
    {
        List<SimulationResult> results = [];

        foreach (SimulationScenario scenario in scenarios)
        {
            PolicyDefinition? definition =
                _catalog.Resolve(scenario.PolicyVersion);

            if (definition is null)
            {
                results.Add(
                    new SimulationResult(
                        ScenarioId: scenario.ScenarioId,
                        Decision: DecisionOutcome.Deferred,
                        ReasonCode: "policy.version-unavailable",
                        PolicyId: PolicyCatalog.DefaultPolicyId,
                        PolicyVersion: scenario.PolicyVersion,
                        ConstraintEvidence:
                        [
                            new ConstraintObservation(
                                "policy.version.resolve",
                                DecisionOutcome.Deferred,
                                "policy.version-unavailable")
                        ],
                        ExecutionAttempted: false));

                continue;
            }

            IReadOnlyList<ConstraintObservation> observations =
                _evaluator.Evaluate(scenario, definition);

            ConstraintObservation winner =
                _composer.Compose(observations);

            results.Add(
                new SimulationResult(
                    ScenarioId: scenario.ScenarioId,
                    Decision: winner.Outcome,
                    ReasonCode: winner.ReasonCode,
                    PolicyId: definition.PolicyId,
                    PolicyVersion: definition.PolicyVersion,
                    ConstraintEvidence: observations,
                    ExecutionAttempted: false));
        }

        return new SimulationReport(results);
    }
}

public sealed class PolicyConstraintEvaluator
{
    public IReadOnlyList<ConstraintObservation> Evaluate(
        SimulationScenario scenario,
        PolicyDefinition policy)
    {
        List<ConstraintObservation> observations = [];

        if (!string.Equals(
                scenario.OperationName,
                "customer.export",
                StringComparison.Ordinal))
        {
            observations.Add(
                new ConstraintObservation(
                    "operation.supported",
                    DecisionOutcome.Denied,
                    "customer-export.operation-unsupported"));
        }

        if (scenario.Risk == RiskLevel.High)
        {
            observations.Add(
                new ConstraintObservation(
                    "risk.high",
                    DecisionOutcome.Denied,
                    "customer-export.risk-high"));
        }

        if (scenario.Environment == EnvironmentState.Degraded)
        {
            observations.Add(
                new ConstraintObservation(
                    "environment.degraded",
                    DecisionOutcome.Deferred,
                    "customer-export.environment-degraded"));
        }

        if (policy.EscalationTenants.Contains(scenario.TenantId))
        {
            observations.Add(
                new ConstraintObservation(
                    "tenant.regulated",
                    DecisionOutcome.EscalationRecommended,
                    "customer-export.tenant-regulated"));
        }

        if (policy.AcknowledgmentRegions.Contains(scenario.Region))
        {
            observations.Add(
                new ConstraintObservation(
                    "region.eu-export",
                    DecisionOutcome.AcknowledgmentRequired,
                    "customer-export.region-eu-acknowledgment"));
        }

        if (scenario.Risk == RiskLevel.Medium &&
            policy.RequireAcknowledgmentForMediumRisk)
        {
            observations.Add(
                new ConstraintObservation(
                    "risk.medium-v2",
                    DecisionOutcome.AcknowledgmentRequired,
                    "customer-export.medium-risk-v2-acknowledgment"));
        }

        if (observations.Count == 0)
        {
            observations.Add(
                new ConstraintObservation(
                    "baseline.allow",
                    DecisionOutcome.Allowed,
                    "customer-export.allowed"));
        }

        return observations;
    }
}

public sealed class PolicyDecisionComposer
{
    private static readonly IReadOnlyDictionary<DecisionOutcome, int> Precedence =
        new Dictionary<DecisionOutcome, int>
        {
            [DecisionOutcome.Allowed] = 100,
            [DecisionOutcome.AcknowledgmentRequired] = 200,
            [DecisionOutcome.EscalationRecommended] = 300,
            [DecisionOutcome.Deferred] = 400,
            [DecisionOutcome.Denied] = 500
        };

    public ConstraintObservation Compose(
        IReadOnlyList<ConstraintObservation> observations)
    {
        if (observations.Count == 0)
        {
            throw new ArgumentException(
                "At least one constraint observation is required.",
                nameof(observations));
        }

        return observations
            .OrderByDescending(
                observation => Precedence[observation.Outcome])
            .ThenBy(
                observation => observation.ConstraintId,
                StringComparer.Ordinal)
            .First();
    }
}
