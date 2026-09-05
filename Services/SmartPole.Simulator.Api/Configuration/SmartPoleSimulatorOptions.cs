namespace SmartPole.Simulator.Api.Configuration;

/// <summary>
/// Configures the simulator's boot (and reset) state.
/// </summary>
public sealed class SmartPoleSimulatorOptions
{
    internal const string SectionName = "SmartPoleSimulator";

    /// <summary>
    /// Gets or sets a value indicating whether the pole boots into the forgotten-override
    /// situation - lamp ON during daylight against its schedule, manual override engaged, recent
    /// maintenance recorded - instead of the quiet baseline.
    /// <para>
    /// Exists for the CLOUD deployment, where there is no switchboard: the deployed demo surface
    /// is deliberately off, so the cloud city cannot be driven into a scenario and must boot into
    /// the one the lecture investigates. It is the state work order WO-8732 (in the presenter's
    /// OneDrive) explains, so the hosted agent's two sources - the deployed Energy Hub and Work IQ
    /// - tell one coherent story. Off by default: the local demo starts quiet and the presenter
    /// drives it.
    /// </para>
    /// </summary>
    public bool StartWithForgottenOverride { get; set; }
}
