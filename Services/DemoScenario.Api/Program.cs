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
builder.Services.AddSingleton<ScenarioCatalog>();
builder.Services.AddSingleton<ScenarioCoordinator>();
builder.Services.AddSingleton<StageCatalog>();
builder.Services.AddSingleton<StageCoordinator>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

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

stages.MapGet(string.Empty, GetStageCatalog);
stages.MapGet("/current", GetCurrentStage);
stages.MapPost("/apply/{stage}", ApplyStageAsync);

app.Run();

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

static IResult GetStageCatalog(StageCatalog catalog, StageCoordinator coordinator) =>
    TypedResults.Ok(new DemoStageCatalogResponse(catalog.GetAll(), coordinator.GetCurrentStage()));

static IResult GetCurrentStage(StageCoordinator coordinator) =>
    TypedResults.Ok(coordinator.GetCurrentStage());

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
