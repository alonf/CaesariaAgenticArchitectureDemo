using Azure.AI.Projects;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;
using SecurityAgent.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddOptions<SecurityAgentApiOptions>()
    .BindConfiguration(SecurityAgentApiOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// The only client to the Security Hub in the entire system. No other service is given one:
// the restricted records are reachable through this agent's reasoning or not at all.
builder.Services.AddHttpClient<ISecurityHubGateway, HttpSecurityHubGateway>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<SecurityAgentApiOptions>>().Value;
    client.BaseAddress = new Uri(options.SecurityHubBaseUri, UriKind.Absolute);
});

builder.Services.AddSingleton<TokenCredential>(new DefaultAzureCredential());
builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<SecurityAgentApiOptions>>().Value;
    return new AIProjectClient(
        new Uri(options.FoundryProjectEndpoint, UriKind.Absolute),
        serviceProvider.GetRequiredService<TokenCredential>());
});
builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<SecurityAgentApiOptions>>().Value;
    return new SecurityAssessmentAgent(
        serviceProvider.GetRequiredService<AIProjectClient>(),
        serviceProvider.GetRequiredService<ISecurityHubGateway>(),
        options.ModelDeploymentName,
        options.AgentName,
        TimeSpan.FromSeconds(options.RequestTimeoutSeconds),
        serviceProvider.GetRequiredService<ILoggerFactory>(),
        serviceProvider.GetRequiredService<ILogger<SecurityAssessmentAgent>>());
});
builder.Services.AddHttpContextAccessor();

// The agent is published as a capability another domain can discover and consult.
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<SecurityMcpTools>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.MapDefaultEndpoints();
app.MapMcp("/mcp");
app.MapDemoBreakpoints(DemoSnippets.MultiAgent);

await app.RunAsync();
