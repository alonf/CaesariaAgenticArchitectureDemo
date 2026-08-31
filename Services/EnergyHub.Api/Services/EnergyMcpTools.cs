using System.ComponentModel;
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
