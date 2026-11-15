using System.Text.Json.Serialization;

namespace Lyrava.Sources.YouTube;

public enum SourceFailure
{
    None,
    Network,
    Timeout,
    HttpFailure,
    AuthenticationRequired,
    RateLimited,
    Unavailable,
    ProtocolChanged,
    SignatureRequired,
    FormatUnsupported,
    Expired,
    ResponseTooLarge,
    RangeUnsupported
}

public enum SourcePlayability
{
    Unspecified,
    Ok,
    LoginRequired,
    Unplayable,
    Error,
    AgeCheckRequired,
    LiveStreamOffline,
    Unknown
}

public enum YouTubePlayerProfile
{
    WebRemix,
    VisionOs
}

/// <summary>不包含服务响应、凭据或临时地址的结果。</summary>
public sealed class SourceResult<T>
{
    private SourceResult(T? value, SourceFailure failure, SourcePlayability playability)
    {
        Value = value;
        Failure = failure;
        Playability = playability;
    }

    public T? Value { get; }
    public SourceFailure Failure { get; }
    public SourcePlayability Playability { get; }
    public bool IsSuccess => Failure == SourceFailure.None;

    public static SourceResult<T> Success(T value, SourcePlayability playability = SourcePlayability.Unspecified)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value, SourceFailure.None, playability);
    }

    public static SourceResult<T> Failed(SourceFailure failure, SourcePlayability playability = SourcePlayability.Unspecified)
    {
        if (failure == SourceFailure.None)
        {
            throw new ArgumentOutOfRangeException(nameof(failure));
        }

        return new(default, failure, playability);
    }
    public override string ToString() => IsSuccess ? "SourceResult(Success)" : $"SourceResult({Failure})";
}

public sealed record SourceTrack(string Id, string Title);

/// <summary>仅供当前播放使用的临时地址，禁止写入存储或诊断。</summary>
public sealed class DirectAudioStream
{
    internal DirectAudioStream(string videoId, Uri playbackUri, int bitrate, DateTimeOffset expiresAt)
    {
        VideoId = videoId;
        PlaybackUri = playbackUri;
        Bitrate = bitrate;
        ExpiresAt = expiresAt;
    }

    public string VideoId { get; }
    [JsonIgnore]
    public Uri PlaybackUri { get; }
    public string MimeType => "audio/mp4";
    public string Codec => "mp4a.40.2";
    public int Bitrate { get; }
    public DateTimeOffset ExpiresAt { get; }
    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now.AddSeconds(30);
    public override string ToString() => "DirectAudioStream(audio/mp4; address redacted)";
}

public sealed record RangeProbeResult(int HttpStatus, bool SupportsByteRange, int BytesRead);

public sealed class YouTubeSourceOptions
{
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public int MaximumResponseBytes { get; init; } = 2 * 1024 * 1024;
    public int MaximumSearchResults { get; init; } = 20;
    public string ClientVersion { get; init; } = "1.20241113.01.00";
    public YouTubePlayerProfile PlayerProfile { get; init; } = YouTubePlayerProfile.WebRemix;
    public string? VisitorData { get; init; }
}
