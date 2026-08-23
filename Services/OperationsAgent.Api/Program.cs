using Azure;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Extensions.Options;
using OperationsAgent.Api.Configuration;
using OperationsAgent.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddOptions<OperationsAgentApiOptions>()
    .BindConfiguration(OperationsAgentApiOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<IEnergyReadGateway, HttpEnergyReadGateway>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OperationsAgentApiOptions>>().Value;
    client.BaseAddress = new Uri(options.EnergyHubBaseUri, UriKind.Absolute);
});
builder.Services.AddHttpClient<ICommandCenterReadGateway, HttpCommandCenterReadGateway>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OperationsAgentApiOptions>>().Value;
    client.BaseAddress = new Uri(options.CommandCenterBaseUri, UriKind.Absolute);
});

// AIProjectClient and DefaultAzureCredential construction is lazy: neither performs network or authentication
// calls until the agent actually runs, so the service still starts cleanly in Deterministic mode even when no
// Azure credential is available in the current environment.
builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OperationsAgentApiOptions>>().Value;
    return new AIProjectClient(new Uri(options.FoundryProjectEndpoint, UriKind.Absolute), new DefaultAzureCredential());
});
builder.Services.AddSingleton<IInvestigationAgentRunner>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OperationsAgentApiOptions>>().Value;
    return new FoundryInvestigationAgentRunner(
        serviceProvider.GetRequiredService<AIProjectClient>(),
        options.ModelDeploymentName,
        options.AgentName,
        serviceProvider.GetRequiredService<ILogger<FoundryInvestigationAgentRunner>>());
});
builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OperationsAgentApiOptions>>().Value;
    return new InvestigationService(
        serviceProvider.GetRequiredService<IEnergyReadGateway>(),
        serviceProvider.GetRequiredService<ICommandCenterReadGateway>(),
        serviceProvider.GetRequiredService<IInvestigationAgentRunner>(),
        serviceProvider.GetRequiredService<ILoggerFactory>(),
        options.AgentName,
        serviceProvider.GetRequiredService<TimeProvider>(),
        serviceProvider.GetRequiredService<ILogger<InvestigationService>>());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapDefaultEndpoints();

var operationsAgent = app.MapGroup("/api/operations-agent")
    .WithTags("Operations Agent");

operationsAgent.MapPost("/assets/{assetId}/investigate", InvestigateAsync);

app.Run();

static async Task<IResult> InvestigateAsync(
    HttpContext context,
    string assetId,
    InvestigationService service,
    ILogger<InvestigationEndpoint> logger,
    CancellationToken cancellationToken)
{
    try
    {
        var result = await service.InvestigateAsync(ValidateAssetId(assetId), context.GetCorrelationId(), cancellationToken);
        return TypedResults.Ok(result);
    }
    catch (ArgumentException exception) when (string.IsNullOrWhiteSpace(assetId))
    {
        return TypedResults.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Invalid request",
            exception.Message,
            context.GetCorrelationId()));
    }
    catch (InvestigationResponseFormatException exception)
    {
        return TypedResults.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status502BadGateway,
            "Operations Agent response was invalid",
            $"The Operations Agent did not return a response that satisfies the required evidence schema: {exception.Message}",
            context.GetCorrelationId()));
    }
    catch (HttpRequestException exception)
    {
        return TypedResults.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status502BadGateway,
            "Operations Agent evidence unavailable",
            $"A required read-only evidence source could not be reached: {exception.Message}",
            context.GetCorrelationId()));
    }
    catch (InvestigationEvidenceUnavailableException exception)
    {
        InvestigationEndpointLog.EvidenceUnavailable(logger, assetId, context.GetCorrelationId(), exception);
        return TypedResults.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status502BadGateway,
            "Operations Agent evidence unavailable",
            exception.Message,
            context.GetCorrelationId()));
    }
    catch (CredentialUnavailableException exception)
    {
        InvestigationEndpointLog.AuthenticationFailed(logger, assetId, context.GetCorrelationId(), exception);
        return TypedResults.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status503ServiceUnavailable,
            "Operations Agent authentication unavailable",
            "No supported Azure credential is available to invoke Microsoft Foundry.",
            context.GetCorrelationId()));
    }
    catch (AuthenticationFailedException exception)
    {
        InvestigationEndpointLog.AuthenticationFailed(logger, assetId, context.GetCorrelationId(), exception);
        return TypedResults.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status502BadGateway,
            "Operations Agent authentication failed",
            "Microsoft Foundry authentication failed for the Operations Agent.",
            context.GetCorrelationId()));
    }
    catch (RequestFailedException exception)
    {
        var statusCode = exception.Status is >= 400 and < 600 ? exception.Status : StatusCodes.Status502BadGateway;
        return TypedResults.Problem(ProblemDetailsFactory.Create(
            statusCode,
            "Operations Agent invocation failed",
            $"Microsoft Foundry rejected the investigation request: {exception.Message}",
            context.GetCorrelationId()));
    }
}

static string ValidateAssetId(string assetId)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
    return assetId;
}

internal sealed class InvestigationEndpoint;

internal static partial class InvestigationEndpointLog
{
    [LoggerMessage(
        EventId = 2450,
        Level = LogLevel.Warning,
        Message = "Operations Agent investigation for asset {AssetId} could not obtain minimum evidence. CorrelationId: {CorrelationId}.")]
    internal static partial void EvidenceUnavailable(ILogger logger, string assetId, string correlationId, Exception exception);

    [LoggerMessage(
        EventId = 2451,
        Level = LogLevel.Warning,
        Message = "Operations Agent investigation for asset {AssetId} could not authenticate to Microsoft Foundry. CorrelationId: {CorrelationId}.")]
    internal static partial void AuthenticationFailed(ILogger logger, string assetId, string correlationId, Exception exception);
}
