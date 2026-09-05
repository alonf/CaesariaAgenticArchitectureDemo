namespace OperationsAgent.Api.Services;

/// <summary>
/// Presenter-controlled selection of which habitat answers the Command Center's agent questions:
/// the Aspire-composed agent in this process, or the same code deployed to Microsoft Foundry's
/// hosted runtime. Defaults to local, so entering the Hosting stage changes nothing until the
/// presenter flips the toggle live - the flip is the lecture beat, exactly as it is for the tool
/// source.
/// <para>
/// The switch lives in this service rather than in the web app because the switchboard already
/// speaks to this API for every other presenter toggle, and because the state must survive a
/// browser refresh mid-demo.
/// </para>
/// </summary>
public sealed class AgentHabitatSwitch
{
    private volatile OperationsAgentHabitat _current = OperationsAgentHabitat.Local;

    /// <summary>
    /// Gets or sets the habitat used for subsequent Command Center questions.
    /// </summary>
    public OperationsAgentHabitat Current
    {
        get => _current;
        set => _current = value;
    }
}
