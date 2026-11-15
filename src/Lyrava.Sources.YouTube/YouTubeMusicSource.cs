using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Lyrava.Sources.YouTube;

/// <summary>无凭据、无脚本执行的 M0 音乐源探针；不承诺完整源站兼容。</summary>
public sealed class YouTubeMusicSource
{
    private readonly HttpClient _httpClient;
    private readonly YouTubeSourceOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly RequestContext _context;
    private string? _visitorData;

    /// <summary>HttpClient 的生命周期由调用方管理，不在此修改默认请求头。</summary>
    public YouTubeMusicSource(HttpClient httpClient, YouTubeSourceOptions? options = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _options = options ?? new YouTubeSourceOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
        if (_options.RequestTimeout <= TimeSpan.Zero || _options.RequestTimeout > TimeSpan.FromSeconds(30) ||
            _options.MaximumResponseBytes is < 1024 or > 4 * 1024 * 1024 ||
            _options.MaximumSearchResults is < 1 or > 50 ||
            string.IsNullOrWhiteSpace(_options.ClientVersion) || _options.ClientVersion.Length > 64 ||
            (_options.PlayerProfile == YouTubePlayerProfile.VisionOs && _options.VisitorData is { } visitor && string.IsNullOrWhiteSpace(visitor) ||
             _options.PlayerProfile == YouTubePlayerProfile.VisionOs && _options.VisitorData is { Length: > 256 }))
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        _context = new RequestContext(new ClientContext("WEB_REMIX", _options.ClientVersion));
    }

