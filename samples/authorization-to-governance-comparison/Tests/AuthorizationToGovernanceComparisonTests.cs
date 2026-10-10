using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace AuthorizationToGovernanceComparison.Tests;

public sealed class AuthorizationToGovernanceComparisonTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static TheoryData<string, Variant> ScenarioMatrix
    {
        get
        {
            TheoryData<string, Variant> data = [];

            foreach (ComparisonScenario scenario in ComparisonRunner.Scenarios)
            {
                foreach (Variant variant in Enum.GetValues<Variant>())
                {
                    data.Add(scenario.Id, variant);
                }
            }

            return data;
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [MemberData(nameof(ScenarioMatrix))]
    public async Task Every_variant_produces_its_expected_status_and_execution_count(string scenarioId, Variant variant)
    {
        ComparisonScenario scenario = ComparisonRunner.Scenarios.Single(item => item.Id == scenarioId);

        VariantRun run = await ComparisonRunner.RunAsync(scenario, variant, Token);

        (int expectedStatus, int expectedInvocations) = ComparisonRunner.Expected(scenario, variant);
        Assert.Equal(expectedStatus, run.StatusCode);
        Assert.Equal(expectedInvocations, run.Invocations);
    }

    [Theory]
    [InlineData(Variant.EndpointAuthorization, "support-a", "acct-100")]
    [InlineData(Variant.ResourceAuthorization, "support-a", "acct-100")]
    [InlineData(Variant.ResourceAuthorization, "admin-b", "acct-100")]
    [InlineData(Variant.ResourceAuthorization, "admin-a", "acct-200")]
    public async Task Authorization_denial_prevents_execution_in_the_authorization_variants(
        Variant variant,
        string actorId,
        string accountId)
    {
        await using SampleHost host = await SampleHost.StartAsync(Token);

        using HttpResponseMessage response = await host.DisableAsync(variant, accountId, actorId, null, Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(host.Disabler.Invocations);
    }

    [Fact]
    public async Task Endpoint_authorization_alone_does_not_see_the_resource_tenant()
    {
        await using SampleHost host = await SampleHost.StartAsync(Token);

        using HttpResponseMessage response =
            await host.DisableAsync(Variant.EndpointAuthorization, "acct-100", "admin-b", null, Token);

        // Variant 1 protects "administrators only". Tenant isolation needs the resource,
        // which is what variant 2 adds; this is a scope difference, not a hidden bug.
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(host.Disabler.Invocations);
    }

    [Theory]
    [InlineData("support-a", "acct-100", GovernanceOutcome.Denied, "account.requires-administrator")]
    [InlineData("admin-b", "acct-100", GovernanceOutcome.Denied, "account.cross-tenant")]
    [InlineData("admin-a", "acct-200", GovernanceOutcome.Denied, "account.protected")]
    [InlineData("admin-a", "acct-400", GovernanceOutcome.AcknowledgmentRequired, "account.active-sessions")]
    [InlineData("admin-a", "acct-500", GovernanceOutcome.EscalationRecommended,
        "account.privileged-requires-security-review")]
    [InlineData("admin-a", "acct-600", GovernanceOutcome.Deferred, "account.directory-sync-pending")]
    public async Task Every_non_allowed_governance_outcome_prevents_execution(
        string actorId,
        string accountId,
        GovernanceOutcome expectedOutcome,
        string expectedReason)
    {
        await using SampleHost host = await SampleHost.StartAsync(Token);

        using HttpResponseMessage response =
            await host.DisableAsync(Variant.GovernedExecution, accountId, actorId, null, Token);
        GovernedResponse? body = await response.Content.ReadFromJsonAsync<GovernedResponse>(_jsonOptions, Token);

        Assert.NotNull(body);
        Assert.Equal(expectedOutcome, body.Outcome);
        Assert.Equal(expectedReason, body.ReasonCode);
        Assert.Equal(GovernedExecutionVariant.StatusCodeFor(expectedOutcome), (int)response.StatusCode);
        Assert.Null(body.ExecutionId);
        Assert.Empty(host.Disabler.Invocations);
        Assert.Empty(host.Executions.Entries);

        DecisionRecord decision = Assert.Single(host.Decisions.Entries);
        Assert.Equal(expectedOutcome, decision.Outcome);
    }

    [Fact]
    public async Task Allowed_governance_decision_executes_once_with_linked_evidence()
    {
        await using SampleHost host = await SampleHost.StartAsync(Token);

        using HttpResponseMessage response =
            await host.DisableAsync(Variant.GovernedExecution, "acct-100", "admin-a", null, Token);
        GovernedResponse? body = await response.Content.ReadFromJsonAsync<GovernedResponse>(_jsonOptions, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(GovernanceOutcome.Allowed, body.Outcome);

        DisableInvocation invocation = Assert.Single(host.Disabler.Invocations);
        Assert.Equal(new DisableInvocation(Variant.GovernedExecution, "acct-100"), invocation);

        DecisionRecord decision = Assert.Single(host.Decisions.Entries);
        ExecutionRecord execution = Assert.Single(host.Executions.Entries);
        Assert.Equal(decision.DecisionId, execution.DecisionId);
        Assert.Equal(execution.ExecutionId, body.ExecutionId);
        Assert.Equal(AccountDisablePolicy.Version, decision.PolicyVersion);
    }

    [Fact]
    public async Task Acknowledgment_continuation_executes_once_and_cannot_be_reused()
    {
        await using SampleHost host = await SampleHost.StartAsync(Token);

        GovernedResponse first = await DisableGovernedAsync(host, "acct-400", "admin-a", null);
        GovernedResponse continued = await DisableGovernedAsync(host, "acct-400", "admin-a", first.DecisionId);
        GovernedResponse reused = await DisableGovernedAsync(host, "acct-400", "admin-a", first.DecisionId);

        Assert.Equal(GovernanceOutcome.AcknowledgmentRequired, first.Outcome);
        Assert.Equal(GovernanceOutcome.Allowed, continued.Outcome);
        Assert.Equal("account.sessions-acknowledged", continued.ReasonCode);
        Assert.Equal(GovernanceOutcome.AcknowledgmentRequired, reused.Outcome);
        Assert.Single(host.Disabler.Invocations);
        Assert.Equal(
            first.DecisionId,
            host.Decisions.Find(continued.DecisionId)?.AcknowledgedDecisionId);
    }

    [Fact]
    public async Task Expired_acknowledgment_does_not_satisfy_the_continuation()
    {
        await using SampleHost host = await SampleHost.StartAsync(Token);

        GovernedResponse first = await DisableGovernedAsync(host, "acct-400", "admin-a", null);
        host.Clock.UtcNow = host.Clock.UtcNow.AddMinutes(11);
        GovernedResponse late = await DisableGovernedAsync(host, "acct-400", "admin-a", first.DecisionId);

        Assert.Equal(GovernanceOutcome.AcknowledgmentRequired, late.Outcome);
        Assert.Empty(host.Disabler.Invocations);
    }

    [Fact]
    public async Task Acknowledgment_for_a_different_account_does_not_satisfy_the_continuation()
    {
        await using SampleHost host = await SampleHost.StartAsync(Token);

        // A non-acknowledgment decision cannot be presented as an acknowledgment either.
        GovernedResponse unrelated = await DisableGovernedAsync(host, "acct-500", "admin-a", null);
        GovernedResponse attempt = await DisableGovernedAsync(host, "acct-400", "admin-a", unrelated.DecisionId);

        Assert.Equal(GovernanceOutcome.AcknowledgmentRequired, attempt.Outcome);
        Assert.Empty(host.Disabler.Invocations);
    }

    [Fact]
    public async Task Execution_boundary_refuses_non_allowed_and_repeated_decisions()
    {
        await using SampleHost host = await SampleHost.StartAsync(Token);

        GovernedResponse denied = await DisableGovernedAsync(host, "acct-200", "admin-a", null);
        GovernedResponse allowed = await DisableGovernedAsync(host, "acct-100", "admin-a", null);

        Assert.Null(host.Boundary.Execute(denied.DecisionId));
        Assert.Null(host.Boundary.Execute(allowed.DecisionId));
        Assert.Null(host.Boundary.Execute("decision-9999"));
        Assert.Single(host.Disabler.Invocations);
    }

    [Theory]
    [InlineData(Variant.EndpointAuthorization)]
    [InlineData(Variant.ResourceAuthorization)]
    [InlineData(Variant.GovernedExecution)]
    public async Task Unauthenticated_requests_never_execute(Variant variant)
    {
        await using SampleHost host = await SampleHost.StartAsync(Token);

        using HttpResponseMessage response = await host.DisableAsync(variant, "acct-100", null, null, Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(host.Disabler.Invocations);
        Assert.Empty(host.Decisions.Entries);
    }

    [Fact]
    public async Task Each_variant_produces_a_different_kind_of_evidence()
    {
        await using SampleHost endpoint = await SampleHost.StartAsync(Token);
        await using SampleHost resource = await SampleHost.StartAsync(Token);
        await using SampleHost governed = await SampleHost.StartAsync(Token);

        (await endpoint.DisableAsync(Variant.EndpointAuthorization, "acct-100", "admin-a", null, Token)).Dispose();
        (await resource.DisableAsync(Variant.ResourceAuthorization, "acct-100", "admin-a", null, Token)).Dispose();
        (await governed.DisableAsync(Variant.GovernedExecution, "acct-100", "admin-a", null, Token)).Dispose();

        // Variant 1: an operational log line only.
        Assert.Contains(
            endpoint.LogSink.Entries,
            entry => entry.Category.EndsWith(nameof(EndpointAuthorizationVariant), StringComparison.Ordinal));
        Assert.Empty(endpoint.Audit.Entries);
        Assert.Empty(endpoint.Decisions.Entries);

        // Variant 2: a structured audit entry, but no decision or execution records.
        AuditEntry audit = Assert.Single(resource.Audit.Entries);
        Assert.True(audit.Succeeded);
        Assert.Empty(resource.Decisions.Entries);
        Assert.Empty(resource.Executions.Entries);

        // Variant 3: separate decision and execution evidence.
        Assert.Single(governed.Decisions.Entries);
        Assert.Single(governed.Executions.Entries);
        Assert.Empty(governed.Audit.Entries);
    }

    [Fact]
    public async Task Resource_authorization_audits_denials_with_reason_codes()
    {
        await using SampleHost host = await SampleHost.StartAsync(Token);

        (await host.DisableAsync(Variant.ResourceAuthorization, "acct-100", "admin-b", null, Token)).Dispose();

        AuditEntry audit = Assert.Single(host.Audit.Entries);
        Assert.False(audit.Succeeded);
        Assert.Equal([DisableAccountHandler.CrossTenant], audit.FailureReasons);
    }

    [Theory]
    [InlineData(false, DirectoryState.SyncPending, true, GovernanceOutcome.Denied)]
    [InlineData(true, DirectoryState.SyncPending, true, GovernanceOutcome.Deferred)]
    [InlineData(true, DirectoryState.Current, true, GovernanceOutcome.EscalationRecommended)]
    public void Policy_applies_access_then_availability_then_workflow_rules(
        bool accessGranted,
        DirectoryState directoryState,
        bool isPrivileged,
        GovernanceOutcome expected)
    {
        Account account = new("acct-x", "tenant-a", IsProtected: false, HasActiveSessions: true, isPrivileged,
            directoryState);

        PolicyResult result = AccountDisablePolicy.Evaluate(
            new GovernanceContext(account, accessGranted, accessGranted ? null : "account.cross-tenant", false));

        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public async Task Comparison_runner_reports_every_expectation_met()
    {
        using StringWriter output = new();

        int exitCode = await ComparisonRunner.RunAllAsync(output, Token);

        Assert.Equal(0, exitCode);
        Assert.Contains(
            $"{ComparisonRunner.Scenarios.Count} scenario(s) x 3 variants behaved as expected.",
            output.ToString(),
            StringComparison.Ordinal);
    }

    private static async Task<GovernedResponse> DisableGovernedAsync(
        SampleHost host,
        string accountId,
        string actorId,
        string? acknowledgedDecisionId)
    {
        using HttpResponseMessage response = await host.DisableAsync(
            Variant.GovernedExecution, accountId, actorId, acknowledgedDecisionId, Token);

        return await response.Content.ReadFromJsonAsync<GovernedResponse>(_jsonOptions, Token)
            ?? throw new InvalidOperationException("The governed endpoint returned no body.");
    }
}
