namespace Lyrava.App;

public sealed class App(MediaProbePage page, IMediaPlaybackService playback) : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(page);
        // Closing the platform window ends this probe session. A page disappearing does not.
        window.Destroying += (_, _) => playback.Dispose();
        return window;
    }
}
