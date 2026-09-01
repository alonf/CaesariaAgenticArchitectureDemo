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
builder.Services.AddHttpClient<IEnergyCommandGateway, HttpEnergyCommandGateway>((serviceProvider, client) =>
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
builder.Services.AddSingleton<ToolSourceSwitch>();
builder.Services.AddSingleton<PendingApprovalStore>();
builder.Services.AddSingleton<StageTransitionEffects>();
builder.Services.AddSingleton<IWorkItemGateway, SimulatedWorkItemGateway>();
builder.Services.AddSingleton<SecurityConsultSwitch>();
// The HTTP client the Security Agent's MCP transport rides on. Note what is absent: this service
// has no client for the Security Hub itself.
builder.Services.AddHttpClient("securityagent-mcp", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OperationsAgentApiOptions>>().Value;
    client.BaseAddress = new Uri(options.SecurityAgentBaseUri, UriKind.Absolute);
});
builder.Services.AddSingleton<RemediationWorkflowService>();
// The HTTP client the MCP transport rides on; service discovery and the standard resilience
// pipeline apply like any other outbound client.
builder.Services.AddHttpClient("energyhub-mcp", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OperationsAgentApiOptions>>().Value;
    client.BaseAddress = new Uri(options.EnergyHubBaseUri, UriKind.Absolute);
});
builder.Services.AddSingleton<IOperationsAgent>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<OperationsAgentApiOptions>>().Value;
    var skillsDirectory = SkillCatalog.ResolveDirectory(options.SkillsDirectory);

    if (skillsDirectory is null)
    {
        OperationsAgentEndpointLog.SkillsDirectoryMissing(
            serviceProvider.GetRequiredService<ILogger<FoundryOperationsAgent>>(),
            options.SkillsDirectory);
    }

    return new FoundryOperationsAgent(
        serviceProvider.GetRequiredService<AIProjectClient>(),
        serviceProvider.GetRequiredService<IEnergyReadGateway>(),
        serviceProvider.GetRequiredService<AgentSessionStore>(),
        serviceProvider.GetRequiredService<IWorkKnowledgeSearch>(),
        serviceProvider.GetRequiredService<ICaseMemoryStore>(),
        serviceProvider.GetRequiredService<DemoStageGate>(),
        serviceProvider.GetRequiredService<ToolSourceSwitch>(),
        serviceProvider.GetRequiredService<PendingApprovalStore>(),
        serviceProvider.GetRequiredService<RemediationWorkflowService>(),
        serviceProvider.GetRequiredService<IWorkItemGateway>(),
        serviceProvider.GetRequiredService<SecurityConsultSwitch>(),
        serviceProvider.GetRequiredService<IHttpClientFactory>(),
        McpEndpoint.Create(options.EnergyHubBaseUri),
        McpEndpoint.Create(options.SecurityAgentBaseUri),
        skillsDirectory,
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
app.MapDemoBreakpoints(DemoSnippets.AgentCreation, DemoSnippets.FunctionTool, DemoSnippets.Session, DemoSnippets.Knowledge, DemoSnippets.CaseMemory, DemoSnippets.Skills, DemoSnippets.McpClient, DemoSnippets.Workflow, DemoSnippets.ToolApproval);

// Instantiate the workflow service at startup: the definition (diagram + YAML) renders once
// here, so a wiring or graph error fails the service start instead of the first panel load.
_ = app.Services.GetRequiredService<RemediationWorkflowService>();

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

    if (CloseCaseValidation.Validate(request) is { } validationError)
    {
        return Results.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Invalid request",
            validationError,
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

// Interactive-input bridge: questions a paused MCP tool asked the operator (MRTR). The Command
// Center lists them and posts the decision, which releases the paused tool call.
var approvals = app.MapGroup("/api/operations-agent/approvals")
    .WithTags("Interactive Input");

approvals.MapGet("/", (PendingApprovalStore store) => TypedResults.Ok(store.GetAll()));
approvals.MapPost("/{id}", (HttpContext context, string id, OperationsAgentApprovalDecision decision, PendingApprovalStore store, DemoStageGate stageGate) =>
{
    if (stageGate.GetCurrent().Id < DemoStage.InteractiveInput)
    {
        return Results.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status409Conflict,
            "Interactive input disabled in the current demo stage",
            $"Approvals require the Interactive Input stage; the current stage is {stageGate.GetCurrent().Name}.",
            context.GetCorrelationId()));
    }

    return store.TryRespond(id, decision.Approved)
        ? Results.Ok()
        : Results.NotFound(ProblemDetailsFactory.Create(
            StatusCodes.Status404NotFound,
            "Pending approval not found",
            $"No interactive-input request with id {id} is awaiting a decision.",
            context.GetCorrelationId()));
});

// The explicit remediation workflow: a code-built orchestration graph run on demand, with
// live steps and a self-rendered definition. Available only at the Workflow stage.
var remediation = app.MapGroup("/api/operations-agent/remediation")
    .WithTags("Remediation Workflow");

