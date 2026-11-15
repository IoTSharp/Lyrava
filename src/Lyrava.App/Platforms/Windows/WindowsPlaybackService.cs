using Lyrava.Core.Media;
using Lyrava.Core.Playback;
using Lyrava.Sources.YouTube;
using Microsoft.Maui.ApplicationModel;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Lyrava.App.Platforms.Windows;

/// <summary>
/// A single Windows-owned media player for the first Windows vertical slice.
/// System transport controls and packaged release acceptance remain separate gates.
/// </summary>
public sealed class WindowsPlaybackService : IMediaPlaybackService
{
    private readonly PlaybackSessionState _state = new();
    private readonly MediaPlayer _player = new();
    private readonly SystemMediaTransportControls _systemControls;
    private readonly HttpClient _sourceHttp = new(new HttpClientHandler { UseCookies = false })
    {
        Timeout = TimeSpan.FromSeconds(12)
    };
    private readonly YouTubeMusicSource _source;
    private CancellationTokenSource? _preparation;
    private CancellationTokenSource? _seek;
    private CancellationTokenSource? _sourceJourney;
    private TaskCompletionSource<bool>? _itemEnded;
    private long _generation;
    private bool _disposed;
    private string _result = "not_validated";
    private TimeSpan? _duration;

    public WindowsPlaybackService()
    {
        _source = new YouTubeMusicSource(_sourceHttp);
        _systemControls = _player.SystemMediaTransportControls;
        _player.CommandManager.IsEnabled = false;
        _systemControls.IsEnabled = true;
        _systemControls.IsPlayEnabled = true;
        _systemControls.IsPauseEnabled = true;
        _systemControls.IsStopEnabled = true;
        _systemControls.IsNextEnabled = false;
        _systemControls.IsPreviousEnabled = false;
        _systemControls.ButtonPressed += OnSystemButtonPressed;
        _systemControls.PlaybackPositionChangeRequested += OnSystemPositionChangeRequested;
        UpdateSystemMetadata();
        _player.MediaOpened += OnMediaOpened;
        _player.MediaFailed += OnMediaFailed;
        _player.MediaEnded += OnMediaEnded;
        _player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
        _player.PlaybackSession.PositionChanged += OnPlaybackPositionChanged;
    }

    public event EventHandler? Changed;

    public MediaProbeView View => new(
        _state.Snapshot.Status.ToString(),
        _disposed ? "disposed" : _player.PlaybackSession.PlaybackState.ToString(),
        _state.Snapshot.Position,
        _duration,
        _result);

    public async Task LoadAndPlayAsync(string? address, CancellationToken cancellationToken)
    {
        EnsureAvailable();
        CancelSourceJourney();
        await LoadMediaAsync(address, cancellationToken);
    }

