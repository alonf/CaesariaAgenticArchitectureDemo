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
        Assert.Equal(new Version(0, 2, 0), VsCodeAttachService.TryParsePackagedVersion("demo-attach-0.2.0.vsix"));
        Assert.Null(VsCodeAttachService.TryParsePackagedVersion("demo-attach.vsix"));
    }

    [Theory]
    [InlineData("0.1.0", "0.2.0", true)]
    [InlineData("0.2.0", "0.2.0", false)]
    [InlineData("0.3.0", "0.2.0", false)]
    [InlineData(null, "0.2.0", false)]
    public void AnUpdateIsOfferedOnlyWhenThePackageIsNewer(string? installed, string? packaged, bool expected) =>
        Assert.Equal(expected, VsCodeAttachService.IsUpdateAvailable(installed, packaged));

    [Fact]
    public void TheStatusOffersAnUpdateOnlyForAnOlderInstall()
    {
        // An older install would swallow the /detach route, so the switchboard must be told to
        // update before it relies on it; a missing extension is an install, not an update.
        Assert.True(new VsCodeAttachStatus(VsCodeAvailable: true, ExtensionInstalled: true, "0.1.0", "0.2.0").UpdateAvailable);
        Assert.False(new VsCodeAttachStatus(VsCodeAvailable: true, ExtensionInstalled: false, null, "0.2.0").UpdateAvailable);
    }
}
