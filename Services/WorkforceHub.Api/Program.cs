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
workforce.MapGroup("/admin").MapPost("/reset", Reset);

// The presenter's own view: the records in full, so the lecture can show what was withheld beside
// what crossed. Loopback only - this is the payload the whole stage exists to keep inside.
app.MapGet("/api/workforce-records", (HttpContext context, WorkforceHubService hub) =>
{
    var remoteAddress = context.Connection.RemoteIpAddress;

    return remoteAddress is null || !System.Net.IPAddress.IsLoopback(remoteAddress)
        ? Results.StatusCode(StatusCodes.Status403Forbidden)
        : Results.Ok(hub.GetAllInFull());
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
