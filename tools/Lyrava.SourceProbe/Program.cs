using System.Net;
using Lyrava.Sources.YouTube;

if (args.Length is < 2 or > 4 || args[0] is not ("search" or "resolve" or "probe" or "journey"))
{
    Console.Error.WriteLine("Usage: source-probe search <query> [--proxy] [--visionos] | resolve <video-id> [--proxy] [--visionos] | probe <video-id> [--proxy] [--visionos] | journey <query> [--proxy] [--visionos]");
    return 64;
}

var useProxy = false;
var profile = YouTubePlayerProfile.WebRemix;
for (var optionIndex = 2; optionIndex < args.Length; optionIndex++)
{
    switch (args[optionIndex])
    {
        case "--proxy":
            if (useProxy) { Console.Error.WriteLine("probe=InvalidInput"); return 64; }
            useProxy = true;
            break;
        case "--visionos":
            if (profile != YouTubePlayerProfile.WebRemix) { Console.Error.WriteLine("probe=InvalidInput"); return 64; }
            profile = YouTubePlayerProfile.VisionOs;
            break;
        default:
            Console.Error.WriteLine("probe=InvalidInput");
            return 64;
    }
}

using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    deadline.Cancel();
};
Console.CancelKeyPress += cancelHandler;
try
{
    // 直连与显式代理分别留证；不继承操作系统代理来掩盖实际网络路线。
    using var handler = new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseProxy = useProxy,
        Proxy = useProxy ? new WebProxy("http://127.0.0.1:7890") : null,
        ConnectTimeout = TimeSpan.FromSeconds(5)
    };
    using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    var source = new YouTubeMusicSource(http, new YouTubeSourceOptions { PlayerProfile = profile });
    Console.WriteLine($"network={(useProxy ? "local-proxy" : "direct")}; authentication=anonymous; client={(profile == YouTubePlayerProfile.VisionOs ? "VISIONOS" : "WEB_REMIX")}");
    var videoId = args[1];
    if (args[0] is "search" or "journey")
    {
        var result = await source.SearchAsync(args[1], deadline.Token);
        Console.WriteLine($"search={result.Failure}; results={result.Value?.Count ?? 0}");
        if (!result.IsSuccess || args[0] == "search")
        {
            return result.IsSuccess ? 0 : 2;
        }

        if (result.Value!.Count == 0)
        {
            Console.WriteLine("selected=0; resolve=NOT_RUN; range=NOT_RUN");
            return 2;
        }

        // 只选首条，不在失败时无限扫描或绕过源站限制。
        videoId = result.Value[0].Id;
        Console.WriteLine("selected=1");
    }

    var resolved = await source.ResolveAsync(videoId, deadline.Token);
    Console.WriteLine($"resolve={resolved.Failure}; playability={resolved.Playability}");
    if (!resolved.IsSuccess)
    {
        return 2;
    }

    Console.WriteLine($"format={resolved.Value!.MimeType}; codec={resolved.Value.Codec}; bitrate={resolved.Value.Bitrate}; expires=known");
    if (args[0] == "resolve")
    {
        return 0;
    }

    var probe = await source.ProbeRangeAsync(resolved.Value, deadline.Token);
    Console.WriteLine($"range={probe.Failure}; bytes={probe.Value?.BytesRead ?? 0}; native-seek=NOT_RUN; background=NOT_RUN");
    return probe.IsSuccess ? 0 : 2;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("probe=CancelledOrDeadline");
    return 3;
}
catch (ArgumentException)
{
    Console.Error.WriteLine("probe=InvalidInput");
    return 64;
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
}
