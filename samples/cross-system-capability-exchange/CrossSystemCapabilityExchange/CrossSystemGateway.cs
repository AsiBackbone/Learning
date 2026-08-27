namespace CrossSystemCapabilityExchange;

public sealed class CrossSystemGateway(
    CrossSystemCapabilityValidator validator,
    ICapabilityUseStore useStore,
    IExportExecutor executor,
    string executionDestination)
{
    private long _decisionSequence;

    public async Task<GatewayResult> ExecuteAsync(
        ProtectedCapabilityArtifact artifact,
        RecipientExportContext context,
        CancellationToken cancellationToken)
    {
        string recipientDecisionId =
            $"dec-b-{Interlocked.Increment(ref _decisionSequence):D4}";

        CapabilityValidationResult validation =
            validator.Validate(
                artifact,
                context);

        if (!validation.Accepted)
        {
            return GatewayResult.Rejected(
                recipientDecisionId,
                validation.ReasonCode);
        }

        CapabilityClaimResult claim =
            await useStore.TryClaimAsync(
                artifact.Capability.CapabilityId,
                artifact.Capability.MaxUses,
                cancellationToken);

        if (!claim.Accepted)
        {
            return GatewayResult.Rejected(
                recipientDecisionId,
                claim.ReasonCode);
        }

        // One stable execution identity per capability is an intentional teaching
        // choice. A real executor can use it as an idempotency/reconciliation key.
        // It is distinct from RecipientDecisionId, which identifies this local
        // evaluation attempt.
        string executionId =
            $"exec-{artifact.Capability.CapabilityId}";

        ValidatedExportCommand command = new(
            ExecutionId: executionId,
            RecipientDecisionId: recipientDecisionId,
            OriginatingSubject:
                artifact.Capability.OriginatingSubject,
            IssuerDecisionId:
                artifact.Capability.IssuerDecisionId,
            ResourceId: context.ResourceId,
            ResourceVersion: context.ResourceVersion,
            Destination: executionDestination,
            Purpose: context.Purpose,
            CapabilityId: artifact.Capability.CapabilityId,
            CorrelationId: context.CorrelationId);

        try
        {
            ExportExecutionResult execution =
                await executor.ExportAsync(
                    command,
                    cancellationToken);

            if (!execution.Succeeded)
            {
                return GatewayResult.ExecutionFailed(
                    recipientDecisionId,
                    executionId,
                    execution.ReasonCode);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Do not propagate executor exception details across the recipient
            // boundary. Internal telemetry may capture the exception separately;
            // the gateway returns only the stable failure category here.
            return GatewayResult.ExecutionFailed(
                recipientDecisionId,
                executionId,
                "execution.failed");
        }

        return GatewayResult.ExecutedSuccessfully(
            recipientDecisionId,
            executionId);
    }
}
