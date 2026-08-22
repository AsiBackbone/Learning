using Xunit;

namespace PolicySimulationHarness.Tests;

public sealed class PolicySimulationHarnessTests
{
    [Fact]
    public void SameIntentDifferentRegionChangesDecision()
    {
        PolicySimulationHarness harness = CreateHarness();

        SimulationReport report = harness.Simulate(
        [
            CreateScenario("us", region: "US"),
            CreateScenario("eu", region: "EU")
        ]);

        Assert.Equal(
            DecisionOutcome.Allowed,
            report.Get("us").Decision);
        Assert.Equal(
            DecisionOutcome.AcknowledgmentRequired,
            report.Get("eu").Decision);
        Assert.Equal(
            "customer-export.region-eu-acknowledgment",
            report.Get("eu").ReasonCode);
    }

    [Fact]
    public void SameIntentDifferentTenantChangesDecision()
    {
        PolicySimulationHarness harness = CreateHarness();

        SimulationReport report = harness.Simulate(
        [
            CreateScenario("standard", tenantId: "tenant-a"),
            CreateScenario(
                "regulated",
                tenantId: "tenant-regulated")
        ]);

        Assert.Equal(
            DecisionOutcome.Allowed,
            report.Get("standard").Decision);
        Assert.Equal(
            DecisionOutcome.EscalationRecommended,
            report.Get("regulated").Decision);
        Assert.Equal(
            "customer-export.tenant-regulated",
            report.Get("regulated").ReasonCode);
    }

    [Fact]
    public void SameIntentDifferentRiskChangesDecision()
    {
        PolicySimulationHarness harness = CreateHarness();

        SimulationReport report = harness.Simulate(
        [
            CreateScenario("low", risk: RiskLevel.Low),
            CreateScenario("high", risk: RiskLevel.High)
        ]);

        Assert.Equal(
            DecisionOutcome.Allowed,
            report.Get("low").Decision);
        Assert.Equal(
            DecisionOutcome.Denied,
            report.Get("high").Decision);
        Assert.Equal(
            "customer-export.risk-high",
            report.Get("high").ReasonCode);
    }

    [Fact]
    public void SameIntentDifferentPolicyVersionChangesDecision()
    {
        PolicySimulationHarness harness = CreateHarness();

        SimulationReport report = harness.Simulate(
        [
            CreateScenario(
                "v1",
                risk: RiskLevel.Medium,
                policyVersion: "1.0"),
            CreateScenario(
                "v2",
                risk: RiskLevel.Medium,
                policyVersion: "2.0")
        ]);

        Assert.Equal(
            DecisionOutcome.Allowed,
            report.Get("v1").Decision);
        Assert.Equal(
            DecisionOutcome.AcknowledgmentRequired,
            report.Get("v2").Decision);
        Assert.Equal(
            "customer-export.medium-risk-v2-acknowledgment",
            report.Get("v2").ReasonCode);
        Assert.Equal("1.0", report.Get("v1").PolicyVersion);
        Assert.Equal("2.0", report.Get("v2").PolicyVersion);
    }

    [Fact]
    public void DegradedEnvironmentDefersSimulation()
    {
        PolicySimulationHarness harness = CreateHarness();

        SimulationResult result = harness.Simulate(
        [
            CreateScenario(
                "degraded",
                environment: EnvironmentState.Degraded)
        ]).Get("degraded");

        Assert.Equal(DecisionOutcome.Deferred, result.Decision);
        Assert.Equal(
            "customer-export.environment-degraded",
            result.ReasonCode);
    }

    [Fact]
    public void StructuredResultPreservesPolicyAndConstraintEvidence()
    {
        PolicySimulationHarness harness = CreateHarness();

        SimulationResult result = harness.Simulate(
        [
            CreateScenario(
                "regulated-eu",
                region: "EU",
                tenantId: "tenant-regulated")
        ]).Get("regulated-eu");

        Assert.Equal(
            PolicyCatalog.DefaultPolicyId,
            result.PolicyId);
        Assert.Equal("1.0", result.PolicyVersion);
        Assert.Equal(
            DecisionOutcome.EscalationRecommended,
            result.Decision);
        Assert.Contains(
            result.ConstraintEvidence,
            item =>
                item.ConstraintId == "tenant.regulated" &&
                item.Outcome ==
                    DecisionOutcome.EscalationRecommended);
        Assert.Contains(
            result.ConstraintEvidence,
            item =>
                item.ConstraintId == "region.eu-export" &&
                item.Outcome ==
                    DecisionOutcome.AcknowledgmentRequired);
    }

