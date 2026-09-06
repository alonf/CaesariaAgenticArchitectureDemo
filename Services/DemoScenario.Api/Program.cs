using DemoScenario.Api.Configuration;
using DemoScenario.Api.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddOptions<DemoScenarioApiOptions>()
    .BindConfiguration(DemoScenarioApiOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHttpClient<ISmartPoleScenarioClient, HttpSmartPoleScenarioClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoScenarioApiOptions>>().Value;
    client.BaseAddress = new Uri(options.SmartPoleBaseUri, UriKind.Absolute);
});
builder.Services.AddHttpClient<IEnergyScenarioClient, HttpEnergyScenarioClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoScenarioApiOptions>>().Value;
    client.BaseAddress = new Uri(options.EnergyHubBaseUri, UriKind.Absolute);
});
builder.Services.AddHttpClient<ISecurityScenarioClient, HttpSecurityScenarioClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoScenarioApiOptions>>().Value;
    client.BaseAddress = new Uri(options.SecurityHubBaseUri, UriKind.Absolute);
});
builder.Services.AddHttpClient<IWorkforceScenarioClient, HttpWorkforceScenarioClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoScenarioApiOptions>>().Value;
    client.BaseAddress = new Uri(options.WorkforceHubBaseUri, UriKind.Absolute);
});
builder.Services.AddHttpClient<ICommandCenterScenarioClient, HttpCommandCenterScenarioClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoScenarioApiOptions>>().Value;
    client.BaseAddress = new Uri(options.CommandCenterBaseUri, UriKind.Absolute);
});
builder.Services.AddHttpClient<ICommandCenterStageClient, HttpCommandCenterStageClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoScenarioApiOptions>>().Value;
    client.BaseAddress = new Uri(options.CommandCenterBaseUri, UriKind.Absolute);
});
builder.Services.AddHttpClient<IOperationsAgentStageClient, HttpOperationsAgentStageClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoScenarioApiOptions>>().Value;
    client.BaseAddress = new Uri(options.OperationsAgentBaseUri, UriKind.Absolute);
});
builder.Services.AddSingleton<ScenarioCatalog>();
builder.Services.AddSingleton<ScenarioCoordinator>();
builder.Services.AddSingleton<StageCatalog>();
builder.Services.AddSingleton<StageCoordinator>();
// The director drives the same coordinators the buttons do, plus the agent's presenter switches.
builder.Services.AddHttpClient<IOperationsAgentSwitchClient, HttpOperationsAgentSwitchClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoScenarioApiOptions>>().Value;
    client.BaseAddress = new Uri(options.OperationsAgentBaseUri, UriKind.Absolute);
});
builder.Services.AddSingleton<IStageApplier>(serviceProvider => serviceProvider.GetRequiredService<StageCoordinator>());
builder.Services.AddSingleton<IScenarioApplier>(serviceProvider => serviceProvider.GetRequiredService<ScenarioCoordinator>());
builder.Services.AddSingleton<DemoDirector>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.MapDefaultEndpoints();

var scenarios = app.MapGroup("/api/demo-scenarios")
    .WithTags("Demo Scenarios");

scenarios.MapGet(string.Empty, GetCatalog);
scenarios.MapGet("/current", GetCurrent);
scenarios.MapPost("/apply/{scenarioId}", ApplyScenarioAsync);
scenarios.MapPost("/reset", ResetAsync);

var stages = app.MapGroup("/api/demo-stage")
    .WithTags("Demo Stage");

stages.MapGet(string.Empty, GetStageCatalogAsync);
stages.MapGet("/current", GetCurrentStage);
stages.MapPost("/apply/{stage}", ApplyStageAsync);
stages.MapGet("/readiness/{stage}", GetStageReadinessAsync);
stages.MapPost("/prepare/{stage}", PrepareStageAsync);

// Narrow presenter surface over the SmartPole behavior configuration (Stage 0 failure controls).
var simulatorBehavior = app.MapGroup("/api/simulator-behavior")
    .WithTags("Simulator Behavior");

simulatorBehavior.MapGet(string.Empty, GetSimulatorBehaviorAsync);
simulatorBehavior.MapPost(string.Empty, UpdateSimulatorBehaviorAsync);

await app.RunAsync();

static async Task<IResult> GetStageReadinessAsync(HttpContext context, DemoStage stage, DemoDirector director, CancellationToken cancellationToken)
{
    try
    {
        return TypedResults.Ok(await director.GetReadinessAsync(stage, context.GetCorrelationId(), cancellationToken));
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return TypedResults.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Unknown demo stage",
            exception.Message,
            context.GetCorrelationId()));
    }
}

