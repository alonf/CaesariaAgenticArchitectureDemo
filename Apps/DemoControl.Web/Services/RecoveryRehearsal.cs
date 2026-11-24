using System.Text.Json;
using DemoControl.Contracts;
using DurableOperations.Contracts;

namespace DemoControl.Web.Services;

/// <summary>Retains the rehearsal independently of browser circuits; domain truth remains in Functions and the Hub.</summary>
internal sealed partial class RecoveryRehearsal : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = CaesareaJsonDefaults.CreateSerializerOptions();
    private readonly IRecoveryLocalRuntime _runtime;
    private readonly IRecoveryRehearsalApi _api;
    private readonly RecoveryLocalSettings _settings;
    private readonly ILogger<RecoveryRehearsal> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private RecoveryRehearsalState _state = new();

    public RecoveryRehearsal(IRecoveryLocalRuntime runtime, IRecoveryRehearsalApi api, RecoveryLocalSettings settings, ILogger<RecoveryRehearsal> logger)
    {
        _runtime = runtime;
        _api = api;
        _settings = settings;
        _logger = logger;
        if (settings.Enabled && File.Exists(StatePath))
        {
            try
            {
                _state = (JsonSerializer.Deserialize<RecoveryRehearsalState>(File.ReadAllText(StatePath), JsonOptions) ?? new())
                    with
                { Busy = false, Message = "Retained rehearsal loaded. Refresh status to reconnect to its worker." };
            }
            catch (JsonException)
            {
                _state = new() { Busy = true, Error = true, Message = "Retained rehearsal metadata could not be read. Inspect the local evidence before starting another repair." };
            }
        }
    }

    public RecoveryRehearsalState State => Volatile.Read(ref _state);
    public bool Enabled => _settings.Enabled;
    public int? WorkerProcessId => _runtime.ProcessId;
    private string StatePath => Path.Combine(_settings.EvidenceDirectory, "rehearsal.json");

    public Task PrepareAsync() => ActAsync(async token =>
    {
        if (State.SessionId is not null && (!State.Terminal || State.Result?.CommandExecuted is null))
        {
            throw new InvalidOperationException("An existing repair is pending or uncertain. Refresh its status; do not start another repair.");
        }

        Save(new() { Busy = true, Message = "Checking scheduler, storage and Functions worker..." });
        await _runtime.EnsureRunningAsync(token);
        Record($"Functions ready (owned launcher {_runtime.ProcessId}); scheduler remains separate.");
        await _api.PrepareCityAsync(token);
        Record("Lights On Reported by a Client prepared; Workflow capability available.");
        Save(State with { Prepared = true });
        Record("Foundation ready. Open Command Center and start the durable recovery conversation there.");
    });

    public Task BeginAsync() => ActAsync(async token =>
    {
        if (!State.Prepared || State.SessionId is not null || _runtime.ProcessId is null)
        {
            throw new InvalidOperationException("Prepare Durable recovery in Demo Director before starting a new conversation in Command Center.");
        }

        var sessionId = await _api.CreateSessionAsync(token);
        Save(State with { SessionId = sessionId, Prepared = false });
        Record("Session identity retained before sending the repair request.");
        var reply = await _api.RunAsync(sessionId, "Investigate L-417 and start supervised recovery. Do not approve it.", token);
        Save(State with { FirstReply = reply.Message });
        await WaitForApprovalAsync(token);
        Record("Awaiting approval: a real unexpired supervisor review is available. Ready for worker crash.");
    });

    public Task CrashAndRecoverAsync() => ActAsync(async token =>
    {
        await RefreshCoreAsync(token);
        var review = RequireReview();
        // This exact review is retained even if the restart or follow-up request fails.
        Save(State with { OriginalReview = review, Recovered = false, WorkerRestarted = false });
        await _runtime.CrashAsync(token);
        Record("Worker stopped: forced process termination confirmed; scheduler and city were not stopped.");
        await _runtime.EnsureRunningAsync(token);
        Save(State with { WorkerRestarted = true });
        Record($"Worker restarted (owned launcher {_runtime.ProcessId}). Continue the same conversation in Command Center.");
    });

    public Task ReconnectAsync() => ActAsync(async token =>
    {
        await _runtime.EnsureRunningAsync(token);
        await RefreshCoreAsync(token);
        if (State.OriginalReview is not null) { Save(State with { WorkerRestarted = true }); }
        Record("Authoritative status refreshed; no agent turn or repair request was resent.");
    });

    public Task ResumeAsync() => ActAsync(async token =>
    {
        await RefreshCoreAsync(token);
        if ((State with { Busy = false }).ContinueBlockedReason is { } reason)
        {
            throw new InvalidOperationException(reason);
        }

        if (_runtime.ProcessId is null)
        {
            throw new InvalidOperationException("Reconnect the worker in Demo Director before continuing this conversation.");
        }

        await ResumeCoreAsync(token);
    });

    public Task DecideAsync(RecoveryReview displayedReview, bool approved) => ActAsync(async token =>
    {
        await RefreshCoreAsync(token);
        if (State.DecisionQueued || displayedReview != RequireReview())
        {
            throw new InvalidOperationException("The displayed review is no longer current, or a decision is already queued. Refresh status.");
        }

        // Mark before dispatch: a lost HTTP response cannot lead to automatic resubmission.
        Save(State with { DecisionQueued = true });
        await _api.DecideAsync(displayedReview, approved, token);
        Record(approved ? "Supervisor approval queued; waiting for the actual business outcome." : "Supervisor decline queued; waiting for the actual business outcome.");
        for (var attempt = 0; attempt < 60; attempt++)
        {
            await RefreshCoreAsync(token);
            if (State.Terminal)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), token);
        }

        throw new TimeoutException("Decision is queued but no terminal outcome is confirmed yet. Status polling continues; do not resend.");
    });

    private async Task ResumeCoreAsync(CancellationToken token)
    {
        var reply = await _api.RunAsync(State.SessionId!, "What happened to the repair you started? Check its status.", token);
        Save(State with { ResumedReply = reply.Message });
        await RefreshCoreAsync(token);
        if (State.OriginalReview != RequireReview() || reply.SessionId != State.SessionId)
        {
            throw new InvalidOperationException("The original session and pending review were not both confirmed unchanged. Do not claim recovery succeeded.");
        }

        Save(State with { Recovered = true });
        Record("Same session resumed without an operation ID in the prompt; original review and deadline are unchanged.");
    }

    private RecoveryReview RequireReview()
    {
        if (State.Operation is not { RuntimeStatus: "Running", Progress.State: "AwaitingApproval", Progress.Review: { } review }
            || review.InstanceId != State.OperationId || review.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("A real, unexpired AwaitingApproval review is required. Scheduling acceptance alone is insufficient.");
        }

        return review;
    }

    private async Task WaitForApprovalAsync(CancellationToken token)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            await RefreshCoreAsync(token);
            if (State.Operation?.Progress?.State == "AwaitingApproval")
            {
                RequireReview();
                return;
            }

            if (State.Terminal)
            {
                throw new InvalidOperationException("The operation ended before approval. Inspect its outcome.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), token);
        }

        throw new TimeoutException("No pending review was confirmed. Refresh the retained session; no automatic repair retry will be made.");
    }

    private async Task RefreshCoreAsync(CancellationToken token)
    {
        if (State.SessionId is null)
        {
            return;
        }

        if (State.OperationId is null)
        {
            var operations = await _api.FindOperationsAsync(State.SessionId, token);
            if (operations.Count == 0)
            {
                return;
            }

            if (operations.Count != 1)
            {
                throw new InvalidOperationException("Multiple operations belong to this session. Inspect the scheduler before choosing a review.");
            }

            Save(State with { OperationId = operations[0] });
        }

        var operation = await _api.GetAsync(State.OperationId!, token);
        // Scheduler termination/failure output is not a RecoveryResult (and can be plain text).
        // Always publish the authoritative runtime status, even when business output is unavailable.
        RecoveryResult? result = null;
        if (operation.RuntimeStatus == "Completed" && !string.IsNullOrWhiteSpace(operation.Outcome))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<RecoveryResult>(operation.Outcome, JsonOptions);
                if (!string.IsNullOrWhiteSpace(parsed?.State) && !string.IsNullOrWhiteSpace(parsed.Summary))
                {
                    result = parsed;
                }
            }
            catch (JsonException) { /* Preserve an unknown business outcome, never the stale pending status. */ }
        }
        var changed = operation.RuntimeStatus != State.Operation?.RuntimeStatus || operation.Progress?.State != State.Operation?.Progress?.State;
        Save(State with { Operation = operation, Result = result });
        if (changed)
        {
            Record($"{operation.RuntimeStatus} / {result?.State ?? operation.Progress?.State ?? "Scheduled"}: {result?.Summary ?? operation.Progress?.Summary}");
        }

        if (State.Terminal)
        {
            var hub = await _api.ReadHubAsync(token);
            Save(State with { Hub = hub, HubObservedAt = DateTimeOffset.UtcNow });
        }
    }

    private async Task ActAsync(Func<CancellationToken, Task> action)
    {
        if (!Enabled || !await _gate.WaitAsync(0))
        {
            return;
        }

        try
        {
            Save(State with { Busy = true, Error = false });
            using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(4));
            await action(budget.Token);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            RehearsalFailed(_logger, State.OperationId ?? State.SessionId ?? "not-started", exception.GetType().Name, exception);
            Save(State with { Error = true, Message = exception.Message });
        }
        finally
        {
            Save(State with { Busy = false });
            _gate.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!Enabled || State.SessionId is null || State.Terminal && State.Hub is not null || !await _gate.WaitAsync(0, stoppingToken))
            {
                continue;
            }

            try
            {
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                budget.CancelAfter(TimeSpan.FromSeconds(10));
                await RefreshCoreAsync(budget.Token);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                Save(State with { Error = true, Message = $"Status unavailable: {exception.Message} The retained operation is not considered failed; polling continues." });
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    private void Record(string summary)
    {
        RehearsalProgress(_logger, State.OperationId ?? State.SessionId ?? "not-started", summary);
        Save(State with { Message = summary, Error = false, Events = [.. State.Events, new(DateTimeOffset.UtcNow, summary)] });
    }

    private void Save(RecoveryRehearsalState state)
    {
        Directory.CreateDirectory(_settings.EvidenceDirectory);
        var snapshot = JsonSerializer.Serialize(state, JsonOptions);
        // Windows file watchers can briefly hold the destination without delete sharing.
        // Retry only this atomic replacement; never retry an agent turn or approval here.
        for (var attempt = 0; attempt <= 10; attempt++)
        {
            try
            {
                File.WriteAllText(StatePath + ".tmp", snapshot);
                File.Move(StatePath + ".tmp", StatePath, overwrite: true);
                break;
            }
            catch (Exception exception) when (attempt < 10 && exception is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
        Volatile.Write(ref _state, state);
    }

    public override void Dispose()
    {
        _gate.Dispose();
        base.Dispose();
    }

    [LoggerMessage(2510, LogLevel.Information, "Recovery rehearsal: {Summary} CorrelationId: {OperationId}")]
    private static partial void RehearsalProgress(ILogger logger, string operationId, string summary);
    [LoggerMessage(2511, LogLevel.Warning, "Recovery rehearsal action failed ({FailureType}). CorrelationId: {OperationId}")]
    private static partial void RehearsalFailed(ILogger logger, string operationId, string failureType, Exception exception);
}
