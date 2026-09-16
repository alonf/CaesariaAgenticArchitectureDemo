using System.Text.Json;

namespace VisualStudioDemoAttach.Tests;

/// <summary>
/// The JSON the helper prints is what DemoControl's VisualStudioAttachService deserializes, so
/// its shape - camelCase names, nulls omitted, the instance's four fields - is a contract.
/// </summary>
public sealed class HelperReportTests
{
    [Fact]
    public void TheReportSerializesCamelCaseWithoutNulls()
    {
        var report = new HelperReport(
            true,
            "attach",
            "Attached.",
            Attached: true,
            Instance: new VisualStudioInstanceReport("Visual Studio 2026 Enterprise", "18.9", 100, @"C:\Dev\Caesarea\Caesarea.slnx", true),
            Target: new TargetReport("OperationsAgent.Api.exe", 4242));

        var json = JsonSerializer.Serialize(report, HelperReportJsonContext.Default.HelperReport);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.GetProperty("succeeded").GetBoolean());
        Assert.Equal("attach", root.GetProperty("operation").GetString());
        Assert.Equal("Attached.", root.GetProperty("message").GetString());
        Assert.True(root.GetProperty("attached").GetBoolean());
        Assert.False(root.TryGetProperty("instances", out _));

        var instance = root.GetProperty("instance");
        Assert.Equal("Visual Studio 2026 Enterprise", instance.GetProperty("displayName").GetString());
        Assert.Equal("18.9", instance.GetProperty("version").GetString());
        Assert.Equal(100, instance.GetProperty("processId").GetInt32());
        Assert.Equal(@"C:\Dev\Caesarea\Caesarea.slnx", instance.GetProperty("solution").GetString());
        Assert.True(instance.GetProperty("supported").GetBoolean());

        Assert.Equal(4242, root.GetProperty("target").GetProperty("processId").GetInt32());
    }

    [Fact]
    public void AFailureCarriesTheInstancesItSaw()
    {
        var report = HelperReport.Failed("status", "No instance has the solution open.", [new VisualStudioInstanceReport("Visual Studio 2022 Enterprise", "17.14", 400, null, false)]);

        var json = JsonSerializer.Serialize(report, HelperReportJsonContext.Default.HelperReport);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.False(root.GetProperty("succeeded").GetBoolean());
        Assert.False(root.TryGetProperty("instance", out _));
        var seen = Assert.Single(root.GetProperty("instances").EnumerateArray());
        Assert.False(seen.GetProperty("supported").GetBoolean());
        Assert.False(seen.TryGetProperty("solution", out _));
    }
}
