using DemoControl.Web.Services;

namespace Caesarea.Deterministic.Tests;

/// <summary>
/// The switchboard remembers which IDE it put on each service, asks the IDEs about attachments it
/// did not make, and claims a service for one operation at a time, so a row never stacks a second
/// debugger on a process and always names the one to let go. The service's own report stays the
/// truth: a debugger it does not see is not attached.
/// </summary>
public sealed class DebuggerSelectionTests
{
    private const string Process = "OperationsAgent.Api.exe";

    private static DebuggerSelection CreateSelection(bool? vsCodeHolds = null, bool? visualStudioHolds = null) =>
        new([new FakeDebuggerAdapter("vscode", "VS Code", vsCodeHolds), new FakeDebuggerAdapter("visualstudio", "Visual Studio 2026", visualStudioHolds)]);

    [Fact]
    public void AdaptersAreFoundByIdInPresentationOrder()
    {
        var selection = CreateSelection();

        Assert.Equal(["vscode", "visualstudio"], selection.Adapters.Select(adapter => adapter.Id));
        Assert.Equal("Visual Studio 2026", selection.Find("visualstudio")?.DisplayName);
        Assert.Null(selection.Find("rider"));
    }

    [Fact]
    public void AServiceWithoutADebuggerOffersAttach()
    {
        var selection = CreateSelection();

        var attachment = selection.Describe(Process, serviceReportsAttached: false);

        Assert.Equal(DebuggerAttachmentKind.NotAttached, attachment.Kind);
        Assert.Null(attachment.Holder);
    }

    [Fact]
    public void TheIdeThatAttachedIsTheOneToDetach()
    {
        var selection = CreateSelection();
        selection.RecordAttached(Process, "visualstudio", 4242);

        var attachment = selection.Describe(Process, serviceReportsAttached: true);

        Assert.Equal(DebuggerAttachmentKind.Attached, attachment.Kind);
        Assert.Equal("visualstudio", attachment.Holder?.Id);
        Assert.Equal(4242, selection.HeldBy(Process)!.ProcessId);
    }

    [Fact]
    public void DescribingIsReadOnlySoAStaleTabCannotEraseAHold()
    {
        // Tab A attached Visual Studio; tab B renders an older "not attached" snapshot. Rendering
        // must not forget who holds the service, or B's next Detach would go to the wrong IDE.
        var selection = CreateSelection();
        selection.RecordAttached(Process, "visualstudio", 4242);

        Assert.Equal(DebuggerAttachmentKind.NotAttached, selection.Describe(Process, serviceReportsAttached: false).Kind);

        var fresh = selection.Describe(Process, serviceReportsAttached: true);
        Assert.Equal(DebuggerAttachmentKind.Attached, fresh.Kind);
        Assert.Equal("visualstudio", fresh.Holder?.Id);
    }

    [Fact]
    public void AFreshReportOfNoDebuggerForgetsTheHoldUnlessAnOperationIsInFlight()
    {
        var selection = CreateSelection();
        selection.RecordAttached(Process, "vscode", 4242);

        // Mid-operation the poll may simply be ahead of the IDE: the record is the operation's.
        Assert.True(selection.TryBeginOperation(Process));
        selection.RecordObservation(Process, 4242, attached: false);
        Assert.NotNull(selection.HeldBy(Process));
        selection.EndOperation(Process);

        // At rest, the service's own word wins.
        selection.RecordObservation(Process, 4242, attached: false);
        Assert.Null(selection.HeldBy(Process));
        Assert.Equal(DebuggerAttachmentKind.AttachedOutside, selection.Describe(Process, serviceReportsAttached: true).Kind);
    }

    [Fact]
    public async Task AnAttachmentTheSwitchboardDidNotMakeIsIdentifiedByAskingTheIdes()
    {
        // Visual Studio can say whether it debugs a process; VS Code cannot. A debugger the
        // presenter attached from Visual Studio itself is still named on the row.
        var selection = CreateSelection(vsCodeHolds: null, visualStudioHolds: true);

        await selection.IdentifyHolderAsync(Process, 4242, TestContext.Current.CancellationToken);

        var attachment = selection.Describe(Process, serviceReportsAttached: true);
        Assert.Equal(DebuggerAttachmentKind.Attached, attachment.Kind);
        Assert.Equal("visualstudio", attachment.Holder?.Id);
        Assert.Equal(4242, selection.HeldBy(Process)!.ProcessId);
    }

