using System.Text.Json;
using DemoControl.Contracts;
using DurableOperations.Contracts;

namespace CommandCenter.Web.Services;

/// <summary>Server-only adapter for the local recovery beat; no process controls or keys reach the browser.</summary>
internal sealed class DurableRecoveryClient(HttpClient client, IHostEnvironment environment)
{
    private static readonly Uri DirectorAddress = new UriBuilder(Uri.UriSchemeHttp, "localhost", 5187).Uri;
    private static readonly JsonSerializerOptions JsonOptions = CaesareaJsonDefaults.CreateSerializerOptions();
    public bool Enabled => environment.IsDevelopment() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));

    public Task<RecoveryRehearsalState> GetAsync(CancellationToken cancellationToken) => SendAsync("status", null, false, cancellationToken);
    public Task<RecoveryRehearsalState> BeginAsync(CancellationToken cancellationToken) => SendAsync("start", new { }, false, cancellationToken);
    public Task<RecoveryRehearsalState> ResumeAsync(CancellationToken cancellationToken) => SendAsync("resume", new { }, false, cancellationToken);
    public Task<RecoveryRehearsalState> DecideAsync(RecoveryReview review, bool approved, CancellationToken cancellationToken) =>
        SendAsync("decision", new RecoveryDecisionRequest(review, approved), true, cancellationToken);

    private async Task<RecoveryRehearsalState> SendAsync(string path, object? body, bool supervisor, CancellationToken cancellationToken)
    {
        if (!Enabled) { throw new InvalidOperationException("The durable recovery rehearsal is available only in local Development."); }
        var settings = FindSettings();
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(settings, cancellationToken));
        var key = document.RootElement.GetProperty("Values").GetProperty(supervisor ? "CAESAREA_SUPERVISOR_KEY" : "CAESAREA_OPERATOR_KEY").GetString();
        using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, new Uri(DirectorAddress, "api/recovery-demo/" + path));
        request.Headers.Add("X-Caesarea-Key", key);
        request.Headers.Add(CorrelationHeaderNames.XCorrelationId, CorrelationIds.Create());
        if (body is not null) { request.Content = JsonContent.Create(body, options: JsonOptions); }
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RecoveryRehearsalState>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The recovery status was empty.");
    }

    private string FindSettings()
    {
        for (var directory = new DirectoryInfo(environment.ContentRootPath); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Caesarea.slnx")))
            {
                var path = Path.Combine(directory.FullName, "Services", "OperationsAgent.Functions", "local.settings.json");
                if (File.Exists(path)) { return path; }
                break;
            }
        }

        throw new InvalidOperationException("Prepare Durable recovery in Demo Director; its one-time private settings are missing.");
    }
}
