using System.Diagnostics;

namespace CentralizedErrorHandlingAndProblemDetails;

public enum GovernanceDecisionOutcome
{
    Allowed,
    Denied,
    Deferred,
    AcknowledgmentRequired,
    EscalationRecommended
}

public sealed record GovernanceDecision(
    GovernanceDecisionOutcome Outcome,
    string Code,
    string PublicDetail)
{
    public static bool TryFromScenario(
        string scenario,
        out GovernanceDecision decision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario);

        GovernanceDecision? candidate = scenario.ToLowerInvariant() switch
        {
            "allowed" => new GovernanceDecision(
                GovernanceDecisionOutcome.Allowed,
                "governance.allowed",
                "The operation may continue."),

            "denied" => new GovernanceDecision(
                GovernanceDecisionOutcome.Denied,
                "governance.denied",
                "The operation is not permitted by the active policy."),

            "deferred" => new GovernanceDecision(
                GovernanceDecisionOutcome.Deferred,
                "governance.deferred",
                "The operation is temporarily deferred."),

            "acknowledgment-required" => new GovernanceDecision(
                GovernanceDecisionOutcome.AcknowledgmentRequired,
                "governance.acknowledgment-required",
                "Explicit acknowledgment is required before the workflow can continue."),

            "escalation-recommended" => new GovernanceDecision(
                GovernanceDecisionOutcome.EscalationRecommended,
                "governance.escalation-recommended",
                "The operation requires a separate review workflow."),

            _ => null
        };

        if (candidate is null)
        {
            decision = null!;
            return false;
        }

        decision = candidate;
        return true;
    }
}

public static class GovernanceHttpMapper
{
    public static IResult ToHttpResult(
        GovernanceDecision decision,
        HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(httpContext);

        return decision.Outcome switch
        {
            GovernanceDecisionOutcome.Allowed =>
                Results.NoContent(),

            GovernanceDecisionOutcome.Denied =>
                ToProblem(
                    decision,
                    httpContext,
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    "/problems/governance-denied"),

            GovernanceDecisionOutcome.Deferred =>
                ToProblem(
                    decision,
                    httpContext,
                    StatusCodes.Status503ServiceUnavailable,
                    "Service Unavailable",
                    "/problems/governance-deferred"),

            GovernanceDecisionOutcome.AcknowledgmentRequired =>
                ToProblem(
                    decision,
                    httpContext,
                    StatusCodes.Status409Conflict,
                    "Acknowledgment Required",
                    "/problems/acknowledgment-required"),

            GovernanceDecisionOutcome.EscalationRecommended =>
                ToProblem(
                    decision,
                    httpContext,
                    StatusCodes.Status409Conflict,
                    "Escalation Recommended",
                    "/problems/escalation-recommended"),

            _ => throw new InvalidOperationException(
                "The governance outcome has no HTTP mapping.")
        };
    }

    private static IResult ToProblem(
        GovernanceDecision decision,
        HttpContext httpContext,
        int statusCode,
        string title,
        string type)
    {
        string traceId =
            Activity.Current?.TraceId.ToString()
            ?? httpContext.TraceIdentifier;

        return Results.Problem(
            statusCode: statusCode,
            title: title,
            type: type,
            detail: decision.PublicDetail,
            instance: httpContext.Request.Path.Value,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = decision.Code,
                ["traceId"] = traceId
            });
    }
}
