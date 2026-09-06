using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace OperationsAgent.Api.Services.Capabilities;

/// <summary>
/// One stage-gated capability of the Operations Agent, owning all of its pieces: what it adds to
/// the agent before the run (tools, context providers), what it needs once the agent exists, what
/// it reports about the run afterwards, and what it must dispose. Capabilities compose in lecture
/// order, so the toolbox reads like the stages do, and each lives in its own file with its own
/// demo snippet region - a stage is one file, not three places in one method.
/// </summary>
internal interface IAgentCapability : IAsyncDisposable
{
    /// <summary>
    /// Whether the stage admits this capability at all. A capability that is not available is
    /// neither composed nor asked to describe the run.
    /// </summary>
    public bool IsAvailable(DemoStage stage);

    /// <summary>
    /// Adds this capability's tools and context providers for the request.
    /// </summary>
    public ValueTask ComposeAsync(AgentComposition composition, CancellationToken cancellationToken);

    /// <summary>
    /// Runs once the agent exists and before it does anything, for what needs the agent itself.
    /// </summary>
    public ValueTask PrepareAsync(AIAgent agent, CancellationToken cancellationToken);

    /// <summary>
    /// Contributes this capability's part of the operator-facing trace.
    /// </summary>
    public void Describe(AgentRunTrace trace, OperationsAgentAnswerParts parts);
}

/// <summary>
/// The toolbox and context one request's capabilities assemble, in the order they compose.
/// </summary>
internal sealed class AgentComposition(DemoStage stage, string correlationId)
{
    public DemoStage Stage { get; } = stage;

    public string CorrelationId { get; } = correlationId;

    public List<AITool> Tools { get; } = [];

    public List<AIContextProvider> ContextProviders { get; } = [];

    /// <summary>
    /// Where the streetlight tool came from this request; reported with the answer.
    /// </summary>
    public OperationsAgentToolSource ToolSource { get; set; } = OperationsAgentToolSource.Local;
}

/// <summary>
/// What the run left behind: every model round trip, and the operator's decision on each
/// intercepted call.
/// </summary>
internal sealed record AgentRunTrace(ModelExchangeRecorder? Recorder, IReadOnlyDictionary<string, bool> ApprovalDecisions);

/// <summary>
/// The capability-owned parts of an answer, filled in by each capability's <see cref="IAgentCapability.Describe"/>.
/// </summary>
internal sealed class OperationsAgentAnswerParts
{
    public List<OperationsAgentEvidence> Evidence { get; } = [];

    public List<OperationsAgentRecalledCase> RecalledCases { get; } = [];

    public List<OperationsAgentSkill> Skills { get; } = [];

    public List<OperationsAgentDelegation> Delegations { get; } = [];
}

/// <summary>
/// No-op hooks, so a capability implements only the pieces it has.
/// </summary>
internal abstract class AgentCapability : IAgentCapability
{
    public abstract bool IsAvailable(DemoStage stage);

    public abstract ValueTask ComposeAsync(AgentComposition composition, CancellationToken cancellationToken);

    public virtual ValueTask PrepareAsync(AIAgent agent, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public virtual void Describe(AgentRunTrace trace, OperationsAgentAnswerParts parts)
    {
        // Nothing to report unless a capability says otherwise.
    }

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// A required tool from a discovered set, or the failure naming the server that did not offer it.
    /// </summary>
    protected static McpClientTool FindDiscoveredTool(IList<McpClientTool> discoveredTools, string toolName, string sourceName) =>
        discoveredTools.FirstOrDefault(tool => tool.Name == toolName)
        ?? throw new OperationsAgentToolUnavailableException(
            $"The {sourceName} MCP server did not offer the required tool '{toolName}'.");
}
