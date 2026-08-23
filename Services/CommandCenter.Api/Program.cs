using CommandCenter.Api.Configuration;
using CommandCenter.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddOptions<CommandCenterApiOptions>()
    .BindConfiguration(CommandCenterApiOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHttpClient<IEnergyHubGateway, HttpEnergyHubGateway>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<CommandCenterApiOptions>>().Value;
    client.BaseAddress = new Uri(options.EnergyHubBaseUri, UriKind.Absolute);
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
admin.MapPost("/scenario", ApplyScenarioContext)
    .ValidateBody<CommandCenterScenarioContext>();

app.Run();

static async Task<IResult> GetSnapshotAsync(
    HttpContext context,
    string assetId,
    int? limit,
    CommandCenterService service,
    IOptions<CommandCenterApiOptions> options,
    CancellationToken cancellationToken)
{
    try
    {
        var correlationId = context.GetCorrelationId();
        var resolvedAssetId = ValidateAssetId(assetId);
        var resolvedLimit = ResolveLimit(limit, options.Value.DefaultSnapshotActivityLimit, options.Value.MaxActivityLimit);

        return TypedResults.Ok(await service.GetSnapshotAsync(resolvedAssetId, resolvedLimit, correlationId, cancellationToken));
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return TypedResults.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Invalid request",
            exception.Message,
            context.GetCorrelationId()));
    }
    catch (ArgumentException exception) when (string.IsNullOrWhiteSpace(assetId))
    {
        return TypedResults.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Invalid request",
            exception.Message,
            context.GetCorrelationId()));
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

static async Task<IResult> GetActivityAsync(
    HttpContext context,
    string assetId,
    int? limit,
    CommandCenterService service,
    IOptions<CommandCenterApiOptions> options,
    CancellationToken cancellationToken)
{
    try
    {
        var correlationId = context.GetCorrelationId();
        var resolvedAssetId = ValidateAssetId(assetId);
        var resolvedLimit = ResolveLimit(limit, options.Value.DefaultRecentActivityLimit, options.Value.MaxActivityLimit);

        return TypedResults.Ok(await service.GetRecentActivityAsync(resolvedAssetId, resolvedLimit, correlationId, cancellationToken));
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return TypedResults.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Invalid request",
            exception.Message,
            context.GetCorrelationId()));
    }
    catch (ArgumentException exception) when (string.IsNullOrWhiteSpace(assetId))
    {
        return TypedResults.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Invalid request",
            exception.Message,
            context.GetCorrelationId()));
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
    try
    {
        ValidateRequiredText(incidentId, nameof(incidentId));
    }
    catch (ArgumentException exception) when (string.IsNullOrWhiteSpace(incidentId))
    {
        return TypedResults.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Invalid request",
            exception.Message,
            context.GetCorrelationId()));
    }
    catch (ArgumentException exception)
    {
        return TypedResults.NotFound(ProblemDetailsFactory.Create(
            StatusCodes.Status404NotFound,
            "Asset not found",
            exception.Message,
            context.GetCorrelationId()));
    }

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
        var result = await service.RestoreScheduledModeAsync(ValidateAssetId(assetId), context.GetCorrelationId(), cancellationToken);

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
        return TypedResults.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Invalid request",
            exception.Message,
            context.GetCorrelationId()));
    }
}

static IResult Reset(HttpContext context, CommandCenterService service) =>
    TypedResults.Ok(service.Reset(context.GetCorrelationId()));

static IResult ApplyScenarioContext(HttpContext context, CommandCenterScenarioContext scenarioContext, CommandCenterService service)
{
    try
    {
        return TypedResults.Ok(service.ApplyScenarioContext(scenarioContext, context.GetCorrelationId()));
    }
    catch (ArgumentException exception)
    {
        return TypedResults.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Invalid request",
            exception.Message,
            context.GetCorrelationId()));
    }
}

static ProblemDetails CreateCommandProblem(int statusCode, string title, RestoreScheduledModeResult result)
{
    var problem = ProblemDetailsFactory.Create(statusCode, title, result.Summary, result.CorrelationId);
    problem.Extensions["assetId"] = result.AssetId;
    if (result.DesiredIsOn is not null)
    {
        problem.Extensions["desiredIsOn"] = result.DesiredIsOn;
    }

    if (result.ReportedIsOn is not null)
    {
        problem.Extensions["reportedIsOn"] = result.ReportedIsOn;
    }
    problem.Extensions["commandStatus"] = result.Status.ToString();
    return problem;
}

static string ValidateAssetId(string assetId)
{
    ValidateRequiredText(assetId, nameof(assetId));
    return assetId;
}

static int ResolveLimit(int? limit, int defaultValue, int maxValue)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(defaultValue);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxValue);

    if (defaultValue > maxValue)
    {
        throw new ArgumentOutOfRangeException(nameof(defaultValue), defaultValue, "The configured default activity limit cannot exceed the configured maximum limit.");
    }

    var resolvedLimit = limit ?? defaultValue;

    if (resolvedLimit < 1 || resolvedLimit > maxValue)
    {
        throw new ArgumentOutOfRangeException(nameof(limit), resolvedLimit, $"The activity limit must be between 1 and {maxValue}.");
    }

    return resolvedLimit;
}

static void ValidateRequiredText(string value, string parameterName) =>
    ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
