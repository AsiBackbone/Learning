namespace CrossSystemCapabilityExchange;

public sealed class CrossSystemCapabilityValidator(
    IIssuerTrustStore trustStore,
    IRevocationStore revocationStore)
{
    public CapabilityValidationResult Validate(
        ProtectedCapabilityArtifact artifact,
        RecipientExportContext context)
    {
        CrossSystemCapability capability = artifact.Capability;
        RecipientIssuerPolicy? issuerPolicy =
            trustStore.Find(capability.Issuer);

        if (issuerPolicy is null)
        {
            return CapabilityValidationResult.Reject(
                "issuer.not-trusted");
        }

        // A real verifier must resolve the presented KeyId to a recipient-accepted
        // trust anchor before attempting cryptographic verification. Otherwise an
        // untrusted KeyId could select verifier material that the recipient never
        // intended to trust. The simulated proof check comes only after this step.
        if (!issuerPolicy.AcceptedKeyIds.Contains(artifact.KeyId))
        {
            return CapabilityValidationResult.Reject(
                "key.not-accepted");
        }

        if (!artifact.Proof.IsValid)
        {
            return CapabilityValidationResult.Reject(
                "proof.invalid");
        }

        if (!string.Equals(
                capability.Audience,
                issuerPolicy.Audience,
                StringComparison.Ordinal) ||
            !string.Equals(
                context.Audience,
                capability.Audience,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Reject(
                "audience.mismatch");
        }

        if (!string.Equals(
                capability.PresenterBinding,
                context.AuthenticatedPresenter,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Reject(
                "presenter.mismatch");
        }

        if (!string.Equals(
                capability.Operation,
                issuerPolicy.Operation,
                StringComparison.Ordinal) ||
            !string.Equals(
                context.Operation,
                capability.Operation,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Reject(
                "operation.mismatch");
        }

        if (!string.Equals(
                context.ResourceId,
                capability.ResourceId,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Reject(
                "resource.mismatch");
        }

        if (!string.Equals(
                context.ResourceVersion,
                capability.ResourceVersion,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Reject(
                "resource.version-drift");
        }

        if (!string.Equals(
                context.Purpose,
                capability.Purpose,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Reject(
                "purpose.mismatch");
        }

        if (!string.Equals(
                context.RequestDigest,
                capability.RequestDigest,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Reject(
                "request.binding-mismatch");
        }

        if (capability.MaxUses <= 0)
        {
            return CapabilityValidationResult.Reject(
                "use-policy.invalid");
        }

        if (capability.RemainingDelegationDepth < 0 ||
            capability.RemainingDelegationDepth >
                issuerPolicy.MaximumRemainingDelegationDepth)
        {
            return CapabilityValidationResult.Reject(
                "delegation.depth-invalid");
        }

        CapabilityValidationResult chainResult =
            ValidateDelegationChain(
                capability,
                issuerPolicy);

        if (!chainResult.Accepted)
        {
            return chainResult;
        }

        TimeSpan lifetime =
            capability.ExpiresAtUtc - capability.IssuedAtUtc;

        if (lifetime <= TimeSpan.Zero ||
            lifetime > issuerPolicy.MaxLifetime)
        {
            return CapabilityValidationResult.Reject(
                "lifetime.not-accepted");
        }

        if (context.NowUtc + issuerPolicy.MaxClockSkew <
            capability.IssuedAtUtc)
        {
            return CapabilityValidationResult.Reject(
                "lifetime.not-yet-valid");
        }

        if (context.NowUtc >=
            capability.ExpiresAtUtc + issuerPolicy.MaxClockSkew)
        {
            return CapabilityValidationResult.Reject(
                "lifetime.expired");
        }

        if (revocationStore.IsRevoked(capability.CapabilityId))
        {
            return CapabilityValidationResult.Reject(
                "capability.revoked");
        }

        if (!context.LocalPolicyAllows)
        {
            return CapabilityValidationResult.Reject(
                "recipient-policy.denied");
        }

        return CapabilityValidationResult.Accept();
    }

    private static CapabilityValidationResult ValidateDelegationChain(
        CrossSystemCapability capability,
        RecipientIssuerPolicy issuerPolicy)
    {
        IReadOnlyList<DelegationHop> chain =
            capability.DelegationChain;

        if (chain.Count == 0)
        {
            return CapabilityValidationResult.Reject(
                "delegation.chain-missing");
        }

        if (!issuerPolicy.AllowChainedDelegation &&
            chain.Count != 1)
        {
            return CapabilityValidationResult.Reject(
                "delegation.chain-not-accepted");
        }

        DelegationHop first = chain[0];
        DelegationHop last = chain[^1];

        if (!string.Equals(
                first.Issuer,
                capability.Issuer,
                StringComparison.Ordinal) ||
            !string.Equals(
                last.DelegatedTo,
                issuerPolicy.RecipientSystemId,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Reject(
                "delegation.endpoint-mismatch");
        }

        for (int index = 0; index < chain.Count; index++)
        {
            DelegationHop hop = chain[index];

            if (hop.HopPosition != index ||
                hop.RemainingDelegationDepth < 0)
            {
                return CapabilityValidationResult.Reject(
                    "delegation.chain-invalid");
            }

            if (index > 0)
            {
                DelegationHop parent = chain[index - 1];

                if (!string.Equals(
                        parent.DelegatedTo,
                        hop.Issuer,
                        StringComparison.Ordinal) ||
                    parent.RemainingDelegationDepth <= 0 ||
                    hop.RemainingDelegationDepth >=
                        parent.RemainingDelegationDepth)
                {
                    return CapabilityValidationResult.Reject(
                        "delegation.not-narrowed");
                }
            }
        }

        if (last.RemainingDelegationDepth !=
            capability.RemainingDelegationDepth)
        {
            return CapabilityValidationResult.Reject(
                "delegation.depth-mismatch");
        }

        return CapabilityValidationResult.Accept();
    }
}
