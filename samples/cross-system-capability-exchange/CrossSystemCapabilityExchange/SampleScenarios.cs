namespace CrossSystemCapabilityExchange;

public static class SampleScenarios
{
    public const string RecipientSystemId = "system-b";
    public const string DefaultDestination =
        "system-b-regulatory-review-store";

    public static readonly DateTimeOffset IssuedUtc =
        new(2032, 4, 10, 12, 0, 0, TimeSpan.Zero);

    public static RecipientIssuerPolicy CreateIssuerPolicy() =>
        new(
            Issuer: "system-a",
            RecipientSystemId: RecipientSystemId,
            AcceptedKeyIds: new HashSet<string>(
                new[] { "a-2032-04" },
                StringComparer.Ordinal),
            Audience: "system-b:records-export",
            Operation: "records.export",
            MaxLifetime: TimeSpan.FromMinutes(5),
            MaxClockSkew: TimeSpan.FromSeconds(30),
            MaximumRemainingDelegationDepth: 0,
            AllowChainedDelegation: false);

    public static CrossSystemGateway CreateGateway(
        IExportExecutor executor,
        ICapabilityUseStore? useStore = null,
        InMemoryRevocationStore? revocationStore = null,
        string executionDestination = DefaultDestination,
        RecipientIssuerPolicy? issuerPolicy = null)
    {
        RecipientIssuerPolicy policy =
            issuerPolicy ?? CreateIssuerPolicy();

        return new CrossSystemGateway(
            new CrossSystemCapabilityValidator(
                new InMemoryIssuerTrustStore(new[] { policy }),
                revocationStore ?? new InMemoryRevocationStore()),
            useStore ?? new InMemoryCapabilityUseStore(),
            executor,
            executionDestination);
    }

    public static ProtectedCapabilityArtifact CreateArtifact(
        string capabilityId = "cap-a-784",
        string issuer = "system-a",
        string audience = "system-b:records-export",
        string resourceVersion = "snapshot-8",
        string requestDigest = "sha256:example-request-001",
        string keyId = "a-2032-04",
        bool proofValid = true,
        DateTimeOffset? issuedAtUtc = null,
        DateTimeOffset? expiresAtUtc = null,
        IReadOnlyList<DelegationHop>? delegationChain = null)
    {
        DateTimeOffset issued =
            issuedAtUtc ?? IssuedUtc;

        CrossSystemCapability capability = new(
            CapabilityId: capabilityId,
            Issuer: issuer,
            Audience: audience,
            OriginatingSubject: "analyst-17",
            PresenterBinding: "system-b-export-worker",
            Operation: "records.export",
            ResourceId: "record-set-42",
            ResourceVersion: resourceVersion,
            Purpose: "regulatory-review",
            RequestDigest: requestDigest,
            IssuerDecisionId: "dec-a-551",
            IssuerPolicyVersion: "4.2",
            IssuedAtUtc: issued,
            ExpiresAtUtc:
                expiresAtUtc ?? issued.AddMinutes(5),
            MaxUses: 1,
            RemainingDelegationDepth: 0,
            DelegationChain: delegationChain ??
            new[]
            {
                new DelegationHop(
                    Issuer: issuer,
                    DelegatedTo: RecipientSystemId,
                    HopPosition: 0,
                    RemainingDelegationDepth: 0)
            });

        return new ProtectedCapabilityArtifact(
            capability,
            keyId,
            new SimulatedProof(proofValid));
    }

    public static RecipientExportContext CreateContext(
        string audience = "system-b:records-export",
        string authenticatedPresenter = "system-b-export-worker",
        string operation = "records.export",
        string resourceId = "record-set-42",
        string resourceVersion = "snapshot-8",
        string purpose = "regulatory-review",
        string requestDigest = "sha256:example-request-001",
        bool localPolicyAllows = true,
        DateTimeOffset? nowUtc = null)
    {
        return new RecipientExportContext(
            CorrelationId: "corr-1001",
            Audience: audience,
            AuthenticatedPresenter: authenticatedPresenter,
            Operation: operation,
            ResourceId: resourceId,
            ResourceVersion: resourceVersion,
            Purpose: purpose,
            RequestDigest: requestDigest,
            LocalPolicyAllows: localPolicyAllows,
            NowUtc: nowUtc ?? IssuedUtc.AddMinutes(1));
    }
}
