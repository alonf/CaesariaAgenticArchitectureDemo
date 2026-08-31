namespace Caesarea.Deterministic.Tests;

public sealed class DemoBreakpointsTests
{
    [Fact]
    public void RegisterAddsSnippetsDisarmed()
    {
        DemoBreakpoints.Register("TEST_REGISTER_A", "TEST_REGISTER_B");

        var status = DemoBreakpoints.GetStatus();

        Assert.Contains(status, snippet => snippet is { SnippetName: "TEST_REGISTER_A", IsArmed: false });
        Assert.Contains(status, snippet => snippet is { SnippetName: "TEST_REGISTER_B", IsArmed: false });
    }

    [Fact]
    public void TrySetArmedRejectsUnknownSnippet()
    {
        Assert.False(DemoBreakpoints.TrySetArmed("TEST_UNKNOWN_SNIPPET", armed: true));
    }

    [Fact]
    public void TrySetArmedArmsRegisteredSnippet()
    {
        DemoBreakpoints.Register("TEST_ARM");

        Assert.True(DemoBreakpoints.TrySetArmed("TEST_ARM", armed: true));
        Assert.Contains(DemoBreakpoints.GetStatus(), snippet => snippet is { SnippetName: "TEST_ARM", IsArmed: true });

        Assert.True(DemoBreakpoints.TrySetArmed("TEST_ARM", armed: false));
        Assert.Contains(DemoBreakpoints.GetStatus(), snippet => snippet is { SnippetName: "TEST_ARM", IsArmed: false });
    }

    [Fact]
    public void TryConsumeIsOneShot()
    {
        DemoBreakpoints.Register("TEST_CONSUME");
        DemoBreakpoints.TrySetArmed("TEST_CONSUME", armed: true);

        Assert.True(DemoBreakpoints.TryConsume("TEST_CONSUME"));
        Assert.False(DemoBreakpoints.TryConsume("TEST_CONSUME"));
        Assert.Contains(DemoBreakpoints.GetStatus(), snippet => snippet is { SnippetName: "TEST_CONSUME", IsArmed: false });
    }

    [Fact]
    public void TryConsumeReturnsFalseWhenDisarmed()
    {
        DemoBreakpoints.Register("TEST_DISARMED");

        Assert.False(DemoBreakpoints.TryConsume("TEST_DISARMED"));
    }

    [Fact]
    public void PauseWithoutDebuggerLeavesSnippetArmed()
    {
        DemoBreakpoints.Register("TEST_PAUSE_NO_DEBUGGER");
        DemoBreakpoints.TrySetArmed("TEST_PAUSE_NO_DEBUGGER", armed: true);

        // Test hosts run without an attached debugger, so Pause must not consume or break.
        DemoBreakpoints.Pause("TEST_PAUSE_NO_DEBUGGER");

        Assert.Contains(
            DemoBreakpoints.GetStatus(),
            snippet => snippet is { SnippetName: "TEST_PAUSE_NO_DEBUGGER", IsArmed: true });
    }
}
