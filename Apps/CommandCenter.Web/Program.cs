using CommandCenter.Web.Components;
using CommandCenter.Web.Configuration;
using CommandCenter.Web.Services;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOptions<CommandCenterWebOptions>()
    .BindConfiguration(CommandCenterWebOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHttpClient<CommandCenterApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<CommandCenterWebOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUri, UriKind.Absolute);
});
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers: replacing the default pipeline per client is this API's purpose.
builder.Services.AddHttpClient<OperationsAgentApiClient>((serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<CommandCenterWebOptions>>().Value;
        client.BaseAddress = new Uri(options.OperationsAgentBaseUri, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(120);
    })
    // The agent's server-side execution budget is 90 seconds and a cold model call can take about a
    // minute; the default resilience pipeline (10 s attempt timeout, retries) would abort and then
    // duplicate the model invocation. Give this client budget-aligned timeouts and no unsafe retries.
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler(resilience =>
    {
        resilience.Retry.DisableForUnsafeHttpMethods();
        resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(100);
        resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(110);
        resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(200);
    });

// The presenter's own credential, resolved at call time: az login on a laptop, anything else
// DefaultAzureCredential can find elsewhere. Registered unconditionally - it probes nothing until
// a token is first requested, and no token is requested unless the hosted panel is configured.
builder.Services.AddSingleton<Azure.Core.TokenCredential>(new Azure.Identity.DefaultAzureCredential());

// The Foundry-hosted twin of the Operations Agent. A hosted session can be cold - the sandbox is
// provisioned on demand and a first model call there takes a while - so this client gets a larger
// budget than the local agent's, and the same no-unsafe-retries rule: a retried POST would run the
// model twice.
builder.Services.AddHttpClient<HostedOperationsAgentClient>(client => client.Timeout = TimeSpan.FromSeconds(180))
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler(resilience =>
    {
        resilience.Retry.DisableForUnsafeHttpMethods();
        resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(170);
        resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(175);
        resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(340);
    });
#pragma warning restore EXTEXP0001
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