    [Fact]
    public void UnknownPolicyVersionDefersWithExplicitEvidence()
    {
        PolicySimulationHarness harness = CreateHarness();

        SimulationResult result = harness.Simulate(
        [
            CreateScenario(
                "unknown-policy",
                policyVersion: "9.9")
        ]).Get("unknown-policy");

        Assert.Equal(DecisionOutcome.Deferred, result.Decision);
        Assert.Equal(
            "policy.version-unavailable",
            result.ReasonCode);
        Assert.Equal("9.9", result.PolicyVersion);
        Assert.Single(result.ConstraintEvidence);
        Assert.Equal(
            "policy.version.resolve",
            result.ConstraintEvidence[0].ConstraintId);
    }

    [Fact]
    public void SimulationDoesNotExposeExecutorDependencyOrAttemptExecution()
    {
        PolicySimulationHarness harness = CreateHarness();

        SimulationReport report = harness.Simulate(
        [
            CreateScenario("allowed"),
            CreateScenario(
                "denied",
                risk: RiskLevel.High)
        ]);

        Assert.All(
            report.Results,
            result => Assert.False(result.ExecutionAttempted));

        Assert.DoesNotContain(
            typeof(PolicySimulationHarness)
                .GetConstructors()
                .SelectMany(constructor =>
                    constructor.GetParameters()),
            parameter =>
                parameter.ParameterType.Name.Contains(
                    "Executor",
                    StringComparison.OrdinalIgnoreCase) ||
                (parameter.Name?.Contains(
                    "executor",
                    StringComparison.OrdinalIgnoreCase) ?? false));

        var simulateMethod =
            typeof(PolicySimulationHarness).GetMethod(
                nameof(PolicySimulationHarness.Simulate));

        Assert.NotNull(simulateMethod);
        Assert.DoesNotContain(
            simulateMethod!.GetParameters(),
            parameter =>
                parameter.ParameterType.Name.Contains(
                    "Executor",
                    StringComparison.OrdinalIgnoreCase) ||
                (parameter.Name?.Contains(
                    "executor",
                    StringComparison.OrdinalIgnoreCase) ?? false));
    }

    [Fact]
    public void SameSyntheticInputsProduceSameDecisionEvidence()
    {
        PolicySimulationHarness harness = CreateHarness();

        SimulationScenario[] scenarios =
        [
            CreateScenario("one", region: "EU"),
            CreateScenario(
                "two",
                risk: RiskLevel.Medium,
                policyVersion: "2.0")
        ];

        SimulationReport first = harness.Simulate(scenarios);
        SimulationReport second = harness.Simulate(scenarios);

        string[] firstProjection = first.Results
            .Select(Project)
            .ToArray();
        string[] secondProjection = second.Results
            .Select(Project)
            .ToArray();

        Assert.Equal(firstProjection, secondProjection);
    }

    private static string Project(SimulationResult result) =>
        string.Join(
            "|",
            result.ScenarioId,
            result.Decision,
            result.ReasonCode,
            result.PolicyId,
            result.PolicyVersion,
            string.Join(
                ",",
                result.ConstraintEvidence.Select(
                    item => item.ConstraintId)));

    private static PolicySimulationHarness CreateHarness() =>
        new(PolicyCatalog.CreateDefault());

    private static SimulationScenario CreateScenario(
        string scenarioId,
        string region = "US",
        string tenantId = "tenant-a",
        RiskLevel risk = RiskLevel.Low,
        EnvironmentState environment = EnvironmentState.Normal,
        string policyVersion = "1.0") =>
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
}
