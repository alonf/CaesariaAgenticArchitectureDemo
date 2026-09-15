namespace VisualStudioDemoAttach;

/// <summary>
/// Chooses the debug engine for a .NET 10 service. Visual Studio's "Attach to Process" dialog lists
/// several managed engines; attaching with the wrong one - the .NET Framework engine, say - reports
/// success and then breaks nowhere.
/// </summary>
internal static class DebuggerEngines
{
    /// <summary>
    /// The engine name Visual Studio uses for .NET Core and .NET 5 and later.
    /// </summary>
    public const string Preferred = "Managed (.NET Core, .NET 5+)";

    private const string PreferredPrefix = "Managed (.NET Core";

    /// <summary>
    /// Picks the engine from the names the instance's default transport offers.
    /// </summary>
    /// <param name="engineNames">The engine names, in Visual Studio's order.</param>
    /// <returns>The engine to attach with, or <see langword="null"/> to let Visual Studio auto-detect when no .NET Core engine is listed.</returns>
    public static string? Choose(IEnumerable<string> engineNames)
    {
        ArgumentNullException.ThrowIfNull(engineNames);

        var names = engineNames.ToList();

        return names.Find(name => string.Equals(name, Preferred, StringComparison.OrdinalIgnoreCase))
            ?? names.Find(name => name.StartsWith(PreferredPrefix, StringComparison.OrdinalIgnoreCase));
    }
}
