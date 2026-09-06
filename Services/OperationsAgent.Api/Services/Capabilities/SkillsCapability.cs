using Microsoft.Agents.AI;

namespace OperationsAgent.Api.Services.Capabilities;

/// <summary>
/// Agent skills: documented procedures discovered from a directory and loaded on demand. The
/// capability snapshots what was advertised for this request once the agent exists, and reports
/// which of those the model actually loaded.
/// </summary>
internal sealed class SkillsCapability(string? skillsDirectory, ILoggerFactory loggerFactory) : AgentCapability
{
    private AgentSkillsProvider? _skills;
    private IReadOnlyList<SkillDescriptor> _advertisedSkills = [];

    public override bool IsAvailable(DemoStage stage) => stage >= DemoStage.Skills && skillsDirectory is not null;

    public override ValueTask ComposeAsync(AgentComposition composition, CancellationToken cancellationToken)
    {
        var directory = skillsDirectory
            ?? throw new InvalidOperationException("Skills compose only when a skills directory is configured.");

        #region AGENT_SKILLS
        DemoBreakpoints.Pause(DemoSnippets.Skills);

        // Progressive disclosure: skill names/descriptions are advertised in the system prompt; the
        // model loads a full procedure on demand through the load_skill tool. Approval for
        // load_skill is disabled here and returns in the ToolApproval stage.
        _skills = new AgentSkillsProvider(
            directory,
            options: new AgentSkillsProviderOptions { DisableLoadSkillApproval = true },
            loggerFactory: loggerFactory);

        composition.ContextProviders.Add(_skills);
        #endregion

        return ValueTask.CompletedTask;
    }

    public override async ValueTask PrepareAsync(AIAgent agent, CancellationToken cancellationToken)
    {
        if (skillsDirectory is null)
        {
            return;
        }

        // Snapshot the advertised skills before the run - through the SDK's own discovery, so the
        // response reports exactly what the provider would advertise for THIS request even if the
        // presenter edits the files while the model works.
        _advertisedSkills = await SkillCatalog.DescribeAsync(skillsDirectory, agent, loggerFactory, cancellationToken);
    }

    public override void Describe(AgentRunTrace trace, OperationsAgentAnswerParts parts)
    {
        // A skill counts as loaded only when a load_skill call named it exactly (the SDK performs
        // an exact lookup) AND the pipeline executed the call and returned a result to the model.
        var recorder = trace.Recorder;

        parts.Skills.AddRange(_advertisedSkills.Select(skill => new OperationsAgentSkill(
            skill.Name,
            skill.Description,
            recorder is not null && recorder.ToolCalls.Any(call =>
                call.ToolName == OperationsAgentToolNames.LoadSkill
                && SkillCatalog.IsLoadSkillCallFor(call.Arguments, skill.Name)
                && recorder.HasResult(call.CallId)))));
    }

    public override async ValueTask DisposeAsync()
    {
        // The skills provider owns its source pipeline.
        _skills?.Dispose();
        await base.DisposeAsync();
    }
}
