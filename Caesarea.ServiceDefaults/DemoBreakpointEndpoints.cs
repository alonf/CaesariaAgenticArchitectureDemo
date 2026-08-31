using Caesarea.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Maps the presenter-only demo breakpoint endpoints used by the DemoControl switchboard.
/// </summary>
public static class DemoBreakpointEndpoints
{
    /// <summary>
    /// Registers the supplied snippets and, in the Development environment only, maps the
    /// <c>/api/demo-breakpoints</c> endpoints that let the presenter arm them at runtime.
    /// </summary>
    /// <param name="app">The web application being configured.</param>
    /// <param name="snippetNames">The snippet names this service can pause on.</param>
    /// <returns>The same <see cref="WebApplication"/> instance for chaining.</returns>
    public static WebApplication MapDemoBreakpoints(this WebApplication app, params string[] snippetNames)
    {
        ArgumentNullException.ThrowIfNull(app);

        DemoBreakpoints.Register(snippetNames);

        // Demo breakpoints are a presenter-only affordance; audience/production profiles never expose them.
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        var group = app.MapGroup("/api/demo-breakpoints").WithTags("Demo Breakpoints");

        // Presenter-machine only: reject any non-loopback caller even in Development, because an armed
        // breakpoint can pause the service under an attached debugger.
        group.AddEndpointFilter(async (invocationContext, next) =>
        {
            var remoteAddress = invocationContext.HttpContext.Connection.RemoteIpAddress;
            if (remoteAddress is null || !System.Net.IPAddress.IsLoopback(remoteAddress))
            {
                return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
            }

            return await next(invocationContext);
        });

        group.MapGet("/", () => TypedResults.Ok(CreateResponse()));

        group.MapPut("/{snippetName}", IResult (string snippetName, DemoBreakpointArmRequest request) =>
            DemoBreakpoints.TrySetArmed(snippetName, request.Armed)
                ? TypedResults.Ok(CreateResponse())
                : TypedResults.NotFound());

        return app;
    }

    private static DemoBreakpointsResponse CreateResponse() =>
        new(DemoBreakpoints.IsDebuggerAttached, DemoBreakpoints.GetStatus());
}

/// <summary>
/// Reports the demo breakpoint state of one service.
/// </summary>
/// <param name="DebuggerAttached">Whether a debugger is attached to the service process.</param>
/// <param name="Snippets">The registered snippets and their armed state.</param>
public sealed record DemoBreakpointsResponse(bool DebuggerAttached, IReadOnlyList<DemoBreakpointStatus> Snippets);

/// <summary>
/// Arms or disarms one demo breakpoint.
/// </summary>
/// <param name="Armed">Whether the snippet should pause on its next debugged run.</param>
public sealed record DemoBreakpointArmRequest(bool Armed);
