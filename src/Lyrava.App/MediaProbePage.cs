using Lyrava.App.Platforms.iOS;

namespace Lyrava.App;

public sealed class MediaProbePage : ContentPage
{
    private readonly IosPlaybackService _playback;
    private readonly Entry _address = new()
    {
        Placeholder = "HTTPS audio URL (memory only)",
        MaxLength = 4096,
        Keyboard = Keyboard.Url,
        IsPassword = true,
        IsTextPredictionEnabled = false,
        ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
        AutomationId = "KnownAudioUrl"
    };
    private readonly Label _state = new() { AutomationId = "PlaybackState" };
    private readonly Entry _videoIds = new()
    {
        Placeholder = "One or two public video IDs, comma separated",
        MaxLength = 40,
        IsTextPredictionEnabled = false,
        IsPassword = true,
        ClearButtonVisibility = ClearButtonVisibility.WhileEditing
    };
    private readonly Slider _position = new() { Minimum = 0, Maximum = 1, IsEnabled = false };
    private readonly Button _play = new() { Text = "Load and play", AutomationId = "LoadAndPlay" };
    private CancellationTokenSource? _command;

    public MediaProbePage(IosPlaybackService playback)
    {
        _playback = playback;
        Title = "Lyrava · M0 media probe";
        var pause = new Button { Text = "Pause" };
        var resume = new Button { Text = "Resume" };
        var stop = new Button { Text = "Stop and release media" };
        var sourceJourney = new Button { Text = "Start two-item source probe" };
        _play.Clicked += async (_, _) => await RunAsync(ct => _playback.LoadAndPlayAsync(_address.Text, ct));
        pause.Clicked += (_, _) => _playback.Pause();
        resume.Clicked += (_, _) => _playback.Resume();
        stop.Clicked += (_, _) => { _command?.Cancel(); _playback.Stop(); _address.Text = string.Empty; };
        sourceJourney.Clicked += (_, _) =>
        {
            _command?.Cancel();
            _playback.StartSourceJourney(_videoIds.Text);
            _videoIds.Text = string.Empty;
        };
        _position.DragCompleted += async (_, _) => await RunAsync(ct => _playback.SeekAsync(TimeSpan.FromSeconds(_position.Value), ct));
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24, 40),
                Spacing = 14,
                Children =
                {
                    new Label { Text = "M0 · iOS media feasibility", FontSize = 26, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Device validation pending. This probe does not establish car, background or source compatibility." },
                    new Label { Text = "Use an audio file you may access. The URL stays in memory and is never included in status or diagnostics." },
                    _address, _play,
                    new Label { Text = "Source journey: at most two public IDs and 30 minutes. Item two resolves only after item one ends. Pause/stop/disconnect cancels the journey. Metadata and AVPlayer use their own system network paths." },
                    _videoIds, sourceJourney, pause, resume, _position, stop, _state
                }
            }
        };
        SemanticProperties.SetDescription(_position, "Playback position in seconds");
        RefreshState();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _playback.Changed += OnPlaybackChanged;
        RefreshState();
    }

    protected override void OnDisappearing()
    {
        _playback.Changed -= OnPlaybackChanged;
        base.OnDisappearing();
    }

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        _command?.Cancel();
        using var command = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        _command = command;
        _play.IsEnabled = false;
        try
        {
            await action(command.Token);
        }
        finally
        {
            if (ReferenceEquals(_command, command))
            {
                _command = null;
                _play.IsEnabled = true;
            }
            RefreshState();
        }
    }

    private void OnPlaybackChanged(object? sender, EventArgs e) => RefreshState();

    private void RefreshState()
    {
        var view = _playback.View;
        _state.Text = $"Intent: {view.Intent}\nNative: {view.NativeStatus}\nPosition: {view.Position.TotalSeconds:F0}s\nResult: {view.Result}";
        if (view.Duration is { TotalSeconds: > 0 } duration)
        {
            _position.IsEnabled = true;
            _position.Maximum = duration.TotalSeconds;
            _position.Value = Math.Clamp(view.Position.TotalSeconds, 0, duration.TotalSeconds);
        }
        else _position.IsEnabled = false;
    }
}
