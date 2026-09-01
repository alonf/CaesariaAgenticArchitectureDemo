using CommandCenter.Api.Services;
using DemoScenario.Api.Services;
using EnergyHub.Api.Services;
using OperationsAgent.Api.Services;
using OperationsAgent.Contracts;
using Security.Contracts;

namespace Caesarea.Deterministic.Tests;

internal sealed class TestTimeProvider(DateTimeOffset? initial = null) : TimeProvider
{
    private DateTimeOffset _utcNow = initial ?? new DateTimeOffset(2026, 8, 23, 8, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan by) => _utcNow = _utcNow.Add(by);
}

internal sealed class FakeSmartPoleGateway : ISmartPoleGateway
{
    public required SmartPolePhysicalState PhysicalState { get; init; }

    public string? LastCorrelationId { get; private set; }

    public SetLampStateCommand? LastCommand { get; private set; }

    public Func<string, string, CancellationToken, Task<SmartPolePhysicalState>>? OnGetStateAsync { get; set; }

    public Func<SetLampStateCommand, string, CancellationToken, Task<SmartPoleCommandResult>>? OnSetLampStateAsync { get; set; }

    public Task<SmartPolePhysicalState> GetStateAsync(string assetId, string correlationId, CancellationToken cancellationToken)
    {
        LastCorrelationId = correlationId;

        if (OnGetStateAsync is not null)
        {
            return OnGetStateAsync(assetId, correlationId, cancellationToken);
        }

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
            command.DesiredIsOn
                ?? throw new InvalidOperationException("Fake SmartPole received a command without a desired state."),
            CommandExecutionStatus.Succeeded,
            correlationId,
            "Fake SmartPole confirmed the command.",
            PhysicalState.LastReportedAt));
    }
}

internal sealed class FakeEnergyHubGateway : IEnergyHubGateway
{
    public required EnergyOperationalTwin State { get; init; }

    public required IReadOnlyList<ActivityRecord> Activity { get; init; }

    public required RestoreScheduledModeResult RestoreResult { get; init; }

    public string? LastCorrelationId { get; private set; }

    public Func<string, string, CancellationToken, Task<EnergyOperationalTwin>>? OnGetStateAsync { get; set; }

    public Func<string, int, string, CancellationToken, Task<IReadOnlyList<ActivityRecord>>>? OnGetRecentActivityAsync { get; set; }

    public Func<string, string, CancellationToken, Task<RestoreScheduledModeResult>>? OnRestoreScheduledModeAsync { get; set; }

    public Task<EnergyOperationalTwin> GetStateAsync(string assetId, string correlationId, CancellationToken cancellationToken)
    {
        LastCorrelationId = correlationId;

        if (OnGetStateAsync is not null)
        {
            return OnGetStateAsync(assetId, correlationId, cancellationToken);
        }

        return Task.FromResult(State);
    }

    public Task<IReadOnlyList<ActivityRecord>> GetRecentActivityAsync(string assetId, int limit, string correlationId, CancellationToken cancellationToken)
    {
        LastCorrelationId = correlationId;

        if (OnGetRecentActivityAsync is not null)
        {
            return OnGetRecentActivityAsync(assetId, limit, correlationId, cancellationToken);
        }

        return Task.FromResult(Activity);
    }

    public Task<RestoreScheduledModeResult> RestoreScheduledModeAsync(string assetId, string correlationId, CancellationToken cancellationToken)
    {
        LastCorrelationId = correlationId;

        if (OnRestoreScheduledModeAsync is not null)
        {
            return OnRestoreScheduledModeAsync(assetId, correlationId, cancellationToken);
        }

        return Task.FromResult(RestoreResult);
    }
}

internal sealed class FakeSmartPoleScenarioClient : ISmartPoleScenarioClient
{
    public int ResetCalls { get; private set; }

    public SmartPoleScenarioState? LastScenarioState { get; private set; }

    public Func<string, CancellationToken, Task>? OnResetAsync { get; set; }

    public Func<SmartPoleScenarioState, string, CancellationToken, Task>? OnApplyScenarioAsync { get; set; }

