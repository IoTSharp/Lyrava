#if WINDOWS
using Lyrava.App.Windows;
#endif

namespace Lyrava.App;

#if WINDOWS
public sealed class App(WindowsRootPage page, IMediaPlaybackService playback, WindowsQueueCoordinator queue) : Application
#else
public sealed class App(MediaProbePage page, IMediaPlaybackService playback) : Application
#endif
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(page);
        // Closing the platform window ends this probe session. A page disappearing does not.
        window.Destroying += (_, _) => playback.Dispose();
#if WINDOWS
        window.Destroying += (_, _) => queue.Dispose();
#endif
        return window;
    }
}
