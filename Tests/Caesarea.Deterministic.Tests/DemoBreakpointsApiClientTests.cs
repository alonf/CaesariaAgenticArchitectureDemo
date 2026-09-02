using System.Net;
using System.Net.Http.Json;
using DemoControl.Web.Services;
using Microsoft.Extensions.Hosting;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// The switchboard reads breakpoints from every service that registers snippets. One service being
/// slow or down is ordinary - they start and stop independently - and it must cost the panel that
/// service's row, never the whole panel.
/// </summary>
public sealed class DemoBreakpointsApiClientTests
{
    [Fact]
    public async Task OneHangingServiceStillLeavesEveryOtherRowReadable()
    {
        // A hang surfaces as a cancellation once the deadline fires. Treating that as caller
        // cancellation failed the fan-out and blanked the panel; it belongs to that row instead.
        var client = new DemoBreakpointsApiClient(
            new StubBreakpointHttpClientFactory(hangingClientName: DemoBreakpointsApiClient.Services[1].ClientName));

        var sources = await client.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DemoBreakpointsApiClient.Services.Count, sources.Count);

        var hung = sources[1];
        Assert.NotNull(hung.Error);
        Assert.Contains("did not respond", hung.Error, StringComparison.Ordinal);
        Assert.Empty(hung.Snippets);

        // The services that answered are unaffected.
        foreach (var healthy in sources.Where((_, index) => index != 1))
        {
            Assert.Null(healthy.Error);
            Assert.Single(healthy.Snippets);
        }
    }

    [Fact]
    public async Task AServiceThatRefusesTheConnectionBecomesItsOwnRow()
    {
        var client = new DemoBreakpointsApiClient(
            new StubBreakpointHttpClientFactory(failingClientName: DemoBreakpointsApiClient.Services[2].ClientName));

        var sources = await client.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DemoBreakpointsApiClient.Services.Count, sources.Count);
        Assert.NotNull(sources[2].Error);
        Assert.All(sources.Take(2), source => Assert.Null(source.Error));
    }

    [Fact]
    public async Task CallerCancellationStillPropagates()
    {
        // The page going away is not a per-service failure, and must not be reported as one.
        var client = new DemoBreakpointsApiClient(
            new StubBreakpointHttpClientFactory(hangingClientName: DemoBreakpointsApiClient.Services[0].ClientName));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetStatusAsync(cancellation.Token));
    }

    private sealed class StubBreakpointHttpClientFactory(string? hangingClientName = null, string? failingClientName = null)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new StubHandler(name == hangingClientName, name == failingClientName))
            {
                BaseAddress = new Uri("http://localhost/")
            };

        private sealed class StubHandler(bool hangs, bool fails) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (fails)
                {
                    throw new HttpRequestException("Connection refused.");
                }

                if (hangs)
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(
                        new DemoBreakpointsResponse(false, [new DemoBreakpointStatus("SNIPPET", false)]),
                        options: CaesareaJsonDefaults.CreateSerializerOptions())
                };
            }
        }
    }
}
