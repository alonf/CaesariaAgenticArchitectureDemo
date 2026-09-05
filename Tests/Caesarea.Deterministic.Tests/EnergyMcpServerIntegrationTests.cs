using System.Text.Json;
using EnergyHub.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// Boots the real Energy Hub MCP server in process (only the SmartPole gateway is faked) and
/// drives it with the real MCP client, pinning the wire contract the lecture demo depends on:
/// tool discovery with honest annotations, the read tool staying read-only, and the MRTR
/// restore producing no side effect without an approval, none on denial, and exactly the
/// caller-correlated command on approval.
/// </summary>
public sealed class EnergyMcpServerIntegrationTests
{
    private const string ReadToolName = "get_streetlight_state";
    private const string RestoreToolName = "restore_scheduled_mode";

    [Fact]
    public async Task DiscoveryExposesBothToolsWithHonestAnnotations()
    {
        var (factory, _) = CreateServer();
        await using var _1 = factory;
        await using var client = await CreateMcpClientAsync(factory);

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        var read = Assert.Single(tools, tool => tool.Name == ReadToolName);
        var readAnnotations = read.ProtocolTool.Annotations;
        Assert.NotNull(readAnnotations);
        Assert.True(readAnnotations.ReadOnlyHint);
        Assert.False(readAnnotations.DestructiveHint);
        Assert.True(readAnnotations.IdempotentHint);
        Assert.False(readAnnotations.OpenWorldHint);

        // The write tool is conservatively annotated: clearing an operator's override discards
        // intent a human expressed, and a repeat call issues another physical command rather than
        // collapsing into the first.
        var restore = Assert.Single(tools, tool => tool.Name == RestoreToolName);
        var restoreAnnotations = restore.ProtocolTool.Annotations;
        Assert.NotNull(restoreAnnotations);
        Assert.False(restoreAnnotations.ReadOnlyHint);
        Assert.True(restoreAnnotations.DestructiveHint);
        Assert.False(restoreAnnotations.IdempotentHint);
        Assert.False(restoreAnnotations.OpenWorldHint);
    }

    [Fact]
    public async Task ReadToolReturnsTheTwinWithoutCommandingAnything()
    {
        var (factory, gateway) = CreateServer();
        await using var _1 = factory;
        await using var client = await CreateMcpClientAsync(factory);

        var result = await client.CallToolAsync(
            ReadToolName,
            new Dictionary<string, object?> { ["assetId"] = DemoAssets.StreetlightAssetId },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEqual(true, result.IsError);
        Assert.Contains(DemoAssets.StreetlightAssetId, GetText(result), StringComparison.OrdinalIgnoreCase);
        Assert.Null(gateway.LastCommand);
        Assert.Null(gateway.LastCorrelationId);
    }

    [Fact]
    public async Task RestoreWithoutAnElicitationHandlerNeverExecutes()
    {
        var (factory, gateway) = CreateServer();
        await using var _1 = factory;
        // MRTR is negotiated by protocol version, so a modern client without an elicitation
        // handler still receives the pause - and fails on its own side answering it. What the
        // demo guarantees is the server half: no confirmation, no side effect.
        await using var client = await CreateMcpClientAsync(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.CallToolAsync(
                RestoreToolName,
                new Dictionary<string, object?> { ["assetId"] = DemoAssets.StreetlightAssetId },
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Null(gateway.LastCommand);
        Assert.Null(gateway.LastCorrelationId);
    }

    [Fact]
    public async Task DeniedConfirmationLeavesTheAssetUntouched()
    {
        var (factory, gateway) = CreateServer();
        await using var _1 = factory;
        await using var client = await CreateMcpClientAsync(factory, elicitationHandler: Answer(approved: false));

        var result = await client.CallToolAsync(
            RestoreToolName,
            new Dictionary<string, object?> { ["assetId"] = DemoAssets.StreetlightAssetId },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("Cancelled", GetText(result), StringComparison.Ordinal);
        Assert.Null(gateway.LastCommand);
        Assert.Null(gateway.LastCorrelationId);
    }

    [Fact]
    public async Task ApprovedConfirmationExecutesTheRestoreWithTheCallerCorrelation()
    {
        var (factory, gateway) = CreateServer();
        await using var _1 = factory;
        await using var client = await CreateMcpClientAsync(
            factory,
            correlationId: "mcp-integration-corr",
            elicitationHandler: Answer(approved: true));

        var result = await client.CallToolAsync(
            RestoreToolName,
            new Dictionary<string, object?> { ["assetId"] = DemoAssets.StreetlightAssetId },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("Succeeded", GetText(result), StringComparison.Ordinal);
        Assert.NotNull(gateway.LastCommand);
        // Daylight with no lighting requirement: scheduled mode means the lamp goes off.
        Assert.False(gateway.LastCommand.DesiredIsOn);
        Assert.Equal("mcp-integration-corr", gateway.LastCorrelationId);
    }

    private static (WebApplicationFactory<EnergyMcpTools> Factory, FakeSmartPoleGateway Gateway) CreateServer()
    {
        var gateway = new FakeSmartPoleGateway { PhysicalState = CreateOverriddenDaylightState() };
        var factory = new WebApplicationFactory<EnergyMcpTools>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISmartPoleGateway>();
                services.AddSingleton<ISmartPoleGateway>(gateway);

                // These tests pin what the TOOLS do to the gateway, and several assert it was
                // never touched at all. The startup hydration performs a legitimate background
                // read that would trip those assertions (and race the correlation-id checks), so
                // it is removed here rather than weakening what the assertions say.
                var hydration = services.FirstOrDefault(descriptor =>
                    descriptor.ImplementationType == typeof(TwinHydration));
                if (hydration is not null)
                {
                    services.Remove(hydration);
                }
            });
        });
        return (factory, gateway);
    }

    private static async Task<McpClient> CreateMcpClientAsync(
        WebApplicationFactory<EnergyMcpTools> factory,
        string? correlationId = null,
        Func<ElicitRequestParams?, CancellationToken, ValueTask<ElicitResult>>? elicitationHandler = null)
    {
        var httpClient = factory.CreateClient();

        if (correlationId is not null)
        {
            httpClient.DefaultRequestHeaders.Add(CorrelationHeaderNames.XCorrelationId, correlationId);
        }

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(httpClient.BaseAddress!, "/mcp") },
            httpClient,
            loggerFactory: null,
            ownsHttpClient: true);

        var options = elicitationHandler is null
            ? null
            : new McpClientOptions
            {
                Handlers = new McpClientHandlers { ElicitationHandler = elicitationHandler }
            };

        return await McpClient.CreateAsync(
            transport,
            options,
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private static Func<ElicitRequestParams?, CancellationToken, ValueTask<ElicitResult>> Answer(bool approved) =>
        (_, _) => ValueTask.FromResult(new ElicitResult
        {
            Action = "accept",
            Content = new Dictionary<string, JsonElement>
            {
                ["approved"] = JsonSerializer.SerializeToElement(approved)
            }
        });

    private static string GetText(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));

    private static SmartPolePhysicalState CreateOverriddenDaylightState() => new(
        DemoAssets.StreetlightAssetId,
        DemoAssets.NorthPromenadeArea,
        IsOn: true,
        IsDaylight: true,
        ExpectedScheduledState: false,
        ManualOverride: true,
        ControllerHealthInfo.Healthy,
        LastCommand: null,
        LastMaintenanceTime: null,
        HasRecentMaintenance: false,
        OperationalContext.None,
        LastReportedAt: new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero),
        SmartPoleBehaviorConfiguration.Default);
}
