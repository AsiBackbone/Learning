namespace DecisionPipelineRefactoring;

public enum DecisionOutcome
{
    Allowed,
    Denied,
    Deferred,
    AcknowledgmentRequired,
    EscalationRecommended
}

public sealed record GovernanceDecision(
    DecisionOutcome Outcome,
    string ReasonCode)
{
    public bool CanExecute => Outcome == DecisionOutcome.Allowed;

    public static GovernanceDecision Allow() =>
        new(DecisionOutcome.Allowed, "account.disable.allowed");

    public static GovernanceDecision Deny(string reasonCode) =>
        new(DecisionOutcome.Denied, reasonCode);

    public static GovernanceDecision Defer(string reasonCode) =>
        new(DecisionOutcome.Deferred, reasonCode);

    public static GovernanceDecision RequireAcknowledgment(string reasonCode) =>
        new(DecisionOutcome.AcknowledgmentRequired, reasonCode);

    public static GovernanceDecision Escalate(string reasonCode) =>
        new(DecisionOutcome.EscalationRecommended, reasonCode);
}

public sealed record AccountDisableRequest(
    string CorrelationId,
    string ActorId,
    string AccountId,
    bool RequesterIsAdministrator,
    bool AcknowledgmentSatisfied);

public sealed record AccountSnapshot(
    string AccountId,
    string TenantId,
    bool IsProtected,
    bool PendingInvestigation,
    bool RequiresManualReview,
    bool IsDisabled,
    int Version);

public sealed record AccountDisableContext(
    string CorrelationId,
    string ActorId,
    string Operation,
    bool RequesterIsAdministrator,
    bool AcknowledgmentSatisfied,
    AccountSnapshot Account);

public interface IAccountRepository
{
    AccountSnapshot GetRequired(string accountId);

    void Disable(string accountId);
}

public sealed class InMemoryAccountRepository(params AccountSnapshot[] accounts)
    : IAccountRepository
{
    private readonly Dictionary<string, AccountSnapshot> _accounts =
        accounts.ToDictionary(account => account.AccountId, StringComparer.Ordinal);

    public int DisableCount { get; private set; }

    public AccountSnapshot GetRequired(string accountId)
    {
        if (!_accounts.TryGetValue(accountId, out AccountSnapshot? account))
        {
            throw new KeyNotFoundException($"Unknown account '{accountId}'.");
        }

        return account;
    }

    public void Disable(string accountId)
    {
        AccountSnapshot current = GetRequired(accountId);

        _accounts[accountId] = current with
        {
            IsDisabled = true,
            Version = current.Version + 1
        };

        DisableCount++;
    }
}

public interface INotificationGateway
{
    void SendAccountDisabled(string accountId);
}

public sealed class RecordingNotificationGateway : INotificationGateway
{
    public int SendCount { get; private set; }

    public void SendAccountDisabled(string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        SendCount++;
    }
}

public interface IAccountEventPublisher
{
    void PublishAccountDisabled(string accountId);
}

public sealed class RecordingAccountEventPublisher : IAccountEventPublisher
{
    public int PublishCount { get; private set; }

    public void PublishAccountDisabled(string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        PublishCount++;
    }
}

public sealed class ScatteredAccountDisableService(
    IAccountRepository repository,
    INotificationGateway notifications,
    IAccountEventPublisher events)
{
    public GovernanceDecision Handle(AccountDisableRequest request)
    {
        // A role check happens before authoritative resource state is loaded.
        if (!request.RequesterIsAdministrator)
        {
            return GovernanceDecision.Deny("account.disable.requester-not-administrator");
        }

        AccountSnapshot account = repository.GetRequired(request.AccountId);

        // Consequential mutation begins before the service has completed its decision.
        if (!account.IsDisabled)
        {
            repository.Disable(account.AccountId);

            // A governance decision is embedded inside the mutation branch.
            if (account.PendingInvestigation)
            {
                return GovernanceDecision.Defer("account.disable.investigation-pending");
            }
        }

        // An external call also occurs before later policy checks.
        notifications.SendAccountDisabled(account.AccountId);

        if (account.IsProtected)
        {
            return GovernanceDecision.Deny("account.disable.protected-account");
        }

        try
        {
            if (account.RequiresManualReview)
            {
                throw new ManualReviewRequiredException();
            }
        }
        catch (ManualReviewRequiredException)
        {
            // A governance outcome is manufactured inside exception handling.
            return GovernanceDecision.Escalate("account.disable.manual-review-required");
        }

        // A continuation requirement is checked only after mutation and notification.
        if (!request.AcknowledgmentSatisfied)
        {
            return GovernanceDecision.RequireAcknowledgment(
                "account.disable.acknowledgment-required");
        }

        // The last governance check sits immediately before event publication.
        events.PublishAccountDisabled(account.AccountId);

        return GovernanceDecision.Allow();
    }

    private sealed class ManualReviewRequiredException : Exception
    {
    }
}

