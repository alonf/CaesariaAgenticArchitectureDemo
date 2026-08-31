using Azure;
using Azure.AI.Projects;
using Azure.Core;
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
builder.Services.AddHttpClient<ICommandCenterStageReader, CommandCenterStageReader>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OperationsAgentApiOptions>>().Value;
    client.BaseAddress = new Uri(options.CommandCenterBaseUri, UriKind.Absolute);
});

// AIProjectClient and DefaultAzureCredential construction is lazy: neither performs network or authentication
// calls until a token is requested, so the service still starts cleanly in Deterministic mode even when no
// Azure credential is available. The credential is a shared singleton because its chain-selection cache is
// per instance; the background warmup below runs the expensive discovery off the request path.
builder.Services.AddSingleton<TokenCredential>(new DefaultAzureCredential());
builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OperationsAgentApiOptions>>().Value;
    return new AIProjectClient(
        new Uri(options.FoundryProjectEndpoint, UriKind.Absolute),
        serviceProvider.GetRequiredService<TokenCredential>());
});
builder.Services.AddSingleton<FoundryCredentialWarmup>();
builder.Services.AddHostedService<DemoStageSynchronizer>();
builder.Services.AddSingleton(_ =>
{
    var initialStage = Enum.TryParse<DemoStage>(builder.Configuration["DemoStage"], out var configuredStage)
        ? configuredStage
        : DemoStage.Deterministic;
    return new DemoStageGate(initialStage);
});
builder.Services.AddSingleton<AgentSessionStore>();
builder.Services.AddSingleton<SimulatedWorkKnowledgeSearch>();
builder.Services.AddSingleton<IWorkKnowledgeSearch>(serviceProvider => serviceProvider.GetRequiredService<SimulatedWorkKnowledgeSearch>());
builder.Services.AddSingleton<ICaseMemoryStore, InMemoryCaseMemoryStore>();
builder.Services.AddSingleton<IOperationsAgent>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OperationsAgentApiOptions>>().Value;
    return new FoundryOperationsAgent(
        serviceProvider.GetRequiredService<AIProjectClient>(),
        serviceProvider.GetRequiredService<IEnergyReadGateway>(),
        serviceProvider.GetRequiredService<AgentSessionStore>(),
        serviceProvider.GetRequiredService<IWorkKnowledgeSearch>(),
        serviceProvider.GetRequiredService<ICaseMemoryStore>(),
        serviceProvider.GetRequiredService<DemoStageGate>(),
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
app.MapDemoBreakpoints(DemoSnippets.AgentCreation, DemoSnippets.FunctionTool, DemoSnippets.Session, DemoSnippets.Knowledge, DemoSnippets.CaseMemory);

var operationsAgent = app.MapGroup("/api/operations-agent")
    .WithTags("Operations Agent");

operationsAgent.MapPost("/ask", AskAsync);

// Stage propagation from the presenter switchboard; the gate blocks agent invocation whenever the
// authoritative stage it holds (pushed or reconciled) is Deterministic.
var demoStage = app.MapGroup("/api/operations-agent/demo-stage")
    .WithTags("Demo Stage");

demoStage.MapGet("/", (DemoStageGate stageGate) => TypedResults.Ok(stageGate.GetCurrent()));

// Presenter control over the simulated work-knowledge fixture: withholding the evidence shows the
// agent reporting missing evidence instead of inventing a work order.
var workKnowledge = app.MapGroup("/api/operations-agent/work-knowledge")
    .WithTags("Work Knowledge");

workKnowledge.MapGet("/", (SimulatedWorkKnowledgeSearch search) =>
    TypedResults.Ok(new WorkKnowledgeStatus(search.EvidencePresent)));
workKnowledge.MapPost("/", (WorkKnowledgeStatus status, SimulatedWorkKnowledgeSearch search) =>
{
    search.EvidencePresent = status.EvidencePresent;
    return TypedResults.Ok(new WorkKnowledgeStatus(search.EvidencePresent));
});

// The agent's case memory: closed investigations recorded by the operator, recalled later as
// hypotheses. Closing a case requires the Memory stage; clearing is a presenter reset.
var cases = app.MapGroup("/api/operations-agent/cases")
    .WithTags("Case Memory");

cases.MapGet("/", (ICaseMemoryStore store) => TypedResults.Ok(CreateCaseMemoryStatus(store)));
cases.MapPost("/", (HttpContext context, OperationsAgentCloseCaseRequest request, ICaseMemoryStore store, DemoStageGate stageGate) =>
{
    if (stageGate.GetCurrent().Id < DemoStage.Memory)
    {
        return Results.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status409Conflict,
            "Case memory disabled in the current demo stage",
            $"Closing a case requires the Memory stage; the current stage is {stageGate.GetCurrent().Name}.",
            context.GetCorrelationId()));
    }

    try
    {
        var closedCase = store.Record(request.AssetId, request.Symptom, request.Resolution);
        return Results.Ok(new OperationsAgentRecalledCase(
            closedCase.CaseId, closedCase.AssetId, closedCase.Symptom, closedCase.Resolution, closedCase.ClosedAt));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Invalid request",
            exception.Message,
            context.GetCorrelationId()));
    }
});
cases.MapPost("/clear", (ICaseMemoryStore store) =>
{
    store.Clear();
    return TypedResults.Ok(CreateCaseMemoryStatus(store));
});
demoStage.MapPost("/", (DemoStageStatus stage, DemoStageGate stageGate, FoundryCredentialWarmup credentialWarmup) =>
{
    var applied = stageGate.SetCurrent(stage);

    // Entering an agent-enabled stage triggers the one-time credential warmup, so the Deterministic
    // stage keeps its promise that no AI credential is used.
    if (stageGate.IsAgentEnabled)
    {
        credentialWarmup.EnsureStarted();
    }

    return TypedResults.Ok(applied);
});

await app.RunAsync();

static OperationsAgentCaseMemoryStatus CreateCaseMemoryStatus(ICaseMemoryStore store) =>
    new([.. store.GetAll().Select(closedCase => new OperationsAgentRecalledCase(
        closedCase.CaseId, closedCase.AssetId, closedCase.Symptom, closedCase.Resolution, closedCase.ClosedAt))]);

static async Task<IResult> AskAsync(
    HttpContext context,
    OperationsAgentRequest request,
    IOperationsAgent agent,
    DemoStageGate stageGate,
    FoundryCredentialWarmup credentialWarmup,
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

    // Safety net in case no stage propagation started the warmup; a no-op after the first call.
    credentialWarmup.EnsureStarted();
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
            reply.Evidence,
            reply.RecalledCases,
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
    catch (OperationsAgentSessionExpiredException exception)
    {
        return TypedResults.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status410Gone,
            "Conversational session expired",
            $"{exception.Message} Ask the question again to start a new session.",
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
