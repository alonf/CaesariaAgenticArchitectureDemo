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

// AIProjectClient and DefaultAzureCredential construction is lazy: neither performs network or authentication
// calls until the agent actually runs, so the service still starts cleanly in Deterministic mode even when no
// Azure credential is available in the current environment.
builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OperationsAgentApiOptions>>().Value;
    return new AIProjectClient(new Uri(options.FoundryProjectEndpoint, UriKind.Absolute), new DefaultAzureCredential());
});
builder.Services.AddSingleton(serviceProvider =>
{
    var initialStage = Enum.TryParse<DemoStage>(builder.Configuration["DemoStage"], out var configuredStage)
        ? configuredStage
        : DemoStage.Deterministic;
    return new DemoStageGate(serviceProvider.GetRequiredService<TimeProvider>(), initialStage);
});
builder.Services.AddSingleton<AgentSessionStore>();
builder.Services.AddSingleton<IOperationsAgent>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OperationsAgentApiOptions>>().Value;
    return new FoundryOperationsAgent(
        serviceProvider.GetRequiredService<AIProjectClient>(),
        serviceProvider.GetRequiredService<IEnergyReadGateway>(),
        serviceProvider.GetRequiredService<AgentSessionStore>(),
        options.ModelDeploymentName,
        options.AgentName,
        options.MaxFunctionIterations,
        TimeSpan.FromSeconds(options.RequestTimeoutSeconds),
        serviceProvider.GetRequiredService<ILoggerFactory>(),
        serviceProvider.GetRequiredService<ILogger<FoundryOperationsAgent>>());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.MapDefaultEndpoints();
app.MapDemoBreakpoints(DemoSnippets.AgentCreation, DemoSnippets.FunctionTool, DemoSnippets.Session);

var operationsAgent = app.MapGroup("/api/operations-agent")
    .WithTags("Operations Agent");

operationsAgent.MapPost("/ask", AskAsync);

// Stage propagation from the presenter switchboard; the gate keeps Stage 0 from ever reaching Foundry.
var demoStage = app.MapGroup("/api/operations-agent/demo-stage")
    .WithTags("Demo Stage");

demoStage.MapGet("/", (DemoStageGate stageGate) => TypedResults.Ok(stageGate.GetCurrent()));
demoStage.MapPost("/", (DemoStageStatus stage, DemoStageGate stageGate) => TypedResults.Ok(stageGate.SetCurrent(stage)));

await app.RunAsync();

static async Task<IResult> AskAsync(
    HttpContext context,
    OperationsAgentRequest request,
    IOperationsAgent agent,
    DemoStageGate stageGate,
    IOptions<OperationsAgentApiOptions> options,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken)
{
    var logger = loggerFactory.CreateLogger("OperationsAgent.Api.AskEndpoint");

    if (!stageGate.IsAgentEnabled)
    {
        return TypedResults.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status409Conflict,
            "Operations Agent disabled in the current demo stage",
            $"The Operations Agent requires the First Agent stage; the current stage is {stageGate.GetCurrent().Name}.",
            context.GetCorrelationId()));
    }
    try
    {
        const int maxQuestionLength = 1000;
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Question);

        if (request.Question.Length > maxQuestionLength)
        {
            return TypedResults.BadRequest(ProblemDetailsFactory.Create(
                StatusCodes.Status400BadRequest,
                "Invalid request",
                $"The question must be at most {maxQuestionLength} characters.",
                context.GetCorrelationId()));
        }

        var correlationId = context.GetCorrelationId();

        // Conversational follow-ups become available at the Session stage; earlier stages run
        // every question as an independent request even when a client sends a session identifier.
        var sessionId = stageGate.GetCurrent().Id >= DemoStage.Session ? request.SessionId : null;
        var reply = await agent.AskAsync(request.Question, sessionId, correlationId, cancellationToken);

        if (string.IsNullOrWhiteSpace(reply.Answer))
        {
            return TypedResults.Problem(ProblemDetailsFactory.Create(
                StatusCodes.Status502BadGateway,
                "Operations Agent returned no answer",
                "The model returned an empty answer.",
                correlationId));
        }

        return TypedResults.Ok(new OperationsAgentResponse(
            options.Value.AgentName,
            reply.Answer,
            reply.SessionId,
            reply.ToolCalls,
            reply.ModelRoundTrips,
            correlationId));
    }
    catch (ArgumentException exception)
    {
        return TypedResults.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Invalid request",
            exception.Message,
            context.GetCorrelationId()));
    }
    catch (HttpRequestException exception)
    {
        return TypedResults.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status502BadGateway,
            "Operations Agent tool unavailable",
            $"The authoritative Energy Hub could not be reached: {exception.Message}",
            context.GetCorrelationId()));
    }
    catch (OperationsAgentTimedOutException exception)
    {
        OperationsAgentEndpointLog.ExecutionTimedOut(logger, context.GetCorrelationId(), exception);
        return TypedResults.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status504GatewayTimeout,
            "Operations Agent request timed out",
            exception.Message,
            context.GetCorrelationId()));
    }
    catch (CredentialUnavailableException exception)
    {
        OperationsAgentEndpointLog.AuthenticationFailed(logger, context.GetCorrelationId(), exception);
        return TypedResults.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status503ServiceUnavailable,
            "Operations Agent authentication unavailable",
            "No supported Azure credential is available to invoke Microsoft Foundry.",
            context.GetCorrelationId()));
    }
    catch (AuthenticationFailedException exception)
    {
        OperationsAgentEndpointLog.AuthenticationFailed(logger, context.GetCorrelationId(), exception);
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
            $"Microsoft Foundry rejected the Operations Agent request: {exception.Message}",
            context.GetCorrelationId()));
    }
}
