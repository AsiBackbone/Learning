namespace FederatedGovernanceCoordination;

public static class Program
{
    public static void Main()
    {
        FederatedGovernanceService service =
            SampleScenarios.CreateService();

        Run(
            "all required authorities allow",
            service,
            SampleScenarios.CreateRequest(),
            SampleScenarios.CreateAllowedContributions());

        Run(
            "peer disagreement remains conflict",
            service,
            SampleScenarios.CreateRequest(),
            [
                SampleScenarios.CreateContribution(
                    "cedar-release",
                    AuthorityOutcome.Allow),
                SampleScenarios.CreateContribution(
                    "harbor-intake",
                    AuthorityOutcome.Deny)
            ]);

        Run(
            "required authority unavailable",
            service,
            SampleScenarios.CreateRequest(),
            [
                SampleScenarios.CreateContribution("cedar-release"),
                SampleScenarios.CreateContribution(
                    "harbor-intake",
                    outcome: null,
                    status: ContributionStatus.Unavailable)
            ]);

        Run(
            "coordinator unavailable for federated operation",
            service,
            SampleScenarios.CreateRequest(
                coordinatorAvailable: false),
            []);

        Run(
            "pre-classified local-only operation during coordinator outage",
            service,
            SampleScenarios.CreateRequest(
                resource: SampleScenarios.CreateLocalResource(),
                coordinatorAvailable: false,
                localPolicyAllows: true),
            []);

        Console.WriteLine();
        Console.WriteLine("Important limitations:");
        Console.WriteLine("- All policy contributions are fictional in-memory records.");
        Console.WriteLine("- No network, signatures, consensus, or protected side effects are modeled.");
        Console.WriteLine("- The sample demonstrates composition invariants, not a federation protocol.");
    }

    private static void Run(
        string name,
        FederatedGovernanceService service,
        EvaluationRequest request,
        IReadOnlyList<AuthorityContribution> contributions)
    {
        FederatedDecision decision = service.Evaluate(
            request,
            contributions);

        Console.WriteLine($"Scenario: {name}");
        Console.WriteLine($"Outcome: {decision.Outcome}");
        Console.WriteLine($"Reason: {decision.ReasonCode}");
        Console.WriteLine($"Authority set: {decision.AuthoritySetId}");
        Console.WriteLine();
    }
}
