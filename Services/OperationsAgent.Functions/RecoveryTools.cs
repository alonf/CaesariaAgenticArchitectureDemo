using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Caesarea.CanonicalModel;
using DemoScenario.Contracts;
using DurableOperations.Contracts;
using Microsoft.Agents.AI.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using OperationsAgent.Api.Services;

namespace OperationsAgent.Functions;

internal sealed partial class RecoveryTools(IEnergyReadGateway reads, ICommandCenterStageReader stages, ILogger<RecoveryTools> logger)
{
    [Description("Starts a supervised recovery of L-417. Returns a process ID promptly; acceptance is not repair completion. Never claims approval.")]
    public async Task<string> StartRecoveryAsync(
        [Description("Set true only to explicitly retry a failed operation that never scheduled command execution. A fresh supervisor approval is always required.")] bool retryFailedOperation = false)
    {
        var context = DurableAgentContext.Current;
        var prefix = SessionPrefix(context.CurrentSession.ToString());
        var twin = await reads.GetStateAsync(DemoAssets.StreetlightAssetId, prefix, CancellationToken.None);
        var stage = await stages.GetCurrentStageAsync(prefix, CancellationToken.None);
        if (stage.Id < DemoStage.Workflow)
        {
            return "Recovery is unavailable below the Workflow stage.";
        }

        var baseId = prefix + "-" + Hash(string.Create(CultureInfo.InvariantCulture, $"{twin.StateRevision}:{stage.AppliedAt:O}"));
        var admission = await SelectAttemptAsync(baseId, retryFailedOperation,
            id => context.GetOrchestrationStatusAsync(id, includeDetails: true));
        if (!admission.CanSchedule)
        {
            return admission.Message;
        }

        var instanceId = admission.InstanceId;
        var request = new RecoveryRequest(twin.AssetId, twin.StateRevision, stage.AppliedAt, instanceId);
        context.ScheduleNewOrchestration(nameof(RecoveryOrchestration.RecoverLighting), request, new StartOrchestrationOptions(instanceId));
        RecoveryStarted(logger, instanceId, twin.AssetId, twin.StateRevision);
        return $"Operation {instanceId} accepted. A supervisor must review it separately. This is not proof of completion.";
    }

    [Description("Reads the actual durable status of an operation started in this conversation. Use the remembered operation ID.")]
    public async Task<object> GetRecoveryStatusAsync([Description("The operation ID returned when this conversation started the repair.")] string instanceId)
    {
        var context = DurableAgentContext.Current;
        if (!instanceId.StartsWith(SessionPrefix(context.CurrentSession.ToString()) + "-", StringComparison.Ordinal))
        {
            return new { error = "This operation does not belong to this agent session." };
        }

        var status = await context.GetOrchestrationStatusAsync(instanceId, includeDetails: true);
        RecoveryStatusRead(logger, instanceId, status?.RuntimeStatus.ToString() ?? "NotFound");
        return status is null ? new { error = "Operation not found." } : (object)new
        {
            instanceId,
            runtimeStatus = status.RuntimeStatus.ToString(),
            progress = status.SerializedCustomStatus,
            outcome = status.SerializedOutput
        };
    }

    internal static string SessionPrefix(string sessionId) => "repair-" + Hash(sessionId);

    // Preserve every failed history. Only pre-dispatch failures can opt into a fresh attempt,
    // with fresh validation/challenge/approval. Unknown or post-dispatch failures require review.
    internal static async Task<RecoveryAdmission> SelectAttemptAsync(string baseId, bool retryFailedOperation,
        Func<string, Task<OrchestrationMetadata?>> readStatus)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var instanceId = attempt == 1 ? baseId : $"{baseId}-attempt-{attempt}";
            var existing = await readStatus(instanceId);
            if (existing is null)
            {
                return new RecoveryAdmission(instanceId, true, "A new attempt requires fresh supervisor approval.");
            }

            if (existing.RuntimeStatus != OrchestrationRuntimeStatus.Failed)
            {
                return new RecoveryAdmission(instanceId, false,
                    $"Operation {instanceId} already exists ({existing.RuntimeStatus}). Query its status; do not start a duplicate.");
            }

            var phase = ReadPhase(existing.SerializedCustomStatus);
            if (phase is not ("Validating" or "AwaitingApproval" or "Revalidating"))
            {
                return new RecoveryAdmission(instanceId, false,
                    $"Operation {instanceId} failed after possible command dispatch or has no trustworthy phase. Its outcome is unknown; inspect the Hub before further work.");
            }

            if (!retryFailedOperation)
            {
                return new RecoveryAdmission(instanceId, false,
                    $"Operation {instanceId} failed before command dispatch. Explicitly request retryFailedOperation=true to create a new attempt with fresh approval.");
            }
        }

        return new RecoveryAdmission(baseId, false, "Three attempts have been used for this state and stage. Investigate the failing dependency before starting further work.");
    }

    private static string? ReadPhase(string? status)
    {
        try
        {
            return status is null ? null : JsonSerializer.Deserialize<RecoveryProgress>(status)?.State;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..24].ToLowerInvariant();

    [LoggerMessage(2400, LogLevel.Information, "Durable recovery accepted for {AssetId}, revision {Revision}. CorrelationId: {InstanceId}.")]
    private static partial void RecoveryStarted(ILogger logger, string instanceId, string assetId, long revision);

    [LoggerMessage(2401, LogLevel.Information, "Durable recovery status {Status}. CorrelationId: {InstanceId}.")]
    private static partial void RecoveryStatusRead(ILogger logger, string instanceId, string status);
}

internal sealed record RecoveryAdmission(string InstanceId, bool CanSchedule, string Message);
