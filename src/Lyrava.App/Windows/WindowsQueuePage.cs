#if WINDOWS
using System.Collections.ObjectModel;
using Lyrava.Core.Media;
using Lyrava.Sources.YouTube;

namespace Lyrava.App.Windows;

/// <summary>Windows M1-02 queue controls. The coordinator owns order and native playback boundaries.</summary>
public sealed class WindowsQueuePage : ContentPage
{
    private readonly WindowsQueueCoordinator _queue;
    private readonly ObservableCollection<MediaTrack> _items = [];
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.None, AutomationId = "WindowsQueueItems" };
    private readonly Label _status = new() { AutomationId = "WindowsQueueStatus" };

    public WindowsQueuePage(WindowsQueueCoordinator queue)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        Title = "Queue";
        _list.ItemsSource = _items;
        _list.ItemTemplate = new DataTemplate(() =>
        {
            var title = new Label { FontSize = 16 };
            title.SetBinding(Label.TextProperty, nameof(MediaTrack.Title));
            var id = new Label { FontSize = 11, Opacity = 0.65 };
            id.SetBinding(Label.TextProperty, nameof(MediaTrack.Id));
            return new Border
            {
                Padding = new Thickness(12, 8),
                Margin = new Thickness(0, 2),
                Content = new VerticalStackLayout { Spacing = 2, Children = { title, id } }
            };
        });

        var play = new Button { Text = "Play current", AutomationId = "WindowsQueuePlay" };
        var previous = new Button { Text = "Previous", AutomationId = "WindowsQueuePrevious" };
        var next = new Button { Text = "Next", AutomationId = "WindowsQueueNext" };
        var stop = new Button { Text = "Stop", AutomationId = "WindowsQueueStop" };
        var clear = new Button { Text = "Clear", AutomationId = "WindowsQueueClear" };
        play.Clicked += async (_, _) => await RunAsync(_queue.PlayCurrentAsync);
        previous.Clicked += async (_, _) => await RunAsync(_queue.PreviousAsync);
        next.Clicked += async (_, _) => await RunAsync(_queue.NextAsync);
        stop.Clicked += (_, _) => { _queue.Stop(); Refresh(); };
        clear.Clicked += (_, _) => { _queue.Clear(); Refresh(); };
        _queue.Changed += OnQueueChanged;

        Content = new VerticalStackLayout
        {
            Padding = new Thickness(24, 32),
            Spacing = 12,
            Children =
            {
                new Label { Text = "Windows queue", FontSize = 26, FontAttributes = FontAttributes.Bold },
                _status,
                new HorizontalStackLayout { Spacing = 8, Children = { play, previous, next, stop, clear } },
                _list
            }
        };
        Refresh();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    public void SetMessage(string message)
    {
        _status.Text = message ?? string.Empty;
    }

    private async Task RunAsync(Func<CancellationToken, Task<QueuePlaybackResult>> command)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var result = await command(timeout.Token);
        _status.Text = result.Started
            ? $"Playing: {result.Track!.Title}"
            : result.DidSeek
                ? "Seeked to start."
                : result.WasCancelled
                    ? "Command cancelled."
                    : result.Failure == SourceFailure.None ? "No queue item." : $"Playback failed: {result.Failure}.";
        Refresh();
    }

    private void OnQueueChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(Refresh);

    private void Refresh()
    {
        _items.Clear();
        foreach (var item in _queue.Items) _items.Add(item);
        _status.Text = _queue.Current is { } current
            ? $"Current: {current.Title} ({_queue.CurrentIndex + 1}/{_queue.Items.Count})"
            : "Queue is empty.";
    }
}
#endif
