using Lyrava.Core.Media;
using Lyrava.Core.Playback;
using Lyrava.Sources.YouTube;

namespace Lyrava.App;

/// <summary>
/// A bounded Windows queue coordinator. The platform playback service remains the
/// sole owner of the native player; this type only owns queue order and resolution.
/// </summary>
public sealed class WindowsQueueCoordinator : IDisposable
{
    public const int MaximumQueueItems = 100;

    private readonly object _stateGate = new();
    private readonly SemaphoreSlim _commandGate = new(1, 1);
    private readonly PlaybackQueue _queue = new();
    private readonly IMediaPlaybackService _playback;
    private readonly YouTubeMusicSource _source;
    private CancellationTokenSource? _operation;
    private bool _disposed;

    public WindowsQueueCoordinator(IMediaPlaybackService playback, YouTubeMusicSource source)
    {
        _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public event EventHandler? Changed;

    public IReadOnlyList<MediaTrack> Items => _queue.Items;

    public MediaTrack? Current => _queue.Current;

    public int CurrentIndex => _queue.CurrentIndex;

    public bool TryEnqueue(SourceTrack track)
    {
        EnsureAvailable();
        ArgumentNullException.ThrowIfNull(track);
        if (string.IsNullOrWhiteSpace(track.Id) || !IsVideoId(track.Id) || string.IsNullOrWhiteSpace(track.Title))
        {
            return false;
        }

        lock (_stateGate)
        {
            if (_queue.Count >= MaximumQueueItems || _queue.Items.Any(item => string.Equals(item.Id, track.Id, StringComparison.Ordinal)))
            {
                return false;
            }

            _queue.Enqueue(new MediaTrack(track.Id, track.Title));
        }

        PublishChanged();
        return true;
    }

    public bool RemoveAt(int index)
    {
        EnsureAvailable();
        lock (_stateGate)
        {
            if (!_queue.RemoveAt(index)) return false;
        }

        PublishChanged();
        return true;
    }

    public void Clear()
    {
        EnsureAvailable();
        CancelActiveOperation();
        _playback.Stop();
        lock (_stateGate)
        {
            _queue.Clear();
        }

        PublishChanged();
    }

    public Task<QueuePlaybackResult> PlayCurrentAsync(CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        CancelActiveOperation();
        return PlayIndexAsync(CurrentIndex, cancellationToken);
    }

    public Task<QueuePlaybackResult> NextAsync(CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        CancelActiveOperation();
        var nextIndex = CurrentIndex + 1;
        return PlayIndexAsync(nextIndex, cancellationToken);
    }

    public Task<QueuePlaybackResult> PreviousAsync(CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        CancelActiveOperation();
        var view = _playback.View;
        if (view.Position > TimeSpan.FromSeconds(3))
        {
            return SeekToStartAsync(cancellationToken);
        }

        var previousIndex = CurrentIndex - 1;
        return PlayIndexAsync(previousIndex, cancellationToken);
    }

    public void Stop()
    {
        EnsureAvailable();
        CancelActiveOperation();
        _playback.Stop();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelActiveOperation();
        _commandGate.Dispose();
        Changed = null;
    }

    private async Task<QueuePlaybackResult> PlayIndexAsync(int index, CancellationToken cancellationToken)
    {
        if (index < 0)
        {
            return QueuePlaybackResult.NoItem;
        }

        await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        using var operation = BeginOperation(cancellationToken);
        try
        {
            MediaTrack? item;
            lock (_stateGate)
            {
                var items = _queue.Items;
                item = index >= 0 && index < items.Count ? items[index] : null;
            }

            if (item is null)
            {
                return QueuePlaybackResult.NoItem;
            }

            var resolved = await _source.ResolveAsync(item.Id, operation.Token).ConfigureAwait(false);
            if (!resolved.IsSuccess)
            {
                return QueuePlaybackResult.Failed(resolved.Failure, resolved.Playability);
            }

            var refreshed = await _source.RefreshIfExpiredAsync(resolved.Value!, operation.Token).ConfigureAwait(false);
            if (!refreshed.IsSuccess)
            {
                return QueuePlaybackResult.Failed(refreshed.Failure, refreshed.Playability);
            }

            await _playback.LoadAndPlayAsync(refreshed.Value!.PlaybackUri.AbsoluteUri, operation.Token).ConfigureAwait(false);
            operation.Token.ThrowIfCancellationRequested();
            lock (_stateGate)
            {
                var items = _queue.Items;
                if (index < items.Count && string.Equals(items[index].Id, item.Id, StringComparison.Ordinal))
                {
                    _queue.Select(index);
                }
            }

            PublishChanged();
            return QueuePlaybackResult.FromStarted(item);
        }
        catch (OperationCanceledException) when (operation.Token.IsCancellationRequested)
        {
            return QueuePlaybackResult.Cancelled;
        }
        finally
        {
            _commandGate.Release();
            EndOperation(operation);
        }
    }

    private async Task<QueuePlaybackResult> SeekToStartAsync(CancellationToken cancellationToken)
    {
        await _commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _playback.SeekAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
            return QueuePlaybackResult.Seeked;
        }
        catch (OperationCanceledException)
        {
            return QueuePlaybackResult.Cancelled;
        }
        finally
        {
            _commandGate.Release();
        }
    }

    private CancellationTokenSource BeginOperation(CancellationToken cancellationToken)
    {
        var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_stateGate)
        {
            _operation = operation;
        }

        return operation;
    }

    private void EndOperation(CancellationTokenSource operation)
    {
        lock (_stateGate)
        {
            if (ReferenceEquals(_operation, operation))
            {
                _operation = null;
            }
        }
    }

    private void CancelActiveOperation()
    {
        lock (_stateGate)
        {
            _operation?.Cancel();
        }
    }

    private static bool IsVideoId(string id) => id.Length == 11 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private void PublishChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private void EnsureAvailable() => ObjectDisposedException.ThrowIf(_disposed, this);
}

public sealed record QueuePlaybackResult(
    bool Started,
    bool DidSeek,
    bool WasCancelled,
    SourceFailure Failure,
    SourcePlayability Playability,
    MediaTrack? Track)
{
    public static QueuePlaybackResult NoItem { get; } = new(false, false, false, SourceFailure.None, SourcePlayability.Unspecified, null);

    public static QueuePlaybackResult Cancelled { get; } = new(false, false, true, SourceFailure.None, SourcePlayability.Unspecified, null);

    public static QueuePlaybackResult Seeked { get; } = new(false, true, false, SourceFailure.None, SourcePlayability.Unspecified, null);

    public static QueuePlaybackResult FromStarted(MediaTrack track) => new(true, false, false, SourceFailure.None, SourcePlayability.Ok, track);

    public static QueuePlaybackResult Failed(SourceFailure failure, SourcePlayability playability) => new(false, false, false, failure, playability, null);
}
