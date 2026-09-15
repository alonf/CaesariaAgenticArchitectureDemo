using System.Runtime.InteropServices;

namespace VisualStudioDemoAttach;

/// <summary>
/// Keeps a busy Visual Studio from rejecting the helper's calls. Visual Studio answers automation
/// calls on its UI thread and refuses them with "call was rejected by callee" while it is
/// building, loading or painting; a registered message filter turns that refusal into a retry.
/// Requires an STA thread, which is why <c>Main</c> is marked <c>[STAThread]</c>.
/// </summary>
internal sealed class MessageFilter : IOleMessageFilter, IDisposable
{
    private const int ServerCallIsHandled = 0;
    private const int ServerCallRetryLater = 2;
    private const int PendingMessageWaitDefaultProcess = 2;
    private const int RetryAfterMilliseconds = 200;
    private const int CancelCall = -1;

    private MessageFilter()
    {
    }

    /// <summary>
    /// Registers a filter on the current thread. Dispose it to restore the previous filter.
    /// </summary>
    /// <returns>The registered filter.</returns>
    public static MessageFilter Register()
    {
        var filter = new MessageFilter();
        Marshal.ThrowExceptionForHR(NativeMethods.CoRegisterMessageFilter(filter, out _));
        return filter;
    }

    /// <inheritdoc />
    public int HandleInComingCall(int callType, IntPtr taskCaller, int tickCount, IntPtr interfaceInfo) => ServerCallIsHandled;

    /// <inheritdoc />
    public int RetryRejectedCall(IntPtr taskCallee, int tickCount, int rejectType) =>
        rejectType == ServerCallRetryLater ? RetryAfterMilliseconds : CancelCall;

    /// <inheritdoc />
    public int MessagePending(IntPtr taskCallee, int tickCount, int pendingType) => PendingMessageWaitDefaultProcess;

    /// <inheritdoc />
    public void Dispose()
    {
        // Restoring the previous filter is best effort on the way out; there is nothing left to
        // retry if it fails.
        _ = NativeMethods.CoRegisterMessageFilter(null, out _);
    }

    private static class NativeMethods
    {
        [DllImport("ole32.dll")]
        public static extern int CoRegisterMessageFilter(IOleMessageFilter? newFilter, out IOleMessageFilter? oldFilter);
    }
}

/// <summary>
/// The COM message filter contract (<c>IOleMessageFilter</c>).
/// </summary>
[ComImport]
[Guid("00000016-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOleMessageFilter
{
    /// <summary>
    /// Decides whether an incoming call is handled; always handled here.
    /// </summary>
    /// <param name="callType">The call type.</param>
    /// <param name="taskCaller">The caller task.</param>
    /// <param name="tickCount">Milliseconds since the call was made.</param>
    /// <param name="interfaceInfo">The interface being called.</param>
    /// <returns>A SERVERCALL value.</returns>
    [PreserveSig]
    public int HandleInComingCall(int callType, IntPtr taskCaller, int tickCount, IntPtr interfaceInfo);

    /// <summary>
    /// Decides what to do with a call the callee rejected: a delay in milliseconds to retry after, or -1 to give up.
    /// </summary>
    /// <param name="taskCallee">The callee task.</param>
    /// <param name="tickCount">Milliseconds since the call was made.</param>
    /// <param name="rejectType">Why it was rejected.</param>
    /// <returns>The retry delay, or -1.</returns>
    [PreserveSig]
    public int RetryRejectedCall(IntPtr taskCallee, int tickCount, int rejectType);

    /// <summary>
    /// Decides how to treat window messages while a call is pending.
    /// </summary>
    /// <param name="taskCallee">The callee task.</param>
    /// <param name="tickCount">Milliseconds since the call was made.</param>
    /// <param name="pendingType">The pending type.</param>
    /// <returns>A PENDINGMSG value.</returns>
    [PreserveSig]
    public int MessagePending(IntPtr taskCallee, int tickCount, int pendingType);
}
