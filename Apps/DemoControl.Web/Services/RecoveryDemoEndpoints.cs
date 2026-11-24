using System.Security.Cryptography;
using System.Text;
using DurableOperations.Contracts;

namespace DemoControl.Web.Services;

internal static partial class RecoveryDemoEndpoints
{
    /// <summary>Maps the local Command Center adapter; worker lifecycle remains presenter-only.</summary>
    public static void MapRecoveryDemo(this WebApplication app)
    {
        if (!app.Services.GetRequiredService<RecoveryLocalSettings>().Enabled) { return; }
        var endpoints = app.MapGroup("/api/recovery-demo");
        endpoints.MapGet("/status", (HttpContext context, RecoveryLocalSettings settings, RecoveryRehearsal rehearsal) =>
            Authorized(context, settings, supervisor: false) ? Results.Json(rehearsal.State) : Results.Unauthorized());
        endpoints.MapPost("/start", async (HttpContext context, RecoveryLocalSettings settings, RecoveryRehearsal rehearsal) =>
        {
            if (!Authorized(context, settings, supervisor: false)) { return Results.Unauthorized(); }
            await rehearsal.BeginAsync();
            return Results.Json(rehearsal.State);
        });
        endpoints.MapPost("/decision", async (HttpContext context, RecoveryDecisionRequest decision, RecoveryLocalSettings settings, RecoveryRehearsal rehearsal) =>
        {
            if (!Authorized(context, settings, supervisor: true)) { return Results.Unauthorized(); }
            await rehearsal.DecideAsync(decision.Review, decision.Approved);
            return Results.Json(rehearsal.State);
        });
        endpoints.MapPost("/resume", async (HttpContext context, RecoveryLocalSettings settings, RecoveryRehearsal rehearsal) =>
        {
            if (!Authorized(context, settings, supervisor: false)) { return Results.Unauthorized(); }
            await rehearsal.ResumeAsync();
            return Results.Json(rehearsal.State);
        });
    }

    private static bool Authorized(HttpContext context, RecoveryLocalSettings settings, bool supervisor)
    {
        var received = context.Request.Headers["X-Caesarea-Key"].ToString();
        var expected = settings.Read()[supervisor ? "CAESAREA_SUPERVISOR_KEY" : "CAESAREA_OPERATOR_KEY"];
        var correlation = InvocationCorrelation.Resolve(context);
        context.Response.Headers[CorrelationHeaderNames.XCorrelationId] = correlation;
        var authorized = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(received), Encoding.UTF8.GetBytes(expected));
        AccessChecked(context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RecoveryDemoEndpoints"),
            supervisor ? "supervisor" : "operator", authorized, correlation);
        return authorized;
    }

    [LoggerMessage(2520, LogLevel.Information, "Recovery demo adapter {Role} authorization: {Authorized}. CorrelationId: {CorrelationId}")]
    private static partial void AccessChecked(ILogger logger, string role, bool authorized, string correlationId);
}
