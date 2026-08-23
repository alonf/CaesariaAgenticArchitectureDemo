namespace CommandCenter.Api.Services;

/// <summary>
/// Stores the current Command Center incidents for the deterministic Stage 0 experience.
/// </summary>
public sealed class IncidentModule
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IncidentRecord> _incidents = [];

    /// <summary>
    /// Gets an incident by identifier.
    /// </summary>
    /// <param name="incidentId">The incident identifier to resolve.</param>
    /// <returns>The matching incident, or <see langword="null"/> when it does not exist.</returns>
    public IncidentRecord? Get(string incidentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(incidentId);

        lock (_gate)
        {
            return _incidents.GetValueOrDefault(incidentId);
        }
    }

    /// <summary>
    /// Gets the first open incident associated with the supplied asset.
    /// </summary>
    /// <param name="assetId">The asset identifier to search.</param>
    /// <returns>The first open incident for the asset, or <see langword="null"/> when none exists.</returns>
    public IncidentRecord? GetOpenIncidentForAsset(string assetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        lock (_gate)
        {
            return _incidents.Values.FirstOrDefault(incident =>
                incident.Status == IncidentStatus.Open
                && string.Equals(incident.AssetId, assetId, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Removes all incidents from the in-memory Command Center store.
    /// </summary>
    public void Reset()
    {
        lock (_gate)
        {
            _incidents.Clear();
        }
    }

    /// <summary>
    /// Replaces the current incident set with the supplied incident, if any.
    /// </summary>
    /// <param name="incident">The incident to store as the current open incident.</param>
    public void Replace(IncidentRecord? incident)
    {
        lock (_gate)
        {
            _incidents.Clear();

            if (incident is not null)
            {
                _incidents[incident.Id] = incident;
            }
        }
    }
}

/// <summary>
/// Stores the local Command Center activity timeline used to enrich authoritative Energy Hub activity.
/// </summary>
public sealed class ActivityTimelineModule
{
    private readonly object _gate = new();
    private readonly List<ActivityRecord> _activity = [];

    /// <summary>
    /// Gets the most recent local activity records.
    /// </summary>
    /// <param name="limit">The maximum number of records to return.</param>
    /// <returns>The recent activity ordered from newest to oldest.</returns>
    public IReadOnlyList<ActivityRecord> GetRecent(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        lock (_gate)
        {
            return _activity
                .OrderByDescending(record => record.OccurredAt)
                .Take(limit)
                .ToArray();
        }
    }

    /// <summary>
    /// Removes all local activity entries.
    /// </summary>
    public void Reset()
    {
        lock (_gate)
        {
            _activity.Clear();
        }
    }

    /// <summary>
    /// Adds a local Command Center activity entry.
    /// </summary>
    /// <param name="record">The activity record to store.</param>
    public void Add(ActivityRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_gate)
        {
            _activity.Add(record);
        }
    }

    /// <summary>
    /// Replaces the local activity timeline with the supplied records.
    /// </summary>
    /// <param name="records">The records to store.</param>
    public void Replace(IEnumerable<ActivityRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        lock (_gate)
        {
            _activity.Clear();
            _activity.AddRange(records);
        }
    }
}

/// <summary>
/// Stores the current synthetic customer report projected into the Command Center.
/// </summary>
public sealed class CustomerReportModule
{
    private readonly object _gate = new();
    private CustomerReportRecord? _current;

    /// <summary>
    /// Gets the current customer report.
    /// </summary>
    /// <returns>The current report, or <see langword="null"/> when no report has arrived.</returns>
    public CustomerReportRecord? GetCurrent()
    {
        lock (_gate)
        {
            return _current;
        }
    }

    /// <summary>
    /// Removes the current customer report.
    /// </summary>
    public void Reset()
    {
        lock (_gate)
        {
            _current = null;
        }
    }

    /// <summary>
    /// Replaces the current customer report.
    /// </summary>
    /// <param name="report">The report to store, or <see langword="null"/> to clear it.</param>
    public void Replace(CustomerReportRecord? report)
    {
        lock (_gate)
        {
            _current = report;
        }
    }
}

/// <summary>
/// Tracks the scenario currently projected by the Command Center.
/// </summary>
/// <param name="timeProvider">The clock used to stamp scenario state changes.</param>
public sealed class ScenarioContextModule(TimeProvider timeProvider)
{
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private ScenarioStatus _currentScenario = CreateNormalOperationScenario(timeProvider.GetUtcNow(), "startup");

    /// <summary>
    /// Gets the current scenario status.
    /// </summary>
    /// <returns>The current scenario.</returns>
    public ScenarioStatus GetCurrent()
    {
        lock (_gate)
        {
            return _currentScenario;
        }
    }

    /// <summary>
    /// Resets the scenario state back to the deterministic normal-operation baseline.
    /// </summary>
    /// <param name="correlationId">The correlation identifier spanning the reset.</param>
    /// <returns>The reset scenario status.</returns>
    public ScenarioStatus Reset(string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        lock (_gate)
        {
            _currentScenario = CreateNormalOperationScenario(_timeProvider.GetUtcNow(), correlationId);
            return _currentScenario;
        }
    }

    /// <summary>
    /// Replaces the current scenario with the supplied value.
    /// </summary>
    /// <param name="scenario">The new current scenario.</param>
    /// <returns>The stored scenario.</returns>
    public ScenarioStatus SetCurrent(ScenarioStatus scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        lock (_gate)
        {
            _currentScenario = scenario;
            return _currentScenario;
        }
    }

    private static ScenarioStatus CreateNormalOperationScenario(DateTimeOffset appliedAt, string correlationId) =>
        new(
            ScenarioId.NormalOperation,
            "Normal Operation",
            "The deterministic baseline: daylight, schedule off, no incident, and controller healthy.",
            appliedAt,
            correlationId);
}

/// <summary>
/// Tracks the demo stage currently propagated from the presenter switchboard to the Command Center.
/// </summary>
/// <param name="timeProvider">The clock used to stamp stage changes.</param>
public sealed class StageContextModule(TimeProvider timeProvider)
{
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private DemoStageStatus _currentStage = CreateDeterministicStage(timeProvider.GetUtcNow(), "startup");

    /// <summary>
    /// Gets the current demo stage.
    /// </summary>
    /// <returns>The current stage.</returns>
    public DemoStageStatus GetCurrent()
    {
        lock (_gate)
        {
            return _currentStage;
        }
    }

    /// <summary>
    /// Replaces the current demo stage with the supplied value.
    /// </summary>
    /// <param name="stage">The new current stage.</param>
    /// <returns>The stored stage.</returns>
    public DemoStageStatus SetCurrent(DemoStageStatus stage)
    {
        ArgumentNullException.ThrowIfNull(stage);

        lock (_gate)
        {
            _currentStage = stage;
            return _currentStage;
        }
    }

    private static DemoStageStatus CreateDeterministicStage(DateTimeOffset appliedAt, string correlationId) =>
        new(
            DemoStage.Deterministic,
            "Deterministic",
            "Only the deterministic Stage 0 capabilities are enabled.",
            ["Deterministic scenarios", "Manual operator actions"],
            appliedAt,
            correlationId);
}

/// <summary>
/// Provides the small spatial context projected in the Stage 0 Command Center map.
/// </summary>
public sealed class SpatialContextModule
{
    /// <summary>
    /// Verifies that the supplied asset identifier is part of the Stage 0 Command Center experience.
    /// </summary>
    /// <param name="assetId">The asset identifier to validate.</param>
    public void EnsureAsset(string assetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);

        if (!string.Equals(assetId, DemoAssets.StreetlightAssetId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"The Stage 0 Command Center only exposes asset {DemoAssets.StreetlightAssetId}.", nameof(assetId));
        }
    }

    /// <summary>
    /// Gets the stable map label used in the Command Center web UI.
    /// </summary>
    /// <param name="assetId">The asset identifier to describe.</param>
    /// <returns>The map label shown in the UI.</returns>
    public string GetMapLabel(string assetId)
    {
        EnsureAsset(assetId);
        return $"{DemoAssets.NorthPromenadeArea} / {DemoAssets.StreetlightAssetId}";
    }
}
