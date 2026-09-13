namespace NanoUint;

/// <summary>Abstraction for posting work to the UI thread.</summary>
public interface IDispatcher
{
    /// <summary>Posts an action to run at idle priority on the UI thread.</summary>
    void BeginInvoke(Action action);
}
