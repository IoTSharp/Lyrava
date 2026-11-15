using AVFoundation;
using CoreFoundation;
using CoreMedia;
using Foundation;
using Lyrava.Core.Media;
using Lyrava.Core.Playback;
using Lyrava.Sources.YouTube;
using MediaPlayer;

namespace Lyrava.App.Platforms.iOS;

/// <summary>A single application-owned native player for M0; no URL or native error text is logged.</summary>
public sealed class IosPlaybackService : IMediaPlaybackService
{
    private readonly PlaybackSessionState _state = new();
    private readonly AVPlayer _player = new();
    private readonly AVAudioSession _audio = AVAudioSession.SharedInstance();
    private readonly HttpClient _sourceHttp = new(new HttpClientHandler { UseCookies = false }) { Timeout = TimeSpan.FromSeconds(12) };
    private readonly YouTubeMusicSource _source;
    private readonly List<(MPRemoteCommand Command, NSObject Target)> _remoteTargets = [];
    private readonly NSObject _interruptionObserver;
    private readonly NSObject _routeObserver;
    private readonly NSObject _timeObserver;
    private AVPlayerItem? _item;
    private IDisposable? _statusObserver;
    private NSObject? _endObserver;
    private CancellationTokenSource? _preparation;
    private CancellationTokenSource? _seek;
    private CancellationTokenSource? _sourceJourney;
    private Task? _sourceJourneyTask;
    private TaskCompletionSource<bool>? _itemEnded;
    private long _generation;
    private bool _disposed;
    private bool _isInterrupted;
    private string _result = "not_validated";
    private TimeSpan? _duration;

    public IosPlaybackService()
    {
        EnsureMainThread();
        _source = new YouTubeMusicSource(_sourceHttp);
        _interruptionObserver = AVAudioSession.Notifications.ObserveInterruption((_, args) =>
            OnMain(() => OnInterruption(args.InterruptionType, args.Option)));
        _routeObserver = AVAudioSession.Notifications.ObserveRouteChange((_, args) =>
        {
            if (args.Reason == AVAudioSessionRouteChangeReason.OldDeviceUnavailable)
                OnMain(() => { CancelSourceJourney(); CancelCommands(); _player.Pause(); _state.RouteDisconnected(); Publish("route_disconnected"); });
        });
        _timeObserver = _player.AddPeriodicTimeObserver(CMTime.FromSeconds(1, 600), DispatchQueue.MainQueue, _ => OnTick());
        var commands = MPRemoteCommandCenter.Shared;
        Register(commands.PlayCommand, Resume);
        Register(commands.PauseCommand, Pause);
        Register(commands.StopCommand, Stop);
        Register(commands.TogglePlayPauseCommand, () =>
        {
            if (_state.Snapshot.Status == PlaybackStatus.Playing) Pause(); else Resume();
        });
        // No queue exists in M0. Unsupported remote commands remain disabled.
        commands.NextTrackCommand.Enabled = false;
        commands.PreviousTrackCommand.Enabled = false;
    }

    public event EventHandler? Changed;

    public MediaProbeView View => new(_state.Snapshot.Status.ToString(),
        _disposed ? "disposed" : _player.TimeControlStatus.ToString(),
        _state.Snapshot.Position, _duration, _result);

    public async Task LoadAndPlayAsync(string? address, CancellationToken cancellationToken)
    {
        EnsureAvailable();
        if (_isInterrupted) { Publish("audio_interrupted"); return; }
        CancelSourceJourney();
        await LoadMediaAsync(address, cancellationToken);
    }

    public void StartSourceJourney(string? videoIds)
    {
        EnsureAvailable();
        if (_isInterrupted) { Publish("audio_interrupted"); return; }
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
        // This task belongs to the application singleton, including when the page is hidden.
        _sourceJourneyTask = RunSourceJourneyAsync(ids, journey);
    }

