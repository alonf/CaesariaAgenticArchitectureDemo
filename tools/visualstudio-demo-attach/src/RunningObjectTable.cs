using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace VisualStudioDemoAttach;

/// <summary>
/// Finds the Visual Studio instances registered in the COM running-object table. Every devenv
/// registers its automation object there under <c>!VisualStudio.DTE.&lt;version&gt;:&lt;pid&gt;</c>,
/// which is how a second process finds a specific instance rather than "whichever answers first".
/// </summary>
internal static class RunningObjectTable
{
    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(uint reserved, out IRunningObjectTable table);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(uint reserved, out IBindCtx context);

    /// <summary>
    /// Lists every registered Visual Studio automation object, any version.
    /// </summary>
    /// <returns>The instances, in table order.</returns>
    public static IReadOnlyList<RunningVisualStudio> FindVisualStudioInstances()
    {
        Marshal.ThrowExceptionForHR(GetRunningObjectTable(0, out var table));
        Marshal.ThrowExceptionForHR(CreateBindCtx(0, out var context));

        table.EnumRunning(out var monikers);
        monikers.Reset();

        var found = new List<RunningVisualStudio>();
        var buffer = new IMoniker[1];

        while (monikers.Next(1, buffer, IntPtr.Zero) == 0)
        {
            buffer[0].GetDisplayName(context, null, out var displayName);

            if (!VisualStudioMoniker.TryParse(displayName, out var majorVersion, out var processId))
            {
                continue;
            }

            if (table.GetObject(buffer[0], out var instance) == 0 && instance is not null)
            {
                found.Add(new RunningVisualStudio(majorVersion, processId, instance));
            }
        }

        return found;
    }
}

/// <summary>
/// One Visual Studio automation object from the running-object table.
/// </summary>
/// <param name="MajorVersion">The major version from the moniker, 18 for Visual Studio 2026.</param>
/// <param name="ProcessId">The devenv process id from the moniker.</param>
/// <param name="Dte">The automation object, castable to <c>EnvDTE80.DTE2</c>.</param>
internal sealed record RunningVisualStudio(int MajorVersion, int ProcessId, object Dte);

/// <summary>
/// Parses the running-object-table moniker Visual Studio registers.
/// </summary>
internal static class VisualStudioMoniker
{
    private const string Prefix = "!VisualStudio.DTE.";

    /// <summary>
    /// Reads the major version and process id from a moniker such as <c>!VisualStudio.DTE.18.0:12345</c>.
    /// </summary>
    /// <param name="displayName">The moniker display name.</param>
    /// <param name="majorVersion">The major version, when parsed.</param>
    /// <param name="processId">The process id, when parsed.</param>
    /// <returns><see langword="true"/> when the name is a Visual Studio moniker.</returns>
    public static bool TryParse(string? displayName, out int majorVersion, out int processId)
    {
        majorVersion = 0;
        processId = 0;

        if (displayName is null || !displayName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = displayName.AsSpan(Prefix.Length);
        var colon = rest.IndexOf(':');

        if (colon < 0)
        {
            return false;
        }

        var version = rest[..colon];
        var dot = version.IndexOf('.');
        var major = dot < 0 ? version : version[..dot];

        return int.TryParse(major, NumberStyles.None, CultureInfo.InvariantCulture, out majorVersion)
            && int.TryParse(rest[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out processId)
            && processId > 0;
    }
}
