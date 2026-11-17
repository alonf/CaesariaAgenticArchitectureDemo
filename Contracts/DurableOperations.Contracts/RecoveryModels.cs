namespace DurableOperations.Contracts;

/// <summary>A turn addressed to the durable agent; the session identifier is opaque, not authorization.</summary>
public sealed record DurableAgentTurn(string Message, string? SessionId = null);

/// <summary>A completed turn and the persisted session to use for the next turn.</summary>
public sealed record DurableAgentReply(string Message, string SessionId);

/// <summary>A recovery request bound to one authoritative Hub revision and Command Center stage epoch.</summary>
public sealed record RecoveryRequest(string AssetId, long ExpectedRevision, DateTimeOffset StageAppliedAt, string CorrelationId);

/// <summary>The review displayed to the supervisor before a decision is accepted.</summary>
public sealed record RecoveryReview(string InstanceId, string AssetId, long Revision, string Challenge, DateTimeOffset ExpiresAt);

/// <summary>A supervisor response echoes the review, preventing approval of a different revision or expired prompt.</summary>
public sealed record RecoveryDecisionRequest(RecoveryReview Review, bool Approved);

/// <summary>The authenticated boundary's signed decision. It is never an agent tool argument.</summary>
public sealed record RecoveryDecision(RecoveryReview Review, bool Approved, string Reviewer, DateTimeOffset DecidedAt, string Signature);

/// <summary>Durable progress; a waiting process is not a completed city operation.</summary>
public sealed record RecoveryProgress(string State, string Summary, RecoveryReview? Review = null);

/// <summary>A recovery observation. Null execution/resolution means unknown, never a confirmed negative.</summary>
public sealed record RecoveryResult(string State, bool? CommandExecuted, bool? Resolved, string Summary);
