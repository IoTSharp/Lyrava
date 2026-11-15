using Microsoft.Extensions.DependencyInjection;

#if IOS
using Lyrava.App.Platforms.iOS;
#elif WINDOWS
using Lyrava.App.Platforms.Windows;
#endif

namespace Lyrava.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
#if IOS
#pragma warning disable CA1416
        builder.Services.AddSingleton<IMediaPlaybackService, IosPlaybackService>();
#pragma warning restore CA1416
#elif WINDOWS
        builder.Services.AddSingleton<IMediaPlaybackService, WindowsPlaybackService>();
#endif
        builder.Services.AddSingleton<MediaProbePage>();
        return builder.Build();
    }
}
