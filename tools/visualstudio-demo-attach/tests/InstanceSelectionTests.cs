namespace VisualStudioDemoAttach.Tests;

public sealed class InstanceSelectionTests
{
    private const string Solution = @"C:\Dev\Caesarea\Caesarea.slnx";

    private static readonly InstanceCandidate Vs2026WithSolution = new(18, 100, Solution, "Visual Studio 2026 Enterprise");
    private static readonly InstanceCandidate Vs2026Empty = new(18, 200, null, "Visual Studio 2026 Enterprise");
    private static readonly InstanceCandidate Vs2026Elsewhere = new(18, 300, @"C:\Other\Other.sln", "Visual Studio 2026 Enterprise");
    private static readonly InstanceCandidate Vs2022WithSolution = new(17, 400, Solution, "Visual Studio 2022 Enterprise");
    private static readonly InstanceCandidate Vs2026SameFolder = new(18, 500, @"C:\Dev\Caesarea\Legacy.sln", "Visual Studio 2026 Enterprise");

    [Fact]
    public void TheSupportedInstanceWithTheSolutionWinsOverEveryOther()
    {
        // Older and unrelated instances are ordinary on a developer machine; none of them may
        // capture the demo's attach.
        var chosen = InstanceSelection.Choose([Vs2022WithSolution, Vs2026Elsewhere, Vs2026SameFolder, Vs2026WithSolution, Vs2026Empty], Solution, null, out var explanation);

        Assert.Same(Vs2026WithSolution, chosen);
        Assert.Contains("PID 100", explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AnotherSolutionFileInTheSameFolderCountsOnlyWhenNothingHasTheExactOne()
    {
        Assert.Same(Vs2026SameFolder, InstanceSelection.Choose([Vs2026SameFolder, Vs2026Elsewhere], Solution, null, out _));
        Assert.Same(Vs2026WithSolution, InstanceSelection.Choose([Vs2026SameFolder, Vs2026WithSolution], Solution, null, out _));
    }

    [Fact]
    public void TwoWindowsOnTheSameSolutionAreAQuestionNotACoinToss()
    {
        var twin = new InstanceCandidate(18, 101, Solution, "Visual Studio 2026 Enterprise");

        var chosen = InstanceSelection.Choose([Vs2026WithSolution, twin], Solution, null, out var explanation);

        Assert.Null(chosen);
        Assert.Contains("2 instances qualify", explanation, StringComparison.Ordinal);
        Assert.Contains("--instance-pid", explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePinnedInstanceIsUsedWhateverElseIsRunning()
    {
        // Detach goes to the window that attached, even when a second window on the same
        // solution has opened since.
        var twin = new InstanceCandidate(18, 101, Solution, "Visual Studio 2026 Enterprise");

        Assert.Same(twin, InstanceSelection.Choose([Vs2026WithSolution, twin], Solution, 101, out var explanation));
        Assert.Contains("is the instance that attached", explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void APinnedInstanceThatIsGoneIsReportedNotReplaced()
    {
        Assert.Null(InstanceSelection.Choose([Vs2026WithSolution], Solution, 999, out var explanation));
        Assert.Contains("PID 999", explanation, StringComparison.Ordinal);
        Assert.Contains("no longer running", explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Only2022HavingTheSolutionOpenIsExplainedNotUsed()
    {
        var chosen = InstanceSelection.Choose([Vs2022WithSolution], Solution, null, out var explanation);

        Assert.Null(chosen);
        Assert.Contains("Visual Studio 2022", explanation, StringComparison.Ordinal);
        Assert.Contains("Visual Studio 2026 only", explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void NoVisualStudioAtAllSaysWhatToOpen()
    {
        Assert.Null(InstanceSelection.Choose([], Solution, null, out var explanation));
        Assert.Contains("Open the Caesarea solution in Visual Studio 2026", explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AVisualStudio2026WithoutTheSolutionIsNamedSoThePresenterOpensIt()
    {
        Assert.Null(InstanceSelection.Choose([Vs2026Empty, Vs2026Elsewhere], Solution, null, out var explanation));
        Assert.Contains("no instance has Caesarea.slnx open", explanation, StringComparison.Ordinal);
        Assert.Contains("PID 200", explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutASolutionASingleSupportedInstanceIsAcceptedAndSeveralAreNot()
    {
        Assert.Same(Vs2026Empty, InstanceSelection.Choose([Vs2022WithSolution, Vs2026Empty], null, null, out _));

        Assert.Null(InstanceSelection.Choose([Vs2026Empty, Vs2026Elsewhere], null, null, out var explanation));
        Assert.Contains("Pass --solution", explanation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"C:\Dev\Caesarea\Caesarea.slnx", false, true)]
    [InlineData(@"c:\dev\caesarea\CAESAREA.SLNX", false, true)]
    [InlineData(@"C:\Dev\Caesarea\Legacy.sln", false, false)]
    [InlineData(@"C:\Dev\Caesarea\Legacy.sln", true, true)]
    [InlineData(@"C:\Dev\Other\Caesarea.slnx", true, false)]
    [InlineData("", true, false)]
    [InlineData(null, true, false)]
    public void SolutionMatchingIgnoresCaseAndTreatsTheSameFolderAsOptional(string? open, bool sameFolderCounts, bool expected) =>
        Assert.Equal(expected, InstanceSelection.SolutionMatches(open, Solution, sameFolderCounts));
}

public sealed class VisualStudioMonikerTests
{
    [Theory]
    [InlineData("!VisualStudio.DTE.18.0:12345", 18, 12345)]
    [InlineData("!VisualStudio.DTE.17.0:7", 17, 7)]
    public void AVisualStudioMonikerYieldsVersionAndProcess(string moniker, int expectedMajor, int expectedProcessId)
    {
        Assert.True(VisualStudioMoniker.TryParse(moniker, out var major, out var processId));
        Assert.Equal(expectedMajor, major);
        Assert.Equal(expectedProcessId, processId);
    }

    [Theory]
    [InlineData("!{9BA05972-F6A8-11CF-A442-00A0C90A8F39}")]
    [InlineData("!VisualStudio.DTE.18.0")]
    [InlineData("!VisualStudio.DTE.18.0:0")]
    [InlineData("!VisualStudio.DTE.x:12")]
    [InlineData("")]
    [InlineData(null)]
    public void OtherRunningObjectsAreIgnored(string? moniker) =>
        Assert.False(VisualStudioMoniker.TryParse(moniker, out _, out _));
}

public sealed class DebuggerEnginesTests
{
    [Fact]
    public void TheDotNetCoreEngineIsPreferredByExactName()
    {
        string[] engines = ["Native", "Managed (.NET Framework 4.x)", "Managed (.NET Core, .NET 5+)", "Script"];

        Assert.Equal("Managed (.NET Core, .NET 5+)", DebuggerEngines.Choose(engines));
    }

    [Fact]
    public void ARenamedDotNetCoreEngineIsFoundByPrefix()
    {
        // A future Visual Studio may reword the suffix; the family prefix is what matters.
        Assert.Equal("Managed (.NET Core, .NET 5+, .NET 10)", DebuggerEngines.Choose(["Managed (.NET Framework 4.x)", "Managed (.NET Core, .NET 5+, .NET 10)"]));
    }

    [Fact]
    public void NoDotNetCoreEngineLeavesTheChoiceToVisualStudio() =>
        Assert.Null(DebuggerEngines.Choose(["Native", "Managed (.NET Framework 4.x)"]));
}
