namespace Lyrava.Core.Media;

/// <summary>与媒体临时地址及平台播放器无关的曲目元数据。</summary>
public sealed record MediaTrack
{
    public MediaTrack(string id, string title, string? artist = null, TimeSpan? duration = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        Id = id;
        Title = title;
        Artist = artist;
        Duration = duration;
    }

    public string Id { get; }

    public string Title { get; }

    public string? Artist { get; }

    public TimeSpan? Duration { get; }
}
