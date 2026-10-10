using System.Collections.Concurrent;

namespace AuthorizationToGovernanceComparison;

public enum DirectoryState
{
    Current,
    SyncPending
}

/// <summary>
/// Authoritative account state. All three variants read the same records.
/// </summary>
public sealed record Account(
    string AccountId,
    string TenantId,
    bool IsProtected,
    bool HasActiveSessions,
    bool IsPrivileged,
    DirectoryState DirectoryState);

public sealed record DemoActor(
    string ActorId,
    string TenantId,
    string Role);

public static class Fixtures
{
    public const string AdministratorRole = "Administrator";

    public static IReadOnlyList<DemoActor> Actors { get; } =
    [
        new("admin-a", "tenant-a", AdministratorRole),
        new("support-a", "tenant-a", "Support"),
        new("admin-b", "tenant-b", AdministratorRole)
    ];

    public static IReadOnlyList<Account> Accounts { get; } =
    [
        new("acct-100", "tenant-a", IsProtected: false, HasActiveSessions: false, IsPrivileged: false,
            DirectoryState.Current),
        new("acct-200", "tenant-a", IsProtected: true, HasActiveSessions: false, IsPrivileged: false,
            DirectoryState.Current),
        new("acct-400", "tenant-a", IsProtected: false, HasActiveSessions: true, IsPrivileged: false,
            DirectoryState.Current),
        new("acct-500", "tenant-a", IsProtected: false, HasActiveSessions: false, IsPrivileged: true,
            DirectoryState.Current),
        new("acct-600", "tenant-a", IsProtected: false, HasActiveSessions: false, IsPrivileged: false,
            DirectoryState.SyncPending)
    ];

    public static DemoActor? FindActor(string actorId)
    {
        return Actors.FirstOrDefault(actor => string.Equals(actor.ActorId, actorId, StringComparison.Ordinal));
    }
}

public sealed class AccountStore
{
    private readonly ConcurrentDictionary<string, Account> _accounts = new(
        Fixtures.Accounts.Select(account => KeyValuePair.Create(account.AccountId, account)),
        StringComparer.Ordinal);

    public Account? Find(string accountId)
    {
        return _accounts.TryGetValue(accountId, out Account? account) ? account : null;
    }
}

/// <summary>
/// A fixed clock, so evidence timestamps and acknowledgment expiry are deterministic.
/// </summary>
public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public static DateTimeOffset SampleUtc { get; } = new(2026, 10, 12, 14, 0, 0, TimeSpan.Zero);

    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow()
    {
        return UtcNow;
    }
}
