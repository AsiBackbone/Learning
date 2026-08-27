namespace FederatedGovernanceCoordination;

public sealed class AuthoritySetResolver
{
    public AuthoritySetDescriptor Resolve(ResourceState resource)
    {
        if (string.Equals(
                resource.CurrentRegion,
                resource.DestinationRegion,
                StringComparison.Ordinal))
        {
            return new AuthoritySetDescriptor(
                AuthoritySetId: $"records.transfer:{resource.CurrentRegion}:local",
                AuthoritySetVersion: $"{resource.ResourceVersion}:local",
                ResourceVersion: resource.ResourceVersion,
                Mode: CoordinationMode.LocalOnly,
                RequiredAuthorityDomains: new HashSet<string>(
                    ["records-app-local"],
                    StringComparer.Ordinal));
        }

        return new AuthoritySetDescriptor(
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
