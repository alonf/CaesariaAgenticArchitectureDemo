using Caesarea.Contracts;
using Caesarea.ServiceDefaults;
using DemoScenario.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient<ISmartPoleScenarioClient, HttpSmartPoleScenarioClient>(client =>
{
    client.BaseAddress = new Uri("https+http://smartpole-simulator-api");
});
builder.Services.AddHttpClient<IEnergyScenarioClient, HttpEnergyScenarioClient>(client =>
{
    client.BaseAddress = new Uri("https+http://energyhub-api");
});
builder.Services.AddHttpClient<ICommandCenterScenarioClient, HttpCommandCenterScenarioClient>(client =>
{
    client.BaseAddress = new Uri("https+http://commandcenter-api");
});
builder.Services.AddSingleton<ScenarioCatalog>();
builder.Services.AddSingleton<ScenarioCoordinator>();

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
