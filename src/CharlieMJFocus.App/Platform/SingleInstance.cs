using System;
using System.Threading;

namespace CharlieMJFocus.App.Platform;

/// <summary>
/// Ensures only one copy runs. Two copies would fight over the same database and timer.
/// A second launch asks the first copy to show its window, then exits.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\CharlieMJFocus.SingleInstance";
    private const string ActivateEventName = @"Local\CharlieMJFocus.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateEvent;
    private readonly bool _isFirst;
    private readonly CancellationTokenSource _cancellation = new();

    public SingleInstance()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out _isFirst);
        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
    }

    /// <summary>True when this process is the first (and therefore only) running copy.</summary>
    public bool IsFirst => _isFirst;

    /// <summary>Tells the already-running copy to come to the front.</summary>
    public void SignalExisting() => _activateEvent.Set();

    /// <summary>Starts a background thread that calls <paramref name="onActivate"/> when another launch happens.</summary>
    public void Listen(Action onActivate)
    {
        var thread = new Thread(() =>
        {
            var handles = new[] { _activateEvent, _cancellation.Token.WaitHandle };

            while (WaitHandle.WaitAny(handles) == 0)
            {
                onActivate();
            }
        })
        {
            IsBackground = true,
            Name = "Single-instance listener"
        };

        thread.Start();
    }

    public void Dispose()
    {
        _cancellation.Cancel();

        if (_isFirst)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _activateEvent.Dispose();
        _cancellation.Dispose();
    }
}