    public void StartSourceJourney(string? videoIds)
    {
        EnsureAvailable();
        CancelSourceJourney();
        StopMedia();
        if (string.IsNullOrWhiteSpace(videoIds) || videoIds.Length > 40)
        {
            Publish("source_invalid_ids");
            return;
        }

        var ids = videoIds.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (ids.Length is < 1 or > 2 || ids.Any(id => id.Length != 11 || !id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
        {
            Publish("source_invalid_ids");
            return;
        }

        var journey = new CancellationTokenSource(TimeSpan.FromMinutes(30));
        _sourceJourney = journey;
        _ = RunSourceJourneyAsync(ids, journey);
    }

    public void Resume()
    {
        EnsureAvailable();
        if (_preparation is not null || _player.Source is null || _state.Snapshot.Status == PlaybackStatus.Failed)
        {
            Publish("resume_unavailable");
            return;
        }

        try
        {
            _state.Play();
            _player.Play();
            Publish("play_requested");
        }
        catch (Exception)
        {
            Fail("native_media_error");
        }
    }

    public void Pause()
    {
        EnsureAvailable();
        CancelSourceJourney();
        CancelCommands();
        _player.Pause();
        _state.Pause();
        Publish("user_paused");
    }

    public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken)
    {
        EnsureAvailable();
        if (_player.Source is null || _duration is not { } duration || position < TimeSpan.Zero || position > duration)
        {
            Publish("seek_unavailable");
            return Task.CompletedTask;
        }

        _seek?.Cancel();
        using var seek = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        seek.CancelAfter(TimeSpan.FromSeconds(5));
        _seek = seek;
        var generation = _generation;
        try
        {
            seek.Token.ThrowIfCancellationRequested();
            _player.PlaybackSession.Position = position;
            if (!_disposed && generation == _generation && ReferenceEquals(_seek, seek))
            {
                _state.UpdatePosition(position);
                Publish("seek_completed");
            }
        }
        catch (OperationCanceledException)
        {
            if (!_disposed && generation == _generation && ReferenceEquals(_seek, seek))
                Publish(cancellationToken.IsCancellationRequested ? "seek_cancelled" : "seek_timeout");
        }
        catch (Exception)
        {
            if (!_disposed && generation == _generation && ReferenceEquals(_seek, seek))
                Publish("seek_failed");
        }
        finally
        {
            if (ReferenceEquals(_seek, seek)) _seek = null;
        }

        return Task.CompletedTask;
    }

    public void Stop()
    {
        EnsureAvailable();
        CancelSourceJourney();
        StopMedia();
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        _player.MediaOpened -= OnMediaOpened;
        _player.MediaFailed -= OnMediaFailed;
        _player.MediaEnded -= OnMediaEnded;
        _player.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;
        _player.PlaybackSession.PositionChanged -= OnPlaybackPositionChanged;
        _systemControls.ButtonPressed -= OnSystemButtonPressed;
        _systemControls.PlaybackPositionChangeRequested -= OnSystemPositionChangeRequested;
        _systemControls.IsEnabled = false;
        _player.Dispose();
        _sourceHttp.Dispose();
        Changed = null;
    }

    private async Task RunSourceJourneyAsync(string[] ids, CancellationTokenSource journey)
    {
        try
        {
            for (var index = 0; index < ids.Length && index < 2; index++)
            {
                journey.Token.ThrowIfCancellationRequested();
                Publish($"source_resolving_item_{index + 1}");
                var resolved = await _source.ResolveAsync(ids[index], journey.Token);
                if (!OwnsJourney(journey)) return;
                if (!resolved.IsSuccess)
                {
                    StopMedia();
                    Publish($"source_{resolved.Failure}_{resolved.Playability}");
                    return;
                }

                var refreshed = await _source.RefreshIfExpiredAsync(resolved.Value!, journey.Token);
                if (!OwnsJourney(journey)) return;
                if (!refreshed.IsSuccess)
                {
                    StopMedia();
                    Publish($"source_{refreshed.Failure}_{refreshed.Playability}");
                    return;
                }

                await LoadMediaAsync(refreshed.Value!.PlaybackUri.AbsoluteUri, journey.Token);
                if (!OwnsJourney(journey)) return;
                var ended = _itemEnded;
                if (ended is null || _state.Snapshot.Status is PlaybackStatus.Failed or PlaybackStatus.Empty)
                {
                    Publish("source_native_preparation_failed");
                    return;
                }

                Publish($"source_item_{index + 1}_loaded");
                if (!await ended.Task.WaitAsync(journey.Token))
                {
                    if (OwnsJourney(journey)) Publish("source_native_playback_failed");
                    return;
                }
            }

            if (OwnsJourney(journey))
            {
                StopMedia();
                Publish("source_journey_completed");
            }
        }
        catch (OperationCanceledException)
        {
            if (OwnsJourney(journey))
            {
                StopMedia();
                Publish("source_journey_timeout");
            }
        }
        catch (Exception)
        {
            if (OwnsJourney(journey))
            {
                StopMedia();
                Publish("source_journey_failed");
            }
        }
        finally
        {
            if (ReferenceEquals(_sourceJourney, journey)) _sourceJourney = null;
            journey.Dispose();
        }
    }

    private async Task LoadMediaAsync(string? address, CancellationToken cancellationToken)
    {
        EnsureAvailable();
        if (address is null || address.Length > 4096 || !Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
        {
            Publish("invalid_https_audio_address");
            return;
        }

        StopMedia();
        var generation = _generation;
        using var preparation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        preparation.CancelAfter(TimeSpan.FromSeconds(15));
        _preparation = preparation;
        _state.Load(new MediaTrack("m0-known-audio", "Lyrava Windows known audio"));
        UpdateSystemMetadata();
        var opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _mediaOpened = opened;
        _itemEnded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _player.Source = MediaSource.CreateFromUri(uri);
            Publish("preparing");
            var canPlay = await opened.Task.WaitAsync(preparation.Token);
            if (_disposed || generation != _generation) return;
            if (!canPlay)
            {
                Fail("media_preparation_failed");
                return;
            }

            var duration = _player.PlaybackSession.NaturalDuration;
            _duration = duration > TimeSpan.Zero ? duration : null;
            _state.Play();
            _player.Play();
            Publish("ready_play_requested");
        }
        catch (OperationCanceledException)
        {
            if (!_disposed && generation == _generation)
            {
                StopMedia();
                Publish(cancellationToken.IsCancellationRequested ? "cancelled" : "preparation_timeout");
            }
        }
        catch (Exception)
        {
            if (!_disposed && generation == _generation) Fail("native_media_error");
        }
        finally
        {
            if (ReferenceEquals(_preparation, preparation)) _preparation = null;
            if (ReferenceEquals(_mediaOpened, opened)) _mediaOpened = null;
        }
    }

    private TaskCompletionSource<bool>? _mediaOpened;

    private bool OwnsJourney(CancellationTokenSource journey) => !_disposed && ReferenceEquals(_sourceJourney, journey);

    private void CancelSourceJourney()
    {
        var journey = _sourceJourney;
        _sourceJourney = null;
        journey?.Cancel();
    }

    private void StopMedia()
    {
        CancelCommands();
        _player.Pause();
        _player.Source = null;
        _mediaOpened?.TrySetResult(false);
        _mediaOpened = null;
        _itemEnded?.TrySetResult(false);
        _itemEnded = null;
        _duration = null;
        _state.Stop();
        Publish("stopped");
    }

    private void Fail(string result)
    {
        CancelCommands();
        _player.Pause();
        _player.Source = null;
        _mediaOpened?.TrySetResult(false);
        _mediaOpened = null;
        _itemEnded?.TrySetResult(false);
        _itemEnded = null;
        _duration = null;
        _state.Fail();
        Publish(result);
    }

    private void CancelCommands()
    {
        _generation++;
        _preparation?.Cancel();
        _seek?.Cancel();
    }

    private void OnMediaOpened(MediaPlayer sender, object args) => _mediaOpened?.TrySetResult(true);

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args) => _mediaOpened?.TrySetResult(false);

