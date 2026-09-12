namespace FederatedGovernanceCoordination;

public sealed class AuthoritySetResolver
{
    public static AuthoritySetDescriptor Resolve(ResourceState resource)
    {
        return string.Equals(
                resource.CurrentRegion,
                resource.DestinationRegion,
                StringComparison.Ordinal)
            ? new AuthoritySetDescriptor(
                AuthoritySetId: $"records.transfer:{resource.CurrentRegion}:local",
                AuthoritySetVersion: $"{resource.ResourceVersion}:local",
                ResourceVersion: resource.ResourceVersion,
                Mode: CoordinationMode.LocalOnly,
                RequiredAuthorityDomains: new HashSet<string>(
                    ["records-app-local"],
                    StringComparer.Ordinal))
            : new AuthoritySetDescriptor(
            AuthoritySetId:
                $"records.transfer:{resource.CurrentRegion}:{resource.DestinationRegion}",
            AuthoritySetVersion:
                $"{resource.ResourceVersion}:{resource.CurrentRegion}->{resource.DestinationRegion}",
            ResourceVersion: resource.ResourceVersion,
            Mode: CoordinationMode.Federated,
            RequiredAuthorityDomains: new HashSet<string>(
                [
                    $"{resource.CurrentRegion}-release",
                    $"{resource.DestinationRegion}-intake"
                ],
                StringComparer.Ordinal));
    }
}
