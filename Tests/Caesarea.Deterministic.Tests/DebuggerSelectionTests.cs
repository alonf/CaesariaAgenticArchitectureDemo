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
    public async Task IdentificationIsAskedAtMostEveryFifteenSecondsAndAgainAfterADetach()
    {
        // An IDE that cannot tell says so every time; asking it on every poll would spawn a
        // helper per poll. Once the service reports no debugger, a later attachment is asked anew.
        var time = new TestTimeProvider();
        var visualStudio = new FakeDebuggerAdapter("visualstudio", "Visual Studio 2026", holds: false);
        var selection = new DebuggerSelection([new FakeDebuggerAdapter("vscode", "VS Code", null), visualStudio], time);

        await selection.IdentifyHolderAsync(Process, 4242, TestContext.Current.CancellationToken);
        await selection.IdentifyHolderAsync(Process, 4242, TestContext.Current.CancellationToken);
        Assert.Equal(1, visualStudio.Asked);
        Assert.Equal(DebuggerAttachmentKind.AttachedOutside, selection.Describe(Process, serviceReportsAttached: true).Kind);

        time.Advance(TimeSpan.FromSeconds(16));
        await selection.IdentifyHolderAsync(Process, 4242, TestContext.Current.CancellationToken);
        Assert.Equal(2, visualStudio.Asked);

        selection.RecordObservation(Process, 4242, attached: false);
        visualStudio.Holds = true;
        await selection.IdentifyHolderAsync(Process, 4242, TestContext.Current.CancellationToken);
        Assert.Equal(3, visualStudio.Asked);
        Assert.Equal("visualstudio", selection.Describe(Process, serviceReportsAttached: true).Holder?.Id);
    }

    [Fact]
    public async Task AFreshHoldIsTrustedForFifteenSecondsThenReCheckedAndCorrected()
    {
        // VS Code let go in its own window and Visual Studio attached, both between two polls:
        // the record says VS Code until the next check, when the IDE that says it holds the
        // process wins.
        var time = new TestTimeProvider();
        var visualStudio = new FakeDebuggerAdapter("visualstudio", "Visual Studio 2026", holds: true);
        var selection = new DebuggerSelection([new FakeDebuggerAdapter("vscode", "VS Code", null), visualStudio], time);
        selection.RecordAttached(Process, "vscode", 4242);

        await selection.IdentifyHolderAsync(Process, 4242, TestContext.Current.CancellationToken);
        Assert.Equal(0, visualStudio.Asked);
        Assert.Equal("vscode", selection.HeldBy(Process)!.AdapterId);

        time.Advance(TimeSpan.FromSeconds(16));
        await selection.IdentifyHolderAsync(Process, 4242, TestContext.Current.CancellationToken);
        Assert.Equal(1, visualStudio.Asked);
        Assert.Equal("visualstudio", selection.HeldBy(Process)!.AdapterId);
    }

    [Fact]
    public async Task AHoldIsDroppedWhenItsIdeSaysItNoLongerHoldsTheProcess()
    {
        var time = new TestTimeProvider();
        var visualStudio = new FakeDebuggerAdapter("visualstudio", "Visual Studio 2026", holds: false);
        var selection = new DebuggerSelection([new FakeDebuggerAdapter("vscode", "VS Code", null), visualStudio], time);
        selection.RecordAttached(Process, "visualstudio", 4242);

        time.Advance(TimeSpan.FromSeconds(16));
        await selection.IdentifyHolderAsync(Process, 4242, TestContext.Current.CancellationToken);

        Assert.Null(selection.HeldBy(Process));
        Assert.Equal(DebuggerAttachmentKind.AttachedOutside, selection.Describe(Process, serviceReportsAttached: true).Kind);
    }

    [Fact]
    public async Task ARestartedServiceDropsTheOldHoldAndIsAskedAboutAtOnce()
    {
        // The Operations Agent restarts under a new process id with Visual Studio on it; the VS
        // Code hold on the old process must not direct Detach at the wrong IDE, or the old id.
        var visualStudio = new FakeDebuggerAdapter("visualstudio", "Visual Studio 2026", holds: true);
        var selection = new DebuggerSelection([new FakeDebuggerAdapter("vscode", "VS Code", null), visualStudio]);
        selection.RecordAttached(Process, "vscode", 111);

        selection.RecordObservation(Process, 222, attached: true);
        Assert.Null(selection.HeldBy(Process));

        await selection.IdentifyHolderAsync(Process, 222, TestContext.Current.CancellationToken);
        var hold = selection.HeldBy(Process);
        Assert.NotNull(hold);
        Assert.Equal("visualstudio", hold.AdapterId);
        Assert.Equal(222, hold.ProcessId);
    }

    [Fact]
    public void ARestartDropsAHoldRecordedWithoutAProcessId()
    {
        // The hold carries no id when the service reported none at attach time. It is still the
        // old process's hold, and must not survive that process.
        var selection = CreateSelection();
        selection.RecordAttached(Process, "vscode");
        selection.RecordObservation(Process, 111, attached: true);
        Assert.Equal("vscode", selection.HeldBy(Process)?.AdapterId);

        selection.RecordObservation(Process, 222, attached: true);

        Assert.Null(selection.HeldBy(Process));
    }

    [Fact]
    public void AnAttachRecordedForTheNewProcessSurvivesItsFirstReport()
    {
        // Attach, then the service's first report under the new id: the fresh hold names that
        // very process, so the restart rule must not erase it.
        var selection = CreateSelection();
        selection.RecordObservation(Process, 111, attached: false);
        selection.RecordAttached(Process, "visualstudio", 222);

        selection.RecordObservation(Process, 222, attached: true);

        Assert.Equal("visualstudio", selection.HeldBy(Process)?.AdapterId);
        Assert.Equal(222, selection.HeldBy(Process)?.ProcessId);
    }

    [Fact]
    public async Task ParallelPollsAttachesAndReportsLeaveConsistentState()
    {
        // Four browser tabs polling while attaches, detaches and service reports interleave:
        // the point is that nothing throws, nothing deadlocks, and the end state is exactly what
        // the last operation said - not a torn mixture of the two dictionaries.
        var visualStudio = new FakeDebuggerAdapter("visualstudio", "Visual Studio 2026", holds: true);
        var selection = new DebuggerSelection([new FakeDebuggerAdapter("vscode", "VS Code", null), visualStudio]);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        var pollers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                selection.RecordObservation(Process, 111, attached: true);
                await selection.IdentifyHolderAsync(Process, 111, CancellationToken.None);
                selection.Describe(Process, serviceReportsAttached: true);
            }
        }));

        var operators = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                if (selection.TryBeginOperation(Process))
                {
                    selection.RecordAttached(Process, "vscode", 111);
                    selection.RecordDetached(Process);
                    selection.EndOperation(Process);
                }
            }
        }));

        await Task.WhenAll(pollers.Concat(operators));

        // Settle: one last operation has the final word, and a check afterwards agrees with it.
        Assert.True(selection.TryBeginOperation(Process));
        selection.RecordAttached(Process, "vscode", 111);
        selection.EndOperation(Process);
        Assert.Equal("vscode", selection.HeldBy(Process)?.AdapterId);
        Assert.False(selection.IsBusy(Process));
    }

    [Fact]
    public async Task NoReCheckWhileAnOperationIsInFlight()
    {
        var time = new TestTimeProvider();
        var visualStudio = new FakeDebuggerAdapter("visualstudio", "Visual Studio 2026", holds: true);
        var selection = new DebuggerSelection([new FakeDebuggerAdapter("vscode", "VS Code", null), visualStudio], time);
        selection.RecordAttached(Process, "vscode", 4242);
        time.Advance(TimeSpan.FromSeconds(16));

        Assert.True(selection.TryBeginOperation(Process));
        await selection.IdentifyHolderAsync(Process, 4242, TestContext.Current.CancellationToken);

        Assert.Equal(0, visualStudio.Asked);
        Assert.Equal("vscode", selection.HeldBy(Process)!.AdapterId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ADelayedReCheckCannotChangeANewerAttach(bool oldAnswer, bool operationCompleted)
    {
        // An old "no" must not erase the new hold; an old "yes" must not replace it.
        // Both stay stale even after the newer operation has finished and IsBusy is false.
        var time = new TestTimeProvider();
        var reply = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var visualStudio = new FakeDebuggerAdapter("visualstudio", "Visual Studio 2026", null) { PendingAnswer = reply.Task };
        var selection = new DebuggerSelection([new FakeDebuggerAdapter("vscode", "VS Code", null), visualStudio], time);
        var adapterId = oldAnswer ? "vscode" : "visualstudio";
        selection.RecordAttached(Process, adapterId, 111);
        time.Advance(TimeSpan.FromSeconds(16));

        var pending = selection.IdentifyHolderAsync(Process, 111, TestContext.Current.CancellationToken);
        Assert.Equal(1, visualStudio.Asked);
        Assert.False(pending.IsCompleted);

        // An IDE status read must not hold up a presenter trying to attach or detach.
        Assert.True(selection.TryBeginOperation(Process));
        selection.RecordAttached(Process, adapterId, 222);
        var newerHold = selection.HeldBy(Process);

        if (operationCompleted)
        {
            selection.EndOperation(Process);
        }

        reply.SetResult(oldAnswer);
        await pending;

        Assert.Same(newerHold, selection.HeldBy(Process));
        Assert.Equal(adapterId, selection.Describe(Process, serviceReportsAttached: true).Holder?.Id);
        selection.EndOperation(Process);
    }

    [Fact]
    public async Task ADelayedIdentificationCannotUndoACompletedDetach()
    {
        var reply = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var visualStudio = new FakeDebuggerAdapter("visualstudio", "Visual Studio 2026", null) { PendingAnswer = reply.Task };
        var selection = new DebuggerSelection([visualStudio]);
        selection.RecordObservation(Process, 111, attached: true);

        var pending = selection.IdentifyHolderAsync(Process, 111, TestContext.Current.CancellationToken);
        Assert.Equal(1, visualStudio.Asked);
        Assert.True(selection.TryBeginOperation(Process));
        selection.RecordDetached(Process);
        selection.EndOperation(Process);

        reply.SetResult(true);
        await pending;

        Assert.Null(selection.HeldBy(Process));
    }

    [Theory]
    [InlineData(222, true)]
    [InlineData(111, false)]
    public async Task AChangedServiceReportInvalidatesAPendingIdentification(int processId, bool attached)
    {
        // A restart or a report of no debugger outranks an IDE answer about the earlier state,
        // even when the switchboard had not identified a holder yet.
        var reply = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var visualStudio = new FakeDebuggerAdapter("visualstudio", "Visual Studio 2026", true) { PendingAnswer = reply.Task };
        var selection = new DebuggerSelection([visualStudio]);
        selection.RecordObservation(Process, 111, attached: true);

        var pending = selection.IdentifyHolderAsync(Process, 111, TestContext.Current.CancellationToken);
        Assert.Equal(1, visualStudio.Asked);
        selection.RecordObservation(Process, processId, attached);
        reply.SetResult(true);
        await pending;

        Assert.Null(selection.HeldBy(Process));

        // The invalidated check must not throttle identification of the new attachment.
        visualStudio.PendingAnswer = null;
        selection.RecordObservation(Process, processId, attached: true);
        await selection.IdentifyHolderAsync(Process, processId, TestContext.Current.CancellationToken);

        Assert.Equal(2, visualStudio.Asked);
        Assert.Equal(processId, selection.HeldBy(Process)?.ProcessId);
    }

    [Fact]
    public async Task ANewerReCheckSupersedesAnOlderAnswer()
    {
        var time = new TestTimeProvider();
        var reply = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var visualStudio = new FakeDebuggerAdapter("visualstudio", "Visual Studio 2026", true) { PendingAnswer = reply.Task };
        var selection = new DebuggerSelection([visualStudio], time);
        selection.RecordAttached(Process, "visualstudio", 111);
        time.Advance(TimeSpan.FromSeconds(16));

        var pending = selection.IdentifyHolderAsync(Process, 111, TestContext.Current.CancellationToken);
        Assert.Equal(1, visualStudio.Asked);

        // The helper can take longer than the refresh interval; the later check's answer wins.
        time.Advance(TimeSpan.FromSeconds(16));
        visualStudio.PendingAnswer = null;
        await selection.IdentifyHolderAsync(Process, 111, TestContext.Current.CancellationToken);
        Assert.Equal(2, visualStudio.Asked);

        reply.SetResult(false);
        await pending;

        Assert.Equal("visualstudio", selection.HeldBy(Process)?.AdapterId);
    }

    [Fact]
    public async Task AnUnchangedServiceReportDoesNotDiscardAPendingIdentification()
    {
        var reply = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var visualStudio = new FakeDebuggerAdapter("visualstudio", "Visual Studio 2026", true) { PendingAnswer = reply.Task };
        var selection = new DebuggerSelection([visualStudio]);
        selection.RecordObservation(Process, 111, attached: true);

        var pending = selection.IdentifyHolderAsync(Process, 111, TestContext.Current.CancellationToken);
        Assert.Equal(1, visualStudio.Asked);

        // Regular polling must not starve an IDE whose answer takes longer than one poll.
        selection.RecordObservation(Process, 111, attached: true);
        reply.SetResult(true);
        await pending;

        Assert.Equal("visualstudio", selection.HeldBy(Process)?.AdapterId);
        Assert.Equal(111, selection.HeldBy(Process)?.ProcessId);
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

        public Task<bool?>? PendingAnswer { get; set; }

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
            return PendingAnswer ?? Task.FromResult(Holds);
        }
    }
}