    public Task ResetAsync(string correlationId, CancellationToken cancellationToken)
    {
        ResetCalls++;

        if (OnResetAsync is not null)
        {
            return OnResetAsync(correlationId, cancellationToken);
        }

        return Task.CompletedTask;
    }

    public Task ApplyScenarioAsync(SmartPoleScenarioState scenarioState, string correlationId, CancellationToken cancellationToken)
    {
        LastScenarioState = scenarioState;

        if (OnApplyScenarioAsync is not null)
        {
            return OnApplyScenarioAsync(scenarioState, correlationId, cancellationToken);
        }

        return Task.CompletedTask;
    }

    public SmartPoleBehaviorConfiguration Behavior { get; set; } = SmartPoleBehaviorConfiguration.Default;

    public Task<SmartPoleBehaviorConfiguration> GetBehaviorAsync(string correlationId, CancellationToken cancellationToken) =>
        Task.FromResult(Behavior);

    public Task<SmartPoleBehaviorConfiguration> UpdateBehaviorAsync(SmartPoleBehaviorConfiguration configuration, string correlationId, CancellationToken cancellationToken)
    {
        Behavior = configuration;
        return Task.FromResult(Behavior);
    }
}

internal sealed class FakeEnergyScenarioClient : IEnergyScenarioClient
{
    public int ResetCalls { get; private set; }

    public EnergyScenarioSyncRequest? LastRequest { get; private set; }

    public Func<string, CancellationToken, Task>? OnResetAsync { get; set; }

    public Func<EnergyScenarioSyncRequest, string, CancellationToken, Task>? OnApplyScenarioAsync { get; set; }

    public Task ResetAsync(string correlationId, CancellationToken cancellationToken)
    {
        ResetCalls++;

        if (OnResetAsync is not null)
        {
            return OnResetAsync(correlationId, cancellationToken);
        }

        return Task.CompletedTask;
    }

