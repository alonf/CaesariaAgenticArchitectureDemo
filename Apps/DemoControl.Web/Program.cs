using DemoControl.Web.Components;
using DemoControl.Web.Configuration;
using DemoControl.Web.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOptions<DemoControlWebOptions>()
    .BindConfiguration(DemoControlWebOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHttpClient<DemoScenarioApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoControlWebOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUri, UriKind.Absolute);
});
builder.Services.AddHttpClient<DemoStageApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoControlWebOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUri, UriKind.Absolute);
});
// Every call to the Operations Agent is a short status read or a switch flip, and the agent is
// the service a presenter pauses at an armed snippet: a paused process answers nothing, and the
// default 100-second HttpClient timeout would hold the whole page - and its Detach button - with
// it. Ten seconds is generous for what these clients do.
var operationsAgentTimeout = TimeSpan.FromSeconds(10);
// One named client per service that registers demo snippets; the switchboard fans out across all
// of them so a snippet can be armed wherever its code lives.
builder.Services.AddHttpClient("breakpoints-operationsagent", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoControlWebOptions>>().Value;
    client.BaseAddress = new Uri(options.OperationsAgentBaseUri, UriKind.Absolute);
    client.Timeout = operationsAgentTimeout;
});
builder.Services.AddHttpClient("breakpoints-energyhub", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoControlWebOptions>>().Value;
    client.BaseAddress = new Uri(options.EnergyHubBaseUri, UriKind.Absolute);
});
builder.Services.AddHttpClient("breakpoints-securityagent", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoControlWebOptions>>().Value;
    client.BaseAddress = new Uri(options.SecurityAgentBaseUri, UriKind.Absolute);
});
builder.Services.AddHttpClient("breakpoints-workforceagent", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoControlWebOptions>>().Value;
    client.BaseAddress = new Uri(options.WorkforceAgentBaseUri, UriKind.Absolute);
});
builder.Services.AddSingleton<DemoBreakpointsApiClient>();
builder.Services.AddHttpClient<WorkforceRecordsApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoControlWebOptions>>().Value;
    client.BaseAddress = new Uri(options.WorkforceHubBaseUri, UriKind.Absolute);
});
builder.Services.AddHttpClient<WorkKnowledgeApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoControlWebOptions>>().Value;
    client.BaseAddress = new Uri(options.OperationsAgentBaseUri, UriKind.Absolute);
    client.Timeout = operationsAgentTimeout;
});
builder.Services.AddHttpClient<CaseMemoryApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoControlWebOptions>>().Value;
    client.BaseAddress = new Uri(options.OperationsAgentBaseUri, UriKind.Absolute);
    client.Timeout = operationsAgentTimeout;
});
builder.Services.AddHttpClient<ToolSourceApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoControlWebOptions>>().Value;
    client.BaseAddress = new Uri(options.OperationsAgentBaseUri, UriKind.Absolute);
    client.Timeout = operationsAgentTimeout;
});
builder.Services.AddHttpClient<SecurityConsultApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoControlWebOptions>>().Value;
    client.BaseAddress = new Uri(options.OperationsAgentBaseUri, UriKind.Absolute);
    client.Timeout = operationsAgentTimeout;
});
builder.Services.AddHttpClient<AgentHabitatApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoControlWebOptions>>().Value;
    client.BaseAddress = new Uri(options.OperationsAgentBaseUri, UriKind.Absolute);
    client.Timeout = operationsAgentTimeout;
});
// The debuggers the presenter can put on a service, in picker order. Each is an adapter over an
// IDE's own attach mechanism; the services never learn which one is on them.
builder.Services.AddSingleton<IDebuggerAdapter, VsCodeAttachService>();
builder.Services.AddSingleton<IDebuggerAdapter, VisualStudioAttachService>();
builder.Services.AddSingleton<DebuggerSelection>();
builder.Services.AddSingleton<RecoveryLocalSettings>();
builder.Services.AddHttpContextAccessor();
#pragma warning disable EXTEXP0001 // Explicitly remove the global 30s pipeline: agent turns are long-running and must not be retried.
builder.Services.AddHttpClient("recovery-rehearsal", client => client.Timeout = TimeSpan.FromSeconds(180))
    .RemoveAllResilienceHandlers(); // Never replay a model turn or supervisor decision on a transport failure.
builder.Services.AddHttpClient("recovery-probe", client => client.Timeout = TimeSpan.FromSeconds(2))
    .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
builder.Services.AddSingleton<IRecoveryLocalRuntime, RecoveryLocalRuntime>();
builder.Services.AddSingleton<IRecoveryRehearsalApi, RecoveryRehearsalApi>();
builder.Services.AddSingleton<RecoveryRehearsal>();
builder.Services.AddHostedService(services => services.GetRequiredService<RecoveryRehearsal>());
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// The local presenter can manage a child process and act as the rehearsal supervisor.
// Never expose those Blazor circuits to a remote client, even on a wildcard dev binding.
if (app.Services.GetRequiredService<RecoveryLocalSettings>().Enabled)
{
    app.Use(async (context, next) =>
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null || !System.Net.IPAddress.IsLoopback(address))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapDefaultEndpoints();
app.MapRecoveryDemo();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
