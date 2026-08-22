using Caesarea.Contracts;
using Caesarea.ServiceDefaults;
using EnergyHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient<ISmartPoleGateway, HttpSmartPoleGateway>(client =>
{
    client.BaseAddress = new Uri("https+http://smartpole-simulator-api");
});
builder.Services.AddSingleton<EnergyHubService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapDefaultEndpoints();

var energy = app.MapGroup("/api/energy")
    .WithTags("Energy Hub");

energy.MapGet("/assets/{assetId}", GetState);
energy.MapGet("/assets/{assetId}/activity", GetActivity);
energy.MapPost("/assets/{assetId}/restore-scheduled-mode", RestoreScheduledModeAsync);

var admin = energy.MapGroup("/admin");
admin.MapPost("/reset", ResetAsync);
admin.MapPost("/scenario", ApplyScenarioAsync);

app.Run();

static IResult GetState(HttpContext context, string assetId, EnergyHubService hub)
{
    try
    {
        return TypedResults.Ok(hub.GetState(assetId));
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

static IResult GetActivity(HttpContext context, string assetId, int? limit, EnergyHubService hub)
{
    try
    {
        return TypedResults.Ok(hub.GetRecentActivity(assetId, limit ?? 20));
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

static async Task<IResult> RestoreScheduledModeAsync(HttpContext context, string assetId, EnergyHubService hub, CancellationToken cancellationToken)
{
    try
    {
        var result = await hub.RestoreScheduledModeAsync(assetId, context.GetCorrelationId(), cancellationToken);

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

static Task<IResult> ResetAsync(HttpContext context, EnergyHubService hub, CancellationToken cancellationToken) =>
    WrapOkAsync(hub.ResetAsync(context.GetCorrelationId(), cancellationToken));

static Task<IResult> ApplyScenarioAsync(HttpContext context, EnergyScenarioSyncRequest request, EnergyHubService hub, CancellationToken cancellationToken) =>
    WrapOkAsync(hub.ApplyScenarioAsync(request, context.GetCorrelationId(), cancellationToken));

static async Task<IResult> WrapOkAsync<T>(Task<T> task) =>
    TypedResults.Ok(await task);

static ProblemDetails CreateCommandProblem(int statusCode, string title, RestoreScheduledModeResult result)
{
    var problem = ProblemDetailsFactory.Create(statusCode, title, result.Summary, result.CorrelationId);
    problem.Extensions["assetId"] = result.AssetId;
    problem.Extensions["desiredIsOn"] = result.DesiredIsOn;
    problem.Extensions["reportedIsOn"] = result.ReportedIsOn;
    problem.Extensions["commandStatus"] = result.Status.ToString();
    return problem;
}
