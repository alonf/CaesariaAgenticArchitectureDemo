using A2A;
using Azure.AI.Projects;
using Azure.Core;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;
using WorkforceAgent.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddOptions<WorkforceAgentApiOptions>()
    .BindConfiguration(WorkforceAgentApiOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<IWorkforceHubGateway, HttpWorkforceHubGateway>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<WorkforceAgentApiOptions>>().Value;
    client.BaseAddress = new Uri(options.WorkforceHubBaseUri, UriKind.Absolute);
});

builder.Services.AddSingleton<TokenCredential>(new DefaultAzureCredential());
// Woken by the Operations Agent when the demo reaches the stage that consults this domain, so the
// first delegated task does not also pay for credential discovery.
builder.Services.AddSingleton<FoundryCredentialWarmup>();
builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<WorkforceAgentApiOptions>>().Value;
    return new AIProjectClient(
        new Uri(options.FoundryProjectEndpoint, UriKind.Absolute),
        serviceProvider.GetRequiredService<TokenCredential>());
});

builder.Services.AddSingleton(serviceProvider =>
{
    var accessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
    return new WorkOrderTools(
        serviceProvider.GetRequiredService<IWorkforceHubGateway>(),
        () => accessor.HttpContext?.GetCorrelationId() ?? CorrelationIds.Create(),
        serviceProvider.GetRequiredService<ILogger<WorkOrderTools>>());
});

// The A2A server resolves the agent by name, so the agent is registered under that key.
var publishedAgentName = builder.Configuration[$"{WorkforceAgentApiOptions.SectionName}:AgentName"]
    ?? "Caesarea Workforce Agent";

builder.Services.AddKeyedSingleton<AIAgent>(publishedAgentName, (serviceProvider, _) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<WorkforceAgentApiOptions>>().Value;
    return WorkforceAgentFactory.Create(
        serviceProvider.GetRequiredService<AIProjectClient>(),
        serviceProvider.GetRequiredService<WorkOrderTools>(),
        options.ModelDeploymentName,
        options.AgentName,
        serviceProvider.GetRequiredService<ILoggerFactory>());
});

// Published for other domains to consult: an agent with a card, not a tool in someone's toolbox.
builder.Services.AddA2AServer(publishedAgentName);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.MapDefaultEndpoints();

app.MapA2AHttpJson(publishedAgentName, "/a2a");

// The card at the A2A well-known location, so any standard resolver finds it. The framework's own
// /a2a/card is its protocol-internal default and carries no domain detail; this is the published
// identity another domain actually discovers.
app.MapGet(WorkforceAgentCard.WellKnownPath, (HttpContext context, IOptions<WorkforceAgentApiOptions> options) =>
{
    var request = context.Request;
    var baseAddress = new Uri($"{request.Scheme}://{request.Host}{request.PathBase}/", UriKind.Absolute);
    return Results.Json(
        WorkforceAgentCard.Create(options.Value.AgentName, baseAddress),
        A2AJsonUtilities.DefaultOptions);
}).WithTags("Workforce Agent");

app.MapDemoBreakpoints(DemoSnippets.A2ASpecialist);

// Woken by the Operations Agent when the demo reaches the stage that consults this domain.
app.MapPost("/api/agent-warmup", (FoundryCredentialWarmup credentialWarmup) =>
{
    credentialWarmup.EnsureStarted();
    return TypedResults.Accepted((string?)null);
}).WithTags("Workforce Agent");

await app.RunAsync();
