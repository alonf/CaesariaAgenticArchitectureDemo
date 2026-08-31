using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace EnergyHub.Api.Services;

#region MCP_SERVER
/// <summary>
/// Exposes the Energy Hub's read-only streetlight tool over the Model Context Protocol. The tool
/// is owned and served by the Energy Hub boundary itself: any MCP-capable client can discover and
/// invoke it, and the Operations Agent no longer needs this service's REST client compiled in.
/// </summary>
[McpServerToolType]
public sealed class EnergyMcpTools(EnergyHubService energyHub)
{
    /// <summary>
    /// Gets the current authoritative operational state of a streetlight.
    /// </summary>
    /// <param name="assetId">The streetlight asset identifier.</param>
    /// <returns>The authoritative operational twin.</returns>
    [McpServerTool(Name = "get_streetlight_state")]
    [Description("Gets the current authoritative operational state of a streetlight.")]
    public EnergyOperationalTwin GetStreetlightState(
        [Description("The streetlight asset identifier, for example L-417.")] string assetId)
    {
        DemoBreakpoints.Pause(DemoSnippets.McpServer);
        return energyHub.GetState(assetId);
    }
}
#endregion

#region MULTI_ROUND_TRIP_REQUEST
/// <summary>
/// The demo's first write-capable tool, guarded by MCP Multi Round-Trip Requests (MRTR): without
/// an operator approval in the request the tool pauses input-required and produces no side
/// effect; the client re-invokes it with the operator's answer, and only an explicit approval
/// executes the restore.
/// </summary>
[McpServerToolType]
public sealed class EnergyRestoreMcpTool(EnergyHubService energyHub)
{
    /// <summary>
    /// Restores a streetlight to its scheduled mode after explicit operator approval.
    /// </summary>
    /// <param name="server">The MCP server handling the request.</param>
    /// <param name="context">The request context carrying any interactive-input responses.</param>
    /// <param name="assetId">The streetlight asset identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The command outcome, or a cancellation note when the operator declines.</returns>
    [McpServerTool(Name = "restore_scheduled_mode")]
    [Description("Restores a streetlight to its scheduled lighting mode, clearing any manual override. Requires explicit operator approval before anything changes.")]
    public async Task<string> RestoreScheduledModeAsync(
        McpServer server,
        RequestContext<CallToolRequestParams> context,
        [Description("The streetlight asset identifier, for example L-417.")] string assetId,
        CancellationToken cancellationToken)
    {
        DemoBreakpoints.Pause(DemoSnippets.InteractiveInput);

        if (TryGetApproval(context, out var approved))
        {
            if (!approved)
            {
                return $"Cancelled: the operator declined restoring {assetId} to scheduled mode. No change was made.";
            }

            var result = await energyHub.RestoreScheduledModeAsync(assetId, Guid.NewGuid().ToString("N"), cancellationToken);
            return $"{result.Status}: {result.Summary}";
        }

        if (!server.IsMrtrSupported)
        {
            return "Interactive input is not supported by the connected client; the restore was not performed.";
        }

        // No side effect before input: pause the call and ask the operator.
        throw new InputRequiredException(inputRequests: new Dictionary<string, InputRequest>
        {
            ["approval"] = InputRequest.ForElicitation(new ElicitRequestParams
            {
                Message = $"Restore {assetId} to scheduled mode? Any active manual override will be cleared.",
                RequestedSchema = new ElicitRequestParams.RequestSchema
                {
                    Properties =
                    {
                        ["approved"] = new ElicitRequestParams.BooleanSchema
                        {
                            Description = "True to perform the restore; false to cancel it."
                        }
                    },
                    Required = ["approved"]
                }
            })
        });
    }

    private static bool TryGetApproval(RequestContext<CallToolRequestParams> context, out bool approved)
    {
        approved = false;

        if (context.Params?.InputResponses?.TryGetValue("approval", out var response) != true)
        {
            return false;
        }

        var result = response!.Deserialize(
            (JsonTypeInfo<ElicitResult>)McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(ElicitResult)));
        approved = result is { IsAccepted: true, Content: { } content }
            && content.TryGetValue("approved", out var approvedValue)
            && approvedValue.ValueKind == JsonValueKind.True;
        return true;
    }
}
#endregion
