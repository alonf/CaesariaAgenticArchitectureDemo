using System.Net;
using System.Text.Json;
using CommandCenter.Web.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Caesarea.Deterministic.Tests;

/// <summary>Recovery requests must follow the configured Director address rather than another app's fixed port.</summary>
public sealed class DurableRecoveryClientTests
{
    /// <summary>A changed Director endpoint retains routing, server-side credentials and correlation.</summary>
    [Theory]
    [InlineData(41001)]
    [InlineData(41002)]
    public async Task UsesTheClientsDirectorAddress(int directorPort)
    {
        var root = Path.Combine(Path.GetTempPath(), "caesarea-recovery-client-" + Guid.NewGuid().ToString("N"));
        var settings = Path.Combine(root, "Services", "OperationsAgent.Functions");
        Directory.CreateDirectory(settings);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "Caesarea.slnx"), "<Solution />", TestContext.Current.CancellationToken);
            var key = new string('o', 32);
            await File.WriteAllTextAsync(Path.Combine(settings, "local.settings.json"), JsonSerializer.Serialize(new { Values = new { CAESAREA_OPERATOR_KEY = key } }), TestContext.Current.CancellationToken);
            using var handler = new CaptureHandler();
            using var http = new HttpClient(handler) { BaseAddress = new Uri($"https://localhost:{directorPort}/") };
            var client = new DurableRecoveryClient(http, new LocalEnvironment(root));

            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(CancellationToken.None));

            Assert.Equal(new Uri(http.BaseAddress, "api/recovery-demo/status"), handler.Address);
            Assert.Equal(key, handler.Key);
            Assert.False(string.IsNullOrWhiteSpace(handler.Correlation));
            Assert.Equal(1, handler.Calls);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public Uri? Address { get; private set; }
        public string? Key { get; private set; }
        public string? Correlation { get; private set; }
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Address = request.RequestUri;
            Key = request.Headers.GetValues("X-Caesarea-Key").Single();
            Correlation = request.Headers.GetValues("X-Correlation-ID").Single();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }

    private sealed class LocalEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "RecoveryClientTest";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
