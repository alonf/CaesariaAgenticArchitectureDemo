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
    [McpServerTool(Name = "get_streetlight_state", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
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
/// an operator confirmation the tool pauses input-required and produces no side effect; the
/// client re-invokes it with the answer, bound to a one-time server-issued request state so a
/// confirmation cannot be fabricated or replayed. MRTR provides interactive input - operator
/// confirmation - not authentication or policy authorization, which arrive in later stages.
/// </summary>
[McpServerToolType]
public sealed class EnergyRestoreMcpTool(
    EnergyHubService energyHub,
    MrtrRequestStateStore requestStateStore,
    IHttpContextAccessor httpContextAccessor)
{
    /// <summary>
    /// Restores a streetlight to its scheduled mode after explicit operator confirmation.
    /// </summary>
    /// <param name="server">The MCP server handling the request.</param>
    /// <param name="context">The request context carrying any interactive-input responses.</param>
    /// <param name="assetId">The streetlight asset identifier.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The command outcome, or a cancellation note when the operator declines.</returns>
    [McpServerTool(Name = "restore_scheduled_mode", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Restores a streetlight to its scheduled lighting mode, clearing any manual override. Requires explicit operator confirmation before anything changes.")]
    public async Task<string> RestoreScheduledModeAsync(
        McpServer server,
        RequestContext<CallToolRequestParams> context,
        [Description("The streetlight asset identifier, for example L-417.")] string assetId,
        CancellationToken cancellationToken)
    {
        DemoBreakpoints.Pause(DemoSnippets.InteractiveInput);

        // MRTR is negotiated by protocol version: a pre-MRTR client gets a refusal instead of a
        // pause it cannot understand. (A modern client without an elicitation handler fails on
        // its own side when asked - still with no side effect here.)
        if (!server.IsMrtrSupported)
        {
            return "Interactive input is not supported by the connected client; the restore was not performed.";
        }

        if (context.Params?.InputResponses?.TryGetValue("approval", out var response) == true)
        {
            // A continuation must echo the one-time state issued with the pause; without it (or
            // with expired/mismatched state) the confirmation is rejected, not honored.
            if (!requestStateStore.TryConsume(context.Params.RequestState, assetId))
            {
                return "Confirmation rejected: the request state is missing, expired, or was issued for a different asset. No change was made.";
            }

            if (!IsApproved(response))
            {
                return $"Cancelled: the operator declined restoring {assetId} to scheduled mode. No change was made.";
            }

            var result = await energyHub.RestoreScheduledModeAsync(assetId, ResolveCorrelationId(), cancellationToken);
            return $"{result.Status}: {result.Summary}";
        }

        // No side effect before input: pause the call and ask the operator, binding the eventual
        // continuation to this pause with a one-time request state.
        throw new InputRequiredException(
            inputRequests: new Dictionary<string, InputRequest>
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
            },
            requestState: requestStateStore.Issue(assetId));
    }

    private static bool IsApproved(InputResponse response)
    {
        var result = response.Deserialize(
            (JsonTypeInfo<ElicitResult>)McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(ElicitResult)));
        return result is { IsAccepted: true, Content: { } content }
            && content.TryGetValue("approved", out var approvedValue)
            && approvedValue.ValueKind == JsonValueKind.True;
    }

    // The command carries the caller's correlation so the Command Center can recognize the
    // resulting state change as this request's own mutation. Read straight from the request
    // header: the response may already be streaming, so nothing is written back here.
    private string ResolveCorrelationId() =>
        httpContextAccessor.HttpContext?.Request.Headers[CorrelationHeaderNames.XCorrelationId].FirstOrDefault()
        is { Length: > 0 } header
            ? header
            : CorrelationIds.Create();
}
#endregion
