using Azure.AI.Projects;
using Azure.Core;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;
using OperationsAgent.Api.Services;
using OperationsAgent.Hosted;

// The Caesarea Operations Agent, hosted by Microsoft Foundry instead of by us.
//
// Slide 43's claim is that the agent code does not determine where it must run. This project is
// where that is either true or a slogan: the instructions, the skills and the Energy Hub tool are
// the same types the Aspire-hosted agent uses, referenced rather than copied. What changes is who
// owns the runtime, the scaling, the identity and the endpoint.
//
// What is deliberately NOT here: approvals, the remediation workflow, the MCP tool source toggle,
// the security consult and the A2A delegation. Those belong to the presenter-driven demo stages and
// need the Command Center and the peer agents; a hosted agent that carried them would be a worse
// example of hosting and a confusing example of everything else.

// The port has to be settled before the host is built, because AgentHost binds during Build().
//
// AgentHost listens on PORT, defaulting to 8088 - and in the Foundry sandbox 8088 is already taken
// by the platform, so the default makes the container die at startup with "Failed to bind to address
// http://0.0.0.0:8088: address already in use". The session then fails with `session_not_ready` and
// a message about the /readiness endpoint, which points at the one thing that was not wrong.
//
// 8080 is the port the aspnet base image already declares and the one the platform routes to. Set it
// only when the platform has not: an explicit PORT from the environment always wins.
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PORT")))
{
    Environment.SetEnvironmentVariable("PORT", "8080");
}

var builder = AgentHost.CreateBuilder(args);

// No credential in the image and none in configuration. The platform mints a dedicated Microsoft
// Entra identity for this agent when its version is created, and DefaultAzureCredential picks it up
// from the sandbox. Locally it falls back to the developer's own sign-in.
builder.Services.AddSingleton<TokenCredential>(new DefaultAzureCredential());

// FOUNDRY_PROJECT_ENDPOINT is injected by the platform. Locally it comes from configuration, so the
// same container runs on a laptop against the same project.
var projectEndpoint = builder.Configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException(
        "FOUNDRY_PROJECT_ENDPOINT is not set. The platform injects it when hosted; set it in configuration to run locally.");

var modelDeploymentName = builder.Configuration["MODEL_DEPLOYMENT_NAME"] ?? "gpt-5.5";

// The Energy Hub is reachable over the network in both habitats: a container app in Azure when
// hosted, the Aspire-composed service when local. The agent does not know or care which.
var energyHubBaseUri = builder.Configuration["ENERGYHUB_BASE_URI"]
    ?? throw new InvalidOperationException("ENERGYHUB_BASE_URI is not set; the agent has no authoritative source to read.");

// Skills are files, so they arrive in the image. SkillCatalog walks up looking for the solution file
// when given a relative path, which no container has - so this is an absolute path, and the
// Dockerfile puts the directory there.
var skillsDirectory = builder.Configuration["SKILLS_DIRECTORY"] ?? "/app/skills";

// Computed here and logged from inside CreateAgent, where a real logger exists. Everything AgentHost
// writes about the platform during construction goes to a bootstrap logger that is gone before
// Application Insights is wired, so those lines survive only in a console nobody can reach.
//
// Names only, never values. Which variables the platform injects is precisely what you need to know
// when running a container you did not configure, and also the last place you want to discover that
// you have written a connection string into a trace.
var platformVariableNames = string.Join(
    ", ",
    Environment.GetEnvironmentVariables().Keys.Cast<string>().Order(StringComparer.Ordinal));

var listeningPort = Environment.GetEnvironmentVariable("PORT") ?? "(not set)";

builder.Services.AddHttpClient<IEnergyReadGateway, HttpEnergyReadGateway>(client =>
{
    client.BaseAddress = new Uri(energyHubBaseUri, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton(serviceProvider => new AIProjectClient(
    new Uri(projectEndpoint, UriKind.Absolute),
    serviceProvider.GetRequiredService<TokenCredential>()));

AIAgent CreateAgent(IServiceProvider serviceProvider)
{
    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("OperationsAgent.Hosted");

    HostedAgentLog.HostingEnvironment(logger, listeningPort, platformVariableNames);

    var resolvedSkills = SkillCatalog.ResolveDirectory(skillsDirectory);

    if (resolvedSkills is null)
    {
        // Loud, not silent. An agent that quietly lost its procedures still answers, just worse -
        // and the difference is invisible until someone reads a transcript on a projector.
        HostedAgentLog.SkillsDirectoryMissing(logger, skillsDirectory);
    }

    // One session-scoped correlation is not available here the way it is behind an ASP.NET request,
    // so the tool carries the agent's own identity for its logs. The platform correlates the run.
    var energyTools = new EnergyTools(
        serviceProvider.GetRequiredService<IEnergyReadGateway>(),
        "hosted",
        loggerFactory.CreateLogger<EnergyTools>());

    var options = new ChatClientAgentOptions
    {
        Name = "Caesarea Operations Agent (hosted)",
        Description = "The Caesarea Operations Agent, running on the Foundry hosted agent runtime.",
        ChatOptions = new()
        {
            ModelId = modelDeploymentName,
            Instructions = OperationsAgentInstructions.Text,
            Tools =
            [
                AIFunctionFactory.Create(
                    energyTools.GetStreetlightStateAsync,
                    EnergyTools.StreetlightStateToolName,
                    "Gets the current authoritative operational state of a streetlight.")
            ]
        }
    };

    if (resolvedSkills is not null)
    {
        // Progressive disclosure, exactly as the local agent does it: names and descriptions are
        // advertised, and the model pulls a full procedure through load_skill when it wants one.
        options.AIContextProviders =
        [
            new AgentSkillsProvider(
                resolvedSkills,
                options: new AgentSkillsProviderOptions { DisableLoadSkillApproval = true },
                loggerFactory: loggerFactory)
        ];
    }

    return serviceProvider.GetRequiredService<AIProjectClient>().AsAIAgent(options, loggerFactory: loggerFactory);
}

builder.Services.AddSingleton(CreateAgent);

// The Responses protocol: the platform manages conversation history, streaming and the session
// lifecycle, and the library maps /readiness for the health probe without being asked. The agent is
// resolved from the container the host builds, not from a second one built here.
builder.Services.AddFoundryResponses();

builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
await app.RunAsync();
