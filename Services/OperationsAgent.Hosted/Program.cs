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
// The Hosting stage's claim is that the agent code does not determine where it must run. This
// project is where that is either true or a slogan: the instructions, the skills and the Energy
// Hub tool are the same types the Aspire-hosted agent uses, referenced rather than copied. What
// changes is who owns the runtime, the scaling, the identity and the endpoint.
// [demo-anchor: HOSTING]
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

// How the agent's procedures reach the model: "provider" (the default, and the real
// AgentSkillsProvider) or "tool", which advertises the same skills and serves their bodies through
// an ordinary function call.
//
// The tool path exists because of a wrong diagnosis, and is kept because it is independently useful:
// it needs no files in the image and no SKILLS_DIRECTORY. The defect it was once believed to dodge -
// HTTP 400 invalid_payload on tool-calling turns - is real but was never about skills: the hosted
// runtime replays reasoning items the service then rejects, and ReasoningReplaySanitizingChatClient
// is the actual fix. See docs/product-status/hosted-agent.md.
var skillsMode = builder.Configuration["SKILLS_MODE"] ?? "provider";

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

// Work IQ, reached through a Foundry toolbox.
//
// This is the one capability that only exists in this habitat, and it is deliberate rather than an
// oversight in the Aspire composition: the toolbox is where the platform performs an OAuth
// on-behalf-of exchange for the CALLING USER. The agent never holds a user token. Microsoft 365
// decides what comes back - permissions, sensitivity labels and all - so the same question asked by
// two people can honestly return two different answers.
//
// x-agent-user-id is what carries the caller into the container, and the toolbox proxy turns it into
// a delegated token on the request's own egress. That is why the SDK describes such a toolbox as
// deferred at startup and resolved per request: at container start there is no user to be.
//
// Absent WORKIQ_TOOLBOX, none of this is registered and the agent behaves exactly as it does under
// Aspire, which keeps one composition honest across both habitats.
var workIqToolbox = builder.Configuration["WORKIQ_TOOLBOX"];

if (!string.IsNullOrWhiteSpace(workIqToolbox))
{
    builder.Services.AddFoundryToolboxes(
        new DefaultAzureCredential(),
        workIqToolbox);
}

// DUMP_MODEL_TRAFFIC: when set to a directory path, every outbound project request body is written
// there. This is how the invalid_payload defect was finally read - the service names no parameter,
// so the only evidence is the request itself. Local diagnosis only, and the hosted habitat enforces
// that rather than trusting configuration to: the dumps contain full prompts and tool outputs -
// including what Work IQ returned about a person's own documents - and the platform's persistent
// filesystem is no place to leave them. FOUNDRY_HOSTING_ENVIRONMENT is the platform's own signal
// that this is that habitat (it is what FoundryEnvironment.IsHosted keys off), so it cannot be
// spoofed away by the same configuration that set the flag. See ModelTrafficDumpPolicy.
var dumpModelTraffic = builder.Configuration["DUMP_MODEL_TRAFFIC"];

if (!string.IsNullOrWhiteSpace(dumpModelTraffic))
{
    // Development-only, twice over: the Foundry-hosted habitat is refused by the platform's own
    // signal, and every OTHER habitat is refused unless the environment is Development - a
    // self-hosted production deployment with the variable set must not persist prompts and
    // Microsoft 365 content either. The dump exists for a laptop with a debugger, nowhere else.
    var hostedByFoundry = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FOUNDRY_HOSTING_ENVIRONMENT"));
    var environmentName = builder.Configuration["DOTNET_ENVIRONMENT"]
        ?? builder.Configuration["ASPNETCORE_ENVIRONMENT"]
        ?? "Production";
    var isDevelopment = string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);

    if (hostedByFoundry || !isDevelopment)
    {
        // Stderr, not a logger: this runs during construction, when everything goes to the
        // bootstrap logger that is gone before Application Insights exists - the same hole
        // HostingDiagnostics documents. The container's own log stream is the one place this line
        // reliably survives.
        await Console.Error.WriteLineAsync(
            "DUMP_MODEL_TRAFFIC is set but this process is "
            + (hostedByFoundry ? "running in the Foundry-hosted environment" : $"not a Development environment ('{environmentName}')")
            + "; refusing to dump model traffic. Diagnose on a Development machine instead.");
        dumpModelTraffic = null;
    }
}

builder.Services.AddSingleton(serviceProvider =>
{
    var clientOptions = new AIProjectClientOptions();

    if (!string.IsNullOrWhiteSpace(dumpModelTraffic))
    {
        clientOptions.AddPolicy(
            new ModelTrafficDumpPolicy(dumpModelTraffic),
            System.ClientModel.Primitives.PipelinePosition.PerCall);
    }

    return new AIProjectClient(
        new Uri(projectEndpoint, UriKind.Absolute),
        serviceProvider.GetRequiredService<TokenCredential>(),
        clientOptions);
});

