using System.Text.Json;
using Microsoft.Agents.AI;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Describes one skill available in the skills directory.
/// </summary>
/// <param name="Name">The skill name from the SKILL.md frontmatter.</param>
/// <param name="Description">The skill description from the SKILL.md frontmatter.</param>
public sealed record SkillDescriptor(string Name, string Description);

/// <summary>
/// Demo-side helpers around the SDK's skills support. Skill discovery itself is never duplicated
/// here: the UI trace enumerates skills through the SDK's own <see cref="AgentFileSkillsSource"/>,
/// so what the response reports is exactly what the <c>AgentSkillsProvider</c> would advertise -
/// same parsing, same validation, same skips.
/// </summary>
internal static class SkillCatalog
{
    /// <summary>
    /// Resolves the skills directory: an absolute configured path is used as-is; a relative one is
    /// resolved against the repository root (located by walking up from the application base
    /// directory), so the presenter edits the same source files the agent reads.
    /// </summary>
    /// <param name="configuredPath">The configured skills directory.</param>
    /// <returns>The resolved directory, or <see langword="null"/> when it cannot be located.</returns>
    public static string? ResolveDirectory(string configuredPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredPath);

        if (Path.IsPathRooted(configuredPath))
        {
            return Directory.Exists(configuredPath) ? configuredPath : null;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Caesarea.slnx")))
            {
                var skillsPath = Path.Combine(directory.FullName, configuredPath);
                return Directory.Exists(skillsPath) ? skillsPath : null;
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// Enumerates the skills the SDK would advertise from the supplied directory, using the SDK's
    /// own file-skills source (invalid or unreadable files are skipped with SDK-logged warnings).
    /// Discovery runs fresh on every call, so presenter edits take effect on the next request.
    /// </summary>
    /// <param name="skillsDirectory">The resolved skills directory.</param>
    /// <param name="agent">The agent the skills would be provided to.</param>
    /// <param name="loggerFactory">The logger factory for SDK discovery diagnostics.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The discovered skills, ordered by name.</returns>
    public static async Task<IReadOnlyList<SkillDescriptor>> DescribeAsync(
        string? skillsDirectory,
        AIAgent agent,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (skillsDirectory is null || !Directory.Exists(skillsDirectory))
        {
            return [];
        }

        using var source = new AgentFileSkillsSource(skillsDirectory, loggerFactory: loggerFactory);
        var skills = await source.GetSkillsAsync(new AgentSkillsSourceContext(agent, session: null), cancellationToken);

        return [.. skills
            .Select(skill => new SkillDescriptor(skill.Frontmatter.Name, skill.Frontmatter.Description))
            .OrderBy(skill => skill.Name, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Determines whether a recorded <c>load_skill</c> invocation targeted the supplied skill by
    /// parsing the exact <c>skillName</c> argument - the SDK performs an exact lookup, so a
    /// substring or case-insensitive match would over-report.
    /// </summary>
    /// <param name="argumentsJson">The recorded tool arguments as JSON.</param>
    /// <param name="skillName">The advertised skill name.</param>
    public static bool IsLoadSkillCallFor(string argumentsJson, string skillName)
    {
        try
        {
            using var arguments = JsonDocument.Parse(argumentsJson);
            return arguments.RootElement.ValueKind == JsonValueKind.Object
                && arguments.RootElement.TryGetProperty("skillName", out var nameProperty)
                && nameProperty.ValueKind == JsonValueKind.String
                && string.Equals(nameProperty.GetString(), skillName, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
