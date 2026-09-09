namespace DistributedAcknowledgmentContinuation;

public sealed class InMemoryAcknowledgmentChallengeStore
    : IAcknowledgmentChallengeStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, AcknowledgmentChallenge> _challenges =
        new(StringComparer.Ordinal);

    public AcknowledgmentChallenge? Find(string challengeId)
    {
        lock (_sync)
        {
            return _challenges.TryGetValue(
                challengeId,
                out AcknowledgmentChallenge? challenge)
                ? challenge
                : null;
        }
    }

    public void Put(AcknowledgmentChallenge challenge)
    {
        lock (_sync)
        {
            _challenges[challenge.ChallengeId] = challenge;
        }
    }
}

public sealed class InMemoryContinuationStateStore
    : IContinuationStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, ContinuationState> _states =
        new(StringComparer.Ordinal);

    public ContinuationState? Find(string continuationId)
    {
        lock (_sync)
        {
            return _states.TryGetValue(
                continuationId,
                out ContinuationState? state)
                ? state
                : null;
        }
    }

    public void Put(ContinuationState state)
    {
        lock (_sync)
        {
            _states[state.ContinuationId] = state;
        }
    }
}

public sealed class InMemoryContinuationClaimStore
    : IContinuationClaimStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, string> _evidenceByChallenge =
        new(StringComparer.Ordinal);

    public ContinuationClaimResult TryClaim(
        string challengeId,
        string evidenceId)
    {
        lock (_sync)
        {
            if (_evidenceByChallenge.ContainsKey(challengeId))
            {
                return new ContinuationClaimResult(
                    false,
                    "continuation.already-claimed",
                    null);
            }

            _evidenceByChallenge.Add(challengeId, evidenceId);

            return new ContinuationClaimResult(
                true,
                "continuation.claimed",
                $"claim-{challengeId}");
        }
    }

    public int GetClaimCount(string challengeId)
    {
        lock (_sync)
        {
            return _evidenceByChallenge.ContainsKey(challengeId)
                ? 1
                : 0;
        }
    }
}
