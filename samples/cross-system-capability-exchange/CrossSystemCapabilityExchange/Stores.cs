namespace CrossSystemCapabilityExchange;

public interface IIssuerTrustStore
{
    RecipientIssuerPolicy? Find(string issuer);
}

public sealed class InMemoryIssuerTrustStore(
    IReadOnlyList<RecipientIssuerPolicy> policies)
    : IIssuerTrustStore
{
    public RecipientIssuerPolicy? Find(string issuer) =>
        policies.FirstOrDefault(
            policy => string.Equals(
                policy.Issuer,
                issuer,
                StringComparison.Ordinal));
}

public interface IRevocationStore
{
    bool IsRevoked(string capabilityId);
}

public sealed class InMemoryRevocationStore : IRevocationStore
{
    private readonly HashSet<string> _revoked =
        new(StringComparer.Ordinal);

    public bool IsRevoked(string capabilityId)
    {
        lock (_revoked)
        {
            return _revoked.Contains(capabilityId);
        }
    }

    public void Revoke(string capabilityId)
    {
        lock (_revoked)
        {
            _revoked.Add(capabilityId);
        }
    }
}

public interface ICapabilityUseStore
{
    Task<CapabilityClaimResult> TryClaimAsync(
        string capabilityId,
        int maxUses,
        CancellationToken cancellationToken);
}

public sealed class InMemoryCapabilityUseStore : ICapabilityUseStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _useCounts =
        new(StringComparer.Ordinal);

    public Task<CapabilityClaimResult> TryClaimAsync(
        string capabilityId,
        int maxUses,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _useCounts.TryGetValue(
                capabilityId,
                out int currentUses);

            if (currentUses >= maxUses)
            {
                return Task.FromResult(
                    new CapabilityClaimResult(
                        false,
                        "claim.replayed"));
            }

            _useCounts[capabilityId] = currentUses + 1;

            return Task.FromResult(
                new CapabilityClaimResult(
                    true,
                    "claim.accepted"));
        }
    }

    public int GetUseCount(string capabilityId)
    {
        lock (_gate)
        {
            return _useCounts.GetValueOrDefault(capabilityId);
        }
    }
}

public sealed class UnavailableCapabilityUseStore : ICapabilityUseStore
{
    public Task<CapabilityClaimResult> TryClaimAsync(
        string capabilityId,
        int maxUses,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            new CapabilityClaimResult(
                false,
                "claim.store-unavailable"));
    }
}
