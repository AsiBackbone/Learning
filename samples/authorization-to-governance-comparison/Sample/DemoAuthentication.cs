using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace AuthorizationToGovernanceComparison;

/// <summary>
/// DEMO-ONLY authentication. The caller names a fixture actor in a header and is trusted.
/// It exists so the comparison can focus on authorization and governance; never use a
/// scheme like this outside a sample or a test.
/// </summary>
public sealed class DemoAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DemoActorHeader";
    public const string HeaderName = "X-Demo-Actor";
    public const string TenantClaim = "tenant_id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out StringValues values) || values.Count != 1)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (Fixtures.FindActor(values[0]!) is not { } actor)
        {
            return Task.FromResult(AuthenticateResult.Fail("Unknown demo actor."));
        }

        ClaimsIdentity identity = new(
            [
                new Claim(ClaimTypes.NameIdentifier, actor.ActorId),
                new Claim(ClaimTypes.Role, actor.Role),
                new Claim(TenantClaim, actor.TenantId)
            ],
            SchemeName);

        return Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

public static class ClaimsPrincipalExtensions
{
    public static string ActorId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
    }
}
