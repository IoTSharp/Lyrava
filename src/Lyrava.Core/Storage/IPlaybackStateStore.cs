namespace Lyrava.Core.Storage;

/// <summary>
/// Replaceable persistence boundary for playback metadata. Implementations own
/// their storage medium and must preserve cancellation and snapshot validation.
/// </summary>
public interface IPlaybackStateStore
{
    ValueTask<PlaybackStateSnapshot?> LoadAsync(CancellationToken cancellationToken = default);

    ValueTask SaveAsync(PlaybackStateSnapshot snapshot, CancellationToken cancellationToken = default);
}

/// <summary>
/// Contract fixture for integration tests and early UI work. It does not write
/// to disk and therefore is not a user-data persistence implementation.
/// </summary>
public sealed class InMemoryPlaybackStateStore : IPlaybackStateStore
{
    private readonly object _gate = new();
    private PlaybackStateSnapshot? _snapshot;

    public ValueTask<PlaybackStateSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return ValueTask.FromResult(_snapshot);
        }
    }

    public ValueTask SaveAsync(PlaybackStateSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _snapshot = snapshot;
        }

        return ValueTask.CompletedTask;
    }
}
