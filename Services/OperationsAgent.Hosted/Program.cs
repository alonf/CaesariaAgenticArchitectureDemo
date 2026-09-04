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

// Read for diagnostics only. PORT belongs to the platform: it is reserved, and the version API
// rejects any attempt to set it with "Environment variable 'PORT' is reserved for platform use".
// Overriding it in code only moved the failure to a different port number.
var injectedPort = Environment.GetEnvironmentVariable("PORT");

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

// Reports what the platform handed this container, once logging is real. Everything AgentHost says
// about its environment during construction goes to a bootstrap logger that is gone before
// Application Insights exists, which is why the first hosted failures were invisible.
builder.Services.AddSingleton<IHostedService>(serviceProvider => new HostingDiagnostics(
    serviceProvider.GetRequiredService<ILogger<HostingDiagnostics>>(),
    injectedPort));

// The Energy Hub's ingress rejects anonymous callers, so in this habitat every call carries the
// agent's own Entra token. ENERGYHUB_SCOPE is what selects that behaviour: set, and the handler is
// composed around the gateway; absent, and the gateway calls exactly as it does under Aspire, where
// the Energy Hub is a neighbour on a private network. The gateway itself is unchanged either way -
// see EnergyHubAuthorizationHandler for why that matters more than it looks.
var energyHubScope = builder.Configuration["ENERGYHUB_SCOPE"];

var energyHubClient = builder.Services.AddHttpClient<IEnergyReadGateway, HttpEnergyReadGateway>(client =>
{
    client.BaseAddress = new Uri(energyHubBaseUri, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(30);
});

if (!string.IsNullOrWhiteSpace(energyHubScope))
{
    builder.Services.AddTransient(serviceProvider => new EnergyHubAuthorizationHandler(
        serviceProvider.GetRequiredService<TokenCredential>(),
        energyHubScope,
        serviceProvider.GetRequiredService<ILogger<EnergyHubAuthorizationHandler>>()));

    energyHubClient.AddHttpMessageHandler<EnergyHubAuthorizationHandler>();
}

builder.Services.AddSingleton(serviceProvider => new AIProjectClient(
    new Uri(projectEndpoint, UriKind.Absolute),
    serviceProvider.GetRequiredService<TokenCredential>()));

AIAgent CreateAgent(IServiceProvider serviceProvider)
{
    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("OperationsAgent.Hosted");
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

// AddFoundryResponses registers the services; this maps the routes and the /readiness probe the
// platform requires. The Foundry.Hosting package ships no builder-level extension that would call
// RegisterProtocol for us, so it is called here.
//
// This composition was blameless the whole time. On Foundry.Hosting 1.19 the hosted runtime bound
// PORT twice and the container died with "Failed to bind to address http://[::]:8088: address
// already in use" - in an empty container, with nothing else running, because the process was
// competing with itself. 1.20 fixed it, and that upgrade was the entire fix.
//
// The trap while diagnosing it: outside the hosted runtime the bug does not exist, because the two
// listeners land on different ports and both come up. Set FOUNDRY_HOSTING_ENVIRONMENT to anything
// non-empty and the sandbox reproduces on a laptop in seconds - worth far more than another
// deployment. Allow a minute for startup there: TaskManager retries its storage calls before Kestrel
// binds, so a container that looks hung at fifteen seconds is often just waiting.
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
await app.RunAsync();