static async Task<IResult> PrepareStageAsync(HttpContext context, DemoStage stage, DemoDirector director, CancellationToken cancellationToken)
{
    try
    {
        return TypedResults.Ok(await director.PrepareAsync(stage, context.GetCorrelationId(), cancellationToken));
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return TypedResults.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Unknown demo stage",
            exception.Message,
            context.GetCorrelationId()));
    }
    catch (HttpRequestException exception)
    {
        return TypedResults.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status502BadGateway,
            "Beat could not be prepared",
            $"A service the beat depends on could not be reached: {exception.Message}",
            context.GetCorrelationId()));
    }
}
static IResult GetCatalog(ScenarioCatalog catalog, ScenarioCoordinator coordinator) =>
    TypedResults.Ok(new ScenarioCatalogResponse(catalog.GetAll(), coordinator.GetCurrentScenario()));

static IResult GetCurrent(ScenarioCoordinator coordinator) =>
    TypedResults.Ok(coordinator.GetCurrentScenario());

static async Task<IResult> ApplyScenarioAsync(HttpContext context, ScenarioId scenarioId, ScenarioCoordinator coordinator, CancellationToken cancellationToken)
{
    try
    {
        return TypedResults.Ok(await coordinator.ApplyAsync(scenarioId, context.GetCorrelationId(), cancellationToken));
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return TypedResults.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Unknown scenario",
            exception.Message,
            context.GetCorrelationId()));
    }
}

static async Task<IResult> ResetAsync(HttpContext context, ScenarioCoordinator coordinator, CancellationToken cancellationToken) =>
    TypedResults.Ok(await coordinator.ResetAsync(context.GetCorrelationId(), cancellationToken));

static async Task<IResult> GetStageCatalogAsync(
    HttpContext context,
    StageCatalog catalog,
    StageCoordinator coordinator,
    CancellationToken cancellationToken) =>
    TypedResults.Ok(new DemoStageCatalogResponse(
        catalog.GetAll(),
        await coordinator.GetCurrentStageAsync(context.GetCorrelationId(), cancellationToken)));

static async Task<IResult> GetCurrentStage(
    HttpContext context,
    StageCoordinator coordinator,
    CancellationToken cancellationToken) =>
    TypedResults.Ok(await coordinator.GetCurrentStageAsync(context.GetCorrelationId(), cancellationToken));

static async Task<IResult> GetSimulatorBehaviorAsync(HttpContext context, ISmartPoleScenarioClient smartPoleClient, CancellationToken cancellationToken)
{
    try
    {
        var configuration = await smartPoleClient.GetBehaviorAsync(context.GetCorrelationId(), cancellationToken);
        return TypedResults.Ok(new SimulatorBehaviorSettings(configuration.CommandDelayMs, configuration.SimulateTimeout, configuration.SimulateFailure));
    }
    catch (HttpRequestException exception)
    {
        return TypedResults.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status502BadGateway,
            "Simulator unavailable",
            $"The SmartPole simulator could not be reached: {exception.Message}",
            context.GetCorrelationId()));
    }
}

static async Task<IResult> UpdateSimulatorBehaviorAsync(
    HttpContext context,
    SimulatorBehaviorSettings settings,
    ISmartPoleScenarioClient smartPoleClient,
    CancellationToken cancellationToken)
{
    // The presenter delay is capped below the command clients' 10-second attempt timeout: a longer
    // delay would surface as client-side aborts, which is what the Simulate timeout toggle is for.
    if (settings.CommandDelayMs is < 0 or > 8000)
    {
        return TypedResults.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Invalid simulator behavior",
            "CommandDelayMs must be between 0 and 8000 milliseconds; use Simulate timeout for timeout behavior.",
            context.GetCorrelationId()));
    }

    try
    {
        var configuration = await smartPoleClient.UpdateBehaviorAsync(
            new SmartPoleBehaviorConfiguration(settings.CommandDelayMs, settings.SimulateTimeout, settings.SimulateFailure),
            context.GetCorrelationId(),
            cancellationToken);
        return TypedResults.Ok(new SimulatorBehaviorSettings(configuration.CommandDelayMs, configuration.SimulateTimeout, configuration.SimulateFailure));
    }
    catch (HttpRequestException exception)
    {
        return TypedResults.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status502BadGateway,
            "Simulator unavailable",
            $"The SmartPole simulator could not be reached: {exception.Message}",
            context.GetCorrelationId()));
    }
}

static async Task<IResult> ApplyStageAsync(HttpContext context, DemoStage stage, StageCoordinator coordinator, CancellationToken cancellationToken)
{
    try
    {
        return TypedResults.Ok(await coordinator.ApplyAsync(stage, context.GetCorrelationId(), cancellationToken));
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return TypedResults.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Unknown demo stage",
            exception.Message,
            context.GetCorrelationId()));
    }
}
