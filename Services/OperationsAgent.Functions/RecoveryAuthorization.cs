using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DurableOperations.Contracts;

namespace OperationsAgent.Functions;

// A local rehearsal boundary, not a substitute for Entra identity and resource-scoped RBAC.
// The signing key never enters an agent prompt, tool schema or durable input/history.
/// <summary>Separates local operator and supervisor credentials and signs immutable review decisions.</summary>
public sealed class RecoveryAuthorization(string operatorKey, string supervisorKey, string supervisorName)
{
    internal bool IsOperator(string? value) => Matches(operatorKey, value);

    internal bool IsSupervisor(string? value) => Matches(supervisorKey, value);

    internal RecoveryDecision Sign(RecoveryDecisionRequest request, DateTimeOffset now)
    {
        var decision = new RecoveryDecision(request.Review, request.Approved, supervisorName, now, string.Empty);
        return decision with { Signature = Signature(decision) };
    }

    internal bool Verify(RecoveryDecision decision, RecoveryReview expected, DateTimeOffset now) =>
        decision.Review == expected
        && decision.DecidedAt <= now
        && decision.DecidedAt < expected.ExpiresAt
        && now < expected.ExpiresAt
        && decision.Reviewer == supervisorName
        && Matches(Signature(decision), decision.Signature);

    private string Signature(RecoveryDecision decision) =>
        Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(supervisorKey),
            JsonSerializer.SerializeToUtf8Bytes(decision with { Signature = string.Empty })));

    private static bool Matches(string expected, string? actual) =>
        actual is not null && CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
            SHA256.HashData(Encoding.UTF8.GetBytes(actual)));
}
