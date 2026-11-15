using Lyrava.Core.Media;

namespace Lyrava.Core.Playback;

/// <summary>
/// A thread-safe, platform-neutral queue for the Windows first delivery slice.
/// The queue stores metadata only; native media resources remain platform-owned.
/// </summary>
public sealed class PlaybackQueue
{
    private readonly object _gate = new();
    private readonly List<MediaTrack> _items = [];
    private int _currentIndex = -1;

    public IReadOnlyList<MediaTrack> Items
    {
        get
        {
            lock (_gate)
            {
                return _items.ToArray();
            }
        }
    }

    public int CurrentIndex
    {
        get
        {
            lock (_gate)
            {
                return _currentIndex;
            }
        }
    }

    public MediaTrack? Current
    {
        get
        {
            lock (_gate)
            {
                return GetCurrentCore();
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _items.Count;
            }
        }
    }

    public int Enqueue(MediaTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        lock (_gate)
        {
            _items.Add(track);
            if (_currentIndex < 0)
            {
                _currentIndex = 0;
            }

            return _items.Count - 1;
        }
    }

    public void Insert(int index, MediaTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        lock (_gate)
        {
            if ((uint)index > (uint)_items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            _items.Insert(index, track);
            if (_currentIndex >= index)
            {
                _currentIndex++;
            }
            else if (_currentIndex < 0)
            {
                _currentIndex = 0;
            }
        }
    }

    public bool Select(int index)
    {
        lock (_gate)
        {
            if ((uint)index >= (uint)_items.Count)
            {
                return false;
            }

            _currentIndex = index;
            return true;
        }
    }

    public bool Move(int fromIndex, int toIndex)
    {
        lock (_gate)
        {
            if ((uint)fromIndex >= (uint)_items.Count || (uint)toIndex >= (uint)_items.Count || fromIndex == toIndex)
            {
                return false;
            }

            var item = _items[fromIndex];
            _items.RemoveAt(fromIndex);
            _items.Insert(toIndex, item);
            _currentIndex = AdjustIndexAfterMove(_currentIndex, fromIndex, toIndex);
            return true;
        }
    }

    public bool RemoveAt(int index)
    {
        lock (_gate)
        {
            if ((uint)index >= (uint)_items.Count)
            {
                return false;
            }

            _items.RemoveAt(index);
            if (_items.Count == 0)
            {
                _currentIndex = -1;
            }
            else if (_currentIndex > index)
            {
                _currentIndex--;
            }
            else if (_currentIndex == index)
            {
                _currentIndex = Math.Min(index, _items.Count - 1);
            }

            return true;
        }
    }

    public bool MoveNext()
    {
        lock (_gate)
        {
            if (_currentIndex < 0 || _currentIndex + 1 >= _items.Count)
            {
                return false;
            }

            _currentIndex++;
            return true;
        }
    }

    public bool MovePrevious()
    {
        lock (_gate)
        {
            if (_currentIndex <= 0)
            {
                return false;
            }

            _currentIndex--;
            return true;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _items.Clear();
            _currentIndex = -1;
        }
    }

    private MediaTrack? GetCurrentCore() => _currentIndex is >= 0 and < int.MaxValue && _currentIndex < _items.Count
        ? _items[_currentIndex]
        : null;

    private static int AdjustIndexAfterMove(int currentIndex, int fromIndex, int toIndex)
    {
        if (currentIndex < 0 || currentIndex == fromIndex)
        {
            return currentIndex == fromIndex ? toIndex : currentIndex;
        }

        if (fromIndex < currentIndex && currentIndex <= toIndex)
        {
            return currentIndex - 1;
        }

        if (toIndex <= currentIndex && currentIndex < fromIndex)
        {
            return currentIndex + 1;
        }

        return currentIndex;
    }
}
