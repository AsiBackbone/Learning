namespace DecisionExplainability;

public static class Program
{
    public static void Main()
    {
        ExplanationProjector projector = new();

        Console.WriteLine("Decision Explainability for Human Operators");
        Console.WriteLine();

        Show(
            "End-user regional denial",
            projector.Project(
                SampleScenarios.RegionalResidencyDenial(),
                ExplanationAudience.EndUser));

        Show(
            "Operator regional denial",
            projector.Project(
                SampleScenarios.RegionalResidencyDenial(),
                ExplanationAudience.Operator));

        Show(
            "Deferred current-context outcome",
            projector.Project(
                SampleScenarios.DeferredContextUnavailable(),
                ExplanationAudience.EndUser));

        Show(
            "Multiple contributing reasons",
            projector.Project(
                SampleScenarios.MultiReasonDenial(),
                ExplanationAudience.Operator));

        Console.WriteLine("Teaching boundary:");
        Console.WriteLine("- explanation is derived from structured decision evidence");
        Console.WriteLine("- protected source context is not copied into the projection");
        Console.WriteLine("- explanation text is never parsed as policy or execution authority");
        Console.WriteLine("- no policy engine, external service, or executor is invoked");
    }

    private static void Show(
        string title,
        ExplanationProjection projection)
    {
        Console.WriteLine(title);
        Console.WriteLine($"  Decision: {projection.DecisionId}");
        Console.WriteLine($"  Outcome: {projection.Outcome}");
        Console.WriteLine($"  Audience: {projection.Audience}");
        Console.WriteLine($"  Headline: {projection.Headline}");

        foreach (string detail in projection.Details)
        {
            Console.WriteLine($"  Detail: {detail}");
        }

        Console.WriteLine($"  Disclosure: {projection.DisclosureStatus}");

        if (projection.DisclosureNotice is not null)
        {
            Console.WriteLine($"  Notice: {projection.DisclosureNotice}");
        }

        Console.WriteLine();
    }
}
