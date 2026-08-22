using Caesarea.Contracts;

namespace CommandCenter.Api.Services;

public sealed class IncidentModule
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IncidentRecord> _incidents = [];

    public IncidentRecord? Get(string incidentId)
    {
        lock (_gate)
        {
            return _incidents.GetValueOrDefault(incidentId);
        }
    }

    public IncidentRecord? GetOpenIncidentForAsset(string assetId)
    {
        lock (_gate)
        {
            return _incidents.Values.FirstOrDefault(incident =>
                incident.Status == IncidentStatus.Open
                && string.Equals(incident.AssetId, assetId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _incidents.Clear();
        }
    }

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

public sealed class ActivityTimelineModule
{
    private readonly object _gate = new();
    private readonly List<ActivityRecord> _activity = [];

    public IReadOnlyList<ActivityRecord> GetRecent(int limit)
    {
        lock (_gate)
        {
            return _activity
                .OrderByDescending(record => record.OccurredAt)
                .Take(Math.Max(1, limit))
                .ToArray();
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _activity.Clear();
        }
    }

    public void Add(ActivityRecord record)
    {
        lock (_gate)
        {
            _activity.Add(record);
        }
    }

    public void Replace(IEnumerable<ActivityRecord> records)
    {
        lock (_gate)
        {
            _activity.Clear();
            _activity.AddRange(records);
        }
    }
}

public sealed class ScenarioContextModule(TimeProvider timeProvider)
{
    private readonly object _gate = new();
    private ScenarioStatus _currentScenario = new(
        ScenarioId.NormalOperation,
        "Normal Operation",
        "The deterministic baseline: daylight, schedule off, no incident, and controller healthy.",
        timeProvider.GetUtcNow(),
        "startup");

    public ScenarioStatus GetCurrent()
    {
        lock (_gate)
        {
            return _currentScenario;
        }
    }

    public ScenarioStatus Reset(string correlationId)
    {
        lock (_gate)
        {
            _currentScenario = new ScenarioStatus(
                ScenarioId.NormalOperation,
                "Normal Operation",
                "The deterministic baseline: daylight, schedule off, no incident, and controller healthy.",
                timeProvider.GetUtcNow(),
                correlationId);

            return _currentScenario;
        }
    }

    public ScenarioStatus SetCurrent(ScenarioStatus scenario)
    {
        lock (_gate)
        {
            _currentScenario = scenario;
            return _currentScenario;
        }
    }
}

public sealed class SpatialContextModule
{
    public void EnsureAsset(string assetId)
    {
        if (!string.Equals(assetId, DemoAssets.StreetlightAssetId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"The Stage 0 Command Center only exposes asset {DemoAssets.StreetlightAssetId}.", nameof(assetId));
        }
    }

    public string GetMapLabel(string assetId)
    {
        EnsureAsset(assetId);
        return $"{DemoAssets.NorthPromenadeArea} / {DemoAssets.StreetlightAssetId}";
    }
}
