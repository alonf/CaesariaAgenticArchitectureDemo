using DemoControl.Web.Services;

namespace Caesarea.Deterministic.Tests;

public sealed class VsCodeAttachServiceTests
{
    [Fact]
    public void TheInstalledExtensionIsReadWithItsVersion()
    {
        var (installed, version) = VsCodeAttachService.ParseInstalledExtension(
            "ms-dotnettools.csharp@2.140.9\r\ncaesarea-demo.demo-attach@0.1.0\r\n");

        Assert.True(installed);
        Assert.Equal("0.1.0", version);
    }

    [Fact]
    public void AMissingExtensionIsNotInstalled()
    {
        var (installed, version) = VsCodeAttachService.ParseInstalledExtension("ms-dotnettools.csharp@2.140.9\n");

        Assert.False(installed);
        Assert.Null(version);
    }

    [Fact]
    public void ThePackagedVersionComesFromTheVsixName()
    {
        Assert.Equal(new Version(0, 3, 0), VsCodeAttachService.TryParsePackagedVersion("demo-attach-0.3.0.vsix"));
        Assert.Null(VsCodeAttachService.TryParsePackagedVersion("demo-attach.vsix"));
    }

    [Theory]
    [InlineData("0.2.0", "0.3.0", true)]
    [InlineData("0.3.0", "0.3.0", false)]
    [InlineData("0.4.0", "0.3.0", false)]
    [InlineData(null, "0.3.0", false)]
    public void AnUpdateIsOfferedOnlyWhenThePackageIsNewer(string? installed, string? packaged, bool expected) =>
        Assert.Equal(expected, VsCodeAttachService.IsUpdateAvailable(installed, packaged));

    [Fact]
    public void TheStatusOffersAnUpdateOnlyForAnOlderInstall()
    {
        // An older install would swallow the /detach route and the processId parameter, so the
        // switchboard must be told to update before it relies on them; a missing extension is an
        // install, not an update.
        Assert.True(new VsCodeAttachStatus(VsCodeAvailable: true, ExtensionInstalled: true, "0.2.0", "0.3.0").UpdateAvailable);
        Assert.False(new VsCodeAttachStatus(VsCodeAvailable: true, ExtensionInstalled: false, null, "0.3.0").UpdateAvailable);
    }

    [Fact]
    public void TheRequestUriCarriesTheProcessIdWhenTheServiceReportedOne()
    {
        // The id is what the service itself reported, so the extension attaches to exactly that
        // process and needs no platform-specific lookup; without one the name is all it gets.
        Assert.Equal(
            "vscode://caesarea-demo.demo-attach/attach?processName=OperationsAgent.Api.exe&processId=4242",
            VsCodeAttachService.BuildRequestUri("attach", new DebuggerTarget("OperationsAgent.Api.exe", 4242)));
        Assert.Equal(
            "vscode://caesarea-demo.demo-attach/detach?processName=OperationsAgent.Api",
            VsCodeAttachService.BuildRequestUri("detach", new DebuggerTarget("OperationsAgent.Api", null)));
    }

    [Fact]
    public void ArgumentsAreQuotedForCmdSoTheUriSeparatorSurvives()
    {
        // cmd.exe reads an unquoted '&' as a command separator and would run "processId=4242"
        // as a program; the quote keeps the whole URI one argument.
        Assert.Equal("\"vscode://caesarea-demo.demo-attach/attach?processName=X&processId=4242\"", VsCodeAttachService.QuoteForCmd("vscode://caesarea-demo.demo-attach/attach?processName=X&processId=4242"));
        Assert.Equal("\"C:\\Demo Tools\\demo-attach-0.3.0.vsix\"", VsCodeAttachService.QuoteForCmd("C:\\Demo Tools\\demo-attach-0.3.0.vsix"));
        Assert.Throws<ArgumentException>(() => VsCodeAttachService.QuoteForCmd("a\"b"));
    }

    [Fact]
    public void AvailabilityFollowsTheToolingStatusAndNamesTheFix()
    {
        var noCli = VsCodeAttachService.Describe(new VsCodeAttachStatus(VsCodeAvailable: false, ExtensionInstalled: false));
        Assert.False(noCli.Available);
        Assert.Equal("CLI not found", noCli.Summary);
        Assert.Null(noCli.SetupAction);

        var missing = VsCodeAttachService.Describe(new VsCodeAttachStatus(VsCodeAvailable: true, ExtensionInstalled: false, null, "0.3.0"));
        Assert.False(missing.Available);
        Assert.Equal("Install VS Code extension", missing.SetupAction);

        var stale = VsCodeAttachService.Describe(new VsCodeAttachStatus(VsCodeAvailable: true, ExtensionInstalled: true, "0.2.0", "0.3.0"));
        Assert.False(stale.Available);
        Assert.Equal("Update VS Code extension 0.2.0 to 0.3.0", stale.SetupAction);

        var current = VsCodeAttachService.Describe(new VsCodeAttachStatus(VsCodeAvailable: true, ExtensionInstalled: true, "0.3.0", "0.3.0"));
        Assert.True(current.Available);
        Assert.Equal("extension 0.3.0", current.Summary);
        Assert.Null(current.SetupAction);
    }

    [Fact]
    public void TheCliIsFoundOnPathOrAtAWellKnownPlace()
    {
        var pathVariable = string.Join(Path.PathSeparator, "/usr/bin", "/home/presenter/.local/bin");

        Assert.Equal("code", VsCodeAttachService.LocateCli(pathVariable, candidate => candidate == Path.Combine("/home/presenter/.local/bin", "code")));

        // No PATH hit: the macOS app bundle is the usual place when "Install 'code' command" was skipped.
        Assert.Equal(
            "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code",
            VsCodeAttachService.LocateCli(pathVariable, candidate => candidate.StartsWith("/Applications/", StringComparison.Ordinal)));

        Assert.Null(VsCodeAttachService.LocateCli(null, _ => false));
    }

    [Fact]
    public void ProcessNamesFollowThePlatform()
    {
        // Windows runs the apphost as OperationsAgent.Api.exe; macOS and Linux as OperationsAgent.Api.
        Assert.Equal("OperationsAgent.Api.exe", DemoProcessNames.For("OperationsAgent.Api", isWindows: true));
        Assert.Equal("OperationsAgent.Api", DemoProcessNames.For("OperationsAgent.Api", isWindows: false));
        Assert.Equal(DemoProcessNames.For("EnergyHub.Api", OperatingSystem.IsWindows()), DemoProcessNames.OnThisPlatform("EnergyHub.Api"));
        Assert.All(DemoBreakpointsApiClient.Services, service => Assert.Equal(OperatingSystem.IsWindows(), service.ProcessName.EndsWith(".exe", StringComparison.Ordinal)));
    }
}
