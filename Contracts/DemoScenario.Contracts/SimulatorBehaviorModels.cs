namespace DemoScenario.Contracts;

/// <summary>
/// Presenter-facing simulator behavior settings used to inject delay, timeout, and failure into the
/// simulated vendor system. Applying or resetting a scenario returns these settings to their defaults.
/// </summary>
/// <param name="CommandDelayMs">The artificial command delay in milliseconds (0 to 30000).</param>
/// <param name="SimulateTimeout">Whether commands report a timeout instead of completing.</param>
/// <param name="SimulateFailure">Whether commands report a controller failure.</param>
public sealed record SimulatorBehaviorSettings(int CommandDelayMs, bool SimulateTimeout, bool SimulateFailure);