    private void OnMediaEnded(MediaPlayer sender, object args)
    {
        _player.Pause();
        _state.Pause();
        Publish("ended");
        _itemEnded?.TrySetResult(true);
    }

    private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        var seconds = sender.Position.TotalSeconds;
        if (double.IsFinite(seconds) && seconds >= 0) _state.UpdatePosition(sender.Position);
        Publish(sender.PlaybackState.ToString());
    }

    private void OnPlaybackPositionChanged(MediaPlaybackSession sender, object args)
    {
        var seconds = sender.Position.TotalSeconds;
        if (double.IsFinite(seconds) && seconds >= 0)
        {
            _state.UpdatePosition(sender.Position);
            Publish(_result);
        }
    }

    private void OnSystemButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        // ButtonPressed is raised away from the UI thread. Keep all player commands on
        // the MAUI thread, and keep the public result limited to stable categories.
        Action? command = args.Button switch
        {
            SystemMediaTransportControlsButton.Play => Resume,
            SystemMediaTransportControlsButton.Pause => Pause,
            SystemMediaTransportControlsButton.Stop => Stop,
            _ => null
        };
        if (command is not null)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!_disposed) command();
            });
    }

    private void OnSystemPositionChangeRequested(SystemMediaTransportControls sender, PlaybackPositionChangeRequestedEventArgs args)
    {
        var position = args.RequestedPlaybackPosition;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_disposed) _ = SeekAsync(position, CancellationToken.None);
        });
    }

    private void Publish(string result)
    {
        _result = result;
        UpdateSystemControls();
        MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke(this, EventArgs.Empty));
    }

    private void UpdateSystemMetadata()
    {
        try
        {
            var updater = _systemControls.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = "Lyrava M0 known audio";
            updater.MusicProperties.Artist = "Lyrava";
            updater.Update();
        }
        catch (Exception)
        {
            // Metadata is advisory; a control-plane failure must not stop playback.
        }
    }

    private void UpdateSystemControls()
    {
        if (_disposed) return;
        try
        {
            _systemControls.PlaybackStatus = _player.PlaybackSession.PlaybackState switch
            {
                MediaPlaybackState.Playing => MediaPlaybackStatus.Playing,
                MediaPlaybackState.Paused => MediaPlaybackStatus.Paused,
                MediaPlaybackState.Opening or MediaPlaybackState.Buffering => MediaPlaybackStatus.Changing,
                _ => MediaPlaybackStatus.Stopped
            };

            if (_duration is not { } duration || duration <= TimeSpan.Zero) return;

            var position = _player.PlaybackSession.Position;
            if (position < TimeSpan.Zero) position = TimeSpan.Zero;
            if (position > duration) position = duration;
            var timeline = new SystemMediaTransportControlsTimelineProperties
            {
                StartTime = TimeSpan.Zero,
                MinSeekTime = TimeSpan.Zero,
                Position = position,
                MaxSeekTime = duration,
                EndTime = duration
            };
            _systemControls.UpdateTimelineProperties(timeline);
        }
        catch (Exception)
        {
            // System controls are best effort while the native player is opening or closing.
        }
    }

    private void EnsureAvailable() => ObjectDisposedException.ThrowIf(_disposed, this);
}