    public async Task<SourceResult<IReadOnlyList<SourceTrack>>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (query.Length > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(query));
        }

        var body = JsonSerializer.Serialize(new SearchRequest(_context, query), InnerTubeJsonContext.Default.SearchRequest);
        var response = await PostAsync("search", body, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            return SourceResult<IReadOnlyList<SourceTrack>>.Failed(response.Failure);
        }

        using var json = response.Value!;
        return ParseSearch(json.RootElement, cancellationToken);
    }

    public async Task<SourceResult<DirectAudioStream>> ResolveAsync(string videoId, CancellationToken cancellationToken = default)
    {
        ValidateVideoId(videoId);
        var response = _options.PlayerProfile == YouTubePlayerProfile.VisionOs
            ? await ResolveVisionOsAsync(videoId, cancellationToken).ConfigureAwait(false)
            : await PostAsync("player", JsonSerializer.Serialize(new PlayerRequest(_context, videoId), InnerTubeJsonContext.Default.PlayerRequest), cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            return SourceResult<DirectAudioStream>.Failed(response.Failure);
        }

        using var json = response.Value!;
        return ParsePlayer(json.RootElement, videoId, _timeProvider.GetUtcNow(), cancellationToken);
    }

    private async Task<SourceResult<JsonDocument>> ResolveVisionOsAsync(string videoId, CancellationToken cancellationToken)
    {
        if (_visitorData is null)
        {
            if (_options.VisitorData is { } configured)
            {
                _visitorData = configured;
            }
            else
            {
                using var bootstrap = new HttpRequestMessage(HttpMethod.Get, "https://www.youtube.com/sw.js_data");
                bootstrap.Headers.Accept.ParseAdd("application/json");
                bootstrap.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 Safari/605.1.15");
                var bootstrapResult = await SendJsonAsync(bootstrap, cancellationToken).ConfigureAwait(false);
                if (!bootstrapResult.IsSuccess)
                {
                    return SourceResult<JsonDocument>.Failed(bootstrapResult.Failure);
                }

                try
                {
                    var root = bootstrapResult.Value!.RootElement;
                    if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0 || root[0].ValueKind != JsonValueKind.Array || root[0].GetArrayLength() <= 2 ||
                        root[0][2].ValueKind != JsonValueKind.Array || root[0][2].GetArrayLength() == 0 || root[0][2][0].ValueKind != JsonValueKind.Array || root[0][2][0].GetArrayLength() == 0 ||
                        root[0][2][0][0].ValueKind != JsonValueKind.Array || root[0][2][0][0].GetArrayLength() <= 13 || root[0][2][0][0][13].ValueKind != JsonValueKind.String)
                    {
                        return SourceResult<JsonDocument>.Failed(SourceFailure.ProtocolChanged);
                    }

                    _visitorData = root[0][2][0][0][13].GetString();
                    return string.IsNullOrWhiteSpace(_visitorData) || _visitorData.Length > 256
                        ? SourceResult<JsonDocument>.Failed(SourceFailure.ProtocolChanged)
                        : await PostVisionOsAsync(videoId, _visitorData, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    bootstrapResult.Value!.Dispose();
                }
            }
        }

        return await PostVisionOsAsync(videoId, _visitorData!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SourceResult<JsonDocument>> PostVisionOsAsync(string videoId, string visitorData, CancellationToken cancellationToken)
    {
        var request = new VisionOsRequest(videoId, true, true, new VisionOsRequestContext(new VisionOsClientContext(
            "VISIONOS", "1.02", "Apple", "RealityDevice17,1", "visionOS", "26.5.23O471", visitorData)));
        var body = JsonSerializer.Serialize(request, InnerTubeJsonContext.Default.VisionOsRequest);
        return await PostAbsoluteAsync("https://www.youtube.com/youtubei/v1/player", body, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SourceResult<JsonDocument>> PostAbsoluteAsync(string address, string body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, address);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 Safari/605.1.15");
        return await SendJsonAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>在即将播放时重新解析过期地址；调用方显式持有和取消此操作。</summary>
    public Task<SourceResult<DirectAudioStream>> RefreshIfExpiredAsync(DirectAudioStream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();
        return stream.IsExpired(_timeProvider.GetUtcNow())
            ? ResolveAsync(stream.VideoId, cancellationToken)
            : Task.FromResult(SourceResult<DirectAudioStream>.Success(stream));
    }

    /// <summary>只读取两个不相邻范围的各16字节；不下载或保存完整媒体。</summary>
    public async Task<SourceResult<RangeProbeResult>> ProbeRangeAsync(DirectAudioStream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();
        if (stream.IsExpired(_timeProvider.GetUtcNow()))
        {
            return SourceResult<RangeProbeResult>.Failed(SourceFailure.Expired);
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.RequestTimeout);
        var totalRead = 0;
        try
        {
            // 两次请求且共享一个墙钟截止，验证非零位置能正确返回范围。
            foreach (var offset in new[] { 0L, 65536L })
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, stream.PlaybackUri);
                request.Headers.Range = new RangeHeaderValue(offset, offset + 15);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.PartialContent ||
                    response.Content.Headers.ContentRange is not { From: var start, To: var end, Unit: "bytes" } ||
                    start != offset || end != offset + 15)
                {
                    return SourceResult<RangeProbeResult>.Failed(response.IsSuccessStatusCode ? SourceFailure.RangeUnsupported : ClassifyHttp(response.StatusCode));
                }

                await using var content = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
                var buffer = new byte[16];
                await content.ReadExactlyAsync(buffer, deadline.Token).ConfigureAwait(false);
                totalRead += buffer.Length;
            }

            return SourceResult<RangeProbeResult>.Success(new RangeProbeResult(206, true, totalRead));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SourceResult<RangeProbeResult>.Failed(SourceFailure.Timeout);
        }
        catch (HttpRequestException)
        {
            return SourceResult<RangeProbeResult>.Failed(SourceFailure.Network);
        }
        catch (IOException)
        {
            return SourceResult<RangeProbeResult>.Failed(SourceFailure.Network);
        }
    }

    private async Task<SourceResult<JsonDocument>> PostAsync(string endpoint, string body, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.RequestTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://music.youtube.com/youtubei/v1/{endpoint}?prettyPrint=false");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        request.Headers.TryAddWithoutValidation("X-Youtube-Client-Name", "67");
        request.Headers.TryAddWithoutValidation("X-Youtube-Client-Version", _options.ClientVersion);
        request.Headers.TryAddWithoutValidation("Origin", "https://music.youtube.com");
        request.Headers.UserAgent.ParseAdd("Lyrava-M0/0.1");
        try
        {
            return await SendJsonAsync(request, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SourceResult<JsonDocument>.Failed(SourceFailure.Timeout);
        }
        catch (HttpRequestException)
        {
            return SourceResult<JsonDocument>.Failed(SourceFailure.Network);
        }
    }

    private async Task<SourceResult<JsonDocument>> SendJsonAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.RequestTimeout);
        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return SourceResult<JsonDocument>.Failed(ClassifyHttp(response.StatusCode));
            }

            if (response.Content.Headers.ContentLength > _options.MaximumResponseBytes)
            {
                return SourceResult<JsonDocument>.Failed(SourceFailure.ResponseTooLarge);
            }

            await using var content = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            var json = await ReadJsonAsync(content, deadline.Token).ConfigureAwait(false);
            return SourceResult<JsonDocument>.Success(json);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SourceResult<JsonDocument>.Failed(SourceFailure.Timeout);
        }
        catch (HttpRequestException)
        {
            return SourceResult<JsonDocument>.Failed(SourceFailure.Network);
        }
        catch (JsonException)
        {
            return SourceResult<JsonDocument>.Failed(SourceFailure.ProtocolChanged);
        }
        catch (InvalidDataException)
        {
            return SourceResult<JsonDocument>.Failed(SourceFailure.ResponseTooLarge);
        }
    }

    private async Task<JsonDocument> ReadJsonAsync(Stream content, CancellationToken cancellationToken)
    {
        using var data = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            // 最大响应4MiB；额外读取1字节证明超限，不接受截断JSON。
            for (var reads = 0; reads <= _options.MaximumResponseBytes; reads++)
            {
                var remaining = _options.MaximumResponseBytes + 1 - (int)data.Length;
                var read = await content.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    var payload = data.GetBuffer().AsMemory(0, (int)data.Length);
                    if (payload.Span.StartsWith(")]}'"u8))
                    {
                        if (payload.Length > 5 && payload.Span[4] == (byte)'\r' && payload.Span[5] == (byte)'\n')
                        {
                            payload = payload[6..];
                        }
                        else if (payload.Length > 4 && payload.Span[4] == (byte)'\n')
                        {
                            payload = payload[5..];
                        }
                        else
                        {
                            throw new JsonException("Invalid XSSI prefix");
                        }
                    }

                    return JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 64 });
                }

                data.Write(buffer, 0, read);
                if (data.Length > _options.MaximumResponseBytes)
                {
                    throw new InvalidDataException("ResponseTooLarge");
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }

        throw new InvalidDataException("ResponseTooLarge");
    }

    private SourceResult<IReadOnlyList<SourceTrack>> ParseSearch(JsonElement root, CancellationToken cancellationToken)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("contents", out var contents) || contents.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            return SourceResult<IReadOnlyList<SourceTrack>>.Failed(SourceFailure.ProtocolChanged);
        }

        var tracks = new List<SourceTrack>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<JsonElement>();
        pending.Enqueue(contents);
        var clock = Stopwatch.StartNew();
        for (var visited = 0; pending.Count > 0 && visited < 20000 && clock.Elapsed < TimeSpan.FromSeconds(2); visited++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var element = pending.Dequeue();
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("videoRenderer", out var renderer) &&
                    TryString(renderer, "videoId", out var id) && IsVideoId(id) && ids.Add(id))
                {
                    tracks.Add(new SourceTrack(id, ReadText(renderer, "title") ?? "Untitled"));
                }
                else if (element.TryGetProperty("musicResponsiveListItemRenderer", out renderer) &&
                         TryPath(renderer, out var watch, "playlistItemData", "videoId") &&
                         watch.ValueKind == JsonValueKind.String && IsVideoId(watch.GetString()!) && ids.Add(watch.GetString()!))
                {
                    var title = "Untitled";
                    if (renderer.TryGetProperty("flexColumns", out var columns))
                    {
                        if (columns.ValueKind != JsonValueKind.Array || columns.GetArrayLength() == 0 || columns[0].ValueKind != JsonValueKind.Object ||
                            !columns[0].TryGetProperty("musicResponsiveListItemFlexColumnRenderer", out var column) || column.ValueKind != JsonValueKind.Object)
                        {
                            return SourceResult<IReadOnlyList<SourceTrack>>.Failed(SourceFailure.ProtocolChanged);
                        }

                        title = ReadText(column, "text") ?? title;
                    }

                    tracks.Add(new SourceTrack(watch.GetString()!, title));
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (pending.Count >= 20000)
                    {
                        return SourceResult<IReadOnlyList<SourceTrack>>.Failed(SourceFailure.ResponseTooLarge);
                    }

                    pending.Enqueue(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in element.EnumerateArray())
                {
                    if (pending.Count >= 20000)
                    {
                        return SourceResult<IReadOnlyList<SourceTrack>>.Failed(SourceFailure.ResponseTooLarge);
                    }

                    pending.Enqueue(child);
                }
            }

            if (tracks.Count >= _options.MaximumSearchResults)
            {
                return SourceResult<IReadOnlyList<SourceTrack>>.Success(tracks.AsReadOnly());
            }
        }

        return pending.Count == 0
            ? SourceResult<IReadOnlyList<SourceTrack>>.Success(tracks.AsReadOnly())
            : SourceResult<IReadOnlyList<SourceTrack>>.Failed(SourceFailure.ResponseTooLarge);
    }

    private static SourceResult<DirectAudioStream> ParsePlayer(JsonElement root, string videoId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var clock = Stopwatch.StartNew();
        if (!TryPath(root, out var status, "playabilityStatus", "status") || status.ValueKind != JsonValueKind.String)
        {
            return SourceResult<DirectAudioStream>.Failed(SourceFailure.ProtocolChanged);
        }

        if (status.GetString() != "OK")
        {
            var playability = status.GetString() switch
            {
                "LOGIN_REQUIRED" => SourcePlayability.LoginRequired,
                "UNPLAYABLE" => SourcePlayability.Unplayable,
                "ERROR" => SourcePlayability.Error,
                "AGE_CHECK_REQUIRED" => SourcePlayability.AgeCheckRequired,
                "LIVE_STREAM_OFFLINE" => SourcePlayability.LiveStreamOffline,
                _ => SourcePlayability.Unknown
            };
            return SourceResult<DirectAudioStream>.Failed(playability is SourcePlayability.LoginRequired or SourcePlayability.AgeCheckRequired ? SourceFailure.AuthenticationRequired : SourceFailure.Unavailable, playability);
        }

        if (!root.TryGetProperty("streamingData", out var streaming) || streaming.ValueKind != JsonValueKind.Object)
        {
            return SourceResult<DirectAudioStream>.Failed(SourceFailure.ProtocolChanged);
        }

        DirectAudioStream? selected = null;
        var cipherSeen = false;
        var expiredSeen = false;
        foreach (var field in new[] { "adaptiveFormats", "formats" })
        {
            if (!streaming.TryGetProperty(field, out var formats) || formats.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var count = 0;
            foreach (var format in formats.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++count > 200 || clock.Elapsed > TimeSpan.FromSeconds(2))
                {
                    return SourceResult<DirectAudioStream>.Failed(SourceFailure.ResponseTooLarge);
                }

                if (format.ValueKind != JsonValueKind.Object)
                {
                    return SourceResult<DirectAudioStream>.Failed(SourceFailure.ProtocolChanged);
                }

                cipherSeen |= format.TryGetProperty("signatureCipher", out _) || format.TryGetProperty("cipher", out _);
                if (!TryString(format, "mimeType", out var mime) || !mime.StartsWith("audio/mp4", StringComparison.OrdinalIgnoreCase) ||
                    !mime.Contains("mp4a.40.2", StringComparison.OrdinalIgnoreCase) ||
                    !TryString(format, "url", out var address) || address.Length > 16384 || !Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
                    uri.Scheme != Uri.UriSchemeHttps || !IsGoogleVideoHost(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) || uri.Port != 443)
                {
                    continue;
                }

                var expiry = ReadExpiry(uri, streaming, now);
                if (expiry is null || expiry <= now.AddSeconds(30))
                {
                    expiredSeen |= expiry is not null;
                    continue;
                }

                var bitrate = 0;
                if (format.TryGetProperty("bitrate", out var rate))
                {
                    if (rate.ValueKind != JsonValueKind.Number || !rate.TryGetInt32(out bitrate) || bitrate < 0)
                    {
                        return SourceResult<DirectAudioStream>.Failed(SourceFailure.ProtocolChanged);
                    }
                }

                if (selected is null || bitrate > selected.Bitrate)
                {
                    selected = new DirectAudioStream(videoId, uri, bitrate, expiry.Value);
                }
            }
        }

        return selected is not null
            ? SourceResult<DirectAudioStream>.Success(selected, SourcePlayability.Ok)
            : SourceResult<DirectAudioStream>.Failed(cipherSeen ? SourceFailure.SignatureRequired : expiredSeen ? SourceFailure.Expired : SourceFailure.FormatUnsupported);
    }

    private static DateTimeOffset? ReadExpiry(Uri uri, JsonElement streaming, DateTimeOffset now)
    {
        foreach (var part in uri.Query.TrimStart('?').Split('&'))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && pair[0] == "expire" && long.TryParse(pair[1], CultureInfo.InvariantCulture, out var seconds) && seconds is >= 0 and <= 253402300799)
            {
                return DateTimeOffset.FromUnixTimeSeconds(seconds);
            }
        }

        if (TryString(streaming, "expiresInSeconds", out var duration) && long.TryParse(duration, CultureInfo.InvariantCulture, out var validFor) && validFor is > 0 and <= 86400)
        {
            return now.AddSeconds(validFor);
        }

        return null;
    }

    private static bool IsGoogleVideoHost(string host) => host.EndsWith(".googlevideo.com", StringComparison.OrdinalIgnoreCase);

    private static SourceFailure ClassifyHttp(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => SourceFailure.AuthenticationRequired,
        HttpStatusCode.NotFound or HttpStatusCode.Gone => SourceFailure.Unavailable,
        HttpStatusCode.TooManyRequests => SourceFailure.RateLimited,
        _ => SourceFailure.HttpFailure
    };

    private static bool TryString(JsonElement element, string field, out string value)
    {
        value = string.Empty;
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(field, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString()!;
        return true;
    }

    private static bool TryPath(JsonElement element, out JsonElement value, params string[] path)
    {
        value = element;
        foreach (var field in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(field, out value))
            {
                return false;
            }
        }

        return true;
    }

    private static string? ReadText(JsonElement renderer, string field)
    {
        if (renderer.ValueKind != JsonValueKind.Object || !renderer.TryGetProperty(field, out var text) || text.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (TryString(text, "simpleText", out var simple))
        {
            return simple;
        }

        if (!text.TryGetProperty("runs", out var runs) || runs.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var builder = new StringBuilder();
        var count = 0;
        foreach (var run in runs.EnumerateArray())
        {
            if (++count > 100 || builder.Length >= 1024)
            {
                break;
            }

            if (TryString(run, "text", out var part))
            {
                builder.Append(part.AsSpan(0, Math.Min(part.Length, 1024 - builder.Length)));
            }
        }

        return builder.ToString();
    }

    private static bool IsVideoId(string id) => id.Length == 11 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

    private static void ValidateVideoId(string videoId)
    {
        ArgumentNullException.ThrowIfNull(videoId);
        if (!IsVideoId(videoId))
        {
            throw new ArgumentException("Expected an 11-character YouTube video identifier.", nameof(videoId));
        }
    }
}
