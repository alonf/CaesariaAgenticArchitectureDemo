using Caesarea.Contracts;

namespace DemoScenario.Api.Services;

public sealed class ScenarioCoordinator
{
    private readonly object _gate = new();
    private readonly ISmartPoleScenarioClient _smartpoleScenarioClient;
    private readonly IEnergyScenarioClient _energyScenarioClient;
    private readonly ICommandCenterScenarioClient _commandCenterScenarioClient;
    private readonly ScenarioCatalog _scenarioCatalog;
    private readonly TimeProvider _timeProvider;
    private ScenarioStatus _currentScenario;

    public ScenarioCoordinator(
        ISmartPoleScenarioClient smartpoleScenarioClient,
        IEnergyScenarioClient energyScenarioClient,
        ICommandCenterScenarioClient commandCenterScenarioClient,
        ScenarioCatalog scenarioCatalog,
        TimeProvider timeProvider)
    {
        _smartpoleScenarioClient = smartpoleScenarioClient;
        _energyScenarioClient = energyScenarioClient;
        _commandCenterScenarioClient = commandCenterScenarioClient;
        _scenarioCatalog = scenarioCatalog;
        _timeProvider = timeProvider;

        var descriptor = _scenarioCatalog.GetDescriptor(ScenarioId.NormalOperation);
        _currentScenario = new ScenarioStatus(
            descriptor.Id,
            descriptor.Name,
            descriptor.Description,
            _timeProvider.GetUtcNow(),
            "startup");
    }

    public ScenarioStatus GetCurrentScenario()
    {
        lock (_gate)
        {
            return _currentScenario;
        }
    }

    public Task<ScenarioApplicationResult> ResetAsync(string correlationId, CancellationToken cancellationToken) =>
        ApplyAsync(ScenarioId.NormalOperation, correlationId, cancellationToken);

    public async Task<ScenarioApplicationResult> ApplyAsync(ScenarioId scenarioId, string correlationId, CancellationToken cancellationToken)
    {
        var recipe = _scenarioCatalog.GetRecipe(scenarioId);

        await _smartpoleScenarioClient.ResetAsync(correlationId, cancellationToken);
        await _smartpoleScenarioClient.ApplyScenarioAsync(recipe.SmartPoleState, correlationId, cancellationToken);

        await _energyScenarioClient.ResetAsync(correlationId, cancellationToken);
        await _energyScenarioClient.ApplyScenarioAsync(recipe.EnergyState, correlationId, cancellationToken);

        await _commandCenterScenarioClient.ResetAsync(correlationId, cancellationToken);

        var status = new ScenarioStatus(
            recipe.Descriptor.Id,
            recipe.Descriptor.Name,
            recipe.Descriptor.Description,
            _timeProvider.GetUtcNow(),
            correlationId);

        await _commandCenterScenarioClient.ApplyScenarioAsync(
            new CommandCenterScenarioContext(status, recipe.OpenIncident, recipe.Activity),
            correlationId,
            cancellationToken);

        lock (_gate)
        {
            _currentScenario = status;
        }

        return new ScenarioApplicationResult(status, recipe.ApplicationSummary);
    }
}
