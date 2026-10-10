using System.Security.Claims;

namespace AuthorizationToGovernanceComparison;

/// <summary>
/// Variant 1: ordinary endpoint authorization plus normal operational logging.
/// </summary>
/// <remarks>
/// The policy runs before the endpoint and sees only the caller, so it can express
/// "administrators only". It cannot see the account, so tenant and protected-account
/// rules are outside what this variant protects.
/// </remarks>
public sealed partial class EndpointAuthorizationVariant
{
    public const string PolicyName = "AdministratorsOnly";
    public const string Route = "/v1/accounts/{accountId}/disable";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost(Route, Handle)
            .RequireAuthorization(PolicyName);
    }

    private static IResult Handle(
        string accountId,
        ClaimsPrincipal user,
        AccountStore accounts,
        IAccountDisabler disabler,
        ILogger<EndpointAuthorizationVariant> logger)
    {
        if (accounts.Find(accountId) is null)
        {
            return Results.NotFound();
        }

        string actorId = user.ActorId();

        disabler.Disable(Variant.EndpointAuthorization, accountId);
        LogAccountDisabled(logger, accountId, actorId);

        return Results.NoContent();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Account {AccountId} disabled by {ActorId}")]
    private static partial void LogAccountDisabled(ILogger logger, string accountId, string actorId);
}
