namespace OperationsAgent.Api.Services;

/// <summary>
/// Loads the declarative YAML expression of the remediation workflow from the repository. The
/// YAML is displayed beside the generated diagram for comparison - the executing graph is always
/// the code-built one.
/// </summary>
public static class RemediationWorkflowYaml
{
    /// <summary>
    /// The repository-relative path of the declarative workflow file.
    /// </summary>
    public const string RelativePath = "workflows/restore-remediation.yaml";

    /// <summary>
    /// Gets the declarative YAML content, or an explanatory placeholder when the repository
    /// file cannot be located (for example when running from a published output).
    /// </summary>
    public static string Content { get; } = Load();

    private static string Load()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Caesarea.slnx")))
            {
                var yamlPath = Path.Combine(directory.FullName, RelativePath);
                return File.Exists(yamlPath)
                    ? File.ReadAllText(yamlPath)
                    : $"# {RelativePath} was not found in the repository.";
            }

            directory = directory.Parent;
        }

        return $"# {RelativePath} is available when running from the repository.";
    }
}
