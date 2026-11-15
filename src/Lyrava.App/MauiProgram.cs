using Microsoft.Extensions.DependencyInjection;
using Lyrava.Sources.YouTube;

#if IOS
using Lyrava.App.Platforms.iOS;
#elif WINDOWS
using Lyrava.App.Platforms.Windows;
using Lyrava.App.Windows;
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
        builder.Services.AddSingleton<YouTubeMusicSource>(_ =>
        {
            var client = new HttpClient(new HttpClientHandler { UseCookies = false })
            {
                Timeout = TimeSpan.FromSeconds(12)
            };
            return new YouTubeMusicSource(client);
        });
        builder.Services.AddSingleton<WindowsSearchViewModel>();
        builder.Services.AddSingleton<WindowsSearchPage>();
        builder.Services.AddSingleton<WindowsQueueCoordinator>();
        builder.Services.AddSingleton<WindowsQueuePage>();
        builder.Services.AddSingleton<WindowsRootPage>();
#endif
        builder.Services.AddSingleton<MediaProbePage>();
        return builder.Build();
    }
}
