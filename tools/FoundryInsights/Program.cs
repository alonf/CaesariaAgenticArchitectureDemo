using System.ClientModel.Primitives;
using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;

#pragma warning disable AAIP001 // Agent Insights is an explicitly selected preview, isolated to this read-only utility.

// Presenter-side read-only utility. Never put monitor findings or proposed patches in the
// operational agent's tools. Output is JSON Lines; redirect only to private rehearsal artifacts.
if (args.Length == 1 && args[0] == "--help")
{
    Console.WriteLine("FoundryInsights <project-endpoint> <monitor-id> [--active]");
    Console.WriteLine("FoundryInsights <project-endpoint> --list-monitors [agent-name]");
    return 0;
}

if (args.Length is < 2 or > 3 || (args.Length == 3 && args[1] != "--list-monitors" && args[2] != "--active") ||
    !Uri.TryCreate(args[0], UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" ||
    !endpoint.Host.EndsWith(".services.ai.azure.com", StringComparison.OrdinalIgnoreCase) ||
    !endpoint.IsDefaultPort || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 ||
    endpoint.Fragment.Length != 0 || !endpoint.AbsolutePath.StartsWith("/api/projects/", StringComparison.Ordinal))
{
    await Console.Error.WriteLineAsync("Use --help for arguments. A valid HTTPS Foundry project endpoint is required.");
    return 1;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
var client = new AIProjectClient(endpoint, new AzureCliCredential());
var ids = new HashSet<string>(StringComparer.Ordinal);
try
{
    if (args[1] == "--list-monitors")
    {
        await foreach (var item in client.AgentInsightMonitors.GetAllAsync(
            agentName: args.Length == 3 ? args[2] : null, limit: 100, cancellationToken: cancellation.Token))
        {
            ids.Add(item.Id);
            Console.WriteLine(ModelReaderWriter.Write(item).ToString());
        }
        Console.WriteLine(JsonSerializer.Serialize(new { kind = "completion", complete = true, count = ids.Count }));
        return 0;
    }

    // A missing or inaccessible monitor is an error, never an empty healthy result.
    var monitor = await client.AgentInsightMonitors.GetAsync(args[1], cancellation.Token);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        kind = "scope",
        project = endpoint.AbsoluteUri,
        monitorId = args[1],
        agentName = monitor.Value.AgentName,
        statusFilter = args.Length == 3 ? "active" : "all",
        includeDetails = true
    }));

    // The SDK's asynchronous collection follows continuation pages. Keep the filters on the
    // collection and preserve the complete wire model, including absent versus present details.
    await foreach (var insight in client.AgentInsightMonitors.GetInsightsAsync(
        args[1], limit: 100, includeDetails: true,
        status: args.Length == 3 ? AgentInsightStatus.Active : null,
        cancellationToken: cancellation.Token).Where(insight => ids.Add(insight.Id)))
    {
        Console.WriteLine(ModelReaderWriter.Write(insight).ToString());
    }
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        kind = "completion",
        complete = true,
        count = ids.Count,
        interpretation = "Generated findings are advisory. No findings does not establish agent health."
    }));
    return 0;
}
catch (Exception exception)
{
    // Previously printed findings remain usable; the caller must retain this incomplete marker.
    Console.WriteLine(JsonSerializer.Serialize(new { kind = "completion", complete = false, count = ids.Count }));
    await Console.Error.WriteLineAsync($"Incomplete Insights retrieval: {exception.Message}");
    return 2;
}