    [Fact]
    public async Task IdentificationIsAskedOncePerProcessAndAgainAfterADetach()
    {
        // An IDE that cannot tell says so every time; asking it on every poll would spawn a
        // helper per poll. Once the service reports no debugger, a later attachment is asked anew.
        var visualStudio = new FakeDebuggerAdapter("visualstudio", "Visual Studio 2026", holds: false);
        var selection = new DebuggerSelection([new FakeDebuggerAdapter("vscode", "VS Code", null), visualStudio]);

        await selection.IdentifyHolderAsync(Process, 4242, TestContext.Current.CancellationToken);
        await selection.IdentifyHolderAsync(Process, 4242, TestContext.Current.CancellationToken);
        Assert.Equal(1, visualStudio.Asked);
        Assert.Equal(DebuggerAttachmentKind.AttachedOutside, selection.Describe(Process, serviceReportsAttached: true).Kind);

        selection.RecordObservation(Process, 4242, attached: false);
        visualStudio.Holds = true;
        await selection.IdentifyHolderAsync(Process, 4242, TestContext.Current.CancellationToken);
        Assert.Equal(2, visualStudio.Asked);
        Assert.Equal("visualstudio", selection.Describe(Process, serviceReportsAttached: true).Holder?.Id);
    }

    [Fact]
    public async Task AKnownHoldIsNotReIdentified()
    {
        var visualStudio = new FakeDebuggerAdapter("visualstudio", "Visual Studio 2026", holds: true);
        var selection = new DebuggerSelection([new FakeDebuggerAdapter("vscode", "VS Code", null), visualStudio]);
        selection.RecordAttached(Process, "vscode", 4242);

        await selection.IdentifyHolderAsync(Process, 4242, TestContext.Current.CancellationToken);

        Assert.Equal(0, visualStudio.Asked);
        Assert.Equal("vscode", selection.HeldBy(Process)!.AdapterId);
    }

    [Fact]
    public void ADebuggerNobodyCanNameIsReportedAsOutside()
    {
        var selection = CreateSelection();

        var attachment = selection.Describe(Process, serviceReportsAttached: true);

        Assert.Equal(DebuggerAttachmentKind.AttachedOutside, attachment.Kind);
        Assert.Null(attachment.Holder);
    }

    [Fact]
    public void OneOperationPerServiceAcrossEveryTab()
    {
        // Two tabs clicking Attach at once must not race two IDEs onto one process: the second
        // click finds the service claimed and does nothing.
        var selection = CreateSelection();

        Assert.True(selection.TryBeginOperation(Process));
        Assert.True(selection.IsBusy(Process));
        Assert.False(selection.TryBeginOperation("operationsagent.api.exe"));
        Assert.True(selection.TryBeginOperation("EnergyHub.Api.exe"));

        selection.EndOperation(Process);
        Assert.False(selection.IsBusy(Process));
        Assert.True(selection.TryBeginOperation(Process));
    }

    [Fact]
    public void TheLastReportOutlivesAServiceThatStopsAnswering()
    {
        // A service paused at a breakpoint answers nothing; the row still needs the process id and
        // the fact that a debugger is on it to offer Detach.
        var selection = CreateSelection();

        Assert.Null(selection.LastObservation(Process));

        selection.RecordObservation(Process, 4242, attached: true);
        var last = selection.LastObservation(Process);

        Assert.NotNull(last);
        Assert.Equal(4242, last.ProcessId);
        Assert.True(last.Attached);

        selection.RecordObservation(Process, 4242, attached: false);
        Assert.False(selection.LastObservation(Process)!.Attached);
    }

    [Fact]
    public void RecordsAreKeyedByProcessRegardlessOfCase()
    {
        var selection = CreateSelection();
        selection.RecordAttached("operationsagent.api.exe", "vscode");

        Assert.Equal("vscode", selection.Describe(Process, serviceReportsAttached: true).Holder?.Id);

        selection.RecordDetached(Process);
        Assert.Equal(DebuggerAttachmentKind.AttachedOutside, selection.Describe(Process, serviceReportsAttached: true).Kind);
    }

    private sealed class FakeDebuggerAdapter(string id, string displayName, bool? holds) : IDebuggerAdapter
    {
        public string Id => id;

        public string DisplayName => displayName;

        public bool? Holds { get; set; } = holds;

        public int Asked { get; private set; }

        public Task<DebuggerAvailability> GetAvailabilityAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new DebuggerAvailability(true, "ready"));

        public Task<DebuggerCommandResult> SetUpAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new DebuggerCommandResult(true, "nothing to do"));

        public Task<DebuggerCommandResult> AttachAsync(DebuggerTarget target, CancellationToken cancellationToken) =>
            Task.FromResult(new DebuggerCommandResult(true, "attached"));

        public Task<DebuggerCommandResult> DetachAsync(DebuggerTarget target, CancellationToken cancellationToken) =>
            Task.FromResult(new DebuggerCommandResult(true, "detached"));

        public Task<bool?> IsAttachedAsync(DebuggerTarget target, CancellationToken cancellationToken)
        {
            Asked++;
            return Task.FromResult(Holds);
        }
    }
}
