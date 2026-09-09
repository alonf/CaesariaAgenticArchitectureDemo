using System.Net;
using System.Text;
using DemoControl.Web.Services;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// The switchboard's error lines say what a service refused and why. A bare "409 (Conflict)" sent a
/// presenter reading code to find out that a switch needs a later stage.
/// </summary>
public sealed class ApiProblemExceptionTests
{
    [Fact]
    public async Task AStageGatedRefusalCarriesTheReasonAndTheStage()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                """
                {"status":409,"title":"Agent habitat toggle disabled in the current demo stage","detail":"Selecting the agent habitat requires the Hosting stage; the current stage is A2ADelegation.","correlationId":"abc","requiredStage":"Hosting"}
                """,
                Encoding.UTF8,
                "application/problem+json")
        };
        response.Headers.Add(CorrelationHeaderNames.XCorrelationId, "abc");

        var exception = await Assert.ThrowsAsync<ApiProblemException>(
            () => response.EnsureSuccessAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Selecting the agent habitat requires the Hosting stage; the current stage is A2ADelegation.", exception.Message);
        Assert.Equal("Agent habitat toggle disabled in the current demo stage", exception.Title);
        Assert.Equal(DemoStage.Hosting, exception.RequiredStage);
        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal("abc", exception.CorrelationId);
    }

    [Fact]
    public async Task ARefusalWithoutAProblemDocumentFallsBackToTheStatusLine()
    {
        // A gateway's HTML or an empty body is not a reason; the status line is all there is.
        using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            ReasonPhrase = "Service Unavailable",
            Content = new StringContent("<html>upstream down</html>", Encoding.UTF8, "text/html")
        };

        var exception = await Assert.ThrowsAsync<ApiProblemException>(
            () => response.EnsureSuccessAsync(TestContext.Current.CancellationToken));

        Assert.Equal("HTTP 503 Service Unavailable", exception.Message);
        Assert.Null(exception.RequiredStage);
    }

    [Fact]
    public async Task AnUnknownRequiredStageIsNotAStage()
    {
        // A stage name this switchboard does not know is left out rather than offered.
        using var response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                """{"status":409,"title":"Refused","detail":"Requires a stage.","requiredStage":"Warp"}""",
                Encoding.UTF8,
                "application/problem+json")
        };

        var exception = await Assert.ThrowsAsync<ApiProblemException>(
            () => response.EnsureSuccessAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Requires a stage.", exception.Message);
        Assert.Null(exception.RequiredStage);
    }

    [Fact]
    public async Task ASuccessIsLeftAlone()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };

        Assert.Null(await Record.ExceptionAsync(() => response.EnsureSuccessAsync(TestContext.Current.CancellationToken)));
    }
}
