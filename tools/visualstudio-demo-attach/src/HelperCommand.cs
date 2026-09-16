using System.Globalization;

namespace VisualStudioDemoAttach;

/// <summary>
/// The parsed command line: an operation and the options that name the solution and the process.
/// </summary>
/// <param name="Operation">One of <see cref="Operations"/>.</param>
/// <param name="Solution">The solution the Visual Studio instance must have open, when the caller pins one.</param>
/// <param name="ProcessName">The target executable name, for example OperationsAgent.Api.exe.</param>
/// <param name="ProcessId">The target process id, which wins over the name when both are given.</param>
/// <param name="Error">Why the command line could not be parsed, or <see langword="null"/> when it could.</param>
/// <param name="InstanceProcessId">The devenv process id an earlier attach used, which pins this operation to that window.</param>
internal sealed record HelperCommand(string Operation, string? Solution, string? ProcessName, int? ProcessId, string? Error, int? InstanceProcessId = null)
{
    /// <summary>
    /// The operations the helper performs.
    /// </summary>
    public static readonly IReadOnlyList<string> Operations = ["status", "attach", "detach", "instances"];

    /// <summary>
    /// The one-line usage printed with every command-line error.
    /// </summary>
    public const string Usage = "Usage: VisualStudioDemoAttach <status|attach|detach|instances> [--solution <path>] [--process-name <name>] [--process-id <pid>] [--instance-pid <devenv pid>]";

    /// <summary>
    /// Parses the command line. Never throws: a bad command line is reported in <see cref="Error"/>
    /// so the caller still gets a JSON report.
    /// </summary>
    /// <param name="args">The raw arguments.</param>
    /// <returns>The command, complete or carrying its error.</returns>
    public static HelperCommand Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
        {
            return Invalid("An operation is required.");
        }

        var operation = args[0].ToLowerInvariant();

        if (!Operations.Contains(operation, StringComparer.Ordinal))
        {
            return Invalid($"Unknown operation '{args[0]}'.");
        }

        string? solution = null;
        string? processName = null;
        int? processId = null;
        int? instanceProcessId = null;

        // Options come in pairs: the name, then its value.
        var index = 1;

        while (index < args.Length)
        {
            var option = args[index];
            var value = index + 1 < args.Length ? args[index + 1] : null;

            if (value is null)
            {
                return Invalid($"Option '{option}' needs a value.");
            }

            switch (option.ToLowerInvariant())
            {
                case "--solution":
                    solution = value;
                    break;
                case "--process-name":
                    processName = value;
                    break;
                case "--process-id":
                    if (!TryParseProcessId(value, out processId))
                    {
                        return Invalid($"'{value}' is not a process id.");
                    }

                    break;
                case "--instance-pid":
                    if (!TryParseProcessId(value, out instanceProcessId))
                    {
                        return Invalid($"'{value}' is not a process id.");
                    }

                    break;
                default:
                    return Invalid($"Unknown option '{option}'.");
            }

            index += 2;
        }

        if (operation is "attach" or "detach" && processName is null && processId is null)
        {
            return Invalid($"'{operation}' needs --process-id or --process-name.");
        }

        return new HelperCommand(operation, solution, processName, processId, Error: null, instanceProcessId);
    }

    private static bool TryParseProcessId(string value, out int? processId)
    {
        processId = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed : null;
        return processId is not null;
    }

    /// <summary>
    /// Describes the target the way messages name it: the id when known, the name otherwise.
    /// </summary>
    /// <returns>The description.</returns>
    public string DescribeTarget() => (ProcessName, ProcessId) switch
    {
        (not null, not null) => $"{ProcessName} (PID {ProcessId})",
        (null, not null) => $"PID {ProcessId}",
        (not null, null) => ProcessName,
        _ => "the process"
    };

    private static HelperCommand Invalid(string error) => new("usage", null, null, null, error);
}
