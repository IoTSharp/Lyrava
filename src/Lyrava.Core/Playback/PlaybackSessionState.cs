using Lyrava.Core.Media;

namespace Lyrava.Core.Playback;

/// <summary>单会话的播放意图；原生播放器及其资源由平台服务拥有。</summary>
public sealed class PlaybackSessionState
{
    private readonly object _gate = new();
    private PlaybackSnapshot _snapshot = new(PlaybackStatus.Empty, null, TimeSpan.Zero);
    private bool _resumeAfterInterruption;

    public PlaybackSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return _snapshot;
            }
        }
    }

    public void Load(MediaTrack track, TimeSpan position = default)
    {
        ArgumentNullException.ThrowIfNull(track);
        ValidatePosition(position, track.Duration);
        lock (_gate)
        {
            _snapshot = new(PlaybackStatus.Ready, track, position);
            _resumeAfterInterruption = false;
        }
    }

    public void Play()
    {
        lock (_gate)
        {
            if (_snapshot.Track is null || _snapshot.Status == PlaybackStatus.Failed)
            {
                throw new InvalidOperationException("Load a playable track before starting playback.");
            }

            _snapshot = _snapshot with { Status = PlaybackStatus.Playing };
            _resumeAfterInterruption = false;
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            PauseCore();
        }
    }

    public void BeginInterruption()
    {
        lock (_gate)
        {
            if (_snapshot.Status == PlaybackStatus.Playing)
            {
                _resumeAfterInterruption = true;
                _snapshot = _snapshot with { Status = PlaybackStatus.Interrupted };
            }
        }
    }

    public void EndInterruption(bool systemAllowsResume)
    {
        lock (_gate)
        {
            if (_snapshot.Status == PlaybackStatus.Interrupted)
            {
                _snapshot = _snapshot with
                {
                    Status = _resumeAfterInterruption && systemAllowsResume
                        ? PlaybackStatus.Playing
                        : PlaybackStatus.Paused
                };
            }

            _resumeAfterInterruption = false;
        }
    }

    public void RouteDisconnected()
    {
        lock (_gate)
        {
            PauseCore();
        }
    }

    public void UpdatePosition(TimeSpan position)
    {
        lock (_gate)
        {
            if (_snapshot.Track is null)
            {
                throw new InvalidOperationException("No track is loaded.");
            }

            ValidatePosition(position, _snapshot.Track.Duration);
            _snapshot = _snapshot with { Position = position };
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _snapshot = new(PlaybackStatus.Empty, null, TimeSpan.Zero);
            _resumeAfterInterruption = false;
        }
    }

    public void Fail()
    {
        lock (_gate)
        {
            _snapshot = _snapshot with { Status = PlaybackStatus.Failed };
            _resumeAfterInterruption = false;
        }
    }

    private void PauseCore()
    {
        if (_snapshot.Track is not null && _snapshot.Status != PlaybackStatus.Failed)
        {
            _snapshot = _snapshot with { Status = PlaybackStatus.Paused };
        }

        _resumeAfterInterruption = false;
    }

    private static void ValidatePosition(TimeSpan position, TimeSpan? duration)
    {
        if (position < TimeSpan.Zero || position > duration)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }
    }
}