public sealed class AccountDisableContextBuilder(IAccountRepository repository)
{
    public AccountDisableContext Build(AccountDisableRequest request)
    {
        AccountSnapshot account = repository.GetRequired(request.AccountId);

        return new AccountDisableContext(
            request.CorrelationId,
            request.ActorId,
            "account.disable",
            request.RequesterIsAdministrator,
            request.AcknowledgmentSatisfied,
            account);
    }
}

public sealed class AccountDisablePolicy
{
    public GovernanceDecision Evaluate(AccountDisableContext context)
    {
        if (!context.RequesterIsAdministrator)
        {
            return GovernanceDecision.Deny("account.disable.requester-not-administrator");
        }

        if (context.Account.IsProtected)
        {
            return GovernanceDecision.Deny("account.disable.protected-account");
        }

        if (context.Account.PendingInvestigation)
        {
            return GovernanceDecision.Defer("account.disable.investigation-pending");
        }

        if (context.Account.RequiresManualReview)
        {
            return GovernanceDecision.Escalate("account.disable.manual-review-required");
        }

        if (!context.AcknowledgmentSatisfied)
        {
            return GovernanceDecision.RequireAcknowledgment(
                "account.disable.acknowledgment-required");
        }

        return GovernanceDecision.Allow();
    }
}

public interface IAccountDisableExecutor
{
    int InvocationCount { get; }

    void Execute(AccountDisableContext context);
}

public sealed class RecordingAccountDisableExecutor(
    IAccountRepository repository,
    INotificationGateway notifications,
    IAccountEventPublisher events)
    : IAccountDisableExecutor
{
    public int InvocationCount { get; private set; }

    public void Execute(AccountDisableContext context)
    {
        InvocationCount++;
        repository.Disable(context.Account.AccountId);
        notifications.SendAccountDisabled(context.Account.AccountId);
        events.PublishAccountDisabled(context.Account.AccountId);
    }
}

public sealed record DecisionEvidenceRecord(
    string Stage,
    string CorrelationId,
    string ActorId,
    string AccountId,
    int ResourceVersion,
    DecisionOutcome Outcome,
    string ReasonCode);

public interface IDecisionEvidenceSink
{
    void Append(DecisionEvidenceRecord record);
}

public sealed class RecordingDecisionEvidenceSink : IDecisionEvidenceSink
{
    private readonly List<DecisionEvidenceRecord> _records = [];

    public IReadOnlyList<DecisionEvidenceRecord> Records => _records;

    public void Append(DecisionEvidenceRecord record) => _records.Add(record);
}

public sealed class AccountDisableDecisionPipeline(
    AccountDisableContextBuilder contextBuilder,
    AccountDisablePolicy policy,
    IAccountDisableExecutor executor,
    IDecisionEvidenceSink evidence)
{
    public GovernanceDecision Handle(AccountDisableRequest request)
    {
        AccountDisableContext context = contextBuilder.Build(request);
        GovernanceDecision decision = policy.Evaluate(context);

        evidence.Append(new DecisionEvidenceRecord(
            "decision",
            context.CorrelationId,
            context.ActorId,
            context.Account.AccountId,
            context.Account.Version,
            decision.Outcome,
            decision.ReasonCode));

        if (!decision.CanExecute)
        {
            return decision;
        }

        executor.Execute(context);

        evidence.Append(new DecisionEvidenceRecord(
            "execution",
            context.CorrelationId,
            context.ActorId,
            context.Account.AccountId,
            context.Account.Version,
            decision.Outcome,
            decision.ReasonCode));

        return decision;
    }
}

