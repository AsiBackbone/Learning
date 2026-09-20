using System.Diagnostics;

namespace CrossSystemCapabilityExchange;

public sealed class CrossSystemGateway(
    CrossSystemCapabilityValidator validator,
    ICapabilityUseStore useStore,
    IExportExecutor executor,
    string executionDestination)
{
    private static readonly ActivitySource _activitySource =
        new("CrossSystemCapabilityExchange.Gateway");

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
        catch (Exception exception)
        {
            string failureCategory = ClassifyExecutionFailure(exception);

            RecordExecutionFailure(
                exception,
                failureCategory,
                recipientDecisionId,
                executionId,
                artifact.Capability.CapabilityId,
                context.CorrelationId);

            // Do not propagate executor exception details across the recipient
            // boundary. Internal telemetry captures categorized failure details;
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

    private static string ClassifyExecutionFailure(Exception exception)
    {
        return exception switch
        {
            TimeoutException => "executor.timeout",
            UnauthorizedAccessException => "executor.authorization",
            ArgumentException => "executor.contract",
            InvalidOperationException => "executor.state",
            System.IO.IOException => "executor.io",
            _ => "executor.unexpected"
        };
    }

    private static void RecordExecutionFailure(
        Exception exception,
        string failureCategory,
        string recipientDecisionId,
        string executionId,
        string capabilityId,
        string correlationId)
    {
        using Activity? activity =
            _activitySource.StartActivity(
                "cross-system.execution.failure",
                ActivityKind.Internal);

        activity?.SetTag("error.type", exception.GetType().FullName);
        activity?.SetTag("error.category", failureCategory);
        activity?.SetTag("gateway.recipient_decision_id", recipientDecisionId);
        activity?.SetTag("gateway.execution_id", executionId);
        activity?.SetTag("gateway.capability_id", capabilityId);
        activity?.SetTag("gateway.correlation_id", correlationId);

        Trace.TraceError(
            "CrossSystemGateway execution failed. Category={0}; ExceptionType={1}; RecipientDecisionId={2}; ExecutionId={3}; CapabilityId={4}; CorrelationId={5}",
            failureCategory,
            exception.GetType().Name,
            recipientDecisionId,
            executionId,
            capabilityId,
            correlationId);
    }
}
