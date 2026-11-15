using Lyrava.Core.Media;

namespace Lyrava.Core.Storage;

/// <summary>Metadata-only queue item used by a replaceable persistence adapter.</summary>
public sealed record PlaybackQueueItem
{
    public PlaybackQueueItem(string id, string title, string? artist = null, TimeSpan? duration = null)
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

    public MediaTrack ToMediaTrack() => new(Id, Title, Artist, Duration);
}

/// <summary>
/// Versioned restore data. It contains queue metadata and intent only; it never
/// contains a temporary media URL, cookie, token or native player state.
/// </summary>
public sealed class PlaybackStateSnapshot
{
    public const int CurrentVersion = 1;
    public const int MaximumQueueItems = 100;

    private readonly IReadOnlyList<PlaybackQueueItem> _queue;

    private PlaybackStateSnapshot(
        int version,
        IReadOnlyList<PlaybackQueueItem> queue,
        int currentIndex,
        TimeSpan position,
        bool explicitlyPaused,
        bool resumeAllowed)
    {
        Version = version;
        _queue = queue;
        CurrentIndex = currentIndex;
        Position = position;
        ExplicitlyPaused = explicitlyPaused;
        ResumeAllowed = resumeAllowed;
    }

    public int Version { get; }

    public IReadOnlyList<PlaybackQueueItem> Queue => _queue;

    public int CurrentIndex { get; }

    public TimeSpan Position { get; }

    public bool ExplicitlyPaused { get; }

    /// <summary>False by default so process restart never starts audio unexpectedly.</summary>
    public bool ResumeAllowed { get; }

    public static PlaybackStateSnapshot Create(
        IEnumerable<PlaybackQueueItem> queue,
        int currentIndex = -1,
        TimeSpan position = default,
        bool explicitlyPaused = false,
        bool resumeAllowed = false,
        int version = CurrentVersion)
    {
        ArgumentNullException.ThrowIfNull(queue);
        if (version != CurrentVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, $"Only snapshot version {CurrentVersion} is supported.");
        }

        var items = queue.ToArray();
        if (items.Length > MaximumQueueItems)
        {
            throw new ArgumentOutOfRangeException(nameof(queue));
        }

        if (items.Length == 0)
        {
            if (currentIndex != -1 || position != TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(currentIndex), "An empty queue must have no current item or position.");
            }
        }
        else if ((uint)currentIndex >= (uint)items.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(currentIndex));
        }

        if (position < TimeSpan.Zero || (items.Length > 0 && items[currentIndex].Duration is { } duration && position > duration))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        if (explicitlyPaused && resumeAllowed)
        {
            throw new ArgumentException("Explicitly paused state cannot allow automatic resume.", nameof(resumeAllowed));
        }

        return new(
            version,
            Array.AsReadOnly(items),
            currentIndex,
            position,
            explicitlyPaused,
            resumeAllowed);
    }

    public static PlaybackStateSnapshot Empty() => Create(Array.Empty<PlaybackQueueItem>());
}
