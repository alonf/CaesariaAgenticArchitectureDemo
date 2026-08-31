using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Agents.AI;

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
/// the YAML frontmatter block and applies the SDK's frontmatter validation, so a skill appears
/// here only if the SDK would advertise it too. Files are read fresh on every call, so presenter
/// edits take effect on the next request; unreadable or invalid files are skipped with a warning.
/// </summary>
internal static partial class SkillCatalog
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
    /// Reads the frontmatter of every SKILL.md under the supplied directory, matching the SDK's
    /// discovery depth (the directory itself plus up to two levels of skill folders).
    /// </summary>
    /// <param name="skillsDirectory">The resolved skills directory.</param>
    /// <param name="logger">The logger used to report skipped files.</param>
    /// <returns>The discovered skills, ordered by name.</returns>
    public static IReadOnlyList<SkillDescriptor> Describe(string? skillsDirectory, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        if (skillsDirectory is null || !Directory.Exists(skillsDirectory))
        {
            return [];
        }

        List<SkillDescriptor> skills = [];

        foreach (var skillFilePath in EnumerateSkillFiles(skillsDirectory))
        {
            try
            {
                if (ParseFrontmatter(File.ReadAllText(skillFilePath)) is { } descriptor)
                {
                    skills.Add(descriptor);
                }
                else
                {
                    SkillCatalogLog.SkillFileInvalid(logger, skillFilePath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // ArgumentException: the SDK frontmatter validation rejected the name/description.
                SkillCatalogLog.SkillFileSkipped(logger, skillFilePath, exception);
            }
        }

        return [.. skills.OrderBy(skill => skill.Name, StringComparer.Ordinal)];
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

    private static IEnumerable<string> EnumerateSkillFiles(string skillsDirectory) =>
        Directory
            .EnumerateFiles(skillsDirectory, "SKILL.md", SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(skillsDirectory, path)
                .Count(character => character == Path.DirectorySeparatorChar) <= 2);

    private static SkillDescriptor? ParseFrontmatter(string content)
    {
        var frontmatterBlock = FrontmatterBlockRegex().Match(content);

        if (!frontmatterBlock.Success)
        {
            return null;
        }

        var name = FieldRegex("name").Match(frontmatterBlock.Groups[1].Value);
        var description = FieldRegex("description").Match(frontmatterBlock.Groups[1].Value);

        if (!name.Success || !description.Success)
        {
            return null;
        }

        // Apply the SDK's own validation, so the catalog never advertises a skill the
        // AgentSkillsProvider would reject; an invalid file throws ArgumentException here.
        var frontmatter = new AgentSkillFrontmatter(name.Groups[1].Value.Trim(), description.Groups[1].Value.Trim());
        return new SkillDescriptor(frontmatter.Name, frontmatter.Description);
    }

    private static Regex FieldRegex(string field) =>
        new($@"^{field}:\s*(.+)$", RegexOptions.Multiline, TimeSpan.FromSeconds(1));

    [GeneratedRegex(@"\A---\s*\r?\n(.*?)\r?\n---\s*\r?\n", RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FrontmatterBlockRegex();
}

internal static partial class SkillCatalogLog
{
    [LoggerMessage(
        EventId = 2611,
        Level = LogLevel.Warning,
        Message = "Skill file {SkillFilePath} has no valid frontmatter and was not advertised.")]
    internal static partial void SkillFileInvalid(ILogger logger, string skillFilePath);

    [LoggerMessage(
        EventId = 2612,
        Level = LogLevel.Warning,
        Message = "Skill file {SkillFilePath} was skipped.")]
    internal static partial void SkillFileSkipped(ILogger logger, string skillFilePath, Exception exception);
}
