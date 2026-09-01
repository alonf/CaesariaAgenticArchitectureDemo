using System.Collections.Concurrent;
using System.Diagnostics;

namespace Caesarea.ServiceDefaults;

/// <summary>
/// Names the demo code snippets that can be paused through <see cref="DemoBreakpoints"/>. The values
/// match the <c>#region</c> markers used to export demo snippets. Identifiers are semantic, stable,
/// and lecture-neutral: they name the concept being demonstrated, never a lecture code or slide
/// position - which deck presents a concept, and where, is presentation metadata that lives in the
/// deck's speaker notes, not in the identifier.
/// </summary>
public static class DemoSnippets
{
    /// <summary>
    /// Gets the agent-creation snippet: constructing the Operations Agent.
    /// </summary>
    public const string AgentCreation = "AGENT_CREATION";

    /// <summary>
    /// Gets the function-tool snippet: the deterministic streetlight state tool.
    /// </summary>
    public const string FunctionTool = "FUNCTION_TOOL";

    /// <summary>
    /// Gets the session-context snippet: continuing the conversation through an agent session.
    /// </summary>
    public const string Session = "AGENT_SESSION";

    /// <summary>
    /// Gets the knowledge-retrieval snippet: on-demand organizational knowledge retrieval.
    /// </summary>
    public const string Knowledge = "KNOWLEDGE_RETRIEVAL";

    /// <summary>
    /// Gets the case-memory snippet: recalling the agent's own closed cases as hypotheses.
    /// </summary>
    public const string CaseMemory = "CASE_MEMORY";

    /// <summary>
    /// Gets the agent-skills snippet: discovering and loading documented procedures on demand.
    /// </summary>
    public const string Skills = "AGENT_SKILLS";

    /// <summary>
    /// Gets the MCP server snippet: the Energy Hub serving its streetlight tool over the protocol.
    /// </summary>
    public const string McpServer = "MCP_SERVER";

    /// <summary>
    /// Gets the MCP client snippet: discovering the remote tool instead of compiling it in.
    /// </summary>
    public const string McpClient = "MCP_CLIENT";

    /// <summary>
    /// Gets the multi-round-trip-request snippet: a tool pausing input-required for operator
    /// approval before any side effect.
    /// </summary>
    public const string InteractiveInput = "MULTI_ROUND_TRIP_REQUEST";

    /// <summary>
    /// Gets the workflow snippet: building the explicit remediation orchestration graph.
    /// </summary>
    public const string Workflow = "WORKFLOW";
}

/// <summary>
/// Presenter-controlled demo breakpoints. When a debugger is attached and a snippet is armed, the code
/// pauses at the top of that snippet so the presenter can single-step it on stage. Calls are removed
/// from Release builds entirely, and without an attached debugger they are inert.
/// </summary>
public static class DemoBreakpoints
{
    private static readonly ConcurrentDictionary<string, bool> Snippets = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets a value indicating whether a debugger is attached to this process.
    /// </summary>
    public static bool IsDebuggerAttached => Debugger.IsAttached;

    /// <summary>
    /// Registers the snippets this service can pause on. Unregistered names cannot be armed.
    /// </summary>
    /// <param name="snippetNames">The snippet names, matching the exported <c>#region</c> markers.</param>
    public static void Register(params string[] snippetNames)
    {
        ArgumentNullException.ThrowIfNull(snippetNames);

        foreach (var snippetName in snippetNames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(snippetName);
            Snippets.TryAdd(snippetName, false);
        }
    }

    /// <summary>
    /// Gets the registered snippets and whether each one is currently armed.
    /// </summary>
    /// <returns>The registered snippets ordered by name.</returns>
    public static IReadOnlyList<DemoBreakpointStatus> GetStatus() =>
        [.. Snippets
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new DemoBreakpointStatus(pair.Key, pair.Value))];

    /// <summary>
    /// Arms or disarms one registered snippet.
    /// </summary>
    /// <param name="snippetName">The snippet to change.</param>
    /// <param name="armed"><see langword="true"/> to pause on the snippet's next run.</param>
    /// <returns><see langword="true"/> when the snippet is registered; otherwise <see langword="false"/>.</returns>
    public static bool TrySetArmed(string snippetName, bool armed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snippetName);

        if (!Snippets.ContainsKey(snippetName))
        {
            return false;
        }

        Snippets[snippetName] = armed;
        return true;
    }

    /// <summary>
    /// Consumes one armed snippet: returns <see langword="true"/> and disarms it when it was armed,
    /// implementing the one-shot "break on next run" semantic.
    /// </summary>
    /// <param name="snippetName">The snippet to consume.</param>
    /// <returns><see langword="true"/> when the snippet was armed and has now been disarmed.</returns>
    public static bool TryConsume(string snippetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snippetName);
        return Snippets.TryUpdate(snippetName, newValue: false, comparisonValue: true);
    }

    /// <summary>
    /// Pauses in the attached debugger when this snippet is armed, surfacing the break on the calling
    /// line so the presenter can single-step the snippet. Removed from Release builds; a no-op when no
    /// debugger is attached, leaving the snippet armed for a later debugged run.
    /// </summary>
    /// <param name="snippetName">The snippet requesting the pause.</param>
    [Conditional("DEBUG")]
    [DebuggerHidden]
    [DebuggerStepThrough]
    public static void Pause(string snippetName)
    {
        if (Debugger.IsAttached && TryConsume(snippetName))
        {
            Debugger.Break();
        }
    }
}

/// <summary>
/// Reports one registered demo breakpoint and whether it is armed.
/// </summary>
/// <param name="SnippetName">The snippet name, matching the exported <c>#region</c> marker.</param>
/// <param name="IsArmed">Whether the snippet will pause on its next debugged run.</param>
public sealed record DemoBreakpointStatus(string SnippetName, bool IsArmed);