    public Task ApplyScenarioAsync(EnergyScenarioSyncRequest request, string correlationId, CancellationToken cancellationToken)
    {
        LastRequest = request;

        if (OnApplyScenarioAsync is not null)
        {
            return OnApplyScenarioAsync(request, correlationId, cancellationToken);
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeSecurityScenarioClient : ISecurityScenarioClient
{
    public int ResetCalls { get; private set; }

    public SecurityScenarioSyncRequest? LastRequest { get; private set; }

    public Task ResetAsync(string correlationId, CancellationToken cancellationToken)
    {
        ResetCalls++;
        return Task.CompletedTask;
    }

    public Task ApplyScenarioAsync(SecurityScenarioSyncRequest request, string correlationId, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.CompletedTask;
    }
}

internal sealed class FakeCommandCenterScenarioClient : ICommandCenterScenarioClient
{
    public int ResetCalls { get; private set; }

    public CommandCenterScenarioContext? LastScenarioContext { get; private set; }

    public Func<string, CancellationToken, Task>? OnResetAsync { get; set; }

    public Func<CommandCenterScenarioContext, string, CancellationToken, Task>? OnApplyScenarioAsync { get; set; }

    public Task ResetAsync(string correlationId, CancellationToken cancellationToken)
    {
        ResetCalls++;

        if (OnResetAsync is not null)
        {
            return OnResetAsync(correlationId, cancellationToken);
        }

        return Task.CompletedTask;
    }

    public Task ApplyScenarioAsync(CommandCenterScenarioContext scenarioContext, string correlationId, CancellationToken cancellationToken)
    {
        LastScenarioContext = scenarioContext;

        if (OnApplyScenarioAsync is not null)
        {
            return OnApplyScenarioAsync(scenarioContext, correlationId, cancellationToken);
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeCommandCenterStageClient : ICommandCenterStageClient
{
    public int ApplyCalls { get; private set; }

    public DemoStageStatus? LastStage { get; private set; }

    public DemoStageStatus CurrentStage { get; set; } = new(
        DemoStage.Deterministic,
        "Deterministic",
        "Deterministic stage",
        ["Deterministic"],
        DateTimeOffset.UtcNow,
        "startup");

    public Func<DemoStageStatus, string, CancellationToken, Task<DemoStageStatus>>? OnApplyStageAsync { get; set; }

    public Task<DemoStageStatus> GetCurrentStageAsync(string correlationId, CancellationToken cancellationToken) =>
        Task.FromResult(CurrentStage);

    public async Task<DemoStageStatus> ApplyStageAsync(DemoStageStatus stage, string correlationId, CancellationToken cancellationToken)
    {
        ApplyCalls++;
        LastStage = stage;

        if (OnApplyStageAsync is not null)
        {
            CurrentStage = await OnApplyStageAsync(stage, correlationId, cancellationToken);
            return CurrentStage;
        }

        CurrentStage = stage;
        return stage;
    }
}

internal sealed class FakeEnergyReadGateway : IEnergyReadGateway
{
    public required EnergyOperationalTwin State { get; init; }

    public required IReadOnlyList<ActivityRecord> Activity { get; init; }

    public Func<string, string, CancellationToken, Task<EnergyOperationalTwin>>? OnGetStateAsync { get; set; }

    public Func<string, int, string, CancellationToken, Task<IReadOnlyList<ActivityRecord>>>? OnGetRecentActivityAsync { get; set; }

    public Task<EnergyOperationalTwin> GetStateAsync(string assetId, string correlationId, CancellationToken cancellationToken) =>
        OnGetStateAsync is not null
            ? OnGetStateAsync(assetId, correlationId, cancellationToken)
            : Task.FromResult(State);

    public Task<IReadOnlyList<ActivityRecord>> GetRecentActivityAsync(string assetId, int limit, string correlationId, CancellationToken cancellationToken) =>
        OnGetRecentActivityAsync is not null
            ? OnGetRecentActivityAsync(assetId, limit, correlationId, cancellationToken)
            : Task.FromResult(Activity);
}

internal sealed class FakeEnergyCommandGateway : IEnergyCommandGateway
{
    public int RestoreCalls { get; private set; }

    public string? LastCorrelationId { get; private set; }

    public long? LastExpectedStateRevision { get; private set; }

    public Func<string, string, long, CancellationToken, Task<EnergyCommandOutcome>>? OnRestoreScheduledModeAsync { get; set; }

    public Task<EnergyCommandOutcome> RestoreScheduledModeAsync(
        string assetId,
        string correlationId,
        long expectedStateRevision,
        CancellationToken cancellationToken)
    {
        RestoreCalls++;
        LastCorrelationId = correlationId;
        LastExpectedStateRevision = expectedStateRevision;

        if (OnRestoreScheduledModeAsync is not null)
        {
            return OnRestoreScheduledModeAsync(assetId, correlationId, expectedStateRevision, cancellationToken);
        }

        return Task.FromResult(new EnergyCommandOutcome(
            CommandExecutionStatus.Succeeded, "Restored to scheduled mode.", PreconditionFailed: false));
    }
}

internal sealed class FakeWorkItemGateway : IWorkItemGateway
{
    private readonly List<MaintenanceWorkItem> _workItems = [];

    public IReadOnlyList<MaintenanceWorkItem> Created => _workItems;

    public Action? OnCreate { get; set; }

    public MaintenanceWorkItem Create(string assetId, string summary, string correlationId)
    {
        OnCreate?.Invoke();
        var workItem = new MaintenanceWorkItem($"WI-{_workItems.Count + 1}", assetId, summary, DateTimeOffset.UtcNow, correlationId);
        _workItems.Add(workItem);
        return workItem;
    }

    public IReadOnlyList<MaintenanceWorkItem> GetAll() => [.. _workItems];
}

internal sealed class FakeOperationsAgentStageClient : IOperationsAgentStageClient
{
    public int ApplyCalls { get; private set; }

    public DemoStageStatus? LastStage { get; private set; }

    public Func<DemoStageStatus, string, CancellationToken, Task>? OnApplyStageAsync { get; set; }

    public async Task ApplyStageAsync(DemoStageStatus stage, string correlationId, CancellationToken cancellationToken)
    {
        ApplyCalls++;
        LastStage = stage;

        if (OnApplyStageAsync is not null)
        {
            await OnApplyStageAsync(stage, correlationId, cancellationToken);
        }
    }
}
