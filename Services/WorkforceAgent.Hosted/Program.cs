using System.Diagnostics;
using Azure.AI.Projects;
using Azure.Core;
using Azure.Identity;
using Caesarea.ServiceDefaults;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using WorkforceAgent.Api.Services;
using WorkforceAgent.Hosted;

// The Caesarea Workforce Agent, hosted by Microsoft Foundry.
//
// This is the A2A half of the hosting story, and the thing to notice is what is absent: there is no
// A2A code here at all. No AddA2AServer, no MapA2AHttpJson, no published card. The container
// implements the Responses protocol and nothing else; Foundry supplies the public A2A endpoint, the
// agent card and the protocol adaptation, and forwards each task to the Responses implementation
// below. That is why incoming A2A requires Responses to be enabled alongside it.
//
// Set against the Aspire-hosted WorkforceAgent.Api, which serves A2A itself with MapA2AHttpJson,
// this is the contrast the Hosting stage exists to make: the same agent, the same tools, the same
// shareable projection - and a different owner of the protocol boundary.
//
// What is unchanged, and must stay unchanged: the tools return the shareable projection of a work
// order, so labour cost, contracted rates and technician identity are never selected and never reach
// this context. Hosting moved the runtime. It did not move the boundary.

var builder = AgentHost.CreateBuilder(args);

// No credential in the image. The platform mints a dedicated Entra identity for this agent when its
// version is created; locally DefaultAzureCredential falls back to the developer's sign-in.
builder.Services.AddSingleton<TokenCredential>(new DefaultAzureCredential());

var projectEndpoint = builder.Configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException(
        "FOUNDRY_PROJECT_ENDPOINT is not set. The platform injects it when hosted; set it in configuration to run locally.");

var modelDeploymentName = builder.Configuration["MODEL_DEPLOYMENT_NAME"] ?? "gpt-5.5";

var agentName = builder.Configuration["WORKFORCE_AGENT_NAME"] ?? "Caesarea Workforce Agent";

var workforceHubBaseUri = builder.Configuration["WORKFORCEHUB_BASE_URI"]
    ?? throw new InvalidOperationException("WORKFORCEHUB_BASE_URI is not set; the agent has no work orders to read.");

// The Workforce Hub's ingress rejects anonymous callers, so in this habitat every call carries the
// agent's own Entra token. WORKFORCEHUB_SCOPE selects that: set, and the handler is composed around
// the gateway; absent, and the gateway calls exactly as it does under Aspire, where the Hub is a
// neighbour on a private network. HttpWorkforceHubGateway is untouched either way.
var workforceHubScope = builder.Configuration["WORKFORCEHUB_SCOPE"];

var hubClient = builder.Services.AddHttpClient<IWorkforceHubGateway, HttpWorkforceHubGateway>(client =>
{
    client.BaseAddress = new Uri(workforceHubBaseUri, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(30);
});

if (!string.IsNullOrWhiteSpace(workforceHubScope))
{
    builder.Services.AddTransient(serviceProvider => new WorkforceHubAuthorizationHandler(
        serviceProvider.GetRequiredService<TokenCredential>(),
        workforceHubScope,
        serviceProvider.GetRequiredService<ILogger<WorkforceHubAuthorizationHandler>>()));

    hubClient.AddHttpMessageHandler<WorkforceHubAuthorizationHandler>();
}

builder.Services.AddSingleton(serviceProvider => new AIProjectClient(
    new Uri(projectEndpoint, UriKind.Absolute),
    serviceProvider.GetRequiredService<TokenCredential>()));

builder.Services.AddSingleton(serviceProvider =>
{
    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

    // There is no HttpContext here to borrow a correlation id from, the way the Aspire-hosted agent
    // does behind an ASP.NET request. The current activity's trace id is the right substitute rather
    // than a constant: it changes per delegated task, so the Hub's logs can tell two consultations
    // apart, and it is the same identifier the platform's own traces carry, so the two line up.
    // A literal here would make every call in the Hub's log look like one very busy caller.
    var tools = new WorkOrderTools(
        serviceProvider.GetRequiredService<IWorkforceHubGateway>(),
        () => Activity.Current?.TraceId.ToString() ?? CorrelationIds.Create(),
        loggerFactory.CreateLogger<WorkOrderTools>());

    // The same factory the Aspire-hosted agent calls, with the same instructions and the same two
    // tools. Referenced, not copied.
    return WorkforceAgentFactory.Create(
        serviceProvider.GetRequiredService<AIProjectClient>(),
        tools,
        modelDeploymentName,
        agentName,
        loggerFactory);
});

builder.Services.AddFoundryResponses();
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
await app.RunAsync();
