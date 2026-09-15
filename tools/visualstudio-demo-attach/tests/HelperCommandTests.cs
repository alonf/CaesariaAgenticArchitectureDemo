namespace VisualStudioDemoAttach.Tests;

public sealed class HelperCommandTests
{
    [Fact]
    public void AFullAttachCommandLineParses()
    {
        var command = HelperCommand.Parse(["attach", "--solution", @"C:\Dev\Caesarea\Caesarea.slnx", "--process-name", "OperationsAgent.Api.exe", "--process-id", "4242"]);

        Assert.Null(command.Error);
        Assert.Equal("attach", command.Operation);
        Assert.Equal(@"C:\Dev\Caesarea\Caesarea.slnx", command.Solution);
        Assert.Equal("OperationsAgent.Api.exe", command.ProcessName);
        Assert.Equal(4242, command.ProcessId);
    }

    [Fact]
    public void TheInstancePinIsParsedLikeAProcessId()
    {
        var command = HelperCommand.Parse(["detach", "--process-id", "4242", "--instance-pid", "31240"]);

        Assert.Null(command.Error);
        Assert.Equal(31240, command.InstanceProcessId);
        Assert.Equal("'x' is not a process id.", HelperCommand.Parse(["status", "--instance-pid", "x"]).Error);
    }

    [Fact]
    public void OperationsAreCaseInsensitiveAndOptionsOptionalForStatus()
    {
        var command = HelperCommand.Parse(["STATUS"]);

        Assert.Null(command.Error);
        Assert.Equal("status", command.Operation);
        Assert.Null(command.Solution);
    }

    [Theory]
    [InlineData(new string[0], "An operation is required.")]
    [InlineData(new[] { "reboot" }, "Unknown operation 'reboot'.")]
    [InlineData(new[] { "status", "--solution" }, "Option '--solution' needs a value.")]
    [InlineData(new[] { "status", "--verbose", "yes" }, "Unknown option '--verbose'.")]
    [InlineData(new[] { "attach", "--process-id", "abc" }, "'abc' is not a process id.")]
    [InlineData(new[] { "attach", "--process-id", "0" }, "'0' is not a process id.")]
    [InlineData(new[] { "attach", "--solution", "x.slnx" }, "'attach' needs --process-id or --process-name.")]
    [InlineData(new[] { "detach" }, "'detach' needs --process-id or --process-name.")]
    public void ABadCommandLineIsReportedNotThrown(string[] args, string expectedError)
    {
        var command = HelperCommand.Parse(args);

        Assert.Equal(expectedError, command.Error);
        Assert.Equal("usage", command.Operation);
    }

    [Fact]
    public void TheTargetIsDescribedByWhatIsKnown()
    {
        Assert.Equal("OperationsAgent.Api.exe (PID 7)", new HelperCommand("attach", null, "OperationsAgent.Api.exe", 7, null).DescribeTarget());
        Assert.Equal("PID 7", new HelperCommand("attach", null, null, 7, null).DescribeTarget());
        Assert.Equal("OperationsAgent.Api.exe", new HelperCommand("attach", null, "OperationsAgent.Api.exe", null, null).DescribeTarget());
        Assert.Equal("the process", new HelperCommand("status", null, null, null, null).DescribeTarget());
    }
}
