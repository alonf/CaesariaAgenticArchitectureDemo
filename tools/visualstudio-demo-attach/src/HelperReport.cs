using System.Text.Json.Serialization;

namespace VisualStudioDemoAttach;

/// <summary>
/// What the helper prints as one JSON object on every exit, success or failure, so DemoControl
/// reads one contract and never scrapes console text.
/// </summary>
/// <param name="Succeeded">Whether the operation succeeded.</param>
/// <param name="Operation">The operation reported: status, attach, detach or instances.</param>
/// <param name="Message">A presenter-facing sentence.</param>
/// <param name="Attached">Whether the target process is debugged by the chosen instance after the operation.</param>
/// <param name="Instance">The Visual Studio instance chosen for the solution, when one qualified.</param>
/// <param name="Instances">Every running Visual Studio instance, when the operation looked at all of them.</param>
/// <param name="Target">The process the operation resolved, when it resolved one.</param>
internal sealed record HelperReport(
    bool Succeeded,
    string Operation,
    string Message,
    bool Attached = false,
    VisualStudioInstanceReport? Instance = null,
    IReadOnlyList<VisualStudioInstanceReport>? Instances = null,
    TargetReport? Target = null)
{
    /// <summary>
    /// Builds a failed report.
    /// </summary>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="message">Why, in a sentence the presenter can act on.</param>
    /// <param name="instances">The instances seen, so the presenter knows what was there.</param>
    /// <returns>The report.</returns>
    public static HelperReport Failed(string operation, string message, IReadOnlyList<VisualStudioInstanceReport>? instances = null) =>
        new(false, operation, message, Instances: instances);
}

/// <summary>
/// One running Visual Studio instance as the helper saw it.
/// </summary>
/// <param name="DisplayName">The product name, for example Visual Studio 2026 Enterprise.</param>
/// <param name="Version">The product version the instance reports.</param>
/// <param name="ProcessId">The devenv process id.</param>
/// <param name="Solution">The full path of the open solution, or <see langword="null"/> when none is open.</param>
/// <param name="Supported">Whether the demo supports attaching through this version.</param>
internal sealed record VisualStudioInstanceReport(string DisplayName, string Version, int ProcessId, string? Solution, bool Supported);

/// <summary>
/// The process an operation resolved.
/// </summary>
/// <param name="ProcessName">The executable name.</param>
/// <param name="ProcessId">The process id.</param>
internal sealed record TargetReport(string ProcessName, int ProcessId);

/// <summary>
/// Source-generated JSON for the report: camelCase, nulls omitted.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HelperReport))]
internal sealed partial class HelperReportJsonContext : JsonSerializerContext;
