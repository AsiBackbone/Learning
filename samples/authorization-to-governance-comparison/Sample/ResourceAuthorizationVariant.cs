using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace AuthorizationToGovernanceComparison;

public sealed record DisableAccountResource(
    string AccountId,
    string TenantId,
    bool IsProtected);

public sealed class DisableAccountRequirement : IAuthorizationRequirement;

/// <summary>
/// The resource-based handler from "When ASP.NET Core Authorization Is Enough". Its
/// failure messages are stable reason codes so they can be audited.
/// </summary>
public sealed class DisableAccountHandler
    : AuthorizationHandler<DisableAccountRequirement, DisableAccountResource>
{
    public const string RequiresAdministrator = "account.requires-administrator";
    public const string CrossTenant = "account.cross-tenant";
    public const string ProtectedAccount = "account.protected";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DisableAccountRequirement requirement,
        DisableAccountResource resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        if (!context.User.IsInRole(Fixtures.AdministratorRole))
        {
            context.Fail(new AuthorizationFailureReason(this, RequiresAdministrator));
            return Task.CompletedTask;
        }

        string? actorTenant = context.User.FindFirst(DemoAuthenticationHandler.TenantClaim)?.Value;

        if (!string.Equals(actorTenant, resource.TenantId, StringComparison.Ordinal))
        {
            context.Fail(new AuthorizationFailureReason(this, CrossTenant));
            return Task.CompletedTask;
        }

        if (resource.IsProtected)
        {
            context.Fail(new AuthorizationFailureReason(this, ProtectedAccount));
            return Task.CompletedTask;
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Variant 2: resource-based policy authorization through <see cref="IAuthorizationService"/>
/// plus a structured application audit entry.
/// </summary>
public static class ResourceAuthorizationVariant
{
    public const string PolicyName = "CanDisableAccount";
    public const string Route = "/v2/accounts/{accountId}/disable";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost(Route, HandleAsync)
            .RequireAuthorization();
    }

    public static async Task<AuthorizationResult> AuthorizeAsync(
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        Account account)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(account);

        return await authorization.AuthorizeAsync(
            user,
            new DisableAccountResource(account.AccountId, account.TenantId, account.IsProtected),
            PolicyName);
    }

    public static IReadOnlyList<string> FailureReasons(AuthorizationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return [.. result.Failure?.FailureReasons.Select(reason => reason.Message) ?? []];
    }

    private static async Task<IResult> HandleAsync(
        string accountId,
        ClaimsPrincipal user,
        AccountStore accounts,
        IAuthorizationService authorization,
        IAccountDisabler disabler,
        ApplicationAuditLog audit,
        TimeProvider clock)
    {
        if (accounts.Find(accountId) is not { } account)
        {
            return Results.NotFound();
        }

        AuthorizationResult result = await AuthorizeAsync(authorization, user, account);

        if (!result.Succeeded)
        {
            audit.Append(new(user.ActorId(), accountId, "account.disable", false, FailureReasons(result),
                clock.GetUtcNow()));
            return Results.Forbid();
        }

        disabler.Disable(Variant.ResourceAuthorization, accountId);
        audit.Append(new(user.ActorId(), accountId, "account.disable", true, [], clock.GetUtcNow()));

        return Results.NoContent();
    }
}
