using System.Text.Json;
using DurableOperations.Contracts;
using Microsoft.DurableTask.Client;
using OperationsAgent.Functions;

namespace Caesarea.Deterministic.Tests;

public sealed class RecoveryAdmissionTests
{
    [Theory]
    [InlineData("Validating")]
    [InlineData("AwaitingApproval")]
    [InlineData("Revalidating")]
    public async Task ExplicitRetryPreservesFailedHistoryAndUsesANewAttempt(string phase)
    {
        var failed = Failed("repair-test", phase);
        var withoutOptIn = await RecoveryTools.SelectAttemptAsync("repair-test", false, _ => Task.FromResult<OrchestrationMetadata?>(failed));
        Assert.False(withoutOptIn.CanSchedule);
        var withOptIn = await RecoveryTools.SelectAttemptAsync("repair-test", true,
            id => Task.FromResult(id == "repair-test" ? failed : null));
        Assert.True(withOptIn.CanSchedule);
        Assert.Equal("repair-test-attempt-2", withOptIn.InstanceId);
    }

    [Theory]
    [InlineData("Executing")]
    [InlineData("Reconciling")]
    [InlineData(null)]
    public async Task PossibleCommandDispatchOrMissingPhaseBlocksNewAttempt(string? phase)
    {
        var admission = await RecoveryTools.SelectAttemptAsync("repair-test", true,
            _ => Task.FromResult<OrchestrationMetadata?>(Failed("repair-test", phase)));
        Assert.False(admission.CanSchedule);
        Assert.Contains("unknown", admission.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(OrchestrationRuntimeStatus.Running)]
    [InlineData(OrchestrationRuntimeStatus.Completed)]
    [InlineData(OrchestrationRuntimeStatus.Terminated)]
    public async Task ExistingNonFailedOperationIsNeverReplaced(OrchestrationRuntimeStatus status)
    {
        var admission = await RecoveryTools.SelectAttemptAsync("repair-test", true,
            _ => Task.FromResult<OrchestrationMetadata?>(new OrchestrationMetadata("RecoverLighting", "repair-test") { RuntimeStatus = status }));
        Assert.False(admission.CanSchedule);
        Assert.Equal("repair-test", admission.InstanceId);
    }

    [Fact]
    public async Task ExplicitRetriesStopAfterThreeAttempts()
    {
        var lookups = 0;
        var admission = await RecoveryTools.SelectAttemptAsync("repair-test", true, id =>
        {
            lookups++;
            return Task.FromResult<OrchestrationMetadata?>(Failed(id, "Validating"));
        });
        Assert.False(admission.CanSchedule);
        Assert.Equal(3, lookups);
    }

    private static OrchestrationMetadata Failed(string id, string? phase) => new("RecoverLighting", id)
    {
        RuntimeStatus = OrchestrationRuntimeStatus.Failed,
        SerializedCustomStatus = phase is null ? null : JsonSerializer.Serialize(new RecoveryProgress(phase, "Test failure"))
    };
}
