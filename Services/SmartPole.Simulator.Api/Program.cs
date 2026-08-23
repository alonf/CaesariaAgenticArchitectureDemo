using SmartPole.Simulator.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<SmartPoleSimulatorService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapDefaultEndpoints();

var smartpole = app.MapGroup("/api/smartpole")
    .WithTags("SmartPole Simulator");

smartpole.MapGet("/state/{assetId}", GetState);
smartpole.MapPost("/reset", Reset);
smartpole.MapPost("/scenario", ApplyScenario)
    .ValidateBody<SmartPoleScenarioState>();
smartpole.MapPost("/configuration", UpdateConfiguration)
    .ValidateBody<SmartPoleBehaviorConfiguration>();
smartpole.MapPost("/commands/lamp-state", SetLampStateAsync)
    .ValidateBody<SetLampStateCommand>();

app.Run();

static IResult GetState(HttpContext context, string assetId, SmartPoleSimulatorService simulator)
{
    try
    {
        return TypedResults.Ok(simulator.GetState(ValidateAssetId(assetId)));
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

static IResult Reset(HttpContext context, SmartPoleSimulatorService simulator) =>
    TypedResults.Ok(simulator.Reset(context.GetCorrelationId()));

static IResult ApplyScenario(HttpContext context, SmartPoleScenarioState scenarioState, SmartPoleSimulatorService simulator)
{
    try
    {
        return TypedResults.Ok(simulator.ApplyScenario(scenarioState, context.GetCorrelationId()));
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

static IResult UpdateConfiguration(HttpContext context, SmartPoleBehaviorConfiguration configuration, SmartPoleSimulatorService simulator)
{
    try
    {
        return TypedResults.Ok(simulator.UpdateConfiguration(configuration, context.GetCorrelationId()));
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

static async Task<IResult> SetLampStateAsync(HttpContext context, SetLampStateCommand command, SmartPoleSimulatorService simulator, CancellationToken cancellationToken)
{
    try
    {
        var result = await simulator.SetLampStateAsync(command, context.GetCorrelationId(), cancellationToken);

        return result.Status switch
        {
            CommandExecutionStatus.Succeeded => TypedResults.Ok(result),
            CommandExecutionStatus.TimedOut => TypedResults.Problem(ProblemDetailsFactory.Create(
                StatusCodes.Status504GatewayTimeout,
                "SmartPole command timed out",
                result.Summary,
                result.CorrelationId)),
            _ => TypedResults.Conflict(ProblemDetailsFactory.Create(
                StatusCodes.Status409Conflict,
                "SmartPole command failed",
                result.Summary,
                result.CorrelationId))
        };
    }
    catch (ArgumentException exception) when (string.IsNullOrWhiteSpace(command.AssetId))
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

static string ValidateAssetId(string assetId)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
    return assetId;
}