public static class SampleData
{
    public static InMemoryAccountRepository CreateRepository() =>
        new(
            new AccountSnapshot(
                "acct-protected",
                "tenant-a",
                IsProtected: true,
                PendingInvestigation: false,
                RequiresManualReview: false,
                IsDisabled: false,
                Version: 7),
            new AccountSnapshot(
                "acct-pending",
                "tenant-a",
                IsProtected: false,
                PendingInvestigation: true,
                RequiresManualReview: false,
                IsDisabled: false,
                Version: 4),
            new AccountSnapshot(
                "acct-manual",
                "tenant-b",
                IsProtected: false,
                PendingInvestigation: false,
                RequiresManualReview: true,
                IsDisabled: false,
                Version: 11),
            new AccountSnapshot(
                "acct-standard",
                "tenant-b",
                IsProtected: false,
                PendingInvestigation: false,
                RequiresManualReview: false,
                IsDisabled: false,
                Version: 3));
}

public static class Program
{
    public static void Main()
    {
        Console.WriteLine("Decision pipeline refactoring sample");
        Console.WriteLine();
        DemonstrateScatteredFailure();
        Console.WriteLine();
        DemonstrateRefactoredPipeline();
    }

    private static void DemonstrateScatteredFailure()
    {
        InMemoryAccountRepository repository = SampleData.CreateRepository();
        var notifications = new RecordingNotificationGateway();
        var events = new RecordingAccountEventPublisher();
        var service = new ScatteredAccountDisableService(
            repository,
            notifications,
            events);

        GovernanceDecision decision = service.Handle(new AccountDisableRequest(
            CorrelationId: "corr-starter",
            ActorId: "admin-17",
            AccountId: "acct-protected",
            RequesterIsAdministrator: true,
            AcknowledgmentSatisfied: true));

        Console.WriteLine("Intentionally flawed scattered service:");
        Console.WriteLine($"  decision: {decision.Outcome} ({decision.ReasonCode})");
        Console.WriteLine($"  account mutations before denial: {repository.DisableCount}");
        Console.WriteLine($"  notifications before denial: {notifications.SendCount}");
        Console.WriteLine($"  events published: {events.PublishCount}");
    }

    private static void DemonstrateRefactoredPipeline()
    {
        Console.WriteLine("Refactored decision pipeline:");

        RunPipelineScenario(
            "Denied",
            accountId: "acct-protected",
            isAdministrator: true,
            acknowledgmentSatisfied: true);

        RunPipelineScenario(
            "Deferred",
            accountId: "acct-pending",
            isAdministrator: true,
            acknowledgmentSatisfied: true);

        RunPipelineScenario(
            "AcknowledgmentRequired",
            accountId: "acct-standard",
            isAdministrator: true,
            acknowledgmentSatisfied: false);

        RunPipelineScenario(
            "EscalationRecommended",
            accountId: "acct-manual",
            isAdministrator: true,
            acknowledgmentSatisfied: true);

        RunPipelineScenario(
            "Allowed",
            accountId: "acct-standard",
            isAdministrator: true,
            acknowledgmentSatisfied: true);
    }

    private static void RunPipelineScenario(
        string label,
        string accountId,
        bool isAdministrator,
        bool acknowledgmentSatisfied)
    {
        InMemoryAccountRepository repository = SampleData.CreateRepository();
        var notifications = new RecordingNotificationGateway();
        var events = new RecordingAccountEventPublisher();
        var executor = new RecordingAccountDisableExecutor(
            repository,
            notifications,
            events);
        var evidence = new RecordingDecisionEvidenceSink();
        var pipeline = new AccountDisableDecisionPipeline(
            new AccountDisableContextBuilder(repository),
            new AccountDisablePolicy(),
            executor,
            evidence);

        GovernanceDecision decision = pipeline.Handle(new AccountDisableRequest(
            CorrelationId: $"corr-{label.ToLowerInvariant()}",
            ActorId: "admin-17",
            AccountId: accountId,
            RequesterIsAdministrator: isAdministrator,
            AcknowledgmentSatisfied: acknowledgmentSatisfied));

        Console.WriteLine(
            $"  {label,-26} outcome={decision.Outcome,-26} " +
            $"executorCalls={executor.InvocationCount} evidence={evidence.Records.Count}");
    }
}