remediation.MapPost("/", (HttpContext context, OperationsAgentRemediationRequest request, RemediationWorkflowService workflowService, DemoStageGate stageGate) =>
{
    if (CreateWorkflowStageProblem(context, stageGate) is { } stageProblem)
    {
        return stageProblem;
    }

    if (string.IsNullOrWhiteSpace(request.AssetId) || request.AssetId.Length > 64)
    {
        return Results.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Invalid request",
            "A remediation run requires an asset identifier of at most 64 characters.",
            context.GetCorrelationId()));
    }

    return workflowService.TryStartRun(request.AssetId.Trim(), context.GetCorrelationId(), out var report)
        ? Results.Ok(report)
        : Results.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status409Conflict,
            "Remediation already in progress",
            $"A remediation workflow run is already in flight for {request.AssetId.Trim()}; wait for it to finish before starting another.",
            context.GetCorrelationId()));
});
// A caller that asked the agent to remediate holds its own correlation, not a run id; this lets
// it find, watch, and approve the run the agent started on its behalf.
remediation.MapGet("/runs", (HttpContext context, string correlationId, RemediationWorkflowService workflowService, DemoStageGate stageGate) =>
{
    if (CreateWorkflowStageProblem(context, stageGate) is { } stageProblem)
    {
        return stageProblem;
    }

    return string.IsNullOrWhiteSpace(correlationId)
        ? Results.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Invalid request",
            "A correlation identifier is required to look up a workflow run.",
            context.GetCorrelationId()))
        : Results.Ok(workflowService.FindRunByCorrelation(correlationId));
});
remediation.MapGet("/work-items", (HttpContext context, IWorkItemGateway workItems, DemoStageGate stageGate) =>
    CreateWorkflowStageProblem(context, stageGate) is { } stageProblem
        ? stageProblem
        : Results.Ok(workItems.GetAll()));
remediation.MapGet("/definition", (HttpContext context, RemediationWorkflowService workflowService, DemoStageGate stageGate) =>
    CreateWorkflowStageProblem(context, stageGate) is { } stageProblem
        ? stageProblem
        : Results.Ok(workflowService.GetDefinition()));
remediation.MapGet("/runs/{runId}", (HttpContext context, string runId, RemediationWorkflowService workflowService, DemoStageGate stageGate) =>
{
    if (CreateWorkflowStageProblem(context, stageGate) is { } stageProblem)
    {
        return stageProblem;
    }

    return workflowService.GetRun(runId) is { } report
        ? Results.Ok(report)
        : Results.NotFound(ProblemDetailsFactory.Create(
            StatusCodes.Status404NotFound,
            "Workflow run not found",
            $"No remediation workflow run with id {runId} exists.",
            context.GetCorrelationId()));
});

// Presenter toggle: whether the agent may consult the Security Operations Agent. The same
// question answered with it off and on is what shows the second agent earning its cost.
var securityConsult = app.MapGroup("/api/operations-agent/security-consult")
    .WithTags("Security Consult");

securityConsult.MapGet("/", (SecurityConsultSwitch consult) =>
    TypedResults.Ok(new OperationsAgentSecurityConsultStatus(consult.Enabled)));
securityConsult.MapPost("/", (HttpContext context, OperationsAgentSecurityConsultStatus status, SecurityConsultSwitch consult, DemoStageGate stageGate) =>
{
    if (stageGate.GetCurrent().Id < DemoStage.MultiAgent)
    {
        return Results.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status409Conflict,
            "Security consult disabled in the current demo stage",
            $"Consulting the Security Operations Agent requires the MultiAgent stage; the current stage is {stageGate.GetCurrent().Name}.",
            context.GetCorrelationId()));
    }

    consult.Enabled = status.Enabled;
    return Results.Ok(new OperationsAgentSecurityConsultStatus(consult.Enabled));
});

// Presenter toggle: where the streetlight tool comes from. Available only once the McpTools
// stage introduces the mechanism; the flip itself is the lecture beat.
var toolSource = app.MapGroup("/api/operations-agent/tool-source")
    .WithTags("Tool Source");

toolSource.MapGet("/", (ToolSourceSwitch toolSourceSwitch) =>
    TypedResults.Ok(new OperationsAgentToolSourceStatus(toolSourceSwitch.Current)));
toolSource.MapPost("/", (HttpContext context, OperationsAgentToolSourceStatus status, ToolSourceSwitch toolSourceSwitch, DemoStageGate stageGate) =>
{
    if (stageGate.GetCurrent().Id < DemoStage.McpTools)
    {
        return Results.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status409Conflict,
            "Tool source toggle disabled in the current demo stage",
            $"Selecting the tool source requires the McpTools stage; the current stage is {stageGate.GetCurrent().Name}.",
            context.GetCorrelationId()));
    }

    toolSourceSwitch.Current = status.Source;
    return Results.Ok(new OperationsAgentToolSourceStatus(toolSourceSwitch.Current));
});
demoStage.MapPost("/", (DemoStageStatus stage, DemoStageGate stageGate, FoundryCredentialWarmup credentialWarmup, StageTransitionEffects transitionEffects) =>
{
    var previous = stageGate.GetCurrent();
    var applied = stageGate.SetCurrent(stage);
    transitionEffects.Apply(previous.Id, applied.Id);

    // Entering an agent-enabled stage triggers the one-time credential warmup, so the Deterministic
    // stage keeps its promise that no AI credential is used.
    if (stageGate.IsAgentEnabled)
    {
        credentialWarmup.EnsureStarted();
    }

    return TypedResults.Ok(applied);
});

await app.RunAsync();

static IResult? CreateWorkflowStageProblem(HttpContext context, DemoStageGate stageGate) =>
    stageGate.GetCurrent().Id < DemoStage.Workflow
        ? Results.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status409Conflict,
            "Remediation workflow disabled in the current demo stage",
            $"The remediation workflow requires the Workflow stage; the current stage is {stageGate.GetCurrent().Name}.",
            context.GetCorrelationId()))
        : null;

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
            $"The Operations Agent requires an agent-enabled stage; the current stage is {stageGate.GetCurrent().Name}.",
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
            reply.Skills,
            reply.ToolSource,
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
    catch (OperationsAgentToolUnavailableException exception)
    {
        return TypedResults.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status502BadGateway,
            "Operations Agent tool unavailable",
            exception.Message,
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
