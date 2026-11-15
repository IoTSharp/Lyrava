using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Lyrava.Sources.YouTube;
using Xunit;

namespace Lyrava.Sources.YouTube.Tests;

public sealed class YouTubeMusicSourceTests
{
    private const string VideoId = "BaW_jenozKc";
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1800000000);

    [Fact]
    public async Task SearchAsync_MusicRows_ReturnsDeduplicatedTracksInOrder()
    {
        const string body = """
            {"contents":{"items":[
              {"musicResponsiveListItemRenderer":{"playlistItemData":{"videoId":"BaW_jenozKc"},"flexColumns":[{"musicResponsiveListItemFlexColumnRenderer":{"text":{"runs":[{"text":"First"}]}}}]}},
              {"videoRenderer":{"videoId":"jNQXAC9IVRw","title":{"simpleText":"Second"}}},
              {"videoRenderer":{"videoId":"BaW_jenozKc","title":{"simpleText":"Duplicate"}}}
            ]}}
            """;
        using var http = Client(_ => Json(body));
        var result = await new YouTubeMusicSource(http).SearchAsync("fixture");
        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "BaW_jenozKc", "jNQXAC9IVRw" }, result.Value!.Select(track => track.Id));
        Assert.Equal("First", result.Value![0].Title);
    }

    [Fact]
    public async Task SearchAsync_Request_UsesSourceGeneratedContextWithoutCredentials()
    {
        using var http = Client(async request =>
        {
            Assert.Equal("music.youtube.com", request.RequestUri!.Host);
            Assert.False(request.Headers.Contains("Authorization"));
            Assert.False(request.Headers.Contains("Cookie"));
            var requestBody = await request.Content!.ReadAsStringAsync();
            Assert.Contains("\"clientName\":\"WEB_REMIX\"", requestBody, StringComparison.Ordinal);
            Assert.Contains("\"query\":\"test\"", requestBody, StringComparison.Ordinal);
            return Json("{\"contents\":{}}");
        });
        Assert.True((await new YouTubeMusicSource(http).SearchAsync("test")).IsSuccess);
    }

    [Fact]
    public async Task ResolveAsync_DirectAac_SelectsHighestBitrateAndRedactsAddress()
    {
        using var http = Client(_ => Json(Player("""
            {"mimeType":"audio/webm; codecs=\"opus\"","url":"https://r1.googlevideo.com/videoplayback?expire=1800003600","bitrate":999999},
            {"mimeType":"audio/mp4; codecs=\"mp4a.40.2\"","url":"https://r1.googlevideo.com/videoplayback?expire=1800003600","bitrate":64000},
            {"mimeType":"audio/mp4; codecs=\"mp4a.40.2\"","url":"https://r2.googlevideo.com/videoplayback?expire=1800003600","bitrate":128000}
            """)));
        var result = await Source(http).ResolveAsync(VideoId);
        Assert.True(result.IsSuccess);
        Assert.Equal(128000, result.Value!.Bitrate);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1800003600), result.Value.ExpiresAt);
        Assert.DoesNotContain("googlevideo", result.Value.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("expire=", result.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"signatureCipher\":\"synthetic fixture only\",\"mimeType\":\"audio/mp4\"}", SourceFailure.SignatureRequired)]
    [InlineData("{\"mimeType\":\"audio/webm; codecs=opus\",\"url\":\"https://r1.googlevideo.com/videoplayback\"}", SourceFailure.FormatUnsupported)]
    [InlineData("{\"mimeType\":\"audio/mp4; codecs=mp4a.40.2\",\"url\":\"https://evil.invalid/videoplayback?expire=1800003600\"}", SourceFailure.FormatUnsupported)]
    [InlineData("{\"mimeType\":\"audio/mp4; codecs=mp4a.40.2\",\"url\":\"https://r1.googlevideo.com/videoplayback?expire=1700000000\"}", SourceFailure.Expired)]
    [InlineData("null", SourceFailure.ProtocolChanged)]
    public async Task ResolveAsync_UnsupportedOrExpiredResponse_ReturnsExplicitFailure(string format, SourceFailure expected)
    {
        using var http = Client(_ => Json(Player(format)));
        var result = await Source(http).ResolveAsync(VideoId);
        Assert.Equal(expected, result.Failure);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task ResolveAsync_LoginRequired_PreservesAuthenticationBoundary()
    {
        using var http = Client(_ => Json("{\"playabilityStatus\":{\"status\":\"LOGIN_REQUIRED\",\"reason\":\"must never log server reason\"}}"));
        var result = await Source(http).ResolveAsync(VideoId);
        Assert.Equal(SourceFailure.AuthenticationRequired, result.Failure);
        Assert.Equal(SourcePlayability.LoginRequired, result.Playability);
    }

    [Fact]
    public async Task ResolveAsync_VisionOsProfile_BootstrapsVisitorOnceAndUsesBoundedPlayerRequest()
    {
        var calls = new List<Uri>();
        using var http = Client(async request =>
        {
            calls.Add(request.RequestUri!);
            if (request.Method == HttpMethod.Get)
            {
                return Json(")]}'\n[[null,null,[[[null,null,null,null,null,null,null,null,null,null,null,null,null,\"visitor-fixture\"]]]]]");
            }

            Assert.Equal("www.youtube.com", request.RequestUri!.Host);
            var body = await request.Content!.ReadAsStringAsync();
            Assert.Contains("\"clientName\":\"VISIONOS\"", body, StringComparison.Ordinal);
            Assert.Contains("\"visitorData\":\"visitor-fixture\"", body, StringComparison.Ordinal);
            return Json(DirectPlayer());
        });

        var options = new YouTubeSourceOptions { PlayerProfile = YouTubePlayerProfile.VisionOs };
        var result = await new YouTubeMusicSource(http, options, new MutableTimeProvider(Now)).ResolveAsync(VideoId);
        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "www.youtube.com", "www.youtube.com" }, calls.Select(uri => uri.Host));
    }

    [Theory]
    [InlineData("\"unexpected\"")]
    [InlineData("{}")]
    [InlineData("null")]
    public async Task ResolveAsync_BitrateShapeChanges_ReturnsProtocolFailure(string bitrate)
    {
        var format = "{\"mimeType\":\"audio/mp4; codecs=mp4a.40.2\",\"url\":\"https://r1.googlevideo.com/videoplayback?expire=1800003600\",\"bitrate\":" + bitrate + "}";
        using var http = Client(_ => Json(Player(format)));
        Assert.Equal(SourceFailure.ProtocolChanged, (await Source(http).ResolveAsync(VideoId)).Failure);
    }

    [Theory]
    [InlineData("[null]")]
    [InlineData("[42]")]
    [InlineData("\"unexpected\"")]
    public async Task SearchAsync_ColumnShapeChanges_ReturnsProtocolFailure(string columns)
    {
        var body = "{\"contents\":{\"musicResponsiveListItemRenderer\":{\"playlistItemData\":{\"videoId\":\"BaW_jenozKc\"},\"flexColumns\":" + columns + "}}}";
        using var http = Client(_ => Json(body));
        Assert.Equal(SourceFailure.ProtocolChanged, (await Source(http).SearchAsync("fixture")).Failure);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"contents\":null")]
    [InlineData("[]")]
    [InlineData("{\"contents\":null}")]
    [InlineData("{\"contents\":42}")]
    public async Task SearchAsync_InvalidResponse_ReturnsProtocolFailure(string body)
    {
        using var http = Client(_ => Json(body));
        Assert.Equal(SourceFailure.ProtocolChanged, (await Source(http).SearchAsync("fixture")).Failure);
    }

    [Fact]
    public async Task SearchAsync_TooLargeBody_IsRejectedBeforeParsing()
    {
        using var http = Client(_ => Json(new string('x', 1100)));
        var source = new YouTubeMusicSource(http, new YouTubeSourceOptions { MaximumResponseBytes = 1024 });
        Assert.Equal(SourceFailure.ResponseTooLarge, (await source.SearchAsync("fixture")).Failure);
    }

    [Fact]
    public async Task SearchAsync_CallerCancellation_PropagatesWithoutRetry()
    {
        var calls = 0;
        using var http = Client(async (_, token) =>
        {
            calls++;
            await Task.Delay(TimeSpan.FromSeconds(2), token);
            return Json("{}");
        });
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Source(http).SearchAsync("fixture", cancel.Token));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task SearchAsync_Deadline_ReturnsTimeout()
    {
        using var http = Client(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), token);
            return Json("{}");
        });
        var source = new YouTubeMusicSource(http, new YouTubeSourceOptions { RequestTimeout = TimeSpan.FromMilliseconds(50) });
        Assert.Equal(SourceFailure.Timeout, (await source.SearchAsync("fixture")).Failure);
    }

    [Fact]
    public async Task ProbeRangeAsync_ServerIgnoresRange_ReturnsUnsupportedAndStops()
    {
        var calls = 0;
        using var http = Client(request =>
        {
            calls++;
            return request.Method == HttpMethod.Post ? Json(DirectPlayer()) : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[16]) };
        });
        var source = Source(http);
        var resolved = await source.ResolveAsync(VideoId);
        var result = await source.ProbeRangeAsync(resolved.Value!);
        Assert.Equal(SourceFailure.RangeUnsupported, result.Failure);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ProbeRangeAsync_TwoAccurateRanges_ReadsOnly32Bytes()
    {
        var offsets = new List<long>();
        using var http = Client(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                return Json(DirectPlayer());
            }

            var range = Assert.Single(request.Headers.Range!.Ranges);
            offsets.Add(range.From!.Value);
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(new byte[16]) };
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(range.From!.Value, range.To!.Value, 1000000);
            return response;
        });
        var source = Source(http);
        var resolved = await source.ResolveAsync(VideoId);
        var result = await source.ProbeRangeAsync(resolved.Value!);
        Assert.True(result.IsSuccess);
        Assert.Equal(32, result.Value!.BytesRead);
        Assert.Equal(new long[] { 0, 65536 }, offsets);
    }

    [Fact]
    public async Task RefreshIfExpiredAsync_ExpiredLease_ResolvesAgain()
    {
        var clock = new MutableTimeProvider(Now);
        var calls = 0;
        using var http = Client(_ =>
        {
            calls++;
            return Json(DirectPlayer());
        });
        var source = new YouTubeMusicSource(http, timeProvider: clock);
        var initial = await source.ResolveAsync(VideoId);
        var cached = await source.RefreshIfExpiredAsync(initial.Value!);
        Assert.Same(initial.Value, cached.Value);
        Assert.Equal(1, calls);
        clock.UtcNow = Now.AddHours(2);
        var refreshed = await source.RefreshIfExpiredAsync(initial.Value!);
        Assert.Equal(2, calls);
        // 同一旧地址的fixture不能冒充已刷新成功。
        Assert.Equal(SourceFailure.Expired, refreshed.Failure);
    }

    [Fact]
    public async Task ProbeRangeAsync_ExpiredAndCancelled_HonorsCallerCancellation()
    {
        var clock = new MutableTimeProvider(Now);
        using var http = Client(_ => Json(DirectPlayer()));
        var source = new YouTubeMusicSource(http, timeProvider: clock);
        var initial = await source.ResolveAsync(VideoId);
        clock.UtcNow = Now.AddHours(2);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.ProbeRangeAsync(initial.Value!, cancelled.Token));
    }

    private static YouTubeMusicSource Source(HttpClient http) => new(http, timeProvider: new MutableTimeProvider(Now));
    private static string Player(string formats) => "{\"playabilityStatus\":{\"status\":\"OK\"},\"streamingData\":{\"adaptiveFormats\":[" + formats + "]}}";
    private static string DirectPlayer() => Player("{\"mimeType\":\"audio/mp4; codecs=mp4a.40.2\",\"url\":\"https://r1.googlevideo.com/videoplayback?expire=1800003600\"}");
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> handler) => Client((request, _) => Task.FromResult(handler(request)));
    private static HttpClient Client(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) => Client((request, _) => handler(request));
    private static HttpClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) => new(new FakeHandler(handler));

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request, cancellationToken);
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
