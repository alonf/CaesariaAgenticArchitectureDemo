var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<SecurityHubService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.MapDefaultEndpoints();

var security = app.MapGroup("/api/security")
    .WithTags("Security Hub");

security.MapGet("/areas/{area}/operations", GetAreaStatus);

var admin = security.MapGroup("/admin");
admin.MapPost("/reset", Reset);
admin.MapPost("/scenario", ApplyScenario)
    .ValidateBody<SecurityScenarioSyncRequest>();

await app.RunAsync();

static IResult GetAreaStatus(HttpContext context, string area, SecurityHubService hub)
{
    // Reading operations is the restricted capability, and the rule is enforced here rather than
    // assumed of the callers: only the Security domain's own agent is admitted.
    if (CallerIdentity.Reject(context, CallerIdentity.SecurityAgent) is { } forbidden)
    {
        return forbidden;
    }

    if (string.IsNullOrWhiteSpace(area))
    {
        return TypedResults.BadRequest(ProblemDetailsFactory.Create(
            StatusCodes.Status400BadRequest,
            "Invalid request",
            "An area is required.",
            context.GetCorrelationId()));
    }

    return TypedResults.Ok(hub.GetAreaStatus(area, context.GetCorrelationId()));
}

static IResult ApplyScenario(HttpContext context, SecurityScenarioSyncRequest request, SecurityHubService hub) =>
    CallerIdentity.Reject(context, CallerIdentity.DemoScenario)
        ?? TypedResults.Ok(hub.ApplyScenario(request, context.GetCorrelationId()));

static IResult Reset(HttpContext context, SecurityHubService hub) =>
    CallerIdentity.Reject(context, CallerIdentity.DemoScenario)
        ?? TypedResults.Ok(hub.Reset(context.GetCorrelationId()));
