namespace AdaptiveRiskContext;

public interface IExecutionAuthorityClaimStore
{
    bool TryClaim(string authorityId);

    int GetClaimCount(string authorityId);
}

public sealed class InMemoryExecutionAuthorityClaimStore
    : IExecutionAuthorityClaimStore
{
    private readonly object _sync = new();
    private readonly HashSet<string> _claimedAuthorityIds =
        new(StringComparer.Ordinal);

    public bool TryClaim(string authorityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityId);

        lock (_sync)
        {
            return _claimedAuthorityIds.Add(authorityId);
        }
    }

    public int GetClaimCount(string authorityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityId);

        lock (_sync)
        {
            return _claimedAuthorityIds.Contains(authorityId) ? 1 : 0;
        }
    }
}