    private async Task RunSourceJourneyAsync(string[] ids, CancellationTokenSource journey)
    {
        try
        {
            // At most two items, with the shared 30-minute deadline and 10-second request deadlines.
            for (var index = 0; index < ids.Length && index < 2; index++)
            {
                journey.Token.ThrowIfCancellationRequested();
                Publish($"source_resolving_item_{index + 1}");
                var resolved = await _source.ResolveAsync(ids[index], journey.Token);
                if (!OwnsJourney(journey)) return;
                if (!resolved.IsSuccess) { StopMedia(); Publish($"source_{resolved.Failure}_{resolved.Playability}"); return; }
                var refreshed = await _source.RefreshIfExpiredAsync(resolved.Value!, journey.Token);
                if (!OwnsJourney(journey)) return;
                if (!refreshed.IsSuccess) { StopMedia(); Publish($"source_{refreshed.Failure}_{refreshed.Playability}"); return; }
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
            if (OwnsJourney(journey)) { StopMedia(); Publish("source_journey_completed"); }
        }
        catch (OperationCanceledException)
        {
            if (OwnsJourney(journey)) { StopMedia(); Publish("source_journey_timeout"); }
        }
        catch (Exception)
        {
            if (OwnsJourney(journey)) { StopMedia(); Publish("source_journey_failed"); }
        }
        finally
        {
            if (ReferenceEquals(_sourceJourney, journey)) _sourceJourney = null;
            journey.Dispose();
        }
    }

    private bool OwnsJourney(CancellationTokenSource journey) => !_disposed && ReferenceEquals(_sourceJourney, journey);

    private void CancelSourceJourney()
    {
        var journey = _sourceJourney;
        _sourceJourney = null;
        journey?.Cancel();
    }

    private async Task LoadMediaAsync(string? address, CancellationToken cancellationToken)
    {
        EnsureAvailable();
        if (_isInterrupted) { Publish("audio_interrupted"); return; }
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
        _state.Load(new MediaTrack("m0-known-audio", "Lyrava M0 known audio"));
        try
        {
            ActivateAudio();
            using var nativeUrl = new NSUrl(uri.AbsoluteUri);
            var item = AVPlayerItem.FromUrl(nativeUrl);
            _item = item;
            _itemEnded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void CheckStatus()
            {
                if (item.Status == AVPlayerItemStatus.ReadyToPlay) ready.TrySetResult(true);
                else if (item.Status == AVPlayerItemStatus.Failed) ready.TrySetResult(false);
            }

            _statusObserver = item.AddObserver("status", NSKeyValueObservingOptions.New, _ => CheckStatus());
            _endObserver = AVPlayerItem.Notifications.ObserveDidPlayToEndTime(item, (_, _) => OnMain(() =>
            {
                if (!ReferenceEquals(item, _item)) return;
                _player.Pause();
                _state.Pause();
                Publish("ended");
                _itemEnded?.TrySetResult(true);
            }));
            _player.ReplaceCurrentItemWithPlayerItem(item);
            CheckStatus();
            Publish("preparing");
            var canPlay = await ready.Task.WaitAsync(preparation.Token);
            if (_disposed || generation != _generation) return;
            _statusObserver?.Dispose();
            _statusObserver = null;
            if (!canPlay) { Fail("media_preparation_failed"); return; }
            var seconds = item.Duration.Seconds;
            _duration = double.IsFinite(seconds) && seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;
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
        }
    }

    public void Resume()
    {
        EnsureAvailable();
        // A remote Play during preparation cannot bypass item readiness.
        if (_isInterrupted || _preparation is not null || _item?.Status != AVPlayerItemStatus.ReadyToPlay || _state.Snapshot.Status == PlaybackStatus.Failed)
        {
            Publish("resume_unavailable");
            return;
        }
        try
        {
            ActivateAudio();
            // A preparation may have been paused before its readiness continuation ran.
            var seconds = _item.Duration.Seconds;
            _duration = double.IsFinite(seconds) && seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;
            _state.Play();
            _player.Play();
            Publish("play_requested");
        }
        catch (Exception) { Fail("audio_session_unavailable"); }
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

    public async Task SeekAsync(TimeSpan position, CancellationToken cancellationToken)
    {
        EnsureAvailable();
        if (_item?.Status != AVPlayerItemStatus.ReadyToPlay || _duration is not { } duration || position < TimeSpan.Zero || position > duration)
        {
            Publish("seek_unavailable");
            return;
        }
        _seek?.Cancel();
        _item.CancelPendingSeeks();
        using var seek = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        seek.CancelAfter(TimeSpan.FromSeconds(5));
        _seek = seek;
        var generation = _generation;
        try
        {
            var completed = await _player.SeekAsync(CMTime.FromSeconds(position.TotalSeconds, 600)).WaitAsync(seek.Token);
            if (!_disposed && generation == _generation && ReferenceEquals(_seek, seek))
            {
                if (completed) _state.UpdatePosition(position);
                Publish(completed ? "seek_completed" : "seek_interrupted");
            }
        }
        catch (OperationCanceledException)
        {
            if (!_disposed && generation == _generation && ReferenceEquals(_seek, seek))
            {
                _item?.CancelPendingSeeks();
                Publish(cancellationToken.IsCancellationRequested ? "seek_cancelled" : "seek_timeout");
            }
        }
        catch (Exception)
        {
            if (!_disposed && generation == _generation && ReferenceEquals(_seek, seek)) Publish("seek_failed");
        }
        finally { if (ReferenceEquals(_seek, seek)) _seek = null; }
    }

    public void Stop()
    {
        EnsureAvailable();
        CancelSourceJourney();
        StopMedia();
    }

    private void StopMedia()
    {
        CancelCommands();
        _player.Pause();
        _statusObserver?.Dispose();
        _statusObserver = null;
        _endObserver?.Dispose();
        _endObserver = null;
        _player.ReplaceCurrentItemWithPlayerItem(null);
        _item?.Dispose();
        _item = null;
        _itemEnded?.TrySetResult(false);
        _itemEnded = null;
        _duration = null;
        _state.Stop();
        MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = new MPNowPlayingInfo();
        _audio.SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out _);
        Publish("stopped");
    }

    public void Dispose()
    {
        EnsureMainThread();
        if (_disposed) return;
        Stop();
        _disposed = true;
        _interruptionObserver.Dispose();
        _routeObserver.Dispose();
        _player.RemoveTimeObserver(_timeObserver);
        _timeObserver.Dispose();
        foreach (var (command, target) in _remoteTargets)
        {
            command.RemoveTarget(target);
            target.Dispose();
            command.Enabled = false;
        }
        _remoteTargets.Clear();
        _player.Dispose();
        _sourceHttp.Dispose();
        Changed = null;
    }

    private void ActivateAudio()
    {
        using var categoryError = _audio.SetCategory(AVAudioSessionCategory.Playback, AVAudioSessionCategoryOptions.AllowBluetoothA2DP);
        if (categoryError is not null ||
            !_audio.SetActive(true, out _))
            throw new InvalidOperationException("Audio session unavailable.");
    }

    private void OnInterruption(AVAudioSessionInterruptionType type, AVAudioSessionInterruptionOptions options)
    {
        if (type == AVAudioSessionInterruptionType.Began)
        {
            _isInterrupted = true;
            // Cancel resolving the next item too; it has no native Playing state to interrupt yet.
            CancelSourceJourney();
            CancelCommands();
            _state.BeginInterruption();
            _player.Pause();
            Publish("interruption_began");
            return;
        }
        _isInterrupted = false;
        _state.EndInterruption(options.HasFlag(AVAudioSessionInterruptionOptions.ShouldResume));
        if (_state.Snapshot.Status == PlaybackStatus.Playing)
        {
            try { ActivateAudio(); _player.Play(); }
            catch (Exception) { Fail("interruption_resume_failed"); return; }
        }
        Publish("interruption_ended");
    }

    private void OnTick()
    {
        if (_disposed || _item is null) return;
        if (_item.Status == AVPlayerItemStatus.Failed) { Fail("native_player_failed"); return; }
        var seconds = _player.CurrentTime.Seconds;
        if (double.IsFinite(seconds) && seconds >= 0) _state.UpdatePosition(TimeSpan.FromSeconds(seconds));
        Publish(_result);
    }

    private void Fail(string result)
    {
        CancelCommands();
        _player.Pause();
        _endObserver?.Dispose();
        _endObserver = null;
        _player.ReplaceCurrentItemWithPlayerItem(null);
        _item?.Dispose();
        _item = null;
        _itemEnded?.TrySetResult(false);
        _itemEnded = null;
        _duration = null;
        _audio.SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out _);
        _state.Fail();
        Publish(result);
    }

