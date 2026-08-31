using System.Text.RegularExpressions;

namespace OperationsAgent.Api.Services;

/// <summary>
/// Describes one skill available in the skills directory.
/// </summary>
/// <param name="Name">The skill name from the SKILL.md frontmatter.</param>
/// <param name="Description">The skill description from the SKILL.md frontmatter.</param>
public sealed record SkillDescriptor(string Name, string Description);

/// <summary>
/// Lightweight discovery of SKILL.md skills for the UI trace. The SDK's
/// <c>AgentSkillsProvider</c> performs its own discovery for the model; this catalog reads only
/// the frontmatter so the response can report which skills were advertised. Files are read fresh
/// on every call, so presenter edits take effect on the next request.
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
    /// Reads the frontmatter of every SKILL.md under the supplied directory (one level of skill
    /// folders, matching the agent skills layout).
    /// </summary>
    /// <param name="skillsDirectory">The resolved skills directory.</param>
    /// <returns>The discovered skills, ordered by name.</returns>
    public static IReadOnlyList<SkillDescriptor> Describe(string? skillsDirectory)
    {
        if (skillsDirectory is null || !Directory.Exists(skillsDirectory))
        {
            return [];
        }

        return [.. Directory
            .GetFiles(skillsDirectory, "SKILL.md", SearchOption.AllDirectories)
            .Select(ParseFrontmatter)
            .OfType<SkillDescriptor>()
            .OrderBy(skill => skill.Name, StringComparer.Ordinal)];
    }

    private static SkillDescriptor? ParseFrontmatter(string skillFilePath)
    {
        var content = File.ReadAllText(skillFilePath);
        var name = FrontmatterFieldRegex("name").Match(content);
        var description = FrontmatterFieldRegex("description").Match(content);

        return name.Success && description.Success
            ? new SkillDescriptor(name.Groups[1].Value.Trim(), description.Groups[1].Value.Trim())
            : null;
    }

    private static Regex FrontmatterFieldRegex(string field) =>
        new($@"^{field}:\s*(.+)$", RegexOptions.Multiline, TimeSpan.FromSeconds(1));
}
