using System.Windows.Threading;

namespace NanoUint.Rendering;

/// <summary>IDispatcher implementation backed by a WPF Window's Dispatcher.</summary>
internal sealed class WpfDispatcher : IDispatcher
{
    private readonly Dispatcher _dispatcher;

    public WpfDispatcher(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void BeginInvoke(Action action)
    {
        _dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, action);
    }

    public void Wait(TimeSpan delay, Action onComplete)
    {
        var timer = new DispatcherTimer { Interval = delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            onComplete();
        };
        timer.Start();
    }
}
