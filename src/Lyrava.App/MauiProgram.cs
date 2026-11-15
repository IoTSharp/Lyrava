using Lyrava.App.Platforms.iOS;
using Microsoft.Extensions.DependencyInjection;

namespace Lyrava.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        builder.Services.AddSingleton<IosPlaybackService>();
        builder.Services.AddSingleton<MediaProbePage>();
        return builder.Build();
    }
}
