using DurableOperations.Contracts;
using Energy.Contracts;

namespace DemoControl.Contracts;

/// <summary>A timestamped observation from the local rehearsal controller.</summary>
public sealed record RecoveryRehearsalEvent(DateTimeOffset At, string Summary);

/// <summary>Local presenter coordination state; Functions and the Hub remain authoritative.</summary>
public sealed record RecoveryRehearsalState
{
    /// <summary>Gets whether the Director prepared the foundation for a new main-app conversation.</summary>
    public bool Prepared { get; init; }
    /// <summary>Gets the retained rehearsal SessionId.</summary>
    public string? SessionId { get; init; }
    /// <summary>Gets the retained rehearsal OperationId.</summary>
    public string? OperationId { get; init; }
    /// <summary>Gets the retained rehearsal FirstReply.</summary>
    public string? FirstReply { get; init; }
    /// <summary>Gets the retained rehearsal ResumedReply.</summary>
    public string? ResumedReply { get; init; }
    /// <summary>Gets the retained rehearsal Operation.</summary>
    public RecoveryOperationStatus? Operation { get; init; }
    /// <summary>Gets the retained rehearsal Result.</summary>
    public RecoveryResult? Result { get; init; }
    /// <summary>Gets the retained rehearsal Hub.</summary>
    public EnergyOperationalTwin? Hub { get; init; }
    /// <summary>Gets the retained rehearsal HubObservedAt.</summary>
    public DateTimeOffset? HubObservedAt { get; init; }
    /// <summary>Gets the retained rehearsal OriginalReview.</summary>
    public RecoveryReview? OriginalReview { get; init; }
    /// <summary>Gets the retained rehearsal Recovered.</summary>
    public bool Recovered { get; init; }
    /// <summary>Gets whether the Director confirmed the worker is back, before a main-app follow-up.</summary>
    public bool WorkerRestarted { get; init; }
    /// <summary>Gets the retained rehearsal DecisionQueued.</summary>
    public bool DecisionQueued { get; init; }
    /// <summary>Gets the retained rehearsal Busy.</summary>
    public bool Busy { get; init; }
    /// <summary>Gets the retained rehearsal Message.</summary>
    public string Message { get; init; } = "Ready to prepare the local rehearsal.";
    /// <summary>Gets the retained rehearsal Error.</summary>
    public bool Error { get; init; }
    /// <summary>Gets the retained rehearsal Events.</summary>
    public IReadOnlyList<RecoveryRehearsalEvent> Events { get; init; } = [];
    /// <summary>Gets the retained rehearsal Terminal.</summary>
    public bool Terminal => Operation?.RuntimeStatus is "Completed" or "Failed" or "Terminated" or "Canceled";
    /// <summary>Gets why the restart demonstration cannot continue, or null when ready.</summary>
    public string? ContinueBlockedReason => this switch
    {
        { Operation.RuntimeStatus: "Terminated" } => "The scheduler reports this operation as terminated. Restarting the worker cannot resume a terminated operation. Inspect the Hub before preparing a new rehearsal.",
        { Terminal: true } => "This operation has ended. Review its outcome before preparing a new rehearsal.",
        { Busy: true } => "Wait for the current action to finish.",
        { Recovered: true } => "The conversation has already resumed. Review the approval below.",
        { WorkerRestarted: false } or { OriginalReview: null } => "Use Crash worker and restart in Demo Director, then return here.",
        { CanDecide: false } => "An unexpired AwaitingApproval review is required. Refresh status in Demo Director.",
        _ => null
    };
    /// <summary>Gets the retained rehearsal BlocksCityChanges.</summary>
    public bool BlocksCityChanges => Busy || SessionId is not null && (!Terminal || Result?.CommandExecuted is null);
    /// <summary>Gets the retained rehearsal CanDecide.</summary>
    public bool CanDecide => !Busy && !DecisionQueued && Operation is { RuntimeStatus: "Running", Progress.State: "AwaitingApproval", Progress.Review: { } review }
        && review.InstanceId == OperationId && review.ExpiresAt > DateTimeOffset.UtcNow;
}


