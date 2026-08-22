using Caesarea.Contracts;
using Caesarea.ServiceDefaults;
using CommandCenter.Api.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient<IEnergyHubGateway, HttpEnergyHubGateway>(client =>
{
    client.BaseAddress = new Uri("https+http://energyhub-api");
});
builder.Services.AddSingleton<IncidentModule>();
builder.Services.AddSingleton<ActivityTimelineModule>();
builder.Services.AddSingleton<ScenarioContextModule>();
builder.Services.AddSingleton<SpatialContextModule>();
builder.Services.AddSingleton<CommandCenterService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapDefaultEndpoints();

var commandCenter = app.MapGroup("/api/command-center")
    .WithTags("Command Center");

commandCenter.MapGet("/snapshot/{assetId}", GetSnapshotAsync);
commandCenter.MapGet("/activity/{assetId}", GetActivityAsync);
commandCenter.MapGet("/incidents/{incidentId}", GetIncident);
commandCenter.MapPost("/assets/{assetId}/restore-scheduled-mode", RestoreScheduledModeAsync);

var admin = commandCenter.MapGroup("/admin");
admin.MapPost("/reset", Reset);
admin.MapPost("/scenario", ApplyScenarioContext);

app.Run();

static async Task<IResult> GetSnapshotAsync(HttpContext context, string assetId, int? limit, CommandCenterService service, CancellationToken cancellationToken)
{
    try
    {
        return TypedResults.Ok(await service.GetSnapshotAsync(assetId, limit ?? 12, context.GetCorrelationId(), cancellationToken));
    }
    catch (ArgumentException exception)
    {
        return TypedResults.NotFound(ProblemDetailsFactory.Create(
            StatusCodes.Status404NotFound,
            "Asset not found",
            exception.Message,
            context.GetCorrelationId()));
    }
}

static async Task<IResult> GetActivityAsync(HttpContext context, string assetId, int? limit, CommandCenterService service, CancellationToken cancellationToken)
{
    try
    {
        return TypedResults.Ok(await service.GetRecentActivityAsync(assetId, limit ?? 20, context.GetCorrelationId(), cancellationToken));
    }
    catch (ArgumentException exception)
    {
        return TypedResults.NotFound(ProblemDetailsFactory.Create(
            StatusCodes.Status404NotFound,
            "Asset not found",
            exception.Message,
            context.GetCorrelationId()));
    }
}

static IResult GetIncident(HttpContext context, string incidentId, CommandCenterService service)
{
    var incident = service.GetIncident(incidentId);

    return incident is null
        ? TypedResults.NotFound(ProblemDetailsFactory.Create(
            StatusCodes.Status404NotFound,
            "Incident not found",
            $"Incident {incidentId} does not exist in the Stage 0 Command Center store.",
            context.GetCorrelationId()))
        : TypedResults.Ok(incident);
}

static async Task<IResult> RestoreScheduledModeAsync(HttpContext context, string assetId, CommandCenterService service, CancellationToken cancellationToken)
{
    try
    {
        var result = await service.RestoreScheduledModeAsync(assetId, context.GetCorrelationId(), cancellationToken);

        return result.Status switch
        {
            CommandExecutionStatus.Succeeded => TypedResults.Ok(result),
            CommandExecutionStatus.TimedOut => TypedResults.Problem(CreateCommandProblem(
                StatusCodes.Status504GatewayTimeout,
                "Restore scheduled mode timed out",
                result)),
            _ => TypedResults.Conflict(CreateCommandProblem(
                StatusCodes.Status409Conflict,
                "Restore scheduled mode failed",
                result))
        };
    }
    catch (ArgumentException exception)
    {
        return TypedResults.NotFound(ProblemDetailsFactory.Create(
            StatusCodes.Status404NotFound,
            "Asset not found",
            exception.Message,
            context.GetCorrelationId()));
    }
}

static IResult Reset(HttpContext context, CommandCenterService service) =>
    TypedResults.Ok(service.Reset(context.GetCorrelationId()));

static IResult ApplyScenarioContext(HttpContext context, CommandCenterScenarioContext scenarioContext, CommandCenterService service) =>
    TypedResults.Ok(service.ApplyScenarioContext(scenarioContext, context.GetCorrelationId()));

static ProblemDetails CreateCommandProblem(int statusCode, string title, RestoreScheduledModeResult result)
{
    var problem = ProblemDetailsFactory.Create(statusCode, title, result.Summary, result.CorrelationId);
    problem.Extensions["assetId"] = result.AssetId;
    problem.Extensions["desiredIsOn"] = result.DesiredIsOn;
    problem.Extensions["reportedIsOn"] = result.ReportedIsOn;
    problem.Extensions["commandStatus"] = result.Status.ToString();
    return problem;
}
