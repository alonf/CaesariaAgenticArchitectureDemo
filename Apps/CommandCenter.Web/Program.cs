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