#region HOSTING
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

    // The shared core plus the Work IQ boundary when the toolbox is registered. The composition
    // is a pure function on OperationsAgentInstructions so the deterministic tests pin it - see
    // that type for why the boundary exists at all.
    var instructions = OperationsAgentInstructions.ComposeForHostedHabitat(
        workIqToolboxRegistered: !string.IsNullOrWhiteSpace(workIqToolbox));

    var options = new ChatClientAgentOptions
    {
        Name = "Caesarea Operations Agent (hosted)",
        Description = "The Caesarea Operations Agent, running on the Foundry hosted agent runtime.",
        ChatOptions = new()
        {
            ModelId = modelDeploymentName,
            Instructions = instructions,
            Tools =
            [
                AIFunctionFactory.Create(
                    energyTools.GetStreetlightStateAsync,
                    EnergyTools.StreetlightStateToolName,
                    "Gets the current authoritative operational state of a streetlight.")
            ]
        }
    };

    // No simulated work-knowledge search in this habitat, deliberately.
    //
    // The Aspire-hosted agent carries SimulatedWorkKnowledgeSearch because it has no real source to
    // read; here there is one, and Work IQ reaches the actual document in the caller's own Microsoft
    // 365. Registering both would leave the model two plausible tools for the same question, and it
    // reliably picked the in-process one - which answers instantly, looks entirely correct, and is
    // labelled "(Simulated work knowledge)" only if you read the tool output rather than the reply.
    // That is the worst failure mode available to this demo: a confident, well-formed, fabricated
    // provenance. One source, and it is the real one.

    // Two ways to reach the same behaviour. "tool" works in the hosted runtime; "provider" is the
    // real AgentSkillsProvider, kept so the defect can be re-tested on a future preview.
    if (skillsMode.Equals("provider", StringComparison.OrdinalIgnoreCase))
    {
        AgentSkillsProvider? skills = resolvedSkills is null
            ? null
            : new AgentSkillsProvider(
                resolvedSkills,
                options: new AgentSkillsProviderOptions { DisableLoadSkillApproval = true },
                loggerFactory: loggerFactory);

        options.AIContextProviders = skills is null ? [] : [skills];
    }
    else
    {
        var skillTools = resolvedSkills is null ? null : SkillsAsTools.Load(resolvedSkills);
        if (skillTools is not null)
        {
            // Advertise, then load on demand - the same two halves the provider implements, carried
            // by an ordinary tool the hosted runtime does not choke on.
            options.ChatOptions.Instructions += skillTools.Catalogue;
            options.ChatOptions.Tools.Add(AIFunctionFactory.Create(skillTools.LoadSkill, SkillsAsTools.ToolName));

            var advertised = string.Join(", ", skillTools.Names);
            HostedAgentLog.SkillsExposedAsTools(logger, advertised);
        }
    }

    // The clientFactory wraps the model client BENEATH the function-invocation loop, which is the
    // only place the fix works: without it, every turn in which the model calls ANY tool fails with
    // HTTP 400 invalid_payload on the follow-up request, because the loop replays the reasoning
    // item's encrypted_content blob and the service rejects it - a deterministic defect that spent a
    // while dressed up as an intermittent one. See ReasoningReplaySanitizingChatClient.
    return serviceProvider.GetRequiredService<AIProjectClient>().AsAIAgent(
        options,
        clientFactory: innerClient => new ReasoningReplaySanitizingChatClient(innerClient),
        loggerFactory: loggerFactory);
}

builder.Services.AddSingleton(CreateAgent);
#endregion

#region PROTOCOL

// The Responses protocol: the platform manages conversation history, streaming and the session
// lifecycle, and the library maps /readiness for the health probe without being asked. The agent is
// resolved from the container the host builds, not from a second one built here.
// [demo-anchor: PROTOCOL]
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
#endregion

var app = builder.Build();

// PROBE: does an inbound request carry a user identity?
//
// The whole delegated-data story depends on it. Foundry Toolboxes describe "a tool source that
// requires a per-user delegated identity, which is only available on a user request's egress" and
// resolve it with "the platform-injected per-user isolation key" - so if a request reaches this
// container with no user on it, an agent cannot read a person's OneDrive as that person, and the
// Work IQ demo has to be an application-permission one instead. Those are different demos and
// different governance stories, so this is worth one deployment to settle.
//
// Header NAMES are logged in full; the user id is truncated, because it identifies a person and a
// diagnostic has no business writing one into telemetry at full length.
app.App.Use(async (context, next) =>
{
    var interesting = context.Request.Headers
        .Where(header =>
            header.Key.StartsWith("x-ms-", StringComparison.OrdinalIgnoreCase) ||
            header.Key.StartsWith("x-agent-", StringComparison.OrdinalIgnoreCase) ||
            header.Key.StartsWith("x-client-", StringComparison.OrdinalIgnoreCase))
        .Select(header => header.Key)
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    var userId = context.Request.Headers["x-ms-user-id"].FirstOrDefault()
        ?? context.Request.Headers["x-agent-user-id"].FirstOrDefault();

    var identity = userId is { Length: > 0 }
        ? $"present ({userId[..Math.Min(8, userId.Length)]}…, {userId.Length} chars)"
        : "absent";

    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("OperationsAgent.Hosted");
    var path = context.Request.Path.Value ?? "(none)";
    var headerNames = interesting.Length == 0 ? "(none)" : string.Join(", ", interesting);

    HostedAgentLog.InboundIdentity(logger, path, identity, headerNames);

    await next();
});

await app.RunAsync();
