using System.Collections.Concurrent;

namespace DemoControl.Web.Services;

/// <summary>
/// What the switchboard knows about debuggers on the demo services: which IDE holds each
/// service, which service is mid-operation, and what each service last reported. All of it lives
/// for the process, not the page: a reload mid-lecture must not forget who holds the Operations
/// Agent, a second tab must not race a second IDE onto a service, and a service paused at a
/// breakpoint - answering nothing - must still be one click from detaching. The choice of IDE is
/// made per attach, on the row: every available IDE offers its own button.
/// </summary>
internal sealed class DebuggerSelection(IEnumerable<IDebuggerAdapter> adapters)
{
    private readonly ConcurrentDictionary<string, DebuggerHold> _heldBy = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _operations = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DebuggerObservation> _observations = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int?> _identificationAsked = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the adapters in presentation order.
    /// </summary>
    public IReadOnlyList<IDebuggerAdapter> Adapters { get; } = adapters.ToList();

    /// <summary>
    /// Finds an adapter by id.
    /// </summary>
    /// <param name="id">The adapter id.</param>
    /// <returns>The adapter, or <see langword="null"/> when no adapter has that id.</returns>
    public IDebuggerAdapter? Find(string id) =>
        Adapters.FirstOrDefault(adapter => string.Equals(adapter.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// Claims a service for one attach or detach at a time, across every open switchboard tab.
    /// </summary>
    /// <param name="processName">The service process.</param>
    /// <returns><see langword="true"/> when the claim succeeded; <see langword="false"/> when an operation is already in progress.</returns>
    public bool TryBeginOperation(string processName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        return _operations.TryAdd(processName, 0);
    }

    /// <summary>
    /// Releases the claim taken by <see cref="TryBeginOperation"/>.
    /// </summary>
    /// <param name="processName">The service process.</param>
    public void EndOperation(string processName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        _operations.TryRemove(processName, out _);
    }

    /// <summary>
    /// Reports whether an attach or detach is in progress on a service, from any tab.
    /// </summary>
    /// <param name="processName">The service process.</param>
    /// <returns><see langword="true"/> while an operation holds the claim.</returns>
    public bool IsBusy(string processName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        return _operations.ContainsKey(processName);
    }

    /// <summary>
    /// Remembers what a service last reported, so a service that stops answering - paused at a
    /// breakpoint under the debugger - still has a process id and an attachment to act on. A
    /// fresh word from the service that no debugger is on it outranks any record of one here,
    /// except while an operation is mid-flight: then the record is the operation's to keep or
    /// drop, because the poll may simply be ahead of the IDE.
    /// </summary>
    /// <param name="processName">The service process.</param>
    /// <param name="processId">The process id it reported.</param>
    /// <param name="attached">Whether it reported a debugger.</param>
    public void RecordObservation(string processName, int? processId, bool attached)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        _observations[processName] = new DebuggerObservation(processId, attached, DateTimeOffset.UtcNow);

        if (!attached)
        {
            _identificationAsked.TryRemove(processName, out _);

            if (!IsBusy(processName))
            {
                _heldBy.TryRemove(processName, out _);
            }
        }
    }

    /// <summary>
    /// Gets what a service last reported, or <see langword="null"/> when it never answered.
    /// </summary>
    /// <param name="processName">The service process.</param>
    /// <returns>The last observation.</returns>
    public DebuggerObservation? LastObservation(string processName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        return _observations.TryGetValue(processName, out var observation) ? observation : null;
    }

    /// <summary>
    /// Records that the switchboard attached one IDE to a service, so the next click on that row
    /// knows which debugger to detach - and which process id - and never puts a second one on
    /// the same process. Recorded when the IDE confirms, not when a poll does: a service that
    /// pauses at an armed snippet the instant the debugger lands never answers that poll.
    /// </summary>
    /// <param name="processName">The service process.</param>
    /// <param name="adapterId">The adapter that attached.</param>
    /// <param name="processId">The process id the attach targeted, when known.</param>
    public void RecordAttached(string processName, string adapterId, int? processId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterId);

        _heldBy[processName] = new DebuggerHold(adapterId, processId, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Forgets which IDE was on a service, after a detach or once the service reports no debugger.
    /// </summary>
    /// <param name="processName">The service process.</param>
    public void RecordDetached(string processName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        _heldBy.TryRemove(processName, out _);
    }

    /// <summary>
    /// Gets which IDE holds a service, or <see langword="null"/> when none is known to.
    /// </summary>
    /// <param name="processName">The service process.</param>
    /// <returns>The hold.</returns>
    public DebuggerHold? HeldBy(string processName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        return _heldBy.TryGetValue(processName, out var hold) ? hold : null;
    }

    /// <summary>
    /// Asks each IDE whether it holds a service that reports a debugger the switchboard did not
    /// put there - a launch.json attach, an attach from another window, one from before a
    /// restart - and records the first that says yes, so the row names the right debugger to
    /// detach. Asked once per process id, not on every poll: an IDE that cannot tell says so
    /// every time, and a helper process per poll would be the cost.
    /// </summary>
    /// <param name="processName">The service process.</param>
    /// <param name="processId">The process id the service reported.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task that completes when the hold is recorded or every adapter has answered.</returns>
    public async Task IdentifyHolderAsync(string processName, int? processId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        if (HeldBy(processName) is not null
            || (_identificationAsked.TryGetValue(processName, out var asked) && asked == processId))
        {
            return;
        }

        _identificationAsked[processName] = processId;
        var target = new DebuggerTarget(processName, processId);

        foreach (var adapter in Adapters)
        {
            if (await adapter.IsAttachedAsync(target, cancellationToken) == true)
            {
                RecordAttached(processName, adapter.Id, processId);
                return;
            }
        }
    }

    /// <summary>
    /// Describes who holds a service's debugger, from what the service reports and what this
    /// switchboard knows, without changing anything: a stale tab rendering an old snapshot must
    /// not erase what a fresh one recorded. The service's report wins: a debugger it does not
    /// see is not attached, whatever was recorded here.
    /// </summary>
    /// <param name="processName">The service process.</param>
    /// <param name="serviceReportsAttached">Whether the service itself reports an attached debugger.</param>
    /// <returns>The attachment as the row should present it.</returns>
    public DebuggerAttachment Describe(string processName, bool serviceReportsAttached)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        if (!serviceReportsAttached)
        {
            return new DebuggerAttachment(DebuggerAttachmentKind.NotAttached, null);
        }

        return HeldBy(processName) is { } hold && Find(hold.AdapterId) is { } holder
            ? new DebuggerAttachment(DebuggerAttachmentKind.Attached, holder)
            : new DebuggerAttachment(DebuggerAttachmentKind.AttachedOutside, null);
    }
}

/// <summary>
/// Which IDE holds a service, and on which process.
/// </summary>
/// <param name="AdapterId">The adapter that attached, or was identified as holding the process.</param>
/// <param name="ProcessId">The process id, or <see langword="null"/>.</param>
/// <param name="Since">When the hold was recorded.</param>
internal sealed record DebuggerHold(string AdapterId, int? ProcessId, DateTimeOffset Since);

/// <summary>
/// What a service last reported about itself.
/// </summary>
/// <param name="ProcessId">The process id it reported, or <see langword="null"/>.</param>
/// <param name="Attached">Whether it reported a debugger.</param>
/// <param name="At">When it said so.</param>
internal sealed record DebuggerObservation(int? ProcessId, bool Attached, DateTimeOffset At);

/// <summary>
/// Who holds a service's debugger, and therefore which buttons its row shows.
/// </summary>
/// <param name="Kind">The attachment state.</param>
/// <param name="Holder">The IDE holding the service when known; <see langword="null"/> when nobody or nobody identifiable.</param>
internal sealed record DebuggerAttachment(DebuggerAttachmentKind Kind, IDebuggerAdapter? Holder);

/// <summary>
/// The attachment states a service row distinguishes.
/// </summary>
internal enum DebuggerAttachmentKind
{
    /// <summary>
    /// The service reports no debugger: the row offers Attach with every available IDE.
    /// </summary>
    NotAttached,

    /// <summary>
    /// A known IDE holds the service: the row offers Detach with that IDE, and no second attach.
    /// </summary>
    Attached,

    /// <summary>
    /// A debugger nobody here can name is on the service - a launch.json attach, a Visual Studio
    /// window that could not be asked, or a debugger from before a switchboard restart. The row
    /// says so and offers Detach through an available IDE as a best effort.
    /// </summary>
    AttachedOutside
}