    private void Publish(string result)
    {
        _result = result;
        var state = _state.Snapshot;
        if (state.Track is not null)
        {
            MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = new MPNowPlayingInfo
            {
                Title = "Lyrava M0 known audio",
                ElapsedPlaybackTime = state.Position.TotalSeconds,
                PlaybackDuration = _duration?.TotalSeconds ?? 0,
                PlaybackRate = _player.Rate
            };
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Register(MPRemoteCommand command, Action action)
    {
        command.Enabled = true;
        var target = command.AddTarget(_ =>
        {
            if (_disposed) return MPRemoteCommandHandlerStatus.CommandFailed;
            OnMain(action);
            return MPRemoteCommandHandlerStatus.Success;
        });
        _remoteTargets.Add((command, target));
    }

    private void CancelCommands()
    {
        _generation++;
        _preparation?.Cancel();
        _seek?.Cancel();
        _item?.CancelPendingSeeks();
        _statusObserver?.Dispose();
        _statusObserver = null;
    }

    private void OnMain(Action action) => MainThread.BeginInvokeOnMainThread(() => { if (!_disposed) action(); });

    private void EnsureAvailable()
    {
        EnsureMainThread();
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static void EnsureMainThread()
    {
        if (!MainThread.IsMainThread) throw new InvalidOperationException("Media commands require the iOS main thread.");
    }
}
