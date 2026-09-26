namespace NanoUint;

/// <summary>Abstraction for posting work to the UI thread.</summary>
public interface IDispatcher
{
    /// <summary>Posts an action to run at idle priority on the UI thread.</summary>
    void BeginInvoke(Action action);

    /// <summary>Schedules an action to run after a delay on the UI thread, then pauses the script.</summary>
    void Wait(TimeSpan delay, Action onComplete);
}
