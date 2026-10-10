using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace AuthorizationToGovernanceComparison;

public enum GovernanceOutcome
{
    Allowed,
    Denied,
    Deferred,
    AcknowledgmentRequired,
    EscalationRecommended
}

public sealed record GovernanceContext(
    Account Account,
    bool AccessGranted,
    string? AccessFailureReason,
    bool AcknowledgmentSatisfied);

public sealed record PolicyResult(
    GovernanceOutcome Outcome,
    string ReasonCode);

/// <summary>
/// A pure function of explicit facts. It consumes the ASP.NET Core authorization result
/// as one input rather than replacing it, and it never performs the side effect.
/// </summary>
public static class AccountDisablePolicy
{
    public const string Version = "account-disable/1.0";

    public static PolicyResult Evaluate(GovernanceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.AccessGranted)
        {
            return new(GovernanceOutcome.Denied, context.AccessFailureReason ?? "access.denied");
        }

        if (context.Account.DirectoryState == DirectoryState.SyncPending)
        {
            return new(GovernanceOutcome.Deferred, "account.directory-sync-pending");
        }

        if (context.Account.IsPrivileged)
        {
            return new(GovernanceOutcome.EscalationRecommended, "account.privileged-requires-security-review");
        }

        if (context.Account.HasActiveSessions)
        {
            return context.AcknowledgmentSatisfied
                ? new(GovernanceOutcome.Allowed, "account.sessions-acknowledged")
                : new(GovernanceOutcome.AcknowledgmentRequired, "account.active-sessions");
        }

        return new(GovernanceOutcome.Allowed, "account.disable-allowed");
    }
}

/// <summary>
/// The host-owned execution boundary. It executes only a recorded <c>Allowed</c>
/// decision, at most once, and records execution evidence separately from the decision.
/// </summary>
public sealed class GovernedExecutionBoundary(
    DecisionLog decisions,
    ExecutionLog executions,
    IAccountDisabler disabler,
    TimeProvider clock)
{
    public ExecutionRecord? Execute(string decisionId)
    {
        if (decisions.Find(decisionId) is not { Outcome: GovernanceOutcome.Allowed } decision)
        {
            return null;
        }

        ExecutionRecord record = new(
            ExecutionId: $"execution-for-{decision.DecisionId}",
            DecisionId: decision.DecisionId,
            AccountId: decision.AccountId,
            ExecutedAtUtc: clock.GetUtcNow());

        // Claim before acting, so a repeated call for the same decision cannot execute twice.
        if (!executions.TryRecord(record))
        {
            return null;
        }

        disabler.Disable(Variant.GovernedExecution, decision.AccountId);
        return record;
    }
}

public sealed record GovernedResponse(
    string DecisionId,
    GovernanceOutcome Outcome,
    string ReasonCode,
    string PolicyVersion,
    string? ExecutionId);

/// <summary>
/// Variant 3: an explicit governance decision, separate decision and execution evidence,
/// and a host-owned execution boundary.
/// </summary>
public static class GovernedExecutionVariant
{
    public const string Route = "/v3/accounts/{accountId}/disable";
    public const string Operation = "account.disable";

    private static readonly TimeSpan _acknowledgmentLifetime = TimeSpan.FromMinutes(10);

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost(Route, HandleAsync)
            .RequireAuthorization();
    }

    private static async Task<IResult> HandleAsync(
        string accountId,
        string? acknowledgedDecisionId,
        ClaimsPrincipal user,
        AccountStore accounts,
        IAuthorizationService authorization,
        DecisionLog decisions,
        GovernedExecutionBoundary boundary,
        TimeProvider clock)
    {
        if (accounts.Find(accountId) is not { } account)
        {
            return Results.NotFound();
        }

        string actorId = user.ActorId();
        DateTimeOffset now = clock.GetUtcNow();

        // Access control stays with ASP.NET Core; governance consumes its result.
        AuthorizationResult access = await ResourceAuthorizationVariant.AuthorizeAsync(authorization, user, account);

        bool acknowledged = IsAcknowledgmentValid(acknowledgedDecisionId, actorId, accountId, decisions, now);
        IReadOnlyList<string> accessFailures = ResourceAuthorizationVariant.FailureReasons(access);
        GovernanceContext context = new(
            account,
            access.Succeeded,
            accessFailures.Count > 0 ? accessFailures[0] : null,
            acknowledged);
        PolicyResult policy = AccountDisablePolicy.Evaluate(context);

        bool reliesOnAcknowledgment =
            acknowledged && policy.Outcome == GovernanceOutcome.Allowed && account.HasActiveSessions;

        if (reliesOnAcknowledgment && !decisions.TryConsumeAcknowledgment(acknowledgedDecisionId!))
        {
            // Another request already used this acknowledgment.
            reliesOnAcknowledgment = false;
            policy = AccountDisablePolicy.Evaluate(context with { AcknowledgmentSatisfied = false });
        }

        DecisionRecord decision = new(
            DecisionId: decisions.NextDecisionId(),
            ActorId: actorId,
            AccountId: accountId,
            Operation: Operation,
            Outcome: policy.Outcome,
            ReasonCode: policy.ReasonCode,
            PolicyVersion: AccountDisablePolicy.Version,
            DecidedAtUtc: now,
            AcknowledgedDecisionId: reliesOnAcknowledgment ? acknowledgedDecisionId : null);
        decisions.Record(decision);

        ExecutionRecord? execution = decision.Outcome == GovernanceOutcome.Allowed
            ? boundary.Execute(decision.DecisionId)
            : null;

        GovernedResponse body = new(
            decision.DecisionId,
            decision.Outcome,
            decision.ReasonCode,
            decision.PolicyVersion,
            execution?.ExecutionId);

        return Results.Json(body, statusCode: StatusCodeFor(decision.Outcome));
    }

    public static int StatusCodeFor(GovernanceOutcome outcome)
    {
        return outcome switch
        {
            GovernanceOutcome.Allowed => StatusCodes.Status200OK,
            GovernanceOutcome.Denied => StatusCodes.Status403Forbidden,
            GovernanceOutcome.Deferred => StatusCodes.Status503ServiceUnavailable,
            GovernanceOutcome.AcknowledgmentRequired => StatusCodes.Status202Accepted,
            GovernanceOutcome.EscalationRecommended => StatusCodes.Status202Accepted,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    private static bool IsAcknowledgmentValid(
        string? acknowledgedDecisionId,
        string actorId,
        string accountId,
        DecisionLog decisions,
        DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(acknowledgedDecisionId) ||
            decisions.Find(acknowledgedDecisionId) is not { } prior)
        {
            return false;
        }

        // The acknowledgment answers one earlier decision, for the same actor and account,
        // and only for a bounded time.
        return prior.Outcome == GovernanceOutcome.AcknowledgmentRequired &&
               string.Equals(prior.ActorId, actorId, StringComparison.Ordinal) &&
               string.Equals(prior.AccountId, accountId, StringComparison.Ordinal) &&
               now - prior.DecidedAtUtc <= _acknowledgmentLifetime;
    }
}
