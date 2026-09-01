namespace OperationsAgent.Api.Services;

/// <summary>
/// Presenter-controlled availability of the Security Operations Agent consult. It defaults to
/// off, so entering the MultiAgent stage changes nothing until the presenter turns it on - and
/// the same question answered with and without the consult is what proves the second agent earns
/// its cost, rather than merely asserting it.
/// </summary>
public sealed class SecurityConsultSwitch
{
    private volatile bool _enabled;

    /// <summary>
    /// Gets or sets a value indicating whether the agent may consult the Security Agent.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }
}
