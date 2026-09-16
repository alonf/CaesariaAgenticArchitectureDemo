using DemoControl.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// The Visual Studio adapter is portable code: it reads the helper's JSON and, off Windows or
/// without the helper built, answers "unavailable" without touching anything. These tests run on
/// every platform; the live round trip with a real Visual Studio is <see cref="DebuggerLiveTests"/>.
/// </summary>
public sealed class VisualStudioAttachServiceTests
{
    [Fact]
    public void AHelperReportIsReadWithItsInstance()
    {
        const string json = """
            {"succeeded":true,"operation":"status","message":"Visual Studio 2026 Enterprise 18.0 (PID 31240) has Caesarea.slnx open.","attached":false,
             "instance":{"displayName":"Visual Studio 2026 Enterprise","version":"18.0","processId":31240,"solution":"C:\\Dev\\Caesarea\\Caesarea.slnx","supported":true},
             "instances":[{"displayName":"Visual Studio 2026 Enterprise","version":"18.0","processId":31240,"solution":"C:\\Dev\\Caesarea\\Caesarea.slnx","supported":true}]}
            """;

        var report = VisualStudioAttachService.ParseReport(json);

        Assert.True(report.Succeeded);
        Assert.Equal("status", report.Operation);
        Assert.NotNull(report.Instance);
        Assert.Equal(31240, report.Instance.ProcessId);
        Assert.Equal("Visual Studio 2026 Enterprise", report.Instance.DisplayName);
        Assert.EndsWith("Caesarea.slnx", report.Instance.Solution, StringComparison.Ordinal);
    }

    [Fact]
    public void OutputThatIsNotAReportBecomesAFailedReportCarryingIt()
    {
        // A crash before the helper prints its report leaves a stack trace on standard output; the
        // presenter needs to see it, not a blank.
        var report = VisualStudioAttachService.ParseReport("Unhandled exception. System.Runtime.InteropServices.COMException: boom\n   at Program.Main");

        Assert.False(report.Succeeded);
        Assert.Contains("at Program.Main", report.Message, StringComparison.Ordinal);
        Assert.Null(report.Instance);

        Assert.Equal("The Visual Studio helper produced no report.", VisualStudioAttachService.ParseReport("  ").Message);
        Assert.False(VisualStudioAttachService.ParseReport("{not json").Succeeded);
    }

    [Fact]
    public void AnInstanceWithTheSolutionMakesVisualStudioAvailable()
    {
        var report = new VisualStudioHelperReport(true, "status", "open", false, new VisualStudioInstanceReport("Visual Studio 2026 Enterprise", "18.0", 31240, "C:\\x\\Caesarea.slnx"));

        var availability = VisualStudioAttachService.Describe(report);

        Assert.True(availability.Available);
        Assert.Contains("PID 31240", availability.Summary, StringComparison.Ordinal);
        Assert.Null(availability.SetupAction);
    }

    [Fact]
    public void AHelperFailureIsTheReasonVisualStudioIsUnavailable()
    {
        var report = new VisualStudioHelperReport(false, "status", "Visual Studio is not running. Open the Caesarea solution in Visual Studio 2026 first.", false, null);

        var availability = VisualStudioAttachService.Describe(report);

        Assert.False(availability.Available);
        Assert.Equal("solution not open", availability.Summary);
        Assert.Equal(report.Message, availability.Detail);
    }

    [Fact]
    public async Task WithoutTheHelperNothingIsLaunchedAndTheAnswerNamesThePlatformOrTheBuild()
    {
        // An empty content root has no helper next to it, so this is the "not built" path on
        // Windows and the "not this platform" path elsewhere - both without starting a process.
        using var root = new TemporaryContentRoot();
        var adapter = new VisualStudioAttachService(new StubWebHostEnvironment(root.Path), NullLogger<VisualStudioAttachService>.Instance);

        var availability = await adapter.GetAvailabilityAsync(TestContext.Current.CancellationToken);
        var attach = await adapter.AttachAsync(new DebuggerTarget("OperationsAgent.Api", 4242), TestContext.Current.CancellationToken);
        var setup = await adapter.SetUpAsync(TestContext.Current.CancellationToken);
        var holds = await adapter.IsAttachedAsync(new DebuggerTarget("OperationsAgent.Api", 4242), TestContext.Current.CancellationToken);

        Assert.False(availability.Available);
        Assert.False(attach.Succeeded);
        Assert.False(setup.Succeeded);
        // Without the helper there is nobody to ask: unknown, not "no".
        Assert.Null(holds);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("helper not built", availability.Summary);
            Assert.Equal("Build Visual Studio helper", availability.SetupAction);
            Assert.Contains("not built", attach.Message, StringComparison.Ordinal);
            Assert.Contains("was not found", setup.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal("not on this platform", availability.Summary);
            Assert.Null(availability.SetupAction);
            Assert.Contains("Use VS Code", attach.Message, StringComparison.Ordinal);
            Assert.Contains("Windows only", setup.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheAdapterIsNamedForThePickerAndTheSessionRecord()
    {
        var adapter = new VisualStudioAttachService(new StubWebHostEnvironment(Path.GetTempPath()), NullLogger<VisualStudioAttachService>.Instance);

        Assert.Equal("visualstudio", adapter.Id);
        Assert.Equal("Visual Studio 2026", adapter.DisplayName);
    }
}

/// <summary>
/// A host environment rooted wherever a test says, so the adapters' relative paths resolve
/// against a folder the test controls.
/// </summary>
internal sealed class StubWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";

    public string ApplicationName { get; set; } = "DemoControl.Web";

    public string ContentRootPath { get; set; } = contentRootPath;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

    public string WebRootPath { get; set; } = contentRootPath;

    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}

/// <summary>
/// An empty folder that is deleted with the test.
/// </summary>
internal sealed class TemporaryContentRoot : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("caesarea-debugger-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is not worth failing a test over.
        }
    }
}
