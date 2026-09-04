var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<WorkforceHubService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.MapDefaultEndpoints();

var workforce = app.MapGroup("/api/workforce")
    .WithTags("Workforce Hub");

// Two reads, and the difference between them is the whole point. The search says which work orders
// exist; the projection says what may be known about one. Neither returns a rate or a name.
workforce.MapGet("/assets/{assetId}/work-orders", FindForAsset);
workforce.MapGet("/work-orders/{workOrderId}/shareable", GetShareableDetails);

// The scenario system's one lever here: put the domain back to its own fixture between demos, so
// nothing a previous walk did survives into the next one.
//
// Mapped only where a presenter is driving. Deployed to Azure this service has public ingress -
// the Workforce agent runs in a Foundry sandbox outside its VNet and cannot reach it otherwise -
// and that ingress authenticates callers without checking which application role they hold. "Erase
// the running scenario" would then be one valid token away. The two reads above are what the agent
// needs; this is not.
//
// /api/workforce-records below needs no such switch: it already requires the caller to name itself
// the switchboard AND to connect over loopback, and nothing in a container app is loopback.
if (builder.Configuration.GetValue("WorkforceHubApi:EnableDemoControlSurface", true))
{
    workforce.MapGroup("/admin").MapPost("/reset", Reset);
}

// The presenter's own view: the records in full, so the lecture can show what was withheld beside
// what crossed. This is the payload the whole stage exists to keep inside, so it is gated twice and
// the two gates are not the same gate. The caller must name itself as the switchboard, because in
// this demo every service is a loopback process and "local" identifies nobody; and the connection
// must still be loopback, so naming yourself the switchboard from another machine gets you nothing.
app.MapGet("/api/workforce-records", (HttpContext context, WorkforceHubService hub) =>
{
    if (CallerIdentity.Reject(context, CallerIdentity.DemoControl) is { } forbidden)
    {
        return forbidden;
    }

    var remoteAddress = context.Connection.RemoteIpAddress;

    return remoteAddress is not null && System.Net.IPAddress.IsLoopback(remoteAddress)
        ? Results.Ok(hub.GetAllInFull())
        : Results.Problem(ProblemDetailsFactory.Create(
            StatusCodes.Status403Forbidden,
            "Caller not permitted",
            "The work orders in full are served to the presenter's own machine only.",
            context.GetCorrelationId()));
}).WithTags("Workforce Hub");

await app.RunAsync();

static IResult FindForAsset(HttpContext context, string assetId, WorkforceHubService hub)
{
    if (CallerIdentity.Reject(context, CallerIdentity.WorkforceAgent) is { } forbidden)
    {
        return forbidden;
    }

    return string.IsNullOrWhiteSpace(assetId)
        ? TypedResults.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Invalid request",
            "An asset identifier is required.",
            context.GetCorrelationId()))
        : TypedResults.Ok(hub.FindForAsset(assetId, context.GetCorrelationId()));
}

static IResult GetShareableDetails(HttpContext context, string workOrderId, WorkforceHubService hub)
{
    if (CallerIdentity.Reject(context, CallerIdentity.WorkforceAgent) is { } forbidden)
    {
        return forbidden;
    }

    if (string.IsNullOrWhiteSpace(workOrderId))
    {
        return TypedResults.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Invalid request",
            "A work order identifier is required.",
            context.GetCorrelationId()));
    }

    return hub.GetShareableDetails(workOrderId, context.GetCorrelationId()) is { } details
        ? TypedResults.Ok(details)
        : TypedResults.NotFound(ProblemDetailsFactory.Create(
            StatusCodes.Status404NotFound,
            "Work order not found",
            $"No work order {workOrderId} exists.",
            context.GetCorrelationId()));
}

static IResult Reset(HttpContext context, WorkforceHubService hub) =>
    CallerIdentity.Reject(context, CallerIdentity.DemoScenario)
        ?? TypedResults.Ok(hub.Reset(context.GetCorrelationId()));
