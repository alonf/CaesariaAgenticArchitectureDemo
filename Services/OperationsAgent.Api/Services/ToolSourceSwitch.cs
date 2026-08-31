namespace OperationsAgent.Api.Services;

/// <summary>
/// Presenter-controlled selection of where the agent's streetlight tool comes from: the local
/// function compiled into this service, or discovery from the Energy Hub's MCP server. Defaults
/// to local, so entering the McpTools stage changes nothing until the presenter flips the toggle
/// live - the flip is the lecture beat.
/// </summary>
public sealed class ToolSourceSwitch
{
    private volatile OperationsAgentToolSource _current = OperationsAgentToolSource.Local;

    /// <summary>
    /// Gets or sets the tool source used for subsequent agent runs.
    /// </summary>
    public OperationsAgentToolSource Current
    {
        get => _current;
        set => _current = value;
    }
}
