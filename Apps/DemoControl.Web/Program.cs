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
// One named client per service that registers demo snippets; the switchboard fans out across all
// of them so a snippet can be armed wherever its code lives.
builder.Services.AddHttpClient("breakpoints-operationsagent", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoControlWebOptions>>().Value;
    client.BaseAddress = new Uri(options.OperationsAgentBaseUri, UriKind.Absolute);
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
});
builder.Services.AddHttpClient<CaseMemoryApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoControlWebOptions>>().Value;
    client.BaseAddress = new Uri(options.OperationsAgentBaseUri, UriKind.Absolute);
});
builder.Services.AddHttpClient<ToolSourceApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoControlWebOptions>>().Value;
    client.BaseAddress = new Uri(options.OperationsAgentBaseUri, UriKind.Absolute);
});
builder.Services.AddHttpClient<SecurityConsultApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<DemoControlWebOptions>>().Value;
    client.BaseAddress = new Uri(options.OperationsAgentBaseUri, UriKind.Absolute);
});
builder.Services.AddSingleton<VsCodeAttachService>();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapDefaultEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
