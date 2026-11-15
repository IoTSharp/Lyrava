#if WINDOWS
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Lyrava.Sources.YouTube;

namespace Lyrava.App.Windows;

/// <summary>
/// Windows M1-01 搜索状态。网络结果只保留展示元数据，临时媒体地址仍由播放器按需解析。
/// </summary>
public sealed class WindowsSearchViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly YouTubeMusicSource _source;
    private CancellationTokenSource? _searchCancellation;
    private int _generation;
    private bool _isBusy;
    private string _statusText = "Enter a search term.";
    private bool _disposed;

    public WindowsSearchViewModel(YouTubeMusicSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<WindowsSearchResult> Results { get; } = [];

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public async Task SearchAsync(string? query, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var normalized = query?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            CancelSearch();
            Results.Clear();
            StatusText = "Enter a search term.";
            return;
        }

        if (normalized.Length > 200)
        {
            Results.Clear();
            StatusText = "Search text is too long.";
            return;
        }

        CancelSearch();
        var generation = ++_generation;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        _searchCancellation = timeout;
        Results.Clear();
        IsBusy = true;
        StatusText = "Searching...";

        try
        {
            var result = await _source.SearchAsync(normalized, timeout.Token).ConfigureAwait(true);
            if (generation != _generation || _disposed)
            {
                return;
            }

            if (!result.IsSuccess)
            {
                StatusText = $"Search failed: {result.Failure}.";
                return;
            }

            foreach (var track in result.Value!)
            {
                Results.Add(new WindowsSearchResult(track.Id, track.Title));
            }

            StatusText = Results.Count == 0
                ? "No results."
                : $"{Results.Count} result(s). Select one to queue.";
        }
        catch (OperationCanceledException)
        {
            if (generation == _generation && !_disposed)
            {
                StatusText = cancellationToken.IsCancellationRequested ? "Search cancelled." : "Search timed out.";
            }
        }
        catch (Exception)
        {
            if (generation == _generation && !_disposed)
            {
                StatusText = "Search failed: unexpected error.";
            }
        }
        finally
        {
            if (generation == _generation && !_disposed)
            {
                IsBusy = false;
                if (ReferenceEquals(_searchCancellation, timeout))
                {
                    _searchCancellation = null;
                }
            }
        }
    }

    public void CancelSearch()
    {
        _searchCancellation?.Cancel();
        _searchCancellation = null;
        _generation++;
        IsBusy = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelSearch();
        Results.Clear();
        PropertyChanged = null;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
#endif
