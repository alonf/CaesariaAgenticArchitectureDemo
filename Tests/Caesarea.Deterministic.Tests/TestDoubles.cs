using Caesarea.Contracts;
using CommandCenter.Api.Services;
using DemoScenario.Api.Services;
using EnergyHub.Api.Services;

namespace Caesarea.Deterministic.Tests;

sealed class TestTimeProvider(DateTimeOffset? initial = null) : TimeProvider
{
    private DateTimeOffset _utcNow = initial ?? new DateTimeOffset(2026, 8, 23, 8, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan by) => _utcNow = _utcNow.Add(by);
}

sealed class FakeSmartPoleGateway : ISmartPoleGateway
{
    public required SmartPolePhysicalState PhysicalState { get; init; }

    public string? LastCorrelationId { get; private set; }

    public SetLampStateCommand? LastCommand { get; private set; }

    public Func<SetLampStateCommand, string, CancellationToken, Task<SmartPoleCommandResult>>? OnSetLampStateAsync { get; set; }

    public Task<SmartPolePhysicalState> GetStateAsync(string assetId, string correlationId, CancellationToken cancellationToken)
    {
        LastCorrelationId = correlationId;
        return Task.FromResult(PhysicalState);
    }

    public Task<SmartPoleCommandResult> SetLampStateAsync(SetLampStateCommand command, string correlationId, CancellationToken cancellationToken)
    {
        LastCommand = command;
        LastCorrelationId = correlationId;

        if (OnSetLampStateAsync is not null)
        {
            return OnSetLampStateAsync(command, correlationId, cancellationToken);
        }

        return Task.FromResult(new SmartPoleCommandResult(
            command.AssetId,
            command.DesiredIsOn,
            CommandExecutionStatus.Succeeded,
            correlationId,
            "Fake SmartPole confirmed the command.",
            PhysicalState.LastReportedAt));
    }
}

sealed class FakeEnergyHubGateway : IEnergyHubGateway
{
    public required EnergyOperationalTwin State { get; init; }

    public required IReadOnlyList<ActivityRecord> Activity { get; init; }

    public required RestoreScheduledModeResult RestoreResult { get; init; }

    public string? LastCorrelationId { get; private set; }

    public Task<EnergyOperationalTwin> GetStateAsync(string assetId, string correlationId, CancellationToken cancellationToken)
    {
        LastCorrelationId = correlationId;
        return Task.FromResult(State);
    }

    public Task<IReadOnlyList<ActivityRecord>> GetRecentActivityAsync(string assetId, int limit, string correlationId, CancellationToken cancellationToken)
    {
        LastCorrelationId = correlationId;
        return Task.FromResult(Activity);
    }

    public Task<RestoreScheduledModeResult> RestoreScheduledModeAsync(string assetId, string correlationId, CancellationToken cancellationToken)
    {
        LastCorrelationId = correlationId;
        return Task.FromResult(RestoreResult);
    }
}

sealed class FakeSmartPoleScenarioClient : ISmartPoleScenarioClient
{
    public int ResetCalls { get; private set; }

    public SmartPoleScenarioState? LastScenarioState { get; private set; }

    public Task ResetAsync(string correlationId, CancellationToken cancellationToken)
    {
        ResetCalls++;
        return Task.CompletedTask;
    }

    public Task ApplyScenarioAsync(SmartPoleScenarioState scenarioState, string correlationId, CancellationToken cancellationToken)
    {
        LastScenarioState = scenarioState;
        return Task.CompletedTask;
    }
}

sealed class FakeEnergyScenarioClient : IEnergyScenarioClient
{
    public int ResetCalls { get; private set; }

    public EnergyScenarioSyncRequest? LastRequest { get; private set; }

    public Task ResetAsync(string correlationId, CancellationToken cancellationToken)
    {
        ResetCalls++;
        return Task.CompletedTask;
    }

    public Task ApplyScenarioAsync(EnergyScenarioSyncRequest request, string correlationId, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.CompletedTask;
    }
}

sealed class FakeCommandCenterScenarioClient : ICommandCenterScenarioClient
{
    public int ResetCalls { get; private set; }

    public CommandCenterScenarioContext? LastScenarioContext { get; private set; }

    public Task ResetAsync(string correlationId, CancellationToken cancellationToken)
    {
        ResetCalls++;
        return Task.CompletedTask;
    }

    public Task ApplyScenarioAsync(CommandCenterScenarioContext scenarioContext, string correlationId, CancellationToken cancellationToken)
    {
        LastScenarioContext = scenarioContext;
        return Task.CompletedTask;
    }
}
