using EnergyHub.Api.Configuration;
using EnergyHub.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddOptions<EnergyHubApiOptions>()
    .BindConfiguration(EnergyHubApiOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHttpClient<ISmartPoleGateway, HttpSmartPoleGateway>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<EnergyHubApiOptions>>().Value;
    client.BaseAddress = new Uri(options.SmartPoleBaseUri, UriKind.Absolute);
});
builder.Services.AddSingleton<EnergyHubService>();
// Hydrates the twin from the authoritative SmartPole at startup. Decisive only where no
// switchboard ever will: the deployed hub otherwise serves its constructor baseline forever.
builder.Services.AddHostedService<TwinHydration>();
builder.Services.AddSingleton<MrtrRequestStateStore>();
builder.Services.AddHttpContextAccessor();

// The Energy Hub owns and serves its streetlight tool over the Model Context Protocol: any
// MCP-capable client can discover and invoke it at this boundary.
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<EnergyMcpTools>()
    .WithTools<EnergyRestoreMcpTool>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.MapDefaultEndpoints();

var energy = app.MapGroup("/api/energy")
    .WithTags("Energy Hub");

energy.MapGet("/assets/{assetId}", GetState);
energy.MapGet("/assets/{assetId}/activity", GetActivity);
energy.MapPost("/assets/{assetId}/restore-scheduled-mode", RestoreScheduledModeAsync);

// The presenter-facing surface, and only where a presenter is driving.
//
// Deployed to Azure this service has public ingress, because the hosted agent runs outside its VNet
// and cannot reach it any other way. That ingress authenticates every caller, but authentication is
// not authorization: it checks that a token is valid and meant for this service, not which
// application role it carries. Mapping admin/reset behind it would put "erase the running scenario"
// one valid token away, and the MCP server alongside it. The reads above are what a hosted agent
// needs; nothing here is.
if (app.Services.GetRequiredService<IOptions<EnergyHubApiOptions>>().Value.EnableDemoControlSurface)
{
    app.MapMcp("/mcp");
    app.MapDemoBreakpoints(DemoSnippets.McpServer, DemoSnippets.InteractiveInput);

    var admin = energy.MapGroup("/admin");
    admin.MapPost("/reset", ResetAsync);
    admin.MapPost("/scenario", ApplyScenarioAsync)
        .ValidateBody<EnergyScenarioSyncRequest>();
}

await app.RunAsync();

static IResult GetState(HttpContext context, string assetId, EnergyHubService hub)
{
    try
    {
        return TypedResults.Ok(hub.GetState(ValidateAssetId(assetId)));
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

static IResult GetActivity(HttpContext context, string assetId, int? limit, EnergyHubService hub, IOptions<EnergyHubApiOptions> options)
{
    try
    {
        var resolvedAssetId = ValidateAssetId(assetId);
        var resolvedLimit = ResolveLimit(limit, options.Value.DefaultRecentActivityLimit, options.Value.MaxActivityLimit);
        return TypedResults.Ok(hub.GetRecentActivity(resolvedAssetId, resolvedLimit));
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

static async Task<IResult> RestoreScheduledModeAsync(HttpContext context, string assetId, long? expectedStateRevision, EnergyHubService hub, CancellationToken cancellationToken)
{
    try
    {
        var result = await hub.RestoreScheduledModeAsync(
            ValidateAssetId(assetId),
            context.GetCorrelationId(),
            cancellationToken,
            expectedStateRevision);

        // A refused precondition is its own answer, not a downstream failure: the caller decided
        // against a state that has since moved, and must re-validate before commanding again. The
        // machine-readable marker is what lets a caller tell the two apart.
        if (result.PreconditionFailed)
        {
            var preconditionProblem = CreateCommandProblem(
                StatusCodes.Status409Conflict,
                "Restore scheduled mode precondition failed",
                result);
            preconditionProblem.Extensions[EnergyCommandProblem.PreconditionFailedExtension] = true;
            return TypedResults.Conflict(preconditionProblem);
        }

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
    ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
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
