using System.ComponentModel;
using System.Text;

namespace OperationsAgent.Hosted;

/// <summary>
/// Progressive disclosure over a skills directory, using an ordinary function tool instead of
/// <c>AgentSkillsProvider</c>.
/// </summary>
/// <remarks>
/// <para>
/// This was born of a wrong diagnosis and is kept because it is independently useful. The HTTP 400
/// <c>invalid_payload</c> failures once blamed on <c>AgentSkillsProvider</c> were never about
/// skills: the hosted runtime replays reasoning items the service rejects on any tool-calling
/// turn, and <c>ReasoningReplaySanitizingChatClient</c> is the actual fix (the full story is in
/// docs/product-status/hosted-agent.md). The provider path works and remains the default
/// (SKILLS_MODE=provider); this tool path needs no files in the image and no SKILLS_DIRECTORY,
/// which is its own reason to exist.
/// </para>
/// <para>
/// What it replaces is smaller than it looks. Progressive disclosure is a pattern, not an API: the
/// names and one-line descriptions are advertised up front so the model can tell what exists, and the
/// full procedure is fetched by name only when the model decides it wants one. Both halves are here.
/// The difference is that the body arrives through a normal function call, which the hosted runtime
/// carries without complaint.
/// </para>
/// <para>
/// Deliberately read-only. The real provider also exposes tools that read skill resources and run
/// skill scripts; nothing in this demo's skills uses either, and adding a script runner to a hosted
/// agent is a much larger decision than restoring a procedure lookup.
/// </para>
/// </remarks>
internal sealed class SkillsAsTools
{
    private readonly Dictionary<string, string> _skills;

    private SkillsAsTools(Dictionary<string, string> skills) => _skills = skills;

    /// <summary>The advertised catalogue, appended to the agent's instructions.</summary>
    public string Catalogue { get; private init; } = string.Empty;

    /// <summary>Skill names found, in advertised order.</summary>
    public IReadOnlyCollection<string> Names => _skills.Keys;

    /// <summary>
    /// Reads every <c>SKILL.md</c> under <paramref name="directory"/>, or returns <see langword="null"/>
    /// when the directory holds none.
    /// </summary>
    public static SkillsAsTools? Load(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        Dictionary<string, string> skills = new(StringComparer.OrdinalIgnoreCase);
        StringBuilder catalogue = new();

        foreach (var skillDirectory in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
        {
            var path = Path.Combine(skillDirectory, "SKILL.md");
            if (!File.Exists(path))
            {
                continue;
            }

            var content = File.ReadAllText(path);
            var name = ReadFrontmatterValue(content, "name") ?? Path.GetFileName(skillDirectory);
            var description = ReadFrontmatterValue(content, "description") ?? "No description provided.";

            skills[name] = content;
            catalogue.Append("- ").Append(name).Append(": ").AppendLine(description);
        }

        if (skills.Count == 0)
        {
            return null;
        }

        return new SkillsAsTools(skills)
        {
            // The advertise half of progressive disclosure: enough to choose by, never the body.
            Catalogue = $"""

                ## Available skills

                These are documented procedures. When one applies to the request, call
                `{ToolName}` with its name to read it in full, and then follow it.

                {catalogue.ToString().TrimEnd()}
                """
        };
    }

    /// <summary>The tool name, matching the real provider's so transcripts read the same.</summary>
    public const string ToolName = "load_skill";

    [Description("Loads the full text of a documented procedure by name, so it can be followed step by step.")]
    public string LoadSkill(
        [Description("The skill name, exactly as advertised in the available skills list.")] string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // Named, not guessed. Returning "nothing found" for a typo would let the model proceed as if
        // no procedure existed, which is the failure mode the skill's own triage table punishes.
        return _skills.TryGetValue(name.Trim(), out var content)
            ? content
            : $"No skill named '{name}'. Available skills: {string.Join(", ", _skills.Keys)}.";
    }


    /// <summary>
    /// Reads one value out of the YAML frontmatter block, without taking a YAML dependency for two
    /// fields. Anything more structured than <c>key: value</c> belongs in the skill body.
    /// </summary>
    private static string? ReadFrontmatterValue(string content, string key)
    {
        var span = content.AsSpan();
        if (!span.StartsWith("---"))
        {
            return null;
        }

        var end = content.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        foreach (var line in content[..end].Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith($"{key}:", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[(key.Length + 1)..].Trim();
            }
        }

        return null;
    }
}
