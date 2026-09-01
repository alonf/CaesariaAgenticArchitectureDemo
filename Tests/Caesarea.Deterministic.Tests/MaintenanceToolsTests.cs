using OperationsAgent.Api.Services;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// The approved capability itself. The operator answers a question about a specific call, and
/// seconds pass before the model makes it - so what runs afterwards must be the call they saw,
/// filed only while the stage that offered the capability is still in force.
/// </summary>
public sealed class MaintenanceToolsTests
{
    [Fact]
    public void AnApprovedCallFilesTheWorkItemItDescribed()
    {
        var world = new MaintenanceWorld(DemoStage.ToolApproval);

        var answer = world.Tools.CreateMaintenanceWorkItem("l-417", "  Controller unresponsive after the override cleared.  ");

        var workItem = Assert.Single(world.WorkItems.Created);
        Assert.Equal("L-417", workItem.AssetId);
        Assert.Equal("Controller unresponsive after the override cleared.", workItem.Summary);
        Assert.Equal("tool-approval-corr", workItem.CorrelationId);
        Assert.Contains(workItem.WorkItemId, answer, StringComparison.Ordinal);
    }

    [Fact]
    public void AStageBelowToolApprovalFilesNothing()
    {
        var world = new MaintenanceWorld(DemoStage.Workflow);

        var answer = world.Tools.CreateMaintenanceWorkItem("L-417", "Controller unresponsive.");

        Assert.Empty(world.WorkItems.Created);
        Assert.Contains("no longer allows", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStageDowngradeCannotInterleaveWithTheWrite()
    {
        // The hazard the atomic guard closes: the stage check passes, the presenter steps back,
        // and the write lands anyway on a capability that has been withdrawn. The check and the
        // write share one critical section, so a downgrade waits for the write it raced.
        var world = new MaintenanceWorld(DemoStage.ToolApproval);
        using var downgradeStarted = new ManualResetEventSlim(false);
        Task? downgrade = null;

        world.WorkItems.OnCreate = () =>
        {
            downgrade = Task.Run(() =>
            {
                downgradeStarted.Set();
                world.MoveStageTo(DemoStage.Knowledge);
            });

            // Wait for the other thread to actually be running, so what the assertion below
            // observes is the lock rather than an unscheduled continuation.
            Assert.True(downgradeStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(downgrade.Wait(TimeSpan.FromMilliseconds(250)));
        };

        var answer = world.Tools.CreateMaintenanceWorkItem("L-417", "Controller unresponsive.");
        await downgrade!;

        Assert.Single(world.WorkItems.Created);
        Assert.Contains("filed", answer, StringComparison.Ordinal);
        // The downgrade was not lost - it landed the moment the write finished.
        Assert.Equal(DemoStage.Knowledge, world.StageGate.GetCurrent().Id);
    }

    [Theory]
    [InlineData("north-promenade")]
    [InlineData("L417")]
    [InlineData("L-1234567")]
    public void AnIdentifierTheCityDoesNotUseFilesNothing(string assetId)
    {
        var world = new MaintenanceWorld(DemoStage.ToolApproval);

        var answer = world.Tools.CreateMaintenanceWorkItem(assetId, "Controller unresponsive.");

        Assert.Empty(world.WorkItems.Created);
        Assert.Contains("not a canonical", answer, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOverLongSummaryIsRefusedRatherThanShortened()
    {
        // Silently truncating would file a work item whose text the operator never approved.
        var world = new MaintenanceWorld(DemoStage.ToolApproval);

        var answer = world.Tools.CreateMaintenanceWorkItem("L-417", new string('x', 501));

        Assert.Empty(world.WorkItems.Created);
        Assert.Contains("between 1 and 500", answer, StringComparison.Ordinal);
    }

    [Fact]
    public void ASummaryThatIsOnlyWhitespaceIsRefused()
    {
        var world = new MaintenanceWorld(DemoStage.ToolApproval);

        Assert.Throws<ArgumentException>(() => world.Tools.CreateMaintenanceWorkItem("L-417", "   "));
        Assert.Empty(world.WorkItems.Created);
    }

    [Fact]
    public void ASummaryThatFitsOnlyAfterTrimmingIsAccepted()
    {
        // The limit applies to what gets filed, not to the whitespace around it.
        var world = new MaintenanceWorld(DemoStage.ToolApproval);

        world.Tools.CreateMaintenanceWorkItem("L-417", $"  {new string('x', 500)}  ");

        Assert.Equal(500, Assert.Single(world.WorkItems.Created).Summary.Length);
    }

    private sealed class MaintenanceWorld
    {
        public MaintenanceWorld(DemoStage stage)
        {
            StageGate = new DemoStageGate(stage);
            WorkItems = new FakeWorkItemGateway();
            Tools = new MaintenanceTools(WorkItems, StageGate, "tool-approval-corr", NullLogger<MaintenanceTools>.Instance);
        }

        public DemoStageGate StageGate { get; }

        public FakeWorkItemGateway WorkItems { get; }

        public MaintenanceTools Tools { get; }

        public void MoveStageTo(DemoStage stage) =>
            StageGate.SetCurrent(new DemoStageStatus(stage, stage.ToString(), "Test stage", ["Test"], DateTimeOffset.UtcNow, "stage-corr"));
    }
}
