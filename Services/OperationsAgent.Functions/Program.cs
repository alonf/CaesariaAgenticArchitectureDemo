using Azure.AI.Projects;
using Azure.Identity;
using Caesarea.ServiceDefaults;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using OperationsAgent.Api.Services;
using OperationsAgent.Functions;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();
// The durable extension's Information logs can include whole request/response messages.
// Keep our correlated operation logs without persisting model content in ordinary logs.
builder.Logging.AddFilter("Microsoft.Agents.AI", LogLevel.Warning);

// This first composition is a local, supervised recovery rehearsal. Cloud publication remains
// gated on Entra authorization, separate executor identity and retention configuration.
if (builder.Configuration["CAESAREA_LOCAL_REHEARSAL"] != "true"
    || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")))
{
    throw new InvalidOperationException("This host requires CAESAREA_LOCAL_REHEARSAL=true and cannot run as an Azure deployment yet.");
}

string Required(string name) => builder.Configuration[name] is { Length: > 0 } value
    ? value : throw new InvalidOperationException($"{name} must be configured.");
Uri LocalHub(string name)
{
    var uri = new Uri(Required(name));
    return uri.IsLoopback && uri.Scheme is "http" or "https"
        ? uri : throw new InvalidOperationException($"{name} must address the local deterministic demo.");
}

var operatorKey = Required("CAESAREA_OPERATOR_KEY");
var supervisorKey = Required("CAESAREA_SUPERVISOR_KEY");
if (operatorKey.Length < 32 || supervisorKey.Length < 32 || operatorKey == supervisorKey)
{
    throw new InvalidOperationException("Use two distinct random credentials, each at least 32 characters long.");
}

builder.Services.AddSingleton(new RecoveryAuthorization(operatorKey, supervisorKey, Required("CAESAREA_SUPERVISOR_NAME")));
builder.Services.AddSingleton(TimeProvider.System);
var energyUri = LocalHub("ENERGYHUB_BASE_URI");
var commandCenterUri = LocalHub("COMMANDCENTER_BASE_URI");
builder.Services.AddHttpClient<IEnergyReadGateway, HttpEnergyReadGateway>(client =>
{
    client.BaseAddress = energyUri;
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpClient<IEnergyCommandGateway, HttpEnergyCommandGateway>(client =>
{
    client.BaseAddress = energyUri;
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<ICommandCenterStageReader, CommandCenterStageReader>(client =>
{
    client.BaseAddress = commandCenterUri;
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddTransient(sp => new RecoveryActivities(sp.GetRequiredService<IEnergyReadGateway>(),
    sp.GetRequiredService<IEnergyCommandGateway>(), sp.GetRequiredService<ICommandCenterStageReader>(),
    sp.GetRequiredService<RecoveryAuthorization>(), sp.GetRequiredService<TimeProvider>()));
builder.Services.AddTransient(sp => new RecoveryHttpFunctions(sp.GetRequiredService<RecoveryAuthorization>()));
builder.Services.AddTransient<RecoveryTools>();

builder.ConfigureDurableAgents(options =>
{
    options.DefaultTimeToLive = TimeSpan.FromHours(24);
    options.ResultRetentionPeriod = TimeSpan.FromHours(1);
    options.AddAIAgentFactory(RecoveryHttpFunctions.AgentName, sp =>
    {
        var tools = sp.GetRequiredService<RecoveryTools>();
        var energy = new EnergyTools(sp.GetRequiredService<IEnergyReadGateway>(),
            () => InvocationCorrelation.Resolve(null), sp.GetRequiredService<ILogger<EnergyTools>>());
        var agentOptions = new ChatClientAgentOptions
        {
            Name = RecoveryHttpFunctions.AgentName,
            ChatOptions = new ChatOptions
            {
                ModelId = builder.Configuration["MODEL_DEPLOYMENT_NAME"] ?? "gpt-5.5",
                Instructions = "You investigate Caesarea's L-417 streetlight. Read authoritative state before explaining. "
                    + "For repair requests, start a supervised recovery and remember its exact operation ID. "
                    + "For follow-up questions, query the remembered operation's actual status. "
                    + "Only explicitly retry failed pre-dispatch operations when the user requests it; every new attempt requires fresh approval. "
                    + "OutcomeUnknown or a null CommandExecuted is uncertainty, never proof that no command executed. "
                    + "You cannot approve, issue device commands, or claim completion from acceptance. "
                    + "A separate supervisor must review the operation outside this conversation.",
                Tools = [AIFunctionFactory.Create(energy.GetStreetlightStateAsync, EnergyTools.StreetlightStateToolName),
                    AIFunctionFactory.Create(tools.StartRecoveryAsync, "start_recovery"),
                    AIFunctionFactory.Create(tools.GetRecoveryStatusAsync, "get_recovery_status")]
            }
        };
        // Chat Completions keeps history in the durable entity. The Foundry Responses
        // adapter instead owns remote history and requires a separate durable ProviderKey.
        var projectClient = new AIProjectClient(new Uri(Required("FOUNDRY_PROJECT_ENDPOINT")), new AzureCliCredential());
        return projectClient.ProjectOpenAIClient.GetChatClient(agentOptions.ChatOptions.ModelId)
            .AsIChatClient().AsAIAgent(agentOptions, loggerFactory: sp.GetRequiredService<ILoggerFactory>());
    }, enableHttpTrigger: false, enableMcpToolTrigger: false);
});
using var app = builder.Build();
await app.RunAsync();
